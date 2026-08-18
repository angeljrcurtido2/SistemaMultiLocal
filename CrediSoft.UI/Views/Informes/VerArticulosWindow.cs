using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CrediSoft.UI.Views.Informes;

public class VisorRow
{
    public int       IdArt      { get; set; }
    public string    Codigo     { get; set; } = string.Empty;
    public string    Desc       { get; set; } = string.Empty;
    public string    Marca      { get; set; } = string.Empty;
    public decimal   Contado    { get; set; }
    public decimal   PVenta     { get; set; }
    public decimal   Pc         { get; set; }
    public int       MaxCuota   { get; set; }
    public byte      SoloContado { get; set; }   // 1 = solo contado, 0 = crédito/ambos
    public byte      Activo     { get; set; } = 1; // ARTICULOS.ES — 1 = activo, 0 = inactivo
    public decimal[] Stocks     { get; set; } = Array.Empty<decimal>();
    // Cuando viene de paginación BD, StockTotal ya viene calculado
    public decimal   TotalStockOverride { get; set; } = -1;
    public decimal   TotalStock => TotalStockOverride >= 0 ? TotalStockOverride
                                 : Stocks.Length == 0 ? 0 : Stocks.Sum();

    // Plan de pagos configurado en el simulador (modo selector) — se completa recién al
    // confirmar "Seleccionar artículo" en SeleccionarArticuloAsync, con los valores que el
    // usuario cargó en el panel derecho (% Descuento, Entrega, % Recargo, Cant. Cuotas).
    public decimal PctDescuentoPlan { get; set; }
    public decimal EntregaPlan      { get; set; }
    public decimal PctRecargoPlan   { get; set; }
    public int     CantCuotasPlan   { get; set; }
    public decimal ValorNetoPlan    { get; set; }   // precio ya con el % descuento aplicado
}

public class StockIndexConverter : IValueConverter
{
    public int Index { get; set; }
    private static readonly CultureInfo _cult = CultureInfo.GetCultureInfo("es-AR");
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VisorRow row && Index < row.Stocks.Length)
            return row.Stocks[Index].ToString("N0", _cult);
        return "0";
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class VerArticulosWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly ILocalRepository    _localRepo;

    // Modo selector: cuando se abre desde VentaCreditoWindow
    private readonly bool _modoSelector;
    private readonly byte _idLocalFiltro;  // 0 = todos los locales
    // Selector "simple" — sin panel de Stock por Local ni Plan de Pagos/Simulador, solo
    // grilla + buscador + paginador. Reutiliza toda la lógica de carga paginada server-side
    // ya optimizada de este mismo componente (antes solo accesible desde el modo selector de
    // Venta a Crédito, que sí necesita esos dos paneles) en vez de duplicarla en un diálogo
    // aparte — reemplaza a BuscarArticuloDialog, que cargaba los ~12.000 artículos completos
    // de una sola vez al abrir (sin paginar en el servidor), reportado como lento. No filtra
    // por local: busca en todos, igual que BuscarArticuloDialog.
    private readonly bool _modoSimple;
    public  VisorRow? ArticuloSeleccionado { get; private set; }

    private List<Local>    _locales   = new();
    // IdLocal -> posición en _locales/VisorRow.Stocks[] — se arma una sola vez al cargar
    // _locales para que el índice de cada columna dinámica de stock-por-local no dependa de
    // recalcular una búsqueda O(n) por cada celda.
    private Dictionary<int, int> _idLocalAIndice = new();

    // paginación BD
    private int  _paginaActual  = 1;
    // Bajado de 50 a 30, y luego a 15: menos filas por página aliviana tanto el round-trip
    // de red como el render del DataGrid (columnas dinámicas de stock por local incluidas)
    // en las PCs de sucursal más flojas — el cuello de botella reportado no era solo la red,
    // sino también el costo de UI de pintar cada página. Con más navegación entre páginas
    // como contrapartida (catálogo de ~12.000 artículos), pero priorizando que cada carga se
    // sienta liviana — pedido explícito de optimización general del buscador.
    private int  _porPagina     = 15;
    private int  _totalRegistros = 0;
    private int  TotalPaginas => _porPagina == int.MaxValue ? 1 : Math.Max(1, (int)Math.Ceiling(_totalRegistros / (double)_porPagina));
    private bool _cargando = false;
    private System.Threading.CancellationTokenSource? _filterCts;

    // controles de paginación en footer
    private TextBlock _lblTotal    = null!;
    private Border    _btnPrev     = null!;
    private Border    _btnNext     = null!;
    private TextBlock _lblPagina   = null!;
    private readonly List<(Border chip, TextBlock txt, int val)> _pagChips = new();

    // búsqueda
    private TextBox     _txtBuscar       = null!;
    private ComboBox    _cboModo         = null!;
    private ComboBox    _cboFiltroStock  = null!;
    private string      _filtroStock     = "con"; // "todos" | "con" | "sin"

    private ComboBox    _cboFiltroTipo   = null!;
    private string      _filtroTipo      = "todos"; // "todos" | "contado" | "credito" — en modo selector arranca en "credito" (ver constructor)
    private DataGrid    _grid            = null!;

    // panel derecho
    private TextBlock _panelCodigo   = null!;
    private TextBlock _panelDesc     = null!;
    private TextBlock _panelContado  = null!;
    private TextBlock _panelPventa   = null!;
    private TextBlock _panelMaxC     = null!;
    private DataGrid  _dgStockLocal  = null!;
    private Border    _stockBadge    = null!;
    private TextBlock _stockBadgeTxt = null!;

    // plan de pago
    private TextBox   _txtPctDesc      = null!;
    private TextBox   _txtEntrega      = null!;
    private TextBlock _lnkEntregaSugerida = null!;
    private TextBox   _txtPctRec       = null!;
    private TextBox   _txtCantCuotas   = null!;
    private TextBlock _lblCantCuotas   = null!;   // label dinámico "Cant. Cuotas (máx. N)"
    private TextBlock _outDescuento    = null!;
    private TextBlock _outValor        = null!;
    private TextBlock _outValorSaldo   = null!;
    private TextBlock _outCuotaSinInteres = null!;
    private TextBlock _outValorMensual = null!;
    private TextBlock _outValorFinal   = null!;

    private decimal _precioSel          = 0;
    private int     _maxCuotaSeleccionado = 0;
    private bool    _calculando         = false;
    // true cuando el cajero tipeó Entrega a mano — a partir de ahí se deja de recalcularla
    // automáticamente al cambiar % Descuento/Cant. Cuotas, para no pisar lo que ya cargó.
    private bool    _entregaEditadaManualmente = false;
    // Pedido explícito del cliente: en 3-10 y 11+ cuotas el vendedor puede subir el % Recargo a
    // mano (para negociar/ganar más), pero nunca por debajo de 4,2% en esos dos rangos — ni
    // siquiera en 11+ cuotas, que hoy sugiere 4% (por debajo del piso). 1-2 cuotas NO se toca:
    // sigue en 0% fijo, no editable, sin piso. Mismo patrón que _entregaEditadaManualmente.
    private bool    _recargoEditadoManualmente = false;

    // % Recargo sugerido según la cantidad de cuotas — el sistema viejo (CrediSoft_local.exe)
    // usaba 4,5% (verificado en vivo: Precio 11.500.000, 3 cuotas, Entrega 3.833.333 → Valor
    // mensual 2.900.556, Valor Final 8.701.667, exacto con 4,5%), pero la dirección de
    // Credimar pidió expresamente bajar la tasa a 4,2% en el sistema nuevo — no es un error de
    // cálculo, es una decisión de negocio. No persiste en ninguna tabla ni columna de la base
    // de datos, ni la actual ni la vieja:
    //   1-2 cuotas  -> 0%   (1 entrega + 1 cuota sin interés — la entrega debe ser del
    //                        monto de una cuota para que no se cobre el recargo)
    //   3-10 cuotas -> 4,2%
    //   11+ cuotas  -> 4%
    private static decimal PctRecargoSugerido(int cuotas) => cuotas switch
    {
        <= 2  => 0m,
        <= 10 => 4.2m,
        _     => 4m,
    };

    private static readonly CultureInfo _cult = CultureInfo.GetCultureInfo("es-AR");

    // decimal.TryParse con NumberStyles.Any/_cult trata un punto como separador de MILES, no
    // decimal: "4.8" se parsea silenciosamente como 48, sin fallar — bug real reportado (el
    // cajero tipeó "4.8" con punto en % Recargo; el simulador seguía mostrando bien, pero al
    // trasladarlo a la venta el valor real guardado no era el que se veía en pantalla). El
    // cajero puede tipear con punto o coma indistintamente por costumbre; esto acepta ambos de
    // forma explícita para valores de PORCENTAJE (no montos, que no tienen esta ambigüedad
    // porque solo importan los dígitos enteros).
    private static bool TryParsePorcentaje(string? s, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var normalizado = s.Trim().Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out valor);
    }

    // Paleta
    private static readonly Color C1 = Color.FromRgb( 18,  78, 120);  // azul marino
    private static readonly Color C2 = Color.FromRgb( 31, 119, 180);  // azul medio
    private static readonly Color C3 = Color.FromRgb(210, 235, 252);  // azul hielo
    private static readonly Color C4 = Color.FromRgb(240, 248, 255);  // azul xclaro fondo
    private static readonly Color C5 = Color.FromRgb(192,  40,  27);  // rojo
    private static readonly Color C6 = Color.FromRgb(252, 228, 224);  // rojo hielo
    private static readonly Color CW = Colors.White;
    private static readonly Color CF = Color.FromRgb(248, 250, 252);  // fondo general
    private static readonly Color CB = Color.FromRgb(218, 225, 234);  // borde
    private static readonly Color CT = Color.FromRgb( 20,  33,  48);  // texto oscuro
    private static readonly Color CS = Color.FromRgb( 90, 107, 124);  // texto suave

    private static SolidColorBrush B(Color c) => new(c);

    public VerArticulosWindow()
    {
        _artRepo   = App.Services.GetRequiredService<IArticuloRepository>();
        _localRepo = App.Services.GetRequiredService<ILocalRepository>();
        _modoSelector  = false;
        _idLocalFiltro = 0;
        Title  = "Artículos / Mercaderías";
        // Pedido explícito: replicar el comportamiento del sistema legacy, donde "Ver
        // Artículos" (grilla + simulador de plan de pago al costado) ocupa todo el espacio
        // libre de la pantalla en vez de un tamaño fijo centrado — con la grilla de
        // artículos y el simulador lado a lado, un tamaño chico deja muy poco lugar para
        // ambos a la vez. El modo selector (constructor de abajo, modal desde Venta a
        // Crédito) NO cambia: ese sigue siendo un diálogo acotado sobre otra ventana.
        //
        // WindowStyle=None — pedido explícito de que se vea "como una pestaña de navegador
        // pegada arriba", sin el marco/barra de título nativo de Windows con sus propios
        // botones _ ☐ X flotando como una ventana más encima de MainWindow. El tamaño y
        // posición reales los fija MainWindow.AnclarAAreaDeContenido() apenas se abre esta
        // ventana (la ancla exacto al área de contenido bajo su menú, y la resincroniza si
        // MainWindow se mueve/redimensiona) — esto de acá es solo el valor inicial de
        // respaldo por si algún día se abre sin ese anclaje.
        // NoResize — con CanResize el usuario podía arrastrar los bordes y "despegar" la
        // ventana del área que le asigna MainWindow (que es quien controla su tamaño/
        // posición por completo acá, no el usuario). Sin barra de título tampoco hay forma
        // de arrastrarla por drag — solo puede moverla/cerrarla MainWindow.
        WindowStyle = WindowStyle.None;
        ResizeMode  = ResizeMode.NoResize;
        // MinHeight bajo — MainWindow.AnclarAAreaDeContenido() fija la altura real después de
        // este constructor, y si el área de contenido disponible (bajo menú/toolbars, sobre
        // status bar) es menor a MinHeight, WPF fuerza igual MinHeight y la ventana se
        // desborda por debajo de la pantalla, tapando el simulador (bug real reportado: se
        // veía cortado el panel incluso con MainWindow maximizada).
        MinWidth = 900; MinHeight = 400;
        var area = SystemParameters.WorkArea;
        Left = area.Left; Top = area.Top;
        Width = area.Width; Height = area.Height;
        Background = B(CF);
        Content = Build();
        Loaded += async (_, __) => await CargarAsync();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    // Constructor para modo selector — abre como modal desde VentaCreditoWindow
    public VerArticulosWindow(byte idLocal, Window owner)
    {
        _artRepo   = App.Services.GetRequiredService<IArticuloRepository>();
        _localRepo = App.Services.GetRequiredService<ILocalRepository>();
        _modoSelector  = true;
        _idLocalFiltro = idLocal;
        // Este selector solo se usa desde Venta a Crédito — arranca filtrado en "Admite
        // crédito" para que los artículos "solo contado" ni aparezcan de entrada, en vez de
        // dejar que el cajero los vea y tenga que descubrir el bloqueo recién al seleccionarlos.
        _filtroTipo = "credito";
        Title  = "Seleccionar Artículo";
        // Height/MinHeight fijos excedían el área de trabajo en pantallas de 1366x768.
        // Ancho ampliado (antes 1020 fijo) — mismo diseño que el modo anclado, con columnas
        // de stock por local + scroll horizontal, aprovecha más pantalla si hay espacio.
        var altoDisponibleSel = SystemParameters.WorkArea.Height - 20;
        var anchoDisponibleSel = SystemParameters.WorkArea.Width - 40;
        Width  = Math.Min(1300, anchoDisponibleSel); Height = Math.Min(760, altoDisponibleSel);
        MinWidth = 900;  MinHeight = Math.Min(660, altoDisponibleSel);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner  = owner;
        Background = B(CF);
        Content = Build();
        Loaded += async (_, __) => await CargarAsync();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    // Constructor para selector SIMPLE (sin panel de Stock por Local ni Plan de Pagos) — usado
    // desde Compras (Nueva Compra / Editar Compras) y desde Artículos > Precios, en reemplazo
    // de BuscarArticuloDialog. No filtra por local ni por tipo (contado/crédito): cualquier
    // artículo activo puede aparecer, igual que el diálogo que reemplaza.
    public VerArticulosWindow(Window owner)
    {
        _artRepo   = App.Services.GetRequiredService<IArticuloRepository>();
        _localRepo = App.Services.GetRequiredService<ILocalRepository>();
        _modoSelector  = true;
        _modoSimple    = true;
        _idLocalFiltro = 0;
        _filtroTipo    = "todos";
        // A diferencia del selector de Venta a Crédito, acá se usa para elegir CUALQUIER
        // artículo (Compras, Precios) — incluyendo uno recién dado de alta sin stock todavía,
        // así que arranca en "Todos" en vez de "Con stock".
        _filtroStock   = "todos";
        Title  = "Seleccionar Artículo";
        // Más chico que el selector de Venta a Crédito a propósito — sin los paneles de Stock
        // por Local/Plan de Pagos no hace falta tanto ancho, y un modal más compacto es más
        // rápido de escanear para este uso puntual (elegir un artículo, nada más).
        var altoDisponibleSel  = SystemParameters.WorkArea.Height - 40;
        var anchoDisponibleSel = SystemParameters.WorkArea.Width  - 80;
        Width  = Math.Min(760, anchoDisponibleSel); Height = Math.Min(560, altoDisponibleSel);
        MinWidth = 620; MinHeight = Math.Min(420, altoDisponibleSel);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner  = owner;
        Background = B(CF);
        Content = Build();
        Loaded += async (_, __) => await CargarAsync();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    // ════════════════════════════════════════════════════════════════════════
    private UIElement Build()
    {
        var root = new Grid();
        // Modo normal (anclado bajo la barra de pestañas de MainWindow): sin header propio
        // — la pestaña "Ver Artículos" de arriba ya cumple esa función de título, y quitar
        // este header le da más alto real a la grilla/simulador (pedido explícito). El
        // modo selector (diálogo modal independiente desde Venta a Crédito) sí necesita su
        // propio header, porque ahí no hay ninguna barra de pestañas visible.
        var incluirHeader = _modoSelector;
        if (incluirHeader)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var hdr = Header(); Grid.SetRow(hdr, 0); root.Children.Add(hdr);
        }
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var offset = incluirHeader ? 1 : 0;
        // La barra de búsqueda ahora vive DENTRO de Body(), pegada solo a la columna de la
        // grilla — así los paneles ARTÍCULO SELECCIONADO / SIMULADOR arrancan desde arriba
        // del todo, en vez de dejar un hueco vacío al costado de la barra de filtros
        // (pedido explícito: usar ese espacio para levantar más esos dos paneles).
        var body = Body();     Grid.SetRow(body, offset);     root.Children.Add(body);
        var foot = Footer();   Grid.SetRow(foot, offset + 1); root.Children.Add(foot);
        return root;
    }

    // ── HEADER ──────────────────────────────────────────────────────────────
    private UIElement Header()
    {
        // Header reducido al mínimo — con la barra de pestañas nueva arriba de esta
        // ventana, cada píxel de alto que este header ahorra es más espacio real para la
        // grilla/simulador debajo (pedido explícito de aprovechar mejor el espacio).
        var hdr = new Border { Background = B(C1), Padding = new Thickness(14, 6, 14, 6) };
        var dp  = new DockPanel();

        // lado izquierdo: línea vertical + textos
        var accent = new Border
        {
            Width = 3, CornerRadius = new CornerRadius(2),
            Background = B(C2), Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        DockPanel.SetDock(accent, Dock.Left);
        dp.Children.Add(accent);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal };
        texts.Children.Add(new TextBlock
        {
            Text = "ARTÍCULOS / MERCADERÍAS",
            FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        });
        texts.Children.Add(new TextBlock
        {
            Text = "  ·  Consulta de precios, stock por local y simulador de plan de pago",
            FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        dp.Children.Add(texts);

        // Botón cerrar propio — con WindowStyle=None (sin marco nativo de Windows, para que
        // la ventana quede "pegada" sin su propia barra de título/botones _ ☐ X flotando
        // encima de MainWindow) ya no existe el botón X nativo. Escape también cierra
        // (ver PreviewKeyDown en el constructor).
        var btnCerrar = new Button
        {
            Content = "✕", FontSize = 14, FontWeight = FontWeights.Bold,
            Width = 32, Height = 32, Background = Brushes.Transparent,
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
        };
        btnCerrar.Click += (_, _) => Close();
        DockPanel.SetDock(btnCerrar, Dock.Right);
        dp.Children.Add(btnCerrar);

        hdr.Child = dp;
        return hdr;
    }

    // ── BARRA BÚSQUEDA ───────────────────────────────────────────────────────
    private UIElement SearchBar()
    {
        var bar = new Border
        {
            Background      = Brushes.White,
            BorderBrush     = B(CB), BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(14, 5, 14, 5),
        };

        // Contenedor pill que agrupa selector + separador + input
        var pill = new Border
        {
            CornerRadius    = new CornerRadius(24),
            BorderBrush     = B(CB), BorderThickness = new Thickness(1.5),
            Background      = B(C4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var pillRow = new StackPanel { Orientation = Orientation.Horizontal };

        // Selector de modo (izquierda del pill)
        _cboModo = new ComboBox
        {
            BorderThickness = new Thickness(0),
            Background      = Brushes.Transparent,
            FontSize        = _modoSimple ? 10.5 : 11,
            FontWeight      = FontWeights.SemiBold,
            Foreground      = B(C1),
            Padding         = new Thickness(_modoSimple ? 10 : 14, 8, 6, 8),
            Width           = _modoSimple ? 96 : 118,
            FocusVisualStyle = null,
        };
        _cboModo.Items.Add(new ComboBoxItem { Content = "Código",      Tag = "codigo",      IsSelected = true });
        _cboModo.Items.Add(new ComboBoxItem { Content = "Descripción", Tag = "descripcion" });
        _cboModo.Items.Add(new ComboBoxItem { Content = "Marca",       Tag = "marca" });
        _cboModo.SelectedIndex = 0;
        _cboModo.SelectionChanged += (_, __) =>
        {
            // Con término vacío, el modo (descripción/marca/código) no cambia el WHERE — ver
            // FiltrarGrid: la condición LIKE del modo solo se agrega si hay texto. Recargar
            // igual era un round-trip completo desperdiciado en el caso más común (el usuario
            // cambia el combo ANTES de escribir), justo el escenario que se sentía lento.
            bool teniaTermino = !string.IsNullOrWhiteSpace(_txtBuscar.Text);
            _txtBuscar.Clear();
            _txtBuscar.Focus();
            if (teniaTermino) FiltrarGrid();
        };
        pillRow.Children.Add(_cboModo);

        // separador vertical
        pillRow.Children.Add(new Border
        {
            Width = 1, Background = B(CB),
            Margin = new Thickness(0, 6, 0, 6),
            VerticalAlignment = VerticalAlignment.Stretch,
        });

        // icono lupa
        pillRow.Children.Add(new TextBlock
        {
            Text = "⌕", FontSize = 15, Foreground = B(CS),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 6, 0),
        });

        // input de texto
        _txtBuscar = new TextBox
        {
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize        = _modoSimple ? 12 : 13,
            Foreground      = B(CT),
            VerticalAlignment = VerticalAlignment.Center,
            Padding         = new Thickness(0, 8, 14, 8),
            MinWidth        = _modoSimple ? 110 : 180,
            FocusVisualStyle = null,
        };
        _txtBuscar.TextChanged += (_, __) => FiltrarGrid();
        pillRow.Children.Add(_txtBuscar);

        pill.Child = pillRow;

        // combo compacto reutilizable para reemplazar los chips y ahorrar espacio vertical
        ComboBox MkFiltroCombo(int width, params (string label, string val)[] items)
        {
            var cbo = new ComboBox
            {
                BorderBrush     = B(CB), BorderThickness = new Thickness(1.5),
                Background      = Brushes.White,
                Foreground      = B(CS),
                FontSize        = _modoSimple ? 10 : 11, FontWeight = FontWeights.SemiBold,
                Padding         = new Thickness(_modoSimple ? 6 : 10, 4, 6, 4),
                Width           = width,
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
            };
            foreach (var (label, val) in items)
                cbo.Items.Add(new ComboBoxItem { Content = label, Tag = val });
            return cbo;
        }

        _cboFiltroStock = MkFiltroCombo(_modoSimple ? 92 : 120,
            ("Todos", "todos"), ("Con stock", "con"), ("Sin stock", "sin"));
        _cboFiltroStock.SelectedIndex = _filtroStock switch { "todos" => 0, "con" => 1, "sin" => 2, _ => 1 };
        _cboFiltroStock.SelectionChanged += (_, __) =>
        {
            _filtroStock = (string)((ComboBoxItem)_cboFiltroStock.SelectedItem).Tag;
            FiltrarGrid();
        };

        var stockPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_modoSimple ? 8 : 12, 0, 0, 0),
        };
        stockPanel.Children.Add(new TextBlock
        {
            Text = "Stock:",
            FontSize = _modoSimple ? 10 : 11, Foreground = B(CS),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });
        stockPanel.Children.Add(_cboFiltroStock);

        // grupo tipo venta: Todos | Contado | Crédito — el valor inicial depende de
        // _filtroTipo (en modo selector ya viene en "credito", ver constructor).
        _cboFiltroTipo = MkFiltroCombo(_modoSimple ? 108 : 140,
            ("Todos", "todos"), ("Solo contado", "contado"), ("Admite crédito", "credito"));
        _cboFiltroTipo.SelectedIndex = _filtroTipo switch { "todos" => 0, "contado" => 1, "credito" => 2, _ => 0 };
        _cboFiltroTipo.SelectionChanged += (_, __) =>
        {
            _filtroTipo = (string)((ComboBoxItem)_cboFiltroTipo.SelectedItem).Tag;
            FiltrarGrid();
        };

        var tipoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_modoSimple ? 6 : 10, 0, 0, 0),
        };
        tipoPanel.Children.Add(new TextBlock
        {
            Text = _modoSimple ? "Venta:" : "Venta permitida:",
            FontSize = _modoSimple ? 10 : 11, Foreground = B(CS),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });
        tipoPanel.Children.Add(_cboFiltroTipo);

        // fila principal: pill + combos. En modo simple se fuerza una sola línea (Orientation
        // Horizontal en vez de WrapPanel) — la ventana ya es más angosta a propósito y el
        // usuario pidió que los filtros no bajen a una segunda fila; los combos y textos se
        // achican arriba (_modoSimple) para que entren cómodos en el ancho disponible.
        Panel mainRow = _modoSimple
            ? new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }
            : new WrapPanel  { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        mainRow.Children.Add(pill);
        mainRow.Children.Add(stockPanel);
        mainRow.Children.Add(tipoPanel);

        bar.Child = mainRow;
        return bar;
    }

    // ── BODY ────────────────────────────────────────────────────────────────
    private UIElement Body()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // columna 0: barra de filtros + grilla, apiladas en su propio Grid — así el panel de
        // la columna 1 no hereda esa fila y puede arrancar desde arriba del todo.
        var colGrid = new Grid();
        colGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        colGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var bar = SearchBar(); Grid.SetRow(bar, 0); colGrid.Children.Add(bar);
        var gridWrap = BuildGrid(); Grid.SetRow(gridWrap, 1); colGrid.Children.Add(gridWrap);
        Grid.SetColumn(colGrid, 0); g.Children.Add(colGrid);

        // Modo simple (selector desde Compras/Precios): ni Stock por Local ni Plan de
        // Pagos/Simulador tienen sentido acá (no se está vendiendo a crédito, solo eligiendo
        // un artículo) — la grilla ocupa el ancho completo en vez de repartirlo con columnas
        // que ni se van a construir.
        if (_modoSimple) return g;

        // Pedido explícito: se quitó la columna aparte "ARTÍCULO SELECCIONADO" — el panel del
        // simulador ahora incluye ese bloque arriba (código/desc/precios) y ocupa el ancho que
        // antes se repartía entre las dos columnas.
        // Pedido explícito: comprimir el ancho del simulador para darle más espacio a la
        // grilla de artículos (que ahora tiene columnas extra de stock por local).
        // Pedido explícito: panel FIJO de Stock por Local, mismo alto que el simulador, como
        // columna intermedia — va ANTES que el simulador (grilla | stock por local | simulador).
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.22, GridUnitType.Star), MinWidth = 190 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.28, GridUnitType.Star), MinWidth = 230 });

        var panelStock = BuildStockPorLocalPanel();
        Grid.SetColumn(panelStock, 1); g.Children.Add(panelStock);

        var panelSim = BuildSimuladorPanel();
        Grid.SetColumn(panelSim, 2); g.Children.Add(panelSim);
        return g;
    }

    // ── GRILLA PRINCIPAL ────────────────────────────────────────────────────
    private UIElement BuildGrid()
    {
        _grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = B(CB),
            RowBackground = Brushes.White,
            AlternatingRowBackground = B(C4),
            Background = Brushes.White,
            Foreground = B(CT),
            BorderThickness = new Thickness(0),
            // Pedido explícito: mismo tamaño de fuente compacto que el sistema legacy —
            // ahí entraban muchas más columnas (stock por local L1..L6) en el mismo ancho
            // porque el texto y las filas eran notablemente más chicos que acá.
            FontSize = 10.5,
            // Alto de header subido de 28 a 36 — las columnas de stock por local usan un
            // header con wrap a 2 líneas (nombre completo del local, sin truncar).
            ColumnHeaderHeight = 36, RowHeight = 32,
            FocusVisualStyle = null,
            EnableRowVirtualization    = true,
            EnableColumnVirtualization = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        // Sin esto, un FontSize no entero (10.5) puede caer en un tamaño de píxel fraccional
        // según el DPI de la pantalla y WPF lo interpola en vez de alinearlo al pixel grid —
        // se ve borroso/gris en vez de nítido. Display + ClearType fuerza snap a pixel real.
        TextOptions.SetTextFormattingMode(_grid, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(_grid, TextRenderingMode.ClearType);
        // El resaltado de "solo contado" en modo selector se resuelve en OnLoadingRow (evento
        // LoadingRow) — ese evento setea Background directamente sobre cada fila y pisaría
        // cualquier RowStyle/DataTrigger definido acá a nivel de Style.
        // Virtualización por ítem (no pixel) + scroll diferido para 12k filas
        VirtualizingPanel.SetScrollUnit(_grid,   ScrollUnit.Item);
        VirtualizingPanel.SetVirtualizationMode(_grid, VirtualizationMode.Recycling);
        VirtualizingPanel.SetIsVirtualizing(_grid, true);
        // selección azul
        _grid.Resources.Add(SystemColors.HighlightBrushKey,                      B(C2));
        _grid.Resources.Add(SystemColors.HighlightTextBrushKey,                  Brushes.White);
        _grid.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey,     B(C3));
        _grid.Resources.Add(SystemColors.InactiveSelectionHighlightTextBrushKey, B(CT));

        // Style implícito de DataGridCell (aplica a TODAS las columnas que no traen su propio
        // CellStyle, ej. Código/Descripción/Marca) — el DataGridCell del tema por defecto pinta
        // su propio fondo blanco encima del Background de la fila, tapándolo salvo que la
        // CELDA (no la fila) esté marcada IsSelected. Sin este estilo, esas columnas quedaban
        // en blanco liso al seleccionar la fila en vez de heredar el azul (bug real reportado:
        // "algo falta está blanco en vez de azul" tras cambiar OnLoadingRow a usar Row.Style).
        var cellDefaultSt = new Style(typeof(DataGridCell));
        cellDefaultSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellDefaultSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        var cellDefaultSel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellDefaultSel.Setters.Add(new Setter(DataGridCell.BackgroundProperty, B(C2)));
        cellDefaultSel.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
        cellDefaultSt.Triggers.Add(cellDefaultSel);
        _grid.Resources.Add(typeof(DataGridCell), cellDefaultSt);

        _grid.LoadingRow       += OnLoadingRow;
        _grid.SelectionChanged += OnSelectionChanged;

        _grid.Columns.Add(Col("Código",      "Codigo",   68));
        // Ancho FIJO (no star) — con columnas star mezcladas, WPF a veces prioriza llenar
        // el viewport comprimiendo esta columna en vez de activar el scroll horizontal, aun
        // con las 15+ columnas de stock por local desbordando. Un ancho fijo hace que el
        // ancho total de la grilla sea determinista y el scroll se dispare de forma confiable.
        _grid.Columns.Add(Col("Descripción", "Desc",     320));
        _grid.Columns.Add(Col("Marca",       "Marca",    72));
        _grid.Columns.Add(ColNum("Contado",  "Contado",  68));
        _grid.Columns.Add(ColNum("P.Venta",  "PVenta",   68));
        _grid.Columns.Add(ColNum("Max.C",    "MaxCuota",  48));
        _grid.Columns.Add(ColSoloContado());
        // Columna Estado (Activo/Inactivo) — solo en el selector simple de Artículos > Precios.
        // En el resto de los usos de esta ventana (Ver Artículos normal, selector de Venta a
        // Crédito) la consulta sigue trayendo solo activos (a.ES = 1), así que la columna
        // siempre diría "Activo" y no aportaría nada — se omite ahí a propósito.
        if (_modoSimple) _grid.Columns.Add(ColEstado());
        // Pedido explícito: "Stock Total" al final de las columnas fijas — las columnas de
        // stock por local (agregadas después, en AgregarColumnasStockPorLocal una vez que
        // _locales esté cargada) van todavía más a la derecha, con scroll horizontal.
        _grid.Columns.Add(ColStock());

        return _grid;
    }

    private DataGridTextColumn Col(string h, string p, double w, bool star = false, double minW = 0)
    {
        var c = new DataGridTextColumn
        {
            Header  = h, Binding = new Binding(p),
            Width   = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w),
            MinWidth = minW,
        };
        c.HeaderStyle  = GridHeaderStyle();
        c.ElementStyle = new Style(typeof(TextBlock))
        {
            Setters = { new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 3, 0)) }
        };
        return c;
    }

    private DataGridTextColumn ColNum(string h, string p, double w)
    {
        var c = new DataGridTextColumn
        {
            Header  = h,
            Binding = new Binding(p) { StringFormat = "N0", ConverterCulture = _cult },
            Width   = new DataGridLength(w),
        };
        c.HeaderStyle  = GridHeaderStyle();
        c.ElementStyle = new Style(typeof(TextBlock))
        {
            Setters = {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0)),
                new Setter(TextBlock.FontWeightProperty,    FontWeights.SemiBold),
            }
        };
        return c;
    }

    private DataGridTemplateColumn ColStock()
    {
        var col = new DataGridTemplateColumn { Header = "Stock Total", Width = 70 };
        col.HeaderStyle = GridHeaderStyle();

        // Pedido explícito: sin badge/cápsula — el color de semáforo (azul=con stock,
        // rojo=negativo, gris=cero) va en toda la celda, igual que las columnas de local.
        var cellSt = new Style(typeof(DataGridCell));
        cellSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new Binding("TotalStock") { Converter = new StockBgConv() }));
        cellSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellSt.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        // Mismo ajuste que ColStockLocal: un solo color de selección fijo (sin distinguir
        // activo/inactivo, ver comentario largo en ColStockLocal) tapando el color de
        // semáforo, y el texto pasa a blanco.
        var selTrig = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selTrig.Setters.Add(new Setter(DataGridCell.BackgroundProperty, B(C2)));
        selTrig.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellSt.Triggers.Add(selTrig);
        col.CellStyle = cellSt;

        var dt = new DataTemplate();
        var t  = new FrameworkElementFactory(typeof(TextBlock));
        t.SetBinding(TextBlock.TextProperty,       new Binding("TotalStock") { StringFormat = "N0", ConverterCulture = _cult });
        t.SetBinding(TextBlock.ForegroundProperty, new Binding("TotalStock") { Converter = new StockFgConv() });
        t.SetValue(TextBlock.FontWeightProperty,   FontWeights.Bold);
        t.SetValue(TextBlock.FontSizeProperty,     10.0);
        t.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        t.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        t.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
        // El Foreground de este TextBlock viene de un Binding (StockFgConv), no de un valor
        // fijo — TextElement.Foreground heredable (usado en ColStockLocal) no sirve acá porque
        // un Binding local siempre gana sobre la herencia. Se necesita un Trigger real que
        // sobrescriba el binding cuando la celda está seleccionada; el RelativeSource.
        // TemplatedParent (la celda que hostea este DataTemplate) es la forma confiable de
        // referenciar "la propia celda" desde dentro de su CellTemplate — FindAncestor desde
        // acá no hacía match de forma consistente (el texto quedaba en su color normal, oscuro
        // e ilegible sobre fondo azul).
        var textSelTrig = new DataTrigger
        {
            Binding = new Binding("IsSelected") { RelativeSource = RelativeSource.TemplatedParent },
            Value = true,
        };
        textSelTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
        dt.Triggers.Add(textSelTrig);
        dt.VisualTree    = t;
        col.CellTemplate = dt;
        return col;
    }

    // Agrega una columna por cada local activo, mostrando el stock puntual de ESE local —
    // pedido explícito replicando la vista del sistema legacy (colores tipo semáforo, scroll
    // horizontal). Se llama una sola vez desde CargarAsync(), recién cuando _locales ya está
    // poblada (BuildGrid() se ejecuta antes, en el constructor, sin ese dato disponible).
    // Paleta pastel fija por columna (no por valor de stock) — pedido explícito: un color
    // distinto para cada local, solo para diferenciarlos a simple vista, igual que el
    // sistema legacy. Rota si hay más locales que colores.
    private static readonly Color[] ColoresLocal =
    {
        Color.FromRgb(0xAE, 0xE0, 0xF2), // celeste
        Color.FromRgb(0xF7, 0xB2, 0x8C), // naranja pastel
        Color.FromRgb(0x9F, 0xD9, 0x9B), // verde pastel
        Color.FromRgb(0xF0, 0xE0, 0x8C), // amarillo pastel
        Color.FromRgb(0xD8, 0xC7, 0xE8), // lila pastel
    };

    private void AgregarColumnasStockPorLocal()
    {
        // Mismo criterio que en CargarAsync: en modo selector, el local destacado es el de
        // la solicitud (_idLocalFiltro); si no, el de la sesión del usuario logueado.
        int? idLocalDestacado = _modoSelector && _idLocalFiltro > 0
            ? _idLocalFiltro
            : SessionService.Instance.LocalActual?.IdLocal;
        for (int i = 0; i < _locales.Count; i++)
        {
            // La columna destacada (siempre la primera, ver CargarAsync) lleva una estrella
            // en el header — refuerzo visual además del orden, para que salte a la vista de
            // inmediato sin tener que leer el nombre completo.
            var esDestacado = _locales[i].IdLocal == idLocalDestacado;
            var header = esDestacado
                ? $"★ L{_locales[i].Codigo} {_locales[i].NombreLocal}"
                : $"L{_locales[i].Codigo} {_locales[i].NombreLocal}";
            _grid.Columns.Add(ColStockLocal(i, header, ColoresLocal[i % ColoresLocal.Length]));
        }
    }

    // ColoresLocal tiene solo 5 colores que rotan (i % Length) — antes ColStockLocal armaba un
    // Style+Trigger de DataGridCell NUEVO por cada una de las hasta 15 columnas de stock por
    // local, pese a que como mucho hay 5 combinaciones de color realmente distintas. Con 15
    // locales eso eran 15 Style/Trigger separados que WPF tenía que parsear y volver a evaluar
    // en cada celda — carga de UI real, no cosmética (no hay sombras/gradientes en este grid),
    // que se notaba en las PCs de sucursal más flojas. Cacheado por color: como mucho 5
    // instancias de Style, reutilizadas entre columnas que comparten color — mismo resultado
    // visual, menos objetos que WPF tiene que mantener y matchear por fila.
    private readonly Dictionary<Color, Style> _cellStyleStockPorColor = new();

    private Style ObtenerCellStyleStock(Color colorLocal)
    {
        if (_cellStyleStockPorColor.TryGetValue(colorLocal, out var cached)) return cached;

        // Pedido explícito: el fondo va en toda la CELDA (como el sistema legacy), fijo por
        // columna/local — no cambia según el valor de stock, solo sirve para distinguir un
        // local de otro a simple vista.
        var cellSt = new Style(typeof(DataGridCell));
        cellSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(colorLocal)));
        cellSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellSt.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellSt.Setters.Add(new Setter(TextElement.ForegroundProperty, B(CT)));
        // Pedido explícito: que toda la fila se vea resaltada en azul de forma UNIFORME al
        // seleccionarla (como el sistema legacy), sin importar si la grilla tiene el foco de
        // teclado — un solo color fijo, sin distinguir activo/inactivo (el intento anterior de
        // replicar ese matiz con Selector.IsSelectionActiveProperty no aplicaba porque esa
        // propiedad adjunta vive en el contenedor de fila, no en la celda individual, y el
        // trigger nunca hacía match). Además el texto pasa a blanco (antes quedaba en su color
        // normal, ilegible sobre el fondo pastel).
        var selTrig = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selTrig.Setters.Add(new Setter(DataGridCell.BackgroundProperty, B(C2)));
        selTrig.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        // TextElement.Foreground (heredable) en vez de TextBlock.Foreground directo — el
        // binding indirecto vía RelativeSource+DataTrigger dentro del propio CellTemplate no
        // hacía match de forma confiable (el texto se quedaba en su color oscuro normal sobre
        // fondo azul, ilegible). Seteando la propiedad heredable en la CELDA misma, el
        // TextBlock hijo (que no fija su propio Foreground de forma local) la hereda sin
        // necesitar un trigger separado en el DataTemplate.
        selTrig.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.White));
        cellSt.Triggers.Add(selTrig);

        _cellStyleStockPorColor[colorLocal] = cellSt;
        return cellSt;
    }

    private DataGridTemplateColumn ColStockLocal(int index, string header, Color colorLocal)
    {
        // Ancho mayor que el resto de columnas fijas y header con wrap propio (2 líneas) —
        // pedido explícito: que se lea el nombre completo del local, no truncado, aceptando
        // que el conjunto de columnas fuerce el scroll horizontal (ya habilitado en _grid).
        var col = new DataGridTemplateColumn { Header = header, Width = 76 };
        col.HeaderStyle = GridHeaderStyleWrap();
        col.CellStyle   = ObtenerCellStyleStock(colorLocal);

        var path = $"Stocks[{index}]";
        var dt = new DataTemplate();
        var t  = new FrameworkElementFactory(typeof(TextBlock));
        t.SetBinding(TextBlock.TextProperty, new Binding(path) { StringFormat = "N0", ConverterCulture = _cult });
        t.SetValue(TextBlock.FontWeightProperty,    FontWeights.SemiBold);
        t.SetValue(TextBlock.FontSizeProperty,      9.0);
        t.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        t.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        t.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);
        dt.VisualTree    = t;
        col.CellTemplate = dt;
        return col;
    }

    private DataGridTemplateColumn ColSoloContado()
    {
        var col = new DataGridTemplateColumn { Header = "Sólo cont.", Width = 60 };
        col.HeaderStyle = GridHeaderStyle();

        var cellSt = new Style(typeof(DataGridCell));
        cellSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellSt.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        var cellSelTrig = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSelTrig.Setters.Add(new Setter(DataGridCell.BackgroundProperty, B(C2)));
        cellSelTrig.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellSt.Triggers.Add(cellSelTrig);
        col.CellStyle = cellSt;

        var dt = new DataTemplate();
        var f  = new FrameworkElementFactory(typeof(TextBlock));
        f.SetBinding(TextBlock.TextProperty, new Binding("SoloContado")
        {
            Converter = new SoloContadoTextConv()
        });
        // El Foreground combina el valor de negocio (SoloContado) con el estado de selección
        // de la propia celda: un Trigger de DataTemplate no sirve porque TemplatedParent, para
        // una DataGridTemplateColumn, no resuelve a DataGridCell (no expone IsSelected), y por
        // eso el trigger nunca se disparaba. Se usa un MultiBinding con FindAncestor real hacia
        // DataGridCell en vez de depender de un Trigger.
        var mb = new MultiBinding { Converter = new SoloContadoFgConv() };
        mb.Bindings.Add(new Binding("SoloContado"));
        mb.Bindings.Add(new Binding("IsSelected")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        f.SetBinding(TextBlock.ForegroundProperty, mb);
        f.SetValue(TextBlock.FontWeightProperty,          FontWeights.SemiBold);
        f.SetValue(TextBlock.FontSizeProperty,            10.0);
        f.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        f.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);

        dt.VisualTree    = f;
        col.CellTemplate = dt;
        return col;
    }

    private DataGridTemplateColumn ColEstado()
    {
        var col = new DataGridTemplateColumn { Header = "Estado", Width = 62 };
        col.HeaderStyle = GridHeaderStyle();

        var cellSt = new Style(typeof(DataGridCell));
        cellSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellSt.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        var cellSelTrig = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSelTrig.Setters.Add(new Setter(DataGridCell.BackgroundProperty, B(C2)));
        cellSelTrig.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellSt.Triggers.Add(cellSelTrig);
        col.CellStyle = cellSt;

        var dt = new DataTemplate();
        var f  = new FrameworkElementFactory(typeof(TextBlock));
        f.SetBinding(TextBlock.TextProperty, new Binding("Activo") { Converter = new ActivoTextConv() });
        var mb = new MultiBinding { Converter = new ActivoFgConv() };
        mb.Bindings.Add(new Binding("Activo"));
        mb.Bindings.Add(new Binding("IsSelected")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        f.SetBinding(TextBlock.ForegroundProperty, mb);
        f.SetValue(TextBlock.FontWeightProperty,          FontWeights.SemiBold);
        f.SetValue(TextBlock.FontSizeProperty,            10.0);
        f.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        f.SetValue(TextBlock.VerticalAlignmentProperty,   VerticalAlignment.Center);

        dt.VisualTree    = f;
        col.CellTemplate = dt;
        return col;
    }

    private static Style GridHeaderStyle()
    {
        var s = new Style(typeof(DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,      new SolidColorBrush(Color.FromRgb(18, 78, 120))));
        s.Setters.Add(new Setter(Control.ForegroundProperty,      Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty,      FontWeights.SemiBold));
        s.Setters.Add(new Setter(Control.FontSizeProperty,        10.5));
        s.Setters.Add(new Setter(Control.PaddingProperty,         new Thickness(4, 0, 4, 0)));
        s.Setters.Add(new Setter(Control.BorderBrushProperty,     new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        return s;
    }

    // Header con wrap de texto (2 líneas) — usado por las columnas de stock por local, donde
    // el nombre del local no debe truncarse (pedido explícito). Un DataGridColumnHeader normal
    // no envuelve su contenido por defecto, así que se le da un ContentTemplate propio.
    private static Style GridHeaderStyleWrap()
    {
        var s = GridHeaderStyle();
        var template = new ControlTemplate(typeof(DataGridColumnHeader));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 0));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
        text.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.FontSizeProperty, 8.5);
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        text.SetValue(TextBlock.PaddingProperty, new Thickness(2, 2, 2, 2));
        border.AppendChild(text);
        template.VisualTree = border;
        s.Setters.Add(new Setter(Control.TemplateProperty, template));
        return s;
    }

    // ── BLOQUE INFO ARTÍCULO (código/desc/precios/cuotas) ────────────────────
    // Este bloque compacto se inserta arriba del todo dentro del panel del simulador,
    // mostrando Precio Contado y Precio Crédito directo al lado del plan de pago.
    // El detalle de stock por local vive aparte, en el panel fijo de la 3ra columna
    // (BuildStockPorLocalPanel), no en este bloque.
    private UIElement BuildInfoArticuloBlock()
    {
        var infoBody = new Border { Padding = new Thickness(12, 4, 12, 2), Background = Brushes.White };
        var infoSp   = new StackPanel();

        // código + descripción en una sola línea horizontal (pedido explícito) — Grid (no
        // StackPanel) para que la descripción tenga un ancho real acotado y pueda truncar
        // con "…" en vez de desbordar o forzar el ancho del panel.
        var codDescRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        codDescRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        codDescRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _panelCodigo = new TextBlock
        {
            FontSize = 16, FontWeight = FontWeights.Bold, Foreground = B(C1), Text = "—",
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_panelCodigo, 0);
        codDescRow.Children.Add(_panelCodigo);

        _panelDesc = new TextBlock
        {
            FontSize = 10.5, Foreground = B(CS),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 2, 0, 0), Text = "Seleccione un artículo",
        };
        Grid.SetColumn(_panelDesc, 1);
        codDescRow.Children.Add(_panelDesc);
        infoSp.Children.Add(codDescRow);

        // Stock total + precio contado + precio crédito + máximo de cuotas, todo en una sola
        // fila de tarjetas compactas (pedido explícito: reducir texto lo que haga falta para
        // que entre todo junto).
        var preciosRow = new Grid();
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        preciosRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });

        // badge stock total — mismo formato compacto que las tarjetas de precio. El detalle
        // por local ahora vive siempre visible en el panel fijo de la 3ra columna (pedido
        // explícito), esta tarjeta ya no necesita ser clickeable.
        var stockCard = new Border
        {
            Background = B(C4), BorderBrush = B(C3), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3, 6, 3),
        };
        var stockSp = new StackPanel();
        stockSp.Children.Add(new TextBlock {
            Text = "STOCK TOTAL", FontSize = 7.5, FontWeight = FontWeights.Bold,
            Foreground = B(CS), Margin = new Thickness(0, 0, 0, 1),
        });
        _stockBadgeTxt = new TextBlock { Text = "—", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = B(CT) };
        stockSp.Children.Add(_stockBadgeTxt);
        stockCard.Child = stockSp;
        _stockBadge = stockCard;

        Border PCard(string lbl, out TextBlock field, Color bg, Color fg)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 3, 6, 3),
            };
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock
            {
                Text = lbl, FontSize = 7.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(160, fg.R, fg.G, fg.B)),
                Margin = new Thickness(0, 0, 0, 1),
            });
            field = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(fg), Text = "—" };
            inner.Children.Add(field);
            b.Child = inner;
            return b;
        }
        var cardContado = PCard("CONTADO", out _panelContado, C3, C1);
        var cardCredito = PCard("CRÉDITO", out _panelPventa,  C6, C5);

        var cuotaB = new Border
        {
            Background = B(C4), BorderBrush = B(C3), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3, 6, 3),
        };
        var cuotaSp = new StackPanel();
        cuotaSp.Children.Add(new TextBlock
        {
            Text = "MÁX. CUOTAS", FontSize = 7.5, FontWeight = FontWeights.Bold,
            Foreground = B(C2), Margin = new Thickness(0, 0, 0, 1),
        });
        _panelMaxC = new TextBlock { Text = "0", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = B(C1) };
        cuotaSp.Children.Add(_panelMaxC);
        cuotaB.Child = cuotaSp;

        Grid.SetColumn(stockCard,   0);
        Grid.SetColumn(cardContado, 2);
        Grid.SetColumn(cardCredito, 4);
        Grid.SetColumn(cuotaB,      6);
        preciosRow.Children.Add(stockCard);
        preciosRow.Children.Add(cardContado);
        preciosRow.Children.Add(cardCredito);
        preciosRow.Children.Add(cuotaB);
        infoSp.Children.Add(preciosRow);

        infoBody.Child = infoSp;

        return infoBody;
    }

    // ── PANEL STOCK POR LOCAL (col 3, fijo, mismo alto que el simulador) ────
    // Pedido explícito (retomado): tabla real ("Locales" | "Stock") siempre visible al lado
    // del simulador, no un popup — se actualiza en cada OnSelectionChanged reutilizando el
    // mismo repositorio que ya se consultaba antes (ObtenerStockTodosLocalesAsync), sin
    // agregar ninguna query nueva al proyecto.
    private UIElement BuildStockPorLocalPanel()
    {
        var outer = new Border {
            Background = B(CF), BorderBrush = B(CB), BorderThickness = new Thickness(1, 0, 0, 0),
        };
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var hdrStock = SectionHeader("STOCK POR LOCAL", C1);
        Grid.SetRow(hdrStock, 0);
        rootGrid.Children.Add(hdrStock);

        // Badges de color para el stock (verde/rojo) sobre fondo blanco, igual estilo que el
        // resto de la ventana — el fondo oscuro se probó antes pero no gustó.
        var colorPos = Color.FromRgb(34, 197, 94);
        var colorNeg = Color.FromRgb(239, 68, 68);

        var hdrColStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrColStyle.Setters.Add(new Setter(Control.BackgroundProperty, B(C4)));
        hdrColStyle.Setters.Add(new Setter(Control.ForegroundProperty, B(CT)));
        hdrColStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrColStyle.Setters.Add(new Setter(Control.FontSizeProperty, 9.0));
        hdrColStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));

        byte? idLocalDestacado = LocalDestacadoParaStock();

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        rowStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 34.0));
        var altTrigger = new Trigger { Property = ItemsControl.AlternationIndexProperty, Value = 1 };
        altTrigger.Setters.Add(new Setter(Control.BackgroundProperty, B(C4)));
        rowStyle.Triggers.Add(altTrigger);
        if (idLocalDestacado.HasValue)
        {
            var destacadoTrigger = new DataTrigger {
                Binding = new System.Windows.Data.Binding("IdLocal"), Value = idLocalDestacado.Value,
            };
            destacadoTrigger.Setters.Add(new Setter(Control.BackgroundProperty, B(C3)));
            destacadoTrigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            rowStyle.Triggers.Add(destacadoTrigger);
        }

        // Pedido explícito: al seleccionar una fila, la celda "Locales" debe pintarse azul
        // (mismo Highlight que ya se veía en la celda "Stock") con texto blanco — antes solo
        // la celda de Stock reaccionaba porque la de Locales usa un DataGridTemplateColumn
        // con el TextBlock en color fijo (CT), que tapaba el resaltado. El Trigger de abajo
        // (en el propio TextBlock del template) lo hace reaccionar igual que la de Stock.
        var cellStyleConSeleccion = new Style(typeof(DataGridCell));
        cellStyleConSeleccion.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        var cellSelTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSelTrigger.Setters.Add(new Setter(Control.BackgroundProperty, SystemColors.HighlightBrush));
        cellStyleConSeleccion.Triggers.Add(cellSelTrigger);
        _dgStockLocal = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = hdrColStyle, FontSize = 9.5,
            AlternationCount = 2, RowStyle = rowStyle,
            CellStyle = cellStyleConSeleccion,
            RowHeaderWidth = 0,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var elementStyleLocales = new Style(typeof(TextBlock));
        elementStyleLocales.Setters.Add(new Setter(TextBlock.ForegroundProperty, B(CT)));
        elementStyleLocales.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        elementStyleLocales.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(8, 0, 4, 0)));
        // Texto blanco cuando la celda contenedora está seleccionada (fondo azul de Highlight,
        // aplicado en cellStyleConSeleccion) — RelativeSource FindAncestor sí funciona de forma
        // confiable acá, a diferencia de dentro de un DataTemplate.Triggers (probado, no
        // reaccionaba ahí).
        var textoBlancoTrigger = new DataTrigger {
            Binding = new System.Windows.Data.Binding {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1),
                Path = new PropertyPath(DataGridCell.IsSelectedProperty),
            },
            Value = true,
        };
        textoBlancoTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
        elementStyleLocales.Triggers.Add(textoBlancoTrigger);

        var colLocales = new DataGridTextColumn {
            Header = "Locales", Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            Binding = new System.Windows.Data.MultiBinding {
                Converter = new LocalNombrePrefijoConverter(idLocalDestacado),
                Bindings = { new System.Windows.Data.Binding("LocalNombre"), new System.Windows.Data.Binding("IdLocal") },
            },
            ElementStyle = elementStyleLocales,
        };
        _dgStockLocal.Columns.Add(colLocales);
        var cellStyleStock = new Style(typeof(DataGridCell));
        cellStyleStock.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        cellStyleStock.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyleStock.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        var stockPosTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("S") { Converter = new StockPositivoConverter() }, Value = true };
        stockPosTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(colorPos)));
        var stockNegTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("S") { Converter = new StockPositivoConverter() }, Value = false };
        stockNegTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(colorNeg)));
        cellStyleStock.Triggers.Add(stockPosTrigger);
        cellStyleStock.Triggers.Add(stockNegTrigger);
        var stockCellSelTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        stockCellSelTrigger.Setters.Add(new Setter(Control.BackgroundProperty, SystemColors.HighlightBrush));
        stockCellSelTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        cellStyleStock.Triggers.Add(stockCellSelTrigger);

        _dgStockLocal.Columns.Add(new DataGridTextColumn {
            Header = "Stock", Binding = new System.Windows.Data.Binding("S") { StringFormat = "N0" },
            Width = new DataGridLength(0.5, DataGridLengthUnitType.Star),
            CellStyle = cellStyleStock,
            ElementStyle = new Style(typeof(TextBlock)) {
                Setters = { new Setter(TextBlock.FontWeightProperty, FontWeights.Bold),
                            new Setter(TextBlock.FontSizeProperty, 9.5),
                            new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center) } },
        });
        Grid.SetRow(_dgStockLocal, 1);
        rootGrid.Children.Add(_dgStockLocal);

        outer.Child = rootGrid;
        return outer;
    }

    private sealed class StockPositivoConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is decimal d && d > 0;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Local propio del usuario logueado — o, en modo selector (Ventas a Crédito), el local
    // elegido para esa venta — se resalta (fila destacada + ⭐) en el panel de Stock por Local.
    // Price.IdLocal es byte, así que se normaliza el tipo acá para comparar bien.
    private byte? LocalDestacadoParaStock()
        => _modoSelector && _idLocalFiltro > 0
            ? _idLocalFiltro
            : (byte?)SessionService.Instance.LocalActual?.IdLocal;

    private sealed class LocalNombrePrefijoConverter : System.Windows.Data.IMultiValueConverter
    {
        private readonly byte? _idLocalDestacado;
        public LocalNombrePrefijoConverter(byte? idLocalDestacado) => _idLocalDestacado = idLocalDestacado;
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var nombre = values.Length > 0 ? values[0]?.ToString() ?? "" : "";
            var idLocal = values.Length > 1 && values[1] is byte b ? b : (byte?)null;
            return (_idLocalDestacado.HasValue && idLocal == _idLocalDestacado) ? "⭐ " + nombre : nombre;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── PANEL SIMULADOR (col 2): Plan de pago + Valor final ─────────────────
    private UIElement BuildSimuladorPanel()
    {
        var outer = new Border
        {
            Background      = B(CF),
            BorderBrush     = B(CB),
            BorderThickness = new Thickness(1, 0, 0, 0),
        };

        // Grid (no StackPanel) para que la fila del "dock" (scroll + valor final) reciba una
        // altura FINITA real heredada de "outer" — un StackPanel mide a sus hijos con altura
        // infinita disponible, así que el ScrollViewer de acá abajo nunca detectaba que el
        // contenido no entraba y jamás mostraba su barra, aunque el card explicativo del
        // recargo quedara cortado fuera de la vista (bug real reportado: agrandar la ventana
        // no alcanzaba, porque el problema no era el tamaño sino que el scroll nunca se activaba).
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Bloque código/descripción/precio contado/precio crédito del artículo seleccionado —
        // antes vivía en su propia columna aparte ("ARTÍCULO SELECCIONADO"), ahora va arriba
        // del simulador (pedido explícito del cliente).
        var infoArticulo = BuildInfoArticuloBlock();
        Grid.SetRow(infoArticulo, 0);
        rootGrid.Children.Add(infoArticulo);

        // header simulador
        var hdrSimulador = SectionHeader(_modoSelector ? "PLAN DE PAGOS" : "SIMULADOR DE PLAN DE PAGO", C5);
        Grid.SetRow(hdrSimulador, 1);
        rootGrid.Children.Add(hdrSimulador);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var planBody = new StackPanel { Background = B(CF) };

        // ·· Grupo 1: Descuento ··
        planBody.Children.Add(PlanGroupLabel("Descuento"));
        var grp1 = new Border
        {
            Margin = new Thickness(10, 0, 10, 4), Background = Brushes.White,
            CornerRadius = new CornerRadius(8), BorderBrush = B(CB), BorderThickness = new Thickness(1),
        };
        var grp1Sp = new StackPanel();
        grp1Sp.Children.Add(PlanInputField("% Descuento", out _txtPctDesc, first: true));
        grp1Sp.Children.Add(PlanInnerDiv());
        grp1Sp.Children.Add(PlanResultField("Descuento",  out _outDescuento,  C5));
        grp1Sp.Children.Add(PlanInnerDiv());
        grp1Sp.Children.Add(PlanResultField("Valor neto", out _outValor,      C1));
        grp1.Child = grp1Sp;
        planBody.Children.Add(grp1);

        // ·· Grupo 2: Entrega ··
        planBody.Children.Add(PlanGroupLabel("Entrega / Saldo"));
        var grp2 = new Border
        {
            Margin = new Thickness(10, 0, 10, 4), Background = Brushes.White,
            CornerRadius = new CornerRadius(8), BorderBrush = B(CB), BorderThickness = new Thickness(1),
        };
        var grp2Sp = new StackPanel();
        grp2Sp.Children.Add(PlanInputField("Entrega (Gs.)", out _txtEntrega, first: true));
        // Link "Usar sugerida" — visible solo cuando el vendedor ya editó Entrega a mano y el
        // valor actual difiere del sugerido (p.ej. la borró para volver a cargarla). Sin esto,
        // no había forma de saber a qué monto volver sin recalcularlo mentalmente (reportado:
        // el vendedor borra la Entrega auto-completada y no sabe qué poner para no perder el
        // "sin recargo" en 1-2 cuotas). Un click restaura el valor Y reactiva el autocompletado.
        _lnkEntregaSugerida = new TextBlock
        {
            FontSize = 10.5, Margin = new Thickness(14, 0, 14, 8),
            Foreground = B(C1), Cursor = Cursors.Hand, TextDecorations = TextDecorations.Underline,
            Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap,
        };
        _lnkEntregaSugerida.MouseLeftButtonUp += (_, __) =>
        {
            _entregaEditadaManualmente = false;
            RecalcPlan();
        };
        grp2Sp.Children.Add(_lnkEntregaSugerida);
        grp2Sp.Children.Add(PlanInnerDiv());
        grp2Sp.Children.Add(PlanResultField("Saldo", out _outValorSaldo, C1));
        grp2.Child = grp2Sp;
        planBody.Children.Add(grp2);

        // ·· Grupo 3: Cuotas ··
        planBody.Children.Add(PlanGroupLabel("Financiación"));
        var grp3 = new Border
        {
            Margin = new Thickness(10, 0, 10, 4), Background = Brushes.White,
            CornerRadius = new CornerRadius(8), BorderBrush = B(CB), BorderThickness = new Thickness(1),
        };
        var grp3Sp = new StackPanel();
        grp3Sp.Children.Add(PlanInputField("% Recargo",    out _txtPctRec,     first: true));
        // % Recargo es editable por el cajero SOLO en 3-10 y 11+ cuotas (pedido explícito: el
        // vendedor puede subir el recargo para ganar más), con piso duro de 4,2% en ambos
        // rangos — nunca se puede bajar de ahí. En 1-2 cuotas sigue sin ser editable (0% fijo,
        // sin cambios). Arranca bloqueado (IsReadOnly) porque todavía no hay cuotas cargadas;
        // ActualizarEdicionRecargo lo habilita/deshabilita según la cantidad real de cuotas.
        _txtPctRec.IsReadOnly = true;
        _txtPctRec.Background = B(C4);
        grp3Sp.Children.Add(PlanInnerDiv());
        grp3Sp.Children.Add(PlanInputFieldCuotas("Cant. Cuotas", out _txtCantCuotas, out _lblCantCuotas));
        grp3Sp.Children.Add(PlanInnerDiv());
        // Cuota SIN % Recargo (solo saldo/cuotas) al lado de la cuota real que se cobra — para
        // que el cliente vea de un vistazo cuánto agrega la financiación por cuota, no solo el
        // total. Mismo término "% Recargo" que el input de arriba (no "interés"), para no usar
        // dos palabras distintas para el mismo concepto en la misma pantalla.
        grp3Sp.Children.Add(PlanResultField("Cuota sin % Recargo", out _outCuotaSinInteres, CS));
        grp3Sp.Children.Add(PlanInnerDiv());
        grp3Sp.Children.Add(PlanResultField("Cuota con % Recargo", out _outValorMensual, C1));
        grp3.Child = grp3Sp;
        planBody.Children.Add(grp3);

        // Card explicativo del recargo — el % Recargo ya no es editable (ver arriba), así que
        // conviene dejar claro por qué varía y cómo se calcula, en vez de que el cajero/cliente
        // solo vea un número fijo sin contexto.
        var infoRecargo = new Border
        {
            Margin = new Thickness(10, 0, 10, 3), Background = B(C4),
            CornerRadius = new CornerRadius(8), BorderBrush = B(C3), BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 3, 7, 3),
        };
        var infoRecargoGrid = new Grid();
        infoRecargoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        infoRecargoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var infoIcon = new TextBlock
        {
            Text = "ⓘ", FontSize = 10, Foreground = B(C2),
            Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Top,
        };
        var infoTexto = new TextBlock
        {
            Text = "1 a 2 cuotas: sin recargo, siempre que la Entrega sea equivalente al " +
                   "monto de una cuota. 3 a 10 cuotas: 4,2% por cuota. " +
                   "11 cuotas o más: 4% por cuota.",
            FontSize = 8, Foreground = B(CS), TextWrapping = TextWrapping.Wrap, LineHeight = 10,
        };
        Grid.SetColumn(infoIcon, 0);
        Grid.SetColumn(infoTexto, 1);
        infoRecargoGrid.Children.Add(infoIcon);
        infoRecargoGrid.Children.Add(infoTexto);
        infoRecargo.Child = infoRecargoGrid;
        planBody.Children.Add(infoRecargo);

        _txtPctDesc.TextChanged    += (_, __) => RecalcPlan();
        // % Recargo marca su propio flag de edición manual — igual patrón que Entrega. Solo
        // cuenta como edición manual si NO viene de RecalcPlan (autocompletado) y el campo está
        // habilitado para edición (3+ cuotas, ver ActualizarEdicionRecargo — se controla con
        // IsReadOnly, no IsEnabled); en 1-2 cuotas el campo está bloqueado (IsReadOnly=true), así
        // que este evento no puede dispararse por tipeo del cajero ahí.
        _txtPctRec.TextChanged     += (_, __) =>
        {
            if (!_calculando && !_txtPctRec.IsReadOnly) _recargoEditadoManualmente = true;
            RecalcPlan();
        };
        // El piso de 4,2% se corrige en el TEXTO del campo recién acá, al perder el foco — NO en
        // cada tecla (eso trababa poder borrar y reescribir un valor mayor, bug real reportado).
        // Mientras se edita, RecalcPlan ya usa el piso para los totales en pantalla sin tocar el
        // texto; esto solo ajusta lo que queda escrito una vez que el cajero termina de tipear.
        _txtPctRec.LostFocus += (_, __) =>
        {
            if (_txtPctRec.IsReadOnly) return;
            if (!TryParsePorcentaje(_txtPctRec.Text, out var pctR) || pctR < 4.2m)
            {
                _txtPctRec.Text = 4.2m.ToString(_cult);
                RecalcPlan();
            }
        };

        // Entrega: separador de miles en tiempo real mientras se escribe
        bool _fmtEntrega = false;
        _txtEntrega.TextChanged += (_, __) =>
        {
            if (_fmtEntrega) return;
            // Si el cambio de texto vino de RecalcPlan (autocompletado), no cuenta como edición
            // manual del cajero — solo lo tipeado directamente en el TextBox marca el flag.
            if (!_calculando) _entregaEditadaManualmente = true;
            _fmtEntrega = true;
            try
            {
                var raw = new string(_txtEntrega.Text.Where(char.IsDigit).ToArray());
                if (long.TryParse(raw, out var num))
                {
                    var formatted = num.ToString("N0", _cult);
                    var caret     = _txtEntrega.CaretIndex;
                    var oldLen    = _txtEntrega.Text.Length;
                    _txtEntrega.Text = formatted;
                    // mantener cursor al final si estaba al final, si no ajustar
                    var delta = formatted.Length - oldLen;
                    _txtEntrega.CaretIndex = Math.Max(0, Math.Min(caret + delta, formatted.Length));
                }
                else
                {
                    _txtEntrega.Text = "0";
                    _txtEntrega.CaretIndex = 1;
                }
            }
            finally
            {
                _fmtEntrega = false;
            }
            RecalcPlan();
        };
        _txtCantCuotas.TextChanged += (_, __) =>
        {
            // Auto-corregir si supera el máximo del artículo seleccionado. MaxCuota == 0
            // significa "sin cuotas configuradas" (tope 1 en la práctica, mismo criterio que
            // cuotasIniciales/Step) — antes el guard solo se activaba con "> 0" y con
            // MaxCuota=0 dejaba tipear cualquier cantidad de cuotas sin corregir.
            var topeCuotas = _maxCuotaSeleccionado > 0 ? _maxCuotaSeleccionado : 1;
            if (int.TryParse(_txtCantCuotas.Text, out var v) && v > topeCuotas)
            {
                _txtCantCuotas.Text = topeCuotas.ToString();
                _txtCantCuotas.CaretIndex = _txtCantCuotas.Text.Length;
                return; // el TextChanged se disparará de nuevo con el valor corregido
            }
            RecalcPlan();
        };

        scroll.Content = planBody;

        // valor final — panel azul destacado, compacto: label al lado del monto (no arriba)
        // para no gastar una fila entera solo en el título (pedido explícito: que todo entre
        // sin scroll).
        var vfOuter = new Border { Background = B(C1), Padding = new Thickness(10, 6, 10, 6) };
        var vfSp    = new StackPanel();

        var vfRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        vfRow.Children.Add(new TextBlock
        {
            Text = "COSTO TOTAL:", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        });
        _outValorFinal = new TextBlock
        {
            Text = "—", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        vfRow.Children.Add(_outValorFinal);
        vfSp.Children.Add(vfRow);

        // Subtítulo + botón de proforma en la misma fila, el botón pegado justo al lado del
        // texto (no al borde derecho) — pedido explícito. StackPanel horizontal en vez de
        // DockPanel: el texto no se estira a ocupar todo el ancho, así el botón queda
        // inmediatamente después de la última palabra.
        var vfFootRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
        vfFootRow.Children.Add(new TextBlock
        {
            Text = "Incluye la entrega — no es solo el saldo a cuotas",
            FontSize = 7.5, Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        // Botón de proforma — desglose cuota por cuota para que el cliente entienda de un
        // vistazo cada monto (fecha estimada, capital sin recargo, recargo, total), en vez de
        // solo los totales agregados que ya muestra este panel.
        var btnProforma = new Button
        {
            Content = "📄 Proforma", Padding = new Thickness(10, 4, 10, 4),
            FontWeight = FontWeights.SemiBold, FontSize = 9,
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            Foreground = Brushes.White, BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        btnProforma.Click += (_, __) => MostrarProforma();
        vfFootRow.Children.Add(btnProforma);
        vfSp.Children.Add(vfFootRow);
        vfOuter.Child = vfSp;

        // ensamblar: header ya agregado, scroll con grupos, valor final abajo
        // usamos DockPanel para que valor final quede siempre visible al fondo
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(vfOuter, Dock.Bottom);
        dock.Children.Add(vfOuter);
        dock.Children.Add(scroll);

        Grid.SetRow(dock, 2);
        rootGrid.Children.Add(dock);
        outer.Child = rootGrid;
        return outer;
    }

    private static Border SectionHeader(string txt, Color bg)
    {
        var b = new Border { Background = new SolidColorBrush(bg), Padding = new Thickness(12, 5, 12, 5) };
        b.Child = new TextBlock
        {
            Text = txt, FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
        };
        return b;
    }

    private static Border HDivider() => new Border
    {
        Height = 1, Background = new SolidColorBrush(Color.FromRgb(218, 225, 234)),
        Margin = new Thickness(0),
    };

    // ── helpers del simulador ────────────────────────────────────────────────
    // Pedido explícito: comprimir mucho más los espacios entre cada input/card del
    // simulador (antes 10-12px de margen vertical entre elementos) para que todo el panel
    // entre sin necesitar tanto scroll — mismo criterio en GroupLabel/InputField/
    // ResultField/grupos de abajo.
    private UIElement PlanGroupLabel(string txt) => new TextBlock
    {
        Text       = txt.ToUpperInvariant(),
        FontSize   = 8.5,
        FontWeight = FontWeights.Bold,
        Foreground = B(CS),
        Margin     = new Thickness(12, 3, 12, 1),
    };

    private UIElement PlanInputField(string label, out TextBox field, bool first)
        => PlanInputField(label, out field, first, out _);

    private UIElement PlanInputField(string label, out TextBox field, bool first, out TextBlock labelRef)
    {
        var g = new Grid { Background = Brushes.White };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        labelRef = new TextBlock
        {
            Text = label, FontSize = 10, Foreground = B(CS),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 2, 6, 2),
            TextWrapping = TextWrapping.Wrap,
        };
        g.Children.Add(labelRef);
        field = new TextBox
        {
            Text = "0", FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = B(CT), Background = B(C4),
            BorderBrush = B(C3), BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2, 6, 2),
            TextAlignment = TextAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 10, 2),
        };
        Grid.SetColumn(field, 1);
        g.Children.Add(field);
        return g;
    }

    // Variante de PlanInputField específica para "Cant. Cuotas", con botones +/- al lado del
    // campo — el vendedor pedía poder subir/bajar la cantidad sin tener que borrar y tipear a
    // mano. El TextChanged ya existente de _txtCantCuotas (guard de máximo + RecalcPlan) sigue
    // disparándose igual, porque los botones solo escriben en field.Text.
    private UIElement PlanInputFieldCuotas(string label, out TextBox field, out TextBlock labelRef)
    {
        var g = new Grid { Background = Brushes.White };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        labelRef = new TextBlock
        {
            Text = label, FontSize = 10, Foreground = B(CS),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 2, 6, 2),
            TextWrapping = TextWrapping.Wrap,
        };
        g.Children.Add(labelRef);

        var fieldLocal = new TextBox
        {
            Text = "0", FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = B(CT), Background = B(C4),
            BorderBrush = B(C3), BorderThickness = new Thickness(1, 1, 0, 1),
            Padding = new Thickness(6, 2, 6, 2),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2),
        };
        field = fieldLocal; // el parámetro 'out' no puede capturarse dentro de Step() más abajo
        Grid.SetColumn(fieldLocal, 2);
        g.Children.Add(fieldLocal);

        Button MkSpin(string glifo, int delta) => new Button
        {
            Content = glifo, FontFamily = new FontFamily("Segoe UI"), FontSize = 11, FontWeight = FontWeights.Bold,
            Width = 22, Background = B(C4), Foreground = B(CT), BorderBrush = B(C3),
            BorderThickness = delta < 0 ? new Thickness(1) : new Thickness(0, 1, 0, 1),
            Cursor = Cursors.Hand, Padding = new Thickness(0),
            Margin = new Thickness(0, 2, delta > 0 ? 10 : 0, 2),
        };
        var btnMenos = MkSpin("-", -1);
        var btnMas   = MkSpin("+", +1);

        void Step(int delta)
        {
            int.TryParse(fieldLocal.Text, out var actual);
            var nuevo = actual + delta;
            if (nuevo < 1) nuevo = 1;
            // MaxCuota == 0 significa "artículo sin cuotas configuradas" (sin tope real, tope 1
            // en la práctica) — mismo criterio que cuotasIniciales al seleccionar el artículo.
            // Antes el guard usaba "> 0" para activarse, y con MaxCuota=0 quedaba desactivado
            // por completo, dejando subir sin límite con el botón "+".
            var tope = _maxCuotaSeleccionado > 0 ? _maxCuotaSeleccionado : 1;
            if (nuevo > tope) nuevo = tope;
            fieldLocal.Text = nuevo.ToString();
            fieldLocal.CaretIndex = fieldLocal.Text.Length;
        }
        btnMenos.Click += (_, __) => Step(-1);
        btnMas.Click   += (_, __) => Step(+1);

        Grid.SetColumn(btnMenos, 1); g.Children.Add(btnMenos);
        Grid.SetColumn(btnMas, 3);   g.Children.Add(btnMas);
        return g;
    }

    private UIElement PlanResultField(string label, out TextBlock field, Color accentColor)
    {
        var g = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(12, accentColor.R, accentColor.G, accentColor.B)),
        };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock
        {
            Text = label, FontSize = 9.5, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(160, accentColor.R, accentColor.G, accentColor.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 2, 6, 2),
            TextWrapping = TextWrapping.Wrap,
        });
        field = new TextBlock
        {
            Text = "—", FontSize = 12.5, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(accentColor),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 2, 10, 2),
        };
        Grid.SetColumn(field, 1);
        g.Children.Add(field);
        return g;
    }

    private static Border PlanInnerDiv() => new Border
    {
        Height = 1,
        Background = new SolidColorBrush(Color.FromRgb(230, 236, 244)),
        Margin = new Thickness(0),
    };

    // ── FOOTER ──────────────────────────────────────────────────────────────
    private UIElement Footer()
    {
        var foot = new Border { Background = B(C1), Padding = new Thickness(12, 7, 12, 7) };
        var dp   = new DockPanel { LastChildFill = true };

        Button Btn(string txt, Color bg, double ml = 0) => new Button
        {
            Content = txt, Padding = new Thickness(14, 5, 14, 5),
            Background = new SolidColorBrush(bg), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            Margin = new Thickness(ml, 0, 0, 0),
        };

        var btns = new StackPanel { Orientation = Orientation.Horizontal };

        if (_modoSelector)
        {
            var btnSel = Btn("✔  Seleccionar artículo", Color.FromRgb(22, 100, 58));
            var btnCx  = Btn("✕  Cancelar",             Color.FromRgb(150, 40, 27), 6);
            btnSel.Click += async (_, __) => await SeleccionarArticuloAsync();
            btnCx.Click  += (_, __) => { DialogResult = false; Close(); };
            btns.Children.Add(btnSel);
            btns.Children.Add(btnCx);
            _grid.MouseDoubleClick += async (_, e) =>
            {
                // El doble click puede burbujear desde CUALQUIER descendiente del DataGrid,
                // incluidas las flechas del scrollbar — clickear ahí repetidamente disparaba
                // esto como si el usuario hubiera elegido una fila. Solo confirma si el click
                // realmente cayó sobre una fila (DataGridRow/DataGridCell), no sobre el scrollbar.
                var origen = e.OriginalSource as DependencyObject;
                while (origen != null && origen is not DataGridRow && origen is not ScrollBar)
                    origen = VisualTreeHelper.GetParent(origen);
                if (origen is not DataGridRow) return;
                await SeleccionarArticuloAsync();
            };
        }
        else
        {
            var btnVP = Btn("🖨  Vista Previa", Color.FromRgb(41, 119, 180));
            var btnIm = Btn("Imprimir",         Color.FromRgb(22, 100, 58),  6);
            var btnCx = Btn("✕  Cerrar",        Color.FromRgb(150, 40, 27),  6);
            btnVP.Click += async (_, __) => await ImprimirAsync(preview: true);
            btnIm.Click += async (_, __) => await ImprimirAsync(preview: false);
            btnCx.Click += (_, __) => Close();
            btns.Children.Add(btnVP); btns.Children.Add(btnIm); btns.Children.Add(btnCx);
        }

        DockPanel.SetDock(btns, Dock.Right);
        dp.Children.Add(btns);

        // ── paginación central ────────────────────────────────────────────
        // selector por página: 20 | 50 | 100 | Todos
        Border MkChipPag(string lbl, int val)
        {
            bool activo = _porPagina == val;
            var txt = new TextBlock
            {
                Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4),
                Foreground = activo ? Brushes.White
                           : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            };
            var chip = new Border
            {
                CornerRadius    = new CornerRadius(12),
                Background      = activo
                                    ? new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))
                                    : Brushes.Transparent,
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child           = txt,
                Margin          = new Thickness(2, 0, 2, 0),
            };
            _pagChips.Add((chip, txt, val));
            chip.MouseLeftButtonUp += async (_, __) =>
            {
                _porPagina    = val;
                _paginaActual = 1;
                UpdatePagChips();
                await CargarPaginaAsync();
            };
            return chip;
        }

        var pagChips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Tag = "pagchips",
        };
        pagChips.Children.Add(new TextBlock
        {
            Text = "Filas:", FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0),
        });
        pagChips.Children.Add(MkChipPag("15",    15));
        pagChips.Children.Add(MkChipPag("20",    20));
        pagChips.Children.Add(MkChipPag("50",    50));
        pagChips.Children.Add(MkChipPag("100",   100));
        pagChips.Children.Add(MkChipPag("Todos", int.MaxValue));

        // botones de navegación ← página →
        Border NavBtn(string sym)
        {
            var tb = new TextBlock
            {
                Text = sym, FontSize = 14, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
            };
            return new Border
            {
                CornerRadius    = new CornerRadius(10),
                Background      = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child           = tb,
                Margin          = new Thickness(3, 0, 3, 0),
                MinWidth        = 28,
            };
        }

        _btnPrev   = NavBtn("←");
        _btnNext   = NavBtn("→");
        _lblPagina = new TextBlock
        {
            Text = "1 / 1", FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
        };

        _btnPrev.MouseLeftButtonUp += async (_, __) =>
        {
            if (_paginaActual > 1) { _paginaActual--; await CargarPaginaAsync(); }
        };
        _btnNext.MouseLeftButtonUp += async (_, __) =>
        {
            if (_paginaActual < TotalPaginas && _porPagina != int.MaxValue)
            { _paginaActual++; await CargarPaginaAsync(); }
        };

        var navRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
        };
        navRow.Children.Add(pagChips);
        navRow.Children.Add(_btnPrev);
        navRow.Children.Add(_lblPagina);
        navRow.Children.Add(_btnNext);

        DockPanel.SetDock(navRow, Dock.Right);
        dp.Children.Add(navRow);

        _lblTotal = new TextBlock
        {
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), Text = "Cargando...",
        };
        dp.Children.Add(_lblTotal);

        foot.Child = dp;
        return foot;
    }

    private void UpdatePagChips()
    {
        foreach (var (chip, txt, val) in _pagChips)
        {
            bool activo = _porPagina == val;
            chip.Background = activo
                ? new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))
                : Brushes.Transparent;
            txt.Foreground = activo
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // DATOS
    // ════════════════════════════════════════════════════════════════════════
    private async Task CargarAsync()
    {
        // TEMPORAL — instrumentación de timing para diagnosticar dónde se va el tiempo
        // percibido al abrir la pantalla (pedido explícito 2026-08-04). Quitar antes de
        // publicar; escribe a Debug.WriteLine, no afecta al usuario final.
        // ListarTodosAsync() (locales) y la primera página de artículos no dependen entre sí
        // — antes se esperaban en secuencia (round-trip de locales, RECIÉN AHÍ arrancaba el
        // de artículos), sumando sus latencias una atrás de la otra. Pedido explícito
        // 2026-08-04 (pantalla se sentía lenta al abrir): se piden en paralelo con
        // Task.WhenAll, recortando el tiempo real de espera de red al mayor de los dos en
        // vez de la suma. CargarPaginaAsync ya no dispara su propio round-trip de locales
        // (no lo hacía), así que alcanza con adelantar acá la llamada al repo de artículos
        // usando los mismos parámetros que arma CargarPaginaAsync en su primera ejecución.
        var termino0 = _txtBuscar?.Text.Trim() ?? "";
        var modo0    = (_cboModo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "codigo";
        var precargaAplica = !(_modoSelector && _idLocalFiltro > 0);

        var tLocales = _localRepo.ListarTodosAsync();
        var tPrimeraPagina = precargaAplica
            ? _artRepo.ObtenerVisorPaginadoAsync(termino0, modo0, _filtroStock, _filtroTipo,
                _paginaActual, _porPagina, _idLocalFiltro, incluirInactivos: _modoSimple)
            : Task.FromResult<(IEnumerable<VisorArticuloPag> filas, int total)>((Array.Empty<VisorArticuloPag>(), 0));

        await Task.WhenAll(tLocales, tPrimeraPagina);
        _locales = (await tLocales).ToList();
        var resultadoPrecargado = precargaAplica ? await tPrimeraPagina : ((IEnumerable<VisorArticuloPag>, int)?)null;

        // Pedido explícito: la columna del local relevante va PRIMERO (pegada a "Stock
        // Total"), antes que el resto — así no hay que buscarla entre las ~15 columnas. En
        // modo selector (desde Venta a Crédito) el local relevante es el de la SOLICITUD
        // (_idLocalFiltro) — el vendedor puede estar cargando una venta de un local distinto
        // al suyo, y ahí es donde importa ver el stock. En modo anclado normal (sin una
        // solicitud puntual) se usa el local de la sesión del usuario logueado. Debe
        // reordenarse ANTES de construir _idLocalAIndice, porque las columnas/índices
        // dependen de este orden.
        int? idLocalDestacado = _modoSelector && _idLocalFiltro > 0
            ? _idLocalFiltro
            : SessionService.Instance.LocalActual?.IdLocal;
        if (idLocalDestacado is int idD)
        {
            var propio = _locales.FirstOrDefault(l => l.IdLocal == idD);
            if (propio != null)
            {
                _locales.Remove(propio);
                _locales.Insert(0, propio);
            }
        }

        _idLocalAIndice = _locales
            .Select((l, i) => (l.IdLocal, i))
            .ToDictionary(x => x.IdLocal, x => x.i);

        // Columnas de stock por local — mismo diseño en modo anclado normal y en el selector
        // modal (pedido explícito: unificar la vista). Se agregan recién acá porque _locales
        // todavía está vacía cuando BuildGrid() se ejecutó en el constructor.
        AgregarColumnasStockPorLocal();

        // Reutiliza el resultado ya resuelto en el Task.WhenAll de arriba — evita pedirle a
        // la base la misma página de artículos una segunda vez.
        await CargarPaginaAsync(resultadoPrecargado);
    }

    // resultadoPrecargado: cuando CargarAsync() ya disparó esta misma consulta en paralelo
    // con la carga de locales (ver Task.WhenAll ahí), se reutiliza acá en vez de volver a
    // pedirla — evita duplicar el round-trip a la base en la primera carga de la ventana.
    private async Task CargarPaginaAsync((IEnumerable<VisorArticuloPag> rows, int total)? resultadoPrecargado = null)
    {
        if (_cargando) return;
        _cargando = true;
        try
        {
            _lblTotal.Text = "Cargando...";
            ActualizarNavBotones();

            var termino = _txtBuscar?.Text.Trim() ?? "";
            var modo    = (_cboModo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "codigo";

            List<VisorRow> filas;

            if (_modoSelector && _idLocalFiltro > 0)
            {
                // modal selector: artículos de ese local, respetando el combo Con/Sin/Todos
                var pag = await _artRepo.ObtenerVisorPaginadoModalAsync(
                    termino, _idLocalFiltro, _paginaActual, _porPagina, _filtroStock);
                filas = pag.Select(a => PagToRow(a)).ToList();
                // el modal no necesita total exacto para navegar; se recarga siempre
                _totalRegistros = filas.Count == _porPagina ? _paginaActual * _porPagina + 1 : (_paginaActual - 1) * _porPagina + filas.Count;
            }
            else
            {
                var (rows, total) = resultadoPrecargado
                    ?? await _artRepo.ObtenerVisorPaginadoAsync(
                        termino, modo, _filtroStock, _filtroTipo,
                        _paginaActual, _porPagina, _idLocalFiltro, incluirInactivos: _modoSimple);
                _totalRegistros = total;
                // Stock por local se carga solo al seleccionar un artículo (OnSelectionChanged)
                // No traemos toda PRICES aquí para no bloquear la navegación de páginas
                filas = rows.Select(PagToRow).ToList();
            }

            // Desglose de stock por local (columnas dinámicas): en modo anclado ya viene
            // incluido en cada VisorArticuloPag (ObtenerVisorPaginadoAsync lo trae en la misma
            // llamada QueryMultipleAsync que la página — ver AsignarStockPorLocal en el repo),
            // así que PagToRow ya lo vuelca en VisorRow.Stocks[] sin round-trip adicional. El
            // selector modal (ObtenerVisorPaginadoModalAsync) sigue sin necesitarlo: ya filtra
            // a un único local, por lo que no hay desglose que completar.
            _grid.ItemsSource = filas;

            var paginaStr = _porPagina == int.MaxValue
                ? $"{_totalRegistros:N0} artículos"
                : $"Pág {_paginaActual}/{TotalPaginas} — {_totalRegistros:N0} artículos";
            _lblTotal.Text = paginaStr;
            _lblPagina.Text = $"{_paginaActual} / {TotalPaginas}";
            ActualizarNavBotones();
        }
        finally { _cargando = false; }
    }

    private VisorRow PagToRow(VisorArticuloPag a)
    {
        // Tamaño ya acorde a _locales para que el binding indexado Stocks[i] de las columnas
        // dinámicas nunca falle. a.StockPorLocal viene vacío en modo selector (no se pide ahí,
        // ver ObtenerVisorPaginadoModalAsync) y queda en 0 por local, igual que antes.
        var stocks = new decimal[_locales.Count];
        foreach (var r in a.StockPorLocal)
            if (_idLocalAIndice.TryGetValue(r.IdLocal, out var idx))
                stocks[idx] = r.S;

        return new VisorRow
        {
            IdArt       = a.Id,
            Codigo      = a.Ca,
            Desc        = a.D,
            Marca       = a.MarcaNombre,
            Contado     = a.Contado,
            PVenta      = a.Pventa,
            Pc          = 0,
            MaxCuota    = a.Maxcuota,
            SoloContado = a.Slc,
            Activo      = a.Es,
            Stocks      = stocks,
            TotalStockOverride = a.StockTotal,
        };
    }

    private void ActualizarNavBotones()
    {
        if (_btnPrev == null) return;
        bool hayAnterior = _paginaActual > 1;
        bool haySiguiente = _paginaActual < TotalPaginas && _porPagina != int.MaxValue;
        _btnPrev.Opacity = hayAnterior ? 1 : 0.3;
        _btnPrev.Cursor  = hayAnterior ? Cursors.Hand : Cursors.Arrow;
        _btnNext.Opacity = haySiguiente ? 1 : 0.3;
        _btnNext.Cursor  = haySiguiente ? Cursors.Hand : Cursors.Arrow;
    }

    private async Task SeleccionarArticuloAsync()
    {
        if (_grid.SelectedItem is not VisorRow row)
        {
            MessageBox.Show("Seleccione un artículo de la lista.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Este bloqueo aplica solo al selector de VentaCreditoWindow (ver comentario del
        // constructor), así que un artículo marcado SoloContado=1 nunca debería poder cargarse
        // ahí — es contradictorio armar un plan de cuotas para un artículo que el sistema marca
        // como "solo contado". El filtro "Venta permitida" de arriba puede mostrarlo igual si el
        // cajero elige "Todos", así que se bloquea también en el momento de confirmar. En modo
        // simple (Compras/Precios) no hay plan de cuotas de por medio, así que no corresponde.
        if (_modoSelector && !_modoSimple && row.SoloContado == 1)
        {
            MessageBox.Show(
                $"El artículo \"{row.Codigo} - {row.Desc}\" está marcado como SOLO CONTADO.\n\n" +
                "No se puede vender a crédito. Si el cliente quiere este artículo, debe " +
                "realizarse como Venta Contado.",
                "Artículo solo contado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // En modo selector (llamado desde VentaCreditoWindow), no alcanza con que el artículo
        // aparezca en la grilla — el filtro "Todos"/"Sin stock" puede mostrarlo igual. Se
        // verifica el stock real en el local activo antes de confirmar, para no dejar cargar
        // a la venta un artículo que en la práctica no está disponible para entregar ahí.
        if (_modoSelector && _idLocalFiltro > 0)
        {
            var stockLocales  = (await _artRepo.ObtenerStockTodosLocalesAsync(row.IdArt)).ToList();
            var stockEnLocal  = stockLocales.FirstOrDefault(p => p.IdLocal == _idLocalFiltro);
            var localActivo   = _locales.FirstOrDefault(l => l.IdLocal == _idLocalFiltro)?.NombreLocal
                                 ?? $"local {_idLocalFiltro}";

            if (stockEnLocal == null || stockEnLocal.S <= 0)
            {
                var otrosConStock = stockLocales
                    .Where(p => p.IdLocal != _idLocalFiltro && p.S > 0)
                    .OrderByDescending(p => p.S)
                    .ToList();

                var detalle = otrosConStock.Count == 0
                    ? "No hay stock disponible de este artículo en ningún local."
                    : "Stock disponible en otros locales:\n" +
                      string.Join("\n", otrosConStock.Select(p => $"  •  {p.LocalNombre}: {p.S:N0}"));

                MessageBox.Show(
                    $"El artículo \"{row.Codigo} - {row.Desc}\" no tiene stock en {localActivo}.\n\n{detalle}",
                    "Sin stock en este local", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // En modo simple (selector liviano para Compras/Precios) no existe el panel Simulador
        // — _txtPctDesc/_txtEntrega/_txtPctRec/_txtCantCuotas nunca se construyen ahí, así que
        // no hay plan de pagos que trasladar. Se confirma directo con el artículo elegido.
        if (_modoSimple)
        {
            ArticuloSeleccionado = row;
            DialogResult = true;
            Close();
            return;
        }

        // Trasladar el plan de pagos configurado en el panel derecho — antes se perdía porque
        // VisorRow no tenía dónde guardarlo y VentaCreditoWindow reseteaba Entrega/Cuotas al
        // recibir el artículo (bug real reportado: cargar un % de descuento acá no se
        // reflejaba al ingresar el artículo a la venta).
        decimal.TryParse(_txtPctDesc.Text,  NumberStyles.Any, _cult, out var pctD);
        decimal.TryParse(_txtEntrega.Text,  NumberStyles.Any, _cult, out var ent);
        TryParsePorcentaje(_txtPctRec.Text, out var pctR);
        int.TryParse(_txtCantCuotas.Text, out var cuotas);
        if (cuotas <= 0) cuotas = 1;

        // Revalidación final del piso de 4,2% en 3+ cuotas — el LostFocus del campo ya lo
        // corrige al salir del TextBox, pero si el cajero hace click en "Confirmar" sin que el
        // campo llegue a perder el foco (o el valor quedó vacío/inválido), esto es la última
        // barrera antes de que el recargo se traslade a la venta real.
        if (cuotas >= 3 && pctR < 4.2m) pctR = 4.2m;

        row.PctDescuentoPlan = pctD;
        row.EntregaPlan      = ent;
        row.PctRecargoPlan   = pctR;
        row.CantCuotasPlan   = cuotas;
        row.ValorNetoPlan    = Math.Round(row.PVenta * (1 - pctD / 100), 0);

        ArticuloSeleccionado = row;
        DialogResult = true;
        Close();
    }

    // ════════════════════════════════════════════════════════════════════════
    // FILTRADO — dispara recarga paginada desde BD con debounce
    // ════════════════════════════════════════════════════════════════════════
    private void FiltrarGrid()
    {
        _paginaActual = 1;
        _filterCts?.Cancel();
        _filterCts = new System.Threading.CancellationTokenSource();
        var token = _filterCts.Token;
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (!token.IsCancellationRequested)
                    await CargarPaginaAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // COLOR FILAS
    // ════════════════════════════════════════════════════════════════════════
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not VisorRow row) return;

        Brush bg, fg;
        string? tooltip = null;

        // En modo selector, "solo contado" tiene prioridad visual sobre el color de stock.
        if (_modoSelector && row.SoloContado == 1)
        {
            bg = B(C6); fg = B(C5);
            tooltip = "Solo contado — no se puede vender a crédito";
        }
        else
        {
            var s = row.TotalStock;
            bg = s > 0 ? Brushes.White
               : s < 0 ? new SolidColorBrush(Color.FromRgb(253, 228, 224))
                       : new SolidColorBrush(Color.FromRgb(248, 249, 250));
            fg = s > 0 ? B(CT)
               : s < 0 ? B(C5)
                       : B(CS);
        }
        e.Row.ToolTip = tooltip;

        // Antes esto se asignaba directo a e.Row.Background/Foreground — un "local value" con
        // prioridad más alta que cualquier estilo/tema, que tapaba el highlight azul nativo de
        // selección de WPF en las celdas SIN CellStyle propio (Código/Descripción/Marca/etc.),
        // dejándolas sin resaltar mientras las columnas de stock (con su propio Trigger
        // explícito) sí cambiaban — la fila quedaba resaltada "a medias". Con un Style +
        // Trigger de IsSelected acá, WPF prioriza el trigger sobre el Setter base solo cuando
        // corresponde, igual que el resto de la grilla.
        var rowSt = new Style(typeof(DataGridRow));
        rowSt.Setters.Add(new Setter(DataGridRow.BackgroundProperty, bg));
        rowSt.Setters.Add(new Setter(DataGridRow.ForegroundProperty, fg));
        var selTrig = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, B(C2)));
        selTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Brushes.White));
        rowSt.Triggers.Add(selTrig);
        e.Row.Style = rowSt;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SELECCIÓN
    // ════════════════════════════════════════════════════════════════════════
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Modo simple: no hay panel de Stock por Local ni Simulador (ver Body()) — ninguno de
        // los controles que actualiza el resto de este método existe (_panelCodigo,
        // _dgStockLocal, _txtPctDesc, etc. quedan null!), así que no hay nada más que hacer acá.
        // La selección en sí ya la maneja _grid.SelectedItem directamente en Aceptar().
        if (_modoSimple) return;

        if (_grid.SelectedItem is not VisorRow row) { LimpiarPanel(); return; }

        _precioSel = row.PVenta;
        _panelCodigo.Text  = row.Codigo;
        _panelDesc.Text    = row.Desc;
        _panelContado.Text = row.Contado.ToString("N0", _cult);
        _panelPventa.Text  = row.PVenta.ToString("N0", _cult);
        _panelMaxC.Text    = row.MaxCuota.ToString();

        var total = row.TotalStock;
        _stockBadgeTxt.Text       = total.ToString("N0", _cult);
        _stockBadge.Background    = total > 0 ? B(C3) : total < 0 ? B(C6) : B(new Color { A = 255, R = 232, G = 238, B = 246 });
        _stockBadgeTxt.Foreground = total > 0 ? B(C1) : total < 0 ? B(C5) : B(CS);

        // Panel lateral: limpia la grilla y dispara query liviana solo para este artículo
        _dgStockLocal.ItemsSource = null;

        // _maxCuotaSeleccionado se actualiza PRIMERO, antes de tocar ningún TextBox — cada
        // asignación de .Text de acá abajo dispara su propio TextChanged (formateo, guard de
        // máximo cuotas, RecalcPlan), y esos handlers deben ver ya el máximo del artículo NUEVO,
        // no el del artículo anterior. Bug real detectado: con el orden viejo (_maxCuotaSeleccionado
        // recién al final), el guard de Cant.Cuotas comparaba contra el máximo del artículo
        // previamente seleccionado — con un artículo de Máximo 1 cuota, el guard dejaba pasar
        // valores de 2 o 3 sin corregir, y como el % Recargo se evaluaba en un RecalcPlan()
        // intermedio disparado antes de que _txtPctRec.Text terminara de asignarse al default,
        // el recargo podía quedar en 0 en la primera selección del artículo.
        _maxCuotaSeleccionado = row.MaxCuota;
        _lblCantCuotas.Text   = row.MaxCuota > 0
            ? $"Cant. Cuotas (máx. {row.MaxCuota})"
            : "Cant. Cuotas";

        // Nuevo artículo: se vuelve a activar el autocompletado de Entrega y Recargo hasta que
        // el cajero los edite a mano para ESTE artículo en particular.
        _entregaEditadaManualmente = false;
        _recargoEditadoManualmente = false;

        // Bug real detectado: estas asignaciones directas de .Text disparan sus propios
        // TextChanged (_txtPctRec/_txtEntrega), y esos handlers solo respetan "_calculando" como
        // señal de "esto es autocompletado, no marques el flag de edición manual" — pero
        // _calculando solo es true DENTRO de RecalcPlan(), no acá. Sin este guard, cada
        // selección de artículo terminaba marcando ambos flags como "editados a mano" por el
        // propio código de inicialización, dejando el autocompletado desactivado desde la
        // primera selección — nunca volvía a sugerir Entrega ni % Recargo al cambiar cuotas.
        _calculando = true;
        try
        {
            // Arranca en 2 cuotas por defecto (si el artículo lo permite) en vez del máximo
            // posible — a la empresa le conviene una entrega inicial más alta (con 2 cuotas
            // la Entrega sugerida es Valor/3, mayor que con el máximo de cuotas, donde queda
            // diluida). El vendedor sigue pudiendo subir Cant. Cuotas manualmente si el
            // cliente lo necesita, bajando la entrega desde ahí (pedido explícito).
            var cuotasIniciales = row.MaxCuota switch {
                <= 0 => 1,
                1    => 1,
                _    => Math.Min(2, row.MaxCuota)
            };
            _txtPctDesc.Text    = "0";
            _txtPctRec.Text     = PctRecargoSugerido(cuotasIniciales).ToString(_cult);
            _txtCantCuotas.Text = cuotasIniciales.ToString();
            _txtEntrega.Text    = "0";
            ActualizarEdicionRecargo(cuotasIniciales);
        }
        finally { _calculando = false; }

        RecalcPlan();

        // Carga stock por local en background (query liviana: WHERE IDART = @id)
        var idArt = row.IdArt;
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var stockPorLocal = (await _artRepo.ObtenerStockTodosLocalesAsync(idArt)).ToList();
                // verificar que el artículo seleccionado no cambió mientras cargaba
                if (_grid.SelectedItem is not VisorRow sel || sel.IdArt != idArt) return;

                // Completa con Stock=0 los locales donde el artículo nunca se cargó en PRICES
                // (ObtenerStockTodosLocalesAsync solo trae los que sí tienen fila) — pedido
                // explícito: mostrar todos los locales siempre, sin distinguir "nunca cargado"
                // de "cargado con stock 0".
                var stockPorIdLocal = stockPorLocal.ToDictionary(p => p.IdLocal);
                var items = _locales
                    .Where(l => !_modoSelector || _idLocalFiltro == 0 || l.IdLocal == _idLocalFiltro)
                    .Select(l => stockPorIdLocal.TryGetValue((byte)l.IdLocal, out var p)
                        ? p
                        : new CrediSoft.Core.Models.Price { IdLocal = (byte)l.IdLocal, LocalNombre = l.NombreLocal, S = 0 })
                    .ToList();

                var idLocalDestacado = LocalDestacadoParaStock();
                items = items
                    .OrderByDescending(p => idLocalDestacado.HasValue && p.IdLocal == idLocalDestacado.Value)
                    .ThenBy(p => p.LocalNombre)
                    .ToList();

                _dgStockLocal.ItemsSource = items;
            }
            catch { /* no bloquear la UI si falla el detalle de stock */ }
        });
    }

    private void LimpiarPanel()
    {
        _precioSel = 0;
        _panelCodigo.Text = "—"; _panelDesc.Text = "Seleccione un artículo";
        _panelContado.Text = "—"; _panelPventa.Text = "—"; _panelMaxC.Text = "0";
        _stockBadgeTxt.Text = "—"; _dgStockLocal.ItemsSource = null;
        _outDescuento.Text = _outValor.Text = _outValorSaldo.Text = _outCuotaSinInteres.Text =
            _outValorMensual.Text = _outValorFinal.Text = "—";

        _maxCuotaSeleccionado = 0;
        _lblCantCuotas.Text   = "Cant. Cuotas";
        _entregaEditadaManualmente = false;
        _recargoEditadoManualmente = false;
        ActualizarEdicionRecargo(0);
    }

    // % Recargo es editable únicamente cuando hay 3+ cuotas (rangos 3-10 y 11+) — en 1-2 cuotas
    // sigue siendo 0% fijo, no editable, sin piso.
    private void ActualizarEdicionRecargo(int cuotas)
    {
        var habilitar = cuotas >= 3;
        _txtPctRec.IsReadOnly = !habilitar;
        _txtPctRec.Focusable  = habilitar;
        _txtPctRec.Background = habilitar ? Brushes.White : B(C4);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PLAN DE PAGO
    // ════════════════════════════════════════════════════════════════════════
    private void RecalcPlan()
    {
        if (_calculando) return;
        _calculando = true;
        try
        {
            if (_precioSel <= 0)
            {
                _outDescuento.Text = _outValor.Text = _outValorSaldo.Text =
                    _outCuotaSinInteres.Text = _outValorMensual.Text = _outValorFinal.Text = "—";
                return;
            }
            decimal.TryParse(_txtPctDesc.Text, NumberStyles.Any, _cult, out var pctD);
            int.TryParse(_txtCantCuotas.Text, out var cuotas);
            if (cuotas <= 0) cuotas = 1;

            var desc  = Math.Round(_precioSel * pctD / 100, 0);
            var valor = _precioSel - desc;

            // Entrega sugerida = Valor (con descuento) / (Cant. Cuotas + 1) — reparte el valor en
            // partes iguales entre la entrega y cada cuota (1 parte para la entrega, el resto
            // para las cuotas), NO solo Valor/Cuotas. Confirmado por el cliente con ejemplo real:
            // Precio 60.000, 1 cuota → Entrega 30.000 y Cuota 30.000 (60.000/2), mitad y mitad —
            // Valor/Cuotas daría 60.000 (el 100%), dejando la cuota en 0, que es el bug real
            // reportado. Esta misma fórmula es también el umbral mínimo de Entrega para que
            // aplique "sin recargo" en 1-2 cuotas (ver más abajo) — es la misma cuenta.
            //
            // Base de 2 cuotas (o 1, si el artículo no permite más) en vez de "cuotas" actual:
            // a la empresa le conviene una entrega inicial alta, y el default de Cant. Cuotas
            // ya arranca en 2 (ver selección de artículo). Si el vendedor sube manualmente la
            // cantidad de cuotas después, la Entrega sugerida NO debe diluirse recalculando con
            // más cuotas — queda "congelada" en el monto de 2 cuotas, y es el saldo el que se
            // reparte en más cuotas. Sigue pudiendo bajarla a mano si el cliente lo necesita
            // (pedido explícito: "si el vendedor quiere bajar más luego puede bajar").
            var cuotasBaseEntrega = Math.Min(cuotas, _maxCuotaSeleccionado == 1 ? 1 : 2);
            var entregaSugerida = Math.Round(valor / (cuotasBaseEntrega + 1), 0);
            if (!_entregaEditadaManualmente)
                _txtEntrega.Text = entregaSugerida.ToString("N0", _cult);

            decimal.TryParse(_txtEntrega.Text, NumberStyles.Any, _cult, out var ent);

            // Link "Usar sugerida" — solo visible cuando el vendedor ya tocó Entrega a mano Y el
            // valor actual difiere del sugerido (si coinciden, no hay nada que "restaurar").
            if (_entregaEditadaManualmente && ent != entregaSugerida)
            {
                _lnkEntregaSugerida.Text = $"↺ Usar entrega sugerida: Gs. {entregaSugerida:N0}";
                _lnkEntregaSugerida.Visibility = Visibility.Visible;
            }
            else
            {
                _lnkEntregaSugerida.Visibility = Visibility.Collapsed;
            }

            ActualizarEdicionRecargo(cuotas);
            // En 1-2 cuotas el % Recargo sigue sin ser editable (0% fijo, o 4,2% automático si la
            // Entrega no alcanza el umbral mínimo). Si el cajero había editado el recargo a mano
            // en 3+ cuotas y después baja a 1-2, esa edición ya no aplica.
            if (cuotas <= 2) _recargoEditadoManualmente = false;

            // % Recargo es editable por el cajero SOLO en 3-10 y 11+ cuotas (pedido explícito:
            // el vendedor puede subir el recargo para ganar más), pero nunca por debajo de 4,2%
            // en esos dos rangos — ni siquiera en 11+ cuotas, que sugiere 4% (por debajo del
            // piso). En 1-2 cuotas el "sin recargo" no es automático: depende de que la Entrega
            // alcance el umbral mínimo (entregaSugerida, arriba). Si el cajero tipeó una Entrega
            // menor a mano, se aplica igual el 4,2% aunque sean 1-2 cuotas.
            var pctSugerido = PctRecargoSugerido(cuotas);
            if (cuotas <= 2 && pctSugerido == 0m && ent < entregaSugerida) pctSugerido = 4.2m;
            if (!(cuotas >= 3 && _recargoEditadoManualmente))
                _txtPctRec.Text = pctSugerido.ToString(_cult);
            TryParsePorcentaje(_txtPctRec.Text, out var pctR);
            // El piso de 4,2% NO se fuerza acá sobre el TEXTO del campo — mientras el cajero está
            // tipeando (ej. borra "6" para escribir "8,5"), el texto pasa por estados intermedios
            // válidos (vacío, "8") que son momentáneamente menores a 4,2 sin que eso signifique
            // que el usuario quiso dejarlo así. Forzar el piso en cada tecla trababa la edición
            // (bug real reportado: no se podía borrar y reescribir el recargo). El piso se aplica
            // solo al perder el foco (OnPctRecLostFocus) y se vuelve a validar antes de guardar
            // (OnAgregarArticulo/GuardarLinea) — acá, para el CÁLCULO de los totales en pantalla
            // mientras se edita, alcanza con no dejar que un valor momentáneamente bajo infle el
            // recargo mostrado por debajo del piso real que se va a aplicar al final.
            if (cuotas >= 3 && pctR < 4.2m) pctR = 4.2m;

            var saldo = valor - ent;
            // % Recargo es una tasa POR CUOTA (interés simple por período financiado), no un
            // cargo único sobre el saldo — se multiplica por la cantidad de cuotas antes de
            // aplicarse. El sistema viejo (CrediSoft_local.exe) usaba 4,5% (verificado en vivo:
            // Precio 11.500.000, Entrega 3.833.333, 3 cuotas → Valor mensual 2.900.556, Valor
            // Final 8.701.667, exacto con 4,5%), pero Credimar pidió bajar la tasa a 4,2% en el
            // sistema nuevo como decisión de negocio. A más cuotas, mayor el recargo total,
            // correcto para una financiación más larga — excepto 11+ cuotas, donde la tasa baja
            // a 4%.
            var rec     = Math.Round(saldo * pctR / 100 * cuotas, 0);
            var mensual = Math.Ceiling((saldo + rec) / cuotas);
            // "Cuota sin % Recargo": solo el saldo repartido entre cuotas, sin el % Recargo — se
            // muestra al lado de "Cuota con % Recargo" para que quede claro cuánto agrega la
            // financiación por cuota, no solo en el total final.
            var cuotaSinInteres = Math.Ceiling(saldo / cuotas);
            // VALOR FINAL A PAGAR = Entrega + saldo financiado con recargo — el costo TOTAL real
            // que termina pagando el cliente por el artículo, no solo el saldo a cuotas. Bug real
            // reportado: antes mostraba solo "saldo + recargo" (sin la Entrega), así que con 1
            // cuota (donde la Entrega autocompletada cubre el 100% y el saldo queda en 0) esta
            // card, la más grande y destacada de la pantalla, mostraba literalmente "Gs. 0" —
            // ambiguo y peligroso, un cajero podía leerlo como "no hay que cobrar nada".
            var totalAPagar = ent + saldo + rec;

            _outDescuento.Text       = $"Gs. {desc:N0}";
            _outValor.Text           = $"Gs. {valor:N0}";
            _outValorSaldo.Text      = $"Gs. {saldo:N0}";
            // Mostrar SOLO la card que corresponde al monto que realmente se cobra por cuota —
            // con recargo (rec > 0): "Cuota con % Recargo" muestra el monto, "Cuota sin %
            // Recargo" pasa a "No aplica" (mostrar las dos cifras a la vez, una de ellas
            // hipotética, confundía al cajero sobre cuál era la real). Sin recargo (1-2 cuotas
            // con Entrega suficiente): es al revés, "Cuota sin % Recargo" es la real.
            _outCuotaSinInteres.Text = rec > 0 ? "No aplica" : $"Gs. {cuotaSinInteres:N0}";
            _outValorMensual.Text    = rec > 0 ? $"Gs. {mensual:N0}" : "No aplica";
            _outValorFinal.Text      = $"Gs. {totalAPagar:N0}";
        }
        finally { _calculando = false; }
    }

    // Desglose cuota por cuota — misma fórmula que RecalcPlan (Ceiling, N cuotas idénticas,
    // sin prorratear ningún remanente en la última) para que la proforma coincida exactamente
    // con los totales ya mostrados en el panel y con AGREGAR_GENERADAS_CS (el SP que genera
    // las cuotas reales al confirmar la venta usa el mismo monto fijo repetido por cuota).
    private void MostrarProforma()
    {
        if (_precioSel <= 0)
        {
            MessageBox.Show("Primero seleccioná un artículo.", "Proforma",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(_txtPctDesc.Text, NumberStyles.Any, _cult, out var pctD);
        int.TryParse(_txtCantCuotas.Text, out var cuotas);
        if (cuotas <= 0) cuotas = 1;
        TryParsePorcentaje(_txtPctRec.Text, out var pctR);
        decimal.TryParse(_txtEntrega.Text, NumberStyles.Any, _cult, out var ent);

        var desc  = Math.Round(_precioSel * pctD / 100, 0);
        var valor = _precioSel - desc;
        var saldo = valor - ent;
        var rec     = Math.Round(saldo * pctR / 100 * cuotas, 0);
        var mensual = Math.Ceiling((saldo + rec) / cuotas);

        // Vencimientos: réplica de AGREGAR_GENERADAS_CS (el SP que realmente genera las cuotas
        // al confirmar la venta) — la cuota 1 vence en la fecha de inicio del plan (acá
        // aproximada a hoy, ya que este simulador corre ANTES de cargar una venta real y no
        // tiene su propio campo de fecha), y cada cuota siguiente +1 mes exacto sobre la
        // anterior. Verificado contra un crédito real generado hoy mismo en producción (6
        // cuotas: 15/07, 15/08, 15/09... — la primera NO es "hoy + 1 mes", es prácticamente
        // "hoy"; equivocar este offset en la proforma haría ver un mes de más en cada cuota).
        //
        // La fila NCUOTA=1 siempre es la Entrega (tenga monto o sea Gs. 0) — GENERADAS graba
        // esa fila SIEMPRE, y recién NCUOTA=2..(cuotas+1) son las cuotas reales acá calculadas.
        // Sin esto, la Proforma mostraba "1/2, 2/2" para las cuotas reales, pero al confirmar
        // la venta esas mismas cuotas quedaban grabadas como NCUOTA=2/NCUOTA=3 — un desfase
        // que confundía al cajero al comparar la Proforma contra la grilla de Cobrar Cuota
        // (que sí muestra el NCUOTA real).
        var filas = new List<FilaProformaCuota>();
        var fechaBase = DateTime.Today;
        filas.Add(new FilaProformaCuota(1, fechaBase, ent, 0));
        for (int i = 1; i <= cuotas; i++)
            filas.Add(new FilaProformaCuota(i + 1, fechaBase.AddMonths(i - 1), mensual, pctR));

        var pf = new ProformaModal(
            codigo: _panelCodigo.Text, descripcion: _panelDesc.Text,
            precioLista: _precioSel, pctDescuento: pctD, descuento: desc, valorNeto: valor,
            entrega: ent, saldo: saldo, pctRecargo: pctR, recargoTotal: rec,
            totalCuotas: mensual * cuotas, totalAPagar: ent + saldo + rec,
            cuotas: filas) { Owner = this };
        pf.ShowDialog();
    }

    // ════════════════════════════════════════════════════════════════════════
    // IMPRIMIR
    // ════════════════════════════════════════════════════════════════════════
    private async Task ImprimirAsync(bool preview)
    {
        var filas = (_grid.ItemsSource as IEnumerable<VisorRow>)?.ToList();
        if (filas == null || filas.Count == 0)
        { MessageBox.Show("No hay datos para imprimir.", "Imprimir", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var (impresora, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");
        var p = new CrediSoft.UI.Views.Maestros.ArticulosPagina
        {
            Filas = filas.Select(r => new CrediSoft.UI.Views.Maestros.FilaArticuloImp(
                Codigo: r.Codigo, Descripcion: r.Desc, Gravada: "—", Iva: "—",
                MaxCuota: r.MaxCuota > 0 ? r.MaxCuota.ToString() : "—",
                StockMinimo: "—", SoloContado: "—",
                Seccion: "", Proveedor: "", Subcategoria: "", Categoria: "",
                Pais: "", Marca: "", UnidadMedida: "", Estado: "Activo")).ToList(),
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            Filtro   = _txtBuscar?.Text.Trim() ?? "",
            Impresora = impresora,
            LogoPath = CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
        };

        if (preview)
            new CrediSoft.UI.Views.Maestros.ArticulosPreviewWindow(p) { Owner = this }.ShowDialog();
        else
            CrediSoft.UI.Views.Maestros.ArticulosImpresora.Imprimir(p);
    }
}

// ── Converters badge stock ───────────────────────────────────────────────────
internal sealed class StockBgConv : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is decimal d)
        {
            if (d > 0) return new SolidColorBrush(Color.FromRgb( 31, 119, 180));  // azul sólido
            if (d < 0) return new SolidColorBrush(Color.FromRgb(192,  40,  27));  // rojo sólido
        }
        return new SolidColorBrush(Color.FromRgb(210, 215, 222));  // gris
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

internal sealed class StockFgConv : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is decimal d)
        {
            if (d > 0) return new SolidColorBrush(Colors.White);
            if (d < 0) return new SolidColorBrush(Colors.White);
        }
        return new SolidColorBrush(Color.FromRgb( 80,  95, 110));  // gris oscuro
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// ── Converters Solo contado ──────────────────────────────────────────────────
internal sealed class SoloContadoTextConv : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is byte b && b == 1 ? "Sí" : "No";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

internal sealed class SoloContadoColorConv : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is byte b && b == 1
            ? new SolidColorBrush(Color.FromRgb(192, 40, 27))   // rojo — solo contado
            : new SolidColorBrush(Color.FromRgb(30, 110, 66));  // verde — admite crédito
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

internal sealed class SoloContadoFgConv : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        if (values.Length == 2 && values[1] is true) return Brushes.White;
        var b = values.Length > 0 && values[0] is byte v ? v : (byte)0;
        return b == 1
            ? new SolidColorBrush(Color.FromRgb(192, 40, 27))   // rojo — solo contado
            : new SolidColorBrush(Color.FromRgb(30, 110, 66));  // verde — admite crédito
    }
    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotImplementedException();
}

// ── Converters Estado (Activo/Inactivo) — solo usados por el selector simple ────────────
internal sealed class ActivoTextConv : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is byte b && b == 1 ? "Activo" : "Inactivo";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

internal sealed class ActivoFgConv : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        if (values.Length == 2 && values[1] is true) return Brushes.White;
        var b = values.Length > 0 && values[0] is byte v ? v : (byte)1;
        return b == 1
            ? new SolidColorBrush(Color.FromRgb(30, 110, 66))   // verde — activo
            : new SolidColorBrush(Color.FromRgb(192, 40, 27));  // rojo — inactivo
    }
    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotImplementedException();
}
