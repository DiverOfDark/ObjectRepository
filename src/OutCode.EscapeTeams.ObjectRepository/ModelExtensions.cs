using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public static class ModelExtensions
    {
        public static string GetPropertiesAsRawData(this object obj)
        {
            if (obj == null)
            {
                return "<null>";
            }

            if (obj.GetType().GetTypeInfo().IsPrimitive || obj is string)
            {
                return obj.ToString();
            }

            var sb = new StringBuilder();

            foreach (var item in obj.GetType().GetProperties().Where(v => v.PropertyType.GetTypeInfo().IsPrimitive || v.PropertyType == typeof(string)))
                sb.Append("<br/><b>" + item.Name + ":</b> " + (item.GetValue(obj) ?? ""));

            foreach (var item in obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                sb.Append("<br/>Method <b>" + item.Name + "</b>(" +
                          string.Join(", ", item.GetParameters().Select(v => v.ParameterType.FullName + " " + v.Name)) +
                          ")");
            }

            return sb.ToString();
        }

        public static TValue GetOrDefault<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        public static TableDictionary<T> ToConcurrentTable<T>(this IEnumerable<T> source, ObjectRepositoryBase owner) where T : ModelBase => new TableDictionary<T>(owner, source);

        public static string ToMD5(this string str)
        {
            using (var md5 = MD5.Create())
            {
                var srcBytes = Encoding.UTF8.GetBytes(str ?? string.Empty);
                var encodedBytes = md5.ComputeHash(srcBytes);

                var sb = new StringBuilder();
                foreach (var b in encodedBytes)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }

        public static string[] SplitLongString(this string str)
        {
            // Azure Table Storage cannot handle a string longer than 64 kb
            // Therefore in UTF-16 it is 32k symbols

            const int PieceSize = 32*1024;

            if (str == null)
                return new string[0];

            var pieceCount = (int) Math.Ceiling((double) str.Length/PieceSize);
            var pieces = new string[pieceCount];

            for (var i = 0; i < pieceCount; i++)
            {
                var start = i*PieceSize;
                var length = PieceSize;
                if (start + length > str.Length)
                    length = str.Length - start;

                pieces[i] = str.Substring(start, length);
            }

            return pieces;
        }
    }
}