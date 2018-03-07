using System;
using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestModel : ModelBase
    {
        public override Guid Id => TestId;

        public Guid TestId { get; set; } = Guid.NewGuid();

        protected override object Entity => this;
    }

    public class ParentModel : ModelBase
    {
        public override Guid Id => TestId;
        public Guid? NullableId => null;

        public Guid TestId { get; set; } = Guid.NewGuid();

        public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
        public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(x => x.NullableTestId);

        protected override object Entity => this;
    }

    public class ChildModel : ModelBase
    {
        public override Guid Id => TestId;

        public Guid TestId { get; set; } = Guid.NewGuid();
        public Guid? NullableTestId => null;

        public Guid ParentId { get; set; }

        public ParentModel Parent => Single<ParentModel>(ParentId);
        public ParentModel ParentOptional => Single<ParentModel>(NullableTestId);

        protected override object Entity => this;
    }
}