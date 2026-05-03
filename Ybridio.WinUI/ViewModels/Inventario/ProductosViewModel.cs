using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Producto;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Inventario;

public sealed partial class ProductosViewModel : ObservableObject
{
    private readonly IProductoService _productos;
    private readonly SessionService _session;

    // ── Colecciones ──────────────────────────────────────────────────────────

    public ObservableCollection<ProductoDto> Productos { get; } = [];
    public ObservableCollection<ProductoDto> ProductosSeleccionados { get; } = [];
    public ObservableCollection<UnidadMedidaDto> UnidadesMedida { get; } = [];
    public ObservableCollection<CategoriaProductoDto> Categorias { get; } = [];
    public ObservableCollection<TipoProductoDto> TiposProducto { get; } = [];
    public ObservableCollection<TipoImpuestoDto> TiposImpuesto { get; } = [];

    public int EmpresaId => _session.EmpresaId;

    /// <summary>
    /// Usado por ProductosPage para mostrar el estado vacío con ErpGridEmptyTemplate.
    /// Se actualiza cuando cambia IsLoading o el contenido de Productos.
    /// </summary>
    public Visibility IsEmptyState =>
        Productos.Count == 0 && !IsLoading ? Visibility.Visible : Visibility.Collapsed;

    // ── Estado ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string successMessage = string.Empty;

    [ObservableProperty]
    private string busqueda = string.Empty;

    [ObservableProperty]
    private bool soloActivos;

    [ObservableProperty]
    private ProductoDto? productoSeleccionado;

    private IReadOnlyList<ProductoDto> _todosLosProductos = [];

    // ── Eventos ──────────────────────────────────────────────────────────────

    public Action<ProductoDto?>? SolicitarAbrirDetalle;
    public Action<(ProductoDto A, ProductoDto B)>? SolicitarComparar;

    public ProductosViewModel(IProductoService productos, SessionService session)
    {
        _productos = productos;
        _session = session;
    }

    // ── Carga ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_session.EmpresaId == 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            await CargarCatalogosAsync(ct);
            _todosLosProductos = await _productos.ListarPorEmpresaAsync(_session.EmpresaId, SoloActivos, ct);
            AplicarFiltro();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar productos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CargarCatalogosAsync(CancellationToken ct)
    {
        var unidades = await _productos.ListarUnidadesMedidaAsync(_session.EmpresaId, ct);
        var categorias = await _productos.ListarCategoriasAsync(_session.EmpresaId, ct);
        var tipos = await _productos.ListarTiposProductoAsync(_session.EmpresaId, ct);
        var impuestos = await _productos.ListarTiposImpuestoAsync(_session.EmpresaId, ct);

        UnidadesMedida.Clear();
        foreach (var u in unidades) UnidadesMedida.Add(u);

        Categorias.Clear();
        foreach (var c in categorias) Categorias.Add(c);

        TiposProducto.Clear();
        foreach (var t in tipos) TiposProducto.Add(t);

        TiposImpuesto.Clear();
        foreach (var i in impuestos) TiposImpuesto.Add(i);
    }

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (_session.EmpresaId == 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _todosLosProductos = await _productos.ListarPorEmpresaAsync(_session.EmpresaId, SoloActivos, ct);
            AplicarFiltro();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al refrescar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Nuevo() => SolicitarAbrirDetalle?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarAbrirDetalle?.Invoke(ProductoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task ClonarAsync(CancellationToken ct = default)
    {
        if (ProductoSeleccionado is null || _session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var dto = new ClonarProductoDto(
                ProductoOrigenId: ProductoSeleccionado.Id,
                NuevoCodigo: $"{ProductoSeleccionado.Codigo}-COPIA",
                NuevoNombre: $"{ProductoSeleccionado.Nombre} (copia)");

            var result = await _productos.ClonarAsync(dto, _session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = $"Clonado como '{result.Value!.Nombre}'.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo clonar.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task CambiarActivoAsync(CancellationToken ct = default)
    {
        if (ProductoSeleccionado is null || _session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var nuevoEstado = !ProductoSeleccionado.Activo;
            var result = await _productos.CambiarActivoAsync(ProductoSeleccionado.Id, nuevoEstado, _session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = nuevoEstado ? "Producto activado." : "Producto desactivado.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo cambiar el estado.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (ProductoSeleccionado is null || _session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.EliminarAsync(ProductoSeleccionado.Id, _session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = "Producto eliminado.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo eliminar.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HayDosSeleccionados))]
    private void Comparar()
    {
        if (ProductosSeleccionados.Count < 2) return;
        SolicitarComparar?.Invoke((ProductosSeleccionados[0], ProductosSeleccionados[1]));
    }

    // ── Métodos públicos para ProductoDetailWindow ───────────────────────────

    public async Task CrearDesdeVentanaAsync(CrearProductoDto dto, CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.CrearAsync(dto, _session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = $"Producto '{result.Value!.Nombre}' creado.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo crear el producto.";
            }
        }
        finally { IsBusy = false; }
    }

    public async Task ActualizarDesdeVentanaAsync(int productoId, ActualizarProductoDto dto, CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.ActualizarAsync(productoId, dto, _session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = $"Producto '{result.Value!.Nombre}' actualizado.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo actualizar.";
            }
        }
        finally { IsBusy = false; }
    }

    // ── CanExecute ───────────────────────────────────────────────────────────

    private bool HaySeleccion() => ProductoSeleccionado is not null;
    private bool HayDosSeleccionados() => ProductosSeleccionados.Count >= 2;

    partial void OnProductoSeleccionadoChanged(ProductoDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        ClonarCommand.NotifyCanExecuteChanged();
        CambiarActivoCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();
    partial void OnSoloActivosChanged(bool value) => _ = RefrescarAsync();

    // Notifica IsEmptyState cada vez que IsLoading cambia
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmptyState));

    private void AplicarFiltro()
    {
        Productos.Clear();
        var termino = Busqueda.Trim();

        var lista = string.IsNullOrWhiteSpace(termino)
            ? _todosLosProductos
            : _todosLosProductos.Where(p =>
                p.Codigo.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                (p.CodigoBarras?.Contains(termino, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.Nombre.Contains(termino, StringComparison.OrdinalIgnoreCase));

        foreach (var p in lista)
            Productos.Add(p);

        OnPropertyChanged(nameof(IsEmptyState));
    }
}
