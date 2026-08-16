using Web.Domain.Entities;
using Xunit;

namespace Web.Domain.Tests
{
    /// <summary>
    /// The league part of a collection name is derived from the source URL, so
    /// a change to the URL shape must not be able to silently rename every
    /// collection.
    /// </summary>
    public class LeagueTests
    {
        [Theory]
        [InlineData("https://www.flashscore.com/football/spain/laliga/", "_laliga")]
        [InlineData("https://www.flashscore.com/football/australia/a-league/", "_a_league")]
        [InlineData("https://www.flashscore.com/football/england/premier-league/results/", "_premier_league")]
        [InlineData("https://www.flashscore.com/football/germany/2-bundesliga", "_2_bundesliga")]
        public void Derives_the_league_segment_from_the_url_path(string link, string expected)
        {
            var league = new League { FlashscoreLink = link };

            Assert.Equal(expected, league.GetFileName);
        }

        [Fact]
        public void Ignores_a_change_of_host_or_scheme()
        {
            var current = new League { FlashscoreLink = "https://www.flashscore.com/football/poland/ekstraklasa/" };
            var moved = new League { FlashscoreLink = "http://m.flashscore.co.uk/football/poland/ekstraklasa/" };

            Assert.Equal(current.GetFileName, moved.GetFileName);
        }

        /// <summary>
        /// Pressing enter at the season prompt names the collection after the
        /// current season, and the shape of a season name depends on the
        /// competition.
        /// </summary>
        [Fact]
        public void Names_the_current_season_by_the_two_years_a_european_league_spans()
        {
            var league = new League
            {
                Name = "LaLiga",
                Country = new League.CountryObj { Name = "spain", Code = "es" }
            };

            Assert.Equal("2025-2026", league.DefaultSeason(new DateTime(2026, 8, 16)));
        }

        [Fact]
        public void Names_the_current_season_of_a_single_year_competition_by_that_year()
        {
            // MLS runs inside one calendar year. Its country is the United
            // States, so a check against the country name cannot see this.
            var league = new League
            {
                Name = "MLS",
                Country = new League.CountryObj { Name = "united states of america", Code = "us" }
            };

            Assert.True(league.HasSingleYearSeason);
            Assert.Equal("2026", league.DefaultSeason(new DateTime(2026, 8, 16)));
        }

        [Fact]
        public void Treats_every_other_competition_as_spanning_two_years()
        {
            var league = new League
            {
                Name = "Premier League",
                Country = new League.CountryObj { Name = "england", Code = "gb-eng" }
            };

            Assert.False(league.HasSingleYearSeason);
            Assert.Equal("2022-2023", league.DefaultSeason(new DateTime(2023, 1, 31)));
        }

        [Fact]
        public void Falls_back_to_the_last_segment_when_the_path_is_short()
        {
            var league = new League { FlashscoreLink = "https://www.flashscore.com/mls/" };

            Assert.Equal("_mls", league.GetFileName);
        }
    }
}
