using CrediSoft.Core.Models;
using Dapper;
using System.Data;

namespace CrediSoft.Data.Repositories;

public interface IArticuloRepository
{
    Task<Articulo?> BuscarPorCodigoAsync(string codigo);
    Task<Articulo?> BuscarPorIdAsync(int id);
    Task<IEnumerable<Articulo>> BuscarAsync(string termino);
    Task<IEnumerable<Articulo>> BuscarTodosAsync();
    Task<Price?> ObtenerPrecioLocalAsync(int idArt, int idLocal);
    Task<IEnumerable<Price>> ObtenerStockTodosLocalesAsync(int idArt);
    Task<bool> ActualizarPreciosAsync(int idArt, int idLocal, decimal pcosto, decimal pventa, decimal contado, decimal promo);
    Task<bool> ActualizarStockAsync(int idArt, int idLocal, decimal cantidad, string modo, int idUsuario);
    Task<bool> InhabilitarEnLocalAsync(int idArt, int idLocal, bool inhabilitar);
    Task<int> GuardarAsync(Articulo articulo, int idLocal, int idUsuario);
    Task<bool> ActualizarAsync(Articulo articulo);
    Task<(IEnumerable<Articulo> arts, IEnumerable<Price> prices)> ObtenerVisorAsync();
}

public class ArticuloRepository : IArticuloRepository
{
    private readonly IDbConnectionFactory _factory;
    public ArticuloRepository(IDbConnectionFactory factory) => _factory = factory;

    // Columnas reales: ARTICULOS(ID,CA,SERIAL,D,PRES,SMIN,IDS,IDPR,IDC,IDSBC,IDPAIS,IDM,IDMED,GRA,IVA,MAXCUOTA,SLC,VU,VTO,FCOMPRA,IDUCOMPRA,ES)
    // JOINs: SECCIONES(IDSECC,CODIGO,SECCION) CATEGORIAS(ID_CAT,CODIGO,CATEGORIA)
    //        MARCAS(ID_MARCA,CODIGO,MARCA) PROVEEDORES(ID_PROVEEDOR,CODIGO_PROVEEDOR,NOMBRE_PROVEEDOR)
    //        LOCALES(ID_LOCAL,CODIGO,NOMBRE) PAISES(ID,CODIGO,PAIS) MEDIDAS(ID,CODIGO,UM)

    private const string ArtJoin =
        "FROM ARTICULOS a " +
        "LEFT JOIN SECCIONES s ON a.IDS = s.IDSECC " +
        "LEFT JOIN CATEGORIAS c ON a.IDC = c.ID_CAT " +
        "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
        "LEFT JOIN PROVEEDORES pr ON a.IDPR = pr.ID_PROVEEDOR " +
        "LEFT JOIN PAISES pa ON a.IDPAIS = pa.ID " +
        "LEFT JOIN MEDIDAS me ON a.IDMED = me.ID";

    private const string ArtCols =
        "a.ID, a.CA, a.SERIAL, a.D, a.PRES, a.SMIN, a.IDS, a.IDPR, a.IDC, a.IDSBC, " +
        "a.IDPAIS, a.IDM, a.IDMED, a.GRA, a.IVA, a.MAXCUOTA, a.SLC, a.VU, a.VTO, " +
        "a.FCOMPRA, a.IDUCOMPRA, a.ES, " +
        "s.SECCION as SeccionNombre, c.CATEGORIA as CategoriaNombre, " +
        "m.MARCA as MarcaNombre, pr.NOMBRE_PROVEEDOR as ProveedorNombre, " +
        "pa.PAIS as PaisNombre, me.UM as MedidaNombre";

    public async Task<Articulo?> BuscarPorCodigoAsync(string codigo)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Articulo>(
            $"SELECT {ArtCols} {ArtJoin} WHERE a.CA = @Codigo", new { Codigo = codigo });
    }

    public async Task<Articulo?> BuscarPorIdAsync(int id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Articulo>(
            $"SELECT {ArtCols} {ArtJoin} WHERE a.ID = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Articulo>> BuscarAsync(string termino)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Articulo>(
            "SELECT TOP 300 a.ID, a.CA, a.D, a.ES, " +
            "m.MARCA as MarcaNombre " +
            "FROM ARTICULOS a " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            "WHERE (a.CA LIKE @T OR a.D LIKE @T) AND a.ES = 1 ORDER BY a.D",
            new { T = $"%{termino}%" });
    }

    public async Task<IEnumerable<Articulo>> BuscarTodosAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Articulo>(
            "SELECT a.ID, a.CA, a.D, a.ES, a.IDS, a.IDC, a.IDSBC, a.IDM, " +
            "s.SECCION as SeccionNombre, c.CATEGORIA as CategoriaNombre, " +
            "m.MARCA as MarcaNombre, sc.SUBCATEGORIA as SubcategoriaNombre " +
            "FROM ARTICULOS a " +
            "LEFT JOIN SECCIONES s ON a.IDS = s.IDSECC " +
            "LEFT JOIN CATEGORIAS c ON a.IDC = c.ID_CAT " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            "LEFT JOIN SUBCATEGORIAS sc ON a.IDSBC = sc.ID_SUB " +
            "ORDER BY a.D");
    }

    public async Task<bool> InhabilitarEnLocalAsync(int idArt, int idLocal, bool inhabilitar)
    {
        using var conn = _factory.Create();
        var deletado = inhabilitar ? 1 : 0;
        var rows = await conn.ExecuteAsync(
            "UPDATE PRICES SET DELETADO=@D WHERE IDART=@Art AND IDLOCAL=@Local",
            new { Art = idArt, Local = idLocal, D = deletado });
        return rows > 0;
    }

    public async Task<Price?> ObtenerPrecioLocalAsync(int idArt, int idLocal)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Price>(
            "SELECT IDPRICES, IDART, IDLOCAL, S, PC, PVENTA, CONTADO, PPROMO, PFUTURO, " +
            "FCOMPRA, FVENTA, FMP, FMS, INICIO, FIN, DELETADO, PR " +
            "FROM PRICES WHERE IDART = @Art AND IDLOCAL = @Local",
            new { Art = idArt, Local = idLocal });
    }

    public async Task<IEnumerable<Price>> ObtenerStockTodosLocalesAsync(int idArt)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Price>(
            "SELECT p.IDPRICES, p.IDLOCAL, p.S, p.PC, p.PVENTA, p.CONTADO, " +
            "l.NOMBRE as LocalNombre " +  // columna real: NOMBRE
            "FROM PRICES p " +
            "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
            "WHERE p.IDART = @Art AND p.DELETADO = 0 ORDER BY l.NOMBRE",
            new { Art = idArt });
    }

    public async Task<bool> ActualizarPreciosAsync(
        int idArt, int idLocal, decimal pcosto, decimal pventa, decimal contado, decimal promo)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE PRICES SET PC=@Pc, PVENTA=@Pv, CONTADO=@Co, PPROMO=@Pp, FMP=GETDATE() " +
            "WHERE IDART=@Art AND IDLOCAL=@Local AND DELETADO=0",
            new { Art = idArt, Local = idLocal, Pc = pcosto, Pv = pventa, Co = contado, Pp = promo });
        return rows > 0;
    }

    public async Task<bool> ActualizarStockAsync(int idArt, int idLocal, decimal cantidad, string modo, int idUsuario)
    {
        using var conn = _factory.Create();
        var sql = modo switch
        {
            "A" => "UPDATE PRICES SET S = S + @Cant, IDUMODSTOCK=@Idu WHERE IDART=@Art AND IDLOCAL=@Local AND DELETADO=0",
            "R" => "UPDATE PRICES SET S = S - @Cant, IDUMODSTOCK=@Idu WHERE IDART=@Art AND IDLOCAL=@Local AND DELETADO=0",
            _   => "UPDATE PRICES SET S = @Cant, IDUMODSTOCK=@Idu WHERE IDART=@Art AND IDLOCAL=@Local AND DELETADO=0"
        };
        var rows = await conn.ExecuteAsync(sql, new { Art = idArt, Local = idLocal, Cant = cantidad, Idu = idUsuario });
        return rows > 0;
    }

    public async Task<int> GuardarAsync(Articulo a, int idLocal, int idUsuario)
    {
        using var conn = _factory.Create();
        // AGREGAR_ARTICULO_CS: @ID,@CA,@SERIAL,@D,@PRES,@SMIN,@IDS,@IDPR,@IDC,@IDSBC,
        //   @IDPAIS,@IDM,@IDMED,@GRA,@IVA,@MAXCUOTA,@SLC,@VU,@IDUCOMPRA,@ES,
        //   @IDPRICES,@IDART,@IdLocal,@msg
        var p = new DynamicParameters();
        p.Add("@ID", 0);
        p.Add("@CA", a.Ca); p.Add("@SERIAL", a.Serial); p.Add("@D", a.D);
        p.Add("@PRES", a.Pres); p.Add("@SMIN", a.Smin);
        p.Add("@IDS", a.Ids); p.Add("@IDPR", a.Idpr); p.Add("@IDC", a.Idc);
        p.Add("@IDSBC", a.Idsbc); p.Add("@IDPAIS", a.Idpais);
        p.Add("@IDM", a.Idm); p.Add("@IDMED", a.Idmed);
        p.Add("@GRA", a.Gra); p.Add("@IVA", a.Iva);
        p.Add("@MAXCUOTA", a.Maxcuota); p.Add("@SLC", a.Slc); p.Add("@VU", a.Vu);
        p.Add("@IDUCOMPRA", idUsuario); p.Add("@ES", 1);
        p.Add("@IDPRICES", 0); p.Add("@IDART", 0); p.Add("@IdLocal", idLocal);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("AGREGAR_ARTICULO_CS", p, commandType: CommandType.StoredProcedure);
        // Obtener ID generado
        return await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 ID FROM ARTICULOS WHERE CA=@Ca ORDER BY ID DESC", new { Ca = a.Ca });
    }

    public async Task<(IEnumerable<Articulo> arts, IEnumerable<Price> prices)> ObtenerVisorAsync()
    {
        using var conn = _factory.Create();
        var arts = await conn.QueryAsync<Articulo>(
            "SELECT a.ID, a.CA, a.D, a.MAXCUOTA, a.ES, " +
            "m.MARCA as MarcaNombre " +
            "FROM ARTICULOS a " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            "WHERE a.ES = 1 ORDER BY a.D");
        var prices = await conn.QueryAsync<Price>(
            "SELECT p.IDART, p.IDLOCAL, p.S, p.PVENTA, p.CONTADO " +
            "FROM PRICES p WHERE p.DELETADO = 0");
        return (arts, prices);
    }

    public async Task<bool> ActualizarAsync(Articulo a)
    {
        using var conn = _factory.Create();
        // ACTUALIZAR_ARTICULO_CS: @CA,@SERIAL,@D,@PRES,@SMIN,@IDS,@IDPR,@IDC,@IDSBC,
        //   @IDPAIS,@IDM,@IDMED,@GRA,@IVA,@MAXCUOTA,@SLC,@VU,@VTO,@ID,@msg
        var p = new DynamicParameters();
        p.Add("@ID", a.Id);
        p.Add("@CA", a.Ca); p.Add("@SERIAL", a.Serial); p.Add("@D", a.D);
        p.Add("@PRES", a.Pres); p.Add("@SMIN", a.Smin);
        p.Add("@IDS", a.Ids); p.Add("@IDPR", a.Idpr); p.Add("@IDC", a.Idc);
        p.Add("@IDSBC", a.Idsbc); p.Add("@IDPAIS", a.Idpais);
        p.Add("@IDM", a.Idm); p.Add("@IDMED", a.Idmed);
        p.Add("@GRA", a.Gra); p.Add("@IVA", a.Iva);
        p.Add("@MAXCUOTA", a.Maxcuota); p.Add("@SLC", a.Slc); p.Add("@VU", a.Vu);
        p.Add("@VTO", a.Vto);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        await conn.ExecuteAsync("ACTUALIZAR_ARTICULO_CS", p, commandType: CommandType.StoredProcedure);
        return true;
    }
}
