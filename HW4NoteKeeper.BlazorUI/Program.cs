using HW4NoteKeeper.BlazorUI.Components;
using HW4NoteKeeper.BlazorUI.Services;

namespace HW4NoteKeeper.BlazorUI
{
    /// <summary>
    /// The main entry point for the Blazor UI application.
    /// Configures services and middleware for the NoteKeeper web interface.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Configures services, middleware, and starts the web host.
        /// </summary>
        /// <param name="args">Command line arguments passed to the application.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Configure HttpClient for the API
            var apiBaseUrl = builder.Configuration["NoteKeeperApi:BaseUrl"] 
                ?? throw new InvalidOperationException("NoteKeeperApi:BaseUrl not configured");

            builder.Services.AddHttpClient<NoteKeeperApiService>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}

 
