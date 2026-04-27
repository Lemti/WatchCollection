using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    private readonly MongoDBService _mongo = new();

    public ObservableCollection<User> Users { get; } = [];

    [ObservableProperty] private string _newFirstName = string.Empty;
    [ObservableProperty] private string _newLastName = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isDatabaseAvailable;

    public UsersViewModel()
    {
        IsDatabaseAvailable = _mongo.IsConnected;

        if (!IsDatabaseAvailable)
        {
            StatusMessage = "MongoDB indisponible — la gestion des utilisateurs nécessite une connexion.";
            return;
        }

        _ = LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            IsBusy = true;
            Users.Clear();

            var users = await _mongo.GetAllUsersAsync();
            foreach (var user in users)
                Users.Add(user);

            StatusMessage = $"{Users.Count} utilisateur(s) chargé(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur de chargement : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshUsers() => await LoadUsersAsync();

    [RelayCommand]
    private async Task AddUser()
    {
        if (!IsValid(out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        try
        {
            IsBusy = true;

            var newUser = new User
            {
                DisplayName = $"{NewFirstName.Trim()} {NewLastName.Trim()}",
                FirstName = NewFirstName.Trim(),
                LastName = NewLastName.Trim(),
                Email = NewEmail.Trim(),
                HashedPassword = NewPassword
            };

            var success = await _mongo.RegisterAsync(newUser);

            if (success)
            {
                StatusMessage = $"Utilisateur '{newUser.Email}' ajouté.";
                ClearForm();
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = "Un compte avec cet email existe déjà.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de l'ajout : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteUser(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusMessage = "Email invalide pour la suppression.";
            return;
        }

        if (Globals.CurrentUser?.Email == email)
        {
            StatusMessage = "Vous ne pouvez pas supprimer le compte actuellement connecté.";
            return;
        }

        try
        {
            IsBusy = true;

            var success = await _mongo.DeleteUserAsync(email);

            if (success)
            {
                StatusMessage = $"Utilisateur '{email}' supprimé.";
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = "La suppression a échoué.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de la suppression : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(NewFirstName))
        {
            error = "Le prénom est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(NewLastName))
        {
            error = "Le nom est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(NewEmail) || !NewEmail.Contains('@'))
        {
            error = "Email invalide.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
        {
            error = "Le mot de passe doit contenir au moins 4 caractères.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ClearForm()
    {
        NewFirstName = string.Empty;
        NewLastName = string.Empty;
        NewEmail = string.Empty;
        NewPassword = string.Empty;
    }
}