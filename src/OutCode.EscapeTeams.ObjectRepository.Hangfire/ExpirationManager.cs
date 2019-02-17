using System;
using System.Linq;
using System.Threading;
using Hangfire.Logging;
using Hangfire.Server;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

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

            var badJobs = _storage.ObjectRepository.Set<JobModel>().Where(v => v.ExpireAt < DateTime.UtcNow);

            var badStates = _storage.ObjectRepository.Set<StateModel>().Where(v => badJobs.Contains(v.Job)).ToList();
            var badParameters = _storage.ObjectRepository.Set<JobParameterModel>()
                .Where(v => badJobs.Any(t => t.Id == v.JobId)).ToList();
            
            _storage.ObjectRepository.RemoveRange(badJobs);
            _storage.ObjectRepository.RemoveRange(badStates);
            _storage.ObjectRepository.RemoveRange(badParameters);
            
            _storage.ObjectRepository.Remove<CounterModel>(s=>s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<ListModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<SetModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<HashModel>(s => s.ExpireAt < DateTime.UtcNow);
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
        }
    }
}
