using CrediSoft.Core.Models;
using Dapper;
using System.Data;
using System.Net;

namespace CrediSoft.Data.Repositories;

public interface IVentaRepository
{
    Task<int> GuardarVentaCreditoAsync(VentaCreditoParams venta);
    Task<int> GuardarVentaContadoAsync(VentaContadoParams venta);
    Task<long> GuardarSolicitudAsync(SolicitudParams p);
    Task<IEnumerable<CabeceraVenta>> BuscarPorClienteAsync(int idCliente);
    Task<IEnumerable<CabeceraVenta>> CargarPorLocalAsync(int idLocal);
    Task<IEnumerable<CabeceraVenta>> BuscarPorPeriodoAsync(DateTime desde, DateTime hasta, int? idLocal = null);
    Task<bool> AnularVentaAsync(AnularVentaParams p);
    Task<int> ObtenerNumeroSolicitudAsync();
}

// ── Params records ─────────────────────────────────────────────────────────────

public record VentaCreditoParams(
    int IdCab, int NSol, byte IdLocal, int IdUsuario, int IdCliente,
    int IdGarante, int IdRef1, int IdRef2,
    string NomRef1, string TelRef1, string TrabRef1,
    string NomRef2, string TelRef2, string TrabRef2,
    string NomRefCom1, string TelRefCom1, string TrabRefCom1,
    string NomRefCom2, string TelRefCom2, string TrabRefCom2,
    byte FormaDeVenta, byte MetodoDeVenta, string NTarjeta,
    decimal Parcial, decimal Descuento, decimal Total,
    decimal EntregaNormal, decimal EntregaLogistica,
    byte Cuotas, decimal MontoCuota,
    decimal Debe, decimal Haber, decimal Cpha,
    byte Estado, decimal Tiva,
    // detalle artículo (el SP procesa un artículo por llamada)
    int IdDet, int IdArt, decimal Cantidad, decimal Pc, decimal Pv, decimal IvaArt, byte EsArt,
    int IdPrices, int IdMovArt, string Mov, string Mod, decimal StIni, decimal PCant,
    int IdSolicitud, int TCuotas,
    // caja
    int IdDetCaja, int IdCabCaja, byte Caja, int CountCaja,
    byte Accion, byte Concepto, decimal Monto, byte Metodo,
    string Numero, int Para, string Obs,
    int IdDoc, string NRecibo, string NPagare,
    DateTime? FechaInicioExterna, int NVenta,
    string Agente = "CS");

public record VentaContadoParams(
    int IdCab, int NSol, byte IdLocal, int IdUsuario, int IdCliente,
    int IdGarante, int IdRef1, int IdRef2,
    string NomRefCom1, string TelRefCom1, string TrabRefCom1,
    string NomRefCom2, string TelRefCom2, string TrabRefCom2,
    byte FormaDeVenta, byte MetodoDeVenta, string NTarjeta,
    decimal Parcial, decimal Descuento, decimal Total,
    decimal EntregaNormal, decimal EntregaLogistica,
    byte Cuotas, decimal MontoCuota,
    decimal Debe, decimal Haber, decimal Cpha,
    byte Estado, decimal Tiva,
    int IdDet, int IdCab2, int IdArt, decimal Cantidad, decimal Pc, decimal Pv, decimal IvaArt, byte EsArt,
    int IdMovArt, byte Mov, byte Mod, decimal StIni, decimal PCant, decimal PcAct,
    int IdCabCaja, int CountCaja, int IdDetCaja, byte Caja,
    byte Accion, byte Concepto, decimal Monto, byte Metodo,
    string Numero, int Para, string Obs, int NVenta,
    string Agente = "CS");

// Params para SOLICITUD_CS (crea CAB_SOL_SALES + DET_SOL_SALES)
public record SolicitudParams(
    // cabecera
    int Agente,
    long IdCabSol,      // OUTPUT en AGENTE=1, INPUT requerido en AGENTE>1
    string Numero, byte IdLocal, int IdUsuario, int IdCliente, int IdGarante,
    int IdRef1, int IdRef2,
    string NomRef1, string TelRef1, string TrabRef1,
    string NomRef2, string TelRef2, string TrabRef2,
    string NomRc1, string TelRc1, string TrabRc1,
    string NomRc2, string TelRc2, string TrabRc2,
    decimal ISalario, decimal IHonorario, decimal IConyuge, decimal IOtros, decimal ITotal,
    decimal EGasto, decimal ECuota, decimal EAlquiler, decimal EOtros, decimal ETotal,
    decimal TotalSale, decimal TotalEntrega,
    DateTime FechaCobro, byte CantCuotas, decimal TotalMontoCuota,
    string Nota, byte Estado,
    // detalle artículo (llamar una vez por artículo)
    int IdDetSol, int IdSolicitud, int IdArt,
    string Ca, string D, decimal Precio, decimal Entrega,
    byte CantCuotasDet, decimal CostoMensual, decimal ValorFinal,
    decimal Cant, decimal Subtotal);

public record AnularVentaParams(
    string Agente, int IdCab, byte IdLocal, int IdArt, decimal Cantidad,
    int IdUModStock, int IdMovArt, string Mov, string Mod, decimal StIni, decimal PCant,
    int IdDetCaja, int IdCabCaja, byte Caja, int CountCaja,
    byte Accion, byte Concepto, decimal Monto, byte Metodo,
    string Numero, int Para, string Obs, int IdCabecera);

// ── Repository ──────────────────────────────────────────────────────────────────

public class VentaRepository : IVentaRepository
{
    private readonly IDbConnectionFactory _factory;
    public VentaRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<int> ObtenerNumeroSolicitudAsync()
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var result = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "BUSCAR_NUM_SOL_CS", p, commandType: CommandType.StoredProcedure);
        return result != null ? (int)result.SOL : 1;
    }

    public async Task<int> GuardarVentaCreditoAsync(VentaCreditoParams v)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@AGENTE", v.Agente);
        p.Add("@IDCAB", v.IdCab); p.Add("@NSOL", v.NSol);
        p.Add("@ID_LOCAL", v.IdLocal); p.Add("@ID_USUARIO", v.IdUsuario);
        p.Add("@ID_CLIENTE", v.IdCliente); p.Add("@ID_GARANTE", v.IdGarante);
        p.Add("@ID_REFERENCIA1", v.IdRef1); p.Add("@ID_REFERENCIA2", v.IdRef2);
        p.Add("@NOM_REFERENCIA1", v.NomRef1); p.Add("@TEL_REFERENCIA1", v.TelRef1); p.Add("@TRAB_REFERENCIA1", v.TrabRef1);
        p.Add("@NOM_REFERENCIA2", v.NomRef2); p.Add("@TEL_REFERENCIA2", v.TelRef2); p.Add("@TRAB_REFERENCIA2", v.TrabRef2);
        p.Add("@NOM_REFERENCIACOMERCIAL1", v.NomRefCom1); p.Add("@TEL_REFERENCIACOMERCIAL1", v.TelRefCom1); p.Add("@TRAB_REFERENCIACOMERCIAL1", v.TrabRefCom1);
        p.Add("@NOM_REFERENCIACOMERCIAL2", v.NomRefCom2); p.Add("@TEL_REFERENCIACOMERCIAL2", v.TelRefCom2); p.Add("@TRAB_REFERENCIACOMERCIAL2", v.TrabRefCom2);
        p.Add("@FORMA_DE_VENTA", v.FormaDeVenta); p.Add("@METODO_DE_VENTA", v.MetodoDeVenta); p.Add("@NTARJETA", v.NTarjeta);
        p.Add("@PARCIAL", v.Parcial); p.Add("@DESCUENTO", v.Descuento); p.Add("@TOTAL", v.Total);
        p.Add("@ENTREGANORMAL", v.EntregaNormal); p.Add("@ENTREGALOGISTICA", v.EntregaLogistica);
        p.Add("@CUOTAS", v.Cuotas); p.Add("@MONTO_CUOTA", v.MontoCuota);
        p.Add("@DEBE", v.Debe); p.Add("@HABER", v.Haber); p.Add("@CPHA", v.Cpha);
        p.Add("@ESTADO", v.Estado); p.Add("@TIVA", v.Tiva);
        p.Add("@iddet", v.IdDet); p.Add("@idart", v.IdArt); p.Add("@cantidad", v.Cantidad);
        p.Add("@pc", v.Pc); p.Add("@pv", v.Pv); p.Add("@iva", v.IvaArt); p.Add("@es", v.EsArt);
        p.Add("@IDPRICES", v.IdPrices); p.Add("@IDMOVART", v.IdMovArt);
        p.Add("@MOV", v.Mov); p.Add("@MOD", v.Mod); p.Add("@STINI", v.StIni); p.Add("@PCANT", v.PCant);
        p.Add("@idsolicitud", v.IdSolicitud); p.Add("@TCUOTAS", v.TCuotas);
        p.Add("@ID_DET_CAJA", v.IdDetCaja); p.Add("@IDCABCAJA", v.IdCabCaja);
        p.Add("@CAJA", v.Caja); p.Add("@COUNTCAJA", v.CountCaja);
        p.Add("@ACCION", v.Accion); p.Add("@CONCEPTO", v.Concepto); p.Add("@MONTO", v.Monto);
        p.Add("@METODO", v.Metodo); p.Add("@NUMERO", v.Numero); p.Add("@PARA", v.Para); p.Add("@OBS", v.Obs);
        p.Add("@IDDOC", v.IdDoc); p.Add("@NRECIBO", v.NRecibo); p.Add("@NPAGARE", v.NPagare);
        p.Add("@FechaInicioExterna", v.FechaInicioExterna); p.Add("@NVenta", v.NVenta);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("GUARDAR_VENTA_CREDITO_CS", p, commandType: CommandType.StoredProcedure);
        // Obtener IDCAB generado
        return await conn.ExecuteScalarAsync<int>(
            "SELECT MAX(IDCAB) FROM CABECERA_SALES WHERE ID_CLIENTE=@C AND ID_LOCAL=@L",
            new { C = v.IdCliente, L = v.IdLocal });
    }

    public async Task<long> GuardarSolicitudAsync(SolicitudParams s)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@AGENTE",            s.Agente);
        p.Add("@IDCABSOL",          s.IdCabSol, DbType.Int64, ParameterDirection.InputOutput);
        p.Add("@NUMERO",            s.Numero);
        p.Add("@IDLOCAL",           s.IdLocal);
        p.Add("@ID_USUARIO",        s.IdUsuario);
        p.Add("@ID_CLIENTE",        s.IdCliente);
        p.Add("@ID_GARANTE",        s.IdGarante);
        p.Add("@ID_REF1",           s.IdRef1);
        p.Add("@ID_REF2",           s.IdRef2);
        p.Add("@NOM_REFERENCIA1",   s.NomRef1);    p.Add("@TEL_REFERENCIA1",  s.TelRef1);   p.Add("@TRAB_REFERENCIA1", s.TrabRef1);
        p.Add("@NOM_REFERENCIA2",   s.NomRef2);    p.Add("@TEL_REFERENCIA2",  s.TelRef2);   p.Add("@TRAB_REFERENCIA2", s.TrabRef2);
        p.Add("@NOM_RC1",           s.NomRc1);     p.Add("@TEL_RC1",          s.TelRc1);    p.Add("@TRAB_RC1",         s.TrabRc1);
        p.Add("@NOM_RC2",           s.NomRc2);     p.Add("@TEL_RC2",          s.TelRc2);    p.Add("@TRAB_RC2",         s.TrabRc2);
        p.Add("@I_SALARIO",         s.ISalario);   p.Add("@I_HONORARIO",      s.IHonorario); p.Add("@I_CONYUGE",       s.IConyuge);
        p.Add("@I_OTROS",           s.IOtros);     p.Add("@I_TOTAL",          s.ITotal);
        p.Add("@E_GASTO",           s.EGasto);     p.Add("@E_CUOTA",          s.ECuota);    p.Add("@E_ALQUILER",       s.EAlquiler);
        p.Add("@E_OTROS",           s.EOtros);     p.Add("@E_TOTAL",          s.ETotal);
        p.Add("@TOTALSALE",         s.TotalSale);  p.Add("@TOTALENTREGA",     s.TotalEntrega);
        p.Add("@FECHA_COBRO",       s.FechaCobro);
        p.Add("@CANTCUOTAS",        s.CantCuotas); p.Add("@TOTALMONTOCUOTA",  s.TotalMontoCuota);
        p.Add("@NOTA",              s.Nota);       p.Add("@ESTADO",           s.Estado);
        p.Add("@ID_DET_SOL",        s.IdDetSol);   p.Add("@ID_SOLICITUD",     s.IdSolicitud);
        p.Add("@IDART",             s.IdArt);
        p.Add("@CA",                s.Ca);         p.Add("@D",                s.D);
        p.Add("@PRECIO",            s.Precio);     p.Add("@ENTREGA",          s.Entrega);
        p.Add("@CANTCUOTASDET",     s.CantCuotasDet);
        p.Add("@COSTOMENSUAL",      s.CostoMensual); p.Add("@VALORFINAL",     s.ValorFinal);
        p.Add("@CANT",              s.Cant);       p.Add("@SUBTOTAL",         s.Subtotal);
        p.Add("@Msg",               dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("SOLICITUD_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@Msg");
        if (string.IsNullOrWhiteSpace(msg) || msg.StartsWith("Error"))
            throw new Exception(string.IsNullOrWhiteSpace(msg) ? "SOLICITUD_CS no procesó la solicitud." : msg);
        return p.Get<long>("@IDCABSOL");
    }

    public async Task<int> GuardarVentaContadoAsync(VentaContadoParams v)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@AGENTE", v.Agente);
        p.Add("@IDCAB", v.IdCab); p.Add("@NSOL", v.NSol);
        p.Add("@ID_LOCAL", v.IdLocal); p.Add("@ID_USUARIO", v.IdUsuario);
        p.Add("@ID_CLIENTE", v.IdCliente); p.Add("@ID_GARANTE", v.IdGarante);
        p.Add("@ID_REFERENCIA1", v.IdRef1); p.Add("@ID_REFERENCIA2", v.IdRef2);
        p.Add("@NOM_REFERENCIACOMERCIAL1", v.NomRefCom1); p.Add("@TEL_REFERENCIACOMERCIAL1", v.TelRefCom1); p.Add("@TRAB_REFERENCIACOMERCIAL1", v.TrabRefCom1);
        p.Add("@NOM_REFERENCIACOMERCIAL2", v.NomRefCom2); p.Add("@TEL_REFERENCIACOMERCIAL2", v.TelRefCom2); p.Add("@TRAB_REFERENCIACOMERCIAL2", v.TrabRefCom2);
        p.Add("@FORMA_DE_VENTA", v.FormaDeVenta); p.Add("@METODO_DE_VENTA", v.MetodoDeVenta); p.Add("@NTARJETA", v.NTarjeta);
        p.Add("@PARCIAL", v.Parcial); p.Add("@DESCUENTO", v.Descuento); p.Add("@TOTAL", v.Total);
        p.Add("@ENTREGANORMAL", v.EntregaNormal); p.Add("@ENTREGALOGISTICA", v.EntregaLogistica);
        p.Add("@CUOTAS", v.Cuotas); p.Add("@MONTO_CUOTA", v.MontoCuota);
        p.Add("@DEBE", v.Debe); p.Add("@HABER", v.Haber); p.Add("@CPHA", v.Cpha);
        p.Add("@ESTADO", v.Estado); p.Add("@TIVA", v.Tiva);
        p.Add("@iddet", v.IdDet); p.Add("@idcab2", v.IdCab2); p.Add("@idart", v.IdArt);
        p.Add("@cantidad", v.Cantidad); p.Add("@pc", v.Pc); p.Add("@pv", v.Pv);
        p.Add("@iva", v.IvaArt); p.Add("@es", v.EsArt);
        p.Add("@IDMOVART", v.IdMovArt); p.Add("@MOV", v.Mov); p.Add("@MOD", v.Mod);
        p.Add("@STINI", v.StIni); p.Add("@PCANT", v.PCant); p.Add("@PCACT", v.PcAct);
        p.Add("@IDCABCAJA", v.IdCabCaja); p.Add("@COUNTCAJA", v.CountCaja);
        p.Add("@ID_DET_CAJA", v.IdDetCaja); p.Add("@CAJA", v.Caja);
        p.Add("@ACCION", v.Accion); p.Add("@CONCEPTO", v.Concepto); p.Add("@MONTO", v.Monto);
        p.Add("@METODO", v.Metodo); p.Add("@NUMERO", v.Numero); p.Add("@PARA", v.Para); p.Add("@OBS", v.Obs);
        p.Add("@NVenta", v.NVenta);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("GUARDAR_VENTA_CONTADO_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@msg");
        if (string.IsNullOrWhiteSpace(msg) || msg.Trim() == "Error")
            throw new Exception(string.IsNullOrWhiteSpace(msg)
                ? "El SP no procesó la venta (AGENTE inválido o parámetros incorrectos)."
                : $"El SP reportó un error interno.");
        // devolver NVENTA (número visible para el usuario)
        return await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 NVENTA FROM CABECERA_SALES WHERE ID_CLIENTE=@C AND ID_LOCAL=@L ORDER BY IDCAB DESC",
            new { C = v.IdCliente, L = v.IdLocal });
    }

    public async Task<IEnumerable<CabeceraVenta>> BuscarPorClienteAsync(int idCliente)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CabeceraVenta>(
            "SELECT cs.IDCAB as IdCab, cs.NSOLICITUD as NSolicitud, " +
            "cs.TOTAL as Total, cs.CUOTAS as Cuotas, cs.MONTO_CUOTA as MontoCuota, " +
            "cs.FECHA as Fecha, cs.ESTADO as Estado, cs.FORMA_DE_VENTA as FormaDeVenta, " +
            "cs.NVENTA as NVenta, l.NOMBRE as LocalNombre " +   // NOMBRE no NOMBRE_LOCAL
            "FROM CABECERA_SALES cs " +
            "INNER JOIN LOCALES l ON cs.ID_LOCAL = l.ID_LOCAL " +
            "WHERE cs.ID_CLIENTE = @Id AND cs.ESTADO = 1 ORDER BY cs.FECHA DESC",
            new { Id = idCliente });
    }

    public async Task<IEnumerable<CabeceraVenta>> CargarPorLocalAsync(int idLocal)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@IdLocal", idLocal);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 12);
        return await conn.QueryAsync<CabeceraVenta>(
            "CARGAR_VENTAS_CREDITO_CS", p, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<CabeceraVenta>> BuscarPorPeriodoAsync(
        DateTime desde, DateTime hasta, int? idLocal = null)
    {
        using var conn = _factory.Create();
        var sql =
            "SELECT cs.IDCAB as IdCab, cs.NSOLICITUD as NSolicitud, " +
            "cs.TOTAL as Total, cs.CUOTAS as Cuotas, cs.FECHA as Fecha, " +
            "cs.ESTADO as Estado, cs.FORMA_DE_VENTA as FormaDeVenta, " +
            "c.NOMBRE_CLIENTE as ClienteNombre, l.NOMBRE as LocalNombre " +
            "FROM CABECERA_SALES cs " +
            "INNER JOIN CLIENTES c ON cs.ID_CLIENTE = c.ID_CLIENTE " +
            "INNER JOIN LOCALES l ON cs.ID_LOCAL = l.ID_LOCAL " +
            "WHERE cs.FECHA BETWEEN @Desde AND @Hasta AND cs.ESTADO = 1 ";
        if (idLocal.HasValue) sql += "AND cs.ID_LOCAL = @Local ";
        sql += "ORDER BY cs.FECHA DESC";

        return await conn.QueryAsync<CabeceraVenta>(sql,
            new { Desde = desde, Hasta = hasta.AddDays(1), Local = idLocal });
    }

    public async Task<bool> AnularVentaAsync(AnularVentaParams v)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@AGENTE", v.Agente); p.Add("@idcab", v.IdCab); p.Add("@idlocal", v.IdLocal);
        p.Add("@idart", v.IdArt); p.Add("@cantidad", v.Cantidad);
        p.Add("@IDUMODSTOCK", v.IdUModStock); p.Add("@IDMOVART", v.IdMovArt);
        p.Add("@MOV", v.Mov); p.Add("@MOD", v.Mod); p.Add("@STINI", v.StIni); p.Add("@PCANT", v.PCant);
        p.Add("@ID_DET_CAJA", v.IdDetCaja); p.Add("@IDCABCAJA", v.IdCabCaja);
        p.Add("@CAJA", v.Caja); p.Add("@COUNTCAJA", v.CountCaja);
        p.Add("@ACCION", v.Accion); p.Add("@CONCEPTO", v.Concepto); p.Add("@MONTO", v.Monto);
        p.Add("@METODO", v.Metodo); p.Add("@NUMERO", v.Numero); p.Add("@PARA", v.Para); p.Add("@OBS", v.Obs);
        p.Add("@idcabecera", v.IdCabecera);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
        await conn.ExecuteAsync("ELIMINAR_VENTA_CREDITO_CS", p, commandType: CommandType.StoredProcedure);
        return true;
    }
}
