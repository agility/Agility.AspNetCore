using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Agility.Web.Configuration;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using System.Xml;
using System.Data;
using Agility.Web.Objects;
using Agility.Web.Enum;
using Agility.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Memory;

namespace Agility.Web
{
	/// <summary>
	/// Main Data access for selecting and inserting Agility objects.
	/// </summary>
	public class Data
	{

		/// <summary>
		/// Gets a Digital Channel from its reference name.
		/// </summary>
		/// <param name="channelReferenceName"></param>
		public static DigitalChannel GetDigitalChannel(string channelReferenceName) 
		{
			var channel = BaseCache.GetDigitalChannels(AgilityContext.WebsiteName).Channels.FirstOrDefault(c => string.Equals(c.ReferenceName, channelReferenceName));
			return new DigitalChannel(channel);			
		}

		public static List<DigitalChannel> GetDigitalChannels()
		{
			var channels = BaseCache.GetDigitalChannels(AgilityContext.WebsiteName).Channels;

			return channels.Select(c => new DigitalChannel(c)).ToList();


		}

		/// <summary>
		/// Gets the dynamic page item values for a given dynamic page path and content row.  This row MUST be part of a dynamic content list.
		/// </summary>
		/// <param name="dynamicPagePath">The sitemap map to the dynamic page as seen in the Agility sitemap tree.</param>
		/// <param name="contentReferenceName"></param>
		/// <param name="contentRow"></param>
		/// <returns></returns>
		public static DynamicPageItem GetDynamicPageItem(string dynamicPagePath, string contentReferenceName, DataRow contentRow)
		{
			return GetDynamicPageItem(dynamicPagePath, contentReferenceName, contentRow, null);
		}

		/// <summary>
		/// Gets the dynamic page item values for a given dynamic page path and content row.  This row MUST be part of a dynamic content list.
		/// </summary>
		/// <param name="contentReferenceName"></param>
		/// <param name="contentRow"></param>
		/// <param name="dynamicPagePath">The sitemap map to the dynamic page as seen in the Agility sitemap tree.</param>
		/// <returns></returns>
		public static DynamicPageItem GetDynamicPageItem(string dynamicPagePath, string contentReferenceName, DataRow contentRow, string languageCode)
		{

			if (string.IsNullOrEmpty(dynamicPagePath)) throw new ArgumentException("You must provide a dynamic page path.", "dynamicPagePath");
			if (string.IsNullOrEmpty(contentReferenceName)) throw new ArgumentException("You must provide a content reference name.", "contentReferenceName");
			if (contentRow == null) throw new ArgumentException("You must provide a content row.", "contentRow");
			if (string.IsNullOrEmpty(languageCode)) languageCode = AgilityContext.LanguageCode;

			dynamicPagePath = dynamicPagePath.ToLowerInvariant();
			int indexOfASPX = dynamicPagePath.IndexOf(".aspx", StringComparison.CurrentCultureIgnoreCase);
			string extension = Path.GetExtension(dynamicPagePath);

			if (indexOfASPX != -1)
			{
				//pull off the .aspx from the path...
				dynamicPagePath = dynamicPagePath.Substring(0, indexOfASPX);
			}

			if (dynamicPagePath.StartsWith("~/")) dynamicPagePath = dynamicPagePath.Substring(1);
			if (!dynamicPagePath.StartsWith("/")) dynamicPagePath = string.Format("/{0}", dynamicPagePath);
			if (dynamicPagePath.EndsWith("/")) dynamicPagePath = dynamicPagePath.TrimEnd('/');

			
			Dictionary<string, Agility.Web.Routing.AgilityRouteCacheItem> routes = Agility.Web.Routing.AgilityRouteTable.GetRouteTable(languageCode, AgilityContext.CurrentChannel.ID);

			Agility.Web.Routing.AgilityRouteCacheItem routeItem = null;
			if (!routes.TryGetValue(dynamicPagePath, out routeItem)) return null;

			AgilityContentServer.AgilityPage page = BaseCache.GetPageFromID(routeItem.PageID, languageCode, AgilityContext.WebsiteName, null);
			
			Agility.Web.AgilityContentServer.DynamicPageFormulaItem dpItem = new AgilityContentServer.DynamicPageFormulaItem(page, contentReferenceName, languageCode, contentRow);
			return new DynamicPageItem(dpItem);
			

		}

		/// <summary>
		/// Get the cache key that a given content view is stored with in the default language.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <returns></returns>
		public static string GetContentCacheResetKey(string referenceName) 
		{
			return GetContentCacheResetKey(referenceName, AgilityContext.LanguageCode);	
		}

		/// <summary>
		/// Get the cache key that a given content view is stored with in a given language.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static string GetContentCacheResetKey(string referenceName, string languageCode)
		{
			Agility.Web.AgilityContentServer.AgilityItemKey itemKey = new Agility.Web.AgilityContentServer.AgilityItemKey();
			itemKey.Key = referenceName;
			itemKey.LanguageCode = languageCode;
			itemKey.ItemType = typeof(Agility.Web.AgilityContentServer.AgilityContent).Name;

			return BaseCache.GetCacheKey(itemKey);

		}

		#region "Selection Methods"



		

		/// <summary>
		/// Returns the DataView for an AgilityContent object in the current language.
		/// </summary>
		/// <param name="contentReferenceName"></param>
		/// <returns></returns>
		public static DataView GetContentView(string contentReferenceName)
		{
			return GetContentView(contentReferenceName, AgilityContext.LanguageCode);
		}


		/// <summary>
		/// Returns a copy of the DataView for an AgilityContent object in the current language.
		/// </summary>
		/// <param name="contentReferenceName"></param>
		/// <returns></returns>
		public static DataView GetContentViewCopy(string contentReferenceName)
		{
			DataView dv = GetContentView(contentReferenceName);
			if (dv != null)
			{
				DataTable dtCopy = dv.Table.Copy();
				return new DataView(dtCopy, dv.RowFilter, dv.Sort, DataViewRowState.CurrentRows);
			}
			return null;
		}
		

		/// <summary>
		/// Returns the DataView for an AgilityContent object in the specified language.
		/// </summary>
		/// <param name="contentReferenceName"></param>
		/// <returns></returns>
		public static DataView GetContentView(string contentReferenceName, string languageCode)
		{
			AgilityContent content = Agility.Web.Data.GetContent(contentReferenceName, languageCode);
			if (content == null) return null;
			if (content.ContentItems != null)
			{
				content.ContentItems.ExtendedProperties["referenceName"] = contentReferenceName;

				DataView dv = new DataView(content.ContentItems);
				
				try
				{
					//setting this sort may error if the columns used in DefaultSort have been deleted or renamed for whatever reason.
					dv.Sort = content.DefaultSort;
					content.ContentItems.ExtendedProperties["defaultSort"] = content.DefaultSort;
				}
				catch { }

				return dv;
			}
			return null;
		}

		/// <summary>
		/// This returns the AgilityContent object based on the specified referenceName and the current language code.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <returns></returns>
		static public AgilityContent GetContent(string referenceName)
		{
			return Agility.Web.Data.GetContent(referenceName, AgilityContext.LanguageCode);
		}

		/// <summary>
		/// This returns the AgilityContent object based on the specified referenceName and language code.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		static public AgilityContent GetContent(string referenceName, string languageCode)
		{



			string websiteName = AgilityContext.WebsiteName;

			
			Agility.Web.AgilityContentServer.AgilityContent serverContent = null;
			try
			{
				serverContent = BaseCache.GetContent(referenceName, languageCode, websiteName);

			}
			catch (Exception ex)
			{
				if (! Current.Settings.DevelopmentMode)
				{
					Agility.Web.Tracing.WebTrace.WriteWarningLine("Error occurred accessing content: " + referenceName + " in language: " + languageCode + "\r\n" + ex.ToString());
				}
				else
				{
					throw new ApplicationException(string.Format("Error occurred accessing content: {0} in language: {1}", referenceName, languageCode), ex);
				}
			}
			
			//Call CreateAPIContent to create the API content object from the server content object.
			if (serverContent != null)
			{
				return CreateAPIContentObject(serverContent);
			}
			else
			{
				return null;
			}
		
		}

		/// <summary>
		/// This returns the Content shell from Agility based on the referenceName and specified Language Code.
		/// </summary>
		/// <param name="referenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		static public AgilityContent GetContentDefinition(string referenceName)
		{
		    return GetContentDefinition(referenceName, AgilityContext.LanguageCode);
		}

        public static AgilityContent GetContentDefinition(string referenceName, string languageCode)
        {
            string websiteName = AgilityContext.WebsiteName;

            Agility.Web.AgilityContentServer.AgilityContent serverContent = BaseCache.GetContentDefinition(referenceName, languageCode, websiteName);

            //Call CreateAPIContent to create the API content object from the server content object.
            AgilityContent content = CreateAPIContentObject(serverContent);

            return content;
        }


		/// <summary>
		/// Gets the AgilityPage object that corresponds to the given url.  The URL should be application based (eg: ~/Path/PageName.aspx).  If the page does not exist in this language, the page from the default language may be returned unless the redirect404ToDefaultLanguage option has been set to false in the web.config/agility.web settings.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		static public Objects.AgilityPage GetPage(string url)
		{			
			string languageCode = AgilityContext.LanguageCode;
			
			return GetPage(url, languageCode);
		}


		static public Objects.AgilityPage GetPage(int id, string languageCode)
		{

			if (string.IsNullOrEmpty(languageCode)) languageCode = AgilityContext.LanguageCode;
			string websiteName = AgilityContext.WebsiteName;

			Agility.Web.AgilityContentServer.AgilityPage serverPage = BaseCache.GetPageFromID(id, languageCode, websiteName, "");

			return CreateAPIPageObject(serverPage);
		}

		/// <summary>
		/// Gets the AgilityPage object that corresponds to the given url with the specified language code.  If the page does not exist in this language, the page from the default language may be returned unless the redirect404ToDefaultLanguage option has been set to false in the web.config/agility.web settings.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		static public Objects.AgilityPage GetPage(string url, string languageCode)
		{
			
			if (string.IsNullOrEmpty(languageCode)) languageCode = AgilityContext.LanguageCode;
			string websiteName = AgilityContext.WebsiteName;

			Agility.Web.AgilityContentServer.AgilityPage serverPage = BaseCache.GetPage(url, languageCode, websiteName);

			return CreateAPIPageObject(serverPage);
		}


		/// <summary>
		/// Gets the AgilitySiteMapNode from the AgilitySiteMapProvider from the given page id.
		/// </summary>
		/// <remarks>
		/// This method may only be used if the AgilitySiteMapProvider is the default sitemap provider for the website.
		/// </remarks>
		/// <param name="pageID"></param>
		/// <returns></returns>
		static public AgilitySiteMapNode GetSiteMapNode(int pageID) 
		{
			//TODO: build the sitemap provider
			return null;
			//Providers.AgilitySiteMapProvider p = SiteMap.Provider as Providers.AgilitySiteMapProvider;
			//if (p == null)
			//{
			//	//check that they are using the AgilitySiteMapProvider
			//	throw new Exceptions.AgilityException("The GetSiteMapNode method map only be used if the AgilitySiteMapProvider is the default sitemap provider for the website.");
			//}

			//return p.FindSiteMapNodeFromKey(pageID.ToString()) as AgilitySiteMapNode;


		}


		/// <summary>
		/// Get a gallery object based on it's object.
		/// </summary>
		/// <param name="galleryID"></param>
		/// <returns></returns>
		static public Gallery GetGallery(int galleryID)
		{

			AgilityContentServer.AgilityAssetMediaGroup tmpGrp = BaseCache.GetMediaGroup(galleryID, AgilityContext.WebsiteName);
			if (tmpGrp == null) return null;

			return new Gallery(tmpGrp);

		}

		/// <summary>
		/// Gets the Config object for this website based on the website domain name in the Agility Content Manager.
		/// </summary>
		/// <returns></returns>
		static public Config GetConfig()
		{
			//OLD-TODO: Add in validation and handlers
			
			string websiteName = AgilityContext.WebsiteName;

			AgilityContentServer.AgilityDomainConfiguration domainConfiguration = BaseCache.GetDomainConfiguration(websiteName);
			return CreateAPIConfigObject(domainConfiguration);
		}


		/// <summary>
		/// Gets the Sitemap object for the given language code.
		/// </summary>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		static public Sitemap GetSitemap(string languageCode)
		{
			string websiteName = AgilityContext.WebsiteName;

			//OLD-TODO: Add in validation and handlers

			AgilityContentServer.AgilitySitemap serverSitemap = BaseCache.GetSitemap(languageCode, websiteName);
			return CreateAPISitemapObject(serverSitemap);
		}

		/// <summary>
		/// Gets the TagList for a given language code.
		/// </summary>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		static public TagList GetTagList(string languageCode)
		{
			AgilityContentServer.AgilityTagList serverTagList = BaseCache.GetTagList(languageCode, AgilityContext.WebsiteName);


			return CreateAPITagListObject(serverTagList);
		}

		

		#endregion
		
		

		/// <summary>
		/// Gets the URL, Target, Label or FileName of the Attachment for a ContentItem based on the field name and the provided DataRowView that represents that Content Item from an AgilityContent object.
		/// </summary>
		/// <param name="fieldName"></param>
		/// <param name="propertyName"></param>
		/// <param name="drv"></param>
		/// <returns></returns>
		public static string AttachmentEval(string fieldName, string propertyName, DataRowView drv)
		{
			//object contentItemObj = DataBinder.Eval(drv, "ContentID");
			//if (!(contentItemObj is int)) return null;


			int versionID = -1;
			if (!int.TryParse($"{drv["VersionID"]}", out versionID)) return null;

			string referenceName = drv.Row.Table.ExtendedProperties["referenceName"] as string;

			List<Attachment> attachments = Agility.Web.Data.GetAttachmentsFromVersionID(versionID, fieldName, referenceName);
						
			if (attachments == null || attachments.Count == 0 || attachments[0] == null) return null;

			if (propertyName == null) propertyName = "url";
			switch (propertyName.ToLowerInvariant())
			{
				case "target":
					return attachments[0].Target;
				case "filename":
					return attachments[0].FileName;
				case "label":
					return attachments[0].Label;
                case "filesize":
			        return attachments[0].FileSize.ToString();
				default:
					return attachments[0].URL;
			}
		}

        public static string UrlEval(string fieldName, string attributeToReturn, DataRowView drv)
        {
            return UrlEval(string.Format("{0}", drv[fieldName]), attributeToReturn);

        }


        /// <summary>
        /// Returns the URL of an image.
        /// </summary>
        /// <param name="attachment">The image to be returned.</param>
        /// <returns>the image url, if the image is not null and URL is not empty; "#", otherwise.</returns>
        public static string ImageSource(Attachment attachment)
        {
            var imgSrc = "#";

            if (attachment != null)
            {
                imgSrc = attachment.URL ?? imgSrc;
            }

            return imgSrc;
        }

        /// <summary>
        /// Evaluates a url and returns the value of the specified attribute. Attribte must be one of "url", "title", or "target"
        /// </summary>
        /// <param name="FullUrlValue">Url to evaluate</param>
        /// <param name="attributeToReturn">Value of the url attribute</param>
        /// <returns></returns>
        public static string UrlEval(string fullUrlValue, string attributeToReturn)
        {
            //URL Format: <a href="{URL}" target="{Target}">{Title}</a>

            string urlValue = string.Format("{0}", fullUrlValue);
            if (string.IsNullOrEmpty(urlValue))
            {
                return null;
            }

            string result = string.Empty;
            attributeToReturn = attributeToReturn.ToLowerInvariant();
            if (attributeToReturn == "url")
            {
                urlValue = urlValue.Substring(urlValue.IndexOf("href=\"") + 6);
                result = urlValue.Substring(0, urlValue.IndexOf("\""));
            }
            else if (attributeToReturn == "title")
            {
                urlValue = urlValue.Substring(urlValue.IndexOf(">") + 1);
                result = urlValue.Substring(0, urlValue.IndexOf("</a>"));
            }
            else if (attributeToReturn == "target")
            {
                urlValue = urlValue.Substring(urlValue.IndexOf("target=\"") + 8);
                result = urlValue.Substring(0, urlValue.IndexOf("\""));
				if (string.IsNullOrEmpty(result)) result = "_self";
            }
            else
            {
                throw new ArgumentException(string.Format("Property name '{0}' is invalid. Valid property names: Url, Title and Target", attributeToReturn));
            }

            return result;

        }


		/// <summary>
		/// Gets a List of of Attachments objects based on the given Content Name, Field Name and Content ID.
		/// </summary>
		/// <param name="contentID"></param>
		/// <param name="fieldName"></param>
		/// <param name="contentReferenceName"></param>
		/// <returns></returns>
		public static List<Attachment> GetAttachments(int contentID, string fieldName, string contentReferenceName)
		{
			return GetAttachments(contentID, fieldName, contentReferenceName, AgilityContext.LanguageCode);
		}


		/// <summary>
		/// Gets a List of of Attachments objects based on the given Content Name, Field Name and Content ID amd LanguageCode
		/// </summary>
		/// <param name="contentID"></param>
		/// <param name="fieldName"></param>
		/// <param name="contentReferenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static List<Attachment> GetAttachments(int contentID, string fieldName, string contentReferenceName, string languageCode)
		{

			Agility.Web.AgilityContentServer.AgilityContent serverContent = BaseCache.GetContent(contentReferenceName, languageCode, AgilityContext.WebsiteName);

			if (serverContent == null
				|| serverContent.DataSet == null
				|| serverContent.DataSet.Tables["ContentItems"] == null) return null;


			object versionIDObj = serverContent.DataSet.Tables["ContentItems"].Compute("Max(versionID)", string.Format("contentID={0}", contentID));

			int versionID = -1;

			if (!int.TryParse(string.Format("{0}", versionIDObj), out versionID))
			{ 
				DataRow[] rows = serverContent.DataSet.Tables["ContentItems"].Select(string.Format("contentID={0}", contentID), "versionID desc", DataViewRowState.CurrentRows);
				if (rows.Length > 0)
				{
					versionIDObj = rows[0]["versionID"];
					int.TryParse(string.Format("{0}", versionIDObj), out versionID);
				}
			}

			if (versionID < 1)
			{
				return null;
			}
			

			

			return GetAttachmentsFromVersionID(versionID, fieldName, contentReferenceName);

			/*

			DataRow[] rows = serverContent.DataSet.Attachments.Select(string.Format("versionID={0} AND managerID='{1}'", versionIDObj, fieldName), "itemOrder");
			List<Attachment> atts = new List<Attachment>(rows.Length);

			foreach (AgilityContentServer.ContentDataSet.AttachmentsRow row in rows) 
			{
				Attachment att = new Attachment();
				StringBuilder sb = new StringBuilder();

				if (row.Filename.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
				{
					//if the attachment has an absolute path, pass it through
					sb.Append(row.Filename);

				}
				else
				{
					 
					//calculate the path relative to the current application if neccessary
					if (HttpContext.Current != null && HttpContext.Current.Request != null)
					{
						sb.Append(HttpContext.Current.Request.ApplicationPath);
						if (!HttpContext.Current.Request.ApplicationPath.EndsWith("/"))
						{
							sb.Append("/");
						}
					}

					if (row.Filename.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
					{
						sb = new StringBuilder(row.Filename);						
					}
					else if (row.Filename.StartsWith("/"))
					{
						sb.Append("ecms.ashx").Append(row.Filename);
					}
					else
					{
						sb.Append("ecms.ashx/").Append(row.GUID).Append("/");
						sb.Append(HttpUtility.UrlPathEncode(contentReferenceName.Replace("/", string.Empty).Replace("\\", string.Empty))).Append("/");
						sb.Append(Agility.Web.Util.Url.RemoveSpecialCharacters(row.Filename));
					}
				}


				att.URL = sb.ToString();
				att.Label = row.Label;
				att.Target = row.Target;
				att.FileName = row.Filename;
				att.FileSize = row.FileSize;
				atts.Add(att);

				//get the thumbnails for this attachment (if any)
				if (serverContent.DataSet.AttachmentThumbnails != null && serverContent.DataSet.AttachmentThumbnails.Rows.Count > 0)
				{
					DataRow[] thmRows = serverContent.DataSet.AttachmentThumbnails.Select(string.Format("versionID={0} AND managerID='{1}'", versionIDObj, fieldName));
					foreach (AgilityContentServer.ContentDataSet.AttachmentThumbnailsRow thmRow in thmRows)
					{
						Thumbnail thm = new Thumbnail()
						{
							Name = thmRow.ThumbnailName,
							URL = thmRow.URL,
							Height = thmRow.Height,
							Width = thmRow.Width
						};

						att.Thumbnails[thm.Name] = thm;
					}
				}
			}

			return atts;
			*/
		}

		/// <summary>
		/// Gets a List of of Attachments objects based on the given Content Name, Field Name and Version ID. 
		/// This is similar to GetAttachments, but is faster and preferred if the VersionID field of the content item is readily available.
		/// </summary>
		/// <param name="versionID"></param>
		/// <param name="fieldName"></param>
		/// <param name="contentReferenceName"></param>		
		/// <returns></returns>
		public static List<Attachment> GetAttachmentsFromVersionID(int versionID, string fieldName, string contentReferenceName)
		{
			return GetAttachmentsFromVersionID(versionID, fieldName, contentReferenceName, AgilityContext.LanguageCode);
		}

		/// <summary>
		/// Gets a single Attachment object from a content item based on the versionID, fieldName, referenceName and language.
		/// </summary>
		/// <param name="versionID"></param>
		/// <param name="fieldName"></param>
		/// <param name="contentReferenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static Attachment GetAttachment(int versionID, string fieldName, string contentReferenceName, string languageCode)
		{
			
			List<Attachment> lst = GetAttachmentsFromVersionID(versionID, fieldName, contentReferenceName, languageCode);
			if (lst == null || lst.Count == 0) return null;
			return lst[0];
		}

		/// <summary>
		/// Gets a list of Attachment objects from a content item based on the versionID, fieldName, referenceName and language.
		/// </summary>
		/// <param name="versionID"></param>
		/// <param name="fieldName"></param>
		/// <param name="contentReferenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static List<Attachment> GetAttachmentsFromVersionID(int versionID, string fieldName, string contentReferenceName, string languageCode)
		{
			

			Agility.Web.AgilityContentServer.AgilityContent serverContent = BaseCache.GetContent(contentReferenceName, languageCode, AgilityContext.WebsiteName);

			if (serverContent == null
				|| serverContent.DataSet == null
				|| ! serverContent.DataSet.Tables.Contains("Attachments")) return null;

			var dtAttachments = serverContent.DataSet.Tables["Attachments"];
			//TODO: Add an index to this...

			if (dtAttachments.DefaultView.Sort != "versionID, managerID")
			{
				if(!dtAttachments.Columns.Contains("versionID")){
					dtAttachments.Columns.Add("versionID");
				}

				if(!dtAttachments.Columns.Contains("managerID")){
					dtAttachments.Columns.Add("managerID");
				}

				dtAttachments.DefaultView.Sort = "versionID, managerID";
				
			}

			var rows = dtAttachments.DefaultView.FindRows(new object[] { versionID, fieldName });
			 


			//DataRow[] rows = serverContent.DataSet.Attachments.Select(string.Format("versionID={0} AND managerID='{1}'", versionID, fieldName), "itemOrder");
			List<Attachment> atts = new List<Attachment>(rows.Length);
			string appRoot = string.Empty;
			if (AgilityContext.HttpContext != null && AgilityContext.HttpContext.Request != null)
			{
				appRoot = "/"; //TODO: figure out ApplicationPath AgilityContext.HttpContext.Request.ApplicationPath;
				if (appRoot == null) appRoot = "/";
				if (!appRoot.EndsWith("/"))
				{
					appRoot += "/";
				}
			}

			foreach (DataRowView drv in rows)
			{
				DataRow row = drv.Row;
				string filename = row["FileName"] as string;

				Attachment att = new Attachment();
				StringBuilder sb = new StringBuilder(appRoot);
				if (filename.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) 
					|| filename.StartsWith("//", StringComparison.CurrentCultureIgnoreCase))
				{
					sb = new StringBuilder(filename);

				}

				//ONLY SUPPORT FULLY QUALIFIED URLS IN ATTACHMENTS
				//else if (row.Filename.StartsWith("/"))
				//{
				//	sb.Append("ecms.ashx").Append(row.Filename);
				//}
				//else
				//{
				//	sb.Append("ecms.ashx/").Append(row.GUID).Append("/");
				//	sb.Append(HttpUtility.UrlEncode(contentReferenceName.Replace("/", string.Empty).Replace("\\", string.Empty))).Append("/");
				//	sb.Append(Agility.Web.Util.Url.RemoveSpecialCharacters(row.Filename));
				//}


				att.URL = sb.ToString();
				att.Label = row["Label"] as string;
				att.Target = row["Target"] as string;
				att.FileName = row["Filename"] as string;
				if (!row.IsNull("FileSize"))
				{
					int fileSize = 0;
					if(!int.TryParse($"{row["FileSize"]}", out fileSize))
					att.FileSize = fileSize;
				}
				
				if (!row.IsNull("itemOrder"))
				{
					att.ItemOrder = string.Format("{0}", row["itemOrder"]).ToInteger(0);
				}

				if (row.Table.Columns.Contains("Width") && !row.IsNull("Width"))
				{
					att.Width = string.Format("{0}", row["Width"]).ToInteger(0);
				}


				if (row.Table.Columns.Contains("Height") && !row.IsNull("Height"))
				{
					att.Height = string.Format("{0}", row["Height"]).ToInteger(0);
				}
				

				atts.Add(att);				


				//get the thumbnails for this attachment..
				if (serverContent.DataSet.Tables.Contains("AttachmentThumbnails") 
					&& serverContent.DataSet.Tables["AttachmentThumbnails"].Rows.Count > 0)
				{
					
					DataRow[] trows = serverContent.DataSet.Tables["AttachmentThumbnails"].Select(string.Format("versionID={0} AND managerID='{1}'", versionID, fieldName));

					foreach (DataRow thumRow in trows) 
					{					
						Thumbnail thm = GetThumbnailFromRow(thumRow);
						att.Thumbnails[thm.Name] = thm;
					}
				}

			}

			//sort it...
			var q = from a in atts
					orderby a.ItemOrder
					select a;
			
			return q.ToList();

		}


		/// <summary>
		/// Gets an AttachmentThumbnail object from a content item based the versionID, fieldName, thumbnailName, contentReferenceName, and languageCode.
		/// </summary>
		/// <param name="versionID"></param>
		/// <param name="fieldName"></param>
		/// <param name="thumbnailName"></param>
		/// <param name="contentReferenceName"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static Thumbnail GetAttachmentThumbnail(int versionID, string fieldName, string thumbnailName, string contentReferenceName, string languageCode)
		{
			Agility.Web.AgilityContentServer.AgilityContent serverContent = BaseCache.GetContent(contentReferenceName, languageCode, AgilityContext.WebsiteName);

			if (serverContent == null
				|| serverContent.DataSet == null
				|| ! serverContent.DataSet.Tables.Contains("AttachmentThumbnails") ) return null;


			DataRow[] rows = serverContent.DataSet.Tables["AttachmentThumbnails"].Select(string.Format("versionID={0} AND managerID='{1}' AND thumbnailName = '{2}'", versionID, fieldName, thumbnailName));

			if (rows.Length == 0) return null;

			
			DataRow row = rows[0];

			Thumbnail thm = GetThumbnailFromRow(row);

			return thm;

		}

		private static Thumbnail GetThumbnailFromRow(DataRow row)
		{
			Thumbnail thm = new Thumbnail();
			thm.Name = string.Format("{0}", row["ThumbnailName"]);
			thm.URL = string.Format("{0}", row["URL"]);

			if (!row.IsNull("Width"))
			{
				int w = -1;
				if (!int.TryParse($"{row["Width"]}", out w)) w = 0;

				thm.Width = w;
			}

			if (!row.IsNull("Height"))
			{
				int h = -1;
				if (!int.TryParse($"{row["Height"]}", out h)) h = 0;

				thm.Height = h;
			}
			return thm;
		}




		/// <summary>
		/// Gets the FileInfo object for an Attachment file.
		/// </summary>
		/// <param name="guidStr"></param>
		/// <param name="filename"></param>
		/// <param name="languageCode"></param>
		/// <param name="websiteName"></param>
		/// <returns></returns>
		public static FileInfo GetAttachmentFileInfo(string guidStr, string filename, string languageCode, string websiteName) 
		{
			if (!string.IsNullOrEmpty(filename) && filename.StartsWith("/"))
			{
				//centrally located attachment.
				return BaseCache.GetDocument(filename, languageCode, websiteName);
			}
			else
			{
				//attachment stored on the item itself
				return BaseCache.GetAttachment(guidStr, filename, languageCode, websiteName);
			}
		}

		/// <summary>
		/// Gets the FileInfo for a Document file with the specified language.
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		public static FileInfo GetDocumentFileInfo(string filepath, string languageCode)
		{
			return BaseCache.GetDocument(filepath, languageCode, AgilityContext.WebsiteName);	
		}

		/// <summary>
		/// Gets the FileInfo for a Document file in the current language.
		/// </summary>
		/// <param name="filepath"></param>
		/// <returns></returns>
		public static FileInfo GetDocumentFileInfo(string filepath)
		{
			return GetDocumentFileInfo(filepath, AgilityContext.LanguageCode);
		}


		#region Utility Methods

		static public Config CreateAPIConfigObject(AgilityContentServer.AgilityDomainConfiguration domainConfiguration) {
			
			if (domainConfiguration == null) return null;

			Config config = new Config();
			config.DomainName = domainConfiguration.DomainName;
			config.DefaultLanguageCode = domainConfiguration.LanguageCode;
			config.IsStagingDomain = domainConfiguration.IsStagingDomain;
			config.EnableOutputCache = domainConfiguration.EnableOutputCache;
			config.ID = domainConfiguration.ID;
			config.LanguageCode = domainConfiguration.LanguageCode;
			config.Name = domainConfiguration.Name;
			config.OutputCacheSlidingExpiration = domainConfiguration.OutputCacheSlidingExpiration;
			config.OutputCacheTimeoutSeconds = domainConfiguration.OutputCacheTimeoutSeconds;
			config.StatsTrackingScript = domainConfiguration.StatsTrackingScript;
			config.DefaultLoginUser = domainConfiguration.DefaultLoginUser;
			config.GlobalCss = domainConfiguration.GlobalCss;
			config.ErrorEmails = domainConfiguration.ErrorEmails;
			config.ExtensionlessUrls = domainConfiguration.XExtensionlessUrls;
			

			if (domainConfiguration.CacheItemPrioritySpecified && domainConfiguration.CacheItemPriority > 0)
			{
				config.CacheItemPriority = (CacheItemPriority)domainConfiguration.CacheItemPriority;
			}
			if (domainConfiguration.Languages != null)
			{
				config.Languages = new Language[domainConfiguration.Languages.Length];
				int i = 0;
				foreach (AgilityContentServer.AgilityLanguage al in domainConfiguration.Languages)
				{
					Language lang = new Language();
					lang.LanguageCode = al.LanguageCode;
					lang.LanguageName = al.LanguageName;
					config.Languages[i] = lang;
					i++;
				}
			}
			else
			{
				config.Languages = new Language[1];
				Language lang = new Language();
				lang.LanguageCode = "en-us";
				lang.LanguageName = "English";
				config.Languages[0] = lang;
			}
			return config;

		}


		static public Sitemap CreateAPISitemapObject(AgilityContentServer.AgilitySitemap serverSitemap)
		{
			if (serverSitemap == null) return null;

			Sitemap sitemap = new Sitemap();
			sitemap.ID = serverSitemap.ID;
			sitemap.LanguageCode = serverSitemap.LanguageCode;
			sitemap.Name = serverSitemap.Name;
			
			XmlDocument siteXML = new XmlDocument();

			siteXML.LoadXml(serverSitemap.SitemapXml);

			//if possible, filter the xml document based on the current channel...
			XmlNode channelNode = siteXML.SelectSingleNode(string.Format("//ChannelNode[@channelID='{0}']", AgilityContext.CurrentChannel.ID));
			if (channelNode != null)
			{
				XmlDocument clonedDoc = new XmlDocument();
				XmlNode rootNode = clonedDoc.CreateNode(XmlNodeType.Element, "SiteMap", "");
				clonedDoc.AppendChild(rootNode);

				//copy the pages from the channel to the new node...
				foreach (XmlNode childNode in channelNode.ChildNodes)
				{
					XmlNode clonedNode = childNode.CloneNode(true);
					XmlNode importNode = clonedDoc.ImportNode(clonedNode, true);
					rootNode.AppendChild(importNode);
				}

				siteXML = clonedDoc;
			}


			sitemap.SitemapXml = siteXML;
			return sitemap;
		}

		static public TagList CreateAPITagListObject(AgilityContentServer.AgilityTagList serverTagList)
		{
			if (serverTagList == null || serverTagList.DSTags == null) return null;

			TagList tagList = new TagList();
			tagList._dsTags = serverTagList.DSTags;
			tagList.LanguageCode = serverTagList.LanguageCode;
			return tagList;
		}


		static public Objects.AgilityPage CreateAPIPageObject(AgilityContentServer.AgilityPage serverPage)
		{
			if (serverPage == null) return null;
			
	
			Objects.AgilityPage page = new Objects.AgilityPage();
			page.ServerPage = serverPage;
			page.ID = serverPage.ID;
			page.LanguageCode = serverPage.LanguageCode;
			page.Name = serverPage.Name;
			page.Title = serverPage.Title;
			page.PullDate = serverPage.PullDate;
			page.ReleaseDate = serverPage.ReleaseDate;
			page.State = (ItemState)serverPage.State;
			
			page.TemplatePath = serverPage.TemplatePath;
			page.TemplateID = serverPage.XTemplateID;

			page.IsPublished = serverPage.IsPublished;

			page.MetaTags = serverPage.MetaTags;
			page.MetaKeyWords = serverPage.MetaKeyWords;
			page.MetaTagsRaw = serverPage.MetaTagsRaw;
			page.RedirectURL = serverPage.RedirectURL;
			
			page.IncludeInStatsTracking = serverPage.IncludeInStatsTracking;
			page.CustomAnalyticsScript = serverPage.CustomAnalyticsScript;
			page.ExcludeFromOutputCache = serverPage.ExcludeFromOutputCache;
			
			if (serverPage.RequiresAuthenticationSpecified)
			{
				page.RequiresAuthentication = serverPage.RequiresAuthentication;
			}


			if (serverPage.ContentSections != null)
			{
				page.ContentSections = new Objects.ContentSection[serverPage.ContentSections.Length];
			

				page.ContentSections = Array.ConvertAll<AgilityContentServer.ContentSection, Objects.ContentSection>(
					serverPage.ContentSections,
					new Converter<AgilityContentServer.ContentSection, ContentSection>
						(
							delegate(Agility.Web.AgilityContentServer.ContentSection contentSection)
							{
								ContentSection c = new ContentSection();
								c.Name = contentSection.Name;
								c.ContentReferenceName = contentSection.ContentReferenceName;
								c.FilterExpression = contentSection.FilterExpression;
								c.SortExpression = contentSection.SortExpression;							
								c.TemplateMarkup = contentSection.TemplateMarkup;
								c.UserControlPath = contentSection.UserControlPath;
								c.ModuleOrder = contentSection.XModuleOrder;
								c.ModuleID = contentSection.ModuleID;
								c.ContentSectionOrder = contentSection.XContentSectionOrder;
								c.ExperimentID = contentSection.XPExperimentID;
								
								return c;
							}
						)
					);

				//sort the content sections/modules
				Array.Sort<ContentSection>(page.ContentSections, delegate(ContentSection c1, ContentSection c2)
				{
					if (c1.ContentSectionOrder != c2.ContentSectionOrder)
					{
						return c1.ContentSectionOrder.CompareTo(c2.ContentSectionOrder);
					}
					else
					{
						return c1.ModuleOrder.CompareTo(c2.ModuleOrder);
					}
				});
			}
			else
			{
				page.ContentSections = new Objects.ContentSection[0];

				Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("Page ID {0}, {1}, {2} has no content sections", serverPage.ID, serverPage.Title, serverPage.LanguageCode));
			}


			//check for a dynamic page, return it with this page.
			if (serverPage.DynamicPageItem != null)
			{
				page.DynamicPageItem = new DynamicPageItem(serverPage.DynamicPageItem);
			}


			return page;
		}

		//static public AgilityAttachment CreateServerAttachmentObject(Attachment attachment, string referenceName, int contentID)
		//{

		//    AgilityContentServer.AgilityAttachment serverAttachment = new AgilityAttachment();
		//    serverAttachment.Blob = attachment.Blob;
		//    serverAttachment.ContentItemID = contentID;
		//    serverAttachment.FileName = attachment.FileName;
		//    serverAttachment.FileSize = attachment.FileSize;
		//    serverAttachment.Label = attachment.Label;
		//    serverAttachment.ManagerID = attachment.ManagerID;
		//    serverAttachment.ContentReferenceName = referenceName;
		//    return serverAttachment;
		//}

		static public AgilityContent CreateAPIContentObject(AgilityContentServer.AgilityContent serverContent)
		{
			if (serverContent == null) return null;

			Objects.AgilityContent content = new AgilityContent();

			content._ID = serverContent.ID;
			content._name = serverContent.Name;
			content._languageCode = serverContent.LanguageCode;
			content._referenceName = serverContent.ReferenceName;
			content._isTimedReleaseEnabled = serverContent.IsTimedReleaseEnabled;
			content._defaultSort = serverContent.DefaultSort;
			content._contentSet = serverContent.DataSet;

            if (content._contentSet != null && (content._contentSet.Tables["ContentItems"] != null))
			{

				content._contentSet.Tables["ContentItems"].ExtendedProperties["referenceName"] = serverContent.ReferenceName;
			}

			return content;
		}

		static public AgilityContentServer.AgilityContent CreateServerContentObject(AgilityContent content)
		{
			AgilityContentServer.AgilityContent serverContent = new AgilityContentServer.AgilityContent();

			serverContent.ID = content._ID;
			serverContent.Name = content._name;
			serverContent.LanguageCode = content._languageCode;
			serverContent.ReferenceName = content._referenceName;
			serverContent.IsTimedReleaseEnabled = content._isTimedReleaseEnabled;
			serverContent.DefaultSort = content._defaultSort;
			serverContent.DataSet = content._contentSet;

			return serverContent;
		}

		#endregion


		public static string GetAgilityVaryByCustomString(HttpContext context)
		{

			StringBuilder sb = new StringBuilder();
			string rawUrl = context.Request.GetEncodedUrl();
			if (rawUrl.IndexOf("?") != -1)	rawUrl = rawUrl.Substring(0, rawUrl.IndexOf("?"));
			sb.Append(rawUrl).Append(".");

			DigitalChannel channel = context.Items[AgilityContext.CACHEKEY_CURRENTCHANNEL] as DigitalChannel;

			if (channel == null)
			{
				try
				{
					//if the channel hasn't been set, try to calculate it...
					string userAgent = context.Request.Headers["User-Agent"];
					if (userAgent == null) userAgent = string.Empty;
					Agility.Web.HttpModules.AgilityHttpModule.CalculateChannel(context.Request.GetEncodedUrl(), userAgent, false);
				}
				catch { }
			}


			channel = context.Items[AgilityContext.CACHEKEY_CURRENTCHANNEL] as DigitalChannel;

			if (channel != null)
			{
				sb.Append(channel.ID);
			}

			
			sb.Append(".").Append(AgilityContext.LanguageCode);
			sb.Append(".agilityChannel-");
			
			//check the current mode from a cookie
			string cookieName = Current.Settings.WebsiteName + "_IsPreview";

			bool isPreview = false;

			//attempt to get the value from the request, then cookie
			object tmp = AgilityContext.HttpContext.Items[cookieName];
			if (tmp is bool)
			{
				isPreview = (bool)tmp;
			}
			else
			{

				string cookieValue = context.Request.Cookies[cookieName];
				bool.TryParse(cookieValue, out isPreview);
				
			}

			if ((! string.IsNullOrEmpty(context.Request.Query["agilitypreviewkey"])) || isPreview)
			{
				//if we are in preview mode, and SOMEHOW this thing is still output caching, make it a unique piece of cache
				sb.Append(Guid.NewGuid());
			}

			sb.Append(".").Append(AgilityContext.AdditionalVaryByCustomString);

            string s = sb.ToString().ToLowerInvariant();
			return s;		


		}

        public static AgilityModuleInstance GetModule(int moduleID)
        {
            if (moduleID <= 0) return null;

            var module =  BaseCache.GetModule(moduleID, AgilityContext.WebsiteName);
            using (StringReader sr = new StringReader(module.XmlSchema))
            {
                DataSet ds = new DataSet();
                ds.ReadXmlSchema(sr);

                string controllerName = ds.ExtendedProperties["MVCController"] as string;
                string action = ds.ExtendedProperties["MVCAction"] as string;

                return new AgilityModuleInstance
                {
                    ModuleName = module.Name,
                    TemplatePath = module.ControlPath,
                    Markup = module.Markup,
                    ControllerName = controllerName,
                    Action = action,
                    ReferenceName = module.ReferenceName
                };
            }
        }
	}
		
}
