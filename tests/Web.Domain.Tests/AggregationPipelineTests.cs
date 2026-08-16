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

        /// <summary>The text of the <c>$project</c> stage, where the derived ratios are computed.</summary>
        private static string ProjectStage(string pipeline)
        {
            var start = pipeline.IndexOf("$project:", StringComparison.Ordinal);
            Assert.True(start >= 0, "pipeline has no $project stage");

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

        /// <summary>
        /// Statistics the source stops publishing arrive as zero, and MongoDB
        /// aborts the whole aggregation on a division by zero. Every divisor in
        /// the derived ratios therefore has to be a constant or guarded.
        /// </summary>
        [Fact]
        public void Guards_every_divisor_in_the_derived_ratios_against_zero()
        {
            var project = ProjectStage(Pipeline("aggregation_firsthalf.js"));

            var divisors = Divisors(project).ToList();

            Assert.NotEmpty(divisors);
            foreach (var divisor in divisors)
            {
                var guarded = divisor.Contains("$cond")
                    || divisor.Contains("OrNull")
                    || double.TryParse(divisor, out _);

                Assert.True(guarded, $"unguarded divisor: {divisor}");
            }
        }

        /// <summary>
        /// Attacks and dangerous attacks are no longer published, so every
        /// ratio built on them divides by zero on a dataset collected now.
        /// These are the nine, named, so that a future edit cannot quietly put
        /// a raw field back.
        /// </summary>
        [Theory]
        [InlineData("converted_Dangerous", "$attacksTotalOrNull")]
        [InlineData("goalAtt_DangAtt", "$dangerousAttacksAvgOrNull")]
        [InlineData("shotongoal_dangatt", "$dangerousAttacksAvgOrNull")]
        [InlineData("min45_Dangatt", "$dangerousAttacksAvgOrNull")]
        [InlineData("failedAttEqatt_Dangatt", "$dangerousAttacksAvgOrNull")]
        [InlineData("passes_attacks", "$attacksAvgOrNull")]
        [InlineData("possesion_att", "$attacksAvgOrNull")]
        [InlineData("passes_attacks1", "$attacksAvgOrNull")]
        [InlineData("passes_attacks2", "$attacksAvgOrNull")]
        public void Divides_the_attack_ratios_by_a_guarded_denominator(string ratio, string expectedDivisor)
        {
            var project = ProjectStage(Pipeline("aggregation_firsthalf.js"));

            Assert.Equal(expectedDivisor, DivisorOf(project, ratio));
        }

        /// <summary>
        /// The guard maps a zero denominator to null, which is what makes the
        /// dead ratio come back empty instead of aborting the aggregation.
        /// </summary>
        [Fact]
        public void Maps_a_zero_denominator_onto_null()
        {
            var pipeline = Pipeline("aggregation_firsthalf.js");

            foreach (var denominator in new[] { "attacksTotal", "attacksAvg", "dangerousAttacksAvg" })
            {
                Assert.Contains(
                    $"$cond: [{{ $eq: [\"${denominator}\", 0] }}, null, \"${denominator}\"]",
                    Regex.Replace(pipeline, @"\s+", " "));
            }
        }

        /// <summary>Second operand of the <c>$divide</c> a named field is built from.</summary>
        private static string DivisorOf(string stage, string fieldName)
        {
            var start = stage.IndexOf(fieldName + ":", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{fieldName} is not in the stage");

            var arrayStart = stage.IndexOf('[', start);
            var array = Balanced(stage, arrayStart, '[', ']');
            var operands = TopLevelOperands(array.Substring(1, array.Length - 2)).ToList();

            Assert.Equal(2, operands.Count);
            return operands[1];
        }

        /// <summary>Second operand of every <c>$divide</c> in the given stage.</summary>
        private static IEnumerable<string> Divisors(string stage)
        {
            foreach (Match match in Regex.Matches(stage, @"\$divide:\s*"))
            {
                var arrayStart = stage.IndexOf('[', match.Index);
                var array = Balanced(stage, arrayStart, '[', ']');
                var operands = TopLevelOperands(array.Substring(1, array.Length - 2)).ToList();

                Assert.Equal(2, operands.Count);
                yield return operands[1];
            }
        }

        private static IEnumerable<string> TopLevelOperands(string arrayBody)
        {
            var depth = 0;
            var current = string.Empty;

            foreach (var character in arrayBody)
            {
                if (character is '{' or '[') depth++;
                if (character is '}' or ']') depth--;

                if (character == ',' && depth == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current)) yield return current.Trim().Trim('"');
                    current = string.Empty;
                    continue;
                }

                current += character;
            }

            if (!string.IsNullOrWhiteSpace(current)) yield return current.Trim().Trim('"');
        }
    }
}
