using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestStorage : List<object>, IStorage
    {
        public Task SaveChanges() => Task.CompletedTask;

        public Task<IEnumerable<T>> GetAll<T>() where T:BaseEntity => Task.FromResult(this.OfType<T>());

        public void Track(ITrackable trackable, bool isReadonly)
        {
        }

        public event Action<Exception> OnError;
    }
}