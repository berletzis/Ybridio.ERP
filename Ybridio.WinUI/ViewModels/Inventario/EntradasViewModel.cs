using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.ViewModels.Inventario;

/// <summary>
/// ViewModel del sub-módulo Inventario > Entradas.
/// Gestiona la CommandBar y el estado de la lista de movimientos de entrada.
/// Se suscribe a SucursalChanged para refrescar datos al cambiar de sucursal.
/// </summary>
public sealed partial class EntradasViewModel : BaseContextViewModel
{
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool    isBusy;
    [ObservableProperty] private string  mensajeEstado  = string.Empty;
    [ObservableProperty] private string  errorMessage   = string.Empty;
    [ObservableProperty] private string  successMessage = string.Empty;
    [ObservableProperty] private string  busqueda       = string.Empty;

    [ObservableProperty]
    private string filtroTemporal = "30 días";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private object? entradaSeleccionada;

    public ObservableCollection<object> Entradas { get; } = [];

    public Visibility IsEmptyState =>
        Entradas.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public EntradasViewModel(
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _observability  = observability;
        _contextTracker = contextTracker;
        Entradas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => EntradaSeleccionada is not null;

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    private void AplicarFiltro() => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() { }
    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void Editar() { }
    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default) => await Task.CompletedTask;
    [RelayCommand]
    private async Task ImportarAsync(CancellationToken ct = default) => await Task.CompletedTask;
    [RelayCommand]
    private async Task ExportarAsync(CancellationToken ct = default) => await Task.CompletedTask;

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var op = new GridOperationContext(
            Module:           "Inventario",
            SubModule:        "Entradas",
            ViewModel:        nameof(EntradasViewModel),
            Entity:           "inventario.Entrada",
            RecordCount:      Entradas.Count,
            Duration:         TimeSpan.Zero,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected,
                                  Note: "AlmacenId en Entrada es campo de documento, no filtro de consulta"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   FiltroTemporal,
            Notes:            ["Datos pendientes — IEntradaService no implementado en Application layer"],
            Timestamp:        DateTime.Now
        );
        _observability.Report(op);

        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module:           "Inventario",
            SubModule:        "Entradas",
            ViewModel:        nameof(EntradasViewModel),
            Entity:           "inventario.Entrada",
            RecordCount:      Entradas.Count,
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "Campo de documento — no es filtro"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source:           "ModuleFrame",
            UpdatedAt:        DateTime.Now
        ));
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    /// <summary>Reporta el contexto vivo sin query. Llamado cuando el tab se activa ya cargado.</summary>
    public void ReportLiveContext()
    {
        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module:           "Inventario",
            SubModule:        "Entradas",
            ViewModel:        nameof(EntradasViewModel),
            Entity:           "inventario.Entrada",
            RecordCount:      Entradas.Count,
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "Campo de documento — no es filtro"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source:           "ModuleFrame",
            UpdatedAt:        DateTime.Now
        ));
    }
}
