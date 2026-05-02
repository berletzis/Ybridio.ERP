using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Inventario;
using Windows.Graphics;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Inventario;

public sealed class ProductoDetailWindow : Window
{
    private readonly ProductosViewModel _vm;
    private readonly ProductoDto? _original;

    private TextBox _txtCodigo = null!;
    private TextBox _txtCodigoBarras = null!;
    private TextBox _txtNombre = null!;
    private TextBox _txtDescripcion = null!;
    private NumberBox _nbPrecio = null!;
    private NumberBox _nbPrecioMinimo = null!;
    private NumberBox _nbCosto = null!;
    private ComboBox _cmbCategoria = null!;
    private ComboBox _cmbTipoProducto = null!;
    private ComboBox _cmbUnidadMedida = null!;
    private ComboBox _cmbTipoImpuesto = null!;
    private CheckBox _chkIvaAplicable = null!;
    private NumberBox _nbStockMinimo = null!;
    private NumberBox _nbStockMaximo = null!;
    private CheckBox _chkActivo = null!;
    private Button _btnGuardar = null!;
    private TextBlock _txtError = null!;

    public ProductoDetailWindow(ProductosViewModel vm, ProductoDto? producto)
    {
        _vm = vm;
        _original = producto;

        Title = producto is null ? "Nuevo producto" : $"Editar: {producto.Nombre}";
        AppWindow.Resize(new SizeInt32(900, 700));

        try
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            AppWindow.SetOwnerWindowId(mainWindow.AppWindow.Id);

            var mainPos  = mainWindow.AppWindow.Position;
            var mainSize = mainWindow.AppWindow.Size;
            var thisSize = AppWindow.Size;
            AppWindow.Move(new Windows.Graphics.PointInt32(
                mainPos.X + (mainSize.Width  - thisSize.Width)  / 2,
                mainPos.Y + (mainSize.Height - thisSize.Height) / 2));
        }
        catch { }

        Content = BuildUI();

        if (producto is not null)
            PopulateForm(producto);
    }

    private UIElement BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new TextBlock
        {
            Text = _original is null ? "Nuevo producto" : "Editar producto",
            Style = XamlApp.Current.Resources["SubtitleTextBlockStyle"] as Style,
            Margin = new Thickness(20, 16, 20, 8)
        };
        root.Children.Add(header);
        Grid.SetRow(header, 0);

        // Formulario scrollable
        var scroll = new ScrollViewer { Padding = new Thickness(20, 0, 20, 8) };
        var form = new StackPanel { Spacing = 16 };

        // ── Identificación ────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Identificación"));
        _txtCodigo = new TextBox { Header = "Código *", PlaceholderText = "Ej: PROD-001" };
        _txtCodigoBarras = new TextBox { Header = "Código de barras", PlaceholderText = "Opcional" };
        _txtNombre = new TextBox { Header = "Nombre *", PlaceholderText = "Nombre del producto" };
        _txtDescripcion = new TextBox
        {
            Header = "Descripción",
            PlaceholderText = "Opcional",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60
        };
        form.Children.Add(TwoColRow(_txtCodigo, _txtCodigoBarras));
        form.Children.Add(TwoColRow(_txtNombre, _txtDescripcion));

        // ── Precios ───────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Precios"));
        _nbPrecio = new NumberBox
        {
            Header = "Precio *",
            PlaceholderText = "0.00",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _nbPrecioMinimo = new NumberBox
        {
            Header = "Precio mínimo",
            PlaceholderText = "0.00",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _nbCosto = new NumberBox
        {
            Header = "Costo",
            PlaceholderText = "0.00",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        form.Children.Add(ThreeColRow(_nbPrecio, _nbPrecioMinimo, _nbCosto));

        // ── Clasificación ─────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Clasificación"));
        _cmbCategoria = new ComboBox
        {
            Header = "Categoría",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _vm.Categorias,
            DisplayMemberPath = "Nombre"
        };
        _cmbTipoProducto = new ComboBox
        {
            Header = "Tipo de producto",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _vm.TiposProducto,
            DisplayMemberPath = "Nombre"
        };
        _cmbUnidadMedida = new ComboBox
        {
            Header = "Unidad de medida",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _vm.UnidadesMedida,
            DisplayMemberPath = "Nombre"
        };
        form.Children.Add(ThreeColRow(_cmbCategoria, _cmbTipoProducto, _cmbUnidadMedida));

        // ── Impuesto ──────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Impuesto"));
        _cmbTipoImpuesto = new ComboBox
        {
            Header = "Tipo de impuesto",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _vm.TiposImpuesto,
            DisplayMemberPath = "Nombre"
        };
        _chkIvaAplicable = new CheckBox
        {
            Content = "IVA aplicable",
            Margin = new Thickness(0, 20, 0, 0)
        };
        form.Children.Add(TwoColRow(_cmbTipoImpuesto, _chkIvaAplicable));

        // ── Inventario ────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Inventario"));
        _nbStockMinimo = new NumberBox
        {
            Header = "Stock mínimo",
            PlaceholderText = "0",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _nbStockMaximo = new NumberBox
        {
            Header = "Stock máximo",
            PlaceholderText = "0",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        form.Children.Add(TwoColRow(_nbStockMinimo, _nbStockMaximo));

        // ── Estado ────────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Estado"));
        _chkActivo = new CheckBox { Content = "Activo", IsChecked = true };
        form.Children.Add(_chkActivo);

        _txtError = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Red),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        form.Children.Add(_txtError);

        scroll.Content = form;
        root.Children.Add(scroll);
        Grid.SetRow(scroll, 1);

        // ── Botones ───────────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Padding = new Thickness(20, 8, 20, 16)
        };

        var btnCancelar = new Button { Content = "Cancelar" };
        btnCancelar.Click += (_, _) => Close();

        _btnGuardar = new Button
        {
            Content = "Guardar",
            Style = XamlApp.Current.Resources["AccentButtonStyle"] as Style
        };
        _btnGuardar.Click += BtnGuardar_Click;

        btnRow.Children.Add(btnCancelar);
        btnRow.Children.Add(_btnGuardar);
        root.Children.Add(btnRow);
        Grid.SetRow(btnRow, 2);

        return root;
    }

    private void PopulateForm(ProductoDto p)
    {
        _txtCodigo.Text = p.Codigo;
        _txtCodigoBarras.Text = p.CodigoBarras ?? string.Empty;
        _txtNombre.Text = p.Nombre;
        _txtDescripcion.Text = p.Descripcion ?? string.Empty;
        _nbPrecio.Value = (double)p.Precio;
        _nbPrecioMinimo.Value = p.PrecioMinimo.HasValue ? (double)p.PrecioMinimo.Value : double.NaN;
        _nbCosto.Value = p.Costo.HasValue ? (double)p.Costo.Value : double.NaN;
        _chkIvaAplicable.IsChecked = p.IvaAplicable;
        _chkActivo.IsChecked = p.Activo;
        _nbStockMinimo.Value = p.StockMinimo.HasValue ? (double)p.StockMinimo.Value : double.NaN;
        _nbStockMaximo.Value = p.StockMaximo.HasValue ? (double)p.StockMaximo.Value : double.NaN;

        foreach (var item in _vm.Categorias)
            if (item.Id == p.CategoriaId) { _cmbCategoria.SelectedItem = item; break; }

        foreach (var item in _vm.TiposProducto)
            if (item.Id == p.TipoProductoId) { _cmbTipoProducto.SelectedItem = item; break; }

        foreach (var item in _vm.UnidadesMedida)
            if (item.Id == p.UnidadMedidaId) { _cmbUnidadMedida.SelectedItem = item; break; }

        foreach (var item in _vm.TiposImpuesto)
            if (item.Id == p.TipoImpuestoId) { _cmbTipoImpuesto.SelectedItem = item; break; }
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        _txtError.Text = string.Empty;

        var codigo = _txtCodigo.Text.Trim();
        var nombre = _txtNombre.Text.Trim();

        if (string.IsNullOrWhiteSpace(codigo)) { _txtError.Text = "El código es obligatorio."; return; }
        if (string.IsNullOrWhiteSpace(nombre)) { _txtError.Text = "El nombre es obligatorio."; return; }
        if (double.IsNaN(_nbPrecio.Value)) { _txtError.Text = "El precio es obligatorio."; return; }

        _btnGuardar.IsEnabled = false;

        try
        {
            var precio = (decimal)_nbPrecio.Value;
            var precioMin = double.IsNaN(_nbPrecioMinimo.Value) ? (decimal?)null : (decimal)_nbPrecioMinimo.Value;
            var costo = double.IsNaN(_nbCosto.Value) ? (decimal?)null : (decimal)_nbCosto.Value;
            var stockMin = double.IsNaN(_nbStockMinimo.Value) ? (decimal?)null : (decimal)_nbStockMinimo.Value;
            var stockMax = double.IsNaN(_nbStockMaximo.Value) ? (decimal?)null : (decimal)_nbStockMaximo.Value;
            var codigoBarras = string.IsNullOrWhiteSpace(_txtCodigoBarras.Text) ? null : _txtCodigoBarras.Text.Trim();
            var descripcion = string.IsNullOrWhiteSpace(_txtDescripcion.Text) ? null : _txtDescripcion.Text.Trim();
            var categoriaId = (_cmbCategoria.SelectedItem as CategoriaProductoDto)?.Id;
            var tipoProductoId = (_cmbTipoProducto.SelectedItem as TipoProductoDto)?.Id;
            var unidadMedidaId = (_cmbUnidadMedida.SelectedItem as UnidadMedidaDto)?.Id;
            var tipoImpuestoId = (_cmbTipoImpuesto.SelectedItem as TipoImpuestoDto)?.Id;
            var ivaAplicable = _chkIvaAplicable.IsChecked ?? false;
            var activo = _chkActivo.IsChecked ?? true;

            if (_original is null)
            {
                var dto = new CrearProductoDto(
                    EmpresaId: _vm.EmpresaId,
                    Codigo: codigo,
                    CodigoBarras: codigoBarras,
                    Nombre: nombre,
                    Descripcion: descripcion,
                    Precio: precio,
                    PrecioMinimo: precioMin,
                    Costo: costo,
                    IvaAplicable: ivaAplicable,
                    TipoImpuestoId: tipoImpuestoId,
                    CategoriaId: categoriaId,
                    TipoProductoId: tipoProductoId,
                    UnidadMedidaId: unidadMedidaId,
                    StockMinimo: stockMin,
                    StockMaximo: stockMax,
                    ProveedorId: null,
                    Activo: activo);

                await _vm.CrearDesdeVentanaAsync(dto);
            }
            else
            {
                var dto = new ActualizarProductoDto(
                    Codigo: codigo,
                    CodigoBarras: codigoBarras,
                    Nombre: nombre,
                    Descripcion: descripcion,
                    Precio: precio,
                    PrecioMinimo: precioMin,
                    Costo: costo,
                    IvaAplicable: ivaAplicable,
                    TipoImpuestoId: tipoImpuestoId,
                    CategoriaId: categoriaId,
                    TipoProductoId: tipoProductoId,
                    UnidadMedidaId: unidadMedidaId,
                    StockMinimo: stockMin,
                    StockMaximo: stockMax,
                    ProveedorId: null,
                    Activo: activo);

                await _vm.ActualizarDesdeVentanaAsync(_original.Id, dto);
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
        Text = title,
        Style = XamlApp.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        Margin = new Thickness(0, 8, 0, 0)
    };

    // FrameworkElement (no UIElement) para que Grid.SetColumn compile en WinUI 3
    private static Grid TwoColRow(FrameworkElement left, FrameworkElement right)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(left);
        Grid.SetColumn(left, 0);
        g.Children.Add(right);
        Grid.SetColumn(right, 1);
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
