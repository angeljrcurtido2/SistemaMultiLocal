using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;

namespace CrediSoft.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
    bool EsSqlite { get; }
}

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public bool EsSqlite => false;

    public SqlServerConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public bool EsSqlite => true;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Create()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        // Habilitar FK y WAL para mejor rendimiento
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
        return conn;
    }
}

// Factory que decide SQL Server o SQLite según la connection string
public static class DbConnectionFactoryBuilder
{
    public static IDbConnectionFactory Create(string connectionString)
    {
        if (connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
            return new SqliteConnectionFactory(connectionString);

        return new SqlServerConnectionFactory(connectionString);
    }
}
