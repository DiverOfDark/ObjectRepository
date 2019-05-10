using System;
using System.Linq;
using System.Threading;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.Server;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;
#pragma warning disable 618

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ExpirationManager : IServerComponent
    {
        private static readonly ILog Logger = LogProvider.For<ExpirationManager>();

        private readonly ObjectRepositoryStorage _storage;

        public ExpirationManager(ObjectRepositoryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            Logger.Debug($"Removing outdated records...");

            _storage.ObjectRepository.Remove<JobModel>(v => v.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<StateModel>(v => _storage.ObjectRepository.Set<JobModel>().Find(v.JobId) == null);
            _storage.ObjectRepository.Remove<JobParameterModel>(v => _storage.ObjectRepository.Set<JobModel>().Find(v.JobId) == null);

            _storage.ObjectRepository.Remove<JobQueueModel>(v =>
                _storage.ObjectRepository.Set<JobModel>().Find(v.JobId) == null);
            
            _storage.ObjectRepository.Remove<CounterModel>(s=>s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<ListModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<SetModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<HashModel>(s => s.ExpireAt < DateTime.UtcNow);
            
            // Hangfire does clever job on storing retries in totally strange way.
            _storage.ObjectRepository.Remove<SetModel>(s =>
                s.Key == "retries" && Guid.TryParse(s.Value, out var jobId) &&
                _storage.ObjectRepository.Set<JobModel>().Find(jobId) == null);
            
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
        }
    }
}
