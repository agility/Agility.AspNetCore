using System;
using System.Linq;

namespace Agility.Web.Extensions
{
	public static class DateTimeExtension
	{

        private static readonly TimeZoneInfo EastTimeZone = TimeZoneInfo.GetSystemTimeZones().Any(x => x.Id == "Eastern Standard Time") ?
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time") :
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        public static DateTime ConvertToEasternStandardTime(this DateTime date)
        {
            var result = date;
            
            if (TimeZoneInfo.Local.Id != EastTimeZone.Id)
            {
                result = TimeZoneInfo.ConvertTime(date, EastTimeZone);
            }

            return result;
        }

		public static string GetRFC822Date(this DateTime date)
		{
			int offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours;
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
