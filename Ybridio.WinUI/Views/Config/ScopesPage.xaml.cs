using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.WinUI.ViewModels.Config;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class ScopesPage : Page
{
    public ScopesViewModel ViewModel { get; }

    public ScopesPage()
    {
        ViewModel = App.Services.GetRequiredService<ScopesViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarEditarScopes = AbrirDialogoEditarScopes;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.ScopeSeleccionado is not null) ViewModel.EditarScopesCommand.Execute(null);
    }

    private async void AbrirDialogoEditarScopes(ScopeUsuarioDto scope)
    {
        var (sucursalesDisp, almacenesDisp)         = await ViewModel.CargarDisponiblesAsync();
        var (sucursalesActuales, almacenesActuales)  = await ViewModel.CargarScopesActualesAsync(scope.UsuarioId);

        var sucActSet = new HashSet<int>(sucursalesActuales);
        var almActSet = new HashSet<int>(almacenesActuales);

        var checksSuc = new List<(CheckBox cb, SucursalScopeItem item)>();
        var checksAlm = new List<(CheckBox cb, AlmacenScopeItem item)>();

        var panel = new StackPanel { Spacing = 4 };

        // -- Sucursales --------------------------------------------------------
        panel.Children.Add(new TextBlock
        {
            Text       = "Sucursales",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize   = 13,
            Margin     = new Thickness(0, 0, 0, 4)
        });

        if (sucursalesDisp.Count == 0)
            panel.Children.Add(new TextBlock { Text = "Sin sucursales disponibles.", Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"] });
        else
        {
            foreach (var s in sucursalesDisp)
            {
                var cb = new CheckBox { Content = s.Nombre, IsChecked = sucActSet.Contains(s.Id) };
                checksSuc.Add((cb, s));
                panel.Children.Add(cb);
            }
        }

        // -- Almacenes --------------------------------------------------------
        panel.Children.Add(new TextBlock
        {
            Text       = "Almacenes",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize   = 13,
            Margin     = new Thickness(0, 16, 0, 4)
        });

        if (almacenesDisp.Count == 0)
            panel.Children.Add(new TextBlock { Text = "Sin almacenes disponibles.", Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"] });
        else
        {
            foreach (var a in almacenesDisp)
            {
                var cb = new CheckBox { Content = a.Nombre, IsChecked = almActSet.Contains(a.Id) };
                checksAlm.Add((cb, a));
                panel.Children.Add(cb);
            }
        }

        panel.Children.Add(new TextBlock
        {
            Text       = scope.EsSuperAdmin ? "ℹ  Usuario SuperAdmin — los scopes se registran pero no se aplican." : "Vacío = sin restricción (accede a todo).",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize   = 12,
            Margin     = new Thickness(0, 16, 0, 0)
        });

        var dialog = new ContentDialog
        {
            Title               = $"Scopes — {scope.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(4, 0, 4, 0) },
            MinWidth            = 400
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var sucIds = checksSuc.Where(x => x.cb.IsChecked == true).Select(x => x.item.Id).ToList();
        var almIds = checksAlm.Where(x => x.cb.IsChecked == true).Select(x => x.item.Id).ToList();

        await ViewModel.GuardarScopesAsync(scope.UsuarioId, sucIds, almIds);
    }
}
