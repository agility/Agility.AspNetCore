using System;
using System.Collections.Generic;
namespace Agility.Web.Objects
{
	public class Media
	{
		internal AgilityContentServer.AgilityAssetMedia _media;

		public Media() { }
		internal Media(AgilityContentServer.AgilityAssetMedia media)
		{
			_media = media;
		}

		public int ID
		{
			get
			{
				return _media.ID;
			}
		}

		public string URL { get { return _media.URL; } }
		public DateTime ModifiedOn { get { return _media.ModifiedOn; } }
		public long Size { get { return _media.Size; } }
		public int SortOrder { get { return _media.SortOrder; } }

		private Dictionary<string, Thumbnail> _thumbnails = null;
		public Dictionary<string, Thumbnail> Thumbnails
		{
			get
			{
				if (_thumbnails == null)
				{
					_thumbnails = new Dictionary<string, Thumbnail>(StringComparer.OrdinalIgnoreCase);
					
					if (_media.Thumbnails != null)
					{
						foreach (var t in _media.Thumbnails)
						{						
							_thumbnails[t.Name] = new Thumbnail()
							{
								URL = t.URL,
								Name = t.Name,
								Height = t.Height,
								Width = t.Width
							};
						}						
					}
				}

				return _thumbnails;	
			}
		}

		private Dictionary<string, string> _metaData = null;
		public Dictionary<string, string> MetaData
		{
			get
			{
				if (_metaData == null) 
				{
					
					_metaData = new Dictionary<string, string>();

					if (_media.MetaData != null)
					{
						foreach (var m in _media.MetaData) 
						{
							_metaData[m.Key] = m.Value;
						}
					}						
				}

				return _metaData;				
			}
		}

		public string Title
		{
			get
			{
				string s = null;
				MetaData.TryGetValue("Title", out s);
				return s;
			}
		}


		public string Description
		{
			get
			{
				string s = null;
				MetaData.TryGetValue("Description", out s);
				return s;
			}
		}

		public string LinkURL
		{
			get
			{
				string s = null;
				MetaData.TryGetValue("LinkUrl", out s);
				return s;
			}
		}

	}
}
