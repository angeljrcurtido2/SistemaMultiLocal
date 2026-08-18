using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnectionFactory _factory;
    public UsuarioRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SelectCols =
        "ID_USUARIO as IdUsuario, CI_USUARIO as CiUsuario, " +
        "CODIGO_USUARIO as CodigoUsuario, CONTRASEÑA_USUARIO as ContrasenaUsuario, " +
        "NOMBRE_USUARIO as NombreUsuario, DIRECCION_USUARIO as DireccionUsuario, " +
        "TELEFONO_USUARIO as TelefonoUsuario, COMISION_USUARIO as ComisionUsuario, " +
        "CARGO_USUARIO as CargoUsuario, SALARIO_USUARIO as SalarioUsuario, " +
        "PERMISO_DESCUENTO as PermisoDescuento, PERMISO_STOCK as PermisoStock, " +
        "PERMISO_PRECIO as PermisoPrecio, PERMISO_ELIMINAR as PermisoEliminar, " +
        "PERMISO_COBRAR_CUOTAS as PermisoCobrarCuotas, PERMISO_COMPRAS as PermisoCompras, " +
        "PERMISO_ELIMINAR_FACTURA as PermisoEliminarFactura, " +
        "LOCAL_USUARIO as LocalUsuario, ZONA_USUARIO as ZonaUsuario, " +
        "COMISION_COBRANZA as ComisionCobranza";

    public async Task<Usuario?> BuscarPorCodigoAsync(string codigo)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Usuario>(
            $"SELECT {SelectCols} FROM USUARIOS WHERE CODIGO_USUARIO = @Codigo",
            new { Codigo = codigo });
    }

    public async Task<IEnumerable<Usuario>> ListarTodosAsync()
    {
        using var conn = _factory.Create();
        // CARGAR_USUARIO_COMPLETO_CS devuelve columnas sin alias — usamos query directa
        return await conn.QueryAsync<Usuario>(
            $"SELECT {SelectCols} FROM USUARIOS ORDER BY NOMBRE_USUARIO");
    }

    public async Task<bool> GuardarAsync(Usuario u)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", 0);
        p.Add("@CI", u.CiUsuario);
        p.Add("@CODIGO", u.CodigoUsuario);
        p.Add("@NOMBRE", u.NombreUsuario);
        p.Add("@DIRECCION", u.DireccionUsuario);
        p.Add("@TELEFONO", u.TelefonoUsuario);
        p.Add("@SALARIO", u.SalarioUsuario);
        p.Add("@COMISION", u.ComisionUsuario);
        p.Add("@LOCAL", u.LocalUsuario);
        p.Add("@ZONA", u.ZonaUsuario);
        p.Add("@PASS", u.ContrasenaUsuario);
        p.Add("@CARGO", u.CargoUsuario);
        p.Add("@DESCUENTO", u.PermisoDescuento);
        p.Add("@STOCK", u.PermisoStock);
        p.Add("@PRECIO", u.PermisoPrecio);
        p.Add("@ELIMINAR", u.PermisoEliminar);
        p.Add("@CUOTAS", u.PermisoCobrarCuotas);
        p.Add("@COMPRAS", u.PermisoCompras);
        p.Add("@FACTURA", u.PermisoEliminarFactura);
        p.Add("@INGRESO", DateTime.Today);
        p.Add("@COMISION_COBRANZA", u.ComisionCobranza);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("AGREGAR_USUARIO_CS", p, commandType: CommandType.StoredProcedure);
        // El SP devuelve @msg='GUARDADO' en éxito, o un motivo de rechazo ('EXISTE CODIGO',
        // 'EXISTE CI', 'EXISTE PASS' —contraseña ya usada por OTRO usuario del sistema—,
        // 'EXISTE NOMBRE', 'EXISTE ADMIN') SIN insertar nada. Antes este método ignoraba
        // @msg y siempre devolvía true, así que la UI mostraba "guardado correctamente"
        // aunque el alta hubiera sido rechazada — bug real detectado: un funcionario nuevo
        // no aparecía después en el listado porque nunca se había insertado.
        var msg = p.Get<string?>("@msg");
        if (!string.IsNullOrWhiteSpace(msg) && !string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(msg);
        return true;
    }

    public async Task<bool> ActualizarAsync(Usuario u)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", u.IdUsuario);
        p.Add("@CI", u.CiUsuario);
        p.Add("@CODIGO", u.CodigoUsuario);
        p.Add("@NOMBRE", u.NombreUsuario);
        p.Add("@DIRECCION", u.DireccionUsuario);
        p.Add("@TELEFONO", u.TelefonoUsuario);
        p.Add("@SALARIO", u.SalarioUsuario);
        p.Add("@COMISION", u.ComisionUsuario);
        p.Add("@LOCAL", u.LocalUsuario);
        p.Add("@ZONA", u.ZonaUsuario);
        p.Add("@PASS", u.ContrasenaUsuario);
        p.Add("@CARGO", u.CargoUsuario);
        p.Add("@DESCUENTO", u.PermisoDescuento);
        p.Add("@STOCK", u.PermisoStock);
        p.Add("@PRECIO", u.PermisoPrecio);
        p.Add("@ELIMINAR", u.PermisoEliminar);
        p.Add("@CUOTAS", u.PermisoCobrarCuotas);
        p.Add("@COMPRAS", u.PermisoCompras);
        p.Add("@FACTURA", u.PermisoEliminarFactura);
        p.Add("@INGRESO", DateTime.Today);
        p.Add("@COMISION_COBRANZA", u.ComisionCobranza);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ACTUALIZAR_USUARIO_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string?>("@msg");
        if (!string.IsNullOrWhiteSpace(msg) && !string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(msg);
        return true;
    }

    public async Task<bool> EliminarAsync(int idUsuario)
    {
        using var conn = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", idUsuario);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ELIMINAR_USUARIO_CS", p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string?>("@msg");
        if (!string.IsNullOrWhiteSpace(msg) && !string.Equals(msg, "ELIMINADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(msg);
        return true;
    }
}
