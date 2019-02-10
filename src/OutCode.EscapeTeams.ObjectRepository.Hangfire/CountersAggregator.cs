using System;
using System.Linq;
using System.Threading;
using Hangfire.Server;
using OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    public class CountersAggregator : IServerComponent
    {
        private readonly ObjectRepositoryStorage _storage;

        public CountersAggregator(ObjectRepositoryStorage storage)
        {
            _storage = storage;
        }

        public void Execute(CancellationToken cancellationToken)
        {
            var counters = _storage.ObjectRepository.Set<CounterModel>().ToList();

            var groupedCounters = counters.GroupBy(c => c.Key).Select(g => new
            {
                g.Key,
                Value = g.Sum(c => c.Value),
                ExpireAt = g.Max(c => c.ExpireAt)
            });

            foreach (var counter in groupedCounters)
            {
                var aggregate = _storage.ObjectRepository.Set<AggregatedCounterModel>()
                    .FirstOrDefault(a => a.Key == counter.Key);

                if (aggregate == null)
                {
                    aggregate = new AggregatedCounterModel(counter.Key)
                    {
                        Key = counter.Key,
                        Value = 0,
                        ExpireAt = DateTime.MinValue
                    };

                    _storage.ObjectRepository.Add(aggregate);
                }

                aggregate.Value += counter.Value;

                if (counter.ExpireAt > aggregate.ExpireAt)
                {
                    aggregate.ExpireAt = counter.ExpireAt;
                }
            }

            _storage.ObjectRepository.RemoveRange(counters);

            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
        }

        public override string ToString()
        {
            return "Counter Table Aggregator";
        }
    }
}