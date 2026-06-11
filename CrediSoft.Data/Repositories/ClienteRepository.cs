using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public interface IClienteRepository
{
    Task<Cliente?> BuscarPorCiAsync(string ci);
    Task<Cliente?> BuscarPorIdAsync(int id);
    Task<IEnumerable<Cliente>> BuscarAsync(string termino, int? local = null);
    Task<int> GuardarAsync(Cliente cliente);
    Task<bool> ActualizarAsync(Cliente cliente);
    Task<bool> EliminarAsync(int idCliente);
    Task<IEnumerable<(int Id, string Nombre)>> ObtenerLocalesAsync();
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

    public async Task<bool> EliminarAsync(int idCliente)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM CLIENTES WHERE ID_CLIENTE = @Id",
            new { Id = idCliente });
        return rows > 0;
    }
}
