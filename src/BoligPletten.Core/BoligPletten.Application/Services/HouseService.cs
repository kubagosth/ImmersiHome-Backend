using BoligPletten.Application.Repositories.Common;
using BoligPletten.Domain.Models;
using System.Runtime.CompilerServices;

namespace BoligPletten.Application.Services
{
    public class HouseService : IHouseService
    {
        private readonly IUnitOfWork _unitOfWork;

        public HouseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Access the repository through the UnitOfWork property
            await foreach (var house in _unitOfWork.Houses.GetRecentlyListedHousesAsync(count, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return house;
            }
        }

        public async IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(
            decimal latitude,
            decimal longitude,
            decimal radiusInKm,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var house in _unitOfWork.Houses.GetHousesByLocationAsync(
                latitude, longitude, radiusInKm, cancellationToken).ConfigureAwait(false))
            {
                yield return house;
            }
        }

        public async Task<HouseModel> AddHouseAsync(
            HouseModel house,
            CancellationToken cancellationToken = default)
        {
            var result = await _unitOfWork.Houses.AddAsync(house, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<HouseModel?> GetHouseByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var house = await _unitOfWork.Houses.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return house;
        }

        public async Task<bool> DeleteHouseAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var result = await _unitOfWork.Houses.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}
