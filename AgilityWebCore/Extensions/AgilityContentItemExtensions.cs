namespace Agility.Web.Extensions
{
    public static class AgilityContentItemExtensions
    {  
        public static string CommentsRecordTypeName(this AgilityContentItem item)
        {
            var table = item.Row.Table;
            return table.ExtendedProperties.ContainsKey("CommentsRecordTypeName") ? table.ExtendedProperties["CommentsRecordTypeName"].ToString() : "";
        }

        public static string CommentsRecordTypeName(this AgilityContentItem item, string referenceName)
        {
			Agility.Web.Objects.AgilityContent content = Data.GetContent(referenceName);
			var dt = content.ContentItems;

			return dt != null && dt.ExtendedProperties.ContainsKey("CommentsRecordTypeName") ? dt.ExtendedProperties["CommentsRecordTypeName"].ToString() : "";
        }

        public static UrlField ParseUrl(this AgilityContentItem item, string urlField)
        {
            if (item == null) return null;

            UrlField link;

            string a = string.Format("{0}", item.Row[urlField]);

            if (a.StartsWith("~/") || a.StartsWith("/") || a.StartsWith("http"))
            {
                link = new UrlField {Href = a};
            }
            else
            {
                try
                {
                    link = new UrlField {Text = a.StripHtml()};

                    var parts = a.ToStrings('>');
                    var qs = parts[0].Replace("<a ", "").Replace(" ", "&").Replace("\"", "");
                    var properties = System.Web.HttpUtility.ParseQueryString(qs);

                    link.Target = properties["target"];
                    link.Href = properties["href"];
                }
                catch (System.Exception)
                {
                    link = null;
                }
            }
            return link;
        }
    }

    public class UrlField
    {
        public string Text { get; set; }
        public string Href { get; set; }
        public string Target { get; set; }
    }
}