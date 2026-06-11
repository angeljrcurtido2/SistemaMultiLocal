using Microsoft.Data.SqlClient;
using System.Data;

namespace CrediSoft.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
