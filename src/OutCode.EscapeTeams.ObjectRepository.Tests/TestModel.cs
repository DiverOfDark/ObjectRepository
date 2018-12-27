using System;
using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class TestEntity : BaseEntity
    {
    }
    
    public class TestModel : ModelBase
    {
        public Guid TestId { get; set; } = Guid.NewGuid();

        protected override BaseEntity Entity { get; } = new TestEntity();
    }

    public class ParentModel : ModelBase
    {
        public Guid? NullableId => null;

        public Guid TestId { get; set; } = Guid.NewGuid();

        public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
        public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(x => x.NullableTestId);

        protected override BaseEntity Entity { get; } = new TestEntity();
    }

    public class ChildModel : ModelBase
    {
        public Guid TestId { get; set; } = Guid.NewGuid();
        public Guid? NullableTestId => null;

        public Guid ParentId { get; set; }

        public ParentModel Parent => Single<ParentModel>(ParentId);
        public ParentModel ParentOptional => Single<ParentModel>(NullableTestId);

        protected override BaseEntity Entity { get; } = new TestEntity();
    }
}