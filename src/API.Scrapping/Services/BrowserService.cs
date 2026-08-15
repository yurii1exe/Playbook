using Web.Domain.Core;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Web.Domain.IServices;

namespace API.Scrapping.Services
{
    public class BrowserService : IBrowserService
    {
        private readonly ILogger<BrowserService> _logger;
        private readonly AppConfiguration _appConfig;
        private IBrowser _browser;
        private IPage _page;
        private bool _disposed = false;

        public BrowserService(ILogger<BrowserService> logger, AppConfiguration appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public async Task InitializeAsync()
        {
            if (_browser != null) return;

            var osNameAndVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            _logger.LogInformation("Operating System: {OS}", osNameAndVersion);

            var browserPath = _appConfig.BrowserPath;
            if (!osNameAndVersion.ToLowerInvariant().Contains("windows"))
            {
                browserPath = _appConfig.BrowserPathMac;
            }

            var options = new LaunchOptions()
            {
                Headless = _appConfig.HeadlessBrowser,
                ExecutablePath = browserPath,
                Product = Product.Chrome
            };

            _browser = await Puppeteer.LaunchAsync(options);
            _page = await _browser.NewPageAsync();
            
            _logger.LogInformation("Browser initialized successfully");
        }

        public async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser == null)
                await InitializeAsync();
            return _browser;
        }

        public async Task<IPage> GetPageAsync()
        {
            if (_page == null)
                await InitializeAsync();
            return _page;
        }

        public async Task<IPage> CreateNewPageAsync()
        {
            if (_browser == null)
                await InitializeAsync();
            return await _browser.NewPageAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _page?.CloseAsync();
                _browser?.CloseAsync();
                _browser?.Dispose();
                _disposed = true;
            }
        }
    }
} 