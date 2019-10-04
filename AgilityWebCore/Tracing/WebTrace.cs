using Agility.Web.Configuration;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Agility.Web.Tracing
{

    public delegate string TraceHook(Exception ex);


    /// <summary>
    /// This class allows static tracing into the website's log file based on the setting in the Agility.Web/tracing section of the web.config.
    /// </summary>
    public abstract class WebTrace
    {

        public static event TraceHook ExceptionTrace;

        internal static bool HasErrorOccurred
        {
            get
            {
                if (AgilityContext.HttpContext != null)
                {
                    object o = AgilityContext.HttpContext.Items["Agility.Web.Tracing.WebTrace.HasErrorOccurred"];
                    if (o is bool) return (bool)o;
                }
                return false;

            }
            set
            {
                if (AgilityContext.HttpContext != null)
                {
                    AgilityContext.HttpContext.Items["Agility.Web.Tracing.WebTrace.HasErrorOccurred"] = value;
                }
            }
        }

        internal const string LOGFILE_CODE = "834ACBB4-7243-4DFE-9EE3-A9E94B9CBDDE";

        internal static string GetEncryptionQueryStringForLogFile(DateTime logDate)
        {
            byte[] data = UnicodeEncoding.Unicode.GetBytes(string.Format("{0:yyyy-MM-dd}_{1}", logDate, WebTrace.LOGFILE_CODE));
            SHA512 shaM = new SHA512Managed();
            byte[] result = shaM.ComputeHash(data);
            string compEnc = Convert.ToBase64String(result);
            return compEnc;
        }


        /// <summary>
        /// Writes out a line with the given message if the trace level is set to Verbose.
        /// </summary>
        /// <param name="message"></param>
        public static void WriteVerboseLine(string message)
        {
            if (Current.Settings != null && Current.Settings.Trace.TraceLevel >= TraceLevel.Verbose)
            {
                WriteLine(message, TraceLevel.Verbose);
            }
        }

        /// <summary>
        /// Writes out a line with the given message if the trace level is set to Verbose, Info, or Warning.
        /// </summary>
        /// <param name="message"></param>
        public static void WriteWarningLine(string message)
        {
            if (Current.Settings != null && Current.Settings.Trace.TraceLevel >= TraceLevel.Warning)
            {
                WriteLine(message, TraceLevel.Warning);
            }
        }

        /// <summary>
        /// Writes out a line with the given message if the trace level is set to Info or Verbose.
        /// </summary>
        /// <param name="message"></param>
        public static void WriteInfoLine(string message)
        {
            if (Current.Settings != null && Current.Settings.Trace.TraceLevel >= TraceLevel.Info)
            {
                WriteLine(message, TraceLevel.Info);
            }
        }

        /// <summary>
        /// Writes out a line with the given message if the trace level is set to Error, Warning, Info, or Verbose.
        /// </summary>
        /// <param name="message"></param>
        public static void WriteErrorLine(string message)
        {

            if (Current.Settings != null && Current.Settings.Trace.TraceLevel >= TraceLevel.Error)
            {
                WriteLine(message, TraceLevel.Error);
            }
        }

        /// <summary>
        /// Writes out a line with the given message if the trace level is set to Error, Warning, Info, or Verbose.  Also, if the emailErrors flag is true in the web.config, an email with this message will be sent to the emailErrorsTo address.
        /// </summary>
        /// <param name="ex"></param>
        public static void WriteException(Exception ex)
        {
            WriteException(ex, string.Empty);
        }
        /// <summary>
        /// This writes out the exception as an error status if the Agility.Config trace switch is set to output error messages.
        /// </summary>
        /// <param name="appendMessage"></param>
        /// <param name="ex"></param>
        public static void WriteException(Exception ex, string appendMessage)
        {

            HasErrorOccurred = true;

            bool skipEmail = false;

            TraceLevel traceLevel = TraceLevel.Error;

            try
            {

                //email the error
                if (AgilityContext.HttpContext != null
                    && Current.Settings != null
                    && Current.Settings.Trace != null
                    //implement trace types 
                    //&& Current.Settings.Trace.ErrorTraceTypes != null
                    )
                {
                    //ErrorTraceElement elem = Current.Settings.Trace.ErrorTraceTypes.FindMatchForException(ex,  AgilityContext.HttpContext.Request);
                    //if (elem != null)
                    //{
                    //	skipEmail = true;
                    //	traceLevel = elem.TraceLevel;
                    //}
                }


                if (!skipEmail)
                {
                    //CHANGE: 2011-01 - we no longer send emails on demand - there is another thread for that.
                    //SendErrorMessage(ex, appendMessage);
                }
            }
            catch (Exception ex2)
            {
                WriteErrorLine(ex2.ToString());
            }

            //write the error to the log
            StringBuilder sb = new StringBuilder();

            if (AgilityContext.HttpContext != null)
            {
                try
                {
                    sb.Append("Request Details:");

                    sb.Append(Environment.NewLine);
                    sb.Append("URL: ").Append(AgilityContext.HttpContext.Request.GetDisplayUrl());


                    if (!string.IsNullOrWhiteSpace(AgilityContext.HttpContext.Request.Headers["Referrer"]))
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append("Referrer: ").Append(AgilityContext.HttpContext.Request.Headers["Referer"]);
                    }


                    sb.Append(Environment.NewLine);
                    sb.Append("User Agent: ").Append(AgilityContext.HttpContext.Request.Headers["User-Agent"]);

                    sb.Append(Environment.NewLine);
                    sb.Append("Host Address: ").Append(AgilityContext.HttpContext.Connection.RemoteIpAddress);


                    //login name
                    if (AgilityContext.HttpContext.User != null && AgilityContext.HttpContext.User.Identity != null)
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append("Username: ");
                        sb.Append(AgilityContext.HttpContext.User.Identity.Name);
                    }
                }
                catch { }
            }

            if (System.Threading.Thread.CurrentPrincipal != null
                && System.Threading.Thread.CurrentPrincipal.Identity != null
                && !string.IsNullOrEmpty(System.Threading.Thread.CurrentPrincipal.Identity.Name))
            {
                sb.Append(Environment.NewLine);
                sb.Append("Identity: ").Append(System.Threading.Thread.CurrentPrincipal.Identity.Name);

            }

            if (ex.InnerException is SqlException)
            {
                sb.Append(Environment.NewLine);
                sb.Append("SQL Details:").Append(Environment.NewLine);
                SqlException sqlEx = (SqlException)ex.InnerException;

                foreach (SqlError error in sqlEx.Errors)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(" - ").Append(error.Message).Append(" - in Proc: ").Append(error.Procedure).Append(" line ").Append(error.LineNumber);

                }

            }
            else if (ex is SqlException)
            {
                sb.Append(Environment.NewLine);
                sb.Append("SQL Details:").Append(Environment.NewLine);
                SqlException sqlEx = (SqlException)ex;
                foreach (SqlError error in sqlEx.Errors)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(" - ").Append(error.Message).Append(" - in Proc: ").Append(error.Procedure).Append(" line ").Append(error.LineNumber);

                }
            }
            else
            {
                sb.Append(Environment.NewLine);
                sb.Append(ex.ToString());

            }
            if (!string.IsNullOrEmpty(appendMessage))
            {
                sb.Append(Environment.NewLine);
                sb.Append("Additional Message: ").Append(appendMessage);

            }

            if (ExceptionTrace != null)
            {
                string hookData = ExceptionTrace(ex);
                sb.Append(Environment.NewLine);
                sb.Append(hookData);
            }


            string message = sb.ToString();

            //actually write the message to the log base on the tracelevel defined for this message.
            switch (traceLevel)
            {
                case TraceLevel.Error:
                    WriteErrorLine(message);
                    break;
                case TraceLevel.Warning:
                    WriteWarningLine(message);
                    break;
                case TraceLevel.Info:
                    WriteInfoLine(message);
                    break;
                case TraceLevel.Verbose:
                    WriteVerboseLine(message);
                    break;

            }
        }

        private static object _lockObj = new object();

        private static void WriteLine(string message, TraceLevel level)
        {

            //build the output
            StringBuilder output = new StringBuilder();
            output.AppendFormat("*** {0} *** {1} *** {2:yyyy-MM-dd HH:mm:ss}",
                level.ToString().PadRight(8, ' '),
                System.Environment.MachineName,
                DateTime.Now);
            output.Append(Environment.NewLine);
            output.Append(message);
            output.Append(Environment.NewLine);
            output.Append(Environment.NewLine);


            OutputTraceManual(output.ToString());

        }


        private static void OutputTraceManual(string message)
        {

            try
            {

                if (AgilityContext.ContentAccessor != null)
                {
                    AgilityContext.ContentAccessor.OutputTrace(message);
                }

                //check the listener collection and ensure that the trace exists 

                string logFile = GetLogFilePath();

                if (!string.IsNullOrEmpty(logFile))
                {

                    FileInfo fo = new FileInfo(logFile);
                    if (fo.Exists && fo.IsReadOnly)
                    {
                        return;
                    }

                    lock (_lockObj)
                    {
                        string folder = Path.GetDirectoryName(logFile);
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }

                        //check the the current identity is the same is the impersonated one...
                        //if (SyncThread.ApplicationIdentity != null && SyncThread.ApplicationIdentity.Name != System.Security.Principal.WindowsIdentity.GetCurrent().Name)
                        //{

                        //	//check sync thread impersonation....
                        //	//WindowsImpersonationContext wi = SyncThread.ApplicationIdentity.Impersonate();
                        //}

                        File.AppendAllText(logFile, message);
                    }

                }
            }
            catch { }


        }


        internal static string GetLogFilePath()
        {
            return GetLogFilePath(DateTime.Now);
        }




        internal static string GetLogFilePath(DateTime logFileDate)
        {
            string logFile = Current.Settings.Trace.LogFilePath;

            //only if we have a file to write to...
            if (!string.IsNullOrEmpty(logFile))
            {

                //map the log file to the current maching
                if (!Path.IsPathRooted(logFile))
                {
                    //WE MUST HAVE A ROOTED PATH FOR THIS
                    return null;

                }


                //append date information to the log file
                if (Path.HasExtension(logFile))
                {
                    logFile = Path.GetDirectoryName(logFile) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(logFile);
                }

                logFile = string.Format("{0}{1:_yyyy_MM_dd}.log", logFile, logFileDate);
            }

            return logFile;
        }




        /// <summary>
        /// Sends a summary of all errors that have been logged since the last summary was sent out.
        /// </summary>
        internal static void SendErrorSummary()
        {
            //only send summary if "Send Error" is enabled
            if (!Configuration.Current.Settings.Trace.EmailErrors) return;

            Stream logStream = null;
            StringBuilder sb = new StringBuilder();
            int errorCount = 0;
            try
            {



                string separatorMarkerPrefix = "*** Summary Sent";
                string errorMarkerPrefix = "*** Error    ***";


                if (AgilityContext.ContentAccessor != null)
                {
                    string logContents = AgilityContext.ContentAccessor.GetLogFileContents(null);
                    logStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(logContents));
                }
                else
                {

                    string logFile = GetLogFilePath();

                    if (string.IsNullOrEmpty(logFile)) return;


                    FileInfo fo = new FileInfo(logFile);
                    if (!fo.Exists) return;
                    if (fo.IsReadOnly) return;

                    string folder = Path.GetDirectoryName(logFile);
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    //check the the current identity is the same is the impersonated one...
                    //if (SyncThread.ApplicationIdentity != null && SyncThread.ApplicationIdentity.Name != System.Security.Principal.WindowsIdentity.GetCurrent().Name)
                    //{
                    //	//check sync thread impersonation
                    //	//WindowsImpersonationContext wi = SyncThread.ApplicationIdentity.Impersonate();
                    //}

                    logStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                }


                lock (_lockObj)
                {


                    Encoding encoding = UTF8Encoding.UTF8;
                    int sizeOfChar = encoding.GetByteCount("\n");
                    byte[] buffer = encoding.GetBytes(Environment.NewLine);

                    //read backwards through the file line by line until we get to the beginning, or to the last "Send" marker.
                    using (logStream)
                    {
                        string tokenSeparator = Environment.NewLine;

                        Int64 endPosition = logStream.Length / sizeOfChar;
                        Int64 lastReadPosition = logStream.Length;
                        byte[] lineBuffer = null;
                        string line = null;

                        for (Int64 position = sizeOfChar; position < endPosition; position += sizeOfChar)
                        {

                            logStream.Seek(-position, SeekOrigin.End);
                            logStream.Read(buffer, 0, buffer.Length);

                            if (encoding.GetString(buffer) == tokenSeparator)
                            {

                                lineBuffer = new byte[lastReadPosition - logStream.Position];
                                lastReadPosition = logStream.Position;
                                logStream.Read(lineBuffer, 0, lineBuffer.Length);
                                line = encoding.GetString(lineBuffer);


                                if (line.IndexOf(separatorMarkerPrefix) != -1)
                                {
                                    //we found the last summary, we are done...
                                    break;
                                }

                                if (line.IndexOf(errorMarkerPrefix) != -1)
                                {
                                    //we found an error 
                                    errorCount++;
                                }

                                //regular line - insert it
                                sb.Insert(0, line);
                                line = string.Empty;

                                if (errorCount > 20)
                                {
                                    break;
                                }

                            }
                        }

                        //if we are at the beginning of the file...
                        if (lastReadPosition - logStream.Position > 0)
                        {
                            lineBuffer = new byte[lastReadPosition - logStream.Position];
                            logStream.Read(lineBuffer, 0, lineBuffer.Length);
                            line = encoding.GetString(lineBuffer);
                            sb.Insert(0, line);
                            if (line.IndexOf(errorMarkerPrefix) != -1)
                            {
                                //we found an error 
                                errorCount++;
                            }
                        }

                    }

                    logStream = null;

                    //append to the end of the file if we have to
                    if (sb.Length > 10)
                    {
                        string output = string.Format("{0} *** {1:yyyy-MM-dd HH:mm:ss}{2}{2}", separatorMarkerPrefix, DateTime.Now, Environment.NewLine);
                        if (AgilityContext.ContentAccessor == null)
                        {
                            string filepath = GetLogFilePath();
                            if (!string.IsNullOrEmpty(filepath))
                            {
                                File.AppendAllText(filepath, output);
                            }
                        }
                        else
                        {
                            AgilityContext.ContentAccessor.OutputTrace(output);
                        }
                    }
                } //end of the lock..



            }
            catch
            {
                if (logStream != null) logStream.Close();
            }

            if (errorCount > 0)
            {
                //send the error by email  


                string subject = string.Format("Application Error - {0} - Found {1} error(s)", Current.Settings.ApplicationName, errorCount);
                if (errorCount > 20)
                {
                    subject = string.Format("Application Error - {0} - Found more than 20 errors", Current.Settings.ApplicationName, errorCount);
                }

                if (subject.Length > 255) subject = subject.Substring(0, 255);

                string toDomain = string.Empty;
                if (HttpModules.AgilityHttpModule.Config != null)
                {
                    toDomain = HttpModules.AgilityHttpModule.Config.ErrorEmails;
                }

                string toWebConfig = Current.Settings.Trace.SendErrorsTo;
                string to = "";
                string allTo = toWebConfig;
                if (!string.IsNullOrEmpty(toDomain))
                {
                    allTo = string.Format("{0},{1}", toDomain, toWebConfig).ToLowerInvariant();
                }

                allTo = allTo.Replace("errors@edentity.ca", string.Empty).Replace("errors@agilitycms.com", string.Empty);
                string[] ary = allTo.Split(",;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                to = string.Join(",", ary);

                if (string.IsNullOrEmpty(to) || to == ",") to = "errors@agilitycms.com";

                Agility.Web.Tracing.WebTrace.WriteVerboseLine(string.Format("Sending error emails to: " + to));

                string from = Current.Settings.Trace.SendErrorsFrom;
                if (string.IsNullOrEmpty(from)) from = "Agility CMS <support@agilitycms.com>";

                //add a url to download the full log
                string logUrl = string.Format("{0}/{1}?enc={2}&date={3:yyyy-MM-dd}",
                    HttpModules.AgilityHttpModule.BaseURL,
                    HttpModules.AgilityHttpModule.ECMS_ERRORS_KEY,
                    HttpUtility.UrlEncode(GetEncryptionQueryStringForLogFile(DateTime.Now)),
                    DateTime.Now);
                sb.Insert(0, string.Format("Download log: {0}{1}{1}", logUrl, Environment.NewLine));

                //add summary info
                if (errorCount > 20)
                {
                    sb.Insert(0, string.Format("More than 20 error on {1} as of {2}.{3}{3}", errorCount, System.Environment.MachineName, DateTime.Now, Environment.NewLine));
                }
                else
                {
                    sb.Insert(0, string.Format("{0} error(s) on {1} as of {2}.{3}{3}", errorCount, System.Environment.MachineName, DateTime.Now, Environment.NewLine));
                }


                //send the message
                try
                {

                    if (AgilityContext.ContentAccessor != null)
                    {

                        //send the message using the Content Accessor...
                        AgilityContext.ContentAccessor.SendErrorEmailMessage(subject, sb.ToString());
                    }
                    else
                    {

                        //build the message
                        MailMessage msg = new MailMessage(from, to, subject, sb.ToString());
                        msg.IsBodyHtml = false;

                        //send the message using the config settings...
                        SmtpClient client;
                        if (string.IsNullOrEmpty(Current.Settings.SmtpServer))
                        {
                            client = new SmtpClient();
                        }
                        else
                        {
                            client = new SmtpClient(Current.Settings.SmtpServer);
                        }
                        client.Send(msg);
                    }
                }
                catch (Exception msgEx)
                {
                    WebTrace.WriteWarningLine(string.Format("Error Message Sent to {0} did not complete.", to) + "\r\n" + msgEx);
                }
            }
        }

    }


}
