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
        private readonly ObjectRepositoryBase _repository;
        private ObjectRepositoryStorage _storage;

        public ObjectRepositoryMonitoringApi([NotNull] ObjectRepositoryStorage storage)
        {
            _storage = storage;
            _repository = storage.ObjectRepository;
        }
        
        public long ScheduledCount()
        {
            return GetNumberOfJobsByStateName(ScheduledState.StateName);
        }

        public long EnqueuedCount(string queue)
        {
            var queueApi = _storage.MonitoringApi;
            var counters = queueApi.GetEnqueuedAndFetchedCount(queue);

            return counters.EnqueuedCount ?? 0;
        }

        public long FetchedCount(string queue)
        {
            var queueApi = _storage.MonitoringApi;
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
            var servers = _repository.Set<ServerModel>().ToList();

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
            return GetJobs(
                @from,
                count,
                FailedState.StateName,
                (sqlJob, job, stateData) => new FailedJobDto
                {
                    Job = job,
                    Reason = sqlJob.State.Reason,
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
            var monitoring = _storage.MonitoringApi;
            var queues = monitoring
                .GetQueues()
                .OrderBy(x => x)
                .ToArray();

            var result = new List<QueueWithTopEnqueuedJobsDto>(queues.Length);

            foreach (var queue in queues)
            {
                var enqueuedJobIds = monitoring.GetEnqueuedJobIds(queue, 0, 5);
                var counters = monitoring.GetEnqueuedAndFetchedCount(queue);

                var firstJobs = EnqueuedJobs(enqueuedJobIds);

                result.Add(new QueueWithTopEnqueuedJobsDto
                {
                    Name = queue,
                    Length = counters.EnqueuedCount ?? 0,
                    Fetched = counters.FetchedCount,
                    FirstJobs = firstJobs
                });
            }

            return result;
        }

        public JobList<EnqueuedJobDto> EnqueuedJobs(string queue, int @from, int perPage)
        {
            var queueApi = _storage.MonitoringApi;
            var enqueuedJobIds = queueApi.GetEnqueuedJobIds(queue, from, perPage);

            return EnqueuedJobs(enqueuedJobIds);
        }

        public JobList<FetchedJobDto> FetchedJobs(string queue, int @from, int perPage)
        {
            var queueApi = _storage.MonitoringApi;
            var fetchedJobIds = queueApi.GetFetchedJobIds(queue, from, perPage);

            var jobs = _repository.Set<JobModel>()
                .Where(v => fetchedJobIds.Contains(v.Id))
                .ToList();

            var result = new List<KeyValuePair<string, FetchedJobDto>>(jobs.Count);

            foreach (var job in jobs)
            {
                result.Add(new KeyValuePair<string, FetchedJobDto>(
                    job.Id.ToString(),
                    new FetchedJobDto
                    {
                        Job = DeserializeJob(job.InvocationData, job.Arguments),
                        State = job.State?.Name
                    }));
            }

            return new JobList<FetchedJobDto>(result);
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
            var job = _repository.Set<JobModel>().Find(jobId);
            if (job == null) 
                return null;

            var parameters = _repository.Set<JobParameterModel>()
                .Where(v => v.JobId == jobId).Aggregate(new Dictionary<string, string>(), (a, b) =>
                    {
                        a[b.Name] = b.Value;
                        return a;
                    });
            var history = _repository.Set<StateModel>()
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
            var jobs = _repository.Set<JobModel>();

            Func<string, int> count = name =>
            {
                var counters = _repository.Set<CounterModel>();
                return counters.Where(v => v.Key == name).Sum(s => s.Value);
            };
            
            var stats = new StatisticsDto
            {
                Enqueued = jobs.Count(v=> v.State?.Name == EnqueuedState.StateName),
                Failed = jobs.Count(v=> v.State?.Name == FailedState.StateName),
                Processing = jobs.Count(v=> v.State?.Name == ProcessingState.StateName),
                Scheduled = jobs.Count(v=> v.State?.Name == ScheduledState.StateName),
                Servers = _repository.Set<ServerModel>().Count(),
                Succeeded = count("stats:succeeded"),
                Deleted = count("stats:deleted"),
                Recurring = _repository.Set<SetModel>().Count(v => v.Key == "recurring-jobs"),
                Queues = _storage.MonitoringApi.GetQueues().Count()
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

            var keyMaps = dates.ToDictionary(x => $"stats:{type}:{x:yyyy-MM-dd-HH}", x => x);

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
                _repository.Set<CounterModel>()
                    .Where(v => keyMaps.Keys.Contains(v.Key))
                    .ToList()
                    .Aggregate(new Dictionary<string, long>(), (a, b) =>
                    {
                        if (!a.ContainsKey(b.Key))
                        {
                            a[b.Key] = b.Value;
                        }
                        else
                        {
                            a[b.Key] += b.Value;
                        }

                        return a;
                    });

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

        private JobList<EnqueuedJobDto> EnqueuedJobs(IEnumerable<Guid> jobIds)
        {
            var jobs = _repository.Set<JobModel>()
                .Where(v => jobIds.Contains(v.Id))
                .ToDictionary(v=>v.Id, v=>v);
            
            var sortedSqlJobs = jobIds
                .Select(v=>jobs[v])
                .ToList();

            return DeserializeJobs(
                sortedSqlJobs,
                (sqlJob, job, stateData) => new EnqueuedJobDto
                {
                    Job = job,
                    State = sqlJob.State?.Name,
                    EnqueuedAt = sqlJob.State?.Name == EnqueuedState.StateName
                        ? JobHelper.DeserializeNullableDateTime(stateData["EnqueuedAt"])
                        : null
                });
        }

        private long GetNumberOfJobsByStateName(string stateName)
        {
            return _repository.Set<JobModel>().Count(v => v.State?.Name == stateName);
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
            var jobs = _repository.Set<JobModel>()
                .Where(s => s.State?.Name == stateName)
                .Skip(from)
                .Take(count)
                .ToList();
                
            return DeserializeJobs(jobs, selector);
        }

        private static JobList<TDto> DeserializeJobs<TDto>(ICollection<JobModel> jobs,
            Func<JobModel, Job, Dictionary<string, string>, TDto> selector)
        {
            var result = new List<KeyValuePair<string, TDto>>(jobs.Count());

            foreach (var job in jobs)
            {
                var dto = default(TDto);

                if (job.InvocationData != null)
                {
                    var deserializedData = JobHelper.FromJson<Dictionary<string, string>>(job.State?.Data);
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
    }
}
