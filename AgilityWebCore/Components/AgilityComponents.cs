using Agility.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace Agility.Web.Components
{
    /// <summary>
    /// Class for Agility Components.
    /// </summary>
    public static class AgilityComponents
    {
        /// <summary>
        /// Returns whether the components are using Inline Code or Local Files.
        /// </summary>
        public static bool UseInlineCode
        {
            get
            {
                return !Configuration.Current.Settings.DebugAgilityComponentFiles;
            }
        }

        private static string LocalTemplatesAndStaticFilePath = "~/Agility.Components.Files";
        private static string TemplatePrefixName = "agility";

        private static string PrefixReferenceName(string referenceName) {
            return string.Format("{0}-{1}", TemplatePrefixName, referenceName);
        }

        /// <summary>
        /// Returns the template path for the request Agility Component Parial View.
        /// </summary>
        public static string TemplatePath(string referenceName)
        {
            referenceName = PrefixReferenceName(referenceName);
            if (UseInlineCode)
            {
                try
                {
                    return Html.AgilityTemplatePath(referenceName.ToLower());
                }
                catch(Exception ex)
                {
                    throw new Exception(string.Format("Cannot find Inline Code template file'{0}'", referenceName.ToLower()), ex.InnerException);
                }
            }
            else
            {
                return string.Format("{0}/Views/{1}.cshtml", LocalTemplatesAndStaticFilePath, referenceName);
            }
        }

        /// <summary>
        /// Returns the CSS link element for the requested Agility Component CSS.
        /// </summary>
        public static string CSS(string referenceName)
        {
            referenceName = PrefixReferenceName(referenceName);
            if (UseInlineCode)
            {
                try
                {
                    return Html.AgilityCSS(referenceName.ToLower());
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Cannot find Inline Code css file '{0}{1}'", referenceName.ToLower()), ex.InnerException);
                }
            }
            else
            {

                string url = string.Format("{0}/CSS/{1}.css", LocalTemplatesAndStaticFilePath, referenceName);


				url = url.Replace("~/", "/");

                return string.Format("<link rel=\"stylesheet\" href=\"{0}\">", url);
            }
        }

        /// <summary>
        /// Returns the JS script element for the requested Agility Component JS.
        /// </summary>
        public static string JS(string referenceName)
        {
            referenceName = PrefixReferenceName(referenceName);
            if (UseInlineCode)
            {
                try
                {
                    return Html.AgilityJavascript(referenceName.ToLower());
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Cannot find Inline Code js file '{0}'", referenceName.ToLower()), ex.InnerException);
                }
            }
            else
            {
                string url = string.Format("{0}/JS/{1}.js", LocalTemplatesAndStaticFilePath, referenceName);
				url = url.Replace("~/", "/");
				return string.Format("<script type=\"text/javascript\" src=\"{0}\"></script>", url);
            }
        }

        /// <summary>
        /// Loads Agility Components Base CSS link element into AgilityContext.Page.MetaTagsRaw (if not already added)
        /// </summary>
        public static void EnsureBaseCSSLoaded()
        {
            if (!BaseCSSLoaded)
            {
                if (AgilityContext.Page != null)
                {
                    AgilityContext.Page.MetaTagsRaw += string.Format("{0}{1}", Environment.NewLine, CSS("base"));
                    BaseCSSLoaded = true;
                }
            }
        }

        
        private static string BaseCSSContextKey = "Agility.Components.BaseCSS";
        /// <summary>
        /// Checks whether the Agility Components Base CSS link element has been loaded on the current page.
        /// </summary>
        public static bool BaseCSSLoaded
        {
            get
            {

                bool isLoaded;
                bool.TryParse(string.Format("{0}", AgilityContext.HttpContext.Items[BaseCSSContextKey]), out isLoaded);
                return isLoaded;
            }
            set
            {
				AgilityContext.HttpContext.Items[BaseCSSContextKey] = value;
            }
        }

    }
}
