using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    [TestClass]
    public class ObjectRepositoryTests
    {
        [TestMethod]
        public void CtorShouldNotThrowException()
        {
            // Given
            var instance = new TestObjectRepository(new TestStorage());

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            // Then
            // no exceptions
        }

        [TestMethod]
        public void TestThatRelationsWorks()
        {
            // Given
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            // Then
            // no exceptions

            var parentModel = instance.Set<ParentModel>().Single();
            var childModel = instance.Set<ChildModel>().Single();
            Assert.AreEqual(parentModel.Children.Single(), childModel);
            Assert.AreEqual(parentModel.OptionalChildren.Count(), 0);
            Assert.AreEqual(childModel.Parent, parentModel);
            Assert.AreEqual(childModel.ParentOptional, null);
        }

        [TestMethod]
        public void TestThatDeletingChildrenDoesntBreaks()
        {
            // Given
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();
            instance.Remove<ChildModel>(v => true);
            
            // Then
            // no exceptions
            var parentModel = instance.Set<ParentModel>().Single();
            var childModel = instance.Set<ChildModel>().ToArray();
            Assert.AreEqual(parentModel.Children.Count(), 0);
            Assert.AreEqual(parentModel.OptionalChildren.Count(), 0);
            Assert.AreEqual(childModel.Length, 0);
        }

        [TestMethod]
        public void TestThatFindWorks()
        {
            // Given
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            var set = instance.Set<ParentModel>();
            
            Assert.AreEqual(set.Find(id), set.Single());
            Assert.AreEqual(set.Find(Guid.Empty), null);
        }

        [TestMethod]
        public void TestThatCustomIndexesWorks()
        {
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "1"},
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "2"}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            instance.Set<ChildModel>().AddIndex(() => x => x.Property);
            var child = instance.Set<ChildModel>().Find(() => x => x.Property, "2");

            Assert.IsNotNull(child);
            Assert.AreEqual(child.Property, "2");
        }

        [TestMethod]
        public void TestThatCustomIndexesWorksAfterPropertyChange()
        {
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "1"},
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "2"}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            instance.Set<ChildModel>().AddIndex(() => x => x.Property);
            var child = instance.Set<ChildModel>().Find(() => x => x.Property, "2");

            Assert.IsNotNull(child);
            Assert.AreEqual(child.Property, "2");

            child.Property = "3";

            Assert.IsNull(instance.Set<ChildModel>().Find(() => x => x.Property, "2"));

            Assert.AreEqual(child, instance.Set<ChildModel>().Find(() => x => x.Property, "3"));
        }

        [TestMethod]
        public void TestThatPropertyUpdaterNotLeaks()
        {
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "1"},
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "2"}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();

            instance.Set<ChildModel>().AddIndex(() => x => x.Property);
            var child = instance.Set<ChildModel>().Find(() => x => x.Property, "2");

            Assert.IsNotNull(child);
            Assert.AreEqual(child.Property, "2");

            child.Property = "3";

            var count = ModelBase.PropertyUpdater<ChildEntity, string>.Cache.Count;
            
            child.Property = "2";

            var newCount = ModelBase.PropertyUpdater<ChildEntity, string>.Cache.Count;
            
            Assert.AreEqual(count, newCount, "Memory leak!");
        }

        [TestMethod]
        public void TestThatNoNotifyWhenValueNotChanged()
        {
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ChildEntity(Guid.NewGuid()) {ParentId = id, Property = "2"}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            var child = instance.Set<ChildModel>().First();

            Assert.IsNotNull(child);
            var shouldFail = false;
            instance.ModelChanged += (a) =>
            {
                if (shouldFail)
                    throw new Exception();
            };
            
            child.Property = "3";
            shouldFail = true;
            child.Property = "3";
            shouldFail = false;
            child.Property = "2";
        }

        [TestMethod, Ignore("TODO finds out how to find which property on which object needs to be reset when such happens.")]
        public void TestThatDeletingParentDoesntBreaks()
        {
            // Given
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentEntity(id),
                new ChildEntity(Guid.NewGuid()) {ParentId = id}
            };

            var instance = new TestObjectRepository(testStorage);

            // When
            instance.WaitForInitialize().GetAwaiter().GetResult();
            instance.Remove<ParentModel>(v => true);
            
            // Then
            // no exceptions
            var parentModel = instance.Set<ParentModel>().ToArray();
            var childModel = instance.Set<ChildModel>().Single();
            Assert.AreEqual(childModel.Parent, null);
            Assert.AreEqual(childModel.ParentOptional, null);
            Assert.AreEqual(parentModel.Length, 0);
        }
    }
}
