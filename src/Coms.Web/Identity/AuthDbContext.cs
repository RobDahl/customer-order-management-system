using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coms.Web.Identity
{
    /// <summary>
    /// ASP.NET Core Identity store, kept in its own schema so the business
    /// tables in dbo stay owned by the SQL migrations. This is the only
    /// place Entity Framework is used.
    /// </summary>
    public class AuthDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public const string Schema = "auth";

        public AuthDbContext(DbContextOptions<AuthDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(Schema);
        }
    }
}
