using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OutCode.EscapeTeams.ObjectRepository.EventStore;
using OutCode.EscapeTeams.ObjectRepository.File;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass]
    public class FileEventStoreTests : ProviderTestBase
    {
        private readonly String _filename = Path.GetTempFileName();
        private bool _firstTime = true;

        protected override ObjectRepositoryBase CreateRepository()
        {
            var fileStorage = new FileStorage(_filename);
            var storage = new EventStorage(fileStorage);
            storage.CompactThreshold = TimeSpan.Zero;
            var objectRepo = new EventStoreObjectRepository(storage, fileStorage);
            objectRepo.OnException += ex => Console.WriteLine(ex.ToString());
            while (objectRepo.IsLoading)
            {
                Thread.Sleep(50);
            }

            if (_firstTime)
            {
                _firstTime = false;

                objectRepo.Add(_testModel);
                objectRepo.Add(_parentModel);
                objectRepo.Add(_childModel);
            }

            return objectRepo;
        }

        [TestMethod]
        public void TestThatPropertyChangeStoredAsEvent()
        {
            var objectRepo = CreateRepository();

            var storage = GetStorage(objectRepo);

            storage.SaveChanges().GetAwaiter().GetResult();

            var testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);

            objectRepo.Set<TestModel>().Single().Property = "update";
            storage.SaveChanges().GetAwaiter().GetResult();

            var eventsStored = GetFileStorage(objectRepo).GetAll<EventEntity>().GetAwaiter().GetResult();
            var lastEvent = eventsStored.OrderByDescending(v=>v.Timestamp).First();
            Assert.AreEqual(lastEvent.Action, ChangeType.Update);
            Assert.AreEqual(lastEvent.ModifiedPropertyName, nameof(TestModel.Property));
            Assert.AreEqual(lastEvent.ModifiedPropertyValue, "\"update\"");
        }

        [TestMethod]
        public void TestThatAddRemoveAfterCompactRemovesIntermediateEvents()
        {
            var objectRepo = CreateRepository();

            var storage = GetStorage(objectRepo);

            storage.SaveChanges().GetAwaiter().GetResult();

            var id = objectRepo.Set<TestModel>().Single().Id;
            objectRepo.Set<TestModel>().Single().Property = "update";
            objectRepo.Remove<TestModel>(v => true);
            storage.SaveChanges().GetAwaiter().GetResult();

            var eventsStored = GetFileStorage(objectRepo).GetAll<EventEntity>().GetAwaiter().GetResult();
            var lastEvents = eventsStored.OrderByDescending(v=>v.Timestamp).Take(2).ToList();
            var updateEvent = lastEvents.Last();
            Assert.AreEqual(updateEvent.Action, ChangeType.Update);
            Assert.AreEqual(updateEvent.ModifiedPropertyName, nameof(TestModel.Property));
            Assert.AreEqual(updateEvent.ModifiedPropertyValue, "\"update\"");
            var deleteEvent = lastEvents.First();
            Assert.AreEqual(deleteEvent.Action, ChangeType.Remove);
            Assert.AreEqual(deleteEvent.Entity, "{\"Property\":\"update\",\"Id\":\"" + id + "\"}");
            
            (storage as EventStorage).Compact().GetAwaiter().GetResult();
            storage.SaveChanges().GetAwaiter().GetResult();

            var newEvents = GetFileStorage(objectRepo).GetAll<EventEntity>().GetAwaiter().GetResult();
            
            Assert.AreEqual(newEvents.Count() + 3, eventsStored.Count());
            Assert.AreEqual(newEvents.Contains(updateEvent), false);
            Assert.AreEqual(newEvents.Contains(deleteEvent), false);
        }

        [TestMethod]
        public void TestThatAddUpdateAfterCompactRemovesUpdateEvents()
        {
            var objectRepo = CreateRepository();

            var storage = GetStorage(objectRepo);

            storage.SaveChanges().GetAwaiter().GetResult();

            objectRepo.Set<TestModel>().Single().Property = "update";
            storage.SaveChanges().GetAwaiter().GetResult();

            var eventsStored = GetFileStorage(objectRepo).GetAll<EventEntity>().GetAwaiter().GetResult();
            var updateEvent = eventsStored.OrderByDescending(v=>v.Timestamp).First();
            Assert.AreEqual(updateEvent.Action, ChangeType.Update);
            Assert.AreEqual(updateEvent.ModifiedPropertyName, nameof(TestModel.Property));
            Assert.AreEqual(updateEvent.ModifiedPropertyValue, "\"update\"");
            
            (storage as EventStorage).Compact().GetAwaiter().GetResult();
            storage.SaveChanges().GetAwaiter().GetResult();

            var newEvents = GetFileStorage(objectRepo).GetAll<EventEntity>().GetAwaiter().GetResult();
            
            Assert.AreEqual(newEvents.Count() + 1, eventsStored.Count());
            Assert.AreEqual(newEvents.Contains(updateEvent), false);
        }
        
        private IStorage GetFileStorage(ObjectRepositoryBase objectRepository) => ((EventStoreObjectRepository) objectRepository).UnderlyingStorage;

        protected override IStorage GetStorage(ObjectRepositoryBase objectRepository) => ((EventStoreObjectRepository) objectRepository).EventStorage;

        internal class EventStoreObjectRepository : ObjectRepositoryBase
        {
            public EventStoreObjectRepository(EventStorage eventStore, IStorage underlyingStorage) : base(eventStore, NullLogger.Instance)
            {
                EventStorage = eventStore;
                UnderlyingStorage = underlyingStorage;
                AddType((TestEntity x) => new TestModel(x));
                AddType((ParentEntity x) => new ParentModel(x));
                AddType((ChildEntity x) => new ChildModel(x));
                Initialize();
            }

            public IStorage UnderlyingStorage { get; }
            
            public EventStorage EventStorage { get; }
        }
    }
}