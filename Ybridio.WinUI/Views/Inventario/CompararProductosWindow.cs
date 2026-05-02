using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.UI;
using Ybridio.Application.DTOs.Catalogos;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Inventario;

public sealed class CompararProductosWindow : Window
{
    public CompararProductosWindow(ProductoDto a, ProductoDto b)
    {
        Title = "Comparar productos";
        AppWindow.Resize(new SizeInt32(900, 600));
        Content = BuildUI(a, b);
    }

    private UIElement BuildUI(ProductoDto a, ProductoDto b)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Encabezado de columnas ────────────────────────────────────────────
        var headerGrid = new Grid
        {
            Background = XamlApp.Current.Resources["LayerFillColorDefaultBrush"] as Brush,
            Padding = new Thickness(16, 10, 16, 10)   // Thickness siempre 4 args en WinUI 3
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var hdrCampo = new TextBlock
        {
            Text = "Campo",
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 }
        };
        var hdrA = new TextBlock
        {
            Text = a.Nombre,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 },
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var hdrB = new TextBlock
        {
            Text = b.Nombre,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 },
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 0, 0)
        };
        headerGrid.Children.Add(hdrCampo);
        headerGrid.Children.Add(hdrA); Grid.SetColumn(hdrA, 1);
        headerGrid.Children.Add(hdrB); Grid.SetColumn(hdrB, 2);

        root.Children.Add(headerGrid);
        Grid.SetRow(headerGrid, 0);

        // ── Filas de comparación ──────────────────────────────────────────────
        var scroll = new ScrollViewer();
        var stack = new StackPanel();

        var fields = new List<(string Label, string ValA, string ValB)>
        {
            ("Código",            a.Codigo,                               b.Codigo),
            ("Código de barras",  a.CodigoBarras ?? "—",                 b.CodigoBarras ?? "—"),
            ("Nombre",            a.Nombre,                              b.Nombre),
            ("Descripción",       a.Descripcion ?? "—",                  b.Descripcion ?? "—"),
            ("Precio",            a.Precio.ToString("N2"),                b.Precio.ToString("N2")),
            ("Precio mínimo",     a.PrecioMinimo?.ToString("N2") ?? "—",  b.PrecioMinimo?.ToString("N2") ?? "—"),
            ("Costo",             a.Costo?.ToString("N2") ?? "—",         b.Costo?.ToString("N2") ?? "—"),
            ("Categoría",         a.CategoriaNombre ?? "—",              b.CategoriaNombre ?? "—"),
            ("Tipo de producto",  a.TipoProductoNombre ?? "—",           b.TipoProductoNombre ?? "—"),
            ("Unidad de medida",  a.UnidadMedidaNombre ?? "—",           b.UnidadMedidaNombre ?? "—"),
            ("Tipo de impuesto",  a.TipoImpuestoNombre ?? "—",           b.TipoImpuestoNombre ?? "—"),
            ("IVA aplicable",     a.IvaAplicable ? "Sí" : "No",          b.IvaAplicable ? "Sí" : "No"),
            ("Stock mínimo",      a.StockMinimo?.ToString("N0") ?? "—",   b.StockMinimo?.ToString("N0") ?? "—"),
            ("Stock máximo",      a.StockMaximo?.ToString("N0") ?? "—",   b.StockMaximo?.ToString("N0") ?? "—"),
            ("Activo",            a.Activo ? "Sí" : "No",                b.Activo ? "Sí" : "No"),
        };

        var diffBrush = new SolidColorBrush(Color.FromArgb(80, 255, 220, 0));
        var altBrush = XamlApp.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush;
        var diffTextBrush = new SolidColorBrush(Colors.OrangeRed);
        var secondaryBrush = XamlApp.Current.Resources["TextFillColorSecondaryBrush"] as Brush;

        for (int i = 0; i < fields.Count; i++)
        {
            var (label, valA, valB) = fields[i];
            var isDiff = valA != valB;

            var row = new Grid
            {
                Padding = new Thickness(16, 6, 16, 6),   // 4 args
                Background = isDiff ? diffBrush : (i % 2 == 0 ? null : altBrush)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblBlock = new TextBlock
            {
                Text = label,
                Foreground = secondaryBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var valABlock = new TextBlock
            {
                Text = valA,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var valBBlock = new TextBlock
            {
                Text = valB,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            if (isDiff) valBBlock.Foreground = diffTextBrush;

            row.Children.Add(lblBlock);
            row.Children.Add(valABlock); Grid.SetColumn(valABlock, 1);
            row.Children.Add(valBBlock); Grid.SetColumn(valBBlock, 2);

            stack.Children.Add(row);
        }

        scroll.Content = stack;
        root.Children.Add(scroll);
        Grid.SetRow(scroll, 1);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(16, 8, 16, 8),   // 4 args
            Spacing = 8
        };

        var legend = new Border
        {
            Width = 16, Height = 16,
            Background = diffBrush,
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var legendTxt = new TextBlock
        {
            Text = "= valores diferentes",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        var btnCerrar = new Button { Content = "Cerrar" };
        btnCerrar.Click += (_, _) => Close();

        footer.Children.Add(legend);
        footer.Children.Add(legendTxt);
        footer.Children.Add(btnCerrar);
        root.Children.Add(footer);
        Grid.SetRow(footer, 2);

        return root;
    }
}
