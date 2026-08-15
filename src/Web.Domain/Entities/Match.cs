using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PuppeteerSharp;
using Web.Domain.Core;
using Web.Domain.Extentions;
using Web.Domain.IEntities;

namespace Web.Domain.Entities
{
    public class Match : BaseEntity, IFlashscore
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public Team THome { get; set; }
        public Team TGuest { get; set; }
        public int RoundNr { get; set; }
        public List<string> Summary { get; set; } = new List<string>();


        /// <summary>
        /// Reads the outer text of every element matching <paramref name="querySelector"/>.
        /// Returns an empty list if the selector matches nothing or the read
        /// fails, so that one changed class name costs a single section rather
        /// than the whole match.
        /// </summary>
        public async Task<List<string>> PopulateData(string querySelector, IPage page2, ILogger logger = null)
        {
            try
            {
                var matchIncidents0 = await page2.QuerySelectorAllAsync(querySelector);
                var matchIncidents = new List<string>();
                
                if (matchIncidents0 == null || matchIncidents0.Length == 0)
                {
                    return matchIncidents;
                }
                
                foreach (var item in matchIncidents0)
                {
                    if (item != null)
                    {
                        var matchContent = (await item.GetPropertyAsync("outerText")).Convert();
                        if (!string.IsNullOrWhiteSpace(matchContent))
                        {
                            matchIncidents.Add(matchContent);
                        }
                    }
                }
                return matchIncidents;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to read elements for selector {QuerySelector} on match {MatchId}; continuing without this section", querySelector, Id);
                return new List<string>();
            }
        }

        /// <summary>
        /// Loads <paramref name="url"/> and deserialises the home and guest
        /// columns of the matching rows into <typeparamref name="T"/>.
        /// Returns an empty array rather than throwing, for the same reason as
        /// the overload above.
        /// </summary>
        public async Task<T[]> PopulateData<T>(string url, string querySelector, IPage page2, AppConfiguration consts, ILogger logger = null) where T : class
        {
            try
            {
                await Task.Delay(consts.OpenPageDelay);
                await page2.GoToAsync(url);
                await Task.Delay(consts.WaitForLoad);

                var matchData = await page2.QuerySelectorAllAsync(querySelector);
                
                if (matchData == null || matchData.Length == 0)
                {
                    return new T[0];
                }
                
                Dictionary<string, string> homeData = new Dictionary<string, string>();
                Dictionary<string, string> guestData = new Dictionary<string, string>();

                foreach (var item in matchData)
                {
                    if (item == null) continue;
                    
                    var matchContent = (await item.GetPropertyAsync("outerText")).Convert();
                    if (string.IsNullOrWhiteSpace(matchContent) || !matchContent.Contains(','))
                    {
                        continue;
                    }
                    
                    var rowSplit = matchContent.Replace("%", "").Split(',');
                    if (rowSplit.Length < 3)
                    {
                        continue;
                    }
                    
                    var colName = rowSplit[1].Replace(" ", "").Trim();
                    if (string.IsNullOrWhiteSpace(colName))
                    {
                        continue;
                    }

                    // Ensure we don't add duplicate keys
                    if (!homeData.ContainsKey(colName))
                    {
                        homeData.Add(colName, rowSplit[0].Trim());
                    }
                    if (!guestData.ContainsKey(colName))
                    {
                        guestData.Add(colName, rowSplit[2].Trim());
                    }
                }
                
                if (homeData.Count == 0 || guestData.Count == 0)
                {
                    return new T[0];
                }
                
                string homeDict = JsonConvert.SerializeObject(homeData);
                var homeStats = JsonConvert.DeserializeObject<T>(homeDict);

                string guestDict = JsonConvert.SerializeObject(guestData);
                var guestStats = JsonConvert.DeserializeObject<T>(guestDict);

                return new T[] { homeStats, guestStats };
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to parse {ResultType} from {Url} using selector {QuerySelector} on match {MatchId}; continuing without this section", typeof(T).Name, url, querySelector, Id);
                return Array.Empty<T>();
            }
        }
    }
}
