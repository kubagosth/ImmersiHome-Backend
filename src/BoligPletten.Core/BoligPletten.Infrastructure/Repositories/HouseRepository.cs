using BoligPletten.Application.Repositories;
using BoligPletten.Domain.Models;
using BoligPletten.Infrastructure.Mappers;
using BoligPletten.Infrastructure.Models;
using BoligPletten.Infrastructure.Repositories.Common;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;

namespace BoligPletten.Infrastructure.Repositories
{
    /// <summary>
    /// High-performance repository for House entities
    /// </summary>
    public class HouseRepository : GenericRepository<HouseModel, HouseEntity, int>, IHouseRepository
    {
        // Cache costly SQL queries
        private static readonly string _recentlyListedHousesSql;
        private static readonly string _geoDistanceQueryTemplate;

        // Static constructor to initialize SQL templates
        static HouseRepository()
        {
            // Get column names for more explicit SQL queries
            var columns = string.Join(", ", EntityReflectionCache<HouseEntity>.GetColumns(true));

            // Pre-compute frequently used SQL queries for better performance
            _recentlyListedHousesSql = $"SELECT {columns} FROM {nameof(HouseEntity)} WHERE IsDeleted = FALSE ORDER BY ListedDate DESC LIMIT @Count";

            // Create optimized geospatial query template
            const decimal EarthRadiusKm = 6371m;
            var geoBuilder = new StringBuilder(512); // Pre-allocate for performance

            // Define the distance calculation expression
            string distanceCalc = $"{EarthRadiusKm} * acos(cos(radians(@lat)) * cos(radians(Latitude)) * cos(radians(Longitude) - radians(@lon)) + sin(radians(@lat)) * sin(radians(Latitude)))";

            geoBuilder.Append($"SELECT {columns}, (");
            geoBuilder.Append(distanceCalc);
            geoBuilder.Append(") AS Distance ");
            geoBuilder.Append($"FROM {nameof(HouseEntity)} ");
            geoBuilder.Append("WHERE IsDeleted = FALSE ");
            // Use the full distance calculation in the WHERE clause instead of referring to the alias
            geoBuilder.Append($"AND ({distanceCalc}) <= @radius ");
            geoBuilder.Append("ORDER BY Distance ASC");

            _geoDistanceQueryTemplate = geoBuilder.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the HouseRepository class
        /// </summary>
        public HouseRepository(
            IDbConnection connection,
            IDbTransaction transaction,
            IGenericMapper<HouseModel, HouseEntity, int> mapper)
            : base(connection, transaction, mapper)
        {
        }

        /// <summary>
        /// Gets the most recently listed houses with optimized query execution
        /// </summary>
        /// <param name="count">The number of houses to retrieve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An async enumerable of house models</returns>
        public async IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Validate input
            if (count <= 0) count = 10; // Default to 10 if invalid count

            // Use cached SQL and minimal parameter dictionary
            var parameters = new Dictionary<string, object?>(1) { { "@Count", count } };

            // Use CommandBehavior.SequentialAccess for more efficient streaming
            using var command = PrepareCommand(_recentlyListedHousesSql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            // Cache column mapping once for the result set
            var columns = GetReaderColumns(reader);

            // Stream results efficiently
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = MapReaderToEntity(reader, columns);
                yield return _mapper.MapToModel(entity);
            }
        }

        /// <summary>
        /// Gets houses near a geographical location with optimized spatial query
        /// </summary>
        /// <param name="latitude">The latitude coordinate</param>
        /// <param name="longitude">The longitude coordinate</param>
        /// <param name="radiusInKm">The search radius in kilometers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An async enumerable of house models ordered by distance</returns>
        public async IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(
            decimal latitude,
            decimal longitude,
            decimal radiusInKm,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Validate input
            if (radiusInKm <= 0) radiusInKm = 10; // Default to 10km if invalid radius

            // Use minimal parameter dictionary with exact capacity
            var parameters = new Dictionary<string, object?>(3)
            {
                {"@lat", latitude },
                {"@lon", longitude },
                {"@radius", radiusInKm }
            };

            // Use cached SQL query template
            using var command = PrepareCommand(_geoDistanceQueryTemplate, parameters);

            // Use CommandBehavior.SequentialAccess for more efficient streaming
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            // Cache column mapping once for the result set
            var columns = GetReaderColumns(reader);

            // Stream results efficiently
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = MapReaderToEntity(reader, columns);
                yield return _mapper.MapToModel(entity);
            }
        }

        /// <summary>
        /// Gets houses with specific amenities (example of a custom method)
        /// </summary>
        public async IAsyncEnumerable<HouseModel> GetHousesByAmenitiesAsync(
            IEnumerable<string> amenities,
            int page = 1,
            int pageSize = 20,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var amenitiesArray = amenities as string[] ?? amenities.ToArray();
            if (!amenitiesArray.Any())
            {
                // If no amenities specified, use standard pagination
                await foreach (var house in GetPaginatedAsync(page, pageSize, cancellationToken))
                {
                    yield return house;
                }
                yield break;
            }

            // Get column names for explicit SQL
            var columns = string.Join(", ", EntityReflectionCache<HouseEntity>.GetColumns(true).Select(c => "h." + c));

            // Create dynamic query for amenities (example implementation)
            var sqlBuilder = new StringBuilder(256);
            sqlBuilder.Append($"SELECT {columns} FROM ");
            sqlBuilder.Append(nameof(HouseEntity));
            sqlBuilder.Append(" h JOIN HouseAmenities ha ON h.Id = ha.HouseId ");
            sqlBuilder.Append("WHERE h.IsDeleted = FALSE AND ha.AmenityName = ANY(@Amenities) ");
            sqlBuilder.Append("GROUP BY h.Id HAVING COUNT(DISTINCT ha.AmenityName) = @AmenityCount ");
            sqlBuilder.Append("ORDER BY h.ListedDate DESC ");
            sqlBuilder.Append("LIMIT @PageSize OFFSET @Offset");

            var parameters = new Dictionary<string, object?>(4)
            {
                { "@Amenities", amenitiesArray },
                { "@AmenityCount", amenitiesArray.Length },
                { "@PageSize", pageSize },
                { "@Offset", (page - 1) * pageSize }
            };

            using var command = PrepareCommand(sqlBuilder.ToString(), parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var readerColumns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = MapReaderToEntity(reader, readerColumns);
                yield return _mapper.MapToModel(entity);
            }
        }

        /// <summary>
        /// Gets houses with prices in a specific range
        /// </summary>
        public async IAsyncEnumerable<HouseModel> GetHousesByPriceRangeAsync(
            decimal minPrice,
            decimal maxPrice,
            int page = 1,
            int pageSize = 20,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string whereClause = "Price >= @MinPrice AND Price <= @MaxPrice";
            string orderByClause = "Price ASC";

            var parameters = new { MinPrice = minPrice, MaxPrice = maxPrice };

            await foreach (var house in GetPaginatedDynamicAsync(
                page,
                pageSize,
                whereClause,
                parameters,
                orderByClause,
                cancellationToken))
            {
                yield return house;
            }
        }

        /// <summary>
        /// Gets newly added houses (listed within the last N days)
        /// </summary>
        public async IAsyncEnumerable<HouseModel> GetNewlyAddedHousesAsync(
            int days = 7,
            int limit = 10,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Get column names for explicit SQL
            var columns = string.Join(", ", EntityReflectionCache<HouseEntity>.GetColumns(true));

            string sql = $@"
                SELECT {columns} FROM {nameof(HouseEntity)} 
                WHERE IsDeleted = FALSE 
                AND ListedDate >= @CutoffDate
                ORDER BY ListedDate DESC
                LIMIT @Limit";

            var parameters = new Dictionary<string, object?>(2)
            {
                { "@CutoffDate", DateTime.UtcNow.AddDays(-days) },
                { "@Limit", limit }
            };

            using var command = PrepareCommand(sql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var readerColumns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = MapReaderToEntity(reader, readerColumns);
                yield return _mapper.MapToModel(entity);
            }
        }
    }
}