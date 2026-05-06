using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class UsuariosPage : Page
{
    private readonly IWindowManager _windowManager;
    public UsuariosViewModel ViewModel { get; }

    public UsuariosPage()
    {
        ViewModel       = App.Services.GetRequiredService<UsuariosViewModel>();
        _windowManager  = App.Services.GetRequiredService<IWindowManager>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarAbrirDetalle = AbrirDetalle;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    private void AbrirDetalle(UsuarioDto? usuario)
    {
        var key = usuario?.Id ?? Guid.Empty;
        _windowManager.OpenWindow<UsuarioDetailWindow, Guid>(
            key,
            () => new UsuarioDetailWindow(ViewModel, usuario),
            new WindowOptions { Width = 700, Height = 600, PositionStrategy = WindowPositionStrategy.CenterOwner });
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.UsuarioSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }
}
