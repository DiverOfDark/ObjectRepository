using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class HashModel : ModelBase
    {
        internal class HashEntity : BaseEntity
        {
            public string Key { get; set; }
            public string Field { get; set; }
            public string Value { get; set; }
            public DateTime? ExpireAt { get; set; }
        }

        private readonly HashEntity _hash;

        public HashModel(HashEntity hashEntity)
        {
            _hash = hashEntity;
        }

        public HashModel(string key, string field)
        {
            _hash = new HashEntity
            {
                Id = Guid.NewGuid(),
                Key = key,
                Field = field
            };
        }

        protected override BaseEntity Entity => _hash;
        
        public DateTime? ExpireAt
        {
            get => _hash.ExpireAt;
            set => UpdateProperty(_hash, () => x => _hash.ExpireAt, value);
        }

        public string Field
        {
            get => _hash.Field;
            set => UpdateProperty(_hash, () => x => _hash.Field, value);
        }

        public string Key
        {
            get => _hash.Key;
            set => UpdateProperty(_hash, () => x => _hash.Key, value);
        }

        public string Value
        {
            get => _hash.Value;
            set => UpdateProperty(_hash, () => x => _hash.Value, value);
        }
    }
}
