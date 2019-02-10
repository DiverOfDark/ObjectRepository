using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryJobQueueProvider
    {
        private readonly ObjectRepositoryJobQueue _jobQueue;
        private readonly ObjectRepositoryJobQueueMonitoringApi _monitoringApi;

        public ObjectRepositoryJobQueueProvider(ObjectRepositoryBase storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            _jobQueue = new ObjectRepositoryJobQueue(storage);
            _monitoringApi = new ObjectRepositoryJobQueueMonitoringApi(storage);
        }

        public ObjectRepositoryJobQueue GetJobQueue() => _jobQueue;

        public ObjectRepositoryJobQueueMonitoringApi GetJobQueueMonitoringApi() => _monitoringApi;
    }
}