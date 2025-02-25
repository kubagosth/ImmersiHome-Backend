using BoligPletten.Application.Repositories;
using BoligPletten.Application.Repositories.Common;
using Microsoft.Extensions.ObjectPool;
using System.Data;

namespace BoligPletten.Infrastructure.Repositories.Common
{
    /// <summary>
    /// High-performance implementation of Unit of Work using connection pooling
    /// </summary>
    public sealed class PooledUnitOfWork : IUnitOfWork
    {
        private readonly IDbConnection _connection;
        private IDbTransaction _transaction;
        private bool _disposed;
        private readonly ObjectPool<IDbConnection> _connectionPool;

        // Factory functions for repositories
        private readonly Func<IDbConnection, IDbTransaction, IHouseRepository> _houseRepositoryFactory;

        // Lazy-loaded repositories
        private Lazy<IHouseRepository> _houseRepository;

        public IHouseRepository Houses => _houseRepository.Value;

        /// <summary>
        /// Initializes a new instance of the PooledUnitOfWork class
        /// </summary>
        public PooledUnitOfWork(
            ObjectPool<IDbConnection> connectionPool,
            Func<IDbConnection, IDbTransaction, IHouseRepository> houseRepositoryFactory)
        {
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _houseRepositoryFactory = houseRepositoryFactory ?? throw new ArgumentNullException(nameof(houseRepositoryFactory));

            // Get connection from pool
            _connection = _connectionPool.Get();

            // Ensure connection is open
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            // Start transaction
            _transaction = _connection.BeginTransaction();

            // Initialize lazy repositories
            InitializeRepositories();
        }

        /// <summary>
        /// Initializes repository instances with lazy loading
        /// </summary>
        private void InitializeRepositories()
        {
            // Use LazyThreadSafetyMode.ExecutionAndPublication for thread safety with minimal overhead
            _houseRepository = new Lazy<IHouseRepository>(
                () => _houseRepositoryFactory(_connection, _transaction),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Commits all changes made in the current transaction
        /// </summary>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            // Early return if no repositories were used
            if (!_houseRepository.IsValueCreated)
            {
                _transaction.Commit();
                ResetTransaction();
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _transaction.Commit();
            }
            catch
            {
                await RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            finally
            {
                ResetTransaction();
            }
        }

        /// <summary>
        /// Rolls back all changes made in the current transaction
        /// </summary>
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _transaction.Rollback();
            }
            finally
            {
                ResetTransaction();
            }

            // Use ConfigureAwait(false) for better performance in ASP.NET
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resets the current transaction and repositories
        /// </summary>
        private void ResetTransaction()
        {
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction();

            // Reset lazy repositories if they were created
            if (_houseRepository.IsValueCreated)
            {
                InitializeRepositories();
            }
        }

        /// <summary>
        /// Disposes resources used by this instance
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try { _transaction?.Dispose(); } catch { }

            // Return connection to pool instead of closing it
            _connectionPool.Return(_connection);

            _disposed = true;
        }
    }
}