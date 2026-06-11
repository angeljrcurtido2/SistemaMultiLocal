using CrediSoft.Core.Models;
using Dapper;
using System.Data;
using System.Net;

namespace CrediSoft.Data.Repositories;

public interface ICuotaRepository
{
    Task<IEnumerable<Cuota>> BuscarPendientesPorClienteAsync(int idCliente);
    Task<IEnumerable<Cuota>> BuscarAtrasosAsync(int? local = null, int? diasMinimos = null);
    Task<bool> CobrarCuotaAsync(CobrarCuotaParams p);
    Task<decimal> CalcularPunitoriocAsync(int idGeneradas);
}

public record CobrarCuotaParams(
    int IdCab, int IdGeneradas, byte NCuota, string Comprobante,
    decimal MontoCuota, int Mora, decimal Punitorio, decimal Total,
    int IdUsuario, byte IdLocal, int IdCajaFisica,
    decimal EntregaAnterior, byte Inforcom, byte ElEstadoCab,
    string FormaPago = "EFECTIVO", string Referencia = "",
    string Obs = "");

public class CuotaRepository : ICuotaRepository
{
    private readonly IDbConnectionFactory _factory;
    public CuotaRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Cuota>> BuscarPendientesPorClienteAsync(int idCliente)
    {
        // GENERADAS no tiene ID_CLIENTE — se une por CABECERA_SALES
        using var conn = _factory.Create();
        return await conn.QueryAsync<Cuota>(
            "SELECT G.IDGENERADAS, G.IDCAB, G.COMPROBANTE, G.NCUOTA, G.MONTO, " +
            "G.VTO, G.FECHACOBRADO, G.MORA, G.PUNITORIO, G.REAJUSTE, G.TOTAL, " +
            "G.IDLOCAL, G.IDU, G.OBS, G.ESTADO, G.ENTREGA, " +
            "C.NOMBRE_CLIENTE as ClienteNombre, C.CI_CLIENTE as ClienteCi, " +
            "C.TELEFONO_CLIENTE as ClienteTelefono, CB.NSOLICITUD " +
            "FROM GENERADAS G " +
            "INNER JOIN CABECERA_SALES CB ON G.IDCAB = CB.IDCAB " +
            "INNER JOIN CLIENTES C ON CB.ID_CLIENTE = C.ID_CLIENTE " +
            "WHERE CB.ID_CLIENTE = @IdCliente AND G.ESTADO = 0 " +
            "ORDER BY G.VTO",
            new { IdCliente = idCliente });
    }

    public async Task<IEnumerable<Cuota>> BuscarAtrasosAsync(int? local = null, int? diasMinimos = null)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 12);
        var rows = (await conn.QueryAsync<dynamic>(
            "LISTA_ATRASOS_CS_2026", p, commandType: CommandType.StoredProcedure)).ToList();

        return rows.Select(r =>
        {
            var dict = (IDictionary<string, object>)r;
            return new Cuota
            {
                IdCab       = dict.TryGetValue("IDCAB", out var cab) ? Convert.ToInt32(cab) : 0,
                NSolicitud  = dict.TryGetValue("SOLICITUD", out var sol) ? sol?.ToString() ?? "" : "",
                ClienteNombre = dict.TryGetValue("CLIENTE", out var cli) ? cli?.ToString() ?? "" : "",
                ClienteTelefono = dict.TryGetValue("TELEFONO", out var tel) ? tel?.ToString() ?? "" : "",
                NCuota      = dict.TryGetValue("Nº CUOTA", out var nc) ? Convert.ToByte(nc ?? 0) : (byte)0,
                Monto       = dict.TryGetValue("MONTO", out var mon) ? Convert.ToDecimal(mon ?? 0) : 0,
                Mora        = dict.TryGetValue("MORA", out var mor) ? Convert.ToInt32(mor ?? 0) : 0,
                VendedorNombre = dict.TryGetValue("VENDEDOR", out var vend) ? vend?.ToString() ?? "" : "",
                IdLocal     = dict.TryGetValue("LOCAL", out var loc) ? Convert.ToByte(loc ?? 0) : (byte)0,
            };
        })
        .Where(c => (!local.HasValue || c.IdLocal == local.Value) &&
                    (!diasMinimos.HasValue || c.Mora >= diasMinimos.Value))
        .ToList();
    }

    public async Task<bool> CobrarCuotaAsync(CobrarCuotaParams prm)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@IdCab",           prm.IdCab);
        p.Add("@ElEstadoCab",     prm.ElEstadoCab);
        p.Add("@Monto_Cuota",     prm.MontoCuota);
        p.Add("@IdGeneradas",     prm.IdGeneradas);
        p.Add("@Mora",            prm.Mora);
        p.Add("@Punitorio",       prm.Punitorio);
        p.Add("@Reajuste",        0m);
        p.Add("@Total",           prm.Total);
        p.Add("@Idu",             prm.IdUsuario);
        p.Add("@Obs",             prm.Obs);
        p.Add("@Estado",          (byte)1);
        p.Add("@NCuota",          prm.NCuota);
        p.Add("@Comprobante",     prm.Comprobante);
        p.Add("@EntregaAnterior", prm.EntregaAnterior);
        p.Add("@Inforcom",        prm.Inforcom);
        p.Add("@Id_Caja_Fisica",  prm.IdCajaFisica);
        p.Add("@Forma_Pago",      prm.FormaPago);
        p.Add("@Id_Local",        prm.IdLocal);
        p.Add("@Referencia",      prm.Referencia);
        p.Add("@NOM_MAQUINA",     Dns.GetHostName());
        p.Add("@IP_MAQUINA",      "127.0.0.1");
        p.Add("@Msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

        await conn.ExecuteAsync("sp_Guardar_Cobranza_Cs_2026", p,
            commandType: CommandType.StoredProcedure);

        var msg = p.Get<string>("@Msg") ?? "";
        return !msg.Contains("Error", StringComparison.OrdinalIgnoreCase) &&
               !msg.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<decimal> CalcularPunitoriocAsync(int idGeneradas)
    {
        using var conn = _factory.Create();
        var cuota = await conn.QueryFirstOrDefaultAsync<(decimal Monto, DateTime Vto)>(
            "SELECT MONTO, VTO FROM GENERADAS WHERE IDGENERADAS = @Id",
            new { Id = idGeneradas });
        if (cuota.Vto >= DateTime.Today) return 0;

        var tasa = await conn.ExecuteScalarAsync<decimal?>(
            "SELECT TOP 1 VALOR_PUNITORIO FROM CONFIGURACION") ?? 0;
        var dias = (DateTime.Today - cuota.Vto).Days;
        return Math.Round(cuota.Monto * (tasa / 100m) * dias, 0);
    }
}
