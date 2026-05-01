using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.POS;

/// <summary>
/// Representa un ítem del carrito en el POS.
/// </summary>
public sealed partial class CartItemViewModel : ObservableObject
{
    public int ProductoId { get; }
    public int AlmacenId { get; }
    public string Nombre { get; }
    public decimal PrecioUnitario { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Importe))]
    private decimal cantidad = 1;

    public decimal Importe => Cantidad * PrecioUnitario;

    public CartItemViewModel(int productoId, int almacenId, string nombre, decimal precio)
    {
        ProductoId = productoId;
        AlmacenId = almacenId;
        Nombre = nombre;
        PrecioUnitario = precio;
    }
}

/// <summary>
/// ViewModel del POS (Mostrador).
/// Gestiona: catálogo de productos (desde existencias), carrito, total en tiempo real y venta.
/// </summary>
public sealed partial class PosViewModel : ObservableObject
{
    private readonly IInventarioService _inventario;
    private readonly IVentaService _ventaService;
    private readonly SessionService _session;

    // ── Catálogo ─────────────────────────────────────────────────────────────

    public ObservableCollection<ExistenciaDto> Productos { get; } = [];

    [ObservableProperty]
    private string busqueda = string.Empty;

    private IReadOnlyList<ExistenciaDto> _todosLosProductos = [];

    // ── Carrito ──────────────────────────────────────────────────────────────

    public ObservableCollection<CartItemViewModel> Carrito { get; } = [];

    [ObservableProperty]
    private decimal total;

    // ── Estado ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string successMessage = string.Empty;

    // Almacén activo (simplificación: se usa el primero disponible)
    private int _almacenId;

    public PosViewModel(IInventarioService inventario, IVentaService ventaService, SessionService session)
    {
        _inventario = inventario;
        _ventaService = ventaService;
        _session = session;

        Carrito.CollectionChanged += (_, _) => RecalcularTotal();
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadProductosAsync(CancellationToken ct = default)
    {
        if (_session.EmpresaId == 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _todosLosProductos = await _inventario.ListarExistenciasAsync(_session.EmpresaId, null, ct);
            _almacenId = _todosLosProductos.FirstOrDefault()?.AlmacenId ?? 0;
            AplicarFiltro();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AgregarAlCarrito(ExistenciaDto producto)
    {
        ArgumentNullException.ThrowIfNull(producto);

        var existente = Carrito.FirstOrDefault(c => c.ProductoId == producto.ProductoId && c.AlmacenId == producto.AlmacenId);
        if (existente is not null)
        {
            existente.Cantidad++;
        }
        else
        {
            // Usamos el nombre del producto desde existencia; precio queda pendiente de catálogo.
            // Por ahora se deja en 0; se completará cuando exista un ProductoService.
            Carrito.Add(new CartItemViewModel(producto.ProductoId, producto.AlmacenId, producto.ProductoNombre, 0));
        }

        RecalcularTotal();
    }

    [RelayCommand]
    private void QuitarDelCarrito(CartItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Carrito.Remove(item);
        RecalcularTotal();
    }

    [RelayCommand]
    private void LimpiarCarrito()
    {
        Carrito.Clear();
        RecalcularTotal();
    }

    [RelayCommand(CanExecute = nameof(CanVender))]
    private async Task VenderAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        IsBusy = true;
        VenderCommand.NotifyCanExecuteChanged();

        try
        {
            if (_session.Usuario is null)
            {
                ErrorMessage = "No hay sesión activa.";
                return;
            }

            var cajaActiva = _session.CajaActiva;
            if (cajaActiva is null)
            {
                ErrorMessage = "Debes abrir una caja antes de vender.";
                return;
            }

            var detalles = Carrito.Select(c => new RegistrarVentaDetalleDto(
                c.ProductoId,
                c.AlmacenId,
                c.Cantidad,
                c.PrecioUnitario)).ToList();

            var dto = new RegistrarVentaDto(
                _session.EmpresaId,
                _session.TiendaId,
                cajaActiva.CajaId,
                cajaActiva.Id,
                DateTime.UtcNow,
                detalles);

            var result = await _ventaService.CrearVentaAsync(dto, _session.Usuario.Id, ct);

            if (result.Succeeded)
            {
                SuccessMessage = $"Venta #{result.Value!.Id} registrada correctamente.";
                Carrito.Clear();
                RecalcularTotal();
                // Recargar catálogo para reflejar nuevo stock
                await LoadProductosAsync(ct);
            }
            else
            {
                ErrorMessage = MapError(result.Error, result.ErrorCode);
            }
        }
        finally
        {
            IsBusy = false;
            VenderCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanVender() => !IsBusy && Carrito.Count > 0;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RecalcularTotal()
    {
        Total = Carrito.Sum(c => c.Importe);
        VenderCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Productos.Clear();
        var filtro = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(filtro)
            ? _todosLosProductos
            : _todosLosProductos.Where(p =>
                p.ProductoNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                p.ProductoCodigo.Contains(filtro, StringComparison.OrdinalIgnoreCase));

        foreach (var p in lista)
            Productos.Add(p);
    }

    private static string MapError(string? message, ErrorCode code) => code switch
    {
        ErrorCode.StockInsuficiente => "Stock insuficiente para uno o más productos.",
        ErrorCode.CajaCerrada => "La caja está cerrada. Ábrela antes de continuar.",
        ErrorCode.Unauthorized or ErrorCode.Forbidden => "No tienes permiso para realizar ventas.",
        ErrorCode.ConcurrencyConflict => "Conflicto de concurrencia. Recarga el catálogo e intenta de nuevo.",
        ErrorCode.Unknown => "Error inesperado al registrar la venta.",
        _ => message ?? "Error desconocido."
    };
}
