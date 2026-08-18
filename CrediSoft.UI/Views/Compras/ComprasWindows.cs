using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
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
// Convierte decimal C# a SqlDecimal con precisión/escala exacta para evitar
// "Error converting data type numeric/decimal" en parámetros de SP.
file static class SpDecimal
{
    // Para columnas decimal(9,0) — precios enteros
    public static System.Data.SqlTypes.SqlDecimal D90(decimal v)
        => new System.Data.SqlTypes.SqlDecimal(9, 0, v >= 0, GetBytes(Math.Truncate(v)));

    // Para columnas decimal(9,3) — cantidades y ppromo
    public static System.Data.SqlTypes.SqlDecimal D93(decimal v)
        => new System.Data.SqlTypes.SqlDecimal(9, 3, v >= 0, GetBytes(v));

    static int[] GetBytes(decimal d)
    { var bits = decimal.GetBits(d); return new[] { bits[0], bits[1], bits[2], 0 }; }
}

public class NuevaCompraWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly SessionService       _sesion;

    // Colores
    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb( 26, 79,110));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb( 14, 47, 68));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229,231,235));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel    = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 22,163, 74));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrAzul     = new(System.Windows.Media.Color.FromRgb( 59,130,246));
    private static readonly System.Windows.Media.SolidColorBrush BrFondoArt = new(System.Windows.Media.Color.FromRgb(235,243,248));

    // Búsqueda artículo
    private TextBox   _txtBuscarArt    = null!;
    private TextBlock _lblNombreArt    = null!;

    // Panel precios del artículo seleccionado
    private TextBox   _txtPC           = null!;
    private TextBox   _txtPV           = null!;
    private TextBox   _txtContado      = null!;
    private TextBox   _txtPPromo       = null!;
    private TextBox   _txtPctPV        = null!;
    private TextBox   _txtPctContado   = null!;
    private TextBox   _txtPctPromo     = null!;
    private bool      _recalcPctC      = false;
    // Si el usuario editó a mano cualquiera de los 4 precios (o el % que los calcula) para el
    // artículo actual, cambiar de Local Destino NO debe volver a pisarlos consultando PRICES
    // del nuevo local — bug real reportado: el usuario ajustaba PRECIO PROMOCIÓN u otro precio
    // y, al cambiar de local con SeleccionarLocal, RecargarPreciosLocal() los sobreescribía en
    // silencio con lo que hubiera guardado para ese artículo en el nuevo local. Se resetea a
    // false cada vez que se carga un artículo (nuevo o desde CargarPreciosLocal/AbrirBuscador),
    // momento en el que SÍ corresponde reflejar los precios reales de PRICES.
    private bool      _preciosEditadosManualmente = false;
    private bool      _cargandoPreciosProgramatico = false;
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
        // Pedido explícito: Administrador y el usuario código 67 (Usuario.PuedeVerTodosLosLocales)
        // manejan compras para distintos locales todo el tiempo y suelen olvidarse de revisar
        // cuál quedó precargado por defecto — eso fue la causa real del bug de "compra fue a
        // Ma. Auxiliadora en vez del local elegido" en producción. Para ellos el campo arranca
        // vacío a propósito, forzando una elección consciente antes de poder insertar artículos.
        if (_sesion.UsuarioActual?.PuedeVerTodosLosLocales == true)
        {
            _idLocalActual     = 0;
            _nombreLocalActual = "";
        }
        else
        {
            _idLocalActual     = _sesion.LocalActual?.IdLocal    ?? 1;
            _nombreLocalActual = _sesion.LocalActual?.NombreLocal ?? "Local 1";
        }
        Title   = "Nueva Compra"; Width = 1020; Height = 650;
        MinWidth = 860; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrPrimary;
        BuildUI();
    }

    private void BuildUI()
    {
        var brFondo    = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 247, 250));
        var brCard     = System.Windows.Media.Brushes.White;
        var brAccent   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xC3, 0xF7));
        var brInputBg  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 251, 253));
        var brSep      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));

        var root = new Grid { Background = brFondo };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // panel artículo
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ══════════════════════════════════════════════════════════════════
        // HEADER — barra oscura con título, búsqueda y botones de acción
        // ══════════════════════════════════════════════════════════════════
        var header = new Border {
            Background = BrPrimDark,
            Padding = new Thickness(16, 0, 16, 0),
            Height = 52
        };
        var hGrid = new Grid();
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // ícono + título
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // búsqueda
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // botones

        // Ícono + título
        var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,24,0) };
        titleSp.Children.Add(new TextBlock { Text = "🛒", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
        var titleCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleCol.Children.Add(new TextBlock { Text = "Nueva Compra", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = BrBlanco });
        titleCol.Children.Add(new TextBlock { Text = "Registrar ingreso de mercadería", FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7F,0xB3,0xD3)) });
        titleSp.Children.Add(titleCol);
        Grid.SetColumn(titleSp, 0); hGrid.Children.Add(titleSp);

        // Búsqueda central
        var busqSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var busqBorder = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40,255,255,255)),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10,0,4,0),
            Height = 36
        };
        var busqInner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        busqInner.Children.Add(new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 13, Foreground = brAccent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        _txtBuscarArt = new TextBox { Width = 160, FontSize = 12, Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Foreground = BrBlanco, CaretBrush = BrBlanco,
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtBuscarArt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticulo(); };
        var phBusq = new TextBlock { Text = "Código del artículo…", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120,255,255,255)),
            FontSize = 12, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,0) };
        var busqStack = new Grid();
        busqStack.Children.Add(phBusq);
        busqStack.Children.Add(_txtBuscarArt);
        _txtBuscarArt.TextChanged += (_, _) => phBusq.Visibility = string.IsNullOrEmpty(_txtBuscarArt.Text) ? Visibility.Visible : Visibility.Collapsed;
        busqInner.Children.Add(busqStack);
        var btnBA = new Button { Content = "Buscar", Height = 28, Padding = new Thickness(12,0,12,0), Margin = new Thickness(6,0,0,0),
            Background = BrPrimary, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 11, Cursor = Cursors.Hand };
        btnBA.Click += async (_, _) => await BuscarArticulo();
        busqInner.Children.Add(btnBA);
        busqBorder.Child = busqInner;
        busqSp.Children.Add(busqBorder);

        // Nombre artículo seleccionado
        _lblNombreArt = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = brAccent, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16,0,0,0), MaxWidth = 380,
            TextTrimming = TextTrimming.CharacterEllipsis };
        busqSp.Children.Add(_lblNombreArt);
        Grid.SetColumn(busqSp, 1); hGrid.Children.Add(busqSp);

        // Botones derecha
        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Button MkHBtn(string icon, string label) => new Button {
            Height = 34, Padding = new Thickness(12,0,12,0), Margin = new Thickness(6,0,0,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50,255,255,255)),
            Foreground = BrBlanco, BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60,255,255,255)),
            FontWeight = FontWeights.SemiBold, FontSize = 11, Cursor = Cursors.Hand,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = icon, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        var btnListaArt = MkHBtn("", "Ver Listado de Artículos");
        var btnNuevoArt = MkHBtn("", "Nuevo Artículo");
        btnListaArt.Click += async (_, _) => await AbrirBuscadorArticulo();
        btnNuevoArt.Click += (_, _) => { new CrediSoft.UI.Views.Maestros.ArticulosWindow { Owner = this }.ShowDialog(); };
        btnsSp.Children.Add(btnListaArt);
        btnsSp.Children.Add(btnNuevoArt);
        Grid.SetColumn(btnsSp, 2); hGrid.Children.Add(btnsSp);

        header.Child = hGrid;
        Grid.SetRow(header, 0); root.Children.Add(header);

        // ══════════════════════════════════════════════════════════════════
        // PANEL ARTÍCULO — precios en cards + cantidad + insertar
        // ══════════════════════════════════════════════════════════════════
        var artPanel = new Border {
            Background = brCard,
            BorderBrush = brSep, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var artGrid = new Grid();
        artGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // precios
        artGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });                    // separador
        artGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });                   // cantidad + insertar

        // ── Columna izquierda: precios en dos filas ──────────────────────
        var preciosCol = new StackPanel();

        // Fila precios principales: Costo | Venta | Contado | badges
        var precMainGrid = new Grid();
        precMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        precMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        precMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        precMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        UIElement MkPrecioCard(string label, string color, out TextBox tb)
        {
            var accentBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            var card = new Border {
                Background = brInputBg, BorderBrush = brSep,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0,0,6,0), Padding = new Thickness(8,6,8,6)
            };
            var col = new StackPanel();
            col.Children.Add(new Border {
                Width = 22, Height = 2, CornerRadius = new CornerRadius(2),
                Background = accentBrush, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0,0,0,3)
            });
            col.Children.Add(new TextBlock { Text = label, FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = BrLabel, Margin = new Thickness(0,0,0,2) });
            var tbLocal = new TextBox { FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = accentBrush, Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0), TextAlignment = TextAlignment.Left,
                Padding = new Thickness(0) };
            tbLocal.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            DataObject.AddPastingHandler(tbLocal, (_, e) => {
                if (e.DataObject.GetDataPresent(typeof(string))) {
                    var t = (string)e.DataObject.GetData(typeof(string));
                    if (!t.All(char.IsDigit)) e.CancelCommand();
                } else e.CancelCommand();
            });
            tbLocal.TextChanged += (_, _) => {
                FormatearMiles(tbLocal);
                if (!_cargandoPreciosProgramatico) _preciosEditadosManualmente = true;
            };
            tb = tbLocal;
            col.Children.Add(tbLocal);
            card.Child = col;
            return card;
        }

        var cardCosto   = MkPrecioCard("PRECIO COSTO",   "#0E2F44", out _txtPC);
        var cardVenta   = MkPrecioCard("PRECIO VENTA",   "#1A4F6E", out _txtPV);
        var cardContado = MkPrecioCard("PRECIO CONTADO", "#1F6089", out _txtContado);

        // Columna 0: solo card Costo (sin %)
        Grid.SetColumn(cardCosto, 0); precMainGrid.Children.Add(cardCosto);

        // Columna 1: card Venta + % debajo alineado
        var colVenta = new StackPanel { Margin = new Thickness(0,0,6,0) };
        colVenta.Children.Add(cardVenta);
        Grid.SetColumn(colVenta, 1); precMainGrid.Children.Add(colVenta);

        // Columna 2: card Contado + % debajo alineado
        var colContado = new StackPanel { Margin = new Thickness(0,0,6,0) };
        colContado.Children.Add(cardContado);
        Grid.SetColumn(colContado, 2); precMainGrid.Children.Add(colContado);

        // Badges históricos a la derecha
        var badgesSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Border MkBadge(string icon, string titulo, out TextBlock lblVal, System.Windows.Media.Color bg)
        {
            var b = new Border {
                Background = new System.Windows.Media.SolidColorBrush(bg),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(8,6,8,6),
                Margin = new Thickness(3,0,0,0), MinWidth = 100, Cursor = Cursors.Hand
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = $"{icon}  {titulo}", FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180,255,255,255)) });
            lblVal = new TextBlock { Text = "—", FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = BrBlanco, Margin = new Thickness(0,3,0,0) };
            sp.Children.Add(lblVal);
            b.Child = sp;
            return b;
        }
        var badgeUC = MkBadge("📦", "ÚLT. COMPRA", out _lblUCFecha, System.Windows.Media.Color.FromRgb(0x0E,0x2F,0x44));
        var badgeUV = MkBadge("💰", "ÚLT. VENTA",  out _lblUVFecha, System.Windows.Media.Color.FromRgb(0x15,0x43,0x60));
        var badgeMP = MkBadge("✏️", "MOD. PRECIO", out _lblMPFecha, System.Windows.Media.Color.FromRgb(0x1F,0x60,0x89));
        badgesSp.Children.Add(badgeUC);
        badgesSp.Children.Add(badgeUV);
        badgesSp.Children.Add(badgeMP);
        Grid.SetColumn(badgesSp, 3); precMainGrid.Children.Add(badgesSp);

        // Inputs % — se insertan directamente en colVenta y colContado para alineación perfecta
        UIElement MkPctInput(string hint, out TextBox pctTxt, TextBox precioTxt)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sp.Children.Add(new TextBlock {
                Text = "% sobre costo →", FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120,140,160)),
                Margin = new Thickness(0,0,0,2)
            });
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            pctTxt = new TextBox {
                Text = "0", Height = 26, TextAlignment = TextAlignment.Right,
                Padding = new Thickness(6,0,6,0), VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240,248,255)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(144,190,240)),
                BorderThickness = new Thickness(1),
                ToolTip = hint
            };
            pctTxt.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');

            var sufijo = new Border {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210,230,250)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(144,190,240)),
                BorderThickness = new Thickness(0,1,1,1), Padding = new Thickness(5,0,5,0)
            };
            sufijo.Child = new TextBlock {
                Text = "%", FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21,101,192)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pctTxt, 0); row.Children.Add(pctTxt);
            Grid.SetColumn(sufijo, 1); row.Children.Add(sufijo);
            sp.Children.Add(row);

            var localPct = pctTxt; var localPrecio = precioTxt;
            localPct.TextChanged += (_, __) => {
                if (_recalcPctC) return;
                if (!decimal.TryParse(_txtPC.Text.Replace(".", "").Replace(",", ""), out var costo) || costo == 0) return;
                if (!decimal.TryParse(localPct.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct)) return;
                _recalcPctC = true;
                localPrecio.Text = Math.Round(costo * (1 + pct / 100m), 0).ToString("N0",
                    System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                _recalcPctC = false;
            };
            localPrecio.TextChanged += (_, __) => {
                if (_recalcPctC) return;
                if (!decimal.TryParse(_txtPC.Text.Replace(".", "").Replace(",", ""), out var costo) || costo == 0) return;
                if (!decimal.TryParse(localPrecio.Text.Replace(".", "").Replace(",", ""), out var precio)) return;
                _recalcPctC = true;
                localPct.Text = Math.Round((precio - costo) / costo * 100m, 1)
                    .ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                _recalcPctC = false;
            };
            return sp;
        }

        colVenta.Children.Add(MkPctInput("% ganancia → Precio Venta",   out _txtPctPV,      _txtPV));
        colContado.Children.Add(MkPctInput("% ganancia → Precio Contado", out _txtPctContado, _txtContado));

        preciosCol.Children.Add(precMainGrid);

        // Fila promoción — discreta, separada
        var promoCard = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,251,235)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(253,230,138)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10,6,10,6), Margin = new Thickness(0,8,0,0)
        };
        var promoInner = new StackPanel { Orientation = Orientation.Horizontal };
        promoInner.Children.Add(new TextBlock { Text = "🏷️", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        var promoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,16,0) };
        promoCol.Children.Add(new TextBlock { Text = "PRECIO PROMOCIÓN", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(146,64,14)) });
        // Fondo blanco propio (antes Transparent) — dentro de la tarjeta amarilla se mimetizaba
        // por completo con el fondo del contenedor, sin ninguna señal visual de que era un
        // campo editable y no solo texto informativo (feedback real: "no se identifica que es
        // editable"). Mismo criterio que el resto de los inputs de precio de esta pantalla
        // (MkPrecioCard), que también usan fondo blanco propio sobre su tarjeta de color.
        _txtPPromo = new TextBox { Width = 120, FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(146,64,14)),
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(253,230,138)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6,3,6,3) };
        _txtPPromo.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        DataObject.AddPastingHandler(_txtPPromo, (_, e) => {
            if (e.DataObject.GetDataPresent(typeof(string))) {
                var t = (string)e.DataObject.GetData(typeof(string));
                if (!t.All(char.IsDigit)) e.CancelCommand();
            } else e.CancelCommand();
        });
        _txtPPromo.TextChanged += (_, _) => {
            FormatearMiles(_txtPPromo);
            if (!_cargandoPreciosProgramatico) _preciosEditadosManualmente = true;
        };
        promoCol.Children.Add(_txtPPromo);
        // Mismo "% sobre costo" que ya tienen P.Venta/P.Contado — pedido explícito: se usa
        // como mini-calculadora al cargar precios, y faltaba para Promoción (antes solo se
        // podía tipear el monto final a mano, sin ver a qué % sobre el costo equivalía).
        // Reutiliza MkPctInput tal cual (misma fórmula/comportamiento bidireccional), la única
        // diferencia es el TextBox de precio que recibe (_txtPPromo en vez de _txtPV/Contado).
        promoCol.Children.Add(MkPctInput("% ganancia → Precio Promoción", out _txtPctPromo, _txtPPromo));
        promoInner.Children.Add(promoCol);
        promoInner.Children.Add(new TextBlock {
            Text = "Completar solo si el artículo tiene precio especial de promoción. Dejar en 0 si no aplica.",
            FontSize = 10, FontStyle = FontStyles.Italic,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161,98,7)),
            VerticalAlignment = VerticalAlignment.Center
        });
        promoCard.Child = promoInner;
        preciosCol.Children.Add(promoCard);

        Grid.SetColumn(preciosCol, 0); artGrid.Children.Add(preciosCol);

        // Separador vertical
        Grid.SetColumn(new Border { Background = brSep, Width = 1, Margin = new Thickness(12,0,12,0) }, 1);
        artGrid.Children.Add(new Border { Background = brSep, Width = 1, Margin = new Thickness(12,0,12,0) });
        Grid.SetColumn(artGrid.Children[artGrid.Children.Count-1], 1);

        // ── Columna derecha: cantidad + local + insertar ─────────────────
        var accionCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        // Cantidad
        var cantCard = new Border {
            Background = brInputBg, BorderBrush = brSep,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,0,8)
        };
        var cantInner = new StackPanel();
        cantInner.Children.Add(new TextBlock { Text = "CANTIDAD A COMPRAR", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, Margin = new Thickness(0,0,0,6) });
        _txtCantidad = new TextBox { FontSize = 22, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = BrPrimDark, Padding = new Thickness(0) };
        _txtCantidad.Text = "1";
        _txtCantidad.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await InsercionRapidaAsync(); };
        cantInner.Children.Add(_txtCantidad);
        cantCard.Child = cantInner;
        accionCol.Children.Add(cantCard);

        // Local
        var localCard = new Border {
            Background = brInputBg, BorderBrush = brSep,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,0,10)
        };
        var localInner = new StackPanel();
        localInner.Children.Add(new TextBlock { Text = "LOCAL DE DESTINO", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, Margin = new Thickness(0,0,0,4) });
        _lblLocalPrefijo = new TextBlock(); // compatibilidad
        var btnSelLocal = new Button {
            Height = 30, Padding = new Thickness(10,0,10,0),
            Background = BrPrimary, Foreground = BrBlanco,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 11, FontWeight = FontWeights.SemiBold
        };
        _lblLocalNombre = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _lblLocalNombre.Text = _idLocalActual == 0
            ? "⚠  Seleccionar local..."
            : $"📍  {_nombreLocalActual}";
        btnSelLocal.Content = _lblLocalNombre;
        btnSelLocal.Click += async (_, _) => await SeleccionarLocal(btnSelLocal);
        localInner.Children.Add(btnSelLocal);
        localCard.Child = localInner;
        accionCol.Children.Add(localCard);

        // Botón INSERTAR
        _btnInsertar = new Button {
            Height = 44, FontSize = 14, FontWeight = FontWeights.Bold,
            Background = BrVerde, Foreground = BrBlanco,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) },
                new TextBlock { Text = "INSERTAR", VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        _btnInsertar.Click += async (_, _) => await InsercionRapidaAsync();
        accionCol.Children.Add(_btnInsertar);

        Grid.SetColumn(accionCol, 2); artGrid.Children.Add(accionCol);

        artPanel.Child = artGrid;
        Grid.SetRow(artPanel, 1); root.Children.Add(artPanel);

        // ══════════════════════════════════════════════════════════════════
        // GRID DETALLE
        // ══════════════════════════════════════════════════════════════════
        // Hint visible arriba de la grilla — antes la única forma de editar un ítem ya
        // insertado era doble click, sin ninguna pista en pantalla de que eso era posible.
        // Pedido explícito: texto claro + íconos en la fila (no solo depender del gesto).
        var hintGrid = new TextBlock
        {
            Text = "✎  Doble click en un artículo para editar cantidad y precios, o use los íconos de la columna Acciones.",
            FontSize = 10.5, FontStyle = FontStyles.Italic, Foreground = BrLabel,
            Margin = new Thickness(0, 0, 0, 4)
        };
        var gridConHint = new DockPanel();
        DockPanel.SetDock(hintGrid, Dock.Top); gridConHint.Children.Add(hintGrid);

        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = brSep,
            RowBackground = brCard,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248,250,252)),
            FontSize = 12, RowHeight = 40, ColumnHeaderHeight = 36,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = BuildGridHeaderStyle(),
            Margin = new Thickness(0)
        };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",         Binding = new System.Windows.Data.Binding("Codigo"),         Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción",    Binding = new System.Windows.Data.Binding("Descripcion"),    Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",          Binding = new System.Windows.Data.Binding("Cantidad"),       Width = 65 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Costo",       Binding = new System.Windows.Data.Binding("PrecioCostoFmt"), Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Venta",       Binding = new System.Windows.Data.Binding("PrecioVentaFmt"), Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Contado",     Binding = new System.Windows.Data.Binding("ContadoFmt"),     Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. Promo",       Binding = new System.Windows.Data.Binding("PPromoFmt"),      Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Subtotal Costo", Binding = new System.Windows.Data.Binding("SubtotalFmt"),    Width = 120 });
        _gridDetalle.Columns.Add(BuildColumnaAccionesRapida());
        _gridDetalle.MouseDoubleClick += OnGridDblClick;
        _gridDetalle.KeyDown += (_, e) => { if (e.Key == Key.Delete || e.Key == Key.Back) QuitarSeleccionado(); };
        _gridDetalle.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => ActualizarTotal();
        DockPanel.SetDock(_gridDetalle, Dock.Bottom); gridConHint.Children.Add(_gridDetalle);
        Grid.SetRow(gridConHint, 2); root.Children.Add(gridConHint);

        // ══════════════════════════════════════════════════════════════════
        // FOOTER
        // ══════════════════════════════════════════════════════════════════
        var footer = new Border {
            Background = BrPrimDark,
            Padding = new Thickness(16, 10, 16, 10)
        };
        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Total + atajos
        var footLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _lblTotal = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        var lblAtajos = new TextBlock {
            Text = "F2: Buscar artículo   F7: Insertar   Doble click: Editar ítem   Ctrl+N: Nuevo artículo   Supr/Del: Excluir   F5: Guardar   Ctrl+S: Cerrar",
            FontSize = 9, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7F,0xB3,0xD3)),
            Margin = new Thickness(0,3,0,0)
        };
        footLeft.Children.Add(_lblTotal);
        footLeft.Children.Add(lblAtajos);
        Grid.SetColumn(footLeft, 0); footGrid.Children.Add(footLeft);

        // Botones de acción
        var footBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var btnAceptar = new Button {
            Height = 38, Padding = new Thickness(24,0,24,0), Margin = new Thickness(0,0,8,0),
            Background = BrVerde, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, FontSize = 13, Cursor = Cursors.Hand,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) },
                new TextBlock { Text = "Confirmar Compra", VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        var btnCerrar = new Button {
            Height = 38, Padding = new Thickness(20,0,20,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)),
            Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 13, Cursor = Cursors.Hand, Content = "Cerrar"
        };
        btnAceptar.Click += (_, _) => AbrirModalConfirmacion();
        btnCerrar.Click  += (_, _) => Close();
        footBtns.Children.Add(btnAceptar);
        footBtns.Children.Add(btnCerrar);
        Grid.SetColumn(footBtns, 1); footGrid.Children.Add(footBtns);

        footer.Child = footGrid;
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        Content = root;
        ActualizarTotal();

        KeyDown += async (_, e) => {
            if (e.Key == Key.F2) { _txtBuscarArt.Focus(); _txtBuscarArt.SelectAll(); }
            else if (e.Key == Key.F7) await InsercionRapidaAsync();
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
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0E, 0x2F, 0x44))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7F, 0xB3, 0xD3))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.0));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(12, 0, 12, 0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x4F, 0x6E))));
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

            // Artículo nuevo: sí corresponde reflejar sus precios reales, sin importar si el
            // artículo ANTERIOR tenía ediciones manuales pendientes (ver
            // _preciosEditadosManualmente / RecargarPreciosLocal).
            _preciosEditadosManualmente = false;
            _idArtActual = (int)art.IDART;
            _caActual    = (string)art.CA;
            _descActual  = (string)art.D;

            var precio = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT IDPRICES, PC, PVENTA, CONTADO, PPROMO, FCOMPRA, FVENTA, FMP FROM PRICES WHERE IDART=@Id AND IDLOCAL=@L AND DELETADO=0",
                new { Id = _idArtActual, L = idlocal });

            _cargandoPreciosProgramatico = true;
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
            _cargandoPreciosProgramatico = false;

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
    private async Task InsercionRapidaAsync()
    {
        if (_idLocalActual == 0)
        {
            MessageBox.Show("Seleccione el local de destino antes de insertar un artículo.",
                "Local requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_idArtActual == 0) { MessageBox.Show("Primero busque un artículo."); return; }
        if (!decimal.TryParse(_txtCantidad.Text.Replace(",","").Replace(".",""), out var cant) || cant <= 0)
            { MessageBox.Show("Ingrese una cantidad válida."); return; }
        if (!decimal.TryParse(_txtPC.Text.Replace(",","").Replace(".",""), out var pc))    pc = 0;
        if (!decimal.TryParse(_txtPV.Text.Replace(",","").Replace(".",""), out var pv))    pv = 0;
        if (!decimal.TryParse(_txtContado.Text.Replace(",","").Replace(".",""), out var co)) co = 0;
        if (!decimal.TryParse(_txtPPromo.Text.Replace(",","").Replace(".",""), out var pp))  pp = 0;

        // Sin precio en el local de destino elegido para ESTA compra — alertar si existe
        // precio cargado en otro local, caso real: artículo activo con precio 0 en 14 de 15
        // locales, nadie se enteraba hasta que un cliente reclamaba (TK886, LU-5004).
        if (pc == 0 && pv == 0)
        {
            var idArtParaAlerta = _idArtActual;
            var resultado = await OfrecerIgualarPreciosAsync(idArtParaAlerta, _caActual, _descActual, _idLocalActual);
            if (resultado != null)
            {
                pc = resultado.PrecioCosto; pv = resultado.PrecioVenta;
                co = resultado.Contado;     pp = resultado.PPromo;
            }
        }

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
        _preciosEditadosManualmente = false; // artículo insertado al detalle, campos limpios para el próximo
        _txtBuscarArt.Text = ""; _txtCantidad.Text = "1";
        _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "";
        _lblNombreArt.Text = "";
        _lblUCFecha.Text = "—";
        _lblUVFecha.Text = "—";
        _lblMPFecha.Text = "—";
        _btnInsertar.IsEnabled = false;
        _txtBuscarArt.Focus();
    }

    private async Task<IgualarPreciosDialogResult?> OfrecerIgualarPreciosAsync(int idArt, string codigo, string descripcion, int idLocalDestino)
    {
        try
        {
            var repo = App.Services.GetRequiredService<IArticuloRepository>();
            var precios = (await repo.ObtenerStockTodosLocalesAsync(idArt))
                .Where(p => p.Pventa > 0 || p.Pc > 0)
                .ToList();
            if (precios.Count == 0) return null; // no hay precio cargado en ningún lado, nada que ofrecer

            using var conn = _db.Create();
            var todosLocales = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();

            var idUsuario = _sesion.UsuarioActual?.IdUsuario ?? 0;
            var nomMaquina = Environment.MachineName;

            var dlg = new IgualarPreciosDialog(repo, idArt, codigo, descripcion,
                precios, todosLocales, idLocalDestino, idUsuario, nomMaquina) { Owner = this };
            dlg.ShowDialog();

            return dlg.Resultado.Aplicado ? dlg.Resultado : null;
        }
        catch (Exception ex)
        {
            // No bloquear la carga de la compra si falla la sugerencia de precios —
            // el artículo ya se insertó con precio 0, el usuario puede seguir igual.
            MessageBox.Show($"No se pudo verificar precios en otros locales: {ex.Message}",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void QuitarSeleccionado()
    {
        if (_gridDetalle.SelectedItem is LineaCompra lc) _items.Remove(lc);
    }

    // Doble click en una línea ya insertada — antes solo permitía cambiar la cantidad, no
    // había forma de corregir el precio de un ítem sin quitarlo y volver a buscarlo. Pedido
    // explícito tras probar el flujo de "Igualar precios": una vez insertado el artículo (con
    // el precio elegido en ese modal, o incluso en 0 si se omitió), sigue haciendo falta poder
    // ajustarlo a mano ahí mismo.
    private async void OnGridDblClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_gridDetalle.SelectedItem is not LineaCompra lc) return;
        await EditarItemSeleccionado(lc);
    }

    private async Task EditarItemSeleccionado(LineaCompra lc)
    {
        // Cargado por adelantado (no recién al tildar el checkbox) para que el panel de
        // locales se expanda al instante — pedido explícito: "al hacer click en el checkbox
        // ya se despliegue los locales debajo", sin esperar una consulta en el medio.
        List<LocalItem> todosLocales;
        try
        {
            using var conn = _db.Create();
            todosLocales = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();
        }
        catch { todosLocales = new List<LocalItem>(); }

        var brFondo   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 247, 250));
        var brBorde2  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
        var brAmbar   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9));
        var brAmbarBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 251, 235));
        var brAmbarBd = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(253, 224, 171));

        var dlg = new Window { Title = "Editar ítem", Width = 480, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = brFondo };

        var root = new DockPanel();

        // ── Header con nombre del artículo, destacado ──────────────────────
        var hdr = new Border { Background = BrPrimary, Padding = new Thickness(18, 14, 18, 14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = lc.Codigo, FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 210, 230)) });
        hdrSp.Children.Add(new TextBlock { Text = lc.Descripcion, FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var body = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };

        decimal Parse(TextBox tb)
        {
            decimal.TryParse(tb.Text.Replace(",", "").Replace(".", ""), out var v);
            return v;
        }
        TextBox MkInput(decimal valor) => new TextBox
        {
            Text = valor.ToString("N0"), Padding = new Thickness(8, 6, 8, 6),
            FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right,
            Background = BrBlanco, BorderBrush = brBorde2, BorderThickness = new Thickness(1)
        };
        TextBlock MkLbl(string t) => new TextBlock { Text = t, FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 4) };

        // ── Cantidad, en su propia fila (es lo primero que se ajusta y lo más distinto en
        // naturaleza de los 4 precios que vienen después) ──────────────────
        var cantSp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        cantSp.Children.Add(MkLbl("CANTIDAD"));
        var txtQ = MkInput(lc.Cantidad);
        txtQ.Width = 120; txtQ.HorizontalAlignment = HorizontalAlignment.Left;
        cantSp.Children.Add(txtQ);
        body.Children.Add(cantSp);

        // ── Precios en grilla 2x2 — antes eran 4 campos apilados sin agrupar, indistintos
        // entre sí y sin jerarquía; acá quedan agrupados como "un bloque de precios". Venta/
        // Contado/Promo llevan además un mini "% sobre costo" con cálculo bidireccional (mismo
        // patrón ya usado al insertar el artículo por primera vez y en Editar Compras) —
        // pedido explícito: acá también hacía falta poder ajustar por porcentaje, no solo a
        // mano el valor final. ──
        body.Children.Add(MkLbl("PRECIOS"));
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var recalcPct = false;
        var txtPC = MkInput(lc.PrecioCosto);

        decimal ParseFlexible(string texto)
        {
            decimal.TryParse(texto.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v);
            return v;
        }

        UIElement MkCeldaConPct(string etiqueta, decimal valor, int col, int row, out TextBox tbPrecio)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = etiqueta, FontSize = 10, Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3) });
            var precioLocal = MkInput(valor);
            sp.Children.Add(precioLocal);

            var pctRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            pctRow.Children.Add(new TextBlock { Text = "%↑costo", FontSize = 9, Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var pctLocal = new TextBox
            {
                Width = 55, Height = 22, FontSize = 10.5, TextAlignment = TextAlignment.Right,
                Padding = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 248, 255)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 190, 240)),
                BorderThickness = new Thickness(1),
                ToolTip = "% de ganancia sobre el precio costo — escribí acá o en el precio, se calculan entre sí"
            };
            pctLocal.PreviewTextInput += (_, ev) => ev.Handled = !ev.Text.All(c => char.IsDigit(c) || c == '.' || c == '-');
            pctRow.Children.Add(pctLocal);
            pctRow.Children.Add(new TextBlock { Text = "%", FontSize = 10.5, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 101, 192)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) });
            sp.Children.Add(pctRow);

            pctLocal.TextChanged += (_, __) =>
            {
                if (recalcPct) return;
                var costo = Parse(txtPC);
                if (costo == 0) return;
                var pct = ParseFlexible(pctLocal.Text);
                recalcPct = true;
                precioLocal.Text = Math.Round(costo * (1 + pct / 100m), 0).ToString("N0");
                recalcPct = false;
            };
            precioLocal.TextChanged += (_, __) =>
            {
                if (recalcPct) return;
                var costo = Parse(txtPC);
                if (costo == 0) return;
                var precio = Parse(precioLocal);
                recalcPct = true;
                pctLocal.Text = Math.Round((precio - costo) / costo * 100m, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                recalcPct = false;
            };
            // % inicial a partir del valor ya cargado — sin esto arrancaba siempre en "0%"
            // aunque el precio ya tuviera un margen real sobre el costo.
            var costoInicial = lc.PrecioCosto;
            if (costoInicial != 0)
                pctLocal.Text = Math.Round((valor - costoInicial) / costoInicial * 100m, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            Grid.SetColumn(sp, col); Grid.SetRow(sp, row);
            tbPrecio = precioLocal;
            return sp;
        }

        var celdaCosto = new StackPanel();
        celdaCosto.Children.Add(new TextBlock { Text = "Costo", FontSize = 10, Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3) });
        celdaCosto.Children.Add(txtPC);
        Grid.SetColumn(celdaCosto, 0); Grid.SetRow(celdaCosto, 0);
        grid.Children.Add(celdaCosto);

        grid.Children.Add(MkCeldaConPct("Venta (crédito)", lc.PrecioVenta, 2, 0, out var txtPV));
        grid.Children.Add(MkCeldaConPct("Contado",         lc.Contado,     0, 2, out var txtContado));
        grid.Children.Add(MkCeldaConPct("Promo",           lc.PPromo,      2, 2, out var txtPPromo));
        body.Children.Add(grid);

        // ── Sección aparte, con fondo propio, para la decisión de tocar el maestro de
        // precios — antes era un checkbox perdido al final de una columna de inputs, con el
        // mismo peso visual que cualquier otro campo, pese a ser una decisión de alcance
        // distinto (esta compra vs. TODO el sistema). Riesgo real señalado: alguien tilda o
        // destilda sin notar la diferencia. Sin tildar por defecto — el comportamiento
        // original de este editor (solo esta compra) sigue siendo lo que pasa si no se toca.
        var cajaMaestro = new Border
        {
            Background = brAmbarBg, BorderBrush = brAmbarBd, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cajaMaestroSp = new StackPanel();
        var chkActualizarMaestro = new CheckBox
        {
            FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = brAmbar,
        };
        chkActualizarMaestro.Content = new TextBlock { Text = "También actualizar este precio en el sistema", TextWrapping = TextWrapping.Wrap };
        cajaMaestroSp.Children.Add(chkActualizarMaestro);
        cajaMaestroSp.Children.Add(new TextBlock
        {
            Text = "Sin marcar, este ajuste queda solo en esta compra. Al marcarlo, elegí abajo en qué locales aplicar el mismo precio.",
            FontSize = 10.5, Foreground = brAmbar, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 3, 0, 0)
        });

        // Panel de locales — colapsado hasta que se tilde el checkbox, ya cargado de
        // antemano así se despliega al instante sin esperar ninguna consulta.
        var chkLocales = new List<CheckBox>();
        var panelLocales = new StackPanel { Margin = new Thickness(24, 10, 0, 0), Visibility = Visibility.Collapsed };
        var wrapLocales = new WrapPanel();
        foreach (var local in todosLocales)
        {
            var chkLoc = new CheckBox
            {
                Content = local.Nombre, Tag = local.IdLocal,
                IsChecked = local.IdLocal == _idLocalActual,
                FontSize = 11, Foreground = brAmbar,
                Margin = new Thickness(0, 0, 14, 6)
            };
            chkLocales.Add(chkLoc);
            wrapLocales.Children.Add(chkLoc);
        }
        panelLocales.Children.Add(wrapLocales);
        cajaMaestroSp.Children.Add(panelLocales);

        chkActualizarMaestro.Checked   += (_, __) => panelLocales.Visibility = Visibility.Visible;
        chkActualizarMaestro.Unchecked += (_, __) => panelLocales.Visibility = Visibility.Collapsed;

        cajaMaestro.Child = cajaMaestroSp;
        body.Children.Add(cajaMaestro);

        DockPanel.SetDock(body, Dock.Top); root.Children.Add(body);

        // ── Footer con botones ──────────────────────────────────────────────
        var footer = new Border { Background = BrBlanco, BorderBrush = brBorde2, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(18, 12, 18, 12) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCancelar = MakeBtn("Cancelar", BrGris);
        btnCancelar.Click += (_, _) => dlg.Close();
        var btnOk = MakeBtn("✓  Aceptar", BrVerde);
        btnOk.Click += async (_, _) => {
            var q = Parse(txtQ);
            if (q <= 0) { MessageBox.Show("Ingrese una cantidad válida."); return; }
            var pc = Parse(txtPC); var pv = Parse(txtPV); var co = Parse(txtContado); var pp = Parse(txtPPromo);
            lc.Cantidad    = q;
            lc.PrecioCosto = pc;
            lc.PrecioVenta = pv;
            lc.Contado     = co;
            lc.PPromo      = pp;
            lc.Subtotal    = q * lc.PrecioCosto;
            _gridDetalle.Items.Refresh(); ActualizarTotal();

            if (chkActualizarMaestro.IsChecked == true)
            {
                var idsLocal = chkLocales.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToList();
                if (idsLocal.Count == 0)
                {
                    MessageBox.Show("Marque al menos un local para actualizar el precio en el sistema, o destilde la opción.",
                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    var repo = App.Services.GetRequiredService<IArticuloRepository>();
                    var idUsuario = _sesion.UsuarioActual?.IdUsuario ?? 0;
                    foreach (var idLocal in idsLocal)
                        await repo.ActualizarPreciosAsync(lc.IdArt, idLocal, pc, pv, co, pp, idUsuario, Environment.MachineName);
                    MessageBox.Show($"Precio actualizado en {idsLocal.Count} local(es).", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar precios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            dlg.Close();
        };
        footSp.Children.Add(btnCancelar); footSp.Children.Add(btnOk);
        footer.Child = footSp;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        dlg.Content = root;
        dlg.Loaded += (_, _) => { txtQ.Focus(); txtQ.SelectAll(); };
        dlg.ShowDialog();
    }

    // Mismo patrón visual que EditarComprasWindow.BuildBotonCircularTemplate — botón redondo
    // con ícono Segoe MDL2, Template propio porque sin reemplazar el chrome nativo de Button,
    // WPF no lo centra bien en tamaños chicos (se ve "aplastado"). Duplicado acá (no
    // compartido) porque el original es privado a esa otra clase.
    private static ControlTemplate BuildBotonCircularTemplateRapida(
        string glifoSegoeMdl2, System.Windows.Media.Color colorBase, System.Windows.Media.Color colorHover)
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "PART_Border");
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(colorBase));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        borderFactory.SetValue(FrameworkElement.WidthProperty, 26.0);
        borderFactory.SetValue(FrameworkElement.HeightProperty, 26.0);

        // Sin FontFamily explícito (no "Segoe MDL2 Assets"): esos glifos son privados de esa
        // fuente de íconos del sistema y no se estaban renderizando en este contexto (se
        // veían como círculos vacíos, reportado real) — con caracteres Unicode comunes (✎/🗑)
        // y la fuente por defecto del control alcanza para que el ícono se vea siempre.
        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.TextProperty, glifoSegoeMdl2);
        iconFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        iconFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
        iconFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(iconFactory);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(colorHover), "PART_Border"));
        template.Triggers.Add(hoverTrigger);
        return template;
    }

    private DataGridTemplateColumn BuildColumnaAccionesRapida()
    {
        var templateEditar = BuildBotonCircularTemplateRapida("✎",
            System.Windows.Media.Color.FromRgb(0x1A, 0x4F, 0x6E),
            System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0xB8));
        var templateQuitar = BuildBotonCircularTemplateRapida("🗑",
            System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26),
            System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

        var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
        stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        stackFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        stackFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var btnEditarF = new FrameworkElementFactory(typeof(Button));
        btnEditarF.SetValue(Control.TemplateProperty, templateEditar);
        btnEditarF.SetValue(FrameworkElement.WidthProperty, 26.0);
        btnEditarF.SetValue(FrameworkElement.HeightProperty, 26.0);
        btnEditarF.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        btnEditarF.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        btnEditarF.SetValue(FrameworkElement.ToolTipProperty, "Editar cantidad y precios");
        btnEditarF.SetValue(Control.FocusableProperty, false);
        btnEditarF.AddHandler(Button.ClickEvent, new RoutedEventHandler(async (s, _) => {
            if ((s as Button)?.DataContext is LineaCompra item) { _gridDetalle.SelectedItem = item; await EditarItemSeleccionado(item); }
        }));
        stackFactory.AppendChild(btnEditarF);

        var btnQuitarF = new FrameworkElementFactory(typeof(Button));
        btnQuitarF.SetValue(Control.TemplateProperty, templateQuitar);
        btnQuitarF.SetValue(FrameworkElement.WidthProperty, 26.0);
        btnQuitarF.SetValue(FrameworkElement.HeightProperty, 26.0);
        btnQuitarF.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        btnQuitarF.SetValue(FrameworkElement.ToolTipProperty, "Quitar artículo de la compra");
        btnQuitarF.SetValue(Control.FocusableProperty, false);
        btnQuitarF.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) => {
            if ((s as Button)?.DataContext is LineaCompra item) { _items.Remove(item); ActualizarTotal(); }
        }));
        stackFactory.AppendChild(btnQuitarF);

        var template = new DataTemplate { VisualTree = stackFactory };
        return new DataGridTemplateColumn { Header = "Acciones", Width = 78, CellTemplate = template };
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

        var brFondoDlg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,247,250));
        var brCardDlg  = System.Windows.Media.Brushes.White;
        var brSepDlg   = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226,232,240));

        var dlg = new Window {
            Title = "Confirmar compra", Width = 560, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = brFondoDlg,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        // ── Helpers locales ───────────────────────────────────────────────
        Border MkCard(UIElement child, Thickness margin) => new Border {
            Background = brCardDlg, BorderBrush = brSepDlg, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14,10,14,10),
            Margin = margin,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, BlurRadius = 6, Opacity = 0.06, Color = System.Windows.Media.Colors.Black },
            Child = child
        };
        TextBlock Lbl(string t) => new TextBlock { Text = t, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0,0,0,4) };

        // ── Campos ────────────────────────────────────────────────────────
        var lblParcial  = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = BrPrimDark };
        lblParcial.Text = $"Gs. {total:N0}".Replace(",",".");

        var lblTotal    = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = BrVerde };
        lblTotal.Text   = $"Gs. {total:N0}".Replace(",",".");

        var txtDescuento = new TextBox { FontSize = 14, FontWeight = FontWeights.Bold,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = BrPrimDark, Padding = new Thickness(0) };
        txtDescuento.Text = "0";
        txtDescuento.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        DataObject.AddPastingHandler(txtDescuento, (_, e) => {
            if (e.DataObject.GetDataPresent(typeof(string))) {
                var t2 = (string)e.DataObject.GetData(typeof(string));
                if (!t2.All(char.IsDigit)) e.CancelCommand();
            } else e.CancelCommand();
        });
        txtDescuento.TextChanged += (_, _) => {
            decimal.TryParse(new string(txtDescuento.Text.Where(char.IsDigit).ToArray()), out var desc);
            var neto = total - desc; if (neto < 0) neto = 0;
            lblTotal.Text = $"Gs. {neto:N0}".Replace(",",".");
        };

        var cboMetodo = new ComboBox { FontSize = 12, BorderBrush = brSepDlg, Padding = new Thickness(6,5,6,5) };
        cboMetodo.Items.Add(new ComboBoxItem { Content = "💵  Efectivo",       Tag = (byte)1 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "🏦  Transferencia",  Tag = (byte)2 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "📄  Cheque",         Tag = (byte)3 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "💳  Tarjeta",        Tag = (byte)4 });
        cboMetodo.SelectedIndex = 0;

        var txtFactura = new TextBox { FontSize = 13, Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), Foreground = BrPrimDark };

        var txtNota = new TextBox { FontSize = 12, Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), Foreground = BrPrimDark };

        var lblProvNombre = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = BrPrimary, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10,0,0,0), MaxWidth = 260,
            TextTrimming = TextTrimming.CharacterEllipsis, Text = "Sin seleccionar" };
        int idProvModal = 0;

        // ── Layout root ───────────────────────────────────────────────────
        var dlgRoot = new Grid();
        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // body
        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ────────────────────────────────────────────────────────
        var dlgHeader = new Border { Background = BrPrimDark, Padding = new Thickness(20,16,20,16) };
        var dlgHGrid  = new Grid();
        dlgHGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dlgHGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dlgHGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Ícono
        var dlgIco = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50,255,255,255)),
            Margin = new Thickness(0,0,14,0), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "🛒", FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }};
        Grid.SetColumn(dlgIco, 0); dlgHGrid.Children.Add(dlgIco);

        // Título
        var dlgTitleCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        dlgTitleCol.Children.Add(new TextBlock { Text = "Confirmar Compra", FontSize = 15,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco });
        dlgTitleCol.Children.Add(new TextBlock {
            Text = $"{_items.Count} artículo(s)  ·  {_nombreLocalActual}",
            FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7F,0xB3,0xD3)),
            Margin = new Thickness(0,3,0,0) });
        Grid.SetColumn(dlgTitleCol, 1); dlgHGrid.Children.Add(dlgTitleCol);

        // Total en header
        var dlgTotalHdr = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        dlgTotalHdr.Children.Add(new TextBlock { Text = "TOTAL", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7F,0xB3,0xD3)),
            TextAlignment = TextAlignment.Right });
        dlgTotalHdr.Children.Add(lblTotal);
        Grid.SetColumn(dlgTotalHdr, 2); dlgHGrid.Children.Add(dlgTotalHdr);

        dlgHeader.Child = dlgHGrid;
        Grid.SetRow(dlgHeader, 0); dlgRoot.Children.Add(dlgHeader);

        // ── Body ──────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(16,14,16,10) };

        // Card 1: Montos — Parcial | Descuento | (Total en header)
        var montosGrid = new Grid();
        montosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        montosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        montosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var colParcial = new StackPanel();
        colParcial.Children.Add(Lbl("MONTO PARCIAL"));
        colParcial.Children.Add(lblParcial);
        Grid.SetColumn(colParcial, 0); montosGrid.Children.Add(colParcial);

        var colDesc = new StackPanel();
        colDesc.Children.Add(Lbl("DESCUENTO (Gs.)"));
        colDesc.Children.Add(txtDescuento);
        Grid.SetColumn(colDesc, 2); montosGrid.Children.Add(colDesc);

        body.Children.Add(MkCard(montosGrid, new Thickness(0,0,0,10)));

        // Card 2: Pago — Método | Factura en una fila
        var pagoGrid = new Grid();
        pagoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pagoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        pagoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pagoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pagoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var lblMetodo = Lbl("MÉTODO DE PAGO");
        var lblFact   = Lbl("N° FACTURA  *");
        Grid.SetColumn(lblMetodo, 0); Grid.SetRow(lblMetodo, 0); pagoGrid.Children.Add(lblMetodo);
        Grid.SetColumn(cboMetodo, 0); Grid.SetRow(cboMetodo, 1); pagoGrid.Children.Add(cboMetodo);
        Grid.SetColumn(lblFact,   2); Grid.SetRow(lblFact,   0); pagoGrid.Children.Add(lblFact);

        var factBorder = new Border { BorderBrush = brSepDlg, BorderThickness = new Thickness(0,0,0,1.5),
            Padding = new Thickness(0,4,0,4) };
        factBorder.Child = txtFactura;
        Grid.SetColumn(factBorder, 2); Grid.SetRow(factBorder, 1); pagoGrid.Children.Add(factBorder);

        body.Children.Add(MkCard(pagoGrid, new Thickness(0,0,0,10)));

        // Card 3: Proveedor
        var provSp = new StackPanel();
        provSp.Children.Add(Lbl("PROVEEDOR  *"));
        var provRow = new StackPanel { Orientation = Orientation.Horizontal };
        var btnBuscProv = new Button {
            Height = 32, Padding = new Thickness(12,0,12,0),
            Background = BrPrimary, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = "🏭", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) },
                new TextBlock { Text = "Seleccionar proveedor", VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        btnBuscProv.Click += (_, _) => {
            var modal = new BuscadorProveedorModal(_db) { Owner = dlg };
            if (modal.ShowDialog() == true && modal.ProveedorSeleccionado != null) {
                idProvModal = modal.ProveedorSeleccionado.IdProveedor;
                lblProvNombre.Text      = modal.ProveedorSeleccionado.Nombre;
                lblProvNombre.Foreground = BrVerde;
            }
        };
        provRow.Children.Add(btnBuscProv);
        provRow.Children.Add(lblProvNombre);
        provSp.Children.Add(provRow);
        body.Children.Add(MkCard(provSp, new Thickness(0,0,0,10)));

        // Card 4: Nota (opcional)
        var notaSp = new StackPanel();
        notaSp.Children.Add(Lbl("NOTA / OBSERVACIÓN  (opcional)"));
        var notaBorder = new Border { BorderBrush = brSepDlg, BorderThickness = new Thickness(0,0,0,1.5), Padding = new Thickness(0,4,0,4) };
        notaBorder.Child = txtNota;
        notaSp.Children.Add(notaBorder);
        body.Children.Add(MkCard(notaSp, new Thickness(0,0,0,4)));

        Grid.SetRow(body, 1); dlgRoot.Children.Add(body);

        // ── Footer ────────────────────────────────────────────────────────
        var dlgFooter = new Border {
            Background = brCardDlg, BorderBrush = brSepDlg,
            BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(16,12,16,12)
        };
        var footRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var btnCerrarModal = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(20,0,20,0), Margin = new Thickness(0,0,8,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)),
            Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12, Cursor = Cursors.Hand
        };
        var btnGuardar = new Button {
            Height = 36, Padding = new Thickness(24,0,24,0),
            Background = BrVerde, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, FontSize = 13, Cursor = Cursors.Hand,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) },
                new TextBlock { Text = "Guardar compra", VerticalAlignment = VerticalAlignment.Center }
            }}
        };

        btnCerrarModal.Click += (_, _) => dlg.Close();
        btnGuardar.Click += async (_, _) => {
            if (idProvModal == 0)                            { MessageBox.Show("Seleccione un proveedor.", "Requerido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(txtFactura.Text)) { MessageBox.Show("Ingrese el N° de factura.", "Requerido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var usuarioAutorizado = await MostrarPermisoUsuario(dlg);
            if (usuarioAutorizado == null) return;

            byte metodo = (cboMetodo.SelectedItem as ComboBoxItem)?.Tag is byte b2 ? b2 : (byte)1;
            decimal.TryParse(new string(txtDescuento.Text.Where(char.IsDigit).ToArray()), out var descuento);
            var totalFinal = total - descuento; if (totalFinal < 0) totalFinal = 0;

            dlg.Close();
            await EjecutarGuardado(idProvModal, txtFactura.Text.Trim(), txtNota.Text.Trim(), 1, metodo, total, totalFinal, descuento, usuarioAutorizado.IdUsuario, idlocal);
        };

        footRow.Children.Add(btnCerrarModal);
        footRow.Children.Add(btnGuardar);
        dlgFooter.Child = footRow;
        Grid.SetRow(dlgFooter, 2); dlgRoot.Children.Add(dlgFooter);

        dlg.Content = dlgRoot;
        dlg.Loaded += (_, _) => txtFactura.Focus();
        dlg.ShowDialog();
    }

    // ── Modal PERMISO DE USUARIOS ─────────────────────────────────────────
    private record UsuarioPermiso(int IdUsuario, string Nombre);

    private async Task<UsuarioPermiso?> MostrarPermisoUsuario(Window owner)
    {
        var r = await CrediSoft.UI.Views.Shared.PermisoUsuariosModal.MostrarAsync(owner, _db);
        return r == null ? null : new UsuarioPermiso(r.IdUsuario, r.Nombre);
    }

    // Guarda la compra en CAB_BUY_TMP / DET_BUY_TMP (pendiente de aprobación del admin).
    // El administrador confirma y mueve a CAB_BUYS desde EditarCompras usando JOGUAANETE_CS.
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
                p.Add("@AGENTE",        i == 0 ? "SI" : "NO");
                p.Add("@IDCABTMP",      0);
                p.Add("@FACTURA",       factura);
                p.Add("@PARCIAL",       SpDecimal.D90(total));
                p.Add("@DESCUENTO",     SpDecimal.D90(descuento));
                p.Add("@TOTAL",         SpDecimal.D90(totalFinal));
                p.Add("@FORMA",         forma);
                p.Add("@METODO",        metodo);
                p.Add("@ID_BANCO",      1);
                p.Add("@IDP",           idProv);
                p.Add("@IDU",           idu);
                p.Add("@STATUS",        (byte)1);
                p.Add("@NOTA",          nota);
                p.Add("@IDDETTMP",      0);
                p.Add("@IDART",         item.IdArt);
                p.Add("@CA",            item.Codigo);
                p.Add("@D",             item.Descripcion);
                p.Add("@CANT",          item.Cantidad);
                p.Add("@PC",            SpDecimal.D90(item.PrecioCosto));
                p.Add("@PVENTA",        SpDecimal.D90(item.PrecioVenta));
                p.Add("@CONTADO",       SpDecimal.D90(item.Contado));
                p.Add("@PPROMO",        SpDecimal.D90(item.PPromo));
                p.Add("@IDENTIFICADOR", i + 1);
                p.Add("@IDLOCAL",       idlocal);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 30);
                await conn.ExecuteAsync("JEJOGUA_TMP_CS", p, commandType: CommandType.StoredProcedure);
                var msg = p.Get<string>("@msg");
                if (msg != "GUARDADO") { MessageBox.Show($"Error en ítem {i + 1}: {msg}"); return; }
            }

            var cantItems = _items.Count;

            // Resetear formulario para nueva compra
            _items.Clear();
            _idArtActual = 0; _idPricesActual = 0; _caActual = ""; _descActual = "";
            _txtBuscarArt.Text = ""; _txtCantidad.Text = "1";
            _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "";
            _lblNombreArt.Text = "";
            _lblUCFecha.Text = "—"; _lblUVFecha.Text = "—"; _lblMPFecha.Text = "—";
            _btnInsertar.IsEnabled = false;

            MostrarModalPendiente(factura, totalFinal, descuento, cantItems);

            _txtBuscarArt.Focus();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar compra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void MostrarModalPendiente(string factura, decimal total, decimal descuento, int cantArticulos)
    {
        var SB = (string h) => new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

        var dlg = new Window {
            Title = "Compra registrada", Width = 460, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = SB("#F0F4F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            ShowInTaskbar = false
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // body
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ────────────────────────────────────────────────────────
        var header = new Border {
            Background = SB("#0E2F44"), Padding = new Thickness(24, 18, 24, 18)
        };
        var hRow = new Grid();
        hRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icoCircle = new Border {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(26),
            Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 79, 195, 247)),
            Child = new TextBlock {
                Text = "", FontSize = 22,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                Foreground = SB("#7FB3D3"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(icoCircle, 0); hRow.Children.Add(icoCircle);

        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock {
            Text = "COMPRA PENDIENTE DE APROBACIÓN",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White
        });
        hTxt.Children.Add(new TextBlock {
            Text = "La compra fue registrada y está en espera de confirmación por un administrador.",
            FontSize = 10.5, TextWrapping = TextWrapping.Wrap,
            Foreground = SB("#7FB3D3"), Margin = new Thickness(0, 5, 0, 0)
        });
        Grid.SetColumn(hTxt, 1); hRow.Children.Add(hTxt);
        header.Child = hRow;
        Grid.SetRow(header, 0); root.Children.Add(header);

        // ── Body ──────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 8) };

        // Card de resumen
        Border MkCard(UIElement child, string borderColor = "#D0DCE8") => new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = SB(borderColor), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = child,
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 1, BlurRadius = 6, Opacity = 0.07,
                Color = System.Windows.Media.Colors.Black }
        };

        // Fila de datos: bullet + label + valor
        StackPanel DataRow(string label, string val, string valColor = "#0E2F44") {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            sp.Children.Add(new Border {
                Width = 4, Height = 4, CornerRadius = new CornerRadius(2),
                Background = SB("#7FB3D3"), Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Width = 110,
                Foreground = SB("#6B7E8C"), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 12,
                FontWeight = FontWeights.SemiBold, Foreground = SB(valColor),
                VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        // Card 1: datos de la compra
        var cardDatos = new StackPanel();
        cardDatos.Children.Add(new TextBlock {
            Text = "DATOS DE LA COMPRA", FontSize = 9.5, FontWeight = FontWeights.Bold,
            Foreground = SB("#5A7D94"), Margin = new Thickness(0, 0, 0, 8)
        });
        cardDatos.Children.Add(DataRow("N° Factura",  factura));
        cardDatos.Children.Add(new Border { Height = 1, Background = SB("#EEF2F6"), Margin = new Thickness(0,2,0,2) });
        cardDatos.Children.Add(DataRow("Artículos",   $"{cantArticulos} ítem(s)"));
        cardDatos.Children.Add(new Border { Height = 1, Background = SB("#EEF2F6"), Margin = new Thickness(0,2,0,2) });
        if (descuento > 0) {
            cardDatos.Children.Add(DataRow("Descuento", $"Gs. {descuento:N0}".Replace(",",".")));
            cardDatos.Children.Add(new Border { Height = 1, Background = SB("#EEF2F6"), Margin = new Thickness(0,2,0,2) });
        }
        cardDatos.Children.Add(DataRow("Total final", $"Gs. {total:N0}".Replace(",","."), "#1A7F4B"));

        var card1 = MkCard(cardDatos); card1.Margin = new Thickness(0, 0, 0, 10);
        body.Children.Add(card1);

        // Card 2: próximo paso (aviso info)
        var infoRow = new Grid();
        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        infoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var infoIco = new Border {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background = SB("#D0E8F5"), Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock {
                Text = "",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 14, Foreground = SB("#1A4F6E"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var infoTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoTxt.Children.Add(new TextBlock {
            Text = "¿Qué sigue?", FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = SB("#154360"), Margin = new Thickness(0, 0, 0, 3)
        });
        infoTxt.Children.Add(new TextBlock {
            Text = "Un administrador debe abrir \"Editar compras\", buscar esta factura y confirmarla para aplicar los precios y el stock.",
            FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Foreground = SB("#2C5F7A")
        });
        Grid.SetColumn(infoIco, 0); infoRow.Children.Add(infoIco);
        Grid.SetColumn(infoTxt, 1); infoRow.Children.Add(infoTxt);

        var card2 = MkCard(infoRow, "#B3D4E8");
        card2.Background = SB("#EBF5FF");
        body.Children.Add(card2);

        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── Footer ────────────────────────────────────────────────────────
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = SB("#D0DCE8"), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 12, 20, 12)
        };
        var btnContent = new StackPanel { Orientation = Orientation.Horizontal };
        btnContent.Children.Add(new TextBlock {
            Text = "",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        btnContent.Children.Add(new TextBlock { Text = "Entendido", VerticalAlignment = VerticalAlignment.Center });
        var btnAceptar = new Button {
            Content = btnContent, Height = 38, Padding = new Thickness(24, 0, 24, 0),
            Background = SB("#1A4F6E"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13,
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnAceptar.Click += (_, _) => dlg.Close();
        dlg.KeyDown      += (_, e) => { if (e.Key == Key.Enter || e.Key == Key.Escape) dlg.Close(); };
        footer.Child = btnAceptar;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        dlg.Content = root;
        dlg.Loaded += (_, _) => btnAceptar.Focus();
        dlg.ShowDialog();
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
        // Si el usuario ya tocó a mano cualquiera de los precios de este artículo (Costo,
        // Venta, Contado, Promo o el % que los calcula), NO se pisan al cambiar de local — ver
        // comentario en _preciosEditadosManualmente. Las fechas de ÚLT. COMPRA/VENTA/MOD.
        // PRECIO sí se refrescan siempre: son solo informativas del local nuevo, no un valor
        // que el usuario haya editado.
        using var conn = _db.Create();
        var p = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT IDPRICES, PC, PVENTA, CONTADO, PPROMO, FCOMPRA, FVENTA, FMP FROM PRICES WHERE IDART=@Id AND IDLOCAL=@L AND DELETADO=0",
            new { Id = _idArtActual, L = _idLocalActual });
        if (p != null)
        {
            _idPricesActual  = (int)p.IDPRICES;
            if (!_preciosEditadosManualmente)
            {
                _cargandoPreciosProgramatico = true;
                _txtPC.Text      = ((decimal)p.PC).ToString("N0");
                _txtPV.Text      = ((decimal)p.PVENTA).ToString("N0");
                _txtContado.Text = ((decimal)p.CONTADO).ToString("N0");
                _txtPPromo.Text  = ((decimal)p.PPROMO).ToString("N0");
                _cargandoPreciosProgramatico = false;
            }
            _lblUCFecha.Text = p.FCOMPRA is DateTime fc ? fc.ToString("dd/MM/yyyy") : "—";
            _lblUVFecha.Text = p.FVENTA  is DateTime fv ? fv.ToString("dd/MM/yyyy") : "—";
            _lblMPFecha.Text = p.FMP     is DateTime fm ? fm.ToString("dd/MM/yyyy") : "—";
        }
        else if (!_preciosEditadosManualmente)
        {
            _idPricesActual = 0;
            _cargandoPreciosProgramatico = true;
            _txtPC.Text = _txtPV.Text = _txtContado.Text = _txtPPromo.Text = "0";
            _cargandoPreciosProgramatico = false;
            _lblUCFecha.Text = _lblUVFecha.Text = _lblMPFecha.Text = "—";
        }
    }

    private async Task AbrirBuscadorArticulo()
    {
        var modal = new BuscadorArticuloModal(_db, (byte)_idLocalActual) { Owner = this };
        if (modal.ShowDialog() != true || modal.ArticuloSeleccionado == null) return;
        var a = modal.ArticuloSeleccionado;
        // Artículo nuevo: sí corresponde reflejar sus precios reales, sin importar si el
        // artículo ANTERIOR tenía ediciones manuales pendientes — el flag es por artículo, no
        // global (ver _preciosEditadosManualmente).
        _preciosEditadosManualmente = false;
        _idArtActual    = a.IdArt;
        _caActual       = a.Codigo;
        _descActual     = a.Descripcion;
        _idPricesActual = a.IdPrices;
        _txtBuscarArt.Text = a.Codigo;
        _lblNombreArt.Text = a.Descripcion;
        _cargandoPreciosProgramatico = true;
        _txtPC.Text        = a.PrecioCosto.ToString("N0");
        _txtPV.Text        = a.PrecioVenta.ToString("N0");
        _txtContado.Text   = a.Contado.ToString("N0");
        _txtPPromo.Text    = a.PPromo.ToString("N0");
        _cargandoPreciosProgramatico = false;
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

    private async Task ImprimirComprobanteCompra(string nombreLocal, string factura, string comprobante,
        List<LineaCompra> items, decimal total, decimal descuento)
    {
        var (impresora, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

        var doc = new System.Drawing.Printing.PrintDocument { DocumentName = "Comprobante Compra" };

        doc.PrintPage += (_, e) =>
        {
            var g      = e.Graphics!;
            var bold9  = new System.Drawing.Font("Courier New", 9,  System.Drawing.FontStyle.Bold);
            var reg8   = new System.Drawing.Font("Courier New", 8,  System.Drawing.FontStyle.Regular);
            var bold10 = new System.Drawing.Font("Courier New", 10, System.Drawing.FontStyle.Bold);
            var reg7   = new System.Drawing.Font("Courier New", 7,  System.Drawing.FontStyle.Regular);

            int lx = 20, y = 20;
            int w  = (int)(e.PageBounds.Width - 40);

            void Linea(string txt, System.Drawing.Font f, bool centrar = false)
            {
                var sz = g.MeasureString(txt, f);
                float fx = centrar ? (w - sz.Width) / 2f + lx : lx;
                g.DrawString(txt, f, System.Drawing.Brushes.Black, fx, y);
                y += (int)sz.Height + 2;
            }

            void Sep() { g.DrawLine(System.Drawing.Pens.Black, lx, y, lx + w, y); y += 4; }

            void Par(string lbl, string val)
            {
                var szV = g.MeasureString(val, reg8);
                g.DrawString(lbl, reg8, System.Drawing.Brushes.Black, lx, y);
                g.DrawString(val, reg8, System.Drawing.Brushes.Black, lx + w - szV.Width, y);
                y += (int)szV.Height + 1;
            }

            Linea("COMPROBANTE DE COMPRA", bold10, centrar: true);
            Linea($"LOCAL: {nombreLocal}", reg8, centrar: true);
            Linea($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}", reg8, centrar: true);
            Sep();

            Par("Factura N°:",     factura);
            Par("Comprobante:",    comprobante);
            Par("Usuario:",        _sesion.UsuarioActual?.NombreUsuario ?? "-");
            Sep();

            Linea("Cant.  Descripción", reg8);
            g.DrawLine(System.Drawing.Pens.DarkGray, lx, y, lx + w, y); y += 2;

            foreach (var it in items)
            {
                if (y > e.PageBounds.Height - 60) { e.HasMorePages = true; break; }
                var desc = it.Descripcion.Length > 28 ? it.Descripcion[..28] : it.Descripcion;
                g.DrawString($"{it.Cantidad,4:0.##}   {desc,-28}", reg7, System.Drawing.Brushes.Black, lx, y);
                var pcStr = it.PrecioCosto.ToString("N0") + " Gs.";
                var pcSz  = g.MeasureString(pcStr, reg7);
                g.DrawString(pcStr, reg7, System.Drawing.Brushes.Black, lx + w - pcSz.Width, y);
                y += 14;
            }

            Sep();
            if (descuento > 0) Par("Descuento:", descuento.ToString("N0") + " Gs.");
            Par("TOTAL:", total.ToString("N0") + " Gs.");
            Sep();
            Linea("Sistema ElectroMar", reg8, centrar: true);

            if (!e.HasMorePages)
            { bold9.Dispose(); reg8.Dispose(); bold10.Dispose(); reg7.Dispose(); }
        };

        CrediSoft.UI.Views.Shared.TicketPrinter.ImprimirConConfig(doc, impresora);
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
        new(System.Windows.Media.Color.FromRgb(26, 79, 110));
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
        Loaded += async (_, _) => { await CargarAsync(); _txtBuscar.Focus(); };
    }

    // Quita el chrome nativo del TextBox (borde azul de foco + fondo por defecto de WPF) para
    // que se vea limpio dentro del Border blanco redondeado que lo envuelve — sin esto, el
    // TextBox dibuja su propio borde encima, dando el aspecto de "doble recuadro" reportado.
    private static void ApplyFlatSearchStyle(TextBox tb)
    {
        var template = new ControlTemplate(typeof(TextBox));
        var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        template.VisualTree = scrollViewer;
        tb.Template = template;
        tb.FocusVisualStyle = null;
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header en dos líneas (título + subtítulo aclarando código/nombre, mismo patrón que
        // EditarComprasWindow) en vez de una sola fila apretada "📍 Buscar local: [___]" — el
        // TextBox quedaba angosto y sin ningún indicativo de qué se puede tipear ahí (WPF no
        // tiene placeholder nativo; el guión amarillo que se veía era solo el foco/subrayado
        // por defecto del control sin estilizar). Feedback real: "no se entiende qué buscar".
        var headerBg = new Border { Background = BrNaranja, Padding = new Thickness(18, 14, 18, 14) };
        var headerCol = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        titleRow.Children.Add(new TextBlock {
            Text = "📍", FontSize = 17, Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var titleTextCol = new StackPanel();
        titleTextCol.Children.Add(new TextBlock {
            Text = "Seleccionar local", FontWeight = FontWeights.Bold, FontSize = 14,
            Foreground = System.Windows.Media.Brushes.White });
        titleTextCol.Children.Add(new TextBlock {
            Text = "Buscá por código o por nombre", FontSize = 10.5,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9C, 0xC5, 0xDE)) });
        titleRow.Children.Add(titleTextCol);
        headerCol.Children.Add(titleRow);

        // Campo con placeholder simulado (TextBlock superpuesto que desaparece al tipear) y
        // estilo propio (foco verde, no el azul de sistema por defecto) para que combine con
        // el resto de la app en vez del control WPF sin estilizar.
        var searchBoxGrid = new Grid();
        _txtBuscar = new TextBox {
            Height = 36, FontSize = 13,
            Padding = new Thickness(34, 0, 10, 0),
            Background = System.Windows.Media.Brushes.White,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 40, 60)),
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            CaretBrush = BrNaranja
        };
        ApplyFlatSearchStyle(_txtBuscar);
        var lupaIcono = new TextBlock {
            Text = "🔍", FontSize = 13, Opacity = 0.55,
            Margin = new Thickness(11, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false };
        var placeholder = new TextBlock {
            Text = "Ej: 6  ó  Buena Vista",
            FontSize = 13, Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(34, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false };
        _txtBuscar.TextChanged += (_, _) => {
            placeholder.Visibility = string.IsNullOrEmpty(_txtBuscar.Text) ? Visibility.Visible : Visibility.Collapsed;
            Filtrar();
        };
        _txtBuscar.KeyDown += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        var searchBoxBorder = new Border {
            Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(6),
            Height = 36 };
        searchBoxGrid.Children.Add(searchBoxBorder);
        searchBoxGrid.Children.Add(_txtBuscar);
        searchBoxGrid.Children.Add(lupaIcono);
        searchBoxGrid.Children.Add(placeholder);
        headerCol.Children.Add(searchBoxGrid);

        headerBg.Child = headerCol;
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
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 79, 110))));
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
    public int     IdArt          { get; set; }
    public int     IdPrices       { get; set; }
    public string  Codigo         { get; set; } = "";
    public string  Descripcion    { get; set; } = "";
    public string  Marca          { get; set; } = "";
    public decimal Stock          { get; set; }
    public decimal PrecioCosto    { get; set; }
    public decimal PrecioVenta    { get; set; }
    public decimal Contado        { get; set; }
    public decimal PPromo         { get; set; }
    public string  LocalUbicacion { get; set; } = "";
    public string  UltimaCompra   { get; set; } = "ÚLTIMA COMPRA: —";
    public string  UltimaVenta    { get; set; } = "ÚLTIMA VENTA: —";
    public string  StockFmt       => Stock.ToString("N0");
    public string  PCFmt          => PrecioCosto.ToString("N0");
    public string  PVFmt          => PrecioVenta.ToString("N0");
}

public class LocalItemBuscadorModal
{
    public byte   Id     { get; set; }
    public string Nombre { get; set; } = "";
}

public class StockLocalItem
{
    public string Nombre { get; set; } = "";
    public decimal Stock { get; set; }
    public string StockFmt => Stock.ToString("N0");
}

public class BuscadorArticuloModal : Window
{
    private readonly IDbConnectionFactory _db;
    private byte     _idlocal;
    private TextBox   _txtBuscar    = null!;
    private ComboBox  _cboLocal     = null!;
    private CheckBox  _chkSoloStock = null!;
    private DataGrid  _grid         = null!;
    private TextBlock _lblConteo    = null!;
    private Border    _panelDesglose  = null!;
    private ItemsControl _listaDesglose = null!;
    // paginación BD
    private int  _paginaMod    = 1;
    private int  _porPaginaMod = 50;
    private bool _cargandoMod  = false;
    private System.Threading.CancellationTokenSource? _mod_cts;

    public ArticuloResumen? ArticuloSeleccionado { get; private set; }

    public void PreFiltrar(string term)
    {
        if (_txtBuscar == null) return;
        _txtBuscar.Text = term;
    }

    private static readonly System.Windows.Media.SolidColorBrush BrPrim  =
        new(System.Windows.Media.Color.FromRgb(14, 47, 68));    // #0E2F44
    private static readonly System.Windows.Media.SolidColorBrush BrDark  =
        new(System.Windows.Media.Color.FromRgb(26, 79, 110));   // #1A4F6E
    private static readonly System.Windows.Media.SolidColorBrush BrClaro =
        new(System.Windows.Media.Color.FromRgb(176, 212, 236)); // #B0D4EC
    private static readonly System.Windows.Media.SolidColorBrush BrGris =
        new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde =
        new(System.Windows.Media.Color.FromRgb(229, 231, 235));

    public BuscadorArticuloModal(IDbConnectionFactory db, byte idlocal)
    {
        _db      = db;
        _idlocal = idlocal;
        Title    = "Buscar Artículo";
        Width    = 1000; Height = 600;
        MinWidth = 760; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarLocalesAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // desglose colapsable
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // footer

        // ── Header búsqueda ──
        var headerBg = new Border {
            Background = BrPrim,
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
        _txtBuscar.TextChanged += (_, _) => FiltrarMod();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        // ── Barra de filtros ──
        var filtros = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BrBorde, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(12,8,12,8)
        };
        var filtrosRow = new StackPanel { Orientation = Orientation.Horizontal };
        filtrosRow.Children.Add(new TextBlock {
            Text = "Local:", FontWeight = FontWeights.SemiBold, Foreground = BrGris,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        _cboLocal = new ComboBox {
            Width = 220, Padding = new Thickness(8,5,8,5), FontSize = 12.5,
            DisplayMemberPath = "Nombre", SelectedValuePath = "Id",
            VerticalAlignment = VerticalAlignment.Center
        };
        _cboLocal.SelectionChanged += (_, _) => { _idlocal = (byte)(_cboLocal.SelectedValue is byte b ? b : 0); FiltrarMod(); };
        filtrosRow.Children.Add(_cboLocal);

        _chkSoloStock = new CheckBox {
            Content = "Solo con stock", FontSize = 12.5, Foreground = BrGris,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20,0,0,0),
            IsChecked = true
        };
        _chkSoloStock.Checked   += (_, _) => FiltrarMod();
        _chkSoloStock.Unchecked += (_, _) => FiltrarMod();
        filtrosRow.Children.Add(_chkSoloStock);
        filtros.Child = filtrosRow;
        Grid.SetRow(filtros, 1); root.Children.Add(filtros);

        // ── DataGrid ──
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrBorde,
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle(),
            CanUserSortColumns = true // click en encabezado ordena (aplica sobre la página cargada)
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Codigo"),         Width = new DataGridLength(100), SortMemberPath = "Codigo" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("Descripcion"),    Width = new DataGridLength(1, DataGridLengthUnitType.Star), SortMemberPath = "Descripcion" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Marca",       Binding = new System.Windows.Data.Binding("Marca"),          Width = new DataGridLength(110), SortMemberPath = "Marca" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Stock",       Binding = new System.Windows.Data.Binding("StockFmt"),       Width = new DataGridLength(70), SortMemberPath = "Stock" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "P. Costo",    Binding = new System.Windows.Data.Binding("PCFmt"),          Width = new DataGridLength(90), SortMemberPath = "PrecioCosto" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "P. Venta",    Binding = new System.Windows.Data.Binding("PVFmt"),          Width = new DataGridLength(90), SortMemberPath = "PrecioVenta" });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ubicación",   Binding = new System.Windows.Data.Binding("LocalUbicacion"), Width = new DataGridLength(140), SortMemberPath = "LocalUbicacion" });
        // El doble clic sobre una fila no siempre dejaba SelectedItem seteado a tiempo —
        // reportado: doble clic sobre "MANTEL DE MESA..." tiraba "Seleccione un artículo de
        // la lista" en vez de tomarlo. Causa: FiltrarMod tiene un debounce de 200ms y
        // CargarPaginaModAsync resetea _grid.SelectedItem=null al repoblar la grilla — si el
        // doble clic cae mientras esa recarga está en curso, el segundo click de MouseDoubleClick
        // puede llegar antes de que WPF vuelva a fijar SelectedItem sobre la fila ya repoblada.
        // Se resuelve la fila real bajo el cursor a partir de e.OriginalSource en vez de
        // depender de SelectedItem, que es la forma robusta de manejar doble clic en DataGrid.
        _grid.MouseDoubleClick += (_, e) =>
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridRow) dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            if (dep is DataGridRow row && row.Item is ArticuloResumen art)
            {
                _grid.SelectedItem = art;
                Seleccionar();
            }
        };
        _grid.SelectionChanged += async (_, _) => await ActualizarDesgloseAsync();
        Grid.SetRow(_grid, 2); root.Children.Add(_grid);

        // ── Panel de desglose por local (colapsado hasta seleccionar un artículo) ──
        _panelDesglose = new Border {
            Margin = new Thickness(8,8,8,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 244, 251)), // #EEF4FB
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 222, 251)), // #BBDEFB
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,8,12,8), MaxHeight = 130,
            Visibility = Visibility.Collapsed
        };
        var desgloseStack = new StackPanel();
        desgloseStack.Children.Add(new TextBlock {
            Text = "STOCK POR LOCAL", FontSize = 9.5, FontWeight = FontWeights.Bold,
            Foreground = BrDark, Margin = new Thickness(0,0,0,6) });
        var scrollDesglose = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 95 };
        _listaDesglose = new ItemsControl();
        var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
        _listaDesglose.ItemsPanel = new ItemsPanelTemplate(wrapPanelFactory);
        _listaDesglose.ItemTemplate = CrearPlantillaChip();
        scrollDesglose.Content = _listaDesglose;
        desgloseStack.Children.Add(scrollDesglose);
        _panelDesglose.Child = desgloseStack;
        Grid.SetRow(_panelDesglose, 3); root.Children.Add(_panelDesglose);

        // ── Footer ──
        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 8, 8, 8), Margin = new Thickness(0,6,0,0)
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
        Grid.SetRow(barBtns, 4); root.Children.Add(barBtns);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private DataTemplate CrearPlantillaChip()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.White);
        borderFactory.SetValue(Border.BorderBrushProperty, BrClaro);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(8,4,8,4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(0,0,6,6));

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Texto"));
        textFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
        textFactory.SetValue(TextBlock.ForegroundProperty, BrPrim);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        borderFactory.AppendChild(textFactory);
        return new DataTemplate { VisualTree = borderFactory };
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty, BrPrim));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        s.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        return s;
    }

    private async Task CargarLocalesAsync()
    {
        try
        {
            using var conn = _db.Create();
            var locales = (await conn.QueryAsync<(byte Id, string Nombre)>(
                "SELECT ID_LOCAL as Id, NOMBRE as Nombre FROM LOCALES ORDER BY NOMBRE")).ToList();
            var opciones = new List<LocalItemBuscadorModal> { new() { Id = 0, Nombre = "Todos los locales" } };
            opciones.AddRange(locales.Select(l => new LocalItemBuscadorModal { Id = l.Id, Nombre = l.Nombre }));
            _cboLocal.ItemsSource = opciones;
            _cboLocal.SelectedValue = opciones.Any(l => l.Id == _idlocal) ? _idlocal : (byte)0;
        }
        catch { _cboLocal.SelectedIndex = 0; }
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        _paginaMod = 1;
        await CargarPaginaModAsync();
    }

    private void FiltrarMod()
    {
        _paginaMod = 1;
        _mod_cts?.Cancel();
        _mod_cts = new System.Threading.CancellationTokenSource();
        var token = _mod_cts.Token;
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (!token.IsCancellationRequested)
                    await CargarPaginaModAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    private async Task CargarPaginaModAsync()
    {
        if (_cargandoMod) return;
        _cargandoMod = true;
        try
        {
            var q = _txtBuscar?.Text.Trim() ?? "";
            bool soloStock = _chkSoloStock?.IsChecked == true;
            using var conn = _db.Create();

            var whereQ = string.IsNullOrEmpty(q)
                ? ""
                : "AND (a.CA LIKE @Q OR a.D LIKE @Q) ";
            var whereStock = soloStock ? "AND ISNULL(p.S,0) > 0 " : "";
            // Con término de búsqueda, prioriza artículos cuya descripción EMPIEZA con lo
            // buscado (ej. "MESA AYUDANTE...") antes que los que solo lo contienen en medio
            // (ej. "MANTEL DE MESA...") — antes ordenaba solo alfabético por a.D, así que
            // "MANTEL..." salía primero que "MESA..." al buscar "mesa" aunque el segundo sea
            // la coincidencia más relevante. Dentro de cada grupo, alfabético como antes.
            var ordenRelevancia = string.IsNullOrEmpty(q)
                ? "a.D"
                : "CASE WHEN a.D LIKE @QInicio THEN 0 ELSE 1 END, a.D";

            string subLocalGlobal = "ISNULL((SELECT TOP 1 l2.NOMBRE FROM PRICES p2 " +
                "INNER JOIN LOCALES l2 ON p2.IDLOCAL = l2.ID_LOCAL " +
                "WHERE p2.IDART = a.ID AND p2.S > 0 AND p2.DELETADO = 0 " +
                "ORDER BY p2.S DESC), '') as LOCAL_UBI";
            string subLocalFijo = "ISNULL((SELECT TOP 1 l2.NOMBRE FROM LOCALES l2 " +
                "WHERE l2.ID_LOCAL = @L), '') as LOCAL_UBI";

            var sqlCount = _idlocal == 0
                ? $"SELECT COUNT(*) FROM ARTICULOS a " +
                  "OUTER APPLY (SELECT SUM(S) as S FROM PRICES WHERE IDART = a.ID AND DELETADO = 0) p " +
                  $"WHERE a.ES = 1 {whereQ}{whereStock}"
                : $"SELECT COUNT(*) FROM ARTICULOS a LEFT JOIN PRICES p ON p.IDART = a.ID AND p.IDLOCAL = @L AND p.DELETADO = 0 WHERE a.ES = 1 {whereQ}{whereStock}";

            int _offsetMod = (_paginaMod - 1) * _porPaginaMod;
            var sqlData = _idlocal == 0
                ? $"SELECT IDART, CA, D, MARCA, IDPRICES, PC, PVENTA, CONTADO, PPROMO, STOCK, LOCAL_UBI FROM (" +
                  $"SELECT a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D, ISNULL(m.MARCA,'') as MARCA, " +
                  "ISNULL(pTop.IDPRICES,0) as IDPRICES, " +
                  "ISNULL(pTop.PC,0) as PC, ISNULL(pTop.PVENTA,0) as PVENTA, " +
                  "ISNULL(pTop.CONTADO,0) as CONTADO, ISNULL(pTop.PPROMO,0) as PPROMO, " +
                  $"ISNULL(p.S,0) as STOCK, {subLocalGlobal}, " +
                  $"ROW_NUMBER() OVER (ORDER BY {ordenRelevancia}) AS __rn " +
                  "FROM ARTICULOS a " +
                  "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
                  "OUTER APPLY (SELECT SUM(S) as S FROM PRICES WHERE IDART = a.ID AND DELETADO = 0) p " +
                  "OUTER APPLY (SELECT TOP 1 IDPRICES, PC, PVENTA, CONTADO, PPROMO " +
                  "FROM PRICES WHERE IDART = a.ID AND DELETADO = 0 ORDER BY IDLOCAL) pTop " +
                  $"WHERE a.ES = 1 {whereQ}{whereStock}" +
                  $") __p WHERE __rn BETWEEN {_offsetMod + 1} AND {_offsetMod + _porPaginaMod}"
                : $"SELECT IDART, CA, D, MARCA, IDPRICES, PC, PVENTA, CONTADO, PPROMO, STOCK, LOCAL_UBI FROM (" +
                  $"SELECT a.ID as IDART, CAST(a.CA AS NVARCHAR(50)) as CA, a.D, ISNULL(m.MARCA,'') as MARCA, " +
                  "ISNULL(p.IDPRICES,0) as IDPRICES, " +
                  "ISNULL(p.PC,0) as PC, ISNULL(p.PVENTA,0) as PVENTA, " +
                  "ISNULL(p.CONTADO,0) as CONTADO, ISNULL(p.PPROMO,0) as PPROMO, " +
                  $"ISNULL(p.S,0) as STOCK, {subLocalFijo}, " +
                  $"ROW_NUMBER() OVER (ORDER BY {ordenRelevancia}) AS __rn " +
                  "FROM ARTICULOS a " +
                  "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
                  "LEFT JOIN PRICES p ON p.IDART = a.ID AND p.IDLOCAL = @L AND p.DELETADO = 0 " +
                  $"WHERE a.ES = 1 {whereQ}{whereStock}" +
                  $") __p WHERE __rn BETWEEN {_offsetMod + 1} AND {_offsetMod + _porPaginaMod}";

            var prm = new { Q = $"%{q}%", QInicio = $"{q}%", L = _idlocal };
            var total = await conn.ExecuteScalarAsync<int>(sqlCount, prm, commandTimeout: 30);
            var rows  = await conn.QueryAsync<dynamic>(sqlData, prm, commandTimeout: 30);

            var lista = rows.Select(r => new ArticuloResumen {
                IdArt          = (int)r.IDART,
                IdPrices       = (int)r.IDPRICES,
                Codigo         = r.CA is double d ? ((long)d).ToString() : r.CA?.ToString() ?? "",
                Descripcion    = (string)r.D,
                Marca          = r.MARCA?.ToString() ?? "",
                Stock          = (decimal)r.STOCK,
                PrecioCosto    = (decimal)r.PC,
                PrecioVenta    = (decimal)r.PVENTA,
                Contado        = (decimal)r.CONTADO,
                PPromo         = (decimal)r.PPROMO,
                LocalUbicacion = r.LOCAL_UBI?.ToString() ?? "",
            }).ToList();

            _grid.ItemsSource  = lista;
            // Se selecciona la primera fila (con su highlight azul visible) en vez de dejar
            // SelectedItem=null — el usuario veía la lista de resultados sin ninguna fila
            // resaltada, asumía que la de arriba (o la que tenía el mouse encima) ya estaba
            // "elegida", y al apretar Enter le saltaba "Seleccione un artículo de la lista"
            // porque en realidad no había nada seleccionado.
            _grid.SelectedIndex = lista.Count > 0 ? 0 : -1;
            int totalPags = Math.Max(1, (int)Math.Ceiling(total / (double)_porPaginaMod));
            _lblConteo.Text = $"{total} artículos — pág. {_paginaMod}/{totalPags}";
            _panelDesglose.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _lblConteo.Text = "Error al cargar";
            MessageBox.Show("Error al cargar artículos:\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cargandoMod = false; }
    }

    private async Task ActualizarDesgloseAsync()
    {
        if (_grid.SelectedItem is not ArticuloResumen art)
        {
            _panelDesglose.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            using var conn = _db.Create();
            var stockPorLocal = (await conn.QueryAsync<StockLocalItem>(
                "SELECT l.NOMBRE as Nombre, p.S as Stock FROM PRICES p " +
                "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
                "WHERE p.IDART = @Id AND p.DELETADO = 0 AND p.S > 0 " +
                "ORDER BY p.S DESC",
                new { Id = art.IdArt })).ToList();

            if (stockPorLocal.Count == 0)
            {
                _panelDesglose.Visibility = Visibility.Collapsed;
                return;
            }

            _listaDesglose.ItemsSource = stockPorLocal.Select(p => new { Texto = $"{p.Nombre}: {p.StockFmt}" }).ToList();
            _panelDesglose.Visibility = Visibility.Visible;
        }
        catch { _panelDesglose.Visibility = Visibility.Collapsed; }
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is ArticuloResumen a)
        {
            ArticuloSeleccionado = a;
            DialogResult = true;
            Close();
        }
        else if (_grid.Items.Count > 0)
        {
            // Resultados visibles pero ninguno resaltado todavía (ej. Enter apretado en el
            // instante exacto en que la búsqueda se está recargando) — se toca la primera
            // fila en vez de solo avisar, así el usuario no tiene que repetir la acción.
            _grid.SelectedIndex = 0;
            Seleccionar();
        }
        else
        {
            MessageBox.Show("No hay artículos para seleccionar. Hacé clic sobre una fila de la lista antes de continuar.",
                "Ningún artículo seleccionado", MessageBoxButton.OK, MessageBoxImage.Information);
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
        new(System.Windows.Media.Color.FromRgb(26, 79, 110));
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
                p.Add("@TOTAL",       Math.Truncate(total));
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
                p.Add("@PC",          Math.Truncate(item.PrecioCosto));
                p.Add("@PVENTA",      Math.Truncate(item.PrecioVenta));
                p.Add("@CONTADO",     Math.Truncate(item.Contado));
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
                p.Add("@PARCIAL",     Math.Truncate(total));
                p.Add("@PUNITORIO",   (decimal)0);
                p.Add("@DESCUENTO",   (decimal)0);
                p.Add("@SUBTOTAL",    Math.Truncate(total));
                p.Add("@HABER",       (decimal)0);
                p.Add("@TOTALFINAL",  Math.Truncate(total));
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
                p.Add("@PC",          Math.Truncate(item.PrecioCosto));
                p.Add("@CANTIDAD",    item.Cantidad);
                p.Add("@IDPRICES",    0);
                p.Add("@PVENTA",      Math.Truncate(item.PrecioVenta));
                p.Add("@CONTADO",     Math.Truncate(item.Contado));
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

// ══════════════════════════════════════════════════════════════════════════════
//  IGUALAR PRECIOS ENTRE LOCALES
//  Se dispara al insertar en una compra un artículo sin precio en el local
//  destino (PC=0 y PVENTA=0) pero que SÍ tiene precio cargado en algún otro
//  local — caso real detectado en producción (TK886, LU-5004): el precio
//  quedaba en 0 en 14 de los 15 locales y nadie se enteraba hasta que un
//  cliente reclamaba. En vez de "adivinar" cuál precio copiar (por fecha más
//  reciente, por local matriz, etc.), se muestran TODOS los precios existentes
//  y el usuario elige explícitamente cuál usar como base — pedido explícito:
//  "bien transparentes... cual deseas para el resto del local, para que el
//  cliente sea responsable".
// ══════════════════════════════════════════════════════════════════════════════
public class IgualarPreciosDialogResult
{
    public bool Aplicado { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal Contado { get; set; }
    public decimal PPromo { get; set; }
}

// Agrupa locales que comparten exactamente el mismo precio — caso más común en la práctica:
// un precio "de catálogo" único vigente en casi todos los locales, con alguno puntual sin
// cargar. Listar 14 filas idénticas para "elegir una" era ruido puro y forzaba una decisión
// que no existía (reportado: "estos locales tiene el mismo precio, no sería redundante?").
// Solo cuando de verdad hay precios distintos entre locales aparecen grupos separados.
public class GrupoPrecio
{
    public decimal Pc { get; set; }
    public decimal Pventa { get; set; }
    public decimal Contado { get; set; }
    public decimal Ppromo { get; set; }
    public List<string> Locales { get; set; } = new();
    public string LocalesResumen => Locales.Count <= 3
        ? string.Join(", ", Locales)
        : $"{string.Join(", ", Locales.Take(2))} y {Locales.Count - 2} más";
}

public class IgualarPreciosDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly int _idArt;
    private readonly string _codigo;
    private readonly string _descripcion;
    private readonly List<Price> _preciosExistentes;
    private readonly List<LocalItem> _todosLocales;
    private readonly int _idLocalActual;
    private readonly int _idUsuario;
    private readonly string _nomMaquina;

    private readonly List<CheckBox> _chkLocales = new();
    private GrupoPrecio? _precioBase;

    public IgualarPreciosDialogResult Resultado { get; } = new();

    public IgualarPreciosDialog(IArticuloRepository repo, int idArt, string codigo, string descripcion,
        List<Price> preciosExistentes, List<LocalItem> todosLocales, int idLocalActual, int idUsuario, string nomMaquina)
    {
        _repo = repo; _idArt = idArt; _codigo = codigo; _descripcion = descripcion;
        _preciosExistentes = preciosExistentes; _todosLocales = todosLocales;
        _idLocalActual = idLocalActual; _idUsuario = idUsuario; _nomMaquina = nomMaquina;

        Title = "Precio no encontrado en este local";
        Width = 780; Height = 660;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        ResizeMode = ResizeMode.NoResize;
        Content = Build();
    }

    private UIElement Build()
    {
        var root = new DockPanel();

        var hdr = CW.Hdr("⚠  Este artículo no tiene precio cargado en este local", "#B45309");
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var info = new TextBlock
        {
            Text = $"{_codigo} — {_descripcion}",
            Margin = new Thickness(14, 10, 14, 2), FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55)),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(info, Dock.Top); root.Children.Add(info);

        var main = new StackPanel { Margin = new Thickness(14, 0, 14, 8) };

        main.Children.Add(new TextBlock
        {
            Text = "PASO 1 · Elija qué precio usar como base (toque el círculo):",
            FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)),
            Margin = new Thickness(0, 4, 0, 6)
        });

        // Columna de selección con RadioButton explícito por fila — antes había que hacer
        // click en la fila del DataGrid y confiar en que el resaltado de selección estándar
        // se notara; quedó reportado como poco intuitivo (no quedaba claro qué fila estaba
        // "elegida" antes de aplicar). Con un RadioButton por fila + fila resaltada en verde +
        // el panel de confirmación de abajo, la elección queda inequívoca.
        Action? SeleccionarInicial = null;
        var panelFilas = new StackPanel();
        var borderTabla = new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true,
            MaxHeight = 210
        };
        var scrollFilas = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panelFilas };
        borderTabla.Child = scrollFilas;

        var colHdr = new Grid { Margin = new Thickness(10, 6, 10, 6) };
        foreach (var w in new[] { 28, 200, 110, 110, 130, 100 })
            colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        void AddHdr(int col, string texto)
        {
            var tb = new TextBlock { Text = texto, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)) };
            Grid.SetColumn(tb, col); colHdr.Children.Add(tb);
        }
        AddHdr(1, "LOCALES"); AddHdr(2, "COSTO"); AddHdr(3, "CONTADO"); AddHdr(4, "VENTA (CRÉDITO)"); AddHdr(5, "PROMO");
        main.Children.Add(colHdr);

        var verdeSel  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231));
        var verdeBrd  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
        var blanco    = System.Windows.Media.Brushes.White;
        var negro     = System.Windows.Media.Brushes.Black;

        var grupos = _preciosExistentes
            .GroupBy(p => (p.Pc, p.Pventa, p.Contado, p.Ppromo))
            .Select(g => new GrupoPrecio
            {
                Pc = g.Key.Pc, Pventa = g.Key.Pventa, Contado = g.Key.Contado, Ppromo = g.Key.Ppromo,
                Locales = g.Select(x => x.LocalNombre).ToList()
            })
            .OrderByDescending(g => g.Locales.Count)
            .ToList();

        foreach (var grupo in grupos)
        {
            var fila = new Border
            {
                Background = blanco, BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand
            };
            var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            foreach (var w in new[] { 28, 200, 110, 110, 130, 100 })
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            var radio = new RadioButton { GroupName = "PrecioBase", VerticalAlignment = VerticalAlignment.Center, Tag = grupo };
            Grid.SetColumn(radio, 0); grid.Children.Add(radio);

            void AddCelda(int col, string texto, bool negrita = false)
            {
                var tb = new TextBlock { Text = texto, FontSize = 12, Foreground = negro,
                    FontWeight = negrita ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(tb, col); grid.Children.Add(tb);
            }
            var etiquetaLocales = grupo.Locales.Count == 1
                ? grupo.Locales[0]
                : $"{grupo.Locales.Count} locales — {grupo.LocalesResumen}";
            AddCelda(1, etiquetaLocales, negrita: true);
            AddCelda(2, grupo.Pc.ToString("N0"));
            AddCelda(3, grupo.Contado.ToString("N0"));
            AddCelda(4, grupo.Pventa.ToString("N0"));
            AddCelda(5, grupo.Ppromo.ToString("N0"));
            fila.Child = grid;
            fila.ToolTip = grupo.Locales.Count > 1 ? string.Join(", ", grupo.Locales) : null;

            void Seleccionar()
            {
                radio.IsChecked = true;
                foreach (var f in panelFilas.Children.OfType<Border>())
                {
                    f.Background = blanco; f.BorderBrush = System.Windows.Media.Brushes.Transparent;
                }
                fila.Background = verdeSel; fila.BorderBrush = verdeBrd;
                _precioBase = grupo;
                ActualizarConfirmacion();
            }
            fila.MouseLeftButtonUp += (_, __) => Seleccionar();
            radio.Checked += (_, __) => Seleccionar();
            if (grupos.Count == 1) SeleccionarInicial = Seleccionar;

            panelFilas.Children.Add(fila);
        }
        main.Children.Add(borderTabla);

        // Confirmación explícita de la elección — sin esto, el usuario podía dudar si su click
        // "pegó" antes de tocar Aplicar. Aparece recién cuando hay una fila elegida.
        _lblConfirmacion = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231)),
            BorderBrush = verdeBrd, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _txtConfirmacion = new TextBlock { FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 128, 61)), TextWrapping = TextWrapping.Wrap };
        _lblConfirmacion.Child = _txtConfirmacion;
        main.Children.Add(_lblConfirmacion);

        main.Children.Add(new TextBlock
        {
            Text = "PASO 2 · Marque en qué locales aplicar ese precio:",
            FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)),
            Margin = new Thickness(0, 14, 0, 6)
        });

        var chkBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246)),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 10, 0, 0)
        };
        var chkSp = new StackPanel();
        var chkHdrRow = new DockPanel();

        // Locales que YA tienen precio cargado (en cualquier grupo, no solo el elegido como
        // base) — nunca deben tildarse automáticamente. Pisar un precio que ya estaba
        // correcto (posiblemente distinto a propósito, ej. una zona con otro costo) sin que
        // el usuario lo decida fila por fila fue señalado como riesgo real: "Marcar todos"
        // tildaba TODO, incluidos locales con su propio precio ya bueno.
        var idsConPrecio = _preciosExistentes.Select(p => (int)p.IdLocal).ToHashSet();

        var btnTodos = new Button
        {
            Content = "Marcar los que faltan", FontSize = 10, Padding = new Thickness(8, 1, 8, 1),
            HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand
        };
        btnTodos.Click += (_, __) => { foreach (var c in _chkLocales.Where(c => !idsConPrecio.Contains((int)c.Tag!))) c.IsChecked = true; };
        DockPanel.SetDock(btnTodos, Dock.Right);
        chkHdrRow.Children.Add(btnTodos);
        chkSp.Children.Add(chkHdrRow);

        chkSp.Children.Add(new TextBlock
        {
            Text = "Los locales marcados con ⚠ ya tienen un precio cargado — al tildarlos, se REEMPLAZA por el elegido arriba.",
            FontSize = 10, FontStyle = FontStyles.Italic,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)),
            Margin = new Thickness(0, 4, 0, 0)
        });

        var chkWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var local in _todosLocales)
        {
            var tienePrecio = idsConPrecio.Contains(local.IdLocal);
            var chk = new CheckBox
            {
                Content = tienePrecio ? $"⚠ {local.Nombre}" : local.Nombre,
                Tag = local.IdLocal,
                IsChecked = local.IdLocal == _idLocalActual,
                Foreground = tienePrecio
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9))
                    : System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 0, 14, 4)
            };
            _chkLocales.Add(chk);
            chkWrap.Children.Add(chk);
        }
        chkSp.Children.Add(chkWrap);
        chkBorder.Child = chkSp;
        main.Children.Add(chkBorder);

        DockPanel.SetDock(main, Dock.Top); root.Children.Add(main);

        var pie = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(14, 0, 14, 12)
        };
        _btnAplicar = CW.Btn("✓  Aplicar precios seleccionados", "#16A34A");
        _btnAplicar.IsEnabled = false;
        _btnAplicar.Click += async (_, __) => await AplicarAsync();
        var btnOmitir = CW.Btn("Omitir por ahora", "#6B7280");
        btnOmitir.Click += (_, __) => { Resultado.Aplicado = false; DialogResult = false; Close(); };
        pie.Children.Add(_btnAplicar);
        pie.Children.Add(btnOmitir);
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // Recién acá existen _lblConfirmacion/_txtConfirmacion (creados más abajo en este
        // mismo método) — invocar la auto-selección del único grupo antes de este punto
        // causaba NullReferenceException dentro de ActualizarConfirmacion().
        SeleccionarInicial?.Invoke();

        return root;
    }

    private Button _btnAplicar = null!;
    private Border _lblConfirmacion = null!;
    private TextBlock _txtConfirmacion = null!;

    private void ActualizarConfirmacion()
    {
        if (_precioBase == null) { _lblConfirmacion.Visibility = Visibility.Collapsed; _btnAplicar.IsEnabled = false; return; }
        var origen = _precioBase.Locales.Count == 1
            ? _precioBase.Locales[0]
            : $"{_precioBase.Locales.Count} locales ({_precioBase.LocalesResumen})";
        _txtConfirmacion.Text =
            $"✓ Base elegida: {origen}  —  Costo {_precioBase.Pc:N0} · Contado {_precioBase.Contado:N0} · " +
            $"Venta {_precioBase.Pventa:N0} · Promo {_precioBase.Ppromo:N0}";
        _lblConfirmacion.Visibility = Visibility.Visible;
        _btnAplicar.IsEnabled = true;
    }

    private async Task AplicarAsync()
    {
        if (_precioBase == null) return;
        var idsLocal = _chkLocales.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToList();
        if (idsLocal.Count == 0)
        {
            MessageBox.Show("Marque al menos un local.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _btnAplicar.IsEnabled = false;
        try
        {
            foreach (var idLocal in idsLocal)
            {
                await _repo.ActualizarPreciosAsync(
                    _idArt, idLocal,
                    _precioBase.Pc, _precioBase.Pventa, _precioBase.Contado, _precioBase.Ppromo,
                    _idUsuario, _nomMaquina);
            }

            if (idsLocal.Contains(_idLocalActual))
            {
                Resultado.Aplicado = true;
                Resultado.PrecioCosto = _precioBase.Pc;
                Resultado.PrecioVenta = _precioBase.Pventa;
                Resultado.Contado = _precioBase.Contado;
                Resultado.PPromo = _precioBase.Ppromo;
            }

            MessageBox.Show($"Precio aplicado en {idsLocal.Count} local(es).", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al aplicar precios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnAplicar.IsEnabled = true;
        }
    }
}

// NOTA: la actualización de precio en otros locales desde "Editar ítem" (Nueva Compra) se
// resolvió integrando el selector de locales EN EL MISMO diálogo (ver OnGridDblClick más
// arriba, panel que se expande al tildar "También actualizar este precio en el sistema") en
// vez de abrir un segundo modal separado — pedido explícito: que se despliegue ahí mismo.

public class EditarComprasWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly SessionService       _sesion;

    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb( 26, 79,110));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb( 14, 47, 68));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 22,163, 74));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel    = new(System.Windows.Media.Color.FromRgb(107,114,128));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229,231,235));
    private static readonly System.Windows.Media.SolidColorBrush BrFondoArt = new(System.Windows.Media.Color.FromRgb(235,243,248));

    // Estado de la compra cargada
    private int     _idCabTmp;
    private string  _interno    = "";
    private int     _idProv;
    private string  _nombreProv = "";
    private int     _idBanco    = 1;
    private byte    _idLocalCompra = 1; // local de destino elegido en "Nueva Compra" (CAB_BUY_TMP.IDLOCAL), no el local de sesión de quien aprueba
    private string  _nombreLocalCompra = "";
    // Editable directamente desde esta pantalla — antes era un TextBlock de solo lectura y no
    // había forma de corregir el destino sin cancelar la compra entera y rehacerla desde "Nueva
    // Compra". Pedido explícito: cualquier usuario que llega a esta pantalla (ya tiene permiso
    // para aprobar/editar la compra) puede cambiarlo, sin restricción extra de rol.
    //
    // Se usa TextBox de solo lectura + botón "Seleccionar" que abre BuscadorLocalModal (ya
    // existente, reutilizado tal cual — busca por Código Y Nombre) en vez de un ComboBox con
    // texto libre: los locales se manejan mucho por código en este negocio (14 sucursales, se
    // conocen por número de local, no por nombre completo) y un ComboBox de solo Nombre no
    // dejaba buscar por código — pedido explícito tras probar la primera versión.
    private TextBox  _txtLocalDestino = null!;
    private LocalItem? _localDestinoSeleccionado;
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
    private TextBlock _lblEstadoBadge = null!;

    public EditarComprasWindow()
    {
        _db    = App.Services.GetRequiredService<IDbConnectionFactory>();
        _sesion = SessionService.Instance;
        Title  = "Modificar datos de compras";
        Width  = 1020; Height = 650;
        MinWidth = 860; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrPrimary;
        BuildUI();
        KeyDown += OnWindowKeyDown;
    }

    // Mismo BuscadorLocalModal que ya usan otras pantallas del sistema (busca por Código Y
    // Nombre) — cambiar el destino acá NO toca la base todavía, solo actualiza el estado en
    // memoria (_idLocalCompra), igual que cualquier otro campo del formulario (proveedor,
    // banco, nota). El cambio se persiste recién al presionar "Guardar cambios" o "Confirmar
    // compra", como el resto de los campos de esta pantalla.
    private void AbrirBuscadorLocalDestino()
    {
        var modal = new BuscadorLocalModal(_db) { Owner = this };
        if (modal.ShowDialog() == true && modal.LocalSeleccionado != null)
        {
            _localDestinoSeleccionado = modal.LocalSeleccionado;
            _idLocalCompra            = (byte)modal.LocalSeleccionado.IdLocal;
            _nombreLocalCompra        = modal.LocalSeleccionado.Nombre;
            _txtLocalDestino.Text     = $"{modal.LocalSeleccionado.Codigo} — {modal.LocalSeleccionado.Nombre}";
        }
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
        // ── Colores corporativos ──────────────────────────────────────────
        var BrHeaderVeryDark = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 47, 68));   // #0E2F44
        var BrAzulBase       = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 79, 110));  // #1A4F6E
        var BrAzulMedio      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 67, 96));   // #154360
        var BrAzulAccion     = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 96, 137));  // #1F6089
        var BrMuted          = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 179, 211)); // #7FB3D3
        var BrFondoClaro     = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 243, 248)); // #EBF3F8
        var BrFondoGris      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 244, 248)); // #F0F4F8
        var BrSep            = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 229, 239));
        var BrInputBg        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 252, 254));
        var BrInputBgRo      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 246, 251));

        // ── Helpers ───────────────────────────────────────────────────────
        // Label azul claro pequeño (encima de campos)
        TextBlock Lbl(string t) => new TextBlock {
            Text = t, FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrMuted, Margin = new Thickness(2, 0, 0, 2) };

        // Contenedor campo + label (stack vertical)
        StackPanel Field(string lbl, UIElement ctrl, Thickness? margin = null) {
            var sp = new StackPanel { Margin = margin ?? new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Lbl(lbl));
            sp.Children.Add(ctrl);
            return sp;
        }

        // TextBox campo cabecera (sobre fondo blanco)
        TextBox TB(int w, bool ro = false) => new TextBox {
            Width = w, Padding = new Thickness(8, 6, 8, 6), FontSize = 12,
            FontWeight = ro ? FontWeights.Normal : FontWeights.SemiBold,
            BorderBrush = BrSep, BorderThickness = new Thickness(1),
            Background = ro ? BrInputBgRo : BrInputBg,
            IsReadOnly = ro,
            Foreground = ro
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 100, 120))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 40, 60)),
            VerticalContentAlignment = VerticalAlignment.Center };

        // Botón de acción compacto (en cabecera)
        Button BtnAcc(string txt, System.Windows.Media.SolidColorBrush bg) => new Button {
            Content = txt, Height = 30, Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 0, 0),
            Background = bg, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };

        // ─────────────────────────────────────────────────────────────────
        var root = new Grid { Background = BrFondoGris };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });                           // Row 0: header oscuro
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Row 1: panel cabecera
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Row 2: panel agregar artículo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Row 3: hint editar artículo
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });         // Row 4: DataGrid
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });                          // Row 5: footer

        // ══ Row 0: HEADER OSCURO ════════════════════════════════════════
        var header = new Border {
            Background = BrHeaderVeryDark,
            Padding = new Thickness(16, 0, 16, 0) };

        var hGrid = new Grid();
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Ícono + título + subtítulo
        var titleSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        titleSp.Children.Add(new TextBlock {
            Text = "🛒", FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0) });
        var titleCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleCol.Children.Add(new TextBlock {
            Text = "Editar Compras",
            FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco });
        titleCol.Children.Add(new TextBlock {
            Text = "Revisión y aprobación de compras pendientes",
            FontSize = 10, Foreground = BrMuted });
        titleSp.Children.Add(titleCol);
        Grid.SetColumn(titleSp, 0); hGrid.Children.Add(titleSp);

        // Badges de estado + local de destino (centro-derecha del header)
        var badgeSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 12, 0) };

        // El selector de local destino vive en el panel de cabecera (fila 1, campo "LOCAL
        // DESTINO" junto a Comprobante/Factura/Parcial) — no acá. Primer intento lo puso
        // apretado dentro de este badge sobre el header oscuro (52px de alto, sin espacio real
        // para un ComboBox interactivo): el popup desplegable heredaba estilos por defecto que
        // no combinaban con el fondo oscuro, quedando con texto gris casi ilegible sobre hover
        // azul — feedback real del usuario ("es muy feo, no se entiende"). Un ComboBox es un
        // control de formulario, no un badge de estado; se lo trata como tal en el panel
        // blanco, con el mismo patrón visual que MÉTODO DE PAGO.

        var badgeBorder = new Border {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(50, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center };
        _lblEstadoBadge = new TextBlock {
            Text = "SIN COMPRA",
            FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = BrMuted };
        badgeBorder.Child = _lblEstadoBadge;
        badgeSp.Children.Add(badgeBorder);
        Grid.SetColumn(badgeSp, 1); hGrid.Children.Add(badgeSp);

        // Botón cerrar (derecha)
        var btnCerrarHdr = new Button {
            Content = "✕  Cerrar",
            Height = 32, Padding = new Thickness(14, 0, 14, 0),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(50, 255, 255, 255)),
            Foreground = BrBlanco,
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center };
        btnCerrarHdr.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrarHdr, 2); hGrid.Children.Add(btnCerrarHdr);

        header.Child = hGrid;
        Grid.SetRow(header, 0); root.Children.Add(header);

        // ══ Row 1: PANEL CABECERA (fondo blanco) ════════════════════════
        var cabPanel = new Border {
            Background = BrBlanco,
            BorderBrush = BrSep, BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 14, 16, 14),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 2, BlurRadius = 8, Opacity = 0.07,
                Color = System.Windows.Media.Colors.Black, Direction = 270 } };

        var cabMainGrid = new Grid();
        cabMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) }); // izq 60%
        cabMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });                    // separador
        cabMainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) }); // der 40%

        // ── Columna izquierda: grid 2x3 de campos ─────────────────────
        var leftGrid = new Grid { Margin = new Thickness(0, 0, 16, 0) };
        leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Campos fila 1: Comprobante (con botón buscar debajo, en su propia fila — puesto al
        // lado del campo se recortaba: la columna del Grid padre es 1* y el ancho combinado
        // TextBox+botón no entraba, WPF lo clipeaba silenciosamente dejando solo una letra
        // visible) | Factura | Parcial
        _txtInterno = TB(w: 175, ro: true);
        var buscarSp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        buscarSp.Children.Add(Lbl("COMPROBANTE"));
        buscarSp.Children.Add(_txtInterno);
        var btnBuscar = new Button {
            Content = "🔍 Buscar comprobante [F2]",
            Height = 28, HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 4, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE6, 0x51, 0x00)),
            Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 10.5,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnBuscar.Click += (_, _) => AbrirBuscadorComprobante();
        buscarSp.Children.Add(btnBuscar);
        Grid.SetColumn(buscarSp, 0); Grid.SetRow(buscarSp, 0); leftGrid.Children.Add(buscarSp);

        _txtFactura = TB(w: 120);
        var fieldFactura = Field("N° FACTURA", _txtFactura);
        Grid.SetColumn(fieldFactura, 1); Grid.SetRow(fieldFactura, 0); leftGrid.Children.Add(fieldFactura);

        _txtParcial = TB(w: 110, ro: true);
        var fieldParcial = Field("PARCIAL Gs.", _txtParcial);
        Grid.SetColumn(fieldParcial, 2); Grid.SetRow(fieldParcial, 0); leftGrid.Children.Add(fieldParcial);

        // Campos fila 2: Descuento | Total (grande/verde) | Método
        _txtDescuento = TB(w: 110);
        _txtDescuento.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        // Formatea con puntos de miles en cada tecleo (mismo patrón que TxtEfectivoRecibido/
        // TxtMontoParcial en CobrosWindow) — antes solo filtraba dígitos, dejando ver
        // "1000000" sin separadores mientras el resto de los montos de la pantalla (Total,
        // P.Costo/P.Venta del modal de ítem) sí los mostraban, inconsistente y difícil de leer
        // a simple vista con montos grandes.
        var formateandoDescuento = false;
        _txtDescuento.TextChanged += (_, _) => {
            if (formateandoDescuento) return;
            formateandoDescuento = true;
            var digitos = new string(_txtDescuento.Text.Where(char.IsDigit).ToArray());
            decimal.TryParse(digitos, out var monto);
            _txtDescuento.Text = monto == 0 ? "" : monto.ToString("N0").Replace(",", ".");
            _txtDescuento.CaretIndex = _txtDescuento.Text.Length;
            formateandoDescuento = false;
            RecalcTotal();
        };
        var fieldDesc = Field("DESCUENTO Gs.", _txtDescuento);
        Grid.SetColumn(fieldDesc, 0); Grid.SetRow(fieldDesc, 2); leftGrid.Children.Add(fieldDesc);

        _txtTotal = TB(w: 120, ro: true);
        _txtTotal.FontSize = 14;
        _txtTotal.FontWeight = FontWeights.Bold;
        _txtTotal.Foreground = BrVerde;
        var fieldTotal = Field("TOTAL Gs.", _txtTotal);
        Grid.SetColumn(fieldTotal, 1); Grid.SetRow(fieldTotal, 2); leftGrid.Children.Add(fieldTotal);

        _cboMetodo = new ComboBox {
            Width = 130, Height = 32, FontSize = 12,
            BorderBrush = BrSep, BorderThickness = new Thickness(1),
            Background = BrInputBg, VerticalContentAlignment = VerticalAlignment.Center };
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Efectivo",      Tag = (byte)1 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia", Tag = (byte)2 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Cheque",        Tag = (byte)3 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta",       Tag = (byte)4 });
        _cboMetodo.SelectedIndex = 0;
        var fieldMetodo = Field("MÉTODO DE PAGO", _cboMetodo);
        Grid.SetColumn(fieldMetodo, 2); Grid.SetRow(fieldMetodo, 2); leftGrid.Children.Add(fieldMetodo);

        Grid.SetColumn(leftGrid, 0); cabMainGrid.Children.Add(leftGrid);

        // Separador vertical
        var vSep = new Border {
            Background = BrSep, Width = 1,
            Margin = new Thickness(0, 4, 0, 4) };
        Grid.SetColumn(vSep, 1); cabMainGrid.Children.Add(vSep);

        // ── Columna derecha: Local destino | Proveedor | Banco | Nota ─
        var rightStack = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };

        // Local destino — dato que decide a qué local se le suma el stock al aprobar, así
        // que va primero y con un borde verde propio para diferenciarlo visualmente del resto
        // de los campos (mismo motivo que el badge del header original, pero ahora integrado
        // al formulario en vez de forzado dentro de una pill sobre fondo oscuro).
        var localDestBorder = new Border {
            BorderBrush = BrVerde, BorderThickness = new Thickness(1.5),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF9, 0xF1)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12) };
        var localDestSp = new StackPanel();
        localDestSp.Children.Add(new TextBlock {
            Text = "📍 LOCAL DESTINO DE LA COMPRA", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x5E, 0x20)),
            Margin = new Thickness(0, 0, 0, 4) });
        _txtLocalDestino = new TextBox {
            Width = 200, Padding = new Thickness(8, 6, 8, 6), FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 40, 20)),
            Background = BrBlanco,
            BorderBrush = BrVerde, BorderThickness = new Thickness(1),
            IsReadOnly = true,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "El stock de esta compra se sumará a este local al aprobarla" };
        var localDestRow = new StackPanel { Orientation = Orientation.Horizontal };
        localDestRow.Children.Add(_txtLocalDestino);
        var btnLocalDest = BtnAcc("📋 Seleccionar", BrVerde);
        btnLocalDest.Margin = new Thickness(6, 0, 0, 0);
        btnLocalDest.Click += (_, _) => AbrirBuscadorLocalDestino();
        localDestRow.Children.Add(btnLocalDest);
        localDestSp.Children.Add(localDestRow);
        localDestBorder.Child = localDestSp;
        rightStack.Children.Add(localDestBorder);

        // Proveedor
        _txtNomProv = TB(w: 200, ro: true);
        _txtIdProv  = new TextBox { Width = 0, Height = 0, Visibility = Visibility.Collapsed };
        var provSp  = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        provSp.Children.Add(Lbl("PROVEEDOR"));
        var provRow = new StackPanel { Orientation = Orientation.Horizontal };
        provRow.Children.Add(_txtNomProv);
        var btnProv = BtnAcc("Seleccionar [F11]", BrAzulAccion);
        btnProv.Margin = new Thickness(6, 0, 0, 0);
        btnProv.Click  += (_, _) => AbrirBuscadorProveedor();
        provRow.Children.Add(btnProv);
        provSp.Children.Add(provRow);
        rightStack.Children.Add(provSp);

        // Banco
        _txtNomBanco = TB(w: 200, ro: true);
        _txtIdBanco  = new TextBox { Width = 0, Height = 0, Visibility = Visibility.Collapsed, Text = "1" };
        var bancoSp  = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        bancoSp.Children.Add(Lbl("BANCO"));
        var bancoRow = new StackPanel { Orientation = Orientation.Horizontal };
        bancoRow.Children.Add(_txtNomBanco);
        var btnBanco = BtnAcc("Seleccionar [F10]", BrAzulMedio);
        btnBanco.Margin = new Thickness(6, 0, 0, 0);
        btnBanco.Click  += (_, _) => AbrirBuscadorBanco();
        bancoRow.Children.Add(btnBanco);
        bancoSp.Children.Add(bancoRow);
        rightStack.Children.Add(bancoSp);

        // Nota
        _txtNota = new TextBox {
            Padding = new Thickness(8, 6, 8, 6), FontSize = 12,
            BorderBrush = BrSep, BorderThickness = new Thickness(1),
            Background = BrInputBg, MinWidth = 200,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 40, 60)),
            VerticalContentAlignment = VerticalAlignment.Center };
        rightStack.Children.Add(Field("NOTA / OBSERVACIÓN", _txtNota, new Thickness(0)));

        Grid.SetColumn(rightStack, 2); cabMainGrid.Children.Add(rightStack);

        cabPanel.Child = cabMainGrid;
        Grid.SetRow(cabPanel, 1); root.Children.Add(cabPanel);

        // ══ Row 2: PANEL AGREGAR ARTÍCULO ═══════════════════════════════
        var artPanel = new Border {
            Background = BrFondoClaro,
            BorderBrush = BrSep, BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10) };

        var artRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        artRow.Children.Add(new TextBlock {
            Text = "Agregar artículo:",
            FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 67, 96)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0) });

        // TextBox código + botón buscar + botón ver lista
        _txtBuscarArt = new TextBox {
            Width = 140, Padding = new Thickness(8, 6, 8, 6), FontSize = 12,
            BorderBrush = BrAzulBase, BorderThickness = new Thickness(0, 0, 0, 2),
            Background = BrBlanco, VerticalContentAlignment = VerticalAlignment.Center };
        _txtBuscarArt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticuloEditar(); };
        artRow.Children.Add(_txtBuscarArt);

        var btnBuscarArt2 = new Button {
            Content = "Buscar [Enter]",
            Height = 32, Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(6, 0, 0, 0),
            Background = BrAzulBase, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnBuscarArt2.Click += async (_, _) => await BuscarArticuloEditar();
        artRow.Children.Add(btnBuscarArt2);

        var btnListaArt2 = new Button {
            Content = "Ver lista",
            Height = 32, Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(6, 0, 0, 0),
            Background = BrFondoGris, Foreground = BrAzulMedio,
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            BorderThickness = new Thickness(1), BorderBrush = BrSep,
            Cursor = Cursors.Hand };
        btnListaArt2.Click += (_, _) => AbrirBuscadorArticuloEditar();
        artRow.Children.Add(btnListaArt2);

        // Pill nombre artículo seleccionado
        var pillArt = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(209, 236, 252)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 160 };
        _lblNomArtEditar = new TextBlock {
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrAzulMedio,
            Text = "— sin artículo —",
            VerticalAlignment = VerticalAlignment.Center };
        pillArt.Child = _lblNomArtEditar;
        artRow.Children.Add(pillArt);

        // Separador + Cantidad + botón INSERTAR
        artRow.Children.Add(new Border {
            Width = 1, Background = BrSep,
            Margin = new Thickness(14, 2, 14, 2),
            VerticalAlignment = VerticalAlignment.Stretch });

        artRow.Children.Add(new TextBlock {
            Text = "Cantidad:",
            FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 67, 96)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0) });

        _txtCant = new TextBox {
            Width = 70, Padding = new Thickness(6, 6, 6, 6), FontSize = 14,
            FontWeight = FontWeights.Bold,
            BorderBrush = BrAzulBase, BorderThickness = new Thickness(0, 0, 0, 2),
            Background = BrBlanco, TextAlignment = TextAlignment.Center, Text = "1",
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtCant.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await InsertarArticuloEditar(); };
        artRow.Children.Add(_txtCant);

        var btnInsertar = new Button {
            Height = 36, Padding = new Thickness(18, 0, 18, 0),
            Margin = new Thickness(10, 0, 0, 0),
            Background = BrVerde, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnInsertar.Content = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = {
                new TextBlock { Text = "+ ", FontSize = 15, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "INSERTAR", FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }
            } };
        btnInsertar.Click += async (_, _) => await InsertarArticuloEditar();
        artRow.Children.Add(btnInsertar);

        artPanel.Child = artRow;
        Grid.SetRow(artPanel, 2); root.Children.Add(artPanel);

        // ══ Row 2b: HINT sobre la grilla ═════════════════════════════════
        // Texto guía explícito arriba de la tabla — antes la única pista de que un artículo
        // se puede editar era el hint chico "Doble click: Editar ítem" en la barra de atajos
        // del pie de la ventana (junto a otros 4 atajos), fácil de pasar por alto. Complementa
        // (no reemplaza) los botones de la columna Acciones: alguien que prefiera el mouse ve
        // el botón ✎ en la fila; el texto explica que doble click también funciona.
        var hintPanel = new Border {
            Background = BrFondoClaro,
            BorderBrush = BrSep, BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6) };
        hintPanel.Child = new TextBlock {
            Text = "✎  Hacé click en Editar (o doble click sobre la fila) para modificar cantidad y precios de un artículo",
            FontSize = 10.5, FontStyle = FontStyles.Italic,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 110, 130)) };
        Grid.SetRow(hintPanel, 3); root.Children.Add(hintPanel);

        // ══ Row 3: DataGrid ══════════════════════════════════════════════
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrSep,
            RowBackground = BrBlanco,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(245, 249, 252)),
            FontSize = 12, RowHeight = 38, ColumnHeaderHeight = 36,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = BuildGridHdrStyle()
        };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("Codigo"),      Width = 110 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",
            Binding = new System.Windows.Data.Binding("CantidadFmt"), Width = 70 });
        // Headers de precio con ícono de lápiz + tooltip: antes solo el hint "Doble click:
        // Editar ítem" al pie de la ventana (letra chica, junto a otros 4 atajos) avisaba que
        // estos valores se pueden modificar — fácil de pasar por alto. El ícono queda pegado
        // a la columna que realmente se edita, no escondido en un pie de página aparte.
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = HeaderEditable("P.Costo"),
            Binding = new System.Windows.Data.Binding("PCFmt"),       Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = HeaderEditable("P.Venta"),
            Binding = new System.Windows.Data.Binding("PVFmt"),       Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = HeaderEditable("P.Contado"),
            Binding = new System.Windows.Data.Binding("ContadoFmt"),  Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = HeaderEditable("P.Promo"),
            Binding = new System.Windows.Data.Binding("PPromoFmt"),   Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "SubTotal",
            Binding = new System.Windows.Data.Binding("SubtotalFmt"), Width = 120 });
        // Columna de botones — más descubrible que depender solo del doble click (que ni
        // siquiera queda indicado en la fila en sí) o del hint de texto arriba de la tabla.
        // Ambos botones toman el DataContext de su fila (la propia LineaCompraEdit) desde el
        // Click handler, no de _gridDetalle.SelectedItem, porque un click en el botón no
        // siempre dispara la selección de fila antes del evento.
        _gridDetalle.Columns.Add(BuildColumnaAcciones());
        _gridDetalle.MouseDoubleClick += (_, _) => EditarItemDetalle();
        // Fila resaltada al pasar el mouse — ya no es indispensable con el ícono en el
        // header, pero refuerza que la fila entera es "clickeable" para editar.
        _gridDetalle.RowStyle = BuildFilaClickeableStyle();
        _gridDetalle.KeyDown += (_, e) => {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && _gridDetalle.SelectedItem is LineaCompraEdit it)
                { _items.Remove(it); RecalcTotal(); }
        };
        _gridDetalle.ItemsSource = _items;
        Grid.SetRow(_gridDetalle, 4); root.Children.Add(_gridDetalle);

        // ══ Row 5: FOOTER OSCURO ════════════════════════════════════════
        var footer = new Border {
            Background = BrHeaderVeryDark,
            Padding = new Thickness(16, 0, 16, 0) };

        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Izquierda: total grande + atajos
        var footLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _lblTotal = new TextBlock {
            FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco };
        var lblAtajos = new TextBlock {
            Text = "F2: Buscar comprobante   F10: Banco   F11: Proveedor   Doble click: Editar ítem   Supr/Del: Eliminar ítem",
            FontSize = 9, Foreground = BrMuted,
            Margin = new Thickness(0, 3, 0, 0) };
        footLeft.Children.Add(_lblTotal);
        footLeft.Children.Add(lblAtajos);
        Grid.SetColumn(footLeft, 0); footGrid.Children.Add(footLeft);

        // Derecha: 3 botones
        var footBtns = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        // Botón "Guardar cambios" (azul)
        _btnGuardarMod = new Button {
            Content = "Guardar cambios",
            Height = 38, Padding = new Thickness(20, 0, 20, 0),
            Margin = new Thickness(0, 0, 6, 0),
            Background = BrAzulBase, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            IsEnabled = false };
        _btnGuardarMod.Click += async (_, _) => await GuardarModificacion();
        footBtns.Children.Add(_btnGuardarMod);

        // Separador visual antes del botón Confirmar
        footBtns.Children.Add(new Border {
            Width = 1,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
            Margin = new Thickness(0, 6, 8, 6) });

        // Botón "Confirmar compra" (verde) + texto auxiliar
        var confirmarStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _btnGuardarCompra = new Button {
            Height = 38, Padding = new Thickness(20, 0, 20, 0),
            Background = BrVerde, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            IsEnabled = false };
        _btnGuardarCompra.Content = new StackPanel {
            Orientation = Orientation.Horizontal,
            Children = {
                new TextBlock { Text = "✔  ", FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "Confirmar compra", VerticalAlignment = VerticalAlignment.Center }
            } };
        _btnGuardarCompra.ToolTip = "Mueve la compra de temporal a definitivo y actualiza precios y stock";
        _btnGuardarCompra.Click += async (_, _) => await GuardarCompra();
        var lblAdminNote = new TextBlock {
            Text = "Requiere autorización de administrador",
            FontSize = 8, Foreground = BrMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0) };
        confirmarStack.Children.Add(_btnGuardarCompra);
        confirmarStack.Children.Add(lblAdminNote);
        footBtns.Children.Add(confirmarStack);

        // Botón "Cerrar" (gris)
        var btnCerrarFoot = new Button {
            Content = "Cerrar",
            Height = 38, Padding = new Thickness(18, 0, 18, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Background = BrGris, Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrarFoot.Click += (_, _) => Close();
        footBtns.Children.Add(btnCerrarFoot);

        Grid.SetColumn(footBtns, 1); footGrid.Children.Add(footBtns);
        footer.Child = footGrid;
        Grid.SetRow(footer, 5); root.Children.Add(footer);

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
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26,79,110))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
            System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8,0,8,0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
        return s;
    }

    // Header con ícono de lápiz + tooltip para columnas de precio editables (ver comentario
    // en _gridDetalle.Columns.Add más arriba).
    private static object HeaderEditable(string texto)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, ToolTip = "Doble click en la fila para editar este valor" };
        sp.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock {
            Text = " ✎", FontSize = 11, Opacity = 0.85,
            VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    // Resalta la fila bajo el mouse y muestra tooltip "click para editar" — refuerza junto al
    // ícono en el header que toda la fila (no solo el precio) abre el modal de edición.
    private static Style BuildFilaClickeableStyle()
    {
        var s = new Style(typeof(DataGridRow));
        s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        s.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Doble click para editar cantidad y precios de este artículo"));
        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD))));
        s.Triggers.Add(trigger);
        return s;
    }

    // ControlTemplate reutilizable para los botones circulares chicos — se construye una sola
    // vez por color y se asigna vía SetValue(Control.TemplateProperty, ...) al FrameworkElementFactory
    // del Button en BuildColumnaAcciones. Sin reemplazar el template, WPF sigue aplicando el
    // chrome del botón nativo de Windows por encima de Width/Height/Padding=0, que no lo
    // centra bien en tamaños chicos y se veía "aplastado y desalineado" (feedback real). Mismo
    // criterio que ApplyFlatSearchStyle (BuscadorLocalModal) para el TextBox del buscador.
    private static ControlTemplate BuildBotonCircularTemplate(
        string glifoSegoeMdl2, System.Windows.Media.Color colorBase, System.Windows.Media.Color colorHover)
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "PART_Border");
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(colorBase));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        borderFactory.SetValue(FrameworkElement.WidthProperty, 26.0);
        borderFactory.SetValue(FrameworkElement.HeightProperty, 26.0);

        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.TextProperty, glifoSegoeMdl2);
        iconFactory.SetValue(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe MDL2 Assets"));
        iconFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        iconFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
        iconFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(iconFactory);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(colorHover), "PART_Border"));
        template.Triggers.Add(hoverTrigger);
        return template;
    }

    // Columna "Acciones" con botones Editar / Quitar por fila — pedido explícito para hacer
    // la edición más descubrible que depender solo del doble click (que no dejaba ninguna
    // pista visual en la fila de que era clickeable, más allá del cursor de mano y un
    // tooltip). Circulares con Template propio en vez de Button nativo con Width/Height
    // chicos: sin reemplazar el template, WPF sigue aplicando el chrome del botón del sistema
    // por encima, que no se centra bien en tamaños chicos y se veía "aplastado y desalineado"
    // (feedback real). El DataContext de cada botón es la propia LineaCompraEdit de esa fila
    // (WPF lo asigna automáticamente dentro de un DataGridTemplateColumn), así que no hace
    // falta pasar por _gridDetalle.SelectedItem.
    private DataGridTemplateColumn BuildColumnaAcciones()
    {
        var templateEditar = BuildBotonCircularTemplate("",
            System.Windows.Media.Color.FromRgb(0x1A, 0x4F, 0x6E),
            System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0xB8));
        var templateQuitar = BuildBotonCircularTemplate("",
            System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26),
            System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

        var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
        stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        stackFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        stackFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var btnEditarF = new FrameworkElementFactory(typeof(Button));
        btnEditarF.SetValue(Control.TemplateProperty, templateEditar);
        btnEditarF.SetValue(FrameworkElement.WidthProperty, 26.0);
        btnEditarF.SetValue(FrameworkElement.HeightProperty, 26.0);
        btnEditarF.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        btnEditarF.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        btnEditarF.SetValue(FrameworkElement.ToolTipProperty, "Editar cantidad y precios");
        btnEditarF.SetValue(Control.FocusableProperty, false);
        btnEditarF.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) => {
            if ((s as Button)?.DataContext is LineaCompraEdit item) EditarItemDetalle(item);
        }));
        stackFactory.AppendChild(btnEditarF);

        var btnQuitarF = new FrameworkElementFactory(typeof(Button));
        btnQuitarF.SetValue(Control.TemplateProperty, templateQuitar);
        btnQuitarF.SetValue(FrameworkElement.WidthProperty, 26.0);
        btnQuitarF.SetValue(FrameworkElement.HeightProperty, 26.0);
        btnQuitarF.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        btnQuitarF.SetValue(FrameworkElement.ToolTipProperty, "Quitar artículo de la compra");
        btnQuitarF.SetValue(Control.FocusableProperty, false);
        btnQuitarF.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) => {
            if ((s as Button)?.DataContext is LineaCompraEdit item) { _items.Remove(item); RecalcTotal(); }
        }));
        stackFactory.AppendChild(btnQuitarF);

        var template = new DataTemplate { VisualTree = stackFactory };
        return new DataGridTemplateColumn { Header = "Acciones", Width = 78, CellTemplate = template };
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
        var term    = _txtBuscarArt.Text.Trim();
        var idlocal = (byte)(_sesion.LocalActual?.IdLocal ?? 1);

        // Coincidencia exacta por código → selección directa sin abrir modal
        if (!string.IsNullOrEmpty(term))
        {
            try {
                using var conn = _db.Create();
                var p = new DynamicParameters();
                p.Add("@CODI",  term);
                p.Add("@Local", idlocal);
                p.Add("@msg",   dbType: DbType.String, direction: ParameterDirection.Output, size: 12);
                var rows = (await conn.QueryAsync<dynamic>("BUSCAR_ART_COMPRATEMPORAL_CS", p,
                    commandType: CommandType.StoredProcedure)).ToList();
                if (rows.Count == 1) {
                    dynamic a = rows[0];
                    _idArtEditar          = (int)a.ID;
                    _caEditar             = (string)a.CA;
                    _descEditar           = (string)a.D;
                    _txtBuscarArt.Text    = _caEditar;
                    _lblNomArtEditar.Text = _descEditar;
                    _txtCant.Focus(); _txtCant.SelectAll();
                    return;
                }
            } catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); return; }
        }

        // Sin coincidencia exacta o campo vacío → abrir modal completo con búsqueda
        var modal = new BuscadorArticuloModal(_db, idlocal) { Owner = this };
        if (!string.IsNullOrEmpty(term)) modal.PreFiltrar(term);
        if (modal.ShowDialog() == true && modal.ArticuloSeleccionado != null)
        {
            var a = modal.ArticuloSeleccionado;
            _idArtEditar          = a.IdArt;
            _caEditar             = a.Codigo;
            _descEditar           = a.Descripcion;
            _txtBuscarArt.Text    = a.Codigo;
            _lblNomArtEditar.Text = a.Descripcion;
            _txtCant.Focus(); _txtCant.SelectAll();
        }
    }

    private void AbrirBuscadorArticuloEditar()
    {
        var idlocal = (byte)(_sesion.LocalActual?.IdLocal ?? 1);
        var modal = new BuscadorArticuloModal(_db, idlocal) { Owner = this };
        if (modal.ShowDialog() == true && modal.ArticuloSeleccionado != null)
        {
            var a = modal.ArticuloSeleccionado;
            _idArtEditar          = a.IdArt;
            _caEditar             = a.Codigo;
            _descEditar           = a.Descripcion;
            _txtBuscarArt.Text    = a.Codigo;
            _lblNomArtEditar.Text = a.Descripcion;
            _txtCant.Focus(); _txtCant.SelectAll();
        }
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

            var linea = new LineaCompraEdit {
                IdArt = _idArtEditar, Codigo = _caEditar, Descripcion = _descEditar,
                Identificador = 0, Cantidad = cant,
                PrecioCosto = pc, PrecioVenta = pv, Contado = contado, PPromo = ppromo
            };
            _items.Add(linea);

            // Sin precio en el local de destino de ESTA compra (no el de sesión de quien
            // aprueba, ver _idLocalCompra) — alertar si existe precio cargado en otro local,
            // caso real: artículo activo con precio 0 en 14 de 15 locales, nadie se enteraba
            // hasta que un cliente reclamaba (TK886, LU-5004). Solo se dispara si el artículo
            // recién insertado sigue en 0 tanto en costo como en venta.
            if (pc == 0 && pv == 0)
                await OfrecerIgualarPreciosAsync(linea);
        }
        _idArtEditar = 0; _caEditar = ""; _descEditar = "";
        _txtBuscarArt.Text = ""; _txtCant.Text = "1";
        _txtBuscarArt.Focus();
        RecalcTotal();
    }

    private async Task OfrecerIgualarPreciosAsync(LineaCompraEdit linea)
    {
        try
        {
            var repo = App.Services.GetRequiredService<IArticuloRepository>();
            var precios = (await repo.ObtenerStockTodosLocalesAsync(linea.IdArt))
                .Where(p => p.Pventa > 0 || p.Pc > 0)
                .ToList();
            if (precios.Count == 0) return; // no hay precio cargado en ningún lado, nada que ofrecer

            using var conn = _db.Create();
            var todosLocales = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();

            var idUsuario = _sesion.UsuarioActual?.IdUsuario ?? 0;
            var nomMaquina = Environment.MachineName;

            var dlg = new IgualarPreciosDialog(repo, linea.IdArt, linea.Codigo, linea.Descripcion,
                precios, todosLocales, _idLocalCompra, idUsuario, nomMaquina) { Owner = this };
            dlg.ShowDialog();

            if (dlg.Resultado.Aplicado)
            {
                linea.PrecioCosto = dlg.Resultado.PrecioCosto;
                linea.PrecioVenta = dlg.Resultado.PrecioVenta;
                linea.Contado     = dlg.Resultado.Contado;
                linea.PPromo      = dlg.Resultado.PPromo;
            }
        }
        catch (Exception ex)
        {
            // No bloquear la carga de la compra si falla la sugerencia de precios —
            // el artículo ya se insertó con precio 0, el usuario puede seguir igual.
            MessageBox.Show($"No se pudo verificar precios en otros locales: {ex.Message}",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
                // STATUS<>2 excluye compras ya aprobadas — CAB_BUY_TMP ya no se borra al aprobar
                // (queda como evidencia permanente), así que sin este filtro el buscador mezclaría
                // solicitudes pendientes reales con compras ya cerradas hace tiempo.
                string sql = string.IsNullOrEmpty(filtro)
                    ? "SELECT TOP 100 INTERNO, FACTURA, FECHA FROM CAB_BUY_TMP WHERE ISNULL(STATUS,1)<>2 ORDER BY IDCABTMP DESC"
                    : "SELECT TOP 100 INTERNO, FACTURA, FECHA FROM CAB_BUY_TMP WHERE ISNULL(STATUS,1)<>2 AND (INTERNO LIKE @f OR FACTURA LIKE @f) ORDER BY IDCABTMP DESC";
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

            // STATUS=2 significa que esta solicitud ya fue aprobada anteriormente. Ya no se
            // borra CAB_BUY_TMP al aprobar (queda como evidencia permanente en STATUS=2), así
            // que hay que bloquear acá una segunda aprobación — de lo contrario JOGUAANETE_CS
            // ya no se usa, pero el reemplazo en C# duplicaría CAB_BUYS/stock si se re-confirma.
            if (ICol(cabD, "STATUS") == 2)
            {
                MessageBox.Show("Esta compra ya fue aprobada anteriormente. No se puede volver a confirmar.",
                    "Compra ya aprobada", MessageBoxButton.OK, MessageBoxImage.Warning);
                _idCabTmp = 0;
                _btnGuardarCompra.IsEnabled = false;
                _btnGuardarMod.IsEnabled    = false;
                return;
            }

            _idCabTmp      = ICol(cabD, "IDCABTMP");
            _interno       = SCol(cabD, "INTERNO");
            _idProv        = ICol(cabD, "IDP");
            _nombreProv    = SCol(cabD, "PROVEEDOR");
            _idBanco       = ICol(cabD, "ID_BANCO");
            _idLocalCompra = (byte)ICol(cabD, "LOCAL");
            // El SP no trae el nombre/código del local (solo el ID), se resuelve acá — el
            // usuario que aprueba necesita ver claramente a qué local va a impactar el stock,
            // dato que antes no aparecía en ningún lugar de esta pantalla.
            var localCompra = await conn.QueryFirstOrDefaultAsync<LocalItem>(
                "SELECT ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as Nombre FROM LOCALES WHERE ID_LOCAL=@id",
                new { id = _idLocalCompra });
            _nombreLocalCompra        = localCompra?.Nombre ?? $"Local {_idLocalCompra}";
            _localDestinoSeleccionado = localCompra;
            _txtLocalDestino.Text     = localCompra != null
                ? $"{localCompra.Codigo} — {localCompra.Nombre}"
                : _nombreLocalCompra;

            _txtInterno.Text  = _interno;
            _txtFactura.Text  = SCol(cabD, "FACTURA");
            _txtNota.Text     = SCol(cabD, "NOTA");
            _txtIdProv.Text   = _idProv.ToString();   // campo oculto, mantiene el ID para lógica interna
            _txtNomProv.Text  = _nombreProv;
            _txtIdBanco.Text  = _idBanco.ToString();  // campo oculto, mantiene el ID para lógica interna

            // Badge de estado: compra pendiente de aprobación
            _lblEstadoBadge.Text       = "● PENDIENTE DE APROBACIÓN";
            _lblEstadoBadge.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(133, 100, 4));
            var badgeParent = _lblEstadoBadge.Parent as System.Windows.Controls.Border;
            if (badgeParent != null)
                badgeParent.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 243, 205));

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

    // ── Doble click en ítem (o botón ✎ Editar de la columna Acciones) ──────
    private void EditarItemDetalle(LineaCompraEdit? itemParam = null)
    {
        var item = itemParam ?? _gridDetalle.SelectedItem as LineaCompraEdit;
        if (item is null) return;

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
        // Borde completo + fondo blanco (antes: solo borde inferior sobre fondo BrFondoArt,
        // que sobre el fondo ya claro de la ventana daba el aspecto de "línea de foco sin
        // recuadro" en vez de un campo de formulario claramente delimitado — feedback real).
        // FocusVisualStyle=null quita además el foco punteado/naranja por defecto de WPF que
        // se veía encima, mismo criterio que ApplyFlatSearchStyle (BuscadorLocalModal).
        TextBox BT(decimal val) {
            var tb = new TextBox {
                Text = val.ToString("N0").Replace(",","."),
                Height = 32, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8,0,8,0), VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = BrBorde, BorderThickness = new Thickness(1),
                Background = System.Windows.Media.Brushes.White,
                Foreground = BrPrimDark,
                TextAlignment = TextAlignment.Right, Width = 160,
                FocusVisualStyle = null
            };
            tb.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            tb.LostFocus        += (_, _) => AplicarMiles(tb);
            tb.GotFocus         += (_, _) => tb.BorderBrush = BrPrimary;
            tb.LostFocus        += (_, _) => tb.BorderBrush = BrBorde;
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

        // Campo "% sobre costo" junto a Precio Venta/Contado — mismo patrón (fórmula y
        // actualización bidireccional) que ya existe en NuevaCompraWindow (MkPctInput):
        // tipear el % calcula el precio, tipear el precio recalcula el %. El usuario ya usa
        // esos campos como una mini-calculadora al crear la compra; acá faltaba lo mismo al
        // editar un ítem ya cargado, donde solo se podía tipear el precio final a mano.
        var recalcPct = false;
        UIElement MkPctMini(TextBox precioTxt, out TextBox pctTxt)
        {
            pctTxt = new TextBox {
                Width = 60, Height = 24, FontSize = 10.5, TextAlignment = TextAlignment.Right,
                Padding = new Thickness(4,0,4,0), VerticalContentAlignment = VerticalAlignment.Center,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240,248,255)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(144,190,240)),
                BorderThickness = new Thickness(1),
                ToolTip = "% de ganancia sobre el precio costo — escribí acá o en el precio, se calculan entre sí" };
            pctTxt.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8,0,0,0) };
            row.Children.Add(new TextBlock { Text = "%↑costo", FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120,140,160)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) });
            row.Children.Add(pctTxt);
            var sufijo = new TextBlock { Text = "%", FontSize = 10.5, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21,101,192)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2,0,0,0) };
            row.Children.Add(sufijo);

            var localPct = pctTxt; var localPrecio = precioTxt;
            localPct.TextChanged += (_, __) => {
                if (recalcPct) return;
                var costo = Parse(txtPC);
                if (costo == 0) return;
                if (!decimal.TryParse(localPct.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct)) return;
                recalcPct = true;
                localPrecio.Text = Math.Round(costo * (1 + pct / 100m), 0).ToString("N0").Replace(",", ".");
                recalcPct = false;
            };
            localPrecio.TextChanged += (_, __) => {
                if (recalcPct) return;
                var costo = Parse(txtPC);
                var precio = Parse(localPrecio);
                if (costo == 0) return;
                recalcPct = true;
                localPct.Text = Math.Round((precio - costo) / costo * 100m, 1)
                    .ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                recalcPct = false;
            };
            return row;
        }
        var pctVentaRow   = MkPctMini(txtPV,      out var txtPctPV);
        var pctContadoRow = MkPctMini(txtContado, out var txtPctContado);
        // P.Promo también con su % — pedido explícito, mismo mini-calculadora que ya tienen
        // Venta/Contado. No es obligatorio completarlo (P.Promo puede quedar en 0 si el
        // artículo no tiene promoción), pero cuando sí se usa, la lógica es la misma: %
        // sobre costo.
        var pctPromoRow = MkPctMini(txtPPromo, out var txtPctPromo);
        // Inicializa el % mostrado a partir del precio ya cargado (no dispara recálculo del
        // precio porque recalcPct evita el ciclo, pero igual se activa el TextChanged de
        // txtPC más abajo — se recalculan los % al abrir el modal, valor correcto desde el
        // primer render en vez de arrancar en "0%").
        void RecalcPctIniciales() {
            var costo = Parse(txtPC);
            if (costo == 0) return;
            recalcPct = true;
            txtPctPV.Text      = Math.Round((Parse(txtPV) - costo) / costo * 100m, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            txtPctContado.Text = Math.Round((Parse(txtContado) - costo) / costo * 100m, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            txtPctPromo.Text   = Math.Round((Parse(txtPPromo) - costo) / costo * 100m, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            recalcPct = false;
        }
        RecalcPctIniciales();

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
        // Si cambia el costo, recalcula Venta/Contado manteniendo el % ya cargado — mismo
        // criterio que RecalcPctIniciales, evita que el usuario tenga que retipear el % cada
        // vez que ajusta el costo.
        txtPC.TextChanged += (_, _) => {
            if (recalcPct) return;
            var costo = Parse(txtPC);
            if (costo == 0) return;
            recalcPct = true;
            if (decimal.TryParse(txtPctPV.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pctV))
                txtPV.Text = Math.Round(costo * (1 + pctV / 100m), 0).ToString("N0").Replace(",", ".");
            if (decimal.TryParse(txtPctContado.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pctC))
                txtContado.Text = Math.Round(costo * (1 + pctC / 100m), 0).ToString("N0").Replace(",", ".");
            if (decimal.TryParse(txtPctPromo.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pctP))
                txtPPromo.Text = Math.Round(costo * (1 + pctP / 100m), 0).ToString("N0").Replace(",", ".");
            recalcPct = false;
        };
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

        // Precio venta / P. contado van junto con su mini-calculadora de % en la misma fila
        // (StackPanel horizontal) — el TextBox de precio mantiene su ancho fijo (160) para no
        // desalinear el resto de las filas, el % se agrega a la derecha.
        var rowPV      = new StackPanel { Orientation = Orientation.Horizontal };
        rowPV.Children.Add(txtPV); rowPV.Children.Add(pctVentaRow);
        var rowContado = new StackPanel { Orientation = Orientation.Horizontal };
        rowContado.Children.Add(txtContado); rowContado.Children.Add(pctContadoRow);
        var rowPromo   = new StackPanel { Orientation = Orientation.Horizontal };
        rowPromo.Children.Add(txtPPromo); rowPromo.Children.Add(pctPromoRow);

        GRC(BL("Cantidad"),      0, 0);  GRC(txtCant,    1, 0);
        GRC(BL("Precio costo"),  0, 2);  GRC(txtPC,      1, 2);
        GRC(BL("Precio venta"),  0, 4);  GRC(rowPV,      1, 4);
        GRC(BL("P. contado"),    0, 6);  GRC(rowContado, 1, 6);
        GRC(BL("Precio promo"),  0, 8);  GRC(rowPromo,   1, 8);

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

        byte metodo  = ((_cboMetodo.SelectedItem as ComboBoxItem)?.Tag is byte b) ? b : (byte)1;
        decimal.TryParse(new string((_txtDescuento.Text ?? "0").Where(char.IsDigit).ToArray()), out var descuento);
        var parcial    = _items.Sum(i => i.Subtotal);
        var totalFinal = parcial - descuento;
        if (totalFinal < 0) totalFinal = 0;
        var factura  = _txtFactura.Text.Trim();
        var nota     = _txtNota.Text.Trim();
        var idu      = _sesion.UsuarioActual?.IdUsuario ?? 1;
        var idlocal  = (byte)(_sesion.LocalActual?.IdLocal ?? 1);
        var idBanco  = _idBanco > 0 ? _idBanco : 1;

        _btnGuardarMod.IsEnabled = false;
        try {
            using var conn = _db.Create();

            // IDENTIFICADOR máximo actual para asignar a ítems nuevos
            var maxId = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COALESCE(MAX(IDENTIFICADOR),0) FROM DET_BUY_TMP WHERE IDCABTMP=@id",
                new { id = _idCabTmp });

            bool cabActualizada = false;

            foreach (var item in _items) {
                if (item.Identificador == 0) {
                    // ── Ítem NUEVO: INSERT directo en DET_BUY_TMP ──
                    // JEJOGUA_TMP_CS @AGENTE='NO' usa MAX(IDCABTMP) global — no sirve para compras existentes
                    maxId++;
                    item.Identificador = maxId;
                    var newDet = await conn.QueryFirstOrDefaultAsync<int>(
                        "SELECT COALESCE(MAX(IDDETTMP),0)+1 FROM DET_BUY_TMP");
                    await conn.ExecuteAsync(
                        @"INSERT INTO DET_BUY_TMP(IDDETTMP,IDCABTMP,IDART,CA,D,CANT,PC,PVENTA,CONTADO,PPROMO,IDENTIFICADOR)
                          VALUES (@det,@cab,@art,@ca,@d,@cant,@pc,@pv,@co,@pp,@id)",
                        new {
                            det  = newDet,
                            cab  = _idCabTmp,
                            art  = item.IdArt,
                            ca   = item.Codigo,
                            d    = item.Descripcion,
                            cant = item.Cantidad,
                            pc   = (int)Math.Truncate(item.PrecioCosto),
                            pv   = (int)Math.Truncate(item.PrecioVenta),
                            co   = (int)Math.Truncate(item.Contado),
                            pp   = (int)Math.Truncate(item.PPromo),
                            id   = maxId
                        });
                } else {
                    // ── Ítem EXISTENTE: actualizar con JAEDITA_BUY_TMP_CS ──
                    var pe = new DynamicParameters();
                    pe.Add("@AGENTE",        cabActualizada ? "NO" : "SI");
                    pe.Add("@IDCABTMP",      _idCabTmp);
                    pe.Add("@INTERNO",       _interno);
                    pe.Add("@FACTURA",       factura);
                    pe.Add("@PARCIAL",       SpDecimal.D90(parcial));
                    pe.Add("@DESCUENTO",     SpDecimal.D90(descuento));
                    pe.Add("@TOTAL",         SpDecimal.D90(totalFinal));
                    pe.Add("@FORMA",         (byte)1);
                    pe.Add("@METODO",        metodo);
                    pe.Add("@ID_BANCO",      idBanco);
                    pe.Add("@IDP",           _idProv);
                    pe.Add("@IDU",           idu);
                    pe.Add("@STATUS",        (byte)1);
                    pe.Add("@NOTA",          nota);
                    pe.Add("@IDART",         item.IdArt);
                    pe.Add("@CA",            item.Codigo);
                    pe.Add("@D",             item.Descripcion);
                    pe.Add("@CANT",          item.Cantidad);
                    pe.Add("@PC",            SpDecimal.D90(item.PrecioCosto));
                    pe.Add("@PVENTA",        SpDecimal.D90(item.PrecioVenta));
                    pe.Add("@CONTADO",       SpDecimal.D90(item.Contado));
                    pe.Add("@PPROMO",        SpDecimal.D90(item.PPromo));
                    pe.Add("@IDENTIFICADOR", item.Identificador);
                    pe.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 30);
                    await conn.ExecuteAsync("JAEDITA_BUY_TMP_CS", pe, commandType: CommandType.StoredProcedure);
                    var me = pe.Get<string>("@msg");
                    if (me != "GUARDADO") { MessageBox.Show($"Error actualizando artículo '{item.Codigo}': {me}"); return; }
                    cabActualizada = true;
                }
            }

            // Si no hubo ítems existentes, actualizar cabecera manualmente
            if (!cabActualizada) {
                await conn.ExecuteAsync(
                    @"UPDATE CAB_BUY_TMP SET FACTURA=@f, PARCIAL=@pa, DESCUENTO=@de, TOTAL=@to,
                      METODO=@me, ID_BANCO=@ib, IDP=@ip, IDU=@iu, NOTA=@no, FECHA=GETDATE()
                      WHERE IDCABTMP=@id",
                    new { f=factura, pa=(int)parcial, de=(int)descuento, to=(int)totalFinal,
                          me=metodo, ib=idBanco, ip=_idProv, iu=idu, no=nota, id=_idCabTmp });
            }

            MessageBox.Show("Modificación guardada correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarDetalleCompra(_interno);
        } catch (Exception ex) {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _btnGuardarMod.IsEnabled = true; }
    }

    // ── Modal permiso de usuario ──────────────────────────────────────────
    private record UsuarioPermiso(int IdUsuario, string Nombre);

    private async Task<UsuarioPermiso?> MostrarPermisoUsuario(Window owner)
    {
        var r = await CrediSoft.UI.Views.Shared.PermisoUsuariosModal.MostrarAsync(owner, _db);
        return r == null ? null : new UsuarioPermiso(r.IdUsuario, r.Nombre);
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
        var idlocal    = _idLocalCompra; // local de destino elegido al crear la compra, NO el local de sesión de quien aprueba

        _btnGuardarCompra.IsEnabled = false;
        _btnGuardarMod.IsEnabled    = false;
        try {
            using var conn = _db.Create();
            if (conn is not SqlConnection sqlConn)
                throw new InvalidOperationException("Se requiere SqlConnection para aprobar la compra.");
            if (sqlConn.State != ConnectionState.Open) await sqlConn.OpenAsync();

            using var tran = sqlConn.BeginTransaction();
            try
            {
                // Reemplaza JOGUAANETE_CS: mismo efecto (CAB_BUYS + DET_BUYS + PRICES + MOVART),
                // pero en una única transacción real (el SP original no tenía transacción) y sin
                // borrar CAB_BUY_TMP/DET_BUY_TMP — la solicitud original queda como evidencia
                // permanente (copiada a *_HIST y marcada STATUS=2, nunca eliminada).
                if (await conn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM CAB_BUYS WHERE COMPROBANTE=@c", new { c = _interno }, tran) > 0)
                { MessageBox.Show("Ya existe una compra con este comprobante."); tran.Rollback(); return; }

                if (await conn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM CAB_BUYS WHERE FACTURA=@f", new { f = factura }, tran) > 0)
                { MessageBox.Show("Ya existe una compra con esta factura."); tran.Rollback(); return; }

                int idCabBuys = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDCABBUYS),0)+1 FROM CAB_BUYS", transaction: tran);

                await conn.ExecuteAsync(
                    "INSERT INTO CAB_BUYS(IDCABBUYS, COMPROBANTE, FACTURA, PARCIAL, PUNITORIO, DESCUENTO, SUBTOTAL, HABER, TOTALFINAL, FORMA, METODO, ID_BANCO, IDP, IDU, FECHA, VTO, ESTADO, ID_LOCAL, NOTA) " +
                    "VALUES (@IDCABBUYS, @COMPROBANTE, @FACTURA, '0', 0, @DESCUENTO, @SUBTOTAL, 0, @TOTALFINAL, 1, @METODO, @ID_BANCO, @IDP, @IDU, GETDATE(), GETDATE(), 1, @ID_LOCAL, @NOTA)",
                    new {
                        IDCABBUYS = idCabBuys, COMPROBANTE = _interno, FACTURA = factura,
                        DESCUENTO = SpDecimal.D90(descuento), SUBTOTAL = SpDecimal.D90(parcial),
                        TOTALFINAL = SpDecimal.D90(totalFinal), METODO = metodo,
                        ID_BANCO = _idBanco > 0 ? _idBanco : 1, IDP = _idProv, IDU = idu, ID_LOCAL = idlocal, NOTA = nota
                    }, tran);

                for (int i = 0; i < _items.Count; i++) {
                    var item = _items[i];

                    decimal stini = await conn.ExecuteScalarAsync<decimal?>(
                        "SELECT S FROM PRICES WHERE IDART=@id AND IDLOCAL=@loc",
                        new { id = item.IdArt, loc = idlocal }, tran) ?? 0m;

                    int idDetBuys = await conn.ExecuteScalarAsync<int>(
                        "SELECT ISNULL(MAX(IDDETBUYS),0)+1 FROM DET_BUYS", transaction: tran);
                    await conn.ExecuteAsync(
                        "INSERT INTO DET_BUYS(IDDETBUYS, IDCABBUYS, IDENTIFICADOR, IDART, PC, CANTIDAD) " +
                        "VALUES (@IDDETBUYS, @IDCABBUYS, @IDENTIFICADOR, @IDART, @PC, @CANTIDAD)",
                        new {
                            IDDETBUYS = idDetBuys, IDCABBUYS = idCabBuys, IDENTIFICADOR = item.Identificador,
                            IDART = item.IdArt, PC = SpDecimal.D90(item.PrecioCosto), CANTIDAD = item.Cantidad
                        }, tran);

                    // Precio/promoción son por-local (mismo criterio que ActualizarPreciosAsync,
                    // usado en el resto de la app) — sin el filtro IDLOCAL este UPDATE pisaba el
                    // precio y la promoción en TODOS los locales donde existe el artículo, no solo
                    // en el local de destino de esta compra.
                    await conn.ExecuteAsync(
                        "UPDATE PRICES SET PC=@PC, PVENTA=@PVENTA, PPROMO=@PPROMO, CONTADO=@CONTADO, " +
                        "FCOMPRA=GETDATE(), FMS=GETDATE(), IDUCOMPRA=@IDU, IDUMODSTOCK=@IDU WHERE IDART=@IDART AND IDLOCAL=@IDLOCAL",
                        new {
                            PC = SpDecimal.D90(item.PrecioCosto), PVENTA = SpDecimal.D90(item.PrecioVenta),
                            PPROMO = SpDecimal.D90(item.PPromo), CONTADO = SpDecimal.D90(item.Contado),
                            IDU = idu, IDART = item.IdArt, IDLOCAL = idlocal
                        }, tran);

                    // DELETADO=0 al sumar stock: si el artículo estaba marcado como eliminado en
                    // este local (de una limpieza de catálogo anterior, por ejemplo) y ahora entra
                    // stock nuevo por compra, tiene que volver a quedar visible/vendible — bug real
                    // encontrado: el stock se sumaba correctamente (S=6) pero el artículo seguía
                    // invisible en "Ver Artículos" y no vendible, porque nadie reactivaba el flag.
                    await conn.ExecuteAsync(
                        "UPDATE PRICES SET S = S + @CANTIDAD, DELETADO = 0 WHERE IDART=@IDART AND IDLOCAL=@IDLOCAL",
                        new { CANTIDAD = item.Cantidad, IDART = item.IdArt, IDLOCAL = idlocal }, tran);

                    int idMovArt = await conn.ExecuteScalarAsync<int>(
                        "SELECT ISNULL(MAX(IDMOVART),0)+1 FROM MOVART", transaction: tran);
                    await conn.ExecuteAsync(
                        "INSERT INTO MOVART(IDMOVART, IDART, MOV, MOD, STINI, CANT, IDLOCAL, IDDESTINO, PCANT, PCACT, IDU, FECHA) " +
                        "VALUES (@IDMOVART, @IDART, 4, 1, @STINI, @CANT, @IDLOCAL, @IDLOCAL, @PCANT, @PCACT, @IDU, GETDATE())",
                        new {
                            IDMOVART = idMovArt, IDART = item.IdArt, STINI = SpDecimal.D93(stini),
                            CANT = item.Cantidad, IDLOCAL = idlocal, PCANT = SpDecimal.D90(0),
                            PCACT = SpDecimal.D90(item.PrecioCosto), IDU = idu
                        }, tran);
                }

                // Evidencia permanente: copia de la solicitud original a *_HIST (con IDLOCAL
                // real elegido al crearla) y marcado como aprobada — NUNCA se borra CAB_BUY_TMP.
                await conn.ExecuteAsync(
                    "INSERT INTO CAB_BUY_TMP_HIST (IDCABTMP, INTERNO, FACTURA, PARCIAL, DESCUENTO, TOTAL, FORMA, METODO, ID_BANCO, IDP, IDU, FECHA, STATUS, NOTA, IDLOCAL, IDCABBUYS, IDU_APROBADOR) " +
                    "SELECT IDCABTMP, INTERNO, FACTURA, PARCIAL, DESCUENTO, TOTAL, FORMA, METODO, ID_BANCO, IDP, IDU, FECHA, STATUS, NOTA, IDLOCAL, @IDCABBUYS, @IDU_APROBADOR " +
                    "FROM CAB_BUY_TMP WHERE IDCABTMP=@IDCABTMP",
                    new { IDCABTMP = _idCabTmp, IDCABBUYS = idCabBuys, IDU_APROBADOR = idu }, tran);

                await conn.ExecuteAsync(
                    "INSERT INTO DET_BUY_TMP_HIST (IDDETTMP, IDCABTMP, IDART, CA, D, CANT, PC, PVENTA, CONTADO, PPROMO, IDENTIFICADOR) " +
                    "SELECT IDDETTMP, IDCABTMP, IDART, CA, D, CANT, PC, PVENTA, CONTADO, PPROMO, IDENTIFICADOR " +
                    "FROM DET_BUY_TMP WHERE IDCABTMP=@IDCABTMP",
                    new { IDCABTMP = _idCabTmp }, tran);

                await conn.ExecuteAsync(
                    "UPDATE CAB_BUY_TMP SET STATUS=2 WHERE IDCABTMP=@IDCABTMP",
                    new { IDCABTMP = _idCabTmp }, tran);

                tran.Commit();
            }
            catch { tran.Rollback(); throw; }

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
            _localDestinoSeleccionado = null;
            _txtLocalDestino.Text     = "";
            // Resetear badge
            _lblEstadoBadge.Text       = "SIN COMPRA";
            _lblEstadoBadge.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(127, 179, 211));
            var badgeParentReset = _lblEstadoBadge.Parent as System.Windows.Controls.Border;
            if (badgeParentReset != null)
                badgeParentReset.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(50, 255, 255, 255));
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
