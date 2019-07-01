using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class StateModel : ModelBase
    {
        internal class StateEntity : BaseEntity
        {
            public Guid JobId { get; set; }
            public string Name { get; set; }
            public string Reason { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Data { get; set; }
        }

        private readonly StateEntity _state;

        public StateModel(StateEntity state)
        {
            _state = state;
        }

        public StateModel()
        {
            _state = new StateEntity
            {
                Id = Guid.NewGuid()
            };
        }

        protected override BaseEntity Entity => _state;

        public DateTime CreatedAt
        {
            get => _state.CreatedAt;
            set => UpdateProperty(() => () => _state.CreatedAt, value);
        }

        public Guid JobId
        {
            get => _state.JobId;
            set => UpdateProperty(() => () => _state.JobId, value);
        }

        public JobModel Job => Single<JobModel>(JobId);
        
        public string Name
        {
            get => _state.Name;
            set => UpdateProperty(() => () => _state.Name, value);
        }

        public string Reason
        {
            get => _state.Reason;
            set => UpdateProperty(() => () => _state.Reason, value);
        }
        
        public string Data
        {
            get => _state.Data;
            set => UpdateProperty(() => () => _state.Data, value);
        }
    }    
}