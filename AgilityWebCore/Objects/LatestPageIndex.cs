using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Agility.Web.Objects
{
	
	[Serializable]	
	public class LatestPageIndex
	{
		public LatestPageIndex()
		{
			Index = new Dictionary<int, LatestPageIndexItem>();
		}

		public int MaxVersionID;

		/// <summary>
		/// Dictionary with PageID as key, MaxVersionID of that page as the value.
		/// </summary>
		public Dictionary<int, LatestPageIndexItem> Index = null;
	}

	
	[Serializable]	
	public class LatestPageIndexItem
	{
		public int MaxVersionID;
		public int PageID;

		[OptionalField]
		public bool Downloaded = false;
	}


	

}
