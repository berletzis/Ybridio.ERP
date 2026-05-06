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
using Ybridio.WinUI.Controls.Navigation;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Inventario;

public sealed partial class ProductosViewModel : BaseContextViewModel
{
    private readonly IProductoService _productos;

    // ── Clasificación (panel tipo Outlook) ───────────────────────────────────

    public ObservableCollection<ClassificationItem> ClasificacionItems { get; } = [];

    // Árbol completo sin filtrar — fuente de verdad para AplicarFiltroClasificacion
    private IReadOnlyList<ClassificationItem> _categoriasBase = [];

    // IDs de la categoría seleccionada Y todos sus descendientes.
    // Empty = TODOS (sin filtro). Permite que seleccionar un padre filtre sus hijos también.
    private HashSet<int> _categoriaFiltroIds = [];

    // Nombre de la categoría activa para mostrar en el chip (null = sin filtro)
    [ObservableProperty]
    private string? filtroActivoNombre;

    partial void OnFiltroActivoNombreChanged(string? value) =>
        OnPropertyChanged(nameof(FiltroActivoVisibility));

    public Visibility FiltroActivoVisibility =>
        string.IsNullOrEmpty(FiltroActivoNombre) ? Visibility.Collapsed : Visibility.Visible;

    // Toggle "Solo categorías con productos" — oculta nodos con Count == 0
    [ObservableProperty]
    private bool mostrarSoloCategorias;

    partial void OnMostrarSoloCategoriasChanged(bool value) => AplicarFiltroClasificacion();

    /// <summary>
    /// Callback invocado cuando el filtro de categoría se limpia (toggle o botón ✕).
    /// Permite que la Page sincronice el panel sin acoplar ViewModel a la Vista.
    /// </summary>
    public Action? FiltroLimpiadoCallback;

    /// <summary>
    /// Llamado por ProductosPage al seleccionar un nodo en ClassificationPanel.
    /// El panel no filtra — solo notifica; el ViewModel decide qué hacer.
    /// </summary>
    public void FiltrarPorClasificacion(ClassificationItem? item)
    {
        if (item is null || item.IsRoot)
        {
            _categoriaFiltroIds = [];
            FiltroActivoNombre  = null;
        }
        else
        {
            // Incluir el nodo seleccionado y TODOS sus descendientes para que
            // seleccionar "Deportes" muestre también productos de "Running", "Fútbol", etc.
            _categoriaFiltroIds = GetAllCategoryIds(item);
            FiltroActivoNombre  = item.Name;
        }
        AplicarFiltro();
    }

    /// <summary>Limpia el filtro de categoría y notifica a la Page para deseleccionar el panel.</summary>
    public void LimpiarFiltro()
    {
        _categoriaFiltroIds = [];
        FiltroActivoNombre  = null;
        FiltroLimpiadoCallback?.Invoke();
        AplicarFiltro();
    }

    /// <summary>
    /// Devuelve el CategoriaId del nodo dado más los de todos sus descendientes.
    /// Permite filtrado jerárquico: padre incluye productos de todos los hijos.
    /// </summary>
    private static HashSet<int> GetAllCategoryIds(ClassificationItem item)
    {
        var ids = new HashSet<int>();
        if (item.CategoriaId.HasValue) ids.Add(item.CategoriaId.Value);
        foreach (var child in item.Children)
            ids.UnionWith(GetAllCategoryIds(child));
        return ids;
    }

    private async Task CargarClasificacionAsync(CancellationToken ct)
    {
        var categorias = await _productos.ListarCategoriasConConteoAsync(Session.EmpresaId, ct);

        // 1. Construir árbol jerárquico
        var arbol = BuildCategoryTree(categorias, null).ToList();

        // 2. Acumular conteos en post-order: cada padre suma los conteos de sus hijos
        //    → "Deportes (0 directos) + Running (5)" = "Deportes (5)"
        foreach (var raiz in arbol)
            AccumulateChildCounts(raiz);

        _categoriasBase = arbol;
        AplicarFiltroClasificacion();
    }

    /// <summary>
    /// Reconstruye ClasificacionItems aplicando el toggle MostrarSoloCategorias.
    /// Limpia el filtro activo para mantener coherencia con el árbol visible.
    /// </summary>
    private void AplicarFiltroClasificacion()
    {
        // Al reconstruir el árbol, limpiar el filtro para evitar inconsistencias
        _categoriaFiltroIds = [];
        FiltroActivoNombre = null;
        FiltroLimpiadoCallback?.Invoke();

        ClasificacionItems.Clear();

        ClasificacionItems.Add(new ClassificationItem
        {
            Id         = "todos",
            Name       = "TODOS",
            Count      = _todosLosProductos.Count,
            IsRoot     = true,
            IsExpanded = true
        });

        foreach (var item in _categoriasBase)
        {
            var nodo = MostrarSoloCategorias ? FiltrarSinProductos(item) : item;
            if (nodo is not null) ClasificacionItems.Add(nodo);
        }

        AplicarFiltro();
    }

    /// <summary>
    /// Post-order: acumula el conteo de todos los descendientes en cada nodo padre.
    /// Modifica el árbol in-place; sólo se llama una vez tras construirlo desde BD.
    /// </summary>
    private static int AccumulateChildCounts(ClassificationItem item)
    {
        if (item.Children.Count == 0) return item.Count;
        int sumHijos = item.Children.Sum(AccumulateChildCounts);
        item.Count += sumHijos;
        return item.Count;
    }

    /// <summary>
    /// Devuelve una copia del nodo excluyendo ramas donde Count == 0.
    /// Tras AccumulateChildCounts, Count == 0 garantiza que no hay productos en toda la rama.
    /// </summary>
    private static ClassificationItem? FiltrarSinProductos(ClassificationItem item)
    {
        if (item.Count == 0) return null;

        var hijosFiltrados = item.Children
            .Select(FiltrarSinProductos)
            .Where(h => h is not null)
            .Cast<ClassificationItem>()
            .ToList();

        return new ClassificationItem
        {
            Id          = item.Id,
            Name        = item.Name,
            Count       = item.Count,
            CategoriaId = item.CategoriaId,
            IsRoot      = item.IsRoot,
            IsExpanded  = item.IsExpanded,
            Children    = new ObservableCollection<ClassificationItem>(hijosFiltrados)
        };
    }

    /// <summary>
    /// Construye recursivamente el árbol de ClassificationItem a partir de la lista plana.
    /// </summary>
    private static IEnumerable<ClassificationItem> BuildCategoryTree(
        IReadOnlyList<CategoriaConConteoDto> all,
        int? parentId)
        => all
            .Where(c => c.CategoriaPadreId == parentId)
            .Select(c => new ClassificationItem
            {
                Id          = $"cat_{c.Id}",
                Name        = c.Nombre,
                Count       = c.TotalProductos,  // conteo directo; AccumulateChildCounts lo acumula
                CategoriaId = c.Id,
                IsExpanded  = true,
                Children    = new ObservableCollection<ClassificationItem>(
                                  BuildCategoryTree(all, c.Id))
            });

    // ── Colecciones ──────────────────────────────────────────────────────────

    public ObservableCollection<ProductoDto> Productos { get; } = [];
    public ObservableCollection<ProductoDto> ProductosSeleccionados { get; } = [];
    public ObservableCollection<UnidadMedidaDto> UnidadesMedida { get; } = [];
    public ObservableCollection<CategoriaProductoDto> Categorias { get; } = [];
    public ObservableCollection<TipoProductoDto> TiposProducto { get; } = [];
    public ObservableCollection<TipoImpuestoDto> TiposImpuesto { get; } = [];

    public int EmpresaId => Session.EmpresaId;

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
        : base(session)
    {
        _productos = productos;
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    // ── Carga ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Session.EmpresaId == 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            await CargarCatalogosAsync(ct);
            _todosLosProductos = await _productos.ListarPorEmpresaAsync(Session.EmpresaId, SoloActivos, ct);
            await CargarClasificacionAsync(ct);
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
        var unidades = await _productos.ListarUnidadesMedidaAsync(Session.EmpresaId, ct);
        var categorias = await _productos.ListarCategoriasAsync(Session.EmpresaId, ct);
        var tipos = await _productos.ListarTiposProductoAsync(Session.EmpresaId, ct);
        var impuestos = await _productos.ListarTiposImpuestoAsync(Session.EmpresaId, ct);

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
        if (Session.EmpresaId == 0) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _todosLosProductos = await _productos.ListarPorEmpresaAsync(Session.EmpresaId, SoloActivos, ct);
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
        if (ProductoSeleccionado is null || Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var dto = new ClonarProductoDto(
                ProductoOrigenId: ProductoSeleccionado.Id,
                NuevoCodigo: $"{ProductoSeleccionado.Codigo}-COPIA",
                NuevoNombre: $"{ProductoSeleccionado.Nombre} (copia)");

            var result = await _productos.ClonarAsync(dto, Session.Usuario.Id, ct);
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
        if (ProductoSeleccionado is null || Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var nuevoEstado = !ProductoSeleccionado.Activo;
            var result = await _productos.CambiarActivoAsync(ProductoSeleccionado.Id, nuevoEstado, Session.Usuario.Id, ct);
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
        if (ProductoSeleccionado is null || Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.EliminarAsync(ProductoSeleccionado.Id, Session.Usuario.Id, ct);
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

    public async Task CrearDesdeVentanaAsync(
        CrearProductoDto dto, IReadOnlyList<int> categoriaIds, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.CrearAsync(dto, Session.Usuario.Id, ct);
            if (result.Success && result.Value is not null)
            {
                if (categoriaIds.Count > 0)
                {
                    var catResult = await _productos.ReemplazarCategoriasAsync(
                        result.Value.Id, categoriaIds, Session.Usuario.Id, ct);
                    if (!catResult.Success)
                    {
                        ErrorMessage = catResult.Error ?? "Error al guardar categorías.";
                        return;
                    }
                }
                SuccessMessage = $"Producto '{result.Value.Nombre}' creado.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo crear el producto.";
            }
        }
        finally { IsBusy = false; }
    }

    public async Task ActualizarDesdeVentanaAsync(
        int productoId, ActualizarProductoDto dto, IReadOnlyList<int> categoriaIds, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _productos.ActualizarAsync(productoId, dto, Session.Usuario.Id, ct);
            if (result.Success && result.Value is not null)
            {
                var catResult = await _productos.ReemplazarCategoriasAsync(
                    productoId, categoriaIds, Session.Usuario.Id, ct);
                if (!catResult.Success)
                {
                    ErrorMessage = catResult.Error ?? "Error al guardar categorías.";
                    return;
                }
                SuccessMessage = $"Producto '{result.Value.Nombre}' actualizado.";
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

        IEnumerable<ProductoDto> lista = _todosLosProductos;

        // Filtro de texto
        if (!string.IsNullOrWhiteSpace(termino))
            lista = lista.Where(p =>
                p.Codigo.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                (p.CodigoBarras?.Contains(termino, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.Nombre.Contains(termino, StringComparison.OrdinalIgnoreCase));

        // Filtro jerárquico N:N: el producto pertenece a la categoría seleccionada O a algún
        // descendiente de ella. _categoriaFiltroIds contiene el nodo seleccionado + todos sus hijos.
        if (_categoriaFiltroIds.Count > 0)
            lista = lista.Where(p => p.CategoriaIds.Any(id => _categoriaFiltroIds.Contains(id)));

        foreach (var p in lista)
            Productos.Add(p);

        OnPropertyChanged(nameof(IsEmptyState));
    }
}
