using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public interface ITableDictionary<out T> : IEnumerable<T>
    {
    }
}