using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.Services.Empresa;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

public sealed partial class EmpresaViewModel : BaseContextViewModel
{
    private readonly IEmpresaService _service;

    [ObservableProperty] private string nombre      = string.Empty;
    [ObservableProperty] private string rfc         = string.Empty;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    public EmpresaViewModel(IEmpresaService service, SessionService session) : base(session)
        => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.ObtenerPorIdAsync(Session.EmpresaId, ct);
            if (result.Success && result.Value is not null)
            {
                Nombre = result.Value.Nombre;
                Rfc    = result.Value.RFC ?? string.Empty;
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo cargar la empresa.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GuardarAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Nombre)) { ErrorMessage = "El nombre es obligatorio."; return; }
        if (Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto    = new UpsertEmpresaDto(Nombre.Trim(), string.IsNullOrWhiteSpace(Rfc) ? null : Rfc.Trim());
            var result = await _service.ActualizarAsync(Session.EmpresaId, dto, Session.Usuario.Id, ct);

            if (result.Success) SuccessMessage = "Empresa actualizada correctamente.";
            else                ErrorMessage   = result.Error ?? "No se pudo guardar.";
        }
        finally { IsBusy = false; }
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
