using PuppeteerSharp;
using Web.Domain.Extentions;

namespace Web.Domain.Entities
{
    public class Team : TeamBase
    {
        public int GoalsPerFirst { get; set; }
        public int GoalsPerSecond { get; set; }
        public string Formation { get; set; }
        public Stats Stats0 { get; set; }
        public Stats Stats1 { get; set; }
        public Stats Stats2 { get; set; }

        public async Task<Team> ConfigTeam(IElementHandle element)
        {
            var participantHomeId = (await element.GetPropertyAsync("href")).Convert().Split('/');

            return new Team
            {
                Id = participantHomeId[5],
                Name = participantHomeId[4]
            };
        }
    }
}