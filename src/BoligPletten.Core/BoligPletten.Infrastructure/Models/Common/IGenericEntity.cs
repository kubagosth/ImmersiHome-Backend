namespace BoligPletten.Infrastructure.Models.Common
{
    public interface IGenericEntity<TKey> where TKey : struct, IEquatable<TKey>
    {
        TKey Id { get; set; }
        bool IsDeleted { get; set; }
        DateTime CreatedUtc { get; set; }
        DateTime ModifiedUtc { get; }

        void MarkAsDeleted();
        void ModelUpdated();
    }
}
