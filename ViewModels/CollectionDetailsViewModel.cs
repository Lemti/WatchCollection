using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;

namespace WatchCollection.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    [ObservableProperty] private Watch _myWatch;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editBrand = "";
    [ObservableProperty] private string _editModel = "";
    [ObservableProperty] private string _editReference = "";
    [ObservableProperty] private string _editBarcode = "";
    [ObservableProperty] private string _editMovement = "";
    [ObservableProperty] private string _editDiameter = "";
    [ObservableProperty] private string _editCaseMaterial = "";
    [ObservableProperty] private string _editPrice = "";
    [ObservableProperty] private string _editYear = "";
    [ObservableProperty] private string _editStock = "";
    [ObservableProperty] private string _statusMessage = "";

    private readonly Action? _onBack;
    private readonly Action? _onDeleted;

    public CollectionDetailsViewModel(ObjectId id, Action? onBack = null, Action? onDeleted = null)
    {
        MyWatch = MyGlobals.MyWatches.First(w => w.Id == id);
        _onBack = onBack;
        _onDeleted = onDeleted;
    }

    public CollectionDetailsViewModel() { MyWatch = new Watch(); }

    [RelayCommand]
    private void Back() => _onBack?.Invoke();

    [RelayCommand]
    private void StartEdit()
    {
        EditBrand = MyWatch.Brand;
        EditModel = MyWatch.Model;
        EditReference = MyWatch.Reference;
        EditBarcode = MyWatch.Barcode;
        EditMovement = MyWatch.Movement;
        EditDiameter = MyWatch.Diameter.ToString();
        EditCaseMaterial = MyWatch.CaseMaterial;
        EditPrice = MyWatch.Price.ToString();
        EditYear = MyWatch.Year.ToString();
        EditStock = MyWatch.Stock.ToString();
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private void SaveEdit()
    {
        MyWatch.Brand = EditBrand;
        MyWatch.Model = EditModel;
        MyWatch.Reference = EditReference;
        MyWatch.Barcode = EditBarcode;
        MyWatch.Movement = EditMovement;
        MyWatch.Diameter = double.TryParse(EditDiameter, out var d) ? d : MyWatch.Diameter;
        MyWatch.CaseMaterial = EditCaseMaterial;
        MyWatch.Price = decimal.TryParse(EditPrice, out var p) ? p : MyWatch.Price;
        MyWatch.Year = int.TryParse(EditYear, out var y) ? y : MyWatch.Year;
        MyWatch.Stock = int.TryParse(EditStock, out var s) ? s : MyWatch.Stock;

        IsEditing = false;
        OnPropertyChanged(nameof(MyWatch));
        StatusMessage = "Montre modifiée avec succès.";
    }

    [RelayCommand]
    private void Delete()
    {
        MyGlobals.MyWatches.Remove(MyWatch);
        _onDeleted?.Invoke();
    }
}