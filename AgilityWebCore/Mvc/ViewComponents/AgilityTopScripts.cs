using Agility.Web.Objects;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agility.Web.Mvc.ViewComponents
{
    public class AgilityTopScripts : ViewComponent
    {
		public HtmlString InvokeAsync()
		{
			AgilityContext.HttpContext = HttpContext;

			AgilityPage currentPage = AgilityContext.Page;

			if (currentPage == null)
			{
				return null;
			}

			StringBuilder sb = new StringBuilder(Environment.NewLine);

			//output the id of any page a/b test experiments



			var experiments = BaseCache.GetExperiments(AgilityContext.WebsiteName);
			var experiment = experiments.GetForPage(currentPage.ID);
			if (experiment != null)
			{

				if (experiment.Variants != null
					&& experiment.Variants.Any(v => !string.IsNullOrWhiteSpace(v.URL)))
				{
					//PAGE REDIRECT EXPERIMENTS ONLY
					AgilityContext.ExperimentKeys.Add(experiment.Key);


					sb.AppendLine("<script type='text/javascript'>");

					//get the winner
					if (experiment.WinningVariant != null)
					{
						sb.AppendFormat("window.AgilityExperimentWinningVariant = {{ experiment: '{0}' variant: {1}, url: '{2}' }};",
							experiment.Key,
							experiment.WinningVariant.ID,
							AgilityHelpers.ResolveUrl(experiment.WinningVariant.URL)
						);
					}
					else
					{
						sb.Append("window.AgilityExperimentVariants = [");
						foreach (var v in experiment.Variants)
						{
							sb.AppendFormat("{{ experiment: '{0}', variant: {1}, url: '{2}' }},",
								experiment.Key,
								v.ID,
								AgilityHelpers.ResolveUrl(v.URL)
							);
						}

						sb.AppendLine("];");
						sb.AppendLine("</script>");
					}
				}
			}
			else if (currentPage.ServerPage.ExperimentIDs != null)
			{
				foreach (int exId in currentPage.ServerPage.ExperimentIDs)
				{
					experiment = experiments.GetExperiment(exId);
					if (experiment != null) AgilityContext.ExperimentKeys.Add(experiment.Key);
				}

			}


			//add the Javascript tracking stuff
			if (currentPage.IncludeInStatsTracking)
			{

				//global script
				if (!string.IsNullOrEmpty(AgilityContext.Domain.StatsTrackingScript))
				{
					var scriptTopGlobal = AgilityContext.Domain.StatsTrackingScript;

					if (scriptTopGlobal.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) != -1)
					{
						scriptTopGlobal = scriptTopGlobal.Substring(0, scriptTopGlobal.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR));
					}

					if (!string.IsNullOrEmpty(scriptTopGlobal))
					{
						sb.Append(scriptTopGlobal);
						sb.Append(Environment.NewLine);
					}
				}
			}

			string scriptTopPage = null;

			//custom script for this page
			if (!string.IsNullOrEmpty(currentPage.CustomAnalyticsScript))
			{

				scriptTopPage = currentPage.CustomAnalyticsScript;

				if (scriptTopPage.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR) != -1)
				{
					scriptTopPage = scriptTopPage.Substring(0, scriptTopPage.IndexOf(AgilityHelpers.GLOBAL_SCRIPT_SEPARATOR));
				}

				if (!string.IsNullOrEmpty(scriptTopPage))
				{
					sb.Append(scriptTopPage);
					sb.Append(Environment.NewLine);
				}
			}

			return new HtmlString(sb.ToString());
			
		}

	}
}
