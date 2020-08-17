
namespace Agility.Web.Extensions
{
    public static class IntegerExtensions
    {
		public static string ToOrdinalString(this int number)
		{
			int condition1 = number % 100;
			int condition2 = number % 10;

			if (condition1 == 11 || condition1 == 12 || condition1 == 13) return string.Format("{0}th", number);

			switch (condition2)
			{
				case 1: return string.Format("{0}st", number);
				case 2: return string.Format("{0}nd", number);
				case 3: return string.Format("{0}rd", number);
				default: return string.Format("{0}th", number);
			}
		}
    }
}