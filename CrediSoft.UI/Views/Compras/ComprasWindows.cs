using CrediSoft.Core.Services;
using CrediSoft.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Compras;

// ══════════════════════════════════════════════════════════════════════════════
//  NUEVA COMPRA  — flujo de dos pasos:
//    1) JEJOGUA_TMP_CS  @AGENTE='SI' (primer ítem) / 'NO' (siguientes)
//    2) JOGUAANETE_CS   @AGENTE=1..N para finalizar y confirmar
// ══════════════════════════════════════════════════════════════════════════════
public class NuevaCompraWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly SessionService       _sesion;

    // Colores
    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb(255,140,  0));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb(224,112,  0));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229,231,235));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel    = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 22,163, 74));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrAzul     = new(System.Windows.Media.Color.FromRgb( 59,130,246));
    private static readonly System.Windows.Media.SolidColorBrush BrFondoArt = new(System.Windows.Media.Color.FromRgb(255,248,230));

    // Búsqueda artículo
    private TextBox   _txtBuscarArt    = null!;
    private TextBlock _lblNombreArt    = null!;

    // Panel precios del artículo seleccionado
    private TextBox   _txtPC           = null!;
    private TextBox   _txtPV           = null!;
    private TextBox   _txtContado      = null!;
    private TextBox   _txtPPromo       = null!;
    // Badges: título fijo arriba, fecha abajo
    private TextBlock _lblUCFecha  = null!;
    private TextBlock _lblUVFecha  = null!;
    private TextBlock _lblMPFecha  = null!;

    // Entrada de cantidad e insertar
    private TextBox   _txtCantidad     = null!;
    private Button    _btnInsertar     = null!;

    // Grid y total
    private DataGrid  _gridDetalle     = null!;
    private TextBlock _lblTotal        = null!;

    private readonly ObservableCollection<LineaCompra> _items = new();

    // Artículo seleccionado actualmente
    private int    _idArtActual;
    private string _caActual        = "";
    private string _descActual      = "";
    private int    _idPricesActual;

    // Local seleccionado (inicia con el local de sesión)
    private int    _idLocalActual;
    private string _nombreLocalActual = "";
    private TextBlock _lblLocalNombre  = null!;
    private TextBlock _lblLocalPrefijo = null!;
    private bool   _localManualmenteSeleccionado = false;

    public NuevaCompraWindow()
    {
        _db     = App.Services.GetRequiredService<IDbConnectionFactory>();
        _sesion = SessionService.Instance;
        _idLocalActual     = _sesion.LocalActual?.IdLocal    ?? 1;
        _nombreLocalActual = _sesion.LocalActual?.NombreLocal ?? "Local 1";
        Title   = "Nueva Compra"; Width = 1200; Height = 700;
        MinWidth = 1050; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrPrimary;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra búsqueda artículo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // panel precios
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Barra de búsqueda de artículo (fondo naranja, igual al sistema viejo) ──
        var busqBar = new Border { Background = BrPrimary, Padding = new Thickness(10, 8, 10, 8) };
        var busqRow = new StackPanel { Orientation = Orientation.Horizontal };

        busqRow.Children.Add(new TextBlock { Text = "Código del artículo", FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrBlanco, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        _txtBuscarArt = new TextBox { Width = 150, Padding = new Thickness(6,4,6,4), FontSize = 12,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1) };
        _txtBuscarArt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticulo(); };
        busqRow.Children.Add(_txtBuscarArt);

        var btnBA = MakeSmBtn("Buscar", BrPrimDark); btnBA.Click += async (_, _) => await BuscarArticulo();
        busqRow.Children.Add(btnBA);

        busqRow.Children.Add(new TextBlock { Text = "Nombre o descripción del artículo/mercadería",
            FontSize = 11, Foreground = BrBlanco, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20,0,8,0) });
        _lblNombreArt = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrBlanco,
            VerticalAlignment = VerticalAlignment.Center, MaxWidth = 500 };
        busqRow.Children.Add(_lblNombreArt);

        Button MkTopBtn(string txt) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            FontSize = 12, FontWeight = FontWeights.Bold,
            Background = System.Windows.Media.Brushes.White,
            Foreground = BrPrimary,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var btnListaArt  = MkTopBtn("🔍  Ver Listado de Artículos");
        var btnNuevoArt  = MkTopBtn("➕  Nuevo Artículo");
        btnListaArt.Click += async (_, _) => await AbrirBuscadorArticulo();
        btnNuevoArt.Click += (_, _) =>
        {
            var w = new CrediSoft.UI.Views.Maestros.ArticulosWindow { Owner = this };
            w.ShowDialog();
        };

        var topBtnsSp = new StackPanel { Orientation = Orientation.Horizontal };

        topBtnsSp.Children.Add(btnListaArt);
        topBtnsSp.Children.Add(btnNuevoArt);

        var busqDock = new DockPanel();
        DockPanel.SetDock(topBtnsSp, Dock.Right);
        busqDock.Children.Add(topBtnsSp);
        busqDock.Children.Add(busqRow);
        busqBar.Child = busqDock;
        Grid.SetRow(busqBar, 0); root.Children.Add(busqBar);

        // ── Panel precios + cantidad a comprar + local + insertar ──────────
        var precBar = new Border { Background = BrFondoArt, BorderBrush = BrBorde, BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(10, 8, 10, 8) };
        var precRoot = new Grid();
        precRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // precios
        precRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // cantidad + local + insertar + info local

        // Fila 0: precios
        var precRow = new StackPanel { Orientation = Orientation.Horizontal };
        TextBlock PL(string t) => new TextBlock { Text = t, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        TextBox PT(int w) {
            var tb = new TextBox { Width = w, Padding = new Thickness(4,3,4,3), FontSize = 12,
                BorderBrush = BrBorde, BorderThickness = new Thickness(1), Background = BrBlanco,
                TextAlignment = TextAlignment.Right };
            tb.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            DataObject.AddPastingHandler(tb, (_, e) => {
                if (e.DataObject.GetDataPresent(typeof(string))) {
                    var t = (string)e.DataObject.GetData(typeof(string));
                    if (!t.All(char.IsDigit)) e.CancelCommand();
                } else e.CancelCommand();
            });
            tb.TextChanged += (_, _) => FormatearMiles(tb);
            return tb;
        }

        precRow.Children.Add(PL("Precio costo"));
        _txtPC = PT(95); precRow.Children.Add(_txtPC);

        precRow.Children.Add(new TextBlock { Width = 6 });
        precRow.Children.Add(PL("Precio promoción"));
        _txtPPromo = PT(95); precRow.Children.Add(_txtPPromo);

        precRow.Children.Add(new TextBlock { Width = 6 });
        precRow.Children.Add(PL("Precio venta"));
        _txtPV = PT(95); precRow.Children.Add(_txtPV);

        precRow.Children.Add(new TextBlock { Width = 6 });
        precRow.Children.Add(PL("Precio contado"));
        _txtContado = PT(95); precRow.Children.Add(_txtContado);

        // Badges dos filas: ÚLTIMA COMPRA / ÚLTIMA VENTA / MOD. P. DE VENTA
        Border MkBadge(string titulo, out TextBlock lblFecha, Thickness margin)
        {
            TextBlock tv = new() { Text = titulo, FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = BrBlanco, TextAlignment = TextAlignment.Center };
            TextBlock fv = new() { Text = "—", FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = BrBlanco, TextAlignment = TextAlignment.Center };
            lblFecha = fv;
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(tv); sp.Children.Add(fv);
            return new Border { Background = BrPrimDark, CornerRadius = new CornerRadius(3),
                Width = 140,
                Padding = new Thickness(10, 6, 10, 6), Child = sp, Margin = margin, Cursor = Cursors.Hand };
        }

        var badgeUC = MkBadge("ÚLTIMA COMPRA",    out _lblUCFecha, new Thickness(8,0,4,0));
        var badgeUV = MkBadge("ÚLTIMA VENTA",     out _lblUVFecha, new Thickness(4,0,4,0));
        var badgeMP = MkBadge("MOD. P. DE VENTA", out _lblMPFecha, new Thickness(4,0,0,0));
        precRow.Children.Add(badgeUC);
        precRow.Children.Add(badgeUV);
        precRow.Children.Add(badgeMP);

        Grid.SetRow(precRow, 0); precRoot.Children.Add(precRow);

        // Fila 1: cantidad + local + mensaje + insertar
        var cantRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,8,0,0) };

        cantRow.Children.Add(new TextBlock { Text = "CANTIDAD A COMPRAR", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
        _txtCantidad = new TextBox { Width = 100, Padding = new Thickness(6,6,6,6), FontSize = 14, FontWeight = FontWeights.Bold,
            BorderBrush = BrPrimary, BorderThickness = new Thickness(2), Background = BrBlanco, TextAlignment = TextAlignment.Center };
        _txtCantidad.Text = "1";
        _txtCantidad.KeyDown += (_, e) => { if (e.Key == Key.Enter) InsercionRapida(); };
        cantRow.Children.Add(_txtCantidad);

        // Selector de local — prefijo + botón
        _lblLocalPrefijo = new TextBlock {
            Text = "Local donde se realizará la compra:",
            FontSize = 11, Foreground = BrGris,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 0, 8, 0)
        };
        cantRow.Children.Add(_lblLocalPrefijo);

        var btnSelLocal = new Button {
            Height = 32, Padding = new Thickness(12, 0, 12, 0),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Background = BrPrimary, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        _lblLocalNombre = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        btnSelLocal.Content = _lblLocalNombre;
        _lblLocalNombre.Text = $"📍 {_nombreLocalActual}";
        btnSelLocal.Click += async (_, _) => await SeleccionarLocal(btnSelLocal);
        cantRow.Children.Add(btnSelLocal);

        _btnInsertar = new Button { Content = "INSERTAR", Height = 40, Padding = new Thickness(20,0,20,0),
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0) };
        _btnInsertar.Click += (_, _) => InsercionRapida();

        var cantDock = new DockPanel();
        DockPanel.SetDock(_btnInsertar, Dock.Right);
        cantDock.Children.Add(_btnInsertar);
        cantDock.Children.Add(cantRow);
        Grid.SetRow(cantDock, 1); precRoot.Children.Add(cantDock);

        precBar.Child = precRoot;
        Grid.SetRow(precBar, 1); root.Children.Add(precBar);

        // ── Grid detalle ──────────────────────────────────────────────────
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229,231,235)),
            RowBackground = BrBlanco,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249,250,251)),
            FontSize = 12, RowHeight = 38, ColumnHeaderHeight = 32,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = BuildGridHeaderStyle()
        };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Codigo"),         Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("Descripcion"),    Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",       Binding = new System.Windows.Data.Binding("Cantidad"),       Width = 65 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. costo",    Binding = new System.Windows.Data.Binding("PrecioCostoFmt"), Width = 90 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. venta",    Binding = new System.Windows.Data.Binding("PrecioVentaFmt"), Width = 90 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. contado",  Binding = new System.Windows.Data.Binding("ContadoFmt"),     Width = 90 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Promo",    Binding = new System.Windows.Data.Binding("PPromoFmt"),      Width = 90 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "SubTotal Costo", Binding = new System.Windows.Data.Binding("SubtotalFmt"), Width = 110 });
        _gridDetalle.MouseDoubleClick += OnGridDblClick;
        _gridDetalle.KeyDown += (_, e) => { if (e.Key == Key.Delete || e.Key == Key.Back) QuitarSeleccionado(); };
        _gridDetalle.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => ActualizarTotal();
        Grid.SetRow(_gridDetalle, 2); root.Children.Add(_gridDetalle);

        // ── Footer ────────────────────────────────────────────────────────
        var footer = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230,230,230)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(10,6,10,6) };
        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Atajos + total
        var footLeft = new StackPanel();
        _lblTotal = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrPrimDark };
        var lblAtajos = new TextBlock {
            Text = "F1: Ayuda artículos   F2: Buscar artículo   F7: Insertar   Ctrl+N= Registrar nuevo artículo   F9: Cancelar\nDoble click: Cambiar cantidad   Sup/Delet: Excluir artículo   F5: Guardar   Ctrl+S: Cerrar",
            FontSize = 9, Foreground = BrGris, Margin = new Thickness(0,2,0,0)
        };
        footLeft.Children.Add(_lblTotal);
        footLeft.Children.Add(lblAtajos);
        Grid.SetColumn(footLeft, 0); footGrid.Children.Add(footLeft);

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var btnAceptar = MakeBtn("Aceptar", BrVerde);  btnAceptar.Click += (_, _) => AbrirModalConfirmacion();
        var btnCerrar  = MakeBtn("Cerrar",  BrGris);   btnCerrar.Click  += (_, _) => Close();
        btnsSp.Children.Add(btnAceptar); btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 1); footGrid.Children.Add(btnsSp);

        footer.Child = footGrid;
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        Content = root;
        ActualizarTotal();

        KeyDown += async (_, e) => {
            if (e.Key == Key.F2) { _txtBuscarArt.Focus(); _txtBuscarArt.SelectAll(); }
            else if (e.Key == Key.F7) InsercionRapida();
            else if (e.Key == Key.F5) AbrirModalConfirmacion();
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) Close();
            else if (e.Key == Key.Delete) QuitarSeleccionado();
        };

        Loaded += (_, _) => _txtBuscarArt.Focus();
    }

    private static Style BuildGridHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,140,0))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8,0,8,0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
        return s;
    }

    private static Button MakeBtn(string txt, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = txt, Height = 34, Padding = new Thickness(18,0,18,0), Margin = new Thickness(0,0,8,0),
        Background = bg, Foreground = System.Windows.Media.Brushes.White,
        FontWeight = FontWeights.SemiBold, FontSize = 13, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
    };
    private static Button MakeSmBtn(string txt, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = txt, Height = 28, Padding = new Thickness(12,0,12,0), Margin = new Thickness(6,0,0,0),
        Background = bg, Foreground = System.Windows.Media.Brushes.White,
        FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
    };

    // ── Búsqueda artículo ─────────────────────────────────────────────────
    private async Task BuscarArticulo()
    {
        var term = _txtBuscarArt.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;
        try
        {
            using var conn = _db.Create();
            var idlocal = _idLocalActual;

            var rows = (await conn.QueryAsync<dynamic>(
                "SELECT TOP 30 a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D FROM ARTICULOS a WHERE (a.CA LIKE @t OR a.D LIKE @t) AND a.ES = 1 ORDER BY a.D",
                new { t = $"%{term}%" })).ToList();
            if (rows.Count == 0) { MessageBox.Show("Artículo no encontrado."); return; }
            dynamic art = rows.Count == 1 ? rows[0] : (await SeleccionarItem(rows, "D", "Seleccionar artículo") ?? rows[0]);
            if (art == null) return;

            _idArtActual = (int)art.IDART;
            _caActual    = (string)art.CA;
            _descActual  = (string)art.D;

            var precio = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT IDPRICES, PC, PVENTA, CONTADO, PPROMO, FCOMPRA, FVENTA, FMP FROM PRICES WHERE IDART=@Id AND IDLOCAL=@L AND DELETADO=0",
                new { Id = _idArtActual, L = idlocal });

            if (precio != null)
            {
                _idPricesActual  = (int)precio.IDPRICES;
                _txtPC.Text      = ((decimal)precio.PC).ToString("N0");
                _txtPV.Text      = ((decimal)precio.PVENTA).ToString("N0");
                _txtContado.Text = ((decimal)precio.CONTADO).ToString("N0");
                _txtPPromo.Text  = ((decimal)precio.PPROMO).ToString("N0");
                _lblUCFecha.Text = precio.FCOMPRA is DateTime fc ? fc.ToString("dd/MM/yyyy") : "—";
                _lblUVFecha.Text = precio.FVENTA  is DateTime fv ? fv.ToString("dd/MM/yyyy") : "—";
                _lblMPFecha.Text = precio.FMP     is DateTime fm ? fm.ToString("dd/MM/yyyy") : "—";
            }
            else
            {
                _idPricesActual = 0;
                _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "0";
                _lblUCFecha.Text = _lblUVFecha.Text = _lblMPFecha.Text = "—";
            }

            _lblNombreArt.Text     = _descActual;
            _txtCantidad.Text      = "1";
            _btnInsertar.IsEnabled = true;
            _txtBuscarArt.Text     = _caActual;
            _txtCantidad.Focus();
            _txtCantidad.SelectAll();
        }
        catch (Exception ex) { MessageBox.Show($"Error buscando artículo: {ex.Message}"); }
    }

    // ── Formato separador de miles en TextBox de precio ───────────────────
    private static void FormatearMiles(TextBox tb)
    {
        // evitar reentrancia desde el propio TextChanged
        if ((bool)(tb.Tag ?? false)) return;
        var raw = new string(tb.Text.Where(char.IsDigit).ToArray());
        if (raw.Length == 0) return;
        if (!long.TryParse(raw, out var num)) return;
        var formatted = num.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                           .Replace(",", ".");   // usar punto como separador de miles
        if (tb.Text == formatted) return;
        tb.Tag = true;
        var caret = tb.CaretIndex;
        // calcular cuántos dígitos había antes del cursor
        var digitsBeforeCaret = tb.Text.Take(caret).Count(char.IsDigit);
        tb.Text = formatted;
        // reposicionar cursor tras el mismo número de dígitos
        var newCaret = 0; var counted = 0;
        for (int i = 0; i < formatted.Length; i++) {
            if (char.IsDigit(formatted[i])) { counted++; if (counted == digitsBeforeCaret) { newCaret = i + 1; } }
        }
        tb.CaretIndex = Math.Min(newCaret, formatted.Length);
        tb.Tag = false;
    }

    // ── Insertar al grid ──────────────────────────────────────────────────
    private void InsercionRapida()
    {
        if (_idArtActual == 0) { MessageBox.Show("Primero busque un artículo."); return; }
        if (!decimal.TryParse(_txtCantidad.Text.Replace(",","").Replace(".",""), out var cant) || cant <= 0)
            { MessageBox.Show("Ingrese una cantidad válida."); return; }
        if (!decimal.TryParse(_txtPC.Text.Replace(",","").Replace(".",""), out var pc))    pc = 0;
        if (!decimal.TryParse(_txtPV.Text.Replace(",","").Replace(".",""), out var pv))    pv = 0;
        if (!decimal.TryParse(_txtContado.Text.Replace(",","").Replace(".",""), out var co)) co = 0;
        if (!decimal.TryParse(_txtPPromo.Text.Replace(",","").Replace(".",""), out var pp))  pp = 0;

        var existente = _items.FirstOrDefault(x => x.IdArt == _idArtActual);
        if (existente != null)
        {
            existente.Cantidad   += cant;
            existente.PrecioCosto = pc; existente.PrecioVenta = pv;
            existente.Contado = co;     existente.PPromo = pp;
            existente.Subtotal = existente.Cantidad * pc;
            _gridDetalle.Items.Refresh();
        }
        else
        {
            _items.Add(new LineaCompra {
                IdArt = _idArtActual, IdPrices = _idPricesActual,
                Codigo = _caActual, Descripcion = _descActual,
                Cantidad = cant, PrecioCosto = pc, PrecioVenta = pv,
                Contado = co, PPromo = pp, Subtotal = cant * pc
            });
        }

        _idArtActual = 0; _idPricesActual = 0; _caActual = ""; _descActual = "";
        _txtBuscarArt.Text = ""; _txtCantidad.Text = "1";
        _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "";
        _lblNombreArt.Text = "";
        _lblUCFecha.Text = "—";
        _lblUVFecha.Text = "—";
        _lblMPFecha.Text = "—";
        _btnInsertar.IsEnabled = false;
        _txtBuscarArt.Focus();
    }

    private void QuitarSeleccionado()
    {
        if (_gridDetalle.SelectedItem is LineaCompra lc) _items.Remove(lc);
    }

    private void OnGridDblClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_gridDetalle.SelectedItem is not LineaCompra lc) return;
        var dlg = new Window { Title = "Cambiar cantidad", Width = 260, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco };
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(new TextBlock { Text = lc.Descripcion, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
        sp.Children.Add(new TextBlock { Text = "Nueva cantidad:", FontSize = 11, Foreground = BrLabel, Margin = new Thickness(0,0,0,4) });
        var txtQ = new TextBox { Text = lc.Cantidad.ToString("N0"), Padding = new Thickness(6,4,6,4),
            FontSize = 14, TextAlignment = TextAlignment.Center };
        sp.Children.Add(txtQ);
        var btnOk = MakeBtn("Aceptar", BrVerde); btnOk.Margin = new Thickness(0,10,0,0);
        btnOk.Click += (_, _) => {
            if (decimal.TryParse(txtQ.Text.Replace(",","").Replace(".",""), out var q) && q > 0) {
                lc.Cantidad = q; lc.Subtotal = q * lc.PrecioCosto;
                _gridDetalle.Items.Refresh(); ActualizarTotal();
            }
            dlg.Close();
        };
        txtQ.KeyDown += (_, ev) => { if (ev.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
        sp.Children.Add(btnOk);
        dlg.Content = sp;
        dlg.Loaded += (_, _) => { txtQ.Focus(); txtQ.SelectAll(); };
        dlg.ShowDialog();
    }

    private void ActualizarTotal() =>
        _lblTotal.Text = $"Total: {_items.Sum(i => i.Subtotal):N0} Gs.";

    // ── Modal de confirmación (igual al sistema viejo) ────────────────────
    private void AbrirModalConfirmacion()
    {
        if (_items.Count == 0) { MessageBox.Show("Agregue al menos un artículo."); return; }

        var total = _items.Sum(i => i.Subtotal);
        var idu   = _sesion.UsuarioActual?.IdUsuario ?? 1;
        var idlocal = (byte)_idLocalActual;

        var dlg = new Window {
            Title = "Confirmar compra", Width = 560, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco
        };

        // Campos del modal
        // Parcial = monto bruto (no editable, igual al sistema viejo)
        TextBlock lblParcial = new() { Text = total.ToString("N0").Replace(",","."),
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = BrPrimDark, VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 100 };
        TextBox txtDescuento= new() { Text = "0",   Width = 130, Padding = new Thickness(6,4,6,4), FontSize = 12 };
        TextBox txtFactura  = new() { Width = 130,              Padding = new Thickness(6,4,6,4), FontSize = 12 };
        TextBox txtNota     = new() { Width = 310,              Padding = new Thickness(6,4,6,4), FontSize = 12 };
        // Total se recalcula cuando cambia el descuento
        TextBlock lblTotal  = new() { Text = total.ToString("N0").Replace(",","."),
            FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = BrPrimDark, VerticalAlignment = VerticalAlignment.Center };

        // Solo dígitos en descuento, recalcula total al cambiar
        txtDescuento.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        DataObject.AddPastingHandler(txtDescuento, (_, e) => {
            if (e.DataObject.GetDataPresent(typeof(string))) {
                var t2 = (string)e.DataObject.GetData(typeof(string));
                if (!t2.All(char.IsDigit)) e.CancelCommand();
            } else e.CancelCommand();
        });
        txtDescuento.TextChanged += (_, _) => {
            decimal.TryParse(new string(txtDescuento.Text.Where(char.IsDigit).ToArray()), out var desc);
            var neto = total - desc;
            if (neto < 0) neto = 0;
            lblTotal.Text = neto.ToString("N0").Replace(",",".");
        };

        // Proveedor
        TextBlock lblProvNombre = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = BrAzul,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), MaxWidth = 280,
            TextWrapping = TextWrapping.NoWrap, Text = "—" };
        int idProvModal = 0;

        // Método de pago (igual al sistema viejo: Efectivo, Banco/Transferencia, Cheque, Tarjeta)
        var cboMetodo = new ComboBox { FontSize = 12, Width = 160 };
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Efectivo",      Tag = (byte)1 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia", Tag = (byte)2 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Cheque",        Tag = (byte)3 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta",       Tag = (byte)4 });
        cboMetodo.SelectedIndex = 0;

        TextBlock DlgLbl(string t) => new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };

        var body = new StackPanel { Margin = new Thickness(20, 14, 20, 10) };

        // Fila 1: Parcial | Descuento | TOTAL
        var r1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
        r1.Children.Add(DlgLbl("Parcial")); r1.Children.Add(lblParcial);
        r1.Children.Add(new TextBlock { Width = 20 });
        r1.Children.Add(DlgLbl("Descuento")); r1.Children.Add(txtDescuento);
        r1.Children.Add(new TextBlock { Width = 20 });
        r1.Children.Add(DlgLbl("TOTAL")); r1.Children.Add(lblTotal);
        body.Children.Add(r1);

        // Fila 2: Método de pago
        var r2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
        r2.Children.Add(DlgLbl("Método de pago")); r2.Children.Add(cboMetodo);
        body.Children.Add(r2);

        // Separador
        body.Children.Add(new Border { Height = 1, Background = BrBorde, Margin = new Thickness(0,4,0,10) });

        // Fila: Factura
        var r3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
        r3.Children.Add(DlgLbl("Factura")); r3.Children.Add(txtFactura);
        body.Children.Add(r3);

        // Fila: Proveedor — botón + nombre seleccionado
        var r3b = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10),
            VerticalAlignment = VerticalAlignment.Center };
        r3b.Children.Add(DlgLbl("Proveedor"));
        var btnBuscProv = new Button {
            Height = 30, Padding = new Thickness(12,0,12,0),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Background = System.Windows.Media.Brushes.White,
            Foreground = BrPrimary,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Content = "🏭  Ver Listado de Proveedores"
        };
        btnBuscProv.Click += (_, _) => {
            var modal = new BuscadorProveedorModal(_db) { Owner = dlg };
            if (modal.ShowDialog() == true && modal.ProveedorSeleccionado != null) {
                idProvModal = modal.ProveedorSeleccionado.IdProveedor;
                lblProvNombre.Text = modal.ProveedorSeleccionado.Nombre;
            }
        };
        r3b.Children.Add(btnBuscProv); r3b.Children.Add(lblProvNombre);
        body.Children.Add(r3b);

        // Fila: Nota
        var r4 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
        r4.Children.Add(DlgLbl("Nota")); r4.Children.Add(txtNota);
        body.Children.Add(r4);

        // Botones guardar / cerrar
        var btnGuardar = new Button { Content = "Guardar", Width = 90, Height = 32,
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        var btnCerrarModal = new Button { Content = "Cerrar", Width = 80, Height = 32, Margin = new Thickness(8,0,0,0),
            Background = BrGris, Foreground = BrBlanco, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrarModal.Click += (_, _) => dlg.Close();

        btnGuardar.Click += async (_, _) => {
            if (idProvModal == 0)                              { MessageBox.Show("Seleccione un proveedor."); return; }
            if (string.IsNullOrWhiteSpace(txtFactura.Text))   { MessageBox.Show("Ingrese N° de factura."); return; }

            // ── Modal de permiso de usuario ───────────────────────────────
            var usuarioAutorizado = await MostrarPermisoUsuario(dlg);
            if (usuarioAutorizado == null) return;

            byte forma  = 1;
            byte metodo = (cboMetodo.SelectedItem as ComboBoxItem)?.Tag is byte b2 ? b2 : (byte)1;

            decimal.TryParse(new string(txtDescuento.Text.Where(char.IsDigit).ToArray()), out var descuento);
            var totalFinal = total - descuento;
            if (totalFinal < 0) totalFinal = 0;
            var factura = txtFactura.Text.Trim();
            var nota    = txtNota.Text.Trim();

            // Cerrar modal de confirmación antes de ejecutar el guardado
            dlg.Close();

            await EjecutarGuardado(idProvModal, factura, nota, forma, metodo, total, totalFinal, descuento, usuarioAutorizado.IdUsuario, idlocal);
        };

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Children.Add(btnGuardar); btnRow.Children.Add(btnCerrarModal);
        body.Children.Add(btnRow);

        dlg.Content = body;
        dlg.Loaded += (_, _) => txtFactura.Focus();
        dlg.ShowDialog();
    }

    // ── Modal PERMISO DE USUARIOS (igual al sistema viejo) ───────────────
    private record UsuarioPermiso(int IdUsuario, string Nombre);

    private async Task<UsuarioPermiso?> MostrarPermisoUsuario(Window owner)
    {
        UsuarioPermiso? resultado = null;

        List<dynamic> usuarios = new();
        try {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT ID_USUARIO, NOMBRE_USUARIO, CODIGO_USUARIO, CONTRASEÑA_USUARIO FROM USUARIOS ORDER BY NOMBRE_USUARIO");
            usuarios = rows.ToList();
        } catch (Exception ex) { MessageBox.Show($"Error cargando usuarios: {ex.Message}"); return null; }

        var W  = System.Windows.Media.Brushes.White;
        var BrN = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,140,0));
        var BrD = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224,112,0));
        var BrG = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128));
        var BrV = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22,163,74));
        var BrF = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249,250,251));
        var BrB = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209,213,219));
        var BrT = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81));

        var dlgPerm = new Window {
            Title = "Autorización requerida", Width = 400, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner,
            ResizeMode = ResizeMode.NoResize, Background = W
        };

        // ── Header naranja ───────────────────────────────────────────────
        var header = new Border {
            Background = BrN, Padding = new Thickness(20, 16, 20, 16)
        };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🔐", FontSize = 22,
            Foreground = W, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,12,0)
        });
        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock {
            Text = "PERMISO DE USUARIOS",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = W
        });
        headerText.Children.Add(new TextBlock {
            Text = "Ingrese sus credenciales para confirmar",
            FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(200,255,255,255))
        });
        headerSp.Children.Add(headerText);
        header.Child = headerSp;

        // ── Cuerpo con campos ────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 8) };

        TextBlock FL(string t) => new TextBlock {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrT, Margin = new Thickness(0,0,0,4)
        };
        TextBox FT() => new TextBox {
            Height = 36, FontSize = 13, Padding = new Thickness(10,0,10,0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = BrB, BorderThickness = new Thickness(1),
            Background = BrF
        };
        PasswordBox FP() => new PasswordBox {
            Height = 36, FontSize = 13, Padding = new Thickness(10,0,10,0),
            BorderBrush = BrB, BorderThickness = new Thickness(1),
            Background = BrF
        };

        // Usuario
        var cboUsuario = new ComboBox {
            Height = 36, FontSize = 13, Margin = new Thickness(0,0,0,14),
            BorderBrush = BrB, BorderThickness = new Thickness(1), Background = W
        };
        foreach (dynamic u in usuarios)
            cboUsuario.Items.Add(new ComboBoxItem { Content = (string)u.NOMBRE_USUARIO, Tag = u });
        cboUsuario.SelectedIndex = 0;

        body.Children.Add(FL("Usuario"));
        body.Children.Add(cboUsuario);

        var txtCodigo = FT(); txtCodigo.Margin = new Thickness(0,0,0,14);
        body.Children.Add(FL("Código"));
        body.Children.Add(txtCodigo);

        var txtPassword = FP(); txtPassword.Margin = new Thickness(0,0,0,6);
        body.Children.Add(FL("Contraseña"));
        body.Children.Add(txtPassword);

        // ── Footer con botones ───────────────────────────────────────────
        var footer = new Border {
            Background = BrF,
            BorderBrush = BrB, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(20, 12, 20, 12), Margin = new Thickness(0,14,0,0)
        };
        var btnAceptar = new Button {
            Content = "✔  Aceptar", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = BrV, Foreground = W, FontWeight = FontWeights.Bold,
            FontSize = 13, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnCerrar = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(16,0,16,0),
            Background = BrG, Foreground = W, FontWeight = FontWeights.SemiBold,
            FontSize = 13, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(8,0,0,0)
        };
        var footerBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footerBtns.Children.Add(btnAceptar); footerBtns.Children.Add(btnCerrar);
        footer.Child = footerBtns;

        // ── Lógica ───────────────────────────────────────────────────────
        btnCerrar.Click  += (_, _) => dlgPerm.Close();

        Action confirmar = () => {
            if (cboUsuario.SelectedItem is not ComboBoxItem ci) { MessageBox.Show("Seleccione un usuario."); return; }
            dynamic u = ci.Tag;
            if (txtCodigo.Text.Trim()      != u.CODIGO_USUARIO.ToString())     { MessageBox.Show("Código incorrecto.",    "Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtCodigo.Focus(); txtCodigo.SelectAll(); return; }
            if (txtPassword.Password.Trim() != u.CONTRASEÑA_USUARIO.ToString()) { MessageBox.Show("Contraseña incorrecta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtPassword.Focus(); txtPassword.SelectAll(); return; }
            resultado = new UsuarioPermiso((int)u.ID_USUARIO, (string)u.NOMBRE_USUARIO);
            dlgPerm.DialogResult = true;
            dlgPerm.Close();
        };
        btnAceptar.Click += (_, _) => confirmar();
        txtPassword.KeyDown += (_, e) => { if (e.Key == Key.Enter) confirmar(); };
        txtCodigo.KeyDown   += (_, e) => { if (e.Key == Key.Enter) txtPassword.Focus(); };

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(footer);
        dlgPerm.Content = root;
        dlgPerm.Loaded  += (_, _) => { txtCodigo.Focus(); };
        dlgPerm.ShowDialog();
        return resultado;
    }

    private async Task EjecutarGuardado(int idProv, string factura, string nota, byte forma, byte metodo,
        decimal total, decimal totalFinal, decimal descuento, int idu, byte idlocal)
    {
        try
        {
            using var conn = _db.Create();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",      i == 0 ? "SI" : "NO");
                p.Add("@IDCABTMP",    0);
                p.Add("@FACTURA",     factura);
                p.Add("@PARCIAL",     (decimal)0);
                p.Add("@DESCUENTO",   descuento);
                p.Add("@TOTAL",       totalFinal);
                p.Add("@FORMA",       forma);
                p.Add("@METODO",      metodo);
                p.Add("@ID_BANCO",    1); // 1 = "NO DEFINIDO", FK requiere valor válido
                p.Add("@IDP",         idProv);
                p.Add("@IDU",         idu);
                p.Add("@STATUS",      (byte)1);
                p.Add("@NOTA",        nota);
                p.Add("@IDDETTMP",    0);
                p.Add("@IDART",       item.IdArt);
                p.Add("@CA",          item.Codigo);
                p.Add("@D",           item.Descripcion);
                p.Add("@CANT",        item.Cantidad);
                p.Add("@PC",          item.PrecioCosto);
                p.Add("@PVENTA",      item.PrecioVenta);
                p.Add("@CONTADO",     item.Contado);
                p.Add("@PPROMO",      item.PPromo);
                p.Add("@IDENTIFICADOR", 0);
                p.Add("@IDLOCAL",     idlocal);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 30);
                await conn.ExecuteAsync("JEJOGUA_TMP_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") { MessageBox.Show($"Error en ítem {i + 1}: {msg}"); return; }
            }

            // Leer IDCABTMP e INTERNO directamente desde lo que insertó JEJOGUA_TMP_CS
            var cabTmp = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 IDCABTMP, INTERNO FROM CAB_BUY_TMP WHERE FACTURA=@F ORDER BY IDCABTMP DESC",
                new { F = factura });
            if (cabTmp == null) { MessageBox.Show("No se encontró la compra temporal."); return; }
            int idCabViejo  = (int)cabTmp.IDCABTMP;
            var comprobante = (string)cabTmp.INTERNO;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",      i + 1);
                p.Add("@ultimo",      _items.Count);
                p.Add("@IDCABVIEJO",  idCabViejo);
                p.Add("@IDCABBUYS",   0);
                p.Add("@COMPROBANTE", comprobante);
                p.Add("@FACTURA",     factura);
                p.Add("@PARCIAL",     "0");
                p.Add("@PUNITORIO",   (decimal)0);
                p.Add("@DESCUENTO",   descuento);
                p.Add("@SUBTOTAL",    total);
                p.Add("@HABER",       (decimal)0);
                p.Add("@TOTALFINAL",  totalFinal);
                p.Add("@FORMA",       forma);
                p.Add("@METODO",      metodo);
                p.Add("@ID_BANCO",    1); // 1 = "NO DEFINIDO", FK requiere valor válido
                p.Add("@IDP",         idProv);
                p.Add("@IDU",         idu);
                p.Add("@ESTADO",      (byte)1);
                p.Add("@ID_LOCAL",    idlocal);
                p.Add("@NOTA",        nota);
                p.Add("@IDDETBUYS",   0);
                p.Add("@IDENTIFICADOR", 0);
                p.Add("@IDART",       item.IdArt);
                p.Add("@PC",          item.PrecioCosto);
                p.Add("@CANTIDAD",    item.Cantidad);
                p.Add("@IDPRICES",    item.IdPrices);
                p.Add("@PVENTA",      item.PrecioVenta);
                p.Add("@CONTADO",     item.Contado);
                p.Add("@PPROMO",      item.PPromo);
                p.Add("@IDMOVART",    0);
                p.Add("@MOV",         (byte)4);
                p.Add("@MOD",         (byte)1);
                p.Add("@stini",       (decimal)0);
                p.Add("@IDLOCAL",     idlocal);
                p.Add("@IDDESTINO",   idlocal);
                p.Add("@pcant",       (decimal)0);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
                await conn.ExecuteAsync("JOGUAANETE_CS", p, commandType: CommandType.StoredProcedure);
                var msg2 = p.Get<string>("@msg");
                if (msg2 != "GUARDADO") { MessageBox.Show($"Error al finalizar ítem {i + 1}: {msg2}"); return; }
            }

            // Resetear formulario para nueva compra sin cerrar la ventana
            _items.Clear();
            _idArtActual = 0; _idPricesActual = 0; _caActual = ""; _descActual = "";
            _txtBuscarArt.Text = ""; _txtCantidad.Text = "1";
            _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "";
            _lblNombreArt.Text = "";
            _lblUCFecha.Text = "—"; _lblUVFecha.Text = "—"; _lblMPFecha.Text = "—";
            _btnInsertar.IsEnabled = false;

            MessageBox.Show("Compra registrada correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);

            _txtBuscarArt.Focus();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar compra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task SeleccionarLocal(Button btnOrigen)
    {
        var modal = new BuscadorLocalModal(_db) { Owner = this };
        if (modal.ShowDialog() != true || modal.LocalSeleccionado == null) return;
        var loc = modal.LocalSeleccionado;
        _idLocalActual     = loc.IdLocal;
        _nombreLocalActual = loc.Nombre;
        _lblLocalNombre.Text  = $"📍 {loc.Nombre}";
        _lblLocalPrefijo.Text = "La compra se realizará en:";
        _localManualmenteSeleccionado = true;
        // Si ya hay artículo activo, recargar sus precios del nuevo local
        if (_idArtActual > 0) await RecargarPreciosLocal();
    }

    private async Task RecargarPreciosLocal()
    {
        using var conn = _db.Create();
        var p = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT IDPRICES, PC, PVENTA, CONTADO, PPROMO, FCOMPRA, FVENTA, FMP FROM PRICES WHERE IDART=@Id AND IDLOCAL=@L AND DELETADO=0",
            new { Id = _idArtActual, L = _idLocalActual });
        if (p != null)
        {
            _idPricesActual  = (int)p.IDPRICES;
            _txtPC.Text      = ((decimal)p.PC).ToString("N0");
            _txtPV.Text      = ((decimal)p.PVENTA).ToString("N0");
            _txtContado.Text = ((decimal)p.CONTADO).ToString("N0");
            _txtPPromo.Text  = ((decimal)p.PPROMO).ToString("N0");
            _lblUCFecha.Text = p.FCOMPRA is DateTime fc ? fc.ToString("dd/MM/yyyy") : "—";
            _lblUVFecha.Text = p.FVENTA  is DateTime fv ? fv.ToString("dd/MM/yyyy") : "—";
            _lblMPFecha.Text = p.FMP     is DateTime fm ? fm.ToString("dd/MM/yyyy") : "—";
        }
        else
        {
            _idPricesActual = 0;
            _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "0";
            _lblUCFecha.Text = _lblUVFecha.Text = _lblMPFecha.Text = "—";
        }
    }

    private async Task AbrirBuscadorArticulo()
    {
        var modal = new BuscadorArticuloModal(_db, (byte)_idLocalActual) { Owner = this };
        if (modal.ShowDialog() != true || modal.ArticuloSeleccionado == null) return;
        var a = modal.ArticuloSeleccionado;
        _idArtActual    = a.IdArt;
        _caActual       = a.Codigo;
        _descActual     = a.Descripcion;
        _idPricesActual = a.IdPrices;
        _txtBuscarArt.Text = a.Codigo;
        _lblNombreArt.Text = a.Descripcion;
        _txtPC.Text        = a.PrecioCosto.ToString("N0");
        _txtPV.Text        = a.PrecioVenta.ToString("N0");
        _txtContado.Text   = a.Contado.ToString("N0");
        _txtPPromo.Text    = a.PPromo.ToString("N0");
        // Cargar badges desde PRICES (FCOMPRA, FVENTA, FMP)
        using var conn = _db.Create();
        var mp = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT FCOMPRA, FVENTA, FMP FROM PRICES WHERE IDART=@Id AND IDLOCAL=@L AND DELETADO=0",
            new { Id = a.IdArt, L = _idLocalActual });
        if (mp != null)
        {
            _lblUCFecha.Text = mp.FCOMPRA is DateTime fc ? fc.ToString("dd/MM/yyyy") : "—";
            _lblUVFecha.Text = mp.FVENTA  is DateTime fv ? fv.ToString("dd/MM/yyyy") : "—";
            _lblMPFecha.Text = mp.FMP     is DateTime fm ? fm.ToString("dd/MM/yyyy") : "—";
        }
        else { _lblUCFecha.Text = _lblUVFecha.Text = _lblMPFecha.Text = "—"; }
        _btnInsertar.IsEnabled = true;
        _txtCantidad.Text = "1";
        _txtCantidad.Focus();
        _txtCantidad.SelectAll();
        _txtCantidad.SelectAll();
    }

    private Task<dynamic?> SeleccionarItem(List<dynamic> rows, string displayProp, string titulo)
    {
        var tcs = new TaskCompletionSource<dynamic?>();
        var win = new Window { Title = titulo, Width = 560, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.CanResize };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, ItemsSource = rows, Margin = new Thickness(8) };
        grid.Columns.Add(new DataGridTextColumn { Header = displayProp, Binding = new System.Windows.Data.Binding($"[{displayProp}]"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.SelectedIndex = 0;
        Grid.SetRow(grid, 0); root.Children.Add(grid);
        var btnOk = MakeBtn("Seleccionar", BrVerde); btnOk.Margin = new Thickness(8);
        btnOk.Click += (_, _) => { win.DialogResult = true; };
        Grid.SetRow(btnOk, 1); root.Children.Add(btnOk);
        win.Content = root;
        grid.MouseDoubleClick += (_, _) => { win.DialogResult = true; };
        win.Closed += (_, _) => tcs.TrySetResult(win.DialogResult == true ? (dynamic?)grid.SelectedItem : null);
        win.ShowDialog();
        return tcs.Task;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE LOCALES (modal estándar)
// ══════════════════════════════════════════════════════════════════════════════
public class LocalItem
{
    public int    IdLocal { get; set; }
    public string Codigo  { get; set; } = "";
    public string Nombre  { get; set; } = "";
}

public class BuscadorLocalModal : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtBuscar  = null!;
    private DataGrid  _grid       = null!;
    private TextBlock _lblConteo  = null!;
    private List<LocalItem> _todos = new();

    public LocalItem? LocalSeleccionado { get; private set; }

    private static readonly System.Windows.Media.SolidColorBrush BrNaranja =
        new(System.Windows.Media.Color.FromRgb(255, 140, 0));
    private static readonly System.Windows.Media.SolidColorBrush BrGris =
        new(System.Windows.Media.Color.FromRgb(107, 114, 128));

    public BuscadorLocalModal(IDbConnectionFactory db)
    {
        _db   = db;
        Title = "Seleccionar Local";
        Width = 500; Height = 420;
        MinWidth = 400; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerBg = new Border { Background = BrNaranja, Padding = new Thickness(12, 10, 12, 10) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "📍", FontSize = 16, Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0)
        });
        headerSp.Children.Add(new TextBlock {
            Text = "Buscar local:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0)
        });
        _txtBuscar = new TextBox {
            Width = 220, Height = 30, FontSize = 13,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("Codigo"), Width = new DataGridLength(70) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("Nombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8, 8, 8, 8)
        };
        _lblConteo = new TextBlock { FontSize = 11, Foreground = BrGris, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSelec  = MkBtn("✔  Seleccionar", "#22C55E");
        var btnCerrar = MkBtn("✕  Cerrar",       "#6B7280");
        btnSelec.Click  += (_, _) => Seleccionar();
        btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnsSp.Children.Add(btnSelec); btnsSp.Children.Add(btnCerrar);

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_lblConteo, 0); Grid.SetColumn(btnsSp, 1);
        barGrid.Children.Add(_lblConteo); barGrid.Children.Add(btnsSp);
        barBtns.Child = barGrid;
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        return s;
    }

    private async Task CargarAsync()
    {
        using var conn = _db.Create();
        var rows = await conn.QueryAsync<LocalItem>(
            "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as Nombre FROM LOCALES ORDER BY ID_LOCAL");
        _todos = rows.ToList();
        ActualizarGrid(_todos);
    }

    private void Filtrar()
    {
        var q = _txtBuscar.Text.Trim();
        var lista = string.IsNullOrEmpty(q)
            ? _todos
            : _todos.Where(l =>
                l.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                l.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ActualizarGrid(lista);
    }

    private void ActualizarGrid(List<LocalItem> lista)
    {
        _grid.ItemsSource = lista;
        _grid.SelectedItem = null;
        _lblConteo.Text = $"{lista.Count} local{(lista.Count != 1 ? "es" : "")} encontrado{(lista.Count != 1 ? "s" : "")}";
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is LocalItem l)
        { LocalSeleccionado = l; DialogResult = true; Close(); }
        else
            MessageBox.Show("Seleccione un local de la lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE ARTÍCULOS (modal estándar, igual que BuscadorClienteModal)
// ══════════════════════════════════════════════════════════════════════════════
public class ArticuloResumen
{
    public int     IdArt        { get; set; }
    public int     IdPrices     { get; set; }
    public string  Codigo       { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public decimal Stock        { get; set; }
    public decimal PrecioCosto  { get; set; }
    public decimal PrecioVenta  { get; set; }
    public decimal Contado      { get; set; }
    public decimal PPromo       { get; set; }
    public string  UltimaCompra { get; set; } = "ÚLTIMA COMPRA: —";
    public string  UltimaVenta  { get; set; } = "ÚLTIMA VENTA: —";
    public string  StockFmt     => Stock.ToString("N0");
    public string  PCFmt        => PrecioCosto.ToString("N0");
    public string  PVFmt        => PrecioVenta.ToString("N0");
}

public class BuscadorArticuloModal : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly byte                 _idlocal;
    private TextBox  _txtBuscar = null!;
    private DataGrid _grid      = null!;
    private TextBlock _lblConteo = null!;
    private List<ArticuloResumen> _todos = new();

    public ArticuloResumen? ArticuloSeleccionado { get; private set; }

    private static readonly System.Windows.Media.SolidColorBrush BrNaranja =
        new(System.Windows.Media.Color.FromRgb(255, 140, 0));
    private static readonly System.Windows.Media.SolidColorBrush BrGris =
        new(System.Windows.Media.Color.FromRgb(107, 114, 128));

    public BuscadorArticuloModal(IDbConnectionFactory db, byte idlocal)
    {
        _db      = db;
        _idlocal = idlocal;
        Title    = "Buscar Artículo";
        Width    = 780; Height = 520;
        MinWidth = 600; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header búsqueda ──
        var headerBg = new Border {
            Background = BrNaranja,
            Padding = new Thickness(12, 10, 12, 10)
        };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 16, Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0)
        });
        headerSp.Children.Add(new TextBlock {
            Text = "Código o descripción del artículo:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0)
        });
        _txtBuscar = new TextBox {
            Width = 300, Height = 30, FontSize = 13,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.White,
            Foreground = System.Windows.Media.Brushes.Black,
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        // ── DataGrid ──
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Codigo"),      Width = new DataGridLength(100) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("Descripcion"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Stock",       Binding = new System.Windows.Data.Binding("StockFmt"),    Width = new DataGridLength(70) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "P. Costo",    Binding = new System.Windows.Data.Binding("PCFmt"),       Width = new DataGridLength(90) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "P. Venta",    Binding = new System.Windows.Data.Binding("PVFmt"),       Width = new DataGridLength(90) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        // ── Footer ──
        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 8, 8, 8)
        };
        _lblConteo = new TextBlock {
            FontSize = 11, Foreground = BrGris,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSelec  = MkBtn("✔  Seleccionar", "#22C55E");
        var btnCerrar = MkBtn("✕  Cerrar",       "#6B7280");
        btnSelec.Click  += (_, _) => Seleccionar();
        btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnsSp.Children.Add(btnSelec);
        btnsSp.Children.Add(btnCerrar);

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_lblConteo, 0); Grid.SetColumn(btnsSp, 1);
        barGrid.Children.Add(_lblConteo); barGrid.Children.Add(btnsSp);
        barBtns.Child = barGrid;
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        return s;
    }

    private async Task CargarAsync()
    {
        using var conn = _db.Create();
        // When idlocal=0 (no local selected), pick any available price row per article
        var sql = _idlocal == 0
            ? @"SELECT a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D,
                     ISNULL(p.IDPRICES,0) as IDPRICES,
                     ISNULL(p.PC,0) as PC, ISNULL(p.PVENTA,0) as PVENTA,
                     ISNULL(p.CONTADO,0) as CONTADO, ISNULL(p.PPROMO,0) as PPROMO,
                     ISNULL(p.S,0) as STOCK
              FROM ARTICULOS a
              OUTER APPLY (
                  SELECT TOP 1 IDPRICES, PC, PVENTA, CONTADO, PPROMO, S
                  FROM PRICES WHERE IDART = a.ID AND DELETADO = 0
                  ORDER BY IDLOCAL
              ) p
              WHERE a.ES = 1
              ORDER BY a.D"
            : @"SELECT a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D,
                     ISNULL(p.IDPRICES,0) as IDPRICES,
                     ISNULL(p.PC,0) as PC, ISNULL(p.PVENTA,0) as PVENTA,
                     ISNULL(p.CONTADO,0) as CONTADO, ISNULL(p.PPROMO,0) as PPROMO,
                     ISNULL(p.S,0) as STOCK
              FROM ARTICULOS a
              LEFT JOIN PRICES p ON p.IDART = a.ID AND p.IDLOCAL = @L AND p.DELETADO = 0
              WHERE a.ES = 1
              ORDER BY a.D";
        var rows = await conn.QueryAsync<dynamic>(sql, new { L = _idlocal });
        _todos = rows.Select(r => new ArticuloResumen {
            IdArt       = (int)r.IDART,
            IdPrices    = (int)r.IDPRICES,
            Codigo      = (string)r.CA,
            Descripcion = (string)r.D,
            Stock       = (decimal)r.STOCK,
            PrecioCosto = (decimal)r.PC,
            PrecioVenta = (decimal)r.PVENTA,
            Contado     = (decimal)r.CONTADO,
            PPromo      = (decimal)r.PPROMO,
        }).ToList();
        ActualizarGrid(_todos);
    }

    private void Filtrar()
    {
        var q = _txtBuscar.Text.Trim();
        var lista = string.IsNullOrEmpty(q)
            ? _todos
            : _todos.Where(a =>
                a.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase))
              .ToList();
        ActualizarGrid(lista);
    }

    private void ActualizarGrid(List<ArticuloResumen> lista)
    {
        _grid.ItemsSource = lista;
        _grid.SelectedItem = null;
        _lblConteo.Text = $"{lista.Count} artículo{(lista.Count != 1 ? "s" : "")} encontrado{(lista.Count != 1 ? "s" : "")}";
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is ArticuloResumen a)
        {
            ArticuloSeleccionado = a;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Seleccione un artículo de la lista.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE PROVEEDORES (modal estándar)
// ══════════════════════════════════════════════════════════════════════════════
public class ProveedorItem
{
    public int    IdProveedor { get; set; }
    public string Ruc         { get; set; } = "";
    public string Nombre      { get; set; } = "";
}

public class BuscadorProveedorModal : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtBuscar = null!;
    private DataGrid  _grid      = null!;
    private TextBlock _lblConteo = null!;
    private List<ProveedorItem> _todos = new();

    public ProveedorItem? ProveedorSeleccionado { get; private set; }

    private static readonly System.Windows.Media.SolidColorBrush BrNaranja =
        new(System.Windows.Media.Color.FromRgb(255, 140, 0));
    private static readonly System.Windows.Media.SolidColorBrush BrGris =
        new(System.Windows.Media.Color.FromRgb(107, 114, 128));

    public BuscadorProveedorModal(IDbConnectionFactory db)
    {
        _db   = db;
        Title = "Seleccionar Proveedor";
        Width = 580; Height = 460;
        MinWidth = 460; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerBg = new Border { Background = BrNaranja, Padding = new Thickness(12, 10, 12, 10) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🏭", FontSize = 16, Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0)
        });
        headerSp.Children.Add(new TextBlock {
            Text = "Buscar proveedor:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0)
        });
        _txtBuscar = new TextBox {
            Width = 240, Height = 30, FontSize = 13,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, RowHeight = 32, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "RUC",    Binding = new System.Windows.Data.Binding("Ruc"),    Width = new DataGridLength(120) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("Nombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8, 8, 8, 8)
        };
        _lblConteo = new TextBlock { FontSize = 11, Foreground = BrGris, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSeleccionar = MkBtn("Seleccionar", "#FF8C00");
        var btnCerrar      = MkBtn("Cerrar",      "#6B7280");
        btnSeleccionar.Click += (_, _) => Seleccionar();
        btnCerrar.Click      += (_, _) => Close();

        var dockBtns = new DockPanel();
        DockPanel.SetDock(_lblConteo, Dock.Left);
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        rightBtns.Children.Add(btnSeleccionar); rightBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(rightBtns, Dock.Right);
        dockBtns.Children.Add(rightBtns); dockBtns.Children.Add(_lblConteo);
        barBtns.Child = dockBtns;
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private Style BuildHeaderStyle()
    {
        var hdr = typeof(System.Windows.Controls.Primitives.DataGridColumnHeader);
        var s = new Style(hdr);
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243,244,246))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8,0,8,0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.HeightProperty, 32.0));
        return s;
    }

    private async Task CargarAsync()
    {
        try {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<ProveedorItem>(
                "SELECT ID_PROVEEDOR as IdProveedor, ISNULL(RUC_PROVEEDOR,'') as Ruc, NOMBRE_PROVEEDOR as Nombre FROM PROVEEDORES ORDER BY NOMBRE_PROVEEDOR");
            _todos = rows.ToList();
            ActualizarGrid(_todos);
        } catch (Exception ex) { MessageBox.Show($"Error cargando proveedores: {ex.Message}"); }
    }

    private void Filtrar()
    {
        var q = _txtBuscar.Text.Trim();
        var lista = string.IsNullOrEmpty(q)
            ? _todos
            : _todos.Where(p =>
                p.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Ruc.Contains(q, StringComparison.OrdinalIgnoreCase))
              .ToList();
        ActualizarGrid(lista);
    }

    private void ActualizarGrid(List<ProveedorItem> lista)
    {
        _grid.ItemsSource = lista;
        _grid.SelectedItem = null;
        _lblConteo.Text = $"{lista.Count} proveedor{(lista.Count != 1 ? "es" : "")} encontrado{(lista.Count != 1 ? "s" : "")}";
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is ProveedorItem p)
        {
            ProveedorSeleccionado = p;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Seleccione un proveedor de la lista.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  COMPRA RÁPIDA  — misma lógica, entrada por ID numérico de artículo
// ══════════════════════════════════════════════════════════════════════════════
public class CompraRapidaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly SessionService       _sesion;

    private TextBox   _txtIdProv      = null!;
    private TextBox   _txtFactura     = null!;
    private TextBox   _txtIdArt       = null!;
    private TextBox   _txtCantidad    = null!;
    private TextBox   _txtPC          = null!;
    private TextBox   _txtPV          = null!;
    private TextBox   _txtContado     = null!;
    private DataGrid  _gridDetalle    = null!;
    private TextBlock _lblTotal       = null!;
    private readonly ObservableCollection<LineaCompra> _items = new();

    public CompraRapidaWindow()
    {
        _db     = App.Services.GetRequiredService<IDbConnectionFactory>();
        _sesion = SessionService.Instance;
        Title   = "Compra Rápida"; Width = 860; Height = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        DockPanel.SetDock(CW.Hdr("Compra Rápida", "#154360"), Dock.Top); root.Children.Add(CW.Hdr("Compra Rápida", "#154360"));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnG = CW.Btn("✔ Confirmar Compra", "#27AE60"); btnG.Click += async (_, _) => await Confirmar();
        var btnC = CW.Btn("Cancelar", "#757575");            btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnG); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var main = new StackPanel { Margin = new Thickness(12) };

        var cab = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        cab.Children.Add(CW.L("ID Proveedor:")); _txtIdProv   = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 70 };  cab.Children.Add(_txtIdProv);
        cab.Children.Add(CW.L("  N° Factura:")); _txtFactura  = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 130 }; cab.Children.Add(_txtFactura);
        main.Children.Add(cab);

        var art = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        art.Children.Add(CW.L("IDART:"));     _txtIdArt   = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 80 };  art.Children.Add(_txtIdArt);
        art.Children.Add(CW.L("  Cant:"));    _txtCantidad= new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 60, Text = "1" }; art.Children.Add(_txtCantidad);
        art.Children.Add(CW.L("  P.Costo:")); _txtPC      = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 80, Text = "0" }; art.Children.Add(_txtPC);
        art.Children.Add(CW.L("  P.Venta:")); _txtPV      = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 80, Text = "0" }; art.Children.Add(_txtPV);
        art.Children.Add(CW.L("  Contado:")); _txtContado = new TextBox { Padding = new Thickness(4, 2, 4, 2), Width = 80, Text = "0" }; art.Children.Add(_txtContado);
        var btnA = CW.SmBtn("+ Agregar", "#2980B9"); btnA.Click += async (_, _) => await AgregarFila(); art.Children.Add(btnA);
        var btnQ = CW.SmBtn("Quitar",    "#C0392B"); btnQ.Click += (_, _) => { if (_gridDetalle.SelectedItem is LineaCompra lc) _items.Remove(lc); }; art.Children.Add(btnQ);
        main.Children.Add(art);

        _gridDetalle = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue, Height = 300 };
        _gridDetalle.Columns.Add(CW.Col("ID",          "IdArt",       55));
        _gridDetalle.Columns.Add(CW.Col("Descripción", "Descripcion", new DataGridLength(1, DataGridLengthUnitType.Star)));
        _gridDetalle.Columns.Add(CW.Col("Cant",        "Cantidad",    60));
        _gridDetalle.Columns.Add(CW.Col("P.Costo",     "PrecioCosto", 80));
        _gridDetalle.Columns.Add(CW.Col("Subtotal",    "Subtotal",    90));
        _gridDetalle.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => _lblTotal.Text = $"Total: {_items.Sum(i => i.Subtotal):N0}";
        main.Children.Add(_gridDetalle);

        _lblTotal = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right };
        main.Children.Add(_lblTotal);
        root.Children.Add(main);
        Content = root;
    }

    private async Task AgregarFila()
    {
        if (!int.TryParse(_txtIdArt.Text.Trim(), out var idArt))           { MessageBox.Show("ID artículo inválido."); return; }
        if (!decimal.TryParse(_txtCantidad.Text, out var cant) || cant<=0) { MessageBox.Show("Cantidad inválida."); return; }
        if (!decimal.TryParse(_txtPC.Text,       out var pc))              { MessageBox.Show("P.Costo inválido."); return; }
        decimal.TryParse(_txtPV.Text, out var pv);
        decimal.TryParse(_txtContado.Text, out var pco);
        try
        {
            using var conn = _db.Create();
            var art = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT ID as IDART, CA as CODIGO_ART, D as DESCRIPCION FROM ARTICULOS WHERE ID=@id", new { id = idArt });
            if (art == null) { MessageBox.Show("Artículo no encontrado."); return; }
            _items.Add(new LineaCompra {
                IdArt = idArt, Codigo = (string)art.CODIGO_ART,
                Descripcion = (string)art.DESCRIPCION, Cantidad = cant,
                PrecioCosto = pc, PrecioVenta = pv, Contado = pco,
                Subtotal = cant * pc });
            _txtIdArt.Text = ""; _txtCantidad.Text = "1"; _txtPC.Text = "0"; _txtPV.Text = "0"; _txtContado.Text = "0";
            _txtIdArt.Focus();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }

    private async Task Confirmar()
    {
        if (!int.TryParse(_txtIdProv.Text.Trim(), out var idProv) || idProv == 0) { MessageBox.Show("ID proveedor inválido."); return; }
        if (_items.Count == 0)                                                     { MessageBox.Show("Agregue al menos un artículo."); return; }
        if (string.IsNullOrWhiteSpace(_txtFactura.Text))                           { MessageBox.Show("Ingrese N° de factura."); return; }

        var  idu     = _sesion.UsuarioActual?.IdUsuario ?? 1;
        var  idlocal = (byte)(_sesion.LocalActual?.IdLocal ?? 1);
        var  total   = (decimal)_items.Sum(i => i.Subtotal);
        var  factura = _txtFactura.Text.Trim();

        try
        {
            using var conn = _db.Create();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",      i == 0 ? "SI" : "NO");
                p.Add("@IDCABTMP",    0);
                p.Add("@FACTURA",     factura);
                p.Add("@PARCIAL",     (decimal)0);
                p.Add("@DESCUENTO",   (decimal)0);
                p.Add("@TOTAL",       total);
                p.Add("@FORMA",       (byte)1);
                p.Add("@METODO",      (byte)1);
                p.Add("@ID_BANCO",    0);
                p.Add("@IDP",         idProv);
                p.Add("@IDU",         idu);
                p.Add("@STATUS",      (byte)0);
                p.Add("@NOTA",        "");
                p.Add("@IDDETTMP",    0);
                p.Add("@IDART",       item.IdArt);
                p.Add("@CA",          item.Codigo);
                p.Add("@D",           item.Descripcion);
                p.Add("@CANT",        item.Cantidad);
                p.Add("@PC",          item.PrecioCosto);
                p.Add("@PVENTA",      item.PrecioVenta);
                p.Add("@CONTADO",     item.Contado);
                p.Add("@PPROMO",      (decimal)0);
                p.Add("@IDENTIFICADOR", 0);
                p.Add("@IDLOCAL",     idlocal);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 30);
                await conn.ExecuteAsync("JEJOGUA_TMP_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") { MessageBox.Show($"Error en ítem {i + 1}: {msg}"); return; }
            }

            int idCabViejo = await conn.ExecuteScalarAsync<int>("SELECT ISNULL(MAX(IDCABTMP),0) FROM CAB_BUY_TMP");
            if (idCabViejo == 0) { MessageBox.Show("No se encontró la compra temporal."); return; }
            var comprobante = $"Credi-{idCabViejo:000000}";

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",      i + 1);
                p.Add("@ultimo",      _items.Count);
                p.Add("@IDCABVIEJO",  idCabViejo);
                p.Add("@IDCABBUYS",   0);
                p.Add("@COMPROBANTE", comprobante);
                p.Add("@FACTURA",     factura);
                p.Add("@PARCIAL",     "0");
                p.Add("@PUNITORIO",   (decimal)0);
                p.Add("@DESCUENTO",   (decimal)0);
                p.Add("@SUBTOTAL",    total);
                p.Add("@HABER",       (decimal)0);
                p.Add("@TOTALFINAL",  total);
                p.Add("@FORMA",       (byte)1);
                p.Add("@METODO",      (byte)1);
                p.Add("@ID_BANCO",    0);
                p.Add("@IDP",         idProv);
                p.Add("@IDU",         idu);
                p.Add("@ESTADO",      (byte)1);
                p.Add("@ID_LOCAL",    idlocal);
                p.Add("@NOTA",        "");
                p.Add("@IDDETBUYS",   0);
                p.Add("@IDENTIFICADOR", 0);
                p.Add("@IDART",       item.IdArt);
                p.Add("@PC",          item.PrecioCosto);
                p.Add("@CANTIDAD",    item.Cantidad);
                p.Add("@IDPRICES",    0);
                p.Add("@PVENTA",      item.PrecioVenta);
                p.Add("@CONTADO",     item.Contado);
                p.Add("@PPROMO",      (decimal)0);
                p.Add("@IDMOVART",    0);
                p.Add("@MOV",         (byte)4);
                p.Add("@MOD",         (byte)1);
                p.Add("@stini",       (decimal)0);
                p.Add("@IDLOCAL",     idlocal);
                p.Add("@IDDESTINO",   idlocal);
                p.Add("@pcant",       (decimal)0);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
                await conn.ExecuteAsync("JOGUAANETE_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") { MessageBox.Show($"Error al finalizar ítem {i + 1}: {msg}"); return; }
            }

            MessageBox.Show("Compra registrada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  Modelo de línea de compra
// ══════════════════════════════════════════════════════════════════════════════
internal class LineaCompra
{
    public int     IdArt       { get; set; }
    public int     IdPrices    { get; set; }
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public decimal Cantidad    { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal Contado     { get; set; }
    public decimal PPromo      { get; set; }
    public decimal Subtotal    { get; set; }

    public string PrecioCostoFmt => PrecioCosto.ToString("N0");
    public string PrecioVentaFmt => PrecioVenta.ToString("N0");
    public string ContadoFmt     => Contado.ToString("N0");
    public string PPromoFmt      => PPromo.ToString("N0");
    public string SubtotalFmt    => Subtotal.ToString("N0");
}

// ══════════════════════════════════════════════════════════════════════════════
//  Helpers UI compartidos dentro de este namespace
// ══════════════════════════════════════════════════════════════════════════════
internal static class CW
{
    static System.Windows.Media.BrushConverter _bc = new();
    internal static Border Hdr(string t, string hex) {
        var b = new Border { Background = (System.Windows.Media.Brush)_bc.ConvertFromString(hex)!, Padding = new Thickness(12, 8, 12, 8) };
        b.Child = new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
        return b;
    }
    internal static Button Btn(string t, string hex) => new Button {
        Content = t, Height = 32, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(4, 0, 4, 0),
        Background = (System.Windows.Media.Brush)_bc.ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
    internal static Button SmBtn(string t, string hex) => new Button {
        Content = t, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(3, 0, 3, 0),
        Background = (System.Windows.Media.Brush)_bc.ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
    internal static TextBlock L(string t) => new TextBlock {
        Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 2, 4, 2) };
    internal static DataGridTextColumn Col(string header, string binding, object width) {
        var col = new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(binding) };
        col.Width = width is DataGridLength dgl ? dgl : new DataGridLength((double)(int)width);
        return col;
    }
    internal static DataGridTextColumn Col(string header, string binding, int width)
        => new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(binding), Width = width };
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL / EDITAR COMPRAS
// ══════════════════════════════════════════════════════════════════════════════
public class LineaCompraEdit : System.ComponentModel.INotifyPropertyChanged
{
    private decimal _cantidad; private decimal _pc; private decimal _pv; private decimal _contado; private decimal _ppromo;
    public int     IdArt        { get; set; }
    public string  Codigo       { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public int     Identificador{ get; set; }
    public decimal Cantidad     { get => _cantidad; set { _cantidad = value; OnProp(); OnProp(nameof(SubtotalFmt)); } }
    public decimal PrecioCosto  { get => _pc;       set { _pc = value;       OnProp(); OnProp(nameof(SubtotalFmt)); } }
    public decimal PrecioVenta  { get => _pv;       set { _pv = value; OnProp(); } }
    public decimal Contado      { get => _contado;  set { _contado = value;  OnProp(); } }
    public decimal PPromo       { get => _ppromo;   set { _ppromo = value;   OnProp(); } }
    public decimal Subtotal     => Cantidad * PrecioCosto;
    public string  SubtotalFmt  => Subtotal.ToString("N0");
    public string  CantidadFmt  => Cantidad.ToString("N0");
    public string  PCFmt        => PrecioCosto.ToString("N0");
    public string  PVFmt        => PrecioVenta.ToString("N0");
    public string  ContadoFmt   => Contado.ToString("N0");
    public string  PPromoFmt    => PPromo.ToString("N0");
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnProp([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
}

public class EditarComprasWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly SessionService       _sesion;

    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb(255,140,  0));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb(224,112,  0));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 22,163, 74));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel    = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229,231,235));
    private static readonly System.Windows.Media.SolidColorBrush BrFondoArt = new(System.Windows.Media.Color.FromRgb(255,248,230));

    // Estado de la compra cargada
    private int     _idCabTmp;
    private string  _interno    = "";
    private int     _idProv;
    private string  _nombreProv = "";
    private int     _idBanco    = 1;
    private readonly System.Collections.ObjectModel.ObservableCollection<LineaCompraEdit> _items = new();

    // Controles cabecera
    private TextBox   _txtInterno    = null!;
    private TextBox   _txtFactura    = null!;
    private TextBox   _txtParcial    = null!;
    private TextBox   _txtDescuento  = null!;
    private TextBox   _txtTotal      = null!;
    private ComboBox  _cboMetodo     = null!;
    private TextBox   _txtIdBanco    = null!;
    private TextBox   _txtNomBanco   = null!;
    private TextBox   _txtIdProv     = null!;
    private TextBox   _txtNomProv    = null!;
    private TextBox   _txtNota       = null!;
    private TextBlock _lblIngresar    = null!;
    private TextBox   _txtBuscarArt  = null!;
    private TextBox   _txtCant       = null!;
    private TextBlock _lblNomArtEditar = null!;
    private DataGrid  _gridDetalle   = null!;
    private TextBlock _lblTotal      = null!;
    private Button    _btnGuardarMod = null!;
    private Button    _btnGuardarCompra = null!;

    public EditarComprasWindow()
    {
        _db    = App.Services.GetRequiredService<IDbConnectionFactory>();
        _sesion = SessionService.Instance;
        Title  = "Modificar datos de compras";
        Width  = 1200; Height = 700;
        MinWidth = 1000; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrPrimary;
        BuildUI();
        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key) {
            case Key.F2: AbrirBuscadorComprobante(); break;
            case Key.F5: _btnGuardarMod?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); break;
            case Key.F10: AbrirBuscadorBanco(); break;
            case Key.F11: AbrirBuscadorProveedor(); break;
            case Key.S when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                _btnGuardarMod?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); break;
        }
    }

    // ══ Construcción de UI ═══════════════════════════════════════════════
    private void BuildUI()
    {
        // colores extra para el diseño moderno
        var BrHeaderDark  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 90,  0));
        var BrSeparator   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60,255,255,255));
        var BrInputBg     = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(220,255,255,255));
        var BrArtPanel    = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,252,245));
        var BrChip        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50,255,255,255));
        var BrFooter      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30));
        var BrFooterLight = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50,50,50));

        // ── helpers ──────────────────────────────────────────────────────
        // label blanco pequeño sobre fondo naranja
        TextBlock LW(string t) => new TextBlock {
            Text = t, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200,255,255,255)),
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10,0,4,0) };

        // campo con borde redondeado sobre fondo naranja
        TextBox TBH(int w, bool ro = false) {
            var tb = new TextBox {
                Width = w, Padding = new Thickness(8,5,8,5), FontSize = 12, FontWeight = FontWeights.SemiBold,
                BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
                Background = BrInputBg, IsReadOnly = ro,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)),
                VerticalContentAlignment = VerticalAlignment.Center };
            if (ro) tb.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(130,255,255,255));
            return tb;
        }

        // campo normal (panel crema)
        TextBox TBL(int w, bool ro = false) => new TextBox {
            Width = w, Padding = new Thickness(6,5,6,5), FontSize = 12,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = ro ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240,240,240)) : BrBlanco,
            IsReadOnly = ro, VerticalContentAlignment = VerticalAlignment.Center };

        // botón oscuro naranja
        Button BtnDark(string txt) => new Button {
            Content = txt, Height = 30, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(6,0,0,0),
            Background = BrHeaderDark, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };

        // botón blanco con texto naranja
        Button BtnWhite(string txt) => new Button {
            Content = txt, Height = 30, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(6,0,0,0),
            Background = BrBlanco, Foreground = BrPrimDark,
            FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };

        // chip: rectángulo semitransparente con texto blanco (separa secciones)
        Border Chip(string txt) {
            var b = new Border {
                Background = BrChip, CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10,3,10,3), Margin = new Thickness(14,0,4,0),
                VerticalAlignment = VerticalAlignment.Center };
            b.Child = new TextBlock { Text = txt, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = BrBlanco };
            return b;
        }

        // contenedor de campo con label arriba (stack vertical)
        StackPanel Field(string lbl, UIElement ctrl) {
            var sp = new StackPanel { Margin = new Thickness(6,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180,255,255,255)),
                Margin = new Thickness(2,0,0,1) });
            sp.Children.Add(ctrl);
            return sp;
        }

        // ─────────────────────────────────────────────────────────────────
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: header principal
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: banco/prov
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: artículo
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 3: grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 4: footer

        // ══ Row 0: header naranja — comprobante ══════════════════════════
        var bar0 = new Border {
            Background = BrPrimary, Padding = new Thickness(12,10,12,10) };

        _txtInterno   = TBH(130, ro: true);
        var btnBuscar = BtnDark("🔍  Buscar");
        btnBuscar.Click += (_, _) => AbrirBuscadorComprobante();
        _txtFactura   = TBH(100);
        _txtParcial   = TBH(90, ro: true);
        _txtDescuento = TBH(80);
        _txtDescuento.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        _txtDescuento.TextChanged      += (_, _) => RecalcTotal();
        _txtTotal     = TBH(90, ro: true);
        _cboMetodo = new ComboBox { Width = 115, Height = 30, FontSize = 12,
            BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
            Background = BrInputBg, VerticalContentAlignment = VerticalAlignment.Center };
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Efectivo",      Tag = (byte)1 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia", Tag = (byte)2 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Cheque",        Tag = (byte)3 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta",       Tag = (byte)4 });
        _cboMetodo.SelectedIndex = 0;

        var row0Sp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        row0Sp.Children.Add(Field("COMPROBANTE", _txtInterno));
        row0Sp.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6,0,0,0), Children = { btnBuscar } });
        row0Sp.Children.Add(Chip("DOCUMENTO"));
        row0Sp.Children.Add(Field("Factura",    _txtFactura));
        row0Sp.Children.Add(Field("Parcial Gs.", _txtParcial));
        row0Sp.Children.Add(Field("Descuento",  _txtDescuento));
        row0Sp.Children.Add(Field("Total Gs.",  _txtTotal));
        row0Sp.Children.Add(Chip("PAGO"));
        row0Sp.Children.Add(Field("Método",     _cboMetodo));
        bar0.Child = row0Sp;
        Grid.SetRow(bar0, 0); root.Children.Add(bar0);

        // ══ Row 1: segunda barra naranja oscura — banco / proveedor / nota ══
        var bar1 = new Border {
            Background = BrHeaderDark, Padding = new Thickness(12,8,12,8) };

        TextBox RoH(int w) => new TextBox { Width = w, Padding = new Thickness(8,5,8,5), FontSize = 12,
            BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(130,255,255,255)),
            IsReadOnly = true, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)),
            VerticalContentAlignment = VerticalAlignment.Center };

        _txtIdBanco = new TextBox { Width = 38, Padding = new Thickness(6,5,6,5), FontSize = 12,
            BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
            Background = BrInputBg, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)),
            VerticalContentAlignment = VerticalAlignment.Center, Text = "1" };
        _txtNomBanco = RoH(190);
        var btnVerBanco = BtnWhite("Ver Bancos");
        btnVerBanco.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60,255,255,255));
        btnVerBanco.Foreground = BrBlanco;
        btnVerBanco.Click += (_, _) => AbrirBuscadorBanco();

        _txtIdProv = new TextBox { Width = 38, Padding = new Thickness(6,5,6,5), FontSize = 12,
            BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
            Background = BrInputBg, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)),
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtNomProv = RoH(220);
        var btnVerProv = BtnWhite("Ver Proveedores");
        btnVerProv.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60,255,255,255));
        btnVerProv.Foreground = BrBlanco;
        btnVerProv.Click += (_, _) => AbrirBuscadorProveedor();

        _txtNota = new TextBox { Padding = new Thickness(8,5,8,5), FontSize = 12,
            BorderBrush = BrSeparator, BorderThickness = new Thickness(0,0,0,2),
            Background = BrInputBg, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)),
            VerticalContentAlignment = VerticalAlignment.Center, MinWidth = 200 };

        var row1Sp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        row1Sp.Children.Add(Chip("BANCO"));
        row1Sp.Children.Add(Field("#", _txtIdBanco));
        row1Sp.Children.Add(Field("Nombre banco", _txtNomBanco));
        row1Sp.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6,0,0,0), Children = { btnVerBanco } });
        row1Sp.Children.Add(Chip("PROVEEDOR"));
        row1Sp.Children.Add(Field("#", _txtIdProv));
        row1Sp.Children.Add(Field("Nombre proveedor", _txtNomProv));
        row1Sp.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6,0,0,0), Children = { btnVerProv } });
        row1Sp.Children.Add(Chip("NOTA"));
        row1Sp.Children.Add(Field("Comentario / nota", _txtNota));
        bar1.Child = row1Sp;
        Grid.SetRow(bar1, 1); root.Children.Add(bar1);

        // ══ Row 2: panel artículo — fondo crema con acento naranja ═══════
        var bar2 = new Border {
            Background = BrArtPanel,
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(12,10,12,10) };

        // título sección
        var secTitle = new StackPanel { Orientation = Orientation.Horizontal,
            Margin = new Thickness(0,0,0,8) };
        var accentBar = new Border { Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrPrimary, Margin = new Thickness(0,0,8,0) };
        secTitle.Children.Add(accentBar);
        secTitle.Children.Add(new TextBlock { Text = "Ingresar mercadería",
            FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrPrimDark,
            VerticalAlignment = VerticalAlignment.Center });

        // fila búsqueda
        _txtBuscarArt = new TextBox { Width = 160, Padding = new Thickness(8,6,8,6), FontSize = 12,
            BorderBrush = BrPrimary, BorderThickness = new Thickness(0,0,0,2),
            Background = BrBlanco, VerticalContentAlignment = VerticalAlignment.Center };
        _txtBuscarArt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticuloEditar(); };

        var btnBuscarArt = new Button {
            Content = "Buscar", Height = 34, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(6,0,0,0), Background = BrPrimary, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnBuscarArt.Click += async (_, _) => await BuscarArticuloEditar();

        var btnListaArt = new Button {
            Content = "📋  Ver todos los artículos", Height = 34, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(6,0,0,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240,240,240)),
            Foreground = BrPrimDark, FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnListaArt.Click += (_, _) => AbrirBuscadorArticuloEditar();

        // label nombre artículo seleccionado con fondo pill
        var pillBorder = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,237,213)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(10,4,10,4),
            Margin = new Thickness(14,0,0,0), VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 180 };
        _lblNomArtEditar = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrPrimDark, Text = "— sin artículo seleccionado —",
            VerticalAlignment = VerticalAlignment.Center };
        pillBorder.Child = _lblNomArtEditar;

        var busqRow = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        busqRow.Children.Add(new TextBlock { Text = "Código:", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
        busqRow.Children.Add(_txtBuscarArt);
        busqRow.Children.Add(btnBuscarArt);
        busqRow.Children.Add(btnListaArt);
        busqRow.Children.Add(pillBorder);

        // fila cantidad + INSERTAR
        _txtCant = new TextBox { Width = 90, Padding = new Thickness(8,6,8,6), FontSize = 14,
            FontWeight = FontWeights.Bold, BorderBrush = BrPrimary, BorderThickness = new Thickness(0,0,0,2),
            Background = BrBlanco, TextAlignment = TextAlignment.Center, Text = "1",
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtCant.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await InsertarArticuloEditar(); };

        var btnInsertar = new Button {
            Height = 42, Padding = new Thickness(28,0,28,0),
            Background = BrVerde, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnInsertar.Content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
            Children = {
                new TextBlock { Text = "✚", FontSize = 16, Margin = new Thickness(0,0,6,0), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "INSERTAR", FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }
            }};
        btnInsertar.Click += async (_, _) => await InsertarArticuloEditar();

        var cantRow = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,8,0,0) };
        cantRow.Children.Add(new TextBlock { Text = "CANTIDAD A AGREGAR", FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = BrLabel,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        cantRow.Children.Add(_txtCant);

        var cantDock = new DockPanel();
        DockPanel.SetDock(btnInsertar, Dock.Right);
        cantDock.Children.Add(btnInsertar);
        cantDock.Children.Add(cantRow);

        var bar2Stack = new StackPanel();
        bar2Stack.Children.Add(secTitle);
        bar2Stack.Children.Add(busqRow);
        bar2Stack.Children.Add(cantDock);
        bar2.Child = bar2Stack;
        Grid.SetRow(bar2, 2); root.Children.Add(bar2);

        // ══ Row 3: DataGrid ═══════════════════════════════════════════════
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235,235,235)),
            RowBackground = BrBlanco,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,251,244)),
            FontSize = 12, RowHeight = 36, ColumnHeaderHeight = 34,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = BuildGridHdrStyle()
        };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("Codigo"),       Width = 110 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",
            Binding = new System.Windows.Data.Binding("CantidadFmt"),  Width = 70 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. costo",
            Binding = new System.Windows.Data.Binding("PCFmt"),        Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. venta",
            Binding = new System.Windows.Data.Binding("PVFmt"),        Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. contado",
            Binding = new System.Windows.Data.Binding("ContadoFmt"),   Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. promo",
            Binding = new System.Windows.Data.Binding("PPromoFmt"),    Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "SubTotal",
            Binding = new System.Windows.Data.Binding("SubtotalFmt"),  Width = 120 });
        _gridDetalle.MouseDoubleClick += (_, _) => EditarItemDetalle();
        _gridDetalle.KeyDown += (_, e) => {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && _gridDetalle.SelectedItem is LineaCompraEdit it)
                { _items.Remove(it); RecalcTotal(); }
        };
        _gridDetalle.ItemsSource = _items;
        Grid.SetRow(_gridDetalle, 3); root.Children.Add(_gridDetalle);

        // ══ Row 4: Footer (estilo original) ══════════════════════════════
        var footer = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230,230,230)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(10,6,10,6) };
        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var footLeft = new StackPanel();
        _lblTotal = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrPrimDark };
        var lblAtajos = new TextBlock {
            Text = "F2: Buscar comprobante   F10: Buscar banco   F11: Buscar proveedor   Doble click: Cambiar datos   Sup/Delet: Excluir artículo   F5: Guardar   Ctrl+S: Cerrar",
            FontSize = 9, Foreground = BrGris, Margin = new Thickness(0,2,0,0) };
        footLeft.Children.Add(_lblTotal); footLeft.Children.Add(lblAtajos);
        Grid.SetColumn(footLeft, 0); footGrid.Children.Add(footLeft);

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _btnGuardarMod    = MakeBtn("Guardar Modificación", BrVerde,   isEnabled: false);
        _btnGuardarCompra = MakeBtn("Guardar compra",       BrPrimary, isEnabled: false);
        var btnCerrar     = MakeBtn("Cerrar",               BrGris);
        _btnGuardarMod.Click    += async (_, _) => await GuardarModificacion();
        _btnGuardarCompra.Click += async (_, _) => await GuardarCompra();
        btnCerrar.Click         += (_, _) => Close();
        btnsSp.Children.Add(_btnGuardarMod);
        btnsSp.Children.Add(_btnGuardarCompra);
        btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 1); footGrid.Children.Add(btnsSp);
        footer.Child = footGrid;
        Grid.SetRow(footer, 4); root.Children.Add(footer);

        Content = root;
        RecalcTotal();
        Loaded += (_, _) => _txtBuscarArt.Focus();
    }

    private Button MakeBtn(string txt, System.Windows.Media.Brush bg, bool isEnabled = true) =>
        new Button { Content = txt, Height = 32, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(4,0,0,0), Background = bg, Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = isEnabled };

    private static Style BuildGridHdrStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,140,0))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
            System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8,0,8,0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
        return s;
    }

    private void RecalcTotal()
    {
        var parcial = _items.Sum(i => i.Subtotal);
        _txtParcial.Text = parcial.ToString("N0").Replace(",",".");
        decimal.TryParse(new string((_txtDescuento?.Text ?? "0").Where(char.IsDigit).ToArray()), out var desc);
        var neto = parcial - desc;
        if (neto < 0) neto = 0;
        _txtTotal.Text = neto.ToString("N0").Replace(",",".");
        _lblTotal.Text = $"Total: {neto:N0} Gs.";
    }

    // ── Buscar artículo para agregar al detalle ───────────────────────────
    private int    _idArtEditar;
    private string _caEditar   = "";
    private string _descEditar = "";
    private int    _idPricesEditar;

    private async Task BuscarArticuloEditar()
    {
        var term = _txtBuscarArt.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;
        try {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@CODI",  term);
            p.Add("@Local", (byte)(_sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@msg",   dbType: DbType.String, direction: ParameterDirection.Output, size: 12);
            var rows = (await conn.QueryAsync<dynamic>("BUSCAR_ART_COMPRATEMPORAL_CS", p,
                commandType: CommandType.StoredProcedure)).ToList();
            if (rows.Count == 0) { MessageBox.Show("Artículo no encontrado."); return; }

            dynamic a = rows.Count == 1 ? rows[0]
                : (await SeleccionarArticuloEditar(rows)) ?? rows[0];

            _idArtEditar  = (int)a.ID;
            _caEditar     = (string)a.CA;
            _descEditar   = (string)a.D;
            _txtBuscarArt.Text    = _caEditar;
            _lblNomArtEditar.Text = _descEditar;
            _txtCant.Focus(); _txtCant.SelectAll();
        } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }

    private void AbrirBuscadorArticuloEditar()
    {
        var dlg = new Window {
            Title = "Buscar artículo", Width = 620, Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrBlanco
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra naranja
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // campo búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        var topBar = new Border { Background = BrPrimary, Padding = new Thickness(12,10,12,10) };
        topBar.Child = new TextBlock { Text = "Seleccionar artículo", FontSize = 14,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        Grid.SetRow(topBar, 0); root.Children.Add(topBar);

        // barra de filtro
        var filterBar = new Border { Background = BrFondoArt, Padding = new Thickness(10,8,10,8),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,0,0,1) };
        var filterSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var txtFiltro = new TextBox { Width = 280, Height = 30, Padding = new Thickness(8,4,8,4),
            FontSize = 12, BorderBrush = BrPrimary, BorderThickness = new Thickness(0,0,0,2),
            VerticalContentAlignment = VerticalAlignment.Center };
        var btnFiltrar = new Button { Content = "Buscar", Height = 30, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(6,0,0,0), Background = BrPrimary, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        filterSp.Children.Add(new TextBlock { Text = "Filtrar:", FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        filterSp.Children.Add(txtFiltro);
        filterSp.Children.Add(btnFiltrar);
        filterBar.Child = filterSp;
        Grid.SetRow(filterBar, 1); root.Children.Add(filterBar);

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        var grid = new DataGrid {
            IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            Foreground = System.Windows.Media.Brushes.Black,
            CellStyle = cellStyle, ColumnHeaderStyle = BuildGridHdrStyle(),
            FontSize = 12, RowHeight = 32, BorderThickness = new Thickness(0)
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("CA"), Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción",
            Binding = new System.Windows.Data.Binding("D"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(grid, 2); root.Children.Add(grid);

        var footerBar = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245)),
            Padding = new Thickness(10,8,10,8), BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnSel   = new Button { Content = "Seleccionar", Height = 30, Padding = new Thickness(16,0,16,0),
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        var btnCancel = new Button { Content = "Cancelar", Height = 30, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(6,0,0,0), Background = BrGris, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        footSp.Children.Add(btnSel); footSp.Children.Add(btnCancel);
        footerBar.Child = footSp;
        Grid.SetRow(footerBar, 3); root.Children.Add(footerBar);

        // carga inicial y filtrado
        async Task Cargar(string filtro) {
            try {
                using var conn = _db.Create();
                string sql = string.IsNullOrWhiteSpace(filtro)
                    ? "SELECT TOP 200 ID, CA, D FROM ARTICULOS WHERE ES=1 ORDER BY D"
                    : "SELECT TOP 200 ID, CA, D FROM ARTICULOS WHERE ES=1 AND (CA LIKE @f OR D LIKE @f) ORDER BY D";
                var rows = (await conn.QueryAsync<dynamic>(sql,
                    string.IsNullOrWhiteSpace(filtro) ? null : new { f = $"%{filtro}%" })).ToList();
                grid.ItemsSource = rows;
            } catch { }
        }

        void Seleccionar() {
            if (grid.SelectedItem == null) return;
            var d = (IDictionary<string,object>)grid.SelectedItem;
            _idArtEditar  = Convert.ToInt32(d["ID"]);
            _caEditar     = d["CA"]?.ToString() ?? "";
            _descEditar   = d["D"]?.ToString()  ?? "";
            _txtBuscarArt.Text    = _caEditar;
            _lblNomArtEditar.Text = _descEditar;
            dlg.Close();
            _txtCant.Focus(); _txtCant.SelectAll();
        }

        btnFiltrar.Click        += async (_, _) => await Cargar(txtFiltro.Text.Trim());
        txtFiltro.KeyDown       += async (_, e) => { if (e.Key == Key.Enter) await Cargar(txtFiltro.Text.Trim()); };
        btnSel.Click            += (_, _) => Seleccionar();
        btnCancel.Click         += (_, _) => dlg.Close();
        grid.MouseDoubleClick   += (_, _) => Seleccionar();

        dlg.Content = root;
        dlg.Loaded += async (_, _) => { await Cargar(""); txtFiltro.Focus(); };
        dlg.ShowDialog();
    }

    private async Task<dynamic?> SeleccionarArticuloEditar(List<dynamic> rows)
    {
        dynamic? result = null;
        var dlg = new Window {
            Title = "Seleccionar artículo", Width = 500, Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrBlanco
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var topBar = new Border { Background = BrPrimary, Padding = new Thickness(10,8,10,8) };
        topBar.Child = new TextBlock { Text = "Seleccione el artículo", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        Grid.SetRow(topBar, 0); root.Children.Add(topBar);

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        var grid = new DataGrid {
            Margin = new Thickness(0), IsReadOnly = true, AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false,
            Foreground = System.Windows.Media.Brushes.Black,
            CellStyle = cellStyle,
            ColumnHeaderStyle = BuildGridHdrStyle()
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("CA"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("D"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.ItemsSource = rows;
        Grid.SetRow(grid, 1); root.Children.Add(grid);

        var footer = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245)),
            Padding = new Thickness(10,6,10,6) };
        var btnSel = new Button { Content = "Seleccionar", Height = 28, Padding = new Thickness(16,0,16,0),
            Background = BrPrimDark, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
        footer.Child = btnSel;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        async Task Seleccionar() {
            if (grid.SelectedItem == null) return;
            result = grid.SelectedItem;
            dlg.Close();
        }
        btnSel.Click += async (_, _) => await Seleccionar();
        grid.MouseDoubleClick += async (_, _) => await Seleccionar();

        dlg.Content = root;
        dlg.ShowDialog();
        return result;
    }

    private async Task InsertarArticuloEditar()
    {
        if (_idArtEditar == 0) { MessageBox.Show("Primero busque un artículo."); return; }
        if (!decimal.TryParse(_txtCant.Text.Replace(",","").Replace(".",""), out var cant) || cant <= 0)
            { MessageBox.Show("Ingrese una cantidad válida."); return; }

        var existe = _items.FirstOrDefault(i => i.IdArt == _idArtEditar && i.Identificador == 0);
        if (existe != null) {
            existe.Cantidad += cant;
        } else {
            // cargar precios actuales desde PRICES
            decimal pc = 0, pv = 0, contado = 0, ppromo = 0;
            try {
                using var conn = _db.Create();
                int loc = _sesion.LocalActual?.IdLocal ?? 1;
                var pr = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TOP 1 PC, PVENTA, CONTADO, PPROMO FROM PRICES WHERE IDART=@id AND IDLOCAL=@loc ORDER BY DELETADO ASC",
                    new { id = _idArtEditar, loc });
                if (pr != null) {
                    var d = (IDictionary<string,object>)pr;
                    decimal Safe(string k) { if (d.TryGetValue(k, out var v) && v != null) try { return Convert.ToDecimal(v); } catch { } return 0m; }
                    pc = Safe("PC"); pv = Safe("PVENTA"); contado = Safe("CONTADO"); ppromo = Safe("PPROMO");
                }
            } catch { }

            _items.Add(new LineaCompraEdit {
                IdArt = _idArtEditar, Codigo = _caEditar, Descripcion = _descEditar,
                Identificador = 0, Cantidad = cant,
                PrecioCosto = pc, PrecioVenta = pv, Contado = contado, PPromo = ppromo
            });
        }
        _idArtEditar = 0; _caEditar = ""; _descEditar = "";
        _txtBuscarArt.Text = ""; _txtCant.Text = "1";
        _txtBuscarArt.Focus();
        RecalcTotal();
    }

    // ── Buscador de comprobantes (modal flotante) ─────────────────────────
    private void AbrirBuscadorComprobante()
    {
        var dlg = new Window {
            Title = "Buscar comprobante", Width = 580, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrBlanco
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra naranja
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // textbox
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // Barra naranja
        var topBar = new Border { Background = BrPrimary, Padding = new Thickness(10,8,10,8) };
        topBar.Child = new TextBlock { Text = "Escriba un texto para realizar la búsqueda",
            FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrBlanco };
        Grid.SetRow(topBar, 0); root.Children.Add(topBar);

        // TextBox búsqueda
        var busqBox = new TextBox { Height = 28, FontSize = 12, Margin = new Thickness(8,6,8,4),
            Padding = new Thickness(6,2,6,2), VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1), Background = BrBlanco };
        Grid.SetRow(busqBox, 1); root.Children.Add(busqBox);

        // Grid resultados con foreground negro explícito
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            FontSize = 12, RowHeight = 26, ColumnHeaderHeight = 28,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229,231,235)),
            RowBackground = BrBlanco,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249,250,251)),
            Foreground = System.Windows.Media.Brushes.Black,
            BorderThickness = new Thickness(0),
            CellStyle = cellStyle,
            ColumnHeaderStyle = BuildGridHdrStyle()
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Comprobante interno",
            Binding = new System.Windows.Data.Binding("Interno"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Factura del proveedor",
            Binding = new System.Windows.Data.Binding("Factura"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",
            Binding = new System.Windows.Data.Binding("FechaFmt"), Width = 140 });
        Grid.SetRow(grid, 2); root.Children.Add(grid);

        // Footer
        var footBar = new Border { Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240,240,240)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(8,8,8,8) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnGuardar = new Button { Content = "Seleccionar", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        var btnCerrar2 = new Button { Content = "Cerrar", Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(8,0,0,0), Background = BrGris, Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar2.Click += (_, _) => dlg.Close();
        footSp.Children.Add(btnGuardar); footSp.Children.Add(btnCerrar2);
        footBar.Child = footSp;
        Grid.SetRow(footBar, 3); root.Children.Add(footBar);

        dlg.Content = root;

        async Task Cargar(string filtro) {
            try {
                using var conn = _db.Create();
                string sql = string.IsNullOrEmpty(filtro)
                    ? "SELECT TOP 100 INTERNO, FACTURA, FECHA FROM CAB_BUY_TMP ORDER BY IDCABTMP DESC"
                    : "SELECT TOP 100 INTERNO, FACTURA, FECHA FROM CAB_BUY_TMP WHERE INTERNO LIKE @f OR FACTURA LIKE @f ORDER BY IDCABTMP DESC";
                var rows = await conn.QueryAsync<dynamic>(sql, string.IsNullOrEmpty(filtro) ? null : new { f = "%" + filtro + "%" });
                grid.ItemsSource = rows.Select(r => new {
                    Interno  = (string)r.INTERNO,
                    Factura  = (string)r.FACTURA,
                    FechaFmt = r.FECHA is DateTime d ? d.ToString("dd/MM/yyyy HH:mm") : "—"
                }).ToList();
            } catch { }
        }

        busqBox.TextChanged += async (_, _) => await Cargar(busqBox.Text.Trim());
        dlg.Loaded += async (_, _) => { await Cargar(""); busqBox.Focus(); };

        async Task Seleccionar() {
            if (grid.SelectedItem == null) return;
            dynamic sel = grid.SelectedItem;
            dlg.Close();
            await CargarDetalleCompra((string)sel.Interno);
        }

        btnGuardar.Click         += async (_, _) => await Seleccionar();
        grid.MouseDoubleClick    += async (_, _) => await Seleccionar();

        dlg.ShowDialog();
    }

    private void AbrirBuscadorProveedor()
    {
        var modal = new BuscadorProveedorModal(_db) { Owner = this };
        if (modal.ShowDialog() == true && modal.ProveedorSeleccionado != null) {
            _idProv         = modal.ProveedorSeleccionado.IdProveedor;
            _nombreProv     = modal.ProveedorSeleccionado.Nombre;
            _txtIdProv.Text  = _idProv.ToString();
            _txtNomProv.Text = _nombreProv;
        }
    }

    private void AbrirBuscadorBanco()
    {
        var dlg = new Window {
            Title = "Buscar banco", Width = 480, Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrBlanco
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra naranja búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // Barra naranja con textbox de búsqueda
        var topBar = new Border { Background = BrPrimary, Padding = new Thickness(10,8,10,8) };
        var busqSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        busqSp.Children.Add(new TextBlock { Text = "Escriba un texto para realizar la búsqueda",
            FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrBlanco,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
        var txtB = new TextBox { Width = 220, Height = 28, FontSize = 12,
            Padding = new Thickness(6,2,6,2), BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = BrBlanco };
        busqSp.Children.Add(txtB);
        topBar.Child = busqSp;
        Grid.SetRow(topBar, 0); root.Children.Add(topBar);

        // Grid con fondo blanco explícito y foreground negro
        var gridBorder = new Border { Background = BrBlanco };
        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            FontSize = 12, RowHeight = 28, ColumnHeaderHeight = 28,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229,231,235)),
            RowBackground = BrBlanco,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249,250,251)),
            Foreground = System.Windows.Media.Brushes.Black,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = BuildGridHdrStyle()
        };

        // Estilo de celda con foreground negro explícito
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        grid.CellStyle = cellStyle;

        grid.Columns.Add(new DataGridTextColumn { Header = "ID",
            Binding = new System.Windows.Data.Binding("Id"), Width = 60 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Nombre del banco",
            Binding = new System.Windows.Data.Binding("Nombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        gridBorder.Child = grid;
        Grid.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // Footer con botones
        var footBar = new Border { Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240,240,240)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(8,8,8,8) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnSel = new Button { Content = "Seleccionar", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        var btnCnl = new Button { Content = "Cancelar", Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(8,0,0,0), Background = BrGris, Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCnl.Click += (_, _) => dlg.Close();
        footSp.Children.Add(btnSel); footSp.Children.Add(btnCnl);
        footBar.Child = footSp;
        Grid.SetRow(footBar, 2); root.Children.Add(footBar);

        dlg.Content = root;

        async Task Cargar(string filtro) {
            try {
                using var conn = _db.Create();
                var sql = string.IsNullOrEmpty(filtro)
                    ? "SELECT ID_BANCO as Id, BANCO as Nombre FROM BANCOS ORDER BY BANCO"
                    : "SELECT ID_BANCO as Id, BANCO as Nombre FROM BANCOS WHERE BANCO LIKE @f ORDER BY BANCO";
                var rows = await conn.QueryAsync<dynamic>(sql, string.IsNullOrEmpty(filtro) ? null : new { f = "%" + filtro + "%" });
                grid.ItemsSource = rows.Select(r => new { Id = (int)r.Id, Nombre = (string)r.Nombre }).ToList();
            } catch { }
        }

        void Seleccionar() {
            if (grid.SelectedItem == null) return;
            dynamic sel = grid.SelectedItem;
            _idBanco = (int)sel.Id;
            _txtIdBanco.Text  = _idBanco.ToString();
            _txtNomBanco.Text = (string)sel.Nombre;
            dlg.Close();
        }

        txtB.TextChanged         += async (_, _) => await Cargar(txtB.Text.Trim());
        btnSel.Click             += (_, _) => Seleccionar();
        grid.MouseDoubleClick    += (_, _) => Seleccionar();
        dlg.Loaded               += async (_, _) => { await Cargar(""); txtB.Focus(); };
        dlg.ShowDialog();
    }

    // ── Cargar detalle por INTERNO ────────────────────────────────────────
    private async Task CargarDetalleCompra(string interno)
    {
        try {
            using var conn = _db.Create();
            var msgOut = new DynamicParameters();
            msgOut.Add("@INTERNO", interno);
            msgOut.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("BUSCAR_BUYTMP_COMPRA_CS", msgOut,
                commandType: CommandType.StoredProcedure)).ToList();
            if (rows.Count == 0) return;

            var cabD = (IDictionary<string,object>)rows[0];
            decimal DCol(IDictionary<string,object> d, params string[] keys) {
                foreach (var k in keys) if (d.TryGetValue(k, out var v) && v != null) try { return Convert.ToDecimal(v); } catch { }
                return 0m;
            }
            string SCol(IDictionary<string,object> d, params string[] keys) {
                foreach (var k in keys) if (d.TryGetValue(k, out var v) && v != null) return v.ToString()!;
                return "";
            }
            int ICol(IDictionary<string,object> d, params string[] keys) {
                foreach (var k in keys) if (d.TryGetValue(k, out var v) && v != null) try { return Convert.ToInt32(v); } catch { }
                return 0;
            }

            _idCabTmp    = ICol(cabD, "IDCABTMP");
            _interno     = SCol(cabD, "INTERNO");
            _idProv      = ICol(cabD, "IDP");
            _nombreProv  = SCol(cabD, "PROVEEDOR");
            _idBanco     = ICol(cabD, "ID_BANCO");

            _txtInterno.Text  = _interno;
            _txtFactura.Text  = SCol(cabD, "FACTURA");
            _txtNota.Text     = SCol(cabD, "NOTA");
            _txtIdProv.Text   = _idProv.ToString();
            _txtNomProv.Text  = _nombreProv;
            _txtIdBanco.Text  = _idBanco.ToString();

            // Parcial y descuento del header
            _txtParcial.Text  = DCol(cabD, "PARCIAL").ToString("N0").Replace(",",".");
            _txtDescuento.Text = DCol(cabD, "DESCUENTO").ToString("N0").Replace(",",".");
            _txtTotal.Text    = DCol(cabD, "TOTAL").ToString("N0").Replace(",",".");

            // Leer nombre del banco
            try {
                var nomB = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT BANCO FROM BANCOS WHERE ID_BANCO=@id", new { id = _idBanco });
                _txtNomBanco.Text = nomB ?? "";
            } catch { }

            // Seleccionar método
            int metodo = ICol(cabD, "METODO");
            foreach (ComboBoxItem ci in _cboMetodo.Items)
                if (ci.Tag is byte b && b == metodo) { _cboMetodo.SelectedItem = ci; break; }

            _items.Clear();
            foreach (var rObj in rows) {
                var r = (IDictionary<string,object>)rObj;
                decimal pc      = DCol(r, "P. COSTO", "PRECIOCOSTO");
                decimal pv      = DCol(r, "P.VENTA",  "PVENTA");
                decimal contado = DCol(r, "CONTADO");
                decimal ppromo  = DCol(r, "PPROMO");
                decimal cant    = DCol(r, "CANTIDAD");
                decimal subtotal= DCol(r, "SUBTOTAL");
                if (subtotal == 0) subtotal = pc * cant;
                _items.Add(new LineaCompraEdit {
                    IdArt         = ICol(r, "ID"),
                    Codigo        = SCol(r, "CODIGO"),
                    Descripcion   = SCol(r, "DESCRIPCION"),
                    Identificador = ICol(r, "IDENTIFICADOR"),
                    Cantidad      = cant,
                    PrecioCosto   = pc,
                    PrecioVenta   = pv,
                    Contado       = contado,
                    PPromo        = ppromo,
                });
            }
            RecalcTotal();
            _btnGuardarMod.IsEnabled    = true;
            _btnGuardarCompra.IsEnabled = true;
        } catch (Exception ex) { MessageBox.Show($"Error cargando detalle: {ex.Message}"); }
    }

    // ── Doble click en ítem: editar ───────────────────────────────────────
    private void EditarItemDetalle()
    {
        if (_gridDetalle.SelectedItem is not LineaCompraEdit item) return;

        var dlg = new Window {
            Title = "Cambiar datos", Width = 460, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco
        };

        // header naranja con descripción del artículo
        var header = new Border { Background = BrPrimary, Padding = new Thickness(14,10,14,10) };
        header.Child = new TextBlock { Text = item.Descripcion, FontSize = 12, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, TextWrapping = TextWrapping.Wrap };

        // helper: formatea con puntos de miles al salir del campo
        static void AplicarMiles(TextBox tb) {
            if (decimal.TryParse(new string(tb.Text.Where(char.IsDigit).ToArray()), out var v))
                tb.Text = v.ToString("N0").Replace(",",".");
        }
        TextBox BT(decimal val) {
            var tb = new TextBox {
                Text = val.ToString("N0").Replace(",","."),
                Height = 30, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(6,0,6,0), VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = BrPrimary, BorderThickness = new Thickness(0,0,0,2),
                Background = BrFondoArt, TextAlignment = TextAlignment.Right, Width = 160
            };
            tb.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            tb.LostFocus        += (_, _) => AplicarMiles(tb);
            return tb;
        }
        decimal Parse(TextBox tb) {
            decimal.TryParse(new string(tb.Text.Where(char.IsDigit).ToArray()), out var v);
            return v;
        }

        var txtCant    = BT(item.Cantidad);
        var txtPC      = BT(item.PrecioCosto);
        var txtPV      = BT(item.PrecioVenta);
        var txtContado = BT(item.Contado);
        var txtPPromo  = BT(item.PPromo);

        // subtotal en tiempo real
        var lblSub = new TextBlock {
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrPrimDark,
            VerticalAlignment = VerticalAlignment.Center
        };
        void ActSub() {
            var cant = Parse(txtCant); var pc = Parse(txtPC);
            lblSub.Text = (cant * pc).ToString("N0").Replace(",",".") + " Gs.";
        }
        txtCant.TextChanged += (_, _) => ActSub();
        txtPC.TextChanged   += (_, _) => ActSub();
        ActSub();

        // grid de campos
        var body = new Grid { Margin = new Thickness(16,12,16,10) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 11; r++)
            body.RowDefinitions.Add(new RowDefinition { Height = r % 2 == 0 ? GridLength.Auto : new GridLength(6) });

        TextBlock BL(string t) => new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,12,0) };
        void GRC(UIElement el, int col, int row) { Grid.SetColumn(el, col); Grid.SetRow(el, row); body.Children.Add(el); }

        GRC(BL("Cantidad"),     0, 0);  GRC(txtCant,    1, 0);
        GRC(BL("Precio costo"), 0, 2);  GRC(txtPC,      1, 2);
        GRC(BL("Precio venta"), 0, 4);  GRC(txtPV,      1, 4);
        GRC(BL("P. contado"),   0, 6);  GRC(txtContado, 1, 6);
        GRC(BL("P. promo"),     0, 8);  GRC(txtPPromo,  1, 8);

        // fila subtotal
        var subRow = new StackPanel { Orientation = Orientation.Horizontal,
            Margin = new Thickness(0,10,0,0), VerticalAlignment = VerticalAlignment.Center };
        subRow.Children.Add(new TextBlock { Text = "SubTotal:", FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        subRow.Children.Add(lblSub);
        GRC(subRow, 0, 10); Grid.SetColumnSpan(subRow, 2);

        // botón Simulador
        var btnSim = new Button {
            Content = "Simulador", Height = 30, Padding = new Thickness(14,0,14,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59,130,246)),
            Foreground = BrBlanco, FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,4,0)
        };
        btnSim.Click += (_, _) => AbrirSimulador(item.Codigo, item.Descripcion, Parse(txtContado));

        var btnOk  = MakeBtn("Aplicar",  BrVerde);
        var btnCnl = MakeBtn("Cancelar", BrGris);
        btnCnl.Click += (_, _) => dlg.Close();
        btnOk.Click  += (_, _) => {
            item.Cantidad    = Parse(txtCant);
            item.PrecioCosto = Parse(txtPC);
            item.PrecioVenta = Parse(txtPV);
            item.Contado     = Parse(txtContado);
            item.PPromo      = Parse(txtPPromo);
            _gridDetalle.Items.Refresh(); RecalcTotal(); dlg.Close();
        };

        var ftrDlg = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Children.Add(btnSim); btnRow.Children.Add(btnOk); btnRow.Children.Add(btnCnl);
        ftrDlg.Child = btnRow;

        var stack = new StackPanel();
        stack.Children.Add(header); stack.Children.Add(body); stack.Children.Add(ftrDlg);
        dlg.Content = stack;
        dlg.Loaded += (_, _) => { txtCant.Focus(); txtCant.SelectAll(); };
        dlg.ShowDialog();
    }

    // ── Simulador de precios ──────────────────────────────────────────────
    private void AbrirSimulador(string codigo, string descripcion, decimal contado)
    {
        var dlg = new Window {
            Title = "Simulador de precios", Width = 320, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = BrBlanco
        };

        var header = new Border { Background = BrPrimary, Padding = new Thickness(12,8,12,8) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = $"Código: {codigo}", FontSize = 10,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200,255,255,255)) });
        hSp.Children.Add(new TextBlock { Text = descripcion, FontSize = 12, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, TextWrapping = TextWrapping.Wrap });
        header.Child = hSp;

        var body = new Grid { Margin = new Thickness(16,12,16,10) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        for (int r = 0; r < 18; r++)
            body.RowDefinitions.Add(new RowDefinition { Height = r % 2 == 0 ? GridLength.Auto : new GridLength(6) });

        void GR(UIElement el, int col, int row, int span = 1) {
            Grid.SetColumn(el, col); Grid.SetRow(el, row);
            if (span > 1) Grid.SetColumnSpan(el, span);
            body.Children.Add(el);
        }
        TextBlock Lbl(string t) => new TextBlock { Text = t, FontSize = 11, Foreground = BrLabel,
            VerticalAlignment = VerticalAlignment.Center };
        TextBox Inp(decimal v) {
            var tb = new TextBox { Text = v.ToString("N0").Replace(",","."),
                Height = 28, FontSize = 12, TextAlignment = TextAlignment.Right,
                Padding = new Thickness(6,0,6,0), VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = BrBorde, BorderThickness = new Thickness(1) };
            tb.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            return tb;
        }
        TextBlock Val(string v) => new TextBlock { Text = v, FontSize = 12, FontWeight = FontWeights.Bold,
            Foreground = BrPrimDark, TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        decimal ParseT(TextBox tb) { decimal.TryParse(new string(tb.Text.Where(char.IsDigit).ToArray()), out var v); return v; }

        var txtPrecio    = Inp(contado);
        var txtDescPct   = Inp(0);
        var lblDescMonto = Val("0");
        var lblValor     = Val(contado.ToString("N0").Replace(",","."));
        var txtEntrega   = Inp(contado / 2);
        var lblSaldo     = Val("0");
        var txtRecargo   = Inp(0);
        var txtCuotas    = Inp(12);
        var lblMensual   = Val("0");
        var lblFinal     = Val("0");

        void Recalcular() {
            var precio  = ParseT(txtPrecio);
            var descPct = ParseT(txtDescPct);
            var descM   = Math.Round(precio * descPct / 100);
            var valor   = precio - descM;
            var entrega = ParseT(txtEntrega);
            var saldo   = valor - entrega;
            var recargo = ParseT(txtRecargo);
            var cuotas  = ParseT(txtCuotas); if (cuotas <= 0) cuotas = 1;
            var mensual = cuotas > 0 ? Math.Round(saldo / cuotas * (1 + recargo/100)) : 0;
            var final   = entrega + mensual * cuotas;

            lblDescMonto.Text = descM  .ToString("N0").Replace(",",".");
            lblValor.Text     = valor  .ToString("N0").Replace(",",".");
            lblSaldo.Text     = saldo  .ToString("N0").Replace(",",".");
            lblMensual.Text   = mensual.ToString("N0").Replace(",",".");
            lblFinal.Text     = final  .ToString("N0").Replace(",",".");
        }

        foreach (var tb in new[] { txtPrecio, txtDescPct, txtEntrega, txtRecargo, txtCuotas })
            tb.TextChanged += (_, _) => Recalcular();

        GR(Lbl("Precio"),       0, 0);  GR(txtPrecio,    1, 0);
        GR(Lbl("% descuento"),  0, 2);  GR(txtDescPct,   1, 2);
        GR(Lbl("Descuento"),    0, 4);  GR(lblDescMonto, 1, 4);
        GR(Lbl("Valor"),        0, 6);  GR(lblValor,     1, 6);
        GR(Lbl("Entrega"),      0, 8);  GR(txtEntrega,   1, 8);
        GR(Lbl("Valor saldo"),  0,10);  GR(lblSaldo,     1,10);
        GR(Lbl("% Recargo"),    0,12);  GR(txtRecargo,   1,12);
        GR(Lbl("Cant/Cuotas"),  0,14);  GR(txtCuotas,    1,14);
        GR(Lbl("Valor mensual"),0,16);  GR(lblMensual,   1,16);

        // fila Valor Final destacada
        var finalBorder = new Border { Background = BrFondoArt, CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10,6,10,6), Margin = new Thickness(0,10,0,0) };
        var finalSp = new StackPanel { Orientation = Orientation.Horizontal };
        finalSp.Children.Add(new TextBlock { Text = "Valor Final", FontSize = 12,
            FontWeight = FontWeights.Bold, Foreground = BrLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 100 });
        lblFinal.FontSize = 14;
        finalSp.Children.Add(lblFinal);
        finalBorder.Child = finalSp;

        var finalRow = new Border { Margin = new Thickness(16,0,16,0) };
        finalRow.Child = finalBorder;

        var ftrDlg = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var btnCerrar = MakeBtn("Cerrar", BrGris);
        btnCerrar.Click += (_, _) => dlg.Close();
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnCerrar);
        ftrDlg.Child = ftrSp;

        var stack = new StackPanel();
        stack.Children.Add(header); stack.Children.Add(body);
        stack.Children.Add(finalRow); stack.Children.Add(ftrDlg);
        dlg.Content = stack;
        Recalcular();
        dlg.ShowDialog();
    }

    // ── Guardar modificación (JAEDITA_BUY_TMP_CS) ────────────────────────
    private async Task GuardarModificacion()
    {
        if (_idCabTmp == 0) { MessageBox.Show("Seleccione una compra primero."); return; }
        if (_idProv   == 0) { MessageBox.Show("Seleccione un proveedor.");       return; }

        var usuarioAutorizado = await MostrarPermisoUsuario(this);
        if (usuarioAutorizado == null) return;

        byte metodo = ((_cboMetodo.SelectedItem as ComboBoxItem)?.Tag is byte b) ? b : (byte)1;
        decimal.TryParse(new string((_txtDescuento.Text ?? "0").Where(char.IsDigit).ToArray()), out var descuento);
        var parcial    = _items.Sum(i => i.Subtotal);
        var totalFinal = parcial - descuento;
        if (totalFinal < 0) totalFinal = 0;
        var factura = _txtFactura.Text.Trim();
        var nota    = _txtNota.Text.Trim();
        var idu     = _sesion.UsuarioActual?.IdUsuario ?? 1;

        _btnGuardarMod.IsEnabled = false;
        try {
            using var conn = _db.Create();
            for (int i = 0; i < _items.Count; i++) {
                var item = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",       i == 0 ? "SI" : "NO");
                p.Add("@IDCABTMP",     _idCabTmp);
                p.Add("@INTERNO",      _interno);
                p.Add("@FACTURA",      factura);
                p.Add("@PARCIAL",      parcial);
                p.Add("@DESCUENTO",    descuento);
                p.Add("@TOTAL",        totalFinal);
                p.Add("@FORMA",        (byte)1);
                p.Add("@METODO",       metodo);
                p.Add("@ID_BANCO",     _idBanco);
                p.Add("@IDP",          _idProv);
                p.Add("@IDU",          idu);
                p.Add("@STATUS",       (byte)1);
                p.Add("@NOTA",         nota);
                p.Add("@IDART",        item.IdArt);
                p.Add("@CA",           item.Codigo);
                p.Add("@D",            item.Descripcion);
                p.Add("@CANT",         item.Cantidad);
                p.Add("@PC",           item.PrecioCosto);
                p.Add("@PVENTA",       item.PrecioVenta);
                p.Add("@CONTADO",      item.Contado);
                p.Add("@PPROMO",       item.PPromo);
                p.Add("@IDENTIFICADOR",item.Identificador);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 30);
                await conn.ExecuteAsync("JAEDITA_BUY_TMP_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") { MessageBox.Show($"Error en ítem {i+1}: {msg}"); return; }
            }
            MessageBox.Show("Modificación guardada correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        finally { _btnGuardarMod.IsEnabled = true; }
    }

    // ── Modal permiso de usuario (igual al de NuevaCompraWindow) ─────────
    private record UsuarioPermiso(int IdUsuario, string Nombre);

    private async Task<UsuarioPermiso?> MostrarPermisoUsuario(Window owner)
    {
        UsuarioPermiso? resultado = null;
        List<dynamic> usuarios = new();
        try {
            using var conn = _db.Create();
            usuarios = (await conn.QueryAsync<dynamic>(
                "SELECT ID_USUARIO, NOMBRE_USUARIO, CODIGO_USUARIO, CONTRASEÑA_USUARIO FROM USUARIOS ORDER BY NOMBRE_USUARIO"))
                .ToList();
        } catch (Exception ex) { MessageBox.Show($"Error cargando usuarios: {ex.Message}"); return null; }

        var W   = System.Windows.Media.Brushes.White;
        var BrN = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,140,0));
        var BrD = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224,112,0));
        var BrG = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128));
        var BrV = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22,163,74));
        var BrF = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249,250,251));
        var BrB = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209,213,219));
        var BrT = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81));

        var dlgPerm = new Window {
            Title = "Autorización requerida", Width = 400, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner,
            ResizeMode = ResizeMode.NoResize, Background = W
        };

        var header = new Border { Background = BrN, Padding = new Thickness(20,16,20,16) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock { Text = "🔐", FontSize = 22,
            Foreground = W, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,12,0) });
        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock { Text = "PERMISO DE USUARIOS",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = W });
        headerText.Children.Add(new TextBlock { Text = "Ingrese sus credenciales para confirmar",
            FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(200,255,255,255)) });
        headerSp.Children.Add(headerText);
        header.Child = headerSp;

        var body = new StackPanel { Margin = new Thickness(24,20,24,8) };
        TextBlock FL(string t) => new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrT, Margin = new Thickness(0,0,0,4) };
        TextBox FT() => new TextBox { Height = 36, FontSize = 13, Padding = new Thickness(10,0,10,0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = BrB, BorderThickness = new Thickness(1), Background = BrF };
        PasswordBox FP() => new PasswordBox { Height = 36, FontSize = 13, Padding = new Thickness(10,0,10,0),
            BorderBrush = BrB, BorderThickness = new Thickness(1), Background = BrF };

        var cboUsuario = new ComboBox { Height = 36, FontSize = 13, Margin = new Thickness(0,0,0,14),
            BorderBrush = BrB, BorderThickness = new Thickness(1), Background = W };
        foreach (dynamic u in usuarios)
            cboUsuario.Items.Add(new ComboBoxItem { Content = (string)u.NOMBRE_USUARIO, Tag = u });
        cboUsuario.SelectedIndex = 0;

        var txtCodigo   = FT(); txtCodigo.Margin   = new Thickness(0,0,0,14);
        var txtPassword = FP(); txtPassword.Margin = new Thickness(0,0,0,6);
        body.Children.Add(FL("Usuario"));    body.Children.Add(cboUsuario);
        body.Children.Add(FL("Código"));     body.Children.Add(txtCodigo);
        body.Children.Add(FL("Contraseña")); body.Children.Add(txtPassword);

        var footerBdr = new Border { Background = BrF, BorderBrush = BrB,
            BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(20,12,20,12),
            Margin = new Thickness(0,14,0,0) };
        var btnAceptar = new Button { Content = "✔  Aceptar", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = BrV, Foreground = W, FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        var btnCancelar = new Button { Content = "Cancelar", Height = 36, Padding = new Thickness(16,0,16,0),
            Background = BrG, Foreground = W, FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(8,0,0,0) };
        var footBtns = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        footBtns.Children.Add(btnAceptar); footBtns.Children.Add(btnCancelar);
        footerBdr.Child = footBtns;

        Action confirmar = () => {
            if (cboUsuario.SelectedItem is not ComboBoxItem ci) { MessageBox.Show("Seleccione un usuario."); return; }
            dynamic u = ci.Tag;
            if (txtCodigo.Text.Trim()       != u.CODIGO_USUARIO.ToString())     { MessageBox.Show("Código incorrecto.",    "Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtCodigo.Focus(); txtCodigo.SelectAll(); return; }
            if (txtPassword.Password.Trim() != u.CONTRASEÑA_USUARIO.ToString()) { MessageBox.Show("Contraseña incorrecta.","Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtPassword.Focus(); txtPassword.SelectAll(); return; }
            resultado = new UsuarioPermiso((int)u.ID_USUARIO, (string)u.NOMBRE_USUARIO);
            dlgPerm.DialogResult = true;
            dlgPerm.Close();
        };
        btnAceptar.Click   += (_, _) => confirmar();
        btnCancelar.Click  += (_, _) => dlgPerm.Close();
        txtPassword.KeyDown += (_, e) => { if (e.Key == Key.Enter) confirmar(); };
        txtCodigo.KeyDown   += (_, e) => { if (e.Key == Key.Enter) txtPassword.Focus(); };

        var rootSp = new StackPanel();
        rootSp.Children.Add(header); rootSp.Children.Add(body); rootSp.Children.Add(footerBdr);
        dlgPerm.Content = rootSp;
        dlgPerm.Loaded += (_, _) => txtCodigo.Focus();
        dlgPerm.ShowDialog();
        return resultado;
    }

    // ── Confirmar compra: TMP → definitivo (JOGUAANETE_CS) ───────────────
    private async Task GuardarCompra()
    {
        if (_idCabTmp == 0) { MessageBox.Show("Seleccione una compra primero."); return; }
        if (_idProv   == 0) { MessageBox.Show("Seleccione un proveedor.");       return; }
        if (_items.Count == 0) { MessageBox.Show("No hay artículos en el detalle."); return; }

        // ── Validación de usuario ─────────────────────────────────────────
        var usuarioAutorizado = await MostrarPermisoUsuario(this);
        if (usuarioAutorizado == null) return;

        byte metodo = ((_cboMetodo.SelectedItem as ComboBoxItem)?.Tag is byte b) ? b : (byte)1;
        decimal.TryParse(new string((_txtDescuento.Text ?? "0").Where(char.IsDigit).ToArray()), out var descuento);
        var parcial    = _items.Sum(i => i.Subtotal);
        var totalFinal = parcial - descuento;
        if (totalFinal < 0) totalFinal = 0;
        var factura    = _txtFactura.Text.Trim();
        var nota       = _txtNota.Text.Trim();
        var idu        = _sesion.UsuarioActual?.IdUsuario ?? 1;
        var idlocal    = (byte)(_sesion.LocalActual?.IdLocal ?? 1);

        _btnGuardarCompra.IsEnabled = false;
        _btnGuardarMod.IsEnabled    = false;
        try {
            using var conn = _db.Create();

            for (int i = 0; i < _items.Count; i++) {
                var item = _items[i];

                // obtener IDPRICES del artículo en este local (sin filtrar DELETADO — puede estar marcado pero aún válido)
                var idPrices = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT TOP 1 IDPRICES FROM PRICES WHERE IDART=@id AND IDLOCAL=@loc ORDER BY DELETADO ASC",
                    new { id = item.IdArt, loc = idlocal }) ?? 0;

                var p = new DynamicParameters();
                p.Add("@AGENTE",       i + 1);
                p.Add("@ultimo",       _items.Count);
                p.Add("@IDCABVIEJO",   _idCabTmp);
                p.Add("@IDCABBUYS",    0);
                p.Add("@COMPROBANTE",  _interno);
                p.Add("@FACTURA",      factura);
                p.Add("@PARCIAL",      "0");
                p.Add("@PUNITORIO",    (decimal)0);
                p.Add("@DESCUENTO",    descuento);
                p.Add("@SUBTOTAL",     parcial);
                p.Add("@HABER",        (decimal)0);
                p.Add("@TOTALFINAL",   totalFinal);
                p.Add("@FORMA",        (byte)1);
                p.Add("@METODO",       metodo);
                p.Add("@ID_BANCO",     _idBanco > 0 ? _idBanco : 1);
                p.Add("@IDP",          _idProv);
                p.Add("@IDU",          idu);
                p.Add("@ESTADO",       (byte)1);
                p.Add("@ID_LOCAL",     idlocal);
                p.Add("@NOTA",         nota);
                p.Add("@IDDETBUYS",    0);
                p.Add("@IDENTIFICADOR", item.Identificador);
                p.Add("@IDART",        item.IdArt);
                p.Add("@PC",           item.PrecioCosto);
                p.Add("@CANTIDAD",     item.Cantidad);
                p.Add("@IDPRICES",     idPrices);
                p.Add("@PVENTA",       item.PrecioVenta);
                p.Add("@CONTADO",      item.Contado);
                p.Add("@PPROMO",       item.PPromo);
                p.Add("@IDMOVART",     0);
                p.Add("@MOV",          (byte)4);
                p.Add("@MOD",          (byte)1);
                p.Add("@stini",        (decimal)0);
                p.Add("@IDLOCAL",      idlocal);
                p.Add("@IDDESTINO",    idlocal);
                p.Add("@pcant",        (decimal)0);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
                await conn.ExecuteAsync("JOGUAANETE_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") {
                    MessageBox.Show($"Error en ítem {i+1} ({item.Codigo}): {msg}");
                    return;
                }
            }

            MessageBox.Show("Compra confirmada correctamente. Precios y stock actualizados.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // limpiar para que no se pueda re-guardar
            _idCabTmp = 0; _interno = "";
            _items.Clear();
            _txtInterno.Text = ""; _txtFactura.Text = ""; _txtNota.Text = "";
            _txtParcial.Text = "0"; _txtDescuento.Text = ""; _txtTotal.Text = "0";
            _txtIdBanco.Text = "1"; _txtNomBanco.Text = "";
            _txtIdProv.Text  = "";  _txtNomProv.Text  = "";
            _btnGuardarMod.IsEnabled    = false;
            _btnGuardarCompra.IsEnabled = false;
            RecalcTotal();
        } catch (Exception ex) {
            MessageBox.Show($"Error al confirmar compra: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        } finally {
            if (_idCabTmp != 0) {
                _btnGuardarCompra.IsEnabled = true;
                _btnGuardarMod.IsEnabled    = true;
            }
        }
    }
}
