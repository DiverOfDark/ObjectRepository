using System;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.Storage;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryFetchedJob : IFetchedJob
    {
        private readonly ObjectRepositoryBase _storage;       

        public ObjectRepositoryFetchedJob(
            [NotNull] ObjectRepositoryBase storage,
            Guid id,           
            Guid jobId,
            string queue)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));

            Id = id;
            JobId = jobId.ToString();
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public Guid Id { get; }
        public string JobId { get; }
        public string Queue { get; }

        public void RemoveFromQueue()
        {
            _storage.Remove<JobQueueModel>(s => s.Id == Id);
        }

        public void Requeue()
        {
            _storage.Set<JobQueueModel>().First(s => s.Id == Id).FetchedAt = null;
        }

        public void Dispose()
        {
            
        }
    }
}
