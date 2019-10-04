using System.Data;

namespace Agility.Web.Objects
{
	/// <summary>
	/// This object contains the fields for Content that is stored in Agility.  This content can be a single item or a list of items.
	/// </summary>
	public class AgilityContent //: Agility.Web.AgilityContentServer.AgilityContent
	{
		internal int _ID = 0;
		internal string _name = "";
		internal string _languageCode = "";
		internal string _referenceName = "";
		internal bool _isTimedReleaseEnabled = false;
		internal string _defaultSort = "";
		internal DataSet _contentSet = null;


		internal AgilityContent()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="referenceName"></param>
		public AgilityContent(string referenceName)
		{
			AgilityContent tmpContent = Data.GetContentDefinition(referenceName);
			_referenceName = referenceName;
			_contentSet = tmpContent._contentSet;
			_ID = tmpContent.ID;
			_name = tmpContent.Name;
			_languageCode = tmpContent.LanguageCode;
			_isTimedReleaseEnabled = tmpContent.IsTimedReleaseEnabled;
			_defaultSort = tmpContent.DefaultSort;
			
			
		}


		public int ID
		{
			get
			{
				return _ID;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public string Name
		{
			get
			{
				return _name;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public string LanguageCode
		{
			get
			{
				return _languageCode;
			}
			set
			{
				_languageCode = value;
			}
		}


		public string ReferenceName
		{
			get
			{
				return _referenceName;
			}
		}
		public bool IsTimedReleaseEnabled
		{
			get
			{
				return _isTimedReleaseEnabled;
			}
		}
		public string DefaultSort
		{
			get
			{
				return _defaultSort;
			}
		}

		


		/// <summary>
		/// 
		/// </summary>
		public DataTable ContentItems
		{
			get
			{
				if (_contentSet == null) return null;

				if (_contentSet.Tables.Contains("ContentItems"))
				{
					return _contentSet.Tables["ContentItems"];
				}
				
				return null;
			}
		}


		/// <summary>
		/// Gets the DataTable of Attachment MetaData that are linked to this ContentView.
		/// </summary>
		public DataTable AttachmentMetaData
		{
			get
			{

				if (_contentSet == null) return null;

				if (_contentSet.Tables.Contains("Attachments"))
				{
					return _contentSet.Tables["Attachments"];
				}

				return null;
				
			}
		}

		/// <summary>
		/// Gets the DataTable of Tags that are linked to this ContentView.
		/// Columns: ContentID, TagID
		/// </summary>
		public DataTable Tags
		{
			get
			{
				return _contentSet.Tables["Tags"];
			}
		}
	}
}
