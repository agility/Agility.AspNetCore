using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Agility.Web.AgilityContentServer
{
	[Serializable]
	public class DynamicPageFormulaItemIndex : Dictionary<string, DynamicPageFormulaItem>
	{

		public DynamicPageFormulaItemIndex()
			: base()
		{
		}

		public DynamicPageFormulaItemIndex(int count)
			: base(count)
		{

		}

        //Added to solve deserialization error
        public DynamicPageFormulaItemIndex(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

		public string DynamicPageContentViewSort { get; set; }
		public string DynamicPageContentViewFilter { get; set; }
		public string DynamicPageMenuText { get; set; }
		public string DynamicPageTitle { get; set; }
		public string DynamicPageName { get; set; }

		public int PageVersionID { get; set; }
	}
}
