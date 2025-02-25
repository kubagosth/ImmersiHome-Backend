using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using System.Reflection;

namespace ImmersiHome_API.Infrastructure.Mappers
{
    // High-performance mapper implementation
    public class HighPerformanceMapper<TModel, TEntity, TKey> : IGenericMapper<TModel, TEntity, TKey>
        where TModel : class, IGenericModel<TKey>, new()
        where TEntity : class, IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        // Cache property mappings for better performance
        private static readonly Dictionary<string, PropertyInfo> _entityProps;
        private static readonly Dictionary<string, PropertyInfo> _modelProps;
        private static readonly List<(PropertyInfo Source, PropertyInfo Target)> _entityToModelMappings;
        private static readonly List<(PropertyInfo Source, PropertyInfo Target)> _modelToEntityMappings;

        static HighPerformanceMapper()
        {
            // Initialize property caches
            _entityProps = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            _modelProps = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            // Pre-compute property mappings
            _entityToModelMappings = [];
            foreach (var srcProp in _entityProps.Values)
            {
                if (_modelProps.TryGetValue(srcProp.Name, out var destProp) && destProp.PropertyType == srcProp.PropertyType)
                {
                    _entityToModelMappings.Add((srcProp, destProp));
                }
            }

            _modelToEntityMappings = [];
            foreach (var srcProp in _modelProps.Values)
            {
                if (_entityProps.TryGetValue(srcProp.Name, out var destProp) && destProp.PropertyType == srcProp.PropertyType)
                {
                    if (destProp.CanWrite) // Only map to writable entity properties
                    {
                        _modelToEntityMappings.Add((srcProp, destProp));
                    }
                }
            }
        }

        public TModel MapToModel(TEntity entity)
        {
            var model = new TModel();
            foreach (var (src, dest) in _entityToModelMappings)
            {
                dest.SetValue(model, src.GetValue(entity));
            }
            return model;
        }

        public TEntity MapToEntity(TModel model)
        {
            var entity = new TEntity();
            foreach (var (src, dest) in _modelToEntityMappings)
            {
                dest.SetValue(entity, src.GetValue(model));
            }
            return entity;
        }
    }
}
