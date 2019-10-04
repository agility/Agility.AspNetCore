using System.Collections.Generic;


namespace Agility.Web.Objects
{
	/// <summary>
	/// This object contains the details for attachments that are associated to content items.  You can get these details from a Content object.
	/// </summary>
	public class Attachment // : AgilityItem
	{

		//non bindable fields (ony used for saving attachments)
		public string GUID;
		public string ManagerID;
		
		public byte[] Blob;

		//bindable fields with Property accessors (used for saving a accessing attachments)
		private string _FileName;
		private string _Label;
		private string _Target;
		private string _URL;
	    private int _FileSize;

		public int ItemOrder { get; set; }

		public string FileName
		{
			get { return _FileName; }
			set { _FileName = value; }
		}

		public string Label
		{
			get {
				if (string.IsNullOrEmpty(_Label))
				{
					return string.Empty;
				}
				return _Label;
			}
			set { _Label = value; }
		}

		public string Target
		{
			get { return _Target; }
			set { _Target = value; }
		}
		
		public string URL
		{
			get { return _URL; }
			set { _URL = value; }
		}

	    public int FileSize
	    {
            get { return _FileSize; }
            set { _FileSize = value; }
	    }

		public int Height { get; set; }

		public int Width { get; set; }

		private Dictionary<string, Thumbnail> _thumbnails = null;

		public Dictionary<string, Thumbnail> Thumbnails
		{
			get
			{
				if (_thumbnails == null)
				{
					_thumbnails = new Dictionary<string, Thumbnail>();
				}
				return _thumbnails;
			}
		}

		public string ThumbnailUrlOrDefault(string thumbnailName)
		{
			Thumbnail t = null;
			if (Thumbnails.TryGetValue(thumbnailName, out t))
			{
				return t.URL;
			}
			else
			{
				return URL;
			}
		}

	}
}
