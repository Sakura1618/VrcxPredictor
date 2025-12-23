using System;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace VrcxPredictor.App.Services;

public static class SnackbarHost
{
    private static readonly SnackbarService _service = new();

    public static void EnsurePresenter(SnackbarPresenter presenter) =>
        _service.SetSnackbarPresenter(presenter);

    public static void Info(string title, string message) =>
        _service.Show(title, message, ControlAppearance.Secondary, null, TimeSpan.FromSeconds(2.5));

    public static void Success(string title, string message) =>
        _service.Show(title, message, ControlAppearance.Success, null, TimeSpan.FromSeconds(2.5));

    public static void Warn(string title, string message) =>
        _service.Show(title, message, ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));

    public static void Error(string title, string message) =>
        _service.Show(title, message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(4));
}
