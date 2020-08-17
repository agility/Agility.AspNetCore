using Agility.Web.Caching;
using Agility.Web.Configuration;
using Agility.Web.Objects;
using Agility.Web.Providers;
using Agility.Web.Util;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Agility.Web.Mvc
{
    public static class AgilityHelpers
    {

        private static object _typeLockObject = new object();
        internal const string GLOBAL_SCRIPT_SEPARATOR = "###AGILITY###";

        public static HtmlString RenderAgilityCss(this IHtmlHelper helper)
        {
            AgilityContext.HttpContext = helper.ViewContext.HttpContext;
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
                if (AgilityContext.Domain != null) globalCss = AgilityContext.Domain.GlobalCss;
                if (!string.IsNullOrEmpty(globalCss))
                {
                    string url = AgilityHelpers.ResolveUrl(helper,
                                                           string.Format("~/{0}/global.css",
                                                                         Agility.Web.HttpModules.AgilityHttpModule.ECMS_EDITOR_CSS_KEY));
                    sb.AppendFormat("<link rel=\"stylesheet\" type=\"text/css\" href=\"{0}\" />", url);
                }


                //set the custom agility meta tags

                sb.Append("<meta name=\"generator\" content=\"Agility CMS\" />");
                sb.Append(Environment.NewLine);

                //Timestamp is now in not modified since
                // sb.AppendFormat("<meta name=\"agility_timestamp\" content=\"{0:yyyy/MM/dd hh:mm:ss tt}\" />", DateTime.Now);

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

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// Render only the Agility Preview Bar CSS.  This should ONLY be used if the Agility CSS and Meta information is being outputted separately.
        /// </summary>
        /// <param name="helper"></param>
        public static HtmlString RenderAgilityPreviewBarCss(this IHtmlHelper helper)
        {

            //add the StatusPanelEmitter if in preview mode, development mode, or edit in place				
            if (AgilityContext.IsPreview
                || Current.Settings.DevelopmentMode
                || AgilityContext.IsTemplatePreview)
            {
                return new HtmlString(StatusPanelEmitter.GetStatusPanelCss());
            }
            else 
            {
                return new HtmlString("");
            }
        }


        /// <summary>
        /// Output the top scripts defined in the Page and Globally
        /// </summary>
        /// <param name="helper"></param>
        public static HtmlString RenderAgilityTopScripts(this IHtmlHelper helper)
        {

            AgilityPage currentPage = AgilityContext.Page;

            if (currentPage == null)
            {
                return new HtmlString("");
            }

            StringBuilder sb = new StringBuilder(Environment.NewLine);

            string scriptTopGlobal;
            string scriptBottomGlobal;

            string scriptBottomPage = null;

            //output the id of any page a/b test experiments



            var experiments = BaseCache.GetExperiments(AgilityContext.WebsiteName);
            var experiment = experiments.GetForPage(currentPage.ID);
            if (experiment != null)
            {

#if NET35
                if (experiment.Variants != null 
					&& experiment.Variants.Any(v => ! string.IsNullOrEmpty(v.URL)))
#else
                if (experiment.Variants != null
                    && experiment.Variants.Any(v => !string.IsNullOrWhiteSpace(v.URL)))
#endif
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
                            ResolveUrl(helper, experiment.WinningVariant.URL)
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
                                ResolveUrl(helper, v.URL)
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
                    scriptTopGlobal = AgilityContext.Domain.StatsTrackingScript;

                    if (scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                    {
                        scriptBottomGlobal = scriptTopGlobal.Substring(scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                        scriptTopGlobal = scriptTopGlobal.Substring(0, scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR));
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

                if (scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                {
                    scriptBottomPage = scriptTopPage.Substring(scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                    scriptTopPage = scriptTopPage.Substring(0, scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR));
                }

                if (!string.IsNullOrEmpty(scriptTopPage))
                {
                    sb.Append(scriptTopPage);
                    sb.Append(Environment.NewLine);
                }
            }

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// Output the bottom scripts defined in the Page and Globally, as well as the Form Builder scripts if the Form module was used on this page.
        /// </summary>
        /// <param name="helper"></param>
        public static HtmlString RenderAgilityBottomScripts(this IHtmlHelper helper)
        {

            AgilityPage currentPage = AgilityContext.Page;

            if (currentPage == null)
            {
                return new HtmlString("");
            }

            StringBuilder sb = new StringBuilder(Environment.NewLine);

            string scriptTopGlobal;
            string scriptBottomGlobal;


            string scriptBottomPage = null;

            //add the Javascript tracking stuff
            if (currentPage.IncludeInStatsTracking)
            {

                //global script
                if (!string.IsNullOrEmpty(AgilityContext.Domain.StatsTrackingScript))
                {
                    scriptTopGlobal = AgilityContext.Domain.StatsTrackingScript;
                    scriptBottomGlobal = string.Empty;

                    if (scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                    {
                        scriptBottomGlobal = scriptTopGlobal.Substring(scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                        scriptTopGlobal = scriptTopGlobal.Substring(0, scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR));
                    }

                    if (!string.IsNullOrEmpty(scriptBottomGlobal))
                    {
                        sb.Append(scriptBottomGlobal);
                        sb.Append(Environment.NewLine);
                    }
                }
            }

            string scriptTopPage = null;

            //custom script for this page
            if (!string.IsNullOrEmpty(currentPage.CustomAnalyticsScript))
            {

                scriptTopPage = currentPage.CustomAnalyticsScript;

                if (scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                {
                    scriptBottomPage = scriptTopPage.Substring(scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                    scriptTopPage = scriptTopPage.Substring(0, scriptTopPage.IndexOf(GLOBAL_SCRIPT_SEPARATOR));
                }

                if (!string.IsNullOrEmpty(scriptBottomPage))
                {
                    sb.Append(scriptBottomPage);
                    sb.Append(Environment.NewLine);
                }
            }

            return new HtmlString(sb.ToString());
        }


        /// <summary>
        /// Output just the scripts for the Agility preview and development bar.
        /// </summary>
        /// <param name="helper"></param>
        public static HtmlString RenderAgilityPreviewBarScripts(this IHtmlHelper helper)
        {

            Agility.Web.Objects.AgilityPage p = AgilityContext.Page;
            if (p != null)
            {
                StringBuilder sb = new StringBuilder();

                //inject the status panel scripts
                if (AgilityContext.IsPreview || Current.Settings.DevelopmentMode)
                {
                    string script = StatusPanelEmitter.GetStatusPanelScriptNoJQuery();
                    sb.Append(script);
                }


                //handle dependencies on ouput cache...
                if (AgilityContext.OutputCacheKeys.Count > 0)
                {
                    AgilityCache.AddResponseCacheDependancy(AgilityContext.OutputCacheKeys);
                }

                return new HtmlString(sb.ToString());

            }
            else if (AgilityContext.IsTemplatePreview)
            {
                //template preview...
                string script = StatusPanelEmitter.GetStatusPanelScriptNoJQuery();

                return new HtmlString(script);
            }
            else
            {
                return new HtmlString("");
            }
        }

        /// <summary>
        /// Render the bottom scripts and the Preview bar scripts, including a jQuery reference.
        /// </summary>
        /// <param name="helper"></param>
        public static HtmlString RenderAgilityScripts(this IHtmlHelper helper)
        {
            StringBuilder sb = new StringBuilder();

            Agility.Web.Objects.AgilityPage p = AgilityContext.Page;
            if (p != null)
            {
                //inject the status panel scripts
                if (AgilityContext.IsPreview || Current.Settings.DevelopmentMode)
                {
                    string script = StatusPanelEmitter.GetStatusPanelScript();
                    sb.Append(script);
                    // helper.ViewContext.Writer.Write(script);
                }

                if (!string.IsNullOrEmpty(p.CustomAnalyticsScript))
                {
                    string script = p.CustomAnalyticsScript;

                    if (script.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                    {
                        string scriptBottomPage = script.Substring(script.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                        if (!string.IsNullOrEmpty(scriptBottomPage))
                        {
                            sb.Append(scriptBottomPage);
                            // helper.ViewContext.Writer.Write(scriptBottomPage);
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

                        if (scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) != -1)
                        {
                            string scriptBottomGlobal = scriptTopGlobal.Substring(scriptTopGlobal.IndexOf(GLOBAL_SCRIPT_SEPARATOR) + GLOBAL_SCRIPT_SEPARATOR.Length);
                            if (!string.IsNullOrEmpty(scriptBottomGlobal))
                            {
                                sb.Append(scriptBottomGlobal);
                                // helper.ViewContext.Writer.Write(scriptBottomGlobal);
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
                string script = StatusPanelEmitter.GetStatusPanelScript();
                
                sb.Append(script);
                // helper.ViewContext.Writer.Write(script);
            }

            return new HtmlString(sb.ToString());

        }

        private static Random random = new Random();

        internal static string RenderModuleHtml(List<ModuleRender> renders, ContentSection cs)
        {

            using (StringWriter writer = new StringWriter())
            {

                if (renders == null || renders.Count == 0)
                {
                    writer.Write(string.Format("<!-- No Output from Module {0} -->", cs.ContentReferenceName));
                    return writer.ToString();
                }


                //wait for the renders..
                var renderTasks = renders.Where(r => r.RenderTask != null).Select(r => r.RenderTask).ToArray();
                if (renderTasks.Length > 0)
                {
                    Task.WaitAll(renderTasks);
                }



                AgilityContentServer.AgilityExperiment experiment = null;

                if (cs.ExperimentID > 0)
                {
                    var lst = BaseCache.GetExperiments(AgilityContext.WebsiteName);
                    experiment = lst.GetExperiment(cs.ExperimentID);
                }

                HtmlEncoder htmlEncoder = HtmlEncoder.Default;

                if (experiment == null)
                {
                    foreach (var render in renders)
                    {
						if (render != null)
						{
							try
							{
								render.PreRenderedContent.WriteTo(writer, htmlEncoder);
							} catch (Exception ex)
							{
								Agility.Web.Tracing.WebTrace.WriteException(ex, $"Error rendering Module {render.ContentReferenceName}");
							}
						}
                    }

                    return writer.ToString();
                }
                else
                {
                    AgilityContext.ExperimentKeys.Add(experiment.Key);

                    if (experiment.Variants.Length > 0)
                    {

                        //MODULE AB TEST WITH VARIANTS
                        writer.Write(string.Format("<div id=\"agility-abtest-container-{0}\" data-agility-experiment=\"{0}\" data-agility-variants=\"{1}\"></div>", experiment.Key, experiment.Variants.Length));


                        int index = 0;
                        foreach (var render in renders)
                        {
                            var html = render.PreRenderedContent;
                            int moduleContentItemID = render.ContentID;

                            //remove this content id from the "auto" list of content ids on this page...
                            AgilityContext.LoadedContentItemIDs.Remove(moduleContentItemID);

                            int variantID = 0;

                            if (render.Variant != null)
                            {
                                variantID = render.Variant.ID;
                            }

                            //wrap the variant in a script						
                            writer.Write(string.Format("<script id=\"agility-abtest-variant-{0}-{1}\" type=\"text/html\" data-agility-content-id=\"{2}\" data-agility-experiment=\"{1}\" data-agility-variant=\"{3}\">",
                                                experiment.Key,
                                                index,
                                                moduleContentItemID,
                                                variantID)
                            );
                            html.WriteTo(writer, htmlEncoder);

                            writer.Write("</script>");


                            if (render.Variant == null)
                            {
                                //output the "control" (default) in a noscript for SEO / accessibility
                                writer.Write("<noscript>");
                                html.WriteTo(writer, htmlEncoder);
                                writer.Write("</noscript>");
                            }

                            index++;
                        }

                    }
                    else
                    {
                        foreach (var render in renders)
                        {
                            var html = render.PreRenderedContent;
                            int moduleContentItemID = render.ContentID;

                            //CONTENT EXPERIMENT - there were only 1 render...
                            //wrap the module in a script and a noscript tag...
                            writer.Write(string.Format("<div id=\"agility-experiment-container-{1}\" data-agility-content-id=\"{0}\"  data-agility-experiment=\"{1}\"></div>", moduleContentItemID, experiment.Key));
                            writer.Write(string.Format("<script id=\"agility-experiment-root-{1}\" type=\"text/html\" data-agility-content-id=\"{0}\" data-agility-experiment=\"{1}\">", moduleContentItemID, experiment.Key));
                            html.WriteTo(writer, htmlEncoder);
                            writer.Write("</script>");

                            writer.Write("<noscript>");
                            html.WriteTo(writer, htmlEncoder);
                            writer.Write("</noscript>");
                        }
                    }

                }

                return writer.ToString();
            }

        }


        public static string AgilityTemplate(this IHtmlHelper helper, string referenceName)
        {
            return helper.AgilityTemplate(referenceName, null);
        }

        public static string AgilityTemplate(this IHtmlHelper helper, string referenceName, object model)
        {

            string contentReferenceName = AgilityDynamicCodeFile.REFNAME_AgilityGlobalCodeTemplates;

            int versionID = 0;
            if (Current.Settings.DevelopmentMode || AgilityContext.CurrentMode == Enum.Mode.Staging)
            {
                //path is like this: DynamicAgilityCode/[ContentReferenceName]/[ItemReferenceName].ext
                string tempPath = string.Format("~/Views/DynamicAgilityCode/{0}/{1}.cshtml", contentReferenceName, referenceName);
                DataRow row = AgilityDynamicCodeFile.GetCodeItem(tempPath);

				if (!int.TryParse($"{row["VersionID"]}", out versionID)) versionID = -1;

            }


            string templatePath = string.Format("~/Views/{0}/DynamicAgilityCode/{1}/{2}.cshtml", versionID, contentReferenceName, referenceName);
            if (string.IsNullOrEmpty(templatePath)) return string.Empty;

            if (model != null)
            {
                helper.RenderPartial(templatePath, model);
            }
            else
            {
                helper.RenderPartial(templatePath);
            }

            return string.Empty;
        }


        public static string AgilityTemplatePath(this IHtmlHelper helper, string referenceName)
        {

            return helper.AgilityTemplatePath(referenceName);
        }

        public static HtmlString AgilityCSS(this IHtmlHelper helper, params string[] referenceNames)
        {

            return new HtmlString(Html.AgilityCSS(referenceNames));
        }

        public static HtmlString AgilityJavascript(this IHtmlHelper helper, params string[] referenceNames)
        {
            return new HtmlString(Agility.Web.Html.AgilityJavascript(referenceNames));
        }




        public static string ResolveUrl(this IHtmlHelper helper, string relativeUrl)
        {
            return ResolveUrl(relativeUrl);
        }
        public static string ResolveUrl(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return string.Empty;
            //if (relativeUrl.StartsWith("~/")
            //	&& !relativeUrl.StartsWith("~/" + AgilityContext.LanguageCode, StringComparison.InvariantCultureIgnoreCase)
            //	&& AgilityContext.IsUsingLanguageModule
            //	&& relativeUrl.IndexOf(".aspx", StringComparison.InvariantCultureIgnoreCase) != -1)
            //{

            //	relativeUrl = "~/" + AgilityContext.LanguageCode + relativeUrl.Substring(1);
            //}

            //replace the ~/
            if (relativeUrl.StartsWith("~/"))
            {


                string appPath = AgilityContext.HttpContext.Request.PathBase;
                if (appPath.EndsWith("/")) return string.Format("{0}{1}", appPath, relativeUrl.Substring(2));
                return string.Format("{0}{1}", appPath, relativeUrl.Substring(1));

            }

            return relativeUrl;
        }

        public static AgilityViewActionResult AgilityView(this Controller controller, string agilityPagePath, ViewDataDictionary viewDataSource)
        {
            return new AgilityViewActionResult(agilityPagePath, viewDataSource);
        }

        public static AgilityViewActionResult AgilityView(this Controller controller, string agilityPagePath, string languageCode, ViewDataDictionary viewDataSource)
        {
            return new AgilityViewActionResult(agilityPagePath, languageCode, viewDataSource);
        }

    }

}
