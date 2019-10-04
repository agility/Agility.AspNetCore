using Agility.Web.Configuration;
using Agility.Web.Extensions;
using Agility.Web.Objects;
using Agility.Web.Providers;
using Agility.Web.Util;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Agility.Web.Mvc
{
    public static class AgilityViewComponentHelperExtension
    {
        public static ConcurrentDictionary<string, DynamicObjectActivator> GenericTypeConstructorCache = new ConcurrentDictionary<string, DynamicObjectActivator>();
        public static ConcurrentDictionary<string, DynamicObjectActivator> TypeConstructorCache = new ConcurrentDictionary<string, DynamicObjectActivator>();

        public static DynamicObjectActivator<T> CreateCtor<T>()
        {
            var type = typeof(T);
            ConstructorInfo emptyConstructor = type.GetConstructor(Type.EmptyTypes);
            var dynamicMethod = new DynamicMethod("CreateInstance", type, Type.EmptyTypes, true);
            ILGenerator ilGenerator = dynamicMethod.GetILGenerator();
            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Newobj, emptyConstructor);
            ilGenerator.Emit(OpCodes.Ret);
            return (DynamicObjectActivator<T>)dynamicMethod.CreateDelegate(typeof(DynamicObjectActivator<T>));
        }

        public static DynamicObjectActivator CreateCtor(Type type)
        {
            if (type == null)
            {
                throw new NullReferenceException("type");
            }
            ConstructorInfo emptyConstructor = type.GetConstructor(Type.EmptyTypes);
            var dynamicMethod = new DynamicMethod("CreateInstance", type, Type.EmptyTypes, true);
            ILGenerator ilGenerator = dynamicMethod.GetILGenerator();
            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Newobj, emptyConstructor);
            ilGenerator.Emit(OpCodes.Ret);
            return (DynamicObjectActivator)dynamicMethod.CreateDelegate(typeof(DynamicObjectActivator));
        }

        public delegate T DynamicObjectActivator<T>();
        public delegate object DynamicObjectActivator();

        public static Task<IHtmlContent> RenderContentZone(this IViewComponentHelper component, string zoneName)
        {
            return component.RenderContentZone(AgilityContext.Page, zoneName);
        }

        private static string GetClassName(string name)
        {
            name = Url.RemoveSpecialCharacters(name);

            System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex("^[1-9]");
            if (r.IsMatch(name))
            {
                name = string.Format("_{0}", name);
            }

            return name;
        }


        public static Task<IHtmlContent> RenderContentZone(this IViewComponentHelper component, AgilityPage page, string zoneName)
        {

            try
            {
                HttpContext currentContext = AgilityContext.HttpContext;

                String websiteName = AgilityContext.WebsiteName;
                if (page == null)
                {

                    if (AgilityContext.IsTemplatePreview)
                    {
                        //TODO render the template preview


                        //var templates = from cs in AgilityContext.CurrentPageTemplateInPreview.ContentSections
                        //				where string.Equals(cs.Name, zoneName, StringComparison.CurrentCultureIgnoreCase)
                        //				orderby cs.XModuleOrder
                        //				select cs;
                        //TextWriter tw = helper.ViewContext.Writer;

                        //foreach (var cs in templates)
                        //{
                        //	tw.Write(string.Format("<div class=\"AgilityContentSectionDefinition\"><div class=\"ContentSectionTitle\">{0}</div></div>", cs.Name));
                        //}

                    }

                    return Task.Run<IHtmlContent>(() => { return new HtmlString("TODO: implement template preview"); });


                }



                //regular page rendering
                var sections = from cs in page.ContentSections
                               where string.Equals(cs.Name, zoneName, StringComparison.CurrentCultureIgnoreCase)
                               orderby cs.ModuleOrder
                               select cs;

                List<Task<string>> sectionTasks = new List<Task<string>>();
                DateTime dtStart = DateTime.Now;

                foreach (var cs in sections)
                {
                    //only continue if we have a content ref name...
                    if (string.IsNullOrEmpty(cs.ContentReferenceName))
                        continue;

                    //keep track of the various way this module can be rendered
                    List<ModuleRender> renders = new List<ModuleRender>()
                    {
                        new ModuleRender()
                        {
                            ContentReferenceName = cs.ContentReferenceName
                        }
                    };

                    //if this content zone is part of a Module AB test Experiment
                    if (cs.ExperimentID > 0)
                    {
                        var lst = BaseCache.GetExperiments(AgilityContext.WebsiteName);
                        AgilityContentServer.AgilityExperiment experiment = lst.GetExperiment(cs.ExperimentID);

                        if (experiment != null)
                        {
                            foreach (var variant in experiment.Variants.Where(v => !string.IsNullOrWhiteSpace(v.ContentReferenceName)))
                            {
                                renders.Add(new ModuleRender()
                                {
                                    ContentReferenceName = variant.ContentReferenceName,
                                    Variant = variant
                                });
                            }
                        }
                    }

                    foreach (ModuleRender moduleRender in renders)
                    {
						try
						{

							string contentReferenceName = moduleRender.ContentReferenceName;

							Agility.Web.AgilityContentServer.AgilityContent moduleContent = BaseCache.GetContent(contentReferenceName, AgilityContext.LanguageCode, websiteName);
							if (moduleContent == null
								|| moduleContent.DataSet == null
								|| moduleContent.DataSet.Tables["ContentItems"] == null
								|| moduleContent.DataSet.Tables["ContentItems"].Rows.Count == 0)
								continue;


							DataRowView drv = moduleContent.DataSet.Tables["ContentItems"].DefaultView[0];

							int moduleContentID = -1;
							int moduleVersionID = -1;

							if (!int.TryParse($"{drv["ContentID"]}", out moduleContentID)) moduleContentID = -1;
							if (!int.TryParse($"{drv["VersionID"]}", out moduleVersionID)) moduleVersionID = -1;
							

							Agility.Web.AgilityContentServer.AgilityModule module = BaseCache.GetModule(cs.ModuleID, websiteName);
							if (module == null)
							{
								continue;
							}


							object model = new AgilityModuleModel()
							{
								ModuleContentName = contentReferenceName,
								ModuleProperties = drv,
								LanguageCode = AgilityContext.LanguageCode
							};

							string viewComponentName = null;
							string agilityCodeRefName = null;

							using (StringReader sr = new StringReader(module.XmlSchema))
							{
								DataSet ds = new DataSet();
								ds.ReadXmlSchema(sr);

								viewComponentName = ds.ExtendedProperties["ViewComponent"] as string;
								agilityCodeRefName = ds.ExtendedProperties["AgilityCodeRefName"] as string;

							}

							if (!string.IsNullOrEmpty(viewComponentName))
							{
								#region *** VIEW COMPONENTS **
								var viewComponentFactory = HtmlHelperViewExtensions.GetServiceOrFail<IViewComponentSelector>(AgilityContext.HttpContext);
								var componentDesc = viewComponentFactory.SelectComponent(viewComponentName);

								if (componentDesc == null)
								{
									throw new ApplicationException(string.Format("The view component {0} was not found.", viewComponentName));
								}

								MethodInfo method = componentDesc.MethodInfo;


								if (method == null)
								{
									throw new ApplicationException(string.Format("The component invoke method was not found in the component {0}", viewComponentName));
								}

								ParameterInfo[] paramAry = method.GetParameters();
								if (paramAry.Length > 0)
								{

									ParameterInfo paramInfo = paramAry[0];
									Type paramType = paramInfo.ParameterType;
									if (paramType.IsSubclassOf(typeof(AgilityContentItem)) || paramType == typeof(AgilityContentItem))
									{
										ConstructorInfo ci = paramType.GetConstructor(System.Type.EmptyTypes);

										if (ci == null)
										{
											throw new ApplicationException(string.Format("No default constructor found for type {0}", paramType.Name));
										}

										AgilityContentItem moduleItem = ci.Invoke(new object[0]) as AgilityContentItem;
										moduleItem.DataRow = drv.Row;
										moduleItem.LanguageCode = AgilityContext.LanguageCode;
										moduleItem.ReferenceName = contentReferenceName;
										moduleItem.ContentID = moduleContentID;
										moduleItem.VersionID = moduleVersionID;


										try
										{


											Task<IHtmlContent> task = component.InvokeAsync(viewComponentName, moduleItem);

											moduleRender.RenderTask = task;
											moduleRender.ContentID = moduleContentID;
										}
										catch (Exception ex)
										{
											moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Component {viewComponentName}</p><pre>{ex}</pre>");
											moduleRender.ContentID = moduleContentID;
										}
									}
									else
									{
										throw new ApplicationException(string.Format("The component invoke method parameter was not of type AgilityContent in the component {0}", viewComponentName));
									}
									#endregion
								}
							}
							else if (!string.IsNullOrEmpty(agilityCodeRefName))
							{
								#region *** Agility Inline Code ***
								DataView dv = Data.GetContentView(AgilityDynamicCodeFile.REFNAME_AgilityModuleCodeTemplates, AgilityDynamicCodeFile.LANGUAGECODE_CODE);

								string filter = string.Format("ReferenceName = '{0}' AND Visible = true", agilityCodeRefName);
								DataRow[] rows = dv.Table.Select(filter);
								if (rows.Length > 0)
								{
									string modulePath =
										$"~/Views/{rows[0]["VersionID"]}/DynamicAgilityCode/{AgilityDynamicCodeFile.REFNAME_AgilityModuleCodeTemplates}/{agilityCodeRefName}.cshtml";

									AgilityContentItem moduleItem = new AgilityContentItem();
									moduleItem.DataRow = drv.Row;
									moduleItem.LanguageCode = AgilityContext.LanguageCode;
									moduleItem.ReferenceName = contentReferenceName;
									moduleItem.ContentID = moduleContentID;
									moduleItem.VersionID = moduleVersionID;
									//moduleItem.InlineCodePath = modulePath;

									try
									{
										Task<IHtmlContent> task = component.InvokeAsync("AgilityInlineCode", new { inlineCodePath = modulePath, module = moduleItem });

										moduleRender.RenderTask = task;
										moduleRender.ContentID = moduleContentID;
									}
									catch (Exception ex)
									{
										moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Inline Code</p><pre>{ex}</pre>");
										moduleRender.ContentID = moduleContentID;
									}
								}
								else
								{
									moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Inline Code</p>");
									moduleRender.ContentID = moduleContentID;
								}
								#endregion
							}
							else if (!string.IsNullOrEmpty(module.ControlPath))
							{
								#region *** Control Path - Partial View ***
								string className = GetClassName(module.ReferenceName ?? module.Name);
								string typeName = string.Format("Module_{0}", className);

								Type paramType = Agility.Web.Utils.FileUtils.GetTypeFromReflection(null, typeName);
								if (paramType.IsSubclassOf(typeof(AgilityContentItem)) || paramType == typeof(AgilityContentItem))
								{
									ConstructorInfo ci = paramType.GetConstructor(System.Type.EmptyTypes);

									if (ci == null)
									{
										throw new ApplicationException(string.Format("No default constructor found for type {0}", paramType.Name));
									}

									AgilityContentItem moduleItem = ci.Invoke(new object[0]) as AgilityContentItem;
									moduleItem.DataRow = drv.Row;
									moduleItem.LanguageCode = AgilityContext.LanguageCode;
									moduleItem.ReferenceName = contentReferenceName;
									moduleItem.ContentID = moduleContentID;
									moduleItem.VersionID = moduleVersionID;


									try
									{
										Task<IHtmlContent> task = component.InvokeAsync("AgilityPartialView", new { partialViewPath = module.ControlPath, module = moduleItem });

										moduleRender.RenderTask = task;
										moduleRender.ContentID = moduleContentID;
									}
									catch (Exception ex)
									{
										moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Partial View at {module.ControlPath}</p><pre>{ex}</pre>");
										moduleRender.ContentID = moduleContentID;
									}
								}
								else
								{
									throw new ApplicationException(string.Format("The component invoke method parameter was not of type AgilityContent in the component {0}", viewComponentName));
								}
								#endregion
							}
							else if (!string.IsNullOrEmpty(module.Markup))
							{

								if (module.Markup.StartsWith("@"))
								{
									#region *** Inline Razor Markup ***
									AgilityContentItem item = new AgilityContentItem();
									item.DataRow = drv.Row;
									item.LanguageCode = AgilityContext.LanguageCode;
									item.ReferenceName = contentReferenceName;
									item.ContentID = moduleContentID;
									item.VersionID = moduleVersionID;
									model = item;

									string viewPath = string.Format("~/Views/DynamicAgilityModule/MVC/{0}/{1}.cshtml", AgilityContext.CurrentMode, module.ID);

									try
									{
										Task<IHtmlContent> task = component.InvokeAsync("AgilityInlineCode", new { inlineCodePath = viewPath, module = item });

										moduleRender.RenderTask = task;
										moduleRender.ContentID = moduleContentID;
									}
									catch (Exception ex)
									{
										moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Dynamic Agility Module</p><pre>{ex}</pre>");
										moduleRender.ContentID = moduleContentID;
									}
									#endregion
								}
							}
						}
						catch (Exception ex)
						{							
							if (Current.Settings.DevelopmentMode)
							{								
								moduleRender.PreRenderedContent = new HtmlString($"<div>Could not output zone {cs.Name}</div><div>{ex.ToString().Replace("\n", "<br/>")}</div>");
							} else
							{
								moduleRender.PreRenderedContent = new HtmlString($"<!-- Could not output zone {cs.Name} - See web log -->");
								Agility.Web.Tracing.WebTrace.WriteException(ex);
							}
						}
                    }

                    Task<string> sectionTask = Task<string>.Run(() =>
                    {
                        AgilityContext.HttpContext = currentContext;
						var rendersToOutput = renders.Where(r => r.RenderTask != null || r.PreRenderedContent != null).ToList();
                        return AgilityHelpers.RenderModuleHtml(rendersToOutput, cs);
                    });

                    sectionTasks.Add(sectionTask);

                }


                Task<IHtmlContent> retTask = Task.Run<IHtmlContent>(() =>
                {
                    AgilityContext.HttpContext = currentContext;

					try
					{
						Task.WaitAll(sectionTasks.ToArray());
					} catch { }

                    using (StringWriter htmlStringWriter = new StringWriter())
                    {
                        foreach (var t in sectionTasks)
                        {
							if (t.IsFaulted)
							{
								Agility.Web.Tracing.WebTrace.WriteException(t.Exception, $"Error rendering module in zone {zoneName} - {t.AsyncState}");



							}
							else
							{
								htmlStringWriter.Write(t.Result);
							}
                        }

                        TimeSpan ts = DateTime.Now - dtStart;
                        if (ts.TotalSeconds > 1)
                        {

                            string renderTimeMessage = string.Format("Content Zone: {0} - Render Time: {1:F2} seconds.", zoneName, ts.TotalMilliseconds / 1000);
                            Agility.Web.Tracing.WebTrace.WriteVerboseLine(renderTimeMessage);
                            htmlStringWriter.Write(string.Format("<!-- {0} -->", renderTimeMessage));
                        }

                        return new HtmlString(htmlStringWriter.ToString());
                    }

                });


                return retTask;


            }
            catch (Exception ex)
            {
                string errHtml = null;
                string msg = string.Format("Could not output content zone {0}.", zoneName);
                if (ex is InvalidOperationException && ex.Message.Contains("No route"))
                {
                    msg +=
                        @" This error is usually caused by a missing route in your Global.asax.cs. Ensure the following route is defined after the normal Agility route.\n
routes.MapRoute(""Default"", ""{controller}/{action}/{id}"",\n
    new { controller = """", action = """", id = """" }\n
);";
                }

                if (Current.Settings.DevelopmentMode)
                {

                    errHtml = ex.ToString().Replace("\n", "<br/>");
                }
                else
                {

                    errHtml = msg;

                }

                Agility.Web.Tracing.WebTrace.WriteException(ex, msg);

                return Task.Run<IHtmlContent>(() => { return new HtmlString(errHtml); });

            }
        }
    }
}
