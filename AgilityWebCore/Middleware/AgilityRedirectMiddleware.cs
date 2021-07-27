using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Agility.Web.Middleware
{
    public class AgilityRedirectMiddleware
    {
        private readonly RequestDelegate _next;

        public AgilityRedirectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {

            if (context.Request.Path.Value != null)
            {
                var path = context.Request.Path.Value.TrimStart('/');
                if (CheckIfDifferentPageNameInLocale(context, path)) return;
            }

            await _next(context);
        }

        private static bool CheckIfDifferentPageNameInLocale(HttpContext context, string path)
        {
            var index = path?.IndexOf("/", StringComparison.Ordinal);
            if (!(index > -1)) return false;

            var locale = path?.Substring(0, index.Value);
            var pageWithoutLocale = path?.Replace(locale, string.Empty);
            var page = Data.GetPage(pageWithoutLocale, locale);

            if (page != null) return false;

            page = Data.GetPage(pageWithoutLocale, AgilityContext.LanguageCode);

            if (locale == AgilityContext.LanguageCode || page == null) return false;

            var sitemap = Data.GetSitemap(locale);
            var node = sitemap.SitemapXml.SelectSingleNode($"//SiteNode[@picID='{page.ID}']");
            var navigateUrl = node?.Attributes?.GetNamedItem("NavigateURL")?.Value;

            if (string.IsNullOrEmpty(navigateUrl)) return false;

            var redirectUrl =
                $"{context.Request.Scheme}://{context.Request.Host}/{locale}/{navigateUrl?.TrimStart('~').TrimStart('/')}";

            AgilityContext.HttpContext.Response.Clear();
            AgilityContext.HttpContext.Response.Headers["Location"] = redirectUrl;
            AgilityContext.HttpContext.Response.StatusCode = 302;

            return true;
        }
    }
}
