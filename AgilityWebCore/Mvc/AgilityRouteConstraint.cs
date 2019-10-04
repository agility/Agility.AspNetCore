using System;
using System.Linq;
using Agility.Web.Objects;
using Agility.Web.HttpModules;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace Agility.Web.Mvc
{
	public class AgilityRouteConstraint : IRouteConstraint
	{	
		
		public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
		{
			AgilityContext.HttpContext = httpContext;
//TODO: handle malicious request throttling (read)

            //ErrorTraceElement elem = Current.Settings.Trace.ErrorTraceTypes.FindDangerousRequestMatch();

            ////if the TraceElement exists and this path IS NOT the specified throttleRedirect
            //if (elem != null && !string.Equals(HttpContext.Current.Request.Path, elem.RequestThrottleRedirect, StringComparison.OrdinalIgnoreCase))
            //{
            //    bool throttle = AgilityHttpModule.HandleDangerousRequest(elem, HttpContext.Current.Request, HttpContext.Current.Response);

            //    if (throttle) return false;
            //}

			string controllerName = values["Controller"] as string;
			if (!string.Equals(controllerName, "Agility"))
			{
				return false;
			}

			if (! string.IsNullOrEmpty(httpContext.Request.Query["lang"]))
			{
				//if the languagecode is in the URL, redirect...
				return true;
			}

			string domain = httpContext.Request.Host.Value;
			string sitemapPath = values["sitemapPath"] as string;
			string languageCode = values["languageCode"] as string;

			 //ECMS_DOCUMENTS_KEY = "ecm
			 //ECMS_DOCUMENTS_KEY2 = "ec
			 //ECMS_RSS_KEY = "ecmsrss.a
			 //ECMS_ERRORS_KEY = "ecmser
			 //ECMS_EDITOR_CSS_KEY = "ec

			if (!string.IsNullOrEmpty(sitemapPath)
				&& (sitemapPath.IndexOf(AgilityHttpModule.ECMS_DOCUMENTS_KEY2, StringComparison.CurrentCultureIgnoreCase) != -1
					|| sitemapPath.IndexOf(AgilityHttpModule.ECMS_RSS_KEY, StringComparison.CurrentCultureIgnoreCase) != -1
					|| sitemapPath.IndexOf(AgilityHttpModule.ECMS_ERRORS_KEY, StringComparison.CurrentCultureIgnoreCase) != -1
					|| sitemapPath.IndexOf(AgilityHttpModule.ECMS_EDITOR_CSS_KEY, StringComparison.CurrentCultureIgnoreCase) != -1
					|| sitemapPath.IndexOf(AgilityHttpModule.DynamicCodePrepend, StringComparison.CurrentCultureIgnoreCase) != -1
					|| sitemapPath.IndexOf("TemplatePreview/", StringComparison.CurrentCultureIgnoreCase) >= 0
					|| (! string.IsNullOrEmpty(httpContext.Request.Query["agilitypreviewkey"]))
				)
			) 
			{
				return true;
			}

			try
			{				
				
				//test to see if the language code is in the URL...
				string pathWithOutSlash = sitemapPath;
				if (pathWithOutSlash == null) pathWithOutSlash = string.Empty;
				if (pathWithOutSlash.StartsWith("~/")) pathWithOutSlash = pathWithOutSlash.Substring(2);
				if (pathWithOutSlash.StartsWith("/")) pathWithOutSlash = pathWithOutSlash.Substring(1);

				string languageCodeTest = null;
				
				//strip out the language from the url (first folder path)
				int index = pathWithOutSlash.IndexOf("/");
				if (index > 0)
				{
					languageCodeTest = pathWithOutSlash.Substring(0, index);
					pathWithOutSlash = pathWithOutSlash.Substring(index + 1);
				}
				else
				{
					languageCodeTest = pathWithOutSlash;
					pathWithOutSlash = string.Empty;
				}

				AgilityContentServer.AgilityDomainConfiguration config = BaseCache.GetDomainConfiguration(AgilityContext.WebsiteName);
				//if (config == null) throw new Exception("Could not access the Domain Configuration.");

				if (config != null && config.Languages != null)
				{

					var lang = config.Languages.FirstOrDefault(l => string.Equals(l.LanguageCode, languageCodeTest, StringComparison.CurrentCultureIgnoreCase));
					if (lang != null)
					{
						//found the language code in the URL, switch up the values...
						sitemapPath = pathWithOutSlash;
						languageCode = languageCodeTest;
					} 
				}


			}
			catch (Exception ex)
			{
				Agility.Web.Tracing.WebTrace.WriteException(ex);
			}

			

			if (HttpModules.AgilityHttpModule.HandleChannelsAndRedirects(ref sitemapPath, languageCode))
			{
				//set a variable that says we can redirect now...
				httpContext.Items["Agility.Web.RequestPreviouslyHandledInRouteConstraint"] = true;
				return true;
			}

			AgilityPage agilityPage = Agility.Web.Data.GetPage(sitemapPath, languageCode);

			return (agilityPage != null || AgilityContext.IsResponseEnded);
			
		}

		
	}
}
