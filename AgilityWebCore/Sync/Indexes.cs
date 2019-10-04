using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Agility.Web.AgilityContentServer;
using System.Web;
using System.Data;

namespace Agility.Web.Sync
{
	internal class Indexes
	{


		private static object _pageIndexLock = new object();
		private static object _contentIndexLock = new object();
		private static object _mediaGalleryIndexLock = new object();
		private static object _contentDefIndexLock = new object();

		private static int CheckContentIndex(int latestVersionID, string referenceName, string languageCode)
		{

			Objects.LatestContentItemIndex index = GetLatestContentItemIndex(languageCode);

			//return true to indicate that we need to check again...
			if (! string.IsNullOrEmpty( referenceName))
			{
				//checking for a specific content reference name
				Objects.LatestContentIndexItem indexItem = null;
				if (index.Index.TryGetValue(referenceName, out indexItem))
				{
					//found it in the index... check it
					if (latestVersionID < indexItem.MaxVersionID)
					{
						return indexItem.MaxVersionID;
					}
				}
				else
				{
					//not in the index, there are not items in this view anymore, only need to check latest if latestVersionID is not 0 (zero)
					return latestVersionID;
				}
			}
			else
			{
				//if we don't have a piece of content to check for
				if (latestVersionID < index.MaxVersionID)
				{
					return index.MaxVersionID;
				}				
			}

			return 0;

		}

		private static bool CheckMediaGalleryIndex(int mediaGalleryID, DateTime lastModDate)
		{

			Objects.LatestIndex index = GetLatestMediaGalleryIndex();

			//return true to indicate that we need to check again...
			if (mediaGalleryID > 0)
			{
				//checking for a specific gallery
				Objects.LatestIndexItem indexItem = null;
				if (index.Index.TryGetValue(mediaGalleryID, out indexItem))
				{
					//found it in the index... check it
					return lastModDate < indexItem.ModifiedOn;
				}
				else
				{
					//not in the index, there are not items in this view anymore, only need to check latest if latestVersionID is not 0 (zero)
					return lastModDate > DateTime.MinValue;
				}
			}
			else
			{
				//if we don't have a page to check for
				return lastModDate < index.MaxModDate;
			}
		
		}

		private static bool CheckContentDefinitionIndex(int contentDefinitionID, DateTime lastModDate)
		{

			Objects.LatestIndex index = GetLatestContentDefinitionIndex();

			//return true to indicate that we need to check again...
			if (contentDefinitionID > 0)
			{
				//checking for a specific def
				Objects.LatestIndexItem indexItem = null;
				if (index.Index.TryGetValue(contentDefinitionID, out indexItem))
				{
					//found it in the index... check it
					return lastModDate < indexItem.ModifiedOn;
				}
				else
				{
					//not in the index
					return lastModDate > DateTime.MinValue;
				}
			}
			else
			{
				//if we don't have a page to check for
				return lastModDate < index.MaxModDate;
			}

		}

		private static Objects.LatestContentItemIndex GetLatestContentItemIndex(string languageCode)
		{
			string fileName = string.Format("AgilityContentIndex_{0}.bin", languageCode);
			string contentIndexFilePath = Path.Combine(BaseCache.GetLocalContentFilePath(), fileName);

			Objects.LatestContentItemIndex index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestContentItemIndex;
			if (index == null)
			{

				lock (_contentIndexLock)
				{
					index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestContentItemIndex;
					if (index == null)
					{

						index = BaseCache.ReadFile<Objects.LatestContentItemIndex>(contentIndexFilePath);
						if (index == null)
						{
							index = new Objects.LatestContentItemIndex();
						}

						int maxVersionID = index.MaxVersionID;

						var indexDelta = ServerAPI.GetLatestContentItemIndex(maxVersionID, languageCode);
						if (indexDelta != null)
						{

							foreach (var item in indexDelta)
							{
								if (item.MaxVersionID > index.MaxVersionID)
								{
									index.MaxVersionID = item.MaxVersionID;
								}

								index.Index[item.ReferenceName] = new Objects.LatestContentIndexItem()
								{
									MaxVersionID = item.MaxVersionID,
									ReferenceName = item.ReferenceName,
									Downloaded = false
								};
							}

							BaseCache.WriteFile(index, contentIndexFilePath);
							AgilityContext.HttpContext.Items[fileName] = index;
						}
					}
				}
			}
			return index;
		}

		private static Objects.LatestIndex GetLatestMediaGalleryIndex()
		{
			string fileName = "AgilityMediaGalleryIndex.bin";
			string indexFilePath = Path.Combine(BaseCache.GetLocalContentFilePath(), fileName);

			Objects.LatestIndex index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestIndex;
			if (index == null)
			{

				lock (_mediaGalleryIndexLock)
				{
					index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestIndex;
					if (index == null)
					{

						index = BaseCache.ReadFile< Objects.LatestIndex>(indexFilePath);
						if (index == null)
						{
							index = new Objects.LatestIndex();
						}

						DateTime maxModDate = index.MaxModDate;

						var indexDelta = ServerAPI.GetLatestMediaGalleryIndex(maxModDate);
						if (indexDelta != null)
						{

							foreach (var item in indexDelta)
							{
								if (item.ModifiedOn > index.MaxModDate)
								{
									index.MaxModDate = item.ModifiedOn;
								}

								index.Index[item.ID] = new Objects.LatestIndexItem()
								{
									ID = item.ID,
									ModifiedOnStr = item.ModifiedOnStr,
									Downloaded = false
								};
							}

							BaseCache.WriteFile(index, indexFilePath);
							AgilityContext.HttpContext.Items[fileName] = index;
						}
					}
				}
			}
			return index;
		}

		private static Objects.LatestIndex GetLatestContentDefinitionIndex()
		{
			string fileName = "AgilityContentDefinitionIndex.bin";
			string indexFilePath = Path.Combine(BaseCache.GetLocalContentFilePath(), fileName);

			Objects.LatestIndex index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestIndex;
			if (index == null)
			{

				lock (_contentDefIndexLock)
				{
					index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestIndex;
					if (index == null)
					{

						index = BaseCache.ReadFile< Objects.LatestIndex>(indexFilePath);
						if (index == null)
						{
							index = new Objects.LatestIndex();
						}

						DateTime maxModDate = index.MaxModDate;

						var indexDelta = ServerAPI.GetLatestContentDefinitionIndex(maxModDate);
						if (indexDelta != null)
						{

							foreach (var item in indexDelta)
							{
								if (item.ModifiedOn > index.MaxModDate)
								{
									index.MaxModDate = item.ModifiedOn;
								}

								index.Index[item.ID] = new Objects.LatestIndexItem()
								{
									ID = item.ID,
									ModifiedOnStr = item.ModifiedOnStr,
									Downloaded = false
								};
							}

							BaseCache.WriteFile(index, indexFilePath);
							AgilityContext.HttpContext.Items[fileName] = index;
						}
					}
				}
			}
			return index;
		}

		private static void SetPageItemsDownloaded(IEnumerable<Objects.LatestPageIndexItem> items, string languageCode)
		{
			string fileName = string.Format("AgilityPageIndex_{0}.bin", languageCode);
			string pageIndexFilePath = Path.Combine( BaseCache.GetLocalContentFilePath(), fileName);

			lock (_pageIndexLock)
			{

				Objects.LatestPageIndex index = GetLatestPageItemIndex(languageCode);
				if (index == null) return;

				Objects.LatestPageIndexItem indexItem = null;
				foreach (var item in items)
				{
					if (index.Index.TryGetValue(item.PageID, out indexItem))
					{
						indexItem.Downloaded = item.Downloaded;
					}
				}

				BaseCache.WriteFile(index, pageIndexFilePath);
				AgilityContext.HttpContext.Items[fileName] = index;
			}

		}


		private static Objects.LatestPageIndex GetLatestPageItemIndex(string languageCode)
		{
			string fileName = string.Format("AgilityPageIndex_{0}.bin", languageCode);
			string pageIndexFilePath = Path.Combine(BaseCache.GetLocalContentFilePath(), fileName);

			Objects.LatestPageIndex index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestPageIndex;
			if (index == null)
			{

				lock (_pageIndexLock)
				{
					index = AgilityContext.HttpContext.Items[fileName] as Objects.LatestPageIndex;
					if (index == null)
					{

						index = BaseCache.ReadFile<Objects.LatestPageIndex>(pageIndexFilePath);
						if (index == null)
						{
							index = new Objects.LatestPageIndex();
						}
						int maxVersionID = index.MaxVersionID;

						var indexDelta = ServerAPI.GetLatestPageItemIndex(maxVersionID, languageCode);
						if (indexDelta != null)
						{
							
							foreach (var item in indexDelta)
							{
								if (item.MaxVersionID > index.MaxVersionID)
								{
									index.MaxVersionID = item.MaxVersionID;
								}

								index.Index[item.PageID] = new Objects.LatestPageIndexItem()
								{
									MaxVersionID = item.MaxVersionID,
									PageID = item.PageID,
									Downloaded = false
								};
							}

							BaseCache.WriteFile(index, pageIndexFilePath);
							AgilityContext.HttpContext.Items[fileName] = index;
						}
					}
				}
			}
			return index;
		}

		/// <summary>
		/// Returns true if the version of the page is out of date
		/// </summary>
		/// <param name="latestVersionID"></param>
		/// <param name="pageID"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		private static bool CheckPageIndex(int latestVersionID, int pageID, string languageCode)
		{

			Objects.LatestPageIndex index = GetLatestPageItemIndex(languageCode);

			//return true to indicate that we need to check again...
			if (pageID > 0)
			{
				//checking for a specific page
				Objects.LatestPageIndexItem indexItem = null;
				if (index.Index.TryGetValue(pageID, out indexItem))
				{
					return latestVersionID < indexItem.MaxVersionID;
				}
			}
			else
			{
				//if we don't have a page to check for
				return latestVersionID < index.MaxVersionID;
			}


			return true;
		}


		internal static bool IsStagingItemOutOfDate(object existingItem)
		{
			if (existingItem == null) return true;
			AgilityContent existingItem_Content = existingItem as AgilityContent;
			AgilitySitemap existingItem_Sitemap = existingItem as AgilitySitemap;
			AgilityPage existingItem_Page = existingItem as AgilityPage;
			AgilityModule existingItem_Module = existingItem as AgilityModule;
			AgilityAssetMediaGroup existingItem_Gallery = existingItem as AgilityAssetMediaGroup;

			if (existingItem_Content != null)
			{
				//CONTENT
				int versionID = 0;
				int maxVersionIDInDataSet = 0;
				if (existingItem_Content.DataSet != null)
				{

					if (existingItem_Content.DataSet.ExtendedProperties.ContainsKey("MaxVersionID"))
					{
						int.TryParse(string.Format("{0}", existingItem_Content.DataSet.ExtendedProperties["MaxVersionID"]), out maxVersionIDInDataSet);
					}

					DataTable dt = existingItem_Content.DataSet.Tables["ContentItems"];
					if (dt != null)
					{
						object maxObj = dt.Compute("max(VersionID)", "");
						if (!int.TryParse($"{maxObj}", out versionID)) versionID = -1;
						
					}


					if (maxVersionIDInDataSet > versionID) versionID = maxVersionIDInDataSet;
				}


				int latestVersionID = CheckContentIndex(versionID, existingItem_Content.ReferenceName, existingItem_Content.LanguageCode);
				if (latestVersionID > 0)
				{
					if (existingItem_Content.DataSet != null)
					{
						existingItem_Content.DataSet.ExtendedProperties["MaxVersionID"] = latestVersionID;
					}
					return true;
				}
				else
				{
					return false;
				}

			}
			else if (existingItem_Page != null)
			{
				//PAGE
				return CheckPageIndex(existingItem_Page.ZVersionID, existingItem_Page.ID, existingItem_Page.LanguageCode);
			}
			else if (existingItem_Module != null)
			{
				//MODULE DEF
				return CheckContentDefinitionIndex(existingItem_Module.ID, existingItem_Module.LastAccessDate);
			}
			else if (existingItem_Sitemap != null)
			{
				//SITEMAP
				return CheckPageIndex(existingItem_Sitemap.XMaxPageVersionID, -1, existingItem_Sitemap.LanguageCode);
			}
			else if (existingItem_Gallery != null)
			{
				//MEDIA GROUPING/GALLERY

				DateTime dtModifiedOn = existingItem_Gallery.ModifiedOn;

				if (existingItem_Gallery.Media != null && existingItem_Gallery.Media.Length > 0)
				{
					//check for the most recent item..

					DateTime dtItem = existingItem_Gallery.Media.Max(d => d.ModifiedOn);
					if (dtItem > dtModifiedOn) dtModifiedOn = dtItem;
				}


				return CheckMediaGalleryIndex(existingItem_Gallery.ID, dtModifiedOn);
			}



			return true;
		}
	}
}
