# Playbook

A .NET data pipeline that pulls semi-structured data from a source it does not
control, parses it defensively, normalises it into a typed model, writes it
idempotently to MongoDB, and aggregates the result into something you can make a
decision from.

That is the same shape as a freight EDI or TMS integration, with the nouns
changed. A partner endpoint you have no authority over. A format that changes
without notice and without a version bump. Parsing that has to degrade section by
section instead of failing the whole document. Writes that must survive being run
twice. A retry budget so a bad day upstream does not turn into a hammering. And
an aggregate at the end that is the actual product — the ingestion exists to feed
it.

Here the source happens to be a JavaScript-rendered football results site, which
is a convenient stand-in: it is public, it genuinely changes its markup between
seasons, and nobody had to sign an NDA for me to show you the code.

![The worker seeding leagues, driving Chrome and writing 11 matches](docs/playbook-demo.gif)

*A real run against the live source on 16 August 2026. The worker seeds 16
leagues from `Data/leagues.json` into an empty MongoDB, MLS and season 2026 are
typed at the prompt, Chrome is driven through the league page, and 30 matches
are found on it. The 19 fixtures that have not been played yet are rejected
from their header alone — those are the yellow lines — and the 11 finished
matches are parsed and written to `us_mls_2026`. Six minutes of real running
time, compressed to 25 seconds: the opening plays at 4x and the parse loop at
30x.*

## The problem

Match statistics are published as rendered HTML, not as an API. The pages load
their content client-side, so an HTTP request returns a shell with no data in it.
A full season sits behind a "show more" button that has to be clicked until it
disappears. Class names change between seasons. Finished, scheduled and abandoned
matches look nearly identical in the markup, and only one of the three is worth
storing.

So there is no way to ask a question like *"across a full season, how do a
team's first-half shots on target and expected goals turn into first-half
goals?"* without first building the dataset yourself.

## What I built

A .NET background worker that drives a real Chrome instance through
PuppeteerSharp.

- **Seeded from configuration, not hardcoded.** 16 leagues across 14 countries in
  `Data/leagues.json`, loaded into MongoDB on first run. The leagues to parse and
  the season are chosen at startup.
- **Pagination is driven, not guessed.** The worker clicks the "show more"
  control until it is gone, then collects every match link on the page.
- **Only finished matches are stored.** The match header is read before anything
  else and decides twice over: an unplayed fixture publishes fewer than the six
  fields a played one does and is rejected on the header's shape, and a header
  that has the six has to say FINISHED. Either way a scheduled or abandoned
  fixture costs one page load instead of five.
- **Each match is parsed in four passes** — header, goal incidents by half,
  statistics, lineups — across the sub-pages the site splits them over. The
  statistics pass captures the 16 statistics the source publishes per team
  (possession, expected goals, goal attempts, shots on and off target, blocked
  shots, passes attempted and completed, corners, offsides, throw-ins, free
  kicks, fouls, yellow cards, tackles, goalkeeper saves), for the full match and
  for each half separately: **96 data points per match** — 16 statistics ×
  2 teams × 3 periods.
- **Every pass fails independently.** A section that cannot be read is logged and
  skipped, so a changed class name costs the lineups rather than the whole match.
  The selectors themselves are chains, which is the part that decides whether a
  scraper survives to a second season — see below.
- **Writes are idempotent.** Every match is checked for existence before insert,
  so an interrupted run is resumed by re-running it. Failures count against a
  budget of five, after which the process stops rather than hammering the source.
- **Storage is partitioned by league and season** — one collection per
  `{country}_{league}_{season}` — so a season can be re-parsed or dropped without
  touching the rest.

Then a set of MongoDB aggregation pipelines roll per-match documents up into
per-team season figures: **34 totals and averages**, and **18 derived ratios** on
top of them — shots on target per goal attempt, first-half goals per shot on
target, minutes of a half per goal attempt, and so on.

| Pipeline | Groups by | Stats period |
|---|---|---|
| `build/aggregation_firsthalf.js` | home team | first half |
| `build/aggregation_home.js` | home team | full match |
| `build/aggregation_guest.js` | away team | full match |

A small Express + Pug application reads the same database and renders the leagues
and matches for browsing.

## Selectors that outlive the markup

Every selector the scraper uses is a chain, tried in order, and the chain is what
carries a run across a change of markup generation. Counting what each link of
two of those chains matched on the live source on 16 August 2026:

| Looking for | Selector | Elements matched |
|---|---|---|
| statistics rows | `[data-testid='wcl-statistics']` | **41** |
| | `[data-testid='stat__row']` | 0 |
| | `div.stat__row` | 0 |
| match rows | `[data-testid='event__match']` | 0 |
| | `div.event__match` | **109** |

The two selectors carrying the site sit at opposite ends of their chains. The
statistics table is on the newest markup; the match list is still on the oldest,
the one that predates the `data-testid` attributes entirely. A scraper pinned to
either generation alone comes back empty from one of the two pages, which is why
every selector here keeps its predecessors instead of being replaced by the one
that works today. What the chain buys is tolerance of renamed markup rather than
immunity to it: a redesign that moves a section somewhere else on the page is
answered by a new selector at the front of its chain.

## How it fits together

```mermaid
flowchart TD
    CFG["appsettings.json<br/>Data/leagues.json"] --> W["Worker<br/>(BackgroundService)"]
    W --> LS[LeagueService]
    LS --> MDB1[("MongoDB: Leagues")]
    W --> MSS[MatchScrapingService]
    MSS --> PS[PuppeteerSharp] --> CH[Chrome] --> SRC[["Results site"]]
    SRC -. "4 passes per match" .-> MSS
    MSS --> MS["MongoService&lt;T&gt;"]
    MS --> MDB2[("MongoDB:<br/>{country}_{league}_{season}")]
    MDB2 --> AGG[Aggregation pipelines]
    MDB2 --> UI[Express + Pug reader]
```

## Result

Runs unattended for any of the 16 configured leagues, resumable after
interruption, and produces a dataset that answers questions the source site
cannot be asked directly: one row per team per season, carrying its first-half
shots on target, expected goals and goals alongside the ratios between them. The
aggregation output is the actual deliverable.

## Stack

.NET 8 · PuppeteerSharp · MongoDB (driver + aggregation framework) ·
`Microsoft.Extensions.Hosting` background service with DI and structured logging ·
Node.js · Express · Pug

## Running it

Requires the .NET 8 SDK, a local Chrome, and MongoDB on `localhost:27017`.

```bash
cd src/API.Scrapping
dotnet run
```

Run it from the project directory: `appsettings.json` is read relative to the
working directory.

On first run the leagues are seeded from `Data/leagues.json`. The console asks
which leagues and which season to parse.

Configuration lives in `src/API.Scrapping/appsettings.json`:

| Setting | Meaning |
|---|---|
| `BrowserPath` / `BrowserPathMac` | Path to a Chrome executable; the platform is detected at runtime |
| `HeadlessBrowser` | Run Chrome without a window; ships `false`, so the browser is visible while it works |
| `OpenPageDelay` / `WaitForLoad` | Politeness delays, in milliseconds |
| `YearToParse` | Season, e.g. `2022-2023`; blank prompts at startup |
| `PlaybookDatabase` | MongoDB connection string and database name |

The reader UI:

```bash
cd ClientApp
npm install
npm run run-server     # http://localhost:8000
```

The match pages read one league-season collection at a time. Pick it with
`?collection=gb-eng_premier_league_2022-2023`, or set `matchesCollection` in
`ClientApp/config/index.js`; with neither set it falls back to whichever
collection the scraper populated first.

## Operating notes

- **Tested without a browser.** 41 tests cover the row parser against rows
  captured from real match pages, the collection naming, and the grouping and
  divisors of the aggregation pipelines. Pagination, sub-page navigation and the
  four passes per match are exercised by running the worker.
- **Source drift.** The statistics table was redesigned in 2026; the parser maps
  the 16 statistics now published. Attacks and dangerous attacks are no longer
  published, so the nine first-half ratios built on them return null and the rest
  of the table is computed from what arrives.
- **The season chosen at startup names the collection, not the URL.** A run
  parses whatever the league's own page currently lists — recent results and
  upcoming fixtures — and stores the finished ones under
  `{country}_{league}_{season}`. Parsing a past season means pointing
  `flashscoreLink` at that season's results page.
- **The reader UI lists and renders.** It lists the leagues, lists the matches in
  a league-season collection and renders a single match, on Bootstrap's
  stylesheet — `main.css` is a placeholder. The aggregation pipelines are run
  against MongoDB directly.
- **`MongoService<T>` holds one collection handle at a time.** `SetCollection`
  assigns it on the shared instance, which is what the worker's single-threaded
  run needs. Parsing two leagues concurrently takes a per-call
  `IMongoCollection<T>` handle instead.

## Note

Built to answer questions about publicly published match results. Delays between
requests are configurable and set conservatively by default. Check the terms of
any site before pointing it at one.
