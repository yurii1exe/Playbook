const mongoose = require('mongoose');
const config = require('../config');

// The .NET worker does not write to a single "matches" collection. It writes one
// collection per league-season, named `{countryCode}_{league}_{season}` — see
// LeagueService.GetCollectionNameAsync. Anything that is not one of the two
// reference collections below is therefore a match collection.
const RESERVED_COLLECTIONS = new Set(['Leagues', 'Teams']);

/**
 * Names of every league-season collection the scraper has populated.
 * @returns {Promise<string[]>}
 */
async function listMatchCollections() {
    const all = await mongoose.connection.db.listCollections().toArray();
    return all
        .map((c) => c.name)
        .filter((name) => !RESERVED_COLLECTIONS.has(name))
        .sort();
}

/**
 * Resolve which league-season to read: an explicit name wins, then the
 * configured default, then whichever collection the scraper wrote first.
 * @param {string} [collectionName]
 * @returns {Promise<string|null>}
 */
async function resolveCollection(collectionName) {
    if (collectionName) return collectionName;
    if (config.vars.matchesCollection) return config.vars.matchesCollection;

    const available = await listMatchCollections();
    return available[0] || null;
}

module.exports = {
    listMatchCollections,

    async getAllMatchesList(collectionName) {
        const name = await resolveCollection(collectionName);
        if (!name) return [];

        const collection = mongoose.connection.db.collection(name);
        return collection.find().toArray();
    },

    async getMatchById(id, collectionName) {
        const name = await resolveCollection(collectionName);
        if (!name) return null;

        const collection = mongoose.connection.db.collection(name);
        return collection.findOne({ _id: id });
    }
};
