using Web.Domain.Core;
using API.Scrapping.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Web.Domain.Entities;
using Web.Domain.IServices;

namespace API.Scrapping
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MongoService<Match> _matchService;
        private readonly ILeagueService _leagueService;
        private readonly IScrapingService _scrapingService;
        private readonly IUIService _uiService;
        private readonly AppConfiguration _appConfig;
        private int _attempts = 0;

        public Worker(
            ILogger<Worker> logger,
            MongoService<Match> matchService,
            ILeagueService leagueService,
            IScrapingService scrapingService,
            IUIService uiService,
            AppConfiguration appConfiguration
            )
        {
            _logger = logger;
            _matchService = matchService;
            _leagueService = leagueService;
            _scrapingService = scrapingService;
            _uiService = uiService;
            _appConfig = appConfiguration;

            _logger.LogInformation("Worker service initialized at {DateTime}", DateTime.Now);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting scraping process");

                // The match collection is not set here: there is no single one.
                // ProcessLeagueAsync points _matchService at the
                // {country}_{league}_{season} collection for each league in turn.

                // Get leagues to parse
                var leaguesToParse = await _leagueService.GetLeaguesToParseAsync();
                
                foreach (var leagueIndex in leaguesToParse)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await ProcessLeagueAsync(leagueIndex, stoppingToken);
                }
                
                _logger.LogInformation("Scraping process completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in scraping process");
                throw;
            }
        }

        private async Task ProcessLeagueAsync(string leagueIndex, CancellationToken stoppingToken)
        {
            try
            {
                var leagueId = Convert.ToInt32(leagueIndex);
                var collectionName = await _leagueService.GetCollectionNameAsync(leagueId);
                
                _logger.LogInformation("Processing league {LeagueIndex} with collection {CollectionName}", leagueIndex, collectionName);
                
                // Set the collection for this league
                _matchService.SetCollection(collectionName);
                
                var league = await _leagueService.GetLeagueByIndexAsync(leagueId);

                // Scrape matches for this league
                var matches = await _scrapingService.ScrapeMatchesForLeagueAsync(leagueIndex, league.FlashscoreLink, stoppingToken);
                
                // Save matches to database
                await SaveMatchesAsync(matches, collectionName, stoppingToken);
                
                _logger.LogInformation("League {LeagueName} processing completed", league.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing league {LeagueIndex}", leagueIndex);
                _attempts++;
                
                if (_attempts >= 5)
                {
                    _logger.LogError("Too many attempts, stopping process");
                    throw;
                }
                
                await Task.Delay(_appConfig.WaitForLoad * 3, stoppingToken);
            }
        }

        private async Task SaveMatchesAsync(List<Match> matches, string collectionName, CancellationToken stoppingToken)
        {
            var savedCount = 0;
            var skippedCount = 0;
            
            foreach (var match in matches)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    // Check if match already exists
                    if (await _matchService.GetAsync(match.Id) != null)
                    {
                        _logger.LogWarning("Match {MatchId} already exists in database {CollectionName}", match.Id, collectionName);
                        skippedCount++;
                        continue;
                    }

                    // Save match
                    await _matchService.CreateAsync(match);
                    savedCount++;
                    
                    _uiService.DisplayProgress($"Match added: {match.Title} [{savedCount}/{matches.Count}]");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving match {MatchId}", match.Id);
                    _attempts++;
                    
                    if (_attempts >= 5)
                    {
                        _logger.LogError("Too many attempts, stopping process");
                        throw;
                    }
                }
            }
            
            _logger.LogInformation("Saved {SavedCount} matches, skipped {SkippedCount} existing matches", savedCount, skippedCount);
        }


    }
}
