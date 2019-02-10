using System.Collections.Generic;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    public class ObjectRepositoryStorage : JobStorage
    {
        internal ObjectRepositoryBase ObjectRepository { get; }

        public ObjectRepositoryStorage(ObjectRepositoryBase objectRepository)
        {
            ObjectRepository = objectRepository;
            var defaultQueueProviders = new ObjectRepositoryJobQueueProvider(ObjectRepository);
            QueueProviders = new PersistentJobQueueProviderCollection(defaultQueueProviders);
        }

        internal PersistentJobQueueProviderCollection QueueProviders { get; }
        
        public override IMonitoringApi GetMonitoringApi()
        {
            return new ObjectRepositoryMonitoringApi(this);
        }

        public override IStorageConnection GetConnection()
        {
            return new ObjectRepositoryConnection(this);
        }

        public override IEnumerable<IServerComponent> GetComponents()
        {
            yield return new ExpirationManager(this);
            yield return new CountersAggregator(this);
        }
    }
}