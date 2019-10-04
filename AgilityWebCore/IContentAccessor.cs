using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agility.Web.AgilityContentServer;
using Agility.Web.Caching;

namespace Agility.Web
{
	public interface IContentAccessor
	{

		bool CompleteSyncProcess(string websiteName);

		bool InitiateSyncProcess(string websiteName);

		bool UnqueueSyncProcess(string websiteName);

		object ReadContentCacheFile(string cacheKey);
		object ReadContentCacheBlobSource(string cacheKey);

		CacheDependency GetCacheDependency(string websiteName, string cacheKey, string[] additionalCacheKeys);

		string GetContentCacheFilePath();

		void WriteContentCacheBlobToTemp(object o, string contentCacheKey, DateTime overrideLastModifiedMetaData);

		void WriteContentCacheBlobToTemp(object o, string contentCacheKey);

		void CopyTempBlobsToLiveStorage(bool clearCacheFiles);

		bool DeleteContentCacheBlob(string contentCacheKey);

		bool DoesBlobExist(string contentCacheKey);

		void ClearCachedBlobs();

		DateTime GetBlobDate(string contentCacheKey);

		void OutputTrace(string message);

		List<string> GetRecentLogFiles();

		string GetLogFileContents(string fileName);

		void SendErrorEmailMessage(string subject, string body);

		void SendMessage(System.Net.Mail.MailMessage msg);

		bool ProcessItemOnOtherInstance(AgilityItem item, string contentCacheKey, string websiteDomain, bool clearCacheFiles);

	}
}
