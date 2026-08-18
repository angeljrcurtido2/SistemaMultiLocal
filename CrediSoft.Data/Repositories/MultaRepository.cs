using Dapper;

namespace CrediSoft.Data.Repositories;

public class MultaFuncionarioRow
{
    public int      Id               { get; set; }
    public int      Idu              { get; set; }
    public string   NombreFuncionario{ get; set; } = "";
    public byte     IdLocal          { get; set; }
    public string   LocalNombre      { get; set; } = "";
    public decimal  Monto            { get; set; }
    public string   Concepto         { get; set; } = "";
    public DateTime Fecha            { get; set; }
    public byte     Mes              { get; set; }
    public short    Anio             { get; set; }
    public string   UsuarioCarga     { get; set; } = "";
    public string   Estado           { get; set; } = "V";
}

public interface IMultaRepository
{
    Task<IEnumerable<MultaFuncionarioRow>> ListarAsync(byte mes, short anio, int? idLocal = null, int? idFuncionario = null);
    Task<int> CargarAsync(int idu, byte idLocal, decimal monto, string concepto, int idUsuarioCarga);
    Task<bool> AnularAsync(int id);
    // Usado por PagosWindow para autocompletar el descuento de Multas del período liquidado.
    Task<decimal> ObtenerTotalMesAsync(int idu, byte mes, short anio);
}

public class MultaRepository : IMultaRepository
{
    private readonly IDbConnectionFactory _factory;
    public MultaRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<MultaFuncionarioRow>> ListarAsync(byte mes, short anio, int? idLocal = null, int? idFuncionario = null)
    {
        using var conn = _factory.Create();
        var where = "WHERE m.MES = @Mes AND m.ANIO = @Anio AND m.ESTADO = 'V' ";
        if (idLocal.HasValue) where += "AND m.ID_LOCAL = @IdLocal ";
        if (idFuncionario.HasValue) where += "AND m.IDU = @IdFuncionario ";

        return await conn.QueryAsync<MultaFuncionarioRow>(
            "SELECT m.ID, m.IDU, ISNULL(u.NOMBRE_USUARIO,'') AS NombreFuncionario, " +
            "m.ID_LOCAL AS IdLocal, ISNULL(l.NOMBRE,'') AS LocalNombre, " +
            "m.MONTO, m.CONCEPTO, m.FECHA, m.MES, m.ANIO, " +
            "ISNULL(uc.NOMBRE_USUARIO,'') AS UsuarioCarga, m.ESTADO " +
            "FROM MULTAS_FUNCIONARIOS m " +
            "LEFT JOIN USUARIOS u  ON m.IDU = u.ID_USUARIO " +
            "LEFT JOIN USUARIOS uc ON m.ID_USUARIO_CARGA = uc.ID_USUARIO " +
            "LEFT JOIN LOCALES  l  ON m.ID_LOCAL = l.ID_LOCAL " +
            where +
            "ORDER BY m.FECHA DESC",
            new { Mes = mes, Anio = anio, IdLocal = idLocal, IdFuncionario = idFuncionario });
    }

    public async Task<int> CargarAsync(int idu, byte idLocal, decimal monto, string concepto, int idUsuarioCarga)
    {
        using var conn = _factory.Create();
        var hoy = DateTime.Today;
        return await conn.QuerySingleAsync<int>(
            "INSERT INTO MULTAS_FUNCIONARIOS (IDU, ID_LOCAL, MONTO, CONCEPTO, FECHA, MES, ANIO, ID_USUARIO_CARGA, ESTADO) " +
            "VALUES (@Idu, @IdLocal, @Monto, @Concepto, GETDATE(), @Mes, @Anio, @IdUsuarioCarga, 'V'); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { Idu = idu, IdLocal = idLocal, Monto = monto, Concepto = concepto,
                  Mes = (byte)hoy.Month, Anio = (short)hoy.Year, IdUsuarioCarga = idUsuarioCarga });
    }

    public async Task<bool> AnularAsync(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE MULTAS_FUNCIONARIOS SET ESTADO = 'A' WHERE ID = @Id AND ESTADO = 'V'",
            new { Id = id });
        return rows > 0;
    }

    public async Task<decimal> ObtenerTotalMesAsync(int idu, byte mes, short anio)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<decimal?>(
            "SELECT SUM(MONTO) FROM MULTAS_FUNCIONARIOS WHERE IDU = @Idu AND MES = @Mes AND ANIO = @Anio AND ESTADO = 'V'",
            new { Idu = idu, Mes = mes, Anio = anio }) ?? 0m;
    }
}
