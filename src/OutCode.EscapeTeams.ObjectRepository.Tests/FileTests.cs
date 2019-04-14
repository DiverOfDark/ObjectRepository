using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OutCode.EscapeTeams.ObjectRepository.File;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass]
    public class FileTests : ProviderTestBase
    {
        private readonly String _filename = Path.GetTempFileName();
        private bool _firstTime = true;

        protected override ObjectRepositoryBase CreateRepository()
        {
            var objectRepo = new FileTestObjectRepository(new FileStorage(_filename));
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
            return ((FileTestObjectRepository) objectRepository).FileStorage;
        }
        
        internal class FileTestObjectRepository : ObjectRepositoryBase
        {
            public FileTestObjectRepository(FileStorage dbLiteStorage) : base(dbLiteStorage, NullLogger.Instance)
            {
                FileStorage = dbLiteStorage;
                AddType((TestEntity x) => new TestModel(x));
                AddType((ParentEntity x) => new ParentModel(x));
                AddType((ChildEntity x) => new ChildModel(x));
                Initialize();
            }

            public FileStorage FileStorage { get; }
        }
    }
}