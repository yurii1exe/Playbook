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

        [Fact]
        public void Falls_back_to_the_last_segment_when_the_path_is_short()
        {
            var league = new League { FlashscoreLink = "https://www.flashscore.com/mls/" };

            Assert.Equal("_mls", league.GetFileName);
        }
    }
}
