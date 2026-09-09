using Coms.Application;
using Microsoft.AspNetCore.Http;

namespace Coms.Web.Identity
{
    /// <summary>The signed-in user's name, for audit columns.</summary>
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpContextCurrentUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public string UserName
        {
            get
            {
                string? name = _accessor.HttpContext?.User?.Identity?.Name;
                return string.IsNullOrEmpty(name) ? "system" : name!;
            }
        }
    }
}
