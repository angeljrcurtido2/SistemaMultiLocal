using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

// ── Interfaces ──────────────────────────────────────────────────────────────

public interface IMaestrosBancoRepository      { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); }
public interface IMaestrosCategoriaRepository  { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); Task<IEnumerable<Categoria>> ListarTodosAsync(); }
public interface IMaestrosSubcategoriaRepository { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task<IEnumerable<Subcategoria>> ListarTodosAsync(); Task<IEnumerable<Subcategoria>> ListarPorCategoriaAsync(int idCategoria); Task InsertarAsync(int idCategoria, string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); }
public interface IMaestrosMarcaRepository      { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); Task<IEnumerable<Marca>> ListarTodosAsync(); }
public interface IMaestrosProveedorRepository  { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task InsertarCompletoAsync(Proveedor p); Task ActualizarAsync(int id, string c, string n); Task ActualizarCompletoAsync(Proveedor p); Task EliminarAsync(int id); Task<IEnumerable<Proveedor>> ListarTodosAsync(); }
public interface IMaestrosSeccionRepository    { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); Task<IEnumerable<Seccion>> ListarTodosAsync(); }
public interface IMaestrosMedidaRepository     { Task<IEnumerable<MaestroItem>> ListarAsync(string? f); Task InsertarAsync(string c, string n); Task ActualizarAsync(int id, string c, string n); Task EliminarAsync(int id); Task<IEnumerable<Medida>> ListarTodosAsync(); }
public interface IMaestrosPaisRepository       { Task<IEnumerable<Pais>> ListarTodosAsync(); }

public interface IMaestroGenericoRepository
{
    Task<IEnumerable<MaestroItem>> ListarAsync(string tabla, string colId, string colCod, string colNom, string? filtro);
    Task InsertarAsync(string tabla, string colCod, string colNom, string codigo, string nombre);
    Task ActualizarAsync(string tabla, string colId, string colCod, string colNom, int id, string codigo, string nombre);
    Task EliminarAsync(string tabla, string colId, int id);
}

// ── Helper para SPs con @msg OUTPUT ─────────────────────────────────────────

internal static class SpHelper
{
    internal static async Task ExecSp(System.Data.IDbConnection conn, string sp, DynamicParameters p)
    {
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);
        await conn.ExecuteAsync(sp, p, commandType: CommandType.StoredProcedure);
        var msg = p.Get<string?>("@msg");
        if (!string.IsNullOrWhiteSpace(msg))
            throw new InvalidOperationException(msg);
    }
}

// ── Base genérica (solo para SELECT dinámico) ─────────────────────────────────

public abstract class MaestroRepositoryBase
{
    protected readonly IDbConnectionFactory Factory;
    protected MaestroRepositoryBase(IDbConnectionFactory f) => Factory = f;

    protected async Task<IEnumerable<MaestroItem>> Listar(
        string tabla, string colId, string colCod, string colNom, string? filtro)
    {
        using var conn = Factory.Create();
        var where = string.IsNullOrWhiteSpace(filtro) ? "" :
            $"WHERE {colCod} LIKE @F OR {colNom} LIKE @F";
        var rows = await conn.QueryAsync(
            $"SELECT {colId} as Id, {colCod} as Codigo, {colNom} as Nombre " +
            $"FROM {tabla} {where} ORDER BY {colNom}",
            new { F = $"%{filtro}%" });
        return rows.Select(r => new MaestroItem((int)r.Id, (string)r.Codigo, (string)r.Nombre));
    }
}

// ── BANCOS — SP: AGREGAR_BANCO_CS(@Id,@Cod,@Banco,@msg)
//                 ACTUALIZAR_BANCO_CS(@Cod,@Banco,@ID,@msg)
//                 ELIMINAR_BANCO_CS(@Id,@msg)
//            Tabla: ID_BANCO, CODIGO, BANCO ────────────────────────────────────

public class MaestrosBancoRepository : MaestroRepositoryBase, IMaestrosBancoRepository
{
    public MaestrosBancoRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("BANCOS", "ID_BANCO", "CODIGO", "BANCO", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", 0); p.Add("@Cod", c); p.Add("@Banco", n);
        await SpHelper.ExecSp(conn, "AGREGAR_BANCO_CS", p);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id); p.Add("@Cod", c); p.Add("@Banco", n);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_BANCO_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_BANCO_CS", p);
    }
}

// ── CATEGORIAS — SP: AGREGAR_CAT_SUBCAT_CS / ACTUALIZAR_CATEGORIA_CS / ELIMINAR_CATEGORIA_CS
//                Tabla: ID_CAT, CODIGO, CATEGORIA ─────────────────────────────

public class MaestrosCategoriaRepository : MaestroRepositoryBase, IMaestrosCategoriaRepository
{
    public MaestrosCategoriaRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("CATEGORIAS", "ID_CAT", "CODIGO", "CATEGORIA", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        var codigoExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CATEGORIAS WITH (UPDLOCK, HOLDLOCK) WHERE CODIGO = @C",
            new { C = c }, tx);
        if (codigoExiste > 0)
        {
            tx.Rollback();
            throw new InvalidOperationException("EXISTE CODIGO");
        }

        var nombreExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CATEGORIAS WITH (UPDLOCK, HOLDLOCK) WHERE CATEGORIA = @N",
            new { N = n }, tx);
        if (nombreExiste > 0)
        {
            tx.Rollback();
            throw new InvalidOperationException("EXISTE CATEGORIA");
        }

        var nextId = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(ID_CAT), 0) + 1 FROM CATEGORIAS WITH (UPDLOCK, HOLDLOCK)",
            transaction: tx);

        await conn.ExecuteAsync(
            "INSERT INTO CATEGORIAS (ID_CAT, CODIGO, CATEGORIA) VALUES (@Id, @C, @N)",
            new { Id = nextId, C = c, N = n }, tx);

        tx.Commit();
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id); p.Add("@Cod", c); p.Add("@Cat", n);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_CATEGORIA_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_CATEGORIA_CS", p);
    }

    public async Task<IEnumerable<Categoria>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Categoria>(
            "SELECT ID_CAT as IdCategoria, CODIGO as CodigoCategoria, CATEGORIA as NombreCategoria " +
            "FROM CATEGORIAS ORDER BY CATEGORIA");
    }
}

// ── SUBCATEGORIAS ───────────────────────────────────────────────────────────

public class MaestrosSubcategoriaRepository : MaestroRepositoryBase, IMaestrosSubcategoriaRepository
{
    public MaestrosSubcategoriaRepository(IDbConnectionFactory f) : base(f) { }

    public async Task<IEnumerable<MaestroItem>> ListarAsync(string? f)
    {
        using var conn = Factory.Create();
        var where = string.IsNullOrWhiteSpace(f) ? "" : "WHERE s.CODIGO LIKE @F OR s.SUBCATEGORIA LIKE @F OR c.CATEGORIA LIKE @F";
        return await conn.QueryAsync<MaestroItem>(
            $"SELECT s.ID_SUB as Id, s.CODIGO as Codigo, s.SUBCATEGORIA as Nombre " +
            $"FROM SUBCATEGORIAS s LEFT JOIN CATEGORIAS c ON c.ID_CAT = s.ID_CAT {where} ORDER BY s.SUBCATEGORIA",
            new { F = $"%{f}%" });
    }

    public async Task<IEnumerable<Subcategoria>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Subcategoria>(
            "SELECT s.ID_SUB as IdSubcategoria, s.ID_CAT as IdCategoria, s.CODIGO as CodigoSubcategoria, " +
            "s.SUBCATEGORIA as NombreSubcategoria, ISNULL(c.CATEGORIA, '') as CategoriaNombre " +
            "FROM SUBCATEGORIAS s LEFT JOIN CATEGORIAS c ON c.ID_CAT = s.ID_CAT ORDER BY s.SUBCATEGORIA");
    }

    public async Task<IEnumerable<Subcategoria>> ListarPorCategoriaAsync(int idCategoria)
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Subcategoria>(
            "SELECT s.ID_SUB as IdSubcategoria, s.ID_CAT as IdCategoria, s.CODIGO as CodigoSubcategoria, " +
            "s.SUBCATEGORIA as NombreSubcategoria, ISNULL(c.CATEGORIA, '') as CategoriaNombre " +
            "FROM SUBCATEGORIAS s LEFT JOIN CATEGORIAS c ON c.ID_CAT = s.ID_CAT " +
            "WHERE s.ID_CAT = @IdCategoria ORDER BY s.SUBCATEGORIA",
            new { IdCategoria = idCategoria });
    }

    public async Task InsertarAsync(int idCategoria, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", 0);
        p.Add("@IDCAT", idCategoria);
        p.Add("@CODIGO", c);
        p.Add("@NOMBRE", n);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("AGREGAR_SUBCATEGORIA_CS", p, commandType: CommandType.StoredProcedure);

        var msg = p.Get<string>("@msg");
        if (!string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? "Error al guardar subcategoria." : msg);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id);
        p.Add("@Cod", c);
        p.Add("@Subcat", n);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ACTUALIZAR_SUBCATEGORIA_CS", p, commandType: CommandType.StoredProcedure);

        var msg = p.Get<string>("@msg");
        if (!string.Equals(msg, "GUARDADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? "Error al actualizar subcategoria." : msg);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ELIMINAR_SUBCATEGORIA_CS", p, commandType: CommandType.StoredProcedure);

        var msg = p.Get<string>("@msg");
        if (!string.Equals(msg, "ELIMINADO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? "Error al eliminar subcategoria." : msg);
    }
}

// ── MARCAS — SP: AGREGAR_MARCA_CS(@ID,@Codigo,@Marca,@msg)
//                 ACTUALIZAR_MARCA_CS(@Cod,@Marca,@ID,@msg)
//                 ELIMINAR_MARCA_CS(@Id,@msg)
//             Tabla: ID_MARCA, CODIGO, MARCA ────────────────────────────────────

public class MaestrosMarcaRepository : MaestroRepositoryBase, IMaestrosMarcaRepository
{
    public MaestrosMarcaRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("MARCAS", "ID_MARCA", "CODIGO", "MARCA", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", 0); p.Add("@Codigo", c); p.Add("@Marca", n);
        await SpHelper.ExecSp(conn, "AGREGAR_MARCA_CS", p);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id); p.Add("@Cod", c); p.Add("@Marca", n);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_MARCA_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_MARCA_CS", p);
    }

    public async Task<IEnumerable<Marca>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Marca>(
            "SELECT ID_MARCA as IdMarca, CODIGO as CodigoMarca, MARCA as NombreMarca " +
            "FROM MARCAS ORDER BY MARCA");
    }
}

// ── PROVEEDORES — SP: AGREGAR_PROVEEDOR_CS / ACTUALIZAR_PROVEEDOR_CS / ELIMINAR_PROVEEDOR_CS
//                 Tabla: ID_PROVEEDOR, CODIGO_PROVEEDOR, NOMBRE_PROVEEDOR ──────

public class MaestrosProveedorRepository : MaestroRepositoryBase, IMaestrosProveedorRepository
{
    public MaestrosProveedorRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("PROVEEDORES", "ID_PROVEEDOR", "CODIGO_PROVEEDOR", "NOMBRE_PROVEEDOR", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID_PROVEEDOR", 0); p.Add("@CODIGO_PROVEEDOR", c);
        p.Add("@RUC_PROVEEDOR", ""); p.Add("@NOMBRE_PROVEEDOR", n);
        p.Add("@DIRECCION_PROVEEDOR", ""); p.Add("@TELEFONO_PROVEEDOR", "");
        p.Add("@CIUDAD_PROVEEDOR", ""); p.Add("@E_MAIL_PROVEEDOR", "");
        p.Add("@WEB_PROVEEDOR", ""); p.Add("@CONTACTO", "");
        p.Add("@CARGO", ""); p.Add("@CELULAR", ""); p.Add("@CORREO", "");
        p.Add("@FECHA_REGISTRO", DateTime.Today);
        await SpHelper.ExecSp(conn, "AGREGAR_PROVEEDOR_CS", p);
    }

    public async Task InsertarCompletoAsync(Proveedor prov)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID_PROVEEDOR", 0); p.Add("@CODIGO_PROVEEDOR", prov.CodigoProveedor);
        p.Add("@RUC_PROVEEDOR", prov.RucProveedor); p.Add("@NOMBRE_PROVEEDOR", prov.NombreProveedor);
        p.Add("@DIRECCION_PROVEEDOR", prov.DireccionProveedor); p.Add("@TELEFONO_PROVEEDOR", prov.TelefonoProveedor);
        p.Add("@CIUDAD_PROVEEDOR", prov.CiudadProveedor); p.Add("@E_MAIL_PROVEEDOR", prov.EmailProveedor);
        p.Add("@WEB_PROVEEDOR", prov.WebProveedor); p.Add("@CONTACTO", prov.ContactoNombre);
        p.Add("@CARGO", prov.ContactoCargo); p.Add("@CELULAR", prov.ContactoCelular); p.Add("@CORREO", prov.ContactoCorreo);
        p.Add("@FECHA_REGISTRO", DateTime.Today);
        await SpHelper.ExecSp(conn, "AGREGAR_PROVEEDOR_CS", p);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var actual = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM PROVEEDORES WHERE ID_PROVEEDOR=@Id", new { Id = id });
        var p = new DynamicParameters();
        p.Add("@ID_PROVEEDOR", id); p.Add("@CODIGO_PROVEEDOR", c);
        p.Add("@RUC_PROVEEDOR", (string?)actual?.RUC_PROVEEDOR ?? "");
        p.Add("@NOMBRE_PROVEEDOR", n);
        p.Add("@DIRECCION_PROVEEDOR", (string?)actual?.DIRECCION_PROVEEDOR ?? "");
        p.Add("@TELEFONO_PROVEEDOR", (string?)actual?.TELEFONO_PROVEEDOR ?? "");
        p.Add("@CIUDAD_PROVEEDOR", (string?)actual?.CIUDAD_PROVEEDOR ?? "");
        p.Add("@E_MAIL_PROVEEDOR", (string?)actual?.E_MAIL_PROVEEDOR ?? "");
        p.Add("@WEB_PROVEEDOR", (string?)actual?.WEB_PROVEEDOR ?? "");
        p.Add("@CONTACTO", (string?)actual?.CONTACTO ?? "");
        p.Add("@CARGO", (string?)actual?.CARGO ?? "");
        p.Add("@CELULAR", (string?)actual?.CELULAR ?? "");
        p.Add("@CORREO", (string?)actual?.CORREO ?? "");
        p.Add("@FECHA_REGISTRO", actual?.FECHA_REGISTRO ?? DateTime.Today);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_PROVEEDOR_CS", p);
    }

    public async Task ActualizarCompletoAsync(Proveedor prov)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID_PROVEEDOR", prov.IdProveedor); p.Add("@CODIGO_PROVEEDOR", prov.CodigoProveedor);
        p.Add("@RUC_PROVEEDOR", prov.RucProveedor); p.Add("@NOMBRE_PROVEEDOR", prov.NombreProveedor);
        p.Add("@DIRECCION_PROVEEDOR", prov.DireccionProveedor); p.Add("@TELEFONO_PROVEEDOR", prov.TelefonoProveedor);
        p.Add("@CIUDAD_PROVEEDOR", prov.CiudadProveedor); p.Add("@E_MAIL_PROVEEDOR", prov.EmailProveedor);
        p.Add("@WEB_PROVEEDOR", prov.WebProveedor); p.Add("@CONTACTO", prov.ContactoNombre);
        p.Add("@CARGO", prov.ContactoCargo); p.Add("@CELULAR", prov.ContactoCelular); p.Add("@CORREO", prov.ContactoCorreo);
        p.Add("@FECHA_REGISTRO", prov.FechaRegistro ?? DateTime.Today);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_PROVEEDOR_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_PROVEEDOR_CS", p);
    }

    public async Task<IEnumerable<Proveedor>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Proveedor>(
            "SELECT ID_PROVEEDOR as IdProveedor, CODIGO_PROVEEDOR as CodigoProveedor, " +
            "RUC_PROVEEDOR as RucProveedor, NOMBRE_PROVEEDOR as NombreProveedor, " +
            "DIRECCION_PROVEEDOR as DireccionProveedor, TELEFONO_PROVEEDOR as TelefonoProveedor, " +
            "CIUDAD_PROVEEDOR as CiudadProveedor, E_MAIL_PROVEEDOR as EmailProveedor, " +
            "WEB_PROVEEDOR as WebProveedor, CONTACTO as ContactoNombre, " +
            "CARGO as ContactoCargo, CELULAR as ContactoCelular, CORREO as ContactoCorreo, " +
            "FECHA_REGISTRO as FechaRegistro " +
            "FROM PROVEEDORES ORDER BY NOMBRE_PROVEEDOR");
    }
}

// ── SECCIONES — SP: AGREGAR_SECCION_CS(@Id,@Cod,@Seccion,@msg)
//                    ACTUALIZAR_SECCION_CS(@Cod,@Seccion,@ID,@msg)
//                    ELIMINAR_SECCION_CS(@Id,@msg)
//               Tabla: IDSECC, CODIGO, SECCION ──────────────────────────────────

public class MaestrosSeccionRepository : MaestroRepositoryBase, IMaestrosSeccionRepository
{
    public MaestrosSeccionRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("SECCIONES", "IDSECC", "CODIGO", "SECCION", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", 0); p.Add("@Cod", c); p.Add("@Seccion", n);
        await SpHelper.ExecSp(conn, "AGREGAR_SECCION_CS", p);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id); p.Add("@Cod", c); p.Add("@Seccion", n);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_SECCION_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_SECCION_CS", p);
    }

    public async Task<IEnumerable<Seccion>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Seccion>(
            "SELECT IDSECC as IdSeccion, CODIGO as CodigoSeccion, SECCION as NombreSeccion " +
            "FROM SECCIONES ORDER BY SECCION");
    }
}

// ── MEDIDAS — SP: AGREGAR_MEDIDA_CS(@Id,@Cod,@UM,@msg)
//                  ACTUALIZAR_MEDIDA_CS(@COD,@UM,@ID,@msg)
//                  ELIMINAR_MEDIDA_CS(@Id,@msg)
//              Tabla: ID, CODIGO, UM ────────────────────────────────────────────

public class MaestrosMedidaRepository : MaestroRepositoryBase, IMaestrosMedidaRepository
{
    public MaestrosMedidaRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(string? f) =>
        Listar("MEDIDAS", "ID", "CODIGO", "UM", f);

    public async Task InsertarAsync(string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", 0); p.Add("@Cod", c); p.Add("@UM", n);
        await SpHelper.ExecSp(conn, "AGREGAR_MEDIDA_CS", p);
    }

    public async Task ActualizarAsync(int id, string c, string n)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@ID", id); p.Add("@COD", c); p.Add("@UM", n);
        await SpHelper.ExecSp(conn, "ACTUALIZAR_MEDIDA_CS", p);
    }

    public async Task EliminarAsync(int id)
    {
        using var conn = Factory.Create();
        var p = new DynamicParameters();
        p.Add("@Id", id);
        await SpHelper.ExecSp(conn, "ELIMINAR_MEDIDA_CS", p);
    }

    public async Task<IEnumerable<Medida>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Medida>(
            "SELECT ID as IdMedida, CODIGO as CodigoMedida, UM as NombreMedida FROM MEDIDAS ORDER BY UM");
    }
}

// ── PAISES — SP: AGREGAR_PAIS_CS(@ID,@Codigo,@Pais,@msg)
//                 ACTUALIZAR_PAIS_CS(@Codigo,@Pais,@ID,@msg)
//                 ELIMINAR_PAIS_CS(@Id,@msg)
//             Tabla: ID, CODIGO, PAIS ───────────────────────────────────────────

public class MaestrosPaisRepository : MaestroRepositoryBase, IMaestrosPaisRepository
{
    public MaestrosPaisRepository(IDbConnectionFactory f) : base(f) { }

    public async Task<IEnumerable<Pais>> ListarTodosAsync()
    {
        using var conn = Factory.Create();
        return await conn.QueryAsync<Pais>(
            "SELECT ID as IdPais, CODIGO as CodigoPais, PAIS as NombrePais FROM PAISES ORDER BY PAIS");
    }
}

// ── LOCALES — SP: AGREGAR_LOCAL_CS(@Id,@Codigo,@Nombre,@Direccion,@Ciudad,@Telefono,@msg)
//                  ACTUALIZAR_LOCAL_CS(@Codigo,@Nombre,@Direccion,@Ciudad,@Telefono,@ID,@msg)
//                  ELIMINAR_LOCAL_CS(@Id,@msg) ────────────────────────────────

// (LocalRepository ya maneja este; ProcedenciasWindow usa INSERT directo vía GenéricoRepository)

// ── GENÉRICO (para MedidasWindow, ProcedenciasWindow, SeccionesWindow, LocalesWindow) ──

public class MaestroGenericoRepository : MaestroRepositoryBase, IMaestroGenericoRepository
{
    public MaestroGenericoRepository(IDbConnectionFactory f) : base(f) { }

    public Task<IEnumerable<MaestroItem>> ListarAsync(
        string tabla, string colId, string colCod, string colNom, string? filtro)
        => Listar(tabla, colId, colCod, colNom, filtro);

    // Para maestros simples sin lógica especial en SP, usamos INSERT/UPDATE/DELETE directo
    public async Task InsertarAsync(string tabla, string colCod, string colNom, string codigo, string nombre)
    {
        // Intentar SP específico primero; fallback a INSERT directo
        var spMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MEDIDAS"]   = "AGREGAR_MEDIDA_CS",
            ["PAISES"]    = "AGREGAR_PAIS_CS",
            ["SECCIONES"] = "AGREGAR_SECCION_CS",
            ["LOCALES"]   = "AGREGAR_LOCAL_CS",
        };

        using var conn = Factory.Create();

        if (spMap.TryGetValue(tabla, out var sp))
        {
            var p = new DynamicParameters();
            // Parámetros comunes a todos estos SPs
            p.Add("@Id", 0);
            if (tabla.Equals("LOCALES", StringComparison.OrdinalIgnoreCase))
            {
                p.Add("@Codigo", codigo); p.Add("@Nombre", nombre);
                p.Add("@Direccion", ""); p.Add("@Ciudad", ""); p.Add("@Telefono", "");
            }
            else if (tabla.Equals("MEDIDAS", StringComparison.OrdinalIgnoreCase))
            { p.Add("@Cod", codigo); p.Add("@UM", nombre); }
            else if (tabla.Equals("PAISES", StringComparison.OrdinalIgnoreCase))
            { p.Add("@ID", 0); p.Add("@Codigo", codigo); p.Add("@Pais", nombre); }
            else if (tabla.Equals("SECCIONES", StringComparison.OrdinalIgnoreCase))
            { p.Add("@Cod", codigo); p.Add("@Seccion", nombre); }
            await SpHelper.ExecSp(conn, sp, p);
        }
        else
        {
            await conn.ExecuteAsync(
                $"INSERT INTO {tabla} ({colCod},{colNom}) VALUES (@C,@N)", new { C = codigo, N = nombre });
        }
    }

    public async Task ActualizarAsync(
        string tabla, string colId, string colCod, string colNom, int id, string codigo, string nombre)
    {
        var spMap = new Dictionary<string, (string sp, string pCod, string pNom)>(StringComparer.OrdinalIgnoreCase)
        {
            ["MEDIDAS"]   = ("ACTUALIZAR_MEDIDA_CS",   "@COD",    "@UM"),
            ["PAISES"]    = ("ACTUALIZAR_PAIS_CS",     "@Codigo", "@Pais"),
            ["SECCIONES"] = ("ACTUALIZAR_SECCION_CS",  "@Cod",    "@Seccion"),
            ["LOCALES"]   = ("ACTUALIZAR_LOCAL_CS",    "@Codigo", "@Nombre"),
        };

        using var conn = Factory.Create();

        if (spMap.TryGetValue(tabla, out var entry))
        {
            var p = new DynamicParameters();
            p.Add("@ID", id);
            p.Add(entry.pCod, codigo);
            p.Add(entry.pNom, nombre);
            if (tabla.Equals("LOCALES", StringComparison.OrdinalIgnoreCase))
            { p.Add("@Direccion", ""); p.Add("@Ciudad", ""); p.Add("@Telefono", ""); }
            await SpHelper.ExecSp(conn, entry.sp, p);
        }
        else
        {
            await conn.ExecuteAsync(
                $"UPDATE {tabla} SET {colCod}=@C, {colNom}=@N WHERE {colId}=@Id",
                new { C = codigo, N = nombre, Id = id });
        }
    }

    public async Task EliminarAsync(string tabla, string colId, int id)
    {
        var spMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MEDIDAS"]   = "ELIMINAR_MEDIDA_CS",
            ["PAISES"]    = "ELIMINAR_PAIS_CS",
            ["SECCIONES"] = "ELIMINAR_SECCION_CS",
            ["LOCALES"]   = "ELIMINAR_LOCAL_CS",
        };

        using var conn = Factory.Create();
        if (spMap.TryGetValue(tabla, out var sp))
        {
            var p = new DynamicParameters();
            p.Add("@Id", id);
            await SpHelper.ExecSp(conn, sp, p);
        }
        else
        {
            await conn.ExecuteAsync($"DELETE FROM {tabla} WHERE {colId}=@Id", new { Id = id });
        }
    }
}


