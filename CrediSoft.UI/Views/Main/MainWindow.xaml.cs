using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Maestros;
using CrediSoft.UI.Views.Ventas;
using CrediSoft.UI.Views.Cobros;
using CrediSoft.UI.Views.Caja;
using CrediSoft.UI.Views.Informes;
using CrediSoft.UI.Views.Transferencias;
using CrediSoft.UI.Views.Herramientas;
using CrediSoft.UI.Views.Compras;
using CrediSoft.UI.Views.Pagos;
using Dapper;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CrediSoft.UI.Views.Main;

// ── DTOs para el dashboard (solo lectura, sin afectar modelos Core) ──────────

public class DashSolicitud
{
    public int    IdSolicitud  { get; set; }
    public string Numero        { get; set; } = "";
    public string LocalNombre   { get; set; } = "";
    public int?   IdLocal       { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public string FechaStr      => FechaSolicitud.ToString("dd/MM/yyyy");
    public string ClienteNombre { get; set; } = "";
    public string VendedorNombre { get; set; } = "";
    public string Estado        { get; set; } = "";
    public bool   VentaGenerada { get; set; }
}

public class DashCuota
{
    public int     IdGeneradas      { get; set; }
    public string  ClienteCi        { get; set; } = "";
    public string  ClienteNombre    { get; set; } = "";
    public byte    NCuota           { get; set; }
    // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial, no una cuota real —
    // ver comentario en Cuota.NCuotaVisible.
    public int     NCuotaVisible    => Math.Max(0, NCuota - 1);
    public DateTime Vto             { get; set; }
    public string  VtoStr           => Vto.ToString("dd/MM/yy");
    public decimal Monto            { get; set; }
    public decimal Entrega          { get; set; }
    public decimal Punitorio        { get; set; }
    // Mismo criterio que "TOTAL DE LA CUOTA" en Cobrar Cuota (ver RecalcTotal en
    // CobrosWindow.xaml.cs): capital pendiente (Monto - Entrega) + Punitorio. Antes acá se
    // mostraba solo Monto crudo (sin punitorio ni descuento de entregas previas), un monto
    // menor al que el cajero veía un segundo después al entrar a cobrar la misma cuota.
    public decimal MontoTotal       => Math.Max(0, Monto - Entrega) + Punitorio;
    public string  MontoStr         => MontoTotal.ToString("N0", CultureInfo.InvariantCulture);
    public byte    Estado           { get; set; }
    public bool    EstaVencida      => Estado == 0 && Vto.Date < DateTime.Today;
    public string  EstadoTextoCorto => EstaVencida ? "Vencida" : "Próxima";

    // Período de gracia de 5 días — misma fórmula que Cuota.DiasDeAtraso (confirmado 2026-07-30; antes 3).
    public int     DiasAtraso       => EstaVencida ? Math.Max(0, (DateTime.Today - Vto.Date).Days - 5) : 0;
}

public class DashPromo
{
    public string  Local       { get; set; } = "";
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public decimal Precio      { get; set; }
    public string  PrecioStr   => Precio.ToString("N0", CultureInfo.InvariantCulture);
    public string  InicioStr   { get; set; } = "";
    public string  FinStr      { get; set; } = "";
    public bool    EsVigente   { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────

public partial class MainWindow : Window
{
    private readonly ISessionService      _session;
    private readonly IDbConnectionFactory _db;

    private List<DashPromo> _todasPromos = new(); // cache para filtrado sin re-query
    private List<DashSolicitud> _todasSolicitudes = new(); // cache para filtrado sin re-query
    private List<DashCuota> _todasCuotas = new(); // cache para filtrado sin re-query

    private CrediSoft.UI.Services.UpdateService? _updateSvc;
    private CrediSoft.UI.Services.UpdateInfo?    _updateInfo;

    public MainWindow()
    {
        InitializeComponent();
        _session = SessionService.Instance;
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        CargarIconoVentana();
        ActualizarStatusBar();
        CargarLogo();
        Loaded += async (_, _) =>
        {
            await CargarDashboardAsync();
            await VerificarActualizacionPendienteAsync();
            await CargarCajaAbiertaAsync();
        };
    }

    // Card "Caja abierta del local" en el encabezado — contexto rápido de quién abrió la caja
    // física de este local y cuándo, para que el cajero/vendedor no tenga dudas al facturar
    // (pedido explícito 2026-08-01, tras un reclamo de ventas hechas sin verificar apertura).
    private async Task CargarCajaAbiertaAsync()
    {
        var idLocal = _session.LocalActual?.IdLocal;
        if (idLocal == null) { CardCajaAbierta.Visibility = Visibility.Collapsed; return; }

        try
        {
            var cajaRepo = App.Services.GetRequiredService<ICajaRepository>();
            var caja = await cajaRepo.ObtenerCajaAbiertaAsync(idLocal.Value);
            if (caja == null) { CardCajaAbierta.Visibility = Visibility.Collapsed; return; }

            // Se incluye el N° de local además del nombre — varios locales tienen nombres
            // parecidos (ej. "Credimar San Juan" vs "Sucursal 1 SJN") y el número es la forma
            // rápida de confirmar sin ambigüedad de cuál se trata.
            var nombreLocal = _session.LocalActual?.NombreLocal ?? "";
            TxtCajaAbiertaLocal.Text = $"Local: {idLocal.Value} — {nombreLocal}";
            TxtCajaAbiertaDetalle.Text =
                $"Abierta por {caja.NombreCajero} — {caja.FechaApertura:dd/MM/yyyy HH:mm}";
            CardCajaAbierta.Visibility = Visibility.Visible;
        }
        catch
        {
            // Si falla la consulta (ej. conexión), no bloquear el resto del dashboard —
            // el card simplemente no se muestra.
            CardCajaAbierta.Visibility = Visibility.Collapsed;
        }
    }

    // Se re-consulta aca (ademas del chequeo automatico al iniciar en App.OnActivated) para
    // que el boton se resalte tanto si el usuario cerro el dialogo con "Más tarde" como si
    // ya estaba en MainWindow cuando salio una version nueva y volvio a abrir el sistema.
    private async Task VerificarActualizacionPendienteAsync()
    {
        if (Application.Current is not App app) return;

        var (svc, info) = await app.ObtenerActualizacionPendienteAsync();
        _updateSvc  = svc;
        _updateInfo = info;

        BtnActualizarVersion.Visibility = info != null ? Visibility.Visible : Visibility.Collapsed;
        if (info != null)
            BtnActualizarVersion.ToolTip = $"Versión {info.Version} disponible — clic para actualizar";
    }

    private void OnActualizarVersionClick(object s, RoutedEventArgs e)
    {
        if (_updateSvc == null || _updateInfo == null) return;

        var dlg = new CrediSoft.UI.Views.Update.UpdateDialog(_updateSvc, _updateInfo) { Owner = this };
        dlg.ShowDialog();
    }

    private void CargarIconoVentana()
    {
        var ruta = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credimar.ico");
        if (!System.IO.File.Exists(ruta)) return;

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(ruta, UriKind.Absolute);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            Icon = bmp;
        }
        catch { }
    }

    // ── Logo ─────────────────────────────────────────────────────────────────
    private void CargarLogo()
    {
        const string nombre = "logotipocredimar2.png";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidatos = new[]
        {
            System.IO.Path.Combine(baseDir, nombre),
            System.IO.Path.Combine(baseDir, "..", nombre),
            System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", nombre),
        };
        var ruta = candidatos.FirstOrDefault(System.IO.File.Exists);
        if (ruta == null) return;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(ruta, UriKind.Absolute);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            ImgLogoHeader.Source = bmp;
        }
        catch { }
    }

    // ── StatusBar ────────────────────────────────────────────────────────────
    private void ActualizarStatusBar()
    {
        if (_session.UsuarioActual != null)
        {
            var usuario = _session.UsuarioActual;
            TxtStatusUsuario.Text = $"  {usuario.NombreUsuario} ({usuario.CargoUsuario})";
            TxtStatusLocal.Text   = $"  {_session.LocalActual?.NombreLocal}";
            TxtSaludo.Text        = $"Bienvenido, {usuario.NombreUsuario.Split(' ')[0]}";

            // Nombre, N° y teléfono del local de la sesión — pedido explícito para tenerlo a
            // la vista junto al saludo, sin depender de si hay caja abierta o no.
            var local = _session.LocalActual;
            TxtLocalActual.Text = local == null ? "" : string.IsNullOrWhiteSpace(local.TelefonoLocal)
                ? $"Local {local.IdLocal} — {local.NombreLocal}"
                : $"Local {local.IdLocal} — {local.NombreLocal} — Tel: {local.TelefonoLocal}";

            // Multas a funcionarios: acceso restringido a administrador + código 67, mismo
            // criterio ya usado en Caja para este mismo usuario (Usuario.PuedeVerTodosLosLocales).
            // Oculto por completo del menú para el resto, no solo bloqueado al hacer clic.
            MnuMultas.Visibility = usuario.PuedeVerTodosLosLocales ? Visibility.Visible : Visibility.Collapsed;

            // Asignar cobrador: mismo criterio (administrador + código 67) — pedido explícito
            // para no dejar que cualquier vendedor reasigne cuotas de otros cobradores.
            MnuAsignarCobrador.Visibility = usuario.PuedeAsignarCobradores ? Visibility.Visible : Visibility.Collapsed;

            // Descuento por Nota de Crédito: mismo criterio (administrador + código 67) —
            // pedido explícito, solo ellos pueden crear descuentos que después cualquier
            // cajero de cualquier local aplica al cobrar la cuota.
            MnuDescuentoCuota.Visibility = usuario.PuedeCrearDescuentosCuota ? Visibility.Visible : Visibility.Collapsed;
        }
        TxtStatusFecha.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm");
    }

    private void SetModulo(string nombre) => TxtStatusModulo.Text = nombre;

    // ── Dashboard ────────────────────────────────────────────────────────────
    private async void OnRefreshDashboard(object s, RoutedEventArgs e) => await CargarDashboardAsync();

    private async Task CargarDashboardAsync()
    {
        SpCargandoDashboard.Visibility = Visibility.Visible;
        try { await CargarDashboardInternoAsync(); }
        finally { SpCargandoDashboard.Visibility = Visibility.Collapsed; }
    }

    private async Task CargarDashboardInternoAsync()
    {
        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) ve el dashboard con datos de todos los locales.
        // Un usuario normal solo ve solicitudes/cuotas/promociones de SU propio local — evita
        // que se filtre información de otras sucursales en el panel principal.
        var puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
        int? idLocal = puedeVerTodos ? null : _session.LocalActual?.IdLocal;
        var errores  = new System.Text.StringBuilder();

        // ── Solicitudes pendientes de verificar ──────────────────────────────
        var solicitudes = new List<DashSolicitud>();
        try
        {
            using var conn = _db.Create();
            // Dashboard: siempre todos los locales.
            //
            // Reemplaza EXEC CARGAR_REV_SOL__VISOR_ADMIN_CS (SP legado) — mismo bug ya
            // corregido en VisorSolicitudesWindow.Cargar() (VentasWindows.cs): el SP trae
            // "TOP 100" ordenado ascendente por fecha, así que con más de 100 solicitudes
            // pendientes/aceptadas acumuladas, las MÁS NUEVAS quedan totalmente fuera —
            // exactamente lo que dejaba "SOLICITUDES A VERIFICAR" en 0 en el dashboard pese a
            // haber solicitudes reales pendientes (bug real reportado: la solicitud 5986,
            // recién puesta en Verificar, no aparecía ni se contaba). Se había corregido en el
            // Visor de Solicitudes pero no acá — mismo SP, código duplicado en otro archivo.
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT IDSOLICITUD, NUMERO, " +
                "CASE WHEN ESTADO = 0 THEN 'Verificar' WHEN ESTADO = 1 THEN 'Aceptado' ELSE 'Rechazado' END AS ESTADO, " +
                "CONVERT(VARCHAR(10), FECHA_SOLICITUD, 103) AS FECHA_SOLICITUD " +
                "FROM CAB_SOL_SALES WHERE ESTADO < 2 ORDER BY FECHA_SOLICITUD ASC");

            // El SP filtra ESTADO<2, por lo que solo trae "Verificar" y "Aceptado" (nunca
            // "Rechazado" en la práctica) — el filtro por estado default es "Todos" (ver
            // CboSolicitudEstado en el XAML) y se aplica después en AplicarFiltroSolicitudes,
            // vía el combo de estados.
            var rowsList = rows.ToList();
            if (rowsList.Count == 0) errores.Append("Sol:0 filas SP | ");
            foreach (var r in rowsList)
            {
                var estado = ((object?)r.ESTADO)?.ToString() ?? "";

                var fechaStr = ((object?)r.FECHA_SOLICITUD)?.ToString() ?? "";
                DateTime.TryParseExact(fechaStr, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var fecha);

                // El SP no devuelve local/cliente directamente — los obtenemos de la sub-query
                // que ya hace el VisorSolicitudesWindow. Para el dashboard: tomamos NUMERO y ESTADO.
                // Cargamos cliente y local en una segunda query agrupada.
                int idSol = (int)(r.IDSOLICITUD ?? 0);
                solicitudes.Add(new DashSolicitud
                {
                    IdSolicitud   = idSol,
                    Numero        = ((object?)r.NUMERO)?.ToString() ?? idSol.ToString(),
                    LocalNombre   = "",   // se rellena abajo
                    FechaSolicitud = fecha,
                    ClienteNombre = "",   // se rellena abajo
                    Estado        = estado,
                });
            }

            // Completar cliente+local con IDSOLICITUD como clave de join — antes se armaba un
            // IN(...) concatenando NUMERO (texto char(15)) SIN comillas en el SQL, lo que SQL
            // Server trataba como literal numérico: la comparación contra la columna char
            // perdía los ceros a la izquierda en la conversión implícita y algunas filas no
            // matcheaban nunca. Eso dejaba sol.IdLocal en null para esas solicitudes, y
            // "null != idLocal" es true, así que el RemoveAll de abajo las eliminaba en
            // silencio como si fueran de otro local — bug real: una solicitud recién creada
            // en el LOCAL correcto de un usuario no-admin no aparecía en su propio dashboard.
            // IDSOLICITUD es entero (ya disponible en ambos resultsets) y Dapper lo parametriza
            // de forma segura con IN @Ids, sin ambigüedad de tipos ni concatenación manual.
            if (solicitudes.Count > 0)
            {
                var ids = solicitudes.Select(s => s.IdSolicitud).ToList();
                // VentaGenerada: EXISTS contra CABECERA_SALES.NSOLICITUD, mismo criterio que
                // SolicitudItem.VentaGenerada (VentasWindows.cs) — el SP admin filtra por
                // ESTADO<2 nada más, así que una solicitud "Aceptado" con la entrega ya
                // confirmada (venta generada) seguía apareciendo acá indefinidamente. Pedido
                // explícito: no mostrar más en el dashboard las solicitudes ya resueltas.
                var cabs = (await conn.QueryAsync<dynamic>(
                    "SELECT s.IDSOLICITUD, s.ID_LOCAL as IdLocal, cl.NOMBRE_CLIENTE as NomCli, l.NOMBRE as NomLocal, " +
                    "ISNULL(u.NOMBRE_USUARIO,'') as NomVend, " +
                    "CASE WHEN EXISTS (SELECT 1 FROM CABECERA_SALES cs WHERE cs.NSOLICITUD = s.NUMERO) THEN 1 ELSE 0 END as VentaGenerada " +
                    "FROM CAB_SOL_SALES s " +
                    "LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE " +
                    "LEFT JOIN LOCALES  l  ON s.ID_LOCAL   = l.ID_LOCAL " +
                    "LEFT JOIN USUARIOS u  ON s.ID_USUARIO = u.ID_USUARIO " +
                    "WHERE s.IDSOLICITUD IN @Ids", new { Ids = ids })).ToDictionary(
                        r => (int)r.IDSOLICITUD,
                        r => r);

                foreach (var sol in solicitudes)
                {
                    if (cabs.TryGetValue(sol.IdSolicitud, out var cab))
                    {
                        sol.ClienteNombre = ((object?)cab.NomCli)?.ToString() ?? "";
                        sol.LocalNombre   = ((object?)cab.NomLocal)?.ToString() ?? "";
                        sol.VendedorNombre = ((object?)cab.NomVend)?.ToString() ?? "";
                        // CAB_SOL_SALES.ID_LOCAL es tinyint en SQL Server — Dapper con
                        // QueryAsync<dynamic> lo devuelve como byte, no como int, así que
                        // "cab.IdLocal is int" siempre daba false y sol.IdLocal quedaba en
                        // null para TODAS las solicitudes sin excepción. Con IdLocal=null,
                        // "null != idLocal" es true, y el RemoveAll de abajo las eliminaba a
                        // todas — este era el bug real detrás de "el dashboard nunca muestra
                        // ninguna solicitud pendiente", incluso después de corregir el join
                        // por NUMERO→IDSOLICITUD. Convert.ToInt32 acepta byte/short/int/etc.
                        object? idLocalRaw = cab.IdLocal;
                        sol.IdLocal = idLocalRaw is null ? (int?)null : Convert.ToInt32(idLocalRaw);
                        sol.VentaGenerada = ((byte?)cab.VentaGenerada) == 1;
                    }
                }

                // Usuario sin acceso total: descarta solicitudes de otros locales — el SP
                // (CARGAR_REV_SOL__VISOR_ADMIN_CS) no acepta filtro de local, así que se
                // filtra en memoria tras completar el ID_LOCAL real desde CAB_SOL_SALES.
                if (!puedeVerTodos)
                    solicitudes.RemoveAll(s => s.IdLocal != idLocal);

                solicitudes.RemoveAll(s => s.VentaGenerada);
            }
        }
        catch (Exception ex) { errores.Append($"Sol:{ex.Message} | "); }

        // ── Cuotas próximas + vencidas ────────────────────────────────────────
        var cuotas = new List<DashCuota>();
        try
        {
            using var conn = _db.Create();
            // Punitorio recalculado en vivo con la misma fórmula que CuotaRepository
            // (PunitorioRecalculado/CalcularPunitoriocAsync) — así "Monto" en este panel
            // coincide con "TOTAL DE LA CUOTA" que ve el cajero al entrar a cobrar la cuota,
            // en vez de mostrar solo el capital crudo sin punitorio ni entregas descontadas.
            var sqlCuotas =
                "SELECT TOP 200 G.IDGENERADAS, G.NCUOTA, G.MONTO, G.ENTREGA, G.VTO, G.ESTADO, G.IDLOCAL, " +
                "CASE WHEN G.VTO >= CAST(GETDATE() AS DATE) THEN 0 ELSE " +
                "  ROUND(G.MONTO * (ISNULL((SELECT TOP 1 VALOR_PUNITORIO FROM CONFIGURACION), 0) / 100.0) / 25.0 * " +
                "    (CASE WHEN (DATEDIFF(day, G.VTO, GETDATE()) - 5) < 0 THEN 0 ELSE (DATEDIFF(day, G.VTO, GETDATE()) - 5) END), 0) " +
                "END AS PUNITORIO_CALC, " +
                "C.NOMBRE_CLIENTE as ClienteNombre, C.CI_CLIENTE as ClienteCi " +
                "FROM GENERADAS G " +
                "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
                "INNER JOIN CLIENTES C ON CB.ID_CLIENTE = C.ID_CLIENTE " +
                "WHERE G.ESTADO = 0 " +
                "AND G.VTO <= DATEADD(day, 7, CAST(GETDATE() AS date)) " +
                (idLocal.HasValue ? "AND G.IDLOCAL = @Local " : "") +
                "ORDER BY G.VTO";

            cuotas = (await conn.QueryAsync<dynamic>(sqlCuotas, new { Local = idLocal }))
                .Select(r => new DashCuota
                {
                    IdGeneradas   = Convert.ToInt32(r.IDGENERADAS ?? 0),
                    ClienteCi     = ((object?)r.ClienteCi)?.ToString() ?? "",
                    ClienteNombre = ((object?)r.ClienteNombre)?.ToString() ?? "",
                    NCuota        = Convert.ToByte(r.NCUOTA ?? 0),
                    Vto           = r.VTO is DateTime vd ? vd : DateTime.Today,
                    Monto         = Convert.ToDecimal(r.MONTO ?? 0m),
                    Entrega       = Convert.ToDecimal(r.ENTREGA ?? 0m),
                    Punitorio     = Convert.ToDecimal(r.PUNITORIO_CALC ?? 0m),
                    Estado        = Convert.ToByte(r.ESTADO ?? 0),
                }).ToList();
        }
        catch (Exception ex) { errores.Append($"Cuotas:{ex.Message} | "); }

        // ── Promociones activas (todas con PR=1, sin filtro de fecha) ────────
        var promos = new List<DashPromo>();
        try
        {
            using var conn = _db.Create();
            var hoy = DateTime.Today;
            var sqlPromo =
                "SELECT TOP 500 L.NOMBRE AS Local, " +
                "CAST(A.CA AS NVARCHAR(50)) AS Codigo, " +
                "A.D AS Articulo, " +
                "P.PPROMO AS PPromo, " +
                "CONVERT(VARCHAR(10), P.INICIO, 103) AS Inicio, " +
                "CONVERT(VARCHAR(10), P.FIN,    103) AS Fin " +
                "FROM LOCALES L " +
                "INNER JOIN PRICES   P ON L.ID_LOCAL = P.IDLOCAL " +
                "INNER JOIN ARTICULOS A ON P.IDART   = A.ID " +
                "WHERE P.PR = 1 AND P.DELETADO = 0 " +
                (idLocal.HasValue ? "AND L.ID_LOCAL = @Local " : "") +
                "ORDER BY L.ID_LOCAL, A.D";

            promos = (await conn.QueryAsync<dynamic>(sqlPromo, new { Local = idLocal }))
                .Select(r =>
                {
                    var iniStr = ((object?)r.Inicio)?.ToString() ?? "";
                    var finStr = ((object?)r.Fin)?.ToString()    ?? "";
                    // Detectar si es vigente hoy para el KPI
                    bool iniOk = DateTime.TryParseExact(iniStr, new[] { "dd/MM/yyyy", "d/M/yyyy" },
                        CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var ini);
                    bool finOk = DateTime.TryParseExact(finStr, new[] { "dd/MM/yyyy", "d/M/yyyy" },
                        CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fin);
                    bool vigente = !iniOk || !finOk || (ini <= hoy && hoy <= fin);
                    return new DashPromo
                    {
                        Local       = ((object?)r.Local)?.ToString()    ?? "",
                        Codigo      = ((object?)r.Codigo)?.ToString()   ?? "",
                        Descripcion = ((object?)r.Articulo)?.ToString() ?? "",
                        Precio      = Convert.ToDecimal(r.PPromo ?? 0m),
                        InicioStr   = iniStr,
                        FinStr      = finStr,
                        EsVigente   = vigente,
                    };
                }).ToList();

            _todasPromos = promos;
        }
        catch (Exception ex) { errores.Append($"Promos:{ex.Message} | "); }

        // ── Actualizar UI ─────────────────────────────────────────────────────
        int pendientes = cuotas.Count;
        int vencidas   = cuotas.Count(c => c.EstaVencida);

        KpiCuotas.Text      = pendientes.ToString();
        KpiCuotasSub.Text   = puedeVerTodos ? "todos los locales" : (_session.LocalActual?.NombreLocal ?? "tu local");
        KpiVencidas.Text    = vencidas.ToString();
        KpiSolicitudes.Text = solicitudes.Count(s => s.Estado.Equals("Verificar", StringComparison.OrdinalIgnoreCase)).ToString();

        int vigentesHoy = promos.Count(p => p.EsVigente);
        KpiPromociones.Text    = vigentesHoy.ToString();
        KpiPromocionesSub.Text = $"vigentes hoy  ({promos.Count} activas)";

        _todasSolicitudes = solicitudes;
        TxtBuscarSolicitud.Text = "";
        DpSolicitudDesde.SelectedDate = null;
        AplicarFiltroSolicitudes();

        _todasCuotas = cuotas;
        TxtBuscarCuota.Text = "";
        DpCuotaDesde.SelectedDate = null;
        DpCuotaHasta.SelectedDate = null;
        AplicarFiltroCuotas();

        // Promos: mostrar todas y limpiar buscador
        TxtBuscarPromo.Text = "";
        AplicarFiltroPromos();

        TxtStatusFecha.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm");

        if (errores.Length > 0)
            TxtStatusModulo.Text = errores.ToString().TrimEnd(' ', '|');
    }

    // ── Búsqueda y filtro de fecha de solicitudes ────────────────────────────
    private void OnBuscarSolicitudChanged(object s, TextChangedEventArgs e) => AplicarFiltroSolicitudes();

    private void OnLimpiarBuscarSolicitud(object s, RoutedEventArgs e)
    {
        TxtBuscarSolicitud.Text = "";
        TxtBuscarSolicitud.Focus();
    }

    private void OnFiltroFechaSolicitudChanged(object s, SelectionChangedEventArgs e) => AplicarFiltroSolicitudes();

    private void OnFiltroEstadoSolicitudChanged(object s, SelectionChangedEventArgs e) => AplicarFiltroSolicitudes();

    private void AplicarFiltroSolicitudes()
    {
        // Durante InitializeComponent(), el ComboBoxItem con IsSelected="True" dispara
        // SelectionChanged antes de que el resto de los controles del panel existan —
        // ignorar esa llamada prematura (se vuelve a aplicar el filtro al terminar de cargar).
        if (LstSolicitudes == null || TxtBuscarSolicitud == null || DpSolicitudDesde == null)
            return;

        var q = TxtBuscarSolicitud.Text.Trim();
        var desde = DpSolicitudDesde.SelectedDate;
        var estadoSel = (CboSolicitudEstado.SelectedItem as ComboBoxItem)?.Tag as string ?? "Todos";

        IEnumerable<DashSolicitud> resultado = _todasSolicitudes;

        if (estadoSel != "Todos")
            resultado = resultado.Where(s => s.Estado.Equals(estadoSel, StringComparison.OrdinalIgnoreCase));

        if (desde.HasValue)
            resultado = resultado.Where(s => s.FechaSolicitud.Date >= desde.Value.Date);

        if (!string.IsNullOrEmpty(q))
            resultado = resultado.Where(s =>
                s.Numero.Contains(q, StringComparison.OrdinalIgnoreCase)        ||
                s.ClienteNombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.LocalNombre.Contains(q, StringComparison.OrdinalIgnoreCase));

        // Verificar primero (son las que requieren acción), y dentro de cada estado las
        // más nuevas arriba — antes se mostraba tal cual venía del query (FECHA_SOLICITUD
        // ASC), así que una solicitud recién puesta en "Verificar" quedaba mezclada abajo
        // de la lista, atrás de decenas de "Aceptado" viejas, en vez de saltar a la vista.
        var lista = resultado
            .OrderBy(s => s.Estado.Equals("Verificar", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(s => s.FechaSolicitud)
            .ToList();
        LstSolicitudes.ItemsSource = lista;
    }

    // ── Búsqueda y filtro de fecha de cuotas próximas ────────────────────────
    private void OnBuscarCuotaChanged(object s, TextChangedEventArgs e) => AplicarFiltroCuotas();

    private void OnLimpiarBuscarCuota(object s, RoutedEventArgs e)
    {
        TxtBuscarCuota.Text = "";
        TxtBuscarCuota.Focus();
    }

    private void OnFiltroFechaCuotaChanged(object s, SelectionChangedEventArgs e) => AplicarFiltroCuotas();

    private void OnFiltroAtrasoCuotaChanged(object s, TextChangedEventArgs e) => AplicarFiltroCuotas();

    private void OnSoloDigitosAtrasoCuota(object s, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    // GridViewColumn.Width no soporta "*" (star sizing) como Grid — se ajusta la columna
    // "Cliente"/"Artículo" a mano para que ocupe el espacio sobrante y no quede un hueco
    // vacío a la derecha cuando el panel es más ancho que la suma de las columnas fijas.
    // Se recalcula vía Dispatcher (prioridad Loaded) porque en el primer SizeChanged tras
    // maximizar/restaurar la ventana, ActualWidth aún no refleja el ancho final del layout.
    private void OnLstCuotasSizeChanged(object s, SizeChangedEventArgs e)
        => Dispatcher.InvokeAsync(AjustarColumnaCuotas, DispatcherPriority.Loaded);

    private void AjustarColumnaCuotas()
    {
        const double otrasColumnas = 32 + 68 + 82 + 42 + 58; // N° + Vto + Monto + Atr. + Estado
        var disponible = LstCuotas.ActualWidth - otrasColumnas;
        ColCuotaCliente.Width = Math.Max(90, disponible);
    }

    private void OnLstPromocionesSizeChanged(object s, SizeChangedEventArgs e)
        => Dispatcher.InvokeAsync(AjustarColumnaPromociones, DispatcherPriority.Loaded);

    private void AjustarColumnaPromociones()
    {
        const double otrasColumnas = 82 + 60 + 72 + 65 + 65; // Local + Código + P.Promo + Desde + Hasta
        var disponible = LstPromociones.ActualWidth - otrasColumnas;
        ColPromoArticulo.Width = Math.Max(100, disponible);
    }

    private void OnLstSolicitudesSizeChanged(object s, SizeChangedEventArgs e)
        => Dispatcher.InvokeAsync(AjustarColumnaSolicitudes, DispatcherPriority.Loaded);

    private void AjustarColumnaSolicitudes()
    {
        const double otrasColumnas = 110 + 90 + 78 + 120 + 78; // Nro Solicitud + Local + Fecha + Vendedor + Estado
        var disponible = LstSolicitudes.ActualWidth - otrasColumnas;
        ColSolicitudCliente.Width = Math.Max(100, disponible);
    }

    private void AplicarFiltroCuotas()
    {
        // Mismo resguardo que AplicarFiltroSolicitudes: evita referencias nulas si algo
        // dispara el filtro antes de que el panel termine de construirse.
        if (LstCuotas == null || TxtBuscarCuota == null || DpCuotaDesde == null || DpCuotaHasta == null
            || TxtAtrasoMin == null || TxtAtrasoMax == null)
            return;

        var q     = TxtBuscarCuota.Text.Trim();
        var desde = DpCuotaDesde.SelectedDate;
        var hasta = DpCuotaHasta.SelectedDate;
        int? atrasoMin = int.TryParse(TxtAtrasoMin.Text.Trim(), out var aMin) ? aMin : null;
        int? atrasoMax = int.TryParse(TxtAtrasoMax.Text.Trim(), out var aMax) ? aMax : null;

        IEnumerable<DashCuota> resultado = _todasCuotas;

        if (desde.HasValue)
            resultado = resultado.Where(c => c.Vto.Date >= desde.Value.Date);

        if (hasta.HasValue)
            resultado = resultado.Where(c => c.Vto.Date <= hasta.Value.Date);

        if (atrasoMin.HasValue)
            resultado = resultado.Where(c => c.DiasAtraso >= atrasoMin.Value);

        if (atrasoMax.HasValue)
            resultado = resultado.Where(c => c.DiasAtraso <= atrasoMax.Value);

        if (!string.IsNullOrEmpty(q))
            resultado = resultado.Where(c => c.ClienteNombre.Contains(q, StringComparison.OrdinalIgnoreCase));

        LstCuotas.ItemsSource = resultado.ToList();
    }

    // ── Búsqueda de promociones ──────────────────────────────────────────────
    private void OnBuscarPromoChanged(object s, TextChangedEventArgs e) => AplicarFiltroPromos();

    private void OnLimpiarBuscarPromo(object s, RoutedEventArgs e)
    {
        TxtBuscarPromo.Text = "";
        TxtBuscarPromo.Focus();
    }

    private void AplicarFiltroPromos()
    {
        var q = TxtBuscarPromo.Text.Trim();
        var resultado = string.IsNullOrEmpty(q)
            ? _todasPromos
            : _todasPromos.Where(p =>
                p.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase)      ||
                p.Local.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        LstPromociones.ItemsSource = resultado;

        int vigHoy = resultado.Count(p => p.EsVigente);
        TxtPromoContador.Text = string.IsNullOrEmpty(q)
            ? $"{resultado.Count} artículo(s) en promoción  ·  {vigHoy} vigentes hoy"
            : $"{resultado.Count} resultado(s) encontrado(s)";

        TxtPromoSubtitulo.Text = string.IsNullOrEmpty(q)
            ? "Todas las activas"
            : $"Filtro: \"{q}\"";
    }

    // ── Apertura de módulos ───────────────────────────────────────────────────
    private void AbrirVentana(Window win, string nombre)
    {
        // Si ya hay una ventana del mismo tipo abierta, la trae al frente
        var existente = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w != this && w.GetType() == win.GetType()
                                           && w.IsLoaded);
        if (existente != null)
        {
            if (existente.WindowState == WindowState.Minimized)
                existente.WindowState = WindowState.Normal;
            existente.Activate();
            win = null!; // descarta la instancia recién creada
            return;
        }

        SetModulo(nombre);
        win.Owner = this;
        // Evitar que WPF minimice el MainWindow al cerrar la ventana hija
        win.Closed += async (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();

            // Refresca el dashboard al volver de una ventana que puede haber creado una
            // solicitud/venta/cobro — antes había que apretar "Actualizar" a mano para ver
            // la novedad reflejada en los KPIs y listados (reportado: una venta a crédito
            // recién guardada no aparecía en "SOLICITUDES A VERIFICAR" hasta refrescar).
            if (RefrescaDashboardAlCerrar(win))
                await CargarDashboardAsync();

            // Datos del local en sesión (nombre/teléfono) pueden haber cambiado en Locales /
            // Sucursales — se repinta el encabezado del dashboard sin esperar a un nuevo login.
            if (win is LocalesWindow)
                ActualizarStatusBar();
        };

        // Ventanas "ancladas" (pedido explícito: que se vean pegadas bajo el menú de
        // ElectroMar, ocupando todo el ancho/alto restante, con una pestaña propia arriba
        // — igual que un navegador — en vez de flotar como ventana aparte). Se posicionan
        // y dimensionan exactamente al área de contenido de MainWindow (bajo el menú/
        // toolbars/barra de pestañas, sobre la status bar) y se resincronizan si
        // MainWindow se mueve/redimensiona/maximiza. No se embebe de verdad (seguiría
        // siendo una Window real, con WindowStyle=None ya puesto en su propio constructor)
        // para no reescribir su lógica interna.
        if (win is VerArticulosWindow or VerArticulosListadoWindow)
        {
            // El simulador de plan de pago necesita bastante alto real (MinHeight=660) para
            // no cortarse — si MainWindow no está maximizada, el área anclada calculada por
            // AnclarAAreaDeContenido puede quedar más chica que ese mínimo y el panel se
            // desborda fuera de la vista. Maximizar garantiza siempre el alto máximo posible.
            if (WindowState != WindowState.Maximized)
                WindowState = WindowState.Maximized;

            // AgregarPestana hace visible TabBarRoot (cambia el layout de MainWindow) —
            // debe ir ANTES de medir con AnclarAAreaDeContenido, y UpdateLayout fuerza que
            // WPF ya haya recalculado ActualHeight/posiciones antes de esa medición (sin
            // esto, la primera Reposicionar() mide con TabBarRoot todavía colapsada).
            AgregarPestana(win, nombre);
            UpdateLayout();
            AnclarAAreaDeContenido(win);
        }

        win.Show();
    }

    // Crea una pestaña (Border con título + botón X) en TabBarItems para `win`, muestra la
    // barra de pestañas si estaba oculta, y la quita automáticamente al cerrar `win`.
    private void AgregarPestana(Window win, string titulo)
    {
        // Celeste con sombreado (look "pestaña elevada" de navegador) — pedido explícito
        // de volver a este esquema de color en vez del azul oscuro del intento anterior.
        var pestana = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xAE, 0xD6, 0xF1)),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(12, 6, 6, 6),
            Margin = new Thickness(4, 4, 0, 0),
            MinWidth = 150,
            Cursor = Cursors.Hand,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = 0.25, BlurRadius = 6, ShadowDepth = 1,
            },
        };
        // Grid en vez de StackPanel: la columna del texto es Auto (mide exacto su ancho, no
        // se come el espacio del botón), la del botón X es Auto fija al final — evita que
        // el botón quede "empujado" fuera del área visible de la pestaña.
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var txt = new TextBlock
        {
            Text = titulo, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x4F, 0x6E)),
        };
        // Botón X cuadrado (no círculo) — pedido explícito.
        var btnX = new Button
        {
            Content = new TextBlock
            {
                Text = "✕", FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x4F, 0x6E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 20, Height = 20,
            Background = new SolidColorBrush(Color.FromRgb(0x8B, 0xB8, 0xDD)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var btnXTemplate = new ControlTemplate(typeof(Button));
        var btnXBorderFactory = new FrameworkElementFactory(typeof(Border));
        btnXBorderFactory.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        var btnXContentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        btnXContentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        btnXContentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        btnXBorderFactory.AppendChild(btnXContentFactory);
        btnXTemplate.VisualTree = btnXBorderFactory;
        btnX.Template = btnXTemplate;
        Grid.SetColumn(txt, 0);
        Grid.SetColumn(btnX, 2);
        fila.Children.Add(txt);
        fila.Children.Add(btnX);
        pestana.Child = fila;
        TabBarItems.Items.Add(pestana);
        TabBarRoot.Visibility = Visibility.Visible;

        // Clic en la pestaña (fuera de la X) trae la ventana al frente — igual que un
        // navegador al hacer clic en una pestaña inactiva.
        pestana.MouseLeftButtonUp += (_, _) =>
        {
            if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
            win.Activate();
        };
        btnX.Click += (_, _) => win.Close();

        win.Closed += (_, _) =>
        {
            TabBarItems.Items.Remove(pestana);
            if (TabBarItems.Items.Count == 0) TabBarRoot.Visibility = Visibility.Collapsed;
        };
    }

    // Alinea `win` (ya con WindowStyle=None) justo DEBAJO de la barra de pestañas (que a su
    // vez queda debajo de la barra de accesos rápidos — "Atrasos | H Cobranzas | ...") y
    // hasta el borde superior de la StatusBar — no a toda MainWindow, que taparía el Menu
    // y las barras de herramientas (pedido explícito: debe verse como una pestaña de
    // navegador, con el menú y las barras de ElectroMar siempre visibles arriba). Se
    // resincroniza mientras ambas ventanas estén abiertas.
    private void AnclarAAreaDeContenido(Window win)
    {
        void Reposicionar()
        {
            if (WindowState == WindowState.Minimized) return;
            var source = PresentationSource.FromVisual(this);
            var escala = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

            // Esquina inferior-izquierda de TabBarRoot (ya visible en este punto) = borde
            // superior del área a cubrir.
            var origenArea = TabBarRoot.PointToScreen(new Point(0, TabBarRoot.ActualHeight));
            // Esquina superior de la StatusBar = borde inferior del área a cubrir.
            var finArea     = StatusBarRoot.PointToScreen(new Point(0, 0));

            win.Left   = origenArea.X / escala;
            win.Top    = origenArea.Y / escala;
            win.Width  = ActualWidth;
            win.Height = (finArea.Y - origenArea.Y) / escala;
        }

        Reposicionar();
        SizeChanged     += (_, _) => Reposicionar();
        LocationChanged += (_, _) => Reposicionar();
        StateChanged    += (_, _) => Reposicionar();
    }

    // Ventanas cuyo cierre puede haber modificado datos que el dashboard muestra
    // (solicitudes, cuotas, promociones) — mantenido como whitelist explícita en vez de
    // refrescar siempre, para no penalizar el cierre de ventanas de solo lectura/consulta.
    private static bool RefrescaDashboardAlCerrar(Window win) => win is
        VentaCreditoWindow or
        VentaContadoWindow or
        VisorSolicitudesWindow or
        CobrosWindow or
        CrediSoft.UI.Views.Herramientas.NotaCreditoWindow or
        GestionPromocionesWindow or
        FinalizarPromoWindow or
        LocalesWindow;

    // MAESTROS
    private void OnMenuArticulos(object s, RoutedEventArgs e)      => AbrirVentana(new ArticulosWindow(),      "Artículos y/o Mercaderías");
    private void OnMenuClientes(object s, RoutedEventArgs e)       => AbrirVentana(new ClientesWindow(),       "Clientes");
    private void OnMenuBancos(object s, RoutedEventArgs e)         => AbrirVentana(new BancosWindow(),         "Bancos");
    private void OnMenuCategorias(object s, RoutedEventArgs e)     => AbrirVentana(new CategoriasWindow(),     "Categorías");
    private void OnMenuSubcategorias(object s, RoutedEventArgs e)  => AbrirVentana(new SubcategoriasWindow(),  "Subcategorías");
    private void OnMenuMarcas(object s, RoutedEventArgs e)         => AbrirVentana(new MarcasWindow(),         "Marcas");
    private void OnMenuProveedores(object s, RoutedEventArgs e)    => AbrirVentana(new ProveedoresWindow(),    "Proveedores");
    private void OnMenuMedidas(object s, RoutedEventArgs e)        => AbrirVentana(new MedidasWindow(),        "Unidades de medida");
    private void OnMenuProcedencias(object s, RoutedEventArgs e)   => AbrirVentana(new ProcedenciasWindow(),   "Procedencias");
    private void OnMenuSecciones(object s, RoutedEventArgs e)      => AbrirVentana(new SeccionesWindow(),      "Secciones");
    private void OnMenuLocales(object s, RoutedEventArgs e)        => AbrirVentana(new LocalesWindow(),        "Locales / Sucursales");
    private void OnMenuReimprimirComprobantes(object s, RoutedEventArgs e)
        => AbrirVentana(new CrediSoft.UI.Views.Shared.ReimprimirComprobantesWindow(), "Reimprimir Comprobantes");
    private async void OnMenuFuncionarios(object s, RoutedEventArgs e)
    {
        if (!await ConfirmarAdministrador()) return;
        AbrirVentana(new FuncionariosWindow(), "Funcionarios");
    }

    // VENTAS
    private void OnMenuVentaCredito(object s, RoutedEventArgs e)      => AbrirVentana(new VisorSolicitudesWindow(), "Ventas a crédito");
    private void OnMenuVentaContado(object s, RoutedEventArgs e)      => AbrirVentana(new VentaContadoWindow(),     "Venta al contado");
    private void OnMenuIngresarSolicitud(object s, RoutedEventArgs e) => AbrirVentana(new VentaCreditoWindow(),     "Ingresar solicitud de crédito");
    private void OnMenuVisorSolicitudes(object s, RoutedEventArgs e)  => AbrirVentana(new VisorSolicitudesWindow(), "Visor de solicitudes");

    // Abre la ficha de verificación de una solicitud directamente desde la tabla
    // del dashboard, con el mismo detalle que se ve en el Visor de Solicitudes.
    private async void OnLstSolicitudesDoubleClick(object s, MouseButtonEventArgs e)
    {
        if (LstSolicitudes.SelectedItem is not DashSolicitud sel || sel.IdSolicitud <= 0) return;

        try
        {
            using var conn = _db.Create();
            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 cl.NOMBRE_CLIENTE as NomCli, u.NOMBRE_USUARIO as NomVend, " +
                "l.NOMBRE as NomLocal, s.ID_LOCAL as IdLocal, s.TOTALSALE as Total, s.TOTALENTREGA as Entrega, " +
                "s.CANTCUOTAS as Cuotas, s.ESTADO as EstadoNum, " +
                "CASE WHEN EXISTS (SELECT 1 FROM CABECERA_SALES cs WHERE cs.NSOLICITUD = s.NUMERO) THEN 1 ELSE 0 END as VentaGenerada " +
                "FROM CAB_SOL_SALES s " +
                "LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE " +
                "LEFT JOIN USUARIOS u  ON s.ID_USUARIO = u.ID_USUARIO " +
                "LEFT JOIN LOCALES  l  ON s.ID_LOCAL   = l.ID_LOCAL " +
                "WHERE s.IDSOLICITUD = @id", new { id = sel.IdSolicitud });

            // Sin esto, una solicitud abierta desde el DASHBOARD (a diferencia del Visor de
            // Solicitudes, que sí revalida VentaGenerada en AbrirDetalle) siempre llegaba con
            // VentaGenerada=false por defecto, sin importar si ya tenía una venta generada —
            // el botón "Confirmar Entrega" quedaba habilitado para siempre, permitiendo
            // reconfirmar y duplicar la venta cada vez que se reabría desde acá (caso real:
            // Fátima/Buena Vista, 3 ventas del mismo celular generadas así, 21/07).
            var item = new SolicitudItem
            {
                IdSolicitud    = sel.IdSolicitud,
                IdLocal        = cab != null ? (int?)cab.IdLocal     ?? 0 : 0,
                Numero         = sel.Numero,
                LocalNombre    = cab != null ? (string?)cab.NomLocal ?? sel.LocalNombre : sel.LocalNombre,
                ClienteNombre  = cab != null ? (string?)cab.NomCli   ?? sel.ClienteNombre : sel.ClienteNombre,
                VendedorNombre = cab != null ? (string?)cab.NomVend  ?? "—" : "—",
                Estado         = sel.Estado,
                FechaSolicitud = sel.FechaSolicitud,
                TotalVenta     = cab != null ? (decimal?)cab.Total   ?? 0 : 0,
                Entrega        = cab != null ? (decimal?)cab.Entrega ?? 0 : 0,
                Cuotas         = cab != null ? (int?)cab.Cuotas      ?? 0 : 0,
                EstadoNum      = cab != null ? (byte?)cab.EstadoNum  ?? 0 : (byte)0,
                VentaGenerada  = cab != null && (byte?)cab.VentaGenerada == 1,
            };

            var estadoAntes = item.EstadoNum;
            var ventaGeneradaAntes = item.VentaGenerada;
            // Show() en vez de ShowDialog() — mismo motivo que en VisorSolicitudesWindow.
            // AbrirDetalle: ShowDialog() dejaba TODO el sistema bloqueado (menú, dashboard,
            // cualquier otro módulo) hasta cerrar esta ficha — este era el camino real que
            // reportó el bloqueo (abierta desde el panel "SOLICITUDES DE CRÉDITO" del
            // dashboard). Owner=this SÍ hace falta (con Show() no bloquea nada): sin él, la
            // ventana queda "huérfana" y Windows la minimiza sola en cuanto otra ventana del
            // mismo proceso toma el foco (bug real: se minimizaba espontáneamente al abrir
            // cualquier otro módulo del menú). Costo conocido: esta ficha no puede pasar "por
            // encima" de MainWindow con un clic — aceptado a cambio de que no se minimice sola.
            var w = new DetalleSolicitudWindow(item, _db) { Owner = this };
            w.Closed += async (_, _) =>
            {
                // Sin este Activate() explícito, cerrar la ficha (con foco en ella) a veces
                // deja el proceso sin ninguna ventana activa y Windows minimiza TODO el
                // programa (bug real reportado: "abro una solicitud y pulso cerrar, se
                // minimiza todo") — no hay garantía de que WPF le devuelva el foco al Owner
                // solo por cerrarse la ventana hija.
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                if (item.EstadoNum != estadoAntes || item.VentaGenerada != ventaGeneradaAntes)
                    await CargarDashboardAsync();
            };
            w.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir la solicitud: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // COBROS
    private void OnMenuListarCuotas(object s, RoutedEventArgs e)
    {
        var w = new CobrosWindow();
        w.ConfigurarAbrirModalAlCargar();
        AbrirVentana(w, "Listar cuotas");
    }
    private void OnMenuCobrarCuota(object s, RoutedEventArgs e) => AbrirVentana(new CobrosWindow(), "Cobrar cuota");
    private void OnMenuAsignarCobranzas(object s, RoutedEventArgs e) => AbrirVentana(new CobranzaAsignacionesWindow(), "Asignar cobrador");

    private void OnMenuDescuentoCuota(object s, RoutedEventArgs e) =>
        AbrirVentana(new CrediSoft.UI.Views.Cobros.DescuentoCuotaWindow(), "Descuento por Nota de Crédito");

    // Doble clic en una fila de "Cuotas próximas" del dashboard → abre Cobrar Cuota
    // directo con el cliente y su cuota ya autocompletados (sin buscar por C.I./RUC).
    private void OnLstCuotasDoubleClick(object s, MouseButtonEventArgs e)
    {
        if (LstCuotas.SelectedItem is not DashCuota sel || string.IsNullOrWhiteSpace(sel.ClienteCi)) return;

        var w = new CobrosWindow();
        w.ConfigurarAbrirCuotaEspecifica(sel.IdGeneradas, sel.ClienteCi);
        AbrirVentana(w, "Cobrar cuota");
    }

    // COMPRAS
    private void OnMenuNuevaCompra(object s, RoutedEventArgs e)   => AbrirVentana(new NuevaCompraWindow(),                               "Nueva Compra");
    // Editar/aprobar compras pendientes queda restringido a Administrador + usuario código 67
    // (mismo criterio que PuedeVerTodosLosLocales) — pedido explícito, dado que esta pantalla
    // ahora también permite cambiar el local destino de la compra (afecta directamente a qué
    // local le suma el stock al aprobar) además de los precios de los artículos.
    private void OnMenuEditarCompras(object s, RoutedEventArgs e)
    {
        if (_session.UsuarioActual?.PuedeVerTodosLosLocales != true)
        {
            MessageBox.Show("No tenés permiso para acceder a esta pantalla.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AbrirVentana(new CrediSoft.UI.Views.Compras.EditarComprasWindow(), "Modificar compras");
    }
    private void OnToolbarCompras(object s, RoutedEventArgs e)    => AbrirContextMenu(s);

    // TRANSFERENCIAS
    private void OnMenuNuevaTransferencia(object s, RoutedEventArgs e)   => AbrirVentana(new NuevaTransferenciaWindow(),   "Nueva Transferencia");
    private void OnMenuAceptarTransferencia(object s, RoutedEventArgs e) => AbrirVentana(new AceptarTransferenciaWindow(), "Recibir Transferencia");
    private void OnToolbarTransferencias(object s, RoutedEventArgs e)    => AbrirContextMenu(s);
    private void OnToolbarVentas(object s, RoutedEventArgs e)            => AbrirContextMenu(s);

    // CAJA
    private void OnMenuCajaApertura(object s, RoutedEventArgs e)    => AbrirVentana(new CajaAperturaWindow(),    "Apertura de Caja");
    private void OnMenuCajaCierre(object s, RoutedEventArgs e)      => AbrirVentana(new CajaCierreWindow(),      "Cierre de Caja");
    private void OnMenuCajaArqueo(object s, RoutedEventArgs e)      => AbrirVentana(new CajaArqueoWindow(),      "Arqueo de Caja");
    private void OnMenuCajaGastos(object s, RoutedEventArgs e)      => AbrirVentana(new CajaGastosWindow(),      "Gastos de Caja");
    private void OnMenuGastoCajaAntiguo(object s, RoutedEventArgs e)=> AbrirVentana(new CajaGastosWindow(),      "Gasto caja antiguo");
    private void OnMenuCajaRegistrar(object s, RoutedEventArgs e)   => AbrirVentana(new CajaRegistrarWindow(),   "Registrar movimiento");
    private void OnMenuCajaHistorial(object s, RoutedEventArgs e)   => AbrirVentana(new CajaHistorialWindow(),   "Historial de Caja");
    private void OnMenuCajaComprobantesPendientes(object s, RoutedEventArgs e)
        => AbrirVentana(new CajaComprobantesPendientesWindow(), "Comprobantes de Depósito Pendientes");

    // INFORMES
    private void OnMenuActivos(object s, RoutedEventArgs e)           => AbrirVentana(new ActivosWindow(),           "Créditos Activos");
    private void OnMenuAtrasos(object s, RoutedEventArgs e)           => AbrirVentana(new AtrasosWindow(),           "Atrasos");
    private void OnMenuHCobranzas(object s, RoutedEventArgs e)        => AbrirVentana(new HCobranzasWindow(),        "Historial de Cobranzas");
    private async void OnMenuHCompras(object s, RoutedEventArgs e)
    {
        if (!await ConfirmarAdministrador()) return;
        AbrirVentana(new HComprasWindow(), "Historial de Compras");
    }
    private void OnMenuHCreditos(object s, RoutedEventArgs e)         => AbrirVentana(new HCreditosWindow(),         "Historial de Créditos");
    private void OnMenuEnPromocion(object s, RoutedEventArgs e)       => AbrirVentana(new EnPromocionWindow(),       "En Promoción");
    private void OnMenuGestionPromo(object s, RoutedEventArgs e)      => AbrirVentana(new GestionPromocionesWindow(),"Gestión de Promociones");
    private void OnMenuHNotaCredito(object s, RoutedEventArgs e)      => AbrirVentana(new HNotaCreditoWindow(),      "H. Nota de crédito");
    private void OnMenuMovArt(object s, RoutedEventArgs e)            => AbrirVentana(new MovArtWindow(),            "Movimiento de artículos");
    private void OnMenuPendientes(object s, RoutedEventArgs e)        => AbrirVentana(new PendientesWindow(),        "Cobros Pendientes");
    private void OnMenuHTransferencias(object s, RoutedEventArgs e)   => AbrirVentana(new HTransferenciasWindow(),   "Historial de Transferencias");
    private void OnMenuHVentas(object s, RoutedEventArgs e)           => AbrirVentana(new HVentasWindow(),           "Historial de Ventas");
    private void OnMenuVerArticulos(object s, RoutedEventArgs e)      => AbrirVentana(new VerArticulosWindow(),      "Ver Artículos");
    private void OnMenuVerArticulosListado(object s, RoutedEventArgs e) => AbrirVentana(new VerArticulosListadoWindow(), "Ver Artículos");
    private void OnMenuVisorPromo(object s, RoutedEventArgs e)        => AbrirVentana(new VisorPromoWindow(),        "Visor Promociones");
    private void OnPromoDropdown(object s, RoutedEventArgs e)         => AbrirContextMenu(s);

    // HERRAMIENTAS
    private void OnMenuActualizarCajasCerradas(object s, RoutedEventArgs e) => AbrirVentana(new ActualizarCajasCerradasWindow(), "Actualizar Cajas Cerradas");
    private void OnMenuBloquearTransf(object s, RoutedEventArgs e)    => AbrirVentana(new BloquearTransfWindow(),        "Bloquear transferencias");
    private void OnMenuEditarCuota(object s, RoutedEventArgs e)       => AbrirVentana(new EditarCuotaWindow(),           "Editar cuota pagada");
    private void OnMenuEliminarVentaCont(object s, RoutedEventArgs e) => AbrirVentana(new EliminarVentaContadoWindow(),  "Eliminar Venta al Contado");
    private void OnMenuEliminarVentaCred(object s, RoutedEventArgs e) => AbrirVentana(new EliminarVentaCreditoWindow(), "Eliminar Venta a Crédito");
    private void OnMenuFinalizarPromo(object s, RoutedEventArgs e)    => AbrirVentana(new FinalizarPromoWindow(),        "Finalizar Promoción");
    private void OnMenuImpresoras(object s, RoutedEventArgs e)        => AbrirVentana(new ImpressorasWindow(),           "Impresoras");
    private void OnMenuNotaCredito(object s, RoutedEventArgs e)       => AbrirVentana(new NotaCreditoWindow(),           "Nota de Crédito");
    private void OnMenuPromocion(object s, RoutedEventArgs e)         => AbrirVentana(new GestionPromocionesWindow(),    "Promoción");
    private void OnMenuPunitorio(object s, RoutedEventArgs e)         => AbrirVentana(new PunitorioWindow(),             "Punitorio");
    private void OnMenuGenerarPagos(object s, RoutedEventArgs e)      => AbrirVentana(new PagosWindow(),                 "Pago de Salarios");
    private void OnMenuEditarPagos(object s, RoutedEventArgs e)       => AbrirVentana(new EditarPagoWindow(),            "Editar Pago");
    private void OnMenuEliminarPago(object s, RoutedEventArgs e)      => AbrirVentana(new CrediSoft.UI.Views.Pagos.EliminarPagoWindow(), "Eliminar Pago");
    // PagoRemuneracionesWindow eliminado del menú — reemplazado por el flujo en PagosWindow (un solo paso)
    private void OnMenuRetiroLibre(object s, RoutedEventArgs e)       => AbrirVentana(new CrediSoft.UI.Views.Retiros.RetiroLibreWindow(), "Retiro libre");
    private void OnMenuMultas(object s, RoutedEventArgs e)
    {
        if (_session.UsuarioActual?.PuedeVerTodosLosLocales != true)
        {
            MessageBox.Show("No tenés permiso para acceder a esta pantalla.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AbrirVentana(new CrediSoft.UI.Views.Retiros.MultasWindow(), "Multas a funcionarios");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void AbrirContextMenu(object s)
    {
        if (s is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnMenuAcercaDe(object s, RoutedEventArgs e) =>
        MessageBox.Show("ElectroMar v2.0\nCredimar S.A. Electrodomésticos\n\nSistema de Gestión Comercial",
                        "Acerca de...", MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnMenuSalir(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("¿Desea salir del sistema?", "Salir",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            Application.Current.Shutdown();
    }

    // A diferencia de "Salir" (cierra toda la app), esto vuelve a la pantalla de Login sin
    // matar el proceso — para el caso de un turno compartido (varios vendedores usan el mismo
    // equipo/caja) donde cerrar y reabrir ElectroMar entero cada vez era mucho más lento que
    // solo cambiar de usuario.
    private void OnMenuCerrarSesion(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("¿Desea cerrar la sesión actual?", "Cerrar sesión",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SessionService.Instance.CerrarSesion();
        var login = new CrediSoft.UI.Views.Login.LoginWindow();
        login.Show();
        Close();
    }

    // ── Confirmación de administrador ─────────────────────────────────────────
    private Task<bool> ConfirmarAdministrador()
    {
        var modal = new CrediSoft.UI.Views.Shared.AutorizacionAdminModal(
            "Solo un administrador puede autorizar esta operación",
            "PERMISO_COMPRAS") { Owner = this };
        return Task.FromResult(modal.ShowDialog() == true);
    }
}
