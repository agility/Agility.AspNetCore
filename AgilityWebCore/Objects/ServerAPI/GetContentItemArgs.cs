using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Agility.Web.Objects.ServerAPI
{
    public class GetContentItemArgs
    {
        //var arg = {
        //    referenceName: "ArticleCategories",
        //    pageSize: 100,
        //    rowOffset:0,
        //    searchFilter:"",
        //    sortField:"Title",
        //    sortDirection:"",
        //    languageCode:C.LanguageCode,
        //    columns: "Title"
        //};

		public GetContentItemArgs()
		{
			searchFilter = string.Empty;
			sortField = string.Empty;
			sortDirection = string.Empty;
		}


        public string referenceName { get; set; }
        public int pageSize { get; set; }
        public int rowOffset { get; set; }
        public string searchFilter { get; set; }
        public string sortField { get; set; }
        public string sortDirection { get; set; }
        public string languageCode { get; set; }
        public string columns { get; set; }
		public bool includeDeleted { get; set; }


		private string _hash = null;
        public string hash
        {
            get
            {
                return _hash;
            }
            set
            {
                _hash = value;
            }
        }

    }
}