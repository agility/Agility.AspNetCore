using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Agility.Web.Configuration;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Agility.Web.Analytics
{
	public class AnalyticsContext
	{
		public static string Url
		{
			get
			{
				string s = Agility.Web.Configuration.Current.Settings.Analytics.Url;
				return s;
			}
		}

		public static string ScriptUrl
		{
			get
			{


				string s = AnalyticsContext.Url;
				if (!s.EndsWith("/")) return $"{s}/scripts/agility-track.min.js";

				return $"{s}scripts/agility-track.min.js";
			}
		}

		public static HtmlString InitScript
		{
			get
			{
				string s = $"Agility.Tracking.Init({{ url: \"{Url}\", websiteName: \"{AgilityContext.WebsiteName}\", authKey: \"{Hash()}\" }});";
				return new HtmlString(s);				
			}			
		}

		public static HtmlString PageViewScript
		{
			get
			{
				if (AgilityContext.Page != null)
				{
					int pageID = AgilityContext.Page.ID;
					string languageCode = AgilityContext.LanguageCode;
					string contentIDStr = string.Join<int>(",", AgilityContext.LoadedContentItemIDs);
					string experimentKeyStr = string.Join<string>("','", AgilityContext.ExperimentKeys);


					string s = $"Agility.Tracking.TrackPageView({{ pageID: {pageID}, languageCode: \"{languageCode}\", contentIDs: [{contentIDStr}], experiments:['{experimentKeyStr}'] }});";
					return new HtmlString(s);
				}

				return HtmlString.Empty;
			}
		}


		public static string Hash()
		{
			string websiteName = AgilityContext.WebsiteName;
			string securityKey = Agility.Web.Configuration.Current.Settings.SecurityKey;

			byte[] key = Encoding.UTF8.GetBytes(securityKey);
			byte[] value = Encoding.UTF8.GetBytes(string.Format("{0}.{1}", securityKey, websiteName));
			var sha = new HMACSHA256(key);

			var hash = sha.ComputeHash(value);

			string hashStr = Base64Encode(hash);

			return hashStr;
		}

		private static string Base64Encode(byte[] input)
		{
			var output = Convert.ToBase64String(input);
			output = output.Split('=')[0]; // Remove any trailing '='s
			output = output.Replace('+', '-'); // 62nd char of encoding
			output = output.Replace('/', '_'); // 63rd char of encoding
			return output;
		}
	}
}
