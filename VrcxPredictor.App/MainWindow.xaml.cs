using VrcxPredictor.App.Services;
using Wpf.Ui.Controls;

namespace VrcxPredictor.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            SnackbarHost.EnsurePresenter(SnackbarPresenter);
            RootNavigation.Navigate(typeof(Views.Pages.DashboardPage));
        };
    }

}
