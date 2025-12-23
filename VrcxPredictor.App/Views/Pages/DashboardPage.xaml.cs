using System.Windows;
using System.Windows.Controls;

namespace VrcxPredictor.App.Views.Pages;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).MainVM;
    }
}
