using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

/// <summary>
/// ViewModel de la page d'ajout de montres.
/// Gère le scanner USB, la validation des champs et la persistance MongoDB
/// (avec lien automatique vers la collection privée de l'utilisateur — cahier v4.0).
/// </summary>
public partial class AddWatchViewModel : ViewModelBase
{
    private readonly Action _onWatchAdded;
    private readonly ScannerManager _scanner = new();
    private readonly MongoDBService _mongoDbService = new();

    /// <summary>
    /// Options disponibles pour le type de mouvement (cahier : auto, quartz, manuel).
    /// </summary>
    public string[] MovementOptions { get; } = ["Automatic", "Quartz", "Manual"];

    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private string _movement = "Automatic";
    [ObservableProperty] private double _diameter;
    [ObservableProperty] private string _caseMaterial = string.Empty;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _year = DateTime.Now.Year;
    [ObservableProperty] private int _stock = 1;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isScannerConnected;

    public AddWatchViewModel(Action onWatchAdded)
    {
        _onWatchAdded = onWatchAdded;
        _scanner.BarcodeScanned += OnBarcodeScanned;
    }

    /// <summary>
    /// Constructeur sans paramètre pour le designer Avalonia uniquement.
    /// </summary>
    public AddWatchViewModel()
    {
        _onWatchAdded = () => { };
    }

    private void OnBarcodeScanned(object? sender, string barcode)
    {
        // Le scanner émet sur un thread non-UI, on bascule sur le thread UI pour modifier la propriété bindée
        Dispatcher.UIThread.Post(() =>
        {
            Barcode = barcode;
            StatusMessage = $"Code-barre scanné : {barcode}";
        });
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        if (_scanner.TryOpenPort(out var error))
        {
            IsScannerConnected = true;
            StatusMessage = "Scanner connecté. En attente de scan...";
        }
        else
        {
            IsScannerConnected = false;
            StatusMessage = error ?? "Erreur de connexion au scanner.";
        }
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        _scanner.ClosePort();
        IsScannerConnected = false;
        StatusMessage = "Scanner déconnecté.";
    }

    /// <summary>
    /// Ajoute la montre à la collection. Persiste en MongoDB si connecté,
    /// sinon ajoute uniquement en mémoire (mode hors-ligne).
    /// </summary>
    [RelayCommand]
    private async Task AddWatch()
    {
        if (!IsValid(out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        try
        {
            var newWatch = new Watch
            {
                Id = ObjectId.GenerateNewId(),
                Barcode = Barcode.Trim(),
                Brand = Brand.Trim(),
                Model = Model.Trim(),
                Reference = Reference.Trim(),
                Movement = Movement,
                Diameter = Diameter,
                CaseMaterial = CaseMaterial.Trim(),
                Price = Price,
                Year = Year,
                Stock = Stock
            };

            // Persistance : MongoDB en priorité, sinon ajout en mémoire (mode hors-ligne)
            if (Globals.IsDatabaseAvailable && Globals.CurrentUser is not null)
            {
                var saved = await _mongoDbService.AddWatchAsync(newWatch, Globals.CurrentUser);
                if (!saved)
                {
                    StatusMessage = "Erreur lors de l'ajout en base de données.";
                    return;
                }
                StatusMessage = "Montre ajoutée à votre collection.";
            }
            else
            {
                StatusMessage = "Montre ajoutée localement (hors-ligne).";
            }

            Globals.MyWatches.Add(newWatch);

            // Petit délai pour que l'utilisateur voie le message de confirmation
            await Task.Delay(800);
            _onWatchAdded?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de l'ajout : {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => _onWatchAdded?.Invoke();

    /// <summary>
    /// Valide les champs avant ajout. Empêche les "champs corrompus" (commentaire prof).
    /// </summary>
    private bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(Barcode))
        {
            error = "Le code-barre est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Brand))
        {
            error = "La marque est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Model))
        {
            error = "Le modèle est obligatoire.";
            return false;
        }
        if (Diameter is < 20 or > 60)
        {
            error = "Le diamètre doit être entre 20 et 60 mm.";
            return false;
        }
        if (Price < 0)
        {
            error = "Le prix ne peut pas être négatif.";
            return false;
        }
        if (Year < 1900 || Year > DateTime.Now.Year + 1)
        {
            error = $"L'année doit être entre 1900 et {DateTime.Now.Year + 1}.";
            return false;
        }
        if (Stock < 0)
        {
            error = "Le stock ne peut pas être négatif.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public new void Dispose()
    {
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.Dispose();
        base.Dispose();
    }
}