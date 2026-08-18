using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public interface IClienteRepository
{
    Task<Cliente?> BuscarPorCiAsync(string ci);
    Task<Cliente?> BuscarPorIdAsync(int id);
    Task<IEnumerable<Cliente>> BuscarAsync(string termino, int? local = null);
    Task<IEnumerable<ClienteCreditoResumen>> BuscarClientesConCreditosAsync(
        string termino = "", bool soloConAtrasos = false, int? local = null, int? idCliente = null);
    Task<int> GuardarAsync(Cliente cliente);
    Task<bool> ActualizarAsync(Cliente cliente);
    Task<bool> EliminarAsync(int idCliente);
    Task<IEnumerable<(int Id, string Nombre)>> ObtenerLocalesAsync();
    Task<(IEnumerable<Cliente> filas, int total)> BuscarPaginadoAsync(
        string termino, int pagina, int porPagina,
        int? estado, int? inforcom, int? tipo, int? local,
        decimal? credDesde, decimal? credHasta, string ciudad,
        bool soloConCuotas = false, bool soloConCreditos = false);
    // Cantidad de ventas a crédito previas del cliente (FORMA_DE_VENTA=2), sin importar su
    // estado actual (activo, cancelado, en mora) — solo cuenta si YA TUVO un crédito antes,
    // no si lo pagó bien. Usado para decidir si el cliente sigue siendo "nuevo" a efectos de
    // exigir garante.
    Task<int> ContarCreditosPreviosAsync(int idCliente);
    // Igual que ContarCreditosPreviosAsync, pero desglosado: Total = todos los créditos
    // previos, Cancelados = cuántos de esos ya tienen CABECERA_SALES.ESTADO=1 (pagados
    // completos). Usado para el texto del card de garante — el cliente puede "ya tener un
    // crédito" sin que esté cancelado todavía, y eso debe quedar explícito, no implícito.
    Task<(int Total, int Cancelados)> ContarCreditosPreviosDetalladoAsync(int idCliente);
}

public class ClienteRepository : IClienteRepository
{
    private readonly IDbConnectionFactory _factory;

    public ClienteRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Cliente?> BuscarPorCiAsync(string ci)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Cliente>(
            "SELECT ID_CLIENTE as IdCliente, RUC_CLIENTE as RucCliente, " +
            "CI_CLIENTE as CiCliente, NOMBRE_CLIENTE as NombreCliente, " +
            "DIRECCION_CLIENTE as DireccionCliente, TELEFONO_CLIENTE as TelefonoCliente, " +
            "CIUDAD_CLIENTE as CiudadCliente, ZONA_CLIENTE as ZonaCliente, " +
            "REFERENCIA_GEO as ReferenciaGeo, E_MAIL_CLIENTE as EmailCliente, " +
            "CRED_MAX as CredMax, ESTADO as Estado, INFORCOM as Inforcom, " +
            "SEXO as Sexo, CONDICION as Condicion, ECV as Ecv, " +
            "CONYUGE as Conyuge, CI_CONYUGE as CiConyuge, " +
            "EMPRESA_LABORAL as EmpresaLaboral, TELEFONO_LABORAL as TelefonoLaboral, " +
            "DIRECCION_LABORAL as DireccionLaboral, ANTIGUEDAD as Antiguedad, " +
            "TIPO as Tipo, LOCAL as Local, FOTO_CLIENTE as FotoCliente, " +
            "VENC_CEDULA as VencCedula, FECHA_REGISTRO as FechaRegistro " +
            "FROM CLIENTES WHERE CI_CLIENTE = @CI",
            new { CI = ci });
    }

    public async Task<Cliente?> BuscarPorIdAsync(int id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Cliente>(
            "SELECT ID_CLIENTE as IdCliente, RUC_CLIENTE as RucCliente, " +
            "CI_CLIENTE as CiCliente, NOMBRE_CLIENTE as NombreCliente, " +
            "DIRECCION_CLIENTE as DireccionCliente, TELEFONO_CLIENTE as TelefonoCliente, " +
            "CIUDAD_CLIENTE as CiudadCliente, ZONA_CLIENTE as ZonaCliente, " +
            "REFERENCIA_GEO as ReferenciaGeo, E_MAIL_CLIENTE as EmailCliente, " +
            "CRED_MAX as CredMax, ESTADO as Estado, INFORCOM as Inforcom, " +
            "SEXO as Sexo, CONDICION as Condicion, ECV as Ecv, " +
            "CONYUGE as Conyuge, CI_CONYUGE as CiConyuge, " +
            "EMPRESA_LABORAL as EmpresaLaboral, TELEFONO_LABORAL as TelefonoLaboral, " +
            "DIRECCION_LABORAL as DireccionLaboral, ANTIGUEDAD as Antiguedad, " +
            "TIPO as Tipo, LOCAL as Local, FOTO_CLIENTE as FotoCliente, " +
            "VENC_CEDULA as VencCedula, FECHA_REGISTRO as FechaRegistro " +
            "FROM CLIENTES WHERE ID_CLIENTE = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Cliente>> BuscarAsync(string termino, int? local = null)
    {
        using var conn = _factory.Create();
        var sql = "SELECT TOP 200 ID_CLIENTE as IdCliente, CI_CLIENTE as CiCliente, " +
                  "NOMBRE_CLIENTE as NombreCliente, TELEFONO_CLIENTE as TelefonoCliente, " +
                  "CIUDAD_CLIENTE as CiudadCliente, ESTADO as Estado, CRED_MAX as CredMax " +
                  "FROM CLIENTES WHERE " +
                  "(CI_CLIENTE LIKE @T OR NOMBRE_CLIENTE LIKE @T) ";
        if (local.HasValue)
            sql += "AND LOCAL = @Local ";
        sql += "ORDER BY NOMBRE_CLIENTE";

        return await conn.QueryAsync<Cliente>(sql,
            new { T = $"%{termino}%", Local = local });
    }

    public async Task<IEnumerable<ClienteCreditoResumen>> BuscarClientesConCreditosAsync(
        string termino = "", bool soloConAtrasos = false, int? local = null, int? idCliente = null)
    {
        using var conn = _factory.Create();
        var tasaPunitorio = await conn.ExecuteScalarAsync<decimal?>(
            "SELECT TOP 1 ISNULL(VALOR_PUNITORIO, 0) FROM CONFIGURACION") ?? 0m;

        var where = new System.Text.StringBuilder();
        where.Append("WHERE 1=1 ");

        if (!string.IsNullOrWhiteSpace(termino))
            where.Append("AND (C.CI_CLIENTE LIKE @Termino OR C.NOMBRE_CLIENTE LIKE @Termino OR C.TELEFONO_CLIENTE LIKE @Termino) ");
        if (local.HasValue)
            where.Append("AND CS.ID_LOCAL = @Local ");
        if (idCliente.HasValue)
            where.Append("AND C.ID_CLIENTE = @IdCliente ");

        var sql = $@"
            ;WITH Creditos AS (
                SELECT
                    CS.IDCAB AS IdCab,
                    C.ID_CLIENTE AS IdCliente,
                    C.CI_CLIENTE AS CiCliente,
                    C.NOMBRE_CLIENTE AS NombreCliente,
                    ISNULL(C.TELEFONO_CLIENTE, '') AS TelefonoCliente,
                    ISNULL(MAX(L.NOMBRE), '') AS LocalNombre,
                    COUNT(*) AS CuotasTotalCredito,
                    SUM(CASE WHEN G.ESTADO = 0 THEN 1 ELSE 0 END) AS CuotasPendientes,
                    SUM(CASE WHEN G.ESTADO = 0 AND G.VTO < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS CuotasAtrasadas,
                    ISNULL(MAX(CASE WHEN G.ESTADO = 0 AND G.VTO < CAST(GETDATE() AS DATE)
                        THEN DATEDIFF(day, G.VTO, CAST(GETDATE() AS DATE)) - 5
                    END), 0) AS DiasAtrasoMax,
                    ISNULL(SUM(CASE
                        WHEN G.ESTADO = 0 AND G.VTO < CAST(GETDATE() AS DATE)
                            THEN G.MONTO + CASE
                                WHEN G.VTO >= CAST(GETDATE() AS DATE) THEN 0
                                ELSE ROUND(G.MONTO * (@TasaPunitorio / 100.0) / 25.0
                                    * CASE WHEN (DATEDIFF(day, G.VTO, GETDATE()) - 5) < 0
                                        THEN 0
                                        ELSE (DATEDIFF(day, G.VTO, GETDATE()) - 5)
                                    END, 0)
                            END
                        ELSE 0
                    END), 0) AS MontoAtraso,
                    MIN(CASE WHEN G.ESTADO = 0 THEN G.VTO END) AS ProximoVencimiento
                FROM CLIENTES C
                INNER JOIN CABECERA_SALES CS ON CS.ID_CLIENTE = C.ID_CLIENTE
                INNER JOIN GENERADAS G ON G.IDCAB = CS.IDCAB
                LEFT JOIN LOCALES L ON L.ID_LOCAL = CS.ID_LOCAL
                {where}
                GROUP BY CS.IDCAB, C.ID_CLIENTE, C.CI_CLIENTE, C.NOMBRE_CLIENTE, C.TELEFONO_CLIENTE
            )
            SELECT *
            FROM (
                SELECT
                    IdCliente,
                    CiCliente,
                    NombreCliente,
                    TelefonoCliente,
                    MAX(LocalNombre) AS LocalNombre,
                    COUNT(*) AS CreditosTotales,
                    SUM(CASE WHEN CuotasPendientes > 0 THEN 1 ELSE 0 END) AS CreditosActivos,
                    SUM(CuotasPendientes) AS CuotasPendientes,
                    SUM(CuotasAtrasadas) AS CuotasAtrasadas,
                    MAX(DiasAtrasoMax) AS DiasAtrasoMax,
                    SUM(MontoAtraso) AS MontoAtraso,
                    MIN(CASE WHEN CuotasPendientes > 0 THEN ProximoVencimiento END) AS ProximoVencimiento
                FROM Creditos
                GROUP BY IdCliente, CiCliente, NombreCliente, TelefonoCliente
            ) Resumen
            WHERE CreditosTotales > 0
              AND (@SoloConAtrasos = 0 OR CuotasAtrasadas > 0)
            ORDER BY CASE WHEN CuotasAtrasadas > 0 THEN 0 ELSE 1 END, CuotasAtrasadas DESC, NombreCliente";

        return await conn.QueryAsync<ClienteCreditoResumen>(sql, new
        {
            Termino = $"%{termino}%",
            SoloConAtrasos = soloConAtrasos ? 1 : 0,
            Local = local,
            IdCliente = idCliente,
            TasaPunitorio = tasaPunitorio
        });
    }

    public async Task<int> GuardarAsync(Cliente cliente)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", 0);
        p.Add("@RUC_CLIENTE", string.IsNullOrWhiteSpace(cliente.RucCliente) ? null : cliente.RucCliente.Trim());
        p.Add("@CI_CLIENTE", cliente.CiCliente);
        p.Add("@NOMBRE_CLIENTE", cliente.NombreCliente);
        p.Add("@DIRECCION_CLIENTE", cliente.DireccionCliente);
        p.Add("@TELEFONO_CLIENTE", cliente.TelefonoCliente);
        p.Add("@CIUDAD_CLIENTE", cliente.CiudadCliente);
        p.Add("@ZONA_CLIENTE", cliente.ZonaCliente);
        p.Add("@REFERENCIA_GEO", cliente.ReferenciaGeo);
        p.Add("@E_MAIL_CLIENTE", cliente.EmailCliente);
        p.Add("@CRED_MAX", cliente.CredMax);
        p.Add("@ESTADO", cliente.Estado);
        p.Add("@INFORCOM", cliente.Inforcom);
        p.Add("@Civil", cliente.Ecv);
        p.Add("@SEXO", cliente.Sexo);
        p.Add("@COND", cliente.Condicion);
        p.Add("@CONYUGE", cliente.Conyuge);
        p.Add("@CI_CONYUGE", cliente.CiConyuge);
        p.Add("@EMPRESA_LABORAL", cliente.EmpresaLaboral);
        p.Add("@TELEFONO_LABORAL", cliente.TelefonoLaboral);
        p.Add("@DIRECCION_LABORAL", cliente.DireccionLaboral);
        p.Add("@ANTIGUEDAD", cliente.Antiguedad);
        p.Add("@TIPO", cliente.Tipo);
        p.Add("@LOCAL", cliente.Local);
        p.Add("@FOTO_CLIENTE", cliente.FotoCliente ?? string.Empty);
        p.Add("@VENC_CEDULA", cliente.VencCedula);
        p.Add("@FECHA_REGISTRO", cliente.FechaRegistro);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);

        await conn.ExecuteAsync("AGREGAR_CLIENTE_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@msg");
        if (!string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? "Error al registrar cliente." : msg);

        var registrado = await BuscarPorCiAsync(cliente.CiCliente);
        return registrado?.IdCliente ?? 0;
    }

    public async Task<bool> ActualizarAsync(Cliente cliente)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@RUC_CLIENTE", string.IsNullOrWhiteSpace(cliente.RucCliente) ? null : cliente.RucCliente.Trim());
        p.Add("@CI_CLIENTE", cliente.CiCliente);
        p.Add("@NOMBRE_CLIENTE", cliente.NombreCliente);
        p.Add("@DIRECCION_CLIENTE", cliente.DireccionCliente);
        p.Add("@TELEFONO_CLIENTE", cliente.TelefonoCliente);
        p.Add("@CIUDAD_CLIENTE", cliente.CiudadCliente);
        p.Add("@ZONA_CLIENTE", cliente.ZonaCliente);
        p.Add("@REFERENCIA_GEO", cliente.ReferenciaGeo);
        p.Add("@E_MAIL_CLIENTE", cliente.EmailCliente);
        p.Add("@CRED_MAX", cliente.CredMax);
        p.Add("@ESTADO", cliente.Estado);
        p.Add("@INFORCOM", cliente.Inforcom);
        p.Add("@SEXO", cliente.Sexo);
        p.Add("@ECivil", cliente.Ecv);
        p.Add("@COND", cliente.Condicion);
        p.Add("@CONYUGE", cliente.Conyuge);
        p.Add("@CI_CONYUGE", cliente.CiConyuge);
        p.Add("@EMPRESA_LABORAL", cliente.EmpresaLaboral);
        p.Add("@TELEFONO_LABORAL", cliente.TelefonoLaboral);
        p.Add("@DIRECCION_LABORAL", cliente.DireccionLaboral);
        p.Add("@ANTIGUEDAD", cliente.Antiguedad);
        p.Add("@TIPO", cliente.Tipo);
        p.Add("@LOCAL", cliente.Local);
        p.Add("@FOTO_CLIENTE", cliente.FotoCliente ?? string.Empty);
        p.Add("@VENC_CEDULA", cliente.VencCedula);
        p.Add("@FECHA_REGISTRO", cliente.FechaRegistro);
        p.Add("@ID", cliente.IdCliente);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);

        await conn.ExecuteAsync("ACTUALIZAR_CLIENTE_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string>("@msg");
        if (!string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? "Error al actualizar cliente." : msg);

        return true;
    }

    public async Task<IEnumerable<(int Id, string Nombre)>> ObtenerLocalesAsync()
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<(int Id, string Nombre)>(
            "SELECT ID_LOCAL as Id, NOMBRE as Nombre FROM LOCALES ORDER BY NOMBRE");
        return rows;
    }

    public async Task<int> ContarCreditosPreviosAsync(int idCliente)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM CABECERA_SALES WHERE ID_CLIENTE = @IdCliente AND FORMA_DE_VENTA = 2",
            new { IdCliente = idCliente });
    }

    public async Task<(int Total, int Cancelados)> ContarCreditosPreviosDetalladoAsync(int idCliente)
    {
        using var conn = _factory.Create();
        var fila = await conn.QueryFirstOrDefaultAsync<(int Total, int Cancelados)>(
            "SELECT COUNT(*) AS Total, ISNULL(SUM(CASE WHEN ESTADO = 1 THEN 1 ELSE 0 END), 0) AS Cancelados " +
            "FROM CABECERA_SALES WHERE ID_CLIENTE = @IdCliente AND FORMA_DE_VENTA = 2",
            new { IdCliente = idCliente });
        return fila;
    }

    public async Task<(IEnumerable<Cliente> filas, int total)> BuscarPaginadoAsync(
        string termino, int pagina, int porPagina,
        int? estado, int? inforcom, int? tipo, int? local,
        decimal? credDesde, decimal? credHasta, string ciudad,
        bool soloConCuotas = false, bool soloConCreditos = false)
    {
        using var conn = _factory.Create();

        var where = new System.Text.StringBuilder("WHERE 1=1 ");
        if (!string.IsNullOrWhiteSpace(termino))
            where.Append("AND (CI_CLIENTE LIKE @T OR NOMBRE_CLIENTE LIKE @T) ");
        if (estado.HasValue)
            where.Append("AND ESTADO = @Estado ");
        if (inforcom.HasValue)
            where.Append("AND INFORCOM = @Inforcom ");
        if (tipo.HasValue)
            where.Append("AND TIPO = @Tipo ");
        if (local.HasValue)
            where.Append("AND LOCAL = @Local ");
        if (credDesde.HasValue)
            where.Append("AND CRED_MAX >= @CredDesde ");
        if (credHasta.HasValue)
            where.Append("AND CRED_MAX <= @CredHasta ");
        if (!string.IsNullOrWhiteSpace(ciudad))
            where.Append("AND CIUDAD_CLIENTE LIKE @Ciudad ");
        if (soloConCreditos)
            where.Append("AND EXISTS (SELECT 1 FROM CABECERA_SALES cs WHERE cs.ID_CLIENTE = CLIENTES.ID_CLIENTE AND cs.ESTADO = 1) ");
        if (soloConCuotas)
            where.Append("AND EXISTS (SELECT 1 FROM GENERADAS cu INNER JOIN CABECERA_SALES cs ON cu.IDCAB = cs.IDCAB WHERE cs.ID_CLIENTE = CLIENTES.ID_CLIENTE AND cu.ESTADO = 0) ");

        // El subquery interno produce columnas con nombre de ALIAS (IdCliente, CiCliente, ...),
        // asi que el SELECT externo (que envuelve el paginado con ROW_NUMBER) debe referenciar
        // esos mismos alias, no las columnas crudas de CLIENTES — que ya no existen en __p.
        var colsCrudas = "ID_CLIENTE as IdCliente, CI_CLIENTE as CiCliente, " +
                          "NOMBRE_CLIENTE as NombreCliente, TELEFONO_CLIENTE as TelefonoCliente, " +
                          "CIUDAD_CLIENTE as CiudadCliente, ESTADO as Estado, CRED_MAX as CredMax, " +
                          "TIPO as Tipo, LOCAL as Local";
        var colsAlias = "IdCliente, CiCliente, NombreCliente, TelefonoCliente, " +
                         "CiudadCliente, Estado, CredMax, Tipo, Local";

        var sqlCount = $"SELECT COUNT(*) FROM CLIENTES {where}";
        var offset   = (pagina - 1) * porPagina;
        var sqlPage  = $"SELECT {colsAlias} FROM (SELECT {colsCrudas}, ROW_NUMBER() OVER (ORDER BY NOMBRE_CLIENTE) AS __rn FROM CLIENTES {where}) __p " +
                       $"WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina}";

        var param = new
        {
            T         = $"%{termino}%",
            Estado    = estado,
            Inforcom  = inforcom,
            Tipo      = tipo,
            Local     = local,
            CredDesde = credDesde,
            CredHasta = credHasta,
            Ciudad    = $"%{ciudad}%"
        };

        var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
        var filas = await conn.QueryAsync<Cliente>(sqlPage, param);
        return (filas, total);
    }

    public async Task<bool> EliminarAsync(int idCliente)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM CLIENTES WHERE ID_CLIENTE = @Id",
            new { Id = idCliente });
        return rows > 0;
    }
}
