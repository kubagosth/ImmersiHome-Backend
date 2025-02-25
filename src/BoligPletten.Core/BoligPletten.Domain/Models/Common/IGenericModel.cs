namespace BoligPletten.Domain.Models.Common
{
    public interface IGenericModel<TKey> where TKey : struct, IEquatable<TKey>
    {
        TKey Id { get; set; }
    }
}
