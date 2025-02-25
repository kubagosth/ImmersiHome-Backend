namespace BoligPletten.Infrastructure.Repositories.Common
{
    public class EntityNotFoundException(string message) : Exception(message)
    {
    }
}
