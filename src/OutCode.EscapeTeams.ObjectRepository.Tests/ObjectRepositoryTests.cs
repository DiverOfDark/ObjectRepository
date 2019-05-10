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
            instance.WaitForLoad();

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
            instance.WaitForLoad();

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
            instance.WaitForLoad();
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
            instance.WaitForLoad();

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
            instance.WaitForLoad();

            instance.Set<ChildModel>().AddIndex(x => x.Property);
            var child = instance.Set<ChildModel>().Find(x=>x.Property, "2");
            
            Assert.IsNotNull(child);
            Assert.AreEqual(child.Property, "2");
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
            instance.WaitForLoad();
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
