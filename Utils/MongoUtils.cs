using DiscordBot.Domain.Entities;
using DiscordBot.Repository;
using MongoDB.Driver;

namespace DiscordBot.Utils
{
    public static class MongoUtils
    {
        public static MongoRepository<T> GetRepository<T>(this IMongoDatabase db, string name) where T: IEntity
        {
            return new MongoRepository<T>(db.GetCollection<T>(name));
        }
    }
}
