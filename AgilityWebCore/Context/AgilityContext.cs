using Agility.Web.AuthorizationHandlers;
using Agility.Web.Caching;
using Agility.Web.Configuration;
using Agility.Web.Enum;
using Agility.Web.HttpModules;
using Agility.Web.Objects;
using Agility.Web.Providers;
using Agility.Web.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Agility.Web
{
    /// <summary>
    /// Class that makes properties about the current Agility Context available.
    /// </summary>
    public class AgilityContext
    {

        private static IHttpContextAccessor m_httpContextAccessor;
        [ThreadStatic]
        private static bool isContextNull;

        public static void Configure(IApplicationBuilder app, IHostEnvironment env, bool useResponseCaching = true)
        {
            try
            {

                m_httpContextAccessor = app.ApplicationServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

                if (useResponseCaching)
                {
                    app.UseMiddleware<AgilityResponseCacheMiddleware>();
                }

                Current.HostingEnvironment = env;

                if (!System.IO.Directory.Exists(Current.Settings.RootedContentCacheFilePath))
                {
                    System.IO.Directory.CreateDirectory(Current.Settings.RootedContentCacheFilePath);
                }

                CacheDependency.fileProvider = new PhysicalFileProvider(Current.Settings.RootedContentCacheFilePath);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error occurred in configure: ", ex);
            }
        }

        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                //required to use inline code
                services.Configure<MvcRazorRuntimeCompilationOptions>(opts => opts.FileProviders.Add(new AgilityDynamicCodeProvider()));
                services.Configure<MvcRazorRuntimeCompilationOptions>(opts => opts.FileProviders.Add(new AgilityDynamicModuleProvider()));


                var settings = configuration.GetSection("Agility").Get<Settings>();

                services.AddAuthorization(options =>
                {
                    options.AddPolicy("CorrectWebsite", policyCorrectWebsite =>
                    {
                        policyCorrectWebsite.Requirements.Add(new CorrectWebsiteRequirement(settings.WebsiteName, settings.SecurityKey));
                    });
                });

                services.AddSingleton<IAuthorizationHandler, CorrectWebsiteAuthorizationHandler>();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error occurred in configure services: ", ex);
            }

        }


        public static HttpContext HttpContext
        {
            get
            {
                if (isContextNull)
                {
                    return null;
                }

                return m_httpContextAccessor.HttpContext;
            }
            set
            {
                if (value == null)
                {
                    isContextNull = true;
                }
                //do nothing...
            }
        }



        public static AgilitySiteMap AgilitySiteMap
        {
            get
            {
                return new AgilitySiteMap();

            }
        }



        private static object _contentAccessorLockObj = new object();
        private static bool _isIContentAccessorSet = false;
        private static IContentAccessor _contentAccessor = null;
        internal static IContentAccessor ContentAccessor
        {
            get
            {
                //string hackpath = @"C:\Resources\Directory\68545f0ef4994a0a84aded270200dff5.AgilityWebsiteHost.LogFiles\testplugin.txt";


                if (!_isIContentAccessorSet)
                {
                    lock (_contentAccessorLockObj)
                    {

                        //check if the variable has been set since we were waiting for the lock...
                        if (!_isIContentAccessorSet)
                        {

                            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
                            string path = Path.Combine(dir, "Agility.Web.Azure.dll");


                            if (File.Exists(path))
                            {
                                AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);
                                Assembly plugin = AppDomain.CurrentDomain.Load(assemblyName);
                                if (plugin != null)
                                {

                                    foreach (Type type in plugin.GetTypes())
                                    {

                                        if (type.GetInterface("IContentAccessor") != null)
                                        {
                                            System.Reflection.ConstructorInfo cons = type.GetConstructor(System.Type.EmptyTypes);
                                            if (cons != null)
                                            {
                                                _contentAccessor = cons.Invoke(new Object[0]) as IContentAccessor;
                                                _isIContentAccessorSet = true;
                                                return _contentAccessor;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                }

                return _contentAccessor;
            }

            //set
            //{
            //    _isIContentAccessorSet = true;
            //    _contentAccessor = value;
            //}
        }

        /// <summary>
        /// Gets/sets whether the current site is in preview mode.
        /// </summary>
        public static bool IsPreview
        {
            get
            {
                //not in a web context (sync thread) assume LIVE
                if (AgilityContext.HttpContext == null)
                {
                    return false;
                }

                //check the current mode from a cookie
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_IsPreview";

                //attempt to get the value from the request, then cookie
                object tmp = AgilityContext.HttpContext.Items[cookieName];
                if (tmp is bool) return (bool)tmp;

                string cookieValue = AgilityContext.HttpContext.Request.Cookies[cookieName];
                if (!string.IsNullOrWhiteSpace(cookieValue))
                {
                    bool _isPreview = false;
                    if (bool.TryParse(cookieValue, out _isPreview))
                    {
                        //set the value, so that it will slide
                        IsPreview = _isPreview;

                        //return the value
                        return _isPreview;
                    }
                }

                //default is not preview
                return false;
            }

            set
            {

                //set the isPreview as a bool stored in 1 day cookie and in context
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_IsPreview";

                //context
                AgilityContext.HttpContext.Items[cookieName] = value;

                //cookie				
                AgilityContext.HttpContext.Response.Cookies.Append(cookieName, value.ToString());
            }
        }

        /// <summary>
        /// Gets/sets the current preview datetime.  
        /// This allows the user to preview content based on a date in future with Release/Pull dates taken into account.
        /// </summary>
        /// <remarks>
        /// If this is DateTime.MinValue, than the current date should be used.
        /// </remarks>
        public static DateTime PreviewDateTime
        {
            get
            {
                //not in a web context (sync thread) assume LIVE, or not previewing, or not in development mode
                if (AgilityContext.HttpContext == null || (!IsPreview && !Current.Settings.DevelopmentMode))
                {
                    return DateTime.MinValue;
                }

                //check the current mode from a cookie
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_PreviewDateTime";

                //attempt to get the value from the request, then cookie
                object tmp = AgilityContext.HttpContext.Items[cookieName];
                if (tmp is DateTime) return (DateTime)tmp;

                string cookieValue = AgilityContext.HttpContext.Request.Cookies[cookieName];
                if (!string.IsNullOrWhiteSpace(cookieValue))
                {
                    DateTime _dt = DateTime.MinValue;
                    if (DateTime.TryParse(cookieValue, out _dt) && _dt >= DateTime.Now)
                    {
                        //set the value, so that it will slide
                        PreviewDateTime = _dt;

                        //return the value
                        return _dt;
                    }
                }

                //default is min value
                return DateTime.MinValue;
            }

            set
            {
                //set the isPreview as a bool stored in 1 day cookie and in context
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_PreviewDateTime";
                string cookieValue = value.ToString("yyyy-MM-dd HH:mm:ss");

                //context
                AgilityContext.HttpContext.Items[cookieName] = value;

                CookieOptions cookieOptions = new CookieOptions();

                if (!string.IsNullOrEmpty(Current.Settings.CookieDomain))
                {
                    cookieOptions.Domain = Current.Settings.CookieDomain;
                }

                AgilityContext.HttpContext.Response.Cookies.Append(cookieName, cookieValue, cookieOptions);
            }
        }


        /// <summary>
        /// Gets whether this page request is currently in template preview mode.
        /// </summary>
        public static bool IsTemplatePreview
        {
            get
            {
                object o = AgilityContext.HttpContext == null ? null : AgilityContext.HttpContext.Items["Agility.Web.Context.IsTemplatePreview"];
                if (o is bool) return (bool)o;
                return false;
            }
            set
            {
                AgilityContext.HttpContext.Items["Agility.Web.Context.IsTemplatePreview"] = value;
            }
        }

        internal static AgilityContentServer.AgilityPageDefinition CurrentPageTemplateInPreview
        {
            get
            {
                return AgilityContext.HttpContext.Items["Agility.Web.Context.CurrentPageTemplateInPreview"] as AgilityContentServer.AgilityPageDefinition;

            }
            set
            {
                AgilityContext.HttpContext.Items["Agility.Web.Context.CurrentPageTemplateInPreview"] = value;
            }
        }

        /// <summary>
        /// This returns the current Mode of the site (Staging, Live)
        /// </summary>
        public static Mode CurrentMode
        {
            get
            {


                if (Current.Settings.DevelopmentMode == true || IsTemplatePreview)
                {
                    //locked in staging mode via the web.config, or they are previewing a template
                    return Mode.Staging;
                }

                //not in a web context (sync thread) assume LIVE
                if (AgilityContext.HttpContext == null)
                {
                    return Mode.Live;
                }

                //check the current mode from a cookie				
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_Mode";

                //attempt to get the value from the request, then cookie
                object tmp = AgilityContext.HttpContext.Items[cookieName];
                if (tmp is Mode) return (Mode)tmp;



                string cookieValue = AgilityContext.HttpContext.Request.Cookies[cookieName];
                if (!string.IsNullOrWhiteSpace(cookieValue))
                {
                    int modeEnum = 0;
                    if (int.TryParse(cookieValue, out modeEnum))
                    {
                        //set the value, so that it will slide
                        CurrentMode = (Mode)modeEnum;

                        //the the value
                        return (Mode)modeEnum;
                    }
                }

                //default is live mode
                return Mode.Live;
            }

            set
            {
                //set the mode as an integer stored in a 30 minute cookie and in context
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_Mode";

                //context
                AgilityContext.HttpContext.Items[cookieName] = value;

                //cookie
                string cookieValue = (((int)value).ToString());

                CookieOptions cookieOptions = new CookieOptions();

                if (!string.IsNullOrEmpty(Current.Settings.CookieDomain))
                {
                    cookieOptions.Domain = Current.Settings.CookieDomain;
                }

                AgilityContext.HttpContext.Response.Cookies.Append(cookieName, cookieValue, cookieOptions);

            }
        }

        /// <summary>
        /// Gets/sets the language code that is stored in a cookie for a given user.
        /// </summary>
        public static string LanguageCode
        {
            get
            {
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_Language";


                //attempt to get the language value from the request, then cookie
                string tmp = AgilityContext.HttpContext.Items[cookieName] as string;
                if (string.IsNullOrEmpty(tmp))
                {
                    tmp = AgilityContext.HttpContext.Request.Cookies[cookieName];
                }

                if (string.IsNullOrEmpty(tmp))
                {
                    //Is Blank, check the channel, then the Domain and set the cookie.		
                    if (Domain != null)
                    {
                        var channel = CurrentChannel;
                        if (channel != null)
                        {

                            string url = UriHelper.GetEncodedUrl(AgilityContext.HttpContext.Request);

                            var channelDomain = channel.Domains.FirstOrDefault(c => url.StartsWith(c.URL, StringComparison.CurrentCultureIgnoreCase));
                            if (channelDomain != null)
                            {
                                //found the channel domain for the current request, flip the lang if possible...
                                if (!string.IsNullOrEmpty(channelDomain.DefaultLanguage))
                                {
                                    tmp = channelDomain.DefaultLanguage;
                                    LanguageCode = tmp;
                                    return tmp;
                                }

                            }
                        }

                        tmp = Domain.LanguageCode;
                        LanguageCode = tmp;
                    }
                    else
                    {
                        //as a last resort, default to en-us
                        return "en-us";
                    }
                }

                return tmp;
            }


            set
            {
                string cookieName = Current.Settings.WebsiteName.Replace(" ", string.Empty) + "_Language";


                //check if the value is already set in context...
                string existingLangCodeContext = AgilityContext.HttpContext.Items[cookieName] as string;

                //don't change if ==
                if (existingLangCodeContext == value) return;

                if (existingLangCodeContext == null)
                {
                    string cookieVal = AgilityContext.HttpContext.Request.Cookies[cookieName];
                    if (cookieVal != null && cookieVal == value)
                    {
                        //set the value in context, leave the cookie as the same
                        AgilityContext.HttpContext.Items[cookieName] = value;
                        return;
                    }
                }

                //AT THIS POINT:
                /*
				 * New value is DIFFERENT from Context Value, 
				 * OR no context value set and different from cookie.
				 */


                //double check that this language code is viable...
                var domainCheck = AgilityContext.Domain.Languages.FirstOrDefault(l => string.Equals(l.LanguageCode, value, StringComparison.CurrentCultureIgnoreCase));

                if (domainCheck == null)
                {
                    throw new ApplicationException(string.Format("The language code {0} is not valid.", value));
                }


                //add a cookie and context  variable for the given value				
                CookieOptions cookieOptions = new CookieOptions();

                if (CurrentMode == Mode.Staging)
                {
                    //language cookie should only last 30 minutes if we are in staging mode
                    cookieOptions.Expires = DateTime.Now.AddMinutes(30);
                }
                else
                {
                    cookieOptions.Expires = DateTime.Now.AddMonths(1);
                }

                if (!string.IsNullOrEmpty(Current.Settings.CookieDomain))
                {
                    cookieOptions.Domain = Current.Settings.CookieDomain;
                }


                AgilityContext.HttpContext.Response.Cookies.Append(cookieName, value, cookieOptions);

                //ensure the page DOES NOT cache when we set this cookie.
                AgilityContext.CacheResponse = false;

                AgilityContext.HttpContext.Items[cookieName] = value;

            }
        }


        /// <summary>
        /// Context Variable to tells Agility whether or not to cache a response.
        /// //TODO: implement this...
        /// </summary>
        public static bool CacheResponse
        {

            get
            {
                object o;
                if (HttpContext.Items.TryGetValue("CacheResponse", out o))
                {
                    if (o is bool) return (bool)o;
                }

                return true;
            }
            set
            {
                HttpContext.Items["CacheResponse"] = value;
            }
        }


        internal static string LastLoadedDynamicPageFormulaItemCacheKey = "Agility.Web.AgilityContext.LastLoadedDynamicPageFormulaItem";
        internal static string DynamicPageItemRowCacheKey = "Agility.Web.AgilityContext.DynamicPageItemRow";

        internal static string DynamicPageFormulaItemCacheKey = "Agility.Web.AgilityContext.DynamicFormulaItem";

        private static object _lockObjDynamicPageFormulaItem = new object();

        internal static AgilityContentServer.DynamicPageFormulaItem LastLoadedDynamicPageFormulaItem
        {
            get
            {
                return AgilityContext.HttpContext.Items[LastLoadedDynamicPageFormulaItemCacheKey] as AgilityContentServer.DynamicPageFormulaItem;
            }
            set
            {
                AgilityContext.HttpContext.Items[LastLoadedDynamicPageFormulaItemCacheKey] = value;
            }
        }



        internal static AgilityContentServer.DynamicPageFormulaItem DynamicPageFormulaItem
        {
            get
            {
                return AgilityContext.HttpContext.Items[DynamicPageFormulaItemCacheKey] as Agility.Web.AgilityContentServer.DynamicPageFormulaItem;
            }
            set
            {
                AgilityContext.HttpContext.Items[DynamicPageFormulaItemCacheKey] = value;
            }
        }


        /// <summary>
        /// Gets the DataRow that represents the current dynamic page item that is being rendered.
        /// This item may be at the current page level, or a parent page level.
        /// </summary>
        public static DataRow DynamicPageItemRow
        {
            get
            {

                AgilityContentServer.DynamicPageFormulaItem dpItem = DynamicPageFormulaItem;
                if (dpItem == null) return null;

                DataRow row = AgilityContext.HttpContext.Items[DynamicPageItemRowCacheKey] as DataRow;
                if (row != null) return row;


                lock (_lockObjDynamicPageFormulaItem)
                {

                    //check context again...
                    row = AgilityContext.HttpContext.Items[DynamicPageItemRowCacheKey] as DataRow;
                    if (row != null) return row;

                    AgilityContentServer.AgilityContent content = BaseCache.GetContent(dpItem.ContentReferenceName, AgilityContext.LanguageCode, AgilityContext.WebsiteName);
                    row = content.GetItemByContentID(dpItem.ContentID);
                    if (row != null)
                    {
                        AgilityContext.HttpContext.Items[DynamicPageItemRowCacheKey] = row;
                    }
                }

                return row;
            }

        }


        /// <summary>
        /// Gets the AgilityContentItem that represents the current dynamic page item that is being rendered.
        /// This item may be at the current page level, or a parent page level.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDynamicPageItem<T>() where T : AgilityContentItem
        {
            DataRow row = DynamicPageItemRow;

            if (row == null) return null;

            Type type = typeof(T);
            ConstructorInfo constr = type.GetConstructor(System.Type.EmptyTypes);
            return AgilityContentRepository<T>.ConvertDataRowToObject(constr, DynamicPageItemRow, LanguageCode, DynamicPageFormulaItem.ContentReferenceName);
        }


        /// <summary>
        /// Gets the AgilityContentItem that represents the dynamic page item that exists for this sitemap node.  
        /// If the given node does not represent a dynamic page, it will return null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="node"></param>
        /// <returns></returns>
        public static T GetDynamicPageItem<T>(AgilitySiteMapNode node) where T : AgilityContentItem
        {

            AgilityDynamicSiteMapNode anode = node as AgilityDynamicSiteMapNode;

            if (anode == null || string.IsNullOrEmpty(anode.ReferenceName) || anode.ContentID < 1) return null;



            lock (_lockObjDynamicPageFormulaItem)
            {
                //check context again...
                AgilityContentServer.AgilityContent content = BaseCache.GetContent(anode.ReferenceName, AgilityContext.LanguageCode, AgilityContext.WebsiteName);
                DataRow row = content.GetItemByContentID(anode.ContentID);
                if (row != null)
                {
                    Type type = typeof(T);
                    ConstructorInfo constr = type.GetConstructor(System.Type.EmptyTypes);
                    return AgilityContentRepository<T>.ConvertDataRowToObject(constr, row, AgilityContext.LanguageCode, anode.ReferenceName);
                }
            }

            return null;


        }


        /// <summary>
        /// Gets the current Page object for the currently executing url and language.
        /// </summary>
        public static AgilityPage Page
        {
            get
            {

                //check context first
                AgilityPage _page = HttpContext.Items["Agility.Web.AgilityContext.Page"] as AgilityPage;
                if (_page == null)
                {
                    //TODO: verify this path is correct to get the page from...
                    string url = HttpContext.Request.Path;

                    url = HttpUtility.UrlPathEncode(url);

                    _page = Data.GetPage(url);

                    HttpContext.Items["Agility.Web.AgilityContext.Page"] = _page;
                }


                return _page;
            }
            set
            {
                HttpContext.Items["Agility.Web.AgilityContext.Page"] = value;
            }
        }


        /// <summary>
        /// Gets the Sitemap object for the current language.
        /// </summary>
        public static Sitemap Sitemap
        {
            get
            {
                Web.Objects.Sitemap siteMap = Data.GetSitemap(LanguageCode);
                if (siteMap == null) throw new Exceptions.AgilityException(string.Format("The sitemap for language {0} could not be found for website {1}.", LanguageCode, WebsiteName));
                return siteMap;
            }
        }


        /// <summary>
        /// Gets the Domain Configuration for the current website.
        /// </summary>
        public static Web.Objects.Config Domain
        {
            get
            {
                return Data.GetConfig();

            }
        }

        public static DateTime DevModeRefreshCheckDate
        {
            get
            {
                string cacheKey = "Agility.Web.AgilityContext.DevModeRefreshCheckDate";
                object o = AgilityContext.HttpContext.Items[cacheKey];
                if (o is DateTime) return (DateTime)o;

                string filepath = string.Format("{0}/{1}/Staging/DevModeRefreshCheckDate.bin",
                        Current.Settings.ContentCacheFilePath,
                        AgilityContext.WebsiteName);

                if (File.Exists(filepath))
                {
                    DateTime dt = BaseCache.ReadFile<DateTime>(filepath);
                    AgilityContext.HttpContext.Items[cacheKey] = dt;
                    return dt;
                }
                return DateTime.MinValue;
            }
            set
            {

                string filepath = string.Format("{0}/{1}/Staging/DevModeRefreshCheckDate.bin",
                        Current.Settings.ContentCacheFilePath,
                        AgilityContext.WebsiteName);

                BaseCache.WriteFile(value, filepath);
            }

        }

        /// <summary>
        /// Gets the default (first declared) website name from the configuration settings.
        /// </summary>
        public static string WebsiteName
        {
            get
            {


                return Current.Settings.WebsiteName;
            }
        }



        /// <summary>
        /// Keep track of all the items that have been loaded into cache for this request.
        /// The module will add these to the OutputCache's dependancies for this request.
        /// </summary>
        internal static List<string> OutputCacheKeys
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    List<string> lst = AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.OutputCacheKeys"] as List<string>;
                    if (lst == null)
                    {
                        lst = new List<string>();
                        AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.OutputCacheKeys"] = lst;
                    }
                    return lst;
                }
                else
                {
                    return new List<string>();
                }


            }
        }

        internal static HashSet<int> LoadedContentItemIDs
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    HashSet<int> lst = AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.LoadedContentItemIDs"] as HashSet<int>;
                    if (lst == null)
                    {
                        lst = new HashSet<int>();
                        AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.LoadedContentItemIDs"] = lst;
                    }
                    return lst;
                }
                else
                {
                    return new HashSet<int>();
                }


            }
        }

        internal static HashSet<string> ExperimentKeys
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    HashSet<string> lst = AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.ExperimentIDs"] as HashSet<string>;
                    if (lst == null)
                    {
                        lst = new HashSet<string>();
                        AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.ExperimentIDs"] = lst;
                    }
                    return lst;
                }
                else
                {
                    return new HashSet<string>();
                }


            }
        }

        internal static Dictionary<string, Agility.Web.AgilityContentServer.AgilityItemKey> LoadedItemKeys
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    Dictionary<string, Agility.Web.AgilityContentServer.AgilityItemKey> lst = AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.LoadedItemKeys"] as Dictionary<string, Agility.Web.AgilityContentServer.AgilityItemKey>;
                    if (lst == null)
                    {
                        lst = new Dictionary<string, Agility.Web.AgilityContentServer.AgilityItemKey>();
                        AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.LoadedItemKeys"] = lst;
                    }
                    return lst;
                }
                else
                {
                    return new Dictionary<string, Agility.Web.AgilityContentServer.AgilityItemKey>();
                }


            }
        }



        public static string UrlForPreviewBar
        {
            get
            {
                return AgilityContext.HttpContext.Items["Agility.Web.Context.AgilityContext.UrlForPreviewBar"] as string;
            }
            set
            {
                AgilityContext.HttpContext.Items["Agility.Web.Context.AgilityContext.UrlForPreviewBar"] = value;
            }
        }


        public static bool WasPathRewritten
        {
            get
            {
                object o = AgilityContext.HttpContext.Items["Agility.Web.Context.AgilityContext.WasPathRewritten"];
                if (o is bool) return (bool)o;
                return false;
            }
            set
            {
                AgilityContext.HttpContext.Items["Agility.Web.Context.AgilityContext.WasPathRewritten"] = value;
            }
        }


        public static bool BuildAgilityContext()
        {
            //set the application identity
            AgilityHttpModule.SetApplicationIdentity(AgilityContext.HttpContext.Request);

            //check for Resource (ecms.aspx or ecms.ashx)
            string requestPath = AgilityContext.HttpContext.Request.Path.Value.ToLowerInvariant();

            if (requestPath.Contains(AgilityHttpModule.ECMS_DOCUMENTS_KEY)
                || requestPath.Contains(AgilityHttpModule.ECMS_DOCUMENTS_KEY2))
            {
                //handle agility file/document/attachment requests
                AgilityHttpModule.HandleAgilityFileRequest(AgilityContext.HttpContext, AgilityContext.HttpContext.Request, AgilityContext.HttpContext.Response);
                return true;
            }
            else if (requestPath.Contains(AgilityHttpModule.ECMS_RSS_KEY))
            {
                //handle agility rss requests
                //TODO: handle rss AgilityHttpModule.HandleAgilityRssRequest(AgilityContext.HttpContext, AgilityContext.HttpContext.Request, AgilityContext.HttpContext.Response);
                return true;
            }
            else if (requestPath.ToLowerInvariant().Contains(AgilityHttpModule.ECMS_EDITOR_CSS_KEY))
            {
                //handle agility rss requests
                AgilityHttpModule.HandleEditorCssRequest(AgilityContext.HttpContext, AgilityContext.HttpContext.Request, AgilityContext.HttpContext.Response);
                return true;
            }
            else if (requestPath.Contains(AgilityHttpModule.ECMS_ERRORS_KEY))
            {
                //handle agility rss requests
                AgilityHttpModule.HandleErrorsRequest(AgilityContext.HttpContext, AgilityContext.HttpContext.Request, AgilityContext.HttpContext.Response);
                return true;
            }

            /*
			* Handle the postbacks from the Status Panel
			*/
            if (string.Equals(AgilityContext.HttpContext.Request.Method, "post", StringComparison.CurrentCultureIgnoreCase))
            {
                string eventArgument = AgilityContext.HttpContext.Request.Form["agilitypostback"];

                if (!string.IsNullOrEmpty(eventArgument))
                {
                    if (AgilityHttpModule.HandleStatusPanelPostback(eventArgument)) return true;
                }
            }

            //do a quick "precheck" to see if this is a request attempting to preview
            if (!string.IsNullOrEmpty(AgilityContext.HttpContext.Request.Query["agilitypreviewkey"]))
            {
                AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Staging;
            }

            //if (AgilityHttpModule.CheckLanguageSwitch(AgilityContext.HttpContext.Request)) return true;

            return false;
        }

        internal const string CACHEKEY_CURRENTCHANNEL = "Agility.Web.AgilityContext.CurrentChannel";

        internal const string GLOBAL_SCRIPT_SEPARATOR = "###AGILITY###";

        public static DigitalChannel CurrentChannel
        {
            get
            {
                DigitalChannel channel = AgilityContext.HttpContext.Items[CACHEKEY_CURRENTCHANNEL] as DigitalChannel;

                //if the channel HASN'T been set, set it
                if (channel == null)
                {
                    HttpRequest req = AgilityContext.HttpContext.Request;

                    string userAgent = req.Headers["User-Agent"].ToString();
                    string url = UriHelper.GetEncodedUrl(AgilityContext.HttpContext.Request);

                    AgilityHttpModule.CalculateChannel(url, userAgent);
                    channel = AgilityContext.HttpContext.Items[CACHEKEY_CURRENTCHANNEL] as DigitalChannel;
                }

                return channel;
            }
            set
            {
                AgilityContext.HttpContext.Items[CACHEKEY_CURRENTCHANNEL] = value;
            }
        }

        internal const string CACHEKEY_FEATUREDIMAGEURL = "Agility.Web.AgilityContext.FeaturedImageUrl";


        /// <summary>
        /// The image url that will be used for OpenGraph and Twitter Cards.  If this value is not set in code, the first image that has the data-
        /// </summary>
        public static string FeaturedImageUrl
        {
            get
            {
                return AgilityContext.HttpContext.Items[CACHEKEY_FEATUREDIMAGEURL] as string;
            }

            set
            {
                AgilityContext.HttpContext.Items[CACHEKEY_FEATUREDIMAGEURL] = value;
            }
        }

        internal const string CACHEKEY_CANONICALLINK = "Agility.Web.AgilityContext.CanonicalLink";

        /// <summary>
        /// Specifies the preferred URL for duplicate content found on multiple URLs. Absolute URLs only.
        /// </summary>
        public static string CanonicalLink
        {
            get
            {
                return AgilityContext.HttpContext.Items[CACHEKEY_CANONICALLINK] as string;
            }

            set
            {
                AgilityContext.HttpContext.Items[CACHEKEY_CANONICALLINK] = value;
            }
        }

        internal static string AdditionalVaryByCustomString
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    return AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.AdditionalVaryByCustomString"] as string;
                }
                return null;
            }
            set
            {
                if (AgilityContext.HttpContext != null)
                {
                    AgilityContext.HttpContext.Items["Agility.Web.AgilityContext.AdditionalVaryByCustomString"] = value;
                }
            }
        }


        internal const string CACHEKEY_URLREDIRECTIONS = "Agility.Web.AgilityContext.URLRedirections";
        internal const string CACHEKEY_URLREDIRECTIONS_WITHOUTQUERYSTRINGS = "Agility.Web.AgilityContext.URLRedirections.WithoutQueryStrings";
        internal const string CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS = "Agility.Web.AgilityContext.URLRedirections.WithQueryStrings";
        internal const string CACHEKEY_URLREDIRECTIONS_WILDCARD = "Agility.Web.AgilityContext.URLRedirections.Wildcard";

        public static Dictionary<string, URLRedirection> URLRedirections
        {
            get
            {
                string cacheKey = string.Format("{0}_{1}", CACHEKEY_URLREDIRECTIONS, AgilityContext.CurrentMode);

                Dictionary<string, URLRedirection> _urlRedirections = AgilityCache.Get(CACHEKEY_URLREDIRECTIONS) as Dictionary<string, URLRedirection>;
                if (_urlRedirections == null)
                {
                    string dependancyStr = null;

                    _urlRedirections = new Dictionary<string, URLRedirection>();

                    //check global list...
                    AgilityContentServer.AgilityUrlRedirectionList lst = BaseCache.GetUrlRedirections(AgilityContext.WebsiteName);
                    if (lst != null && lst.Redirections.Length > 0)
                    {


                        dependancyStr = BaseCache.GetCacheKey(lst);
                        //use the global list...
                        foreach (AgilityContentServer.AgilityUrlRedirection redir in lst.Redirections)
                        {

                            string origUrl = redir.OriginUrl;
                            if (origUrl.StartsWith("/")) origUrl = string.Format("~{0}", origUrl);
                            origUrl = origUrl.TrimEnd('/');


                            URLRedirection uLookup;

                            URLRedirection u = new URLRedirection()
                            {
                                HTTPStatusCode = redir.HttpCode,
                                Content = redir.Content,
                                OriginalURL = origUrl,
                                RedirectURL = redir.DestinationUrl,
                                DestinationLanguageCode = redir.DestinationLanguageCode,
                                OriginLanguageCodes = redir.OriginLanguages,
                                UserAgents = redir.UserAgents
                            };

                            if (_urlRedirections.TryGetValue(origUrl.ToLowerInvariant(), out uLookup) && uLookup != null)
                            {
                                //if this redirection has already been defined, add it to the current one...
                                if (uLookup.OtherRedirections == null) uLookup.OtherRedirections = new List<URLRedirection>();
                                uLookup.OtherRedirections.Add(u);

                            }
                            else
                            {
                                //add the new redirection
                                _urlRedirections[origUrl.ToLowerInvariant()] = u;
                            }
                        }

                    }


                    //add to cache if live...
                    if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
                    {
                        CacheDependency dep = new CacheDependency(new string[0], new string[] { dependancyStr });
                        AgilityCache.Set(CACHEKEY_URLREDIRECTIONS, _urlRedirections, TimeSpan.FromDays(1), dep, AgilityContext.DefaultCachePriority);

                    }
                    else
                    {
                        //staging store for a limited time...
                        AgilityCache.Set(CACHEKEY_URLREDIRECTIONS, _urlRedirections, TimeSpan.FromMinutes(1), null, AgilityContext.DefaultCachePriority);
                    }
                }

                if (_urlRedirections == null) _urlRedirections = new Dictionary<string, URLRedirection>();

                return _urlRedirections;

            }
        }

        private static CacheItemPriority _defaultCachePriority = CacheItemPriority.Low;

        public static CacheItemPriority DefaultCachePriority
        {
            get
            {

                if (_defaultCachePriority == CacheItemPriority.Low)
                {

                    string priStr = Configuration.Current.Settings.DefaultCachePriority;
                    if (string.IsNullOrWhiteSpace(priStr))
                    {
                        _defaultCachePriority = CacheItemPriority.Normal;

                    }
                    else
                    {

                        switch (priStr.ToLower())
                        {
                            case "abovenormal":
                                _defaultCachePriority = CacheItemPriority.High;
                                break;
                            case "normal":
                                _defaultCachePriority = CacheItemPriority.Normal;
                                break;
                            case "high":
                                _defaultCachePriority = CacheItemPriority.High;
                                break;
                            default:
                                _defaultCachePriority = CacheItemPriority.NeverRemove;
                                break;
                        }
                    }
                }

                return _defaultCachePriority;


            }
        }

        public static Dictionary<string, URLRedirection> URLRedirections_WithQueryStrings
        {
            get
            {
                //get a list of the redirections with ?

                Dictionary<string, URLRedirection> redirectsWithQuerys = null;

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    redirectsWithQuerys = AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS] as Dictionary<string, URLRedirection>;
                }
                else
                {
                    redirectsWithQuerys = AgilityCache.Get(CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS) as Dictionary<string, URLRedirection>;
                }

                if (redirectsWithQuerys == null)
                {

                    redirectsWithQuerys =
                        (from u in URLRedirections
                         where u.Key.Contains('?')
                         select u.Value).ToDictionary(u => u.OriginalURL.ToLower());


                }

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS] = redirectsWithQuerys;
                }
                else
                {
                    CacheDependency dep = new CacheDependency(new string[0], new string[] { CACHEKEY_URLREDIRECTIONS });
                    AgilityCache.Remove(CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS);

                    AgilityCache.Set(CACHEKEY_URLREDIRECTIONS_WITHQUERYSTRINGS, redirectsWithQuerys, TimeSpan.FromDays(1), dep, AgilityContext.DefaultCachePriority);

                }

                return redirectsWithQuerys;
            }
        }

        public static Dictionary<string, URLRedirection> URLRedirections_WithoutQueryStrings
        {
            get
            {
                //get a list of the redirections with no ?

                Dictionary<string, URLRedirection> redirectsWithoutQuerys = null;

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    redirectsWithoutQuerys = AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WITHOUTQUERYSTRINGS] as Dictionary<string, URLRedirection>;
                }
                else
                {
                    redirectsWithoutQuerys = AgilityCache.Get(CACHEKEY_URLREDIRECTIONS_WITHOUTQUERYSTRINGS) as Dictionary<string, URLRedirection>;
                }

                if (redirectsWithoutQuerys == null)
                {

                    redirectsWithoutQuerys =
                        (from u in URLRedirections
                         where (!u.Key.Contains('?'))
                         select u.Value).ToDictionary(u => u.OriginalURL.ToLower());

                }

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WITHOUTQUERYSTRINGS] = redirectsWithoutQuerys;
                }
                else
                {
                    CacheDependency dep = new CacheDependency(new string[0], new string[] { CACHEKEY_URLREDIRECTIONS });
                    AgilityCache.Set(CACHEKEY_URLREDIRECTIONS_WITHOUTQUERYSTRINGS, redirectsWithoutQuerys, TimeSpan.FromDays(1), dep, AgilityContext.DefaultCachePriority);

                }

                return redirectsWithoutQuerys;
            }
        }


        public static Dictionary<string, URLRedirection> WildcardRedirections
        {
            get
            {
                //get a list of the redirections with *

                Dictionary<string, URLRedirection> wildcardRedirects = null;

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    wildcardRedirects = AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WILDCARD] as Dictionary<string, URLRedirection>;
                }
                else
                {
                    wildcardRedirects = AgilityCache.Get(CACHEKEY_URLREDIRECTIONS_WILDCARD) as Dictionary<string, URLRedirection>;
                }

                if (wildcardRedirects == null)
                {

                    wildcardRedirects = new Dictionary<string, URLRedirection>();

                    var redirs = URLRedirections;
                    if (redirs != null)
                    {

                        foreach (string key in redirs.Keys)
                        {
                            if (key.EndsWith("*"))
                            {
                                string keyNoStar = key.Substring(0, key.Length - 1);
                                wildcardRedirects.Add(keyNoStar, redirs[key]);
                            }
                        }
                    }
                }

                if (AgilityContext.CurrentMode == Mode.Staging)
                {
                    AgilityContext.HttpContext.Items[CACHEKEY_URLREDIRECTIONS_WILDCARD] = wildcardRedirects;
                }
                else
                {
                    CacheDependency dep = new CacheDependency(new string[0], new string[] { CACHEKEY_URLREDIRECTIONS });
                    AgilityCache.Set(CACHEKEY_URLREDIRECTIONS_WILDCARD, wildcardRedirects, TimeSpan.FromDays(1), dep, AgilityContext.DefaultCachePriority);
                }

                return wildcardRedirects;
            }
        }

        internal static bool IsResponseEnded
        {
            get
            {
                object o = AgilityContext.HttpContext.Items["Agility.Web.Context.IsResponseEnded"];
                if (o is bool) return (bool)o;
                return false;
            }
            set
            {
                HttpContext.Items["Agility.Web.Context.IsResponseEnded"] = value;

            }
        }


		public static List<string> OutputCacheDependencies
        {
            get
            {
                //TODO: make sure this is used for the Output Cache / Response Cache dependancy
                List<string> lst = HttpContext.Items["Agility.Web.Context.OutputCacheDependencies"] as List<string>;
                if (lst == null)
                {
                    lst = new List<string>();
                    HttpContext.Items["Agility.Web.Context.OutputCacheDependencies"] = lst;
                }
                return lst;
            }
        }

        /// <summary>
        /// Returns either the language specific twitter card or the global twitter card site value.
        /// </summary>
        public static string TwitterCardSite
        {
            get
            {
                string languageCode = LanguageCode;
                //TODO: enable twitter cards for multiple languages
                //if (Current.Settings.TwitterCardSites != null 
                //	&& Current.Settings.TwitterCardSites[languageCode] != null)
                //{
                //	return Current.Settings.TwitterCardSites[languageCode].TwitterCardSite;
                //}

                return Current.Settings.TwitterCardSite;

            }
        }


#if NET35

		private static object _throttleLock = new object();

		/// <summary>
		/// REQUEST THROTTLING
		/// </summary>
		private static Dictionary<string, int> _requestThrottler = new Dictionary<string, int>();
		public static Dictionary<string, int> RequestThrottler
		{
			get
			{
				return _requestThrottler;
			}
		}
		public static void SetRequestThrottler(string key, int value)
		{

			lock (_throttleLock)
			{
				if (_requestThrottler.ContainsKey(key))
				{
					int existValue = _requestThrottler[key] += 1;
					_requestThrottler[key] = existValue;
				}
				else
				{
					_requestThrottler[key] = value;
				}
			}

			
		}

#else
        /// <summary>
        /// REQUEST THROTTLING
        /// </summary>
        private static ConcurrentDictionary<string, int> _requestThrottler = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, int> RequestThrottler
        {
            get
            {
                return _requestThrottler;
            }
        }
        public static void SetRequestThrottler(string key, int value)
        {
            _requestThrottler.AddOrUpdate(key, value, (k, v) => v + 1);
        }

#endif
    }
}
