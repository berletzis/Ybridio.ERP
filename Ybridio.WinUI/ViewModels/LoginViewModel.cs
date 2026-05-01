using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Ybridio.Application.Common;
using Ybridio.Application.Services.Auth;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;

namespace Ybridio.WinUI.ViewModels;

/// <summary>
/// ViewModel de la pantalla de inicio de sesión.
/// Llama a <see cref="IAuthService"/> y establece <see cref="SessionService"/> si el login es exitoso.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly SessionService _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public LoginViewModel(IAuthService auth, SessionService session, INavigationService navigation)
    {
        _auth = auth;
        _session = session;
        _navigation = navigation;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        LoginCommand.NotifyCanExecuteChanged();

        try
        {
            var result = await _auth.LoginAsync(new(Email, Password), ct);

            if (!result.Succeeded)
            {
                ErrorMessage = MapError(result.Error, result.ErrorCode);
                return;
            }

            _session.SetUsuario(result.Value!);

            // Navegar al Shell; el Shell cargará el Dashboard
            _navigation.NavigateTo(typeof(ShellPage));
        }
        finally
        {
            IsBusy = false;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    private static string MapError(string? message, ErrorCode code) => code switch
    {
        ErrorCode.NotFound or ErrorCode.Unauthorized => "Usuario o contraseña incorrectos.",
        ErrorCode.Inactive => "El usuario está inactivo. Contacta al administrador.",
        ErrorCode.Unknown => "Ocurrió un error inesperado. Intenta de nuevo.",
        _ => message ?? "Error desconocido."
    };

    // Notificar cambios en Email/Password para habilitar el comando
    partial void OnEmailChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
}
