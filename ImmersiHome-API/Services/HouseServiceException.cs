namespace ImmersiHome_API.Services
{
    public class HouseServiceException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
