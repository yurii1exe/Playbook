using Newtonsoft.Json;
using Web.Domain.Entities;
using Web.Domain.Extentions;
using Xunit;

namespace Web.Domain.Tests
{
    /// <summary>
    /// The row texts in these tests were captured from a real match page on
    /// 15 August 2026, after the source had renamed several statistics and
    /// changed the shape of the pass and tackle rows. They are the fixtures the
    /// parser is expected to survive; a browser is not involved.
    /// </summary>
    public class StatRowParserTests
    {
        [Fact]
        public void Reads_home_label_and_guest_from_a_three_cell_row()
        {
            var cells = StatRowParser.ParseRow("58%,Ball possession,42%").ToList();

            var cell = Assert.Single(cells);
            Assert.Equal("Ballpossession", cell.Key);
            Assert.Equal("58", cell.Home);
            Assert.Equal("42", cell.Guest);
        }

        [Theory]
        // The source renamed these between seasons; the model did not.
        [InlineData("12,Total shots,12", "GoalAttempts")]
        [InlineData("5,Shots on target,2", "ShotsOnGoal")]
        [InlineData("3,Shots off target,6", "ShotsOffGoal")]
        [InlineData("26,Throw ins,16", "Throw-in")]
        [InlineData("1,Goalkeeper saves,3", "GoalkeeperSaves")]
        public void Maps_renamed_labels_onto_the_names_the_model_uses(string row, string expectedKey)
        {
            var cell = Assert.Single(StatRowParser.ParseRow(row));

            Assert.Equal(expectedKey, cell.Key);
        }

        [Fact]
        public void Splits_a_pass_row_into_attempted_and_completed()
        {
            // "81% (336/414) Passes 73% (221/301)"
            var cells = StatRowParser.ParseRow("81%,(336/414),Passes,73%,(221/301)").ToList();

            Assert.Equal(2, cells.Count);

            var attempted = cells.Single(cell => cell.Key == "TotalPasses");
            Assert.Equal("414", attempted.Home);
            Assert.Equal("301", attempted.Guest);

            var completed = cells.Single(cell => cell.Key == "CompletedPasses");
            Assert.Equal("336", completed.Home);
            Assert.Equal("221", completed.Guest);
        }

        [Fact]
        public void Takes_the_attempted_count_from_other_fraction_rows()
        {
            // "17% (1/6) Tackles 50% (6/12)" — one tackle won of six made.
            var cell = Assert.Single(StatRowParser.ParseRow("17%,(1/6),Tackles,50%,(6/12)"));

            Assert.Equal("Tackles", cell.Key);
            Assert.Equal("6", cell.Home);
            Assert.Equal("12", cell.Guest);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("FINISHED")]
        [InlineData("Ball possession")]
        [InlineData("58,42")]
        [InlineData("58,,42")]
        public void Ignores_a_row_that_is_not_a_statistic(string row)
        {
            Assert.Empty(StatRowParser.ParseRow(row));
        }

        [Fact]
        public void Keeps_the_first_value_when_the_source_repeats_a_row()
        {
            // The page prints a headline block above the full table, so the
            // same label arrives twice.
            var (home, guest) = StatRowParser.ToColumns(new[]
            {
                "0.89,Expected goals (xG),1.25",
                "0.89,Expected goals (xG),1.25",
            });

            Assert.Equal("0.89", Assert.Single(home).Value);
            Assert.Equal("1.25", Assert.Single(guest).Value);
        }

        [Fact]
        public void Survives_a_null_set_of_rows()
        {
            var (home, guest) = StatRowParser.ToColumns(null);

            Assert.Empty(home);
            Assert.Empty(guest);
        }

        [Fact]
        public void Parsed_columns_deserialise_into_the_typed_model()
        {
            // Zaglebie v Slask Wroclaw, 15 August 2026, full match.
            var rows = new[]
            {
                "0.89,Expected goals (xG),1.25",
                "58%,Ball possession,42%",
                "12,Total shots,12",
                "5,Shots on target,2",
                "3,Shots off target,6",
                "4,Blocked shots,4",
                "8,Corner kicks,0",
                "1,Offsides,0",
                "10,Free kicks,13",
                "81%,(336/414),Passes,73%,(221/301)",
                "26,Throw ins,16",
                "13,Fouls,10",
                "2,Yellow cards,2",
                "1,Goalkeeper saves,3",
                "35,Duels won,49",
            };

            var (home, guest) = StatRowParser.ToColumns(rows);
            var homeStats = JsonConvert.DeserializeObject<Stats>(JsonConvert.SerializeObject(home));
            var guestStats = JsonConvert.DeserializeObject<Stats>(JsonConvert.SerializeObject(guest));

            Assert.Equal(0.89f, homeStats.ExpectedGoals, 2);
            Assert.Equal(58, homeStats.BallPossession);
            Assert.Equal(12, homeStats.GoalAttempts);
            Assert.Equal(5, homeStats.ShotsOnGoal);
            Assert.Equal(3, homeStats.ShotsOffGoal);
            Assert.Equal(414, homeStats.TotalPasses);
            Assert.Equal(336, homeStats.CompletedPasses);
            Assert.Equal(26, homeStats.ThrowIn);

            Assert.Equal(42, guestStats.BallPossession);
            Assert.Equal(2, guestStats.ShotsOnGoal);
            Assert.Equal(301, guestStats.TotalPasses);
            Assert.Equal(3, guestStats.GoalkeeperSaves);

            // The source stopped publishing attacks in 2026; the field stays at
            // its default rather than being invented.
            Assert.Equal(0, homeStats.Attacks);
            Assert.Equal(0, homeStats.DangerousAttacks);
        }

        [Fact]
        public void Reads_a_formation_row_the_same_way_as_a_statistic()
        {
            // The lineups page uses the same three-cell shape.
            var (home, guest) = StatRowParser.ToColumns(new[] { "3 - 5 - 2,FORMATION,5 - 3 - 2" });

            var homeTeam = JsonConvert.DeserializeObject<Team>(JsonConvert.SerializeObject(home));
            var guestTeam = JsonConvert.DeserializeObject<Team>(JsonConvert.SerializeObject(guest));

            Assert.Equal("3 - 5 - 2", homeTeam.Formation);
            Assert.Equal("5 - 3 - 2", guestTeam.Formation);
        }
    }
}
