using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OutCode.EscapeTeams.ObjectRepository.EventStore
{
    public class EventStorage : IStorage, ITrackable
    {
        private readonly IStorage _underlyingStorage;

        public EventStorage(IStorage underlyingStorage)
        {
            _underlyingStorage = underlyingStorage;
        }
    
        public Task SaveChanges() => _underlyingStorage.SaveChanges();

        public async Task<IEnumerable<T>> GetAll<T>() where T:BaseEntity
        {
            var items = new List<T>();
            var events = await _underlyingStorage.GetAll<EventEntity>();
            var eventModels = events.OrderBy(v => v.Timestamp).Select(v => new EventModel(v)).ToList();
            var actualEvents = eventModels.Where(v => v.Type == typeof(T));

            foreach (var ev in actualEvents)
            {
                Apply(ev, items);
            }

            return items;
        }

        private void Apply<T>(EventModel ev, IList<T> items) where T:BaseEntity
        {
            switch (ev.Action)
            {
                case ChangeType.Add:
                    items.Add((T) ev.AffectedEntity);
                    break;
                case ChangeType.Remove:
                    items.Remove(items.First(v => v.Id == ev.AffectedEntity.Id));
                    break;
                case ChangeType.Update:
                    var affectedItem = items.First(v => v.Id == ev.AffectedEntity.Id);
                    UpdatePropertySetter(ev.Type, affectedItem, ev.ModifiedPropertyName, ev.ModifiedPropertyValue);
                    break;
            }
        }

        private void UpdatePropertySetter(Type evType, BaseEntity entity, string evModifiedPropertyName,
            string value)
        {
            var propertyInfo = evType.GetProperty(evModifiedPropertyName);
            var setter = propertyInfo.GetSetMethod();
            var del = setter.CreateDelegate(typeof(Action<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));

            var newValue = JsonConvert.DeserializeObject(value, propertyInfo.PropertyType);
            
            del.DynamicInvoke(entity, newValue);
        }

        public void Track(ITrackable trackable, bool isReadonly)
        {
            _underlyingStorage.Track(this, isReadonly);
            trackable.ModelChanged += ObjectRepositoryOnModelChanged;
        }

        private void ObjectRepositoryOnModelChanged(ModelChangedEventArgs obj) => ModelChanged(ModelChangedEventArgs.Added(new EventModel(obj)));

        public event Action<Exception> OnError;

        public event Action<ModelChangedEventArgs> ModelChanged;
    }
}