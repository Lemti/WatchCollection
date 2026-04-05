using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class AddWatchViewModel : ViewModelBase
{
    private readonly Action _onWatchAdded;
    private readonly DeviceOrientationService _scanner = new();

    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _brand = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _movement = "Automatic";
    [ObservableProperty] private string _diameter = "";
    [ObservableProperty] private string _caseMaterial = "";
    [ObservableProperty] private string _price = "";
    [ObservableProperty] private string _year = "";
    [ObservableProperty] private string _stock = "1";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isScannerConnected;

    public AddWatchViewModel(Action onWatchAdded)
    {
        _onWatchAdded = onWatchAdded;
        _scanner.SerialBuffer.Changed += OnScannerDataReceived;
    }

    public AddWatchViewModel() { _onWatchAdded = () => { }; }

    private void OnScannerDataReceived(object? sender, EventArgs e)
    {
        if (_scanner.SerialBuffer.Count > 0)
        {
            var data = _scanner.SerialBuffer.Dequeue()?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(data))
            {
                Barcode = data;
                StatusMessage = $"Code scanné : {data}";
            }
        }
    }

    [RelayCommand]
    private void ConnectScanner()
    {
        try
        {
            _scanner.OpenPort();
            IsScannerConnected = true;
            StatusMessage = "Scanner connecté.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur scanner: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DisconnectScanner()
    {
        _scanner.ClosePort();
        IsScannerConnected = false;
        StatusMessage = "Scanner déconnecté.";
    }

    [RelayCommand]
    private void AddWatch()
    {
        if (string.IsNullOrWhiteSpace(Brand) || string.IsNullOrWhiteSpace(Model))
        {
            StatusMessage = "La marque et le modèle sont obligatoires.";
            return;
        }

        var watch = new Watch
        {
            Id = ObjectId.GenerateNewId(),
            Barcode = Barcode,
            Brand = Brand,
            Model = Model,
            Reference = Reference,
            Movement = Movement,
            Diameter = double.TryParse(Diameter, out var d) ? d : 0,
            CaseMaterial = CaseMaterial,
            Price = decimal.TryParse(Price, out var p) ? p : 0,
            Year = int.TryParse(Year, out var y) ? y : DateTime.Now.Year,
            Stock = int.TryParse(Stock, out var s) ? s : 1
        };

        MyGlobals.MyWatches.Add(watch);
        _onWatchAdded();
    }
}
