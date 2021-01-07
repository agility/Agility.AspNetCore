using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Web;

using System.Security.Permissions;
using System.Collections.Specialized;
using Agility.Web.AgilityContentServer;
using Agility.Web.Tracing;
using Agility.Web.Objects;
using System.IO;
using System.Data;
using Agility.Web.Routing;
using AgilityPage = Agility.Web.Objects.AgilityPage;
using Agility.Web.Caching;
using Microsoft.AspNetCore.Http.Extensions;

namespace Agility.Web
{

	public class AgilitySiteMap
	{

		Dictionary<string, string> _tmpAddedNodes = new Dictionary<string, string>();


		private XmlDocument menuXml
		{
			get
			{

				string key = string.Format("Agility.Web.Providers.AgilitySiteMapProvider.menuXml.{0}.{1}", AgilityContext.LanguageCode, AgilityContext.WebsiteName);
				XmlDocument document = AgilityContext.HttpContext.Items[key] as XmlDocument;

				if (document == null)
				{

					//get the sitemap object for the current domain
					AgilityContentServer.AgilitySitemap sitemap = BaseCache.GetSitemap(AgilityContext.LanguageCode, AgilityContext.WebsiteName);
					document = new XmlDocument();
					if (sitemap != null)
					{
						document.LoadXml(sitemap.SitemapXml);
						AgilityContext.HttpContext.Items[key] = document;
					}

				}

				return document;
			}
		}


		public AgilitySiteMapNode RootNode
		{
			get
			{


				AgilitySiteMapNode node = CachedRootNode;

				if (node != null) return node;

				lock (_lockObj)
				{

					//check to see if the cached node has been set while we were waiting...
					node = CachedRootNode;
					if (node != null) return node;

					//generate the sitemap
					node = GenerateAgilitySitemap();

					//add the root node to cache
					CachedRootNode = node;


					return node;
				}

			}
		}


		public AgilitySiteMapNode CurrentNode
		{
			get
			{
				string cacheKey = "AgilitySitemapProvider.CurrentNode";
				AgilitySiteMapNode node = null;

				if (AgilityContext.HttpContext != null)
				{
					node = AgilityContext.HttpContext.Items[cacheKey] as AgilitySiteMapNode;
				}

				if (node != null) return node;

                var rawUrl = AgilityContext.HttpContext?.Request.Path;
                node = FindSiteMapNode(rawUrl);

                if (node != null && AgilityContext.HttpContext != null)
                {
                    AgilityContext.HttpContext.Items[cacheKey] = node;
                    return node;
				}

                AgilityPage page = AgilityContext.Page;
				AgilitySiteMapNode parentNode = null;
				
                if (page != null)
				{
					node = FindSiteMapNodeFromKey(page.ID.ToString());
					if (node != null)
					{
						parentNode = node.ParentNode;

						//traverse up the tree until the first static page is found
						//for most dynamic pages -- parentNode will be the exit point
						while (parentNode != null)
						{
							var aNode = parentNode as AgilitySiteMapNode;
							if (aNode != null
								&& aNode.AgilityPage != null
								&& (
									!string.IsNullOrEmpty(aNode.AgilityPage.ServerPage.DynamicPageContentViewReferenceName)
									|| !string.IsNullOrEmpty(aNode.AgilityPage.ServerPage.DynamicPageParentFieldName)
								)
							)
							{
								parentNode = aNode.ParentNode;
							}
							else
							{
								break;
							}

						}

					}


					if (parentNode == null) parentNode = RootNode;

					//check if this is a dynamic page... if so, we need to load up the child items for the parent...
					if (AgilityContext.DynamicPageItemRow != null)
					{

						int contentID = -1;
						if (!int.TryParse($"{AgilityContext.DynamicPageItemRow["ContentID"]}", out contentID)) contentID = -1;

						//do a search for this node, starting at the root...
						node = FindDynamicNodeByContentID(parentNode, contentID);

						//now, we may need to keep searching if the CURRENT node is actually a static page UNDERNEATH the dynamic node..
						AgilitySiteMapNode anode = node as AgilitySiteMapNode;
						if (anode != null && anode.PageItemID != page.ID)
						{
							//now we have to search
							node = FindAgilityNodeByPageID(anode, page.ID);
						}

					}

					if (node != null && AgilityContext.HttpContext != null)
					{
						AgilityContext.HttpContext.Items[cacheKey] = node;
					}

					return node;
				}
				return null;
			}
		}


		protected virtual string Cachekey
		{
			get
			{
				string key = string.Format("Agility.Web.Providers.AgilitySiteMapProvider.cachedRootNode_{0}_{1}_{2}_{3}", AgilityContext.CurrentMode, AgilityContext.LanguageCode, AgilityContext.WebsiteName, AgilityContext.CurrentChannel.ID);
				return key;
			}
		}



		/// <summary>
		/// Finds a dynamic sitemap node.
		/// </summary>
		/// <param name="parentNode"></param>
		/// <param name="contentID"></param>
		/// <returns></returns>
		public AgilitySiteMapNode FindDynamicNodeByContentID(AgilitySiteMapNode parentNode, int contentID)
		{

			foreach (AgilitySiteMapNode childNode in parentNode.ChildNodes)
			{
				var dynNode = childNode as AgilityDynamicSiteMapNode;
				if (dynNode != null && dynNode.ContentID == contentID) return dynNode;



				AgilitySiteMapNode testNode = FindDynamicNodeByContentID(childNode, contentID);
				if (testNode != null) return testNode;

			}

			return null;
		}


		private AgilitySiteMapNode CachedRootNode
		{
			get
			{
				string key = Cachekey;
				if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging || AgilityContext.IsPreview)
				{
					//attempt to get from context
					AgilitySiteMapNode o = AgilityContext.HttpContext.Items[key] as AgilitySiteMapNode;
					if (o != null) return o;
				}


				//attempt to get the from cache
				AgilitySiteMapNode obj = AgilityCache.Get(key) as AgilitySiteMapNode;
				if (obj != null) return obj;


				return null;
			}
			set
			{
				string key = Cachekey; // string.Format("Agility.Web.Providers.AgilitySiteMapProvider.cachedRootNode_{0}_{1}_{2}", AgilityContext.CurrentMode, AgilityContext.LanguageCode, AgilityContext.WebsiteName);

				//if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging || AgilityContext.IsPreview)
				//{
				//	HttpContext.Current.Items[key] = value;
				//}


				if (value == null)
				{
					AgilityCache.Remove(key);
				}
				else
				{

					//use a dependance on the Sitemap cache object
					AgilityContentServer.AgilityItemKey itemKey = new AgilityContentServer.AgilityItemKey();
					itemKey.Key = BaseCache.ITEMKEY_SITEMAP;
					itemKey.LanguageCode = AgilityContext.LanguageCode;
					itemKey.ItemType = typeof(AgilityContentServer.AgilitySitemap).Name;

					string cacheKey = BaseCache.GetCacheKey(itemKey);


					CacheDependency cd = new CacheDependency(null, new string[] { cacheKey });
					if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging || AgilityContext.IsPreview)
					{
						string filename = BaseCache.GetFilePathForItemKey(itemKey, AgilityContext.WebsiteName, transientPath: true);
						if (File.Exists(filename))
						{
							cd = new CacheDependency(new string[] { filename }, null);
						}
					}

					try
					{
						AgilityContext.HttpContext.Items[key] = value;
						AgilityCache.Set(key, value, TimeSpan.FromDays(1), cd, AgilityContext.DefaultCachePriority);
					}
					catch
					{


					}
				}
				//}
			}
		}


		/// <summary>
		/// Default constructor.
		/// </summary>
		public AgilitySiteMap() { }


		private static object _lockObj = new object();


		protected virtual AgilitySiteMapNode GenerateAgilitySitemap()
		{
			// Since the SiteMap class is static, make sure that it is
			// not modified while the site map is built.


			AgilitySiteMapNode _rootNode = null;


			WebTrace.WriteInfoLine(string.Format("Building Sitemap: {0}, {1}, {2}, {3}", AgilityContext.CurrentMode, AgilityContext.LanguageCode, AgilityContext.WebsiteName, AgilityContext.CurrentChannel.ReferenceName));

			// If there is no root node, then there is no site map.

			// Start with a clean slate
			_tmpAddedNodes.Clear();


			if (menuXml != null)
			{
				//THE ROOT NODE
				_rootNode = new AgilitySiteMapNode(string.Empty, string.Empty, string.Empty);
				_rootNode.ParentNode = null;
			}
			else
			{
				return null;
			}

			if (menuXml.DocumentElement == null) return null;

			//get the xml element that represents this channel

			XmlNode channelElem = menuXml.DocumentElement.SelectSingleNode(string.Format("//ChannelNode[@channelID='{0}']", AgilityContext.CurrentChannel.ID));


			if (channelElem == null)
			{
				//if we have a channel in the XML, use the first one...
				channelElem = menuXml.DocumentElement.SelectSingleNode("ChannelNode");
			}

			if (channelElem == null)
			{
				channelElem = menuXml.DocumentElement;
			}

			XmlNodeList childNodes = channelElem.SelectNodes("SiteNode");
			foreach (XmlElement elem in childNodes)
			{
				AgilitySiteMapNode childNode = ConvertXmlElementToSiteMapNode(elem, null);
                if (childNode == null) continue;

                //if the child node wasn't excluded via timed release
                var agilitySiteMapNodes = AddNode(childNode, _rootNode);

                foreach (var agilitySiteMapNode in agilitySiteMapNodes)
                {
                    //add it's children
                    AddChildNodes(agilitySiteMapNode,elem);
                }
            }

			return _rootNode;

		}




		/// <summary>
		/// Recursively add nodes to the SiteMap from the XML File
		/// </summary>
		/// <param name="parentNode"></param>
		/// <param name="elem"></param>
		private void AddChildNodes(AgilitySiteMapNode parentNode, XmlElement elem)
		{
			XmlNodeList childNodes = elem.SelectNodes("SiteNode");
			foreach (XmlElement childElem in childNodes)
			{
				AgilitySiteMapNode childNode = ConvertXmlElementToSiteMapNode(childElem, parentNode);
				var agilitySiteMapNodes = AddNode(childNode, parentNode);

                foreach (var agilitySiteMapNode in agilitySiteMapNodes)
                {
					AddChildNodes(agilitySiteMapNode, childElem);
                }
            }
		}


		public AgilitySiteMapNode FindSiteMapNode(string rawUrl)
		{

			AgilitySiteMapNode parentNode = RootNode as AgilitySiteMapNode;
			if (parentNode == null) return null;
			return FindAgilityNodeByUrl(parentNode, rawUrl);


		}

		public AgilitySiteMapNode FindSiteMapNodeFromKey(string key)
		{
			AgilitySiteMapNode node = RootNode as AgilitySiteMapNode;
			if (node == null) return null;
			return FindAgilityNodeByKey(node, key);
		}


		protected virtual AgilitySiteMapNode FindAgilityNodeByUrl(AgilitySiteMapNode parentNode, string rawUrl)
		{
			AgilitySiteMapNode node = null;

			rawUrl = rawUrl.ToLower();

			string appPath = "/"; //TODO: handle application path better HttpContext.Current.Request.ApplicationPath.ToLower();
			if (appPath != "/" && rawUrl.StartsWith(appPath))
			{
				rawUrl = rawUrl.Replace(appPath, "~");
			}
			else if (appPath == "/" && rawUrl.StartsWith("/"))
			{
				rawUrl = "~" + rawUrl;
			}

			if (rawUrl.StartsWith("~/")) rawUrl = rawUrl.Substring(1);
			if (!rawUrl.StartsWith("/")) rawUrl = string.Format("/{0}", rawUrl);
			if (rawUrl.EndsWith("/")) rawUrl = rawUrl.TrimEnd('/');

			if (rawUrl.EndsWith(".aspx", StringComparison.CurrentCultureIgnoreCase)) rawUrl = rawUrl.Substring(0, rawUrl.LastIndexOf(".aspx", StringComparison.CurrentCultureIgnoreCase));


			Dictionary<string, AgilityRouteCacheItem> routes = AgilityRouteTable.GetRouteTable(AgilityContext.LanguageCode, AgilityContext.CurrentChannel.ID);
			AgilityRouteCacheItem routeItem = null;

			if (routes.TryGetValue(rawUrl, out routeItem))
			{
				node = FindAgilityNodeByKey(parentNode, routeItem.PageID.ToString());
			}



			return node;
		}

		protected virtual AgilitySiteMapNode FindAgilityNodeByPageID(AgilitySiteMapNode parentNode, int pageID)
		{
			if (parentNode.PageItemID == pageID) return parentNode;

			foreach (AgilitySiteMapNode node in parentNode.ChildNodes)
			{
				if (node.PageItemID == pageID) return node;
				AgilitySiteMapNode foundNode = FindAgilityNodeByPageID(node, pageID);
				if (foundNode != null) return foundNode;
			}

			return null;

		}

		protected virtual AgilitySiteMapNode FindAgilityNodeByKey(AgilitySiteMapNode parentNode, string key)
		{
			if (parentNode.Key == key) return parentNode;

			foreach (AgilitySiteMapNode node in parentNode.ChildNodes)
			{
				if (node.Key == key) return node;
				AgilitySiteMapNode foundNode = FindAgilityNodeByKey(node, key);
				if (foundNode != null) return foundNode;
			}

			return null;

		}

		protected virtual void InsertNode(AgilitySiteMapNode node, AgilitySiteMapNode parentNode, int index)
		{
			if (_tmpAddedNodes.ContainsKey(node.Key)) return;

			_tmpAddedNodes.Add(node.Key, node.Key);

			AgilitySiteMapNode anode = node as AgilitySiteMapNode;
			AgilitySiteMapNode pnode = parentNode as AgilitySiteMapNode;
			if (anode != null && pnode != null)
			{
				anode.ParentNode = pnode;
				pnode.ChildNodes.Insert(index, anode);
			}
		}


		protected List<AgilitySiteMapNode> AddNode(AgilitySiteMapNode node, AgilitySiteMapNode parentNode)
		{
			if (node == null) return new List<AgilitySiteMapNode>();
			if (_tmpAddedNodes.ContainsKey(node.Key)) return new List<AgilitySiteMapNode>();

			AgilitySiteMapNode pnode = parentNode as AgilitySiteMapNode;

            if (pnode == null) return new List<AgilitySiteMapNode>();

            node.ParentNode = pnode;

            string dynamicPageContentReferenceName = node.DynamicPageContentReferenceName;
            string dynamicPageParentFieldName = node.DynamicPageParentFieldName;

            if (!string.IsNullOrEmpty(dynamicPageContentReferenceName)
                || !string.IsNullOrEmpty(dynamicPageParentFieldName))
            {
                //if this node is a dynamic page, expand it
                List<AgilitySiteMapNode> col = new List<AgilitySiteMapNode>();
                col = GetDynamicChildNodes(node, parentNode, col);
                pnode.ChildNodes.AddRange(col);

                return col;
            }
            else
            {
                //it's a regular one...
                _tmpAddedNodes.Add(node.Key, node.Key);
                pnode.ChildNodes.Add(node);
                var list =  new List<AgilitySiteMapNode>();
                list.Add(node);
                return list;

            }


        }


		private List<AgilitySiteMapNode> GetDynamicChildNodes(AgilitySiteMapNode anode, AgilitySiteMapNode parentNode, List<AgilitySiteMapNode> collection)
		{


			string dynamicPageContentReferenceName = anode.DynamicPageContentReferenceName;
			string dynamicPageParentFieldName = anode.DynamicPageParentFieldName;

			if (string.IsNullOrEmpty(dynamicPageContentReferenceName) && string.IsNullOrEmpty(dynamicPageParentFieldName)) return collection;


			//the child pages are dynamic pages...
			AgilityPage page = anode.AgilityPage;
			int parentContentID = 0;

			if (page != null)
			{
				string contentReferenceName = dynamicPageContentReferenceName;

				if (string.IsNullOrEmpty(contentReferenceName))
				{

					AgilityDynamicSiteMapNode dpNode = anode.ParentNode as AgilityDynamicSiteMapNode;

					if (dpNode == null)
					{

						AgilitySiteMapNode pnode = parentNode;

						while (pnode != null)
						{
							dpNode = pnode.ParentNode as AgilityDynamicSiteMapNode;
							if (dpNode != null) break;
							pnode = pnode.ParentNode;
						}
					}

					if (!string.IsNullOrEmpty(dynamicPageParentFieldName) && dpNode != null && !string.IsNullOrEmpty(dpNode.ReferenceName))
					{
						//get the content reference name from the parent page...
						AgilityContentServer.AgilityContent parentContent = BaseCache.GetContent(dpNode.ReferenceName, AgilityContext.LanguageCode, AgilityContext.WebsiteName);
						if (parentContent != null
							&& parentContent.DataSet != null
							&& parentContent.DataSet.Tables["ContentItems"] != null
							&& parentContent.DataSet.Tables["ContentItems"].Columns.Contains(dynamicPageParentFieldName))
						{

							DataRow row = parentContent.GetItemByContentID(dpNode.ContentID);

							if (row != null)
							{
								//the contentReferenceName is stored in the field value...
								contentReferenceName = row[dynamicPageParentFieldName] as string;
							}
						}

					}

				}
				else if (!string.IsNullOrEmpty(dynamicPageParentFieldName))
				{
					//filter the dynamic page list by the parent field...
					AgilityDynamicSiteMapNode dpNode = anode.ParentNode as AgilityDynamicSiteMapNode;

					if (dpNode == null)
					{
						AgilitySiteMapNode pnode = parentNode;

						while (pnode != null)
						{
							dpNode = pnode.ParentNode as AgilityDynamicSiteMapNode;
							if (dpNode != null) break;
							pnode = pnode.ParentNode;
						}
					}

					parentContentID = -1;
					if (!string.IsNullOrEmpty(dynamicPageParentFieldName) && dpNode != null && !string.IsNullOrEmpty(dpNode.ReferenceName))
					{
						parentContentID = dpNode.ContentID;
					}

				}


				if (!string.IsNullOrEmpty(contentReferenceName))
				{

					//add this page and reference name to the page/dynamic content index
					BaseCache.UpdateDynamicPageIndex(page.ID, contentReferenceName);

					//get the content first
					DataView dv = Data.GetContentView(contentReferenceName);

					//get the dynamic page formula index
					Dictionary<string, Agility.Web.AgilityContentServer.DynamicPageFormulaItem> dpIndex = BaseCache.GetDynamicPageFormulaIndex(page.ID, contentReferenceName, AgilityContext.LanguageCode, page.ServerPage, true);

					if (dpIndex != null && dv != null)
					{
						//make an ID based index
						Dictionary<int, Agility.Web.AgilityContentServer.DynamicPageFormulaItem> idIndex = new Dictionary<int, DynamicPageFormulaItem>();


						foreach (var item in dpIndex.Values)
						{
							idIndex[item.ContentID] = item;
						}


						//loop all of the dynamic pages....
						foreach (DataRowView dvr in dv)
						{

							DynamicPageFormulaItem item = null;

							int contentID = -1;
							if (!int.TryParse($"{dvr["ContentID"]}", out contentID)) contentID = -1;

							if (!idIndex.TryGetValue(contentID, out item)) continue;

							if (parentContentID != 0)
							{
								//do a lookup to ensure the parent id condition is met if necessary

								object testParentContentIDObj = item.GetContentItemValue(dynamicPageParentFieldName);
								if (testParentContentIDObj != null)
								{
									int testParentContentID = -1;
									string testParentContentIDStr = string.Format("{0}", testParentContentIDObj);

									if (int.TryParse(testParentContentIDStr, out testParentContentID))
									{
										//if the value is an int, test for equality...
										if (parentContentID != testParentContentID)
										{
											continue;
										}
									}
									else
									{
										//value is NOT an int, test for "in" '[id],' or ',[id],' or ',[id]'
										if (!testParentContentIDStr.StartsWith(string.Format("{0},", parentContentID))
											&& !testParentContentIDStr.EndsWith(string.Format(",{0}", parentContentID))
											&& !testParentContentIDStr.Contains(string.Format(",{0},", parentContentID)))
										{
											continue;
										}
									}
								}
								else
								{
									continue;
								}
							}

							AgilityDynamicSiteMapNode thisDNode = AgilityDynamicSiteMapNode.GetDynamicNode(anode, item, page);

							collection.Add(thisDNode);
						}


					}

				}
			}

			return collection;


		}

		public List<AgilitySiteMapNode> GetChildNodes(AgilitySiteMapNode node)
		{
			AgilitySiteMapNode anode = node as AgilitySiteMapNode;



			if (anode != null)
			{
				List<AgilitySiteMapNode> col = new List<AgilitySiteMapNode>();

				for (int i = 0; i < anode.ChildNodes.Count; i++)
				{

					//check for dynamic pages...
					AgilitySiteMapNode childNode = anode.ChildNodes[i] as AgilitySiteMapNode;

					if (childNode == null)
					{
						col.Add(anode.ChildNodes[i]);
						continue;
					}

					string dynamicPageContentReferenceName = childNode.DynamicPageContentReferenceName;
					string dynamicPageParentFieldName = childNode.DynamicPageParentFieldName;

					if (!string.IsNullOrEmpty(dynamicPageContentReferenceName)
						|| !string.IsNullOrEmpty(dynamicPageParentFieldName))
					{
						col = GetDynamicChildNodes(childNode, anode, col);
					}
					else
					{
						col.Add(childNode);
					}

				}


				return col;
			}

			return new List<AgilitySiteMapNode>();

		}


		public AgilitySiteMapNode GetParentNode(AgilitySiteMapNode node)
		{
			if (node == null) return null;
			AgilitySiteMapNode anode = node as AgilitySiteMapNode;
			if (anode == null) return node.ParentNode;

			return anode.ParentNode;

		}

		private AgilitySiteMapNode ConvertXmlElementToSiteMapNode(XmlElement elem, AgilitySiteMapNode parentNode)
		{
			//check the release/pull date first
			if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
			{
				DateTime dtViewingDate = DateTime.Now;
				if (AgilityContext.IsPreview && AgilityContext.PreviewDateTime != DateTime.MinValue)
				{
					dtViewingDate = AgilityContext.PreviewDateTime;
				}

				//filter on release/pull date
				string releaseDateStr = elem.GetAttribute("releaseDate");
				string pullDateStr = elem.GetAttribute("pullDate");

				DateTime releaseDate = DateTime.MinValue;
				DateTime pullDate = DateTime.MinValue;

				if (DateTime.TryParse(releaseDateStr, out releaseDate) && releaseDate != DateTime.MinValue)
				{
					if (releaseDate != DateTime.MinValue && releaseDate > dtViewingDate)
					{
						//don't return the object until it is released
						return null;
					}
				}

				if (DateTime.TryParse(pullDateStr, out pullDate) && pullDate != DateTime.MinValue)
				{
					if (pullDate <= dtViewingDate)
					{
						//don't return the page object if it is pulled
						return null;
					}
				}
			}


			//do the conversion
			string url = elem.GetAttribute("NavigateURL");
			if (url == null || url == string.Empty)
			{
				url = "javascript:;";
			}
            else if(parentNode is AgilityDynamicSiteMapNode)
            {
                var partialUrl = url.Substring(url.LastIndexOf('/'));
                url = parentNode.Url + partialUrl;
            }

			//append the language code to the URL if we on a app relative url and the language module is in use.

			//TODO: DETERMINE IF WE ARE ADDING LANGUAGE CODE TO THE URL...

			//if (url.StartsWith("~/") && AgilityContext.IsUsingLanguageModule && url.IndexOf(".aspx", StringComparison.InvariantCultureIgnoreCase) != -1)
			//{
			//	url = "~/" + AgilityContext.LanguageCode + url.Substring(1);
			//}



			string id = elem.GetAttribute("picID").ToLower();
			int picID = 0;
			int.TryParse(id, out picID);

			AgilitySiteMapNode node = new AgilitySiteMapNode(id, url, elem.GetAttribute("Text"));
			node.PageItemID = picID;
			//add the other attribute values into the AgilitySiteMapNode
			//node["tempUrl"] = url;

			bool addedTarget = false;

			foreach (XmlAttribute att in elem.Attributes)
			{

				var attributeName = att.Name.ToLowerInvariant();


				switch (attributeName)
				{
					case "pagepath":
						node.PagePath = att.Value;
						break;

					case "target":
						node.Target = att.Value;
						break;

					case "menuvisible":
						bool bv = true;
						if (bool.TryParse(att.Value, out bv)) node.MenuVisible = bv;
						break;

					case "sitemapvisible":
						bool sv = true;
						if (bool.TryParse(att.Value, out sv)) node.SitemapVisible = sv;
						break;

					case "dynamicpagecontentreferencename":
						node.DynamicPageContentReferenceName = att.Value;
						break;

					case "dynamicpageparentfieldname":
						node.DynamicPageParentFieldName = att.Value;
						break;

				}

			}


			if (string.IsNullOrEmpty(node.PagePath))
			{
				node.PagePath = elem.GetAttribute("FolderPath");
			}


			return node;
		}




	}

}