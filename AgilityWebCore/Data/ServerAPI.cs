using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agility.Web.Configuration;
using System.Security.Cryptography;
using Agility.Web.Objects.ServerAPI;
using System.Reflection;
using System.Net;
using System.Collections.Specialized;
using Agility.Web.Objects;
using System.IO;
using Newtonsoft.Json;

namespace Agility.Web
{
    public class ServerAPI
    {

        public static string WebsiteName;
        public static string WebsiteSecurityKey;

        public static string APIURL
        {
            get
            {
                string url = Current.Settings.ContentServerUrl;
                url = url.Substring(0, url.IndexOf("/", url.IndexOf("://") + 4));
                url = string.Format("{0}/Services/API.ashx/", url);

                return url;
            }
        }

        public static string GetInitializationScript()
        {

            string url = Current.Settings.ContentServerUrl;
            url = url.Substring(0, url.IndexOf("/", url.IndexOf("://") + 4));
            url = string.Format("{0}/Services/API.ashx/", url);

            return string.Format("Agility.CMS.API.OnInit(\"{0}\", \"{1}\"); ", url, AgilityContext.WebsiteName);
        }

        public static string CreateHash(string method, IEnumerable<string> stringParams)
        {

            string websiteName = WebsiteName;
            string securityKey = WebsiteSecurityKey;

            if (string.IsNullOrEmpty(websiteName))
            {
                websiteName = AgilityContext.WebsiteName;
            }

            if (string.IsNullOrEmpty(WebsiteSecurityKey))
            {
                securityKey = Current.Settings.SecurityKey;
            }


            //recreate the hash and test it
            StringBuilder sb = new StringBuilder();
            sb.Append(websiteName).Append(".");
            sb.Append(securityKey).Append(".");
            sb.Append(method).Append(".");

            foreach (string s in stringParams.OrderBy(t => t))
            {
                sb.Append(s).Append(".");
            }

            sb.Append(websiteName);

            return CreateHash(sb.ToString());
        }

        public static String CreateHash(String s)
        {
            byte[] bytes = UTF8Encoding.UTF8.GetBytes(s);
            SHA1 sha = new SHA1Managed();
            byte[] data = sha.ComputeHash(bytes);

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();


            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        private static string BuildAPIUrl(string methodName, Dictionary<string, string> args)
        {
                if(string.IsNullOrEmpty(APIURL))
                {
                    return null;
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(APIURL);

               

                //ensure the url ends with /
                if (APIURL.LastIndexOf("/") != APIURL.Length - 1)
                {
                    sb.Append("/");
                }

                //create the base url for the call
                sb.AppendFormat("{0}?website={1}", methodName, System.Web.HttpUtility.UrlEncode(AgilityContext.WebsiteName).Replace("+", "%20"));


                foreach(var prop in args.Keys) 
                {
                    string propValue = System.Web.HttpUtility.UrlEncode(args[prop]).Replace("+", "%20");

                    sb.AppendFormat("&{0}={1}", prop, propValue);
                }

                return sb.ToString();

        }

		public static string GetSitemap(string languageCode)
		{
			
			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("languageCode", languageCode);

			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("GetSitemap", values);
			args.Add("hash", hash);

			using (WebClient wc = new WebClient())
			{
				wc.Encoding = Encoding.UTF8;
				string result = wc.DownloadString(BuildAPIUrl("GetSitemap", args));
				return result;
			}			

		}

		public static string GetPage(int pageID, string languageCode)
		{

			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("pageID", pageID.ToString());
			args.Add("languageCode", languageCode);

			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("GetPage", values);
			args.Add("hash", hash);

			using (WebClient wc = new WebClient())
			{
				wc.Encoding = Encoding.UTF8;
				string result = wc.DownloadString(BuildAPIUrl("GetPage", args));
				return result;
			}

		}

		public static string SavePage(int pageID, string languageCode, string pageItemEncoded)
		{
			return SavePage(pageID, languageCode, pageItemEncoded, -1, -1);
		}

		public static string SavePage(int pageID, string languageCode, string pageItemEncoded, int placeBeforePageID)
		{
			return SavePage(pageID, languageCode, pageItemEncoded, placeBeforePageID, -1);
		}

		public static string SavePage(int pageID, string languageCode, string pageItemEncoded, int placeBeforePageID, int channelID)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("pageID", pageID.ToString());
				args.Add("languageCode", languageCode);
				//if (placeBeforePageID > 0)
				//{
					args.Add("placeBeforePageID", placeBeforePageID.ToString());
				//}
				//if (channelID > 0)
				//{
					args.Add("channelID", channelID.ToString());
				//}
				

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("SavePage", values);

				args.Add("hash", hash);



				var url = BuildAPIUrl("SavePage", args);

				//SubmitPostData(url, {contentItem:contentItemStr, attachments:attachmentStr}, callback);

				using (WebClient wc = new WebClient())
				{
					NameValueCollection formData = new NameValueCollection();
					formData["pageItem"] = pageItemEncoded;
					
					// add more form field / values here

					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					byte[] responseBytes = webClient.UploadValues(url, "POST", formData);
					string response = Encoding.UTF8.GetString(responseBytes);


					return response;
				}
				
			}
			catch (Exception ex)
			{
				throw new ApplicationException(string.Format("The page with ID {0} for language {1} could not be saved.", pageID, languageCode), ex);
			}

		}

		public static string RemoveModuleFromPage(int pageID, string languageCode, string contentReferenceName)
		{
			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("pageID", pageID.ToString());
			args.Add("languageCode", languageCode);
			args.Add("contentReferenceName", contentReferenceName);

			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("RemoveModuleFromPage", values);
			args.Add("hash", hash);

			using (WebClient wc = new WebClient())
			{
				wc.Encoding = Encoding.UTF8;
				string result = wc.DownloadString(BuildAPIUrl("RemoveModuleFromPage", args));
				return result;
			}
		}

		public static string AddModuleToPage(int pageID, string languageCode, string moduleZoneReferenceName, string moduleDefinitionReferenceName) 
		{
			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("pageID", pageID.ToString());
			args.Add("languageCode", languageCode);
			args.Add("moduleZoneReferenceName", moduleZoneReferenceName);
			args.Add("moduleDefinitionReferenceName", moduleDefinitionReferenceName);

			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("AddModuleToPage", values);
			args.Add("hash", hash);

			using (WebClient wc = new WebClient())
			{
				wc.Encoding = Encoding.UTF8;
				string result = wc.DownloadString(BuildAPIUrl("AddModuleToPage", args));
				return result;
			}
		}

        public static string GetContentItems(GetContentItemArgs args)
        {
            try
            {

                string hash = CreateHash("SelectContentItems", GetArgValueList(args));
                args.hash = hash;



                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                using (WebClient wc = new WebClient())
                {

					string url = BuildAPIUrl("SelectContentItems", GetArgPropValueData(args));
					wc.Encoding = Encoding.UTF8;
                    string result = wc.DownloadString(url);

                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

		public static string GetThumbnailRootUrl()
		{
			try
			{
				Dictionary<string, string> args = new Dictionary<string, string>();
				List<string> values = new List<string>();

				string hash = CreateHash("GetThumbnailRootUrl", values);
				args.Add("hash", hash);

				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;

					string result = wc.DownloadString(BuildAPIUrl("GetThumbnailRootUrl", args));
					return result;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static string GetMediaID(string originKey)
		{
			try
			{
				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("originKey", originKey);
				
				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetMediaID", values);
				args.Add("hash", hash);

				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					string result = wc.DownloadString(BuildAPIUrl("GetMediaID", args));
					return result;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

        public static string GetContentItem(int contentID, string languageCode)
        {
            try
            {
                Dictionary<string, string> args = new Dictionary<string, string>();
                args.Add("contentID", contentID.ToString());
                args.Add("languageCode", languageCode);
         

                List<string> values = new List<string>();
                foreach (string key in args.Keys)
                {
                    values.Add(args[key]);
                }

                string hash = CreateHash("GetContentItem", values);
                args.Add("hash", hash);

                using (WebClient wc = new WebClient())
                {
					wc.Encoding = Encoding.UTF8;
                    string result = wc.DownloadString(BuildAPIUrl("GetContentItem", args));

                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

		public static string GetGalleryByID(int id)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("id", id.ToString());

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetGalleryByID", values);

				args.Add("hash", hash);

				var url = BuildAPIUrl("GetGalleryByID", args);

				using (WebClient wc = new WebClient())
				{

					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					string result = wc.DownloadString(url);

					return result;
				}

			}
			catch (Exception ex)
			{
				throw new ApplicationException(string.Format("The Gallery with id {0} could not be returned.", id), ex);
			}

		}

		public static string GetGalleryByName(string name)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("name", name);
				
				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetGalleryByName", values);

				args.Add("hash", hash);

				var url = BuildAPIUrl("GetGalleryByName", args);

				using (WebClient wc = new WebClient())
				{

					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					string result = wc.DownloadString(url);

					return result;
				}

			}
			catch (Exception ex)
			{
				throw new ApplicationException(string.Format("The Gallery with name {0} could not be returned.", name), ex);
			}

		}

		/// <summary>
		/// Saves a Gallery. 
		/// </summary>
		/// <param name="galleryID">Pass in -1 for a new gallery.</param>
		/// <param name="name"></param>
		/// <param name="description"></param>
		/// <param name="thumbnailSettingsEncoded">Encoded list of ThumbnailSetting objects</param>
		/// <returns></returns>
		public static string SaveGallery(int galleryID, string name, string description, string thumbnailSettingsEncoded)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("galleryID", galleryID.ToString());
				args.Add("name", name);
				args.Add("description", description);

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				//List<ThumbnailSettings> settings

				

				string hash = CreateHash("SaveGallery", values);

				args.Add("hash", hash);

				var url = BuildAPIUrl("SaveGallery", args);

				using (WebClient wc = new WebClient())
				{
					
					NameValueCollection formData = new NameValueCollection();
					formData["thumbnails"] = thumbnailSettingsEncoded;
                    // add more form field / values here

                    WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
                    byte[] responseBytes = webClient.UploadValues(url, "POST", formData);
                    string response = Encoding.UTF8.GetString(responseBytes);

					return response;
				}

			}
			catch (Exception ex)
			{
				throw new ApplicationException(string.Format("The Gallery with ID {0} could not be saved.", galleryID), ex);
			}

		}

		public static string AddImageToGallery(int galleryID, string fileName, string contentType, Stream fileData)
		{

			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("galleryID", galleryID.ToString());
			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("AddImageToGallery", values);
			args.Add("hash", hash);

			var url = BuildAPIUrl("AddImageToGallery", args);

			NameValueCollection extraParams = new NameValueCollection();
			return HttpUploadFile(url, fileName, fileData, contentType, extraParams);


		}

        public static string SaveContentItem(int contentID, string referenceName, string languageCode, string contentItemEncoded, string attachmentsEncoded)
        {
            try
            {

                Dictionary<string, string> args = new Dictionary<string, string>();
                args.Add("contentID", contentID.ToString());
                args.Add("languageCode", languageCode);
                args.Add("referenceName", referenceName);

                List<string> values = new List<string>();
                foreach (string key in args.Keys)
                {
                    values.Add(args[key]);
                }

                string hash = CreateHash("SaveContentItem", values);

                args.Add("hash", hash);



                var url = BuildAPIUrl("SaveContentItem", args);

				//SubmitPostData(url, {contentItem:contentItemStr, attachments:attachmentStr}, callback);

				ServicePointManager.ServerCertificateValidationCallback +=  BaseCache.ValidateRemoteCertificate;


				using (WebClient webClient = new WebClient())
                {
                    NameValueCollection formData = new NameValueCollection();
                    formData["contentItem"] = contentItemEncoded;
					formData["attachments"] = attachmentsEncoded;
                    // add more form field / values here

                   
					
					webClient.Encoding = Encoding.UTF8;
                    byte[] responseBytes = webClient.UploadValues(url, "POST", formData);
                    string response = Encoding.UTF8.GetString(responseBytes);


                    return response;
                }
            }
            catch (Exception ex)
            {
				throw new ApplicationException(string.Format("The content with ID {0} in {1} for language {2} could not be saved.", contentID, referenceName, languageCode), ex);
            }

        }

		public static string RequestApproval(int contentID, string languageCode)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("contentID", contentID.ToString());
				args.Add("languageCode", languageCode);				

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("RequestApproval", values);

				args.Add("hash", hash);

				var url = BuildAPIUrl("RequestApproval", args);

				using (WebClient wc = new WebClient())
				{
					
					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					byte[] responseBytes = webClient.DownloadData(url);
					string response = Encoding.UTF8.GetString(responseBytes);


					return response;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static string PublishContent(int contentID, string languageCode)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("contentID", contentID.ToString());
				args.Add("languageCode", languageCode);				

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("PublishContent", values);

				args.Add("hash", hash);



				var url = BuildAPIUrl("PublishContent", args);

				using (WebClient wc = new WebClient())
				{
					
					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					byte[] responseBytes = webClient.DownloadData(url);
					string response = Encoding.UTF8.GetString(responseBytes);


					return response;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static string DeleteContent(int contentID, string languageCode)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("contentID", contentID.ToString());
				args.Add("languageCode", languageCode);

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("DeleteContent", values);

				args.Add("hash", hash);



				var url = BuildAPIUrl("DeleteContent", args);

				using (WebClient wc = new WebClient())
				{

					WebClient webClient = new WebClient();
					webClient.Encoding = Encoding.UTF8;
					byte[] responseBytes = webClient.DownloadData(url);
					string response = Encoding.UTF8.GetString(responseBytes);


					return response;
				}
				
			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static string UploadMedia(string mediaFolder, string fileName, string contentType, Stream fileData)
		{


			Dictionary<string, string> args = new Dictionary<string, string>();
			args.Add("folder", mediaFolder);
			List<string> values = new List<string>();
			foreach (string key in args.Keys)
			{
				values.Add(args[key]);
			}

			string hash = CreateHash("UploadMedia", values);
			args.Add("hash", hash);

			var url = BuildAPIUrl("UploadMedia", args);

			NameValueCollection extraParams = new NameValueCollection();
			return HttpUploadFile(url, fileName, fileData, contentType, extraParams);


		}

		private static string HttpUploadFile(string url, string fileName, Stream fileStream, string contentType, NameValueCollection nvc)
		{
			
			string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

			HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
			wr.ContentType = "multipart/form-data; boundary=" + boundary;
			wr.Method = "POST";
			wr.KeepAlive = true;

			using (Stream rs = wr.GetRequestStream())
			{

				string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
				foreach (string key in nvc.Keys)
				{
					rs.Write(boundarybytes, 0, boundarybytes.Length);
					string formitem = string.Format(formdataTemplate, key, nvc[key]);
					byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
					rs.Write(formitembytes, 0, formitembytes.Length);
				}
				rs.Write(boundarybytes, 0, boundarybytes.Length);

				string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
				string header = string.Format(headerTemplate, "File", fileName, contentType);
				byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
				rs.Write(headerbytes, 0, headerbytes.Length);



				byte[] buffer = new byte[4096];
				int bytesRead = 0;
				while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
				{
					rs.Write(buffer, 0, bytesRead);
				}


				byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
				rs.Write(trailer, 0, trailer.Length);
				
			}

			WebResponse wresp = null;
			try
			{
				wresp = wr.GetResponse();
				using (Stream stream2 = wresp.GetResponseStream())
				{
					using (StreamReader reader2 = new StreamReader(stream2))
					{
						return reader2.ReadToEnd();
					}
				}				
			}
			catch (Exception ex)
			{				
				if (wresp != null)
				{
					wresp.Close();
					wresp = null;
				}

				throw new ApplicationException(string.Format("An error occurred while uploading file {0}.", fileName, ex));
			}
			finally
			{
				
				wr = null;
			}
		}

		public static IEnumerable<LatestIndexItem> GetLatestMediaGalleryIndex(DateTime latestModDate)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("latestModDateStr", latestModDate.ToString("s"));
				
				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetMediaGalleryIndex", values);
				args.Add("hash", hash);

				string result = null;
				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					result = wc.DownloadString(BuildAPIUrl("GetMediaGalleryIndex", args));
				}

				

				//parse the string...
				APIResult<IEnumerable<LatestIndexItem>> res = Deserialize<APIResult<IEnumerable<LatestIndexItem>>>(result);

				CheckResponse(res);

				return res.ResponseData;

			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static IEnumerable<LatestIndexItem> GetLatestContentDefinitionIndex(DateTime latestModDate)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("latestModDateStr", latestModDate.ToString("s"));

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetContentDefinitionIndex", values);
				args.Add("hash", hash);

				string result = null;
				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					result = wc.DownloadString(BuildAPIUrl("GetContentDefinitionIndex", args));
				}



				//parse the string...
				APIResult<IEnumerable<LatestIndexItem>> res = Deserialize<APIResult<IEnumerable<LatestIndexItem>>>(result);

				CheckResponse(res);

				return res.ResponseData;

			}
			catch (Exception ex)
			{
				throw ex;
			}

		}

		public static IEnumerable<LatestContentIndexItem> GetLatestContentItemIndex(int latestVersionID, string languageCode)
		{
			try
			{
				
				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("latestContentItemID", latestVersionID.ToString());
				args.Add("languageCode", languageCode);


				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetContentIndex", values);
				args.Add("hash", hash);

				string result = null;
				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					result = wc.DownloadString(BuildAPIUrl("GetContentIndex", args));
				}

				//parse the string...
				APIResult<IEnumerable<LatestContentIndexItem>> res = Deserialize<APIResult<IEnumerable<LatestContentIndexItem>>>(result);
				CheckResponse(res);
				return res.ResponseData;
				
			}
			catch (Exception ex)
			{
				throw ex;
			}
			
		}
		
		public static IEnumerable<LatestPageIndexItem> GetLatestPageItemIndex(int latestVersionID, string languageCode)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("latestPageItemID", latestVersionID.ToString());
				args.Add("languageCode", languageCode);


				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetPageIndex", values);
				args.Add("hash", hash);

				string result = null;
				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					result = wc.DownloadString(BuildAPIUrl("GetPageIndex", args));
				}

				//parse the string...
				APIResult<IEnumerable<LatestPageIndexItem>> res = Deserialize<APIResult<IEnumerable<LatestPageIndexItem>>>(result);
				CheckResponse(res);
				return res.ResponseData;
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		public static AgilityContentServer.AgilityExperimentListing GetExperimentsDelta(DateTime latestModDate)
		{
			try
			{

				Dictionary<string, string> args = new Dictionary<string, string>();
				args.Add("latestModDateStr", latestModDate.ToString("s"));

				List<string> values = new List<string>();
				foreach (string key in args.Keys)
				{
					values.Add(args[key]);
				}

				string hash = CreateHash("GetExperimentsDelta", values);
				args.Add("hash", hash);

				string result = null;
				using (WebClient wc = new WebClient())
				{
					wc.Encoding = Encoding.UTF8;
					result = wc.DownloadString(BuildAPIUrl("GetExperimentsDelta", args));
				}
				
				//parse the string...
				APIResult<AgilityContentServer.AgilityExperimentListing> res = Deserialize<APIResult<AgilityContentServer.AgilityExperimentListing >>(result);

				CheckResponse(res);

				return res.ResponseData;

			}
			catch (Exception ex)
			{
				Agility.Web.Tracing.WebTrace.WriteInfoLine (string.Format("Error getting Experiments for date: {0} - {1}", latestModDate, ex));
			}

			return null;
		}

		private static void CheckResponse(APIResult result)
		{
			if (result.IsError)
			{
				if (!string.IsNullOrEmpty(result.Message))
				{
					throw new ApplicationException(string.Format("An error occurred in the Content Server REST API: {0}", result.Message));
				}
				else
				{
					throw new ApplicationException("An unknown error occurred in the Content Server REST API.");
				}
			}
		}

        private static List<string> GetArgValueList(object args)
        {
            PropertyInfo[] pInfo = args.GetType().GetProperties();

            List<string> values = new List<string>();
            foreach (PropertyInfo pi in pInfo)
            {
                object value = pi.GetValue(args, null);
                if (value != null)
                {
                    values.Add(string.Format("{0}", value));
                }
            }

            return values;
        }

        private static Dictionary<string, string> GetArgPropValueData(object args)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();

            PropertyInfo[] pInfo = args.GetType().GetProperties();

            List<string> values = new List<string>();
            foreach (PropertyInfo pi in pInfo)
            {
                data.Add(pi.Name, string.Format("{0}", pi.GetValue(args, null)));
            }

            return data;
        }

		public static string Serialize<T>(T obj)
		{

			return JsonConvert.SerializeObject(obj);
			
		}

		public static T Deserialize<T>(string json)
		{

			return JsonConvert.DeserializeObject<T>(json);
			
		}
    }

	public class APIResult
	{

		public bool IsError;
		public string Message;
	
	}


	public class ListingResult<T>
	{

		public int TotalCount;
		public List<T> Items;
	}


	public class APIResult<T> : APIResult
	{

		public T ResponseData;

	}
}
