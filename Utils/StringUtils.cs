using DiscordBot.Domain.Entities;
using DiscordBot.Repository;
using MongoDB.Driver;

namespace DiscordBot.Utils
{
    public class StringUtils
    {
        public static ulong[] SplitAndParseMemberIds(string memberIds)
        {
            return memberIds
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => ulong.Parse(a))
                .ToArray();
        }
    }

    public static class MongoUtils
    {
        public static MongoRepository<T> GetRepository<T>(this IMongoDatabase db, string name) where T: IEntity
        {
            return new MongoRepository<T>(db.GetCollection<T>(name));
        }
    }
}
