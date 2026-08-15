using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Web.Domain.Entities
{
    public abstract class BaseEntity
    {
        [BsonId]
        [JsonProperty(PropertyName = "_id")]
        public virtual string Id { get; set; }
    }
}
