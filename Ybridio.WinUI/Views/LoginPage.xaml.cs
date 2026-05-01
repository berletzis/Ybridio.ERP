using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
    }
}
