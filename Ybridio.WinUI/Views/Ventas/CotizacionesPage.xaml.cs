using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class CotizacionesPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService   _workspace;
    private readonly ICotizacionService  _cotizacionService;
    private readonly IWindowManager      _windowManager;

    public CotizacionesViewModel ViewModel { get; }

    /// <summary>
    /// ID del documento actualmente abierto en el Document Surface inline.
    /// Null si no hay surface activa o si es un documento nuevo (sin ID persistido).
    /// Usado para implementar Single Document Session Rule (evitar doble apertura inline).
    /// </summary>
    private long? _currentInlineDocumentId;

    public CotizacionesPage()
    {
        ViewModel          = App.Services.GetRequiredService<CotizacionesViewModel>();
        _workspace         = App.Services.GetRequiredService<IWorkspaceService>();
        _cotizacionService = App.Services.GetRequiredService<ICotizacionService>();
        _windowManager     = App.Services.GetRequiredService<IWindowManager>();
        InitializeComponent();
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

    // ── Document Surface UX Pattern Handlers (ADR-032) ───────────────────────

    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var page = new CotizacionDocumentoPage(null);
        page.VolverALista = OnVolverALista;
        page.EsInlineMode = true;
        _currentInlineDocumentId = null;  // Nuevo documento — sin ID asignado todavía

        // Global Document Runtime Ownership Pattern:
        // Cuando el documento nuevo se guarda por primera vez, DocumentSaved notifica.
        // En ese momento _currentInlineDocumentId se actualiza con el ID real asignado por BD.
        // Esto garantiza que si el usuario intenta abrir el mismo documento desde el grid
        // (después de guardar y que aparezca en la lista), el check 2 de AbrirCotizacionInline
        // lo detecte como ya abierto inline y no cree una segunda instancia.
        page.ViewModel.DocumentSaved = () =>
        {
            _currentInlineDocumentId = page.ViewModel.DocumentoId;
        };

        ViewModel.DocumentSurfaceContent   = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        await AbrirCotizacionInline(ViewModel.CotizacionSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        _ = AbrirCotizacionInline(ViewModel.CotizacionSeleccionada.Id);
    }

    /// <summary>
    /// Abre una cotización en el Document Surface inline aplicando Single Document Session Rule.
    ///
    /// REGLA: Un documento comercial NO puede existir simultáneamente en múltiples sesiones
    /// runtime independientes. Antes de crear cualquier instancia, se verifica:
    ///
    /// 1. ¿Está ya en una ventana detached? → Activar esa ventana, no abrir nueva sesión.
    /// 2. ¿Está ya en el surface inline? → No hacer nada (ya está visible).
    /// 3. Solo si no existe sesión activa → crear nueva sesión inline.
    /// </summary>
    private async System.Threading.Tasks.Task AbrirCotizacionInline(long id)
    {
        // ── Verificación 1: ¿Existe sesión activa en ventana detached? ──────────
        // Single Document Session Rule: si el documento ya está en una ventana OS real,
        // activar esa ventana — NO crear nueva instancia en el tab.
        if (_windowManager.TryActivateWindow($"detached:cotizacion:{id}"))
        {
            // Sesión encontrada en ventana detached — activada y traída al frente.
            // No abrir segunda sesión. No crear nuevo ViewModel.
            return;
        }

        // ── Verificación 2: ¿Ya está visible inline con el mismo documento? ────
        if (ViewModel.IsDocumentSurfaceVisible && _currentInlineDocumentId == id)
            return;  // Ya mostrando este documento inline — nada que hacer

        // ── Sin sesión activa: cargar y abrir normalmente ────────────────────
        var result = await _cotizacionService.ObtenerConDetallesAsync(id);
        if (!result.Success || result.Value is null)
        {
            ViewModel.ErrorMessage = result.Error ?? "No se pudo cargar la cotización.";
            return;
        }

        var page = new CotizacionDocumentoPage(result.Value);
        page.VolverALista   = OnVolverALista;
        page.EsInlineMode   = true;

        _currentInlineDocumentId           = id;
        ViewModel.DocumentSurfaceContent   = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private async void OnVolverALista()
    {
        _currentInlineDocumentId = null;   // Limpiar tracking de sesión inline
        await ViewModel.CerrarDocumentSurfaceAsync();
    }
}
