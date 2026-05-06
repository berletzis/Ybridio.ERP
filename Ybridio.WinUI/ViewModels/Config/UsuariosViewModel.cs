using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

public sealed partial class UsuariosViewModel : BaseContextViewModel
{
    private readonly IUsuarioService _service;

    public ObservableCollection<UsuarioDto> Usuarios { get; } = [];

    [ObservableProperty] private UsuarioDto? usuarioSeleccionado;
    [ObservableProperty] private string      busqueda       = string.Empty;
    [ObservableProperty] private bool        isBusy;
    [ObservableProperty] private string      errorMessage   = string.Empty;
    [ObservableProperty] private string      successMessage = string.Empty;

    private IReadOnlyList<UsuarioDto> _todos = [];

    // Callback para abrir la ventana de detalle (asignado por la Page)
    public Action<UsuarioDto?>? SolicitarAbrirDetalle;

    public UsuariosViewModel(IUsuarioService service, SessionService session) : base(session)
        => _service = service;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            _todos = await _service.ListarPorEmpresaAsync(Session.EmpresaId, ct);
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarAbrirDetalle?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarAbrirDetalle?.Invoke(UsuarioSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task CambiarActivoAsync(CancellationToken ct = default)
    {
        if (UsuarioSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var nuevo  = !UsuarioSeleccionado.Activo;
            var result = await _service.CambiarActivoAsync(UsuarioSeleccionado.Id, nuevo, Session.Usuario.Id, ct);
            if (result.Success) { SuccessMessage = nuevo ? "Activado." : "Desactivado."; await LoadAsync(ct); }
            else                { ErrorMessage = result.Error ?? "No se pudo cambiar el estado."; }
        }
        finally { IsBusy = false; }
    }

    public IUsuarioService Service => _service;

    private bool HaySeleccion() => UsuarioSeleccionado is not null;

    partial void OnUsuarioSeleccionadoChanged(UsuarioDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        CambiarActivoCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Usuarios.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(u =>
                u.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (u.Email?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var u in lista) Usuarios.Add(u);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}
