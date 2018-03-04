using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public interface IStorage
    {
        Task SaveChanges();
        Task<IEnumerable<T>> GetAll<T>();
        void Track(ObjectRepositoryBase objectRepository, bool isReadonly);
        event Action<Exception> OnError;
    }
}