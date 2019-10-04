using Agility.Web.AgilityContentServer;
using Agility.Web.Configuration;
using Agility.Web.Sync;
using Agility.Web.Tracing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;

namespace Agility.Web.Controllers
{
    [Authorize(Policy = "CorrectWebsite")]
    [Route("api/[controller]/[action]")]
    public class AgilityWebsiteController : Controller
    {
        [HttpPost]
        public void TriggerCacheSync([FromBody] AgilityPublishRequest publishRequest)
        {
            ValidateRequest(publishRequest.WebsiteName, publishRequest.SecurityKey);

            WebTrace.WriteVerboseLine(string.Format("Cache sync triggered: Domain:{0}, Website:{1}, Key:{2}", publishRequest.WebsiteDomain, publishRequest.WebsiteName, publishRequest.SecurityKey));

            SyncThread.QueueSyncThread(publishRequest, false);
        }

        [HttpGet]
        public Dictionary<string, string> CheckDomainStatus()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            #region agilityWebVersion
            //**** agilityWebVersion
            try
            {
                result["agilityWebVersion"] = this.GetType().Assembly.GetName().Version.ToString();
            }
            catch (Exception ex)
            {
                result["agilityWebVersion"] = "Error";
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            #region canWriteContentFiles

            //**** canWriteContentFiles
            try
            {
                if (!Directory.Exists(Current.Settings.ContentCacheFilePath)) Directory.CreateDirectory(Current.Settings.ContentCacheFilePath);
                string filepath = Path.Combine(Current.Settings.ContentCacheFilePath, "TestWebsiteConfiguration.tmp");
                System.IO.File.WriteAllText(filepath, DateTime.Now.ToString());

                result["canWriteContentFiles"] = true.ToString();

            }
            catch (Exception ex)
            {
                result["canWriteContentFiles"] = false.ToString();
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            #region canDeleteContentFiles

            //**** canDeleteContentFiles
            try
            {
                string filepath = Path.Combine(Current.Settings.ContentCacheFilePath, "TestWebsiteConfiguration.tmp");
                System.IO.File.WriteAllText(filepath, DateTime.Now.ToString());
                System.IO.File.Delete(filepath);

                result["canDeleteContentFiles"] = true.ToString();
            }
            catch (Exception ex)
            {
                result["canDeleteContentFiles"] = false.ToString();
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            #region dotNetVersion
            //**** dotNetVersion
            try
            {
                result["dotNetVersion"] = System.Environment.Version.ToString();
            }
            catch (Exception ex)
            {
                result["dotNetVersion"] = "Error";
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            //**** contentServerURL
            result["contentServerUrl"] = Current.Settings.ContentServerUrl;

            #region canContactContentServer
            //**** canContactContentServer
            try
            {
                AgilityWebsiteAuthorization auth = BaseCache.GetAgilityWebsiteAuthorization();
                try
                {
                    if (AgilityContext.Domain != null)
                    {
                        auth.WebsiteDomain = AgilityContext.Domain.DomainName;
                    }
                }
                catch { }

                using (AgilityContentServerClient client = BaseCache.GetAgilityServerClient())
                {
                    string url = client.TestConnectionAsync(auth).Result.TestConnectionResult;

                }

                result["canContactContentServer"] = true.ToString();
            }
            catch (Exception ex)
            {
                result["canContactContentServer"] = false.ToString();
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            //**** smtpServer
            result["smtpServer"] = Current.Settings.SmtpServer;

            #region canWriteToLog

            //**** canWriteToLog
            try
            {
                if (AgilityContext.ContentAccessor != null)
                {

                    result["canWriteToLog"] = true.ToString();
                }
                else
                {

                    string logFile = WebTrace.GetLogFilePath();
                    FileInfo fo = new FileInfo(logFile);
                    if (string.IsNullOrEmpty(logFile) || (fo.Exists && fo.IsReadOnly))
                    {
                        result["canWriteToLog"] = false.ToString();
                    }
                    else
                    {

                        if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
                            if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                            {
                                throw new ApplicationException("The log directory does not exist.");
                            }
                        }
                        string filepath = Path.Combine(Path.GetDirectoryName(logFile), "TestWebsiteConfiguration.tmp");
                        System.IO.File.WriteAllText(filepath, DateTime.Now.ToString());
                        result["canWriteToLog"] = true.ToString();
                    }

                }


            }
            catch (Exception ex)
            {
                result["canWriteToLog"] = false.ToString();
                Agility.Web.Tracing.WebTrace.WriteWarningLine(ex.ToString());
            }
            #endregion

            //**** logFilePath
            if (AgilityContext.ContentAccessor != null)
            {
                result["logFilePath"] = "Blob Storage";
            }
            else
            {
                result["logFilePath"] = Current.Settings.Trace.LogFilePath;
            }

            //**** traceLevel
            result["traceLevel"] = string.Format("{0}", Current.Settings.Trace.TraceLevel);

            //**** siteUsername
            result["siteUsername"] = string.Format("{0}", Environment.UserName); // System.Threading.Thread..CurrentPrincipal.Identity.Name);

            //**** isDevelopmentMode
            result["isDevelopmentMode"] = string.Format("{0}", Current.Settings.DevelopmentMode);

            //**** osVersion
            result["osVersion"] = string.Format("{0}", Environment.OSVersion);

            return result;
        }

        /// <summary>
        /// Gets a list of at most 30 of the recent log files for this site.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<string> GetRecentLogFiles()
        {
            var websiteName = Request.Headers["WebsiteName"].ToString();
            var securityKey = Request.Headers["SecurityKey"].ToString();

            ValidateRequest(websiteName, securityKey);


            if (AgilityContext.ContentAccessor != null)
            {
                return AgilityContext.ContentAccessor.GetRecentLogFiles();
            }

            string logFile = Current.Settings.Trace.LogFilePath;


            if (!string.IsNullOrEmpty(logFile))
            {
                //map the log file to the current machine
                if (!Path.IsPathRooted(logFile))
                {
                    //WE MUST HAVE A ROOTED PATH FOR THIS
                    throw new FileNotFoundException("Log file path needs to be rooted.");
                }
            }

            List<string> retFiles = new List<string>(30);

            if (Directory.Exists(Path.GetDirectoryName(logFile)))
            {
                string[] files = Directory.GetFiles(Path.GetDirectoryName(logFile), Path.GetFileNameWithoutExtension(logFile) + "*");

                int length = files.Length;
                if (length > 30) length = 30;

                int count = 0;
                for (int i = files.Length - 1; i >= 0 && count < length; i--)
                {
                    retFiles.Add(Path.GetFileName(files[i]));
                    count++;
                }
            }


            return retFiles;
        }

        /// <summary>
        /// Gets the contents of a log file given the filename.
        /// </summary>
        /// <param name="websiteName"></param>
        /// <param name="securityKey"></param>
        /// <param name="logFileName"></param>
        /// <returns></returns>
        [HttpGet]
        public string GetLogFileContents(string logFileName)
        {
            var websiteName = Request.Headers["WebsiteName"].ToString();
            var securityKey = Request.Headers["SecurityKey"].ToString();

            ValidateRequest(websiteName, securityKey);

            string logFileText = null;
            if (AgilityContext.ContentAccessor != null)
            {
                logFileText = AgilityContext.ContentAccessor.GetLogFileContents(logFileName);
            }
            else
            {

                string logFile = Current.Settings.Trace.LogFilePath;

                if (!string.IsNullOrEmpty(logFile))
                {
                    //map the log file to the current machine
                    if (!Path.IsPathRooted(logFile))
                    {
                        throw new FileNotFoundException("Log file path needs to be rooted.");
                    }
                }

                logFile = Path.GetDirectoryName(logFile) + Path.DirectorySeparatorChar + logFileName;
                if (!System.IO.File.Exists(logFile))
                {
                    throw new ApplicationException("The log file '" + logFileName + "' does not exist.");
                }

                FileStream fs = null;
                StreamReader sr = null;
                try
                {
                    fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    sr = new StreamReader(fs);
                    logFileText = sr.ReadToEnd();

                }
                catch
                {
                    throw;
                }
                finally
                {
                    if (sr != null) sr.Close();
                    if (fs != null) fs.Close();
                }
            }

            //trim the file to 768kb..
            int mb = 1024 * 768;
            if (logFileText.Length > mb)
            {
                logFileText = "**** Showing last 768kb of file ****\r\n" + logFileText.Substring(logFileText.Length - mb, mb);
            }

            return logFileText;
        }

        /// <summary>
        /// Clears all the cache files on the server (Live and Staging)
        /// </summary>
        /// <param name="websiteName"></param>
        /// <param name="securityKey"></param>
        [HttpPost]
        public void ClearAllCache([FromBody] AgilityPublishRequest publishRequest)
        {
            WebTrace.WriteInfoLine("Triggering Cache Clear: " + publishRequest.WebsiteName + " - " + publishRequest.SecurityKey);

            ValidateRequest(publishRequest.WebsiteName, publishRequest.SecurityKey);

            //trigger a sync that will sync ALL items
            Sync.SyncThread.QueueSyncThread(publishRequest, true);
        }

        private bool ValidateRequest(string websiteName, string securityKey)
        {
            if (string.IsNullOrEmpty(websiteName) || string.IsNullOrEmpty(securityKey))
            {
                throw new ArgumentException("A website name and security key must be provided.");
            }

            if (Current.Settings == null)
            {
                throw new Exception("Invalid Agility Website Configuration");
            }

            //check that the website name is configured for use in this site...
            if (Current.Settings.WebsiteName != websiteName)
            {
                throw new ArgumentException("The website name provided is not valid, or the website configuration does not have the websiteName present.", "websiteName");
            }

            if (Current.Settings.SecurityKey != securityKey)
            {
                throw new ArgumentException("The security key provided is not valid, or the website configuration does not have the securityKey present.", "securityKey");
            }

            return true;
        }
    }
}
