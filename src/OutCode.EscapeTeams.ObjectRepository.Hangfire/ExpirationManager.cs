using System;
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
            _storage.ObjectRepository.Remove<JobModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<ListModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<SetModel>(s => s.ExpireAt < DateTime.UtcNow);
            _storage.ObjectRepository.Remove<HashModel>(s => s.ExpireAt < DateTime.UtcNow);
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
        }
    }
}
