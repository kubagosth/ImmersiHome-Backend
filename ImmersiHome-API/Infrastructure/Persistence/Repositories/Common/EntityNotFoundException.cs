namespace ImmersiHome_API.Infrastructure.Persistence.Repositories.Common
{
    public class EntityNotFoundException(string message) : Exception(message)
    {
    }
}
