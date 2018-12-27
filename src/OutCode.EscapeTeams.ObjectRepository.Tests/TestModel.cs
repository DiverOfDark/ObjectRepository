using System;
using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository.Tests
{
    public class ParentEntity : BaseEntity
    {
        public ParentEntity(Guid id) => Id = id;
    }
    
    public class ChildEntity : BaseEntity
    {
        public ChildEntity(Guid id) => Id = id;
        public Guid ParentId { get; set; }
    }
    
    public class ParentModel : ModelBase
    {
        public ParentModel(ParentEntity entity)
        {
            Entity = entity;
        }

        public Guid? NullableId => null;

        public IEnumerable<ChildModel> Children => Multiple<ChildModel>(x => x.ParentId);
        public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(x => x.NullableTestId);

        protected override BaseEntity Entity { get; }
    }

    public class ChildModel : ModelBase
    {
        public ChildModel(ChildEntity entity)
        {
            Entity = entity;
        }

        public Guid? NullableTestId => null;

        public Guid ParentId
        {
            get => ((ChildEntity) Entity).ParentId;
            set => UpdateProperty(() => ((ChildEntity) Entity).ParentId, value);
        }

        public ParentModel Parent => Single<ParentModel>(ParentId);
        public ParentModel ParentOptional => Single<ParentModel>(NullableTestId);

        protected override BaseEntity Entity { get; }
    }
}