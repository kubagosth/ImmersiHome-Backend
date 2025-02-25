using BoligPletten.Infrastructure.Models.Common;

namespace BoligPletten.Infrastructure.Models
{
    public class HouseEntity : IGenericEntity<int>
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; private set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public DateTime ListedDate { get; set; }

        public HouseEntity()
        {
            CreatedUtc = DateTime.UtcNow;
            ModifiedUtc = DateTime.UtcNow;
            IsDeleted = false;
        }

        public void MarkAsDeleted()
        {
            IsDeleted = true;
            ModifiedUtc = DateTime.UtcNow;
        }

        public void ModelUpdated()
        {
            ModifiedUtc = DateTime.UtcNow;
        }
    }
}
