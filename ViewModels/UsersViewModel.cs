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
    private readonly MongoDbService _mongoDbService = new();

    public ObservableCollection<User> Users { get; } = [];

    [ObservableProperty] private string _newFirstName = "";
    [ObservableProperty] private string _newLastName = "";
    [ObservableProperty] private string _newEmail = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _statusMessage = "";

    public UsersViewModel()
    {
        _ = LoadUsers();
    }

    private async Task LoadUsers()
    {
        try
        {
            Users.Clear();
            var users = await _mongoDbService.GetAllUsersAsync();
            foreach (var user in users) Users.Add(user);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddUser()
    {
        if (string.IsNullOrWhiteSpace(NewFirstName) || string.IsNullOrWhiteSpace(NewLastName) ||
            string.IsNullOrWhiteSpace(NewEmail) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Veuillez remplir tous les champs.";
            return;
        }

        try
        {
            var user = new User
            {
                FirstName = NewFirstName,
                LastName = NewLastName,
                Email = NewEmail,
                Password = NewPassword,
                Role = "user"
            };

            var success = await _mongoDbService.RegisterAsync(user);
            if (success)
            {
                StatusMessage = "Utilisateur ajouté !";
                NewFirstName = ""; NewLastName = ""; NewEmail = ""; NewPassword = "";
                await LoadUsers();
            }
            else
            {
                StatusMessage = "Un compte avec cet email existe déjà.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteUser(string email)
    {
        try
        {
            await _mongoDbService.DeleteUserAsync(email);
            StatusMessage = "Utilisateur supprimé.";
            await LoadUsers();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
        }
    }
}
