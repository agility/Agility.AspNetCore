using Agility.Web.Configuration;
using Agility.Web.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace Agility.Web.Caching
{
    public class AgilityResponseCacheMiddleware
    {

        private readonly RequestDelegate _next;

        public AgilityResponseCacheMiddleware(RequestDelegate next)
        {
            _next = next;
        }



        public async Task Invoke(HttpContext context)
        {



            string key = Data.GetAgilityVaryByCustomString(context);

            var cachedPage = AgilityCache.Get<AgilityOutputCacheResponse>(key);


            if (cachedPage != null)
            {
                if (CheckForNotModified(context, cachedPage)) return;

                await WriteCachedPage(cachedPage, context.Response, "hit");
                return;
            }

			if (AgilityContext.IsResponseEnded)
			{
				return;
			}


            ApplyClientHeaders(context, key);

            if (IsCacheable(context))
            {

                AgilityOutputCacheResponse page = await CaptureResponse(context);

                if (page != null)
                {
                    TimeSpan serverCacheDuration = GetOutputCacheExpiration();

                    var depKeys = AgilityContext.OutputCacheDependencies;

                    CacheDependency dep = new CacheDependency(cacheKeys: depKeys.ToArray());

                    AgilityCache.Set(key, page, serverCacheDuration, dep);
                }
            }
            else
            {
                await _next.Invoke(context);
            }
        }

        public async Task WriteCachedPage(AgilityOutputCacheResponse page, HttpResponse response, string hitStatus)
        {
            response.StatusCode = page.StatusCode;

            foreach (string key in page.Headers.Keys)
            {
                if (string.Equals(key, "Transfer-Encoding", StringComparison.CurrentCultureIgnoreCase)) continue;
                string value = page.Headers[key];
                response.Headers[key] = value;
            }


            response.Headers["X-AgilityCache"] = hitStatus;
            response.Headers["X-MachineName"] = Environment.MachineName;

            if (!string.IsNullOrWhiteSpace(page.ETag))
            {
                response.Headers["ETag"] = page.ETag;
            }


            if (page.Body != null && page.Body.Length > 0)
            {
                try
                {

                    await response.Body.WriteAsync(page.Body, 0, page.Body.Length);
                }
                catch
                {
                    response.HttpContext.Abort();
                }
            }

            return;
        }


        /// <summary>
        ///Returns true if the content wasn't modified and has outputted a 304
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private bool CheckForNotModified(HttpContext context, AgilityOutputCacheResponse pageResponse)
        {

            string modSinceStr = context.Request.Headers["If-Modified-Since"];
            if (string.IsNullOrWhiteSpace(modSinceStr)) return false;

            string etagMatch = context.Request.Headers["If-None-Match"];

            DateTimeOffset dtIfModifiedSince = DateTimeOffset.MinValue;
            if (DateTimeOffset.TryParse(modSinceStr, out dtIfModifiedSince))
            {
                TimeSpan expiration = GetOutputCacheExpiration();
                TimeSpan timeSinceCache = DateTimeOffset.UtcNow - dtIfModifiedSince;

                if (timeSinceCache < expiration
                    && pageResponse.ETag == etagMatch)
                {
                    //if the page has NOT expired and the etags
                    //return a 304...
                    context.Response.StatusCode = 304;

                    var typedHeaders = context.Response.GetTypedHeaders();


                    typedHeaders.CacheControl = new CacheControlHeaderValue
                    {
                        Public = true,
                        MaxAge = expiration
                    };

                    typedHeaders.Expires = dtIfModifiedSince + expiration;
                    typedHeaders.LastModified = dtIfModifiedSince;

                    return true;
                }
            }


            return false;
        }

        private static AgilityPage GetPage(HttpContext context)
        {
            AgilityPage _page = context.Items["Agility.Web.AgilityContext.Page"] as AgilityPage;
            if (_page == null)
            {

                string url = AgilityContext.HttpContext.Request.Path;

                url = HttpUtility.UrlPathEncode(url);

                _page = Agility.Web.Data.GetPage(url);

                context.Items["Agility.Web.AgilityContext.Page"] = _page;
            }
            return _page;
        }

        private bool IsCacheable(HttpContext context)
        {

			if (! string.IsNullOrWhiteSpace(context.Request.Query["agilitypreviewkey"])
				|| AgilityContext.IsPreview
				|| AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging
				|| !AgilityContext.Domain.EnableOutputCache)
			{
				return false;
			}

            var agilityPage = GetPage(context);

            if (agilityPage == null                
                || agilityPage.ExcludeFromOutputCache)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void ApplyClientHeaders(HttpContext context, string cacheKey)
        {


			context.Response.OnStarting(() =>
            {

                TimeSpan expiration = GetOutputCacheExpiration();


				if (!IsCacheable(context))
                {

                    context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue()
                    {
                        NoCache = true,
                        NoStore = true,
                        MustRevalidate = true
                    };
                    context.Response.Headers["Expires"] = "0";
                }
                else
                {
                    var typedHeaders = context.Response.GetTypedHeaders();


                    typedHeaders.CacheControl = new CacheControlHeaderValue
                    {
                        Public = true,
                        MaxAge = expiration
                    };

                    typedHeaders.Expires = DateTimeOffset.UtcNow + expiration;
                    typedHeaders.LastModified = DateTimeOffset.UtcNow;

                    //TODO: add an etag here based on the cache key
                }


                return Task.CompletedTask;
            });
        }

        private static TimeSpan GetOutputCacheExpiration()
        {
            TimeSpan expiration = TimeSpan.FromMinutes(Current.Settings.OutputCacheDefaultTimeoutMinutes);

            if (AgilityContext.Domain?.OutputCacheTimeoutSeconds > 0)
            {
                expiration = TimeSpan.FromSeconds(AgilityContext.Domain.OutputCacheTimeoutSeconds);
            }

            return expiration;
        }

        private async Task<AgilityOutputCacheResponse> CaptureResponse(HttpContext context)
        {
            var responseStream = context.Response.Body;

            AgilityOutputCacheResponse page = new AgilityOutputCacheResponse();

            using (var buffer = new MemoryStream())
            {
                try
                {

                    context.Response.Body = buffer;

                    await _next.Invoke(context);
                }
                finally
                {
                    context.Response.Body = responseStream;
                }

                if (buffer.Length == 0) return null;

                var bytes = buffer.ToArray(); // you could gzip here

                responseStream.Write(bytes, 0, bytes.Length);

                return new AgilityOutputCacheResponse(context, bytes);
            }
        }



    }

}
