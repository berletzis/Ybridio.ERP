using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Vista del tab "Auditoría del Sistema" en Configuración Global.
/// Delega toda la lógica a <see cref="AuditoriaViewModel"/>.
/// </summary>
public sealed partial class AuditoriaPage : Page
{
    /// <summary>ViewModel que provee datos y comandos a los bindings XAML.</summary>
    public AuditoriaViewModel ViewModel { get; }

    public AuditoriaPage()
    {
        ViewModel = App.Services.GetRequiredService<AuditoriaViewModel>();
        InitializeComponent();
    }
}
