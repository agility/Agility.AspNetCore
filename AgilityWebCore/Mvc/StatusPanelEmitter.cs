using System.Linq;
using Agility.Web.Configuration;
using Agility.Web.Objects;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http.Extensions;
using System.Globalization;
using Agility.Web.Tracing;

namespace Agility.Web.Mvc
{
	internal class StatusPanelEmitter
	{
		static string cssURL = "https://media.agilitycms.com/preview-bar/2018-11/agility-preview-bar.min.css";

		internal static string GetStatusPanelCssOnly()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat(@"
<link href='//fonts.googleapis.com/css?family=Open+Sans:400,600' rel='stylesheet' type='text/css'>
<link type='text/css' rel='stylesheet' href='{0}'/>
", cssURL);

			return sb.ToString();
		}

		internal static string GetStatusPanelCss()
		{
			return GetStatusPanelCssOnly();
		}

		internal static string GetStatusPanelScriptNoJQuery()
		{

			return GetStatusPanelScript();
		}

		internal static string GetStatusPanelScript()
		{

			//Add the Style Sheets for the StatusBar and Edit In Place:
			StringBuilder sb = new StringBuilder();
			//HACK

			bool isPublished = false;
			bool containsUnpublishedModules = false;

			string pageTemplatePath = string.Empty;
			int pageTemplateID = -1;
			int pageID = -1;

			if (AgilityContext.Page != null)
			{
				AgilityPage page = AgilityContext.Page;

				pageID = page.ID;
				if (page.IsPublished)
				{
					isPublished = true;
				}

				if (!string.IsNullOrEmpty(page.TemplatePath) && page.TemplateID > 0)
				{

					pageTemplatePath = page.TemplatePath;
					if (pageTemplatePath.StartsWith("~/"))
					{
						pageTemplatePath = pageTemplatePath.Substring(1);

						string appPath = "/"; 
						if (appPath != "/")
						{
							pageTemplatePath = string.Format("{0}{1}{2}", appPath, "/TemplatePreview", pageTemplatePath);
						}
						else
						{
							pageTemplatePath = string.Format("{0}{1}", "/TemplatePreview", pageTemplatePath);
						}
					}

					pageTemplateID = page.TemplateID;
				}



				bool switchMode = false;
				if (AgilityContext.CurrentMode == Agility.Web.Enum.Mode.Live)
				{
					//if the site is in live mode, switch to staging to check if any modules are required publishing.
					switchMode = true;
					AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Staging;
				}

				//check if there are any modules that have not yet been publish
				foreach (ContentSection sect in AgilityContext.Page.ContentSections)
				{
					if (sect.ModuleID > 0)
					{

						AgilityContentServer.AgilityModule module = BaseCache.GetModule(sect.ModuleID, AgilityContext.WebsiteName);
						if (module != null && !module.IsPublished && module.IsPublishedSpecified)
						{
							containsUnpublishedModules = true;
						}
					}
				}

				if (switchMode)
				{
					AgilityContext.CurrentMode = Agility.Web.Enum.Mode.Live;
				}
			}

			//generate the preview key
			string securityKey = Current.Settings.SecurityKey;
			byte[] data = UnicodeEncoding.Unicode.GetBytes(string.Format("{0}_{1}_Preview", -1, securityKey));
			SHA512 shaM = new SHA512Managed();
			byte[] result = shaM.ComputeHash(data);


			string previewKey = Convert.ToBase64String(result);
			string appendQuery = string.Format("agilitypreviewkey={0}&agilityts={1}&lang={2}",
										HttpUtility.UrlEncode(previewKey),
										DateTime.Now.ToString("yyyyMMddhhmmss"),
										HttpUtility.UrlEncode(AgilityContext.LanguageCode));

			string pageUrl = AgilityContext.UrlForPreviewBar;
			if (string.IsNullOrEmpty(pageUrl))
			{
				pageUrl = AgilityContext.HttpContext.Request.GetEncodedUrl();
				
			}


			string innerUrl = Agility.Web.Util.Url.ModifyQueryString(
								HttpUtility.UrlPathEncode(pageUrl),
								appendQuery,
								"ispreview");

			string subject = string.Format("Agility {0} Preview", AgilityContext.WebsiteName);
			string body = string.Format("Click the link below to preview the {0} site:\n{1}\n____________________\nSent from Agility\nhttp://www.agilitycms.com",
				AgilityContext.WebsiteName,
				innerUrl);

			string previewURL = string.Format("mailto:?subject={0}&body={1}",
				HttpUtility.UrlEncode(subject).Replace("+", "%20"),
				HttpUtility.UrlEncode(body).Replace("+", "%20"));

			//channel listing
			string[] channels = (from c in BaseCache.GetDigitalChannels(AgilityContext.WebsiteName).Channels
								 select string.Format("{{Name:\"{0}\",ID:'{1}'}}", c.DisplayName.Replace("\"", "\\\""), c.ReferenceName)).ToArray();

			string uniqueID = Guid.NewGuid().ToString();

			string previewDateStr = AgilityContext.PreviewDateTime.ToString("yyyy-M-d h:mm tt", CultureInfo.InvariantCulture);
			if (AgilityContext.PreviewDateTime == DateTime.MinValue) previewDateStr = string.Empty;

			//output the script for the onload			
			sb.Append("<script type='text/javascript'>");

			//output the context object
			sb.AppendFormat(@"var agilityContextObj = {{
					currentMode:""{0}"", 
					isPreview:{1}, 
					isTemplatePreview:{2}, 
					languageCode:""{3}"", 
					websiteName:""{4}"", 
					isDevelopmentMode:{5}, 
					controlUniqueID:""{6}"", 
					previewDateTime:""{7}"",
					isPublished:{8}, 
					containsUnpublishedModules:{9}, 
					pageTemplatePath:""{10}"", 
					pageTemplateID:{11}, 
					previewURL:""{12}"",
					cookieDomain:""{13}"",
					pageID:""{14}"",
					errorLink:{15},
					channel:'{16}',
					channels:[{17}]
				}}; ",
				new object[] {
					AgilityContext.CurrentMode, //0
					AgilityContext.IsPreview.ToString().ToLowerInvariant(), //1
					AgilityContext.IsTemplatePreview.ToString().ToLowerInvariant(),	//2
					AgilityContext.LanguageCode, //3
					AgilityContext.WebsiteName.Replace("\"", "\\\"").Replace(" ", ""), //4
					Current.Settings.DevelopmentMode.ToString().ToLowerInvariant(), //5
					uniqueID, //6
					previewDateStr, //7
					isPublished.ToString().ToLowerInvariant(), //8
					containsUnpublishedModules.ToString().ToLowerInvariant(), //9
					pageTemplatePath.Replace("\"", "\\\""), //10
					pageTemplateID, //11
					previewURL.Replace("\"", "\\\""), //12
					Current.Settings.CookieDomain, //13
					pageID,
					Current.Settings.DevelopmentMode && WebTrace.HasErrorOccurred ? string.Format("'{0}?enc={1}'", Agility.Web.HttpModules.AgilityHttpModule.ECMS_ERRORS_KEY, HttpUtility.UrlEncode(WebTrace.GetEncryptionQueryStringForLogFile(DateTime.Now))) : "null",
					AgilityContext.CurrentChannel.ReferenceName,
					string.Join(",", channels)

				 });
			sb.Append(Environment.NewLine);
			sb.Append("var agilityLanguages = [");

			foreach (Language lang in AgilityContext.Domain.Languages)
			{
				sb.AppendFormat("['{0}', '{1}'],", lang.LanguageName, lang.LanguageCode);
			}
			sb = sb.Remove(sb.Length - 1, 1);
			sb.Append("];");
			sb.Append(Environment.NewLine);
			sb.Append("</script>");

			sb.Append("<script type='text/javascript' src='https://media.agilitycms.com/preview-bar/2018-11/agility-preview-bar.es5.min.js'></script>");


			return sb.ToString();

		}


	}
}
