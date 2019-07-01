using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class CounterModel : ModelBase
    {
        internal class CounterEntity : BaseEntity
        {
            public string Key { get; set; }
            public int Value { get; set; }
            public DateTime? ExpireAt { get; set; }
        }

        private readonly CounterEntity _counter;

        public CounterModel(CounterEntity counter)
        {
            _counter = counter;
        }

        public CounterModel(string key)
        {
            _counter = new CounterEntity
            {
                Id = Guid.NewGuid(),
                Key = key
            };
        }

        protected override BaseEntity Entity => _counter;

        public DateTime? ExpireAt
        {
            get => _counter.ExpireAt;
            set => UpdateProperty(() => () => _counter.ExpireAt, value);
        }

        public string Key
        {
            get => _counter.Key;
            set => UpdateProperty(() => () => _counter.Key, value);
        }

        public int Value
        {
            get => _counter.Value;
            set => UpdateProperty(() => () =>  _counter.Value, value);
        }
    }
}