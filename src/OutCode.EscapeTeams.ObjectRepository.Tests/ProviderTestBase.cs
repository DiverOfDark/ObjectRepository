using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public abstract class ProviderTestBase
    {
        protected readonly TestModel _testModel;
        protected readonly ParentModel _parentModel;
        protected readonly ChildModel _childModel;

        public ProviderTestBase()
        {
            _testModel = new TestModel();
            _parentModel = new ParentModel();
            _childModel = new ChildModel(_parentModel);
        }

        protected abstract ObjectRepositoryBase CreateRepository();

        protected abstract IStorage GetStorage(ObjectRepositoryBase objectRepository);

        [TestMethod]
        public void TestThatAfterRestartWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.SaveChanges();
            
            var storage = GetStorage(objectRepo);

            storage.SaveChanges().GetAwaiter().GetResult();

            objectRepo = CreateRepository();
            
            Assert.AreEqual(objectRepo.Set<TestModel>().Count(), 1);
            Assert.AreEqual(objectRepo.Set<ParentModel>().Count(), 1);
            Assert.AreEqual(objectRepo.Set<ChildModel>().Count(), 1);

            Assert.AreEqual(objectRepo.Set<TestModel>().First().Id, _testModel.TestId);
            Assert.AreEqual(objectRepo.Set<ParentModel>().First().Id, _parentModel.TestId);
            Assert.AreEqual(objectRepo.Set<ChildModel>().First().Id, _childModel.TestId);
            Assert.AreEqual(objectRepo.Set<ChildModel>().First().ParentId, _parentModel.Id);

        }
        
        [TestMethod]
        public void TestThatAddWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.SaveChanges();
            
            var storage = GetStorage(objectRepo);

            storage.SaveChanges().GetAwaiter().GetResult();

            var testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();
            var parentsStored = storage.GetAll<ParentEntity>().GetAwaiter().GetResult().ToList();
            var childStored = storage.GetAll<ChildEntity>().GetAwaiter().GetResult().ToList();

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
            var storage = GetStorage(objectRepo);
            storage.SaveChanges().GetAwaiter().GetResult();

            var testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();
            var parentsStored = storage.GetAll<ParentEntity>().GetAwaiter().GetResult().ToList();
            var childStored = storage.GetAll<ChildEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count(), 0);
            Assert.AreEqual(parentsStored.Count(), 1);
            Assert.AreEqual(childStored.Count(), 0);

            Assert.AreEqual(parentsStored.First().Id, _parentModel.TestId);
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
                _entity = new TestEntity {Id = Guid.NewGuid()};
            }

            public string Property
            {
                get => _entity.Property;
                set => UpdateProperty(() => _entity.Property, value);
            }

            public Guid TestId
            {
                get => _entity.Id;
                set => UpdateProperty(() => _entity.Id, value);
            }

            protected override BaseEntity Entity => _entity;
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
                _entity = new ParentEntity {Id = Guid.NewGuid()};
            }

            public Guid? NullableId
            {
                get => _entity.NullableId;
                set => UpdateProperty(() => _entity.NullableId, value);
            }

            public Guid TestId
            {
                get => _entity.Id;
                set => UpdateProperty(() => _entity.Id, value);
            }

            public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
            public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(x => x.NullableTestId);

            protected override BaseEntity Entity => _entity;
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
                _entity = new ChildEntity {Id = Guid.NewGuid()};
                if (parent != null)
                {
                    _entity.ParentId = parent.Id;
                }
            }

            public Guid TestId
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

            protected override BaseEntity Entity => _entity;
        }
        
        [TestMethod]
        public void TestThatUpdateWorks()
        {
            var objectRepo = CreateRepository();

            objectRepo.Remove(_testModel);
            var newTestModel = new TestModel {Property = "123"};

            objectRepo.Add(newTestModel);

            objectRepo.SaveChanges();
            var storage = GetStorage(objectRepo);
            storage.SaveChanges().GetAwaiter().GetResult();

            var testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);
            Assert.AreEqual(testsStored.First().Property, "123");

            newTestModel.Property = "234";

            objectRepo.SaveChanges();
            storage.SaveChanges().GetAwaiter().GetResult();
            testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

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
            var storage = GetStorage(objectRepo);
            storage.SaveChanges().GetAwaiter().GetResult();

            var testsStored = storage.GetAll<TestEntity>().GetAwaiter().GetResult().ToList();

            Assert.AreEqual(testsStored.Count, 1);
            Assert.AreEqual(testsStored.First().Id, _testModel.TestId);
        }
    }
}