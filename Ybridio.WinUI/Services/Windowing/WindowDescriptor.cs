using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace Ybridio.WinUI.Services.Windowing;

/// <summary>
/// Descriptor interno que encapsula el estado de una ventana registrada
/// en <see cref="WindowManager"/>.
/// </summary>
internal sealed class WindowDescriptor
{
    /// <summary>Clave interna única: "TipoVentana_Clave" (ej: "ProductoDetailWindow_42").</summary>
    public required string Key { get; init; }

    /// <summary>Instancia WinUI 3 de la ventana.</summary>
    public required Window Instance { get; init; }

    /// <summary>AppWindow asociada para operaciones de z-order y posicionamiento.</summary>
    public required AppWindow AppWindow { get; init; }

    /// <summary>Marca de tiempo UTC en que se registró la ventana.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
