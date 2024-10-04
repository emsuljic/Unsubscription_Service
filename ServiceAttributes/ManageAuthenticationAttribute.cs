using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace UnsubscribeService.ServiceAttributes
{
    public class ManageAuthenticationAttribute: ActionFilterAttribute
    {
        #region << Fields >>

        private IConfiguration _configuration;

        #endregion

        #region << Constructor >>

        public ManageAuthenticationAttribute(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #endregion

        #region << Public methods >>

        public override void OnActionExecuting(ActionExecutingContext actionContext)
        {
            string authHeader = actionContext?.HttpContext?.Request?.Headers["Authorization"];

            if(!string.IsNullOrWhiteSpace(authHeader))
            {
                string token = authHeader.Substring(6).Trim();
                string credentialString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                string[] creadentials = credentialString.Split(':');

                if (creadentials[0] == _configuration.GetValue<string>("Security:ManageCredentials:Username") &&
                    creadentials[1] == _configuration.GetValue<string>("Security:ManageCredentials:Password"))
                {
                    return;
                }
                else
                {
                    actionContext.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    actionContext.HttpContext.Response.CompleteAsync();
                    throw new UnauthorizedAccessException("Authorization is not valid");
                }
            }

            // If we get to this point, it means that request is missing BasicAuthentication
            actionContext.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            actionContext.HttpContext.Response.CompleteAsync();
            throw new UnauthorizedAccessException("Authorization is not valid");
        }

        #endregion
    }
}
