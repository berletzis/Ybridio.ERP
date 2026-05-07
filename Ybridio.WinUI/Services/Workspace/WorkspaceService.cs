using System;
using System.Collections.ObjectModel;
using System.Linq;
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
}
