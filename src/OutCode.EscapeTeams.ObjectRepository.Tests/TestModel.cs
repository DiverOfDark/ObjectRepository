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
        
        public String Property { get; set; }
    }
    
    public class ParentModel : ModelBase
    {
        public ParentModel(ParentEntity entity)
        {
            Entity = entity;
        }

        public Guid? NullableId => null;

        public IEnumerable<ChildModel> Children => Multiple<ChildModel>(() => x => x.ParentId);
        public IEnumerable<ChildModel> OptionalChildren => Multiple<ChildModel>(() => x => x.NullableTestId);

        protected internal override BaseEntity Entity { get; }
    }

    public class ChildModel : ModelBase
    {
        private readonly ChildEntity _myEntity;

        public ChildModel(ChildEntity entity)
        {
            _myEntity = entity;
        }

        public Guid? NullableTestId => null;

        public string Property
        {
            get => ((ChildEntity) Entity).Property;
            set => UpdateProperty(_myEntity, () => x => x.Property, value);
        }

        public Guid ParentId
        {
            get => ((ChildEntity) Entity).ParentId;
            set => UpdateProperty(_myEntity, () => x => x.ParentId, value);
        }

        public ParentModel Parent
        {
            get => Single<ParentModel>(ParentId);
            set => UpdateProperty(_myEntity, () => x => x.ParentId, value.Id);
        }

        public ParentModel ParentOptional => Single<ParentModel>(NullableTestId);

        protected internal override BaseEntity Entity => _myEntity;
    }
}