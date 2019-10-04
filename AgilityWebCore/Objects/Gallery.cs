using System;
using System.Collections.Generic;

namespace Agility.Web.Objects
{
	public class Gallery
	{
		internal AgilityContentServer.AgilityAssetMediaGroup _mediaGroup;
		
		internal Gallery(AgilityContentServer.AgilityAssetMediaGroup mediaGroup)
		{
			_mediaGroup = mediaGroup;
		}
		
		public int ID
		{
			get { return _mediaGroup.ID; }
		}

		public string Name
		{
			get { return _mediaGroup.Name; }
		}

		public string Description
		{
			get { return _mediaGroup.Description; }
		}

		public DateTime ModifiedOn
		{
			get { return _mediaGroup.ModifiedOn; }
		}

		public IEnumerable<Media> Media
		{
			get
			{
				for (int i = 0; i < _mediaGroup.Media.Length; i++ )
				{
					yield return new Media(_mediaGroup.Media[i]);
				}
			}
		}
		

	}
}
