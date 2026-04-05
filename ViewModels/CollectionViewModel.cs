using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class ExportColumn : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public partial class CollectionViewModel : ViewModelBase
{
    public IRelayCommand<ObjectId> FromParentCommand { get; set; }
    public ObservableCollection<Watch> MyObservableWatches { get; }
    public ObservableCollection<ExportColumn> ExportColumns { get; } = [];

    [ObservableProperty] private Watch? _selectedWatch;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showExportPanel;

    public CollectionViewModel(IRelayCommand<ObjectId> fromParentCommand)
    {
        FromParentCommand = fromParentCommand;
        MyObservableWatches = [];

        foreach (var watch in MyGlobals.MyWatches)
        {
            MyObservableWatches.Add(new Watch
            {
                Id = watch.Id, Barcode = watch.Barcode, Brand = watch.Brand,
                Model = watch.Model, Reference = watch.Reference, Movement = watch.Movement,
                Diameter = watch.Diameter, CaseMaterial = watch.CaseMaterial,
                Price = watch.Price, Year = watch.Year, Stock = watch.Stock,
                Picture = watch.Picture
            });
        }

        // Colonnes exportables
        ExportColumns.Add(new ExportColumn { Name = "Barcode", DisplayName = "Code-barre", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Brand", DisplayName = "Marque", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Model", DisplayName = "Modèle", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Reference", DisplayName = "Référence", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Movement", DisplayName = "Mouvement", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Diameter", DisplayName = "Diamètre", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "CaseMaterial", DisplayName = "Matériau", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Price", DisplayName = "Prix", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Year", DisplayName = "Année", IsSelected = true });
        ExportColumns.Add(new ExportColumn { Name = "Stock", DisplayName = "Stock", IsSelected = true });
    }

    public CollectionViewModel()
    {
        FromParentCommand = null!;
        MyObservableWatches = [];
    }

    private TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    [RelayCommand]
    private void ToggleExportPanel()
    {
        ShowExportPanel = !ShowExportPanel;
    }

    [RelayCommand]
    private async Task ConfirmExport()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel == null) return;

            var selectedColumns = ExportColumns.Where(c => c.IsSelected).ToList();
            if (selectedColumns.Count == 0)
            {
                StatusMessage = "Sélectionnez au moins une colonne.";
                return;
            }

            var csv = new StringBuilder();
            // Header
            csv.AppendLine(string.Join(";", selectedColumns.Select(c => c.DisplayName)));

            // Data
            foreach (var watch in MyGlobals.MyWatches)
            {
                var values = new List<string>();
                foreach (var col in selectedColumns)
                {
                    var prop = typeof(Watch).GetProperty(col.Name);
                    values.Add(prop?.GetValue(watch)?.ToString() ?? "");
                }
                csv.AppendLine(string.Join(";", values));
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Enregistrer le fichier CSV",
                SuggestedFileName = "watches.csv"
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                using var writer = new System.IO.StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(csv.ToString());
                StatusMessage = $"{MyGlobals.MyWatches.Count} montres exportées ({selectedColumns.Count} colonnes).";
            }

            ShowExportPanel = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur export: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportCsv()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel == null) return;
            var csvService = new CsvServices(topLevel);
            var imported = await csvService.LoadDataAsync();
            foreach (var watch in imported)
            {
                if (watch.Id == ObjectId.Empty) watch.Id = ObjectId.GenerateNewId();
                MyGlobals.MyWatches.Add(watch);
                MyObservableWatches.Add(watch);
            }
            if (imported.Count > 0)
                StatusMessage = $"{imported.Count} montres importées.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur import: {ex.Message}";
        }
    }
}