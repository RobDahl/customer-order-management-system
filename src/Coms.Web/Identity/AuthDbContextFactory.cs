using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coms.Web.Identity
{
    /// <summary>
    /// Used only by the dotnet-ef tooling when adding migrations, so the
    /// tool does not have to start the whole web host.
    /// </summary>
    public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Coms;Integrated Security=true;TrustServerCertificate=true",
                    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema))
                .Options;

            return new AuthDbContext(options);
        }
    }
}
