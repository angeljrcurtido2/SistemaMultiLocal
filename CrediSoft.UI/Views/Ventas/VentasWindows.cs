using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Ventas;

// ══════════════════════════════════════════════════════════════════════════════
//  VENTA AL CONTADO
// ══════════════════════════════════════════════════════════════════════════════

public class VentaContadoWindow : Window
{
    private readonly IClienteRepository _clienteRepo;
    private readonly IArticuloRepository _artRepo;
    private readonly IVentaRepository _ventaRepo;
    private readonly IDbConnectionFactory _db;

    private Cliente? _clienteActual;
    private readonly List<LineaDetalle> _carrito = new();
    private TextBox _txtLocalDisplay = null!;

    // controles
    private TextBox    _txtCodigo = null!, _txtNombreDesc = null!, _txtCantidad = null!;
    private TextBox    _txtPrecioContado = null!, _txtDescuento = null!;
    private TextBox    _txtEfectivo = null!, _txtCambio = null!, _txtNroTarjeta = null!;
    // campos legacy (se mantienen para que OnConfirmar compile sin cambios)
    private TextBox    _txtBuscarCliente = null!, _txtBuscarArticulo = null!, _txtTarjeta = null!;
    private TextBlock  _lblClienteNombre = null!, _lblClienteCI = null!, _lblTotal = null!;
    private DataGrid   _gridClientes = null!, _gridArticulos = null!, _gridDetalle = null!;
    private ComboBox   _cboMetodo = null!;
    private Button     _btnConfirmar = null!;

    // artículo seleccionado actualmente
    private ArticuloConPrecio? _artSeleccionado;

    // brushes
    private static readonly SolidColorBrush BrNaranja   = new(Color.FromRgb(255,140,  0));
    private static readonly SolidColorBrush BrNaranjaOsc= new(Color.FromRgb(200, 90,  0));
    private static readonly SolidColorBrush BrFondo     = new(Color.FromRgb(255,248,230));
    private static readonly SolidColorBrush BrBlanco    = new(Colors.White);
    private static readonly SolidColorBrush BrGrisText  = new(Color.FromRgb(107,114,128));
    private static readonly SolidColorBrush BrVerde     = new(Color.FromRgb( 22,163, 74));
    private static readonly SolidColorBrush BrGris      = new(Color.FromRgb(107,114,128));
    private static readonly SolidColorBrush BrFooter    = new(Color.FromRgb( 50, 50, 50));
    private static readonly SolidColorBrush BrGridHdr   = new(Color.FromRgb(255,140,  0));

    public VentaContadoWindow()
    {
        var svc = App.Services;
        _clienteRepo = svc.GetRequiredService<IClienteRepository>();
        _artRepo     = svc.GetRequiredService<IArticuloRepository>();
        _ventaRepo   = svc.GetRequiredService<IVentaRepository>();
        _db          = svc.GetRequiredService<IDbConnectionFactory>();

        Title = "Ventas contado";
        Width = 900; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrBlanco;
        BuildUI();
    }

    private void BuildUI()
    {
        // ── helpers ───────────────────────────────────────────────────────────
        TextBox TB(string? def = null, double w = double.NaN) => new TextBox {
            Text = def ?? "", Height = 28, Padding = new Thickness(6,2,6,2),
            FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center,
            Background = Colors.White is Color ? new SolidColorBrush(Colors.White) : BrBlanco,
            BorderBrush = new SolidColorBrush(Color.FromRgb(200,200,200)),
            BorderThickness = new Thickness(1),
            Width = double.IsNaN(w) ? double.NaN : w
        };
        StackPanel LF(string lbl, UIElement ctrl, double minW = 0) {
            var sp = new StackPanel { Margin = new Thickness(0,0,10,0), MinWidth = minW };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(90,40,0)), Margin = new Thickness(0,0,0,2) });
            sp.Children.Add(ctrl);
            return sp;
        }

        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(245,245,245)) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // topBar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer

        // ── FILA 1: formulario de ingreso ────────────────────────────────────
        var topBar = new Border {
            Background = new SolidColorBrush(Color.FromRgb(255,200,80)),
            BorderBrush  = new SolidColorBrush(Color.FromRgb(220,140,0)),
            BorderThickness = new Thickness(0,0,0,2),
            Padding = new Thickness(10,8,10,8)
        };
        var topWrap = new WrapPanel { Orientation = Orientation.Horizontal };

        // Local + botón búsqueda
        var session = SessionService.Instance;
        _txtLocalDisplay = TB(session.LocalActual?.NombreLocal ?? "—", 148);
        _txtLocalDisplay.IsReadOnly = true;
        _txtLocalDisplay.Background = new SolidColorBrush(Color.FromRgb(255,245,210));
        var btnBuscarLocal = new Button {
            Content = "Buscar local", Height = 28, Padding = new Thickness(10,0,10,0),
            FontSize = 11, FontWeight = FontWeights.Bold,
            Background = BrNaranja, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4,0,0,0)
        };
        btnBuscarLocal.Click += async (_, _) => await AbrirBuscadorLocal();
        var localRow = new StackPanel { Orientation = Orientation.Horizontal };
        localRow.Children.Add(_txtLocalDisplay);
        localRow.Children.Add(btnBuscarLocal);
        topWrap.Children.Add(LF("Local", localRow));

        // Código + botón buscar artículo
        _txtCodigo = TB("", 100);
        _txtBuscarArticulo = _txtCodigo;
        _txtCodigo.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarPorCodigoAsync(); };
        var btnBuscarArt = new Button {
            Content = "Buscar artículo", Height = 28, Padding = new Thickness(10,0,10,0),
            FontSize = 11, FontWeight = FontWeights.Bold,
            Background = BrNaranja, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4,0,0,0)
        };
        btnBuscarArt.Click += async (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtCodigo.Text))
                // sin texto: abrir selector con todos los artículos
                await AbrirSelectorArticuloVacio();
            else
                await BuscarPorCodigoAsync();
        };
        var codRow = new StackPanel { Orientation = Orientation.Horizontal };
        codRow.Children.Add(_txtCodigo);
        codRow.Children.Add(btnBuscarArt);
        topWrap.Children.Add(LF("Código", codRow));

        // Nombre / descripción
        _txtNombreDesc = TB("", 260);
        _txtNombreDesc.IsReadOnly = true;
        _txtNombreDesc.Background = new SolidColorBrush(Color.FromRgb(255,245,210));
        topWrap.Children.Add(LF("Nombre o descripción", _txtNombreDesc));

        // Cantidad
        _txtCantidad = TB("", 70);
        _txtCantidad.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        topWrap.Children.Add(LF("Cantidad", _txtCantidad));

        // Precio contado-contado
        _txtPrecioContado = TB("", 120);
        _txtPrecioContado.IsReadOnly = true;
        _txtPrecioContado.Background = new SolidColorBrush(Color.FromRgb(255,245,210));
        topWrap.Children.Add(LF("Precio contado-contado", _txtPrecioContado));

        // Cliente: campo readonly + botón naranja
        _lblClienteNombre = new TextBlock(); // legacy stub, no se usa visualmente
        _lblClienteCI     = new TextBlock();
        _txtBuscarCliente = TB("— sin cliente —", 200);
        _txtBuscarCliente.IsReadOnly = true;
        _txtBuscarCliente.Background = new SolidColorBrush(Color.FromRgb(255,245,210));
        var btnBuscarCliente = new Button {
            Content = "Buscar cliente", Height = 28, Padding = new Thickness(10,0,10,0),
            FontSize = 11, FontWeight = FontWeights.Bold,
            Background = BrNaranja, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4,0,0,0)
        };
        btnBuscarCliente.Click += (_, _) => AbrirBuscadorCliente();
        var clienteRow = new StackPanel { Orientation = Orientation.Horizontal };
        clienteRow.Children.Add(_txtBuscarCliente);
        clienteRow.Children.Add(btnBuscarCliente);
        topWrap.Children.Add(LF("Cliente", clienteRow));

        // Botón Ingresar
        var btnIngresar = new Button {
            Content = "✔ Ingresar", Height = 28, Padding = new Thickness(16,0,16,0),
            Background = BrNaranja, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(0,18,0,0)
        };
        btnIngresar.Click += async (_, _) => await AgregarArticuloAsync();
        topWrap.Children.Add(btnIngresar);

        topBar.Child = topWrap;
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        // ── FOOTER ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = new SolidColorBrush(Color.FromRgb(230,230,230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200,200,200)),
            BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14,10,14,10)
        };
        var fGrid = new Grid();
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var darkText  = new SolidColorBrush(Color.FromRgb(50,50,50));
        var labelText = new SolidColorBrush(Color.FromRgb(90,90,90));
        var boxBorder = new SolidColorBrush(Color.FromRgb(180,180,180));
        var boxBg     = new SolidColorBrush(Color.FromRgb(245,245,245));

        // Total
        _lblTotal = new TextBlock {
            Text = "Total: 0 Gs.", FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = BrNaranja, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,20,0)
        };
        Grid.SetColumn(_lblTotal, 0); fGrid.Children.Add(_lblTotal);

        // Caja pago en efectivo
        var caja = new Border {
            Background = boxBg, BorderBrush = boxBorder,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,14,0)
        };
        var cajaSp = new StackPanel();
        cajaSp.Children.Add(new TextBlock { Text = "Pago en efectivo",
            FontSize = 10, FontWeight = FontWeights.Bold, Foreground = labelText,
            Margin = new Thickness(0,0,0,6) });

        _txtEfectivo = new TextBox { Width = 120, Height = 26, TextAlignment = TextAlignment.Right,
            Padding = new Thickness(6,2,6,2), FontSize = 12, Text = "0",
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtCambio = new TextBox { Width = 120, Height = 26, TextAlignment = TextAlignment.Right,
            Padding = new Thickness(6,2,6,2), FontSize = 12, Text = "0", IsReadOnly = true,
            Background = new SolidColorBrush(Color.FromRgb(235,235,235)),
            Foreground = darkText, VerticalContentAlignment = VerticalAlignment.Center };
        _txtTarjeta = new TextBox();

        // solo dígitos, con separador de miles en tiempo real
        bool _efFormatting = false;
        _txtEfectivo.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        _txtEfectivo.TextChanged += (_, _) => {
            if (_efFormatting) return;
            _efFormatting = true;
            var digits = new string(_txtEfectivo.Text.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digits, out var v)) {
                var formatted = v == 0 ? "0" : v.ToString("N0").Replace(",",".");
                var caret = _txtEfectivo.CaretIndex;
                var oldLen = _txtEfectivo.Text.Length;
                _txtEfectivo.Text = formatted;
                // mantener cursor al final si se está escribiendo
                _txtEfectivo.CaretIndex = formatted.Length;
            }
            _efFormatting = false;
            ActualizarTotal();
            if (decimal.TryParse(new string(_txtEfectivo.Text.Where(char.IsDigit).ToArray()), out var ent))
                _txtCambio.Text = (ent - _carrito.Sum(x => x.Subtotal)).ToString("N0").Replace(",",".");
        };

        TextBlock AddRow(StackPanel p, string lbl, UIElement ctrl) {
            var r = new Grid { Margin = new Thickness(0,3,0,0) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tb = new TextBlock { Text = lbl, Foreground = darkText, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, 0); r.Children.Add(tb);
            Grid.SetColumn(ctrl, 1); r.Children.Add(ctrl);
            p.Children.Add(r);
            return tb;
        }
        var lblEfectivoRow = AddRow(cajaSp, "Efectivo", _txtEfectivo);
        AddRow(cajaSp, "Cambio",   _txtCambio);
        caja.Child = cajaSp;
        Grid.SetColumn(caja, 1); fGrid.Children.Add(caja);

        // Método + Número (Número solo visible cuando no es EFECTIVO)
        var metBorder = new Border {
            Background = boxBg, BorderBrush = boxBorder,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,14,0)
        };
        var metSp = new StackPanel();
        _cboMetodo = new ComboBox { Width = 150, Height = 26, Margin = new Thickness(0,0,0,6) };
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "EFECTIVO",        Tag = (byte)1, IsSelected = true });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta débito",  Tag = (byte)2 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta crédito", Tag = (byte)3 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia",   Tag = (byte)4 });
        _cboMetodo.SelectedIndex = 0;
        _txtNroTarjeta = new TextBox { Width = 150, Height = 26, Padding = new Thickness(6,2,6,2), FontSize = 11 };
        var lblNumero = new TextBlock { Text = "N° comprobante", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = labelText, Margin = new Thickness(0,6,0,4),
            Visibility = Visibility.Collapsed };
        _txtNroTarjeta.Visibility = Visibility.Collapsed;

        // mostrar/ocultar Número según método y ajustar labels descriptivos
        _cboMetodo.SelectionChanged += (_, _) => {
            var met = _cboMetodo.SelectedItem is ComboBoxItem ci ? (byte)(ci.Tag ?? (byte)1) : (byte)1;
            var esEfectivo = met == 1;
            lblNumero.Visibility      = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            _txtNroTarjeta.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            lblEfectivoRow.Text = esEfectivo ? "Efectivo" : "Monto entregado";
            lblNumero.Text = met switch {
                2 => "N° voucher débito",
                3 => "N° voucher crédito",
                4 => "N° transferencia",
                _ => "N° comprobante"
            };
            if (esEfectivo) _txtNroTarjeta.Text = "";
        };

        metSp.Children.Add(new TextBlock { Text = "Método", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = labelText, Margin = new Thickness(0,0,0,4) });
        metSp.Children.Add(_cboMetodo);
        metSp.Children.Add(lblNumero);
        metSp.Children.Add(_txtNroTarjeta);
        metBorder.Child = metSp;
        Grid.SetColumn(metBorder, 2); fGrid.Children.Add(metBorder);

        // Atajos
        var atajSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };
        foreach (var t in new[] {"F1: Ayuda local","F2: Ayuda código","F5: Guardar",
                                  "F9: Cancelar","Ctrl+S: Cerrar","Ctrl+P: Imprimir"})
            atajSp.Children.Add(new TextBlock { Text = t, FontSize = 9, Foreground = labelText });
        Grid.SetColumn(atajSp, 3); fGrid.Children.Add(atajSp);

        // Botones
        _btnConfirmar = new Button {
            Content = "Guardar", Height = 46, Width = 90,
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0,0,8,0), IsEnabled = false
        };
        _btnConfirmar.Click += OnConfirmar;
        var btnCerrar = new Button {
            Content = "Cerrar", Height = 46, Width = 80,
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnCerrar.Click += (_, _) => Close();
        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        btnsSp.Children.Add(_btnConfirmar); btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 4); fGrid.Children.Add(btnsSp);

        footer.Child = fGrid;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        // ── GRID DETALLE ─────────────────────────────────────────────────────
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            Background = BrBlanco, RowBackground = BrBlanco,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250,250,255)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(220,220,230)),
            CanUserResizeRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            FontSize = 13, BorderThickness = new Thickness(0), RowHeight = 36,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
        };
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,9,10,9)));
        hdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(220,120,0))));
        hdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));
        _gridDetalle.ColumnHeaderStyle = hdrStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,0,10,0)));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        _gridDetalle.CellStyle = cellStyle;

        Style rStyle() { var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0,0,8,0))); return s; }
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("ArticuloCodigo"), Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("ArticuloNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",       Binding = new System.Windows.Data.Binding("Cantidad"),        Width = 60,  ElementStyle = rStyle() });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. venta",    Binding = new System.Windows.Data.Binding("PvStr"),           Width = 110, ElementStyle = rStyle() });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("SubtotalStr"),     Width = 120, ElementStyle = rStyle() });

        var btnQCol = new DataGridTemplateColumn { Header = "", Width = 30 };
        var cf = new FrameworkElementFactory(typeof(Button));
        cf.SetValue(Button.ContentProperty, "✕");
        cf.SetValue(Button.BackgroundProperty, Brushes.Transparent);
        cf.SetValue(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(220,50,50)));
        cf.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        cf.SetValue(Button.CursorProperty, Cursors.Hand);
        cf.SetValue(Button.FontSizeProperty, 11.0);
        cf.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnQuitarArticulo));
        btnQCol.CellTemplate = new DataTemplate { VisualTree = cf };
        _gridDetalle.Columns.Add(btnQCol);

        _gridDetalle.KeyDown += (_, e) => {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && _gridDetalle.SelectedItem is LineaDetalle ld)
                { _carrito.Remove(ld); RefrescarCarrito(); }
        };

        // campos legacy ocultos
        _gridClientes  = new DataGrid { Visibility = Visibility.Collapsed };
        _gridArticulos = new DataGrid { Visibility = Visibility.Collapsed };
        _txtDescuento  = new TextBox { Text = "0" };

        var gridBorder = new Border { Child = _gridDetalle, Background = BrBlanco };
        Grid.SetRow(gridBorder, 1);
        root.Children.Add(gridBorder);
        Content = root;
    }

    // ── Búsqueda por código (Enter en campo Código) ─────────────────────────
    private async Task BuscarPorCodigoAsync()
    {
        var term = _txtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(term)) return;
        var session = SessionService.Instance;
        var resultados = await _artRepo.BuscarAsync(term);
        var lista = resultados.ToList();
        if (lista.Count == 0) { _txtNombreDesc.Text = "— no encontrado —"; return; }

        ArticuloConPrecio Pick(Core.Models.Articulo a) {
            return new ArticuloConPrecio { Id = a.Id, Ca = a.Ca, D = a.D, MarcaNombre = a.MarcaNombre, PventaLocal = 0 };
        }

        if (lista.Count == 1) {
            var a = lista[0];
            var precio = session.LocalActual != null
                ? await _artRepo.ObtenerPrecioLocalAsync(a.Id, session.LocalActual.IdLocal) : null;
            _artSeleccionado = new ArticuloConPrecio { Id = a.Id, Ca = a.Ca, D = a.D,
                MarcaNombre = a.MarcaNombre, PventaLocal = precio?.Pventa ?? 0 };
            _txtCodigo.Text        = a.Ca;
            _txtNombreDesc.Text    = a.D;
            _txtPrecioContado.Text = (_artSeleccionado.PventaLocal).ToString("N0").Replace(",",".");
            _txtCantidad.Focus(); _txtCantidad.SelectAll();
        } else {
            // múltiples resultados: abrir mini buscador
            AbrirSelectorArticulo(lista);
        }
    }

    private async Task AbrirSelectorArticuloVacio()
    {
        // carga TOP 200 para el browse inicial
        var session = SessionService.Instance;
        List<Core.Models.Articulo> lista;
        try {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT TOP 200 ID, CA, D FROM ARTICULOS WHERE ES=1 ORDER BY D");
            lista = rows.Select(r => {
                var d = (IDictionary<string,object>)r;
                return new Core.Models.Articulo {
                    Id = d.TryGetValue("ID", out var v1) ? Convert.ToInt32(v1) : 0,
                    Ca = d.TryGetValue("CA", out var v2) ? v2?.ToString() ?? "" : "",
                    D  = d.TryGetValue("D",  out var v3) ? v3?.ToString() ?? "" : ""
                };
            }).ToList();
        } catch (Exception ex) {
            MessageBox.Show($"Error cargando artículos: {ex.Message}"); return;
        }
        AbrirSelectorArticulo(lista);
    }

    private void AbrirSelectorArticulo(List<Core.Models.Articulo> lista)
    {
        var session = SessionService.Instance;
        var dlg = new Window { Title = "Buscar artículo", Width = 560, Height = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco };
        var dp = new DockPanel();

        // header
        var hdr = new Border { Background = BrNaranja, Padding = new Thickness(12,8,12,8) };
        hdr.Child = new TextBlock { Text = "Buscar artículo", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        // footer
        var ftrBorder = new Border {
            Padding = new Thickness(10,8,10,8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,1,0,0)
        };
        var btnSel = new Button { Content = "Seleccionar", Height = 30, Width = 110,
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        var btnCan = new Button { Content = "Cancelar", Height = 30, Width = 80,
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, Margin = new Thickness(6,0,0,0) };
        btnCan.Click += (_, _) => dlg.Close();
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSel); ftrSp.Children.Add(btnCan);
        ftrBorder.Child = ftrSp;
        DockPanel.SetDock(ftrBorder, Dock.Bottom); dp.Children.Add(ftrBorder);

        // barra de filtro
        var filtroBar = new Border {
            Padding = new Thickness(10,8,10,8),
            Background = new SolidColorBrush(Color.FromRgb(250,250,250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,0,0,1)
        };
        var filtroDp = new DockPanel();
        var txtFiltro = new TextBox { Height = 28, Padding = new Thickness(8,2,8,2),
            FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center };
        var btnFiltro = new Button { Content = "Buscar", Height = 28, Width = 70,
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, Margin = new Thickness(6,0,0,0) };
        DockPanel.SetDock(btnFiltro, Dock.Right);
        filtroDp.Children.Add(btnFiltro); filtroDp.Children.Add(txtFiltro);
        filtroBar.Child = filtroDp;
        DockPanel.SetDock(filtroBar, Dock.Top); dp.Children.Add(filtroBar);

        // grid
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));

        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255,250,240)),
            BorderThickness = new Thickness(0), ColumnHeaderStyle = hdrStyle };
        grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Ca"), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("D"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.ItemsSource = lista;
        dp.Children.Add(grid);
        dlg.Content = dp;

        // filtro en tiempo real
        void Filtrar() {
            var f = txtFiltro.Text.Trim().ToUpperInvariant();
            grid.ItemsSource = string.IsNullOrEmpty(f)
                ? lista
                : lista.Where(a => a.Ca.ToUpperInvariant().Contains(f) || a.D.ToUpperInvariant().Contains(f)).ToList();
        }
        txtFiltro.TextChanged += (_, _) => Filtrar();
        btnFiltro.Click       += (_, _) => Filtrar();
        txtFiltro.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Filtrar(); };
        dlg.Loaded += (_, _) => txtFiltro.Focus();

        btnSel.Click += async (_, _) => {
            if (grid.SelectedItem is not Core.Models.Articulo sel) return;
            var precio = session.LocalActual != null
                ? await _artRepo.ObtenerPrecioLocalAsync(sel.Id, session.LocalActual.IdLocal) : null;
            _artSeleccionado = new ArticuloConPrecio { Id = sel.Id, Ca = sel.Ca, D = sel.D,
                MarcaNombre = sel.MarcaNombre, PventaLocal = precio?.Pventa ?? 0 };
            _txtCodigo.Text        = sel.Ca;
            _txtNombreDesc.Text    = sel.D;
            _txtPrecioContado.Text = (_artSeleccionado.PventaLocal).ToString("N0").Replace(",",".");
            dlg.Close();
            _txtCantidad.Focus(); _txtCantidad.SelectAll();
        };
        grid.MouseDoubleClick += async (_, _) => btnSel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        dlg.ShowDialog();
    }

    private async Task AbrirBuscadorLocal()
    {
        // cargar locales desde BD
        List<(int Id, string Nombre)> locales;
        try {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>("SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL");
            locales = rows.Select(r => {
                var d = (IDictionary<string,object>)r;
                int id = d.TryGetValue("ID_LOCAL", out var v1) ? Convert.ToInt32(v1) : 0;
                string nom = d.TryGetValue("NOMBRE", out var v2) ? v2?.ToString() ?? "" : "";
                return (id, nom);
            }).ToList();
        } catch (Exception ex) {
            MessageBox.Show($"Error cargando locales: {ex.Message}");
            return;
        }

        var dlg = new Window { Title = "Seleccionar local", Width = 380, Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco };

        var dp = new DockPanel();
        var hdr = new Border { Background = BrNaranja, Padding = new Thickness(12,8,12,8) };
        hdr.Child = new TextBlock { Text = "Seleccionar local", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        var ftr = new Border { Padding = new Thickness(10,8,10,8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,1,0,0) };
        var btnSel = new Button { Content = "Seleccionar", Height = 30, Width = 110,
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        var btnCan = new Button { Content = "Cancelar", Height = 30, Width = 80, Margin = new Thickness(6,0,0,0),
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        btnCan.Click += (_, _) => dlg.Close();
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSel); ftrSp.Children.Add(btnCan);
        ftr.Child = ftrSp;
        DockPanel.SetDock(ftr, Dock.Bottom); dp.Children.Add(ftr);

        var lb = new ListBox { Margin = new Thickness(10), FontSize = 12 };
        foreach (var (id, nom) in locales)
            lb.Items.Add(new ListBoxItem { Content = nom, Tag = id,
                Padding = new Thickness(8,5,8,5) });
        // preseleccionar local actual
        var session = SessionService.Instance;
        foreach (ListBoxItem li in lb.Items)
            if (li.Tag is int tagId && tagId == session.LocalActual?.IdLocal) {
                lb.SelectedItem = li; break;
            }
        dp.Children.Add(lb);
        dlg.Content = dp;

        btnSel.Click += (_, _) => {
            if (lb.SelectedItem is not ListBoxItem sel) return;
            var idSel = (int)sel.Tag;
            var nomSel = sel.Content.ToString() ?? "";
            // actualizar sesión
            if (session.LocalActual != null) {
                session.LocalActual.IdLocal    = idSel;
                session.LocalActual.NombreLocal = nomSel;
            }
            _txtLocalDisplay.Text = nomSel;
            dlg.Close();
        };
        lb.MouseDoubleClick += (_, _) => btnSel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        dlg.ShowDialog();
    }

    private void AbrirBuscadorCliente()
    {
        var dlg = new Window { Title = "Buscar cliente", Width = 560, Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco };
        var dp = new DockPanel();

        // header
        var hdr = new Border { Background = BrNaranja, Padding = new Thickness(12,8,12,8) };
        hdr.Child = new TextBlock { Text = "Buscar cliente", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        // footer
        var ftrBorder = new Border {
            Padding = new Thickness(10,8,10,8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,1,0,0)
        };
        var btnSel = new Button { Content = "Seleccionar", Height = 30, Width = 110,
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        var btnCan = new Button { Content = "Cancelar", Height = 30, Width = 80, Margin = new Thickness(6,0,0,0),
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        btnCan.Click += (_, _) => dlg.Close();
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSel); ftrSp.Children.Add(btnCan);
        ftrBorder.Child = ftrSp;
        DockPanel.SetDock(ftrBorder, Dock.Bottom); dp.Children.Add(ftrBorder);

        // barra de filtro
        var filtroBar = new Border {
            Padding = new Thickness(10,8,10,8),
            Background = new SolidColorBrush(Color.FromRgb(250,250,250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,0,0,1)
        };
        var filtroDp = new DockPanel();
        var txtF = new TextBox { Height = 28, Padding = new Thickness(8,2,8,2),
            FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center };
        var btnF = new Button { Content = "Buscar", Height = 28, Width = 70,
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, Margin = new Thickness(6,0,0,0) };
        DockPanel.SetDock(btnF, Dock.Right);
        filtroDp.Children.Add(btnF); filtroDp.Children.Add(txtF);
        filtroBar.Child = filtroDp;
        DockPanel.SetDock(filtroBar, Dock.Top); dp.Children.Add(filtroBar);

        // grid
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));

        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255,250,240)),
            BorderThickness = new Thickness(0), ColumnHeaderStyle = hdrStyle };
        grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("NombreCliente"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "CI",     Binding = new System.Windows.Data.Binding("CiCliente"),     Width = 110 });
        dp.Children.Add(grid);
        dlg.Content = dp;

        List<Cliente> _todos = new();

        // carga todos al abrir y aplica filtro en tiempo real
        dlg.Loaded += async (_, _) => {
            txtF.Focus();
            var res = await _clienteRepo.BuscarAsync("");
            _todos = res.ToList();
            grid.ItemsSource = _todos;
        };

        void Filtrar() {
            var f = txtF.Text.Trim().ToUpperInvariant();
            grid.ItemsSource = string.IsNullOrEmpty(f)
                ? _todos
                : _todos.Where(c =>
                    c.NombreCliente.ToUpperInvariant().Contains(f) ||
                    (c.CiCliente ?? "").ToUpperInvariant().Contains(f)).ToList();
        }
        txtF.TextChanged += (_, _) => Filtrar();
        btnF.Click       += (_, _) => Filtrar();
        txtF.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Filtrar(); };

        btnSel.Click += (_, _) => {
            if (grid.SelectedItem is not Cliente c) return;
            _clienteActual = c;
            _txtBuscarCliente.Text = $"{c.NombreCliente}  CI: {c.CiCliente}";
            ActualizarConfirmarBtn();
            dlg.Close();
        };
        grid.MouseDoubleClick += (_, _) => btnSel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        dlg.ShowDialog();
    }

    // ── Clientes (legacy stubs, lógica real en AbrirBuscadorCliente) ─────────

    private Task BuscarClienteAsync() => Task.CompletedTask;

    private void OnClienteSelected(object sender, SelectionChangedEventArgs e) { }

    // ── Artículos ───────────────────────────────────────────────────────────────

    private Task BuscarArticuloAsync() => BuscarPorCodigoAsync();

    private async Task AgregarArticuloAsync()
    {
        if (_artSeleccionado == null)
        {
            // intentar buscar por código si no hay artículo seleccionado
            await BuscarPorCodigoAsync();
            if (_artSeleccionado == null) {
                MessageBox.Show("Primero busque un artículo por código.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var cantText = _txtCantidad.Text.Trim();
        if (!decimal.TryParse(string.IsNullOrWhiteSpace(cantText) ? "1" : cantText, out var cantidad) || cantidad <= 0)
            cantidad = 1;

        var artSelec = _artSeleccionado;
        var existente = _carrito.FirstOrDefault(x => x.IdArt == artSelec.Id);
        if (existente != null)
        {
            existente.Cantidad += cantidad;
        }
        else
        {
            var art = await _artRepo.BuscarPorCodigoAsync(artSelec.Ca);
            _carrito.Add(new LineaDetalle
            {
                IdArt          = artSelec.Id,
                ArticuloCodigo = artSelec.Ca,
                ArticuloNombre = artSelec.D,
                Cantidad       = cantidad,
                Pv             = artSelec.PventaLocal,
                Iva            = art?.Iva ?? 0
            });
        }
        // limpiar selección
        _artSeleccionado = null;
        _txtCodigo.Text = ""; _txtNombreDesc.Text = ""; _txtPrecioContado.Text = "";
        _txtCantidad.Text = ""; _txtCodigo.Focus();
        RefrescarCarrito();
    }

    private void OnQuitarArticulo(object sender, RoutedEventArgs e)
    {
        if (_gridDetalle.SelectedItem is LineaDetalle l) { _carrito.Remove(l); RefrescarCarrito(); }
    }

    private void RefrescarCarrito()
    {
        _gridDetalle.ItemsSource = null;
        _gridDetalle.ItemsSource = _carrito.ToList();
        ActualizarTotal();
        ActualizarConfirmarBtn();
    }

    private void ActualizarTotal()
    {
        var total = _carrito.Sum(x => x.Subtotal);
        if (_lblTotal != null) _lblTotal.Text = $"Total: {total:N0} Gs.";
        if (_txtCambio != null && decimal.TryParse(
            new string((_txtEfectivo?.Text ?? "0").Where(char.IsDigit).ToArray()), out var ent))
            _txtCambio.Text = (ent - total).ToString("N0").Replace(",",".");
    }

    private void ActualizarConfirmarBtn()
        => _btnConfirmar.IsEnabled = _clienteActual != null && _carrito.Count > 0;

    // ── Modal de éxito + reset ──────────────────────────────────────────────────

    private void MostrarExitoYResetear(string clienteNombre, int nroVenta, decimal total)
    {
        var dlg = new Window {
            Title = "Venta registrada",
            Width = 400, Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = BrBlanco
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // header verde
        var hdr = new Border { Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Padding = new Thickness(16,12,16,12) };
        hdr.Child = new TextBlock { Text = "Venta registrada exitosamente", FontSize = 14,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // detalle
        var body = new StackPanel { Margin = new Thickness(20,16,20,0), VerticalAlignment = VerticalAlignment.Center };
        void Linea(string lbl, string val) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,0) };
            sp.Children.Add(new TextBlock { Text = lbl, FontWeight = FontWeights.Bold, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(80,80,80)), Width = 110 });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(30,30,30)) });
            body.Children.Add(sp);
        }
        Linea("Cliente:",    clienteNombre);
        Linea("N° de venta:", nroVenta.ToString());
        Linea("Total:",      total.ToString("N0").Replace(",",".") + " Gs.");
        Grid.SetRow(body, 1); root.Children.Add(body);

        // botón aceptar
        var ftr = new Border { Padding = new Thickness(16,10,16,10),
            Background = new SolidColorBrush(Color.FromRgb(245,245,245)) };
        var btnOk = new Button { Content = "Aceptar", Height = 32, Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
        btnOk.Click += (_, _) => dlg.Close();
        ftr.Child = btnOk;
        Grid.SetRow(ftr, 2); root.Children.Add(ftr);

        dlg.Content = root;
        dlg.ShowDialog();

        // al cerrar el modal de éxito → resetear el formulario
        ResetearFormulario();
    }

    private void ResetearFormulario()
    {
        _carrito.Clear();
        _artSeleccionado   = null;
        _clienteActual     = null;

        _txtCodigo.Text         = "";
        _txtNombreDesc.Text     = "";
        _txtPrecioContado.Text  = "";
        _txtCantidad.Text       = "";
        _txtBuscarCliente.Text  = "— sin cliente —";
        _txtEfectivo.Text       = "0";
        _txtCambio.Text         = "0";
        _txtNroTarjeta.Text     = "";
        _cboMetodo.SelectedIndex = 0;

        RefrescarCarrito();
        _txtCodigo.Focus();
    }

    // ── Modal de confirmación de venta ──────────────────────────────────────────

    private bool MostrarConfirmacionVenta(string clienteNombre, decimal total)
    {
        var dlg = new Window {
            Title = "Confirmar venta al contado",
            Width = 640, Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = BrBlanco
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // info cliente
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // total
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer botones

        // ── header ──
        var hdr = new Border { Background = BrNaranja, Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "¿Desea realizar la venta al contado de los siguientes productos?",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrBlanco,
            TextWrapping = TextWrapping.Wrap
        });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── info cliente ──
        var infoBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(255,248,225)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255,200,80)),
            BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(16,8,16,8)
        };
        var infoSp = new StackPanel { Orientation = Orientation.Horizontal };
        infoSp.Children.Add(new TextBlock { Text = "Cliente: ", FontWeight = FontWeights.Bold,
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(90,40,0)) });
        infoSp.Children.Add(new TextBlock { Text = clienteNombre,
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(50,50,50)) });
        infoBorder.Child = infoSp;
        Grid.SetRow(infoBorder, 1); root.Children.Add(infoBorder);

        // ── grid artículos ──
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,7,10,7)));

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,0,10,0)));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));

        Style rStyle() { var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0,0,8,0))); return s; }

        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 34, FontSize = 12,
            Background = BrBlanco, RowBackground = BrBlanco,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255,250,240)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = hdrStyle, CellStyle = cellStyle,
            CanUserResizeRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0)
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("ArticuloCodigo"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("ArticuloNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cant.",       Binding = new System.Windows.Data.Binding("Cantidad"),        Width = 55,  ElementStyle = rStyle() });
        grid.Columns.Add(new DataGridTextColumn { Header = "P. venta",    Binding = new System.Windows.Data.Binding("PvStr"),           Width = 100, ElementStyle = rStyle() });
        grid.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("SubtotalStr"),     Width = 110, ElementStyle = rStyle() });
        grid.ItemsSource = _carrito.ToList();
        Grid.SetRow(grid, 2); root.Children.Add(grid);

        // ── total ──
        var totalBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(255,248,225)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,200,100)),
            BorderThickness = new Thickness(0,1,0,1),
            Padding = new Thickness(16,10,16,10)
        };
        var totalTb = new TextBlock {
            Text = $"Total:  {total.ToString("N0").Replace(",",".")} Gs.",
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = BrNaranja, HorizontalAlignment = HorizontalAlignment.Right
        };
        totalBorder.Child = totalTb;
        Grid.SetRow(totalBorder, 3); root.Children.Add(totalBorder);

        // ── botones ──
        var ftrBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(245,245,245)),
            Padding = new Thickness(16,12,16,12)
        };
        bool resultado = false;
        var btnSi = new Button {
            Content = "Sí, confirmar venta", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnNo = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(10,0,0,0)
        };
        btnSi.Click += (_, _) => { resultado = true;  dlg.Close(); };
        btnNo.Click += (_, _) => { resultado = false; dlg.Close(); };
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSi); ftrSp.Children.Add(btnNo);
        ftrBorder.Child = ftrSp;
        Grid.SetRow(ftrBorder, 4); root.Children.Add(ftrBorder);

        dlg.Content = root;
        dlg.ShowDialog();
        return resultado;
    }

    // ── Confirmar ───────────────────────────────────────────────────────────────

    private async void OnConfirmar(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null || _carrito.Count == 0) return;

        var session  = SessionService.Instance;
        if (session.UsuarioActual == null || session.LocalActual == null) return;

        var subtotal = _carrito.Sum(x => x.Subtotal);
        var descPct  = ParseDec(_txtDescuento.Text);
        var descuento= subtotal * descPct / 100m;
        var total    = subtotal - descuento;
        var monto   = ParseDec(_txtEfectivo.Text);   // campo único para efectivo y no-efectivo
        var tarjeta = ParseDec(_txtTarjeta.Text);    // legacy, no usado visualmente
        byte metodo = 1;
        if (_cboMetodo.SelectedItem is ComboBoxItem cboItem && cboItem.Tag is byte m) metodo = m;

        // efectivo va en EntregaNormal; tarjeta/transferencia va en EntregaLogistica
        var efectivo = metodo == 1 ? monto : 0m;
        tarjeta      = metodo != 1 ? monto : 0m;

        // validar que el monto ingresado cubra el total
        if (monto < total)
        {
            MessageBox.Show(
                $"El monto ingresado ({monto.ToString("N0").Replace(",",".")} Gs.) es menor al total ({total.ToString("N0").Replace(",",".")} Gs.).",
                "Monto insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!MostrarConfirmacionVenta(_clienteActual.NombreCliente, total)) return;

        _btnConfirmar.IsEnabled = false;
        try
        {
            var nSol = await _ventaRepo.ObtenerNumeroSolicitudAsync();
            int idCabResult = 0;

            for (int i = 0; i < _carrito.Count; i++)
            {
                var det = _carrito[i];
                var esPrimero = i == 0;
                var prm = new VentaContadoParams(
                    IdCab: 0, NSol: nSol,
                    IdLocal: (byte)session.LocalActual.IdLocal,
                    IdUsuario: session.UsuarioActual.IdUsuario,
                    IdCliente: _clienteActual.IdCliente,
                    IdGarante: 0, IdRef1: 0, IdRef2: 0,
                    NomRefCom1: "", TelRefCom1: "", TrabRefCom1: "",
                    NomRefCom2: "", TelRefCom2: "", TrabRefCom2: "",
                    FormaDeVenta: 2, MetodoDeVenta: metodo,
                    NTarjeta: _txtNroTarjeta.Text,
                    Parcial: subtotal, Descuento: descuento, Total: total,
                    EntregaNormal: efectivo, EntregaLogistica: tarjeta,
                    Cuotas: 1, MontoCuota: total,
                    Debe: 0, Haber: total, Cpha: 0,
                    Estado: 1, Tiva: _carrito.Sum(x => x.Iva * x.Cantidad * x.Pv / 100m),
                    IdDet: 0, IdCab2: 0, IdArt: det.IdArt,
                    Cantidad: det.Cantidad, Pc: 0, Pv: det.Pv, IvaArt: det.Iva, EsArt: 1,
                    IdMovArt: 0, Mov: 2, Mod: 2,
                    StIni: 0, PCant: 0, PcAct: 0,
                    IdCabCaja: 0, CountCaja: esPrimero ? 1 : 0,
                    IdDetCaja: 0, Caja: 0,
                    Accion: esPrimero ? (byte)1 : (byte)0,
                    Concepto: 1, Monto: esPrimero ? total : 0,
                    Metodo: metodo, Numero: _txtNroTarjeta.Text,
                    Para: 0, Obs: "", NVenta: 0,
                    Agente: esPrimero ? "SI" : "NO");

                idCabResult = await _ventaRepo.GuardarVentaContadoAsync(prm);
            }

            MostrarExitoYResetear(_clienteActual.NombreCliente, idCabResult, total);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnConfirmar.IsEnabled = true;
        }
    }

    // extrae solo dígitos antes de parsear — compatible con formato "300.000" o "300,000"
    private static decimal ParseDec(string? s) {
        var digits = new string((s ?? "").Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }

    private static Button Btn(string text, string hex, int width = 110) => new Button
    {
        Content = text, Height = 30, Width = width, Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand
    };

    private static TextBlock Lbl(string t) => new TextBlock
        { Text = t, FontSize = 11, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 4, 4, 1) };
}

// ══════════════════════════════════════════════════════════════════════════════
//  VISOR DE SOLICITUDES
// ══════════════════════════════════════════════════════════════════════════════

public class SolicitudItem
{
    public int      IdSolicitud    { get; set; }
    public string   Numero         { get; set; } = string.Empty;
    public string   LocalNombre    { get; set; } = string.Empty;
    public string   ClienteNombre  { get; set; } = string.Empty;
    public string   VendedorNombre { get; set; } = string.Empty;
    public string   Estado         { get; set; } = string.Empty;
    public byte     EstadoNum      { get; set; }  // 0=Pendiente 1=Aprobado 2=Rechazado
    public DateTime FechaSolicitud { get; set; }
    public decimal  TotalVenta     { get; set; }
    public decimal  Entrega        { get; set; }
    public int      Cuotas         { get; set; }
}

public class VisorSolicitudesWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private TextBox   _txtFiltro = null!;
    private DataGrid  _grid      = null!;
    private TextBlock _lblConteo = null!;
    private List<SolicitudItem> _todosItems = new();

    // Paleta naranja CrediSoft
    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb(255, 140,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb(224, 112,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrFondo    = new(System.Windows.Media.Color.FromRgb(240, 242, 245));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229, 231, 235));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrAlt      = new(System.Windows.Media.Color.FromRgb(249, 250, 251));

    // Chips de estado
    private static readonly System.Windows.Media.SolidColorBrush BrChipAcep  = new(System.Windows.Media.Color.FromRgb( 34, 197,  94));
    private static readonly System.Windows.Media.SolidColorBrush BrChipVer   = new(System.Windows.Media.Color.FromRgb( 59, 130, 246));
    private static readonly System.Windows.Media.SolidColorBrush BrChipRech  = new(System.Windows.Media.Color.FromRgb(239,  68,  68));
    private static readonly System.Windows.Media.SolidColorBrush BrChipPend  = new(System.Windows.Media.Color.FromRgb(245, 158,  11));

    public VisorSolicitudesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Visor de Solicitudes de Crédito";
        Width = 1120; Height = 660;
        MinWidth = 860; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header naranja ────────────────────────────────────────────────────
        var hdr = new Border {
            Background = BrPrimary, Padding = new Thickness(18, 12, 18, 12),
        };
        hdr.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 6, ShadowDepth = 2, Opacity = 0.22,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        var hdrGrid = new Grid();
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hdrLeft.Children.Add(new Border {
            Background = BrPrimDark, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock { Text = "📋", FontSize = 20 }
        });
        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock {
            Text = "Ventas a Crédito — Solicitudes",
            Foreground = BrBlanco, FontSize = 16, FontWeight = FontWeights.Bold
        });
        hdrTexts.Children.Add(new TextBlock {
            Text = "Listado de solicitudes · Haga clic en \"+ Nueva solicitud\" para crear una nueva venta a crédito",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 224, 178)),
            FontSize = 10.5
        });
        hdrLeft.Children.Add(hdrTexts);
        Grid.SetColumn(hdrLeft, 0);

        _lblConteo = new TextBlock {
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,224,178)),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(_lblConteo, 1);

        hdrGrid.Children.Add(hdrLeft);
        hdrGrid.Children.Add(_lblConteo);
        hdr.Child = hdrGrid;
        Grid.SetRow(hdr, 0);
        root.Children.Add(hdr);

        // ── Barra búsqueda ────────────────────────────────────────────────────
        var searchBar = new Border {
            Background = BrBlanco, Padding = new Thickness(14, 10, 14, 10),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var searchPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var searchBox = new Border {
            Background = BrFondo, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var searchInner = new StackPanel { Orientation = Orientation.Horizontal };
        searchInner.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = BrGris
        });
        _txtFiltro = new TextBox {
            Width = 260, Height = 32, FontSize = 13, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17,24,39))
        };
        _txtFiltro.TextChanged += (_, _) => FiltrarLocal(_txtFiltro.Text.Trim());
        searchInner.Children.Add(_txtFiltro);
        searchBox.Child = searchInner;
        searchPanel.Children.Add(searchBox);

        var btnRefresh = MakeBtn("↺  Refrescar", BrPrimary);
        btnRefresh.Click += async (_, _) => { _txtFiltro.Text = ""; await Cargar(); };
        searchPanel.Children.Add(btnRefresh);

        var btnNueva = MakeBtn("+ Nueva solicitud", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22,163,74)));
        btnNueva.Margin = new Thickness(8,0,0,0);
        btnNueva.Click += (_, _) => {
            var w = new VentaCreditoWindow { Owner = this };
            w.ShowDialog();
        };
        searchPanel.Children.Add(btnNueva);

        searchBar.Child = searchPanel;
        Grid.SetRow(searchBar, 1);
        root.Children.Add(searchBar);

        // ── DataGrid moderno ──────────────────────────────────────────────────
        var gridWrap = new Border {
            Margin = new Thickness(12, 10, 12, 0),
            Background = BrBlanco,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true
        };
        gridWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 8, ShadowDepth = 1, Opacity = 0.08,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 38, ColumnHeaderHeight = 36, FontSize = 12.5,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrBorde,
            Background = BrBlanco,
            AlternatingRowBackground = BrAlt,
            BorderThickness = new Thickness(0),
            RowBackground = BrBlanco,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };

        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, BrPrimary));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, BrBlanco));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.5));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10, 0, 10, 0)));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, BrPrimDark));
        _grid.ColumnHeaderStyle = colHdrStyle;

        // RowStyle: sin bordes ni foco visual
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.FocusVisualStyleProperty, null));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        _grid.RowStyle = rowStyle;

        // CellStyle: reemplaza el ControlTemplate para eliminar por completo el borde de foco/selección
        var cellTemplate = new ControlTemplate(typeof(DataGridCell));
        var cellBorderFactory = new FrameworkElementFactory(typeof(Border));
        cellBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        cellBorderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cellBorderFactory.AppendChild(contentFactory);
        cellTemplate.VisualTree = cellBorderFactory;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(DataGridCell.TemplateProperty, cellTemplate));
        cellStyle.Setters.Add(new Setter(DataGridCell.VerticalAlignmentProperty, VerticalAlignment.Stretch));
        _grid.CellStyle = cellStyle;

        var txtStyle = new Style(typeof(TextBlock));
        txtStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txtStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10, 0, 10, 0)));

        _grid.Columns.Add(new DataGridTextColumn {
            Header = "N° Solicitud",
            Binding = new System.Windows.Data.Binding("Numero"),
            Width = 115, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Local",
            Binding = new System.Windows.Data.Binding("LocalNombre"),
            Width = 130, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Cliente",
            Binding = new System.Windows.Data.Binding("ClienteNombre"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Vendedor",
            Binding = new System.Windows.Data.Binding("VendedorNombre"),
            Width = 155, ElementStyle = txtStyle
        });

        // Columna Estado como chip de color
        var estadoTemplate = new DataTemplate();
        var chipFactory = new FrameworkElementFactory(typeof(Border));
        chipFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        chipFactory.SetValue(Border.PaddingProperty, new Thickness(10, 3, 10, 3));
        chipFactory.SetValue(Border.MarginProperty, new Thickness(8, 6, 8, 6));
        chipFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        var chipText = new FrameworkElementFactory(typeof(TextBlock));
        chipText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Estado"));
        chipText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        chipText.SetValue(TextBlock.FontSizeProperty, 10.5);
        chipText.SetValue(TextBlock.ForegroundProperty, BrBlanco);
        chipText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        chipFactory.AppendChild(chipText);
        estadoTemplate.VisualTree = chipFactory;
        _grid.Columns.Add(new DataGridTemplateColumn {
            Header = "Estado", CellTemplate = estadoTemplate, Width = 110
        });

        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Fecha",
            Binding = new System.Windows.Data.Binding("FechaSolicitud") { StringFormat = "dd/MM/yyyy" },
            Width = 95, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Total (Gs.)",
            Binding = new System.Windows.Data.Binding("TotalVenta") { StringFormat = "N0" },
            Width = 105, ElementStyle = txtStyle
        });

        // Columna acción
        var btnColTemplate = new DataTemplate();
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "Ver detalle →");
        btnFactory.SetValue(Button.HeightProperty, 26.0);
        btnFactory.SetValue(Button.PaddingProperty, new Thickness(10, 0, 10, 0));
        btnFactory.SetValue(Button.MarginProperty, new Thickness(6, 5, 6, 5));
        btnFactory.SetValue(Button.BackgroundProperty, BrPrimary);
        btnFactory.SetValue(Button.ForegroundProperty, BrBlanco);
        btnFactory.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
        btnFactory.SetValue(Button.FontSizeProperty, 11.0);
        btnFactory.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
        btnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        btnFactory.AddHandler(Button.ClickEvent, new System.Windows.RoutedEventHandler(OnVerDetalle));
        btnColTemplate.VisualTree = btnFactory;
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "Acción", CellTemplate = btnColTemplate, Width = 115 });

        _grid.LoadingRow += OnLoadingRow;
        gridWrap.Child = _grid;
        Grid.SetRow(gridWrap, 2);
        root.Children.Add(gridWrap);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border {
            Background = BrBlanco, Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 6, ShadowDepth = -2, Opacity = 0.07,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var btnCerrar = MakeBtn("✕  Cerrar", BrGris);
        btnCerrar.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrar, 1);

        footerGrid.Children.Add(btnCerrar);
        footer.Child = footerGrid;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
    }

    private static System.Windows.Media.SolidColorBrush ChipColor(string estado)
    {
        var s = estado.ToUpperInvariant();
        if (s.Contains("ACEPT") || s.Contains("APROBAD")) return BrChipAcep;
        if (s.Contains("VERIF"))  return BrChipVer;
        if (s.Contains("RECHAZ")) return BrChipRech;
        return BrChipPend;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not SolicitudItem item) return;
        e.Row.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(17, 24, 39));
        // Color del chip de estado — se aplica al Border del template
        e.Row.Loaded += (_, _) =>
        {
            var chip = FindVisualChild<Border>(e.Row, b => b.CornerRadius.TopLeft == 10);
            if (chip != null) chip.Background = ChipColor(item.Estado);
        };
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent, Func<T, bool>? filter = null) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t && (filter == null || filter(t))) return t;
            var result = FindVisualChild<T>(child, filter);
            if (result != null) return result;
        }
        return null;
    }

    private void FiltrarLocal(string filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro))
        {
            _grid.ItemsSource = _todosItems;
            _lblConteo.Text = $"{_todosItems.Count} solicitudes";
            return;
        }
        var lista = _todosItems.Where(x =>
            x.Numero.Contains(filtro, StringComparison.OrdinalIgnoreCase)        ||
            x.ClienteNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
            x.LocalNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)   ||
            x.VendedorNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)||
            x.Estado.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();
        _grid.ItemsSource = lista;
        _lblConteo.Text = $"{lista.Count} de {_todosItems.Count} solicitudes";
    }

    private static Button MakeBtn(string text, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = text, Height = 32, Padding = new Thickness(14, 0, 14, 0),
        Margin = new Thickness(0, 0, 6, 0),
        Background = bg, Foreground = BrBlanco,
        BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand,
        FontSize = 12.5, FontWeight = FontWeights.SemiBold
    };

    private async void OnVerDetalle(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not SolicitudItem item) return;
        var estadoAntes = item.EstadoNum;
        var w = new DetalleSolicitudWindow(item, _db) { Owner = this };
        w.ShowDialog();
        if (item.EstadoNum != estadoAntes)
            await Cargar();
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            IEnumerable<dynamic> rows;
            var session = SessionService.Instance;
            bool esAdmin = session.UsuarioActual?.EsAdministrador == true;

            var p = new DynamicParameters();
            if (esAdmin)
            {
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                rows = await conn.QueryAsync<dynamic>("CARGAR_REV_SOL__VISOR_ADMIN_CS", p, commandType: CommandType.StoredProcedure);
            }
            else
            {
                p.Add("@Idlocal", (byte)(session.LocalActual?.IdLocal ?? 1));
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                rows = await conn.QueryAsync<dynamic>("CARGAR_REV_SOL_VISOR_CS", p, commandType: CommandType.StoredProcedure);
            }

            var items = new List<SolicitudItem>();
            foreach (var r in rows)
            {
                int    idSol    = (int)r.IDSOLICITUD;
                string nro      = ((string?)r.NUMERO) ?? idSol.ToString();
                string estado   = ((string?)r.ESTADO) ?? "—";
                string fechaStr = ((string?)r.FECHA_SOLICITUD) ?? "";
                DateTime.TryParseExact(fechaStr, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var fecha);

                var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TOP 1 cl.NOMBRE_CLIENTE as NomCli, u.NOMBRE_USUARIO as NomVend, " +
                    "l.NOMBRE as NomLocal, s.TOTALSALE as Total, s.TOTALENTREGA as Entrega, " +
                    "s.CANTCUOTAS as Cuotas, s.ESTADO as EstadoNum " +
                    "FROM CAB_SOL_SALES s " +
                    "LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE " +
                    "LEFT JOIN USUARIOS u  ON s.ID_USUARIO = u.ID_USUARIO " +
                    "LEFT JOIN LOCALES  l  ON s.ID_LOCAL   = l.ID_LOCAL " +
                    "WHERE s.IDSOLICITUD = @id", new { id = idSol });

                items.Add(new SolicitudItem {
                    IdSolicitud    = idSol,
                    Numero         = nro,
                    LocalNombre    = cab != null ? (string?)cab.NomLocal  ?? "—" : "—",
                    ClienteNombre  = cab != null ? (string?)cab.NomCli    ?? "—" : "—",
                    VendedorNombre = cab != null ? (string?)cab.NomVend   ?? "—" : "—",
                    Estado         = estado,
                    FechaSolicitud = fecha,
                    TotalVenta     = cab != null ? (decimal?)cab.Total    ?? 0 : 0,
                    Entrega        = cab != null ? (decimal?)cab.Entrega  ?? 0 : 0,
                    Cuotas         = cab != null ? (int?)cab.Cuotas       ?? 0 : 0,
                    EstadoNum      = cab != null ? (byte?)cab.EstadoNum   ?? 0 : (byte)0,
                });
            }

            _todosItems = items;
            _grid.ItemsSource = _todosItems;
            _lblConteo.Text = $"{_todosItems.Count} solicitudes";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar solicitudes: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  DETALLE DE SOLICITUD  (vista de aprobación al hacer clic en una fila)
// ══════════════════════════════════════════════════════════════════════════════

internal class DetalleSolRow
{
    public string Codigo      { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio     { get; set; }
    public decimal Entrega    { get; set; }
    public int    Cuotas      { get; set; }
    public decimal CostoMens  { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal Cantidad   { get; set; }
    public decimal TotalGral  { get; set; }
}

public class DetalleSolicitudWindow : Window
{
    private readonly SolicitudItem        _sol;
    private readonly IDbConnectionFactory _db;

    // Campos dinámicos — cliente
    private TextBlock _tbCliNombre = null!, _tbCliCi = null!, _tbCliDir = null!,
                      _tbCliCel = null!, _tbCliCiudad = null!, _tbCliEcv = null!,
                      _tbCliCredMax = null!, _tbCliSaldo = null!,
                      _tbCliCondicion = null!, _tbCliTipo = null!,
                      _tbCliVencCi = null!, _tbCliConyuge = null!;
    // Campos dinámicos — garante
    private TextBlock _tbGarNombre = null!, _tbGarCi = null!, _tbGarDir = null!,
                      _tbGarCel = null!, _tbGarCiudad = null!,
                      _tbGarEmpresa = null!, _tbGarTelLab = null!, _tbGarEcv = null!;
    // Referencias
    private TextBlock _tbRef1Nom = null!, _tbRef1Tel = null!, _tbRef1Trab = null!;
    private TextBlock _tbRef2Nom = null!, _tbRef2Tel = null!, _tbRef2Trab = null!;
    private TextBlock _tbRefCom1Nom = null!, _tbRefCom1Tel = null!;
    private TextBlock _tbRefCom2Nom = null!, _tbRefCom2Tel = null!;
    // Ingresos/Egresos
    private TextBlock _tbISalario = null!, _tbIHonorario = null!, _tbIConyuge = null!,
                      _tbIOtros = null!, _tbITotal = null!;
    private TextBlock _tbEGasto = null!, _tbECuota = null!, _tbEAlquiler = null!,
                      _tbEOtros = null!, _tbETotal = null!, _tbSaldo = null!;
    // Nota / logística
    private TextBox   _txtNota       = null!;
    private TextBox   _txtLogistica  = null!, _txtRecibo = null!, _txtPagare = null!;
    // Método de pago
    private ComboBox  _cmbMetodoPago = null!;
    private TextBox   _txtNroMetodo  = null!;
    // Campos DATOS
    private TextBlock _tbDatAprobado = null!, _tbDatTotal = null!, _tbDatEntrega = null!,
                      _tbDatCuotas  = null!, _tbDatMonto = null!, _tbDatPago    = null!;
    private DataGrid  _gridProductos = null!;
    // Estado interno cargado desde BD
    private string _fotoCedulaCliente = "";
    private int    _idClienteCargado  = 0;
    private int    _idGaranteCargado  = 0;
    private string _nomGaranteCargado = "";

    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb(255, 140,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb(224, 112,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrFondo    = new(System.Windows.Media.Color.FromRgb(240, 242, 245));
    private static readonly System.Windows.Media.SolidColorBrush BrCard     = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229, 231, 235));
    private static readonly System.Windows.Media.SolidColorBrush BrSecHead  = new(System.Windows.Media.Color.FromRgb(255, 140,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrSecBody  = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
    private static readonly System.Windows.Media.SolidColorBrush BrGrisOsc  = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 34, 197,  94));
    private static readonly System.Windows.Media.SolidColorBrush BrRojo     = new(System.Windows.Media.Color.FromRgb(239,  68,  68));
    private static readonly System.Windows.Media.SolidColorBrush BrAzul     = new(System.Windows.Media.Color.FromRgb( 59, 130, 246));
    private static readonly System.Windows.Media.SolidColorBrush BrAmarillo = new(System.Windows.Media.Color.FromRgb(245, 158,  11));
    private static readonly System.Windows.Media.SolidColorBrush BrLabelTxt = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrValTxt   = new(System.Windows.Media.Color.FromRgb( 17,  24,  39));

    public DetalleSolicitudWindow(SolicitudItem sol, IDbConnectionFactory db)
    {
        _sol = sol;
        _db  = db;
        Title = $"Solicitud N° {sol.Numero}  —  {sol.ClienteNombre}";
        Width = 1280; Height = 800;
        MinWidth = 1100; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        BuildUI();
        Loaded += async (_, _) => await CargarDetalleAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CONSTRUCCIÓN DE UI
    // ═══════════════════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // banner estado (solo si no es Verificar)
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // contenido
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ──────────────────────────────────────────────────────────
        var header = new Border {
            Background = BrPrimary,
            Padding = new Thickness(20, 12, 20, 12)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Izquierda: número · cliente en una sola línea, mismo tamaño y negrita
        var headerTb = new TextBlock {
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, VerticalAlignment = VerticalAlignment.Center
        };
        headerTb.Inlines.Add(new System.Windows.Documents.Run($"Solicitud N° {_sol.Numero}"));
        headerTb.Inlines.Add(new System.Windows.Documents.Run("  ·  ") { Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 255)) });
        headerTb.Inlines.Add(new System.Windows.Documents.Run(_sol.ClienteNombre));
        Grid.SetColumn(headerTb, 0);
        headerGrid.Children.Add(headerTb);

        // Derecha: chip estado con fondo blanco sólido
        var chipEstado = BuildChipEstado(_sol.Estado);
        chipEstado.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chipEstado, 1);
        headerGrid.Children.Add(chipEstado);

        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // ── Banner estado (Aprobado / Rechazado) ────────────────────────────
        if (_sol.EstadoNum != 0)
        {
            bool aprobado = _sol.EstadoNum == 1;
            var banner = new Border {
                Background = aprobado
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)),
                BorderBrush = aprobado
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 8, 16, 8)
            };
            banner.Child = new TextBlock {
                Text = aprobado
                    ? "Esta solicitud ha sido APROBADA. Solo se permite consulta de datos."
                    : "Esta solicitud ha sido RECHAZADA. Solo se permite consulta de datos.",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = aprobado
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27))
            };
            Grid.SetRow(banner, 1);
            root.Children.Add(banner);
        }

        // ── Contenido principal ──────────────────────────────────────────────
        var scroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = BrFondo
        };

        var contentGrid = new Grid { Margin = new Thickness(12, 8, 12, 8) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

        var main = new StackPanel();
        Grid.SetColumn(main, 0);
        contentGrid.Children.Add(main);

        var lateral = BuildPanelLateral();
        Grid.SetColumn(lateral, 2);
        contentGrid.Children.Add(lateral);

        scroll.Content = contentGrid;
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);

        // Secciones
        main.Children.Add(SecGroup("Cliente", BuildClienteGrid()));
        main.Children.Add(SecGroup("Garante", BuildGaranteGrid()));
        main.Children.Add(SecGroup("Referencias", BuildReferenciasGrid()));
        main.Children.Add(SecGroup("Mercaderías", BuildMercaderiasContent()));
        main.Children.Add(BuildSeccionDatos());

        var rowIngNota = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        rowIngNota.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        rowIngNota.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        rowIngNota.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ingSec = SecGroup("Ingresos / Egresos", BuildIngresosEgresosGrid());
        Grid.SetColumn(ingSec, 0);
        rowIngNota.Children.Add(ingSec);

        _txtNota = new TextBox {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 80, Padding = new Thickness(8),
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(1), FontSize = 12
        };
        var notaSec = SecGroup("Nota", _txtNota);
        Grid.SetColumn(notaSec, 2);
        rowIngNota.Children.Add(notaSec);
        main.Children.Add(rowIngNota);
        main.Children.Add(BuildFilaLogistica());

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = BrCard,
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        var footerDp = new DockPanel();

        if (_sol.EstadoNum == 0)
        {
            var accionesSp = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnVerif    = MakeBtn("Verif. Datos", BrAzul);
            var btnRechazar = MakeBtn("Rechazar",     BrRojo);
            var btnAceptar  = MakeBtn("Aceptar",      BrVerde);
            btnVerif.Click    += async (_, _) => await CambiarEstadoAsync(0);
            btnRechazar.Click += async (_, _) => await CambiarEstadoAsync(2);
            btnAceptar.Click  += async (_, _) => await CambiarEstadoAsync(1);
            accionesSp.Children.Add(btnVerif);
            accionesSp.Children.Add(btnRechazar);
            accionesSp.Children.Add(btnAceptar);
            DockPanel.SetDock(accionesSp, Dock.Left);
            footerDp.Children.Add(accionesSp);
        }

        var rightBtns = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var btnGuardar = MakeBtn("Guardar", BrPrimary);
        var btnCerrar  = MakeBtn("Cerrar",  BrGrisOsc);
        btnGuardar.Click += async (_, _) => await GuardarAsync();
        btnCerrar.Click  += (_, _) => Close();
        rightBtns.Children.Add(btnGuardar);
        rightBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(rightBtns, Dock.Right);
        footerDp.Children.Add(rightBtns);

        footer.Child = footerDp;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
    }

    private static Border BuildChipEstado(string estado)
    {
        // Sobre fondo naranja: fondo blanco, texto con color semántico
        var fg = estado.ToUpperInvariant() switch {
            var s when s.Contains("APROB") => System.Windows.Media.Color.FromRgb( 22, 163,  74),
            var s when s.Contains("RECH")  => System.Windows.Media.Color.FromRgb(220,  38,  38),
            var s when s.Contains("VERIF") => System.Windows.Media.Color.FromRgb( 37,  99, 235),
            _                              => System.Windows.Media.Color.FromRgb(180,  83,   9)
        };
        return new Border {
            Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(14, 5, 14, 5),
            BorderBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(2),
            Child = new TextBlock {
                Text = estado.ToUpperInvariant(),
                FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(fg)
            }
        };
    }

    private static Button MakeBtn(string text, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = text, Height = 32, Padding = new Thickness(18, 0, 18, 0),
        Margin = new Thickness(0, 0, 8, 0),
        Background = bg, Foreground = BrBlanco,
        FontWeight = FontWeights.SemiBold, FontSize = 13,
        BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    // ── Grids de datos ───────────────────────────────────────────────────────

    private Grid BuildClienteGrid()
    {
        var g = new Grid();
        // 6 columnas: label | valor | label | valor | label | valor
        for (int i = 0; i < 12; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition {
                Width = i % 2 == 0 ? new GridLength(100) : new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _tbCliNombre   = Tb(""); _tbCliCi     = Tb(""); _tbCliDir      = Tb("");
        _tbCliCel      = Tb(""); _tbCliCiudad = Tb(""); _tbCliCondicion= Tb("");
        _tbCliCredMax  = Tb(""); _tbCliSaldo  = Tb(""); _tbCliTipo     = Tb("");
        _tbCliEcv      = Tb(""); _tbCliVencCi = Tb(""); _tbCliConyuge  = Tb("");
        var tbCliRuc   = Tb("");  // RUC separado — no reusar _tbCliCiudad

        // Fila 0: nombre Cliente ocupa cols 0-5, luego CI, RUC, Dirección
        // Col 0-1 = botón Cliente + nombre
        var btnCliente = BtnAccion("Cliente", "#FF8C00");
        btnCliente.IsEnabled = false;
        Grid.SetRow(btnCliente, 0); Grid.SetColumn(btnCliente, 0); g.Children.Add(btnCliente);
        Grid.SetRow(_tbCliNombre, 0); Grid.SetColumn(_tbCliNombre, 1);
        Grid.SetColumnSpan(_tbCliNombre, 5); g.Children.Add(_tbCliNombre);

        AddCell(g, "C.I:",       _tbCliCi,        0, 6);
        AddCell(g, "RUC:",       tbCliRuc,        0, 8);
        AddCell(g, "Dirección:", _tbCliDir,       0, 10);

        // Fila 1
        AddCell(g, "Celular:",      _tbCliCel,       1, 0);
        AddCell(g, "Ciudad:",       _tbCliCiudad,    1, 2);
        AddCell(g, "Estado Civil:", _tbCliEcv,       1, 4);
        AddCell(g, "Condición:",    _tbCliCondicion, 1, 6);
        AddCell(g, "Tipo:",         _tbCliTipo,      1, 8);

        // Fila 2
        AddCell(g, "Crédito máx:", _tbCliCredMax, 2, 0);
        AddCell(g, "Saldo actual:", _tbCliSaldo,   2, 2);
        AddCell(g, "Cónyuge:",     _tbCliConyuge,  2, 4);
        AddCell(g, "Venc. C.I:",   _tbCliVencCi,   2, 8);

        // Fila 3: botones Historial y Ver cédula
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var btnHistorial = BtnAccion("Historial", "#6B7280");
        btnHistorial.Click += (_, _) => MostrarHistorial();
        Grid.SetRow(btnHistorial, 3); Grid.SetColumn(btnHistorial, 0);
        Grid.SetColumnSpan(btnHistorial, 2); g.Children.Add(btnHistorial);

        var btnVerCedula = BtnAccion("Ver cédula", "#3B82F6");
        btnVerCedula.Click += (_, _) => VerCedula();
        Grid.SetRow(btnVerCedula, 3); Grid.SetColumn(btnVerCedula, 2);
        Grid.SetColumnSpan(btnVerCedula, 2); g.Children.Add(btnVerCedula);

        return g;
    }

    private Grid BuildGaranteGrid()
    {
        var g = new Grid();
        for (int i = 0; i < 12; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition {
                Width = i % 2 == 0 ? new GridLength(100) : new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _tbGarNombre  = Tb(""); _tbGarCi    = Tb(""); _tbGarDir   = Tb("");
        _tbGarCel     = Tb(""); _tbGarCiudad= Tb(""); _tbGarEmpresa=Tb("");
        _tbGarTelLab  = Tb(""); _tbGarEcv   = Tb("");

        var btnGar = BtnAccion("Garante", "#FF8C00");
        btnGar.Click += (_, _) => MostrarInfoGarante();
        Grid.SetRow(btnGar, 0); Grid.SetColumn(btnGar, 0); g.Children.Add(btnGar);
        Grid.SetRow(_tbGarNombre, 0); Grid.SetColumn(_tbGarNombre, 1);
        Grid.SetColumnSpan(_tbGarNombre, 5); g.Children.Add(_tbGarNombre);
        AddCell(g, "C.I:",          _tbGarCi,      0, 6);
        AddCell(g, "Dirección:",    _tbGarDir,     0, 8);
        AddCell(g, "Teléfono:",     _tbGarCel,     0, 10);
        AddCell(g, "Lugar trabajo:",_tbGarEmpresa, 1, 0, colSpanVal: 3);
        AddCell(g, "Tel. laboral:", _tbGarTelLab,  1, 4);
        AddCell(g, "Estado Civil:", _tbGarEcv,     1, 6);

        return g;
    }

    private Grid BuildReferenciasGrid()
    {
        // 6 columnas: [btn+label 90] [valor *] [label 90] [valor *] [label 90] [valor *]
        // repetido para dos mitades (Ref1: cols 0-5, Ref2: cols 6-11)
        var g = new Grid();
        for (int i = 0; i < 12; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition {
                Width = i % 2 == 0 ? new GridLength(100) : new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _tbRef1Nom = Tb(""); _tbRef1Tel = Tb(""); _tbRef1Trab = Tb("");
        _tbRef2Nom = Tb(""); _tbRef2Tel = Tb(""); _tbRef2Trab = Tb("");
        _tbRefCom1Nom = Tb(""); _tbRefCom1Tel = Tb("");
        _tbRefCom2Nom = Tb(""); _tbRefCom2Tel = Tb("");

        // ---- Fila 0: Ref. Personal 1 (cols 0-5) ----
        var btnRef1 = BtnCirculoRojo("Ref. Pers. 1:");
        btnRef1.Click += (_, _) => MostrarInfoRef(1);
        Grid.SetRow(btnRef1, 0); Grid.SetColumn(btnRef1, 0); g.Children.Add(btnRef1);
        Grid.SetRow(_tbRef1Nom, 0); Grid.SetColumn(_tbRef1Nom, 1); g.Children.Add(_tbRef1Nom);
        var lb1Tel = LbField("Celular:");
        Grid.SetRow(lb1Tel, 0); Grid.SetColumn(lb1Tel, 2); g.Children.Add(lb1Tel);
        Grid.SetRow(_tbRef1Tel, 0); Grid.SetColumn(_tbRef1Tel, 3); g.Children.Add(_tbRef1Tel);
        var lb1Tr = LbField("Lugar trab.:");
        Grid.SetRow(lb1Tr, 0); Grid.SetColumn(lb1Tr, 4); g.Children.Add(lb1Tr);
        Grid.SetRow(_tbRef1Trab, 0); Grid.SetColumn(_tbRef1Trab, 5); g.Children.Add(_tbRef1Trab);

        // ---- Fila 0: Ref. Personal 2 (cols 6-11) ----
        var btnRef2 = BtnCirculoRojo("Ref. Pers. 2:");
        btnRef2.Click += (_, _) => MostrarInfoRef(2);
        Grid.SetRow(btnRef2, 0); Grid.SetColumn(btnRef2, 6); g.Children.Add(btnRef2);
        Grid.SetRow(_tbRef2Nom, 0); Grid.SetColumn(_tbRef2Nom, 7); g.Children.Add(_tbRef2Nom);
        var lb2Tel = LbField("Celular:");
        Grid.SetRow(lb2Tel, 0); Grid.SetColumn(lb2Tel, 8); g.Children.Add(lb2Tel);
        Grid.SetRow(_tbRef2Tel, 0); Grid.SetColumn(_tbRef2Tel, 9); g.Children.Add(_tbRef2Tel);
        var lb2Tr = LbField("Lugar trab.:");
        Grid.SetRow(lb2Tr, 0); Grid.SetColumn(lb2Tr, 10); g.Children.Add(lb2Tr);
        Grid.SetRow(_tbRef2Trab, 0); Grid.SetColumn(_tbRef2Trab, 11); g.Children.Add(_tbRef2Trab);

        // ---- Fila 1: Ref. Comerciales ----
        AddCell(g, "Ref. Com. 1:", _tbRefCom1Nom, 1, 0, colSpanVal: 3);
        AddCell(g, "Teléfono:",    _tbRefCom1Tel, 1, 4);
        AddCell(g, "Ref. Com. 2:", _tbRefCom2Nom, 1, 6, colSpanVal: 3);
        AddCell(g, "Teléfono:",    _tbRefCom2Tel, 1, 10);

        return g;
    }

    // Botón con círculo rojo + texto (para encabezados de referencias)
    private static Button BtnCirculoRojo(string texto)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new Border
        {
            Width = 13, Height = 13, CornerRadius = new CornerRadius(7),
            Background = BrRojo, Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = texto, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, VerticalAlignment = VerticalAlignment.Center
        });
        return new Button
        {
            Content = sp, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(2),
            Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left
        };
    }

    private static TextBlock LbField(string texto) =>
        new TextBlock { Text = texto, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, Margin = new Thickness(4, 2, 2, 2),
            VerticalAlignment = VerticalAlignment.Center };

    private void MostrarInfoRef(int numero)
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        if (win.ShowDialog() != true || win.PersonaSeleccionada == null) return;

        var p = win.PersonaSeleccionada;
        if (numero == 1)
        {
            _tbRef1Nom.Text  = p.Nombre;
            _tbRef1Tel.Text  = p.Telefono;
            _tbRef1Trab.Text = p.Empresa;
        }
        else
        {
            _tbRef2Nom.Text  = p.Nombre;
            _tbRef2Tel.Text  = p.Telefono;
            _tbRef2Trab.Text = p.Empresa;
        }
    }

    private void MostrarInfoGarante()
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        if (win.ShowDialog() != true || win.PersonaSeleccionada == null) return;

        var p = win.PersonaSeleccionada;
        _tbGarNombre.Text  = p.Nombre;
        _tbGarCi.Text      = p.Ci;
        _tbGarDir.Text     = p.Direccion;
        _tbGarCiudad.Text  = p.Ciudad;
        _tbGarCel.Text     = p.Telefono;
        _tbGarEmpresa.Text = p.Empresa;
        _tbGarTelLab.Text  = p.TelLaboral;
        _tbGarEcv.Text     = p.Ecv;
        _idGaranteCargado  = p.Id;
        _nomGaranteCargado = p.Nombre;
    }

    private UIElement BuildMercaderiasContent()
    {
        _gridProductos = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            ColumnHeaderHeight = 28, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            BorderThickness = new Thickness(0),
            Background = BrCard,
            RowBackground = BrCard,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
        };

        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, BrSecHead));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(4,2,4,2)));
        _gridProductos.ColumnHeaderStyle = hdrStyle;

        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Codigo"),                   Width = 80 });
        var colDesc = new DataGridTextColumn {
            Header  = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"),
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
        };
        var descStyle = new Style(typeof(TextBlock));
        descStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        colDesc.ElementStyle = descStyle;
        _gridProductos.Columns.Add(colDesc);
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Precio",      Binding = new System.Windows.Data.Binding("Precio")     { StringFormat = "N0" }, Width = 80 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Entrega",     Binding = new System.Windows.Data.Binding("Entrega")    { StringFormat = "N0" }, Width = 80 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Cuotas",      Binding = new System.Windows.Data.Binding("Cuotas"),                   Width = 55 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Costo mens.", Binding = new System.Windows.Data.Binding("CostoMens")  { StringFormat = "N0" }, Width = 90 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Total final", Binding = new System.Windows.Data.Binding("ValorFinal") { StringFormat = "N0" }, Width = 90 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Cantidad",    Binding = new System.Windows.Data.Binding("Cantidad")   { StringFormat = "N0" }, Width = 60 });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("TotalGral")  { StringFormat = "N0" }, Width = 90 });

        // Fila de Total debajo del grid
        var totalBorder = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 247, 237)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var totalSp = new StackPanel { Orientation = Orientation.Horizontal };

        TextBox TotalBox(string label, string valor) {
            totalSp.Children.Add(new TextBlock {
                Text = label, FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = BrLabelTxt, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            var tb = new TextBox {
                Text = valor, FontWeight = FontWeights.Bold, FontSize = 13, IsReadOnly = true,
                Background = BrCard, Padding = new Thickness(10, 4, 10, 4), MinWidth = 130,
                BorderBrush = BrBorde, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 20, 0), Foreground = BrPrimary
            };
            totalSp.Children.Add(tb);
            return tb;
        }
        TotalBox("Total:", _sol.TotalVenta.ToString("N0"));
        TotalBox("Entrega:", _sol.Entrega.ToString("N0"));

        totalBorder.Child = totalSp;

        var sp = new StackPanel();
        sp.Children.Add(_gridProductos);
        sp.Children.Add(totalBorder);
        return sp;
    }

    private Border BuildPanelLateral()
    {
        _cmbMetodoPago = new ComboBox {
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(6, 4, 6, 4),
            FontSize = 12
        };
        _cmbMetodoPago.Items.Add("Efectivo");
        _cmbMetodoPago.Items.Add("Cheque");
        _cmbMetodoPago.Items.Add("Transferencia");
        _cmbMetodoPago.Items.Add("Tarjeta");
        _cmbMetodoPago.SelectedIndex = 0;

        _txtNroMetodo = new TextBox {
            Padding = new Thickness(6, 4, 6, 4), FontSize = 12,
            Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1)
        };

        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(new TextBlock {
            Text = "Método de pago", FontWeight = FontWeights.Bold, FontSize = 11,
            Foreground = BrLabelTxt, Margin = new Thickness(0, 0, 0, 6)
        });
        sp.Children.Add(_cmbMetodoPago);
        sp.Children.Add(new TextBlock {
            Text = "N° de referencia", FontSize = 11, Foreground = BrLabelTxt,
            Margin = new Thickness(0, 8, 0, 4)
        });
        sp.Children.Add(_txtNroMetodo);

        return new Border {
            Background = BrCard,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 4),
            Child = sp,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private async Task CambiarEstadoAsync(byte nuevoEstado)
    {
        // Verif. Datos: solo mostrar avisos
        if (nuevoEstado == 0)
        {
            var (avisos, _) = await ObtenerVerificacionAsync();
            MostrarModalVerificacion(avisos);
            return;
        }
        // Rechazar: confirmación simple
        if (nuevoEstado == 2)
        {
            if (MessageBox.Show("¿Confirmar RECHAZAR esta solicitud?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await AplicarCambioEstadoAsync(nuevoEstado);
            return;
        }
        // Aceptar: modal con alertas + datos cliente + confirmación
        if (nuevoEstado == 1)
        {
            var (avisos, datosCliente) = await ObtenerVerificacionAsync();
            var modalResult = MostrarModalAprobar(avisos, datosCliente);
            if (!modalResult.Confirmado) return;
            await AplicarCambioEstadoAsync(nuevoEstado, modalResult.NRecibo, modalResult.NPagare, modalResult.Metodo, modalResult.NumPago);
        }
    }

    private async Task AplicarCambioEstadoAsync(byte nuevoEstado, string nRecibo = "", string nPagare = "", byte metodo = 1, string numPago = "")
    {
        try
        {
            using var conn = _db.Create();

            // Actualizar ESTADO en la solicitud
            await conn.ExecuteAsync(
                "UPDATE CAB_SOL_SALES SET ESTADO=@Estado, FECHA_APROB=GETDATE() WHERE IDSOLICITUD=@Id",
                new { Estado = nuevoEstado, Id = _sol.IdSolicitud });

            // Si se aprueba, registrar la venta real usando GUARDAR_VENTA_CREDITO_CS
            if (nuevoEstado == 1)
                await RegistrarVentaAprobadaAsync(conn, nRecibo, nPagare, metodo, numPago);

            _sol.EstadoNum = nuevoEstado;
            _sol.Estado = nuevoEstado switch { 1 => "Aprobado", 2 => "Rechazado", _ => "Verificar" };

            var msg = nuevoEstado == 1
                ? "Solicitud aprobada y venta registrada correctamente."
                : "Estado actualizado correctamente.";
            MessageBox.Show(msg, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RegistrarVentaAprobadaAsync(System.Data.IDbConnection conn, string nRecibo = "", string nPagare = "", byte metodo = 1, string numPago = "")
    {
        var session = CrediSoft.Core.Services.SessionService.Instance;
        if (session.UsuarioActual == null || session.LocalActual == null)
            throw new InvalidOperationException("No hay sesión activa.");

        // Cargar datos completos de la solicitud desde CAB_SOL_SALES
        var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT s.ID_CLIENTE, s.ID_GARANTE, s.ID_LOCAL, s.ID_USUARIO," +
            "  s.NUMERO, s.CANTCUOTAS, s.TOTAL_MONTO_CUOTA, s.TOTALSALE, s.TOTALENTREGA," +
            "  s.FECHA_COBRO," +
            "  s.ID_REFERENCIA1, s.ID_REFERENCIA2," +
            "  s.NOM_REFERENCIA1, s.TEL_REFERENCIA1, s.TRAB_REFERENCIA1," +
            "  s.NOM_REFERENCIA2, s.TEL_REFERENCIA2, s.TRAB_REFERENCIA2," +
            "  s.NOM_REFERENCIACOMERCIAL1, s.TEL_REFERENCIACOMERCIAL1, s.TRAB_REFERENCIACOMERCIAL1," +
            "  s.NOM_REFERENCIACOMERCIAL2, s.TEL_REFERENCIACOMERCIAL2, s.TRAB_REFERENCIACOMERCIAL2," +
            "  s.I_SALARIO, s.I_HONORARIO, s.I_CONYUGE, s.I_OTROS, s.I_TOTAL," +
            "  s.E_GASTO, s.E_CUOTA, s.E_ALQUILER, s.E_OTROS, s.E_TOTAL" +
            " FROM CAB_SOL_SALES s WHERE s.IDSOLICITUD = @id",
            new { id = _sol.IdSolicitud });

        if (cab == null) throw new Exception("No se encontraron datos de la solicitud.");

        // Cargar detalles de DET_SOL_SALES para registrar cada artículo
        var detalles = (await conn.QueryAsync<dynamic>(
            "SELECT d.IDART, d.CA, d.D, d.PRECIO, d.ENTREGA, d.CANTCUOTAS," +
            "  d.COSTOMENSUAL, d.VALORFINAL, d.CANTIDAD, d.SUBTOTAL," +
            "  ISNULL(a.IVA, 0) as Iva, ISNULL(p.PC, 0) as Pc" +
            " FROM DET_SOL_SALES d" +
            " LEFT JOIN ARTICULOS a ON d.IDART = a.ID" +
            " LEFT JOIN PRICES p   ON d.IDART = p.IDART AND p.IDLOCAL = @loc" +
            " WHERE d.IDSOLICITUD = @id ORDER BY d.ID_DET_SOL",
            new { id = _sol.IdSolicitud, loc = (int?)cab.ID_LOCAL ?? session.LocalActual.IdLocal }))
            .ToList();

        if (detalles.Count == 0) throw new Exception("La solicitud no tiene artículos.");

        var ventaRepo = App.Services.GetRequiredService<IVentaRepository>();

        int    idCliente    = (int)cab.ID_CLIENTE;
        int    idGarante    = (int?)cab.ID_GARANTE ?? 0;
        byte   idLocal      = (byte?)cab.ID_LOCAL ?? (byte)session.LocalActual.IdLocal;
        int    idUsuario    = (int?)cab.ID_USUARIO ?? session.UsuarioActual.IdUsuario;
        string nSol         = ((string?)cab.NUMERO ?? "").Trim();
        byte   cuotas       = (byte?)cab.CANTCUOTAS ?? 1;
        decimal montoCuota  = (decimal?)cab.TOTAL_MONTO_CUOTA ?? 0;
        decimal totalSale   = (decimal?)cab.TOTALSALE ?? 0;
        decimal totalEnt    = (decimal?)cab.TOTALENTREGA ?? 0;
        decimal entNorm     = totalEnt;
        decimal entLog      = 0;
        DateTime fechaCobro = (DateTime?)cab.FECHA_COBRO ?? DateTime.Today.AddMonths(1);

        for (int i = 0; i < detalles.Count; i++)
        {
            var d = detalles[i];
            bool esPrimero = i == 0;

            decimal pc  = (decimal?)d.Pc  ?? 0;
            decimal iva = (decimal?)d.Iva ?? 0;

            var prm = new VentaCreditoParams(
                IdCab: 0, NSol: 0,
                IdLocal: idLocal, IdUsuario: idUsuario,
                IdCliente: idCliente, IdGarante: idGarante,
                IdRef1: (int?)cab.ID_REFERENCIA1 ?? 0,
                IdRef2: (int?)cab.ID_REFERENCIA2 ?? 0,
                NomRef1:  (string?)cab.NOM_REFERENCIA1  ?? "", TelRef1: (string?)cab.TEL_REFERENCIA1  ?? "", TrabRef1: (string?)cab.TRAB_REFERENCIA1  ?? "",
                NomRef2:  (string?)cab.NOM_REFERENCIA2  ?? "", TelRef2: (string?)cab.TEL_REFERENCIA2  ?? "", TrabRef2: (string?)cab.TRAB_REFERENCIA2  ?? "",
                NomRefCom1: (string?)cab.NOM_REFERENCIACOMERCIAL1 ?? "", TelRefCom1: (string?)cab.TEL_REFERENCIACOMERCIAL1 ?? "", TrabRefCom1: (string?)cab.TRAB_REFERENCIACOMERCIAL1 ?? "",
                NomRefCom2: (string?)cab.NOM_REFERENCIACOMERCIAL2 ?? "", TelRefCom2: (string?)cab.TEL_REFERENCIACOMERCIAL2 ?? "", TrabRefCom2: (string?)cab.TRAB_REFERENCIACOMERCIAL2 ?? "",
                FormaDeVenta: 2, MetodoDeVenta: 1, NTarjeta: "",
                Parcial: totalSale, Descuento: 0, Total: totalSale,
                EntregaNormal: entNorm, EntregaLogistica: entLog,
                Cuotas: cuotas, MontoCuota: montoCuota,
                Debe: totalSale - totalEnt, Haber: totalEnt,
                Cpha: 0, Estado: 1,
                Tiva: iva * (decimal)d.CANTIDAD * (decimal)d.PRECIO / 100m,
                IdDet: 0, IdArt: (int)d.IDART,
                Cantidad: (decimal)d.CANTIDAD,
                Pc: pc, Pv: (decimal)d.PRECIO, IvaArt: iva, EsArt: 1,
                IdPrices: 0, IdMovArt: 0, Mov: "E", Mod: "V",
                StIni: 0, PCant: 0,
                IdSolicitud: _sol.IdSolicitud, TCuotas: cuotas,
                IdDetCaja: 0, IdCabCaja: 0, Caja: 0,
                CountCaja: esPrimero ? 1 : 0,
                Accion: esPrimero ? (byte)1 : (byte)0,
                Concepto: 1,
                Monto: esPrimero ? entNorm : 0,
                Metodo: metodo, Numero: numPago, Para: 0, Obs: "",
                IdDoc: 0, NRecibo: nRecibo, NPagare: nPagare,
                FechaInicioExterna: fechaCobro, NVenta: 0,
                Agente: esPrimero ? "SI" : "NO");

            await ventaRepo.GuardarVentaCreditoAsync(prm);
        }
    }

    private async Task<(string avisos, string datosCliente)> ObtenerVerificacionAsync()
    {
        try
        {
            using var conn = _db.Create();
            var datos = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT s.ID_CLIENTE, s.ID_GARANTE, s.TOTALSALE," +
                "  cl.TIPO, cl.NOMBRE_CLIENTE, cl.CI_CLIENTE," +
                "  cl.TELEFONO_CLIENTE, cl.CIUDAD_CLIENTE, cl.CRED_MAX" +
                " FROM CAB_SOL_SALES s" +
                " LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE" +
                " WHERE s.IDSOLICITUD = @id", new { id = _sol.IdSolicitud });

            if (datos == null) return ("No se pudo cargar datos.", "");

            int     idCliente = (int)datos.ID_CLIENTE;
            int     idGarante = _idGaranteCargado > 0 ? _idGaranteCargado : ((int?)datos.ID_GARANTE ?? 0);
            byte    tipo      = (byte?)datos.TIPO      ?? 0;
            decimal totalSol  = (decimal?)datos.TOTALSALE ?? 0;
            decimal credMax   = (decimal?)datos.CRED_MAX  ?? 0;
            string  nomCli    = (string?)datos.NOMBRE_CLIENTE  ?? "";
            string  ciCli     = (string?)datos.CI_CLIENTE      ?? "";
            string  telCli    = (string?)datos.TELEFONO_CLIENTE ?? "";
            string  ciudadCli = (string?)datos.CIUDAD_CLIENTE   ?? "";
            string  tipoTxt   = tipo switch { 1=>"Bueno", 2=>"Regular", 3=>"Malo", _=>"—" };
            string  garTxt    = idGarante > 0
                ? (_nomGaranteCargado.Length > 0 ? _nomGaranteCargado : "Asignado")
                : "Sin garante";

            var sb = new System.Text.StringBuilder();

            int totalCreditos = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CABECERA_SALES WHERE ID_CLIENTE = @id AND FORMA_DE_VENTA = 2",
                new { id = idCliente });

            if (totalCreditos == 0)
                sb.AppendLine("• Cliente nuevo: sin ventas a crédito previas.");

            if ((totalCreditos == 0 || tipo >= 2) && idGarante == 0)
                sb.AppendLine($"• Tipo de cliente: {tipoTxt}. No tiene garante asignado.");

            decimal deudaActiva = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(DEBE - HABER), 0) FROM CABECERA_SALES" +
                " WHERE ID_CLIENTE = @id AND ESTADO = 1 AND DEBE > HABER",
                new { id = idCliente });

            if (deudaActiva > 0)
                sb.AppendLine($"• Deuda activa: {deudaActiva:N0} Gs.");

            if (credMax > 0 && totalSol > credMax)
                sb.AppendLine($"• Monto ({totalSol:N0} Gs.) supera crédito máximo ({credMax:N0} Gs.).");

            string datosCliente =
                $"Nombre:        {nomCli}\n" +
                $"C.I.:          {ciCli}\n" +
                $"Teléfono:      {telCli}\n" +
                $"Ciudad:        {ciudadCli}\n" +
                $"Tipo:          {tipoTxt}\n" +
                $"Garante:       {garTxt}\n" +
                $"Total sol.:    {totalSol:N0} Gs.\n" +
                $"Crédito máx.:  {(credMax > 0 ? credMax.ToString("N0") + " Gs." : "Sin límite")}";

            return (sb.ToString().TrimEnd(), datosCliente);
        }
        catch (Exception ex)
        {
            return ($"Error en verificación: {ex.Message}", "");
        }
    }

    private void MostrarModalVerificacion(string avisos)
    {
        var win = new Window {
            Title = "Verif. Datos", Width = 460, Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White
        };
        var sp = new StackPanel { Margin = new Thickness(16) };
        if (string.IsNullOrEmpty(avisos))
        {
            sp.Children.Add(new TextBlock {
                Text = "Verificación completada. No se encontraron observaciones.\nPuede proceder a Aprobar o Rechazar.",
                TextWrapping = TextWrapping.Wrap, FontSize = 13
            });
        }
        else
        {
            sp.Children.Add(new TextBlock {
                Text = "OBSERVACIONES:", FontWeight = FontWeights.Bold, FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 80, 0)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            sp.Children.Add(new TextBlock {
                Text = avisos, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });
            sp.Children.Add(new TextBlock {
                Text = "Revise los datos antes de Aprobar o Rechazar.",
                FontStyle = FontStyles.Italic, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            });
        }
        var btnOk = new Button {
            Content = "Aceptar", Width = 90, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 100, 180)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold
        };
        btnOk.Click += (_, _) => win.Close();
        sp.Children.Add(btnOk);
        win.Content = sp;
        win.ShowDialog();
    }

    private record AprobarModalResult(bool Confirmado, string NRecibo, string NPagare, byte Metodo, string NumPago);

    private AprobarModalResult MostrarModalAprobar(string avisos, string datosCliente)
    {
        bool  confirmado = false;
        bool  hayAvisos  = !string.IsNullOrEmpty(avisos);

        var win = new Window {
            Title = "Aprobar Solicitud", Width = 520, Height = hayAvisos ? 560 : 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel { Margin = new Thickness(16) };
        scroll.Content = root;

        // Alertas (si hay)
        if (hayAvisos)
        {
            var alertBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 140, 0)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 12)
            };
            var alertSp = new StackPanel();
            alertSp.Children.Add(new TextBlock {
                Text = "⚠  OBSERVACIONES:", FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 80, 0)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            alertSp.Children.Add(new TextBlock {
                Text = avisos, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 50, 0))
            });
            alertBorder.Child = alertSp;
            root.Children.Add(alertBorder);
        }

        // Pregunta de confirmación
        root.Children.Add(new TextBlock {
            Text = "¿Está seguro de querer APROBAR esta solicitud?",
            FontWeight = FontWeights.Bold, FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 100, 30)),
            Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap
        });

        // Datos del cliente
        var datosBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(235, 245, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(150, 180, 220)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 14)
        };
        datosBorder.Child = new TextBlock {
            Text = datosCliente, FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(datosBorder);

        // Campos de registro del pago
        var pagoGroup = new Border {
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 14),
            Background = new SolidColorBrush(Color.FromRgb(248, 252, 248))
        };
        var pagoSp = new StackPanel();
        pagoSp.Children.Add(new TextBlock {
            Text = "DATOS DE REGISTRO", FontWeight = FontWeights.Bold, FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 80, 40)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        TextBlock Lbl(string t) => new TextBlock {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Margin = new Thickness(0, 0, 0, 3)
        };
        TextBox Txt() => new TextBox {
            Height = 26, Padding = new Thickness(6, 2, 6, 2), FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };

        pagoSp.Children.Add(Lbl("N° de Recibo"));
        var txtRecibo = Txt();
        pagoSp.Children.Add(txtRecibo);

        pagoSp.Children.Add(Lbl("N° de Pagaré"));
        var txtPagare = Txt();
        pagoSp.Children.Add(txtPagare);

        pagoSp.Children.Add(Lbl("Método de pago/entrega"));
        var cboMetodo = new ComboBox { Height = 26, Margin = new Thickness(0, 0, 0, 8) };
        cboMetodo.Items.Add(new ComboBoxItem { Content = "EFECTIVO",        Tag = (byte)1, IsSelected = true });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta débito",  Tag = (byte)2 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta crédito", Tag = (byte)3 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia",   Tag = (byte)4 });
        cboMetodo.SelectedIndex = 0;
        pagoSp.Children.Add(cboMetodo);

        var lblNumPago = Lbl("N° (tarjeta / comprobante)");
        var txtNumPago = Txt();
        txtNumPago.Margin = new Thickness(0, 0, 0, 0);
        pagoSp.Children.Add(lblNumPago);
        pagoSp.Children.Add(txtNumPago);

        cboMetodo.SelectionChanged += (_, _) => {
            bool esEfectivo = cboMetodo.SelectedItem is ComboBoxItem ci && (byte)(ci.Tag ?? (byte)1) == 1;
            lblNumPago.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            txtNumPago.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
        };
        lblNumPago.Visibility = Visibility.Collapsed;
        txtNumPago.Visibility = Visibility.Collapsed;

        pagoGroup.Child = pagoSp;
        root.Children.Add(pagoGroup);

        // Botones
        var barBtns = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnSi = new Button {
            Content = "Sí, Aprobar", Width = 110, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 140, 60)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnNo = new Button {
            Content = "Cancelar", Width = 90, Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(180, 40, 40)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        btnSi.Click += (_, _) => { confirmado = true;  win.Close(); };
        btnNo.Click += (_, _) => { confirmado = false; win.Close(); };
        barBtns.Children.Add(btnSi);
        barBtns.Children.Add(btnNo);
        root.Children.Add(barBtns);

        win.Content = scroll;
        win.ShowDialog();

        byte metodoVal = cboMetodo.SelectedItem is ComboBoxItem selItem
            ? (byte)(selItem.Tag ?? (byte)1)
            : (byte)1;

        return new AprobarModalResult(
            confirmado,
            txtRecibo.Text.Trim(),
            txtPagare.Text.Trim(),
            metodoVal,
            txtNumPago.Text.Trim());
    }

    private UIElement BuildSeccionDatos()
    {
        _tbDatAprobado = Tb(_sol.FechaSolicitud.ToString("dd/MM/yyyy"));
        _tbDatTotal    = Tb(_sol.TotalVenta.ToString("N0"));
        _tbDatEntrega  = Tb(_sol.Entrega.ToString("N0"));
        _tbDatCuotas   = Tb(_sol.Cuotas.ToString());
        _tbDatMonto    = Tb(_sol.Cuotas > 0
            ? ((_sol.TotalVenta - _sol.Entrega) / _sol.Cuotas).ToString("N0") : "0");
        _tbDatPago     = Tb("—");

        // Barra de resumen financiero (naranja oscuro)
        var bar = new Border {
            Background = BrPrimDark,
            Padding = new Thickness(12, 6, 12, 6)
        };
        var barSp = new StackPanel { Orientation = Orientation.Horizontal };
        void AddStat(string lbl, string val) {
            var col = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            col.Children.Add(new TextBlock {
                Text = lbl, FontSize = 10, Foreground = BrBlanco, Opacity = 0.8
            });
            col.Children.Add(new TextBlock {
                Text = val, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrBlanco
            });
            barSp.Children.Add(col);
        }
        AddStat("Total venta",   _sol.TotalVenta.ToString("N0") + " Gs.");
        AddStat("Entrega",       _sol.Entrega.ToString("N0")    + " Gs.");
        AddStat("Cuotas",        _sol.Cuotas.ToString());
        AddStat("Costo cuota",   _sol.Cuotas > 0
            ? ((_sol.TotalVenta - _sol.Entrega) / _sol.Cuotas).ToString("N0") + " Gs." : "—");
        AddStat("Fecha sol.",    _sol.FechaSolicitud.ToString("dd/MM/yyyy"));
        bar.Child = barSp;

        var outer = new StackPanel();
        outer.Children.Add(SecGroup("Resumen financiero", bar));
        return outer;
    }

    private UIElement BuildFilaLogistica()
    {
        var row = new Grid { Margin = new Thickness(0,2,0,0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _txtLogistica = new TextBox { Padding = new Thickness(6,4,6,4), FontSize = 12, Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1) };
        _txtRecibo    = new TextBox { Padding = new Thickness(6,4,6,4), FontSize = 12, Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1) };
        _txtPagare    = new TextBox { Padding = new Thickness(6,4,6,4), FontSize = 12, Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1) };

        void Set(UIElement el, int col) { Grid.SetColumn(el, col); row.Children.Add(el); }
        Set(new TextBlock { Text = "Logística:", FontWeight = FontWeights.SemiBold, FontSize = 12,
            Foreground = BrLabelTxt, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) }, 0);
        Set(_txtLogistica, 1);
        Set(new TextBlock { Text = "N° de recibo:", FontWeight = FontWeights.SemiBold, FontSize = 12,
            Foreground = BrLabelTxt, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) }, 3);
        Set(_txtRecibo, 4);
        Set(new TextBlock { Text = "N° de Pagaré:", FontWeight = FontWeights.SemiBold, FontSize = 12,
            Foreground = BrLabelTxt, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) }, 6);
        Set(_txtPagare, 7);

        var brd = new Border {
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 4)
        };
        brd.Child = row;
        return brd;
    }

    private Grid BuildIngresosEgresosGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // etiqueta
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // INGRESOS val
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // etiqueta egresos
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // EGRESOS val
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Encabezados
        var hIng = new TextBlock { Text = "INGRESOS", FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = BrBlanco,
            Background = BrVerde, Padding = new Thickness(4, 3, 4, 3), FontSize = 11 };
        Grid.SetColumn(hIng, 0); Grid.SetColumnSpan(hIng, 2); Grid.SetRow(hIng, 0);
        g.Children.Add(hIng);

        var hEgr = new TextBlock { Text = "EGRESOS", FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = BrBlanco,
            Background = BrRojo, Padding = new Thickness(4, 3, 4, 3), FontSize = 11 };
        Grid.SetColumn(hEgr, 2); Grid.SetColumnSpan(hEgr, 2); Grid.SetRow(hEgr, 0);
        g.Children.Add(hEgr);

        _tbISalario   = Tb("0"); _tbIHonorario = Tb("0"); _tbIConyuge = Tb("0");
        _tbIOtros     = Tb("0"); _tbITotal     = Tb("0");
        _tbEGasto     = Tb("0"); _tbECuota     = Tb("0"); _tbEAlquiler= Tb("0");
        _tbEOtros     = Tb("0"); _tbETotal     = Tb("0"); _tbSaldo    = Tb("0");

        AddIERow(g, 1, "Salario",        _tbISalario,   "Gastos familiares", _tbEGasto);
        AddIERow(g, 2, "Honorario Prof.",_tbIHonorario, "Cuotas",            _tbECuota);
        AddIERow(g, 3, "Salario Cónyuge",_tbIConyuge,   "Alquiler",          _tbEAlquiler);
        AddIERow(g, 4, "Otros",          _tbIOtros,     "Otros",             _tbEOtros);

        var totalBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246));
        // TOTAL row
        var lbTI = new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold,
            FontSize = 11, Padding = new Thickness(4, 3, 4, 3), Background = totalBg };
        Grid.SetColumn(lbTI, 0); Grid.SetRow(lbTI, 5); g.Children.Add(lbTI);
        _tbITotal.FontWeight = FontWeights.Bold; _tbITotal.Background = totalBg;
        _tbITotal.Foreground = BrVerde;
        Grid.SetColumn(_tbITotal, 1); Grid.SetRow(_tbITotal, 5); g.Children.Add(_tbITotal);

        var lbTE = new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold,
            FontSize = 11, Padding = new Thickness(4, 3, 4, 3), Background = totalBg };
        Grid.SetColumn(lbTE, 2); Grid.SetRow(lbTE, 5); g.Children.Add(lbTE);
        _tbETotal.FontWeight = FontWeights.Bold; _tbETotal.Background = totalBg;
        _tbETotal.Foreground = BrRojo;
        Grid.SetColumn(_tbETotal, 3); Grid.SetRow(_tbETotal, 5); g.Children.Add(_tbETotal);

        // SALDO
        var saldoBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 247, 237));
        var lbS = new TextBlock { Text = "SALDO:", FontWeight = FontWeights.Bold, FontSize = 11,
            Padding = new Thickness(4, 3, 4, 3), Background = saldoBg, Foreground = BrPrimDark };
        Grid.SetColumn(lbS, 0); Grid.SetColumnSpan(lbS, 2); Grid.SetRow(lbS, 6); g.Children.Add(lbS);
        _tbSaldo.FontWeight = FontWeights.Bold; _tbSaldo.Background = saldoBg;
        _tbSaldo.Foreground = BrPrimDark;
        Grid.SetColumn(_tbSaldo, 2); Grid.SetColumnSpan(_tbSaldo, 2); Grid.SetRow(_tbSaldo, 6);
        g.Children.Add(_tbSaldo);

        return g;
    }

    private static void AddIERow(Grid g, int row, string lblI, TextBlock valI, string lblE, TextBlock valE)
    {
        var li = new TextBlock { Text = lblI, Padding = new Thickness(4,2,4,2) };
        Grid.SetColumn(li, 0); Grid.SetRow(li, row); g.Children.Add(li);
        Grid.SetColumn(valI, 1); Grid.SetRow(valI, row); g.Children.Add(valI);
        var le = new TextBlock { Text = lblE, Padding = new Thickness(4,2,4,2) };
        Grid.SetColumn(le, 2); Grid.SetRow(le, row); g.Children.Add(le);
        Grid.SetColumn(valE, 3); Grid.SetRow(valE, row); g.Children.Add(valE);
    }

    // ── Carga de datos ────────────────────────────────────────────────────────
    private async Task CargarDetalleAsync()
    {
        try
        {
            using var conn = _db.Create();

            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT " +
                "  s.ID_CLIENTE as IdCli, s.ID_GARANTE as IdGar," +
                "  cl.NOMBRE_CLIENTE as NomCli, cl.CI_CLIENTE as Ci, cl.RUC_CLIENTE as Ruc," +
                "  cl.DIRECCION_CLIENTE as Dir, cl.TELEFONO_CLIENTE as Cel, cl.CIUDAD_CLIENTE as Ciudad," +
                "  cl.ECV as Ecv, cl.CRED_MAX as CredMax, cl.CONDICION as Condicion," +
                "  cl.TIPO as Tipo, cl.VENC_CEDULA as VencCi, cl.CONYUGE as Conyuge," +
                "  g.NOMBRE_CLIENTE as NomGar, g.CI_CLIENTE as CiGar," +
                "  g.DIRECCION_CLIENTE as DirGar, g.TELEFONO_CLIENTE as CelGar," +
                "  g.CIUDAD_CLIENTE as CiudadGar, g.EMPRESA_LABORAL as EmpGar," +
                "  g.TELEFONO_LABORAL as TelLabGar, g.ECV as EcvGar," +
                "  s.NOTA as Nota," +
                "  s.NOM_REFERENCIA1 as Ref1Nom, s.TEL_REFERENCIA1 as Ref1Tel, s.TRAB_REFERENCIA1 as Ref1Trab," +
                "  s.NOM_REFERENCIA2 as Ref2Nom, s.TEL_REFERENCIA2 as Ref2Tel, s.TRAB_REFERENCIA2 as Ref2Trab," +
                "  s.NOM_REFERENCIACOMERCIAL1 as RefC1Nom, s.TEL_REFERENCIACOMERCIAL1 as RefC1Tel," +
                "  s.NOM_REFERENCIACOMERCIAL2 as RefC2Nom, s.TEL_REFERENCIACOMERCIAL2 as RefC2Tel," +
                "  s.I_SALARIO as ISal, s.I_HONORARIO as IHon, s.I_CONYUGE as ICon," +
                "  s.I_OTROS as IOtros, s.I_TOTAL as ITotal," +
                "  s.E_GASTO as EGas, s.E_CUOTA as ECuo, s.E_ALQUILER as EAlq," +
                "  s.E_OTROS as EOtros, s.E_TOTAL as ETotal" +
                " FROM CAB_SOL_SALES s" +
                " LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE" +
                " LEFT JOIN CLIENTES g  ON s.ID_GARANTE  = g.ID_CLIENTE" +
                " WHERE s.IDSOLICITUD = @id", new { id = _sol.IdSolicitud });

            if (cab != null)
            {
                static string S(object? v) => v?.ToString()?.Trim() is { Length: > 0 } s ? s : "—";
                static string N(object? v) => v is decimal d ? d.ToString("N0") : "0";

                // Cliente
                _tbCliNombre.Text    = S(cab.NomCli);
                _tbCliCi.Text        = S(cab.Ci);
                _tbCliDir.Text       = S(cab.Dir);
                _tbCliCel.Text       = S(cab.Cel);
                _tbCliCiudad.Text    = S(cab.Ciudad);
                _tbCliEcv.Text       = ((byte?)cab.Ecv) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" };
                _tbCliCredMax.Text   = N(cab.CredMax);
                _tbCliCondicion.Text = ((byte?)cab.Condicion) switch { 1=>"Activo",2=>"Inactivo",_=>"—" };
                _tbCliTipo.Text      = ((byte?)cab.Tipo) switch { 1=>"Bueno",2=>"Regular",3=>"Malo",_=>"—" };
                _tbCliVencCi.Text    = cab.VencCi is DateTime vd ? vd.ToString("dd/MM/yyyy") : "—";
                _tbCliConyuge.Text   = S(cab.Conyuge);
                _tbCliSaldo.Text     = "—";

                // Garante
                _tbGarNombre.Text   = S(cab.NomGar);
                _tbGarCi.Text       = S(cab.CiGar);
                _tbGarDir.Text      = S(cab.DirGar);
                _tbGarCel.Text      = S(cab.CelGar);
                _tbGarCiudad.Text   = S(cab.CiudadGar);
                _tbGarEmpresa.Text  = S(cab.EmpGar);
                _tbGarTelLab.Text   = S(cab.TelLabGar);
                _tbGarEcv.Text      = ((byte?)cab.EcvGar) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" };

                // Referencias
                _tbRef1Nom.Text    = S(cab.Ref1Nom);  _tbRef1Tel.Text  = S(cab.Ref1Tel);  _tbRef1Trab.Text = S(cab.Ref1Trab);
                _tbRef2Nom.Text    = S(cab.Ref2Nom);  _tbRef2Tel.Text  = S(cab.Ref2Tel);  _tbRef2Trab.Text = S(cab.Ref2Trab);
                _tbRefCom1Nom.Text = S(cab.RefC1Nom); _tbRefCom1Tel.Text = S(cab.RefC1Tel);
                _tbRefCom2Nom.Text = S(cab.RefC2Nom); _tbRefCom2Tel.Text = S(cab.RefC2Tel);

                // Ingresos / Egresos
                _tbISalario.Text   = N(cab.ISal);   _tbIHonorario.Text = N(cab.IHon);
                _tbIConyuge.Text   = N(cab.ICon);   _tbIOtros.Text     = N(cab.IOtros);
                _tbITotal.Text     = N(cab.ITotal);
                _tbEGasto.Text     = N(cab.EGas);   _tbECuota.Text     = N(cab.ECuo);
                _tbEAlquiler.Text  = N(cab.EAlq);   _tbEOtros.Text     = N(cab.EOtros);
                _tbETotal.Text     = N(cab.ETotal);
                decimal saldo = ((decimal?)cab.ITotal ?? 0) - ((decimal?)cab.ETotal ?? 0);
                _tbSaldo.Text = saldo.ToString("N0");

                // Nota
                _txtNota.Text = (string?)cab.Nota ?? "";

                // Guardar estado para botones Historial / Ver cédula / Garante
                _idClienteCargado  = (int?)cab.IdCli  ?? 0;
                _fotoCedulaCliente = S(cab.Ci) == "—" ? "" : S(cab.Ci);
                _idGaranteCargado  = (int?)cab.IdGar  ?? 0;
                _nomGaranteCargado = S(cab.NomGar);
            }

            // Productos
            var detalles = await conn.QueryAsync<dynamic>(
                "SELECT a.CA as Codigo, a.D as Descripcion," +
                "  d.PRECIO as Precio, d.ENTREGA as Entrega, d.CANTCUOTAS as Cuotas," +
                "  d.COSTOMENSUAL as CostoMens, d.VALORFINAL as ValorFinal," +
                "  d.CANTIDAD as Cantidad, d.SUBTOTAL as TotalGral" +
                " FROM DET_SOL_SALES d" +
                " LEFT JOIN ARTICULOS a ON d.IDART = a.ID" +
                " WHERE d.IDSOLICITUD = @id ORDER BY a.D", new { id = _sol.IdSolicitud });

            _gridProductos.ItemsSource = detalles.Select(d => new DetalleSolRow {
                Codigo      = (string?)d.Codigo      ?? "",
                Descripcion = (string?)d.Descripcion ?? "",
                Precio      = (decimal?)d.Precio     ?? 0,
                Entrega     = (decimal?)d.Entrega    ?? 0,
                Cuotas      = (int?)d.Cuotas         ?? 0,
                CostoMens   = (decimal?)d.CostoMens  ?? 0,
                ValorFinal  = (decimal?)d.ValorFinal ?? 0,
                Cantidad    = (decimal?)d.Cantidad   ?? 0,
                TotalGral   = (decimal?)d.TotalGral  ?? 0,
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task GuardarAsync()
    {
        try
        {
            using var conn = _db.Create();

            // Guardar nota + garante si fue asignado desde el buscador
            if (_idGaranteCargado > 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE CAB_SOL_SALES SET NOTA=@Nota, ID_GARANTE=@Gar WHERE IDSOLICITUD=@Id",
                    new { Nota = _txtNota.Text, Gar = _idGaranteCargado, Id = _sol.IdSolicitud });
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE CAB_SOL_SALES SET NOTA=@Nota WHERE IDSOLICITUD=@Id",
                    new { Nota = _txtNota.Text, Id = _sol.IdSolicitud });
            }

            MessageBox.Show("Guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Helpers de UI ────────────────────────────────────────────────────────

    private static Border SecGroup(string titulo, UIElement content)
    {
        var dp = new DockPanel();
        var header = new Border {
            Background = BrPrimary, Padding = new Thickness(10, 5, 10, 5)
        };
        header.Child = new TextBlock {
            Text = titulo, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 12
        };
        DockPanel.SetDock(header, Dock.Top);
        dp.Children.Add(header);

        var body = new Border {
            Background = BrCard, Padding = new Thickness(8, 6, 8, 6)
        };
        body.Child = content;
        dp.Children.Add(body);

        return new Border {
            Child = dp,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 6),
            ClipToBounds = true
        };
    }

    private static void AddCell(Grid g, string label, TextBlock val, int row, int col, int colSpanVal = 1)
    {
        var lbl = new TextBlock {
            Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabelTxt,
            Padding = new Thickness(2, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(lbl, row); Grid.SetColumn(lbl, col); g.Children.Add(lbl);

        val.Padding = new Thickness(3, 3, 3, 3);
        val.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(val, row); Grid.SetColumn(val, col + 1);
        if (colSpanVal > 1) Grid.SetColumnSpan(val, colSpanVal * 2 - 1);
        g.Children.Add(val);
    }

    private static TextBlock Tb(string text) => new TextBlock {
        Text = text, FontSize = 12, Padding = new Thickness(3, 3, 3, 3),
        Foreground = BrValTxt, VerticalAlignment = VerticalAlignment.Center
    };

    private static Button Btn(string text, string hex) => new Button {
        Content = text, Height = 32, Padding = new Thickness(18, 0, 18, 0),
        Margin = new Thickness(0, 0, 8, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = BrBlanco, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13,
        FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0)
    };

    private static Button BtnAccion(string text, string hex) => new Button {
        Content = text, Height = 26, Padding = new Thickness(10, 0, 10, 0),
        Margin = new Thickness(0, 2, 6, 2),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = BrBlanco, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11,
        FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0)
    };

    private void AbrirFichaCliente()
    {
        if (_idClienteCargado == 0) return;
        MessageBox.Show($"ID Cliente: {_idClienteCargado}\n(Ficha de cliente — pendiente de implementar)",
            "Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void MostrarHistorial()
    {
        if (_idClienteCargado == 0) return;
        try
        {
            using var conn = _db.Create();
            var historial = await conn.QueryAsync<dynamic>(
                "SELECT TOP 20 s.IDSOLICITUD, s.FECHA_SOLICITUD, s.TOTALSALE, s.ESTADO " +
                " FROM CAB_SOL_SALES s " +
                " WHERE s.ID_CLIENTE = @id " +
                " ORDER BY s.FECHA_SOLICITUD DESC",
                new { id = _idClienteCargado });

            var win = new Window {
                Title = "Historial Crediticio", Width = 600, Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
            };
            var dg = new DataGrid {
                AutoGenerateColumns = true, IsReadOnly = true,
                Margin = new Thickness(8), FontSize = 12
            };
            dg.ItemsSource = historial.Select(r => {
                var d = (IDictionary<string, object>)r;
                var estado = d.TryGetValue("ESTADO", out var e) ? Convert.ToByte(e) : (byte)0;
                return new {
                    ID    = d.TryGetValue("IDSOLICITUD", out var id)  ? Convert.ToInt32(id)    : 0,
                    Fecha = d.TryGetValue("FECHA_SOLICITUD", out var fs) && fs is DateTime dt
                            ? dt.ToString("dd/MM/yyyy") : "",
                    Total = d.TryGetValue("TOTALSALE",   out var tot) ? Convert.ToDecimal(tot).ToString("C0") : "",
                    Estado = estado == 1 ? "Aprobado" : estado == 2 ? "Rechazado" : "Pendiente"
                };
            }).ToList();
            win.Content = new ScrollViewer { Content = dg };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al cargar historial:\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void VerCedula()
    {
        if (string.IsNullOrEmpty(_fotoCedulaCliente) && _idClienteCargado == 0) {
            MessageBox.Show("No se encontró número de C.I. para buscar la foto.",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        byte[]? datos = null;
        try
        {
            using var conn = _db.Create();
            datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci = _fotoCedulaCliente, id = _idClienteCargado });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al consultar la foto: {ex.Message}",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (datos == null || datos.Length == 0) {
            MessageBox.Show($"No se encontró foto de cédula para el cliente (CI: {_fotoCedulaCliente}).",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BitmapImage bmp;
        try
        {
            bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(datos);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 500; // reduce desde la decodificación, sin pérdida visible
            bmp.EndInit();
            bmp.Freeze();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar la imagen: {ex.Message}",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        double escala    = 1.0;
        double rotacion  = 0.0;
        var tg = new TransformGroup();
        var st = new ScaleTransform(1, 1);
        var rt = new RotateTransform(0);
        tg.Children.Add(st);
        tg.Children.Add(rt);

        var img = new System.Windows.Controls.Image {
            Source = bmp,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            LayoutTransform = tg,
            Margin = new Thickness(8)
        };

        var scroll = new ScrollViewer {
            Content = img,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var win = new Window {
            Title = $"Cédula  —  CI: {_fotoCedulaCliente}",
            Width = 580, Height = 500,
            MinWidth = 420, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        void AplicarTransform()
        {
            st.ScaleX = escala; st.ScaleY = escala;
            rt.Angle  = rotacion;
            img.Stretch = escala <= 1.0 ? Stretch.Uniform : Stretch.None;
        }

        // Helper: botón moderno oscuro con ícono+texto apilados
        Button BtnViewer(string icono, string label, Color bg)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock {
                Text = icono, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White
            });
            sp.Children.Add(new TextBlock {
                Text = label, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 210))
            });
            return new Button {
                Content = sp, Height = 52, Width = 64, Margin = new Thickness(3, 3, 3, 3),
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0)
            };
        }

        var btnDis   = BtnViewer("🔍−", "Alejar",    Color.FromRgb(55,  55,  70));
        var btnAum   = BtnViewer("🔍+", "Acercar",   Color.FromRgb(25,  90, 160));
        var btnReset = BtnViewer("⊡",   "Normal",    Color.FromRgb(60,  60,  60));
        var btnRotL  = BtnViewer("↺",   "Rotar ←",  Color.FromRgb(70,  50, 100));
        var btnRotR  = BtnViewer("↻",   "Rotar →",  Color.FromRgb(70,  50, 100));
        var btnCer   = BtnViewer("✕",   "Cerrar",    Color.FromRgb(160, 30,  30));

        btnDis.Click   += (_, _) => { escala   = Math.Max(escala - 0.25, 0.25);    AplicarTransform(); };
        btnAum.Click   += (_, _) => { escala   = Math.Min(escala + 0.25, 5.0);     AplicarTransform(); };
        btnReset.Click += (_, _) => { escala   = 1.0; rotacion = 0.0;              AplicarTransform(); };
        btnRotL.Click  += (_, _) => { rotacion = (rotacion - 90 + 360) % 360;      AplicarTransform(); };
        btnRotR.Click  += (_, _) => { rotacion = (rotacion + 90) % 360;            AplicarTransform(); };
        btnCer.Click   += (_, _) => win.Close();

        // Separador vertical decorativo
        var sep = new Border {
            Width = 1, Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Margin = new Thickness(6, 8, 6, 8)
        };

        var bar = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(22, 22, 28))
        };
        bar.Children.Add(btnDis);
        bar.Children.Add(btnReset);
        bar.Children.Add(btnAum);
        bar.Children.Add(sep);
        bar.Children.Add(btnRotL);
        bar.Children.Add(btnRotR);
        bar.Children.Add(new Border { Width = 1, Background = new SolidColorBrush(Color.FromRgb(70,70,70)), Margin = new Thickness(6,8,6,8) });
        bar.Children.Add(btnCer);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        Grid.SetRow(scroll, 0);
        Grid.SetRow(bar, 1);
        root.Children.Add(scroll);
        root.Children.Add(bar);

        win.Content = root;
        win.ShowDialog();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE PERSONA  (lista de referentes / garante)
// ══════════════════════════════════════════════════════════════════════════════

public class PersonaItem
{
    public int    Fila       { get; set; }
    public int    Id         { get; set; }
    public string Ci         { get; set; } = "";
    public string Nombre     { get; set; } = "";
    public string Telefono   { get; set; } = "";
    public string Direccion  { get; set; } = "";
    public string Ciudad     { get; set; } = "";
    public string Empresa    { get; set; } = "";
    public string TelLaboral { get; set; } = "";
    public string Ecv        { get; set; } = "";
    // campos extendidos del cliente
    public string Sexo       { get; set; } = "";
    public string Inforconf  { get; set; } = "";
    public string Tipo       { get; set; } = "";
    public string Condicion  { get; set; } = "";
    public string EstadoTexto{ get; set; } = "";
    public string Conyuge    { get; set; } = "";
    public string VencCI     { get; set; } = "";
    public decimal CredMax   { get; set; }
    public decimal SaldoActivo { get; set; }
    public string Antiguedad { get; set; } = "";
    public string Ruc        { get; set; } = "";
}

// DTO tipado para mapeo Dapper — evita problemas de cast con dynamic
file class ClienteRow
{
    public long     Fila       { get; set; }
    public int      Id         { get; set; }
    public string?  Ci         { get; set; }
    public string?  Ruc        { get; set; }
    public string?  Nombre     { get; set; }
    public string?  Telefono   { get; set; }
    public string?  Dir        { get; set; }
    public string?  Ciudad     { get; set; }
    public string?  Empresa    { get; set; }
    public string?  TelLab     { get; set; }
    public int      EcvNum     { get; set; }
    public int      SexoNum    { get; set; }
    public int      Inforcom   { get; set; }
    public int      TipoNum    { get; set; }
    public int      CondNum    { get; set; }
    public int      EstNum     { get; set; }
    public decimal  CredMax    { get; set; }
    public string?  Antiguedad { get; set; }
    public string?  Conyuge    { get; set; }
    public DateTime? VencCI    { get; set; }
    public decimal  SaldoActivo{ get; set; }
}

public class BuscadorPersonaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtBuscar = null!;
    private DataGrid  _grid      = null!;
    private List<PersonaItem> _todos = new();

    public PersonaItem? PersonaSeleccionada { get; private set; }

    public BuscadorPersonaWindow(IDbConnectionFactory db)
    {
        _db = db;
        Title = "Lista de referencias";
        Width = 780; Height = 580; MinWidth = 500; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        // Grid de 3 filas: búsqueda (Auto) | tabla (*) | botones (Auto)
        var root = new Grid {
            Margin = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(255, 198, 0))
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Fila 0: barra de búsqueda ──────────────────────────────────────
        var barBuscar = new StackPanel { Orientation = Orientation.Horizontal };
        barBuscar.Children.Add(new TextBlock {
            Text = "Escriba el nombre o C.I para buscar",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0), FontSize = 12
        });
        _txtBuscar = new TextBox {
            Width = 240, Height = 26, FontSize = 12, Background = Brushes.White
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        barBuscar.Children.Add(_txtBuscar);
        var barBuscarBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(255, 198, 0)),
            Padding = new Thickness(4, 6, 4, 6),
            Child = barBuscar,
            Margin = new Thickness(0, 0, 0, 0)
        };
        Grid.SetRow(barBuscarBorder, 0); root.Children.Add(barBuscarBorder);

        // ── Fila 1: DataGrid ───────────────────────────────────────────────
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            ColumnHeaderHeight = 32, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(255, 245, 180)),
            RowBackground = new SolidColorBrush(Color.FromRgb(255, 245, 180)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255, 230, 120)),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(50, 80, 140))));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
            Brushes.White));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty,
            FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty,
            12.0));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty,
            new Thickness(8, 0, 8, 0)));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.HorizontalContentAlignmentProperty,
            HorizontalAlignment.Left));
        _grid.ColumnHeaderStyle = hdrStyle;

        _grid.Columns.Add(new DataGridTextColumn {
            Header = "ID", Binding = new System.Windows.Data.Binding("Fila"), Width = 60
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "C.I", Binding = new System.Windows.Data.Binding("Ci"), Width = 120
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "NOMBRE Y APELLIDO",
            Binding = new System.Windows.Data.Binding("Nombre"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _grid.MouseDoubleClick += (_, _) => Aceptar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        // ── Fila 2: botones ────────────────────────────────────────────────
        var barBtns = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        var btnNuevo = new Button {
            Content = "Nuevo", Width = 80, Height = 28, Margin = new Thickness(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 100, 180)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnAceptar = new Button {
            Content = "Aceptar", Width = 80, Height = 28, Margin = new Thickness(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 140, 60)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnCerrar = new Button {
            Content = "Cerrar", Width = 80, Height = 28,
            Background = new SolidColorBrush(Color.FromRgb(180, 40, 40)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        btnNuevo.Click   += async (_, _) => { _grid.SelectedItem = null; await AbrirFormNuevo(); _grid.SelectedItem = null; };
        btnAceptar.Click += (_, _) => Aceptar();
        btnCerrar.Click  += (_, _) => { DialogResult = false; Close(); };
        barBtns.Children.Add(btnNuevo);
        barBtns.Children.Add(btnAceptar);
        barBtns.Children.Add(btnCerrar);
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        Content = root;
    }

    private static byte ToB(object? v)
    {
        if (v == null || v is DBNull) return 0;
        try { return Convert.ToByte(v); } catch { return 0; }
    }

    private async Task CargarAsync()
    {
        try
        {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<ClienteRow>(
                "SELECT ROW_NUMBER() OVER (ORDER BY NOMBRE_CLIENTE) as Fila," +
                "  cl.ID_CLIENTE as Id, cl.CI_CLIENTE as Ci, cl.RUC_CLIENTE as Ruc," +
                "  cl.NOMBRE_CLIENTE as Nombre, cl.TELEFONO_CLIENTE as Telefono," +
                "  cl.DIRECCION_CLIENTE as Dir, cl.CIUDAD_CLIENTE as Ciudad," +
                "  cl.EMPRESA_LABORAL as Empresa, cl.TELEFONO_LABORAL as TelLab," +
                "  cl.ECV as EcvNum, cl.SEXO as SexoNum, cl.INFORCOM as Inforcom," +
                "  cl.TIPO as TipoNum, cl.CONDICION as CondNum, cl.ESTADO as EstNum," +
                "  ISNULL(cl.CRED_MAX,0) as CredMax, cl.ANTIGUEDAD as Antiguedad," +
                "  ISNULL(cl.CONYUGE,'') as Conyuge, cl.VENC_CEDULA as VencCI," +
                "  ISNULL((SELECT SUM(DEBE-HABER) FROM CABECERA_SALES" +
                "          WHERE ID_CLIENTE=cl.ID_CLIENTE AND ESTADO=1 AND FORMA_DE_VENTA=2),0) as SaldoActivo" +
                " FROM CLIENTES cl" +
                " ORDER BY cl.NOMBRE_CLIENTE");

            _todos = rows.Select(r => new PersonaItem {
                Fila        = (int)r.Fila,
                Id          = r.Id,
                Ci          = r.Ci         ?? "",
                Ruc         = r.Ruc        ?? "",
                Nombre      = r.Nombre     ?? "",
                Telefono    = r.Telefono   ?? "",
                Direccion   = r.Dir        ?? "",
                Ciudad      = r.Ciudad     ?? "",
                Empresa     = r.Empresa    ?? "",
                TelLaboral  = r.TelLab     ?? "",
                Antiguedad  = r.Antiguedad ?? "",
                Conyuge     = r.Conyuge    ?? "",
                VencCI      = r.VencCI.HasValue ? r.VencCI.Value.ToString("dd/MM/yyyy") : "",
                CredMax     = r.CredMax,
                SaldoActivo = r.SaldoActivo,
                Ecv = r.EcvNum switch {
                    0=>"Soltero",1=>"Casado",2=>"Divorciado",3=>"Separado",4=>"Viudo",_=>""
                },
                Sexo = r.SexoNum switch {
                    0=>"Femenino",1=>"Masculino",_=>""
                },
                Inforconf = r.Inforcom switch {
                    0=>"Libre",1=>"Registrado",_=>""
                },
                Tipo = r.TipoNum switch {
                    0=>"Malo",1=>"Regular",2=>"Bueno",3=>"Muy Bueno",4=>"Excelente",_=>""
                },
                Condicion = r.CondNum switch {
                    0=>"Activo",1=>"Bloqueado",2=>"Moroso",3=>"Inactivo",_=>""
                },
                EstadoTexto = r.EstNum switch {
                    0=>"Normal",1=>"Moroso",_=>""
                },
            }).ToList();

            _grid.ItemsSource = _todos;
            _grid.SelectedItem = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al cargar lista:\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filtrar()
    {
        var q = _txtBuscar.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(q))
            _grid.ItemsSource = _todos;
        else
            _grid.ItemsSource = _todos
                .Where(p => p.Nombre.ToLowerInvariant().Contains(q)
                         || p.Ci.Contains(q))
                .ToList();
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem is PersonaItem p)
        {
            PersonaSeleccionada = p;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Seleccione una persona de la lista.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task AbrirFormNuevo()
    {
        byte[]? fotoBytes = null; // bytes de la foto seleccionada

        var form = new Window {
            Title = "Nueva referencia / cliente", Width = 520, Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
        };

        // Layout principal: campos a la izquierda, foto a la derecha
        var mainGrid = new Grid { Margin = new Thickness(12) };
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Columna izquierda: campos de texto
        var g = new Grid { Margin = new Thickness(0, 0, 10, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++)
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBox TxtRow(string label, int row)
        {
            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 8, 5) };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); g.Children.Add(lbl);
            var txt = new TextBox { Height = 28, Margin = new Thickness(0, 5, 0, 5),
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
            Grid.SetRow(txt, row); Grid.SetColumn(txt, 1); g.Children.Add(txt);
            return txt;
        }

        var txtCi      = TxtRow("Documento (CI):", 0);
        var txtNombre  = TxtRow("Nombre y Apellido:", 1);
        var txtCel     = TxtRow("Celular:", 2);
        var txtTrabajo = TxtRow("Lugar de trabajo:", 3);

        Grid.SetRow(g, 0); Grid.SetColumn(g, 0); mainGrid.Children.Add(g);

        // Columna derecha: previsualización de foto cédula
        var fotoPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

        var fotoLabel = new TextBlock {
            Text = "Foto de Cédula", FontWeight = FontWeights.SemiBold,
            FontSize = 11, Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var imgPreview = new System.Windows.Controls.Image {
            Width = 150, Height = 110, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var imgBorder = new Border {
            Width = 152, Height = 112,
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            Child = imgPreview, Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var lblFotoEstado = new TextBlock {
            Text = "Sin foto", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4)
        };

        var btnSelFoto = new Button {
            Content = "📷  Seleccionar foto", Height = 28, Width = 150,
            Background = new SolidColorBrush(Color.FromRgb(50, 100, 160)),
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand, FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var btnQuitarFoto = new Button {
            Content = "✕  Quitar foto", Height = 24, Width = 150,
            Background = new SolidColorBrush(Color.FromRgb(160, 50, 50)),
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand, FontSize = 10, Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0)
        };

        btnSelFoto.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog {
                Title = "Seleccionar foto de cédula",
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                fotoBytes = File.ReadAllBytes(dlg.FileName);
                var bmpPrev = new BitmapImage();
                bmpPrev.BeginInit();
                bmpPrev.StreamSource = new System.IO.MemoryStream(fotoBytes);
                bmpPrev.CacheOption = BitmapCacheOption.OnLoad;
                bmpPrev.DecodePixelWidth = 300;
                bmpPrev.EndInit();
                bmpPrev.Freeze();
                imgPreview.Source = bmpPrev;
                lblFotoEstado.Text = System.IO.Path.GetFileName(dlg.FileName);
                lblFotoEstado.Foreground = new SolidColorBrush(Color.FromRgb(30, 120, 30));
                btnQuitarFoto.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo cargar la imagen:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        btnQuitarFoto.Click += (_, _) =>
        {
            fotoBytes = null;
            imgPreview.Source = null;
            lblFotoEstado.Text = "Sin foto";
            lblFotoEstado.Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140));
            btnQuitarFoto.Visibility = Visibility.Collapsed;
        };

        fotoPanel.Children.Add(fotoLabel);
        fotoPanel.Children.Add(imgBorder);
        fotoPanel.Children.Add(lblFotoEstado);
        fotoPanel.Children.Add(btnSelFoto);
        fotoPanel.Children.Add(btnQuitarFoto);

        Grid.SetRow(fotoPanel, 0); Grid.SetColumn(fotoPanel, 1); mainGrid.Children.Add(fotoPanel);

        // Barra de botones inferior
        var barBtn = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var btnGuardar = new Button {
            Content = "Guardar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 140, 60)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnCancelar = new Button {
            Content = "Cerrar", Width = 90, Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(180, 40, 40)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        btnCancelar.Click += (_, _) => form.Close();
        btnGuardar.Click  += async (_, _) =>
        {
            var nombre = txtNombre.Text.Trim();
            var ci     = txtCi.Text.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("El nombre es obligatorio.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Mostrar confirmación con preview de foto antes de guardar
            if (!ConfirmarGuardado(nombre, ci, txtCel.Text.Trim(), txtTrabajo.Text.Trim(), fotoBytes, form))
                return;

            try
            {
                using var conn = _db.Create();
                // Insertar cliente y recuperar el ID generado
                var nuevoId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO CLIENTES (CI_CLIENTE, NOMBRE_CLIENTE, TELEFONO_CLIENTE," +
                    "  EMPRESA_LABORAL, CONDICION, TIPO, ESTADO)" +
                    " VALUES (@Ci, @Nom, @Tel, @Trab, 1, 1, 1);" +
                    " SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { Ci = ci, Nom = nombre, Tel = txtCel.Text.Trim(),
                          Trab = txtTrabajo.Text.Trim() });

                // Guardar foto en tabla FOTOS si se seleccionó una
                if (fotoBytes != null && fotoBytes.Length > 0 && nuevoId > 0)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO FOTOS (IDCLIE, CI, DATOS) VALUES (@Id, @Ci, @Datos)",
                        new { Id = nuevoId, Ci = ci, Datos = fotoBytes });
                }

                form.Close();
                await CargarAsync();  // refresca la lista
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        barBtn.Children.Add(btnGuardar);
        barBtn.Children.Add(btnCancelar);
        Grid.SetRow(barBtn, 1); Grid.SetColumnSpan(barBtn, 2); mainGrid.Children.Add(barBtn);

        form.Content = mainGrid;
        form.ShowDialog();
    }

    private static bool ConfirmarGuardado(string nombre, string ci, string cel, string trabajo,
                                           byte[]? fotoBytes, Window owner)
    {
        bool confirmado = false;

        var win = new Window {
            Title = "Confirmar nuevo cliente",
            Width = fotoBytes != null ? 500 : 360,
            Height = fotoBytes != null ? 400 : 230,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
        };

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // título
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // contenido
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // botones

        // Título
        var titulo = new TextBlock {
            Text = "¿Confirmar el registro del cliente?",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 70, 140)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(titulo, 0); root.Children.Add(titulo);

        // Contenido: datos + foto en paralelo
        var contenido = new Grid();
        contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (fotoBytes != null)
            contenido.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });

        // Panel de datos (izquierda)
        var datos = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

        void FilaDato(string lbl, string val)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            sp.Children.Add(new TextBlock {
                Text = lbl, FontWeight = FontWeights.SemiBold, Width = 110,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
            });
            sp.Children.Add(new TextBlock {
                Text = string.IsNullOrEmpty(val) ? "—" : val,
                Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            });
            datos.Children.Add(sp);
        }

        FilaDato("Documento (CI):", ci);
        FilaDato("Nombre:", nombre);
        FilaDato("Celular:", cel);
        FilaDato("Lugar de trabajo:", trabajo);
        datos.Children.Add(new TextBlock {
            Text = fotoBytes != null
                ? $"Foto de cédula: ✔ adjunta ({fotoBytes.Length / 1024} KB)"
                : "Foto de cédula: sin foto",
            Margin = new Thickness(0, 8, 0, 0),
            FontStyle = fotoBytes != null ? FontStyles.Normal : FontStyles.Italic,
            Foreground = fotoBytes != null
                ? new SolidColorBrush(Color.FromRgb(20, 130, 40))
                : new SolidColorBrush(Color.FromRgb(150, 100, 0))
        });

        Grid.SetColumn(datos, 0); contenido.Children.Add(datos);

        // Preview de foto (derecha) — solo si hay foto
        if (fotoBytes != null)
        {
            var bmpConf = new BitmapImage();
            bmpConf.BeginInit();
            bmpConf.StreamSource = new System.IO.MemoryStream(fotoBytes);
            bmpConf.CacheOption = BitmapCacheOption.OnLoad;
            bmpConf.DecodePixelWidth = 320;
            bmpConf.EndInit();
            bmpConf.Freeze();

            var imgConf = new System.Windows.Controls.Image {
                Source = bmpConf, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var fotoBox = new Border {
                BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
                Child = imgConf, Margin = new Thickness(10, 0, 0, 0),
                CornerRadius = new CornerRadius(3)
            };
            Grid.SetColumn(fotoBox, 1); contenido.Children.Add(fotoBox);
        }

        Grid.SetRow(contenido, 1); root.Children.Add(contenido);

        // Botones
        var barBtn = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var btnSi = new Button {
            Content = "✔  Sí, guardar", Width = 120, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 140, 60)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnNo = new Button {
            Content = "✕  Cancelar", Width = 100, Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(180, 40, 40)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        btnSi.Click += (_, _) => { confirmado = true;  win.Close(); };
        btnNo.Click += (_, _) => { confirmado = false; win.Close(); };
        barBtn.Children.Add(btnSi);
        barBtn.Children.Add(btnNo);
        Grid.SetRow(barBtn, 2); root.Children.Add(barBtn);

        win.Content = root;
        win.ShowDialog();
        return confirmado;
    }
}
