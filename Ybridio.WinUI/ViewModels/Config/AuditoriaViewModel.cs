using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ybridio.Infrastructure.Persistence.Audit;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// Fila de presentación para un <see cref="SchemaAuditEntry"/>.
/// Expone la etiqueta y el color de severidad para binding directo en XAML x:Bind.
/// </summary>
public sealed class AuditEntryRow
{
    public AuditSeverity   Severity      { get; }
    public string          SeverityLabel { get; }
    public string          Module        { get; }
    public string          Category      { get; }
    public string          Message       { get; }
    public string          Suggestion    { get; }
    public SolidColorBrush SeverityBrush { get; }

    internal AuditEntryRow(SchemaAuditEntry entry)
    {
        Severity = entry.Severity;

        (SeverityLabel, var color) = entry.Severity switch
        {
            AuditSeverity.Critical         => ("CRÍTICO",    Color.FromArgb(255, 215,  38,  38)),
            AuditSeverity.Error            => ("ERROR",      Color.FromArgb(255, 200, 100,  10)),
            AuditSeverity.Warning          => ("WARN",       Color.FromArgb(255, 175, 125,   0)),
            AuditSeverity.LegacyData       => ("LEGACY",     Color.FromArgb(255,   0, 100, 160)),
            AuditSeverity.MigrationPending => ("MIGR.PEND",  Color.FromArgb(255, 130,  20, 180)),
            _                              => ("INFO",        Color.FromArgb(255, 120, 120, 120)),
        };

        Module        = entry.Module ?? "General";
        Category      = entry.Category;
        Message       = entry.Message;
        Suggestion    = entry.Suggestion ?? string.Empty;
        SeverityBrush = new SolidColorBrush(color);
    }
}

/// <summary>
/// ViewModel del tab "Auditoría del Sistema" en Configuración Global.
/// Orquesta SchemaAuditService (estructura EF), DatabaseAuditService (datos catálogos)
/// y WorkflowAuditService (lifecycle/snapshots/legacy). Sin lógica de negocio ni acceso directo a datos.
/// </summary>
/// <summary>Métrica por módulo para el panel ejecutivo.</summary>
public sealed record ModuleMetric(string Module, int Critical, int Errors, int Total);

/// <summary>
/// ViewModel del tab "Auditoría del Sistema" en Configuración Global.
/// Orquesta los 4 servicios de auditoría ERP: SchemaAudit, DataAudit, WorkflowAudit, CommercialIntegrity.
/// Sin lógica de negocio ni acceso directo a datos.
/// </summary>
public sealed partial class AuditoriaViewModel : ObservableObject
{
    private readonly ISchemaAuditService              _auditService;
    private readonly IDatabaseAuditService             _dataAuditService;
    private readonly IWorkflowAuditService             _workflowAuditService;
    private readonly ICommercialIntegrityAuditService  _commercialAuditService;
    private readonly List<AuditEntryRow>               _todosHallazgos = [];
    private SchemaAuditReport?                         _ultimoReporte;
    private string?                                    _filtroSeveridad;
    private string?                                    _filtroModulo;

    public ObservableCollection<AuditEntryRow>  HallazgosVisibles { get; } = [];
    public ObservableCollection<ModuleMetric>   ModuleMetrics     { get; } = [];
    public ObservableCollection<string>         ModulosDisponibles{ get; } = [];

    [ObservableProperty] private bool       isBusy;
    [ObservableProperty] private int        totalCritical;
    [ObservableProperty] private int        totalErrors;
    [ObservableProperty] private int        totalWarnings;
    [ObservableProperty] private int        totalLegacy;
    [ObservableProperty] private int        totalMigrationPending;
    [ObservableProperty] private string     resumenEstado         = "Sin ejecutar — presione una auditoría.";
    [ObservableProperty] private string     errorMessage          = string.Empty;
    [ObservableProperty] private string     successMessage        = string.Empty;
    [ObservableProperty] private Visibility estadoVacioVisibility = Visibility.Visible;
    [ObservableProperty] private string     filtroActivoLabel     = "Todos";
    [ObservableProperty] private string     moduloFiltroLabel     = "Todos los módulos";

    public AuditoriaViewModel(
        ISchemaAuditService              auditService,
        IDatabaseAuditService            dataAuditService,
        IWorkflowAuditService            workflowAuditService,
        ICommercialIntegrityAuditService  commercialAuditService)
    {
        _auditService           = auditService;
        _dataAuditService       = dataAuditService;
        _workflowAuditService   = workflowAuditService;
        _commercialAuditService = commercialAuditService;
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    /// <summary>Auditoría de esquema: compara modelo EF vs estructura real de la BD.</summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaAsync(CancellationToken ct)
        => await EjecutarYMostrarAsync(
            () => _auditService.RunAsync(ct),
            "Auditoría de Esquema", ct);

    /// <summary>Auditoría de datos: duplicados, FK catálogos, dependencias dbo legacy.</summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaDatosAsync(CancellationToken ct)
        => await EjecutarYMostrarAsync(
            () => _dataAuditService.RunAsync(ct),
            "Auditoría de Datos", ct);

    /// <summary>
    /// Auditoría de workflow: lifecycle documental, snapshots, clasificación legacy,
    /// scripts manuales pendientes y folios.
    /// </summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaWorkflowAsync(CancellationToken ct)
        => await EjecutarYMostrarAsync(
            () => _workflowAuditService.RunAsync(ct),
            "Auditoría de Workflow", ct);

    /// <summary>Auditoría de integridad comercial: cadena COT→PED→VTA, totales financieros,
    /// pagos, aging, referencias cruzadas, crédito/CxC, audit trail.</summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaComercialAsync(CancellationToken ct)
        => await EjecutarYMostrarAsync(
            () => _commercialAuditService.RunAsync(ct),
            "Integridad Comercial", ct);

    /// <summary>Filtra hallazgos por módulo de negocio.</summary>
    [RelayCommand]
    private void FiltrarModulo(string? modulo)
    {
        _filtroModulo     = string.IsNullOrEmpty(modulo) ? null : modulo;
        ModuloFiltroLabel = _filtroModulo is null ? "Todos los módulos" : _filtroModulo;
        AplicarFiltro();
    }

    private async Task EjecutarYMostrarAsync(
        Func<Task<SchemaAuditReport>> ejecutar, string etiqueta, CancellationToken ct)
    {
        IsBusy = true; ErrorMessage = string.Empty; SuccessMessage = string.Empty;
        try
        {
            _ultimoReporte = await ejecutar();

            _todosHallazgos.Clear();
            _todosHallazgos.AddRange(_ultimoReporte.Entries.Select(e => new AuditEntryRow(e)));

            TotalCritical        = _ultimoReporte.CriticalCount;
            TotalErrors          = _ultimoReporte.ErrorCount;
            TotalWarnings        = _ultimoReporte.WarningCount;
            TotalLegacy          = _ultimoReporte.LegacyDataCount;
            TotalMigrationPending = _ultimoReporte.MigrationPendingCount;

            ResumenEstado = _ultimoReporte.HasCriticalErrors
                ? $"⚠ {_ultimoReporte.CriticalCount} CRÍTICO(S) — acción inmediata requerida"
                : _ultimoReporte.HasErrors
                    ? $"{_ultimoReporte.ErrorCount} error(es) | {_ultimoReporte.WarningCount} advertencia(s)"
                    : _ultimoReporte.HasPendingMigrations
                        ? $"{_ultimoReporte.MigrationPendingCount} script(s) pendiente(s) | {_ultimoReporte.LegacyDataCount} legacy"
                        : $"✓ Sin errores críticos | {_ultimoReporte.LegacyDataCount} legacy | {_ultimoReporte.WarningCount} warn";

            // Calcular métricas por módulo para el panel ejecutivo
            ActualizarModuleMetrics();

            _filtroSeveridad = null; _filtroModulo = null;
            FiltroActivoLabel = "Todos"; ModuloFiltroLabel = "Todos los módulos";
            AplicarFiltro();
            SuccessMessage = $"{etiqueta} completada: {_ultimoReporte.Entries.Count} hallazgo(s).";
        }
        catch (OperationCanceledException) { /* ADR-026 */ }
        catch (Exception ex) { ErrorMessage = $"Error en {etiqueta}: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Exporta el último reporte a un archivo JSON elegido por el usuario.
    /// No hace nada si aún no se ejecutó la auditoría.
    /// </summary>
    [RelayCommand]
    private async Task ExportarJsonAsync()
    {
        if (_ultimoReporte is null)
        {
            ErrorMessage = "Ejecute la auditoría antes de exportar.";
            return;
        }

        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName      = $"schema-audit-{DateTime.Now:yyyyMMdd-HHmmss}"
            };
            picker.FileTypeChoices.Add("JSON", [".json"]);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                App.Services.GetRequiredService<MainWindow>());
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, _ultimoReporte.ToJsonString());
                SuccessMessage = $"Reporte exportado a: {file.Name}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al exportar: {ex.Message}";
        }
    }

    /// <summary>
    /// Filtra los hallazgos visibles por severidad.
    /// Parámetro vacío o null = mostrar todos.
    /// </summary>
    [RelayCommand]
    private void Filtrar(string? filtro)
    {
        _filtroSeveridad = string.IsNullOrEmpty(filtro) ? null : filtro;

        FiltroActivoLabel = _filtroSeveridad switch
        {
            "Critical"         => "Solo Críticos",
            "Error"            => "Solo Errores",
            "Warning"          => "Solo Advertencias",
            "LegacyData"       => "Solo Legacy",
            "MigrationPending" => "Solo Migr. Pend.",
            "Info"             => "Solo Info",
            _                  => "Todos"
        };

        AplicarFiltro();
    }

    // ── Helpers privados ─────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        HallazgosVisibles.Clear();

        IEnumerable<AuditEntryRow> fuente = _todosHallazgos;

        if (_filtroSeveridad is not null)
            fuente = fuente.Where(h => h.Severity.ToString() == _filtroSeveridad);

        if (_filtroModulo is not null)
            fuente = fuente.Where(h => (h.Module ?? "General") == _filtroModulo);

        foreach (var fila in fuente)
            HallazgosVisibles.Add(fila);

        EstadoVacioVisibility = HallazgosVisibles.Count == 0 && !IsBusy
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ActualizarModuleMetrics()
    {
        if (_ultimoReporte is null) return;

        ModuleMetrics.Clear();
        ModulosDisponibles.Clear();
        ModulosDisponibles.Add("Todos los módulos");

        var breakdown = _ultimoReporte.Entries
            .GroupBy(e => e.Module ?? "General")
            .OrderByDescending(g => g.Count(e => e.Severity is AuditSeverity.Critical or AuditSeverity.Error))
            .ThenBy(g => g.Key)
            .ToList();

        foreach (var group in breakdown)
        {
            var crit  = group.Count(e => e.Severity == AuditSeverity.Critical);
            var err   = group.Count(e => e.Severity == AuditSeverity.Error);
            var total = group.Count();
            ModuleMetrics.Add(new ModuleMetric(group.Key, crit, err, total));
            ModulosDisponibles.Add(group.Key);
        }
    }
}
