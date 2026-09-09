using System;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.DbMigrator;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Coms.Data.Tests
{
    /// <summary>
    /// Builds the Coms_Test database once per test run: drop, create, apply
    /// every migration, load the demo seed. Tests that write open a session
    /// with a transaction and dispose it without committing, so the seed
    /// data is the same for every test.
    /// </summary>
    public sealed class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            IsAvailable = TestDatabase.IsAvailable;
            ConnectionString = TestDatabase.ConnectionString;
            Factory = new SqlConnectionFactory(ConnectionString);

            if (IsAvailable)
            {
                MigrationRunner.Rebuild(ConnectionString, seed: true);
            }
        }

        public bool IsAvailable { get; }

        public string ConnectionString { get; }

        public IDbConnectionFactory Factory { get; }

        /// <summary>A session with no transaction, for read-only tests and for failure paths that must not mutate.</summary>
        public DbSession OpenSession()
        {
            return new DbSession(Factory);
        }

        /// <summary>A session with a transaction already started. Dispose it to roll everything back.</summary>
        public async Task<DbSession> BeginSessionAsync()
        {
            var session = new DbSession(Factory);
            await session.BeginAsync();
            return session;
        }

        /// <summary>Runs a scalar query on its own connection, outside any test transaction.</summary>
        public async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                return (await connection.ExecuteScalarAsync<T>(sql, parameters))!;
            }
        }

        public void Dispose()
        {
            // The database is left in place so it can be inspected after a failing run.
        }
    }

    [CollectionDefinition(Name)]
    public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "Database";
    }
}
