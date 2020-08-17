using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Objects
{
	public class Config
	{

		public string DomainName = "";
		public string DefaultLanguageCode = "";
		public bool EnableOutputCache = false;
		public bool IsStagingDomain = false;
		public int ID = 0;
		public string LanguageCode = "";
		public string Name = "";
		public bool OutputCacheSlidingExpiration = false;
		public int OutputCacheTimeoutSeconds = 0;

		
		public CacheItemPriority CacheItemPriority;
		public string StatsTrackingScript;
		public string DefaultLoginUser;
		public string GlobalCss;
		public string ErrorEmails;
		public Language[] Languages;

		public bool ExtensionlessUrls = false;

	}
}
