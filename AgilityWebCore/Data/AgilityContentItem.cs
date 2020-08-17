using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Microsoft.AspNetCore.Html;

namespace Agility.Web
{
	public partial class AgilityContentItem
	{
		
		private DataRow _dataRow;

		internal DataRow DataRow
		{
			get { return _dataRow; }
			set { _dataRow = value; }
		}


		

		[System.Runtime.Serialization.IgnoreDataMember]
		[System.Xml.Serialization.XmlIgnore]
        public DataRow Row
		{
			get
			{
				return _dataRow;
			}
		}

		private string _referenceName;

		public virtual string ReferenceName
		{
			get { return _referenceName; }
			set { _referenceName = value; }
		}

		public virtual bool EnablePersonalization { get; set; }

		protected string ContentReferenceName
		{
			get { return _referenceName; }			
		}
		
		public int ContentID { get; set; }
		public int VersionID { get; set; }
		string _languageCode;

		public string LanguageCode
		{
			get { return _languageCode; }
			set { _languageCode = value; }
		}

		public string State { get; set; }
		public DateTime? PullDate { get; set; }
		public DateTime? ReleaseDate { get; set; }		

		public DateTime ModifiedDate { get; set; }
		public DateTime CreatedDate { get; set; }

		private Dictionary<string, List<Agility.Web.Objects.Attachment>> _cachedAttachmentLists = new Dictionary<string, List<Agility.Web.Objects.Attachment>>();
		private Dictionary<string, object> _cachedLinkedContents = new Dictionary<string, object>();



		public List<Agility.Web.Objects.Attachment> GetAttachments(string fieldName)
		{
			//track this content id as being loaded in this request...
			AgilityContext.LoadedContentItemIDs.Add(ContentID);

			List<Agility.Web.Objects.Attachment> lst = null;
			if (! _cachedAttachmentLists.TryGetValue(fieldName, out lst))
			{

				lst = Data.GetAttachmentsFromVersionID(VersionID, fieldName, _referenceName, LanguageCode);
				if (lst == null) lst = new List<Agility.Web.Objects.Attachment>();
				_cachedAttachmentLists[fieldName] = lst;
			}
			return lst;
		}

		public Agility.Web.Objects.Attachment GetAttachment(string fieldName)
		{
			List<Agility.Web.Objects.Attachment> lst = GetAttachments(fieldName);
			
			if (lst != null && lst.Count > 0) return lst[0];
			return null;
		}

		public Agility.Web.Objects.Gallery GetGallery(string fieldName)
		{
			//track this content id as being loaded in this request...
			AgilityContext.LoadedContentItemIDs.Add(ContentID);

			int galleryID = GetFieldValue<int>(fieldName);
			if (galleryID < 1) return null;
			return Data.GetGallery(galleryID);
		}

		public IAgilityContentRepository<T> GetLinkedContent<T>(string fieldName) where T : AgilityContentItem
		{
			return GetLinkedContent<T>(fieldName, string.Empty, string.Empty, string.Empty);
		}

		public IAgilityContentRepository<T> GetLinkedContent<T>(string fieldName, string languageCode, string sort, string filter) where T : AgilityContentItem 
		{
			if (Row == null) return null;

			//track this content id as being loaded in this request...
			AgilityContext.LoadedContentItemIDs.Add(ContentID);

			string fieldValue = GetFieldValue<string>(fieldName);
			if (string.IsNullOrEmpty(fieldValue)) fieldValue = string.Empty;
			if (string.IsNullOrEmpty(languageCode)) languageCode = LanguageCode;

			object obj = null;
			if (_cachedLinkedContents.TryGetValue(fieldName, out obj))
			{
				IAgilityContentRepository<T> obj2 = obj as IAgilityContentRepository<T>;
				if (obj2 != null)
				{
					return obj2;
				}
			}

			IAgilityContentRepository<T> repo = new AgilityContentRepository<T>(fieldValue, languageCode, sort, filter);
			_cachedLinkedContents[fieldName] = repo;
			return repo;
		}

		public IAgilityContentRepository<AgilityContentItem> GetContent(string fieldName)
		{
			IAgilityContentRepository<AgilityContentItem> x = GetLinkedContent<AgilityContentItem>(fieldName);
			return x;
		}

		public IAgilityContentRepository<AgilityContentItem> GetContent(string fieldName, string languageCode, string sort, string filter)
		{
			return GetLinkedContent<AgilityContentItem>(fieldName, languageCode, sort, filter);
		}

		public object this[string fieldName]
		{
			get
			{
				//track this content id as being loaded in this request...
				AgilityContext.LoadedContentItemIDs.Add(ContentID);

				if (! Row.Table.Columns.Contains(fieldName))
				{
					return string.Empty;
				}
				return Row[fieldName];
			}
		}

		public HtmlString Raw(string fieldName)
		{
			return Raw(fieldName, "{0}");
		}

		public HtmlString Raw(string fieldName, string format)
		{
			object value = null;
			if (Row.Table.Columns.Contains(fieldName))
			{
				value = Row[fieldName];
			}

			string html = string.Format(format, value);
			html = Util.Url.ResolveTildaUrlsInHtml(html);

			//track this content id as being loaded in this request...
			AgilityContext.LoadedContentItemIDs.Add(ContentID);

			return new HtmlString(html);
		}

		public T GetFieldValue<T>(string fieldName) 
		{
			//track this content id as being loaded in this request...
			AgilityContext.LoadedContentItemIDs.Add(ContentID);

			Type type = typeof(T);
			if (Row == null || !Row.Table.Columns.Contains(fieldName)) return default(T);
			object o = Row[fieldName];
			
			//handle null
			if (o == DBNull.Value) return default(T);

			//handle non-string types where the types don't have to be converted
			if (type == o.GetType() && type != typeof(string)) return (T)o;

			string stringValue = string.Format("{0}", Row[fieldName]);

			if (type == typeof(string))
			{
				stringValue = Util.Url.ResolveTildaUrlsInHtml(stringValue);
				return (T)((object)stringValue);
			} 
			else if (type == typeof(int))
			{
				int intValue;
				if (int.TryParse(stringValue, out intValue)) return (T)((object)intValue);
			}
			else if (type == typeof(decimal))
			{
				decimal decValue;
				if (decimal.TryParse(stringValue, out decValue)) return (T)((object)decValue);
			}
			else if (type == typeof(double))
			{
				double dblValue;
				if (double.TryParse(stringValue, out dblValue)) return (T)((object)dblValue);
			}
			else if (type == typeof(DateTime))
			{
				DateTime dtValue;
				if (DateTime.TryParse(stringValue, out dtValue)) return (T)((object)dtValue);
			}
			else if (type == typeof(bool))
			{
				bool boolValue;
				if (bool.TryParse(stringValue, out boolValue)) return (T)((object)boolValue);
			}
			else if (type == typeof(Agility.Web.Objects.Gallery))
			{
				//get the int value of the field, and call "get gallery"
				int intValue;
				if (int.TryParse(stringValue, out intValue))
				{
					Agility.Web.Objects.Gallery g = Data.GetGallery(intValue);
					return (T)((object)g);
				}

			}
			
			return default(T);
		}

		List<AgilityContentTag> _tags = null;

		public List<AgilityContentTag> Tags
		{
			get {
				if (_tags == null)
				{
					//TODO: implement tags...
					//_tags = new List<AgilityContentTag>();
					//AgilityContentServer.AgilityContent content = BaseCache.GetContent(ReferenceName, LanguageCode, AgilityContext.WebsiteName);


					//if (content != null && content.DataSet != null && content.DataSet.Tables.Contains("Tags"))
					//{
					//	DataTable tags = content.DataSet.Tables["Tags"];
					//	if (tags != null)
					//	{
					//		string filter = string.Format("ContentID = {0}", ContentID);

					//		AgilityContentServer.AgilityTagList tagList = BaseCache.GetTagList(LanguageCode, AgilityContext.WebsiteName);
					//		if (tagList != null && tagList.DSTags != null && tagList.DSTags.Tags != null)
					//		{

					//			DataRow[] rows = tags.Select(filter);

					//			foreach (DataRow row in rows)
					//			{
					//				string tagFilter = string.Format("TagID = {0}", row.TagID);
					//				DataRow[] tagRows = tagList.DSTags.Tables["Tags"].Select(tagFilter);
					//				if (tagRows.Length > 0)
					//				{
					//					_tags.Add(new AgilityContentTag()
					//					{
					//						Tag = tagRows[0]["Tag"] as string,
					//						TagID = (int) row["TagID"]
					//					});

					//				}

					//			}
					//		}
					//	}
					//}
				}
				return _tags; 
			}
			
		}

		
	}
}
