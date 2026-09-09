namespace Coms.Web.Identity
{
    /// <summary>The three roles. A user has exactly one.</summary>
    public static class Roles
    {
        public const string Administrator = "Administrator";
        public const string Staff = "Staff";
        public const string ReadOnly = "ReadOnly";

        public static readonly string[] All = { Administrator, Staff, ReadOnly };
    }

    /// <summary>Authorization policy names used on controllers and actions.</summary>
    public static class Policies
    {
        /// <summary>Any signed-in user.</summary>
        public const string CanView = "CanView";

        /// <summary>Staff and administrators: create and change business records.</summary>
        public const string CanEdit = "CanEdit";

        /// <summary>Administrators only: users, settings.</summary>
        public const string IsAdministrator = "IsAdministrator";
    }
}
