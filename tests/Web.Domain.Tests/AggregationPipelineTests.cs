using System.Text.RegularExpressions;
using Xunit;

namespace Web.Domain.Tests
{
    /// <summary>
    /// The aggregation pipelines in <c>build/</c> are the deliverable: they turn
    /// per-match documents into the per-team season table. They are pasted into
    /// mongosh or Compass rather than compiled, so the two things that decide
    /// whether their output is trustworthy are checked here as text.
    /// </summary>
    public class AggregationPipelineTests
    {
        private static string Pipeline(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "build", fileName);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException($"Could not find build/{fileName} above {AppContext.BaseDirectory}");
        }

        /// <summary>The text of the <c>$group</c> stage, where the team a row belongs to is decided.</summary>
        private static string GroupStage(string pipeline)
        {
            var start = pipeline.IndexOf("$group:", StringComparison.Ordinal);
            Assert.True(start >= 0, "pipeline has no $group stage");

            return Balanced(pipeline, pipeline.IndexOf('{', start), '{', '}');
        }

        private static string Balanced(string text, int openIndex, char open, char close)
        {
            var depth = 0;

            for (var i = openIndex; i < text.Length; i++)
            {
                if (text[i] == open) depth++;
                else if (text[i] == close && --depth == 0) return text.Substring(openIndex, i - openIndex + 1);
            }

            throw new InvalidOperationException("unbalanced pipeline text");
        }

        /// <summary>
        /// A pipeline that groups by one team's name and accumulates the other
        /// team's statistics publishes the wrong side's figures under the wrong
        /// name, and nothing downstream can tell.
        /// </summary>
        [Theory]
        [InlineData("aggregation_home.js", "THome", "TGuest")]
        [InlineData("aggregation_guest.js", "TGuest", "THome")]
        [InlineData("aggregation_firsthalf.js", "THome", "TGuest")]
        public void Accumulates_the_statistics_of_the_team_it_groups_by(string fileName, string side, string otherSide)
        {
            var group = GroupStage(Pipeline(fileName));

            Assert.Contains($"_id: \"${side}.Name\"", group);
            Assert.DoesNotContain($"${otherSide}.", group);
            Assert.True(Regex.Matches(group, $@"\${side}\.").Count > 30, $"expected the accumulators to read {side}");
        }
    }
}
