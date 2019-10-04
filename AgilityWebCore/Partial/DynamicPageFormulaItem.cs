using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agility.Web.AgilityContentServer;
using System.Data;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Agility.Web.AgilityContentServer
{
	[Serializable]
	public class DynamicPageFormulaItem
	{
		public int PageID { get; set; }
		public int ContentID { get; set; }
		public string ContentReferenceName { get; set; }
		public DateTime LastModifiedDate { get; set; }
		public string Name { get; set; }
		public string Title { get; set; }
		public string MenuText { get; set; }
		public bool VisibleOnMenu { get; set; }
		public bool VisibleOnSitemap { get; set; }
		

		public string MetaKeyWords {get; set;}
		public string MetaDescription {get; set;}
		public string AdditionalHeaderCode {get; set;}

		public string TopScript {get; set;}
		public string BottomScript {get; set;}


		public string LanguageCode { get; set; }

		[NonSerialized]
		private DataRow row = null;

		public object GetContentItemValue(string columnName)
		{
			if (row == null)
			{

				if (string.IsNullOrEmpty(LanguageCode))
				{
					LanguageCode = AgilityContext.LanguageCode;
				}

				AgilityContent content = BaseCache.GetContent(ContentReferenceName, LanguageCode, AgilityContext.WebsiteName);
				row = content.GetItemByContentID(ContentID);				
			}

			if (row != null && row.Table.Columns.Contains(columnName))
			{
				return row[columnName];
			}

			return null;

		}

		//public Dictionary<string, object> RowValues { get; set; }

		public DynamicPageFormulaItem()
		{

		}

		public DynamicPageFormulaItem(Agility.Web.AgilityContentServer.AgilityPage page, string contentReferenceName, string languageCode, DataRow row)
		{

			DataTable dt = row.Table;

			//resolve the formulas...
			MenuText = ResolveFormula(page.DynamicPageMenuText, row, false);
			Title = ResolveFormula(page.DynamicPageTitle, row, false);
			Name = ResolveFormula(page.DynamicPageName, row, true);
			LanguageCode = languageCode;


			DateTime lastModified = DateTime.MinValue;
			object o = row["CreatedDate"];
			if (o is DateTime)
			{
				lastModified = (DateTime)o;
			}
			else
			{
				DateTime.TryParse($"{o}", out lastModified);
			}

			LastModifiedDate = lastModified;

			ContentReferenceName = contentReferenceName;
			int id = -1;
			if (!int.TryParse($"{row["ContentID"]}", out id)) id = -1;
			ContentID = id;
			
			VisibleOnMenu = page.DynamicPageVisibleOnMenu;
			VisibleOnSitemap = page.DynamicPageVisibleOnSitemap;
			

			if (dt.Columns.Contains("DynamicPageVisibleOnMenu")
				&& ! row.IsNull("DynamicPageVisibleOnMenu"))
			{
				VisibleOnMenu = string.Format("{0}", row["DynamicPageVisibleOnMenu"]).ToLowerInvariant() == "true";
			}

			if (dt.Columns.Contains("DynamicPageVisibleOnSitemap")
				&& ! row.IsNull("DynamicPageVisibleOnSitemap"))
			{
				VisibleOnSitemap = string.Format("{0}", row["DynamicPageVisibleOnSitemap"]).ToLowerInvariant() == "true";
			}

			//add the meta and script stuff...
			if (dt.Columns.Contains("DynamicPageMetaKeywords"))
			{
				MetaKeyWords = row["DynamicPageMetaKeywords"] as string;
				MetaDescription = row["DynamicPageMetaDescription"] as string;
				AdditionalHeaderCode = row["DynamicPageAdditionalHeaderCode"] as string;
				TopScript = row["DynamicPageTopScript"] as string;
				BottomScript = row["DynamicPageBottomScript"] as string;
			}

			
		}


		public static string ResolveFormula(string formula, DataRow row, bool removeSpecialCharacters)
		{
			/*
			 * EX: "Text ##FieldName## More Text"
			 * EX2: "Text ##FieldName:mm-dd-yyyy## More Text"
			 */
			if (string.IsNullOrEmpty(formula)) return string.Empty;
			StringBuilder sbOutput = new StringBuilder();

			Regex rex = new Regex("##[^#^ .][^#^ .]*##");
			MatchCollection matchCol = rex.Matches(formula);


			int index = 0;
			System.Data.DataTable dt = row.Table;

			foreach (Match match in matchCol)
			{
				sbOutput.Append(formula.Substring(index, match.Index - index));

				string fieldName = match.Value.Trim("##".ToCharArray());
				string formatString = "{0}";
				int colonIndex = fieldName.IndexOf(":");
				if (colonIndex != -1)
				{
					formatString = string.Format("{{0:{0}}}", fieldName.Substring(colonIndex + 1));
					fieldName = fieldName.Substring(0, colonIndex);
				}

				if (dt.Columns.Contains(fieldName))
				{
				    string fieldValue = string.Format(formatString, row[fieldName]);
					if (removeSpecialCharacters)
					{
					    fieldValue = MakeSegmentFriendly(fieldValue);
					}
					sbOutput.Append(fieldValue);
				}



				index = match.Index + match.Length;

			}

			if (index < formula.Length) sbOutput.Append(formula.Substring(index));

			return sbOutput.ToString();

		}

		internal static string RemoveSpecialCharacters(string str)
		{
            string s = RemoveDiacritics(str);
                
            s = Regex.Replace(s, @"[^\w\-@-]", "");

			return s;
		}

        /// <summary>
        /// http://en.wikipedia.org/wiki/Diacritic
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string RemoveDiacritics(string s)
        {

            String normalizedString = s.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < normalizedString.Length; i++)
            {
                Char c = normalizedString[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append(c);
            }

            return stringBuilder.ToString();

        }

        /// <summary>
        /// Turns segment into a friendly string. Suitable for just one fragment of a URL. Forward slashes will be escaped like everything else.
        /// </summary>
        internal static string MakeSegmentFriendly(string segment)
        {
            string result = RemoveSpecialCharacters(segment.Replace(" ", "-"));
            return Regex.Replace(result, "--+", "-");
        }

        /// <summary>
        /// Turns path into a friendly string. Suitable for a slash-separated path. Forward slashes will not be escaped.
        /// </summary>
        internal static string MakePathFriendly(string path)
        {
            string[] friendlySegments = path.Split('/').Select(MakeSegmentFriendly).ToArray();
            return String.Join("/", friendlySegments);
        }
	}
}
