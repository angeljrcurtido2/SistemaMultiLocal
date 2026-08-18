using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Compras;
using CrediSoft.UI.Views.Maestros;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// modelos para las grillas de Atrasos
internal class DetalleArticuloAtraso
{
    public int    IdArt          { get; set; }
    public string ArticuloCodigo { get; set; } = "";
    public string ArticuloNombre { get; set; } = "";
    public decimal Cantidad      { get; set; }
    public decimal Pv            { get; set; }
}

internal class ResumenLocalAtraso
{
    public string  Local         { get; set; } = "";
    public decimal TotalCredito  { get; set; }
    public decimal TotalCobrar   { get; set; }
    public decimal TotalAtrasos  { get; set; }
    public decimal Atr1a30       { get; set; }
    public decimal Atr31a60      { get; set; }
    public decimal Atr61a90      { get; set; }
    public decimal AtrMas90      { get; set; }
}

namespace CrediSoft.UI.Views.Informes
{
    // ── ATRASOS ────────────────────────────────────────────────────────────────
    public partial class AtrasosWindow : Window
    {
        private readonly ICuotaRepository     _cuotas;
        private readonly IDbConnectionFactory _db;
        private int?   _localFiltroId  = null;
        private string _vendedorFiltro = "";
        private List<Cuota> _todosSinFiltrarVendedor = new();
        private System.Windows.Threading.DispatcherTimer? _debounceTimer;

        public AtrasosWindow()
        {
            InitializeComponent();
            _cuotas = App.Services.GetRequiredService<ICuotaRepository>();
            _db     = App.Services.GetRequiredService<IDbConnectionFactory>();
            DpDesde.SelectedDate = DateTime.Today.AddMonths(-1);
            DpHasta.SelectedDate = DateTime.Today;
        }

        // Dispara búsqueda inmediata al cambiar fechas
        private async void OnFiltroAutoSearch(object s, SelectionChangedEventArgs e)
        {
            if (DpDesde == null) return;
            await Buscar();
        }

        // Debounce para TextBoxes (espera 600ms tras última tecla)
        private void OnFiltroTextChanged(object s, TextChangedEventArgs e)
        {
            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(600) };
                _debounceTimer.Tick += async (_, _) =>
                {
                    _debounceTimer.Stop();
                    await Buscar();
                };
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void OnCriterioChanged(object s, RoutedEventArgs e)
        {
            if (DpDesde == null || TxtDias == null || TxtIntDesde == null) return;
            DpDesde.IsEnabled     = DpHasta.IsEnabled     = RbPeriodo.IsChecked  == true;
            TxtDias.IsEnabled     = RbDias.IsChecked      == true;
            TxtIntDesde.IsEnabled = TxtIntHasta.IsEnabled = RbIntervalo.IsChecked == true;

            if (BtnCompleto == null) return;
            var todosActivo = RbTodos.IsChecked == true;
            BtnCompleto.IsEnabled = !todosActivo;
            if (BtnCompleto.ToolTip is System.Windows.Controls.ToolTip tt)
                tt.Content = todosActivo
                    ? "El filtro completo ya está aplicado"
                    : "Ver todos los atrasos sin filtro";

            // Auto-buscar al cambiar el radio activo
            _ = Buscar();
        }

        private void OnSeleccionarLocal(object s, RoutedEventArgs e)
        {
            var dlg = new CrediSoft.UI.Views.Compras.BuscadorLocalModal(_db)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dlg.ShowDialog() == true && dlg.LocalSeleccionado != null)
            {
                _localFiltroId             = dlg.LocalSeleccionado.IdLocal;
                TxtLocal.Text              = dlg.LocalSeleccionado.Nombre;
                TxtLocal.FontStyle         = FontStyles.Normal;
                BtnLimpiarLocal.Visibility = Visibility.Visible;
                _ = Buscar();
            }
        }

        private void OnLimpiarLocal(object s, RoutedEventArgs e)
        {
            _localFiltroId             = null;
            TxtLocal.Text              = "(todos)";
            TxtLocal.FontStyle         = FontStyles.Italic;
            BtnLimpiarLocal.Visibility = Visibility.Collapsed;
            _ = Buscar();
        }

        private async void OnBuscar(object s, RoutedEventArgs e) => await Buscar();

        private async void OnCompleto(object s, RoutedEventArgs e)
        {
            RbTodos.IsChecked     = true;
            BtnCompleto.IsEnabled = false;
            // Resetear filtro de local
            _localFiltroId             = null;
            TxtLocal.Text              = "(todos)";
            TxtLocal.FontStyle         = FontStyles.Italic;
            BtnLimpiarLocal.Visibility = Visibility.Collapsed;
            await Buscar();
        }

        private async Task Buscar()
        {
            if (DpDesde == null || _cuotas == null) return;  // guard durante init

            int? diasMin = null, diasMax = null;
            if (RbDias.IsChecked == true && int.TryParse(TxtDias.Text, out var d))
                diasMin = d;
            if (RbIntervalo.IsChecked == true)
            {
                if (int.TryParse(TxtIntDesde.Text, out var id)) diasMin = id;
                if (int.TryParse(TxtIntHasta.Text, out var ih)) diasMax = ih;
            }

            int? local = _localFiltroId;
            var todos = (await _cuotas.BuscarAtrasosAsync(local, diasMin)).ToList();

            // "Todos los atrasos" = solo cuotas que ya superaron el período de gracia de 5
            // días (Mora > 0, confirmado explícitamente: una cuota vencida hace 1-4 días NO
            // cuenta como atraso todavía). La query ya trae también las próximas a vencer
            // para poder mostrarlas cuando el usuario elige Período/Días/Intervalo.
            if (RbTodos.IsChecked == true)
                todos = todos.Where(c => c.Mora > 0).ToList();

            // Filtro Período (lado cliente)
            if (RbPeriodo.IsChecked == true)
            {
                var desde = DpDesde.SelectedDate ?? DateTime.MinValue;
                var hasta = DpHasta.SelectedDate ?? DateTime.MaxValue;
                todos = todos.Where(c => c.Vto >= desde && c.Vto <= hasta).ToList();
            }
            // Filtro Intervalo máximo (lado cliente)
            if (diasMax.HasValue)
                todos = todos.Where(c => c.Mora <= diasMax.Value).ToList();

            // Orden explícito por días de mora — el SP legado no tiene ORDER BY garantizado
            // (perseguir el orden natural exacto del viejo resultó frágil e inconsistente),
            // así que se deja como criterio elegible por el usuario en vez de adivinarlo.
            todos = CboOrdenMora.SelectedIndex == 1
                ? todos.OrderBy(c => c.Mora).ToList()
                : todos.OrderByDescending(c => c.Mora).ToList();

            // Guardar para filtro vendedor en memoria
            _todosSinFiltrarVendedor = todos;
            AplicarFiltroVendedor();
        }

        private Dictionary<int, string> _nombresLocales = new();

        private async Task CargarNombresLocalesAsync()
        {
            if (_nombresLocales.Count > 0) return;
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<(int Id, string Nombre)>(
                "SELECT ID_LOCAL as Id, NOMBRE as Nombre FROM LOCALES");
            _nombresLocales = rows.ToDictionary(r => r.Id, r => r.Nombre);
        }

        private async Task ActualizarResumenAsync(List<Cuota> todos)
        {
            await CargarNombresLocalesAsync();

            var filas = todos
                .GroupBy(c => c.IdLocal)
                .OrderBy(g => g.Key)
                .Select(g => {
                    var nombre = _nombresLocales.TryGetValue(g.Key, out var n) ? n : $"Local {g.Key}";
                    return new ResumenLocalAtraso {
                        Local        = nombre,
                        TotalAtrasos = g.Sum(c => c.Monto),
                        Atr1a30      = g.Where(c => c.Mora >= 1  && c.Mora <= 30).Sum(c => c.Monto),
                        Atr31a60     = g.Where(c => c.Mora >= 31 && c.Mora <= 60).Sum(c => c.Monto),
                        Atr61a90     = g.Where(c => c.Mora >= 61 && c.Mora <= 90).Sum(c => c.Monto),
                        AtrMas90     = g.Where(c => c.Mora > 90).Sum(c => c.Monto),
                    };
                }).ToList();

            // Fila de totales generales
            if (filas.Count > 0)
            {
                filas.Add(new ResumenLocalAtraso {
                    Local        = "── TOTALES GENERALES",
                    TotalAtrasos = filas.Sum(f => f.TotalAtrasos),
                    Atr1a30      = filas.Sum(f => f.Atr1a30),
                    Atr31a60     = filas.Sum(f => f.Atr31a60),
                    Atr61a90     = filas.Sum(f => f.Atr61a90),
                    AtrMas90     = filas.Sum(f => f.AtrMas90),
                });
            }

            GridResumen.ItemsSource = filas;
        }

        private async void OnMorosoSeleccionado(object s, SelectionChangedEventArgs e)
        {
            if (GridMorosos.SelectedItem is not Cuota cuota) return;
            await CargarArticulosDeAsync(cuota);
        }

        // WPF no dispara SelectionChanged si el usuario hace doble clic sobre una fila que
        // YA estaba seleccionada (el ítem seleccionado no cambia) — reportado: los artículos
        // no aparecían al hacer doble clic repetido en la misma fila. Este handler recarga
        // siempre, sin depender de que la selección haya cambiado.
        private async void OnMorosoDobleClick(object s, MouseButtonEventArgs e)
        {
            if (GridMorosos.SelectedItem is not Cuota cuota) return;
            await CargarArticulosDeAsync(cuota);
        }

        private async Task CargarArticulosDeAsync(Cuota cuota)
        {
            try
            {
                using var conn = _db.Create();

                // Cargar artículos de esa venta
                var arts = await conn.QueryAsync<DetalleArticuloAtraso>(
                    @"SELECT d.IDART, CAST(a.CA AS NVARCHAR(50)) as ArticuloCodigo,
                             a.D as ArticuloNombre, d.CANTIDAD, d.PV as Pv
                      FROM DETALLES_SALES d
                      JOIN ARTICULOS a ON a.ID = d.IDART
                      WHERE d.IDCAB = @IdCab",
                    new { IdCab = cuota.IdCab });
                GridProductos.ItemsSource = arts.ToList();

                // El resumen ya está calculado al hacer la búsqueda
            }
            catch (Exception ex)
            {
                GridProductos.ItemsSource = null;
                System.Diagnostics.Debug.WriteLine($"Error cargando detalle: {ex.Message}");
            }
        }

        private async void OnResumenLocal(object s, RoutedEventArgs e)
        {
            var todos = (GridMorosos.ItemsSource as IEnumerable<Cuota>)?.ToList() ?? new();
            if (todos.Count == 0) { MessageBox.Show("Primero ejecute una búsqueda.", "Sin datos"); return; }
            await ActualizarResumenAsync(todos);
        }

        private void AplicarFiltroVendedor()
        {
            var resultados = string.IsNullOrEmpty(_vendedorFiltro)
                ? _todosSinFiltrarVendedor
                : _todosSinFiltrarVendedor
                    .Where(c => c.VendedorNombre.Contains(_vendedorFiltro, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            GridMorosos.ItemsSource = resultados;
            var total = resultados.Sum(c => c.Monto);
            LblConteo.Text       = $"{resultados.Count} cuota(s) en atraso";
            LblTotalMorosos.Text = $"Total en atraso: Gs. {total:N0}";
            _ = ActualizarResumenAsync(resultados);
            GridProductos.ItemsSource = null;
        }

        private void OnSeleccionarVendedor(object s, RoutedEventArgs e)
        {
            var vendedores = _todosSinFiltrarVendedor
                .Select(c => c.VendedorNombre)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            if (!vendedores.Any())
            {
                MessageBox.Show("No hay datos cargados. Ejecute una búsqueda primero.",
                    "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var modal = new BuscadorVendedorAtrasoModal(vendedores) { Owner = this };
            if (modal.ShowDialog() != true || modal.VendedorSeleccionado == null) return;
            _vendedorFiltro               = modal.VendedorSeleccionado;
            TxtVendedorNombre.Text        = modal.VendedorSeleccionado;
            TxtVendedorNombre.Visibility  = Visibility.Visible;
            BtnLimpiarVendedor.Visibility = Visibility.Visible;
            AplicarFiltroVendedor();
        }

        private void OnLimpiarVendedor(object s, RoutedEventArgs e)
        {
            _vendedorFiltro               = "";
            TxtVendedorNombre.Text        = "";
            TxtVendedorNombre.Visibility  = Visibility.Collapsed;
            BtnLimpiarVendedor.Visibility = Visibility.Collapsed;
            AplicarFiltroVendedor();
        }

        private async void OnVistaPrevia(object s, RoutedEventArgs e)
        {
            var p = await BuildPaginaAsync();
            if (p == null) return;
            new AtrasosMorososPreviewWindow(p) { Owner = this }.ShowDialog();
        }

        private async void OnImprimir(object s, RoutedEventArgs e)
        {
            var p = await BuildPaginaAsync();
            if (p == null) return;
            AtrasosImpresora.ImprimirMorosos(p, this);
        }

        // Datos por venta (artículo + garante) para el reporte de tarjetas — una sola query
        // por lote de IdCab visibles en la grilla, no N+1 por cada moroso.
        private class DetalleVenta
        {
            public int    IdCab          { get; set; }
            public string ArticuloDesc   { get; set; } = "";
            public decimal ArticuloPrecio{ get; set; }
            public string GaranteNombre  { get; set; } = "";
            public string GaranteTel     { get; set; } = "";
        }

        private async Task<Dictionary<int, DetalleVenta>> CargarDetallesVentaAsync(IEnumerable<int> idsCab)
        {
            var ids = idsCab.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, DetalleVenta>();

            using var conn = _db.Create();
            // El garante "NO DEFINIDO" (ID_G=1) es el registro genérico al que apuntan casi
            // todos los créditos que nunca cargaron un garante real — se muestra como "XXX"
            // en ambos campos (igual que ya sale su teléfono) para que coincida con lo que el
            // usuario ya reconoce del sistema viejo y no lo confunda con un dato real.
            var filas = await conn.QueryAsync<DetalleVenta>(
                @"SELECT CB.IDCAB,
                         ISNULL((SELECT TOP 1 a.D FROM DETALLES_SALES d
                                 JOIN ARTICULOS a ON a.ID = d.IDART
                                 WHERE d.IDCAB = CB.IDCAB), '') AS ArticuloDesc,
                         ISNULL((SELECT TOP 1 d.PV FROM DETALLES_SALES d
                                 WHERE d.IDCAB = CB.IDCAB), 0) AS ArticuloPrecio,
                         CASE WHEN G.NOMBRE = 'NO DEFINIDO' THEN 'XXX' ELSE ISNULL(G.NOMBRE, '') END AS GaranteNombre,
                         ISNULL(G.TELEFONO, '') AS GaranteTel
                  FROM CABECERA_SALES CB
                  LEFT JOIN GARANTES G ON G.ID_G = CB.ID_GARANTE
                  WHERE CB.IDCAB IN @Ids",
                new { Ids = ids });

            return filas.ToDictionary(f => f.IdCab);
        }

        private async Task<AtrasosPagina?> BuildPaginaAsync()
        {
            var morosos = (GridMorosos.ItemsSource as IEnumerable<Cuota>)?.ToList();
            if (morosos == null || morosos.Count == 0)
            {
                MessageBox.Show("No hay datos para imprimir. Ejecute una búsqueda primero.",
                    "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            var resumen = (GridResumen.ItemsSource as IEnumerable<ResumenLocalAtraso>)?.ToList()
                          ?? new List<ResumenLocalAtraso>();

            var detalles = await CargarDetallesVentaAsync(morosos.Select(c => c.IdCab));

            // Descripción del filtro activo
            var partes = new System.Collections.Generic.List<string>();
            if (RbPeriodo.IsChecked == true)
            {
                var desde = DpDesde.SelectedDate?.ToString("dd/MM/yyyy") ?? "—";
                var hasta = DpHasta.SelectedDate?.ToString("dd/MM/yyyy") ?? "—";
                partes.Add($"Período: {desde} → {hasta}");
            }
            else if (RbDias.IsChecked == true && !string.IsNullOrEmpty(TxtDias.Text))
                partes.Add($"Mora ≥ {TxtDias.Text} días");
            else if (RbIntervalo.IsChecked == true)
                partes.Add($"Mora: {TxtIntDesde.Text}–{TxtIntHasta.Text} días");

            var localTxt = TxtLocal.Text.Trim();
            if (localTxt != "(todos)" && !string.IsNullOrEmpty(localTxt))
                partes.Add($"Local: {localTxt}");

            var filtroDesc = partes.Count > 0 ? string.Join("   |   ", partes) : "";

            return new AtrasosPagina
            {
                Morosos  = morosos.Select(c =>
                {
                    detalles.TryGetValue(c.IdCab, out var det);
                    return new AtrasosFilaMoroso
                    {
                        Cliente        = c.ClienteNombre   ?? "",
                        Telefono       = c.ClienteTelefono ?? "",
                        Solicitud      = c.NSolicitud       ?? "",
                        NCuota         = c.NCuotaVisible,
                        Monto          = c.Monto,
                        Vto            = c.Vto.ToString("dd/MM/yyyy"),
                        Mora           = c.Mora,
                        Local          = c.IdLocal.ToString(),
                        Vendedor       = c.VendedorNombre  ?? "",
                        ClienteCi      = c.ClienteCi ?? "",
                        GaranteNombre  = det?.GaranteNombre ?? "",
                        GaranteTel     = det?.GaranteTel ?? "",
                        ArticuloDesc   = det?.ArticuloDesc ?? "",
                        ArticuloPrecio = det?.ArticuloPrecio ?? 0,
                    };
                }).ToList(),
                Resumen  = resumen.Select(r => new AtrasosFilaResumen
                {
                    Local     = r.Local,
                    Total     = r.TotalAtrasos,
                    Atr1a30   = r.Atr1a30,
                    Atr31a60  = r.Atr31a60,
                    Atr61a90  = r.Atr61a90,
                    AtrMas90  = r.AtrMas90,
                    EsTotales = r.Local.StartsWith("──"),
                }).ToList(),
                TotalGs  = morosos.Sum(c => c.Monto),
                Filtro   = filtroDesc,
                FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
                LogoPath = CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.Escape)
                Close();
        }
    }

    // ── CONVERTERS ───────────────────────────────────────────────────────────
    internal class MoraColorConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
            => v is int mora && mora > 0;
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotImplementedException();
    }

    // "0" → "—"  |  "3" → "3d"
    internal class MoraTextConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        {
            if (v is int mora) return mora > 0 ? $"{mora}d" : "—";
            return "—";
        }
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotImplementedException();
    }

    // "000000000008220" → "#8220"  (últimos 6 chars sin ceros líderes)
    internal class SolicitudShortConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        {
            var s = v?.ToString() ?? "";
            var trimmed = s.TrimStart('0');
            return string.IsNullOrEmpty(trimmed) ? s : "#" + trimmed;
        }
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotImplementedException();
    }

    // ── MODELOS INTERNOS ─────────────────────────────────────────────────────
    internal class FilaCobro
    {
        public int     IdGeneradas  { get; set; }
        public int     IdCab        { get; set; }
        public int     IdCliente    { get; set; }
        public string  FechaCobrado { get; set; } = "";
        public string  Local        { get; set; } = "";
        public string  Cliente      { get; set; } = "";
        public string  CI           { get; set; } = "";
        public string  Solicitud    { get; set; } = "";
        public string  Cuota        { get; set; } = "";
        // Usados solo para resolver el local REAL del cobro (ver CorregirLocalCobrosAsync) —
        // GENERADAS.IDLOCAL es el local de ORIGEN del crédito, no dónde se cobró cada evento.
        public string  Comprobante  { get; set; } = "";
        public byte    NCuotaNum    { get; set; }
        public int?    LocalIdReal  { get; set; }
        public string  Cobrador     { get; set; } = "";
        public string  Vendedor     { get; set; } = "";
        public string  Telefono     { get; set; } = "";
        public decimal Monto        { get; set; }
        public decimal Punitorio    { get; set; }
        public decimal Total        { get; set; }
        public string  Vto          { get; set; } = "";
        public int     Mora         { get; set; }
        public string  Obs          { get; set; } = "";

        // Lo efectivamente cobrado en ESTE evento puntual — distinto de Total (que es el saldo
        // de la cuota, no lo pagado hoy). Para pagos que completaron la cuota, coincide con
        // Total (todo se pagó de una vez); para abonos parciales, es el monto real de ESE abono
        // (GENERADAS.ENTREGA/HISTORIAL_ENTREGAS_GENERADAS.ENTREGA de esa fecha específica, no el
        // acumulado). Bug real: con varios abonos parciales sobre la misma cuota, la grilla
        // mostraba el Total (saldo restante decreciente) en cada fila, pareciendo un cobro
        // completo duplicado en vez de dos abonos parciales distintos.
        public decimal MontoEvento  { get; set; }

        // El cobro se registró a nombre de un vendedor distinto de quien vendió el crédito
        // (ver CobrosWindow, badge "Cobrado por") — resalta la fila en el informe para que el
        // dueño del negocio lo note de un vistazo, sin tener que abrir el detalle de cada fila.
        public bool EsCobradorDistinto =>
            !string.IsNullOrWhiteSpace(Cobrador) && !string.IsNullOrWhiteSpace(Vendedor) &&
            !string.Equals(Cobrador.Trim(), Vendedor.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal class FilaArticuloCobro
    {
        public string  Articulo  { get; set; } = "";
        public decimal Cantidad  { get; set; }
        public decimal Precio    { get; set; }
        public decimal Subtotal  { get; set; }
    }

    // ── HISTORIAL COBRANZAS ───────────────────────────────────────────────────
    public class HCobranzasWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService      _session;

        // Grilla principal y panel detalle
        private DataGrid   _gridCobros    = null!;
        private DataGrid   _gridArts      = null!;
        private Border     _panelDetalle  = null!;

        // Campos del panel detalle
        private TextBlock _detCliente  = null!, _detCI      = null!, _detLocal   = null!;
        private TextBlock _detFecha    = null!, _detSol     = null!, _detCuota   = null!;
        private TextBlock _detCobrador = null!, _detVendedor= null!, _detVto     = null!;
        private TextBlock _detMora     = null!, _detMonto   = null!, _detPunit   = null!;
        private TextBlock _detTotal    = null!, _detObs     = null!;

        // Filtros
        private DatePicker   _dpDesde        = null!, _dpHasta = null!;
        private TextBox      _txtLocal       = null!, _txtCliente = null!;
        private TextBlock    _lblTotal       = null!, _lblConteo = null!;
        private Button       _btnQuitarLocal = null!, _btnVerTodos = null!, _btnSelLocal = null!;
        private RadioButton  _rbPeriodo      = null!, _rbTodos = null!;
        private int?         _localFiltroId   = null;
        private int?         _clienteFiltroId = null;
        private List<FilaCobro> _todos = new();

        private static System.Windows.Media.SolidColorBrush Br(string hex) =>
            new(System.Windows.Media.ColorConverter.ConvertFromString(hex) is System.Windows.Media.Color c ? c
                : System.Windows.Media.Colors.Gray);

        // Style para DataGridColumnHeader que preserva el template nativo (con flecha de sort)
        // pero sobreescribe colores. La flecha nativa de WPF usa la propiedad SortDirection
        // del header — funciona automáticamente si CanUserSortColumns=true y SortMemberPath está seteado.
        private static Style BuildSortableHeaderStyle()
        {
            var orange     = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A4F6E"));
            var orangeDark = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A4F6E"));
            var white      = System.Windows.Media.Brushes.White;

            // ControlTemplate con Grid: [texto estrella | flecha sort | thumb resize]
            var ct = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));

            var outerBorder = new FrameworkElementFactory(typeof(Border));
            outerBorder.Name = "OuterBorder";
            outerBorder.SetValue(Border.BackgroundProperty, orange);
            outerBorder.SetValue(Border.BorderBrushProperty, orangeDark);
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

            // Texto del header
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

            // Flecha sort — StackPanel con dos Path, visibilidad controlada por triggers
            var arrowStack = new FrameworkElementFactory(typeof(StackPanel));
            arrowStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            arrowStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowStack.SetValue(StackPanel.MarginProperty, new Thickness(4, 0, 4, 0));
            arrowStack.SetValue(Grid.ColumnProperty, 1);

            var pathAsc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            pathAsc.Name = "SortAsc";
            pathAsc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,4 L 4,0 L 8,4 Z"));
            pathAsc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
            pathAsc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 0, 1));
            pathAsc.SetValue(VisibilityProperty, Visibility.Collapsed);

            var pathDesc = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            pathDesc.Name = "SortDesc";
            pathDesc.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
            pathDesc.SetValue(System.Windows.Shapes.Path.FillProperty, white);
            pathDesc.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 1, 0, 0));
            pathDesc.SetValue(VisibilityProperty, Visibility.Collapsed);

            arrowStack.AppendChild(pathAsc);
            arrowStack.AppendChild(pathDesc);
            grid.AppendChild(arrowStack);

            // Thumb resize (columna 2)
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

            // Triggers dentro del ControlTemplate para mostrar flechas
            var trigAsc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Ascending };
            trigAsc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortAsc"));
            ct.Triggers.Add(trigAsc);

            var trigDesc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Descending };
            trigDesc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortDesc"));
            ct.Triggers.Add(trigDesc);

            // Trigger hover: fondo más oscuro
            var trigHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigHover.Setters.Add(new Setter(Border.BackgroundProperty, orangeDark, "OuterBorder"));
            ct.Triggers.Add(trigHover);

            var style = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.TemplateProperty, ct));
            return style;
        }

        public HCobranzasWindow()
        {
            _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title    = "Informe de Cobranzas";
            // Pedido explícito: ensanchar para que la grilla (izquierda) y el panel de
            // detalle (derecha) entren mejor, sin cortarse — antes 1020 fijo.
            var anchoDisponible = SystemParameters.WorkArea.Width - 60;
            var altoDisponible  = SystemParameters.WorkArea.Height - 60;
            Width    = Math.Min(1360, anchoDisponible); Height = Math.Min(800, altoDisponible);
            MinWidth = 860;  MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Br("#FAF9F8");
            BuildUI();

            // Un usuario normal solo debe poder consultar cobranzas de SU propio local — antes
            // "Local: (todos)" quedaba disponible para cualquiera, sin restricción, mostrando
            // datos de otras sucursales. Solo un ADMINISTRADOR (o el usuario con excepción
            // puntual, ver Usuario.PuedeVerTodosLosLocales) puede elegir "Todos" u otro local.
            if (_session.UsuarioActual?.PuedeVerTodosLosLocales != true && _session.LocalActual != null)
            {
                _localFiltroId     = _session.LocalActual.IdLocal;
                _txtLocal.Text     = _session.LocalActual.NombreLocal;
                _txtLocal.FontStyle = FontStyles.Normal;
                _btnSelLocal.Visibility    = Visibility.Collapsed;
                _btnQuitarLocal.Visibility = Visibility.Collapsed;
            }

            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            // ══ ESTRUCTURA RAÍZ ══════════════════════════════════════════════
            // Row 0: header naranja
            // Row 1: filtros
            // Row 2: [grilla | panel detalle]  ← ocupa todo el alto restante
            // Row 3: barra totales
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── HEADER ───────────────────────────────────────────────────────
            var hdr = new Border { Background = Br("#1A4F6E"), Padding = new Thickness(16, 8, 16, 8) };
            Grid.SetRow(hdr, 0);
            var hdrG = new Grid();
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // título
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // espacio
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // botones acción
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // conteo

            hdrG.Children.Add(new TextBlock { Text = "💰  Informe de Cobranzas",
                Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });

            // Botones de acción en el header
            Button MkHBtn(string txt, string bg) {
                var b = new Button { Content = txt, Height = 32,
                    Padding = new Thickness(14, 0, 14, 0),
                    Background = Br(bg), Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0) };
                return b;
            }
            var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hdrBtns, 2);

            var _btnBuscarH = MkHBtn("🔍 Buscar", "#1A4F6E");
            _btnBuscarH.FontWeight = FontWeights.Bold;
            _btnBuscarH.Click += async (_, _) => await Buscar();
            hdrBtns.Children.Add(_btnBuscarH);

            var _btnVistaH = MkHBtn("👁 Vista previa", "#6A1B9A");
            _btnVistaH.Click += (_, _) => ImprimirCobranzas(preview: true);
            hdrBtns.Children.Add(_btnVistaH);

            var _btnImprimirH = MkHBtn("🖨 Imprimir", "#1565C0");
            _btnImprimirH.Click += (_, _) => ImprimirCobranzas(preview: false);
            hdrBtns.Children.Add(_btnImprimirH);

            var _btnCerrarH = MkHBtn("✕ Cerrar", "#6B7280");
            _btnCerrarH.Margin = new Thickness(0);
            _btnCerrarH.Click += (_, _) => Close();
            hdrBtns.Children.Add(_btnCerrarH);

            hdrG.Children.Add(hdrBtns);

            _lblConteo = new TextBlock { Foreground = Br("#FFE0B2"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(18, 0, 0, 0) };
            Grid.SetColumn(_lblConteo, 3);
            hdrG.Children.Add(_lblConteo);
            hdr.Child = hdrG;
            root.Children.Add(hdr);

            // ── FILTROS ───────────────────────────────────────────────────────
            // Dos filas: período+botones | local+cliente
            var fRoot = new Border { Background = Br("#1A4F6E") };
            Grid.SetRow(fRoot, 1);
            var fGrid = new Grid { Margin = new Thickness(14, 7, 14, 7) };
            fGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fGrid.RowDefinitions.Add(new RowDefinition { Height = new Thickness(0,4,0,0).Top < 0 ? GridLength.Auto : GridLength.Auto });

            Button MkBtn(string txt, string bg, int h = 30) {
                var b = new Button { Content = txt, Height = h,
                    Padding = new Thickness(12, 0, 12, 0),
                    Background = Br(bg), Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center };
                return b;
            }
            TextBlock Lbl2(string t) => new TextBlock { Text = t,
                Foreground = Br("#FFE0B2"), FontWeight = FontWeights.SemiBold, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            Border VSep() => new Border { Width = 1, Background = Br("#2A7AB5"), Margin = new Thickness(10, 0, 10, 0) };

            // Fila 1 de filtros
            // ORDEN: crear TODOS los controles primero, suscribir eventos DESPUÉS
            var fRow1 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // 1. Crear controles sin eventos todavía
            _rbTodos   = new RadioButton { Content = "Todos",    GroupName = "CobPeriodo",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center };
            _rbPeriodo = new RadioButton { Content = "Período:", GroupName = "CobPeriodo",
                IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center };

            var modernDpStyle = CrediSoft.UI.Views.Shared.UiStyles.ModernDatePickerStyle();
            _dpDesde = new DatePicker { Width = 118, SelectedDate = DateTime.Today.AddMonths(-1),
                Style = modernDpStyle, VerticalAlignment = VerticalAlignment.Center };
            _dpHasta = new DatePicker { Width = 118, SelectedDate = DateTime.Today,
                Style = modernDpStyle, VerticalAlignment = VerticalAlignment.Center };

            _btnVerTodos = MkBtn("📅 Mes actual", "#388E3C");
            _btnVerTodos.Margin = new Thickness(6, 0, 0, 0);

            // 2. Suscribir eventos DESPUÉS de crear todos los controles
            _rbTodos.Checked += async (_, _) =>
            {
                _dpDesde.IsEnabled      = false;
                _dpHasta.IsEnabled      = false;
                _btnVerTodos.IsEnabled  = false;
                await Buscar();
            };
            _rbPeriodo.Checked += async (_, _) =>
            {
                _dpDesde.IsEnabled     = true;
                _dpHasta.IsEnabled     = true;
                _btnVerTodos.IsEnabled = true;
                await Buscar();
            };
            _dpDesde.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _dpHasta.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _btnVerTodos.Click += (_, _) =>
            {
                _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _dpHasta.SelectedDate = DateTime.Today;
            };

            // 3. Armar layout — WrapPanel para que los filtros se adapten al ancho disponible
            _ = fRow1; // descartado — reemplazado por fWrap
            var fWrap = new WrapPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Grupo período
            var grpPeriodo = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2) };

            var brdTodos = new Border { Background = Br("#1A4F6E"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdTodos.Child = _rbTodos;
            grpPeriodo.Children.Add(brdTodos);

            var brdPer = new Border { Background = Br("#1A4F6E"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdPer.Child = _rbPeriodo;
            grpPeriodo.Children.Add(brdPer);

            grpPeriodo.Children.Add(_dpDesde);
            grpPeriodo.Children.Add(Lbl2(" →"));
            grpPeriodo.Children.Add(_dpHasta);
            grpPeriodo.Children.Add(_btnVerTodos);
            fWrap.Children.Add(grpPeriodo);

            fWrap.Children.Add(VSep());

            // Grupo local
            var grpLocal = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2) };
            grpLocal.Children.Add(Lbl2("Local:"));
            _txtLocal = new TextBox { Width = 130, IsReadOnly = true, Cursor = Cursors.Arrow,
                Background = Br("#0E2F44"), Foreground = Br("#7FB3D3"), FontStyle = FontStyles.Italic,
                FontSize = 11, Padding = new Thickness(5,3,5,3), Text = "(todos)",
                BorderBrush = Br("#BDBDBD"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0) };
            grpLocal.Children.Add(_txtLocal);
            _btnSelLocal = MkBtn("Sel.", "#5C6BC0");
            _btnSelLocal.Click += OnSeleccionarLocal;
            grpLocal.Children.Add(_btnSelLocal);
            _btnQuitarLocal = MkBtn("✕", "#546E7A");
            _btnQuitarLocal.Width = 28; _btnQuitarLocal.Visibility = Visibility.Collapsed;
            _btnQuitarLocal.Margin = new Thickness(3, 0, 0, 0);
            _btnQuitarLocal.ToolTip = "Ver todos los locales";
            _btnQuitarLocal.Click += OnLimpiarLocal;
            grpLocal.Children.Add(_btnQuitarLocal);
            fWrap.Children.Add(grpLocal);

            fWrap.Children.Add(VSep());

            // Grupo cliente
            var grpCliente = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2) };
            grpCliente.Children.Add(Lbl2("Cliente:"));
            _txtCliente = new TextBox { Width = 150, IsReadOnly = true, Cursor = Cursors.Arrow,
                Background = Br("#0E2F44"), Foreground = Br("#7FB3D3"), FontStyle = FontStyles.Italic,
                FontSize = 11, Padding = new Thickness(5,3,5,3), Text = "(todos)",
                BorderBrush = Br("#BDBDBD"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0) };
            grpCliente.Children.Add(_txtCliente);
            var btnSelCli = MkBtn("Sel.", "#5C6BC0");
            btnSelCli.Click += OnSeleccionarCliente;
            grpCliente.Children.Add(btnSelCli);
            var btnQCli = MkBtn("✕", "#546E7A");
            btnQCli.Width = 28; btnQCli.Margin = new Thickness(3, 0, 0, 0);
            btnQCli.ToolTip = "Quitar filtro cliente";
            btnQCli.Click += async (_, _) => { _clienteFiltroId = null; _txtCliente.Text = "(todos)"; _txtCliente.FontStyle = FontStyles.Italic; await Buscar(); };
            grpCliente.Children.Add(btnQCli);
            fWrap.Children.Add(grpCliente);

            fGrid.Children.Add(fWrap);
            fRoot.Child = fGrid;
            root.Children.Add(fRoot);

            // ── CUERPO: grilla izquierda + panel detalle derecho ─────────────
            var body = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300), MinWidth = 260 });
            Grid.SetRow(body, 2);

            // ─ GRILLA (columna 0) ───────────────────────────────────────────
            var gridOuter = new Border { BorderBrush = Br("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0,0,6,0) };
            Grid.SetColumn(gridOuter, 0);

            var gridPanel = new Grid();
            gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // sub-header grilla
            var ghdr = new Border { Background = Br("#1A4F6E"), Padding = new Thickness(12, 6, 12, 6) };
            var ghdrG = new Grid();
            ghdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ghdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ghdrG.Children.Add(new TextBlock { Text = "📋  Listado de cobros",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
            var ghint = new TextBlock { Text = "← seleccionar fila para ver detalle",
                Foreground = Br("#FFD0A0"), FontSize = 10, FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(ghint, 1);
            ghdrG.Children.Add(ghint);
            ghdr.Child = ghdrG;
            Grid.SetRow(ghdr, 0);
            gridPanel.Children.Add(ghdr);

            var colHdrStyle = BuildSortableHeaderStyle();

            // Estilo de fila: mora > 0 → rojo suave
            var rowStyle = new Style(typeof(DataGridRow));
            var moraT = new DataTrigger { Binding = new System.Windows.Data.Binding("Mora") { Converter = new MoraColorConverter() }, Value = true };
            moraT.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Br("#C0392B")));
            rowStyle.Triggers.Add(moraT);

            // FontSize reducido (antes heredaba el default ~13px de la Window) — con 10 columnas
            // compitiendo por el ancho disponible (compartido con el panel de detalle fijo de
            // 300px a la derecha), el texto se cortaba en casi todas las columnas salvo Cliente.
            _gridCobros = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 28,
                FontSize = 10.5,
                CanUserSortColumns = true,
                AlternatingRowBackground = Br("#F4F8FB"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = Br("#F0E0D0"), BorderThickness = new Thickness(0),
                ColumnHeaderStyle = colHdrStyle, RowStyle = rowStyle,
                SelectionMode = DataGridSelectionMode.Single };
            _gridCobros.SelectionChanged += OnCobroSeleccionado;
            Grid.SetRow(_gridCobros, 1);

            DataGridTextColumn GC(string h, string p, double w, string? fmt = null) =>
                new() { Header = h, Width = w, MinWidth = 40, SortMemberPath = p,
                    Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                          : new System.Windows.Data.Binding(p) };

            // Resalta "Cobrado por" en negrita + color ámbar cuando difiere del Vendedor —
            // ver FilaCobro.EsCobradorDistinto.
            Style CobradorDistintoStyle()
            {
                var st = new Style(typeof(TextBlock));
                var trig = new DataTrigger { Binding = new System.Windows.Data.Binding("EsCobradorDistinto"), Value = true };
                trig.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                trig.Setters.Add(new Setter(TextBlock.ForegroundProperty, Br("#E65100")));
                st.Triggers.Add(trig);
                return st;
            }

            // Columnas esenciales — detalle completo va al panel derecho. Anchos recalculados
            // para el FontSize más chico (10.5) y para que ninguna quede más angosta que su
            // contenido típico (antes "Teléfono"/"Cuota"/"Local" quedaban cortados con "09...",
            // etc. pese a tener ancho fijo, porque competían con columnas sobredimensionadas).
            // Con hora — antes solo mostraba la fecha, y con varios cobros del mismo cliente/día
            // (frecuente con abonos parciales) no se podía distinguir el orden real.
            _gridCobros.Columns.Add(GC("Fecha",     "FechaCobrado", 100));
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Cliente", SortMemberPath = "Cliente",
                Binding = new System.Windows.Data.Binding("Cliente"),
                Width = new DataGridLength(1.3, DataGridLengthUnitType.Star), MinWidth = 110 });
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Vendedor", SortMemberPath = "Vendedor",
                Binding = new System.Windows.Data.Binding("Vendedor"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 80 });
            // "Cobrado por" — puede ser distinto del Vendedor desde que se agregó la opción de
            // registrar el cobro a nombre de otro vendedor (CobrosWindow, badge "Cobrado por").
            // Estilo propio (no reutiliza GC) para poder resaltar en negrita+color cuando difiere
            // del vendedor original — ayuda al dueño del negocio a detectar de un vistazo estos casos.
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Cobrado por", SortMemberPath = "Cobrador",
                Binding = new System.Windows.Data.Binding("Cobrador"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 80,
                ElementStyle = CobradorDistintoStyle() });
            _gridCobros.Columns.Add(GC("Teléfono",  "Telefono",     78));
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Solicitud", SortMemberPath = "Solicitud",
                Binding = new System.Windows.Data.Binding("Solicitud")
                    { Converter = new SolicitudShortConverter() },
                Width = 62, MinWidth = 55 });
            _gridCobros.Columns.Add(GC("Cuota",     "Cuota",         42));
            _gridCobros.Columns.Add(GC("Monto Cobrado", "MontoEvento", 82, "N0"));
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Mora", SortMemberPath = "Mora",
                Binding = new System.Windows.Data.Binding("Mora")
                    { Converter = new MoraTextConverter() },
                Width = 48, MinWidth = 42 });
            _gridCobros.Columns.Add(new DataGridTextColumn { Header = "Local", SortMemberPath = "Local",
                Binding = new System.Windows.Data.Binding("Local"),
                Width = new DataGridLength(0.9, DataGridLengthUnitType.Star), MinWidth = 70 });

            gridPanel.Children.Add(_gridCobros);
            gridOuter.Child = gridPanel;
            body.Children.Add(gridOuter);

            // ─ PANEL DETALLE (columna 1) ─────────────────────────────────────
            // Estructura: DockPanel vertical
            //   Top    → header "Detalle del cobro"
            //   Bottom → sección artículos (altura fija 200px: header+grid)
            //   Fill   → ScrollViewer con los campos de info
            _panelDetalle = new Border { BorderBrush = Br("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), ClipToBounds = true };
            Grid.SetColumn(_panelDetalle, 1);

            var detDock = new DockPanel { LastChildFill = true };

            // ▸ Header superior
            var dHdr = new Border { Background = Br("#3949AB"), Padding = new Thickness(12, 8, 12, 8) };
            DockPanel.SetDock(dHdr, Dock.Top);
            dHdr.Child = new TextBlock { Text = "📄  Detalle del cobro",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
            detDock.Children.Add(dHdr);

            // ▸ Sección artículos — anclada al fondo
            var artSection = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(artSection, Dock.Bottom);

            var artColStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            artColStyle.Setters.Add(new Setter(Control.BackgroundProperty, Br("#546E7A")));
            artColStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            artColStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            artColStyle.Setters.Add(new Setter(Control.FontSizeProperty, 10.0));
            artColStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));

            var aHdr = new Border { Background = Br("#546E7A"), Padding = new Thickness(12, 6, 12, 6) };
            DockPanel.SetDock(aHdr, Dock.Top);
            aHdr.Child = new TextBlock { Text = "🛒  Artículos de la solicitud",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 };
            artSection.Children.Add(aHdr);

            // ScrollViewer para que el DataGrid no crezca infinito dentro del DockPanel
            var artScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 210,
                MinHeight = 80
            };
            _gridArts = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 30,
                FontSize = 11, BorderThickness = new Thickness(0),
                AlternatingRowBackground = Br("#ECEFF1"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = Br("#CFD8DC"),
                ColumnHeaderStyle = artColStyle,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled };
            _gridArts.Columns.Add(new DataGridTextColumn { Header = "Artículo",
                Binding = new System.Windows.Data.Binding("Articulo"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _gridArts.Columns.Add(new DataGridTextColumn { Header = "Cant",
                Binding = new System.Windows.Data.Binding("Cantidad") { StringFormat = "N0" }, Width = 48 });
            _gridArts.Columns.Add(new DataGridTextColumn { Header = "P.Venta",
                Binding = new System.Windows.Data.Binding("Precio") { StringFormat = "N0" }, Width = 90 });
            artScroll.Content = _gridArts;
            artSection.Children.Add(artScroll);
            detDock.Children.Add(artSection);

            // ▸ Info cobro — ocupa el espacio restante con scroll
            var infoScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

            var dInfo = new StackPanel { Background = System.Windows.Media.Brushes.White };

            void DRow(StackPanel parent, string label, ref TextBlock valRef, bool highlight = false)
            {
                var row = new Border { Padding = new Thickness(12, 7, 12, 7),
                    BorderBrush = Br("#F0F0F0"), BorderThickness = new Thickness(0, 0, 0, 1) };
                var rg = new Grid();
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rg.Children.Add(new TextBlock { Text = label, Foreground = Br("#9E9E9E"),
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center });
                var val = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = highlight ? Br("#1F6089") : Br("#212121"),
                    FontWeight = highlight ? FontWeights.Bold : FontWeights.Normal };
                Grid.SetColumn(val, 1);
                rg.Children.Add(val);
                row.Child = rg;
                parent.Children.Add(row);
                valRef = val;
            }

            DRow(dInfo, "CLIENTE",    ref _detCliente,  false);
            DRow(dInfo, "CI",         ref _detCI,        false);
            DRow(dInfo, "LOCAL",      ref _detLocal,     false);
            DRow(dInfo, "FECHA COB.", ref _detFecha,     false);
            DRow(dInfo, "SOLICITUD",  ref _detSol,       false);
            DRow(dInfo, "CUOTA",      ref _detCuota,     false);
            DRow(dInfo, "VENCIM.",    ref _detVto,       false);
            DRow(dInfo, "MORA días",  ref _detMora,      true);

            // Bloque financiero con fondo naranja suave
            var finCard = new Border { Background = Br("#E8F0F7") };
            var finSp   = new StackPanel();
            finCard.Child = finSp;
            TextBlock fRef1 = null!, fRef2 = null!, fRef3 = null!;
            DRow(finSp, "MONTO",      ref fRef1, false);
            DRow(finSp, "PUNITORIO",  ref fRef2, true);
            DRow(finSp, "TOTAL",      ref fRef3, true);
            _detMonto = fRef1; _detPunit = fRef2; _detTotal = fRef3;
            dInfo.Children.Add(finCard);

            TextBlock cRef = null!, vRef = null!;
            DRow(dInfo, "COBRADOR",   ref cRef, false);
            DRow(dInfo, "VENDEDOR",   ref vRef, false);
            _detCobrador = cRef; _detVendedor = vRef;

            // Observación
            var obsRow = new Border { Padding = new Thickness(12, 7, 12, 10),
                BorderBrush = Br("#F0F0F0"), BorderThickness = new Thickness(0, 0, 0, 1) };
            var obsG = new StackPanel();
            obsG.Children.Add(new TextBlock { Text = "OBSERVACIÓN", Foreground = Br("#9E9E9E"),
                FontSize = 10, FontWeight = FontWeights.SemiBold });
            _detObs = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Foreground = Br("#212121"), Margin = new Thickness(0, 3, 0, 0) };
            obsG.Children.Add(_detObs);
            obsRow.Child = obsG;
            dInfo.Children.Add(obsRow);

            infoScroll.Content = dInfo;
            detDock.Children.Add(infoScroll);  // LastChildFill = true → ocupa el resto

            _panelDetalle.Child = detDock;
            body.Children.Add(_panelDetalle);

            root.Children.Add(body);

            // ── BARRA TOTALES ─────────────────────────────────────────────────
            var totBar = new Border { Background = Br("#263238"), Padding = new Thickness(14, 8, 14, 8) };
            Grid.SetRow(totBar, 3);
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12,
                Foreground = Br("#1A4F6E") };
            totBar.Child = _lblTotal;
            root.Children.Add(totBar);

            Content = root;
        }

        private void OnSeleccionarLocal(object s, RoutedEventArgs e)
        {
            var dlg = new CrediSoft.UI.Views.Compras.BuscadorLocalModal(_db)
                { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (dlg.ShowDialog() == true && dlg.LocalSeleccionado != null)
            {
                _localFiltroId              = dlg.LocalSeleccionado.IdLocal;
                _txtLocal.Text              = dlg.LocalSeleccionado.Nombre;
                _txtLocal.FontStyle         = FontStyles.Normal;
                _btnQuitarLocal.Visibility  = Visibility.Visible;
                _ = Buscar();
            }
        }

        private void OnLimpiarLocal(object s, RoutedEventArgs e)
        {
            _localFiltroId             = null;
            _txtLocal.Text             = "(todos)";
            _txtLocal.FontStyle        = FontStyles.Italic;
            _btnQuitarLocal.Visibility = Visibility.Collapsed;
            _ = Buscar();
        }

        private async void OnSeleccionarCliente(object s, RoutedEventArgs e)
        {
            var repo = App.Services.GetRequiredService<IClienteRepository>();
            var dlg  = new CrediSoft.UI.Views.Maestros.BuscadorClienteModal(repo)
                { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (dlg.ShowDialog() == true && dlg.ClienteSeleccionado != null)
            {
                _clienteFiltroId      = dlg.ClienteSeleccionado.IdCliente;
                _txtCliente.Text      = dlg.ClienteSeleccionado.NombreCliente;
                _txtCliente.FontStyle = FontStyles.Normal;
                // Relanzar consulta sin filtro de fecha para ver todo el historial del cliente
                await Buscar();
            }
        }

        private void OnCobroSeleccionado(object s, SelectionChangedEventArgs e)
        {
            if (_gridCobros.SelectedItem is not FilaCobro cobro)
            {
                _gridArts.ItemsSource = null;
                return;
            }
            // Llenar panel detalle
            _detCliente.Text  = cobro.Cliente;
            _detCI.Text       = cobro.CI;
            _detLocal.Text    = cobro.Local;
            _detFecha.Text    = cobro.FechaCobrado;
            _detSol.Text      = new SolicitudShortConverter().Convert(cobro.Solicitud, typeof(string), null!, System.Globalization.CultureInfo.CurrentCulture)?.ToString() ?? cobro.Solicitud;
            _detCuota.Text    = cobro.Cuota;
            _detVto.Text      = cobro.Vto;
            _detMora.Text     = cobro.Mora > 0 ? $"{cobro.Mora} día(s) de atraso" : "✓ Al día";
            _detMonto.Text    = $"Gs. {cobro.Monto:N0}";
            _detPunit.Text    = cobro.Punitorio > 0 ? $"Gs. {cobro.Punitorio:N0}" : "—";
            _detTotal.Text    = $"Gs. {cobro.Total:N0}";
            _detCobrador.Text = cobro.Cobrador;
            _detVendedor.Text = cobro.Vendedor;
            _detObs.Text      = string.IsNullOrWhiteSpace(cobro.Obs) ? "—" : cobro.Obs;
            _ = CargarArticulosAsync(cobro);
        }

        private async Task CargarArticulosAsync(FilaCobro cobro)
        {
            try
            {
                using var conn = _db.Create();
                var arts = (await conn.QueryAsync<FilaArticuloCobro>(
                    @"SELECT A.D AS Articulo, DET.CANTIDAD AS Cantidad, DET.PV AS Precio,
                             DET.CANTIDAD * DET.PV AS Subtotal
                      FROM DETALLES_SALES DET
                      INNER JOIN ARTICULOS A ON DET.IDART = A.ID
                      WHERE DET.IDCAB = @IdCab
                      ORDER BY A.D",
                    new { IdCab = cobro.IdCab })).ToList();
                _gridArts.ItemsSource = arts;
            }
            catch { _gridArts.ItemsSource = null; }
        }

        private async Task Buscar()
        {
            if (_dpDesde == null) return;
            try
            {
                var desde  = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var hasta  = _dpHasta.SelectedDate ?? DateTime.Today;
                var local  = _localFiltroId;

                using var conn = _db.Create();
                bool todosPeriodo = _rbTodos?.IsChecked == true;
                // Si hay cliente específico ignoramos el filtro de fecha — mostramos todo su historial
                bool ignorarFecha = _clienteFiltroId.HasValue;

                // Dos fuentes combinadas para no perder ningún cobro del período:
                //   (a) GENERADAS WHERE ESTADO=1 — cuotas que quedaron TOTALMENTE canceladas
                //       (pago completo directo, o el abono que finalmente saldó la cuota).
                //   (b) HISTORIAL_ENTREGAS_GENERADAS — un registro por CADA abono individual,
                //       incluye los parciales que NO alcanzaron a saldar la cuota (ESTADO sigue
                //       en 0 en GENERADAS). Sin esto, un abono parcial de hoy que no completó la
                //       cuota quedaba invisible en el informe aunque el ingreso fue real — bug
                //       real reportado (pago parcial de 12.500 Gs que no aparecía).
                // El filtro "H.IDGENERADAS no está en GENERADAS ESTADO=1 para esa misma fecha de
                // abono" no es necesario: (a) trae la fila con FECHACOBRADO = fecha del ÚLTIMO
                // abono (el que completó), y (b) trae CADA abono con su propia FECHA — son
                // eventos distintos en el tiempo, ambos reales, no se duplican.
                var whereCompletos = new System.Text.StringBuilder("WHERE G.ESTADO = 1");
                var whereParciales = new System.Text.StringBuilder("WHERE G.ESTADO = 0");
                if (!todosPeriodo && !ignorarFecha)
                {
                    whereCompletos.Append(" AND G.FECHACOBRADO >= @Desde AND G.FECHACOBRADO < @Hasta");
                    whereParciales.Append(" AND H.FECHA >= @Desde AND H.FECHA < @Hasta");
                }
                // El filtro de local NO se aplica acá — GENERADAS.IDLOCAL es el local de ORIGEN
                // del crédito, no dónde se cobró cada evento puntual (bug real confirmado: un
                // cliente de Tembiapora que paga su cuota en Sucursal 1 SJN no aparecía en el
                // informe de Sucursal 1 SJN). Se trae todo y se filtra en C# después de
                // resolver el local real vía CorregirLocalCobrosAsync, mismo patrón ya usado
                // para corregir "Cobrador" en el Explorador de Caja.
                if (_clienteFiltroId.HasValue)
                {
                    whereCompletos.Append(" AND C.ID_CLIENTE = @Cliente");
                    whereParciales.Append(" AND C.ID_CLIENTE = @Cliente");
                }

                // FechaOrden (datetime real) separada de FechaCobrado (string dd/MM/yyyy para
                // mostrar) — ordenar directo por el string alfabético daría un orden cronológico
                // incorrecto (ej. "05/07/2026" quedaría antes que "31/01/2026"). Dapper ignora
                // la columna extra al mapear a FilaCobro (no tiene esa propiedad).
                //
                // MontoEvento (rama "completos"): G.TOTAL es el SALDO PENDIENTE de la cuota,
                // no lo cobrado en este evento — cuando la cuota queda saldada (abono parcial
                // que la completa, o cobro directo), TOTAL baja a 0, mostrando "Monto Cobrado:
                // 0" para un pago real (bug confirmado: cuota 7697, Roberto Rotela Sanabria).
                // MONTO+PUNITORIO+REAJUSTE es el costo total real de la cuota, que es lo que
                // efectivamente se terminó pagando para saldarla.
                var sql = $@"
                    SELECT * FROM (
                        SELECT G.IDGENERADAS, G.IDCAB,
                               C.ID_CLIENTE AS IdCliente,
                               G.FECHACOBRADO AS FechaOrden,
                               CONVERT(VARCHAR(10), G.FECHACOBRADO, 103) + ' ' + CONVERT(VARCHAR(5), G.FECHACOBRADO, 108) AS FechaCobrado,
                               L.NOMBRE AS Local,
                               C.NOMBRE_CLIENTE AS Cliente, C.CI_CLIENTE AS CI,
                               C.TELEFONO_CLIENTE AS Telefono,
                               CS.NSOLICITUD AS Solicitud,
                               CAST(G.NCUOTA - 1 AS VARCHAR(10)) + '/' + CAST(CS.CUOTAS AS VARCHAR(10)) AS Cuota,
                               G.COMPROBANTE AS Comprobante, G.NCUOTA AS NCuotaNum,
                               U_COB.NOMBRE_USUARIO AS Cobrador,
                               U_VEN.NOMBRE_USUARIO AS Vendedor,
                               G.MONTO, G.PUNITORIO, G.REAJUSTE, G.TOTAL,
                               G.MONTO + G.PUNITORIO + G.REAJUSTE AS MontoEvento,
                               CONVERT(VARCHAR(10), G.VTO, 103) AS Vto,
                               G.MORA, G.OBS
                        FROM GENERADAS G
                        INNER JOIN LOCALES L           ON G.IDLOCAL      = L.ID_LOCAL
                        INNER JOIN CABECERA_SALES CS   ON CS.IDCAB       = G.IDCAB
                        INNER JOIN CLIENTES C          ON CS.ID_CLIENTE  = C.ID_CLIENTE
                        INNER JOIN USUARIOS U_VEN      ON CS.ID_USUARIO  = U_VEN.ID_USUARIO
                        INNER JOIN USUARIOS U_COB      ON G.IDU          = U_COB.ID_USUARIO
                        {whereCompletos}

                        UNION ALL

                        SELECT H.IDGENERADAS, H.IDCAB,
                               C.ID_CLIENTE AS IdCliente,
                               H.FECHA AS FechaOrden,
                               CONVERT(VARCHAR(10), H.FECHA, 103) + ' ' + CONVERT(VARCHAR(5), H.FECHA, 108) AS FechaCobrado,
                               L.NOMBRE AS Local,
                               C.NOMBRE_CLIENTE AS Cliente, C.CI_CLIENTE AS CI,
                               C.TELEFONO_CLIENTE AS Telefono,
                               CS.NSOLICITUD AS Solicitud,
                               CAST(H.NCUOTA - 1 AS VARCHAR(10)) + '/' + CAST(CS.CUOTAS AS VARCHAR(10)) AS Cuota,
                               G.COMPROBANTE AS Comprobante, H.NCUOTA AS NCuotaNum,
                               U_COB.NOMBRE_USUARIO AS Cobrador,
                               U_VEN.NOMBRE_USUARIO AS Vendedor,
                               H.ENTREGA AS MONTO, H.PUNITORIO, H.REAJUSTE, H.TOTAL,
                               H.ENTREGA AS MontoEvento,
                               CONVERT(VARCHAR(10), G.VTO, 103) AS Vto,
                               H.MORA, H.NOTA AS OBS
                        FROM HISTORIAL_ENTREGAS_GENERADAS H
                        INNER JOIN GENERADAS G          ON G.IDGENERADAS  = H.IDGENERADAS
                        INNER JOIN LOCALES L            ON G.IDLOCAL      = L.ID_LOCAL
                        INNER JOIN CABECERA_SALES CS    ON CS.IDCAB       = H.IDCAB
                        INNER JOIN CLIENTES C           ON CS.ID_CLIENTE  = C.ID_CLIENTE
                        INNER JOIN USUARIOS U_VEN       ON CS.ID_USUARIO  = U_VEN.ID_USUARIO
                        INNER JOIN USUARIOS U_COB       ON H.IDU          = U_COB.ID_USUARIO
                        {whereParciales}
                    ) T
                    ORDER BY FechaOrden DESC";

                _todos = (await conn.QueryAsync<FilaCobro>(sql, new {
                    Desde    = desde,
                    Hasta    = hasta.AddDays(1),
                    Cliente  = _clienteFiltroId ?? 0
                })).ToList();

                await CorregirLocalCobrosAsync(conn, _todos);

                AplicarFiltros();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al cargar cobranzas"); }
        }

        // Resuelve el local REAL donde se cobró cada evento (CAJA_DETALLE.ID_LOCAL vía
        // CAJA_MASTER), reemplazando el local de ORIGEN del crédito (GENERADAS.IDLOCAL) que
        // trae el SQL principal — bug real confirmado: un cliente de Tembiapora que paga su
        // cuota en Sucursal 1 SJN no aparecía en el informe de Sucursal 1 SJN. No hay FK
        // estructurada entre CAJA_DETALLE y GENERADAS/HISTORIAL_ENTREGAS_GENERADAS — se
        // empareja por comprobante+cuota extraídos del propio texto de Concepto, mismo patrón
        // ya usado para corregir "Cobrador" en el Explorador de Caja.
        private static async Task CorregirLocalCobrosAsync(System.Data.IDbConnection conn, List<FilaCobro> filas)
        {
            var candidatos = filas.Where(f => !string.IsNullOrEmpty(f.Comprobante)).ToList();
            if (candidatos.Count == 0) return;

            var sufijos = candidatos.Select(f => f.Comprobante.TrimStart('0')).Distinct().ToList();

            var p = new DynamicParameters();
            for (int i = 0; i < sufijos.Count; i++) p.Add($"@s{i}", $"%{sufijos[i]}");
            var movimientos = (await conn.QueryAsync<(string Concepto, int IdLocal)>(
                "SELECT D.CONCEPTO, D.ID_LOCAL " +
                "FROM CAJA_DETALLE D " +
                "WHERE D.SUBTIPO IN ('COBRO','COBRO_SISTEMA') AND D.ESTADO_REG = 'V' AND (" +
                string.Join(" OR ", Enumerable.Range(0, sufijos.Count).Select(i => $"D.CONCEPTO LIKE @s{i}")) + ")",
                p))
                .ToList();

            // (Comprobante sin ceros, NCuota) -> ID_LOCAL. Se resuelve una sola vez toda la
            // lista de movimientos candidatos con el mismo regex que ya usa Anular() más abajo.
            var porClave = new Dictionary<(string, byte), int>();
            foreach (var (concepto, idLocal) in movimientos)
            {
                var m = System.Text.RegularExpressions.Regex.Match(concepto,
                    @"CUOTA N.?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*(\d+)");
                if (!m.Success) continue;
                var clave = (m.Groups[2].Value.TrimStart('0'), byte.Parse(m.Groups[1].Value));
                porClave[clave] = idLocal;
            }

            foreach (var f in candidatos)
            {
                var clave = (f.Comprobante.TrimStart('0'), f.NCuotaNum);
                if (porClave.TryGetValue(clave, out var idLocal))
                    f.LocalIdReal = idLocal;
            }
        }

        private void AplicarFiltros()
        {
            var lista = _todos.AsEnumerable();
            if (_clienteFiltroId.HasValue)
                lista = lista.Where(r => r.IdCliente == _clienteFiltroId.Value);
            // Filtro por LocalIdReal (dónde se cobró de verdad, ver CorregirLocalCobrosAsync)
            // en vez del local de origen del crédito que ya trae el SQL. Si no se pudo
            // resolver el local real (LocalIdReal==null, cobro sin match en CAJA_DETALLE),
            // se deja pasar en vez de ocultarlo — mejor mostrar de más que perder un cobro real.
            if (_localFiltroId.HasValue)
                lista = lista.Where(r => r.LocalIdReal == null || r.LocalIdReal == _localFiltroId.Value);

            var result = lista.ToList();
            _gridCobros.ItemsSource = result;
            _gridArts.ItemsSource   = null;

            var totalCobros   = result.Sum(r => r.Monto);
            var totalPunit    = result.Sum(r => r.Punitorio);
            var totalGeneral  = result.Sum(r => r.Total);
            _lblConteo.Text   = $"{result.Count} cobro(s)";
            _lblTotal.Text    = $"Cobros: {result.Count}   |   " +
                                $"Monto cuotas: Gs. {totalCobros:N0}   |   " +
                                $"Punitorio: Gs. {totalPunit:N0}   |   " +
                                $"Total cobrado: Gs. {totalGeneral:N0}";
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
        }

        private async void ImprimirCobranzas(bool preview = false)
        {
            var filas = (_gridCobros.ItemsSource as IEnumerable<FilaCobro>)?.ToList();
            if (filas == null || filas.Count == 0)
            { MessageBox.Show("No hay datos para imprimir.", "Imprimir", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var (impresora, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

            var p = new CrediSoft.UI.Views.Cobros.CobranzasPagina
            {
                Filas     = filas.Select(f => new CrediSoft.UI.Views.Cobros.FilaCobranzaImp(
                                f.FechaCobrado, f.Cliente, f.Telefono, f.Vendedor,
                                f.Cobrador, f.Local,
                                f.Cuota, f.Solicitud, f.Mora, f.Monto, f.Punitorio, f.Total)).ToList(),
                Desde     = _dpDesde?.SelectedDate?.ToString("dd/MM/yyyy") ?? "—",
                Hasta     = _dpHasta?.SelectedDate?.ToString("dd/MM/yyyy") ?? "—",
                FechaImp  = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario   = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
                Impresora = impresora,
                LogoPath  = CrediSoft.UI.Views.Cobros.CobranzasPagina.ResolverLogoPath()
            };

            if (preview)
            {
                var win = new CrediSoft.UI.Views.Cobros.CobranzasPreviewWindow(p) { Owner = this };
                win.ShowDialog();
            }
            else
            {
                CrediSoft.UI.Views.Cobros.CobranzasImpresora.Imprimir(p);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HISTORIAL DE CRÉDITOS  — rediseño completo
    // ══════════════════════════════════════════════════════════════════════════
    public class HCreditosWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly IClienteRepository   _clienteRepo;

        // Grillas
        private DataGrid _gridCabs  = null!;
        private DataGrid _gridCuotas = null!;

        // Filtros
        private DatePicker  _dpDesde = null!, _dpHasta = null!;
        private RadioButton _rbPeriodo = null!, _rbTodos = null!;
        private Button      _btnMesActual = null!;
        private TextBox     _txtLocal = null!, _txtCliente = null!, _txtUsuario = null!;
        private Button      _btnQuitarLocal = null!, _btnQuitarCliente = null!, _btnQuitarUsuario = null!;
        private int?        _idLocalFiltro = null, _idClienteFiltro = null, _idUsuarioFiltro = null;

        // Totales
        private TextBlock _lblConteo = null!, _lblTotal = null!, _lblCuotasHdr = null!;

        private static System.Windows.Media.SolidColorBrush HBr(string hex) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        public HCreditosWindow()
        {
            _db          = App.Services.GetRequiredService<IDbConnectionFactory>();
            _clienteRepo = App.Services.GetRequiredService<IClienteRepository>();
            Title    = "Historial de Créditos";
            Width    = 1020; Height = 650;
            MinWidth = 860;  MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = HBr("#FAF9F8");
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── HEADER ─────────────────────────────────────────────────────
            var hdr = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(16, 10, 16, 10) };
            Grid.SetRow(hdr, 0);
            var hdrG = new Grid();
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.Children.Add(new TextBlock { Text = "💳  Historial de Créditos",
                Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });

            Button MkHBtn(string txt, string bg) => new Button {
                Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
                Background = HBr(bg), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) };
            var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hdrBtns, 2);

            // ── Usuario (en header para no desbordar la barra de filtros) ──
            hdrBtns.Children.Add(new TextBlock { Text = "👤 Usuario:",
                Foreground = HBr("#7FB3D3"), FontSize = 11, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            _txtUsuario = new TextBox { Width = 130, Height = 26, Padding = new Thickness(5, 2, 5, 2),
                IsReadOnly = true, Cursor = Cursors.Arrow, FontStyle = FontStyles.Italic,
                Background = HBr("#E8F0F7"), Foreground = HBr("#1F6089"), FontSize = 11,
                Text = "(todos)", VerticalAlignment = VerticalAlignment.Center, BorderBrush = HBr("#2A7AB5") };
            hdrBtns.Children.Add(_txtUsuario);
            var btnUsuario = new Button { Content = "Selec.", Height = 26, Padding = new Thickness(8, 0, 8, 0),
                Background = HBr("#1F6089"), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0) };
            btnUsuario.Click += (_, _) => {
                var modal = new BuscadorUsuarioModal(_db) { Owner = this };
                if (modal.ShowDialog() == true && modal.UsuarioSeleccionado != null) {
                    _idUsuarioFiltro = modal.UsuarioSeleccionado.IdUsuario;
                    _txtUsuario.Text = modal.UsuarioSeleccionado.NombreUsuario;
                    _txtUsuario.FontStyle = FontStyles.Normal;
                    _btnQuitarUsuario.Visibility = Visibility.Visible;
                    _ = Buscar();
                }
            };
            hdrBtns.Children.Add(btnUsuario);
            _btnQuitarUsuario = new Button { Content = "✕", Height = 26, Padding = new Thickness(6, 0, 6, 0),
                Background = HBr("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 10, 0),
                Visibility = Visibility.Collapsed };
            _btnQuitarUsuario.Click += async (_, _) => {
                _idUsuarioFiltro = null; _txtUsuario.Text = "(todos)";
                _txtUsuario.FontStyle = FontStyles.Italic;
                _btnQuitarUsuario.Visibility = Visibility.Collapsed;
                await Buscar();
            };
            hdrBtns.Children.Add(_btnQuitarUsuario);

            hdrBtns.Children.Add(new Border { Width = 1, Background = HBr("#7FB3D3"),
                Margin = new Thickness(0, 4, 10, 4), VerticalAlignment = VerticalAlignment.Stretch });

            var bVista = MkHBtn("👁 Vista previa", "#6A1B9A");
            bVista.Click += (_, _) => ImprimirCreditos(preview: true);
            hdrBtns.Children.Add(bVista);
            var bImpr = MkHBtn("🖨 Imprimir", "#1B5E20");
            bImpr.Click += (_, _) => ImprimirCreditos(preview: false);
            hdrBtns.Children.Add(bImpr);
            hdrG.Children.Add(hdrBtns);

            _lblConteo = new TextBlock { Foreground = HBr("#7FB3D3"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(_lblConteo, 3); hdrG.Children.Add(_lblConteo);
            hdr.Child = hdrG; root.Children.Add(hdr);

            // ── FILTROS ─────────────────────────────────────────────────────
            var fBorder = new Border { Background = HBr("#1A4F6E"), Padding = new Thickness(12, 8, 12, 8) };
            Grid.SetRow(fBorder, 1);

            Button MkBtn(string t, string bg, int h = 30) => new Button {
                Content = t, Height = h, Padding = new Thickness(10, 0, 10, 0),
                Background = HBr(bg), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            TextBlock Lbl(string t) => new TextBlock { Text = t,
                Foreground = HBr("#BBDEFB"), FontWeight = FontWeights.SemiBold, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            Border Sep() => new Border { Width = 1, Background = HBr("#2A7AB5"), Margin = new Thickness(10, 0, 10, 0) };

            var fp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Radios Todos / Período
            _rbTodos   = new RadioButton { Content = "Todos",    GroupName = "CredPer",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center };
            _rbPeriodo = new RadioButton { Content = "Período:", GroupName = "CredPer", IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center };
            _dpDesde = new DatePicker { Width = 112, SelectedDate = DateTime.Today.AddMonths(-1),
                VerticalAlignment = VerticalAlignment.Center };
            _dpHasta = new DatePicker { Width = 112, SelectedDate = DateTime.Today,
                VerticalAlignment = VerticalAlignment.Center };
            _btnMesActual = MkBtn("📅 Mes actual", "#1F6089");
            _btnMesActual.Margin = new Thickness(6, 0, 0, 0);

            _rbTodos.Checked   += async (_, _) => { _dpDesde.IsEnabled=false; _dpHasta.IsEnabled=false; _btnMesActual.IsEnabled=false; await Buscar(); };
            _rbPeriodo.Checked += async (_, _) => { _dpDesde.IsEnabled=true;  _dpHasta.IsEnabled=true;  _btnMesActual.IsEnabled=true;  await Buscar(); };
            _dpDesde.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _dpHasta.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _btnMesActual.Click += (_, _) => {
                _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _dpHasta.SelectedDate = DateTime.Today;
            };

            var brdTodos = new Border { Background = HBr("#1F6089"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdTodos.Child = _rbTodos;
            var brdPer = new Border { Background = HBr("#1F6089"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdPer.Child = _rbPeriodo;

            fp.Children.Add(brdTodos); fp.Children.Add(brdPer);
            fp.Children.Add(_dpDesde); fp.Children.Add(Lbl("  →")); fp.Children.Add(_dpHasta);
            fp.Children.Add(_btnMesActual);
            fp.Children.Add(Sep());

            // Local
            fp.Children.Add(Lbl("🏪 Local:"));
            _txtLocal = new TextBox { Width = 150, Padding = new Thickness(5, 3, 5, 3),
                IsReadOnly = true, Cursor = Cursors.Arrow, FontStyle = FontStyles.Italic,
                Background = HBr("#E8F0F7"), Foreground = HBr("#1F6089"), FontSize = 11,
                Text = "(todos)", VerticalAlignment = VerticalAlignment.Center, BorderBrush = HBr("#2A7AB5") };
            var btnLocal = MkBtn("🏪 Selec.", "#1F6089");
            btnLocal.Margin = new Thickness(4, 0, 0, 0);
            btnLocal.Click += (_, _) => {
                var modal = new BuscadorLocalModal(_db) { Owner = this };
                if (modal.ShowDialog() == true && modal.LocalSeleccionado != null) {
                    _idLocalFiltro = modal.LocalSeleccionado.IdLocal;
                    _txtLocal.Text = modal.LocalSeleccionado.Nombre;
                    _txtLocal.FontStyle = FontStyles.Normal;
                    _btnQuitarLocal.Visibility = Visibility.Visible;
                    _ = Buscar();
                }
            };
            _btnQuitarLocal = MkBtn("✕", "#546E7A");
            _btnQuitarLocal.Margin = new Thickness(3, 0, 0, 0);
            _btnQuitarLocal.Visibility = Visibility.Collapsed;
            _btnQuitarLocal.Click += async (_, _) => {
                _idLocalFiltro = null; _txtLocal.Text = "(todos)";
                _txtLocal.FontStyle = FontStyles.Italic;
                _btnQuitarLocal.Visibility = Visibility.Collapsed;
                await Buscar();
            };
            fp.Children.Add(_txtLocal); fp.Children.Add(btnLocal); fp.Children.Add(_btnQuitarLocal);
            fp.Children.Add(Sep());

            // Cliente
            fp.Children.Add(Lbl("👤 Cliente:"));
            _txtCliente = new TextBox { Width = 180, Padding = new Thickness(5, 3, 5, 3),
                IsReadOnly = true, Cursor = Cursors.Arrow, FontStyle = FontStyles.Italic,
                Background = HBr("#E8F0F7"), Foreground = HBr("#1F6089"), FontSize = 11,
                Text = "(todos)", VerticalAlignment = VerticalAlignment.Center, BorderBrush = HBr("#2A7AB5") };
            var btnCliente = MkBtn("👤 Selec.", "#1F6089");
            btnCliente.Margin = new Thickness(4, 0, 0, 0);
            btnCliente.Click += (_, _) => {
                var modal = new BuscadorClienteModal(_clienteRepo, soloConCreditos: true) { Owner = this };
                if (modal.ShowDialog() == true && modal.ClienteSeleccionado != null) {
                    _idClienteFiltro = modal.ClienteSeleccionado.IdCliente;
                    _txtCliente.Text = modal.ClienteSeleccionado.NombreCliente;
                    _txtCliente.FontStyle = FontStyles.Normal;
                    _btnQuitarCliente.Visibility = Visibility.Visible;
                    // Resetear a "Todos" para ver todo el historial del cliente
                    _rbTodos.IsChecked = true;
                    _ = Buscar();
                }
            };
            _btnQuitarCliente = MkBtn("✕", "#546E7A");
            _btnQuitarCliente.Margin = new Thickness(3, 0, 0, 0);
            _btnQuitarCliente.Visibility = Visibility.Collapsed;
            _btnQuitarCliente.Click += async (_, _) => {
                _idClienteFiltro = null; _txtCliente.Text = "(todos)";
                _txtCliente.FontStyle = FontStyles.Italic;
                _btnQuitarCliente.Visibility = Visibility.Collapsed;
                // Volver a período al quitar el cliente
                _rbPeriodo.IsChecked = true;
                await Buscar();
            };
            fp.Children.Add(_txtCliente); fp.Children.Add(btnCliente); fp.Children.Add(_btnQuitarCliente);

            var btnCerrar = MkBtn("✕ Cerrar", "#546E7A", 32);
            btnCerrar.FontSize = 12; fp.Children.Add(btnCerrar);
            btnCerrar.Click += (_, _) => Close();

            fBorder.Child = fp; root.Children.Add(fBorder);

            // ── CUERPO: cabeceras arriba + cuotas abajo ──────────────────
            var body = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) }); // splitter
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            Grid.SetRow(body, 2);

            // ─ GRILLA CABECERAS ─────────────────────────────────────────
            var cabOuter = new Border { BorderBrush = HBr("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), ClipToBounds = true };
            Grid.SetRow(cabOuter, 0);

            var cabPanel = new Grid();
            cabPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cabPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var cabHdr = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(12, 6, 12, 6) };
            var cabHdrG = new Grid();
            cabHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cabHdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cabHdrG.Children.Add(new TextBlock { Text = "📋  Solicitudes de crédito",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 });
            var hint = new TextBlock { Text = "← seleccionar para ver cuotas",
                Foreground = HBr("#7FB3D3"), FontSize = 10, FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(hint, 1); cabHdrG.Children.Add(hint);
            cabHdr.Child = cabHdrG;
            Grid.SetRow(cabHdr, 0); cabPanel.Children.Add(cabHdr);

            var colHdrStyle = BuildOrangeHeaderStyle();

            // Row style: cerrados en gris tachado
            var cabRowStyle = new Style(typeof(DataGridRow));
            var dtCerrado = new DataTrigger {
                Binding = new System.Windows.Data.Binding("EsCerrado"), Value = true };
            dtCerrado.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#90A4AE")));
            dtCerrado.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HBr("#F4F6F8")));
            cabRowStyle.Triggers.Add(dtCerrado);

            _gridCabs = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 32,
                CanUserSortColumns = true,
                AlternatingRowBackground = HBr("#F3F8FF"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = HBr("#DDEEFF"),
                BorderThickness = new Thickness(0),
                ColumnHeaderStyle = colHdrStyle,
                RowStyle = cabRowStyle,
                SelectionMode = DataGridSelectionMode.Single };
            _gridCabs.SelectionChanged += OnCabSeleccionada;
            Grid.SetRow(_gridCabs, 1);

            DataGridTextColumn CC(string h, string p, double w, string? fmt = null) =>
                new() { Header = h, Width = w, MinWidth = 40, SortMemberPath = p,
                    Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                          : new System.Windows.Data.Binding(p) };

            _gridCabs.Columns.Add(CC("IDCAB",       "IdCab",       60));
            _gridCabs.Columns.Add(CC("Fecha",       "Fecha",      105));
            _gridCabs.Columns.Add(new DataGridTextColumn { Header = "Cliente", SortMemberPath = "Cliente",
                Binding = new System.Windows.Data.Binding("Cliente"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 80 });
            _gridCabs.Columns.Add(CC("Solicitud",   "Solicitud",   90));
            _gridCabs.Columns.Add(CC("Teléfono",    "Telefono",   100));
            _gridCabs.Columns.Add(CC("Local",       "Local",       60));
            _gridCabs.Columns.Add(CC("Cuo.",        "Cuotas",      45));
            _gridCabs.Columns.Add(CC("Total Gs.",   "Total",      100, "N0"));
            _gridCabs.Columns.Add(CC("Debe Gs.",    "Debe",       100, "N0"));
            _gridCabs.Columns.Add(CC("Haber Gs.",   "Haber",      100, "N0"));
            _gridCabs.Columns.Add(CC("Estado",      "EstadoStr",   75));
            _gridCabs.Columns.Add(CC("Usuario",     "Usuario",    110));

            cabPanel.Children.Add(_gridCabs);
            cabOuter.Child = cabPanel;
            body.Children.Add(cabOuter);

            // splitter visual
            var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = HBr("#E0E0E0"), ResizeDirection = GridResizeDirection.Rows };
            Grid.SetRow(splitter, 1); body.Children.Add(splitter);

            // ─ GRILLA CUOTAS ────────────────────────────────────────────
            var cuotasOuter = new Border { BorderBrush = HBr("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), ClipToBounds = true };
            Grid.SetRow(cuotasOuter, 2);

            var cuotasPanel = new Grid();
            cuotasPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cuotasPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var cuotasHdr = new Border { Background = HBr("#1F6089"), Padding = new Thickness(12, 6, 12, 6) };
            _lblCuotasHdr = new TextBlock { Text = "📅  Cuotas de la solicitud",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
            cuotasHdr.Child = _lblCuotasHdr;
            Grid.SetRow(cuotasHdr, 0); cuotasPanel.Children.Add(cuotasHdr);

            var cuotaColHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            cuotaColHdr.Setters.Add(new Setter(Control.BackgroundProperty, HBr("#1F6089")));
            cuotaColHdr.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            cuotaColHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            cuotaColHdr.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
            cuotaColHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));

            var cuotaRowStyle = new Style(typeof(DataGridRow));
            // Cancelado: texto verde suave
            var dtCancelado = new DataTrigger {
                Binding = new System.Windows.Data.Binding("Estado"),
                Value = "Cancelado" };
            dtCancelado.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#2E7D32")));
            dtCancelado.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.Normal));
            // Vencida: fondo rojo muy suave, texto rojo oscuro — sin fondo oscuro agresivo
            var dtVencido = new DataTrigger {
                Binding = new System.Windows.Data.Binding("EsVencida"),
                Value = true };
            dtVencido.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HBr("#FDECEA")));
            dtVencido.Setters.Add(new Setter(DataGridRow.ForegroundProperty, HBr("#C62828")));
            dtVencido.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
            cuotaRowStyle.Triggers.Add(dtVencido);
            cuotaRowStyle.Triggers.Add(dtCancelado);

            _gridCuotas = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 30,
                FontSize = 11, BorderThickness = new Thickness(0),
                AlternatingRowBackground = HBr("#EBF3FF"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = HBr("#C5D8F0"),
                ColumnHeaderStyle = cuotaColHdr,
                RowStyle = cuotaRowStyle,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "N° Cuota",
                Binding = new System.Windows.Data.Binding("NCuotaTexto"), Width = 80 });
            _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Monto Gs.",
                Binding = new System.Windows.Data.Binding("Monto") { StringFormat = "N0" }, Width = 130 });
            _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Vencimiento",
                Binding = new System.Windows.Data.Binding("Vto"), Width = 130 });
_gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Estado",
                Binding = new System.Windows.Data.Binding("Estado"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            Grid.SetRow(_gridCuotas, 1); cuotasPanel.Children.Add(_gridCuotas);
            cuotasOuter.Child = cuotasPanel;
            body.Children.Add(cuotasOuter);

            root.Children.Add(body);

            // ── BARRA TOTALES ───────────────────────────────────────────
            var totBar = new Border { Background = HBr("#263238"), Padding = new Thickness(14, 8, 14, 8) };
            Grid.SetRow(totBar, 3);
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12, Foreground = HBr("#4FC3F7") };
            totBar.Child = _lblTotal; root.Children.Add(totBar);

            Content = root;
        }

        private static Style BuildOrangeHeaderStyle()
        {
            var orange     = HBr("#0E2F44");
            var orangeDark = HBr("#1F6089");
            var white      = System.Windows.Media.Brushes.White;

            var ct = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            var outerBorder = new FrameworkElementFactory(typeof(Border));
            outerBorder.Name = "OuterBorder";
            outerBorder.SetValue(Border.BackgroundProperty, orange);
            outerBorder.SetValue(Border.BorderBrushProperty, orangeDark);
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
            tHover.Setters.Add(new Setter(Border.BackgroundProperty, orangeDark, "OuterBorder"));
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
                bool sinFecha      = _rbTodos?.IsChecked == true;
                bool tieneCliente  = _idClienteFiltro.HasValue;
                var  desde         = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var  hasta         = _dpHasta.SelectedDate ?? DateTime.Today;

                var where = new System.Text.StringBuilder("WHERE CS.FORMA_DE_VENTA = 2");

                // Con cliente específico: mostrar todo su historial (activo y cerrado) sin filtro de fecha
                // Sin cliente: solo activos dentro del período seleccionado
                if (!tieneCliente)
                {
                    where.Append(" AND CS.ESTADO = 1");
                    if (!sinFecha)
                    {
                        where.Append(" AND CAST(CS.FECHA AS DATE) >= @Desde");
                        where.Append(" AND CAST(CS.FECHA AS DATE) <= @Hasta");
                    }
                }

                if (_idLocalFiltro.HasValue)   where.Append(" AND CS.ID_LOCAL = @IdLocal");
                if (tieneCliente)              where.Append(" AND CS.ID_CLIENTE = @IdCliente");
                if (_idUsuarioFiltro.HasValue) where.Append(" AND CS.ID_USUARIO = @IdUsuario");

                var sql = $@"
                    SELECT CS.IDCAB,
                           CONVERT(VARCHAR(10), CS.FECHA, 103)   AS Fecha,
                           CL.NOMBRE_CLIENTE                     AS Cliente,
                           CL.TELEFONO_CLIENTE                   AS Telefono,
                           '#' + CAST(CAST(CS.NSOLICITUD AS BIGINT) AS VARCHAR) AS Solicitud,
                           L.NOMBRE                              AS Local,
                           CS.CUOTAS                             AS Cuotas,
                           CS.TOTAL                              AS Total,
                           CS.DEBE                               AS Debe,
                           CS.HABER                              AS Haber,
                           CS.ESTADO                             AS EstadoCab,
                           U.NOMBRE_USUARIO                      AS Usuario
                    FROM CABECERA_SALES CS
                    INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE
                    LEFT  JOIN LOCALES  L  ON CS.ID_LOCAL   = L.ID_LOCAL
                    LEFT  JOIN USUARIOS U  ON CS.ID_USUARIO = U.ID_USUARIO
                    {where}
                    ORDER BY CS.FECHA DESC";

                using var conn = _db.Create();
                var rows = (await conn.QueryAsync<FilaCredito>(sql, new {
                    Desde      = desde,
                    Hasta      = hasta,
                    IdLocal    = _idLocalFiltro    ?? 0,
                    IdCliente  = _idClienteFiltro  ?? 0,
                    IdUsuario  = _idUsuarioFiltro  ?? 0
                })).ToList();

                _gridCabs.ItemsSource  = rows;
                _gridCuotas.ItemsSource = null;
                _lblCuotasHdr.Text = "📅  Cuotas de la solicitud";

                var totalGs = rows.Sum(r => r.Total);
                var debeGs  = rows.Sum(r => r.Debe);
                _lblConteo.Text = $"{rows.Count} solicitud(es)";
                _lblTotal.Text  = $"Solicitudes: {rows.Count}   |   " +
                                  $"Total otorgado: Gs. {totalGs:N0}   |   " +
                                  $"Total pendiente (Debe): Gs. {debeGs:N0}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al cargar créditos"); }
        }

        private void OnCabSeleccionada(object s, SelectionChangedEventArgs e)
        {
            if (_gridCabs.SelectedItem is not FilaCredito cab) { _gridCuotas.ItemsSource = null; return; }
            _lblCuotasHdr.Text = $"📅  Cuotas  —  {cab.Cliente}  —  Sol: {cab.Solicitud}  —  {cab.Cuotas} cuota(s)";
            _ = CargarCuotas(cab.IdCab);
        }

        private async Task CargarCuotas(int idCab)
        {
            try
            {
                using var conn = _db.Create();
                var cuotas = (await conn.QueryAsync<FilaCuotaCredito>(@"
                    SELECT G.NCUOTA                                  AS NCuota,
                           G.MONTO                                   AS Monto,
                           CONVERT(VARCHAR(10), G.VTO, 103)          AS Vto,
                           ISNULL(G.PUNITORIO, 0)                    AS Punitorio,
                           CASE WHEN G.ESTADO = 1 THEN 'Cancelado'
                                ELSE 'Pendiente' END                 AS Estado,
                           CASE WHEN G.ESTADO = 0 AND G.VTO < GETDATE() THEN CAST(1 AS BIT)
                                ELSE CAST(0 AS BIT) END              AS EsVencida
                    FROM GENERADAS G
                    WHERE G.IDCAB = @Id
                    ORDER BY G.NCUOTA", new { Id = idCab })).ToList();
                _gridCuotas.ItemsSource = cuotas;
            }
            catch { _gridCuotas.ItemsSource = null; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.F5) _ = Buscar();
        }

        private void ImprimirCreditos(bool preview = false)
        {
            var filas = (_gridCabs.ItemsSource as IEnumerable<FilaCredito>)?.ToList();
            if (filas == null || filas.Count == 0)
            { MessageBox.Show("No hay datos para imprimir.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            bool sinFecha = _rbTodos?.IsChecked == true;
            var p = new CreditosPagina
            {
                Filas    = filas.Select(f => new FilaCreditoImp(
                               f.Fecha, f.Cliente, f.Telefono, f.Solicitud,
                               f.Local, f.Cuotas, f.Total, f.Debe, f.Haber, f.Usuario)).ToList(),
                Desde    = sinFecha ? "" : _dpDesde?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
                Hasta    = sinFecha ? "" : _dpHasta?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
                FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
                LogoPath = CreditosPagina.ResolverLogoPath(),
            };

            if (preview)
                new CreditosPreviewWindow(p) { Owner = this }.ShowDialog();
            else
                CreditosImpresora.Imprimir(p, this);
        }
    }

    internal class FilaCredito
    {
        public int     IdCab      { get; set; }
        public string  Fecha      { get; set; } = "";
        public string  Cliente    { get; set; } = "";
        public string  Telefono   { get; set; } = "";
        public string  Solicitud  { get; set; } = "";
        public string  Local      { get; set; } = "";
        public int     Cuotas     { get; set; }
        public decimal Total      { get; set; }
        public decimal Debe       { get; set; }
        public decimal Haber      { get; set; }
        public string  Usuario    { get; set; } = "";
        public int     EstadoCab  { get; set; }
        public string  EstadoStr  => EstadoCab == 1 ? "Activo" : "Cerrado";
        public bool    EsCerrado  => EstadoCab == 0;
    }

    internal class FilaCuotaCredito
    {
        public int     NCuota    { get; set; }
        public decimal Monto     { get; set; }
        public string  Vto       { get; set; } = "";
        public decimal Punitorio { get; set; }
        public string  Estado    { get; set; } = "";
        public bool    EsVencida { get; set; }
        // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial, no una cuota real —
        // ver comentario en Cuota.NCuotaVisible.
        public bool    EsEntrega   => NCuota == 1;
        public string  NCuotaTexto => EsEntrega ? "Entrega" : (NCuota - 1).ToString();
    }

    // ── HISTORIAL VENTAS ──────────────────────────────────────────────────────
    public class HVentasWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly ISessionService      _session;

        private DatePicker _dpDesde = null!, _dpHasta = null!;
        private TextBox    _txtLocal = null!, _txtVendedor = null!;
        private Button     _btnSelLocal = null!, _btnQuitarLocal = null!, _btnQuitarVendedor = null!;
        private int?       _idLocalFiltro    = null;
        private int?       _idUsuarioFiltro  = null;
        private DateTime?  _fechaIngresoFiltro = null;

        private TextBlock _kpiTotal = null!, _kpiCount = null!, _kpiEntrega = null!, _kpiSaldo = null!;
        private TextBlock _lblInfo  = null!;
        private ComboBox  _cmbEstado = null!;
        private ComboBox  _cmbCliente = null!;
        private Border    _totalesBar = null!;
        private TextBlock _totEnt = null!, _totLog = null!, _totDebe = null!, _totHaber = null!;

        private StackPanel   _detallePanel  = null!;
        private ScrollViewer _detalleScroll = null!;
        private Border       _detalleEmpty  = null!;

        private StackPanel   _resumenPanel  = null!;
        private ScrollViewer _resumenScroll    = null!;
        private Border       _resumenEmpty     = null!;
        private Border       _resumenTotBar    = null!;
        private Border       _resumenHeaderBar = null!;
        private TextBlock    _kpiResArts = null!, _kpiResUnits = null!, _kpiResTotal = null!;
        // TextBlocks de la barra de totales del resumen
        private TextBlock _resTotCant = null!, _resTotTotal = null!, _resTotEntrega = null!,
                          _resTotDebe = null!, _resTotHaber = null!, _resTotSaldo  = null!;

        private List<IDictionary<string,object>> _rawRows   = new();
        private Dictionary<int,List<dynamic>>    _artsCache = new();
        private bool _enResumen = false;
        private System.Windows.Threading.DispatcherTimer? _filterDebounce;
        private Border _overlay = null!;

        // Paginación
        private int _pageSize    = 10;
        private int _currentPage = 1;
        private StackPanel _pagerPanel = null!;
        private ComboBox   _cmbPageSize = null!;

        static System.Windows.Media.Color HexC(string h) =>
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h);
        static System.Windows.Media.SolidColorBrush Br(string h) => new(HexC(h));

        public HVentasWindow()
        {
            _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
            _session = SessionService.Instance;
            Title    = "Historial de Ventas — ElectroMar";
            Width = 1020; Height = 650;
            MinWidth = 860; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Br("#EBF5FB");
            BuildUI();

            // Mismo criterio que Informe de Cobranzas (ver HCobranzasWindow): un usuario normal
            // solo debe consultar el historial de SU propio local. Solo ADMINISTRADOR o la
            // excepción puntual (ver Usuario.PuedeVerTodosLosLocales) puede elegir "Todos" u otro.
            if (_session.UsuarioActual?.PuedeVerTodosLosLocales != true && _session.LocalActual != null)
            {
                _idLocalFiltro      = _session.LocalActual.IdLocal;
                _txtLocal.Text      = _session.LocalActual.NombreLocal;
                _txtLocal.FontStyle = FontStyles.Normal;
                _txtLocal.Foreground = Br("#212529");
                _btnSelLocal.Visibility    = Visibility.Collapsed;
                _btnQuitarLocal.Visibility = Visibility.Collapsed;
            }

            Loaded += async (_, _) => await Buscar();
        }

        void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0 header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1 KPIs
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2 totales bar
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3 filtros
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 4 contenido
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5 statusbar

            // HEADER
            var hdr = new Border { Background = Br("#1A5276"), Padding = new Thickness(24,14,24,14) };
            var hdrRow = new DockPanel();
            var btnCerrar = new Button { Content = "✕  Cerrar", FontSize = 12, FontWeight = FontWeights.SemiBold, Background = Br("#C0392B"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Padding = new Thickness(16,8,16,8), Cursor = System.Windows.Input.Cursors.Hand };
            btnCerrar.Click += (_, _) => Close();
            DockPanel.SetDock(btnCerrar, Dock.Right);

            var btnImprimir = new Button { Content = "🖨  Imprimir", FontSize = 12, FontWeight = FontWeights.SemiBold, Background = Br("#1A5276"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Padding = new Thickness(16,8,16,8), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0,0,8,0), ToolTip = "Vista previa e impresión PDF del resultado actual (Detalle o Resumen)" };
            btnImprimir.Click += (_, _) => ImprimirActual();
            DockPanel.SetDock(btnImprimir, Dock.Right);

            var btnExcel = new Button { Content = "📊  Excel", FontSize = 12, FontWeight = FontWeights.SemiBold, Background = Br("#1E6B2E"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Padding = new Thickness(16,8,16,8), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0,0,8,0), ToolTip = "Exportar el resultado actual a Excel (.xlsx), agrupado por local con subtotales" };
            btnExcel.Click += (_, _) => ExportarExcelActual();
            DockPanel.SetDock(btnExcel, Dock.Right);

            hdrRow.Children.Add(btnCerrar);
            hdrRow.Children.Add(btnImprimir);
            hdrRow.Children.Add(btnExcel);
            var hdrStack = new StackPanel();
            hdrStack.Children.Add(new TextBlock { Text = "HISTORIAL DE VENTAS", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Br("#FFFFFF") });
            hdrStack.Children.Add(new TextBlock { Text = $"Usuario: {_session.UsuarioActual?.NombreUsuario ?? "—"}  |  Local: {_session.LocalActual?.NombreLocal ?? "—"}", FontSize = 11, Foreground = Br("#AED6F1") });
            hdrRow.Children.Add(hdrStack);
            hdr.Child = hdrRow;
            Grid.SetRow(hdr, 0); root.Children.Add(hdr);

            // KPIs
            var kpiRow = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(24,12,24,0) };
            kpiRow.Children.Add(MakeKpi("TOTAL VENTAS (Gs.)",    "#1A5276", out _kpiTotal));
            kpiRow.Children.Add(MakeKpi("CANTIDAD",              "#1A5276", out _kpiCount));
            kpiRow.Children.Add(MakeKpi("TOTAL ENTREGA (Gs.)",   "#1A5276", out _kpiEntrega));
            kpiRow.Children.Add(MakeKpi("SALDO PENDIENTE (Gs.)", "#C0392B", out _kpiSaldo));
            Grid.SetRow(kpiRow, 1); root.Children.Add(kpiRow);

            // BARRA RESUMEN TOTAL (fija, fila 2)
            _totalesBar = new Border
            {
                Background = Br("#1A5276"), Visibility = Visibility.Collapsed,
                Padding = new Thickness(24, 10, 24, 10), Margin = new Thickness(0, 8, 0, 0)
            };
            var totGrid = new Grid();
            for (int i = 0; i < 5; i++)
                totGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void TotCol(string lbl, string tooltip, out TextBlock val, int col, bool red = false)
            {
                var sp = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = tooltip };
                sp.Children.Add(new TextBlock { Text = lbl, FontSize = 9, Foreground = Br("#AED6F1"),
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
                val = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold,
                    Foreground = Br(red ? "#F1948A" : "#FFFFFF") };
                sp.Children.Add(val);
                Grid.SetColumn(sp, col); totGrid.Children.Add(sp);
            }

            // Col 0: etiqueta
            var totLbl = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            totLbl.Children.Add(new TextBlock { Text = "RESUMEN", FontSize = 9, Foreground = Br("#AED6F1"), FontWeight = FontWeights.Bold });
            totLbl.Children.Add(new TextBlock { Text = "del período", FontSize = 8, Foreground = Br("#7FB3D3") });
            Grid.SetColumn(totLbl, 0); totGrid.Children.Add(totLbl);

            // Col 1: Total vendido
            TotCol("TOTAL VENDIDO",
                "Suma del precio total de todas las ventas del período",
                out _totEnt, 1);

            // Col 2: Cobrado inicial (entrega)
            TotCol("COBRADO INICIAL",
                "Suma de las entregas iniciales cobradas al momento de la venta",
                out _totLog, 2);

            // Col 3: Saldo pendiente
            TotCol("SALDO PENDIENTE",
                "Lo que aún falta cobrar (Total − lo ya pagado)",
                out _totDebe, 3, true);

            // Col 4: Total ya cobrado
            TotCol("TOTAL COBRADO",
                "Suma de todos los pagos recibidos (entrega inicial + cuotas cobradas)",
                out _totHaber, 4);

            _totalesBar.Child = totGrid;
            Grid.SetRow(_totalesBar, 2); root.Children.Add(_totalesBar);

            // FILTROS
            var fila = new Border { Background = Br("#FFFFFF"), BorderBrush = Br("#DEE2E6"), BorderThickness = new Thickness(0,1,0,1), Padding = new Thickness(24,10,24,10), Margin = new Thickness(0,10,0,0) };
            var frow = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            frow.Children.Add(new TextBlock { Text = "Desde:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0), FontSize = 12 });
            _dpDesde = new DatePicker { SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), Width = 120, Margin = new Thickness(0,0,12,0), FontSize = 12 };
            frow.Children.Add(_dpDesde);

            frow.Children.Add(new TextBlock { Text = "Hasta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0), FontSize = 12 });
            _dpHasta = new DatePicker { SelectedDate = DateTime.Today, Width = 120, Margin = new Thickness(0,0,12,0), FontSize = 12 };
            frow.Children.Add(_dpHasta);

            var btnMes = new Button { Content = "Este mes", Margin = new Thickness(0,0,16,0), Padding = new Thickness(10,5,10,5), FontSize = 12, Background = Br("#1A5276"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            btnMes.Click += (_, _) => { _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _dpHasta.SelectedDate = DateTime.Today; };
            frow.Children.Add(btnMes);

            frow.Children.Add(new TextBlock { Text = "Local:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0), FontSize = 12 });
            _txtLocal = new TextBox { Width = 140, IsReadOnly = true, Text = "Todos los locales", FontStyle = FontStyles.Italic, Foreground = Br("#ADB5BD"), FontSize = 12, Padding = new Thickness(6,4,6,4), Margin = new Thickness(0,0,4,0) };
            frow.Children.Add(_txtLocal);
            _btnSelLocal = new Button { Content = "Sel.", Margin = new Thickness(0,0,4,0), Padding = new Thickness(8,4,8,4), FontSize = 11, Background = Br("#1A5276"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            _btnSelLocal.Click += OnSeleccionarLocal;
            frow.Children.Add(_btnSelLocal);
            _btnQuitarLocal = new Button { Content = "✕", Margin = new Thickness(0,0,16,0), Padding = new Thickness(6,4,6,4), FontSize = 11, Background = Br("#C0392B"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, Cursor = System.Windows.Input.Cursors.Hand };
            _btnQuitarLocal.Click += (_, _) => { _idLocalFiltro = null; _txtLocal.Text = "Todos los locales"; _txtLocal.FontStyle = FontStyles.Italic; _txtLocal.Foreground = Br("#ADB5BD"); _btnQuitarLocal.Visibility = Visibility.Collapsed; };
            frow.Children.Add(_btnQuitarLocal);

            frow.Children.Add(new TextBlock { Text = "Vendedor:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0), FontSize = 12 });
            _txtVendedor = new TextBox { Width = 160, IsReadOnly = true, Text = "Todos los vendedores", FontStyle = FontStyles.Italic, Foreground = Br("#ADB5BD"), FontSize = 12, Padding = new Thickness(6,4,6,4), Margin = new Thickness(0,0,4,0) };
            frow.Children.Add(_txtVendedor);
            var btnSelVend = new Button { Content = "Sel.", Margin = new Thickness(0,0,4,0), Padding = new Thickness(8,4,8,4), FontSize = 11, Background = Br("#1A5276"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            btnSelVend.Click += OnSeleccionarVendedor;
            frow.Children.Add(btnSelVend);
            _btnQuitarVendedor = new Button { Content = "✕", Margin = new Thickness(0,0,16,0), Padding = new Thickness(6,4,6,4), FontSize = 11, Background = Br("#C0392B"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, Cursor = System.Windows.Input.Cursors.Hand };
            _btnQuitarVendedor.Click += (_, _) => { _idUsuarioFiltro = null; _fechaIngresoFiltro = null; _txtVendedor.Text = "Todos los vendedores"; _txtVendedor.FontStyle = FontStyles.Italic; _txtVendedor.Foreground = Br("#ADB5BD"); _btnQuitarVendedor.Visibility = Visibility.Collapsed; };
            frow.Children.Add(_btnQuitarVendedor);

            var btnBuscar = new Button { Content = "🔍  Buscar", Padding = new Thickness(16,6,16,6), FontSize = 12, FontWeight = FontWeights.SemiBold, Background = Br("#C0392B"), Foreground = Br("#FFFFFF"), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            btnBuscar.Click += async (_, _) => await Buscar();
            frow.Children.Add(btnBuscar);

            // ── FILTRO ESTADO ─────────────────────────────────────────────────
            frow.Children.Add(new TextBlock { Text = "Estado:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0), FontSize = 12 });
            _cmbEstado = new ComboBox
            {
                Width = 110, FontSize = 12,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(6, 4, 6, 4),
                ToolTip = "Filtra por estado de cobro de la venta"
            };
            _cmbEstado.Items.Add(new ComboBoxItem
            {
                Content = "Pendientes", Tag = "1",
                ToolTip = "Ventas con saldo aún por cobrar (créditos activos o contados con pago parcial)"
            });
            _cmbEstado.Items.Add(new ComboBoxItem
            {
                Content = "Cerradas", Tag = "0",
                ToolTip = "Ventas ya cobradas al 100% (contados cobrados y créditos saldados)"
            });
            _cmbEstado.Items.Add(new ComboBoxItem
            {
                Content = "Todas", Tag = "todos",
                ToolTip = "Muestra todas las ventas sin importar el estado de cobro"
            });
            _cmbEstado.SelectedIndex = 0;
            _cmbEstado.SelectionChanged += OnEstadoChanged;
            frow.Children.Add(_cmbEstado);

            // ── FILTRO CLIENTE ────────────────────────────────────────────────
            frow.Children.Add(new TextBlock { Text = "Cliente:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0), FontSize = 12 });
            _cmbCliente = new ComboBox { Width = 155, FontSize = 12, Margin = new Thickness(0), Padding = new Thickness(6, 4, 6, 4),
                ToolTip = "Filtra por tipo de cliente registrado en la venta" };
            _cmbCliente.Items.Add(new ComboBoxItem { Content = "Todos los clientes",  Tag = "todos",
                ToolTip = "Muestra ventas de cualquier tipo de cliente" });
            _cmbCliente.Items.Add(new ComboBoxItem { Content = "Cliente identificado", Tag = "id",
                ToolTip = "Ventas donde el cliente tiene nombre y datos registrados" });
            _cmbCliente.Items.Add(new ComboBoxItem { Content = "Consumidor final",    Tag = "xxx",
                ToolTip = "Ventas realizadas sin identificar al comprador (cliente genérico)" });
            _cmbCliente.SelectedIndex = 0;
            _cmbCliente.SelectionChanged += OnClienteChanged;
            frow.Children.Add(_cmbCliente);

            fila.Child = frow;
            Grid.SetRow(fila, 3); root.Children.Add(fila);

            // CONTENIDO
            var contenido = new Grid { Margin = new Thickness(24,10,24,0) };
            contenido.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // 0: tabs
            contenido.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: lista
            contenido.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // 2: paginador

            var tabBar = new Border { Background = Br("#FFFFFF"), BorderBrush = Br("#DEE2E6"), BorderThickness = new Thickness(1,1,1,0), Padding = new Thickness(12,8,12,8) };
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnTabDet = MakeTabBtn("📋  Detalle", true);
            var btnTabRes = MakeTabBtn("📊  Resumen", false);
            tabRow.Children.Add(btnTabDet); tabRow.Children.Add(btnTabRes);
            tabBar.Child = tabRow;
            Grid.SetRow(tabBar, 0); contenido.Children.Add(tabBar);

            var areaBorder = new Border { Background = Br("#FFFFFF"), BorderBrush = Br("#DEE2E6"), BorderThickness = new Thickness(1,0,1,1) };
            var areaGrid = new Grid();

            // Tab Detalle
            _detallePanel  = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            _detalleScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _detallePanel };
            _detalleEmpty  = MakeEmpty("No se encontraron ventas para el período seleccionado");
            var detalleHost = new Grid();
            detalleHost.Children.Add(_detalleScroll);
            detalleHost.Children.Add(_detalleEmpty);

            // Tab Resumen — layout: [totales fijos] [header cols] [scroll filas] [empty]
            _resumenPanel  = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            _resumenScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _resumenPanel };
            _resumenEmpty  = MakeEmpty("Sin resultados para este período");

            // Barra de totales fija — misma grilla 8 cols que las filas
            _resumenTotBar = new Border { Background = Br("#1A5276"), Visibility = Visibility.Collapsed,
                Padding = new Thickness(0, 8, 0, 8) };
            {
                var tg = MakeResumenGrid();
                void RT(string lbl, out TextBlock tb, int col, bool rojo = false)
                {
                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8, Foreground = Br("#AED6F1"),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(col == 0 ? 12 : 6, 0, 6, 1),
                        TextAlignment = col <= 1 ? TextAlignment.Left : TextAlignment.Right });
                    tb = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold,
                        Foreground = Br(rojo ? "#F1948A" : "#FFFFFF"),
                        Margin = new Thickness(col == 0 ? 12 : 6, 0, 6, 0),
                        TextAlignment = col <= 1 ? TextAlignment.Left : TextAlignment.Right };
                    sp.Children.Add(tb);
                    Grid.SetColumn(sp, col); tg.Children.Add(sp);
                }
                RT("TOTALES DEL PERÍODO", out _resTotCant,    0);
                RT("",                    out _resTotTotal,   1);
                RT("VENTAS",              out _resTotEntrega, 2);
                RT("TOTAL VENDIDO",       out _resTotDebe,    3);
                RT("COBRADO INICIAL",     out _resTotHaber,   4);
                RT("DEBE",                out _resTotSaldo,   5);
                TextBlock _th, _ts;
                RT("COBRADO",    out _th, 6);
                RT("SALDO PEND.", out _ts, 7, rojo: true);
                _resumenTotBar.Child = tg;
            }

            // Header de columnas fijo
            _resumenHeaderBar = new Border { Visibility = Visibility.Collapsed };
            _resumenHeaderBar.Child = BuildResumenHeader();

            var resumenHost = new Grid();
            resumenHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: totales
            resumenHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: header cols
            resumenHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: filas
            Grid.SetRow(_resumenTotBar,    0); resumenHost.Children.Add(_resumenTotBar);
            Grid.SetRow(_resumenHeaderBar, 1); resumenHost.Children.Add(_resumenHeaderBar);
            var resumenScrollHost = new Grid();
            resumenScrollHost.Children.Add(_resumenScroll);
            resumenScrollHost.Children.Add(_resumenEmpty);
            Grid.SetRow(resumenScrollHost, 2); resumenHost.Children.Add(resumenScrollHost);

            // Overlay
            _overlay = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(210,255,255,255)),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = "⏳  Buscando...", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Br("#1A5276"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };

            areaGrid.Children.Add(detalleHost);
            areaGrid.Children.Add(resumenHost);
            areaGrid.Children.Add(_overlay);
            resumenHost.Visibility = Visibility.Collapsed;
            areaBorder.Child = areaGrid;
            Grid.SetRow(areaBorder, 1); contenido.Children.Add(areaBorder);

            // ── BARRA PAGINADOR (fila 2 del contenido) ───────────────────────
            var pagerBar = new Border
            {
                Background = Br("#FFFFFF"), BorderBrush = Br("#D6EAF8"),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Padding = new Thickness(16, 8, 16, 8)
            };
            var pagerRow = new DockPanel { LastChildFill = false };

            // Izquierda: selector "por página"
            var leftSide = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftSide.Children.Add(new TextBlock { Text = "Por página:", FontSize = 11, Foreground = Br("#546E7A"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
            _cmbPageSize = new ComboBox { FontSize = 11, Width = 70, VerticalAlignment = VerticalAlignment.Center };
            _cmbPageSize.Items.Add(new ComboBoxItem { Content = "5",    Tag = 5  });
            _cmbPageSize.Items.Add(new ComboBoxItem { Content = "10",   Tag = 10 });
            _cmbPageSize.Items.Add(new ComboBoxItem { Content = "20",   Tag = 20 });
            _cmbPageSize.Items.Add(new ComboBoxItem { Content = "Todos", Tag = 0 });
            _cmbPageSize.SelectedIndex = 1;
            _cmbPageSize.SelectionChanged += (_, _) =>
            {
                var tag = (_cmbPageSize.SelectedItem as ComboBoxItem)?.Tag;
                _pageSize = tag is int i ? i : 10;
                _currentPage = 1;
                _ = RenderCurrentPage();
            };
            leftSide.Children.Add(_cmbPageSize);
            DockPanel.SetDock(leftSide, Dock.Left);
            pagerRow.Children.Add(leftSide);

            // Derecha: botones de página
            _pagerPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_pagerPanel, Dock.Right);
            pagerRow.Children.Add(_pagerPanel);

            pagerBar.Child = pagerRow;
            Grid.SetRow(pagerBar, 2); contenido.Children.Add(pagerBar);

            Grid.SetRow(contenido, 4); root.Children.Add(contenido);

            // Lógica tabs
            btnTabDet.Click += (_, _) =>
            {
                _enResumen = false;
                detalleHost.Visibility = Visibility.Visible; resumenHost.Visibility = Visibility.Collapsed;
                btnTabDet.Background = Br("#1A5276"); btnTabDet.Foreground = Br("#FFFFFF");
                btnTabRes.Background = Br("#D6EAF8"); btnTabRes.Foreground = Br("#1A5276");
            };
            btnTabRes.Click += async (_, _) =>
            {
                _enResumen = true;
                detalleHost.Visibility = Visibility.Collapsed; resumenHost.Visibility = Visibility.Visible;
                btnTabDet.Background = Br("#D6EAF8"); btnTabDet.Foreground = Br("#1A5276");
                btnTabRes.Background = Br("#1A5276"); btnTabRes.Foreground = Br("#FFFFFF");
                await BuscarResumen();
            };

            // Status bar (solo info)
            var sb = new Border { Background = Br("#FFFFFF"), BorderBrush = Br("#D6EAF8"), BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(16,6,16,6), Margin = new Thickness(0,4,0,0) };
            _lblInfo = new TextBlock { FontSize = 11, Foreground = Br("#6C757D"), VerticalAlignment = VerticalAlignment.Center };
            sb.Child = _lblInfo;
            Grid.SetRow(sb, 5); root.Children.Add(sb);

            Content = root;
        }

        static Button MakeTabBtn(string txt, bool activo) => new Button
        {
            Content = txt, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0,0,6,0), BorderThickness = new Thickness(0),
            Padding = new Thickness(18,7,18,7), Cursor = System.Windows.Input.Cursors.Hand,
            Background = activo ? Br("#1A5276") : Br("#D6EAF8"),
            Foreground = activo ? Br("#FFFFFF") : Br("#1A5276")
        };

        static Border MakeKpi(string titulo, string color, out TextBlock valor)
        {
            var v = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Br(color), Text = "—" };
            valor = v;
            return new Border
            {
                Background = Br("#FFFFFF"), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18,12,18,12), Margin = new Thickness(0,0,10,0),
                BorderBrush = Br(color), BorderThickness = new Thickness(0,4,0,0),
                Child = new StackPanel { Children = { new TextBlock { Text = titulo, FontSize = 11, Foreground = Br("#7F8C8D"), FontWeight = FontWeights.SemiBold }, v } }
            };
        }

        static Border MakeEmpty(string msg) => new Border
        {
            Visibility = Visibility.Collapsed,
            Child = new TextBlock { Text = msg, FontSize = 14, Foreground = Br("#ADB5BD"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,40,0,0) }
        };

        string BuildWhere()
        {
            var w = "WHERE CS.FECHA >= @Desde AND CS.FECHA < @Hasta";
            var estadoTag  = (_cmbEstado.SelectedItem  as ComboBoxItem)?.Tag?.ToString() ?? "1";
            var clienteTag = (_cmbCliente.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "todos";
            if (estadoTag == "1")    w += " AND CS.ESTADO = 1";
            else if (estadoTag == "0") w += " AND CS.ESTADO = 0";
            if (clienteTag == "xxx") w += " AND UPPER(RTRIM(LTRIM(CL.NOMBRE_CLIENTE))) = 'XXX'";
            else if (clienteTag == "id") w += " AND UPPER(RTRIM(LTRIM(CL.NOMBRE_CLIENTE))) <> 'XXX'";
            if (_idLocalFiltro.HasValue)   w += " AND CS.ID_LOCAL = @IdLocal";
            if (_idUsuarioFiltro.HasValue) w += " AND CS.ID_USUARIO = @IdUsuario";
            return w;
        }

        object BuildParams(DateTime desde, DateTime hasta) => new { Desde = desde, Hasta = hasta, IdLocal = _idLocalFiltro ?? 0, IdUsuario = _idUsuarioFiltro ?? 0 };

        (DateTime desde, DateTime hasta) GetRango()
        {
            var desdeRaw = _dpDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var hasta    = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);
            var desde    = (_fechaIngresoFiltro.HasValue && _fechaIngresoFiltro.Value > desdeRaw) ? _fechaIngresoFiltro.Value.Date : desdeRaw;
            return (desde, hasta);
        }

        const string SqlCab = @"
            SELECT CS.IDCAB, CS.NSOLICITUD, CS.FECHA,
                CASE CS.FORMA_DE_VENTA WHEN 1 THEN 'Contado' WHEN 2 THEN 'Crédito' ELSE 'Otro' END AS TIPO_VENTA,
                L.NOMBRE AS LOCAL, CL.NOMBRE_CLIENTE AS CLIENTE, U.NOMBRE_USUARIO AS VENDEDOR,
                CS.TOTAL, CS.ENTREGANORMAL, CS.ENTREGALOGISTICA,
                (CS.TOTAL - CS.ENTREGANORMAL) AS SALDO,
                CS.CUOTAS, CS.MONTO_CUOTA AS MONTOCUOTA, CS.DEBE, CS.HABER,
                CASE CS.ESTADO WHEN 1 THEN 'Pendiente' WHEN 0 THEN 'Cerrada' ELSE '?' END AS ESTADO_TXT
            FROM CABECERA_SALES CS
            INNER JOIN LOCALES  L  ON CS.ID_LOCAL   = L.ID_LOCAL
            INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE
            INNER JOIN USUARIOS U  ON CS.ID_USUARIO = U.ID_USUARIO";

        const string SqlArts = @"
            SELECT DS.IDCAB, A.D AS ARTICULO, DS.CANTIDAD, DS.PV AS PRECIO,
                   (DS.CANTIDAD * DS.PV) AS SUBTOTAL
            FROM DETALLES_SALES DS
            INNER JOIN ARTICULOS A ON DS.IDART = A.ID
            WHERE DS.IDCAB IN @Ids";

        private void OnEstadoChanged(object? s, SelectionChangedEventArgs e) => DispararBusquedaDemorada();
        private void OnClienteChanged(object? s, SelectionChangedEventArgs e) => DispararBusquedaDemorada();

        void DispararBusquedaDemorada()
        {
            if (_filterDebounce == null)
            {
                _filterDebounce = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(300) };
                _filterDebounce.Tick += async (_, _) =>
                {
                    _filterDebounce.Stop();
                    await Buscar();
                };
            }
            _filterDebounce.Stop();
            _filterDebounce.Start();
        }

        private async Task Buscar()
        {
            if (_enResumen) { await BuscarResumen(); return; }
            _overlay.Visibility = Visibility.Visible;
            _detallePanel.Children.Clear();
            _rawRows.Clear(); _artsCache.Clear(); _currentPage = 1;
            _detalleEmpty.Visibility = Visibility.Collapsed;
            _detalleScroll.Visibility = Visibility.Visible;
            _pagerPanel.Children.Clear();
            try
            {
                var (desde, hasta) = GetRango();
                using var conn = _db.Create();
                _rawRows = (await conn.QueryAsync<dynamic>($"{SqlCab} {BuildWhere()} ORDER BY CS.FECHA DESC", BuildParams(desde, hasta)))
                           .Cast<IDictionary<string,object>>().ToList();

                decimal tot = 0, ent = 0, sal = 0;
                foreach (var r in _rawRows)
                {
                    if (r["TOTAL"]         != null) tot += Convert.ToDecimal(r["TOTAL"]);
                    if (r["ENTREGANORMAL"] != null) ent += Convert.ToDecimal(r["ENTREGANORMAL"]);
                    if (r["SALDO"]         != null) sal += Convert.ToDecimal(r["SALDO"]);
                }
                _kpiTotal.Text   = $"Gs. {tot:N0}";
                _kpiCount.Text   = $"{_rawRows.Count}";
                _kpiEntrega.Text = $"Gs. {ent:N0}";
                _kpiSaldo.Text   = $"Gs. {sal:N0}";

                if (_rawRows.Count == 0)
                {
                    _detalleEmpty.Visibility  = Visibility.Visible;
                    _detalleScroll.Visibility = Visibility.Collapsed;
                    _totalesBar.Visibility    = Visibility.Collapsed;

                    // Dar contexto útil: ¿hay registros con otro estado?
                    var estadoTag  = (_cmbEstado.SelectedItem  as ComboBoxItem)?.Tag?.ToString() ?? "1";
                    var clienteTag = (_cmbCliente.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "todos";
                    if (estadoTag != "todos")
                    {
                        var (d2, h2) = GetRango();
                        var altWhere = BuildWhereWith(estadoTag == "1" ? "0" : "1", clienteTag);
                        var altCount = await conn.ExecuteScalarAsync<int>(
                            $"SELECT COUNT(*) FROM CABECERA_SALES CS INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE {altWhere}",
                            BuildParams(d2, h2));
                        var altLabel  = estadoTag == "1" ? "cerradas" : "pendientes de cobro";
                        var altOpcion = estadoTag == "1" ? "Cerradas" : "Pendientes";
                        _lblInfo.Text = altCount > 0
                            ? $"Sin resultados — hay {altCount} venta(s) {altLabel} con este filtro. Cambiá \"Estado\" a \"{altOpcion}\" o \"Todas\"."
                            : "Sin resultados para el período y filtros seleccionados.";
                    }
                    else
                    {
                        _lblInfo.Text = "Sin resultados para el período y filtros seleccionados.";
                    }
                    return;
                }

                _overlay.Visibility = Visibility.Collapsed;
                await RenderCurrentPage();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al buscar", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _overlay.Visibility = Visibility.Collapsed; }
        }

        string BuildWhereWith(string estadoTag, string clienteTag)
        {
            var w = "WHERE CS.FECHA >= @Desde AND CS.FECHA < @Hasta";
            if (estadoTag == "1")      w += " AND CS.ESTADO = 1";
            else if (estadoTag == "0") w += " AND CS.ESTADO = 0";
            if (clienteTag == "xxx")   w += " AND UPPER(RTRIM(LTRIM(CL.NOMBRE_CLIENTE))) = 'XXX'";
            else if (clienteTag == "id") w += " AND UPPER(RTRIM(LTRIM(CL.NOMBRE_CLIENTE))) <> 'XXX'";
            if (_idLocalFiltro.HasValue)   w += " AND CS.ID_LOCAL = @IdLocal";
            if (_idUsuarioFiltro.HasValue) w += " AND CS.ID_USUARIO = @IdUsuario";
            return w;
        }

        private void ExportarExcelActual()
        {
            if (_enResumen)
            {
                _ = ExportarExcelResumenAsync();
            }
            else
            {
                if (_rawRows.Count == 0)
                {
                    MessageBox.Show("No hay datos para exportar. Realizá una búsqueda primero.",
                        "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var pagina = BuildPaginaDetalle();
                HVentasExcel.ExportarDetalle(pagina);
            }
        }

        private async Task ExportarExcelResumenAsync()
        {
            _overlay.Visibility = Visibility.Visible;
            try
            {
                var (desde, hasta) = GetRango();
                using var conn = _db.Create();
                var sqlRes = $@"
                    SELECT L.NOMBRE AS LOCAL, U.NOMBRE_USUARIO AS VENDEDOR,
                           CS.NSOLICITUD AS SOLICITUD, CS.TOTAL AS TOTAL,
                           CS.ENTREGANORMAL AS ENTREGA, CS.DEBE AS DEBE,
                           CS.HABER AS HABER, (CS.DEBE - CS.HABER) AS SALDO
                    FROM CABECERA_SALES CS
                    INNER JOIN LOCALES  L  ON CS.ID_LOCAL   = L.ID_LOCAL
                    INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE
                    INNER JOIN USUARIOS U  ON CS.ID_USUARIO = U.ID_USUARIO
                    {BuildWhere()}
                    ORDER BY L.NOMBRE, U.NOMBRE_USUARIO, CS.FECHA DESC";
                var rows = (await conn.QueryAsync<dynamic>(sqlRes, BuildParams(desde, hasta)))
                           .Cast<IDictionary<string,object>>().ToList();
                if (rows.Count == 0)
                {
                    MessageBox.Show("No hay datos para exportar con los filtros actuales.",
                        "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                decimal sumT = 0, sumE = 0, sumS = 0;
                var filas = new List<HVentasFilaResumen>();
                foreach (var r in rows)
                {
                    var t = r["TOTAL"]   != null ? Convert.ToDecimal(r["TOTAL"])   : 0m;
                    var e = r["ENTREGA"] != null ? Convert.ToDecimal(r["ENTREGA"]) : 0m;
                    var d = r["DEBE"]    != null ? Convert.ToDecimal(r["DEBE"])    : 0m;
                    var h = r["HABER"]   != null ? Convert.ToDecimal(r["HABER"])   : 0m;
                    var s = r["SALDO"]   != null ? Convert.ToDecimal(r["SALDO"])   : 0m;
                    sumT += t; sumE += e; sumS += Math.Max(0m, s);
                    filas.Add(new HVentasFilaResumen
                    {
                        Local = r["LOCAL"]?.ToString() ?? "—", Vendedor = r["VENDEDOR"]?.ToString() ?? "—",
                        Solicitud = r["SOLICITUD"]?.ToString() ?? "—",
                        Total = t, Entrega = e, Debe = d, Haber = h, Saldo = s,
                    });
                }
                var pagina = new HVentasPagina
                {
                    Resumen = filas, SumTotal = sumT, SumEntrega = sumE, SumSaldo = sumS,
                    Cantidad = filas.Count, Filtro = BuildFiltroTxt(),
                    FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    Usuario  = _session.UsuarioActual?.NombreUsuario ?? "—",
                    LogoPath = CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
                };
                HVentasExcel.ExportarResumen(pagina);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al exportar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _overlay.Visibility = Visibility.Collapsed; }
        }

        private HVentasPagina BuildPaginaDetalle()
        {
            decimal sumT = 0, sumE = 0, sumS = 0;
            var filas = new List<HVentasFilaDetalle>();
            foreach (var r in _rawRows)
            {
                var t = r["TOTAL"]         != null ? Convert.ToDecimal(r["TOTAL"])         : 0m;
                var e = r["ENTREGANORMAL"] != null ? Convert.ToDecimal(r["ENTREGANORMAL"]) : 0m;
                var d = r["DEBE"]          != null ? Convert.ToDecimal(r["DEBE"])          : 0m;
                var h = r["HABER"]         != null ? Convert.ToDecimal(r["HABER"])         : 0m;
                var saldo = Math.Max(0m, d - h);
                sumT += t; sumE += e; sumS += saldo;
                filas.Add(new HVentasFilaDetalle
                {
                    Local     = r["LOCAL"]?.ToString()      ?? "—",
                    Vendedor  = r["VENDEDOR"]?.ToString()   ?? "—",
                    Solicitud = r["NSOLICITUD"]?.ToString() ?? "—",
                    Tipo      = r["TIPO_VENTA"]?.ToString() ?? "—",
                    Cliente   = r["CLIENTE"]?.ToString()    ?? "—",
                    Total     = t, Entrega = e, Saldo = saldo,
                    Estado    = r["ESTADO_TXT"]?.ToString() ?? "—",
                    Fecha     = r["FECHA"] != null ? Convert.ToDateTime(r["FECHA"]).ToString("dd/MM/yy") : "—",
                });
            }
            return new HVentasPagina
            {
                Detalle = filas, SumTotal = sumT, SumEntrega = sumE, SumSaldo = sumS,
                Cantidad = filas.Count, Filtro = BuildFiltroTxt(),
                FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario  = _session.UsuarioActual?.NombreUsuario ?? "—",
                LogoPath = CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
            };
        }

        private string BuildFiltroTxt()
        {
            var estadoTag  = (_cmbEstado.SelectedItem  as ComboBoxItem)?.Tag?.ToString() ?? "todos";
            var clienteTag = (_cmbCliente.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "todos";
            var estadoLbl  = estadoTag  switch { "1" => "Pendientes", "0" => "Cerradas", _ => "Todas" };
            var clienteLbl = clienteTag switch { "xxx" => "Consumidor final", "id" => "Cliente identificado", _ => "Todos los clientes" };
            var partes = new List<string> { $"Estado: {estadoLbl}", $"Cliente: {clienteLbl}" };
            if (_idLocalFiltro.HasValue)   partes.Add($"Local: {_txtLocal.Text}");
            if (_idUsuarioFiltro.HasValue) partes.Add($"Vendedor: {_txtVendedor.Text}");
            partes.Add($"Período: {_dpDesde.SelectedDate?.ToString("dd/MM/yyyy")} al {_dpHasta.SelectedDate?.ToString("dd/MM/yyyy")}");
            return string.Join("  |  ", partes);
        }

        private void ImprimirActual()
        {
            if (_enResumen)
            {
                _ = ImprimirResumenAsync(BuildFiltroTxt(),
                    CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
                    _session.UsuarioActual?.NombreUsuario ?? "—",
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            }
            else
            {
                if (_rawRows.Count == 0)
                {
                    MessageBox.Show("No hay datos para imprimir. Realizá una búsqueda primero.",
                        "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var preview = new HVentasDetallePreviewWindow(BuildPaginaDetalle()) { Owner = this };
                preview.Show();
            }
        }

        private async Task ImprimirResumenAsync(string filtroTxt, string logoPath, string usuario, string fechaImp)
        {
            _overlay.Visibility = Visibility.Visible;
            try
            {
                var (desde, hasta) = GetRango();
                using var conn = _db.Create();
                var sqlRes = $@"
                    SELECT L.NOMBRE AS LOCAL, U.NOMBRE_USUARIO AS VENDEDOR,
                           CS.NSOLICITUD AS SOLICITUD, CS.TOTAL AS TOTAL,
                           CS.ENTREGANORMAL AS ENTREGA, CS.DEBE AS DEBE,
                           CS.HABER AS HABER, (CS.DEBE - CS.HABER) AS SALDO
                    FROM CABECERA_SALES CS
                    INNER JOIN LOCALES  L  ON CS.ID_LOCAL   = L.ID_LOCAL
                    INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE
                    INNER JOIN USUARIOS U  ON CS.ID_USUARIO = U.ID_USUARIO
                    {BuildWhere()}
                    ORDER BY L.NOMBRE, U.NOMBRE_USUARIO, CS.FECHA DESC";

                var rows = (await conn.QueryAsync<dynamic>(sqlRes, BuildParams(desde, hasta)))
                           .Cast<IDictionary<string,object>>().ToList();

                if (rows.Count == 0)
                {
                    MessageBox.Show("No hay datos para imprimir con los filtros actuales.",
                        "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                decimal sumT = 0, sumE = 0, sumS = 0;
                var filas = new List<HVentasFilaResumen>();
                foreach (var r in rows)
                {
                    var t  = r["TOTAL"]   != null ? Convert.ToDecimal(r["TOTAL"])   : 0m;
                    var e  = r["ENTREGA"] != null ? Convert.ToDecimal(r["ENTREGA"]) : 0m;
                    var d  = r["DEBE"]    != null ? Convert.ToDecimal(r["DEBE"])    : 0m;
                    var h  = r["HABER"]   != null ? Convert.ToDecimal(r["HABER"])   : 0m;
                    var s  = r["SALDO"]   != null ? Convert.ToDecimal(r["SALDO"])   : 0m;
                    sumT += t; sumE += e; sumS += Math.Max(0m, s);
                    filas.Add(new HVentasFilaResumen
                    {
                        Local    = r["LOCAL"]?.ToString()    ?? "—",
                        Vendedor = r["VENDEDOR"]?.ToString() ?? "—",
                        Solicitud= r["SOLICITUD"]?.ToString()?? "—",
                        Total    = t, Entrega = e, Debe = d, Haber = h, Saldo = s,
                    });
                }

                var pagina = new HVentasPagina
                {
                    Resumen   = filas,
                    SumTotal  = sumT,
                    SumEntrega= sumE,
                    SumSaldo  = sumS,
                    Cantidad  = filas.Count,
                    Filtro    = filtroTxt,
                    FechaImp  = fechaImp,
                    Usuario   = usuario,
                    LogoPath  = logoPath,
                };
                var preview = new HVentasResumenPreviewWindow(pagina) { Owner = this };
                preview.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al preparar impresión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _overlay.Visibility = Visibility.Collapsed; }
        }

        private async Task BuscarResumen()
        {
            _overlay.Visibility = Visibility.Visible;
            _resumenPanel.Children.Clear();
            _resumenEmpty.Visibility  = Visibility.Collapsed;
            _resumenScroll.Visibility = Visibility.Visible;
            _pagerPanel.Children.Clear();
            try
            {
                var (desde, hasta) = GetRango();
                using var conn = _db.Create();

                // Query agrupada por local + vendedor (igual al reporte VB6)
                var sqlRes = $@"
                    SELECT
                        L.NOMBRE                          AS LOCAL,
                        U.NOMBRE_USUARIO                  AS VENDEDOR,
                        CS.NSOLICITUD                     AS SOLICITUD,
                        CS.TOTAL                          AS TOTAL,
                        CS.ENTREGANORMAL                  AS ENTREGA,
                        CS.DEBE                           AS DEBE,
                        CS.HABER                          AS HABER,
                        (CS.DEBE - CS.HABER)              AS SALDO
                    FROM CABECERA_SALES CS
                    INNER JOIN LOCALES  L  ON CS.ID_LOCAL   = L.ID_LOCAL
                    INNER JOIN CLIENTES CL ON CS.ID_CLIENTE = CL.ID_CLIENTE
                    INNER JOIN USUARIOS U  ON CS.ID_USUARIO = U.ID_USUARIO
                    {BuildWhere()}
                    ORDER BY L.NOMBRE, U.NOMBRE_USUARIO, CS.FECHA DESC";

                var filas = (await conn.QueryAsync<dynamic>(sqlRes, BuildParams(desde, hasta)))
                            .Cast<IDictionary<string,object>>().ToList();

                if (filas.Count == 0)
                {
                    _resumenEmpty.Visibility    = Visibility.Visible;
                    _resumenScroll.Visibility   = Visibility.Collapsed;
                    _resumenTotBar.Visibility    = Visibility.Collapsed;
                    _resumenHeaderBar.Visibility = Visibility.Collapsed;
                    _lblInfo.Text = "Sin resultados";
                    return;
                }

                // Calcular totales una sola vez
                decimal totTotal   = filas.Sum(r => r["TOTAL"]   != null ? Convert.ToDecimal(r["TOTAL"])   : 0m);
                decimal totEntrega = filas.Sum(r => r["ENTREGA"] != null ? Convert.ToDecimal(r["ENTREGA"]) : 0m);
                decimal totDebe    = filas.Sum(r => r["DEBE"]    != null ? Convert.ToDecimal(r["DEBE"])    : 0m);
                decimal totHaber   = filas.Sum(r => r["HABER"]   != null ? Convert.ToDecimal(r["HABER"])   : 0m);
                decimal totSaldo   = filas.Sum(r => r["SALDO"]   != null ? Convert.ToDecimal(r["SALDO"])   : 0m);

                // ── Barra de totales fija (arriba) ────────────────────────────
                _resTotCant.Text    = $"{filas.Count} venta(s)";
                _resTotTotal.Text   = "";
                _resTotEntrega.Text = $"Gs. {filas.Count:N0}";
                _resTotDebe.Text    = $"Gs. {totTotal:N0}";
                _resTotHaber.Text   = $"Gs. {totEntrega:N0}";
                _resTotSaldo.Text   = $"Gs. {totDebe:N0}";
                // acceder a los últimos 2 TB por referencia directa (HABER y SALDO en cols 6/7)
                // Se actualizan via los campos _th/_ts que son locales — usamos los campos de instancia
                // reasignamos via cast del child del grid
                var tg2 = (Grid)_resumenTotBar.Child;
                ((StackPanel)tg2.Children[6]).Children.OfType<TextBlock>().Last().Text = $"Gs. {totHaber:N0}";
                ((StackPanel)tg2.Children[7]).Children.OfType<TextBlock>().Last().Text = totSaldo > 0 ? $"Gs. {totSaldo:N0}" : "—";
                _resumenTotBar.Visibility    = Visibility.Visible;
                _resumenHeaderBar.Visibility = Visibility.Visible;

                // ── Filas agrupadas por local ─────────────────────────────────
                string localActual = "";
                bool zebraOscuro = false;
                foreach (var r in filas)
                {
                    var local     = r["LOCAL"]?.ToString()     ?? "—";
                    var vendedor  = r["VENDEDOR"]?.ToString()  ?? "—";
                    var solicitud = r["SOLICITUD"]?.ToString() ?? "—";
                    var total     = r["TOTAL"]   != null ? Convert.ToDecimal(r["TOTAL"])   : 0m;
                    var entrega   = r["ENTREGA"] != null ? Convert.ToDecimal(r["ENTREGA"]) : 0m;
                    var debe      = r["DEBE"]    != null ? Convert.ToDecimal(r["DEBE"])    : 0m;
                    var haber     = r["HABER"]   != null ? Convert.ToDecimal(r["HABER"])   : 0m;
                    var saldo     = r["SALDO"]   != null ? Convert.ToDecimal(r["SALDO"])   : 0m;

                    if (local != localActual)
                    {
                        if (localActual != "") _resumenPanel.Children.Add(BuildResumenGrupoDivider());
                        localActual  = local;
                        zebraOscuro  = false;
                    }

                    _resumenPanel.Children.Add(BuildResumenFila(
                        local, vendedor, solicitud, total, entrega, debe, haber, saldo, zebraOscuro));
                    zebraOscuro = !zebraOscuro;
                }

                _lblInfo.Text = $"Resumen: {filas.Count} venta(s) | Total vendido Gs. {totTotal:N0} | Saldo pendiente Gs. {totSaldo:N0}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al buscar resumen", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _overlay.Visibility = Visibility.Collapsed; }
        }

        // Anchos de columna para la tabla resumen (compartidos entre header y filas)
        static readonly GridLength[] ResumenCols =
        {
            new GridLength(140),                          // LOCAL
            new GridLength(1, GridUnitType.Star),         // VENDEDOR
            new GridLength(150),                          // Nº SOLICITUD
            new GridLength(110),                          // TOTAL
            new GridLength(110),                          // ENTREGA
            new GridLength(110),                          // DEBE
            new GridLength(110),                          // HABER
            new GridLength(110),                          // SALDO
        };

        static Grid MakeResumenGrid()
        {
            var g = new Grid();
            foreach (var w in ResumenCols)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            return g;
        }

        static UIElement BuildResumenHeader()
        {
            var bg = new Border { Background = Br("#1A5276"), Padding = new Thickness(0, 8, 0, 8) };
            var g  = MakeResumenGrid();

            void H(string txt, int col, TextAlignment ta = TextAlignment.Left)
            {
                var tb = new TextBlock { Text = txt, FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = Br("#FFFFFF"), TextAlignment = ta,
                    Margin = new Thickness(col == 0 ? 12 : 6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tb, col); g.Children.Add(tb);
            }

            H("LOCAL",        0);
            H("VENDEDOR",     1);
            H("Nº SOLICITUD", 2);
            H("TOTAL",        3, TextAlignment.Right);
            H("ENTREGA",      4, TextAlignment.Right);
            H("DEBE",         5, TextAlignment.Right);
            H("HABER",        6, TextAlignment.Right);
            H("SALDO",        7, TextAlignment.Right);
            bg.Child = g;
            return bg;
        }

        static UIElement BuildResumenFila(string local, string vendedor, string solicitud,
            decimal total, decimal entrega, decimal debe, decimal haber, decimal saldo, bool oscuro)
        {
            var bg = new Border
            {
                Background = Br(oscuro ? "#F4F8FB" : "#FFFFFF"),
                BorderBrush = Br("#EBF5FB"), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 6, 0, 6)
            };
            var g = MakeResumenGrid();

            void C(string txt, int col, bool bold = false, bool rojo = false, TextAlignment ta = TextAlignment.Left)
            {
                var tb = new TextBlock
                {
                    Text = txt, FontSize = 11, TextAlignment = ta,
                    FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = Br(rojo && saldo > 0 ? "#C0392B" : "#2C3E50"),
                    Margin = new Thickness(col == 0 ? 12 : 6, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(tb, col); g.Children.Add(tb);
            }

            // Acortar número de solicitud (quitar ceros a la izquierda)
            var solDisplay = solicitud.TrimStart('0');
            if (solDisplay.Length == 0 || solicitud.Replace("x","").Trim().Length == 0)
                solDisplay = "—";
            else
                solDisplay = $"#{solDisplay}";

            C(local,                  0, bold: true);
            C(vendedor,               1);
            C(solDisplay,             2);
            C($"Gs. {total:N0}",      3, ta: TextAlignment.Right);
            C($"Gs. {entrega:N0}",    4, ta: TextAlignment.Right);
            C(debe   > 0 ? $"Gs. {debe:N0}"  : "—", 5, ta: TextAlignment.Right);
            C(haber  > 0 ? $"Gs. {haber:N0}" : "—", 6, ta: TextAlignment.Right);
            C(saldo  > 0 ? $"Gs. {saldo:N0}" : "—", 7, rojo: true, ta: TextAlignment.Right);
            bg.Child = g;
            return bg;
        }

        static UIElement BuildResumenGrupoDivider()
        {
            return new Border
            {
                Height = 4, Background = Br("#D6EAF8"),
                Margin = new Thickness(0, 2, 0, 2)
            };
        }


        // Paginación real — renderiza la página actual y construye el paginador
        async Task RenderCurrentPage()
        {
            var panel = _enResumen ? _resumenPanel : _detallePanel;
            panel.Children.Clear();

            if (_rawRows.Count == 0) return;

            int totalPages = _pageSize > 0 ? (int)Math.Ceiling(_rawRows.Count / (double)_pageSize) : 1;
            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            var page = _pageSize > 0
                ? _rawRows.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList()
                : _rawRows;

            // Cargar artículos de los ids de esta página que no estén en caché
            var missingIds = page.Select(r => Convert.ToInt32(r["IDCAB"]))
                                 .Where(id => !_artsCache.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                _overlay.Visibility = Visibility.Visible;
                try
                {
                    using var conn = _db.Create();
                    foreach (var g in (await conn.QueryAsync<dynamic>(SqlArts, new { Ids = missingIds }))
                                      .GroupBy(a => (int)((IDictionary<string,object>)a)["IDCAB"]))
                        _artsCache[g.Key] = g.ToList();
                }
                finally { _overlay.Visibility = Visibility.Collapsed; }
            }

            foreach (var r in page)
            {
                var idcab = Convert.ToInt32(r["IDCAB"]);
                panel.Children.Add(BuildCard(r, _artsCache.TryGetValue(idcab, out var al) ? al : new List<dynamic>()));
            }

            // Totales fijos arriba (no en el scroll)
            decimal sumTotal = 0, sumEntrega = 0, sumDebe = 0, sumHaber = 0;
            foreach (var r in _rawRows)
            {
                if (r["TOTAL"]         != null) sumTotal   += Convert.ToDecimal(r["TOTAL"]);
                if (r["ENTREGANORMAL"] != null) sumEntrega += Convert.ToDecimal(r["ENTREGANORMAL"]);
                if (r["DEBE"]          != null) sumDebe    += Convert.ToDecimal(r["DEBE"]);
                if (r["HABER"]         != null) sumHaber   += Convert.ToDecimal(r["HABER"]);
            }
            var sumSaldo = sumDebe - sumHaber;
            _totEnt.Text   = $"Gs. {sumTotal:N0}";    // TOTAL VENDIDO
            _totLog.Text   = $"Gs. {sumEntrega:N0}";  // COBRADO INICIAL
            _totDebe.Text  = $"Gs. {sumSaldo:N0}";    // SALDO PENDIENTE
            _totHaber.Text = $"Gs. {sumHaber:N0}";    // TOTAL COBRADO
            _totalesBar.Visibility = Visibility.Visible;

            // Info
            int desde = _pageSize > 0 ? (_currentPage - 1) * _pageSize + 1 : 1;
            int hasta = _pageSize > 0 ? Math.Min(_currentPage * _pageSize, _rawRows.Count) : _rawRows.Count;
            _lblInfo.Text = $"Mostrando {desde}–{hasta} de {_rawRows.Count} venta(s)";

            // Construir paginador
            BuildPager(totalPages);

            // Scroll al tope
            var scroll = _enResumen ? _resumenScroll : _detalleScroll;
            scroll.ScrollToTop();
        }

        void BuildPager(int totalPages)
        {
            _pagerPanel.Children.Clear();
            if (_pageSize == 0) return;

            // Usa Border en lugar de Button para control total del estilo
            UIElement PagerItem(string label, int targetPage, bool active, bool enabled)
            {
                if (label == "…")
                    return new TextBlock
                    {
                        Text = "…", FontSize = 12, Foreground = Br("#90A4AE"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0)
                    };

                var bg     = active   ? "#1A5276" : "#FFFFFF";
                var fg     = active   ? "#FFFFFF" : enabled ? "#1A5276" : "#BDBDBD";
                var border = new Border
                {
                    Width = 32, Height = 32,
                    Background      = Br(bg),
                    BorderBrush     = Br(active ? "#1A5276" : enabled ? "#AED6F1" : "#E0E0E0"),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Margin          = new Thickness(2, 0, 2, 0),
                    Cursor          = enabled && !active ? Cursors.Hand : Cursors.Arrow,
                    Child = new TextBlock
                    {
                        Text = label, FontSize = 11,
                        FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = Br(fg),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center
                    }
                };
                if (enabled && !active)
                {
                    border.MouseEnter += (_, _) => { border.Background = Br("#EBF5FB"); border.BorderBrush = Br("#1A5276"); };
                    border.MouseLeave += (_, _) => { border.Background = Br("#FFFFFF"); border.BorderBrush = Br("#AED6F1"); };
                    border.MouseLeftButtonUp += async (_, _) => { _currentPage = targetPage; await RenderCurrentPage(); };
                }
                return border;
            }

            // ‹ anterior
            _pagerPanel.Children.Add(PagerItem("‹", _currentPage - 1, false, _currentPage > 1));

            if (totalPages > 1)
            {
                int start = Math.Max(1, _currentPage - 2);
                int end   = Math.Min(totalPages, start + 4);
                start     = Math.Max(1, end - 4);

                if (start > 1)
                {
                    _pagerPanel.Children.Add(PagerItem("1", 1, false, true));
                    if (start > 2) _pagerPanel.Children.Add(PagerItem("…", 0, false, false));
                }
                for (int p = start; p <= end; p++)
                    _pagerPanel.Children.Add(PagerItem(p.ToString(), p, p == _currentPage, p != _currentPage));
                if (end < totalPages)
                {
                    if (end < totalPages - 1) _pagerPanel.Children.Add(PagerItem("…", 0, false, false));
                    _pagerPanel.Children.Add(PagerItem(totalPages.ToString(), totalPages, false, true));
                }
            }
            else
            {
                _pagerPanel.Children.Add(PagerItem("1", 1, true, false));
            }

            // › siguiente
            _pagerPanel.Children.Add(PagerItem("›", _currentPage + 1, false, _currentPage < totalPages));
        }

        static Border BuildTotalesBlock(decimal ent, decimal entLog, decimal debe, decimal haber)
        {
            var outer = new Border
            {
                BorderBrush = Br("#1A5276"), BorderThickness = new Thickness(0, 0, 0, 2),
                Margin = new Thickness(12, 8, 12, 16), Background = Br("#FFFFFF"),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = System.Windows.Media.Colors.Black, Opacity = 0.07, BlurRadius = 6, ShadowDepth = 1 }
            };
            var stack = new StackPanel();

            var hdrBar = new Border { Background = Br("#1A5276"), Padding = new Thickness(14, 7, 14, 7) };
            hdrBar.Child = new TextBlock { Text = "RESUMEN TOTAL", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br("#FFFFFF") };
            stack.Children.Add(hdrBar);

            var dataGrid = new Grid { Margin = new Thickness(14, 10, 14, 12) };
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            void TotCell(string lbl, string val, int col, bool red = false)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
                sp.Children.Add(new TextBlock { Text = lbl, FontSize = 9, Foreground = Br("#90A4AE"), FontWeight = FontWeights.SemiBold });
                sp.Children.Add(new TextBlock { Text = $"Gs. {val}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Br(red ? "#C0392B" : "#263238") });
                Grid.SetColumn(sp, col); dataGrid.Children.Add(sp);
            }
            TotCell("ENTREGA",          $"{ent:N0}",    0);
            TotCell("ENTREGA LOG.",     $"{entLog:N0}", 1);
            TotCell("DEBE",             $"{debe:N0}",   2, true);
            TotCell("HABER",            $"{haber:N0}",  3);

            stack.Children.Add(dataGrid);
            outer.Child = stack;
            return outer;
        }

        static Border BuildCard(IDictionary<string,object> r, List<dynamic> arts)
        {
            var estado   = r["ESTADO_TXT"]?.ToString() ?? "—";
            var clienteRaw = r["CLIENTE"]?.ToString() ?? "—";
            var esGenerico = clienteRaw.Trim().ToUpper() == "XXX";
            var cliente  = esGenerico ? "CONSUMIDOR FINAL" : clienteRaw;
            var tipo     = r["TIPO_VENTA"]?.ToString() ?? "—";
            var anulado = estado == "Cerrada";

            var total   = r["TOTAL"]            != null ? Convert.ToDecimal(r["TOTAL"])            : 0m;
            var entrega = r["ENTREGANORMAL"]    != null ? Convert.ToDecimal(r["ENTREGANORMAL"])    : 0m;
            var entLog  = r["ENTREGALOGISTICA"] != null ? Convert.ToDecimal(r["ENTREGALOGISTICA"]) : 0m;
            var cuotas  = r["CUOTAS"]           != null ? Convert.ToInt32(r["CUOTAS"])             : 0;
            var mCuota  = r["MONTOCUOTA"]       != null ? Convert.ToDecimal(r["MONTOCUOTA"])       : 0m;
            var debe    = r["DEBE"]             != null ? Convert.ToDecimal(r["DEBE"])             : 0m;
            var haber   = r["HABER"]            != null ? Convert.ToDecimal(r["HABER"])            : 0m;
            var nsol    = r["NSOLICITUD"]?.ToString() ?? "—";
            var fecha   = r["FECHA"] is DateTime dt  ? dt.ToString("dd/MM/yyyy") : "—";
            var hora    = r["FECHA"] is DateTime dt2 ? dt2.ToString("HH:mm")     : "";
            var vendedor= r["VENDEDOR"]?.ToString() ?? "—";
            var local   = r["LOCAL"]?.ToString()    ?? "—";
            var cuotaPagada = mCuota > 0 ? (int)Math.Floor(haber / mCuota) : 0;

            var estadoFg = anulado ? "#888888" : "#1A5276";
            var estadoBg = anulado ? "#F5F5F5" : "#D6EAF8";
            var tipFg    = "#C0392B";
            var tipBg    = "#FADBD8";

            // ── ACORDEÓN: contenedor raíz ────────────────────────────────────
            var outer = new Border
            {
                Background      = Br("#FFFFFF"),
                BorderBrush     = Br("#D6EAF8"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin          = new Thickness(0)
            };
            var outerStack = new StackPanel();

            // ── FILA DE ENCABEZADO (siempre visible) — clickeable ────────────
            var headerRow = new Border
            {
                Background  = Br("#FFFFFF"),
                Padding     = new Thickness(0),
                Cursor      = Cursors.Hand
            };
            // Hover sutil
            headerRow.MouseEnter += (s, e) => headerRow.Background = Br("#F9FAFB");
            headerRow.MouseLeave += (s, e) => headerRow.Background = Br("#FFFFFF");

            var hg = new Grid { Margin = new Thickness(16, 11, 16, 11) };
            // col 0: chevron  col 1: cliente+meta  col 2: badges  col 3: total
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

            // Chevron (▶ / ▼)
            var chevron = new TextBlock
            {
                Text = "▶", FontSize = 9,
                Foreground = Br("#B0BEC5"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(chevron, 0); hg.Children.Add(chevron);

            // Nombre + metadatos
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text         = cliente.ToUpper(),
                FontSize     = 12,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = Br(anulado ? "#9E9E9E" : "#1C2B33"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var metaLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            void MetaItem(string txt, bool dot = false)
            {
                if (dot) metaLine.Children.Add(new TextBlock { Text = "  ·  ", FontSize = 9, Foreground = Br("#CFD8DC"), VerticalAlignment = VerticalAlignment.Center });
                metaLine.Children.Add(new TextBlock { Text = txt, FontSize = 9, Foreground = Br("#90A4AE"), VerticalAlignment = VerticalAlignment.Center });
            }
            MetaItem($"N° {nsol}");
            MetaItem($"{fecha}  {hora}".Trim(), true);
            MetaItem(local, true);
            MetaItem(vendedor, true);
            nameStack.Children.Add(metaLine);
            Grid.SetColumn(nameStack, 1); hg.Children.Add(nameStack);

            // Badges tipo + estado (+ genérico si aplica)
            var badgePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            if (esGenerico)
                badgePanel.Children.Add(new Border
                {
                    Background = Br("#F5EEF8"), CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 5, 0),
                    ToolTip = "Venta realizada sin identificar al comprador",
                    Child = new TextBlock { Text = "Consumidor Final", FontSize = 9, Foreground = Br("#7D3C98"), FontWeight = FontWeights.SemiBold }
                });
            var solicitud = r["NSOLICITUD"]?.ToString() ?? "";
            var esMigrado = esGenerico && solicitud.Replace("x","").Trim().Length == 0;

            string tipoTooltip = tipo switch
            {
                "Contado"  => "Venta al contado: el cliente paga el total en el momento (puede quedar saldo si pagó en cuotas)",
                "Crédito"  => esMigrado
                              ? "Venta registrada en el sistema anterior — el tipo puede no ser exacto"
                              : "Venta a crédito: el cliente paga en cuotas según el plan acordado",
                _          => tipo
            };

            badgePanel.Children.Add(new Border
            {
                Background = Br(tipBg), CornerRadius = new CornerRadius(2),
                Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 5, 0),
                ToolTip = tipoTooltip,
                Child = new TextBlock { Text = tipo, FontSize = 9, Foreground = Br(tipFg), FontWeight = FontWeights.SemiBold }
            });

            var saldoReal = debe - haber;
            string estadoTooltip = estado == "Pendiente"
                ? $"Saldo pendiente de cobro: Gs. {saldoReal:N0}\n(Total: Gs. {total:N0} — Ya cobrado: Gs. {haber:N0})"
                : $"Venta cobrada en su totalidad\n(Total: Gs. {total:N0})";

            badgePanel.Children.Add(new Border
            {
                Background = Br(estadoBg), CornerRadius = new CornerRadius(2),
                Padding = new Thickness(7, 2, 7, 2),
                ToolTip = estadoTooltip,
                Child = new TextBlock { Text = estado, FontSize = 9, Foreground = Br(estadoFg), FontWeight = FontWeights.SemiBold }
            });
            Grid.SetColumn(badgePanel, 2); hg.Children.Add(badgePanel);

            // Total
            var totalTb = new TextBlock
            {
                Text                = $"Gs. {total:N0}",
                FontSize            = 13,
                FontWeight          = FontWeights.Bold,
                Foreground          = Br(anulado ? "#BDBDBD" : "#1C2B33"),
                TextAlignment       = TextAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetColumn(totalTb, 3); hg.Children.Add(totalTb);

            headerRow.Child = hg;
            outerStack.Children.Add(headerRow);

            // ── PANEL EXPANDIDO (oculto por defecto) ─────────────────────────
            var expandPanel = new StackPanel { Visibility = Visibility.Collapsed };

            // Separador superior del panel expandido
            expandPanel.Children.Add(new Border { Height = 1, Background = Br("#ECEFF1") });

            // Métricas en fila
            int mcols = (tipo == "Crédito") ? 6 : 4;
            var metGrid = new Grid { Margin = new Thickness(56, 10, 16, 10), Background = Br("#FAFAFA") };
            metGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < mcols; i++)
                metGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void Metric(string lbl, string val, int col, bool red = false)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
                sp.Children.Add(new TextBlock { Text = lbl, FontSize = 8, Foreground = Br("#90A4AE"), FontWeight = FontWeights.SemiBold });
                sp.Children.Add(new TextBlock { Text = val, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Br(red ? "#C0392B" : "#1A5276") });
                Grid.SetColumn(sp, col); metGrid.Children.Add(sp);
            }
            Metric("ENTREGA",      $"Gs. {entrega:N0}", 0);
            Metric("ENTREGA LOG.", $"Gs. {entLog:N0}",  1);
            Metric("DEBE",         $"Gs. {debe:N0}",    2, debe > 0);
            Metric("HABER",        $"Gs. {haber:N0}",   3);
            if (tipo == "Crédito")
            {
                Metric("CUOTAS",   $"{cuotaPagada} / {cuotas}", 4);
                Metric("M. CUOTA", $"Gs. {mCuota:N0}",          5);
            }
            expandPanel.Children.Add(metGrid);

            // Artículos
            if (arts.Count > 0)
            {
                expandPanel.Children.Add(new Border { Height = 1, Background = Br("#ECEFF1") });

                var artContainer = new Grid { Margin = new Thickness(56, 0, 16, 10) };
                artContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                artContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                artContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                artContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                int ri = 0;

                // Cabecera de tabla
                artContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                void ArtHdrCell(string t, int c, TextAlignment al = TextAlignment.Left)
                {
                    var tb = new TextBlock { Text = t, FontSize = 8, Foreground = Br("#90A4AE"), FontWeight = FontWeights.SemiBold, TextAlignment = al, Padding = new Thickness(0, 6, 0, 4) };
                    Grid.SetRow(tb, ri); Grid.SetColumn(tb, c); artContainer.Children.Add(tb);
                }
                ArtHdrCell("ARTÍCULO", 0);
                ArtHdrCell("CANT.", 1, TextAlignment.Center);
                ArtHdrCell("PRECIO", 2, TextAlignment.Right);
                ArtHdrCell("SUBTOTAL", 3, TextAlignment.Right);
                ri++;

                artContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
                var hline = new Border { Height = 1, Background = Br("#ECEFF1") };
                Grid.SetRow(hline, ri); Grid.SetColumnSpan(hline, 4); artContainer.Children.Add(hline);
                ri++;

                foreach (IDictionary<string,object> a in arts)
                {
                    var artNom = a["ARTICULO"]?.ToString() ?? "—";
                    var cant   = a["CANTIDAD"] != null ? Convert.ToInt32(a["CANTIDAD"])   : 0;
                    var prec   = a["PRECIO"]   != null ? Convert.ToDecimal(a["PRECIO"])   : 0m;
                    var sub    = a["SUBTOTAL"] != null ? Convert.ToDecimal(a["SUBTOTAL"]) : 0m;

                    artContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Fondo zebra
                    var rowBg = new Border { Background = Br(ri % 2 == 0 ? "#FAFAFA" : "#FFFFFF") };
                    Grid.SetRow(rowBg, ri); Grid.SetColumnSpan(rowBg, 4); artContainer.Children.Add(rowBg);

                    void ArtCell(string t, int c, TextAlignment al2 = TextAlignment.Left, bool bold = false)
                    {
                        var tb = new TextBlock
                        {
                            Text = t, FontSize = 11, TextAlignment = al2,
                            FontWeight   = bold ? FontWeights.SemiBold : FontWeights.Normal,
                            Foreground   = Br(bold ? "#263238" : "#607D8B"),
                            Padding      = new Thickness(0, 5, 0, 5),
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        Grid.SetRow(tb, ri); Grid.SetColumn(tb, c); artContainer.Children.Add(tb);
                    }
                    ArtCell(artNom, 0, TextAlignment.Left, true);
                    ArtCell($"{cant}", 1, TextAlignment.Center);
                    ArtCell($"Gs. {prec:N0}", 2, TextAlignment.Right);
                    ArtCell($"Gs. {sub:N0}", 3, TextAlignment.Right);
                    ri++;
                }
                expandPanel.Children.Add(artContainer);
            }

            outerStack.Children.Add(expandPanel);
            outer.Child = outerStack;

            // ── TOGGLE al hacer clic en el header ────────────────────────────
            bool expanded = false;
            headerRow.MouseLeftButtonUp += (s, e) =>
            {
                expanded = !expanded;
                expandPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                chevron.Text           = expanded ? "▼" : "▶";
                outer.BorderThickness  = expanded
                    ? new Thickness(3, 0, 0, 0)
                    : new Thickness(0, 0, 0, 1);
                outer.BorderBrush      = expanded ? Br("#1A5276") : Br("#D6EAF8");
            };

            return outer;
        }

        private async void OnSeleccionarLocal(object sender, RoutedEventArgs e)
        {
            var dlg = new CrediSoft.UI.Views.Compras.BuscadorLocalModal(_db) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.LocalSeleccionado != null)
            {
                _idLocalFiltro = dlg.LocalSeleccionado.IdLocal;
                _txtLocal.Text = dlg.LocalSeleccionado.Nombre;
                _txtLocal.FontStyle = FontStyles.Normal; _txtLocal.Foreground = Br("#212529");
                _btnQuitarLocal.Visibility = Visibility.Visible;
                await Buscar();
                if (_enResumen) await BuscarResumen();
            }
        }

        private async void OnSeleccionarVendedor(object sender, RoutedEventArgs e)
        {
            var dlg = new BuscadorVendedorModal(_db) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.UsuarioSeleccionado != null)
            {
                _idUsuarioFiltro    = dlg.UsuarioSeleccionado.IdUsuario;
                _fechaIngresoFiltro = dlg.UsuarioSeleccionado.FechaIngreso;
                _txtVendedor.Text   = dlg.UsuarioSeleccionado.NombreUsuario;
                _txtVendedor.FontStyle = FontStyles.Normal; _txtVendedor.Foreground = Br("#212529");
                _btnQuitarVendedor.Visibility = Visibility.Visible;
                await Buscar();
                if (_enResumen) await BuscarResumen();
            }
        }
    }


    // ── Modal selector de vendedor (BD — usado por HCobranzasWindow) ────────────
    internal sealed class BuscadorVendedorModal : Window
    {
        private readonly IDbConnectionFactory _db;
        private DataGrid _grid      = null!;
        private TextBox  _txtBuscar = null!;
        private List<UsuarioItem> _todos = new();

        public UsuarioItem? UsuarioSeleccionado { get; private set; }

        public BuscadorVendedorModal(IDbConnectionFactory db)
        {
            _db = db;
            Title  = "Seleccionar Vendedor";
            Width  = 480; Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            BuildUI();
            Loaded += async (_, _) => await Cargar();
        }

        private void BuildUI()
        {
            var root = new DockPanel();
            var top  = new DockPanel { Margin = new Thickness(8) };
            DockPanel.SetDock(top, Dock.Top);
            _txtBuscar = new TextBox { Padding = new Thickness(4, 2, 4, 2) };
            _txtBuscar.TextChanged += (_, _) => Filtrar();
            var lbl = new TextBlock { Text = "Buscar: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            DockPanel.SetDock(lbl, Dock.Left);
            top.Children.Add(lbl); top.Children.Add(_txtBuscar);
            root.Children.Add(top);
            var bot = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
            DockPanel.SetDock(bot, Dock.Bottom);
            var btnSel = new Button { Content = "Seleccionar", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            btnSel.Click += (_, _) => Seleccionar();
            var btnCan = new Button { Content = "Cancelar", Padding = new Thickness(10, 4, 10, 4), IsCancel = true };
            btnCan.Click += (_, _) => DialogResult = false;
            bot.Children.Add(btnSel); bot.Children.Add(btnCan);
            root.Children.Add(bot);
            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
            _grid.Columns.Add(new DataGridTextColumn { Header = "ID",     Binding = new System.Windows.Data.Binding("IdUsuario"),     Width = 50 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("NombreUsuario"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("CodigoUsuario"), Width = 80 });
            _grid.MouseDoubleClick += (_, _) => Seleccionar();
            root.Children.Add(_grid);
            Content = root;
            KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false; };
        }

        private async Task Cargar()
        {
            using var conn = _db.Create();
            _todos = (await conn.QueryAsync<UsuarioItem>(
                "SELECT ID_USUARIO AS IdUsuario, NOMBRE_USUARIO AS NombreUsuario, CODIGO_USUARIO AS CodigoUsuario, FECHA_DE_INGRESO AS FechaIngreso FROM USUARIOS ORDER BY NOMBRE_USUARIO"))
                .ToList();
            _grid.ItemsSource = _todos;
        }

        private void Filtrar()
        {
            var q = _txtBuscar.Text.Trim();
            _grid.ItemsSource = string.IsNullOrEmpty(q) ? _todos
                : _todos.Where(u => u.NombreUsuario.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || u.CodigoUsuario.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void Seleccionar()
        {
            if (_grid.SelectedItem is UsuarioItem u) { UsuarioSeleccionado = u; DialogResult = true; }
        }
    }

    // ── Modal selector de vendedor (desde lista en memoria — solo los del resultado actual) ──
    internal sealed class BuscadorVendedorAtrasoModal : Window
    {
        private DataGrid _grid      = null!;
        private TextBox  _txtBuscar = null!;
        private List<string> _todos = new();

        public string? VendedorSeleccionado { get; private set; }

        public BuscadorVendedorAtrasoModal(IEnumerable<string> vendedores)
        {
            _todos = vendedores.OrderBy(v => v).ToList();
            Title  = $"Seleccionar Vendedor  ({_todos.Count})";
            Width  = 420; Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(240, 242, 245));
            BuildUI();
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var hdr = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(26, 35, 126)),  // #1A237E azul marino
                Padding = new Thickness(12, 10, 12, 10)
            };
            var hdrSp = new StackPanel { Orientation = Orientation.Horizontal };
            hdrSp.Children.Add(new TextBlock
            {
                Text = "Nombre:", Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            _txtBuscar = new TextBox
            {
                Width = 240, Height = 28, FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = System.Windows.Media.Brushes.White,
            };
            _txtBuscar.TextChanged += (_, _) => Filtrar();
            hdrSp.Children.Add(_txtBuscar);
            hdr.Child = hdrSp;
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Grid
            _grid = new DataGrid
            {
                AutoGenerateColumns      = false,
                IsReadOnly               = true,
                SelectionMode            = DataGridSelectionMode.Single,
                GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(229, 231, 235)),
                RowBackground            = System.Windows.Media.Brushes.White,
                AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(249, 250, 251)),
                FontSize  = 12,
                Margin    = new Thickness(8, 6, 8, 0),
                ColumnHeaderStyle = BuildHdrStyle(),
            };
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header  = "Vendedor",
                Binding = new System.Windows.Data.Binding("."),
                Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
            _grid.MouseDoubleClick += (_, _) => Seleccionar();
            _grid.ItemsSource = _todos;
            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            // Footer
            var footer = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8)
            };
            var footSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnSel = new Button
            {
                Content = "Seleccionar", Height = 32, Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            };
            var btnCan = new Button
            {
                Content = "Cerrar", Height = 32, Padding = new Thickness(16, 0, 16, 0),
                FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(107, 114, 128)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            };
            btnSel.Click += (_, _) => Seleccionar();
            btnCan.Click += (_, _) => DialogResult = false;
            footSp.Children.Add(btnSel);
            footSp.Children.Add(btnCan);
            footer.Child = footSp;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
            Loaded += (_, _) => _txtBuscar.Focus();
            KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false; };
        }

        private static Style BuildHdrStyle()
        {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 35, 126))));
            s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
                System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty,
                FontWeights.Bold));
            s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty,
                new Thickness(10, 0, 10, 0)));
            s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty,
                new Thickness(0)));
            return s;
        }

        private void Filtrar()
        {
            var q = _txtBuscar.Text.Trim();
            _grid.ItemsSource = string.IsNullOrEmpty(q)
                ? _todos
                : _todos.Where(v => v.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void Seleccionar()
        {
            if (_grid.SelectedItem is string nombre)
            {
                VendedorSeleccionado = nombre;
                DialogResult = true;
            }
        }
    }

    internal sealed class UsuarioItem
    {
        public int       IdUsuario     { get; set; }
        public string    NombreUsuario { get; set; } = "";
        public string    CodigoUsuario { get; set; } = "";
        public DateTime? FechaIngreso  { get; set; }
    }

    // ── H. NOTA DE CRÉDITO ────────────────────────────────────────────────────
    public class HNotaCreditoWindow : Window
    {
        private readonly IDbConnectionFactory _db;
        private readonly IClienteRepository   _clienteRepo;

        private DataGrid   _grid         = null!;
        private DatePicker _dpDesde      = null!, _dpHasta = null!;
        private TextBox    _txtLocal     = null!, _txtCliente = null!, _txtTipo = null!;
        private Button     _btnQuitarLocal = null!, _btnQuitarCliente = null!, _btnQuitarTipo = null!;
        private TextBlock  _lblInfo      = null!;
        private TextBlock  _kpiMonto     = null!, _kpiCount = null!, _kpiClientes = null!;
        private int?       _idLocalFiltro   = null;
        private int?       _idClienteFiltro = null;
        private string?    _tipoFiltro      = null;

        private static System.Windows.Media.SolidColorBrush NBr(string hex) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        public HNotaCreditoWindow()
        {
            _db          = App.Services.GetRequiredService<IDbConnectionFactory>();
            _clienteRepo = App.Services.GetRequiredService<IClienteRepository>();
            Title    = "Cobros por Nota de Crédito — ElectroMar";
            Width    = 1020; Height = 650;
            MinWidth = 860;  MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = NBr("#F4F6F9");
            BuildUI();
            Loaded += async (_, _) => await Buscar();
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.F5) _ = Buscar(); };
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // kpi
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grilla
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // pie

            // ── HEADER ──────────────────────────────────────────────────────
            var hdr = new Border { Background = NBr("#0E2F44"), Padding = new Thickness(20, 14, 20, 14) };
            var hdrSp = new StackPanel();
            hdrSp.Children.Add(new TextBlock { Text = "COBROS POR NOTA DE CRÉDITO",
                FontSize = 18, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
            hdrSp.Children.Add(new TextBlock { Text = "Historial de pagos registrados mediante nota de crédito",
                FontSize = 11, Foreground = NBr("#7FB3D3"), Margin = new Thickness(0,2,0,0) });
            hdr.Child = hdrSp;
            Grid.SetRow(hdr, 0); root.Children.Add(hdr);

            // ── KPI BAR ──────────────────────────────────────────────────────
            var kpiPanel = new Border { Background = NBr("#F8F9FA"), BorderBrush = NBr("#DEE2E6"),
                BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(12,10,12,10) };
            var kpiRow = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 };

            Border MakeKpi(string titulo, string color, out TextBlock val)
            {
                var b = new Border { Background = System.Windows.Media.Brushes.White,
                    BorderBrush = NBr(color), BorderThickness = new Thickness(0,3,0,0),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(16,10,16,10),
                    Margin = new Thickness(4,0,4,0) };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = titulo, FontSize = 9, FontWeight = FontWeights.Bold,
                    Foreground = NBr("#6C757D") });
                val = new TextBlock { Text = "—", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = NBr(color) };
                sp.Children.Add(val);
                b.Child = sp; return b;
            }
            kpiRow.Children.Add(MakeKpi("TOTAL COBRADO (Gs.)", "#0E2F44", out _kpiMonto));
            kpiRow.Children.Add(MakeKpi("COBROS",              "#2980B9", out _kpiCount));
            kpiRow.Children.Add(MakeKpi("CLIENTES ÚNICOS",     "#27AE60", out _kpiClientes));
            kpiPanel.Child = kpiRow;
            Grid.SetRow(kpiPanel, 1); root.Children.Add(kpiPanel);

            // ── FILTROS ──────────────────────────────────────────────────────
            var fBorder = new Border { Background = System.Windows.Media.Brushes.White,
                BorderBrush = NBr("#DEE2E6"), BorderThickness = new Thickness(0,0,0,1),
                Padding = new Thickness(16, 10, 16, 10) };
            var fp = new DockPanel { LastChildFill = false };

            TextBlock FLbl(string t) => new TextBlock { Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = NBr("#6C757D"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };

            // Fechas
            fp.Children.Add(FLbl("Desde:"));
            _dpDesde = new DatePicker { Width = 120, SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
            _dpDesde.SelectedDateChanged += async (_, _) => await Buscar();
            fp.Children.Add(_dpDesde);
            fp.Children.Add(FLbl("Hasta:"));
            _dpHasta = new DatePicker { Width = 120, SelectedDate = DateTime.Today,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
            _dpHasta.SelectedDateChanged += async (_, _) => await Buscar();
            fp.Children.Add(_dpHasta);

            var btnMes = new Button { Content = "📅 Este mes", Padding = new Thickness(10,5,10,5),
                Background = NBr("#6C757D"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(4,0,12,0) };
            btnMes.Click += (_, _) => {
                _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _dpHasta.SelectedDate = DateTime.Today;
            };
            fp.Children.Add(btnMes);

            // Separador
            fp.Children.Add(new Border { Width = 1, Background = NBr("#DEE2E6"), Margin = new Thickness(4,2,12,2) });

            // Local
            fp.Children.Add(FLbl("Local:"));
            _txtLocal = new TextBox { Width = 140, IsReadOnly = true, Cursor = Cursors.Arrow,
                Text = "Todos los locales", FontStyle = FontStyles.Italic, FontSize = 11,
                Foreground = NBr("#ADB5BD"), VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6,4,6,4), BorderBrush = NBr("#CED4DA"), Margin = new Thickness(0,0,3,0) };
            fp.Children.Add(_txtLocal);
            var btnSelLocal = new Button { Content = "Sel.", Padding = new Thickness(8,5,8,5),
                Background = NBr("#2980B9"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0,0,3,0) };
            btnSelLocal.Click += (_, _) => {
                var m = new BuscadorLocalModal(_db) { Owner = this };
                if (m.ShowDialog() == true && m.LocalSeleccionado != null) {
                    _idLocalFiltro = m.LocalSeleccionado.IdLocal;
                    _txtLocal.Text = m.LocalSeleccionado.Nombre;
                    _txtLocal.FontStyle = FontStyles.Normal; _txtLocal.Foreground = NBr("#212529");
                    _btnQuitarLocal.Visibility = Visibility.Visible;
                    _ = Buscar();
                }
            };
            fp.Children.Add(btnSelLocal);
            _btnQuitarLocal = new Button { Content = "✕ Todos", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8,5,8,5), Margin = new Thickness(0,0,10,0),
                Background = NBr("#E8F0F7"), Foreground = NBr("#0E2F44"), BorderBrush = NBr("#0E2F44"),
                BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed, Cursor = Cursors.Hand };
            _btnQuitarLocal.Click += async (_, _) => {
                _idLocalFiltro = null; _txtLocal.Text = "Todos los locales";
                _txtLocal.FontStyle = FontStyles.Italic; _txtLocal.Foreground = NBr("#ADB5BD");
                _btnQuitarLocal.Visibility = Visibility.Collapsed; await Buscar();
            };
            fp.Children.Add(_btnQuitarLocal);

            // Cliente
            fp.Children.Add(FLbl("Cliente:"));
            _txtCliente = new TextBox { Width = 160, IsReadOnly = true, Cursor = Cursors.Arrow,
                Text = "Todos los clientes", FontStyle = FontStyles.Italic, FontSize = 11,
                Foreground = NBr("#ADB5BD"), VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6,4,6,4), BorderBrush = NBr("#CED4DA"), Margin = new Thickness(0,0,3,0) };
            fp.Children.Add(_txtCliente);
            var btnSelCliente = new Button { Content = "Sel.", Padding = new Thickness(8,5,8,5),
                Background = NBr("#2980B9"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 11, Cursor = Cursors.Hand, Margin = new Thickness(0,0,3,0) };
            btnSelCliente.Click += (_, _) => {
                var m = new BuscadorClienteModal(_clienteRepo) { Owner = this };
                if (m.ShowDialog() == true && m.ClienteSeleccionado != null) {
                    _idClienteFiltro = m.ClienteSeleccionado.IdCliente;
                    _txtCliente.Text = m.ClienteSeleccionado.NombreCliente;
                    _txtCliente.FontStyle = FontStyles.Normal; _txtCliente.Foreground = NBr("#212529");
                    _btnQuitarCliente.Visibility = Visibility.Visible;
                    _ = Buscar();
                }
            };
            fp.Children.Add(btnSelCliente);
            _btnQuitarCliente = new Button { Content = "✕ Todos", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8,5,8,5), Margin = new Thickness(0,0,10,0),
                Background = NBr("#E8F0F7"), Foreground = NBr("#0E2F44"), BorderBrush = NBr("#0E2F44"),
                BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed, Cursor = Cursors.Hand };
            _btnQuitarCliente.Click += async (_, _) => {
                _idClienteFiltro = null; _txtCliente.Text = "Todos los clientes";
                _txtCliente.FontStyle = FontStyles.Italic; _txtCliente.Foreground = NBr("#ADB5BD");
                _btnQuitarCliente.Visibility = Visibility.Collapsed; await Buscar();
            };
            fp.Children.Add(_btnQuitarCliente);

            // Tipo (Cuota específica, Secuestro, etc)
            fp.Children.Add(FLbl("Tipo:"));
            var tipoItems = new[] { "Todos", "Cuota específica", "Secuestro" };
            var cmbTipo = new ComboBox { Width = 140, VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11, Margin = new Thickness(0,0,10,0) };
            foreach (var t in tipoItems) cmbTipo.Items.Add(t);
            cmbTipo.SelectedIndex = 0;
            cmbTipo.SelectionChanged += async (_, _) => {
                _tipoFiltro = cmbTipo.SelectedIndex == 0 ? null : cmbTipo.SelectedItem?.ToString();
                await Buscar();
            };
            fp.Children.Add(cmbTipo);

            // Buscar
            var btnBuscar = new Button { Content = "🔍  Buscar", Padding = new Thickness(14,6,14,6),
                Background = NBr("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand };
            btnBuscar.Click += async (_, _) => await Buscar();
            DockPanel.SetDock(btnBuscar, Dock.Right);
            fp.Children.Add(btnBuscar);

            fBorder.Child = fp;
            Grid.SetRow(fBorder, 2); root.Children.Add(fBorder);

            // ── GRILLA ──────────────────────────────────────────────────────
            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 34,
                CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = NBr("#F0F0F0"),
                RowBackground = System.Windows.Media.Brushes.White,
                AlternatingRowBackground = NBr("#FAFAFA"),
                Background = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 13,
                ColumnHeaderHeight = 36, FocusVisualStyle = null };
            _grid.Resources.Add(SystemColors.HighlightBrushKey,                      NBr("#E8F0F7"));
            _grid.Resources.Add(SystemColors.HighlightTextBrushKey,                  NBr("#212529"));
            _grid.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey,     NBr("#E8F0F7"));
            _grid.Resources.Add(SystemColors.InactiveSelectionHighlightTextBrushKey, NBr("#212529"));

            // estilo encabezados
            var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty,    NBr("#F8F9FA")));
            colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty,    NBr("#495057")));
            colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty,    FontWeights.Bold));
            colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty,      11.0));
            colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty,       new Thickness(10,0,10,0)));
            colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty,   NBr("#DEE2E6")));
            colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,1)));
            _grid.ColumnHeaderStyle = colHdrStyle;

            DataGridTextColumn GC(string h, string p, double w, string? fmt = null, bool star = false) {
                var c = new DataGridTextColumn { Header = h,
                    Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                          : new System.Windows.Data.Binding(p),
                    Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w) };
                c.HeaderStyle = colHdrStyle; return c;
            }
            DataGridTextColumn GCR(string h, string p, double w, string fmt) {
                var c = GC(h, p, w, fmt);
                c.ElementStyle = new Style(typeof(TextBlock)) { Setters = {
                    new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                    new Setter(TextBlock.FontWeightProperty,    FontWeights.SemiBold),
                    new Setter(FrameworkElement.MarginProperty, new Thickness(0,0,10,0)) } };
                return c;
            }

            _grid.Columns.Add(GC("Fecha",      "Fecha",      100, "dd/MM/yyyy"));
            _grid.Columns.Add(GC("N° Solicitud","Solicitud",  130));
            _grid.Columns.Add(GC("Comprobante", "Comprobante",130));
            _grid.Columns.Add(GC("Local",       "Local",       90));
            _grid.Columns.Add(GC("Tipo",        "Tipo",        120));
            _grid.Columns.Add(GC("N° Cuota",    "NCuota",      70));
            _grid.Columns.Add(GC("Vendedor",    "Vendedor",    130));
            _grid.Columns.Add(GC("Cliente",     "Cliente",     0, star: true));
            _grid.Columns.Add(GCR("Monto Gs.",  "Monto",      110, "N0"));

            _grid.MouseDoubleClick += OnDobleClick;

            var gridBorder = new Border { Background = System.Windows.Media.Brushes.White,
                BorderBrush = NBr("#DEE2E6"), BorderThickness = new Thickness(0),
                Child = _grid, Margin = new Thickness(0) };
            Grid.SetRow(gridBorder, 3); root.Children.Add(gridBorder);

            // ── PIE ─────────────────────────────────────────────────────────
            var pie = new Border { Background = NBr("#F8F9FA"), BorderBrush = NBr("#DEE2E6"),
                BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(16,8,16,8) };
            var pieDp = new DockPanel();
            var btnCerrar = new Button { Content = "✕  Cerrar", Padding = new Thickness(16,6,16,6),
                Background = NBr("#6C757D"), Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand };
            btnCerrar.Click += (_, _) => Close();
            DockPanel.SetDock(btnCerrar, Dock.Right);
            pieDp.Children.Add(btnCerrar);
            _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12, Foreground = NBr("#6C757D") };
            pieDp.Children.Add(_lblInfo);
            pie.Child = pieDp;
            Grid.SetRow(pie, 4); root.Children.Add(pie);

            Content = root;
        }

        private async Task Buscar()
        {
            if (_dpDesde == null) return;
            try
            {
                var desde = _dpDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var hasta = (_dpHasta.SelectedDate ?? DateTime.Today).AddDays(1);

                var where = new System.Text.StringBuilder(
                    "WHERE H.FECHA >= @Desde AND H.FECHA < @Hasta");
                if (_idLocalFiltro.HasValue)   where.Append(" AND H.ID_LOCAL = @IdLocal");
                if (_idClienteFiltro.HasValue) where.Append(" AND H.ID_CLIENTE = @IdCliente");
                if (_tipoFiltro != null)       where.Append(" AND H.TIPO = @Tipo");

                var sql = $@"
                    SELECT
                        H.IDHNC                                         AS IdHnc,
                        H.FECHA                                         AS Fecha,
                        H.NSOLICITUD                                    AS Solicitud,
                        H.NVENTACHAR                                    AS Comprobante,
                        H.TIPO                                          AS Tipo,
                        H.NCUOTA                                        AS NCuota,
                        L.NOMBRE                                        AS Local,
                        U.NOMBRE_USUARIO                                AS Vendedor,
                        CL.NOMBRE_CLIENTE                               AS Cliente,
                        H.MONTO                                         AS Monto,
                        H.OBS                                           AS Obs,
                        H.IDCAB                                         AS IdCab
                    FROM HISTORIAL_NOTA_CREDITO H
                    LEFT JOIN LOCALES  L  ON H.ID_LOCAL    = L.ID_LOCAL
                    LEFT JOIN USUARIOS U  ON H.ID_USUARIO  = U.ID_USUARIO
                    LEFT JOIN CLIENTES CL ON H.ID_CLIENTE  = CL.ID_CLIENTE
                    {where}
                    ORDER BY H.FECHA DESC";

                using var conn = _db.Create();
                var rows = (await conn.QueryAsync<dynamic>(sql, new {
                    Desde     = desde,
                    Hasta     = hasta,
                    IdLocal   = _idLocalFiltro   ?? 0,
                    IdCliente = _idClienteFiltro ?? 0,
                    Tipo      = _tipoFiltro ?? ""
                })).ToList();

                _grid.ItemsSource = rows;

                decimal sumMonto = 0;
                var clientesUnicos = new System.Collections.Generic.HashSet<string>();
                foreach (IDictionary<string,object> r in rows)
                {
                    if (r.TryGetValue("Monto",   out var m) && m != null) sumMonto += Convert.ToDecimal(m);
                    if (r.TryGetValue("Cliente", out var c) && c != null) clientesUnicos.Add(c.ToString()!);
                }

                _kpiMonto.Text    = $"Gs. {sumMonto:N0}";
                _kpiCount.Text    = $"{rows.Count}";
                _kpiClientes.Text = $"{clientesUnicos.Count}";
                _lblInfo.Text     = $"{rows.Count} registro(s) encontrado(s)  |  Doble clic para ver artículos";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OnDobleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_grid.SelectedItem is not IDictionary<string,object> row) return;
            // El SP BUSCAR_ARTICULOS_NOTACREDITO_CS espera el número del comprobante
            // convertido a entero (NVENTACHAR "000027199" -> 27199), no el IDCAB de la tabla
            if (!row.TryGetValue("Comprobante", out var compObj)) return;
            var compStr = compObj?.ToString()?.Trim() ?? "";
            if (!int.TryParse(compStr, out var idCabComp)) return;
            var cliente   = row.TryGetValue("Cliente",   out var c) ? c?.ToString() ?? "" : "";
            var solicitud = row.TryGetValue("Solicitud", out var s) ? s?.ToString() ?? "" : "";
            var dlg = new DetalleNotaCreditoModal(_db, idCabComp, cliente, solicitud) { Owner = this };
            dlg.ShowDialog();
        }
    }

    internal class DetalleNotaCreditoModal : Window
    {
        public DetalleNotaCreditoModal(IDbConnectionFactory db, int idCab, string cliente, string solicitud)
        {
            Title  = $"Artículos — {cliente}  |  Sol: {solicitud}";
            Width  = 600; Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdr = new Border { Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E2F44")),
                Padding = new Thickness(14, 10, 14, 10) };
            var hdrSp = new StackPanel();
            hdrSp.Children.Add(new TextBlock { Text = "Artículos de la Nota de Crédito",
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White });
            hdrSp.Children.Add(new TextBlock { Text = $"{cliente}  —  Sol: {solicitud}",
                FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7FB3D3")) });
            hdr.Child = hdrSp;
            Grid.SetRow(hdr, 0); root.Children.Add(hdr);

            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 32,
                BorderThickness = new Thickness(0), FontSize = 12,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(250,250,250)) };
            grid.Columns.Add(new DataGridTextColumn { Header = "Cantidad",
                Binding = new System.Windows.Data.Binding("CANTIDAD") { StringFormat = "N0" }, Width = 80 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Descripción",
                Binding = new System.Windows.Data.Binding("DESCRIPCION"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "P. Venta",
                Binding = new System.Windows.Data.Binding("PVENTA") { StringFormat = "N0" }, Width = 100 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Fecha",
                Binding = new System.Windows.Data.Binding("FECHA"), Width = 90 });
            Grid.SetRow(grid, 1); root.Children.Add(grid);

            var pie = new Border { Padding = new Thickness(10,8,10,8), BorderBrush =
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(222,226,230)),
                BorderThickness = new Thickness(0,1,0,0) };
            var btnCerrar = new Button { Content = "Cerrar", Padding = new Thickness(20,6,20,6),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108,117,125)),
                Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            btnCerrar.Click += (_, _) => Close();
            pie.Child = btnCerrar;
            Grid.SetRow(pie, 2); root.Children.Add(pie);

            Content = root;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            Loaded += async (_, _) =>
            {
                using var conn = db.Create();
                // El SP recibe IDCAB (de la venta original), no IDHNC
                var p = new DynamicParameters();
                p.Add("@Idcab", idCab);
                p.Add("@msg", dbType: System.Data.DbType.String,
                               direction: System.Data.ParameterDirection.Output, size: 12);
                var items = (await conn.QueryAsync<dynamic>(
                    "BUSCAR_ARTICULOS_NOTACREDITO_CS", p,
                    commandType: System.Data.CommandType.StoredProcedure)).ToList();
                grid.ItemsSource = items;
            };
        }
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  MOVIMIENTO DE ARTÍCULOS — rediseño v2
    // ══════════════════════════════════════════════════════════════════════════
    public class MovArtWindow : Window
    {
        private readonly IDbConnectionFactory _db;

        private DataGrid    _grid         = null!;
        private DatePicker  _dpDesde      = null!, _dpHasta = null!;
        private RadioButton _rbTodos      = null!, _rbPeriodo = null!;
        private Button      _btnMesActual = null!;
        private TextBox     _txtCodigo    = null!;
        private TextBox     _txtLocal     = null!;
        private Button      _btnQuitarLocal = null!;
        private int?        _idLocalFiltro  = null;
        // Filtro tipo movimiento
        private CheckBox    _chkEntrada = null!, _chkSalida = null!, _chkOtros = null!;
        private TextBlock   _lblConteo  = null!, _lblTotal  = null!;
        private List<FilaMovArt> _todos = new();

        private static System.Windows.Media.SolidColorBrush MBr(string hex) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        public MovArtWindow()
        {
            _db = App.Services.GetRequiredService<IDbConnectionFactory>();
            Title    = "Movimiento de Artículos";
            Width    = 1020; Height = 650;
            MinWidth = 860;  MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = MBr("#FAF9F8");
            BuildUI();
            Loaded += async (_, _) => await Buscar();
        }

        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros fila 1
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros fila 2 (tipo mov)
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grilla
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // totales

            // ── HEADER ──────────────────────────────────────────────────────
            var hdr = new Border { Background = MBr("#1A4F6E"), Padding = new Thickness(16, 10, 16, 10) };
            Grid.SetRow(hdr, 0);
            var hdrG = new Grid();
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrG.Children.Add(new TextBlock { Text = "📦  Movimiento de Artículos",
                Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });

            Button MkHBtn(string txt, string bg) => new Button {
                Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
                Background = MBr(bg), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) };
            var hdrBtns = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hdrBtns, 2);
            var bVista = MkHBtn("👁 Vista previa", "#6A1B9A");
            bVista.Click += (_, _) => ImprimirMovArt(preview: true);
            hdrBtns.Children.Add(bVista);
            var bImpr = MkHBtn("🖨 Imprimir", "#1B5E20");
            bImpr.Click += (_, _) => ImprimirMovArt(preview: false);
            hdrBtns.Children.Add(bImpr);
            hdrG.Children.Add(hdrBtns);

            _lblConteo = new TextBlock { Foreground = MBr("#7FB3D3"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(_lblConteo, 3); hdrG.Children.Add(_lblConteo);
            hdr.Child = hdrG; root.Children.Add(hdr);

            // helpers
            Button MkBtn(string t, string bg, int h = 30) => new Button {
                Content = t, Height = h, Padding = new Thickness(10, 0, 10, 0),
                Background = MBr(bg), Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            TextBlock Lbl(string t) => new TextBlock { Text = t,
                Foreground = MBr("#BBDEFB"), FontWeight = FontWeights.SemiBold, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            Border Sep() => new Border { Width = 1, Background = MBr("#2A7AB5"), Margin = new Thickness(10, 0, 10, 0) };

            // ── FILTROS FILA 1: período + local + código ─────────────────
            var f1Border = new Border { Background = MBr("#1A4F6E"), Padding = new Thickness(12, 8, 12, 8) };
            Grid.SetRow(f1Border, 1);
            var fp1 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _rbTodos   = new RadioButton { Content = "Todos",    GroupName = "MovPer",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center };
            _rbPeriodo = new RadioButton { Content = "Período:", GroupName = "MovPer", IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            _dpDesde = new DatePicker { Width = 112, SelectedDate = DateTime.Today.AddMonths(-1),
                VerticalAlignment = VerticalAlignment.Center };
            _dpHasta = new DatePicker { Width = 112, SelectedDate = DateTime.Today,
                VerticalAlignment = VerticalAlignment.Center };
            _btnMesActual = MkBtn("📅 Mes actual", "#1A4F6E");
            _btnMesActual.Margin = new Thickness(6, 0, 0, 0);

            _rbTodos.Checked   += async (_, _) => { _dpDesde.IsEnabled=false; _dpHasta.IsEnabled=false; _btnMesActual.IsEnabled=false; await Buscar(); };
            _rbPeriodo.Checked += async (_, _) => { _dpDesde.IsEnabled=true;  _dpHasta.IsEnabled=true;  _btnMesActual.IsEnabled=true;  await Buscar(); };
            _dpDesde.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _dpHasta.SelectedDateChanged += async (_, _) => { if (_rbPeriodo.IsChecked == true) await Buscar(); };
            _btnMesActual.Click += (_, _) => {
                _dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _dpHasta.SelectedDate = DateTime.Today;
            };

            var brdT = new Border { Background = MBr("#1A4F6E"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdT.Child = _rbTodos;
            var brdP = new Border { Background = MBr("#1A4F6E"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0) };
            brdP.Child = _rbPeriodo;

            fp1.Children.Add(brdT); fp1.Children.Add(brdP);
            fp1.Children.Add(_dpDesde); fp1.Children.Add(Lbl("  →")); fp1.Children.Add(_dpHasta);
            fp1.Children.Add(_btnMesActual);
            fp1.Children.Add(Sep());

            fp1.Children.Add(Lbl("🏪 Local:"));
            _txtLocal = new TextBox { Width = 170, Padding = new Thickness(5, 3, 5, 3),
                IsReadOnly = true, Cursor = Cursors.Arrow, FontStyle = FontStyles.Italic,
                Background = MBr("#E8F0F7"), Foreground = MBr("#1A4F6E"), FontSize = 11,
                Text = "(todos)", VerticalAlignment = VerticalAlignment.Center, BorderBrush = MBr("#2A7AB5") };
            var btnLocal = MkBtn("🏪 Selec.", "#1A4F6E");
            btnLocal.Margin = new Thickness(4, 0, 0, 0);
            btnLocal.Click += (_, _) => {
                var modal = new BuscadorLocalModal(_db) { Owner = this };
                if (modal.ShowDialog() == true && modal.LocalSeleccionado != null) {
                    _idLocalFiltro = modal.LocalSeleccionado.IdLocal;
                    _txtLocal.Text = modal.LocalSeleccionado.Nombre;
                    _txtLocal.FontStyle = FontStyles.Normal;
                    _btnQuitarLocal.Visibility = Visibility.Visible;
                    _ = Buscar();
                }
            };
            _btnQuitarLocal = MkBtn("✕", "#546E7A");
            _btnQuitarLocal.Margin = new Thickness(3, 0, 0, 0);
            _btnQuitarLocal.Visibility = Visibility.Collapsed;
            _btnQuitarLocal.Click += async (_, _) => {
                _idLocalFiltro = null; _txtLocal.Text = "(todos)";
                _txtLocal.FontStyle = FontStyles.Italic;
                _btnQuitarLocal.Visibility = Visibility.Collapsed;
                await Buscar();
            };
            fp1.Children.Add(_txtLocal); fp1.Children.Add(btnLocal); fp1.Children.Add(_btnQuitarLocal);
            fp1.Children.Add(Sep());

            fp1.Children.Add(Lbl("🔖 Código / Nombre:"));
            _txtCodigo = new TextBox { Width = 160, Padding = new Thickness(5, 3, 5, 3),
                VerticalAlignment = VerticalAlignment.Center };
            _txtCodigo.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Buscar(); };
            fp1.Children.Add(_txtCodigo);
            fp1.Children.Add(Sep());

            var btnBuscar = MkBtn("🔍 Buscar", "#1A4F6E", 32);
            btnBuscar.FontSize = 12; btnBuscar.Click += async (_, _) => await Buscar();
            var btnCerrar = MkBtn("✕ Cerrar", "#546E7A", 32);
            btnCerrar.Margin = new Thickness(6, 0, 0, 0); btnCerrar.Click += (_, _) => Close();
            fp1.Children.Add(btnBuscar); fp1.Children.Add(btnCerrar);

            f1Border.Child = fp1; root.Children.Add(f1Border);

            // ── FILTROS FILA 2: tipo de movimiento ───────────────────────
            var f2Border = new Border { Background = MBr("#0E2F44"), Padding = new Thickness(14, 5, 14, 5) };
            Grid.SetRow(f2Border, 2);

            var fp2 = new DockPanel { LastChildFill = false };

            // Etiqueta "Mostrar:"
            var lblMostrar = new TextBlock { Text = "Mostrar:",
                Foreground = MBr("#7FB3D3"), FontWeight = FontWeights.Bold, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            DockPanel.SetDock(lblMostrar, Dock.Left);
            fp2.Children.Add(lblMostrar);

            // Fábrica de checkbox uniforme
            CheckBox MkChk(string label) {
                var chk = new CheckBox {
                    IsChecked = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 20, 0) };
                chk.Content = new TextBlock {
                    Text = label,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center };
                return chk;
            }

            _chkEntrada = MkChk("Entradas");
            _chkSalida  = MkChk("Salidas");
            _chkOtros   = MkChk("Ajustes / Otros");

            _chkEntrada.Checked   += (_, _) => AplicarFiltroTipo();
            _chkEntrada.Unchecked += (_, _) => AplicarFiltroTipo();
            _chkSalida.Checked    += (_, _) => AplicarFiltroTipo();
            _chkSalida.Unchecked  += (_, _) => AplicarFiltroTipo();
            _chkOtros.Checked     += (_, _) => AplicarFiltroTipo();
            _chkOtros.Unchecked   += (_, _) => AplicarFiltroTipo();

            // Contenedor inline alineado
            var chkPanel = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center };
            chkPanel.Children.Add(_chkEntrada);
            chkPanel.Children.Add(_chkSalida);
            chkPanel.Children.Add(_chkOtros);

            DockPanel.SetDock(chkPanel, Dock.Left);
            fp2.Children.Add(chkPanel);

            // Separador + nota
            fp2.Children.Add(new Border { Width = 1, Background = MBr("#2A7AB5"),
                Margin = new Thickness(4, 2, 14, 2), VerticalAlignment = VerticalAlignment.Stretch });
            fp2.Children.Add(new TextBlock {
                Text = "Filtro instantáneo — sin nueva consulta a la base de datos",
                Foreground = MBr("#7FB3D3"), FontSize = 10, FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center });

            f2Border.Child = fp2; root.Children.Add(f2Border);

            // ── GRILLA ──────────────────────────────────────────────────────
            var gridOuter = new Border { BorderBrush = MBr("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(8, 6, 8, 6) };
            Grid.SetRow(gridOuter, 3);

            var gridPanel = new Grid();
            gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var gridHdr = new Border { Background = MBr("#1A4F6E"), Padding = new Thickness(12, 6, 12, 6) };
            gridHdr.Child = new TextBlock { Text = "📋  Entradas · Salidas · Transferencias",
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
            Grid.SetRow(gridHdr, 0); gridPanel.Children.Add(gridHdr);

            // headers naranja con sort
            var colHdrStyle = BuildMovHeaderStyle();

            // colores por tipo de movimiento
            var rowStyle = new Style(typeof(DataGridRow));
            var dtEntrada = new DataTrigger { Binding = new System.Windows.Data.Binding("Movimiento"), Value = "ENTRADA" };
            dtEntrada.Setters.Add(new Setter(DataGridRow.ForegroundProperty,  MBr("#1B5E20")));
            dtEntrada.Setters.Add(new Setter(DataGridRow.BackgroundProperty,  MBr("#F1F8E9")));
            var dtSalida = new DataTrigger { Binding = new System.Windows.Data.Binding("Movimiento"), Value = "SALIDA" };
            dtSalida.Setters.Add(new Setter(DataGridRow.ForegroundProperty,   MBr("#B71C1C")));
            dtSalida.Setters.Add(new Setter(DataGridRow.BackgroundProperty,   MBr("#FFF3F3")));
            var dtAjuste = new DataTrigger { Binding = new System.Windows.Data.Binding("Movimiento"), Value = "AJUSTE" };
            dtAjuste.Setters.Add(new Setter(DataGridRow.ForegroundProperty,   MBr("#1565C0")));
            dtAjuste.Setters.Add(new Setter(DataGridRow.BackgroundProperty,   MBr("#E8F0FE")));
            rowStyle.Triggers.Add(dtEntrada); rowStyle.Triggers.Add(dtSalida); rowStyle.Triggers.Add(dtAjuste);

            _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 30,
                CanUserSortColumns = true, FontSize = 11,
                AlternatingRowBackground = MBr("#FAFAFA"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = MBr("#EEEEEE"),
                BorderThickness = new Thickness(0),
                ColumnHeaderStyle = colHdrStyle,
                RowStyle = rowStyle,
                SelectionMode = DataGridSelectionMode.Single };

            DataGridTextColumn GC(string h, string p, double w, string? fmt = null) =>
                new() { Header = h, Width = w, MinWidth = 40, SortMemberPath = p,
                    Binding = fmt != null ? new System.Windows.Data.Binding(p) { StringFormat = fmt }
                                          : new System.Windows.Data.Binding(p) };

            // Columna Movimiento con ancho fijo + badge visual
            _grid.Columns.Add(GC("Tipo",          "Movimiento",  95));
            _grid.Columns.Add(GC("Modo",           "Modo",       130));
            _grid.Columns.Add(GC("Fecha",          "Fecha",      130));
            _grid.Columns.Add(GC("Local",          "Local",      120));
            _grid.Columns.Add(GC("Destino",        "DestinoMostrar", 120));
            _grid.Columns.Add(GC("Código",         "Codigo",      90));
            _grid.Columns.Add(new DataGridTextColumn { Header = "Artículo", SortMemberPath = "Nombre",
                Binding = new System.Windows.Data.Binding("Nombre"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 180 });
            _grid.Columns.Add(GC("St. Ant.",       "StAnterior",  80, "N0"));
            _grid.Columns.Add(GC("Cantidad",       "Cantidad",    80, "N2"));
            _grid.Columns.Add(GC("P.Costo",        "PCostoAct",  100, "N0"));
            _grid.Columns.Add(GC("Usuario",        "Usuario",    110));

            _grid.MouseDoubleClick += OnFilaDobleClick;
            Grid.SetRow(_grid, 1); gridPanel.Children.Add(_grid);
            gridOuter.Child = gridPanel; root.Children.Add(gridOuter);

            // ── TOTALES ─────────────────────────────────────────────────────
            var totBar = new Border { Background = MBr("#263238"), Padding = new Thickness(14, 8, 14, 8) };
            Grid.SetRow(totBar, 4);
            _lblTotal = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12, Foreground = MBr("#4FC3F7") };
            totBar.Child = _lblTotal; root.Children.Add(totBar);

            Content = root;
        }

        private void OnFilaDobleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_grid.SelectedItem is not FilaMovArt r) return;
            MostrarDetalleModal(r);
        }

        private void MostrarDetalleModal(FilaMovArt r)
        {
            // Colores por tipo
            var (tipoColor, tipoBg) = r.Movimiento switch {
                "ENTRADA" => ("#1B5E20", "#E8F5E9"),
                "SALIDA"  => ("#B71C1C", "#FFEBEE"),
                _         => ("#1565C0", "#E8F0FE"),
            };
            var tipoIcon = r.Movimiento switch {
                "ENTRADA" => "⬆", "SALIDA" => "⬇", _ => "↔"
            };

            var dlg = new Window {
                Title = "Detalle del movimiento",
                Width = 480, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = MBr("#F4F8FB"),
            };

            // ── Header ──────────────────────────────────────────────────────
            var hdrGrid = new Grid { Height = 60 };
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.Children.Add(new TextBlock {
                Text = tipoIcon, FontSize = 26,
                Foreground = MBr(tipoColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 12, 0) });
            var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock {
                Text = $"Detalle de {r.Movimiento}",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14, FontWeight = FontWeights.Bold });
            titleSp.Children.Add(new TextBlock {
                Text = $"{r.Modo}  ·  {r.Fecha}",
                Foreground = MBr("#7FB3D3"), FontSize = 10 });
            Grid.SetColumn(titleSp, 1); hdrGrid.Children.Add(titleSp);
            var header = new Border { Background = MBr("#0E2F44"), Child = hdrGrid };

            // ── Helpers ─────────────────────────────────────────────────────
            static TextBlock MiniLbl(string t) => new TextBlock {
                Text = t, FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(127, 179, 211)),
                Margin = new Thickness(0, 0, 0, 2) };
            static TextBlock Val(string t, bool bold = false) => new TextBlock {
                Text = t, FontSize = 12,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 40, 60)),
                TextWrapping = TextWrapping.Wrap };
            static Border Card(UIElement child) => new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(214, 229, 239)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    ShadowDepth = 1, BlurRadius = 4, Opacity = 0.06,
                    Color = System.Windows.Media.Colors.Black, Direction = 270 },
                Child = child };
            StackPanel Row(string lbl, string val, bool bold = false) {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                sp.Children.Add(MiniLbl(lbl));
                sp.Children.Add(Val(val, bold));
                return sp; }

            // ── Chip tipo ───────────────────────────────────────────────────
            var chipSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            chipSp.Children.Add(new Border {
                Background = MBr(tipoBg),
                BorderBrush = MBr(tipoColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 4, 12, 4),
                Child = new TextBlock {
                    Text = $"{tipoIcon}  {r.Movimiento}  ·  {r.Modo}",
                    Foreground = MBr(tipoColor),
                    FontWeight = FontWeights.Bold, FontSize = 12 } });

            // ── Sección UBICACIÓN ────────────────────────────────────────────
            var locSp = new StackPanel();
            locSp.Children.Add(Row("FECHA", r.Fecha));
            locSp.Children.Add(Row("LOCAL", r.Local));
            if (!string.IsNullOrEmpty(r.DestinoMostrar) && r.DestinoMostrar != "—")
                locSp.Children.Add(Row("DESTINO", r.DestinoMostrar));
            locSp.Children.Add(Row("USUARIO", r.Usuario));

            // ── Sección ARTÍCULO ─────────────────────────────────────────────
            var artSp = new StackPanel();
            artSp.Children.Add(Row("CÓDIGO", r.Codigo));
            artSp.Children.Add(Row("ARTÍCULO", r.Nombre, bold: true));

            // ── Sección STOCK / PRECIOS ──────────────────────────────────────
            var stockGrid = new Grid();
            stockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel StatBox(string lbl, string val, string fg) {
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock {
                    Text = val, FontSize = 20, FontWeight = FontWeights.Bold,
                    Foreground = MBr(fg), TextAlignment = TextAlignment.Center });
                sp.Children.Add(new TextBlock {
                    Text = lbl, FontSize = 9, FontWeight = FontWeights.Bold,
                    Foreground = MBr("#7FB3D3"), TextAlignment = TextAlignment.Center });
                return sp; }

            var statsRow = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            statsRow.Children.Add(new Border {
                Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(4, 0, 4, 0),
                Background = MBr("#EEF2F6"), CornerRadius = new CornerRadius(6),
                Child = StatBox("STOCK ANTERIOR", r.StAnterior.ToString("N0"), "#546E7A") });
            statsRow.Children.Add(new Border {
                Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(4, 0, 4, 0),
                Background = MBr(tipoBg), CornerRadius = new CornerRadius(6),
                Child = StatBox("CANTIDAD", r.Cantidad.ToString("N2"), tipoColor) });

            var priceSp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            priceSp.Children.Add(Row("P. COSTO ANTERIOR Gs.", r.PCostoAnt.ToString("N0")));
            priceSp.Children.Add(Row("P. COSTO ACTUAL Gs.", r.PCostoAct.ToString("N0"), bold: true));

            // ── Ensamble ─────────────────────────────────────────────────────
            var body = new StackPanel { Margin = new Thickness(16, 12, 16, 4) };
            body.Children.Add(chipSp);
            body.Children.Add(Card(locSp));
            body.Children.Add(Card(artSp));
            var stockCard = new StackPanel();
            stockCard.Children.Add(statsRow);
            stockCard.Children.Add(priceSp);
            body.Children.Add(Card(stockCard));

            var btnCerrar = new Button {
                Content = "✓  Cerrar", Height = 36, Padding = new Thickness(28, 0, 28, 0),
                Background = MBr("#1A4F6E"), Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 12,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            btnCerrar.Click += (_, __) => dlg.Close();

            var footer = new Border {
                Padding = new Thickness(16, 10, 16, 14), Background = MBr("#EEF2F6"),
                BorderBrush = MBr("#D6E5EF"), BorderThickness = new Thickness(0, 1, 0, 0),
                Child = new StackPanel {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { btnCerrar } } };

            var root = new StackPanel();
            root.Children.Add(header);
            root.Children.Add(body);
            root.Children.Add(footer);

            dlg.Content = root;
            dlg.KeyDown += (_, e) => { if (e.Key == Key.Escape || e.Key == Key.Enter) dlg.Close(); };
            dlg.ShowDialog();
        }

        private static Style BuildMovHeaderStyle()
        {
            var orange     = MBr("#1A4F6E");
            var orangeDark = MBr("#1A4F6E");
            var white      = System.Windows.Media.Brushes.White;

            var ct = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            var outerBorder = new FrameworkElementFactory(typeof(Border));
            outerBorder.Name = "OuterBorder";
            outerBorder.SetValue(Border.BackgroundProperty, orange);
            outerBorder.SetValue(Border.BorderBrushProperty, orangeDark);
            outerBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 2));
            outerBorder.SetValue(Border.PaddingProperty, new Thickness(8, 6, 4, 6));

            var gridF = new FrameworkElementFactory(typeof(Grid));
            var fc0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            fc0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var fc1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            fc1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            var fc2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            fc2.SetValue(ColumnDefinition.WidthProperty, new GridLength(6));
            gridF.AppendChild(fc0); gridF.AppendChild(fc1); gridF.AppendChild(fc2);

            var txt = new FrameworkElementFactory(typeof(TextBlock));
            txt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Content") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            txt.SetValue(TextBlock.ForegroundProperty, white);
            txt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            txt.SetValue(TextBlock.FontSizeProperty, 11.0);
            txt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            txt.SetValue(Grid.ColumnProperty, 0);
            gridF.AppendChild(txt);

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
            gridF.AppendChild(arrowStack);

            var thumb = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Thumb));
            thumb.SetValue(Grid.ColumnProperty, 2);
            thumb.SetValue(FrameworkElement.CursorProperty, Cursors.SizeWE);
            thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            thumb.SetValue(FrameworkElement.WidthProperty, 6.0);
            thumb.SetValue(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            thumb.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            gridF.AppendChild(thumb);

            outerBorder.AppendChild(gridF);
            ct.VisualTree = outerBorder;

            var tAsc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Ascending };
            tAsc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortAsc"));
            ct.Triggers.Add(tAsc);
            var tDesc = new Trigger { Property = System.Windows.Controls.Primitives.DataGridColumnHeader.SortDirectionProperty, Value = System.ComponentModel.ListSortDirection.Descending };
            tDesc.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "SortDesc"));
            ct.Triggers.Add(tDesc);
            var tHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            tHover.Setters.Add(new Setter(Border.BackgroundProperty, orangeDark, "OuterBorder"));
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
                bool sinFecha = _rbTodos?.IsChecked == true;
                var  desde    = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                var  hasta    = _dpHasta.SelectedDate ?? DateTime.Today;
                var  busq     = _txtCodigo?.Text.Trim() ?? "";

                var where = new System.Text.StringBuilder("WHERE 1=1");
                if (!sinFecha) {
                    where.Append(" AND CAST(M.FECHA AS DATE) >= @Desde");
                    where.Append(" AND CAST(M.FECHA AS DATE) <= @Hasta");
                }
                if (_idLocalFiltro.HasValue) where.Append(" AND M.IDLOCAL = @IdLocal");
                if (!string.IsNullOrEmpty(busq))
                    where.Append(" AND (CAST(A.CA AS NVARCHAR(50)) LIKE @Busq OR A.D LIKE @Busq)");

                var sql = $@"
                    SELECT
                        CASE M.MOV
                            WHEN 1 THEN 'ENTRADA'
                            WHEN 2 THEN 'SALIDA'
                            WHEN 4 THEN 'AJUSTE'
                            ELSE 'OTRO' END                              AS Movimiento,
                        CASE M.MOD
                            WHEN 1 THEN 'Compra'
                            WHEN 2 THEN 'Venta'
                            WHEN 3 THEN 'Transferencia'
                            WHEN 4 THEN 'Ajuste manual'
                            WHEN 5 THEN 'Devolución'
                            ELSE '—' END                                  AS Modo,
                        CONVERT(VARCHAR(16), M.FECHA, 103)               AS Fecha,
                        ISNULL(L.NOMBRE,  '?')                           AS Local,
                        ISNULL(LD.NOMBRE, '—')                           AS Destino,
                        CASE WHEN M.IDLOCAL = M.IDDESTINO
                             THEN '—'
                             ELSE ISNULL(LD.NOMBRE, '—') END             AS DestinoMostrar,
                        CAST(A.CA AS NVARCHAR(50))                       AS Codigo,
                        A.D                                               AS Nombre,
                        CAST(M.STINI AS INT)                             AS StAnterior,
                        M.CANT                                            AS Cantidad,
                        M.PCANT                                           AS PCostoAnt,
                        M.PCACT                                           AS PCostoAct,
                        ISNULL(U.NOMBRE_USUARIO, '?')                    AS Usuario
                    FROM MOVART M
                    JOIN      ARTICULOS A  ON M.IDART     = A.ID
                    LEFT JOIN LOCALES   L  ON M.IDLOCAL   = L.ID_LOCAL
                    LEFT JOIN LOCALES   LD ON M.IDDESTINO = LD.ID_LOCAL
                    LEFT JOIN USUARIOS  U  ON M.IDU       = U.ID_USUARIO
                    {where}
                    ORDER BY M.FECHA DESC, M.IDMOVART DESC";

                using var conn = _db.Create();
                _todos = (await conn.QueryAsync<FilaMovArt>(sql, new {
                    Desde   = desde,
                    Hasta   = hasta,
                    IdLocal = _idLocalFiltro ?? 0,
                    Busq    = "%" + busq + "%"
                })).ToList();

                AplicarFiltroTipo();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error al cargar movimientos"); }
        }

        private void AplicarFiltroTipo()
        {
            if (_todos == null) return;
            bool verEnt  = _chkEntrada?.IsChecked == true;
            bool verSal  = _chkSalida?.IsChecked  == true;
            bool verOtros= _chkOtros?.IsChecked   == true;

            var vista = _todos.Where(r =>
                (r.Movimiento == "ENTRADA" && verEnt)  ||
                (r.Movimiento == "SALIDA"  && verSal)  ||
                (r.Movimiento != "ENTRADA" && r.Movimiento != "SALIDA" && verOtros)
            ).ToList();

            _grid.ItemsSource = vista;

            var ent = _todos.Count(r => r.Movimiento == "ENTRADA");
            var sal = _todos.Count(r => r.Movimiento == "SALIDA");
            var ots = _todos.Count - ent - sal;
            _lblConteo.Text = $"{vista.Count} de {_todos.Count} movimientos";
            _lblTotal.Text  = $"Total BD: {_todos.Count}   |   " +
                              $"✅ Entradas: {ent}   |   " +
                              $"🔻 Salidas: {sal}   |   " +
                              $"⚙️ Otros: {ots}   |   " +
                              $"Mostrando: {vista.Count}";
        }

        private void ImprimirMovArt(bool preview = false)
        {
            var filas = (_grid.ItemsSource as System.Collections.Generic.IEnumerable<FilaMovArt>)?.ToList();
            if (filas == null || filas.Count == 0)
            { MessageBox.Show("No hay datos para imprimir.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            bool sinFecha = _rbTodos?.IsChecked == true;
            int totalBD  = _todos.Count;
            int entradas = _todos.Count(r => r.Movimiento == "ENTRADA");
            int salidas  = _todos.Count(r => r.Movimiento == "SALIDA");
            int otros    = totalBD - entradas - salidas;

            var p = new MovArtPagina
            {
                Filas    = filas.Select(f => new FilaMovArtImp(
                               f.Movimiento, f.Modo, f.Fecha, f.Local, f.DestinoMostrar,
                               f.Codigo, f.Nombre, f.StAnterior, f.Cantidad, f.PCostoAct, f.Usuario)).ToList(),
                Desde    = sinFecha ? "" : _dpDesde?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
                Hasta    = sinFecha ? "" : _dpHasta?.SelectedDate?.ToString("dd/MM/yyyy") ?? "",
                FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "—",
                LogoPath = MovArtPagina.ResolverLogoPath(),
                TotalBD  = totalBD,
                Entradas = entradas,
                Salidas  = salidas,
                Otros    = otros,
            };

            if (preview)
                new MovArtPreviewWindow(p) { Owner = this }.ShowDialog();
            else
                MovArtImpresora.Imprimir(p, this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.F5) _ = Buscar();
        }
    }

    internal class FilaMovArt
    {
        public string  Movimiento     { get; set; } = "";
        public string  Modo           { get; set; } = "";
        public string  Fecha          { get; set; } = "";
        public string  Local          { get; set; } = "";
        public string  Destino        { get; set; } = "";
        public string  DestinoMostrar { get; set; } = "";
        public string  Codigo         { get; set; } = "";
        public string  Nombre         { get; set; } = "";
        public int     StAnterior     { get; set; }
        public decimal Cantidad       { get; set; }
        public decimal PCostoAnt      { get; set; }
        public decimal PCostoAct      { get; set; }
        public string  Usuario        { get; set; } = "";
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE USUARIOS (modal — filtro en HCreditosWindow)
// ══════════════════════════════════════════════════════════════════════════════
namespace CrediSoft.UI.Views.Informes
{
    internal class BuscadorUsuarioModal : Window
    {
        private readonly IDbConnectionFactory _db;
        private System.Windows.Controls.TextBox   _txtBuscar  = null!;
        private System.Windows.Controls.DataGrid  _grid       = null!;
        private System.Windows.Controls.TextBlock _lblConteo  = null!;
        private List<UsuarioItem> _todos = new();

        // UsuarioItem.NombreUsuario holds the display name; expose a convenience wrapper
        public UsuarioItem? UsuarioSeleccionado { get; private set; }

        private static System.Windows.Media.SolidColorBrush HBr(string hex) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        public BuscadorUsuarioModal(IDbConnectionFactory db)
        {
            _db   = db;
            Title = "Seleccionar Usuario";
            Width = 440; Height = 380;
            MinWidth = 340; MinHeight = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = HBr("#F0F2F5");
            BuildUI();
            Loaded += async (_, _) => await CargarAsync();
        }

        private void BuildUI()
        {
            var root = new System.Windows.Controls.Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hdrBg = new Border { Background = HBr("#0E2F44"), Padding = new Thickness(12, 10, 12, 10) };
            var hdrSp = new System.Windows.Controls.StackPanel { Orientation = Orientation.Horizontal };
            hdrSp.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "👤", FontSize = 16, Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            hdrSp.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "Buscar usuario:",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
            });
            _txtBuscar = new System.Windows.Controls.TextBox {
                Width = 200, Height = 30, FontSize = 13,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _txtBuscar.TextChanged += (_, _) => Filtrar();
            _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
            hdrSp.Children.Add(_txtBuscar);
            hdrBg.Child = hdrSp;
            System.Windows.Controls.Grid.SetRow(hdrBg, 0); root.Children.Add(hdrBg);

            _grid = new System.Windows.Controls.DataGrid {
                AutoGenerateColumns = false, IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = HBr("#E5E7EB"),
                RowBackground = System.Windows.Media.Brushes.White,
                AlternatingRowBackground = HBr("#F9FAFB"),
                FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
                ColumnHeaderStyle = BuildHeaderStyle()
            };
            _grid.Columns.Add(new DataGridTextColumn {
                Header = "Nombre",
                Binding = new System.Windows.Data.Binding("NombreUsuario"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _grid.MouseDoubleClick += (_, _) => Seleccionar();
            System.Windows.Controls.Grid.SetRow(_grid, 1); root.Children.Add(_grid);

            var barBtns = new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = HBr("#E5E7EB"), BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 8, 8, 8)
            };
            _lblConteo = new System.Windows.Controls.TextBlock {
                FontSize = 11, Foreground = HBr("#6B7280"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0)
            };

            System.Windows.Controls.Button MkBtn(string txt, string hex) => new System.Windows.Controls.Button {
                Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
                Background = HBr(hex),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            var btnSelec  = MkBtn("✔  Seleccionar", "#22C55E");
            var btnCerrar = MkBtn("✕  Cerrar",       "#6B7280");
            btnSelec.Click  += (_, _) => Seleccionar();
            btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };

            var btnsSp = new System.Windows.Controls.StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnsSp.Children.Add(btnSelec); btnsSp.Children.Add(btnCerrar);

            var barGrid = new System.Windows.Controls.Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            System.Windows.Controls.Grid.SetColumn(_lblConteo, 0);
            System.Windows.Controls.Grid.SetColumn(btnsSp, 1);
            barGrid.Children.Add(_lblConteo); barGrid.Children.Add(btnsSp);
            barBtns.Child = barGrid;
            System.Windows.Controls.Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

            Content = root;
            Loaded += (_, _) => _txtBuscar.Focus();
        }

        private static Style BuildHeaderStyle()
        {
            var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            s.Setters.Add(new Setter(Control.BackgroundProperty, HBr("#0E2F44")));
            s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
            s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            return s;
        }

        private async Task CargarAsync()
        {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<UsuarioItem>(
                "SELECT ID_USUARIO AS IdUsuario, NOMBRE_USUARIO AS NombreUsuario, CODIGO_USUARIO AS CodigoUsuario FROM USUARIOS ORDER BY NOMBRE_USUARIO");
            _todos = rows.ToList();
            ActualizarGrid(_todos);
        }

        private void Filtrar()
        {
            var q = _txtBuscar.Text.Trim();
            var lista = string.IsNullOrEmpty(q)
                ? _todos
                : _todos.Where(u => u.NombreUsuario.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            ActualizarGrid(lista);
        }

        private void ActualizarGrid(List<UsuarioItem> lista)
        {
            _grid.ItemsSource  = lista;
            _grid.SelectedItem = null;
            _lblConteo.Text    = $"{lista.Count} usuario{(lista.Count != 1 ? "s" : "")} encontrado{(lista.Count != 1 ? "s" : "")}";
        }

        private void Seleccionar()
        {
            if (_grid.SelectedItem is UsuarioItem u)
            { UsuarioSeleccionado = u; DialogResult = true; Close(); }
            else
                MessageBox.Show("Seleccione un usuario de la lista.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
