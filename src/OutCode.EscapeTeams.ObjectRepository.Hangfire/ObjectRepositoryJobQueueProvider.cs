using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryJobQueueProvider
    {
        private readonly ObjectRepositoryJobQueueMonitoringApi _monitoringApi;

        public ObjectRepositoryJobQueueProvider(ObjectRepositoryBase storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            _monitoringApi = new ObjectRepositoryJobQueueMonitoringApi(storage);
        }

        public ObjectRepositoryJobQueueMonitoringApi GetJobQueueMonitoringApi() => _monitoringApi;
    }
}