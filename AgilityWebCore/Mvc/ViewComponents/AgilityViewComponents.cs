using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Agility.Web.Mvc.ViewComponents
{
    public class AgilityInlineCode : ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync(string inlineCodePath, AgilityContentItem module)
        {
            return Task.Run<IViewComponentResult>(() =>
            {
                return View(inlineCodePath, module);
            });
        }
    }

    public class AgilityPartialView: ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync(string partialViewPath, object module)
        {
            return Task.Run<IViewComponentResult>(() =>
            {
                return View(partialViewPath, module);
            });
        }
    }
}
