using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Coms.Desktop.Infrastructure
{
    /// <summary>
    /// Runs a unit of work inside a dependency injection scope. The data
    /// session is scoped, so every screen action gets its own connection
    /// and the connection is closed when the action finishes, the same
    /// shape as one web request.
    /// </summary>
    internal sealed class Scoped
    {
        private readonly IServiceProvider _root;

        public Scoped(IServiceProvider root)
        {
            _root = root;
        }

        public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work)
        {
            using (IServiceScope scope = _root.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                return await work(service);
            }
        }

        public async Task RunAsync<TService>(Func<TService, Task> work)
        {
            using (IServiceScope scope = _root.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                await work(service);
            }
        }

        public async Task<TResult> RunAsync<TResult>(Func<IServiceProvider, Task<TResult>> work)
        {
            using (IServiceScope scope = _root.CreateScope())
            {
                return await work(scope.ServiceProvider);
            }
        }
    }
}
