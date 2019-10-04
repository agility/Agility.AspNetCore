using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Agility.Web.Objects
{
	[Serializable]
	public class LatestContentItemIndex
	{
		public LatestContentItemIndex()
		{
			Index = new Dictionary<string, LatestContentIndexItem>();
		}

		public int MaxVersionID;

		/// <summary>
		/// ReferenceName as the key, MaxVersionID as the value.
		/// </summary>
		public Dictionary<string, LatestContentIndexItem> Index = null;
	}

	[Serializable]
	public class LatestContentIndexItem
	{
		public string ReferenceName;
		public int MaxVersionID;

		[OptionalField]
		public bool Downloaded = false;
	}
}
