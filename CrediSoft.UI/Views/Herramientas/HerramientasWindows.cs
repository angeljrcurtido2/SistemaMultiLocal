using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.UI.Views.Informes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Herramientas;

// ══════════════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN DE PUNITORIO
// ══════════════════════════════════════════════════════════════════════════════
public class PunitorioWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox  _txtValorPunit = null!;
    private TextBlock _lblActual    = null!;
    private int      _idConfig;

    private static System.Windows.Media.SolidColorBrush PBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public PunitorioWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Configuración de Punitorio";
        Width = 460; Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = PBC("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // Header con gradiente naranja
        var hdr = new Border { Padding = new Thickness(20, 16, 20, 16) };
        var grad = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(230, 81, 0),
            System.Windows.Media.Color.FromRgb(255, 143, 0), 0);
        hdr.Background = grad;
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "⚙  Configuración de Punitorio",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock {
            Text = "Solo los administradores pueden modificar este valor",
            Foreground = PBC("#FFE0B2"), FontSize = 10, Margin = new Thickness(0,3,0,0) });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // Pie con botones
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = PBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16, 10, 16, 10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnGuardar = new Button { Content = "💾  Guardar",
            Height = 36, Padding = new Thickness(20,0,20,0), Margin = new Thickness(0,0,10,0),
            Background = PBC("#E65100"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13 };
        btnGuardar.Click += async (_, _) => await Guardar();
        var btnCerrar = new Button { Content = "✖  Cerrar",
            Height = 36, Padding = new Thickness(20,0,20,0),
            Background = PBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13 };
        btnCerrar.Click += (_, _) => Close();
        pieSp.Children.Add(btnGuardar); pieSp.Children.Add(btnCerrar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // Cuerpo
        var body = new StackPanel { Margin = new Thickness(28, 20, 28, 0) };

        // Card valor actual
        var card = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = PBC("#FFB74D"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 20) };
        var cardSp = new StackPanel { Orientation = Orientation.Horizontal };
        var iconBorder = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(21),
            Background = PBC("#FFF3E0"), Margin = new Thickness(0,0,14,0) };
        iconBorder.Child = new TextBlock { Text = "%", FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = PBC("#E65100"), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        cardSp.Children.Add(iconBorder);
        var cardTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        cardTxt.Children.Add(new TextBlock { Text = "Valor actual de punitorio",
            FontSize = 11, Foreground = PBC("#757575") });
        _lblActual = new TextBlock { Text = "—", FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = PBC("#E65100") };
        cardTxt.Children.Add(_lblActual);
        cardSp.Children.Add(cardTxt);
        card.Child = cardSp;
        body.Children.Add(card);

        // Campo nuevo valor
        body.Children.Add(new TextBlock { Text = "Nuevo valor punitorio (%):",
            FontWeight = FontWeights.SemiBold, Foreground = PBC("#424242"),
            Margin = new Thickness(0,0,0,6) });
        _txtValorPunit = new TextBox { Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = PBC("#FFB74D"), BorderThickness = new Thickness(1),
            FontSize = 15, FontWeight = FontWeights.Bold };
        body.Children.Add(_txtValorPunit);

        root.Children.Add(body);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async Task Cargar()
    {
        try {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>("CARGAR_PUNITORIO_CS", p, commandType: CommandType.StoredProcedure);
            if (row != null) {
                _idConfig = (int)row.ID_CONFIG;
                var val = (decimal)row.VALOR_PUNITORIO;
                _lblActual.Text = val.ToString("F2") + " %";
                _txtValorPunit.Text = val.ToString("F2");
            }
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar: {ex.Message}");
        }
    }

    private async Task Guardar()
    {
        if (!decimal.TryParse(_txtValorPunit.Text.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var vPunit) || vPunit < 0) {
            MessageBox.Show("Ingrese un valor numérico válido.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        // Solicitar contraseña de administrador
        if (!ValidarAdmin()) return;

        try {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@Id",    (byte)_idConfig);
            p.Add("@Valor", vPunit);
            p.Add("@msg",   dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("ACTUALIZAR_PUNITORIO_CS", p, commandType: CommandType.StoredProcedure);
            _lblActual.Text = vPunit.ToString("F2") + " %";
            MessageBox.Show("Valor de punitorio actualizado correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (Exception ex) {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ValidarAdmin()
    {
        var dlg = new Window {
            Title = "Autorización requerida", Width = 340, Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = PBC("#FAFAFA"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        bool ok = false;
        var dp = new DockPanel();

        var dHdr = new Border { Padding = new Thickness(16,12,16,12) };
        var dGrad = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(230,81,0),
            System.Windows.Media.Color.FromRgb(255,143,0), 0);
        dHdr.Background = dGrad;
        dHdr.Child = new TextBlock { Text = "🔒  Contraseña de Administrador",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 };
        DockPanel.SetDock(dHdr, Dock.Top); dp.Children.Add(dHdr);

        var dPie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = PBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dPieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        PasswordBox pwBox = null!;
        var btnOk = new Button { Content = "Confirmar", Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(0,0,8,0), Background = PBC("#E65100"),
            Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontWeight = FontWeights.SemiBold };
        var btnCx = new Button { Content = "Cancelar", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = PBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        dPieSp.Children.Add(btnOk); dPieSp.Children.Add(btnCx);
        dPie.Child = dPieSp;
        DockPanel.SetDock(dPie, Dock.Bottom); dp.Children.Add(dPie);

        var dBody = new StackPanel { Margin = new Thickness(20, 14, 20, 0) };
        dBody.Children.Add(new TextBlock { Text = "Ingrese la contraseña del administrador:",
            Foreground = PBC("#424242"), Margin = new Thickness(0,0,0,8), TextWrapping = TextWrapping.Wrap });
        pwBox = new PasswordBox { Padding = new Thickness(8,6,8,6),
            BorderBrush = PBC("#FFB74D"), BorderThickness = new Thickness(1) };
        dBody.Children.Add(pwBox);
        dp.Children.Add(dBody);
        dlg.Content = dp;

        btnOk.Click += async (_, _) => {
            var pw = pwBox.Password;
            try {
                using var conn = _db.Create();
                var existe = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM USUARIOS WHERE CONTRASEÑA_USUARIO=@c AND CARGO_USUARIO='ADMINISTRADOR'",
                    new { c = pw });
                if (existe > 0) { ok = true; dlg.Close(); }
                else MessageBox.Show("Contraseña incorrecta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            } catch { MessageBox.Show("Error al validar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        btnCx.Click += (_, _) => dlg.Close();
        pwBox.KeyDown += async (_, e) => { if (e.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        dlg.ShowDialog();
        return ok;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  NOTA DE CRÉDITO (Herramientas)
// ══════════════════════════════════════════════════════════════════════════════
public class NotaCreditoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    // búsqueda ventas
    private TextBox    _txtLocalFiltro = null!;   // display del local seleccionado
    private TextBox    _txtCiFiltro    = null!;
    private DataGrid   _dgVentas       = null!;
    private TextBlock  _tagVentasCnt   = null!;   // contador "N registros" sobre _dgVentas

    // cuotas de la venta seleccionada
    private DataGrid   _dgCuotas   = null!;
    private TextBlock  _lblVenta   = null!;

    // panel inferior (acción)
    private Border     _panelAccion = null!;
    private TextBox    _txtObs     = null!;
    private TextBox    _txtMonto   = null!;
    private TextBlock  _lblCuotaInfo = null!;

    // tipo de NC seleccionado (radiobuttons)
    private RadioButton _rbCuota = null!, _rbCancelTotal = null!, _rbSecuestro = null!;

    // artículos de la venta
    private List<FilaArtNC> _articulosVenta = new();

    // venta y cuota seleccionadas
    private FilaVentaNC?  _ventaSel = null;
    private FilaCuotaNC?  _cuotaSel = null;

    // locales disponibles y local filtro activo
    private List<CrediSoft.Core.Models.Local> _locales    = new();
    private CrediSoft.Core.Models.Local?      _localFiltro = null;  // null = todos

    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    // ── Paleta azul corporativa ───────────────────────────────────────────
    static readonly System.Windows.Media.SolidColorBrush
        AzulOscuro  = EBC("#0E2F44"),
        AzulBase    = EBC("#1A4F6E"),
        AzulMedio   = EBC("#154360"),
        AzulClaro   = EBC("#1F6089"),
        AzulTexto   = EBC("#D6EAF8"),
        AzulMuted   = EBC("#7FB3D3"),
        AzulHint    = EBC("#4A7FA5"),
        BlancoPuro  = System.Windows.Media.Brushes.White,
        FondoBody   = EBC("#F0F4F7"),
        FondoCard   = EBC("#FFFFFF"),
        BordeCard   = EBC("#C8DDE9"),
        // acento para tipos de NC
        AcentoCuota = EBC("#1A6FA8"),
        AcentoCanc  = EBC("#1A7A4A"),
        AcentoSeq   = EBC("#7B3FA0");

    public NotaCreditoWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = App.Services.GetRequiredService<ISessionService>();
        Title = "Nota de Crédito — ElectroMar";
        Width = 1160; Height = 720; MinWidth = 960; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = FondoBody;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        _ = InicializarAsync();
    }

    private void BuildUI()
    {
        // ── helpers locales ───────────────────────────────────────────────
        // Header de grilla: fondo gris oscuro neutro, texto blanco, sin bordes laterales agresivos
        Style HdrSt(string bgHex = "#1A4F6E") {
            var hdrBg   = EBC(bgHex);
            var hdrLine = EBC("#155980");
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty,    hdrBg));
            s.Setters.Add(new Setter(Control.ForegroundProperty,    BlancoPuro));
            s.Setters.Add(new Setter(Control.FontWeightProperty,    FontWeights.SemiBold));
            s.Setters.Add(new Setter(Control.FontSizeProperty,      11.5));
            s.Setters.Add(new Setter(Control.PaddingProperty,       new Thickness(12, 9, 12, 9)));
            s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
            s.Setters.Add(new Setter(Control.BorderBrushProperty,   hdrLine));
            return s;
        }
        DataGridTextColumn DC(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn {
                Header  = h,
                Binding = new System.Windows.Data.Binding(b),
                Width   = new DataGridLength(w, DataGridLengthUnitType.Star) };
            var es = new Style(typeof(TextBlock));
            es.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, a));
            es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10, 0, 10, 0)));
            es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            c.ElementStyle = es;
            return c;
        }
        // columna ancho fijo en píxeles
        DataGridTextColumn DCpx(string h, string b, double px, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn {
                Header  = h,
                Binding = new System.Windows.Data.Binding(b),
                Width   = new DataGridLength(px, DataGridLengthUnitType.Pixel) };
            var es = new Style(typeof(TextBlock));
            es.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, a));
            es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
            es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            c.ElementStyle = es;
            return c;
        }
        var root = new DockPanel { Background = EBC("#EEF2F5") };

        // ══════════════════════════════════════════════════════════════════
        // HEADER
        // ══════════════════════════════════════════════════════════════════
        var hdr = new Border {
            Background = AzulOscuro,
            Padding    = new Thickness(24, 15, 24, 15),
            Effect     = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 2, BlurRadius = 8, Opacity = 0.35,
                Color = System.Windows.Media.Colors.Black } };
        var hGrid = new Grid();
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hLeft.Children.Add(new TextBlock {
            Text = "NOTA DE CRÉDITO",
            Foreground = BlancoPuro, FontSize = 18, FontWeight = FontWeights.Bold });
        hLeft.Children.Add(new TextBlock {
            Text = "Cuota específica  ·  Cancelación total  ·  Cancelación por secuestro",
            Foreground = AzulMuted, FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(hLeft, 0); hGrid.Children.Add(hLeft);

        var hBadge = new Border {
            Background    = AzulMedio, CornerRadius = new CornerRadius(4),
            Padding       = new Thickness(14, 7, 14, 7),
            VerticalAlignment = VerticalAlignment.Center };
        hBadge.Child = new TextBlock {
            Text       = $"Local: {_session.LocalActual?.NombreLocal ?? "—"}",
            Foreground = AzulTexto, FontSize = 11, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(hBadge, 1); hGrid.Children.Add(hBadge);

        hdr.Child = hGrid;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ══════════════════════════════════════════════════════════════════
        // FOOTER
        // ══════════════════════════════════════════════════════════════════
        var pie = new Border { Background = AzulOscuro, Padding = new Thickness(18, 11, 18, 11) };
        var pieDp = new DockPanel { LastChildFill = false };

        var hintTxt = new TextBlock {
            Text = "Esc = Cerrar  ·  F5 = Buscar  ·  Enter = Guardar",
            Foreground = AzulHint, FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(hintTxt, Dock.Left); pieDp.Children.Add(hintTxt);

        Button FootBtn(string icon, string label,
                       System.Windows.Media.SolidColorBrush bg,
                       System.Windows.Media.SolidColorBrush hover) {
            var btn = new Button {
                Height          = 38, Padding = new Thickness(20, 0, 20, 0),
                Margin          = new Thickness(8, 0, 0, 0),
                Background      = bg, Foreground = BlancoPuro,
                FontWeight      = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor          = System.Windows.Input.Cursors.Hand, FontSize = 12 };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock {
                Text = icon, FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            btn.Content   = sp;
            btn.MouseEnter += (_, _) => btn.Background = hover;
            btn.MouseLeave += (_, _) => btn.Background = bg;
            return btn;
        }

        var btnGuardar   = FootBtn("💾", "Guardar Nota de Crédito", EBC("#1565C0"), EBC("#1976D2"));
        var btnArts      = FootBtn("📦", "Ver Artículos",            AzulMedio,     AzulClaro);
        var btnHistorial = FootBtn("📋", "Historial",                EBC("#0E6655"), EBC("#117A65"));
        var btnCerrar    = FootBtn("✖",  "Cerrar",                   EBC("#37474F"), EBC("#546E7A"));
        btnGuardar.Click   += async (_, _) => await GuardarNC();
        btnArts.Click      += (_, _) => MostrarArticulos();
        btnHistorial.Click += (_, _) => MostrarHistorial();
        btnCerrar.Click    += (_, _) => Close();

        var pieBtns = new StackPanel { Orientation = Orientation.Horizontal };
        pieBtns.Children.Add(btnGuardar);
        pieBtns.Children.Add(btnArts);
        pieBtns.Children.Add(btnHistorial);
        pieBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(pieBtns, Dock.Right); pieDp.Children.Add(pieBtns);
        pie.Child = pieDp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ══════════════════════════════════════════════════════════════════
        // BARRA DE BÚSQUEDA
        // ══════════════════════════════════════════════════════════════════
        var searchBar = new Border {
            Background      = AzulOscuro,
            Padding         = new Thickness(20, 12, 20, 12),
            BorderBrush     = AzulMedio,
            BorderThickness = new Thickness(0, 0, 0, 1) };

        var searchRow = new StackPanel {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };

        // label blanco + control — helper local
        void Campo(string label, System.Windows.UIElement ctrl, double marginRight = 20) {
            searchRow.Children.Add(new TextBlock {
                Text              = label,
                Foreground        = BlancoPuro,
                FontSize          = 11.5,
                FontWeight        = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 8, 0) });
            if (ctrl is FrameworkElement fe) fe.Margin = new Thickness(0, 0, marginRight, 0);
            searchRow.Children.Add(ctrl);
        }

        // input readonly que muestra el local seleccionado
        _txtLocalFiltro = new TextBox {
            Width                    = 190, Height = 34,
            Padding                  = new Thickness(10, 6, 10, 6),
            Background               = EBC("#D6E4EE"),
            Foreground               = AzulOscuro,
            BorderBrush              = AzulHint,
            BorderThickness          = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize                 = 12,
            IsReadOnly               = true,
            Text                     = "Todos los locales" };

        var btnSelLocal = new Button {
            Height          = 34, Padding = new Thickness(12, 0, 12, 0),
            Background      = AzulMedio,
            Foreground      = BlancoPuro,
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            FontSize        = 11, FontWeight = FontWeights.SemiBold,
            Margin          = new Thickness(0, 0, 24, 0),
            Content         = "📍 Seleccionar" };
        btnSelLocal.MouseEnter += (_, _) => btnSelLocal.Background = AzulClaro;
        btnSelLocal.MouseLeave += (_, _) => btnSelLocal.Background = AzulMedio;
        btnSelLocal.Click += (_, _) => {
            var modal = new CrediSoft.UI.Views.Shared.SeleccionarLocalModal(_locales, _localFiltro) { Owner = this };
            if (modal.ShowDialog() != true) return;
            _localFiltro = modal.LocalSeleccionado;
            _txtLocalFiltro.Text = _localFiltro == null
                ? "Todos los locales"
                : $"{_localFiltro.IdLocal} — {_localFiltro.NombreLocal}";
            _ = BuscarVentas();
        };

        Campo("Local:", _txtLocalFiltro, 4);
        searchRow.Children.Add(btnSelLocal);

        _txtCiFiltro = new TextBox {
            Width                    = 210, Height = 34,
            Padding                  = new Thickness(10, 6, 10, 6),
            Background               = BlancoPuro,
            BorderBrush              = AzulHint,
            BorderThickness          = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize                 = 12 };
        _txtCiFiltro.KeyDown    += async (_, e) => { if (e.Key == Key.Enter) await BuscarVentas(); };
        _txtCiFiltro.TextChanged += async (_, _) => await BuscarVentas();
        Campo("CI / Nombre:", _txtCiFiltro, 16);

        var btnBuscar = new Button {
            Height          = 34, Padding = new Thickness(20, 0, 20, 0),
            Background      = AzulBase,
            Foreground      = BlancoPuro,
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            FontWeight      = FontWeights.SemiBold, FontSize = 12 };
        var bSp = new StackPanel { Orientation = Orientation.Horizontal };
        bSp.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0) });
        bSp.Children.Add(new TextBlock {
            Text = "Buscar", VerticalAlignment = VerticalAlignment.Center });
        btnBuscar.Content    = bSp;
        btnBuscar.MouseEnter += (_, _) => btnBuscar.Background = AzulClaro;
        btnBuscar.MouseLeave += (_, _) => btnBuscar.Background = AzulBase;
        btnBuscar.Click      += async (_, _) => await BuscarVentas();
        searchRow.Children.Add(btnBuscar);

        searchBar.Child = searchRow;
        DockPanel.SetDock(searchBar, Dock.Top); root.Children.Add(searchBar);

        // ══════════════════════════════════════════════════════════════════
        // BODY: columna izquierda (grillas) + columna derecha (panel acción)
        // ══════════════════════════════════════════════════════════════════
        var body = new Grid { Margin = new Thickness(16, 14, 16, 14) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        root.Children.Add(body);

        // ── COLUMNA IZQUIERDA ─────────────────────────────────────────────
        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // tag créditos
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.15, GridUnitType.Star) }); // dg ventas
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // gap
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // tag cuotas
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });    // dg cuotas
        Grid.SetColumn(leftGrid, 0); body.Children.Add(leftGrid);

        // ─ Tarjeta Créditos Activos ─
        var cardVentas = new Border {
            Background      = FondoCard,
            BorderBrush     = EBC("#CBD8E1"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(6),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 1, BlurRadius = 6, Opacity = 0.10,
                Color = System.Windows.Media.Colors.Black } };
        var cardVentasDp = new DockPanel();

        // cabecera de tarjeta ventas
        var tagVentas = new Border {
            Background   = AzulOscuro,
            Padding      = new Thickness(14, 9, 14, 9),
            CornerRadius = new CornerRadius(5, 5, 0, 0) };
        var tagVentasRow = new Grid();
        tagVentasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tagVentasRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tagVentasRow.Children.Add(new TextBlock {
            Text = "CRÉDITOS ACTIVOS", Foreground = BlancoPuro,
            FontSize = 10.5, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center });
        var tagVentasCnt = new TextBlock {
            Foreground = AzulMuted, FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tagVentasCnt, 1); tagVentasRow.Children.Add(tagVentasCnt);
        tagVentas.Child = tagVentasRow;
        DockPanel.SetDock(tagVentas, Dock.Top); cardVentasDp.Children.Add(tagVentas);

        _dgVentas = new DataGrid {
            AutoGenerateColumns      = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode            = DataGridSelectionMode.Single,
            GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#E8EEF3"),
            Background               = FondoCard,
            AlternatingRowBackground = EBC("#F4F8FA"),
            BorderThickness          = new Thickness(0),
            ColumnHeaderStyle        = HdrSt("#1A4F6E"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanUserResizeColumns     = true,
            FontSize = 12.5, RowHeight = 36,
            SelectionUnit = DataGridSelectionUnit.FullRow };

        var rsVentas = new Style(typeof(DataGridRow));
        rsVentas.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        _dgVentas.RowStyle = rsVentas;

        // N° Sol.
        _dgVentas.Columns.Add(DCpx("N° Sol.", "NSolCorta", 70, TextAlignment.Center));

        // CI
        _dgVentas.Columns.Add(DCpx("CI", "Ci", 78, TextAlignment.Center));

        // Cliente — Star: usa todo el espacio sobrante, texto truncado + tooltip
        var colCliente = new DataGridTemplateColumn {
            Header          = "Cliente",
            Width           = new DataGridLength(1, DataGridLengthUnitType.Star),
            CanUserResize   = true,
            SortMemberPath  = "Cliente" };
        var cellTpl = new FrameworkElementFactory(typeof(TextBlock));
        cellTpl.SetBinding(TextBlock.TextProperty,            new System.Windows.Data.Binding("Cliente"));
        cellTpl.SetBinding(ToolTipService.ToolTipProperty,   new System.Windows.Data.Binding("Cliente"));
        cellTpl.SetValue(TextBlock.TextTrimmingProperty,      TextTrimming.CharacterEllipsis);
        cellTpl.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        cellTpl.SetValue(TextBlock.PaddingProperty,           new Thickness(10, 0, 10, 0));
        cellTpl.SetValue(TextBlock.FontSizeProperty,          12.5);
        colCliente.CellTemplate = new DataTemplate { VisualTree = cellTpl };
        _dgVentas.Columns.Add(colCliente);

        // Total Gs.
        _dgVentas.Columns.Add(DCpx("Total Gs.", "TotalFmt", 92, TextAlignment.Right));

        // Fecha — dd/MM/yy
        _dgVentas.Columns.Add(DCpx("Fecha", "FechaStr", 90, TextAlignment.Center));

        // L (local)
        _dgVentas.Columns.Add(DCpx("L", "IdLocal", 46, TextAlignment.Center));

        _dgVentas.SelectionChanged += async (_, _) => await OnVentaSeleccionada();

        // Contador actualizado directamente en BuscarVentas() (ver más abajo) — antes se
        // actualizaba acá en LoadingRow, pero WPF NO dispara ese evento cuando la grilla queda
        // con 0 filas (ej. una búsqueda sin resultados): el contador se quedaba mostrando el
        // valor de la búsqueda ANTERIOR, dando la falsa impresión de que sí había resultados
        // aunque la tabla estuviera vacía (caso real: buscar un CI sin créditos activos seguía
        // mostrando "67 registros" de la búsqueda previa).
        _tagVentasCnt = tagVentasCnt;

        cardVentasDp.Children.Add(_dgVentas);
        cardVentas.Child = cardVentasDp;
        Grid.SetRow(cardVentas, 0); Grid.SetRowSpan(cardVentas, 2);
        leftGrid.Children.Add(cardVentas);

        // ─ Tarjeta Cuotas ─
        var cardCuotas = new Border {
            Background      = FondoCard,
            BorderBrush     = EBC("#CBD8E1"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(6),
            Margin          = new Thickness(0, 12, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 1, BlurRadius = 6, Opacity = 0.10,
                Color = System.Windows.Media.Colors.Black } };
        var cardCuotasDp = new DockPanel();

        var tagCuotas = new Border {
            Background   = EBC("#154360"),
            Padding      = new Thickness(14, 9, 14, 9),
            CornerRadius = new CornerRadius(5, 5, 0, 0) };
        _lblVenta = new TextBlock {
            Foreground = AzulTexto, FontSize = 10.5, FontWeight = FontWeights.Bold,
            Text = "CUOTAS DEL CRÉDITO" };
        tagCuotas.Child = _lblVenta;
        DockPanel.SetDock(tagCuotas, Dock.Top); cardCuotasDp.Children.Add(tagCuotas);

        _dgCuotas = new DataGrid {
            AutoGenerateColumns      = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode            = DataGridSelectionMode.Single,
            GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#ECEFF1"),
            Background               = FondoCard,
            AlternatingRowBackground = EBC("#F7FAFB"),
            BorderThickness          = new Thickness(0),
            ColumnHeaderStyle        = HdrSt("#154360"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanUserResizeColumns     = true,
            FontSize = 12, RowHeight = 34,
            SelectionUnit = DataGridSelectionUnit.FullRow };

        // fila cancelada → verde muy suave
        var rsCuotas = new Style(typeof(DataGridRow));
        rsCuotas.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        var dtCob = new DataTrigger {
            Binding = new System.Windows.Data.Binding("EstadoStr"), Value = "Cancelado" };
        dtCob.Setters.Add(new Setter(DataGridRow.BackgroundProperty, EBC("#E8F5E9")));
        dtCob.Setters.Add(new Setter(DataGridRow.ForegroundProperty, EBC("#2E7D32")));
        rsCuotas.Triggers.Add(dtCob);
        _dgCuotas.RowStyle = rsCuotas;

        _dgCuotas.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _dgCuotas.Columns.Add(DC   ("Comprobante", "Comprobante", 1.0));
        _dgCuotas.Columns.Add(DCpx ("N°",          "NCuota",      50,  TextAlignment.Center));
        _dgCuotas.Columns.Add(DCpx ("Monto Gs.",   "MontoFmt",    88,  TextAlignment.Right));
        _dgCuotas.Columns.Add(DCpx ("Entrega Gs.", "EntregaFmt",  88,  TextAlignment.Right));
        _dgCuotas.Columns.Add(DCpx ("Vencimiento", "VtoStr",      96,  TextAlignment.Center));
        _dgCuotas.Columns.Add(DCpx ("Estado",      "EstadoStr",   88,  TextAlignment.Center));
        _dgCuotas.Columns.Add(DCpx ("CC",          "Cpha",        40,  TextAlignment.Center));
        _dgCuotas.SelectionChanged += OnCuotaSeleccionada;

        cardCuotasDp.Children.Add(_dgCuotas);
        cardCuotas.Child = cardCuotasDp;
        Grid.SetRow(cardCuotas, 2); Grid.SetRowSpan(cardCuotas, 3);
        leftGrid.Children.Add(cardCuotas);

        // ── COLUMNA DERECHA: panel de acción ─────────────────────────────
        _panelAccion = new Border {
            Background = FondoCard,
            BorderBrush = BordeCard, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                ShadowDepth = 1, BlurRadius = 10, Opacity = 0.13,
                Color = System.Windows.Media.Colors.Black } };
        Grid.SetColumn(_panelAccion, 2); body.Children.Add(_panelAccion);

        var acRoot = new DockPanel();

        // header del panel de acción
        var acHdr = new Border {
            Background = EBC("#1565C0"),
            Padding    = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(6, 6, 0, 0) };
        var acHSp = new StackPanel();
        acHSp.Children.Add(new TextBlock {
            Text = "APLICAR NOTA DE CRÉDITO",
            Foreground = BlancoPuro, FontWeight = FontWeights.Bold, FontSize = 12.5 });
        _lblCuotaInfo = new TextBlock {
            Foreground = EBC("#90CAF9"), FontSize = 10.5,
            Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
        acHSp.Children.Add(_lblCuotaInfo);
        acHdr.Child = acHSp;
        DockPanel.SetDock(acHdr, Dock.Top); acRoot.Children.Add(acHdr);

        var acScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var acBody = new StackPanel { Margin = new Thickness(16, 16, 16, 16) };

        // ─ sección tipo NC ─
        acBody.Children.Add(new TextBlock {
            Text = "TIPO DE OPERACIÓN",
            Foreground = EBC("#546E7A"), FontSize = 9.5, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10) });

        Border RbCard(string titulo, string desc, string icono,
                      System.Windows.Media.SolidColorBrush acento,
                      System.Windows.Media.SolidColorBrush fondo,
                      out RadioButton rb) {
            var card = new Border {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1.5), BorderBrush = EBC("#C8D6E0"),
                Background = fondo, Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rb = new RadioButton { GroupName = "TipoNC", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rb, 0); row.Children.Add(rb);
            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock {
                Text = $"{icono}  {titulo}", FontWeight = FontWeights.SemiBold,
                Foreground = acento, FontSize = 12.5 });
            txt.Children.Add(new TextBlock {
                Text = desc, FontSize = 10.5, Foreground = EBC("#607D8B"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(txt, 2); row.Children.Add(txt);
            card.Child = row;
            var cap = rb;
            card.MouseLeftButtonUp += (_, _) => cap.IsChecked = true;
            // highlight on checked
            rb.Checked   += (_, _) => { card.BorderBrush = acento; card.Background = fondo; };
            rb.Unchecked += (_, _) => { card.BorderBrush = EBC("#C8D6E0"); };
            return card;
        }

        acBody.Children.Add(RbCard("Cuota específica",
            "Ajusta la cuota seleccionada en la grilla",
            "📌", AcentoCuota, EBC("#F0F8FF"), out _rbCuota));
        acBody.Children.Add(RbCard("Cancelación total",
            "Cancela todas las cuotas pendientes",
            "✔", AcentoCanc, EBC("#F1FFF4"), out _rbCancelTotal));
        acBody.Children.Add(RbCard("Cancelación por secuestro",
            "Secuestro de mercadería — reactiva al cliente",
            "⚠", AcentoSeq, EBC("#FAF5FF"), out _rbSecuestro));
        _rbCuota.IsChecked = true;

        // ─ divisor ─
        acBody.Children.Add(new Border {
            Height = 1, Background = EBC("#E0EAF0"),
            Margin = new Thickness(0, 4, 0, 14) });

        // ─ monto ─
        acBody.Children.Add(new TextBlock {
            Text = "MONTO A COBRAR POR NC",
            Foreground = EBC("#546E7A"), FontSize = 9.5, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8) });

        var montoBorder = new Border {
            BorderBrush = BordeCard, BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(5), Background = EBC("#F8FBFD") };
        var montoRow = new Grid();
        montoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        montoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _txtMonto = new TextBox {
            Padding = new Thickness(12, 10, 8, 10),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            FontSize = 20, FontWeight = FontWeights.Bold, Foreground = AzulBase };
        var montoSufijo = new TextBlock {
            Text = "Gs.", Foreground = AzulMuted, FontSize = 14, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(_txtMonto,   0); montoRow.Children.Add(_txtMonto);
        Grid.SetColumn(montoSufijo, 1); montoRow.Children.Add(montoSufijo);
        montoBorder.Child = montoRow;
        acBody.Children.Add(montoBorder);

        acBody.Children.Add(new TextBlock {
            Text = "Ingresá 0 para condonación sin cobro en efectivo.",
            Foreground = EBC("#78909C"), FontSize = 10, Margin = new Thickness(0, 5, 0, 14),
            TextWrapping = TextWrapping.Wrap });

        // ─ observación ─
        acBody.Children.Add(new TextBlock {
            Text = "OBSERVACIÓN",
            Foreground = EBC("#546E7A"), FontSize = 9.5, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8) });
        _txtObs = new TextBox {
            Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = BordeCard, BorderThickness = new Thickness(1.5),
            Background = EBC("#F8FBFD"), Height = 90,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 12 };
        acBody.Children.Add(_txtObs);

        acScroll.Content = acBody;
        acRoot.Children.Add(acScroll);
        _panelAccion.Child = acRoot;

        Content = root;
        KeyDown += (_, e) => {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.F5)    _ = BuscarVentas();
        };
    }

    private async Task InicializarAsync()
    {
        try {
            using var conn = _db.Create();
            _locales = (await conn.QueryAsync<CrediSoft.Core.Models.Local>(
                "SELECT ID_LOCAL AS IdLocal, NOMBRE AS NombreLocal FROM LOCALES ORDER BY ID_LOCAL")).ToList();
            // preseleccionar local de sesión
            _localFiltro = _locales.FirstOrDefault(l => l.IdLocal == (_session.LocalActual?.IdLocal ?? 0));
            _txtLocalFiltro.Text = _localFiltro == null
                ? "Todos los locales"
                : $"{_localFiltro.IdLocal} — {_localFiltro.NombreLocal}";
            await BuscarVentas();
        } catch (Exception ex) {
            MessageBox.Show($"Error al inicializar: {ex.Message}");
        }
    }

    private async Task BuscarVentas()
    {
        try {
            var ci = _txtCiFiltro.Text.Trim();
            // Buscar por CI/Nombre no debe depender del local: el cajero muchas veces no sabe
            // en qué local está el crédito del cliente (pedido explícito) — con texto cargado
            // se ignora el filtro de Local (idLocal=0 = "todos", ya soportado por la query de
            // abajo), y solo se respeta el Local seleccionado cuando la búsqueda está vacía.
            var idLocal = string.IsNullOrEmpty(ci) ? (byte)(_localFiltro?.IdLocal ?? 0) : (byte)0;
            using var conn = _db.Create();
            // "Es crédito" NO depende de CS.FORMA_DE_VENTA — esa columna no distingue
            // contado/crédito de forma confiable (bug real detectado: ventas reales a crédito,
            // con cuotas en GENERADAS y saldo pendiente, existen con FORMA_DE_VENTA=1 Y con
            // FORMA_DE_VENTA=2 — ej. IDCAB 33167, FORMA_DE_VENTA=1, CUOTAS=3, HABER<TOTAL,
            // con cuotas reales cobrables desde Cobrar Cuota, que el filtro viejo excluía acá).
            // El criterio real es que la venta tenga filas generadas en GENERADAS (cuotas).
            var ventas = (await conn.QueryAsync<FilaVentaNC>(
                @"SELECT CS.IDCAB AS IdCab, CS.NSOLICITUD AS NSolicitud, CS.ID_LOCAL AS IdLocal,
                         CL.NOMBRE_CLIENTE AS Cliente, CL.CI_CLIENTE AS Ci, CS.ID_CLIENTE AS IdCliente,
                         CS.TOTAL, CS.FECHA, CS.NVENTACHAR AS NVentaChar
                  FROM CABECERA_SALES CS
                  INNER JOIN CLIENTES CL ON CL.ID_CLIENTE = CS.ID_CLIENTE
                  WHERE CS.ESTADO = 1
                    AND EXISTS (SELECT 1 FROM GENERADAS G WHERE G.IDCAB = CS.IDCAB)
                    AND (@l = 0 OR CS.ID_LOCAL = @l)
                    AND (@ci = '' OR CL.CI_CLIENTE LIKE '%' + @ci + '%' OR CL.NOMBRE_CLIENTE LIKE '%' + @ci + '%')
                  ORDER BY CS.FECHA DESC",
                new { l = idLocal, ci })).ToList();
            _dgVentas.ItemsSource = ventas;
            _tagVentasCnt.Text = $"{ventas.Count} registro{(ventas.Count != 1 ? "s" : "")}";
            _panelAccion.Visibility = Visibility.Collapsed;
            _dgCuotas.ItemsSource = null;
        } catch (Exception ex) {
            MessageBox.Show($"Error al buscar: {ex.Message}");
        }
    }

    private async Task OnVentaSeleccionada()
    {
        if (_dgVentas.SelectedItem is not FilaVentaNC v) return;
        _ventaSel = v;
        _cuotaSel = null;
        _panelAccion.Visibility = Visibility.Collapsed;

        // El combo "Local" arriba debe reflejar el local REAL del crédito elegido, no quedar
        // fijo en el local de la sesión — la búsqueda por CI ya ignora el local (ver
        // BuscarVentas), así que el cliente puede tener su crédito en un local distinto al que
        // el cajero tiene seleccionado. Sin esto, "Nota de Crédito" quedaba visualmente
        // desalineado del local real (columna "L" de la grilla) tras encontrar el crédito.
        var localCredito = _locales.FirstOrDefault(l => l.IdLocal == v.IdLocal);
        if (localCredito != null)
        {
            _localFiltro = localCredito;
            _txtLocalFiltro.Text = $"{localCredito.IdLocal} — {localCredito.NombreLocal}";
        }
        try {
            using var conn = _db.Create();
            // cuotas
            var p = new DynamicParameters();
            p.Add("@Idcab", v.IdCab);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var cuotas = (await conn.QueryAsync<FilaCuotaNC>(
                "BUSCAR_G_NOTA_CREDITO_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            // CPHA (CC) no lo devuelve el SP — una sola query extra ya que es el mismo para todas las cuotas
            if (cuotas.Count > 0)
            {
                var cpha = await conn.ExecuteScalarAsync<byte?>(
                    "SELECT CPHA FROM CABECERA_SALES WHERE IDCAB = @id",
                    new { id = v.IdCab });
                byte cphaVal = cpha ?? 0;
                foreach (var c in cuotas) c.Cpha = cphaVal;
            }
            _dgCuotas.ItemsSource = cuotas;
            // artículos de la venta
            var p2 = new DynamicParameters();
            p2.Add("@Idcab", v.IdCab);
            p2.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var arts = (await conn.QueryAsync<FilaArtNC>(
                "BUSCAR_ARTICULOS_NOTACREDITO_CS", p2, commandType: CommandType.StoredProcedure)).ToList();
            _articulosVenta = arts;
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar cuotas: {ex.Message}");
        }
    }

    private void OnCuotaSeleccionada(object s, SelectionChangedEventArgs e)
    {
        if (_dgCuotas.SelectedItem is not FilaCuotaNC c) return;
        _cuotaSel = c;
        _lblCuotaInfo.Text = $"Cuota #{c.NCuota}  •  Vence: {c.VtoStr}  •  Monto: {c.MontoFmt} Gs.";
        // Precargar el SALDO pendiente de la cuota (Monto - Entrega ya registrada), no el
        // monto original completo — bug real detectado: si el cliente ya había abonado un
        // adelanto parcial de esta cuota (GENERADAS.ENTREGA > 0), "Monto a Cobrar por NC"
        // seguía mostrando el monto total, ignorando lo ya entregado.
        var saldoPendiente = Math.Max(0, c.Monto - c.Entrega);
        _txtMonto.Text = saldoPendiente.ToString("N0", _py);
        _panelAccion.Visibility = Visibility.Visible;
    }

    private async void MostrarHistorial()
    {
        if (_ventaSel == null) {
            MessageBox.Show("Seleccione una venta primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try {
            using var conn = _db.Create();
            conn.Open();
            // Historial por CUOTA con fecha de pago real — misma query que
            // CuotaRepository.ObtenerHistorialAsync (ver comentario ahí sobre FECHACOBRADO).
            var cuotas = (await conn.QueryAsync<dynamic>(
                "SELECT G.NCUOTA, G.MONTO, " +
                "CONVERT(VARCHAR(10), G.VTO, 103) AS VTO, " +
                "CASE WHEN G.ESTADO = 0 THEN 'Pendiente' ELSE 'Cancelado' END AS ESTADO, " +
                // G.MORA persistida no es confiable para cuotas canceladas (ver comentario en
                // CuotaRepository.ObtenerHistorialAsync) — se recalcula desde FECHACOBRADO.
                "CASE WHEN G.ESTADO = 0 THEN DATEDIFF(day, G.VTO, GETDATE()) " +
                "ELSE (CASE WHEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) > 0 THEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) ELSE 0 END) END AS MORA, " +
                "ISNULL(G.OBS, '') AS OBS, " +
                "CASE WHEN G.ESTADO = 1 THEN CONVERT(VARCHAR(10), G.FECHACOBRADO, 103) ELSE NULL END AS FECHAPAGO, " +
                "CASE WHEN G.ESTADO = 1 THEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) ELSE NULL END AS DIASVTOAPAGO " +
                "FROM GENERADAS G WHERE G.IDCAB = @IdCab ORDER BY G.NCUOTA",
                new { IdCab = _ventaSel.IdCab })).ToList();

            var cuotasHist = cuotas.Select(c => new CrediSoft.UI.Views.Cobros.CuotaHistorialDetallada(
                NCuota       : (byte)c.NCUOTA,
                Monto        : (decimal)c.MONTO,
                Vto          : (string)c.VTO,
                Estado       : (string)c.ESTADO,
                Mora         : (int)c.MORA,
                Obs          : (string)c.OBS,
                FechaPago    : (string?)c.FECHAPAGO,
                DiasVtoAPago : (int?)c.DIASVTOAPAGO));

            new CrediSoft.UI.Views.Cobros.HistorialCobrosModal(
                _ventaSel.Cliente, _ventaSel.IdCab, cuotasHist) { Owner = this }.ShowDialog();
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar historial: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MostrarArticulos()
    {
        if (_articulosVenta.Count == 0) {
            MessageBox.Show("Seleccione una venta primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new Window {
            Title = "Artículos de la venta", Width = 560, Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        var dp = new DockPanel();
        var dhdr = new Border { Padding = new Thickness(14,10,14,10) };
        var dg = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(21,101,192),
            System.Windows.Media.Color.FromRgb(30,136,229), 0);
        dhdr.Background = dg;
        dhdr.Child = new TextBlock { Text = $"📦  Artículos — {_ventaSel?.Cliente}",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var hdrS = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrS.Setters.Add(new Setter(Control.BackgroundProperty, EBC("#1565C0")));
        hdrS.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        hdrS.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrS.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
        var dg2 = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), RowHeight = 30,
            ColumnHeaderStyle = hdrS, FontSize = 12, Margin = new Thickness(8) };
        dg2.Columns.Add(new DataGridTextColumn { Header = "Descripción", Width = new DataGridLength(3, DataGridLengthUnitType.Star),
            Binding = new System.Windows.Data.Binding("Descripcion") });
        dg2.Columns.Add(new DataGridTextColumn { Header = "Cantidad", Width = new DataGridLength(0.6, DataGridLengthUnitType.Star),
            Binding = new System.Windows.Data.Binding("CantFmt") });
        dg2.Columns.Add(new DataGridTextColumn { Header = "P. Venta", Width = new DataGridLength(0.9, DataGridLengthUnitType.Star),
            Binding = new System.Windows.Data.Binding("PvFmt") });
        dg2.ItemsSource = _articulosVenta;
        dp.Children.Add(dg2); dlg.Content = dp;
        dlg.ShowDialog();
    }

    private async Task GuardarNC()
    {
        if (_ventaSel == null) {
            MessageBox.Show("Seleccione una venta primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        bool esCuotaEsp = _rbCuota.IsChecked == true;
        bool esCancelTotal = _rbCancelTotal.IsChecked == true;
        bool esSecuestro   = _rbSecuestro.IsChecked == true;

        if (esCuotaEsp && _cuotaSel == null) {
            MessageBox.Show("Seleccione una cuota de la grilla para 'Cobrar por cuota específica'.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (!decimal.TryParse(_txtMonto.Text.Replace(".", "").Replace(",",""), out var montoCobrado) || montoCobrado < 0) {
            MessageBox.Show("Ingrese un monto válido (puede ser 0 para condonación total).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var v = _ventaSel;
        // Valores de TIPO deben caber en VARCHAR(20) — verificado contra HISTORIAL_NOTA_CREDITO en producción
        string tipoTxt  = esCuotaEsp ? "Cuota específica" :
                          esCancelTotal ? "Cancelación" : "Secuestro";
        // Texto largo solo para mostrar en UI/confirmación
        string tipoDesc = esCuotaEsp ? "Cobrar por cuota específica" :
                          esCancelTotal ? "Cancelación total" : "Cancelación por secuestro";
        string spNombre = esCuotaEsp ? "ACTUALIZAR_NOTA_CREDITO_NEW_CS_2026" :
                          esCancelTotal ? "ACTUALIZAR_NOTA_CREDITO_CANCELACION_CS_2026" :
                          "ACTUALIZAR_NOTA_CREDITO_SECUESTRO_CS_2026";

        if (!MostrarConfirmacionActualizacion(v, tipoDesc)) return;

        // Autorización de administrador — igual que el sistema anterior (VB6)
        if (!await ValidarAdminAsync()) return;

        try {
            using var conn = (Microsoft.Data.SqlClient.SqlConnection)_db.Create();
            await conn.OpenAsync();
            // Transacción explícita — los SPs no tienen COMMIT/ROLLBACK interno;
            // el VB6 original manejaba la transacción en el cliente, nosotros hacemos lo mismo.
            using var tx = (System.Data.IDbTransaction)await conn.BeginTransactionAsync();
            int idUsuario = _session.UsuarioActual?.IdUsuario ?? 1;
            byte idLocal  = (byte)(v.IdLocal);
            string nomMaq = System.Net.Dns.GetHostName();

            // AGENTE=1 en el primer artículo (crea historial + modifica cabecera/generadas),
            // AGENTE=2+ solo inserta fila en DETALLES_NOTA_CREDITO.
            // Sin artículos: una llamada con IDART=0 CANTIDAD=0 (mismo comportamiento que VB6).
            var arts = _articulosVenta.Count > 0 ? _articulosVenta
                : new List<FilaArtNC> { new FilaArtNC { IdArt = 0, Cantidad = 0 } };

            bool primero = true;
            foreach (var art in arts) {
                var p = new DynamicParameters();
                p.Add("@AGENTE", primero ? 1 : 2);

                if (esCuotaEsp) {
                    p.Add("@idcab",        v.IdCab);
                    p.Add("@idlocal",      idLocal);
                    p.Add("@estado",       (byte)1);       // mantiene venta activa (solo ajusta cuota)
                    p.Add("@estadogen",    (byte)1);
                    p.Add("@montoactual",  _cuotaSel?.Monto ?? 0m);
                    p.Add("@ncuota",       _cuotaSel?.NCuota ?? (byte)1);
                    p.Add("@id_usuario",   idUsuario);
                    p.Add("@comprobante",  _cuotaSel?.Comprobante ?? "");
                    p.Add("@NSOLICITUD",   v.NSolicitud ?? "");
                    p.Add("@NVENTACHAR",   v.NVentaChar ?? "");
                    p.Add("@TIPO",         tipoTxt);
                    p.Add("@IDCLIENTENC",  v.IdCliente);
                    p.Add("@MONTOCOBRADO", montoCobrado);
                    p.Add("@OBSNOTA",      _txtObs.Text.Trim());
                    p.Add("@IDART",        art.IdArt);
                    p.Add("@CANTIDAD",     art.Cantidad);
                    p.Add("@NOM_MAQUINA",  nomMaq);
                    p.Add("@IP_MAQUINA",   "127.0.0.1");
                    p.Add("@msg",          dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
                } else {
                    // Cancelación total y Secuestro — ambos SPs ponen CABECERA_SALES.ESTADO=0
                    // y actualizan TODAS las cuotas pendientes (ESTADO=0→1) del crédito.
                    // Secuestro además hace UPDATE CLIENTES SET CONDICION=1.
                    p.Add("@IDCAB",        v.IdCab);
                    p.Add("@ID_LOCAL",     idLocal);       // Dapper es case-insensitive; tanto @ID_LOCAL como @idlocal se bindean igual
                    p.Add("@estadogen",    (byte)1);
                    p.Add("@montoactual",  montoCobrado);  // monto ingresado por el usuario
                    p.Add("@id_usuario",   idUsuario);
                    p.Add("@comprobante",  "");
                    p.Add("@NSOLICITUD",   v.NSolicitud ?? "");
                    p.Add("@NVENTACHAR",   v.NVentaChar ?? "");
                    p.Add("@TIPO",         tipoTxt);
                    p.Add("@NCUOTA",       (byte)1);
                    p.Add("@IDCLIENTENC",  v.IdCliente);
                    p.Add("@MONTOCOBRADO", montoCobrado);
                    p.Add("@OBSNOTA",      _txtObs.Text.Trim());
                    p.Add("@IDART",        art.IdArt);
                    p.Add("@CANTIDAD",     art.Cantidad);
                    p.Add("@NOM_MAQUINA",  nomMaq);
                    p.Add("@IP_MAQUINA",   "127.0.0.1");
                    p.Add("@msg",          dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
                }

                await conn.ExecuteAsync(spNombre, p, commandType: CommandType.StoredProcedure, transaction: tx);
                var spMsg = p.Get<string>("@msg") ?? "";
                if (!spMsg.StartsWith("GUARDADO", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"El procedimiento {spNombre} rechazó la operación:\n\"{spMsg}\"");
                primero = false;
            }

            tx.Commit();

            MessageBox.Show("Nota de Crédito aplicada correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _panelAccion.Visibility = Visibility.Collapsed;
            _cuotaSel = null; _ventaSel = null;
            await BuscarVentas();
        } catch (Exception ex) {
            MessageBox.Show($"Error al guardar NC:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool MostrarConfirmacionActualizacion(FilaVentaNC v, string tipoTxt)
    {
        var dlg = new Window {
            Title  = "Confirmación de actualización",
            Width  = 460, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#EEF2F5"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        bool confirmo = false;
        var dp = new DockPanel();

        // ── Header ──
        var hdr = new Border { Background = AzulOscuro, Padding = new Thickness(18, 14, 18, 14) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock {
            Text = "CONFIRMACIÓN DE ACTUALIZACIÓN",
            Foreground = BlancoPuro, FontWeight = FontWeights.Bold, FontSize = 14 });
        hSp.Children.Add(new TextBlock {
            Text = $"Tipo: {tipoTxt}",
            Foreground = AzulMuted, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        // ── Pie ──
        var pie = new Border {
            Background = AzulOscuro,
            BorderBrush = AzulMedio, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 12, 16, 12) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        Button MkBtn(string txt, string bg, string hover) {
            var b = new Button {
                Content = txt, Height = 36, Padding = new Thickness(24, 0, 24, 0),
                Margin = new Thickness(8, 0, 0, 0),
                Background = EBC(bg), Foreground = BlancoPuro,
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand };
            b.MouseEnter += (_, _) => b.Background = EBC(hover);
            b.MouseLeave += (_, _) => b.Background = EBC(bg);
            return b;
        }
        var btnSi = MkBtn("✔  Sí, proceder", "#1565C0", "#1976D2");
        var btnNo = MkBtn("✖  Cancelar",      "#37474F", "#546E7A");
        btnSi.Click += (_, _) => { confirmo = true; dlg.Close(); };
        btnNo.Click += (_, _) => dlg.Close();
        pieSp.Children.Add(btnSi); pieSp.Children.Add(btnNo);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); dp.Children.Add(pie);

        // ── Cuerpo con scroll ──
        var scroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16, 14, 16, 14) };
        var body = new StackPanel();

        // Card datos de la venta
        var card = new Border {
            Background = FondoCard, BorderBrush = BordeCard, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 12) };
        var cardSp = new StackPanel();

        // tag dentro de la card
        cardSp.Children.Add(new TextBlock {
            Text = "DATOS DE LA OPERACIÓN",
            Foreground = AzulHint, FontSize = 9.5, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8) });

        void Fila(string lbl, string val) {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lblTb = new TextBlock {
                Text = lbl, Foreground = EBC("#607D8B"),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var valTb = new TextBlock {
                Text = val, Foreground = AzulBase,
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(lblTb, 0); g.Children.Add(lblTb);
            Grid.SetColumn(valTb, 1); g.Children.Add(valTb);
            cardSp.Children.Add(g);
        }
        Fila("Nº Solicitud:",  v.NSolicitud?.Trim() ?? "—");
        Fila("Nº de Venta:",   v.NVentaChar?.Trim()  ?? "—");
        if (_cuotaSel != null)
            Fila("Cuota Nº:", _cuotaSel.NCuota.ToString());
        Fila("Cliente:", v.Cliente);
        card.Child = cardSp; body.Children.Add(card);

        // Advertencia caja chica
        var warn = new Border {
            Background = EBC("#FFF8E1"), BorderBrush = EBC("#FFD54F"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 12) };
        var wGrid = new Grid();
        wGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        wGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        wGrid.Children.Add(new TextBlock {
            Text = "⚠", FontSize = 22, Foreground = EBC("#F57F17"),
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 10, 0) });
        var wTxt = new StackPanel();
        wTxt.Children.Add(new TextBlock {
            Text = "¡ATENCIÓN CON LA CAJA CHICA!",
            FontWeight = FontWeights.Bold, Foreground = EBC("#E65100"),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
        wTxt.Children.Add(new TextBlock {
            Text = "Esta operación afecta los montos de la venta. Deberá ajustar " +
                   "manualmente el efectivo en caja (retirar o ingresar billetes) según corresponda.",
            TextWrapping = TextWrapping.Wrap, Foreground = EBC("#4E342E"), FontSize = 11.5, LineHeight = 18 });
        wTxt.Children.Add(new TextBlock {
            Text = "Si ya realizó el arqueo de caja o el movimiento físico de este " +
                   "comprobante, NO mueva el efectivo de la caja.",
            TextWrapping = TextWrapping.Wrap, Foreground = EBC("#4E342E"),
            FontSize = 11.5, LineHeight = 18, Margin = new Thickness(0, 6, 0, 0) });
        Grid.SetColumn(wTxt, 1); wGrid.Children.Add(wTxt);
        warn.Child = wGrid; body.Children.Add(warn);

        // Pregunta final
        body.Children.Add(new TextBlock {
            Text = "¿Está seguro de proceder con la actualización?",
            FontWeight = FontWeights.SemiBold, Foreground = AzulBase,
            FontSize = 12.5, TextWrapping = TextWrapping.Wrap });

        scroll.Content = body;
        dp.Children.Add(scroll);
        dlg.Content = dp;
        dlg.PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Enter)  { confirmo = true; dlg.Close(); }
            if (e.Key == Key.Escape) dlg.Close();
        };
        dlg.ShowDialog();
        return confirmo;
    }

    private async Task<bool> ValidarAdminAsync()
    {
        var dlg = new Window {
            Title  = "Autorización requerida",
            Width  = 380, Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#EEF2F5"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        bool ok = false;
        var dp = new DockPanel();

        // ── Header azul corporativo ──
        var dHdr = new Border { Background = AzulOscuro, Padding = new Thickness(18, 14, 18, 14) };
        var dHSp = new StackPanel();
        dHSp.Children.Add(new TextBlock {
            Text = "🔒  AUTORIZACIÓN DE ADMINISTRADOR",
            Foreground = BlancoPuro, FontWeight = FontWeights.Bold, FontSize = 13 });
        dHSp.Children.Add(new TextBlock {
            Text = "Ingrese la contraseña para continuar",
            Foreground = AzulMuted, FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0) });
        dHdr.Child = dHSp;
        DockPanel.SetDock(dHdr, Dock.Top); dp.Children.Add(dHdr);

        // ── Pie ──
        var dPie = new Border {
            Background = AzulOscuro,
            BorderBrush = AzulMedio, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10, 16, 10) };
        var dPieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        PasswordBox pwBox = null!;
        var btnOk = new Button {
            Content = "✔  Confirmar", Height = 34, Padding = new Thickness(20, 0, 20, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = AzulBase, Foreground = BlancoPuro,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontWeight = FontWeights.SemiBold };
        var btnCx = new Button {
            Content = "✖  Cancelar", Height = 34, Padding = new Thickness(20, 0, 20, 0),
            Background = EBC("#37474F"), Foreground = BlancoPuro,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontWeight = FontWeights.SemiBold };
        btnOk.MouseEnter += (_, _) => btnOk.Background = AzulClaro;
        btnOk.MouseLeave += (_, _) => btnOk.Background = AzulBase;
        btnCx.MouseEnter += (_, _) => btnCx.Background = EBC("#546E7A");
        btnCx.MouseLeave += (_, _) => btnCx.Background = EBC("#37474F");
        dPieSp.Children.Add(btnOk); dPieSp.Children.Add(btnCx);
        dPie.Child = dPieSp;
        DockPanel.SetDock(dPie, Dock.Bottom); dp.Children.Add(dPie);

        // ── Cuerpo ──
        var dBody = new StackPanel { Margin = new Thickness(20, 16, 20, 0) };
        dBody.Children.Add(new TextBlock {
            Text = "Esta operación requiere la contraseña de un administrador:",
            Foreground = EBC("#37474F"), FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
        pwBox = new PasswordBox {
            Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = BordeCard, BorderThickness = new Thickness(1),
            Background = FondoCard, FontSize = 14 };
        dBody.Children.Add(pwBox);
        dp.Children.Add(dBody);
        dlg.Content = dp;

        btnOk.Click += async (_, _) => {
            var pw = pwBox.Password;
            try {
                using var conn = _db.Create();
                var existe = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM USUARIOS WHERE CONTRASEÑA_USUARIO=@c AND CARGO_USUARIO='ADMINISTRADOR'",
                    new { c = pw });
                if (existe > 0) { ok = true; dlg.Close(); }
                else MessageBox.Show("Contraseña incorrecta.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            } catch {
                MessageBox.Show("Error al validar.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        btnCx.Click  += (_, _) => dlg.Close();
        pwBox.KeyDown += async (_, e) => {
            if (e.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        dlg.Loaded += (_, _) => pwBox.Focus();
        dlg.ShowDialog();
        return ok;
    }
}

// POCOs para NC
internal class FilaVentaNC
{
    public int    IdCab      { get; set; }
    public string NSolicitud { get; set; } = "";
    public byte   IdLocal    { get; set; }
    public string Cliente    { get; set; } = "";
    public string Ci         { get; set; } = "";
    public int    IdCliente  { get; set; }
    public decimal Total     { get; set; }
    public DateTime Fecha    { get; set; }
    public string NVentaChar { get; set; } = "";
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string TotalFmt  => Total.ToString("N0", _py);
    public string FechaStr  => Fecha.ToString("dd/MM/yy");
    // Muestra solo los últimos 7 dígitos significativos (sin ceros a la izquierda)
    public string NSolCorta => NSolicitud.TrimStart('0') is string s && s.Length > 0 ? s : "0";
}
internal class FilaCuotaNC
{
    public int     IdCab       { get; set; }
    public string  NSolicitud  { get; set; } = "";
    public string  Comprobante { get; set; } = "";
    public byte    NCuota      { get; set; }
    public decimal Monto       { get; set; }
    public decimal Entrega     { get; set; }
    // SP devuelve VTO (string dd/MM/yyyy) y ESTADO (string "Pendiente"/"Cancelado")
    public string  VTO         { get; set; } = "";
    public string  ESTADO      { get; set; } = "";
    public byte    Cpha        { get; set; }   // CC de CABECERA_SALES
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string MontoFmt   => Monto.ToString("N0", _py);
    public string EntregaFmt => Entrega.ToString("N0", _py);
    // Aliases para bindings en la grilla
    public string VtoStr    => VTO;
    public string EstadoStr => ESTADO;
}
internal class FilaArtNC
{
    public int    IdArt       { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal Cantidad   { get; set; }
    public decimal Pventa     { get; set; }
    public string FechaStr    { get; set; } = "";
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string CantFmt => Cantidad.ToString("N3").TrimEnd('0').TrimEnd('.');
    public string PvFmt   => Pventa.ToString("N0", _py);
}

// ══════════════════════════════════════════════════════════════════════════════
//  GENERAR PAGOS  (genera cuotas para una venta a crédito existente)
// ══════════════════════════════════════════════════════════════════════════════
public class GenerarPagosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtIdCab = null!, _txtMonto = null!, _txtEntrega = null!, _txtCuotas = null!;
    private DatePicker _dtInicio = null!;

    public GenerarPagosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Generar Pagos / Cuotas"; Width = 420; Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = MakeSmHdr("Generar Pagos (Cuotas)", "#16A085");
        root.Children.Add(hdr);

        void AddRow(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _txtIdCab  = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; AddRow("N° Cabecera venta:", _txtIdCab);
        _txtMonto  = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; AddRow("Monto total a financiar:", _txtMonto);
        _txtEntrega= new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" }; AddRow("Entrega normal:", _txtEntrega);
        _txtCuotas = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "6" }; AddRow("Número de cuotas:", _txtCuotas);
        _dtInicio  = new DatePicker { SelectedDate = DateTime.Today }; AddRow("Fecha de inicio:", _dtInicio);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnG = MakeSmBtn("✔ Generar", "#27AE60"); btnG.Click += async (_, _) => await Generar();
        var btnC = MakeSmBtn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Generar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("N° Cabecera inválido."); return; }
        if (!decimal.TryParse(_txtMonto.Text, out var monto) || monto <= 0) { MessageBox.Show("Monto inválido."); return; }
        if (!decimal.TryParse(_txtEntrega.Text, out var entrega)) entrega = 0;
        if (!int.TryParse(_txtCuotas.Text, out var cuotas) || cuotas <= 0) { MessageBox.Show("Cuotas inválidas."); return; }
        var fechaInicio = _dtInicio.SelectedDate ?? DateTime.Today;
        var sesion = SessionService.Instance;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@NumeroDeFechas",  cuotas);
            p.Add("@monto",           monto);
            p.Add("@ENTREGANORMAL",   entrega);
            p.Add("@ID_LOCAL",        (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@ID_USU",          (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@FechaInicioExterna", fechaInicio);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("AGREGAR_GENERADAS_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Pagos generados. Resultado: {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static Border MakeSmHdr(string t, string hex) {
        var b = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12) };
        b.Child = new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        return b;
    }
    private static Button MakeSmBtn(string t, string hex) => new Button { Content = t, Height = 30, Width = 90, Margin = new Thickness(0, 0, 8, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
}

// ══════════════════════════════════════════════════════════════════════════════
//  EDITAR PAGOS
// ══════════════════════════════════════════════════════════════════════════════
public class EditarPagosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox  _txtIdCab = null!;
    private DataGrid _grid     = null!;

    public EditarPagosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Editar Pagos Generados"; Width = 820; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        var hdrB = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2980B9")!, Padding = new Thickness(12, 6, 12, 6) };
        hdrB.Child = new TextBlock { Text = "Editar Pagos Generados", Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdrB, Dock.Top); root.Children.Add(hdrB);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = new Button { Content = "Cerrar", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close(); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new DockPanel { Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "N° Cabecera venta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtIdCab = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 100, Margin = new Thickness(0, 0, 6, 0) };
        _txtIdCab.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Cargar(); };
        var btnB = new Button { Content = "Cargar cuotas", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(_txtIdCab); filterBar.Children.Add(btnB);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue, Margin = new Thickness(8, 0, 8, 0) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",      Binding = new System.Windows.Data.Binding("IDGENERADAS"), Width = 60 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N°Cuota", Binding = new System.Windows.Data.Binding("NCUOTA"),      Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Monto",   Binding = new System.Windows.Data.Binding("MONTO"),       Width = 80 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Entrega", Binding = new System.Windows.Data.Binding("ENTREGA"),     Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Vence",   Binding = new System.Windows.Data.Binding("VTO"),         Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",  Binding = new System.Windows.Data.Binding("ESTADO"),      Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new System.Windows.Data.Binding("CLIENTE"),     Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("N° Cabecera inválido."); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@IdCab", idCab);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_CUOTAS_GENERADAS_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ELIMINAR VENTA AL CONTADO
// ══════════════════════════════════════════════════════════════════════════════
public class EliminarVentaContadoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private Button     _btnLocal = null!;
    private TextBlock  _lblLocalNombre = null!;
    private DataGrid   _dgVentas = null!, _dgDetalle = null!;
    private TextBlock  _lblInfo = null!;

    private List<FilaVentaContado>   _ventas   = new();
    private List<FilaDetalleVentaC>  _detalle  = new();
    private FilaVentaContado?        _selVenta = null;
    private List<(byte Id, string Nombre)> _locales = new();
    private byte _localSelId   = 0;
    private string _localSelNom = "Todos los locales";

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public EliminarVentaContadoWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = App.Services.GetRequiredService<ISessionService>();
        Title = "Eliminar Venta al Contado";
        Width = 980; Height = 680; MinWidth = 860; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EBC("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        // Pedido explícito: listar apenas se abre, sin esperar que el usuario pulse
        // "Buscar" — el rango de fecha ya arranca en "hoy" por defecto (ver _dpDesde/_dpHasta
        // en BuildUI), así que hay un filtro válido desde el primer render.
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = EBC("#B71C1C"), Padding = new Thickness(18, 11, 18, 11) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "ELIMINAR VENTA AL CONTADO",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock { Text = "⚠  Elimina la venta completa o solo un artículo puntual — ambas acciones restauran el stock y son irreversibles",
            Foreground = EBC("#FFCDD2"), FontSize = 11 });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14, 8, 14, 8) };
        var pieSp = new DockPanel();
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#616161"), FontSize = 11 };
        DockPanel.SetDock(_lblInfo, Dock.Left); pieSp.Children.Add(_lblInfo);
        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        Button PBtn(string t, string bg) => new Button { Content = t, Height = 34,
            Padding = new Thickness(18,0,18,0), Margin = new Thickness(8,0,0,0),
            Background = EBC(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var btnQuitarArt = PBtn("➖  Quitar artículo seleccionado", "#E65100");
        var btnEliminar  = PBtn("🗑  Eliminar venta completa", "#B71C1C");
        var btnCerrar    = PBtn("✖  Cerrar", "#546E7A");
        btnQuitarArt.Click += async (_, _) => await QuitarArticulo();
        btnEliminar.Click  += async (_, _) => await Eliminar();
        btnCerrar.Click    += (_, _) => Close();
        btnsSp.Children.Add(btnQuitarArt); btnsSp.Children.Add(btnEliminar); btnsSp.Children.Add(btnCerrar);
        DockPanel.SetDock(btnsSp, Dock.Right); pieSp.Children.Add(btnsSp);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Filtros ───────────────────────────────────────────────────────
        var filtBar = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#FFCDD2"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(10, 8, 10, 6) };

        var filtSp = new StackPanel { Orientation = Orientation.Horizontal };

        // helper label
        void FLabel(string t) => filtSp.Children.Add(new TextBlock { Text = t,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold,
            Foreground = EBC("#B71C1C"), FontSize = 12 });

        // helper DatePicker container
        Border DateBox(DatePicker dp) {
            var b = new Border { Background = EBC("#FFF5F5"), BorderBrush = EBC("#FFCDD2"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2) };
            b.Child = dp; return b;
        }

        _dpDesde = new DatePicker { SelectedDate = DateTime.Today,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        _dpHasta = new DatePicker { SelectedDate = DateTime.Today,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        // Pedido explícito: recargar solo al cambiar fecha o local, sin esperar "Buscar".
        _dpDesde.SelectedDateChanged += async (_, _) => await Cargar();
        _dpHasta.SelectedDateChanged += async (_, _) => await Cargar();

        FLabel("Desde:");
        filtSp.Children.Add(DateBox(_dpDesde));
        filtSp.Children.Add(new Border { Width = 14 });
        FLabel("Hasta:");
        filtSp.Children.Add(DateBox(_dpHasta));

        // Separador vertical
        filtSp.Children.Add(new Border { Width = 1, Background = EBC("#FFCDD2"),
            Margin = new Thickness(16, 2, 16, 2) });

        // Botón selector de local
        var localIconPath = new System.Windows.Shapes.Path {
            Fill = System.Windows.Media.Brushes.White,
            Data = System.Windows.Media.Geometry.Parse(
                "M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z" +
                "M12 11.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"),
            Width = 14, Height = 14, Stretch = System.Windows.Media.Stretch.Uniform,
            Margin = new Thickness(0, 0, 6, 0) };

        _lblLocalNombre = new TextBlock {
            Text = "Todos los locales", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12 };
        var chevron = new TextBlock { Text = " ▾", VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#FFCDD2"), FontSize = 11 };

        var btnLocalContent = new StackPanel { Orientation = Orientation.Horizontal };
        btnLocalContent.Children.Add(localIconPath);
        btnLocalContent.Children.Add(_lblLocalNombre);
        btnLocalContent.Children.Add(chevron);

        _btnLocal = new Button {
            Content = btnLocalContent, Height = 34,
            Padding = new Thickness(14, 0, 12, 0),
            Background = EBC("#C62828"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 12 };
        _btnLocal.Click += (_, _) => AbrirSelectorLocal();

        filtSp.Children.Add(_btnLocal);

        // Separador
        filtSp.Children.Add(new Border { Width = 14 });

        // Botón Buscar
        var buscarContent = new StackPanel { Orientation = Orientation.Horizontal };
        buscarContent.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,6,0) });
        buscarContent.Children.Add(new TextBlock { Text = "Buscar", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold });
        var btnBuscar = new Button {
            Content = buscarContent, Height = 34,
            Padding = new Thickness(16, 0, 16, 0),
            Background = EBC("#B71C1C"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnBuscar.Click += async (_, _) => await Cargar();
        filtSp.Children.Add(btnBuscar);

        filtBar.Child = filtSp;
        DockPanel.SetDock(filtBar, Dock.Top); root.Children.Add(filtBar);

        // ── Instrucción ───────────────────────────────────────────────────
        var instrBar = new Border { Background = EBC("#FFF8E1"),
            BorderBrush = EBC("#FFD54F"), BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(14, 6, 14, 6) };
        instrBar.Child = new TextBlock { FontSize = 11, Foreground = EBC("#5D4037"),
            Text = "PASO 1: Haga clic sobre una venta para cargar sus productos abajo.   " +
                   "PASO 2: Verifique los productos y presione 'Eliminar Venta' si está seguro." };
        DockPanel.SetDock(instrBar, Dock.Top); root.Children.Add(instrBar);

        // ── Cuerpo: grilla ventas (arriba) + detalle (abajo) ─────────────
        var body = new Grid { Margin = new Thickness(10, 6, 10, 6) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(body);

        Style ColHdrS(string bg) {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty, EBC(bg)));
            s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
            return s;
        }

        // Grid ventas
        _dgVentas = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EBC("#FFEBEE"),
            BorderThickness = new Thickness(1), BorderBrush = EBC("#FFCDD2"),
            ColumnHeaderStyle = ColHdrS("#B71C1C"), FontSize = 12, RowHeight = 30 };

        DataGridTextColumn DCV(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty,a)}};
            return c;
        }
        _dgVentas.Columns.Add(DCV("IDCAB",    "IdCab",    0.5, TextAlignment.Center));
        _dgVentas.Columns.Add(DCV("N° Venta", "NVenta",   1.0));
        _dgVentas.Columns.Add(DCV("Local",    "NomLocal", 0.8));
        _dgVentas.Columns.Add(DCV("Cliente",  "Cliente",  2.0));
        _dgVentas.Columns.Add(DCV("Total",    "TotalFmt", 0.9, TextAlignment.Right));
        _dgVentas.Columns.Add(DCV("Fecha",    "FechaStr", 0.9));
        // "Usuario" ya trae el nombre real del vendedor (U.NOMBRE_USUARIO vía
        // CABECERA_SALES.ID_USUARIO) — se quitó la columna "ID_USER" con el id crudo, que
        // no aportaba nada útil y generaba confusión sobre quién vendió realmente.
        _dgVentas.Columns.Add(DCV("Vendedor", "Usuario",  1.4));
        // Pedido explícito: cargar el detalle con un solo clic (selección), no doble clic.
        _dgVentas.SelectionChanged += async (_, _) => await CargarDetalle();
        Grid.SetRow(_dgVentas, 0); body.Children.Add(_dgVentas);

        // Grid detalle artículos
        _dgDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = EBC("#FFCDD2"),
            ColumnHeaderStyle = ColHdrS("#C62828"), FontSize = 13, RowHeight = 42 };
        _dgDetalle.Columns.Add(DCV("Id",           "IdArt",     0.4, TextAlignment.Center));
        _dgDetalle.Columns.Add(DCV("Nombre/Desc",  "Nombre",    3.0));
        _dgDetalle.Columns.Add(DCV("Precio venta", "PvFmt",     1.0, TextAlignment.Right));
        _dgDetalle.Columns.Add(DCV("Cantidad",     "CantFmt",   0.7, TextAlignment.Center));
        Grid.SetRow(_dgDetalle, 2); body.Children.Add(_dgDetalle);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        // cargar locales y datos iniciales
        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        try {
            using var conn = _db.Create();
            _locales = (await conn.QueryAsync<(byte Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL")).ToList();
            // preseleccionar local del usuario
            var idLocalSes = (byte)(_session.LocalActual?.IdLocal ?? 0);
            var localSes = _locales.FirstOrDefault(l => l.Id == idLocalSes);
            if (localSes.Id != 0) {
                _localSelId  = localSes.Id;
                _localSelNom = localSes.Nombre;
                _lblLocalNombre.Text = localSes.Nombre;
            }
            await Cargar();
        } catch (Exception ex) {
            MessageBox.Show($"Error al inicializar: {ex.Message}");
        }
    }

    private void AbrirSelectorLocal()
    {
        var dlg = new Window {
            Title = "Seleccionar Local",
            Width = 520, Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        var outer = new Border {
            Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 24, Opacity = 0.22, ShadowDepth = 4, Direction = 270,
                Color = System.Windows.Media.Colors.Black },
            ClipToBounds = true };

        var dp = new DockPanel();

        // Header degradado
        var hdr = new Border { Padding = new Thickness(20, 16, 20, 16) };
        var hdrBrush = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(183, 28, 28),
            System.Windows.Media.Color.FromRgb(211, 47, 47), 0);
        hdr.Background = hdrBrush;
        var hdrDp = new DockPanel();
        var hdrTitle = new StackPanel();
        hdrTitle.Children.Add(new TextBlock { Text = "📍  Seleccionar Local",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hdrTitle.Children.Add(new TextBlock { Text = "Elige el local para filtrar las ventas",
            Foreground = EBC("#FFCDD2"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        var btnX = new Button { Content = "✕", Width = 28, Height = 28,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = EBC("#FFCDD2"), BorderThickness = new Thickness(0),
            FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top };
        btnX.Click += (_, _) => dlg.Close();
        DockPanel.SetDock(btnX, Dock.Right);
        hdrDp.Children.Add(btnX); hdrDp.Children.Add(hdrTitle);
        hdr.Child = hdrDp;
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        // Área de cards con scroll
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16, 14, 16, 14) };
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        Border MakeCard(byte id, string nombre, bool isTodos = false)
        {
            bool selected = isTodos ? _localSelId == 0 : _localSelId == id;
            string icon   = isTodos ? "🏪" : $"📦";
            string codStr = isTodos ? "" : $"Local #{id}";
            string clrBg  = selected ? "#B71C1C" : "#FAFAFA";
            string clrFg  = selected ? "White"   : "#212121";
            string clrSub = selected ? "#FFCDD2" : "#757575";
            string clrBdr = selected ? "#B71C1C" : "#E0E0E0";

            var card = new Border {
                Width = 140, Height = 100, Margin = new Thickness(6),
                Background = EBC(clrBg), CornerRadius = new CornerRadius(10),
                BorderBrush = EBC(clrBdr), BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = selected ? new System.Windows.Media.Effects.DropShadowEffect {
                    BlurRadius = 12, Opacity = 0.3, ShadowDepth = 2, Direction = 270,
                    Color = System.Windows.Media.Color.FromRgb(183,28,28) } : null };

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = icon, FontSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,4) });
            sp.Children.Add(new TextBlock { Text = nombre, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = EBC(clrFg), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 120,
                HorizontalAlignment = HorizontalAlignment.Center });
            if (!string.IsNullOrEmpty(codStr))
                sp.Children.Add(new TextBlock { Text = codStr, FontSize = 10,
                    Foreground = EBC(clrSub), TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = sp;

            card.MouseLeftButtonUp += async (_, _) => {
                _localSelId  = isTodos ? (byte)0 : id;
                _localSelNom = isTodos ? "Todos los locales" : nombre;
                _lblLocalNombre.Text = _localSelNom;
                dlg.Close();
                await Cargar();
            };
            // hover
            card.MouseEnter += (_, _) => {
                if (!selected) card.Background = EBC("#FFEBEE");
            };
            card.MouseLeave += (_, _) => {
                if (!selected) card.Background = EBC(clrBg);
            };
            return card;
        }

        wrap.Children.Add(MakeCard(0, "Todos los locales", isTodos: true));
        foreach (var (id, nom) in _locales)
            wrap.Children.Add(MakeCard(id, nom));

        scroll.Content = wrap;
        dp.Children.Add(scroll);
        outer.Child = dp; dlg.Content = outer;

        // arrastrar la ventana sin titlebar
        hdr.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) dlg.DragMove(); };

        dlg.ShowDialog();
    }

    private async Task Cargar()
    {
        try {
            using var conn = _db.Create();
            var desde = _dpDesde.SelectedDate ?? DateTime.Today;
            var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
            var idLocal = _localSelId;

            // CABECERA_SALES.ID_USUARIO guarda el ID_USUARIO interno de quien vendió (así lo
            // graba VentaRepository al insertar la venta, consistente con AtrasosWindow y
            // AdicionalesWindows). Bug real encontrado: acá comparaba contra CODIGO_USUARIO en
            // vez de ID_USUARIO — coincidía por casualidad numérica con el CÓDIGO de OTRO
            // usuario en la mayoría de los casos (mostrando vendedor equivocado), y cuando no
            // había ninguna coincidencia (INNER JOIN) la venta directamente desaparecía de la
            // lista, aunque existiera y estuviera dentro del rango de fecha filtrado.
            _ventas = (await conn.QueryAsync<FilaVentaContado>(
                @"SELECT CS.IDCAB, CS.NVENTACHAR AS NVenta, CS.ID_LOCAL AS IdLocal,
                         L.NOMBRE AS NomLocal, C.NOMBRE_CLIENTE AS Cliente,
                         CS.TOTAL, CS.FECHA, U.NOMBRE_USUARIO AS Usuario,
                         CS.ID_USUARIO AS IdUsuario
                  FROM CABECERA_SALES CS
                  INNER JOIN CLIENTES C ON C.ID_CLIENTE = CS.ID_CLIENTE
                  INNER JOIN LOCALES  L ON L.ID_LOCAL   = CS.ID_LOCAL
                  INNER JOIN USUARIOS U ON U.ID_USUARIO = CS.ID_USUARIO
                  WHERE CS.FORMA_DE_VENTA = 1
                    AND CS.FECHA >= @d AND CS.FECHA < @h
                    AND (@l = 0 OR CS.ID_LOCAL = @l)
                  ORDER BY CS.FECHA DESC, CS.IDCAB DESC",
                new { d = desde, h = hasta, l = idLocal })).ToList();

            _dgVentas.ItemsSource = _ventas;
            _dgDetalle.ItemsSource = null;
            _selVenta = null;
            _lblInfo.Text = $"{_ventas.Count} venta(s) encontrada(s). Doble clic para ver artículos.";
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CargarDetalle()
    {
        if (_dgVentas.SelectedItem is not FilaVentaContado v) return;
        _selVenta = v;
        try {
            using var conn = _db.Create();
            _detalle = (await conn.QueryAsync<FilaDetalleVentaC>(
                @"SELECT DS.IDDET, DS.IDCAB, DS.IDART, A.D AS Nombre,
                         DS.PV, DS.CANTIDAD
                  FROM DETALLES_SALES DS
                  INNER JOIN ARTICULOS A ON A.ID = DS.IDART
                  WHERE DS.IDCAB = @id
                  ORDER BY DS.IDDET",
                new { id = v.IdCab })).ToList();
            _dgDetalle.ItemsSource = _detalle;
            _lblInfo.Text = $"Venta {v.NVenta?.Trim()} — {v.Cliente} — {_detalle.Count} artículo(s). Verifique y presione 'Eliminar venta'.";
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}");
        }
    }

    private async Task Eliminar()
    {
        if (_selVenta == null) {
            MessageBox.Show("Haga doble clic sobre una venta para seleccionarla primero.",
                "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (_detalle.Count == 0) {
            MessageBox.Show("Cargue los artículos de la venta haciendo doble clic primero.",
                "Sin detalle", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var v = _selVenta;
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");

        // diálogo de advertencia
        var dlg = new Window {
            Title = "Confirmar Eliminación y Ajuste de Caja",
            Width = 520, Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        var tcs = new TaskCompletionSource<bool>();
        var dp = new DockPanel();
        var dhdr = new Border { Background = EBC("#B71C1C"), Padding = new Thickness(14,10,14,10) };
        dhdr.Child = new TextBlock { Text = "Confirmar Eliminación y Ajuste de Caja",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var bSi = new Button { Content = "Sí", Width = 70, Height = 32, Margin = new Thickness(0,0,8,0),
            Background = EBC("#B71C1C"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bNo = new Button { Content = "No", Width = 70, Height = 32,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        bSi.Click += (_, _) => { tcs.SetResult(true);  dlg.Close(); };
        bNo.Click += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dps.Children.Add(bSi); dps.Children.Add(bNo); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);
        var dbody = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        dbody.Children.Add(new TextBlock { Text = "¡ADVERTENCIA DE CAJA!",
            FontWeight = FontWeights.Bold, Foreground = EBC("#B71C1C"), FontSize = 13,
            Margin = new Thickness(0,0,0,8) });
        dbody.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Text = $"Esta acción eliminará la venta definitivamente del sistema.\n\n" +
                   $"  • N° de Venta: {v.NVenta?.Trim()}\n" +
                   $"  • Cliente: {v.Cliente}\n" +
                   $"  • Total: {v.Total.ToString("N0", fmt)} Gs.\n\n" +
                   "NOTA IMPORTANTE:\n" +
                   "Recuerde verificar el flujo de efectivo. Si ya realizó el arqueo de caja " +
                   "de este período, deberá ajustar (retirar/ingresar) manualmente el dinero físico para evitar diferencias." });
        dbody.Children.Add(new TextBlock { Text = "\n¿Está completamente seguro de continuar?",
            FontWeight = FontWeights.SemiBold, Foreground = EBC("#B71C1C") });
        dp.Children.Add(dbody); dlg.Content = dp;
        dlg.ShowDialog();
        if (!await tcs.Task) return;

        // pedir contraseña admin
        if (!await ValidarAdminEVC()) return;

        try {
            using var conn = _db.Create();
            int idUsuario = _session.UsuarioActual?.IdUsuario ?? 1;

            // @AGENTE='SI' en el primer artículo: DELETE DET_CAJA + DELETE CABECERA_SALES
            //   (cascade elimina DETALLES_SALES, GENERADAS, CAJA_DETALLE, DOCUMENTOS)
            //   + UPDATE PRICES (devuelve stock) + INSERT MOVART
            // @AGENTE='NO' en los siguientes: solo UPDATE PRICES + INSERT MOVART
            // CAB_CAJA y CAJA_MASTER almacenan 0 — se recalculan desde DET_CAJA/CAJA_DETALLE,
            // que ya quedaron eliminados por cascade. No requieren ajuste manual.
            bool primero = true;
            foreach (var det in _detalle) {
                var p = new DynamicParameters();
                p.Add("@AGENTE",      primero ? "SI" : "NO");
                p.Add("@idcab",       v.IdCab);
                p.Add("@idart",       det.IdArt);
                p.Add("@cantidad",    det.Cantidad);
                p.Add("@idlocal",     v.IdLocal);
                p.Add("@idumodstock", idUsuario);
                p.Add("@IDMOVART",    0);
                p.Add("@MOV",         (byte)1);  // 1 = ingreso stock por devolución
                p.Add("@MOD",         (byte)0);
                p.Add("@STINI",       (decimal)0);
                p.Add("@PCANT",       (decimal)0);
                // parámetros de caja — el SP los recibe pero están comentados internamente
                p.Add("@ID_DET_CAJA", 0);
                p.Add("@IDCABCAJA",   0);
                p.Add("@CAJA",        (byte)0);
                p.Add("@COUNTCAJA",   0);
                p.Add("@ACCION",      (byte)0);
                p.Add("@CONCEPTO",    (byte)0);
                p.Add("@MONTO",       (decimal)0);
                p.Add("@METODO",      (byte)0);
                p.Add("@NUMERO",      "");
                p.Add("@PARA",        0);
                p.Add("@OBS",         "");
                p.Add("@idcabecera",  v.IdCab);
                p.Add("@msg",         dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
                await conn.ExecuteAsync("ELIMINAR_VENTA_CONTADO_CS", p, commandType: CommandType.StoredProcedure);
                var spMsg = p.Get<string>("@msg") ?? "";
                if (spMsg.Trim() != "GUARDADO")
                    throw new Exception($"SP devolvió '{spMsg}' para artículo ID {det.IdArt}");
                primero = false;
            }

            MessageBox.Show($"Venta {v.NVenta?.Trim()} eliminada correctamente.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            _selVenta = null;
            await Cargar();
        } catch (Exception ex) {
            MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Quita UN artículo puntual de una venta al contado sin borrar la venta entera —
    // ELIMINAR_VENTA_CONTADO_CS (SP legado) siempre borra la cabecera completa en cascada
    // al primer artículo, así que no sirve acá; se replica a mano el mismo ajuste que se
    // hizo manualmente por SQL para el caso real (venta 33278, Dep. Tavai: se retiró un
    // Ropero 3P de una venta combinada con otro artículo, dejando el resto intacto):
    // 1) borra la línea de DETALLES_SALES, 2) recalcula TOTAL/ENTREGANORMAL en
    // CABECERA_SALES restando esa línea, 3) ajusta el monto en CAJA_DETALLE (si la caja
    // sigue abierta, el nuevo total se refleja solo — si ya cerró, requiere ajuste manual,
    // mismo aviso que "Eliminar venta completa"), 4) devuelve el stock en PRICES,
    // 5) registra el movimiento en MOVART para trazabilidad.
    private async Task QuitarArticulo()
    {
        if (_selVenta == null) {
            MessageBox.Show("Haga clic sobre una venta para seleccionarla primero.",
                "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (_dgDetalle.SelectedItem is not FilaDetalleVentaC art) {
            MessageBox.Show("Seleccione el artículo a quitar en la grilla de abajo.",
                "Sin artículo seleccionado", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (_detalle.Count <= 1) {
            MessageBox.Show("Esta venta tiene un solo artículo — use 'Eliminar venta completa' en vez de quitar la línea.",
                "No aplica", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var v = _selVenta;
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        decimal nuevoTotal = v.Total - (art.Pv * art.Cantidad);

        // diálogo de advertencia
        var dlg = new Window {
            Title = "Confirmar Ajuste de Venta y Caja",
            Width = 520, Height = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        var tcs = new TaskCompletionSource<bool>();
        var dp = new DockPanel();
        var dhdr = new Border { Background = EBC("#E65100"), Padding = new Thickness(14,10,14,10) };
        dhdr.Child = new TextBlock { Text = "Confirmar Ajuste de Venta y Caja",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var bSi = new Button { Content = "Sí", Width = 70, Height = 32, Margin = new Thickness(0,0,8,0),
            Background = EBC("#E65100"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bNo = new Button { Content = "No", Width = 70, Height = 32,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        bSi.Click += (_, _) => { tcs.SetResult(true);  dlg.Close(); };
        bNo.Click += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dps.Children.Add(bSi); dps.Children.Add(bNo); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);
        var dbody = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        dbody.Children.Add(new TextBlock { Text = "¡ADVERTENCIA DE CAJA!",
            FontWeight = FontWeights.Bold, Foreground = EBC("#E65100"), FontSize = 13,
            Margin = new Thickness(0,0,0,8) });
        dbody.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Text = $"Se quitará este artículo de la venta {v.NVenta?.Trim()} (el resto queda intacto).\n\n" +
                   $"  • Artículo: {art.Nombre}\n" +
                   $"  • Cantidad: {art.CantFmt}\n" +
                   $"  • Precio: {art.Pv.ToString("N0", fmt)} Gs.\n" +
                   $"  • Total actual de la venta: {v.Total.ToString("N0", fmt)} Gs.\n" +
                   $"  • Total nuevo de la venta: {nuevoTotal.ToString("N0", fmt)} Gs.\n\n" +
                   "NOTA IMPORTANTE:\n" +
                   "Recuerde verificar el flujo de efectivo. Si ya realizó el arqueo de caja " +
                   "de este período, deberá ajustar (retirar/ingresar) manualmente el dinero físico para evitar diferencias." });
        dbody.Children.Add(new TextBlock { Text = "\n¿Está completamente seguro de continuar?",
            FontWeight = FontWeights.SemiBold, Foreground = EBC("#E65100") });
        dp.Children.Add(dbody); dlg.Content = dp;
        dlg.ShowDialog();
        if (!await tcs.Task) return;

        // pedir contraseña admin
        if (!await ValidarAdminEVC()) return;

        try {
            using var conn = _db.Create();
            int idUsuario = _session.UsuarioActual?.IdUsuario ?? 1;

            // 1) Eliminar la línea del detalle
            await conn.ExecuteAsync(
                "DELETE FROM DETALLES_SALES WHERE IDDET = @iddet",
                new { iddet = art.IdDet });

            // 2) Recalcular TOTAL/ENTREGANORMAL en CABECERA_SALES restando esa línea —
            // en venta de contado ambos campos deben seguir coincidiendo entre sí.
            await conn.ExecuteAsync(
                "UPDATE CABECERA_SALES SET TOTAL = @nuevo, ENTREGANORMAL = @nuevo WHERE IDCAB = @idcab",
                new { nuevo = nuevoTotal, idcab = v.IdCab });

            // 3) Ajustar el monto en CAJA_DETALLE — si la caja sigue abierta, el nuevo total
            // se refleja solo en los reportes que suman en vivo (Explorador de Caja, Arqueo).
            await conn.ExecuteAsync(
                @"UPDATE CAJA_DETALLE SET MONTO = @nuevo,
                         CONCEPTO = CONCEPTO + ' (AJUSTADA: se retiró ' + @nombreArt + ')'
                  WHERE ID_VENTA = @idcab",
                new { nuevo = nuevoTotal, idcab = v.IdCab, nombreArt = art.Nombre });

            // 4) Devolver el stock del artículo quitado
            await conn.ExecuteAsync(
                "UPDATE PRICES SET S = S + @cant WHERE IDART = @idart AND IDLOCAL = @idlocal",
                new { cant = art.Cantidad, idart = art.IdArt, idlocal = v.IdLocal });

            // 5) Trazabilidad en MOVART — mismo patrón que ELIMINAR_VENTA_CONTADO_CS
            // (MOV=1 = ingreso de stock por devolución).
            var nuevoIdMovart = await conn.ExecuteScalarAsync<int>("SELECT ISNULL(MAX(IDMOVART),0)+1 FROM MOVART");
            await conn.ExecuteAsync(
                @"INSERT INTO MOVART(IDMOVART,IDART,MOV,MOD,STINI,CANT,IDLOCAL,IDDESTINO,PCANT,PCACT,IDU,FECHA)
                  VALUES(@idmovart,@idart,1,0,0,@cant,@idlocal,@idlocal,@pcant,@pcant,@idu,GETDATE())",
                new { idmovart = nuevoIdMovart, idart = art.IdArt, cant = art.Cantidad,
                      idlocal = v.IdLocal, pcant = art.Pv, idu = idUsuario });

            MessageBox.Show($"Artículo \"{art.Nombre}\" quitado de la venta {v.NVenta?.Trim()} correctamente.\n\nNuevo total: {nuevoTotal.ToString("N0", fmt)} Gs.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            _selVenta = null;
            await Cargar();
        } catch (Exception ex) {
            MessageBox.Show($"Error al quitar el artículo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> ValidarAdminEVC()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window {
            Title = "Contraseña de Administrador", Width = 360, Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        var dp = new DockPanel();
        var dhdr = new Border { Background = EBC("#37474F"), Padding = new Thickness(14,10,14,10) };
        dhdr.Child = new TextBlock { Text = "CONTRASEÑA DE ADMINISTRADOR",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var bAcept = new Button { Content = "✔  Aceptar", Width = 100, Height = 32, Margin = new Thickness(0,0,10,0),
            Background = EBC("#2E7D32"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bCanc = new Button { Content = "✖  Cerrar", Width = 90, Height = 32,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        dps.Children.Add(bAcept); dps.Children.Add(bCanc); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);

        var body = new StackPanel { Margin = new Thickness(20, 14, 20, 10) };
        var lblErr = new TextBlock { Foreground = EBC("#C62828"), FontSize = 11,
            Margin = new Thickness(0,0,0,4), Visibility = Visibility.Collapsed };
        var pb = new PasswordBox { Padding = new Thickness(8,5,8,5),
            BorderBrush = EBC("#BDBDBD"), BorderThickness = new Thickness(1) };
        body.Children.Add(lblErr); body.Children.Add(pb);
        dp.Children.Add(body); dlg.Content = dp;

        async void Validar() {
            var pwd = pb.Password;
            if (string.IsNullOrEmpty(pwd)) { lblErr.Text="Ingrese la contraseña."; lblErr.Visibility=Visibility.Visible; return; }
            try {
                using var conn = _db.Create();
                var ok = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM USUARIOS
                      WHERE CONTRASEÑA_USUARIO=@p AND UPPER(CARGO_USUARIO)='ADMINISTRADOR'",
                    new { p = pwd });
                if (ok > 0) { tcs.SetResult(true); dlg.Close(); }
                else { lblErr.Text="Contraseña incorrecta."; lblErr.Visibility=Visibility.Visible; pb.Clear(); pb.Focus(); }
            } catch (Exception ex) { lblErr.Text=$"Error: {ex.Message}"; lblErr.Visibility=Visibility.Visible; }
        }
        bAcept.Click += (_, _) => Validar();
        pb.KeyDown   += (_, e) => { if (e.Key == Key.Enter) Validar(); };
        bCanc.Click  += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed   += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dlg.ShowDialog();
        return await tcs.Task;
    }
}

internal class FilaVentaContado
{
    public int     IdCab    { get; set; }
    public string  NVenta   { get; set; } = "";
    public byte    IdLocal  { get; set; }
    public string  NomLocal { get; set; } = "";
    public string  Cliente  { get; set; } = "";
    public decimal Total    { get; set; }
    public DateTime Fecha   { get; set; }
    public string  Usuario  { get; set; } = "";
    public int     IdUsuario{ get; set; }
    public string  FechaStr => Fecha.ToString("dd/MM/yyyy HH:mm");
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string  TotalFmt => Total.ToString("N0", _py);
}

internal class FilaDetalleVentaC
{
    public int     IdDet    { get; set; }
    public int     IdCab    { get; set; }
    public int     IdArt    { get; set; }
    public string  Nombre   { get; set; } = "";
    public decimal Pv       { get; set; }
    public decimal Cantidad { get; set; }
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string PvFmt   => Pv.ToString("N0", _py);
    public string CantFmt => Cantidad.ToString("N3").TrimEnd('0').TrimEnd('.');
}

// ══════════════════════════════════════════════════════════════════════════════
//  ELIMINAR VENTA A CRÉDITO
// ══════════════════════════════════════════════════════════════════════════════
public class EliminarVentaCreditoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private Button     _btnLocal = null!;
    private TextBlock  _lblLocalNombre = null!;
    private DataGrid   _dgVentas = null!, _dgDetalle = null!, _dgCuotas = null!;
    private TextBlock  _lblInfo = null!;

    private List<FilaVentaCredito>        _ventas   = new();
    private List<FilaDetalleVentaC>       _detalle  = new();
    // Pedido explícito: antes de anular, mostrar el estado real de cada cuota generada
    // (monto, recargo/INFORCONF, punitorio, mora, si ya fue cobrada) — sin esto no se veía
    // qué se estaba a punto de perder al anular una venta con cuotas ya cobradas o con
    // cargos aplicados.
    private List<FilaCuotaEliminar>       _cuotas   = new();
    private FilaVentaCredito?             _selVenta = null;
    private List<(byte Id, string Nombre)> _locales = new();
    private byte   _localSelId  = 0;
    private string _localSelNom = "Todos los locales";

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public EliminarVentaCreditoWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = App.Services.GetRequiredService<ISessionService>();
        Title    = "Eliminar Venta a Crédito";
        Width = 1200; Height = 700; MinWidth = 1000; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EBC("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = EBC("#1A237E"), Padding = new Thickness(18, 11, 18, 11) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "ELIMINAR VENTA A CRÉDITO",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock { Text = "⚠  Esta acción anula la venta, elimina cuotas y restaura el stock — es irreversible",
            Foreground = EBC("#C5CAE9"), FontSize = 11 });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14, 8, 14, 8) };
        var pieSp = new DockPanel();
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#616161"), FontSize = 11 };
        DockPanel.SetDock(_lblInfo, Dock.Left); pieSp.Children.Add(_lblInfo);
        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        Button PBtn(string t, string bg) => new Button { Content = t, Height = 34,
            Padding = new Thickness(18,0,18,0), Margin = new Thickness(8,0,0,0),
            Background = EBC(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var btnAnular  = PBtn("🗑  Anular venta", "#1A237E");
        var btnCerrar  = PBtn("✖  Cerrar",        "#546E7A");
        btnAnular.Click += async (_, _) => await Anular();
        btnCerrar.Click += (_, _) => Close();
        btnsSp.Children.Add(btnAnular); btnsSp.Children.Add(btnCerrar);
        DockPanel.SetDock(btnsSp, Dock.Right); pieSp.Children.Add(btnsSp);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Filtros ───────────────────────────────────────────────────────
        var filtBar = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#C5CAE9"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(10, 8, 10, 6) };
        var filtSp = new StackPanel { Orientation = Orientation.Horizontal };

        void FLabel(string t) => filtSp.Children.Add(new TextBlock { Text = t,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold,
            Foreground = EBC("#1A237E"), FontSize = 12 });

        Border DateBox(DatePicker dp) {
            var b = new Border { Background = EBC("#F5F5FF"), BorderBrush = EBC("#C5CAE9"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2) };
            b.Child = dp; return b;
        }

        _dpDesde = new DatePicker { SelectedDate = DateTime.Today,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        _dpHasta = new DatePicker { SelectedDate = DateTime.Today,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };

        FLabel("Desde:"); filtSp.Children.Add(DateBox(_dpDesde));
        filtSp.Children.Add(new Border { Width = 14 });
        FLabel("Hasta:"); filtSp.Children.Add(DateBox(_dpHasta));
        filtSp.Children.Add(new Border { Width = 1, Background = EBC("#C5CAE9"), Margin = new Thickness(16,2,16,2) });

        // Auto-refresco al cambiar el rango de fechas — antes había que apretar "Buscar" a
        // mano, y quedaba en pantalla "0 ventas encontradas" con el rango viejo sin que fuera
        // obvio que hacía falta re-buscar.
        _dpDesde.SelectedDateChanged += async (_, _) => await Cargar();
        _dpHasta.SelectedDateChanged += async (_, _) => await Cargar();

        _lblLocalNombre = new TextBlock {
            Text = "Todos los locales", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, FontSize = 12 };
        var chevron = new TextBlock { Text = " ▾", VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#C5CAE9"), FontSize = 11 };
        var btnLocalContent = new StackPanel { Orientation = Orientation.Horizontal };
        btnLocalContent.Children.Add(new TextBlock { Text = "📍 ", VerticalAlignment = VerticalAlignment.Center });
        btnLocalContent.Children.Add(_lblLocalNombre);
        btnLocalContent.Children.Add(chevron);
        _btnLocal = new Button {
            Content = btnLocalContent, Height = 34, Padding = new Thickness(14,0,12,0),
            Background = EBC("#283593"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12 };
        _btnLocal.Click += (_, _) => AbrirSelectorLocal();
        filtSp.Children.Add(_btnLocal);
        filtSp.Children.Add(new Border { Width = 14 });

        var buscarContent = new StackPanel { Orientation = Orientation.Horizontal };
        buscarContent.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
        buscarContent.Children.Add(new TextBlock { Text = "Buscar", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
        var btnBuscar = new Button {
            Content = buscarContent, Height = 34, Padding = new Thickness(16,0,16,0),
            Background = EBC("#1A237E"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnBuscar.Click += async (_, _) => await Cargar();
        filtSp.Children.Add(btnBuscar);
        filtBar.Child = filtSp;
        DockPanel.SetDock(filtBar, Dock.Top); root.Children.Add(filtBar);

        // ── Instrucción ───────────────────────────────────────────────────
        var instrBar = new Border { Background = EBC("#FFF8E1"),
            BorderBrush = EBC("#FFD54F"), BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(14, 6, 14, 6) };
        instrBar.Child = new TextBlock { FontSize = 11, Foreground = EBC("#5D4037"),
            Text = "PASO 1: Haga clic sobre una venta para cargar sus artículos abajo.   " +
                   "PASO 2: Verifique los artículos y presione 'Anular Venta' si está seguro." };
        DockPanel.SetDock(instrBar, Dock.Top); root.Children.Add(instrBar);

        // ── Cuerpo ────────────────────────────────────────────────────────
        var body = new Grid { Margin = new Thickness(10, 6, 10, 6) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(body);

        Style ColHdrS(string bg) {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty,  EBC(bg)));
            s.Setters.Add(new Setter(Control.ForegroundProperty,  System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(Control.FontWeightProperty,  FontWeights.Bold));
            s.Setters.Add(new Setter(Control.FontSizeProperty,    11.0));
            s.Setters.Add(new Setter(Control.PaddingProperty,     new Thickness(8,5,8,5)));
            return s;
        }

        DataGridTextColumn DCV(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty, a)}};
            return c;
        }

        _dgVentas = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EBC("#E8EAF6"),
            BorderThickness = new Thickness(1), BorderBrush = EBC("#C5CAE9"),
            ColumnHeaderStyle = ColHdrS("#1A237E"), FontSize = 12, RowHeight = 30 };
        _dgVentas.Columns.Add(DCV("IDCAB",      "IdCab",     0.5, TextAlignment.Center));
        _dgVentas.Columns.Add(DCV("N° Venta",   "NVenta",    1.0));
        _dgVentas.Columns.Add(DCV("Local",       "NomLocal",  0.8));
        _dgVentas.Columns.Add(DCV("Cliente",     "Cliente",   2.0));
        _dgVentas.Columns.Add(DCV("Total",       "TotalFmt",  0.9, TextAlignment.Right));
        _dgVentas.Columns.Add(DCV("Entrega",     "EntregaFmt",0.9, TextAlignment.Right));
        _dgVentas.Columns.Add(DCV("Cuotas",      "Cuotas",    0.5, TextAlignment.Center));
        _dgVentas.Columns.Add(DCV("Fecha",       "FechaStr",  0.9));
        _dgVentas.Columns.Add(DCV("Vendedor",    "Usuario",   1.2));
        // Pedido explícito: cargar el detalle con un solo clic (selección), no doble clic.
        _dgVentas.SelectionChanged += async (_, _) => await CargarDetalle();
        Grid.SetRow(_dgVentas, 0); body.Children.Add(_dgVentas);

        _dgDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = EBC("#C5CAE9"),
            ColumnHeaderStyle = ColHdrS("#283593"), FontSize = 12, RowHeight = 38 };
        _dgDetalle.Columns.Add(DCV("Id",           "IdArt",   0.4, TextAlignment.Center));
        _dgDetalle.Columns.Add(DCV("Artículo",     "Nombre",  3.5));
        _dgDetalle.Columns.Add(DCV("Precio",       "PvFmt",   1.0, TextAlignment.Right));
        _dgDetalle.Columns.Add(DCV("Cantidad",     "CantFmt", 0.7, TextAlignment.Center));

        // Pedido explícito: antes de anular, ver el estado real de cada cuota generada —
        // monto base, recargo (REAJUSTE/INFORCONF), punitorio acumulado por mora, y si ya
        // fue cobrada. Sin esto no quedaba claro qué se perdía al anular una venta con
        // cuotas ya cobradas o con cargos aplicados.
        _dgCuotas = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = EBC("#C5CAE9"),
            ColumnHeaderStyle = ColHdrS("#283593"), FontSize = 11, RowHeight = 30 };
        Style TxRojo() { var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.ForegroundProperty, EBC("#C62828"))); return s; }
        _dgCuotas.Columns.Add(DCV("N°",         "NCuotaTxt",   1.4, TextAlignment.Center));
        _dgCuotas.Columns.Add(DCV("Vence",      "VtoFmt",      0.8));
        _dgCuotas.Columns.Add(DCV("Monto",      "MontoFmt",    0.9, TextAlignment.Right));
        _dgCuotas.Columns.Add(DCV("Recargo",    "ReajusteFmt", 0.7, TextAlignment.Right));
        _dgCuotas.Columns.Add(DCV("Punitorio",  "PunitorioFmt",0.7, TextAlignment.Right));
        _dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Total",
            Binding = new System.Windows.Data.Binding("TotalFmt"),
            Width = new DataGridLength(0.9, DataGridLengthUnitType.Star), ElementStyle = TxRojo() });
        _dgCuotas.Columns.Add(DCV("Mora",       "MoraTxt",     0.5, TextAlignment.Center));
        _dgCuotas.Columns.Add(DCV("Inforconf",  "InforcomTxt", 0.5, TextAlignment.Center));
        _dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Estado",
            Binding = new System.Windows.Data.Binding("EstadoTxt"),
            Width = new DataGridLength(0.6, DataGridLengthUnitType.Star) });
        _dgCuotas.Columns.Add(DCV("Fecha cobro","CobradaFmt",  0.8));
        var colAccionCuota = new DataGridTemplateColumn { Header = "Acción", Width = 84 };
        var cellTemplate = new DataTemplate();
        var factoryBtn = new FrameworkElementFactory(typeof(Button));
        factoryBtn.Name = "BtnEliminarCuota";
        factoryBtn.SetValue(Button.ToolTipProperty, "Eliminar esta cuota y replantear el saldo pendiente entre el resto");
        // ControlTemplate propio en vez de Content/ContentPresenter — el botón quedaba
        // renderizando solo como un rectángulo sólido sin texto visible dentro del DataGrid
        // (el ContentPresenter del template por defecto de Button no estaba mostrando el
        // TextBlock hijo agregado vía AppendChild). Con un Border+TextBlock propio como
        // Template el texto blanco queda garantizado, sin depender de cómo el estilo por
        // defecto del Button posiciona su contenido.
        var tplBtn = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "BtnBorder";
        borderFactory.SetValue(Border.BackgroundProperty, EBC("#D32F2F"));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
        var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
        txtFactory.SetValue(TextBlock.TextProperty, "Quitar");
        txtFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
        txtFactory.SetValue(TextBlock.FontSizeProperty, 10.5);
        txtFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        txtFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(txtFactory);
        tplBtn.VisualTree = borderFactory;
        var trigHover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigHover.Setters.Add(new Setter(Border.BackgroundProperty, EBC("#B71C1C")) { TargetName = "BtnBorder" });
        tplBtn.Triggers.Add(trigHover);
        factoryBtn.SetValue(Button.TemplateProperty, tplBtn);
        factoryBtn.SetValue(Button.WidthProperty, 68.0);
        factoryBtn.SetValue(Button.HeightProperty, 22.0);
        factoryBtn.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factoryBtn.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
        factoryBtn.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        factoryBtn.SetValue(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        factoryBtn.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
        factoryBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler(async (s, _) => {
            if ((s as Button)?.DataContext is FilaCuotaEliminar fc) await EliminarYReplantearCuota(fc);
        }));
        cellTemplate.VisualTree = factoryBtn;
        // La fila de Entrega no es una cuota real del plan — no tiene sentido "eliminarla y
        // replantear" como si fuera una cuota, así que el botón directamente no se genera ahí.
        var dtOcultarBtn = new DataTrigger { Binding = new System.Windows.Data.Binding("EsEntrega"), Value = true };
        dtOcultarBtn.Setters.Add(new Setter(Button.VisibilityProperty, Visibility.Collapsed) { TargetName = "BtnEliminarCuota" });
        cellTemplate.Triggers.Add(dtOcultarBtn);
        colAccionCuota.CellTemplate = cellTemplate;
        _dgCuotas.Columns.Add(colAccionCuota);
        _dgCuotas.LoadingRow += (_, e) => {
            if (e.Row.Item is FilaCuotaEliminar fc) {
                if (fc.EsEntrega)
                    e.Row.Background = EBC("#E3F2FD"); // la entrega no es una cuota real — resalte distinto (azul)
                else if (fc.Estado == 1)
                    e.Row.Background = EBC("#E8F5E9"); // resalta cuotas ya cobradas — se pierden al anular
            }
        };

        var detalleCols = new Grid();
        detalleCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        detalleCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        detalleCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.8, GridUnitType.Star) });

        var artStack = new StackPanel();
        artStack.Children.Add(new TextBlock { Text = "ARTÍCULOS", FontSize = 10.5, FontWeight = FontWeights.Bold,
            Foreground = EBC("#5C6BC0"), Margin = new Thickness(2,0,0,3) });
        artStack.Children.Add(_dgDetalle);
        Grid.SetColumn(artStack, 0); detalleCols.Children.Add(artStack);

        var cuotasStack = new StackPanel();
        cuotasStack.Children.Add(new TextBlock { Text = "CUOTAS GENERADAS — lo que se pierde al anular", FontSize = 10.5,
            FontWeight = FontWeights.Bold, Foreground = EBC("#5C6BC0"), Margin = new Thickness(2,0,0,3) });
        cuotasStack.Children.Add(_dgCuotas);
        Grid.SetColumn(cuotasStack, 2); detalleCols.Children.Add(cuotasStack);

        Grid.SetRow(detalleCols, 2); body.Children.Add(detalleCols);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        try {
            using var conn = _db.Create();
            _locales = (await conn.QueryAsync<(byte Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL")).ToList();
            // Administrador o la excepción puntual (código 67, ver Usuario.PuedeVerTodosLosLocales)
            // arranca en "Todos los locales" por default — antes siempre preseleccionaba el
            // local de la sesión actual sin importar el usuario, obligando a un admin a
            // cambiar el filtro manualmente cada vez para buscar ventas de otra sucursal.
            var puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
            if (!puedeVerTodos) {
                var idLocalSes = (byte)(_session.LocalActual?.IdLocal ?? 0);
                var localSes = _locales.FirstOrDefault(l => l.Id == idLocalSes);
                if (localSes.Id != 0) {
                    _localSelId  = localSes.Id;
                    _localSelNom = localSes.Nombre;
                    _lblLocalNombre.Text = localSes.Nombre;
                }
            }
            await Cargar();
        } catch (Exception ex) {
            MessageBox.Show($"Error al inicializar: {ex.Message}");
        }
    }

    private void AbrirSelectorLocal()
    {
        var dlg = new Window {
            Title = "Seleccionar Local", Width = 520, Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };

        var outer = new Border {
            Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 24, Opacity = 0.22, ShadowDepth = 4, Direction = 270,
                Color = System.Windows.Media.Colors.Black }, ClipToBounds = true };

        var dp = new DockPanel();
        var hdr = new Border { Padding = new Thickness(20, 16, 20, 16),
            Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(26, 35, 126),
                System.Windows.Media.Color.FromRgb(40, 53, 147), 0) };
        var hdrDp = new DockPanel();
        var hdrTitle = new StackPanel();
        hdrTitle.Children.Add(new TextBlock { Text = "📍  Seleccionar Local",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hdrTitle.Children.Add(new TextBlock { Text = "Elige el local para filtrar las ventas",
            Foreground = EBC("#C5CAE9"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        var btnX = new Button { Content = "✕", Width = 28, Height = 28,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = EBC("#C5CAE9"), BorderThickness = new Thickness(0),
            FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top };
        btnX.Click += (_, _) => dlg.Close();
        DockPanel.SetDock(btnX, Dock.Right);
        hdrDp.Children.Add(btnX); hdrDp.Children.Add(hdrTitle);
        hdr.Child = hdrDp;
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16, 14, 16, 14) };
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        Border MakeCard(byte id, string nombre, bool isTodos = false) {
            bool selected = isTodos ? _localSelId == 0 : _localSelId == id;
            string clrBg  = selected ? "#1A237E" : "#FAFAFA";
            string clrFg  = selected ? "White"   : "#212121";
            string clrSub = selected ? "#C5CAE9" : "#757575";
            string clrBdr = selected ? "#1A237E" : "#E0E0E0";
            var card = new Border {
                Width = 140, Height = 100, Margin = new Thickness(6),
                Background = EBC(clrBg), CornerRadius = new CornerRadius(10),
                BorderBrush = EBC(clrBdr), BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = selected ? new System.Windows.Media.Effects.DropShadowEffect {
                    BlurRadius = 12, Opacity = 0.3, ShadowDepth = 2, Direction = 270,
                    Color = System.Windows.Media.Color.FromRgb(26,35,126) } : null };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = isTodos ? "🏪" : "📦", FontSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,4) });
            sp.Children.Add(new TextBlock { Text = nombre, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = EBC(clrFg), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 120,
                HorizontalAlignment = HorizontalAlignment.Center });
            if (!isTodos)
                sp.Children.Add(new TextBlock { Text = $"Local #{id}", FontSize = 10,
                    Foreground = EBC(clrSub), TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = sp;
            card.MouseLeftButtonUp += async (_, _) => {
                _localSelId  = isTodos ? (byte)0 : id;
                _localSelNom = isTodos ? "Todos los locales" : nombre;
                _lblLocalNombre.Text = _localSelNom;
                dlg.Close();
                // Auto-refresco al elegir local — mismo criterio que las fechas: antes había
                // que apretar "Buscar" a mano después de cambiar el local.
                await Cargar();
            };
            card.MouseEnter += (_, _) => { if (!selected) card.Background = EBC("#E8EAF6"); };
            card.MouseLeave += (_, _) => { if (!selected) card.Background = EBC(clrBg); };
            return card;
        }

        // Pedido explícito: agregar un botón de cerrar visible en el pie — el "✕" del header
        // podía pasar desapercibido (poco contraste sobre el degradado azul), dejando sin
        // salida clara a quien abrió el selector por accidente. Se agrega ANTES que el
        // scroll: en DockPanel el último hijo sin Dock explícito es el que rellena el
        // espacio restante, así que scroll debe quedar al final de la colección.
        var pieBtn = new Border { Padding = new Thickness(16,10,16,10),
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var btnCerrarPie = new Button { Content = "✕  Cerrar", Height = 32, Padding = new Thickness(16,0,16,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            FontWeight = FontWeights.SemiBold };
        btnCerrarPie.Click += (_, _) => dlg.Close();
        pieBtn.Child = btnCerrarPie;
        DockPanel.SetDock(pieBtn, Dock.Bottom); dp.Children.Add(pieBtn);

        wrap.Children.Add(MakeCard(0, "Todos los locales", isTodos: true));
        foreach (var (id, nom) in _locales) wrap.Children.Add(MakeCard(id, nom));
        scroll.Content = wrap; dp.Children.Add(scroll);

        outer.Child = dp; dlg.Content = outer;
        hdr.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) dlg.DragMove(); };
        dlg.ShowDialog();
    }

    private async Task Cargar()
    {
        try {
            using var conn = _db.Create();
            var desde   = _dpDesde.SelectedDate ?? DateTime.Today;
            var hasta   = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
            var idLocal = _localSelId;

            _ventas = (await conn.QueryAsync<FilaVentaCredito>(
                @"SELECT CS.IDCAB, CS.NVENTACHAR AS NVenta, CS.ID_LOCAL AS IdLocal,
                         L.NOMBRE AS NomLocal, C.NOMBRE_CLIENTE AS Cliente,
                         CS.TOTAL, CS.ENTREGANORMAL AS Entrega, CS.CUOTAS,
                         CS.FECHA, CS.ID_USUARIO AS IdUsuario, U.NOMBRE_USUARIO AS Usuario
                  FROM CABECERA_SALES CS
                  INNER JOIN CLIENTES C ON C.ID_CLIENTE = CS.ID_CLIENTE
                  INNER JOIN LOCALES  L ON L.ID_LOCAL   = CS.ID_LOCAL
                  INNER JOIN USUARIOS U ON U.ID_USUARIO = CS.ID_USUARIO
                  WHERE CS.FORMA_DE_VENTA = 2
                    AND CS.ESTADO = 1
                    AND CS.FECHA >= @d AND CS.FECHA < @h
                    AND (@l = 0 OR CS.ID_LOCAL = @l)
                  ORDER BY CS.FECHA DESC, CS.IDCAB DESC",
                new { d = desde, h = hasta, l = idLocal })).ToList();

            _dgVentas.ItemsSource = _ventas;
            // Limpiar también Cuotas — antes solo se limpiaba Detalle, así que tras anular una
            // venta (o al recargar por cambio de filtro) la grilla de cuotas se quedaba mostrando
            // el plan de pagos de la venta ya eliminada, dando a entender por error que
            // correspondía a la siguiente venta de la lista.
            _dgDetalle.ItemsSource = null;
            _dgCuotas.ItemsSource  = null;
            _detalle = new();
            _cuotas  = new();
            _selVenta = null;
            _lblInfo.Text = $"{_ventas.Count} venta(s) a crédito encontrada(s). Doble clic para ver artículos.";
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CargarDetalle()
    {
        if (_dgVentas.SelectedItem is not FilaVentaCredito v) return;
        _selVenta = v;
        try {
            using var conn = _db.Create();
            _detalle = (await conn.QueryAsync<FilaDetalleVentaC>(
                @"SELECT DS.IDDET, DS.IDCAB, DS.IDART, A.D AS Nombre,
                         DS.PV, DS.CANTIDAD
                  FROM DETALLES_SALES DS
                  INNER JOIN ARTICULOS A ON A.ID = DS.IDART
                  WHERE DS.IDCAB = @id
                  ORDER BY DS.IDDET",
                new { id = v.IdCab })).ToList();
            _dgDetalle.ItemsSource = _detalle;

            _cuotas = (await conn.QueryAsync<FilaCuotaEliminar>(
                @"SELECT IDGENERADAS AS IdGeneradas, NCUOTA AS NCuota, MONTO AS Monto, ISNULL(REAJUSTE,0) AS Reajuste,
                         ISNULL(PUNITORIO,0) AS Punitorio, TOTAL AS Total, ISNULL(MORA,0) AS Mora,
                         ESTADO AS Estado, ISNULL(INFORCOM_APLICADO,0) AS InforcomAplicado,
                         VTO AS Vto, FECHACOBRADO AS FechaCobrado
                  FROM GENERADAS
                  WHERE IDCAB = @id
                  ORDER BY NCUOTA",
                new { id = v.IdCab })).ToList();
            // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial de la venta (grabada así por el
            // SP legado AGREGAR_GENERADAS_CS), no una cuota real del plan de pago — se marca aparte
            // para no confundirla con las cuotas 2..N que sí son pagos mensuales pactados.
            if (_cuotas.Count > 0) _cuotas[0].EsEntrega = true;
            _dgCuotas.ItemsSource = _cuotas;

            var cuotasReales    = _cuotas.Where(c => !c.EsEntrega).ToList();
            var cuotasCobradas  = cuotasReales.Count(c => c.Estado == 1);
            var totalCobrado    = cuotasReales.Where(c => c.Estado == 1).Sum(c => c.Monto);
            var entrega         = _cuotas.FirstOrDefault(c => c.EsEntrega);
            var avisoEntrega    = entrega is { Estado: 1 }
                ? $" — ⚠ la ENTREGA de Gs. {entrega.Monto:N0} ya fue cobrada."
                : "";
            var avisoCobrado    = cuotasCobradas > 0
                ? $" — ⚠ {cuotasCobradas} cuota(s) YA COBRADA(S) por Gs. {totalCobrado:N0} se perderán."
                : "";
            _lblInfo.Text = $"Venta {v.NVenta?.Trim()} — {v.Cliente} — {_detalle.Count} artículo(s), {cuotasReales.Count} cuota(s).{avisoEntrega}{avisoCobrado} Verifique y presione 'Anular Venta'.";
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}");
        }
    }

    // Corrige un error de carga en el plan de pagos de una venta a crédito ya generada: borra
    // UNA cuota puntual (cobrada por error, o que directamente no debería existir) y redistribuye
    // el saldo restante entre las demás cuotas PENDIENTES — sin tocar cuotas ya cobradas
    // correctamente ni el precio total pactado con el cliente. Si la cuota eliminada estaba
    // cobrada, ese dinero NO se acredita a favor del cliente (fue un cobro erróneo): se descarta
    // del saldo, y si generó un movimiento de caja se revierte (o se avisa si la caja ya cerró).
    private async Task EliminarYReplantearCuota(FilaCuotaEliminar cuota)
    {
        if (_selVenta == null) return;
        var v   = _selVenta;
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");

        var pendientesRestantes = _cuotas.Where(c => !c.EsEntrega && c.IdGeneradas != cuota.IdGeneradas && c.Estado == 0).ToList();
        if (pendientesRestantes.Count == 0) {
            MessageBox.Show(
                "No quedarían cuotas pendientes para redistribuir el saldo después de eliminar esta. " +
                "Use 'Anular venta' si lo que corresponde es anular todo el crédito.",
                "Sin cuotas pendientes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var eraCobrada = cuota.Estado == 1;
        var avisoCobro = eraCobrada
            ? $"\n\n⚠ Esta cuota YA FUE COBRADA por Gs. {cuota.Monto.ToString("N0", fmt)} — se descartará " +
              "como un cobro erróneo (NO se acredita a favor del cliente)."
            : "";
        var confirmar = MessageBox.Show(
            $"Se eliminará la Cuota {cuota.NCuota - 1} (vence {cuota.VtoFmt}, Gs. {cuota.MontoFmt}) " +
            $"del plan de pagos de la venta {v.NVenta?.Trim()}.\n\n" +
            $"Las {pendientesRestantes.Count} cuota(s) pendiente(s) restante(s) se recalcularán para " +
            "seguir sumando el saldo correcto que falta cobrar." + avisoCobro +
            "\n\n¿Confirma esta corrección del plan de pagos?",
            "Eliminar y replantear cuota", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmar != MessageBoxResult.Yes) return;

        if (!await ValidarAdminEVC2()) return;

        try {
            using var conn = _db.Create();
            int idUsuario = _session.UsuarioActual?.IdUsuario ?? 1;

            // Si la cuota eliminada estaba cobrada, puede tener un movimiento asociado en
            // CAJA_DETALLE. No hay FK directo a IDGENERADAS, y el cobro de cuotas normales
            // (SP legado sp_Guardar_Cobranza_Cs_2026) deja CAJA_DETALLE.ID_VENTA en NULL
            // (confirmado con datos reales) — buscar solo por ID_VENTA+monto nunca encontraba
            // estos movimientos, dejándolos huérfanos en caja al eliminar la cuota. Se
            // empareja por COMPROBANTE + N° de cuota extraídos del propio texto de CONCEPTO
            // (mismo patrón que CorregirLocalCobrosAsync en AtrasosWindow.xaml.cs), validado
            // con match exacto para evitar falsos positivos de sufijo de comprobante.
            var avisoCajaCerrada = "";
            if (eraCobrada) {
                var sufijoComp = (v.NVenta ?? "").Trim().TrimStart('0');
                var movsCajaCandidatos = (await conn.QueryAsync<(int IdDetalle, int IdMaster, string LocalNombre, decimal Monto, string EstadoCaja, string Concepto)>(
                    @"SELECT D.ID_DETALLE, D.ID_MASTER, ISNULL(L.NOMBRE,'') AS LocalNombre, D.MONTO, M.ESTADO AS EstadoCaja, D.CONCEPTO
                      FROM CAJA_DETALLE D
                      INNER JOIN CAJA_MASTER M ON M.ID_MASTER = D.ID_MASTER
                      LEFT JOIN LOCALES L ON L.ID_LOCAL = M.ID_LOCAL
                      WHERE D.ESTADO_REG = 'V' AND (
                        (D.ID_VENTA = @idcab AND D.MONTO = @monto)
                        OR (D.SUBTIPO IN ('COBRO','COBRO_SISTEMA') AND D.CONCEPTO LIKE @comp)
                      )",
                    new { idcab = v.IdCab, monto = cuota.Total, comp = $"%COMPROBANTE:%{sufijoComp}%" })).ToList();
                var movsCaja = movsCajaCandidatos.Where(m => {
                    var match = System.Text.RegularExpressions.Regex.Match(m.Concepto ?? "",
                        @"CUOTA N\S*?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*0*(\d+)");
                    if (!match.Success) return true; // vino por ID_VENTA+monto, ya validado
                    return match.Groups[2].Value == sufijoComp && byte.Parse(match.Groups[1].Value) == cuota.NCuota;
                }).ToList();

                var cerrados = movsCaja.Where(m => m.EstadoCaja == "C").ToList();
                var abiertos = movsCaja.Where(m => m.EstadoCaja != "C").ToList();

                if (cerrados.Count > 0) {
                    var seguir = await MostrarAvisoCajaCerrada(
                        cerrados.Select(c => (c.IdDetalle, c.IdMaster, c.LocalNombre, c.Monto, c.EstadoCaja)).ToList(),
                        $"El cobro de la Cuota {cuota.NCuota - 1} de la venta {v.NVenta?.Trim()} ya quedó dentro de un arqueo cerrado.");
                    if (!seguir) return;
                    avisoCajaCerrada = $"\n\nATENCIÓN: {cerrados.Count} movimiento(s) de caja ya cerrada NO se tocaron — siga los pasos indicados para el ajuste manual.";
                }
                if (abiertos.Count > 0) {
                    var idsDetalle = abiertos.Select(m => m.IdDetalle).ToList();
                    await conn.ExecuteAsync("DELETE FROM CAJA_DETALLE WHERE ID_DETALLE IN @ids", new { ids = idsDetalle });
                }
            }

            // Saldo real que sigue financiado: suma de las cuotas pendientes restantes (la
            // eliminada no aporta nada al saldo, cobrada o no — si estaba pendiente simplemente
            // deja de existir, si estaba cobrada por error ese dinero no se acredita).
            var saldoPendiente = pendientesRestantes.Sum(c => c.Monto);
            var nuevaCantidad  = pendientesRestantes.Count;
            var montoBase      = Math.Floor(saldoPendiente / nuevaCantidad);
            var resto          = saldoPendiente - (montoBase * nuevaCantidad);

            await conn.ExecuteAsync("DELETE FROM GENERADAS WHERE IDGENERADAS = @id", new { id = cuota.IdGeneradas });

            // Redistribuye el saldo entre las cuotas pendientes restantes en orden de
            // vencimiento — la última cuota absorbe el resto del redondeo (mismo criterio que
            // Math.Ceiling ya usado en el cálculo original del plan de pagos, para no perder
            // ni un guaraní del saldo pactado).
            for (int i = 0; i < pendientesRestantes.Count; i++) {
                var montoNuevo = montoBase + (i == pendientesRestantes.Count - 1 ? resto : 0);
                await conn.ExecuteAsync(
                    "UPDATE GENERADAS SET MONTO=@m, TOTAL=@m WHERE IDGENERADAS=@id",
                    new { m = montoNuevo, id = pendientesRestantes[i].IdGeneradas });
            }

            MessageBox.Show(
                $"Cuota eliminada. Las {nuevaCantidad} cuota(s) pendiente(s) restante(s) fueron recalculadas " +
                $"a Gs. {montoBase.ToString("N0", fmt)} c/u (saldo total: Gs. {saldoPendiente.ToString("N0", fmt)})." +
                avisoCajaCerrada,
                "Plan de pagos corregido", MessageBoxButton.OK, MessageBoxImage.Information);

            await CargarDetalle();
        } catch (Exception ex) {
            MessageBox.Show($"Error al eliminar la cuota: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Modal detallado que reemplaza al MessageBox genérico de "caja cerrada" — antes solo decía
    // "ajuste el efectivo manualmente si corresponde" sin explicar CÓMO, y no existe ningún
    // módulo en el sistema para reabrir/editar una caja ya cerrada (solo Herramientas → Historial
    // Caja, que es de solo consulta). Se detallan los pasos concretos que sí puede hacer un
    // encargado hoy: registrar el ajuste en la PRÓXIMA apertura/cierre de esa misma caja física,
    // usando el monto y N° de caja que quedan documentados acá.
    private async Task<bool> MostrarAvisoCajaCerrada(
        List<(int IdDetalle, int IdMaster, string LocalNombre, decimal Monto, string EstadoCaja)> movs,
        string tituloContexto)
    {
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window {
            Title = "Caja ya cerrada — acción manual requerida",
            Width = 600, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13 };
        var dp = new DockPanel();
        var dhdr = new Border { Background = EBC("#B71C1C"), Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "⚠  Caja ya cerrada (arqueada)",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock { Text = tituloContexto,
            Foreground = EBC("#FFCDD2"), FontSize = 11, Margin = new Thickness(0,3,0,0), TextWrapping = TextWrapping.Wrap });
        dhdr.Child = hdrSp;
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);

        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14,10,14,10) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var bSi = new Button { Content = "Entendido, continuar", Width = 160, Height = 34, Margin = new Thickness(0,0,8,0),
            Background = EBC("#B71C1C"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bNo = new Button { Content = "Cancelar", Width = 90, Height = 34,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        bSi.Click += (_, _) => { tcs.SetResult(true);  dlg.Close(); };
        bNo.Click += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dps.Children.Add(bSi); dps.Children.Add(bNo); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(18,14,18,14) };
        var body = new StackPanel();

        body.Children.Add(new TextBlock {
            Text = "El movimiento de esta venta ya quedó dentro de un arqueo de caja YA CERRADO — " +
                   "no se puede borrar sin descuadrar ese cierre ya realizado. El sistema NO tocará " +
                   "ese registro; el resto de la operación (venta, cuotas, stock) sí se procesa normalmente.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0,0,0,12) });

        var cajasBox = new Border { Background = EBC("#FFF3E0"), BorderBrush = EBC("#FFB74D"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,0,14) };
        var cajasSp = new StackPanel();
        cajasSp.Children.Add(new TextBlock { Text = "CAJA(S) AFECTADA(S):",
            FontWeight = FontWeights.Bold, FontSize = 11, Foreground = EBC("#E65100"), Margin = new Thickness(0,0,0,6) });
        foreach (var m in movs)
            cajasSp.Children.Add(new TextBlock {
                Text = $"•  Caja N° {m.IdMaster} — {m.LocalNombre} — Gs. {m.Monto.ToString("N0", fmt)}",
                FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,1,0,1) });
        cajasBox.Child = cajasSp;
        body.Children.Add(cajasBox);

        body.Children.Add(new TextBlock { Text = "QUÉ HACER MANUALMENTE:",
            FontWeight = FontWeights.Bold, FontSize = 12, Foreground = EBC("#1A237E"), Margin = new Thickness(0,0,0,6) });

        void Paso(string n, string t) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
            sp.Children.Add(new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = EBC("#1A237E"), Margin = new Thickness(0,1,8,0),
                Child = new TextBlock { Text = n, Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center } });
            sp.Children.Add(new TextBlock { Text = t, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Width = 480, VerticalAlignment = VerticalAlignment.Center });
            body.Children.Add(sp);
        }
        Paso("1", "Anote el N° de caja y el monto exacto que se muestra arriba — lo va a necesitar para el ajuste.");
        Paso("2", "Avise al encargado o administrador del local dueño de esa caja (no hay ningún módulo en el sistema " +
                   "todavía para reabrir o editar una caja ya cerrada).");
        Paso("3", "En la PRÓXIMA apertura de esa misma caja física, cuente el efectivo real y regístrelo con una " +
                   "diferencia (ingreso o egreso manual) por el monto anotado, dejando una nota que explique el motivo " +
                   "(ej. \"Ajuste por anulación de venta/cuota #" + (_selVenta?.IdCab.ToString() ?? "") + "\").");
        Paso("4", "Ese ajuste va a quedar reflejado en Herramientas → Historial Caja para quien necesite auditar " +
                   "el movimiento más adelante.");

        scroll.Content = body;
        DockPanel.SetDock(scroll, Dock.Top); dp.Children.Add(scroll);
        dlg.Content = dp;
        dlg.ShowDialog();
        return await tcs.Task;
    }

    private async Task Anular()
    {
        if (_selVenta == null) {
            MessageBox.Show("Haga doble clic sobre una venta para seleccionarla primero.",
                "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (_detalle.Count == 0) {
            MessageBox.Show("Cargue los artículos de la venta haciendo doble clic primero.",
                "Sin detalle", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var v   = _selVenta;
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");

        // Diálogo de confirmación
        var dlg = new Window {
            Title = "Confirmar Anulación de Venta a Crédito",
            Width = 540, Height = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13 };
        var tcs = new TaskCompletionSource<bool>();
        var dp  = new DockPanel();
        var dhdr = new Border { Background = EBC("#1A237E"), Padding = new Thickness(14,10,14,10) };
        dhdr.Child = new TextBlock { Text = "⚠  Confirmar Anulación de Venta a Crédito",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var bSi = new Button { Content = "Sí, anular", Width = 100, Height = 32, Margin = new Thickness(0,0,8,0),
            Background = EBC("#1A237E"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bNo = new Button { Content = "Cancelar", Width = 90, Height = 32,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        bSi.Click  += (_, _) => { tcs.SetResult(true);  dlg.Close(); };
        bNo.Click  += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dps.Children.Add(bSi); dps.Children.Add(bNo); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);
        var dbody = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        dbody.Children.Add(new TextBlock { Text = "¡ADVERTENCIA!",
            FontWeight = FontWeights.Bold, Foreground = EBC("#1A237E"), FontSize = 13,
            Margin = new Thickness(0,0,0,8) });
        dbody.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Text = $"Esta acción anulará definitivamente la venta a crédito:\n\n" +
                   $"  • N° de Venta : {v.NVenta?.Trim()}\n" +
                   $"  • Cliente     : {v.Cliente}\n" +
                   $"  • Local       : {v.NomLocal}\n" +
                   $"  • Total       : {v.Total.ToString("N0", fmt)} Gs.\n" +
                   $"  • Entrega     : {v.Entrega.ToString("N0", fmt)} Gs.\n" +
                   $"  • Cuotas      : {v.Cuotas}\n\n" +
                   "Se eliminarán: cabecera, detalle, cuotas generadas y movimiento de caja.\n" +
                   "El stock será restaurado automáticamente." });
        dbody.Children.Add(new TextBlock { Text = "\n¿Está completamente seguro de continuar?",
            FontWeight = FontWeights.SemiBold, Foreground = EBC("#1A237E") });
        dp.Children.Add(dbody); dlg.Content = dp;
        dlg.ShowDialog();
        if (!await tcs.Task) return;

        if (!await ValidarAdminEVC2()) return;

        try {
            using var conn = _db.Create();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using var tx = conn.BeginTransaction();
            int idUsuario = _session.UsuarioActual?.IdUsuario ?? 1;
            var nomMaquina = System.Net.Dns.GetHostName();
            const string ipMaquina = "127.0.0.1";

            // Reemplaza a sp_Eliminar_Venta_Credito_Cs_2026 (SP legado) — replicado en C# en
            // vez de llamarlo, porque el SP borra CAJA_DETALLE sin validar en absoluto si la
            // caja donde se registró ese movimiento ya está cerrada (arqueada): si se anula
            // una venta con entrega/cuotas ya cobradas y esa caja se cerró, el movimiento
            // desaparecía silenciosamente dejando el arqueo con un total que ya no coincide
            // con la realidad. Acá, si la caja está cerrada, el movimiento de esa caja NO se
            // borra (se avisa al usuario para que ajuste el efectivo físico manualmente si
            // corresponde) — si sigue abierta, se borra igual que el SP.
            // El cobro de la ENTREGA (InsertarMovimientoCajaAsync) sí graba CAJA_DETALLE.ID_VENTA,
            // pero el cobro de CUOTAS normales lo deja en NULL — confirmado con datos reales de
            // producción/local. Buscar solo por ID_VENTA dejaba huérfanos en caja los movimientos
            // de cuotas ya cobradas al anular la venta. Se agrega la misma búsqueda por
            // COMPROBANTE que ya usa CorregirLocalCobrosAsync en AtrasosWindow.xaml.cs para
            // relacionar CAJA_DETALLE con GENERADAS de este crédito.
            //
            // El comprobante real grabado en CAJA_DETALLE.CONCEPTO ("COMPROBANTE: 000000033484")
            // usa el IDCAB de la venta, NO el NVENTACHAR ("N° de Venta" comercial que se ve en
            // pantalla, ej. "000030951") — son dos numeraciones completamente distintas
            // (confirmado contra GENERADAS.COMPROBANTE = '000000' + IDCAB). El código anterior
            // armaba el sufijo de búsqueda a partir de v.NVenta, así que en la práctica NUNCA
            // podía encontrar el movimiento de caja real de una cuota cobrada — bug real
            // detectado al auditar el flujo con datos de producción/local. Corregido para armar
            // el sufijo a partir de v.IdCab, que es lo que realmente queda grabado en CONCEPTO.
            var sufijoComp = v.IdCab.ToString();
            var movsCajaCandidatos = (await conn.QueryAsync<(int IdDetalle, int IdMaster, string LocalNombre, decimal Monto, string EstadoCaja, string Concepto)>(
                @"SELECT D.ID_DETALLE, D.ID_MASTER, ISNULL(L.NOMBRE,'') AS LocalNombre, D.MONTO, M.ESTADO AS EstadoCaja, D.CONCEPTO
                  FROM CAJA_DETALLE D
                  INNER JOIN CAJA_MASTER M ON M.ID_MASTER = D.ID_MASTER
                  LEFT JOIN LOCALES L ON L.ID_LOCAL = M.ID_LOCAL
                  WHERE D.ESTADO_REG = 'V' AND (
                    D.ID_VENTA = @idcab
                    OR (D.SUBTIPO IN ('COBRO','COBRO_SISTEMA') AND D.CONCEPTO LIKE @comp)
                  )",
                new { idcab = v.IdCab, comp = $"%COMPROBANTE:%{sufijoComp}%" }, tx)).ToList();
            // Los que vinieron por ID_VENTA quedan sin más validación; los que vinieron por
            // CONCEPTO (LIKE, solo filtro grueso) se confirman con match exacto de comprobante
            // — evita falsos positivos tipo "19112" matcheando dentro de "119112".
            var movsCaja = movsCajaCandidatos.Where(m => {
                var match = System.Text.RegularExpressions.Regex.Match(m.Concepto ?? "", @"COMPROBANTE:\s*0*(\d+)");
                return !match.Success || match.Groups[1].Value == sufijoComp;
            }).ToList();
            var movsCajaCerrada  = movsCaja.Where(m => m.EstadoCaja == "C").ToList();
            var movsCajaAbierta  = movsCaja.Where(m => m.EstadoCaja != "C").ToList();

            if (movsCajaCerrada.Count > 0)
            {
                var seguir = await MostrarAvisoCajaCerrada(
                    movsCajaCerrada.Select(c => (c.IdDetalle, c.IdMaster, c.LocalNombre, c.Monto, c.EstadoCaja)).ToList(),
                    $"La entrega y/o cobro de cuotas de la venta {v.NVenta?.Trim()} ya quedaron dentro de un arqueo cerrado.");
                if (!seguir) { tx.Rollback(); return; }
            }

            // 1) Devolver stock y registrar MOVART por cada artículo (mismo patrón que el SP:
            // MOV=1, MOD=5, sin afectar PRICES.S dos veces si hay más de un detalle del mismo
            // artículo, ya que cada línea de DETALLES_SALES es una fila independiente).
            foreach (var det in _detalle) {
                var precioActual = await conn.QueryFirstOrDefaultAsync<(decimal S, decimal Pc)>(
                    "SELECT ISNULL(S,0) AS S, ISNULL(PC,0) AS Pc FROM PRICES WHERE IDART=@idart AND IDLOCAL=@idlocal",
                    new { idart = det.IdArt, idlocal = v.IdLocal }, tx);

                var filasAfectadas = await conn.ExecuteAsync(
                    "UPDATE PRICES SET S = S + @cant, FMS = GETDATE(), IDUMODSTOCK = @idu WHERE IDART=@idart AND IDLOCAL=@idlocal",
                    new { cant = det.Cantidad, idu = idUsuario, idart = det.IdArt, idlocal = v.IdLocal }, tx);
                if (filasAfectadas == 0)
                    throw new Exception($"No se encontró PRICES para el artículo ID {det.IdArt} en el local {v.IdLocal}.");

                var nuevoIdMovart = await conn.ExecuteScalarAsync<int>("SELECT ISNULL(MAX(IDMOVART),0)+1 FROM MOVART", transaction: tx);
                await conn.ExecuteAsync(
                    @"INSERT INTO MOVART(IDMOVART,IDART,MOV,MOD,STINI,CANT,IDLOCAL,IDDESTINO,PCANT,PCACT,IDU,FECHA)
                      VALUES(@idmovart,@idart,1,5,@stini,@cant,@idlocal,@idlocal,@pcant,@pcant,@idu,GETDATE())",
                    new { idmovart = nuevoIdMovart, idart = det.IdArt, stini = precioActual.S,
                          cant = det.Cantidad, idlocal = v.IdLocal, pcant = precioActual.Pc, idu = idUsuario }, tx);
            }
            await InsertarAuditoriaEVC(conn, tx, idUsuario, "PRICES/MOVART", v.IdCab.ToString(), 'U',
                "(sin registro previo)",
                $"Stock restaurado para {_detalle.Count} artículo(s) de la venta {v.NVenta?.Trim()}",
                "ANULAR VENTA A CREDITO", nomMaquina, ipMaquina);

            // 2) Borrar los movimientos de caja SOLO de las cajas que siguen abiertas.
            if (movsCajaAbierta.Count > 0)
            {
                var idsDetalleABorrar = movsCajaAbierta.Select(m => m.IdDetalle).ToList();
                await conn.ExecuteAsync("DELETE FROM CAJA_DETALLE WHERE ID_DETALLE IN @ids", new { ids = idsDetalleABorrar }, tx);
                foreach (var m in movsCajaAbierta)
                    await InsertarAuditoriaEVC(conn, tx, idUsuario, "CAJA_DETALLE", m.IdDetalle.ToString(), 'D',
                        $"Concepto: {m.Concepto} | Monto: {m.Monto} | Local: {m.LocalNombre}", "(ELIMINADO por anulación de venta)",
                        "ANULAR VENTA A CREDITO", nomMaquina, ipMaquina);
            }

            // 3) Borrar detalle, cuotas y cabecera de la venta — igual que el SP.
            await conn.ExecuteAsync("DELETE FROM DETALLES_SALES WHERE IDCAB = @id", new { id = v.IdCab }, tx);
            await conn.ExecuteAsync("DELETE FROM GENERADAS WHERE IDCAB = @id", new { id = v.IdCab }, tx);
            await conn.ExecuteAsync("DELETE FROM CABECERA_SALES WHERE IDCAB = @id", new { id = v.IdCab }, tx);

            // Limpiar DOCUMENTOS huérfano si existe
            await conn.ExecuteAsync("DELETE FROM DOCUMENTOS WHERE IDCAB = @id", new { id = v.IdCab }, tx);

            await InsertarAuditoriaEVC(conn, tx, idUsuario, "CABECERA_SALES", v.IdCab.ToString(), 'D',
                $"Venta {v.NVenta?.Trim()} — Cliente: {v.Cliente} — Total: {v.Total} — {_cuotas.Count} cuota(s)",
                "(ELIMINADO: cabecera, detalle, cuotas y documentos de la venta anulada)",
                "ANULAR VENTA A CREDITO", nomMaquina, ipMaquina);

            tx.Commit();

            var mensajeFinal = $"Venta {v.NVenta?.Trim()} anulada correctamente.\nStock restaurado.";
            if (movsCajaCerrada.Count > 0)
                mensajeFinal += $"\n\nATENCIÓN: {movsCajaCerrada.Count} movimiento(s) de caja ya cerrada NO se tocaron — ajuste el efectivo manualmente si corresponde.";
            MessageBox.Show(mensajeFinal, "Anulación exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            _selVenta = null;
            await Cargar();
        } catch (Exception ex) {
            MessageBox.Show($"Error al anular: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> ValidarAdminEVC2() => await AdminValidacionHelper.PedirContrasenaAdmin(this, _db);

    // Anular() no dejaba NINGÚN rastro en AUDITORIA pese a borrar cabecera, cuotas, stock y
    // movimientos de caja — a diferencia de casi todo el resto del sistema (cobros, compras,
    // etc.), que sí audita cada operación. Detectado al intentar verificar en detalle una
    // anulación real: no había forma de confirmar por qué mecanismo se había borrado un
    // movimiento de caja puntual. Mismo patrón que InsertarAuditoriaAsync en CuotaRepository.
    private static Task InsertarAuditoriaEVC(IDbConnection conn, IDbTransaction tx, int idUsuario, string tabla, string idRegistro,
        char operacion, string valorAntes, string valorDespues, string modulo, string nomMaquina, string ipMaquina) =>
        conn.ExecuteAsync(
            "INSERT INTO AUDITORIA (FECHA_HORA,ID_USUARIO,TABLA,ID_REGISTRO,OPERACION,CAMPO,VALOR_ANTES,VALOR_DESPUES,MODULO,NOM_MAQUINA,IP_MAQUINA) " +
            "VALUES (GETDATE(),@IdUsuario,@Tabla,@IdRegistro,@Operacion,@Campo,@ValorAntes,@ValorDespues,@Modulo,@NomMaquina,@IpMaquina)",
            new { IdUsuario = idUsuario, Tabla = tabla, IdRegistro = idRegistro, Operacion = operacion.ToString(), Campo = "TODOS",
                  ValorAntes = valorAntes, ValorDespues = valorDespues, Modulo = modulo, NomMaquina = nomMaquina, IpMaquina = ipMaquina }, tx);
}

// Diálogo compartido de "contraseña de administrador" — usado por cualquier acción sensible
// de Herramientas (anular venta a crédito, eliminar/replantear cuota, editar cuota pagada) que
// necesite una segunda confirmación antes de tocar datos ya cobrados o movimientos de caja.
internal static class AdminValidacionHelper
{
    private static System.Windows.Media.SolidColorBrush C(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public static async Task<bool> PedirContrasenaAdmin(Window owner, IDbConnectionFactory db)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window {
            Title = "Contraseña de Administrador", Width = 360, Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = C("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13 };
        var dp = new DockPanel();
        var dhdr = new Border { Background = C("#37474F"), Padding = new Thickness(14,10,14,10) };
        dhdr.Child = new TextBlock { Text = "CONTRASEÑA DE ADMINISTRADOR",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);
        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = C("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var dps = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var bAcept = new Button { Content = "✔  Aceptar", Width = 100, Height = 32, Margin = new Thickness(0,0,10,0),
            Background = C("#2E7D32"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var bCanc = new Button { Content = "✖  Cerrar", Width = 90, Height = 32,
            Background = C("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        dps.Children.Add(bAcept); dps.Children.Add(bCanc); dpie.Child = dps;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);
        var body = new StackPanel { Margin = new Thickness(20, 14, 20, 10) };
        var lblErr = new TextBlock { Foreground = C("#C62828"), FontSize = 11,
            Margin = new Thickness(0,0,0,4), Visibility = Visibility.Collapsed };
        var pb = new PasswordBox { Padding = new Thickness(8,5,8,5),
            BorderBrush = C("#BDBDBD"), BorderThickness = new Thickness(1) };
        body.Children.Add(lblErr); body.Children.Add(pb);
        dp.Children.Add(body); dlg.Content = dp;

        async void Validar() {
            var pwd = pb.Password;
            if (string.IsNullOrEmpty(pwd)) { lblErr.Text = "Ingrese la contraseña."; lblErr.Visibility = Visibility.Visible; return; }
            try {
                using var conn = db.Create();
                var ok = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM USUARIOS
                      WHERE CONTRASEÑA_USUARIO=@p AND UPPER(CARGO_USUARIO)='ADMINISTRADOR'",
                    new { p = pwd });
                if (ok > 0) { tcs.SetResult(true); dlg.Close(); }
                else { lblErr.Text = "Contraseña incorrecta."; lblErr.Visibility = Visibility.Visible; pb.Clear(); pb.Focus(); }
            } catch (Exception ex) { lblErr.Text = $"Error: {ex.Message}"; lblErr.Visibility = Visibility.Visible; }
        }
        bAcept.Click += (_, _) => Validar();
        pb.KeyDown   += (_, e) => { if (e.Key == Key.Enter) Validar(); };
        bCanc.Click  += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed   += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        dlg.ShowDialog();
        return await tcs.Task;
    }
}

internal class FilaVentaCredito
{
    public int      IdCab    { get; set; }
    public string   NVenta   { get; set; } = "";
    public byte     IdLocal  { get; set; }
    public string   NomLocal { get; set; } = "";
    public string   Cliente  { get; set; } = "";
    public decimal  Total    { get; set; }
    public decimal  Entrega  { get; set; }
    public int      Cuotas   { get; set; }
    public DateTime Fecha    { get; set; }
    public string   Usuario  { get; set; } = "";
    public int      IdUsuario{ get; set; }
    public string   FechaStr  => Fecha.ToString("dd/MM/yyyy HH:mm");
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string   TotalFmt   => Total.ToString("N0", _py);
    public string   EntregaFmt => Entrega.ToString("N0", _py);
}

// Detalle de una cuota (GENERADAS) para mostrar antes de anular una venta a crédito —
// qué se pierde exactamente: monto base, recargo/INFORCONF, punitorio acumulado, si ya
// fue cobrada y cuándo.
internal class FilaCuotaEliminar
{
    public int      IdGeneradas      { get; set; }
    public int      NCuota           { get; set; }
    public decimal  Monto            { get; set; }
    public decimal  Reajuste         { get; set; }
    public decimal  Punitorio        { get; set; }
    public decimal  Total            { get; set; }
    public int      Mora             { get; set; }
    public byte     Estado           { get; set; }
    public bool     InforcomAplicado { get; set; }
    public DateTime Vto              { get; set; }
    public DateTime? FechaCobrado    { get; set; }
    // El SP legado (AGREGAR_GENERADAS_CS) graba la ENTREGA inicial de la venta como si fuera
    // "NCUOTA=1" dentro de GENERADAS — mismo monto que CABECERA_SALES.ENTREGANORMAL, pero no
    // es una cuota real del plan de pago (el cliente pactó "Cuotas" reales a partir de la
    // fila 2). Sin esta distinción, la grilla mostraba "Cuota N° 1 — COBRADA" confundiendo
    // la entrega ya recibida en el momento de la venta con un pago de cuota normal.
    public bool     EsEntrega        { get; set; }
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string NCuotaTxt   => EsEntrega ? $"Cuota {NCuota} (Corresponde a Entrega)" : NCuota.ToString();
    public string EstadoTxt   => Estado == 1 ? "COBRADA" : "Pendiente";
    public string MontoFmt    => Monto.ToString("N0", _py);
    public string ReajusteFmt => Reajuste == 0 ? "" : Reajuste.ToString("N0", _py);
    public string PunitorioFmt=> Punitorio == 0 ? "" : Punitorio.ToString("N0", _py);
    public string TotalFmt    => Total.ToString("N0", _py);
    public string VtoFmt      => Vto.ToString("dd/MM/yyyy");
    public string CobradaFmt  => FechaCobrado.HasValue ? FechaCobrado.Value.ToString("dd/MM/yyyy HH:mm") : "";
    public string InforcomTxt => InforcomAplicado ? "Sí" : "";
    public string MoraTxt     => Mora > 0 ? $"{Mora} días" : "";
}

// ══════════════════════════════════════════════════════════════════════════════
//  FINALIZAR PROMOCIÓN
// ══════════════════════════════════════════════════════════════════════════════
public class FinalizarPromoWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // estado
    private List<FilaPromo>         _articulos  = new();
    private List<(byte Id, string Nombre)> _locales = new();
    private readonly HashSet<int>   _selArticulos = new();  // IDs marcados
    private bool                    _todosModo = true;      // "todos los artículos"

    // controles
    private DataGrid  _dgPromo   = null!;
    private TextBlock _lblConteo = null!;
    private TextBox   _txtBuscar = null!;
    private Border    _btnTodos  = null!, _btnSeleccion = null!;
    private StackPanel _panelLocales = null!;
    private readonly Dictionary<byte, CheckBox> _cbLocales = new();

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public FinalizarPromoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Finalizar Promoción";
        Width = 900; Height = 640; MinWidth = 780; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EBC("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        _ = CargarAsync();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Padding = new Thickness(20, 13, 20, 13) };
        var hGrad = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(106, 27, 154),
            System.Windows.Media.Color.FromRgb(142, 36, 170), 0);
        hdr.Background = hGrad;
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "🏷  FINALIZAR PROMOCIÓN",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock {
            Text = "Seleccione los artículos en promoción y los locales donde desea finalizar",
            Foreground = EBC("#E1BEE7"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14, 8, 14, 8) };
        var pieDp = new DockPanel();
        _lblConteo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#616161"), FontSize = 11 };
        DockPanel.SetDock(_lblConteo, Dock.Left); pieDp.Children.Add(_lblConteo);
        var pieBtns = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        Button PBtn(string t, string bg) => new Button { Content = t, Height = 36,
            Padding = new Thickness(20,0,20,0), Margin = new Thickness(8,0,0,0),
            Background = EBC(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13 };
        var btnFinalizar = PBtn("✔  Finalizar promoción", "#6A1B9A");
        var btnCerrar    = PBtn("✖  Cerrar", "#546E7A");
        btnFinalizar.Click += async (_, _) => await Finalizar();
        btnCerrar.Click    += (_, _) => Close();
        pieBtns.Children.Add(btnFinalizar); pieBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(pieBtns, Dock.Right); pieDp.Children.Add(pieBtns);
        pie.Child = pieDp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo: columna izquierda (artículos) + columna derecha (locales) ──
        var body = new Grid { Margin = new Thickness(10, 8, 10, 8) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        root.Children.Add(body);

        // ── Panel izquierdo: artículos ─────────────────────────────────────
        var leftPanel = new DockPanel();
        Grid.SetColumn(leftPanel, 0); body.Children.Add(leftPanel);

        // toolbar de modo
        var modeBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E1BEE7"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0,0,0,6) };
        var modeSp = new StackPanel { Orientation = Orientation.Horizontal };

        Border ModeBtn(string icon, string label, bool active, out Border btn) {
            var b = new Border {
                Background = active ? EBC("#6A1B9A") : EBC("#F3E5F5"),
                CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0,0,6,0), Cursor = System.Windows.Input.Cursors.Hand };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = icon, Margin = new Thickness(0,0,5,0),
                VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold,
                Foreground = active ? System.Windows.Media.Brushes.White : EBC("#6A1B9A"),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
            b.Child = sp; btn = b; return b;
        }
        ModeBtn("🌐", "Todos los artículos", true,  out _btnTodos);
        ModeBtn("☑", "Selección manual",     false, out _btnSeleccion);
        _btnTodos.MouseLeftButtonUp    += (_, _) => SetModo(true);
        _btnSeleccion.MouseLeftButtonUp += (_, _) => SetModo(false);
        modeSp.Children.Add(_btnTodos); modeSp.Children.Add(_btnSeleccion);

        // búsqueda
        modeSp.Children.Add(new Border { Width = 12 });
        _txtBuscar = new TextBox { Width = 200, Padding = new Thickness(8,5,8,5),
            BorderBrush = EBC("#CE93D8"), BorderThickness = new Thickness(1),
            FontSize = 12 };
        var searchHint = new TextBlock { Text = "🔍 Buscar artículo...", Foreground = EBC("#BDBDBD"),
            IsHitTestVisible = false, Margin = new Thickness(10,6,0,0) };
        var searchBox = new Grid();
        searchBox.Children.Add(_txtBuscar);
        searchBox.Children.Add(searchHint);
        _txtBuscar.TextChanged += (_, _) => {
            searchHint.Visibility = string.IsNullOrEmpty(_txtBuscar.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            FiltrarGrid();
        };
        modeSp.Children.Add(searchBox);
        modeBar.Child = modeSp;
        DockPanel.SetDock(modeBar, Dock.Top); leftPanel.Children.Add(modeBar);

        // DataGrid artículos en promo
        _dgPromo = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#F3E5F5"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EBC("#FCF7FF"),
            BorderThickness = new Thickness(1), BorderBrush = EBC("#CE93D8"),
            RowHeight = 36, FontSize = 12 };

        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty,  EBC("#6A1B9A")));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty,  System.Windows.Media.Brushes.White));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty,  FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty,     new Thickness(8,6,8,6)));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty,    11.0));
        _dgPromo.ColumnHeaderStyle = hdrStyle;

        // columna de checkbox de selección
        var chkCol = new DataGridTemplateColumn { Header = "✔", Width = new DataGridLength(36) };
        var chkFact = new FrameworkElementFactory(typeof(CheckBox));
        chkFact.SetBinding(CheckBox.IsCheckedProperty,
            new System.Windows.Data.Binding("Seleccionado") { Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        chkFact.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        chkFact.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
        chkCol.CellTemplate = new DataTemplate { VisualTree = chkFact };
        _dgPromo.Columns.Add(chkCol);

        DataGridTextColumn DC(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)) {
                Setters = { new Setter(TextBlock.TextAlignmentProperty, a) } };
            return c;
        }
        _dgPromo.Columns.Add(DC("Código",        "Codigo",      0.7));
        _dgPromo.Columns.Add(DC("Descripción",   "Descripcion", 2.5));
        _dgPromo.Columns.Add(DC("P. Normal",     "PventaFmt",   0.8, TextAlignment.Right));
        _dgPromo.Columns.Add(DC("P. Promo",      "PpromoFmt",   0.8, TextAlignment.Right));
        _dgPromo.Columns.Add(DC("Locales",        "LocalesStr",  0.5, TextAlignment.Center));
        leftPanel.Children.Add(_dgPromo);

        // ── Panel derecho: locales ─────────────────────────────────────────
        var rightBorder = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#CE93D8"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8) };
        Grid.SetColumn(rightBorder, 2); body.Children.Add(rightBorder);

        var rightDp = new DockPanel();

        var rightHdr = new Border { Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8, 8, 0, 0) };
        var rGrad = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(106, 27, 154),
            System.Windows.Media.Color.FromRgb(142, 36, 170), 90);
        rightHdr.Background = rGrad;
        var rHdrSp = new StackPanel();
        rHdrSp.Children.Add(new TextBlock { Text = "📍 Locales destino",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 });
        rHdrSp.Children.Add(new TextBlock { Text = "¿En qué locales finalizar?",
            Foreground = EBC("#E1BEE7"), FontSize = 10, Margin = new Thickness(0,2,0,0) });
        rightHdr.Child = rHdrSp;
        DockPanel.SetDock(rightHdr, Dock.Top); rightDp.Children.Add(rightHdr);

        // botón "seleccionar todos los locales"
        var todosLocalesBtn = new Border { Background = EBC("#F3E5F5"),
            Margin = new Thickness(10, 8, 10, 4), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 7, 10, 7), Cursor = System.Windows.Input.Cursors.Hand };
        var tlSp = new StackPanel { Orientation = Orientation.Horizontal };
        var cbTodosLocales = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,8,0) };
        tlSp.Children.Add(cbTodosLocales);
        tlSp.Children.Add(new TextBlock { Text = "Todos los locales", FontWeight = FontWeights.SemiBold,
            Foreground = EBC("#4A148C"), VerticalAlignment = VerticalAlignment.Center });
        todosLocalesBtn.Child = tlSp;
        DockPanel.SetDock(todosLocalesBtn, Dock.Top); rightDp.Children.Add(todosLocalesBtn);

        var sep = new Border { Height = 1, Background = EBC("#E1BEE7"), Margin = new Thickness(10, 4, 10, 4) };
        DockPanel.SetDock(sep, Dock.Top); rightDp.Children.Add(sep);

        // lista de locales con checkboxes
        var locScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _panelLocales = new StackPanel { Margin = new Thickness(10, 4, 10, 8) };
        locScroll.Content = _panelLocales;
        rightDp.Children.Add(locScroll);
        rightBorder.Child = rightDp;

        // lógica "todos los locales" toggle
        cbTodosLocales.Checked   += (_, _) => { foreach (var cb in _cbLocales.Values) cb.IsChecked = true; };
        cbTodosLocales.Unchecked += (_, _) => { foreach (var cb in _cbLocales.Values) cb.IsChecked = false; };
        todosLocalesBtn.MouseLeftButtonUp += (_, _) => { cbTodosLocales.IsChecked = !(cbTodosLocales.IsChecked ?? false); };

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void SetModo(bool todos)
    {
        _todosModo = todos;
        _btnTodos.Background     = todos ? EBC("#6A1B9A") : EBC("#F3E5F5");
        _btnSeleccion.Background = todos ? EBC("#F3E5F5") : EBC("#6A1B9A");
        foreach (var sp in new[] { _btnTodos.Child as StackPanel, _btnSeleccion.Child as StackPanel })
            if (sp != null) foreach (var c in sp.Children.OfType<TextBlock>())
                c.Foreground = sp.Parent == _btnTodos
                    ? (todos ? System.Windows.Media.Brushes.White : EBC("#6A1B9A") as System.Windows.Media.Brush)
                    : (todos ? EBC("#6A1B9A") : System.Windows.Media.Brushes.White);
        if (todos) foreach (var f in _articulos) f.Seleccionado = true;
        ActualizarConteo();
    }

    private void FiltrarGrid()
    {
        var txt = _txtBuscar.Text.Trim().ToLower();
        _dgPromo.ItemsSource = string.IsNullOrEmpty(txt)
            ? _articulos
            : _articulos.Where(a => a.Descripcion.ToLower().Contains(txt) ||
                                    a.Codigo.ToLower().Contains(txt)).ToList();
    }

    private void ActualizarConteo()
    {
        var sel = _todosModo ? _articulos.Count : _articulos.Count(a => a.Seleccionado);
        _lblConteo.Text = $"{_articulos.Count} artículo(s) en promoción  •  {sel} seleccionado(s)";
    }

    private async Task CargarAsync()
    {
        try {
            using var conn = _db.Create();
            _locales = (await conn.QueryAsync<(byte Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY ID_LOCAL")).ToList();

            // Construir checkboxes de locales
            foreach (var (id, nom) in _locales) {
                var row = new Border { CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 2),
                    Background = EBC("#FAFAFA"), Cursor = System.Windows.Input.Cursors.Hand };
                var rowSp = new StackPanel { Orientation = Orientation.Horizontal };
                var cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0,0,8,0) };
                _cbLocales[id] = cb;
                var lbl = new TextBlock { Text = $"{id}  {nom}", VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12, Foreground = EBC("#212121") };
                rowSp.Children.Add(cb); rowSp.Children.Add(lbl);
                row.Child = rowSp;
                row.MouseLeftButtonUp += (_, _) => { cb.IsChecked = !(cb.IsChecked ?? false); };
                row.MouseEnter += (_, _) => row.Background = EBC("#F3E5F5");
                row.MouseLeave += (_, _) => row.Background = EBC("#FAFAFA");
                _panelLocales.Children.Add(row);
            }

            // Cargar artículos en promo (distintos, con precio y conteo de locales)
            var arts = (await conn.QueryAsync<FilaPromo>(
                @"SELECT P.IDART AS Id, A.CA AS Codigo, A.D AS Descripcion,
                         MAX(P.PVENTA) AS Pventa, MAX(P.PPROMO) AS Ppromo,
                         COUNT(DISTINCT P.IDLOCAL) AS CantLocales
                  FROM PRICES P
                  INNER JOIN ARTICULOS A ON A.ID = P.IDART
                  WHERE P.PR = 1
                  GROUP BY P.IDART, A.CA, A.D
                  ORDER BY A.D")).ToList();

            foreach (var a in arts) { a.Seleccionado = true; a.OnChange = ActualizarConteo; }
            _articulos = arts;
            _dgPromo.ItemsSource = _articulos;
            ActualizarConteo();
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar: {ex.Message}");
        }
    }

    private async Task Finalizar()
    {
        var artsFinalizar = _todosModo
            ? _articulos
            : _articulos.Where(a => a.Seleccionado).ToList();

        if (artsFinalizar.Count == 0) {
            MessageBox.Show("No hay artículos seleccionados.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var localesSel = _cbLocales.Where(kv => kv.Value.IsChecked == true)
                                   .Select(kv => kv.Key).ToList();
        if (localesSel.Count == 0) {
            MessageBox.Show("Seleccione al menos un local.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var conf = MessageBox.Show(
            $"¿Finalizar promoción de {artsFinalizar.Count} artículo(s) en {localesSel.Count} local(es)?\n\n" +
            "Esta acción quitará el precio promocional en los locales seleccionados.",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (conf != MessageBoxResult.Yes) return;

        int ok = 0, sinPromo = 0, errores = 0;
        try {
            using var conn = _db.Create();
            // El SP acepta hasta 6 locales por llamada — hacemos lotes de 6
            foreach (var art in artsFinalizar) {
                for (int offset = 0; offset < localesSel.Count; offset += 6) {
                    var lote = localesSel.Skip(offset).Take(6).ToList();
                    var p = new DynamicParameters();
                    p.Add("@Idart",   art.Id);
                    p.Add("@Enpromo", (byte)0);
                    p.Add("@ppromo",  0m);
                    for (int i = 1; i <= 6; i++) {
                        bool activo = i <= lote.Count;
                        p.Add($"@P{i}", activo ? (byte)1 : (byte)0);
                        p.Add($"@L{i}", activo ? lote[i-1] : (byte)0);
                    }
                    p.Add("@Result", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
                    await conn.ExecuteAsync("TERMINAR_PROMOCION_LOCALES_CS", p, commandType: CommandType.StoredProcedure);
                    var res = p.Get<string>("@Result") ?? "";
                    if (res == "GUARDADO") ok++;
                    else if (res == "SIN_PROMO") sinPromo++;
                    else errores++;
                }
            }

            var msg = $"Proceso completado.\n\n  • Actualizados: {ok}\n  • Sin promo activa: {sinPromo}";
            if (errores > 0) msg += $"\n  • Errores: {errores}";
            MessageBox.Show(msg, "Resultado", MessageBoxButton.OK,
                errores > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await CargarAsync();
        } catch (Exception ex) {
            MessageBox.Show($"Error al finalizar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal class FilaPromo : System.ComponentModel.INotifyPropertyChanged
{
    public int    Id          { get; set; }
    public string Codigo      { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Pventa     { get; set; }
    public decimal Ppromo     { get; set; }
    public int    CantLocales { get; set; }
    public Action? OnChange   { get; set; }

    private bool _seleccionado = true;
    public bool Seleccionado {
        get => _seleccionado;
        set { _seleccionado = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Seleccionado))); OnChange?.Invoke(); }
    }

    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string PventaFmt  => Pventa.ToString("N0", _py);
    public string PpromoFmt  => Ppromo.ToString("N0", _py);
    public string LocalesStr => $"{CantLocales}";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

// ══════════════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN DE IMPRESORAS
// ══════════════════════════════════════════════════════════════════════════════
public class ImpressorasWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private TextBox    _txtComp = null!, _txtFact = null!, _txtRep = null!;
    private ComboBox   _cboTipoComp = null!, _cboTipoFact = null!, _cboTipoRep = null!;
    private ComboBox   _cboTamComp  = null!, _cboTamFact  = null!;
    private StackPanel _rowTamComp  = null!, _rowTamFact  = null!;
    private Border     _previewCompCard = null!, _previewFactCard = null!;
    private TextBlock  _previewCompTxt  = null!, _previewFactTxt  = null!;
    private TextBlock  _lblStatus = null!;

    // Paleta azul única
    private static readonly string Azul1 = "#0E2F44";  // muy oscuro — títulos de sección
    private static readonly string Azul2 = "#1565C0";  // base — strip, botones
    private static readonly string Azul3 = "#1E88E5";  // medio — hover, acento
    private static readonly string AzulBg   = "#EEF4FB"; // fondo card body
    private static readonly string AzulLine = "#BBDEFB"; // borde preview

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    private static readonly string[] Tipos     = { "Normal", "Ticket" };
    private static readonly string[] TamTicket = { "80 mm  (ancho)", "58 mm  (estándar)", "40 mm  (mini)" };

    public ImpressorasWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Configuración de Impresoras";
        Width = 700; Height = 580; MinWidth = 620; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EBC("#F0F4F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        _ = CargarAsync();
    }

    // ── Helper: construye una card de impresora con selector de tamaño opcional
    // Retorna (inner StackPanel, previewCard, previewTxt, rowTam, cboTipo, cboTam)
    private (StackPanel inner, Border prevCard, TextBlock prevTxt,
             StackPanel rowTam, ComboBox cboTipo, ComboBox cboTam)
        BuildCard(string titulo, string icono, string subtitulo,
                  out TextBox txtImpresora, bool conTipo, bool conTam)
    {
        var card = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC(AzulLine), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0,0,0,12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 12, Opacity = 0.07, ShadowDepth = 2, Direction = 270,
                Color = System.Windows.Media.Colors.Black }
        };

        var outer = new DockPanel();

        // Strip izquierdo azul oscuro
        var strip = new Border { Width = 5, Background = EBC(Azul1),
            CornerRadius = new CornerRadius(10, 0, 0, 10) };
        DockPanel.SetDock(strip, Dock.Left); outer.Children.Add(strip);

        var inner = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };

        // Cabecera de la card: ícono + título + subtítulo
        var hRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
        var icoBox = new Border {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(8),
            Background = EBC(AzulBg), Margin = new Thickness(0,0,12,0) };
        icoBox.Child = new TextBlock { Text = icono, FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        hRow.Children.Add(icoBox);
        var hText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hText.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.Bold,
            FontSize = 13, Foreground = EBC(Azul1) });
        hText.Children.Add(new TextBlock { Text = subtitulo,
            FontSize = 10, Foreground = EBC("#78909C"), Margin = new Thickness(0,2,0,0) });
        hRow.Children.Add(hText);
        inner.Children.Add(hRow);

        // Separador
        inner.Children.Add(new Border { Height = 1, Background = EBC("#E3EAF2"), Margin = new Thickness(0,0,0,12) });

        // Fila impresora
        var gImp = new Grid { Margin = new Thickness(0,0,0,8) };
        gImp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        gImp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gImp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lblImpresora = new TextBlock { Text = "Impresora:", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = EBC("#37474F") };
        Grid.SetColumn(lblImpresora, 0); gImp.Children.Add(lblImpresora);

        txtImpresora = new TextBox { IsReadOnly = true, Padding = new Thickness(10,7,10,7),
            Background = EBC("#F8FAFC"), BorderBrush = EBC("#CFD8DC"),
            BorderThickness = new Thickness(1), FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center };
        Grid.SetColumn(txtImpresora, 1); gImp.Children.Add(txtImpresora);

        var btnSel = new Button {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = "🖨", FontSize = 13, Margin = new Thickness(0,0,6,0),
                    VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "Seleccionar", FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center } } },
            Height = 34, Padding = new Thickness(14,0,14,0), Margin = new Thickness(8,0,0,0),
            Background = EBC(Azul2), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            FontWeight = FontWeights.SemiBold };
        var captTxt = txtImpresora;
        btnSel.Click += (_, _) => AbrirSelectorImpresora(captTxt);
        Grid.SetColumn(btnSel, 2); gImp.Children.Add(btnSel);
        inner.Children.Add(gImp);

        // Fila tipo (Normal / Ticket)
        ComboBox cboTipo = new(), cboTam = new();
        StackPanel rowTam = new();
        Border prevCard; TextBlock prevTxt;

        if (conTipo) {
            var gTipo = new Grid { Margin = new Thickness(0,0,0,6) };
            gTipo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            gTipo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gTipo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblTipo = new TextBlock { Text = "Tipo:", VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold, Foreground = EBC("#37474F") };
            Grid.SetColumn(lblTipo, 0); gTipo.Children.Add(lblTipo);

            cboTipo = new ComboBox { Width = 165, Padding = new Thickness(8,6,8,6),
                BorderBrush = EBC("#CFD8DC"), FontSize = 12 };
            foreach (var t in Tipos) cboTipo.Items.Add(t);
            cboTipo.SelectedIndex = 0;
            Grid.SetColumn(cboTipo, 1); gTipo.Children.Add(cboTipo);

            var hint = new TextBlock { Text = "  Normal = A4 completo   ·   Ticket = rollo térmico",
                Foreground = EBC("#90A4AE"), FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hint, 2); gTipo.Children.Add(hint);
            inner.Children.Add(gTipo);
        }

        // Fila tamaño ticket
        rowTam = new StackPanel { Orientation = Orientation.Horizontal,
            Margin = new Thickness(0,0,0,6), Visibility = Visibility.Collapsed };
        if (conTam) {
            var lblTam = new TextBlock { Text = "Tamaño:", VerticalAlignment = VerticalAlignment.Center,
                Width = 95, FontWeight = FontWeights.SemiBold, Foreground = EBC("#37474F") };
            rowTam.Children.Add(lblTam);
            cboTam = new ComboBox { Width = 205, Padding = new Thickness(8,6,8,6),
                BorderBrush = EBC("#CFD8DC"), FontSize = 12 };
            foreach (var s in TamTicket) cboTam.Items.Add(s);
            cboTam.SelectedIndex = 0;
            rowTam.Children.Add(cboTam);
            rowTam.Children.Add(new TextBlock { Text = "  del rollo de papel",
                Foreground = EBC("#90A4AE"), FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center });
            inner.Children.Add(rowTam);
        }

        // Preview descriptivo
        prevCard = new Border {
            CornerRadius = new CornerRadius(7), Margin = new Thickness(0,8,0,0),
            Padding = new Thickness(12, 8, 12, 8),
            Background = EBC(AzulBg), BorderBrush = EBC(AzulLine),
            BorderThickness = new Thickness(1) };
        prevTxt = new TextBlock { FontSize = 11, Foreground = EBC(Azul2),
            TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
        prevCard.Child = prevTxt;
        if (conTipo) inner.Children.Add(prevCard);

        outer.Children.Add(inner);
        card.Child = outer;
        // body se agrega desde el caller
        return (inner, prevCard, prevTxt, rowTam, cboTipo, cboTam);
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Padding = new Thickness(22, 15, 22, 15) };
        hdr.Background = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(14, 47, 68),
            System.Windows.Media.Color.FromRgb(21, 101, 192), 0);
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "🖨  CONFIGURACIÓN DE IMPRESORAS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock {
            Text = "Asigne una impresora y formato de impresión para cada documento",
            Foreground = EBC("#90CAF9"), FontSize = 11, Margin = new Thickness(0,3,0,0) });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#BBDEFB"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16, 10, 16, 10) };
        var pieDp = new DockPanel();
        _lblStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#546E7A"), FontSize = 11 };
        DockPanel.SetDock(_lblStatus, Dock.Left); pieDp.Children.Add(_lblStatus);

        var pieBtns = new StackPanel { Orientation = Orientation.Horizontal };
        Button PBtn(string txt, string bg, string fg = "#FFFFFF") => new Button {
            Content = txt, Height = 36, Padding = new Thickness(22,0,22,0),
            Margin = new Thickness(8,0,0,0), Background = EBC(bg),
            Foreground = EBC(fg), FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12 };
        var btnGuardar = PBtn("💾  Guardar", Azul2);
        var btnCerrar  = PBtn("✖  Cerrar",  "#546E7A");
        btnGuardar.Click += async (_, _) => await Guardar();
        btnCerrar.Click  += (_, _) => Close();
        pieBtns.Children.Add(btnGuardar); pieBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(pieBtns, Dock.Right); pieDp.Children.Add(pieBtns);
        pie.Child = pieDp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo ────────────────────────────────────────────────────────
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(20, 16, 20, 16) };
        var body = new StackPanel();

        // ── Card 1: Comprobantes de Venta y Cobros ────────────────────────
        var (innerC, prevCardC, prevTxtC, rowTamC, cboTipoC, cboTamC) =
            BuildCard("Comprobantes de Venta y Cobros", "🧾",
                      "Tickets de cobro de cuotas y comprobantes de venta",
                      out _txtComp, conTipo: true, conTam: true);
        _cboTipoComp = cboTipoC; _cboTamComp = cboTamC;
        _rowTamComp = rowTamC; _previewCompCard = prevCardC; _previewCompTxt = prevTxtC;
        // agregar la card al body (innerC.Parent.Parent es la card)
        body.Children.Add((Border)((DockPanel)innerC.Parent).Parent);
        _cboTipoComp.SelectionChanged += (_, _) => ActualizarPreview(_cboTipoComp, _cboTamComp, _rowTamComp, _previewCompCard, _previewCompTxt);
        _cboTamComp.SelectionChanged  += (_, _) => ActualizarPreview(_cboTipoComp, _cboTamComp, _rowTamComp, _previewCompCard, _previewCompTxt);

        // ── Card 2: Facturas ──────────────────────────────────────────────
        var (innerF, prevCardF, prevTxtF, rowTamF, cboTipoF, cboTamF) =
            BuildCard("Facturas", "📄",
                      "Facturas de venta a crédito y contado",
                      out _txtFact, conTipo: true, conTam: true);
        _cboTipoFact = cboTipoF; _cboTamFact = cboTamF;
        _rowTamFact = rowTamF; _previewFactCard = prevCardF; _previewFactTxt = prevTxtF;
        body.Children.Add((Border)((DockPanel)innerF.Parent).Parent);
        _cboTipoFact.SelectionChanged += (_, _) => ActualizarPreview(_cboTipoFact, _cboTamFact, _rowTamFact, _previewFactCard, _previewFactTxt);
        _cboTamFact.SelectionChanged  += (_, _) => ActualizarPreview(_cboTipoFact, _cboTamFact, _rowTamFact, _previewFactCard, _previewFactTxt);

        // ── Card 3: Reportes e Informes ───────────────────────────────────
        {
            var (innerR, _, _, _, _, _) =
                BuildCard("Reportes e Informes", "📊",
                          "Reportes de gestión, atrasos e históricos",
                          out _txtRep, conTipo: false, conTam: false);
            body.Children.Add((Border)((DockPanel)innerR.Parent).Parent);
        }
        _cboTipoRep = new ComboBox(); // dummy, no persiste

        scroll.Content = body;
        root.Children.Add(scroll);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        // Preview inicial
        ActualizarPreview(_cboTipoComp, _cboTamComp, _rowTamComp, _previewCompCard, _previewCompTxt);
        ActualizarPreview(_cboTipoFact, _cboTamFact, _rowTamFact, _previewFactCard, _previewFactTxt);
    }

    // Preview y visibilidad tamaño ticket — compartido por ambas cards
    private void ActualizarPreview(ComboBox cboTipo, ComboBox cboTam,
        StackPanel rowTam, Border prevCard, TextBlock prevTxt)
    {
        bool esTicket = cboTipo.SelectedIndex == 1;
        rowTam.Visibility = esTicket ? Visibility.Visible : Visibility.Collapsed;

        if (!esTicket) {
            prevTxt.Text =
                "📄  Formato A4 — Hoja completa con encabezado corporativo Credimar, " +
                "tabla de datos en columnas, caja de total destacada, firma del cliente y timbrado fiscal. " +
                "Se genera PDF automáticamente si no hay impresora física configurada.";
        } else {
            prevTxt.Text = cboTam.SelectedIndex switch {
                0 => "🧾  Ticket 80 mm — Rollo ancho de ticketera térmica POS. Fuente Courier New, " +
                     "diseño amplio en columnas, total resaltado en grande, monto en letras y timbrado al pie.",
                2 => "🧾  Ticket 40 mm — Rollo mini para ticketera pequeña. Fuente reducida (6–8 pt), " +
                     "descripción de artículos acortada, layout ultra-compacto optimizado para papel angosto.",
                _ => "🧾  Ticket 58 mm — Rollo estándar de ticketera térmica POS. Fuente Courier New, " +
                     "diseño compacto en columnas, total resaltado en grande, monto en letras y timbrado al pie.",
            };
        }
        // preview siempre en azul — varía solo el tono
        prevCard.Background  = EBC(esTicket ? "#EAF2FF" : AzulBg);
        prevCard.BorderBrush = EBC(esTicket ? "#90CAF9" : AzulLine);
        prevTxt.Foreground   = EBC(esTicket ? Azul1 : Azul2);
    }

    private void AbrirSelectorImpresora(TextBox destino)
    {
        var impresoras = System.Drawing.Printing.PrinterSettings.InstalledPrinters
            .Cast<string>().OrderBy(x => x).ToList();

        var dlg = new Window {
            Title = "Impresoras disponibles",
            Width = 440, Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };
        var dp = new DockPanel();

        var dhdr = new Border { Padding = new Thickness(14,11,14,11) };
        dhdr.Background = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(21,101,192),
            System.Windows.Media.Color.FromRgb(30,136,229), 0);
        var dhSp = new StackPanel();
        dhSp.Children.Add(new TextBlock { Text = "🖨  Seleccionar impresora",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 });
        dhSp.Children.Add(new TextBlock { Text = $"{impresoras.Count} impresora(s) detectada(s)",
            Foreground = EBC("#BBDEFB"), FontSize = 10, Margin = new Thickness(0,2,0,0) });
        dhdr.Child = dhSp;
        DockPanel.SetDock(dhdr, Dock.Top); dp.Children.Add(dhdr);

        var dpie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(10,8,10,8) };
        var btnLimpiar = new Button { Content = "✕  Quitar impresora", Height = 30,
            Padding = new Thickness(12,0,12,0), Margin = new Thickness(0,0,8,0),
            Background = EBC("#EF5350"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11 };
        btnLimpiar.Click += (_, _) => { destino.Text = ""; dlg.Close(); };
        dpie.Child = btnLimpiar;
        DockPanel.SetDock(dpie, Dock.Bottom); dp.Children.Add(dpie);

        var scroll2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10,8,10,8) };
        var listSp = new StackPanel();

        foreach (var imp in impresoras) {
            var row = new Border {
                Background = EBC("#FAFAFA"), CornerRadius = new CornerRadius(5),
                BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0,0,0,4),
                Cursor = System.Windows.Input.Cursors.Hand };
            var rowSp = new StackPanel { Orientation = Orientation.Horizontal };
            rowSp.Children.Add(new TextBlock { Text = "🖨", FontSize = 16,
                Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
            rowSp.Children.Add(new TextBlock { Text = imp, VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12, Foreground = EBC("#212121"), TextWrapping = TextWrapping.Wrap, MaxWidth = 340 });
            row.Child = rowSp;
            row.MouseEnter += (_, _) => { row.Background = EBC("#E3F2FD"); row.BorderBrush = EBC("#1565C0"); };
            row.MouseLeave += (_, _) => { row.Background = EBC("#FAFAFA"); row.BorderBrush = EBC("#E0E0E0"); };
            row.MouseLeftButtonUp += (_, _) => { destino.Text = imp; dlg.Close(); };
            listSp.Children.Add(row);
        }
        if (impresoras.Count == 0)
            listSp.Children.Add(new TextBlock { Text = "No se encontraron impresoras instaladas.",
                Foreground = EBC("#9E9E9E"), FontSize = 12, Margin = new Thickness(10) });

        scroll2.Content = listSp; dp.Children.Add(scroll2);
        dlg.Content = dp;
        dlg.ShowDialog();
    }

    // Identificador de esta PC — se usa como clave en la tabla IMPRESORAS
    private static string NombrePC => Environment.MachineName.ToUpper();

    private async Task CargarAsync()
    {
        try {
            using var conn = _db.Create();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT NOMBRE_COMPROBANTE, TIPO_COMPROBANTE, FORMATO_COMPROBANTE, " +
                "NOMBRE_FACTURA, TIPO_FACTURA, FORMATO_FACTURA, " +
                "NOMBRE_REPORTE FROM IMPRESORAS WHERE NOMBRE_PC=@pc",
                new { pc = NombrePC });
            if (row == null) return;

            _txtComp.Text = ((string?)row.NOMBRE_COMPROBANTE)?.Trim() ?? "";
            _txtFact.Text = ((string?)row.NOMBRE_FACTURA)?.Trim()     ?? "";
            _txtRep.Text  = ((string?)row.NOMBRE_REPORTE)?.Trim()     ?? "";

            byte tipoC    = row.TIPO_COMPROBANTE   ?? 0;
            byte tipoF    = row.TIPO_FACTURA        ?? 0;
            byte formatoC = row.FORMATO_COMPROBANTE ?? 0;
            byte formatoF = row.FORMATO_FACTURA     ?? 0;

            _cboTipoComp.SelectedIndex = tipoC == 1 ? 1 : 0;
            _cboTipoFact.SelectedIndex = tipoF == 1 ? 1 : 0;

            // FORMATO: 0=A4, 1=58mm→idx 1, 2=40mm→idx 2, 3=80mm→idx 0
            _cboTamComp.SelectedIndex = formatoC switch { 3 => 0, 2 => 2, _ => 1 };
            _cboTamFact.SelectedIndex = formatoF switch { 3 => 0, 2 => 2, _ => 1 };

            ActualizarPreview(_cboTipoComp, _cboTamComp, _rowTamComp, _previewCompCard, _previewCompTxt);
            ActualizarPreview(_cboTipoFact, _cboTamFact, _rowTamFact, _previewFactCard, _previewFactTxt);
            _lblStatus.Text = "Configuración cargada correctamente";
        } catch (Exception ex) {
            _lblStatus.Text = $"Error al cargar: {ex.Message}";
        }
    }

    private async Task Guardar()
    {
        try {
            byte tipoC = (byte)(_cboTipoComp.SelectedIndex == 1 ? 1 : 0);
            byte tipoF = (byte)(_cboTipoFact.SelectedIndex == 1 ? 1 : 0);

            // FORMATO: Normal→0, Ticket 80mm→3, Ticket 58mm→1, Ticket 40mm→2
            byte formatoC = tipoC == 0 ? (byte)0 : _cboTamComp.SelectedIndex switch { 0 => (byte)3, 2 => (byte)2, _ => (byte)1 };
            byte formatoF = tipoF == 0 ? (byte)0 : _cboTamFact.SelectedIndex switch { 0 => (byte)3, 2 => (byte)2, _ => (byte)1 };

            using var conn = _db.Create();
            await conn.ExecuteAsync(
                @"IF EXISTS (SELECT 1 FROM IMPRESORAS WHERE NOMBRE_PC=@pc)
                      UPDATE IMPRESORAS SET
                          NOMBRE_COMPROBANTE=@nc, TIPO_COMPROBANTE=@tc, FORMATO_COMPROBANTE=@fc,
                          NOMBRE_FACTURA=@nf,     TIPO_FACTURA=@tf,     FORMATO_FACTURA=@ff,
                          NOMBRE_REPORTE=@nr
                      WHERE NOMBRE_PC=@pc
                  ELSE
                      INSERT INTO IMPRESORAS(ID_IMPRESORA, NOMBRE_PC,
                          NOMBRE_COMPROBANTE,TIPO_COMPROBANTE,FORMATO_COMPROBANTE,
                          NOMBRE_FACTURA,TIPO_FACTURA,FORMATO_FACTURA,NOMBRE_REPORTE)
                      VALUES(ISNULL((SELECT MAX(ID_IMPRESORA)+1 FROM IMPRESORAS),1),
                             @pc,@nc,@tc,@fc,@nf,@tf,@ff,@nr)",
                new { pc = NombrePC,
                      nc = _txtComp.Text.Trim(), tc = tipoC, fc = formatoC,
                      nf = _txtFact.Text.Trim(), tf = tipoF, ff = formatoF,
                      nr = _txtRep.Text.Trim() });

            _lblStatus.Text = $"✔  Guardado correctamente — {DateTime.Now:HH:mm:ss}";
            MessageBox.Show("Configuración de impresoras guardada.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (Exception ex) {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  RETIRO LIBRE
// ══════════════════════════════════════════════════════════════════════════════
public class RetiroLibreWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtMonto    = null!;
    private TextBox   _txtHoras    = null!;
    private TextBox   _txtConcepto = null!;
    private TextBox   _txtNota     = null!;
    private ComboBox  _cboTipo     = null!;

    public RetiroLibreWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Retiro Libre"; Width = 440; Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1A5276")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Registrar Retiro Libre", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _cboTipo = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Retiro en efectivo",   Tag = (byte)1 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Adelanto de sueldo",   Tag = (byte)2 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Devolución proveedor", Tag = (byte)3 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Otro",                 Tag = (byte)4 });
        _cboTipo.SelectedIndex = 0;
        Row("Tipo de retiro:", _cboTipo);

        _txtMonto    = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _txtHoras    = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _txtConcepto = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
        _txtNota     = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 50,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        Row("Monto:",    _txtMonto);
        Row("Horas:",    _txtHoras);
        Row("Concepto:", _txtConcepto);
        Row("Nota:",     _txtNota);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var btnG = new Button { Content = "✔ Registrar", Width = 100, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1A5276")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Registrar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Registrar()
    {
        if (!decimal.TryParse(_txtMonto.Text, out var monto) || monto < 0) { MessageBox.Show("Monto inválido."); return; }
        if (!decimal.TryParse(_txtHoras.Text, out var horas)) horas = 0;
        if (string.IsNullOrWhiteSpace(_txtConcepto.Text)) { MessageBox.Show("Ingrese un concepto."); return; }
        var sesion = SessionService.Instance;
        byte tipo = _cboTipo.SelectedItem is ComboBoxItem ci && ci.Tag is byte b ? b : (byte)1;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@ID",        0);
            p.Add("@MOVIMIENTO",(byte)2);
            p.Add("@TIPO",      tipo);
            p.Add("@MONTO",     monto);
            p.Add("@HORAS",     horas);
            p.Add("@CONCEPTO",  _txtConcepto.Text.Trim());
            p.Add("@IDU",       (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@NOMBRE",    sesion.UsuarioActual?.NombreUsuario ?? "");
            p.Add("@NOTA",      _txtNota.Text.Trim());
            p.Add("@ID_LOCAL",  (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("AGREGAR_RETIRO_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Retiro registrado. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al registrar retiro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  PROMOCIÓN — Crear / activar promoción de un artículo
// ══════════════════════════════════════════════════════════════════════════════
public class PromocionWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox    _txtIdArt   = null!;
    private TextBox    _txtPrecio  = null!;
    private DatePicker _dtInicio   = null!;
    private DatePicker _dtFin      = null!;
    private CheckBox[] _chkLocales = null!;
    private TextBox[]  _txtPases   = null!;

    public PromocionWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Crear / Activar Promoción"; Width = 520; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root   = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = root;

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#6C3483")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Promoción de Artículo", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _txtIdArt  = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
        _txtPrecio = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _dtInicio  = new DatePicker { SelectedDate = DateTime.Today };
        _dtFin     = new DatePicker { SelectedDate = DateTime.Today.AddMonths(1) };
        Row("ID Artículo (IDART):", _txtIdArt);
        Row("Precio de promoción:", _txtPrecio);
        Row("Fecha inicio:", _dtInicio);
        Row("Fecha fin:",    _dtFin);

        root.Children.Add(new TextBlock { Text = "Locales participantes:",
            Margin = new Thickness(0, 10, 0, 4), FontWeight = FontWeights.SemiBold });

        _chkLocales = new CheckBox[6];
        _txtPases   = new TextBox[6];
        for (int i = 0; i < 6; i++)
        {
            var rowDp = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            _chkLocales[i] = new CheckBox { Content = $"Local {i + 1}", Width = 90, VerticalAlignment = VerticalAlignment.Center };
            _txtPases[i]   = new TextBox  { Padding = new Thickness(4, 2, 4, 2), Text = "0", Width = 100 };
            DockPanel.SetDock(_chkLocales[i], Dock.Left); rowDp.Children.Add(_chkLocales[i]);
            rowDp.Children.Add(new TextBlock { Text = "  Precio: ", VerticalAlignment = VerticalAlignment.Center });
            rowDp.Children.Add(_txtPases[i]);
            root.Children.Add(rowDp);
        }

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnG = new Button { Content = "✔ Guardar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#6C3483")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Guardar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = scroll;
    }

    private async Task Guardar()
    {
        if (!int.TryParse(_txtIdArt.Text.Trim(), out var idArt)) { MessageBox.Show("ID de artículo inválido."); return; }
        if (!decimal.TryParse(_txtPrecio.Text, out var precio) || precio < 0) { MessageBox.Show("Precio inválido."); return; }
        var inicio = _dtInicio.SelectedDate ?? DateTime.Today;
        var fin    = _dtFin.SelectedDate    ?? DateTime.Today.AddMonths(1);
        if (fin <= inicio) { MessageBox.Show("La fecha de fin debe ser posterior al inicio."); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@idart",       idArt);
            p.Add("@enpromo",     (byte)1);
            p.Add("@preciopromo", precio);
            for (int i = 0; i < 6; i++)
            {
                bool activo = _chkLocales[i].IsChecked == true;
                decimal.TryParse(_txtPases[i].Text, out var pLocal);
                p.Add($"@Pase{i + 1}", activo ? pLocal : 0m);
                p.Add($"@L{i + 1}",   activo ? (byte)1 : (byte)0);
            }
            p.Add("@inicio", inicio);
            p.Add("@fin",    fin);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("GUARDAR_PROMOCIONAR_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Promoción guardada. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar promoción: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  PAGO DE REMUNERACIONES
// ══════════════════════════════════════════════════════════════════════════════
public class PagoRemuneracionesWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtSalario      = null!, _txtVenta         = null!, _txtPorcVenta    = null!;
    private TextBox _txtCobranza     = null!, _txtPorcCobranza  = null!, _txtPlus         = null!;
    private TextBox _txtHorasExtra   = null!, _txtBonificacion  = null!, _txtOtrasComis   = null!;
    private TextBox _txtAusencias    = null!, _txtAdelantos     = null!, _txtIps          = null!;
    private TextBox _txtCuotas       = null!, _txtMultas        = null!, _txtOtros        = null!;
    private TextBox _txtEquis        = null!, _txtNombre        = null!, _txtNotaAsig     = null!;
    private TextBox _txtNotaEgr      = null!;
    private DatePicker _dtFecha           = null!;
    private TextBlock  _lblTotalIngresos  = null!, _lblTotalEgresos = null!;

    public PagoRemuneracionesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Pago de Remuneraciones"; Width = 640; Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root   = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = root;

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Planilla de Pago de Remuneraciones",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 5, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }
        TextBox MkTxt(string def = "0") => new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = def };

        _txtNombre = MkTxt(""); Row("Nombre del funcionario:", _txtNombre);
        _dtFecha   = new DatePicker { SelectedDate = DateTime.Today }; Row("Fecha:", _dtFecha);

        root.Children.Add(new TextBlock { Text = "── INGRESOS ──────────────────",
            Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")! });

        _txtSalario      = MkTxt(); Row("Salario base:", _txtSalario);
        _txtVenta        = MkTxt(); Row("Venta (monto):", _txtVenta);
        _txtPorcVenta    = MkTxt(); Row("% Comisión por venta:", _txtPorcVenta);
        _txtCobranza     = MkTxt(); Row("Cobranza (monto):", _txtCobranza);
        _txtPorcCobranza = MkTxt(); Row("% Comisión por cobranza:", _txtPorcCobranza);
        _txtPlus         = MkTxt(); Row("Plus:", _txtPlus);
        _txtHorasExtra   = MkTxt(); Row("Horas extras:", _txtHorasExtra);
        _txtBonificacion = MkTxt(); Row("Bonificación:", _txtBonificacion);
        _txtOtrasComis   = MkTxt(); Row("Otras comisiones:", _txtOtrasComis);

        _lblTotalIngresos = new TextBlock { Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkGreen };
        root.Children.Add(_lblTotalIngresos);

        root.Children.Add(new TextBlock { Text = "── EGRESOS ───────────────────",
            Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")! });

        _txtAusencias = MkTxt(); Row("Ausencias:", _txtAusencias);
        _txtAdelantos = MkTxt(); Row("Adelantos:", _txtAdelantos);
        _txtIps       = MkTxt(); Row("IPS:", _txtIps);
        _txtCuotas    = MkTxt(); Row("Cuotas:", _txtCuotas);
        _txtMultas    = MkTxt(); Row("Multas:", _txtMultas);
        _txtOtros     = MkTxt(); Row("Otros:", _txtOtros);
        _txtEquis     = MkTxt(); Row("Equis:", _txtEquis);

        _lblTotalEgresos = new TextBlock { Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkRed };
        root.Children.Add(_lblTotalEgresos);

        _txtNotaAsig = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 40,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        _txtNotaEgr  = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 40,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        Row("Nota asignación:", _txtNotaAsig);
        Row("Nota egreso:",     _txtNotaEgr);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var btnCalc = new Button { Content = "Calcular", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2E86C1")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnCalc.Click += (_, _) => Calcular();
        var btnG = new Button { Content = "✔ Generar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Generar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnCalc); btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = scroll;

        foreach (var tb in new[] { _txtSalario, _txtVenta, _txtPorcVenta, _txtCobranza, _txtPorcCobranza,
                                   _txtPlus, _txtHorasExtra, _txtBonificacion, _txtOtrasComis,
                                   _txtAusencias, _txtAdelantos, _txtIps, _txtCuotas, _txtMultas, _txtOtros, _txtEquis })
            tb.TextChanged += (_, _) => Calcular();
    }

    private (decimal ti, decimal tv, decimal tc, decimal te) GetTotals()
    {
        decimal D(TextBox t) => decimal.TryParse(t.Text, out var v) ? v : 0;
        var tv = D(_txtVenta) * D(_txtPorcVenta) / 100m;
        var tc = D(_txtCobranza) * D(_txtPorcCobranza) / 100m;
        var ti = D(_txtSalario) + tv + tc + D(_txtPlus) + D(_txtHorasExtra) + D(_txtBonificacion) + D(_txtOtrasComis);
        var te = D(_txtAusencias) + D(_txtAdelantos) + D(_txtIps) + D(_txtCuotas) + D(_txtMultas) + D(_txtOtros) + D(_txtEquis);
        return (ti, tv, tc, te);
    }

    private void Calcular()
    {
        var (ti, _, _, te) = GetTotals();
        _lblTotalIngresos.Text = $"Total ingresos: {ti:N2}";
        _lblTotalEgresos.Text  = $"Total egresos: {te:N2}   →  Neto: {(ti - te):N2}";
    }

    private async Task Generar()
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text)) { MessageBox.Show("Ingrese el nombre del funcionario."); return; }
        decimal D(TextBox t) => decimal.TryParse(t.Text, out var v) ? v : 0;
        var (ti, tv, tc, te) = GetTotals();
        var sesion = SessionService.Instance;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@IDGPS",           0);
            p.Add("@IDU",             (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@IDLOCAL",         (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@SALARIO",         D(_txtSalario));
            p.Add("@VENTA",           D(_txtVenta));
            p.Add("@PORCVENTA",       D(_txtPorcVenta));
            p.Add("@TOTALVENTA",      tv);
            p.Add("@COBRANZA",        D(_txtCobranza));
            p.Add("@PORCCOBRANZA",    D(_txtPorcCobranza));
            p.Add("@TOTALCOBRANZA",   tc);
            p.Add("@PLUS",            D(_txtPlus));
            p.Add("@HORASEXTRAS",     D(_txtHorasExtra));
            p.Add("@BONIFICACION",    D(_txtBonificacion));
            p.Add("@OTRASCOMISIONES", D(_txtOtrasComis));
            p.Add("@TOTALINGRESOS",   ti);
            p.Add("@AUSENCIAS",       D(_txtAusencias));
            p.Add("@ADELANTOS",       D(_txtAdelantos));
            p.Add("@IPS",             D(_txtIps));
            p.Add("@CUOTAS",          D(_txtCuotas));
            p.Add("@MULTAS",          D(_txtMultas));
            p.Add("@OTROS",           D(_txtOtros));
            p.Add("@EQUIS",           D(_txtEquis));
            p.Add("@TOTALEGRESOS",    te);
            p.Add("@NOMBRE",          _txtNombre.Text.Trim());
            p.Add("@FECHA",           _dtFecha.SelectedDate ?? DateTime.Today);
            p.Add("@NOTAASIGNACION",  _txtNotaAsig.Text.Trim());
            p.Add("@NOTAEGRESO",      _txtNotaEgr.Text.Trim());
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("GENERAR_PAGOSALARIO_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Planilla generada. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al generar planilla: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BLOQUEAR TRANSFERENCIAS
// ══════════════════════════════════════════════════════════════════════════════
public class BloquearTransfWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid  _grid      = null!;
    private TextBox   _txtBuscar = null!;
    private ComboBox  _cboEstado = null!;
    private ComboBox  _cboOrigen = null!;
    private TextBlock _lblPend   = null!, _lblBloq = null!, _lblTotal = null!, _lblInfo = null!;

    private List<FilaRemito> _todos   = new();
    private List<FilaRemito> _filtros = new();

    private static System.Windows.Media.SolidColorBrush TB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public BloquearTransfWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Gestión de Transferencias";
        Width = 1020; Height = 600; MinWidth = 860; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = TB("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = TB("#4E342E"), Padding = new Thickness(18, 12, 18, 12) };
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hdrLeft = new StackPanel();
        hdrLeft.Children.Add(new TextBlock { Text = "GESTIÓN DE TRANSFERENCIAS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrLeft.Children.Add(new TextBlock { Text = "Bloqueo y desbloqueo de remitos pendientes",
            Foreground = TB("#BCAAA4"), FontSize = 11 });
        Grid.SetColumn(hdrLeft, 0); hdrG.Children.Add(hdrLeft);
        // tarjetas de resumen en el header
        var cards = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Border StatCard(string lbl, out TextBlock val, string bg) {
            var b = new Border { Background = TB(bg), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(6, 0, 0, 0) };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            val = new TextBlock { Text = "0", FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White, TextAlignment = TextAlignment.Center };
            sp.Children.Add(val);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 9, Foreground = TB("#FFFFFFB0"),
                TextAlignment = TextAlignment.Center });
            b.Child = sp; return b;
        }
        cards.Children.Add(StatCard("PENDIENTES", out _lblPend, "#E53935"));
        cards.Children.Add(StatCard("BLOQUEADOS",  out _lblBloq, "#6D4C41"));
        cards.Children.Add(StatCard("TOTAL",       out _lblTotal,"#37474F"));
        Grid.SetColumn(cards, 1); hdrG.Children.Add(cards);
        hdr.Child = hdrG;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Barra de filtros ──────────────────────────────────────────────
        var filtBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = TB("#E0E0E0"), BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(14, 10, 14, 10) };
        var filtSp = new StackPanel { Orientation = Orientation.Horizontal };

        TextBlock FL(string t) => new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = TB("#5D4037"),
            Margin = new Thickness(12, 0, 6, 0) };

        _txtBuscar = new TextBox { Width = 200, Height = 32, Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = TB("#BDBDBD"), BorderThickness = new Thickness(1) };
        _txtBuscar.TextChanged += (_, _) => AplicarFiltro();

        _cboEstado = new ComboBox { Width = 140, Height = 32, Padding = new Thickness(6, 0, 6, 0) };
        _cboEstado.Items.Add(new ComboBoxItem { Content = "Todos los estados", Tag = "TODOS" });
        _cboEstado.Items.Add(new ComboBoxItem { Content = "🔴  Pendiente",     Tag = "Pendiente" });
        _cboEstado.Items.Add(new ComboBoxItem { Content = "🔒  Bloqueado",     Tag = "Bloqueado" });
        _cboEstado.SelectedIndex = 0;
        _cboEstado.SelectionChanged += (_, _) => AplicarFiltro();

        _cboOrigen = new ComboBox { Width = 180, Height = 32, Padding = new Thickness(6, 0, 6, 0) };
        _cboOrigen.Items.Add(new ComboBoxItem { Content = "Todos los orígenes", Tag = "" });
        _cboOrigen.SelectedIndex = 0;
        _cboOrigen.SelectionChanged += (_, _) => AplicarFiltro();

        var btnRefresh = new Button { Content = "↻  Actualizar", Height = 32,
            Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(12, 0, 0, 0),
            Background = TB("#5D4037"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        btnRefresh.Click += async (_, _) => await Cargar();

        filtSp.Children.Add(FL("Buscar:"));
        filtSp.Children.Add(_txtBuscar);
        filtSp.Children.Add(FL("Estado:"));
        filtSp.Children.Add(_cboEstado);
        filtSp.Children.Add(FL("Origen:"));
        filtSp.Children.Add(_cboOrigen);
        filtSp.Children.Add(btnRefresh);
        filtBar.Child = filtSp;
        DockPanel.SetDock(filtBar, Dock.Top); root.Children.Add(filtBar);

        // ── Pie: acciones + info ──────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = TB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14, 10, 14, 10) };
        var pieG = new Grid();
        pieG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pieG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = TB("#757575"), FontSize = 11 };
        Grid.SetColumn(_lblInfo, 0); pieG.Children.Add(_lblInfo);

        var btnSp = new StackPanel { Orientation = Orientation.Horizontal };
        Button MkBtn(string ico, string txt, string bg) => new Button {
            Content = $"{ico}  {txt}", Height = 36, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0),
            Background = TB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };

        var btnBlqTodos  = MkBtn("🔒", "Bloquear todos",      "#7B241C");
        var btnBlq       = MkBtn("🔒", "Bloquear selección",  "#C0392B");
        var btnDesblq    = MkBtn("🔓", "Desbloquear selec.",  "#2E7D32");
        var btnDesblqTodos = MkBtn("🔓","Desbloquear todos",  "#1B5E20");
        var btnCerrar    = MkBtn("✖",  "Cerrar",              "#546E7A");

        btnBlqTodos.Click    += async (_, _) => await CambiarEstadoTodos(true);
        btnBlq.Click         += async (_, _) => await CambiarEstado(true);
        btnDesblq.Click      += async (_, _) => await CambiarEstado(false);
        btnDesblqTodos.Click += async (_, _) => await CambiarEstadoTodos(false);
        btnCerrar.Click      += (_, _) => Close();

        btnSp.Children.Add(btnBlqTodos); btnSp.Children.Add(btnBlq);
        btnSp.Children.Add(btnDesblq);  btnSp.Children.Add(btnDesblqTodos);
        btnSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnSp, 1); pieG.Children.Add(btnSp);
        pie.Child = pieG;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── DataGrid ──────────────────────────────────────────────────────
        var colHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdr.Setters.Add(new Setter(Control.BackgroundProperty, TB("#4E342E")));
        colHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdr.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        colHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
        colHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,2)));
        colHdr.Setters.Add(new Setter(Control.BorderBrushProperty, TB("#FFFFFF30")));

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, SelectionMode = DataGridSelectionMode.Extended,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = TB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = TB("#FBE9E7"),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = colHdr,
            FontSize = 12, RowHeight = 36,
            Margin = new Thickness(0) };

        DataGridTextColumn DGC(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var col = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left)
                col.ElementStyle = new System.Windows.Style(typeof(TextBlock)) {
                    Setters = { new Setter(TextBlock.TextAlignmentProperty, a) } };
            return col;
        }
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",
            Binding = new System.Windows.Data.Binding("Id"), Width = 55 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Remito",
            Binding = new System.Windows.Data.Binding("Numero"), Width = 100 });
        _grid.Columns.Add(DGC("Origen",      "Origen",     1.2));
        _grid.Columns.Add(DGC("Destino",     "Destino",    1.2));
        _grid.Columns.Add(new DataGridTextColumn { Header = "Total Costo",
            Binding = new System.Windows.Data.Binding("TotalCosto") { StringFormat = "N0" },
            Width = 110,
            ElementStyle = new System.Windows.Style(typeof(TextBlock)) {
                Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                            new Setter(TextBlock.PaddingProperty, new Thickness(0,0,8,0)) } } });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",
            Binding = new System.Windows.Data.Binding("Estado"), Width = 100 });

        // color de fila por estado
        var rowStyle = new System.Windows.Style(typeof(DataGridRow));
        var trigBlq = new DataTrigger { Binding = new System.Windows.Data.Binding("Estado"), Value = "Bloqueado" };
        trigBlq.Setters.Add(new Setter(DataGridRow.ForegroundProperty, TB("#B71C1C")));
        trigBlq.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        rowStyle.Triggers.Add(trigBlq);
        _grid.RowStyle = rowStyle;
        _grid.SelectionChanged += (_, _) => ActualizarInfo();

        var dgBorder = new Border { BorderBrush = TB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        dgBorder.Child = _grid;
        root.Children.Add(dgBorder);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.F5) _ = Cargar(); };
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            var rows = (await conn.QueryAsync<FilaRemito>(
                @"SELECT r.ID_REMITO_TMP        AS Id,
                         r.NUMERO_REM_TMP        AS Numero,
                         ISNULL(lo.NOMBRE, '')   AS Origen,
                         ISNULL(ld.NOMBRE, '')   AS Destino,
                         r.TOTALCOSTO            AS TotalCosto,
                         CASE r.ESTADO WHEN 0 THEN 'Pendiente' WHEN 3 THEN 'Bloqueado'
                             ELSE CAST(r.ESTADO AS NVARCHAR) END AS Estado
                  FROM CAB_REMITO_TMP r
                  LEFT JOIN LOCALES lo ON lo.ID_LOCAL = r.IDORIGENTMP
                  LEFT JOIN LOCALES ld ON ld.ID_LOCAL = r.IDDESTINOTMP
                  WHERE r.ESTADO IN (0, 3)
                  ORDER BY r.ESTADO ASC, r.ID_REMITO_TMP DESC")).ToList();

            _todos = rows;

            // poblar combo origen
            var origenActual = (_cboOrigen.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            _cboOrigen.Items.Clear();
            _cboOrigen.Items.Add(new ComboBoxItem { Content = "Todos los orígenes", Tag = "" });
            foreach (var o in rows.Select(r => r.Origen).Distinct().OrderBy(x => x))
                _cboOrigen.Items.Add(new ComboBoxItem { Content = o, Tag = o });
            _cboOrigen.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(origenActual)) {
                for (int i = 0; i < _cboOrigen.Items.Count; i++)
                    if ((_cboOrigen.Items[i] as ComboBoxItem)?.Tag?.ToString() == origenActual)
                    { _cboOrigen.SelectedIndex = i; break; }
            }

            AplicarFiltro();
        }
        catch (Exception ex) { MessageBox.Show($"Error al cargar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void AplicarFiltro()
    {
        var txt    = _txtBuscar.Text.Trim().ToUpperInvariant();
        var estado = (_cboEstado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODOS";
        var origen = (_cboOrigen.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        _filtros = _todos.Where(r =>
            (estado == "TODOS" || r.Estado == estado) &&
            (string.IsNullOrEmpty(origen) || r.Origen == origen) &&
            (string.IsNullOrEmpty(txt) ||
             r.Numero.ToUpper().Contains(txt) ||
             r.Origen.ToUpper().Contains(txt) ||
             r.Destino.ToUpper().Contains(txt) ||
             r.Id.ToString().Contains(txt))).ToList();

        _grid.ItemsSource = _filtros;

        // actualizar contadores
        _lblPend.Text  = _todos.Count(r => r.Estado == "Pendiente").ToString();
        _lblBloq.Text  = _todos.Count(r => r.Estado == "Bloqueado").ToString();
        _lblTotal.Text = _todos.Count.ToString();
        ActualizarInfo();
    }

    private void ActualizarInfo()
    {
        var sel = _grid.SelectedItems.Count;
        _lblInfo.Text = $"Mostrando {_filtros.Count} de {_todos.Count} remitos" +
            (sel > 0 ? $"  •  {sel} seleccionado(s)" : "");
    }

    private async Task CambiarEstado(bool bloquear)
    {
        if (_grid.SelectedItems.Count == 0) {
            MessageBox.Show("Seleccione al menos un remito.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (!await ValidarAdmin()) return;

        try
        {
            using var conn = _db.Create();
            byte nuevoEstado = bloquear ? (byte)3 : (byte)0;
            foreach (FilaRemito row in _grid.SelectedItems)
                await conn.ExecuteAsync(
                    "UPDATE CAB_REMITO_TMP SET ESTADO=@e WHERE ID_REMITO_TMP=@id",
                    new { e = nuevoEstado, id = row.Id });
            await Cargar();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task CambiarEstadoTodos(bool bloquear)
    {
        var accion = bloquear ? "BLOQUEAR TODOS" : "DESBLOQUEAR TODOS";
        var estadoActual = bloquear ? "Pendiente" : "Bloqueado";
        if (MessageBox.Show($"¿Está seguro que desea {accion} los remitos {estadoActual}s?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (!await ValidarAdmin()) return;

        try
        {
            using var conn = _db.Create();
            byte nuevoEstado = bloquear ? (byte)3 : (byte)0;
            byte estadoFiltro = bloquear ? (byte)0 : (byte)3;
            var afectados = await conn.ExecuteAsync(
                "UPDATE CAB_REMITO_TMP SET ESTADO=@e WHERE ESTADO=@f",
                new { e = nuevoEstado, f = estadoFiltro });
            await Cargar();
            MessageBox.Show($"Se {(bloquear ? "bloquearon" : "desbloquearon")} {afectados} remito(s) correctamente.",
                "Completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task<bool> ValidarAdmin()
    {
        var B = (string h) => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(h)!;

        var dlg = new Window {
            Title = "Autenticación",
            Width = 420, Height = 340,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = B("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = false,
            BorderBrush = B("#BDBDBD"),
            BorderThickness = new Thickness(1)
        };

        var root = new DockPanel { LastChildFill = true };

        // ── Franja superior de color ───────────────────────────────────────
        var top = new Border { Background = B("#5D4037"), Height = 6 };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);

        // ── Barra de título custom ─────────────────────────────────────────
        var titleBar = new Border { Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(20, 12, 12, 12) };
        var titleG = new Grid();
        titleG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleSp = new StackPanel { Orientation = Orientation.Horizontal };
        titleSp.Children.Add(new TextBlock { Text = "🔐", FontSize = 18, Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
        var titleTxt = new StackPanel();
        titleTxt.Children.Add(new TextBlock { Text = "Autenticación requerida",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = B("#212121") });
        titleTxt.Children.Add(new TextBlock { Text = "Solo administradores pueden continuar",
            FontSize = 11, Foreground = B("#757575") });
        titleSp.Children.Add(titleTxt);
        Grid.SetColumn(titleSp, 0); titleG.Children.Add(titleSp);
        var btnX = new Button { Content = "✕", Width = 32, Height = 32, FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = B("#757575"), Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top };
        btnX.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };
        Grid.SetColumn(btnX, 1); titleG.Children.Add(btnX);
        titleBar.Child = titleG;
        DockPanel.SetDock(titleBar, Dock.Top); root.Children.Add(titleBar);

        // ── Pie con botones ────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = B("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(20, 12, 20, 12) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCan = new Button { Content = "Cancelar", Width = 100, Height = 36,
            Background = B("#EEEEEE"), Foreground = B("#424242"),
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0,0,8,0) };
        btnCan.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };
        var btnOk = new Button { Content = "Ingresar", Width = 110, Height = 36,
            Background = B("#5D4037"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13 };
        pieSp.Children.Add(btnCan); pieSp.Children.Add(btnOk);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo: campos ─────────────────────────────────────────────────
        var card = new Border { Background = System.Windows.Media.Brushes.White,
            Margin = new Thickness(20, 12, 20, 12),
            BorderBrush = B("#E0E0E0"), BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 16, 20, 20) };
        var body = new StackPanel();

        // campo usuario
        body.Children.Add(new TextBlock { Text = "Usuario o código", FontSize = 11,
            FontWeight = FontWeights.SemiBold, Foreground = B("#616161"),
            Margin = new Thickness(0, 0, 0, 5) });
        var txtUsuario = new TextBox {
            Height = 38, Padding = new Thickness(10, 0, 10, 0),
            FontSize = 13, BorderThickness = new Thickness(1),
            BorderBrush = B("#BDBDBD"), Background = B("#FAFAFA"),
            VerticalContentAlignment = VerticalAlignment.Center };
        body.Children.Add(txtUsuario);

        // hint bajo el campo
        body.Children.Add(new TextBlock { Text = "Ingrese nombre de usuario o código",
            FontSize = 10, Foreground = B("#9E9E9E"), Margin = new Thickness(2, 3, 0, 14) });

        // campo contraseña
        body.Children.Add(new TextBlock { Text = "Contraseña", FontSize = 11,
            FontWeight = FontWeights.SemiBold, Foreground = B("#616161"),
            Margin = new Thickness(0, 0, 0, 5) });
        var pwdBox = new PasswordBox {
            Height = 38, Padding = new Thickness(10, 0, 10, 0),
            FontSize = 13, BorderThickness = new Thickness(1),
            BorderBrush = B("#BDBDBD"), Background = B("#FAFAFA") };
        body.Children.Add(pwdBox);

        // mensaje de error (oculto por defecto)
        var lblError = new Border { Background = B("#FFEBEE"), BorderBrush = B("#EF9A9A"),
            BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
        lblError.Child = new TextBlock { Text = "⚠ Usuario o contraseña incorrectos.",
            Foreground = B("#C62828"), FontSize = 11 };
        body.Children.Add(lblError);

        card.Child = body;
        root.Children.Add(card);
        dlg.Content = root;

        // focus highlight en campos
        txtUsuario.GotFocus  += (_, _) => txtUsuario.BorderBrush = B("#5D4037");
        txtUsuario.LostFocus += (_, _) => txtUsuario.BorderBrush = B("#BDBDBD");
        pwdBox.GotFocus      += (_, _) => pwdBox.BorderBrush     = B("#5D4037");
        pwdBox.LostFocus     += (_, _) => pwdBox.BorderBrush     = B("#BDBDBD");

        bool resultado = false;
        btnOk.Click += async (_, _) => {
            var usuario  = txtUsuario.Text.Trim();
            var password = pwdBox.Password;
            lblError.Visibility = Visibility.Collapsed;
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password)) {
                lblError.Visibility = Visibility.Visible;
                ((TextBlock)lblError.Child).Text = "⚠ Complete usuario y contraseña.";
                return;
            }
            try {
                using var conn = _db.Create();
                // acepta CODIGO_USUARIO o NOMBRE_USUARIO, debe ser ADMINISTRADOR
                var ok = await conn.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM USUARIOS
                      WHERE (CODIGO_USUARIO = @u OR NOMBRE_USUARIO = @u)
                        AND CONTRASEÑA_USUARIO = @p
                        AND UPPER(CARGO_USUARIO) = 'ADMINISTRADOR'",
                    new { u = usuario, p = password });
                if (ok > 0) {
                    resultado = true;
                    dlg.DialogResult = true;
                    dlg.Close();
                } else {
                    lblError.Visibility = Visibility.Visible;
                    ((TextBlock)lblError.Child).Text = "⚠ Usuario o contraseña incorrectos.";
                    pwdBox.Clear(); pwdBox.Focus();
                }
            } catch (Exception ex) {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        pwdBox.KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter)
                btnOk.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        };
        txtUsuario.KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter) pwdBox.Focus();
        };
        dlg.Loaded += (_, _) => txtUsuario.Focus();
        dlg.ShowDialog();
        return resultado;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  EDITAR CUOTA PAGADA
// ══════════════════════════════════════════════════════════════════════════════
internal class FilaRemito
{
    public int     Id        { get; set; }
    public string  Numero    { get; set; } = "";
    public string  Origen    { get; set; } = "";
    public string  Destino   { get; set; } = "";
    public decimal TotalCosto{ get; set; }
    public string  Estado    { get; set; } = "";
}

public class EditarCuotaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private TextBox    _txtCi        = null!;
    private TextBlock  _lblCliente   = null!;
    private DataGrid   _dgCuotas     = null!;
    private Grid       _panelInferior = null!;

    // panel edición
    private TextBlock  _lblCuotaInfo = null!;
    private TextBox    _txtMonto     = null!;
    private TextBox    _txtEntrega   = null!;
    private TextBox    _txtMora      = null!;
    private TextBox    _txtPunitorio = null!;
    private TextBox    _txtReajuste  = null!;
    private TextBox    _txtTotal     = null!;
    private DatePicker _dtVto        = null!;
    private DatePicker _dtFechaCob   = null!;
    private TextBox    _txtObs       = null!;
    private ComboBox   _cboEstado    = null!;

    // panel artículos
    private DataGrid   _dgArticulos  = null!;
    private TextBlock  _lblArtInfo   = null!;

    // Filtros — antes solo se podía buscar por C.I. de cliente, y toda la grilla quedaba
    // mezclada (varios créditos distintos, entrega + cuotas, cobradas + pendientes), difícil
    // de leer para un cliente con historial largo. Se filtran en memoria sobre _cuotas ya
    // cargadas (no vuelven a golpear la base), reaplicados automáticamente al cambiar cualquiera.
    private DatePicker _dpDesde     = null!, _dpHasta = null!;
    private TextBox    _txtNVenta   = null!;
    private ComboBox   _cboEstadoFiltro = null!;

    private int  _idCuotaSel      = 0;
    private int  _idCabSel        = 0;
    private bool _recalcSuspendido = false;
    private List<FilaCuota> _cuotas = new();

    private static System.Windows.Media.SolidColorBrush EB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public EditarCuotaWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = App.Services.GetRequiredService<ISessionService>();
        Title = "Edición de Cuotas Pagadas";
        Width = 1020; Height = 700; MinWidth = 860; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EB("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = EB("#BF360C"), Padding = new Thickness(18, 12, 18, 12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "EDICIÓN DE CUOTAS PAGADAS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock { Text = "⚠  Modifica datos de cuotas ya registradas — use con precaución",
            Foreground = EB("#FFCCBC"), FontSize = 11 });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie: botones ──────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16, 10, 16, 10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button PBtn(string t, string bg) => new Button { Content = t, Height = 36,
            Padding = new Thickness(20,0,20,0), Margin = new Thickness(8,0,0,0),
            Background = EB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var btnGuardar = PBtn("💾  Guardar cambios", "#2E7D32");
        var btnCerrar  = PBtn("✖  Cerrar", "#546E7A");
        btnGuardar.Click += async (_, _) => await Guardar();
        btnCerrar.Click  += (_, _) => Close();
        pieSp.Children.Add(btnGuardar); pieSp.Children.Add(btnCerrar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo vertical ───────────────────────────────────────────────
        var body = new DockPanel { Margin = new Thickness(12, 10, 12, 8) };
        root.Children.Add(body);

        // ── Barra búsqueda (top) ──────────────────────────────────────────
        var busqBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#FFCCBC"), BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0,0,0,8) };
        var busqG = new Grid();
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lCI = new TextBlock { Text = "C.I. del Cliente:", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = EB("#BF360C"), Margin = new Thickness(0,0,8,0) };
        _txtCi = new TextBox { Padding = new Thickness(8,5,8,5), BorderBrush = EB("#BDBDBD"),
            VerticalContentAlignment = VerticalAlignment.Center };
        var btnBuscar = new Button { Content = "🔍  Buscar", Height = 34,
            Padding = new Thickness(14,0,14,0), Background = EB("#BF360C"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(8,0,8,0) };
        var btnSelCliente = new Button { Content = "👥  Seleccionar cliente",
            Height = 34, Padding = new Thickness(14,0,14,0),
            Background = EB("#37474F"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        _lblCliente = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = EB("#1565C0"), FontWeight = FontWeights.SemiBold,
            FontSize = 12, Margin = new Thickness(16,0,0,0) };
        btnBuscar.Click     += async (_, _) => await BuscarCliente();
        btnSelCliente.Click += async (_, _) => await AbrirSelectorCliente();
        _txtCi.KeyDown      += async (_, e) => { if (e.Key == Key.Enter) await BuscarCliente(); };

        Grid.SetColumn(lCI, 0);           busqG.Children.Add(lCI);
        Grid.SetColumn(_txtCi, 1);        busqG.Children.Add(_txtCi);
        Grid.SetColumn(btnBuscar, 2);     busqG.Children.Add(btnBuscar);
        Grid.SetColumn(btnSelCliente, 3); busqG.Children.Add(btnSelCliente);
        Grid.SetColumn(_lblCliente, 4);   busqG.Children.Add(_lblCliente);
        busqBar.Child = busqG;
        DockPanel.SetDock(busqBar, Dock.Top); body.Children.Add(busqBar);

        // ── Barra de filtros (fecha vto., N° venta, estado) — acota la grilla de cuotas de
        // un cliente con historial largo, sin volver a golpear la base (filtra en memoria). ──
        var filtBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#FFCCBC"), BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0,0,0,8) };
        var filtSp = new StackPanel { Orientation = Orientation.Horizontal };
        void FLbl(string t) => filtSp.Children.Add(new TextBlock { Text = t,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0),
            FontWeight = FontWeights.SemiBold, Foreground = EB("#BF360C"), FontSize = 12 });
        Border DBox(DatePicker dp) => new Border { Background = EB("#FFF3F0"), BorderBrush = EB("#FFCCBC"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4,2,4,2), Child = dp };

        _dpDesde = new DatePicker { Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), Width = 110 };
        _dpHasta = new DatePicker { Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), Width = 110 };
        FLbl("Vto. desde:"); filtSp.Children.Add(DBox(_dpDesde));
        filtSp.Children.Add(new Border { Width = 10 });
        FLbl("hasta:"); filtSp.Children.Add(DBox(_dpHasta));
        filtSp.Children.Add(new Border { Width = 1, Background = EB("#FFCCBC"), Margin = new Thickness(14,2,14,2) });

        FLbl("N° Venta:");
        _txtNVenta = new TextBox { Width = 130, Padding = new Thickness(6,4,6,4),
            BorderBrush = EB("#FFCCBC"), VerticalContentAlignment = VerticalAlignment.Center };
        filtSp.Children.Add(_txtNVenta);
        filtSp.Children.Add(new Border { Width = 1, Background = EB("#FFCCBC"), Margin = new Thickness(14,2,14,2) });

        FLbl("Estado:");
        _cboEstadoFiltro = new ComboBox { Width = 150, Padding = new Thickness(6,4,6,4) };
        _cboEstadoFiltro.Items.Add(new ComboBoxItem { Content = "Todos", Tag = (byte?)null });
        _cboEstadoFiltro.Items.Add(new ComboBoxItem { Content = "Pendiente", Tag = (byte?)0 });
        _cboEstadoFiltro.Items.Add(new ComboBoxItem { Content = "Cobrado",   Tag = (byte?)1 });
        _cboEstadoFiltro.SelectedIndex = 0;
        filtSp.Children.Add(_cboEstadoFiltro);

        var btnLimpiarFiltros = new Button { Content = "✕ Limpiar filtros", Height = 30,
            Padding = new Thickness(10,0,10,0), Margin = new Thickness(14,0,0,0),
            Background = EB("#78909C"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11 };
        filtSp.Children.Add(btnLimpiarFiltros);

        // Auto-refresco al cambiar cualquier filtro — mismo criterio ya usado en otras
        // pantallas de Herramientas (Eliminar Venta a Crédito, Historial de Caja).
        _dpDesde.SelectedDateChanged     += (_, _) => AplicarFiltrosCuota();
        _dpHasta.SelectedDateChanged     += (_, _) => AplicarFiltrosCuota();
        _txtNVenta.TextChanged           += (_, _) => AplicarFiltrosCuota();
        _cboEstadoFiltro.SelectionChanged += (_, _) => AplicarFiltrosCuota();
        btnLimpiarFiltros.Click += (_, _) => {
            _dpDesde.SelectedDate = null; _dpHasta.SelectedDate = null;
            _txtNVenta.Text = ""; _cboEstadoFiltro.SelectedIndex = 0;
            AplicarFiltrosCuota();
        };

        filtBar.Child = filtSp;
        DockPanel.SetDock(filtBar, Dock.Top); body.Children.Add(filtBar);

        // ── Grid cuotas (medio, altura fija 220) ─────────────────────────
        Style ColHdrStyle(string bg) {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty, EB(bg)));
            s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
            return s;
        }

        _dgCuotas = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EB("#FBE9E7"),
            BorderThickness = new Thickness(1), BorderBrush = EB("#FFCCBC"),
            ColumnHeaderStyle = ColHdrStyle("#BF360C"), FontSize = 12, RowHeight = 30,
            Height = 200, Margin = new Thickness(0,0,0,8) };

        DataGridTextColumn DC(string h, string b, double w, string? fmt = null, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h,
                Binding = fmt != null ? new System.Windows.Data.Binding(b){StringFormat=fmt}
                                      : new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new System.Windows.Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty,a)}};
            return c;
        }
        _dgCuotas.Columns.Add(new DataGridTextColumn { Header="ID",    Binding=new System.Windows.Data.Binding("IdGen"),       Width=55 });
        _dgCuotas.Columns.Add(new DataGridTextColumn { Header="Comp.", Binding=new System.Windows.Data.Binding("Comprobante"), Width=150 });
        _dgCuotas.Columns.Add(DC("N°",       "NCuotaTexto",0.5, null, TextAlignment.Center));
        _dgCuotas.Columns.Add(DC("Monto",    "Monto",      1.0, "N0",  TextAlignment.Right));
        _dgCuotas.Columns.Add(DC("Entrega",  "Entrega",    0.9, "N0",  TextAlignment.Right));
        _dgCuotas.Columns.Add(DC("Punit.",   "Punitorio",  0.9, "N0",  TextAlignment.Right));
        _dgCuotas.Columns.Add(DC("Total",    "Total",      1.0, "N0",  TextAlignment.Right));
        _dgCuotas.Columns.Add(DC("Vto.",     "VtoStr",     0.9));
        _dgCuotas.Columns.Add(DC("Cobrado",  "FechaCobStr",0.9));

        var rs = new System.Windows.Style(typeof(DataGridRow));
        var tCob = new DataTrigger { Binding=new System.Windows.Data.Binding("Estado"), Value=(byte)1 };
        tCob.Setters.Add(new Setter(DataGridRow.BackgroundProperty, EB("#E8F5E9")));
        rs.Triggers.Add(tCob);
        _dgCuotas.RowStyle = rs;
        _dgCuotas.SelectionChanged += OnCuotaSelected;
        DockPanel.SetDock(_dgCuotas, Dock.Top); body.Children.Add(_dgCuotas);

        // ── Panel inferior: edición izq | artículos der ───────────────────
        _panelInferior = new Grid { Visibility = Visibility.Hidden };
        _panelInferior.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        _panelInferior.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        _panelInferior.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(_panelInferior);

        // ── Columna edición (izquierda) ───────────────────────────────────
        var editBorder = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#FFCCBC"), BorderThickness = new Thickness(1) };
        Grid.SetColumn(editBorder, 0); _panelInferior.Children.Add(editBorder);

        var editScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14, 10, 14, 10) };
        var editSp = new StackPanel();
        _lblCuotaInfo = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = EB("#BF360C"), Margin = new Thickness(0,0,0,10),
            TextWrapping = TextWrapping.Wrap };
        editSp.Children.Add(_lblCuotaInfo);

        void ERow(string lbl, UIElement ctrl, string? hint = null) {
            editSp.Children.Add(new TextBlock { Text = lbl, FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = EB("#616161"),
                Margin = new Thickness(0,6,0,2) });
            editSp.Children.Add(ctrl);
            if (hint != null) editSp.Children.Add(new TextBlock { Text = hint,
                FontSize = 9, Foreground = EB("#9E9E9E"), Margin = new Thickness(2,1,0,0) });
        }
        TextBox MkTxt() => new TextBox { Padding = new Thickness(7,4,7,4),
            BorderBrush = EB("#BDBDBD"), BorderThickness = new Thickness(1), Background = EB("#FAFAFA") };

        // 2 columnas para los campos numéricos
        var gridNum = new Grid();
        gridNum.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridNum.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        gridNum.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void GNum(UIElement left2, UIElement right2, int row) {
            gridNum.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(left2, row); Grid.SetColumn(left2, 0); gridNum.Children.Add(left2);
            Grid.SetRow(right2,row); Grid.SetColumn(right2, 2); gridNum.Children.Add(right2);
        }
        _txtMonto     = MkTxt(); _txtEntrega   = MkTxt();
        _txtMora      = MkTxt(); _txtPunitorio = MkTxt();
        _txtReajuste  = MkTxt();
        _txtTotal     = new TextBox { Padding = new Thickness(7,4,7,4), BorderBrush = EB("#BDBDBD"),
            BorderThickness = new Thickness(1), Background = EB("#EEEEEE"),
            Foreground = EB("#616161"), IsReadOnly = true };

        var lblM  = Lbl("Monto");     var lblE  = Lbl("Entrega");
        var lblMo = Lbl("Mora(días)");var lblPu = Lbl("Punitorio");
        var lblRe = Lbl("Reajuste");  var lblTo = Lbl("Total");
        TextBlock Lbl(string t) => new TextBlock { Text=t, FontSize=10, FontWeight=FontWeights.SemiBold,
            Foreground=EB("#616161"), Margin=new Thickness(0,6,0,2) };
        var spM = SP(lblM, _txtMonto);   var spE = SP(lblE, _txtEntrega);
        var spMo= SP(lblMo,_txtMora);    var spPu= SP(lblPu,_txtPunitorio);
        var spRe= SP(lblRe,_txtReajuste);var spTo= SP(lblTo,_txtTotal,
            new TextBlock{Text="= Monto + Punitorio + Reajuste", FontSize=9,
                Foreground=EB("#9E9E9E"),Margin=new Thickness(2,1,0,0)});
        StackPanel SP(params UIElement[] els) { var s=new StackPanel(); foreach(var el in els) s.Children.Add(el); return s; }

        GNum(spM, spE,   0);
        GNum(spMo,spPu,  1);
        GNum(spRe,spTo,  2);
        editSp.Children.Add(gridNum);

        _dtVto      = new DatePicker { Margin = new Thickness(0) }; ERow("Vencimiento",   _dtVto);
        _dtFechaCob = new DatePicker { Margin = new Thickness(0) }; ERow("Fecha cobrado", _dtFechaCob);
        // Solo 2 opciones — igual que el sistema viejo (Pendiente/Cancelado) y los únicos 2
        // valores reales que usa GENERADAS.ESTADO en la base (0/1). Antes había una tercera
        // opción "2 — Anulado" que no corresponde a ningún valor usado históricamente ni
        // interpretado por ninguna otra pantalla del sistema.
        _cboEstado  = new ComboBox   { Padding = new Thickness(6,3,6,3) };
        _cboEstado.Items.Add(new ComboBoxItem { Content="0 — Pendiente", Tag=(byte)0 });
        _cboEstado.Items.Add(new ComboBoxItem { Content="1 — Cobrado",   Tag=(byte)1 });
        ERow("Estado", _cboEstado);
        _txtObs = new TextBox { Padding=new Thickness(7,4,7,4), Height=48,
            AcceptsReturn=true, TextWrapping=TextWrapping.Wrap,
            BorderBrush=EB("#BDBDBD"), BorderThickness=new Thickness(1),
            Background=EB("#FAFAFA"), VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        ERow("Observación", _txtObs);

        editScroll.Content = editSp; editBorder.Child = editScroll;

        // ── Columna artículos (derecha) ───────────────────────────────────
        var artBorder = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#C8E6C9"), BorderThickness = new Thickness(1) };
        Grid.SetColumn(artBorder, 2); _panelInferior.Children.Add(artBorder);

        var artDp = new DockPanel();
        var artHdr = new Border { Background = EB("#2E7D32"),
            Padding = new Thickness(12, 8, 12, 8) };
        var artHdrSp = new StackPanel();
        artHdrSp.Children.Add(new TextBlock { Text = "ARTÍCULOS DE LA VENTA",
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
        _lblArtInfo = new TextBlock { Foreground = EB("#A5D6A7"), FontSize = 10 };
        artHdrSp.Children.Add(_lblArtInfo);
        artHdr.Child = artHdrSp;
        DockPanel.SetDock(artHdr, Dock.Top); artDp.Children.Add(artHdr);

        _dgArticulos = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EB("#E8F5E9"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EB("#F1F8E9"),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = ColHdrStyle("#388E3C"), FontSize = 12, RowHeight = 30 };

        DataGridTextColumn DA(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header=h, Binding=new System.Windows.Data.Binding(b),
                Width=new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty,a)}};
            return c;
        }
        _dgArticulos.Columns.Add(DA("Descripción", "Descripcion", 3.0));
        _dgArticulos.Columns.Add(DA("Cant.",        "Cantidad",    0.6, TextAlignment.Center));
        _dgArticulos.Columns.Add(DA("P.Venta",      "PvFmt",       1.0, TextAlignment.Right));
        _dgArticulos.Columns.Add(DA("Subtotal",     "SubtotalFmt", 1.1, TextAlignment.Right));
        artDp.Children.Add(_dgArticulos);
        artBorder.Child = artDp;

        // ── Separador visual entre paneles ────────────────────────────────
        var sep = new Border { Background = EB("#E0E0E0") };
        Grid.SetColumn(sep, 1); _panelInferior.Children.Add(sep);

        // ── Formato miles en tiempo real ──────────────────────────────────
        var culture = System.Globalization.CultureInfo.GetCultureInfo("es-PY");

        // quita todos los puntos de miles y devuelve el número como long string
        string Limpiar(string s) => s.Replace(".", "").Replace(",", "");

        void RegistrarMiles(TextBox tb) {
            bool _busy = false;
            tb.PreviewTextInput += (_, e) => { e.Handled = !char.IsDigit(e.Text, 0); };
            DataObject.AddPastingHandler(tb, (_, e) => {
                if (e.DataObject.GetDataPresent(DataFormats.Text)) {
                    var t = (string)e.DataObject.GetData(DataFormats.Text);
                    if (!Limpiar(t).All(char.IsDigit)) e.CancelCommand();
                } else e.CancelCommand();
            });
            tb.TextChanged += (_, _) => {
                if (_busy || _recalcSuspendido) return;
                _busy = true;
                var raw = Limpiar(tb.Text);
                if (long.TryParse(raw, out var v) && v > 0) {
                    var formatted = v.ToString("N0", culture);
                    if (tb.Text != formatted) {
                        // guardar posición del cursor relativa al final
                        int fromEnd = tb.Text.Length - tb.CaretIndex;
                        tb.Text = formatted;
                        // restaurar cursor al final relativo
                        int newPos = Math.Max(0, formatted.Length - fromEnd);
                        tb.CaretIndex = Math.Min(newPos, formatted.Length);
                    }
                } else if (raw.Length == 0) {
                    tb.Text = "";
                    tb.CaretIndex = 0;
                }
                _busy = false;
            };
        }
        foreach (var tb in new[]{ _txtMonto, _txtEntrega, _txtPunitorio, _txtReajuste })
            RegistrarMiles(tb);
        _txtMora.PreviewTextInput += (_, e) => { e.Handled = !char.IsDigit(e.Text, 0); };

        void Recalc(object? s, TextChangedEventArgs? e) {
            if (_recalcSuspendido) return;
            decimal V(TextBox tb) { long.TryParse(Limpiar(tb.Text), out var v); return v; }
            _txtTotal.Text = (V(_txtMonto) + V(_txtPunitorio) + V(_txtReajuste)).ToString("N0", culture);
        }
        _txtMonto.TextChanged     += Recalc;
        _txtPunitorio.TextChanged += Recalc;
        _txtReajuste.TextChanged  += Recalc;

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async Task AbrirSelectorCliente()
    {
        // cargar lista de clientes con cuotas
        List<ClienteConCuotas> lista;
        try {
            using var conn = _db.Create();
            lista = (await conn.QueryAsync<ClienteConCuotas>(
                @"SELECT C.CI_CLIENTE AS Ci, C.NOMBRE_CLIENTE AS Nombre,
                         COUNT(G.IDGENERADAS)               AS TotalCuotas,
                         SUM(CASE WHEN G.ESTADO=1 THEN 1 ELSE 0 END) AS Cobradas,
                         SUM(CASE WHEN G.ESTADO=0 THEN 1 ELSE 0 END) AS Pendientes
                  FROM CLIENTES C
                  INNER JOIN CABECERA_SALES CS ON CS.ID_CLIENTE = C.ID_CLIENTE
                  INNER JOIN GENERADAS G ON G.IDCAB = CS.IDCAB
                  GROUP BY C.CI_CLIENTE, C.NOMBRE_CLIENTE
                  ORDER BY TotalCuotas DESC")).ToList();
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar clientes: {ex.Message}"); return;
        }

        // ventana popup
        var pop = new Window {
            Title = "Seleccionar cliente", Width = 640, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.CanResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EB("#F4F6F8"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13
        };

        var root = new DockPanel();

        // header
        var hdr = new Border { Background = EB("#37474F"), Padding = new Thickness(14,10,14,10) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "Seleccionar cliente con cuotas",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock { Text = $"{lista.Count} clientes con cuotas registradas",
            Foreground = EB("#B0BEC5"), FontSize = 11 });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // barra filtro
        var filtroBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#E0E0E0"), BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(12,8,12,8) };
        var filtroG = new Grid();
        filtroG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filtroG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filtroG.Children.Add(new TextBlock { Text = "Buscar: ", VerticalAlignment = VerticalAlignment.Center,
            Foreground = EB("#616161"), Margin = new Thickness(0,0,8,0) });
        var txtFiltro = new TextBox { Padding = new Thickness(8,5,8,5), BorderBrush = EB("#BDBDBD") };
        Grid.SetColumn(txtFiltro, 1); filtroG.Children.Add(txtFiltro);
        filtroBar.Child = filtroG;
        DockPanel.SetDock(filtroBar, Dock.Top); root.Children.Add(filtroBar);

        // pie
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnSel = new Button { Content = "✔  Seleccionar", Height = 34,
            Padding = new Thickness(18,0,18,0), Background = EB("#BF360C"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0,0,8,0), IsEnabled = false };
        var btnCanc = new Button { Content = "Cancelar", Height = 34,
            Padding = new Thickness(14,0,14,0), Background = EB("#546E7A"),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnCanc.Click += (_, _) => pop.Close();
        pieSp.Children.Add(btnSel); pieSp.Children.Add(btnCanc);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // grid clientes
        var colHdr2 = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdr2.Setters.Add(new Setter(Control.BackgroundProperty, EB("#37474F")));
        colHdr2.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        colHdr2.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        colHdr2.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        colHdr2.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,6,8,6)));

        var dg = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = EB("#ECEFF1"),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = colHdr2, FontSize = 12, RowHeight = 34 };

        DataGridTextColumn DC2(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty,a)}};
            return c;
        }
        dg.Columns.Add(DC2("C.I.",       "Ci",         1.0));
        dg.Columns.Add(DC2("Nombre",     "Nombre",     2.5));
        dg.Columns.Add(DC2("Total",      "TotalCuotas",0.7, TextAlignment.Center));
        dg.Columns.Add(DC2("Cobradas",   "Cobradas",   0.7, TextAlignment.Center));
        dg.Columns.Add(DC2("Pendientes", "Pendientes", 0.8, TextAlignment.Center));

        dg.ItemsSource = lista;
        dg.SelectionChanged += (_, _) => btnSel.IsEnabled = dg.SelectedItem != null;

        // filtro en tiempo real
        txtFiltro.TextChanged += (_, _) => {
            var q = txtFiltro.Text.Trim().ToLower();
            dg.ItemsSource = string.IsNullOrEmpty(q)
                ? lista
                : lista.Where(x => x.Ci.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || x.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        };

        // doble clic = seleccionar directamente
        dg.MouseDoubleClick += (_, _) => { if (dg.SelectedItem != null) btnSel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        ClienteConCuotas? elegido = null;
        btnSel.Click += (_, _) => {
            elegido = dg.SelectedItem as ClienteConCuotas;
            pop.Close();
        };

        root.Children.Add(dg);
        pop.Content = root;
        pop.ShowDialog();

        if (elegido != null) {
            _txtCi.Text = elegido.Ci;
            await BuscarCliente();
        }
    }

    private async Task BuscarCliente()
    {
        var ci = _txtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci)) return;
        try
        {
            using var conn = _db.Create();
            var cliente = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT ID_CLIENTE, NOMBRE_CLIENTE AS NombreCompleto
                  FROM CLIENTES WHERE CI_CLIENTE = @ci", new { ci });
            if (cliente == null) {
                _lblCliente.Text = "Cliente no encontrado";
                _lblCliente.Foreground = EB("#C62828");
                _dgCuotas.ItemsSource = null;
                return;
            }
            _lblCliente.Text = ((string)cliente.NombreCompleto).Trim();
            _lblCliente.Foreground = EB("#1565C0");

            int idCliente = (int)cliente.ID_CLIENTE;
            _cuotas = (await conn.QueryAsync<FilaCuota>(
                @"SELECT G.IDGENERADAS AS IdGen, G.IDCAB, G.COMPROBANTE AS Comprobante,
                         G.NCUOTA, G.MONTO, G.VTO, G.FECHACOBRADO,
                         G.MORA, G.PUNITORIO, G.REAJUSTE, G.TOTAL, G.ENTREGA,
                         G.ESTADO, ISNULL(G.OBS,'') AS Obs
                  FROM GENERADAS G
                  INNER JOIN CABECERA_SALES CS ON CS.IDCAB = G.IDCAB
                  WHERE CS.ID_CLIENTE = @idCli
                  ORDER BY G.IDCAB DESC, G.NCUOTA ASC",
                new { idCli = idCliente })).ToList();

            AplicarFiltrosCuota();
            _panelInferior.Visibility = Visibility.Hidden;
            _dgArticulos.ItemsSource = null;
        }
        catch (Exception ex) {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Filtra _cuotas (ya cargadas en memoria para el cliente actual) por rango de vencimiento,
    // N° de venta/comprobante y estado — sin volver a consultar la base. Se reaplica solo con
    // cada cambio de filtro (ver suscripciones en BuildUI).
    private void AplicarFiltrosCuota()
    {
        if (_cuotas.Count == 0) { _dgCuotas.ItemsSource = null; return; }

        var lista = _cuotas.AsEnumerable();

        if (_dpDesde?.SelectedDate is DateTime desde)
            lista = lista.Where(c => c.Vto.Date >= desde.Date);
        if (_dpHasta?.SelectedDate is DateTime hasta)
            lista = lista.Where(c => c.Vto.Date <= hasta.Date);

        var nVenta = _txtNVenta?.Text?.Trim();
        if (!string.IsNullOrEmpty(nVenta))
            lista = lista.Where(c => (c.Comprobante ?? "").Contains(nVenta, StringComparison.OrdinalIgnoreCase));

        if ((_cboEstadoFiltro?.SelectedItem as ComboBoxItem)?.Tag is byte estadoFiltro)
            lista = lista.Where(c => c.Estado == estadoFiltro);

        _dgCuotas.ItemsSource = lista.ToList();
    }

    private async void OnCuotaSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_dgCuotas.SelectedItem is not FilaCuota c) return;
        _idCuotaSel = c.IdGen;
        _idCabSel   = c.IdCab;
        _lblCuotaInfo.Text = $"{(c.EsEntrega ? "Entrega" : $"Cuota N° {c.NCuotaTexto}")}  —  {c.Comprobante?.Trim()}  |  ID {c.IdGen}  —  Cab {c.IdCab}";

        var fmtCulture = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        void SetFmt(TextBox tb, decimal v) =>
            tb.Text = v == 0 ? "" : v.ToString("N0", fmtCulture);

        // suspender recalc mientras cargamos para evitar totales parciales
        _recalcSuspendido = true;
        SetFmt(_txtMonto,     c.Monto);
        SetFmt(_txtEntrega,   c.Entrega);
        _txtMora.Text = c.Mora == 0 ? "" : c.Mora.ToString();
        SetFmt(_txtPunitorio, c.Punitorio);
        SetFmt(_txtReajuste,  c.Reajuste);
        SetFmt(_txtTotal,     c.Total);
        _recalcSuspendido = false;
        _dtVto.SelectedDate      = c.Vto;
        _dtFechaCob.SelectedDate = c.FechaCobrado;
        _txtObs.Text = c.Obs;
        for (int i = 0; i < _cboEstado.Items.Count; i++)
            if ((_cboEstado.Items[i] as ComboBoxItem)?.Tag is byte b && b == c.Estado)
            { _cboEstado.SelectedIndex = i; break; }

        _panelInferior.Visibility = Visibility.Visible;

        // cargar artículos de la venta
        try {
            using var conn = _db.Create();
            var arts = (await conn.QueryAsync<FilaArticulo>(
                @"SELECT A.D AS Descripcion,
                         DS.CANTIDAD,
                         DS.PV,
                         DS.CANTIDAD * DS.PV AS Subtotal
                  FROM DETALLES_SALES DS
                  INNER JOIN ARTICULOS A ON A.ID = DS.IDART
                  WHERE DS.IDCAB = @idCab
                  ORDER BY DS.IDDET",
                new { idCab = c.IdCab })).ToList();
            _dgArticulos.ItemsSource = arts;
            _lblArtInfo.Text = $"Venta #{c.IdCab} — {arts.Count} artículo(s)";
        }
        catch (Exception ex) {
            _lblArtInfo.Text = $"Error al cargar artículos: {ex.Message}";
        }
    }

    private async Task Guardar()
    {
        if (_idCuotaSel == 0) {
            MessageBox.Show("Seleccione una cuota de la lista.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        decimal Parse(TextBox tb) { decimal.TryParse(tb.Text.Replace(".","").Replace(",",""), out var v); return v; }
        var nuevoMonto     = Parse(_txtMonto);
        var nuevoEntrega   = Parse(_txtEntrega);
        var nuevoMora      = Parse(_txtMora);
        var nuevoPunitorio = Parse(_txtPunitorio);
        var nuevoReajuste  = Parse(_txtReajuste);
        var nuevoTotal     = Parse(_txtTotal);
        var nuevoVto       = _dtVto.SelectedDate;
        var nuevoFechaCob  = _dtFechaCob.SelectedDate;
        var nuevoObs       = _txtObs.Text.Trim();
        var nuevoEstado    = (_cboEstado.SelectedItem as ComboBoxItem)?.Tag is byte b ? b : (byte)0;

        if (nuevoVto == null) { MessageBox.Show("Seleccione fecha de vencimiento.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        try {
            using var conn = _db.Create();

            // cargar valores originales
            var orig = await conn.QueryFirstOrDefaultAsync<FilaCuota>(
                @"SELECT IDGENERADAS AS IdGen, IDCAB, COMPROBANTE AS Comprobante,
                         NCUOTA, MONTO, ENTREGA, MORA, PUNITORIO, REAJUSTE, TOTAL,
                         VTO, FECHACOBRADO, ESTADO, ISNULL(OBS,'') AS Obs
                  FROM GENERADAS WHERE IDGENERADAS=@id", new { id = _idCuotaSel });
            if (orig == null) { MessageBox.Show("La cuota ya no existe en la BD."); return; }

            // Diferencia en total, para el ajuste de caja — solo tiene sentido en dos casos:
            // 1) la cuota YA estaba cobrada (orig.Estado==1) y cambia el monto — ajuste por la
            //    diferencia real contra lo que ya había en caja.
            // 2) la cuota estaba Pendiente y pasa a Cobrado por primera vez desde acá (corrección
            //    de un cobro que no quedó bien registrado en su momento) — ahí se acredita el
            //    nuevoTotal completo, no una diferencia (orig.Total es 0 para una pendiente).
            // Las cuotas pendientes normalmente tienen TOTAL=0 en la base (el sistema legado solo
            // lo calcula/graba al momento del cobro real, ver Cuota.EstaPendiente) — sin esta
            // distinción, cualquier valor que un admin dejara en el campo Total de una cuota que
            // SIGUE pendiente generaría un movimiento de caja FALSO (dinero que nunca entró).
            var pasaAPendienteCobrado = orig.Estado == 0 && nuevoEstado == 1;
            var difTotal =
                orig.Estado == 1 ? nuevoTotal - orig.Total :
                pasaAPendienteCobrado ? nuevoTotal :
                0m;

            // buscar caja abierta del local actual
            var idLocal  = (byte)(_session.LocalActual?.IdLocal ?? 1);
            var idUsuario= _session.UsuarioActual?.IdUsuario ?? 1;
            var cajaMaster = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT ID_MASTER FROM CAJA_MASTER WHERE ID_LOCAL=@l AND ESTADO='A'",
                new { l = idLocal });

            // nombres de estado para el diálogo
            Func<byte,string>    estNom = e => e switch { 1=>"Cobrado", _=>"Pendiente" };
            Func<decimal,string> fmt    = v => v.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));

            // construir diálogo de confirmación detallado
            bool confirmo = await MostrarDialogoConfirmacion(orig, nuevoMonto, nuevoEntrega,
                nuevoMora, nuevoPunitorio, nuevoReajuste, nuevoTotal, nuevoVto.Value,
                nuevoFechaCob, nuevoEstado, nuevoObs, difTotal, cajaMaster != null, estNom, fmt);
            if (!confirmo) return;

            // Segunda confirmación con contraseña de administrador — mismo criterio que
            // "Anular venta a crédito" y "Eliminar y replantear cuota": esta pantalla modifica
            // datos de cuotas ya cobradas y puede generar movimientos de caja, no es una edición
            // trivial que cualquier cajero debería poder hacer sin supervisión.
            if (!await AdminValidacionHelper.PedirContrasenaAdmin(this, _db)) return;

            // 1) actualizar cuota
            await conn.ExecuteAsync(
                @"UPDATE GENERADAS SET
                    MONTO=@m, ENTREGA=@e, MORA=@mo, PUNITORIO=@pu, REAJUSTE=@re,
                    TOTAL=@tot, VTO=@v, FECHACOBRADO=@fc, ESTADO=@est, OBS=@obs
                  WHERE IDGENERADAS=@id",
                new { m=nuevoMonto, e=nuevoEntrega, mo=(int)nuevoMora, pu=nuevoPunitorio,
                      re=nuevoReajuste, tot=nuevoTotal, v=nuevoVto.Value,
                      fc=nuevoFechaCob, est=nuevoEstado, obs=nuevoObs, id=_idCuotaSel });

            // 2) ajuste automático de caja si hay diferencia y hay caja abierta
            if (difTotal != 0 && cajaMaster != null)
            {
                int idMaster = (int)cajaMaster.ID_MASTER;
                bool esIngreso = difTotal > 0;
                string tipo    = esIngreso ? "I" : "E";
                string subtipo = "AJUSTE";
                decimal montoAbs = Math.Abs(difTotal);
                string concepto  = $"AJUSTE EDICIÓN CUOTA #{_idCuotaSel} — {orig.Comprobante?.Trim()} C{orig.NCuotaTexto} — {(esIngreso ? "INCREMENTO" : "REDUCCIÓN")} {fmt(montoAbs)}";
                await conn.ExecuteAsync(
                    @"INSERT INTO CAJA_DETALLE
                        (ID_MASTER,ID_LOCAL,FECHA_HORA,TIPO,SUBTIPO,FORMA_PAGO,
                         MONTO,ID_CAJERO,CONCEPTO,REFERENCIA,ESTADO_REG)
                      VALUES(@m,@l,GETDATE(),@ti,@su,'EFECTIVO',@mo,@idU,@con,@referen,'V')",
                    new { m=idMaster, l=idLocal, ti=tipo, su=subtipo,
                          mo=montoAbs, idU=idUsuario, con=concepto,
                          referen=$"IDGEN:{_idCuotaSel}" });
            }

            MessageBox.Show(
                difTotal != 0 && cajaMaster != null
                    ? $"Cuota actualizada.\nSe registró ajuste de caja por {(difTotal>0?"+":"")}{fmt(difTotal)} Gs."
                    : difTotal != 0
                        ? "Cuota actualizada.\n⚠ No hay caja abierta — el ajuste de caja NO se registró automáticamente."
                        : "Cuota actualizada. No se generó movimiento de caja (la cuota sigue pendiente, o no hubo cambio en una cuota ya cobrada).",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            await BuscarCliente();
        }
        catch (Exception ex) {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task<bool> MostrarDialogoConfirmacion(
        FilaCuota orig, decimal nuevoMonto, decimal nuevoEntrega, decimal nuevoMora,
        decimal nuevoPunitorio, decimal nuevoReajuste, decimal nuevoTotal,
        DateTime nuevoVto, DateTime? nuevoFechaCob, byte nuevoEstado, string nuevoObs,
        decimal difTotal, bool cajAbierta,
        Func<byte,string> estNom, Func<decimal,string> fmt)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dlg = new Window {
            Title = "Confirmar cambios", Width = 560, Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EB("#F4F6F8"), FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13
        };

        var root = new DockPanel();

        // header
        var hdr = new Border { Background = EB("#BF360C"), Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "Confirmar modificación de cuota",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock {
            Text = $"Cuota #{orig.IdGen} — Comprobante {orig.Comprobante?.Trim()} — N° {orig.NCuotaTexto}",
            Foreground = EB("#FFCCBC"), FontSize = 11 });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // botones pie
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16,10,16,10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnSi = new Button { Content = "✔  Sí, guardar cambios", Height = 36,
            Padding = new Thickness(20,0,20,0), Background = EB("#2E7D32"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0,0,8,0) };
        var btnNo = new Button { Content = "✖  Cancelar", Height = 36,
            Padding = new Thickness(16,0,16,0), Background = EB("#546E7A"),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { tcs.SetResult(true);  dlg.Close(); };
        btnNo.Click += (_, _) => { tcs.SetResult(false); dlg.Close(); };
        dlg.Closed  += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult(false); };
        pieSp.Children.Add(btnSi); pieSp.Children.Add(btnNo);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // contenido scrollable
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(16,12,16,12) };

        void Seccion(string titulo) {
            body.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.Bold,
                FontSize = 12, Foreground = EB("#BF360C"), Margin = new Thickness(0,10,0,4) });
            body.Children.Add(new Border { Height = 1, Background = EB("#FFCCBC"), Margin = new Thickness(0,0,0,6) });
        }

        void Fila(string campo, string antes, string despues, bool cambio = false) {
            var g = new Grid { Margin = new Thickness(0,2,0,2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lblCampo = new TextBlock { Text = campo+":", Foreground = EB("#616161"), FontSize = 11 };
            var lblAntes = new TextBlock { Text = antes, FontSize = 11,
                Foreground = cambio ? EB("#C62828") : EB("#424242"),
                TextDecorations = cambio ? TextDecorations.Strikethrough : null };
            var arrow = new TextBlock { Text = "→", HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = EB("#9E9E9E"), FontSize = 11 };
            var lblDes = new TextBlock { Text = despues, FontSize = 11,
                Foreground = cambio ? EB("#1B5E20") : EB("#424242"),
                FontWeight = cambio ? FontWeights.SemiBold : FontWeights.Normal };
            Grid.SetColumn(lblCampo, 0); g.Children.Add(lblCampo);
            Grid.SetColumn(lblAntes, 1); g.Children.Add(lblAntes);
            Grid.SetColumn(arrow,    2); g.Children.Add(arrow);
            Grid.SetColumn(lblDes,   3); g.Children.Add(lblDes);
            body.Children.Add(g);
        }

        Seccion("CAMBIOS EN LA CUOTA");
        bool cambioMonto     = orig.Monto     != nuevoMonto;
        bool cambioEntrega   = orig.Entrega   != nuevoEntrega;
        bool cambioMora      = orig.Mora      != nuevoMora;
        bool cambioPun       = orig.Punitorio != nuevoPunitorio;
        bool cambioReaj      = orig.Reajuste  != nuevoReajuste;
        bool cambioTotal     = orig.Total     != nuevoTotal;
        bool cambioVto       = orig.Vto.Date  != nuevoVto.Date;
        bool cambioFechaCob  = orig.FechaCobrado?.Date != nuevoFechaCob?.Date;
        bool cambioEstado    = orig.Estado    != nuevoEstado;
        bool cambioObs       = orig.Obs       != nuevoObs;

        Fila("Monto",       fmt(orig.Monto),     fmt(nuevoMonto),     cambioMonto);
        Fila("Entrega",     fmt(orig.Entrega),   fmt(nuevoEntrega),   cambioEntrega);
        Fila("Mora (días)", orig.Mora.ToString(), nuevoMora.ToString(),cambioMora);
        Fila("Punitorio",   fmt(orig.Punitorio), fmt(nuevoPunitorio), cambioPun);
        Fila("Reajuste",    fmt(orig.Reajuste),  fmt(nuevoReajuste),  cambioReaj);
        Fila("TOTAL",       fmt(orig.Total)+" Gs", fmt(nuevoTotal)+" Gs", cambioTotal);
        Fila("Vencimiento", orig.Vto.ToString("dd/MM/yyyy"), nuevoVto.ToString("dd/MM/yyyy"), cambioVto);
        Fila("Fecha cobrado",
            orig.FechaCobrado?.ToString("dd/MM/yyyy") ?? "(sin fecha)",
            nuevoFechaCob?.ToString("dd/MM/yyyy") ?? "(sin fecha)", cambioFechaCob);
        Fila("Estado",      estNom(orig.Estado), estNom(nuevoEstado), cambioEstado);
        Fila("Observación", string.IsNullOrEmpty(orig.Obs)?"(vacío)":orig.Obs,
                            string.IsNullOrEmpty(nuevoObs)?"(vacío)":nuevoObs, cambioObs);

        // bloque ajuste de caja
        Seccion("AJUSTE AUTOMÁTICO DE CAJA");
        if (difTotal == 0) {
            body.Children.Add(new TextBlock { Text = "✓  Sin cambio en el TOTAL — no se generará movimiento de caja.",
                Foreground = EB("#388E3C"), FontSize = 11 });
        } else {
            var cajaBorder = new Border { Padding = new Thickness(12,8,12,8),
                Margin = new Thickness(0,0,0,4),
                Background = difTotal > 0 ? EB("#E8F5E9") : EB("#FFF3E0"),
                BorderBrush = difTotal > 0 ? EB("#66BB6A") : EB("#FFA726"),
                BorderThickness = new Thickness(1) };
            var cajaSp = new StackPanel();
            cajaSp.Children.Add(new TextBlock {
                Text = difTotal > 0
                    ? $"⬆  INGRESO en caja:  +{fmt(difTotal)} Gs  (el total subió)"
                    : $"⬇  EGRESO en caja:  -{fmt(Math.Abs(difTotal))} Gs  (el total bajó)",
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = difTotal > 0 ? EB("#1B5E20") : EB("#E65100") });
            cajaSp.Children.Add(new TextBlock {
                Text = cajAbierta
                    ? $"  Tipo: AJUSTE — Concepto: AJUSTE EDICIÓN CUOTA #{orig.IdGen}\n  Forma de pago: EFECTIVO — Caja: ABIERTA ✓"
                    : "⚠  No hay caja abierta en este local — el ajuste NO se registrará.",
                FontSize = 10, Foreground = cajAbierta ? EB("#424242") : EB("#C62828"),
                Margin = new Thickness(0,4,0,0) });
            cajaBorder.Child = cajaSp;
            body.Children.Add(cajaBorder);
        }

        // advertencia general
        body.Children.Add(new Border { Background = EB("#FFF8E1"),
            BorderBrush = EB("#FFD54F"), BorderThickness = new Thickness(1),
            Padding = new Thickness(10,6,10,6), Margin = new Thickness(0,10,0,0),
            Child = new TextBlock {
                Text = "⚠  Si modificó el monto o el total, recuerde verificar el saldo\n    del cliente en el sistema.",
                FontSize = 11, Foreground = EB("#5D4037"), TextWrapping = TextWrapping.Wrap }});

        sv.Content = body;
        root.Children.Add(sv);
        dlg.Content = root;
        dlg.ShowDialog();
        return tcs.Task;
    }
}

internal class FilaArticulo
{
    public string  Descripcion  { get; set; } = "";
    public decimal Cantidad     { get; set; }
    public decimal Pv           { get; set; }
    public decimal Subtotal     { get; set; }
    private static readonly System.Globalization.CultureInfo _py =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");
    public string PvFmt       => Pv.ToString("N0", _py);
    public string SubtotalFmt => Subtotal.ToString("N0", _py);
}

internal class ClienteConCuotas
{
    public string Ci          { get; set; } = "";
    public string Nombre      { get; set; } = "";
    public int    TotalCuotas { get; set; }
    public int    Cobradas    { get; set; }
    public int    Pendientes  { get; set; }
}

internal class FilaCuota
{
    public int      IdGen       { get; set; }
    public int      IdCab       { get; set; }
    public string   Comprobante { get; set; } = "";
    public byte     NCuota      { get; set; }
    public decimal  Monto       { get; set; }
    public decimal  Entrega     { get; set; }
    public decimal  Mora        { get; set; }
    public decimal  Punitorio   { get; set; }
    public decimal  Reajuste    { get; set; }
    public decimal  Total       { get; set; }
    public DateTime Vto         { get; set; }
    public DateTime? FechaCobrado{ get; set; }
    public byte     Estado      { get; set; }
    public string   Obs         { get; set; } = "";
    public string   VtoStr      => Vto.ToString("dd/MM/yyyy");
    public string   FechaCobStr => FechaCobrado?.ToString("dd/MM/yyyy") ?? "";
    // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial de la venta, no una cuota real —
    // ver comentario en Cuota.NCuotaVisible.
    public bool     EsEntrega   => NCuota == 1;
    public string   NCuotaTexto => EsEntrega ? "Entrega" : (NCuota - 1).ToString();
}

// ══════════════════════════════════════════════════════════════════════════════
//  ELIMINAR PAGO GENERADO
// ══════════════════════════════════════════════════════════════════════════════
public class EliminarPagoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtIdCuota = null!;

    public EliminarPagoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Eliminar Pago Generado"; Width = 400; Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#922B21")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12)
        };
        hdr.Child = new TextBlock { Text = "Eliminar Pago Generado", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);
        root.Children.Add(new TextBlock { Text = "⚠ Acción irreversible.",
            Foreground = System.Windows.Media.Brushes.Red, Margin = new Thickness(0, 0, 0, 8), FontSize = 11 });
        root.Children.Add(new TextBlock { Text = "ID Cuota generada (IDGENERADAS):",
            Margin = new Thickness(0, 0, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtIdCuota = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(_txtIdCuota);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnE = new Button { Content = "Eliminar", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#922B21")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnE.Click += async (_, _) => await Eliminar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnE); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Eliminar()
    {
        if (!int.TryParse(_txtIdCuota.Text.Trim(), out var idCuota)) { MessageBox.Show("ID inválido."); return; }
        if (MessageBox.Show($"¿Eliminar la cuota generada ID {idCuota}?", "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            await conn.ExecuteAsync("DELETE FROM GENERADAS WHERE IDGENERADAS=@id", new { id = idCuota });
            MessageBox.Show("Pago eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al eliminar pago: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ACTUALIZAR CAJAS CERRADAS (solo administrador)
// ══════════════════════════════════════════════════════════════════════════════
// Permite corregir una caja YA CERRADA (arqueada) sin reabrirla: agregar/editar/anular
// movimientos de ingreso y egreso, y ajustar el monto de cierre real. Se edita CAJA_DETALLE
// directamente dejando ESTADO='C' intacto en todo momento — reabrir la caja (volver a 'A')
// no existe como flujo en el sistema y rompería otras pantallas que asumen una sola caja
// abierta por local a la vez (ver investigación previa). Tras cada cambio se recalculan
// TOT_INGRESOS/TOT_EGRESOS de CAJA_MASTER sumando los CAJA_DETALLE vigentes (ESTADO_REG='V') —
// no existe un SP reutilizable para este recálculo puntual, sp_CerrarCaja_CS espera un flujo
// de cierre completo, no vive en este repo.
public class ActualizarCajasCerradasWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private Button     _btnLocal = null!;
    private TextBlock  _lblLocalNombre = null!;
    private DataGrid   _dgCajas = null!, _dgMovs = null!;
    private TextBlock  _lblInfoCaja = null!, _lblTotales = null!;
    private TextBox    _txtMontoCierreReal = null!;
    private Button     _btnGuardarCierre = null!, _btnNuevoIngreso = null!, _btnNuevoEgreso = null!,
                        _btnEditarMov = null!, _btnAnularMov = null!;
    private ComboBox   _cboEstadoMov = null!;

    private List<FilaCajaCerrada> _cajas = new();
    private FilaCajaCerrada?      _cajaSel;
    private List<FilaExploradorCaja> _movs = new();
    private byte  _localSelId  = 0;
    private string _localSelNom = "Todos los locales";
    private bool  _autorizado  = false;

    private static System.Windows.Media.SolidColorBrush EBC(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public ActualizarCajasCerradasWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = App.Services.GetRequiredService<ISessionService>();
        Title    = "Actualizar Cajas Cerradas";
        Width = 1180; Height = 850; MinWidth = 980; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EBC("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;
        BuildUI();
        // No se puede asignar Owner (dentro de PedirContrasenaAdmin) a una ventana hija ANTES
        // de que esta ventana se muestre — WPF lo rechaza. Se dispara recién en Loaded, cuando
        // ya está visible, en vez de desde el constructor.
        Loaded += async (_, _) => await InicializarAsync();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var hdr = new Border { Background = EBC("#4A148C"), Padding = new Thickness(18, 11, 18, 11) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "ACTUALIZAR CAJAS CERRADAS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock { Text = "⚠  Solo administrador — modifica arqueos ya cerrados. Los cambios no reabren la caja.",
            Foreground = EBC("#E1BEE7"), FontSize = 11 });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Filtros ───────────────────────────────────────────────────────
        var filtBar = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#CE93D8"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(10, 8, 10, 6) };
        var filtSp = new StackPanel { Orientation = Orientation.Horizontal };

        void FLabel(string t) => filtSp.Children.Add(new TextBlock { Text = t,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold,
            Foreground = EBC("#4A148C"), FontSize = 12 });

        Border DateBox(DatePicker dp) {
            var b = new Border { Background = EBC("#F5F0FF"), BorderBrush = EBC("#CE93D8"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2) };
            b.Child = dp; return b;
        }

        _dpDesde = new DatePicker { SelectedDate = DateTime.Today.AddDays(-7),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        _dpHasta = new DatePicker { SelectedDate = DateTime.Today,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0) };

        FLabel("Desde:"); filtSp.Children.Add(DateBox(_dpDesde));
        filtSp.Children.Add(new Border { Width = 14 });
        FLabel("Hasta:"); filtSp.Children.Add(DateBox(_dpHasta));
        filtSp.Children.Add(new Border { Width = 1, Background = EBC("#CE93D8"), Margin = new Thickness(16,2,16,2) });

        _lblLocalNombre = new TextBlock {
            Text = "Todos los locales", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White, FontSize = 12 };
        var chevron = new TextBlock { Text = " ▾", VerticalAlignment = VerticalAlignment.Center,
            Foreground = EBC("#E1BEE7"), FontSize = 11 };
        var btnLocalContent = new StackPanel { Orientation = Orientation.Horizontal };
        btnLocalContent.Children.Add(new TextBlock { Text = "📍 ", VerticalAlignment = VerticalAlignment.Center });
        btnLocalContent.Children.Add(_lblLocalNombre);
        btnLocalContent.Children.Add(chevron);
        _btnLocal = new Button {
            Content = btnLocalContent, Height = 34, Padding = new Thickness(14,0,12,0),
            Background = EBC("#4A148C"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12 };
        _btnLocal.Click += (_, _) => AbrirSelectorLocal();
        filtSp.Children.Add(_btnLocal);
        filtSp.Children.Add(new Border { Width = 14 });

        var buscarContent = new StackPanel { Orientation = Orientation.Horizontal };
        buscarContent.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
        buscarContent.Children.Add(new TextBlock { Text = "Buscar", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
        var btnBuscar = new Button {
            Content = buscarContent, Height = 34, Padding = new Thickness(16,0,16,0),
            Background = EBC("#6A1B9A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnBuscar.Click += async (_, _) => await CargarCajas();
        filtSp.Children.Add(btnBuscar);

        // Auto-refresco al cambiar fechas o local — mismo criterio ya usado en Eliminar Venta
        // a Crédito / Historial de Caja.
        _dpDesde.SelectedDateChanged += async (_, _) => await CargarCajas();
        _dpHasta.SelectedDateChanged += async (_, _) => await CargarCajas();

        filtBar.Child = filtSp;
        DockPanel.SetDock(filtBar, Dock.Top); root.Children.Add(filtBar);

        // ── Cuerpo: cajas cerradas (arriba, más chico) | movimientos + edición (abajo, más
        // alto) — la grilla de movimientos es la que más se usa una vez elegida la caja, así
        // que necesita más espacio visible que el listado de cajas de arriba.
        var body = new Grid { Margin = new Thickness(10,0,10,10) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.8, GridUnitType.Star) });
        root.Children.Add(body);

        Style ColHdrS(string bg) {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty, EBC(bg)));
            s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
            return s;
        }
        DataGridTextColumn DCV(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var c = new DataGridTextColumn { Header = h, Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left) c.ElementStyle = new Style(typeof(TextBlock)){
                Setters={new Setter(TextBlock.TextAlignmentProperty,a)}};
            return c;
        }

        // Grilla de cajas cerradas
        _dgCajas = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = EBC("#CE93D8"),
            ColumnHeaderStyle = ColHdrS("#4A148C"), FontSize = 12, RowHeight = 30 };
        _dgCajas.Columns.Add(DCV("N° Caja",     "IdMaster",       0.6, TextAlignment.Center));
        _dgCajas.Columns.Add(DCV("Local",       "LocalNombre",    1.3));
        _dgCajas.Columns.Add(DCV("Apertura",    "FechaAperturaStr", 1.0));
        _dgCajas.Columns.Add(DCV("Cierre",      "FechaCierreStr",   1.0));
        _dgCajas.Columns.Add(DCV("Cerrado por", "UsuarioCierreNombre", 1.1));
        _dgCajas.Columns.Add(DCV("Ingresos",    "TotIngresosFmt", 0.9, TextAlignment.Right));
        _dgCajas.Columns.Add(DCV("Egresos",     "TotEgresosFmt",  0.9, TextAlignment.Right));
        _dgCajas.Columns.Add(DCV("Cierre real", "MontoCierreRealFmt", 0.9, TextAlignment.Right));
        _dgCajas.SelectionChanged += OnCajaSelectionChanged;
        Grid.SetRow(_dgCajas, 0); body.Children.Add(_dgCajas);

        // Panel inferior: movimientos (izq, ancho) + edición de cierre (der)
        var abajoGrid = new Grid();
        abajoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
        abajoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        abajoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(abajoGrid, 2); body.Children.Add(abajoGrid);

        var movsStack = new DockPanel();
        _lblInfoCaja = new TextBlock { Text = "Seleccione una caja cerrada arriba para ver sus movimientos.",
            FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = EBC("#4A148C"),
            Margin = new Thickness(2,0,0,4) };
        DockPanel.SetDock(_lblInfoCaja, Dock.Top); movsStack.Children.Add(_lblInfoCaja);

        var movBtnBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,4) };
        Button MBtn(string t, string bg) => new Button { Content = t, Height = 30,
            Padding = new Thickness(10,0,10,0), Margin = new Thickness(0,0,6,0),
            Background = EBC(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 11.5, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, IsEnabled = false };
        _btnNuevoIngreso = MBtn("➕ Nuevo ingreso", "#2E7D32");
        _btnNuevoEgreso  = MBtn("➖ Nuevo egreso",  "#C62828");
        _btnEditarMov    = MBtn("✎ Editar",        "#1565C0");
        _btnAnularMov    = MBtn("🚫 Anular",        "#EF6C00");
        _btnNuevoIngreso.Click += async (_, _) => await NuevoMovimiento(esIngreso: true);
        _btnNuevoEgreso.Click  += async (_, _) => await NuevoMovimiento(esIngreso: false);
        _btnEditarMov.Click    += async (_, _) => await EditarMovimientoSeleccionado();
        _btnAnularMov.Click    += async (_, _) => await AnularMovimientoSeleccionado();
        movBtnBar.Children.Add(_btnNuevoIngreso); movBtnBar.Children.Add(_btnNuevoEgreso);
        movBtnBar.Children.Add(_btnEditarMov);    movBtnBar.Children.Add(_btnAnularMov);

        // Filtro por estado (Todos/Vigentes/Anulados) — filtra en memoria sobre _movs ya
        // cargados, sin volver a golpear la base. Antes no había forma de separar los
        // movimientos anulados de los vigentes en la grilla, mezclándose todos juntos.
        movBtnBar.Children.Add(new Border { Width = 14 });
        movBtnBar.Children.Add(new TextBlock { Text = "Estado:", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,6,0), FontWeight = FontWeights.SemiBold, Foreground = EBC("#4A148C"), FontSize = 11.5 });
        _cboEstadoMov = new ComboBox { Width = 130, Height = 30, Padding = new Thickness(6,3,6,3) };
        _cboEstadoMov.Items.Add(new ComboBoxItem { Content = "Todos" });
        _cboEstadoMov.Items.Add(new ComboBoxItem { Content = "Vigentes" });
        _cboEstadoMov.Items.Add(new ComboBoxItem { Content = "Anulados" });
        _cboEstadoMov.SelectedIndex = 0;
        _cboEstadoMov.SelectionChanged += (_, _) => AplicarFiltroMovs();
        movBtnBar.Children.Add(_cboEstadoMov);
        DockPanel.SetDock(movBtnBar, Dock.Top); movsStack.Children.Add(movBtnBar);

        _lblTotales = new TextBlock { FontSize = 11.5, Foreground = EBC("#616161"), Margin = new Thickness(0,0,0,4) };
        DockPanel.SetDock(_lblTotales, Dock.Bottom); movsStack.Children.Add(_lblTotales);

        _dgMovs = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = EBC("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = EBC("#CE93D8"),
            ColumnHeaderStyle = ColHdrS("#6A1B9A"), FontSize = 12, RowHeight = 30 };
        _dgMovs.Columns.Add(DCV("Hora",     "FechaHoraStr", 1.1));
        _dgMovs.Columns.Add(DCV("Tipo",     "TipoDesc",     0.6, TextAlignment.Center));
        _dgMovs.Columns.Add(DCV("Subtipo",  "SubTipo",      0.8));
        _dgMovs.Columns.Add(new DataGridTextColumn { Header = "Monto",
            Binding = new System.Windows.Data.Binding("Monto") { StringFormat = "N0" },
            Width = new DataGridLength(0.7, DataGridLengthUnitType.Star),
            ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } } });
        _dgMovs.Columns.Add(DCV("Concepto", "Concepto",     1.6));
        _dgMovs.Columns.Add(DCV("Estado",   "EstadoDesc",   0.7, TextAlignment.Center));
        var rs = new Style(typeof(DataGridRow));
        var dtAnulado = new DataTrigger { Binding = new System.Windows.Data.Binding("Anulado"), Value = true };
        dtAnulado.Setters.Add(new Setter(DataGridRow.BackgroundProperty, EBC("#FAFAFA")));
        dtAnulado.Setters.Add(new Setter(DataGridRow.ForegroundProperty, EBC("#BDBDBD")));
        rs.Triggers.Add(dtAnulado);
        _dgMovs.RowStyle = rs;
        DockPanel.SetDock(_dgMovs, Dock.Top); movsStack.Children.Add(_dgMovs);

        Grid.SetColumn(movsStack, 0); abajoGrid.Children.Add(movsStack);

        // Panel derecho: ajuste de monto de cierre real
        var cierrePanel = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#CE93D8"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14,12,14,12) };
        var cierreSp = new StackPanel();
        cierreSp.Children.Add(new TextBlock { Text = "MONTO DE CIERRE REAL",
            FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = EBC("#4A148C"),
            Margin = new Thickness(0,0,0,4) });
        cierreSp.Children.Add(new TextBlock {
            Text = "Conteo físico registrado al cerrar esta caja — corregilo solo si el conteo original estaba mal cargado.",
            FontSize = 10.5, Foreground = EBC("#757575"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,0,0,10) });
        _txtMontoCierreReal = new TextBox { Padding = new Thickness(8,6,8,6), FontSize = 15,
            FontWeight = FontWeights.Bold, BorderBrush = EBC("#CE93D8"), IsEnabled = false,
            TextAlignment = TextAlignment.Right };
        cierreSp.Children.Add(_txtMontoCierreReal);
        _btnGuardarCierre = new Button { Content = "💾  Guardar monto de cierre", Height = 34,
            Margin = new Thickness(0,10,0,0), Background = EBC("#4A148C"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            IsEnabled = false };
        _btnGuardarCierre.Click += async (_, _) => await GuardarMontoCierre();
        cierreSp.Children.Add(_btnGuardarCierre);
        cierrePanel.Child = cierreSp;
        Grid.SetColumn(cierrePanel, 2); abajoGrid.Children.Add(cierrePanel);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14, 8, 14, 8) };
        var btnCerrar = new Button { Content = "✖  Cerrar", Height = 34,
            Padding = new Thickness(18,0,18,0), Background = EBC("#546E7A"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async Task InicializarAsync()
    {
        // Gate de entrada: solo administrador puede abrir este módulo — se pide contraseña
        // ANTES de mostrar cualquier dato, no solo antes de guardar cambios.
        _autorizado = await AdminValidacionHelper.PedirContrasenaAdmin(this, _db);
        if (!_autorizado) {
            MessageBox.Show("Este módulo es exclusivo para administradores.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }
        await CargarCajas();
    }

    private async Task CargarCajas()
    {
        if (!_autorizado) return;
        try {
            using var conn = _db.Create();
            var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddDays(-7);
            var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);

            _cajas = (await conn.QueryAsync<FilaCajaCerrada>(
                @"SELECT M.ID_MASTER AS IdMaster, M.ID_LOCAL AS IdLocal, ISNULL(L.NOMBRE,'') AS LocalNombre,
                         M.FECHA_APERTURA AS FechaApertura, M.FECHA_CIERRE AS FechaCierre,
                         ISNULL(U.NOMBRE_USUARIO,'') AS UsuarioCierreNombre,
                         ISNULL(M.TOT_INGRESOS,0) AS TotIngresos, ISNULL(M.TOT_EGRESOS,0) AS TotEgresos,
                         ISNULL(M.MONTO_CIERRE_REAL,0) AS MontoCierreReal
                  FROM CAJA_MASTER M
                  LEFT JOIN LOCALES L  ON L.ID_LOCAL = M.ID_LOCAL
                  LEFT JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_CIE
                  WHERE M.ESTADO = 'C'
                    AND M.FECHA_CIERRE >= @d AND M.FECHA_CIERRE < @h
                    AND (@l = 0 OR M.ID_LOCAL = @l)
                  ORDER BY M.FECHA_CIERRE DESC",
                new { d = desde, h = hasta, l = _localSelId })).ToList();

            // Si la caja que ya estaba seleccionada sigue apareciendo en el nuevo resultado
            // (ej. el usuario solo cambió el filtro de Local sin que afecte a esta caja), se
            // mantiene la selección y sus movimientos — antes cualquier re-búsqueda limpiaba
            // todo sin condición, obligando a volver a hacer clic y perdiendo el filtro de
            // Estado (Vigentes/Anulados) que el usuario ya había elegido para esa caja.
            var idPrevio = _cajaSel?.IdMaster;
            _dgCajas.ItemsSource = _cajas;
            _lblInfoCaja.Text = $"{_cajas.Count} caja(s) cerrada(s) encontrada(s). Haga clic en una fila para ver sus movimientos.";

            var cajaAunPresente = idPrevio.HasValue ? _cajas.FirstOrDefault(c => c.IdMaster == idPrevio.Value) : null;
            if (cajaAunPresente != null) {
                // Asignar SelectedItem dispara SelectionChanged -> SeleccionarCaja(), que ya
                // se encarga de recargar movimientos y habilitar el panel — no duplicar acá.
                _dgCajas.SelectedItem = cajaAunPresente;
            } else {
                _dgMovs.ItemsSource = null;
                _movs = new();
                _cajaSel = null;
                HabilitarPanelMovimientos(false);
            }
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar cajas cerradas: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AbrirSelectorLocal()
    {
        var dlg = new Window {
            Title = "Seleccionar Local", Width = 480, Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = EBC("#F4F6F8"), FontFamily = new System.Windows.Media.FontFamily("Segoe UI") };
        var dp = new DockPanel();
        var hdr = new Border { Background = EBC("#4A148C"), Padding = new Thickness(16,12,16,12) };
        hdr.Child = new TextBlock { Text = "📍  Seleccionar Local",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(16,10,16,10),
            BorderBrush = EBC("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var btnCerrarPie = new Button { Content = "✕  Cerrar", Height = 32, Padding = new Thickness(16,0,16,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = EBC("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
        btnCerrarPie.Click += (_, _) => dlg.Close();
        pie.Child = btnCerrarPie;
        DockPanel.SetDock(pie, Dock.Bottom); dp.Children.Add(pie);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14) };
        var wrap = new WrapPanel();

        Border MakeCard(byte id, string nombre, bool isTodos) {
            bool selected = isTodos ? _localSelId == 0 : _localSelId == id;
            var card = new Border { Width = 130, Height = 70, Margin = new Thickness(5),
                Background = EBC(selected ? "#4A148C" : "#FAFAFA"),
                CornerRadius = new CornerRadius(8),
                BorderBrush = EBC(selected ? "#4A148C" : "#E0E0E0"),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = nombre, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = selected ? System.Windows.Media.Brushes.White : EBC("#212121"),
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 110,
                HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = sp;
            card.MouseLeftButtonUp += async (_, _) => {
                _localSelId  = isTodos ? (byte)0 : id;
                _localSelNom = isTodos ? "Todos los locales" : nombre;
                _lblLocalNombre.Text = _localSelNom;
                dlg.Close();
                await CargarCajas();
            };
            return card;
        }

        wrap.Children.Add(MakeCard(0, "🏪 Todos", true));
        scroll.Content = wrap;
        DockPanel.SetDock(scroll, Dock.Top); dp.Children.Add(scroll);
        dlg.Content = dp;

        _ = CargarLocalesSelector(wrap, MakeCard);
        dlg.ShowDialog();
    }

    private async Task CargarLocalesSelector(WrapPanel wrap, Func<byte,string,bool,Border> makeCard)
    {
        try {
            using var conn = _db.Create();
            var locales = (await conn.QueryAsync<(byte Id, string Nombre)>(
                "SELECT ID_LOCAL, NOMBRE FROM LOCALES ORDER BY NOMBRE")).ToList();
            foreach (var l in locales) wrap.Children.Add(makeCard(l.Id, l.Nombre, false));
        } catch { /* selector queda solo con "Todos" si falla */ }
    }

    private async void OnCajaSelectionChanged(object sender, SelectionChangedEventArgs e) => await SeleccionarCaja();

    private async Task SeleccionarCaja()
    {
        if (_dgCajas.SelectedItem is not FilaCajaCerrada c) { HabilitarPanelMovimientos(false); return; }
        _cajaSel = c;
        await CargarMovimientos();
        _txtMontoCierreReal.Text = c.MontoCierreReal.ToString("N0");
        HabilitarPanelMovimientos(true);
    }

    private void HabilitarPanelMovimientos(bool habilitar)
    {
        _btnNuevoIngreso.IsEnabled = habilitar;
        _btnNuevoEgreso.IsEnabled  = habilitar;
        _btnEditarMov.IsEnabled    = habilitar;
        _btnAnularMov.IsEnabled    = habilitar;
        _btnGuardarCierre.IsEnabled = habilitar;
        _txtMontoCierreReal.IsEnabled = habilitar;
        if (!habilitar) { _txtMontoCierreReal.Text = ""; _dgMovs.ItemsSource = null; _lblTotales.Text = ""; }
    }

    private async Task CargarMovimientos()
    {
        if (_cajaSel == null) return;
        try {
            using var conn = _db.Create();
            _movs = (await conn.QueryAsync<FilaExploradorCaja>(
                @"SELECT D.ID_DETALLE, D.ID_MASTER, D.ID_VENTA,
                         CONVERT(VARCHAR(10), D.FECHA_HORA, 103) + ' ' + CONVERT(VARCHAR(5), D.FECHA_HORA, 108) AS FechaHoraStr,
                         ISNULL(L.NOMBRE,'') AS LocalNombre,
                         ISNULL(UC.NOMBRE_USUARIO,'') AS Cajero,
                         '' AS Cobrador,
                         CASE WHEN D.TIPO='I' THEN 'INGRESO' ELSE 'EGRESO' END AS TipoDesc,
                         ISNULL(D.SUBTIPO,'') AS SubTipo,
                         D.MONTO AS Monto, ISNULL(D.CONCEPTO,'') AS Concepto,
                         ISNULL(D.REFERENCIA,'') AS Referencia, '' AS Receptor,
                         ISNULL(D.FORMA_PAGO,'') AS FormaPago,
                         CASE WHEN D.ESTADO_REG='A' THEN 'ANULADO' ELSE 'VIGENTE' END AS EstadoDesc,
                         D.ID_CAJERO, ISNULL(D.ID_ENTIDAD,0) AS ID_ENTIDAD
                  FROM CAJA_DETALLE D
                  LEFT JOIN LOCALES L  ON L.ID_LOCAL = D.ID_LOCAL
                  LEFT JOIN USUARIOS UC ON UC.ID_USUARIO = D.ID_CAJERO
                  WHERE D.ID_MASTER = @idMaster
                  ORDER BY D.FECHA_HORA",
                new { idMaster = _cajaSel.IdMaster })).ToList();

            AplicarFiltroMovs();
            var ingVig = _movs.Where(m => m.TipoDesc == "INGRESO" && !m.Anulado).Sum(m => m.Monto);
            var egVig  = _movs.Where(m => m.TipoDesc == "EGRESO"  && !m.Anulado).Sum(m => m.Monto);
            _lblTotales.Text = $"Ingresos vigentes: Gs. {ingVig:N0}   |   Egresos vigentes: Gs. {egVig:N0}   |   Neto: Gs. {(ingVig-egVig):N0}";
        } catch (Exception ex) {
            MessageBox.Show($"Error al cargar movimientos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Filtra _movs (ya cargados en memoria para la caja seleccionada) por estado
    // Todos/Vigentes/Anulados — sin volver a consultar la base.
    private void AplicarFiltroMovs()
    {
        var filtro = (_cboEstadoMov?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
        _dgMovs.ItemsSource = filtro switch {
            "Vigentes" => _movs.Where(m => !m.Anulado).ToList(),
            "Anulados" => _movs.Where(m => m.Anulado).ToList(),
            _          => _movs,
        };
    }

    // Recalcula TOT_INGRESOS/TOT_EGRESOS de CAJA_MASTER sumando los CAJA_DETALLE vigentes —
    // se llama después de cualquier alta/edición/anulación de movimiento sobre la caja cerrada.
    private async Task RecalcularTotalesCaja(IDbConnection conn, int idMaster)
    {
        await conn.ExecuteAsync(
            @"UPDATE CAJA_MASTER SET
                TOT_INGRESOS = ISNULL((SELECT SUM(MONTO) FROM CAJA_DETALLE WHERE ID_MASTER=@id AND TIPO='I' AND ESTADO_REG='V'), 0),
                TOT_EGRESOS  = ISNULL((SELECT SUM(MONTO) FROM CAJA_DETALLE WHERE ID_MASTER=@id AND TIPO='E' AND ESTADO_REG='V'), 0)
              WHERE ID_MASTER=@id",
            new { id = idMaster });
    }

    private async Task NuevoMovimiento(bool esIngreso)
    {
        if (_cajaSel == null) return;
        var dlg = new CajaEditarMovDialog(_db, _session, null, _cajaSel.IdMaster, _cajaSel.IdLocal) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        try {
            using var conn = _db.Create();
            await RecalcularTotalesCaja(conn, _cajaSel.IdMaster);
        } catch (Exception ex) {
            MessageBox.Show($"El movimiento se guardó, pero falló el recálculo de totales: {ex.Message}",
                "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await CargarMovimientos();
        await RefrescarFilaCajaSeleccionada();
    }

    private async Task EditarMovimientoSeleccionado()
    {
        if (_dgMovs.SelectedItem is not FilaExploradorCaja fila) {
            MessageBox.Show("Seleccione un movimiento de la lista primero.", "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (fila.Anulado) {
            MessageBox.Show("Este movimiento ya está anulado, no se puede editar.", "Movimiento anulado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new CajaEditarMovDialog(_db, _session, fila) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        try {
            using var conn = _db.Create();
            await RecalcularTotalesCaja(conn, fila.ID_MASTER);
        } catch (Exception ex) {
            MessageBox.Show($"El movimiento se editó, pero falló el recálculo de totales: {ex.Message}",
                "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await CargarMovimientos();
        await RefrescarFilaCajaSeleccionada();
    }

    private async Task AnularMovimientoSeleccionado()
    {
        if (_dgMovs.SelectedItem is not FilaExploradorCaja fila) {
            MessageBox.Show("Seleccione un movimiento de la lista primero.", "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (fila.Anulado) {
            MessageBox.Show("Este movimiento ya está anulado.", "Sin cambios", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Los movimientos de tipo VENTA/COBRO son generados automáticamente por una venta o
        // el cobro de una cuota real — anularlos acá SOLO corrige el registro de caja (arqueo),
        // no revierte nada de la venta/cuota en sí (stock, GENERADAS, CABECERA_SALES quedan
        // intactos). Sin esta aclaración, un admin podía pensar que "Anular" acá deshace todo
        // el proceso como en Eliminar Venta a Crédito / Editar Cuota Pagada, cuando en realidad
        // son herramientas completamente distintas con alcances distintos.
        var esMovimientoDeSistema = fila.SubTipo?.ToUpperInvariant() is "VENTA" or "COBRO_SISTEMA" or "COBRO_S" or "COBRO_C";
        var avisoAlcance = esMovimientoDeSistema
            ? "\n\n⚠ IMPORTANTE: este movimiento corresponde a una VENTA/COBRO real. Anular acá SOLO " +
              "corrige el registro de caja (arqueo) — NO revierte el stock del artículo ni cambia el " +
              "estado de la cuota en el plan de pagos del cliente (seguirá figurando como cobrada). " +
              "Si lo que necesitás es deshacer la venta o la cuota en sí, usá 'Eliminar Venta a Crédito' " +
              "o 'Editar Cuota Pagada' en su lugar."
            : "";

        if (MessageBox.Show(
            $"¿Anular este movimiento?\n\n{fila.TipoDesc} — Gs. {fila.Monto:N0} — {fila.Concepto}\n\n" +
            "No se elimina físicamente (queda como ANULADO para no perder el historial), y se " +
            "descuenta del total de ingresos/egresos de esta caja." + avisoAlcance,
            "Confirmar anulación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try {
            using var conn = _db.Create();
            await conn.ExecuteAsync(
                "UPDATE CAJA_DETALLE SET ESTADO_REG='A' WHERE ID_DETALLE=@id",
                new { id = fila.ID_DETALLE });
            await RecalcularTotalesCaja(conn, fila.ID_MASTER);
            await CargarMovimientos();
            await RefrescarFilaCajaSeleccionada();
        } catch (Exception ex) {
            MessageBox.Show($"Error al anular: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task GuardarMontoCierre()
    {
        if (_cajaSel == null) return;
        if (!decimal.TryParse(new string(_txtMontoCierreReal.Text.Where(c => char.IsDigit(c) || c=='-').ToArray()), out var nuevoMonto)) {
            MessageBox.Show("Ingrese un monto válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(
            $"¿Actualizar el monto de cierre real de la Caja N° {_cajaSel.IdMaster} a Gs. {nuevoMonto:N0}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try {
            using var conn = _db.Create();
            await conn.ExecuteAsync(
                "UPDATE CAJA_MASTER SET MONTO_CIERRE_REAL=@m WHERE ID_MASTER=@id",
                new { m = nuevoMonto, id = _cajaSel.IdMaster });
            MessageBox.Show("Monto de cierre actualizado.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefrescarFilaCajaSeleccionada();
        } catch (Exception ex) {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Refresca solo los datos de la caja actualmente seleccionada (totales, monto de cierre)
    // sin perder la selección ni recargar toda la lista de cajas — recargar CargarCajas()
    // entero perdería la fila seleccionada y colapsaría el panel de movimientos.
    private async Task RefrescarFilaCajaSeleccionada()
    {
        if (_cajaSel == null) return;
        try {
            using var conn = _db.Create();
            var actualizada = await conn.QueryFirstOrDefaultAsync<FilaCajaCerrada>(
                @"SELECT M.ID_MASTER AS IdMaster, M.ID_LOCAL AS IdLocal, ISNULL(L.NOMBRE,'') AS LocalNombre,
                         M.FECHA_APERTURA AS FechaApertura, M.FECHA_CIERRE AS FechaCierre,
                         ISNULL(U.NOMBRE_USUARIO,'') AS UsuarioCierreNombre,
                         ISNULL(M.TOT_INGRESOS,0) AS TotIngresos, ISNULL(M.TOT_EGRESOS,0) AS TotEgresos,
                         ISNULL(M.MONTO_CIERRE_REAL,0) AS MontoCierreReal
                  FROM CAJA_MASTER M
                  LEFT JOIN LOCALES L  ON L.ID_LOCAL = M.ID_LOCAL
                  LEFT JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_CIE
                  WHERE M.ID_MASTER = @id",
                new { id = _cajaSel.IdMaster });
            if (actualizada == null) return;

            var idx = _cajas.FindIndex(c => c.IdMaster == actualizada.IdMaster);
            if (idx >= 0) _cajas[idx] = actualizada;

            // Reasignar ItemsSource pierde la selección visual de la grilla (dispara
            // SelectionChanged con SelectedItem=null), lo que a su vez llama a
            // SeleccionarCaja() y limpia el panel de movimientos que recién se había cargado
            // — por eso se desuscribe el evento momentáneamente y se restaura la selección a
            // mano, sin volver a disparar la recarga (los movimientos ya están al día porque
            // quien llamó a este método — Anular/Editar/Nuevo movimiento — ya los recargó).
            _dgCajas.SelectionChanged -= OnCajaSelectionChanged;
            _dgCajas.ItemsSource = null;
            _dgCajas.ItemsSource = _cajas;
            _dgCajas.SelectedItem = actualizada;
            _dgCajas.SelectionChanged += OnCajaSelectionChanged;

            _cajaSel = actualizada;
            _txtMontoCierreReal.Text = actualizada.MontoCierreReal.ToString("N0");
        } catch { /* la grilla principal se termina de refrescar en el próximo CargarCajas() */ }
    }
}

internal class FilaCajaCerrada
{
    public int      IdMaster       { get; set; }
    public byte     IdLocal        { get; set; }
    public string   LocalNombre    { get; set; } = "";
    public DateTime FechaApertura  { get; set; }
    public DateTime? FechaCierre   { get; set; }
    public string   UsuarioCierreNombre { get; set; } = "";
    public decimal  TotIngresos    { get; set; }
    public decimal  TotEgresos     { get; set; }
    public decimal  MontoCierreReal{ get; set; }
    public string   FechaAperturaStr => FechaApertura.ToString("dd/MM/yyyy HH:mm");
    public string   FechaCierreStr   => FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "";
    public string   TotIngresosFmt   => TotIngresos.ToString("N0");
    public string   TotEgresosFmt    => TotEgresos.ToString("N0");
    public string   MontoCierreRealFmt => MontoCierreReal.ToString("N0");
}

