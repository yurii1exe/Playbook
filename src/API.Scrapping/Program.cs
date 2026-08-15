using Web.Domain.Core;
using API.Scrapping.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Web.Domain.IServices;

namespace API.Scrapping
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(c =>
            {
                c.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices(services =>
            {
                // Configuration
                services.AddSingleton<AppConfiguration>();
                services.AddSingleton<DatabaseConfiguration>();
                
                // Data Services
                services.AddSingleton(typeof(MongoService<>));
                
                // Application Services
                services.AddScoped<IBrowserService, BrowserService>();
                services.AddScoped<ILeagueService, LeagueService>();
                services.AddScoped<IScrapingService, MatchScrapingService>();
                services.AddScoped<IUIService, UIService>();
                
                // Background Service
                services.AddHostedService<Worker>();
            })
            .Build();
            await host.RunAsync();
        }
    }
}
