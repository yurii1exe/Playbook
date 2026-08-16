using System.Text.RegularExpressions;

namespace Web.Domain.Extentions
{
    /// <summary>
    /// Turns the text of one statistics row into home/guest values keyed by a
    /// stable name.
    /// </summary>
    /// <remarks>
    /// The source publishes a statistics row as three lines — home value, label,
    /// guest value — which arrive here as a single comma-joined string (see
    /// <see cref="Common.Convert"/>). Two things about that shape are outside our
    /// control and both have already changed once:
    /// <list type="bullet">
    /// <item>the label wording ("Shots on goal" became "Shots on target"), which
    /// is why labels are mapped through <see cref="Aliases"/> onto the names the
    /// domain model uses rather than being trusted verbatim;</item>
    /// <item>the row shape: pass and tackle rows now carry a percentage and a
    /// "(completed/attempted)" fraction on each side, so a row can have five
    /// cells instead of three.</item>
    /// </list>
    /// Keeping this as a pure string-to-values function is deliberate: it is the
    /// part of the scraper most likely to break when the source changes, and it
    /// is the part that can be tested without a browser.
    /// </remarks>
    public static class StatRowParser
    {
        private static readonly Regex Fraction = new(@"^\((\d+)\s*/\s*(\d+)\)$", RegexOptions.Compiled);

        /// <summary>
        /// Source label (spaces removed) mapped onto the property name used by
        /// <c>Stats</c>. Anything not listed here is passed through unchanged and
        /// matched case-insensitively during deserialisation.
        /// </summary>
        private static readonly Dictionary<string, string> Aliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Totalshots"] = "GoalAttempts",
                ["Goalattempts"] = "GoalAttempts",
                ["Shotsontarget"] = "ShotsOnGoal",
                ["Shotsongoal"] = "ShotsOnGoal",
                ["Shotsofftarget"] = "ShotsOffGoal",
                ["Shotsoffgoal"] = "ShotsOffGoal",
                ["Throwins"] = "Throw-in",
                ["Throw-in"] = "Throw-in",
                ["Goalkeepersaves"] = "GoalkeeperSaves",
            };

        /// <summary>
        /// Reduces a set of row texts to the home and guest columns. First
        /// occurrence of a key wins, because the source repeats the headline
        /// rows above the full table.
        /// </summary>
        public static (Dictionary<string, string> Home, Dictionary<string, string> Guest) ToColumns(
            IEnumerable<string> rowTexts)
        {
            var home = new Dictionary<string, string>();
            var guest = new Dictionary<string, string>();

            if (rowTexts == null)
            {
                return (home, guest);
            }

            foreach (var rowText in rowTexts)
            {
                foreach (var cell in ParseRow(rowText))
                {
                    if (!home.ContainsKey(cell.Key))
                    {
                        home.Add(cell.Key, cell.Home);
                    }

                    if (!guest.ContainsKey(cell.Key))
                    {
                        guest.Add(cell.Key, cell.Guest);
                    }
                }
            }

            return (home, guest);
        }

        /// <summary>
        /// Parses one row. Returns nothing for a row that does not carry a
        /// label and two values; returns two entries for a pass row, which
        /// yields both an attempted and a completed count.
        /// </summary>
        public static IEnumerable<StatCell> ParseRow(string rowText)
        {
            if (string.IsNullOrWhiteSpace(rowText) || !rowText.Contains(','))
            {
                yield break;
            }

            var cells = rowText
                .Replace("%", string.Empty)
                .Split(',')
                .Select(cell => cell.Trim())
                .ToArray();

            // "81 | (336/414) | Passes | 73 | (221/301)" — a percentage and a
            // completed/attempted fraction on each side of the label.
            if (cells.Length >= 5 && Fraction.IsMatch(cells[1]) && Fraction.IsMatch(cells[^1]))
            {
                var label = Normalise(cells[2]);
                var homeFraction = Fraction.Match(cells[1]);
                var guestFraction = Fraction.Match(cells[^1]);

                if (string.Equals(label, "Passes", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new StatCell("TotalPasses", homeFraction.Groups[2].Value, guestFraction.Groups[2].Value);
                    yield return new StatCell("CompletedPasses", homeFraction.Groups[1].Value, guestFraction.Groups[1].Value);
                    yield break;
                }

                // For every other fraction row the attempted count is the one
                // that matches what the label used to mean when the source
                // published it as a plain number (tackles made, crosses put in).
                yield return new StatCell(label, homeFraction.Groups[2].Value, guestFraction.Groups[2].Value);
                yield break;
            }

            if (cells.Length < 3)
            {
                yield break;
            }

            var key = Normalise(cells[1]);
            if (string.IsNullOrWhiteSpace(key))
            {
                yield break;
            }

            yield return new StatCell(key, cells[0], cells[2]);
        }

        /// <summary>
        /// Strips the spacing the source uses for display and maps renamed
        /// labels back onto the names the domain model knows.
        /// </summary>
        public static string Normalise(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            var compact = label.Replace(" ", string.Empty).Trim();
            return Aliases.TryGetValue(compact, out var alias) ? alias : compact;
        }
    }

    /// <summary>One statistic: its key, the home value and the guest value.</summary>
    public readonly record struct StatCell(string Key, string Home, string Guest);
}
