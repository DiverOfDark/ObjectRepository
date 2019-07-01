using System;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire.Entities
{
    internal class ServerModel : ModelBase
    {
        internal class ServerEntity : BaseEntity
        {
            public string Data { get; set; }
            public DateTime LastHeartbeat { get; set; }
            public string Name { get; set; }
        }

        private readonly ServerEntity _server;

        public ServerModel(ServerEntity server)
        {
            _server = server;
        }

        public ServerModel(string serverId)
        {
            _server = new ServerEntity
            {
                Id = Guid.NewGuid(),
                Name = serverId
            };
        }

        protected override BaseEntity Entity => _server;

        public string Data
        {
            get => _server.Data;
            set => UpdateProperty(() => () => _server.Data, value);
        }

        public DateTime LastHeartbeat
        {
            get => _server.LastHeartbeat;
            set => UpdateProperty(() => () => _server.LastHeartbeat, value);
        }

        public string Name
        {
            get => _server.Name;
            set => UpdateProperty(() => () => _server.Name, value);
        }
    }
}
