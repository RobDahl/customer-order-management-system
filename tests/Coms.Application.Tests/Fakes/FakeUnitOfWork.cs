using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Begins { get; private set; }

        public int Commits { get; private set; }

        public int Rollbacks { get; private set; }

        public bool HasActiveTransaction { get; private set; }

        public Task BeginAsync(CancellationToken cancellationToken = default)
        {
            Begins++;
            HasActiveTransaction = true;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            HasActiveTransaction = false;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollbacks++;
            HasActiveTransaction = false;
            return Task.CompletedTask;
        }
    }
}
