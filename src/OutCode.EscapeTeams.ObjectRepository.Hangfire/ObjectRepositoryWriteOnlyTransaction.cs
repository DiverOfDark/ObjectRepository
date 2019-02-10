using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class ObjectRepositoryWriteOnlyTransaction : JobStorageTransaction
    {
        private readonly Queue<Action> _commandQueue = new Queue<Action>();
        private readonly ObjectRepositoryStorage _storage;        

        public ObjectRepositoryWriteOnlyTransaction(ObjectRepositoryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public override void Commit()
        {
            foreach (var command in _commandQueue)
            {
                command();
            }                
        }

        public override void ExpireJob(string jobId, TimeSpan expireIn)
        {
            QueueCommand(() => _storage.ObjectRepository.Set<JobModel>()
                .Where(v => v.Id == Guid.Parse(jobId)).ForEach(s => s.ExpireAt = DateTime.UtcNow.Add(expireIn)));
        }

        public override void PersistJob(string jobId)
        {
            QueueCommand(() => _storage.ObjectRepository.Set<JobModel>()
                .Where(v => v.Id == Guid.Parse(jobId)).ForEach(s => s.ExpireAt = null));
        }

        public override void SetJobState(string jobId, IState state)
        {
            QueueCommand(() =>
            {
                var stateModel = new StateModel
                {
                    JobId = Guid.Parse(jobId),
                    Name = state.Name,
                    Reason = state.Reason,
                    CreatedAt = DateTime.UtcNow,
                    Data = JobHelper.ToJson(state.SerializeData())
                };
                _storage.ObjectRepository.Add(
                    stateModel);

                var jobIdGuid = Guid.Parse(jobId);
                
                _storage.ObjectRepository.Set<JobModel>().First(v => v.Id == jobIdGuid)
                    .StateId = stateModel.Id;
            });
        }

        public override void AddJobState(string jobId, IState state)
        {
            QueueCommand(() => _storage.ObjectRepository.Add(
                new StateModel
                {
                    JobId = Guid.Parse(jobId),
                    Name = state.Name,
                    Reason = state.Reason,
                    CreatedAt = DateTime.UtcNow,
                    Data = JobHelper.ToJson(state.SerializeData())
                }));
        }

        public override void AddToQueue(string queue, string jobId)
        {
            var provider = _storage.QueueProviders.GetProvider(queue);
            var persistentQueue = provider.GetJobQueue();

            var jobGuid = Guid.Parse(jobId);
            QueueCommand(() => persistentQueue.Enqueue(queue, jobGuid));
        }

        public override void IncrementCounter(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Add(new CounterModel(key)
                {Value = +1})
            );
        }

        public override void IncrementCounter(string key, TimeSpan expireIn)
        {
            QueueCommand(() => _storage.ObjectRepository.Add(new CounterModel(key)
                {Value = +1, ExpireAt = DateTime.UtcNow.Add(expireIn)})
            );
        }

        public override void DecrementCounter(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Add(new CounterModel(key)
                {Value = -1})
            );
        }

        public override void DecrementCounter(string key, TimeSpan expireIn)
        {
            QueueCommand(() => _storage.ObjectRepository.Add(new CounterModel(key)
                {Value = -1, ExpireAt = DateTime.UtcNow.Add(expireIn)})
            );
        }

        public override void AddToSet(string key, string value)
        {
            AddToSet(key, value, 0.0);
        }

        public override void AddToSet(string key, string value, double score)
        {
            QueueCommand(() =>
            {
                var fetchedSet =
                    _storage.ObjectRepository.Set<SetModel>().FirstOrDefault(v => v.Key == key && v.Value == value);
                
                if (fetchedSet == null)
                {
                    fetchedSet = new SetModel(key, value);
                    _storage.ObjectRepository.Add(fetchedSet);
                }

                fetchedSet.Score = score;
            });
        }

        public override void RemoveFromSet(string key, string value)
        {
            QueueCommand(() =>
                _storage.ObjectRepository.Remove<SetModel>(v => v.Key == key && v.Value == value)
            );
        }

        public override void InsertToList(string key, string value)
        {
            QueueCommand(() =>
                _storage.ObjectRepository.Add(new ListModel(key, value)));
        }

        public override void RemoveFromList(string key, string value)
        {
            QueueCommand(() => _storage.ObjectRepository.Remove<ListModel>(v => v.Key == key && v.Value == value));
        }

        public override void TrimList(string key, int keepStartingFrom, int keepEndingAt)
        {
            QueueCommand(() =>
            {
                var listModels = _storage.ObjectRepository.Set<ListModel>().Where(v => v.Key == key).Skip(keepStartingFrom)
                    .Take(keepEndingAt - keepStartingFrom + 1)
                    .ToList();
                _storage.ObjectRepository.RemoveRange(
                    listModels);
            });
        }

        public override void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            QueueCommand(() =>
            {
                foreach (var keyValuePair in keyValuePairs)
                {
                    var fetchedHash = _storage.ObjectRepository.Set<HashModel>()
                        .FirstOrDefault(v => v.Key == key && v.Field == keyValuePair.Key);
                    
                    if (fetchedHash == null)
                    {
                        fetchedHash = new HashModel(key, keyValuePair.Key);
                        _storage.ObjectRepository.Add(fetchedHash);
                    }

                    fetchedHash.Value = keyValuePair.Value;
                }
            });
        }

        public override void RemoveHash(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Remove<HashModel>(v => v.Key == key));
        }

        public override void AddRangeToSet(string key, IList<string> items)
        {
            QueueCommand(() => items.Select(v => new SetModel(key, v))
                .ForEach(v => _storage.ObjectRepository.Add(v)));
        }

        public override void RemoveSet(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Remove<SetModel>(v => v.Key == key));
        }

        public override void ExpireHash(string key, TimeSpan expireIn)
        {
            var when = DateTime.UtcNow.Add(expireIn);
            QueueCommand(() => _storage.ObjectRepository.Set<HashModel>()
                .Where(v=>v.Key == key)
                .ForEach(s=>s.ExpireAt = when));
        }

        public override void ExpireSet(string key, TimeSpan expireIn)
        {
            var when = DateTime.UtcNow.Add(expireIn);
            QueueCommand(() => _storage.ObjectRepository.Set<SetModel>()
                .Where(v => v.Key == key)
                .ForEach(s => s.ExpireAt = when));
        }

        public override void ExpireList(string key, TimeSpan expireIn)
        {
            var when = DateTime.UtcNow.Add(expireIn);
            QueueCommand(() => _storage.ObjectRepository.Set<ListModel>()
                .Where(v => v.Key == key)
                .ForEach(s => s.ExpireAt = when));
        }

        public override void PersistHash(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Set<HashModel>()
                .Where(v => v.Key == key).ForEach(s => s.ExpireAt = null));
        }

        public override void PersistSet(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Set<SetModel>()
                .Where(v => v.Key == key).ForEach(s => s.ExpireAt = null));
        }

        public override void PersistList(string key)
        {
            QueueCommand(() => _storage.ObjectRepository.Set<ListModel>()
                .Where(v => v.Key == key).ForEach(s => s.ExpireAt = null));
        }

        internal void QueueCommand(Action action) => _commandQueue.Enqueue(action);
    }
}