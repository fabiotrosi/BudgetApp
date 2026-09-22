using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace BudgetApp.Services
{
    public interface IRazorViewToStringRenderer
    {
        Task<string> RenderAsync<TModel>(string viewPath, TModel model);
    }

    /// <summary>
    /// Renders a .cshtml view (e.g. under Views/Emails) to an HTML string outside of an
    /// HTTP request, so EmailService can reuse the app's Razor views/layouts for email bodies.
    /// </summary>
    public class RazorViewToStringRenderer : IRazorViewToStringRenderer
    {
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        public RazorViewToStringRenderer(
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider
        )
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
            var routeData = new RouteData();
            routeData.Values["controller"] = "Emails";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            var viewResult = _viewEngine.GetView(
                executingFilePath: null,
                viewPath: viewPath,
                isMainPage: true
            );
            if (!viewResult.Success)
                throw new InvalidOperationException($"Email view '{viewPath}' not found.");

            await using var writer = new StringWriter();
            var viewData = new ViewDataDictionary<TModel>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary()
            )
            {
                Model = model,
            };

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewData,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                writer,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return writer.ToString();
        }
    }
}
