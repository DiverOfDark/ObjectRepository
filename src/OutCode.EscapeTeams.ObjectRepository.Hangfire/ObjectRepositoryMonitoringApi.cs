using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryMonitoringApi : IMonitoringApi
    {
        private readonly ObjectRepositoryStorage _storage;

        public ObjectRepositoryMonitoringApi([NotNull] ObjectRepositoryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public long ScheduledCount()
        {
            return GetNumberOfJobsByStateName(ScheduledState.StateName);
        }

        public long EnqueuedCount(string queue)
        {
            var queueApi = GetQueueApi(queue);
            var counters = queueApi.GetEnqueuedAndFetchedCount(queue);

            return counters.EnqueuedCount ?? 0;
        }

        public long FetchedCount(string queue)
        {
            var queueApi = GetQueueApi(queue);
            var counters = queueApi.GetEnqueuedAndFetchedCount(queue);

            return counters.FetchedCount ?? 0;
        }

        public long FailedCount()
        {
            return GetNumberOfJobsByStateName(FailedState.StateName);
        }

        public long ProcessingCount()
        {
            return GetNumberOfJobsByStateName(ProcessingState.StateName);
        }

        public JobList<ProcessingJobDto> ProcessingJobs(int @from, int count)
        {
            return GetJobs(
                from, count,
                ProcessingState.StateName,
                (sqlJob, job, stateData) => new ProcessingJobDto
                {
                    Job = job,
                    ServerId = stateData.ContainsKey("ServerId") ? stateData["ServerId"] : stateData["ServerName"],
                    StartedAt = JobHelper.DeserializeDateTime(stateData["StartedAt"]),
                });
        }

        public JobList<ScheduledJobDto> ScheduledJobs(int @from, int count)
        {
            return GetJobs(
                from, count,
                ScheduledState.StateName,
                (sqlJob, job, stateData) => new ScheduledJobDto
                {
                    Job = job,
                    EnqueueAt = JobHelper.DeserializeDateTime(stateData["EnqueueAt"]),
                    ScheduledAt = JobHelper.DeserializeDateTime(stateData["ScheduledAt"])
                });
        }

        public IDictionary<DateTime, long> SucceededByDatesCount() => GetTimelineStats("succeeded");

        public IDictionary<DateTime, long> FailedByDatesCount() => GetTimelineStats("failed");

        public IList<ServerDto> Servers()
        {
            var servers = _storage.ObjectRepository.Set<ServerModel>().ToList();

            var result = new List<ServerDto>();

            foreach (var server in servers)
            {
                var data = JobHelper.FromJson<ServerData>(server.Data);
                result.Add(new ServerDto
                {
                    Name = server.Name,
                    Heartbeat = server.LastHeartbeat,
                    Queues = data.Queues,
                    StartedAt = data.StartedAt ?? DateTime.MinValue,
                    WorkersCount = data.WorkerCount
                });
            }

            return result;
        }

        public JobList<FailedJobDto> FailedJobs(int @from, int count)
        {
            var states = _storage.ObjectRepository.Set<StateModel>().ToDictionary(v => v.Id, v => v);
            return GetJobs(
                @from,
                count,
                FailedState.StateName,
                (sqlJob, job, stateData) => new FailedJobDto
                {
                    Job = job,
                    Reason = states[sqlJob.StateId].Reason,
                    ExceptionDetails = stateData["ExceptionDetails"],
                    ExceptionMessage = stateData["ExceptionMessage"],
                    ExceptionType = stateData["ExceptionType"],
                    FailedAt = JobHelper.DeserializeNullableDateTime(stateData["FailedAt"])
                });
        }

        public JobList<SucceededJobDto> SucceededJobs(int @from, int count)
        {
            return GetJobs(
                from,
                count,
                SucceededState.StateName,
                (sqlJob, job, stateData) => new SucceededJobDto
                {
                    Job = job,
                    Result = stateData.ContainsKey("Result") ? stateData["Result"] : null,
                    TotalDuration = stateData.ContainsKey("PerformanceDuration") && stateData.ContainsKey("Latency")
                        ? (long?)long.Parse(stateData["PerformanceDuration"]) + (long?)long.Parse(stateData["Latency"])
                        : null,
                    SucceededAt = JobHelper.DeserializeNullableDateTime(stateData["SucceededAt"])
                });
        }

        public JobList<DeletedJobDto> DeletedJobs(int @from, int count)
        {
            return GetJobs(
                from,
                count,
                DeletedState.StateName,
                (sqlJob, job, stateData) => new DeletedJobDto
                {
                    Job = job,
                    DeletedAt = JobHelper.DeserializeNullableDateTime(stateData.ContainsKey("DeletedAt")
                        ? stateData["DeletedAt"]
                        : null)
                });
        }

        public IList<QueueWithTopEnqueuedJobsDto> Queues()
        {
            var tuples = _storage.QueueProviders
                .Select(x => x.GetJobQueueMonitoringApi())
                .SelectMany(x => x.GetQueues(), (monitoring, queue) => new { Monitoring = monitoring, Queue = queue })
                .OrderBy(x => x.Queue)
                .ToArray();

            var result = new List<QueueWithTopEnqueuedJobsDto>(tuples.Length);

            foreach (var tuple in tuples)
            {
                var enqueuedJobIds = tuple.Monitoring.GetEnqueuedJobIds(tuple.Queue, 0, 5);
                var counters = tuple.Monitoring.GetEnqueuedAndFetchedCount(tuple.Queue);

                var firstJobs = EnqueuedJobs(enqueuedJobIds);

                result.Add(new QueueWithTopEnqueuedJobsDto
                {
                    Name = tuple.Queue,
                    Length = counters.EnqueuedCount ?? 0,
                    Fetched = counters.FetchedCount,
                    FirstJobs = firstJobs
                });
            }

            return result;
        }

        public JobList<EnqueuedJobDto> EnqueuedJobs(string queue, int @from, int perPage)
        {
            var queueApi = GetQueueApi(queue);
            var enqueuedJobIds = queueApi.GetEnqueuedJobIds(queue, from, perPage);

            return EnqueuedJobs(enqueuedJobIds);
        }

        public JobList<FetchedJobDto> FetchedJobs(string queue, int @from, int perPage)
        {
            var queueApi = GetQueueApi(queue);
            var fetchedJobIds = queueApi.GetFetchedJobIds(queue, from, perPage);

            return FetchedJobs(fetchedJobIds);
        }

        public IDictionary<DateTime, long> HourlySucceededJobs()
        {
            return GetHourlyTimelineStats("succeeded");
        }

        public IDictionary<DateTime, long> HourlyFailedJobs()
        {
            return GetHourlyTimelineStats("failed");
        }

        public JobDetailsDto JobDetails(String jobIdString)
        {
            if (!Guid.TryParse(jobIdString, out var jobId))
                return null;
            var job = _storage.ObjectRepository.Set<JobModel>().FirstOrDefault(v => v.Id == jobId);
            if (job == null) 
                return null;

            var parameters = _storage.ObjectRepository.Set<JobParameterModel>()
                .Where(v => v.JobId == jobId).ToDictionary(x => x.Name, x => x.Value);
            var history = _storage.ObjectRepository.Set<StateModel>()
                .Where(v => v.JobId == jobId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(x => new StateHistoryDto
                {
                    StateName = x.Name,
                    CreatedAt = x.CreatedAt,
                    Reason = x.Reason,
                    Data = new Dictionary<string, string>(
                        JobHelper.FromJson<Dictionary<string, string>>(x.Data),
                        StringComparer.OrdinalIgnoreCase),
                })
                .ToList();

            return new JobDetailsDto
            {
                CreatedAt = job.CreatedAt,
                ExpireAt = job.ExpireAt,
                Job = DeserializeJob(job.InvocationData, job.Arguments),
                History = history,
                Properties = parameters
            };
        }

        public long SucceededListCount() => GetNumberOfJobsByStateName(SucceededState.StateName);

        public long DeletedListCount() => GetNumberOfJobsByStateName(DeletedState.StateName);

        public StatisticsDto GetStatistics()
        {
            var stateIds = _storage.ObjectRepository.Set<StateModel>().ToList();
            var jobs = _storage.ObjectRepository.Set<JobModel>();

            Func<string, int> count = name =>
            {
                var counters = _storage.ObjectRepository.Set<CounterModel>();
                var agcounters = _storage.ObjectRepository.Set<AggregatedCounterModel>();
                return counters.Where(v => v.Key == name).Sum(s => s.Value)
                       + agcounters.Where(v => v.Key == name).Sum(s => s.Value);
            };
            
            var stats = new StatisticsDto
            {
                Enqueued = jobs.Count(v=>stateIds.Where(s=>s.Name == "Enqueued").Any(s=>s.Id == v.StateId)),
                Failed = jobs.Count(v=>stateIds.Where(s=>s.Name == "Failed").Any(s=>s.Id == v.StateId)),
                Processing = jobs.Count(v=>stateIds.Where(s=>s.Name == "Processing").Any(s=>s.Id == v.StateId)),
                Scheduled = jobs.Count(v=>stateIds.Where(s=>s.Name == "Scheduled").Any(s=>s.Id == v.StateId)),
                Servers = _storage.ObjectRepository.Set<ServerModel>().Count(),
                Succeeded = count("stats:succeeded"),
                Deleted = count("stats:deleted"),
                Recurring = _storage.ObjectRepository
                    .Set<SetModel>().Count(v => v.Key == "recurring-jobs"),
                Queues = _storage.QueueProviders
                    .SelectMany(x => x.GetJobQueueMonitoringApi().GetQueues())
                    .Count()
            };

            return stats;
        }

        private Dictionary<DateTime, long> GetHourlyTimelineStats(string type)
        {
            var endDate = DateTime.UtcNow;
            var dates = new List<DateTime>();
            for (var i = 0; i < 24; i++)
            {
                dates.Add(endDate);
                endDate = endDate.AddHours(-1);
            }

            var keyMaps = dates.ToDictionary(x => $"stats:{type}:{x.ToString("yyyy-MM-dd-HH")}", x => x);

            return GetTimelineStats(keyMaps);
        }

        private Dictionary<DateTime, long> GetTimelineStats(string type)
        {
            var endDate = DateTime.UtcNow.Date;
            var dates = new List<DateTime>();
            for (var i = 0; i < 7; i++)
            {
                dates.Add(endDate);
                endDate = endDate.AddDays(-1);
            }

            var keyMaps = dates.ToDictionary(x => $"stats:{type}:{x.ToString("yyyy-MM-dd")}", x => x);

            return GetTimelineStats(keyMaps);
        }

        private Dictionary<DateTime, long> GetTimelineStats(IDictionary<string, DateTime> keyMaps)
        {
            var valuesMap =
                _storage.ObjectRepository.Set<AggregatedCounterModel>()
                    .Where(v => keyMaps.Keys.Contains(v.Key))
                    .ToDictionary(v => v.Key, v => v.Value);

            foreach (var key in keyMaps.Keys)
            {
                if (!valuesMap.ContainsKey(key)) valuesMap.Add(key, 0);
            }

            var result = new Dictionary<DateTime, long>();
            for (var i = 0; i < keyMaps.Count; i++)
            {
                var value = valuesMap[keyMaps.ElementAt(i).Key];
                result.Add(keyMaps.ElementAt(i).Value, value);
            }

            return result;
        }

        private ObjectRepositoryJobQueueMonitoringApi GetQueueApi(string queueName)
        {
            var provider = _storage.QueueProviders.GetProvider(queueName);
            var monitoringApi = provider.GetJobQueueMonitoringApi();

            return monitoringApi;
        }

        private JobList<EnqueuedJobDto> EnqueuedJobs(IEnumerable<Guid> jobIds)
        {
            var states = _storage.ObjectRepository.Set<StateModel>().ToDictionary(v => v.Id, v => v);

            var jobs = _storage.ObjectRepository.Set<JobModel>()
                .Where(v => jobIds.Contains(v.Id))
                .ToDictionary(v=>v.Id, v=>v);
            
            var sortedSqlJobs = jobIds
                .Select(v=>jobs[v])
                .ToList();

            return DeserializeJobs(
                sortedSqlJobs,
                states,
                (sqlJob, job, stateData) => new EnqueuedJobDto
                {
                    Job = job,
                    State = states[sqlJob.StateId].Name,
                    EnqueuedAt = states[sqlJob.StateId].Name == EnqueuedState.StateName
                        ? JobHelper.DeserializeNullableDateTime(stateData["EnqueuedAt"])
                        : null
                });
        }

        private long GetNumberOfJobsByStateName(string stateName)
        {
            var stateId = _storage.ObjectRepository.Set<StateModel>().Where(v => v.Name == stateName).Select(v => v.Id)
                .FirstOrDefault();
            return _storage.ObjectRepository.Set<JobModel>().Count(v => v.StateId == stateId);
        }

        private static Job DeserializeJob(string invocationData, string arguments)
        {
            var data = JobHelper.FromJson<InvocationData>(invocationData);
            data.Arguments = arguments;

            try
            {
                return data.Deserialize();
            }
            catch (JobLoadException)
            {
                return null;
            }
        }

        private JobList<TDto> GetJobs<TDto>(
            int from,
            int count,
            string stateName,
            Func<JobModel, Job, Dictionary<string, string>, TDto> selector)
        {
            var states = _storage.ObjectRepository.Set<StateModel>().ToDictionary(v => v.Id, v => v);

            var jobs = _storage.ObjectRepository.Set<JobModel>()
                .Where(s => states[s.StateId].Name == stateName)
                .Skip(from)
                .Take(count)
                .ToList();
                
            return DeserializeJobs(jobs, states, selector);
        }

        private static JobList<TDto> DeserializeJobs<TDto>(IEnumerable<JobModel> jobs,
            Dictionary<Guid, StateModel> states,
            Func<JobModel, Job, Dictionary<string, string>, TDto> selector)
        {
            var result = new List<KeyValuePair<string, TDto>>(jobs.Count());

            foreach (var job in jobs)
            {
                var dto = default(TDto);

                if (job.InvocationData != null)
                {
                    var deserializedData = JobHelper.FromJson<Dictionary<string, string>>(states[job.StateId].Data);
                    var stateData = deserializedData != null
                        ? new Dictionary<string, string>(deserializedData, StringComparer.OrdinalIgnoreCase)
                        : null;

                    dto = selector(job, DeserializeJob(job.InvocationData, job.Arguments), stateData);
                }

                result.Add(new KeyValuePair<string, TDto>(
                    job.Id.ToString(), dto));
            }

            return new JobList<TDto>(result);
        }

        private JobList<FetchedJobDto> FetchedJobs(IEnumerable<Guid> jobIds)
        {
            var states = _storage.ObjectRepository.Set<StateModel>().ToDictionary(v => v.Id, v => v);

            var jobs = _storage.ObjectRepository.Set<JobModel>()
                .Where(v => jobIds.Contains(v.Id))
                .ToList();

            var result = new List<KeyValuePair<string, FetchedJobDto>>(jobs.Count);

            foreach (var job in jobs)
            {
                result.Add(new KeyValuePair<string, FetchedJobDto>(
                    job.Id.ToString(),
                    new FetchedJobDto
                    {
                        Job = DeserializeJob(job.InvocationData, job.Arguments),
                        State = states[job.StateId].Name                        
                    }));
            }

            return new JobList<FetchedJobDto>(result);
        }
    }
}
