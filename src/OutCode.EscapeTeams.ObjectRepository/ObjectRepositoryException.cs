using System;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public class ObjectRepositoryException<T> : Exception
    {
        public ObjectRepositoryException(Guid? id, string callingProperty, string where):base($"bad ID {id} for type {typeof(T).FullName} at {callingProperty} ({where})")
        {
            
        }
    }
}