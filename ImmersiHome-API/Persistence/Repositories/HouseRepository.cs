using Dapper;
using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Models.Entities;
using ImmersiHome_API.Persistence.Mappers;
using ImmersiHome_API.Persistence.Repositories.Common;
using Microsoft.Extensions.Caching.Memory;
using System.Data.Common;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImmersiHome_API.Persistence.Repositories
{
    public class HouseRepository : GenericRepository<HouseModel, HouseEntity, int>, IHouseRepository
    {
        public HouseRepository(IDbConnection connection, IDbTransaction transaction, IMemoryCache cache, IGenericMapper<HouseModel, HouseEntity, int> mapper)
            : base(connection, transaction, cache, mapper)
        {
        }

        public async IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {nameof(HouseEntity)} WHERE IsDeleted = FALSE ORDER BY ListedDate DESC LIMIT @Count;";
            var result = await _connection.QueryAsync<HouseEntity>(sql, new { Count = count }, _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }

        public async IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            const decimal EarthRadiusKm = 6371m;
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("SELECT *, (");
            sqlBuilder.Append($"{EarthRadiusKm} * acos(cos(radians(@lat)) * cos(radians(Latitude)) ");
            sqlBuilder.Append(" * cos(radians(Longitude) - radians(@lon)) + sin(radians(@lat)) * sin(radians(Latitude)))");
            sqlBuilder.Append(") AS Distance ");
            sqlBuilder.Append($"FROM {nameof(HouseEntity)} ");
            sqlBuilder.Append("WHERE IsDeleted = FALSE ");
            sqlBuilder.Append("HAVING Distance <= @radius ");
            sqlBuilder.Append("ORDER BY Distance ASC;");

            var parameters = new DynamicParameters();
            parameters.Add("lat", latitude);
            parameters.Add("lon", longitude);
            parameters.Add("radius", radiusInKm);

            var result = await _connection.QueryAsync<HouseEntity>(sqlBuilder.ToString(), parameters, _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }
    }
}
