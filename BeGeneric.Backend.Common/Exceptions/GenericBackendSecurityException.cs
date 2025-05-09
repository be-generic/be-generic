namespace BeGeneric.Backend.Common.Exceptions;

public class GenericBackendSecurityException : Exception
{
    public GenericBackendSecurityException(SecurityStatus securityStatus)
        : base()
    {
        SecurityStatus = securityStatus;
    }

    public GenericBackendSecurityException(SecurityStatus securityStatus, object errorObject)
        : base()
    {
        SecurityStatus = securityStatus;
        ErrorObject = errorObject;
    }

    public SecurityStatus SecurityStatus { get; set; }
    public object ErrorObject { get; set; }
}
