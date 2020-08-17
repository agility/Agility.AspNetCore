using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agility.Web.Extensions
{
	public static class DateTimeExtension
	{

		public static string GetRFC822Date(this DateTime date)
		{
			int offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Hours;
			string timeZone = "+" + offset.ToString().PadLeft(2, '0');

			if (offset < 0)
			{
				int i = offset * -1;
				timeZone = "-" + i.ToString().PadLeft(2, '0');
			}

			return date.ToString("ddd, dd MMM yyyy HH:mm:ss " + timeZone.PadRight(5, '0'));
		}

	}
}
