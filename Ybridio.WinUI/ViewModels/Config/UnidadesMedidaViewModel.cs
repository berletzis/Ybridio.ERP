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

/// <summary>ViewModel para el catálogo de Unidades de Medida en Configuración Global → Catálogos.</summary>
public sealed partial class UnidadesMedidaViewModel : BaseContextViewModel
{
    private readonly IUnidadMedidaService _service;

    public ObservableCollection<UnidadMedidaDto> Unidades { get; } = [];

    [ObservableProperty] private UnidadMedidaDto? unidadSeleccionada;
    [ObservableProperty] private string busqueda       = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private System.Collections.Generic.IReadOnlyList<UnidadMedidaDto> _todos
        = Array.Empty<UnidadMedidaDto>();

    /// <summary>Callback asignado por la Page para abrir diálogo nuevo/editar.</summary>
    public Action<UnidadMedidaDto?>? SolicitarNuevoEditar;

    public UnidadesMedidaViewModel(IUnidadMedidaService service, SessionService session)
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
    private void Editar() => SolicitarNuevoEditar?.Invoke(UnidadSeleccionada);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (UnidadSeleccionada is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(UnidadSeleccionada.Id, ct);
            if (result.Success) { SuccessMessage = "Unidad eliminada."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarUnidadAsync(
        UnidadMedidaDto? dto, string nombre, string abreviatura, bool activo,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var upsert = new UpsertUnidadMedidaDto(nombre, abreviatura, activo);
            var result  = await _service.GuardarAsync(dto?.Id ?? 0, upsert, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Unidad creada." : "Unidad actualizada.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => UnidadSeleccionada is not null;

    partial void OnUnidadSeleccionadaChanged(UnidadMedidaDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Unidades.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(u =>
                u.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                u.Abreviatura.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var u in lista) Unidades.Add(u);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
