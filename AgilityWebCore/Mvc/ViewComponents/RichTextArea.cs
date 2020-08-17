using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Agility.Web.Mvc.ViewComponents
{
    public class RichTextArea : ViewComponent
    {
		public async Task<HtmlString> InvokeAsync(AgilityContentItem item)
		{
			AgilityContext.HttpContext = HttpContext;

			string value = item["TextBlob"] as string;
			
			return new HtmlString(value);
		}
		
	}
}
