using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using ImmersiHome_API.Persistence.Repositories.Common;

namespace ImmersiHome_API.Services.Common
{
    /// <summary>
    /// Generic service
    /// </summary>
    /// <typeparam name="TDomain">The domain model type.</typeparam>
    /// <typeparam name="TEntity">The persistence entity type.</typeparam>
    /// <typeparam name="TKey">The primary key type.</typeparam>
    public class GenericService<TDomain, TEntity, TKey> : IGenericService<TDomain, TKey>
        where TDomain : IGenericModel<TKey>, new()
        where TEntity : IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly IGenericRepository<TDomain, TKey> Repository;
        protected readonly ILogger<GenericService<TDomain, TEntity, TKey>> Logger;

        public GenericService(IUnitOfWork unitOfWork, ILogger<GenericService<TDomain, TEntity, TKey>> logger)
        {
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Repository = UnitOfWork.GetRepository<TDomain, TEntity, TKey>();
        }

        public async Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Adding new {Type} entity", typeof(TDomain).Name);
                var result = await Repository.AddAsync(model, cancellationToken);
                await UnitOfWork.CommitAsync();
                Logger.LogInformation("{Type} entity added with id {Id}", typeof(TDomain).Name, result.Id);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding {Type} entity", typeof(TDomain).Name);
                await UnitOfWork.RollbackAsync();
                throw new GenericServiceException($"Error adding {typeof(TDomain).Name}", ex);
            }
        }

        public async Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Updating {Type} entity with id {Id}", typeof(TDomain).Name, model.Id);
                var result = await Repository.UpdateAsync(model, cancellationToken);
                await UnitOfWork.CommitAsync();
                Logger.LogInformation("{Type} entity with id {Id} updated", typeof(TDomain).Name, result.Id);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating {Type} entity with id {Id}", typeof(TDomain).Name, model.Id);
                await UnitOfWork.RollbackAsync();
                throw new GenericServiceException($"Error updating {typeof(TDomain).Name} with id {model.Id}", ex);
            }
        }

        public async Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Upserting {Type} entity with id {Id}", typeof(TDomain).Name, model.Id);
                var result = await Repository.UpsertAsync(model, cancellationToken);
                await UnitOfWork.CommitAsync();
                Logger.LogInformation("{Type} entity upserted with id {Id}", typeof(TDomain).Name, result.Id);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error upserting {Type} entity", typeof(TDomain).Name);
                await UnitOfWork.RollbackAsync();
                throw new GenericServiceException($"Error upserting {typeof(TDomain).Name}", ex);
            }
        }

        public async Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Soft deleting {Type} entity with id {Id}", typeof(TDomain).Name, id);
                var result = await Repository.SoftDeleteAsync(id, cancellationToken);
                await UnitOfWork.CommitAsync();
                Logger.LogInformation("{Type} entity with id {Id} soft deleted", typeof(TDomain).Name, id);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error soft deleting {Type} entity with id {Id}", typeof(TDomain).Name, id);
                await UnitOfWork.RollbackAsync();
                throw new GenericServiceException($"Error soft deleting {typeof(TDomain).Name} with id {id}", ex);
            }
        }

        public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Deleting {Type} entity with id {Id}", typeof(TDomain).Name, id);
                var result = await Repository.DeleteAsync(id, cancellationToken);
                await UnitOfWork.CommitAsync();
                Logger.LogInformation("{Type} entity with id {Id} deleted", typeof(TDomain).Name, id);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting {Type} entity with id {Id}", typeof(TDomain).Name, id);
                await UnitOfWork.RollbackAsync();
                throw new GenericServiceException($"Error deleting {typeof(TDomain).Name} with id {id}", ex);
            }
        }

        public async Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Retrieving {Type} entity with id {Id}", typeof(TDomain).Name, id);
                var result = await Repository.GetByIdAsync(id, cancellationToken);
                if (result == null)
                {
                    Logger.LogWarning("{Type} entity with id {Id} not found", typeof(TDomain).Name, id);
                }
                else
                {
                    Logger.LogInformation("{Type} entity with id {Id} retrieved", typeof(TDomain).Name, id);
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving {Type} entity with id {Id}", typeof(TDomain).Name, id);
                throw new GenericServiceException($"Error retrieving {typeof(TDomain).Name} with id {id}", ex);
            }
        }

        public async Task<IEnumerable<TDomain>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Retrieving all {Type} entities", typeof(TDomain).Name);
                var result = new List<TDomain>();
                await foreach (var item in Repository.GetAllAsync(cancellationToken))
                {
                    result.Add(item);
                }
                Logger.LogInformation("{Count} {Type} entities retrieved", result.Count, typeof(TDomain).Name);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving all {Type} entities", typeof(TDomain).Name);
                throw new GenericServiceException($"Error retrieving all {typeof(TDomain).Name} entities", ex);
            }
        }
    }
}
