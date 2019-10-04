using Agility.Web.Objects;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Data;

namespace Agility.Web.Mvc
{
	
	public class AgilityTemplateModel
	{
		[Required]
		[DataType(DataType.Custom)]
		[DisplayName("Current Page")]
		public AgilityPage Page { get; set; }


		

	}

	public class AgilityModuleModel
	{

		[Required]
		[DataType(DataType.Custom)]
		[DisplayName("Module Properties")]
		public DataRowView ModuleProperties { get; set; }

		[Required]
		[DataType(DataType.Text)]
		[DisplayName("Language Code")]
		public string LanguageCode { get; set; }

		[Required]
		[DataType(DataType.Text)]
		[DisplayName("Module Content Name")]
		public string ModuleContentName { get; set; }


		public AgilityContent GetLinkedContent(string propertyName)
		{

			if (string.IsNullOrEmpty(propertyName)) return null;
			if (ModuleProperties == null) return null;
			string refName = ModuleProperties[propertyName] as string;
			return Data.GetContent(refName);
		}
		
	}

	public class AgilityModuleModel<TAgilityContentItem> : AgilityModuleModel where TAgilityContentItem : AgilityContentItem
	{
		[Required]
		[DataType(DataType.Custom)]
		[DisplayName("Module")]
		public TAgilityContentItem Module { get; set; }
	}

}