using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using VrcxPredictor.App.ViewModels;

namespace VrcxPredictor.App.Views.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
        }
        e.Handled = true;
    }
}
