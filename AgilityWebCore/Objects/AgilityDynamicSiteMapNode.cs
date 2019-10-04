using System;
using System.Collections.Generic;

namespace Agility.Web.Objects
{
	public class AgilityDynamicSiteMapNode : AgilitySiteMapNode
	{
		public AgilityDynamicSiteMapNode(string key, string url, string text)
			: base(key, url, text)
		{ }


		public string ReferenceName { get; set; }
		public int ContentID { get; set; }

		internal static AgilityDynamicSiteMapNode GetDynamicNode(AgilitySiteMapNode parentNode, Agility.Web.AgilityContentServer.DynamicPageFormulaItem pageFormulaItem, AgilityPage page)
		{
			string nodeID = string.Format("{0}_{1}", parentNode.Key, pageFormulaItem.ContentID);

			string menuText = pageFormulaItem.MenuText;
			string pageName = pageFormulaItem.Name;

			string url = parentNode.ParentNode.PagePath;
			if (string.IsNullOrEmpty(url)) url = parentNode.ParentNode.Url;


			int index = url.LastIndexOf(".aspx", StringComparison.CurrentCultureIgnoreCase);
			if (index > 0) url = url.Substring(0, index);

			bool addAspx = true;
			if (AgilityContext.Domain != null && AgilityContext.Domain.ExtensionlessUrls)
			{
				addAspx = false;
			}

			string urlPath = string.Format("{0}/{1}", url, pageName);
			url = string.Format("{0}/{1}", url, pageName);
			if (addAspx)
			{
				url = string.Format("{0}.aspx", url);
			}
			if (url.StartsWith("/")) url = string.Format("~{0}", url);

			AgilityDynamicSiteMapNode node = new AgilityDynamicSiteMapNode(nodeID, url, menuText);

			node.ReferenceName = pageFormulaItem.ContentReferenceName;
			node.ContentID = pageFormulaItem.ContentID;

			//override the visibility attributes...
			node.SitemapVisible = pageFormulaItem.VisibleOnSitemap;
			node.MenuVisible = pageFormulaItem.VisibleOnMenu;
			
			node.ParentNode = parentNode.ParentNode;
			node.PageItemID = parentNode.PageItemID;
			node.ChildNodes = new List<AgilitySiteMapNode>();
			

			foreach (AgilitySiteMapNode parentChildNode in parentNode.ChildNodes)
			{
				AgilitySiteMapNode childNode = parentChildNode.Copy(urlPath, node);				
				node.ChildNodes.Add(childNode);
			}
			
			return node;
		}


		public override AgilitySiteMapNode Copy(string urlPath, AgilitySiteMapNode newParentNode)
		{
			AgilityDynamicSiteMapNode copyNode = (AgilityDynamicSiteMapNode)base.Copy(urlPath, newParentNode);
			copyNode.ReferenceName = ReferenceName;
			return copyNode;
		}
		
	}

	
}
