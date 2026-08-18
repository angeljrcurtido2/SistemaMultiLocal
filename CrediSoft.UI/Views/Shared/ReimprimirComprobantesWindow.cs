using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CrediSoft.Data;
using CrediSoft.Core.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace CrediSoft.UI.Views.Shared;

// Fila de resultado del historial COMPROBANTES_GENERADOS — solo lo necesario para mostrar
// en la grilla; el contenido real para reimprimir vive en DatosJson (deserializado on-demand
// al presionar "Reimprimir", no antes, para no pagar el costo en cada búsqueda).
public class ComprobanteHistorialRow
{
    public int      IdComprobante  { get; set; }
    public string    Tipo          { get; set; } = "";
    public long      NumeroTicket  { get; set; }
    public int       IdLocal       { get; set; }
    public DateTime  FechaEmision  { get; set; }
    public string?   NombreCajero  { get; set; }
    public string?   NombreCliente { get; set; }
    public string?   NroSolicitud  { get; set; }
    public string?   NRecibo       { get; set; }
    public decimal?  MontoTotal    { get; set; }
    public string?   DatosJson     { get; set; }

    public string TipoTexto => Tipo switch {
        "COBRO"            => "Cobro de cuota",
        "VENTA_CONTADO"     => "Venta al contado",
        "VENTA_CREDITO"     => "Venta a crédito",
        "SOLICITUD_APROBADA" => "Solicitud aprobada",
        _ => Tipo
    };
}

// "Bibliorato" de comprobantes: busca en COMPROBANTES_GENERADOS (poblada por
// TicketPrinter.RegistrarComprobanteAsync desde Cobros/Ventas) y reimprime el ticket original
// EXACTO deserializando el snapshot JSON guardado — no reconstruye el dato desde otras tablas,
// que pueden haber cambiado desde la emisión original (pedido explícito 2026-08-10: módulo de
// reimpresión "robusto").
public class ReimprimirComprobantesWindow : Window
{
    private readonly ISessionService _session = SessionService.Instance;

    private DataGrid _grid = null!;
    private TextBox  _txtTicket = null!, _txtCliente = null!;
    private ComboBox _cboTipo = null!, _cboLocal = null!;
    private DatePicker _dpDesde = null!, _dpHasta = null!;
    private Button   _btnBuscar = null!, _btnReimprimir = null!;
    private TextBlock _txtEstado = null!;
    private List<ComprobanteHistorialRow> _resultados = new();

    private static readonly SolidColorBrush _brPrim  = new(Color.FromRgb(255,140,  0));
    private static readonly SolidColorBrush _brDark  = new(Color.FromRgb(224,112,  0));
    private static readonly SolidColorBrush _brFondo = new(Color.FromRgb(250,250,252));
    private static readonly SolidColorBrush _brCard  = Brushes.White;
    private static readonly SolidColorBrush _brBorde = new(Color.FromRgb(229,231,235));
    private static readonly SolidColorBrush _brLabel = new(Color.FromRgb(107,114,128));

    public ReimprimirComprobantesWindow()
    {
        Title = "Reimprimir Comprobantes";
        Width = 1080; Height = 640;
        MinWidth = 900; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = _brFondo;
        FontFamily = new FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await BuscarAsync();
    }

    private Button MkBtn(string text, string hex) => new() {
        Content = text, Height = 32, Padding = new Thickness(12,0,12,0),
        Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
        Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12,
        BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
    };

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ──────────────────────────────────────────────────────────
        var hdr = new Border { Background = _brPrim, Padding = new Thickness(16,10,16,10) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "🧾  Reimprimir Comprobantes", Foreground = Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold
        });
        hdrSp.Children.Add(new TextBlock {
            Text = "Buscá un cobro o venta ya emitido y reimprimí el comprobante original",
            Foreground = new SolidColorBrush(Color.FromRgb(255,224,178)), FontSize = 10.5
        });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Filtros ─────────────────────────────────────────────────────────
        var filtrosBorder = new Border {
            Background = _brCard, BorderBrush = _brBorde, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(12,10,12,10)
        };
        var filtrosSp = new StackPanel { Orientation = Orientation.Horizontal };

        TextBlock Lbl(string t) => new() {
            Text = t, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
            Foreground = _brLabel, Margin = new Thickness(0,0,0,2)
        };
        StackPanel Campo(string lbl, UIElement control) {
            var sp = new StackPanel { Margin = new Thickness(0,0,10,0) };
            sp.Children.Add(Lbl(lbl));
            sp.Children.Add(control);
            return sp;
        }

        _txtTicket = new TextBox { Width = 110, Height = 26, Padding = new Thickness(5,2,5,2) };
        _txtCliente = new TextBox { Width = 160, Height = 26, Padding = new Thickness(5,2,5,2) };

        _cboTipo = new ComboBox { Width = 150, Height = 26 };
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Todos", Tag = "", IsSelected = true });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Cobro de cuota", Tag = "COBRO" });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Venta al contado", Tag = "VENTA_CONTADO" });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Venta a crédito", Tag = "VENTA_CREDITO" });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Solicitud aprobada", Tag = "SOLICITUD_APROBADA" });

        // Local: admin/usuario con acceso total ven combo de los 14; el resto queda fijo en
        // su propio local (mismo criterio ya usado en LocalesWindow) — no tiene sentido
        // ofrecer buscar comprobantes de otro local si tampoco puede verlos en ningún lado.
        bool puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
        _cboLocal = new ComboBox { Width = 130, Height = 26, IsEnabled = puedeVerTodos };
        _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos", Tag = 0, IsSelected = true });

        _dpDesde = new DatePicker { Width = 120, Height = 26 };
        _dpHasta = new DatePicker { Width = 120, Height = 26 };

        filtrosSp.Children.Add(Campo("N° Ticket", _txtTicket));
        filtrosSp.Children.Add(Campo("Cliente", _txtCliente));
        filtrosSp.Children.Add(Campo("Tipo", _cboTipo));
        filtrosSp.Children.Add(Campo("Local", _cboLocal));
        filtrosSp.Children.Add(Campo("Desde", _dpDesde));
        filtrosSp.Children.Add(Campo("Hasta", _dpHasta));

        _btnBuscar = MkBtn("🔍  Buscar", "#1565C0");
        _btnBuscar.Height = 26; _btnBuscar.Margin = new Thickness(4,16,0,0);
        _btnBuscar.Click += async (_, _) => await BuscarAsync();
        filtrosSp.Children.Add(_btnBuscar);

        filtrosBorder.Child = filtrosSp;
        Grid.SetRow(filtrosBorder, 1); root.Children.Add(filtrosBorder);

        if (puedeVerTodos)
            _ = CargarCombosLocalesAsync();
        else
            _cboLocal.Items.Add(new ComboBoxItem {
                Content = _session.LocalActual?.NombreLocal ?? "", Tag = _session.LocalActual?.IdLocal ?? 0
            });

        // ── Grid resultados ─────────────────────────────────────────────────
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = _brBorde, RowBackground = _brCard,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(249,250,251)),
            RowHeight = 32, ColumnHeaderHeight = 32, FontSize = 12,
            BorderThickness = new Thickness(0), Margin = new Thickness(10,8,10,8)
        };
        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, _brPrim));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, Brushes.White));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,0,10,0)));
        _grid.ColumnHeaderStyle = colHdrStyle;

        var txt = new Style(typeof(TextBlock));
        txt.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txt.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,10,0)));

        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Ticket", Binding = new System.Windows.Data.Binding("NumeroTicket") { StringFormat = "D9" }, Width = 100, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Tipo", Binding = new System.Windows.Data.Binding("TipoTexto"), Width = 140, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha", Binding = new System.Windows.Data.Binding("FechaEmision") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = 130, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new System.Windows.Data.Binding("NombreCliente"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cajero/Vendedor", Binding = new System.Windows.Data.Binding("NombreCajero"), Width = 150, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Recibo/Solic.", Binding = new System.Windows.Data.Binding("NRecibo"), Width = 120, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Monto", Binding = new System.Windows.Data.Binding("MontoTotal") { StringFormat = "N0" }, Width = 100, ElementStyle = txt });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Local", Binding = new System.Windows.Data.Binding("IdLocal"), Width = 60, ElementStyle = txt });
        _grid.SelectionChanged += (_, _) => _btnReimprimir.IsEnabled = _grid.SelectedItem != null;
        _grid.MouseDoubleClick += async (_, _) => await ReimprimirSeleccionadoAsync();

        Grid.SetRow(_grid, 2); root.Children.Add(_grid);

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = _brCard, BorderBrush = _brBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8)
        };
        var footerDp = new DockPanel();
        _txtEstado = new TextBlock { FontSize = 11, Foreground = _brLabel, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(_txtEstado, Dock.Left);
        footerDp.Children.Add(_txtEstado);

        var barBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _btnReimprimir = MkBtn("🖨  Reimprimir", "#27AE60");
        _btnReimprimir.IsEnabled = false;
        _btnReimprimir.Click += async (_, _) => await ReimprimirSeleccionadoAsync();
        var btnCerrar = MkBtn("✕  Cerrar", "#757575");
        btnCerrar.Click += (_, _) => Close();
        barBtns.Children.Add(_btnReimprimir);
        barBtns.Children.Add(btnCerrar);
        footerDp.Children.Add(barBtns);

        footer.Child = footerDp;
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        Content = root;
    }

    private async Task CargarCombosLocalesAsync()
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            var locales = (await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();
            foreach (var l in locales)
                _cboLocal.Items.Add(new ComboBoxItem { Content = $"{l.Id} — {l.Nombre}", Tag = l.Id });
        }
        catch { /* combo queda solo con "Todos" si falla */ }
    }

    private async Task BuscarAsync()
    {
        _txtEstado.Text = "Buscando...";
        _btnBuscar.IsEnabled = false;
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();

            var existeTabla = await conn.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID('dbo.COMPROBANTES_GENERADOS','U') IS NULL THEN 0 ELSE 1 END");
            if (existeTabla == 0)
            {
                _resultados = new();
                _grid.ItemsSource = _resultados;
                _txtEstado.Text = "Todavía no hay comprobantes registrados en el historial.";
                return;
            }

            var tipo = (_cboTipo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var idLocal = (int)((_cboLocal.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            var ticketTxt = _txtTicket.Text.Trim();
            var cliente = _txtCliente.Text.Trim();

            // Usuario sin PuedeVerTodosLosLocales: el combo queda fijo en su local (ver
            // BuildUI), pero se refuerza acá también — defensa en profundidad, mismo criterio
            // que el resto de la app.
            bool puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
            if (!puedeVerTodos) idLocal = _session.LocalActual?.IdLocal ?? 0;

            var sql = @"SELECT TOP 300 IDCOMPROBANTE AS IdComprobante, TIPO AS Tipo,
                               NUMERO_TICKET AS NumeroTicket, ID_LOCAL AS IdLocal,
                               FECHA_EMISION AS FechaEmision, NOMBRE_CAJERO AS NombreCajero,
                               NOMBRE_CLIENTE AS NombreCliente, NRO_SOLICITUD AS NroSolicitud,
                               NRECIBO AS NRecibo, MONTO_TOTAL AS MontoTotal, DATOS_JSON AS DatosJson
                        FROM COMPROBANTES_GENERADOS
                        WHERE (@tipo = '' OR TIPO = @tipo)
                          AND (@idLocal = 0 OR ID_LOCAL = @idLocal)
                          AND (@ticket = '' OR CAST(NUMERO_TICKET AS VARCHAR(20)) LIKE '%' + @ticket + '%')
                          AND (@cliente = '' OR NOMBRE_CLIENTE LIKE '%' + @cliente + '%')
                          AND (@desde IS NULL OR FECHA_EMISION >= @desde)
                          AND (@hasta IS NULL OR FECHA_EMISION < @hasta)
                        ORDER BY FECHA_EMISION DESC";

            _resultados = (await conn.QueryAsync<ComprobanteHistorialRow>(sql, new {
                tipo, idLocal, ticket = ticketTxt, cliente,
                desde = _dpDesde.SelectedDate,
                hasta = _dpHasta.SelectedDate?.AddDays(1)
            })).ToList();

            _grid.ItemsSource = _resultados;
            _txtEstado.Text = $"{_resultados.Count} comprobante(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            _txtEstado.Text = $"Error al buscar: {ex.Message}";
        }
        finally { _btnBuscar.IsEnabled = true; }
    }

    private async Task ReimprimirSeleccionadoAsync()
    {
        if (_grid.SelectedItem is not ComprobanteHistorialRow fila || string.IsNullOrWhiteSpace(fila.DatosJson))
            return;
        try
        {
            _btnReimprimir.IsEnabled = false;
            var fmt = await TicketPrinter.ObtenerFormatoComprobanteAsync();

            if (fila.Tipo == "COBRO")
            {
                var datos = System.Text.Json.JsonSerializer.Deserialize<DatosTicketCobro>(fila.DatosJson);
                if (datos == null) { MessageBox.Show("No se pudo leer el comprobante guardado.", "Error"); return; }
                var previa = new Cobros.ComprobantePreviaWindow(datos, fmt) { Owner = this };
                previa.ShowDialog();
            }
            else
            {
                var datos = System.Text.Json.JsonSerializer.Deserialize<DatosTicketVenta>(fila.DatosJson);
                if (datos == null) { MessageBox.Show("No se pudo leer el comprobante guardado.", "Error"); return; }
                var previa = new Ventas.ComprobanteVentaPreviaWindow(datos, fmt) { Owner = this };
                previa.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al reimprimir: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _btnReimprimir.IsEnabled = true; }
    }
}
