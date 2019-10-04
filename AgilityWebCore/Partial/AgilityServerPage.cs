using System;
using System.Collections.Generic;
using System.Text;
using Agility.Web.Configuration;

namespace Agility.Web.AgilityContentServer
{


	partial class AgilityPage
	{

		

		internal string ItemsToPreloadFilePath
		{
			get
			{
				return string.Format("{0}/{1}/Staging/{2}_PreloadItems.bin",
					Current.Settings.ContentCacheFilePath,
					AgilityContext.WebsiteName,
					this.ID);
			}
		}


		 [System.Xml.Serialization.XmlIgnore()]
		internal DynamicPageFormulaItem DynamicPageItem { get; set; }

	}
}
