using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Ybridio.WinUI.Services.Workspace;

/// <summary>
/// Representa una pestaña activa en el workspace persistente del ERP.
/// Mantiene viva la instancia de <see cref="Content"/> mientras la pestaña exista,
/// preservando el estado completo del ViewModel (filtros, selecciones, grids).
/// </summary>
public sealed partial class WorkspaceTabItem : ObservableObject
{
    /// <summary>Identificador único de la pestaña (e.g. "Inventario", "Venta_123").</summary>
    public required string Key { get; init; }

    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>Glifo de Segoe MDL2 Assets para el ícono del tab (e.g. "").</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Instancia de Page preservada durante el ciclo de vida de la pestaña.
    /// No se destruye al cambiar de tab — garantiza estado persistente del ViewModel.
    /// </summary>
    public required UIElement Content { get; init; }

    [ObservableProperty]
    private bool isDirty;

    /// <summary>Indica si el usuario puede cerrar la pestaña manualmente.</summary>
    public bool IsClosable { get; init; } = true;

    /// <summary>Datos de contexto opcionales para pestañas contextuales (e.g. ID de venta).</summary>
    public object? ContextData { get; init; }

    /// <summary>Timestamp de creación de la pestaña. Permite calcular tiempo de vida del workspace item.</summary>
    public DateTime CreatedAt { get; } = DateTime.Now;
}
