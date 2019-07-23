using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class JobParameterModel : ModelBase
    {
        internal class JobParameterEntity : BaseEntity
        {
            public Guid JobId { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private readonly JobParameterEntity _jobParameter;

        public JobParameterModel(JobParameterEntity jobParameter)
        {
            _jobParameter = jobParameter;
        }

        public JobParameterModel(Guid jobId, string name)
        {
            _jobParameter = new JobParameterEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Name = name
            };
        }

        protected override BaseEntity Entity => _jobParameter;
        
        public Guid JobId
        {
            get => _jobParameter.JobId;
            set => UpdateProperty(_jobParameter, () => x => x.JobId, value);
        }

        public string Name
        {
            get => _jobParameter.Name;
            set => UpdateProperty(_jobParameter, () => x => x.Name, value);
        }

        public string Value
        {
            get => _jobParameter.Value;
            set => UpdateProperty(_jobParameter, () => x => x.Value, value);
        }
    }
}
