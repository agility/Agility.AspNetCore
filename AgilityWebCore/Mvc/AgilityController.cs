using System;
using Agility.Web.Tracing;
using Agility.Web.HttpModules;
using Microsoft.AspNetCore.Mvc;

namespace Agility.Web.Mvc
{
    public class AgilityController : Controller
    {

        public ActionResult RenderPage()
        {
			try
			{

				AgilityContext.HttpContext = this.HttpContext;

				string controllerName = RouteData.Values["Controller"] as string;


				if (AgilityContext.IsResponseEnded)
				{
					return new EmptyResult();
				}

				string redirectUrl = null;

				try
				{
					AgilityHttpModule.ParseLanguageCode(RouteData, ref redirectUrl);
				}
				catch (Exception ex)
				{
					WebTrace.WriteException(ex);
				}

				if (AgilityContext.IsResponseEnded)
				{
					return new EmptyResult();
				}

				if (!string.IsNullOrEmpty(redirectUrl))
				{
					
					if (redirectUrl.StartsWith("~/")) redirectUrl = redirectUrl.Substring(1);
					return new RedirectResult(redirectUrl, false);

					//TODO: AgilityOutputCacheModule.TurnOffCacheInProgress();						
				}

				//now process the url...
				string domain = HttpContext.Request.Host.Host;
				string sitemapPath = RouteData.Values["sitemapPath"] as string;
				string languageCode = RouteData.Values["languageCode"] as string;

				
				return this.AgilityView(sitemapPath, languageCode, ViewData);
			}
			catch (Exception ex)
			{
				
				WebTrace.WriteException(ex);
				AgilityHttpModule.HandleIntializationException(ex);
				//TODO: AgilityOutputCacheModule.TurnOffCacheInProgress();
				return new EmptyResult();
			}

					
        }
		

    }


	

	
}
