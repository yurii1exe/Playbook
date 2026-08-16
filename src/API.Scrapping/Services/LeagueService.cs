using Web.Domain.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;
using Web.Domain.Entities;
using Web.Domain.IServices;


namespace API.Scrapping.Services
{
    public class LeagueService : ILeagueService
    {
        private readonly ILogger<LeagueService> _logger;
        private readonly MongoService<League> _leagueService;
        private readonly IUIService _uiService;

        public LeagueService(
            ILogger<LeagueService> logger,
            MongoService<League> leagueService,
            IUIService uiService,
            DatabaseConfiguration databaseConfiguration)
        {
            _logger = logger;
            _leagueService = leagueService;
            _uiService = uiService;
            _leagueService.SetCollection(databaseConfiguration.LeaguesCollection);
        }

        public async Task<List<League>> GetAllLeaguesAsync()
        {
            var leagues = await _leagueService.GetAsync() ?? new List<League>();
            
            if (leagues.Count < 1)
            {
                await InitializeLeaguesFromJsonAsync();
                return await GetAllLeaguesAsync();
            }

            return leagues;
        }

        public async Task<League> GetLeagueByIdAsync(string id)
        {
            return await _leagueService.GetAsync(id);
        }

        public async Task<League> GetLeagueByIndexAsync(int index)
        {
            var leagues = await GetAllLeaguesAsync();
            return leagues[index];
        }

        public async Task<string> GetCollectionNameAsync(int leagueIndex, string year = null)
        {
            var league = await GetLeagueByIndexAsync(leagueIndex);
            var url = league.FlashscoreLink;

            if (string.IsNullOrEmpty(year))
            {
                year = await _uiService.GetUserYearInputAsync();
            }

            if (url.Contains("results"))
            {
                url = url.Replace("/results", "");
                url += "-" + year;
            }
            else
            {
                if (!string.IsNullOrEmpty(year))
                {
                    url = url.Substring(0, url.LastIndexOf('/'));
                    url += "-" + year;
                }

                if (url.EndsWith('/'))
                {
                    url += "results";
                }
                else
                {
                    url += "/results";
                }
            }

            if (string.IsNullOrEmpty(year))
            {
                year = league.DefaultSeason(DateTime.Now);
            }

            return league.Country.Code + league.GetFileName + "_" + year;
        }

        public async Task<string[]> GetLeaguesToParseAsync()
        {
            var leagues = await GetAllLeaguesAsync();
            _logger.LogInformation("Found {LeagueCount} leagues", leagues.Count);
            
            _uiService.DisplayLeagues(leagues);
            return await _uiService.GetUserLeagueSelectionAsync(leagues);
        }

        private async Task InitializeLeaguesFromJsonAsync()
        {
            try
            {
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var leaguesJson = await File.ReadAllTextAsync(path + "/Data/leagues.json");
                var leaguesArr = JsonConvert.DeserializeObject<List<League>>(leaguesJson);
                
                foreach (var league in leaguesArr)
                {
                    await _leagueService.CreateAsync(league);
                }
                
                _logger.LogInformation("Initialized {LeagueCount} leagues from JSON", leaguesArr.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize leagues from JSON");
                throw;
            }
        }
    }
} 