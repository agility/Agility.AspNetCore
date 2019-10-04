using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Globalization;
using Agility.Web.Tracing;
using Agility.Web.Objects;
using System.IO;
using Agility.Web.Configuration;
using System.Security.Cryptography;
using System.Collections.Specialized;
using Agility.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Agility.Web.Caching;

namespace Agility.Web.HttpModules
{


	public class AgilityHttpModule
	{
		internal const string ECMS_DOCUMENTS_KEY = "ecms.aspx";
		internal const string ECMS_DOCUMENTS_KEY2 = "ecms.ashx";
		internal const string ECMS_RSS_KEY = "ecmsrss.aspx";
		internal const string ECMS_ERRORS_KEY = "ecmserrors.ashx";
		internal const string ECMS_EDITOR_CSS_KEY = "ecms-editor-css.ashx";
		internal const string DynamicCodePrepend = "DynamicAgilityCode/";

		internal static string BaseURL = string.Empty;
		public static Agility.Web.Objects.Config Config = null;

		/// <summary>
		/// Dispose of any shared resources.
		/// </summary>
		

		
			
		
		internal async static void HandleIntializationException(Exception ex)
		{	
			HttpContext Context = AgilityContext.HttpContext;
			HttpRequest Request = Context.Request;
			HttpResponse Response = Context.Response;

			string path = Request.Path.Value.ToLowerInvariant();

			//don't bother showing the error for ecms.aspx or ecms.ashx references...
			if (path.EndsWith(ECMS_DOCUMENTS_KEY)
				|| path.EndsWith(ECMS_DOCUMENTS_KEY2)) return;


			//handle ALL exceptions by displaying the "maintanence mode" page
			string imageUrl = "//dehd7rclpxx3r.cloudfront.net/preview-bar/2015-03/MaintenanceMode.gif";

			//MOD - joelv - can't clear the response in this case... Response.Clear();

			//set the response to NOT be cacheable
			AgilityContext.CacheResponse = false;
			
			//write out a simple page

			StringBuilder sb = new StringBuilder(@"<html><head><title>Maintenance Mode</title>
					<style type=""text/css"">
						body {
							color: #777777;
							font-family: Verdana, sans;
							font-size: 14px;
						}
						a { color: #cd6700; }
					</style>					
				</head>");

			sb.Append("<body>");
			
			sb.AppendFormat("<a href='javascript:location.reload()'><img border='0' src='{0}'/></a>", imageUrl);
			if (Current.Settings.DevelopmentMode)
			{
				//turn on refresh mode for this request...
				//AgilityContext.RefreshStagingModeDataOnDomain = true;

				//write out a link to the log
				sb.Append("<div style='margin-left: 60px; width: 800px; '>");
				sb.Append(string.Format("<div> Click <a href='{0}?enc={1}' target='_blank'>here</a> to view the log file.</div>", AgilityHttpModule.ECMS_ERRORS_KEY, HttpUtility.UrlEncode(WebTrace.GetEncryptionQueryStringForLogFile(DateTime.Now))));
				sb.Append("</div>");
				sb.Append("<div style='margin-left: 60px; width: 800px; '>");
				sb.Append(string.Format("{0}", ex).Replace("\n", "<br/>"));
				sb.Append("</div>");
				
			}
			
			WebTrace.WriteException(ex);
			
			sb.Append("</body></html>");

			await Response.WriteAsync(sb.ToString());
		}

		private static IDictionary<string, CultureInfo> cultures = new Dictionary<string, CultureInfo>();
		private static HybridDictionary invalidCultures = new HybridDictionary();

		internal static void SetLanguageAndCultureBasedOnPage(AgilityPage page)
		{
			//set the current language code IN CONTEXT
			//NOTE: in this case			
			AgilityContext.LanguageCode = page.LanguageCode;

			var context = AgilityContext.HttpContext;
			

			//attempt to set the CurrentThread's culture based on the page's language code					
			string cultureCode = page.LanguageCode;
			//special case for chinese.
			if (cultureCode.ToLowerInvariant() == "zh") cultureCode = "zh-cn";

			CultureInfo info = null;
			
			

			if (! cultures.TryGetValue(cultureCode, out info) && ! invalidCultures.Contains(cultureCode))
			{
				var availableCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

				info = availableCultures.FirstOrDefault(c => string.Equals(c.Name, cultureCode, StringComparison.CurrentCultureIgnoreCase));
				
				if (info == null)
				{
					//try to create the culture
					try
					{
						info = CultureInfo.CreateSpecificCulture(cultureCode);
					}
					catch
					{
						Agility.Web.Tracing.WebTrace.WriteWarningLine(string.Format("Could not switch culture to {0}", cultureCode));
					}
				}
				
				if (info != null)
				{
					cultures[cultureCode] = info;
					invalidCultures.Remove(cultureCode);					
				}
				else
				{
					invalidCultures[cultureCode] = true;
				}
			}

			if (info != null)
			{
				//CultureInfo info = CultureInfo.CreateSpecificCulture(page.LanguageCode);
				System.Threading.Thread.CurrentThread.CurrentCulture = info;
				System.Threading.Thread.CurrentThread.CurrentUICulture = info;
			}
		}

		internal static void SetupOutputCaching(HttpRequest Request, HttpResponse Response, AgilityPage page)
		{
			
			if (page == null 
				|| AgilityContext.IsPreview
				|| AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Staging
				|| !AgilityContext.Domain.EnableOutputCache
				|| page.ExcludeFromOutputCache				
				)
			{
				Agility.Web.Tracing.WebTrace.WriteInfoLine("Cache not enabled on this page. " + Request.GetDisplayUrl());
				//set the response to NOT be cacheable
//TODO: fix OUTPUT CACHING
				//Response.Cache.SetCacheability(HttpCacheability.NoCache);
				//Response.Cache.SetNoServerCaching();
				//Response.Cache.SetExpires(DateTime.Now.Subtract(TimeSpan.FromDays(60)));				
			}
			else
			{			
				SetDefaultCacheability(Request, Response);				
			}
		}

		public static void SetDefaultCacheability(HttpRequest Request, HttpResponse Response)
		{
			if (string.Format("{0}", Request.HttpContext.Items["AgilityCacheControlSet"]) != "1")
			{
				//add a custom header and cache "vary by" parameter base on the page id and the language code

				TimeSpan expiration = TimeSpan.FromMinutes(Current.Settings.OutputCacheDefaultTimeoutMinutes);
				if (AgilityContext.Domain.OutputCacheTimeoutSeconds > 0)
				{
					expiration = TimeSpan.FromSeconds(AgilityContext.Domain.OutputCacheTimeoutSeconds);
				}
				//TODO: fix OUTPUT CACHING
				if (AgilityCache.UseAgilityOutputCache)
				{
					//	//USING AGILITY CACHING...

					//	var settings = AgilityOutputCacheModule.Settings;

					//	settings.Timeout = expiration;
					//	settings.AllowInOutputCache = true;

					//	Response.Cache.SetNoServerCaching();

					//}
					//else
					//{
					//	//BUILT IN OUPUT CACHE
					//	Response.Cache.VaryByParams["lang"] = true;
					//	Response.Cache.VaryByParams["agilitychannel"] = true;
					//	Response.Cache.VaryByParams["agilitychannelid"] = true;
					//	Response.Cache.VaryByParams["agilitypreviewkey"] = true;

					//	Response.Cache.SetExpires(DateTime.Now.Add(expiration));

					//	Response.Cache.SetValidUntilExpires(true);
					//	Response.Cache.SetCacheability(HttpCacheability.Server);
					//	Response.Cache.SetMaxAge(expiration);
					//	Response.Cache.SetVaryByCustom("AgilityCacheControl");
					}

					Request.HttpContext.Items["AgilityCacheControlSet"] = "1";
			}
		}
		
		internal static void SetApplicationIdentity(HttpRequest Request)
		{
			//set the application impersonation identity
			//if (Agility.Web.Sync.SyncThread.ApplicationIdentity == null)
			//{
				//get the current windows identity
				//Agility.Web.Sync.SyncThread.ApplicationIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
				
				//set the base url for sending out links to here from non-http threads
				string _u = UriHelper.GetEncodedUrl(AgilityContext.HttpContext.Request);
				
				_u = _u.Substring(0, _u.IndexOf("/", _u.IndexOf("//") + 2));
				if (Request.Path.Value != "/")
				{
					BaseURL = string.Format("{0}{1}/", _u, Request.Path.Value);
				}
				else
				{
					BaseURL = _u;
				}

				Config = Data.GetConfig();

//TODO: double check that the offline thread should run like this...
				//initialize the offline processing...
				if (!OfflineProcessing.IsOfflineThreadRunning)
				{
					OfflineProcessing.StartOfflineThread();
				}


			//}
		}


		/// <summary>
		/// Handle Postbacks from the Status Panel
		/// </summary>
		/// <param name="eventArgument"></param>		
		public static bool HandleStatusPanelPostback(string eventArgument)
		{

			HttpContext Context = AgilityContext.HttpContext;
			HttpRequest Request = Context.Request;
			HttpResponse Response = Context.Response;

			Tracing.WebTrace.WriteVerboseLine("Status Panel Postback: " + eventArgument);
			string url = $"{Request.Path.Value}?{Request.QueryString.Value}";

			AgilityContext.IsPreview = true;

			if (string.Compare(eventArgument, "endPreview", true) == 0)
			{
				//end preview mode
				AgilityContext.IsPreview = false;
				AgilityContext.PreviewDateTime = DateTime.MinValue;				
			}
			else if (string.Compare(eventArgument, "switchToLive", true) == 0)
			{
				//change to live

				AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Live;

			}
			else if (string.Compare(eventArgument, "switchToStaging", true) == 0)
			{
				//change to staging

				AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Staging;

			}
			else if (eventArgument.StartsWith("switchpreviewdate"))
			{
				//change the preview date - and we can only preview in Live mode with a date...

				AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Live;

				string[] args = eventArgument.Split(';');
				DateTime dt = DateTime.MinValue;
				if (args.Length == 2 && DateTime.TryParse(args[1], out dt))
				{
					AgilityContext.PreviewDateTime = dt;
				}
				else
				{
					AgilityContext.PreviewDateTime = DateTime.MinValue;
				}

			}
			else if (eventArgument.StartsWith("switchlanguage"))
			{
				string[] args = eventArgument.Split(';');
				if (args.Length == 2)
				{
					string lang = args[1];

					//refresh the page to the URL in the client browser
					url = Agility.Web.Util.Url.ModifyQueryString(url, "lang=" + lang, "");

				}

			}

			url = Agility.Web.Util.Url.RemoveQueryString(url, "agilitypostback");

			//refresh the page to the URL in the client browser
			RedirectResponse(url);
			
			return true;

		}
		
		internal static void RedirectResponse(string url)
		{
			RedirectResponse(url, 302);
		}

		internal static void RedirectResponse(string url, int statusCode)
		{
			RedirectResponse(url, statusCode, string.Empty);
		}

		internal static void RedirectResponse(string url, int statusCode, string content)
		{
			var response = AgilityContext.HttpContext.Response;

			//handle urls that have ~/
			if (url.StartsWith("~/"))
			{
				//assume ~/ is the root of the site...
				url = url.Substring(1);
				
			}

			//send back a response to the browser
			response.Headers["Location"] = url;
			response.StatusCode = statusCode;

			//HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			response.Headers["Cache-Control"] = "no-cache";

			
			//if (!string.IsNullOrWhiteSpace(content))
			//{
			//	await response.WriteAsync(content);
			//}

			
			

		}

		/// <summary>
		/// Checks whether the current request should initiate or maintain the "Preview" mode state of the current browser session.
		/// </summary>		
		/// <param name="page"></param>
		/// <param name="Context"></param>
		public static bool CheckPreviewMode(HttpContext Context, AgilityPage page)
		{
			return CheckPreviewMode(Context);
		}


		/// <summary>
		/// Checks whether the current request should initiate or maintain the "Preview" mode state of the current browser session.
		/// </summary>		
		/// <param name="Context"></param>
		public static bool CheckPreviewMode(HttpContext Context)
		{

			
			if (AgilityContext.Domain == null || Context.Request.Query["agilitypreview"] == "0")
			{
				AgilityContext.IsPreview = false;
				return false;
			}

			#region *** New preview URL ***
			string agilitypreviewkey = Context.Request.Query["agilitypreviewkey"];
			if (!string.IsNullOrEmpty(agilitypreviewkey))
			{

				////attempt to validate the key using the current page id
				//string securityKey = Current.Settings.WebsiteSettings[AgilityContext.WebsiteName].SecurityKey;
				//byte[] data = UnicodeEncoding.Unicode.GetBytes(string.Format("{0}_{1}_Preview", page.ID, securityKey));
				//SHA512 shaM = new SHA512Managed();
				//byte[] result = shaM.ComputeHash(data);
				//string pageKey = Convert.ToBase64String(result);				

				//if (string.Equals(pageKey, agilitypreviewkey, StringComparison.CurrentCultureIgnoreCase)
				//    || string.Equals(pageKey, agilitypreviewkey.Replace(" ", "+"), StringComparison.CurrentCultureIgnoreCase))
				//{
				//    //preview mode is ON
				//    AgilityContext.IsPreview = true;
				//    AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Staging;

				//    //redirect the URL to remove the "agilitypreviewkey" from the URL and add on the preview maintaince query
				//    string url = Agility.Web.Util.Url.RemoveQueryString("agilitypreviewkey");

				//    RedirectResponse(url);
				//    return true;

				//}
				//else
				//{
					//attempt to validate the key universally (using -1)
					string securityKey = Current.Settings.SecurityKey;
					byte[] data = UnicodeEncoding.Unicode.GetBytes(string.Format("{0}_{1}_Preview", -1, securityKey));
					SHA512 shaM = new SHA512Managed();
					byte[] result = shaM.ComputeHash(data);
					string generalKey = Convert.ToBase64String(result);

					if (string.Equals(generalKey, agilitypreviewkey, StringComparison.CurrentCultureIgnoreCase)
						|| string.Equals(generalKey, agilitypreviewkey.Replace(" ", "+"), StringComparison.CurrentCultureIgnoreCase))
					{
						//preview is ON, flip to staging mode
						AgilityContext.IsPreview = true;
						AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Staging;

						if (AgilityContext.PreviewDateTime > DateTime.MinValue)
						{
							AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Live;
						}

						//redirect the URL to remove the "agilitypreviewkey" from the URL
						string url = Agility.Web.Util.Url.RemoveQueryString("agilitypreviewkey");

						RedirectResponse(url);
						return true;
					}
					else
					{
						Agility.Web.Tracing.WebTrace.WriteWarningLine(string.Format("The preview key {0} could not be verified against the general key {1}.", agilitypreviewkey, generalKey));
					}
				//}

				//if the value was set, and it is incorrect, ensure we are not in staging mode
				AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Live;

			}
			#endregion

			#region *** Maintain Preview Via Cookie ***

			if (AgilityContext.IsPreview) return false;

			#endregion

			return false;
		}
		
		/// <summary>
		/// Handle a request for an agility file or attachment (ecms.aspx OR ecms.ashx)
		/// </summary>
		/// <param name="context"></param>
		/// <param name="request"></param>
		/// <param name="response"></param>
		internal static void HandleAgilityFileRequest(HttpContext context, HttpRequest request, HttpResponse response)
		{
			response.Clear();

			try
			{

				//check if this is a manual error trigger
				if (string.Compare(request.Query["throwerror"], "true") == 0)
				{
					var task = response.WriteAsync("Test exception triggered.");
					Agility.Web.Tracing.WebTrace.WriteException(new Exception("This is a test exception."));
					task.Wait();
				}

				


				string filepath = request.Headers["Path-Info"];
				if (string.IsNullOrEmpty(filepath))
				{
					filepath = request.Path.Value;
					int index = filepath.IndexOf(ECMS_DOCUMENTS_KEY, StringComparison.InvariantCultureIgnoreCase);

					if (index == -1)
					{
						index = filepath.IndexOf(ECMS_DOCUMENTS_KEY2, StringComparison.InvariantCultureIgnoreCase);
					}

					index += ECMS_DOCUMENTS_KEY.Length;

					if (filepath.Length > index)
					{
						filepath = filepath.Substring(index, filepath.Length - index);
					}
					else
					{
						filepath = string.Empty;
					}

				}


				if (string.IsNullOrEmpty(filepath) || filepath == "/")
				{
					//if there is not PathInfo, throw a 404 (not found)
					response.StatusCode = 404;					
					return;
				}

				//set the content-type based on the file extension
				string extension = Path.GetExtension(filepath);
				string contentType = "application";
				if (extension != null)
				{
					extension = extension.TrimStart('.');
					
					switch (extension)
					{
						case "jpg":
							contentType = "image/jpeg";
							break;
						case "png":
							contentType = "image/png";
							break;
						case "css":
							contentType = "text/css";
							break;
					}

				}

				if (contentType != string.Empty)
				{
					response.ContentType = contentType;
				}


				//Check the resources embedded in the assembly:
				System.IO.Stream file = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(filepath.Substring(1));
				if (file != null)
				{
					//Reads in the resources for images.
					byte[] fileData = new byte[file.Length];
					file.Read(fileData, 0, fileData.Length);
					
					response.Body.Write(fileData, 0, fileData.Length);
				}
				else
				{


					//Pull file from Agility
					FileInfo fileInfo = null;

					string websiteName = AgilityContext.WebsiteName;


					//if the path starts with /$, assume the root path represents the website name
					//BUT WE IGNORE THIS NOW
					if (filepath.StartsWith("/$"))
					{
						//We used to pull company name from the URL here - but NOT ANYMORE

						//websiteName = filepath.Substring(2, filepath.IndexOf("/", 2) - 2);
						//websiteName = websiteName.Replace("+", " ");						

						filepath = filepath.Substring(filepath.IndexOf("/", 2));

					}

					//ATTACHMENT CHECK
					//check if the first directory AFTER the company is a GUID, and the next is the referenceName, then the filename
					string[] ary = filepath.Split('/');
					if (ary.Length == 4)
					{
						//Jon Voigt - modification, If a filename is this length, it them errors.
						if (ary[1].Length == 36 && ary[1].IndexOf(".") == -1)
						{
							try
							{
								string guidStr = ary[1];
								string filename = ary[3];

								fileInfo = BaseCache.GetAttachment(guidStr, filename, AgilityContext.LanguageCode, websiteName);
							}
							catch (Exception ex)
							{
								WebTrace.WriteException(ex, "Going to continue to try and process the file as a document");
							}
						}
					}

					

					//if we get this far, there was no file, or no data to stream: return a 404					
					response.StatusCode = 404;					
				}
			}
			catch (Exception ex)
			{
				WebTrace.WriteException(ex);

				//if we get this far, there was no file, or no data to stream: return a 404	
				response.StatusCode = 404;				
			}
			
		}
		
        
		internal async static void HandleErrorsRequest(HttpContext context, HttpRequest request, HttpResponse response)
		{
			try
			{
				
				response.ContentType = "text/plain";



				string enc = request.Query["enc"];
				if (string.IsNullOrEmpty(enc))
				{				
					await response.WriteAsync("Not authorized.");
					return;
				}

				//get the date that we want logs for...
				DateTime logDate = DateTime.MinValue;

				if (!DateTime.TryParse(request.Query["date"], out logDate))
				{
					logDate = DateTime.Now;
				}



				string compEnc = WebTrace.GetEncryptionQueryStringForLogFile(logDate);
				if (compEnc != enc)
				{
					await response.WriteAsync("Not authorized.");
					return;
				}


				if (AgilityContext.ContentAccessor == null)
				{
					string filepath = WebTrace.GetLogFilePath(logDate);
					if (!File.Exists(filepath))
					{
						await response.WriteAsync(string.Format("The log file for {0:d} could not be found.", logDate));
						return;
					}
					else
					{

						await response.WriteAsync(string.Format("{0} - log file for {1:d} on {2}{3}{3}", 
							Current.Settings.ApplicationName, logDate, Environment.MachineName, Environment.NewLine));
						await response.SendFileAsync(filepath);
					}
				}
				else
				{
					await response.WriteAsync(string.Format("{0} - log file for {1:d} on {2}{3}{3}",
							Current.Settings.ApplicationName, logDate, Environment.MachineName, Environment.NewLine));

					string logFileContents = AgilityContext.ContentAccessor.GetLogFileContents(string.Format("{0:yyyy_MM_dd}.log", logDate));
					await response.WriteAsync(logFileContents);
				}
				
			}			
			catch (Exception ex)
			{
				WebTrace.WriteException(ex);

				//if we get this far, there was no file, or no data to stream: return a 404	
				response.StatusCode = 500;
				//response.Description = "The css data not be loaded.";
			}
			
		}

		internal async static void HandleEditorCssRequest(HttpContext context, HttpRequest request, HttpResponse response)
		{
			try
			{
				
				response.ContentType = "text/css";
				await response.WriteAsync(Data.GetConfig().GlobalCss);
				

			}			
			catch (Exception ex)
			{
				WebTrace.WriteException(ex);

				//if we get this far, there was no file, or no data to stream: return a 404	
				response.StatusCode = 500;
				//response.StatusDescription = "The css data not be loaded.";
			}
			
		}

		/// <summary>
		/// Checks whether the sitemap path and language code (and current user agent) match with a channel and redirection.
		/// </summary>
		/// <param name="sitemapPath"></param>
		/// <param name="languageCode"></param>
		/// <returns></returns>
		internal static bool HandleChannelsAndRedirects(ref string sitemapPath, string languageCode)
		{


			string url = AgilityContext.HttpContext.Request.GetEncodedUrl();
			string userAgent = AgilityContext.HttpContext.Request.Headers["User-Agent"];

			//ignore the service requests...
			if (url.IndexOf("AgilityWebsiteService.svc", StringComparison.CurrentCultureIgnoreCase) != -1
				|| url.IndexOf("AgilityCacheNotificationHandler.ashx", StringComparison.CurrentCultureIgnoreCase) != -1)
			{
				return false;
			}


			if (userAgent == null) userAgent = string.Empty;

			if (string.IsNullOrEmpty(languageCode)) languageCode = AgilityContext.LanguageCode;

			string prechannelSitemapPath = sitemapPath;


			string channelRedirUrl = CalculateChannel(url, userAgent);

			if (! string.IsNullOrEmpty(channelRedirUrl))
			{
				AgilityContext.HttpContext.Response.Clear();
				//AgilityContext.HttpContext.Response.ClearHeaders();
				AgilityContext.HttpContext.Response.Headers["Location"] = channelRedirUrl;
				AgilityContext.HttpContext.Response.StatusCode = 301;
				return true;
			}
							
			if (string.IsNullOrEmpty(sitemapPath) || sitemapPath == "~/" || sitemapPath == "/")
			{
				//get the first page in the folder...
				sitemapPath = BaseCache.GetDefaultPagePath(AgilityContext.LanguageCode, AgilityContext.WebsiteName, "");
			}
			

			/*
			 * Check for a redirect.  3 urls to check:
			 *   1: full, absolute url
			 *   2: absolute url with no query string
			 *   3: server relative url
			 */

			string absoluteUrlTest = AgilityContext.HttpContext.Request.GetEncodedUrl().ToLowerInvariant().TrimEnd('/');
			string langCodeInPath = string.Format("/{0}/", AgilityContext.LanguageCode).ToLower();
			if (absoluteUrlTest.IndexOf(langCodeInPath, StringComparison.Ordinal) != -1)
			{
				absoluteUrlTest = absoluteUrlTest.Replace(langCodeInPath, "/");
			}

			string absoluteUrlNoQueryTest = string.Empty;
			string appRelativeUrlTest = string.Empty;
			string appRelativeUrlTestWithQuery = string.Empty;
			string query = string.Empty;

			URLRedirection redirection = null;

			int queryIndex = absoluteUrlTest.IndexOf("?", StringComparison.Ordinal);

			if (queryIndex > -1)
			{
				query = absoluteUrlTest.Substring(queryIndex);
				absoluteUrlNoQueryTest = absoluteUrlTest.Substring(0, queryIndex);
			}


			//get the redirect url from the pre-microsite path...
			if (!string.IsNullOrEmpty(prechannelSitemapPath))
			{
				appRelativeUrlTest = prechannelSitemapPath.ToLowerInvariant();
				if (!appRelativeUrlTest.StartsWith("~/"))
				{
					if (appRelativeUrlTest.StartsWith("/"))
					{
						appRelativeUrlTest = string.Format("~{0}", appRelativeUrlTest);
					}
					else
					{
						appRelativeUrlTest = string.Format("~/{0}", appRelativeUrlTest);
					}
				}
				appRelativeUrlTestWithQuery = string.Format("{0}{1}", appRelativeUrlTest, query);
				appRelativeUrlTest = appRelativeUrlTest.TrimEnd('/');
			}


			StringBuilder sbTraceMessage = new StringBuilder();

			//absolute urls 1st
			Dictionary<string, URLRedirection> redirs = AgilityContext.URLRedirections;
			Dictionary<string, URLRedirection> redirsNoQuery = AgilityContext.URLRedirections_WithoutQueryStrings;			
			Dictionary<string, URLRedirection> redirsWithQueryStrings = AgilityContext.URLRedirections_WithQueryStrings;


            if (!redirs.TryGetEscapedUri(absoluteUrlTest, out redirection))
			{
                if (!redirsNoQuery.TryGetEscapedUri(absoluteUrlNoQueryTest, out redirection))
				{
                    if (!redirsNoQuery.TryGetEscapedUri(appRelativeUrlTest, out redirection))
					{
                        if (redirsWithQueryStrings.TryGetEscapedUri(appRelativeUrlTestWithQuery, out redirection))
						{
							//found a redirect with a query string - we need to remove querystrings from the current url when we redirect
							sbTraceMessage.AppendFormat("Redirecting URL {0} to {1}.", appRelativeUrlTestWithQuery, redirection.RedirectURL);							
						}
					} 
					else 
					{

						sbTraceMessage.AppendFormat("Redirecting URL {0} to {1}.", appRelativeUrlTest, redirection.RedirectURL);
					}
				}
				else
				{
					sbTraceMessage.AppendFormat("Redirecting URL {0} to {1}.", absoluteUrlNoQueryTest, redirection.RedirectURL);
				}
			}
			else
			{
				sbTraceMessage.AppendFormat("Redirecting URL {0} to {1}.", absoluteUrlTest, redirection.RedirectURL);
			}
			

			if (redirection == null)
			{
				//now test the urls - wildcards 2nd
				//NEVER WILDCARD THE SVC OR ASHX FOR SYNC
				Dictionary<string, URLRedirection> wildcardRedirs = AgilityContext.WildcardRedirections;
				if (wildcardRedirs != null
					&& wildcardRedirs.Count > 0
					&& absoluteUrlTest.IndexOf("AgilityWebsiteService.svc", StringComparison.CurrentCultureIgnoreCase) == -1
					&& absoluteUrlTest.IndexOf("AgilityCacheNotificationHandler", StringComparison.CurrentCultureIgnoreCase) == -1)
				{

					string key = wildcardRedirs.Keys.FirstOrDefault(k => absoluteUrlTest.StartsWith(k, StringComparison.CurrentCultureIgnoreCase));
					if (key != null)
					{
						URLRedirection redirectionTmp = wildcardRedirs[key];

						string redirUrl = redirectionTmp.RedirectURL;
						if (redirUrl.Contains("*"))
						{
							//switch the entire url if neccessary
							redirUrl = redirUrl.Substring(0, redirUrl.IndexOf("*"));
							redirUrl = string.Format("{0}{1}", redirUrl, absoluteUrlTest.Substring(key.Length));
							redirection = new URLRedirection()
							{
								RedirectURL = redirUrl,
								UserAgents = redirectionTmp.UserAgents,
								Content = redirectionTmp.Content,
								DestinationLanguageCode = redirectionTmp.DestinationLanguageCode,
								HTTPStatusCode = redirectionTmp.HTTPStatusCode,
								OriginLanguageCodes = redirectionTmp.OriginLanguageCodes,
								OtherRedirections = redirectionTmp.OtherRedirections
							};

						}
						else
						{
							redirection = redirectionTmp;
						}

						sbTraceMessage.AppendFormat("Redirecting wildcard URL {0} to {1}.", absoluteUrlTest, redirection.RedirectURL);
					}
				}
			}

			if (redirection == null)
			{
				//no redirection necessary - url's don't match
				return false;
			}

			if (!redirection.MatchUserAgentAndLanguage(sbTraceMessage, languageCode))
			{
				//handle multiple matches
				string originUrl = redirection.OriginalURL;
				List<URLRedirection> lstRedir = redirection.OtherRedirections;
				redirection = null;

				if (lstRedir != null && lstRedir.Count > 0)
				{
					sbTraceMessage.AppendFormat("\r\nChecking {0} other redirections for url {1}.", lstRedir.Count, originUrl);
					foreach (URLRedirection otherRedirection in lstRedir)
					{
						if (otherRedirection.MatchUserAgentAndLanguage(sbTraceMessage, languageCode))
						{

							redirection = otherRedirection;
							break;
						}
					}
				}
			}

			if (redirection != null)
			{
				//redirect
				string redirectUrl = redirection.RedirectURL;
				if (string.IsNullOrEmpty(redirectUrl))
				{
					sbTraceMessage.Append("Could not redirect - no redirect url specified...");
					Agility.Web.Tracing.WebTrace.WriteVerboseLine(sbTraceMessage.ToString());
					return false;
				}

				if (redirectUrl.StartsWith("~/"))
				{
					//ALWAYS ASSUME WE ARE RUNNING AS A WEBSITE
					redirectUrl = redirectUrl.Substring(1);										 
				}


				AgilityContext.HttpContext.Response.Clear();
				AgilityContext.HttpContext.Response.Headers["Location"] = redirectUrl;
				AgilityContext.HttpContext.Response.StatusCode = redirection.HTTPStatusCode;
				
				sbTraceMessage.AppendFormat("\r\nUsing HTTP {0}", redirection.HTTPStatusCode);

				//set the new language if we have to...
				if (!string.IsNullOrEmpty(redirection.DestinationLanguageCode)
					&& !string.Equals(redirection.DestinationLanguageCode, languageCode))
				{
					sbTraceMessage.AppendFormat("\r\nSettings language to {0}", redirection.DestinationLanguageCode);
					AgilityContext.LanguageCode = redirection.DestinationLanguageCode;
				}
				
				//write out a status message
				Agility.Web.Tracing.WebTrace.WriteVerboseLine(sbTraceMessage.ToString());
				return true;
			}

			return false;


		}

		/// <summary>
		/// Figure out what the channel is from the given absolute URL.  If a non-null string is returned, the request should be redirected to that URL.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="userAgent"></param>
		/// <returns></returns>
		internal static string CalculateChannel(string url, string userAgent, bool setCookies = true)
		{
			Agility.Web.AgilityContentServer.AgilityDigitalChannel currentChannel = null;
			Agility.Web.AgilityContentServer.AgilityDigitalChannelList channelList = BaseCache.GetDigitalChannels(AgilityContext.WebsiteName);

			string channelCookieName = string.Format("{0}_AgilityChannel", AgilityContext.WebsiteName);

			var context = AgilityContext.HttpContext;

			//check for overridden channel via cookie or via querystring
			string overrideChannelIDStr = context.Request.Query["AgilityChannelID"];
			string overrideChannelName = context.Request.Query["AgilityChannel"];
			int overrideChannelID = -1;
			if (int.TryParse(overrideChannelIDStr, out overrideChannelID))
			{
				//query string with ID...
				currentChannel = channelList.Channels.FirstOrDefault(c => c.ID == overrideChannelID);

				//if this has been set, set a session cookie to capture this...
				if (currentChannel != null)
				{
					if (setCookies)
					{
						context.Response.Cookies.Append(channelCookieName, currentChannel.ReferenceName);
					}
					overrideChannelName = currentChannel.ReferenceName;
				}
			}
			else if (!string.IsNullOrEmpty(overrideChannelName))
			{
				//query string with name...
				currentChannel = channelList.Channels.FirstOrDefault(c => string.Equals(c.ReferenceName, overrideChannelName, StringComparison.CurrentCultureIgnoreCase));

				//if this has been set, set a session cookie to capture this...
				if (currentChannel != null && setCookies)
				{
					context.Response.Cookies.Append(channelCookieName, currentChannel.ReferenceName);
				}
			}
			else
			{
				//check to see if the channel has been set via a cookie...
				string cookieValue = context.Request.Cookies[channelCookieName];
				if (!string.IsNullOrEmpty(cookieValue))
				{
					overrideChannelName = cookieValue;
					currentChannel = channelList.Channels.FirstOrDefault(c => string.Equals(c.ReferenceName, overrideChannelName, StringComparison.CurrentCultureIgnoreCase));
				}
			}

			//if we get this far, we need to look at User Agent matching to TRY and derive the correct url that we should be loading
			if (currentChannel == null)
			{
				foreach (var channel in channelList.Channels.Where(c => c.DigitalChannelDomains != null))
				{
					foreach (var channelDomain in channel.DigitalChannelDomains.Where(cd => cd.ForceUserAgentsToThisChannel))
					{
						//check user agents...
						if (channelDomain.UserAgentFilters == null || channelDomain.UserAgentFilters.Length == 0 || string.IsNullOrEmpty(userAgent)) continue;
						string userAgentMatch = channelDomain.UserAgentFilters.FirstOrDefault(u => userAgent.IndexOf(u, StringComparison.CurrentCultureIgnoreCase) != -1);
						if (userAgentMatch != null)
						{
							//found the agent

							if (currentChannel != null && currentChannel.ID == channel.ID)
							{
								//we are already on this channel. do nothing.
								break;
							}


							if (url.StartsWith(channelDomain.DomainUrl, StringComparison.CurrentCultureIgnoreCase))
							{								
								//the domain matches, so we don't need to change the url, just the channel...
								currentChannel = channel;

								//set the channel cookie
								if (setCookies)
								{
									context.Response.Cookies.Append(channelCookieName, currentChannel.ReferenceName);
								}

							}
							else
							{

								//switch to the correct channel, keep the path if neccessary
								string redirUrl = channelDomain.DomainUrl;
								if (Current.Settings.KeepUrlPathOnForcedChannel)
								{
									redirUrl = redirUrl.TrimEnd('/');
									Uri uri = new Uri(url);
									string urlPath = uri.PathAndQuery;									
									redirUrl = string.Format("{0}{1}", redirUrl, urlPath);
								}

								return redirUrl;
							}

						}

					}
				}
			}

			if (currentChannel == null)
			{
				//check which channel is loading based on Url and User Agent
				Agility.Web.AgilityContentServer.AgilityDigitalChannel defaultChannel = channelList.Channels[0];

				//loop the channels and try and resolve this domain
				foreach (var channel in channelList.Channels.Where(c => c.DigitalChannelDomains != null))
				{

					if (channel.IsDefaultChannel) defaultChannel = channel;

					//try to match this channel to this domain
					var channelDomain = channel.DigitalChannelDomains.FirstOrDefault(c => url.StartsWith(c.DomainUrl, StringComparison.CurrentCultureIgnoreCase));
					if (channelDomain != null)
					{
						
						//if the domain matches, check user agents...
						if (channelDomain.UserAgentFilters != null && channelDomain.UserAgentFilters.Length > 0)
						{
							string userAgentMatch = channelDomain.UserAgentFilters.FirstOrDefault(u => userAgent.IndexOf(u, StringComparison.CurrentCultureIgnoreCase) != -1);
							if (userAgent != null)
							{
								//found the agent, we are good
								currentChannel = channel;
								break;
							}
						}
						else
						{
							//don't have to check channels, we are good
							currentChannel = channel;
							break;
						}
					}
				}


				//check if we have the current channel, default to the "Default" channel if we need to
				if (currentChannel == null)
				{
					currentChannel = defaultChannel;

				}
			}


			//set the current channel for this context
			DigitalChannel requestchannel = new DigitalChannel(currentChannel);
			AgilityContext.CurrentChannel = requestchannel;

			if (currentChannel != null && currentChannel.DigitalChannelDomains != null)
			{

				var cd = currentChannel.DigitalChannelDomains.FirstOrDefault(c => url.StartsWith(c.DomainUrl, StringComparison.CurrentCultureIgnoreCase));
				if (cd != null)
				{

					//check for forced language on this channel domain..
					if (cd.XForceDefaultLanguageToThisDomain && (!string.IsNullOrEmpty(cd.XDefaultLanguage)))
					{

						if (cd.XDefaultLanguage != AgilityContext.LanguageCode)
						{
							AgilityContext.LanguageCode = cd.XDefaultLanguage;
						}

						//
					}
				}
				
			}
			

			return null;
		}


		internal static void ParseLanguageCode(RouteData RouteData, ref string redirectUrl)
		{

			string pagePath = RouteData.Values["sitemapPath"] as string;
			string languageCode = RouteData.Values["languageCode"] as string;
			if (string.IsNullOrEmpty(languageCode))
			{
				ParseLanguageCode(ref pagePath, ref redirectUrl);
			}
			else
			{

				ParseLanguageCode(ref pagePath, ref languageCode, ref redirectUrl);
			}

			RouteData.Values["sitemapPath"] = pagePath;
			RouteData.Values["languageCode"] = languageCode;
			if (!string.IsNullOrEmpty(languageCode))
			{
				AgilityContext.LanguageCode = languageCode;
			}

		}

		internal static void ParseLanguageCode(ref string pagePath, ref string redirectUrl)
		{
			string pathWithOutSlash = pagePath;
			if (pathWithOutSlash == null) pathWithOutSlash = string.Empty;
			if (pathWithOutSlash.StartsWith("~/")) pathWithOutSlash = pathWithOutSlash.Substring(2);
			if (pathWithOutSlash.StartsWith("/")) pathWithOutSlash = pathWithOutSlash.Substring(1);

			string languageCode = null;

			//strip out the language from the url (first folder path)
			int index = pathWithOutSlash.IndexOf("/");
			if (index > 0)
			{
				languageCode = pathWithOutSlash.Substring(0, index);
				pathWithOutSlash = pathWithOutSlash.Substring(index + 1);
			}
			else
			{
				languageCode = pathWithOutSlash;
				pathWithOutSlash = string.Empty;
			}

			ParseLanguageCode(ref pathWithOutSlash, ref languageCode, ref redirectUrl);
			
			if (pathWithOutSlash.StartsWith("/")) pathWithOutSlash = string.Format("~{0}", pathWithOutSlash);
			if (!pathWithOutSlash.StartsWith("~/")) pathWithOutSlash = string.Format("~/{0}", pathWithOutSlash);

			pagePath = pathWithOutSlash;

		}

		internal static void ParseLanguageCode(ref string pagePath, ref string languageCodeFromPath, ref string redirectUrl)
		{

			AgilityContentServer.AgilityDomainConfiguration config = BaseCache.GetDomainConfiguration(AgilityContext.WebsiteName);

			if (config == null) throw new Exception("Could not access the Domain Configuration.");

			string originalLanguage = AgilityContext.LanguageCode;

			var context = AgilityContext.HttpContext;

			//if the lang is in the query string, set the context based on this...
			string qlang = context.Request.Query["lang"];
			bool wasLanguageSetFromQueryString = false;
			string originalRedirect = null;
			if (!string.IsNullOrEmpty(qlang))
			{

				AgilityContentServer.AgilityLanguage lang = null;
				if (config != null && config.Languages != null)
				{
					lang = config.Languages.FirstOrDefault(l => string.Equals(l.LanguageCode, qlang, StringComparison.CurrentCultureIgnoreCase));
				}

				if (lang != null)
				{
					//redirect to the new language with a relative url
					redirectUrl = "/"; //assume a website
					
					//add on the query string...
					if (!string.IsNullOrEmpty(context.Request.QueryString.Value))
					{												
						redirectUrl = Agility.Web.Util.Url.RemoveQueryString(context.Request.GetEncodedPathAndQuery(), "lang");
					}

					AgilityContext.LanguageCode = lang.LanguageCode;
					wasLanguageSetFromQueryString = true;

				}
				originalRedirect = redirectUrl;

			}

			bool wasLangInUrl = false;

			//if the language code portion of the URL is passed in with the path, then ensure it is a valid language code
			if (!string.IsNullOrEmpty(languageCodeFromPath))
			{
				string inputLanguageCode = languageCodeFromPath;

				AgilityContentServer.AgilityLanguage lang = config.Languages.FirstOrDefault(l => string.Equals(l.LanguageCode, inputLanguageCode, StringComparison.CurrentCultureIgnoreCase));
				if (lang == null)
				{
					//not a match, set the language to null and append them together 
					if (string.IsNullOrEmpty(pagePath))
					{
						pagePath = string.Format("{0}", languageCodeFromPath);
					}
					else
					{
						pagePath = string.Format("{0}/{1}", languageCodeFromPath, pagePath);
					}

					languageCodeFromPath = AgilityContext.LanguageCode;
					

				}
				else
				{

					wasLangInUrl = true;

					//it is a match, but doesn't match the current language, switch the current language
					//unless the current language was set by the query string, in which case we redirect
					if (!wasLanguageSetFromQueryString)
					{
						if (!string.Equals(languageCodeFromPath, AgilityContext.LanguageCode))
						{
							//change the context language based on the url...
							AgilityContext.LanguageCode = languageCodeFromPath;
						}
					}

					if (!string.IsNullOrEmpty(redirectUrl))
					{
						redirectUrl = pagePath;
					}
				}
			}

			if (wasLanguageSetFromQueryString)
			{
				//lang is NOT in the url...
				//try to get the page from the sitemap in the old lang...

				string lookupPath = pagePath;
				if (!lookupPath.StartsWith("/")) lookupPath = string.Format("/{0}", lookupPath);
				if (lookupPath.StartsWith("/")) lookupPath = string.Format("~{0}", lookupPath);

				string thisLanguage = AgilityContext.LanguageCode;

				if (AgilityContext.LanguageCode != thisLanguage)
				{
					AgilityContext.LanguageCode = thisLanguage;
				}

				//WE MAY HAVE TO REDIRECT TO A DIFFERENT CHANNEL DOMAIN...
				if (AgilityContext.CurrentChannel != null)
				{
					//find if there is a channel domain that is the default "switch" domain for this language...
					var channelDomain = AgilityContext.CurrentChannel.Domains.FirstOrDefault(d => d.DefaultLanguage == AgilityContext.LanguageCode && d.ForceDefaultLanguageToThisDomain);
					if (channelDomain != null)
					{
						string rootUrl = channelDomain.URL;
						if (rootUrl.EndsWith("/")) rootUrl = rootUrl.Substring(0, rootUrl.Length - 1);

						//redirect to the new language with an absolute url
						redirectUrl = string.Format("{0}{1}", rootUrl, redirectUrl.Substring(1));
					}

				}


			}

			//if we are redirecting, we MIGHT need to add the language code to the url...
			if (! wasLangInUrl
				&& config.IncludeLanguageCodeInUrl)
			{				
				//we need to redirect... build the redirect url...
				if (string.IsNullOrEmpty(redirectUrl))
				{
					redirectUrl = "/"; //as

					//add on the query string...
					if (!string.IsNullOrEmpty(context.Request.QueryString.Value))
					{
						redirectUrl = Agility.Web.Util.Url.RemoveQueryString(string.Format("{0}{1}", redirectUrl, context.Request.QueryString.Value), "lang");
						
					}

					if (!string.IsNullOrEmpty(context.Request.Path.Value))
					{
						redirectUrl = string.Format("{0}/{1}", redirectUrl, context.Request.Path.Value);
					}
				}

				
				
				if (redirectUrl.StartsWith("~/")) redirectUrl = redirectUrl.Substring(2);
				if (redirectUrl.StartsWith("/")) redirectUrl = redirectUrl.Substring(1);
				redirectUrl = string.Format("~/{0}/{1}", AgilityContext.LanguageCode, redirectUrl);

			}

		}

		//EVENTS FOR HTTP MODULE ROUTING OVERRIDES
		public static event BeforeRoutingEvent BeforeRouting = delegate { return true; };
		public static event BeforeRedirectsEvent BeforeRedirects = delegate { return true; };
		public static event AfterRoutingEvent AfterRouting = delegate {  };

		internal static bool TriggerBeforeRedirects()
		{
			return BeforeRedirects();
		}

		internal static bool TriggerBeforeRouting(ref string pagePath)
		{
			return BeforeRouting(ref pagePath);
		}

		internal static void TriggerAfterRouting()
		{
			AfterRouting();
		}

		//TODO: implement this...
        //internal static bool HandleDangerousRequest(ErrorTraceElement elem, HttpRequest request, HttpResponse response)
        //{
        //    string key = (elem.RequestThrottleIdentifier == RequestThrottleIdentifier.IpAddress) ? request.UserHostAddress : request.UserAgent;
        //    int max = elem.RequestThrottleMax;
        //    bool throttled = false;

        //    //this requester has hit their limit, throw unauthorized
        //    if (AgilityContext.RequestThrottler.ContainsKey(key) 
        //        && AgilityContext.RequestThrottler[key] >= max)
        //    {
        //        response.Clear();
        //        response.Status = "302 Found";
        //        response.StatusCode = 302;
        //        response.RedirectLocation = elem.RequestThrottleRedirect;

        //        HttpContext.Current.ApplicationInstance.CompleteRequest();

        //        throttled = true;
        //    }

        //    return throttled;
        //}
	}

	public delegate bool BeforeRoutingEvent(ref string pagePath);
	public delegate bool BeforeRedirectsEvent();
	public delegate void AfterRoutingEvent();
}
