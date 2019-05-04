using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace OutCode.EscapeTeams.ObjectRepository.EventStore
{
    public class EventStorage : IStorage, ITrackable
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<String, Tuple<Type, Delegate>>> _settersCache = 
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, Tuple<Type, Delegate>>>();

        private readonly IStorage _underlyingStorage;

        public EventStorage(IStorage underlyingStorage)
        {
            _underlyingStorage = underlyingStorage;
        }

        public TimeSpan CompactThreshold { get; set; } = TimeSpan.FromDays(1);

        public Task SaveChanges() => _underlyingStorage.SaveChanges();

        public async Task<IEnumerable<T>> GetAll<T>() where T:BaseEntity
        {
            var items = new List<T>();
            var events = await _underlyingStorage.GetAll<EventEntity>();
            var eventModels = events.Select(v => new EventModel(v)).OrderBy(v => v.Timestamp).ToList();
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
            var setters = _settersCache.GetOrAdd(evType, new ConcurrentDictionary<string, Tuple<Type, Delegate>>());
            var propertyMethod = setters.GetOrAdd(evModifiedPropertyName, prop =>
            {
                var propertyInfo = evType.GetProperty(evModifiedPropertyName);
                var setter = propertyInfo.GetSetMethod();
                var del = setter.CreateDelegate(typeof(Action<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));
                return Tuple.Create(propertyInfo.PropertyType, del);
            });
            
            var newValue = JsonConvert.DeserializeObject(value, propertyMethod.Item1);
            propertyMethod.Item2.DynamicInvoke(entity, newValue);
        }

        public async Task Compact()
        {
            var entities = await _underlyingStorage.GetAll<EventEntity>();
            var events = entities.OrderBy(v => v.Timestamp).Where(v=>v.Timestamp < DateTime.UtcNow - CompactThreshold).ToList();

            var dict = new ConcurrentDictionary<String, Dictionary<string, EventEntity>>();

            foreach (var ev in events)
            {
                var state = dict.GetOrAdd(ev.Type, new Dictionary<string, EventEntity>());

                var jobj = JObject.Parse(ev.Entity);

                var id = jobj[nameof(BaseEntity.Id)].Value<string>();
                
                if (ev.Action == ChangeType.Add)
                {
                    state[id] = ev;
                }

                if (ev.Action == ChangeType.Update)
                {
                    var oldValue = state[id].Entity;

                    var oldObject = JObject.Parse(state[id].Entity);
                    oldObject[ev.ModifiedPropertyName] = JToken.Parse(ev.ModifiedPropertyValue);
                    state[id].Entity = oldObject.ToString();
                    
                    var newValue = state[id].Entity;
                    ModelChanged(ModelChangedEventArgs.PropertyChange(new EventModel(ev), nameof(ev.Entity), oldValue, newValue));
                    ModelChanged(ModelChangedEventArgs.Removed(new EventModel(ev)));
                }

                if (ev.Action == ChangeType.Remove)
                {
                    ModelChanged(ModelChangedEventArgs.Removed(new EventModel(state[id])));
                    state.Remove(id);
                    ModelChanged(ModelChangedEventArgs.Removed(new EventModel(ev)));
                }
            }
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