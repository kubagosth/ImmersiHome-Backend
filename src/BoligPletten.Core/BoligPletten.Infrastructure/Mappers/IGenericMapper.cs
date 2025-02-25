using BoligPletten.Domain.Models.Common;
using BoligPletten.Infrastructure.Models.Common;

namespace BoligPletten.Infrastructure.Mappers
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
