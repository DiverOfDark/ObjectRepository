using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class SetModel : ModelBase
    {
        internal class SetEntity : BaseEntity
        {
            public string Key { get; set; }

            public double Score { get; set; }

            public string Value { get; set; }

            public DateTime? ExpireAt { get; set; }
        }

        private readonly SetEntity _set;

        public SetModel(SetEntity set)
        {
            _set = set;
        }

        public SetModel(string key, string value)
        {
            _set = new SetEntity
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                Score = 0.0
            };
        }

        protected override BaseEntity Entity => _set;
        
        public DateTime? ExpireAt
        {
            get => _set.ExpireAt;
            set => UpdateProperty(_set, () => x => x.ExpireAt, value);
        }
        
        public double Score
        {
            get => _set.Score;
            set => UpdateProperty(_set, () => x => x.Score, value);
        }

        public string Key
        {
            get => _set.Key;
            set => UpdateProperty(_set, () => x => x.Key, value);
        }

        public string Value
        {
            get => _set.Value;
            set => UpdateProperty(_set, () => x => x.Value, value);
        }
    }
}
