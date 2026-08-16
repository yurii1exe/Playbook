const mongoose = require('mongoose');
const Schema = mongoose.Schema;

const LeaguesSchema = new Schema ({
    name: { type: String },
    country: {
        name: { type: String },
        code: { type: String }
    },
    flashscoreLink: { type: String },
    footystatsLink: { type: String },
    tdslLink: { type: String },
    whoScoredLink: { type: String }
})

module.exports = mongoose.model('League', LeaguesSchema, 'Leagues');