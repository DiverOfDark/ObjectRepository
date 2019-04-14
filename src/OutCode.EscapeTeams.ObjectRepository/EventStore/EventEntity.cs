using System;

namespace OutCode.EscapeTeams.ObjectRepository.EventStore
{
    internal class EventEntity : BaseEntity
    {
        public string Entity { get; set; }

        public string Type { get; set; }
        
        public string ModifiedPropertyName { get; set; }
        
        public string ModifiedPropertyValue { get; set; }

        public ChangeType Action { get; set; }

        public DateTime Timestamp { get; set; }
    }
}