using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Core;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels;
using Ybridio.WinUI.Views.Configuracion;
using Ybridio.WinUI.Views.Contactos;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.Finanzas;
using Ybridio.WinUI.Views.Inventario;
using Ybridio.WinUI.Views.POS;
using Ybridio.WinUI.Views.Ventas;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    private readonly IWorkspaceService     _workspace;
    private readonly SessionService        _session;
    private readonly ICurrentContextTracker _contextTracker;
    private bool _suppressTabSync;

    public ShellPage()
    {
        ViewModel        = App.Services.GetRequiredService<ShellViewModel>();
        _workspace       = App.Services.GetRequiredService<IWorkspaceService>();
        _session         = App.Services.GetRequiredService<SessionService>();
        _contextTracker  = App.Services.GetRequiredService<ICurrentContextTracker>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _workspace.Tabs.CollectionChanged += OnWorkspaceTabsChanged;
        _workspace.ActiveTabChanged       += OnWorkspaceActiveTabChanged;

        await ViewModel.InitializeAsync();

        ModuleFrame.Navigate(typeof(DashboardPage));

        SetActiveNavButton(BtnDashboard);
        AjustarPaddingTopBar();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _workspace.Tabs.CollectionChanged -= OnWorkspaceTabsChanged;
        _workspace.ActiveTabChanged       -= OnWorkspaceActiveTabChanged;
    }

    // ── Developer Mode (Ctrl+Shift+D) ────────────────────────────────────────

    /// <summary>
    /// Ctrl+Shift+D alterna el Runtime Diagnostic Panel.
    /// Usa KeyboardAccelerator (declarado en XAML) para capturar el atajo
    /// independientemente del elemento que tenga foco.
    /// </summary>
    private void DevMode_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _session.ToggleDeveloperMode();
        DiagnosticPanelCtrl.Visibility = _session.IsDeveloperMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        args.Handled = true;
    }

    // ── Navegación de módulos (ModuleFrame) ───────────────────────────────────

    private void NavigateToModule(string tag)
    {
        (Type? pageType, object? param) = tag switch
        {
            "Dashboard"                              => ((Type?)typeof(DashboardPage),     (object?)null),
            "POS"                                    => (typeof(PosPage),                  null),
            "Inventario"                             => (typeof(InventarioPage),            null),
            "Ventas"                                 => (typeof(VentasPage),               null),
            "Contactos"                              => (typeof(ContactosPage),             null),
            "Finanzas"                               => (typeof(FinanzasPage),              null),
            "Configuracion" or "ConfiguracionGlobal" => (typeof(ConfiguracionPage),        "Global"),
            "ConfiguracionTienda"                    => (typeof(ConfiguracionPage),        "Tienda"),
            _                                        => (null, null)
        };

        if (pageType is null) return;
        if (ModuleFrame.CurrentSourcePageType == pageType && param is null) return;

        ModuleFrame.Navigate(pageType, param);

        // Notifica el módulo activo al tracker — contexto parcial hasta que el ViewModel cargue
        var moduleName = tag switch
        {
            "Dashboard"                              => "Dashboard",
            "POS"                                    => "POS",
            "Inventario"                             => "Inventario",
            "Ventas"                                 => "Ventas",
            "Contactos"                              => "Contactos",
            "Finanzas"                               => "Finanzas",
            "Configuracion" or "ConfiguracionGlobal" => "Configuración",
            "ConfiguracionTienda"                    => "Config. Tienda",
            _                                        => tag
        };
        _contextTracker.SetModuleContext(moduleName);
    }

    // ── WorkspaceService → TabView sync ──────────────────────────────────────

    private void OnWorkspaceTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            WorkspaceTabView.TabItems.Clear();
            WorkspaceTabView.Visibility = Visibility.Collapsed;
            return;
        }

        if (e.NewItems is not null)
            foreach (WorkspaceTabItem item in e.NewItems)
                AddTabToView(item);

        if (e.OldItems is not null)
            foreach (WorkspaceTabItem item in e.OldItems)
                RemoveTabFromView(item);

        WorkspaceTabView.Visibility = _workspace.Tabs.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AddTabToView(WorkspaceTabItem item)
    {
        var tabItem = new TabViewItem
        {
            Tag        = item.Key,
            Header     = item.Title,
            Content    = item.Content,
            IsClosable = item.IsClosable,
        };
        WorkspaceTabView.TabItems.Add(tabItem);
    }

    private void RemoveTabFromView(WorkspaceTabItem item)
    {
        var tvItem = WorkspaceTabView.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is string k && k == item.Key);

        if (tvItem is not null)
            WorkspaceTabView.TabItems.Remove(tvItem);
    }

    private void OnWorkspaceActiveTabChanged(WorkspaceTabItem? tab)
    {
        if (tab is null) return;

        _suppressTabSync = true;
        var tvItem = WorkspaceTabView.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is string k && k == tab.Key);

        if (tvItem is not null)
            WorkspaceTabView.SelectedItem = tvItem;

        _suppressTabSync = false;
    }

    // ── TabView → WorkspaceService sync ──────────────────────────────────────

    private void WorkspaceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabSync) return;

        if (WorkspaceTabView.SelectedItem is TabViewItem tvItem && tvItem.Tag is string key)
            _workspace.ActivateTab(key);
    }

    private void WorkspaceTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is string key)
            _workspace.CloseTab(key);
    }

    // ── Helpers de ShellPage ──────────────────────────────────────────────────

    private void AjustarPaddingTopBar()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        var rightInsetFisico = mainWindow.TitleBarRightInset;
        var scale = XamlRoot?.RasterizationScale ?? 1.0;

        var rightPadding = rightInsetFisico > 0 ? (int)(rightInsetFisico / scale) : 0;
        TopBarGrid.Padding = new Microsoft.UI.Xaml.Thickness(12, 0, rightPadding, 0);
    }

    private void ModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            ViewModel.SelectModuleCommand.Execute(tag);
            NavigateToModule(tag);
            SetActiveNavButton(btn);
        }
    }

    private void TiendaSelector_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SucursalDto tienda)
        {
            ViewModel.SeleccionarSucursalCommand.Execute(tienda);
            SucursalFlyout.Hide();
        }
    }

    private void RibbonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ViewModel.NavigateToCommand.Execute(tag);
    }

    private void SetActiveNavButton(Button activeBtn)
    {
        foreach (UIElement child in NavButtonsPanel.Children)
        {
            if (child is Button btn)
                btn.ClearValue(BackgroundProperty);
        }
        if (XamlApp.Current.Resources.ContainsKey("SubtleFillColorSecondaryBrush"))
            activeBtn.Background = (Brush)XamlApp.Current.Resources["SubtleFillColorSecondaryBrush"];
    }
}
