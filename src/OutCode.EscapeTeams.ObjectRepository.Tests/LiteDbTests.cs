using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OutCode.EscapeTeams.ObjectRepository.LiteDB;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass]
    public class LiteDbTests
    {
        private readonly TestModel _testModel;
        private readonly ParentModel _parentModel;
        private readonly ChildModel _childModel;

        public LiteDbTests()
        {
            _testModel = new TestModel();
            _parentModel = new ParentModel();
            _childModel = new ChildModel(_parentModel);
        }

        private LiteDbTestObjectRepository CreateRepository()
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

        [TestMethod]
        public void TestThatAddWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.SaveChanges();
            objectRepo.LiteStorage.SaveChanges();

            var testsStored = objectRepo.LiteStorage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();
            var parentsStored = objectRepo.LiteStorage.GetAll<ParentEntity>().GetAwaiter().GetResult().ToList();
            var childStored = objectRepo.LiteStorage.GetAll<ChildEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count(), 1);
            Assert.AreEqual(parentsStored.Count(), 1);
            Assert.AreEqual(childStored.Count(), 1);

            Assert.AreEqual(testsStored.First().Id, _testModel.TestId);
            Assert.AreEqual(parentsStored.First().Id, _parentModel.TestId);
            Assert.AreEqual(childStored.First().Id, _childModel.TestId);
            Assert.AreEqual(childStored.First().ParentId, _parentModel.Id);
        }

        [TestMethod]
        public void TestThatRemoveWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.Remove(_testModel);
            objectRepo.Remove(_childModel);

            objectRepo.SaveChanges();
            objectRepo.LiteStorage.SaveChanges();

            var testsStored = objectRepo.LiteStorage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();
            var parentsStored = objectRepo.LiteStorage.GetAll<ParentEntity>().GetAwaiter().GetResult().ToList();
            var childStored = objectRepo.LiteStorage.GetAll<ChildEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count(), 0);
            Assert.AreEqual(parentsStored.Count(), 1);
            Assert.AreEqual(childStored.Count(), 0);

            Assert.AreEqual(parentsStored.First().Id, _parentModel.TestId);
        }

        [TestMethod]
        public void TestThatUpdateWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.Remove(_testModel);
            var newTestModel = new TestModel {Property = "123"};

            objectRepo.Add(newTestModel);

            objectRepo.SaveChanges();
            objectRepo.LiteStorage.SaveChanges();

            var testsStored = objectRepo.LiteStorage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);
            Assert.AreEqual(testsStored.First().Property, "123");

            newTestModel.Property = "234";

            objectRepo.SaveChanges();
            objectRepo.LiteStorage.SaveChanges();
            testsStored = objectRepo.LiteStorage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);
            Assert.AreEqual(testsStored.First().Property, "234");
        }

        [TestMethod]
        public void TestThatFastAddRemoveNotBreaks()
        {
            var objectRepo = CreateRepository();

            var newTestModel = new TestModel();

            objectRepo.Add(newTestModel);
            newTestModel.Property = "123";
            objectRepo.Remove(newTestModel);

            objectRepo.SaveChanges();
            objectRepo.LiteStorage.SaveChanges();

            var testsStored = objectRepo.LiteStorage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);
            Assert.AreEqual(testsStored.First().Id, _testModel.TestId);
        }

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

        public class TestEntity : BaseEntity
        {
            public string Property { get; set; }
        }

        public class ParentEntity : BaseEntity
        {
            public Guid? NullableId { get; set; }
        }

        public class ChildEntity : BaseEntity
        {
            public Guid ParentId { get; set; }
            public Guid? NullableId { get; set; }
        }

        public class TestModel : ModelBase
        {
            private readonly TestEntity _entity;

            public TestModel(TestEntity entity)
            {
                _entity = entity;
            }

            public TestModel()
            {
                _entity = new TestEntity {Id = ObjectId.NewObjectId()};
            }

            public override Guid Id => _entity.Id.ToGuid();

            public string Property
            {
                get => _entity.Property;
                set => UpdateProperty(() => _entity.Property, value);
            }

            public ObjectId TestId
            {
                get => _entity.Id;
                set => UpdateProperty(() => _entity.Id, value);
            }

            protected override object Entity => _entity;
        }

        public class ParentModel : ModelBase
        {
            private readonly ParentEntity _entity;

            public ParentModel(ParentEntity entity)
            {
                _entity = entity;
            }

            public ParentModel()
            {
                _entity = new ParentEntity {Id = ObjectId.NewObjectId()};
            }

            public override Guid Id => _entity.Id.ToGuid();

            public Guid? NullableId
            {
                get => _entity.NullableId;
                set => UpdateProperty(() => _entity.NullableId, value);
            }

            public ObjectId TestId
            {
                get => _entity.Id;
                set => UpdateProperty(() => _entity.Id, value);
            }

            public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
            public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(x => x.NullableTestId);

            protected override object Entity => _entity;
        }

        public class ChildModel : ModelBase
        {
            private readonly ChildEntity _entity;

            public ChildModel(ChildEntity entity)
            {
                _entity = entity;
            }

            public ChildModel(ParentModel parent)
            {
                _entity = new ChildEntity {Id = ObjectId.NewObjectId()};
                if (parent != null)
                {
                    _entity.ParentId = parent.Id;
                }
            }

            public override Guid Id => _entity.Id.ToGuid();

            public ObjectId TestId
            {
                get => _entity.Id;
                set => UpdateProperty(() => _entity.Id, value);
            }

            public Guid? NullableTestId
            {
                get => _entity.NullableId;
                set => UpdateProperty(() => _entity.NullableId, value);
            }

            public Guid ParentId
            {
                get => _entity.ParentId;
                set => UpdateProperty(() => _entity.ParentId, value);
            }

            public ParentModel Parent => Single<ParentModel>(ParentId);
            public ParentModel ParentOptional => Single<ParentModel>(NullableTestId);

            protected override object Entity => _entity;
        }
    }
}