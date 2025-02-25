using System.Data;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoligPletten.Infrastructure.Repositories.Common
{
    // Utility class for caching entity metadata.
    public static class EntityReflectionCache<TEntity> where TEntity : class, new()
    {
        public static readonly Dictionary<string, PropertyInfo> EntityProperties;
        public static readonly PropertyInfo? IdProperty;
        public static readonly string TableName;

        public const string IdColumnName = "Id";
        public const string IsDeletedColumnName = "IsDeleted";

        static EntityReflectionCache()
        {
            EntityProperties = typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            IdProperty = EntityProperties.TryGetValue(IdColumnName, out var idProp) ? idProp : null;

            var tableAttr = typeof(TEntity).GetCustomAttribute<TableAttribute>();
            TableName = tableAttr != null ? tableAttr.Name : typeof(TEntity).Name;
        }

        public static IEnumerable<string> GetColumns(bool includeId)
        {
            var props = EntityProperties.Values.AsEnumerable();
            if (!includeId)
                props = props.Where(p => !p.Name.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase));
            props = props.Where(p => !p.Name.Equals(IsDeletedColumnName, StringComparison.OrdinalIgnoreCase));
            return props.Select(p => p.Name);
        }
    }
}
