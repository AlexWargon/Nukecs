using System;
using System.Text;

namespace Wargon.Nukecs
{
#pragma warning disable CS0168
    public static class TypeExtensions
    {
        public static string GetGenericName(this Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            var sb = new StringBuilder();

            // убираем `1, `2 и т.д.
            var name = type.Name;
            var index = name.IndexOf('`');
            if (index > 0)
                name = name.Substring(0, index);

            sb.Append(name);
            sb.Append('<');

            var args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                sb.Append(args[i].GetGenericName()); // рекурсия

                if (i < args.Length - 1)
                    sb.Append(", ");
            }

            sb.Append('>');

            return sb.ToString();
        }
    }
}