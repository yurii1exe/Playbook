using Web.Domain.Core;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Globalization;
using Web.Domain.Entities;
using Web.Domain.IServices;
using Web.Domain.Extentions;

namespace API.Scrapping.Services
{
    public class MatchScrapingService : IScrapingService
    {
        private readonly ILogger<MatchScrapingService> _logger;
        private readonly AppConfiguration _appConfig;
        private readonly IBrowserService _browserService;
        private readonly MongoService<TeamBase> _teamService;

        public MatchScrapingService(
            ILogger<MatchScrapingService> logger,
            AppConfiguration appConfig,
            IBrowserService browserService,
            MongoService<TeamBase> teamService,
            DatabaseConfiguration databaseConfiguration)
        {
            _logger = logger;
            _appConfig = appConfig;
            _browserService = browserService;
            _teamService = teamService;
            _teamService.SetCollection(databaseConfiguration.TeamsCollection);
        }

        public async Task<List<Match>> ScrapeMatchesForLeagueAsync(string leagueId, string leagueUrl, CancellationToken cancellationToken = default)
        {
            var matches = new List<Match>();
            var page = await _browserService.GetPageAsync();

            await page.GoToAsync(leagueUrl);

            // Load all matches - updated selector for new structure
            while (await page.QuerySelectorAsync("[data-testid='event__more']") != null)
            {
                await page.EvaluateExpressionAsync("document.querySelector('[data-testid=\"event__more\"]')?.click()");
                await Task.Delay(_appConfig.WaitForLoad, cancellationToken);
            }

            // Updated selector for match elements
            var matchElements = await page.QuerySelectorAllAsync("[data-testid='event__match']");
            _logger.LogInformation("Found {MatchCount} matches", matchElements.Length);

            foreach (var matchElement in matchElements)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var matchId = (await matchElement.GetPropertyAsync("id")).RemoteObject.Value.ToString().Replace("g_1_", "");
                var match = await ScrapeMatchAsync(matchId, cancellationToken);
                
                if (match != null)
                {
                    matches.Add(match);
                }
            }

            return matches;
        }

        public async Task<Match> ScrapeMatchAsync(string matchId, CancellationToken cancellationToken = default)
        {
            var match = new Match { Id = matchId };
            
            using var page = await _browserService.CreateNewPageAsync();
            var matchUrl = $"https://www.flashscore.com/match/{matchId}/#/match-summary/";
            
            await Task.Delay(_appConfig.OpenPageDelay, cancellationToken);
            await page.GoToAsync(matchUrl + "match-summary/");
            
            // Wait for page to load and try to find the match header
            await Task.Delay(_appConfig.WaitForLoad, cancellationToken);
            
            // Updated selectors for match header - try new structure first, then fallback
            var matchHeader = await page.QuerySelectorAsync("[data-testid='duelParticipant']") 
                ?? await page.QuerySelectorAsync("div.duelParticipant") 
                ?? await page.QuerySelectorAsync(".duelParticipant")
                ?? await page.QuerySelectorAsync("[class*='duelParticipant']");
            if (matchHeader == null)
            {
                // Try to get some debug info about what's on the page
                var pageTitle = await page.GetTitleAsync();
                var bodyText = await page.EvaluateExpressionAsync("document.body.innerText");
                _logger.LogWarning("Match header not found for match {MatchId}. URL: {MatchUrl}. Page title: {PageTitle}. Body preview: {BodyPreview}", 
                    matchId, matchUrl, pageTitle, bodyText.ToString().Substring(0, Math.Min(200, bodyText.ToString().Length)));
                return null;
            }
            
            var matchHeaderText = (await matchHeader.GetPropertyAsync("outerText")).Convert();
            _logger.LogDebug("Raw match header text for {MatchId}: '{HeaderText}'", matchId, matchHeaderText);
            
            // Check if the header text is empty or very short
            if (string.IsNullOrWhiteSpace(matchHeaderText) || matchHeaderText.Length < 10)
            {
                _logger.LogWarning("Match header text is too short or empty for match {MatchId}: '{HeaderText}'. Trying to wait longer...", matchId, matchHeaderText);
                
                // Wait a bit more and try again
                await Task.Delay(_appConfig.WaitForLoad * 2, cancellationToken);
                matchHeaderText = (await matchHeader.GetPropertyAsync("outerText")).Convert();
                _logger.LogDebug("After waiting, header text for {MatchId}: '{HeaderText}'", matchId, matchHeaderText);
                
                if (string.IsNullOrWhiteSpace(matchHeaderText) || matchHeaderText.Length < 10)
                {
                    _logger.LogWarning("Match header still empty after waiting for match {MatchId}", matchId);
                    return null;
                }
            }
            
            var matchHeaderData = matchHeaderText.Split(',');

            if (matchHeaderData.Length < 6)
            {
                _logger.LogWarning("Unexpected match header format for match {MatchId}. Expected at least 6 elements, got {ElementCount}. Header text: '{HeaderText}'", 
                    matchId, matchHeaderData.Length, matchHeaderText);
                return null;
            }
            if (matchHeaderData[5].ToUpper() != "FINISHED")
            {
                return null;
            }
            match.Title = matchHeaderData[1] + " - " + matchHeaderData.LastOrDefault();
            _logger.LogInformation("Parsing {MatchTitle}", match.Title);

            // Parse teams - updated selector
            var participants = await page.QuerySelectorAllAsync("[data-testid='participant__participantLink']");
            if (participants.Length == 0)
            {
                // Fallback to old selector
                participants = await page.QuerySelectorAllAsync("a.participant__participantLink");
            }
            match.THome = await new Team().ConfigTeam(participants.FirstOrDefault());
            match.TGuest = await new Team().ConfigTeam(participants.LastOrDefault());

            // Add teams to collection if they don't exist
            await EnsureTeamExistsAsync(match.THome);
            await EnsureTeamExistsAsync(match.TGuest);

            await Task.Delay(_appConfig.OpenPageDelay, cancellationToken);

            // Parse goals - updated selector
            try
            {
                var incidentsData = await match.PopulateData("[data-testid='smv__incidentsHeader']", page, _logger);
                if (incidentsData == null || incidentsData.Count == 0)
                {
                    // Fallback to old selector
                    incidentsData = await match.PopulateData("div.smv__incidentsHeader", page, _logger);
                }
                
                if (incidentsData != null && incidentsData.Count > 0)
                {
                    var goalsPerFirst = incidentsData.FirstOrDefault()?.Split(',').LastOrDefault()?.Split('-');
                    if (goalsPerFirst != null && goalsPerFirst.Length >= 2)
                    {
                        match.THome.GoalsPerFirst = Convert.ToInt32(goalsPerFirst[0]);
                        match.TGuest.GoalsPerFirst = Convert.ToInt32(goalsPerFirst[1]);
                    }
                    
                    var goalsPerSecond = incidentsData.LastOrDefault()?.Split(',').LastOrDefault()?.Split('-');
                    if (goalsPerSecond != null && goalsPerSecond.Length >= 2)
                    {
                        match.THome.GoalsPerSecond = Convert.ToInt32(goalsPerSecond[0]);
                        match.TGuest.GoalsPerSecond = Convert.ToInt32(goalsPerSecond[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing goals for match {MatchId}: {ErrorMessage}", matchId, ex.Message);
                // Continue without goals data
            }

            // Parse summary - updated selector
            try
            {
                await Task.Delay(_appConfig.WaitForLoad, cancellationToken);
                
                // Updated selector for tournament header
                var matchRound = await page.EvaluateExpressionAsync("document.querySelector('[data-testid=\"tournamentHeader__country\"]')?.lastChild?.innerHTML || document.querySelector('span.tournamentHeader__country')?.lastChild?.innerHTML");
                var roundText = matchRound.ToString();
                if (!string.IsNullOrEmpty(roundText) && roundText.Contains("Round"))
                {
                    match.RoundNr = Convert.ToInt32(roundText.Substring(roundText.IndexOf("Round") + 6));
                }

                match.Date = DateTime.ParseExact(matchHeaderData[0].Replace('.', '-'), "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
                if (matchHeaderData.Length >= 5)
                {
                    match.Summary.Add(matchHeaderData[2] + matchHeaderData[3] + matchHeaderData[4]);
                }
                
                // Updated selector for summary data
                var summaryData = await match.PopulateData("[data-testid='smv__participantRow']", page, _logger);
                if (summaryData == null || summaryData.Count == 0)
                {
                    // Fallback to old selector
                    summaryData = await match.PopulateData("div.smv__participantRow", page, _logger);
                }
                
                if (summaryData != null)
                {
                    match.Summary = summaryData;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing summary for match {MatchId}: {ErrorMessage}", matchId, ex.Message);
                // Continue without summary data
            }

            // Parse stats per half - updated selector
            try
            {
                var statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/0", "[data-testid='stat__row']", page, _appConfig, _logger);
                if (statsArr == null || statsArr.Length < 2)
                {
                    // Fallback to old selector
                    statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/0", "div.stat__row", page, _appConfig, _logger);
                }
                
                if (statsArr != null && statsArr.Length >= 2)
                {
                    match.THome.Stats0 = statsArr[0];
                    match.TGuest.Stats0 = statsArr[1];
                }

                statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/1", "[data-testid='stat__row']", page, _appConfig, _logger);
                if (statsArr == null || statsArr.Length < 2)
                {
                    // Fallback to old selector
                    statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/1", "div.stat__row", page, _appConfig, _logger);
                }
                
                if (statsArr != null && statsArr.Length >= 2)
                {
                    match.THome.Stats1 = statsArr[0];
                    match.TGuest.Stats1 = statsArr[1];
                }
                
                statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/2", "[data-testid='stat__row']", page, _appConfig, _logger);
                if (statsArr == null || statsArr.Length < 2)
                {
                    // Fallback to old selector
                    statsArr = await match.PopulateData<Stats>(matchUrl + "match-statistics/2", "div.stat__row", page, _appConfig, _logger);
                }
                
                if (statsArr != null && statsArr.Length >= 2)
                {
                    match.THome.Stats2 = statsArr[0];
                    match.TGuest.Stats2 = statsArr[1];
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing stats for match {MatchId}: {ErrorMessage}", matchId, ex.Message);
                // Continue without stats data
            }

            // Parse lineups - updated selector
            try
            {
                await Task.Delay(_appConfig.OpenPageDelay, cancellationToken);
                await page.GoToAsync(matchUrl + "match-summary/lineups");
                await Task.Delay(_appConfig.WaitForLoad, cancellationToken);
                
                // Updated selector for lineups
                var lf = await match.PopulateData<Team>(matchUrl + "lineups", "[data-testid='lf__header']", page, _appConfig, _logger);
                if (lf == null || lf.Length < 2)
                {
                    // Fallback to old selector
                    lf = await match.PopulateData<Team>(matchUrl + "lineups", "div.lf__header", page, _appConfig, _logger);
                }

                // Log detailed info about the lf array
                if (lf == null)
                {
                    _logger.LogWarning("Lineup array is null for match {MatchId}", matchId);
                    return match;
                }
                
                _logger.LogDebug("Lineup array for match {MatchId}: Length={Length}, Values=[{Values}]", matchId, lf.Length, string.Join(", ", lf.Select(x => x == null ? "null" : $"Type={x.GetType().Name}, Formation={x.Formation}")));

                if (lf.Length < 2 || lf[0] == null || lf[1] == null)
                {
                    _logger.LogWarning("Lineup data not found or incomplete for match {MatchId}. Length={Length}, Values=[{Values}]", matchId, lf.Length, string.Join(", ", lf.Select(x => x == null ? "null" : $"Type={x.GetType().Name}, Formation={x.Formation}")));
                    return match;
                }
                
                // Safe access to Formation property
                if (lf[0]?.Formation != null)
                    match.THome.Formation = lf[0].Formation;
                if (lf[1]?.Formation != null)
                    match.TGuest.Formation = lf[1].Formation;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing lineups for match {MatchId}: {ErrorMessage}", matchId, ex.Message);
                // Continue without lineup data
            }

            return match;
        }

        private async Task EnsureTeamExistsAsync(Team team)
        {
            if (await _teamService.GetAsync(team.Id) == null)
            {
                await _teamService.CreateAsync(team.GetInstance());
                _logger.LogInformation("Added team: {TeamName}", team.Name);
            }
        }
    }
} 