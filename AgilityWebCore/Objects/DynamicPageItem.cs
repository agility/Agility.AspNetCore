using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Agility.Web.Objects
{
	public class DynamicPageItem
	{

		public DynamicPageItem()
		{

		}

		internal DynamicPageItem(Agility.Web.AgilityContentServer.DynamicPageFormulaItem dpItem)
		{
			PageID = dpItem.PageID;
			ContentID = dpItem.ContentID;
			ContentReferenceName = dpItem.ContentReferenceName;
			LastModifiedDate = dpItem.LastModifiedDate;
			Name = dpItem.Name;
			Title = dpItem.Title;
			MenuText = dpItem.MenuText;
			VisibleOnMenu = dpItem.VisibleOnMenu;
			VisibleOnSitemap = dpItem.VisibleOnSitemap;
		}

		public int PageID { get; set; }
		public int ContentID { get; set; }
		public string ContentReferenceName { get; set; }
		public DateTime LastModifiedDate { get; set; }
		public string Name { get; set; }
		public string Title { get; set; }
		public string MenuText { get; set; }
		public bool VisibleOnMenu { get; set; }
		public bool VisibleOnSitemap { get; set; }

		public T GetContentItem<T>() where T : AgilityContentItem
		{
			var content = BaseCache.GetContent(ContentReferenceName, AgilityContext.LanguageCode, AgilityContext.WebsiteName, false);

			var row = content.GetItemByContentID(ContentID);
			if (row == null) return null;
			
			Type type = typeof(T);
			ConstructorInfo constr = type.GetConstructor(System.Type.EmptyTypes);
			return AgilityContentRepository<T>.ConvertDataRowToObject(constr, row, AgilityContext.LanguageCode, ContentReferenceName);
		}
		

	}
}
