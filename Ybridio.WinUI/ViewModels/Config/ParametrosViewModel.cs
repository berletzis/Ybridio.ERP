using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Configuracion;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la sección Parámetros del módulo Configuración Global.
/// Gestiona el catálogo de parámetros de configuración operacional de la empresa.
/// </summary>
public sealed partial class ParametrosViewModel : BaseContextViewModel
{
    private readonly IParametroGlobalService _parametroService;

    public ObservableCollection<ParametroGlobalDto> Parametros { get; } = [];

    [ObservableProperty] private ParametroGlobalDto? parametroSeleccionado;
    [ObservableProperty] private string busqueda      = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage  = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private System.Collections.Generic.IReadOnlyList<ParametroGlobalDto> _todos
        = Array.Empty<ParametroGlobalDto>();

    /// <summary>Callback asignado por la Page para abrir el diálogo de nuevo/editar (requiere XamlRoot).</summary>
    public Action<ParametroGlobalDto?>? SolicitarNuevoEditar;

    public ParametrosViewModel(IParametroGlobalService parametroService, SessionService session)
        : base(session)
        => _parametroService = parametroService;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy        = true;
        ErrorMessage  = string.Empty;
        try
        {
            _todos = await _parametroService.ListarAsync(ct);
            AplicarFiltro();
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar parámetros: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(ParametroSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (ParametroSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _parametroService.EliminarAsync(ParametroSeleccionado.Id, ct);
            if (result.Success) { SuccessMessage = "Parámetro eliminado."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    /// <summary>Guarda un parámetro nuevo o actualizado desde el diálogo de edición.</summary>
    public async Task<bool> GuardarParametroAsync(
        ParametroGlobalDto? dto,
        string clave, string valor, string? descripcion,
        string tipoDato, string grupo, int ordenVisual, bool activo,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var guardar = new GuardarParametroGlobalDto(
                dto?.Id ?? 0, clave, valor, descripcion, tipoDato, grupo, ordenVisual, activo);
            var result = await _parametroService.GuardarAsync(guardar, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "Error al guardar."; return false; }
            SuccessMessage = dto is null ? "Parámetro creado." : "Parámetro actualizado.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => ParametroSeleccionado is not null;

    partial void OnParametroSeleccionadoChanged(ParametroGlobalDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Parametros.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(p =>
                p.Clave.Contains(t, StringComparison.OrdinalIgnoreCase)    ||
                p.Valor.Contains(t, StringComparison.OrdinalIgnoreCase)    ||
                p.Grupo.Contains(t, StringComparison.OrdinalIgnoreCase)    ||
                (p.Descripcion?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var p in lista) Parametros.Add(p);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
