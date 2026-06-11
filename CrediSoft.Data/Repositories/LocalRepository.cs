using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public class LocalRepository : ILocalRepository
{
    private readonly IDbConnectionFactory _factory;
    public LocalRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SelectCols =
        "ID_LOCAL as IdLocal, CODIGO as Codigo, NOMBRE as NombreLocal, " +
        "DIRECCION as DireccionLocal, CIUDAD as CiudadLocal, TELEFONO as TelefonoLocal";

    public async Task<Local?> ObtenerPorIdAsync(int id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Local>(
            $"SELECT {SelectCols} FROM LOCALES WHERE ID_LOCAL = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Local>> ListarTodosAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Local>(
            $"SELECT {SelectCols} FROM LOCALES ORDER BY NOMBRE");
    }

    // AGREGAR_LOCAL_CS(@Id,@Codigo,@Nombre,@Direccion,@Ciudad,@Telefono,@msg)
    public async Task<bool> GuardarAsync(Local l)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", 0);
        p.Add("@Codigo", (byte)l.IdLocal);
        p.Add("@Nombre", l.NombreLocal);
        p.Add("@Direccion", l.DireccionLocal);
        p.Add("@Ciudad", l.CiudadLocal);
        p.Add("@Telefono", l.TelefonoLocal);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("AGREGAR_LOCAL_CS", p, commandType: CommandType.StoredProcedure);
        return true;
    }

    // ACTUALIZAR_LOCAL_CS(@Codigo,@Nombre,@Direccion,@Ciudad,@Telefono,@ID,@msg)
    public async Task<bool> ActualizarAsync(Local l)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", l.IdLocal);
        p.Add("@Codigo", (byte)l.IdLocal);
        p.Add("@Nombre", l.NombreLocal);
        p.Add("@Direccion", l.DireccionLocal);
        p.Add("@Ciudad", l.CiudadLocal);
        p.Add("@Telefono", l.TelefonoLocal);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ACTUALIZAR_LOCAL_CS", p, commandType: CommandType.StoredProcedure);
        return true;
    }

    // ELIMINAR_LOCAL_CS(@Id,@msg)
    public async Task<bool> EliminarAsync(int id)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ELIMINAR_LOCAL_CS", p, commandType: CommandType.StoredProcedure);
        return true;
    }
}
