using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agility.Web.Configuration
{
	
	public class Current
	{
		private static Settings _currentSettings;
		private static ReaderWriterLock _settingsReaderWriter = new ReaderWriterLock();

		/// <summary>
		/// Gets the current Settings object from the web.config (in the application root)
		/// </summary>
		public static Settings Settings
		{
			get
			{
				try
				{

					Settings settings = null;
					_settingsReaderWriter.AcquireReaderLock(5000);
					//grab from static field (this happens when doing a sync thread)
					settings = _currentSettings;

					if (null == settings)
					{
						try
						{
							_settingsReaderWriter.UpgradeToWriterLock(5000);

							var builder = new ConfigurationBuilder()
								.SetBasePath(Directory.GetCurrentDirectory())
								.AddJsonFile("appsettings.json", false, true)
								.AddJsonFile($"appsettings.{HostingEnvironment.EnvironmentName}.json", true, true);

							var configuration = builder.Build();
							var agilitySection = configuration.GetSection("Agility");
							if (agilitySection == null) throw new Exception("The Agility section was not found in the appsettings.json file");

							settings = agilitySection.Get<Settings>();

							_currentSettings = settings;

						}
						catch(Exception)
						{
							throw;
						}
						finally
						{
							if (_settingsReaderWriter != null && _settingsReaderWriter.IsWriterLockHeld)
							{
								_settingsReaderWriter.ReleaseWriterLock();

							}
						}
					}


					return settings;
				}
				catch (Exception ex)
				{
					throw ex;
				}
				finally
				{
					if (_settingsReaderWriter != null && _settingsReaderWriter.IsReaderLockHeld)
					{
						_settingsReaderWriter.ReleaseReaderLock();

					}
				}

			}
		}

        public static IHostEnvironment HostingEnvironment { get; internal set; }
    }

	/// <summary>
	/// The configuration settings for this application declared in the Agility.Web section of the web.config.
	/// </summary>
	public class Settings 
	{

		
		

		
	
		/// <summary>
		/// Gets/sets the address of the SMTP Server that will be used to send emails through.
		/// </summary>
		public string SmtpServer { get; set; }
		


		public string DefaultCachePriority { get; set; }
		

		/// <summary>
		/// Gets/sets whether trailing slashes will be removed from all urls
		/// </summary>
		public bool IgnoreTrailingSlash { get; set; }
		


        /// <summary>
		/// Gets/sets whether Open Graph tags will be outputted in the header.
		/// </summary>		
		public bool OutputOpenGraph { get; set; }
		

		/// <summary>
		/// Gets/sets whether Twitter Card tags will be out output in the header.
		/// </summary>		
		public string TwitterCardSite { get; set; }
		

		/// <summary>
		/// Gets/sets whether content data tables are copied or not.
		/// </summary>		
		public bool CopyDataTables { get; set; }
		

		/// <summary>
		/// Gets/sets the ApplicationName settings.  This is the identifier for this application or website in the log file and emails.
		/// </summary>
		
		public string ApplicationName { get; set; }
		

		/// <summary>
		/// Gets/sets the WCFHostHeader settings.  This is the host header that matches the domain name in Agility.
		/// </summary>		
		public string WCFHostHeader { get; set; }
		

		/// <summary>
		/// Gets/sets whether to redirect to the default language if a given page is not found in the current language. 
		/// </summary>	
		public bool Redirect404ToDefaultLanguage { get; set; }
		

		/// <summary>
		/// Gets/sets whether the URL path is maintained when the digital channel is switched.
		/// </summary>		
		public bool KeepUrlPathOnForcedChannel { get; set; }
		

		/// <summary>
		/// Gets/sets the Domain that will be used for cookies.  If this is blank, then the default domain will be used.
		/// </summary>		
		public string CookieDomain { get; set; }

		/// <summary>
		/// Gets/sets the WebsiteName settings.  This is the identifier for website according to agility.
		/// </summary>
		public string WebsiteName { get; set; }


		/// <summary>
		/// Gets/sets the Agility Security Key for this website.  This key can be obtained from the Content Mangager application.
		/// </summary>
		public string SecurityKey { get; set; }


		/// <summary>
		/// Gets/sets the replace settings used with the ReplacementFilterModule. Can be a string or comma-delimited list of strings.
		/// </summary>
		public string Replace { get; set; }

		/// <summary>
		/// Gets/sets the replaceWith settings used with the ReplacementFilterModule. Must be a single string.
		/// </summary>
		public string ReplaceWith { get; set; }

		
		/// <summary>
		/// Gets/sets the ContentCacheFilePath settings.  This is the location where any files will be cached to disk.
		/// </summary>		
		public string ContentCacheFilePath { get; set; }

		public string RootedContentCacheFilePath {
			get {
				if (!string.IsNullOrEmpty(ContentCacheFilePath) && !Path.IsPathRooted(ContentCacheFilePath)) 
				{
					return $"{Directory.GetCurrentDirectory()}/{ContentCacheFilePath}";
				}

				return ContentCacheFilePath;
			}
		}
		
		/// <summary>
		/// Gets/sets the OutputCacheFilePath settings.  This is the location where any output cached files will be saved to disk.
		/// </summary>		
		public string OutputCacheFilePath { get; set; }
		

		/// <summary>
		/// Gets/sets whether the site is in Development Mode and locked to staging content only.
		/// </summary>		
		public bool DevelopmentMode { get; set; }

		/// <summary>
		/// Gets/sets the number of hours that can elapse before data is automatically refreshed in development mode.
		/// </summary>				
		public int DevelopmentModeRefreshTimeoutHours { get; set; } = 1;
		
		/// <summary>
		/// Gets/sets the trace settings for this application.
		/// </summary>		
		public TraceSettings Trace { get; set; }

		/// <summary>
		/// Gets/sets the default number of minutes to hold output cache.
		/// </summary>		
		public int OutputCacheDefaultTimeoutMinutes { get; set; } = 5;


		/// <summary>
		/// Gets/sets the default number of minutes to hold output cache.
		/// </summary>		
		public bool NoJQuery { get; set; } = true;
		
		public bool DebugAgilityComponentFiles { get; set; }

		/// <summary>
		/// Gets/sets the URL to the Content Server.
		/// </summary>
		public string ContentServerUrl { get; set; }

		/// <summary>
		/// Gets/set whether we want to debug the sync process.
		/// </summary>
		public bool DebugSync { get; set; }


		public ComponentSettings Analytics { get; set; }

		public ComponentSettings UGC { get; set; }
    }
}
