using System.Windows;
using Wpf.Ui.Appearance;
using VrcxPredictor.App.ViewModels;
using VrcxPredictor.Core.Models;

namespace VrcxPredictor.App;

public partial class App : Application
{
    public MainViewModel MainVM { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplicationThemeManager.ApplySystemTheme();

        var cfg = AppConfig.LoadOrCreate();
        MainVM = new MainViewModel(cfg);

        var win = new MainWindow { DataContext = MainVM };

        SystemThemeWatcher.Watch(win);

        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { MainVM?.PersistConfig(); } catch { }
        base.OnExit(e);
    }
}
