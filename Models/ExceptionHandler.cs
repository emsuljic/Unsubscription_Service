namespace UnsubscribeService.Models
{
    public class ExceptionHandler : Exception
    {
        public ExceptionHandler(string message) : base(message) { }
    }
}
