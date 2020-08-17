using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Agility.Web.AgilityContentServer;
using Agility.Web.Tracing;

using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Web;

using System.Threading;
using Agility.Web.Configuration;
using System.Xml;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Net;
using Agility.Web.HttpModules;
using Agility.Web.Routing;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Agility.Web.Sync;
using System.Reflection;
using Agility.Web.Objects.ServerAPI;
using Agility.Web.Caching;
using Microsoft.Extensions.Caching.Memory;
using Agility.Web.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Newtonsoft.Json;
using Agility.Web.Providers;

namespace Agility.Web
{
    public class BaseCache
    {

        internal const string DYNAMICPAGEINDEX_FILENAME = "DynamicPageIndex";
        internal const string DYNAMICPAGEINDEX_CACHEKEY = "agility_web_dynamicpageindex";

        internal const string DYNAMICPAGEFORMULAINDEX_FILENAME = "DynamicPageFormulaIndex";
        internal const string DYNAMICPAGEFORMULAINDEX_CACHEKEY = "agility_web_dynamicpageformulaindex";

        internal static readonly string PROTO_BUF_ASSEMBLY_NAME = "protobuf-net";
        internal static readonly string PROTO_BUF_DATA_ASSEMBLY_NAME = "protobuf-net-data";

        private static object serverLockObj = new object();
        private static Semaphore _serverClientLockAccess = new Semaphore(3, 3);

        private static ReaderWriterLockSlim _dynamicPageIndexLock = new ReaderWriterLockSlim();
		
        private static object _dynlockAccess = new object();
        private static Dictionary<int, ReaderWriterLockSlim> _dynamicPageFormulaLocks = new Dictionary<int, ReaderWriterLockSlim>(10);
		
		private static ReaderWriterLockSlim GetDynamicPageFormulaLock(int pageID)
        {

            //wait for access the lock collection
            ReaderWriterLockSlim fileLock = null;

            lock (_dynlockAccess)
            {

                //initialize the lock object we will use to lock this write operation				
                if (_dynamicPageFormulaLocks.ContainsKey(pageID))
                {
                    fileLock = _dynamicPageFormulaLocks[pageID];
                }
                else
                {
                    fileLock = new ReaderWriterLockSlim();
                    _dynamicPageFormulaLocks[pageID] = fileLock;
                }

            }
            return fileLock;
        }



        private static int _countThreads = 0;

        /// <summary>
        /// Certificate validation callback.
        /// </summary>
        internal static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {

            return true;
        }

        internal static AgilityContentServerClient CreateContentServerClientInstance()
        {

            //_serverClientLockAccess.WaitOne();

            _countThreads++;

            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

            BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.None);

            string url = Current.Settings.ContentServerUrl;

            if (url.StartsWith("https", StringComparison.CurrentCultureIgnoreCase))
            {
                binding.Security.Mode = BasicHttpSecurityMode.Transport;
            }

            binding.SendTimeout = TimeSpan.FromMinutes(30);
            binding.OpenTimeout = TimeSpan.FromMinutes(1);
            binding.CloseTimeout = TimeSpan.FromMinutes(1);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(30);


            binding.AllowCookies = false;
            binding.BypassProxyOnLocal = false;

			//MOD: old setting -- binding.HostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
			binding.TextEncoding = System.Text.Encoding.UTF8;
			
            //MOD: old setting -- binding.MessageEncoding = WSMessageEncoding.Text;
            binding.TransferMode = TransferMode.Buffered;


            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferSize = int.MaxValue;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            binding.ReaderQuotas.MaxBytesPerRead = 4096;

            AgilityContentServerClient client = new AgilityContentServerClient(binding, new EndpointAddress(url));
            //client.InnerChannel.Closed += new EventHandler(InnerChannel_Closed);

            return client;
        }

        static void InnerChannel_Closed(object sender, EventArgs e)
        {
            _countThreads--;
            //_serverClientLockAccess.Release(1);
        }
		
        internal static AgilityContentServerClient GetAgilityServerClient()
        {
            AgilityContentServerClient _serverClient = null;

            _serverClient = CreateContentServerClientInstance();
            _serverClient.InnerChannel.OperationTimeout = TimeSpan.FromMinutes(30);

            return _serverClient;

        }


        internal const string ITEMKEY_CHANNELS = "channels";
        internal const string ITEMKEY_SITEMAP = "sitemap";
        internal const string ITEMKEY_MODULE = "module";
        internal const string ITEMKEY_CONFIG = "config";
        internal const string ITEMKEY_TAGLIST = "taglist";

        internal const string CACHEKEY_CONFIG = "agility_web_config";
        internal const string CACHEKEY_CHANNELS = "agility_web_channels";

        internal const string ITEMKEY_CONTENTDEFINDEX = "contentdefindex";
        internal const string ITEMKEY_SHAREDCONTENTINDEX = "sharedcontentindex";
        internal const string ITEMKEY_URLREDIRECTIONS = "urlredirections";
        internal const string ITEMKEY_EXPERIMENTS = "experiments";

		internal const string CACHEKEY_PREFIX = "agility_web_";

        /// <summary>
        /// Gets the AgilityDomainConfiguration for a specific websiteName.
        /// </summary>		
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static AgilityDomainConfiguration GetDomainConfiguration(string websiteName)
        {
            //get the itemkey for the domain	
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_CONFIG;
            itemKey.ItemType = typeof(AgilityDomainConfiguration).Name;


            string cacheKey = GetCacheKey(itemKey); // string.Format("{0}.TempCache", GetCacheKey(itemKey));
            AgilityDomainConfiguration domain = null;

            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
				//try to get the item from cache first
				domain = AgilityCache.Get(cacheKey) as AgilityDomainConfiguration;
                if (domain != null) return domain;
            }

            domain = GetItem<AgilityDomainConfiguration>(itemKey, websiteName);

            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
				//put this in cache for 30 seconds in staging mode...
				AgilityCache.Set(cacheKey, domain, TimeSpan.FromSeconds(30));
            }

            return domain;

        }

        internal static AgilityDigitalChannelList GetDigitalChannels(string websiteName)
        {

            //get the itemkey for the domain	
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_CHANNELS;
            itemKey.ItemType = typeof(AgilityDigitalChannelList).Name;

            string cacheKey = string.Format("{0}.TempCache", GetCacheKey(itemKey));

            AgilityDigitalChannelList channels = null;

            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
                //try to get the item from cache first
                channels = AgilityCache.Get(cacheKey) as AgilityDigitalChannelList;
                if (channels != null) return channels;
            }

            channels = GetItem<AgilityDigitalChannelList>(itemKey, websiteName);

            if (channels == null || channels.Channels == null || channels.Channels.Length == 0)
            {
                //create a default channel if we need to...
                channels = new AgilityDigitalChannelList()
                {
                    ID = 1,
                    Channels = new AgilityDigitalChannel[1]
                };

                channels.Channels[0] = new AgilityDigitalChannel()
                {
                    ID = 0,
                    ReferenceName = "Website",
                    DisplayName = "Website",
                    IsDefaultChannel = true
                };

            }


            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
                //put this in cache for 30 seconds in staging mode...
                AgilityCache.Set(cacheKey, channels, TimeSpan.FromSeconds(30));
            }


            return channels;
        }

        internal static NameIndex GetContentDefinitionIndex(string websiteName)
        {

            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_CONTENTDEFINDEX;
            itemKey.ItemType = typeof(NameIndex).Name;

            NameIndex index = GetItem<NameIndex>(itemKey, websiteName);
            return index;
        }
		
        internal static NameIndex GetSharedContentIndex(string websiteName)
        {

            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_SHAREDCONTENTINDEX;
            itemKey.ItemType = typeof(NameIndex).Name;

            NameIndex index = GetItem<NameIndex>(itemKey, websiteName);
            return index;
        }

		internal static AgilityExperimentListing GetExperiments(string websiteName)
		{

			AgilityItemKey itemKey = new AgilityItemKey();
			itemKey.Key = ITEMKEY_EXPERIMENTS;
			itemKey.ItemType = typeof(AgilityExperimentListing).Name;

			AgilityExperimentListing experiments = GetItem<AgilityExperimentListing>(itemKey, websiteName);
			if (experiments == null) experiments = new AgilityExperimentListing();
			if (experiments.Items == null) experiments.Items = new AgilityExperiment[0];
			return experiments;
		}

		/// <summary>
		/// Gets the sitemap object for the given site in the given language.
		/// </summary>
		/// <remarks>
		/// The sitemapXml XmlElement is not filtered on ReleaseDate/PullDate here.  It is filtered later, in its accessor from Agility.Data.
		/// </remarks>
		/// <param name="languageCode"></param>
		/// <param name="websiteName"></param>
		/// <returns></returns>
		internal static AgilitySitemap GetSitemap(string languageCode, string websiteName)
        {
            //get the itemkey for the sitemap	
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_SITEMAP;
            itemKey.LanguageCode = languageCode;
            itemKey.ItemType = typeof(AgilitySitemap).Name;


            AgilitySitemap sitemap = GetItem<AgilitySitemap>(itemKey, websiteName, true);

            return sitemap;
        }

        /// <summary>
        /// Gets the taglist object for the given site in the given language.
        /// </summary>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static AgilityTagList GetTagList(string languageCode, string websiteName)
        {
            //get the itemkey for the sitemap	
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_TAGLIST;
            itemKey.LanguageCode = languageCode;
            itemKey.ItemType = typeof(AgilityTagList).Name;


            AgilityTagList tagList = GetItem<AgilityTagList>(itemKey, websiteName);

            return tagList;
        }


        /// <summary>
        /// Gets an empty dataset representing the Content Definition.
        /// </summary>
        /// <param name="referenceName"></param>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static AgilityContent GetContentDefinition(string referenceName, string languageCode, string websiteName)
        {
            //Staging/Development Mode calls the server
            //Live, gets from Cache.
            AgilityContent content = GetContent(referenceName, languageCode, websiteName);

            //make a copy of this content to work on (so we don't harm the in-memory copy) 		
            AgilityContent defContent = new AgilityContent();
            defContent.DataSet = content.DataSet.Clone();
            defContent.DefaultSort = content.DefaultSort;
            defContent.HasSecondaryData = true;
            defContent.ID = content.ID;
            defContent.IsCompleteSet = content.IsCompleteSet;
            defContent.IsDeleted = content.IsDeleted;
            defContent.IsTimedReleaseEnabled = content.IsTimedReleaseEnabled;
            defContent.LanguageCode = content.LanguageCode;
            defContent.LastAccessDate = content.LastAccessDate;
            defContent.ModuleID = content.ModuleID;
            defContent.Name = content.Name;
            defContent.ReferenceName = content.ReferenceName;
            defContent.DisableRSSOutput = content.DisableRSSOutput;
            defContent.DisableRSSOutputSpecified = content.DisableRSSOutputSpecified;

            defContent.DisableAPIOutput = content.DisableAPIOutput;
            defContent.DisableAPIOutputSpecified = content.DisableAPIOutputSpecified;

            return defContent;
        }

        internal static AgilityAssetMediaGroup GetMediaGroup(int groupID, string websiteName)
        {

            //get the ItemKey key for the Content
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = groupID;
            itemKey.ItemType = typeof(AgilityAssetMediaGroup).Name;

            AgilityAssetMediaGroup group = GetItem<AgilityAssetMediaGroup>(itemKey, websiteName, false);
            return group;
        }

        /// <summary>
        /// Gets a Content object based on a referenceName and a languageCode.
        /// </summary>
        /// <param name="referenceName"></param>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static AgilityContent GetContent(string referenceName, string languageCode, string websiteName)
        {
            return GetContent(referenceName, languageCode, websiteName, false);
        }


        internal static AgilityContent GetContent(string referenceName, string languageCode, string websiteName, bool forceget)
        {
            if (string.IsNullOrEmpty(referenceName)) return null;

            //get the ItemKey key for the Content
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = referenceName;
            itemKey.LanguageCode = languageCode;
            itemKey.ItemType = typeof(AgilityContent).Name;
			itemKey.XAdditionalInfo = "AllowNulls";
			

            string origCacheKey = GetCacheKey(itemKey);
            string schedCacheKey = string.Format("{0}.Scheduled", origCacheKey);

            //check if the content has been used already this request - if so, just use it directly
            AgilityContent scheduledContentInRequest = AgilityContext.HttpContext == null ? null : AgilityContext.HttpContext.Items[schedCacheKey] as AgilityContent;
            if (scheduledContentInRequest != null)
            {
                return scheduledContentInRequest;
            }
            AgilityContent unscheduledContentInRequest = AgilityContext.HttpContext == null ? null : AgilityContext.HttpContext.Items[origCacheKey] as AgilityContent;
            if (unscheduledContentInRequest != null)
            {
                return unscheduledContentInRequest;
            }

            AgilityContent content = GetItem<AgilityContent>(itemKey, websiteName, forceget);
            if (content == null)
            {
				 return null;
            }

            //check if the columns have changed... if so, we need to get file from the file system again (dump cache...)
            if (content.DataSet != null && content.DataSet.ExtendedProperties.ContainsKey("ColumnCount"))
            {
                object cntObject = content.DataSet.ExtendedProperties["ColumnCount"];

				int cnt = -1;
				if (!int.TryParse($"{cntObject}", out cnt)) cnt = -1;


				if (content.DataSet.Tables["ContentItems"] != null)
                {
                    if ((cnt >= 0 ) && (cnt != content.DataSet.Tables["ContentItems"].Columns.Count))
                    {
						//column count has changed, dump from cache and get again
						if (AgilityContext.HttpContext != null)
						{
							AgilityCache.Remove(origCacheKey);
							AgilityCache.Remove(schedCacheKey);
							AgilityCache.Remove(origCacheKey);
						}

                        //get the item again..
                        content = GetItem<AgilityContent>(itemKey, websiteName, forceget);
                    }
                }
            }


            //if we are in staging mode, don't filter
            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
                if (AgilityContext.HttpContext != null) AgilityContext.HttpContext.Items[origCacheKey] = content;
                return content;
            }

            if (!content.IsTimedReleaseEnabled)
            {



                //date/time filtering is not used in the ContentView, so ignore it
                if (content.DataSet != null && content.DataSet.HasChanges())
                {
                    content.DataSet.RejectChanges();
                }

                if (content.DataSet.Tables["ContentItems"] != null)
                {
                    content.DataSet.ExtendedProperties["ColumnCount"] = content.DataSet.Tables["ContentItems"].Columns.Count;
                }
                AgilityContext.HttpContext.Items[origCacheKey] = content;
                return content;
            }
            else
            {
                //check the cached version of the Scheduled stuff
                AgilityContent schedContent = null;
                if (!AgilityContext.IsPreview)
                {
                    //if we are NOT in preview mode, use a separate cache for the scheduled version...
                    schedContent = AgilityCache.Get(schedCacheKey) as AgilityContent;
                    if (schedContent != null && schedContent.DataSet != null)
                    {
                        if (schedContent.DataSet.HasChanges())
                        {
                            schedContent.DataSet.RejectChanges();
                        }

                        if (schedContent.DataSet.ExtendedProperties.ContainsKey("ColumnCount"))
                        {
                            object rawCountObj = schedContent.DataSet.ExtendedProperties["ColumnCount"];
							int rawCount = -1;
							if (!int.TryParse($"{rawCountObj}", out rawCount)) rawCount = -1;


							if (rawCount >= 0 && rawCount == schedContent.DataSet.Tables["ContentItems"].Columns.Count)
                            {
                                AgilityContext.HttpContext.Items[schedCacheKey] = schedContent;
                                return schedContent;
                            }
                        }
                    }
                }


                //filter the content on Release/Pull Dates							
                if (content.DataSet != null
                    && content.DataSet.Tables["ContentItems"] != null
                    && content.DataSet.Tables["ContentItems"].Rows.Count > 0)
                {

                    //make a copy of this content to work on (so we don't harm the in-memory copy) 		
                    schedContent = new AgilityContent();
                    schedContent.DataSet = content.DataSet.Copy();
                    schedContent.DefaultSort = content.DefaultSort;
                    schedContent.HasSecondaryData = true;
                    schedContent.ID = content.ID;
                    schedContent.IsCompleteSet = content.IsCompleteSet;
                    schedContent.IsDeleted = content.IsDeleted;
                    schedContent.IsTimedReleaseEnabled = content.IsTimedReleaseEnabled;
                    schedContent.LanguageCode = content.LanguageCode;
                    schedContent.LastAccessDate = content.LastAccessDate;
                    schedContent.ModuleID = content.ModuleID;
                    schedContent.Name = content.Name;
                    schedContent.ReferenceName = content.ReferenceName;
                    schedContent.DisableRSSOutput = content.DisableRSSOutput;
                    schedContent.DisableRSSOutputSpecified = content.DisableRSSOutputSpecified;

                    schedContent.DisableAPIOutput = content.DisableAPIOutput;
                    schedContent.DisableAPIOutputSpecified = content.DisableAPIOutputSpecified;

                    DataTable dt = schedContent.DataSet.Tables["ContentItems"];
                    DataTable dt2 = schedContent.DataSet.Tables["ContentItems"].Clone();

                    string dateFormat = "MM/dd/yyyy HH:mm";
                    string currentDateString = DateTime.Now.ToString(dateFormat);
                    if ((AgilityContext.IsPreview || Current.Settings.DevelopmentMode) && AgilityContext.PreviewDateTime != DateTime.MinValue)
                    {
                        //set the preview date for release/pull dates if possible
                        currentDateString = AgilityContext.PreviewDateTime.ToString(dateFormat);
                    }

                    string filter = string.Format("ISNULL(releaseDate, #1/1/1900#) <= #{0}# AND ISNULL(pullDate, #1/1/9999#) > #{0}#", currentDateString);

                    string sort = schedContent.DefaultSort;
                    if (string.IsNullOrEmpty(sort)) sort = "ItemOrder, versionID DESC";
                    DataRow[] rows = null;
                    try
                    {
                        rows = dt.Select(filter, sort);
                    }
                    catch
                    {
                        rows = dt.Select(filter, "ItemOrder, versionID DESC");
                    }



                    //perform the filter		
                    Dictionary<int, int> importedItems = new Dictionary<int, int>(rows.Length);
                    foreach (DataRow row in rows)
                    {
						int contentID = -1;
						if (!int.TryParse($"{row["ContentID"]}", out contentID)) contentID = -1;

						int versionID = -1;
						if (!int.TryParse($"{row["VersionID"]}", out versionID)) versionID = -1;
						
                        if (!importedItems.ContainsKey(contentID))
                        {
                            //check if the row is already in the set before adding it							
                            dt2.ImportRow(row);

                            //update the import hashtable
                            importedItems.Add(contentID, versionID);
                        }
                        else if (importedItems[contentID] < versionID)
                        {

                            //if the row already exists for this itemContainer, take the one published LAST
							
                            DataRow[] prevRows = dt2.Select(string.Format("versionID={0}", importedItems[contentID]));
                            if (prevRows.Length > 0) prevRows[0].Delete();
                            dt2.ImportRow(row);

                            //update the import hashtable
                            importedItems[contentID] = versionID;
                        }
                    }

                    schedContent.DataSet.Tables.Remove(dt);
                    schedContent.DataSet.Tables.Add(dt2);
					
                    schedContent.DataSet.AcceptChanges();

                    //put this in a special cache (if we are not in preview mode)...
                    if (!AgilityContext.IsPreview)
                    {
                        CacheDependency dep = new CacheDependency(new string[0], new string[1] { origCacheKey });
						AgilityCache.Set(schedCacheKey, schedContent, TimeSpan.FromMinutes(5), dep, CacheItemPriority.NeverRemove);
                    }


                    schedContent.DataSet.ExtendedProperties["ColumnCount"] = schedContent.DataSet.Tables["ContentItems"].Columns.Count;
                    AgilityContext.HttpContext.Items[schedCacheKey] = schedContent;
                    return schedContent;
                }
            }

            if (content.DataSet.Tables["ContentItems"] != null)
            {
                content.DataSet.ExtendedProperties["ColumnCount"] = content.DataSet.Tables["ContentItems"].Columns.Count;
            }
            AgilityContext.HttpContext.Items[origCacheKey] = content;
            return content;
        }


        internal static AgilityUrlRedirectionList GetUrlRedirections(string websiteName)
        {

            //get the ItemKey key for the Content
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = ITEMKEY_URLREDIRECTIONS;
            itemKey.ItemType = typeof(AgilityUrlRedirectionList).Name;

            AgilityUrlRedirectionList list = null;

            string cacheKey = string.Format("{0}.TempCache", GetCacheKey(itemKey));

            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
                //try to get the item from cache first
                list = AgilityCache.Get(cacheKey) as AgilityUrlRedirectionList;
                if (list != null) return list;
            }

            list = GetItem<AgilityUrlRedirectionList>(itemKey, websiteName);


            if (list == null)
            {
                if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
                {
                    //in staging mode, throw an error if there is a request for content that doesn't exist..
                    throw new ApplicationException("The URL Redirections could not be loaded.");
                }

                list = new AgilityUrlRedirectionList();
            }

            if (list.Redirections == null) list.Redirections = new AgilityUrlRedirection[0];

            //if we are in staging mode, cache this list for 15 seconds (so multiple resource requests don't require a lookup)
            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
            {
				AgilityCache.Set(cacheKey, list, TimeSpan.FromMinutes(1), null, CacheItemPriority.NeverRemove);
            }


            return list;
        }



        /// <summary>
        /// Gets the default page for the folder
        /// </summary>		
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        internal static string GetDefaultPagePath(string languageCode, string websiteName, string folder)
        {

            if (string.IsNullOrEmpty(languageCode)) languageCode = AgilityContext.LanguageCode;

            AgilitySitemap siteMap = GetSitemap(languageCode, websiteName);
            if (siteMap == null)
            {
                throw new Exceptions.AgilityException(string.Format("The site map could not be found for this language: {0}", languageCode));
            }


            //attempt to find the page in the current sitemap
            XmlDocument sitemapXML = new XmlDocument();

            sitemapXML.LoadXml(siteMap.SitemapXml);


            XmlNode sitemapNode = sitemapXML.SelectSingleNode(string.Format("//ChannelNode[@channelID='{0}']", AgilityContext.CurrentChannel.ID));

            if (sitemapNode == null)
            {
                sitemapNode = sitemapXML.SelectSingleNode("SiteMap");
            }

            if (!string.IsNullOrEmpty(folder))
            {
                string xPath = string.Format("//SiteNode[@ID='{0}']", folder.Replace("/", "-").ToLowerInvariant());
                sitemapNode = sitemapNode.SelectSingleNode(xPath);
            }

            XmlNodeList nodes = sitemapNode.SelectNodes("SiteNode");
            string pagePath = null;

            foreach (XmlElement node in nodes)
            {
                if (node == null) continue;
                pagePath = node.GetAttribute("NavigateURL");

                if (!string.IsNullOrEmpty(pagePath))
                {

                    break;
                }
            }

            return pagePath;


        }

        /// <summary>
        /// Gets an Agility page based on a application relative url (application path represented by ~/);
        /// </summary>
        /// <param name="url"></param>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static AgilityContentServer.AgilityPage GetPage(string url, string languageCode, string websiteName)
        {
            if (url == "/" || url == "~/") url = string.Empty;

            if (string.IsNullOrEmpty(url))
            {
                url = GetDefaultPagePath(languageCode, websiteName, string.Empty);
            }

            if (string.IsNullOrEmpty(url)) return null;

            //Remove any query strings after .aspx			
            url = url.ToLowerInvariant();
            if (url.IndexOf("?") != -1) url = url.Substring(0, url.IndexOf("?"));

            string extension = Path.GetExtension(url);

            //if the path has an extension that is NOT .aspx, ignore it...
            if (!string.IsNullOrEmpty(extension) && !string.Equals(extension, ".aspx", StringComparison.CurrentCultureIgnoreCase)) return null;

            int indexOfASPX = url.IndexOf(".aspx", StringComparison.CurrentCultureIgnoreCase);
            if (indexOfASPX != -1)
            {
                //pull off the .aspx from the path...
                url = url.Substring(0, indexOfASPX);
            }

            if (url.StartsWith("~/")) url = url.Substring(1);
            if (!url.StartsWith("/")) url = string.Format("/{0}", url);
            if (url.EndsWith("/")) url = url.TrimEnd('/');

            //check if the page is already in memory...
            string memoryKey = string.Format("Agility.Web.BaseCache.GetPage.{0}.{1}.{2}", url, languageCode, AgilityContext.CurrentChannel.ID);
            if (AgilityContext.HttpContext != null)
            {
                AgilityContentServer.AgilityPage pageFromMemory = AgilityContext.HttpContext.Items[memoryKey] as AgilityContentServer.AgilityPage;
                if (pageFromMemory != null) return pageFromMemory;
            }

            AgilityPage page = null;

            //try to resolve the page in this language
            List<ResolvedPage> lstResolvedPages = AgilityRouteTable.ResolveRoutePath(url, languageCode);


            if ((lstResolvedPages == null || lstResolvedPages.Count == 0) && !AgilityContext.IsResponseEnded)
            {
                if (Current.Settings.Redirect404ToDefaultLanguage)
                {
                    #region *** get page in other language ***

                    /*
					 * if the page is not found in the current language, 
					 * check the default language in the domain config in an attempt find it in their sitemap.
					 */

                    if (AgilityContext.Domain != null)
                    {
                        foreach (var lang in AgilityContext.Domain.Languages.Where(l => l.LanguageCode != languageCode))
                        {

                            lstResolvedPages = AgilityRouteTable.ResolveRoutePath(url, lang.LanguageCode);

                            if (lstResolvedPages != null && lstResolvedPages.Count > 0)
                            {
                                languageCode = lang.LanguageCode;
                                break;
                            }
                        }

                    }
                    #endregion
                }

            }

            if (lstResolvedPages != null && lstResolvedPages.Count > 0)
            {
                //get the page object from the id
                ResolvedPage resolvedPage = lstResolvedPages[lstResolvedPages.Count - 1];
                page = resolvedPage.Page;
                DynamicPageFormulaItem dpItem = lstResolvedPages[lstResolvedPages.Count - 1].DynamicPageItem;

                ResolvedPage drPage = lstResolvedPages.AsQueryable().Reverse().FirstOrDefault(r => r.DynamicPageItem != null);

                AgilityContext.LastLoadedDynamicPageFormulaItem = drPage == null ? null : drPage.DynamicPageItem; ;

                //if we get a page back that's a dynamic page, and we aren't looking for one, then return null;
                if (dpItem != null)
                {
                    string scriptTop = page.CustomAnalyticsScript;
                    if (scriptTop == null) scriptTop = string.Empty;
                    string scriptBottom = string.Empty;
                    if (scriptTop.IndexOf(AgilityContext.GLOBAL_SCRIPT_SEPARATOR) != -1)
                    {
                        scriptBottom = scriptTop.Substring(scriptTop.IndexOf(AgilityContext.GLOBAL_SCRIPT_SEPARATOR) + AgilityContext.GLOBAL_SCRIPT_SEPARATOR.Length);
                        scriptTop = scriptTop.Substring(0, scriptTop.IndexOf(AgilityContext.GLOBAL_SCRIPT_SEPARATOR));
                    }
                    scriptTop = string.Format("{0}{1}", scriptTop, dpItem.TopScript);
                    scriptBottom = string.Format("{0}{1}", scriptBottom, dpItem.BottomScript);


                    DateTime pullDate = page.PullDate;

                    //copy the page
                    AgilityContentServer.AgilityPage dynPage = new AgilityPage()
                    {

                        //get the content 
                        Title = dpItem.Title,
                        Name = dpItem.Name,
                        MetaKeyWords = string.Format("{0}{1}", page.MetaKeyWords, dpItem.MetaKeyWords),
                        MetaTags = string.Format("{0}{1}", page.MetaTags, dpItem.MetaDescription),
                        MetaTagsRaw = string.Format("{0}{1}", page.MetaTagsRaw, dpItem.AdditionalHeaderCode),
                        CustomAnalyticsScript = string.Format("{0}{1}{2}", scriptTop, AgilityContext.GLOBAL_SCRIPT_SEPARATOR, scriptBottom),
                        ContentSections = new ContentSection[page.ContentSections.Length],
                        ExcludeFromOutputCache = page.ExcludeFromOutputCache,
                        HasSecondaryData = page.HasSecondaryData,
                        ID = page.ID,
                        IncludeInStatsTracking = page.IncludeInStatsTracking,
                        IsDeleted = page.IsDeleted,
                        IsPublished = page.IsPublished,
                        LanguageCode = page.LanguageCode,
                        LastAccessDate = page.LastAccessDate,
                        ParentPageID = page.ParentPageID,
                        PullDate = pullDate,
                        RedirectURL = page.RedirectURL,
                        ReleaseDate = page.ReleaseDate,
                        RequiresAuthentication = page.RequiresAuthentication,
                        State = page.State,
                        TemplatePath = page.TemplatePath,
                        XTemplateID = page.XTemplateID,
                        DynamicPageItem = dpItem

                    };

                    //if the resolved page had a redirect url, set that on the page copy...
                    if (!string.IsNullOrEmpty(resolvedPage.RedirectURL))
                    {
                        dynPage.RedirectURL = resolvedPage.RedirectURL;
                    }

                    page.ContentSections.CopyTo(dynPage.ContentSections, 0);

                    if (AgilityContext.HttpContext != null)
                    {
                        AgilityContext.HttpContext.Items[memoryKey] = dynPage;
                    }

                    return dynPage;

                }
            }


            if (AgilityContext.HttpContext != null && page != null)
            {
                AgilityContext.HttpContext.Items[memoryKey] = page;
            }

            return page;

        }

        internal static AgilityPage GetPageFromID(int pageItemContainerID, string languageCode, string websiteName, string currentUrl)
        {
            //page not found in ANY sitemap
            if (pageItemContainerID < 1) return null;

            //build the item key to access the page with
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.ItemType = typeof(AgilityContentServer.AgilityPage).Name;
            itemKey.LanguageCode = languageCode;
            itemKey.Key = pageItemContainerID;


            string cacheKey = GetCacheKey(itemKey);
            bool wasObjectInCache = false;

            if (AgilityContext.HttpContext != null)
            {
                wasObjectInCache = AgilityContext.HttpContext.Items[cacheKey] != null;
            }

            //get the page object itself
            AgilityContentServer.AgilityPage page = GetItem<AgilityContentServer.AgilityPage>(itemKey, websiteName);

            if (page == null)
            {
                return null;
            }
            else
            {


                //filter the page on it's release date (in the sitemap, only in Live mode)
                if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
                {

                    if (! string.IsNullOrEmpty(page.TemplatePath) || page.XTemplateID > 0)
                    {
                        page.RedirectURL = null;
                    }

                    DateTime dtViewingDate = DateTime.Now;
                    if (AgilityContext.IsPreview && AgilityContext.PreviewDateTime != DateTime.MinValue)
                    {
                        dtViewingDate = AgilityContext.PreviewDateTime;
                    }

                    if (page.ReleaseDate != DateTime.MinValue && page.ReleaseDate > dtViewingDate)
                    {
                        //if the page is not released, check for a schedule redirect...
                        if (!string.IsNullOrEmpty(page.ScheduledRedirectURL))
                        {
                            page.RedirectURL = page.ScheduledRedirectURL;
                            return page;
                        }

                        //don't return the page object until it is released
                        return null;
                    }

                    if (page.PullDate != DateTime.MinValue && page.PullDate <= dtViewingDate)
                    {

                        //if the page is pulled, check for a schedule redirect...
                        if (!string.IsNullOrEmpty(page.ScheduledRedirectURL))
                        {
                            page.RedirectURL = page.ScheduledRedirectURL;
                            return page;
                        }

                        //don't return the page object if it is pulled
                        return null;
                    }
                }
            }


            //if we are loaded this page in the browser in staging mode, preload the modules on it...
            //special case for pages... if this is a page, request all of the modules and content/linked content for this page
            if (!wasObjectInCache
                && AgilityContext.CurrentMode == Enum.Mode.Staging
                && AgilityContext.HttpContext != null
                && string.Equals(currentUrl, AgilityContext.HttpContext.Request.Path.Value, StringComparison.CurrentCultureIgnoreCase))
            {

                lock (_secondaryPageObjectLock)
                {
                    if (AgilityContext.HttpContext.Items[cacheKey] == null)
                    {
                        //only get the secondary object on THIS page...						
                        GetSecondaryObjectsForPage(itemKey, websiteName, page);
                    }
                }
            }



            return page;
        }

        public static void UpdateDynamicPageIndex(int pageItemContainerID, string contentReferenceName)
        {
            if (string.IsNullOrEmpty(contentReferenceName)) return;//throw new ArgumentException("Content Reference Name cannot be null or empty.", "contentReferenceName");

            contentReferenceName = contentReferenceName.ToLowerInvariant();

            string cacheKey = string.Format("{0}_{1}", DYNAMICPAGEINDEX_CACHEKEY, AgilityContext.CurrentMode);

            Dictionary<string, List<int>> dpIndex = GetDynamicPageIndex();

            List<int> lstPageIDs = null;

            if (!dpIndex.TryGetValue(contentReferenceName, out lstPageIDs)) lstPageIDs = new List<int>();

            if (!lstPageIDs.Contains(pageItemContainerID))
            {
                //update the index if the pageID isn't listed, or if it's changed...				
                _dynamicPageIndexLock.EnterWriteLock();

                try
                {

                    //update the index
                    lstPageIDs.Add(pageItemContainerID);
                    dpIndex[contentReferenceName] = lstPageIDs;
					
                    //build the file path and update the file...
                    string filepath = Path.Combine(
							Current.Settings.TransientCacheFilePath,
							AgilityContext.WebsiteName,
							AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
							$"{DYNAMICPAGEINDEX_FILENAME}.bin"
						).Replace("//", "/");

                    //write to a temp file first...
                    string tempfilepath = Path.Combine(
							Current.Settings.TransientCacheFilePath,
							AgilityContext.WebsiteName,
							"Temp",
							Guid.NewGuid().ToString().Substring(0, 6)
                        ).Replace("//", "/");


					//TO BE REMOVED
                    //if (AgilityContext.ContentAccessor != null)
                    //{
                    //    filepath = string.Format("{0}/{1}/{2}.bin",
                    //        Current.Settings.ContentCacheFilePath,
                    //        AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
                    //        DYNAMICPAGEINDEX_FILENAME
                    //        );

                    //    tempfilepath = string.Format("{0}/{1}/{2}.bin",
                    //        Current.Settings.ContentCacheFilePath,
                    //        "Temp",
                    //        Guid.NewGuid().ToString().Substring(0, 6)
                    //        );
                    //}


                    #region *** Write the File with Retries ***
                    int numCopyTries = 3;
                    int currentTry = 1;
                    while (currentTry <= numCopyTries)
                    {
                        try
                        {

                            //write the file to temp...
                            WriteFile(dpIndex, tempfilepath);

                            Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("Update Dyn Page Index: Temp file {0}, live file {1}", tempfilepath, filepath));

                            if (currentTry < numCopyTries)
                            {
                                if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                                }

                                File.Delete(filepath);
                                File.Move(tempfilepath, filepath);
                            }
                            else
                            {
                                //last resort, try a copy
                                File.Copy(tempfilepath, filepath, true);
                            }

                            //if we make it here, no need to retry
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (currentTry >= numCopyTries)
                            {
                                throw ex;
                            }
                            else
                            {
                                Agility.Web.Tracing.WebTrace.WriteWarningLine(string.Format("Error moving file {0} to {1}, retrying. {2}", tempfilepath, filepath, ex));
                            }

                        }

                        //if we get here, we need to retry after 2 seconds
                        Thread.Sleep(2000);
                        currentTry++;
                    }

                    #endregion

                    CacheDependency dep = new CacheDependency(filepath);


                    if (AgilityContext.HttpContext != null && dpIndex != null)
                    {

                        //put the thing in cache...	
						AgilityCache.Set(cacheKey, dpIndex, TimeSpan.FromDays(1), dep, CacheItemPriority.NeverRemove);
                    }

                }
                catch
                {
                    throw;
                }
                finally
                {
                    _dynamicPageIndexLock.ExitWriteLock();
                }

            }
        }

        internal static Dictionary<string, List<int>> GetDynamicPageIndex()
        {

            string cacheKey = string.Format("{0}_{1}", DYNAMICPAGEINDEX_CACHEKEY, AgilityContext.CurrentMode);

            Dictionary<string, List<int>> dpIndex = null;
            if (AgilityContext.HttpContext != null)
            {

                dpIndex = AgilityCache.Get(cacheKey) as Dictionary<string, List<int>>;
                if (dpIndex != null) return dpIndex;

            }

            string filepath = string.Format("{0}/{1}/{2}/{3}.bin",
                Current.Settings.TransientCacheFilePath,
                AgilityContext.WebsiteName,
                AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
                DYNAMICPAGEINDEX_FILENAME
                );

            if (AgilityContext.ContentAccessor != null)
            {
                filepath = string.Format("{0}/{1}/{2}.bin",
                    Current.Settings.TransientCacheFilePath,
                    AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
                    DYNAMICPAGEINDEX_FILENAME
                    );
            }


            CacheDependency dep = null;



			//wait for any writes to happen...
			_dynamicPageIndexLock.EnterReadLock();

            try
            {

                //check for cache one more time...
                if (AgilityContext.HttpContext != null)
                {
					dpIndex =  AgilityCache.Get(cacheKey) as Dictionary<string, List<int>>;
					dpIndex = AgilityCache.Get(cacheKey) as Dictionary<string, List<int>>;
                    if (dpIndex != null)
                    {
                        return dpIndex;
                    }

                }


                if (File.Exists(filepath))
                {

                    //get the object from the file system (since this file is specfic to this machine, we don't need to keep it in blob)
                    dpIndex = ReadFile< Dictionary<string, List<int>>>(filepath, cacheKey);
                    dep = new CacheDependency(filepath);
                }

                if (AgilityContext.HttpContext != null && dpIndex != null)
                {

                    //put the thing in cache...	
					AgilityCache.Set(cacheKey, dpIndex, TimeSpan.FromDays(1), dep, CacheItemPriority.NeverRemove);
                }

                if (dpIndex == null) dpIndex = new Dictionary<string, List<int>>();

                return dpIndex;
            }
            catch
            {
                throw;
            }
            finally
            {
				
				_dynamicPageIndexLock.ExitReadLock();
            }
        }

        public static void ClearDynamicDynamicPageFormulaIndex(AgilityPage page)
        {
            //update the Dynamic Page Formula Index that use this page...
            Dictionary<string, List<int>> dpIndex = BaseCache.GetDynamicPageIndex();
            foreach (string contentReferenceName in dpIndex.Keys)
            {
                List<int> pageIDs = dpIndex[contentReferenceName];
				if (pageIDs.Contains(page.ID))
				{
					//build the file path and update the file...
					string filepath = Path.Combine(
							Current.Settings.TransientCacheFilePath,
							AgilityContext.WebsiteName,
							AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
							$"{DYNAMICPAGEFORMULAINDEX_FILENAME}_{page.ID}_{contentReferenceName}_{page.LanguageCode}.bin"
						);

					//TO BE REMOVED
                    //if (AgilityContext.ContentAccessor != null)
                    //{
                    //    filepath = string.Format("{0}/{1}/{2}_{3}_{4}_{5}.bin",
                    //        Current.Settings.ContentCacheFilePath,
                    //        AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
                    //        DYNAMICPAGEFORMULAINDEX_FILENAME,
                    //        page.ID,
                    //        contentReferenceName,
                    //        page.LanguageCode
                    //        );
                    //}

                    string cacheKey = string.Format("{0}_{1}_{2}_{3}_{4}",
                        DYNAMICPAGEFORMULAINDEX_CACHEKEY,
                        AgilityContext.CurrentMode,
                        page.LanguageCode,
                        page.ID,
                        contentReferenceName);


                    //update the index	
                    ReaderWriterLockSlim _dynamicPageFormulaIndexLock = GetDynamicPageFormulaLock(page.ID);
                    _dynamicPageFormulaIndexLock.EnterWriteLock();

                    try
                    {

                        if (AgilityContext.ContentAccessor != null && AgilityContext.CurrentMode != Enum.Mode.Staging)
                        {
                            //use the content accessor if possible
                            AgilityContext.ContentAccessor.DeleteContentCacheBlob(cacheKey);
                        }

                        File.Delete(filepath);

                    }
                    catch (Exception)
                    {
                        throw new ApplicationException(string.Format("Could not delete dynamic page formula index: {0}", filepath));
                    }
                    finally
                    {
                        _dynamicPageFormulaIndexLock.ExitWriteLock();
                    }
                }
            }
        }

        internal static void UpdateDynamicPageFormulaIndex(AgilityPage page, AgilityContent existingContent, AgilityContent deltaContent, string contentReferenceName, bool startFromNull)
        {


            string cacheKey = string.Format("{0}_{1}_{2}_{3}_{4}",
                DYNAMICPAGEFORMULAINDEX_CACHEKEY,
                AgilityContext.CurrentMode,
                page.LanguageCode,
                page.ID,
                contentReferenceName);


            DynamicPageFormulaItemIndex dpIndex = new DynamicPageFormulaItemIndex();
            if (!startFromNull)
            {
                dpIndex = GetDynamicPageFormulaIndex(page.ID, contentReferenceName, page.LanguageCode, page, false);
            }
            dpIndex.PageVersionID = page.ZVersionID;

            //update the index	
            ReaderWriterLockSlim _dynamicPageFormulaIndexLock = GetDynamicPageFormulaLock(page.ID);
            _dynamicPageFormulaIndexLock.EnterWriteLock();


            try
            {

                //create a NEW index based on the values already in the values list...
                Dictionary<int, DynamicPageFormulaItem> idIndex = new Dictionary<int, DynamicPageFormulaItem>(dpIndex.Count);
                foreach (DynamicPageFormulaItem item in dpIndex.Values)
                {
                    idIndex[item.ContentID] = item;
                }

                string sort = page.DynamicPageContentViewSort;
                string filter = page.DynamicPageContentViewFilter;

                if (!string.Equals(page.DynamicPageContentViewSort, dpIndex.DynamicPageContentViewSort)
                    || !string.Equals(page.DynamicPageContentViewFilter, dpIndex.DynamicPageContentViewFilter)
                    || !string.Equals(page.DynamicPageMenuText, dpIndex.DynamicPageMenuText)
                    || !string.Equals(page.DynamicPageName, dpIndex.DynamicPageName)
                    || !string.Equals(page.DynamicPageTitle, dpIndex.DynamicPageTitle)
                    )
                {
                    dpIndex.Clear();

                    dpIndex.DynamicPageContentViewSort = page.DynamicPageContentViewSort;
                    dpIndex.DynamicPageContentViewFilter = page.DynamicPageContentViewFilter;
                    dpIndex.DynamicPageMenuText = page.DynamicPageMenuText;
                    dpIndex.DynamicPageName = page.DynamicPageName;
                    dpIndex.DynamicPageTitle = page.DynamicPageTitle;

                }


                if (dpIndex.Count == 0
                    && startFromNull == false
                    && existingContent != null
                    && existingContent.DataSet != null
                    && existingContent.DataSet.Tables["ContentItems"] != null)
                {
                    //if we don't have an index at all yet, start from the existing items...
                    DataTable dt = existingContent.DataSet.Tables["ContentItems"];
                    DataView dv = dt.DefaultView;


                    if (!string.IsNullOrEmpty(filter) || !string.IsNullOrEmpty(sort))
                    {
                        dv = new DataView(dt, filter, sort, DataViewRowState.CurrentRows);
                    }

                    foreach (DataRowView drv in dv)
                    {
                       
						int contentID = -1;
						if (!int.TryParse($"{drv["ContentID"]}", out contentID)) contentID = -1;


						//add the new item...
						DynamicPageFormulaItem deltaItem = new DynamicPageFormulaItem(page, contentReferenceName, existingContent.LanguageCode, drv.Row);
                        string key = string.Format("/{0}", deltaItem.Name).ToLowerInvariant();
                        dpIndex[key] = deltaItem;
                        idIndex[contentID] = deltaItem;
                    }

                }


                if (deltaContent != null)
                {


                    if (deltaContent.DataSet.Tables["ContentItems"] != null && deltaContent.DataSet.Tables["ContentItems"].Rows.Count > 0)
                    {
                        //merge the rows from the datatable into the dictionary...
                        DataTable dt = deltaContent.DataSet.Tables["ContentItems"];
                        DataView dv = dt.DefaultView;


                        if (!string.IsNullOrEmpty(filter) || !string.IsNullOrEmpty(sort))
                        {
                            dv = new DataView(dt, filter, sort, DataViewRowState.CurrentRows);
                        }
                        string key = null;
                        foreach (DataRowView drv in dv)
                        {
							int contentID = -1;
							if (!int.TryParse($"{drv["ContentID"]}", out contentID)) contentID = -1;


							DateTime lastModified = DateTime.MinValue;
							object o = drv["CreatedDate"];
							if (o is DateTime)
							{
								lastModified = (DateTime)o;
							} else
							{
								DateTime.TryParse($"{o}", out lastModified);
							}

							

                            //check for this content in the current dpIndex based on a lookup to the Values based on ContentID
                            DynamicPageFormulaItem existingItem = null;
                            if (idIndex.TryGetValue(contentID, out existingItem))
                            {
                                //remove the existing item
                                key = string.Format("/{0}", existingItem.Name).ToLowerInvariant();
                                dpIndex.Remove(key);
                            }

                            //add the new item...
                            DynamicPageFormulaItem deltaItem = new DynamicPageFormulaItem(page, contentReferenceName, deltaContent.LanguageCode, drv.Row);
                            key = string.Format("/{0}", deltaItem.Name).ToLowerInvariant();
                            dpIndex[key] = deltaItem;
                            idIndex[contentID] = deltaItem;
                        }
                    }

                    //clean out the deletions...

                    if (deltaContent.DataSet.Tables.Contains("Deletions"))
                    {
						var deletionsTable = deltaContent.DataSet.Tables["Deletions"];

						foreach (DataRow drow in deletionsTable.Rows)
                        {
							int contentID = -1;
							if (!int.TryParse($"{drow["ContentID"]}", out contentID)) contentID = -1;
							
                            DynamicPageFormulaItem existingItem = null;
                            if (idIndex.TryGetValue(contentID, out existingItem))
                            {
                                //remove the existing item
                                string key = string.Format("/{0}", existingItem.Name).ToLowerInvariant();
                                dpIndex.Remove(key);
                            }
                        }
                    }
                }



                //build the file path and update the file...
                string filepath = Path.Combine(
						Current.Settings.TransientCacheFilePath,
						AgilityContext.WebsiteName,
						AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
						$"{DYNAMICPAGEFORMULAINDEX_FILENAME}_{page.ID}_{contentReferenceName}_{page.LanguageCode}.bin"
					);

                string tempfilepath = Path.Combine(
						Current.Settings.TransientCacheFilePath,
						AgilityContext.WebsiteName,
						"Temp",
						$"{DYNAMICPAGEFORMULAINDEX_FILENAME}_{page.ID}_{Guid.NewGuid().ToString().Substring(0, 8)}_{page.LanguageCode}.bin"
					);


				//TO BE REMOVED
                //if (AgilityContext.ContentAccessor != null)
                //{
                //    filepath = string.Format("{0}/{1}/{2}_{3}_{4}_{5}.bin",
                //    Current.Settings.ContentCacheFilePath,
                //    AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
                //    DYNAMICPAGEFORMULAINDEX_FILENAME,
                //    page.ID,
                //    contentReferenceName,
                //    page.LanguageCode
                //    );

                //    tempfilepath = string.Format("{0}/{1}/{2}_{3}_{4}_{5}.bin",
                //    Current.Settings.ContentCacheFilePath,
                //    "Temp",
                //    DYNAMICPAGEFORMULAINDEX_FILENAME,
                //    page.ID,
                //    Guid.NewGuid().ToString().Substring(0, 8),
                //    page.LanguageCode
                //    );
                //}



                //write the object to the temp file
                string tempDir = Path.GetDirectoryName(tempfilepath);
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("UpdateDynamicPageFormulaIndex: Temp file {0}, live file {1}", tempfilepath, filepath));
                WriteFile(dpIndex, tempfilepath);

                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                    }

                    File.Delete(filepath);
                    File.Move(tempfilepath, filepath);
                }
                catch
                {
                    File.Copy(tempfilepath, filepath, true);
                    File.Delete(tempfilepath);
                }



                CacheDependency dep = new CacheDependency(filepath);


                if (AgilityContext.HttpContext != null && dpIndex != null)
                {

                    //put the thing in cache...		
					AgilityCache.Set(cacheKey, dpIndex, TimeSpan.FromDays(1),  dep, CacheItemPriority.NeverRemove);
                }

            }
            catch
            {
                throw;
            }
            finally
            {
                _dynamicPageFormulaIndexLock.ExitWriteLock();
            }

        }

        internal static DynamicPageFormulaItemIndex GetDynamicPageFormulaIndex(int pageItemContainerID, string referenceName, string languageCode, AgilityPage page, bool updateIfNeeded)
        {
            if (string.IsNullOrEmpty(referenceName)) throw new ArgumentException("ReferenceName cannot be null", referenceName);


            string cacheKey = string.Format("{0}_{1}_{2}_{3}_{4}",
                DYNAMICPAGEFORMULAINDEX_CACHEKEY,
                AgilityContext.CurrentMode,
                languageCode,
                pageItemContainerID,
                referenceName);

            DynamicPageFormulaItemIndex dpIndex = null;
            if (AgilityContext.HttpContext != null)
            {
                //try to get from cache first...
                dpIndex = AgilityCache.Get<DynamicPageFormulaItemIndex>(cacheKey);
                if (dpIndex != null) return dpIndex;

            }

            //build the file path and update the file...
            string filepath = Path.Combine(
					Current.Settings.TransientCacheFilePath,
					AgilityContext.WebsiteName,
					AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
					$"{DYNAMICPAGEFORMULAINDEX_FILENAME}_{page.ID}_{referenceName}_{page.LanguageCode}.bin"
				);

            string tempfilepath = Path.Combine(
					Current.Settings.TransientCacheFilePath,
					AgilityContext.WebsiteName,
					"Temp",
					$"{DYNAMICPAGEFORMULAINDEX_FILENAME}_{page.ID}_{referenceName}_{page.LanguageCode}.bin"
				);


			//TO BE REMOVED
            //if (AgilityContext.ContentAccessor != null)
            //{
            //    filepath = string.Format("{0}/{1}/{2}_{3}_{4}_{5}.bin",
            //    Current.Settings.ContentCacheFilePath,
            //    AgilityContext.CurrentMode == Enum.Mode.Live ? "Live" : "Staging",
            //    DYNAMICPAGEFORMULAINDEX_FILENAME,
            //    page.ID,
            //    referenceName,
            //    page.LanguageCode
            //    );

            //    tempfilepath = string.Format("{0}/{1}/{2}_{3}_{4}_{5}.bin",
            //    Current.Settings.ContentCacheFilePath,
            //    "Temp",
            //    DYNAMICPAGEFORMULAINDEX_FILENAME,
            //    page.ID,
            //    Guid.NewGuid().ToString().Substring(0, 8),
            //    page.LanguageCode
            //    );
            //}


            CacheDependency dep = null;

			

			//wait for any writes to happen...
			ReaderWriterLockSlim _dynamicPageFormulaIndexLock = GetDynamicPageFormulaLock(page.ID);

            _dynamicPageFormulaIndexLock.EnterUpgradeableReadLock();



            try
            {

                //check for cache again
                if (AgilityContext.HttpContext != null)
                {
                    dpIndex = AgilityCache.Get< DynamicPageFormulaItemIndex>(cacheKey);
                    if (dpIndex != null)
                    {
                        return dpIndex;
                    }

                }


                if (File.Exists(filepath))
                {

                    //get the object from the file system
                    try
                    {
                        dpIndex = ReadFile<DynamicPageFormulaItemIndex>(filepath, cacheKey);

                        if (dpIndex != null && dpIndex.Count > 0 && string.IsNullOrEmpty(dpIndex.First().Value.LanguageCode))
                        {
                            //rebuild the index if it doesn't have the language code..
                            dpIndex = null;
                        }

                        dep = new CacheDependency(filepath);
                    }
                    catch
                    {
                        //ignore read errors here...
                        dpIndex = null;
                    }

                }

                if (updateIfNeeded && dpIndex != null && (Current.Settings.DevelopmentMode || AgilityContext.CurrentMode == Enum.Mode.Staging))
                {
                    //if we are staging mode, compare the Page version to the Index version

                    //get the page if we need to.
                    if (page == null)
                    {
                        page = GetPageFromID(pageItemContainerID, languageCode, AgilityContext.WebsiteName, null);
                    }

                    if (page != null && page.ZVersionID > dpIndex.PageVersionID)
                    {
                        dpIndex = null;
                    }
                }

                if (updateIfNeeded && dpIndex == null)
                {
                    //if we haven't yet built the index for this content view... build it now....
                    _dynamicPageFormulaIndexLock.EnterWriteLock();

                    //check for cache one more time...
                    if (AgilityContext.HttpContext != null)
                    {
                        dpIndex = AgilityCache.Get< DynamicPageFormulaItemIndex>(cacheKey);
                        if (dpIndex != null)
                        {
                            return dpIndex;
                        }

                    }


                    try
                    {
                        //get the datatable if we don't already have it...				
                        DataTable dt = null;
                        AgilityContent content = null;

                        try
                        {
                            content = GetContent(referenceName, languageCode, AgilityContext.WebsiteName);
                        }
                        catch (Exception ex)
                        {
                            Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
                        }


                        if (content != null && content.DataSet != null) dt = content.DataSet.Tables["ContentItems"];


                        if (dt != null && dt.Rows.Count > 0)
                        {

                            //get the page if we need to.
                            if (page == null)
                            {
                                page = GetPageFromID(pageItemContainerID, languageCode, AgilityContext.WebsiteName, null);
                            }

                            //merge the rows from the datatable into the dictionary...
                            DataView dv = dt.DefaultView;

                            string sort = page.DynamicPageContentViewSort;
                            string filter = page.DynamicPageContentViewFilter;

                            if (!string.IsNullOrEmpty(filter) || !string.IsNullOrEmpty(sort))
                            {
                                dv = new DataView(dt, filter, sort, DataViewRowState.CurrentRows);
                            }

                            if (dpIndex == null) dpIndex = new DynamicPageFormulaItemIndex(dv.Count);
                            dpIndex.PageVersionID = page.ZVersionID;

                            //create a NEW index based on the values already in the values list...
                            Dictionary<int, DynamicPageFormulaItem> idIndex = new Dictionary<int, DynamicPageFormulaItem>(dpIndex.Count);
                            foreach (DynamicPageFormulaItem item in dpIndex.Values)
                            {
                                idIndex[item.ContentID] = item;
                            }

                            foreach (DataRowView drv in dv)
                            {
								int contentID = -1;
								if (!int.TryParse($"{drv["ContentID"]}", out contentID)) contentID = -1;

								DateTime lastModified = DateTime.MinValue;
								object o = drv["CreatedDate"];
								if (o is DateTime)
								{
									lastModified = (DateTime)o;
								}
								else
								{
									DateTime.TryParse($"{o}", out lastModified);
								}

								//check for this content in the current dpIndex based on a lookup to the Values based on ContentID
								DynamicPageFormulaItem existingItem = null;
                                if (idIndex.TryGetValue(contentID, out existingItem))
                                {
                                    //remove the existing item
                                    dpIndex.Remove(existingItem.Name);
                                }

                                //add the new item...
                                DynamicPageFormulaItem deltaItem = new DynamicPageFormulaItem(page, referenceName, content.LanguageCode, drv.Row);
                                string key = string.Format("/{0}", deltaItem.Name).ToLowerInvariant();
                                dpIndex[key] = deltaItem;
                                idIndex[contentID] = deltaItem;
                            }
                        }



                        //write the object to the temp file
                        string tempDir = Path.GetDirectoryName(tempfilepath);
                        if (!Directory.Exists(tempDir))
                        {
                            Directory.CreateDirectory(tempDir);
                        }

                        Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("GetDynamicPageFormulaIndex: Temp file {0}, live file {1}", tempfilepath, filepath));
                        WriteFile(dpIndex, tempfilepath);

                        try
                        {
                            File.Delete(filepath);
                            File.Move(tempfilepath, filepath);
                        }
                        catch (Exception ex)
                        {
                            Agility.Web.Tracing.WebTrace.WriteException(ex, string.Format("Error swapping file {0} with {1}", filepath, tempfilepath));
                            File.Copy(tempfilepath, filepath, true);
                            File.Delete(tempfilepath);
                        }

                        dep = new CacheDependency(filepath);


                    }
                    catch
                    {

                        throw;
                    }
                    finally
                    {
                        _dynamicPageFormulaIndexLock.ExitWriteLock();

						
						
					}

                }

                if (dpIndex != null)
                {

                    if (AgilityContext.HttpContext != null)
                    {

                        //put the thing in cache...		
						AgilityCache.Set(cacheKey, dpIndex, TimeSpan.FromDays(1), dep, CacheItemPriority.NeverRemove);
                    }
                }

                if (dpIndex == null) dpIndex = new DynamicPageFormulaItemIndex();
                if (page != null)
                {
                    dpIndex.PageVersionID = page.ZVersionID;
                }

                return dpIndex;
            }
            catch
            {
                throw;
            }
            finally
            {
                _dynamicPageFormulaIndexLock.ExitUpgradeableReadLock();
            }
        }

        internal static string GetPagePath(int id, string languageCode, string websiteName)
        {
            //Remove any query strings after .aspx

            //attempt the get the pagecontentID from the sitemap for the current language	
            AgilitySitemap siteMap = GetSitemap(languageCode, websiteName);
            if (siteMap == null)
            {
                throw new Exceptions.AgilityException(string.Format("The site map could not be found for this language: {0}", languageCode));
            }

            string xpath = string.Format("//SiteNode[@picID='{0}']", id);

            //attempt to find the page in the current sitemap
            XmlDocument sitemapXML = new XmlDocument();

            sitemapXML.LoadXml(siteMap.SitemapXml);
            XmlNodeList nodes = sitemapXML.SelectNodes(xpath);
            XmlElement pageNode = null;
            if (nodes.Count == 1)
            {
                pageNode = nodes[0] as XmlElement;
            }
            else
            {
                foreach (XmlElement node in nodes)
                {
                    pageNode = node;

                    if (string.IsNullOrEmpty(node.GetAttribute("PagePath")))
                    {
                        //always prefer the Non Redirect pages.
                        break;
                    }
                }
            }

            if (pageNode != null)
            {
                string s = pageNode.GetAttribute("NavigateURL");
                if (string.IsNullOrEmpty(s)) s = pageNode.GetAttribute("PagePath");
                return s;
            }

            return null;
        }


        /// <summary>
        /// Gets the Module based on a module ID.
        /// </summary>
        /// <param name="moduleID"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        public static AgilityModule GetModule(int moduleID, string websiteName)
        {
            //get the ItemKey key for the document
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = moduleID;
            itemKey.LanguageCode = AgilityContext.LanguageCode;
            itemKey.ItemType = typeof(AgilityModule).Name;

            return GetItem<AgilityModule>(itemKey, websiteName);

        }


        internal static AgilityPageDefinition GetPageDefinition(int pageDefinitionID, string websiteName)
        {
            //get the ItemKey key for the document
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = pageDefinitionID;
            itemKey.LanguageCode = AgilityContext.LanguageCode;
            itemKey.ItemType = typeof(AgilityPageDefinition).Name;

            return GetItem<AgilityPageDefinition>(itemKey, websiteName);

        }

        /// <summary>
        /// Gets the file information for an agility document based on the RELATIVE filepath.
        /// </summary>
        /// <remarks>
        /// The Url for a "document" request looks like "~/ecms.aspx/filepath" OR ~/ecms.ashx/filepath
        /// </remarks>
        /// <param name="filepath"></param>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static FileInfo GetDocument(string filepath, string languageCode, string websiteName)
        {
            //get the ItemKey key for the document
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.Key = filepath;
            itemKey.LanguageCode = languageCode;
            itemKey.ItemType = typeof(AgilityDocument).Name;

            string fullFilePath = GetFilePathForItemKey(itemKey, websiteName, transientPath: true);


            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
            {
                //live mode				

                //just check if the file exists				
                if (File.Exists(fullFilePath))
                {
                    //if the file is there, return the file info
                    return new FileInfo(fullFilePath);
                }
            }
            else
            {

                //staging mode

                //get the document object itself (this will download it in staging mode, if neccessary, and check for file existance)
                AgilityDocument document = GetItem<AgilityDocument>(itemKey, websiteName);

                if (document != null && File.Exists(fullFilePath))
                {
                    //if the file is there, return the file info
                    return new FileInfo(fullFilePath);
                }
            }

            //return a null reference if we can't get the file.
            return null;
        }


        /// <summary>
        /// Get an attachment in the current mode from the provded parameters.
        /// The Url for an "attachment" request looks like "~/ecms.aspx/guid/filename.ext" OR "~/ecms.ashx/guid/filename.ext"
        /// </summary>
        /// <remarks>
        /// The attachment is location solely based on the guid, and the filename is only used for the server and browser to identity the mime type.
        /// </remarks>
        /// <param name="guid"></param>
        /// <param name="filename"></param>
        /// <param name="languageCode"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        internal static FileInfo GetAttachment(string guid, string filename, string languageCode, string websiteName)
        {

            //get the item key for the attachment
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.ItemType = typeof(Objects.Attachment).Name;
            itemKey.Key = guid + filename;

            //resolve the attachment filename
            string attachmentFilePath = BaseCache.GetFilePathForItemKey(itemKey, websiteName, transientPath: true);

            //get the DIRECTORY that the attachment is in...
            string directory = Path.GetDirectoryName(attachmentFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            //search for the file by the guid
            string[] filePaths = Directory.GetFiles(directory, guid + "*", SearchOption.TopDirectoryOnly);

            if (filePaths.Length > 0)
            {
                return new FileInfo(filePaths[0]);
            }

			//if we get this far, and are in staging mode, download the attachment
			if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging)
			{
				AgilityWebsiteAuthorization auth = GetAgilityWebsiteAuthorization();

				AgilityContentServerClient client = BaseCache.GetAgilityServerClient();


				byte[] bytes = client.SelectAttachmentDataAsync(auth, guid).Result.SelectAttachmentDataResult;

				//stream the file into the correct location
				using (MemoryStream attStream = new MemoryStream(bytes))
				{
					BaseCache.WriteFile(attStream, attachmentFilePath, DateTime.MinValue);

					if (File.Exists(attachmentFilePath))
					{
						//return the new filepath
						return new FileInfo(attachmentFilePath);
					}
				} 
            }
            

            //if we get this far, the attachment does not exist
            return null;

        }

        public static AgilityWebsiteAuthorization GetAgilityWebsiteAuthorization()
        {
            AgilityWebsiteAuthorization auth = new AgilityWebsiteAuthorization();
            auth.SecurityKey = Current.Settings.SecurityKey;
            auth.WebsiteName = AgilityContext.WebsiteName;

            if (AgilityContext.HttpContext != null && AgilityContext.HttpContext.Request != null)
            {
				auth.IPAddress = AgilityContext.HttpContext.Connection.RemoteIpAddress.ToString();
                try
                {
                    auth.Referrer = string.Format("{0}", AgilityContext.HttpContext.Request.Headers["Referer"]);
                }
                catch { }
                auth.Url = string.Format("{0}", AgilityContext.HttpContext.Request.GetDisplayUrl());

                if (AgilityContext.HttpContext.User != null && AgilityContext.HttpContext.User.Identity != null)
                {
                    auth.Username = AgilityContext.HttpContext.User.Identity.Name;
                }
            }
            else
            {
                auth.Url = AgilityHttpModule.BaseURL;
            }


            return auth;
        }



        /// <summary>
        /// Merges a delta content object with an existing content object.
        /// </summary>
        /// <param name="existingContent"></param>
        /// <param name="deltaContent"></param>
        /// <returns></returns>
        public static AgilityContent MergeContent(AgilityContent existingContent, AgilityContent deltaContent)
        {

			bool debugSync = Current.Settings.DebugSync;

            //set the serialization formats here...
            if (existingContent != null)
            {
				if (existingContent.DataSet == null) existingContent.DataSet = new DataSet();
				//existingContent.DataSet.RemotingFormat = SerializationFormat.Binary;
                
            }

            if (deltaContent.DataSet == null) deltaContent.DataSet = new DataSet();
            //deltaContent.DataSet.RemotingFormat = SerializationFormat.Binary;

            if (debugSync) WebTrace.WriteWarningLine("MergeContent: Pre GetDynamicPageIndex");


            //if this is a Dynamic Page Content list, resolve the formulas...			
            Dictionary<string, List<int>> dpIndex = GetDynamicPageIndex();


            if (debugSync) WebTrace.WriteWarningLine("MergeContent: Post GetDynamicPageIndex");

            List<int> lstPageIDs = null;
            if (dpIndex.TryGetValue(deltaContent.ReferenceName.ToLowerInvariant(), out lstPageIDs))
            {
                foreach (int pageID in lstPageIDs)
                {

                    AgilityPage page = GetPageFromID(pageID, deltaContent.LanguageCode, AgilityContext.WebsiteName, null);
                    if (page != null
                        && deltaContent.DataSet != null)
                    {
                        if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Pre UpdateDynamicPageFormulaIndex: pageID: {0}, refname:{1}", pageID, deltaContent.ReferenceName));

                        //update all of the DynamicPageIndexes that this content appears on...
                        BaseCache.UpdateDynamicPageFormulaIndex(page, existingContent, deltaContent, deltaContent.ReferenceName, deltaContent.IsCompleteSet);

                        if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Post UpdateDynamicPageFormulaIndex: pageID: {0}, refname:{1}", pageID, deltaContent.ReferenceName));
                    }
                }
            }


            if (deltaContent.IsCompleteSet || existingContent == null)
            {
                if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Pre AcceptChanges: refname:{0}", deltaContent.ReferenceName));

                deltaContent.DataSet.AcceptChanges();
                return deltaContent;
            }

            DataSet dsExisting = existingContent.DataSet;
            DataSet dsDelta = deltaContent.DataSet;

            if (deltaContent.IsTimedReleaseEnabled)
            {
                existingContent.IsTimedReleaseEnabled = true;
            }


            //merge the new and updated items
            if (dsDelta.Tables["ContentItems"].Rows.Count > 0)
            {
                if (debugSync) WebTrace.WriteWarningLine(string.Format("Merging {0} new/updated items for Content: {1}",
                    dsDelta.Tables["ContentItems"].Rows.Count,
                    deltaContent.ReferenceName));
            }

            if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Getting list of uniques in delta: refname:{0}", deltaContent.ReferenceName));


            List<int> uniqueContentIDs = new List<int>(10);
            //get a list of all the unique content ids in here
            foreach (DataRow row in dsDelta.Tables["ContentItems"].Rows)
            {
				int contentID = -1;
				if (!int.TryParse($"{row["ContentID"]}", out contentID)) contentID = -1;


				if (!uniqueContentIDs.Contains(contentID)) uniqueContentIDs.Add(contentID);
            }

            if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Removing duplicates: refname:{0}", deltaContent.ReferenceName));


            //remove any duplicate content and tags that is newly added
            foreach (int contentID in uniqueContentIDs)
            {
                DataRow[] currentRows = dsExisting.Tables["ContentItems"].Select(string.Format("contentID={0}", contentID), string.Empty, DataViewRowState.CurrentRows);
                foreach (DataRow currentRow in currentRows)
                {
                    currentRow.Delete();
                }

                ////duplicate tags
                //DataRow[] currentTagRows = dsExisting.Tables["Tags"].Select(string.Format("contentID={0}", contentID), string.Empty, DataViewRowState.CurrentRows);
                //foreach (ContentDataSet.TagsRow currentRow in currentTagRows)
                //{
                //    currentRow.Delete();
                //}
            }


            //re-import the new content
            if (debugSync) WebTrace.WriteWarningLine(string.Format("Re-importing delta items for Content View: {0}",
                    deltaContent.ReferenceName));


            List<int> importedVersionIDs = new List<int>();
            foreach (DataRow row in dsDelta.Tables["ContentItems"].Rows)
            {
                //add the new/updated row
                dsExisting.Tables["ContentItems"].ImportRow(row);

				int versionID = -1;
				if (!int.TryParse($"{row["VersionID"]}", out versionID)) versionID = -1;

				importedVersionIDs.Add(versionID);
            }



            if (dsDelta.Tables.Contains("Deletions"))
            {
				var dtDeletions = dsDelta.Tables["Deletions"];

				//remove the deleted ones.
				WebTrace.WriteVerboseLine(string.Format("Merging {0} deleted items for Content View: {1}",
					dtDeletions.Rows.Count,
                    deltaContent.ReferenceName));

                foreach (DataRow row in dtDeletions.Rows)
                {
					int contentID = -1;
					if (!int.TryParse($"{row["ContentID"]}", out contentID)) contentID = -1;
					

                    string filter = string.Format("contentID={0}", contentID);
                    DataRow[] delRows = dsExisting.Tables["ContentItems"].Select(filter, string.Empty, DataViewRowState.CurrentRows);
                    foreach (DataRow delRow in delRows)
                    {
                        delRow.Delete();
                    }
                }
            }


            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
            {
                //remove any published items that have been "pulled"

                if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Removing pulled items: refname:{0}", deltaContent.ReferenceName));


                string filter = string.Format("pullDate<#{0:MM/dd/yyyy HH:mm}#", DateTime.Now);
                DataRow[] delRows = dsExisting.Tables["ContentItems"].Select(filter, string.Empty, DataViewRowState.CurrentRows);
                foreach (DataRow delRow in delRows)
                {
                    delRow.Delete();
                }
            }


            //add/replace in any new Attachments
            if (dsDelta.Tables.Contains("Attachments"))
            {
				var dtAttachments = dsDelta.Tables["Attachments"];
				var dtExistingAttachments = dsExisting.Tables["Attachments"];

                if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Handling attachments 1: refname:{0}", deltaContent.ReferenceName));

                //remove duplicate attachment rows
                foreach (DataRow attRow in dtAttachments.Rows)
                {
					var guid = $"{attRow["GUID"]}";

					int versionID = -1;
					if (!int.TryParse($"{attRow["VersionID"]}", out versionID)) versionID = -1;
					

					DataRow[] attDelRows = dtExistingAttachments.Select(string.Format("GUID = '{0}' AND versionID = {1}", guid, versionID), string.Empty, DataViewRowState.CurrentRows);
                    foreach (DataRow attDelRow in attDelRows)
                    {
                        //remove any duplicate attachment row(s)
                        attDelRow.Delete();
                    }

					//add the new attachment row(s)
					dtExistingAttachments.ImportRow(attRow);

                }

                if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Handling attachments 2: refname:{0}", deltaContent.ReferenceName));

                //dump any attachments for items that were just synced...
                foreach (int versionID in importedVersionIDs)
                {
                    DataRow[] attDelRows = dtAttachments.Select(string.Format("VersionID = {0}", versionID), string.Empty, DataViewRowState.CurrentRows);
                    foreach (DataRow attDelRow in attDelRows)
                    {
                        attDelRow.Delete();
                    }
                }

            }

            //add/replace in any new AttachmentThumbnails
            if (dsDelta.Tables.Contains("AttachmentThumbnails"))
            {
				var dtAttachmentThumbnails = dsDelta.Tables["AttachmentThumbnails"];
				var dtExistingThumbnails = dsExisting.Tables["AttachmentThumbnails"];


				if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Handling thumbnails: refname:{0}", deltaContent.ReferenceName));

                foreach (DataRow attRow in dtAttachmentThumbnails.Rows)
                {
					var managerID = $"{attRow["ManagerID"]}";
					var thumbnailName = $"{attRow["ThumbnailName"]}";

					int versionID = -1;
					if (!int.TryParse($"{attRow["VersionID"]}", out  versionID)) versionID = -1;
					

					DataRow[] attDelRows = dtExistingThumbnails.Select(string.Format("ManagerID = '{0}' AND versionID = {1} AND ThumbnailName = '{2}'", managerID, versionID, thumbnailName), string.Empty, DataViewRowState.CurrentRows);
                    foreach (DataRow attDelRow in attDelRows)
                    {
                        //remove any duplicate attachment row(s)
                        attDelRow.Delete();
                    }

					//add the new attachment row(s)
					dtExistingThumbnails.ImportRow(attRow);

                }

            }

            if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Re-sorting the list: refname:{0}", deltaContent.ReferenceName));

            //re-sort the list (based on the SORT property, or the ItemOrder)			
            string sort = deltaContent.DefaultSort;
            if (string.IsNullOrEmpty(sort)) sort = "ItemOrder";

            try
            {
                dsExisting.Tables["ContentItems"].DefaultView.Sort = sort;
            }
            catch
            {
                dsExisting.Tables["ContentItems"].DefaultView.Sort = "itemOrder";
            }

            //add in the tags	
//TODO: figure out the tags			
            //if (dsExisting.Tags != null)
            //{
            //    if (dsDelta.Tags != null)
            //    {
            //        if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Merging tags: refname:{0}", deltaContent.ReferenceName));

            //        dsExisting.Tags.Clear();
            //        dsExisting.Tags.Merge(dsDelta.Tags);
            //    }
            //}
            //else
            //{
            //    if (dsDelta.Tags != null)
            //    {
            //        if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Copying tags: refname:{0}", deltaContent.ReferenceName));
            //        dsExisting.Tables.Add(dsDelta.Tags.Copy());
            //    }
            //}

            if (debugSync) WebTrace.WriteWarningLine(string.Format("MergeContent: Accept changes: refname:{0}", deltaContent.ReferenceName));
            dsExisting.AcceptChanges();

            //set the last accessed date based on the delta list
            existingContent.LastAccessDate = deltaContent.LastAccessDate;

            return existingContent;
        }

        /// <summary>
        /// Writes an object to the file system in the temp location (ContentFilePath/Temp)
        /// </summary>
        /// <param name="item"></param>
        /// <param name="websiteName"></param>
        internal static void WriteCacheObjectToTemp(AgilityItem item, string websiteName)
        {
            //get the cache key for this item
            string cacheKey = GetCacheKey(item);

            //get the filepath
            string filepath = GetTempFilePathForItem(item, websiteName);

            //write out the file
            WriteFile(item, filepath, item.LastAccessDate);


        }
		
        public static AgilityItem MergeDeltaItems(AgilityItem existingItem, AgilityItem deltaItem, string websiteName)
        {
            string filepath = GetFilePathForItem(deltaItem, websiteName, transientPath: false);

            if (existingItem is AgilityContent || deltaItem is AgilityContent)
            {

                //get the "current" version of the content
                AgilityContent newItem = MergeContent((AgilityContent)existingItem, (AgilityContent)deltaItem);

                //write the newItem to the fileSystem
                WriteFile(newItem, filepath, DateTime.MinValue);

                return newItem;

            }
            else if (existingItem is AgilityUrlRedirectionList)
            {
                //handle url redirections
                AgilityUrlRedirectionList existing = existingItem as AgilityUrlRedirectionList;
                AgilityUrlRedirectionList delta = deltaItem as AgilityUrlRedirectionList;

                if (existing == null) return delta;


                //add any new/updated items
                if (delta.Redirections == null) delta.Redirections = new AgilityUrlRedirection[0];
                if (existing.Redirections == null) existing.Redirections = new AgilityUrlRedirection[0];

                if (delta.Redirections.Length == 0 && (delta.DeletedRedirections == null || delta.DeletedRedirections.Length == 0))
                {
                    return existing;
                }

                if (delta.IsCompleteSet) return delta;

                List<AgilityUrlRedirection> lst = new List<AgilityUrlRedirection>(existing.Redirections);

                foreach (AgilityUrlRedirection rDelta in delta.Redirections)
                {
                    //check for update...
                    int index = lst.FindIndex(r => r.UrlRedirectionID == rDelta.UrlRedirectionID);
                    if (index != -1)
                    {
                        lst[index] = rDelta;
                    }
                    else
                    {
                        lst.Add(rDelta);
                    }
                }

                //deletions
                if (delta.DeletedRedirections != null)
                {
                    foreach (int id in delta.DeletedRedirections)
                    {
                        int index = lst.FindIndex(r => r.UrlRedirectionID == id);
                        if (index != -1)
                        {
                            lst.RemoveAt(index);
                        }
                    }
                }

                existing.Redirections = lst.ToArray();

                return existing;

            }
            else if (existingItem is AgilitySitemap)
            {
                //handle sitemap delta
                if (((AgilitySitemap)deltaItem).SitemapXml != null)
                {
                    //write out the object to the file system
                    WriteFile(deltaItem, filepath, DateTime.MinValue);

                    return deltaItem;
                }
                else
                {
                    return existingItem;
                }

            }
            else if (existingItem is AgilityPage)
            {

                //if this is a dynamic page with a content reference name specified, update the index
                //create/update the dynamic page index with this page id and dynamic page list reference name

                AgilityPage page = deltaItem as AgilityPage;
                if (page == null || page.ID == -1) page = existingItem as AgilityPage;

                if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
                    || !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
                {
                    BaseCache.UpdateDynamicPageIndex(page.ID, page.DynamicPageContentViewReferenceName);
                    if (existingItem.ID != deltaItem.ID)
                    {
                        //update the Dynamic Page Formula Index that use this page...
                        BaseCache.ClearDynamicDynamicPageFormulaIndex(page);
                    }

                    if (AgilityContext.CurrentMode == Enum.Mode.Staging || Current.Settings.DevelopmentMode)
                    {
                        //clear out the dynamic formulas for this page in staging mode cause the dependance won't work in this case
                        BaseCache.ClearDynamicDynamicPageFormulaIndex(page);
                    }
                }


                return page;

            }
			else if (existingItem is AgilityExperimentListing)
			{

				//if this is an experiment listing

				AgilityExperimentListing exList = existingItem as AgilityExperimentListing;
				AgilityExperimentListing deltaList = deltaItem as AgilityExperimentListing;

				if (deltaList != null && deltaList.Items != null && deltaList.Items.Length > 0)
				{
					exList.Merge(deltaList);
				}

				return deltaItem;

			}
			else
            {
                //handle items that are changed or not...

                if (deltaItem == null || deltaItem.ID == -1)
                {
                    return existingItem;
                }
                else
                {
                    return deltaItem;
                }
            }
        }

        internal static string GetTempFilePathForItem(AgilityItemKey itemKey, string websiteName)
        {
            return GetFilePathForItemKey(itemKey, websiteName, tempPath: true, transientPath: false);
        }

        internal static string GetTempFilePathForItem(AgilityItem item, string websiteName)
        {
            AgilityItemKey itemKey = GetItemKeyFromAgilityItem(item);

            return GetFilePathForItemKey(itemKey, websiteName, tempPath: true, transientPath: false);
        }

        internal static string GetFilePathForItem(AgilityItem item, string websiteName, bool transientPath = false)
        {
            AgilityItemKey itemKey = GetItemKeyFromAgilityItem(item);

            return GetFilePathForItemKey(itemKey, websiteName, transientPath: transientPath);

        }

        internal static string GetFilePathForItemKey(AgilityItemKey itemKey, string websiteName, bool tempPath = false, bool transientPath = false)
        {

            string cacheRoot = null;

            if (transientPath)
            {
                //build the path to the transient cache...
                cacheRoot = Current.Settings.TransientCacheFilePath;
            }
            else
            {
                //build the path to the persistent cache
                cacheRoot = Current.Settings.ContentCacheFilePath;
            }

            StringBuilder sb = new StringBuilder(cacheRoot);

            if (!cacheRoot.EndsWith(Path.DirectorySeparatorChar) && !cacheRoot.EndsWith("/"))
			{
				sb.Append(Path.DirectorySeparatorChar);
			}

            if (AgilityContext.ContentAccessor == null)
            {
                //only append the website name if we have to
                sb.Append(websiteName).Append(Path.DirectorySeparatorChar);
            }

			if (!sb.ToString().EndsWith(Path.DirectorySeparatorChar.ToString()))
			{
				sb.Append(Path.DirectorySeparatorChar);
			}

            if (tempPath)
            {
                sb.Append("Temp");
            }
            else
            {
                if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
                {
                    sb.Append("Live");
                }
                else
                {
                    sb.Append("Staging");
                }
            }

            sb.Append(Path.DirectorySeparatorChar);

            if (itemKey.ItemType == typeof(AgilityDocument).Name
                || itemKey.ItemType == typeof(Objects.Attachment).Name)
            {
                //build the file name from the key alone
                sb.Append(itemKey.Key);
            }
            else if (itemKey.ItemType == typeof(AgilityModule).Name
                || itemKey.ItemType == typeof(AgilityDomainConfiguration).Name)
            {
                sb.Append(itemKey.ItemType).Append("_").Append(itemKey.Key).Append(".bin");
            }
            else
            {

                //build the file name from the key and the "bin" extension
                sb.Append(itemKey.ItemType).Append("_").Append(itemKey.Key).Append("_").Append(itemKey.LanguageCode).Append(".bin");
            }

            return sb.ToString();

        }

        /// <summary>
        /// Gets an AgilityItem with the given item key.
        /// </summary>
        /// <param name="itemKey"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        private static TAgilityItem GetItem<TAgilityItem>(AgilityItemKey itemKey, string websiteName) where TAgilityItem : AgilityItem
        {
            return GetItem<TAgilityItem>(itemKey, websiteName, false);
        }
        private static TAgilityItem GetItem<TAgilityItem>(AgilityItemKey itemKey, string websiteName, bool forceGetFromServer) where TAgilityItem : AgilityItem
        {


            #region  *** parameter validation ***

            if (itemKey == null)
            {
                throw new ArgumentException("itemKey cannot be null.", "itemKey");
            }

            if (string.IsNullOrEmpty(websiteName))
            {
                throw new ArgumentException("websiteName cannot be null.", "websiteName");
            }

            #endregion

            //check that the website name is configured for use in this site...
            if (string.IsNullOrWhiteSpace(Current.Settings.WebsiteName))
            {
                throw new ApplicationException(string.Format("The security key for website '{0}' could not be found.", websiteName));
            }

            //build the authorization object
            AgilityWebsiteAuthorization auth = BaseCache.GetAgilityWebsiteAuthorization();

            object o = null;

            string cacheKey = GetCacheKey(itemKey);

            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
            {
                #region *** LIVE ***


                //get the object from cache if possible
                if (AgilityContext.HttpContext != null && cacheKey != null)
                {
                    o = AgilityCache.Get(cacheKey);
                }

                if (o == null)
                {
                    //get the filepath
                    string filepath = GetFilePathForItemKey(itemKey, websiteName, transientPath: true);


                    if (AgilityContext.ContentAccessor != null)
                    {

                        //use the content accessor if possible
                        o = AgilityContext.ContentAccessor.ReadContentCacheFile(cacheKey);
                    }
                    else if (File.Exists(filepath))
                    {

                        //get the object from the file system
                        o = ReadFile<TAgilityItem>(filepath, cacheKey);
                    }


                    //special case for Content - if it has changes, we need to AcceptChanges if reading from the filesystem
                    if (o is AgilityContent)
                    {
                        AgilityContent ac = (AgilityContent)o;
                        if (ac.DataSet != null && ac.DataSet.HasChanges())
                        {
                            ac.DataSet.AcceptChanges();
                        }

                    }


                    if (cacheKey != null)
                    {
                        //check that the item wasn't added to cache since we did our read
                        if (AgilityContext.HttpContext != null)
                        {
                            object tmpObj = AgilityCache.Get(cacheKey);
                            if (tmpObj is AgilityItem)
                            {
                                //if the object was already in cache, use it
                                o = tmpObj;
                            }
                            else if (o != null && o is AgilityItem)
                            {
                                AddObjectToCache((AgilityItem)o, cacheKey, filepath);
                            }
                        }
                    }
                }

                if (o != null)
                {
                    //add this object's cachekey to the OutputCache dependancy				
                    if (!AgilityContext.OutputCacheKeys.Contains(cacheKey))
                    {
                        AgilityContext.OutputCacheKeys.Add(cacheKey);
                    }
                }

                #endregion

            }
            else
            {
                #region *** STAGING ***

                if (AgilityContext.HttpContext != null && cacheKey != null)
                {
                    o = AgilityContext.HttpContext.Items[cacheKey];

                }

                AgilityItem item = null;

                if (o == null || !(o is TAgilityItem))
                {

					//access the content server
					AgilityContentServerClient client = BaseCache.GetAgilityServerClient();
                    

                    string filepath = GetFilePathForItemKey(itemKey, websiteName, transientPath: true);
                    DateTime lastWriteTime = File.GetLastWriteTime(filepath);

                       
                    //grab the existing item from the file system
                    o = ReadFile<TAgilityItem>(filepath);

                       


                    TAgilityItem existingItem = default(TAgilityItem);

                    if (o != null && o is AgilityItem)
                    {
                        //if we have an existing item, set the last access date on the itemKey for the delta lookup
                        existingItem = (TAgilityItem)o;

                        AgilityDomainConfiguration config = existingItem as AgilityDomainConfiguration;
                        if (config != null)
                        {
                            //use the domain config from the file system if it's less than 1 minute old...
                            FileInfo fi = new FileInfo(filepath);
                            if (fi.Exists && (DateTime.Now - fi.LastWriteTime) > TimeSpan.FromMinutes(1))
                            {
                                existingItem = null;
                            }
                            else
                            {
                                //SPECIAL CASE FOR CONFIG OBJECT
                                AgilityContext.HttpContext.Items[cacheKey] = config;
                                return existingItem;
                            }


                        }

                        //if we have a sitemap object, don't specify the last access date if the XML is pooched
                        AgilitySitemap existingSitemap = existingItem as AgilitySitemap;
                        if (existingSitemap == null || (existingSitemap != null && existingSitemap.XMaxPageVersionID < 1))
                        {
                            itemKey.LastAccessDate = DateTime.MinValue;
                        }
                        else
                        {
                            itemKey.LastAccessDate = existingItem.LastAccessDate;
                            itemKey.LastAccessDateSpecified = true;
                        }

                        if (existingItem is AgilityContent)
                        {
                            //special case for AgilityContent
                            AgilityContent tmpContent = existingItem as AgilityContent;
                            if (tmpContent.DataSet != null && ! tmpContent.DataSet.Tables.Contains("AttachmentThumbnails"))
                            {
                                //force a full pull get if the Attachment Thumbnails have not been initialized
                                existingItem = null;
                            }
                            else
                            {
                                itemKey.LastAccessDate = existingItem.LastAccessDate;
                                itemKey.LastAccessDateSpecified = true;
                            }

                        }


                    }

                    AgilityItemKey[] itemKeys = new AgilityItemKey[] { itemKey };

                    AgilityItem[] items = new AgilityItem[0];

                    //check if the item is up to date...						


                    //HACK if (existingItem == null || AgilityContext.RefreshStagingModeObject(lastWriteTime) || forceGetFromServer)
                    if (existingItem == null || Indexes.IsStagingItemOutOfDate(existingItem))
                    {
                        //only go to the server if we have 
                        WebTrace.WriteVerboseLine("GetAgilityItemsForStaging: " + itemKey.ItemType + " - " + itemKey.Key);
                        items = client.GetAgilityItemsForStagingAsync(auth, itemKeys).Result.GetAgilityItemsForStagingResult;

                        bool hackTest = Indexes.IsStagingItemOutOfDate(existingItem);

                    }
                    else
                    {
                        //use the item from the file system if possible
                        o = existingItem;

                    }

                    if (items != null && items.Length > 0)
                    {
                        item = items[0];


                        if (existingItem != default(TAgilityItem) || item is AgilityContent)
                        {
                            //merge/overwrite the content as neccessary
                            //this will write the object to the file system appropriately, too
                            o = MergeDeltaItems(existingItem, item, websiteName);


                        }
                        else
                        {
                            o = item;
                        }

                        if (o != null && o is TAgilityItem)
                        {

                            //write to the file system so we can do DELTA caching
                            if (item is AgilityContent
                                && ((AgilityContent)item).DataSet != null
                                && ((AgilityContent)item).DataSet.Tables["ContentItems"] != null
                                && ((AgilityContent)item).DataSet.Tables["ContentItems"].Rows.Count > 0)
                            {
                                //write the content to the file system
                               // ((AgilityContent)item).DataSet.RemotingFormat = SerializationFormat.Binary;
                                WriteFile(o, GetFilePathForItem((TAgilityItem)o, websiteName, transientPath: true), DateTime.MinValue);
                            }
                            else if (o is AgilitySitemap && existingItem == null)
                            {
                                //write the sitemap to the file system (if it is new)									
                                WriteFile(o, GetFilePathForItem((TAgilityItem)o, websiteName, transientPath: true), DateTime.MinValue);
                            }
                            else if (o is AgilityDomainConfiguration && existingItem == null)
                            {
                                //write the sitemap to the file system (if it is new)									
                                WriteFile(o, GetFilePathForItem((TAgilityItem)o, websiteName, transientPath: true), DateTime.MinValue);
                            }
                            else if (o is AgilityTagList && item != null)
                            {
                                //write the taglist to the file system (if it has changed)
                                AgilityTagList tagList = o as AgilityTagList;
                                if (tagList != null)
                                {
                                    if (tagList.DSTags != null)
                                    {
                                       // tagList.DSTags.RemotingFormat = SerializationFormat.Binary;
                                    }
                                    WriteFile(o, GetFilePathForItem((TAgilityItem)o, websiteName, transientPath: true), DateTime.MinValue);
                                }
                            }
                            else if (item.ID > 0)
                            {
                                //only write to the file system if the item we got back from the server is a valid item
                                WriteFile(o, GetFilePathForItem((TAgilityItem)o, websiteName, transientPath: true), DateTime.MinValue);
                            }


                        }

                        //if we accessed the item from the server, ensure it's last write time is set...
                        if (File.Exists(filepath))
                        {
                            File.SetLastWriteTime(filepath, DateTime.Now);
                        }


                    }




                    //put the item in Context, then return it						
                    if (AgilityContext.HttpContext != null && cacheKey != null && o != null)
                    {
                        AgilityContext.HttpContext.Items[cacheKey] = o;

                    }
                    

                }


                //keep track of all the items that have been loaded in staging mode
                if (itemKey.ItemType == "AgilityContent")
                {
                    AgilityContext.LoadedItemKeys[cacheKey] = itemKey;
                }

                #endregion
            }


            //return the object
            if (o != null && o is TAgilityItem)
            {

                return (TAgilityItem)o;
            }
            else
            {
                //return nothing; the item isn't available
                return default(TAgilityItem);
            }

        }




        private static object _secondaryPageObjectLock = new object();

        /// <summary>
        /// Recursively gets the items that we need everytime for a page (modules)
        /// </summary>
        /// <param name="itemKey"></param>
        /// <param name="websiteName"></param>
        /// <param name="agilityPage"></param>
        private static void GetSecondaryObjectsForPage(AgilityItemKey itemKey, string websiteName, AgilityPage agilityPage)
        {


            //only proceeed if the page is NOT in context (only do this once per 

            List<AgilityItemKey> lstSecondaryKeys = new List<AgilityItemKey>();
            List<AgilityItem> lstSecondaryItems = new List<AgilityItem>();
            if (agilityPage == null || agilityPage.ContentSections == null) return;
            foreach (ContentSection contentSection in agilityPage.ContentSections)
            {
                if (string.IsNullOrEmpty(contentSection.ContentReferenceName)) continue;

                //module defs on the page
                AgilityItemKey moduleKey = lstSecondaryKeys.Find(delegate(AgilityItemKey match)
                {
                    int id = 0;
					if (!int.TryParse($"{match.Key}", out id)) id = -1;
					
                    return match.ItemType == typeof(AgilityModule).Name && contentSection.ModuleID == id;
                });

                if (moduleKey == null)
                {
                    moduleKey = new AgilityItemKey();
                    moduleKey.ItemType = typeof(AgilityModule).Name;
                    moduleKey.Key = contentSection.ModuleID;
                    moduleKey.LanguageCode = itemKey.LanguageCode;

                    DateTime lastWriteTime = DateTime.MinValue;

                    //see if this module is already cached for whatever reason...
                    string moduleCacheKey = GetCacheKey(moduleKey);
                    if (AgilityContext.HttpContext.Items[moduleCacheKey] == null)
                    {
                        string moduleFilePath = GetFilePathForItemKey(moduleKey, websiteName, transientPath: true);
                        AgilityModule existingModule = ReadFile< AgilityModule>(moduleFilePath);
                        if (existingModule != null)
                        {
                            moduleKey.LastAccessDate = existingModule.LastAccessDate;
                            moduleKey.LastAccessDateSpecified = true;
                            lstSecondaryItems.Add(existingModule);
                            lastWriteTime = File.GetLastWriteTime(moduleFilePath);

                        }


                        if (Indexes.IsStagingItemOutOfDate(existingModule))
                        {
                            //only add the lookup key if we need to (if we add the key, it will do a delta lookup)
                            lstSecondaryKeys.Add(moduleKey);
                        }
                    }
                }

            }


            //ONLY GET THE ITEMS THAT WERE REQUESTED LAST TIME for this page
            Dictionary<string, AgilityItemKey> preloadedItemKeys = ReadFile< Dictionary<string, AgilityItemKey>>(agilityPage.ItemsToPreloadFilePath);
            if (preloadedItemKeys != null)
            {
                foreach (AgilityItemKey preloadedItemKey in preloadedItemKeys.Values)
                {
                    string preloadedItemCacheKey = GetCacheKey(preloadedItemKey);
                    if (AgilityContext.HttpContext.Items[preloadedItemCacheKey] == null)
                    {
                        string preloadedItemFilepath = GetFilePathForItemKey(preloadedItemKey, websiteName, transientPath: true);
                        AgilityItem existingPreloadedItem = ReadFile<AgilityItem>(preloadedItemFilepath);
                        if (existingPreloadedItem != null)
                        {
                            preloadedItemKey.LastAccessDate = existingPreloadedItem.LastAccessDate;
                            preloadedItemKey.LastAccessDateSpecified = true;
                            lstSecondaryItems.Add(existingPreloadedItem);

                        }

                        DateTime lastWriteTime = File.GetLastWriteTime(preloadedItemFilepath);


                        if (Indexes.IsStagingItemOutOfDate(existingPreloadedItem))
                        {
                            //only add the lookup key if we need to (if we add the key, it will do a delta lookup)
                            lstSecondaryKeys.Add(preloadedItemKey);
                        }
                    }

                }
            }


            //make the request to the server for these deltas...
            List<AgilityItem> secondaryItemsFromServer = new List<AgilityItem>();
            if (lstSecondaryKeys.Count > 0)
            {
                //if there are more than 40 items in the list, only get the first 40 and write a warning to the log
                if (lstSecondaryKeys.Count > 40)
                {
                    Agility.Web.Tracing.WebTrace.WriteWarningLine(string.Format("There are {0} items being loaded on page {1}.  This could be causing performance problems in staging/development mode.", lstSecondaryKeys.Count, AgilityContext.HttpContext.Request.GetDisplayUrl()));
                    lstSecondaryKeys = lstSecondaryKeys.GetRange(0, 39);
                }

				AgilityContentServerClient client = BaseCache.GetAgilityServerClient();
      
                    AgilityWebsiteAuthorization auth = new AgilityWebsiteAuthorization();
                    auth.SecurityKey = Current.Settings.SecurityKey;
                    auth.WebsiteName = websiteName;

                    foreach (var itemKeyx in lstSecondaryKeys)
                    {
                        WebTrace.WriteVerboseLine("GetAgilityItemsForStaging: " + itemKeyx.ItemType + " - " + itemKeyx.Key);
                    }

					var secondaryRes = client.GetAgilityItemsForStagingAsync(auth, lstSecondaryKeys.ToArray()).Result;
					secondaryItemsFromServer = new List<AgilityItem>(secondaryRes.GetAgilityItemsForStagingResult);
                
            }

            //delta these items, save to filesystem and cache them in memory...
            for (int i = secondaryItemsFromServer.Count - 1; i >= 0; i--)
            {

                AgilityItem itemFromServer = secondaryItemsFromServer[i];

                string filepath = GetFilePathForItem(itemFromServer, websiteName, transientPath: true);
                string cacheKey = GetCacheKey(itemFromServer);

                AgilityItem existingItem = null;

                int existingItemIndex = lstSecondaryItems.FindIndex(delegate(AgilityItem match)
                {
                    return match.GetType().Name == itemFromServer.GetType().Name && match.ID == itemFromServer.ID;
                });



                if (existingItemIndex == -1)
                {
                    //no delta - write the thing to the filesystem
                    WriteFile(itemFromServer, filepath);
                    AgilityContext.HttpContext.Items[cacheKey] = itemFromServer;
                }
                else
                {
                    //do the delta
                    existingItem = lstSecondaryItems[existingItemIndex];

                    AgilityItem deltaItem = MergeDeltaItems(existingItem, itemFromServer, websiteName);
                    WriteFile(deltaItem, filepath);
                    AgilityContext.HttpContext.Items[cacheKey] = deltaItem;
                    lstSecondaryItems.RemoveAt(existingItemIndex);


                }

                //set the last write time of the file..
                if (File.Exists(filepath))
                {
                    File.SetLastWriteTime(filepath, DateTime.Now);
                }


                secondaryItemsFromServer.RemoveAt(i);
            }

            //if there are any "existing" items left (from file system), put them in cache
            foreach (AgilityItem item in lstSecondaryItems)
            {
                string cacheKey = GetCacheKey(item);
                AgilityContext.HttpContext.Items[cacheKey] = item;
            }


        }

        private static void GetContentAndKeysToUpdate(string websiteName, ref List<AgilityItemKey> lstSecondaryKeys, ref List<AgilityItem> lstSecondaryItems, string contentReferenceName, string languageCode)
        {
            if (string.IsNullOrEmpty(contentReferenceName)) return;

            if (lstSecondaryKeys.FindIndex(delegate(AgilityItemKey match)
            {
                return string.Equals(match.Key as string, contentReferenceName, StringComparison.CurrentCultureIgnoreCase);
            }) != -1)
            {
                //dont process the same item twice...
                return;
            }

            if (lstSecondaryItems.FindIndex(delegate(AgilityItem match)
            {
                AgilityContent content = match as AgilityContent;
                if (content == null) return false;

                return string.Equals(content.ReferenceName, contentReferenceName, StringComparison.CurrentCultureIgnoreCase);
            }) != -1)
            {
                //dont process the same item twice...
                return;
            }

            AgilityItemKey contentKey = lstSecondaryKeys.Find(delegate(AgilityItemKey match)
            {
                return match.ItemType == typeof(AgilityContent).Name && contentReferenceName == string.Format("{0}", match.Key);
            });
            if (contentKey == null)
            {
                contentKey = new AgilityItemKey();
                contentKey.ItemType = typeof(AgilityContent).Name;
                contentKey.Key = contentReferenceName;
                contentKey.LanguageCode = languageCode;

                //check if this content is cached already
                string contentCacheKey = GetCacheKey(contentKey);
                AgilityContent existingContent = AgilityContext.HttpContext.Items[contentCacheKey] as AgilityContent;

                if (existingContent == null)
                {

                    string contentFilePath = GetFilePathForItemKey(contentKey, websiteName, transientPath: true);
                    existingContent = ReadFile< AgilityContent>(contentFilePath);
                    if (existingContent != null)
                    {
                        contentKey.LastAccessDate = existingContent.LastAccessDate;
                        contentKey.LastAccessDateSpecified = true;
                        lstSecondaryItems.Add(existingContent);
                    }

                    DateTime lastWriteTime = File.GetLastWriteTime(contentFilePath);

                    if (Indexes.IsStagingItemOutOfDate(existingContent))
                    {
                        lstSecondaryKeys.Add(contentKey);
                    }
                }

                //recurse into shared/linked content for this content...
                if (existingContent != null && existingContent.DataSet != null && existingContent.DataSet.Tables["ContentItems"] != null)
                {
                    foreach (DataRow contentRow in existingContent.DataSet.Tables["ContentItems"].Rows)
                    {
                        //go through each linked content on here...

                        foreach (DataColumn column in existingContent.DataSet.Tables["ContentItems"].Columns)
                        {

                            if (!string.IsNullOrEmpty(string.Format("{0}", column.ExtendedProperties["contentdefinition"])))
                            {

                                string linkedReferenceName = string.Format("{0}", contentRow[column]);
                                if (!string.IsNullOrEmpty(linkedReferenceName))
                                {
                                    GetContentAndKeysToUpdate(websiteName, ref lstSecondaryKeys, ref lstSecondaryItems, linkedReferenceName, languageCode);
                                }
                            }
                        }
                    }
                }

            }
        }



        /// <summary>
        /// Adds an AgilityItem to the Memory cache with the appropriate dependancies.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="cacheKey"></param>
        /// <param name="filepath"></param>
        private static void AddObjectToCache(AgilityItem item, string cacheKey, string filepath)
        {
            //build a dependancy based on the item's type
            CacheDependency dep = null;
            CacheItemPriority cachePriority = CacheItemPriority.NeverRemove;
            if (item is AgilityDomainConfiguration)
            {
                //check if the domain config has changed, if not, DO NOT SAVE THE FILE

                //config is dependant only on its file
                if (AgilityContext.ContentAccessor != null)
                {
                    dep = AgilityContext.ContentAccessor.GetCacheDependency(AgilityContext.WebsiteName, cacheKey, null);
                }
                else
                {
                    dep = new CacheDependency(filepath);
                }

                //get the cache priority for here
                AgilityDomainConfiguration domainConfig = (AgilityDomainConfiguration)item;
                if (domainConfig.CacheItemPrioritySpecified && domainConfig.CacheItemPriority > 0)
                {
                    cachePriority = (CacheItemPriority)domainConfig.CacheItemPriority;
                }

            }
            else if (item is AgilitySitemap)
            {

                string[] depKeys = new string[] { CACHEKEY_CONFIG };

                //sitemap is dependant on the config and its file
                if (AgilityContext.ContentAccessor != null)
                {
                    dep = AgilityContext.ContentAccessor.GetCacheDependency(AgilityContext.WebsiteName, cacheKey, depKeys);
                }
                else
                {
                    dep = new CacheDependency(new string[] { filepath }, depKeys);
                }
            }
            else if (!string.IsNullOrEmpty(item.LanguageCode))
            {
                //all with a language are dependant on the sitemap for their language and their file
                string[] depKeys = null;
                if (item.LanguageCode != Providers.AgilityDynamicCodeFile.LANGUAGECODE_CODE)
                {
                    // use the sitemap as main cache key - if that changes, bump everything
                    depKeys = new string[] { GetCacheKey_Sitemap(item.LanguageCode) };
                }

                if (AgilityContext.ContentAccessor != null)
                {
                    dep = AgilityContext.ContentAccessor.GetCacheDependency(AgilityContext.WebsiteName, cacheKey, depKeys);
                }
                else
                {
                    dep = new CacheDependency(new string[] { filepath }, depKeys);
                }

                if(BaseCache.CACHEKEY_CONFIG == cacheKey)
                {
                    cachePriority = CacheItemPriority.NeverRemove;
                }
                else if(AgilityContext.Domain != null)
                {
                    //get the cache item priority from the current domain configuration object
                    cachePriority = AgilityContext.Domain.CacheItemPriority;
                }
            }
            else
            {
                //just dependant on the file				
                if (AgilityContext.ContentAccessor != null)
                {
                    dep = AgilityContext.ContentAccessor.GetCacheDependency(AgilityContext.WebsiteName, cacheKey, null);
                }
                else
                {
                    dep = new CacheDependency(filepath);
                }

                //get the cache item priority from the current domain configuration object
                if (AgilityContext.Domain != null)
                {
                    cachePriority = AgilityContext.Domain.CacheItemPriority;
                }
            }

            if (cachePriority <= 0)
            {
                cachePriority = CacheItemPriority.NeverRemove;
            }

            //add the object to cache for 24 hours (or until the app pool resets)
            if (AgilityContext.HttpContext != null)
            {
                AgilityCache.Set(cacheKey, item, TimeSpan.FromDays(1), dep, cachePriority);

				AgilityContext.OutputCacheDependencies.Add(cacheKey);
                
            }
        }

        /// <summary>
        /// Gets the Cache Key for a given AgilityItem.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The CacheKey that will be used to store the given item in cache and on the file system.</returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="NotSupportedException"/>
        public static string GetCacheKey(AgilityItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            AgilityItemKey itemKey = GetItemKeyFromAgilityItem(item);

            return GetCacheKey(itemKey);
        }


        /// <summary>
        /// Gets the cache key from the an ItemKey object.
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        internal static string GetCacheKey(AgilityItemKey itemKey)
        {
            if (itemKey == null)
            {
                throw new ArgumentNullException("itemKey");
            }


            if (itemKey.ItemType == typeof(AgilityDigitalChannelList).Name)
            {
                return CACHEKEY_CHANNELS;
            }
            else if (itemKey.ItemType == typeof(AgilityDomainConfiguration).Name)
            {
                return CACHEKEY_CONFIG;
            }
            else if (itemKey.ItemType == typeof(AgilitySitemap).Name)
            {
                //sitemap uses the word Sitemap and the language code
                return GetCacheKey_Sitemap(itemKey.LanguageCode);

            }
            else if (itemKey.ItemType == typeof(AgilityContentServer.AgilityPage).Name)
            {
				//pageitem uses the ID (stored in sitemap as picID) and the language
				int id = -1;
				if (!int.TryParse($"{itemKey.Key}", out id)) id = -1;

				return GetCacheKey_Page(id, itemKey.LanguageCode);

            }
            else if (itemKey.ItemType == typeof(AgilityContent).Name)
            {
                //content uses the reference name and the language code
                return GetCacheKey_Content((string)itemKey.Key, itemKey.LanguageCode);
            }
            else if (itemKey.ItemType == typeof(AgilityModule).Name)
            {
				//module uses module_ID
				int id = -1;
				if (!int.TryParse($"{itemKey.Key}", out id)) id = -1;
				return GetCacheKey_Module(id);
            }
            else if (itemKey.ItemType == typeof(AgilityPageDefinition).Name)
            {
				//page def uses pageDefinition_ID
				int id = -1;
				if (!int.TryParse($"{itemKey.Key}", out id)) id = -1;
				return GetCacheKey_PageDefinition(id);
            }
            else if (itemKey.ItemType == typeof(AgilityTagList).Name)
            {
                //content uses the reference name and the language code
                return GetCacheKey_TagList(itemKey.LanguageCode);
            }
            else if (itemKey.ItemType == typeof(AgilityDocument).Name)
            {
                if (itemKey.Key == null) return null;
                return string.Format("AgilityDocument_{0}", itemKey.Key.ToString().Replace("\\", "/").Replace("/", "~~~"));
            }
            else if (itemKey.ItemType == typeof(AgilityAssetMediaGroup).Name)
            {
                if (itemKey.Key == null) return null;
                return string.Format("AgilityMediaGroup_{0}", itemKey.Key.ToString());
            }
            else
            {
                return string.Format("{0}{1}", CACHEKEY_PREFIX, itemKey.Key).ToLowerInvariant();
            }

            throw new NotSupportedException(string.Format("The type {0} does not support caching.", itemKey.ItemType));
        }

        private static string GetCacheKey_Sitemap(string languageCode)
        {
            return CACHEKEY_PREFIX + ITEMKEY_SITEMAP + string.Format("_{0}", languageCode).ToLowerInvariant();
        }

        private static string GetCacheKey_Page(int pagecontentID, string languageCode)
        {
            return CACHEKEY_PREFIX + string.Format("page_{0}_{1}", pagecontentID, languageCode).ToLowerInvariant();
        }

        private static string GetCacheKey_Content(string referenceName, string languageCode)
        {
            return CACHEKEY_PREFIX + string.Format("content_{0}_{1}", referenceName, languageCode).ToLowerInvariant();
        }

        private static string GetCacheKey_TagList(string languageCode)
        {
            return string.Format("{0}{1}_{2}", CACHEKEY_PREFIX, BaseCache.ITEMKEY_TAGLIST, languageCode).ToLowerInvariant();
        }

        private static string GetCacheKey_Module(int id)
        {
            return CACHEKEY_PREFIX + string.Format("module_{0}", id);
        }

        private static string GetCacheKey_PageDefinition(int id)
        {
            return CACHEKEY_PREFIX + string.Format("pagedefinition_{0}", id);
        }




        /// <summary>
        /// Gets an AgilityItemKey from an AgilityItem based on the various item types that we have.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static AgilityItemKey GetItemKeyFromAgilityItem(AgilityItem item)
        {
            AgilityItemKey itemKey = new AgilityItemKey();
            itemKey.ItemType = item.GetType().Name;
            itemKey.LanguageCode = item.LanguageCode;


            if (item is AgilityDocument)
            {
                itemKey.Key = ((AgilityDocument)item).FilePath;
            }
            else if (item is AgilityContent)
            {
                itemKey.Key = ((AgilityContent)item).ReferenceName;
            }
            else if (item is AgilityContentServer.AgilityPage)
            {
                itemKey.Key = item.ID;
            }
            else if (item is AgilityDomainConfiguration)
            {
                itemKey.Key = ITEMKEY_CONFIG;
            }
            else if (item is AgilityDigitalChannelList)
            {
                itemKey.Key = ITEMKEY_CHANNELS;
            }
            else if (item is AgilitySitemap)
            {
                itemKey.Key = ITEMKEY_SITEMAP;
            }
            else if (item is AgilityModule)
            {
                itemKey.Key = item.ID;
            }
            else if (item is AgilityTagList)
            {
                itemKey.Key = ITEMKEY_TAGLIST;
            }
            else if (item is AgilityUrlRedirectionList)
            {
                itemKey.Key = ITEMKEY_URLREDIRECTIONS;
            }
            else
            {
                //default...
                itemKey.Key = item.ID;
            }

            return itemKey;
        }


        /// <summary>
        /// Returns the path the local content folder for the current website (in staging or live mode)
        /// </summary>
        /// <returns></returns>
        internal static string GetLocalContentFilePath()
        {

            StringBuilder sb = new StringBuilder(Current.Settings.ContentCacheFilePath);
            if (AgilityContext.ContentAccessor == null)
            {
                //only append the website name if we have to
                sb.Append(AgilityContext.WebsiteName).Append(Path.DirectorySeparatorChar);
            }

            if (!sb.ToString().EndsWith(Path.DirectorySeparatorChar.ToString())) sb.Append(Path.DirectorySeparatorChar);


            if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
            {
                sb.Append("Live");
            }
            else
            {
                sb.Append("Staging");
            }

            return sb.ToString();

        }




        #region *** File Operations ***


        private static object _lockAccess = new object();
        private static Dictionary<string, ReaderWriterLockSlim> _fileLocks = new Dictionary<string, ReaderWriterLockSlim>(100);

        private static ReaderWriterLockSlim GetFileLock(string filepath)
        {
            string lockKey = filepath.ToLowerInvariant();


            lock (_lockAccess)
            {
                //initialize the lock object we will use to lock this write operation		
                //wait for access the lock collection
                ReaderWriterLockSlim fileLock = null;

                if (!_fileLocks.TryGetValue(lockKey, out fileLock))
                {
                    fileLock = new ReaderWriterLockSlim();
                    _fileLocks[lockKey] = fileLock;
                }

                return fileLock;
            }

        }

        private static void ClearFileLock(string filepath, bool isDeleting)
        {
            string lockKey = filepath.ToLowerInvariant();

            //wait for access the lock collection


            lock (_lockAccess)
            {
                ReaderWriterLockSlim fileLock = null;

                if (_fileLocks.TryGetValue(lockKey, out fileLock))
                {
                    if (isDeleting
                        || (fileLock.WaitingReadCount == 0
                        && fileLock.WaitingUpgradeCount == 0
                        && fileLock.WaitingWriteCount == 0)
                        )
                    {
                        _fileLocks.Remove(lockKey);
                        fileLock.Dispose();

                    }
                }
            }
        }


        internal static void WriteFile(object item, string filepath)
        {
            WriteFile(item, filepath, DateTime.MinValue);
        }

        internal static void WriteFile(object item, string filepath, DateTime overrideLastModifiedDate)
        {
            filepath = filepath.Replace("//", "/");

            ReaderWriterLockSlim fileLock = GetFileLock(filepath);

            if (!fileLock.TryEnterWriteLock(TimeSpan.FromSeconds(60)))
            {
                throw new ApplicationException(string.Format("The write lock for file {0} could not be obtained.", filepath));
            }
            try
            {
                FileInfo fileInfo = new FileInfo(filepath);
                //engage binary serialization
                WriteFileWithRetries(item, filepath, overrideLastModifiedDate);
            }
            catch
            {
                throw;
            }
            finally
            {
                //unlock the writer

                fileLock.ExitWriteLock();
            }

        }

        private static DateTime WriteFileWithRetries(object item, string filepath, DateTime overrideLastModifiedDate)
        {
            //write out the file
            FileStream fs = null;
            int retryCount = 0;



            try
            {


                //create the folder if we need to
                if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                }

                //if the object is null, then we just want to clean up and get out..
                if (item == null)
                {
                    File.Delete(filepath);
                }


                int totalRetries = 3;
                IOException writeException = null;

                while (retryCount < totalRetries)
                {
                    try
                    {
                        fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read);

                        if (item is string)
                        {
                            StreamWriter sw = new StreamWriter(fs, Encoding.Unicode);
                            string str = item as string;
                            sw.Write(str);
                            sw.Flush();
                        }
                        else if (item is Stream)
                        {
                            //handle case for a Stream (usually an Attachment or Document)
                            long totalBytes = 0;
                            int bufferSize = 4096;
                            using (BufferedStream bfs = new BufferedStream((Stream)item, bufferSize))
                            {
                                byte[] bytes = new byte[bufferSize];
                                int bytesRead = 0;
                                do
                                {
                                    bytesRead = bfs.Read(bytes, 0, bufferSize);

                                    fs.Write(bytes, 0, bytesRead);
                                    totalBytes += bytesRead;
                                } while (bytesRead > 0);

                                bytes = null;
                            }

                            if (totalBytes == 0)
                            {
                                fs.Flush();
                                fs.Close();
                                fs = null;
                                overrideLastModifiedDate = DateTime.MinValue;
                                File.Delete(filepath);
                            }
                        }
                        else
                        {
                            if (item != null)
                            {

								var serializer = new JsonSerializer();

								string json = JsonConvert.SerializeObject(item);
								

								//serialize the "rest" using binary
								StreamWriter sw = new StreamWriter(fs);
								sw.Write(json);
								sw.Flush();

								//JsonWriter jw = new JsonTextWriter(sw);
								
								//	serializer.Serialize(jw, item);
								

								
								//BinaryFormatter bf = new BinaryFormatter();
        //                        bf.Serialize(fs, item);
                                
                            }
                        }

                        //if we get this far, we are good...
                        writeException = null;
                        break;
                    }
                    catch (IOException ioex)
                    {
                        //if we get an exception, keep it for reporting later...
                        writeException = ioex;

                        if (retryCount < totalRetries - 1)
                        {
                            Agility.Web.Tracing.WebTrace.WriteInfoLine(string.Format("File write error for file {0}, retrying. {1}", filepath, ioex));
                        }

                        Thread.Sleep(2000); //wait a couple seconds before retrying...
                    }

                    retryCount++;
                }


                if (writeException != null) throw writeException;

                if (fs != null)
                {
                    fs.Flush();
                    fs.Close();
                    fs = null;
                }


                //override the last mod date if we have a value for it passed in.
                if (overrideLastModifiedDate != DateTime.MinValue)
                {
                    File.SetLastWriteTime(filepath, overrideLastModifiedDate);
                }


            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error writing file: {0}.  Retried {1} time(s).", filepath, retryCount), ex);
            }
            finally
            {
                if (fs != null) fs.Close();

            }
            return overrideLastModifiedDate;
        }


        internal static void DoFileOperation(string filepath, Agility.Web.Utils.FileUtils.FileOperationDelegate fileOperationDelegate)
        {
            filepath = filepath.Replace("//", "/");

            ReaderWriterLockSlim fileLock = GetFileLock(filepath);

            if (!fileLock.TryEnterWriteLock(TimeSpan.FromSeconds(30)))
            {
                throw new ApplicationException(string.Format("The write lock for file {0} could not be obtained.", filepath));
            }

            try
            {

                //do the work...
                fileOperationDelegate(filepath);
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error doing file operation: {0}", filepath), ex);
            }
            finally
            {

                //unlock the writer
                fileLock.ExitWriteLock();
                ClearFileLock(filepath, false);
            }
        }


        internal static void DoFileReadOperation(string filepath, Agility.Web.Utils.FileUtils.FileOperationDelegate fileOperationDelegate)
        {
            filepath = filepath.Replace("//", "/");

            ReaderWriterLockSlim fileLock = GetFileLock(filepath);

            if (!fileLock.TryEnterReadLock(TimeSpan.FromSeconds(30)))
            {
                throw new ApplicationException(string.Format("The read lock for file {0} could not be obtained.", filepath));
            }

            try
            {

                //do the work...
                fileOperationDelegate(filepath);
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error doing file read operation: {0}", filepath), ex);
            }
            finally
            {

                //unlock the reader
                fileLock.ExitReadLock();
                ClearFileLock(filepath, false);
            }
        }

        internal static void DeleteFile(string filepath)
        {
            filepath = filepath.Replace("//", "/");

            ReaderWriterLockSlim fileLock = GetFileLock(filepath);

            if (!fileLock.TryEnterWriteLock(TimeSpan.FromSeconds(30)))
            {
                throw new ApplicationException(string.Format("The write lock for file {0} could not be obtained.", filepath));
            }

            try
            {
                if (File.Exists(filepath))
                {
                    File.Delete(filepath);
                }

            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error deleting file: {0}", filepath), ex);
            }
            finally
            {

                //onlock the writer
                fileLock.ExitWriteLock();
                ClearFileLock(filepath, true);

            }


        }


        /// <summary>
        /// Read the bytes of a file based on a FileInfo object.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        internal static byte[] ReadFileBytes(FileInfo fileInfo)
        {


            //don't do any work if the file isnt' there
            if (fileInfo == null) return null;
            if (!fileInfo.Exists) return null;

            ReaderWriterLockSlim fileLock = GetFileLock(fileInfo.FullName);

            //read a read lock on the file			
            if (!fileLock.TryEnterReadLock(TimeSpan.FromSeconds(30)))
            {
                throw new ApplicationException(string.Format("Could not obtain read lock on file {0}.", fileInfo.FullName));
            }

            //read the file
            FileStream fs = null;
            byte[] bytes = null;
            try
            {

                fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);


                bytes = new byte[fileInfo.Length];
                fs.Read(bytes, 0, bytes.Length);
                fs.Flush();
                fs.Close();
                fs = null;
            }
            catch (Exception ex)
            {
                //this means a file was improperly saved, or cannot be deserialized
                WebTrace.WriteWarningLine(string.Format("File {0} could not be read. ", fileInfo.FullName) + ex);
            }
            finally
            {
                if (fs != null) fs.Close();

                //release the read lock
                fileLock.ExitReadLock();
            }

            //return the bytes
            return bytes;
        }

        /// <summary>
        /// Reads a file an deserializes using binary formatting to its object format.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        //internal static object ReadFile(string filepath)
        //{
        //    return ReadFile<object>(filepath, null);
        //}

		internal static T ReadFile<T>(string filepath)
		{
			return ReadFile<T>(filepath, null);
		}

		/// <summary>
		/// Reads a file an deserializes using binary formatting to its object format.
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="cacheKey">The cacheKey that the file will be associated with</param>
		/// <returns></returns>
		internal static T ReadFile<T>(string filepath, string cacheKey)
        {
            filepath = filepath.Replace("//", "/");

            //don't do any work if the file isnt' there
            if (!File.Exists(filepath)) return default(T);
			
            ReaderWriterLockSlim fileLock = GetFileLock(filepath);

            //read a read lock on the file (wait up to 60 seconds)
            if (!fileLock.TryEnterReadLock(TimeSpan.FromSeconds(60)))
            {
                throw new ApplicationException(string.Format("Could not aquire read lock for {0}.", filepath));
            }

            T o = default(T);

            //ensure that the file hasn't been read into cache while we were waiting...
            if (AgilityContext.HttpContext != null && cacheKey != null)
            {
                o = AgilityCache.Get<T>(cacheKey);
                if (o != null)
                {
					//release the read locks
					fileLock.ExitReadLock();

                    //return the cached version of the object
                    return o;
                }
            }


            //read the file
            FileStream fs = null;

            try
            {
				FileInfo fi = new FileInfo(filepath);

				if (fi.Exists && fi.Length > 0)
				{

					fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
					
					

					using (StreamReader sr = new StreamReader(fs))
					{
						string json = sr.ReadToEnd();
						o = JsonConvert.DeserializeObject<T>(json);
					}


					//using (JsonReader reader = new JsonTextReader(sr))
					//{
					//	JsonSerializer serializer = new JsonSerializer();

					//	// read the json from a stream
					//	// json size doesn't matter because only a small piece is read at a time from the HTTP request
					//	o = serializer.Deserialize(reader);						
					//}


					//BinaryFormatter bf = new BinaryFormatter();					
					//o = bf.Deserialize(fs);
					//fs.Flush();
					//fs.Close();
					fs = null;
					
				}

            }
            catch (Exception serEx)
            {
                //this means a file was improperly saved, or cannot be deserialized
                WebTrace.WriteWarningLine(string.Format("File {0} could not be deserialized. Exception: {1} - Stacktrace: {2}", filepath, serEx, Environment.StackTrace));
                o = default(T);
            }            
            finally
            {
                if (fs != null) fs.Close();

				//release the read locks
				fileLock.ExitReadLock();
            }


            //return the object
            return o;

        }


        public static void DeleteFolder(string folderPath)
        {
            string[] filenames = System.IO.Directory.GetFiles(folderPath);

            foreach (string file in filenames)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                System.IO.File.Delete(file);
            }

            string[] directories = Directory.GetDirectories(folderPath);
            foreach (string dir in directories)
            {
                DeleteFolder(dir);
            }
        }


        internal static void ClearCacheFiles(string websiteName)
        {
            WebTrace.WriteInfoLine("Deleting cache files: " + websiteName);


            StringBuilder sb = new StringBuilder(Current.Settings.ContentCacheFilePath);

            sb.Append(websiteName).Append(Path.DirectorySeparatorChar);

            string[] dirs = Directory.GetDirectories(sb.ToString());

            foreach (string dir in dirs)
            {
                if (string.Compare(Path.GetFileName(dir), "temp", true) == 0) continue;

                if (Directory.Exists(sb.ToString()))
                {
                    try
                    {
                        try
                        {

                            //attempt a normal delete
                            Directory.Delete(dir, true);
                        }
                        catch
                        {
                            //if that fails, use a recursive delete
                            DeleteFolder(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Tracing.WebTrace.WriteException(ex, "Error occurred while trying to delete cache files.");
                    }
                }
            }

            WebTrace.WriteInfoLine("Cache Cleared: " + websiteName);


            
            if (! string.IsNullOrWhiteSpace(Current.Settings.TransientCacheFilePath)
                && Current.Settings.ContentCacheFilePath != Current.Settings.TransientCacheFilePath)
            {
                
                string dirPath = Path.Combine(Current.Settings.TransientCacheFilePath, websiteName, "Live");
                WebTrace.WriteInfoLine("Deleting transient cache files: " + dirPath);

                try
                {
                    Directory.Delete(dirPath, true);                    
                }
                catch (Exception ex)
                {
                    Tracing.WebTrace.WriteException(ex, "Error occurred while trying to delete transient cache files.");
                }

                WebTrace.WriteInfoLine("Transient Cache Cleared: " + websiteName);
            }


        }

        #endregion
    }
}
