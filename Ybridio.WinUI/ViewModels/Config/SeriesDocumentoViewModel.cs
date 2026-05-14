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
using Ybridio.Domain.Catalogos;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la gestión de Series Documentales en Configuración Global → Catálogos.
/// Permite configurar los prefijos, longitudes y consecutivos de folios por tipo de documento.
/// </summary>
public sealed partial class SeriesDocumentoViewModel : BaseContextViewModel
{
    private readonly ISerieDocumentoService _service;

    public ObservableCollection<SerieDocumentoDto> Series { get; } = [];

    [ObservableProperty] private SerieDocumentoDto? serieSeleccionada;
    [ObservableProperty] private string busqueda       = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private IReadOnlyList<SerieDocumentoDto> _todas = Array.Empty<SerieDocumentoDto>();

    /// <summary>Callback para diálogo nuevo/editar (requiere XamlRoot).</summary>
    public Action<SerieDocumentoDto?>? SolicitarNuevoEditar;

    /// <summary>Nombres legibles de los tipos de documento para el ComboBox del diálogo.</summary>
    public static IReadOnlyList<(TipoDocumentoSerie Tipo, string Nombre)> TiposDocumento { get; } =
    [
        (TipoDocumentoSerie.Cotizacion,       "Cotización"),
        (TipoDocumentoSerie.Pedido,           "Pedido"),
        (TipoDocumentoSerie.Venta,            "Venta"),
        (TipoDocumentoSerie.OrdenTrabajo,     "Orden de Trabajo"),
        (TipoDocumentoSerie.EntradaAlmacen,   "Entrada de Almacén"),
        (TipoDocumentoSerie.SalidaAlmacen,    "Salida de Almacén"),
        (TipoDocumentoSerie.OrdenCompra,      "Orden de Compra"),
        (TipoDocumentoSerie.ConteoInventario, "Conteo de Inventario"),
        (TipoDocumentoSerie.Traspaso,         "Traspaso"),
        (TipoDocumentoSerie.AjusteInventario, "Ajuste de Inventario"),
    ];

    public SeriesDocumentoViewModel(ISerieDocumentoService service, SessionService session)
        : base(session)
        => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy        = true;
        ErrorMessage  = string.Empty;
        try
        {
            _todas = await _service.ListarTodasAsync(ct);
            AplicarFiltro();
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(SerieSeleccionada);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (SerieSeleccionada is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(SerieSeleccionada.Id, ct);
            if (result.Success) { SuccessMessage = "Serie eliminada."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarSerieAsync(
        SerieDocumentoDto? dto,
        TipoDocumentoSerie tipo,
        string prefijo,
        int longitud,
        long siguienteNumero,
        bool reinicioAnual,
        bool activo,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var guardar = new GuardarSerieDocumentoDto(
                dto?.Id ?? 0, null /* SucursalId — global en V1 */, tipo,
                prefijo, longitud, siguienteNumero, reinicioAnual, activo);

            var result = await _service.GuardarAsync(guardar, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Serie creada." : "Serie actualizada.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => SerieSeleccionada is not null;

    partial void OnSerieSeleccionadaChanged(SerieDocumentoDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Series.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todas
            : _todas.Where(s =>
                s.Prefijo.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                s.TipoDocumentoNombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var s in lista) Series.Add(s);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
