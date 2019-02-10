using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class AggregatedCounterModel : ModelBase
    {
        internal class AggregatedCounterEntity : BaseEntity
        {
            public string Key { get; set; }
            public int Value { get; set; }
            public DateTime ExpireAt { get; set; }
        }

        private readonly AggregatedCounterEntity _aggregatedCounter;

        public AggregatedCounterModel(AggregatedCounterEntity aggregatedCounter)
        {
            _aggregatedCounter = aggregatedCounter;
        }

        public AggregatedCounterModel(string key)
        {
            _aggregatedCounter = new AggregatedCounterEntity
            {
                Id = Guid.NewGuid(),
                Key = key
            };
        }

        protected override BaseEntity Entity => _aggregatedCounter;

        public DateTime ExpireAt
        {
            get => _aggregatedCounter.ExpireAt;
            set => UpdateProperty(() => _aggregatedCounter.ExpireAt, value);
        }

        public string Key
        {
            get => _aggregatedCounter.Key;
            set => UpdateProperty(() => _aggregatedCounter.Key, value);
        }

        public int Value
        {
            get => _aggregatedCounter.Value;
            set => UpdateProperty(() => _aggregatedCounter.Value, value);
        }
    }
}