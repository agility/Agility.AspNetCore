using Agility.Web.Caching;
using Agility.Web.Configuration;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Threading.Tasks;

namespace Agility.Web.Mvc.ViewComponents
{
    public class AgilityBottomScripts : ViewComponent
    {
		public HtmlString InvokeAsync()
		{
			AgilityContext.HttpContext = HttpContext;

			StringBuilder sb = new StringBuilder();

			Agility.Web.Objects.AgilityPage p = AgilityContext.Page;
			if (p != null)
			{
				//inject the status panel scripts
				if (AgilityContext.IsPreview || Current.Settings.DevelopmentMode)
				{
					string script = StatusPanelEmitter.GetStatusPanelScriptNoJQuery();
					sb.AppendLine(script);
				}

				if (!string.IsNullOrEmpty(p.CustomAnalyticsScript))
				{
					string script = p.CustomAnalyticsScript;

					if (script.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) != -1)
					{
						string scriptBottomPage = script.Substring(script.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) + AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR.Length);
						if (!string.IsNullOrEmpty(scriptBottomPage))
						{
							sb.AppendLine(scriptBottomPage);
						}
					}
				}


				//add the Javascript tracking stuff
				if (p.IncludeInStatsTracking)
				{

					//global script
					if (!string.IsNullOrEmpty(AgilityContext.Domain.StatsTrackingScript))
					{
						string scriptTopGlobal = AgilityContext.Domain.StatsTrackingScript;

						if (scriptTopGlobal.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) != -1)
						{
							string scriptBottomGlobal = scriptTopGlobal.Substring(scriptTopGlobal.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) + AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR.Length);
							if (!string.IsNullOrEmpty(scriptBottomGlobal))
							{
								sb.AppendLine(scriptBottomGlobal);
							}
						}
					}

				}

				//handle dependencies on ouput cache...
				if (AgilityContext.OutputCacheKeys.Count > 0)
				{
					AgilityCache.AddResponseCacheDependancy(AgilityContext.OutputCacheKeys);
				}

			}
			else if (AgilityContext.IsTemplatePreview)
			{
				//template preview...
				string script = StatusPanelEmitter.GetStatusPanelScriptNoJQuery();
				sb.AppendLine(script);
			}


			return new HtmlString(sb.ToString());
			
		}

	}
}
