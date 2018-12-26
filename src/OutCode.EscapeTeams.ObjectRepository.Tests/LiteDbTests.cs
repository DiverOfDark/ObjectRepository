using System;
using System.IO;
using System.Threading;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OutCode.EscapeTeams.ObjectRepository.LiteDB;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass]
    public class LiteDbTests : ProviderTestBase
    {
        protected override ObjectRepositoryBase CreateRepository()
        {
            var db = new LiteDatabase(new MemoryStream(), disposeStream: true);

            var dbStorage = new LiteDbStorage(db);
            var objectRepo = new LiteDbTestObjectRepository(dbStorage);
            objectRepo.OnException += ex => Console.WriteLine(ex.ToString());
            while (objectRepo.IsLoading)
            {
                Thread.Sleep(50);
            }

            objectRepo.Add(_testModel);
            objectRepo.Add(_parentModel);
            objectRepo.Add(_childModel);

            return objectRepo;
        }

        protected override IStorage GetStorage(ObjectRepositoryBase objectRepository) => ((LiteDbTestObjectRepository) objectRepository).LiteStorage;

        internal class LiteDbTestObjectRepository : ObjectRepositoryBase
        {
            public LiteDbTestObjectRepository(LiteDbStorage dbLiteStorage) : base(dbLiteStorage, NullLogger.Instance)
            {
                LiteStorage = dbLiteStorage;
                AddType((TestEntity x) => new TestModel(x));
                AddType((ParentEntity x) => new ParentModel(x));
                AddType((ChildEntity x) => new ChildModel(x));
                Initialize();
            }

            public LiteDbStorage LiteStorage { get; }
        }
    }
}