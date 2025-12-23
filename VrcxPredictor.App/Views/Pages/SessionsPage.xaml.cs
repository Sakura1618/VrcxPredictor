using System.Windows;
using System.Windows.Controls;

namespace VrcxPredictor.App.Views.Pages;

public partial class SessionsPage : Page
{
    public SessionsPage()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).MainVM;
    }
}
