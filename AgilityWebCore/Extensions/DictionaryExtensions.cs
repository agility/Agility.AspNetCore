using Agility.Web.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Agility.Web.Extensions
{
    public static class DictionaryExtensions
    {

        public static bool TryGetEncoded(this Dictionary<string, AgilityRouteCacheItem> routes, string path, out AgilityRouteCacheItem routeItem)
        {
            bool found = false;

            if (routes.TryGetValue(path, out routeItem))
            {
                found = true;
            }
            else
            {
                string decoded = HttpUtility.UrlDecode(path);

                if (routes.TryGetValue(decoded, out routeItem))
				{
					found = true;
				}
				else
				{
					string encoded = HttpUtility.UrlPathEncode(path);

					if (routes.TryGetValue(encoded, out routeItem))
						found = true;
				}
                    

                
            }

            return found;
        }

        /// <summary>
        /// Purpose of this method is to ensure redirect origins like "/our%20culture" are matched when either "/our culture" or "/our%20culture" is passed in
        /// </summary>
        /// <param name="redirections"></param>
        /// <param name="path"></param>
        /// <param name="redirection"></param>
        /// <returns></returns>
        public static bool TryGetEscapedUri(this Dictionary<string, URLRedirection> redirections, string path, out URLRedirection redirection)
        {
            bool found = false;

            if (redirections.TryGetValue(path, out redirection))
            {
                found = true;
            }
            else
            {
                //if we get here we know that whatever the route is, hasn't been matched in the dict
                //if the url is well formed, we unescape
                //otherwise we escape
                string test = (Uri.IsWellFormedUriString(path, UriKind.RelativeOrAbsolute)) ? Uri.UnescapeDataString(path) : Uri.EscapeUriString(path);

                if (redirections.TryGetValue(test, out redirection))
                    found = true;

            }

            return found;
        }
    }
}
