using System;
using Hangfire;
using Hangfire.Annotations;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    public static class ObjectRepositoryExtensions
    {
        internal const int QueuePollInterval = 1000;

        public static void RegisterHangfireScheme(this ObjectRepositoryBase objectRepository)
        {
            objectRepository.AddType((AggregatedCounterModel.AggregatedCounterEntity x) => new AggregatedCounterModel(x));
            objectRepository.AddType((CounterModel.CounterEntity x) => new CounterModel(x));
            objectRepository.AddType((HashModel.HashEntity x) => new HashModel(x));
            objectRepository.AddType((JobModel.JobEntity x) => new JobModel(x));
            objectRepository.AddType((JobParameterModel.JobParameterEntity x) => new JobParameterModel(x));
            objectRepository.AddType((JobQueueModel.JobQueueEntity x) => new JobQueueModel(x));
            objectRepository.AddType((ListModel.ListEntity x) => new ListModel(x));
            objectRepository.AddType((ServerModel.ServerEntity x) => new ServerModel(x));
            objectRepository.AddType((SetModel.SetEntity x) => new SetModel(x));
            objectRepository.AddType((StateModel.StateEntity x) => new StateModel(x));
        }

        public static IGlobalConfiguration<ObjectRepositoryStorage> UseHangfireStorage(
            [NotNull] this IGlobalConfiguration configuration, ObjectRepositoryBase objectRepository)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var storage = new ObjectRepositoryStorage(objectRepository);
            return configuration.UseStorage(storage);
        }
    }
}
