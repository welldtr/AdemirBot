using DiscordBot.Entities;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;

namespace DiscordBot.Repository
{
    public class MongoRepository<T> : IRepository<T> where T : IEntity
    {
        private readonly IMongoCollection<T> _collection;

        private PropertyInfo GetIdProperty()
        {
            var classMap = BsonClassMap.LookupClassMap(typeof(T));
            var idMemberMap = classMap.IdMemberMap;
            var idProperty = idMemberMap?.MemberInfo as PropertyInfo;
            if (idProperty == null)
            {
                throw new InvalidOperationException($"Class {typeof(T).Name} does not have a property with the BsonId attribute.");
            }
            return idProperty;
        }

        public MongoRepository(IMongoCollection<T> collection)
        {
            _collection = collection;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        public async Task<T> GetByIdAsync(object id)
        {
            var idProperty = GetIdProperty();
            var filter = Builders<T>.Filter.Eq(idProperty.Name, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<T> FindOneAsync(Expression<Func<T, bool>> filter)
        {
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<DeleteResult> DeleteAsync(Expression<Func<T, bool>> filter)
        {
            return await _collection.DeleteManyAsync(filter);
        }

        public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter)
        {
            return _collection.Find(filter);
        }

        public async Task<long> Count(Expression<Func<T, bool>> filter)
        {
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task AddAsync(T entity)
        {
            await _collection.InsertOneAsync(entity);
        }

        public async Task DeleteAsync(T entity)
        {
            var idProperty = GetIdProperty();
            var filter = Builders<T>.Filter.Eq(idProperty.Name, idProperty.GetValue(entity));
            await _collection.DeleteOneAsync(filter);
        }

        public async Task DeleteAsync(object id)
        {
            var idProperty = GetIdProperty();
            var filter = Builders<T>.Filter.Eq(idProperty.Name, id);
            await _collection.DeleteOneAsync(filter);
        }

        public async Task UpsertAsync(T entity)
        {
            var idProperty = GetIdProperty();
            var filter = Builders<T>.Filter.Eq(idProperty.Name, idProperty.GetValue(entity));
            await _collection.ReplaceOneAsync(filter, entity, new ReplaceOptions { IsUpsert = true });
        }
    }
}
