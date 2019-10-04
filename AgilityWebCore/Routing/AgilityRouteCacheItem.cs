using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agility.Web.Routing
{
	[Serializable]
	public class AgilityRouteCacheItem
	{
		public int PageID { get; set; }
		public string ChildDynamicPagePath { get; set; }
	}
}
