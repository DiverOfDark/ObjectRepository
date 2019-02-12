using System.Collections.Generic;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    public class ObjectRepositoryStorage : JobStorage
    {
        internal ObjectRepositoryJobQueueMonitoringApi MonitoringApi { get; set; }
        internal ObjectRepositoryBase ObjectRepository { get; }

        public ObjectRepositoryStorage(ObjectRepositoryBase objectRepository)
        {
            ObjectRepository = objectRepository;
            MonitoringApi = new ObjectRepositoryJobQueueMonitoringApi(objectRepository);
        }

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
        }
    }
}