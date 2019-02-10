using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class ListModel : ModelBase
    {
        internal class ListEntity : BaseEntity
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public DateTime? ExpireAt { get; set; }
        }

        private readonly ListEntity _list;

        public ListModel(ListEntity list)
        {
            _list = list;
        }

        public ListModel(string key, string value)
        {
            _list = new ListEntity
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value
            };
        }

        protected override BaseEntity Entity => _list;

        public DateTime? ExpireAt
        {
            get => _list.ExpireAt;
            set => UpdateProperty(() => _list.ExpireAt, value);
        }

        public string Key
        {
            get => _list.Key;
            set => UpdateProperty(() => _list.Key, value);
        }

        public string Value
        {
            get => _list.Value;
            set => UpdateProperty(() => _list.Value, value);
        }
    }
}