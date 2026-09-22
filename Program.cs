using BudgetApp.Data;
using BudgetApp.Data.Repositories;
using BudgetApp.Models;
using BudgetApp.Resources;
using BudgetApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using NLog;
using NLog.Web;

// Early init of NLog to allow startup and exception logging, before host is built
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder
        .Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
        })
        .AddDataAnnotationsLocalization(options =>
        {
            options.DataAnnotationLocalizerProvider = (type, factory) =>
                factory.Create(typeof(DataAnnotations));
        });

    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    // Authentication
    builder
        .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

    builder.Services.AddScoped<IPasswordHasher<UserModel>, PasswordHasher<UserModel>>();

    // Email
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

    // Dependency Injection for Repositories
    builder.Services.AddScoped<DapperContext>();
    builder.Services.AddScoped<IBudgetRepository<BudgetModel>, BudgetRepository<BudgetModel>>();
    builder.Services.AddScoped<
        ICategoryRepository<CategoryModel>,
        CategoryRepository<CategoryModel>
    >();
    builder.Services.AddScoped<
        IPositionRepository<PositionModel>,
        PositionRepository<PositionModel>
    >();
    builder.Services.AddScoped<
        IPositionTypeRepository<PositionTypeModel>,
        PositionTypeRepository<PositionTypeModel>
    >();
    builder.Services.AddScoped<
        ISubCategoryRepository<SubCategoryModel>,
        SubCategoryRepository<SubCategoryModel>
    >();
    builder.Services.AddScoped<
        ITemplateBudgetRepository<TemplateBudgetModel>,
        TemplateBudgetRepository<TemplateBudgetModel>
    >();
    builder.Services.AddScoped<
        ITemplatePositionRepository<TemplatePositionModel>,
        TemplatePositionRepository<TemplatePositionModel>
    >();
    builder.Services.AddScoped<IUserRepository<UserModel>, UserRepository<UserModel>>();
    builder.Services.AddScoped<
        IBudgetUserRepository<BudgetUserModel>,
        BudgetUserRepository<BudgetUserModel>
    >();
    builder.Services.AddScoped<
        IBudgetInviteRepository<BudgetInviteModel>,
        BudgetInviteRepository<BudgetInviteModel>
    >();
    builder.Services.AddScoped<
        ITransactionRepository<TransactionModel>,
        TransactionRepository<TransactionModel>
    >();
    builder.Services.AddScoped<
        ITransactionDocumentRepository<TransactionDocumentModel>,
        TransactionDocumentRepository<TransactionDocumentModel>
    >();

    // NLog: Setup NLog for Dependency injection
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var app = builder.Build();

    var supportedCultures = new[] { "de-CH" };
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("de-CH")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    app.UseRequestLocalization(localizationOptions);

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit
    LogManager.Shutdown();
}
