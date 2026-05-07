using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Services.Workspace;

/// <summary>
/// Gestiona el workspace de pestañas persistentes del ERP.
/// Las pestañas conservan su Page en memoria para preservar grids, filtros y estado del ViewModel.
/// Complementa a <c>IWindowManager</c>: el Workspace es para módulos principales,
/// <c>IWindowManager</c> es para dialogs y ventanas auxiliares.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>Colección observable de pestañas actualmente abiertas.</summary>
    ObservableCollection<WorkspaceTabItem> Tabs { get; }

    /// <summary>Pestaña actualmente activa, o <c>null</c> si el workspace está vacío.</summary>
    WorkspaceTabItem? ActiveTab { get; }

    /// <summary>Se dispara cuando cambia la pestaña activa (incluye <c>null</c> al cerrar la última).</summary>
    event Action<WorkspaceTabItem?>? ActiveTabChanged;

    /// <summary>
    /// Abre una nueva pestaña o activa la existente si ya está abierta con esa <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Clave única de la pestaña. Determina la deduplicación.</param>
    /// <param name="title">Título mostrado en el tab.</param>
    /// <param name="icon">Glifo de Segoe MDL2 Assets para el ícono.</param>
    /// <param name="pageFactory">Función que instancia la Page; solo se llama si la pestaña no existe.</param>
    /// <param name="contextData">Datos de contexto para pestañas dinámicas (e.g. ID de entidad).</param>
    /// <param name="isClosable">Si el usuario puede cerrar la pestaña. Por defecto <c>true</c>.</param>
    /// <returns>El <see cref="WorkspaceTabItem"/> creado o reutilizado.</returns>
    WorkspaceTabItem OpenTab(
        string key,
        string title,
        string icon,
        Func<Page> pageFactory,
        object? contextData = null,
        bool isClosable = true);

    /// <summary>Activa la pestaña con la clave dada sin crear una nueva.</summary>
    void ActivateTab(string key);

    /// <summary>Cierra y elimina la pestaña con la clave dada.</summary>
    void CloseTab(string key);

    /// <summary>Devuelve <c>true</c> si ya existe una pestaña abierta con esa clave.</summary>
    bool Exists(string key);

    /// <summary>
    /// Cierra todas las pestañas y limpia el workspace.
    /// Debe llamarse en Logout para liberar recursos y limpiar suscripciones.
    /// </summary>
    void CloseAll();
}
