using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryConnection : JobStorageConnection
	{
		private class InProcessLockDisposable : IDisposable
		{
			public static readonly InProcessLockDisposable Instance = new InProcessLockDisposable();
			
			private InProcessLockDisposable()
			{
			}
			
			public void Dispose() => Monitor.Exit(Instance);
		}
		
        private readonly ObjectRepositoryStorage _storage;
        private static readonly ConcurrentList<JobQueueModel> _jobsTakenOut = new ConcurrentList<JobQueueModel>();

        public ObjectRepositoryConnection(ObjectRepositoryStorage storage)
        {
	        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

		public override IWriteOnlyTransaction CreateWriteTransaction()
		{
            return new ObjectRepositoryWriteOnlyTransaction(_storage);
        }

		public override IDisposable AcquireDistributedLock(string resource, TimeSpan timeout)
		{
			Monitor.Enter(InProcessLockDisposable.Instance);
			return InProcessLockDisposable.Instance;
        }

		public override IFetchedJob FetchNextJob(string[] queues, CancellationToken cancellationToken)
		{
			lock (_jobsTakenOut)
			{
				if (queues == null || queues.Length == 0) throw new ArgumentNullException(nameof(queues));

				if (queues == null) throw new ArgumentNullException(nameof(queues));
				if (queues.Length == 0) throw new ArgumentException("Queue array must be non-empty.", nameof(queues));

				JobQueueModel fetchedJob;

				do
				{
					cancellationToken.ThrowIfCancellationRequested();

					fetchedJob = _storage.ObjectRepository.Set<JobQueueModel>()
						.Where(s => s.FetchedAt == null || s.FetchedAt < DateTime.UtcNow)
						.Where(v => !_jobsTakenOut.Contains(v))
						.FirstOrDefault(s => queues.Contains(s.Queue));

					if (fetchedJob != null)
					{
						fetchedJob.FetchedAt = DateTime.UtcNow;
					}

					if (fetchedJob == null)
					{
						cancellationToken.WaitHandle.WaitOne(ObjectRepositoryExtensions.QueuePollInterval);
						cancellationToken.ThrowIfCancellationRequested();
					}
				} while (fetchedJob == null);

				return new ObjectRepositoryFetchedJob(_jobsTakenOut, _storage.ObjectRepository,
					fetchedJob);
			}
		}

		public override string CreateExpiredJob(
			Job job,
			IDictionary<string, string> parameters, 
			DateTime createdAt,
			TimeSpan expireIn)
		{
            var invocationData = InvocationData.Serialize(job);

            var jobModel = new JobModel();
            _storage.ObjectRepository.Add(jobModel);
            jobModel.InvocationData = JobHelper.ToJson(invocationData);
            jobModel.Arguments = invocationData.Arguments;
            jobModel.CreatedAt = createdAt;
            jobModel.ExpireAt = createdAt.Add(expireIn);

            var jobId = jobModel.Id;

            foreach (var parameter in parameters)
            {
	            var jpm = new JobParameterModel(jobId, parameter.Key) {Value = parameter.Value};
	            _storage.ObjectRepository.Add(jpm);
            }
            
            return jobId.ToString();
        }

		public override JobData GetJobData(string id)
		{
			if (!Guid.TryParse(id, out var guid))
				return null;
			
			var jobData = _storage.ObjectRepository.Set<JobModel>().Find(guid);

            if (jobData == null) return null;

            Job job = null;
            JobLoadException loadException = null;

            try
            {
	            var invocationData = JobHelper.FromJson<InvocationData>(jobData.InvocationData);
	            invocationData.Arguments = jobData.Arguments;

                job = invocationData.Deserialize();
            }
            catch (JobLoadException ex)
            {
                loadException = ex;
            }

            var state = _storage.ObjectRepository.Set<StateModel>().FirstOrDefault(v => v.Id == jobData.StateId);
            
            return new JobData
            {
                Job = job,
                State = state?.Name,
                CreatedAt = jobData.CreatedAt,
                LoadException = loadException
            };
        }

		public override StateData GetStateData(string jobId)
		{
			if (!Guid.TryParse(jobId, out var jobIdGuid))
				return null;
			
			var stateId = _storage.ObjectRepository.Set<JobModel>().Find(jobIdGuid);
			if (stateId?.StateId == null)
				return null;

			var state = _storage.ObjectRepository.Set<StateModel>().Find(stateId.StateId.Value);
			if (state == null)
				return null;
			
			var data = new Dictionary<string, string>(
				JobHelper.FromJson<Dictionary<string, string>>(state.Data),
				StringComparer.OrdinalIgnoreCase);

			return new StateData
			{
				Name = state.Name,
				Reason = state.Reason,
				Data = data
			};
		}

		public override void SetJobParameter(string id, string name, string value)
		{
			var jobId = Guid.Parse(id);

			var jobParam = _storage.ObjectRepository.Set<JobParameterModel>()
				.FirstOrDefault(v => v.JobId == jobId && v.Name == name);

			if (jobParam == null)
			{
				jobParam = new JobParameterModel(jobId, name);
				_storage.ObjectRepository.Add(jobParam);
			}

			jobParam.Value = value;
		}

		public override string GetJobParameter(string id, string name)
		{
			if (!Guid.TryParse(id, out var jobId))
				return null;
			return _storage.ObjectRepository.Set<JobParameterModel>()
				.Where(v => v.JobId == jobId && v.Name == name)
				.Select(v => v.Value)
				.FirstOrDefault();
        }

		public override HashSet<string> GetAllItemsFromSet(string key)
		{
			return new HashSet<string>(_storage.ObjectRepository.Set<SetModel>().Where(v => v.Key == key)
				.Select(v => v.Value));
        }

		public override string GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore)
		{
			return _storage.ObjectRepository.Set<SetModel>()
				.Where(v => v.Key == key && v.Score >= fromScore && v.Score <= toScore)
				.OrderBy(v => v.Score)
				.Select(v => v.Value)
				.FirstOrDefault();
        }

		public override void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
		{
			foreach (var keyValuePair in keyValuePairs)
			{
				var fetchedHash = _storage.ObjectRepository
					.Set<HashModel>()
					.FirstOrDefault(v => v.Key == key && v.Field == keyValuePair.Key);
				
				if (fetchedHash == null)
				{
					fetchedHash = new HashModel(key, keyValuePair.Key);
					_storage.ObjectRepository.Add(fetchedHash);
				}

				fetchedHash.Value = keyValuePair.Value;
			}
		}

		public override Dictionary<string, string> GetAllEntriesFromHash(string key)
		{
			return _storage.ObjectRepository.Set<HashModel>()
				.Where(v => v.Key == key)
				.ToDictionary(v => v.Field, v => v.Value);
        }

		public override void AnnounceServer(string serverId, ServerContext context)
		{
			if (serverId == null) throw new ArgumentNullException(nameof(serverId));
			if (context == null) throw new ArgumentNullException(nameof(context));

			var data = new ServerData
			{
				WorkerCount = context.WorkerCount,
				Queues = context.Queues,
				StartedAt = DateTime.UtcNow,
			};

			var existing = _storage.ObjectRepository.Set<ServerModel>().FirstOrDefault(v => v.Name == serverId);

			if (existing == null)
			{
				existing = new ServerModel(serverId);
				_storage.ObjectRepository.Add(existing);
			}

			existing.Data = JobHelper.ToJson(data);
			existing.LastHeartbeat = DateTime.UtcNow;
		}

		public override void RemoveServer(string serverId)
		{
			_storage.ObjectRepository.Remove<ServerModel>(v=>v.Name == serverId);
        }

		public override void Heartbeat(string serverId)
		{
			_storage.ObjectRepository
				.Set<ServerModel>()
				.First(v => v.Name == serverId).LastHeartbeat = DateTime.UtcNow;
        }

		public override int RemoveTimedOutServers(TimeSpan timeOut)
		{
			var badServers = _storage.ObjectRepository.Set<ServerModel>().Where(v => v.LastHeartbeat < DateTime.UtcNow.Add(timeOut.Negate()))
				.ToList();
			
			_storage.ObjectRepository.RemoveRange(badServers);
			return badServers.Count;
		}

		public override long GetSetCount(string key)
		{
			return _storage.ObjectRepository
				.Set<SetModel>()
				.Count(v => v.Key == key);
        }

		public override List<string> GetRangeFromSet(string key, int startingFrom, int endingAt)
		{
			return _storage.ObjectRepository.Set<SetModel>()
				.Where(v => v.Key == key)
				.Select(s => s.Value)
				.OrderBy(v => v)
				.Skip(startingFrom)
				.Take(endingAt - startingFrom + 1)
				.ToList();
		}

		public override TimeSpan GetSetTtl(string key)
		{
			var result = _storage.ObjectRepository.Set<SetModel>()
				.Where(v => v.Key == key && v.ExpireAt != null)
				.OrderBy(v => v.ExpireAt)
				.Select(v => v.ExpireAt)
				.FirstOrDefault();

			if (!result.HasValue) return TimeSpan.FromSeconds(-1);

			return result.Value - DateTime.UtcNow;
		}

		public override long GetCounter(string key)
		{
			return _storage.ObjectRepository.Set<CounterModel>().Where(v => v.Key == key)
				.Select(v => v.Value).Sum();
        }

		public override long GetHashCount(string key)
		{
			return _storage.ObjectRepository
				.Set<HashModel>()
				.Count(v => v.Key == key);
        }

		public override TimeSpan GetHashTtl(string key)
		{
			var result = _storage.ObjectRepository.Set<HashModel>()
				.Where(v => v.Key == key && v.ExpireAt != null)
				.OrderBy(v => v.ExpireAt)
				.Select(v => v.ExpireAt)
				.FirstOrDefault();

			if (!result.HasValue) return TimeSpan.FromSeconds(-1);

			return result.Value - DateTime.UtcNow;
		}

		public override string GetValueFromHash(string key, string name)
		{
			return _storage.ObjectRepository.Set<HashModel>()
				.Where(v => v.Key == key && v.Field == name)
				.Select(v => v.Value)
				.FirstOrDefault();
        }

		public override long GetListCount(string key)
		{
			return _storage.ObjectRepository.Set<ListModel>()
				.Count(s => s.Key == key);
        }

		public override TimeSpan GetListTtl(string key)
		{
			var result = _storage.ObjectRepository.Set<ListModel>()
				.OrderBy(v => v.ExpireAt)
				.Select(v => v.ExpireAt)
				.FirstOrDefault();
            if (!result.HasValue) return TimeSpan.FromSeconds(-1);

            return result.Value - DateTime.UtcNow;
        }

		public override List<string> GetRangeFromList(string key, int startingFrom, int endingAt)
		{
			return _storage.ObjectRepository
				.Set<ListModel>()
				.Where(v => v.Key == key)
				.Select(v => v.Value)
				.OrderBy(v => v)
				.Skip(startingFrom)
				.Take(endingAt - startingFrom + 1)
				.ToList();
		}

		public override List<string> GetAllItemsFromList(string key)
		{
			return _storage.ObjectRepository.Set<ListModel>()
				.Where(v => v.Key == key)
				.Select(v => v.Value)
				.OrderBy(v => v)
				.ToList();
		}
	}
}
