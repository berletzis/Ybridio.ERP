using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.ViewModels.Diagnostic;

/// <summary>
/// ViewModel del panel de diagnóstico runtime.
/// Refresca automáticamente el snapshot desde <see cref="RuntimeDiagnosticService"/> cada 2 segundos.
/// No genera overhead en producción: el timer solo corre cuando el panel es visible (IsDeveloperMode).
/// </summary>
public sealed partial class DiagnosticPanelViewModel : ObservableObject
{
    private readonly RuntimeDiagnosticService _diagnostic;
    private DispatcherTimer? _timer;
    private RuntimeContextSnapshot?      _s;
    private GridOperationContext?        _op;
    private CurrentOperationalContext?   _ctx;

    // ── Estado del panel ──────────────────────────────────────────────────────

    [ObservableProperty] private bool   isExpanded;
    [ObservableProperty] private int    activeTab;
    [ObservableProperty] private string statusBar = "RUNTIME DIAGNOSTICS";

    // ── Derivadas de IsExpanded ───────────────────────────────────────────────

    public Visibility ContentVisibility  => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TabStripVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string     ToggleArrow        => IsExpanded ? "▼" : "▲";

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ContentVisibility));
        OnPropertyChanged(nameof(TabStripVisibility));
        OnPropertyChanged(nameof(ToggleArrow));
    }

    // ── Visibilidades por tab ─────────────────────────────────────────────────

    public Visibility Tab0Vis => ActiveTab == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Tab1Vis => ActiveTab == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Tab2Vis => ActiveTab == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Tab3Vis => ActiveTab == 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Tab4Vis => ActiveTab == 4 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Tab5Vis => ActiveTab == 5 ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyTabVisibilities()
    {
        OnPropertyChanged(nameof(Tab0Vis));
        OnPropertyChanged(nameof(Tab1Vis));
        OnPropertyChanged(nameof(Tab2Vis));
        OnPropertyChanged(nameof(Tab3Vis));
        OnPropertyChanged(nameof(Tab4Vis));
        OnPropertyChanged(nameof(Tab5Vis));
    }

    // ── TAB 0 — Contexto ─────────────────────────────────────────────────────

    public string CtxAuthenticated  => _s?.IsAuthenticated == true ? "✓ Autenticado"   : "✗ Sin sesión";
    public string CtxUsuarioNombre  => _s?.UsuarioNombre   ?? "—";
    public string CtxUsuarioEmail   => _s?.UsuarioEmail    ?? "—";
    public string CtxUsuarioUser    => _s?.UsuarioUserName ?? "—";
    public string CtxUsuarioId      => _s?.UsuarioId?.ToString("D") ?? "—";
    public string CtxEmpresaId      => _s?.EmpresaId.ToString()     ?? "—";
    public string CtxSucursalId     => _s?.SucursalId.ToString()    ?? "—";
    public string CtxSucursalNombre => _s?.SucursalNombre ?? "—";
    public string CtxCaja           => _s?.HasCajaActiva == true ? $"✓ {_s.CajaNombre}" : "— Sin caja activa";

    // ── TAB 1 — Seguridad ────────────────────────────────────────────────────

    public string SecAuthenticated   => _s?.IsAuthenticated == true ? "✓ Sesión activa" : "✗ Sin autenticar";
    public string SecEmpresaContext  => _s?.EmpresaId  != 0 ? $"✓ Empresa {_s.EmpresaId}"   : "⚠ Sin empresa (EmpresaId=0)";
    public string SecSucursalContext => _s?.SucursalId != 0 ? $"✓ Sucursal {_s.SucursalId}" : "⚠ Sin sucursal (SucursalId=0)";

    // ── TAB 2 — Workspace (Navegación real de módulos) ───────────────────────

    /// <summary>Cantidad de módulos visitados en la sesión (uno por módulo único).</summary>
    public string WsTabCount    => _s?.NavigationHistory.Count.ToString() ?? "0";

    /// <summary>Módulo actualmente activo.</summary>
    public string WsActiveTitle => _s?.CurrentContext?.Module ?? "—";

    /// <summary>SubMódulo actualmente activo.</summary>
    public string WsActiveSubModule => _s?.CurrentContext?.SubModule ?? "—";

    /// <summary>Módulo key activo.</summary>
    public string WsActiveKey   => _s?.ActiveModuleKey ?? "—";

    /// <summary>Historial de módulos navegados en la sesión.</summary>
    public IReadOnlyList<ModuleNavigationEntry> NavigationHistory =>
        _s?.NavigationHistory ?? [];

    // WorkspaceService persistente (para futuros workspace items)
    public string WsPersistentTabCount => _s?.WorkspaceTabCount.ToString() ?? "0";
    public IReadOnlyList<WorkspaceTabDisplay> WsPersistentTabs =>
        _s?.WorkspaceTabs.Select(t => new WorkspaceTabDisplay(
            t.Key, t.Title, t.IsClosable,
            FormatAge(DateTime.Now - t.CreatedAt)
        )).ToList() ?? [];

    private static string FormatAge(TimeSpan age) =>
        age.TotalMinutes < 1 ? $"{(int)age.TotalSeconds}s" :
        age.TotalHours   < 1 ? $"{(int)age.TotalMinutes}m {age.Seconds:D2}s" :
                               $"{(int)age.TotalHours}h {age.Minutes:D2}m";

    // ── TAB 3 — Filtros EF ───────────────────────────────────────────────────

    public string FilterSoftDelete  => "✔  !Borrado activo — AuditableEntity + CreationAuditEntity";
    public string FilterEmpresa     => _s?.HasEmpresaFilter  == true ? $"✔  EmpresaId = {_s.EmpresaId}" : "❌  EmpresaId = 0 — filtro global desactivado";
    public string FilterSucursal    => _s?.HasSucursalFilter == true ? $"✔  SucursalId = {_s.SucursalId}" : "⚠  SucursalId = 0 — sin filtro de sucursal";

    // ── TAB 4 — Current Context (VIVO) ───────────────────────────────────────

    public Visibility CtxActiveVisibility   => _ctx is not null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CtxPartialVisibility  => _ctx is { HasViewModelContext: false } ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoCtxVisibility       => _ctx is null     ? Visibility.Visible : Visibility.Collapsed;

    public string CurModule      => _ctx?.Module    ?? "—";
    public string CurSubModule   => _ctx?.SubModule ?? "(esperando sub-módulo...)";
    public string CurViewModel   => _ctx?.ViewModel ?? "(esperando carga de ViewModel...)";
    public string CurEntity      => _ctx?.Entity    ?? "—";
    public string CurRecords     => _ctx is null ? "—" : $"{_ctx.RecordCount} registro(s) visible(s)";
    public string CurSearch      => string.IsNullOrWhiteSpace(_ctx?.SearchTerm) ? "(vacío)" : _ctx!.SearchTerm;
    public string CurSoloActivos => _ctx?.SoloActivos == true ? "Sí" : "No";
    public string CurCategoria   => _ctx?.CategoriaFiltro ?? "(ninguna)";
    public string CurFiltroTemp  => _ctx?.FiltroTemporal  ?? "—";
    public string CurAge         => _ctx?.AgeDisplay ?? "—";
    public string CurSource      => _ctx?.Source ?? "—";

    public string CurFilterEmpresa    => FormatFilter(_ctx?.EmpresaFilter);
    public string CurFilterSucursal   => FormatFilter(_ctx?.SucursalFilter);
    public string CurFilterAlmacen    => FormatFilter(_ctx?.AlmacenFilter);
    public string CurFilterSoftDelete => FormatFilter(_ctx?.SoftDeleteFilter);

    // ── TAB 4 — Operacional (antes: Runtime) ─────────────────────────────────

    // Última operación
    public bool HasOperation           => _op is not null;
    public Visibility OpPanelVisibility => _op is not null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoOpVisibility    => _op is null     ? Visibility.Visible : Visibility.Collapsed;

    public string OpModule       => _op?.Module    ?? "—";
    public string OpSubModule    => _op?.SubModule ?? "—";
    public string OpViewModel    => _op?.ViewModel ?? "—";
    public string OpEntity       => _op?.Entity    ?? "—";
    public string OpRecords      => _op is null ? "—" : $"{_op.RecordCount} registro(s)";
    public string OpDuration     => _op is null ? "—" : $"{_op.Duration.TotalMilliseconds:F1} ms";
    public string OpTimestamp    => _op?.Timestamp.ToString("HH:mm:ss.fff") ?? "—";
    public string OpSearch       => string.IsNullOrWhiteSpace(_op?.SearchTerm) ? "(vacío)" : _op!.SearchTerm;
    public string OpSoloActivos  => _op?.SoloActivos == true ? "Sí" : "No";
    public string OpCategoria    => _op?.CategoriaFiltro ?? "(ninguna)";
    public string OpFiltroTemp   => _op?.FiltroTemporal  ?? "—";
    public string OpNotes        => _op?.Notes is { Count: > 0 } ? string.Join("\n", _op.Notes) : "—";

    // Estados de filtros operacionales
    public string OpFilterEmpresa    => FormatFilter(_op?.EmpresaFilter);
    public string OpFilterSucursal   => FormatFilter(_op?.SucursalFilter);
    public string OpFilterAlmacen    => FormatFilter(_op?.AlmacenFilter);
    public string OpFilterSoftDelete => FormatFilter(_op?.SoftDeleteFilter);

    // Historial de operaciones
    public IReadOnlyList<GridOperationContext> RecentOps => _s?.RecentOperations ?? [];

    private static string FormatFilter(FilterDetail? f)
    {
        if (f is null) return "—";
        return f.State switch
        {
            FilterState.Applied         => $"✔  Aplicado{(f.Value is not null ? $" = {f.Value}" : "")}",
            FilterState.OmittedExpected => $"⚠  Omitido — {f.Note ?? "N/A para este módulo"}",
            FilterState.Missing         => $"❌  Faltante — {f.Note ?? "debería aplicarse"}",
            _                           => "?"
        };
    }

    // ── TAB 5 — Alertas ──────────────────────────────────────────────────────

    public IReadOnlyList<DiagnosticAlert> Alerts => _s?.Alerts ?? [];
    public string AlertCount => $"{_s?.Alerts.Count ?? 0} alerta(s)";

    // ── Runtime general ───────────────────────────────────────────────────────

    public string RuntimeTimestamp => _s?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "—";

    // ─────────────────────────────────────────────────────────────────────────

    public DiagnosticPanelViewModel(RuntimeDiagnosticService diagnostic)
    {
        _diagnostic = diagnostic;
    }

    public void StartMonitoring()
    {
        Refresh();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    public void StopMonitoring()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Refresh()
    {
        _s   = _diagnostic.GetSnapshot();
        _op  = _s.LastOperation;
        _ctx = _s.CurrentContext;

        OnPropertyChanged(string.Empty);

        var ctxSummary = _ctx is not null
            ? (_ctx.HasViewModelContext
                ? $"{_ctx.Module} > {_ctx.SubModule}  {_ctx.RecordCount} reg"
                : $"{_ctx.Module} (cargando...)")
            : "sin contexto";

        StatusBar = $"RUNTIME  ▸  E:{_s.EmpresaId}  S:{_s.SucursalId}" +
                    $"  ▸  {ctxSummary}" +
                    $"  ▸  {_s.Timestamp:HH:mm:ss}";
    }

    // ── Comandos ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void TogglePanel() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void SelectTab(int tab)
    {
        ActiveTab = tab;
        NotifyTabVisibilities();
    }
}

/// <summary>Helper de presentación para tabs del workspace en el panel diagnóstico.</summary>
public sealed record WorkspaceTabDisplay(string Key, string Title, bool IsClosable, string Age);
