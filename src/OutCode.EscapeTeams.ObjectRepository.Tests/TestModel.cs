using System;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestModel : ModelBase
    {
        public override Guid Id => TestId;

        public Guid TestId { get; set; } = Guid.NewGuid();

        protected override object Entity => this;
    }
}