using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Contoso.Mail.Web.Services;

public partial class Program // Make Program public and in global namespace for integration tests
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();
        
        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages();

        // Register email services
        builder.Services.AddScoped<IExchangeServiceFactory, ExchangeServiceFactory>();
        builder.Services.AddScoped<IEmailService, EwsEmailService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Mail/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles(); // Use this instead of MapStaticAssets
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{action=Index}/{id?}",
            defaults: new { controller = "Mail" });

        app.MapControllerRoute(
            name: "mail",
            pattern: "mail/{action=Index}/{id?}",
            defaults: new { controller = "Mail" });

        app.MapGet("/signin-oidc", async context =>
        {
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                RedirectUri = "/"
            });
        });

        app.MapGet("/signout", async context =>
        {
            await context.SignOutAsync();
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                RedirectUri = "/"
            });
        });

        app.Run();
    }
}
