using System;
using Newtonsoft.Json;

namespace OutCode.EscapeTeams.ObjectRepository.EventStore
{
    public class EventModel : ModelBase
    {
        private EventEntity _entity;

        public EventModel(EventEntity entity)
        {
            _entity = entity;
        }

        public EventModel(ModelChangedEventArgs args)
        {
            _entity = new EventEntity
            {
                Timestamp = DateTime.UtcNow,
                Action = args.ChangeType,
                Type = args.Entity.GetType().AssemblyQualifiedName,
                Entity = JsonConvert.SerializeObject(args.Entity),
                Id = Guid.NewGuid()
            };
            if (args.ChangeType == ChangeType.Update)
            {
                _entity.ModifiedPropertyName = args.PropertyName;
                _entity.ModifiedPropertyValue = JsonConvert.SerializeObject(args.NewValue);
            }
        }

        protected internal override BaseEntity Entity => _entity;

        public DateTime Timestamp => _entity.Timestamp;

        public ChangeType Action => _entity.Action;

        public string ModifiedPropertyName => _entity.ModifiedPropertyName;

        public string ModifiedPropertyValue => _entity.ModifiedPropertyValue;

        public BaseEntity AffectedEntity => (BaseEntity) JsonConvert.DeserializeObject(_entity.Entity, Type);
        public Type Type => Type.GetType(_entity.Type);
    }
}