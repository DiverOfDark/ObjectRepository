using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class JobModel : ModelBase
    {
        internal class JobEntity : BaseEntity
        {
            public Guid StateId { get; set; }
            public string InvocationData { get; set; }
            public string Arguments { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? ExpireAt { get; set; }
        }

        private readonly JobEntity _job;

        public JobModel(JobEntity job)
        {
            _job = job;
        }

        public JobModel()
        {
            _job = new JobEntity
            {
                Id = Guid.NewGuid()
            };
        }

        protected override BaseEntity Entity => _job;

        public string InvocationData
        {
            get => _job.InvocationData;
            set => UpdateProperty(() => _job.InvocationData, value);
        }

        public string Arguments
        {
            get => _job.Arguments;
            set => UpdateProperty(() => _job.Arguments, value);
        }

        public DateTime CreatedAt
        {
            get => _job.CreatedAt;
            set => UpdateProperty(() => _job.CreatedAt, value);
        }

        public Guid StateId
        {
            get => _job.StateId;
            set => UpdateProperty(() => _job.StateId, value);
        }

        public DateTime? ExpireAt
        {
            get => _job.ExpireAt;
            set => UpdateProperty(() => _job.ExpireAt, value);
        }
    }
}