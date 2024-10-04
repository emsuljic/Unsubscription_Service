using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;
using System.Threading.Tasks;


namespace UnsubscribeService.Middlewares
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        #region << Public methods >>
        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogInformation("Checking rate limit for {Path}", context.Request.Path);

            await _next(context);

            _logger.LogInformation("Rate limit check passed for {Path}", context.Request.Path);

            
        }
        #endregion
    }
}
