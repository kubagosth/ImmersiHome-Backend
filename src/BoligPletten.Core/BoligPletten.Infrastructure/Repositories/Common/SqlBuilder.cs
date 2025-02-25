using System.Collections.Concurrent;
using System.Text;

namespace BoligPletten.Infrastructure.Repositories.Common
{
    /// <summary>
    /// High-performance SQL query builder with minimal allocations and string concatenations
    /// </summary>
    public sealed class SqlBuilder
    {
        private readonly StringBuilder _builder;
        private readonly string _tableName;
        private readonly string _idColumn;
        private readonly string _isDeletedColumn;
        private readonly HashSet<string> _columns;

        // Cache builders for specific tables to avoid recreation
        private static readonly ConcurrentDictionary<string, SqlBuilder> _builderCache =
            new ConcurrentDictionary<string, SqlBuilder>();

        /// <summary>
        /// Gets or creates a SqlBuilder for the specified entity type
        /// </summary>
        public static SqlBuilder For<TEntity>() where TEntity : class, new()
        {
            string tableName = EntityReflectionCache<TEntity>.TableName;
            return _builderCache.GetOrAdd(tableName, t => new SqlBuilder(
                t,
                EntityReflectionCache<TEntity>.GetColumns(true),
                EntityReflectionCache<TEntity>.IdColumnName,
                EntityReflectionCache<TEntity>.IsDeletedColumnName));
        }

        private SqlBuilder(string tableName, IEnumerable<string> columns, string idColumn, string isDeletedColumn)
        {
            _tableName = tableName;
            _idColumn = idColumn;
            _isDeletedColumn = isDeletedColumn;
            _builder = new StringBuilder(256); // Pre-allocate for better performance
            _columns = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a SELECT query to fetch a record by its ID
        /// </summary>
        public string SelectById()
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = @Id");

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" AND ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(';');
            return _builder.ToString();
        }

        /// <summary>
        /// Builds a SELECT query to fetch all non-deleted records
        /// </summary>
        public string SelectAll()
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" WHERE ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(';');
            return _builder.ToString();
        }

        /// <summary>
        /// Builds a SELECT query with pagination
        /// </summary>
        public string SelectPaginated()
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" WHERE ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(" LIMIT @PageSize OFFSET @Offset;");
            return _builder.ToString();
        }

        /// <summary>
        /// Builds an INSERT query that returns the inserted ID
        /// </summary>
        public string Insert(IEnumerable<string> columnNames)
        {
            var cols = columnNames.ToList();

            _builder.Clear();
            _builder.Append("INSERT INTO ");
            _builder.Append(_tableName);
            _builder.Append(" (");
            _builder.Append(string.Join(", ", cols));
            _builder.Append(") VALUES (");
            _builder.Append(string.Join(", ", cols.Select(c => "@" + c)));
            _builder.Append(") RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds an UPDATE query for a specific record
        /// </summary>
        public string Update(IEnumerable<string> columnNames)
        {
            var cols = columnNames.ToList();

            _builder.Clear();
            _builder.Append("UPDATE ");
            _builder.Append(_tableName);
            _builder.Append(" SET ");
            _builder.Append(string.Join(", ", cols.Select(c => $"{c} = @{c}")));
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = @Id RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a UPSERT (INSERT ... ON CONFLICT) query
        /// </summary>
        public string Upsert(IEnumerable<string> columnNames)
        {
            var cols = columnNames.ToList();

            _builder.Clear();
            _builder.Append("INSERT INTO ");
            _builder.Append(_tableName);
            _builder.Append(" (");
            _builder.Append(string.Join(", ", cols));
            _builder.Append(") VALUES (");
            _builder.Append(string.Join(", ", cols.Select(c => "@" + c)));
            _builder.Append(") ON CONFLICT (");
            _builder.Append(_idColumn);
            _builder.Append(") DO UPDATE SET ");
            _builder.Append(string.Join(", ", cols.Select(c => $"{c} = EXCLUDED.{c}")));
            _builder.Append(" RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a soft delete query
        /// </summary>
        public string SoftDelete()
        {
            _builder.Clear();
            _builder.Append("UPDATE ");
            _builder.Append(_tableName);
            _builder.Append(" SET ");
            _builder.Append(_isDeletedColumn);
            _builder.Append(" = TRUE WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = @Id RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a hard delete query
        /// </summary>
        public string HardDelete()
        {
            _builder.Clear();
            _builder.Append("DELETE FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = @Id RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a query to check if a record exists
        /// </summary>
        public string Exists()
        {
            _builder.Clear();
            _builder.Append("SELECT COUNT(1) FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = @Id");

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" AND ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(';');
            return _builder.ToString();
        }

        /// <summary>
        /// Builds a count query for all non-deleted records
        /// </summary>
        public string Count()
        {
            _builder.Clear();
            _builder.Append("SELECT COUNT(1) FROM ");
            _builder.Append(_tableName);

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" WHERE ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(';');
            return _builder.ToString();
        }

        /// <summary>
        /// Builds a query to fetch records by a list of IDs
        /// </summary>
        public string SelectByIds()
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = ANY(@Ids)");

            if (!string.IsNullOrEmpty(_isDeletedColumn))
            {
                _builder.Append(" AND ");
                _builder.Append(_isDeletedColumn);
                _builder.Append(" = FALSE");
            }

            _builder.Append(';');
            return _builder.ToString();
        }

        /// <summary>
        /// Builds a bulk soft delete query
        /// </summary>
        public string BulkSoftDelete()
        {
            _builder.Clear();
            _builder.Append("UPDATE ");
            _builder.Append(_tableName);
            _builder.Append(" SET ");
            _builder.Append(_isDeletedColumn);
            _builder.Append(" = TRUE WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = ANY(@Ids) RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a bulk hard delete query
        /// </summary>
        public string BulkHardDelete()
        {
            _builder.Clear();
            _builder.Append("DELETE FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(_idColumn);
            _builder.Append(" = ANY(@Ids) RETURNING ");
            _builder.Append(_idColumn);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds a query with dynamic pagination and optional where/order by clauses
        /// </summary>
        public string SelectDynamicPaginated(string? additionalWhereClause, string? orderByClause)
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);

            // Base condition (is not deleted)
            string baseCondition = string.IsNullOrEmpty(_isDeletedColumn) ?
                string.Empty : $"{_isDeletedColumn} = FALSE";

            // Combine with additional where clause if provided
            string whereClause = !string.IsNullOrWhiteSpace(additionalWhereClause) && !string.IsNullOrEmpty(baseCondition)
                ? $"{baseCondition} AND ({additionalWhereClause})"
                : !string.IsNullOrWhiteSpace(additionalWhereClause)
                    ? additionalWhereClause
                    : baseCondition;

            if (!string.IsNullOrEmpty(whereClause))
            {
                _builder.Append(" WHERE ");
                _builder.Append(whereClause);
            }

            // Add order by if provided
            if (!string.IsNullOrWhiteSpace(orderByClause))
            {
                _builder.Append(" ORDER BY ");
                _builder.Append(orderByClause);
            }

            _builder.Append(" LIMIT @PageSize OFFSET @Offset;");
            return _builder.ToString();
        }

        /// <summary>
        /// Creates a custom SELECT query with the specified WHERE clause
        /// </summary>
        public string SelectCustomWhere(string whereClause)
        {
            _builder.Clear();
            _builder.Append("SELECT * FROM ");
            _builder.Append(_tableName);
            _builder.Append(" WHERE ");
            _builder.Append(whereClause);
            _builder.Append(';');

            return _builder.ToString();
        }

        /// <summary>
        /// Builds the base of a custom query
        /// </summary>
        public SqlBuilder BeginCustomQuery()
        {
            _builder.Clear();
            return this;
        }

        /// <summary>
        /// Appends text to the query
        /// </summary>
        public SqlBuilder Append(string text)
        {
            _builder.Append(text);
            return this;
        }

        /// <summary>
        /// Gets the final SQL string
        /// </summary>
        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}