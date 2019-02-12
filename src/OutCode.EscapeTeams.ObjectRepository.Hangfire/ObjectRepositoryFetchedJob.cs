using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.Storage;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryFetchedJob : IFetchedJob
    {
        private readonly ConcurrentList<JobQueueModel> _jobsTakenOut;
        private readonly ObjectRepositoryBase _storage;
        private readonly JobQueueModel _job;

        public ObjectRepositoryFetchedJob(ConcurrentList<JobQueueModel> jobsTakenOut,
            [NotNull] ObjectRepositoryBase storage,
            JobQueueModel job)
        {
            _storage = storage;
            _jobsTakenOut = jobsTakenOut;
            _job = job;

            jobsTakenOut.Add(job);
        }

        public Guid Id => _job.Id;
        public string JobId => _job.JobId.ToString();
        public string Queue => _job.Queue;

        public void RemoveFromQueue()
        {
            _jobsTakenOut.Remove(_job);
            _storage.Remove<JobQueueModel>(s => s.Id == Id);
        }

        public void Requeue()
        {
            _jobsTakenOut.Remove(_job);
            _storage.Set<JobQueueModel>().First(s => s.Id == Id).FetchedAt = null;
        }

        public void Dispose()
        {
            _jobsTakenOut.Remove(_job);
        }
    }
}
