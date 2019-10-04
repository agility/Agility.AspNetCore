using System;

namespace Agility.Web.Objects.ServerAPI
{
    public class ContributorContentItem
    {
        //var contentItem = {
        //    ContributorID: Agility.UGC.API.APIProfileRecordID,
        //    ContributorName: userName,
        //    Type: "Article",
        //    Title: $("input[name='Title']", pnl).val(),
        //    Date: $("input[name='Date']", pnl).val(),
        //    TextBlob: textBlob,
        //    Excerpt: excerpt,
        //    CategoriesIDs: cats.join(","),
        //    SubcategoriesIDs: subCats.join(","),
        //    TagsIDs: tags.join(",")
        //};

        public int ContributorID { get; set; }
        public string ContributorName { get; set; }
		public string URL { get; set; }
		public string AuthorName { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string TextBlob { get; set; }
        public string Excerpt { get; set; }
        public string CategoriesIDs { get; set; }
        public int [] SubcategoriesIDs { get; set; }
        public int [] TagsIDs { get; set; }


    }
}
