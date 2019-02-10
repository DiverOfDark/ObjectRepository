using System;
using System.Collections;
using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal class PersistentJobQueueProviderCollection : IEnumerable<ObjectRepositoryJobQueueProvider>
    {
        private readonly List<ObjectRepositoryJobQueueProvider> _providers
            = new List<ObjectRepositoryJobQueueProvider>();
        private readonly Dictionary<string, ObjectRepositoryJobQueueProvider> _providersByQueue
            = new Dictionary<string, ObjectRepositoryJobQueueProvider>(StringComparer.OrdinalIgnoreCase);

        private readonly ObjectRepositoryJobQueueProvider _defaultProvider;

        public PersistentJobQueueProviderCollection(ObjectRepositoryJobQueueProvider defaultProvider)
        {
            _defaultProvider = defaultProvider ?? throw new ArgumentNullException(nameof(defaultProvider));

            _providers.Add(_defaultProvider);
        }

        public void Add(ObjectRepositoryJobQueueProvider provider, IEnumerable<string> queues)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (queues == null) throw new ArgumentNullException(nameof(queues));

            _providers.Add(provider);

            foreach (var queue in queues)
            {
                _providersByQueue.Add(queue, provider);
            }
        }

        public ObjectRepositoryJobQueueProvider GetProvider(string queue)
        {
            return _providersByQueue.ContainsKey(queue)
                ? _providersByQueue[queue]
                : _defaultProvider;
        }

        public IEnumerator<ObjectRepositoryJobQueueProvider> GetEnumerator()
        {
            return _providers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}