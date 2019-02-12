using System;
using System.Collections.Generic;
using System.Linq;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryJobQueueMonitoringApi
    {
        private readonly ObjectRepositoryBase _storage;

        public ObjectRepositoryJobQueueMonitoringApi(ObjectRepositoryBase storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IEnumerable<string> GetQueues()
        {
            return _storage.Set<JobQueueModel>().Select(v => v.Queue).Distinct().ToList();
        }

        public IEnumerable<Guid> GetEnqueuedJobIds(string queue, int @from, int perPage)
        {
            return _storage.Set<JobQueueModel>().Where(v => v.Queue == queue)
                .Skip(from).Take(perPage).Select(s => s.JobId).ToList();
        }

        public IEnumerable<Guid> GetFetchedJobIds(string queue, int @from, int perPage)
        {
            return _storage.Set<JobQueueModel>()
                .Where(s => s.Queue == queue && s.FetchedAt.HasValue)
                .Skip(from)
                .Take(perPage)
                .Select(v => v.JobId)
                .ToList();
        }

        public EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue)
        {
            return _storage.Set<JobQueueModel>().Where(v => v.Queue == queue).Aggregate(
                new EnqueuedAndFetchedCountDto(), (a, b) =>
                {
                    a.EnqueuedCount += b.FetchedAt == null ? 1 : 0;
                    a.FetchedCount += b.FetchedAt != null ? 1 : 0;
                    return a;
                });
        }
    }
}