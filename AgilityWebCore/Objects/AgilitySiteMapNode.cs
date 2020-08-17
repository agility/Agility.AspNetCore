using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Agility.Web.Objects
{
	public class AgilitySiteMapNode
	{
		public string Target { get; set; }

		public string Key { get; set; }

		public string Url { get; set; }

		public string Title { get; set; }

		public bool SitemapVisible { get; set; } = true;
		public bool MenuVisible { get; set; } = true;

		public string DynamicPageContentReferenceName {get; set;}
		public string DynamicPageParentFieldName { get; set; }

		public AgilitySiteMapNode(string key, string url, string text)			
		{
			this.Key = key;
			this.Url = url;
			this.Title = text;
		}


		public AgilitySiteMapNode ParentNode;
		public List<AgilitySiteMapNode> ChildNodes = new List<AgilitySiteMapNode>();
		
		private AgilityPage _agilityPage;

		/// <summary>
		/// Gets the Agility Page object that is associated with this sitemap node.
		/// </summary>
		public virtual AgilityPage AgilityPage
		{
			get 
			{
				if (_agilityPage == null && PageItemID > 0)
				{
					AgilityContentServer.AgilityPage serverPage = BaseCache.GetPageFromID(PageItemID, AgilityContext.LanguageCode, AgilityContext.WebsiteName, null);
					_agilityPage = Data.CreateAPIPageObject(serverPage);
				}
				return _agilityPage; 
			}
			
		}

		public string PagePath { get; set; }

		public int PageItemID { get; set; }

		public virtual AgilitySiteMapNode Copy(string urlPath, AgilitySiteMapNode newParentNode)
		{

			string childPageName = this.PagePath;
			if (string.IsNullOrEmpty(childPageName)) childPageName = this.Url;
			
			if (childPageName.EndsWith("/")) childPageName = childPageName.Substring(0, childPageName.Length - 1);
			int slashIndex = childPageName.LastIndexOf("/");
			if (slashIndex > 0) childPageName = childPageName.Substring(slashIndex + 1);
			string url = string.Format("{0}/{1}", urlPath, childPageName);

			string newPagePath = url;

			if (this.Url.StartsWith("javascript:", StringComparison.CurrentCultureIgnoreCase)) url = this.Url;

			AgilitySiteMapNode node = null;
			if (node is AgilityDynamicSiteMapNode)
			{

				node = new AgilityDynamicSiteMapNode(this.Key, url, this.Title);
			}
			else
			{
				node = new AgilitySiteMapNode(this.Key, url, this.Title);
			}

			node.ParentNode = newParentNode;
			node._agilityPage = this._agilityPage;
			
			node.PageItemID = this.PageItemID;
			node.PagePath = newPagePath;

			node.ChildNodes = new List<AgilitySiteMapNode>();

			urlPath = newPagePath;
			if (urlPath.IndexOf(".aspx") > 0) urlPath = urlPath.Substring(0, urlPath.IndexOf(".aspx"));

			foreach (AgilitySiteMapNode childNode in ChildNodes)
			{				
				node.ChildNodes.Add(childNode.Copy(urlPath, node));
			}

			return node;

		}
	}
}
