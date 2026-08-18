using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public interface ICajaRepository
{
    Task<CajaMaster?> ObtenerCajaAbiertaAsync(int idLocal, int idCajaFisica = 1);
    Task<int> AbrirCajaAsync(int idLocal, int idUsuario, decimal montoBase, int idCajaFisica = 1);
    Task<bool> CerrarCajaAsync(int idMaster, int idUsuario, decimal montoEfectivo, decimal montoTarjeta,
        decimal montoTransf, decimal montoQr, decimal montoCheque, string observaciones);
    Task<IEnumerable<CajaDetalle>> ObtenerMovimientosAsync(int idMaster);
    Task<IEnumerable<CajaMaster>> ObtenerHistorialAsync(int idLocal, DateTime desde, DateTime hasta);
    Task<IEnumerable<CajaMaster>> ListarCajasAbiertasAsync();
    // Comprobante de depósito — ver comentario en CajaMaster.ComprobanteCompleto/
    // DentroDePlazoComprobante. Se puede llamar en el momento del cierre (foto/nro ya
    // disponibles) o después, desde la pantalla de Comprobantes de Depósito Pendientes.
    Task GuardarComprobanteDepositoAsync(int idMaster, string nroComprobante, byte[]? foto);
    Task<IEnumerable<CajaMaster>> ListarCierresRecientesAsync(int? idLocal, int dias = 3);
    Task<byte[]?> ObtenerFotoComprobanteDepositoAsync(int idMaster);
    Task<CajaMaster?> ObtenerCierrePorIdAsync(int idMaster);
}

public class CajaRepository : ICajaRepository
{
    private readonly IDbConnectionFactory _factory;
    public CajaRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<CajaMaster?> ObtenerCajaAbiertaAsync(int idLocal, int idCajaFisica = 1)
    {
        using var conn = _factory.Create();
        // sp_RecuperarCajaActiva_CS devuelve: ID_MASTER, MONTO_BASE, ID_USUARIO_APE
        var p = new DynamicParameters();
        p.Add("@ID_LOCAL", idLocal);
        p.Add("@ID_CAJA_FISICA", idCajaFisica);
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "sp_RecuperarCajaActiva_CS", p, commandType: CommandType.StoredProcedure);
        if (row == null) return null;

        // El SP no trae FECHA_APERTURA real ni el nombre del usuario que abrio (solo el ID).
        // Se completa con una consulta liviana aparte en vez de tocar el SP, que puede estar
        // compartido con el sistema viejo.
        var idMaster     = (int)row.ID_MASTER;
        var idUsuarioApe = (int)row.ID_USUARIO_APE;
        var detalle = await conn.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT M.FECHA_APERTURA, U.NOMBRE_USUARIO
              FROM CAJA_MASTER M
              INNER JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_APE
              WHERE M.ID_MASTER = @idMaster",
            new { idMaster });

        return new CajaMaster
        {
            IdMaster              = idMaster,
            IdLocal               = (byte)idLocal,
            IdCajaFisica          = idCajaFisica,
            MontoBase             = (decimal)row.MONTO_BASE,
            IdUsuarioApe          = idUsuarioApe,
            Estado                = 'A',
            FechaApertura         = detalle?.FECHA_APERTURA ?? DateTime.Now,
            NombreCajero          = detalle?.NOMBRE_USUARIO ?? "",
            UsuarioAperturanombre = detalle?.NOMBRE_USUARIO ?? ""
        };
    }

    public async Task<int> AbrirCajaAsync(int idLocal, int idUsuario, decimal montoBase, int idCajaFisica = 1)
    {
        using var conn = _factory.Create();
        // sp_AbrirCaja_CS: @ID_LOCAL, @ID_CAJA_FISICA, @ID_USUARIO_APE, @MONTO_BASE
        await conn.ExecuteAsync(
            "sp_AbrirCaja_CS",
            new { ID_LOCAL = idLocal, ID_CAJA_FISICA = idCajaFisica, ID_USUARIO_APE = idUsuario, MONTO_BASE = montoBase },
            commandType: CommandType.StoredProcedure);
        // Recuperar el ID_MASTER recién creado
        var idMaster = await conn.ExecuteScalarAsync<int>(
            "SELECT MAX(ID_MASTER) FROM CAJA_MASTER WHERE ID_LOCAL=@L AND ID_CAJA_FISICA=@C AND ESTADO='A'",
            new { L = idLocal, C = idCajaFisica });
        return idMaster;
    }

    // Reemplaza EXEC sp_CerrarCaja_Desglosado_CS por UPDATE directo — el SP nunca llegó a
    // existir en ningún servidor real (ni producción ni local, confirmado), y no es un SP
    // compartido con CrediMar.exe/VB6 (que sigue usando sp_CerrarCaja_CS aparte), así que no
    // hay ningún riesgo de romper el sistema legado migrándolo. Guarda lo que el cajero
    // contó/declaró por CADA medio de pago (MONTO_CIERRE_EFECTIVO/TARJETA/TRANSF/QR/CHEQUE),
    // y MONTO_CIERRE_REAL = la suma de los 5, para que reportes/lecturas existentes de ese
    // campo único no cambien. QR entra dentro de TOT_OTRO/EG_OTRO (no hay columna TOT_QR en
    // CAJA_MASTER) pero sí tiene su propia columna de "contado" nueva, para poder compararlo
    // aparte.
    public async Task<bool> CerrarCajaAsync(int idMaster, int idUsuario, decimal montoEfectivo, decimal montoTarjeta,
        decimal montoTransf, decimal montoQr, decimal montoCheque, string observaciones)
    {
        using var conn = _factory.Create();
        var totIngresos = montoEfectivo + montoTarjeta + montoTransf + montoQr + montoCheque;

        var filas = await conn.ExecuteAsync(
            "UPDATE CAJA_MASTER SET " +
            "  ESTADO = 'C', FECHA_CIERRE = GETDATE(), ID_USUARIO_CIE = @IdUsuario, " +
            "  TOT_EFECTIVO = @MontoEfectivo, TOT_TARJETA = @MontoTarjeta, TOT_TRANSF = @MontoTransf, " +
            "  TOT_CHEQUE = @MontoCheque, TOT_OTRO = @MontoQr, TOT_INGRESOS = @TotIngresos, " +
            "  EG_EFECTIVO = 0, EG_TARJETA = 0, EG_TRANSF = 0, EG_CHEQUE = 0, EG_OTRO = 0, " +
            "  EG_ANTICIPOS = 0, TOT_EGRESOS = 0, " +
            "  MONTO_CIERRE_REAL = @TotIngresos, " +
            "  MONTO_CIERRE_EFECTIVO = @MontoEfectivo, MONTO_CIERRE_TARJETA = @MontoTarjeta, " +
            "  MONTO_CIERRE_TRANSF = @MontoTransf, MONTO_CIERRE_QR = @MontoQr, MONTO_CIERRE_CHEQUE = @MontoCheque, " +
            "  OBSERVACIONES = @Observaciones " +
            "WHERE ID_MASTER = @IdMaster AND ESTADO = 'A'",
            new
            {
                IdUsuario = idUsuario, MontoEfectivo = montoEfectivo, MontoTarjeta = montoTarjeta,
                MontoTransf = montoTransf, MontoCheque = montoCheque, MontoQr = montoQr,
                TotIngresos = totIngresos, Observaciones = observaciones ?? "", IdMaster = idMaster
            });

        return filas > 0;
    }

    public async Task<IEnumerable<CajaDetalle>> ObtenerMovimientosAsync(int idMaster)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CajaDetalle>(
            "SELECT ID_DETALLE as IdDetalle, ID_MASTER as IdMaster, ID_VENTA as IdVenta, " +
            "ID_LOCAL as IdLocal, FECHA_HORA as FechaHora, TIPO as Tipo, SUBTIPO as Subtipo, " +
            "FORMA_PAGO as FormaPago, MONTO as Monto, ID_CAJERO as IdCajero, " +
            "ID_ENTIDAD as IdEntidad, CONCEPTO as Concepto, REFERENCIA as Referencia, " +
            "ESTADO_REG as EstadoReg " +
            "FROM CAJA_DETALLE WHERE ID_MASTER = @IdMaster AND ESTADO_REG = 'V' " +
            "ORDER BY FECHA_HORA",
            new { IdMaster = idMaster });
    }

    public async Task<IEnumerable<CajaMaster>> ListarCajasAbiertasAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CajaMaster>(
            "SELECT M.ID_MASTER as IdMaster, M.ID_LOCAL as IdLocal, M.ID_CAJA_FISICA as IdCajaFisica, " +
            "M.ESTADO as Estado, M.FECHA_APERTURA as FechaApertura, M.MONTO_BASE as MontoBase, " +
            "M.ID_USUARIO_APE as IdUsuarioApe, ISNULL(L.NOMBRE,'') as LocalNombre, " +
            "ISNULL(U.NOMBRE_USUARIO,'') as NombreCajero " +
            "FROM CAJA_MASTER M " +
            "LEFT JOIN LOCALES  L ON L.ID_LOCAL    = M.ID_LOCAL " +
            "LEFT JOIN USUARIOS U ON U.ID_USUARIO  = M.ID_USUARIO_APE " +
            "WHERE M.ESTADO = 'A' ORDER BY M.ID_LOCAL, M.ID_CAJA_FISICA");
    }

    public async Task<IEnumerable<CajaMaster>> ObtenerHistorialAsync(int idLocal, DateTime desde, DateTime hasta)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<CajaMaster>(
            "SELECT ID_MASTER as IdMaster, ID_LOCAL as IdLocal, ESTADO as Estado, " +
            "FECHA_APERTURA as FechaApertura, FECHA_CIERRE as FechaCierre, " +
            "MONTO_BASE as MontoBase, TOT_INGRESOS as TotIngresos, TOT_EGRESOS as TotEgresos " +
            "FROM CAJA_MASTER WHERE ID_LOCAL = @Local " +
            "AND FECHA_APERTURA BETWEEN @Desde AND @Hasta " +
            "ORDER BY FECHA_APERTURA DESC",
            new { Local = idLocal, Desde = desde, Hasta = hasta.AddDays(1) });
    }

    public async Task GuardarComprobanteDepositoAsync(int idMaster, string nroComprobante, byte[]? foto)
    {
        using var conn = _factory.Create();
        // Solo pisa la foto si viene una nueva — permite cargar primero el número y la foto
        // después (o viceversa) sin perder lo ya guardado en una llamada previa.
        if (foto != null)
            await conn.ExecuteAsync(
                "UPDATE CAJA_MASTER SET NRO_COMPROBANTE_DEPOSITO = @Nro, FOTO_COMPROBANTE_DEPOSITO = @Foto " +
                "WHERE ID_MASTER = @IdMaster",
                new { IdMaster = idMaster, Nro = nroComprobante, Foto = foto });
        else
            await conn.ExecuteAsync(
                "UPDATE CAJA_MASTER SET NRO_COMPROBANTE_DEPOSITO = @Nro WHERE ID_MASTER = @IdMaster",
                new { IdMaster = idMaster, Nro = nroComprobante });
    }

    public async Task<byte[]?> ObtenerFotoComprobanteDepositoAsync(int idMaster)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<byte[]?>(
            "SELECT FOTO_COMPROBANTE_DEPOSITO FROM CAJA_MASTER WHERE ID_MASTER = @IdMaster",
            new { IdMaster = idMaster });
    }

    public async Task<CajaMaster?> ObtenerCierrePorIdAsync(int idMaster)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<CajaMaster>(
            "SELECT M.ID_MASTER as IdMaster, M.ID_LOCAL as IdLocal, M.ESTADO as Estado, " +
            "M.FECHA_APERTURA as FechaApertura, M.FECHA_CIERRE as FechaCierre, " +
            "M.MONTO_BASE as MontoBase, M.TOT_INGRESOS as TotIngresos, M.TOT_EGRESOS as TotEgresos, " +
            "M.TOT_EFECTIVO as TotEfectivo, M.TOT_TARJETA as TotTarjeta, M.TOT_TRANSF as TotTransf, " +
            "M.TOT_CHEQUE as TotCheque, M.MONTO_CIERRE_REAL as MontoGierreReal, " +
            "M.MONTO_CIERRE_EFECTIVO as MontoCierreEfectivo, M.MONTO_CIERRE_TARJETA as MontoCierreTarjeta, " +
            "M.MONTO_CIERRE_TRANSF as MontoCierreTransf, M.MONTO_CIERRE_QR as MontoCierreQr, " +
            "M.MONTO_CIERRE_CHEQUE as MontoCierreCheque, " +
            "ISNULL(M.OBSERVACIONES,'') as Observaciones, " +
            "M.NRO_COMPROBANTE_DEPOSITO as NroComprobanteDeposito, " +
            "CASE WHEN M.FOTO_COMPROBANTE_DEPOSITO IS NOT NULL THEN 1 ELSE 0 END as TieneFotoComprobante, " +
            "ISNULL(L.NOMBRE, '') as LocalNombre, ISNULL(U.NOMBRE_USUARIO, '') as NombreCajero, " +
            "CASE WHEN EXISTS ( " +
            "    SELECT 1 FROM CAJA_DETALLE D WHERE D.ID_MASTER = M.ID_MASTER " +
            "    AND D.TIPO = 'E' AND D.SUBTIPO = 'PAGO' AND D.ESTADO_REG = 'V' " +
            ") THEN 1 ELSE 0 END as DesfinanciadaPorPagoSalario " +
            "FROM CAJA_MASTER M " +
            "LEFT JOIN LOCALES L ON L.ID_LOCAL = M.ID_LOCAL " +
            "LEFT JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_CIE " +
            "WHERE M.ID_MASTER = @IdMaster",
            new { IdMaster = idMaster });
    }

    public async Task<IEnumerable<CajaMaster>> ListarCierresRecientesAsync(int? idLocal, int dias = 3)
    {
        using var conn = _factory.Create();
        var desde = DateTime.Now.AddDays(-dias);
        return await conn.QueryAsync<CajaMaster>(
            "SELECT M.ID_MASTER as IdMaster, M.ID_LOCAL as IdLocal, M.ESTADO as Estado, " +
            "M.FECHA_APERTURA as FechaApertura, M.FECHA_CIERRE as FechaCierre, " +
            "M.MONTO_BASE as MontoBase, M.TOT_INGRESOS as TotIngresos, M.TOT_EGRESOS as TotEgresos, " +
            "M.MONTO_CIERRE_REAL as MontoGierreReal, " +
            "M.MONTO_CIERRE_EFECTIVO as MontoCierreEfectivo, M.MONTO_CIERRE_TARJETA as MontoCierreTarjeta, " +
            "M.MONTO_CIERRE_TRANSF as MontoCierreTransf, M.MONTO_CIERRE_QR as MontoCierreQr, " +
            "M.MONTO_CIERRE_CHEQUE as MontoCierreCheque, " +
            "M.NRO_COMPROBANTE_DEPOSITO as NroComprobanteDeposito, " +
            "CASE WHEN M.FOTO_COMPROBANTE_DEPOSITO IS NOT NULL THEN 1 ELSE 0 END as TieneFotoComprobante, " +
            "ISNULL(L.NOMBRE, '') as LocalNombre, ISNULL(U.NOMBRE_USUARIO, '') as NombreCajero, " +
            "CASE WHEN EXISTS ( " +
            "    SELECT 1 FROM CAJA_DETALLE D WHERE D.ID_MASTER = M.ID_MASTER " +
            "    AND D.TIPO = 'E' AND D.SUBTIPO = 'PAGO' AND D.ESTADO_REG = 'V' " +
            ") THEN 1 ELSE 0 END as DesfinanciadaPorPagoSalario " +
            "FROM CAJA_MASTER M " +
            "LEFT JOIN LOCALES L ON L.ID_LOCAL = M.ID_LOCAL " +
            "LEFT JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_CIE " +
            "WHERE M.ESTADO = 'C' AND M.FECHA_CIERRE >= @Desde " +
            "AND (@IdLocal IS NULL OR M.ID_LOCAL = @IdLocal) " +
            "ORDER BY M.FECHA_CIERRE DESC",
            new { IdLocal = idLocal, Desde = desde });
    }
}
