namespace BeGeneric.Backend.Services.BeGeneric.Exceptions
{
    public class GenericBackendSecurityException : Exception
    {
        public GenericBackendSecurityException(SecurityStatus securityStatus)
            : base()
        {
            this.SecurityStatus = securityStatus;
        }

        public GenericBackendSecurityException(SecurityStatus securityStatus, object errorObject)
            : base()
        {
            this.SecurityStatus = securityStatus;
            this.ErrorObject = errorObject;
        }

        public SecurityStatus SecurityStatus { get; set; }
        public object ErrorObject { get; set; }
    }
}
