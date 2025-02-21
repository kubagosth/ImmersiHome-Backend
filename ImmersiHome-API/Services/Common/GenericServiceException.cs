namespace ImmersiHome_API.Services.Common
{
    /// <summary>
    /// Generic exception class for service-level errors.
    /// </summary>
    public class GenericServiceException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
