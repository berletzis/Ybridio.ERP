using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Catalogos;
using Ybridio.Domain.Catalogos;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la sección Impuestos del módulo Configuración Global.
/// Gestiona el catálogo de tipos de impuesto. Reemplaza el hardcode de FiscalConstants.
/// </summary>
public sealed partial class ImpuestosViewModel : BaseContextViewModel
{
    private readonly ITipoImpuestoService _impuestoService;

    public ObservableCollection<TipoImpuestoDto> Impuestos { get; } = [];

    [ObservableProperty] private TipoImpuestoDto? impuestoSeleccionado;
    [ObservableProperty] private string busqueda       = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private System.Collections.Generic.IReadOnlyList<TipoImpuestoDto> _todos
        = Array.Empty<TipoImpuestoDto>();

    /// <summary>Callback asignado por la Page para abrir diálogo nuevo/editar.</summary>
    public Action<TipoImpuestoDto?>? SolicitarNuevoEditar;

    public ImpuestosViewModel(ITipoImpuestoService impuestoService, SessionService session)
        : base(session)
        => _impuestoService = impuestoService;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy        = true;
        ErrorMessage  = string.Empty;
        try
        {
            _todos = await _impuestoService.ListarTodosAsync(ct);
            AplicarFiltro();
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(ImpuestoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (ImpuestoSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _impuestoService.EliminarAsync(ImpuestoSeleccionado.Id, ct);
            if (result.Success) { SuccessMessage = "Impuesto eliminado."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarImpuestoAsync(
        TipoImpuestoDto? dto,
        string nombre,
        decimal porcentaje,
        bool activo,
        string codigo = "",
        TipoGravamen gravamen = TipoGravamen.IVA,
        int ordenVisual = 0,
        string? descripcion = null,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var upsert = new UpsertTipoImpuestoDto(nombre, porcentaje, activo, codigo, gravamen, ordenVisual, descripcion);
            var result  = await _impuestoService.GuardarAsync(dto?.Id ?? 0, upsert, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Impuesto creado." : "Impuesto actualizado.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => ImpuestoSeleccionado is not null;

    partial void OnImpuestoSeleccionadoChanged(TipoImpuestoDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Impuestos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(i => i.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var i in lista) Impuestos.Add(i);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
