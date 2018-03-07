using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestObjectRepository : ObjectRepositoryBase
    {
        public TestObjectRepository(TestStorage storage) : base(storage, NullLogger.Instance)
        {
            AddType((TestModel x) => x);
            AddType((ParentModel x) => x);
            AddType((ChildModel x) => x);
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