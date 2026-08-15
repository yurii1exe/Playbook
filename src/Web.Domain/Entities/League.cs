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
