namespace APBD6.Exceptions
{
    public class OverlapException : Exception
    {
        public OverlapException() : base() { }
        public OverlapException(string message) : base(message) { }
        public OverlapException(string message, Exception innerException) : base(message, innerException) { }
    }
}