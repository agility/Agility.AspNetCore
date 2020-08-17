//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Web;
//using Microsoft.AspNetCore.Http.Extensions;
//using System.IO;

//using System.Data;
//using Agility.Web.Configuration;

//using Agility.Web.Mvc;
//using System.Reflection;

//using System.Text;
//using Agility.Web.Objects;
//using Agility.Web.Util;
//using Microsoft.AspNetCore.Mvc.ViewFeatures;
//using Microsoft.AspNetCore.Html;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Routing;
//using Microsoft.AspNetCore.Mvc.Controllers;
//using Agility.Web.Caching;
//using Agility.Web.Extensions;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.AspNetCore.Mvc.ViewComponents;
//using System.Threading.Tasks;

//namespace Agility.Web.Mvc.ViewComponents
//{
//	public class ContentZone : ViewComponent
//	{
//		public Task<IHtmlContent> InvokeAsync(string zoneName)
//		{
//			try
//			{

//				AgilityPage page = AgilityContext.Page;
//				HttpContext currentContext = AgilityContext.HttpContext;

//				String websiteName = AgilityContext.WebsiteName;
//				if (page == null)
//				{

//					if (AgilityContext.IsTemplatePreview)
//					{
//						//TODO render the template preview


//						//var templates = from cs in AgilityContext.CurrentPageTemplateInPreview.ContentSections
//						//				where string.Equals(cs.Name, zoneName, StringComparison.CurrentCultureIgnoreCase)
//						//				orderby cs.XModuleOrder
//						//				select cs;
//						//TextWriter tw = helper.ViewContext.Writer;

//						//foreach (var cs in templates)
//						//{
//						//	tw.Write(string.Format("<div class=\"AgilityContentSectionDefinition\"><div class=\"ContentSectionTitle\">{0}</div></div>", cs.Name));
//						//}

//					}

//					return Task.Run<IHtmlContent>(() => { return new HtmlString("TODO: implement template preview"); });


//				}



//				//regular page rendering
//				var sections = from cs in page.ContentSections
//							   where string.Equals(cs.Name, zoneName, StringComparison.CurrentCultureIgnoreCase)
//							   orderby cs.ModuleOrder
//							   select cs;




//				List<Task<string>> sectionTasks = new List<Task<string>>();
//				DateTime dtStart = DateTime.Now;

//				foreach (var cs in sections)
//				{

//					//only continue if we have a content ref name...
//					if (string.IsNullOrEmpty(cs.ContentReferenceName)) continue;

//					//keep track of the various way this module can be rendered
//					List<ModuleRender> renders = new List<ModuleRender>()
//						{
//							new ModuleRender()
//							{
//								ContentReferenceName = cs.ContentReferenceName
//							}
//						};




//					//if this content zone is part of a Module AB test Experiment
//					if (cs.ExperimentID > 0)
//					{
//						var lst = BaseCache.GetExperiments(AgilityContext.WebsiteName);
//						AgilityContentServer.AgilityExperiment experiment = lst.GetExperiment(cs.ExperimentID);

//						if (experiment != null)
//						{
//							foreach (var variant in experiment.Variants.Where(v => !string.IsNullOrWhiteSpace(v.ContentReferenceName)))
//							{
//								renders.Add(new ModuleRender()
//								{
//									ContentReferenceName = variant.ContentReferenceName,
//									Variant = variant
//								});
//							}
//						}
//					}



//					foreach (ModuleRender moduleRender in renders)
//					{

//						string contentReferenceName = moduleRender.ContentReferenceName;

//						Agility.Web.AgilityContentServer.AgilityContent moduleContent = BaseCache.GetContent(contentReferenceName, AgilityContext.LanguageCode, websiteName);
//						if (moduleContent == null
//							|| moduleContent.DataSet == null
//							|| moduleContent.DataSet.Tables["ContentItems"] == null
//							|| moduleContent.DataSet.Tables["ContentItems"].Rows.Count == 0) continue;


//						DataRowView drv = moduleContent.DataSet.Tables["ContentItems"].DefaultView[0];

//						int moduleContentID = (int)drv["ContentID"];
//						int moduleVersionID = (int)drv["VersionID"];

//						Agility.Web.AgilityContentServer.AgilityModule module = BaseCache.GetModule(cs.ModuleID, websiteName);
//						if (module == null) continue;
//						object model = new AgilityModuleModel()
//						{
//							ModuleContentName = contentReferenceName,
//							ModuleProperties = drv,
//							LanguageCode = AgilityContext.LanguageCode
//						};

//						string controllerName = null;
//						string action = null;
//						string agilityCodeRefName = null;

//						using (StringReader sr = new StringReader(module.XmlSchema))
//						{
//							DataSet ds = new DataSet();
//							ds.ReadXmlSchema(sr);

//							controllerName = ds.ExtendedProperties["MVCController"] as string;
//							action = ds.ExtendedProperties["MVCAction"] as string;
//							agilityCodeRefName = ds.ExtendedProperties["AgilityCodeRefName"] as string;

//						}

//						if (!string.IsNullOrEmpty(controllerName)
//							&& !string.IsNullOrEmpty(action))
//						{
//							#region *** Controller Action **



//							var viewComponentFactory = HtmlHelperViewExtensions.GetServiceOrFail<IViewComponentSelector>(AgilityContext.HttpContext);
//							var viewComponentInvoker = HtmlHelperViewExtensions.GetServiceOrFail<IViewComponentInvoker>(AgilityContext.HttpContext);

//							var componentDesc = viewComponentFactory.SelectComponent(action);
							
//							if (componentDesc == null)
//							{
//								throw new ApplicationException(string.Format("The view component {0} was not found.", action));
//							}

//							MethodInfo method = componentDesc.MethodInfo;


//							if (method == null)
//							{
//								throw new ApplicationException(string.Format("The component invoke method was not found in the component {0}", action));
//							}


//							ParameterInfo[] paramAry = method.GetParameters();
//							if (paramAry.Length > 0)
//							{

//								ParameterInfo paramInfo = paramAry[0];
//								Type paramType = paramInfo.ParameterType;
//								if (paramType.IsSubclassOf(typeof(AgilityContentItem)) || paramType == typeof(AgilityContentItem))
//								{
//									ConstructorInfo ci = paramType.GetConstructor(System.Type.EmptyTypes);

//									if (ci == null)
//									{
//										throw new ApplicationException(string.Format("No default constructor found for type {0}", paramType.Name));
//									}

//									object o = ci.Invoke(new object[0]);

//									AgilityContentItem moduleItem = ci.Invoke(new object[0]) as AgilityContentItem;
//									moduleItem.DataRow = drv.Row;
//									moduleItem.LanguageCode = AgilityContext.LanguageCode;
//									moduleItem.ReferenceName = contentReferenceName;
//									moduleItem.ContentID = moduleContentID;
//									moduleItem.VersionID = moduleVersionID;


//									try
//									{

										
//										Task<IHtmlContent> task = component.InvokeAsync(action, moduleItem);

//										moduleRender.RenderTask = task;
//										moduleRender.ContentID = moduleContentID;
//									}
//									catch (Exception ex)
//									{
//										moduleRender.PreRenderedContent = new HtmlString($"<p>Error rendering Component {action}</p><pre>{ex}</pre>");
//										moduleRender.ContentID = moduleContentID;
//									}

//								}
//								else
//								{
//									throw new ApplicationException(string.Format("The component invoke method parameter was not of type AgilityContent in the component {0}", action));
//								}



//								//	HtmlString str = helper.RenderAction(action, controllerName, viewContext.RouteData);

//								//	//update the module render object
//								//	moduleRender.RenderedContent = str;
//								//	moduleRender.ContentID = moduleContentID;


//								//	//MOD JOELV - AB TEST RenderModuleHtml(str, cs, moduleContentID, helper.ViewContext.Writer);
//								//}
//								//else
//								//{
//								//	HtmlString content = helper.RenderAction(action, controllerName);

//								//	//update the module render object
//								//	moduleRender.RenderedContent = content;
//								//	moduleRender.ContentID = moduleContentID;


//								//	//MOD JOELV - AB TEST RenderModuleHtml(str, cs, moduleContentID, helper.ViewContext.Writer);
//								//}

//								#endregion
//							}
//						}
//						else if (!string.IsNullOrEmpty(agilityCodeRefName))
//						{

//							moduleRender.PreRenderedContent = new HtmlString($"<pre>Could not render module content {contentReferenceName} (Inline Code: {agilityCodeRefName})</pre>");
//							#region *** Agility Inline Code ***

//							//DataView dv = Data.GetContentView(AgilityDynamicCodeFile.REFNAME_AgilityModuleCodeTemplates, AgilityDynamicCodeFile.LANGUAGECODE_CODE);

//							//string filter = string.Format("ReferenceName = '{0}' AND Visible = true", agilityCodeRefName);
//							//DataRow[] rows = dv.Table.Select(filter);
//							//if (rows.Length > 0)
//							//{
//							//	string modulePath = string.Format("~/Views/{0}/DynamicAgilityCode/{1}/{2}.cshtml",
//							//	rows[0]["VersionID"],
//							//	AgilityDynamicCodeFile.REFNAME_AgilityModuleCodeTemplates,
//							//	agilityCodeRefName);

//							//	//inline Razor							

//							//	AgilityContentItem item = new AgilityContentItem();
//							//	item.DataRow = drv.Row;
//							//	item.LanguageCode = AgilityContext.LanguageCode;
//							//	item.ReferenceName = contentReferenceName;
//							//	item.ContentID = moduleContentID;
//							//	item.VersionID = moduleVersionID;
//							//	model = item;

//							//	helper.RenderPartial(modulePath, model);

//							//}
//							//else
//							//{
//							//	throw new ApplicationException(string.Format("The code template for the module {0} is not available.", agilityCodeRefName));
//							//}


//							#endregion
//						}
//						else if (!string.IsNullOrEmpty(module.ControlPath))
//						{
//							moduleRender.PreRenderedContent = new HtmlString($"<pre>Could not render module content {contentReferenceName} (Partial View: {module.ControlPath})</pre>");
//							#region *** Control Path - Partial View ***
//							////TODO: special case for the Agility Form Builder
//							//if (module.ControlPath == "AgilityFormBuilder")
//							//{
//							//	//string str = FormBuilder.EmitFormBuilderHtml(model as AgilityModuleModel);
//							//	//helper.ViewContext.Writer.Write(str);
//							//}
//							//else
//							//{

//							//	//try to resolve the model to the typename
//							//	string className = GetClassName(module.ReferenceName ?? module.Name);
//							//	string typeName = string.Format("Module_{0}", className);

//							//	Type modelType = Agility.Web.Utils.FileUtils.GetTypeFromReflection(null, typeName);

//							//	if (modelType != null)
//							//	{
//							//		ConstructorInfo constr = modelType.GetConstructor(System.Type.EmptyTypes);
//							//		if (constr != null)
//							//		{
//							//			AgilityContentItem t = constr.Invoke(new object[0]) as AgilityContentItem;
//							//			if (t != null)
//							//			{

//							//				t.DataRow = drv.Row;
//							//				t.LanguageCode = AgilityContext.LanguageCode;
//							//				t.ReferenceName = contentReferenceName;
//							//				t.ContentID = moduleContentID;
//							//				t.VersionID = moduleVersionID;

//							//				model = t;

//							//			}
//							//		}
//							//	}


//							//	//module control path (partial view)
//							//	string path = module.ControlPath;

//							//	var context = helper.ViewContext;
//							//	context.RouteData.Values["Controller"] = "Agility";
//							//	context.RouteData.Values["Action"] = "RenderModule";
//							//	context.RouteData.Values["model"] = model;
//							//	context.RouteData.Values["viewName"] = path;

//							//	HtmlString str = helper.RenderAction("RenderModule", "Agility", context.RouteData);

//							//	//update the module render object
//							//	moduleRender.RenderedContent = str;
//							//	moduleRender.ContentID = moduleContentID;


//							//	//MOD JOELV - AB TEST RenderModuleHtml(str, cs, moduleContentID, helper.ViewContext.Writer);


//							//}
//							#endregion

//						}
//						else if (!string.IsNullOrEmpty(module.Markup))
//						{

//							moduleRender.PreRenderedContent = new HtmlString($"<pre>Could not render module content {contentReferenceName} (Embedded Markup)</pre>");

//							if (module.Markup.StartsWith("@"))
//							{
//								#region *** Inline Razor Markup ***
//								//	//inline Razor							

//								//	AgilityContentItem item = new AgilityContentItem();
//								//	item.DataRow = drv.Row;
//								//	item.LanguageCode = AgilityContext.LanguageCode;
//								//	item.ReferenceName = contentReferenceName;
//								//	item.ContentID = (int)drv["ContentID"];
//								//	item.VersionID = (int)drv["VersionID"];
//								//	model = item;

//								//	string viewPath = string.Format("~/Views/DynamicAgilityModule/MVC/{0}/{1}.cshtml", AgilityContext.CurrentMode, module.ID);

//								//	var context = helper.ViewContext;
//								//	context.RouteData.Values["Controller"] = "Agility";
//								//	context.RouteData.Values["Action"] = "RenderModule";
//								//	context.RouteData.Values["model"] = model;
//								//	context.RouteData.Values["viewName"] = viewPath;

//								//	HtmlString str = helper.RenderAction("RenderModule", "Agility", context.RouteData);

//								//	//update the module render object
//								//	moduleRender.RenderedContent = str;
//								//	moduleRender.ContentID = moduleContentID;


//								//	//MOD JOELV - AB TEST RenderModuleHtml(str, cs, moduleContentID, helper.ViewContext.Writer);
//								#endregion

//							}
//						}
//					}

//					Task<string> sectionTask = Task<string>.Run(() => {
//						AgilityContext.HttpContext = currentContext;
//						return AgilityHelpers.RenderModuleHtml(renders, cs);
//					});

//					sectionTasks.Add(sectionTask);

//				}


//				Task<IHtmlContent> retTask = Task.Run<IHtmlContent>(() =>
//				{
//					AgilityContext.HttpContext = currentContext;

//					Task.WaitAll(sectionTasks.ToArray());

//					using (StringWriter htmlStringWriter = new StringWriter())
//					{
//						foreach (var t in sectionTasks)
//						{
//							htmlStringWriter.Write(t.Result);
//						}

//						TimeSpan ts = DateTime.Now - dtStart;
//						if (ts.TotalSeconds > 1)
//						{

//							string renderTimeMessage = string.Format("Content Zone: {0} - Render Time: {1:F2} seconds.", zoneName, ts.TotalMilliseconds / 1000);
//							Agility.Web.Tracing.WebTrace.WriteVerboseLine(renderTimeMessage);
//							htmlStringWriter.Write(string.Format("<!-- {0} -->", renderTimeMessage));
//						}

//						return new HtmlString(htmlStringWriter.ToString());
//					}

//				});


//				return retTask;


//			}
//			catch (Exception ex)
//			{
//				string errHtml = null;
//				string msg = string.Format("Could not output content zone {0}.", zoneName);
//				if (ex is InvalidOperationException && ex.Message.Contains("No route"))
//				{
//					msg +=
//						@" This error is usually caused by a missing route in your Global.asax.cs. Ensure the following route is defined after the normal Agility route.\n
//routes.MapRoute(""Default"", ""{controller}/{action}/{id}"",\n
//    new { controller = """", action = """", id = """" }\n
//);";
//				}

//				if (Current.Settings.DevelopmentMode)
//				{

//					errHtml = ex.ToString().Replace("\n", "<br/>");
//				}
//				else
//				{

//					errHtml = msg;

//				}

//				Agility.Web.Tracing.WebTrace.WriteException(ex, msg);

//				return Task.Run<IHtmlContent>(() => { return new HtmlString(errHtml); });

//			}

//		}
//	}
//}
