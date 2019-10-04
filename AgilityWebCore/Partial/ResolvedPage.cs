using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agility.Web.AgilityContentServer
{
	internal class ResolvedPage
	{
		public AgilityPage Page;
		public DynamicPageFormulaItem DynamicPageItem;

		/// <summary>
		/// The url that this page should route to be removing special chars, multi dashes, diacritics, whatever.
		/// /page/page---details, => /page/page-details
		/// /page/page's-details, => /page/pages-details
		/// </summary>
		public string RedirectURL;

		public ResolvedPage(AgilityPage page)
		{
			Page = page;
		}		

		public ResolvedPage(AgilityPage page, DynamicPageFormulaItem dynamicPageItem)
		{
			Page = page;
			DynamicPageItem = dynamicPageItem;
		}

		public override string ToString()
		{
			if (Page != null) return Page.Name;
			return base.ToString();
		}
	}
}
