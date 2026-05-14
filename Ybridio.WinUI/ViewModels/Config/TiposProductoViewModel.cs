using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Catalogos;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>ViewModel para el catálogo de Tipos de Producto en Configuración Global → Catálogos.</summary>
public sealed partial class TiposProductoViewModel : BaseContextViewModel
{
    private readonly ITipoProductoService _service;

    public ObservableCollection<TipoProductoDto> TiposProducto { get; } = [];

    [ObservableProperty] private TipoProductoDto? tipoSeleccionado;
    [ObservableProperty] private string busqueda       = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private System.Collections.Generic.IReadOnlyList<TipoProductoDto> _todos
        = Array.Empty<TipoProductoDto>();

    /// <summary>Callback asignado por la Page para abrir diálogo nuevo/editar.</summary>
    public Action<TipoProductoDto?>? SolicitarNuevoEditar;

    public TiposProductoViewModel(ITipoProductoService service, SessionService session)
        : base(session) => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy        = true;
        ErrorMessage  = string.Empty;
        try
        {
            _todos = await _service.ListarTodosAsync(ct);
            AplicarFiltro();
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(TipoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (TipoSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(TipoSeleccionado.Id, ct);
            if (result.Success) { SuccessMessage = "Tipo eliminado."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarTipoAsync(
        TipoProductoDto? dto,
        string nombre,
        string? descripcion,
        bool activo,
        string clave = "",
        int ordenVisual = 0,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var upsert = new UpsertTipoProductoDto(nombre, descripcion, activo, clave, ordenVisual);
            var result  = await _service.GuardarAsync(dto?.Id ?? 0, upsert, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Tipo creado." : "Tipo actualizado.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => TipoSeleccionado is not null;

    partial void OnTipoSeleccionadoChanged(TipoProductoDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        TiposProducto.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(tp =>
                tp.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (tp.Descripcion?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var tp in lista) TiposProducto.Add(tp);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
