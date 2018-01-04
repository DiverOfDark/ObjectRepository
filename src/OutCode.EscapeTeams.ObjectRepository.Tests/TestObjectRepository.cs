using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestObjectRepository : ObjectRepositoryBase
    {
        public TestObjectRepository() : base(new TestStorage(), NullLogger.Instance)
        {
            AddType((object x) => new TestModel());
            Initialize();
        }

        public void WaitForLoad()
        {
            while (IsLoading)
            {
                Thread.Sleep(50);
            }
        }
    }
}