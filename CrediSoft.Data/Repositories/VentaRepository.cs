using CrediSoft.Core.Models;
using Dapper;
using System.Data;
using System.Net;

namespace CrediSoft.Data.Repositories;

public interface IVentaRepository
{
    Task<int> GuardarVentaCreditoAsync(VentaCreditoParams venta);
    Task<(int IdCab, int NVenta)> GuardarVentaContadoAsync(VentaContadoParams venta);
    // Para AGENTE=1 devuelve el IDCABSOL y el NUMERO real generados (calculados acá mismo,
    // no adivinados de antemano — ver comentario en la implementación). Para AGENTE>1
    // (detalle adicional del mismo carrito) Numero viene vacío, se ignora.
    Task<(long IdCabSol, string Numero)> GuardarSolicitudAsync(SolicitudParams p);
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
    int IdPrices, int IdMovArt, int Mov, int Mod, decimal StIni, decimal PCant,
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
    // sp_Guardar_Venta_Contado_CS: IdUsuario = quién vendió (comisión, CABECERA_SALES.
    // ID_USUARIO/ID_VENDEDOR) — puede ser distinto de IdCajero (dueño real de la caja
    // física usada, CAJA_DETALLE.ID_CAJERO), igual que IdUsuarioSesion en cobros.
    int IdCajero, byte IdCajaFisica, string FormaPago, decimal MontoCaja, string Referencia,
    int Agente = 1, int Ultimo = 1);

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
    decimal Cant, decimal Subtotal,
    // "Bueno", "Malo", o "" (no consultado) — ver comentario en ContarCreditosPreviosAsync
    string EstadoInforconf = "");

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

    // Reemplaza el antiguo EXEC GUARDAR_VENTA_CREDITO_CS por INSERT directo — mismo motivo que
    // GuardarSolicitudAsync: el SP es compartido con CrediMar.exe (VB6), que lo llama
    // posicionalmente. Cualquier ajuste de lógica interna (incluso sin tocar parámetros)
    // puede romper el binario legado sin aviso. Se replica acá exactamente la secuencia del
    // SP (generación de IDs vía MAX(...)+1, CABECERA_SALES/DETALLES_SALES, stock en
    // PRICES+MOVART, cuotas en GENERADAS — reemplazando AGREGAR_GENERADAS_CS —, CAB_SOL_SALES,
    // DET_CAJA, DOCUMENTOS), en una transacción explícita propia.
    //
    // Fix incluido acá (pedido explícito, no aplicado al SP legado): la cuota 1 (Entrega)
    // queda ESTADO=0 (Pendiente) cuando EntregaNormal=0, en vez de quedar siempre "Cobrada"
    // como hacía AGREGAR_GENERADAS_CS sin mirar el monto real.
    //
    // No se inserta en CAJA_DETALLE acá (a diferencia del SP legado, que sí lo hacía): ese
    // insert ya lo hace VentasWindows.InsertarMovimientoCajaAsync justo después de llamar a
    // este método — hacerlo también acá duplicaba el movimiento de caja de la entrega.
    public async Task<int> GuardarVentaCreditoAsync(VentaCreditoParams v)
    {
        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            int idCab;
            if (string.Equals(v.Agente, "SI", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(v.NRecibo))
                {
                    var yaExiste = await conn.ExecuteScalarAsync<bool>(
                        "SELECT CASE WHEN EXISTS (SELECT 1 FROM DOCUMENTOS WHERE NRECIBO=@r) THEN 1 ELSE 0 END",
                        new { r = v.NRecibo }, tx);
                    if (yaExiste)
                        throw new Exception($"El N° de Recibo \"{v.NRecibo}\" ya fue usado en otro documento.");
                }

                idCab = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDCAB), 0) + 1 FROM CABECERA_SALES WITH (TABLOCKX)", transaction: tx);
                int nVenta = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(NVENTA), 0) + 1 FROM CABECERA_SALES WITH (TABLOCKX)", transaction: tx);
                int idDet = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDDET), 0) + 1 FROM DETALLES_SALES WITH (TABLOCKX)", transaction: tx);
                int idMovArt = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDMOVART), 0) + 1 FROM MOVART WITH (TABLOCKX)", transaction: tx);
                int idDoc = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDDOC), 0) + 1 FROM DOCUMENTOS WITH (TABLOCKX)", transaction: tx);
                string formatoNVenta = nVenta.ToString().PadLeft(9, '0');

                await conn.ExecuteAsync(
                    "INSERT INTO CABECERA_SALES (IDCAB,NSOLICITUD,ID_LOCAL,ID_USUARIO,ID_CLIENTE,ID_GARANTE," +
                    " ID_REFERENCIA1,ID_REFERENCIA2," +
                    " NOM_REFERENCIA1,TEL_REFERENCIA1,TRAB_REFERENCIA1," +
                    " NOM_REFERENCIA2,TEL_REFERENCIA2,TRAB_REFERENCIA2," +
                    " NOM_REFERENCIACOMERCIAL1,TEL_REFERENCIACOMERCIAL1,TRAB_REFERENCIACOMERCIAL1," +
                    " NOM_REFERENCIACOMERCIAL2,TEL_REFERENCIACOMERCIAL2,TRAB_REFERENCIACOMERCIAL2," +
                    " FORMA_DE_VENTA,METODO_DE_VENTA,NTARJETA,PARCIAL," +
                    " DESCUENTO,TOTAL,ENTREGANORMAL,ENTREGALOGISTICA,CUOTAS,MONTO_CUOTA,FECHA," +
                    " DEBE,HABER,CPHA,ESTADO,TIVA,NVENTA,NVENTACHAR) " +
                    "VALUES (@IdCab,@NSol,@IdLocal,@IdUsuario,@IdCliente,@IdGarante," +
                    " @IdRef1,@IdRef2," +
                    " @NomRef1,@TelRef1,@TrabRef1," +
                    " @NomRef2,@TelRef2,@TrabRef2," +
                    " @NomRefCom1,@TelRefCom1,@TrabRefCom1," +
                    " @NomRefCom2,@TelRefCom2,@TrabRefCom2," +
                    " @FormaDeVenta,@MetodoDeVenta,@NTarjeta,@Parcial," +
                    " @Descuento,@Total,@EntregaNormal,@EntregaLogistica,@Cuotas,@MontoCuota,GETDATE()," +
                    " @Debe,@Haber,@Cpha,@Estado,@Tiva,@NVenta,@FormatoNVenta)",
                    new
                    {
                        IdCab = idCab, v.NSol, v.IdLocal, v.IdUsuario, v.IdCliente, v.IdGarante,
                        v.IdRef1, v.IdRef2,
                        v.NomRef1, v.TelRef1, v.TrabRef1,
                        v.NomRef2, v.TelRef2, v.TrabRef2,
                        v.NomRefCom1, v.TelRefCom1, v.TrabRefCom1,
                        v.NomRefCom2, v.TelRefCom2, v.TrabRefCom2,
                        v.FormaDeVenta, v.MetodoDeVenta, v.NTarjeta, v.Parcial,
                        v.Descuento, v.Total, v.EntregaNormal, v.EntregaLogistica, v.Cuotas, v.MontoCuota,
                        v.Debe, v.Haber, v.Cpha, v.Estado, v.Tiva, NVenta = nVenta, FormatoNVenta = formatoNVenta
                    }, tx);

                await InsertarDetalleVentaAsync(conn, tx, idDet, idCab, v.IdArt, v.Cantidad, v.Pc, v.Pv, v.IvaArt, v.EsArt, formatoNVenta, v.IdLocal, v.IdUsuario, idMovArt, v.Mov, v.Mod);

                await GenerarCuotasAsync(conn, tx, idCab, v.TCuotas, v.MontoCuota, v.EntregaNormal, v.IdLocal, v.IdUsuario, v.FechaInicioExterna ?? DateTime.Today);

                if (v.IdSolicitud > 0)
                    await conn.ExecuteAsync(
                        "UPDATE CAB_SOL_SALES SET ESTADO=1, FECHA_APROB=GETDATE() WHERE IDSOLICITUD=@id",
                        new { id = v.IdSolicitud }, tx);

                // DET_CAJA (legado) — se mantiene igual que hacía el SP; CAJA_DETALLE (sistema
                // nuevo) lo inserta el llamador (InsertarMovimientoCajaAsync), no acá.
                int idDetCaja = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(ID_DET_CAJA), 0) + 1 FROM DET_CAJA WITH (TABLOCKX)", transaction: tx);
                int idCabCaja = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(ID), 1) FROM CAB_CAJA", transaction: tx);
                int countCaja = await conn.ExecuteScalarAsync<int?>(
                    "SELECT MAX(COUNTCAJA) FROM CAB_CAJA", transaction: tx) ?? 1;
                await conn.ExecuteAsync(
                    "INSERT INTO DET_CAJA (ID_DET_CAJA,IDCABCAJA,ID_LOCAL,ID_USUARIO,CAJA,COUNTCAJA,NUMOPERACION,ACCION,CONCEPTO,MONTO,METODO,NUMERO,FECHA,OBS,IDCAB) " +
                    "VALUES (@IdDetCaja,@IdCabCaja,@IdLocal,@IdUsuario,@Caja,@CountCaja,@IdDetCaja,@Accion,@Concepto,@Monto,@Metodo,@Numero,GETDATE(),@Obs,@IdCab)",
                    new
                    {
                        IdDetCaja = idDetCaja, IdCabCaja = idCabCaja, v.IdLocal, v.IdUsuario,
                        v.Caja, CountCaja = countCaja, v.Accion, v.Concepto, v.Monto, v.Metodo, v.Numero, v.Obs, IdCab = idCab
                    }, tx);

                await conn.ExecuteAsync(
                    "INSERT INTO DOCUMENTOS (IDDOC,IDCAB,IDCLIE,IDLOCAL,NRECIBO,NPAGARE,FECHARECIBO,FECHAPAGARE,ESTADO) " +
                    "VALUES (@IdDoc,@IdCab,@IdCliente,@IdLocal,@NRecibo,@NPagare,GETDATE(),GETDATE(),1)",
                    new { IdDoc = idDoc, IdCab = idCab, v.IdCliente, v.IdLocal, v.NRecibo, v.NPagare }, tx);
            }
            else
            {
                // AGENTE=NO: artículo adicional del mismo carrito, sobre la venta ya creada.
                idCab = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDCAB), 0) FROM CABECERA_SALES WITH (TABLOCKX)", transaction: tx);
                int idDet = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDDET), 0) + 1 FROM DETALLES_SALES WITH (TABLOCKX)", transaction: tx);
                int idMovArt = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(IDMOVART), 0) + 1 FROM MOVART WITH (TABLOCKX)", transaction: tx);
                int nVenta = await conn.ExecuteScalarAsync<int>(
                    "SELECT ISNULL(MAX(NVENTA), 0) FROM CABECERA_SALES WITH (TABLOCKX)", transaction: tx);
                string formatoNVenta = nVenta.ToString().PadLeft(9, '0');

                await InsertarDetalleVentaAsync(conn, tx, idDet, idCab, v.IdArt, v.Cantidad, v.Pc, v.Pv, v.IvaArt, v.EsArt, formatoNVenta, v.IdLocal, v.IdUsuario, idMovArt, v.Mov, v.Mod);
            }

            tx.Commit();
            return idCab;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task InsertarDetalleVentaAsync(IDbConnection conn, IDbTransaction tx, int idDet, int idCab, int idArt, decimal cantidad, decimal pc, decimal pv, decimal iva, byte es, string formatoNVenta, byte idLocal, int idUsuario, int idMovArt, int mov, int mod)
    {
        await conn.ExecuteAsync(
            "INSERT INTO DETALLES_SALES (iddet,idcab,idart,cantidad,pc,pv,iva,es,NVENTACHAR) " +
            "VALUES (@IdDet,@IdCab,@IdArt,@Cantidad,@Pc,@Pv,@Iva,@Es,@FormatoNVenta)",
            new { IdDet = idDet, IdCab = idCab, IdArt = idArt, Cantidad = cantidad, Pc = pc, Pv = pv, Iva = iva, Es = es, FormatoNVenta = formatoNVenta }, tx);

        decimal stockAnterior = await conn.ExecuteScalarAsync<decimal?>(
            "SELECT S FROM PRICES WHERE IDART=@IdArt AND IDLOCAL=@IdLocal",
            new { IdArt = idArt, IdLocal = idLocal }, tx) ?? 0;
        decimal costoAnterior = await conn.ExecuteScalarAsync<decimal?>(
            "SELECT PC FROM PRICES WHERE IDART=@IdArt AND IDLOCAL=@IdLocal",
            new { IdArt = idArt, IdLocal = idLocal }, tx) ?? 0;

        await conn.ExecuteAsync(
            "UPDATE PRICES SET S=S-@Cantidad,FMS=GETDATE(),IDUMODSTOCK=@IdUsuario WHERE IDART=@IdArt AND IDLOCAL=@IdLocal",
            new { Cantidad = cantidad, IdUsuario = idUsuario, IdArt = idArt, IdLocal = idLocal }, tx);

        await conn.ExecuteAsync(
            "INSERT INTO MOVART (IDMOVART,IDART,MOV,MOD,STINI,CANT,IDLOCAL,IDDESTINO,PCANT,PCACT,IDU,FECHA) " +
            "VALUES (@IdMovArt,@IdArt,@Mov,@Mod,@StIni,@Cantidad,@IdLocal,@IdLocal,@PcAnt,@PcAnt,@IdUsuario,GETDATE())",
            new { IdMovArt = idMovArt, IdArt = idArt, Mov = mov, Mod = mod, StIni = stockAnterior, Cantidad = cantidad, IdLocal = idLocal, PcAnt = costoAnterior, IdUsuario = idUsuario }, tx);
    }

    // Reemplaza EXEC AGREGAR_GENERADAS_CS — misma lógica de generación de cuotas (mensuales,
    // reiniciando el correlativo de NCUOTA cada 12) y misma cantidad total de filas que el
    // sistema viejo: NCUOTA=1 es siempre la fila de la Entrega (exista monto o sea $0 —
    // patrón confirmado en TODO el historial real: IDCAB 33480/33479/33473, CUOTAS=2 en
    // CABECERA_SALES → siempre 3 filas en GENERADAS), y NCUOTA=2..tCuotas son las cuotas
    // reales pactadas. tCuotas ya viene como "cuotas pactadas + 1" desde el llamador
    // (RegistrarVentaAprobadaAsync) para incluir esa fila.
    //
    // Fix real (el único cambio de comportamiento frente al SP legado): "Confirmar Entrega"
    // ES el momento en que el cajero confirma la entrega frente al cliente — con monto real
    // o con Gs. 0 (venta sin entrega pactada), ese paso ya quedó resuelto en ese instante, así
    // que la fila de Entrega siempre nace ESTADO=1 (Cobrada). El SP legado ya hacía esto
    // (CASE WHEN @NCuota=1 THEN 1) — no hay cambio de comportamiento acá, solo se preserva.
    private static async Task GenerarCuotasAsync(IDbConnection conn, IDbTransaction tx, int idCab, int tCuotas, decimal monto, decimal entregaNormal, byte idLocal, int idUsuario, DateTime fechaInicioExterna)
    {
        string comprobante = idCab.ToString().PadLeft(12, '0');
        int maxId = await conn.ExecuteScalarAsync<int?>("SELECT MAX(IDGENERADAS) FROM GENERADAS", transaction: tx) ?? 0;
        DateTime fechaBase = fechaInicioExterna.AddMonths(-2);
        int nCuota = 1;

        for (int i = 1; i <= tCuotas; i++)
        {
            fechaBase = fechaBase.AddMonths(1);
            int idGeneradas = maxId + i;
            bool esFilaEntrega = nCuota == 1;
            decimal montoFila = esFilaEntrega ? entregaNormal : monto;
            DateTime vto = fechaBase.AddMonths(1);
            byte estado = (byte)(esFilaEntrega ? 1 : 0);
            DateTime fechaCobrado = esFilaEntrega ? DateTime.Now : vto;
            decimal total = esFilaEntrega ? entregaNormal : 0;

            await conn.ExecuteAsync(
                "INSERT INTO GENERADAS (IDGENERADAS,IDCAB,COMPROBANTE,NCUOTA,MONTO,VTO,FECHACOBRADO,MORA,PUNITORIO,REAJUSTE,TOTAL,IDLOCAL,IDU,OBS,ENTREGA,ESTADO) " +
                "VALUES (@IdGeneradas,@IdCab,@Comprobante,@NCuota,@Monto,@Vto,@FechaCobrado,0,0,0,@Total,@IdLocal,@IdU,'x',0,@Estado)",
                new
                {
                    IdGeneradas = idGeneradas, IdCab = idCab, Comprobante = comprobante, NCuota = nCuota,
                    Monto = montoFila, Vto = vto, FechaCobrado = fechaCobrado, Total = total,
                    IdLocal = idLocal, IdU = idUsuario, Estado = estado
                }, tx);

            nCuota = nCuota == 12 ? 1 : nCuota + 1;
        }
    }

    // Reemplaza el antiguo EXEC SOLICITUD_CS por INSERT directo — el SP legado es compartido
    // con CrediMar.exe (VB6), que arma la llamada POSICIONALMENTE sin nombrar parámetros.
    // Cualquier columna que ElectroMar necesite pero el VB6 no conozca (ej. ESTADO_INFORCONF)
    // desalinea esa llamada legada y rompe Solicitudes ahí — ya pasó una vez (27/07). Sacar
    // a ElectroMar de esa dependencia evita que un cambio en un lado rompa al otro.
    public async Task<(long IdCabSol, string Numero)> GuardarSolicitudAsync(SolicitudParams s)
    {
        using var conn = _factory.Create();

        if (s.Agente == 1)
        {
            // Bug real detectado (28/07): antes el número de solicitud venía de
            // ObtenerNumeroSolicitudAsync (lee CONTADORES.SOL sin incrementar — el
            // incremento lo hacía SOLICITUD_CS, que dejamos de usar). Sin ese incremento,
            // TODAS las solicitudes nuevas reutilizaban el mismo número, y al aprobar la
            // segunda, VentasWindows.RegistrarVentaAprobadaAsync (SELECT MAX(IDCAB) por
            // cliente/local) terminaba "secuestrando" una venta antigua ajena que ya tenía
            // ese NUMERO, sobrescribiéndole el NSOLICITUD (caso real: solicitud 8729,
            // apropiándose de la venta de Miguel Gimenez Vera del 25/07). Ahora el número
            // se calcula acá mismo, a partir del NUMERO real más alto en CAB_SOL_SALES
            // (no de un contador aparte que puede desincronizarse, como pasó tras restaurar
            // un backup), bajo el mismo TABLOCKX que ya protege el IDSOLICITUD.
            var fila = await conn.QuerySingleAsync<(long NuevoId, string NuevoNumero)>(
                "DECLARE @NuevoId BIGINT; DECLARE @NuevoNumero CHAR(15); " +
                "SELECT @NuevoId = ISNULL(MAX(IDSOLICITUD), 0) + 1, " +
                "       @NuevoNumero = RIGHT('000000000000000' + CAST(ISNULL(MAX(CAST(NUMERO AS BIGINT)), 0) + 1 AS VARCHAR(15)), 15) " +
                "FROM CAB_SOL_SALES WITH (TABLOCKX); " +
                "INSERT INTO CAB_SOL_SALES " +
                "(IDSOLICITUD, NUMERO, ID_LOCAL, ID_USUARIO, ID_CLIENTE, ID_GARANTE, ID_REFERENCIA1, ID_REFERENCIA2, " +
                " NOM_REFERENCIA1, TEL_REFERENCIA1, TRAB_REFERENCIA1, NOM_REFERENCIA2, TEL_REFERENCIA2, TRAB_REFERENCIA2, " +
                " NOM_REFERENCIACOMERCIAL1, TEL_REFERENCIACOMERCIAL1, TRAB_REFERENCIACOMERCIAL1, " +
                " NOM_REFERENCIACOMERCIAL2, TEL_REFERENCIACOMERCIAL2, TRAB_REFERENCIACOMERCIAL2, " +
                " I_SALARIO, I_HONORARIO, I_CONYUGE, I_OTROS, I_TOTAL, " +
                " E_GASTO, E_CUOTA, E_ALQUILER, E_OTROS, E_TOTAL, " +
                " TOTALSALE, TOTALENTREGA, FECHA_SOLICITUD, FECHA_APROB, FECHA_COBRO, " +
                " CANTCUOTAS, TOTAL_MONTO_CUOTA, NOTA, ESTADO, ESTADO_INFORCONF) " +
                "VALUES " +
                "(@NuevoId, @NuevoNumero, @IdLocal, @IdUsuario, @IdCliente, @IdGarante, @IdRef1, @IdRef2, " +
                " @NomRef1, @TelRef1, @TrabRef1, @NomRef2, @TelRef2, @TrabRef2, " +
                " @NomRc1, @TelRc1, @TrabRc1, " +
                " @NomRc2, @TelRc2, @TrabRc2, " +
                " @ISalario, @IHonorario, @IConyuge, @IOtros, @ITotal, " +
                " @EGasto, @ECuota, @EAlquiler, @EOtros, @ETotal, " +
                " @TotalSale, @TotalEntrega, GETDATE(), GETDATE(), @FechaCobro, " +
                " @CantCuotas, @TotalMontoCuota, @Nota, @Estado, @EstadoInforconf); " +
                "UPDATE CONTADORES SET SOL = @NuevoId WHERE ID = 1; " +
                "SELECT @NuevoId AS NuevoId, @NuevoNumero AS NuevoNumero;",
                new
                {
                    s.IdLocal, s.IdUsuario, s.IdCliente, s.IdGarante, s.IdRef1, s.IdRef2,
                    s.NomRef1, s.TelRef1, s.TrabRef1, s.NomRef2, s.TelRef2, s.TrabRef2,
                    s.NomRc1, s.TelRc1, s.TrabRc1, s.NomRc2, s.TelRc2, s.TrabRc2,
                    s.ISalario, s.IHonorario, s.IConyuge, s.IOtros, s.ITotal,
                    s.EGasto, s.ECuota, s.EAlquiler, s.EOtros, s.ETotal,
                    s.TotalSale, s.TotalEntrega, s.FechaCobro,
                    s.CantCuotas, s.TotalMontoCuota, s.Nota, s.Estado, s.EstadoInforconf
                });

            await InsertarDetalleSolicitudAsync(conn, fila.NuevoId, s);
            return (fila.NuevoId, fila.NuevoNumero);
        }

        // Artículos siguientes del carrito: reutiliza el IDCABSOL ya generado por AGENTE=1.
        if (s.IdCabSol <= 0)
            throw new Exception("IDCABSOL no válido para agregar detalle de solicitud.");

        await InsertarDetalleSolicitudAsync(conn, s.IdCabSol, s);
        return (s.IdCabSol, "");
    }

    private static async Task InsertarDetalleSolicitudAsync(IDbConnection conn, long idCabSol, SolicitudParams s)
    {
        await conn.ExecuteAsync(
            "DECLARE @NuevoIdDet INT; " +
            "SELECT @NuevoIdDet = ISNULL(MAX(ID_DET_SOL), 0) + 1 FROM DET_SOL_SALES WITH (TABLOCKX); " +
            "INSERT INTO DET_SOL_SALES " +
            "(ID_DET_SOL, IDSOLICITUD, IDART, CA, D, PRECIO, ENTREGA, CANTCUOTAS, COSTOMENSUAL, VALORFINAL, CANTIDAD, SUBTOTAL) " +
            "VALUES " +
            "(@NuevoIdDet, @IdCabSol, @IdArt, @Ca, @D, @Precio, @Entrega, @CantCuotasDet, @CostoMensual, @ValorFinal, @Cant, @Subtotal);",
            new
            {
                IdCabSol = idCabSol,
                s.IdArt, s.Ca, s.D, s.Precio, s.Entrega, s.CantCuotasDet,
                s.CostoMensual, s.ValorFinal, s.Cant, s.Subtotal
            });
    }

    public async Task<(int IdCab, int NVenta)> GuardarVentaContadoAsync(VentaContadoParams v)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@AGENTE", v.Agente); p.Add("@ULTIMO", v.Ultimo);
        p.Add("@IDCAB", v.IdCab, dbType: DbType.Int32, direction: ParameterDirection.InputOutput);
        p.Add("@COMPROBANTE", dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 50, value: "");
        p.Add("@NSOL", v.NSol);
        p.Add("@ID_LOCAL", v.IdLocal); p.Add("@ID_USUARIO", v.IdUsuario);
        p.Add("@ID_CLIENTE", v.IdCliente); p.Add("@ID_GARANTE", v.IdGarante);
        p.Add("@ID_REFERENCIA1", v.IdRef1); p.Add("@ID_REFERENCIA2", v.IdRef2);
        p.Add("@NOM_REFERENCIA1", ""); p.Add("@TEL_REFERENCIA1", ""); p.Add("@TRAB_REFERENCIA1", "");
        p.Add("@NOM_REFERENCIA2", ""); p.Add("@TEL_REFERENCIA2", ""); p.Add("@TRAB_REFERENCIA2", "");
        p.Add("@NOM_REFERENCIACOMERCIAL1", v.NomRefCom1); p.Add("@TEL_REFERENCIACOMERCIAL1", v.TelRefCom1); p.Add("@TRAB_REFERENCIACOMERCIAL1", v.TrabRefCom1);
        p.Add("@NOM_REFERENCIACOMERCIAL2", v.NomRefCom2); p.Add("@TEL_REFERENCIACOMERCIAL2", v.TelRefCom2); p.Add("@TRAB_REFERENCIACOMERCIAL2", v.TrabRefCom2);
        p.Add("@FORMA_DE_VENTA", v.FormaDeVenta); p.Add("@METODO_DE_VENTA", v.MetodoDeVenta); p.Add("@NTARJETA", v.NTarjeta);
        p.Add("@PARCIAL", v.Parcial); p.Add("@DESCUENTO", v.Descuento); p.Add("@TOTAL", v.Total);
        p.Add("@ENTREGANORMAL", v.EntregaNormal); p.Add("@ENTREGALOGISTICA", v.EntregaLogistica);
        p.Add("@CUOTAS", v.Cuotas); p.Add("@MONTO_CUOTA", v.MontoCuota);
        p.Add("@DEBE", v.Debe); p.Add("@HABER", v.Haber); p.Add("@CPHA", v.Cpha);
        p.Add("@ESTADO", v.Estado); p.Add("@TIVA", v.Tiva);
        p.Add("@NVenta", v.NVenta);
        p.Add("@idart", v.IdArt); p.Add("@cantidad", v.Cantidad); p.Add("@pc", v.Pc); p.Add("@pv", v.Pv);
        p.Add("@iva", v.IvaArt); p.Add("@es", v.EsArt);
        p.Add("@MOV", v.Mov); p.Add("@MOD", v.Mod); p.Add("@PCACT", v.PcAct);
        p.Add("@FORMA_PAGO", v.FormaPago); p.Add("@MONTO_CAJA", v.MontoCaja);
        p.Add("@ID_CAJERO", v.IdCajero); p.Add("@ID_CAJA_FISICA", v.IdCajaFisica);
        p.Add("@REFERENCIA", v.Referencia);
        p.Add("@NOM_MAQUINA", Environment.MachineName);
        p.Add("@IP_MAQUINA", "");
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
        p.Add("@msgcomprobante", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);

        await conn.ExecuteAsync("sp_Guardar_Venta_Contado_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@msg");
        if (string.IsNullOrWhiteSpace(msg) || msg.Trim() != "GUARDADO")
            throw new Exception(string.IsNullOrWhiteSpace(msg)
                ? "El SP no procesó la venta (AGENTE inválido o parámetros incorrectos)."
                : $"El SP reportó un error: {msg}");
        // @NVenta no viene marcado OUTPUT en el SP (limitación existente, no tocada acá) —
        // el comprobante real sale por @msgcomprobante, que sí es OUTPUT.
        var comprobante = p.Get<string>("@msgcomprobante");
        _ = int.TryParse(comprobante, out var nVenta);
        return (p.Get<int>("@IDCAB"), nVenta);
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
