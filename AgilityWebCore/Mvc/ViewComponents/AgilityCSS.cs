using Agility.Web.Caching;
using Agility.Web.Configuration;
using Agility.Web.Objects;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Agility.Web.Mvc.ViewComponents
{
    public class AgilityCSS : ViewComponent
    {
		public Task<HtmlString> InvokeAsync()
		{
			AgilityContext.HttpContext = HttpContext;

			AgilityPage currentPage = AgilityContext.Page;

			StringBuilder sb = new StringBuilder(Environment.NewLine);

			if (currentPage != null)
			{

				//canonical link
				if (!string.IsNullOrEmpty(AgilityContext.CanonicalLink))
				{
					sb.AppendFormat("<link rel=\"canonical\" href=\"{0}\" />", AgilityContext.CanonicalLink);
					sb.Append(Environment.NewLine);
				}

				//set the page specific meta tags
				sb.Append("<meta name=\"description\" content=\"").Append(currentPage.MetaTags).Append("\" />");
				sb.Append(Environment.NewLine);

				if (!string.IsNullOrEmpty(currentPage.MetaKeyWords))
				{
					sb.Append("<meta name=\"keywords\" content=\"").Append(currentPage.MetaKeyWords).Append("\" />");
					sb.Append(Environment.NewLine);
				}

				string rawTags = currentPage.MetaTagsRaw;
				if (rawTags == null) rawTags = string.Empty;

				if (!string.IsNullOrEmpty(rawTags))
				{
					sb.Append(Agility.Web.Util.Url.ResolveTildaUrlsInHtml(rawTags));
					sb.Append(Environment.NewLine);
				}

				if (!string.IsNullOrEmpty(AgilityContext.TwitterCardSite))
				{
					if (rawTags.IndexOf("<meta name=\"twitter:site\"", StringComparison.CurrentCultureIgnoreCase) == -1)
					{
						string site = AgilityContext.TwitterCardSite;
						if (!site.StartsWith("@")) site = string.Format("@{0}", site);

						sb.AppendFormat("<meta name=\"twitter:site\" value=\"{0}\" />", site);

					}

					string twitterCardType = "summary";
					if (!string.IsNullOrEmpty(AgilityContext.FeaturedImageUrl))
					{
						twitterCardType = "summary_large_image";
						sb.AppendFormat("<meta name=\"twitter:image:src\" content=\"{0}\" />", AgilityContext.FeaturedImageUrl);
					}

					sb.AppendFormat("<meta name=\"twitter:card\" content=\"{0}\" />", twitterCardType);
					sb.AppendFormat("<meta name=\"twitter:title\" content=\"{0}\" />", currentPage.Title);
					sb.AppendFormat("<meta name=\"twitter:description\" content=\"{0}\" />", currentPage.MetaTags);

				}

				if (Current.Settings.OutputOpenGraph)
				{

					sb.AppendFormat("<meta name=\"og:title\" content=\"{0}\" />", currentPage.Title);
					sb.AppendFormat("<meta name=\"og:description\" content=\"{0}\" />", currentPage.MetaTags);
					if (!string.IsNullOrEmpty(AgilityContext.FeaturedImageUrl))
					{
						sb.AppendFormat("<meta name=\"og:image\" content=\"{0}\" />", AgilityContext.FeaturedImageUrl);
						sb.Append(Environment.NewLine);
					}


				}

				//content language
				sb.Append("<meta http-equiv='content-language' content='").Append(currentPage.LanguageCode).Append("'/>");
				sb.Append(Environment.NewLine);

				//default css...
				string globalCss = AgilityContext.Domain.GlobalCss;
				if (!string.IsNullOrEmpty(globalCss))
				{
					string url = AgilityHelpers.ResolveUrl(string.Format("~/{0}/global.css",
																		 Agility.Web.HttpModules.AgilityHttpModule.ECMS_EDITOR_CSS_KEY));
					sb.AppendFormat("<link rel=\"stylesheet\" type=\"text/css\" href=\"{0}\" />", url);
				}


				//set the custom agility meta tags

				sb.Append("<meta name=\"generator\" content=\"Agility CMS\" />");
				sb.Append(Environment.NewLine);

				sb.AppendFormat("<meta name=\"agility_timestamp\" content=\"{0:yyyy/MM/dd hh:mm:ss tt}\" />", DateTime.Now);

				sb.AppendFormat("<meta name=\"agility_attributes\" content=\"Mode={0}, IsPreview={1}, Language={2}, Machine={3}, CustomOutputCache={4}\" />",
					AgilityContext.CurrentMode,
					AgilityContext.IsPreview,
					AgilityContext.LanguageCode,
					Environment.MachineName,
					AgilityCache.UseAgilityOutputCache);
				sb.Append(Environment.NewLine);

				


			}

			//add the StatusPanelEmitter if in preview mode, development mode, or edit in place				
			if (AgilityContext.IsPreview
				|| Current.Settings.DevelopmentMode
				|| AgilityContext.IsTemplatePreview)
			{

				sb.Append(StatusPanelEmitter.GetStatusPanelCssOnly());

			}
			

			return Task.FromResult(new HtmlString(sb.ToString()));
		}

	}
}
