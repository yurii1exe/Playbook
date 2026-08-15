using Web.Domain.Core;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Web.Domain.Entities;
using Web.Domain.IServices;

namespace API.Scrapping.Services
{
    public class MongoService<T> : IMongoService<T> where T : BaseEntity
    {
        private readonly IMongoDatabase _db;
        private IMongoCollection<T> _mongoCollection;

        public MongoService(
            DatabaseConfiguration itemStoreDatabaseSettings,
            ILogger<MongoService<T>> logger)
        {
            var client = new MongoClient(
                itemStoreDatabaseSettings.ConnectionString);

            _db = client.GetDatabase(
                itemStoreDatabaseSettings.DatabaseName);

            // Fail fast: the driver connects lazily, so without this a bad
            // connection string would not surface until the first write —
            // potentially an hour into a scraping run.
            var databaseNames = client.ListDatabaseNames().ToList();
            logger.LogInformation(
                "Connected to MongoDB database {DatabaseName} ({DatabaseCount} databases visible)",
                itemStoreDatabaseSettings.DatabaseName,
                databaseNames.Count);
        }

        public void SetCollection(string collection)
        {
            _mongoCollection = _db.GetCollection<T>(collection);
        }

        public async Task<List<T>> GetAsync() =>
            await _mongoCollection.Find(_ => true).ToListAsync();

        public async Task<T> GetAsync(string id) =>
            await _mongoCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(T newItem) =>
            await _mongoCollection.InsertOneAsync(newItem);

        public async Task UpdateAsync(string id, T updatedItem) =>
            await _mongoCollection.ReplaceOneAsync(x => x.Id == id, updatedItem);

        public async Task RemoveAsync(string id) =>
            await _mongoCollection.DeleteOneAsync(x => x.Id == id);
    }
}
