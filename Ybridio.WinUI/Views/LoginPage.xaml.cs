using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();

#if DEBUG
        ViewModel.Email = "c.berletzis@dological.com";
        ViewModel.Password = "@Adm1nDO!!__";
#endif
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }
}