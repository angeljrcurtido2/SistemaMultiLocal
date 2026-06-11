using CrediSoft.Core.Services;
using CrediSoft.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Informes;

// ══════════════════════════════════════════════════════════════════════════════
//  COBROS PENDIENTES  (BUSCAR_GENERADAS_PENDIENTES_CS — requiere ID cab)
//  Muestra listado de cuotas generadas pendientes de cobro de TODAS las ventas
// ══════════════════════════════════════════════════════════════════════════════
public class PendientesWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtFiltro = null!;
    private DataGrid  _grid      = null!;
    private TextBlock _lblInfo   = null!;

    public PendientesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Cobros Pendientes"; Width = 960; Height = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar(null);
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Cobros Pendientes de Pago", "#E67E22", Dock.Top));

        var bottom = BotBar(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new DockPanel { Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "Buscar cliente:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtFiltro = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 200, Margin = new Thickness(0, 0, 6, 0) };
        _txtFiltro.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Cargar(_txtFiltro.Text.Trim()); };
        var btnB = UiHelpers.Btn("Buscar", "#FF8C00"); btnB.Click += async (_, _) => await Cargar(_txtFiltro.Text.Trim());
        var btnR = UiHelpers.Btn("↺ Todos", "#2980B9"); btnR.Click += async (_, _) => { _txtFiltro.Text = ""; await Cargar(null); };
        filterBar.Children.Add(_txtFiltro); filterBar.Children.Add(btnB); filterBar.Children.Add(btnR);
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };
        filterBar.Children.Add(_lblInfo);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("N° Solicitud", "NSOLICITUD",    100));
        _grid.Columns.Add(UiHelpers.Col("Cliente",       "CLIENTE",      new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(UiHelpers.Col("Debe",          "DEBE",          80));
        _grid.Columns.Add(UiHelpers.Col("Cuotas",        "CUOTAS",        60));
        _grid.Columns.Add(UiHelpers.Col("Monto/Cuota",   "MONTO_CUOTA",   90));
        _grid.Columns.Add(UiHelpers.Col("N°Cuota",       "NCUOTA",        65));
        _grid.Columns.Add(UiHelpers.Col("Comprobante",   "COMPROBANTE",   110));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar(string? filtro)
    {
        try
        {
            using var conn = _db.Create();
            // Cargar todas las cabeceras activas con deuda > 0 y buscar pendientes
            var cabs = await conn.QueryAsync<dynamic>(
                "SELECT IDCAB FROM CABECERA_SALES WHERE ESTADO=1 AND DEBE>0");

            var todos = new List<dynamic>();
            foreach (var cab in cabs)
            {
                var p = new DynamicParameters();
                p.Add("@Idcab", (int)cab.IDCAB);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                var rows = await conn.QueryAsync<dynamic>("BUSCAR_GENERADAS_PENDIENTES_CS", p, commandType: CommandType.StoredProcedure);
                todos.AddRange(rows);
            }

            IEnumerable<dynamic> result = todos;
            if (!string.IsNullOrWhiteSpace(filtro))
                result = todos.Where(r => ((string?)r.CLIENTE ?? "").Contains(filtro, StringComparison.OrdinalIgnoreCase));

            var lista = result.ToList();
            _grid.ItemsSource = lista;
            _lblInfo.Text = $"{lista.Count} cuotas pendientes";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private StackPanel BotBar()
    {
        var b = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close(); b.Children.Add(btnC); return b;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ARTÍCULOS EN PROMOCIÓN
// ══════════════════════════════════════════════════════════════════════════════
public class EnPromocionWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid  _grid    = null!;
    private TextBlock _lblInfo = null!;

    public EnPromocionWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Artículos en Promoción"; Width = 860; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Artículos en Promoción", "#8E44AD", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnR = UiHelpers.Btn("↺ Refrescar", "#2980B9"); btnR.Click += async (_, _) => await Cargar();
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnR); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        _lblInfo = new TextBlock { Margin = new Thickness(8, 4, 8, 0), Foreground = System.Windows.Media.Brushes.Gray };
        DockPanel.SetDock(_lblInfo, Dock.Top); root.Children.Add(_lblInfo);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("Local",     "LOCAL",    120));
        _grid.Columns.Add(UiHelpers.Col("Código",    "CODIGO",    70));
        _grid.Columns.Add(UiHelpers.Col("Artículo",  "ARTICULO", new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(UiHelpers.Col("Inicio",    "INICIO",   100));
        _grid.Columns.Add(UiHelpers.Col("Fin",       "FIN",      100));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_TODOS_ARTICULOS_EN_PROMOCION_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
            _lblInfo.Text = $"{rows.Count} artículos en promoción";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  VISOR PROMOCIÓN (artículos en promo del local actual)
// ══════════════════════════════════════════════════════════════════════════════
public class VisorPromoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid  _grid    = null!;
    private TextBlock _lblInfo = null!;

    public VisorPromoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Visor de Promociones"; Width = 820; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Visor de Promociones — Local Actual", "#16A085", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnR = UiHelpers.Btn("↺ Refrescar", "#2980B9"); btnR.Click += async (_, _) => await Cargar();
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnR); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        _lblInfo = new TextBlock { Margin = new Thickness(8, 4, 8, 0), Foreground = System.Windows.Media.Brushes.Gray };
        DockPanel.SetDock(_lblInfo, Dock.Top); root.Children.Add(_lblInfo);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("Código",   "CODIGO",   70));
        _grid.Columns.Add(UiHelpers.Col("Artículo", "ARTICULO", new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(UiHelpers.Col("Inicio",   "INICIO",   100));
        _grid.Columns.Add(UiHelpers.Col("Fin",      "FIN",      100));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        var local = SessionService.Instance.LocalActual;
        if (local == null) return;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@idlocal", (byte)local.IdLocal);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_ARTICULOS_EN_PROMOCION_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
            _lblInfo.Text = $"{rows.Count} artículos en promoción en {local.NombreLocal}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE COMPRAS
// ══════════════════════════════════════════════════════════════════════════════
public class HComprasWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtComprobante = null!;
    private DataGrid  _grid           = null!;
    private TextBlock _lblInfo        = null!;

    public HComprasWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Historial de Compras"; Width = 860; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Historial de Compras", "#1ABC9C", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new DockPanel { Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "N° Comprobante:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtComprobante = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 160, Margin = new Thickness(0, 0, 6, 0) };
        _txtComprobante.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Cargar(); };
        var btnB = UiHelpers.Btn("Buscar", "#FF8C00"); btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(_txtComprobante); filterBar.Children.Add(btnB);
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };
        filterBar.Children.Add(_lblInfo);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("Código",      "CODIGO",      80));
        _grid.Columns.Add(UiHelpers.Col("Descripción", "DESCRIPCION", new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(UiHelpers.Col("P.Costo",     "PCOSTO",      80));
        _grid.Columns.Add(UiHelpers.Col("Cantidad",    "CANTIDAD",    80));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        var comp = _txtComprobante.Text.Trim();
        if (string.IsNullOrEmpty(comp)) { MessageBox.Show("Ingrese número de comprobante.", "Aviso"); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@comprobante", comp);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
            var rows = (await conn.QueryAsync<dynamic>("QUERY_COMPRAS_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
            _lblInfo.Text = $"{rows.Count} artículos";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE TRANSFERENCIAS
// ══════════════════════════════════════════════════════════════════════════════
public class HTransferenciasWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid  _grid    = null!;
    private TextBlock _lblInfo = null!;

    public HTransferenciasWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Historial de Transferencias"; Width = 900; Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Historial de Transferencias", "#2980B9", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnR = UiHelpers.Btn("↺ Refrescar", "#2980B9"); btnR.Click += async (_, _) => await Cargar();
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnR); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        _lblInfo = new TextBlock { Margin = new Thickness(8, 4, 8, 0), Foreground = System.Windows.Media.Brushes.Gray };
        DockPanel.SetDock(_lblInfo, Dock.Top); root.Children.Add(_lblInfo);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("N° Remito",  "NUMERO_REM",    150));
        _grid.Columns.Add(UiHelpers.Col("Origen",     "ORIGEN",        130));
        _grid.Columns.Add(UiHelpers.Col("Destino",    "DESTINO",       130));
        _grid.Columns.Add(UiHelpers.Col("Total",      "TOTALCONTADO",   90));
        _grid.Columns.Add(UiHelpers.Col("Estado",     "ESTADO_TXT",     80));
        _grid.Columns.Add(UiHelpers.Col("Fecha",      "FECHA",         120));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT r.NUMERO_REM_TMP as NUMERO_REM, " +
                "lo.NOMBRE as ORIGEN, ld.NOMBRE as DESTINO, " +
                "r.TOTALCONTADO, r.FECHA, " +
                "CASE r.ESTADO WHEN 0 THEN 'Pendiente' WHEN 1 THEN 'Aceptado' WHEN 2 THEN 'Anulado' ELSE '?' END as ESTADO_TXT " +
                "FROM CAB_REMITO_TMP r " +
                "LEFT JOIN LOCALES lo ON r.IDORIGENTMP=lo.ID_LOCAL " +
                "LEFT JOIN LOCALES ld ON r.IDDESTINOTMP=ld.ID_LOCAL " +
                "ORDER BY r.FECHA DESC");
            var lista = rows.ToList();
            _grid.ItemsSource = lista;
            _lblInfo.Text = $"{lista.Count} transferencias";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  HISTORIAL DE CAJA
// ══════════════════════════════════════════════════════════════════════════════
public class CajaHistorialWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DatePicker _dtDesde = null!, _dtHasta = null!;
    private DataGrid   _grid    = null!;
    private TextBlock  _lblInfo = null!;

    public CajaHistorialWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Historial de Caja"; Width = 980; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Historial de Caja", "#27AE60", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "Desde:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _dtDesde = new DatePicker { Width = 120, SelectedDate = DateTime.Today.AddMonths(-1), Margin = new Thickness(0, 0, 8, 0) };
        filterBar.Children.Add(_dtDesde);
        filterBar.Children.Add(new TextBlock { Text = "Hasta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _dtHasta = new DatePicker { Width = 120, SelectedDate = DateTime.Today, Margin = new Thickness(0, 0, 8, 0) };
        filterBar.Children.Add(_dtHasta);
        var btnB = UiHelpers.Btn("Buscar", "#FF8C00"); btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(btnB);
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };
        filterBar.Children.Add(_lblInfo);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("Fecha",     "FECHA",     130));
        _grid.Columns.Add(UiHelpers.Col("Local",     "ID_LOCAL",   55));
        _grid.Columns.Add(UiHelpers.Col("Usuario",   "USUARIO",   100));
        _grid.Columns.Add(UiHelpers.Col("Acción",    "ACCION",     55));
        _grid.Columns.Add(UiHelpers.Col("Concepto",  "CONCEPTO",  100));
        _grid.Columns.Add(UiHelpers.Col("Monto",     "MONTO",      80));
        _grid.Columns.Add(UiHelpers.Col("Método",    "METODO",     70));
        _grid.Columns.Add(UiHelpers.Col("N°",        "NUMERO",     90));
        _grid.Columns.Add(UiHelpers.Col("Recibidor", "RECIBIDOR",  100));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        var desde = _dtDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
        var hasta = (_dtHasta.SelectedDate ?? DateTime.Today).AddDays(1);
        var local = SessionService.Instance.LocalActual;
        if (local == null) return;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@FechaInicio", desde);
            p.Add("@FechaFin",    hasta);
            p.Add("@IDLocal",     local.IdLocal);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_HIST_CAJA_LOCAL_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
            var total = rows.Sum(r => (decimal)r.MONTO);
            _lblInfo.Text = $"{rows.Count} movimientos — Total: {total:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  VER GASTOS DE CAJA
// ══════════════════════════════════════════════════════════════════════════════
public class CajaGastosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DatePicker _dtDesde = null!, _dtHasta = null!;
    private DataGrid   _grid    = null!;
    private TextBlock  _lblInfo = null!;

    public CajaGastosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Ver Gastos de Caja"; Width = 820; Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        root.Children.Add(UiHelpers.MakeHdr("Gastos de Caja", "#E74C3C", Dock.Top));

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = UiHelpers.Btn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "Desde:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _dtDesde = new DatePicker { Width = 120, SelectedDate = DateTime.Today.AddMonths(-1), Margin = new Thickness(0, 0, 8, 0) };
        filterBar.Children.Add(_dtDesde);
        filterBar.Children.Add(new TextBlock { Text = "Hasta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _dtHasta = new DatePicker { Width = 120, SelectedDate = DateTime.Today, Margin = new Thickness(0, 0, 8, 0) };
        filterBar.Children.Add(_dtHasta);
        var btnB = UiHelpers.Btn("Buscar", "#FF8C00"); btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(btnB);
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };
        filterBar.Children.Add(_lblInfo);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = UiHelpers.MakeGrid();
        _grid.Columns.Add(UiHelpers.Col("Fecha",    "FECHA",      130));
        _grid.Columns.Add(UiHelpers.Col("Concepto", "CONCEPTO",   new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(UiHelpers.Col("Monto",    "MONTO",       90));
        _grid.Columns.Add(UiHelpers.Col("Usuario",  "USUARIO",    100));
        _grid.Columns.Add(UiHelpers.Col("Observ.",  "OBSERVACION",130));
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        var desde = _dtDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
        var hasta = (_dtHasta.SelectedDate ?? DateTime.Today).AddDays(1);
        var local = SessionService.Instance.LocalActual;
        if (local == null) return;
        try
        {
            using var conn = _db.Create();
            // Gastos = ACCION=2 (egreso/gasto) en historial de caja
            var p = new DynamicParameters();
            p.Add("@FechaInicio", desde);
            p.Add("@FechaFin",    hasta);
            p.Add("@IDLocal",     local.IdLocal);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_HIST_CAJA_LOCAL_CS", p, commandType: CommandType.StoredProcedure))
                .Where(r => (byte)r.ACCION == 2)
                .ToList();
            _grid.ItemsSource = rows;
            var total = rows.Sum(r => (decimal)r.MONTO);
            _lblInfo.Text = $"{rows.Count} gastos — Total: {total:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
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

