using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Controls;

/// <summary>
/// Grid subclass that exposes cursor control for column resize feedback.
/// ProtectedCursor is protected on UIElement, so it can only be set from a derived class.
/// </summary>
internal sealed class HeaderGrid : Grid
{
    private static readonly InputCursor ResizeCursor =
        InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

    internal void ShowResizeCursor() => ProtectedCursor = ResizeCursor;
    internal void RestoreCursor()    => ProtectedCursor = null;
}
