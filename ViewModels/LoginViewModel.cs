using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly Action _onLoginSuccess;
    private MongoDbService? _mongoDbService;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _firstName = "";
    [ObservableProperty] private string _lastName = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isRegistering;
    [ObservableProperty] private bool _isMongoAvailable = true;
    public Avalonia.Media.IImage? WatchHeroImage { get; }

    public LoginViewModel(Action onLoginSuccess)
    {
        _onLoginSuccess = onLoginSuccess;
        _ = CheckMongoConnection();
        try { WatchHeroImage = WatchCollection.Helpers.ImageHelper.LoadFromResource(new Uri("avares://WatchCollection/Assets/watch-hero.png")); } catch { }
    }

    public LoginViewModel() { _onLoginSuccess = () => { }; }

    private async Task CheckMongoConnection()
    {
        try
        {
            _mongoDbService = new MongoDbService();
            await _mongoDbService.GetAllUsersAsync();
            IsMongoAvailable = true;
        }
        catch
        {
            IsMongoAvailable = false;
            ErrorMessage = "MongoDB indisponible. Vous pouvez continuer sans compte.";
        }
    }

    [RelayCommand]
    private async Task Login()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Veuillez remplir tous les champs.";
            return;
        }

        try
        {
            _mongoDbService ??= new MongoDbService();
            var user = await _mongoDbService.LoginAsync(Email, Password);
            if (user != null)
            {
                MyGlobals.CurrentUser = user;
                _onLoginSuccess();
            }
            else
            {
                ErrorMessage = "Email ou mot de passe incorrect.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de connexion à MongoDB: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Veuillez remplir tous les champs.";
            return;
        }

        try
        {
            _mongoDbService ??= new MongoDbService();
            var user = new User
            {
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                Password = Password,
                Role = "user"
            };

            var success = await _mongoDbService.RegisterAsync(user);
            if (success)
            {
                ErrorMessage = "Inscription réussie ! Connectez-vous.";
                IsRegistering = false;
            }
            else
            {
                ErrorMessage = "Un compte avec cet email existe déjà.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleRegister()
    {
        IsRegistering = !IsRegistering;
        ErrorMessage = "";
    }

    [RelayCommand]
    private void SkipLogin()
    {
        // Mode sans MongoDB : créer un utilisateur local
        MyGlobals.CurrentUser = new User
        {
            FirstName = "Utilisateur",
            LastName = "Local",
            Email = "local@watchcollection.app",
            Role = "admin"
        };
        _onLoginSuccess();
    }
}