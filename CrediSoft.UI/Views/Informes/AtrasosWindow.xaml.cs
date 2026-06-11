using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Informes
{
    // ── ATRASOS ────────────────────────────────────────────────────────────────
    public partial class AtrasosWindow : Window
    {
        private readonly ICuotaRepository _cuotas;

        public AtrasosWindow()
        {
            InitializeComponent();
            _cuotas = App.Services.GetRequiredService<ICuotaRepository>();
            DpDesde.SelectedDate = DateTime.Today.AddMonths(-1);
            DpHasta.SelectedDate = DateTime.Today;
        }

        private void OnCriterioChanged(object s, RoutedEventArgs e)
        {
            DpDesde.IsEnabled = DpHasta.IsEnabled = RbPeriodo.IsChecked == true;
            TxtDias.IsEnabled = RbDias.IsChecked == true;
            TxtIntDesde.IsEnabled = TxtIntHasta.IsEnabled = RbIntervalo.IsChecked == true;
        }

        private async void OnBuscar(object s, RoutedEventArgs e) => await Buscar();
        private async void OnCompleto(object s, RoutedEventArgs e) { RbTodos.IsChecked = true; await Buscar(); }

        private async Task Buscar()
        {
            int? dias = RbDias.IsChecked == true && int.TryParse(TxtDias.Text, out var d) ? d : null;
            int? local = int.TryParse(TxtLocal.Text, out var l) ? l : null;
            var resultados = await _cuotas.BuscarAtrasosAsync(local, dias);
            GridMorosos.ItemsSource = resultados;
        }

        private void OnMorosoSeleccionado(object s, SelectionChangedEventArgs e) { }
        private void OnResumenLocal(object s, RoutedEventArgs e) =>
            MessageBox.Show("Función Resumen x Local en desarrollo.", "Próximamente");

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.Escape)
                Close();
        }
    }

    // ── HISTORIAL COBRANZAS ───────────────────────────────────────────────────
    public class HCobranzasWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService _session;
        private DataGrid _grid = null!;
        private DatePicker _dpDesde = null!, _dpHasta = null!;
        private TextBox _txtLocal = null!;
        private TextBlock _lblTotal = null!;

        public HCobranzasWindow()
        {
            _db = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title = "Historial de Cobranzas"; Width = 1000; Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new DockPanel();

            // Filtros top
            var filtros = new Border { Background = System.Windows.Media.Brushes.DarkOrange, Padding = new Thickness(8, 6, 8, 6) };
            var fp = new StackPanel { Orientation = Orientation.Horizontal };
            fp.Children.Add(new TextBlock { Text = "Desde:", Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            _dpDesde = new DatePicker { Width = 130, SelectedDate = DateTime.Today.AddMonths(-1) };
            fp.Children.Add(_dpDesde);
            fp.Children.Add(new TextBlock { Text = "Hasta:", Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) });
            _dpHasta = new DatePicker { Width = 130, SelectedDate = DateTime.Today };
            fp.Children.Add(_dpHasta);
            fp.Children.Add(new TextBlock { Text = "Local:", Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) });
            _txtLocal = new TextBox { Width = 50, Padding = new Thickness(4, 2, 4, 2), Text = _session.LocalActual?.IdLocal.ToString() };
            fp.Children.Add(_txtLocal);
            var btnBuscar = new Button { Content = "🔍 Buscar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 0, 0, 0) };
            btnBuscar.Click += async (_, _) => await Buscar();
            fp.Children.Add(btnBuscar);
            var btnCerrar = new Button { Content = "Cerrar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 0, 0, 0) };
            btnCerrar.Click += (_, _) => Close();
            fp.Children.Add(btnCerrar);
            filtros.Child = fp;
            DockPanel.SetDock(filtros, Dock.Top);
            root.Children.Add(filtros);

            // Total bottom
            var bottomBar = new Border { Background = System.Windows.Media.Brushes.LightGray, Padding = new Thickness(8, 4, 8, 4) };
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 13 };
            bottomBar.Child = _lblTotal;
            DockPanel.SetDock(bottomBar, Dock.Bottom);
            root.Children.Add(bottomBar);

            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
                AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
            _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",      Binding = new System.Windows.Data.Binding("FECHA") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = 130 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Local",      Binding = new System.Windows.Data.Binding("ID_LOCAL"), Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Usuario",    Binding = new System.Windows.Data.Binding("USUARIO"), Width = 130 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Acción",     Binding = new System.Windows.Data.Binding("ACCION"), Width = 60 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Concepto",   Binding = new System.Windows.Data.Binding("CONCEPTO"), Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Monto Gs.",  Binding = new System.Windows.Data.Binding("MONTO") { StringFormat = "N0" }, Width = 110 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Método",     Binding = new System.Windows.Data.Binding("METODO"), Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Nro.",       Binding = new System.Windows.Data.Binding("NUMERO"), Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Recibidor",  Binding = new System.Windows.Data.Binding("RECIBIDOR"), Width = 120 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Obs.",       Binding = new System.Windows.Data.Binding("OBSERVACION"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid);
            Content = root;
        }

        private async Task Buscar()
        {
            try
            {
                var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var hasta = _dpHasta.SelectedDate ?? DateTime.Today;
                int? idLocal = int.TryParse(_txtLocal.Text, out var l) ? l : (int?)null;

                using var conn = _db.Create();
                var p = new DynamicParameters();
                p.Add("@FechaInicio", desde);
                p.Add("@FechaFin", hasta.AddDays(1));
                p.Add("@IDLocal", idLocal ?? _session.LocalActual?.IdLocal ?? 0);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                var rows = (await conn.QueryAsync("CARGAR_HIST_CAJA_LOCAL_CS", p, commandType: CommandType.StoredProcedure)).ToList();
                _grid.ItemsSource = rows;

                var total = rows.Sum(r => { try { return (decimal)((IDictionary<string, object>)r)["MONTO"]; } catch { return 0m; } });
                _lblTotal.Text = $"Total registros: {rows.Count}   |   Total Gs.: {total:N0}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }
    }

    // ── HISTORIAL CRÉDITOS (Solicitudes aceptadas) ────────────────────────────
    public class HCreditosWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService _session;
        private DataGrid _grid = null!;
        private DatePicker _dpDesde = null!, _dpHasta = null!;
        private TextBox _txtLocal = null!;
        private TextBlock _lblTotal = null!;

        public HCreditosWindow()
        {
            _db = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title = "Historial de Créditos"; Width = 1050; Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new DockPanel();
            var filtros = new Border { Background = System.Windows.Media.Brushes.DarkOrange, Padding = new Thickness(8, 6, 8, 6) };
            var fp = new StackPanel { Orientation = Orientation.Horizontal };
            void Lbl(string t) => fp.Children.Add(new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
            Lbl("Desde:"); _dpDesde = new DatePicker { Width = 130, SelectedDate = DateTime.Today.AddMonths(-1) }; fp.Children.Add(_dpDesde);
            Lbl("Hasta:"); _dpHasta = new DatePicker { Width = 130, SelectedDate = DateTime.Today }; fp.Children.Add(_dpHasta);
            Lbl("Local:"); _txtLocal = new TextBox { Width = 50, Padding = new Thickness(4, 2, 4, 2), Text = _session.LocalActual?.IdLocal.ToString() }; fp.Children.Add(_txtLocal);
            var btnB = new Button { Content = "🔍 Buscar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 0, 0, 0) }; btnB.Click += async (_, _) => await Buscar(); fp.Children.Add(btnB);
            var btnC = new Button { Content = "Cerrar",    Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };  btnC.Click += (_, _) => Close(); fp.Children.Add(btnC);
            filtros.Child = fp; DockPanel.SetDock(filtros, Dock.Top); root.Children.Add(filtros);

            var bot = new Border { Background = System.Windows.Media.Brushes.LightGray, Padding = new Thickness(8, 4, 8, 4) };
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 13 };
            bot.Child = _lblTotal; DockPanel.SetDock(bot, Dock.Bottom); root.Children.Add(bot);

            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
            _grid.Columns.Add(new DataGridTextColumn { Header = "IDCAB",       Binding = new System.Windows.Data.Binding("IDCAB"),       Width = 60 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Solicitud",   Binding = new System.Windows.Data.Binding("NSOLICITUD"),  Width = 130 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",       Binding = new System.Windows.Data.Binding("FECHA") { StringFormat = "dd/MM/yyyy" }, Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Local",       Binding = new System.Windows.Data.Binding("ID_LOCAL"),   Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente",     Binding = new System.Windows.Data.Binding("ID_CLIENTE"), Width = 80 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Total Gs.",   Binding = new System.Windows.Data.Binding("TOTAL") { StringFormat = "N0" },     Width = 110 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Cuotas",      Binding = new System.Windows.Data.Binding("CUOTAS"),     Width = 60 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Mto.Cuota",   Binding = new System.Windows.Data.Binding("MONTO_CUOTA") { StringFormat = "N0" }, Width = 100 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Entrega",     Binding = new System.Windows.Data.Binding("ENTREGANORMAL") { StringFormat = "N0" }, Width = 100 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",      Binding = new System.Windows.Data.Binding("ESTADO"),     Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Usuario",     Binding = new System.Windows.Data.Binding("ID_USUARIO"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid); Content = root;
        }

        private async Task Buscar()
        {
            try
            {
                var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
                int idLocal = int.TryParse(_txtLocal.Text, out var l) ? l : (_session.LocalActual?.IdLocal ?? 0);

                using var conn = _db.Create();
                var rows = (await conn.QueryAsync(
                    "SELECT * FROM CABECERA_SALES WHERE ID_LOCAL=@L AND FECHA BETWEEN @D AND @H AND FORMA_DE_VENTA=1 ORDER BY FECHA DESC",
                    new { L = idLocal, D = desde, H = hasta })).ToList();
                _grid.ItemsSource = rows;

                decimal total = 0;
                foreach (IDictionary<string, object> r in rows)
                    if (r.TryGetValue("TOTAL", out var v)) total += Convert.ToDecimal(v ?? 0);
                _lblTotal.Text = $"Registros: {rows.Count}   |   Total Gs.: {total:N0}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }
    }

    // ── HISTORIAL VENTAS ──────────────────────────────────────────────────────
    public class HVentasWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService _session;
        private DataGrid _grid = null!;
        private DatePicker _dpDesde = null!, _dpHasta = null!;
        private TextBox _txtLocal = null!;
        private TextBlock _lblTotal = null!;

        public HVentasWindow()
        {
            _db = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title = "Historial de Ventas"; Width = 1050; Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new DockPanel();
            var filtros = new Border { Background = System.Windows.Media.Brushes.DarkOrange, Padding = new Thickness(8, 6, 8, 6) };
            var fp = new StackPanel { Orientation = Orientation.Horizontal };
            void Lbl(string t) => fp.Children.Add(new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
            Lbl("Desde:"); _dpDesde = new DatePicker { Width = 130, SelectedDate = DateTime.Today.AddMonths(-1) }; fp.Children.Add(_dpDesde);
            Lbl("Hasta:"); _dpHasta = new DatePicker { Width = 130, SelectedDate = DateTime.Today };              fp.Children.Add(_dpHasta);
            Lbl("Local:"); _txtLocal = new TextBox { Width = 50, Padding = new Thickness(4, 2, 4, 2), Text = _session.LocalActual?.IdLocal.ToString() }; fp.Children.Add(_txtLocal);
            var btnB = new Button { Content = "🔍 Buscar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 0, 0, 0) }; btnB.Click += async (_, _) => await Buscar(); fp.Children.Add(btnB);
            var btnC = new Button { Content = "Cerrar",    Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6,  0, 0, 0) }; btnC.Click += (_, _) => Close(); fp.Children.Add(btnC);
            filtros.Child = fp; DockPanel.SetDock(filtros, Dock.Top); root.Children.Add(filtros);

            var bot = new Border { Background = System.Windows.Media.Brushes.LightGray, Padding = new Thickness(8, 4, 8, 4) };
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 13 };
            bot.Child = _lblTotal; DockPanel.SetDock(bot, Dock.Bottom); root.Children.Add(bot);

            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
            _grid.Columns.Add(new DataGridTextColumn { Header = "IDCAB",     Binding = new System.Windows.Data.Binding("IDCAB"),     Width = 60 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Solicitud", Binding = new System.Windows.Data.Binding("NSOLICITUD"), Width = 130 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",     Binding = new System.Windows.Data.Binding("FECHA") { StringFormat = "dd/MM/yyyy" }, Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Tipo",      Binding = new System.Windows.Data.Binding("FORMA_DE_VENTA"), Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Local",     Binding = new System.Windows.Data.Binding("ID_LOCAL"),  Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente",   Binding = new System.Windows.Data.Binding("ID_CLIENTE"), Width = 80 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Total Gs.", Binding = new System.Windows.Data.Binding("TOTAL") { StringFormat = "N0" }, Width = 110 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Descuento", Binding = new System.Windows.Data.Binding("DESCUENTO") { StringFormat = "N0" }, Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Entrega",   Binding = new System.Windows.Data.Binding("ENTREGANORMAL") { StringFormat = "N0" }, Width = 100 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",    Binding = new System.Windows.Data.Binding("ESTADO"),    Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Nventa",    Binding = new System.Windows.Data.Binding("NVENTA"),    Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid); Content = root;
        }

        private async Task Buscar()
        {
            try
            {
                var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
                int idLocal = int.TryParse(_txtLocal.Text, out var l) ? l : (_session.LocalActual?.IdLocal ?? 0);

                using var conn = _db.Create();
                var rows = (await conn.QueryAsync(
                    "SELECT * FROM CABECERA_SALES WHERE ID_LOCAL=@L AND FECHA BETWEEN @D AND @H ORDER BY FECHA DESC",
                    new { L = idLocal, D = desde, H = hasta })).ToList();
                _grid.ItemsSource = rows;

                decimal total = 0;
                foreach (IDictionary<string, object> r in rows)
                    if (r.TryGetValue("TOTAL", out var v)) total += Convert.ToDecimal(v ?? 0);
                _lblTotal.Text = $"Registros: {rows.Count}   |   Total Gs.: {total:N0}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }
    }

    // ── H. NOTA DE CRÉDITO ────────────────────────────────────────────────────
    public class HNotaCreditoWindow : Window
    {
        public HNotaCreditoWindow()
        {
            Title = "Cobros por Nota de Crédito"; Width = 700; Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            var btn = new Button { Content = "Cerrar", Width = 80, Height = 30, Margin = new Thickness(16) };
            btn.Click += (_, _) => Close();
            Content = new StackPanel { Children = {
                new TextBlock { Text = "Módulo Nota de Crédito — en desarrollo", Margin = new Thickness(20), FontSize = 14 }, btn } };
        }
    }


    // ── MOVIMIENTO DE ARTÍCULOS ───────────────────────────────────────────────
    public class MovArtWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService _session;
        private DataGrid _grid = null!;
        private DatePicker _dpDesde = null!, _dpHasta = null!;
        private TextBox _txtLocal = null!, _txtArt = null!;
        private TextBlock _lblCont = null!;

        public MovArtWindow()
        {
            _db = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title = "Movimiento de Artículos"; Width = 1000; Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new DockPanel();
            var top = new Border { Background = System.Windows.Media.Brushes.DarkOrange, Padding = new Thickness(8, 6, 8, 6) };
            var fp = new StackPanel { Orientation = Orientation.Horizontal };
            void Lbl(string t) => fp.Children.Add(new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
            Lbl("Desde:"); _dpDesde = new DatePicker { Width = 130, SelectedDate = DateTime.Today.AddMonths(-1) }; fp.Children.Add(_dpDesde);
            Lbl("Hasta:"); _dpHasta = new DatePicker { Width = 130, SelectedDate = DateTime.Today };              fp.Children.Add(_dpHasta);
            Lbl("Local:"); _txtLocal = new TextBox { Width = 50, Padding = new Thickness(4, 2, 4, 2), Text = _session.LocalActual?.IdLocal.ToString() }; fp.Children.Add(_txtLocal);
            Lbl("ID Art:"); _txtArt = new TextBox { Width = 70, Padding = new Thickness(4, 2, 4, 2) }; fp.Children.Add(_txtArt);
            var btnB = new Button { Content = "🔍 Buscar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 0, 0, 0) }; btnB.Click += async (_, _) => await Buscar(); fp.Children.Add(btnB);
            var btnC = new Button { Content = "Cerrar",    Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6,  0, 0, 0) }; btnC.Click += (_, _) => Close(); fp.Children.Add(btnC);
            top.Child = fp; DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);

            var bot = new Border { Background = System.Windows.Media.Brushes.LightGray, Padding = new Thickness(8, 4, 8, 4) };
            _lblCont = new TextBlock { FontWeight = FontWeights.Bold };
            bot.Child = _lblCont; DockPanel.SetDock(bot, Dock.Bottom); root.Children.Add(bot);

            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
            _grid.Columns.Add(new DataGridTextColumn { Header = "ID Mov",   Binding = new System.Windows.Data.Binding("IDMOVART"),  Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "ID Art",   Binding = new System.Windows.Data.Binding("IDART"),     Width = 70 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Mov",      Binding = new System.Windows.Data.Binding("MOV"),       Width = 40 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Modo",     Binding = new System.Windows.Data.Binding("MOD"),       Width = 40 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "St.Inicial",Binding = new System.Windows.Data.Binding("STINI") { StringFormat = "N0" },   Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Cantidad", Binding = new System.Windows.Data.Binding("CANT") { StringFormat = "N2" },    Width = 80 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "St.Final", Binding = new System.Windows.Data.Binding("PCANT") { StringFormat = "N0" },   Width = 80 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "P.Costo",  Binding = new System.Windows.Data.Binding("PCACT") { StringFormat = "N0" },   Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Local",    Binding = new System.Windows.Data.Binding("IDLOCAL"),   Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Destino",  Binding = new System.Windows.Data.Binding("IDDESTINO"), Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Usuario",  Binding = new System.Windows.Data.Binding("IDU"),       Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",    Binding = new System.Windows.Data.Binding("FECHA") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid); Content = root;
        }

        private async Task Buscar()
        {
            try
            {
                var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
                int idLocal = int.TryParse(_txtLocal.Text, out var l) ? l : (_session.LocalActual?.IdLocal ?? 0);
                int? idArt = int.TryParse(_txtArt.Text, out var a) ? a : (int?)null;

                var whereArt = idArt.HasValue ? "AND IDART=@A" : "";
                using var conn = _db.Create();
                var rows = (await conn.QueryAsync(
                    $"SELECT * FROM MOVART WHERE IDLOCAL=@L AND FECHA BETWEEN @D AND @H {whereArt} ORDER BY FECHA DESC",
                    new { L = idLocal, D = desde, H = hasta, A = idArt ?? 0 })).ToList();
                _grid.ItemsSource = rows;
                _lblCont.Text = $"Movimientos: {rows.Count}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }
    }
}
