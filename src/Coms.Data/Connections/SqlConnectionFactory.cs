using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Coms.Data.Connections
{
    public sealed class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }
}
