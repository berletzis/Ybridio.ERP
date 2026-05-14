using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Catalogos;
using Ybridio.Application.Services.Configuracion;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la sección Otros Cargos del módulo Configuración Global.
/// Cargos accesorios documentales (Flete, Maniobras, Seguro, etc.) — NO son productos inventariables.
/// </summary>
public sealed partial class OtrosCargosViewModel : BaseContextViewModel
{
    private readonly IOtroCargoService    _cargoService;
    private readonly ITipoImpuestoService _impuestoService;

    public ObservableCollection<OtroCargoDto>    Cargos        { get; } = [];
    public ObservableCollection<TipoImpuestoDto> TiposImpuesto { get; } = [];

    [ObservableProperty] private OtroCargoDto? cargoSeleccionado;
    [ObservableProperty] private string busqueda       = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    public static IReadOnlyList<string> TiposCargo { get; } =
        ["Logística", "Financiero", "Seguro", "Operativo", "Otro"];

    private IReadOnlyList<OtroCargoDto> _todos = Array.Empty<OtroCargoDto>();

    /// <summary>Callback asignado por la Page para abrir diálogo nuevo/editar.</summary>
    public Action<OtroCargoDto?>? SolicitarNuevoEditar;

    public OtrosCargosViewModel(IOtroCargoService cargoService,
                                 ITipoImpuestoService impuestoService,
                                 SessionService session)
        : base(session)
    {
        _cargoService    = cargoService;
        _impuestoService = impuestoService;
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy        = true;
        ErrorMessage  = string.Empty;
        try
        {
            _todos = await _cargoService.ListarTodosAsync(ct);
            AplicarFiltro();

            var impuestos = await _impuestoService.ListarAsync(ct);
            TiposImpuesto.Clear();
            foreach (var i in impuestos) TiposImpuesto.Add(i);
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(CargoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (CargoSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _cargoService.EliminarAsync(CargoSeleccionado.Id, ct);
            if (result.Success) { SuccessMessage = "Cargo eliminado."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarCargoAsync(
        OtroCargoDto? dto,
        string codigo, string nombre, string tipoCargo,
        bool aplicaIva, int? tipoImpuestoId, int ordenVisual, bool activo,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var guardar = new GuardarOtroCargoDto(
                dto?.Id ?? 0, codigo, nombre, tipoCargo, aplicaIva, tipoImpuestoId, ordenVisual, activo);
            var result = await _cargoService.GuardarAsync(guardar, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Cargo creado." : "Cargo actualizado.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => CargoSeleccionado is not null;

    partial void OnCargoSeleccionadoChanged(OtroCargoDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Cargos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(c =>
                c.Codigo.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.TipoCargo.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) Cargos.Add(c);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
