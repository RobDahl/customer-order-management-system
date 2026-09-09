using System;
using System.Data;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Xunit;

namespace Coms.Data.Tests.Connections
{
    public class SqlConnectionFactoryTests
    {
        [Fact]
        public void Constructor_RejectsEmptyConnectionString()
        {
            Assert.Throws<ArgumentException>(() => new SqlConnectionFactory(" "));
        }

        [Fact]
        public async Task OpenAsync_ReturnsOpenConnection_WhenServerReachable()
        {
            if (!TestDatabase.IsAvailable)
            {
                return;
            }

            // Connect to master so this passes before any migration has run.
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(TestDatabase.ConnectionString)
            {
                InitialCatalog = "master"
            };

            var factory = new SqlConnectionFactory(builder.ConnectionString);

            using (IDbConnection connection = await factory.OpenAsync())
            {
                Assert.Equal(ConnectionState.Open, connection.State);
            }
        }
    }
}
