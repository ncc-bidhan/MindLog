namespace MindLog.Exceptions
{
    public class NotFoundException : Exception
    {
        public string ResourceType { get; }
        public object ResourceId { get; }

        public NotFoundException(string resourceType, object resourceId)
            : base($"{resourceType} with ID '{resourceId}' was not found.")
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }

        public NotFoundException(string message) : base(message)
        {
        }

        public NotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
