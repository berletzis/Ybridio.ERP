using Microsoft.UI.Xaml;
using System;

namespace Ybridio.WinUI.Services.Windowing;

/// <summary>
/// Servicio centralizado para gestionar el ciclo de vida y comportamiento visual
/// de todas las ventanas secundarias del ERP.
/// </summary>
/// <remarks>
/// Garantiza:
/// <list type="bullet">
///   <item>Una única instancia por registro (sin duplicados)</item>
///   <item>La ventana siempre aparece al frente mediante relación Win32 owner/owned</item>
///   <item>Posicionamiento coherente según <see cref="WindowPositionStrategy"/></item>
///   <item>Limpieza automática al cerrar (sin memory leaks)</item>
/// </list>
/// Debe registrarse como Singleton en el contenedor DI.
/// </remarks>
public interface IWindowManager
{
    /// <summary>
    /// Abre una ventana del tipo indicado para la clave especificada.
    /// Si ya existe una instancia abierta para esa clave, la trae al frente
    /// sin crear una nueva.
    /// </summary>
    /// <typeparam name="TWindow">Tipo de ventana (debe heredar de <see cref="Window"/>).</typeparam>
    /// <typeparam name="TKey">Tipo del identificador lógico (ej: <see cref="int"/> para ProductoId).</typeparam>
    /// <param name="key">Identificador único del registro. Ej: ID del producto.</param>
    /// <param name="factory">Función que crea la ventana cuando no existe instancia previa.</param>
    /// <param name="options">Tamaño, posición y comportamiento. Si es <see langword="null"/>, usa valores por defecto.</param>
    /// <example>
    /// Abrir detalle de producto desde ProductosPage:
    /// <code>
    /// _windowManager.OpenWindow&lt;ProductoDetailWindow, int&gt;(
    ///     productoId,
    ///     () => new ProductoDetailWindow(ViewModel, producto),
    ///     new WindowOptions { Width = 900, Height = 700 }
    /// );
    /// </code>
    /// </example>
    void OpenWindow<TWindow, TKey>(TKey key, Func<TWindow> factory, WindowOptions? options = null)
        where TWindow : Window;

    /// <summary>
    /// Indica si hay una ventana del tipo y clave especificados actualmente abierta.
    /// </summary>
    /// <typeparam name="TWindow">Tipo de ventana.</typeparam>
    /// <typeparam name="TKey">Tipo del identificador lógico.</typeparam>
    /// <param name="key">Identificador del registro.</param>
    /// <returns><see langword="true"/> si la ventana está abierta.</returns>
    bool IsOpen<TWindow, TKey>(TKey key) where TWindow : Window;

    /// <summary>
    /// Cierra programáticamente la ventana del tipo y clave indicados, si está abierta.
    /// </summary>
    /// <typeparam name="TWindow">Tipo de ventana.</typeparam>
    /// <typeparam name="TKey">Tipo del identificador lógico.</typeparam>
    /// <param name="key">Identificador del registro.</param>
    void CloseWindow<TWindow, TKey>(TKey key) where TWindow : Window;

    /// <summary>
    /// Trae al frente la ventana indicada sin reabrirla.
    /// Útil para re-enfocar una ventana minimizada o cubierta.
    /// </summary>
    /// <typeparam name="TWindow">Tipo de ventana.</typeparam>
    /// <typeparam name="TKey">Tipo del identificador lógico.</typeparam>
    /// <param name="key">Identificador del registro.</param>
    void BringToFront<TWindow, TKey>(TKey key) where TWindow : Window;

    /// <summary>
    /// Single Document Session Rule — intenta activar/enfocar una ventana existente
    /// usando solo la clave de documento, sin conocer el tipo de ventana.
    /// </summary>
    /// <remarks>
    /// Usar SIEMPRE antes de abrir cualquier documento para evitar crear múltiples
    /// sesiones runtime del mismo documento simultáneamente.
    ///
    /// Busca en el registro de ventanas activas cualquier ventana cuya key interna
    /// termine con <c>_{documentKey}</c>. La key de documento sigue la convención:
    /// <c>detached:{tipo}:{id}</c> (ej: <c>detached:cotizacion:123</c>).
    ///
    /// Si la ventana existe: la activa y retorna <c>true</c> → el caller debe abortar la apertura.
    /// Si no existe: retorna <c>false</c> → el caller puede abrir normalmente.
    /// </remarks>
    /// <param name="documentKey">
    /// Clave del documento (sin prefijo de tipo de ventana).
    /// Ejemplo: <c>"detached:cotizacion:123"</c>
    /// </param>
    /// <returns>
    /// <c>true</c> si existía una sesión activa y fue activada; <c>false</c> si no existe.
    /// </returns>
    bool TryActivateWindow(string documentKey);
}
