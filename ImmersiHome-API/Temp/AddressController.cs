using Microsoft.AspNetCore.Mvc;

namespace ImmersiHome_API.Temp
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private static readonly List<Address> Addresses = new()
        {
            new Address { Id = 1, Latitude = 55.6761, Longitude = 12.5683, Description = "Copenhagen" },
            new Address { Id = 2, Latitude = 56.2639, Longitude = 9.5018, Description = "Aarhus" },
            new Address { Id = 3, Latitude = 55.4670, Longitude = 10.4691, Description = "Odense" },
            new Address { Id = 4, Latitude = 57.0488, Longitude = 9.9168, Description = "Aalborg" },
            new Address { Id = 5, Latitude = 55.3973, Longitude = 12.6768, Description = "Helsingør" },
            new Address { Id = 6, Latitude = 56.0428, Longitude = 8.4593, Description = "Esbjerg" },
            new Address { Id = 7, Latitude = 55.7114, Longitude = 9.2505, Description = "Kolding" },
            new Address { Id = 8, Latitude = 55.4095, Longitude = 10.0241, Description = "Vejle" },
            new Address { Id = 9, Latitude = 55.4760, Longitude = 12.5254, Description = "Næstved" },
            new Address { Id = 10, Latitude = 55.8320, Longitude = 10.6093, Description = "Fredericia" },
        };


        [HttpGet("search")]
        [ProducesResponseType(typeof(List<Address>), StatusCodes.Status200OK)]
        public IActionResult SearchAddresses(double latitude, double longitude, double rangeInKm = 100)
        {
            var result = Addresses
                .Where(a => GetDistance(latitude, longitude, a.Latitude, a.Longitude) <= rangeInKm)
                .ToList();

            return Ok(result);
        }

        /// <summary>
        /// Calculate the distance between two points on the Earth
        /// </summary>
        /// <param name="lat1"></param>
        /// <param name="lon1"></param>
        /// <param name="lat2"></param>
        /// <param name="lon2"></param>
        /// <returns></returns>
        private double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        private double ToRadians(double angle) => angle * Math.PI / 180;
    }
}
