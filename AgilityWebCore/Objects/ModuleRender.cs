using Microsoft.AspNetCore.Html;
using System.Threading.Tasks;

namespace Agility.Web.Objects
{
    internal class ModuleRender
	{
		public string ContentReferenceName;
		public int ContentID;
		public Task<IHtmlContent> RenderTask;

		private IHtmlContent _PreRenderedContent = null;

		public IHtmlContent PreRenderedContent
		{
			get
			{
				if (_PreRenderedContent == null)
				{
					if (RenderTask != null) _PreRenderedContent = RenderTask.Result;
				}
				return _PreRenderedContent;
			}
			set
			{
				_PreRenderedContent = value;
			}
		}
		public AgilityContentServer.AgilityExperimentVariant Variant;
	}
}
