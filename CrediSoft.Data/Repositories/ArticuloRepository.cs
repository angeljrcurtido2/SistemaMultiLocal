using CrediSoft.Core.Models;
using Dapper;
using System.Data;
using System.Linq;

namespace CrediSoft.Data.Repositories;

public interface IArticuloRepository
{
    Task<Articulo?> BuscarPorCodigoAsync(string codigo);
    Task<Articulo?> BuscarPorIdAsync(int id);
    Task<IEnumerable<Articulo>> BuscarAsync(string termino);
    Task<IEnumerable<Articulo>> BuscarTodosAsync();
    Task<Price?> ObtenerPrecioLocalAsync(int idArt, int idLocal);
    Task<IEnumerable<Price>> ObtenerStockTodosLocalesAsync(int idArt);
    // Lista TODOS los pares artículo/local actualmente inhabilitados (PRICES.DELETADO=1),
    // con artículo activo (ARTICULOS.ES=1) — mismo criterio usado en el diagnóstico manual que
    // encontró 31 casos reales de stock cargado pero invisible (TK886, LU-5004). Antes, el
    // único flujo para habilitar exigía saber de antemano el código exacto del artículo — sin
    // forma de "ver qué está inhabilitado", el usuario no tenía cómo enterarse de que existía
    // algo para corregir. Filtro opcional por texto (código o descripción) y por local.
    Task<IEnumerable<InhabilitadoRow>> ObtenerInhabilitadosAsync(string? filtroTexto = null, int idLocal = 0, int top = 300);
    // Desglose de stock por local para un LOTE de artículos (una sola query, no N+1) —
    // usado para las columnas "Stock por local" de la grilla de VerArticulosWindow,
    // limitado siempre a los artículos de la página visible, nunca al listado completo.
    Task<IEnumerable<StockPorLocalRow>> ObtenerStockPorLocalDeArticulosAsync(IEnumerable<int> idsArt);
    // idUsuario/nomMaquina: quedan registrados en AUDITORIA (antes/después por campo) para que
    // el historial de precios (ver ObtenerHistorialPreciosAsync) pueda mostrar quién y cuándo
    // cambió cada valor — pedido explícito: el usuario no tenía forma de confirmar que un
    // cambio de precio "pegó" salvo confiar a ciegas, sin ningún rastro visible.
    Task<bool> ActualizarPreciosAsync(int idArt, int idLocal, decimal pcosto, decimal pventa, decimal contado,
        decimal promo, int idUsuario, string nomMaquina);
    // Últimos cambios de precio de un artículo (todos los locales, o uno puntual si idLocal>0)
    // leídos de AUDITORIA — usado por el botón "Historial" del modal Modificar Precios.
    Task<IEnumerable<HistorialPrecioRow>> ObtenerHistorialPreciosAsync(int idArt, int idLocal = 0, int top = 50);
    Task<bool> ActualizarStockAsync(int idArt, int idLocal, decimal cantidad, string modo, int idUsuario);
    // idUsuario/nomMaquina: antes este método no dejaba ningún rastro en AUDITORIA (a
    // diferencia de ActualizarPreciosAsync, que sí audita cada cambio de precio) — un
    // artículo podía pasar de inhabilitado a habilitado (o viceversa) sin que quedara
    // registrado quién ni cuándo. Bug real encontrado al verificar una habilitación real.
    Task<bool> InhabilitarEnLocalAsync(int idArt, int idLocal, bool inhabilitar, int idUsuario, string nomMaquina);
    Task<int> GuardarAsync(Articulo articulo, int idLocal, int idUsuario);
    Task<bool> ActualizarAsync(Articulo articulo);
    Task<(IEnumerable<Articulo> arts, IEnumerable<Price> prices)> ObtenerVisorAsync();
    Task<IEnumerable<Price>> ObtenerStockPorLocalTodosAsync();
    Task<IEnumerable<Price>> ObtenerPreciosPorLocalAsync(int idLocal);

    // Paginación real para VerArticulosWindow. incluirInactivos=true solo lo usa el selector
    // simple de Artículos > Precios (ver VerArticulosWindow._modoSimple) — el resto de usos
    // (Ver Artículos normal, selector de Venta a Crédito) siguen mostrando solo activos.
    Task<(IEnumerable<VisorArticuloPag> filas, int total)> ObtenerVisorPaginadoAsync(
        string termino, string modo, string filtroStock, string filtroTipo,
        int pagina, int porPagina, int idLocal = 0, bool incluirInactivos = false);
    Task<IEnumerable<VisorArticuloPag>> ObtenerVisorPaginadoModalAsync(
        string termino, int idLocal, int pagina, int porPagina, string filtroStock = "con");

    // Buscador de artículos para Ventas al Contado: stock TOTAL (suma de los 15 locales)
    // en una sola query agregada, sin el patrón N+1 (una consulta por artículo) que tenía
    // el buscador anterior. idLocalPreferido, si se pasa, ademas trae el stock puntual de
    // ESE local por separado para poder resaltarlo/ordenarlo antes que el total.
    Task<IEnumerable<ArticuloBuscador>> BuscarParaVentaContadoAsync(string termino, int? idLocalPreferido = null);

    // Listado para el reporte "Ver Artículos" (agrupado por categoría o proveedor).
    // idLocal = 0 => stock TOTAL sumado de todos los locales; caso contrario, stock de ESE local.
    // termino: filtro libre por código o descripción (contiene), independiente del rango
    // alfabético Desde/Hasta — vacío/null no filtra.
    Task<IEnumerable<ArticuloListadoRow>> ObtenerListadoAgrupadoAsync(
        string agruparPor, string descDesde, string descHasta, int idLocal, string? termino = null);

    // Listado en grilla plana (ID, Código, Descripción, P.Costo, P.Venta, Stock, Parcial)
    // para la pantalla "Ver Artículos" — réplica del listado del sistema viejo.
    // idLocal = 0 => precios/stock agregados de todos los locales; caso contrario, del local puntual.
    // termino: filtro libre por código o descripción (contiene), independiente del rango
    // alfabético Desde/Hasta — vacío/null no filtra.
    Task<IEnumerable<ArticuloListadoDetalleRow>> ObtenerListadoDetalladoAsync(
        string agruparPor, string descDesde, string descHasta, int idLocal, string? termino = null);

    // Listado agrupado (categoría/proveedor) con el desglose de stock POR LOCAL de cada
    // artículo — solo tiene sentido con "todos los locales" (no recibe idLocal: siempre es
    // el equivalente al idLocal=0 de los otros métodos). Usado por el reporte "Stock por
    // Local" (matriz con una columna por cada local), distinto del listado con una sola
    // columna de stock total que ya usan los otros métodos.
    Task<IEnumerable<ArticuloListadoRow>> ObtenerListadoAgrupadoConStockPorLocalAsync(
        string agruparPor, string descDesde, string descHasta, string? termino = null);
}

// DTO para el reporte "Ver Artículos" — agrupado por categoría o proveedor
public class ArticuloListadoRow
{
    public int     Id      { get; set; }
    public string  Grupo   { get; set; } = string.Empty;
    public string  Codigo  { get; set; } = string.Empty;
    public string  D       { get; set; } = string.Empty;
    public decimal Stock   { get; set; }
    public decimal PCosto  { get; set; }
    public decimal PVenta  { get; set; }
    // Solo se completa en ObtenerListadoAgrupadoConStockPorLocalAsync (reporte "Stock por
    // Local") — desglose de stock de este artículo en cada uno de los locales, mismo patrón
    // que ArticuloListadoDetalleRow.StockPorLocal.
    public List<StockPorLocalRow> StockPorLocal { get; set; } = new();
}

// DTO para la grilla plana de "Ver Artículos" (con precios y parcial = PCosto * Stock)
public class ArticuloListadoDetalleRow
{
    public int     Id          { get; set; }
    public string  Codigo      { get; set; } = string.Empty;
    public string  Descripcion { get; set; } = string.Empty;
    public decimal PCosto      { get; set; }
    public decimal PVenta      { get; set; }
    // Máximo de cuotas del artículo (ARTICULOS.MAXCUOTA) — mismo dato en todos los locales
    // (no varía por PRICES), a diferencia de precios/stock.
    public int     MaxCuota    { get; set; }
    // Precio contado (PRICES.CONTADO) — columna propia y distinta de PVenta (crédito) y
    // PPromo (promoción). Confirmado contra el listado del sistema viejo: su columna
    // "CONTADO" coincide exactamente con PRICES.CONTADO crudo, no con PPROMO ni PVENTA.
    public decimal Contado     { get; set; }
    public decimal Stock       { get; set; }
    public decimal Parcial     { get; set; }
    // Precio promocional vigente ahora mismo con un LOCAL PUNTUAL elegido (0 si ese local no
    // tiene promo activa). Con "todos los locales" cada uno puede tener su propio precio
    // promocional distinto, así que ahí no se completa este campo — se usa Stocks[] en su
    // lugar (ver VerArticulosListadoWindow, columna por local) para no mezclar 14 locales en
    // un solo número ambiguo.
    public decimal PPromo      { get; set; }

    // Solo se completa cuando idLocal=0 ("todos los locales"): desglose de stock de este
    // artículo por local — mismo tipo/patrón que VisorArticuloPag.StockPorLocal, la UI lo
    // mapea a columnas usando el orden de locales que ya tiene cargado.
    public List<StockPorLocalRow> StockPorLocal { get; set; } = new();
}

// DTO para el buscador de artículos de Venta al Contado — stock total + del local preferido
public class ArticuloBuscador
{
    public int     Id              { get; set; }
    public string  Ca              { get; set; } = string.Empty;
    public string  D               { get; set; } = string.Empty;
    public string  MarcaNombre     { get; set; } = string.Empty;
    public decimal StockTotal      { get; set; }
    public decimal StockLocal      { get; set; }
    public decimal Pventa          { get; set; }
    public decimal Contado         { get; set; }
}

// Fila plana IdArt+IdLocal+Stock — el mapeo a posición de columna/array queda a cargo de la
// UI (VerArticulosWindow ya tiene el orden de _locales cargado), no de la capa de datos.
public class StockPorLocalRow
{
    public int  IdArt   { get; set; }
    public byte IdLocal { get; set; }
    public decimal S    { get; set; }
}

// Fila para el listado de "artículos inhabilitados por local" — ordenada priorizando Stock
// descendente (los casos con mercadería cargada pero invisible, el problema real detectado,
// suben primero) sobre los que están en 0 y probablemente deshabilitados a propósito.
public class InhabilitadoRow
{
    public int    IdArt      { get; set; }
    public string Ca         { get; set; } = "";
    public string D          { get; set; } = "";
    public int    IdLocal    { get; set; }
    public string LocalNombre { get; set; } = "";
    public decimal Stock     { get; set; }
}

// DTO ligero para paginación del visor
public class VisorArticuloPag
{
    public int     Id         { get; set; }
    public string  Ca         { get; set; } = string.Empty;
    public string  D          { get; set; } = string.Empty;
    public string  MarcaNombre { get; set; } = string.Empty;
    public int     Maxcuota   { get; set; }
    public byte    Slc        { get; set; }
    public byte    Es         { get; set; } = 1;   // 1 = activo, 0 = inactivo (ARTICULOS.ES)
    public decimal Pventa     { get; set; }
    public decimal Contado    { get; set; }
    public decimal StockTotal { get; set; }

    // Desglose de stock por local, traído en la misma llamada QueryMultipleAsync que la
    // página de artículos (ver AsignarStockPorLocal) — evita un segundo round-trip de red.
    public List<StockPorLocalRow> StockPorLocal { get; set; } = new();
}

// Fila de historial de cambios de precio, leída de AUDITORIA — ver ObtenerHistorialPreciosAsync.
// VALOR_ANTES/VALOR_DESPUES se guardan como "costo|venta|contado|promo" (4 decimales separados
// por '|', orden fijo) para poder separarlos de forma confiable acá — antes se guardaba como
// texto libre "C:... V:... Co:... P:..." pensado solo para lectura humana en una tabla plana;
// para armar tarjetas que resalten SOLO el campo que cambió hace falta el valor numérico de
// cada uno por separado, no un string a parsear con heurísticas.
public class HistorialPrecioRow
{
    public DateTime Fecha        { get; set; }
    public string   Usuario      { get; set; } = string.Empty;
    public string   IdRegistro   { get; set; } = string.Empty; // "{idArt}-{idLocal}"
    public string   ValorAntes   { get; set; } = string.Empty;
    public string   ValorDespues { get; set; } = string.Empty;
    public string   Maquina      { get; set; } = string.Empty;

    // Soporta también el formato viejo "C:1.234 V:5.678 Co:9.012 P:3.456" (texto libre, con
    // puntos de miles) — los primeros registros insertados antes de este cambio de formato
    // quedaron guardados así en AUDITORIA; sin este fallback, ObtenerHistorialPreciosAsync
    // los mostraría todos como "sin cambios" (parseo en 0) aunque el guardado real sí haya
    // funcionado, confundiendo al usuario justo con lo que este historial busca aclarar.
    private static decimal[] Partes(string v)
    {
        if (v.Contains('|'))
            return v.Split('|').Select(p => decimal.TryParse(p, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0).ToArray();

        decimal ExtraerViejo(string prefijo)
        {
            var idx = v.IndexOf(prefijo, StringComparison.Ordinal);
            if (idx < 0) return 0;
            var inicio = idx + prefijo.Length;
            var fin = v.IndexOf(' ', inicio + 1);
            var tramo = fin > inicio ? v[inicio..fin] : v[inicio..];
            return decimal.TryParse(tramo.Replace(".", ""), out var d) ? d : 0;
        }
        return new[] { ExtraerViejo("C:"), ExtraerViejo("V:"), ExtraerViejo("Co:"), ExtraerViejo("P:") };
    }

    public decimal CostoAntes    => Partes(ValorAntes).ElementAtOrDefault(0);
    public decimal VentaAntes    => Partes(ValorAntes).ElementAtOrDefault(1);
    public decimal ContadoAntes  => Partes(ValorAntes).ElementAtOrDefault(2);
    public decimal PromoAntes    => Partes(ValorAntes).ElementAtOrDefault(3);
    public decimal CostoDespues   => Partes(ValorDespues).ElementAtOrDefault(0);
    public decimal VentaDespues   => Partes(ValorDespues).ElementAtOrDefault(1);
    public decimal ContadoDespues => Partes(ValorDespues).ElementAtOrDefault(2);
    public decimal PromoDespues   => Partes(ValorDespues).ElementAtOrDefault(3);
}

// Extiende VisorArticuloPag para recibir COUNT(*) OVER() en una sola query
internal class VisorArticuloPagWithCount : VisorArticuloPag
{
    public int TotalCount { get; set; }
}

// Caché de stock en memoria — se comparte entre todas las instancias de la ventana
internal record StockEntry(decimal StockTotal, decimal Pventa, decimal Contado);

internal class StockCache
{
    private static readonly Dictionary<int, (Dictionary<int, StockEntry> data, DateTime ts)> _store = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);
    private static readonly object _lock = new();

    public static Dictionary<int, StockEntry>? Get(int idLocal)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(idLocal, out var entry) && DateTime.UtcNow - entry.ts < Ttl)
                return entry.data;
            return null;
        }
    }

    public static void Set(int idLocal, Dictionary<int, StockEntry> data)
    {
        lock (_lock) { _store[idLocal] = (data, DateTime.UtcNow); }
    }

    public static void Invalidate()
    {
        lock (_lock) { _store.Clear(); }
    }
}

// Caché del listado agrupado de "Ver Artículos" — la consulta es pesada (agrupa todos
// los artículos activos por categoría/proveedor con stock agregado) y varios usuarios
// suelen repetir los mismos criterios (ej. "Todos los locales" + "Categorías") en la
// misma franja horaria. TTL corto porque el stock cambia con cada venta/compra.
internal class ListadoAgrupadoCache
{
    private static readonly Dictionary<string, (List<ArticuloListadoRow> data, DateTime ts)> _store = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);
    private static readonly object _lock = new();

    public static string Clave(string agruparPor, string desde, string hasta, int idLocal, string? termino = null) =>
        $"{agruparPor}|{desde}|{hasta}|{idLocal}|{termino}";

    public static List<ArticuloListadoRow>? Get(string clave)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(clave, out var entry) && DateTime.UtcNow - entry.ts < Ttl)
                return entry.data;
            return null;
        }
    }

    public static void Set(string clave, List<ArticuloListadoRow> data)
    {
        lock (_lock) { _store[clave] = (data, DateTime.UtcNow); }
    }
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

    public async Task<bool> InhabilitarEnLocalAsync(int idArt, int idLocal, bool inhabilitar, int idUsuario, string nomMaquina)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var deletado = inhabilitar ? 1 : 0;
        var antes = await conn.QueryFirstOrDefaultAsync<byte?>(
            "SELECT DELETADO FROM PRICES WHERE IDART=@Art AND IDLOCAL=@Local", new { Art = idArt, Local = idLocal }, tx);

        var rows = await conn.ExecuteAsync(
            "UPDATE PRICES SET DELETADO=@D WHERE IDART=@Art AND IDLOCAL=@Local",
            new { Art = idArt, Local = idLocal, D = deletado }, tx);

        if (rows > 0)
        {
            await conn.ExecuteAsync(
                "INSERT INTO AUDITORIA (FECHA_HORA,ID_USUARIO,TABLA,ID_REGISTRO,OPERACION,CAMPO,VALOR_ANTES,VALOR_DESPUES,MODULO,NOM_MAQUINA,IP_MAQUINA) " +
                "VALUES (GETDATE(),@IdUsuario,'PRICES',@IdRegistro,'U','DELETADO',@ValorAntes,@ValorDespues,'INHABILITAR/HABILITAR ARTICULO',@NomMaquina,'127.0.0.1')",
                new {
                    IdUsuario = idUsuario, IdRegistro = $"{idArt}-{idLocal}",
                    ValorAntes = $"Deletado: {antes ?? 0}",
                    ValorDespues = $"Deletado: {deletado}",
                    NomMaquina = nomMaquina
                }, tx);
        }

        tx.Commit();
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
        // PPROMO faltaba en el SELECT — Dapper lo mapeaba a Price.Ppromo con su default (0)
        // porque la columna simplemente nunca llegaba en el resultset, sin importar el valor
        // real en la tabla. Bug real reportado: el modal de "Igualar precios" (Nueva Compra)
        // mostraba "Promo: 0" para artículos que sí tenían un precio de promoción cargado.
        return await conn.QueryAsync<Price>(
            "SELECT p.IDPRICES, p.IDLOCAL, p.S, p.PC, p.PVENTA, p.CONTADO, p.PPROMO, " +
            "l.NOMBRE as LocalNombre " +  // columna real: NOMBRE
            "FROM PRICES p " +
            "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
            "WHERE p.IDART = @Art AND p.DELETADO = 0 ORDER BY l.NOMBRE",
            new { Art = idArt });
    }

    public async Task<IEnumerable<InhabilitadoRow>> ObtenerInhabilitadosAsync(string? filtroTexto = null, int idLocal = 0, int top = 300)
    {
        using var conn = _factory.Create();
        var where = "WHERE p.DELETADO = 1 AND a.ES = 1";
        if (!string.IsNullOrWhiteSpace(filtroTexto)) where += " AND (a.CA LIKE @Texto OR a.D LIKE @Texto)";
        if (idLocal > 0) where += " AND p.IDLOCAL = @Local";

        return await conn.QueryAsync<InhabilitadoRow>(
            $"SELECT TOP (@Top) a.ID as IdArt, a.CA as Ca, a.D as D, p.IDLOCAL as IdLocal, l.NOMBRE as LocalNombre, p.S as Stock " +
            "FROM PRICES p " +
            "INNER JOIN ARTICULOS a ON p.IDART = a.ID " +
            "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
            $"{where} " +
            "ORDER BY p.S DESC, a.CA",
            new { Top = top, Texto = $"%{filtroTexto}%", Local = idLocal });
    }

    public async Task<IEnumerable<StockPorLocalRow>> ObtenerStockPorLocalDeArticulosAsync(IEnumerable<int> idsArt)
    {
        var ids = idsArt.Distinct().ToList();
        if (ids.Count == 0) return Enumerable.Empty<StockPorLocalRow>();
        using var conn = _factory.Create();

        // SQL Server corta en 2100 parámetros por statement — Dapper expande "IN @Ids" a un
        // parámetro por cada Id, así que con "Todos los locales" y sin filtro de descripción
        // (miles de artículos) un solo IN se pasaba de largo ("too many parameters"). Se
        // manda en lotes en vez de un único IN gigante.
        const int loteMax = 2000;
        var resultado = new List<StockPorLocalRow>();
        for (int i = 0; i < ids.Count; i += loteMax)
        {
            var lote = ids.Skip(i).Take(loteMax).ToList();
            var filas = await conn.QueryAsync<StockPorLocalRow>(
                "SELECT p.IDART AS IdArt, p.IDLOCAL AS IdLocal, p.S AS S " +
                "FROM PRICES p WHERE p.DELETADO = 0 AND p.IDART IN @Ids",
                new { Ids = lote }, commandTimeout: 30);
            resultado.AddRange(filas);
        }
        return resultado;
    }

    public async Task<bool> ActualizarPreciosAsync(
        int idArt, int idLocal, decimal pcosto, decimal pventa, decimal contado, decimal promo,
        int idUsuario, string nomMaquina)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Precios ANTES del update — necesarios para el detalle de AUDITORIA (antes → después).
        // Se lee SIN filtrar DELETADO: si el registro estaba dado de baja en este local, igual
        // hace falta su valor "antes" para el historial, y además para saber si hace falta
        // reactivarlo (ver más abajo).
        var antes = await conn.QueryFirstOrDefaultAsync<(decimal Pc, decimal Pv, decimal Co, decimal Pp, byte Deletado)>(
            "SELECT PC, PVENTA, CONTADO, PPROMO, DELETADO FROM PRICES WHERE IDART=@Art AND IDLOCAL=@Local",
            new { Art = idArt, Local = idLocal }, tx);

        // Guardar un precio de un artículo activo (ARTICULOS.ES=1) es una acción explícita del
        // usuario que da a entender que ese artículo debe volver a estar disponible en este
        // local — antes el UPDATE exigía DELETADO=0 en su WHERE, así que si el registro estaba
        // dado de baja (visto real: un artículo con DELETADO=1 en los 15 locales) el UPDATE no
        // tocaba NINGUNA fila y el guardado fallaba en silencio: el usuario veía "Precios
        // guardados" pero nada cambiaba en la base — exactamente la "percepción de que no se
        // actualiza" que motivó todo este historial. Se reactiva acá con el mismo criterio ya
        // usado en Compras/Transferencias cuando entra stock a un artículo inactivo.
        var rows = await conn.ExecuteAsync(
            "UPDATE PRICES SET PC=@Pc, PVENTA=@Pv, CONTADO=@Co, PPROMO=@Pp, FMP=GETDATE(), DELETADO=0 " +
            "WHERE IDART=@Art AND IDLOCAL=@Local",
            new { Art = idArt, Local = idLocal, Pc = pcosto, Pv = pventa, Co = contado, Pp = promo }, tx);

        if (rows > 0)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            await conn.ExecuteAsync(
                "INSERT INTO AUDITORIA (FECHA_HORA,ID_USUARIO,TABLA,ID_REGISTRO,OPERACION,CAMPO,VALOR_ANTES,VALOR_DESPUES,MODULO,NOM_MAQUINA,IP_MAQUINA) " +
                "VALUES (GETDATE(),@IdUsuario,'PRICES',@IdRegistro,'U','PRECIOS',@ValorAntes,@ValorDespues,'MODIFICAR PRECIOS',@NomMaquina,'127.0.0.1')",
                new {
                    IdUsuario = idUsuario, IdRegistro = $"{idArt}-{idLocal}",
                    ValorAntes   = $"{antes.Pc.ToString(inv)}|{antes.Pv.ToString(inv)}|{antes.Co.ToString(inv)}|{antes.Pp.ToString(inv)}",
                    ValorDespues = $"{pcosto.ToString(inv)}|{pventa.ToString(inv)}|{contado.ToString(inv)}|{promo.ToString(inv)}",
                    NomMaquina = nomMaquina
                }, tx);
        }

        tx.Commit();
        if (rows > 0) StockCache.Invalidate();
        return rows > 0;
    }

    public async Task<IEnumerable<HistorialPrecioRow>> ObtenerHistorialPreciosAsync(int idArt, int idLocal = 0, int top = 50)
    {
        using var conn = _factory.Create();
        var idRegistroFiltro = idLocal > 0 ? $"{idArt}-{idLocal}" : $"{idArt}-%";
        var op = idLocal > 0 ? "AND ID_REGISTRO = @IdRegistroFiltro " : "AND ID_REGISTRO LIKE @IdRegistroFiltro ";
        return await conn.QueryAsync<HistorialPrecioRow>(
            $"SELECT TOP {top} au.FECHA_HORA AS Fecha, ISNULL(u.NOMBRE_USUARIO,'—') AS Usuario, " +
            "au.ID_REGISTRO AS IdRegistro, au.VALOR_ANTES AS ValorAntes, au.VALOR_DESPUES AS ValorDespues, au.NOM_MAQUINA AS Maquina " +
            "FROM AUDITORIA au LEFT JOIN USUARIOS u ON u.ID_USUARIO = au.ID_USUARIO " +
            "WHERE au.TABLA = 'PRICES' AND au.MODULO = 'MODIFICAR PRECIOS' " + op +
            "ORDER BY au.FECHA_HORA DESC",
            new { IdRegistroFiltro = idRegistroFiltro });
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
        if (rows > 0) StockCache.Invalidate();
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
            "WHERE a.ES = 1 ORDER BY a.D",
            commandTimeout: 120);
        var prices = await conn.QueryAsync<Price>(
            "SELECT p.IDART, p.IDLOCAL, p.S, p.PVENTA, p.CONTADO " +
            "FROM PRICES p WHERE p.DELETADO = 0",
            commandTimeout: 120);
        return (arts, prices);
    }

    public async Task<IEnumerable<Price>> ObtenerStockPorLocalTodosAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Price>(
            "SELECT p.IDPRICES, p.IDART, p.IDLOCAL, p.S, p.PC, p.PVENTA, p.CONTADO, " +
            "l.NOMBRE as LocalNombre " +
            "FROM PRICES p " +
            "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
            "WHERE p.DELETADO = 0 ORDER BY p.IDART, l.NOMBRE",
            commandTimeout: 120);
    }

    public async Task<IEnumerable<Price>> ObtenerPreciosPorLocalAsync(int idLocal)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Price>(
            "SELECT p.IDPRICES, p.IDART, p.IDLOCAL, p.S, p.PC, p.PVENTA, p.CONTADO, p.PPROMO, " +
            "p.DELETADO, l.NOMBRE as LocalNombre " +
            "FROM PRICES p " +
            "INNER JOIN LOCALES l ON p.IDLOCAL = l.ID_LOCAL " +
            "WHERE p.IDLOCAL = @Local AND p.DELETADO = 0 ORDER BY p.IDART",
            new { Local = idLocal });
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

    public async Task<(IEnumerable<VisorArticuloPag> filas, int total)> ObtenerVisorPaginadoAsync(
        string termino, string modo, string filtroStock, string filtroTipo,
        int pagina, int porPagina, int idLocal = 0, bool incluirInactivos = false)
    {
        using var conn = _factory.Create();

        // Filtros de artículo (no dependen de PRICES)
        var whereArts = new System.Text.StringBuilder(incluirInactivos ? "WHERE 1 = 1 " : "WHERE a.ES = 1 ");
        if (!string.IsNullOrWhiteSpace(termino))
            whereArts.Append(modo switch
            {
                "descripcion" => "AND a.D LIKE @Term ",
                "marca"       => "AND m.MARCA LIKE @Term ",
                _             => "AND a.CA LIKE @Term ",
            });
        if (filtroTipo == "contado") whereArts.Append("AND a.SLC = 1 ");
        if (filtroTipo == "credito") whereArts.Append("AND a.SLC = 0 ");

        int offset = (pagina - 1) * porPagina;
        var prm = new { Term = $"%{termino}%", TermPrefijo = $"{termino}%" };

        // Restricción de local en PRICES (una sola vez, usada en CTE y en datos)
        string localFilter = idLocal > 0 ? $"AND p.IDLOCAL = {idLocal} " : "";

        // Orden: con término de búsqueda en código/descripción, los que EMPIEZAN con el
        // término van primero (CASE 0), el resto de las coincidencias LIKE '%termino%' después
        // (CASE 1) — sin esto, buscar "mesa" traía primero "BEBEDERO DE MESA..." antes que
        // "MESA..." porque ORDER BY a.D ordenaba alfabético plano sobre toda la descripción,
        // ignorando en qué posición aparecía la coincidencia (reportado real por el usuario).
        // Con modo "marca" no aplica: ahí se busca por m.MARCA, no por a.D/a.CA.
        string ordenCampo = modo switch { "descripcion" => "a.D", "marca" => "a.D", _ => "a.CA" };
        string orderBy = !string.IsNullOrWhiteSpace(termino) && modo != "marca"
            ? $"ORDER BY CASE WHEN {ordenCampo} LIKE @TermPrefijo THEN 0 ELSE 1 END, a.D"
            : "ORDER BY a.D";

        string sqlCount;
        string sqlData;

        if (filtroStock == "todos")
        {
            // Sin filtro de stock: COUNT directo sobre ARTICULOS (no toca PRICES)
            string needsMarca = (modo == "marca") ? "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " : "";
            sqlCount = $"SELECT COUNT(*) FROM ARTICULOS a {needsMarca}{whereArts}";

            // CTE nombrada (antes era subquery anónima "SELECT * FROM (...) __p") para poder
            // reutilizar la misma página de 50 IDs en un segundo SELECT de stock-por-local sin
            // repetir el filtro/paginación — ver comentario grande más abajo sobre por qué esto
            // reemplaza a CompletarStockPorLocalAsync.
            sqlData =
                "; WITH Pagina AS (" +
                "SELECT a.ID as Id, a.CA as Ca, a.D as D, ISNULL(m.MARCA,'') AS MarcaNombre, " +
                "a.MAXCUOTA as Maxcuota, a.SLC as Slc, a.ES as Es, " +
                "ISNULL((SELECT MAX(p2.PVENTA)  FROM PRICES p2 WHERE p2.IDART=a.ID " + localFilter + "AND p2.DELETADO=0), 0) AS Pventa, " +
                "ISNULL((SELECT MAX(p2.CONTADO) FROM PRICES p2 WHERE p2.IDART=a.ID " + localFilter + "AND p2.DELETADO=0), 0) AS Contado, " +
                "ISNULL((SELECT SUM(p2.S)       FROM PRICES p2 WHERE p2.IDART=a.ID " + localFilter + "AND p2.DELETADO=0), 0) AS StockTotal, " +
                $"ROW_NUMBER() OVER ({orderBy}) AS __rn " +
                "FROM ARTICULOS a " +
                "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
                whereArts +
                $") SELECT * FROM Pagina WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina}";

            string sqlStock =
                "; WITH Pagina AS (" +
                $"SELECT a.ID as Id, ROW_NUMBER() OVER ({orderBy}) AS __rn " +
                "FROM ARTICULOS a " + needsMarca + whereArts +
                ") SELECT p.IDART AS IdArt, p.IDLOCAL AS IdLocal, p.S AS S " +
                "FROM PRICES p WHERE p.DELETADO = 0 AND p.IDART IN (" +
                $"SELECT Id FROM Pagina WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina})";

            using var multi2 = await conn.QueryMultipleAsync(sqlCount + sqlData + sqlStock, prm, commandTimeout: 60);
            int totalCount2 = await multi2.ReadFirstAsync<int>();
            var filas2 = (await multi2.ReadAsync<VisorArticuloPag>()).ToList();
            var stock2 = (await multi2.ReadAsync<StockPorLocalRow>()).ToList();
            AsignarStockPorLocal(filas2, stock2);
            return (filas2, totalCount2);
        }

        // ── Con filtro de stock: resuelto íntegramente en SQL con paginación real ──
        // Antes esta rama traía el catálogo COMPLETO de ARTICULOS activos a memoria de la
        // app (sqlArts sin TOP/paginación) para recién ahí filtrar por stock y paginar en
        // LINQ — con miles de artículos, esto se sentía como una demora perceptible en cada
        // apertura de "Ver Artículos" bajo el filtro por defecto ("Con stock"). Reescrito
        // 2026-08-04 para que el filtro de stock se resuelva en el propio SQL dentro de la
        // misma CTE paginada (mismo patrón que la rama "todos" de arriba), así SQL Server
        // solo materializa las porPagina filas de la página pedida, no el catálogo completo.
        //
        // OUTER APPLY (no CROSS) porque un artículo sin NINGUNA fila en PRICES para el local
        // filtrado debe seguir contando como "sin stock" (StockTotal tratado como 0, igual
        // que el comportamiento anterior en memoria vía Dictionary.TryGetValue) — con CROSS
        // APPLY + GROUP BY, un artículo sin filas en PRICES no produce ninguna fila y
        // desaparecería por completo del filtro "sin stock" en vez de aparecer con stock 0.
        bool soloConStock = filtroStock == "con";
        var condStock = soloConStock ? "ISNULL(sp.StockTotal, 0) > 0" : "ISNULL(sp.StockTotal, 0) <= 0";
        string needsMarcaStock = (modo == "marca") ? "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " : "";
        string applyStock =
            "OUTER APPLY (SELECT SUM(p.S) AS StockTotal, MAX(p.PVENTA) AS Pventa, MAX(p.CONTADO) AS Contado " +
            "             FROM PRICES p WHERE p.IDART = a.ID AND p.DELETADO = 0 " + localFilter + ") sp ";

        sqlCount =
            "SELECT COUNT(*) FROM ARTICULOS a " + needsMarcaStock + applyStock + whereArts +
            $"AND {condStock} ";

        sqlData =
            "; WITH Pagina AS (" +
            "SELECT a.ID as Id, a.CA as Ca, a.D as D, ISNULL(m.MARCA,'') AS MarcaNombre, " +
            "a.MAXCUOTA as Maxcuota, a.SLC as Slc, a.ES as Es, " +
            "ISNULL(sp.Pventa, 0) AS Pventa, ISNULL(sp.Contado, 0) AS Contado, " +
            "ISNULL(sp.StockTotal, 0) AS StockTotal, " +
            $"ROW_NUMBER() OVER ({orderBy}) AS __rn " +
            "FROM ARTICULOS a " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            applyStock +
            whereArts +
            $"AND {condStock} " +
            $") SELECT * FROM Pagina WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina}";

        // Mismo desglose de stock-por-local que la rama "todos" — ver comentario grande abajo
        // (AsignarStockPorLocal) sobre por qué esto reemplaza al round-trip separado que hacía
        // CompletarStockPorLocalAsync desde la ventana.
        string sqlStock3 =
            "; WITH Pagina AS (" +
            $"SELECT a.ID as Id, ROW_NUMBER() OVER ({orderBy}) AS __rn " +
            "FROM ARTICULOS a " + needsMarcaStock + applyStock + whereArts + $"AND {condStock} " +
            ") SELECT p.IDART AS IdArt, p.IDLOCAL AS IdLocal, p.S AS S " +
            "FROM PRICES p WHERE p.DELETADO = 0 AND p.IDART IN (" +
            $"SELECT Id FROM Pagina WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina})";

        using var multi3 = await conn.QueryMultipleAsync(sqlCount + sqlData + sqlStock3, prm, commandTimeout: 60);
        int totalCount3 = await multi3.ReadFirstAsync<int>();
        var filas3 = (await multi3.ReadAsync<VisorArticuloPag>()).ToList();
        var stock3 = (await multi3.ReadAsync<StockPorLocalRow>()).ToList();
        AsignarStockPorLocal(filas3, stock3);
        return (filas3, totalCount3);
    }

    // Empaqueta el desglose de stock-por-local (traído en la MISMA llamada QueryMultipleAsync
    // que la página de artículos, vía CTE reutilizada) dentro de VisorArticuloPag.StockPorLocal
    // — antes esto vivía en un segundo round-trip de red separado (CompletarStockPorLocalAsync,
    // disparado desde VerArticulosWindow después de recibir la página). Con latencia de red
    // alta (sucursales con internet malo), dos viajes secuenciales duplicaban el tiempo de
    // espera aunque cada query individual tardara ~20ms en el servidor — confirmado real con
    // SET STATISTICS TIME. Unificar en una sola llamada corta ese tiempo a la mitad.
    private static void AsignarStockPorLocal(List<VisorArticuloPag> filas, List<StockPorLocalRow> stock)
    {
        if (filas.Count == 0) return;
        var porArt = stock.GroupBy(r => r.IdArt).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var f in filas)
            f.StockPorLocal = porArt.TryGetValue(f.Id, out var lst) ? lst : new List<StockPorLocalRow>();
    }

    public async Task<IEnumerable<VisorArticuloPag>> ObtenerVisorPaginadoModalAsync(
        string termino, int idLocal, int pagina, int porPagina, string filtroStock = "con")
    {
        using var conn = _factory.Create();

        // Reescrito 2026-08-04 — mismo motivo y mismo patrón que ObtenerVisorPaginadoAsync:
        // esto traía el catálogo COMPLETO de ARTICULOS activos a memoria (sin paginar en SQL)
        // para recién ahí filtrar por stock y paginar con LINQ. Con ~12.000 artículos, cada
        // apertura del selector desde Venta a Crédito pagaba ese costo completo para mostrar
        // 50 filas. Resuelto con la misma paginación real vía ROW_NUMBER() + OUTER APPLY
        // sobre PRICES filtrado a ESTE local — sigue sin usar StockCache (ver motivo abajo).
        //
        // StockCache NO se usa acá — es compartido estáticamente con ObtenerVisorPaginadoAsync
        // (que cachea agregados de TODOS los locales bajo otras claves/criterios); con TTL de
        // 3 min, un artículo sin stock en ESTE local específico podía seguir apareciendo en el
        // selector si el caché tenía una entrada contaminada de otro contexto. Se consulta
        // PRICES en vivo, sin caché, para garantizar que "Con stock" refleje el stock real del
        // local filtrado.
        var whereArts = new System.Text.StringBuilder("WHERE a.ES = 1 ");
        if (!string.IsNullOrWhiteSpace(termino))
            whereArts.Append("AND (a.CA LIKE @Term OR a.D LIKE @Term) ");

        int offset = (pagina - 1) * porPagina;
        var prm = new { Term = $"%{termino}%", Local = idLocal };

        var condStock = filtroStock switch
        {
            "con" => "ISNULL(sp.StockTotal, 0) > 0 ",
            "sin" => "ISNULL(sp.StockTotal, 0) <= 0 ",
            _     => null, // "todos" — sin condición de stock
        };

        var applyStock =
            "OUTER APPLY (SELECT SUM(p.S) AS StockTotal, MAX(p.PVENTA) AS Pventa, MAX(p.CONTADO) AS Contado " +
            "             FROM PRICES p WHERE p.IDART = a.ID AND p.DELETADO = 0 AND p.IDLOCAL = @Local) sp ";

        var sqlData =
            "SELECT * FROM (" +
            "SELECT a.ID as Id, a.CA as Ca, a.D as D, ISNULL(m.MARCA,'') AS MarcaNombre, " +
            "a.MAXCUOTA as Maxcuota, a.SLC as Slc, " +
            "ISNULL(sp.Pventa, 0) AS Pventa, ISNULL(sp.Contado, 0) AS Contado, " +
            "ISNULL(sp.StockTotal, 0) AS StockTotal, " +
            "ROW_NUMBER() OVER (ORDER BY a.D) AS __rn " +
            "FROM ARTICULOS a " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            applyStock +
            whereArts +
            (condStock != null ? $"AND {condStock}" : "") +
            $") __p WHERE __rn BETWEEN {offset + 1} AND {offset + porPagina}";

        return await conn.QueryAsync<VisorArticuloPag>(sqlData, prm, commandTimeout: 30);
    }

    public async Task<IEnumerable<ArticuloBuscador>> BuscarParaVentaContadoAsync(string termino, int? idLocalPreferido = null)
    {
        using var conn = _factory.Create();

        // Una sola query agregada (SUM por artículo, sin importar cuantos de los 15 locales
        // tengan precio cargado) — evita el patrón N+1 del buscador anterior, que hacia una
        // consulta de precio/stock POR CADA articulo devuelto (hasta 300 round-trips).
        var stockRows = await conn.QueryAsync<(int IdArt, decimal StockTotal, decimal Pventa, decimal Contado)>(
            "SELECT p.IDART, SUM(p.S) AS StockTotal, MAX(p.PVENTA) AS Pventa, MAX(p.CONTADO) AS Contado " +
            "FROM PRICES p WHERE p.DELETADO = 0 GROUP BY p.IDART",
            commandTimeout: 30);
        var stockMap = stockRows.ToDictionary(r => r.IdArt, r => r);

        Dictionary<int, decimal>? stockLocalMap = null;
        // Precio contado resuelto del local preferido, con promoción aplicada cuando corresponde:
        // PR=1 y la fecha de hoy cae dentro de INICIO/FIN. Sin esto, "Precio contado-contado"
        // en Venta al Contado siempre mostraba PRICES.CONTADO fijo (además agregado con MAX
        // entre TODOS los locales, ni siquiera el del local real de la venta), ignorando
        // cualquier promoción vigente — bug real reportado: mostraba 1.450.000 cuando el
        // artículo tenía una promo activa a 1.399.000 (PPROMO) para ese local puntual.
        Dictionary<int, decimal>? contadoConPromoLocalMap = null;
        if (idLocalPreferido is int local && local > 0)
        {
            var localRows = await conn.QueryAsync<(int IdArt, decimal S, decimal Contado, decimal? PPromo, byte Pr, DateTime? Inicio, DateTime? Fin)>(
                "SELECT IDART, S, CONTADO, PPROMO, PR, INICIO, FIN FROM PRICES WHERE DELETADO = 0 AND IDLOCAL = @Local",
                new { Local = local });
            var localList = localRows.ToList();
            stockLocalMap = localList.ToDictionary(r => r.IdArt, r => r.S);
            var hoy = DateTime.Today;
            contadoConPromoLocalMap = localList.ToDictionary(
                r => r.IdArt,
                r => r.Pr == 1 && r.PPromo.HasValue
                     && (!r.Inicio.HasValue || r.Inicio.Value.Date <= hoy)
                     && (!r.Fin.HasValue || hoy <= r.Fin.Value.Date)
                    ? r.PPromo.Value
                    : r.Contado);
        }

        var where = string.IsNullOrWhiteSpace(termino)
            ? "WHERE a.ES = 1 "
            : "WHERE a.ES = 1 AND (a.CA LIKE @Term OR a.D LIKE @Term) ";

        var sqlArts =
            "SELECT TOP 500 a.ID as Id, a.CA as Ca, a.D as D, ISNULL(m.MARCA,'') as MarcaNombre " +
            "FROM ARTICULOS a " +
            "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
            where + "ORDER BY a.D";

        var arts = await conn.QueryAsync<ArticuloBuscador>(sqlArts,
            new { Term = $"%{termino}%" }, commandTimeout: 30);

        return arts.Select(a =>
        {
            if (stockMap.TryGetValue(a.Id, out var s))
            {
                a.StockTotal = s.StockTotal;
                a.Pventa     = s.Pventa;
                a.Contado    = s.Contado;
            }
            if (stockLocalMap != null && stockLocalMap.TryGetValue(a.Id, out var sl))
                a.StockLocal = sl;
            if (contadoConPromoLocalMap != null && contadoConPromoLocalMap.TryGetValue(a.Id, out var contadoLocal))
                a.Contado = contadoLocal;
            return a;
        }).ToList();
    }

    public async Task<IEnumerable<ArticuloListadoRow>> ObtenerListadoAgrupadoAsync(
        string agruparPor, string descDesde, string descHasta, int idLocal, string? termino = null)
    {
        var clave = ListadoAgrupadoCache.Clave(agruparPor, descDesde, descHasta, idLocal, termino);
        var cacheado = ListadoAgrupadoCache.Get(clave);
        if (cacheado != null) return cacheado;

        using var conn = _factory.Create();

        string grupoExpr = agruparPor == "proveedores"
            ? "ISNULL(pr.NOMBRE_PROVEEDOR, '(Sin proveedor)')"
            : "ISNULL(c.CATEGORIA, '(Sin categoría)')";

        string joinGrupo = agruparPor == "proveedores"
            ? "LEFT JOIN PROVEEDORES pr ON a.IDPR = pr.ID_PROVEEDOR"
            : "LEFT JOIN CATEGORIAS c ON a.IDC = c.ID_CAT";

        // Rango alfabético opcional sobre la descripción (ambos límites son inclusive), más
        // el buscador libre por código o descripción (independiente del rango).
        var where = new System.Text.StringBuilder("WHERE a.ES = 1 ");
        if (!string.IsNullOrWhiteSpace(descDesde)) where.Append("AND a.D >= @Desde ");
        if (!string.IsNullOrWhiteSpace(descHasta)) where.Append("AND a.D <= @Hasta ");
        if (!string.IsNullOrWhiteSpace(termino)) where.Append("AND (a.D LIKE @Termino OR a.CA LIKE @Termino) ");

        // Antes esto era una subconsulta SUM(...) correlacionada por artículo (una
        // ejecución contra PRICES por cada fila de ARTICULOS — con ~12.000 artículos son
        // ~12.000 sub-queries). Se reemplaza por un único JOIN a un stock ya agregado por
        // GROUP BY una sola vez, mismo patrón que ArticuloRepository.BuscarParaVentaContadoAsync.
        string joinStock = idLocal > 0
            ? "LEFT JOIN (SELECT IDART, SUM(S) AS Total FROM PRICES WHERE IDLOCAL = @IdLocal AND DELETADO = 0 GROUP BY IDART) st ON st.IDART = a.ID"
            : "LEFT JOIN (SELECT IDART, SUM(S) AS Total FROM PRICES WHERE DELETADO = 0 GROUP BY IDART) st ON st.IDART = a.ID";

        // Mismo criterio que ObtenerListadoDetalladoAsync: con un local puntual, PCosto/PVenta
        // son el precio de ESE local; con "todos los locales", se toma el mayor entre locales
        // como referencia (los precios no suelen variar entre locales).
        string localFilterPrecio = idLocal > 0 ? "AND p2.IDLOCAL = @IdLocal " : "";

        var sql =
            $"SELECT {grupoExpr} AS Grupo, a.CA AS Codigo, a.D AS D, ISNULL(st.Total, 0) AS Stock, " +
            "ISNULL((SELECT MAX(p2.PC)     FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilterPrecio + "AND p2.DELETADO = 0), 0) AS PCosto, " +
            "ISNULL((SELECT MAX(p2.PVENTA) FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilterPrecio + "AND p2.DELETADO = 0), 0) AS PVenta " +
            "FROM ARTICULOS a " +
            $"{joinGrupo} " +
            $"{joinStock} " +
            $"{where}" +
            "ORDER BY Grupo, a.D";

        var resultado = (await conn.QueryAsync<ArticuloListadoRow>(sql,
            new { Desde = descDesde, Hasta = descHasta, IdLocal = idLocal, Termino = $"%{termino}%" }, commandTimeout: 60)).ToList();

        ListadoAgrupadoCache.Set(clave, resultado);
        return resultado;
    }

    // Reporte "Stock por Local": mismo agrupado por categoría/proveedor que
    // ObtenerListadoAgrupadoAsync, pero siempre a nivel "todos los locales" (no recibe
    // idLocal) y con el desglose de stock POR LOCAL de cada artículo — para armar la matriz
    // con una columna por cada local en vez de una sola columna de stock total.
    public async Task<IEnumerable<ArticuloListadoRow>> ObtenerListadoAgrupadoConStockPorLocalAsync(
        string agruparPor, string descDesde, string descHasta, string? termino = null)
    {
        using var conn = _factory.Create();

        string grupoExpr = agruparPor == "proveedores"
            ? "ISNULL(pr.NOMBRE_PROVEEDOR, '(Sin proveedor)')"
            : "ISNULL(c.CATEGORIA, '(Sin categoría)')";

        string joinGrupo = agruparPor == "proveedores"
            ? "LEFT JOIN PROVEEDORES pr ON a.IDPR = pr.ID_PROVEEDOR"
            : "LEFT JOIN CATEGORIAS c ON a.IDC = c.ID_CAT";

        var where = new System.Text.StringBuilder("WHERE a.ES = 1 ");
        if (!string.IsNullOrWhiteSpace(descDesde)) where.Append("AND a.D >= @Desde ");
        if (!string.IsNullOrWhiteSpace(descHasta)) where.Append("AND a.D <= @Hasta ");
        if (!string.IsNullOrWhiteSpace(termino)) where.Append("AND (a.D LIKE @Termino OR a.CA LIKE @Termino) ");

        string joinStock =
            "LEFT JOIN (SELECT IDART, SUM(S) AS Total FROM PRICES WHERE DELETADO = 0 GROUP BY IDART) st ON st.IDART = a.ID";

        var sql =
            $"SELECT a.ID AS Id, {grupoExpr} AS Grupo, a.CA AS Codigo, a.D AS D, ISNULL(st.Total, 0) AS Stock, " +
            "ISNULL((SELECT MAX(p2.PC)     FROM PRICES p2 WHERE p2.IDART = a.ID AND p2.DELETADO = 0), 0) AS PCosto, " +
            "ISNULL((SELECT MAX(p2.PVENTA) FROM PRICES p2 WHERE p2.IDART = a.ID AND p2.DELETADO = 0), 0) AS PVenta " +
            "FROM ARTICULOS a " +
            $"{joinGrupo} " +
            $"{joinStock} " +
            $"{where}" +
            "ORDER BY Grupo, a.D";

        var filas = (await conn.QueryAsync<ArticuloListadoRow>(sql,
            new { Desde = descDesde, Hasta = descHasta, Termino = $"%{termino}%" }, commandTimeout: 60)).ToList();

        if (filas.Count > 0)
        {
            var stockPorLocal = (await ObtenerStockPorLocalDeArticulosAsync(filas.Select(f => f.Id))).ToList();
            var porArt = stockPorLocal.GroupBy(r => r.IdArt).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var f in filas)
                f.StockPorLocal = porArt.TryGetValue(f.Id, out var lst) ? lst : new List<StockPorLocalRow>();
        }
        return filas;
    }

    public async Task<IEnumerable<ArticuloListadoDetalleRow>> ObtenerListadoDetalladoAsync(
        string agruparPor, string descDesde, string descHasta, int idLocal, string? termino = null)
    {
        using var conn = _factory.Create();

        var where = new System.Text.StringBuilder("WHERE a.ES = 1 ");
        if (!string.IsNullOrWhiteSpace(descDesde)) where.Append("AND a.D >= @Desde ");
        if (!string.IsNullOrWhiteSpace(descHasta)) where.Append("AND a.D <= @Hasta ");
        if (!string.IsNullOrWhiteSpace(termino)) where.Append("AND (a.D LIKE @Termino OR a.CA LIKE @Termino) ");

        // Mismo criterio que ObtenerVisorPaginadoAsync: con un local puntual, PC/PVENTA son
        // el precio de ESE local (MAX porque debería ser una sola fila); con "todos los
        // locales", se suma el stock y se toma el precio más alto entre locales como
        // referencia (los precios no suelen variar entre locales, pero evita NULL si algún
        // local no tiene fila en PRICES para ese artículo).
        string localFilter = idLocal > 0 ? "AND p2.IDLOCAL = @IdLocal " : "";

        var sql =
            "SELECT a.ID AS Id, a.CA AS Codigo, a.D AS Descripcion, a.MAXCUOTA AS MaxCuota, " +
            "ISNULL((SELECT MAX(p2.PC)      FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilter + "AND p2.DELETADO = 0), 0) AS PCosto, " +
            "ISNULL((SELECT MAX(p2.PVENTA)  FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilter + "AND p2.DELETADO = 0), 0) AS PVenta, " +
            "ISNULL((SELECT MAX(p2.CONTADO) FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilter + "AND p2.DELETADO = 0), 0) AS Contado, " +
            "ISNULL((SELECT SUM(p2.S)       FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilter + "AND p2.DELETADO = 0), 0) AS Stock, " +
            "ISNULL((SELECT MAX(p2.PPROMO) FROM PRICES p2 WHERE p2.IDART = a.ID " + localFilter + "AND p2.DELETADO = 0 " +
            "        AND p2.PPROMO > 0 AND p2.INICIO IS NOT NULL AND p2.FIN IS NOT NULL " +
            "        AND GETDATE() BETWEEN p2.INICIO AND p2.FIN), 0) AS PPromo " +
            "FROM ARTICULOS a " +
            $"{where}" +
            "ORDER BY a.D ASC";

        var filas = (await conn.QueryAsync<ArticuloListadoDetalleRow>(sql,
            new { Desde = descDesde, Hasta = descHasta, IdLocal = idLocal, Termino = $"%{termino}%" }, commandTimeout: 60)).ToList();

        foreach (var f in filas) f.Parcial = f.PCosto * f.Stock;

        // "Todos los locales": un artículo puede tener stock y precio promocional distintos
        // en cada uno de los 14 locales — sumarlos o promediarlos en una sola celda sería
        // engañoso, así que acá se trae el desglose real por local para que la UI arme una
        // columna de stock por cada local (mismo patrón que VisorArticuloPag.StockPorLocal).
        if (idLocal == 0 && filas.Count > 0)
        {
            var stockPorLocal = (await ObtenerStockPorLocalDeArticulosAsync(filas.Select(f => f.Id))).ToList();
            var porArt = stockPorLocal.GroupBy(r => r.IdArt).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var f in filas)
                f.StockPorLocal = porArt.TryGetValue(f.Id, out var lst) ? lst : new List<StockPorLocalRow>();
        }
        return filas;
    }
}
