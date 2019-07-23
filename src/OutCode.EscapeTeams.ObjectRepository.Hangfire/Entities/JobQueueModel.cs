using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class JobQueueModel : ModelBase
    {
        internal class JobQueueEntity : BaseEntity
        {
            public Guid JobId { get; set; }
            public string Queue { get; set; }
            public DateTime? FetchedAt { get; set; }
        }

        private readonly JobQueueEntity _jobQueue;

        public JobQueueModel(JobQueueEntity jobQueue)
        {
            _jobQueue = jobQueue;
        }

        public JobQueueModel()
        {
            _jobQueue = new JobQueueEntity
            {
                Id = Guid.NewGuid()
            };
        }

        protected override BaseEntity Entity => _jobQueue;
        
        public Guid JobId 
        {
            get => _jobQueue.JobId;
            set => UpdateProperty(_jobQueue, () => x => x.JobId, value);
        }
        public string Queue 
        {
            get => _jobQueue.Queue;
            set => UpdateProperty(_jobQueue, () => x => x.Queue, value);
        }
        public DateTime? FetchedAt 
        {
            get => _jobQueue.FetchedAt;
            set => UpdateProperty(_jobQueue, () => x => x.FetchedAt, value);
        }

        public JobModel Job => Single<JobModel>(JobId);
    }
}