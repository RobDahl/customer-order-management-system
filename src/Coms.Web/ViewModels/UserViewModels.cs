using System;
using System.Collections.Generic;
using Coms.Web.Identity;

namespace Coms.Web.ViewModels
{
    public sealed class UserRow
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string Role { get; set; } = string.Empty;

        public bool IsLockedOut { get; set; }

        public int AccessFailedCount { get; set; }
    }

    public sealed class UserListModel
    {
        public IReadOnlyList<UserRow> Users { get; set; } = new List<UserRow>();
    }

    public sealed class CreateUserModel
    {
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = Roles.ReadOnly;
    }

    public sealed class EditUserModel
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = Roles.ReadOnly;

        public bool IsLockedOut { get; set; }

        /// <summary>Leave blank to keep the current password.</summary>
        public string? NewPassword { get; set; }

        public bool IsSelf { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }
    }
}
