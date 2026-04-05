using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace WatchCollection.ViewModels;

public partial class ChartsViewModel : ViewModelBase
{
    public ISeries[] BrandSeries { get; }
    public ISeries[] MovementSeries { get; }
    public ISeries[] PriceSeries { get; }
    public Axis[] PriceXAxes { get; }
    public Axis[] PriceYAxes { get; }

    public ChartsViewModel()
    {
        // Graphique 1 : Stock par marque
        var brandGroups = MyGlobals.MyWatches
            .GroupBy(w => w.Brand)
            .Select(g => new { Brand = g.Key, Count = g.Sum(w => w.Stock) })
            .ToList();

        BrandSeries = brandGroups.Select(g => new PieSeries<int>
        {
            Values = new[] { g.Count },
            Name = g.Brand
        } as ISeries).ToArray();

        // Graphique 2 : Répartition par mouvement
        var movementGroups = MyGlobals.MyWatches
            .GroupBy(w => w.Movement)
            .Select(g => new { Movement = g.Key, Count = g.Count() })
            .ToList();

        MovementSeries = movementGroups.Select(g => new PieSeries<int>
        {
            Values = new[] { g.Count },
            Name = g.Movement
        } as ISeries).ToArray();

        // Graphique 3 : Prix par modèle (barres horizontales avec couleurs)
        var watches = MyGlobals.MyWatches
            .OrderByDescending(w => w.Price)
            .ToList();

        var labels = watches.Select(w => $"{w.Brand} {w.Model}").ToList();

        PriceSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = watches.Select(w => (double)w.Price).ToList(),
                Name = "Prix (€)",
                Fill = new SolidColorPaint(SKColors.SteelBlue),
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 12,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = point => $"{point.PrimaryValue:N0} €"            }
        };

        PriceYAxes = new[]
        {
            new Axis
            {
                Labels = labels,
                TextSize = 12
            }
        };

        PriceXAxes = new[]
        {
            new Axis { Name = "Prix (€)" }
        };
    }
}