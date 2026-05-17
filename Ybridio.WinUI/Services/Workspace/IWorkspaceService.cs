using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

    /// <summary>
    /// Activa el tab de módulo indicado por su key.
    /// Usado por workflows que necesitan activar un tab de módulo sin abrir un documento.
    /// </summary>
    bool ActivateModuleTab(string moduleKey);

    /// <summary>Devuelve <c>true</c> si ya existe una pestaña abierta con esa clave.</summary>
    bool Exists(string key);

    /// <summary>
    /// Cierra todas las pestañas y limpia el workspace.
    /// Debe llamarse en Logout para liberar recursos y limpiar suscripciones.
    /// </summary>
    void CloseAll();

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
    /// <returns>El <see cref="WorkspaceTabItem"/> creado o reutilizado, o <c>null</c> si falla la carga de datos.</returns>
    /// <remarks>
    /// <para>
    /// Este método centraliza el patrón <c>Exists() → ActivateTab()</c> vs <c>await service → OpenTab()</c>
    /// que antes se repetía manualmente en cada Page.
    /// </para>
    /// <para>
    /// <strong>Single-Document-Instance Policy</strong>:
    /// Un solo tab por documento operacional. Si el usuario intenta abrir un documento ya abierto
    /// (ej. "Venta #91"), el workspace activa el tab existente en lugar de crear un duplicado.
    /// </para>
    /// <para>
    /// <strong>Key Conventions</strong>:
    /// - Documentos guardados: <c>"{tipo}-{id}"</c> (ej. <c>"venta-91"</c>, <c>"pedido-55"</c>, <c>"ot-12"</c>)
    /// - Documentos nuevos: <c>"{tipo}-nueva-{guid}"</c> (ej. <c>"venta-nueva-abc123"</c>)
    /// - Módulos operacionales: <c>"{modulo}"</c> (ej. <c>"inventario"</c>, <c>"dashboard"</c>)
    /// </para>
    /// <para>
    /// <strong>Title Conventions</strong>:
    /// - Documentos guardados: <c>"{Tipo} #{id}"</c> (ej. <c>"Venta #91"</c>, <c>"OT #12"</c>)
    /// - Documentos nuevos: <c>"Nuevo/Nueva {Tipo}"</c> (ej. <c>"Nueva Venta"</c>, <c>"Nuevo Pedido"</c>)
    /// - Módulos: nombre completo (ej. <c>"Inventario"</c>, <c>"Dashboard"</c>)
    /// </para>
    /// </remarks>
    Task<WorkspaceTabItem?> OpenOrActivateDocumentTabAsync<TData>(
        string key,
        string title,
        string icon,
        Func<Task<TData?>> dataLoader,
        Func<TData, Page> pageFactory,
        Action<string>? onError = null,
        bool isClosable = true);
}
