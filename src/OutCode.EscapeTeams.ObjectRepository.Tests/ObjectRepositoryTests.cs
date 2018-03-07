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
            var testStorage = new TestStorage()
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
    }
}
