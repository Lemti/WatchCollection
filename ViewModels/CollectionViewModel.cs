using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

/// <summary>
/// Représente une colonne sélectionnable pour l'export CSV.
/// </summary>
public partial class ExportColumn : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public partial class CollectionViewModel : ViewModelBase
{
    private readonly MongoDBService _mongo = new();

    public IRelayCommand<ObjectId> FromParentCommand { get; set; }
    public ObservableCollection<Watch> MyObservableWatches { get; }
    public ObservableCollection<ExportColumn> ExportColumns { get; } = [];

    [ObservableProperty] private Watch? _selectedWatch;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showExportPanel;
    [ObservableProperty] private bool _isBusy;

    public CollectionViewModel(IRelayCommand<ObjectId> fromParentCommand)
    {
        FromParentCommand = fromParentCommand;
        MyObservableWatches = [];

        foreach (var watch in Globals.MyWatches)
            MyObservableWatches.Add(watch);

        InitializeExportColumns();
    }

    public CollectionViewModel()
    {
        FromParentCommand = null!;
        MyObservableWatches = [];
        InitializeExportColumns();
    }

    private void InitializeExportColumns()
    {
        ExportColumns.Add(new ExportColumn { Name = "Barcode", DisplayName = "Code-barre" });
        ExportColumns.Add(new ExportColumn { Name = "Brand", DisplayName = "Marque" });
        ExportColumns.Add(new ExportColumn { Name = "Model", DisplayName = "Modèle" });
        ExportColumns.Add(new ExportColumn { Name = "Reference", DisplayName = "Référence" });
        ExportColumns.Add(new ExportColumn { Name = "Movement", DisplayName = "Mouvement" });
        ExportColumns.Add(new ExportColumn { Name = "Diameter", DisplayName = "Diamètre" });
        ExportColumns.Add(new ExportColumn { Name = "CaseMaterial", DisplayName = "Matériau" });
        ExportColumns.Add(new ExportColumn { Name = "Price", DisplayName = "Prix" });
        ExportColumns.Add(new ExportColumn { Name = "Year", DisplayName = "Année" });
        ExportColumns.Add(new ExportColumn { Name = "Stock", DisplayName = "Stock" });
    }

    private static TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    [RelayCommand]
    private void ToggleExportPanel() => ShowExportPanel = !ShowExportPanel;

    [RelayCommand]
    private async Task ConfirmExport()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            StatusMessage = "Impossible d'accéder au gestionnaire de fichiers.";
            return;
        }

        var selectedColumnNames = ExportColumns
            .Where(c => c.IsSelected)
            .Select(c => c.Name)
            .ToList();

        if (selectedColumnNames.Count == 0)
        {
            StatusMessage = "Sélectionnez au moins une colonne.";
            return;
        }

        var csvService = new CsvServices(topLevel);
        var result = await csvService.SaveDataAsync(Globals.MyWatches, selectedColumnNames);

        if (result.UserCancelled)
        {
            ShowExportPanel = false;
            return;
        }

        if (result.Success)
        {
            StatusMessage = $"{result.ExportedCount} montres exportées ({selectedColumnNames.Count} colonnes).";
            ShowExportPanel = false;
        }
        else
        {
            StatusMessage = $"Erreur export : {result.ErrorMessage}";
        }
    }

    /// <summary>
    /// Importe une collection depuis un fichier CSV avec déduplication intelligente :
    ///
    /// 1) Match prioritaire par Barcode (clé d'unicité métier primaire)
    /// 2) Fallback par triplet Brand+Model+Reference (clé naturelle composite)
    /// 3) Sinon insertion comme nouvelle montre
    ///
    /// Quand un match est trouvé, on applique une fusion enrichissante (PATCH semantics) :
    /// seuls les champs présents et non vides du CSV mettent à jour la montre existante.
    /// Si la montre mise à jour devient identique (Brand+Model+Reference) à une autre
    /// montre du user, on fusionne aussi les stocks et on supprime le doublon.
    /// </summary>
    [RelayCommand]
    private async Task ImportCsv()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            StatusMessage = "Impossible d'accéder au gestionnaire de fichiers.";
            return;
        }

        var csvService = new CsvServices(topLevel);
        var result = await csvService.LoadDataAsync();

        if (result.UserCancelled)
            return;

        if (!string.IsNullOrEmpty(result.GlobalError))
        {
            StatusMessage = $"Erreur import : {result.GlobalError}";
            return;
        }

        if (Globals.CurrentUser is null)
        {
            StatusMessage = "Aucun utilisateur connecté pour l'import.";
            return;
        }

        try
        {
            IsBusy = true;

            var addedCount = 0;
            var updatedCount = 0;
            var mergedCount = 0;
            var dbErrorCount = 0;
            var incompleteWatches = new List<string>();

            foreach (var importedWatch in result.Watches)
            {
                Watch? existing = null;

                // Étape 1 : recherche locale par Barcode (instance déjà en UI)
                if (!string.IsNullOrWhiteSpace(importedWatch.Barcode))
                {
                    existing = Globals.MyWatches.FirstOrDefault(w =>
                        !string.IsNullOrWhiteSpace(w.Barcode) &&
                        w.Barcode.Equals(importedWatch.Barcode, StringComparison.OrdinalIgnoreCase));
                }

                // Étape 2 : fallback locale par triplet Brand + Model + Reference
                if (existing is null
                    && !string.IsNullOrWhiteSpace(importedWatch.Brand)
                    && !string.IsNullOrWhiteSpace(importedWatch.Model)
                    && !string.IsNullOrWhiteSpace(importedWatch.Reference))
                {
                    existing = Globals.MyWatches.FirstOrDefault(w =>
                        w.Brand.Equals(importedWatch.Brand, StringComparison.OrdinalIgnoreCase)
                        && w.Model.Equals(importedWatch.Model, StringComparison.OrdinalIgnoreCase)
                        && w.Reference.Equals(importedWatch.Reference, StringComparison.OrdinalIgnoreCase));
                }

                if (existing is not null)
                {
                    // PATCH : fusion enrichissante in-place
                    MergeInto(existing, importedWatch);

                    // Vérification de fusion identitaire :
                    // après PATCH, le triplet est-il devenu identique à une AUTRE montre ?
                    var twin = FindIdentityTwin(existing);

                    if (twin is not null)
                    {
                        // Fusion : on cumule les stocks et on supprime le jumeau
                        existing.Stock += twin.Stock;
                        if (string.IsNullOrWhiteSpace(existing.Barcode) && !string.IsNullOrWhiteSpace(twin.Barcode))
                            existing.Barcode = twin.Barcode;

                        if (Globals.IsDatabaseAvailable)
                            await _mongo.DeleteWatchAsync(twin.Id);

                        Globals.MyWatches.Remove(twin);
                        MyObservableWatches.Remove(twin);

                        mergedCount++;
                    }

                    var updated = Globals.IsDatabaseAvailable
                        ? await _mongo.UpdateWatchAsync(existing)
                        : true;

                    if (updated)
                        updatedCount++;
                    else
                        dbErrorCount++;
                }
                else
                {
                    // INSERT : nouvelle montre
                    if (importedWatch.Id == ObjectId.Empty)
                        importedWatch.Id = ObjectId.GenerateNewId();

                    var saved = Globals.IsDatabaseAvailable
                        ? await _mongo.AddWatchAsync(importedWatch, Globals.CurrentUser)
                        : true;

                    if (saved)
                    {
                        addedCount++;
                        Globals.MyWatches.Add(importedWatch);
                        MyObservableWatches.Add(importedWatch);

                        if (IsIncomplete(importedWatch, out var label))
                            incompleteWatches.Add(label);
                    }
                    else
                    {
                        dbErrorCount++;
                    }
                }
            }

            // Construction du message de feedback détaillé
            var parts = new List<string>();

            if (addedCount > 0) parts.Add($"{addedCount} ajoutée(s)");
            if (updatedCount > 0) parts.Add($"{updatedCount} mise(s) à jour");
            if (mergedCount > 0) parts.Add($"{mergedCount} fusionnée(s)");
            if (result.LineErrors.Count > 0) parts.Add($"{result.LineErrors.Count} ligne(s) ignorée(s)");
            if (dbErrorCount > 0) parts.Add($"{dbErrorCount} non sauvegardée(s)");
            if (!Globals.IsDatabaseAvailable) parts.Add("(mode hors-ligne)");

            var summary = parts.Count > 0 ? string.Join(", ", parts) + "." : "Aucune montre importée.";

            if (incompleteWatches.Count > 0)
            {
                var preview = string.Join(", ", incompleteWatches.Take(3));
                if (incompleteWatches.Count > 3) preview += $", + {incompleteWatches.Count - 3} autre(s)";
                summary += $" À compléter : {preview}.";
            }

            StatusMessage = summary;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de l'import : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Fusion enrichissante : les champs non vides de <paramref name="source"/>
    /// écrasent ceux de <paramref name="target"/>. Les champs vides ou par défaut
    /// du source sont ignorés (les valeurs existantes du target sont préservées).
    /// </summary>
    private static void MergeInto(Watch target, Watch source)
    {
        if (!string.IsNullOrWhiteSpace(source.Barcode)) target.Barcode = source.Barcode;
        if (!string.IsNullOrWhiteSpace(source.Brand)) target.Brand = source.Brand;
        if (!string.IsNullOrWhiteSpace(source.Model)) target.Model = source.Model;
        if (!string.IsNullOrWhiteSpace(source.Reference)) target.Reference = source.Reference;
        if (!string.IsNullOrWhiteSpace(source.Movement)) target.Movement = source.Movement;
        if (source.Diameter > 0) target.Diameter = source.Diameter;
        if (!string.IsNullOrWhiteSpace(source.CaseMaterial)) target.CaseMaterial = source.CaseMaterial;
        if (source.Price > 0) target.Price = source.Price;
        if (source.Year > 0) target.Year = source.Year;
        if (source.Stock > 0) target.Stock = source.Stock;
    }

    /// <summary>
    /// Détecte si une montre nouvellement importée est incomplète (champs essentiels manquants).
    /// Retourne true et un libellé descriptif si oui.
    /// </summary>
    private static bool IsIncomplete(Watch watch, out string label)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(watch.Barcode)) missing.Add("Code-barre");
        if (string.IsNullOrWhiteSpace(watch.Reference)) missing.Add("Référence");
        if (watch.Diameter <= 0) missing.Add("Diamètre");
        if (watch.Price <= 0) missing.Add("Prix");

        if (missing.Count == 0)
        {
            label = string.Empty;
            return false;
        }

        var brandModel = string.IsNullOrWhiteSpace(watch.Model)
            ? watch.Brand
            : $"{watch.Brand} {watch.Model}";

        if (string.IsNullOrWhiteSpace(brandModel)) brandModel = "Montre sans nom";

        label = $"{brandModel} (manque : {string.Join(", ", missing)})";
        return true;
    }

    /// <summary>
    /// Cherche dans la collection une AUTRE montre (différente de <paramref name="watch"/>)
    /// qui a le même triplet identifiant Brand+Model+Reference. Renvoie null si aucune trouvée.
    /// </summary>
    private static Watch? FindIdentityTwin(Watch watch)
    {
        if (string.IsNullOrWhiteSpace(watch.Brand) ||
            string.IsNullOrWhiteSpace(watch.Model) ||
            string.IsNullOrWhiteSpace(watch.Reference))
            return null;

        return Globals.MyWatches.FirstOrDefault(w =>
            w.Id != watch.Id
            && w.Brand.Equals(watch.Brand, StringComparison.OrdinalIgnoreCase)
            && w.Model.Equals(watch.Model, StringComparison.OrdinalIgnoreCase)
            && w.Reference.Equals(watch.Reference, StringComparison.OrdinalIgnoreCase));
    }
}