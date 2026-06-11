using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public interface ICajaRepository
{
    Task<CajaMaster?> ObtenerCajaAbiertaAsync(int idLocal, int idCajaFisica = 1);
    Task<int> AbrirCajaAsync(int idLocal, int idUsuario, decimal montoBase, int idCajaFisica = 1);
    Task<bool> CerrarCajaAsync(int idMaster, int idUsuario, decimal montoCierreReal, string observaciones);
    Task<IEnumerable<CajaDetalle>> ObtenerMovimientosAsync(int idMaster);
    Task<IEnumerable<CajaMaster>> ObtenerHistorialAsync(int idLocal, DateTime desde, DateTime hasta);
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
        return new CajaMaster
        {
            IdMaster = (int)row.ID_MASTER,
            IdLocal = (byte)idLocal,
            IdCajaFisica = idCajaFisica,
            MontoBase = (decimal)row.MONTO_BASE,
            IdUsuarioApe = (int)row.ID_USUARIO_APE,
            Estado = 'A',
            FechaApertura = DateTime.Now
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

    public async Task<bool> CerrarCajaAsync(int idMaster, int idUsuario, decimal montoCierreReal, string observaciones)
    {
        using var conn = _factory.Create();
        // sp_CerrarCaja_CS requiere totales desagregados — pasamos todo en efectivo
        var p = new DynamicParameters();
        p.Add("@ID_MASTER", idMaster);
        p.Add("@ID_USUARIO_CIE", idUsuario);
        p.Add("@TOT_EFECTIVO", montoCierreReal);
        p.Add("@TOT_TARJETA", 0m);
        p.Add("@TOT_TRANSF", 0m);
        p.Add("@TOT_CHEQUE", 0m);
        p.Add("@TOT_OTRO", 0m);
        p.Add("@TOT_INGRESOS", montoCierreReal);
        p.Add("@EG_EFECTIVO", 0m);
        p.Add("@EG_TARJETA", 0m);
        p.Add("@EG_TRANSF", 0m);
        p.Add("@EG_CHEQUE", 0m);
        p.Add("@EG_OTRO", 0m);
        p.Add("@EG_ANTICIPOS", 0m);
        p.Add("@TOT_EGRESOS", 0m);
        p.Add("@MONTO_CIERRE_REAL", montoCierreReal);
        p.Add("@OBSERVACIONES", observaciones ?? "");
        p.Add("@Msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("sp_CerrarCaja_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@Msg") ?? "";
        return !msg.Contains("Error", StringComparison.OrdinalIgnoreCase);
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
}
