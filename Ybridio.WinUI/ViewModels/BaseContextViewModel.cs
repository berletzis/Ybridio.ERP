using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using Ybridio.WinUI.Services;

namespace Ybridio.WinUI.ViewModels;

/// <summary>
/// Base para ViewModels que muestran datos dependientes del contexto (empresa / sucursal).
/// Se suscribe a SessionService.SucursalChanged y llama OnContextChangedAsync() al cambiar.
///
/// Uso:
///   1. Heredar en lugar de ObservableObject.
///   2. Llamar DetachFromContext() en OnNavigatedFrom de la Page.
///   3. Sobreescribir OnContextChangedAsync() para recargar datos.
/// </summary>
public abstract partial class BaseContextViewModel : ObservableObject
{
    public readonly SessionService Session;

    protected BaseContextViewModel(SessionService session)
    {
        Session = session;
        session.SucursalChanged += HandleSucursalChanged;
    }

    private async void HandleSucursalChanged(int _) => await OnContextChangedAsync();

    /// <summary>
    /// Sobreescribir para recargar datos cuando cambia la tienda activa.
    /// Implementación por defecto: no-op.
    /// </summary>
    protected virtual Task OnContextChangedAsync() => Task.CompletedTask;

    /// <summary>
    /// Desuscribe del evento. Llamar desde Page.OnNavigatedFrom para evitar recargas fantasma.
    /// </summary>
    public void DetachFromContext() => Session.SucursalChanged -= HandleSucursalChanged;
}
