using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using OutCode.EscapeTeams.ObjectRepository.AzureTableStorage;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass, Ignore]
    public class AzureStorageTests : ProviderTestBase
    {
        protected override ObjectRepositoryBase CreateRepository()
        {
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            var client = account.CreateCloudTableClient();
            var storage = new AzureTableContext(client);

            client.GetTableReference("ChildEntity").DeleteIfExistsAsync().GetAwaiter().GetResult();
            client.GetTableReference("ParentEntity").DeleteIfExistsAsync().GetAwaiter().GetResult();
            client.GetTableReference("TestEntity").DeleteIfExistsAsync().GetAwaiter().GetResult();
            
            var objectRepo = new AzureObjectRepository(storage);
            
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

        protected override IStorage GetStorage(ObjectRepositoryBase objectRepository) => ((AzureObjectRepository) objectRepository).AzureTableContext;

        internal class AzureObjectRepository : ObjectRepositoryBase
        {
            public AzureObjectRepository(AzureTableContext dbAzureTableContext) : base(dbAzureTableContext, NullLogger.Instance)
            {
                IsReadOnly = true;
                AzureTableContext = dbAzureTableContext;
                AddType((TestEntity x) => new TestModel(x));
                AddType((ParentEntity x) => new ParentModel(x));
                AddType((ChildEntity x) => new ChildModel(x));
                Initialize();
            }

            public AzureTableContext AzureTableContext { get; }
        }
    }
}