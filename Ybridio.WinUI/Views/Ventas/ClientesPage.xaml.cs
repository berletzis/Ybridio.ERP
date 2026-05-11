using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Módulo Clientes con Document Surface UX Pattern (ADR-025 + ADR-030).
/// Migrado de dialog-based CRUD a embedded Document Surface.
/// </summary>
public sealed partial class ClientesPage : Page, ILiveContextReporter
{
    public ClientesViewModel ViewModel { get; }

    public ClientesPage()
    {
        ViewModel = App.Services.GetRequiredService<ClientesViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
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
        if (ViewModel.ClienteSeleccionado is not null)
            BtnAbrir_Click(sender, null!);
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    // ── Document Surface UX Pattern (ADR-025 + ADR-030) ─────────────────────

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        var page = new ClienteDocumentoPage(null);
        page.GuardarAsync = (existente, nombre, rfc, email, tel, dir, notas, limite) =>
            ViewModel.GuardarAsync(existente, nombre, rfc, email, tel, dir, notas, limite);
        page.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        page.ToggleDetach = OnToggleDetach;
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClienteSeleccionado is null) return;

        var page = new ClienteDocumentoPage(ViewModel.ClienteSeleccionado);
        page.GuardarAsync = (existente, nombre, rfc, email, tel, dir, notas, limite) =>
            ViewModel.GuardarAsync(existente, nombre, rfc, email, tel, dir, notas, limite);
        page.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        page.ToggleDetach = OnToggleDetach;
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private void OnDocumentSaved()
    {
        _ = ViewModel.CerrarDocumentSurfaceAsync();
    }

    private void OnVolverALista()
    {
        _ = ViewModel.CerrarDocumentSurfaceAsync();
    }

    private void OnToggleDetach()
    {
        ViewModel.ToggleDetach();
    }
}
