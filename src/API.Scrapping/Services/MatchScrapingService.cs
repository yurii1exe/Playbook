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
        // Every selector below is a fallback chain, newest markup first. The
        // source has renamed these once already: the data-testid attributes it
        // used in 2023-2025 were dropped in 2026 in favour of the class names it
        // had before that, so both layers stay in place.
        private const string MatchRowSelector = "[data-testid='event__match'], div.event__match";
        private const string ShowMoreSelector = "[data-testid='event__more'], a.event__more";
        private const string StatRowSelector = "[data-testid='wcl-statistics'], [data-testid='stat__row'], div.stat__row";
        private const string FormationSelector = "div.lf__formationHeader, [data-testid='lf__header'], div.lf__header";
        private const string HalfHeaderSelector = "[data-testid='wcl-headerSection-text']";

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

            // The list is rendered client-side after the load event, so wait for
            // the first row instead of racing it.
            try
            {
                await page.WaitForSelectorAsync(MatchRowSelector,
                    new WaitForSelectorOptions { Timeout = _appConfig.WaitForLoad * 4 });
            }
            catch (WaitTaskTimeoutException)
            {
                _logger.LogWarning("No match rows appeared on {LeagueUrl} within {Timeout}ms", leagueUrl, _appConfig.WaitForLoad * 4);
            }

            // Load all matches
            while (await page.QuerySelectorAsync(ShowMoreSelector) != null)
            {
                await page.EvaluateFunctionAsync("selector => document.querySelector(selector)?.click()", ShowMoreSelector);
                await Task.Delay(_appConfig.WaitForLoad, cancellationToken);
            }

            var matchElements = await page.QuerySelectorAllAsync(MatchRowSelector);
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
            
            // The source redirects /match/{id}/ to a slug URL and now serves the
            // statistics and lineups as paths off that, not as hash routes, so
            // the sub-pages are built from where we actually landed.
            var landedUrl = page.Url;

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
                if (incidentsData == null || incidentsData.Count == 0)
                {
                    // 2026 markup: the half-time scores moved into the generic
                    // section headers, so the rows have to be filtered by label.
                    incidentsData = (await match.PopulateData(HalfHeaderSelector, page, _logger))
                        .Where(header => header.Contains("HALF", StringComparison.OrdinalIgnoreCase))
                        .ToList();
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

                // Kick-off time and score come from the header we already read,
                // so they are set before anything that touches the page again.
                match.Date = DateTime.ParseExact(matchHeaderData[0].Replace('.', '-'), "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
                if (matchHeaderData.Length >= 5)
                {
                    match.Summary.Add(matchHeaderData[2] + matchHeaderData[3] + matchHeaderData[4]);
                }

                // Round number: current markup first, then the two shapes the
                // source used before it. Returns null when none of them match,
                // which is a missing round rather than a failed match.
                var matchRound = await page.EvaluateExpressionAsync(
                    "document.querySelector('[data-testid=\"tournamentHeader__country\"]')?.lastChild?.innerHTML" +
                    " || document.querySelector('span.tournamentHeader__country')?.lastChild?.innerHTML" +
                    " || document.querySelector('[data-testid=\"wcl-scores-overline-03\"]')?.textContent" +
                    " || null");
                var roundText = matchRound?.ToString() ?? string.Empty;
                var roundNumber = System.Text.RegularExpressions.Regex.Match(
                    roundText, @"Round\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (roundNumber.Success)
                {
                    match.RoundNr = int.Parse(roundNumber.Groups[1].Value);
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

            // Parse stats: full match, then each half separately
            try
            {
                var full = await ScrapeStatsAsync(match, page, landedUrl, matchUrl, "summary/stats/overall", "match-statistics/0");
                if (full.Length >= 2)
                {
                    match.THome.Stats0 = full[0];
                    match.TGuest.Stats0 = full[1];
                }

                var firstHalf = await ScrapeStatsAsync(match, page, landedUrl, matchUrl, "summary/stats/1st-half", "match-statistics/1");
                if (firstHalf.Length >= 2)
                {
                    match.THome.Stats1 = firstHalf[0];
                    match.TGuest.Stats1 = firstHalf[1];
                }

                var secondHalf = await ScrapeStatsAsync(match, page, landedUrl, matchUrl, "summary/stats/2nd-half", "match-statistics/2");
                if (secondHalf.Length >= 2)
                {
                    match.THome.Stats2 = secondHalf[0];
                    match.TGuest.Stats2 = secondHalf[1];
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
                var lf = await match.PopulateData<Team>(BuildSectionUrl(landedUrl, "summary/lineups"), FormationSelector, page, _appConfig, _logger);
                if (lf == null || lf.Length < 2)
                {
                    // Fallback to the hash route the source used until 2026
                    lf = await match.PopulateData<Team>(matchUrl + "lineups", FormationSelector, page, _appConfig, _logger);
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

        /// <summary>
        /// Reads one statistics period. Tries the current path-based sub-page
        /// first and falls back to the hash route the source used until 2026, so
        /// a match that still serves the old shape is not lost.
        /// </summary>
        private async Task<Stats[]> ScrapeStatsAsync(Match match, IPage page, string landedUrl, string legacyMatchUrl, string section, string legacySection)
        {
            var stats = await match.PopulateData<Stats>(BuildSectionUrl(landedUrl, section), StatRowSelector, page, _appConfig, _logger);

            if (stats == null || stats.Length < 2)
            {
                stats = await match.PopulateData<Stats>(legacyMatchUrl + legacySection, StatRowSelector, page, _appConfig, _logger);
            }

            if (stats == null || stats.Length < 2)
            {
                _logger.LogWarning("No statistics found for match {MatchId} section {Section}", match.Id, section);
                return Array.Empty<Stats>();
            }

            return stats;
        }

        /// <summary>
        /// Builds a sub-page URL from the URL the match page redirected to:
        /// "/match/football/a/b/?mid=X" plus "summary/stats/overall" becomes
        /// "/match/football/a/b/summary/stats/overall/?mid=X".
        /// </summary>
        internal static string BuildSectionUrl(string landedUrl, string section)
        {
            var uri = new Uri(landedUrl);
            var path = uri.AbsolutePath.TrimEnd('/');
            return $"{uri.Scheme}://{uri.Host}{path}/{section}/{uri.Query}";
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