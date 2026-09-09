using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Data.Connections
{
    public sealed class DbSession : IDbSession
    {
        private readonly IDbConnectionFactory _factory;
        private IDbConnection? _connection;
        private IDbTransaction? _transaction;
        private bool _disposed;

        public DbSession(IDbConnectionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IDbTransaction? Transaction => _transaction;

        public bool HasActiveTransaction => _transaction != null;

        public async Task<IDbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_connection == null)
            {
                _connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            return _connection;
        }

        public async Task BeginAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_transaction != null)
            {
                throw new InvalidOperationException("A transaction is already active on this session.");
            }

            IDbConnection connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            _transaction = connection.BeginTransaction();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction is active on this session.");
            }

            try
            {
                _transaction.Commit();
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
            }

            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCore();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            RollbackCore();
            _connection?.Dispose();
            _connection = null;
        }

        private void RollbackCore()
        {
            if (_transaction == null)
            {
                return;
            }

            try
            {
                _transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
                // The server already rolled the transaction back, for example
                // when a stored procedure failed and rolled back itself.
                // There is nothing left to undo.
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DbSession));
            }
        }
    }
}
