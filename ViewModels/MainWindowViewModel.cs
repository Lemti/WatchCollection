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
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _currentUserName = "";

    private readonly JSONServices _jsonServices = new();

    public MainWindowViewModel()
    {
        CurrentPage = new LoginViewModel(OnLoginSuccess);
    }

    private async void OnLoginSuccess()
    {
        IsLoggedIn = true;
        IsAdmin = MyGlobals.IsAdmin;
        CurrentUserName = $"{MyGlobals.CurrentUser?.FirstName} {MyGlobals.CurrentUser?.LastName}";
        await LoadWatchesFromServer();
        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
    }

    private async Task LoadWatchesFromServer()
    {
        try
        {
            var watches = await _jsonServices.GetWatchesAsync();
            if (watches.Count > 0)
                MyGlobals.MyWatches = watches;
            else
                LoadSampleData();
        }
        catch { LoadSampleData(); }
    }

    private void LoadSampleData()
    {
        MyGlobals.MyWatches.Clear();
        MyGlobals.MyWatches.Add(new Watch
        {
            Id = ObjectId.GenerateNewId(), Barcode = "5711-1A-010",
            Brand = "Patek Philippe", Model = "Nautilus", Reference = "5711/1A-010",
            Movement = "Automatic", Diameter = 40, CaseMaterial = "Acier",
            Price = 35000, Year = 2021, Stock = 1,
            Picture = LoadImage("nautilus.png")
        });
        MyGlobals.MyWatches.Add(new Watch
        {
            Id = ObjectId.GenerateNewId(), Barcode = "126610LN",
            Brand = "Rolex", Model = "Submariner", Reference = "126610LN",
            Movement = "Automatic", Diameter = 41, CaseMaterial = "Acier Oystersteel",
            Price = 9150, Year = 2023, Stock = 2,
            Picture = LoadImage("submariner.png")
        });
        MyGlobals.MyWatches.Add(new Watch
        {
            Id = ObjectId.GenerateNewId(), Barcode = "SRPD55K1",
            Brand = "Seiko", Model = "Seiko 5 Sports", Reference = "SRPD55K1",
            Movement = "Automatic", Diameter = 42.5, CaseMaterial = "Acier",
            Price = 299, Year = 2024, Stock = 5,
            Picture = LoadImage("seiko5.png")
        });
        MyGlobals.MyWatches.Add(new Watch
        {
            Id = ObjectId.GenerateNewId(), Barcode = "GA2100-1A1",
            Brand = "Casio", Model = "G-Shock CasiOak", Reference = "GA-2100-1A1",
            Movement = "Quartz", Diameter = 45.4, CaseMaterial = "Résine carbone",
            Price = 99, Year = 2022, Stock = 10,
            Picture = LoadImage("casioak.png")
        });
        MyGlobals.MyWatches.Add(new Watch
        {
            Id = ObjectId.GenerateNewId(), Barcode = "31030425",
            Brand = "Omega", Model = "Speedmaster Moonwatch", Reference = "310.30.42.50.01.002",
            Movement = "Manual", Diameter = 42, CaseMaterial = "Acier",
            Price = 6900, Year = 2023, Stock = 3,
            Picture = LoadImage("speedmaster.png")
        });
    }

    private static Avalonia.Media.IImage? LoadImage(string fileName)
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

    partial void OnCurrentPageChanging(ViewModelBase? oldValue, ViewModelBase? newValue) { oldValue?.Dispose(); }

    [RelayCommand]
    private void GoToDetailsFromChild(ObjectId watchId)
    {
        CurrentPage = new CollectionDetailsViewModel(watchId, () => BackToMain(), () => BackToMain());
    }

    [RelayCommand] private void BackToMain() { CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand); }
    [RelayCommand] private void GoToAddWatch() { CurrentPage = new AddWatchViewModel(OnWatchAdded); }

    private async void OnWatchAdded()
    {
        try { await _jsonServices.SetWatchesAsync(MyGlobals.MyWatches); } catch { }
        CurrentPage = new CollectionViewModel(GoToDetailsFromChildCommand);
    }

    [RelayCommand] private void GoToUsers() { CurrentPage = new UsersViewModel(); }
    [RelayCommand] private void GoToCharts() { CurrentPage = new ChartsViewModel(); }

    [RelayCommand]
    private void Logout()
    {
        MyGlobals.CurrentUser = null;
        IsLoggedIn = false;
        IsAdmin = false;
        CurrentUserName = "";
        CurrentPage = new LoginViewModel(OnLoginSuccess);
    }
}