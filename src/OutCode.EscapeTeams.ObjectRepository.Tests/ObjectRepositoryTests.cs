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
            var instance = new TestObjectRepository();

            // When
            instance.WaitForLoad();

            // Then
            // no exceptions
        }
    }
}
