using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using System.Reflection;

namespace Agility.Web.Extensions
{
	public static class HtmlHelperViewExtensions
	{

		public static ControllerActionDescriptor GetController(this IHtmlHelper helper, string action, string controller)
		{
			var currentHttpContext = helper.ViewContext?.HttpContext;
			//var cFactoryProvider = GetServiceOrFail<IControllerFactoryProvider>(currentHttpContext);



			//var cFactory = cFactoryProvider.CreateControllerFactory(new ControllerActionDescriptor()
			//{
			//	ControllerName = controllerName,
			//	ActionName = action		,
			//	ControllerTypeInfo = new TypeInfo()
			//});

			//return cFactory;
			
			var actionSelector = GetServiceOrFail<IActionDescriptorCollectionProvider>(currentHttpContext);
			
			var items = actionSelector.ActionDescriptors.Items;

			foreach (var item in items)
			{
				ControllerActionDescriptor cad = item as ControllerActionDescriptor;
				if (cad == null) continue;

				if (string.Equals(cad.ActionName, action, StringComparison.CurrentCultureIgnoreCase)
					&& string.Equals(cad.ControllerName, controller, StringComparison.CurrentCultureIgnoreCase))
				{
					return cad;
				}
			}

			return null;

		}

		public static HtmlString RenderAction(this IHtmlHelper helper, string action, RouteData routeData = null)
		{
			var controller = (string)helper.ViewContext.RouteData.Values["controller"];

			return RenderAction(helper, action, controller, routeData);
		}

		public static HtmlString RenderAction(this IHtmlHelper helper, string action, string controller, RouteData routeData = null)
		{
			var area = (string)helper.ViewContext.RouteData.Values["area"];

			return RenderAction(helper, action, controller, area, routeData);
		}

		public static HtmlString RenderAction(this IHtmlHelper helper, string action, string controller, string area, RouteData routeData = null)
		{
			if (action == null)
				throw new ArgumentNullException("action");

			if (controller == null)
				throw new ArgumentNullException("controller");

			

			var task = RenderActionAsync(helper, action, controller, area, routeData);

			return task.Result;
		}

		private static async Task<HtmlString> RenderActionAsync(this IHtmlHelper helper, string action, string controller, string area,
			RouteData routeData = null)
		{
			// fetching required services for invocation
			var currentHttpContext = helper.ViewContext?.HttpContext;
			
			
			var httpContextFactory = GetServiceOrFail<IHttpContextFactory>(currentHttpContext);
			var actionInvokerFactory = GetServiceOrFail<IActionInvokerFactory>(currentHttpContext);
			var actionSelector = GetServiceOrFail<IActionDescriptorCollectionProvider>(currentHttpContext);

			// creating new action invocation context
			var routeData2 = new RouteData();
			//var routeParams = new RouteValueDictionary(parameters ?? new { });
			//var routeValues = new RouteValueDictionary(new { area = area, controller = controller, action = action });
			var newHttpContext = httpContextFactory.Create(currentHttpContext.Features);

			newHttpContext.Response.Body = new MemoryStream();

			//foreach (var router in helper.ViewContext.RouteData.Routers)
			//	routeData.PushState(router, null, null);

			//routeData2.PushState(null, routeValues, null);
			//routeData.PushState(null, routeParams, null);

			ControllerActionDescriptor actionDescriptor = null;

			foreach (var item in actionSelector.ActionDescriptors.Items)
			{
				ControllerActionDescriptor cad = item as ControllerActionDescriptor;
				if (cad == null) continue;

				if (string.Equals(cad.ActionName, action, StringComparison.CurrentCultureIgnoreCase)
					&& string.Equals(cad.ControllerName, controller, StringComparison.CurrentCultureIgnoreCase))
				{
					actionDescriptor = cad;
					break;
				}
			}

			if (actionDescriptor == null) throw new ApplicationException($"The controller/action {controller}/{action} could not be found.");


			var cFactoryProvider = GetServiceOrFail<IControllerFactoryProvider>(currentHttpContext);
			
			var cFactory = cFactoryProvider.CreateControllerFactory(new ControllerActionDescriptor()
			{
				ControllerName = controller,
				ActionName = action,
				ControllerTypeInfo = actionDescriptor.ControllerTypeInfo
			});


			ActionContext actionContext = new ActionContext(currentHttpContext, routeData, actionDescriptor);
			ControllerContext context = new ControllerContext(actionContext);

			var invoker = actionInvokerFactory.CreateInvoker(actionContext);

			string content = null;

			await invoker.InvokeAsync().ContinueWith(task =>
			{
				
				if (task.IsFaulted)
				{
					content = task.Exception.Message;
				}
				else if (task.IsCompleted)
				{
					
					//newHttpContext.Response.Body.Position = 0;
					//using (var reader = new StreamReader(newHttpContext.Response.Body))
					//	content = reader.ReadToEnd();
				}
			});

			return new HtmlString(content);
		}

		

		internal static TService GetServiceOrFail<TService>(HttpContext httpContext)
		{
			if (httpContext == null)
				throw new ArgumentNullException(nameof(httpContext));

			var service = httpContext.RequestServices.GetService(typeof(TService));

			if (service == null)
				throw new InvalidOperationException($"Could not locate service: {nameof(TService)}");

			return (TService)service;
		}
	}
}
