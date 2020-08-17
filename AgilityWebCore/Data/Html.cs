using Agility.Web.Configuration;
using Agility.Web.Providers;
using System.Collections.Generic;
using System.Data;

namespace Agility.Web
{
    public class Html
    {
        public static string AgilityCSS(params string[] referenceNames)
        {
            string url = AgilityFile(FileType.CSS, referenceNames);

            return string.Format("<link rel=\"stylesheet\" href=\"{0}\">", url);

        }

        public static string AgilityJavascript(params string[] referenceNames)
        {
            string url = AgilityFile(FileType.Javascript, referenceNames);
            return string.Format("<script type=\"text/javascript\" src=\"{0}\"></script>", url);
        }

        public static string AgilityTemplatePath(string referenceName)
        {
            string contentReferenceName = AgilityDynamicCodeFile.REFNAME_AgilityGlobalCodeTemplates;

            int versionID = 0;
            if (Current.Settings.DevelopmentMode || AgilityContext.CurrentMode == Enum.Mode.Staging)
            {
                //path is like this: DynamicAgilityCode/[ContentReferenceName]/[ItemReferenceName].ext
                string tempPath = string.Format("~/Views/DynamicAgilityCode/{0}/{1}.cshtml", contentReferenceName, referenceName);
                DataRow row = AgilityDynamicCodeFile.GetCodeItem(tempPath);

				if (!int.TryParse($"{row["VersionID"]}", out versionID)) versionID = -1;
				
            }

            return string.Format("~/Views/{0}/DynamicAgilityCode/{1}/{2}.cshtml", versionID, contentReferenceName, referenceName);

        }


        private static string AgilityFile(FileType FileType, params string[] referenceNames)
        {
            string contentReferenceName = null;
            string ext = null;
            string prefix = null;

            switch (FileType)
            {
                case Web.FileType.CSS:
                    contentReferenceName = AgilityDynamicCodeFile.REFNAME_AgilityCSSFiles;
                    ext = ".css";
                    prefix = "Content/css/";
                    break;
                case Web.FileType.Javascript:
                    contentReferenceName = AgilityDynamicCodeFile.REFNAME_AgilityJavascriptFiles;
                    ext = ".js";
                    prefix = "scripts/";
                    break;


            }

            if (referenceNames == null || referenceNames.Length == 0) return string.Empty;
            if (string.IsNullOrEmpty(contentReferenceName)) return string.Empty;

            Agility.Web.AgilityContentServer.AgilityContent content = BaseCache.GetContent(contentReferenceName, AgilityDynamicCodeFile.LANGUAGECODE_CODE, AgilityContext.WebsiteName);
            if (content == null) return string.Empty;

            DataTable dt = content.DataSet.Tables["ContentItems"];
            if (dt == null) return string.Empty;

            bool minify = true;
            List<string> versionIDs = new List<string>();
            foreach (string refName in referenceNames)
            {
                DataRow[] rows = dt.Select(string.Format("ReferenceName = '{0}'", refName.Replace("'", "''")));
                if (rows.Length > 0)
                {
                    if (string.Format("{0}", rows[0]["Minify"]).ToLower() != "true")
                    {
                        minify = false;
                    }
                    string idStr = string.Format("{0}", rows[0]["VersionID"]);
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        versionIDs.Add(idStr);
                    }
                }
            }

            if (versionIDs.Count == 0) return string.Empty;

            var config = BaseCache.GetDomainConfiguration(AgilityContext.WebsiteName);
            string baseDomain = config.XAgilityCDNBaseUrl;
            if (baseDomain.EndsWith("/"))
            {
                baseDomain = baseDomain.Substring(baseDomain.Length - 1);
            }

            //handle ssl...
            if (AgilityContext.HttpContext.Request.IsHttps)
            {
                //replace cdn.agiltiycms.com...
                baseDomain = baseDomain.Replace("http://cdn.agilitycms.com", "https://az184419.vo.msecnd.net");
                baseDomain = baseDomain.Replace("http://cdndev.agilitycms.com", "https://az99666.vo.msecnd.net");
            }

            string websiteNameStripped = baseDomain.Substring(baseDomain.LastIndexOf("/") + 1);
            baseDomain = baseDomain.Substring(0, baseDomain.LastIndexOf("/"));

            string url = string.Format("{0}/code/{1}/{2}/{3}{4}{5}",
                baseDomain,
                websiteNameStripped,
                contentReferenceName.ToLowerInvariant(),
                string.Join("-", versionIDs.ToArray()),
                minify ? ".min" : string.Empty,
                ext);

            return url;

        }

    }
}
