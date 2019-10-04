using System;

namespace Agility.Web.Objects.ServerAPI
{
	public class ThumbnailSettings
	{
		public string ID = Guid.NewGuid().ToString().ToLower();
		public string Name;

		/// <summary>
		/// Crop = 3, Resize & Fill = 2, Scale = 1
		/// </summary>
		public int Type = 3;
		public int Width;
		public int Height;

		/// <summary>
		/// JPeg quality (0-100)
		/// </summary>
		public int Quality = 90;

		/// <summary>
		/// Top = 1, Center = 2, Bottom = 3
		/// </summary>
		public int VerticalAlignment = 2;


		/// <summary>
		/// Left = 1, Center = 2, Right = 3
		/// </summary>
		public int HorizontalAlignment = 2;

		/// <summary>
		/// HEX Value eg #000000, #FFFFFF
		/// </summary>
		public string FillColor = "#000000";
		public string Title;
	}
}
