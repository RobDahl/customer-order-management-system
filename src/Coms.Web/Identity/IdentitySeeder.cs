using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coms.Web.Identity
{
    /// <summary>
    /// Applies the auth schema migrations, makes sure the three roles exist
    /// and, if there are no users at all, creates the configured seed users.
    /// </summary>
    public static class IdentitySeeder
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            using (IServiceScope scope = services.CreateScope())
            {
                IServiceProvider provider = scope.ServiceProvider;
                ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

                AuthDbContext context = provider.GetRequiredService<AuthDbContext>();
                await context.Database.MigrateAsync();

                RoleManager<IdentityRole> roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (string role in Roles.All)
                {
                    if (!await roles.RoleExistsAsync(role))
                    {
                        await roles.CreateAsync(new IdentityRole(role));
                        logger.LogInformation("Created role {Role}", role);
                    }
                }

                UserManager<IdentityUser> users = provider.GetRequiredService<UserManager<IdentityUser>>();
                if (await users.Users.AnyAsync())
                {
                    return;
                }

                var options = new SeedUserOptions();
                provider.GetRequiredService<IConfiguration>().GetSection(SeedUserOptions.SectionName).Bind(options);

                if (options.Users.Count == 0)
                {
                    logger.LogWarning("No users exist and no Identity:SeedUsers are configured; nobody can sign in.");
                    return;
                }

                foreach (SeedUser seed in options.Users)
                {
                    if (!Roles.All.Contains(seed.Role))
                    {
                        logger.LogWarning("Seed user {User} has unknown role {Role}; skipped.", seed.UserName, seed.Role);
                        continue;
                    }

                    var user = new IdentityUser { UserName = seed.UserName, Email = seed.Email, EmailConfirmed = true };
                    IdentityResult created = await users.CreateAsync(user, seed.Password);
                    if (!created.Succeeded)
                    {
                        logger.LogError("Could not create seed user {User}: {Errors}", seed.UserName, string.Join("; ", created.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await users.AddToRoleAsync(user, seed.Role);
                    logger.LogInformation("Created seed user {User} in role {Role}", seed.UserName, seed.Role);
                }
            }
        }
    }
}
