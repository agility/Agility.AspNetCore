using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Agility.Web.AgilityContentServer
{
	public partial class AgilityContent
	{
		public DataRow GetItemByContentID(int contentID)
		{
			if (this.DataSet == null || !this.DataSet.Tables.Contains("ContentItems")) return null;

			
			DataTable t = this.DataSet.Tables["ContentItems"];

			DataRow[] rows = null;
			//TODO: figure out the content id index
			//if (t != null && t.ContentIDIndex != null)
			//{
			//	rows = t.ContentIDIndex.Find(contentID);
			//}
			//else
			//{
				//fallback to the Select method...
				rows = t.Select(string.Format("ContentID = {0}", contentID));
			//}

			if (rows.Length > 0)
			{
				return rows[0];
			}
			return null;
		}

		public DataRow[] GetItemsByContentIDs(IEnumerable<object> contentIDs)
		{
			DataTable t = this.DataSet.Tables["ContentItems"];

			DataRow[] rows = null;

			//TODO: figure out the content id index
			//if (t != null && t.ContentIDIndex != null)
			//{
			//	rows = t.ContentIDIndex.FindAll(contentIDs);
			//}
			//else
			//{
				//fallback to the Select method...
				string[] strIDs = (from c in contentIDs
								  select string.Format("{0}", c)).ToArray();

				rows = t.Select(string.Format("ContentID in ({0})", string.Join(",", strIDs)));
			//}

			
			return rows;
		}



	}
}
