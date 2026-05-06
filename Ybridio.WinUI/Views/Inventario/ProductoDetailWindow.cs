using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Inventario;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Inventario;

public sealed class ProductoDetailWindow : Microsoft.UI.Xaml.Window
{
    private readonly ProductosViewModel _vm;
    private readonly ProductoDto? _original;

    private TextBox   _txtCodigo      = null!;
    private TextBox   _txtCodigoBarras = null!;
    private TextBox   _txtNombre      = null!;
    private TextBox   _txtDescripcion = null!;
    private NumberBox _nbPrecio       = null!;
    private NumberBox _nbPrecioMinimo = null!;
    private NumberBox _nbCosto        = null!;
    private ComboBox  _cmbTipoProducto = null!;
    private ComboBox  _cmbUnidadMedida = null!;
    private ComboBox  _cmbTipoImpuesto = null!;
    private CheckBox  _chkIvaAplicable = null!;
    private NumberBox _nbStockMinimo  = null!;
    private NumberBox _nbStockMaximo  = null!;
    private CheckBox  _chkActivo      = null!;
    private Button    _btnGuardar     = null!;
    private TextBlock _txtError       = null!;

    // ── Selector de categorías múltiple ──────────────────────────────────────
    private StackPanel _chipsPanel = null!;
    private readonly List<CategoriaProductoDto> _categoriasSeleccionadas = new();
    private readonly Dictionary<int, CheckBox>  _categoriaCheckBoxes    = new();
    private bool _suppressCheckboxEvents;

    public ProductoDetailWindow(ProductosViewModel vm, ProductoDto? producto)
    {
        _vm       = vm;
        _original = producto;

        Title = producto is null ? "Nuevo producto" : $"Editar: {producto.Nombre}";

        try
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            mainWindow.Closed += (_, _) => this.Close();
        }
        catch { }

        Content = BuildUI();

        if (producto is not null)
            PopulateForm(producto);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private UIElement BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text   = _original is null ? "Nuevo producto" : "Editar producto",
            Style  = XamlApp.Current.Resources["SubtitleTextBlockStyle"] as Style,
            Margin = new Thickness(20, 16, 20, 8)
        };
        root.Children.Add(header);
        Grid.SetRow(header, 0);

        var scroll = new ScrollViewer { Padding = new Thickness(20, 0, 20, 8) };
        var form   = new StackPanel { Spacing = 16 };

        // ── Identificación ────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Identificación"));
        _txtCodigo       = new TextBox { Header = "Código *",          PlaceholderText = "Ej: PROD-001" };
        _txtCodigoBarras = new TextBox { Header = "Código de barras",  PlaceholderText = "Opcional" };
        _txtNombre       = new TextBox { Header = "Nombre *",          PlaceholderText = "Nombre del producto" };
        _txtDescripcion  = new TextBox
        {
            Header          = "Descripción",
            PlaceholderText = "Opcional",
            AcceptsReturn   = true,
            TextWrapping    = TextWrapping.Wrap,
            MinHeight       = 60
        };
        form.Children.Add(TwoColRow(_txtCodigo, _txtCodigoBarras));
        form.Children.Add(TwoColRow(_txtNombre, _txtDescripcion));

        // ── Precios ───────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Precios"));
        _nbPrecio = new NumberBox
        {
            Header                   = "Precio *",
            PlaceholderText          = "0.00",
            Minimum                  = 0,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment      = HorizontalAlignment.Stretch
        };
        _nbPrecioMinimo = new NumberBox
        {
            Header                   = "Precio mínimo",
            PlaceholderText          = "0.00",
            Minimum                  = 0,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment      = HorizontalAlignment.Stretch
        };
        _nbCosto = new NumberBox
        {
            Header                   = "Costo",
            PlaceholderText          = "0.00",
            Minimum                  = 0,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment      = HorizontalAlignment.Stretch
        };
        form.Children.Add(ThreeColRow(_nbPrecio, _nbPrecioMinimo, _nbCosto));

        // ── Clasificación ─────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Clasificación"));
        _cmbTipoProducto = new ComboBox
        {
            Header              = "Tipo de producto",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource         = _vm.TiposProducto,
            DisplayMemberPath   = "Nombre"
        };
        _cmbUnidadMedida = new ComboBox
        {
            Header              = "Unidad de medida",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource         = _vm.UnidadesMedida,
            DisplayMemberPath   = "Nombre"
        };
        form.Children.Add(TwoColRow(_cmbTipoProducto, _cmbUnidadMedida));

        // ── Categorías (chips + flyout) ────────────────────────────────────────
        form.Children.Add(BuildCategorySection());

        // ── Impuesto ──────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Impuesto"));
        _cmbTipoImpuesto = new ComboBox
        {
            Header              = "Tipo de impuesto",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource         = _vm.TiposImpuesto,
            DisplayMemberPath   = "Nombre"
        };
        _chkIvaAplicable = new CheckBox
        {
            Content = "IVA aplicable",
            Margin  = new Thickness(0, 20, 0, 0)
        };
        form.Children.Add(TwoColRow(_cmbTipoImpuesto, _chkIvaAplicable));

        // ── Inventario ────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Inventario"));
        _nbStockMinimo = new NumberBox
        {
            Header                   = "Stock mínimo",
            PlaceholderText          = "0",
            Minimum                  = 0,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment      = HorizontalAlignment.Stretch
        };
        _nbStockMaximo = new NumberBox
        {
            Header                   = "Stock máximo",
            PlaceholderText          = "0",
            Minimum                  = 0,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment      = HorizontalAlignment.Stretch
        };
        form.Children.Add(TwoColRow(_nbStockMinimo, _nbStockMaximo));

        // ── Estado ────────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Estado"));
        _chkActivo = new CheckBox { Content = "Activo", IsChecked = true };
        form.Children.Add(_chkActivo);

        _txtError = new TextBlock
        {
            Foreground  = new SolidColorBrush(Colors.Red),
            TextWrapping = TextWrapping.Wrap,
            Margin      = new Thickness(0, 4, 0, 0)
        };
        form.Children.Add(_txtError);

        scroll.Content = form;
        root.Children.Add(scroll);
        Grid.SetRow(scroll, 1);

        // ── Botones ───────────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing             = 8,
            Padding             = new Thickness(20, 8, 20, 16)
        };

        var btnCancelar = new Button { Content = "Cancelar" };
        btnCancelar.Click += (_, _) => Close();

        _btnGuardar = new Button
        {
            Content = "Guardar",
            Style   = XamlApp.Current.Resources["AccentButtonStyle"] as Style
        };
        _btnGuardar.Click += BtnGuardar_Click;

        btnRow.Children.Add(btnCancelar);
        btnRow.Children.Add(_btnGuardar);
        root.Children.Add(btnRow);
        Grid.SetRow(btnRow, 2);

        return root;
    }

    // ── Category section ──────────────────────────────────────────────────────

    private UIElement BuildCategorySection()
    {
        var section = new StackPanel { Spacing = 6 };

        // Label — misma apariencia visual que el Header de un ComboBox
        section.Children.Add(new TextBlock
        {
            Text     = "Categorías",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x5C, 0x5C, 0x5C))
        });

        // Chips row: scrollable horizontalmente, min-height para que el área sea visible
        _chipsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 6
        };

        var chipsScroll = new ScrollViewer
        {
            Content                   = _chipsPanel,
            HorizontalScrollMode      = ScrollMode.Auto,
            VerticalScrollMode        = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
            MinHeight                 = 32
        };
        section.Children.Add(chipsScroll);

        // Botón "Agregar" con Flyout inline
        var btnAgregar = new Button
        {
            Content = "+ Agregar categoría",
            Padding = new Thickness(8, 4, 8, 4),
            Flyout  = BuildCategoryFlyout()
        };
        section.Children.Add(btnAgregar);

        return section;
    }

    private Flyout BuildCategoryFlyout()
    {
        var cbPanel = new StackPanel { Spacing = 4 };

        foreach (var cat in _vm.Categorias)
        {
            var cb = new CheckBox
            {
                Content = cat.Nombre,
                Tag     = cat
            };

            cb.Checked += (_, _) =>
            {
                if (_suppressCheckboxEvents) return;
                if (!_categoriasSeleccionadas.Any(c => c.Id == cat.Id))
                {
                    _categoriasSeleccionadas.Add(cat);
                    RebuildChips();
                }
            };

            cb.Unchecked += (_, _) =>
            {
                if (_suppressCheckboxEvents) return;
                _categoriasSeleccionadas.RemoveAll(c => c.Id == cat.Id);
                RebuildChips();
            };

            _categoriaCheckBoxes[cat.Id] = cb;
            cbPanel.Children.Add(cb);
        }

        var scroll = new ScrollViewer
        {
            Content                     = cbPanel,
            MaxHeight                   = 260,
            VerticalScrollMode          = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var panel = new StackPanel { Spacing = 4, MinWidth = 200 };
        panel.Children.Add(new TextBlock
        {
            Text       = "Seleccionar categorías",
            FontSize   = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x5C, 0x5C, 0x5C)),
            Margin     = new Thickness(0, 0, 0, 6)
        });
        panel.Children.Add(scroll);

        return new Flyout { Content = panel };
    }

    private void RebuildChips()
    {
        _chipsPanel.Children.Clear();
        foreach (var cat in _categoriasSeleccionadas)
            _chipsPanel.Children.Add(CreateChip(cat));
    }

    private UIElement CreateChip(CategoriaProductoDto cat)
    {
        var stack = new StackPanel
        {
            Orientation     = Orientation.Horizontal,
            Spacing         = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text              = cat.Nombre,
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnX = new Button
        {
            Content         = new FontIcon { Glyph = "", FontSize = 10 },
            Background      = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding         = new Thickness(2, 0, 2, 0),
            MinWidth        = 0,
            Height          = 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnX.Click += (_, _) =>
        {
            _categoriasSeleccionadas.RemoveAll(c => c.Id == cat.Id);

            _suppressCheckboxEvents = true;
            if (_categoriaCheckBoxes.TryGetValue(cat.Id, out var cb))
                cb.IsChecked = false;
            _suppressCheckboxEvents = false;

            RebuildChips();
        };

        stack.Children.Add(btnX);

        return new Border
        {
            Background      = new SolidColorBrush(Color.FromArgb(0xFF, 0xEB, 0xF3, 0xFB)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(0xFF, 0xC8, 0xDD, 0xF4)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(8, 4, 4, 4),
            Child           = stack
        };
    }

    // ── Populate (edición) ────────────────────────────────────────────────────

    private void PopulateForm(ProductoDto p)
    {
        _txtCodigo.Text        = p.Codigo;
        _txtCodigoBarras.Text  = p.CodigoBarras ?? string.Empty;
        _txtNombre.Text        = p.Nombre;
        _txtDescripcion.Text   = p.Descripcion ?? string.Empty;
        _nbPrecio.Value        = (double)p.Precio;
        _nbPrecioMinimo.Value  = p.PrecioMinimo.HasValue ? (double)p.PrecioMinimo.Value : double.NaN;
        _nbCosto.Value         = p.Costo.HasValue ? (double)p.Costo.Value : double.NaN;
        _chkIvaAplicable.IsChecked = p.IvaAplicable;
        _chkActivo.IsChecked       = p.Activo;
        _nbStockMinimo.Value   = p.StockMinimo.HasValue ? (double)p.StockMinimo.Value : double.NaN;
        _nbStockMaximo.Value   = p.StockMaximo.HasValue ? (double)p.StockMaximo.Value : double.NaN;

        foreach (var item in _vm.TiposProducto)
            if (item.Id == p.TipoProductoId) { _cmbTipoProducto.SelectedItem = item; break; }

        foreach (var item in _vm.UnidadesMedida)
            if (item.Id == p.UnidadMedidaId) { _cmbUnidadMedida.SelectedItem = item; break; }

        foreach (var item in _vm.TiposImpuesto)
            if (item.Id == p.TipoImpuestoId) { _cmbTipoImpuesto.SelectedItem = item; break; }

        // Cargar categorías múltiples desde CategoriaIds (relación N:N)
        _suppressCheckboxEvents = true;
        foreach (var id in p.CategoriaIds)
        {
            var cat = _vm.Categorias.FirstOrDefault(c => c.Id == id);
            if (cat is not null && !_categoriasSeleccionadas.Any(c => c.Id == id))
            {
                _categoriasSeleccionadas.Add(cat);
                if (_categoriaCheckBoxes.TryGetValue(id, out var cb))
                    cb.IsChecked = true;
            }
        }
        _suppressCheckboxEvents = false;
        RebuildChips();
    }

    // ── Guardar ───────────────────────────────────────────────────────────────

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        _txtError.Text = string.Empty;

        var codigo = _txtCodigo.Text.Trim();
        var nombre = _txtNombre.Text.Trim();

        if (string.IsNullOrWhiteSpace(codigo)) { _txtError.Text = "El código es obligatorio."; return; }
        if (string.IsNullOrWhiteSpace(nombre)) { _txtError.Text = "El nombre es obligatorio."; return; }
        if (double.IsNaN(_nbPrecio.Value))      { _txtError.Text = "El precio es obligatorio."; return; }

        _btnGuardar.IsEnabled = false;

        try
        {
            var precio       = (decimal)_nbPrecio.Value;
            var precioMin    = double.IsNaN(_nbPrecioMinimo.Value) ? (decimal?)null : (decimal)_nbPrecioMinimo.Value;
            var costo        = double.IsNaN(_nbCosto.Value)        ? (decimal?)null : (decimal)_nbCosto.Value;
            var stockMin     = double.IsNaN(_nbStockMinimo.Value)  ? (decimal?)null : (decimal)_nbStockMinimo.Value;
            var stockMax     = double.IsNaN(_nbStockMaximo.Value)  ? (decimal?)null : (decimal)_nbStockMaximo.Value;
            var codigoBarras = string.IsNullOrWhiteSpace(_txtCodigoBarras.Text) ? null : _txtCodigoBarras.Text.Trim();
            var descripcion  = string.IsNullOrWhiteSpace(_txtDescripcion.Text)  ? null : _txtDescripcion.Text.Trim();
            var tipoProductoId = (_cmbTipoProducto.SelectedItem as TipoProductoDto)?.Id;
            var unidadMedidaId = (_cmbUnidadMedida.SelectedItem as UnidadMedidaDto)?.Id;
            var tipoImpuestoId = (_cmbTipoImpuesto.SelectedItem as TipoImpuestoDto)?.Id;
            var ivaAplicable = _chkIvaAplicable.IsChecked ?? false;
            var activo       = _chkActivo.IsChecked ?? true;

            // La primera categoría seleccionada se marca como principal en el DTO
            var categoriaId  = _categoriasSeleccionadas.Count > 0 ? _categoriasSeleccionadas[0].Id : (int?)null;
            var categoriaIds = (IReadOnlyList<int>)_categoriasSeleccionadas.Select(c => c.Id).ToList();

            if (_original is null)
            {
                var dto = new CrearProductoDto(
                    EmpresaId:      _vm.EmpresaId,
                    Codigo:         codigo,
                    CodigoBarras:   codigoBarras,
                    Nombre:         nombre,
                    Descripcion:    descripcion,
                    Precio:         precio,
                    PrecioMinimo:   precioMin,
                    Costo:          costo,
                    IvaAplicable:   ivaAplicable,
                    TipoImpuestoId: tipoImpuestoId,
                    CategoriaId:    categoriaId,
                    TipoProductoId: tipoProductoId,
                    UnidadMedidaId: unidadMedidaId,
                    StockMinimo:    stockMin,
                    StockMaximo:    stockMax,
                    ProveedorId:    null,
                    Activo:         activo);

                await _vm.CrearDesdeVentanaAsync(dto, categoriaIds);
            }
            else
            {
                var dto = new ActualizarProductoDto(
                    Codigo:         codigo,
                    CodigoBarras:   codigoBarras,
                    Nombre:         nombre,
                    Descripcion:    descripcion,
                    Precio:         precio,
                    PrecioMinimo:   precioMin,
                    Costo:          costo,
                    IvaAplicable:   ivaAplicable,
                    TipoImpuestoId: tipoImpuestoId,
                    CategoriaId:    categoriaId,
                    TipoProductoId: tipoProductoId,
                    UnidadMedidaId: unidadMedidaId,
                    StockMinimo:    stockMin,
                    StockMaximo:    stockMax,
                    ProveedorId:    null,
                    Activo:         activo);

                await _vm.ActualizarDesdeVentanaAsync(_original.Id, dto, categoriaIds);
            }

            if (!string.IsNullOrEmpty(_vm.ErrorMessage))
                _txtError.Text = _vm.ErrorMessage;
            else
                Close();
        }
        finally
        {
            _btnGuardar.IsEnabled = true;
        }
    }

    // ── Helpers de layout ────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string title) => new()
    {
        Text   = title,
        Style  = XamlApp.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        Margin = new Thickness(0, 8, 0, 0)
    };

    // FrameworkElement (no UIElement) para que Grid.SetColumn compile en WinUI 3
    private static Grid TwoColRow(FrameworkElement left, FrameworkElement right)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(left);  Grid.SetColumn(left, 0);
        g.Children.Add(right); Grid.SetColumn(right, 1);
        return g;
    }

    private static Grid ThreeColRow(FrameworkElement a, FrameworkElement b, FrameworkElement c)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(a); Grid.SetColumn(a, 0);
        g.Children.Add(b); Grid.SetColumn(b, 1);
        g.Children.Add(c); Grid.SetColumn(c, 2);
        return g;
    }
}
