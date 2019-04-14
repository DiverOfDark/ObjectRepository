using System;
using System.IO;
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
            var objectRepo = new EventStoreObjectRepository(storage);
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

        protected override IStorage GetStorage(ObjectRepositoryBase objectRepository)
        {
            return ((EventStoreObjectRepository) objectRepository).FileStorage;
        }
        
        internal class EventStoreObjectRepository : ObjectRepositoryBase
        {
            public EventStoreObjectRepository (EventStorage eventStore) : base(eventStore, NullLogger.Instance)
            {
                FileStorage = eventStore;
                AddType((TestEntity x) => new TestModel(x));
                AddType((ParentEntity x) => new ParentModel(x));
                AddType((ChildEntity x) => new ChildModel(x));
                Initialize();
            }

            public EventStorage FileStorage { get; }
        }
    }
}