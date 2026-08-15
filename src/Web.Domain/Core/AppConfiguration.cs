using Microsoft.Extensions.Configuration;

namespace Web.Domain.Core
{
    public class AppConfiguration
    {
        public AppConfiguration(IConfiguration configuration)
        {
            configuration.Bind(this);
        }

        public string BrowserPath { get; set; }
        public string BrowserPathMac { get; set; }
        public int OpenPageDelay { get; set; }
        public int WaitForLoad { get; set; }
        public bool HeadlessBrowser { get; set; }
        public string YearToParse { get; set; }

    }
    public class DatabaseConfiguration
    {
        public DatabaseConfiguration(IConfiguration configuration)
        {
            configuration.Bind("PlaybookDatabase", this);

        }
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string LeaguesCollection { get; set; } = "Leagues";
        public string TeamsCollection { get; set; } = "Teams";

    }
}
