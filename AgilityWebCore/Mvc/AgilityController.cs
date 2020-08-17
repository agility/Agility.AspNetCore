using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Agility.Web.Objects;
using Agility.Web;
using System.IO;
using Agility.Web.Tracing;
using Agility.Web.HttpModules;
using Agility.Web.Routing;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

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
					Agility.Web.Tracing.WebTrace.WriteException(ex);
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
				
				Agility.Web.Tracing.WebTrace.WriteException(ex);
				AgilityHttpModule.HandleIntializationException(ex);
				//TODO: AgilityOutputCacheModule.TurnOffCacheInProgress();
				return new EmptyResult();
			}

					
        }
		

    }


	

	
}
