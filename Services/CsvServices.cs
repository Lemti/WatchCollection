using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MongoDB.Bson;
using WatchCollection.Models;

namespace WatchCollection.Services;

/// <summary>
/// Service d'import/export CSV pour la collection. Gère les fichiers corrompus,
/// les colonnes manquantes, les valeurs invalides et les caractères spéciaux.
/// </summary>
public class CsvServices
{
    private const char Separator = ';';
    private readonly TopLevel _topLevel;

    public CsvServices(TopLevel topLevel)
    {
        _topLevel = topLevel;
    }

    /// <summary>
    /// Charge une collection depuis un fichier CSV choisi par l'utilisateur.
    /// </summary>
    /// <returns>Résultat contenant la liste chargée et les éventuelles erreurs ligne par ligne.</returns>
    public async Task<CsvImportResult> LoadDataAsync()
    {
        var result = new CsvImportResult();

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sélectionnez un fichier CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Fichier CSV") { Patterns = ["*.csv"] }]
        });

        if (files.Count == 0)
        {
            result.UserCancelled = true;
            return result;
        }

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Lecture complète en async (corrige CA2024 : pas de EndOfStream dans une méthode async)
            var content = await reader.ReadToEndAsync();
            var lines = content
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                result.GlobalError = "Le fichier est vide.";
                return result;
            }

            // Validation de l'en-tête (corrige le crash sur null - CS8602)
            var headerLine = lines[0];
            var headers = headerLine.Split(Separator);
            if (headers.Length == 0)
            {
                result.GlobalError = "L'en-tête du fichier CSV est invalide.";
                return result;
            }

            var properties = typeof(Watch).GetProperties();

            for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                var values = line.Split(Separator);

                try
                {
                    var watch = ParseLine(headers, values, properties);
                    result.Watches.Add(watch);
                }
                catch (Exception ex)
                {
                    // On note l'erreur mais on continue à parser les autres lignes
                    result.LineErrors.Add($"Ligne {lineIndex + 1} : {ex.Message}");
                }
            }
        }
        catch (FormatException ex)
        {
            result.GlobalError = $"Format de fichier invalide : {ex.Message}";
        }
        catch (IOException ex)
        {
            result.GlobalError = $"Erreur de lecture du fichier : {ex.Message}";
        }
        catch (Exception ex)
        {
            result.GlobalError = $"Erreur inattendue : {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Sauvegarde une liste d'objets dans un fichier CSV choisi par l'utilisateur.
    /// </summary>
    /// <returns>true si la sauvegarde a réussi, false sinon (avec message d'erreur).</returns>
    public async Task<CsvExportResult> SaveDataAsync<T>(List<T> data, IEnumerable<string>? selectedColumns = null)
    {
        var result = new CsvExportResult();

        if (data.Count == 0)
        {
            result.ErrorMessage = "Aucune donnée à exporter.";
            return result;
        }

        try
        {
            var allProperties = typeof(T).GetProperties();
            var propertiesToExport = selectedColumns is null
                ? allProperties
                : allProperties.Where(p => selectedColumns.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

            if (propertiesToExport.Length == 0)
            {
                result.ErrorMessage = "Aucune colonne sélectionnée pour l'export.";
                return result;
            }

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(Separator, propertiesToExport.Select(p => EscapeCsvValue(p.Name))));

            foreach (var item in data)
            {
                var values = propertiesToExport.Select(p =>
                    EscapeCsvValue(p.GetValue(item)?.ToString() ?? string.Empty));
                csv.AppendLine(string.Join(Separator, values));
            }

            var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Enregistrer le fichier CSV",
                SuggestedFileName = "watches.csv",
                DefaultExtension = "csv"
            });

            if (file is null)
            {
                result.UserCancelled = true;
                return result;
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(csv.ToString());

            result.Success = true;
            result.ExportedCount = data.Count;
        }
        catch (UnauthorizedAccessException)
        {
            result.ErrorMessage = "Accès refusé au fichier de destination.";
        }
        catch (IOException ex)
        {
            result.ErrorMessage = $"Erreur d'écriture : {ex.Message}";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Erreur inattendue : {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parse une ligne CSV en objet Watch en respectant les types de propriétés.
    /// </summary>
    private static Watch ParseLine(string[] headers, string[] values, PropertyInfo[] properties)
    {
        var watch = new Watch();

        var maxIndex = Math.Min(headers.Length, values.Length);
        for (var j = 0; j < maxIndex; j++)
        {
            var header = headers[j].Trim();
            var rawValue = values[j].Trim();

            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(rawValue))
                continue;

            var property = properties.FirstOrDefault(p =>
                p.Name.Equals(header, StringComparison.OrdinalIgnoreCase));

            if (property is null || !property.CanWrite)
                continue;

            try
            {
                var convertedValue = ConvertValue(rawValue, property.PropertyType);
                if (convertedValue is not null)
                    property.SetValue(watch, convertedValue);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Colonne '{header}' avec valeur '{rawValue}' invalide : {ex.Message}");
            }
        }

        // Si pas d'Id en source, on en génère un nouveau pour éviter ObjectId.Empty
        if (watch.Id == ObjectId.Empty)
            watch.Id = ObjectId.GenerateNewId();

        return watch;
    }

    /// <summary>
    /// Convertit une valeur string vers le type cible avec gestion des cas particuliers.
    /// </summary>
    private static object? ConvertValue(string rawValue, Type targetType)
    {
        // Type nullable : on récupère le type sous-jacent
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(ObjectId))
            return ObjectId.TryParse(rawValue, out var id) ? id : ObjectId.GenerateNewId();

        if (underlyingType == typeof(double) || underlyingType == typeof(float) || underlyingType == typeof(decimal))
        {
            // Accepte la virgule ET le point comme séparateur décimal (locale FR vs EN)
            var normalized = rawValue.Replace(',', '.');
            return Convert.ChangeType(normalized, underlyingType, CultureInfo.InvariantCulture);
        }

        if (underlyingType.IsEnum)
            return Enum.Parse(underlyingType, rawValue, ignoreCase: true);

        return Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Échappe les valeurs contenant le séparateur, des guillemets ou des sauts de ligne.
    /// </summary>
    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(Separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}

/// <summary>
/// Résultat d'un import CSV avec détail des erreurs ligne par ligne.
/// </summary>
public class CsvImportResult
{
    public List<Watch> Watches { get; } = [];
    public List<string> LineErrors { get; } = [];
    public string? GlobalError { get; set; }
    public bool UserCancelled { get; set; }
    public bool HasErrors => LineErrors.Count > 0 || !string.IsNullOrEmpty(GlobalError);
}

/// <summary>
/// Résultat d'un export CSV.
/// </summary>
public class CsvExportResult
{
    public bool Success { get; set; }
    public int ExportedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool UserCancelled { get; set; }
}