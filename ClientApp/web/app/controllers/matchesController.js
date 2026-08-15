const calcCore = require('../../../calc_core');

module.exports = {
    /**
     * Controller Function to list all matches
     * @param {*} req 
     * @param {*} res 
     */
    async listAllMatches(req, res) {
        const collections = await calcCore.matches.listMatchCollections();
        const collection = req.query.collection;
        const matches = await calcCore.matches.getAllMatchesList(collection);

        res.render('pages/matches/listAll', {
            title: 'Matches',
            matches,
            collections,
            collection
        });

    },

    /**
     * Controller function to show one specific match, defined by id in params
     * @param {*} req 
     * @param {*} res 
     */
    async showSingleMatchPage(req, res) {
        const matchId = req.params.id;
        const match = await calcCore.matches.getMatchById(matchId, req.query.collection);

        if (!match) {
            return res.status(404).render('pages/matches/listAll', {
                title: 'Match not found',
                matches: [],
                collections: await calcCore.matches.listMatchCollections(),
                collection: req.query.collection
            });
        }

        res.render('pages/matches/matchPage', {
            title: match.Title,
            match
        });
    }
}