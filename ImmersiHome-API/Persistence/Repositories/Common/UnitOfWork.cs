using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using ImmersiHome_API.Persistence.Mappers;
using Microsoft.Extensions.Caching.Memory;
using System.Data;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDbConnection _connection;
        private IDbTransaction _transaction;
        private readonly IMemoryCache _cache;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, object> _repositories = new Dictionary<string, object>();
        private bool _disposed;

        public UnitOfWork(IDbConnection connection, IMemoryCache cache, IServiceProvider serviceProvider)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            if (_connection.State != ConnectionState.Open)
                _connection.Open();
            _transaction = _connection.BeginTransaction();
        }

        public IGenericRepository<TDomain, TKey> GetRepository<TDomain, TEntity, TKey>()
            where TDomain : IGenericModel<TKey>, new()
            where TEntity : IGenericEntity<TKey>, new()
            where TKey : struct, IEquatable<TKey>
        {
            string key = $"{typeof(TDomain).FullName}_{typeof(TEntity).FullName}_{typeof(TKey).FullName}";
            if (!_repositories.ContainsKey(key))
            {
                IGenericRepository<TDomain, TKey> repository;
                // For houses, return the custom repository.
                if (typeof(TEntity) == typeof(Models.Entities.HouseEntity) &&
                    typeof(TDomain) == typeof(Models.Domain.HouseModel) &&
                    typeof(TKey) == typeof(int))
                {
                    var mapper = _serviceProvider.GetService<IGenericMapper<Models.Domain.HouseModel, Models.Entities.HouseEntity, int>>()
                                 ?? new DefaultGenericMapper<Models.Domain.HouseModel, Models.Entities.HouseEntity, int>();
                    repository = (IGenericRepository<TDomain, TKey>)(object)new HouseRepository(
                        _connection, _transaction, _cache, mapper);
                }
                else
                {
                    var mapper = _serviceProvider.GetService<IGenericMapper<TDomain, TEntity, TKey>>()
                                 ?? new DefaultGenericMapper<TDomain, TEntity, TKey>();
                    repository = new GenericRepository<TDomain, TEntity, TKey>(_connection, _transaction, _cache, mapper);
                }
                _repositories.Add(key, repository);
            }
            return (IGenericRepository<TDomain, TKey>)_repositories[key];
        }

        public async Task CommitAsync()
        {
            try
            {
                _transaction.Commit();
            }
            catch
            {
                _transaction.Rollback();
                throw;
            }
            finally
            {
                _repositories.Clear();
                _transaction.Dispose();
                _transaction = _connection.BeginTransaction();
            }
            await Task.CompletedTask;
        }

        public async Task RollbackAsync()
        {
            try
            {
                _transaction.Rollback();
            }
            finally
            {
                _repositories.Clear();
                _transaction.Dispose();
                _transaction = _connection.BeginTransaction();
            }
            await Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try { _transaction?.Dispose(); } catch { }
                    try
                    {
                        if (_connection.State == ConnectionState.Open)
                        {
                            _connection.Close();
                        }
                        _connection.Dispose();
                    }
                    catch { }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
