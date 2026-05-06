using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.Services.Sucursal;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

public sealed partial class SucursalesConfigViewModel : BaseContextViewModel
{
    private readonly ISucursalService _service;

    public ObservableCollection<SucursalDto> Sucursales { get; } = [];

    [ObservableProperty] private SucursalDto? sucursalSeleccionada;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    public SucursalesConfigViewModel(ISucursalService service, SessionService session) : base(session)
        => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var lista = await _service.ListarPorEmpresaAsync(Session.EmpresaId, ct);
            Sucursales.Clear();
            foreach (var t in lista) Sucursales.Add(t);
        }
        finally { IsBusy = false; }
    }

    public async Task<ServiceResult<SucursalDto>> CrearAsync(string nombre, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return ServiceResult<SucursalDto>.Fail("Sin sesión.", ErrorCode.Unauthorized);
        var result = await _service.CrearAsync(Session.EmpresaId, nombre, Session.Usuario.Id, ct);
        if (result.Success && result.Value is not null)
        {
            Sucursales.Add(result.Value);
            SuccessMessage = $"Sucursal '{result.Value.Nombre}' creada.";
        }
        else { ErrorMessage = result.Error ?? "No se pudo crear."; }
        return result;
    }

    public async Task<ServiceResult<SucursalDto>> ActualizarAsync(int sucursalId, string nombre, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return ServiceResult<SucursalDto>.Fail("Sin sesión.", ErrorCode.Unauthorized);
        var result = await _service.ActualizarAsync(sucursalId, nombre, Session.Usuario.Id, ct);
        if (result.Success && result.Value is not null)
        {
            var idx = Sucursales.IndexOf(Sucursales.FirstOrDefault(t => t.Id == sucursalId)!);
            if (idx >= 0) Sucursales[idx] = result.Value;
            SuccessMessage = "Sucursal actualizada.";
        }
        else { ErrorMessage = result.Error ?? "No se pudo actualizar."; }
        return result;
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (SucursalSeleccionada is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(SucursalSeleccionada.Id, Session.Usuario.Id, ct);
            if (result.Success)
            {
                Sucursales.Remove(SucursalSeleccionada);
                SucursalSeleccionada = null;
                SuccessMessage = "Sucursal eliminada.";
            }
            else { ErrorMessage = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => SucursalSeleccionada is not null;

    partial void OnSucursalSeleccionadaChanged(SucursalDto? value)
        => EliminarCommand.NotifyCanExecuteChanged();

    protected override Task OnContextChangedAsync() => LoadAsync();
}
