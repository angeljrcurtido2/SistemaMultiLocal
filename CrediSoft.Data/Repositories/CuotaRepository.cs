using CrediSoft.Core.Models;
using Dapper;
using System.Data;
using System.Net;

namespace CrediSoft.Data.Repositories;

public interface ICuotaRepository
{
    Task<IEnumerable<Cuota>> BuscarTodasPorClienteAsync(int idCliente);
    Task<IEnumerable<Cuota>> BuscarTodasPorCiAsync(string ci);
    Task<IEnumerable<Cuota>> BuscarPendientesTodosAsync(int? idLocal = null);
    Task<IEnumerable<Cuota>> BuscarAtrasosAsync(int? local = null, int? diasMinimos = null);
    Task<IEnumerable<AsignacionCobranza>> ListarAsignacionesCobranzaAsync(int? idCobrador = null);
    Task<AsignacionCobranza?> ObtenerAsignacionCuotaAsync(int idCab, int idGeneradas);
    Task<bool> AsignarCobranzaCreditoAsync(int idCab, int idCobrador, int idUsuarioRegistro);
    Task<bool> AsignarCobranzaCuotaAsync(int idGeneradas, int idCobrador, int idUsuarioRegistro);
    Task<bool> QuitarAsignacionCobranzaAsync(int? idCab = null, int? idGeneradas = null);
    Task<bool> CobrarCuotaAsync(CobrarCuotaParams p);
    Task<(bool Ok, bool PagoCompleto, string Msg)> CobrarCuotaParcialAsync(CobrarCuotaParcialParams p);
    Task<decimal> CalcularPunitoriocAsync(int idGeneradas);
    Task<decimal> ObtenerTasaPunitiorioAsync();
    Task<decimal> ObtenerValorInforconfAsync();
    Task<bool> CorrespondeCargoInforconfAsync(int idCliente, int idGeneradas);
    Task MarcarInforconfAplicadoAsync(int idGeneradas);
    // Historial por CUOTA (una fila por cuota, no por pago) — incluye Fecha de pago real
    // (GENERADAS.FECHACOBRADO, cobertura 100% de las cuotas cobradas, verificado contra
    // AUDITORIA) y los días entre el vencimiento y esa fecha de pago (negativo = pagó antes,
    // 0 = el mismo día, positivo = con atraso). Consulta directa a GENERADAS, no el SP legado
    // HISTORIAL_GENERADAS_CLIE_CS (que no trae FECHACOBRADO en absoluto).
    Task<IEnumerable<(byte NCuota, decimal Monto, string Vto, string Estado, int Mora, string Obs, string? FechaPago, int? DiasVtoAPago)>> ObtenerHistorialAsync(int idCab);
    Task<IEnumerable<(string Descripcion, decimal Cantidad, decimal PVenta)>> ObtenerArticulosAsync(int idCab);
    Task<bool> EliminarVentaCreditoAsync(EliminarVentaParams p);
    // Pagos reales registrados en CAJA_DETALLE para una cuota — a diferencia de GENERADAS
    // (que solo guarda el ESTADO FINAL: una fila con Entrega/Total ya acumulados, sin rastro
    // de abonos parciales previos), CAJA_DETALLE tiene una fila por cada cobro/abono real que
    // entró a caja, con fecha, monto y forma de pago. Filtra por CONCEPTO porque no existe un
    // vínculo directo IDGENERADAS en CAJA_DETALLE (ver CobrarCuotaAsync/CobrarCuotaParcialAsync,
    // que arman el CONCEPTO siempre con el mismo patrón "...COMPROBANTE: {n}...").
    Task<IEnumerable<PagoCuotaRow>> ObtenerPagosCuotaAsync(string comprobante, byte nCuota);

    // Descuento por Nota de Crédito aplicado de antemano a una cuota puntual (ver
    // DescuentoCuotaWindow, restringida a Administrador/código 67) — queda guardado en
    // DESCUENTOS_CUOTA y lo ve/aplica automáticamente cualquier cajero de cualquier local al
    // cobrar esa cuota, como una línea "Descuento NC: -X" aparte del total original.
    Task<DescuentoCuotaRow?> ObtenerDescuentoPendienteAsync(int idGeneradas);
    Task<bool> CrearDescuentoCuotaAsync(int idGeneradas, decimal monto, string? motivo, string? nroNotaCredito, int idUsuarioCreador);
}

public record DescuentoCuotaRow(int IdDescuento, int IdGeneradas, decimal Monto, string? Motivo, string? NroNotaCredito, DateTime FechaCreacion);

public record PagoCuotaRow(DateTime Fecha, decimal Monto, string FormaPago, string Concepto);

// IdUsuario = a quién se le acredita la comisión de cobranza (GENERADAS.IDU) — puede ser
// distinto del usuario logueado si el cobro se registra "a nombre de otro vendedor" (ver
// CobrosWindow, badge "Cobrado por"). IdUsuarioSesion = quién realmente está operando el
// sistema/la caja (CAJA_DETALLE.ID_CAJERO + auditoría) — default null usa IdUsuario, para
// no romper llamadores que cobran normalmente (sin vendedor alternativo).
public record CobrarCuotaParams(
    int IdCab, int IdGeneradas, byte NCuota, string Comprobante,
    decimal MontoCuota, int Mora, decimal Punitorio, decimal Total,
    int IdUsuario, byte IdLocal, int IdCajaFisica,
    decimal EntregaAnterior, byte Inforcom, byte ElEstadoCab,
    string FormaPago = "EFECTIVO", string Referencia = "",
    string Obs = "", decimal Reajuste = 0m, int? IdUsuarioSesion = null);

public record CobrarCuotaParcialParams(
    int IdCab, int IdGeneradas, byte NCuota, string Comprobante,
    decimal MontoCuota, int Mora, decimal Punitorio, decimal Reajuste,
    decimal TotalCuota, decimal MontoPagado,
    int IdUsuario, byte IdLocal, int IdCajaFisica,
    decimal EntregaAnterior, byte Inforcom,
    string FormaPago = "EFECTIVO", string Referencia = "",
    string Obs = "", int? IdUsuarioSesion = null);

public class CuotaRepository : ICuotaRepository
{
    private readonly IDbConnectionFactory _factory;
    public CuotaRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string TablaAsignaciones = "COBRANZA_ASIGNACIONES";
    private static readonly object _schemaLock = new();
    private static bool _schemaVerificado = false;

    public async Task<IEnumerable<Cuota>> BuscarPendientesTodosAsync(int? idLocal = null)
    {
        using var conn = _factory.Create();
        var sql =
            "SELECT TOP 2000 G.IDGENERADAS, G.IDCAB, G.COMPROBANTE, G.NCUOTA, G.MONTO, " +
            $"G.VTO, G.FECHACOBRADO, G.MORA, {PunitorioRecalculado} AS PUNITORIO, G.REAJUSTE, G.TOTAL, " +
            "G.IDLOCAL, G.IDU, G.OBS, G.ESTADO, G.ENTREGA, G.INFORCOM_APLICADO AS InforcomAplicado, " +
            "C.NOMBRE_CLIENTE as ClienteNombre, C.CI_CLIENTE as ClienteCi, " +
            "C.TELEFONO_CLIENTE as ClienteTelefono, CB.NSOLICITUD, " +
            "CB.TOTAL as CabTotal, CB.HABER as CabHaber, " +
            "U.NOMBRE_USUARIO as VendedorNombre, L.NOMBRE as LocalNombre, " +
            "(SELECT MAX(G2.NCUOTA) FROM GENERADAS G2 WHERE G2.IDCAB = G.IDCAB) as TotalCuotasCredito " +
            "FROM GENERADAS G " +
            "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
            "INNER JOIN CLIENTES C ON CB.ID_CLIENTE = C.ID_CLIENTE " +
            "LEFT JOIN USUARIOS U ON CB.ID_USUARIO = U.ID_USUARIO " +
            "LEFT JOIN LOCALES L ON G.IDLOCAL = L.ID_LOCAL " +
            "WHERE G.ESTADO = 0 " +
            (idLocal.HasValue ? "AND G.IDLOCAL = @Local " : "") +
            "ORDER BY G.VTO";
        return await conn.QueryAsync<Cuota>(sql, new { Local = idLocal });
    }

    // Punitorio recalculado en vivo (misma fórmula que CalcularPunitoriocAsync) en vez del
    // valor crudo persistido en GENERADAS.PUNITORIO — ese campo queda en 0 tras anular un
    // cobro (Anular() en el Explorador de Caja lo resetea junto con ESTADO/MORA), y sin este
    // recálculo la grilla de "Cuotas pendientes" mostraba Punitorio=0 aunque la cuota siguiera
    // vencida, mientras el panel de detalle (que sí recalcula) mostraba el monto real — dos
    // valores distintos para la misma cuota en la misma pantalla.
    private const string PunitorioRecalculado =
        "CASE WHEN G.VTO >= CAST(GETDATE() AS DATE) THEN 0 ELSE " +
        "  ROUND(G.MONTO * (ISNULL((SELECT TOP 1 VALOR_PUNITORIO FROM CONFIGURACION), 0) / 100.0) / 25.0 * " +
        "    (CASE WHEN (DATEDIFF(day, G.VTO, GETDATE()) - 5) < 0 THEN 0 ELSE (DATEDIFF(day, G.VTO, GETDATE()) - 5) END), 0) " +
        "END";

    // Trae TODAS las cuotas del cliente (pendientes, vencidas y ya cobradas/canceladas,
    // incluida la cuota 1 = entrega) — pedido explícito para que la grilla de Cobrar Cuota
    // coincida con el sistema viejo, donde el cajero siempre vio el detalle completo del
    // crédito, no solo lo pendiente.
    public async Task<IEnumerable<Cuota>> BuscarTodasPorClienteAsync(int idCliente)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Cuota>(
            "SELECT G.IDGENERADAS, G.IDCAB, G.COMPROBANTE, G.NCUOTA, G.MONTO, " +
            $"G.VTO, G.FECHACOBRADO, G.MORA, {PunitorioRecalculado} AS PUNITORIO, G.REAJUSTE, G.TOTAL, " +
            "G.IDLOCAL, G.IDU, G.OBS, G.ESTADO, G.ENTREGA, G.INFORCOM_APLICADO AS InforcomAplicado, " +
            "C.NOMBRE_CLIENTE as ClienteNombre, C.CI_CLIENTE as ClienteCi, " +
            "C.TELEFONO_CLIENTE as ClienteTelefono, CB.NSOLICITUD, " +
            "CB.TOTAL as CabTotal, CB.HABER as CabHaber, " +
            "(SELECT MAX(G2.NCUOTA) FROM GENERADAS G2 WHERE G2.IDCAB = G.IDCAB) as TotalCuotasCredito " +
            "FROM GENERADAS G " +
            "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
            "INNER JOIN CLIENTES C ON CB.ID_CLIENTE = C.ID_CLIENTE " +
            "WHERE CB.ID_CLIENTE = @IdCliente " +
            "ORDER BY G.VTO",
            new { IdCliente = idCliente });
    }

    public async Task<IEnumerable<Cuota>> BuscarTodasPorCiAsync(string ci)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Cuota>(
            "SELECT G.IDGENERADAS, G.IDCAB, G.COMPROBANTE, G.NCUOTA, G.MONTO, " +
            $"G.VTO, G.FECHACOBRADO, G.MORA, {PunitorioRecalculado} AS PUNITORIO, G.REAJUSTE, G.TOTAL, " +
            "G.IDLOCAL, G.IDU, G.OBS, G.ESTADO, G.ENTREGA, G.INFORCOM_APLICADO AS InforcomAplicado, " +
            "C.NOMBRE_CLIENTE as ClienteNombre, C.CI_CLIENTE as ClienteCi, " +
            "C.TELEFONO_CLIENTE as ClienteTelefono, CB.NSOLICITUD, " +
            "CB.TOTAL as CabTotal, CB.HABER as CabHaber, " +
            "(SELECT MAX(G2.NCUOTA) FROM GENERADAS G2 WHERE G2.IDCAB = G.IDCAB) as TotalCuotasCredito " +
            "FROM GENERADAS G " +
            "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
            "INNER JOIN CLIENTES C ON CB.ID_CLIENTE = C.ID_CLIENTE " +
            "WHERE C.CI_CLIENTE = @Ci " +
            "ORDER BY G.VTO",
            new { Ci = ci });
    }

    // Réplica de LISTA_ATRASOS_CS_2026 pero SIN el filtro "AND G.VTO < GETDATE()" del SP
    // legado (no se toca el SP: es compartido con CrediMar.exe/VB6) — el sistema viejo SÍ
    // muestra cuotas con vencimiento futuro con MORA negativa (confirmado por captura real:
    // ACHUCARRO LUIS ALBERTO, vto 30/07/2026, Mora -3), cosa que el SP nunca puede devolver
    // porque excluye esas filas de raíz. Se agrega además CI_CLIENTE (no está en el SP).
    public async Task<IEnumerable<Cuota>> BuscarAtrasosAsync(int? local = null, int? diasMinimos = null)
    {
        using var conn = _factory.Create();
        var sql =
            "SELECT CB.IDCAB, C.NOMBRE_CLIENTE AS Cliente, C.CI_CLIENTE AS ClienteCi, " +
            "C.TELEFONO_CLIENTE AS Telefono, CB.NSOLICITUD AS Solicitud, " +
            "G.NCUOTA AS NCuota, G.MONTO AS Monto, G.VTO AS Vto, " +
            "DATEDIFF(day, G.VTO, CAST(GETDATE() AS DATE)) - 5 AS Mora, " +
            "U.NOMBRE_USUARIO AS Vendedor, CB.ID_LOCAL AS IdLocal " +
            "FROM CLIENTES C " +
            "INNER JOIN CABECERA_SALES CB ON C.ID_CLIENTE = CB.ID_CLIENTE " +
            "INNER JOIN GENERADAS G ON CB.IDCAB = G.IDCAB " +
            "INNER JOIN USUARIOS U ON CB.ID_USUARIO = U.ID_USUARIO " +
            "WHERE G.ESTADO = 0 " +
            "ORDER BY C.CI_CLIENTE";

        var rows = (await conn.QueryAsync<dynamic>(sql, commandTimeout: 60)).ToList();

        return rows.Select(r =>
        {
            var dict = (IDictionary<string, object>)r;
            var vto = dict.TryGetValue("Vto", out var vtoVal) && vtoVal is DateTime vtoDate ? vtoDate : DateTime.Today;
            var mora = dict.TryGetValue("Mora", out var mor) ? Convert.ToInt32(mor ?? 0) : 0;
            return new Cuota
            {
                IdCab           = dict.TryGetValue("IDCAB",    out var cab)  ? Convert.ToInt32(cab)   : 0,
                NSolicitud      = dict.TryGetValue("Solicitud", out var sol) ? sol?.ToString()  ?? "" : "",
                ClienteNombre   = dict.TryGetValue("Cliente",  out var cli)  ? cli?.ToString()  ?? "" : "",
                ClienteCi       = dict.TryGetValue("ClienteCi", out var ci)  ? ci?.ToString()   ?? "" : "",
                ClienteTelefono = dict.TryGetValue("Telefono", out var tel)  ? tel?.ToString()  ?? "" : "",
                NCuota          = dict.TryGetValue("NCuota",   out var nc)   ? Convert.ToByte(nc  ?? 0) : (byte)0,
                Monto           = dict.TryGetValue("Monto",    out var mon)  ? Convert.ToDecimal(mon ?? 0) : 0,
                Mora            = mora,
                Vto             = vto,
                VendedorNombre  = dict.TryGetValue("Vendedor", out var vend) ? vend?.ToString() ?? "" : "",
                IdLocal         = dict.TryGetValue("IdLocal",  out var loc)  ? Convert.ToByte(loc  ?? 0) : (byte)0,
                Estado          = 0,
            };
        })
        .Where(c => (!local.HasValue || c.IdLocal == local.Value) &&
                    (!diasMinimos.HasValue || c.Mora >= diasMinimos.Value))
        .ToList();
    }

    public async Task<IEnumerable<AsignacionCobranza>> ListarAsignacionesCobranzaAsync(int? idCobrador = null)
    {
        using var conn = _factory.Create();
        await AsegurarTablaAsignacionesAsync(conn);

        var sql = $@"
            SELECT
                A.IDASIGNACION AS IdAsignacion,
                A.IDCAB AS IdCab,
                A.IDGENERADAS AS IdGeneradas,
                CS.ID_CLIENTE AS IdCliente,
                ISNULL(CL.NOMBRE_CLIENTE, '') AS ClienteNombre,
                ISNULL(CL.CI_CLIENTE, '') AS ClienteCi,
                ISNULL(L.NOMBRE, '') AS LocalNombre,
                ISNULL(CS.NSOLICITUD, '') AS NSolicitud,
                G.NCUOTA AS NCuota,
                G.VTO AS Vto,
                G.ESTADO AS EstadoCuota,
                G.MONTO AS MontoCuota,
                A.IDCOBRADOR AS IdCobrador,
                ISNULL(UC.NOMBRE_USUARIO, '') AS CobradorNombre,
                A.IDUSUARIO_REGISTRO AS IdUsuarioRegistro,
                ISNULL(UR.NOMBRE_USUARIO, '') AS UsuarioRegistroNombre,
                A.FECHA_ASIGNACION AS FechaAsignacion,
                A.ACTIVO AS Activo
            FROM {TablaAsignaciones} A
            INNER JOIN USUARIOS UC ON UC.ID_USUARIO = A.IDCOBRADOR
            LEFT JOIN USUARIOS UR ON UR.ID_USUARIO = A.IDUSUARIO_REGISTRO
            LEFT JOIN CABECERA_SALES CS ON CS.IDCAB = A.IDCAB
            LEFT JOIN CLIENTES CL ON CL.ID_CLIENTE = CS.ID_CLIENTE
            LEFT JOIN LOCALES L ON L.ID_LOCAL = CS.ID_LOCAL
            LEFT JOIN GENERADAS G ON G.IDGENERADAS = A.IDGENERADAS
            WHERE A.ACTIVO = 1
              AND (@IdCobrador IS NULL OR A.IDCOBRADOR = @IdCobrador)
            ORDER BY A.FECHA_ASIGNACION DESC, A.IDASIGNACION DESC";

        return await conn.QueryAsync<AsignacionCobranza>(sql, new { IdCobrador = idCobrador });
    }

    // Resuelve la asignación EFECTIVA de una cuota puntual: una asignación a nivel "Cuota"
    // (IDGENERADAS = @IdGeneradas) tiene prioridad sobre la del crédito completo
    // (IDGENERADAS IS NULL, IDCAB = @IdCab) — mismo criterio que CobranzaAsignacionesWindow
    // usa en memoria (ObtenerAsignacion). Se usa en CobrosWindow para bloquear el cobro de
    // una cuota asignada a otro cobrador (ver OnCobrar).
    public async Task<AsignacionCobranza?> ObtenerAsignacionCuotaAsync(int idCab, int idGeneradas)
    {
        using var conn = _factory.Create();
        await AsegurarTablaAsignacionesAsync(conn);

        var sql = $@"
            SELECT TOP 1
                A.IDASIGNACION AS IdAsignacion,
                A.IDCAB AS IdCab,
                A.IDGENERADAS AS IdGeneradas,
                A.IDCOBRADOR AS IdCobrador,
                ISNULL(UC.NOMBRE_USUARIO, '') AS CobradorNombre,
                A.FECHA_ASIGNACION AS FechaAsignacion,
                A.ACTIVO AS Activo
            FROM {TablaAsignaciones} A
            INNER JOIN USUARIOS UC ON UC.ID_USUARIO = A.IDCOBRADOR
            WHERE A.ACTIVO = 1
              AND ((A.IDGENERADAS = @IdGeneradas) OR (A.IDGENERADAS IS NULL AND A.IDCAB = @IdCab))
            ORDER BY CASE WHEN A.IDGENERADAS IS NOT NULL THEN 0 ELSE 1 END, A.FECHA_ASIGNACION DESC";

        return await conn.QueryFirstOrDefaultAsync<AsignacionCobranza>(sql, new { IdCab = idCab, IdGeneradas = idGeneradas });
    }

    public async Task<bool> AsignarCobranzaCreditoAsync(int idCab, int idCobrador, int idUsuarioRegistro)
    {
        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await AsegurarTablaAsignacionesAsync(conn, tx);

            await conn.ExecuteAsync(
                $@"UPDATE {TablaAsignaciones}
                   SET ACTIVO = 0
                   WHERE ACTIVO = 1 AND IDCAB = @IdCab",
                new { IdCab = idCab }, tx);

            await conn.ExecuteAsync(
                $@"INSERT INTO {TablaAsignaciones}
                   (IDCAB, IDGENERADAS, IDCOBRADOR, IDUSUARIO_REGISTRO, FECHA_ASIGNACION, ACTIVO)
                   VALUES (@IdCab, NULL, @IdCobrador, @IdUsuarioRegistro, GETDATE(), 1)",
                new { IdCab = idCab, IdCobrador = idCobrador, IdUsuarioRegistro = idUsuarioRegistro }, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> AsignarCobranzaCuotaAsync(int idGeneradas, int idCobrador, int idUsuarioRegistro)
    {
        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await AsegurarTablaAsignacionesAsync(conn, tx);

            var idCab = await conn.ExecuteScalarAsync<int?>(
                "SELECT IDCAB FROM GENERADAS WHERE IDGENERADAS = @IdGeneradas",
                new { IdGeneradas = idGeneradas }, tx);
            if (idCab == null)
                throw new InvalidOperationException("No se encontró la cuota seleccionada.");

            await conn.ExecuteAsync(
                $@"UPDATE {TablaAsignaciones}
                   SET ACTIVO = 0
                   WHERE ACTIVO = 1 AND IDGENERADAS = @IdGeneradas",
                new { IdGeneradas = idGeneradas }, tx);

            await conn.ExecuteAsync(
                $@"INSERT INTO {TablaAsignaciones}
                   (IDCAB, IDGENERADAS, IDCOBRADOR, IDUSUARIO_REGISTRO, FECHA_ASIGNACION, ACTIVO)
                   VALUES (@IdCab, @IdGeneradas, @IdCobrador, @IdUsuarioRegistro, GETDATE(), 1)",
                new { IdCab = idCab.Value, IdGeneradas = idGeneradas, IdCobrador = idCobrador, IdUsuarioRegistro = idUsuarioRegistro }, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> QuitarAsignacionCobranzaAsync(int? idCab = null, int? idGeneradas = null)
    {
        if (!idCab.HasValue && !idGeneradas.HasValue)
            throw new ArgumentException("Debe indicarse un crédito o una cuota.");

        using var conn = _factory.Create();
        await AsegurarTablaAsignacionesAsync(conn);
        var where = new List<string>();
        var p = new DynamicParameters();
        if (idCab.HasValue)
        {
            where.Add("IDCAB = @IdCab");
            p.Add("@IdCab", idCab.Value);
        }
        if (idGeneradas.HasValue)
        {
            where.Add("IDGENERADAS = @IdGeneradas");
            p.Add("@IdGeneradas", idGeneradas.Value);
        }

        var sql = $@"UPDATE {TablaAsignaciones} SET ACTIVO = 0
                     WHERE ACTIVO = 1 AND {string.Join(" AND ", where)}";
        var filas = await conn.ExecuteAsync(sql, p);
        return filas > 0;
    }

    // Reemplaza EXEC sp_Guardar_Cobranza_Cs_2026 por INSERT/UPDATE directo — el SP legado en
    // esta base no tiene el parámetro @IdUsuarioSesion (error real: "has too many arguments
    // specified" al cobrar), y no se puede tocar el SP porque es compartido con CrediMar.exe
    // (VB6), que lo llama posicionalmente. Se replica acá la misma secuencia exacta del SP
    // (UPDATE CABECERA_SALES, UPDATE GENERADAS, UPDATE CLIENTES.INFORCOM si corresponde,
    // INSERT CAJA_DETALLE, registros de AUDITORIA) en una sola transacción.
    public async Task<bool> CobrarCuotaAsync(CobrarCuotaParams prm)
    {
        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await AsegurarTablaAsignacionesAsync(conn, tx);
            await ValidarAsignacionCobranzaAsync(conn, tx, prm.IdCab, prm.IdGeneradas, prm.IdUsuario);

            var nomMaquina = Dns.GetHostName();
            const string ipMaquina = "127.0.0.1";

            // Monto que realmente ingresa HOY a caja: el total de la cuota menos lo ya
            // entregado en abonos previos — evita acreditar dos veces un abono parcial ya
            // registrado (mismo criterio que el SP legado).
            var montoHoy = Math.Max(0, prm.Total - prm.EntregaAnterior);

            var cliente = await conn.QuerySingleAsync<(int IdCliente, string Nombre)>(
                "SELECT CS.ID_CLIENTE, ISNULL(LTRIM(RTRIM(CL.NOMBRE_CLIENTE)), '(Sin nombre)') " +
                "FROM CABECERA_SALES CS INNER JOIN CLIENTES CL ON CL.ID_CLIENTE = CS.ID_CLIENTE WHERE CS.IDCAB = @IdCab",
                new { prm.IdCab }, tx);
            var nombreCobrador = await conn.ExecuteScalarAsync<string>(
                "SELECT NOMBRE_USUARIO FROM USUARIOS WHERE ID_USUARIO = @IdUsuario", new { prm.IdUsuario }, tx) ?? "";

            var idUsuarioSesion = prm.IdUsuarioSesion ?? prm.IdUsuario;

            var antesCab = await conn.QuerySingleAsync<(decimal Haber, short Cpha, byte Estado)>(
                "SELECT ISNULL(HABER,0), ISNULL(CPHA,0), ISNULL(ESTADO,0) FROM CABECERA_SALES WHERE IDCAB = @IdCab",
                new { prm.IdCab }, tx);
            var filasCab = await conn.ExecuteAsync(
                "UPDATE CABECERA_SALES SET HABER = HABER + @MontoHoy, CPHA = CPHA + 1, ESTADO = @ElEstadoCab WHERE IDCAB = @IdCab",
                new { MontoHoy = montoHoy, prm.ElEstadoCab, prm.IdCab }, tx);
            if (filasCab == 0) { tx.Rollback(); return false; }
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CABECERA_SALES", prm.IdCab.ToString(), 'U', "HABER, CPHA, ESTADO",
                $"Haber: {antesCab.Haber} | CPHA: {antesCab.Cpha} | Estado: {antesCab.Estado}",
                $"Haber: {antesCab.Haber + montoHoy} | CPHA: {antesCab.Cpha + 1} | Estado: {prm.ElEstadoCab} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTAS", nomMaquina, ipMaquina);

            var antesGen = await conn.QuerySingleAsync<(int Mora, decimal Punitorio, decimal Reajuste, decimal Total, byte Estado)>(
                "SELECT ISNULL(MORA,0), ISNULL(PUNITORIO,0), ISNULL(REAJUSTE,0), ISNULL(TOTAL,0), ISNULL(ESTADO,0) FROM GENERADAS WHERE IDGENERADAS = @IdGeneradas",
                new { prm.IdGeneradas }, tx);
            var filasGen = await conn.ExecuteAsync(
                "UPDATE GENERADAS SET FECHACOBRADO=GETDATE(), MORA=@Mora, PUNITORIO=@Punitorio, REAJUSTE=@Reajuste, " +
                "TOTAL=@Total, ENTREGA=@Total, IDU=@IdUsuario, OBS=@Obs, ESTADO=@Estado WHERE IDGENERADAS=@IdGeneradas",
                new { prm.Mora, prm.Punitorio, prm.Reajuste, prm.Total, prm.IdUsuario, prm.Obs, Estado = (byte)1, prm.IdGeneradas }, tx);
            if (filasGen == 0) { tx.Rollback(); return false; }
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "GENERADAS", prm.IdGeneradas.ToString(), 'U', "VARIOS",
                $"Mora: {antesGen.Mora} | Punitorio: {antesGen.Punitorio} | Reajuste: {antesGen.Reajuste} | Total: {antesGen.Total} | Estado: {antesGen.Estado}",
                $"Comprobante: {prm.Comprobante} | Cuota Nº: {prm.NCuota} | Entrega anterior: {prm.EntregaAnterior} | Monto cobrado hoy: {montoHoy} | " +
                $"Mora: {prm.Mora} | Punitorio: {prm.Punitorio} | Reajuste: {prm.Reajuste} | Total: {prm.Total} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTAS", nomMaquina, ipMaquina);

            // Cuota cobrada: si tenía una asignación PUNTUAL propia, se desactiva — ya no
            // tiene sentido que siga apareciendo en "Asignaciones activas" (CobranzaAsignacionesWindow)
            // una cuota que ya está saldada. No se toca la asignación a nivel CRÉDITO completo
            // (IDGENERADAS IS NULL): el resto de las cuotas de ese crédito puede seguir
            // necesitando ese cobrador asignado.
            await AsegurarTablaAsignacionesAsync(conn, tx);
            await conn.ExecuteAsync(
                $"UPDATE {TablaAsignaciones} SET ACTIVO = 0 WHERE ACTIVO = 1 AND IDGENERADAS = @IdGeneradas",
                new { prm.IdGeneradas }, tx);

            if (prm.Inforcom == 1)
            {
                var antesInforcom = await conn.ExecuteScalarAsync<byte?>(
                    "SELECT ISNULL(INFORCOM,0) FROM CLIENTES WHERE ID_CLIENTE = @IdCliente", new { cliente.IdCliente }, tx) ?? 0;
                await conn.ExecuteAsync("UPDATE CLIENTES SET INFORCOM = @Inforcom WHERE ID_CLIENTE = @IdCliente",
                    new { prm.Inforcom, cliente.IdCliente }, tx);
                await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CLIENTES", cliente.IdCliente.ToString(), 'U', "INFORCOM",
                    $"Inforcom: {antesInforcom} | Cliente: {cliente.Nombre}",
                    $" Actualizar Inforconf a: {prm.Inforcom} | Cliente: {cliente.Nombre} | Motivo: Mora en cuota Nº {prm.NCuota} | Comprobante: {prm.Comprobante} | Usuario: {nombreCobrador}",
                    "COBRO CUOTAS", nomMaquina, ipMaquina);
            }

            var idMaster = await conn.ExecuteScalarAsync<int?>(
                "SELECT ID_MASTER FROM CAJA_MASTER WITH (UPDLOCK, HOLDLOCK) WHERE ID_LOCAL=@IdLocal AND ID_CAJA_FISICA=@IdCajaFisica AND ESTADO='A'",
                new { prm.IdLocal, prm.IdCajaFisica }, tx);
            if (idMaster == null) { tx.Rollback(); return false; }

            var concepto = $"COBRO CUOTA N°: {prm.NCuota} | COMPROBANTE: {prm.Comprobante} | CLIENTE: {cliente.Nombre}";
            var idDetalle = await conn.QuerySingleAsync<int>(
                // ID_CAJERO = quien realmente operaba la caja (IdUsuarioSesion); ID_VENDEDOR =
                // a quién se le acredita la comisión (IdUsuario, puede ser "a nombre de otro
                // vendedor") — antes estaban invertidos, ver comentario de CobrarCuotaParams.
                "INSERT INTO CAJA_DETALLE (ID_MASTER,ID_VENTA,ID_LOCAL,FECHA_HORA,TIPO,SUBTIPO,FORMA_PAGO,MONTO,ID_CAJERO,ID_ENTIDAD,CONCEPTO,REFERENCIA,ESTADO_REG,ID_VENDEDOR) " +
                "VALUES (@IdMaster,NULL,@IdLocal,GETDATE(),'I','COBRO_SISTEMA',@FormaPago,@MontoHoy,@IdUsuarioSesion,NULL,@Concepto,@Referencia,'V',@IdUsuario); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { IdMaster = idMaster, prm.IdLocal, prm.FormaPago, MontoHoy = montoHoy, prm.IdUsuario, Concepto = concepto, prm.Referencia, IdUsuarioSesion = idUsuarioSesion }, tx);
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CAJA_DETALLE", idDetalle.ToString(), 'I', "TODOS",
                "(NUEVO)",
                $"Cobro Cuota N°: {prm.NCuota} | Comprobante: {prm.Comprobante} | Monto cobrado hoy: {montoHoy} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTA", nomMaquina, ipMaquina);

            // Descuento por Nota de Crédito pendiente para esta cuota (si lo hay) se marca
            // usado DENTRO de esta misma transacción — si el cobro hace rollback por cualquier
            // motivo, el descuento sigue disponible para el próximo intento en vez de quedar
            // consumido en falso.
            await MarcarDescuentoSiExisteAsync(conn, tx, prm.IdGeneradas, prm.IdUsuario);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task MarcarDescuentoSiExisteAsync(IDbConnection conn, IDbTransaction tx, int idGeneradas, int idUsuario) =>
        await conn.ExecuteAsync(
            "UPDATE DESCUENTOS_CUOTA SET APLICADO = 1, FECHA_APLICADO = GETDATE(), ID_USUARIO_APLICO = @IdUsuario " +
            "WHERE IDGENERADAS = @IdGeneradas AND APLICADO = 0 AND ANULADO = 0",
            new { IdGeneradas = idGeneradas, IdUsuario = idUsuario }, tx);

    private static Task InsertarAuditoriaAsync(IDbConnection conn, IDbTransaction tx, int idUsuario, string tabla, string idRegistro,
        char operacion, string campo, string valorAntes, string valorDespues, string modulo, string nomMaquina, string ipMaquina) =>
        conn.ExecuteAsync(
            "INSERT INTO AUDITORIA (FECHA_HORA,ID_USUARIO,TABLA,ID_REGISTRO,OPERACION,CAMPO,VALOR_ANTES,VALOR_DESPUES,MODULO,NOM_MAQUINA,IP_MAQUINA) " +
            "VALUES (GETDATE(),@IdUsuario,@Tabla,@IdRegistro,@Operacion,@Campo,@ValorAntes,@ValorDespues,@Modulo,@NomMaquina,@IpMaquina)",
            new { IdUsuario = idUsuario, Tabla = tabla, IdRegistro = idRegistro, Operacion = operacion.ToString(), Campo = campo,
                  ValorAntes = valorAntes, ValorDespues = valorDespues, Modulo = modulo, NomMaquina = nomMaquina, IpMaquina = ipMaquina }, tx);

    // Reemplaza EXEC sp_Guardar_Cobranza_Parcial_Cs_2026 — mismo motivo que CobrarCuotaAsync.
    public async Task<(bool Ok, bool PagoCompleto, string Msg)> CobrarCuotaParcialAsync(CobrarCuotaParcialParams prm)
    {
        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await AsegurarTablaAsignacionesAsync(conn, tx);
            await ValidarAsignacionCobranzaAsync(conn, tx, prm.IdCab, prm.IdGeneradas, prm.IdUsuario);

            var nomMaquina = Dns.GetHostName();
            const string ipMaquina = "127.0.0.1";

            var nuevaEntrega = prm.EntregaAnterior + prm.MontoPagado;
            var esPagoCompleto = nuevaEntrega >= prm.TotalCuota;
            byte estadoFinal = (byte)(esPagoCompleto ? 1 : 0);
            var tipoPagoTxt = esPagoCompleto ? "COMPLETO" : "PARCIAL";

            var cliente = await conn.QuerySingleAsync<(int IdCliente, string Nombre)>(
                "SELECT CS.ID_CLIENTE, ISNULL(LTRIM(RTRIM(CL.NOMBRE_CLIENTE)), '(Sin nombre)') " +
                "FROM CABECERA_SALES CS INNER JOIN CLIENTES CL ON CL.ID_CLIENTE = CS.ID_CLIENTE WHERE CS.IDCAB = @IdCab",
                new { prm.IdCab }, tx);
            var nombreCobrador = await conn.ExecuteScalarAsync<string>(
                "SELECT NOMBRE_USUARIO FROM USUARIOS WHERE ID_USUARIO = @IdUsuario", new { prm.IdUsuario }, tx) ?? "";

            var idUsuarioSesion = prm.IdUsuarioSesion ?? prm.IdUsuario;

            var antesCab = await conn.QuerySingleAsync<(decimal Haber, short Cpha, byte Estado)>(
                "SELECT ISNULL(HABER,0), ISNULL(CPHA,0), ISNULL(ESTADO,0) FROM CABECERA_SALES WHERE IDCAB = @IdCab",
                new { prm.IdCab }, tx);
            var filasCab = await conn.ExecuteAsync(
                "UPDATE CABECERA_SALES SET HABER = HABER + @MontoPagado, CPHA = CPHA + @Incr WHERE IDCAB = @IdCab",
                new { prm.MontoPagado, Incr = esPagoCompleto ? 1 : 0, prm.IdCab }, tx);
            if (filasCab == 0) { tx.Rollback(); return (false, false, "No se guardaron los cambios. Error en tabla CABECERA_SALES."); }
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CABECERA_SALES", prm.IdCab.ToString(), 'U', "HABER, CPHA",
                $"Haber: {antesCab.Haber} | CPHA: {antesCab.Cpha} | Estado: {antesCab.Estado}",
                $"Haber: {antesCab.Haber + prm.MontoPagado} | CPHA: {antesCab.Cpha + (esPagoCompleto ? 1 : 0)} | Tipo pago: {tipoPagoTxt} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTAS (PARCIAL)", nomMaquina, ipMaquina);

            var saldoRestante = esPagoCompleto ? 0 : prm.TotalCuota - nuevaEntrega;
            var antesGen = await conn.QuerySingleAsync<(decimal Entrega, byte Estado)>(
                "SELECT ISNULL(ENTREGA,0), ISNULL(ESTADO,0) FROM GENERADAS WHERE IDGENERADAS = @IdGeneradas",
                new { prm.IdGeneradas }, tx);
            var filasGen = await conn.ExecuteAsync(
                "UPDATE GENERADAS SET ENTREGA=@NuevaEntrega, MORA=@Mora, PUNITORIO=@Punitorio, REAJUSTE=@Reajuste, " +
                "TOTAL=@SaldoRestante, IDU=@IdUsuario, OBS=@Obs, ESTADO=@EstadoFinal, FECHACOBRADO=GETDATE() WHERE IDGENERADAS=@IdGeneradas",
                new { NuevaEntrega = nuevaEntrega, prm.Mora, prm.Punitorio, prm.Reajuste, SaldoRestante = saldoRestante, prm.IdUsuario, prm.Obs, EstadoFinal = estadoFinal, prm.IdGeneradas }, tx);
            if (filasGen == 0) { tx.Rollback(); return (false, false, "No se guardaron los cambios. Error en tabla GENERADAS."); }
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "GENERADAS", prm.IdGeneradas.ToString(), 'U', "ENTREGA, ESTADO, TOTAL",
                $"Entrega: {antesGen.Entrega} | Estado: {antesGen.Estado}",
                $"Comprobante: {prm.Comprobante} | Cuota N°: {prm.NCuota} | Entrega anterior: {prm.EntregaAnterior} | Abono hoy: {prm.MontoPagado} | " +
                $"Entrega total: {nuevaEntrega} | Saldo restante: {saldoRestante} | Tipo pago: {tipoPagoTxt} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTAS (PARCIAL)", nomMaquina, ipMaquina);

            // Igual que CobrarCuotaAsync: si este abono TERMINÓ de pagar la cuota, se
            // desactiva su asignación puntual (no la del crédito completo) — un abono
            // parcial que todavía deja saldo pendiente NO desactiva nada, la cuota sigue
            // necesitando cobrador asignado.
            if (esPagoCompleto)
            {
                await conn.ExecuteAsync(
                    $"UPDATE {TablaAsignaciones} SET ACTIVO = 0 WHERE ACTIVO = 1 AND IDGENERADAS = @IdGeneradas",
                    new { prm.IdGeneradas }, tx);
            }

            // Un registro por cada abono (parcial o el que completa la cuota) — trazabilidad
            // de cada pago individual, igual que el SP legado.
            var iegNuevo = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(IEG), 0) + 1 FROM HISTORIAL_ENTREGAS_GENERADAS WITH (UPDLOCK, HOLDLOCK)", transaction: tx);
            await conn.ExecuteAsync(
                "INSERT INTO HISTORIAL_ENTREGAS_GENERADAS (IEG,IDCAB,IDGENERADAS,COMPROBANTE,NCUOTA,ENTREGA,IDU,IDCLIE,FECHA,MORA,PUNITORIO,REAJUSTE,TOTAL,NOTA) " +
                "VALUES (@Ieg,@IdCab,@IdGeneradas,@Comprobante,@NCuota,@MontoPagado,@IdUsuario,@IdCliente,GETDATE(),@Mora,@Punitorio,@Reajuste,@SaldoRestante,@Obs)",
                new { Ieg = iegNuevo, prm.IdCab, prm.IdGeneradas, prm.Comprobante, prm.NCuota, prm.MontoPagado, prm.IdUsuario, cliente.IdCliente, prm.Mora, prm.Punitorio, prm.Reajuste, SaldoRestante = saldoRestante, prm.Obs }, tx);
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "HISTORIAL_ENTREGAS_GENERADAS", iegNuevo.ToString(), 'I', "TODOS",
                "(NUEVO)",
                $"Abono cuota N° {prm.NCuota} | Monto: {prm.MontoPagado} | Comprobante: {prm.Comprobante} | Cliente: {cliente.Nombre} | Usuario: {nombreCobrador}",
                "COBRO CUOTAS (PARCIAL)", nomMaquina, ipMaquina);

            if (prm.Inforcom == 1)
            {
                var antesInforcom = await conn.ExecuteScalarAsync<byte?>(
                    "SELECT ISNULL(INFORCOM,0) FROM CLIENTES WHERE ID_CLIENTE = @IdCliente", new { cliente.IdCliente }, tx) ?? 0;
                await conn.ExecuteAsync("UPDATE CLIENTES SET INFORCOM = @Inforcom WHERE ID_CLIENTE = @IdCliente",
                    new { prm.Inforcom, cliente.IdCliente }, tx);
                await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CLIENTES", cliente.IdCliente.ToString(), 'U', "INFORCOM",
                    $"Inforcom: {antesInforcom} | Cliente: {cliente.Nombre}",
                    $" Actualizar Inforconf a: {prm.Inforcom} | Cliente: {cliente.Nombre} | Motivo: Mora en cuota N° {prm.NCuota} | Comprobante: {prm.Comprobante} | Usuario: {nombreCobrador}",
                    "COBRO CUOTAS (PARCIAL)", nomMaquina, ipMaquina);
            }

            var idMaster = await conn.ExecuteScalarAsync<int?>(
                "SELECT ID_MASTER FROM CAJA_MASTER WITH (UPDLOCK, HOLDLOCK) WHERE ID_LOCAL=@IdLocal AND ID_CAJA_FISICA=@IdCajaFisica AND ESTADO='A'",
                new { prm.IdLocal, prm.IdCajaFisica }, tx);
            if (idMaster == null) { tx.Rollback(); return (false, false, "CAJA NO ABIERTA"); }

            var tipoTxt = esPagoCompleto ? "COBRO CUOTA N°: " : "ABONO PARCIAL CUOTA N°: ";
            var concepto = $"{tipoTxt}{prm.NCuota} | COMPROBANTE: {prm.Comprobante} | CLIENTE: {cliente.Nombre}";
            // ID_CAJERO = quien realmente operaba la caja (IdUsuarioSesion); ID_VENDEDOR =
            // a quién se le acredita la comisión (IdUsuario, puede ser "a nombre de otro
            // vendedor") — antes estaban invertidos, ver comentario de CobrarCuotaParams.
            var idDetalle = await conn.QuerySingleAsync<int>(
                "INSERT INTO CAJA_DETALLE (ID_MASTER,ID_VENTA,ID_LOCAL,FECHA_HORA,TIPO,SUBTIPO,FORMA_PAGO,MONTO,ID_CAJERO,ID_ENTIDAD,CONCEPTO,REFERENCIA,ESTADO_REG,ID_VENDEDOR) " +
                "VALUES (@IdMaster,NULL,@IdLocal,GETDATE(),'I','COBRO_SISTEMA',@FormaPago,@MontoPagado,@IdUsuarioSesion,NULL,@Concepto,@Referencia,'V',@IdUsuario); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { IdMaster = idMaster, prm.IdLocal, prm.FormaPago, prm.MontoPagado, prm.IdUsuario, Concepto = concepto, prm.Referencia, IdUsuarioSesion = idUsuarioSesion }, tx);
            await InsertarAuditoriaAsync(conn, tx, prm.IdUsuario, "CAJA_DETALLE", idDetalle.ToString(), 'I', "TODOS",
                "(NUEVO)", concepto, "COBRO CUOTA (PARCIAL)", nomMaquina, ipMaquina);

            // Solo se consume el descuento cuando el abono TERMINA de pagar la cuota — un
            // abono parcial que deja saldo pendiente no debe gastar el descuento todavía,
            // sigue disponible para cuando se complete el pago.
            if (esPagoCompleto)
                await MarcarDescuentoSiExisteAsync(conn, tx, prm.IdGeneradas, prm.IdUsuario);

            tx.Commit();
            var msg = esPagoCompleto ? "GUARDADO" : "GUARDADO_PARCIAL";
            return (true, esPagoCompleto, msg);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private async Task ValidarAsignacionCobranzaAsync(IDbConnection conn, IDbTransaction tx, int idCab, int idGeneradas, int idUsuario)
    {
        var asignacion = await conn.QueryFirstOrDefaultAsync<dynamic>(
            $@"SELECT TOP 1 A.IDCOBRADOR AS IdCobrador,
                      ISNULL(U.NOMBRE_USUARIO, '') AS Nombre
               FROM {TablaAsignaciones} A
               INNER JOIN USUARIOS U ON U.ID_USUARIO = A.IDCOBRADOR
               WHERE A.ACTIVO = 1
                 AND (
                    A.IDGENERADAS = @IdGeneradas
                    OR (A.IDGENERADAS IS NULL AND A.IDCAB = @IdCab)
                 )
               ORDER BY CASE WHEN A.IDGENERADAS IS NOT NULL THEN 0 ELSE 1 END,
                        A.FECHA_ASIGNACION DESC,
                        A.IDASIGNACION DESC",
            new { IdCab = idCab, IdGeneradas = idGeneradas }, tx);

        if (asignacion == null) return;
        int idCobradorAsignado = Convert.ToInt32(asignacion.IdCobrador);
        if (idCobradorAsignado == idUsuario) return;

        throw new InvalidOperationException(
            $"Este crédito/cuota está asignado a {((string?)asignacion.Nombre) ?? ""}. Solo ese cobrador puede registrarlo.");
    }

    private async Task AsegurarTablaAsignacionesAsync(IDbConnection conn, IDbTransaction? tx = null)
    {
        if (_schemaVerificado) return;

        lock (_schemaLock)
        {
            if (_schemaVerificado) return;
        }

        var existe = await conn.ExecuteScalarAsync<int>(
            @"SELECT CASE WHEN OBJECT_ID('dbo.COBRANZA_ASIGNACIONES', 'U') IS NULL THEN 0 ELSE 1 END",
            transaction: tx);
        if (existe == 1)
        {
            lock (_schemaLock) { _schemaVerificado = true; }
            return;
        }

        var ddl = @"
            CREATE TABLE dbo.COBRANZA_ASIGNACIONES
            (
                IDASIGNACION       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                IDCAB              INT NULL,
                IDGENERADAS        INT NULL,
                IDCOBRADOR         INT NOT NULL,
                IDUSUARIO_REGISTRO INT NOT NULL,
                FECHA_ASIGNACION   DATETIME NOT NULL CONSTRAINT DF_COBRANZA_ASIGNACIONES_FECHA DEFAULT(GETDATE()),
                ACTIVO             BIT NOT NULL CONSTRAINT DF_COBRANZA_ASIGNACIONES_ACTIVO DEFAULT(1)
            );

            CREATE INDEX IX_COBRANZA_ASIGNACIONES_CAB
                ON dbo.COBRANZA_ASIGNACIONES (IDCAB, ACTIVO, FECHA_ASIGNACION DESC);

            CREATE INDEX IX_COBRANZA_ASIGNACIONES_CUOTA
                ON dbo.COBRANZA_ASIGNACIONES (IDGENERADAS, ACTIVO, FECHA_ASIGNACION DESC);

            CREATE INDEX IX_COBRANZA_ASIGNACIONES_COBRADOR
                ON dbo.COBRANZA_ASIGNACIONES (IDCOBRADOR, ACTIVO, FECHA_ASIGNACION DESC);

            CREATE UNIQUE INDEX UX_COBRANZA_ASIGNACIONES_CAB_ACTIVA
                ON dbo.COBRANZA_ASIGNACIONES (IDCAB)
                WHERE ACTIVO = 1 AND IDGENERADAS IS NULL;

            CREATE UNIQUE INDEX UX_COBRANZA_ASIGNACIONES_CUOTA_ACTIVA
                ON dbo.COBRANZA_ASIGNACIONES (IDGENERADAS)
                WHERE ACTIVO = 1 AND IDGENERADAS IS NOT NULL;";

        await conn.ExecuteAsync(ddl, transaction: tx);
        lock (_schemaLock) { _schemaVerificado = true; }
    }

    public async Task<decimal> ObtenerTasaPunitiorioAsync()
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<decimal?>(
            "SELECT TOP 1 VALOR_PUNITORIO FROM CONFIGURACION") ?? 0;
    }

    public async Task<decimal> ObtenerValorInforconfAsync()
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<decimal?>(
            "SELECT TOP 1 VALOR_INFORCONF FROM CONFIGURACION") ?? 0;
    }

    // El cargo de Inforconf se cobra UNA vez por episodio de mora (no una vez por cuota
    // atrasada) y se concentra en la cuota atrasada MÁS ANTIGUA (menor VTO) del cliente,
    // siempre que esa cuota ya lleve 90+ días de mora (con el mismo período de gracia de 5
    // días usado en el resto del sistema). No depende de CLIENTES.INFORCOM — ese flag es un
    // dato manual aparte, no el disparador del cargo.
    // GENERADAS.INFORCOM_APLICADO marca si esa cuota ya llevó el cargo de este episodio —
    // mientras el cliente tenga cuotas atrasadas, no se vuelve a cobrar; si se pone al día
    // y luego se atrasa de nuevo, la cuota nueva empieza con el flag en 0 (episodio nuevo).
    public async Task<bool> CorrespondeCargoInforconfAsync(int idCliente, int idGeneradas)
    {
        using var conn = _factory.Create();
        var masAntigua = await conn.QueryFirstOrDefaultAsync<(int IdGeneradas, bool Aplicado, DateTime Vto)?>(
            "SELECT TOP 1 G.IDGENERADAS AS IdGeneradas, G.INFORCOM_APLICADO AS Aplicado, G.VTO AS Vto " +
            "FROM GENERADAS G " +
            "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
            "WHERE CB.ID_CLIENTE = @IdCliente AND G.ESTADO = 0 AND G.VTO < CAST(GETDATE() AS date) " +
            "ORDER BY G.VTO ASC",
            new { IdCliente = idCliente });

        if (masAntigua == null) return false;
        if (masAntigua.Value.IdGeneradas != idGeneradas || masAntigua.Value.Aplicado) return false;

        var diasMora = Math.Max(0, (DateTime.Today - masAntigua.Value.Vto.Date).Days - 5);
        return diasMora >= 90;
    }

    public async Task MarcarInforconfAplicadoAsync(int idGeneradas)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "UPDATE GENERADAS SET INFORCOM_APLICADO = 1 WHERE IDGENERADAS = @Id",
            new { Id = idGeneradas });
    }

    public async Task<decimal> CalcularPunitoriocAsync(int idGeneradas)
    {
        using var conn = _factory.Create();
        var cuota = await conn.QueryFirstOrDefaultAsync<(decimal Monto, DateTime Vto)>(
            "SELECT MONTO, VTO FROM GENERADAS WHERE IDGENERADAS = @Id",
            new { Id = idGeneradas });
        if (cuota.Vto.Date >= DateTime.Today) return 0;

        var tasa = await conn.ExecuteScalarAsync<decimal?>(
            "SELECT TOP 1 VALOR_PUNITORIO FROM CONFIGURACION") ?? 0;
        // El sistema aplica un período de gracia de 5 días (confirmado por el usuario 2026-07-30; antes 3)
        var dias = Math.Max(0, (DateTime.Today - cuota.Vto.Date).Days - 5);
        if (dias == 0) return 0;
        // El punitorio se calcula SIEMPRE sobre el capital ORIGINAL de la cuota (MONTO), nunca
        // restando ENTREGA — el punitorio ya cobrado/persistido en un abono anterior es un
        // valor histórico y no se recalcula retroactivamente aplicándole la entrega de ese
        // mismo abono (bug real detectado: tras un abono parcial de Gs. 10, GENERADAS.PUNITORIO
        // quedaba en 195.325, pero al reabrir el panel se recalculaba en 195.319 restando esos
        // mismos Gs. 10 del capital, pisando un valor ya persistido y ya mostrado al cliente).
        // Tasa mensual base 25 (mes comercial de 25 días hábiles — fórmula del sistema anterior)
        return Math.Round(cuota.Monto * (tasa / 100m) / 25m * dias, 0);
    }

    public async Task<IEnumerable<(byte NCuota, decimal Monto, string Vto, string Estado, int Mora, string Obs, string? FechaPago, int? DiasVtoAPago)>> ObtenerHistorialAsync(int idCab)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<dynamic>(
            "SELECT G.NCUOTA, G.MONTO, " +
            "CONVERT(VARCHAR(10), G.VTO, 103) AS VTO, " +
            "CASE WHEN G.ESTADO = 0 THEN 'Pendiente' ELSE 'Cancelado' END AS ESTADO, " +
            // Para cuotas canceladas NO se usa la columna G.MORA persistida: quedó en 0 para
            // prácticamente todo pago histórico (confirmado con datos reales — créditos con
            // FECHACOBRADO hasta 21 días después de VTO igual muestran MORA=0), aparentemente
            // nunca se actualizó al cobrar. Se recalcula igual que DIASVTOAPAGO pero con piso en
            // 0 (si pagó antes de vencer no hay "mora", hay anticipo).
            "CASE WHEN G.ESTADO = 0 THEN DATEDIFF(day, G.VTO, GETDATE()) " +
            "ELSE (CASE WHEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) > 0 THEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) ELSE 0 END) END AS MORA, " +
            "ISNULL(G.OBS, '') AS OBS, " +
            // FECHACOBRADO solo es una fecha de pago real cuando la cuota está efectivamente
            // cobrada (ESTADO=1) — el sistema viejo inicializa este campo en VTO al CREAR la
            // cuota, así que para una cuota todavía PENDIENTE no significa nada (ver
            // Cuota.EstaPendiente en el modelo C#, mismo criterio ya usado en toda la app).
            "CASE WHEN G.ESTADO = 1 THEN CONVERT(VARCHAR(10), G.FECHACOBRADO, 103) ELSE NULL END AS FECHAPAGO, " +
            // Días entre vencimiento y pago: negativo = pagó antes de vencer, 0 = el mismo día,
            // positivo = con atraso. Verificado con 100% de cobertura en cuotas cobradas
            // (GENERADAS.FECHACOBRADO), cruzado contra AUDITORIA para confirmar que no es un
            // valor "legado sin actualizar" — coincide al minuto con el registro real del cobro.
            "CASE WHEN G.ESTADO = 1 THEN DATEDIFF(day, G.VTO, G.FECHACOBRADO) ELSE NULL END AS DIASVTOAPAGO " +
            "FROM GENERADAS G WHERE G.IDCAB = @IdCab ORDER BY G.NCUOTA",
            new { IdCab = idCab });

        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            byte ncuota      = d.TryGetValue("NCUOTA",       out var nc)  ? Convert.ToByte(nc  ?? 0) : (byte)0;
            decimal monto    = d.TryGetValue("MONTO",        out var mo)  ? Convert.ToDecimal(mo ?? 0) : 0m;
            string vto       = d.TryGetValue("VTO",          out var vt)  ? vt?.ToString() ?? "" : "";
            string estado    = d.TryGetValue("ESTADO",       out var es)  ? es?.ToString() ?? "" : "";
            int mora         = d.TryGetValue("MORA",         out var mr)  ? Convert.ToInt32(mr ?? 0) : 0;
            string obs       = d.TryGetValue("OBS",          out var ob)  ? ob?.ToString() ?? "" : "";
            string? fechaPago = d.TryGetValue("FECHAPAGO",    out var fp)  ? fp?.ToString() : null;
            int? diasVtoPago = d.TryGetValue("DIASVTOAPAGO", out var dv) && dv != null
                ? Convert.ToInt32(dv) : (int?)null;
            return (ncuota, monto, vto, estado, mora, obs, fechaPago, diasVtoPago);
        }).ToList();
    }

    public async Task<IEnumerable<(string Descripcion, decimal Cantidad, decimal PVenta)>> ObtenerArticulosAsync(int idCab)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@Idcab", idCab);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 12);

        var rows = await conn.QueryAsync<dynamic>(
            "BUSCAR_ARTICULOS_HISTORIAL_CS", p, commandType: CommandType.StoredProcedure);

        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            string desc    = d.TryGetValue("DESCRIPCION", out var de) ? de?.ToString() ?? "" : "";
            decimal cant   = d.TryGetValue("CANTIDAD",    out var ca) ? Convert.ToDecimal(ca ?? 0) : 0m;
            decimal pventa = d.TryGetValue("PVENTA",      out var pv) ? Convert.ToDecimal(pv ?? 0) : 0m;
            return (desc, cant, pventa);
        }).ToList();
    }

    public async Task<IEnumerable<PagoCuotaRow>> ObtenerPagosCuotaAsync(string comprobante, byte nCuota)
    {
        using var conn = _factory.Create();
        // El texto de CONCEPTO no es estable entre distintas versiones del sistema: código
        // legado escribía "COBRO ENTREGA CUOTA N°: 2 | ... | Cliente: ..." con el carácter
        // ordinal º (0xBA) donde el código actual (CobrarCuotaAsync/CobrarCuotaParcialAsync)
        // escribe "COBRO CUOTA N°: 2 |"/"ABONO PARCIAL CUOTA N°: 2 |" con el símbolo de grado
        // ° (U+00B0) — un filtro que hardcodee ese carácter deja afuera los registros viejos
        // (bug real detectado: solo traía 1 de 3 pagos de un crédito con abonos previos a esta
        // funcionalidad). Se usa el comodín "_" de LIKE para el carácter variable, y se ancla
        // "CUOTA N_: {n} |" (con espacio antes de la barra) para no confundir cuota 2 con 20+.
        var filtroCuota = $"CUOTA N_: {nCuota} |";
        var filtroComprobante = $"COMPROBANTE: {comprobante}";
        return await conn.QueryAsync<PagoCuotaRow>(
            "SELECT FECHA_HORA AS Fecha, MONTO AS Monto, FORMA_PAGO AS FormaPago, CONCEPTO AS Concepto " +
            "FROM CAJA_DETALLE " +
            "WHERE TIPO='I' AND SUBTIPO='COBRO_SISTEMA' " +
            "AND CONCEPTO LIKE @FiltroCuota AND CONCEPTO LIKE @FiltroComprobante " +
            "ORDER BY FECHA_HORA",
            new { FiltroCuota = $"%{filtroCuota}%", FiltroComprobante = $"%{filtroComprobante}%" });
    }

    public async Task<DescuentoCuotaRow?> ObtenerDescuentoPendienteAsync(int idGeneradas)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<DescuentoCuotaRow>(
            "SELECT IDDESCUENTO AS IdDescuento, IDGENERADAS AS IdGeneradas, MONTO AS Monto, " +
            "MOTIVO AS Motivo, NRO_NOTA_CREDITO AS NroNotaCredito, FECHA_CREACION AS FechaCreacion " +
            "FROM DESCUENTOS_CUOTA WHERE IDGENERADAS = @IdGeneradas AND APLICADO = 0 AND ANULADO = 0",
            new { IdGeneradas = idGeneradas });
    }

    public async Task<bool> CrearDescuentoCuotaAsync(int idGeneradas, decimal monto, string? motivo, string? nroNotaCredito, int idUsuarioCreador)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync(
                "INSERT INTO DESCUENTOS_CUOTA (IDGENERADAS, MONTO, MOTIVO, NRO_NOTA_CREDITO, ID_USUARIO_CREADOR) " +
                "VALUES (@IdGeneradas, @Monto, @Motivo, @NroNotaCredito, @IdUsuarioCreador)",
                new { IdGeneradas = idGeneradas, Monto = monto, Motivo = motivo, NroNotaCredito = nroNotaCredito, IdUsuarioCreador = idUsuarioCreador }, tx);

            await InsertarAuditoriaAsync(conn, tx, idUsuarioCreador, "DESCUENTOS_CUOTA", idGeneradas.ToString(), 'I', "MONTO",
                "(NUEVO)",
                $"Descuento: {monto:N0} | Motivo: {motivo} | NC: {nroNotaCredito}",
                "DESCUENTO NOTA DE CREDITO", Dns.GetHostName(), "127.0.0.1");

            tx.Commit();
            return rows > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // Se llama DESDE la misma transacción de CobrarCuotaAsync/CobrarCuotaParcialAsync — recibe
    // conn/tx ya abiertos en vez de crear los suyos, para que "cobrar la cuota" y "marcar el
    // descuento como usado" sean atómicos: si el cobro falla y hace rollback, el descuento
    // sigue disponible para el próximo intento en vez de quedar consumido en falso.
    public async Task<bool> EliminarVentaCreditoAsync(EliminarVentaParams prm)
    {
        using var conn = _factory.Create();

        var articulos = (await conn.QueryAsync<(int IdArt, decimal Cantidad)>(
            "SELECT IDART, CANTIDAD FROM DETALLES_SALES WHERE IDCAB = @IdCab AND ES = 1",
            new { prm.IdCab })).ToList();

        if (articulos.Count == 0) return false;

        var ultimo = articulos.Count;
        string? lastMsg = null;

        for (int i = 1; i <= ultimo; i++)
        {
            var art = articulos[i - 1];
            var p = new DynamicParameters();
            p.Add("@AGENTE",      i);
            p.Add("@ULTIMO",      ultimo);
            p.Add("@IDCAB",       prm.IdCab);
            p.Add("@LOCAL",       prm.IdLocal);
            p.Add("@TOTALVENTA",  prm.TotalVenta.ToString());
            p.Add("@ENTREGADO",   prm.Entregado.ToString());
            p.Add("@IDART",       art.IdArt);
            p.Add("@CANTIDAD",    art.Cantidad);
            p.Add("@IDUMODSTOCK", prm.IdUsuario);
            p.Add("@IDVENTA",     prm.IdCab);
            p.Add("@NVENTA",      prm.NVenta);
            p.Add("@NOM_MAQUINA", Dns.GetHostName());
            p.Add("@IP_MAQUINA",  "127.0.0.1");
            p.Add("@Msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

            await conn.ExecuteAsync("sp_Eliminar_Venta_Credito_Cs_2026", p,
                commandType: CommandType.StoredProcedure);

            lastMsg = p.Get<string>("@Msg") ?? "";
            if (lastMsg.StartsWith("Error", StringComparison.OrdinalIgnoreCase)) return false;
        }

        return lastMsg == "GUARDADO";
    }
}

public record EliminarVentaParams(
    int    IdCab,
    byte   IdLocal,
    int    IdUsuario,
    decimal TotalVenta,
    decimal Entregado,
    string NVenta);
