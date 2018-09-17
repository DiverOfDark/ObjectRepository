using System;
using LiteDB;

namespace OutCode.EscapeTeams.ObjectRepository.LiteDB
{
    public static class ObjectIdExtensions
    {
        public static Guid ToGuid(this ObjectId id)
        {
            var byteArray = id.ToByteArray();
            Array.Resize(ref byteArray, 16);
            return new Guid(byteArray);
        }
    }
}