using ImmersiHome_API.Models.Domain.Common;

namespace ImmersiHome_API.Models.Domain
{
    public class HouseModel : IGenericModel<int>
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public DateTime ListedDate { get; set; }
    }
}
