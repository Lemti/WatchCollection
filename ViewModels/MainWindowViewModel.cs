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

    [RelayCommand]
    private void Logout()
    {
        Globals.CurrentUser = null;
        IsLoggedIn = false;
        IsAdmin = false;
        CurrentUserName = string.Empty;
        StatusMessage = string.Empty;
        CurrentPage = new LoginViewModel(OnLoginSuccessRequested);
    }

    // ===== Callbacks =====
    // Ces méthodes sont des callbacks invoqués par les enfants. Elles déclenchent
    // une opération asynchrone via une commande dédiée (LoadAfterLoginCommand)
    // au lieu d'utiliser async void (anti-pattern).

    private void OnLoginSuccessRequested()
    {
        IsLoggedIn = true;
        IsAdmin = Globals.IsAdmin;
        CurrentUserName = $"{Globals.CurrentUser?.FirstName} {Globals.CurrentUser?.LastName}".Trim();
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
    /// Charge les montres depuis le serveur JSON après connexion.
    /// En cas d'échec réseau, charge des données d'exemple.
    /// </summary>
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
/// Charge depuis le serveur JSON du professeur (mode hors-ligne MongoDB).
/// </summary>
private async Task LoadFromJsonServer()
{
    try
    {
        var watches = await _jsonServices.GetWatchesAsync();

        if (watches is { Count: > 0 })
        {
            Globals.MyWatches = watches;
            StatusMessage = $"{watches.Count} montre(s) chargée(s) depuis le serveur JSON.";
        }
        else
        {
            LoadSampleData();
            StatusMessage = "Serveur vide — données d'exemple chargées.";
        }
    }
    catch (System.Net.Http.HttpRequestException)
    {
        LoadSampleData();
        StatusMessage = "Pas de connexion réseau — mode hors-ligne avec données d'exemple.";
    }
}

    /// <summary>
    /// Sauvegarde la collection sur le serveur JSON après ajout, puis retourne à la liste.
    /// </summary>
    [RelayCommand]
    private async Task SaveAndReturn()
    {
        try
        {
            await _jsonServices.SetWatchesAsync(Globals.MyWatches);
            StatusMessage = "Collection sauvegardée sur le serveur.";
        }
        catch (System.Net.Http.HttpRequestException)
        {
            StatusMessage = "Sauvegarde locale uniquement — serveur indisponible.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur de sauvegarde : {ex.Message}";
        }
        finally
        {
            CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
        }
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