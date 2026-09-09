using System.Security.Claims;

namespace Coms.Web.Identity
{
    public static class PrincipalExtensions
    {
        /// <summary>Staff and administrators may create and change business records.</summary>
        public static bool CanEdit(this ClaimsPrincipal user)
        {
            return user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.Staff);
        }

        public static bool IsAdministrator(this ClaimsPrincipal user)
        {
            return user.IsInRole(Roles.Administrator);
        }

        public static string RoleLabel(this ClaimsPrincipal user)
        {
            if (user.IsInRole(Roles.Administrator))
            {
                return "Administrator";
            }

            if (user.IsInRole(Roles.Staff))
            {
                return "Staff";
            }

            return user.IsInRole(Roles.ReadOnly) ? "Read only" : string.Empty;
        }
    }
}
