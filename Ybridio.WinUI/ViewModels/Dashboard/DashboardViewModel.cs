using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Ybridio.WinUI.ViewModels.Dashboard;

/// <summary>
/// ViewModel del Dashboard inicial.
/// Carga: ventas del día, estado de caja, productos con bajo stock y últimas ventas.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IVentaService _ventas;
    private readonly ICajaService _caja;
    private readonly IInventarioService _inventario;
    private readonly SessionService _session;

    // ── KPI cards ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private decimal ventasDelDia;

    [ObservableProperty]
    private int totalTransacciones;

    [ObservableProperty]
    private string cajaEstado = "Sin caja";

    [ObservableProperty]
    private string cajaDescripcion = string.Empty;

    [ObservableProperty]
    private bool isCajaActiva;

    // ── Listas ───────────────────────────────────────────────────────────────

    public ObservableCollection<VentaDto> UltimasVentas { get; } = [];

    public ObservableCollection<ExistenciaDto> BajoStock { get; } = [];

    // ── Loading ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public DashboardViewModel(
        IVentaService ventas,
        ICajaService caja,
        IInventarioService inventario,
        SessionService session)
    {
        _ventas = ventas;
        _caja = caja;
        _inventario = inventario;
        _session = session;
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // Secuencial en lugar de WhenAll — evita concurrencia en DbContext
            await LoadVentasAsync(ct);
            await LoadCajaAsync(ct);
            await LoadBajoStockAsync(ct);
        }
        catch (OperationCanceledException) { /* navegación cancelada */ }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar el dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadVentasAsync(CancellationToken ct)
    {
        if (_session.EmpresaId == 0) return;

        var hoy = DateTime.UtcNow.Date;
        var lista = await _ventas.ListarPorEmpresaAsync(_session.EmpresaId, hoy, hoy.AddDays(1).AddTicks(-1), ct);

        UltimasVentas.Clear();
        foreach (var v in lista.Take(10))
            UltimasVentas.Add(v);

        VentasDelDia = lista.Sum(v => v.Total);
        TotalTransacciones = lista.Count;
    }

    private async Task LoadCajaAsync(CancellationToken ct)
    {
        if (_session.Usuario is null) return;

        var result = await _caja.ObtenerCajaActivaAsync(_session.Usuario.Id, ct);
        if (result.Success && result.Value is not null)
        {
            CajaEstado = "Abierta";
            CajaDescripcion = result.Value.CajaNombre;
            IsCajaActiva = true;
        }
        else
        {
            CajaEstado = "Cerrada";
            CajaDescripcion = "No hay caja activa";
            IsCajaActiva = false;
        }
    }

    private async Task LoadBajoStockAsync(CancellationToken ct)
    {
        if (_session.EmpresaId == 0) return;

        var existencias = await _inventario.ListarExistenciasAsync(_session.EmpresaId, null, ct);

        BajoStock.Clear();
        // Umbral de bajo stock: menos de 5 unidades
        foreach (var e in existencias.Where(e => e.Cantidad < 5).Take(10))
            BajoStock.Add(e);
    }
}
