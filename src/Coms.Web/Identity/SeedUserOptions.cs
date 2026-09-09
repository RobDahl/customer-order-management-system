using System.Collections.Generic;

namespace Coms.Web.Identity
{
    /// <summary>
    /// Users created on first start when the user table is empty. Bound from
    /// the "Identity:SeedUsers" configuration section. Passwords here are
    /// for a fresh development database only; change them on first login
    /// or override the section through user secrets or environment variables.
    /// </summary>
    public sealed class SeedUserOptions
    {
        public const string SectionName = "Identity:SeedUsers";

        public List<SeedUser> Users { get; set; } = new List<SeedUser>();
    }

    public sealed class SeedUser
    {
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = Roles.ReadOnly;
    }
}
