using Agility.Web.Requirements;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;

namespace Agility.Web.AuthorizationHandlers
{
    internal class CorrectWebsiteAuthorizationHandler : AuthorizationHandler<CorrectWebsiteRequirement>
    {

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CorrectWebsiteRequirement requirement)
        {
            if(context == null)
                throw new ArgumentNullException(nameof(context));

            if(requirement == null)
                throw new ArgumentNullException(nameof(requirement));

            var authFilterCtx = (Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext)context.Resource;
            var httpContext = authFilterCtx.HttpContext;

            if (!httpContext.Request.Headers.ContainsKey("WebsiteName") ||
                !httpContext.Request.Headers.ContainsKey("SecurityKey"))
            {
                return Task.CompletedTask;
            }

            string websiteName = httpContext.Request.Headers["WebsiteName"];
            string securityKey = httpContext.Request.Headers["SecurityKey"];

            if (string.IsNullOrEmpty(websiteName) ||
                string.IsNullOrEmpty(securityKey))
            {
                return Task.CompletedTask;
            }

            if (websiteName == requirement.WebsiteName && securityKey == requirement.SecurityKey)
            {
                // Mark the requirement as satisfied
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
