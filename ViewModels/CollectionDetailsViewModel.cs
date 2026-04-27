using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using WatchCollection.Models;
using WatchCollection.Services;

namespace WatchCollection.ViewModels;

public partial class CollectionDetailsViewModel : ViewModelBase
{
    private readonly MongoDBService _mongo = new();
    private readonly Action? _onBack;
    private readonly Action? _onDeleted;

    public string[] MovementOptions { get; } = ["Automatic", "Quartz", "Manual"];

    [ObservableProperty] private Watch _myWatch;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isMergeConfirmationVisible;
    [ObservableProperty] private string _mergeConfirmationText = string.Empty;

    [ObservableProperty] private string _editBrand = string.Empty;
    [ObservableProperty] private string _editModel = string.Empty;
    [ObservableProperty] private string _editReference = string.Empty;
    [ObservableProperty] private string _editBarcode = string.Empty;
    [ObservableProperty] private string _editMovement = "Automatic";
    [ObservableProperty] private string _editDiameter = string.Empty;
    [ObservableProperty] private string _editCaseMaterial = string.Empty;
    [ObservableProperty] private string _editPrice = string.Empty;
    [ObservableProperty] private string _editYear = string.Empty;
    [ObservableProperty] private string _editStock = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private Watch? _pendingMergeTwin;

    public CollectionDetailsViewModel(ObjectId id, Action? onBack = null, Action? onDeleted = null)
    {
        MyWatch = Globals.MyWatches.First(w => w.Id == id);
        _onBack = onBack;
        _onDeleted = onDeleted;
    }

    public CollectionDetailsViewModel()
    {
        MyWatch = new Watch();
    }

    [RelayCommand]
    private void Back() => _onBack?.Invoke();

    [RelayCommand]
    private void StartEdit()
    {
        EditBrand = MyWatch.Brand;
        EditModel = MyWatch.Model;
        EditReference = MyWatch.Reference;
        EditBarcode = MyWatch.Barcode;
        EditMovement = string.IsNullOrWhiteSpace(MyWatch.Movement) ? "Automatic" : MyWatch.Movement;
        EditDiameter = MyWatch.Diameter.ToString();
        EditCaseMaterial = MyWatch.CaseMaterial;
        EditPrice = MyWatch.Price.ToString();
        EditYear = MyWatch.Year.ToString();
        EditStock = MyWatch.Stock.ToString();
        StatusMessage = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = string.Empty;
        IsMergeConfirmationVisible = false;
        _pendingMergeTwin = null;
    }

    /// <summary>
    /// Tentative de sauvegarde. Si la modification rend la montre identique à une autre
    /// (même Brand+Model+Reference), on demande confirmation avant de fusionner les stocks.
    /// </summary>
    [RelayCommand]
    private async Task SaveEdit()
    {
        if (!IsValid(out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        var brand = EditBrand.Trim();
        var model = EditModel.Trim();
        var reference = EditReference.Trim();

        var twin = Globals.MyWatches.FirstOrDefault(w =>
            w.Id != MyWatch.Id
            && !string.IsNullOrWhiteSpace(brand)
            && !string.IsNullOrWhiteSpace(model)
            && !string.IsNullOrWhiteSpace(reference)
            && w.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)
            && w.Model.Equals(model, StringComparison.OrdinalIgnoreCase)
            && w.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));

        if (twin is not null)
        {
            _pendingMergeTwin = twin;
            int.TryParse(EditStock, out var newStock);
            MergeConfirmationText =
                $"Une montre identique existe déjà : {twin.Brand} {twin.Model} ({twin.Reference}). " +
                $"Voulez-vous fusionner ? Le stock sera additionné ({newStock} + {twin.Stock} = {newStock + twin.Stock}) " +
                $"et l'autre fiche sera supprimée.";
            IsMergeConfirmationVisible = true;
            return;
        }

        await ApplySaveAsync();
    }

    /// <summary>
    /// Confirme la fusion identitaire après que l'utilisateur ait validé le message d'avertissement.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmMerge()
    {
        IsMergeConfirmationVisible = false;
        if (_pendingMergeTwin is null) return;

        try
        {
            IsBusy = true;

            ApplyEditFieldsTo(MyWatch);
            MyWatch.Stock += _pendingMergeTwin.Stock;

            if (string.IsNullOrWhiteSpace(MyWatch.Barcode) && !string.IsNullOrWhiteSpace(_pendingMergeTwin.Barcode))
                MyWatch.Barcode = _pendingMergeTwin.Barcode;

            if (Globals.IsDatabaseAvailable)
                await _mongo.DeleteWatchAsync(_pendingMergeTwin.Id);
            Globals.MyWatches.Remove(_pendingMergeTwin);

            if (Globals.IsDatabaseAvailable)
            {
                var updated = await _mongo.UpdateWatchAsync(MyWatch);
                if (!updated)
                {
                    StatusMessage = "Erreur lors de la fusion en base de données.";
                    return;
                }
            }

            StatusMessage = "Montres fusionnées avec succès.";
            IsEditing = false;
            _pendingMergeTwin = null;
            OnPropertyChanged(nameof(MyWatch));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de la fusion : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Annule la fusion : on revient à l'état d'édition sans sauvegarder.
    /// </summary>
    [RelayCommand]
    private void CancelMerge()
    {
        IsMergeConfirmationVisible = false;
        _pendingMergeTwin = null;
        StatusMessage = "Fusion annulée. Modifiez la référence pour éviter le doublon.";
    }

    /// <summary>
    /// Sauvegarde standard (pas de fusion détectée) : update simple en DB et mémoire.
    /// </summary>
    private async Task ApplySaveAsync()
    {
        try
        {
            IsBusy = true;

            ApplyEditFieldsTo(MyWatch);

            if (Globals.IsDatabaseAvailable)
            {
                var success = await _mongo.UpdateWatchAsync(MyWatch);
                if (!success)
                {
                    StatusMessage = "Erreur lors de la sauvegarde en base de données.";
                    return;
                }
                StatusMessage = "Montre modifiée et sauvegardée.";
            }
            else
            {
                StatusMessage = "Modifications enregistrées localement (hors-ligne).";
            }

            IsEditing = false;
            OnPropertyChanged(nameof(MyWatch));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de la modification : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyEditFieldsTo(Watch target)
    {
        target.Brand = EditBrand.Trim();
        target.Model = EditModel.Trim();
        target.Reference = EditReference.Trim();
        target.Barcode = EditBarcode.Trim();
        target.Movement = EditMovement;
        target.Diameter = double.TryParse(EditDiameter, out var d) ? d : target.Diameter;
        target.CaseMaterial = EditCaseMaterial.Trim();
        target.Price = decimal.TryParse(EditPrice, out var p) ? p : target.Price;
        target.Year = int.TryParse(EditYear, out var y) ? y : target.Year;
        target.Stock = int.TryParse(EditStock, out var s) ? s : target.Stock;
    }

    [RelayCommand]
    private async Task Delete()
    {
        try
        {
            IsBusy = true;

            if (Globals.IsDatabaseAvailable)
            {
                var success = await _mongo.DeleteWatchAsync(MyWatch.Id);
                if (!success)
                {
                    StatusMessage = "Erreur lors de la suppression en base de données.";
                    IsBusy = false;
                    return;
                }
            }

            Globals.MyWatches.Remove(MyWatch);

            _onDeleted?.Invoke();
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
        if (string.IsNullOrWhiteSpace(EditBarcode))
        {
            error = "Le code-barre est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(EditBrand))
        {
            error = "La marque est obligatoire.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(EditModel))
        {
            error = "Le modèle est obligatoire.";
            return false;
        }
        if (!double.TryParse(EditDiameter, out var diameter) || diameter is < 20 or > 60)
        {
            error = "Le diamètre doit être entre 20 et 60 mm.";
            return false;
        }
        if (!decimal.TryParse(EditPrice, out var price) || price < 0)
        {
            error = "Le prix doit être un nombre positif.";
            return false;
        }
        if (!int.TryParse(EditYear, out var year) || year < 1900 || year > DateTime.Now.Year + 1)
        {
            error = $"L'année doit être entre 1900 et {DateTime.Now.Year + 1}.";
            return false;
        }
        if (!int.TryParse(EditStock, out var stock) || stock < 0)
        {
            error = "Le stock doit être un entier positif.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}   