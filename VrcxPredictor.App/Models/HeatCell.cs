using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VrcxPredictor.App.Models;

public sealed partial class HeatCell : ObservableObject
{
    [ObservableProperty] private int _row;
    [ObservableProperty] private int _col;
    [ObservableProperty] private double _value;
    [ObservableProperty] private Brush _brush = Brushes.Transparent;
    [ObservableProperty] private string _tooltip = "";
}
