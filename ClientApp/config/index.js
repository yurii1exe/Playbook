module.exports = {
    vars: {
        port: 8000,
        env: 'development',

        // Which league-season collection the match pages read, e.g.
        // 'gb-eng_premier_league_2022-2023'. Leave null to use whichever
        // collection the scraper populated first.
        matchesCollection: null
    },

    mongoose: {
        url: 'mongodb://127.0.0.1:27017/PlaybookDB?authSource=admin'
    }
}