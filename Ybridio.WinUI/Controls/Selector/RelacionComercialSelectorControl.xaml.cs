using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.Services.Directorio;
using Ybridio.WinUI.Services;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Controls.Selector;

/// <summary>
/// Control institucional ERP para selección de socios comerciales del Directorio (ADR-038).
/// Busca directamente en <c>Persona</c> y <c>EmpresaComercial</c>; NO requiere RelacionComercial preexistente.
/// RelacionComercial se crea/reutiliza de forma transparente al guardar el documento (GetOrCreate pattern).
/// </summary>
/// <remarks>
/// Uso:
/// <code>
/// &lt;selector:RelacionComercialSelectorControl
///     x:Name="SelectorCliente"
///     PlaceholderText="Buscar cliente, empresa, RFC..."
///     EmpresaId="{x:Bind ViewModel.EmpresaId}"
///     EntidadSeleccionada="{x:Bind ViewModel.EntidadDirectorioSeleccionada, Mode=TwoWay}"
///     SelectionChanged="OnClienteSeleccionado" /&gt;
/// </code>
/// PROHIBIDO usar otros patrones para selección comercial (ADR-037 / ADR-038).
/// </remarks>
public sealed partial class RelacionComercialSelectorControl : UserControl
{
    // ── DependencyProperties ─────────────────────────────────────────────────

    /// <summary>Texto de placeholder mostrado cuando el campo está vacío.</summary>
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string),
            typeof(RelacionComercialSelectorControl),
            new PropertyMetadata("Buscar socio comercial por nombre, RFC o email..."));

    /// <summary>ID de la empresa para contexto de búsqueda. OBLIGATORIO.</summary>
    public static readonly DependencyProperty EmpresaIdProperty =
        DependencyProperty.Register(nameof(EmpresaId), typeof(int),
            typeof(RelacionComercialSelectorControl),
            new PropertyMetadata(0));

    /// <summary>Entidad del Directorio actualmente seleccionada. null si no hay selección (ADR-038).</summary>
    public static readonly DependencyProperty EntidadSeleccionadaProperty =
        DependencyProperty.Register(nameof(EntidadSeleccionada), typeof(DirectorioSelectorDto),
            typeof(RelacionComercialSelectorControl),
            new PropertyMetadata(null, OnEntidadSeleccionadaChanged));

    /// <summary>Controla si el control está habilitado para interacción.</summary>
    public new static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(nameof(IsEnabled), typeof(bool),
            typeof(RelacionComercialSelectorControl),
            new PropertyMetadata(true));

    // ── Propiedades públicas ─────────────────────────────────────────────────

    /// <summary>Texto de placeholder del campo de búsqueda.</summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>ID de empresa para la búsqueda (multi-tenant).</summary>
    public int EmpresaId
    {
        get => (int)GetValue(EmpresaIdProperty);
        set => SetValue(EmpresaIdProperty, value);
    }

    /// <summary>Entidad del Directorio seleccionada (Persona o EmpresaComercial). null si el usuario no ha seleccionado (ADR-038).</summary>
    public DirectorioSelectorDto? EntidadSeleccionada
    {
        get => (DirectorioSelectorDto?)GetValue(EntidadSeleccionadaProperty);
        set => SetValue(EntidadSeleccionadaProperty, value);
    }

    /// <inheritdoc/>
    public new bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    // ── Evento ───────────────────────────────────────────────────────────────

    /// <summary>Se dispara cuando el usuario selecciona una entidad del Directorio (ADR-038).</summary>
    public event EventHandler<DirectorioSelectorDto?>? SelectionChanged;

    // ── Estado interno ───────────────────────────────────────────────────────

    private readonly IDirectorioService                          _service;
    private readonly SessionService                              _session;
    private readonly ObservableCollection<DirectorioSelectorDto> _resultados = [];

    private CancellationTokenSource? _searchCts;
    private DispatcherTimer?         _debounceTimer;
    private const int                DebounceMs = 250;
    private bool                     _isSelecting;
    private bool                     _popupMouseOver;

    // ── Constructor ──────────────────────────────────────────────────────────

    public RelacionComercialSelectorControl()
    {
        _service = App.Services.GetService(typeof(IDirectorioService))
            as IDirectorioService
            ?? throw new InvalidOperationException("IDirectorioService no registrado en DI.");
        _session = App.Services.GetService(typeof(SessionService))
            as SessionService
            ?? throw new InvalidOperationException("SessionService no registrado en DI.");

        InitializeComponent();

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
        _debounceTimer.Tick += DebounceTimer_Tick;

        LvResultados.PointerEntered += (_, _) => _popupMouseOver = true;
        LvResultados.PointerExited  += (_, _) => _popupMouseOver = false;
    }

    // ── Cambio externo de EntidadSeleccionada (binding TwoWay) ──────────────

    private static void OnEntidadSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RelacionComercialSelectorControl ctrl)
            ctrl.ActualizarUI((DirectorioSelectorDto?)e.NewValue);
    }

    /// <summary>
    /// Actualiza la UI según el estado de selección.
    ///
    /// ESTADO A (sin selección):
    ///   InputBorder visible — TextBox de búsqueda activo.
    ///   EntityChipPanel colapsado.
    ///
    /// ESTADO B (con selección):
    ///   InputBorder colapsado — TextBox completamente oculto, sin texto residual.
    ///   EntityChipPanel visible — chip compacto con nombre + badge + botón limpiar.
    ///
    /// La entidad NUNCA se renderiza dentro del InputBorder/TextBox.
    /// </summary>
    private void ActualizarUI(DirectorioSelectorDto? entidad)
    {
        CerrarPopup();

        if (entidad is null)
        {
            // Estado A: buscador limpio
            _isSelecting     = true;
            TxtBusqueda.Text = string.Empty;
            _isSelecting     = false;

            InputBorder.Visibility    = Visibility.Visible;
            EntityChipPanel.Visibility = Visibility.Collapsed;

            Focus(FocusState.Programmatic);
        }
        else
        {
            // Estado B: ocultar input, mostrar chip compacto debajo
            _isSelecting     = true;
            TxtBusqueda.Text = string.Empty;   // limpiar sin disparar TextChanged
            _isSelecting     = false;

            InputBorder.Visibility    = Visibility.Collapsed;
            EntityChipPanel.Visibility = Visibility.Visible;

            MostrarChip(entidad);
        }
    }

    // ── Handlers de UI ───────────────────────────────────────────────────────

    private void TxtBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSelecting) return;

        var texto = TxtBusqueda.Text;

        if (string.IsNullOrWhiteSpace(texto))
        {
            LimpiarSeleccionSilencioso();
            return;
        }

        _debounceTimer!.Stop();
        _debounceTimer.Start();
    }

    private void TxtBusqueda_GotFocus(object sender, RoutedEventArgs e)
    {
        // No reabrir popup si ya hay una entidad seleccionada en el chip
        if (EntidadSeleccionada is not null) return;

        if (!string.IsNullOrWhiteSpace(TxtBusqueda.Text))
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        InputBorder.BorderBrush = XamlApp.Current.Resources["FocusStrokeColorOuterBrush"] as Brush
            ?? new SolidColorBrush(Colors.CornflowerBlue);
    }

    private void TxtBusqueda_LostFocus(object sender, RoutedEventArgs e)
    {
        InputBorder.BorderBrush = XamlApp.Current.Resources["TextControlBorderBrush"] as Brush
            ?? new SolidColorBrush(Colors.LightGray);

        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(150);
            if (!_popupMouseOver)
                CerrarPopup();
        });
    }

    private void TxtBusqueda_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down when ResultsPopup.IsOpen:
                if (_resultados.Count > 0)
                {
                    LvResultados.Focus(FocusState.Keyboard);
                    LvResultados.SelectedIndex = 0;
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                CerrarPopup();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Enter:
                if (_resultados.Count > 0)
                    ConfirmarSeleccion(_resultados[0]);
                e.Handled = true;
                break;
        }
    }

    private void LvResultados_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                if (LvResultados.SelectedItem is DirectorioSelectorDto sel)
                    ConfirmarSeleccion(sel);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                CerrarPopup();
                TxtBusqueda.Focus(FocusState.Keyboard);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Up when LvResultados.SelectedIndex == 0:
                LvResultados.SelectedIndex = -1;
                TxtBusqueda.Focus(FocusState.Keyboard);
                e.Handled = true;
                break;
        }
    }

    private void LvResultados_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvResultados.SelectedItem is DirectorioSelectorDto seleccionado)
            ConfirmarSeleccion(seleccionado);
    }

    private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
    {
        LimpiarSeleccion();
        // Restaurar foco en el buscador para que el usuario pueda buscar de nuevo
        DispatcherQueue.TryEnqueue(() => TxtBusqueda.Focus(FocusState.Keyboard));
    }

    // ── Debounce + Búsqueda ──────────────────────────────────────────────────

    private async void DebounceTimer_Tick(object? sender, object e)
    {
        _debounceTimer!.Stop();
        var texto = TxtBusqueda.Text?.Trim() ?? string.Empty;

        if (texto.Length < 2)
        {
            CerrarPopup();
            return;
        }

        await EjecutarBusquedaAsync(texto);
    }

    /// <summary>
    /// Búsqueda incremental con cancellation (ADR-026).
    /// Cancela búsqueda previa antes de iniciar nueva.
    /// </summary>
    private async Task EjecutarBusquedaAsync(string termino)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        MostrarCargando(true);
        _resultados.Clear();

        try
        {
            var empresaId = EmpresaId != 0 ? EmpresaId : _session.EmpresaId;
            var resultados = await _service.BuscarParaSelectorAsync(empresaId, termino, ct);

            ct.ThrowIfCancellationRequested();

            _resultados.Clear();
            foreach (var r in resultados)
                _resultados.Add(r);

            MostrarCargando(false);

            if (_resultados.Count > 0)
            {
                MostrarSinResultados(false);
                AbrirPopup();
            }
            else
            {
                MostrarSinResultados(true);
                AbrirPopup();
            }
        }
        catch (OperationCanceledException)
        {
            // Búsqueda cancelada por nueva pulsación — comportamiento esperado
        }
        catch (Exception)
        {
            MostrarCargando(false);
            CerrarPopup();
        }
    }

    // ── Selección ────────────────────────────────────────────────────────────

    private void ConfirmarSeleccion(DirectorioSelectorDto entidad)
    {
        // Cerrar popup ANTES de asignar la propiedad para evitar flash visual
        CerrarPopup();
        EntidadSeleccionada = entidad;
        SelectionChanged?.Invoke(this, entidad);
        // Mover foco al siguiente control en el TAB order (comportamiento natural ERP)
        Focus(FocusState.Programmatic);
    }

    private void LimpiarSeleccion()
    {
        EntidadSeleccionada = null;
        SelectionChanged?.Invoke(this, null);
    }

    /// <summary>Limpia la selección sin disparar SelectionChanged.</summary>
    private void LimpiarSeleccionSilencioso()
    {
        _isSelecting        = true;
        EntidadSeleccionada = null;
        _isSelecting        = false;
        // Restaurar Estado A
        InputBorder.Visibility     = Visibility.Visible;
        EntityChipPanel.Visibility = Visibility.Collapsed;
    }

    // ── Chip del seleccionado ─────────────────────────────────────────────────

    /// <summary>
    /// Rellena el EntityChipPanel con los datos de la entidad seleccionada.
    /// Muestra: glyph + nombre + badge institucional.
    /// El email y teléfono se muestran en el bloque de metadatos de la página — NO aquí,
    /// para evitar duplicar información que ya renderiza el contenedor padre.
    /// </summary>
    private void MostrarChip(DirectorioSelectorDto entidad)
    {
        ChipGlyph.Glyph  = entidad.Glyph;
        ChipNombre.Text  = entidad.DisplayName;

        // Badge con tokens institucionales Og* del ResourceDictionary
        var (label, bgKey, borderKey, fgKey) = entidad.TipoVisual switch
        {
            "Empresa"        => ("Empresa",        "OgBadgeEmpresaBg",  "OgBadgeEmpresaBorder", "OgBadgeEmpresaFg"),
            "Persona Física" => ("Persona Física", "OgBadgePersonaBg",  "OgBadgePersonaBorder", "OgBadgePersonaFg"),
            _                => (entidad.TipoVisual, "OgBadgePersonaBg", "OgBadgePersonaBorder", "OgBadgePersonaFg")
        };

        ChipBadgeText.Text       = label;
        ChipBadge.Background     = Resources.ContainsKey(bgKey)     ? (Brush)Resources[bgKey]     : new SolidColorBrush(Colors.LightGray);
        ChipBadge.BorderBrush    = Resources.ContainsKey(borderKey) ? (Brush)Resources[borderKey] : new SolidColorBrush(Colors.Gray);
        ChipBadgeText.Foreground = Resources.ContainsKey(fgKey)     ? (Brush)Resources[fgKey]     : new SolidColorBrush(Colors.Black);
    }

    // ── Popup: abrir / cerrar ────────────────────────────────────────────────

    private void AbrirPopup()
    {
        PopupBorder.MinWidth = ActualWidth > 0 ? ActualWidth : 300;
        ResultsPopup.IsOpen  = true;
    }

    private void CerrarPopup()
    {
        ResultsPopup.IsOpen        = false;
        LvResultados.SelectedIndex = -1;
    }

    // ── Estado visual del popup ──────────────────────────────────────────────

    private void MostrarCargando(bool activo)
    {
        LoadingRing.IsActive        = activo;
        TxtCargando.Visibility      = activo ? Visibility.Visible   : Visibility.Collapsed;
        LvResultados.Visibility     = activo ? Visibility.Collapsed : Visibility.Visible;
        TxtSinResultados.Visibility = Visibility.Collapsed;
    }

    private void MostrarSinResultados(bool mostrar)
    {
        TxtSinResultados.Visibility = mostrar ? Visibility.Visible   : Visibility.Collapsed;
        LvResultados.Visibility     = mostrar ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Limpia la selección programáticamente sin disparar eventos.
    /// Útil para resetear el formulario después de guardar.
    /// </summary>
    public void Reset()
    {
        _searchCts?.Cancel();
        _debounceTimer?.Stop();
        LimpiarSeleccion();
    }
}
