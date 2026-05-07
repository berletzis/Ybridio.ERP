using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.WinUI.ViewModels.Diagnostic;

namespace Ybridio.WinUI.Views.Diagnostic;

public sealed partial class DiagnosticPanel : UserControl
{
    public DiagnosticPanelViewModel ViewModel { get; }

    private Button[] _tabButtons = [];

    public DiagnosticPanel()
    {
        ViewModel = App.Services.GetRequiredService<DiagnosticPanelViewModel>();
        InitializeComponent();

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tabButtons = [BtnTab0, BtnTab1, BtnTab2, BtnTab3, BtnTab4, BtnTab5];
        ViewModel.StartMonitoring();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopMonitoring();
    }

    private void DiagTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tagStr) return;
        if (!int.TryParse(tagStr, out var tabIndex)) return;

        ViewModel.SelectTabCommand.Execute(tabIndex);
        UpdateTabStyles(tabIndex);
    }

    private void UpdateTabStyles(int activeIndex)
    {
        for (var i = 0; i < _tabButtons.Length; i++)
        {
            var btn = _tabButtons[i];
            if (i == activeIndex)
            {
                btn.Background       = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1C, 0x1C, 0x1C));
                btn.BorderThickness  = new Thickness(1, 0, 1, 0);
                btn.BorderBrush      = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x3E, 0x3E, 0x3E));
                btn.Foreground       = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD4, 0xD4, 0xD4));
            }
            else
            {
                btn.ClearValue(BackgroundProperty);
                btn.ClearValue(BorderThicknessProperty);
                btn.ClearValue(BorderBrushProperty);
                btn.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x85, 0x85, 0x85));
            }
        }
    }
}
