using System;
using System.Threading.Tasks;
using Coms.Application;
using Coms.Data;
using Coms.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coms.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            string connectionString = builder.Configuration.GetConnectionString("Coms")
                ?? throw new InvalidOperationException("Connection string 'Coms' is not configured.");

            var options = new ComsOptions();
            builder.Configuration.GetSection("Coms").Bind(options);

            builder.Services.AddSingleton(new AppInfo(connectionString, builder.Environment.EnvironmentName));
            builder.Services.AddComsData(connectionString);
            builder.Services.AddComsApplication(options);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            builder.Services.AddDbContext<AuthDbContext>(db =>
                db.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)));

            builder.Services
                .AddIdentity<IdentityUser, IdentityRole>(identity =>
                {
                    identity.Password.RequiredLength = 8;
                    identity.Password.RequireNonAlphanumeric = false;
                    identity.Lockout.MaxFailedAccessAttempts = 5;
                    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    identity.User.RequireUniqueEmail = false;
                })
                .AddEntityFrameworkStores<AuthDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(cookie =>
            {
                cookie.LoginPath = "/Account/Login";
                cookie.LogoutPath = "/Account/Logout";
                cookie.AccessDeniedPath = "/Account/AccessDenied";
                cookie.ExpireTimeSpan = TimeSpan.FromHours(10);
                cookie.SlidingExpiration = true;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
            });

            builder.Services.AddAuthorization(auth =>
            {
                auth.AddPolicy(Policies.CanView, policy => policy.RequireRole(Roles.Administrator, Roles.Staff, Roles.ReadOnly));
                auth.AddPolicy(Policies.CanEdit, policy => policy.RequireRole(Roles.Administrator, Roles.Staff));
                auth.AddPolicy(Policies.IsAdministrator, policy => policy.RequireRole(Roles.Administrator));

                // Everything requires a signed-in user unless an action opts out with [AllowAnonymous].
                auth.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });

            builder.Services.AddControllersWithViews(mvc =>
            {
                // Validation messages come from the domain, not from the framework's
                // implicit [Required] on non-nullable reference types.
                mvc.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            });

            WebApplication app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Liveness check for monitoring: proves the database answers.
            app.MapGet("/health", async (Coms.Data.Connections.IDbConnectionFactory factory) =>
            {
                try
                {
                    using (System.Data.IDbConnection connection = await factory.OpenAsync())
                    using (System.Data.IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT 1";
                        command.ExecuteScalar();
                    }

                    return Results.Text("OK", "text/plain");
                }
                catch (Exception ex)
                {
                    return Results.Text("Database unavailable: " + ex.Message, "text/plain", statusCode: 503);
                }
            }).AllowAnonymous();

            await IdentitySeeder.RunAsync(app.Services);

            await app.RunAsync();
        }
    }
}
