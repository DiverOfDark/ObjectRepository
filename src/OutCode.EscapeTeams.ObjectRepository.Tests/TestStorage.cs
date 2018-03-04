using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestStorage : IStorage
    {
        public Task SaveChanges() => Task.CompletedTask;

        public Task<IEnumerable<T>> GetAll<T>() => Task.FromResult(Enumerable.Empty<T>());

        public void Track(ObjectRepositoryBase objectRepository, bool isReadonly)
        {
        }

        public event Action<Exception> OnError;
    }
}