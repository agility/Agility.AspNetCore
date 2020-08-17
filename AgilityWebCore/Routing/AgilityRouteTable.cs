using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Web;

using System.Security.Permissions;
using System.Collections.Specialized;
using Agility.Web.Tracing;
using System.IO;
using Agility.Web.AgilityContentServer;
using System.Data;
using Agility.Web.Extensions;
using Agility.Web.Caching;

namespace Agility.Web.Routing
{
	public class AgilityRouteTable
	{

		private static object _lockObj = new object();

		/// <summary>
		/// A dictionary of the static routes and page ids from the agility sitemap.
		/// </summary>
		public static Dictionary<string, AgilityRouteCacheItem> GetRouteTable(string languageCode, int channelID)
		{

			string cacheKey = string.Format("Agility.Web.Providers.AgilitySiteMapProvider.RouteTableCacheKey_{0}_{1}_{2}_{3}", AgilityContext.CurrentMode, languageCode, AgilityContext.WebsiteName, channelID);

			//get the sitemap first, cause the dependancy is based on that...
			AgilityContentServer.AgilitySitemap sitemap = BaseCache.GetSitemap(languageCode, AgilityContext.WebsiteName);

            if (sitemap == null || string.IsNullOrEmpty(sitemap.SitemapXml))
            {
                Agility.Web.Tracing.WebTrace.WriteVerboseLine("Could not load Sitemap data");
                return new Dictionary<string, AgilityRouteCacheItem>();
            }


			Dictionary<string, AgilityRouteCacheItem> routeTable = AgilityCache.Get(cacheKey) as Dictionary<string, AgilityRouteCacheItem>;
			if (routeTable != null) return routeTable;

			lock (_lockObj)
			{

				Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("Building route table - {0} - {1}", languageCode, channelID));

				//check to see if this has been added to cache while we've been waiting...
				routeTable = AgilityCache.Get(cacheKey) as Dictionary<string, AgilityRouteCacheItem>;
				if (routeTable != null) return routeTable;

				routeTable = new Dictionary<string, AgilityRouteCacheItem>();

				//get the sitemap xml that we need for the sitemap...
				
				XmlDocument document = new XmlDocument();
				document.LoadXml(sitemap.SitemapXml);

				XmlNodeList siteMapNodes = document.SelectNodes(string.Format("//SiteNode[@channelID='{0}']", channelID));
				if (siteMapNodes.Count == 0)
				{
					//if the channels haven't been synced yet...
					siteMapNodes = document.SelectNodes("//SiteNode");
				}


				foreach (XmlNode node in siteMapNodes)
				{
					XmlElement elem = node as XmlElement;
					if (elem == null) continue;


					//add to the route table if this node represents a page...
					int pageID = -1;
					if (int.TryParse(elem.GetAttribute("picID"), out pageID) && pageID > 0)
					{
						
						string path = GetRoutePath(elem);

						if (!routeTable.ContainsKey(path))
						{
							AgilityRouteCacheItem item = new AgilityRouteCacheItem()
							{
								PageID = pageID
							};

							if (!string.IsNullOrEmpty(elem.GetAttribute("dynamicPageContentReferenceName"))
								|| !string.IsNullOrEmpty(elem.GetAttribute("dynamicPageParentFieldName")))
							{
								//update the parent item...
								XmlElement parentElem = elem.ParentNode as XmlElement;
								if (parentElem != null)
								{
									string parentPath = GetRoutePath(parentElem);
									if (routeTable.ContainsKey(parentPath))
									{
										routeTable[parentPath].ChildDynamicPagePath = path;
									}
								}
							}
							

							routeTable[path] = item;
						}
					}


				}

				
				//always put the thing in cache...  
				//make it dependant of the sitemap file (this way we can cache in live and staging mode...)
				CacheDependency dep = null;
				if (AgilityContext.ContentAccessor != null && AgilityContext.CurrentMode == Enum.Mode.Live)
				{
					AgilityItemKey itemKey = new AgilityItemKey();
					itemKey.Key = BaseCache.ITEMKEY_SITEMAP;
					itemKey.LanguageCode = languageCode;
					itemKey.ItemType = typeof(AgilitySitemap).Name;

					string sitemapCacheKey = BaseCache.GetCacheKey(itemKey);
					dep = new CacheDependency(new string[0], new string[1]{sitemapCacheKey});
				}
				else 
				{ 
					string filename = BaseCache.GetFilePathForItem(sitemap, AgilityContext.WebsiteName, transientPath: true);
					bool exists = File.Exists(filename);
					dep = new CacheDependency(filename);
				}

				AgilityCache.Set(cacheKey, routeTable, TimeSpan.FromDays(1),  dep, AgilityContext.DefaultCachePriority);

				return routeTable;
			}

			
		}

		private static string GetRoutePath(XmlElement elem)
		{
			string path = elem.GetAttribute("PagePath");
			if (string.IsNullOrEmpty(path)) path = elem.GetAttribute("NavigateURL");
			if (string.IsNullOrEmpty(path))
			{
				//the route for a folder, should redirect to the first page in the folder...
				path = elem.GetAttribute("FolderPath");
			
			}

			if (string.IsNullOrEmpty(path))
			{
				string x = elem.OuterXml;
			}

			//if we STILL can't get the path, something is wrong...
			if (string.IsNullOrEmpty(path))
			{
				Agility.Web.Tracing.WebTrace.WriteErrorLine(string.Format("Could not determine route for sitemap item: {0}", elem.OuterXml));
			}  

			path = path.ToLowerInvariant();

			//strip off ~ and aspx
			path = path.TrimStart('~');
			int aspxIndex = path.IndexOf(".aspx");
			if (aspxIndex > 0)
			{
				path = path.Substring(0, aspxIndex);
			}
			return path;
		}

		internal static List<ResolvedPage> ResolveRoutePath(string path, string languageCode)
		{

			if (string.IsNullOrEmpty(path)) return null;

			

			path = path.ToLowerInvariant();
			
			List<ResolvedPage> lstResolvedPages = new List<ResolvedPage>();

			AgilityRouteCacheItem routeItem = null;
			
			string websiteName = AgilityContext.WebsiteName;
			Dictionary<string, AgilityRouteCacheItem> routes = AgilityRouteTable.GetRouteTable(languageCode, AgilityContext.CurrentChannel.ID);

			#region *** STATIC PAGE ***

			//STATIC PAGE

			if (routes.TryGetEncoded(path, out routeItem))
			{

				var appRelativeCurrentExecutionFilePath = "/";

				//try to resolve the entire path first... if that works, we can kick out right away...
				AgilityPage page = BaseCache.GetPageFromID(routeItem.PageID, languageCode, websiteName, appRelativeCurrentExecutionFilePath);
				if (page == null) return null;
				if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
					|| !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
				{
					//we are on a static page, but we don't have the dynamic part of the URL in place... s

					if (Agility.Web.HttpModules.AgilityHttpModule.CheckPreviewMode(AgilityContext.HttpContext))
					{
						AgilityContext.IsResponseEnded = true;
					}
					else //if (AgilityContext.IsPreview || Configuration.Current.Settings.DevelopmentMode)
					{
						//add in the dynamic part of the URL if we are in preview mode...
						RedirectToDynamicPreviewUrl(routes, path, languageCode);
					}

					return null;
				}

				lstResolvedPages.Add(new ResolvedPage(page));
				return lstResolvedPages;
			} 
			#endregion

			//DYNAMIC PAGE
			//if we can't find the path, start peeling off parts of the path left to right and get the 
			//static paths combined with the dynamic paths

			string[] pathParts = path.Split(new char[]{'/'}, StringSplitOptions.RemoveEmptyEntries);

			//if there are less than 2 parts to the path, it CAN'T be a dynamic path...
			if (pathParts.Length < 2) return null;

			string staticRoute = "";
			string dynamicRoute = string.Empty;
			AgilityRouteCacheItem testRouteItem = null;			
			routeItem = null;

			ResolvedPage parentPage = null;

			
			//Assume we are a WEB SITE (not a virtual directory)
			string url = "/";

			//loop the rest of the path...
		    bool foundRoute = false; // signal that we found a route, we can return safely
			bool foundDynamicRoute = false; //signal that we found a dynamic route

			var pathIndex = -1;

			foreach (string pathPart in pathParts)
			{
				pathIndex++;
			    foundRoute = false; // init to not found for every iteration
				
				string testStaticRoute = string.Format("{0}/{1}", staticRoute, pathPart);

				//check for a secondary static route (eg: /Static1/Dyn1/Dyn2/Static2
				if (routes.TryGetValue(testStaticRoute, out testRouteItem))
				{
				    foundRoute = true; // static route means we found a route
				
					//found a static route, add it to the list of resolved pages...
					AgilityPage page = BaseCache.GetPageFromID(testRouteItem.PageID, languageCode, websiteName, null);
					if (page == null)
					{
						//assume a null page means a folder...
						lstResolvedPages.Add(new ResolvedPage(null));
						staticRoute = testStaticRoute;
						routeItem = testRouteItem;
						continue;
					}


					//todo...
					////check if this page is routing to a dynamic page redirect... (/path/dynamic-page/item-[contentid]
					//if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
					//	|| !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
					//{
					//	if (pathParts.Length == pathIndex + 1)
					//	{

					//		xxx

					//		//check for item- in the last part of the path to see if this is a redirect...
					//	}
					//}

					
					parentPage = new ResolvedPage(page);
					lstResolvedPages.Add(parentPage);

					routeItem = testRouteItem;
					staticRoute = testStaticRoute;

				}
				else
				{
					//did not find a static route, add this to the dynamic route...
					dynamicRoute = string.Format("{0}/{1}", dynamicRoute, pathPart);

					//if we have a dynamic Route left over, and the static page was set, then we have our first page candidate..
					if (routeItem != null)
					{

						//derive a resolved page from routeItem and dynamic route...
						ResolvedPage resolvedPage = ResolvePage(ref routes, routeItem, dynamicRoute, languageCode, websiteName, ref lstResolvedPages, url);
						if (resolvedPage != null)
						{
						    foundRoute = true;
							foundDynamicRoute = true;

							parentPage = resolvedPage;
							lstResolvedPages.Add(resolvedPage);
							
							parentPage = resolvedPage;
							dynamicRoute = string.Empty;

							//now the NEXT route item becomes the child of this dynamic page..
							if (!string.IsNullOrEmpty(routeItem.ChildDynamicPagePath)
								&& routes.TryGetValue(routeItem.ChildDynamicPagePath, out testRouteItem))
							{
								staticRoute = routeItem.ChildDynamicPagePath;
								routeItem = testRouteItem;
							}
						}
					}
				}
				
			}

			//if we didn't manage to find a dynamic page in there, return null;
			if (foundRoute && foundDynamicRoute)
            {
                return lstResolvedPages;
            }

			return null;

		}


		private static ResolvedPage ResolvePage(
			ref Dictionary<string, AgilityRouteCacheItem> routes, 
			AgilityRouteCacheItem routeItem, 
			string dynamicRoute, 
			string languageCode, 
			string websiteName,
			ref List<ResolvedPage> resolvedPages,
			string url) 
		{


			AgilityPage page = BaseCache.GetPageFromID(routeItem.PageID, languageCode, websiteName, url);

			

			if (string.IsNullOrEmpty(dynamicRoute))
			{
				//make sure we have the page, if not, then kick out
				if (page == null) return null;

				if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
					|| !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
				{
					//we have NO dynamic route, but the page is dynamic, kick out
					return null;
				}

				//not a dynamic page (a static page nested under a dynamic one...)
				return new ResolvedPage(page, null);
			}
			else
			{
				//resolve the Dynamic Route

				if (string.IsNullOrEmpty(routeItem.ChildDynamicPagePath))
				{
					//no dynamic page attached to this static page.. kick out
					return null;
				}

				//get the route item for the child dynamic page
				if (!routes.TryGetValue(routeItem.ChildDynamicPagePath, out routeItem))
				{
					return null;
				}

			

				//get the dynamic page
				page = BaseCache.GetPageFromID(routeItem.PageID, languageCode, websiteName, url);

				if (page == null)
				{
					Agility.Web.Tracing.WebTrace.WriteWarningLine(string.Format("Routing: could not load page with id: {0} in language {1} websitename {2} url {3}", routeItem.PageID, languageCode, websiteName, url));
					return null;
				}


				if ( string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
					&& string.IsNullOrEmpty(page.DynamicPageParentFieldName))
				{
					//we have a dynamic route, but the page is not dynamic, kick out
					return null;
				}

				string contentReferenceName = page.DynamicPageContentViewReferenceName;
				int parentContentID = -1;
				if (string.IsNullOrEmpty(contentReferenceName))
				{
					if (!string.IsNullOrEmpty(page.DynamicPageParentFieldName))
					{

						for (int i = resolvedPages.Count - 1; i >= 0; i--)
						{							
							DynamicPageFormulaItem dpParent = resolvedPages[i].DynamicPageItem;
							if (dpParent == null) continue;

							//get the content reference name from the parent page...								
							object fieldValueObj = dpParent.GetContentItemValue(page.DynamicPageParentFieldName);
							if (fieldValueObj != null)
							{
								contentReferenceName = fieldValueObj as string;
								break;
							}
						}
					}
				}
				else if (!string.IsNullOrEmpty(page.DynamicPageParentFieldName))
				{
					//filter the dynamic page list by the parent field...
					for (int i = resolvedPages.Count - 1; i >= 0; i--)
					{
						DynamicPageFormulaItem dpParent = resolvedPages[i].DynamicPageItem;
						if (dpParent == null) continue;
						if (dpParent != null)
						{
							parentContentID = dpParent.ContentID;
							break;
						}
					}
					if (parentContentID < 1) return null;
					
				}

				//if we didn't resolve a content view, kick out...
				if (string.IsNullOrEmpty(contentReferenceName)) return null;

				//create/update the dynamic page index with this page id and dynamic page list reference name
				BaseCache.UpdateDynamicPageIndex(page.ID, contentReferenceName);

				//get the content first if we are in development or staging mode...
				if (AgilityContext.CurrentMode == Enum.Mode.Staging || Agility.Web.Configuration.Current.Settings.DevelopmentMode)
				{
					AgilityContentServer.AgilityContent content = BaseCache.GetContent(contentReferenceName, page.LanguageCode, AgilityContext.WebsiteName);
				}

				//get the dynamic page list dictionary for this page (this method also updates if neccessary)...
				DynamicPageFormulaItemIndex dpIndex = BaseCache.GetDynamicPageFormulaIndex(page.ID, contentReferenceName, page.LanguageCode, page, true);
				
				DynamicPageFormulaItem dpItem = null;
				if (dpIndex == null || !dpIndex.TryGetValue(dynamicRoute, out dpItem))
				{					
					//check if friendly-making the route would result in a match
					string friendly = DynamicPageFormulaItem.MakePathFriendly(dynamicRoute);
					if (friendly == dynamicRoute || !dpIndex.TryGetValue(friendly, out dpItem))
					{
						//we didn't find the item, and making the given path friendly didn't work either. Kick out.
						return null;
					}

					//there's an item, but only after fixing the path. redirect.
					string baseUrl = Data.GetSiteMapNode(page.ID).PagePath;
					string redirectUrl = baseUrl.LastIndexOf('/') > 0
										   ? baseUrl.Substring(0, baseUrl.LastIndexOf('/')) + friendly
										   : friendly;
					if (url.ToLower().EndsWith(".aspx"))
					{
						redirectUrl += ".aspx";
					}

					//add the current query string..
					string query = AgilityContext.HttpContext.Request.QueryString.Value;
					if (!string.IsNullOrEmpty(query))
					{
						redirectUrl += query;
					}
					
					//set the url to get this page, and the ActionResult or HttpModule will 301 redirect to it.
					ResolvedPage resolvedPage = new ResolvedPage(page, dpItem);
					resolvedPage.RedirectURL = redirectUrl;
					return resolvedPage;

				}

				//check parent content if neccessary  
				if (parentContentID > 0
					&& !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
				{
					object testParentContentIDObj = dpItem.GetContentItemValue(page.DynamicPageParentFieldName);

					if (testParentContentIDObj != null)
					{
						int testParentContentID = -1;
						string testParentContentIDStr = string.Format("{0}", testParentContentIDObj);

						if (int.TryParse(testParentContentIDStr, out testParentContentID))
						{
							//if the value is an int, test for equality...								
							if (parentContentID != testParentContentID)
							{
								return null;
							}
						}
						else
						{
							//value is NOT an int, test for "in" '[id],' or ',[id],' or ',[id]'
							if (!testParentContentIDStr.StartsWith(string.Format("{0},", parentContentID))
								&& !testParentContentIDStr.EndsWith(string.Format(",{0}", parentContentID))
								&& !testParentContentIDStr.Contains(string.Format(",{0},", parentContentID)))
							{
								return null;
							}
						}
					}
				}

				return new ResolvedPage(page, dpItem);
			}
			

		}


		private static void RedirectToDynamicPreviewUrl(Dictionary<string, AgilityRouteCacheItem> routes, string path, string languageCode) 
		{

			List<string> urlPaths = new List<string>();

			string[] pathParts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			string staticRoute = string.Empty;
			
			AgilityRouteCacheItem routeItem = null;

			//keep some parent objects...
			AgilityPage parentPage = null;
			DynamicPageFormulaItem dpItemParent = null;
			AgilityContent parentContent = null;

			int previewContentID = -1;
			int previewVersionID = -1;
			if (!int.TryParse(AgilityContext.HttpContext.Request.Query["previewContentID"], out previewContentID))
			{
				if (!int.TryParse(AgilityContext.HttpContext.Request.Query["previewVersionID"], out previewVersionID))
				{
					int.TryParse(AgilityContext.HttpContext.Request.Query["ContentID"], out previewContentID);
				}
			}

			foreach (string pathPart in pathParts)
			{
				string testStaticRoute = string.Format("{0}/{1}", staticRoute, pathPart);

				if (routes.TryGetValue(testStaticRoute, out routeItem))
				{

					int pageID = routeItem.PageID;

					

					//get the page...
					AgilityPage page = BaseCache.GetPageFromID(pageID, languageCode, AgilityContext.WebsiteName, null);
					if (page == null)
					{
						//if the page is null at this point, assume it's a folder page and jsut insert the static path...
						urlPaths.Add(pathPart);
						staticRoute = testStaticRoute;
						continue;
					}

					string contentReferenceName = null;
					int parentContentID = -1;

					

					if (string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
						&& string.IsNullOrEmpty(page.DynamicPageParentFieldName))
					{
						//static page...
						urlPaths.Add(pathPart);
					}
					else 
					{
						//dynamic page..
						if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName))
						{

							contentReferenceName = page.DynamicPageContentViewReferenceName;

							if (!string.IsNullOrEmpty(page.DynamicPageParentFieldName))
							{
								if (parentPage == null || dpItemParent == null) return;

								//we have to match up to the parent page...
								parentContentID = dpItemParent.ContentID;

							}


						}
						else if (!string.IsNullOrEmpty(page.DynamicPageParentFieldName))
						{
							//get the dynamic content from the PARENT page
							if (parentPage == null || dpItemParent == null) return;

							object obj = dpItemParent.GetContentItemValue(page.DynamicPageParentFieldName);
							if (obj != null)
							{
								contentReferenceName = string.Format("{0}", obj);
							}

						}

						if (string.IsNullOrEmpty(contentReferenceName)) return;


						//get the content first if we are in development or staging mode...
						AgilityContentServer.AgilityContent content = BaseCache.GetContent(contentReferenceName, languageCode, AgilityContext.WebsiteName);


						//get the dynamic content for this page
						DynamicPageFormulaItemIndex dpIndex = BaseCache.GetDynamicPageFormulaIndex(pageID, contentReferenceName, languageCode, page, true);
						if (dpIndex == null || dpIndex.Count == 0) return;


						DynamicPageFormulaItem dpItem = null;


						//try to use the preview url
						if (previewVersionID > 0 || previewContentID > 0)
						{
							DataRow row = null;
							if (previewContentID > 0)
							{
								row = content.GetItemByContentID(previewContentID);								
							}
							else
							{
								DataRow[] rows = content.DataSet.Tables["ContentItems"].Select(string.Format("VersionID = {0}", previewVersionID));
								if (rows.Length > 0) row = rows[0];
							}

							if (row != null)
							{
								string pageName = string.Format("/{0}", DynamicPageFormulaItem.ResolveFormula(page.DynamicPageName, row, true)).ToLowerInvariant();
								if (dpIndex.TryGetValue(pageName, out dpItem))
								{

									//adjust the parent if we need to...
									if (!string.IsNullOrEmpty(page.DynamicPageParentFieldName) && dpItemParent != null)
									{
										//grab the parent id...
										object parentIDObj = dpItem.GetContentItemValue(page.DynamicPageParentFieldName);
										if (parentIDObj != null)
										{
											string parentIDStr = string.Format("{0}", parentIDObj);
											if (parentIDStr.IndexOf(",") != -1) parentIDStr = parentIDStr.Substring(0, parentIDStr.IndexOf(","));
											int adjustedParentID = -1;
											if (int.TryParse(parentIDStr, out adjustedParentID))
											{
												if (dpItemParent.ContentID != adjustedParentID)
												{
													// if the parent id DOES NOT match, we need to do some URL jigging...
													DataRow parentRow = parentContent.GetItemByContentID(adjustedParentID);
													
                                                    parentContentID = adjustedParentID;
													if (parentRow != null)
													{
														//get the parent page name and switch it up in the url..
														string parentPageName = string.Format("/{0}", DynamicPageFormulaItem.ResolveFormula(parentPage.DynamicPageName, parentRow, true)).ToLowerInvariant();
														urlPaths[urlPaths.Count - 1] = parentPageName.Substring(1);

													}
												}
											}
										}
									}
								}

							}
						}


						if (dpItem == null)
						{
							//use the first one if we don't have an item yet...
							dpItem = dpIndex.Values.FirstOrDefault();
						}


                        if (parentContentID > 1 && previewVersionID < 1 && previewContentID < 1)
						{
							//unless we have a parent id to follow...

							foreach (DynamicPageFormulaItem item in dpIndex.Values)
							{


								object testParentContentIDObj = item.GetContentItemValue(page.DynamicPageParentFieldName);
								if (testParentContentIDObj != null)
								{
									int testParentContentID = -1;
									string testParentContentIDStr = string.Format("{0}", testParentContentIDObj);

									if (int.TryParse(testParentContentIDStr, out testParentContentID))
									{
										//if the value is an int, test for equality...								
										if (parentContentID == testParentContentID)
										{
											dpItem = item;
											break;
										}
									}
									else
									{
										//value is NOT an int, test for "in" '[id],' or ',[id],' or ',[id]'
										if (testParentContentIDStr.StartsWith(string.Format("{0},", parentContentID))
											|| testParentContentIDStr.EndsWith(string.Format(",{0}", parentContentID))
											|| testParentContentIDStr.Contains(string.Format(",{0},", parentContentID)))
										{
											dpItem = item;
											break;
										}
									}
								}
							}

						}

						if (dpItem == null) return;


						urlPaths.Add(dpItem.Name.TrimStart("/".ToCharArray()));
						dpItemParent = dpItem;
						parentContent = content;

					}

					staticRoute = testStaticRoute;
					parentPage = page;
					

				}
				else
				{
					//if we can't find a static route, kick out.
					return;
				}

				
			}

			if (urlPaths.Count > 0)
			{
				StringBuilder sbUrl = new StringBuilder("~/");
				sbUrl.Append(string.Join("/", urlPaths.ToArray()));

				if (AgilityContext.HttpContext.Request.Path.Value.EndsWith(".aspx", StringComparison.CurrentCultureIgnoreCase)) sbUrl.Append(".aspx");
				if (!string.IsNullOrEmpty(AgilityContext.HttpContext.Request.QueryString.Value))
				{
					sbUrl.Append(AgilityContext.HttpContext.Request.QueryString.Value);
				}
				
				string redirectUrl = sbUrl.ToString();

				//strip off the querystrings that we don't want...
				redirectUrl = Agility.Web.Util.Url.RemoveQueryString(redirectUrl, "ContentID&VersionID&previewContentID&previewVersionID");


				Agility.Web.HttpModules.AgilityHttpModule.RedirectResponse(redirectUrl, 301);

				AgilityContext.IsResponseEnded = true;
			}
			
		}

		public static void UpdateDynamicPageIndex(AgilityPage page)
		{
			if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
											|| !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
			{
				if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName.ToLowerInvariant()))
				{
					BaseCache.UpdateDynamicPageIndex(page.ID, page.DynamicPageContentViewReferenceName.ToLowerInvariant());					
				}

				//update the Dynamic Page Formula Index that use this page...
				BaseCache.ClearDynamicDynamicPageFormulaIndex(page);
			}
		}

		public static void UpdateDynamicPageFormFormulas(AgilityContent existingContent, AgilityContent deltaContent)
		{

			if (deltaContent == null || deltaContent.DataSet == null)
			{
				
				return;
			}


		

			Dictionary<string, List<int>> dpIndex = BaseCache.GetDynamicPageIndex();

			List<int> lstPageIDs = null;
			if (dpIndex.TryGetValue(deltaContent.ReferenceName.ToLowerInvariant(), out lstPageIDs))
			{

				foreach (int pageID in lstPageIDs)
				{

					AgilityPage page = BaseCache.GetPageFromID(pageID, deltaContent.LanguageCode, AgilityContext.WebsiteName, null);
					if (page != null)
					{
						//update all of the DynamicPageIndexes that this content appears on...
						BaseCache.UpdateDynamicPageFormulaIndex(page, existingContent, deltaContent, deltaContent.ReferenceName, existingContent == null);
					} 
				}
			}			

		}

	}
}
