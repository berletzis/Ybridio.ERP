using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.Services.Empresa;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la pantalla de Empresa en Configuración Global.
/// Implementa el Singleton Operational Surface Pattern:
/// grid institucional con un único registro + surface de edición lateral.
/// </summary>
public sealed partial class EmpresaViewModel : BaseContextViewModel
{
    private readonly IEmpresaService _service;

    // ── Grid display (un único registro) ─────────────────────────────────────
    public ObservableCollection<EmpresaDto> Empresas { get; } = [];

    // ── Edit form fields ──────────────────────────────────────────────────────
    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private string rfc    = string.Empty;

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   isEditing;
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    /// <summary>Inverso de IsEditing — para binding de IsReadOnly en TextBoxes.</summary>
    public bool IsNotEditing => !IsEditing;

    // ── Cancel snapshot ───────────────────────────────────────────────────────
    private string _nombreSnapshot = string.Empty;
    private string _rfcSnapshot    = string.Empty;

    public EmpresaViewModel(IEmpresaService service, SessionService session) : base(session)
        => _service = service;

    // ── Comandos ──────────────────────────────────────────────────────────────

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
                var dto = result.Value;
                Nombre = dto.Nombre;
                Rfc    = dto.RFC ?? string.Empty;

                Empresas.Clear();
                Empresas.Add(dto);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo cargar la empresa.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditar))]
    private void Editar()
    {
        _nombreSnapshot = Nombre;
        _rfcSnapshot    = Rfc;
        IsEditing       = true;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
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

            if (result.Success)
            {
                SuccessMessage = "Empresa actualizada correctamente.";
                IsEditing      = false;

                if (result.Value is not null)
                {
                    Empresas.Clear();
                    Empresas.Add(result.Value);
                }
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo guardar.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(PuedeCancelar))]
    private void Cancelar()
    {
        Nombre    = _nombreSnapshot;
        Rfc       = _rfcSnapshot;
        IsEditing = false;
        ErrorMessage = string.Empty;
    }

    // ── CanExecute guards ─────────────────────────────────────────────────────

    private bool PuedeEditar()   => !IsEditing;
    private bool PuedeGuardar()  =>  IsEditing;
    private bool PuedeCancelar() =>  IsEditing;

    // ── Reactive hooks ────────────────────────────────────────────────────────

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotEditing));
        EditarCommand.NotifyCanExecuteChanged();
        GuardarCommand.NotifyCanExecuteChanged();
        CancelarCommand.NotifyCanExecuteChanged();
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
