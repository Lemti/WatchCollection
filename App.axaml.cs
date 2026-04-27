using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using WatchCollection.ViewModels;
using WatchCollection.Views;

namespace WatchCollection;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Une seule instance du ViewModel principal, créée ici de manière contrôlée.
            // Le constructeur est synchrone ; toute opération asynchrone passe par InitializeAsync().
            var mainViewModel = new MainWindowViewModel();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Initialisation asynchrone post-construction (séparation propre sync/async).
            // Pattern recommandé : on ne bloque pas le démarrage de l'UI.
            _ = mainViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}