using System;
using System.Linq;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Storage;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryJobQueue
    {
        private readonly ObjectRepositoryBase _storage;
        public ObjectRepositoryJobQueue(ObjectRepositoryBase storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        [NotNull]
        public IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken)
        {
            if (queues == null) throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0) throw new ArgumentException("Queue array must be non-empty.", nameof(queues));

            JobQueueModel fetchedJob = null;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                fetchedJob = _storage.Set<JobQueueModel>()
                        .Where(s => s.FetchedAt == null || s.FetchedAt < DateTime.UtcNow)
                        .FirstOrDefault(s => queues.Contains(s.Queue));

                if (fetchedJob != null)
                {
                    fetchedJob.FetchedAt = DateTime.UtcNow;
                }
                
                if (fetchedJob == null)
                {
                    cancellationToken.WaitHandle.WaitOne(ObjectRepositoryExtensions.QueuePollInterval);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            } while (fetchedJob == null);

            return new ObjectRepositoryFetchedJob(
                _storage,
                fetchedJob.Id,                
                fetchedJob.JobId,
                fetchedJob.Queue);
        }

        public void Enqueue(string queue, Guid jobId)
        {
            _storage.Add(new JobQueueModel
            {
                JobId = jobId,
                Queue = queue
            });
        }
    }
}