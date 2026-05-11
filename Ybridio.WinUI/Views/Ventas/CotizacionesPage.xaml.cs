using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class CotizacionesPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService   _workspace;
    private readonly ICotizacionService  _cotizacionService;
    public CotizacionesViewModel ViewModel { get; }

    public CotizacionesPage()
    {
        ViewModel          = App.Services.GetRequiredService<CotizacionesViewModel>();
        _workspace         = App.Services.GetRequiredService<IWorkspaceService>();
        _cotizacionService = App.Services.GetRequiredService<ICotizacionService>();
        InitializeComponent();

        // ADR-027: Suscribirse a cambios de IsDocumentSurfaceDetached para ajustar el layout
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsDocumentSurfaceDetached))
        {
            AjustarLayoutDetached(ViewModel.IsDocumentSurfaceDetached);
        }
    }

    /// <summary>
    /// Ajusta el Grid principal para mostrar modo normal/content replacement o modo detached split view.
    /// ADR-027: UN solo ListView compartido, cambia su posición y margen según el modo.
    /// </summary>
    private void AjustarLayoutDetached(bool isDetached)
    {
        if (isDetached)
        {
            // Modo Detached: Layout de 3 columnas (listado | splitter | surface)
            ListadoColumn.Width = new GridLength(2, GridUnitType.Star);
            ListadoColumn.MinWidth = 400;
            SplitterColumn.Width = new GridLength(4);
            SurfaceColumn.Width = new GridLength(3, GridUnitType.Star);
            SurfaceColumn.MinWidth = 600;

            // Ajustar elementos para el split view
            ListadoBorder.Margin = new Thickness(20, 4, 8, 0);
            ListadoBorder.SetValue(Grid.ColumnProperty, 0);
            ListadoBorder.Visibility = Visibility.Visible;

            DocumentSurfacePresenter.Margin = new Thickness(8, 4, 20, 0);
            DocumentSurfacePresenter.SetValue(Grid.ColumnProperty, 2);
            DocumentSurfacePresenter.SetValue(Grid.ColumnSpanProperty, 1);
        }
        else
        {
            // Modo Normal/Content Replacement: Layout de 1 columna (ocupa todo el espacio)
            ListadoColumn.Width = new GridLength(1, GridUnitType.Star);
            ListadoColumn.MinWidth = 0;
            SplitterColumn.Width = new GridLength(0);
            SurfaceColumn.Width = new GridLength(0);
            SurfaceColumn.MinWidth = 0;

            // Ajustar elementos para el content replacement
            ListadoBorder.Margin = new Thickness(20, 4, 20, 0);
            ListadoBorder.SetValue(Grid.ColumnProperty, 0);
            // Visibility se controla por binding en XAML: InverseBoolToVisibilityConverter(IsDocumentSurfaceVisible)

            DocumentSurfacePresenter.Margin = new Thickness(20, 4, 20, 0);
            DocumentSurfacePresenter.SetValue(Grid.ColumnProperty, 0);
            DocumentSurfacePresenter.SetValue(Grid.ColumnSpanProperty, 3);
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.RefrescarCommand.CanExecute(null))
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    // ── Document Surface UX Pattern Handlers ─────────────────────────────────

    /// <summary>
    /// Abre el Document Surface para crear una nueva cotización.
    /// Reemplaza el comportamiento anterior de abrir una nueva Workspace Tab.
    /// </summary>
    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var page = new CotizacionDocumentoPage(null);
        page.ViewModel.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        page.ToggleDetach = OnToggleDetach; // ADR-027: wire detachable mode callback
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Abre el Document Surface para editar la cotización seleccionada.
    /// Reemplaza el comportamiento anterior de abrir una Workspace Tab persistente.
    /// </summary>
    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        await AbrirCotizacionEnDocumentSurface(ViewModel.CotizacionSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        _ = AbrirCotizacionEnDocumentSurface(ViewModel.CotizacionSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirCotizacionEnDocumentSurface(long id)
    {
        var result = await _cotizacionService.ObtenerConDetallesAsync(id);
        if (!result.Success || result.Value is null)
        {
            ViewModel.ErrorMessage = result.Error ?? "No se pudo cargar la cotización.";
            return;
        }
        var page = new CotizacionDocumentoPage(result.Value);
        page.ViewModel.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        page.ToggleDetach = OnToggleDetach; // ADR-027: wire detachable mode callback
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Callback invocado cuando el documento se guarda exitosamente.
    /// Cierra el Document Surface y refresca el grid de listado.
    /// </summary>
    private async void OnDocumentSaved()
    {
        await ViewModel.CerrarDocumentSurfaceAsync();
        ViewModel.SuccessMessage = "Cotización guardada correctamente.";
    }

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "← Volver a Lista".
    /// Cierra el Document Surface sin guardar y vuelve al grid de listado.
    /// </summary>
    private async void OnVolverALista()
    {
        await ViewModel.CerrarDocumentSurfaceAsync();
    }

    // ── Document Surface Detachable Mode (ADR-027) ───────────────────────────

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "Desacoplar Surface".
    /// Alterna entre modo acoplado (content replacement: grid XOR surface) y 
    /// modo desacoplado (split view: grid + surface simultáneos).
    /// Permite multitarea ligera controlada sin volver a Workspace Tabs infinitos.
    /// </summary>
    private void OnToggleDetach()
    {
        ViewModel.ToggleDetachCommand.Execute(null);
    }
}
