using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Helpers;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isDatabaseAvailable;
    [ObservableProperty] private string _currentUserName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>
    /// Version affichée dans l'UI (exigence : versioning visible à l'écran).
    /// </summary>
    public string AppVersion => "Version 1.1";

    private readonly JSONServices _jsonServices = new();
    private readonly MongoDBService _mongoDbService = new();

    /// <summary>
    /// Constructeur synchrone : aucune opération async ici.
    /// L'initialisation asynchrone se fait via InitializeAsync(), appelée explicitement depuis App.
    /// </summary>
    public MainWindowViewModel()
    {
        CurrentPage = new LoginViewModel(OnLoginSuccessRequested);
        IsDatabaseAvailable = Globals.IsDatabaseAvailable;
    }

    /// <summary>
    /// Méthode d'initialisation asynchrone. À appeler après construction.
    /// Sépare proprement la construction synchrone de toute opération réseau ou I/O.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;

    // ===== Navigation entre pages =====

    [RelayCommand]
    private void GoToCollection() => CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);

    [RelayCommand]
    private void GoToAddWatch() => CurrentPage = new AddWatchViewModel(OnWatchAddedRequested);

    [RelayCommand]
    private void GoToUsers() => CurrentPage = new UsersViewModel();

    [RelayCommand]
    private void GoToCharts() => CurrentPage = new ChartsViewModel();

    [RelayCommand]
    private void GoToDetailsFromChild(ObjectId watchId)
    {
        CurrentPage = new CollectionDetailsViewModel(
            watchId,
            onBack: GoToCollection,
            onDeleted: GoToCollection);
    }

    [RelayCommand]
    private void BackToMain() => GoToCollection();

    /// <summary>
    /// Déconnexion : on nettoie la session pour qu'un nouveau login (notamment hors-ligne)
    /// reparte d'un état propre, sans conserver les montres de la session précédente en mémoire.
    /// </summary>
    [RelayCommand]
    private void Logout()
    {
        Globals.CurrentUser = null;
        Globals.MyWatches.Clear();
        IsLoggedIn = false;
        IsAdmin = false;
        IsDatabaseAvailable = false;
        CurrentUserName = string.Empty;
        StatusMessage = string.Empty;
        CurrentPage = new LoginViewModel(OnLoginSuccessRequested);
    }

    // ===== Callbacks =====

    private void OnLoginSuccessRequested()
    {
        IsLoggedIn = true;
        IsAdmin = Globals.IsAdmin;
        IsDatabaseAvailable = Globals.IsDatabaseAvailable;

        // Préfère DisplayName s'il est défini (cas hors-ligne où First/Last sont vides),
        // sinon "Prénom Nom", sinon fallback "Utilisateur Local"
        CurrentUserName = !string.IsNullOrWhiteSpace(Globals.CurrentUser?.DisplayName)
            ? Globals.CurrentUser.DisplayName
            : $"{Globals.CurrentUser?.FirstName} {Globals.CurrentUser?.LastName}".Trim();

        if (string.IsNullOrWhiteSpace(CurrentUserName))
            CurrentUserName = "Utilisateur Local";

        // Lance le chargement asynchrone via une commande (gestion d'erreurs propre par CommunityToolkit)
        LoadAfterLoginCommand.Execute(null);
    }

    private void OnWatchAddedRequested()
    {
        // Lance la sauvegarde JSON et le retour à la collection via une commande asynchrone
        SaveAndReturnCommand.Execute(null);
    }

    /// <summary>
    /// Charge les montres après connexion, en priorité depuis MongoDB
    /// (collection privée — cahier v4.0). Si la base est indisponible,
    /// fallback sur le serveur JSON, puis sur les données d'exemple.
    /// </summary>
    [RelayCommand]
    private async Task LoadAfterLogin()
    {
        try
        {
            // Stratégie en cascade : MongoDB > JSON serveur > données d'exemple
            if (Globals.IsDatabaseAvailable && Globals.CurrentUser is not null)
            {
                await LoadFromDatabase();
            }
            else
            {
                await LoadFromJsonServer();
            }
        }
        catch (Exception ex)
        {
            LoadSampleData();
            StatusMessage = $"Erreur de chargement : {ex.Message}. Données d'exemple chargées.";
        }
        finally
        {
            CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
        }
    }

    /// <summary>
    /// Charge la collection privée de l'utilisateur depuis MongoDB.
    /// Admin : toutes les montres de la base. User : uniquement ses montres.
    /// </summary>
    private async Task LoadFromDatabase()
    {
        var watches = await _mongoDbService.GetWatchesForUserAsync(Globals.CurrentUser!);

        if (watches.Count > 0)
        {
            Globals.MyWatches = watches;
            StatusMessage = $"{watches.Count} montre(s) chargée(s) depuis votre collection.";
        }
        else
        {
            Globals.MyWatches = [];
            StatusMessage = "Votre collection est vide. Ajoutez votre première montre !";
        }
    }

    /// <summary>
    /// Charge depuis le serveur JSON académique (mode hors-ligne MongoDB).
    /// Le service JSON gère lui-même tous les modes d'échec (réseau, corruption,
    /// timeout, coupure de flux) et expose le détail via LastError.
    /// </summary>
    private async Task LoadFromJsonServer()
    {
        var watches = await _jsonServices.GetWatchesAsync();

        if (watches.Count > 0)
        {
            Globals.MyWatches = watches;
            StatusMessage = $"{watches.Count} montre(s) chargée(s) depuis le serveur JSON.";
        }
        else if (!string.IsNullOrEmpty(_jsonServices.LastError))
        {
            // Erreur capturée par le service (réseau, JSON corrompu, timeout, etc.)
            LoadSampleData();
            StatusMessage = $"Serveur JSON indisponible ({_jsonServices.LastError}). Données d'exemple chargées.";
        }
        else
        {
            // Réponse vide sans erreur (premier accès au serveur)
            LoadSampleData();
            StatusMessage = "Serveur vide — données d'exemple chargées.";
        }
    }

    /// <summary>
    /// Retour à la liste après ajout d'une montre. La persistance MongoDB est déjà
    /// faite par AddWatchAsync dans AddWatchViewModel (collection privée — cahier v4).
    /// Le serveur JSON académique est utilisé en lecture seule comme fallback de résilience,
    /// jamais en écriture, pour éviter la pollution croisée entre étudiants partageant le serveur.
    /// </summary>
    [RelayCommand]
    private void SaveAndReturn()
    {
        StatusMessage = Globals.IsDatabaseAvailable
            ? "Montre ajoutée à votre collection."
            : "Montre ajoutée localement (mode hors-ligne, non persistée).";

        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
    }
    // ===== Cycle de vie =====

    /// <summary>
    /// Quand la page courante change, on dispose proprement la précédente
    /// pour éviter les fuites mémoire (notamment pour le scanner dans AddWatch).
    /// </summary>
    partial void OnCurrentPageChanging(ViewModelBase? oldValue, ViewModelBase? newValue)
    {
        oldValue?.Dispose();
    }

    // ===== Données d'exemple (mode hors-ligne) =====

    private void LoadSampleData()
    {
        Globals.MyWatches.Clear();

        Globals.MyWatches.Add(CreateSampleWatch("5711-1A-010", "Patek Philippe", "Nautilus", "5711/1A-010",
            "Automatic", 40, "Acier", 35000, 2021, 1, "nautilus.png"));
        Globals.MyWatches.Add(CreateSampleWatch("126610LN", "Rolex", "Submariner", "126610LN",
            "Automatic", 41, "Acier Oystersteel", 9150, 2023, 2, "submariner.png"));
        Globals.MyWatches.Add(CreateSampleWatch("SRPD55K1", "Seiko", "Seiko 5 Sports", "SRPD55K1",
            "Automatic", 42.5, "Acier", 299, 2024, 5, "seiko5.png"));
        Globals.MyWatches.Add(CreateSampleWatch("GA2100-1A1", "Casio", "G-Shock CasiOak", "GA-2100-1A1",
            "Quartz", 45.4, "Résine carbone", 99, 2022, 10, "casioak.png"));
        Globals.MyWatches.Add(CreateSampleWatch("31030425", "Omega", "Speedmaster Moonwatch",
            "310.30.42.50.01.002", "Manual", 42, "Acier", 6900, 2023, 3, "speedmaster.png"));
    }

    private static Watch CreateSampleWatch(string barcode, string brand, string model, string reference,
        string movement, double diameter, string caseMaterial, decimal price, int year, int stock,
        string imageFileName)
    {
        return new Watch
        {
            Id = ObjectId.GenerateNewId(),
            Barcode = barcode,
            Brand = brand,
            Model = model,
            Reference = reference,
            Movement = movement,
            Diameter = diameter,
            CaseMaterial = caseMaterial,
            Price = price,
            Year = year,
            Stock = stock,
            Picture = LoadImageOrNull(imageFileName)
        };
    }

    private static Avalonia.Media.IImage? LoadImageOrNull(string fileName)
    {
        try
        {
            return ImageHelper.LoadFromResource(new Uri($"avares://WatchCollection/Assets/{fileName}"));
        }
        catch
        {
            return null;
        }
    }
}