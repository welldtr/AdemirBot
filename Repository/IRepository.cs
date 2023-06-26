using DiscordBot.Domain.Entities;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace DiscordBot.Repository
{
    public interface IRepository<T> where T : IEntity
    {
        Task AddAsync(T entity);
        Task DeleteAsync(object id);
        Task DeleteAsync(T entity);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> GetByIdAsync(object id);
        Task UpsertAsync(T entity);
        Task<T> FindOneAsync(Expression<Func<T, bool>> filter);
        Task<long> Count(Expression<Func<T, bool>> filter);
        Task<DeleteResult> DeleteAsync(Expression<Func<T, bool>> filter);
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter);
    }
}