using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Services.Workspace;

/// <summary>
/// Implementación Singleton del workspace de pestañas persistentes del ERP.
/// Persiste durante toda la sesión; se limpia con <see cref="CloseAll"/> en Logout.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    /// <inheritdoc/>
    public ObservableCollection<WorkspaceTabItem> Tabs { get; } = [];

    /// <inheritdoc/>
    public WorkspaceTabItem? ActiveTab { get; private set; }

    /// <inheritdoc/>
    public event Action<WorkspaceTabItem?>? ActiveTabChanged;

    /// <inheritdoc/>
    public WorkspaceTabItem OpenTab(
        string key,
        string title,
        string icon,
        Func<Page> pageFactory,
        object? contextData = null,
        bool isClosable = true)
    {
        var existing = Tabs.FirstOrDefault(t => t.Key == key);
        if (existing is not null)
        {
            ActivateTab(key);
            return existing;
        }

        var tab = new WorkspaceTabItem
        {
            Key         = key,
            Content     = pageFactory(),
            Icon        = icon,
            IsClosable  = isClosable,
            ContextData = contextData,
        };
        tab.Title = title;

        Tabs.Add(tab);
        ActivateTab(key);
        return tab;
    }

    /// <inheritdoc/>
    public void ActivateTab(string key)
    {
        var tab = Tabs.FirstOrDefault(t => t.Key == key);
        if (tab is null || tab == ActiveTab) return;

        ActiveTab = tab;
        ActiveTabChanged?.Invoke(tab);
    }

    /// <inheritdoc/>
    public bool ActivateModuleTab(string moduleKey)
    {
        var tab = Tabs.FirstOrDefault(t => t.Key == moduleKey);
        if (tab is null) return false;
        ActivateTab(moduleKey);
        return true;
    }

    /// <inheritdoc/>
    public void CloseTab(string key)
    {
        var tab = Tabs.FirstOrDefault(t => t.Key == key);
        if (tab is null) return;

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (ActiveTab != tab) return;

        var next = idx < Tabs.Count ? Tabs[idx] : Tabs.LastOrDefault();
        ActiveTab = next;
        ActiveTabChanged?.Invoke(next);
    }

    /// <inheritdoc/>
    public bool Exists(string key) => Tabs.Any(t => t.Key == key);

    /// <inheritdoc/>
    public void CloseAll()
    {
        Tabs.Clear();
        ActiveTab = null;
        ActiveTabChanged?.Invoke(null);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS — Workspace Operational UX Stabilization
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Abre un documento operacional en el workspace con single-instance enforcement.
    /// Si el documento ya existe (mismo <paramref name="key"/>), activa el tab existente.
    /// Si no existe, carga los datos con <paramref name="dataLoader"/> y abre un nuevo tab.
    /// </summary>
    /// <typeparam name="TData">Tipo de datos del documento (ej. VentaDto, PedidoDto).</typeparam>
    /// <param name="key">Clave única del documento (ej. "venta-91", "pedido-55").</param>
    /// <param name="title">Título runtime del tab (ej. "Venta #91", "Pedido #55").</param>
    /// <param name="icon">Glifo Segoe MDL2 del ícono (ej. "", "").</param>
    /// <param name="dataLoader">Función async que carga los datos del documento desde el servicio Application.</param>
    /// <param name="pageFactory">Función que crea la Page del documento con los datos cargados.</param>
    /// <param name="onError">Callback opcional para manejar errores de carga.</param>
    /// <param name="isClosable">Si el usuario puede cerrar el tab. Por defecto <c>true</c>.</param>
    /// <returns>El <see cref="WorkspaceTabItem"/> creado o reutilizado.</returns>
    /// <remarks>
    /// Este método centraliza el patrón <c>Exists() → ActivateTab()</c> vs <c>await service → OpenTab()</c>
    /// que antes se repetía manualmente en cada Page. Garantiza single-document-instance: un solo tab por documento.
    /// </remarks>
    public async Task<WorkspaceTabItem?> OpenOrActivateDocumentTabAsync<TData>(
        string key,
        string title,
        string icon,
        Func<Task<TData?>> dataLoader,
        Func<TData, Page> pageFactory,
        Action<string>? onError = null,
        bool isClosable = true)
    {
        // Single-instance: si el documento ya está abierto, solo activar
        if (Exists(key))
        {
            ActivateTab(key);
            return Tabs.FirstOrDefault(t => t.Key == key);
        }

        // Cargar datos del documento
        TData? data;
        try
        {
            data = await dataLoader();
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Error al cargar datos: {ex.Message}");
            return null;
        }

        if (data is null)
        {
            onError?.Invoke("No se pudieron cargar los datos del documento.");
            return null;
        }

        // Abrir nuevo tab con los datos cargados
        return OpenTab(
            key:         key,
            title:       title,
            icon:        icon,
            pageFactory: () => pageFactory(data),
            contextData: data,
            isClosable:  isClosable);
    }
}
