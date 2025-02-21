using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;

namespace ImmersiHome_API.Persistence.Mappers
{
    public interface IGenericMapper<TModel, TEntity, TKey>
        where TModel : IGenericModel<TKey>
        where TEntity : IGenericEntity<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        TModel MapToModel(TEntity entity);
        TEntity MapToEntity(TModel model);
    }
}
