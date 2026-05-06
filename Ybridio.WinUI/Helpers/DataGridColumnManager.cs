using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Text.Json;
using Windows.Storage;
using Ybridio.WinUI.Controls;

namespace Ybridio.WinUI.Helpers;

/// <summary>
/// Manages column widths for ERP ListView-based grids:
/// restores saved widths, syncs item containers via ContainerContentChanging,
/// enables user resize via header drag, and persists to LocalSettings.
///
/// Usage (once, after InitializeComponent):
///   DataGridColumnManager.Initialize(myListView, myHeaderGrid, "MyModuleGrid");
///
/// Contract:
///   - Header Grid and ItemTemplate Grid must have identical ColumnDefinition counts.
///   - Header Grid should be a HeaderGrid for resize cursor feedback (optional).
///   - Column 0 (indicator spacer) and the last column (action) are not resizable.
///   - Absolute-width middle columns are resizable by dragging their right edge.
/// </summary>
public sealed class DataGridColumnManager
{
    private readonly ListView _listView;
    private readonly Grid     _headerGrid;
    private readonly string   _storageKey;
    private readonly bool[]   _resizable;

    private bool   _isDragging;
    private int    _dragColumnIndex;
    private double _dragStartX;
    private double _dragStartWidth;

    private const double HitZone     = 6.0;
    private const double MinColWidth = 40.0;
    private const double MaxColWidth = 800.0;

    private DataGridColumnManager(ListView listView, Grid headerGrid, string gridKey)
    {
        _listView   = listView;
        _headerGrid = headerGrid;
        _storageKey = gridKey;
        _resizable  = BuildResizableFlags(headerGrid.ColumnDefinitions);

        RestoreOrKeepDefaults();

        headerGrid.PointerMoved       += Header_PointerMoved;
        headerGrid.PointerPressed     += Header_PointerPressed;
        headerGrid.PointerReleased    += Header_PointerReleased;
        headerGrid.PointerCaptureLost += Header_PointerCaptureLost;

        listView.ContainerContentChanging += ListView_ContainerContentChanging;
    }

    /// <summary>
    /// Attaches column management to a ListView.
    /// Call once from OnNavigatedTo or Page.Loaded, after InitializeComponent.
    /// </summary>
    public static DataGridColumnManager Initialize(
        ListView listView,
        Grid headerGrid,
        string gridKey)
        => new(listView, headerGrid, gridKey);

    // ── Restore ───────────────────────────────────────────────────────────────

    private void RestoreOrKeepDefaults()
    {
        var saved = LoadWidths();
        if (saved is null) return;

        var cols = _headerGrid.ColumnDefinitions;
        for (int i = 0; i < Math.Min(saved.Length, cols.Count); i++)
        {
            if (_resizable[i] && saved[i] > 0)
                cols[i].Width = new GridLength(saved[i], GridUnitType.Pixel);
        }
    }

    // ── Item container sync ───────────────────────────────────────────────────

    private void ListView_ContainerContentChanging(
        ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue)
            args.RegisterUpdateCallback(SyncItemGridWidths);
    }

    private void SyncItemGridWidths(
        ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer?.ContentTemplateRoot is Grid itemGrid)
            ApplyHeaderWidthsTo(itemGrid);
    }

    private void ApplyHeaderWidthsTo(Grid itemGrid)
    {
        var hCols = _headerGrid.ColumnDefinitions;
        var iCols = itemGrid.ColumnDefinitions;
        int count = Math.Min(hCols.Count, iCols.Count);
        for (int i = 0; i < count; i++)
            iCols[i].Width = hCols[i].Width;
    }

    // Called during drag to update currently visible containers immediately.
    private void SyncAllVisibleItems()
    {
        for (int i = 0; i < _listView.Items.Count; i++)
        {
            if (_listView.ContainerFromIndex(i) is ListViewItem container &&
                container.ContentTemplateRoot is Grid itemGrid)
            {
                ApplyHeaderWidthsTo(itemGrid);
            }
        }
    }

    // ── Header resize (pointer) ───────────────────────────────────────────────

    // Returns the index of the column whose right edge is within HitZone of pointerX.
    // Coordinates are relative to _headerGrid; left padding is factored in.
    private int? GetBoundaryAt(double pointerX)
    {
        double offset = _headerGrid.Padding.Left;
        var cols = _headerGrid.ColumnDefinitions;
        for (int i = 0; i < cols.Count - 1; i++)
        {
            offset += cols[i].ActualWidth;
            if (_resizable[i] && Math.Abs(pointerX - offset) <= HitZone)
                return i;
        }
        return null;
    }

    private void Header_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(_headerGrid).Position;

        if (_isDragging)
        {
            double newWidth = Math.Clamp(
                _dragStartWidth + (pt.X - _dragStartX),
                MinColWidth, MaxColWidth);

            _headerGrid.ColumnDefinitions[_dragColumnIndex].Width =
                new GridLength(newWidth, GridUnitType.Pixel);

            SyncAllVisibleItems();
            e.Handled = true;
            return;
        }

        bool atBoundary = GetBoundaryAt(pt.X).HasValue;
        if (_headerGrid is HeaderGrid hg)
        {
            if (atBoundary) hg.ShowResizeCursor();
            else            hg.RestoreCursor();
        }
    }

    private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt       = e.GetCurrentPoint(_headerGrid).Position;
        var boundary = GetBoundaryAt(pt.X);
        if (!boundary.HasValue) return;

        _isDragging      = true;
        _dragColumnIndex = boundary.Value;
        _dragStartX      = pt.X;
        _dragStartWidth  = _headerGrid.ColumnDefinitions[_dragColumnIndex].Width.Value;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Header_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        if (_headerGrid is HeaderGrid hg) hg.RestoreCursor();
        SaveWidths();
        e.Handled = true;
    }

    private void Header_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        if (_headerGrid is HeaderGrid hg) hg.RestoreCursor();
        SaveWidths();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void SaveWidths()
    {
        var cols   = _headerGrid.ColumnDefinitions;
        var widths = new double[cols.Count];
        for (int i = 0; i < cols.Count; i++)
        {
            var w = cols[i].Width;
            widths[i] = w.IsAbsolute ? w.Value : -1.0;
        }
        try
        {
            ApplicationData.Current.LocalSettings.Values[_storageKey] =
                JsonSerializer.Serialize(widths);
        }
        catch { }
    }

    private double[]? LoadWidths()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values
                    .TryGetValue(_storageKey, out var raw) && raw is string json)
            {
                return JsonSerializer.Deserialize<double[]>(json);
            }
        }
        catch { }
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Column 0 (indicator spacer) and the last column (action/status) are skipped.
    // Only absolute-width middle columns are resizable.
    private static bool[] BuildResizableFlags(ColumnDefinitionCollection cols)
    {
        var flags = new bool[cols.Count];
        for (int i = 1; i < cols.Count - 1; i++)
            flags[i] = cols[i].Width.IsAbsolute && cols[i].Width.Value > 0;
        return flags;
    }
}
