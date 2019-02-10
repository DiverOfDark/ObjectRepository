using System;
using System.Collections.Generic;

namespace OutCode.EscapeTeams.ObjectRepository.Hangfire
{
    internal static class CollectionHelpers
    {
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection)
                action(item);
        }
    }
}