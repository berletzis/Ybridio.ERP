using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

public sealed partial class RolesViewModel : BaseContextViewModel
{
    private readonly IRolService _service;

    public ObservableCollection<RolDto> Roles { get; } = [];

    [ObservableProperty] private RolDto? rolSeleccionado;
    [ObservableProperty] private bool    isBusy;
    [ObservableProperty] private string  errorMessage   = string.Empty;
    [ObservableProperty] private string  successMessage = string.Empty;

    public RolesViewModel(IRolService service, SessionService session) : base(session)
        => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var lista = await _service.ListarAsync(ct);
            Roles.Clear();
            foreach (var r in lista) Roles.Add(r);
        }
        finally { IsBusy = false; }
    }

    public IRolService Service => _service;

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (RolSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(RolSeleccionado.Id, ct);
            if (result.Success)
            {
                Roles.Remove(RolSeleccionado);
                RolSeleccionado = null;
                SuccessMessage  = "Rol eliminado.";
            }
            else { ErrorMessage = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => RolSeleccionado is not null;

    partial void OnRolSeleccionadoChanged(RolDto? value)
        => EliminarCommand.NotifyCanExecuteChanged();

    // Roles son globales (no cambian con tienda)
    protected override Task OnContextChangedAsync() => Task.CompletedTask;
}
