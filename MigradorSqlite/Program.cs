using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

var destino = args.Length > 0 ? args[0] : @"..\CrediSoft.UI\CREDISOFT.db";
var sqlConn  = "Server=.\\SQLEXPRESS;Database=CREDISOFT;Integrated Security=True;TrustServerCertificate=True";

if (File.Exists(destino)) File.Delete(destino);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("MIGRACION SQL SERVER -> SQLITE");
Console.WriteLine($"Destino: {Path.GetFullPath(destino)}");
Console.ResetColor();
Console.WriteLine();

static string MapTipo(string t) => t.ToLower() switch {
    var x when x.StartsWith("int")     => "INTEGER",
    "bigint"                            => "INTEGER",
    "smallint"                          => "INTEGER",
    "tinyint"                           => "INTEGER",
    "bit"                               => "INTEGER",
    var x when x.StartsWith("decimal") => "REAL",
    var x when x.StartsWith("numeric") => "REAL",
    var x when x.StartsWith("float")   => "REAL",
    var x when x.StartsWith("money")   => "REAL",
    _                                   => "TEXT"
};

var tablas = new[] {
    "ARTICULOS","AUDITORIA","BANCOS","CAB_BUY_TMP","CAB_BUYS","CAB_CAJA",
    "CAB_REMITO_ACEPTADO","CAB_REMITO_TMP","CAB_SOL_SALES","CABECERA_SALES",
    "CAJA","CAJA_DETALLE","CAJA_MASTER","CATEGORIAS","CLIENTES","CONFIGURACION",
    "CONTADORES","DET_BUY_TMP","DET_BUYS","DET_CAJA","DET_REMITO_ACEPTADO",
    "DET_REMITO_TMP","DET_SOL_SALES","DETALLE_PAGO","DETALLES_NOTA_CREDITO",
    "DETALLES_SALES","DOCUMENTOS","EMPRESA","FOTOS","GARANTES","GENERADAS",
    "GENERAR_PAGOSALARIO","HISTORIAL_COBRO_CUOTAS","HISTORIAL_ENTREGAS_GENERADAS",
    "HISTORIAL_NOTA_CREDITO","HISTORIAL_PAGOS","HISTORIALPAGOFUN","IMPRESORAS",
    "LOCALES","MARCAS","MEDIDAS","MOV_FUNCIONARIOS","MOVART","NumeracionTickets",
    "PAISES","PRICES","PROVEEDORES","REFERENCIAS","RETIRO_LIBRE","SECCIONES",
    "SUBCATEGORIAS","USUARIOS"
};

using var lite = new SqliteConnection($"Data Source={destino}");
lite.Open();
using (var pragma = lite.CreateCommand()) {
    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF; PRAGMA foreign_keys=OFF; PRAGMA cache_size=-65536;";
    pragma.ExecuteNonQuery();
}

int tablaNum = 0;
int totalFilas = 0;
var errores = new List<string>();

for (int i = 0; i < tablas.Length; i++) {
    var tabla = tablas[i];
    tablaNum++;
    Console.Write($"[{tablaNum}/{tablas.Length}] {tabla,-40}");

    try {
        using var sql = new SqlConnection(sqlConn);
        sql.Open();

        // Obtener columnas con tipo e identidad
        var cols = new List<(string Name, string SqlType, bool IsIdentity, bool Nullable)>();
        using (var cmd = sql.CreateCommand()) {
            cmd.CommandText = $@"
                SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                       ISNULL(COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, 'IsIdentity'), 0) AS IS_ID
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @t
                ORDER BY c.ORDINAL_POSITION";
            cmd.Parameters.AddWithValue("@t", tabla);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                cols.Add((rd.GetString(0), rd.GetString(1), rd.GetInt32(3) == 1, rd.GetString(2) == "YES"));
        }

        if (cols.Count == 0) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  (sin columnas, omitida)");
            Console.ResetColor();
            continue;
        }

        // Crear tabla SQLite — sin PRIMARY KEY AUTOINCREMENT si la tabla la define externamente
        var defs = cols.Select(c => {
            var tipo = MapTipo(c.SqlType);
            var pk   = c.IsIdentity ? " PRIMARY KEY AUTOINCREMENT" : "";
            var nn   = (!c.Nullable && !c.IsIdentity) ? " NOT NULL" : "";
            return $"[{c.Name}] {tipo}{pk}{nn}";
        });
        using (var cmd = lite.CreateCommand()) {
            cmd.CommandText = $"DROP TABLE IF EXISTS [{tabla}];\nCREATE TABLE [{tabla}] ({string.Join(", ", defs)});";
            cmd.ExecuteNonQuery();
        }

        // Contar filas para info
        int filas = 0;
        using (var cntCmd = sql.CreateCommand()) {
            cntCmd.CommandText = $"SELECT COUNT(*) FROM [{tabla}]";
            cntCmd.CommandTimeout = 120;
            filas = (int)cntCmd.ExecuteScalar()!;
        }

        if (filas == 0) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  0 filas (vacía)");
            Console.ResetColor();
            continue;
        }

        // Columnas excluyendo identity para INSERT
        var insertCols  = cols.Where(c => !c.IsIdentity).ToList();
        var colNames    = string.Join(", ", insertCols.Select(c => $"[{c.Name}]"));
        var colParams   = string.Join(", ", insertCols.Select(c => $"@p{c.Name}"));
        var allColNames = string.Join(", ", cols.Select(c => $"[{c.Name}]"));
        var allParams   = string.Join(", ", cols.Select(c => $"@p{c.Name}"));

        // Decidir si hay identity column para usar INSERT con todos los campos
        bool tieneIdentity = cols.Any(c => c.IsIdentity);
        string insertSql;
        List<(string Name, string SqlType)> paramCols;
        if (tieneIdentity) {
            // Deshabilitar autoincrement insertando valor explícito
            using var noId = lite.CreateCommand();
            noId.CommandText = $"DROP TABLE IF EXISTS [{tabla}]; CREATE TABLE [{tabla}] ({string.Join(", ", defs.Select(d => d.Replace(" PRIMARY KEY AUTOINCREMENT", " PRIMARY KEY")))});";
            noId.ExecuteNonQuery();
            insertSql = $"INSERT OR REPLACE INTO [{tabla}] ({allColNames}) VALUES ({allParams})";
            paramCols = cols.Select(c => (c.Name, c.SqlType)).ToList();
        } else {
            insertSql = $"INSERT OR REPLACE INTO [{tabla}] ({colNames}) VALUES ({colParams})";
            paramCols = insertCols.Select(c => (c.Name, c.SqlType)).ToList();
        }

        // Lectura streaming desde SQL Server
        using var dataCmd = sql.CreateCommand();
        dataCmd.CommandText   = tieneIdentity
            ? $"SELECT {allColNames} FROM [{tabla}]"
            : $"SELECT {colNames} FROM [{tabla}]";
        dataCmd.CommandTimeout = 600;
        using var reader = dataCmd.ExecuteReader();

        using var tx  = lite.BeginTransaction();
        using var ins = lite.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = insertSql;
        foreach (var c in paramCols)
            ins.Parameters.Add($"@p{c.Name}", SqliteType.Text);

        int insertadas = 0;
        while (reader.Read()) {
            for (int j = 0; j < paramCols.Count; j++) {
                var val = reader.GetValue(j);
                ins.Parameters[$"@p{paramCols[j].Name}"].Value =
                    val is DBNull ? DBNull.Value : (object)val.ToString()!;
            }
            ins.ExecuteNonQuery();
            insertadas++;

            if (insertadas % 10000 == 0) {
                Console.Write($"\r[{tablaNum}/{tablas.Length}] {tabla,-40} {insertadas}/{filas}...");
            }
        }
        tx.Commit();
        totalFilas += insertadas;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\r[{tablaNum}/{tablas.Length}] {tabla,-40} {insertadas,8} filas  OK");
        Console.ResetColor();

    } catch (Exception ex) {
        errores.Add($"{tabla}: {ex.Message}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ERROR: {ex.Message}");
        Console.ResetColor();
    }
}

lite.Close();

var mb = new FileInfo(destino).Length / 1024.0 / 1024.0;
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"COMPLETADO — {Path.GetFullPath(destino)}");
Console.WriteLine($"Total filas migradas: {totalFilas:N0}");
Console.WriteLine($"Tamaño archivo:       {mb:F1} MB");
Console.ResetColor();

if (errores.Count > 0) {
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\nTablas con error ({errores.Count}):");
    foreach (var e in errores) Console.WriteLine($"  - {e}");
    Console.ResetColor();
}
