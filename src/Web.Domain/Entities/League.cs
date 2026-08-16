using MongoDB.Bson.Serialization.Attributes;

namespace Web.Domain.Entities
{
    public class League : BaseEntity
    {
        [BsonElement("name")]
        public string Name { get; set; }
        [BsonElement("country")]
        public CountryObj Country { get; set; }
        [BsonElement("flashscoreLink")]
        public string FlashscoreLink { get; set; }
        [BsonElement("footystatsLink")]
        public string FootystatsLink { get; set; }
        [BsonElement("tdslLink")]
        public string TdslLink { get; set; }
        [BsonElement("whoScoredLink")]
        public string WhoScoredLink { get; set; }

        public class CountryObj
        {
            [BsonElement("name")]
            public string Name { get; set; }
            [BsonElement("code")]
            public string Code { get; set; }
        }

        /// <summary>
        /// True for a competition whose season is named by a single calendar
        /// year rather than by the two years it spans.
        /// </summary>
        public bool HasSingleYearSeason =>
            string.Equals(Name, "MLS", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The season a run falls back to when no year is typed at the prompt.
        /// </summary>
        /// <remarks>
        /// The distinction is a property of the competition, not of its
        /// country: MLS runs March to December and names its season "2026",
        /// while the European leagues span two calendar years and name it
        /// "2025-2026". The season names the collection, so getting it wrong
        /// splits one season across two of them.
        /// </remarks>
        public string DefaultSeason(DateTime today) =>
            HasSingleYearSeason
                ? today.Year.ToString()
                : (today.Year - 1) + "-" + today.Year;

        /// <summary>
        /// Turns the league's source URL into the league part of a collection
        /// name, e.g. ".../football/spain/laliga/" becomes "_laliga".
        /// </summary>
        /// <remarks>
        /// Derived from the URL path rather than by trimming a fixed number of
        /// characters, so a change to the site's host or path prefix cannot
        /// silently corrupt every collection name.
        /// </remarks>
        public string GetFileName
        {
            get
            {
                // Path only: "/football/spain/laliga/" -> ["football", "spain", "laliga"]
                var segments = new Uri(FlashscoreLink).AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Where(segment => !segment.Equals("results", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                // Drop the leading sport and country segments; what remains
                // identifies the league itself.
                var leagueSegments = segments.Skip(2).ToArray();
                if (leagueSegments.Length == 0)
                {
                    leagueSegments = segments.Length > 0
                        ? new[] { segments[^1] }
                        : Array.Empty<string>();
                }

                // Leading underscore is intentional: callers concatenate this
                // directly onto the country code.
                return "_" + string.Join('_', leagueSegments).Replace('-', '_');
            }
        }
    }

}
