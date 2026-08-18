using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Transferencias;

// ══════════════════════════════════════════════════════════════════════════════
//  Modelos locales
// ══════════════════════════════════════════════════════════════════════════════

internal class LineaRemito
{
    public int     IdArt    { get; set; }
    public string  Codigo   { get; set; } = string.Empty;
    public string  Nombre   { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Pcosto   { get; set; }
    public decimal Pcontado { get; set; }
    public decimal SubCosto   => Cantidad * Pcosto;
    public decimal Subtotal   => Cantidad * Pcontado;
    // Solo se completa en AceptarTransferenciaWindow: stock actual del artículo en el local
    // destino ANTES de aceptar la transferencia, para que el usuario pueda validarlo.
    public decimal? StockActualDestino { get; set; }
}

internal class CabRemito
{
    public int      IdRemitoTmp   { get; set; }
    public string   NumeroRemTmp  { get; set; } = string.Empty;
    public byte     IdOrigenTmp   { get; set; }
    public byte     IdDestinoTmp  { get; set; }
    public byte     Estado        { get; set; }
    public DateTime Fecha         { get; set; }
    public decimal  TotalCosto    { get; set; }
    public decimal  TotalContado  { get; set; }
    public int      Idu           { get; set; }
    public string   LocalOrigen   { get; set; } = string.Empty;
    public string   LocalDestino  { get; set; } = string.Empty;
}

internal class ArticuloParaRemito
{
    public int    Id  { get; set; }
    public string Ca  { get; set; } = string.Empty;
    public string D   { get; set; } = string.Empty;
    public decimal S  { get; set; }
    public decimal Pc { get; set; }
    public decimal Pv { get; set; }
    public string LocalUbicacion { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════════════════════════
//  NUEVA TRANSFERENCIA
// ══════════════════════════════════════════════════════════════════════════════

public class NuevaTransferenciaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly IArticuloRepository  _artRepo;

    private ComboBox  _cboOrigen = null!, _cboDestino = null!;
    private TextBox   _txtCodigo = null!, _txtDescripcion = null!,
                      _txtCantidad = null!, _txtPcosto = null!, _txtPventa = null!,
                      _txtNota = null!;
    private DataGrid  _gridDetalle = null!;
    private TextBlock _lblTotal = null!, _lblSubCosto = null!, _lblSubVenta = null!;
    private Button    _btnInsertar = null!, _btnConfirmar = null!;

    private List<ArticuloParaRemito> _todosArticulos = new();
    private ArticuloParaRemito? _artSeleccionado;
    private readonly List<LineaRemito> _items = new();
    private List<Local> _locales = new();

    // Colores corporativos azul
    private static readonly System.Windows.Media.SolidColorBrush BrPrim  = new(System.Windows.Media.Color.FromRgb( 21,101,192));
    private static readonly System.Windows.Media.SolidColorBrush BrDark  = new(System.Windows.Media.Color.FromRgb( 14, 47, 68));
    private static readonly System.Windows.Media.SolidColorBrush BrFondo = new(System.Windows.Media.Color.FromRgb(238,244,251));
    private static readonly System.Windows.Media.SolidColorBrush BrCard  = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrBorde = new(System.Windows.Media.Color.FromRgb(187,222,251));
    private static readonly System.Windows.Media.SolidColorBrush BrAlt   = new(System.Windows.Media.Color.FromRgb(232,240,254));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel = new(System.Windows.Media.Color.FromRgb(80,80,80));

    public NuevaTransferenciaWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _artRepo = App.Services.GetRequiredService<IArticuloRepository>();
        Title    = "Transferencia entre Locales";
        Width    = 1100; Height = 680; MinWidth = 900; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await CargarLocalesAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra origen/destino
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra campos artículo
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // totales
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Fila 0: Header ─────────────────────────────────────────────────
        var hdr = new Border { Background = BrPrim, Padding = new Thickness(16,10,16,10) };
        hdr.Child = new TextBlock {
            Text = "📦  Transferencia entre Locales",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold
        };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Fila 1: Origen / Destino / Nota ───────────────────────────────
        var barOD = new Border {
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(12,8,12,8)
        };
        var odSp = new StackPanel { Orientation = Orientation.Horizontal };

        // IsEditable + IsTextSearchEnabled: el equipo se maneja más por código de local que
        // por nombre (pedido explícito 2026-08-10) — con esto pueden escribir el código
        // (ej. "3") y el combo salta directo al ítem que empieza así, en vez de tener que
        // buscar el nombre en la lista desplegada.
        odSp.Children.Add(MkFieldLbl("Origen"));
        _cboOrigen = new ComboBox {
            Height = 28, MinWidth = 200, Margin = new Thickness(4,0,16,0), Padding = new Thickness(4,0,4,0),
            IsEditable = true, IsTextSearchEnabled = true, StaysOpenOnEdit = true
        };
        odSp.Children.Add(_cboOrigen);

        odSp.Children.Add(new TextBlock { Text = "→", FontSize = 16, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,16,0),
            Foreground = BrPrim });

        odSp.Children.Add(MkFieldLbl("Destino"));
        _cboDestino = new ComboBox {
            Height = 28, MinWidth = 200, Margin = new Thickness(4,0,24,0), Padding = new Thickness(4,0,4,0),
            IsEditable = true, IsTextSearchEnabled = true, StaysOpenOnEdit = true
        };
        odSp.Children.Add(_cboDestino);

        odSp.Children.Add(MkFieldLbl("Nota"));
        _txtNota = new TextBox { Height = 28, MinWidth = 260, Margin = new Thickness(4,0,0,0),
            Padding = new Thickness(6,2,6,2), BorderBrush = BrBorde, BorderThickness = new Thickness(1) };
        odSp.Children.Add(_txtNota);

        barOD.Child = odSp;
        Grid.SetRow(barOD, 1); root.Children.Add(barOD);

        // ── Fila 2: Campos artículo — 2 subfijas ────────────────────────────────
        var barArt = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,248,238)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(12,6,12,6)
        };
        var artSp = new StackPanel { Orientation = Orientation.Vertical };

        // ── Sub-fila 1: Código + F1 + Descripción ────────────────────────────
        var fila1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,4) };

        // Código + botón F1
        fila1.Children.Add(MkFieldLbl("Código"));
        var codRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4,0,12,0) };
        _txtCodigo = new TextBox {
            Width = 140, Height = 28, Padding = new Thickness(6,2,6,2),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12
        };
        _txtCodigo.KeyDown += OnCodigoKeyDown;
        var btnF1 = new Button {
            Content = "🔍 F1", Height = 28, Padding = new Thickness(8,0,8,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,100,200)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(2,0,0,0)
        };
        btnF1.Click += OnAbrirBuscador;
        codRow.Children.Add(_txtCodigo);
        codRow.Children.Add(btnF1);
        fila1.Children.Add(codRow);

        fila1.Children.Add(MkFieldLbl("Descripción"));
        _txtDescripcion = new TextBox {
            Width = 420, Height = 28, Padding = new Thickness(6,2,6,2),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Margin = new Thickness(4,0,0,0), IsReadOnly = true,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245))
        };
        fila1.Children.Add(_txtDescripcion);
        artSp.Children.Add(fila1);

        // ── Sub-fila 2: Cantidad + P.Costo + P.Venta + Insertar ──────────────
        var fila2 = new StackPanel { Orientation = Orientation.Horizontal };

        fila2.Children.Add(MkFieldLbl("Cantidad"));
        _txtCantidad = new TextBox {
            Width = 80, Height = 28, Text = "1", Padding = new Thickness(6,2,6,2),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Margin = new Thickness(4,0,16,0), TextAlignment = TextAlignment.Right
        };
        fila2.Children.Add(_txtCantidad);

        fila2.Children.Add(MkFieldLbl("Precio costo"));
        _txtPcosto = new TextBox {
            Width = 110, Height = 28, Padding = new Thickness(6,2,6,2),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Margin = new Thickness(4,0,16,0), IsReadOnly = true, TextAlignment = TextAlignment.Right,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245))
        };
        fila2.Children.Add(_txtPcosto);

        fila2.Children.Add(MkFieldLbl("Precio venta"));
        _txtPventa = new TextBox {
            Width = 110, Height = 28, Padding = new Thickness(6,2,6,2),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Margin = new Thickness(4,0,16,0), IsReadOnly = true, TextAlignment = TextAlignment.Right,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245))
        };
        fila2.Children.Add(_txtPventa);

        _btnInsertar = new Button {
            Content = "💾  Insertar (F7)", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,130,60)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0,0,0,0)
        };
        _btnInsertar.Click += OnAgregar;
        fila2.Children.Add(_btnInsertar);
        artSp.Children.Add(fila2);

        barArt.Child = artSp;
        Grid.SetRow(barArt, 2); root.Children.Add(barArt);

        // ── Fila 3: Grid de detalle ─────────────────────────────────────────
        var gridWrap = new Border {
            Margin = new Thickness(10,6,10,0),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true
        };
        gridWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 4, ShadowDepth = 1, Opacity = 0.06,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 45, ColumnHeaderHeight = 32, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrBorde,
            RowBackground = BrCard, AlternatingRowBackground = BrAlt,
            BorderThickness = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, BrPrim));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.0));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,0,10,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, BrDark));
        _gridDetalle.ColumnHeaderStyle = hs;

        var txL = new Style(typeof(TextBlock));
        txL.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,6,0)));
        txL.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        var txR = new Style(typeof(TextBlock));
        txR.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6,0,10,0)));
        txR.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txR.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        txR.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas")));

        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",    Binding = new System.Windows.Data.Binding("Codigo"),                             Width = 110, ElementStyle = txL });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción",Binding = new System.Windows.Data.Binding("Nombre"),                            Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",      Binding = new System.Windows.Data.Binding("Cantidad") { StringFormat = "N0" },  Width = 70,  ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. costo",   Binding = new System.Windows.Data.Binding("Pcosto")   { StringFormat = "N0" },  Width = 90,  ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. venta",   Binding = new System.Windows.Data.Binding("Pcontado") { StringFormat = "N0" },  Width = 90,  ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Sub costo",  Binding = new System.Windows.Data.Binding("SubCosto") { StringFormat = "N0" },  Width = 100, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Sub venta",  Binding = new System.Windows.Data.Binding("Subtotal") { StringFormat = "N0" },  Width = 100, ElementStyle = txR });

        var btnQuitarCol = new DataGridTemplateColumn { Header = "", Width = 110, CanUserSort = false, CanUserResize = false };
        var cellTemplate = new DataTemplate();
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "🗑 Quitar");
        btnFactory.SetValue(Button.HeightProperty, 28.0);
        btnFactory.SetValue(Button.PaddingProperty, new Thickness(8, 0, 8, 0));
        btnFactory.SetValue(Button.FontSizeProperty, 11.0);
        btnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
        btnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        btnFactory.SetValue(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)));
        btnFactory.SetValue(Button.ForegroundProperty, System.Windows.Media.Brushes.White);
        btnFactory.SetValue(Button.MarginProperty, new Thickness(4, 0, 4, 0));
        btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) => {
            if (((FrameworkElement)s).DataContext is LineaRemito l) { _items.Remove(l); RefrescarGrid(); }
        }));
        cellTemplate.VisualTree = btnFactory;
        btnQuitarCol.CellTemplate = cellTemplate;
        _gridDetalle.Columns.Add(btnQuitarCol);

        _gridDetalle.KeyDown += (_, e) => { if (e.Key == Key.Delete || e.Key == Key.Back) OnQuitar(null!, null!); };
        gridWrap.Child = _gridDetalle;
        Grid.SetRow(gridWrap, 3); root.Children.Add(gridWrap);

        // ── Fila 4: Totales ─────────────────────────────────────────────────
        var totBorder = new Border {
            Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,1),
            Padding = new Thickness(12,6,12,6), Margin = new Thickness(0,4,0,0)
        };
        var totSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        _lblSubCosto = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0,0,24,0) };
        _lblSubVenta = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0,0,24,0) };
        _lblTotal = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = BrPrim };
        totSp.Children.Add(_lblSubCosto);
        totSp.Children.Add(_lblSubVenta);
        totSp.Children.Add(_lblTotal);
        totBorder.Child = totSp;
        Grid.SetRow(totBorder, 4); root.Children.Add(totBorder);

        // ── Fila 5: Footer / Botones ───────────────────────────────────────
        var footer = new Border {
            Background = BrCard, Padding = new Thickness(12,8,12,8),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0)
        };
        var footerDp = new DockPanel();

        var leftBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var btnQuitar = MkBtn("🗑  Quitar (Supr)", "#E74C3C"); btnQuitar.Click += OnQuitar;
        leftBtns.Children.Add(btnQuitar);
        DockPanel.SetDock(leftBtns, Dock.Left);
        footerDp.Children.Add(leftBtns);

        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _btnConfirmar = MkBtn("✔  Guardar (F5)", "#22C55E"); _btnConfirmar.Click += OnConfirmar; _btnConfirmar.IsEnabled = false;
        var btnImprimir = MkBtn("🖨  Imprimir", "#2196F3"); btnImprimir.Click += (_, _) => MessageBox.Show("Función de impresión próximamente.", "Imprimir");
        var btnCerrar   = MkBtn("✕  Cerrar", "#6B7280");  btnCerrar.Click += (_, _) => Close();
        rightBtns.Children.Add(_btnConfirmar);
        rightBtns.Children.Add(btnImprimir);
        rightBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(rightBtns, Dock.Right);
        footerDp.Children.Add(rightBtns);

        footer.Child = footerDp;
        Grid.SetRow(footer, 5); root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) => _txtCodigo.Focus();

        // Hotkeys
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F1) OnAbrirBuscador(null!, null!);
            if (e.Key == Key.F7) OnAgregar(null!, null!);
            if (e.Key == Key.F5 && _btnConfirmar.IsEnabled) OnConfirmar(null!, null!);
        };
    }

    private static TextBlock MkFieldLbl(string t) => new() {
        Text = t, FontSize = 11, Foreground = BrLabel,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,2,0)
    };

    private static Button MkBtn(string txt, string hex) => new Button {
        Content = txt, Height = 32, Padding = new Thickness(14,0,14,0),
        Margin = new Thickness(6,0,0,0),
        Background = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
        FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
    };

    // "Código — Nombre" (ej. "3 — Fassardi"): el equipo se maneja más por código de local
    // que por nombre — mostrarlo al frente hace que IsTextSearchEnabled salte al ítem
    // correcto con solo escribir el número, y de paso queda más claro cuál local es cuál.
    private static string TextoLocal(Local l) => $"{l.Codigo} — {l.NombreLocal}";

    private async Task CargarLocalesAsync()
    {
        using var conn = _db.Create();
        _locales = (await conn.QueryAsync<Local>(
            "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as NombreLocal FROM LOCALES ORDER BY NOMBRE")).ToList();

        var sesion = SessionService.Instance;
        var local  = sesion.LocalActual;

        foreach (var l in _locales)
            _cboDestino.Items.Add(new ComboBoxItem { Content = TextoLocal(l), Tag = l.IdLocal });
        if (_cboDestino.Items.Count > 0) _cboDestino.SelectedIndex = 0;

        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) puede elegir de qué local sale la mercadería.
        // Un vendedor normal solo puede transferir stock de SU propio local — el combo
        // Origen queda fijo y deshabilitado, sin la lista completa. El Destino sigue libre
        // para cualquier usuario: cualquiera puede decidir a qué sucursal enviar.
        var puedeVerTodos = sesion.UsuarioActual?.PuedeVerTodosLosLocales == true;
        if (puedeVerTodos)
        {
            foreach (var l in _locales)
                _cboOrigen.Items.Add(new ComboBoxItem { Content = TextoLocal(l), Tag = l.IdLocal });
            for (int i = 0; i < _cboOrigen.Items.Count; i++)
            {
                if (((ComboBoxItem)_cboOrigen.Items[i]).Tag is int id && id == local?.IdLocal)
                { _cboOrigen.SelectedIndex = i; break; }
            }
            _cboOrigen.IsEnabled = true;
        }
        else
        {
            var localSesion = _locales.FirstOrDefault(l => l.IdLocal == local?.IdLocal);
            _cboOrigen.Items.Add(new ComboBoxItem {
                Content = localSesion != null ? TextoLocal(localSesion) : (local?.NombreLocal ?? "Mi local"),
                Tag = local?.IdLocal ?? 0
            });
            _cboOrigen.SelectedIndex = 0;
            _cboOrigen.IsEnabled = false;
        }

        // Cargar artículos del local origen al iniciar
        await CargarArticulosOrigenAsync();
        _cboOrigen.SelectionChanged += async (_, _) => await CargarArticulosOrigenAsync();
    }

    private async Task CargarArticulosOrigenAsync()
    {
        var origenLocal = GetLocalId(_cboOrigen);
        if (origenLocal <= 0) return;
        try
        {
            using var conn = _db.Create();
            var subLocal = @"ISNULL((SELECT TOP 1 l2.NOMBRE FROM LOCALES l2 WHERE l2.ID_LOCAL = @L), '') as LOCAL_UBI";
            var rows = await conn.QueryAsync<dynamic>(
                $@"SELECT a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D,
                          ISNULL(p.S,0) as S, ISNULL(p.PC,0) as PC, ISNULL(p.PVENTA,0) as PV,
                          {subLocal}
                   FROM ARTICULOS a
                   INNER JOIN PRICES p ON p.IDART = a.ID AND p.IDLOCAL = @L AND p.DELETADO = 0 AND p.S > 0
                   WHERE a.ES = 1
                   ORDER BY a.D",
                new { L = origenLocal });

            _todosArticulos = rows.Select(r => new ArticuloParaRemito {
                Id             = (int)r.IDART,
                Ca             = r.CA is double d ? ((long)d).ToString() : r.CA?.ToString() ?? "",
                D              = (string)r.D,
                S              = (decimal)r.S,
                Pc             = (decimal)r.PC,
                Pv             = (decimal)r.PV,
                LocalUbicacion = r.LOCAL_UBI?.ToString() ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando artículos:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            _todosArticulos = new();
        }
    }

    private void OnCodigoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            var codigo = _txtCodigo.Text.Trim();
            var art = _todosArticulos.FirstOrDefault(a =>
                a.Ca.Equals(codigo, StringComparison.OrdinalIgnoreCase));
            if (art != null) CargarCamposArticulo(art);
            else if (!string.IsNullOrEmpty(codigo))
                MessageBox.Show($"Código '{codigo}' no encontrado.\nUse F1 para buscar.", "Artículo",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAbrirBuscador(object sender, RoutedEventArgs e)
    {
        var win = new BuscadorTransferenciaArticulo(_todosArticulos) { Owner = this };
        if (win.ShowDialog() == true && win.Seleccionado != null)
            CargarCamposArticulo(win.Seleccionado);
    }

    private void CargarCamposArticulo(ArticuloParaRemito art)
    {
        _artSeleccionado     = art;
        _txtCodigo.Text      = art.Ca;
        _txtDescripcion.Text = art.D;
        _txtPcosto.Text      = art.Pc.ToString("N0");
        _txtPventa.Text      = art.Pv.ToString("N0");
        _txtCantidad.SelectAll();
        _txtCantidad.Focus();
    }

    private void OnAgregar(object sender, RoutedEventArgs e)
    {
        if (_artSeleccionado == null) { MessageBox.Show("Seleccione un artículo (F1)."); return; }
        if (!decimal.TryParse(_txtCantidad.Text.Replace(",","").Replace(".",""), out var cant) || cant <= 0)
        {
            new CantidadInvalidaDialog(_artSeleccionado.Ca, _artSeleccionado.D, _txtCantidad.Text, _artSeleccionado.S)
                { Owner = this }.ShowDialog();
            return;
        }

        var art = _artSeleccionado;
        var existente = _items.FirstOrDefault(x => x.IdArt == art.Id);

        // El stock del artículo (art.S) es el disponible en el local origen al momento de
        // cargar el buscador. Si ya hay una línea de este artículo en el remito, la cantidad
        // acumulada (existente + nueva) tampoco puede superar ese stock — sumar en dos
        // pasadas no debe permitir transferir más de lo que realmente hay.
        var cantidadYaEnRemito = existente?.Cantidad ?? 0m;
        if (cantidadYaEnRemito + cant > art.S)
        {
            MessageBox.Show(
                $"No hay stock suficiente en el local origen.\n\nStock disponible: {art.S:N0}\nYa agregado al remito: {cantidadYaEnRemito:N0}\nIntenta agregar: {cant:N0}",
                "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (existente != null) existente.Cantidad += cant;
        else _items.Add(new LineaRemito { IdArt = art.Id, Codigo = art.Ca, Nombre = art.D, Cantidad = cant, Pcosto = art.Pc, Pcontado = art.Pv });

        RefrescarGrid();
        // Limpiar campos para siguiente artículo
        _artSeleccionado = null;
        _txtCodigo.Clear(); _txtDescripcion.Clear(); _txtPcosto.Clear(); _txtPventa.Clear();
        _txtCantidad.Text = "1";
        _txtCodigo.Focus();
    }

    private void OnQuitar(object sender, RoutedEventArgs e)
    {
        if (_gridDetalle.SelectedItem is LineaRemito l) { _items.Remove(l); RefrescarGrid(); }
    }

    private void RefrescarGrid()
    {
        _gridDetalle.ItemsSource = null;
        _gridDetalle.ItemsSource = _items.ToList();
        var totalCosto  = _items.Sum(x => x.SubCosto);
        var totalVenta  = _items.Sum(x => x.Subtotal);
        _lblSubCosto.Text = $"Total costo: {totalCosto:N0}";
        _lblSubVenta.Text = $"Total venta: {totalVenta:N0}";
        _lblTotal.Text    = $"Artículos: {_items.Count}";
        _btnConfirmar.IsEnabled = _items.Count > 0;
    }

    private async void OnConfirmar(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;

        // Con IsEditable=true (búsqueda por código, ver CargarLocalesAsync) el usuario puede
        // escribir texto que no matchea ningún local y dejar el combo sin una selección real
        // — SelectedItem queda null pero el texto tipeado sigue visible, dando la falsa
        // impresión de que un local quedó elegido. GetLocalId(...) devolvía "1" por defecto en
        // ese caso, con riesgo real de transferir desde/hacia el local equivocado sin aviso.
        if (_cboOrigen.SelectedItem is not ComboBoxItem)
        { MessageBox.Show("Elegí un Origen válido de la lista (por código o nombre).", "Local inválido",
            MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (_cboDestino.SelectedItem is not ComboBoxItem)
        { MessageBox.Show("Elegí un Destino válido de la lista (por código o nombre).", "Local inválido",
            MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var origen  = GetLocalId(_cboOrigen);
        var destino = GetLocalId(_cboDestino);
        if (origen == destino) { MessageBox.Show("Origen y destino no pueden ser el mismo."); return; }

        var sesion = SessionService.Instance;
        if (sesion.UsuarioActual == null) return;

        var confirm = MessageBox.Show($"¿Confirmar transferencia de {_items.Count} artículo(s) de Local {origen} a Local {destino}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _btnConfirmar.IsEnabled = false;
        try
        {
            using var conn = _db.Create();
            var nroRem = $"REM-{DateTime.Now:yyyyMMddHHmmss}";
            var totalCosto   = _items.Sum(x => x.Cantidad * x.Pcosto);
            var totalContado = _items.Sum(x => x.Subtotal);

            // Pedido explícito: si el usuario no escribe nota, se genera una automática con
            // quién envía, de dónde a dónde y cuántos artículos — antes quedaba vacía en la
            // gran mayoría de los casos y no quedaba ningún rastro útil (ni siquiera se copia
            // a CAB_REMITO_ACEPTADO una vez aceptada, esa tabla no tiene columna NOTA).
            var nota = _txtNota.Text.Trim();
            if (string.IsNullOrEmpty(nota))
                nota = $"Transferencia de {sesion.UsuarioActual.NombreUsuario} — " +
                       $"{GetLocalNombre(_cboOrigen)} → {GetLocalNombre(_cboDestino)} — " +
                       $"{_items.Count} artículo(s) — {DateTime.Now:dd/MM/yyyy HH:mm}";
            if (nota.Length > 255) nota = nota.Substring(0, 255);

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                var p = new DynamicParameters();
                // El SP solo reconoce @AGENTE='SI' (crea CAB_REMITO_TMP + primer detalle)
                // o 'NO' (agrega detalle al remito ya creado). Cualquier otro valor no cae
                // en ninguna rama del IF, hace COMMIT de una transacción vacía y devuelve
                // 'GUARDADO' sin insertar nada — por eso la transferencia no aparecía a aceptar.
                p.Add("@AGENTE",              i == 0 ? "SI" : "NO");
                p.Add("@id_remito_tmp",       0);
                p.Add("@numero_rem_tmp",      nroRem);
                p.Add("@idorigentmp",         (byte)origen);
                p.Add("@iddestinotmp",        (byte)destino);
                p.Add("@totalcosto",          totalCosto);
                p.Add("@totalcontado",        totalContado);
                p.Add("@idu",                 sesion.UsuarioActual.IdUsuario);
                p.Add("@estado",              (byte)0);
                p.Add("@nota",                nota);
                p.Add("@id_det_remito_tmp",   0);
                p.Add("@orden",               (byte)(i + 1));
                p.Add("@idart",               it.IdArt);
                p.Add("@cant",                it.Cantidad);
                p.Add("@pcosto",              it.Pcosto);
                p.Add("@pcontado",            it.Pcontado);
                p.Add("@IDMOVART",            0);
                p.Add("@MOV",                 (byte)1);
                p.Add("@MOD",                 (byte)3);
                p.Add("@STINI",               0m);
                p.Add("@PCANT",               0m);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                await conn.ExecuteAsync("REMITO_TMP_CS", p, commandType: CommandType.StoredProcedure);
            }

            MessageBox.Show($"Transferencia '{nroRem}' creada.\nPendiente de aceptación por el local destino.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnConfirmar.IsEnabled = true;
        }
    }

    private static int GetLocalId(ComboBox cbo)
        => cbo.SelectedItem is ComboBoxItem item && item.Tag is int id ? id : 1;

    private static string GetLocalNombre(ComboBox cbo)
        => cbo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "" : "";
}

// ── Buscador interno para transferencias ──────────────────────────────────────
internal class BuscadorTransferenciaArticulo : Window
{
    public ArticuloParaRemito? Seleccionado { get; private set; }
    private DataGrid _grid = null!;
    private readonly List<ArticuloParaRemito> _todos;

    public BuscadorTransferenciaArticulo(List<ArticuloParaRemito> todos)
    {
        _todos = todos;
        Title  = "Buscar Artículo";
        Width  = 700; Height = 480; MinWidth = 500; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250,250,252));
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var brPrim  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 21,101,192));
        var brDark  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 14, 47, 68));
        var brBorde = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187,222,251));
        var brAlt   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232,240,254));

        // Header
        var hdr = new Border { Background = brPrim, Padding = new Thickness(14,10,14,10) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "📦  Buscar Artículo", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8)
        });
        var searchB = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(11, 40, 60)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,136,229)),
            BorderThickness = new Thickness(1), Padding = new Thickness(10,0,6,0)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187,222,251)),
            Margin = new Thickness(0,0,8,0)
        });
        var txtBuscar = new TextBox {
            Height = 32, MinWidth = 320, FontSize = 12,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush  = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center
        };
        txtBuscar.TextChanged += (_, _) => Filtrar(txtBuscar.Text);
        txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Confirmar(); };
        searchRow.Children.Add(txtBuscar);
        searchB.Child = searchRow;
        hdrSp.Children.Add(searchB);
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // Grid
        var wrap = new Border { Margin = new Thickness(10,8,10,0), BorderBrush = brBorde,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), ClipToBounds = true };
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 30, ColumnHeaderHeight = 30, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = brBorde,
            RowBackground = System.Windows.Media.Brushes.White, AlternatingRowBackground = brAlt,
            BorderThickness = new Thickness(0)
        };
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, brPrim));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.0));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8,0,8,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, brDark));
        _grid.ColumnHeaderStyle = hs;
        var txL = new Style(typeof(TextBlock));
        txL.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(8,0,8,0)));
        txL.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        var txR = new Style(typeof(TextBlock));
        txR.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(8,0,8,0)));
        txR.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txR.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Ca"), Width = 100, ElementStyle = txL });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("D"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ubicación",   Binding = new System.Windows.Data.Binding("LocalUbicacion"), Width = 130, ElementStyle = txL });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Stock",       Binding = new System.Windows.Data.Binding("S")  { StringFormat = "N0" }, Width = 70, ElementStyle = txR });
        _grid.Columns.Add(new DataGridTextColumn { Header = "P. Costo",   Binding = new System.Windows.Data.Binding("Pc") { StringFormat = "N0" }, Width = 80, ElementStyle = txR });
        _grid.MouseDoubleClick += (_, _) => Confirmar();
        _grid.ItemsSource = _todos;
        wrap.Child = _grid;
        Grid.SetRow(wrap, 1); root.Children.Add(wrap);

        // Footer
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White, BorderBrush = brBorde,
            BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(10,7,10,7),
            Margin = new Thickness(0,4,0,0)
        };
        var btnSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnOk = new Button {
            Content = "✔  Seleccionar", Height = 30, Padding = new Thickness(14,0,14,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34,197,94)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,6,0)
        };
        var btnCerrar = new Button {
            Content = "✕  Cerrar", Height = 30, Padding = new Thickness(14,0,14,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnOk.Click     += (_, _) => Confirmar();
        btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };
        btnSp.Children.Add(btnOk); btnSp.Children.Add(btnCerrar);
        footer.Child = btnSp;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) => txtBuscar.Focus();
    }

    private void Filtrar(string q)
    {
        q = q.Trim().ToLowerInvariant();
        _grid.ItemsSource = string.IsNullOrEmpty(q) ? _todos
            : _todos.Where(a => a.D.ToLowerInvariant().Contains(q) || a.Ca.ToLowerInvariant().Contains(q)).ToList();
    }

    private void Confirmar()
    {
        if (_grid.SelectedItem is ArticuloParaRemito a)
        { Seleccionado = a; DialogResult = true; }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ACEPTAR TRANSFERENCIA
// ══════════════════════════════════════════════════════════════════════════════

public class AceptarTransferenciaWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private DataGrid  _gridRemitos  = null!;
    private DataGrid  _gridDetalle  = null!;
    private TextBlock _lblInfo      = null!;
    private Button    _btnAceptar   = null!;
    private TextBox   _txtBuscar    = null!;
    private TextBlock _txtBuscarPlaceholder = null!;
    private Border    _badgePendientes    = null!;
    private TextBlock _lblBadgePendientes = null!;
    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private ComboBox   _cboLocalDestino = null!;

    private List<Local> _locales = new();
    private bool _puedeVerTodosLosLocales;

    private List<CabRemito> _todosRemitos = new(); // ya filtrado por rango de fecha + local destino
    private List<CabRemito> _remitos = new();
    private CabRemito?      _seleccionado;

    private static readonly System.Windows.Media.SolidColorBrush _ABrPrim  = new(System.Windows.Media.Color.FromRgb( 21,101,192));
    private static readonly System.Windows.Media.SolidColorBrush _ABrDark  = new(System.Windows.Media.Color.FromRgb( 14, 47, 68));
    private static readonly System.Windows.Media.SolidColorBrush _ABrBorde = new(System.Windows.Media.Color.FromRgb(187,222,251));
    private static readonly System.Windows.Media.SolidColorBrush _ABrAlt   = new(System.Windows.Media.Color.FromRgb(232,240,254));

    public AceptarTransferenciaWindow()
    {
        _db   = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Aceptar Transferencia";
        Width = 980; Height = 600; MinWidth = 760; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250,250,252));
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        var sesion = SessionService.Instance;
        _puedeVerTodosLosLocales = sesion.UsuarioActual?.PuedeVerTodosLosLocales == true;

        using (var conn = _db.Create())
        {
            _locales = (await conn.QueryAsync<Local>(
                "SELECT ID_LOCAL as IdLocal, NOMBRE as NombreLocal FROM LOCALES ORDER BY NOMBRE")).ToList();
        }

        foreach (var l in _locales)
            _cboLocalDestino.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });

        var localUsuario = sesion.LocalActual;
        for (int i = 0; i < _cboLocalDestino.Items.Count; i++)
        {
            if (((ComboBoxItem)_cboLocalDestino.Items[i]).Tag is int id && id == localUsuario?.IdLocal)
            { _cboLocalDestino.SelectedIndex = i; break; }
        }
        if (_cboLocalDestino.SelectedIndex < 0 && _cboLocalDestino.Items.Count > 0)
            _cboLocalDestino.SelectedIndex = 0;

        // Un usuario normal solo debe ver lo que le corresponde recibir a SU local — antes
        // se mostraban las transferencias de todos los locales y el usuario se confundía
        // buscando en la lista completa. Solo ADMINISTRADOR o la excepción puntual (ver
        // Usuario.PuedeVerTodosLosLocales) puede cambiar el local y ver otras transferencias.
        _cboLocalDestino.IsEnabled = _puedeVerTodosLosLocales;
        _cboLocalDestino.SelectionChanged += async (_, _) => await CargarRemitosAsync();

        await CargarRemitosAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0 header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1 filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2 grid remitos
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3 info
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 4 grid detalle
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5 footer

        // ── Header ─────────────────────────────────────────────────────────
        var hdr = new Border {
            Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(0x0E, 0x2F, 0x44),
                System.Windows.Media.Color.FromRgb(0x1A, 0x4F, 0x6E), 0),
            Padding = new Thickness(20, 16, 20, 16)
        };
        var hdrRow = new Grid();
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrIcono = new Border {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(38,255,255,255)),
            Margin = new Thickness(0,0,14,0),
            // "✔" (check simple, Unicode estándar) en vez de "✅" (emoji multicolor con
            // relleno verde nativo que ignora Foreground y se ve oscuro sobre el círculo).
            Child = new TextBlock { Text = "✔", FontSize = 19, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(hdrIcono, 0); hdrRow.Children.Add(hdrIcono);

        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock {
            Text = "ACEPTAR TRANSFERENCIA", FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White
        });
        hdrTexts.Children.Add(new TextBlock {
            Text = "Confirmá la recepción de mercadería enviada a tu local",
            FontSize = 11, Foreground = _ABrBorde, Margin = new Thickness(0,3,0,0)
        });
        Grid.SetColumn(hdrTexts, 1); hdrRow.Children.Add(hdrTexts);

        _badgePendientes = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(45,255,255,255)),
            CornerRadius = new CornerRadius(16), Padding = new Thickness(14,7,14,7),
            VerticalAlignment = VerticalAlignment.Center
        };
        _lblBadgePendientes = new TextBlock {
            Text = "0 pendientes", FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White
        };
        _badgePendientes.Child = _lblBadgePendientes;
        Grid.SetColumn(_badgePendientes, 2); hdrRow.Children.Add(_badgePendientes);

        hdr.Child = hdrRow;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Filtros: rango de fecha + local que debe aceptar ────────────────
        var filtrosBorder = new Border {
            Background = _ABrDark, Padding = new Thickness(14,8,14,8)
        };
        var filtrosRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        filtrosRow.Children.Add(new TextBlock {
            Text = "Desde:", Foreground = System.Windows.Media.Brushes.White, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0)
        });
        var modernDpStyle = CrediSoft.UI.Views.Shared.UiStyles.ModernDatePickerStyle();
        _dpDesde = new DatePicker { Width = 120, FontSize = 11.5, Margin = new Thickness(0,0,14,0),
            Style = modernDpStyle, Foreground = System.Windows.Media.Brushes.Black,
            SelectedDate = DateTime.Today.AddDays(-30) };
        _dpDesde.SelectedDateChanged += async (_, _) => await CargarRemitosAsync();
        filtrosRow.Children.Add(_dpDesde);

        filtrosRow.Children.Add(new TextBlock {
            Text = "Hasta:", Foreground = System.Windows.Media.Brushes.White, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0)
        });
        _dpHasta = new DatePicker { Width = 120, FontSize = 11.5, Margin = new Thickness(0,0,14,0),
            Style = modernDpStyle, Foreground = System.Windows.Media.Brushes.Black,
            SelectedDate = DateTime.Today };
        _dpHasta.SelectedDateChanged += async (_, _) => await CargarRemitosAsync();
        filtrosRow.Children.Add(_dpHasta);

        filtrosRow.Children.Add(new TextBlock {
            Text = "Local que debe aceptar:", Foreground = System.Windows.Media.Brushes.White, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0)
        });
        _cboLocalDestino = new ComboBox { Width = 190, FontSize = 11.5, Margin = new Thickness(0,0,14,0),
            Background = System.Windows.Media.Brushes.White, Foreground = System.Windows.Media.Brushes.Black };
        filtrosRow.Children.Add(_cboLocalDestino);

        var btnFiltrar = new Button {
            Content = "🔍  Filtrar", Height = 26, Padding = new Thickness(12,0,12,0),
            Background = _ABrPrim, Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 11.5, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnFiltrar.Click += async (_, _) => await CargarRemitosAsync();
        filtrosRow.Children.Add(btnFiltrar);

        filtrosBorder.Child = filtrosRow;
        Grid.SetRow(filtrosBorder, 1); root.Children.Add(filtrosBorder);

        // ── Sección superior: remitos pendientes ───────────────────────────
        var topWrap = new Border {
            Margin = new Thickness(10,8,10,0),
            BorderBrush = _ABrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true
        };
        var topDp = new DockPanel();
        var topHdr = new Border { Background = _ABrDark, Padding = new Thickness(10,6,10,6) };
        var topHdrGrid = new Grid();
        topHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var topHdrTxt = new TextBlock {
            Text = "Transferencias pendientes de aceptación",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(topHdrTxt, 0); topHdrGrid.Children.Add(topHdrTxt);

        var searchBorder = new Border {
            Background = _ABrDark, CornerRadius = new CornerRadius(4),
            BorderBrush = _ABrBorde, BorderThickness = new Thickness(1),
            Padding = new Thickness(8,0,8,0), Width = 280
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = _ABrBorde, Margin = new Thickness(0,0,6,0)
        });

        // TextBox nativo no tiene placeholder — se simula con un TextBlock superpuesto
        // que se oculta apenas hay texto (WPF no expone Watermark en TextBox estándar).
        var searchInputGrid = new Grid { Width = 220 };
        _txtBuscarPlaceholder = new TextBlock {
            Text = "Buscar por remito o local...", FontSize = 11.5, FontStyle = FontStyles.Italic,
            Foreground = _ABrBorde, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
        };
        _txtBuscar = new TextBox {
            Height = 24, FontSize = 11.5, Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White, CaretBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center
        };
        _txtBuscar.TextChanged += (_, _) => {
            _txtBuscarPlaceholder.Visibility = string.IsNullOrEmpty(_txtBuscar.Text) ? Visibility.Visible : Visibility.Collapsed;
            AplicarFiltroTexto();
        };
        searchInputGrid.Children.Add(_txtBuscarPlaceholder);
        searchInputGrid.Children.Add(_txtBuscar);
        searchRow.Children.Add(searchInputGrid);
        searchBorder.Child = searchRow;
        Grid.SetColumn(searchBorder, 1); topHdrGrid.Children.Add(searchBorder);

        topHdr.Child = topHdrGrid;
        DockPanel.SetDock(topHdr, Dock.Top); topDp.Children.Add(topHdr);

        _gridRemitos = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 32, ColumnHeaderHeight = 30, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = _ABrBorde,
            RowBackground = System.Windows.Media.Brushes.White, AlternatingRowBackground = _ABrAlt,
            BorderThickness = new Thickness(0)
        };
        var hs1 = MkHdrStyle();
        _gridRemitos.ColumnHeaderStyle = hs1;
        var txL = MkTxStyle(false); var txR = MkTxStyle(true);
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "N° Remito", Binding = new System.Windows.Data.Binding("NumeroRemTmp"), Width = 170, ElementStyle = txL });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Origen",    Binding = new System.Windows.Data.Binding("LocalOrigen"),  Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Destino",   Binding = new System.Windows.Data.Binding("LocalDestino"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Total",     Binding = new System.Windows.Data.Binding("TotalContado") { StringFormat = "N0" }, Width = 100, ElementStyle = txR });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Fecha",     Binding = new System.Windows.Data.Binding("Fecha") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = 140, ElementStyle = txL });
        _gridRemitos.SelectionChanged += OnRemitoSeleccionado;
        topDp.Children.Add(_gridRemitos);
        topWrap.Child = topDp;
        Grid.SetRow(topWrap, 2); root.Children.Add(topWrap);

        // ── Info entre grids ───────────────────────────────────────────────
        _lblInfo = new TextBlock {
            Margin = new Thickness(12,4,12,4),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)),
            FontSize = 11
        };
        Grid.SetRow(_lblInfo, 3); root.Children.Add(_lblInfo);

        // ── Sección inferior: detalle artículos ────────────────────────────
        var botWrap = new Border {
            Margin = new Thickness(10,0,10,0),
            BorderBrush = _ABrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true
        };
        var botDp = new DockPanel();
        var botHdr = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14,47,68)),
            Padding = new Thickness(10,6,10,6)
        };
        botHdr.Child = new TextBlock {
            Text = "Artículos de la transferencia seleccionada",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12
        };
        DockPanel.SetDock(botHdr, Dock.Top); botDp.Children.Add(botHdr);
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 30, ColumnHeaderHeight = 28, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = _ABrBorde,
            RowBackground = System.Windows.Media.Brushes.White, AlternatingRowBackground = _ABrAlt,
            BorderThickness = new Thickness(0)
        };
        _gridDetalle.ColumnHeaderStyle = MkHdrStyle();
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Artículo",  Binding = new System.Windows.Data.Binding("Nombre"),   Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cantidad",  Binding = new System.Windows.Data.Binding("Cantidad") { StringFormat = "N0" }, Width = 80, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Costo",  Binding = new System.Windows.Data.Binding("Pcosto")   { StringFormat = "N0" }, Width = 90, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Venta",  Binding = new System.Windows.Data.Binding("Pcontado") { StringFormat = "N0" }, Width = 90, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Sub costo", Binding = new System.Windows.Data.Binding("SubCosto") { StringFormat = "N0" }, Width = 90, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Sub venta", Binding = new System.Windows.Data.Binding("Subtotal") { StringFormat = "N0" }, Width = 90, ElementStyle = txR });
        _gridDetalle.Columns.Add(new DataGridTextColumn {
            Header = "Stock actual en destino", Binding = new System.Windows.Data.Binding("StockActualDestino") { StringFormat = "N0" },
            Width = 150, ElementStyle = MkTxStyle(true),
            CellStyle = new Style(typeof(DataGridCell)) { Setters = {
                new Setter(DataGridCell.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232,240,254)))
            }}
        });
        botDp.Children.Add(_gridDetalle);
        botWrap.Child = botDp;
        Grid.SetRow(botWrap, 4); root.Children.Add(botWrap);

        // ── Footer ─────────────────────────────────────────────────────────
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White, BorderBrush = _ABrBorde,
            BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(12,8,12,8),
            Margin = new Thickness(0,4,0,0)
        };
        var footerDp = new DockPanel();
        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _btnAceptar = new Button {
            Content = "✔  Aceptar transferencia", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34,197,94)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            IsEnabled = false, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,6,0)
        };
        _btnAceptar.Click += OnAceptar;
        var btnRefresh = new Button {
            Content = "↺  Refrescar", Height = 32, Padding = new Thickness(14,0,14,0),
            Background = _ABrPrim, Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0,0,6,0)
        };
        btnRefresh.Click += async (_, _) => await CargarRemitosAsync();
        var btnCerrar = new Button {
            Content = "✕  Cerrar", Height = 32, Padding = new Thickness(14,0,14,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnCerrar.Click += (_, _) => Close();
        rightBtns.Children.Add(_btnAceptar); rightBtns.Children.Add(btnRefresh); rightBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(rightBtns, Dock.Right);
        footerDp.Children.Add(rightBtns);
        footer.Child = footerDp;
        Grid.SetRow(footer, 5); root.Children.Add(footer);

        Content = root;
    }

    private Style MkHdrStyle()
    {
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, _ABrDark));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.0));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,0,10,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, _ABrDark));
        return hs;
    }
    private static Style MkTxStyle(bool right)
    {
        var s = new Style(typeof(TextBlock));
        s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,10,0)));
        s.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        if (right) s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        return s;
    }

    private async Task CargarRemitosAsync()
    {
        _seleccionado = null;
        _btnAceptar.IsEnabled = false;
        _gridDetalle.ItemsSource = null;

        using var conn = _db.Create();
        var p = new DynamicParameters();
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var rows = (await conn.QueryAsync<dynamic>("CARGAR_REV_REMITO_CS", p, commandType: CommandType.StoredProcedure)).ToList();

        // Cargar nombres de locales
        var locales = (await conn.QueryAsync<dynamic>("SELECT ID_LOCAL, NOMBRE FROM LOCALES")).ToDictionary(r => (int)r.ID_LOCAL, r => (string)r.NOMBRE);

        var todos = rows.Select(r => new CabRemito
        {
            IdRemitoTmp  = (int)r.ID_REMITO_TMP,
            NumeroRemTmp = (string)r.NUMERO_REM_TMP,
            IdOrigenTmp  = (byte)r.IDORIGENTMP,
            IdDestinoTmp = (byte)r.IDDESTINOTMP,
            Estado       = (byte)r.ESTADO,
            Fecha        = (DateTime)r.FECHA,
            TotalCosto   = (decimal)r.TOTALCOSTO,
            TotalContado = (decimal)r.TOTALCONTADO,
            Idu          = (int)r.IDU,
            LocalOrigen  = locales.TryGetValue((int)r.IDORIGENTMP, out var o) ? o : r.IDORIGENTMP.ToString(),
            LocalDestino = locales.TryGetValue((int)r.IDDESTINOTMP, out var d) ? d : r.IDDESTINOTMP.ToString()
        }).ToList();

        var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddDays(-30);
        var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1); // inclusive todo el día
        todos = todos.Where(r => r.Fecha >= desde && r.Fecha < hasta).ToList();

        // Pedido explícito: ahora se filtra por el local seleccionado en "Local que debe
        // aceptar" (preseleccionado con el local del usuario logueado). Antes se quitó el
        // filtro porque los usuarios confundían locales de nombre parecido (ej. "Tavai" vs
        // "Dep. Tavai") y buscaban en el local equivocado — el selector explícito con nombre
        // resuelve esa confusión sin perder la restricción. Solo ADMINISTRADOR o la excepción
        // puntual (Usuario.PuedeVerTodosLosLocales) puede cambiar el combo y ver otro local.
        if (_cboLocalDestino.SelectedItem is ComboBoxItem itemLocal && itemLocal.Tag is int idLocalSel)
            todos = todos.Where(r => r.IdDestinoTmp == idLocalSel).ToList();

        // Más nuevo primero — con el orden crudo del SP (por ID ascendente) las transferencias
        // recién enviadas quedaban al final de la lista y se confundían con las más viejas.
        _todosRemitos = todos.OrderByDescending(r => r.Fecha).ToList();

        AplicarFiltroTexto();
    }

    private void AplicarFiltroTexto()
    {
        var q = (_txtBuscar?.Text ?? "").Trim();
        _remitos = string.IsNullOrEmpty(q)
            ? _todosRemitos
            : _todosRemitos.Where(r =>
                r.NumeroRemTmp.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.LocalOrigen.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.LocalDestino.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _gridRemitos.ItemsSource = _remitos;
        _lblInfo.Text = string.IsNullOrEmpty(q)
            ? $"{_remitos.Count} transferencias pendientes de aceptación."
            : $"{_remitos.Count} de {_todosRemitos.Count} transferencias (filtro: \"{q}\").";

        _lblBadgePendientes.Text = _todosRemitos.Count == 1
            ? "1 pendiente"
            : $"{_todosRemitos.Count} pendientes";
    }

    private async void OnRemitoSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        if (_gridRemitos.SelectedItem is not CabRemito rem) return;
        _seleccionado = rem;
        _btnAceptar.IsEnabled = true;

        using var conn = _db.Create();
        var p = new DynamicParameters();
        p.Add("@elID", rem.IdRemitoTmp);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var rows = await conn.QueryAsync<dynamic>("BUSCAR_DETREMITO_TMP_CS", p, commandType: CommandType.StoredProcedure);

        // SP devuelve: Id, Código, Descripción, Cantidad, "P. costo", "P. contado", SubCosto, SubContado, ir
        var detalles = rows.Select(r => {
            var row = (IDictionary<string, object>)r;
            return new LineaRemito {
                IdArt    = row.TryGetValue("Id",          out var id)   && id   != null ? Convert.ToInt32(id)   : 0,
                Codigo   = row.TryGetValue("Código",      out var ca)   && ca   != null ? ca.ToString()!        : "",
                Nombre   = row.TryGetValue("Descripción", out var desc) && desc != null ? desc.ToString()!      : "",
                Cantidad = row.TryGetValue("Cantidad",    out var cant) && cant != null ? Convert.ToDecimal(cant) : 0m,
                Pcosto   = row.TryGetValue("P. costo",   out var pc)   && pc   != null ? Convert.ToDecimal(pc)   : 0m,
                Pcontado = row.TryGetValue("P. contado", out var pv)   && pv   != null ? Convert.ToDecimal(pv)   : 0m
            };
        }).ToList();

        // Stock actual del artículo en el local DESTINO — para que el usuario pueda validar
        // antes de aceptar (pedido explícito: "puedes validar cuanto stock tengo en este
        // producto antes de confirmar transferencia"). Se consulta aparte de BUSCAR_DETREMITO_TMP_CS
        // (que no trae stock) para no tocar el SP legado.
        if (detalles.Count > 0)
        {
            var idsArt = detalles.Select(d => d.IdArt).ToList();
            var stocks = (await conn.QueryAsync<(int IdArt, decimal Stock)>(
                "SELECT IDART, S FROM PRICES WHERE IDLOCAL = @IdLocal AND IDART IN @IdsArt",
                new { IdLocal = rem.IdDestinoTmp, IdsArt = idsArt }))
                .ToDictionary(x => x.IdArt, x => x.Stock);
            foreach (var d in detalles)
                d.StockActualDestino = stocks.TryGetValue(d.IdArt, out var s) ? s : 0m;
        }

        _gridDetalle.ItemsSource = detalles;
    }

    private async Task<bool> PedirCredenciales()
    {
        var repo = App.Services.GetRequiredService<IUsuarioRepository>();
        var ok = false;

        TextBox txtCodigo = null!;
        PasswordBox txtPass = null!;
        TextBlock lblError = null!;
        Button btnOk = null!;

        var dlg = new Window
        {
            Title = "Confirmar identidad",
            Width = 360, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };

        var brNaranja = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14,47,68));
        var brGris    = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(117,117,117));

        var hdr = new Border {
            Background = brNaranja, Padding = new Thickness(16,12,16,12),
            Child = new TextBlock {
                Text = "🔐  Ingrese sus credenciales para continuar",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };

        static TextBlock Lbl(string t) => new TextBlock {
            Text = t, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14,47,68)),
            Margin = new Thickness(0,8,0,2)
        };

        txtCodigo = new TextBox { Padding = new Thickness(6,4,6,4) };
        txtPass   = new PasswordBox { Padding = new Thickness(6,4,6,4) };
        lblError  = new TextBlock {
            Foreground = System.Windows.Media.Brushes.Red,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,6,0,0),
            Visibility = Visibility.Collapsed
        };

        var body = new StackPanel { Margin = new Thickness(16,4,16,8) };
        body.Children.Add(Lbl("Código de usuario"));
        body.Children.Add(txtCodigo);
        body.Children.Add(Lbl("Contraseña"));
        body.Children.Add(txtPass);
        body.Children.Add(lblError);

        btnOk = new Button {
            Content = "Confirmar", Width = 110, Height = 32,
            Margin = new Thickness(0,0,8,0),
            Background = brNaranja,
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnCancelar = new Button {
            Content = "Cancelar", Width = 90, Height = 32,
            Background = brGris,
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnCancelar.Click += (_, _) => dlg.Close();

        async Task Verificar()
        {
            lblError.Visibility = Visibility.Collapsed;
            btnOk.IsEnabled = false; btnOk.Content = "Verificando...";
            try
            {
                var usuario = await repo.BuscarPorCodigoAsync(txtCodigo.Text.Trim());
                if (usuario == null || usuario.ContrasenaUsuario != txtPass.Password)
                {
                    lblError.Text = "Código o contraseña incorrectos.";
                    lblError.Visibility = Visibility.Visible;
                    txtPass.Clear(); txtPass.Focus();
                    return;
                }
                ok = true;
                dlg.Close();
            }
            finally { btnOk.IsEnabled = true; btnOk.Content = "Confirmar"; }
        }

        btnOk.Click    += async (_, _) => await Verificar();
        txtPass.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Verificar(); };
        txtCodigo.KeyDown += (_, e) => { if (e.Key == Key.Enter) txtPass.Focus(); };

        var sep = new Border { Height = 1, Margin = new Thickness(0,4,0,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(221,221,221)) };
        var btns = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16,10,16,16)
        };
        btns.Children.Add(btnOk); btns.Children.Add(btnCancelar);

        var root = new StackPanel();
        root.Children.Add(hdr); root.Children.Add(body);
        root.Children.Add(sep); root.Children.Add(btns);
        dlg.Content = root;
        dlg.Loaded += (_, _) => txtCodigo.Focus();
        dlg.ShowDialog();
        return ok;
    }

    private bool ConfirmarAceptacion(CabRemito rem, int cantItems)
    {
        var resultado = false;

        var dlg = new Window {
            Title = "Confirmar aceptación de transferencia",
            Width = 460, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };

        var brVerde  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34,139,34));
        var brNaranja = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21,101,192));
        var brGris   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128));
        var brFondo  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,245,245));
        var brBorde  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220,220,220));

        // Header verde
        var hdr = new Border {
            Background = brVerde, Padding = new Thickness(16,12,16,12),
            Child = new TextBlock {
                Text = "📥  ¿Confirmar recepción de transferencia?",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14, FontWeight = FontWeights.Bold
            }
        };

        // Tarjeta de datos
        var card = new Border {
            Margin = new Thickness(16,12,16,4),
            Background = brFondo, BorderBrush = brBorde,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16,12,16,12)
        };

        static StackPanel Fila(string lbl, string val, bool resaltar = false)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,4) };
            sp.Children.Add(new TextBlock {
                Text = lbl, Width = 130, FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80,80,80))
            });
            sp.Children.Add(new TextBlock {
                Text = val,
                FontWeight = resaltar ? FontWeights.Bold : FontWeights.Normal,
                Foreground = resaltar
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34,139,34))
                    : System.Windows.Media.Brushes.Black,
                FontSize = resaltar ? 13 : 12
            });
            return sp;
        }

        var cardSp = new StackPanel();
        cardSp.Children.Add(Fila("N° Remito:",      rem.NumeroRemTmp));
        cardSp.Children.Add(new Border { Height = 1, Background = brBorde, Margin = new Thickness(0,4,0,4) });
        cardSp.Children.Add(Fila("Local origen:",   rem.LocalOrigen));
        cardSp.Children.Add(Fila("Local destino:",  rem.LocalDestino));
        cardSp.Children.Add(new Border { Height = 1, Background = brBorde, Margin = new Thickness(0,4,0,4) });
        cardSp.Children.Add(Fila("Fecha remito:",   rem.Fecha.ToString("dd/MM/yyyy HH:mm")));
        cardSp.Children.Add(Fila("Artículos:",      $"{cantItems} ítem(s)"));
        cardSp.Children.Add(Fila("Total costo:",    $"Gs. {rem.TotalCosto:N0}"));
        cardSp.Children.Add(Fila("Total venta:",    $"Gs. {rem.TotalContado:N0}", resaltar: true));
        card.Child = cardSp;

        // La lista de remitos ya no filtra por local (ver CargarRemitosAsync) — cualquier
        // usuario puede ver transferencias de cualquier sucursal, para no depender de que
        // busquen en el local exacto (confundían locales con nombres parecidos, ej. "Tavai"
        // vs "Dep. Tavai"). Como contrapartida, si el local de sesión actual no coincide con
        // el destino real del remito, se agrega este aviso rojo bien visible — no bloquea la
        // acción (puede ser legítimo, ej. un encargado aceptando por otra sucursal), pero
        // obliga a una lectura consciente antes de confirmar.
        var sesionLocal = SessionService.Instance.LocalActual;
        bool localNoCoincide = sesionLocal != null && sesionLocal.IdLocal != rem.IdDestinoTmp;
        UIElement? avisoLocalIncorrecto = null;
        if (localNoCoincide)
        {
            avisoLocalIncorrecto = new Border {
                Margin = new Thickness(16,4,16,4),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254,226,226)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220,38,38)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12,8,12,8),
                Child = new TextBlock {
                    Text = $"🛑  ATENCIÓN: esta transferencia es para \"{rem.LocalDestino}\", pero tu sesión actual está en " +
                           $"\"{sesionLocal!.NombreLocal}\". Aceptala solo si estás seguro de que la mercadería realmente " +
                           $"llegó a \"{rem.LocalDestino}\" — el stock se sumará ahí, no en tu local actual.",
                    TextWrapping = TextWrapping.Wrap, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153,27,27))
                }
            };
        }

        // Aviso
        var aviso = new Border {
            Margin = new Thickness(16,4,16,12),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,243,205)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,193,7)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12,8,12,8),
            Child = new TextBlock {
                Text = "⚠️  Al aceptar, el stock será actualizado en el local destino y descontado del local origen. Esta acción no se puede deshacer.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100,70,0))
            }
        };

        // Botones
        var btnSi = new Button {
            Content = "✔  Sí, aceptar transferencia", Height = 34, Padding = new Thickness(16,0,16,0),
            Background = brVerde, Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,8,0)
        };
        var btnNo = new Button {
            Content = "✕  Cancelar", Height = 34, Padding = new Thickness(14,0,14,0),
            Background = brGris, Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnSi.Click += (_, _) => { resultado = true; dlg.Close(); };
        btnNo.Click += (_, _) => dlg.Close();

        var sep = new Border { Height = 1, Background = brBorde };
        var btns = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16,10,16,14)
        };
        btns.Children.Add(btnSi); btns.Children.Add(btnNo);

        var root = new StackPanel();
        root.Children.Add(hdr);
        root.Children.Add(card);
        if (avisoLocalIncorrecto != null) root.Children.Add(avisoLocalIncorrecto);
        root.Children.Add(aviso);
        root.Children.Add(sep);
        root.Children.Add(btns);
        dlg.Content = root;
        dlg.ShowDialog();
        return resultado;
    }

    private async void OnAceptar(object sender, RoutedEventArgs e)
    {
        if (_seleccionado == null) return;

        var sesion = SessionService.Instance;
        if (sesion.UsuarioActual == null) return;

        if (!await PedirCredenciales()) return;

        var rem = _seleccionado;

        // Contar artículos del detalle actual
        var cantItems = (_gridDetalle.ItemsSource as System.Collections.IList)?.Count ?? 0;

        if (!ConfirmarAceptacion(rem, cantItems)) return;

        _btnAceptar.IsEnabled = false;
        try
        {
            using var conn = _db.Create();
            // Open() explícito: sin esto, el primer QueryAsync (al SP) abre la conexión y
            // Dapper la vuelve a CERRAR sola al terminar (comportamiento normal cuando la
            // conexión estaba cerrada al invocar el método) — el BeginTransaction() de más
            // abajo caía entonces sobre una conexión ya cerrada, tirando exactamente
            // "Operación no válida; se ha terminado la conexión" (bug real reportado en
            // producción, remito 000000047 Depósito 1 San Juan Nep → Tavai, 14/08/2026).
            conn.Open();

            // Cargar detalle del remito
            var pdet = new DynamicParameters();
            pdet.Add("@elID", rem.IdRemitoTmp);
            pdet.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var detalles = (await conn.QueryAsync<dynamic>("BUSCAR_DETREMITO_TMP_CS", pdet, commandType: CommandType.StoredProcedure)).ToList();

            // Reemplaza EXEC REMITO_ACEPTADO_CS por INSERT/UPDATE directo — mismo motivo que
            // CuotaRepository.CobrarCuotaAsync (SP legado compartido con CrediMar.exe VB6, no
            // se puede tocar). Se replica acá la secuencia exacta del SP en UNA sola transacción
            // real para todo el remito (el SP corría una transacción propia POR ARTÍCULO, sin
            // atomicidad entre ellos — un remito de varios artículos podía quedar a medio
            // aceptar si fallaba en el artículo 3 de 5), con dos fixes sobre el comportamiento
            // original:
            //   1) DELETADO=0 al sumar stock en destino — el SP nunca reactivaba este flag; si
            //      el artículo estaba dado de baja en el local destino (limpieza de catálogo
            //      anterior), el stock quedaba sumado bien pero invisible/no vendible en "Ver
            //      Artículos" (mismo bug ya corregido en Compras, ComprasWindows.cs ~línea 4055).
            //      Caso real que lo destapó: Aire Acondicionado Tokio 18000BTU, Local 6 ->
            //      Local 7 Buena Vista, 12/08/2026 — la encargada de destino veía stock 0 pese
            //      a la transferencia aceptada, y terminó generándose una segunda transferencia
            //      de la misma unidad por la confusión.
            //   2) Una sola cabecera en CAB_REMITO_ACEPTADO por remito, no una por artículo (ver
            //      comentario histórico ya eliminado acá: mandar @AGENTE='SI' en cada vuelta del
            //      loop original inflaba x12 el TOTALCOSTO/TOTALCONTADO de reportes que suman
            //      esa tabla — bug real confirmado en producción, IDREMITO 12078-12089).
            using var tx = conn.BeginTransaction();

            var idRemito = await conn.ExecuteScalarAsync<int?>(
                "SELECT MAX(IDREMITO) FROM CAB_REMITO_ACEPTADO", transaction: tx) ?? 0;
            idRemito++;

            var nomMaquina = Dns.GetHostName();
            const string ipMaquina = "127.0.0.1";

            await conn.ExecuteAsync(
                "INSERT INTO CAB_REMITO_ACEPTADO (IDREMITO,NUMERO_REM_TMP,IDORIGEN,IDDESTINO,TOTALCOSTO,TOTALCONTADO,IDUENVIO,IDURECIBO,FECHA) " +
                "VALUES (@IdRemito,@NumeroRemTmp,@IdOrigen,@IdDestino,@TotalCosto,@TotalContado,@IdUEnvio,@IdURecibo,GETDATE())",
                new {
                    IdRemito = idRemito, rem.NumeroRemTmp, IdOrigen = rem.IdOrigenTmp, IdDestino = rem.IdDestinoTmp,
                    rem.TotalCosto, rem.TotalContado, IdUEnvio = rem.Idu, IdURecibo = sesion.UsuarioActual.IdUsuario
                }, tx);

            for (int i = 0; i < detalles.Count; i++)
            {
                var det = (IDictionary<string, object>)detalles[i];
                var idArt     = det.TryGetValue("Id",         out var idartO) && idartO != null ? Convert.ToInt32(idartO) : 0;
                var cant      = det.TryGetValue("Cantidad",   out var cantO)  && cantO  != null ? Convert.ToDecimal(cantO)  : 0m;
                var pcosto    = det.TryGetValue("P. costo",   out var pcO)    && pcO    != null ? Convert.ToDecimal(pcO)    : 0m;
                var pcontado  = det.TryGetValue("P. contado", out var pvO)    && pvO    != null ? Convert.ToDecimal(pvO)    : 0m;

                var idDetRemito = await conn.ExecuteScalarAsync<int?>(
                    "SELECT MAX(IDDETREMITO) FROM DET_REMITO_ACEPTADO", transaction: tx) ?? 0;
                idDetRemito++;
                await conn.ExecuteAsync(
                    "INSERT INTO DET_REMITO_ACEPTADO (IDDETREMITO,IDREMITO,ORDEN,IDART,CANT,PCOSTO,PCONTADO) " +
                    "VALUES (@IdDetRemito,@IdRemito,@Orden,@IdArt,@Cant,@PCosto,@PContado)",
                    new { IdDetRemito = idDetRemito, IdRemito = idRemito, Orden = (byte)(i + 1), IdArt = idArt, Cant = cant, PCosto = pcosto, PContado = pcontado }, tx);

                // UPDLOCK/ROWLOCK — mismo criterio que el SP original, evita condiciones de
                // carrera si dos remitos del mismo artículo se aceptan en simultáneo.
                var stockDestinoAntes = await conn.ExecuteScalarAsync<decimal?>(
                    "SELECT S FROM PRICES WITH (UPDLOCK, ROWLOCK) WHERE IDART=@IdArt AND IDLOCAL=@IdDestino",
                    new { IdArt = idArt, IdDestino = rem.IdDestinoTmp }, tx) ?? 0m;
                var pcDestino = await conn.ExecuteScalarAsync<decimal?>(
                    "SELECT PC FROM PRICES WHERE IDART=@IdArt AND IDLOCAL=@IdDestino",
                    new { IdArt = idArt, IdDestino = rem.IdDestinoTmp }, tx) ?? 0m;
                // Bloquea también la fila de origen (sin usar el valor) para evitar condiciones
                // de carrera si dos remitos del mismo artículo se aceptan en simultáneo — mismo
                // criterio que el SP original.
                await conn.ExecuteScalarAsync<decimal?>(
                    "SELECT S FROM PRICES WITH (UPDLOCK, ROWLOCK) WHERE IDART=@IdArt AND IDLOCAL=@IdOrigen",
                    new { IdArt = idArt, IdOrigen = rem.IdOrigenTmp }, tx);

                // Fix: DELETADO=0 al sumar stock en destino (ver comentario arriba).
                await conn.ExecuteAsync(
                    "UPDATE PRICES SET S = S + @Cant, DELETADO = 0 WHERE IDART = @IdArt AND IDLOCAL = @IdDestino",
                    new { Cant = cant, IdArt = idArt, IdDestino = rem.IdDestinoTmp }, tx);
                await conn.ExecuteAsync(
                    "UPDATE PRICES SET S = S - @Cant WHERE IDART = @IdArt AND IDLOCAL = @IdOrigen",
                    new { Cant = cant, IdArt = idArt, IdOrigen = rem.IdOrigenTmp }, tx);

                var idMovArt = await conn.ExecuteScalarAsync<int?>(
                    "SELECT MAX(IDMOVART) FROM MOVART", transaction: tx) ?? 0;
                idMovArt++;
                await conn.ExecuteAsync(
                    "INSERT INTO MOVART (IDMOVART,IDART,MOV,MOD,STINI,CANT,IDLOCAL,IDDESTINO,PCANT,PCACT,IDU,FECHA) " +
                    "VALUES (@IdMovArt,@IdArt,2,3,@StIni,@Cant,@IdOrigen,@IdDestino,@PcDestino,@PcDestino,@IdURecibo,GETDATE())",
                    new {
                        IdMovArt = idMovArt, IdArt = idArt, StIni = stockDestinoAntes, Cant = cant,
                        IdOrigen = rem.IdOrigenTmp, IdDestino = rem.IdDestinoTmp, PcDestino = pcDestino,
                        IdURecibo = sesion.UsuarioActual.IdUsuario
                    }, tx);
            }

            await conn.ExecuteAsync(
                "DELETE FROM CAB_REMITO_TMP WHERE NUMERO_REM_TMP = @NumeroRemTmp",
                new { rem.NumeroRemTmp }, tx);

            await conn.ExecuteAsync(
                "INSERT INTO AUDITORIA (FECHA_HORA,ID_USUARIO,TABLA,ID_REGISTRO,OPERACION,CAMPO,VALOR_ANTES,VALOR_DESPUES,MODULO,NOM_MAQUINA,IP_MAQUINA) " +
                "VALUES (GETDATE(),@IdUsuario,'CAB_REMITO_ACEPTADO',@IdRegistro,'I','TODOS','(NUEVO)',@Detalle,'TRANSFERENCIA ACEPTADA',@NomMaquina,@IpMaquina)",
                new {
                    IdUsuario = sesion.UsuarioActual.IdUsuario, IdRegistro = idRemito.ToString(),
                    Detalle = $"Remito: {rem.NumeroRemTmp} | Origen: {rem.IdOrigenTmp} | Destino: {rem.IdDestinoTmp} | " +
                              $"Artículos: {detalles.Count} | Total costo: {rem.TotalCosto:N0}",
                    NomMaquina = nomMaquina, IpMaquina = ipMaquina
                }, tx);

            tx.Commit();

            MessageBox.Show("Transferencia aceptada correctamente. El stock fue actualizado.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarRemitosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnAceptar.IsEnabled = true;
        }
    }
}
