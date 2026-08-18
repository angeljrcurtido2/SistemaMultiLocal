using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Caja;
using CrediSoft.UI.Views.Compras;
using CrediSoft.UI.Views.Shared;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Informes;

// ══════════════════════════════════════════════════════════════════════════════
//  COBROS PENDIENTES  — rediseñado con KPIs, grilla enriquecida y búsqueda inline
// ══════════════════════════════════════════════════════════════════════════════
public class PendientesWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Header KPIs
    private TextBlock _kpiClientes  = null!;
    private TextBlock _kpiTotal     = null!;
    private TextBlock _kpiCuotas    = null!;
    private TextBlock _lblFiltroSub = null!;

    // Barra de búsqueda inline
    private TextBox   _txtBuscar    = null!;
    private ComboBox  _cboLocal     = null!;

    // Grilla principal
    private DataGrid  _grid         = null!;

    // Estado de filtro
    private List<FilaPendiente> _todos = new();
    private DateTime? _fechaDesde;
    private DateTime? _fechaHasta;
    private int?      _idLocal;
    private string    _localNombre = "Todos los locales";

    private static System.Windows.Media.SolidColorBrush PBr(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public PendientesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title  = "Cobros Pendientes";
        Width  = 1020; Height = 650;
        MinWidth = 860; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = PBr("#EEF2F6");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await MostrarFiltroYCargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── HEADER ──────────────────────────────────────────────────────────
        var hdr = new Border { Background = PBr("#0E2F44"), Padding = new Thickness(18, 12, 18, 12) };
        DockPanel.SetDock(hdr, Dock.Top);

        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Título + subtítulo filtro activo
        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,28,0) };
        hdrTxt.Children.Add(new TextBlock { Text = "COBROS PENDIENTES",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        _lblFiltroSub = new TextBlock { Text = "Cargando...",
            Foreground = PBr("#7FB3D3"), FontSize = 10.5, Margin = new Thickness(0,3,0,0) };
        hdrTxt.Children.Add(_lblFiltroSub);
        Grid.SetColumn(hdrTxt, 0); hdrG.Children.Add(hdrTxt);

        // KPIs
        Border KpiCard(string label, out TextBlock valTb, string bg = "#1A4F6E")
        {
            valTb = new TextBlock { FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center };
            var sp = new StackPanel { Margin = new Thickness(0,2,0,2) };
            sp.Children.Add(valTb);
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9,
                Foreground = PBr("#7FB3D3"), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,2,0,0) });
            return new Border { Background = PBr(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14,7,14,7), Margin = new Thickness(6,0,6,0), Child = sp };
        }
        var kpiRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        kpiRow.Children.Add(KpiCard("CLIENTES",       out _kpiClientes));
        kpiRow.Children.Add(KpiCard("TOTAL A COBRAR", out _kpiTotal));
        kpiRow.Children.Add(KpiCard("CUOTAS PEND.",   out _kpiCuotas));
        Grid.SetColumn(kpiRow, 1); hdrG.Children.Add(kpiRow);

        // Botones header
        var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        var btnFiltrar = new Button {
            Content = "⚙  Filtro", Height = 34, Padding = new Thickness(14,0,14,0),
            Margin = new Thickness(0,0,8,0),
            Background = PBr("#2A7AB5"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnFiltrar.MouseEnter += (_, _) => btnFiltrar.Background = PBr("#1F6089");
        btnFiltrar.MouseLeave += (_, _) => btnFiltrar.Background = PBr("#2A7AB5");
        btnFiltrar.Click += async (_, _) => await MostrarFiltroYCargar();
        var btnCerrar = new Button {
            Content = "✕  Cerrar", Height = 34, Padding = new Thickness(14,0,14,0),
            Background = PBr("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        hdrBtns.Children.Add(btnFiltrar);
        hdrBtns.Children.Add(btnCerrar);
        Grid.SetColumn(hdrBtns, 2); hdrG.Children.Add(hdrBtns);
        hdr.Child = hdrG;
        root.Children.Add(hdr);

        // ── BARRA DE BÚSQUEDA INLINE ─────────────────────────────────────────
        var barraFiltro = new Border { Background = PBr("#1A4F6E"),
            Padding = new Thickness(14, 10, 14, 10) };
        DockPanel.SetDock(barraFiltro, Dock.Top);
        var barraG = new Grid();
        barraG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barraG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        barraG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        _txtBuscar = new TextBox {
            Height = 32, FontSize = 12, Padding = new Thickness(10,0,10,0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = PBr("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            CaretBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0) };
        var buscarBorder = new Border {
            Background = PBr("#0E2F44"), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(1), Margin = new Thickness(0,0,10,0) };
        var buscarStack = new StackPanel { Orientation = Orientation.Horizontal };
        buscarStack.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
            Foreground = PBr("#7FB3D3"), Margin = new Thickness(8,0,4,0) });
        buscarStack.Children.Add(_txtBuscar);
        buscarBorder.Child = buscarStack;
        _txtBuscar.TextChanged += (_, _) => AplicarFiltroInline();
        Grid.SetColumn(buscarBorder, 0); barraG.Children.Add(buscarBorder);

        _cboLocal = new ComboBox { Height = 32, FontSize = 12, Margin = new Thickness(0,0,10,0),
            VerticalContentAlignment = VerticalAlignment.Center };
        _cboLocal.SelectionChanged += (_, _) => AplicarFiltroInline();
        Grid.SetColumn(_cboLocal, 1); barraG.Children.Add(_cboLocal);

        var btnImprimir = new Button {
            Content = "🖨  Imprimir", Height = 32, Padding = new Thickness(12,0,12,0),
            Background = PBr("#0E2F44"), Foreground = PBr("#4FC3F7"),
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(1), BorderBrush = PBr("#2A7AB5"),
            Cursor = Cursors.Hand };
        btnImprimir.Click += (_, _) => ImprimirReportePendientes();
        Grid.SetColumn(btnImprimir, 2); barraG.Children.Add(btnImprimir);

        barraFiltro.Child = barraG;
        root.Children.Add(barraFiltro);

        // ── FOOTER ───────────────────────────────────────────────────────────
        var footer = new Border { Background = PBr("#0E2F44"), Padding = new Thickness(16,8,16,8) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var footSp = new StackPanel { Orientation = Orientation.Horizontal };
        var _footClientes = new TextBlock { Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,28,0) };
        var _footTotal    = new TextBlock { Foreground = PBr("#4FC3F7"),
            FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,28,0) };
        var _footCuotas   = new TextBlock { Foreground = PBr("#7FB3D3"), FontSize = 11.5 };
        footSp.Children.Add(_footClientes);
        footSp.Children.Add(_footTotal);
        footSp.Children.Add(_footCuotas);
        footer.Child = footSp;
        // guardamos refs para actualizar luego
        _footClientesRef = _footClientes;
        _footTotalRef    = _footTotal;
        _footCuotasRef   = _footCuotas;
        root.Children.Add(footer);

        // ── GRILLA PRINCIPAL ─────────────────────────────────────────────────
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, PBr("#0E2F44")));
        colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,8,10,8)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, PBr("#1A4F6E")));
        colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));

        // Row style para cuotas vencidas (VTO < hoy)
        var rowStyle = new Style(typeof(DataGridRow));
        var vencidaTrigger = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EsVencida"), Value = true };
        vencidaTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PBr("#FDECEA")));
        vencidaTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, PBr("#C62828")));
        rowStyle.Triggers.Add(vencidaTrigger);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 34,
            FontSize = 12, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = PBr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = PBr("#DDEEFF"),
            ColumnHeaderStyle = colHdrStyle,
            RowStyle = rowStyle,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserSortColumns = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

        DataGridTextColumn GC(string h, string path, double w, string? fmt = null, bool right = false)
        {
            var col = new DataGridTextColumn {
                Header = h, SortMemberPath = path,
                Width = w > 0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                               : new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = fmt != null
                    ? new System.Windows.Data.Binding(path) { StringFormat = fmt }
                    : new System.Windows.Data.Binding(path) };
            if (right)
            {
                var s = new Style(typeof(DataGridCell));
                s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.CellStyle = s;
            }
            return col;
        }

        _grid.Columns.Add(GC("Local",       "LOCAL_NOMBRE",  0));
        _grid.Columns.Add(GC("Cliente",      "CLI_NOMBRE",    0));
        _grid.Columns.Add(GC("Nº Venta",     "NVENTACHAR",  110));
        _grid.Columns.Add(GC("Nº Cuota",     "NCUOTA",       72, null, true));
        _grid.Columns.Add(GC("Vencimiento",  "VTO_STR",      98));
        _grid.Columns.Add(GC("Monto Gs.",    "MONTO_CUOTA", 115, "N0", true));
        _grid.Columns.Add(GC("Saldo Gs.",    "DEBE",        115, "N0", true));

        _grid.MouseDoubleClick += async (_, _) => {
            if (_grid.SelectedItem is FilaPendiente r) await MostrarDetallePendiente(r);
        };
        _grid.SelectionChanged += async (_, _) => {
            if (_grid.SelectedItem is FilaPendiente r) await MostrarDetallePendiente(r);
        };

        root.Children.Add(_grid);
        Content = root;
    }

    // Refs del footer actualizables
    private TextBlock _footClientesRef = null!;
    private TextBlock _footTotalRef    = null!;
    private TextBlock _footCuotasRef   = null!;

    // ── MODAL DE DETALLE ────────────────────────────────────────────────────
    private async Task MostrarDetallePendiente(FilaPendiente r)
    {
        // Colores según vencimiento
        var esVencida = r.EsVencida;
        var accentColor = esVencida ? "#C62828" : "#1A4F6E";
        var accentBg    = esVencida ? "#FDECEA" : "#E3F2FD";

        // ── consulta enriquecida ──────────────────────────────────────────
        DetallePendienteExtra? extra = null;
        List<FilaCuotaDetallePend> cuotas  = new();
        List<FilaArticuloPend>     arts    = new();
        List<FilaCobroPend>        cobros  = new();
        try
        {
            using var conn = _db.Create();

            extra = await conn.QueryFirstOrDefaultAsync<DetallePendienteExtra>(@"
SELECT
    RTRIM(ISNULL(CLI.CI_CLIENTE,''))        AS Ci,
    RTRIM(ISNULL(CLI.TELEFONO_CLIENTE,''))  AS Telefono,
    RTRIM(ISNULL(CLI.CIUDAD_CLIENTE,''))    AS Ciudad,
    RTRIM(ISNULL(CLI.DIRECCION_CLIENTE,'')) AS Direccion,
    RTRIM(ISNULL(CLI.EMPRESA_LABORAL,''))   AS Empresa,
    ISNULL(CLI.CRED_MAX,0)                  AS CredMax,
    CASE WHEN CS.ID_GARANTE>1 THEN RTRIM(ISNULL(GAR.NOMBRE_CLIENTE,'')) ELSE '' END AS Garante,
    CASE WHEN CS.ID_GARANTE>1 THEN RTRIM(ISNULL(GAR.CI_CLIENTE,''))     ELSE '' END AS GaranteCI,
    CASE WHEN CS.ID_GARANTE>1 THEN RTRIM(ISNULL(GAR.TELEFONO_CLIENTE,'')) ELSE '' END AS GaranteTel,
    CS.TOTAL, CS.DEBE, CS.HABER,
    CS.MONTO_CUOTA,
    (SELECT COUNT(*) FROM GENERADAS G2 WHERE G2.IDCAB=CS.IDCAB AND G2.ESTADO=0) AS CuotasPend,
    (SELECT COUNT(*) FROM GENERADAS G3 WHERE G3.IDCAB=CS.IDCAB)                 AS CuotasTotal,
    (SELECT COUNT(*) FROM GENERADAS G4 WHERE G4.IDCAB=CS.IDCAB AND G4.ESTADO=0 AND G4.VTO < GETDATE()) AS CuotasVencidas,
    CONVERT(VARCHAR(10),CS.FECHA,103)       AS FechaVenta,
    RTRIM(ISNULL(U.NOMBRE_USUARIO,'—'))     AS Vendedor
FROM CABECERA_SALES CS
INNER JOIN CLIENTES CLI ON CLI.ID_CLIENTE=CS.ID_CLIENTE
LEFT  JOIN CLIENTES GAR ON GAR.ID_CLIENTE=CS.ID_GARANTE AND CS.ID_GARANTE>1
LEFT  JOIN USUARIOS  U  ON U.ID_USUARIO=CS.ID_USUARIO
WHERE CS.IDCAB=@idcab", new { idcab = r.IDCAB });

            cuotas = (await conn.QueryAsync<FilaCuotaDetallePend>(@"
SELECT NCUOTA, MONTO,
    CONVERT(VARCHAR(10),VTO,103)         AS VtoStr,
    VTO                                  AS VtoDate,
    CASE ESTADO WHEN 0 THEN 'Pendiente' ELSE 'Cobrada' END AS EstadoStr,
    CONVERT(VARCHAR(10),FECHACOBRADO,103) AS FechaCobrado
FROM GENERADAS WHERE IDCAB=@idcab ORDER BY NCUOTA", new { idcab = r.IDCAB })).ToList();

            arts = (await conn.QueryAsync<FilaArticuloPend>(@"
SELECT RTRIM(A.D) AS Nombre, DS.CANTIDAD, DS.PV,
    CAST(DS.CANTIDAD * DS.PV AS DECIMAL(18,0)) AS Subtotal
FROM DETALLES_SALES DS
INNER JOIN ARTICULOS A ON A.ID=DS.IDART
WHERE DS.IDCAB=@idcab ORDER BY DS.IDDET", new { idcab = r.IDCAB })).ToList();

            cobros = (await conn.QueryAsync<FilaCobroPend>(@"
SELECT CONVERT(VARCHAR(10),P.FECHA,103) AS FechaStr,
    P.MONTO,
    RTRIM(ISNULL(U2.NOMBRE_USUARIO,'—')) AS Usuario,
    ISNULL(P.OBSERVACION,'') AS Obs
FROM PAGOS P
LEFT JOIN USUARIOS U2 ON U2.ID_USUARIO=P.ID_USUARIO
WHERE P.IDCAB=@idcab ORDER BY P.FECHA DESC", new { idcab = r.IDCAB })).ToList();
        }
        catch { /* si falla, igual mostramos datos básicos de FilaPendiente */ }

        // ── ventana modal ─────────────────────────────────────────────────
        var dlg = new Window {
            Title = $"Detalle — {r.CLI_NOMBRE}",
            Width = 720, Height = 680,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.CanResize,
            Background = PBr("#F4F8FB"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };

        // ── helpers ───────────────────────────────────────────────────────
        static System.Windows.Media.SolidColorBrush DBr(string h) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

        TextBlock MiniLbl(string t) => new TextBlock {
            Text = t, FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = DBr("#7FB3D3"), Margin = new Thickness(0,0,0,2) };
        TextBlock Val(string t, bool bold = false, string? color = null) => new TextBlock {
            Text = t, FontSize = 12,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = color != null ? DBr(color) : DBr("#0D1F2D"),
            TextWrapping = TextWrapping.Wrap };
        Border Card(string titulo, UIElement child) {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock {
                Text = titulo, FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = DBr("#1A4F6E"), Margin = new Thickness(0,0,0,8),
                TextDecorations = System.Windows.TextDecorations.Underline });
            sp.Children.Add(child);
            return new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = DBr("#D6E5EF"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(14,12,14,12),
                Margin = new Thickness(0,0,0,10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    ShadowDepth=1, BlurRadius=4, Opacity=0.07,
                    Color=System.Windows.Media.Colors.Black, Direction=270 },
                Child = sp }; }
        Grid TwoCol(params (string lbl, string val, bool bold, string? color)[] items) {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0, col = 0;
            foreach (var (lbl, val, bold, color) in items) {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cell = new StackPanel { Margin = new Thickness(0,0,col==0?16:0,10) };
                cell.Children.Add(MiniLbl(lbl));
                cell.Children.Add(Val(val, bold, color));
                Grid.SetRow(cell, row); Grid.SetColumn(cell, col); g.Children.Add(cell);
                col++; if (col > 1) { col = 0; row++; }
            }
            return g; }
        DataGrid MiniDG(params (string header, string path, double w, bool right)[] cols) {
            var colH = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            colH.Setters.Add(new Setter(Control.BackgroundProperty, DBr("#1A4F6E")));
            colH.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            colH.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            colH.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
            colH.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
            var dg = new DataGrid {
                AutoGenerateColumns=false, IsReadOnly=true, RowHeight=28, FontSize=11,
                BorderThickness=new Thickness(0), Background=System.Windows.Media.Brushes.White,
                AlternatingRowBackground=DBr("#F4F8FB"),
                GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush=DBr("#E0E8EE"),
                ColumnHeaderStyle=colH, MaxHeight=180,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
            foreach (var (h, path, w, right) in cols) {
                var c = new DataGridTextColumn {
                    Header=h, SortMemberPath=path,
                    Width = w>0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                                : new DataGridLength(1, DataGridLengthUnitType.Star),
                    Binding = new System.Windows.Data.Binding(path) };
                if (right) { var cs=new Style(typeof(DataGridCell)); cs.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)); c.CellStyle=cs; }
                dg.Columns.Add(c); }
            return dg; }

        // ── HEADER del modal ─────────────────────────────────────────────
        var hdr = new Border { Background = DBr("#0E2F44"), Padding = new Thickness(18,14,18,14) };
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = r.CLI_NOMBRE,
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        var hdrSub = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,0) };
        hdrSub.Children.Add(new TextBlock { Text = r.LOCAL_NOMBRE,
            Foreground = DBr("#7FB3D3"), FontSize = 11 });
        if (!string.IsNullOrEmpty(extra?.Ci))
            hdrSub.Children.Add(new TextBlock { Text = $"  ·  CI: {extra.Ci}",
                Foreground = DBr("#7FB3D3"), FontSize = 11 });
        if (!string.IsNullOrEmpty(extra?.Telefono))
            hdrSub.Children.Add(new TextBlock { Text = $"  ·  ☎ {extra.Telefono}",
                Foreground = DBr("#4FC3F7"), FontSize = 11 });
        hdrSp.Children.Add(hdrSub);
        hdrG.Children.Add(hdrSp);

        // Badge estado cuota
        var badge = new Border {
            Background = DBr(esVencida ? "#C62828" : "#1B5E20"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,6,12,6),
            VerticalAlignment = VerticalAlignment.Center };
        badge.Child = new TextBlock {
            Text = esVencida ? $"⚠ VENCIDA\n{r.VTO_STR}" : $"✓ VIGENTE\n{r.VTO_STR}",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 11,
            TextAlignment = TextAlignment.Center };
        Grid.SetColumn(badge, 1); hdrG.Children.Add(badge);
        hdr.Child = hdrG;

        // ── CHIPS financieros ────────────────────────────────────────────
        Border FinChip(string val, string lbl, string bg, string fg) {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = val, FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = DBr(fg), TextAlignment = TextAlignment.Center });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8.5,
                Foreground = DBr(fg), TextAlignment = TextAlignment.Center, Opacity = 0.8 });
            return new Border { Background = DBr(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14,8,14,8), Margin = new Thickness(4,0,4,0), Child = sp }; }

        var chips = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0,0,0,10) };
        if (extra != null) {
            chips.Children.Add(FinChip($"Gs. {extra.Total:N0}",   "TOTAL VENTA",   "#E3F2FD", "#0D47A1"));
            chips.Children.Add(FinChip($"Gs. {extra.Haber:N0}",   "COBRADO",       "#E8F5E9", "#1B5E20"));
            chips.Children.Add(FinChip($"Gs. {extra.Debe:N0}",    "SALDO DEBE",    accentBg,  accentColor));
            chips.Children.Add(FinChip($"Gs. {extra.MontoCuota:N0}", "CUOTA MENS.", "#EEF2F6", "#37474F"));
            chips.Children.Add(FinChip($"{extra.CuotasPend}/{extra.CuotasTotal}", "CUOTAS PEND.", accentBg, accentColor));
            if (extra.CuotasVencidas > 0)
                chips.Children.Add(FinChip($"{extra.CuotasVencidas}", "C. VENCIDAS", "#FDECEA", "#C62828"));
        }

        // ── TABS ─────────────────────────────────────────────────────────
        var tabs = new TabControl { Background = PBr("#F4F8FB"), BorderThickness = new Thickness(0) };

        // Tab: Resumen
        var tabResumen = new StackPanel { Margin = new Thickness(0,4,0,0) };
        tabResumen.Children.Add(chips);

        // Card: datos del crédito
        var creditoGrid = TwoCol(
            ("VENTA Nº",       string.IsNullOrEmpty(r.NVENTACHAR) ? "—" : r.NVENTACHAR, true, null),
            ("FECHA DE VENTA", extra?.FechaVenta ?? r.FECHA_STR, false, null),
            ("LOCAL",          r.LOCAL_NOMBRE, false, null),
            ("VENDEDOR",       extra?.Vendedor ?? "—", false, null),
            ("CUOTA Nº",       r.NCUOTA.ToString(), true, accentColor),
            ("VENCIMIENTO",    r.VTO_STR, true, esVencida ? accentColor : null) );
        tabResumen.Children.Add(Card("DATOS DEL CRÉDITO", creditoGrid));

        // Card: cliente
        var cliGrid = TwoCol(
            ("CI / RUC",    extra?.Ci        ?? "—", false, null),
            ("TELÉFONO",    extra?.Telefono  ?? "—", false, null),
            ("CIUDAD",      extra?.Ciudad    ?? "—", false, null),
            ("DIRECCIÓN",   extra?.Direccion ?? "—", false, null),
            ("EMPRESA",     extra?.Empresa   ?? "—", false, null),
            ("CRÉDITO MÁX.",extra != null ? $"Gs. {extra.CredMax:N0}" : "—", false, null) );
        tabResumen.Children.Add(Card("DATOS DEL CLIENTE", cliGrid));

        // Card: garante (si tiene)
        if (extra != null && !string.IsNullOrEmpty(extra.Garante)) {
            var garGrid = TwoCol(
                ("GARANTE", extra.Garante, true, null),
                ("CI GARANTE", extra.GaranteCI, false, null),
                ("TEL GARANTE", extra.GaranteTel, false, null),
                ("", "", false, null) );
            tabResumen.Children.Add(Card("GARANTE", garGrid)); }

        tabs.Items.Add(new TabItem { Header = "Resumen",
            Content = new ScrollViewer { Content = tabResumen,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(14,10,14,10) } });

        // Tab: Cuotas
        var cuotasRowStyle = new Style(typeof(DataGridRow));
        var pendTrig = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoStr"), Value = "Pendiente" };
        pendTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, DBr("#C62828")));
        pendTrig.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        var vencTrig = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EsVencida"), Value = true };
        vencTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, DBr("#FDECEA")));
        cuotasRowStyle.Triggers.Add(pendTrig);
        cuotasRowStyle.Triggers.Add(vencTrig);
        var dgCuotas = MiniDG(
            ("Nº",        "NCUOTA",      38, true),
            ("Venc.",     "VtoStr",      88, false),
            ("Monto Gs.", "MONTO",        0, true),
            ("Estado",    "EstadoStr",   80, false),
            ("Cobrado",   "FechaCobrado",90, false));
        dgCuotas.RowStyle = cuotasRowStyle;
        dgCuotas.MaxHeight = 300;
        dgCuotas.ItemsSource = cuotas;
        // Formatear Monto
        (dgCuotas.Columns[2] as DataGridTextColumn)!.Binding =
            new System.Windows.Data.Binding("MONTO") { StringFormat = "N0" };
        tabs.Items.Add(new TabItem { Header = $"Cuotas ({cuotas.Count})",
            Content = new Border { Child = dgCuotas, Margin = new Thickness(10) } });

        // Tab: Artículos
        var dgArts = MiniDG(
            ("Artículo",    "Nombre",    0,   false),
            ("Cant.",       "CANTIDAD",  52,  true),
            ("P. Venta Gs.","PV",        110, true),
            ("Subtotal Gs.","Subtotal",  115, true));
        (dgArts.Columns[2] as DataGridTextColumn)!.Binding =
            new System.Windows.Data.Binding("PV") { StringFormat = "N0" };
        (dgArts.Columns[3] as DataGridTextColumn)!.Binding =
            new System.Windows.Data.Binding("Subtotal") { StringFormat = "N0" };
        dgArts.ItemsSource = arts;
        tabs.Items.Add(new TabItem { Header = $"Artículos ({arts.Count})",
            Content = new Border { Child = dgArts, Margin = new Thickness(10) } });

        // Tab: Cobros realizados
        if (cobros.Count > 0) {
            var dgCobros = MiniDG(
                ("Fecha",       "FechaStr",  90,  false),
                ("Monto Gs.",   "MONTO",     0,   true),
                ("Usuario",     "Usuario",   100, false),
                ("Observación", "Obs",       120, false));
            (dgCobros.Columns[1] as DataGridTextColumn)!.Binding =
                new System.Windows.Data.Binding("MONTO") { StringFormat = "N0" };
            dgCobros.ItemsSource = cobros;
            tabs.Items.Add(new TabItem { Header = $"Cobros ({cobros.Count})",
                Content = new Border { Child = dgCobros, Margin = new Thickness(10) } }); }

        // ── FOOTER ───────────────────────────────────────────────────────
        var btnCerrar = new Button {
            Content = "✓  Cerrar", Height = 36, Padding = new Thickness(28,0,28,0),
            Background = DBr("#1A4F6E"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, __) => dlg.Close();
        var footer = new Border {
            Padding = new Thickness(16,10,16,14), Background = DBr("#EEF2F6"),
            BorderBrush = DBr("#D6E5EF"), BorderThickness = new Thickness(0,1,0,0),
            Child = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { btnCerrar } } };

        // ── LAYOUT ───────────────────────────────────────────────────────
        var rootSp = new DockPanel();
        DockPanel.SetDock(hdr, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        rootSp.Children.Add(hdr);
        rootSp.Children.Add(footer);
        rootSp.Children.Add(tabs);

        dlg.Content = rootSp;
        dlg.KeyDown += (_, e) => { if (e.Key == Key.Escape) dlg.Close(); };
        dlg.ShowDialog();
    }

    private async Task MostrarFiltroYCargar()
    {
        var dlg = new PendientesFiltroDialog(_db) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _fechaDesde  = dlg.FechaDesde;
        _fechaHasta  = dlg.FechaHasta;
        _idLocal     = dlg.IdLocal;
        _localNombre = dlg.LocalNombre;
        await Cargar();
    }

    private async Task Cargar()
    {
        try
        {
            var desdeStr = _fechaDesde.HasValue ? _fechaDesde.Value.ToString("dd/MM/yyyy") : "inicio";
            var hastaStr = _fechaHasta.HasValue ? _fechaHasta.Value.ToString("dd/MM/yyyy") : "hoy";
            _lblFiltroSub.Text = _idLocal.HasValue
                ? $"Local: {_localNombre}  ·  Vto. {desdeStr} → {hastaStr}"
                : $"Todos los locales  ·  Vto. {desdeStr} → {hastaStr}";

            // Cargar locales en combo inline si no están aún
            if (_cboLocal.Items.Count == 0)
            {
                using var connL = _db.Create();
                var locales = (await connL.QueryAsync<(int Id, string Nombre)>(
                    "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY NOMBRE")).ToList();
                _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
                foreach (var (id, nombre) in locales)
                    _cboLocal.Items.Add(new ComboBoxItem { Content = nombre, Tag = id });
                _cboLocal.SelectedIndex = 0;
            }

            using var conn = _db.Create();
            var sql = @"
SELECT CS.IDCAB, ISNULL(CS.NVENTACHAR,'') AS NVENTACHAR,
    L.NOMBRE AS LOCAL_NOMBRE, CLI.NOMBRE_CLIENTE AS CLI_NOMBRE,
    CASE WHEN CS.ID_GARANTE>1 THEN ISNULL(GAR.NOMBRE_CLIENTE,'') ELSE '' END AS GAR_NOMBRE,
    CS.TOTAL, CS.DEBE, CS.HABER,
    CONVERT(VARCHAR(10),CS.FECHA,103) AS FECHA_STR,
    G.NCUOTA, CS.MONTO_CUOTA,
    CONVERT(VARCHAR(10),G.VTO,103) AS VTO_STR,
    G.VTO AS VTO_DATE,
    L.ID_LOCAL
FROM CABECERA_SALES CS
INNER JOIN GENERADAS G   ON G.IDCAB=CS.IDCAB AND G.ESTADO=0
INNER JOIN CLIENTES CLI  ON CLI.ID_CLIENTE=CS.ID_CLIENTE
LEFT  JOIN CLIENTES GAR  ON GAR.ID_CLIENTE=CS.ID_GARANTE
INNER JOIN LOCALES  L    ON L.ID_LOCAL=CS.ID_LOCAL
WHERE CS.ESTADO=1 AND CS.DEBE>0";
            if (_fechaDesde.HasValue) sql += " AND G.VTO >= @desde";
            if (_fechaHasta.HasValue) sql += " AND G.VTO <  @hasta";
            if (_idLocal.HasValue)    sql += " AND CS.ID_LOCAL = @idLocal";
            sql += " ORDER BY L.NOMBRE, CLI.NOMBRE_CLIENTE, G.VTO, G.NCUOTA";

            _todos = (await conn.QueryAsync<FilaPendiente>(sql, new {
                desde   = _fechaDesde,
                hasta   = _fechaHasta.HasValue ? _fechaHasta.Value.AddDays(1) : (DateTime?)null,
                idLocal = _idLocal
            })).ToList();

            AplicarFiltroInline();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AplicarFiltroInline()
    {
        var busq   = _txtBuscar.Text.Trim().ToUpperInvariant();
        var idLoc  = (_cboLocal.SelectedItem as ComboBoxItem)?.Tag is int l && l > 0 ? (int?)l : null;

        var lista = _todos.AsEnumerable();
        if (idLoc.HasValue) lista = lista.Where(f => f.ID_LOCAL == idLoc);
        if (!string.IsNullOrEmpty(busq))
            lista = lista.Where(f =>
                f.CLI_NOMBRE.ToUpperInvariant().Contains(busq) ||
                f.NVENTACHAR.ToUpperInvariant().Contains(busq));

        var result = lista.ToList();
        _grid.ItemsSource = result;

        var totalClientes = result.Select(f => f.IDCAB).Distinct().Count();
        var totalDebe     = result.GroupBy(f => f.IDCAB).Sum(g => g.First().DEBE);
        var totalCuotas   = result.Count;

        _kpiClientes.Text = totalClientes.ToString("N0");
        _kpiTotal.Text    = $"Gs. {totalDebe:N0}";
        _kpiCuotas.Text   = totalCuotas.ToString("N0");

        _footClientesRef.Text = $"Clientes: {totalClientes}";
        _footTotalRef.Text    = $"Total a cobrar: Gs. {totalDebe:N0}";
        _footCuotasRef.Text   = $"Cuotas: {totalCuotas}";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5) _ = Cargar();
    }

    private async void ImprimirReportePendientes()
    {
        if (_todos.Count == 0)
        { MessageBox.Show("No hay datos para imprimir.", "Imprimir", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var (impresora, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");
        var doc = new System.Drawing.Printing.PrintDocument { DocumentName = "Cobros Pendientes" };
        doc.DefaultPageSettings.Landscape = false;

        // Estado de paginación: grupo actual, fila actual dentro del grupo
        var grupos = _todos.GroupBy(f => f.LOCAL_NOMBRE).OrderBy(g => g.Key)
                           .Select(g => (Local: g.Key, Filas: g.ToList())).ToList();
        int _gi = 0, _fi = 0;
        bool _firstPage = true;
        var _tituloP   = _lblFiltroSub.Text;
        var _fechaImpP = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        // Columnas portrait A4 (787px útil): Local(120)|Cliente(220)|NVenta(90)|Cuota(60)|Monto(100) = 590 + márgenes
        int[] cws         = { 120, 220, 90, 60, 100 };
        bool[] rightAlignP= { false, false, false, true, true };
        string[] colHdrs  = { "Local", "Cliente", "N.Venta", "Cuota", "Monto Gs." };

        string logoPPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logotipocredimar2.png");
        var logoImgP = System.IO.File.Exists(logoPPath) ? System.Drawing.Image.FromFile(logoPPath) : null;
        doc.EndPrint += (_, _) => logoImgP?.Dispose();

        doc.PrintPage += (_, e) =>
        {
            var gr  = e.Graphics!;
            int pgW = 827, pgH = 1169, lx = 20, pw = pgW - 40;
            gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            gr.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;

            using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
            using var fntRow  = new System.Drawing.Font("Arial", 7f);
            using var fntGrp  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
            using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
            var azul = System.Drawing.Color.FromArgb(13, 71, 161);
            var verde = System.Drawing.Color.FromArgb(27, 94, 32);
            var rojo  = System.Drawing.Color.FromArgb(210, 0, 0);

            // Marca de agua
            if (logoImgP != null)
            {
                float wmW = 500f, wmH = logoImgP.Height*(wmW/logoImgP.Width);
                var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.055f };
                var ia = new System.Drawing.Imaging.ImageAttributes(); ia.SetColorMatrix(cm);
                gr.DrawImage(logoImgP, new System.Drawing.Rectangle((int)(pgW/2f-wmW/2f),(int)(pgH/2f-wmH/2f+40f),(int)wmW,(int)wmH),
                    0,0,logoImgP.Width,logoImgP.Height,System.Drawing.GraphicsUnit.Pixel,ia); ia.Dispose();
            }

            // Encabezado de página
            int y = 4;
            gr.FillRectangle(System.Drawing.Brushes.White, 0, 0, pgW, 90);
            using var penRojo = new System.Drawing.Pen(rojo, 3f);
            gr.DrawLine(penRojo, 0, 2, pgW, 2);
            if (logoImgP != null)
            {
                float lh = 60f, lw2 = logoImgP.Width*(lh/logoImgP.Height);
                gr.DrawImage(logoImgP, lx, 8, (int)lw2, (int)lh);
                float sx = lx+lw2+10f;
                using var pgris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190,190,190),1f);
                gr.DrawLine(pgris, sx, 6, sx, 76);
                float tx = sx+10f, tpw2 = pw-(sx-lx)-10f;
                using var fntTit = new System.Drawing.Font("Arial",12,System.Drawing.FontStyle.Bold);
                using var bAz = new System.Drawing.SolidBrush(azul);
                gr.FillRectangle(bAz, tx, 8, tpw2, 32);
                { var sz = gr.MeasureString("COBROS PENDIENTES", fntTit); gr.DrawString("COBROS PENDIENTES", fntTit, System.Drawing.Brushes.White, tx+(tpw2-sz.Width)/2f, 8+(32-sz.Height)/2f); }
                using var fntSub = new System.Drawing.Font("Arial",7f);
                using var bGris = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(80,80,80));
                { var sz = gr.MeasureString(_tituloP, fntSub); gr.DrawString(_tituloP, fntSub, bGris, tx+(tpw2-sz.Width)/2f, 46f); }
                { var s2 = $"Impreso: {_fechaImpP}  ·  {CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario??"—"}";
                  var sz = gr.MeasureString(s2, fntSub); gr.DrawString(s2, fntSub, bGris, tx+(tpw2-sz.Width)/2f, 60f); }
            }
            gr.DrawLine(penRojo, 0, 82, pgW, 82);
            y = 90;

            // Cabecera columnas (solo primera página o tras salto)
            if (_firstPage)
            {
                using var bHdr = new System.Drawing.SolidBrush(azul);
                gr.FillRectangle(bHdr, lx, y, pw, 20);
                int cx = lx;
                for (int i = 0; i < colHdrs.Length; i++)
                {
                    if (rightAlignP[i]) { var sz = gr.MeasureString(colHdrs[i],fntHdr); gr.DrawString(colHdrs[i],fntHdr,System.Drawing.Brushes.White,cx+cws[i]-sz.Width-2f,y+2f); }
                    else gr.DrawString(colHdrs[i],fntHdr,System.Drawing.Brushes.White,cx+2f,y+2f);
                    cx += cws[i];
                }
                y += 22;
                _firstPage = false;
            }

            // Función para dibujar celda
            void Cell(string txt, System.Drawing.Font f2, int colIdx, int ry, int rh, System.Drawing.Brush br2, bool right2)
            {
                int cx2 = lx + cws.Take(colIdx).Sum();
                float fh2 = f2.GetHeight(gr);
                float ty = ry + Math.Max(0f,(rh-fh2)/2f);
                if (right2) { var sz = gr.MeasureString(txt,f2); gr.DrawString(txt,f2,br2,cx2+cws[colIdx]-sz.Width-2f,ty); }
                else        gr.DrawString(txt,f2,br2,cx2+2f,ty);
            }

            int rowH = 20; int grpH = 18;
            var totalDebe2 = _todos.GroupBy(f=>f.IDCAB).Sum(gp=>gp.First().DEBE);

            while (_gi < grupos.Count)
            {
                var (local, localFilas) = grupos[_gi];

                // Encabezado de grupo
                if (_fi == 0)
                {
                    if (y > pgH - 60) { e.HasMorePages = true; _firstPage = true; return; }
                    using var bGrpBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200,230,201));
                    gr.FillRectangle(bGrpBg, lx, y, pw, grpH);
                    using var bGrpFg = new System.Drawing.SolidBrush(verde);
                    gr.DrawString($"  {local}", fntGrp, bGrpFg, lx+2f, y+2f);
                    y += grpH + 2;
                }

                // Filas del grupo
                while (_fi < localFilas.Count)
                {
                    if (y > pgH - 55) { e.HasMorePages = true; _firstPage = true; return; }
                    var f2 = localFilas[_fi++];
                    if (_fi % 2 == 0)
                        using (var bAlt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(245,247,252)))
                            gr.FillRectangle(bAlt, lx, y, pw, rowH);
                    string[] vals = { f2.LOCAL_NOMBRE, f2.CLI_NOMBRE, f2.NVENTACHAR, f2.NCUOTA.ToString(), f2.MONTO_CUOTA.ToString("N0") };
                    for (int i = 0; i < vals.Length; i++)
                        Cell(vals[i], fntRow, i, y, rowH, System.Drawing.Brushes.Black, rightAlignP[i]);
                    y += rowH;
                    using var pSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210,210,220),0.5f);
                    gr.DrawLine(pSep, lx, y, lx+pw, y);
                }

                // Subtotal del local
                if (y > pgH - 55) { e.HasMorePages = true; _firstPage = true; return; }
                var subDebe = localFilas.GroupBy(f=>f.IDCAB).Sum(gp=>gp.First().DEBE);
                using (var bSub = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232,245,233)))
                    gr.FillRectangle(bSub, lx, y, pw, grpH);
                using var bSubFg = new System.Drawing.SolidBrush(verde);
                gr.DrawString($"  — Resumen {local}: Gs. {subDebe:N0}", fntGrp, bSubFg, lx+2f, y+2f);
                y += grpH + 4;

                _gi++; _fi = 0;
            }

            // Total general al final
            y += 4;
            using (var p2 = new System.Drawing.Pen(azul,1.5f)) gr.DrawLine(p2, lx, y, lx+pw, y);
            y += 4;
            using var bTotBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(13,71,161));
            gr.FillRectangle(bTotBg, lx, y, pw, 22);
            gr.DrawString("  TOTAL GENERAL A COBRAR:", fntFoot, System.Drawing.Brushes.White, lx+2f, y+3f);
            { var s3 = $"Gs. {totalDebe2:N0}"; var sz = gr.MeasureString(s3,fntFoot); gr.DrawString(s3,fntFoot,System.Drawing.Brushes.White,lx+pw-sz.Width-4f,y+3f); }
        };

        CrediSoft.UI.Views.Shared.TicketPrinter.ImprimirConConfig(doc, impresora);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  CRÉDITOS ACTIVOS — búsqueda, filtros y detalle completo
// ══════════════════════════════════════════════════════════════════════════════
public class ActivosWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Filtros sidebar
    private TextBox   _txtBuscar    = null!;
    private ComboBox  _cboLocal     = null!;
    private ComboBox  _cboCuotas    = null!;

    // KPIs header
    private TextBlock _kpiClientes  = null!;
    private TextBlock _kpiTotal     = null!;
    private TextBlock _kpiCuotas    = null!;

    // Grilla
    private DataGrid  _grid         = null!;

    // Panel detalle con pestañas
    private Border    _detPanel       = null!;
    private TabControl _detTabs       = null!;
    // Pestaña Info
    private TextBlock _detNombre      = null!;
    private TextBlock _detCI          = null!;
    private TextBlock _detTel         = null!;
    private TextBlock _detLocal       = null!;
    private TextBlock _detNVenta      = null!;
    private TextBlock _detFecha       = null!;
    private TextBlock _detTotal       = null!;
    private TextBlock _detDebe        = null!;
    private TextBlock _detCuota       = null!;
    private TextBlock _detCuotasPend  = null!;
    // Pestaña Artículos
    private DataGrid  _detGridArts    = null!;
    // Pestaña Cuotas
    private DataGrid  _detGridCuotas  = null!;
    // Pestaña Historial
    private DataGrid  _detGridHist    = null!;

    private List<FilaActivo> _todos = new();

    private static System.Windows.Media.SolidColorBrush ABr(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public ActivosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Créditos Activos";
        // Ancho ampliado (antes 1020) — el panel de filtros (220px fijo) + las columnas de la
        // grilla (CI/RUC, Teléfono, N° Venta, Fecha, Total, Debe, Cuota, Cuot. Pend., todas
        // con ancho fijo) sumaban más de 1100px solo en columnas fijas, sin contar Local/
        // Cliente — quedaba apretado incluso en pantallas de 1360px.
        var anchoDisponibleActivos = System.Windows.SystemParameters.WorkArea.Width - 20;
        Width = Math.Min(1220, anchoDisponibleActivos); Height = 650;
        MinWidth = 980; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ABr("#EEF2F6");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await CargarDatos();
    }

    private void BuildUI()
    {
        // Layout: header | body(sidebar + main)
        var root = new DockPanel();

        // ── HEADER ──────────────────────────────────────────────────────────
        var hdr = new Border { Background = ABr("#0E2F44"), Padding = new Thickness(18,12,18,12) };
        DockPanel.SetDock(hdr, Dock.Top);
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Título
        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,32,0) };
        hdrTxt.Children.Add(new TextBlock { Text = "CRÉDITOS ACTIVOS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrTxt.Children.Add(new TextBlock { Text = "Créditos vigentes con saldo pendiente",
            Foreground = ABr("#7FB3D3"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        Grid.SetColumn(hdrTxt, 0); hdrG.Children.Add(hdrTxt);

        // KPIs
        Border KpiCard(string label, out TextBlock valTb)
        {
            valTb = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
            var lblTb = new TextBlock { Text = label, FontSize = 9.5, Foreground = ABr("#7FB3D3"),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,0) };
            var sp = new StackPanel { Margin = new Thickness(0,2,0,2) };
            sp.Children.Add(valTb); sp.Children.Add(lblTb);
            return new Border { Background = ABr("#1A4F6E"), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16,8,16,8), Margin = new Thickness(6,0,6,0), Child = sp };
        }
        var kpiRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        kpiRow.Children.Add(KpiCard("CLIENTES",      out _kpiClientes));
        kpiRow.Children.Add(KpiCard("TOTAL A COBRAR",out _kpiTotal));
        kpiRow.Children.Add(KpiCard("CUOTAS PEND.",  out _kpiCuotas));
        Grid.SetColumn(kpiRow, 1); hdrG.Children.Add(kpiRow);

        // Botón cerrar
        var btnCerrarHdr = new Button { Content = "✕  Cerrar", Height = 32, Padding = new Thickness(14,0,14,0),
            Background = ABr("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        btnCerrarHdr.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrarHdr, 2); hdrG.Children.Add(btnCerrarHdr);
        hdr.Child = hdrG; root.Children.Add(hdr);

        // ── SIDEBAR (filtros) ────────────────────────────────────────────────
        var sidebar = new Border { Width = 220, Background = ABr("#1A4F6E"),
            BorderBrush = ABr("#0E2F44"), BorderThickness = new Thickness(0,0,2,0) };
        DockPanel.SetDock(sidebar, Dock.Left);

        TextBlock SLbl(string t) => new() { Text = t, Foreground = ABr("#7FB3D3"),
            FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,12,0,4) };

        var sideStack = new StackPanel { Margin = new Thickness(14,16,14,16) };
        sideStack.Children.Add(new TextBlock { Text = "FILTROS", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0,0,0,14) });

        // Búsqueda
        sideStack.Children.Add(SLbl("BUSCAR"));
        _txtBuscar = new TextBox { Height = 34, FontSize = 12.5,
            Padding = new Thickness(8,0,8,0), VerticalContentAlignment = VerticalAlignment.Center,
            Background = ABr("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            CaretBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), BorderBrush = ABr("#2A6F9E") };
        _txtBuscar.TextChanged += (_, _) => AplicarFiltros();
        sideStack.Children.Add(_txtBuscar);
        sideStack.Children.Add(new TextBlock { Text = "Nombre, CI o Nº Venta",
            Foreground = ABr("#5A8AAA"), FontSize = 9.5, Margin = new Thickness(0,2,0,0) });

        // Local
        sideStack.Children.Add(SLbl("LOCAL"));
        _cboLocal = new ComboBox { Height = 32, FontSize = 12 };
        _cboLocal.SelectionChanged += (_, _) => AplicarFiltros();
        sideStack.Children.Add(_cboLocal);

        // Cuotas
        sideStack.Children.Add(SLbl("ESTADO DE CUOTAS"));
        _cboCuotas = new ComboBox { Height = 32, FontSize = 12 };
        _cboCuotas.Items.Add(new ComboBoxItem { Content = "Todas",               Tag = "todas"    });
        _cboCuotas.Items.Add(new ComboBoxItem { Content = "Con cuotas pend.",     Tag = "conpend"  });
        _cboCuotas.Items.Add(new ComboBoxItem { Content = "Sin cuotas (residual)",Tag = "sinpend"  });
        _cboCuotas.SelectedIndex = 0;
        _cboCuotas.SelectionChanged += (_, _) => AplicarFiltros();
        sideStack.Children.Add(_cboCuotas);
        sideStack.Children.Add(new TextBlock {
            Text = "⚠ 'Sin cuotas' = saldo residual\ndel sistema anterior",
            Foreground = ABr("#F59E0B"), FontSize = 9, Margin = new Thickness(0,3,0,0),
            TextWrapping = TextWrapping.Wrap });

        // Botón limpiar
        var btnLimpiar = new Button { Content = "Limpiar filtros", Height = 30, Margin = new Thickness(0,18,0,0),
            Background = ABr("#0E2F44"), Foreground = ABr("#7FB3D3"),
            FontSize = 11, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnLimpiar.Click += (_, _) => {
            _txtBuscar.Text = "";
            if (_cboLocal.Items.Count > 0) _cboLocal.SelectedIndex = 0;
            _cboCuotas.SelectedIndex = 0;
        };
        sideStack.Children.Add(btnLimpiar);

        sidebar.Child = sideStack;
        root.Children.Add(sidebar);

        // ── PANEL DETALLE (derecha) con pestañas ────────────────────────────
        _detPanel = new Border { Width = 390, Background = ABr("#F8FAFC"),
            BorderBrush = ABr("#C5D5E8"), BorderThickness = new Thickness(1,0,0,0),
            Visibility = Visibility.Collapsed };
        DockPanel.SetDock(_detPanel, Dock.Right);

        TextBlock DLbl(string t) => new() { Text = t, FontSize = 9, FontWeight = FontWeights.SemiBold,
            Foreground = ABr("#5A8AAA"), Margin = new Thickness(0,8,0,1) };
        TextBlock DVal(out TextBlock tb) { tb = new TextBlock { FontSize = 12.5, Foreground = ABr("#0D1F2D"),
            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }; return tb; }

        var detRoot = new DockPanel();

        // Header del panel — nombre del cliente
        var detHdr = new Border { Background = ABr("#0E2F44"), Padding = new Thickness(14,12,14,12) };
        DockPanel.SetDock(detHdr, Dock.Top);
        var detHdrSp = new StackPanel();
        _detNombre = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White, TextWrapping = TextWrapping.Wrap };
        _detCI  = new TextBlock { FontSize = 11, Foreground = ABr("#7FB3D3"), Margin = new Thickness(0,2,0,0) };
        _detTel = new TextBlock { FontSize = 11, Foreground = ABr("#7FB3D3") };
        detHdrSp.Children.Add(_detNombre);
        detHdrSp.Children.Add(_detCI);
        detHdrSp.Children.Add(_detTel);
        detHdr.Child = detHdrSp;
        detRoot.Children.Add(detHdr);

        // ── Tab: Info ──
        var infoStack = new StackPanel { Margin = new Thickness(16,10,16,16) };

        Border InfoCard(string label, string? badge = null)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = ABr("#5A8AAA"),
                FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            if (badge != null)
            {
                var b = new Border { Background = ABr("#FEF3C7"), CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6,2,6,2) };
                b.Child = new TextBlock { Text = badge, FontSize = 9, Foreground = ABr("#92400E"),
                    FontWeight = FontWeights.Bold };
                Grid.SetColumn(b, 1); g.Children.Add(b);
            }
            return new Border { BorderBrush = ABr("#E0E8EE"), BorderThickness = new Thickness(0,0,0,1),
                Padding = new Thickness(0,8,0,6), Child = g };
        }

        infoStack.Children.Add(InfoCard("DATOS DEL CRÉDITO"));
        infoStack.Children.Add(DLbl("LOCAL"));         infoStack.Children.Add(DVal(out _detLocal));
        infoStack.Children.Add(DLbl("Nº VENTA"));      infoStack.Children.Add(DVal(out _detNVenta));
        infoStack.Children.Add(DLbl("FECHA VENTA"));   infoStack.Children.Add(DVal(out _detFecha));
        infoStack.Children.Add(new Border { Height = 1, Background = ABr("#E0E8EE"), Margin = new Thickness(0,12,0,8) });
        infoStack.Children.Add(InfoCard("MONTOS"));
        infoStack.Children.Add(DLbl("TOTAL VENTA"));   infoStack.Children.Add(DVal(out _detTotal));
        infoStack.Children.Add(DLbl("SALDO DEBE"));    infoStack.Children.Add(DVal(out _detDebe));
        infoStack.Children.Add(DLbl("CUOTA MENSUAL")); infoStack.Children.Add(DVal(out _detCuota));
        infoStack.Children.Add(DLbl("CUOTAS PEND."));  infoStack.Children.Add(DVal(out _detCuotasPend));

        var tabInfo = new TabItem { Header = "Info",
            Content = new ScrollViewer { Content = infoStack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = ABr("#F8FAFC") } };

        // ── Tab: Artículos ──
        _detGridArts = MiniGrid(
            ("Artículo",   "NOMBRE",    0,   null,  false),
            ("Cant.",      "CANTIDAD",  48,  "N0",  true),
            ("Precio Gs.", "PV",        95,  "N0",  true));
        var tabArts = new TabItem { Header = "Artículos", Content = _detGridArts };

        // ── Tab: Cuotas ──
        _detGridCuotas = MiniGrid(
            ("Nº",         "NCUOTA",     28,  null,  true),
            ("Monto Gs.",  "MONTO",      82,  "N0",  true),
            ("Vto.",       "VTO_STR",    78,  null,  false),
            ("Cobrado",    "COBRADO_STR",78,  null,  false),
            ("Estado",     "ESTADO_STR", 66,  null,  false));
        // Row style para colorear pendientes vs cobradas
        var cuotaRowStyle = new Style(typeof(DataGridRow));
        var pendTrigger = new DataTrigger {
            Binding = new System.Windows.Data.Binding("ESTADO_STR"),
            Value = "Pendiente" };
        pendTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ABr("#DC2626")));
        pendTrigger.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        cuotaRowStyle.Triggers.Add(pendTrigger);
        _detGridCuotas.RowStyle = cuotaRowStyle;
        var tabCuotas = new TabItem { Header = "Cuotas", Content = _detGridCuotas };

        // ── Tab: Historial ──
        _detGridHist = MiniGrid(
            ("Nº Venta",   "NVENTACHAR",  0,   null,  false),
            ("Fecha",      "FECHA_STR",   82,  null,  false),
            ("Debe Gs.",   "DEBE",        90,  "N0",  true),
            ("Estado",     "ESTADO_STR",  62,  null,  false));
        var histRowStyle = new Style(typeof(DataGridRow));
        var activoTrigger = new DataTrigger {
            Binding = new System.Windows.Data.Binding("ESTADO_STR"),
            Value = "Activo" };
        activoTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ABr("#059669")));
        activoTrigger.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        histRowStyle.Triggers.Add(activoTrigger);
        _detGridHist.RowStyle = histRowStyle;
        var tabHist = new TabItem { Header = "Historial", Content = _detGridHist };

        _detTabs = new TabControl { Background = ABr("#F8FAFC"), BorderThickness = new Thickness(0) };
        _detTabs.Items.Add(tabInfo);
        _detTabs.Items.Add(tabArts);
        _detTabs.Items.Add(tabCuotas);
        _detTabs.Items.Add(tabHist);
        _detTabs.SelectionChanged += async (_, _) => {
            if (_grid.SelectedItem is FilaActivo f) await CargarDetallePestaña(f);
        };

        detRoot.Children.Add(_detTabs);
        _detPanel.Child = detRoot;
        root.Children.Add(_detPanel);

        // ── GRILLA PRINCIPAL ─────────────────────────────────────────────────
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, ABr("#0E2F44")));
        colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,8,10,8)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, ABr("#1A4F6E")));
        colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 36,
            FontSize = 12.5, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = ABr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = ABr("#E0E8EE"),
            ColumnHeaderStyle = colHdrStyle,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserSortColumns = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

        DataGridTextColumn GC(string h, string path, double w, string? fmt = null, bool right = false, double minW = 0)
        {
            var col = new DataGridTextColumn {
                Header = h, SortMemberPath = path,
                Width = w > 0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                               : new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = minW,
                Binding = fmt != null
                    ? new System.Windows.Data.Binding(path) { StringFormat = fmt }
                    : new System.Windows.Data.Binding(path) };
            if (right)
            {
                var s = new Style(typeof(DataGridCell));
                s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.CellStyle = s;
            }
            return col;
        }
        // MinWidth en Local/Cliente (Star) — sin esto colapsaban demasiado en pantallas
        // angostas, cortando nombres largos ("7281694" apenas visible, etc.).
        _grid.Columns.Add(GC("Local",        "LOCAL_NOMBRE",  0, minW: 90));
        _grid.Columns.Add(GC("Cliente",       "CLI_NOMBRE",    0, minW: 150));
        _grid.Columns.Add(GC("CI / RUC",      "CI",          100));
        _grid.Columns.Add(GC("Teléfono",      "TELEFONO",    105));
        _grid.Columns.Add(GC("Nº Venta",      "NVENTACHAR",  95));
        _grid.Columns.Add(GC("Fecha",         "FECHA_STR",    95));
        _grid.Columns.Add(GC("Total Gs.",     "TOTAL",       105, "N0", true));
        _grid.Columns.Add(GC("Debe Gs.",      "DEBE",        105, "N0", true));
        _grid.Columns.Add(GC("Cuota Gs.",     "MONTO_CUOTA", 100, "N0", true));
        _grid.Columns.Add(GC("Cuot. Pend.",   "CUOTAS_PEND",  85, null, true));

        _grid.SelectionChanged += async (_, _) =>
        {
            if (_grid.SelectedItem is FilaActivo f) await AbrirDetalle(f);
        };

        root.Children.Add(_grid);
        Content = root;
    }

    private DataGrid MiniGrid(params (string header, string path, double w, string? fmt, bool right)[] cols)
    {
        var colHdrS = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrS.Setters.Add(new Setter(Control.BackgroundProperty, ABr("#1A4F6E")));
        colHdrS.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdrS.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        colHdrS.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        colHdrS.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6,5,6,5)));
        colHdrS.Setters.Add(new Setter(Control.BorderBrushProperty, ABr("#2A6F9E")));
        colHdrS.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));

        var dg = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 30,
            FontSize = 11.5, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = ABr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = ABr("#E0E8EE"),
            ColumnHeaderStyle = colHdrS,
            SelectionMode = DataGridSelectionMode.Single,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto };

        foreach (var (h, path, w, fmt, right) in cols)
        {
            var col = new DataGridTextColumn {
                Header = h, SortMemberPath = path,
                Width = w > 0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                               : new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = fmt != null
                    ? new System.Windows.Data.Binding(path) { StringFormat = fmt }
                    : new System.Windows.Data.Binding(path) };
            if (right)
            {
                var cs = new Style(typeof(DataGridCell));
                cs.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.CellStyle = cs;
            }
            dg.Columns.Add(col);
        }
        return dg;
    }

    private async Task AbrirDetalle(FilaActivo f)
    {
        // Header
        _detNombre.Text = f.CLI_NOMBRE;
        _detCI.Text     = string.IsNullOrEmpty(f.CI) ? "" : $"CI/RUC: {f.CI}";
        _detTel.Text    = string.IsNullOrEmpty(f.TELEFONO) ? "" : $"Tel: {f.TELEFONO}";

        // Tab Info
        _detLocal.Text      = f.LOCAL_NOMBRE;
        _detNVenta.Text     = string.IsNullOrEmpty(f.NVENTACHAR) ? "—" : f.NVENTACHAR;
        _detFecha.Text      = f.FECHA_STR;
        _detTotal.Text      = $"Gs. {f.TOTAL:N0}";
        _detDebe.Text       = $"Gs. {f.DEBE:N0}";
        _detCuota.Text      = $"Gs. {f.MONTO_CUOTA:N0}";

        // Indicar si es residual (cuotas=0 pero DEBE>0)
        if (f.CUOTAS_PEND == 0)
            _detCuotasPend.Text = "0  ⚠ Saldo residual del sistema anterior";
        else
            _detCuotasPend.Text = f.CUOTAS_PEND.ToString();

        _detPanel.Visibility = Visibility.Visible;
        await CargarDetallePestaña(f);
    }

    private async Task CargarDetallePestaña(FilaActivo f)
    {
        try
        {
            using var conn = _db.Create();
            var tab = _detTabs.SelectedIndex;

            if (tab == 1) // Artículos
            {
                var arts = await conn.QueryAsync<FilaArticuloDetalle>(@"
SELECT RTRIM(A.D) AS NOMBRE, DS.CANTIDAD, DS.PV
FROM DETALLES_SALES DS
INNER JOIN ARTICULOS A ON A.ID = DS.IDART
WHERE DS.IDCAB = @idcab
ORDER BY DS.IDDET", new { idcab = f.IDCAB });
                _detGridArts.ItemsSource = arts.ToList();
            }
            else if (tab == 2) // Cuotas
            {
                var cuotas = await conn.QueryAsync<FilaCuotaDetalle>(@"
SELECT NCUOTA, MONTO, CONVERT(VARCHAR(10),VTO,103) AS VTO_STR,
    CASE ESTADO WHEN 0 THEN 'Pendiente' ELSE 'Cobrada' END AS ESTADO_STR,
    CONVERT(VARCHAR(10),FECHACOBRADO,103) AS COBRADO_STR
FROM GENERADAS WHERE IDCAB = @idcab ORDER BY NCUOTA", new { idcab = f.IDCAB });
                _detGridCuotas.ItemsSource = cuotas.ToList();
            }
            else if (tab == 3) // Historial
            {
                var hist = await conn.QueryAsync<FilaHistorialDetalle>(@"
SELECT RTRIM(ISNULL(CS.NVENTACHAR,'')) AS NVENTACHAR,
    CONVERT(VARCHAR(10),CS.FECHA,103) AS FECHA_STR,
    CS.TOTAL, CS.DEBE,
    CASE CS.ESTADO WHEN 1 THEN 'Activo' ELSE 'Cerrado' END AS ESTADO_STR
FROM CABECERA_SALES CS
WHERE CS.ID_CLIENTE = @idcli
ORDER BY CS.IDCAB DESC", new { idcli = f.ID_CLIENTE });
                _detGridHist.ItemsSource = hist.ToList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CargarDatos()
    {
        try
        {
            using var conn = _db.Create();

            // Cargar locales en el combo
            var locales = (await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY NOMBRE")).ToList();
            _cboLocal.Items.Clear();
            _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
            foreach (var (id, nombre) in locales)
                _cboLocal.Items.Add(new ComboBoxItem { Content = nombre, Tag = id });
            _cboLocal.SelectedIndex = 0;

            // Cargar datos
            _todos = (await conn.QueryAsync<FilaActivo>(@"
SELECT CS.IDCAB, CS.ID_CLIENTE, RTRIM(ISNULL(CS.NVENTACHAR,'')) AS NVENTACHAR,
    L.ID_LOCAL, RTRIM(L.NOMBRE) AS LOCAL_NOMBRE,
    RTRIM(CLI.NOMBRE_CLIENTE) AS CLI_NOMBRE,
    RTRIM(ISNULL(CLI.CI_CLIENTE,''))       AS CI,
    RTRIM(ISNULL(CLI.TELEFONO_CLIENTE,'')) AS TELEFONO,
    CS.TOTAL, CS.DEBE, CS.HABER, CS.MONTO_CUOTA,
    CONVERT(VARCHAR(10),CS.FECHA,103) AS FECHA_STR,
    (SELECT COUNT(*) FROM GENERADAS G WITH(INDEX(IX_GENERADAS_IDCAB_ESTADO))
     WHERE G.IDCAB=CS.IDCAB AND G.ESTADO=0) AS CUOTAS_PEND
FROM CABECERA_SALES CS WITH(INDEX(IX_CABECERA_ESTADO_DEBE))
INNER JOIN CLIENTES CLI ON CLI.ID_CLIENTE=CS.ID_CLIENTE
INNER JOIN LOCALES  L   ON L.ID_LOCAL=CS.ID_LOCAL
WHERE CS.ESTADO=1 AND CS.DEBE>0
ORDER BY L.NOMBRE, CLI.NOMBRE_CLIENTE")).ToList();

            AplicarFiltros();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AplicarFiltros()
    {
        var busq   = _txtBuscar.Text.Trim().ToUpperInvariant();
        var idLoc  = (_cboLocal.SelectedItem  as ComboBoxItem)?.Tag is int l && l > 0 ? (int?)l : null;
        var cuotas = (_cboCuotas.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "todas";

        var lista = _todos.AsEnumerable();
        if (idLoc.HasValue)          lista = lista.Where(f => f.ID_LOCAL == idLoc);
        if (!string.IsNullOrEmpty(busq))
            lista = lista.Where(f =>
                f.CLI_NOMBRE.ToUpperInvariant().Contains(busq)  ||
                f.CI.ToUpperInvariant().Contains(busq)           ||
                f.NVENTACHAR.ToUpperInvariant().Contains(busq));
        if (cuotas == "conpend")     lista = lista.Where(f => f.CUOTAS_PEND > 0);
        if (cuotas == "sinpend")     lista = lista.Where(f => f.CUOTAS_PEND == 0);

        var result = lista.ToList();
        _grid.ItemsSource = result;
        _detPanel.Visibility = Visibility.Collapsed;

        _kpiClientes.Text = result.Count.ToString("N0");
        _kpiTotal.Text    = $"Gs. {result.Sum(f => f.DEBE):N0}";
        _kpiCuotas.Text   = result.Sum(f => f.CUOTAS_PEND).ToString("N0");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5) _ = CargarDatos();
    }
}

internal class FilaActivo
{
    public int     IDCAB        { get; set; }
    public int     ID_CLIENTE   { get; set; }
    public string  NVENTACHAR   { get; set; } = "";
    public int     ID_LOCAL     { get; set; }
    public string  LOCAL_NOMBRE { get; set; } = "";
    public string  CLI_NOMBRE   { get; set; } = "";
    public string  CI           { get; set; } = "";
    public string  TELEFONO     { get; set; } = "";
    public decimal TOTAL        { get; set; }
    public decimal DEBE         { get; set; }
    public decimal HABER        { get; set; }
    public decimal MONTO_CUOTA  { get; set; }
    public string  FECHA_STR    { get; set; } = "";
    public int     CUOTAS_PEND  { get; set; }
}

internal class FilaArticuloDetalle
{
    public string  NOMBRE    { get; set; } = "";
    public decimal CANTIDAD  { get; set; }
    public decimal PV        { get; set; }
}

internal class FilaCuotaDetalle
{
    public int     NCUOTA      { get; set; }
    public decimal MONTO       { get; set; }
    public string  VTO_STR     { get; set; } = "";
    public string  ESTADO_STR  { get; set; } = "";
    public string  COBRADO_STR { get; set; } = "";
}

internal class FilaHistorialDetalle
{
    public string  NVENTACHAR  { get; set; } = "";
    public string  FECHA_STR   { get; set; } = "";
    public decimal TOTAL       { get; set; }
    public decimal DEBE        { get; set; }
    public string  ESTADO_STR  { get; set; } = "";
}

internal class FilaPendiente
{
    public int      IDCAB        { get; set; }
    public string   NVENTACHAR   { get; set; } = "";
    public int      ID_LOCAL     { get; set; }
    public string   LOCAL_NOMBRE { get; set; } = "";
    public string   CLI_NOMBRE   { get; set; } = "";
    public string   GAR_NOMBRE   { get; set; } = "";
    public decimal  TOTAL        { get; set; }
    public decimal  DEBE         { get; set; }
    public decimal  HABER        { get; set; }
    public string   FECHA_STR    { get; set; } = "";
    public int      NCUOTA       { get; set; }
    public decimal  MONTO_CUOTA  { get; set; }
    public string   VTO_STR      { get; set; } = "";
    public DateTime? VTO_DATE    { get; set; }
    public bool     EsVencida    => VTO_DATE.HasValue && VTO_DATE.Value.Date < DateTime.Today;
}

internal class DetallePendienteExtra
{
    public string  Ci             { get; set; } = "";
    public string  Telefono       { get; set; } = "";
    public string  Ciudad         { get; set; } = "";
    public string  Direccion      { get; set; } = "";
    public string  Empresa        { get; set; } = "";
    public decimal CredMax        { get; set; }
    public string  Garante        { get; set; } = "";
    public string  GaranteCI      { get; set; } = "";
    public string  GaranteTel     { get; set; } = "";
    public decimal Total          { get; set; }
    public decimal Debe           { get; set; }
    public decimal Haber          { get; set; }
    public decimal MontoCuota     { get; set; }
    public int     CuotasPend     { get; set; }
    public int     CuotasTotal    { get; set; }
    public int     CuotasVencidas { get; set; }
    public string  FechaVenta     { get; set; } = "";
    public string  Vendedor       { get; set; } = "";
}

internal class FilaCuotaDetallePend
{
    public int      NCUOTA       { get; set; }
    public decimal  MONTO        { get; set; }
    public string   VtoStr       { get; set; } = "";
    public DateTime? VtoDate     { get; set; }
    public string   EstadoStr    { get; set; } = "";
    public string   FechaCobrado { get; set; } = "";
    public bool     EsVencida    => EstadoStr == "Pendiente" && VtoDate.HasValue && VtoDate.Value.Date < DateTime.Today;
}

internal class FilaArticuloPend
{
    public string  Nombre   { get; set; } = "";
    public decimal CANTIDAD { get; set; }
    public decimal PV       { get; set; }
    public decimal Subtotal { get; set; }
}

internal class FilaCobroPend
{
    public string  FechaStr { get; set; } = "";
    public decimal MONTO    { get; set; }
    public string  Usuario  { get; set; } = "";
    public string  Obs      { get; set; } = "";
}

internal class PendientesFiltroDialog : Window
{
    private readonly IDbConnectionFactory _db;

    public DateTime? FechaDesde  { get; private set; }
    public DateTime? FechaHasta  { get; private set; }
    public int?      IdLocal     { get; private set; }
    public string    LocalNombre { get; private set; } = "Todos los locales";

    private DatePicker _dpDesde  = null!;
    private DatePicker _dpHasta  = null!;
    private CheckBox   _chkTodas = null!;
    private ComboBox   _cboLocal = null!;

    private static System.Windows.Media.SolidColorBrush FB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public PendientesFiltroDialog(IDbConnectionFactory db)
    {
        _db = db;
        Title  = "Configurar filtro";
        Width  = 460; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize   = 13;
        BuildUI();
        Loaded += async (_, _) => await CargarLocales();
    }

    private void BuildUI()
    {
        var root = new StackPanel();

        // Header azul
        var hdr = new Border { Background = FB("#0E2F44"), Padding = new Thickness(16, 14, 16, 14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "⚙  CONFIGURAR FILTRO",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14 });
        hdrSp.Children.Add(new TextBlock { Text = "Cobros pendientes por vencimiento",
            Foreground = FB("#7FB3D3"), FontSize = 10.5, Margin = new Thickness(0,3,0,0) });
        hdr.Child = hdrSp;
        root.Children.Add(hdr);

        // Cuerpo del formulario
        var body = new StackPanel { Margin = new Thickness(20, 18, 20, 10) };

        TextBlock Lbl(string t) => new TextBlock { Text = t, FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = FB("#1A4F6E"),
            Margin = new Thickness(0, 0, 0, 4) };
        Border Sep() => new Border { Height = 1, Background = FB("#DDEEFF"), Margin = new Thickness(0,14,0,14) };

        // Rango de fechas
        body.Children.Add(Lbl("RANGO DE VENCIMIENTO"));
        var fechaGrid = new Grid { Margin = new Thickness(0,0,0,8) };
        fechaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fechaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fechaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _dpDesde = new DatePicker { SelectedDate = DateTime.Today.AddMonths(-3),
            VerticalAlignment = VerticalAlignment.Center };
        var sepArrow = new TextBlock { Text = "→", FontSize = 16, Foreground = FB("#1A4F6E"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,10,0) };
        _dpHasta = new DatePicker { SelectedDate = DateTime.Today,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(sepArrow, 1); Grid.SetColumn(_dpHasta, 2);
        fechaGrid.Children.Add(_dpDesde);
        fechaGrid.Children.Add(sepArrow);
        fechaGrid.Children.Add(_dpHasta);
        body.Children.Add(fechaGrid);

        _chkTodas = new CheckBox {
            Content = "Sin filtro de fecha — mostrar todas",
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = FB("#546E7A"), Margin = new Thickness(0,4,0,0) };
        _chkTodas.Checked   += (_, _) => { _dpDesde.IsEnabled = false; _dpHasta.IsEnabled = false; };
        _chkTodas.Unchecked += (_, _) => { _dpDesde.IsEnabled = true;  _dpHasta.IsEnabled = true;  };
        body.Children.Add(_chkTodas);

        body.Children.Add(Sep());

        // Local
        body.Children.Add(Lbl("LOCAL"));
        _cboLocal = new ComboBox { Height = 34, FontSize = 12.5 };
        body.Children.Add(_cboLocal);

        root.Children.Add(body);

        // Footer
        var footer = new Border { Background = FB("#EEF2F6"),
            BorderBrush = FB("#C5D5E8"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16, 12, 16, 14) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnAceptar = new Button {
            Content = "✓  Aplicar filtro", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = FB("#1A4F6E"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12.5,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(0,0,8,0) };
        btnAceptar.Click += OnAceptar;
        var btnCancelar = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(16,0,16,0),
            Background = FB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand };
        btnCancelar.Click += (_, _) => Close();
        footSp.Children.Add(btnAceptar);
        footSp.Children.Add(btnCancelar);
        footer.Child = footSp;
        root.Children.Add(footer);

        Content = root;
    }

    private async Task CargarLocales()
    {
        try
        {
            using var conn = _db.Create();
            var locales = (await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL")).ToList();

            _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
            foreach (var (id, nombre) in locales)
                _cboLocal.Items.Add(new ComboBoxItem { Content = nombre, Tag = id });
            _cboLocal.SelectedIndex = 0;
        }
        catch { /* sin locales */ }
    }

    private void OnAceptar(object s, RoutedEventArgs e)
    {
        if (_chkTodas.IsChecked == true)
        {
            FechaDesde = null;
            FechaHasta = null;
        }
        else
        {
            FechaDesde = _dpDesde.SelectedDate;
            FechaHasta = _dpHasta.SelectedDate;
            if (FechaDesde == null || FechaHasta == null)
            { MessageBox.Show("Seleccione ambas fechas o marque 'Todas las fechas'.", "Aviso"); return; }
            if (FechaDesde > FechaHasta)
            { MessageBox.Show("La fecha inicial no puede ser mayor a la fecha final.", "Aviso"); return; }
        }

        if (_cboLocal.SelectedItem is ComboBoxItem sel && (int)sel.Tag > 0)
        {
            IdLocal     = (int)sel.Tag;
            LocalNombre = sel.Content?.ToString() ?? "";
        }
        else
        {
            IdLocal     = null;
            LocalNombre = "Todos los locales";
        }

        DialogResult = true;
        Close();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  GESTIÓN DE PROMOCIONES  — crear / terminar promos por artículo y local
// ══════════════════════════════════════════════════════════════════════════════
public class GestionPromocionesWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private TextBox   _txtBusqArt     = null!;
    private DataGrid  _gridArts       = null!;
    private int?      _idArtSel       = null;
    private TextBlock _lblArtSel      = null!;
    private TextBlock _lblPventa      = null!;
    // Réplica de _lblArtSel/_lblPventa pero ubicada junto al campo de precio promo
    // (columna de configuración), para que se vea qué artículo se está por ingresar
    // sin tener que mirar la columna de la grilla de búsqueda.
    private TextBlock _lblArtSelCfg   = null!;
    private TextBlock _lblPventaCfg   = null!;
    private TextBox   _txtPrecioPromo = null!;
    private DatePicker _dpInicio      = null!;
    private DatePicker _dpFin         = null!;
    private readonly List<(CheckBox Chk, int IdLocal, string Nombre)> _localesChk = new();
    private CheckBox  _chkTodos       = null!;
    private DataGrid  _gridEstado     = null!;
    private TextBlock _lblEstado      = null!;
    private WrapPanel _wrapLocales    = null!;
    private List<FilaEstadoLocal> _estadoTodos = new();

    // ── Lista pendiente de artículos a promocionar (réplica del sistema viejo:
    // se van "Ingresando" varios artículos con su propio precio a una grilla, y
    // recién se impactan todos juntos en la base al presionar "Guardar Promoción") ──
    private DataGrid  _gridPendientes = null!;
    private readonly ObservableCollection<FilaPromoPendiente> _pendientes = new();
    private Button    _btnIngresar    = null!;

    private static System.Windows.Media.SolidColorBrush GB(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    public GestionPromocionesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title  = "Gestión de Promociones";
        Width  = 1280; Height = 680;
        MinWidth = 1080; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = GB("#FAF8FF");
        BuildUI();
        Loaded += async (_, _) => { await CargarLocales(); await BuscarArticulos(); };
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── HEADER ──────────────────────────────────────────────────────────
        var hdr = new Border { Background = GB("#4527A0"), Padding = new Thickness(16, 10, 16, 10) };
        Grid.SetRow(hdr, 0);
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.Children.Add(new TextBlock { Text = "Gestión de Promociones",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 17, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center });

        Button MkHBtn(string txt, string bg) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            Background = GB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0) };

        var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(hdrBtns, 1);
        var bVista = MkHBtn("👁 Vista previa", "#6A1B9A");
        bVista.Click += (_, _) => ImprimirPromocion(preview: true);
        hdrBtns.Children.Add(bVista);
        var bImpr = MkHBtn("🖨 Imprimir", "#1B5E20");
        bImpr.Click += (_, _) => ImprimirPromocion(preview: false);
        hdrBtns.Children.Add(bImpr);

        var btnCerrar = MkHBtn("✕ Cerrar", "#311B92");
        btnCerrar.Margin = new Thickness(0, 0, 0, 0);
        btnCerrar.Click += (_, _) => Close();
        hdrBtns.Children.Add(btnCerrar);
        hdrG.Children.Add(hdrBtns);
        hdr.Child = hdrG;
        root.Children.Add(hdr);

        // ── CUERPO ───────────────────────────────────────────────────────────
        // 3 columnas: artículos | configuración+lista pendiente | locales+estado —
        // antes "Aplicar a locales" y "Estado por local" quedaban apiladas debajo de
        // la configuración, y al crecer la lista de pendientes se comprimían/tapaban
        // entre sí. Separadas en su propia columna no compiten por el mismo espacio vertical.
        var body = new Grid { Margin = new Thickness(10, 8, 10, 8) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) }); // col articulos
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 320 }); // col config+lista pendiente
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) }); // col locales+estado
        Grid.SetRow(body, 1);

        // ── COL 0: lista artículos ───────────────────────────────────────────
        var col0 = new Grid();
        col0.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        col0.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // búsqueda
        col0.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grilla
        col0.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // info art seleccionado
        Grid.SetColumn(col0, 0);

        var artHdrBorder = new Border { Background = GB("#512DA8"),
            CornerRadius = new CornerRadius(6, 6, 0, 0), Padding = new Thickness(12, 9, 12, 9) };
        artHdrBorder.Child = new TextBlock { Text = "1.  Seleccionar artículo",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 };
        Grid.SetRow(artHdrBorder, 0); col0.Children.Add(artHdrBorder);

        var busqBar = new Border { Background = GB("#EDE7F6"), Padding = new Thickness(8, 7, 8, 7) };
        var busqRow = new Grid();
        busqRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        busqRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _txtBusqArt = new TextBox { Padding = new Thickness(7, 5, 7, 5), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = GB("#9575CD") };
        _txtBusqArt.TextChanged += async (_, _) => await BuscarArticulos();
        _txtBusqArt.KeyDown     += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticulos(); };
        var btnBusq = new Button { Content = "Buscar", Height = 32, Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(6, 0, 0, 0), Background = GB("#512DA8"),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnBusq.Click += async (_, _) => await BuscarArticulos();
        Grid.SetColumn(btnBusq, 1);
        busqRow.Children.Add(_txtBusqArt); busqRow.Children.Add(btnBusq);
        busqBar.Child = busqRow;
        Grid.SetRow(busqBar, 1); col0.Children.Add(busqBar);

        // grilla artículos — columna Descripción con TextWrapping
        var artGridBorder = new Border { BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1),
            ClipToBounds = true };
        var artColHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        artColHdr.Setters.Add(new Setter(Control.BackgroundProperty, GB("#7E57C2")));
        artColHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        artColHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        artColHdr.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        artColHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        artColHdr.Setters.Add(new Setter(Control.BorderBrushProperty, GB("#512DA8")));
        artColHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 2)));

        // row style para artículos: resaltar el seleccionado con promo activa
        var artRowStyle = new Style(typeof(DataGridRow));
        var dtArtPromo = new DataTrigger { Binding = new System.Windows.Data.Binding("TienePromo"), Value = true };
        dtArtPromo.Setters.Add(new Setter(DataGridRow.ForegroundProperty, GB("#1B5E20")));
        dtArtPromo.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        artRowStyle.Triggers.Add(dtArtPromo);

        _gridArts = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 44,
            FontSize = 11, BorderThickness = new Thickness(0), SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = GB("#F3EEF8"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = GB("#D1C4E9"),
            ColumnHeaderStyle = artColHdr, RowStyle = artRowStyle };

        // Código — ancho fijo
        _gridArts.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("Codigo"), Width = 80, MinWidth = 60 });

        // Descripción — TextWrapping via ElementStyle
        var descCol = new DataGridTextColumn {
            Header = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var descElemStyle = new Style(typeof(TextBlock));
        descElemStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        descElemStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        descElemStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
        descCol.ElementStyle = descElemStyle;
        _gridArts.Columns.Add(descCol);

        _gridArts.SelectionChanged += OnArticuloSeleccionado;
        artGridBorder.Child = _gridArts;
        Grid.SetRow(artGridBorder, 2); col0.Children.Add(artGridBorder);

        // Info artículo seleccionado
        var artInfoBorder = new Border { Background = GB("#EDE7F6"),
            Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1, 0, 1, 1),
            CornerRadius = new CornerRadius(0, 0, 6, 6) };
        _lblArtSel = new TextBlock { Text = "Haga clic en un artículo para seleccionarlo",
            Foreground = GB("#9E9E9E"), FontStyle = FontStyles.Italic,
            FontSize = 11, TextWrapping = TextWrapping.Wrap };
        _lblPventa = new TextBlock { Foreground = GB("#4527A0"), FontWeight = FontWeights.Bold,
            FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
        var artInfoSp = new StackPanel();
        artInfoSp.Children.Add(_lblArtSel); artInfoSp.Children.Add(_lblPventa);
        artInfoBorder.Child = artInfoSp;
        Grid.SetRow(artInfoBorder, 3); col0.Children.Add(artInfoBorder);

        body.Children.Add(col0);

        // ── COL 2: configuración de la promo + lista de artículos pendientes ─
        var col2 = new Grid();
        col2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(col2, 2);

        // ─ Sección 2: Configurar precio y fechas ────────────────────────────
        var cfgBorder = new Border { Background = GB("#EDE7F6"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 14), BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1) };
        Grid.SetRow(cfgBorder, 0);
        var cfgStack = new StackPanel();
        cfgStack.Children.Add(new TextBlock { Text = "2.  Configurar promoción",
            Foreground = GB("#4527A0"), FontWeight = FontWeights.Bold, FontSize = 13,
            Margin = new Thickness(0, 0, 0, 10) });

        // ── Artículo seleccionado (nombre + precio venta de referencia) — visible
        // acá, junto al campo de precio promo, para saber a qué artículo corresponde
        // el precio que se está por ingresar antes de sumarlo a la lista. Antes solo
        // vivía debajo de la grilla de la columna 1, lejos del campo de precio.
        var artSelBox = new Border { Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(6), BorderBrush = GB("#9575CD"),
            BorderThickness = new Thickness(1), Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12) };
        var artSelSp = new StackPanel();
        _lblArtSelCfg = new TextBlock { Text = "Ningún artículo seleccionado",
            Foreground = GB("#9E9E9E"), FontStyle = FontStyles.Italic, FontSize = 12,
            TextWrapping = TextWrapping.Wrap };
        _lblPventaCfg = new TextBlock { Foreground = GB("#4527A0"), FontWeight = FontWeights.Bold,
            FontSize = 11, Margin = new Thickness(0, 3, 0, 0) };
        artSelSp.Children.Add(_lblArtSelCfg);
        artSelSp.Children.Add(_lblPventaCfg);
        artSelBox.Child = artSelSp;
        cfgStack.Children.Add(artSelBox);

        TextBlock CL(string t) => new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = GB("#512DA8"),
            Margin = new Thickness(0, 0, 6, 0) };

        // Precio de promo en su propia fila, a todo lo ancho — antes compartía fila con
        // Inicio/Fin en un Grid de 8 columnas fijas, y al quedar esta columna del layout
        // más angosta (con la nueva col. de Locales/Estado al lado) la columna Star del
        // precio se comprimía a un ancho casi nulo, dejando el campo invisible/inutilizable.
        var precioRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        precioRow.Children.Add(CL("Precio Promo Gs.:"));
        _txtPrecioPromo = new TextBox { Padding = new Thickness(8, 5, 8, 5), FontSize = 14,
            FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = GB("#9575CD"), Width = 160, TextAlignment = TextAlignment.Right };
        // Separador de miles en vivo — mismo patrón que _txtMonto en CajaEditarMovDialog.
        bool fmtBusyPromo = false;
        _txtPrecioPromo.TextChanged += (_, _) => {
            if (fmtBusyPromo) return; fmtBusyPromo = true;
            var raw = new string(_txtPrecioPromo.Text.Where(char.IsDigit).ToArray());
            if (long.TryParse(raw, out var n) && n > 0) {
                var fmt = n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                _txtPrecioPromo.Text = fmt; _txtPrecioPromo.CaretIndex = fmt.Length;
            } else if (raw.Length == 0) {
                _txtPrecioPromo.Text = "";
            }
            fmtBusyPromo = false;
        };
        precioRow.Children.Add(_txtPrecioPromo);
        cfgStack.Children.Add(precioRow);

        // Inicio / Fin en su propia fila
        var fechasRow = new Grid();
        fechasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fechasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fechasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        fechasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fechasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblIni = CL("Inicio:"); Grid.SetColumn(lblIni, 0); fechasRow.Children.Add(lblIni);
        _dpInicio = new DatePicker { SelectedDate = DateTime.Today, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_dpInicio, 1); fechasRow.Children.Add(_dpInicio);
        var lblFin = CL("Fin:"); Grid.SetColumn(lblFin, 3); fechasRow.Children.Add(lblFin);
        _dpFin = new DatePicker { SelectedDate = DateTime.Today.AddDays(30), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_dpFin, 4); fechasRow.Children.Add(_dpFin);
        cfgStack.Children.Add(fechasRow);

        // ── "Ingresar" — agrega el artículo seleccionado (con este precio) a la
        // lista pendiente de abajo, réplica del botón "Ingresar" del sistema viejo.
        // Todavía NO toca la base — recién "Guardar Promoción" impacta todo junto.
        _btnIngresar = new Button { Content = "⬇  Ingresar a la lista", Height = 34,
            Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = GB("#5E35B1"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand };
        _btnIngresar.Click += (_, _) => IngresarPendiente();
        cfgStack.Children.Add(_btnIngresar);

        // ── Grilla de artículos pendientes (varios artículos, cada uno con su
        // precio propio) — se guardan todos juntos al presionar "Guardar Promoción".
        var pendHdr = new TextBlock { Text = "Artículos a promocionar en esta tanda:",
            Foreground = GB("#512DA8"), FontWeight = FontWeights.SemiBold, FontSize = 11,
            Margin = new Thickness(0, 12, 0, 4) };
        cfgStack.Children.Add(pendHdr);

        var pendGridBorder = new Border { BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), ClipToBounds = true, MaxHeight = 150 };
        var pendColHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        pendColHdr.Setters.Add(new Setter(Control.BackgroundProperty, GB("#7E57C2")));
        pendColHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        pendColHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        pendColHdr.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
        pendColHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 6, 5)));
        _gridPendientes = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            FontSize = 11, BorderThickness = new Thickness(0), SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = GB("#F3EEF8"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = GB("#D1C4E9"), ColumnHeaderStyle = pendColHdr,
            ItemsSource = _pendientes, CanUserAddRows = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _gridPendientes.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("Codigo"), Width = 70 });
        // Descripción con TextWrapping — antes el nombre largo del artículo se cortaba sin
        // wrap dentro de la columna Star, mismo criterio que la grilla de "1. Seleccionar
        // artículo" (descElemStyle). RowHeight pasa a Auto en la grilla para que la fila
        // crezca cuando el texto envuelve a 2 líneas.
        var pendDescCol = new DataGridTextColumn {
            Header = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var pendDescElemStyle = new Style(typeof(TextBlock));
        pendDescElemStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        pendDescElemStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        pendDescElemStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 3, 4, 3)));
        pendDescCol.ElementStyle = pendDescElemStyle;
        _gridPendientes.Columns.Add(pendDescCol);
        _gridPendientes.Columns.Add(new DataGridTextColumn { Header = "Precio promo",
            Binding = new System.Windows.Data.Binding("PrecioPromoStr"), Width = 90 });
        pendGridBorder.Child = _gridPendientes;
        cfgStack.Children.Add(pendGridBorder);

        var pendFoot = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        var btnQuitarPend = new Button { Content = "Quitar seleccionado", Height = 26,
            Padding = new Thickness(10, 0, 10, 0), Background = GB("#B0A8D9"),
            Foreground = System.Windows.Media.Brushes.White, FontSize = 10.5,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnQuitarPend.Click += (_, _) => {
            if (_gridPendientes.SelectedItem is FilaPromoPendiente f) _pendientes.Remove(f);
        };
        pendFoot.Children.Add(btnQuitarPend);
        cfgStack.Children.Add(pendFoot);

        cfgBorder.Child = cfgStack;
        col2.Children.Add(cfgBorder);

        // ── COL 3: locales + estado — separada de la configuración para que no
        // se compriman entre sí cuando la lista de artículos pendientes crece.
        var col3 = new Grid();
        col3.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // locales
        col3.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        col3.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // estado
        Grid.SetColumn(col3, 4);

        // ─ Sección 3: Locales ────────────────────────────────────────────────
        var locBorder = new Border { Background = GB("#F3EEF8"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12), BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1) };
        Grid.SetRow(locBorder, 0);
        var locHdrG = new Grid();
        locHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        locHdrG.Children.Add(new TextBlock { Text = "3.  Aplicar a locales",
            Foreground = GB("#4527A0"), FontWeight = FontWeights.Bold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center });
        // Arranca DESTILDADO — antes venía marcado por defecto y, si el vendedor no se daba
        // cuenta de destildarlo antes de guardar, la promo se aplicaba a TODOS los locales
        // del sistema (vía GUARDAR_PROMOCIONAR_TODOS_CS) aunque solo hubiera querido un local
        // puntual. Bug real reportado 2026-08-04: verificado contra la base que una promo
        // pensada para un solo local terminó activa en 8 locales sin que el usuario lo pidiera.
        _chkTodos = new CheckBox { IsChecked = false, VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        _chkTodos.Content = new TextBlock { Text = "Seleccionar todos", Foreground = GB("#4527A0"),
            FontWeight = FontWeights.Bold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        _chkTodos.Checked   += OnChkTodosChecked;
        _chkTodos.Unchecked += OnChkTodosUnchecked;
        Grid.SetColumn(_chkTodos, 1); locHdrG.Children.Add(_chkTodos);
        _wrapLocales = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), Orientation = Orientation.Horizontal };
        var locSp = new StackPanel();
        locSp.Children.Add(locHdrG);
        locSp.Children.Add(_wrapLocales);
        locBorder.Child = locSp;
        col3.Children.Add(locBorder);

        // ─ Sección 4: Estado por local ───────────────────────────────────────
        var estBorder = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true,
            BorderBrush = GB("#D1C4E9"), BorderThickness = new Thickness(1) };
        Grid.SetRow(estBorder, 2);
        var estPanel = new Grid();
        estPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        estPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var estHdr = new Border { Background = GB("#512DA8"), Padding = new Thickness(12, 8, 12, 8) };
        _lblEstado = new TextBlock { Text = "4.  Estado actual por local (seleccione un artículo)",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
        estHdr.Child = _lblEstado;
        Grid.SetRow(estHdr, 0); estPanel.Children.Add(estHdr);

        var estColHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        estColHdr.Setters.Add(new Setter(Control.BackgroundProperty, GB("#7E57C2")));
        estColHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        estColHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        estColHdr.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        estColHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        estColHdr.Setters.Add(new Setter(Control.BorderBrushProperty, GB("#512DA8")));
        estColHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 2)));

        // row style: verde = en promo, gris claro = sin promo
        var estRowStyle = new Style(typeof(DataGridRow));
        var dtEnPromo = new DataTrigger { Binding = new System.Windows.Data.Binding("EnPromo"), Value = true };
        dtEnPromo.Setters.Add(new Setter(DataGridRow.BackgroundProperty, GB("#E8F5E9")));
        dtEnPromo.Setters.Add(new Setter(DataGridRow.ForegroundProperty, GB("#1B5E20")));
        dtEnPromo.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        var dtSinPromo = new DataTrigger { Binding = new System.Windows.Data.Binding("EnPromo"), Value = false };
        dtSinPromo.Setters.Add(new Setter(DataGridRow.ForegroundProperty, GB("#757575")));
        estRowStyle.Triggers.Add(dtEnPromo);
        estRowStyle.Triggers.Add(dtSinPromo);

        _gridEstado = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 32,
            FontSize = 11, BorderThickness = new Thickness(0), SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = GB("#F9F7FF"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = GB("#D1C4E9"),
            ColumnHeaderStyle = estColHdr, RowStyle = estRowStyle };
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "Local",
            Binding = new System.Windows.Data.Binding("NombreLocal"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "Estado",
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Width = 130 });
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "P. Promo Gs.",
            Binding = new System.Windows.Data.Binding("PProm") { StringFormat = "N0" }, Width = 120 });
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "P. Venta Gs.",
            Binding = new System.Windows.Data.Binding("Pventa") { StringFormat = "N0" }, Width = 120 });
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "Inicio",
            Binding = new System.Windows.Data.Binding("Inicio"), Width = 95 });
        _gridEstado.Columns.Add(new DataGridTextColumn { Header = "Fin",
            Binding = new System.Windows.Data.Binding("Fin"), Width = 95 });
        Grid.SetRow(_gridEstado, 1); estPanel.Children.Add(_gridEstado);
        estBorder.Child = estPanel;
        col3.Children.Add(estBorder);

        body.Children.Add(col2);
        body.Children.Add(col3);
        root.Children.Add(body);

        // ── BARRA INFERIOR ───────────────────────────────────────────────────
        var botBar = new Border { Background = GB("#1A0050"), Padding = new Thickness(14, 10, 14, 10) };
        Grid.SetRow(botBar, 2);
        var botG = new Grid();
        botG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        botG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Info izquierda
        var botInfo = new TextBlock { Foreground = GB("#CE93D8"), FontSize = 11, FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Verde = promo activa   |   Gris = sin promo   |   F5 = refrescar" };
        botG.Children.Add(botInfo);

        // Botones derecha
        var botBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(botBtns, 1);

        Button MkAct(string t, string bg) => new Button { Content = t, Height = 38,
            Padding = new Thickness(20, 0, 20, 0), Margin = new Thickness(8, 0, 0, 0),
            Background = GB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };

        var btnGuardar = MkAct("Guardar Promoción", "#2E7D32");
        btnGuardar.Click += async (_, _) => await GuardarPromocion();
        var btnTermSel = MkAct("Terminar (locales sel.)", "#E65100");
        btnTermSel.Click += async (_, _) => await TerminarPromoLocales();
        var btnTermTodo = MkAct("Terminar TODO", "#B71C1C");
        btnTermTodo.ToolTip = "Termina la promoción en todos los locales del artículo";
        btnTermTodo.Click += async (_, _) => await TerminarPromoTodos();

        botBtns.Children.Add(btnGuardar);
        botBtns.Children.Add(btnTermSel);
        botBtns.Children.Add(btnTermTodo);
        botG.Children.Add(botBtns);
        botBar.Child = botG;
        root.Children.Add(botBar);

        Content = root;
    }

    private async Task CargarLocales()
    {
        try
        {
            using var conn = _db.Create();
            var locales = (await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL")).ToList();

            foreach (var (id, nombre) in locales)
            {
                // Arranca DESTILDADO — ver comentario junto a _chkTodos: el vendedor debe
                // elegir explícitamente a qué local(es) aplica la promo, en vez de que quede
                // marcado por defecto y afecte locales que no correspondían.
                var chk = new CheckBox {
                    IsChecked = false, Margin = new Thickness(0, 0, 16, 8),
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center };
                chk.Content = new TextBlock { Text = nombre, FontSize = 11,
                    Foreground = GB("#311B92"), VerticalAlignment = VerticalAlignment.Center };
                chk.Checked   += (_, _) => ActualizarChkTodos();
                chk.Unchecked += (_, _) => ActualizarChkTodos();
                _localesChk.Add((chk, id, nombre));
                _wrapLocales.Children.Add(chk);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando locales: {ex.Message}");
        }
    }

    private void ActualizarChkTodos()
    {
        bool todos   = _localesChk.All(l => l.Chk.IsChecked == true);
        bool ninguno = _localesChk.All(l => l.Chk.IsChecked == false);
        _chkTodos.Checked   -= OnChkTodosChecked;
        _chkTodos.Unchecked -= OnChkTodosUnchecked;
        _chkTodos.IsChecked  = todos ? true : (ninguno ? false : null);
        _chkTodos.Checked   += OnChkTodosChecked;
        _chkTodos.Unchecked += OnChkTodosUnchecked;
        FiltrarEstadoPorLocales();
    }
    private void OnChkTodosChecked  (object s, RoutedEventArgs e) { foreach (var l in _localesChk) l.Chk.IsChecked = true;  FiltrarEstadoPorLocales(); }
    private void OnChkTodosUnchecked(object s, RoutedEventArgs e) { foreach (var l in _localesChk) l.Chk.IsChecked = false; FiltrarEstadoPorLocales(); }

    private void FiltrarEstadoPorLocales()
    {
        if (_estadoTodos.Count == 0) return;
        // Mapa nombre→id para filtrar por los locales marcados
        var selNombres = new HashSet<string>(
            _localesChk.Where(l => l.Chk.IsChecked == true).Select(l => l.Nombre),
            StringComparer.OrdinalIgnoreCase);
        _gridEstado.ItemsSource = _estadoTodos
            .Where(f => selNombres.Contains(f.NombreLocal))
            .ToList();
    }

    private async Task BuscarArticulos()
    {
        var q = _txtBusqArt.Text.Trim();
        try
        {
            using var conn = _db.Create();
            List<FilaArtBusq> arts;
            if (string.IsNullOrEmpty(q))
            {
                arts = (await conn.QueryAsync<FilaArtBusq>(@"
                    SELECT TOP 300
                        A.ID,
                        CAST(A.CA AS NVARCHAR(50)) AS Codigo,
                        A.D AS Descripcion,
                        CAST(CASE WHEN EXISTS(SELECT 1 FROM PRICES P2 WHERE P2.IDART=A.ID AND P2.PR=1 AND P2.DELETADO=0) THEN 1 ELSE 0 END AS BIT) AS TienePromo
                    FROM ARTICULOS A
                    WHERE EXISTS (SELECT 1 FROM PRICES P WHERE P.IDART = A.ID AND P.DELETADO = 0)
                    ORDER BY TienePromo DESC, A.D")).ToList();
            }
            else
            {
                arts = (await conn.QueryAsync<FilaArtBusq>(@"
                    SELECT DISTINCT
                        A.ID,
                        CAST(A.CA AS NVARCHAR(50)) AS Codigo,
                        A.D AS Descripcion,
                        CAST(CASE WHEN EXISTS(SELECT 1 FROM PRICES P2 WHERE P2.IDART=A.ID AND P2.PR=1 AND P2.DELETADO=0) THEN 1 ELSE 0 END AS BIT) AS TienePromo
                    FROM ARTICULOS A
                    INNER JOIN PRICES P ON A.ID = P.IDART
                    WHERE P.DELETADO = 0
                      AND (A.D LIKE @q OR CAST(A.CA AS NVARCHAR(50)) LIKE @q)
                    ORDER BY TienePromo DESC, A.D", new { q = "%" + q + "%" })).ToList();
            }
            _gridArts.ItemsSource = arts;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void OnArticuloSeleccionado(object s, SelectionChangedEventArgs e)
    {
        if (_gridArts.SelectedItem is not FilaArtBusq art) return;
        _idArtSel             = art.ID;
        _lblArtSel.Text       = art.Descripcion;
        _lblArtSel.FontStyle  = FontStyles.Normal;
        _lblArtSel.Foreground = GB("#311B92");
        _lblArtSelCfg.Text       = $"{art.Codigo} — {art.Descripcion}";
        _lblArtSelCfg.FontStyle  = FontStyles.Normal;
        _lblArtSelCfg.Foreground = GB("#311B92");
        // limpiar precio para que se auto-cargue desde promo activa si existe
        _txtPrecioPromo.Text  = "";
        _ = CargarEstadoArt(art.ID);
    }

    private async Task CargarEstadoArt(int idArt)
    {
        try
        {
            using var conn = _db.Create();
            var filas = (await conn.QueryAsync<FilaEstadoLocal>(@"
                SELECT L.NOMBRE AS NombreLocal,
                       P.PR,
                       P.PVENTA,
                       P.PPROMO AS PProm,
                       CASE P.PR WHEN 1 THEN 'ACTIVA' ELSE 'Sin promo' END AS EstadoTexto,
                       CONVERT(VARCHAR(10), P.INICIO, 103) AS Inicio,
                       CONVERT(VARCHAR(10), P.FIN,    103) AS Fin
                FROM PRICES P
                INNER JOIN LOCALES L ON P.IDLOCAL = L.ID_LOCAL
                WHERE P.IDART = @id AND P.DELETADO = 0
                ORDER BY L.ID_LOCAL", new { id = idArt })).ToList();

            _estadoTodos = filas;
            FiltrarEstadoPorLocales();

            int activas = filas.Count(f => f.EnPromo);
            int total   = filas.Count;

            // Precio de venta de referencia (primer registro)
            var pv = filas.FirstOrDefault()?.Pventa ?? 0;
            _lblPventa.Text    = pv > 0 ? $"Precio venta: Gs. {pv:N0}" : "";
            _lblPventaCfg.Text = pv > 0 ? $"Precio venta: Gs. {pv:N0}" : "";

            // Si hay promos activas, pre-cargar precio y fechas del primero activo
            var primActivo = filas.FirstOrDefault(f => f.EnPromo);
            if (primActivo != null)
            {
                _txtPrecioPromo.Text = ((int)primActivo.PProm).ToString();
                // intentar parsear fechas dd/MM/yyyy del primer registro activo
                if (DateTime.TryParseExact(primActivo.Inicio, "dd/MM/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dtIni))
                    _dpInicio.SelectedDate = dtIni;
                if (DateTime.TryParseExact(primActivo.Fin, "dd/MM/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dtFin))
                    _dpFin.SelectedDate = dtFin;
            }

            _lblEstado.Text = activas > 0
                ? $"4.  Estado actual — {activas} de {total} local(es) con promoción activa"
                : $"4.  Estado actual — Sin promociones activas en ningún local";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    // Agrega el artículo actualmente seleccionado (con el precio tipeado) a la
    // grilla de pendientes — no toca la base todavía. Réplica del botón "Ingresar"
    // del sistema viejo, que iba sumando filas a la lista antes de guardar todo junto.
    private void IngresarPendiente()
    {
        if (_idArtSel == null) { MessageBox.Show("Seleccione un artículo primero.", "Aviso"); return; }
        if (!decimal.TryParse(_txtPrecioPromo.Text.Replace(".", "").Replace(",", ""), out var precio) || precio <= 0)
        { MessageBox.Show("Ingrese un precio de promoción válido.", "Aviso"); return; }

        var art = _gridArts.SelectedItem as FilaArtBusq;
        var codigo = art?.Codigo ?? "";
        var desc   = _lblArtSel.Text;

        var yaEnLista = _pendientes.FirstOrDefault(f => f.IdArt == _idArtSel.Value);
        if (yaEnLista != null)
        {
            // Ya estaba en la lista: solo actualiza el precio en vez de duplicar la fila.
            yaEnLista.PrecioPromo = precio;
            _gridPendientes.Items.Refresh();
        }
        else
        {
            _pendientes.Add(new FilaPromoPendiente {
                IdArt = _idArtSel.Value, Codigo = codigo, Descripcion = desc, PrecioPromo = precio });
        }

        // Limpiar selección tras ingresar — deja claro que este artículo ya quedó
        // sumado a la lista y evita ingresarlo dos veces sin querer con doble clic.
        _idArtSel = null;
        _txtPrecioPromo.Text = "";
        _gridArts.SelectedItem = null;
        _lblArtSelCfg.Text       = "Ningún artículo seleccionado";
        _lblArtSelCfg.FontStyle  = FontStyles.Italic;
        _lblArtSelCfg.Foreground = GB("#9E9E9E");
        _lblPventaCfg.Text = "";
    }

    // Guarda TODOS los artículos de la lista pendiente de una sola vez, aplicando
    // el mismo período (Inicio/Fin) y los mismos locales a cada uno — mismo criterio
    // del sistema viejo, donde Inicio/Fin/Locales son compartidos por la tanda mientras
    // que el precio es propio de cada artículo ingresado.
    private async Task GuardarPromocion()
    {
        if (_pendientes.Count == 0)
        {
            MessageBox.Show("Ingrese al menos un artículo a la lista antes de guardar (botón \"Ingresar a la lista\").",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var ini = _dpInicio.SelectedDate;
        var fin = _dpFin.SelectedDate;
        if (ini == null || fin == null || ini > fin)
        { MessageBox.Show("Las fechas de inicio y fin son inválidas.", "Aviso"); return; }

        var localesSel = _localesChk.Where(l => l.Chk.IsChecked == true).Select(l => l.IdLocal).ToList();
        if (localesSel.Count == 0) { MessageBox.Show("Seleccione al menos un local.", "Aviso"); return; }

        var todos = localesSel.Count == _localesChk.Count;
        var listaTxt = string.Join("\n", _pendientes.Select(f => $"  • {f.Codigo} — {f.Descripcion}: Gs. {f.PrecioPromo:N0}"));
        var confirm = MessageBox.Show(
            $"Se va a guardar la promoción de {_pendientes.Count} artículo(s):\n{listaTxt}\n\n" +
            $"Período: {ini:dd/MM/yyyy} — {fin:dd/MM/yyyy}\n" +
            $"Locales: {(todos ? "Todos" : string.Join(", ", _localesChk.Where(l => l.Chk.IsChecked==true).Select(l => l.Nombre)))}\n\n" +
            "¿Confirmar guardar promoción?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var conn = _db.Create();
            int totalAfectados = 0;
            var iniNorm = ini.Value.Date;
            var finNorm = fin.Value.Date.AddHours(23).AddMinutes(59);

            foreach (var fila in _pendientes)
            {
                if (todos)
                {
                    var p = new DynamicParameters();
                    p.Add("@idart",        fila.IdArt);
                    p.Add("@enpromo",      (byte)1);
                    p.Add("@preciopromo",  fila.PrecioPromo);
                    p.Add("@inicio",       ini.Value);
                    p.Add("@fin",          fin.Value);
                    p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
                    await conn.ExecuteAsync("GUARDAR_PROMOCIONAR_TODOS_CS", p, commandType: CommandType.StoredProcedure);
                    var msg = p.Get<string>("@msg");
                    if (msg != "GUARDADO") { MessageBox.Show($"Artículo {fila.Codigo}: respuesta del servidor: {msg}"); continue; }
                    totalAfectados += _localesChk.Count;
                }
                else
                {
                    // SQL directo para manejar N locales sin limitación de 6
                    foreach (var idLocal in localesSel)
                    {
                        totalAfectados += await conn.ExecuteAsync(@"
                            UPDATE PRICES SET PR=1, PPROMO=@precio, INICIO=@ini, FIN=@fin
                            WHERE IDART=@idart AND IDLOCAL=@idlocal AND DELETADO=0",
                            new { idart = fila.IdArt, idlocal = idLocal,
                                  precio = fila.PrecioPromo, ini = iniNorm, fin = finNorm });
                    }
                }
            }

            MessageBox.Show($"Promoción guardada: {_pendientes.Count} artículo(s), {totalAfectados} fila(s) de local afectadas.",
                "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
            _pendientes.Clear();
            if (_idArtSel != null) await CargarEstadoArt(_idArtSel.Value);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
    }

    private async Task TerminarPromoLocales()
    {
        if (_idArtSel == null) { MessageBox.Show("Seleccione un artículo primero.", "Aviso"); return; }
        var localesSel = _localesChk.Where(l => l.Chk.IsChecked == true).Select(l => l.IdLocal).ToList();
        if (localesSel.Count == 0) { MessageBox.Show("Seleccione al menos un local.", "Aviso"); return; }

        var confirm = MessageBox.Show(
            $"Terminar promoción de:\n{_lblArtSel.Text}\n\n" +
            $"En {localesSel.Count} local(es) seleccionado(s).\n¿Confirmar?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var conn = _db.Create();
            int afect = await conn.ExecuteAsync(@"
                UPDATE PRICES SET PR=0, INICIO=GETDATE(), FIN=GETDATE()
                WHERE IDART=@idart AND IDLOCAL IN @locales AND PR=1 AND DELETADO=0",
                new { idart = _idArtSel.Value, locales = localesSel });
            MessageBox.Show($"Promoción terminada en {afect} local(es).", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarEstadoArt(_idArtSel.Value);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }

    private async Task TerminarPromoTodos()
    {
        if (_idArtSel == null) { MessageBox.Show("Seleccione un artículo primero.", "Aviso"); return; }
        var confirm = MessageBox.Show(
            $"Terminar promoción de:\n{_lblArtSel.Text}\n\nEN TODOS LOS LOCALES.\n¿Confirmar?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@Idart",   _idArtSel.Value);
            p.Add("@ppromo",  0m);
            p.Add("@Result", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
            await conn.ExecuteAsync("TERMINAR_PROMOCION_TODOS_LOCALES_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@Result");
            MessageBox.Show(msg == "GUARDADO" ? "Promoción terminada en todos los locales." : $"Resultado: {msg}",
                "Resultado", MessageBoxButton.OK, msg == "GUARDADO" ? MessageBoxImage.Information : MessageBoxImage.Warning);
            await CargarEstadoArt(_idArtSel.Value);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }

    private void ImprimirPromocion(bool preview = false)
    {
        if (_idArtSel == null || _estadoTodos.Count == 0)
        {
            MessageBox.Show("Seleccione un artículo para ver su estado antes de imprimir.",
                "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var p = new PromocionPagina
        {
            Filas    = _estadoTodos.Select(f => new FilaPromoImp(
                           f.NombreLocal, f.EstadoTexto, f.PProm, f.Pventa, f.Inicio, f.Fin)).ToList(),
            Articulo = _lblArtSel.Text,
            Codigo   = (_gridArts.SelectedItem as FilaArtBusq)?.Codigo ?? "",
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            LogoPath = PromocionPagina.ResolverLogoPath(),
        };

        if (preview)
            new PromocionPreviewWindow(p) { Owner = this }.ShowDialog();
        else
            PromocionImpresora.Imprimir(p, this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }
}

internal class FilaArtBusq
{
    public int    ID          { get; set; }
    public string Codigo      { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool   TienePromo  { get; set; }
}

internal class FilaEstadoLocal
{
    public string  NombreLocal  { get; set; } = "";
    public byte    PR           { get; set; }
    public decimal Pventa       { get; set; }
    public decimal PProm        { get; set; }
    public string  EstadoTexto  { get; set; } = "";
    public string  Inicio       { get; set; } = "";
    public string  Fin          { get; set; } = "";
    public bool    EnPromo      => PR == 1;
}

// Fila de la grilla "pendiente de guardar" — réplica del diseño viejo: cada
// artículo ingresado a la lista lleva su PROPIO precio de promoción, mientras que
// Inicio/Fin/Locales son compartidos por todos los artículos de la tanda.
internal class FilaPromoPendiente
{
    public int     IdArt        { get; set; }
    public string  Codigo       { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public decimal PrecioPromo  { get; set; }
    public string  PrecioPromoStr => PrecioPromo.ToString("N0");
}

// ══════════════════════════════════════════════════════════════════════════════
//  ARTÍCULOS EN PROMOCIÓN  — rediseño moderno violeta/púrpura
// ══════════════════════════════════════════════════════════════════════════════
public class EnPromocionWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Controles
    private DataGrid  _grid      = null!;
    private TextBox   _txtBuscar = null!;
    private TextBox   _txtLocal  = null!;
    private Button    _btnQuitarLocal = null!;
    private int?      _idLocalFiltro  = null;

    // Contadores en header
    private TextBlock _lblConteo   = null!;
    private TextBlock _lblVigentes = null!;
    private TextBlock _lblTotal    = null!;

    // Datos completos para filtrado en memoria
    private List<FilaPromo> _todos = new();

    private static System.Windows.Media.SolidColorBrush PBr(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    public EnPromocionWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title  = "Artículos en Promoción";
        Width  = 1200; Height = 680;
        MinWidth = 900; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = PBr("#FAF8FF");
        BuildUI();
        Loaded += async (_, _) => await CargarTodos();
    }

    // Filtro de estado activo: null=todos, "Vigente", "Vencida", "Futura"
    private string? _filtroEstado = null;
    private TextBlock _kpiVigentes = null!, _kpiVencidas = null!, _kpiFuturas = null!;
    private Border _chipTodos = null!, _chipVig = null!, _chipVenc = null!, _chipFut = null!;

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── HEADER ─────────────────────────────────────────────────────────
        var hdr = new Border { Background = PBr("#0E2F44"), Padding = new Thickness(16, 10, 16, 10) };
        Grid.SetRow(hdr, 0);
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Título + subtítulo
        var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleSp.Children.Add(new TextBlock {
            Text = "🏷  Artículos en Promoción",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 16, FontWeight = FontWeights.Bold });
        titleSp.Children.Add(new TextBlock {
            Text = "Promociones activas, vencidas y futuras por local",
            Foreground = PBr("#7FB3D3"), FontSize = 10 });
        hdrG.Children.Add(titleSp);

        // KPI chips en el centro
        Border KpiChip(string label, string val, string bg, string fg, out TextBlock valBlock) {
            var tb = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = PBr(fg), TextAlignment = TextAlignment.Center };
            valBlock = tb;
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(tb);
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = PBr(fg), TextAlignment = TextAlignment.Center, Opacity = 0.8 });
            return new Border { Background = PBr(bg), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(4, 0, 4, 0),
                Child = sp };
        }
        var kpiPanel = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        kpiPanel.Children.Add(KpiChip("VIGENTES", "0", "#1B5E20", "#A5D6A7", out _kpiVigentes));
        kpiPanel.Children.Add(KpiChip("VENCIDAS", "0", "#B71C1C", "#FFCDD2", out _kpiVencidas));
        kpiPanel.Children.Add(KpiChip("FUTURAS",  "0", "#0D47A1", "#BBDEFB", out _kpiFuturas));
        Grid.SetColumn(kpiPanel, 1); hdrG.Children.Add(kpiPanel);

        // Botones Vista previa / Imprimir
        Button MkHBtn(string t, string bg) => new Button {
            Content = t, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            Background = PBr(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0) };
        var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        var btnPrev  = MkHBtn("👁 Vista previa", "#6A1B9A");
        var btnPrint = MkHBtn("🖨 Imprimir",     "#1B5E20");
        btnPrev.Click  += (_, _) => ImprimirEnPromocion(preview: true);
        btnPrint.Click += (_, _) => ImprimirEnPromocion(preview: false);
        hdrBtns.Children.Add(btnPrev);
        hdrBtns.Children.Add(btnPrint);
        Grid.SetColumn(hdrBtns, 2); hdrG.Children.Add(hdrBtns);

        // Conteo derecha
        _lblConteo = new TextBlock { Foreground = PBr("#7FB3D3"), FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(_lblConteo, 3); hdrG.Children.Add(_lblConteo);
        // _lblVigentes usado solo para compatibilidad con AplicarFiltro
        _lblVigentes = new TextBlock { Visibility = Visibility.Collapsed };
        hdr.Child = hdrG; root.Children.Add(hdr);

        // ── FILTROS ─────────────────────────────────────────────────────────
        var fBar = new Border { Background = PBr("#1A4F6E"), Padding = new Thickness(14, 8, 14, 8) };
        Grid.SetRow(fBar, 1);

        Button MkFBtn(string t, string bg, int h = 30) => new Button {
            Content = t, Height = h, Padding = new Thickness(12, 0, 12, 0),
            Background = PBr(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        TextBlock FLbl(string t) => new TextBlock { Text = t,
            Foreground = PBr("#BBDEFB"), FontWeight = FontWeights.SemiBold, FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
        Border FSep() => new Border { Width = 1, Background = PBr("#2A7AB5"),
            Margin = new Thickness(10, 0, 10, 0) };

        var fp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        // Búsqueda
        fp.Children.Add(FLbl("🔍 Buscar:"));
        _txtBuscar = new TextBox { Width = 210, Padding = new Thickness(7, 5, 7, 5),
            VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
            Background = PBr("#E8F0F7"), Foreground = PBr("#0E2F44"),
            BorderBrush = PBr("#2A7AB5"), BorderThickness = new Thickness(1) };
        _txtBuscar.TextChanged += (_, _) => AplicarFiltro();
        fp.Children.Add(_txtBuscar);
        fp.Children.Add(FSep());

        // Chips de filtro de estado
        fp.Children.Add(FLbl("Estado:"));
        Border MkChip(string label, string? estado, string activeColor) {
            var tb = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center };
            var chip = new Border { CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(2, 0, 2, 0),
                Background = PBr(activeColor), Cursor = Cursors.Hand, Child = tb,
                VerticalAlignment = VerticalAlignment.Center };
            chip.MouseLeftButtonUp += (_, _) => {
                _filtroEstado = (_filtroEstado == estado) ? null : estado;
                ActualizarChips();
                AplicarFiltro();
            };
            return chip;
        }
        _chipTodos = MkChip("Todos",    null,       "#1F6089");
        _chipVig   = MkChip("Vigentes", "Vigente",  "#2E7D32");
        _chipVenc  = MkChip("Vencidas", "Vencida",  "#C62828");
        _chipFut   = MkChip("Futuras",  "Futura",   "#1565C0");
        fp.Children.Add(_chipTodos);
        fp.Children.Add(_chipVig);
        fp.Children.Add(_chipVenc);
        fp.Children.Add(_chipFut);
        fp.Children.Add(FSep());

        // Selector local
        fp.Children.Add(FLbl("🏪 Local:"));
        _txtLocal = new TextBox { Width = 170, Padding = new Thickness(5, 4, 5, 4),
            IsReadOnly = true, Cursor = Cursors.Arrow,
            Background = PBr("#E8F0F7"), Foreground = PBr("#1F6089"),
            FontStyle = FontStyles.Italic, FontSize = 11,
            Text = "(todos)", VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = PBr("#2A7AB5"), BorderThickness = new Thickness(1) };
        var btnLocal = MkFBtn("Selec.", "#1F6089");
        btnLocal.Margin = new Thickness(4, 0, 0, 0);
        btnLocal.Click += (_, _) => {
            var modal = new BuscadorLocalModal(_db) { Owner = this };
            if (modal.ShowDialog() == true && modal.LocalSeleccionado != null) {
                _idLocalFiltro = modal.LocalSeleccionado.IdLocal;
                _txtLocal.Text = modal.LocalSeleccionado.Nombre;
                _txtLocal.FontStyle = FontStyles.Normal;
                _btnQuitarLocal.Visibility = Visibility.Visible;
                AplicarFiltro();
            }
        };
        _btnQuitarLocal = MkFBtn("✕", "#546E7A");
        _btnQuitarLocal.Margin = new Thickness(3, 0, 0, 0);
        _btnQuitarLocal.Visibility = Visibility.Collapsed;
        _btnQuitarLocal.Click += (_, _) => {
            _idLocalFiltro = null;
            _txtLocal.Text = "(todos)";
            _txtLocal.FontStyle = FontStyles.Italic;
            _btnQuitarLocal.Visibility = Visibility.Collapsed;
            AplicarFiltro();
        };
        fp.Children.Add(_txtLocal);
        fp.Children.Add(btnLocal);
        fp.Children.Add(_btnQuitarLocal);
        fp.Children.Add(FSep());

        var btnRefresh = MkFBtn("↺ Refrescar", "#0D47A1", 32);
        btnRefresh.FontSize = 12; btnRefresh.Margin = new Thickness(0, 0, 6, 0);
        btnRefresh.Click += async (_, _) => {
            _txtBuscar.Text = ""; _idLocalFiltro = null; _filtroEstado = null;
            _txtLocal.Text = "(todos)"; _txtLocal.FontStyle = FontStyles.Italic;
            _btnQuitarLocal.Visibility = Visibility.Collapsed;
            ActualizarChips();
            await CargarTodos();
        };
        var btnCerrar = MkFBtn("✕ Cerrar", "#546E7A", 32);
        btnCerrar.FontSize = 12;
        btnCerrar.Click += (_, _) => Close();
        fp.Children.Add(btnRefresh);
        fp.Children.Add(btnCerrar);

        fBar.Child = fp; root.Children.Add(fBar);

        // ── GRILLA ──────────────────────────────────────────────────────────
        var gridBorder = new Border { Margin = new Thickness(8, 6, 8, 0),
            BorderBrush = PBr("#D6E5EF"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true };
        Grid.SetRow(gridBorder, 2);

        var gridPanel = new Grid();
        gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        gridPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var gridHdr = new Border { Background = PBr("#1F6089"), Padding = new Thickness(12, 7, 12, 7) };
        var gridHdrG = new Grid();
        gridHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gridHdrG.Children.Add(new TextBlock { Text = "📋  Listado de artículos en promoción",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        var legendSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        void AddLegend(string color, string label) {
            legendSp.Children.Add(new Border { Width = 10, Height = 10, Background = PBr(color),
                CornerRadius = new CornerRadius(2), Margin = new Thickness(8, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center });
            legendSp.Children.Add(new TextBlock { Text = label, Foreground = PBr("#B3D4E8"),
                FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
        }
        AddLegend("#C8E6C9", "Vigente");
        AddLegend("#FFECB3", "Futura");
        AddLegend("#F5F5F5", "Vencida");
        Grid.SetColumn(legendSp, 1); gridHdrG.Children.Add(legendSp);
        gridHdr.Child = gridHdrG;
        Grid.SetRow(gridHdr, 0); gridPanel.Children.Add(gridHdr);

        var colHdrStyle = BuildPromoHeaderStyle();

        // Row styles: vigente=verde suave, futura=ámbar suave, vencida=gris
        var rowStyle = new Style(typeof(DataGridRow));
        var dtVigente = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vigente" };
        dtVigente.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PBr("#E8F5E9")));
        dtVigente.Setters.Add(new Setter(DataGridRow.ForegroundProperty, PBr("#1B5E20")));
        var dtFutura = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Futura" };
        dtFutura.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PBr("#FFFDE7")));
        dtFutura.Setters.Add(new Setter(DataGridRow.ForegroundProperty, PBr("#E65100")));
        var dtVencido = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vencida" };
        dtVencido.Setters.Add(new Setter(DataGridRow.ForegroundProperty, PBr("#9E9E9E")));
        dtVencido.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PBr("#FAFAFA")));
        rowStyle.Triggers.Add(dtVigente);
        rowStyle.Triggers.Add(dtFutura);
        rowStyle.Triggers.Add(dtVencido);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 33,
            FontSize = 12, BorderThickness = new Thickness(0),
            AlternatingRowBackground = PBr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = PBr("#DDEEFF"),
            ColumnHeaderStyle = colHdrStyle,
            RowStyle = rowStyle,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserSortColumns = true };

        DataGridTextColumn PC(string h, string p, double w, string? fmt = null) =>
            new() { Header = h, Width = w, MinWidth = 40, SortMemberPath = p,
                Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                      : new System.Windows.Data.Binding(p) };

        _grid.Columns.Add(new DataGridTextColumn { Header = "Local", SortMemberPath = "Local",
            Binding = new System.Windows.Data.Binding("Local"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 140 });
        _grid.Columns.Add(PC("Código",      "Codigo",    90));
        _grid.Columns.Add(new DataGridTextColumn { Header = "Artículo", SortMemberPath = "Articulo",
            Binding = new System.Windows.Data.Binding("Articulo"),
            Width = new DataGridLength(2.5, DataGridLengthUnitType.Star), MinWidth = 200 });
        _grid.Columns.Add(PC("Inicio",       "Inicio",   100));
        _grid.Columns.Add(PC("Fin",          "Fin",      100));
        _grid.Columns.Add(PC("P. Promo Gs.", "PPromo",   130, "N0"));
        _grid.Columns.Add(PC("Estado",       "EstadoTexto", 80));

        Grid.SetRow(_grid, 1); gridPanel.Children.Add(_grid);
        gridBorder.Child = gridPanel;
        root.Children.Add(gridBorder);

        // ── BARRA TOTALES ────────────────────────────────────────────────
        var statsBar = new Border { Background = PBr("#263238"), Padding = new Thickness(14, 8, 14, 8) };
        Grid.SetRow(statsBar, 3);
        _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12, Foreground = PBr("#4FC3F7") };
        statsBar.Child = _lblTotal;
        root.Children.Add(statsBar);

        Content = root;
        ActualizarChips();
    }

    private void ActualizarChips()
    {
        void SetChip(Border chip, string? estado) {
            bool activo = _filtroEstado == estado;
            chip.Opacity = activo ? 1.0 : 0.55;
            chip.BorderThickness = activo ? new Thickness(2) : new Thickness(0);
            chip.BorderBrush = activo
                ? System.Windows.Media.Brushes.White
                : System.Windows.Media.Brushes.Transparent;
        }
        if (_chipTodos != null) { SetChip(_chipTodos, null); SetChip(_chipVig, "Vigente"); SetChip(_chipVenc, "Vencida"); SetChip(_chipFut, "Futura"); }
    }

    private void ImprimirEnPromocion(bool preview = false)
    {
        var filas = (_grid.ItemsSource as System.Collections.Generic.IEnumerable<FilaPromo>)?.ToList();
        if (filas == null || filas.Count == 0)
        {
            MessageBox.Show("No hay datos para imprimir.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int vigentes = filas.Count(r => r.Vigente);
        int vencidas = filas.Count(r => !r.Vigente && r.EstadoTexto == "Vencida");
        int futuras  = filas.Count(r => r.EstadoTexto == "Futura");

        var localFiltro = _idLocalFiltro.HasValue ? _txtLocal.Text : "Todos los locales";

        var p = new EnPromoPagina
        {
            Filas       = filas.Select(f => new FilaEnPromoImp(
                              f.Local, f.Codigo, f.Articulo,
                              f.Inicio, f.Fin, f.PPromo, f.EstadoTexto)).ToList(),
            FechaImp    = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario     = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            LogoPath    = EnPromoPagina.ResolverLogoPath(),
            LocalFiltro = localFiltro,
            Vigentes    = vigentes,
            Vencidas    = vencidas,
            Futuras     = futuras,
        };

        if (preview)
            new EnPromocionPreviewWindow(p) { Owner = this }.ShowDialog();
        else
            EnPromocionImpresora.Imprimir(p, this);
    }

    private static Style BuildPromoHeaderStyle()
    {
        var purple     = PBr("#1F6089");
        var purpleDark = PBr("#0E2F44");
        var white      = System.Windows.Media.Brushes.White;

        var ct = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        var outerBorder = new FrameworkElementFactory(typeof(Border));
        outerBorder.Name = "OuterBorder";
        outerBorder.SetValue(Border.BackgroundProperty, purple);
        outerBorder.SetValue(Border.BorderBrushProperty, purpleDark);
        outerBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 2));
        outerBorder.SetValue(Border.PaddingProperty, new Thickness(8, 6, 4, 6));

        var grid = new FrameworkElementFactory(typeof(Grid));
        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        grid.AppendChild(c0); grid.AppendChild(c1);

        var txt = new FrameworkElementFactory(typeof(TextBlock));
        txt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Content") {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        txt.SetValue(TextBlock.ForegroundProperty, white);
        txt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        txt.SetValue(TextBlock.FontSizeProperty, 11.0);
        txt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        txt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        txt.SetValue(Grid.ColumnProperty, 0);
        grid.AppendChild(txt);

        var arrowStack = new FrameworkElementFactory(typeof(StackPanel));
        arrowStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        arrowStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowStack.SetValue(StackPanel.MarginProperty, new Thickness(4, 0, 4, 0));
        arrowStack.SetValue(Grid.ColumnProperty, 1);

        var pAsc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        pAsc.Name = "SortAsc";
        pAsc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,4 L 4,0 L 8,4 Z"));
        pAsc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
        pAsc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 0, 1));
        pAsc.SetValue(VisibilityProperty, Visibility.Collapsed);

        var pDesc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        pDesc.Name = "SortDesc";
        pDesc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
        pDesc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
        pDesc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 1, 0, 0));
        pDesc.SetValue(VisibilityProperty, Visibility.Collapsed);

        arrowStack.AppendChild(pAsc); arrowStack.AppendChild(pDesc);
        grid.AppendChild(arrowStack);
        outerBorder.AppendChild(grid);
        ct.VisualTree = outerBorder;

        var tAsc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Ascending };
        tAsc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortAsc"));
        ct.Triggers.Add(tAsc);
        var tDesc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Descending };
        tDesc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortDesc"));
        ct.Triggers.Add(tDesc);
        var tHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        tHover.Setters.Add(new Setter(Border.BackgroundProperty, purpleDark, "OuterBorder"));
        ct.Triggers.Add(tHover);

        var style = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.TemplateProperty, ct));
        return style;
    }

    private async Task CargarTodos()
    {
        try
        {
            using var conn = _db.Create();
            var raw = await conn.QueryAsync<FilaPromoRaw>(@"
                SELECT L.NOMBRE AS Local,
                       CAST(A.CA AS NVARCHAR(50)) AS Codigo,
                       A.D AS Articulo,
                       CONVERT(VARCHAR(10), P.INICIO, 103) AS Inicio,
                       CONVERT(VARCHAR(10), P.FIN,    103) AS Fin,
                       P.PPROMO AS PPromo
                FROM LOCALES L
                INNER JOIN PRICES   P ON L.ID_LOCAL = P.IDLOCAL
                INNER JOIN ARTICULOS A ON P.IDART    = A.ID
                WHERE P.PR = 1 AND P.DELETADO = 0
                ORDER BY L.ID_LOCAL, A.D");

            var hoy = DateTime.Today;
            _todos = raw.Select(r => {
                DateTime.TryParse(r.Fin,   out var finDate);
                DateTime.TryParse(r.Inicio, out var iniDate);
                bool vigente = iniDate <= hoy && hoy <= finDate;
                return new FilaPromo {
                    Local       = r.Local,
                    Codigo      = r.Codigo,
                    Articulo    = r.Articulo,
                    Inicio      = r.Inicio,
                    Fin         = r.Fin,
                    PPromo      = r.PPromo,
                    Vigente     = vigente,
                    EstadoTexto = vigente ? "Vigente" : (hoy > finDate ? "Vencida" : "Futura"),
                };
            }).ToList();

            AplicarFiltro();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AplicarFiltro()
    {
        var buscar = _txtBuscar?.Text.Trim() ?? "";
        var lista  = _todos.AsEnumerable();

        if (_idLocalFiltro.HasValue)
            lista = lista.Where(r => r.Local.Equals(_txtLocal.Text, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(buscar))
            lista = lista.Where(r =>
                r.Articulo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                r.Codigo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                r.Local.Contains(buscar, StringComparison.OrdinalIgnoreCase));

        if (_filtroEstado != null)
            lista = lista.Where(r => r.EstadoTexto == _filtroEstado);

        var result   = lista.ToList();
        _grid.ItemsSource = result;

        // KPIs sobre el total sin filtro de estado (base = solo texto + local)
        var baseList = _todos.AsEnumerable();
        if (_idLocalFiltro.HasValue)
            baseList = baseList.Where(r => r.Local.Equals(_txtLocal.Text, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(buscar))
            baseList = baseList.Where(r =>
                r.Articulo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                r.Codigo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                r.Local.Contains(buscar, StringComparison.OrdinalIgnoreCase));
        var baseResult = baseList.ToList();

        int vigentes = baseResult.Count(r => r.EstadoTexto == "Vigente");
        int vencidas = baseResult.Count(r => r.EstadoTexto == "Vencida");
        int futuras  = baseResult.Count(r => r.EstadoTexto == "Futura");

        if (_kpiVigentes != null) { _kpiVigentes.Text = vigentes.ToString(); }
        if (_kpiVencidas != null) { _kpiVencidas.Text = vencidas.ToString(); }
        if (_kpiFuturas  != null) { _kpiFuturas.Text  = futuras.ToString();  }

        _lblConteo.Text = $"{result.Count} mostrado(s)";
        _lblTotal.Text  = $"Mostrando: {result.Count}   |   Vigentes: {vigentes}   |   " +
                          $"Vencidas: {vencidas}   |   Futuras: {futuras}   |   " +
                          $"Locales: {baseResult.Select(r => r.Local).Distinct().Count()}";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5)    _ = CargarTodos();
    }
}

internal class FilaPromoRaw
{
    public string  Local    { get; set; } = "";
    public string  Codigo   { get; set; } = "";
    public string  Articulo { get; set; } = "";
    public string  Inicio   { get; set; } = "";
    public string  Fin      { get; set; } = "";
    public decimal PPromo   { get; set; }
}

internal class FilaPromo
{
    public string  Local       { get; set; } = "";
    public string  Codigo      { get; set; } = "";
    public string  Articulo    { get; set; } = "";
    public string  Inicio      { get; set; } = "";
    public string  Fin         { get; set; } = "";
    public decimal PPromo      { get; set; }
    public bool    Vigente     { get; set; }
    public string  EstadoTexto { get; set; } = "";

    public System.Windows.Media.SolidColorBrush BadgeBg => EstadoTexto switch {
        "Vigente" => new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32")),
        "Futura"  => new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0")),
        _         => new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#757575")),
    };
}

// ══════════════════════════════════════════════════════════════════════════════
//  VISOR PROMOCIÓN — rediseño moderno (local actual del usuario)
// ══════════════════════════════════════════════════════════════════════════════
public class VisorPromoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid  _grid        = null!;
    private TextBox   _txtBuscar   = null!;
    private TextBlock _lblFooter   = null!;
    private TextBlock _kpiTotal    = null!;
    private TextBlock _kpiVigente  = null!;
    private TextBlock _kpiVencida  = null!;
    private TextBlock _kpiFutura   = null!;
    private Border    _chipTodos   = null!;
    private Border    _chipVig     = null!;
    private Border    _chipVenc    = null!;
    private Border    _chipFut     = null!;
    private string?   _filtroEstado = null;  // null=todos
    private List<FilaPromo> _todos = new();

    private static System.Windows.Media.SolidColorBrush VBr(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    public VisorPromoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title  = "Visor de Promociones";
        Width  = 1020; Height = 620;
        MinWidth = 800; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = VBr("#F0F4F8");
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var local = SessionService.Instance.LocalActual;
        var localNombre = local?.NombreLocal ?? "Local actual";

        var root = new DockPanel();

        // ── HEADER ── paleta corporativa #0E2F44 ───────────────────────────
        var hdr = new Border {
            Background = VBr("#0E2F44"),
            Padding    = new Thickness(18, 14, 18, 14) };
        DockPanel.SetDock(hdr, Dock.Top);

        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // título
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // KPIs
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // botones

        // título
        var titSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titSp.Children.Add(new TextBlock {
            Text = "VISOR DE PROMOCIONES",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold });
        titSp.Children.Add(new TextBlock {
            Text = $"📍 {localNombre}",
            Foreground = VBr("#7FB3D3"), FontSize = 11, Margin = new Thickness(0,3,0,0) });
        Grid.SetColumn(titSp, 0); hdrG.Children.Add(titSp);

        // KPI chips
        Border KpiChip(string label, out TextBlock valTb) {
            var sp = new StackPanel { Margin = new Thickness(0,0,10,0),
                HorizontalAlignment = HorizontalAlignment.Center };
            valTb = new TextBlock { Text = "—", FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                TextAlignment = TextAlignment.Center };
            sp.Children.Add(valTb);
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = VBr("#7FB3D3"),
                TextAlignment = TextAlignment.Center });
            return new Border { Background = VBr("#1A4F6E"), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14,7,14,7), Margin = new Thickness(0,0,8,0),
                VerticalAlignment = VerticalAlignment.Center, Child = sp }; }

        var kpiSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        kpiSp.Children.Add(KpiChip("TOTAL",    out _kpiTotal));
        kpiSp.Children.Add(KpiChip("VIGENTES", out _kpiVigente));
        kpiSp.Children.Add(KpiChip("VENCIDAS", out _kpiVencida));
        kpiSp.Children.Add(KpiChip("FUTURAS",  out _kpiFutura));
        Grid.SetColumn(kpiSp, 1); hdrG.Children.Add(kpiSp);

        // botones header
        Button MkBtn(string t, string bg) => new Button {
            Content = t, Height = 32, Padding = new Thickness(14,0,14,0),
            Background = VBr(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,6,0) };

        var btnSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        var btnPrev  = MkBtn("👁  Vista previa", "#1F6089");
        var btnPrint = MkBtn("🖨  Imprimir",     "#1A4F6E");
        var btnClose = MkBtn("✕  Cerrar",        "#37474F");
        btnPrev .Click += (_, _) => ImprimirVisor(preview: true);
        btnPrint.Click += (_, _) => ImprimirVisor(preview: false);
        btnClose.Click += (_, _) => Close();
        btnSp.Children.Add(btnPrev);
        btnSp.Children.Add(btnPrint);
        btnSp.Children.Add(btnClose);
        Grid.SetColumn(btnSp, 2); hdrG.Children.Add(btnSp);

        hdr.Child = hdrG;
        root.Children.Add(hdr);

        // ── BARRA FILTROS ── #1A4F6E ───────────────────────────────────────
        var fBar = new Border {
            Background = VBr("#1A4F6E"),
            Padding    = new Thickness(16, 9, 16, 9) };
        DockPanel.SetDock(fBar, Dock.Top);

        var fSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        // ícono lupa
        fSp.Children.Add(new TextBlock { Text = "🔍", FontSize = 14,
            Foreground = VBr("#7FB3D3"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,7,0) });

        // search box estilizado
        var searchBorder = new Border {
            Background = VBr("#EEF2F6"), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(0), Margin = new Thickness(0,0,14,0),
            VerticalAlignment = VerticalAlignment.Center };
        _txtBuscar = new TextBox {
            Width = 260, Padding = new Thickness(8,5,8,5),
            FontSize = 12, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center };
        _txtBuscar.TextChanged += (_, _) => AplicarFiltroVisor();
        searchBorder.Child = _txtBuscar;
        fSp.Children.Add(searchBorder);

        fSp.Children.Add(new Border { Width = 1, Background = VBr("#2A7AB5"),
            Margin = new Thickness(0,4,12,4), VerticalAlignment = VerticalAlignment.Stretch });

        // botón refrescar
        var btnRef = new Button {
            Content = "↻  Refrescar", Height = 30, Padding = new Thickness(12,0,12,0),
            Background = VBr("#1F6089"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center };
        btnRef.Click += async (_, _) => { _txtBuscar.Text = ""; await Cargar(); };
        fSp.Children.Add(btnRef);

        fBar.Child = fSp;
        root.Children.Add(fBar);

        // ── CHIPS DE FILTRO ESTADO ── #0E2F44 ──────────────────────────────
        var chipBar = new Border {
            Background = VBr("#0E2F44"),
            Padding    = new Thickness(16, 7, 16, 7) };
        DockPanel.SetDock(chipBar, Dock.Top);

        var chipSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        Border MkChip(string label) {
            var tb = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14,5,14,5), Margin = new Thickness(0,0,8,0),
                Cursor = Cursors.Hand, Child = tb, Opacity = 0.55 };
            return b; }

        _chipTodos = MkChip("Todos");
        _chipVig   = MkChip("✓  Vigente");
        _chipVenc  = MkChip("✕  Vencida");
        _chipFut   = MkChip("⏳  Futura");

        void SetFiltro(string? estado) {
            _filtroEstado = estado;
            ActualizarChips();
            AplicarFiltroVisor(); }

        _chipTodos.MouseLeftButtonUp += (_, _) => SetFiltro(null);
        _chipVig  .MouseLeftButtonUp += (_, _) => SetFiltro("Vigente");
        _chipVenc .MouseLeftButtonUp += (_, _) => SetFiltro("Vencida");
        _chipFut  .MouseLeftButtonUp += (_, _) => SetFiltro("Futura");

        chipSp.Children.Add(_chipTodos);
        chipSp.Children.Add(_chipVig);
        chipSp.Children.Add(_chipVenc);
        chipSp.Children.Add(_chipFut);
        chipSp.Children.Add(new TextBlock { Text = "  Clic en un estado para filtrar",
            Foreground = VBr("#4A7CA0"), FontSize = 10, FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center });

        chipBar.Child = chipSp;
        root.Children.Add(chipBar);

        // ── FOOTER ─────────────────────────────────────────────────────────
        var footer = new Border {
            Background = VBr("#0E2F44"),
            Padding    = new Thickness(16, 8, 16, 8) };
        DockPanel.SetDock(footer, Dock.Bottom);
        _lblFooter = new TextBlock {
            Foreground = VBr("#7FB3D3"), FontSize = 11, FontWeight = FontWeights.SemiBold };
        footer.Child = _lblFooter;
        root.Children.Add(footer);

        // ── GRILLA ─────────────────────────────────────────────────────────
        var colHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdr.Setters.Add(new Setter(Control.BackgroundProperty, VBr("#1A4F6E")));
        colHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdr.Setters.Add(new Setter(Control.FontSizeProperty,   11.0));
        colHdr.Setters.Add(new Setter(Control.PaddingProperty,    new Thickness(10,8,10,8)));
        colHdr.Setters.Add(new Setter(Control.BorderBrushProperty, VBr("#0E2F44")));
        colHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,2)));

        // Row colors por estado
        var rowStyle = new Style(typeof(DataGridRow));
        var dtVig = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vigente" };
        dtVig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, VBr("#E8F5E9")));
        dtVig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, VBr("#1B5E20")));
        var dtVenc2 = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vencida" };
        dtVenc2.Setters.Add(new Setter(DataGridRow.ForegroundProperty, VBr("#757575")));
        var dtFut = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Futura" };
        dtFut.Setters.Add(new Setter(DataGridRow.BackgroundProperty, VBr("#E3F2FD")));
        dtFut.Setters.Add(new Setter(DataGridRow.ForegroundProperty, VBr("#0D47A1")));
        rowStyle.Triggers.Add(dtVig);
        rowStyle.Triggers.Add(dtVenc2);
        rowStyle.Triggers.Add(dtFut);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 34,
            FontSize = 12, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = VBr("#F7F9FC"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = VBr("#DEE6EF"),
            ColumnHeaderStyle = colHdr, RowStyle = rowStyle,
            CanUserSortColumns = true,
            SelectionMode = DataGridSelectionMode.Single,
            EnableRowVirtualization = true };
        VirtualizingPanel.SetVirtualizationMode(_grid, VirtualizationMode.Recycling);

        DataGridTextColumn TC(string h, string p, double w, bool star = false, string? fmt = null, bool right = false) {
            var c = new DataGridTextColumn {
                Header  = h,
                Width   = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w),
                Binding = fmt != null
                    ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                    : new System.Windows.Data.Binding(p) };
            if (right) {
                var cs = new Style(typeof(DataGridCell));
                cs.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                c.CellStyle = cs; }
            return c; }

        // columna Estado — CellStyle con DataTrigger a nivel de celda (más confiable que template anidado)
        var cellStyleEstado = new Style(typeof(DataGridCell));
        // base: centrado, sin borde de selección
        cellStyleEstado.Setters.Add(new Setter(DataGridCell.VerticalAlignmentProperty,   VerticalAlignment.Stretch));
        cellStyleEstado.Setters.Add(new Setter(DataGridCell.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        // Vigente → verde
        var cVig = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vigente" };
        cVig.Setters.Add(new Setter(DataGridCell.BackgroundProperty, VBr("#2E7D32")));
        cVig.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.White));
        // Vencida → gris medio
        var cVenc = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Vencida" };
        cVenc.Setters.Add(new Setter(DataGridCell.BackgroundProperty, VBr("#78909C")));
        cVenc.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.White));
        // Futura → azul
        var cFut = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoTexto"), Value = "Futura" };
        cFut.Setters.Add(new Setter(DataGridCell.BackgroundProperty, VBr("#1565C0")));
        cFut.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.White));
        cellStyleEstado.Triggers.Add(cVig);
        cellStyleEstado.Triggers.Add(cVenc);
        cellStyleEstado.Triggers.Add(cFut);

        var estadoCol = new DataGridTextColumn {
            Header      = "Estado",
            Width       = 100,
            Binding     = new System.Windows.Data.Binding("EstadoTexto"),
            HeaderStyle = colHdr,
            CellStyle   = cellStyleEstado,
            IsReadOnly  = true };
        // centrar texto dentro de la celda
        var elStyle = new Style(typeof(TextBlock));
        elStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,    TextAlignment.Center));
        elStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center));
        elStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty,       FontWeights.Bold));
        elStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty,         11.0));
        estadoCol.ElementStyle = elStyle;

        _grid.Columns.Add(TC("Código",        "Codigo",   90));
        _grid.Columns.Add(TC("Artículo",       "Articulo",  0, star: true));
        _grid.Columns.Add(TC("Inicio",         "Inicio",  100));
        _grid.Columns.Add(TC("Fin",            "Fin",     100));
        _grid.Columns.Add(TC("P. Promo Gs.",   "PPromo",  130, fmt: "N0", right: true));
        _grid.Columns.Add(estadoCol);

        root.Children.Add(_grid);
        Content = root;
        ActualizarChips();
    }

    private void ActualizarChips()
    {
        void Set(Border chip, bool activo) {
            chip.Opacity     = activo ? 1.0 : 0.5;
            chip.Background  = activo ? VBr("#2A7AB5") : VBr("#1A4F6E");
            chip.BorderBrush = activo ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
            chip.BorderThickness = new Thickness(activo ? 1 : 0); }
        Set(_chipTodos, _filtroEstado == null);
        Set(_chipVig,   _filtroEstado == "Vigente");
        Set(_chipVenc,  _filtroEstado == "Vencida");
        Set(_chipFut,   _filtroEstado == "Futura");
    }

    private async Task Cargar()
    {
        var local = SessionService.Instance.LocalActual;
        if (local == null) { MessageBox.Show("No hay local seleccionado."); return; }
        try
        {
            using var conn = _db.Create();
            var raw = await conn.QueryAsync<FilaPromoRaw>(@"
                SELECT L.NOMBRE AS Local,
                       CAST(A.CA AS NVARCHAR(50)) AS Codigo,
                       A.D AS Articulo,
                       CONVERT(VARCHAR(10), P.INICIO, 103) AS Inicio,
                       CONVERT(VARCHAR(10), P.FIN,    103) AS Fin,
                       P.PPROMO AS PPromo
                FROM LOCALES L
                INNER JOIN PRICES   P ON L.ID_LOCAL = P.IDLOCAL
                INNER JOIN ARTICULOS A ON P.IDART    = A.ID
                WHERE P.PR = 1 AND P.DELETADO = 0 AND L.ID_LOCAL = @idlocal
                ORDER BY A.D", new { idlocal = local.IdLocal });

            var hoy = DateTime.Today;
            _todos = raw.Select(r => {
                DateTime.TryParse(r.Fin,    out var finDate);
                DateTime.TryParse(r.Inicio, out var iniDate);
                bool vigente = iniDate <= hoy && hoy <= finDate;
                return new FilaPromo {
                    Local       = local.NombreLocal ?? "",
                    Codigo      = r.Codigo,
                    Articulo    = r.Articulo,
                    Inicio      = r.Inicio,
                    Fin         = r.Fin,
                    PPromo      = r.PPromo,
                    Vigente     = vigente,
                    EstadoTexto = vigente ? "Vigente" : (hoy > finDate ? "Vencida" : "Futura"),
                };
            }).ToList();

            AplicarFiltroVisor();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImprimirVisor(bool preview = false)
    {
        var filas = (_grid.ItemsSource as System.Collections.Generic.IEnumerable<FilaPromo>)?.ToList();
        if (filas == null || filas.Count == 0)
        {
            MessageBox.Show("No hay datos para imprimir.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var local = SessionService.Instance.LocalActual;
        int vigentes = filas.Count(r => r.Vigente);
        int vencidas = filas.Count(r => !r.Vigente && r.EstadoTexto == "Vencida");
        int futuras  = filas.Count(r => r.EstadoTexto == "Futura");

        var p = new EnPromoPagina
        {
            Filas       = filas.Select(f => new FilaEnPromoImp(
                              f.Local, f.Codigo, f.Articulo,
                              f.Inicio, f.Fin, f.PPromo, f.EstadoTexto)).ToList(),
            FechaImp    = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario     = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            LogoPath    = EnPromoPagina.ResolverLogoPath(),
            LocalFiltro = local?.NombreLocal ?? "Local actual",
            Vigentes    = vigentes,
            Vencidas    = vencidas,
            Futuras     = futuras,
        };

        if (preview)
            new EnPromocionPreviewWindow(p) { Owner = this }.ShowDialog();
        else
            EnPromocionImpresora.Imprimir(p, this);
    }

    private void AplicarFiltroVisor()
    {
        var buscar = _txtBuscar?.Text.Trim() ?? "";
        IEnumerable<FilaPromo> lista = _todos;

        if (_filtroEstado != null)
            lista = lista.Where(r => r.EstadoTexto == _filtroEstado);
        if (!string.IsNullOrEmpty(buscar))
            lista = lista.Where(r =>
                r.Articulo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                r.Codigo.Contains(buscar, StringComparison.OrdinalIgnoreCase));

        var result = lista.ToList();
        _grid.ItemsSource = result;

        int vigentes = _todos.Count(r => r.EstadoTexto == "Vigente");
        int vencidas = _todos.Count(r => r.EstadoTexto == "Vencida");
        int futuras  = _todos.Count(r => r.EstadoTexto == "Futura");

        if (_kpiTotal   != null) _kpiTotal  .Text = _todos.Count.ToString("N0");
        if (_kpiVigente != null) _kpiVigente.Text = vigentes.ToString("N0");
        if (_kpiVencida != null) _kpiVencida.Text = vencidas.ToString("N0");
        if (_kpiFutura  != null) _kpiFutura .Text = futuras .ToString("N0");

        if (_lblFooter != null)
            _lblFooter.Text = $"Mostrando {result.Count} de {_todos.Count} artículos  ·  " +
                              $"Vigentes: {vigentes}  ·  Vencidas: {vencidas}  ·  Futuras: {futuras}";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5)    _ = Cargar();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE COMPRAS
// ══════════════════════════════════════════════════════════════════════════════
// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE COMPRAS  — rediseño completo
// ══════════════════════════════════════════════════════════════════════════════
public class HComprasWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Grillas
    private DataGrid _gridCabs = null!;   // cabeceras (una fila por factura)
    private DataGrid _gridDets = null!;   // detalle artículos de la cab seleccionada

    // Filtros
    private DatePicker  _dpDesde = null!, _dpHasta = null!;
    private RadioButton _rbPeriodo = null!, _rbTodos = null!;
    private Button      _btnMesActual = null!;
    private TextBox     _txtProv = null!, _txtFactura = null!;
    private Button      _btnQuitarProv = null!;
    private int?        _idProvFiltro = null;

    // Totales / conteos
    private TextBlock _lblConteo = null!, _lblTotal = null!, _lblDetHdr = null!;

    // Datos cargados
    private List<FilaCompra> _todas = new();

    private static System.Windows.Media.SolidColorBrush Br(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    public HComprasWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title    = "Historial de Compras";
        Width    = 1020; Height = 650;
        MinWidth = 860; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Br("#FAF9F8");
        BuildUI();
        Loaded += async (_, _) => await Buscar();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // cuerpo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // totales

        // ── HEADER ─────────────────────────────────────────────────────────
        var hdr = new Border { Background = Br("#1565C0"), Padding = new Thickness(16, 10, 16, 10) };
        Grid.SetRow(hdr, 0);
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.Children.Add(new TextBlock { Text = "🛒  Historial de Compras",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center });

        // Botones acción en header
        Button MkHBtn(string txt, string bg) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            Background = Br(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0) };
        var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(hdrBtns, 2);
        var btnVista = MkHBtn("👁 Vista previa", "#6A1B9A");
        btnVista.Click += (_, _) => ImprimirCompras(preview: true);
        hdrBtns.Children.Add(btnVista);
        var btnImprimir = MkHBtn("🖨 Imprimir", "#1B5E20");
        btnImprimir.Click += (_, _) => ImprimirCompras(preview: false);
        hdrBtns.Children.Add(btnImprimir);
        hdrG.Children.Add(hdrBtns);

        _lblConteo = new TextBlock { Foreground = Br("#90CAF9"), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 0, 0) };
        Grid.SetColumn(_lblConteo, 3);
        hdrG.Children.Add(_lblConteo);
        hdr.Child = hdrG;
        root.Children.Add(hdr);

        // ── FILTROS ─────────────────────────────────────────────────────────
        var fBorder = new Border { Background = Br("#1976D2"), Padding = new Thickness(14, 8, 14, 8) };
        Grid.SetRow(fBorder, 1);

        Button MkBtn(string t, string bg, int h = 30) => new Button {
            Content = t, Height = h, Padding = new Thickness(12, 0, 12, 0),
            Background = Br(bg), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        TextBlock Lbl(string t) => new TextBlock { Text = t,
            Foreground = Br("#BBDEFB"), FontWeight = FontWeights.SemiBold, FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
        Border Sep() => new Border { Width = 1, Background = Br("#42A5F5"), Margin = new Thickness(10, 0, 10, 0) };

        var fp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Radios período
        _rbTodos   = new RadioButton { Content = "Todos",    GroupName = "CompPer",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center };
        _rbPeriodo = new RadioButton { Content = "Período:", GroupName = "CompPer", IsChecked = true,
            Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center };
        _dpDesde = new DatePicker { Width = 112, SelectedDate = DateTime.Today.AddMonths(-1),
            VerticalAlignment = VerticalAlignment.Center };
        _dpHasta = new DatePicker { Width = 112, SelectedDate = DateTime.Today,
            VerticalAlignment = VerticalAlignment.Center };
        _btnMesActual = MkBtn("📅 Mes actual", "#0D47A1");
        _btnMesActual.Margin = new Thickness(6, 0, 0, 0);

        // Suscribir eventos DESPUÉS de crear todos los controles
        _rbTodos.Checked   += async (_, _) => { _dpDesde.IsEnabled = false; _dpHasta.IsEnabled = false; _btnMesActual.IsEnabled = false; await Buscar(); };
        _rbPeriodo.Checked += async (_, _) => { _dpDesde.IsEnabled = true;  _dpHasta.IsEnabled = true;  _btnMesActual.IsEnabled = true;  await Buscar(); };
        _dpDesde.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
        _dpHasta.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
        _btnMesActual.Click += (_, _) => {
            _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _dpHasta.SelectedDate = DateTime.Today;
        };

        var brdTodos = new Border { Background = Br("#0D47A1"), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
        brdTodos.Child = _rbTodos;
        var brdPer = new Border { Background = Br("#0D47A1"), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
        brdPer.Child = _rbPeriodo;

        fp.Children.Add(brdTodos);
        fp.Children.Add(brdPer);
        fp.Children.Add(_dpDesde);
        fp.Children.Add(Lbl("  →"));
        fp.Children.Add(_dpHasta);
        fp.Children.Add(_btnMesActual);
        fp.Children.Add(Sep());

        // Proveedor — botón selector
        fp.Children.Add(Lbl("🏭 Proveedor:"));
        _txtProv = new TextBox { Width = 190, Padding = new Thickness(5, 3, 5, 3),
            IsReadOnly = true, Cursor = Cursors.Arrow,
            Background = Br("#E3F2FD"), Foreground = Br("#0D47A1"),
            FontStyle = FontStyles.Italic, FontSize = 11,
            Text = "(todos)", VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = Br("#90CAF9") };
        var btnProv = MkBtn("🏭 Seleccionar", "#0D47A1");
        btnProv.Margin = new Thickness(4, 0, 0, 0);
        btnProv.Click += (_, _) => {
            var modal = new BuscadorProveedorModal(_db) { Owner = this };
            if (modal.ShowDialog() == true && modal.ProveedorSeleccionado != null) {
                _idProvFiltro  = modal.ProveedorSeleccionado.IdProveedor;
                _txtProv.Text  = modal.ProveedorSeleccionado.Nombre;
                _txtProv.FontStyle = FontStyles.Normal;
                _btnQuitarProv.Visibility = Visibility.Visible;
                _ = Buscar();
            }
        };
        _btnQuitarProv = MkBtn("✕ Todos", "#546E7A");
        _btnQuitarProv.Margin = new Thickness(4, 0, 0, 0);
        _btnQuitarProv.Visibility = Visibility.Collapsed;
        _btnQuitarProv.Click += async (_, _) => {
            _idProvFiltro  = null;
            _txtProv.Text  = "(todos)";
            _txtProv.FontStyle = FontStyles.Italic;
            _btnQuitarProv.Visibility = Visibility.Collapsed;
            await Buscar();
        };
        fp.Children.Add(_txtProv);
        fp.Children.Add(btnProv);
        fp.Children.Add(_btnQuitarProv);
        fp.Children.Add(Sep());

        // Factura / comprobante
        fp.Children.Add(Lbl("📄 Factura:"));
        _txtFactura = new TextBox { Width = 130, Padding = new Thickness(5, 3, 5, 3),
            VerticalAlignment = VerticalAlignment.Center };
        _txtFactura.TextChanged += async (_, _) => await Buscar();
        fp.Children.Add(_txtFactura);
        fp.Children.Add(Sep());

        var btnBuscar = MkBtn("🔍 Buscar", "#0D47A1", 32);
        btnBuscar.FontWeight = FontWeights.Bold; btnBuscar.FontSize = 12;
        btnBuscar.Click += async (_, _) => await Buscar();
        fp.Children.Add(btnBuscar);
        var btnCerrar = MkBtn("✕ Cerrar", "#546E7A", 32);
        btnCerrar.Margin = new Thickness(6, 0, 0, 0); btnCerrar.FontSize = 12;
        btnCerrar.Click += (_, _) => Close();
        fp.Children.Add(btnCerrar);

        fBorder.Child = fp;
        root.Children.Add(fBorder);

        // ── CUERPO: grilla cabeceras + panel detalle derecho ─────────────
        var body = new Grid { Margin = new Thickness(8, 6, 8, 6) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        Grid.SetRow(body, 2);

        // ─ GRILLA CABECERAS ──────────────────────────────────────────────
        var cabOuter = new Border { BorderBrush = Br("#E0E0E0"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(cabOuter, 0);

        var cabPanel = new Grid();
        cabPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cabPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var cabHdr = new Border { Background = Br("#1976D2"), Padding = new Thickness(12, 6, 12, 6) };
        var cabHdrG = new Grid();
        cabHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cabHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cabHdrG.Children.Add(new TextBlock { Text = "📦  Órdenes de compra",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        var hint = new TextBlock { Text = "← seleccionar para ver artículos",
            Foreground = Br("#90CAF9"), FontSize = 10, FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(hint, 1); cabHdrG.Children.Add(hint);
        cabHdr.Child = cabHdrG;
        Grid.SetRow(cabHdr, 0); cabPanel.Children.Add(cabHdr);

        // Header style azul con flechas sort
        var colHdrStyle = BuildBlueHeaderStyle();

        var cabRowStyle = new Style(typeof(DataGridRow));

        _gridCabs = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 32,
            CanUserSortColumns = true,
            AlternatingRowBackground = Br("#F3F8FF"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = Br("#DDEEFF"),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = colHdrStyle,
            SelectionMode = DataGridSelectionMode.Single };
        _gridCabs.SelectionChanged += OnCabSeleccionada;
        Grid.SetRow(_gridCabs, 1);

        DataGridTextColumn CC(string h, string p, double w, string? fmt = null) =>
            new() { Header = h, Width = w, MinWidth = 40, SortMemberPath = p,
                Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                      : new System.Windows.Data.Binding(p) };

        _gridCabs.Columns.Add(CC("Fecha",      "Fecha",        110));
        _gridCabs.Columns.Add(new DataGridTextColumn { Header = "Proveedor", SortMemberPath = "Proveedor",
            Binding = new System.Windows.Data.Binding("Proveedor"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 130 });
        _gridCabs.Columns.Add(CC("Factura",    "Factura",       110));
        _gridCabs.Columns.Add(CC("Interno",    "Interno",        95));
        _gridCabs.Columns.Add(CC("Local",      "Local",          80));
        _gridCabs.Columns.Add(CC("Items",      "Items",          52));
        _gridCabs.Columns.Add(CC("Total Gs.",  "Total",         120, "N0"));

        cabPanel.Children.Add(_gridCabs);
        cabOuter.Child = cabPanel;
        body.Children.Add(cabOuter);

        // ─ PANEL DETALLE DERECHO ────────────────────────────────────────
        var detOuter = new Border { BorderBrush = Br("#E0E0E0"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true };
        Grid.SetColumn(detOuter, 1);

        var detDock = new DockPanel { LastChildFill = true };

        // Header detalle azul oscuro
        var detHdrBorder = new Border { Background = Br("#0D47A1"), Padding = new Thickness(12, 8, 12, 8) };
        DockPanel.SetDock(detHdrBorder, Dock.Top);
        _lblDetHdr = new TextBlock { Text = "📄  Artículos de la compra",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
        detHdrBorder.Child = _lblDetHdr;
        detDock.Children.Add(detHdrBorder);

        // Grilla de artículos — fill
        var artHdr = new Border { Background = Br("#1565C0"), Padding = new Thickness(12, 6, 12, 6) };
        DockPanel.SetDock(artHdr, Dock.Top);
        artHdr.Child = new TextBlock { Text = "📋  Artículos",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 };
        detDock.Children.Add(artHdr);

        var artColHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        artColHdr.Setters.Add(new Setter(Control.BackgroundProperty, Br("#1565C0")));
        artColHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        artColHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        artColHdr.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
        artColHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));

        _gridDets = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 30,
            FontSize = 11, BorderThickness = new Thickness(0),
            AlternatingRowBackground = Br("#EBF3FF"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = Br("#C5D8F0"),
            ColumnHeaderStyle = artColHdr,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _gridDets.Columns.Add(new DataGridTextColumn { Header = "Código",
            Binding = new System.Windows.Data.Binding("Codigo"), Width = 70 });
        _gridDets.Columns.Add(new DataGridTextColumn { Header = "Descripción",
            Binding = new System.Windows.Data.Binding("Descripcion"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDets.Columns.Add(new DataGridTextColumn { Header = "Cant",
            Binding = new System.Windows.Data.Binding("Cantidad") { StringFormat = "N0" }, Width = 52 });
        _gridDets.Columns.Add(new DataGridTextColumn { Header = "P.Costo",
            Binding = new System.Windows.Data.Binding("PC") { StringFormat = "N0" }, Width = 90 });
        _gridDets.Columns.Add(new DataGridTextColumn { Header = "Subtotal",
            Binding = new System.Windows.Data.Binding("Subtotal") { StringFormat = "N0" }, Width = 100 });
        detDock.Children.Add(_gridDets);

        detOuter.Child = detDock;
        body.Children.Add(detOuter);
        root.Children.Add(body);

        // ── BARRA TOTALES ───────────────────────────────────────────────
        var totBar = new Border { Background = Br("#263238"), Padding = new Thickness(14, 8, 14, 8) };
        Grid.SetRow(totBar, 3);
        _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12, Foreground = Br("#4FC3F7") };
        totBar.Child = _lblTotal;
        root.Children.Add(totBar);

        Content = root;
    }

    private static Style BuildBlueHeaderStyle()
    {
        var blue     = Br("#1976D2");
        var blueDark = Br("#1565C0");
        var white    = System.Windows.Media.Brushes.White;

        var ct = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        var outerBorder = new FrameworkElementFactory(typeof(Border));
        outerBorder.Name = "OuterBorder";
        outerBorder.SetValue(Border.BackgroundProperty, blue);
        outerBorder.SetValue(Border.BorderBrushProperty, blueDark);
        outerBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 2));
        outerBorder.SetValue(Border.PaddingProperty, new Thickness(8, 6, 4, 6));

        var grid = new FrameworkElementFactory(typeof(Grid));
        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var c2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c2.SetValue(ColumnDefinition.WidthProperty, new GridLength(6));
        grid.AppendChild(c0); grid.AppendChild(c1); grid.AppendChild(c2);

        var txt = new FrameworkElementFactory(typeof(TextBlock));
        txt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Content") {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        txt.SetValue(TextBlock.ForegroundProperty, white);
        txt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        txt.SetValue(TextBlock.FontSizeProperty, 11.0);
        txt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        txt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        txt.SetValue(Grid.ColumnProperty, 0);
        grid.AppendChild(txt);

        var arrowStack = new FrameworkElementFactory(typeof(StackPanel));
        arrowStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        arrowStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowStack.SetValue(StackPanel.MarginProperty, new Thickness(4, 0, 4, 0));
        arrowStack.SetValue(Grid.ColumnProperty, 1);

        var pAsc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        pAsc.Name = "SortAsc";
        pAsc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,4 L 4,0 L 8,4 Z"));
        pAsc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
        pAsc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 0, 1));
        pAsc.SetValue(VisibilityProperty, Visibility.Collapsed);

        var pDesc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        pDesc.Name = "SortDesc";
        pDesc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
        pDesc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
        pDesc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 1, 0, 0));
        pDesc.SetValue(VisibilityProperty, Visibility.Collapsed);

        arrowStack.AppendChild(pAsc); arrowStack.AppendChild(pDesc);
        grid.AppendChild(arrowStack);

        var thumb = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Thumb));
        thumb.SetValue(Grid.ColumnProperty, 2);
        thumb.SetValue(FrameworkElement.CursorProperty, Cursors.SizeWE);
        thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        thumb.SetValue(FrameworkElement.WidthProperty, 6.0);
        thumb.SetValue(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        thumb.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        grid.AppendChild(thumb);

        outerBorder.AppendChild(grid);
        ct.VisualTree = outerBorder;

        var tAsc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Ascending };
        tAsc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortAsc"));
        ct.Triggers.Add(tAsc);
        var tDesc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Descending };
        tDesc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortDesc"));
        ct.Triggers.Add(tDesc);
        var tHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        tHover.Setters.Add(new Setter(Border.BackgroundProperty, blueDark, "OuterBorder"));
        ct.Triggers.Add(tHover);

        var style = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.TemplateProperty, ct));
        return style;
    }

    private async Task Buscar()
    {
        if (_dpDesde == null) return;
        try
        {
            bool sinFecha  = _rbTodos?.IsChecked == true;
            var  desde     = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
            var  hasta     = _dpHasta.SelectedDate ?? DateTime.Today;
            var  idProv    = _idProvFiltro;
            var  facFiltro = _txtFactura?.Text.Trim() ?? "";

            var where = new System.Text.StringBuilder("WHERE 1=1");
            if (!sinFecha)
            {
                where.Append(" AND CAST(C.FECHA AS DATE) >= @Desde");
                where.Append(" AND CAST(C.FECHA AS DATE) <= @Hasta");
            }
            if (idProv.HasValue) where.Append(" AND C.IDP = @IdProv");
            if (!string.IsNullOrEmpty(facFiltro)) where.Append(" AND C.FACTURA LIKE @Factura");

            var sql = $@"
                SELECT C.IDCABTMP, C.FACTURA, C.INTERNO,
                       ISNULL(P.NOMBRE_PROVEEDOR, '(sin proveedor)') AS Proveedor,
                       L.NOMBRE AS Local,
                       CONVERT(VARCHAR(16), C.FECHA, 103) AS Fecha,
                       C.TOTAL AS Total,
                       COUNT(D.IDDETTMP) AS Items
                FROM CAB_BUY_TMP C
                LEFT JOIN PROVEEDORES P ON C.IDP   = P.ID_PROVEEDOR
                LEFT JOIN LOCALES     L ON C.IDLOCAL = L.ID_LOCAL
                LEFT JOIN DET_BUY_TMP D ON D.IDCABTMP = C.IDCABTMP
                {where}
                GROUP BY C.IDCABTMP, C.FACTURA, C.INTERNO, P.NOMBRE_PROVEEDOR, L.NOMBRE, C.FECHA, C.TOTAL
                ORDER BY C.FECHA DESC";

            using var conn = _db.Create();
            _todas = (await conn.QueryAsync<FilaCompra>(sql, new {
                Desde   = desde,
                Hasta   = hasta,
                IdProv  = idProv ?? 0,
                Factura = "%" + facFiltro + "%"
            })).ToList();

            _gridCabs.ItemsSource = _todas;
            _gridDets.ItemsSource = null;
            _lblDetHdr.Text       = "📄  Artículos de la compra";

            var totalGs = _todas.Sum(r => r.Total);
            _lblConteo.Text = $"{_todas.Count} compra(s)";
            _lblTotal.Text  = $"Compras: {_todas.Count}   |   " +
                              $"Artículos: {_todas.Sum(r => r.Items)}   |   " +
                              $"Total invertido: Gs. {totalGs:N0}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error al cargar compras"); }
    }

    private void OnCabSeleccionada(object s, SelectionChangedEventArgs e)
    {
        if (_gridCabs.SelectedItem is not FilaCompra cab) { _gridDets.ItemsSource = null; return; }
        _lblDetHdr.Text = $"📄  {cab.Factura}  —  {cab.Proveedor}  —  {cab.Fecha}";
        _ = CargarDetalle(cab.IdCabTmp);
    }

    private async Task CargarDetalle(int idCab)
    {
        try
        {
            using var conn = _db.Create();
            var dets = await conn.QueryAsync<FilaDetCompra>(@"
                SELECT D.CA AS Codigo, D.D AS Descripcion,
                       CAST(D.CANT AS INT) AS Cantidad,
                       D.PC,
                       CAST(D.CANT AS DECIMAL(18,2)) * D.PC AS Subtotal
                FROM DET_BUY_TMP D
                WHERE D.IDCABTMP = @Id
                ORDER BY D.D", new { Id = idCab });
            _gridDets.ItemsSource = dets.ToList();
        }
        catch { _gridDets.ItemsSource = null; }
    }

    private void ImprimirCompras(bool preview)
    {
        var p = BuildPaginaCompras();
        if (p == null) return;
        if (preview)
            new ComprasPreviewWindow(p) { Owner = this }.ShowDialog();
        else
            ComprasImpresora.Imprimir(p, this);
    }

    private ComprasPagina? BuildPaginaCompras()
    {
        if (_todas.Count == 0)
        {
            MessageBox.Show("No hay datos para imprimir.", "Sin datos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        bool sinFecha = _rbTodos?.IsChecked == true;
        return new ComprasPagina
        {
            Filas    = _todas.Select(f => new FilaCompraImp(
                           f.Fecha, f.Proveedor, f.Factura, f.Interno,
                           f.Local, f.Items, f.Total)).ToList(),
            Desde    = sinFecha ? "" : _dpDesde?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
            Hasta    = sinFecha ? "" : _dpHasta?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            LogoPath = ComprasPagina.ResolverLogoPath(),
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5) _ = Buscar();
    }
}

internal class FilaCompra
{
    public int     IdCabTmp  { get; set; }
    public string  Factura   { get; set; } = "";
    public string  Interno   { get; set; } = "";
    public string  Proveedor { get; set; } = "";
    public string  Local     { get; set; } = "";
    public string  Fecha     { get; set; } = "";
    public decimal Total     { get; set; }
    public int     Items     { get; set; }
}

internal class FilaDetCompra
{
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public int     Cantidad    { get; set; }
    public decimal PC          { get; set; }
    public decimal Subtotal    { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE TRANSFERENCIAS
// ══════════════════════════════════════════════════════════════════════════════
public class HTransferenciasWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Header KPIs
    private TextBlock _kpiTotal     = null!;
    private TextBlock _kpiPendiente = null!;
    private TextBlock _kpiAceptado  = null!;
    private TextBlock _kpiAnulado   = null!;

    // Filtros
    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private TextBox    _txtBuscar   = null!;
    private TextBox    _txtLocal    = null!;
    private int?       _idLocalFiltro = null;

    // Chips de estado
    private Border  _chipTodos = null!, _chipPend = null!, _chipAcep = null!, _chipAnul = null!;
    private string? _filtroEstado = null;

    // Grilla
    private DataGrid  _grid = null!;
    private List<FilaTransferencia> _todos = new();

    // Paginación
    private int  _pagina    = 1;
    private int  _porPagina = 50;
    private int  _total     = 0;
    private bool _cargando  = false;
    private TextBlock _lblPagInfo = null!;
    private Button    _btnPrev    = null!, _btnNext = null!;

    private static System.Windows.Media.SolidColorBrush HBr(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public HTransferenciasWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Historial de Transferencias";
        Width = 1020; Height = 650;
        MinWidth = 860; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = HBr("#EEF2F6");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await Buscar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── HEADER con KPIs ──────────────────────────────────────────────────
        var hdr = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(18,12,18,12) };
        DockPanel.SetDock(hdr, Dock.Top);
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,24,0) };
        hdrTxt.Children.Add(new TextBlock { Text = "HISTORIAL DE TRANSFERENCIAS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hdrTxt.Children.Add(new TextBlock { Text = "Remitos entre locales",
            Foreground = HBr("#7FB3D3"), FontSize = 10.5, Margin = new Thickness(0,3,0,0) });
        Grid.SetColumn(hdrTxt, 0); hdrG.Children.Add(hdrTxt);

        Border KpiC(string lbl, out TextBlock tb, string bg = "#1A4F6E") {
            tb = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center };
            var sp = new StackPanel();
            sp.Children.Add(tb);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8.5,
                Foreground = HBr("#7FB3D3"), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,2,0,0) });
            return new Border { Background = HBr(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12,7,12,7), Margin = new Thickness(5,0,5,0), Child = sp }; }

        var kpiRow = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        kpiRow.Children.Add(KpiC("TRANSFERENCIAS",   out _kpiTotal));
        kpiRow.Children.Add(KpiC("PENDIENTES",        out _kpiPendiente, "#E65100"));
        kpiRow.Children.Add(KpiC("ACEPTADAS",         out _kpiAceptado,  "#1B5E20"));
        kpiRow.Children.Add(KpiC("ANULADAS",          out _kpiAnulado,   "#546E7A"));
        Grid.SetColumn(kpiRow, 1); hdrG.Children.Add(kpiRow);

        var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        Button HBtn(string t, string bg) {
            var b = new Button { Content = t, Height = 34,
                Padding = new Thickness(14,0,14,0), Margin = new Thickness(4,0,0,0),
                Background = HBr(bg), Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            return b; }
        var btnPrev = HBtn("👁 Vista previa", "#1F6089");
        btnPrev.Click += (_, _) => ImprimirTransferencias(preview: true);
        var btnImpr = HBtn("🖨 Imprimir", "#1A4F6E");
        btnImpr.Click += (_, _) => ImprimirTransferencias(preview: false);
        var btnCerrar = HBtn("✕ Cerrar", "#546E7A");
        btnCerrar.Click += (_, _) => Close();
        hdrBtns.Children.Add(btnPrev);
        hdrBtns.Children.Add(btnImpr);
        hdrBtns.Children.Add(btnCerrar);
        Grid.SetColumn(hdrBtns, 2); hdrG.Children.Add(hdrBtns);
        hdr.Child = hdrG;
        root.Children.Add(hdr);

        // ── BARRA DE FILTROS ─────────────────────────────────────────────────
        var barra = new Border { Background = HBr("#1A4F6E"), Padding = new Thickness(14,10,14,10) };
        DockPanel.SetDock(barra, Dock.Top);
        var barraG = new Grid { ColumnDefinitions = {
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto },
        }};

        // Búsqueda texto
        _txtBuscar = new TextBox { Height = 32, FontSize = 12,
            Padding = new Thickness(8,0,8,0), VerticalContentAlignment = VerticalAlignment.Center,
            Background = HBr("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            CaretBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0) };
        var busqBorder = new Border { Background = HBr("#0E2F44"), CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0,0,10,0) };
        var busqSp = new StackPanel { Orientation = Orientation.Horizontal };
        busqSp.Children.Add(new TextBlock { Text = "🔍", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Foreground = HBr("#7FB3D3"),
            Margin = new Thickness(8,0,4,0) });
        busqSp.Children.Add(_txtBuscar);
        busqBorder.Child = busqSp;
        _txtBuscar.TextChanged += (_, _) => AplicarFiltroLocal();
        Grid.SetColumn(busqBorder, 0); barraG.Children.Add(busqBorder);

        // Fecha desde-hasta — helper para DatePicker estilizado. Antes armaba un estilo manual
        // solo para el DatePickerTextBox interno y dejaba dp.CalendarStyle = null — sin un
        // ControlTemplate propio con PART_Popup, el calendario no se desplegaba al hacer click
        // (bug real reportado: "no se despliega el input date el date picker"). Se reutiliza
        // UiStyles.ModernDatePickerStyle(), el mismo ControlTemplate ya usado en otras pantallas
        // de Informes con PART_Root/PART_TextBox/PART_Button/PART_Popup completos.
        DatePicker MakeDp(DateTime fecha) {
            var dp = new DatePicker {
                Width = 118, SelectedDate = fecha,
                VerticalAlignment = VerticalAlignment.Center,
                Style = CrediSoft.UI.Views.Shared.UiStyles.ModernDatePickerStyle() };
            return dp; }

        Border WrapDp(DatePicker dp, string label) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock {
                Text = label, FontSize = 9.5, FontWeight = FontWeights.Bold,
                Foreground = HBr("#BBDEFB"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8,0,5,0) });
            sp.Children.Add(dp);
            return new Border {
                Background = HBr("#EEF2F6"), CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0,0,6,0),
                Padding = new Thickness(0,1,6,1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = sp }; }

        var fechaSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
        _dpDesde = MakeDp(new DateTime(DateTime.Today.Year, 1, 1));
        _dpHasta = MakeDp(DateTime.Today);
        // Suscribir DESPUÉS de asignar la fecha inicial para no disparar Buscar() prematuramente
        _dpDesde.SelectedDateChanged += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _dpHasta.SelectedDateChanged += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        fechaSp.Children.Add(WrapDp(_dpDesde, "DESDE"));
        fechaSp.Children.Add(new TextBlock { Text = "→", Foreground = HBr("#7FB3D3"),
            FontSize = 16, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,6,0) });
        fechaSp.Children.Add(WrapDp(_dpHasta, "HASTA"));
        Grid.SetColumn(fechaSp, 1); barraG.Children.Add(fechaSp);

        // Botón mes actual
        var btnMes = new Button { Content = "Mes actual", Height = 32,
            Padding = new Thickness(10,0,10,0), Margin = new Thickness(0,0,10,0),
            Background = HBr("#0E2F44"), Foreground = HBr("#4FC3F7"),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1), BorderBrush = HBr("#2A7AB5"),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        btnMes.Click += (_, _) => {
            _pagina = 1;
            _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _dpHasta.SelectedDate = DateTime.Today;
        };
        Grid.SetColumn(btnMes, 2); barraG.Children.Add(btnMes);

        // Local selector
        _txtLocal = new TextBox { Width = 160, Padding = new Thickness(6,0,6,0), Height = 32,
            IsReadOnly = true, Cursor = Cursors.Arrow,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = HBr("#0E2F44"), Foreground = HBr("#7FB3D3"),
            FontStyle = FontStyles.Italic, FontSize = 11,
            Text = "Todos los locales", BorderThickness = new Thickness(0) };
        var btnLocal = new Button { Content = "📍 Local", Height = 32,
            Padding = new Thickness(10,0,10,0), Margin = new Thickness(4,0,0,0),
            Background = HBr("#0E2F44"), Foreground = HBr("#4FC3F7"),
            FontSize = 11, BorderThickness = new Thickness(1), BorderBrush = HBr("#2A7AB5"),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        btnLocal.Click += async (_, _) => {
            var modal = new CrediSoft.UI.Views.Compras.BuscadorLocalModal(_db) { Owner = this };
            if (modal.ShowDialog() == true && modal.LocalSeleccionado != null) {
                _idLocalFiltro = modal.LocalSeleccionado.IdLocal;
                _txtLocal.Text = modal.LocalSeleccionado.Nombre;
                _txtLocal.FontStyle = FontStyles.Normal;
                _txtLocal.Foreground = System.Windows.Media.Brushes.White;
                _pagina = 1;
                await Buscar();
            }
        };
        var btnTodosLocal = new Button { Content = "✕", Height = 32, Width = 32,
            Background = HBr("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(2,0,0,0), VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed };
        btnTodosLocal.Click += async (_, _) => {
            _idLocalFiltro = null;
            _txtLocal.Text = "Todos los locales";
            _txtLocal.FontStyle = FontStyles.Italic;
            _txtLocal.Foreground = HBr("#7FB3D3");
            btnTodosLocal.Visibility = Visibility.Collapsed;
            _pagina = 1;
            await Buscar();
        };
        btnLocal.Click += (_, _) => btnTodosLocal.Visibility =
            _idLocalFiltro.HasValue ? Visibility.Visible : Visibility.Collapsed;
        var localSp = new StackPanel { Orientation = Orientation.Horizontal,
            Margin = new Thickness(10,0,0,0) };
        localSp.Children.Add(_txtLocal);
        localSp.Children.Add(btnLocal);
        localSp.Children.Add(btnTodosLocal);
        Grid.SetColumn(localSp, 3); barraG.Children.Add(localSp);

        barra.Child = barraG;
        root.Children.Add(barra);

        // ── CHIPS DE ESTADO ──────────────────────────────────────────────────
        var chipBar = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(14,8,14,8) };
        DockPanel.SetDock(chipBar, Dock.Top);
        var chipSp = new StackPanel { Orientation = Orientation.Horizontal };

        Border MakeChip(string txt, string? estado, string bg, string fg, out Border chip) {
            var tb = new TextBlock { Text = txt, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = HBr(fg), VerticalAlignment = VerticalAlignment.Center };
            chip = new Border { Background = HBr(bg), CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14,5,14,5), Margin = new Thickness(0,0,8,0),
                Cursor = Cursors.Hand, Child = tb };
            var capEstado = estado;
            chip.MouseLeftButtonUp += (_, _) => {
                _filtroEstado = _filtroEstado == capEstado ? null : capEstado;
                AplicarFiltroLocal();
                ActualizarChips(); };
            return chip; }

        chipSp.Children.Add(MakeChip("Todos",      null,        "#1A4F6E", "#BBDEFB", out _chipTodos));
        chipSp.Children.Add(MakeChip("⏳ Pendiente","Pendiente", "#7B3A00", "#FFB74D", out _chipPend));
        chipSp.Children.Add(MakeChip("✓ Aceptado", "Aceptado",  "#1B4A1E", "#81C784", out _chipAcep));
        chipSp.Children.Add(MakeChip("✕ Anulado",  "Anulado",   "#4A1E1E", "#EF9A9A", out _chipAnul));
        chipSp.Children.Add(new TextBlock { Text = "  Clic en un estado para filtrar",
            Foreground = HBr("#546E7A"), FontSize = 10, FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center });
        chipBar.Child = chipSp;
        ActualizarChips();
        root.Children.Add(chipBar);

        // ── FOOTER ───────────────────────────────────────────────────────────
        var footer = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(16,8,16,8) };
        DockPanel.SetDock(footer, Dock.Bottom);
        footer.Child = new TextBlock {
            Text = "Doble clic sobre una fila para ver el detalle completo de la transferencia",
            Foreground = HBr("#546E7A"), FontSize = 10.5, FontStyle = FontStyles.Italic };
        root.Children.Add(footer);

        // ── GRILLA PRINCIPAL ─────────────────────────────────────────────────
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, HBr("#0E2F44")));
        colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,8,10,8)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, HBr("#1A4F6E")));
        colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));

        var rowStyle = new Style(typeof(DataGridRow));
        var pendTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("EstadoTxt"), Value = "Pendiente" };
        pendTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HBr("#FFF3E0")));
        pendTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#E65100")));
        var acepTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("EstadoTxt"), Value = "Aceptado" };
        acepTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#1B5E20")));
        var anulTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("EstadoTxt"), Value = "Anulado" };
        anulTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HBr("#FAFAFA")));
        anulTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#9E9E9E")));
        rowStyle.Triggers.Add(pendTrig);
        rowStyle.Triggers.Add(acepTrig);
        rowStyle.Triggers.Add(anulTrig);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 34,
            FontSize = 12, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = HBr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = HBr("#DDEEFF"),
            ColumnHeaderStyle = colHdrStyle, RowStyle = rowStyle,
            SelectionMode = DataGridSelectionMode.Single, CanUserSortColumns = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

        DataGridTextColumn GC(string h, string path, double w, string? fmt = null, bool right = false) {
            var col = new DataGridTextColumn {
                Header = h, SortMemberPath = path,
                Width = w > 0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                               : new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = fmt != null ? new System.Windows.Data.Binding(path) { StringFormat = fmt }
                                      : new System.Windows.Data.Binding(path) };
            if (right) { var s = new Style(typeof(DataGridCell));
                s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.CellStyle = s; }
            return col; }

        _grid.Columns.Add(GC("N° Remito",   "NumeroRem",   120));
        _grid.Columns.Add(GC("Fecha",        "Fecha",       100));
        _grid.Columns.Add(GC("Origen",       "Origen",      0));
        _grid.Columns.Add(GC("Destino",      "Destino",     0));
        _grid.Columns.Add(GC("Arts.",         "CantArts",    55, null, true));
        _grid.Columns.Add(GC("Total Gs.",    "TotalContado",120, "N0", true));
        _grid.Columns.Add(GC("Estado",       "EstadoTxt",   90));
        _grid.Columns.Add(GC("Emisor",       "Emisor",      120));

        _grid.MouseDoubleClick += async (_, _) => {
            if (_grid.SelectedItem is FilaTransferencia r) await AbrirDetalle(r); };
        _grid.SelectionChanged += async (_, _) => {
            if (_grid.SelectedItem is FilaTransferencia r) await AbrirDetalle(r); };

        // ── FOOTER PAGINACIÓN — debe agregarse ANTES del grid en DockPanel ───
        var pagFooter = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(12,6,12,6) };
        DockPanel.SetDock(pagFooter, Dock.Bottom);
        var pagG = new Grid();
        pagG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pagG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pagG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Chips de tamaño de página
        var chipsPag = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var n in new[] { 20, 50, 100 })
        {
            var nn = n;
            var chip = new Border {
                CornerRadius = new CornerRadius(12), Padding = new Thickness(10,3,10,3),
                Margin = new Thickness(3,0,3,0), Cursor = Cursors.Hand,
                Background = nn == _porPagina ? System.Windows.Media.Brushes.White : HBr("#1A4F6E")
            };
            var tb = new TextBlock { Text = nn.ToString(), FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = nn == _porPagina ? HBr("#0E2F44") : HBr("#7FB3D3") };
            chip.Child = tb;
            chip.MouseLeftButtonUp += async (_, _) => {
                _porPagina = nn; _pagina = 1;
                // actualizar visual
                foreach (Border c in chipsPag.Children)
                    if (c.Child is TextBlock t2) {
                        bool act = t2.Text == nn.ToString();
                        c.Background = act ? System.Windows.Media.Brushes.White : HBr("#1A4F6E");
                        t2.Foreground = act ? HBr("#0E2F44") : HBr("#7FB3D3");
                    }
                await Buscar();
            };
            chipsPag.Children.Add(chip);
        }
        Grid.SetColumn(chipsPag, 0); pagG.Children.Add(chipsPag);

        // Botones prev/next + info
        var navSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        Button MkNavBtn(string txt, Thickness margin) {
            var b = new Button {
                Width = 70, Height = 28, Margin = margin,
                Background = HBr("#1F6089"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1), BorderBrush = HBr("#4FC3F7"),
                Cursor = Cursors.Hand, FontSize = 12, FontWeight = FontWeights.Bold,
                Content = new TextBlock {
                    Text = txt,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    FontSize = 12, FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                }
            };
            return b;
        }
        _btnPrev = MkNavBtn("< Ant", new Thickness(0,0,8,0));
        _btnNext = MkNavBtn("Sig >", new Thickness(8,0,0,0));
        _lblPagInfo = new TextBlock { Foreground = HBr("#BBDEFB"), FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,4,0) };
        _btnPrev.Click += async (_, _) => { if (_pagina > 1) { _pagina--; await Buscar(); } };
        _btnNext.Click += async (_, _) => { if (_pagina * _porPagina < _total) { _pagina++; await Buscar(); } };
        navSp.Children.Add(_btnPrev);
        navSp.Children.Add(_lblPagInfo);
        navSp.Children.Add(_btnNext);
        Grid.SetColumn(navSp, 1); pagG.Children.Add(navSp);

        pagFooter.Child = pagG;
        root.Children.Add(pagFooter);

        // El grid SIEMPRE último en DockPanel — ocupa el espacio restante
        root.Children.Add(_grid);

        Content = root;
    }

    private void ActualizarChips()
    {
        void Set(Border chip, bool active) {
            chip.BorderThickness = active ? new Thickness(2) : new Thickness(0);
            chip.BorderBrush = HBr("#FFFFFF");
            chip.Opacity = active ? 1.0 : 0.55; }
        Set(_chipTodos, _filtroEstado == null);
        Set(_chipPend,  _filtroEstado == "Pendiente");
        Set(_chipAcep,  _filtroEstado == "Aceptado");
        Set(_chipAnul,  _filtroEstado == "Anulado");
    }

    private async Task Buscar()
    {
        if (_dpDesde == null || _cargando) return;
        _cargando = true;
        try
        {
            var desde = _dpDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var hasta = _dpHasta.SelectedDate ?? DateTime.Today;
            var busq  = _txtBuscar?.Text.Trim() ?? "";

            var localCondTmp  = _idLocalFiltro.HasValue
                ? "AND (r.IDORIGENTMP = @IdLocal OR r.IDDESTINOTMP = @IdLocal)" : "";
            var localCondAcep = _idLocalFiltro.HasValue
                ? "AND (a.IDORIGEN    = @IdLocal OR a.IDDESTINO    = @IdLocal)" : "";
            var estadoCond = _filtroEstado switch {
                "Pendiente" => "AND EstadoTxt = 'Pendiente'",
                "Aceptado"  => "AND EstadoTxt = 'Aceptado'",
                "Anulado"   => "AND EstadoTxt = 'Anulado'",
                _           => ""
            };
            var busqCond = string.IsNullOrWhiteSpace(busq) ? "" :
                "AND (NumeroRem LIKE @Busq OR Origen LIKE @Busq OR Destino LIKE @Busq OR Emisor LIKE @Busq)";

            int offset = (_pagina - 1) * _porPagina;

            // CTE base — CantArts via LEFT JOIN (no subquery por fila)
            var cteBase = $@"
WITH DetTmp AS (
    SELECT ID_REMITO_TMP AS IdRef, COUNT(*) AS Cnt FROM DET_REMITO_TMP GROUP BY ID_REMITO_TMP
),
DetAcep AS (
    SELECT IDREMITO AS IdRef, COUNT(*) AS Cnt FROM DET_REMITO_ACEPTADO GROUP BY IDREMITO
),
Base AS (
    SELECT r.ID_REMITO_TMP                          AS IdRemitoTmp,
           RTRIM(r.NUMERO_REM_TMP)                  AS NumeroRem,
           ISNULL(lo.NOMBRE,'Local ?')              AS Origen,
           ISNULL(ld.NOMBRE,'Local ?')              AS Destino,
           r.TOTALCOSTO                             AS TotalCosto,
           r.TOTALCONTADO                           AS TotalContado,
           CONVERT(VARCHAR(10),r.FECHA,103)         AS Fecha,
           CONVERT(VARCHAR(5), r.FECHA,108)         AS Hora,
           CASE r.ESTADO WHEN 0 THEN 'Pendiente' WHEN 2 THEN 'Anulado' ELSE 'Pendiente' END AS EstadoTxt,
           r.ESTADO                                 AS EstadoNum,
           ISNULL(dt.Cnt, 0)                        AS CantArts,
           RTRIM(ISNULL(ue.NOMBRE_USUARIO,'—'))     AS Emisor,
           CAST('—' AS NVARCHAR(100))               AS Receptor,
           ISNULL(r.NOTA,'')                        AS Nota,
           CAST(0 AS INT)                           AS EsAceptada,
           r.FECHA                                  AS FechaOrd
    FROM CAB_REMITO_TMP r
    LEFT JOIN LOCALES  lo ON r.IDORIGENTMP  = lo.ID_LOCAL
    LEFT JOIN LOCALES  ld ON r.IDDESTINOTMP = ld.ID_LOCAL
    LEFT JOIN USUARIOS ue ON r.IDU          = ue.ID_USUARIO
    LEFT JOIN DetTmp   dt ON dt.IdRef       = r.ID_REMITO_TMP
    WHERE CAST(r.FECHA AS DATE) >= @Desde AND CAST(r.FECHA AS DATE) <= @Hasta
      AND r.ESTADO <> 1
      {localCondTmp}
    UNION ALL
    SELECT a.IDREMITO                               AS IdRemitoTmp,
           RTRIM(a.NUMERO_REM_TMP)                  AS NumeroRem,
           ISNULL(lo.NOMBRE,'Local ?')              AS Origen,
           ISNULL(ld.NOMBRE,'Local ?')              AS Destino,
           a.TOTALCOSTO                             AS TotalCosto,
           a.TOTALCONTADO                           AS TotalContado,
           CONVERT(VARCHAR(10),a.FECHA,103)         AS Fecha,
           CONVERT(VARCHAR(5), a.FECHA,108)         AS Hora,
           'Aceptado'                               AS EstadoTxt,
           1                                        AS EstadoNum,
           ISNULL(da.Cnt, 0)                        AS CantArts,
           RTRIM(ISNULL(ue.NOMBRE_USUARIO,'—'))     AS Emisor,
           RTRIM(ISNULL(ur.NOMBRE_USUARIO,'—'))     AS Receptor,
           CAST('' AS NVARCHAR(100))                AS Nota,
           CAST(1 AS INT)                           AS EsAceptada,
           a.FECHA                                  AS FechaOrd
    FROM CAB_REMITO_ACEPTADO a
    LEFT JOIN LOCALES  lo ON a.IDORIGEN  = lo.ID_LOCAL
    LEFT JOIN LOCALES  ld ON a.IDDESTINO = ld.ID_LOCAL
    LEFT JOIN USUARIOS ue ON a.IDUENVIO  = ue.ID_USUARIO
    LEFT JOIN USUARIOS ur ON a.IDURECIBO = ur.ID_USUARIO
    LEFT JOIN DetAcep  da ON da.IdRef    = a.IDREMITO
    WHERE CAST(a.FECHA AS DATE) >= @Desde AND CAST(a.FECHA AS DATE) <= @Hasta
      {localCondAcep}
),
x AS (SELECT * FROM Base WHERE 1=1 {estadoCond} {busqCond})";

            // SQL Server: una CTE solo aplica al SELECT inmediato siguiente.
            // Usamos dos queries independientes en el mismo batch (separadas por ;)
            var sqlCount = cteBase + " SELECT COUNT(*) FROM x;";
            var sqlData  = cteBase + $@"
SELECT IdRemitoTmp, NumeroRem, Origen, Destino, TotalCosto, TotalContado,
       Fecha, Hora, EstadoTxt, EstadoNum, CantArts, Emisor, Receptor, Nota, EsAceptada AS EsAceptadaInt
FROM (
    SELECT *, ROW_NUMBER() OVER (ORDER BY FechaOrd DESC, IdRemitoTmp DESC) AS __rn FROM x
) __p
WHERE __rn BETWEEN {offset + 1} AND {offset + _porPagina};";

            var prm = new { Desde = desde, Hasta = hasta,
                            IdLocal = _idLocalFiltro ?? 0,
                            Busq = $"%{busq}%" };

            // Dos conexiones separadas: SQL Server no permite dos queries activas en la misma
            using var connCount = _db.Create();
            using var connData  = _db.Create();
            var taskCount = connCount.QueryFirstAsync<int>(sqlCount, prm, commandTimeout: 60);
            var taskData  = connData.QueryAsync<FilaTransferencia>(sqlData, prm, commandTimeout: 60);
            await System.Threading.Tasks.Task.WhenAll(taskCount, taskData);
            _total = taskCount.Result;
            _todos = taskData.Result.ToList();

            _grid.ItemsSource = _todos;

            // KPIs — sobre el total del período, no solo la página
            // Cargamos los conteos globales en una segunda query rápida (solo conteos, sin datos)
            var sqlKpi = $@"
SELECT
  SUM(CASE WHEN EstadoTxt='Pendiente' THEN 1 ELSE 0 END) AS Pendientes,
  SUM(CASE WHEN EstadoTxt='Aceptado'  THEN 1 ELSE 0 END) AS Aceptados,
  SUM(CASE WHEN EstadoTxt='Anulado'   THEN 1 ELSE 0 END) AS Anulados,
  COUNT(*) AS Total
FROM (
    SELECT CASE r.ESTADO WHEN 0 THEN 'Pendiente' WHEN 2 THEN 'Anulado' ELSE 'Pendiente' END AS EstadoTxt
    FROM CAB_REMITO_TMP r
    WHERE CAST(r.FECHA AS DATE) >= @Desde AND CAST(r.FECHA AS DATE) <= @Hasta AND r.ESTADO <> 1 {localCondTmp}
    UNION ALL
    SELECT 'Aceptado' FROM CAB_REMITO_ACEPTADO a
    WHERE CAST(a.FECHA AS DATE) >= @Desde AND CAST(a.FECHA AS DATE) <= @Hasta {localCondAcep}
) k";
            using var connKpi = _db.Create();
            var kpi = await connKpi.QueryFirstAsync<dynamic>(sqlKpi, prm, commandTimeout: 30);
            _kpiTotal.Text     = Convert.ToInt32(kpi.Total).ToString("N0");
            _kpiPendiente.Text = Convert.ToInt32(kpi.Pendientes).ToString("N0");
            _kpiAceptado.Text  = Convert.ToInt32(kpi.Aceptados).ToString("N0");
            _kpiAnulado.Text   = Convert.ToInt32(kpi.Anulados).ToString("N0");

            ActualizarPaginacion();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cargando = false; }
    }

    private void ActualizarPaginacion()
    {
        if (_lblPagInfo == null) return;
        int totalPags = (_total + _porPagina - 1) / _porPagina;
        if (totalPags < 1) totalPags = 1;
        _lblPagInfo.Text = $"Pág {_pagina}/{totalPags}  —  {_total:N0} registros";
        _btnPrev.IsEnabled = _pagina > 1;
        _btnNext.IsEnabled = _pagina < totalPags;
    }

    private void AplicarFiltroLocal()
    {
        // Con paginación SQL, los filtros de texto/estado disparan Buscar() directamente
        _pagina = 1;
        _ = Buscar();
    }

    private async Task AbrirDetalle(FilaTransferencia r)
    {
        try
        {
            using var conn = _db.Create();
            List<dynamic> detalles;

            if (r.EsAceptada)
            {
                // Artículos de transferencia aceptada
                detalles = (await conn.QueryAsync<dynamic>(@"
SELECT RTRIM(ISNULL(CAST(a.CA AS NVARCHAR(50)),'?')) AS IDART,
       RTRIM(ISNULL(a.D,'?'))                        AS DESCRIPCION,
       d.CANT                                        AS CANT,
       d.PCOSTO                                      AS PCOSTO,
       d.PCONTADO                                    AS PCONTADO
FROM DET_REMITO_ACEPTADO d
LEFT JOIN ARTICULOS a ON d.IDART = a.ID
WHERE d.IDREMITO = @Id
ORDER BY d.ORDEN", new { Id = r.IdRemitoTmp })).ToList();
            }
            else
            {
                var p = new DynamicParameters();
                p.Add("@elID", r.IdRemitoTmp);
                p.Add("@msg", dbType: System.Data.DbType.String,
                    direction: System.Data.ParameterDirection.Output, size: 20);
                detalles = (await conn.QueryAsync<dynamic>("BUSCAR_DETREMITO_TMP_CS", p,
                    commandType: System.Data.CommandType.StoredProcedure)).ToList();
            }

            new DetalleTransferenciaModal(r, detalles) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImprimirTransferencias(bool preview = false)
    {
        var lista = (_grid.ItemsSource as IEnumerable<FilaTransferencia>)?.ToList();
        if (lista == null || lista.Count == 0)
        { MessageBox.Show("No hay datos para imprimir.", "Sin datos",
            MessageBoxButton.OK, MessageBoxImage.Information); return; }

        var p = new TransferenciasPagina {
            Filas       = lista.Select(r => new FilaTransfImp(
                r.NumeroRem, r.Origen, r.Destino, r.TotalContado, r.EstadoTxt, r.Fecha)).ToList(),
            FechaImp    = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario     = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
            LogoPath    = TransferenciasPagina.ResolverLogoPath(),
            LocalFiltro = _txtLocal?.Text ?? "",
            Desde       = _dpDesde?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
            Hasta       = _dpHasta?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
        };
        if (preview) new TransferenciasPreviewWindow(p) { Owner = this }.ShowDialog();
        else         TransferenciasImpresora.Imprimir(p, this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.F5) _ = Buscar();
    }
}

internal class FilaTransferencia
{
    public int     IdRemitoTmp  { get; set; }
    public string  NumeroRem    { get; set; } = "";
    public string  Origen       { get; set; } = "";
    public string  Destino      { get; set; } = "";
    public decimal TotalCosto   { get; set; }
    public decimal TotalContado { get; set; }
    public string  Fecha        { get; set; } = "";
    public string  Hora         { get; set; } = "";
    public string  EstadoTxt    { get; set; } = "";
    public int     EstadoNum    { get; set; }
    public int     CantArts     { get; set; }
    public string  Emisor       { get; set; } = "";
    public string  Receptor     { get; set; } = "";
    public string  Nota         { get; set; } = "";
    public int     EsAceptadaInt { get; set; }
    public bool    EsAceptada    => EsAceptadaInt == 1;
}

// ── Modal de detalle rediseñado ───────────────────────────────────────────────
internal class DetalleTransferenciaModal : Window
{
    private static System.Windows.Media.SolidColorBrush TBr(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    public DetalleTransferenciaModal(FilaTransferencia r, List<dynamic> detalles)
    {
        Title  = $"Transferencia {r.NumeroRem}";
        Width  = 900; Height = 640;
        MinWidth = 720; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = TBr("#EEF2F6");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        // ── Helpers ───────────────────────────────────────────────────────────
        TextBlock MiniLbl(string t) => new TextBlock { Text = t, FontSize = 9,
            FontWeight = FontWeights.Bold, Foreground = TBr("#7FB3D3"),
            Margin = new Thickness(0,0,0,2) };
        TextBlock Val(string t, bool bold = false, string? color = null) => new TextBlock {
            Text = t, FontSize = 12,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = color != null ? TBr(color) : TBr("#0D1F2D"),
            TextWrapping = TextWrapping.Wrap };
        StackPanel InfoSp(string lbl, string val, bool bold = false, string? color = null) {
            var sp = new StackPanel { Margin = new Thickness(0,0,0,10) };
            sp.Children.Add(MiniLbl(lbl)); sp.Children.Add(Val(val, bold, color));
            return sp; }
        Border Card(UIElement child) => new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = TBr("#D6E5EF"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14,12,14,12),
            Margin = new Thickness(0,0,0,10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth=1, BlurRadius=4, Opacity=0.07,
                Color=System.Windows.Media.Colors.Black, Direction=270 },
            Child = child };

        // Estado y colores
        var (estadoBg, estadoFg) = r.EstadoTxt switch {
            "Aceptado"  => ("#1B5E20", "#E8F5E9"),
            "Anulado"   => ("#B71C1C", "#FFEBEE"),
            _           => ("#E65100", "#FFF3E0"),
        };
        var (estadoIcon, estadoLabel) = r.EstadoTxt switch {
            "Aceptado" => ("✓", "ACEPTADO"),
            "Anulado"  => ("✕", "ANULADO"),
            _          => ("⏳", "PENDIENTE"),
        };

        // ── HEADER ───────────────────────────────────────────────────────────
        var hdr = new Border { Background = TBr("#0E2F44"), Padding = new Thickness(18,14,18,14) };
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = $"Remito  {r.NumeroRem}",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        var hdrSub = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,0) };
        hdrSub.Children.Add(new TextBlock { Text = $"{r.Fecha}  {r.Hora}",
            Foreground = TBr("#7FB3D3"), FontSize = 11 });
        if (!string.IsNullOrEmpty(r.Emisor) && r.Emisor != "—")
            hdrSub.Children.Add(new TextBlock { Text = $"  ·  Emisor: {r.Emisor}",
                Foreground = TBr("#7FB3D3"), FontSize = 11 });
        hdrSp.Children.Add(hdrSub);
        hdrG.Children.Add(hdrSp);

        var estadoBadge = new Border {
            Background = TBr(estadoBg), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14,8,14,8), VerticalAlignment = VerticalAlignment.Center };
        estadoBadge.Child = new TextBlock {
            Text = $"{estadoIcon}  {estadoLabel}",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12 };
        Grid.SetColumn(estadoBadge, 1); hdrG.Children.Add(estadoBadge);
        hdr.Child = hdrG;

        // ── CHIPS RUTA y TOTALES ──────────────────────────────────────────────
        var rutaBar = new Border { Background = TBr("#1A4F6E"), Padding = new Thickness(16,10,16,10) };
        var rutaG = new Grid();
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rutaG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Border LocalChip(string lbl, string nom, string bg) {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8.5,
                Foreground = TBr("#7FB3D3"), Margin = new Thickness(0,0,0,2) });
            sp.Children.Add(new TextBlock { Text = nom, FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White });
            return new Border { Background = TBr(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14,8,14,8), Child = sp }; }

        var origenChip = LocalChip("ORIGEN", r.Origen, "#0E2F44");
        var arrow = new TextBlock { Text = "  →  ", FontSize = 20, Foreground = TBr("#4FC3F7"),
            VerticalAlignment = VerticalAlignment.Center };
        var destinoChip = LocalChip("DESTINO", r.Destino, "#0E2F44");
        Grid.SetColumn(arrow, 1); Grid.SetColumn(destinoChip, 2);
        rutaG.Children.Add(origenChip); rutaG.Children.Add(arrow); rutaG.Children.Add(destinoChip);

        // Chips de totales — mapeo flexible para SP antiguo y query directa
        var filas = detalles.Select(d => {
            var rd = (IDictionary<string, object>)d;
            string S(params string[] keys) {
                foreach (var k in keys)
                    if (rd.TryGetValue(k, out var v) && v != null) return v.ToString()!;
                return ""; }
            decimal D(params string[] keys) {
                foreach (var k in keys)
                    if (rd.TryGetValue(k, out var v) && v != null &&
                        decimal.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out var dv))
                        return dv;
                return 0m; }
            var cant  = D("Cantidad",  "CANT");
            var pcosto = D("P. costo", "PCOSTO");
            var pcont  = D("P. contado", "PCONTADO");
            return new FilaDetTransf {
                Codigo      = S("Código",      "IDART"),
                Descripcion = S("Descripción", "DESCRIPCION"),
                Cantidad    = cant,
                PCosto      = pcosto,
                PContado    = pcont,
                SubCosto    = D("SubCosto")    == 0 ? cant * pcosto : D("SubCosto"),
                SubContado  = D("SubContado")  == 0 ? cant * pcont  : D("SubContado") }; }).ToList();

        Border TotalChip(string lbl, string val, string bg, string fg) {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = val, FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = TBr(fg), TextAlignment = TextAlignment.Center });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8, Foreground = TBr(fg),
                TextAlignment = TextAlignment.Center, Opacity = 0.8 });
            return new Border { Background = TBr(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12,7,12,7), Margin = new Thickness(8,0,0,0),
                VerticalAlignment = VerticalAlignment.Center, Child = sp }; }

        var totalCosto   = filas.Any() ? filas.Sum(f => f.SubCosto)   : r.TotalCosto;
        var totalContado = filas.Any() ? filas.Sum(f => f.SubContado) : r.TotalContado;
        Grid.SetColumn(TotalChip($"Gs. {totalCosto:N0}",   "TOTAL COSTO",   "#0D1F2D", "#7FB3D3"), 3);
        Grid.SetColumn(TotalChip($"Gs. {totalContado:N0}", "TOTAL CONTADO", "#0D1F2D", "#4FC3F7"), 4);
        Grid.SetColumn(TotalChip($"{filas.Count}",         "ARTÍCULOS",     "#0D1F2D", "#BBDEFB"), 5);
        rutaG.Children.Add(TotalChip($"Gs. {totalCosto:N0}",   "TOTAL COSTO",   "#0D1F2D", "#7FB3D3"));
        rutaG.Children.Add(TotalChip($"Gs. {totalContado:N0}", "TOTAL CONTADO", "#0D1F2D", "#4FC3F7"));
        rutaG.Children.Add(TotalChip($"{filas.Count}",         "ARTÍCULOS",     "#0D1F2D", "#BBDEFB"));
        rutaBar.Child = rutaG;

        // ── GRILLA DE ARTÍCULOS ───────────────────────────────────────────────
        var colHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdr.Setters.Add(new Setter(Control.BackgroundProperty, TBr("#0E2F44")));
        colHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdr.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        colHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,7,10,7)));
        colHdr.Setters.Add(new Setter(Control.BorderBrushProperty, TBr("#1A4F6E")));
        colHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));

        var artGrid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 32,
            FontSize = 12, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = TBr("#F4F8FB"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = TBr("#DDEEFF"),
            ColumnHeaderStyle = colHdr,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

        DataGridTextColumn DC(string h, string bind, double w, string? fmt = null, bool right = false) {
            var col = new DataGridTextColumn {
                Header = h,
                Width = w > 0 ? new DataGridLength(w, DataGridLengthUnitType.Pixel)
                               : new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = fmt != null ? new System.Windows.Data.Binding(bind) { StringFormat = fmt }
                                      : new System.Windows.Data.Binding(bind) };
            if (right) { var s = new Style(typeof(DataGridCell));
                s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.CellStyle = s; }
            return col; }

        artGrid.Columns.Add(DC("Código",         "Codigo",      90));
        artGrid.Columns.Add(DC("Descripción",    "Descripcion", 0));
        artGrid.Columns.Add(DC("Cantidad",       "Cantidad",    72,  "N2", true));
        artGrid.Columns.Add(DC("P. Costo Gs.",   "PCosto",     110,  "N0", true));
        artGrid.Columns.Add(DC("P. Contado Gs.", "PContado",   110,  "N0", true));
        artGrid.Columns.Add(DC("Subt. Costo",    "SubCosto",   115,  "N0", true));
        artGrid.Columns.Add(DC("Subt. Contado",  "SubContado", 115,  "N0", true));
        artGrid.ItemsSource = filas;

        // ── NOTA / INFO ADICIONAL ─────────────────────────────────────────────
        UIElement? receptorRow = null;
        var receptorInfo = (r.EstadoTxt == "Aceptado" && !string.IsNullOrWhiteSpace(r.Receptor) && r.Receptor != "—")
            ? $"  ·  Recibido por: {r.Receptor}" : "";
        var notaText = !string.IsNullOrWhiteSpace(r.Nota) ? $"📝  Nota: {r.Nota}"
                     : r.EstadoTxt == "Aceptado"          ? $"✓  Transferencia aceptada{receptorInfo}"
                     : null;
        if (notaText != null)
        {
            var notaBg = r.EstadoTxt == "Aceptado" ? "#E8F5E9" : "#FFF8E1";
            var notaFg = r.EstadoTxt == "Aceptado" ? "#1B5E20" : "#E65100";
            receptorRow = new Border {
                Background = TBr(notaBg),
                BorderBrush = TBr(r.EstadoTxt == "Aceptado" ? "#A5D6A7" : "#FFE082"),
                BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(16,8,16,8) };
            ((Border)receptorRow).Child = new TextBlock {
                Text = notaText, Foreground = TBr(notaFg),
                FontWeight = FontWeights.SemiBold, FontSize = 12 };
        }

        // ── FOOTER ───────────────────────────────────────────────────────────
        var btnCerrar = new Button {
            Content = "✓  Cerrar", Height = 36, Padding = new Thickness(28,0,28,0),
            Background = TBr("#1A4F6E"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        var footer = new Border {
            Background = TBr("#EEF2F6"), Padding = new Thickness(16,10,16,14),
            BorderBrush = TBr("#D6E5EF"), BorderThickness = new Thickness(0,1,0,0),
            Child = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { btnCerrar } } };

        // ── LAYOUT ───────────────────────────────────────────────────────────
        var rootDp = new DockPanel();
        DockPanel.SetDock(hdr,     Dock.Top);
        DockPanel.SetDock(rutaBar, Dock.Top);
        DockPanel.SetDock(footer,  Dock.Bottom);
        rootDp.Children.Add(hdr);
        rootDp.Children.Add(rutaBar);
        rootDp.Children.Add(footer);
        if (receptorRow != null) { DockPanel.SetDock(receptorRow, Dock.Bottom); rootDp.Children.Add(receptorRow); }
        rootDp.Children.Add(artGrid);

        Content = rootDp;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}

internal class FilaDetTransf
{
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public decimal Cantidad    { get; set; }
    public decimal PCosto      { get; set; }
    public decimal PContado    { get; set; }
    public decimal SubCosto    { get; set; }
    public decimal SubContado  { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
//  REGISTRAR / EXPLORADOR DE CAJA
// ══════════════════════════════════════════════════════════════════════════════
public class CajaExploradorWindow : Window
{
    // ── State ────────────────────────────────────────────────────────────────
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    // Controles
    private DataGrid   _grid      = null!;
    private DatePicker _dtDesde   = null!, _dtHasta = null!;
    private TextBox    _txtBuscar = null!;
    private ComboBox   _cboLocal  = null!, _cboTipoF = null!, _cboSubTipo = null!, _cboEstado = null!;
    private Border     _lblLocalFijo = null!;
    private TextBlock  _lblPagInfo = null!, _lblKpi = null!, _lblSaldo = null!;
    private Border     _btnPrev   = null!, _btnNext = null!;

    // Paginación en memoria
    private int  _pagina    = 1;
    private int  _porPagina = 50;
    private int  _total     = 0;
    private bool _cargando  = false;
    private List<FilaExploradorCaja> _todosFiltrados = new();

    // Datos de locales para el filtro
    private List<(int Id, string Nombre)> _locales = new();
    private int? _idLocalFiltro = null;

    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaExploradorWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title    = "Explorador de Caja";
        Width = 1220; Height = 740;
        MinWidth = 960; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = RB("#F0F2F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();

        // Rango de fechas: solo Administrador o el usuario código "67" (Aida Acosta, ver
        // Usuario.PuedeVerTodosLosLocales) pueden salir del día de hoy.
        var puedeAmpliarRango = _session.UsuarioActual?.PuedeVerTodosLosLocales ?? false;
        _dtDesde.IsEnabled = puedeAmpliarRango;
        _dtHasta.IsEnabled = puedeAmpliarRango;

        Loaded += async (_, _) => { await CargarLocales(); await Buscar(); };
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // paginacion
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer kpi

        // ── Header ── paleta unificada (#0E2F44/#1565C0, la misma que Cobros/Caja/
        // Informe de Cobranzas) en vez del índigo #283593/#1A237E que quedaba disonante
        // frente al resto de la app; íconos Segoe MDL2 Assets en vez de emojis.
        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(20, 14, 20, 14) };
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var hdrIcon = new Border {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(8),
            Background = RB("#1A4F6E"), Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 17,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        hdrTitleRow.Children.Add(hdrIcon);
        var hdrTitle = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTitle.Children.Add(new TextBlock { Text = "EXPLORADOR DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrTitle.Children.Add(new TextBlock { Text = "Movimientos de caja",
            Foreground = RB("#90A4AE"), FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        hdrTitleRow.Children.Add(hdrTitle);
        hdrG.Children.Add(hdrTitleRow);

        Border HBtn(string ico, string txt, string bg) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ico, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 12, Margin = new Thickness(0,0,6,0),
                Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = txt, Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            return new Border { Height = 34, Padding = new Thickness(14,0,14,0), Margin = new Thickness(6,0,0,0),
                Background = RB(bg), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand, Child = sp };
        }
        var btnNuevo  = HBtn("", "Nuevo",  "#2E7D32");
        var btnEditar = HBtn("", "Editar", "#1565C0");
        var btnAnular = HBtn("", "Anular", "#C62828");
        var btnCerrar = HBtn("", "Cerrar", "#546E7A");
        var btnSp = new StackPanel { Orientation = Orientation.Horizontal };
        btnSp.Children.Add(btnNuevo); btnSp.Children.Add(btnEditar);
        btnSp.Children.Add(btnAnular); btnSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnSp, 1); hdrG.Children.Add(btnSp);
        hdr.Child = hdrG;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Barra de filtros ────────────────────────────────────────────────
        var filtroBorder = new Border {
            Background = RB("#F8FAFD"),
            BorderBrush = RB("#DDE3ED"), BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12) };

        var filtroPanel = new WrapPanel { Orientation = Orientation.Horizontal };

        // Helper label+control
        UIElement LblCtrl(string lbl, UIElement ctrl, double w = 0) {
            var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0,0,10,4) };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = RB("#5C6BC0"), Margin = new Thickness(0,0,0,2) });
            if (w > 0 && ctrl is FrameworkElement fe) fe.Width = w;
            sp.Children.Add(ctrl); return sp;
        }

        // Rango de fechas restringido: por defecto solo se puede ver el día de hoy (evita que
        // un cajero/vendedor normal navegue movimientos de caja de otras fechas). Solo
        // Administrador o el usuario 67 pueden ampliar el rango — ver ajuste de fechas más
        // abajo en Loaded, una vez que _session.UsuarioActual ya está disponible.
        var modernDpStyle = CrediSoft.UI.Views.Shared.UiStyles.ModernDatePickerStyle();
        _dtDesde = new DatePicker { SelectedDate = DateTime.Today, Style = modernDpStyle, Width = 130, IsEnabled = false };
        _dtHasta = new DatePicker { SelectedDate = DateTime.Today, Style = modernDpStyle, Width = 130, IsEnabled = false };

        // Placeholder inicial — CargarLocales() decide si termina siendo un combo real
        // (administrador) o una etiqueta fija de solo lectura (usuario normal). El chip
        // arranca visible con "Cargando..." para que SIEMPRE haya algo mostrado desde el
        // primer render, incluso si CargarLocales (async) tarda o falla silenciosamente.
        _cboLocal = new ComboBox { FontSize = 12, Width = 150, Visibility = Visibility.Collapsed };
        _lblLocalFijo = new Border {
            Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = RB("#EEF4FB"), BorderBrush = RB("#BBDEFB"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3), Width = 220,
            ToolTip = "Cargando...",
            Child = new TextBlock { Text = "Cargando...", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = RB("#0E2F44"), VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis },
            Visibility = Visibility.Visible };

        _cboTipoF = new ComboBox { FontSize = 12, Width = 110 };
        foreach (var t in new[] { "Todos", "INGRESO", "EGRESO" }) _cboTipoF.Items.Add(t);
        _cboTipoF.SelectedIndex = 0;

        _cboSubTipo = new ComboBox { FontSize = 12, Width = 120 };
        foreach (var s in new[] { "Todos", "VENTA", "COBRO_S", "COBRO_C", "GASTOS", "CAJA_INICIAL", "TRANSFERENCIA" })
            _cboSubTipo.Items.Add(s);
        _cboSubTipo.SelectedIndex = 0;

        _cboEstado = new ComboBox { FontSize = 12, Width = 100 };
        foreach (var e in new[] { "Todos", "VALIDO", "ANULADO" }) _cboEstado.Items.Add(e);
        _cboEstado.SelectedIndex = 0;

        _txtBuscar = new TextBox { FontSize = 12, Width = 180, Height = 26,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Busca por concepto, referencia o nombre del cajero" };

        var btnBuscar = new Border { Height = 26, Padding = new Thickness(12,0,12,0), Margin = new Thickness(0,16,8,4),
            Background = RB("#1565C0"), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Buscar", Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center } };
        var btnLimpiar = new Border { Height = 26, Padding = new Thickness(10,0,10,0), Margin = new Thickness(0,16,0,4),
            Background = RB("#78909C"), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Limpiar", Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center } };

        var localSlot = new Grid { Width = 220 };
        localSlot.Children.Add(_cboLocal);
        localSlot.Children.Add(_lblLocalFijo);

        filtroPanel.Children.Add(LblCtrl("Desde", _dtDesde));
        filtroPanel.Children.Add(LblCtrl("Hasta", _dtHasta));
        filtroPanel.Children.Add(LblCtrl("Local", localSlot));
        filtroPanel.Children.Add(LblCtrl("Tipo", _cboTipoF));
        filtroPanel.Children.Add(LblCtrl("SubTipo", _cboSubTipo));
        filtroPanel.Children.Add(LblCtrl("Estado", _cboEstado));
        filtroPanel.Children.Add(LblCtrl("Concepto / Cajero", _txtBuscar));
        filtroPanel.Children.Add(btnBuscar);
        filtroPanel.Children.Add(btnLimpiar);

        filtroBorder.Child = filtroPanel;
        Grid.SetRow(filtroBorder, 1); root.Children.Add(filtroBorder);

        // ── DataGrid ── misma paleta que el resto de la app (#37474F headers, no el
        // índigo #283593 disonante) ─────────────────────────────────────────────
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, RB("#37474F")));
        colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 10.0));
        colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6,5,6,5)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,2)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, RB("#00000030")));

        var rowStyle = new Style(typeof(DataGridRow));
        var egresoTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("TipoDesc"), Value = "EGRESO" };
        egresoTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, RB("#BF360C")));
        egresoTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, RB("#FFF3E0")));
        var anulTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("Anulado"), Value = true };
        anulTrig.Setters.Add(new Setter(DataGridRow.BackgroundProperty, RB("#FFEBEE")));
        anulTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, RB("#9E9E9E")));
        rowStyle.Triggers.Add(egresoTrig);
        rowStyle.Triggers.Add(anulTrig);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 28,
            FontSize = 10.5, BorderThickness = new Thickness(0),
            AlternatingRowBackground = RB("#FAFAFA"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = RB("#EEEEEE"),
            ColumnHeaderStyle = colHdrStyle, RowStyle = rowStyle,
            SelectionMode = DataGridSelectionMode.Single, CanUserSortColumns = false };

        DataGridTextColumn GC(string h, string p, double w, TextAlignment align = TextAlignment.Left) {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, align));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4,0,4,0)));
            return new DataGridTextColumn { Header = h, Width = w, SortMemberPath = p,
                Binding = new System.Windows.Data.Binding(p), ElementStyle = style };
        }
        DataGridTextColumn GCFmt(string h, string p, double w, string fmt) {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4,0,6,0)));
            return new DataGridTextColumn { Header = h, Width = w,
                Binding = new System.Windows.Data.Binding(p) { StringFormat = fmt }, ElementStyle = style };
        }

        _grid.Columns.Add(GC("Fecha/Hora", "FechaHoraStr", 130));
        _grid.Columns.Add(GC("Local",      "LocalNombre",  110));
        _grid.Columns.Add(GC("Cajero",     "Cajero",       120));
        // Ver comentario en FilaExploradorCaja.Cobrador: distingue quién operó la caja
        // físicamente de a quién se le atribuye la venta/comisión de este movimiento.
        _grid.Columns.Add(GC("Cobrador",   "Cobrador",     120));
        _grid.Columns.Add(GC("Tipo",       "TipoDesc",      70, TextAlignment.Center));
        _grid.Columns.Add(GC("SubTipo",    "SubTipo",        90));
        _grid.Columns.Add(GCFmt("Monto",   "Monto",         100, "N0"));
        _grid.Columns.Add(new DataGridTextColumn { Header = "Concepto",
            Binding = new System.Windows.Data.Binding("Concepto"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star), MinWidth = 130 });
        _grid.Columns.Add(GC("Referencia", "Referencia",     80));
        _grid.Columns.Add(GC("Receptor",   "Receptor",      110));
        _grid.Columns.Add(GC("Forma pago", "FormaPago",      80));
        _grid.Columns.Add(GC("Estado",     "EstadoDesc",     70, TextAlignment.Center));

        _grid.MouseDoubleClick += (_, _) => AbrirEdicion();
        Grid.SetRow(_grid, 2); root.Children.Add(_grid);

        // ── Paginación ── misma paleta #0E2F44 que el header (antes #1A237E/#283593
        // índigo, disonante) ─────────────────────────────────────────────────────
        var pagBorder = new Border { Background = RB("#0E2F44"), Padding = new Thickness(12, 6, 12, 6) };
        var pagPanel = new Grid();
        pagPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pagPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pagPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Selector items por página
        var perPageSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        perPageSp.Children.Add(new TextBlock { Text = "Filas por página:", Foreground = RB("#90A4AE"),
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
        var cboPerPage = new ComboBox { FontSize = 11, Width = 60, Height = 24,
            Background = System.Windows.Media.Brushes.White,
            Foreground = RB("#0E2F44"), BorderBrush = RB("#1565C0") };
        foreach (var n in new[] { 20, 50, 100, 200 }) cboPerPage.Items.Add(n);
        cboPerPage.SelectedItem = 50;
        perPageSp.Children.Add(cboPerPage);
        pagPanel.Children.Add(perPageSp);

        // Info de página
        _lblPagInfo = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(_lblPagInfo, 1); pagPanel.Children.Add(_lblPagInfo);

        // Botones nav — Border en vez de Button para eliminar el chrome de WPF (Aero hover/press)
        Border MkNav(string txt) => new Border {
            Width = 72, Height = 26, Margin = new Thickness(4,0,0,0),
            Background = RB("#1A4F6E"), CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1), BorderBrush = RB("#1565C0"), Cursor = Cursors.Hand,
            Child = new TextBlock { Text = txt, Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        _btnPrev = MkNav("< Ant");
        _btnNext = MkNav("Sig >");
        var navSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        navSp.Children.Add(_btnPrev); navSp.Children.Add(_btnNext);
        Grid.SetColumn(navSp, 2); pagPanel.Children.Add(navSp);
        pagBorder.Child = pagPanel;
        Grid.SetRow(pagBorder, 3); root.Children.Add(pagBorder);

        // ── Footer KPI ──────────────────────────────────────────────────────
        var footBorder = new Border { Background = RB("#0A2436"), Padding = new Thickness(14, 7, 14, 7) };
        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _lblKpi = new TextBlock { Foreground = RB("#90A4AE"), FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center };
        footGrid.Children.Add(_lblKpi);

        _lblSaldo = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 13,
            FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(_lblSaldo, 1); footGrid.Children.Add(_lblSaldo);

        footBorder.Child = footGrid;
        Grid.SetRow(footBorder, 4); root.Children.Add(footBorder);

        Content = root;

        // ── Wiring ──────────────────────────────────────────────────────────
        btnNuevo.MouseLeftButtonUp  += (_, _) => AbrirNuevo();
        btnEditar.MouseLeftButtonUp += (_, _) => AbrirEdicion();
        btnAnular.MouseLeftButtonUp += async (_, _) => await Anular();
        btnCerrar.MouseLeftButtonUp += (_, _) => Close();
        btnBuscar.MouseLeftButtonUp += (_, _) => { _pagina = 1; _ = Buscar(); };
        btnLimpiar.MouseLeftButtonUp += (_, _) => Limpiar();
        _txtBuscar.KeyDown += (_, e) => { if (e.Key == Key.Return) { _pagina = 1; _ = Buscar(); } };
        _dtDesde.SelectedDateChanged += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _dtHasta.SelectedDateChanged += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _cboLocal.SelectionChanged   += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _cboTipoF.SelectionChanged   += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _cboSubTipo.SelectionChanged += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _cboEstado.SelectionChanged  += (_, _) => { if (_lblPagInfo != null) { _pagina = 1; _ = Buscar(); } };
        _btnPrev.MouseLeftButtonUp += (_, _) => { if (_pagina > 1) { _pagina--; MostrarPagina(); } };
        _btnNext.MouseLeftButtonUp += (_, _) => { int tot = Math.Max(1, (int)Math.Ceiling(_total / (double)_porPagina)); if (_pagina < tot) { _pagina++; MostrarPagina(); } };
        cboPerPage.SelectionChanged += (_, _) => {
            if (cboPerPage.SelectedItem is int n) { _porPagina = n; _pagina = 1; MostrarPagina(); }
        };
    }

    private async Task CargarLocales()
    {
        try {
            using var conn = _db.Create();
            var todosLocales = (await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY NOMBRE")).ToList();

            // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
            // Usuario.PuedeVerTodosLosLocales) puede explorar movimientos de "Todos los
            // locales" o de uno distinto al propio. Un usuario normal solo ve/filtra por SU
            // local — el combo queda fijo en ese local, sin "Todos" ni el resto de la lista.
            var esAdmin = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
            _cboLocal.Items.Clear();
            if (esAdmin)
            {
                _locales = todosLocales;
                _cboLocal.Items.Add("Todos los locales");
                foreach (var (id, nombre) in _locales) _cboLocal.Items.Add(nombre);
                _cboLocal.SelectedIndex = 0;
                _cboLocal.Visibility = Visibility.Visible;
                _lblLocalFijo.Visibility = Visibility.Collapsed;
            }
            else
            {
                var idLocalSesion = _session.LocalActual?.IdLocal ?? 0;
                var localSesion = todosLocales.FirstOrDefault(l => l.Id == idLocalSesion);
                _locales = localSesion.Nombre != null ? new() { localSesion } : new();
                var nombreLocal = localSesion.Nombre ?? _session.LocalActual?.NombreLocal ?? "Mi local";
                ((TextBlock)_lblLocalFijo.Child).Text = nombreLocal;
                _lblLocalFijo.ToolTip = nombreLocal;
                _cboLocal.Visibility = Visibility.Collapsed;
                _lblLocalFijo.Visibility = Visibility.Visible;
            }
        } catch (Exception ex) {
            ((TextBlock)_lblLocalFijo.Child).Text = "Error: " + ex.Message;
            _lblLocalFijo.Visibility = Visibility.Visible;
        }
    }

    private void Limpiar()
    {
        _dtDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _dtHasta.SelectedDate = DateTime.Today;
        _cboLocal.SelectedIndex = 0;
        _cboTipoF.SelectedIndex = 0;
        _cboSubTipo.SelectedIndex = 0;
        _cboEstado.SelectedIndex = 0;
        _txtBuscar.Text = "";
        _pagina = 1;
        _ = Buscar();
    }

    // Carga todos los datos del período en memoria — paginación es instantánea después
    private async Task Buscar()
    {
        if (_cargando) return;
        _cargando = true;
        try
        {
            var desde = _dtDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var hasta = (_dtHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);
            var busq  = _txtBuscar.Text.Trim();

            var esAdmin = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
            if (!esAdmin)
            {
                // Combo sin "Todos los locales" para usuarios normales: un unico item fijo
                // en su propio local (ver CargarLocales) — el filtro va directo por sesion.
                _idLocalFiltro = _session.LocalActual?.IdLocal;
            }
            else
            {
                _idLocalFiltro = null;
                if (_cboLocal.SelectedIndex > 0 && _cboLocal.SelectedIndex - 1 < _locales.Count)
                    _idLocalFiltro = _locales[_cboLocal.SelectedIndex - 1].Id;
            }

            var conds = new System.Text.StringBuilder("WHERE D.FECHA_HORA >= @Desde AND D.FECHA_HORA <= @Hasta ");
            if (_idLocalFiltro.HasValue)     conds.Append($"AND D.ID_LOCAL = {_idLocalFiltro.Value} ");
            if (_cboTipoF.SelectedIndex > 0) {
                var t = _cboTipoF.SelectedIndex == 1 ? "I" : "E";
                conds.Append($"AND D.TIPO = '{t}' ");
            }
            if (_cboSubTipo.SelectedIndex > 0 && _cboSubTipo.SelectedItem is string st)
                conds.Append($"AND D.SUBTIPO = '{st}' ");
            if (_cboEstado.SelectedIndex > 0) {
                var ev = _cboEstado.SelectedIndex == 1 ? "V" : "A";
                conds.Append($"AND D.ESTADO_REG = '{ev}' ");
            }
            if (!string.IsNullOrEmpty(busq))
                conds.Append("AND (D.CONCEPTO LIKE @Busq OR D.REFERENCIA LIKE @Busq OR UC.NOMBRE_USUARIO LIKE @Busq OR UV.NOMBRE_USUARIO LIKE @Busq) ");

            var joins = @"FROM CAJA_DETALLE D
                INNER JOIN USUARIOS UC ON UC.ID_USUARIO = D.ID_CAJERO
                LEFT  JOIN USUARIOS UR ON UR.ID_USUARIO = D.ID_ENTIDAD
                LEFT  JOIN USUARIOS UV ON UV.ID_USUARIO = D.ID_VENDEDOR
                LEFT  JOIN LOCALES  L  ON L.ID_LOCAL    = D.ID_LOCAL ";

            // Una sola query — trae TODO el período filtrado, paginación en memoria
            var sql = $@"SELECT
                        D.ID_DETALLE, D.ID_MASTER, D.ID_VENTA,
                        CONVERT(VARCHAR(10),D.FECHA_HORA,103)+' '+CONVERT(VARCHAR(5),D.FECHA_HORA,108) AS FechaHoraStr,
                        ISNULL(L.NOMBRE, CAST(D.ID_LOCAL AS VARCHAR)) AS LocalNombre,
                        ISNULL(UC.NOMBRE_USUARIO,'') AS Cajero,
                        ISNULL(UV.NOMBRE_USUARIO,'') AS Cobrador,
                        CASE D.TIPO WHEN 'I' THEN 'INGRESO' WHEN 'E' THEN 'EGRESO' ELSE '' END AS TipoDesc,
                        ISNULL(D.SUBTIPO,'') AS SubTipo,
                        D.MONTO,
                        ISNULL(D.CONCEPTO,'') AS Concepto,
                        ISNULL(D.REFERENCIA,'---') AS Referencia,
                        ISNULL(UR.NOMBRE_USUARIO,'---') AS Receptor,
                        ISNULL(D.FORMA_PAGO,'') AS FormaPago,
                        CASE D.ESTADO_REG WHEN 'V' THEN 'VALIDO' WHEN 'A' THEN 'ANULADO' ELSE '' END AS EstadoDesc,
                        ISNULL(UC.ID_USUARIO,0) AS ID_CAJERO,
                        ISNULL(UR.ID_USUARIO,0) AS ID_ENTIDAD
                    {joins} {conds}
                    ORDER BY D.FECHA_HORA DESC";

            var prm = new { Desde = desde, Hasta = hasta, Busq = $"%{busq}%" };
            using var conn = _db.Create();
            _todosFiltrados = (await conn.QueryAsync<FilaExploradorCaja>(sql, prm, commandTimeout: 60)).ToList();

            await CorregirCobradorCobrosAsync(conn, _todosFiltrados);

            _pagina = 1;
            MostrarPagina();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cargando = false; }
    }

    // Para movimientos COBRO/COBRO_SISTEMA, la columna "Cobrador" quedaba mal atribuida:
    // CAJA_DETALLE.ID_VENDEDOR (usado por la query base) es el vendedor que originó el
    // CRÉDITO completo (CABECERA_SALES.ID_USUARIO vía el SP legado), no quien realmente cobró
    // ESTA cuota puntual — confirmado con datos reales (cobro registrado por Aida Acosta vía
    // "Cambiar Vendedor" en Cobros, pero Cobrador mostraba al vendedor original del crédito).
    // El dato correcto está en GENERADAS.IDU, se extrae comprobante+cuota del propio texto de
    // Concepto (mismo patrón regex que ya usa Anular() más abajo, no hay columna estructurada)
    // y se resuelve en un solo batch para no golpear la base fila por fila.
    private static async Task CorregirCobradorCobrosAsync(System.Data.IDbConnection conn, List<FilaExploradorCaja> filas)
    {
        // "N.?:" (0 o 1 carácter) no alcanza: el "°" corrupto de CONCEPTO (varchar no-Unicode,
        // ver limpieza de encoding en CajaCierreWindow) queda grabado como 2 bytes (0xC2 0xB0),
        // no 1 — con "ABONO PARCIAL" el match fallaba silenciosamente y la fila quedaba con el
        // ID_VENDEDOR crudo (vendedor original del crédito) en vez del cobrador real corregido.
        var candidatos = filas
            .Where(f => f.SubTipo is "COBRO" or "COBRO_SISTEMA")
            .Select(f => (Fila: f, Match: System.Text.RegularExpressions.Regex.Match(f.Concepto,
                @"CUOTA N\S*?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*(\d+)")))
            .Where(x => x.Match.Success)
            .ToList();
        if (candidatos.Count == 0) return;

        // Los comprobantes cortos del texto de Concepto (ej. "010355") son el sufijo del
        // COMPROBANTE real de 12 dígitos con ceros a la izquierda (ej. "000000010355") — se
        // resuelve con LIKE '%sufijo' en una sola query por IN de sufijos distintos, y el
        // emparejamiento exacto por cuota se hace en memoria. Se evita una tabla temporal
        // (#tabla): IDbConnectionFactory.Create() no abre la conexión explícitamente, así que
        // Dapper puede abrirla/cerrarla entre llamadas sucesivas y la tabla temporal
        // desaparece a mitad de camino ("Invalid object name '#...'").
        var sufijos = candidatos.Select(x => x.Match.Groups[2].Value).Distinct().ToList();

        var p = new DynamicParameters();
        for (int i = 0; i < sufijos.Count; i++) p.Add($"@s{i}", $"%{sufijos[i]}");
        var filasGeneradas = (await conn.QueryAsync<(string Comprobante, byte NCuota, int Idu, string Nombre)>(
            "SELECT G.COMPROBANTE, G.NCUOTA, G.IDU, ISNULL(U.NOMBRE_USUARIO,'') " +
            "FROM GENERADAS G LEFT JOIN USUARIOS U ON U.ID_USUARIO = G.IDU " +
            "WHERE " + string.Join(" OR ", Enumerable.Range(0, sufijos.Count).Select(i => $"G.COMPROBANTE LIKE @s{i}")),
            p))
            .ToList();

        var idus = filasGeneradas
            .GroupBy(x => (x.Comprobante.TrimStart('0'), x.NCuota))
            .ToDictionary(g => g.Key, g => g.First().Nombre);

        foreach (var (fila, match) in candidatos)
        {
            var ncuota = byte.Parse(match.Groups[1].Value);
            var comprobante = match.Groups[2].Value.TrimStart('0');
            if (idus.TryGetValue((comprobante, ncuota), out var nombreReal) && !string.IsNullOrEmpty(nombreReal))
                fila.Cobrador = nombreReal;
        }
    }

    // Paginación instantánea en memoria — sin tocar la base de datos
    private void MostrarPagina()
    {
        _total = _todosFiltrados.Count;
        int totalPag = Math.Max(1, (int)Math.Ceiling(_total / (double)_porPagina));
        if (_pagina > totalPag) _pagina = totalPag;

        var pagina = _todosFiltrados
            .Skip((_pagina - 1) * _porPagina)
            .Take(_porPagina)
            .ToList();
        _grid.ItemsSource = pagina;

        decimal ing      = _todosFiltrados.Where(f => f.TipoDesc == "INGRESO" && !f.Anulado).Sum(f => f.Monto);
        decimal egr      = _todosFiltrados.Where(f => f.TipoDesc == "EGRESO"  && !f.Anulado).Sum(f => f.Monto);
        int     anulados = _todosFiltrados.Count(f => f.Anulado);

        _lblPagInfo.Text = $"Pág. {_pagina} / {totalPag}    ({_total} registros)";
        _lblKpi.Text     = $"{_total} movimientos  •  {anulados} anulados";
        _lblSaldo.Text   = $"INGRESOS: {ing:N0}  |  EGRESOS: {egr:N0}  |  SALDO: {(ing - egr):N0}";

        bool prevOk = _pagina > 1;
        bool nextOk = _pagina < totalPag;
        _btnPrev.Opacity = prevOk ? 1 : 0.35; _btnPrev.Cursor = prevOk ? Cursors.Hand : Cursors.Arrow;
        _btnNext.Opacity = nextOk ? 1 : 0.35; _btnNext.Cursor = nextOk ? Cursors.Hand : Cursors.Arrow;
    }

    // Solo el usuario que abrió ESA caja puntual (CAJA_MASTER.ID_USUARIO_APE) o un
    // ADMINISTRADOR (Usuario.EsAdministrador) puede crear/editar/anular movimientos desde acá —
    // hoy cualquier usuario con acceso a Explorador de Caja podía tocar movimientos de una caja
    // que ni abrió. CajaCredencialesDialog (usado en Anular) solo registra QUIÉN autorizó, no
    // restringe el acceso — por eso hacía falta esta validación previa, independiente de eso.
    private async Task<bool> PuedeModificarCajaAsync(int idMaster)
    {
        var usuario = _session.UsuarioActual;
        if (usuario == null) return false;
        if (usuario.EsAdministrador) return true;

        using var conn = _db.Create();
        var idUsuarioApe = await conn.ExecuteScalarAsync<int?>(
            "SELECT ID_USUARIO_APE FROM CAJA_MASTER WHERE ID_MASTER = @idMaster",
            new { idMaster });
        return idUsuarioApe.HasValue && idUsuarioApe.Value == usuario.IdUsuario;
    }

    // Variante para validar el usuario de las CREDENCIALES TIPEADAS en CajaCredencialesDialog
    // (cred.UsuarioId), no el logueado — pueden ser distintos: esta ventana ya validó el acceso
    // del usuario LOGUEADO más arriba (PuedeModificarCajaAsync(idMaster) de arriba), pero
    // CajaCredencialesDialog deja tipear las credenciales de CUALQUIER usuario válido del
    // sistema sin volver a chequear que sea dueño de la caja — sin esto, alguien podía tipear
    // sus propias credenciales (válidas) para autorizar la anulación de una caja ajena.
    private async Task<bool> PuedeModificarCajaAsync(int idMaster, int idUsuarioCred)
    {
        using var conn = _db.Create();
        var cargo = await conn.ExecuteScalarAsync<string?>(
            "SELECT CARGO_USUARIO FROM USUARIOS WHERE ID_USUARIO = @id", new { id = idUsuarioCred });
        if (string.Equals(cargo?.Trim(), "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)) return true;

        var idUsuarioApe = await conn.ExecuteScalarAsync<int?>(
            "SELECT ID_USUARIO_APE FROM CAJA_MASTER WHERE ID_MASTER = @idMaster", new { idMaster });
        return idUsuarioApe.HasValue && idUsuarioApe.Value == idUsuarioCred;
    }

    private void MostrarSinPermiso() =>
        MessageBox.Show(
            "Solo el usuario que abrió esta caja o un administrador puede realizar esta acción.",
            "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);

    private async Task<int?> ObtenerIdMasterCajaAbiertaAsync()
    {
        var idLocal = _session.LocalActual?.IdLocal;
        if (idLocal == null) return null;
        var caja = await App.Services.GetRequiredService<CrediSoft.Data.Repositories.ICajaRepository>().ObtenerCajaAbiertaAsync(idLocal.Value);
        return caja?.IdMaster;
    }

    private async void AbrirNuevo()
    {
        // Para un movimiento NUEVO no hay ID_MASTER de una fila seleccionada — se valida
        // contra la caja actualmente abierta del local de sesión (misma que usaría el
        // registro nuevo al guardarse).
        var idMaster = await ObtenerIdMasterCajaAbiertaAsync();
        if (idMaster == null) { MessageBox.Show("No hay una caja abierta en este local.", "Caja cerrada", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (!await PuedeModificarCajaAsync(idMaster.Value)) { MostrarSinPermiso(); return; }

        var dlg = new CajaEditarMovDialog(_db, _session, null) { Owner = this };
        if (dlg.ShowDialog() == true) _ = Buscar();
    }

    private async void AbrirEdicion()
    {
        if (_grid.SelectedItem is not FilaExploradorCaja fila) return;
        if (!await PuedeModificarCajaAsync(fila.ID_MASTER)) { MostrarSinPermiso(); return; }

        var dlg = new CajaEditarMovDialog(_db, _session, fila) { Owner = this };
        if (dlg.ShowDialog() == true) _ = Buscar();
    }

    private async Task Anular()
    {
        if (_grid.SelectedItem is not FilaExploradorCaja fila) return;
        if (fila.Anulado) { MessageBox.Show("Este registro ya está anulado.", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (fila.SubTipo is "VENTA")
        { MessageBox.Show("No se puede anular un movimiento de venta.", "No permitido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (!await PuedeModificarCajaAsync(fila.ID_MASTER)) { MostrarSinPermiso(); return; }

        var cred = new CajaCredencialesDialog { Owner = this };
        if (cred.ShowDialog() != true) return;
        if (!await PuedeModificarCajaAsync(fila.ID_MASTER, cred.UsuarioId))
        {
            MessageBox.Show("El usuario ingresado no es quien abrió esta caja ni es administrador.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Bug real detectado: anular acá solo marcaba CAJA_DETALLE — la cuota en GENERADAS
        // quedaba "Cobrada" para siempre aunque el dinero ya no contara (caso real: Saturnino
        // Giménez Barúa, cuota 3, ID_DETALLE 4055, anulada por error un minuto después de
        // cargarla y la cuota nunca volvió a Pendiente). Para COBRO/COBRO_SISTEMA se extrae
        // el comprobante y N° de cuota del propio texto de Concepto (no hay columna
        // estructurada) y se revierte GENERADAS junto con el movimiento de caja.
        string? avisoCuota = null;
        if (fila.SubTipo is "COBRO" or "COBRO_SISTEMA")
        {
            var m = System.Text.RegularExpressions.Regex.Match(fila.Concepto,
                @"CUOTA N.?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*(\d+)");
            if (m.Success)
            {
                var ncuota = int.Parse(m.Groups[1].Value);
                var comprobante = m.Groups[2].Value;
                avisoCuota = $"\n\nTambién se revertirá la Cuota {ncuota} a \"Pendiente\" (comprobante {comprobante}).";
            }
        }

        if (MessageBox.Show($"¿Confirmar anulación de este registro?\n{fila.Concepto}{avisoCuota}",
            "Confirmar anulación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            await conn.ExecuteAsync(
                "UPDATE CAJA_DETALLE SET ESTADO_REG='A', FECHA_ANULACION=GETDATE(), ID_USUARIO_ANUL=@uid WHERE ID_DETALLE=@id",
                new { uid = cred.UsuarioId, id = fila.ID_DETALLE });

            if (fila.SubTipo is "COBRO" or "COBRO_SISTEMA")
            {
                var m = System.Text.RegularExpressions.Regex.Match(fila.Concepto,
                    @"CUOTA N.?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*(\d+)");
                if (m.Success)
                {
                    var ncuota = byte.Parse(m.Groups[1].Value);
                    var comprobante = m.Groups[2].Value;
                    // El SP de cobro (sp_Guardar_Cobranza_Cs_2026) hace ENTREGA=@Total y
                    // TOTAL=@Total, donde @Total es el ACUMULADO de la cuota (no solo lo
                    // cobrado en esta operación) — @MontoHoy = @Total - @EntregaAnterior es lo
                    // único que suma a CABECERA_SALES.HABER y a fila.Monto (caja). Poner
                    // ENTREGA=0 sin condición borraba abonos parciales previos LEGÍTIMOS de
                    // antes de este cobro (confirmado: crédito 10184 cuota 2 tenía un abono
                    // parcial previo real de Gs. 120.030 — quedó en 0 en vez de volver a ese
                    // valor). Se resta fila.Monto de ENTREGA actual en vez de resetear,
                    // reconstruyendo el estado previo a ESTE cobro puntual.
                    //
                    // TOTAL no puede revertirse igual que ENTREGA: en un cobro COMPLETO (no
                    // parcial) el SP pisa TOTAL=@Total en vez de acumularlo, así que restar
                    // fila.Monto de TOTAL da 0 en vez del monto base pendiente (confirmado:
                    // crédito 30951 cuota 3, TOTAL quedó en 0 en vez de volver a 477.726).
                    // Se recalcula TOTAL = MONTO + REAJUSTE en su lugar (PUNITORIO ya vuelve a
                    // 0 arriba). El cargo de Inforconf (GENERADAS.INFORCOM_APLICADO) es
                    // permanente por episodio de mora — si el REAJUSTE persistido coincide
                    // exactamente con VALOR_INFORCONF y el flag está en 1, se mantiene (no se
                    // vuelve a cobrar dentro del mismo episodio); en cualquier otro caso el
                    // REAJUSTE era ajuste manual puntual de este cobro y se resetea a 0.
                    // Bug real detectado (tercera parte, misma causa): el AND ESTADO=1 acá
                    // asumía que solo se anulan cobros que COMPLETARON la cuota — un abono
                    // PARCIAL nunca deja la cuota en ESTADO=1 (sigue en 0/Pendiente mientras no
                    // se complete), así que este UPDATE no tocaba ninguna fila al anular un
                    // abono parcial y ENTREGA quedaba huérfana para siempre (confirmado: crédito
                    // 2397 cuota 2, 3 abonos parciales anulados de 12.000+12.500+30.000, los 3
                    // con SUBTIPO correcto, pero ENTREGA se quedó en 54.500). COMPROBANTE+NCUOTA
                    // ya identifican la cuota exacta sin ambigüedad, no hace falta el filtro de
                    // estado.
                    var valorInforconf = await conn.ExecuteScalarAsync<decimal?>(
                        "SELECT TOP 1 VALOR_INFORCONF FROM CONFIGURACION") ?? 0;
                    await conn.ExecuteAsync(
                        "UPDATE GENERADAS SET ESTADO=0, FECHACOBRADO=NULL, MORA=0, PUNITORIO=0, " +
                        "ENTREGA = CASE WHEN ENTREGA - @monto < 0 THEN 0 ELSE ENTREGA - @monto END, " +
                        "REAJUSTE = CASE WHEN INFORCOM_APLICADO = 1 AND REAJUSTE = @valorInforconf THEN REAJUSTE ELSE 0 END " +
                        "WHERE COMPROBANTE=@comp AND NCUOTA=@nc",
                        new { comp = comprobante, nc = ncuota, monto = fila.Monto, valorInforconf });
                    await conn.ExecuteAsync(
                        "UPDATE GENERADAS SET TOTAL = MONTO + REAJUSTE " +
                        "WHERE COMPROBANTE=@comp AND NCUOTA=@nc",
                        new { comp = comprobante, nc = ncuota });

                    // Bug real detectado (segunda parte, misma causa): el SP de cobro suma el
                    // monto TOTAL cobrado (capital+punitorio+reajuste) a CABECERA_SALES.HABER
                    // — sin revertir esto acá, el crédito queda mostrando un saldo pagado
                    // mayor al real aunque la cuota ya vuelva a Pendiente (confirmado: crédito
                    // 10355, HABER quedó en 1.071.682 en vez de 561.050 tras anular la cuota 3).
                    // Se ubica el IDCAB vía GENERADAS.COMPROBANTE en vez de fila.ID_VENTA —
                    // CAJA_DETALLE.ID_VENTA viene NULL en la gran mayoría de cobros reales
                    // (confirmado: crédito 10559 — con fila.ID_VENTA la condición nunca
                    // corría y HABER quedaba con el cobro anulado sin revertir).
                    await conn.ExecuteAsync(
                        "UPDATE CABECERA_SALES SET HABER = HABER - @monto " +
                        "WHERE IDCAB = (SELECT TOP 1 IDCAB FROM GENERADAS WHERE COMPROBANTE=@comp)",
                        new { monto = fila.Monto, comp = comprobante });
                }
            }

            await Buscar();
        }
        catch (Exception ex) { MessageBox.Show($"Error al anular: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ── Dialog: editar/nuevo movimiento ─────────────────────────────────────────
internal class CajaEditarMovDialog : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;
    private readonly FilaExploradorCaja?  _fila;
    // Caja destino explícita para un movimiento NUEVO — cuando viene informado, se usa en vez
    // de buscar "la caja abierta del local logueado" (ver Guardar()). Necesario para agregar
    // movimientos a una caja YA CERRADA (ActualizarCajasCerradasWindow), donde no hay ninguna
    // caja abierta que buscar y el destino lo elige explícitamente el administrador.
    private readonly int?                 _idMasterForzado;
    private readonly int?                 _idLocalForzado;

    // controles
    private ComboBox _cboTipo = null!, _cboSubTipo = null!, _cboMetodo = null!;
    private TextBox  _txtMonto = null!, _txtNumDoc = null!, _txtConcepto = null!;
    private TextBlock _lblMontoGs = null!;
    private StackPanel _funcionarioSp = null!;
    private TextBox    _txtFuncionario = null!;
    // Selector de local — solo visible/habilitado para usuarios con PuedeVerTodosLosLocales
    // (hoy: administrador o código 67), y solo al crear un movimiento nuevo. Pedido explícito
    // 2026-08-04: el código 67 necesita poder cargar ingresos/egresos en la caja de CUALQUIER
    // local, no solo la de su propia sesión — antes el modal siempre tomaba
    // _session.LocalActual sin dar opción de elegir otro local.
    private StackPanel _localSp = null!;
    private ComboBox   _cboLocalDestino = null!;

    // estado
    private bool _esIngreso = false;
    private int? _idFuncionarioSeleccionado = null;

    private static System.Windows.Media.SolidColorBrush DB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    private bool EsNuevo => _fila == null;

    public CajaEditarMovDialog(IDbConnectionFactory db, ISessionService session, FilaExploradorCaja? fila,
        int? idMasterForzado = null, int? idLocalForzado = null)
    {
        _db = db; _session = session; _fila = fila;
        _idMasterForzado = idMasterForzado; _idLocalForzado = idLocalForzado;
        _esIngreso = fila == null ? false : fila.TipoDesc == "INGRESO";
        Title  = fila == null ? "Nuevo movimiento" : "Modificar movimiento";
        Width  = 540; Height = 540;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = DB("#F4F6F9");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        if (fila != null) PreCargar();
        ActualizarToggle();
        if (_cboLocalDestino != null) Loaded += async (_, _) => await CargarLocalesDestinoAsync();
    }

    // Puebla el selector de local con SOLO los que tienen caja abierta ahora mismo (ver
    // comentario en BuildUI) — preseleccionado en el local de la sesión actual del usuario si
    // ese local tiene caja abierta, para que el caso más común (cargar en su propio local) no
    // requiera tocar el combo, y quede a mano cambiarlo a cualquier otro cuando sí haga falta.
    private async Task CargarLocalesDestinoAsync()
    {
        try
        {
            var cajaRepo = App.Services.GetRequiredService<ICajaRepository>();
            var cajasAbiertas = (await cajaRepo.ListarCajasAbiertasAsync())
                .Select(c => new LocalConCajaAbierta(c.IdLocal, c.LocalNombre))
                .OrderBy(x => x.NombreLocal)
                .ToList();
            _cboLocalDestino.ItemsSource = cajasAbiertas;
            var idLocalSesion = _session.LocalActual?.IdLocal;
            _cboLocalDestino.SelectedItem = cajasAbiertas.FirstOrDefault(l => l.IdLocal == idLocalSesion)
                ?? cajasAbiertas.FirstOrDefault();
        }
        catch (Exception ex) { MessageBox.Show($"Error cargando locales con caja abierta: {ex.Message}"); }
    }

    // Item del selector de local: TextoBusqueda combina código+nombre para que TextSearch (al
    // tipear en el ComboBox editable) filtre por cualquiera de los dos, no solo por el nombre
    // visible. IdLocal se castea a Local? en Guardar() vía patrón, ver más abajo.
    private record LocalConCajaAbierta(int IdLocal, string NombreLocal)
    {
        public string TextoBusqueda => $"{IdLocal} {NombreLocal}";
        public override string ToString() => NombreLocal;
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Clear();

        // ── Panel principal destacado: borde punteado azul + ícono grande a la
        // izquierda + botones circulares Guardar/Cerrar — réplica del diseño del sistema
        // viejo ("Ingresar movimiento"), adaptado a la paleta azul del sistema nuevo en
        // vez del naranja original.
        var panel = new Border {
            Background = DB("#EAF2FB"), BorderBrush = DB("#1565C0"),
            BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(6),
            Margin = new Thickness(16) };
        var panelStack = new StackPanel { Margin = new Thickness(20,18,20,18) };

        // ── Fila superior: ícono grande (lápiz) + título, alineados en la misma línea ──
        // Segoe MDL2 Assets ya viene con Windows; se usa el escape \uXXXX explícito para
        // evitar que el glifo se corrompa al guardar el archivo como texto.
        var headRow = new Grid { Margin = new Thickness(0,0,0,16) };
        headRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBox = new Border {
            Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
            Background = DB("#1565C0"), Margin = new Thickness(0,0,14,0) };
        iconBox.Child = new TextBlock {
            Text = EsNuevo ? "" : "",
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 20,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(iconBox, 0); headRow.Children.Add(iconBox);

        var headTxtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headTxtStack.Children.Add(new TextBlock {
            Text = EsNuevo ? "Nuevo movimiento" : "Modificar movimiento",
            FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DB("#0D3B66") });
        if (!EsNuevo)
            headTxtStack.Children.Add(new TextBlock {
                Text = $"ID {_fila!.ID_DETALLE}", FontSize = 11, Foreground = DB("#5C7A94"),
                Margin = new Thickness(0,2,0,0) });
        Grid.SetColumn(headTxtStack, 1); headRow.Children.Add(headTxtStack);
        panelStack.Children.Add(headRow);

        // ── Cuerpo (campos) ────────────────────────────────────────────────
        var body = new StackPanel();
        panelStack.Children.Add(body);

        TextBlock Lbl(string t) => new TextBlock {
            Text = t, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = DB("#5C7A94"), Margin = new Thickness(1,0,0,6) };

        // ── Selector de local — solo visible para usuarios con PuedeVerTodosLosLocales
        // (administrador o código 67) y solo al crear un movimiento nuevo. Un movimiento
        // existente (EsNuevo=false) ya pertenece a una caja fija, no tiene sentido "mover" el
        // local desde acá. Pedido explícito 2026-08-04.
        //
        // Solo lista locales con CAJA ABIERTA ahora mismo (no todos los locales del sistema)
        // — evita que el 67 elija un local sin caja y recién se entere del error "No hay caja
        // abierta en este local" después de completar todo el formulario. IsEditable=true +
        // StaysOpenOnEdit permite escribir código o nombre para filtrar en vivo en vez de
        // desplegar toda la lista de locales abiertos a buscar a ojo. Mismo criterio que
        // ListarCajasAbiertasAsync (ya usado en el selector de "Confirmar entrega").
        var puedeElegirLocal = EsNuevo && (_session.UsuarioActual?.PuedeVerTodosLosLocales == true);
        if (puedeElegirLocal)
        {
            _localSp = new StackPanel { Margin = new Thickness(0,0,0,14) };
            _localSp.Children.Add(Lbl("LOCAL DESTINO (solo con caja abierta)"));
            _cboLocalDestino = new ComboBox { Height = 38, FontSize = 13,
                Padding = new Thickness(11,0,8,0), BorderBrush = DB("#CFD8DC"),
                BorderThickness = new Thickness(1.3), Background = System.Windows.Media.Brushes.White,
                IsEditable = true, IsTextSearchCaseSensitive = false };
            TextSearch.SetTextPath(_cboLocalDestino, "TextoBusqueda");
            _localSp.Children.Add(_cboLocalDestino);
            body.Children.Add(_localSp);
        }

        // Plantilla con esquinas redondeadas completas (por defecto WPF dibuja el borde de
        // TextBox como un rectángulo recto sin CornerRadius, aunque se le asigne Border*).
        Style TxtStyle() {
            var st = new Style(typeof(TextBox));
            var tpl = new ControlTemplate(typeof(TextBox));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.White);
            border.SetValue(Border.BorderBrushProperty, DB("#CFD8DC"));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.3));
            var scroller = new FrameworkElementFactory(typeof(ScrollViewer));
            scroller.Name = "PART_ContentHost";
            scroller.SetValue(MarginProperty, new Thickness(0));
            border.AppendChild(scroller);
            tpl.VisualTree = border;
            var trigFocus = new Trigger { Property = TextBox.IsFocusedProperty, Value = true };
            trigFocus.Setters.Add(new Setter(Border.BorderBrushProperty, DB("#1565C0"), "border"));
            trigFocus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1.6), "border"));
            tpl.Triggers.Add(trigFocus);
            st.Setters.Add(new Setter(TextBox.TemplateProperty, tpl));
            return st;
        }

        TextBox MkTxt(bool grande = false) => new TextBox {
            FontSize = grande ? 20 : 13, Padding = new Thickness(11, 9, 11, 9),
            FontWeight = grande ? FontWeights.Bold : FontWeights.Normal,
            Style = TxtStyle() };

        ComboBox MkCbo(double h = 38) => new ComboBox { Height = h, FontSize = 13,
            Padding = new Thickness(11,0,8,0), BorderBrush = DB("#CFD8DC"),
            BorderThickness = new Thickness(1.3), Background = System.Windows.Media.Brushes.White };

        // ── Layout compacto en grilla de 2 columnas, replicando el diseño del sistema
        // viejo ("Ingresar movimiento"): fila 1 Tipo+Monto, fila 2 SubTipo+NroDoc,
        // fila 3 Método+Funcionario (o vacío), fila 4 Concepto a todo el ancho.
        Grid Fila2Col() {
            var g = new Grid { Margin = new Thickness(0,0,0,16) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }
        void Celda(Grid g, int col, string lbl, UIElement ctrl) {
            var sp = new StackPanel();
            sp.Children.Add(Lbl(lbl));
            sp.Children.Add(ctrl);
            Grid.SetColumn(sp, col); g.Children.Add(sp);
        }

        // ── Fila 1: Tipo (Entrada/Salida) + Monto ─────────────────────────
        var row1 = Fila2Col();
        _cboTipo = MkCbo();
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Entrada" });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Salida" });
        _cboTipo.SelectedIndex = _esIngreso ? 0 : 1;
        _cboTipo.SelectionChanged += (_, _) => {
            if (EsReadOnly()) { _cboTipo.SelectedIndex = _esIngreso ? 0 : 1; return; }
            _esIngreso = _cboTipo.SelectedIndex == 0;
            ActualizarSubtipos();
            ActualizarToggle();
        };
        Celda(row1, 0, "TIPO", _cboTipo);

        _lblMontoGs = new TextBlock(); // se mantiene el campo por compatibilidad, sin uso visual propio ya
        _txtMonto = MkTxt();
        _txtMonto.TextAlignment = TextAlignment.Right;
        _txtMonto.Text = "0";
        bool _fmtBusy = false;
        _txtMonto.TextChanged += (_, _) => {
            if (_fmtBusy) return; _fmtBusy = true;
            var raw = new string(_txtMonto.Text.Where(char.IsDigit).ToArray());
            if (long.TryParse(raw, out var n)) {
                var fmt = n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                _txtMonto.Text = fmt; _txtMonto.CaretIndex = fmt.Length;
            }
            _fmtBusy = false;
        };
        _txtMonto.GotFocus += (_, _) => { if (_txtMonto.Text == "0") _txtMonto.SelectAll(); };
        Celda(row1, 2, "MONTO", _txtMonto);
        body.Children.Add(row1);

        // ── Fila 2: SubTipo + Número de Documento ─────────────────────────
        var row2b = Fila2Col();
        _cboSubTipo = MkCbo();
        Celda(row2b, 0, "SUBTIPO", _cboSubTipo);
        _txtNumDoc = MkTxt(); // alfanumérico: sin restricción de caracteres
        Celda(row2b, 2, "NÚMERO DOC.", _txtNumDoc);
        body.Children.Add(row2b);

        // ── Fila 3: Método de pago + Funcionario (solo con ANTICIPO/ADELANTO) ─────
        var row3 = Fila2Col();
        _cboMetodo = MkCbo();
        foreach (var m in new[] { "EFECTIVO","CHEQUE","TRANSFERENCIA","OTRO" })
            _cboMetodo.Items.Add(new ComboBoxItem { Content = m });
        _cboMetodo.SelectedIndex = 0;
        Celda(row3, 0, "MÉTODO", _cboMetodo);

        // Se guarda el ID real del funcionario en ID_ENTIDAD (columna existente en
        // CAJA_DETALLE, sin uso consistente hasta ahora) además de mantener el mismo texto
        // "[NOMBRE]" en el concepto — así no se rompe el cálculo automático de adelantos en
        // Pago de Salarios (que hoy matchea por nombre, ver PagosWindow.CalcularComisionesPeriodo),
        // y queda preparado el vínculo por ID para cuando se pueda migrar ese cálculo a usarlo.
        _funcionarioSp = new StackPanel { Visibility = Visibility.Collapsed };
        _funcionarioSp.Children.Add(Lbl("FUNCIONARIO"));
        var funcRow = new Grid();
        funcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        funcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var btnBuscarFunc = new Border {
            Height = 38, Width = 38, Margin = new Thickness(0,0,7,0),
            Background = DB("#1565C0"), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 14, Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        btnBuscarFunc.MouseLeftButtonUp += async (_, _) => await BuscarFuncionario();
        Grid.SetColumn(btnBuscarFunc, 0); funcRow.Children.Add(btnBuscarFunc);
        _txtFuncionario = MkTxt(); _txtFuncionario.IsReadOnly = true; _txtFuncionario.Cursor = Cursors.Hand;
        _txtFuncionario.MouseLeftButtonUp += async (_, _) => await BuscarFuncionario();
        Grid.SetColumn(_txtFuncionario, 1); funcRow.Children.Add(_txtFuncionario);
        _funcionarioSp.Children.Add(funcRow);
        Grid.SetColumn(_funcionarioSp, 2); row3.Children.Add(_funcionarioSp);
        body.Children.Add(row3);

        _cboSubTipo.SelectionChanged += (_, _) => ActualizarSelectorFuncionario();

        // ── Fila 4: Concepto, a todo el ancho ─────────────────────────────
        var conceptoSp = new StackPanel { Margin = new Thickness(0,0,0,8) };
        _txtConcepto = MkTxt(); _txtConcepto.MaxLength = 250;
        conceptoSp.Children.Add(Lbl("CONCEPTO"));
        conceptoSp.Children.Add(_txtConcepto);
        body.Children.Add(conceptoSp);

        // ── Advertencia venta ─────────────────────────────────────────────
        if (_fila != null && _fila.SubTipo is "VENTA" or "COBRO_S" or "COBRO_C") {
            var warn = new Border { Background = DB("#FFF8E1"), BorderBrush = DB("#FFD54F"),
                BorderThickness = new Thickness(0,0,0,3), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,0,0,8) };
            warn.Child = new TextBlock {
                Text = "Este movimiento fue generado por el sistema y solo puede modificarse el concepto.",
                Foreground = DB("#E65100"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            body.Children.Add(warn);
        }

        // ── Fila inferior del panel: botones circulares Guardar / Cerrar ──────
        // Réplica de los íconos grandes de disquete/X del sistema viejo, con Segoe MDL2
        // Assets (ya incluida en Windows) en vez de imágenes descargadas.
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,14,0,0) };

        Border MkCircBtn(string ico, string txt, string bg) {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            var circle = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
                Background = DB(bg), Cursor = Cursors.Hand,
                Child = new TextBlock { Text = ico, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 20, Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            sp.Children.Add(circle);
            sp.Children.Add(new TextBlock { Text = txt, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = DB("#546E7A"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,4,0,0) });
            return new Border { Child = sp, Cursor = Cursors.Hand };
        }
        var btnGuardar  = MkCircBtn("", "Guardar", "#1565C0");
        var btnCancelar = MkCircBtn("", "Cerrar",  "#78909C");
        btnGuardar.MouseLeftButtonUp  += async (_, _) => await Guardar();
        btnCancelar.MouseLeftButtonUp += (_, _) => Close();
        pieSp.Children.Add(btnGuardar);
        pieSp.Children.Add(btnCancelar);
        panelStack.Children.Add(pieSp);

        panel.Child = panelStack;
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Content = panel;
        root.Children.Add(scroll);
        Content = root;

        ActualizarSubtipos();
    }

    private bool EsReadOnly() => _fila != null && _fila.SubTipo is "VENTA" or "COBRO_S" or "COBRO_C" or "COBRO_SISTEMA";

    private void ActualizarToggle()
    {
        if (_txtMonto != null)
            _txtMonto.Foreground = _esIngreso ? DB("#1B5E20") : DB("#C62828");
    }

    private void ActualizarSubtipos()
    {
        if (_cboSubTipo == null) return;
        var prevSel = (_cboSubTipo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        _cboSubTipo.Items.Clear();
        // ANTICIPO y ADELANTO son sinónimos del mismo movimiento (anticipo de haberes a un
        // funcionario) — se dejan ambas etiquetas porque el cajero está acostumbrado a
        // "Adelanto" del sistema viejo, pero ambas disparan el mismo selector de funcionario
        // (ver ActualizarSelectorFuncionario) y graban igual en CAJA_DETALLE.
        string[] opciones = _esIngreso
            ? new[] { "APERTURA","ANTICIPO","COBRO","VENTA","OTRO_INGRESO" }
            : new[] { "GASTOS","PAGO","COMPRA","ANTICIPO","ADELANTO","TRANSFERENCIA","OTRO_EGRESO" };
        ComboBoxItem? selItem = null;
        foreach (var s in opciones) {
            var ci = new ComboBoxItem { Content = s, Tag = s };
            _cboSubTipo.Items.Add(ci);
            if (s == prevSel) selItem = ci;
        }
        _cboSubTipo.SelectedItem = selItem ?? _cboSubTipo.Items[0];
        ActualizarSelectorFuncionario();
    }

    private void ActualizarSelectorFuncionario()
    {
        if (_funcionarioSp == null || _cboSubTipo == null) return;
        var sel = (_cboSubTipo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var mostrar = sel is "ANTICIPO" or "ADELANTO";
        _funcionarioSp.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
        if (!mostrar) { _idFuncionarioSeleccionado = null; _txtFuncionario.Text = ""; }
    }

    private async Task BuscarFuncionario()
    {
        var usuarioRepo = App.Services.GetRequiredService<IUsuarioRepository>();
        var usuarios    = (await usuarioRepo.ListarTodosAsync()).ToList();
        if (usuarios.Count == 0) { MessageBox.Show("No hay usuarios registrados.", "Aviso"); return; }

        var seleccionado = SelectorModal.MostrarVendedores(this, usuarios);
        if (seleccionado == null) return;

        _idFuncionarioSeleccionado = seleccionado.IdUsuario;
        _txtFuncionario.Text = seleccionado.NombreUsuario;
        // Concepto autocompletado con el mismo formato que ya usa el sistema hoy
        // ("Anticipo de haberes: [NOMBRE] fecha"), para que el fix de PagosWindow (que
        // matchea por ese texto) siga funcionando sin cambios adicionales.
        if (string.IsNullOrWhiteSpace(_txtConcepto.Text))
            _txtConcepto.Text = $"Anticipo de haberes: [{seleccionado.NombreUsuario}] {DateTime.Today:dd/MM/yyyy}";
    }

    private void PreCargar()
    {
        if (_fila == null) return;
        _txtMonto.Text    = _fila.Monto.ToString("0");
        _txtNumDoc.Text   = _fila.Referencia == "---" ? "" : _fila.Referencia;
        _txtConcepto.Text = _fila.Concepto;
        _cboTipo.SelectedIndex = _esIngreso ? 0 : 1;
        ActualizarSubtipos();
        foreach (ComboBoxItem item in _cboSubTipo.Items)
            if (item.Content?.ToString() == _fila.SubTipo) { _cboSubTipo.SelectedItem = item; break; }
        foreach (ComboBoxItem item in _cboMetodo.Items)
            if (item.Content?.ToString() == _fila.FormaPago) { _cboMetodo.SelectedItem = item; break; }
        if (EsReadOnly()) {
            _cboTipo.IsEnabled = false;
            _cboSubTipo.IsEnabled = _cboMetodo.IsEnabled = false;
            _txtMonto.IsReadOnly = _txtNumDoc.IsReadOnly = false; // concepto editable
        }
    }

    private async Task Guardar()
    {
        if (!decimal.TryParse(new string(_txtMonto.Text.Where(char.IsDigit).ToArray()), out var monto) || monto <= 0) {
            MessageBox.Show("Ingrese un monto válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (string.IsNullOrWhiteSpace(_txtConcepto.Text)) {
            MessageBox.Show("El concepto es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var subTipoStr = ((ComboBoxItem)_cboSubTipo.SelectedItem).Content.ToString()!;
        if (subTipoStr is "ANTICIPO" or "ADELANTO" && _idFuncionarioSeleccionado == null) {
            MessageBox.Show("Seleccione el funcionario que recibe el anticipo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var cred = new CajaCredencialesDialog { Owner = this };
        if (cred.ShowDialog() != true) return;

        var metodoStr  = ((ComboBoxItem)_cboMetodo.SelectedItem).Content.ToString()!;
        var tipoChar   = _esIngreso ? "I" : "E";

        try
        {
            using var conn = _db.Create();
            if (EsNuevo) {
                int? master; int idLocalDestino;
                if (_idMasterForzado.HasValue) {
                    master = _idMasterForzado.Value;
                    idLocalDestino = _idLocalForzado ?? _session.LocalActual!.IdLocal;
                } else {
                    // Si el selector de local está visible (usuario con PuedeVerTodosLosLocales,
                    // ver BuildUI), se respeta el local elegido ahí en vez de forzar siempre el
                    // de la sesión — pedido explícito 2026-08-04: el código 67 necesita poder
                    // cargar movimientos en la caja de cualquier local, no solo el propio.
                    idLocalDestino = (_cboLocalDestino?.SelectedItem as LocalConCajaAbierta)?.IdLocal
                        ?? _session.LocalActual!.IdLocal;
                    master = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT TOP 1 ID_MASTER FROM CAJA_MASTER WHERE ID_LOCAL=@loc AND ESTADO='A' ORDER BY ID_MASTER DESC",
                        new { loc = idLocalDestino });
                    if (master == null) { MessageBox.Show("No hay caja abierta en este local.", "Sin caja", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                }

                // CajaCredencialesDialog solo valida que cred.UsuarioId sea un usuario VÁLIDO del
                // sistema (código+clave correctos) — no que sea dueño de ESTA caja. Sin este
                // chequeo adicional, cualquier usuario válido (ej. Mabel) podía tipear sus propias
                // credenciales acá y guardar en una caja que no abrió, aunque AbrirNuevo/
                // AbrirEdicion (CajaExploradorWindow) ya hubieran bloqueado el ACCESO a esta
                // pantalla para el usuario LOGUEADO — dos validaciones distintas, sobre usuarios
                // potencialmente distintos (logueado vs. el de las credenciales tipeadas acá).
                if (!await PuedeModificarCajaAsync(conn, master.Value, cred.UsuarioId)) {
                    MessageBox.Show("El usuario ingresado no es quien abrió esta caja ni es administrador.",
                        "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ID_ENTIDAD guarda el ID real del funcionario cuando el movimiento es un
                // Anticipo/Adelanto (ver BuscarFuncionario) — el concepto sigue llevando el
                // mismo texto "[NOMBRE]" que ya usa PagosWindow para el cálculo automático.
                await conn.ExecuteAsync(@"
                    INSERT INTO CAJA_DETALLE (ID_MASTER,ID_LOCAL,FECHA_HORA,TIPO,SUBTIPO,FORMA_PAGO,MONTO,ID_CAJERO,ID_ENTIDAD,CONCEPTO,REFERENCIA,ESTADO_REG)
                    VALUES (@master,@local,GETDATE(),@tipo,@subtipo,@metodo,@monto,@cajero,@idEntidad,@concepto,@numDoc,'V')",
                    new { master, local = idLocalDestino, tipo = tipoChar, subtipo = subTipoStr,
                          metodo = metodoStr, monto, cajero = cred.UsuarioId,
                          idEntidad = _idFuncionarioSeleccionado,
                          concepto = _txtConcepto.Text.Trim(),
                          numDoc = string.IsNullOrWhiteSpace(_txtNumDoc.Text) ? (string?)null : _txtNumDoc.Text.Trim() });
            } else {
                if (!await PuedeModificarCajaAsync(conn, _fila!.ID_MASTER, cred.UsuarioId)) {
                    MessageBox.Show("El usuario ingresado no es quien abrió esta caja ni es administrador.",
                        "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var setTipo = EsReadOnly() ? "" : "TIPO=@tipo, SUBTIPO=@subtipo, FORMA_PAGO=@metodo, MONTO=@monto, ";
                await conn.ExecuteAsync(
                    $"UPDATE CAJA_DETALLE SET {setTipo}CONCEPTO=@concepto, REFERENCIA=@numDoc WHERE ID_DETALLE=@id",
                    new { tipo = tipoChar, subtipo = subTipoStr, metodo = metodoStr, monto,
                          concepto = _txtConcepto.Text.Trim(),
                          numDoc = string.IsNullOrWhiteSpace(_txtNumDoc.Text) ? (string?)null : _txtNumDoc.Text.Trim(),
                          id = _fila!.ID_DETALLE });
            }
            DialogResult = true;
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // Valida que el usuario de las CREDENCIALES TIPEADAS en CajaCredencialesDialog (no el
    // usuario logueado — pueden ser distintos: CajaExploradorWindow ya validó el acceso del
    // logueado a esta pantalla, pero acá el cajero puede tipear las credenciales de CUALQUIER
    // usuario válido del sistema) sea quien abrió esa caja puntual o sea administrador.
    private static async Task<bool> PuedeModificarCajaAsync(IDbConnection conn, int idMaster, int idUsuarioCred)
    {
        // Trae CODIGO_USUARIO además de CARGO_USUARIO y evalúa con Usuario.PuedeVerTodosLosLocales
        // (misma lógica que ya usan las pantallas de consulta de Caja: administrador o código
        // 67) en vez de duplicar acá el chequeo de solo "ADMINISTRADOR" — antes esta validación
        // bloqueaba al código 67 igual que a cualquier otro usuario sin caja propia. Pedido
        // explícito 2026-08-04: el 67 necesita poder crear ingresos/egresos en cualquier caja.
        var cargoYCodigo = await conn.QueryFirstOrDefaultAsync<(string? Cargo, string? Codigo)>(
            "SELECT CARGO_USUARIO AS Cargo, CODIGO_USUARIO AS Codigo FROM USUARIOS WHERE ID_USUARIO = @id",
            new { id = idUsuarioCred });
        var usuarioCred = new Usuario { CargoUsuario = cargoYCodigo.Cargo ?? "", CodigoUsuario = cargoYCodigo.Codigo ?? "" };
        if (usuarioCred.PuedeVerTodosLosLocales) return true;

        var idUsuarioApe = await conn.ExecuteScalarAsync<int?>(
            "SELECT ID_USUARIO_APE FROM CAJA_MASTER WHERE ID_MASTER = @idMaster", new { idMaster });
        return idUsuarioApe.HasValue && idUsuarioApe.Value == idUsuarioCred;
    }
}

// ── Dialog: control de acceso (credenciales) ────────────────────────────────
public class CajaCredencialesDialog : Window
{
    public int    UsuarioId   { get; private set; }
    public string UsuarioNomb { get; private set; } = "";
    private TextBox     _txtCodigo = null!;
    private PasswordBox _txtClave  = null!;
    private readonly IDbConnectionFactory _db;

    private static System.Windows.Media.SolidColorBrush CB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaCredencialesDialog()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title  = "Control de Acceso";
        Width  = 360; Height = 240;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        var hdr = new Border { Background = CB("#283593"), Padding = new Thickness(14, 10, 14, 10) };
        hdr.Child = new TextBlock { Text = "CONTROL DE ACCESO",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = CB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnAcep = new Button { Content = "✔ Aceptar", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = CB("#1B5E20"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(0,0,8,0) };
        var btnCan = new Button { Content = "Cerrar", Height = 32, Padding = new Thickness(12,0,12,0),
            Background = CB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnAcep.Click += async (_, _) => await Validar();
        btnCan.Click  += (_, _) => Close();
        pieSp.Children.Add(btnAcep); pieSp.Children.Add(btnCan);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var form = new Grid { Margin = new Thickness(20, 16, 20, 0) };
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddRow(string label, UIElement ctrl, int row) {
            var lbl = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold,
                Foreground = CB("#37474F"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0,0,8,12) };
            Grid.SetRow(lbl,  row); Grid.SetColumn(lbl,  0); form.Children.Add(lbl);
            Grid.SetRow(ctrl, row); Grid.SetColumn(ctrl, 1);
            if (ctrl is FrameworkElement fe) fe.Margin = new Thickness(0,0,0,12);
            form.Children.Add(ctrl);
        }

        _txtCodigo = new TextBox { Padding = new Thickness(8,5,8,5) };
        _txtClave  = new PasswordBox { Padding = new Thickness(8,5,8,5) };
        _txtClave.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Validar(); };

        AddRow("CODIGO",     _txtCodigo, 0);
        AddRow("CONTRASEÑA", _txtClave,  1);

        root.Children.Add(form);
        Content = root;
        Loaded += (_, _) => _txtCodigo.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async Task Validar()
    {
        var codigo = _txtCodigo.Text.Trim();
        var clave  = _txtClave.Password;
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(clave)) {
            MessageBox.Show("Ingrese código y contraseña.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        try
        {
            using var conn = _db.Create();
            var user = await conn.QueryFirstOrDefaultAsync<(int Id, string Nombre)>(
                @"SELECT ID_USUARIO, ISNULL(NOMBRE_USUARIO,'') FROM USUARIOS
                  WHERE CODIGO_USUARIO = @cod AND CONTRASEÑA_USUARIO = @clave",
                new { cod = codigo, clave });
            if (user.Id == 0) {
                MessageBox.Show("Código o contraseña incorrectos.", "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtClave.Password = "";
                _txtClave.Focus();
                return;
            }
            UsuarioId   = user.Id;
            UsuarioNomb = user.Nombre;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal class FilaExploradorCaja
{
    public long    ID_DETALLE   { get; set; }
    public int     ID_MASTER    { get; set; }
    public int?    ID_VENTA     { get; set; }
    public string  FechaHoraStr { get; set; } = "";
    public string  LocalNombre  { get; set; } = "";
    // "Cajero" = quién operaba físicamente esta caja (CAJA_DETALLE.ID_CAJERO, a quien queda
    // atribuido el arqueo). "Cobrador" = a quién se le atribuye la venta/comisión de este
    // movimiento (CAJA_DETALLE.ID_VENDEDOR, ver "Cobrado por" en CobrosWindow) — pueden ser
    // personas distintas, ej. un vendedor le pide a otro cajero que le cobre una cuota porque
    // su propia sesión/máquina falla, y sin esta columna el monto quedaba atribuido solo al
    // cajero, sin forma de saber a quién correspondía realmente la venta/cobro.
    public string  Cajero       { get; set; } = "";
    public string  Cobrador     { get; set; } = "";
    public string  TipoDesc     { get; set; } = "";
    public string  SubTipo      { get; set; } = "";
    public decimal Monto        { get; set; }
    public string  Concepto     { get; set; } = "";
    public string  Referencia   { get; set; } = "";
    public string  Receptor     { get; set; } = "";
    public string  FormaPago    { get; set; } = "";
    public string  EstadoDesc   { get; set; } = "";
    public bool    Anulado      => EstadoDesc == "ANULADO";
    public int     ID_CAJERO    { get; set; }
    public int     ID_ENTIDAD   { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
//  VER GASTOS DE CAJA
// ══════════════════════════════════════════════════════════════════════════════
public class CajaGastosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService     _session;
    private DatePicker _dtDesde = null!, _dtHasta = null!;
    private DataGrid   _grid    = null!;
    private TextBlock  _lblTotal = null!, _lblCant = null!;
    private TextBox    _txtLocal = null!;
    private int?       _idLocal;
    private List<FilaGasto> _todos = new();

    // Paginación
    private int  _pagina      = 1;
    private int  _porPagina   = 50;
    private int  _totalReg    = 0;
    private bool _cargando    = false;
    private TextBlock _lblPagInfo = null!;
    private Border    _btnPrev = null!, _btnNext = null!;
    private readonly List<(Border chip, TextBlock txt, int val)> _chips = new();

    private static System.Windows.Media.SolidColorBrush GB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaGastosWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title = "Egresos / Gastos de Caja";
        Width = 1100; Height = 640;
        MinWidth = 900; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var AzulOscuro = GB("#0E2F44");
        var AzulBase   = GB("#1A4F6E");
        var AzulMedio  = GB("#154360");
        var AzulClaro  = GB("#1F6089");
        var AzulMuted  = GB("#7FB3D3");
        var Blanco     = System.Windows.Media.Brushes.White;
        var FondoApp   = GB("#F0F2F5");
        var FondoCard  = System.Windows.Media.Brushes.White;

        var root = new DockPanel();
        Content  = root;

        // ── Header ──────────────────────────────────────────────────────────
        var hdr = new Border { Background = AzulOscuro, Padding = new Thickness(24, 14, 24, 14) };
        var hdrRow = new Grid();
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hdrStack = new StackPanel();
        hdrStack.Children.Add(new TextBlock {
            Text = "EGRESOS / GASTOS DE CAJA",
            Foreground = Blanco, FontSize = 17, FontWeight = FontWeights.Bold });
        hdrStack.Children.Add(new TextBlock {
            Text = "Consulta de movimientos de egreso por rango de fechas",
            Foreground = AzulMuted, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        hdrRow.Children.Add(hdrStack);
        hdr.Child = hdrRow;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Barra de filtros ─────────────────────────────────────────────────
        var fBar = new Border { Background = AzulBase, Padding = new Thickness(20, 11, 20, 11) };
        var fGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        foreach (var w in new[] { "A","140","A","140","A","200","A","A","A","*" })
            fGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = w == "A" ? GridLength.Auto :
                        w == "*" ? new GridLength(1, GridUnitType.Star) :
                        new GridLength(double.Parse(w)) });

        System.Windows.Media.SolidColorBrush lFg = Blanco;
        TextBlock FL(string t, int col) {
            var tb = new TextBlock { Text = t, Foreground = lFg,
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(col == 0 ? 0 : 14, 0, 6, 0) };
            Grid.SetColumn(tb, col); fGrid.Children.Add(tb); return tb;
        }

        // Estilo "chip" (fondo blanco + borde marcado) en vez del DatePicker plano por
        // defecto — sobre la barra de filtro azul oscuro, el control por defecto se
        // perdía visualmente contra el header de arriba.
        var dpStyleFiltro = CrediSoft.UI.Views.Shared.UiStyles.ModernDatePickerStyle();
        _dtDesde = new DatePicker { SelectedDate = DateTime.Today.AddMonths(-1), Style = dpStyleFiltro,
            Width = 138, VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = GB("#4FC3F7"), BorderThickness = new Thickness(2) };
        _dtHasta = new DatePicker { SelectedDate = DateTime.Today, Style = dpStyleFiltro,
            Width = 138, VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = GB("#4FC3F7"), BorderThickness = new Thickness(2) };

        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) puede consultar egresos de "Todos los locales" o
        // elegir uno distinto al propio. Un usuario normal solo ve los gastos de SU local —
        // el filtro queda fijo desde el inicio, sin boton de seleccionar/limpiar.
        var esAdmin = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;

        _txtLocal = new TextBox {
            IsReadOnly = true, IsEnabled = false,
            Text = "Todos los locales", FontStyle = FontStyles.Italic,
            Padding = new Thickness(10, 6, 10, 6), FontSize = 12,
            Background = GB("#1F6089"), Foreground = GB("#D6EAF8"),
            BorderBrush = GB("#4A7FA5"), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center };

        var btnLimpLoc = new Button {
            Content = "✕", Height = 32, Width = 32, Margin = new Thickness(4, 0, 0, 0),
            Background = GB("#37474F"), Foreground = Blanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Quitar filtro de local" };
        btnLimpLoc.Click += async (_, _) => {
            _idLocal = null;
            _txtLocal.Text = "Todos los locales";
            _txtLocal.FontStyle = FontStyles.Italic;
            btnLimpLoc.Visibility = Visibility.Collapsed;
            _pagina = 1; await Cargar();
        };

        var btnSelLoc = new Button {
            Height = 32, Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(6, 0, 0, 0),
            Background = AzulClaro, Foreground = Blanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11, FontWeight = FontWeights.SemiBold, Content = "🏪 Seleccionar" };
        btnSelLoc.Click += async (_, _) => {
            var modal = new ArqueoLocalSelectorModal(_db) { Owner = this };
            if (modal.ShowDialog() != true || modal.LocalId == null) return;
            _idLocal = modal.LocalId;
            _txtLocal.Text = modal.LocalNombre;
            _txtLocal.FontStyle = FontStyles.Normal;
            btnLimpLoc.Visibility = Visibility.Visible;
            _pagina = 1; await Cargar();
        };

        if (!esAdmin)
        {
            _idLocal = _session.LocalActual?.IdLocal;
            _txtLocal.Text = _session.LocalActual?.NombreLocal ?? "Mi local";
            _txtLocal.FontStyle = FontStyles.Normal;
            btnSelLoc.Visibility = Visibility.Collapsed;
            btnLimpLoc.Visibility = Visibility.Collapsed;
        }

        _dtDesde.SelectedDateChanged += async (_, _) => { _pagina = 1; await Cargar(); };
        _dtHasta.SelectedDateChanged += async (_, _) => { _pagina = 1; await Cargar(); };

        FL("Desde:", 0);
        Grid.SetColumn(_dtDesde, 1); fGrid.Children.Add(_dtDesde);
        FL("Hasta:", 2);
        Grid.SetColumn(_dtHasta, 3); fGrid.Children.Add(_dtHasta);
        FL("Local:", 4);
        Grid.SetColumn(_txtLocal, 5); fGrid.Children.Add(_txtLocal);
        Grid.SetColumn(btnSelLoc, 6); fGrid.Children.Add(btnSelLoc);
        Grid.SetColumn(btnLimpLoc, 7); fGrid.Children.Add(btnLimpLoc);

        fBar.Child = fGrid;
        DockPanel.SetDock(fBar, Dock.Top); root.Children.Add(fBar);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border { Background = AzulMedio, Padding = new Thickness(14, 8, 14, 8) };
        var footRoot = new Grid();
        footRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Fila 1: totales + botones ────────────────────────────────────────
        var fila1 = new Grid();
        fila1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _lblCant = new TextBlock {
            Foreground = AzulMuted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
        _lblTotal = new TextBlock {
            Foreground = Blanco, FontSize = 13.5, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        fila1.Children.Add(_lblCant);
        var f1Right = new StackPanel { Orientation = Orientation.Horizontal };

        var btnImprimir = new Button {
            Content = "🖨  Imprimir", Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Background = AzulClaro, Foreground = Blanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0) };
        btnImprimir.Click += (_, _) => Imprimir();

        var btnCerrar = new Button {
            Content = "✕  Cerrar", Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Background = GB("#37474F"), Foreground = Blanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        btnCerrar.Click += (_, _) => Close();

        f1Right.Children.Add(_lblTotal);
        f1Right.Children.Add(btnImprimir);
        f1Right.Children.Add(btnCerrar);
        Grid.SetColumn(f1Right, 1); fila1.Children.Add(f1Right);
        Grid.SetRow(fila1, 0); footRoot.Children.Add(fila1);

        // ── Fila 2: paginación ───────────────────────────────────────────────
        var fila2 = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 7, 0, 0)
        };

        // Chips filas por página
        var chipsSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0) };
        chipsSp.Children.Add(new TextBlock {
            Text = "Filas:", Foreground = AzulMuted, FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
        });

        Border MkChip(int val)
        {
            var txt = new TextBlock {
                Text = val == int.MaxValue ? "Todo" : val.ToString(),
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var chip = new Border {
                CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 3, 11, 3),
                Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand,
                Background = GB("#1A4F6E"), BorderThickness = new Thickness(1),
                BorderBrush = GB("#2E7EA6")
            };
            txt.Foreground = AzulMuted;
            chip.Child = txt;
            _chips.Add((chip, txt, val));
            chip.MouseLeftButtonUp += async (_, _) => {
                if (_porPagina == val) return;
                _porPagina = val; _pagina = 1;
                UpdateChips(); UpdateNav();
                await Cargar();
            };
            chipsSp.Children.Add(chip);
            return chip;
        }
        MkChip(20); MkChip(50); MkChip(100); MkChip(int.MaxValue);
        fila2.Children.Add(chipsSp);

        // Separador vertical
        fila2.Children.Add(new Border {
            Width = 1, Background = GB("#2E7EA6"), Margin = new Thickness(0, 2, 16, 2)
        });

        // Botón anterior
        _btnPrev = new Border {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
            Background = GB("#1A4F6E"), BorderBrush = GB("#2E7EA6"), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnPrev.Child = new TextBlock {
            Text = "←", Foreground = Blanco, FontSize = 14, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        _btnPrev.MouseLeftButtonUp += async (_, _) => {
            if (_pagina <= 1) return;
            _pagina--; UpdateNav(); await Cargar();
        };

        _lblPagInfo = new TextBlock {
            Foreground = Blanco, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0),
            MinWidth = 80, TextAlignment = TextAlignment.Center
        };

        _btnNext = new Border {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
            Background = GB("#1A4F6E"), BorderBrush = GB("#2E7EA6"), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnNext.Child = new TextBlock {
            Text = "→", Foreground = Blanco, FontSize = 14, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        _btnNext.MouseLeftButtonUp += async (_, _) => {
            int totalPag = _porPagina == int.MaxValue ? 1 : (int)Math.Ceiling(_totalReg / (double)_porPagina);
            if (_pagina >= totalPag) return;
            _pagina++; UpdateNav(); await Cargar();
        };

        fila2.Children.Add(_btnPrev);
        fila2.Children.Add(_lblPagInfo);
        fila2.Children.Add(_btnNext);

        Grid.SetRow(fila2, 1); footRoot.Children.Add(fila2);
        footer.Child = footRoot;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        // ── Grilla ───────────────────────────────────────────────────────────
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, AzulBase));
        colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, Blanco));
        colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty,   11.5));
        colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty,    new Thickness(12, 9, 12, 9)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, GB("#155980")));

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        var anulTrigger = new DataTrigger {
            Binding = new System.Windows.Data.Binding("Anulado"), Value = true };
        anulTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, GB("#FFEBEE")));
        anulTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, GB("#C62828")));
        rowStyle.Triggers.Add(anulTrigger);

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 34,
            FontSize = 12.5, BorderThickness = new Thickness(0),
            Background = FondoCard,
            AlternatingRowBackground = GB("#F4F8FA"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = GB("#E8EEF3"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ColumnHeaderStyle = colHdrStyle, RowStyle = rowStyle,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserSortColumns = true, CanUserResizeColumns = true };

        DataGridTextColumn GC(string h, string p, double px, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn {
                Header = h, SortMemberPath = p,
                Width  = new DataGridLength(px, DataGridLengthUnitType.Pixel),
                Binding = new System.Windows.Data.Binding(p) };
            var es = new Style(typeof(TextBlock));
            es.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, a));
            es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
            es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            c.ElementStyle = es; return c;
        }
        DataGridTextColumn GCStar(string h, string p, double star = 1.0) {
            var c = new DataGridTextColumn {
                Header = h, SortMemberPath = p,
                Width  = new DataGridLength(star, DataGridLengthUnitType.Star),
                Binding = new System.Windows.Data.Binding(p) };
            var es = new Style(typeof(TextBlock));
            es.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(8, 0, 8, 0)));
            es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            c.ElementStyle = es; return c;
        }

        _grid.Columns.Add(GC    ("Local",        "LocalNombre",  120));
        _grid.Columns.Add(GC    ("Fecha",         "FechaStr",     128));
        _grid.Columns.Add(GC    ("SubTipo",       "SubTipo",       80));
        _grid.Columns.Add(GC    ("Monto Gs.",     "Monto",        105, TextAlignment.Right));
        _grid.Columns.Add(GCStar("Concepto",      "Concepto",     2.0));
        _grid.Columns.Add(GC    ("Nº Doc.",       "Referencia",    80, TextAlignment.Center));
        _grid.Columns.Add(GCStar("Cajero",        "Cajero",       1.0));
        _grid.Columns.Add(GCStar("Beneficiario",  "Beneficiario", 1.0));
        _grid.Columns.Add(GC    ("Estado",        "EstadoStr",     70, TextAlignment.Center));

        var dgBorder = new Border {
            Margin = new Thickness(10), Background = FondoCard,
            BorderBrush = GB("#CBD8E1"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 1, BlurRadius = 6, Opacity = 0.08,
                Color = System.Windows.Media.Colors.Black },
            Child = _grid };
        root.Children.Add(dgBorder);
    }

    private void UpdateChips()
    {
        foreach (var (chip, txt, val) in _chips)
        {
            bool sel = val == _porPagina;
            chip.Background   = sel ? System.Windows.Media.Brushes.White : GB("#1A4F6E");
            chip.BorderBrush  = sel ? System.Windows.Media.Brushes.White : GB("#2E7EA6");
            txt.Foreground    = sel ? GB("#0E2F44") : GB("#7FB3D3");
        }
    }

    private void UpdateNav()
    {
        int totalPag = _porPagina == int.MaxValue ? 1
            : Math.Max(1, (int)Math.Ceiling(_totalReg / (double)_porPagina));

        _lblPagInfo.Text = $"Pág. {_pagina} / {totalPag}";

        bool prevOk = _pagina > 1;
        bool nextOk = _pagina < totalPag;

        _btnPrev.Opacity = prevOk ? 1 : 0.35;
        _btnPrev.Cursor  = prevOk ? Cursors.Hand : Cursors.Arrow;
        _btnNext.Opacity = nextOk ? 1 : 0.35;
        _btnNext.Cursor  = nextOk ? Cursors.Hand : Cursors.Arrow;
    }

    private async Task Cargar()
    {
        if (_cargando) return;
        _cargando = true;
        try
        {
            var desde = _dtDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
            var hasta = (_dtHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);
            int porPag  = _porPagina == int.MaxValue ? 100_000 : _porPagina;
            int offset  = (_pagina - 1) * porPag;

            var where = "WHERE CD.TIPO = 'E' AND CD.FECHA_HORA BETWEEN @desde AND @hasta"
                      + (_idLocal.HasValue ? " AND CD.ID_LOCAL = @idLocal" : "");

            var sqlBase = @"
                FROM CAJA_DETALLE CD
                LEFT JOIN LOCALES  L  ON L.ID_LOCAL   = CD.ID_LOCAL
                LEFT JOIN USUARIOS UC ON UC.ID_USUARIO = CD.ID_CAJERO
                LEFT JOIN USUARIOS UE ON UE.ID_USUARIO = CD.ID_ENTIDAD
                " + where;

            // COUNT total + SUM válidos en una sola query
            var sqlCount = $@"
                SELECT
                    COUNT(*)                                                   AS Total,
                    SUM(CASE CD.ESTADO_REG WHEN 'A' THEN 0 ELSE CD.MONTO END) AS SumaValidos,
                    SUM(CASE CD.ESTADO_REG WHEN 'A' THEN 1 ELSE 0 END)        AS Anulados
                {sqlBase}";

            // Página de datos — ROW_NUMBER para compatibilidad SQL Server 2008
            var sqlData = $@"
                SELECT ID_DETALLE, LocalNombre, FechaStr, SubTipo, MONTO,
                       Concepto, Referencia, Cajero, Beneficiario, EstadoStr, Anulado, FechaOrd
                FROM (
                    SELECT
                        CD.ID_DETALLE,
                        ISNULL(L.NOMBRE, CAST(CD.ID_LOCAL AS VARCHAR)) AS LocalNombre,
                        CONVERT(VARCHAR(16), CD.FECHA_HORA, 103) + ' ' +
                            CONVERT(VARCHAR(5), CD.FECHA_HORA, 108)    AS FechaStr,
                        CD.SUBTIPO   AS SubTipo,
                        CD.MONTO,
                        ISNULL(CD.CONCEPTO,   '') AS Concepto,
                        ISNULL(CD.REFERENCIA, '') AS Referencia,
                        ISNULL(UC.NOMBRE_USUARIO, '') AS Cajero,
                        ISNULL(UE.NOMBRE_USUARIO, '') AS Beneficiario,
                        CASE CD.ESTADO_REG WHEN 'A' THEN 'Anulado' ELSE '' END AS EstadoStr,
                        CAST(CASE CD.ESTADO_REG WHEN 'A' THEN 1 ELSE 0 END AS BIT) AS Anulado,
                        CD.FECHA_HORA AS FechaOrd,
                        ROW_NUMBER() OVER (ORDER BY CD.FECHA_HORA DESC) AS __rn
                    {sqlBase}
                ) __p
                WHERE __rn BETWEEN {offset + 1} AND {offset + porPag}";

            var prm = new { desde, hasta, idLocal = _idLocal };

            using var conn  = _db.Create();
            using var multi = await conn.QueryMultipleAsync(sqlCount + "; " + sqlData, prm, commandTimeout: 60);

            var cnt = await multi.ReadFirstAsync<dynamic>();
            _totalReg = (int)cnt.Total;
            decimal sumaValidos = (decimal)(cnt.SumaValidos ?? 0);
            int anulados        = (int)(cnt.Anulados ?? 0);
            int validos         = _totalReg - anulados;

            _todos = (await multi.ReadAsync<FilaGasto>()).ToList();
            _grid.ItemsSource = _todos;

            _lblCant.Text = anulados > 0
                ? $"{_totalReg} registros  ({validos} válidos, {anulados} anulados)"
                : $"{_totalReg} registros";
            _lblTotal.Text = $"Total egresos: Gs. {sumaValidos:N0}";

            UpdateChips();
            UpdateNav();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar gastos: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cargando = false; }
    }

    private async void Imprimir()
    {
        if (_totalReg == 0)
        {
            MessageBox.Show("No hay datos para imprimir.", "Imprimir", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Cargar TODOS los registros para el impreso (sin paginación)
        List<FilaGasto> todosParaImpreso;
        try
        {
            var desde = _dtDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
            var hasta = (_dtHasta.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);
            var where = "WHERE CD.TIPO = 'E' AND CD.FECHA_HORA BETWEEN @desde AND @hasta"
                      + (_idLocal.HasValue ? " AND CD.ID_LOCAL = @idLocal" : "");
            var sql = $@"
                SELECT
                    CD.ID_DETALLE,
                    ISNULL(L.NOMBRE, CAST(CD.ID_LOCAL AS VARCHAR)) AS LocalNombre,
                    CONVERT(VARCHAR(16), CD.FECHA_HORA, 103) + ' ' +
                        CONVERT(VARCHAR(5), CD.FECHA_HORA, 108) AS FechaStr,
                    CD.SUBTIPO AS SubTipo, CD.MONTO,
                    ISNULL(CD.CONCEPTO,   '') AS Concepto,
                    ISNULL(CD.REFERENCIA, '') AS Referencia,
                    ISNULL(UC.NOMBRE_USUARIO, '') AS Cajero,
                    ISNULL(UE.NOMBRE_USUARIO, '') AS Beneficiario,
                    CASE CD.ESTADO_REG WHEN 'A' THEN 'Anulado' ELSE '' END AS EstadoStr,
                    CAST(CASE CD.ESTADO_REG WHEN 'A' THEN 1 ELSE 0 END AS BIT) AS Anulado,
                    CD.FECHA_HORA AS FechaOrd
                FROM CAJA_DETALLE CD
                LEFT JOIN LOCALES  L  ON L.ID_LOCAL   = CD.ID_LOCAL
                LEFT JOIN USUARIOS UC ON UC.ID_USUARIO = CD.ID_CAJERO
                LEFT JOIN USUARIOS UE ON UE.ID_USUARIO = CD.ID_ENTIDAD
                {where}
                ORDER BY CD.FECHA_HORA DESC";
            using var conn = _db.Create();
            todosParaImpreso = (await conn.QueryAsync<FilaGasto>(sql,
                new { desde, hasta, idLocal = _idLocal }, commandTimeout: 120)).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al preparar impresión: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var pagina = new CajaGastosPagina
        {
            Filas       = todosParaImpreso.Select(r => new FilaGastoImp(
                r.LocalNombre, r.FechaStr, r.SubTipo, r.Monto,
                r.Referencia, r.Concepto, r.EstadoStr,
                r.Cajero, r.Beneficiario, r.Anulado)).ToList(),
            Desde       = _dtDesde.SelectedDate?.ToString("dd/MM/yyyy") ?? "-",
            Hasta       = _dtHasta.SelectedDate?.ToString("dd/MM/yyyy") ?? "-",
            LocalFiltro = (_txtLocal.Text == "Todos los locales" || string.IsNullOrWhiteSpace(_txtLocal.Text))
                          ? "TODOS LOS LOCALES" : _txtLocal.Text.ToUpperInvariant(),
            FechaImp    = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario     = "Sistema",
            LogoPath    = CajaGastosPagina.ResolverLogoPath(),
        };

        var preview = new CajaGastosPreviewWindow(pagina) { Owner = this };
        preview.ShowDialog();
    }
}

internal class FilaGasto
{
    public long    ID_DETALLE   { get; set; }
    public string  LocalNombre  { get; set; } = "";
    public string  FechaStr     { get; set; } = "";
    public string  SubTipo      { get; set; } = "";
    public decimal Monto        { get; set; }
    public string  Concepto     { get; set; } = "";
    public string  Referencia   { get; set; } = "";
    public string  Cajero       { get; set; } = "";
    public string  Beneficiario { get; set; } = "";
    public string  EstadoStr    { get; set; } = "";
    public bool    Anulado      { get; set; }
    public DateTime FechaOrd    { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HELPERS COMPARTIDOS (métodos de fábrica de UI)
// ══════════════════════════════════════════════════════════════════════════════

internal static class UiHelpers
{
    internal static Border MakeHdr(string text, string hex, Dock dock)
    {
        var b = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
            Padding = new Thickness(12, 6, 12, 6)
        };
        b.Child = new TextBlock { Text = text, Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(b, dock);
        return b;
    }

    internal static DataGrid MakeGrid() => new DataGrid {
        AutoGenerateColumns = false, IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Single,
        AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite,
        Margin = new Thickness(8, 0, 8, 0)
    };

    internal static DataGridTextColumn Col(string header, string binding, object width)
    {
        var col = new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(binding) };
        col.Width = width is DataGridLength dgl ? dgl : new DataGridLength((double)(int)width);
        return col;
    }

    internal static DataGridTextColumn Col(string header, string binding, int width)
        => new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(binding), Width = width };

    internal static Button Btn(string text, string hex) => new Button
    {
        Content = text, Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand
    };
}

