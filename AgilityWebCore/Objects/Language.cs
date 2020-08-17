using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Objects
{
	public class Language
	{
		private string _languageCode;
		private string _languageName;

		public string LanguageCode
		{
			get { return _languageCode; }
			set { _languageCode = value; }
		}
		

		public string LanguageName
		{
			get { return _languageName; }
			set { _languageName = value; }
		}
	}
}
