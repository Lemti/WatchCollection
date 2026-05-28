using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

// ViewModel de la page d'ajout de montres.
// Connecte automatiquement le scanner USB à l'ouverture, gère la validation
// des champs et la persistance MongoDB (collection privée de l'utilisateur).
public partial class AddWatchViewModel : ViewModelBase
{
    private readonly Action? _onWatchAdded;
    private readonly ScannerManager _scanner = new();
    private readonly MongoDBService _mongoDbService = new();

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

        // Connexion automatique du scanner à l'ouverture de la page.
        // Si aucun scanner n'est branché, on continue sans bloquer (saisie manuelle possible).
        TryConnectScanner();
    }

    // Constructeur sans paramètre requis par le designer Avalonia.
    public AddWatchViewModel()
    {
    }

    // Tente de connecter le scanner. Non bloquant : en cas d'échec,
    // l'utilisateur peut toujours saisir le code-barre à la main.
    private void TryConnectScanner()
    {
        if (_scanner.TryOpenPort(out var error))
        {
            IsScannerConnected = true;
            StatusMessage = "Scanner connecté, en attente de scan…";
        }
        else
        {
            IsScannerConnected = false;
            StatusMessage = error ?? "Aucun scanner détecté, saisie manuelle disponible.";
        }
    }

    private void OnBarcodeScanned(object? sender, string barcode)
    {
        // Le SerialPort déclenche DataReceived sur un thread du pool, pas sur le thread UI.
        // On repasse donc sur le thread UI pour modifier une propriété bindée sans crash.
        Dispatcher.UIThread.Post(() =>
        {
            Barcode = barcode;
            StatusMessage = $"Code-barre scanné : {barcode}";
        });
    }

    // Ajoute la montre. Persiste en MongoDB si connecté, sinon en mémoire (hors-ligne).
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

    // Valide les champs avant ajout pour éviter d'enregistrer une montre incomplète.
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
        // Déconnexion automatique du scanner quand on quitte la page.
        _scanner.BarcodeScanned -= OnBarcodeScanned;
        _scanner.Dispose();
        base.Dispose();
    }
}