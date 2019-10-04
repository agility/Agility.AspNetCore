using System;
using System.Text;
using Agility.Web.HttpModules;
using Agility.Web.Objects;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Agility.Web.Configuration;
using Agility.Web.Caching;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Agility.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;
using Microsoft.AspNetCore.Mvc.Razor;
using Agility.Web.Providers;

namespace Agility.Web.Mvc
{
    public class AgilityViewActionResult : ActionResult
    {
        public string AgilityPagePath { get; set; }
        public string LanguageCode { get; set; }

        /// <summary>
        /// Set this if we do NOT want to process the Redirects or derive the current channel.
        /// </summary>
        public bool IgnorePrechecks { get; set; }

        public ViewDataDictionary<AgilityTemplateModel> ViewData { get; set; }

        public AgilityViewActionResult(string agilityPagePath, ViewDataDictionary viewDataSource)
        {
            AgilityPagePath = agilityPagePath;
            ViewData = new ViewDataDictionary<AgilityTemplateModel>(viewDataSource);
        }

        public AgilityViewActionResult(string agilityPagePath, string languageCode, ViewDataDictionary viewDataSource)
        {
            AgilityPagePath = agilityPagePath;
            LanguageCode = languageCode;
            ViewData = new ViewDataDictionary<AgilityTemplateModel>(viewDataSource);
        }

        public AgilityViewActionResult(string agilityPagePath, string languageCode, bool ignorePrechecks, ViewDataDictionary viewDataSource)
        {
            AgilityPagePath = agilityPagePath;
            LanguageCode = languageCode;
            IgnorePrechecks = ignorePrechecks;
            ViewData = new ViewDataDictionary<AgilityTemplateModel>(viewDataSource);

        }


        public override void ExecuteResult(ActionContext context)
        {

            try
            {
                EmptyResult empty = new EmptyResult();

                //check if the request has been already served (set in route constraint)
                object constraintCheck = context.HttpContext.Items["Agility.Web.RequestPreviouslyHandledInRouteConstraint"];
                if (constraintCheck is bool && ((bool)constraintCheck))
                {
                    empty.ExecuteResult(context);
                    return;
                }


                HttpRequest Request = context.HttpContext.Request;
                HttpResponse Response = context.HttpContext.Response;



                string url = UriHelper.GetEncodedUrl(Request);
                string rawUrl = Request.GetEncodedPathAndQuery();

                //check if this is Robots.txt and a publishwithagility.com domain (used for staging only)
                if ((
                    Request.Host.Value.IndexOf(".publishwithagility.com", StringComparison.CurrentCultureIgnoreCase) != -1
                    || Request.Host.Value.IndexOf(".azurewebsites.net", StringComparison.CurrentCultureIgnoreCase) != -1)
                    && string.Equals(rawUrl, "/robots.txt", StringComparison.CurrentCultureIgnoreCase))
                {

                    Response.ContentType = "text/plain";

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("User-agent: *");
                    sb.Append("Disallow: /");
                    var task = Response.WriteAsync(sb.ToString());
                    task.Wait();

                    empty.ExecuteResult(context);
                    AgilityCache.TurnOffCacheInProgress();
                    return;
                }


                //don't allow ANY urls to end with /
                if (!Current.Settings.IgnoreTrailingSlash)
                {
                    if (rawUrl != "/" && rawUrl.EndsWith("/"))
                    {
                        string newUrl = rawUrl.TrimEnd('/');

                        AgilityHttpModule.RedirectResponse(newUrl, 301);
                        AgilityCache.TurnOffCacheInProgress();
                        return;
                    }
                }



                if (AgilityContext.BuildAgilityContext())
                {
                    empty.ExecuteResult(context);


                    AgilityCache.TurnOffCacheInProgress();

                    return;
                }

                //pre-process the request for any channel or redirections that we encounter...
                string pagePath = AgilityPagePath;
                if (!IgnorePrechecks)
                {
                    if (AgilityHttpModule.HandleChannelsAndRedirects(ref pagePath, LanguageCode))
                    {
                        empty.ExecuteResult(context);

                        AgilityCache.TurnOffCacheInProgress();

                        return;
                    }
                }


                //use the first page in the sitemap if there is no page passed in...
                if (string.IsNullOrEmpty(pagePath) || pagePath == "/" || pagePath == "~/")
                {

                    pagePath = BaseCache.GetDefaultPagePath(AgilityContext.LanguageCode, AgilityContext.WebsiteName, string.Empty);
                }

                //check to see if the site should be in "preview mode"
                if (AgilityHttpModule.CheckPreviewMode(context.HttpContext))
                {
                    empty.ExecuteResult(context);

                    AgilityCache.TurnOffCacheInProgress();

                    return;
                }




                AgilityPage page = null;

                if (!string.IsNullOrEmpty(pagePath))
                {
                    //add the ~ if neccessary
                    if (!pagePath.StartsWith("~/"))
                    {
                        if (!pagePath.StartsWith("/"))
                        {
                            pagePath = string.Format("~/{0}", pagePath);
                        }
                        else
                        {
                            pagePath = string.Format("~{0}", pagePath);
                        }
                    }


                    page = Agility.Web.Data.GetPage(pagePath, LanguageCode);

                    //check if this page is a folder, kick out...				
                    if (page != null
                        && string.IsNullOrEmpty(page.TemplatePath)
                        && page.TemplateID < 1
                        && string.IsNullOrEmpty(page.RedirectURL))
                    {
                        page = null;
                    }


                }


                if (AgilityContext.IsResponseEnded)
                {
                    //check if we've ended the response...
                    empty.ExecuteResult(context);

                    AgilityCache.TurnOffCacheInProgress();

                    return;
                }

                if (page == null)
                {
                    //possibly route the page if it is a page template...

                    string aspxPath = context.HttpContext.Request.PathBase;

                    int previewIndex = aspxPath.IndexOf("TemplatePreview/", StringComparison.CurrentCultureIgnoreCase);

                    if (previewIndex > -1)
                    {
                        aspxPath = aspxPath.Substring(previewIndex + "TemplatePreview/".Length);
                        aspxPath = string.Format("~/{0}", aspxPath);

                        if (!aspxPath.EndsWith(".aspx", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //assume we wanted to render a .cshtml file							
                            aspxPath = string.Format("{0}.cshtml", aspxPath);
                        }


                        //load the page def in Preview mode...
                        int pageDefinitionID = 0;
                        if (int.TryParse(Request.Query["agilityPageDefinitionID"], out pageDefinitionID)
                            && pageDefinitionID > 0)
                        {


                            AgilityContext.IsTemplatePreview = true;

                            string pageTitle = string.Empty;

                            AgilityContentServer.AgilityPageDefinition _currentPageDefinition = BaseCache.GetPageDefinition(pageDefinitionID, AgilityContext.WebsiteName);
                            if (_currentPageDefinition != null)
                            {



                                AgilityContext.CurrentPageTemplateInPreview = _currentPageDefinition;

                                pageTitle = string.Format("Template Preview - {0}", _currentPageDefinition.Name);

                                ViewData.Model = new AgilityTemplateModel()
                                {
                                    Page = new AgilityPage()
                                    {
                                        Title = pageTitle
                                    }
                                };

                                ViewResult templateView = new ViewResult()
                                {
                                    //TODO: handle template preview with inline code...
                                    ViewName = _currentPageDefinition.TemplatePath,
                                    ViewData = ViewData
                                };

                                return;
                            }
                        }
                    }

                    //

                    //404 - we can't load the page, so we have to throw a 404
                    //TODO: figure out how to return a 404
                    context.HttpContext.Response.StatusCode = 404;
                    return;

                }



                //if we get here and we don't have a page, throw a 404
                if (page == null)
                {
                    context.HttpContext.Response.StatusCode = 404;
                    return;
                }


                //set the dynamic page formula items from the last ones that were loaded...
                AgilityContext.DynamicPageFormulaItem = AgilityContext.LastLoadedDynamicPageFormulaItem;

                //check if this page is a redirect
                if (!string.IsNullOrEmpty(page.RedirectURL))
                {
                    //redirect to the link specified on the page
                    AgilityHttpModule.RedirectResponse(page.RedirectURL);
                    AgilityCache.TurnOffCacheInProgress();
                    return;
                }


                //set the current language and culture based on the language code...
                AgilityHttpModule.SetLanguageAndCultureBasedOnPage(page);

                //set the page in context (Context.items)
                AgilityContext.Page = page;

                //setup ouput caching
                AgilityHttpModule.SetupOutputCaching(context.HttpContext.Request, context.HttpContext.Response, page);

                //assemble the model data
                ViewData.Model = new AgilityTemplateModel()
                {
                    Page = page
                };

                ViewData["Title"] = page.Title;

                string templatePath = string.Empty;

                DataView dv = Data.GetContentView(AgilityDynamicCodeFile.REFNAME_AgilityPageCodeTemplates, AgilityDynamicCodeFile.LANGUAGECODE_CODE);
                if (dv != null && dv.Count > 0)
                {
                    string filter = string.Format("ReferenceName = '{0}' AND Visible = true", page.TemplateID);
                    DataRow[] rows = dv.Table.Select(filter);
                    if (rows.Length > 0)
                    {
                        templatePath = string.Format("~/Views/{0}/DynamicAgilityCode/{1}/{2}.cshtml",
                        rows[0]["VersionID"],
                        AgilityDynamicCodeFile.REFNAME_AgilityPageCodeTemplates,
                        page.TemplateID);
                    }

                    else
                    {

                    }
                }

                if (string.IsNullOrEmpty(templatePath))
                {
                    //if it's not in code, check the regular template path
                    templatePath = page.TemplatePath;
                }

                if (string.IsNullOrEmpty(templatePath))
                {
                    //still null? throw an error...
                    throw new ApplicationException("The template for this page is not available.");

                }


				//get the view engine
				var tempdata = HtmlHelperViewExtensions.GetServiceOrFail<ITempDataDictionaryFactory>(context.HttpContext);
                var engine = HtmlHelperViewExtensions.GetServiceOrFail<IRazorViewEngine>(context.HttpContext);


                ViewEngineResult viewResult = engine.GetView(null, templatePath, true);

                var tempDataDict = tempdata.GetTempData(context.HttpContext);

                StringWriter sw = new StringWriter();

                if (!viewResult.Success || viewResult?.View == null)
                {
                    throw new ApplicationException("The template for this page is not available.");

                }

				

                var viewContext = new ViewContext(context, viewResult.View, ViewData, tempDataDict, sw, new HtmlHelperOptions());

				//render the view...
                var t = viewResult.View.RenderAsync(viewContext);
                t.Wait();

                string s = sw.GetStringBuilder().ToString();


				//set the content type
				Response.ContentType = "text/html";

				t = Response.WriteAsync(s);
                t.Wait();


                //ViewResult view = new ViewResult()
                //{
                //	ViewName = templatePath,
                //	ViewData = ViewData					
                //};



                //view.ExecuteResult(context);
            }
            //TODO: do we get an HttpException here?
            //catch (HttpException hex)
            //{

            //	if (hex.InnerException != null)
            //	{
            //		if (hex.InnerException.InnerException != null && hex.InnerException.InnerException is HttpException)
            //		{
            //			//http exceptions thrown from Partial Controllers are buried...
            //			throw hex.InnerException.InnerException;
            //		}

            //	} 

            //	throw hex;

            //}
            catch (Exception ex)
            {

                StringBuilder sb = new StringBuilder();
                sb.Append("ViewData: ").AppendLine();
                foreach (string key in ViewData.Keys)
                {
                    sb.AppendFormat("{0}: {1}", key, ViewData[key]).AppendLine();
                }


                ApplicationException aex = new ApplicationException(sb.ToString(), ex);

                AgilityHttpModule.HandleIntializationException(aex);
            }
        }








    }
}
