using System.Net;
using UnsubscribeService.Models;

namespace UnsubscribeService.Middlewares
{
    public class ExceptionHandlingMiddleware
    {

        #region << Fields >>

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        #endregion

        #region << Constructor >>
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        #endregion

        #region << Public methods >>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ExceptionHandler ex)
            {
                _logger.LogError(ex, "An expected error occurred");
                await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred");
                await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
            }
        }
        #endregion

        #region << Private methods >>
        private Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new { error = message };
            return context.Response.WriteAsJsonAsync(response);
        }
        #endregion
    }
}
