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
/// Expone el color de severidad calculado para binding directo en XAML x:Bind.
/// </summary>
public sealed class AuditEntryRow
{
    /// <summary>Severidad original del hallazgo.</summary>
    public AuditSeverity Severity { get; }

    /// <summary>Etiqueta localizada de la severidad (CRÍTICO / ERROR / WARN / INFO).</summary>
    public string SeverityLabel { get; }

    /// <summary>Categoría del hallazgo (Tablas, Columnas, Relaciones, etc.).</summary>
    public string Category { get; }

    /// <summary>Descripción del problema detectado.</summary>
    public string Message { get; }

    /// <summary>Acción sugerida para corregir el problema.</summary>
    public string Suggestion { get; }

    /// <summary>Color correspondiente a la severidad para resaltado visual.</summary>
    public SolidColorBrush SeverityBrush { get; }

    internal AuditEntryRow(SchemaAuditEntry entry)
    {
        Severity = entry.Severity;

        SeverityLabel = entry.Severity switch
        {
            AuditSeverity.Critical => "CRÍTICO",
            AuditSeverity.Error    => "ERROR",
            AuditSeverity.Warning  => "WARN",
            _                      => "INFO"
        };

        Category   = entry.Category;
        Message    = entry.Message;
        Suggestion = entry.Suggestion ?? string.Empty;

        SeverityBrush = new SolidColorBrush(entry.Severity switch
        {
            AuditSeverity.Critical => Color.FromArgb(255, 215, 38,  38),  // rojo
            AuditSeverity.Error    => Color.FromArgb(255, 200, 100, 10),  // naranja
            AuditSeverity.Warning  => Color.FromArgb(255, 175, 125,  0),  // ámbar
            _                      => Color.FromArgb(255, 120, 120, 120)  // gris
        });
    }
}

/// <summary>
/// ViewModel del tab "Auditoría del Sistema" en Configuración Global.
/// Orquesta la ejecución de <see cref="ISchemaAuditService"/>, filtrado de hallazgos
/// y exportación a JSON. No contiene lógica de negocio ni acceso directo a datos.
/// </summary>
public sealed partial class AuditoriaViewModel : ObservableObject
{
    private readonly ISchemaAuditService   _auditService;
    private readonly IDatabaseAuditService _dataAuditService;
    private readonly List<AuditEntryRow>   _todosHallazgos = [];
    private SchemaAuditReport?             _ultimoReporte;
    private string?                        _filtroActual;

    /// <summary>Hallazgos visibles después de aplicar el filtro activo.</summary>
    public ObservableCollection<AuditEntryRow> HallazgosVisibles { get; } = [];

    [ObservableProperty] private bool       isBusy;
    [ObservableProperty] private int        totalCritical;
    [ObservableProperty] private int        totalErrors;
    [ObservableProperty] private int        totalWarnings;
    [ObservableProperty] private string     resumenEstado   = "Sin ejecutar — presione 'Ejecutar Auditoría'.";
    [ObservableProperty] private string     errorMessage    = string.Empty;
    [ObservableProperty] private string     successMessage  = string.Empty;
    [ObservableProperty] private Visibility estadoVacioVisibility = Visibility.Visible;
    [ObservableProperty] private string     filtroActivoLabel     = "Todos";

    public AuditoriaViewModel(
        ISchemaAuditService   auditService,
        IDatabaseAuditService dataAuditService)
    {
        _auditService     = auditService;
        _dataAuditService = dataAuditService;
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    /// <summary>Ejecuta la auditoría completa contra la base de datos y actualiza los resultados.</summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaAsync(CancellationToken ct)
    {
        IsBusy         = true;
        ErrorMessage   = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            _ultimoReporte = await _auditService.RunAsync(ct);

            _todosHallazgos.Clear();
            _todosHallazgos.AddRange(_ultimoReporte.Entries.Select(e => new AuditEntryRow(e)));

            TotalCritical = _ultimoReporte.CriticalCount;
            TotalErrors   = _ultimoReporte.ErrorCount;
            TotalWarnings = _ultimoReporte.WarningCount;

            ResumenEstado = _ultimoReporte.HasCriticalErrors
                ? $"⚠ Esquema con errores críticos — {_ultimoReporte.CriticalCount} crítico(s), {_ultimoReporte.ErrorCount} error(es)"
                : _ultimoReporte.HasErrors
                    ? $"Errores no críticos detectados — {_ultimoReporte.ErrorCount} error(es), {_ultimoReporte.WarningCount} advertencia(s)"
                    : $"✓ Esquema consistente — {_ultimoReporte.WarningCount} advertencia(s)";

            _filtroActual = null;
            FiltroActivoLabel = "Todos";
            AplicarFiltro();

            SuccessMessage = $"Auditoría completada: {_ultimoReporte.Entries.Count} hallazgo(s).";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al ejecutar la auditoría: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Ejecuta la auditoría de integridad de datos post-migración
    /// (duplicados, FK rotas, consistencia de catálogos, dependencias dbo).
    /// </summary>
    [RelayCommand]
    private async Task EjecutarAuditoriaDatosAsync(CancellationToken ct)
    {
        IsBusy         = true;
        ErrorMessage   = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            _ultimoReporte = await _dataAuditService.RunAsync(ct);

            _todosHallazgos.Clear();
            _todosHallazgos.AddRange(_ultimoReporte.Entries.Select(e => new AuditEntryRow(e)));

            TotalCritical = _ultimoReporte.CriticalCount;
            TotalErrors   = _ultimoReporte.ErrorCount;
            TotalWarnings = _ultimoReporte.WarningCount;

            ResumenEstado = _ultimoReporte.HasCriticalErrors
                ? $"⚠ Datos con errores críticos — {_ultimoReporte.CriticalCount} crítico(s)"
                : _ultimoReporte.HasErrors
                    ? $"Inconsistencias detectadas — {_ultimoReporte.ErrorCount} error(es)"
                    : $"✓ Datos consistentes — {_ultimoReporte.WarningCount} advertencia(s)";

            _filtroActual = null;
            FiltroActivoLabel = "Todos";
            AplicarFiltro();

            SuccessMessage = $"Auditoría de datos completada: {_ultimoReporte.Entries.Count} hallazgo(s).";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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
        _filtroActual = string.IsNullOrEmpty(filtro) ? null : filtro;

        FiltroActivoLabel = _filtroActual switch
        {
            "Critical" => "Solo Críticos",
            "Error"    => "Solo Errores",
            "Warning"  => "Solo Advertencias",
            "Info"     => "Solo Info",
            _          => "Todos"
        };

        AplicarFiltro();
    }

    // ── Helpers privados ─────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        HallazgosVisibles.Clear();

        var fuente = _filtroActual is null
            ? _todosHallazgos
            : _todosHallazgos.Where(h => h.Severity.ToString() == _filtroActual);

        foreach (var fila in fuente)
            HallazgosVisibles.Add(fila);

        EstadoVacioVisibility = HallazgosVisibles.Count == 0 && !IsBusy
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
