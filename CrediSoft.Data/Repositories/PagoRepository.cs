using Dapper;
using System.Data;
using System.Net;

namespace CrediSoft.Data.Repositories;

public record FuncionarioInfo(
    int     IdUsuario,
    string  Ci,
    string  Nombre,
    int     IdLocal,
    string  NombreLocal,
    decimal Salario,
    decimal PorcVenta,
    decimal PorcCobranza);

public record PagoTempInfo(
    int     IdGps,
    int     IdUsuario,
    string  Nombre,
    decimal Salario,
    decimal Venta,
    decimal PorcVenta,
    decimal TotalVenta,
    decimal Cobranza,
    decimal PorcCobranza,
    decimal TotalCobranza,
    decimal Plus,
    decimal HorasExtras,
    decimal Bonificacion,
    decimal OtrasComisiones,
    decimal TotalIngresos,
    decimal Ausencias,
    decimal Adelantos,
    decimal Ips,
    decimal Cuotas,
    decimal Multas,
    decimal Otros,
    decimal Equis,
    decimal TotalEgresos,
    string  NotaAsignacion,
    string  NotaEgreso,
    string  Fecha);

public record GenerarPagoParams(
    int     IdUsuario,
    byte    IdLocal,
    decimal Salario,
    decimal Venta,
    decimal PorcVenta,
    decimal TotalVenta,
    decimal Cobranza,
    decimal PorcCobranza,
    decimal TotalCobranza,
    decimal Plus,
    decimal HorasExtras,
    decimal Bonificacion,
    decimal OtrasComisiones,
    decimal TotalIngresos,
    decimal Ausencias,
    decimal Adelantos,
    decimal Ips,
    decimal Cuotas,
    decimal Multas,
    decimal Otros,
    decimal Equis,
    decimal TotalEgresos,
    string  Nombre,
    string  NotaAsignacion,
    string  NotaEgreso);

public record GuardarPagoParams(
    int     IdGps,
    int     IdUsuario,
    byte    IdLocal,
    decimal Salario,
    decimal Venta,
    decimal PorcVenta,
    decimal TotalVenta,
    decimal Cobranza,
    decimal PorcCobranza,
    decimal TotalCobranza,
    decimal Plus,
    decimal HorasExtras,
    decimal Bonificacion,
    decimal OtrasComisiones,
    decimal TotalIngresos,
    decimal Ausencias,
    decimal Adelantos,
    decimal Ips,
    decimal Cuotas,
    decimal Multas,
    decimal Otros,
    decimal Equis,
    decimal TotalEgresos,
    string  Nombre,
    DateTime Fecha,
    string  NotaAsignacion,
    string  NotaEgreso,
    string  FormaPago,
    decimal MontoCaja,
    int     IdCajero,
    int     IdCajaFisica,
    int     IdMaster,
    string  Referencia);

public record HistorialPagoItem(
    int     IdHpf,
    decimal Salario,
    decimal Venta,       decimal PorcVenta,    decimal TotalVenta,
    decimal Cobranza,    decimal PorcCobranza, decimal TotalCobranza,
    decimal Plus,        decimal HorasExtras,  decimal Bonificacion, decimal OtrasComisiones,
    decimal TotalIngresos,
    decimal Ausencias,   decimal Adelantos,    decimal Ips,
    decimal Cuotas,      decimal Multas,       decimal Otros,  decimal Equis,
    decimal TotalEgresos,
    string  Nombre,      DateTime Fecha,
    string  NotaAsignacion, string NotaEgreso)
{
    public decimal Neto       => TotalIngresos - TotalEgresos;
    public string  FechaFmt   => Fecha.ToString("dd/MM/yyyy");
    public string  SalarioFmt => Salario.ToString("N0");
    public string  IngresosFmt=> TotalIngresos.ToString("N0");
    public string  EgresosFmt => TotalEgresos.ToString("N0");
    public string  NetoFmt    => Neto.ToString("N0");
}

public record DetalleVentaItem(
    int     IdCab,
    string  NVenta,
    string  ClienteNombre,
    DateTime Fecha,
    decimal Total)
{
    public string FechaFmt  => Fecha.ToString("dd/MM/yyyy");
    public string TotalFmt  => Total.ToString("N0");
}

public record DetalleCobranzaItem(
    int     IdGeneradas,
    string  ClienteNombre,
    byte    NCuota,
    DateTime FechaCobrado,
    decimal Monto,
    decimal Total,
    bool    EsPagoParcial)
{
    public string FechaFmt => FechaCobrado.ToString("dd/MM/yyyy");
    public string MontoFmt => Monto.ToString("N0");
    public string TotalFmt => Total.ToString("N0");
    // La entrega de la venta nunca llega acá (se graba con SUBTIPO='VENTA' en
    // CAJA_DETALLE, no 'COBRO_SISTEMA' — ver GetDetalleCobranzasAsync), así que NCUOTA=1 sí
    // es una cuota real cobrada. NCUOTA=0 solo puede pasar si el texto de CONCEPTO no siguió
    // el formato esperado (dato viejo o corrupto) — no hay forma de saber a qué cuota
    // corresponde ese pago.
    public string NCuotaTexto => NCuota == 0 ? "—" : (NCuota - 1).ToString();
    // Un abono parcial no cierra la cuota — el resto se sigue cobrando después y genera su
    // propia fila (y su propia comisión) aparte, así que hay que dejar claro que ESTE pago
    // puntual no fue el total de la cuota.
    public string TipoPagoTexto => EsPagoParcial ? "Parcial" : "Completo";
}

public interface IPagoRepository
{
    Task<IEnumerable<FuncionarioInfo>> ListarFuncionariosAsync();
    Task<FuncionarioInfo?> BuscarFuncionarioPorCiAsync(string ci);
    Task<int> GenerarPagoTempAsync(GenerarPagoParams p);
    Task<bool> EditarPagoTempAsync(int idGps, GenerarPagoParams p);
    Task<bool> EliminarPagoTempAsync(int idGps);
    Task<bool> GuardarPagoAsync(GuardarPagoParams p);
    Task<IEnumerable<PagoTempInfo>> ObtenerHistorialAsync(int idUsuario, DateTime desde, DateTime hasta);
    Task<IEnumerable<HistorialPagoItem>> ObtenerHistorialPagosAsync(int idUsuario, DateTime desde, DateTime hasta);
    Task<bool> ActualizarPagoAsync(int idHpf, GuardarPagoParams p);
    Task<bool> EliminarPagoDefinitivoAsync(int idHpf);
    Task<IEnumerable<DetalleVentaItem>> GetDetalleVentasAsync(int idUsuario, int idLocal, DateTime desde, DateTime hasta);
    Task<IEnumerable<DetalleCobranzaItem>> GetDetalleCobranzasAsync(int idUsuario, int idLocal, DateTime desde, DateTime hasta);
}

public class PagoRepository : IPagoRepository
{
    private readonly IDbConnectionFactory _factory;
    public PagoRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<FuncionarioInfo>> ListarFuncionariosAsync()
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<dynamic>(
            @"SELECT U.ID_USUARIO, U.CI_USUARIO, U.NOMBRE_USUARIO, U.CARGO_USUARIO,
                     U.LOCAL_USUARIO, U.SALARIO_USUARIO, U.COMISION_USUARIO, U.COMISION_COBRANZA,
                     ISNULL(L.NOMBRE,'') AS NOMBRE_LOCAL
              FROM USUARIOS U
              LEFT JOIN LOCALES L ON L.ID_LOCAL = U.LOCAL_USUARIO
              ORDER BY U.NOMBRE_USUARIO");

        return rows.Select(r => new FuncionarioInfo(
            IdUsuario:    Convert.ToInt32(r.ID_USUARIO   ?? 0),
            Ci:           r.CI_USUARIO?.ToString()       ?? "",
            Nombre:       r.NOMBRE_USUARIO?.ToString()   ?? "",
            IdLocal:      Convert.ToInt32(r.LOCAL_USUARIO ?? 0),
            NombreLocal:  r.NOMBRE_LOCAL?.ToString()     ?? "",
            Salario:      Convert.ToDecimal(r.SALARIO_USUARIO  ?? 0),
            PorcVenta:    Convert.ToDecimal(r.COMISION_USUARIO ?? 0),
            PorcCobranza: Convert.ToDecimal(r.COMISION_COBRANZA ?? 0)));
    }

    public async Task<FuncionarioInfo?> BuscarFuncionarioPorCiAsync(string ci)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@Ci",  ci);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 12);

        var rows = await conn.QueryAsync<dynamic>(
            "BUSCAR_FUNCIONARIO_CI_CS", p, commandType: CommandType.StoredProcedure);

        var row = rows.FirstOrDefault();
        if (row == null) return null;

        var d = (IDictionary<string, object>)row;

        int idLocal = d.TryGetValue("LOCAL_USUARIO", out var lo) ? Convert.ToInt32(lo ?? 0) : 0;

        // Traer nombre del local
        var nombreLocal = await conn.ExecuteScalarAsync<string>(
            "SELECT ISNULL(NOMBRE,'') FROM LOCALES WHERE ID_LOCAL = @Id",
            new { Id = idLocal }) ?? "";

        return new FuncionarioInfo(
            IdUsuario:    d.TryGetValue("ID_USUARIO",       out var id)  ? Convert.ToInt32(id  ?? 0) : 0,
            Ci:           d.TryGetValue("CI_USUARIO",       out var c)   ? c?.ToString() ?? ""       : "",
            Nombre:       d.TryGetValue("NOMBRE_USUARIO",   out var n)   ? n?.ToString() ?? ""       : "",
            IdLocal:      idLocal,
            NombreLocal:  nombreLocal,
            Salario:      d.TryGetValue("SALARIO_USUARIO",  out var s)   ? Convert.ToDecimal(s ?? 0) : 0,
            PorcVenta:    d.TryGetValue("COMISION_USUARIO", out var pv)  ? Convert.ToDecimal(pv ?? 0): 0,
            PorcCobranza: d.TryGetValue("COMISION_COBRANZA",out var pc)  ? Convert.ToDecimal(pc ?? 0): 0);
    }

    public async Task<int> GenerarPagoTempAsync(GenerarPagoParams p)
    {
        using var conn = _factory.Create();
        var dp = new DynamicParameters();
        dp.Add("@IDGPS",            dbType: DbType.Int32, direction: ParameterDirection.Output);
        dp.Add("@IDU",              p.IdUsuario);
        dp.Add("@IDLOCAL",          p.IdLocal);
        dp.Add("@SALARIO",          p.Salario);
        dp.Add("@VENTA",            p.Venta);
        dp.Add("@PORCVENTA",        p.PorcVenta);
        dp.Add("@TOTALVENTA",       p.TotalVenta);
        dp.Add("@COBRANZA",         p.Cobranza);
        dp.Add("@PORCCOBRANZA",     p.PorcCobranza);
        dp.Add("@TOTALCOBRANZA",    p.TotalCobranza);
        dp.Add("@PLUS",             p.Plus);
        dp.Add("@HORASEXTRAS",      p.HorasExtras);
        dp.Add("@BONIFICACION",     p.Bonificacion);
        dp.Add("@OTRASCOMISIONES",  p.OtrasComisiones);
        dp.Add("@TOTALINGRESOS",    p.TotalIngresos);
        dp.Add("@AUSENCIAS",        p.Ausencias);
        dp.Add("@ADELANTOS",        p.Adelantos);
        dp.Add("@IPS",              p.Ips);
        dp.Add("@CUOTAS",           p.Cuotas);
        dp.Add("@MULTAS",           p.Multas);
        dp.Add("@OTROS",            p.Otros);
        dp.Add("@EQUIS",            p.Equis);
        dp.Add("@TOTALEGRESOS",     p.TotalEgresos);
        dp.Add("@NOMBRE",           p.Nombre);
        dp.Add("@NOTAASIGNACION",   p.NotaAsignacion);
        dp.Add("@NOTAEGRESO",       p.NotaEgreso);
        dp.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("GENERAR_PAGOSALARIO_CS_2026", dp, commandType: CommandType.StoredProcedure);

        var msg = dp.Get<string>("@msg") ?? "";
        if (!msg.Equals("GUARDADO", StringComparison.OrdinalIgnoreCase)) return 0;
        return dp.Get<int>("@IDGPS");
    }

    public async Task<bool> EditarPagoTempAsync(int idGps, GenerarPagoParams p)
    {
        using var conn = _factory.Create();
        var dp = new DynamicParameters();
        dp.Add("@idgps",          idGps);
        dp.Add("@salario",        p.Salario);
        dp.Add("@venta",          p.Venta);
        dp.Add("@totalventa",     p.TotalVenta);
        dp.Add("@cobranza",       p.Cobranza);
        dp.Add("@totalcobranza",  p.TotalCobranza);
        dp.Add("@plus",           p.Plus);
        dp.Add("@horas",          p.HorasExtras);
        dp.Add("@bonificacion",   p.Bonificacion);
        dp.Add("@otras",          p.OtrasComisiones);
        dp.Add("@totalingresos",  p.TotalIngresos);
        dp.Add("@ausencias",      p.Ausencias);
        dp.Add("@adelantos",      p.Adelantos);
        dp.Add("@ips",            p.Ips);
        dp.Add("@cuotas",         p.Cuotas);
        dp.Add("@multas",         p.Multas);
        dp.Add("@otros",          p.Otros);
        dp.Add("@equis",          p.Equis);
        dp.Add("@totalegresos",   p.TotalEgresos);
        dp.Add("@nota_asignacion",p.NotaAsignacion);
        dp.Add("@nota_egreso",    p.NotaEgreso);
        dp.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await conn.ExecuteAsync("EDITAR_GENERAR_PAGO_CS", dp, commandType: CommandType.StoredProcedure);
        var msg = dp.Get<string>("@msg") ?? "";
        return msg.Equals("Guardado", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> EliminarPagoTempAsync(int idGps)
    {
        using var conn = _factory.Create();
        var dp = new DynamicParameters();
        dp.Add("@idgps", idGps);
        dp.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ELIMINAR_PAGOGENTMP_CS", dp, commandType: CommandType.StoredProcedure);
        var msg = dp.Get<string>("@msg") ?? "";
        return msg.Equals("ELIMINADO", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> GuardarPagoAsync(GuardarPagoParams p)
    {
        using var conn = _factory.Create();
        var dp = new DynamicParameters();
        dp.Add("@IDHPF",           0);
        dp.Add("@IDU",             p.IdUsuario);
        dp.Add("@IDLOCAL",         p.IdLocal);
        dp.Add("@SALARIO",         p.Salario);
        dp.Add("@VENTA",           p.Venta);
        dp.Add("@PORCVENTA",       p.PorcVenta);
        dp.Add("@TOTALVENTA",      p.TotalVenta);
        dp.Add("@COBRANZA",        p.Cobranza);
        dp.Add("@PORCCOBRANZA",    p.PorcCobranza);
        dp.Add("@TOTALCOBRANZA",   p.TotalCobranza);
        dp.Add("@PLUS",            p.Plus);
        dp.Add("@HORASEXTRAS",     p.HorasExtras);
        dp.Add("@BONIFICACION",    p.Bonificacion);
        dp.Add("@OTRASCOMISIONES", p.OtrasComisiones);
        dp.Add("@TOTALINGRESOS",   p.TotalIngresos);
        dp.Add("@AUSENCIAS",       p.Ausencias);
        dp.Add("@ADELANTOS",       p.Adelantos);
        dp.Add("@IPS",             p.Ips);
        dp.Add("@CUOTAS",          p.Cuotas);
        dp.Add("@OTROS",           p.Otros);
        dp.Add("@EQUIS",           p.Equis);
        dp.Add("@TOTALEGRESOS",    p.TotalEgresos);
        dp.Add("@NOMBRE",          p.Nombre);
        dp.Add("@FECHA",           p.Fecha);
        dp.Add("@NOTAASIGNACION",  p.NotaAsignacion);
        dp.Add("@NOTAEGRESO",      p.NotaEgreso);
        dp.Add("@FORMA_PAGO",      p.FormaPago);
        dp.Add("@MONTO_CAJA",      p.MontoCaja);
        dp.Add("@ID_CAJERO",       p.IdCajero);
        dp.Add("@ID_CAJA_FISICA",  p.IdCajaFisica);
        dp.Add("@ID_MASTER",       p.IdMaster);
        dp.Add("@REFERENCIA",      p.Referencia);
        dp.Add("@NOM_MAQUINA",     Dns.GetHostName());
        dp.Add("@IP_MAQUINA",      "127.0.0.1");
        dp.Add("@ElIdgps",         p.IdGps);
        dp.Add("@MSG", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("GUARDAR_PAGOS_CS_2026", dp, commandType: CommandType.StoredProcedure);
        var msg = dp.Get<string>("@MSG") ?? "";
        return msg.Equals("GUARDADO", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IEnumerable<PagoTempInfo>> ObtenerHistorialAsync(int idUsuario, DateTime desde, DateTime hasta)
    {
        using var conn = _factory.Create();
        var dp = new DynamicParameters();
        dp.Add("@Idu",         idUsuario);
        dp.Add("@fechainicio", desde);
        dp.Add("@fechafin",    hasta);
        dp.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 12);

        var rows = await conn.QueryAsync<dynamic>(
            "HISTORIAL_PAGO_SALARIO_CS", dp, commandType: CommandType.StoredProcedure);

        return rows.Select(r =>
        {
            var d = (IDictionary<string, object>)r;
            T Get<T>(string key, T def = default!) =>
                d.TryGetValue(key, out var v) && v != null ? (T)Convert.ChangeType(v, typeof(T)) : def;

            return new PagoTempInfo(
                IdGps:          Get<int>("IDHPF"),
                IdUsuario:      idUsuario,
                Nombre:         Get<string>("PAGADO POR") ?? "",
                Salario:        Get<decimal>("SALARIO"),
                Venta:          Get<decimal>("VENTA"),
                PorcVenta:      Get<decimal>("% VENTA"),
                TotalVenta:     Get<decimal>("TOTAL VENTA"),
                Cobranza:       Get<decimal>("COBRANZA"),
                PorcCobranza:   Get<decimal>("% COBRANZA"),
                TotalCobranza:  Get<decimal>("TOTAL COBRANZA"),
                Plus:           Get<decimal>("PLUS"),
                HorasExtras:    Get<decimal>("HORAS EXTRAS"),
                Bonificacion:   Get<decimal>("BONIFICACION"),
                OtrasComisiones:Get<decimal>("OTRAS COMISIONES"),
                TotalIngresos:  Get<decimal>("TOTAL INGRESOS"),
                Ausencias:      Get<decimal>("AUSENCIAS"),
                Adelantos:      Get<decimal>("ADELANTOS"),
                Ips:            Get<decimal>("IPS"),
                Cuotas:         Get<decimal>("CUOTAS"),
                Multas:         0m,
                Otros:          Get<decimal>("OTROS"),
                Equis:          Get<decimal>("EQUIS"),
                TotalEgresos:   Get<decimal>("TOTAL EGRESOS"),
                NotaAsignacion: Get<string>("NOTA ASIGNACION") ?? "",
                NotaEgreso:     Get<string>("NOTA EGRESO") ?? "",
                Fecha:          Get<string>("FECHA") ?? "");
        }).ToList();
    }

    public async Task<IEnumerable<HistorialPagoItem>> ObtenerHistorialPagosAsync(int idUsuario, DateTime desde, DateTime hasta)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<dynamic>(
            @"SELECT H.IDHPF,H.SALARIO,H.VENTA,H.PORCVENTA,H.TOTALVENTA,
                     H.COBRANZA,H.PORCCOBRANZA,H.TOTALCOBRANZA,
                     H.PLUS,H.HORASEXTRAS,H.BONIFICACION,H.OTRASCOMISIONES,H.TOTALINGRESOS,
                     H.AUSENCIAS,H.ADELANTOS,H.IPS,H.CUOTAS,
                     H.OTROS,H.EQUIS,H.TOTALEGRESOS,
                     H.NOMBRE,H.FECHA,
                     ISNULL(H.NOTAASIGNACION,'') AS NOTAASIGNACION,
                     ISNULL(H.NOTAEGRESO,'') AS NOTAEGRESO
              FROM HISTORIALPAGOFUN H
              WHERE H.IDU=@IdU AND H.FECHA BETWEEN @Desde AND @Hasta
              ORDER BY H.FECHA DESC",
            new { IdU = idUsuario, Desde = desde, Hasta = hasta.AddDays(1).AddSeconds(-1) });

        return rows.Select(r => new HistorialPagoItem(
            IdHpf:          Convert.ToInt32(r.IDHPF),
            Salario:        Convert.ToDecimal(r.SALARIO),
            Venta:          Convert.ToDecimal(r.VENTA),
            PorcVenta:      Convert.ToDecimal(r.PORCVENTA),
            TotalVenta:     Convert.ToDecimal(r.TOTALVENTA),
            Cobranza:       Convert.ToDecimal(r.COBRANZA),
            PorcCobranza:   Convert.ToDecimal(r.PORCCOBRANZA),
            TotalCobranza:  Convert.ToDecimal(r.TOTALCOBRANZA),
            Plus:           Convert.ToDecimal(r.PLUS),
            HorasExtras:    Convert.ToDecimal(r.HORASEXTRAS),
            Bonificacion:   Convert.ToDecimal(r.BONIFICACION),
            OtrasComisiones:Convert.ToDecimal(r.OTRASCOMISIONES),
            TotalIngresos:  Convert.ToDecimal(r.TOTALINGRESOS),
            Ausencias:      Convert.ToDecimal(r.AUSENCIAS),
            Adelantos:      Convert.ToDecimal(r.ADELANTOS),
            Ips:            Convert.ToDecimal(r.IPS),
            Cuotas:         Convert.ToDecimal(r.CUOTAS),
            Multas:         0m,
            Otros:          Convert.ToDecimal(r.OTROS),
            Equis:          Convert.ToDecimal(r.EQUIS),
            TotalEgresos:   Convert.ToDecimal(r.TOTALEGRESOS),
            Nombre:         r.NOMBRE?.ToString() ?? "",
            Fecha:          Convert.ToDateTime(r.FECHA),
            NotaAsignacion: r.NOTAASIGNACION?.ToString() ?? "",
            NotaEgreso:     r.NOTAEGRESO?.ToString() ?? "")).ToList();
    }

    public async Task<bool> ActualizarPagoAsync(int idHpf, GuardarPagoParams p)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            @"UPDATE HISTORIALPAGOFUN SET
                SALARIO=@Salario, VENTA=@Venta, PORCVENTA=@PorcVenta, TOTALVENTA=@TotalVenta,
                COBRANZA=@Cobranza, PORCCOBRANZA=@PorcCobranza, TOTALCOBRANZA=@TotalCobranza,
                PLUS=@Plus, HORASEXTRAS=@HorasExtras, BONIFICACION=@Bonificacion,
                OTRASCOMISIONES=@OtrasComisiones, TOTALINGRESOS=@TotalIngresos,
                AUSENCIAS=@Ausencias, ADELANTOS=@Adelantos, IPS=@Ips,
                CUOTAS=@Cuotas, OTROS=@Otros, EQUIS=@Equis,
                TOTALEGRESOS=@TotalEgresos,
                NOTAASIGNACION=@NotaAsignacion, NOTAEGRESO=@NotaEgreso
              WHERE IDHPF=@IdHpf",
            new {
                IdHpf = idHpf,
                p.Salario, p.Venta, p.PorcVenta, p.TotalVenta,
                p.Cobranza, p.PorcCobranza, p.TotalCobranza,
                p.Plus, p.HorasExtras, p.Bonificacion, p.OtrasComisiones, p.TotalIngresos,
                p.Ausencias, p.Adelantos, p.Ips, p.Cuotas, p.Otros, p.Equis, p.TotalEgresos,
                p.NotaAsignacion, p.NotaEgreso
            });
        return rows > 0;
    }

    public async Task<bool> EliminarPagoDefinitivoAsync(int idHpf)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM HISTORIALPAGOFUN WHERE IDHPF=@IdHpf",
            new { IdHpf = idHpf });
        return rows > 0;
    }

    public async Task<IEnumerable<DetalleVentaItem>> GetDetalleVentasAsync(int idUsuario, int idLocal, DateTime desde, DateTime hasta)
    {
        using var conn = _factory.Create();
        // La comisión de venta a crédito se calcula sobre la ENTREGA inicial cobrada al
        // momento de la venta, no sobre el precio total del producto (el saldo a crédito
        // lo cobra el cajero cuota a cuota, con su propia comisión de cobranza aparte).
        var rows = await conn.QueryAsync<dynamic>(
            @"SELECT CS.IDCAB, ISNULL(CAST(CS.NVENTA AS VARCHAR),'') AS NVENTA,
                     C.NOMBRE_CLIENTE, CS.FECHA, CS.ENTREGANORMAL
              FROM CABECERA_SALES CS
              INNER JOIN CLIENTES C ON CS.ID_CLIENTE = C.ID_CLIENTE
              WHERE CS.ID_USUARIO = @IdU AND CS.ID_LOCAL = @IdLocal
                AND CS.FECHA BETWEEN @Desde AND @Hasta
              ORDER BY CS.FECHA",
            new { IdU = idUsuario, IdLocal = idLocal, Desde = desde, Hasta = hasta });

        return rows.Select(r => new DetalleVentaItem(
            IdCab:         Convert.ToInt32(r.IDCAB),
            NVenta:        r.NVENTA?.ToString() ?? "",
            ClienteNombre: r.NOMBRE_CLIENTE?.ToString() ?? "",
            Fecha:         Convert.ToDateTime(r.FECHA),
            Total:         Convert.ToDecimal(r.ENTREGANORMAL ?? 0)));
    }

    public async Task<IEnumerable<DetalleCobranzaItem>> GetDetalleCobranzasAsync(int idUsuario, int idLocal, DateTime desde, DateTime hasta)
    {
        using var conn = _factory.Create();
        // Antes esto leía GENERADAS con ESTADO=1 (cuota COMPLETAMENTE pagada) — un abono
        // PARCIAL no cambia ESTADO hasta que se termina de pagar la cuota, así que la
        // comisión de ese abono no aparecía hasta el pago final, aunque el cobrador ya lo
        // hubiera cobrado. GENERADAS tampoco guarda el monto de cada abono por separado
        // (solo el total de la cuota), así que no hay forma de sacar "cuánto se cobró en
        // ESTE pago" desde ahí.
        // CAJA_DETALLE sí tiene una fila por cada pago (completo o parcial) con su MONTO
        // exacto — es la misma tabla que graba cada COBRO_SISTEMA (ver CobrarCuotaAsync /
        // CobrarCuotaParcialAsync), así que se lee de acá: la comisión de un abono parcial
        // queda proporcional al monto realmente abonado en ESE pago, sin esperar a que la
        // cuota se complete. SUBTIPO='COBRO_SISTEMA' ya excluye la entrega de la venta (esa
        // se graba con SUBTIPO='VENTA' en VentasWindows), así que no hace falta filtrar
        // NCUOTA<>1 como antes.
        // El nombre del cliente y el N° de cuota no están en columnas propias de
        // CAJA_DETALLE — se extraen del texto de CONCEPTO (mismo patrón regex que ya usa
        // CorregirCobradorCobrosAsync en AdicionalesWindows.cs), ya que REFERENCIA queda
        // vacía por defecto y no sirve para un JOIN contra GENERADAS.COMPROBANTE.
        // Sin filtro de local (mismo criterio que CalcularComisionesPeriodo / totalCobranza):
        // un vendedor cobra comisión de TODO lo que generó, sin importar en qué local ocurrió
        // el cobro — así que el detalle debe sumar exactamente el mismo total que se muestra
        // arriba, no una porción filtrada por local. idLocal queda sin usar acá a propósito.
        var rows = await conn.QueryAsync<dynamic>(
            @"SELECT D.ID_DETALLE, D.CONCEPTO, D.FECHA_HORA, D.MONTO
              FROM CAJA_DETALLE D
              WHERE D.ID_VENDEDOR = @IdU
                AND D.SUBTIPO = 'COBRO_SISTEMA' AND D.ESTADO_REG = 'V'
                AND D.FECHA_HORA BETWEEN @Desde AND @Hasta
              ORDER BY D.FECHA_HORA",
            new { IdU = idUsuario, Desde = desde, Hasta = hasta });

        return rows.Select(r =>
        {
            var concepto = r.CONCEPTO?.ToString() ?? "";
            var esParcial = concepto.StartsWith("ABONO PARCIAL", StringComparison.OrdinalIgnoreCase);
            var match = System.Text.RegularExpressions.Regex.Match(concepto,
                @"CUOTA N\S*?:\s*(\d+)\s*\|\s*COMPROBANTE:\s*\d+\s*\|\s*CLIENTE:\s*(.+)$");
            var nCuota = match.Success ? byte.Parse(match.Groups[1].Value) : (byte)0;
            var clienteNombre = match.Success ? match.Groups[2].Value.Trim() : "";
            var monto = Convert.ToDecimal(r.MONTO ?? 0);

            return new DetalleCobranzaItem(
                IdGeneradas:   Convert.ToInt32(r.ID_DETALLE),
                ClienteNombre: clienteNombre,
                NCuota:        nCuota,
                FechaCobrado:  Convert.ToDateTime(r.FECHA_HORA),
                Monto:         monto,
                Total:         monto,
                EsPagoParcial: esParcial);
        });
    }
}
