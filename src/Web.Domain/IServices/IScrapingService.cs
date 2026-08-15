using Web.Domain.Entities;

namespace Web.Domain.IServices
{
    public interface IScrapingService
    {
        /// <summary>
        /// Scrapes every finished match listed at <paramref name="leagueUrl"/>.
        /// </summary>
        /// <param name="leagueId">Index of the league being processed, for logging.</param>
        /// <param name="leagueUrl">Results page to start from.</param>
        Task<List<Match>> ScrapeMatchesForLeagueAsync(string leagueId, string leagueUrl, CancellationToken cancellationToken = default);
        Task<Match> ScrapeMatchAsync(string matchId, CancellationToken cancellationToken = default);
    }
} 