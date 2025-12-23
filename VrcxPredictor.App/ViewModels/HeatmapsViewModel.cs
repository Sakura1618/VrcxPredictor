using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VrcxPredictor.App.Models;
using VrcxPredictor.App.Services;

namespace VrcxPredictor.App.ViewModels;

public sealed partial class HeatmapsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty] private bool _hourly = true;
    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private ObservableCollection<HeatCell> _cells = new();
    [ObservableProperty] private ObservableCollection<string> _colHeaders = new();
    [ObservableProperty] private ObservableCollection<string> _rowHeaders = new() { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

    public HeatmapsViewModel(MainViewModel main)
    {
        _main = main;
        UpdateFromMain();

        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Analysis))
                UpdateFromMain();
        };
    }

    [RelayCommand]
    private void ToggleResolution()
    {
        Hourly = !Hourly;
        UpdateFromMain();
    }

    partial void OnSelectedTabIndexChanged(int value) => UpdateFromMain();

    private void UpdateFromMain()
    {
        Cells.Clear();
        ColHeaders.Clear();

        var analysis = _main.Analysis;
        if (analysis is null)
            return;

        double[,]? matrix = SelectedTabIndex == 0
            ? analysis.ProbabilityMatrix
            : analysis.GlobalOnlineMatrix;

        if (matrix is null)
            return;

        int bins = matrix.GetLength(1);

        if (Hourly)
        {
            for (int h = 0; h < 24; h++)
                ColHeaders.Add(h.ToString("00"));

            for (int r = 0; r < 7; r++)
            {
                for (int h = 0; h < 24; h++)
                {
                    int binsPerHour = Math.Max(1, bins / 24);
                    double sum = 0;
                    for (int k = 0; k < binsPerHour; k++)
                    {
                        int idx = h * binsPerHour + k;
                        if (idx < bins) sum += matrix[r, idx];
                    }
                    double v = sum / binsPerHour;

                    Cells.Add(new HeatCell
                    {
                        Row = r,
                        Col = h,
                        Value = v,
                        Brush = HeatmapPalette.BrushFor(v),
                        Tooltip = $"{RowHeaders[r]} {h:00}:00  数值={v:0.000}"
                    });
                }
            }
        }
        else
        {
            int minutesPerBin = 24 * 60 / bins;
            for (int c = 0; c < bins; c++)
            {
                int minutes = c * minutesPerBin;
                int hh = minutes / 60;
                int mm = minutes % 60;
                ColHeaders.Add(mm == 0 ? $"{hh:00}" : $"{mm:00}");
            }

            for (int r = 0; r < 7; r++)
            for (int c = 0; c < bins; c++)
            {
                double v = matrix[r, c];
                int minutes = c * minutesPerBin;
                int hh = minutes / 60;
                int mm = minutes % 60;

                Cells.Add(new HeatCell
                {
                    Row = r,
                    Col = c,
                    Value = v,
                    Brush = HeatmapPalette.BrushFor(v),
                    Tooltip = $"{RowHeaders[r]} {hh:00}:{mm:00}  数值={v:0.000}"
                });
            }
        }
    }
}
