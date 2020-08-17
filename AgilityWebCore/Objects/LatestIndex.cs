using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Agility.Web.Objects
{
	[Serializable]
	public class LatestIndex
	{
		public LatestIndex()
		{
			Index = new Dictionary<int, LatestIndexItem>();
		}

		public DateTime MaxModDate;

		/// <summary>
		/// ReferenceName as the key, MaxVersionID as the value.
		/// </summary>
		public Dictionary<int, LatestIndexItem> Index = null;
	}

	[Serializable]
	public class LatestIndexItem
	{
		public int ID;

		public string ModifiedOnStr;

		[System.Runtime.Serialization.IgnoreDataMember]
		public DateTime ModifiedOn
		{
			get
			{
				DateTime dt;
				if (DateTime.TryParse(ModifiedOnStr, out dt))
				{
					return dt;
				}
				return DateTime.MinValue;
			}
		}

		[OptionalField]
		public bool Downloaded = false;
	}
}
