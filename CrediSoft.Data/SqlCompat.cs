using System.Text.RegularExpressions;

namespace CrediSoft.Data;

/// <summary>
/// Convierte sintaxis SQL Server → SQLite en tiempo de ejecución.
/// Solo aplica cuando esSqlite=true; SQL Server paths no se modifican.
/// </summary>
public static class SqlCompat
{
    private static readonly Regex _hint      = new(@"\s+WITH\s*\([^)]*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _isNull    = new(@"\bISNULL\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _getDate   = new(@"\bGETDATE\s*\(\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _convert   = new(@"\bCONVERT\s*\(\s*VARCHAR\s*(?:\(\s*\d+\s*\))?\s*,\s*([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _castVc    = new(@"\bCAST\s*\(([^)]+)\s+AS\s+VARCHAR(?:\s*\(\s*\d+\s*\))?\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Captura SELECT TOP N en cualquier posición (outer o subquery)
    private static readonly Regex _topSelect = new(@"\bSELECT\s+TOP\s+(\d+)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Adapt(string sql, bool esSqlite)
    {
        if (!esSqlite) return sql;

        // 1. Table hints
        sql = _hint.Replace(sql, "");

        // 2. ISNULL → IFNULL
        sql = _isNull.Replace(sql, "IFNULL(");

        // 3. GETDATE() → datetime('now')
        sql = _getDate.Replace(sql, "datetime('now')");

        // 4. CONVERT(VARCHAR, expr) → CAST(expr AS TEXT)
        sql = _convert.Replace(sql, m => $"CAST({m.Groups[1].Value.Trim()} AS TEXT)");

        // 5. CAST(x AS VARCHAR) → CAST(x AS TEXT)
        sql = _castVc.Replace(sql, m => $"CAST({m.Groups[1].Value.Trim()} AS TEXT)");

        // 6. SELECT TOP N → SELECT  (+ LIMIT al final del SELECT correspondiente)
        //    Procesar cada ocurrencia de SELECT TOP N:
        //    - Subqueries (dentro de paréntesis): reemplazar SELECT TOP N por SELECT y añadir LIMIT N
        //      justo antes del ) de cierre del subquery
        //    - Outer SELECT TOP N: añadir LIMIT N al final de toda la query
        sql = ConvertTopToLimit(sql);

        return sql;
    }

    private static string ConvertTopToLimit(string sql)
    {
        // Estrategia: reemplazar todos los SELECT TOP N por SELECT y registrar el N.
        // Luego para cada SELECT que tenía TOP, necesitamos insertar LIMIT N al final
        // del SELECT correspondiente (antes de su ) si es subquery, o al final si es outer).
        //
        // Implementación simplificada pero correcta:
        // Buscar SELECT TOP N de derecha a izquierda (para tratar primero los más internos)
        // y para cada uno:
        //   - Quitar "TOP N " del SELECT
        //   - Buscar dónde termina ese SELECT (antes del ) de cierre o al final)
        //   - Insertar LIMIT N

        var result = sql;
        int safety = 0;
        while (_topSelect.IsMatch(result) && safety++ < 10)
        {
            // Encontrar la última ocurrencia (más interna en subqueries anidadas)
            var matches = _topSelect.Matches(result);
            var m = matches[matches.Count - 1];  // última = más interna

            int n      = int.Parse(m.Groups[1].Value);
            int selectStart = m.Index;
            int afterTop    = m.Index + m.Length; // posición después de "SELECT TOP N "

            // Reconstruir: quitar "TOP N " del match
            var before = result[..selectStart] + "SELECT ";
            var after  = result[afterTop..];

            // Ahora encontrar dónde termina este SELECT dentro de 'after'
            // Buscar el final: si hay ) no balanceado → el final del subquery, si no → final del string
            int limitPos = FindSelectEnd(after);

            // Insertar LIMIT N en esa posición dentro de 'after'
            var afterWithLimit = after[..limitPos] + $" LIMIT {n}" + after[limitPos..];

            result = before + afterWithLimit;
        }

        return result.Trim();
    }

    /// <summary>
    /// Dado el texto después de "SELECT [cols...] FROM ...",
    /// encuentra la posición donde termina este SELECT (para insertar LIMIT allí).
    /// Termina en: ) no balanceado, ; o fin de string.
    /// Ignora paréntesis balanceados (subqueries internas ya procesadas).
    /// </summary>
    private static int FindSelectEnd(string text)
    {
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(') { depth++; }
            else if (c == ')')
            {
                if (depth == 0) return i;  // cierra un paréntesis externo → fin del subquery
                depth--;
            }
            else if (c == ';' && depth == 0) return i;
        }
        // Llegamos al final sin encontrar cierre → es el SELECT outer
        // Quitar espacios/punto y coma finales
        int end = text.Length;
        while (end > 0 && (text[end - 1] == ' ' || text[end - 1] == '\t' ||
                           text[end - 1] == '\n' || text[end - 1] == '\r' ||
                           text[end - 1] == ';'))
            end--;
        return end;
    }
}
