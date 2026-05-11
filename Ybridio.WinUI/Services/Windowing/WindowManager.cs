using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Ybridio.WinUI.Services.Windowing;

/// <inheritdoc cref="IWindowManager"/>
public sealed class WindowManager : IWindowManager
{
    // ── Win32 P/Invoke ───────────────────────────────────────────────────────
    //
    // SetWindowLongPtr(GWLP_HWNDPARENT) establece la relación owner/owned.
    // El OS garantiza que la owned window siempre esté por encima del owner
    // en z-order — sin hacks de timing ni polling.

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int GWLP_HWNDPARENT = -8;
    private const int SW_RESTORE      = 9;

    // ── Estado ───────────────────────────────────────────────────────────────

    private readonly Dictionary<string, WindowDescriptor> _windows = new();
    private readonly MainWindow _mainWindow;
    private readonly ILogger<WindowManager> _logger;
    private int _cascadeCount;
    private int _detachedWindowsCount; // ADR-028: Window Detach Mode tracking

    // ── Constantes de policy ─────────────────────────────────────────────────

    /// <summary>
    /// Límite máximo global de ventanas desacopladas (detached windows) activas simultáneamente.
    /// Policy arquitectónica: Window Detach Mode — ADR-028.
    /// </summary>
    private const int MaxDetachedWindows = 2;

    /// <summary>
    /// Prefijo de convención para keys de ventanas detached.
    /// Ventanas con key que empieza con este prefijo cuentan contra el límite global.
    /// </summary>
    private const string DetachedKeyPrefix = "detached:";

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una instancia del WindowManager. Inyectado como Singleton via DI.
    /// </summary>
    /// <param name="mainWindow">Ventana principal del ERP; actúa como owner Win32 de todas las ventanas secundarias.</param>
    /// <param name="logger">Logger para diagnóstico de apertura, reutilización y errores.</param>
    public WindowManager(MainWindow mainWindow, ILogger<WindowManager> logger)
    {
        _mainWindow = mainWindow;
        _logger     = logger;
    }

    // ── IWindowManager ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void OpenWindow<TWindow, TKey>(TKey key, Func<TWindow> factory, WindowOptions? options = null)
        where TWindow : Window
    {
        if (!_mainWindow.DispatcherQueue.HasThreadAccess)
        {
            _mainWindow.DispatcherQueue.TryEnqueue(() => OpenWindow(key, factory, options));
            return;
        }

        options ??= new WindowOptions();
        var internalKey = BuildKey<TWindow, TKey>(key);

        // ── Policy: Window Detach Mode Limit (ADR-028) ──────────────────────
        // Si la key indica ventana detached y ya hay 2 abiertas, lanzar excepción operacional
        var isDetachedWindow = internalKey.StartsWith(DetachedKeyPrefix, StringComparison.Ordinal);
        if (isDetachedWindow && _detachedWindowsCount >= MaxDetachedWindows)
        {
            _logger.LogWarning("[WindowManager] Límite detached windows alcanzado ({Current}/{Max}): {Key}",
                _detachedWindowsCount, MaxDetachedWindows, internalKey);
            throw new DetachedWindowLimitException(MaxDetachedWindows, _detachedWindowsCount);
        }

        // Reutilizar instancia existente — nunca duplicar
        if (_windows.TryGetValue(internalKey, out var existing))
        {
            _logger.LogDebug("[WindowManager] Reutilizando instancia: {Key}", internalKey);
            BringDescriptorToFront(existing);
            return;
        }

        // Crear via factory
        var window    = factory();
        var hwnd      = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId  = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Validar HWND antes de establecer ownership
        if (hwnd != IntPtr.Zero)
        {
            var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, ownerHwnd);
            _logger.LogDebug("[WindowManager] Ownership establecida para: {Key}", internalKey);
        }
        else
        {
            _logger.LogWarning("[WindowManager] HWND inválido para {Key} — z-order no garantizado", internalKey);
        }

        // Tamaño y posición (antes de Activate para que el usuario nunca vea el tamaño por defecto)
        appWindow.Resize(new SizeInt32((int)options.Width, (int)options.Height));
        ApplyPositionStrategy(appWindow, options);

        // Registrar
        var descriptor = new WindowDescriptor
        {
            Key       = internalKey,
            Instance  = window,
            AppWindow = appWindow
        };
        _windows[internalKey] = descriptor;

        // Incrementar contador detached si aplica
        if (isDetachedWindow)
        {
            _detachedWindowsCount++;
            _logger.LogDebug("[WindowManager] Detached windows count: {Count}/{Max}",
                _detachedWindowsCount, MaxDetachedWindows);
        }

        // Limpiar al cerrar
        window.Closed += (_, _) =>
        {
            _windows.Remove(internalKey);

            if (options.PositionStrategy == WindowPositionStrategy.Cascade)
                _cascadeCount = Math.Max(0, _cascadeCount - 1);

            // Decrementar contador detached si aplica
            if (isDetachedWindow)
            {
                _detachedWindowsCount = Math.Max(0, _detachedWindowsCount - 1);
                _logger.LogDebug("[WindowManager] Detached window closed. Count: {Count}/{Max}",
                    _detachedWindowsCount, MaxDetachedWindows);
            }

            _logger.LogDebug("[WindowManager] Ventana cerrada: {Key}", internalKey);
        };

        _logger.LogDebug("[WindowManager] Abriendo: {Key} ({W}x{H})", internalKey, options.Width, options.Height);

        if (options.ActivateOnOpen)
            BringDescriptorToFront(descriptor);
    }

    /// <inheritdoc/>
    public bool IsOpen<TWindow, TKey>(TKey key) where TWindow : Window
        => _windows.ContainsKey(BuildKey<TWindow, TKey>(key));

    /// <inheritdoc/>
    public void CloseWindow<TWindow, TKey>(TKey key) where TWindow : Window
    {
        if (!_mainWindow.DispatcherQueue.HasThreadAccess)
        {
            _mainWindow.DispatcherQueue.TryEnqueue(() => CloseWindow<TWindow, TKey>(key));
            return;
        }

        var internalKey = BuildKey<TWindow, TKey>(key);
        if (_windows.TryGetValue(internalKey, out var descriptor))
        {
            _logger.LogDebug("[WindowManager] Cerrando: {Key}", internalKey);
            descriptor.Instance.Close();
        }
    }

    /// <inheritdoc/>
    public void BringToFront<TWindow, TKey>(TKey key) where TWindow : Window
    {
        if (!_mainWindow.DispatcherQueue.HasThreadAccess)
        {
            _mainWindow.DispatcherQueue.TryEnqueue(() => BringToFront<TWindow, TKey>(key));
            return;
        }

        var internalKey = BuildKey<TWindow, TKey>(key);
        if (_windows.TryGetValue(internalKey, out var descriptor))
        {
            _logger.LogDebug("[WindowManager] BringToFront: {Key}", internalKey);
            BringDescriptorToFront(descriptor);
        }
    }

    // ── Helpers privados ─────────────────────────────────────────────────────

    /// <summary>
    /// Activa y lleva al frente una ventana registrada con tres capas de activación:
    /// <list type="number">
    ///   <item>OverlappedPresenter.Restore() — restaura si estaba minimizada (WinUI 3 nativo)</item>
    ///   <item>AppWindow.Show() — asegura visibilidad a nivel AppWindow</item>
    ///   <item>Window.Activate() — da foco a nivel WinUI 3</item>
    ///   <item>SetForegroundWindow — garantía Win32 de foreground</item>
    /// </list>
    /// La relación owner/owned (establecida en OpenWindow) garantiza z-order
    /// permanente sin necesidad de llamadas repetidas.
    /// </summary>
    private void BringDescriptorToFront(WindowDescriptor descriptor)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(descriptor.Instance);

            if (hwnd == IntPtr.Zero)
            {
                _logger.LogWarning("[WindowManager] HWND inválido al activar: {Key}", descriptor.Key);
                return;
            }

            // 1. Restaurar si está minimizada (API nativa WinUI 3)
            if (descriptor.AppWindow.Presenter is OverlappedPresenter op &&
                op.State == OverlappedPresenterState.Minimized)
            {
                op.Restore();
            }

            // 2. Show a nivel AppWindow (asegura que sea visible aunque haya sido ocultada)
            descriptor.AppWindow.Show();

            // 3. Activate a nivel WinUI 3 (da foco de teclado)
            descriptor.Instance.Activate();

            // 4. SetForegroundWindow Win32 (garantía final de foreground)
            SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WindowManager] Error al activar: {Key}", descriptor.Key);
        }
    }

    /// <summary>
    /// Aplica la estrategia de posicionamiento con awareness de monitor activo.
    /// Siempre clampea dentro del área de trabajo para evitar ventanas fuera de pantalla.
    /// </summary>
    private void ApplyPositionStrategy(AppWindow appWindow, WindowOptions options)
    {
        try
        {
            var ownerPos  = _mainWindow.AppWindow.Position;
            var ownerSize = _mainWindow.AppWindow.Size;
            var thisSize  = appWindow.Size;

            // Área de trabajo del monitor donde está el owner (multi-monitor aware)
            var displayArea = DisplayArea.GetFromWindowId(
                _mainWindow.AppWindow.Id, DisplayAreaFallback.Primary);
            var work = displayArea.WorkArea;

            switch (options.PositionStrategy)
            {
                case WindowPositionStrategy.CenterOwner:
                {
                    var x = ownerPos.X + (ownerSize.Width  - thisSize.Width)  / 2;
                    var y = ownerPos.Y + (ownerSize.Height - thisSize.Height) / 2;

                    // Clamp dentro del área de trabajo (evita salir de pantalla)
                    x = Math.Clamp(x, work.X, work.X + Math.Max(0, work.Width  - thisSize.Width));
                    y = Math.Clamp(y, work.Y, work.Y + Math.Max(0, work.Height - thisSize.Height));

                    appWindow.Move(new PointInt32(x, y));
                    break;
                }

                case WindowPositionStrategy.Cascade:
                {
                    var offset = _cascadeCount * options.CascadeOffset;
                    var x      = ownerPos.X + 80 + offset;
                    var y      = ownerPos.Y + 80 + offset;

                    // Resetear cascada si sale del área de trabajo
                    if (x + thisSize.Width  > work.X + work.Width ||
                        y + thisSize.Height > work.Y + work.Height)
                    {
                        _cascadeCount = 0;
                        x = ownerPos.X + 80;
                        y = ownerPos.Y + 80;
                        _logger.LogDebug("[WindowManager] Cascade reset (fuera de área de trabajo)");
                    }

                    appWindow.Move(new PointInt32(x, y));
                    _cascadeCount++;
                    break;
                }

                case WindowPositionStrategy.CenterScreen:
                {
                    // Centrar en el área de trabajo del monitor activo
                    var x = work.X + (work.Width  - thisSize.Width)  / 2;
                    var y = work.Y + (work.Height - thisSize.Height) / 2;
                    appWindow.Move(new PointInt32(x, y));
                    break;
                }

                case WindowPositionStrategy.Manual:
                    break; // El OS gestiona la posición inicial
            }
        }
        catch (Exception ex)
        {
            // Posicionamiento es no-crítico; si falla, el OS ubica la ventana por defecto
            _logger.LogWarning(ex, "[WindowManager] Error en posicionamiento, usando posición por defecto");
        }
    }

    /// <summary>
    /// Construye la clave interna del diccionario: "TipoVentana_Clave".
    /// Ejemplo: "ProductoDetailWindow_42"
    /// </summary>
    private static string BuildKey<TWindow, TKey>(TKey key)
        => $"{typeof(TWindow).Name}_{key}";
}
