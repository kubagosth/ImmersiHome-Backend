using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using System.Reflection;

namespace ImmersiHome_API.Persistence.Mappers
{
    public class DefaultGenericMapper<TModel, TEntity, TKey> : IGenericMapper<TModel, TEntity, TKey>
        where TModel : IGenericModel<TKey>, new()
        where TEntity : IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        public TModel MapToModel(TEntity entity)
        {
            var model = new TModel();
            DefaultGenericMapper<TModel, TEntity, TKey>.CopyProperties(entity, model);
            return model;
        }

        public TEntity MapToEntity(TModel model)
        {
            var entity = new TEntity();
            DefaultGenericMapper<TModel, TEntity, TKey>.CopyProperties(model, entity);
            return entity;
        }

        private static void CopyProperties(object source, object destination)
        {
            var srcProps = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.CanRead);
            var destProps = destination.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.CanWrite);

            foreach (var src in srcProps)
            {
                var dest = destProps.FirstOrDefault(p => p.Name == src.Name && p.PropertyType == src.PropertyType);
                dest?.SetValue(destination, src.GetValue(source));
            }
        }
    }
}
