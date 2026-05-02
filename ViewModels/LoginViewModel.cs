using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchCollection.Helpers;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly Action _onLoginSuccess;
    private readonly MongoDBService _mongo;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isRegistering;
    [ObservableProperty] private bool _isMongoAvailable;
    [ObservableProperty] private bool _isBusy;

    public Avalonia.Media.IImage? WatchHeroImage { get; }

    public LoginViewModel(Action onLoginSuccess)
    {
        _onLoginSuccess = onLoginSuccess;
        _mongo = new MongoDBService();
        IsMongoAvailable = _mongo.IsConnected;

        if (!IsMongoAvailable)
        {
            var detail = _mongo.LastError ?? "raison inconnue";
            ErrorMessage = $"MongoDB indisponible : {detail}";
        }

        try
        {
            WatchHeroImage = ImageHelper.LoadFromResource(
                new Uri("avares://WatchCollection/Assets/watch-hero.png"));
        }
        catch
        {
        }
    }

    public LoginViewModel()
    {
        _onLoginSuccess = () => { };
        _mongo = new MongoDBService();
    }

    /// <summary>
    /// Commande dispatchée par la touche Enter sur les champs du formulaire.
    /// Route vers Login ou Register selon le mode actif.
    /// </summary>
    [RelayCommand]
    private async Task Submit()
    {
        if (IsRegistering)
            await Register();
        else
            await Login();
    }

    [RelayCommand]
    private async Task Login()
    {
        ErrorMessage = string.Empty;

        if (!IsValidLogin(out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        if (!IsMongoAvailable)
        {
            ErrorMessage = "Connexion impossible : MongoDB indisponible.";
            return;
        }

        try
        {
            IsBusy = true;
            var user = await _mongo.LoginAsync(Email.Trim(), Password);

            if (user is not null)
            {
                Globals.CurrentUser = user;
                _onLoginSuccess();
            }
            else
            {
                ErrorMessage = "Email ou mot de passe incorrect.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de connexion : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        ErrorMessage = string.Empty;

        if (!IsValidRegister(out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        if (!IsMongoAvailable)
        {
            ErrorMessage = "Inscription impossible : MongoDB indisponible.";
            return;
        }

        try
        {
            IsBusy = true;

            var newUser = new User
            {
                DisplayName = $"{FirstName.Trim()} {LastName.Trim()}",
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = Email.Trim(),
                HashedPassword = Password
            };

            var success = await _mongo.RegisterAsync(newUser);

            if (success)
            {
                ErrorMessage = "Inscription réussie. Vous pouvez maintenant vous connecter.";
                IsRegistering = false;
                ClearRegisterFields();
            }
            else
            {
                ErrorMessage = "Un compte avec cet email existe déjà.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur d'inscription : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleRegister()
    {
        IsRegistering = !IsRegistering;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void SkipLogin()
    {
        // Mode hors-ligne / démo : bypass de l'authentification MongoDB.
        // L'utilisateur a Role=User (pas Admin) pour rester cohérent avec l'absence
        // d'authentification réelle. Les opérations CRUD restent en mémoire,
        // les modifications ne sont pas persistées en base de données.
        Globals.CurrentUser = new User
        {
            DisplayName = "Mode Hors-ligne",
            Role = "User"
        };
        _onLoginSuccess();
    }

    private bool IsValidLogin(out string error)
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            error = "L'email est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            error = "Le mot de passe est obligatoire.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool IsValidRegister(out string error)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            error = "Le prénom est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(LastName))
        {
            error = "Le nom est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            error = "Email invalide.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
        {
            error = "Le mot de passe doit contenir au moins 4 caractères.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ClearRegisterFields()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Password = string.Empty;
    }
}