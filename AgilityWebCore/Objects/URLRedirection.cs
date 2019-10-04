using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agility.Web
{
	public class URLRedirection
	{



		/// <summary>
		/// The URL that will be redirected from (app relative)
		/// </summary>
		public string OriginalURL { get; set; }

		/// <summary>
		/// The URL  (relative or absolute) that will be redirected to.
		/// </summary>
		public string RedirectURL { get; set; }

		/// <summary>
		/// The HTTP Status Code that will be used for the redirect (301 oe 302)
		/// </summary>
		public int HTTPStatusCode { get; set; }

		/// <summary>
		/// The Content (if any) that should be sent to the client.
		/// </summary>
		public string Content { get; set; }


		public string[] OriginLanguageCodes { get; set; }

		public string DestinationLanguageCode { get; set; }

		public string[] UserAgents { get; set; }

		/// <summary>
		/// List of Redirections with the same origin url but different user agents or origin langs.
		/// </summary>
		public List<URLRedirection> OtherRedirections { get; set; }



		internal bool MatchUserAgentAndLanguage(StringBuilder sbTraceMessage, string languageCode)
		{
			//test the user agents
			string[] userAgentTests = UserAgents;
			if (userAgentTests != null)
			{
				string userAgent = AgilityContext.HttpContext.Request.Headers["User-Agent"];
                if (string.IsNullOrEmpty(userAgent))
                {
                    sbTraceMessage.Append("\r\nNo redirection - user agent not provided.");
                } 
                else
                {
                    string userAgentTest = userAgentTests.FirstOrDefault(ua => userAgent.IndexOf(ua, StringComparison.CurrentCultureIgnoreCase) != -1);
                    if (string.IsNullOrEmpty(userAgentTest))
                    {
                        //no redirection necessary - user agent doesn't match, keep looking
                        sbTraceMessage.AppendFormat("\r\nNo redirection - user agent {0} not found in {1}.", userAgent, string.Join(",", userAgentTests));
                        Agility.Web.Tracing.WebTrace.WriteVerboseLine(sbTraceMessage.ToString());
                        return false;
                    }
                    
                    sbTraceMessage.AppendFormat("\r\nUser agent {0} found in {1}.", userAgentTest, userAgent);
                }
			}

			//test languages
			string[] languageTests = OriginLanguageCodes;
			if (languageTests != null)
			{
				string currentLanguageCode = languageCode;
				string languageTest = languageTests.FirstOrDefault(l => string.Equals(l, currentLanguageCode, StringComparison.CurrentCultureIgnoreCase));

				if (string.IsNullOrEmpty(languageTest))
				{
					//no redirection necessary - user agent doesn't match
					sbTraceMessage.AppendFormat("\r\nNo redirection - language {0} not found in {1}.", currentLanguageCode, string.Join(",", languageTests));
					Agility.Web.Tracing.WebTrace.WriteVerboseLine(sbTraceMessage.ToString());
					return false;
				}
				else
				{
					sbTraceMessage.AppendFormat("\r\nLanguage {0} found in {1}.", currentLanguageCode, languageTests);
				}
			}

			return true;
		}
	}
}
