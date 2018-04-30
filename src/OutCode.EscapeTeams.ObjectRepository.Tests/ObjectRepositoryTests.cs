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
                new ParentModel {TestId = id},
                new ChildModel {ParentId = id}
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
                new ParentModel {TestId = id},
                new ChildModel {ParentId = id}
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
        
        [TestMethod, Ignore("TODO finds out how to find which property on which object needs to be reset when such happens.")]
        public void TestThatDeletingParentDoesntBreaks()
        {
            // Given
            var id = Guid.NewGuid();
            var testStorage = new TestStorage
            {
                new ParentModel {TestId = id},
                new ChildModel {ParentId = id}
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
