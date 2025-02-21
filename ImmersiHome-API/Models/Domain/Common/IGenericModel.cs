namespace ImmersiHome_API.Models.Domain.Common
{
    public interface IGenericModel<TKey> where TKey : struct, IEquatable<TKey>
    {
        TKey Id { get; set; }
    }
}
