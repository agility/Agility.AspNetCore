using Agility.Web.AgilityContentServer;
using Agility.Web.Configuration;
using Agility.Web.Enum;
using Agility.Web.Tracing;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Agility.Web.Sync
{
    public static class SyncThread
    {

        //static fields
        private static bool _isThreadQueued;
        private static bool _isClearCacheRequested;
        private static bool _isSyncInProgress = false;
        
        private static Thread _workerThread = null;
        private static object lockObj = new object();

        /// <summary>
        /// Keeps track of whether the current sync thread is queued to sync again after the current sync is finished.
        /// </summary>
        private static bool IsThreadQueued
        {
            get { return _isThreadQueued; }
            set { _isThreadQueued = value; }
        }


        private static string syncCheckFileName
        {
            get
            {
                string websiteName = AgilityContext.WebsiteName;
                string fileName = "CacheInProgress.log";

                return Path.Combine(Current.Settings.ContentCacheFilePath, websiteName, fileName);

            }
        }

        private static string syncStateFileName
        {
            get
            {
                string websiteName = AgilityContext.WebsiteName;
                string fileName = "SyncState.txt";

                return Path.Combine(Current.Settings.ContentCacheFilePath, websiteName, fileName);

            }
        }

        internal static bool IsSyncInProgress
        {
            get
            {
                return _isSyncInProgress;
            }

            set
            {
                _isSyncInProgress = value;

                string path = syncCheckFileName;

                if (value)
                {

                    string content = Environment.MachineName;

                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(path, content);
                }
                else
                {
                    File.Delete(path);
                }

            }
        }


        internal static bool IsSyncInProgressOnOtherMachine
        {
            get
            {
                if (_isSyncInProgress) return false;

                string path = syncCheckFileName;
                //check for the sync file...
                if (File.Exists(path))
                {
                    //if it's the same machine name as this..
                    string content = File.ReadAllText(path);
                    if (content != Environment.MachineName)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static long SyncState
        {
            get
            {

                long ret = -1;
                string path = syncStateFileName;
                if (File.Exists(path))
                {
                    string content = File.ReadAllText(path);
                    long.TryParse(content, out ret);
                }
                return ret;

            }

            set
            {
                string path = syncStateFileName;

                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, value.ToString());

            }
        }


        /// <summary>
        /// If a Clear Cache Request has been recieved.
        /// </summary>
        public static bool IsClearCacheRequested
        {
            get { return SyncThread._isClearCacheRequested; }
            set { SyncThread._isClearCacheRequested = value; }
        }


        /// <summary>
        /// The application identity within the HttpContext that we will impersonate throughout the sync thread.
        /// </summary>
        //public static System.Security.Principal.WindowsIdentity ApplicationIdentity
        //{
        //	get { return SyncThread._applicationIdentity; }
        //	set { SyncThread._applicationIdentity = value; }
        //}


        /// <summary>
        /// Method used to kick off the sync thread.  Typically this is called from the ContentServer.
        /// </summary>
        /// <param name="publishRequest"></param>
        /// <param name="clearAllCache"></param>
        internal static void QueueSyncThread(AgilityPublishRequest publishRequest, bool clearAllCache)
        {
            lock (lockObj)
            {

                if (IsSyncInProgressOnOtherMachine)
                {
                    WebTrace.WriteVerboseLine("Sync Happening On Other Machine - ignoring sync trigger.");
                    return;
                }

                if (AgilityContext.ContentAccessor != null)
                {
                    WebTrace.WriteVerboseLine("Sync Thread: InitiateSyncProcess.");

                    if (!AgilityContext.ContentAccessor.InitiateSyncProcess(publishRequest.WebsiteName))
                    {
                        WebTrace.WriteInfoLine("Sync Thread: The sync is already running.");
                    }
                }

                //if (ApplicationIdentity == null) 
                //{
                //	//get the current windows identity
                //	ApplicationIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
                //}

                string cPath = Current.Settings.ContentCacheFilePath;
                string logPath = Current.Settings.Trace.LogFilePath;

                IsClearCacheRequested = clearAllCache;

                if (_workerThread == null)
                {


                    WebTrace.WriteInfoLine("Sync Thread: Starting worker thread...");
                    if (_workerThread != null)
                    {
                        WebTrace.WriteInfoLine("Sync Thread: Old worker thread state: " + _workerThread.ThreadState);
                    }

                    _workerThread = null;

                    ParameterizedThreadStart ts = new ParameterizedThreadStart(RunSyncThread);
                    _workerThread = new Thread(ts);
                    _workerThread.Priority = ThreadPriority.BelowNormal;
                    _workerThread.Start(publishRequest);
                }
                else
                {
                    IsThreadQueued = true;
                }
            }
        }


        /// <summary>
        /// The Method that runs as part or the Sync Thread.
        /// </summary>
        /// <param name="objPublishRequest"></param>
        private static void RunSyncThread(object objPublishRequest)
        {

            AgilityContext.HttpContext = null;

            bool debugSync = Current.Settings.DebugSync;

            IsSyncInProgress = true;

            AgilityPublishRequest publishRequest = objPublishRequest as AgilityPublishRequest;

            //TODO: ensure impersonation works in SyncThread
            //WindowsImpersonationContext wi = ApplicationIdentity.Impersonate();

            if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Start");

            string websiteDomain = publishRequest.WebsiteDomain;
            string websiteName = publishRequest.WebsiteName;

            AgilityWebsiteAuthorization auth = BaseCache.GetAgilityWebsiteAuthorization();
            auth.WebsiteName = websiteName;
            auth.WebsiteDomain = websiteDomain;

            if (string.IsNullOrEmpty(auth.WebsiteDomain) && AgilityContext.Domain != null)
            {
                auth.WebsiteDomain = AgilityContext.Domain.ID.ToString();
            }

            //consume and reset the IsClearCacheRequested flag
            bool clearCacheFiles = IsClearCacheRequested;
            IsClearCacheRequested = false;

            //keep a running list of item statuses
            List<AgilityItemStatus> itemStatusAry = new List<AgilityItemStatus>(10);
            List<AgilityItemStatus> itemStatusAryComplete = new List<AgilityItemStatus>(100);


            try
            {
                //access the content server
                AgilityContentServerClient client = BaseCache.GetAgilityServerClient();



                //determine whether we are doing a full sync, or only a "delta" sync
                bool performFullSync = false;

                if (clearCacheFiles)
                {
                    //do a full sync if we have no domain passed in, or no domain object locally.
                    if (debugSync) WebTrace.WriteWarningLine("Doing full sync...");
                    performFullSync = true;
                }

                if (debugSync) WebTrace.WriteWarningLine("Getting pending items...");

                if (AgilityContext.ContentAccessor != null)
                {
                    lock (lockObj)
                    {
                        AgilityContext.ContentAccessor.UnqueueSyncProcess(AgilityContext.WebsiteName);
                    }
                }


                //get the list of AgilityItems pending publish for this domain

                int itemOffset = 0;

                var loopRes = client.GetPendingItemsLoopAsync(new GetPendingItemsLoopRequest()
                {
                    auth = auth,
                    performFullSync = performFullSync,
                    recordOffset = itemOffset
                }).Result;

                AgilityItem[] items = loopRes.GetPendingItemsLoopResult;
                int remainingItemCount = loopRes.remainingItemCount;

                if (items == null)
                {
                    //if we don't even get an empty list from the server, kick out.
                    WebTrace.WriteWarningLine("The pending item list was not returned from the server. Exiting sync thread.");
                    IsSyncInProgress = false;
                    return;
                }

                //check if Eastern Standard Time exists, otherwise we are in linux
                TimeZoneInfo eastTimeZone = TimeZoneInfo.GetSystemTimeZones().Any(x => x.Id == "Eastern Standard Time") ?
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time") :
                    TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

                AgilityItemStatus domainStatus = null;

                Dictionary<int, List<string>> contentRefIndex = null;

                //loop until we get all the pending items...
                while (items.Length > 0)
                {

                    if (debugSync) WebTrace.WriteWarningLine(string.Format("Got {0} pending items, {1} remaing.", items.Length, remainingItemCount));

                    itemOffset += items.Length;



                    #region *** loop each item and process it it ***
                    for (int itemCnt = 0; itemCnt < items.Length; itemCnt++)
                    {
                        AgilityItem item = items[itemCnt];

                        if (item is AgilityDomainConfiguration)
                        {
                            auth.WebsiteDomain = ((AgilityDomainConfiguration)item).ID.ToString();
                        }



                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Sync Item: {0}, ID={1}, 2nd={2}, Deleted:{3}", item.GetType().Name, item.ID, item.HasSecondaryData, item.IsDeleted));



                        //track the status for this item
                        AgilityItemStatus status = new AgilityItemStatus();
                        status.ItemID = item.ID;

                        status.ItemType = item.GetType().Name;
                        status.LanguageCode = item.LanguageCode;

                        try
                        {

                            //get the filepath for the item (from persistent cache)
                            string contentCacheKey = BaseCache.GetCacheKey(item);
                            string filepath = BaseCache.GetFilePathForItem(item, websiteName, transientPath: false);


                            //process the other instance first...
                            if (AgilityContext.ContentAccessor != null)
                            {
                                if (!AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(item, contentCacheKey, auth.WebsiteDomain, clearCacheFiles))
                                {
                                    throw new ApplicationException(string.Format("Could not sync item {0} on other instances.", contentCacheKey));
                                }
                            }

                            if (item.IsDeleted)
                            {
                                //deleted state (delete the file associated with the item, and the cache will clear out automatically		
                                #region *** DELETED ITEM ****
                                if (AgilityContext.ContentAccessor != null)
                                {
                                    //BLOB STORAGE
                                    if (!AgilityContext.ContentAccessor.DeleteContentCacheBlob(contentCacheKey))
                                    {
                                        //TODO: handle renamed blobs/fields in the ContentAccessor...
                                    }

                                }
                                else
                                {

                                    //FILE STORAGE
                                    if (Directory.Exists(filepath))
                                    {
                                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Deleting Cache Item (Folder): {0}, ID={1}, filepath: {2}", item.GetType().Name, item.ID, filepath));
                                        Directory.Delete(filepath, true);
                                    }

                                    if (File.Exists(filepath))
                                    {
                                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Deleting Cache Item: {0}, ID={1}, filepath: {2}", item.GetType().Name, item.ID, filepath));
                                        BaseCache.DeleteFile(filepath);
                                    }
                                    else if (item is AgilityContent)
                                    {
                                        //if the reference name has changed, loop ALL the agility content files and find the matching ID
                                        if (!string.IsNullOrEmpty(filepath))
                                        {
                                            string dirPath = Path.GetDirectoryName(filepath);

                                            //build the content ref index 

                                            if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                                            {

                                                if (contentRefIndex == null)
                                                {

                                                    string[] contentFiles = Directory.GetFiles(Path.GetDirectoryName(filepath), "AgilityContent_*");
                                                    contentRefIndex = new Dictionary<int, List<string>>(contentFiles.Length);
                                                    foreach (string contentFile in contentFiles)
                                                    {
                                                        //find the content with the old name and delete it.
                                                        AgilityContent ac = BaseCache.ReadFile<AgilityContent>(contentFile);
                                                        if (ac != null)
                                                        {
                                                            List<string> lst = null;
                                                            if (!contentRefIndex.TryGetValue(ac.ID, out lst))
                                                            {
                                                                lst = new List<string>();
                                                                contentRefIndex[ac.ID] = lst;
                                                            }
                                                            lst.Add(contentFile);
                                                        }
                                                    }
                                                }

                                                List<string> contentFilesToDelete = null;
                                                if (contentRefIndex.TryGetValue(item.ID, out contentFilesToDelete))
                                                {
                                                    foreach (string contentFileToDelete in contentFilesToDelete)
                                                    {
                                                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Deleting Content View Cache: {0}, ID={1}, filepath: {2}", item.GetType().Name, item.ID, contentFileToDelete));
                                                        BaseCache.DeleteFile(contentFileToDelete);
                                                    }

                                                }
                                            }
                                        }
                                    }

                                }
                                #endregion
                            }
                            else
                            {
                                //	secondary data (files or DataSet)
                                if (item.HasSecondaryData)
                                {
                                    #region *** SECONDARY DATA ACCESS ***
                                    //get the secondary data

                                    if (item is AgilityContent)
                                    {
                                        //get the existing content object from the file system
                                        object objExisting = null;

                                        if (!clearCacheFiles)
                                        {
                                            //read the item ONLY if we are not going to clear the cache..
                                            if (AgilityContext.ContentAccessor != null)
                                            {
                                                //ContentAccessor
                                                objExisting = AgilityContext.ContentAccessor.ReadContentCacheFile(contentCacheKey);
                                            }
                                            else
                                            {
                                                //FILE STORAGE
                                                objExisting = BaseCache.ReadFile<AgilityContent>(filepath);
                                            }
                                        }

                                        AgilityContent existingItem = null;
                                        DateTime accessDate = DateTime.MinValue;
                                        if (objExisting is AgilityContent)
                                        {
                                            existingItem = (AgilityContent)objExisting;

                                            //special case for AgilityContent with no Attachment Table...
                                            if (existingItem.DataSet != null && !existingItem.DataSet.Tables.Contains("AttachmentThumbnails"))
                                            {
                                                //force a full pull get if the Attachment Thumbnails have not been initialized
                                                existingItem = null;
                                                objExisting = null;
                                            }
                                            else
                                            {
                                                //only get the last access date from the actual content...


                                                if (existingItem.DataSet != null && existingItem.DataSet.Tables.Contains("ContentItems"))
                                                {
                                                    DataTable dtContentItems = existingItem.DataSet.Tables["ContentItems"];
                                                    if (dtContentItems.Rows.Count == 1)
                                                    {
                                                        //only a single item in the list
                                                        DataRow row = dtContentItems.Rows[0];

														DateTime lastModified = DateTime.MinValue;
														object o = row["CreatedDate"];
														if (o is DateTime)
														{
															lastModified = (DateTime)o;
														}
														else
														{
															DateTime.TryParse($"{o}", out lastModified);
														}

									
                                                        accessDate = lastModified;
                                                    }
                                                    else if (dtContentItems.Rows.Count > 1)
                                                    {
                                                        //multiple items in the list, get the access date from the most recent one
                                                        DataRow row = dtContentItems.Select("", "versionID DESC")[0];

														DateTime lastModified = DateTime.MinValue;
														object o = row["CreatedDate"];
														if (o is DateTime)
														{
															lastModified = (DateTime)o;
														}
														else
														{
															DateTime.TryParse($"{o}", out lastModified);
														}

                                                        accessDate = lastModified;
                                                    }
                                                }
                                            }
                                        }


                                        if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                                        {
                                            //HACK: convert the access date to EST
                                            accessDate = TimeZoneInfo.ConvertTime(accessDate, eastTimeZone);
                                        }

                                        string refName = ((AgilityContent)item).ReferenceName;

                                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Getting delta: {0}, lang={1}, date:{2}", refName, item.LanguageCode, accessDate));
                                        //get the dataset for the content

                                        var deltaItemResult = client.SelectContentItemsDeltaAsync(auth, refName, (int)Mode.Live, item.LanguageCode, accessDate).Result;
                                        AgilityContent deltaItem = deltaItemResult.SelectContentItemsDeltaResult;

                                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Got delta: {0}, lang={1}, date:{2}", refName, item.LanguageCode, accessDate));

                                        //handle the updated attachments
                                        if (deltaItem != null && deltaItem.DataSet.Tables.Contains("Attachments"))
                                        {
                                            //loop all the new/updated attachments
                                            #region *** ATTACHMENTS ***
                                            foreach (DataRow row in deltaItem.DataSet.Tables["Attachments"].Rows)
                                            {
                                                string filename = row["FileName"] as string;
                                                string guid = row["guid"] as string;
                                                string fileSize = $"{row["FileSize"]}";

                                                if (!string.IsNullOrEmpty(filename)
                                                    && (filename.StartsWith("/")
                                                    || filename.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)))
                                                {
                                                    continue;
                                                }


                                                if (debugSync) WebTrace.WriteWarningLine(string.Format("Syncing attachment: {0}, size: {1}.", filename, fileSize));

                                                //get a copy of the attachment item											
                                                AgilityItemKey itemKey = new AgilityItemKey();
                                                itemKey.ItemType = typeof(Objects.Attachment).Name;
                                                itemKey.Key = guid + filename;

                                                if (AgilityContext.ContentAccessor != null)
                                                {
                                                    //BLOB STORAGE
                                                    string attachmentCacheKey = itemKey.Key as string;

                                                    if (clearCacheFiles || !AgilityContext.ContentAccessor.DoesBlobExist(attachmentCacheKey))
                                                    {
                                                        //download the bytes for the attachment

                                                        var bytes = client.SelectAttachmentDataAsync(auth, guid).Result.SelectAttachmentDataResult;
                                                        using (MemoryStream attStream = new MemoryStream(bytes))
                                                        {
                                                            AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(attStream, attachmentCacheKey);

                                                        }
                                                    }

                                                }
                                                else
                                                {
                                                    //FILE STORAGE

                                                    //resolve the attachment filename
                                                    string attachmentFilePath = BaseCache.GetFilePathForItemKey(itemKey, websiteName, transientPath: false);

                                                    //check to see if it is already there or not 
                                                    //unless we are doing a clear all cache
                                                    if (clearCacheFiles || !File.Exists(attachmentFilePath))
                                                    {
                                                        //download the bytes for the attachment
                                                        var bytes = client.SelectAttachmentDataAsync(auth, guid).Result.SelectAttachmentDataResult;
                                                        using (MemoryStream attStream = new MemoryStream(bytes))
                                                        {
                                                            //stream the file into the correct location (temp path)												
                                                            BaseCache.WriteFile(attStream, attachmentFilePath.Replace("/Live/", "/Temp/"), DateTime.MinValue);
                                                        }
                                                    }

                                                }
                                            }
                                            #endregion
                                        }

                                        if (existingItem == null)
                                        {

                                            if (deltaItem != null && deltaItem.DataSet != null)
                                            {
                                                if (debugSync) WebTrace.WriteWarningLine(string.Format("No delta for content {0}", deltaItem.ID));
                                                // deltaItem.DataSet.RemotingFormat = SerializationFormat.Binary;
                                                deltaItem.DataSet.AcceptChanges();
                                            }
                                            item = deltaItem;
                                        }
                                        else
                                        {
                                            //merge delta on content items

                                            if (deltaItem != null)
                                            {
                                                if (debugSync) WebTrace.WriteWarningLine(string.Format("Merging delta for content {0} - delta found for access date {1}...", deltaItem.ID, accessDate));
                                                item = BaseCache.MergeContent(existingItem, deltaItem);
                                            }
                                            else
                                            {
                                                if (debugSync) WebTrace.WriteWarningLine(string.Format("Merging delta for content {0} - no delta found for access date {1}...", existingItem.ID, accessDate));
                                                item = existingItem;
                                            }

                                        }

                                    }
                                    else if (item is AgilityDocument)
                                    {

                                        AgilityDocument document = (AgilityDocument)item;

                                        if (debugSync) WebTrace.WriteWarningLine("Syncing document: " + contentCacheKey + " - " + filepath);

                                        DateTime lastModDate = DateTime.MinValue;
                                        if (!clearCacheFiles)
                                        {
                                            if (AgilityContext.ContentAccessor != null)
                                            {
                                                //BLOB STORAGE
                                                lastModDate = AgilityContext.ContentAccessor.GetBlobDate(contentCacheKey);
                                            }
                                            else
                                            {
                                                //FILE STORAGE
                                                if (File.Exists(filepath))
                                                {
                                                    lastModDate = File.GetLastWriteTime(filepath);
                                                }
                                            }
                                        }
                                        if (debugSync) WebTrace.WriteWarningLine("document last mod date: " + lastModDate);

                                        //compare the the last modified time from the the filesystem file to the item
                                        //only sync the new file is clear all cache is requested, or the file on the server is newer
                                        if (clearCacheFiles || lastModDate < item.LastAccessDate)
                                        {


                                            //download the new stream
                                            var bytes = client.SelectDocumentDataAsync(auth, ((AgilityDocument)item).FilePath).Result.SelectDocumentDataResult;
                                            MemoryStream docStream = new MemoryStream(bytes);

                                            if (AgilityContext.ContentAccessor != null)
                                            {
                                                //BLOB STORAGE
                                                AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(docStream, contentCacheKey, item.LastAccessDate);
                                            }

                                            //FILE STORAGE
                                            //write out the stream to the filesystem (temp folder)
                                            BaseCache.WriteFile(docStream, filepath.Replace("/Live/", "/Temp/"), item.LastAccessDate);

                                        }
                                    }
                                    else if (item is AgilityTagList)
                                    {
                                        //get the existing content object from the file system
                                        object objExisting = null;

                                        if (!clearCacheFiles)
                                        {
                                            //read the item ONLY if we are not going to clear the cache..
                                            if (AgilityContext.ContentAccessor != null)
                                            {
                                                //BLOB STORAGE
                                                objExisting = AgilityContext.ContentAccessor.ReadContentCacheBlobSource(contentCacheKey);
                                            }
                                            else
                                            {

                                                //FILE STORAGE
                                                objExisting = BaseCache.ReadFile<AgilityTagList>(filepath);
                                            }
                                        }

                                        AgilityTagList existingItem = null;
                                        DateTime accessDate = DateTime.MinValue;
                                        if (objExisting is AgilityTagList)
                                        {
                                            existingItem = (AgilityTagList)objExisting;
                                            accessDate = existingItem.LastAccessDate;
                                        }

                                        //get the dataset for the content
                                        if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                                        {
                                            //HACK: convert the access date to EST
                                            accessDate = TimeZoneInfo.ConvertTime(item.LastAccessDate, eastTimeZone);
                                        }

                                        AgilityTagList tagList = client.SelectTagsAsync(auth, item.LanguageCode, accessDate).Result.SelectTagsResult;
                                        if (tagList != null)
                                        {
                                            if (existingItem == null)
                                            {
                                                existingItem = tagList;
                                            }

                                            existingItem.LastAccessDate = tagList.LastAccessDate;
                                            if (tagList.DSTags != null)
                                            {
                                                existingItem.DSTags = tagList.DSTags;
                                            }
                                            if (existingItem.DSTags != null)
                                            {
                                                //existingItem.DSTags.RemotingFormat = SerializationFormat.Binary;
                                            }


                                            item = existingItem;
                                        }


                                    }
                                    else if (item is AgilityAssetMediaGroup)
                                    {
                                        //get the existing content object from the file system
                                        object objExisting = null;

                                        if (!clearCacheFiles)
                                        {
                                            //read the item ONLY if we are not going to clear the cache..
                                            if (AgilityContext.ContentAccessor != null)
                                            {
                                                //BLOB STORAGE
                                                objExisting = AgilityContext.ContentAccessor.ReadContentCacheBlobSource(contentCacheKey);
                                            }
                                            else
                                            {

                                                //FILE STORAGE
                                                objExisting = BaseCache.ReadFile<AgilityAssetMediaGroup>(filepath);
                                            }
                                        }

                                        AgilityAssetMediaGroup existingItem = null;
                                        DateTime accessDate = DateTime.MinValue;
                                        if (objExisting is AgilityAssetMediaGroup)
                                        {
                                            existingItem = (AgilityAssetMediaGroup)objExisting;
                                            accessDate = existingItem.LastAccessDate;
                                        }

                                        //get the dataset for the content
                                        if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                                        {
                                            //HACK: convert the access date to EST
                                            accessDate = TimeZoneInfo.ConvertTime(item.LastAccessDate, eastTimeZone);
                                        }

                                        AgilityAssetMediaGroup group = client.SelectAssetMediaGroupDeltaAsync(auth, item.ID, accessDate).Result.SelectAssetMediaGroupDeltaResult;
                                        if (group != null)
                                        {
                                            existingItem = group;
                                            item = group;
                                        }
                                    }

                                    #endregion
                                }


                                //serialize the item to the filesystem and put it in a temp location

                                if (!(item is AgilityDocument) && item != null)
                                {

                                    if (debugSync) WebTrace.WriteWarningLine(string.Format("Writing Item: {0}, ID={1}", item.GetType().Name, item.ID));

                                    if (AgilityContext.ContentAccessor != null)
                                    {
                                        AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(item, contentCacheKey);
                                    }
                                    else
                                    {
                                        BaseCache.WriteCacheObjectToTemp(item, websiteName);
                                    }
                                }

                                if (item is AgilityPage)
                                {
                                    //if this is a dynamic page with a content reference name specified, update the index
                                    //create/update the dynamic page index with this page id and dynamic page list reference name

                                    AgilityPage page = item as AgilityPage;

                                    if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName)
                                        || !string.IsNullOrEmpty(page.DynamicPageParentFieldName))
                                    {
                                        if (!string.IsNullOrEmpty(page.DynamicPageContentViewReferenceName))
                                        {
                                            BaseCache.UpdateDynamicPageIndex(page.ID, page.DynamicPageContentViewReferenceName);
                                        }

                                        //update the Dynamic Page Formula Index that use this page...
                                        BaseCache.ClearDynamicDynamicPageFormulaIndex(page);
                                    }
                                }
                            }

                            //set the state on the itemState object
                            if (item is AgilityDomainConfiguration)
                            {
                                //if the item is a domain configuration, set it to "InProgress" and keep a handle on the status object
                                domainStatus = new AgilityItemStatus();
                                domainStatus.ItemID = status.ItemID;
                                domainStatus.LanguageCode = status.LanguageCode;
                                domainStatus.ItemType = status.ItemType;

                                status.PublishState = (int)PublishState.InProgress;
                                status.PublishStateSpecified = true;
                            }
                            else
                            {
                                //all other item types - set in progress
                                status.PublishState = (int)PublishState.InProgress;
                                status.PublishStateSpecified = true;

                                itemStatusAryComplete.Add(status);
                            }

                        }
                        catch (System.Exception ex)
                        {
                            WebTrace.WriteWarningLine("The item could not be synchronized: " + ex);
                            status.PublishState = (int)PublishState.Failed;
                            status.PublishStateSpecified = true;
                            status.ErrorMessage = ex.Message;
                        }

                        //notify server that the item is processed (if there are 10 or more statuses to send...)
                        itemStatusAry.Add(status);

                        if (itemStatusAry.Count >= 10)
                        {
                            var res = client.SetItemStatusesAsync(auth, itemStatusAry.ToArray()).Result;
                            itemStatusAry = new List<AgilityItemStatus>(10);
                        }

                    }
                    #endregion


                    //if there are any itemstatuses we haven't sent to the server, send them now...
                    if (itemStatusAry.Count > 0)
                    {
                        var res = client.SetItemStatusesAsync(auth, itemStatusAry.ToArray()).Result;
                        itemStatusAry.Clear();
                    }

                    //get the next set of pending items
                    if (remainingItemCount > 0)
                    {

                        var loopResInner = client.GetPendingItemsLoopAsync(new GetPendingItemsLoopRequest()
                        {
                            auth = auth,
                            performFullSync = false,
                            recordOffset = itemOffset
                        }).Result;


                        items = loopResInner.GetPendingItemsLoopResult;
                        remainingItemCount = loopResInner.remainingItemCount;

                    }
                    else
                    {
                        items = new AgilityItem[0];
                    }

                }

                //if there are any itemstatuses we haven't sent to the server, send them now...
                if (itemStatusAry.Count > 0)
                {
                    var res = client.SetItemStatusesAsync(auth, itemStatusAry.ToArray()).Result;
                }

                #region *** Sync the channels list ***

                if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Downloading Channels.");

                //download the channels
                AgilityItemKey channelKey = new AgilityItemKey();
                channelKey.Key = BaseCache.ITEMKEY_CHANNELS;
                channelKey.ItemType = typeof(AgilityDigitalChannelList).Name;

                string channelsCacheKey = BaseCache.GetCacheKey(channelKey);
                string tempChannelsFilePath = BaseCache.GetTempFilePathForItem(channelKey, websiteName);
                string channelsFilePath = BaseCache.GetFilePathForItemKey(channelKey, websiteName, transientPath: false);

                AgilityDigitalChannelList lstChannels = null;


                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    lstChannels = AgilityContext.ContentAccessor.ReadContentCacheFile(channelsCacheKey) as AgilityDigitalChannelList;
                }
                else
                {
                    //FILE STORAGE
                    lstChannels = BaseCache.ReadFile<AgilityDigitalChannelList>(channelsFilePath);
                }


                DateTime channelLastAccessDate = DateTime.MinValue;
                if (lstChannels != null)
                {
                    channelLastAccessDate = lstChannels.LastAccessDate;

                    if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                    {
                        //HACK: convert the access date to EST		
                        channelLastAccessDate = TimeZoneInfo.ConvertTime(channelLastAccessDate, eastTimeZone);
                    }
                }

                AgilityDigitalChannelList lstChannelsDownload = client.GetDigitalChannelsAsync(auth, channelLastAccessDate).Result.GetDigitalChannelsResult;
                if (lstChannelsDownload != null)
                {
                    //only write the new file if we didn't have it written before or if the new one has the channels list in it...

                    //write the file...
                    if (AgilityContext.ContentAccessor != null)
                    {
                        //BLOB STORAGE
                        AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(lstChannelsDownload, channelsCacheKey);
                        AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, channelsCacheKey, auth.WebsiteDomain, clearCacheFiles);
                    }
                    else
                    {
                        //FILE STORAGE
                        BaseCache.WriteFile(lstChannelsDownload, tempChannelsFilePath);
                    }
                }
                #endregion

                #region *** Download the indexes on every sync ***

                if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Downloading indexes.");

                //download the content definition index and do a delta on it
                AgilityItemKey key = new AgilityItemKey();
                key.Key = BaseCache.ITEMKEY_CONTENTDEFINDEX;
                key.ItemType = typeof(NameIndex).Name;


                string indexCacheKey = BaseCache.GetCacheKey(key);

                string tempIndexFilePath = BaseCache.GetTempFilePathForItem(key, websiteName);
                string indexFilePath = BaseCache.GetFilePathForItemKey(key, websiteName, transientPath: false);

                NameIndex contentDefIndex = null;

                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    contentDefIndex = AgilityContext.ContentAccessor.ReadContentCacheFile(indexCacheKey) as NameIndex;
                }
                else
                {
                    //FILE STORAGE
                    contentDefIndex = BaseCache.ReadFile<NameIndex>(indexFilePath);
                }


                DateTime lastAccessDate = DateTime.MinValue;
                if (contentDefIndex != null)
                {
                    lastAccessDate = contentDefIndex.LastAccessDate;

                    if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                    {
                        //HACK: convert the access date to EST		
                        lastAccessDate = TimeZoneInfo.ConvertTime(lastAccessDate, eastTimeZone);
                    }
                }

                NameIndex contentDefIndexDownload = client.GetContentDefinitionIndexAsync(auth, lastAccessDate).Result.GetContentDefinitionIndexResult;
                if (contentDefIndexDownload != null)
                {
                    //only write the new file if we didn't have it written before or if the new one has the index in it...
                    if (contentDefIndexDownload.ID != -1)
                    {
                        //write the file...
                        if (AgilityContext.ContentAccessor != null)
                        {
                            //BLOB STORAGE
                            AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(contentDefIndexDownload, indexCacheKey);
                            AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, indexCacheKey, auth.WebsiteDomain, clearCacheFiles);
                        }
                        else
                        {
                            //FILE STORAGE
                            BaseCache.WriteFile(contentDefIndexDownload, tempIndexFilePath);
                        }

                        if (contentDefIndexDownload.Index != null)
                        {
                            //sync all of the content defs deltas...
                            List<AgilityItemKey> lstDefKeys = new List<AgilityItemKey>();
                            foreach (IndexItem indexItem in contentDefIndexDownload.Index)
                            {
                                int defID = indexItem.ID;

                                AgilityItemKey moduleKey = new AgilityItemKey();
                                moduleKey.Key = defID;
                                moduleKey.ItemType = typeof(AgilityModule).Name;

                                string defFilePath = BaseCache.GetFilePathForItemKey(moduleKey, websiteName, transientPath: false);
                                string defCacheKey = BaseCache.GetCacheKey(moduleKey);

                                AgilityModule def = null;

                                if (AgilityContext.ContentAccessor != null)
                                {
                                    //BLOB STORAGE
                                    def = AgilityContext.ContentAccessor.ReadContentCacheFile(defCacheKey) as AgilityModule;
                                }
                                else
                                {
                                    //FILE STORAGE
                                    def = BaseCache.ReadFile<AgilityModule>(defFilePath);
                                }

                                //check if the def itself was modified since we downloaded...
                                if (def == null || def.LastAccessDate < indexItem.LastModified || string.IsNullOrEmpty(def.XmlSchema))
                                {
                                    //get the updated definition...
                                    def = client.GetContentDefinitionAsync(auth, defID).Result.GetContentDefinitionResult;

                                    //write it to the temp folder so it will be copied over at the end of the sync...
                                    if (AgilityContext.ContentAccessor != null)
                                    {
                                        //BLOB STORAGE
                                        AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(def, defCacheKey);
                                        AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, defCacheKey, auth.WebsiteDomain, clearCacheFiles);
                                    }
                                    else
                                    {
                                        //FILE STORAGE
                                        BaseCache.WriteCacheObjectToTemp(def, websiteName);
                                    }
                                }
                            }
                        }
                    }
                }

                //download the shared content index and do a delta on it
                key = new AgilityItemKey();
                key.Key = BaseCache.ITEMKEY_SHAREDCONTENTINDEX;
                key.ItemType = typeof(NameIndex).Name;

                indexCacheKey = BaseCache.GetCacheKey(key);

                tempIndexFilePath = BaseCache.GetTempFilePathForItem(key, websiteName);
                indexFilePath = BaseCache.GetFilePathForItemKey(key, websiteName, transientPath: false);

                NameIndex sharedContentIndex = null;
                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    sharedContentIndex = AgilityContext.ContentAccessor.ReadContentCacheFile(indexCacheKey) as NameIndex;

                }
                else
                {
                    //FILE STORAGE
                    sharedContentIndex = BaseCache.ReadFile<NameIndex>(indexFilePath);
                }
                lastAccessDate = DateTime.MinValue;
                if (sharedContentIndex != null)
                {
                    lastAccessDate = sharedContentIndex.LastAccessDate;

                    if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                    {
                        //HACK: convert the access date to EST		
                        lastAccessDate = TimeZoneInfo.ConvertTime(lastAccessDate, eastTimeZone);
                    }
                }

                NameIndex sharedContentIndexDownload = client.GetSharedContentIndexAsync(auth, lastAccessDate).Result.GetSharedContentIndexResult;
                if (sharedContentIndexDownload != null)
                {
                    if (sharedContentIndexDownload.ID != -1)
                    {
                        //only write the new file if we didn't have it written before or if the new one has the index in it...
                        if (AgilityContext.ContentAccessor != null)
                        {
                            //BLOB STORAGE
                            AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(sharedContentIndexDownload, indexCacheKey);
                            AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, indexCacheKey, auth.WebsiteDomain, clearCacheFiles);
                        }
                        else
                        {
                            //FILE STORAGE
                            BaseCache.WriteFile(sharedContentIndexDownload, tempIndexFilePath);
                        }
                    }
                }

                #endregion

                #region *** Sync the Redirections ***
                if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Downloading Redirections.");

                //download the content definition index and do a delta on it
                key = new AgilityItemKey();
                key.Key = BaseCache.ITEMKEY_URLREDIRECTIONS;
                key.ItemType = typeof(AgilityUrlRedirectionList).Name;

                string cacheKey = BaseCache.GetCacheKey(key);
                string tempFilePath = BaseCache.GetTempFilePathForItem(key, websiteName);
                string filePath = BaseCache.GetFilePathForItemKey(key, websiteName, transientPath: false);

                AgilityUrlRedirectionList urlRedirectionList = null;

                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    urlRedirectionList = AgilityContext.ContentAccessor.ReadContentCacheFile(cacheKey) as AgilityUrlRedirectionList;
                }
                else
                {
                    //FILE STORAGE
                    urlRedirectionList = BaseCache.ReadFile<AgilityUrlRedirectionList>(filePath);
                }

                lastAccessDate = DateTime.MinValue;
                if (urlRedirectionList != null)
                {
                    lastAccessDate = urlRedirectionList.LastAccessDate;

                    if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                    {
                        //HACK: convert the access date to EST		
                        lastAccessDate = TimeZoneInfo.ConvertTime(lastAccessDate, eastTimeZone);
                    }
                }

                var redirectRes = client.SelectUrlRedirectionsDeltaAsync(auth, lastAccessDate).Result;
                AgilityUrlRedirectionList urlRedirectionListDelta = redirectRes.SelectUrlRedirectionsDeltaResult;

                if (urlRedirectionListDelta != null)
                {
                    if (urlRedirectionListDelta.ID > 0)
                    {
                        urlRedirectionList = (AgilityUrlRedirectionList)BaseCache.MergeDeltaItems(urlRedirectionList, urlRedirectionListDelta, websiteName);

                        //write the file...
                        if (AgilityContext.ContentAccessor != null)
                        {
                            //BLOB STORAGE
                            AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(urlRedirectionList, cacheKey);
                            AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, cacheKey, auth.WebsiteDomain, clearCacheFiles);
                        }
                        else
                        {
                            //FILE STORAGE
                            BaseCache.WriteFile(urlRedirectionList, tempFilePath);
                        }
                    }
                }
                #endregion

                #region *** Sync the Experiments ***

                if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Downloading Experiments.");

                //download the experiments and do a delta on the list
                key = new AgilityItemKey();
                key.Key = BaseCache.ITEMKEY_EXPERIMENTS;
                key.ItemType = typeof(AgilityExperimentListing).Name;

                string experimentCacheKey = BaseCache.GetCacheKey(key);
                string experimentTempFilePath = BaseCache.GetTempFilePathForItem(key, websiteName);
                string experimentfilePath = BaseCache.GetFilePathForItemKey(key, websiteName, transientPath: false);

                AgilityExperimentListing experimentList = null;

                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    experimentList = AgilityContext.ContentAccessor.ReadContentCacheFile(experimentCacheKey) as AgilityExperimentListing;
                }
                else
                {
                    //FILE STORAGE
                    experimentList = BaseCache.ReadFile<AgilityExperimentListing>(experimentfilePath);
                }

                lastAccessDate = DateTime.MinValue;
                if (experimentList != null)
                {
                    lastAccessDate = experimentList.LastAccessDate;

                    if (TimeZoneInfo.Local.Id != eastTimeZone.Id)
                    {
                        //HACK: convert the access date to EST		
                        lastAccessDate = TimeZoneInfo.ConvertTime(lastAccessDate, eastTimeZone);
                    }
                }
                else
                {
                    //init the list if we have to
                    experimentList = new AgilityExperimentListing();
                    experimentList.ID = 1;
                }

                var experimentListingDelta = ServerAPI.GetExperimentsDelta(lastAccessDate);

                if (experimentListingDelta != null && experimentListingDelta.Items != null && experimentCacheKey.Length > 0)
                {

                    if (debugSync) WebTrace.WriteWarningLine(string.Format("Sync Thread: {0} Experiments since {1}", experimentListingDelta.Items.Length, lastAccessDate));

                    experimentList.Merge(experimentListingDelta);

                    //write the file...
                    if (AgilityContext.ContentAccessor != null)
                    {
                        //BLOB STORAGE
                        AgilityContext.ContentAccessor.WriteContentCacheBlobToTemp(experimentList, experimentCacheKey);
                        AgilityContext.ContentAccessor.ProcessItemOnOtherInstance(null, experimentCacheKey, auth.WebsiteDomain, clearCacheFiles);
                    }
                    else
                    {
                        if (debugSync) WebTrace.WriteWarningLine(string.Format("Sync Thread: Write experiment file {0}", experimentTempFilePath));
                        //FILE STORAGE
                        BaseCache.WriteFile(experimentList, experimentTempFilePath);
                    }

                }
                else
                {
                    if (debugSync) WebTrace.WriteWarningLine(string.Format("Sync Thread: No Experiments since {0}", lastAccessDate));
                }

                #endregion



                if (clearCacheFiles)
                {

                    if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Clearing cache files.");

                    //clear cache files
                    if (AgilityContext.ContentAccessor != null)
                    {
                        //BLOB STORAGE
                        AgilityContext.ContentAccessor.ClearCachedBlobs();
                    }
                    else
                    {
                        //FILE STORAGE
                        BaseCache.ClearCacheFiles(websiteName);
                    }
                }

                //move all files from "temp" folder to the actual folder
                if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Moving temp files.");
                StringBuilder sbMoveError = new StringBuilder();

                if (AgilityContext.ContentAccessor != null)
                {
                    //BLOB STORAGE
                    try
                    {
                        AgilityContext.ContentAccessor.CopyTempBlobsToLiveStorage(clearCacheFiles);
                    }
                    catch (Exception ex)
                    {
                        sbMoveError.Append(ex.ToString());
                    }
                }
                else
                {
                    //FILE STORAGE
                    StringBuilder sbTemp = new StringBuilder(Current.Settings.ContentCacheFilePath);
                    StringBuilder sbLive = new StringBuilder(Current.Settings.ContentCacheFilePath);

                    sbTemp.Append(websiteName).Append(Path.DirectorySeparatorChar);
                    sbLive.Append(websiteName).Append(Path.DirectorySeparatorChar);

                    sbTemp.Append("Temp");
                    sbLive.Append("Live").Append(Path.DirectorySeparatorChar);

                    string livePath = sbLive.ToString();

                    if (!Directory.Exists(livePath))
                    {
                        Directory.CreateDirectory(livePath);
                    }

                    if (!Directory.Exists(sbTemp.ToString()))
                    {
                        Directory.CreateDirectory(sbTemp.ToString());
                    }


                    //loop all files in the Temp folder and move to Live
                    int numCopyTries = 3;
                    string tmpFolder = sbTemp.ToString();
                    string[] tmpFiles = Directory.GetFiles(sbTemp.ToString(), "*", SearchOption.AllDirectories);
                    foreach (string tmpFilePath in tmpFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(tmpFilePath);
                            string folders = tmpFilePath.Substring(tmpFolder.Length + 1, tmpFilePath.LastIndexOf(fileName) - tmpFolder.Length - 1);

                            string liveFilePath = Path.Combine(livePath, folders);
                            if (!Directory.Exists(liveFilePath))
                            {
                                Directory.CreateDirectory(liveFilePath);
                            }

                            liveFilePath = Path.Combine(liveFilePath, fileName);

                            int currentTry = 1;
                            while (currentTry <= numCopyTries)
                            {
                                try
                                {

                                    if (currentTry < numCopyTries)
                                    {
                                        File.Delete(liveFilePath);
                                        File.Move(tmpFilePath, liveFilePath);
                                    }
                                    else
                                    {
                                        //last resort, try a copy
                                        File.Copy(tmpFilePath, liveFilePath, true);
                                    }

                                    //if we make it here, no need to retry
                                    break;
                                }
                                catch (System.IO.FileNotFoundException)
                                {
                                    //ignore file not found
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
                                        Agility.Web.Tracing.WebTrace.WriteInfoLine(string.Format("File moving file {0}, retrying. {1}", tmpFilePath, ex));
                                    }
                                }

                                //if we get here, we need to retry after 2 seconds
                                Thread.Sleep(2000);
                                currentTry++;
                            }

                        }
                        catch (Exception ex)
                        {
                            sbMoveError.Append(System.Environment.NewLine);
                            sbMoveError.Append("File move error:").Append(tmpFilePath); ;
                            sbMoveError.Append(System.Environment.NewLine);
                            sbMoveError.Append(ex);
                            sbMoveError.Append(System.Environment.NewLine);

                        }
                    }


                    //clear out the temp folder
                    try
                    {
                        Directory.Delete(tmpFolder, true);
                    }
                    catch (Exception ex)
                    {
                        sbMoveError.Append(System.Environment.NewLine);
                        sbMoveError.Append("Temp folder clear :").Append(tmpFolder); ;
                        sbMoveError.Append(System.Environment.NewLine);
                        sbMoveError.Append(ex);
                        sbMoveError.Append(System.Environment.NewLine);
                    }
                }

                if (sbMoveError.Length > 0)
                {
                    //ERRORS  
                    WebTrace.WriteException(new Exception(sbMoveError.ToString()));
                    if (domainStatus != null)
                    {
                        domainStatus.PublishState = (int)PublishState.Failed;
                        domainStatus.PublishStateSpecified = true;
                        domainStatus.ErrorMessage = "Errors occurred while copying files to the live site.  Please try again.";
                        var res = client.SetItemStatusesAsync(auth, new AgilityItemStatus[] { domainStatus }).Result;
                    }

                }
                else
                {

                    //SUCCESS
                    if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Temp files moved.");

                    ////set everything as "synced"
                    //var tmpLst = new List<AgilityItemStatus>(10);
                    //foreach (var statusItem in itemStatusAryComplete)
                    //{
                    //	statusItem.PublishState = (int)PublishState.Published;
                    //	statusItem.PublishStateSpecified = true;

                    //	tmpLst.Add(statusItem);
                    //	if (tmpLst.Count % 10 == 0)
                    //	{
                    //		client.SetItemStatuses(auth, tmpLst.ToArray());
                    //		tmpLst.Clear();
                    //	}
                    //}
                    //if (tmpLst.Count > 0)
                    //{
                    //	client.SetItemStatuses(auth, tmpLst.ToArray());	
                    //}

                    //set the DomainConfig for "Synchronized"
                    if (domainStatus != null)
                    {

                        domainStatus.PublishState = (int)PublishState.Synchronized;
                        domainStatus.PublishStateSpecified = true;

                        var res = client.SetItemStatusesAsync(auth, new AgilityItemStatus[] { domainStatus }).Result;
                    }

                    if (debugSync) WebTrace.WriteWarningLine("Sync Thread: Domain statuses set to sync.");

                }


            }
            catch (ThreadAbortException)
            {
                //ignore these errors, cause they are caused by iisreset, apppool cycle and other high level activities.		
                if (debugSync) WebTrace.WriteWarningLine("Thread has been aborted.");
            }
            catch (Exception ex)
            {
                WebTrace.WriteException(ex);
            }


            if (AgilityContext.ContentAccessor != null)
            {
                if (!AgilityContext.ContentAccessor.CompleteSyncProcess(AgilityContext.WebsiteName))
                {
                    WebTrace.WriteInfoLine("Sync Thread: The sync process has been queued and is being restarted.");
                    //run this thread again if neccessary
                    RunSyncThread(publishRequest);
                    return;
                }
            }

            //check if another publish is queued, 
            if (IsThreadQueued)
            {
                IsThreadQueued = false;
                WebTrace.WriteInfoLine("Sync Thread: The sync process has been queued and is being restarted.");

                //run this thread again if neccessary
                RunSyncThread(publishRequest);
                return;
            }

            _workerThread = null;
            IsSyncInProgress = false;

            SyncState = (long)(DateTime.Now - DateTime.UnixEpoch).TotalSeconds;

            WebTrace.WriteInfoLine("Sync Thread: End");
        }

    }
}
