using System.Windows;
using System.Windows.Controls;
using VrcxPredictor.App.ViewModels;

namespace VrcxPredictor.App.Views.Pages;

public partial class HeatmapsPage : Page
{
    public HeatmapsPage()
    {
        InitializeComponent();
        DataContext = new PageHostVM(((App)Application.Current).MainVM);
    }

    private void OnGoDashboard(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            var nav = mw.FindName("RootNavigation") as Wpf.Ui.Controls.NavigationView;
            nav?.Navigate(typeof(DashboardPage));
        }
    }
}

public sealed class PageHostVM
{
    public PageHostVM(MainViewModel main)
    {
        Main = main;
        HeatmapsVM = new HeatmapsViewModel(main);
    }

    public MainViewModel Main { get; }
    public HeatmapsViewModel HeatmapsVM { get; }
}
