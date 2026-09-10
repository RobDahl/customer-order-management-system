using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.IsAdministrator)]
    public class UsersController : ComsController
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly ILogger<UsersController> _logger;

        public UsersController(UserManager<IdentityUser> users, ILogger<UsersController> logger)
        {
            _users = users;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            List<IdentityUser> users = await _users.Users.OrderBy(u => u.UserName).ToListAsync();
            var rows = new List<UserRow>(users.Count);

            foreach (IdentityUser user in users)
            {
                IList<string> roles = await _users.GetRolesAsync(user);
                rows.Add(new UserRow
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "(none)",
                    IsLockedOut = await _users.IsLockedOutAsync(user),
                    AccessFailedCount = user.AccessFailedCount
                });
            }

            return View(new UserListModel { Users = rows });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserModel model)
        {
            if (string.IsNullOrWhiteSpace(model.UserName))
            {
                ModelState.AddModelError(nameof(model.UserName), "User name is required.");
            }

            if (!Roles.All.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Choose a role.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new IdentityUser { UserName = model.UserName.Trim(), Email = model.Email.Trim(), EmailConfirmed = true, LockoutEnabled = true };
            IdentityResult created = await _users.CreateAsync(user, model.Password);
            if (!created.Succeeded)
            {
                AddIdentityErrors(created);
                return View(model);
            }

            await _users.AddToRoleAsync(user, model.Role);
            _logger.LogInformation("User {User} created in role {Role} by {Admin}", user.UserName, model.Role, User.Identity?.Name);

            Flash("User " + user.UserName + " created.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            IdentityUser? user = await _users.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            IList<string> roles = await _users.GetRolesAsync(user);
            return View(new EditUserModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? Roles.ReadOnly,
                IsLockedOut = await _users.IsLockedOutAsync(user),
                LockoutEnd = user.LockoutEnd,
                IsSelf = string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserModel model)
        {
            IdentityUser? user = await _users.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            bool isSelf = string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
            model.IsSelf = isSelf;
            model.UserName = user.UserName ?? string.Empty;

            if (!Roles.All.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Choose a role.");
            }

            if (isSelf && model.Role != Roles.Administrator)
            {
                ModelState.AddModelError(nameof(model.Role), "You cannot remove your own administrator role.");
            }

            if (isSelf && model.IsLockedOut)
            {
                ModelState.AddModelError(nameof(model.IsLockedOut), "You cannot lock your own account.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.Email = model.Email.Trim();
            IdentityResult updated = await _users.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                AddIdentityErrors(updated);
                return View(model);
            }

            IList<string> currentRoles = await _users.GetRolesAsync(user);
            if (!currentRoles.Contains(model.Role))
            {
                await _users.RemoveFromRolesAsync(user, currentRoles);
                await _users.AddToRoleAsync(user, model.Role);
            }

            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, model.IsLockedOut ? DateTimeOffset.MaxValue : (DateTimeOffset?)null);
            if (!model.IsLockedOut)
            {
                await _users.ResetAccessFailedCountAsync(user);
            }

            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                string token = await _users.GeneratePasswordResetTokenAsync(user);
                IdentityResult reset = await _users.ResetPasswordAsync(user, token, model.NewPassword);
                if (!reset.Succeeded)
                {
                    AddIdentityErrors(reset);
                    return View(model);
                }
            }

            _logger.LogInformation("User {User} updated by {Admin}: role {Role}, locked {Locked}", user.UserName, User.Identity?.Name, model.Role, model.IsLockedOut);

            Flash("User " + user.UserName + " saved.");
            return RedirectToAction(nameof(Index));
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
