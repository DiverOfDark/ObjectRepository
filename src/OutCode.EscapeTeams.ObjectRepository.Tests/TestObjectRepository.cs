using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestObjectRepository : ObjectRepositoryBase
    {
        public TestObjectRepository(IStorage storage) : base(storage, NullLogger.Instance)
        {
            AddType((ParentEntity x) => new ParentModel(x));
            AddType((ChildEntity x) => new ChildModel(x));
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