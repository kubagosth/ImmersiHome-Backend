using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using ImmersiHome_API.Persistence.Mappers;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Retrieves (and caches) a repository for mapping between domain models and persistence entities.
        /// </summary>
        IGenericRepository<TDomain, TKey> GetRepository<TDomain, TEntity, TKey>()
            where TDomain : IGenericModel<TKey>, new()
            where TEntity : IGenericEntity<TKey>, new()
            where TKey : struct, IEquatable<TKey>;

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        Task RollbackAsync();
    }
}
