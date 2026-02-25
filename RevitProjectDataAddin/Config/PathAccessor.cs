using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RevitProjectDataAddin
{
    public static class PathAccessor
    {
        public static object GetByPath(object root, string path)
        {
            var parts = ParsePath(path);
            object current = root;

            foreach (var part in parts)
            {
                current = GetValue(current, part);
                if (current == null) return null;
            }

            return current;
        }

        public static bool SetByPath(object root, string path, object value)
        {
            var parts = ParsePath(path);
            if (parts.Count == 0) return false;

            object current = root;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                current = GetValue(current, parts[i]);
                if (current == null) return false;
            }

            return SetValue(current, parts.Last(), value);
        }

        private static object GetValue(object obj, PathPart part)
        {
            if (obj == null || part == null) return null;

            Type type = obj.GetType();
            object value = null;

            // Get property
            var prop = type.GetProperty(part.PropertyName);
            if (prop != null)
            {
                value = prop.GetValue(obj);
            }
            else
            {
                // Get field
                var field = type.GetField(part.PropertyName);
                if (field != null)
                    value = field.GetValue(obj);
            }

            return ProcessMatch(value, part);
        }

        private static object ProcessMatch(object value, PathPart part)
        {
            if (value is IList list && part.Index >= 0 && part.Index < list.Count)
            {
                return list[part.Index];
            }

            if (value is IEnumerable enumerable && !string.IsNullOrEmpty(part.MatchName))
            {
                foreach (var item in enumerable)
                {
                    var idProp = item.GetType().GetProperty("Id");
                    if (idProp != null && idProp.GetValue(item)?.ToString() == part.MatchName)
                        return item;
                }

                return null;
            }

            return value;
        }

        private static bool SetValue(object obj, PathPart part, object newValue)
        {
            if (obj == null || part == null) return false;

            Type type = obj.GetType();

            var prop = type.GetProperty(part.PropertyName);
            if (prop != null)
            {
                object converted = ConvertToType(newValue, prop.PropertyType);
                prop.SetValue(obj, converted);
                return true;
            }

            var field = type.GetField(part.PropertyName);
            if (field != null)
            {
                object converted = ConvertToType(newValue, field.FieldType);
                field.SetValue(obj, converted);
                return true;
            }

            return false;
        }

        private static object ConvertToType(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString());

            return Convert.ChangeType(value, targetType);
        }

        private class PathPart
        {
            public string PropertyName;
            public int Index = -1;
            public string MatchName;
        }

        private static List<PathPart> ParsePath(string path)
        {
            var parts = path.Split('/');
            var result = new List<PathPart>();

            foreach (var part in parts)
            {
                var p = new PathPart();
                //Trong hệ thống PathAccessor của bạn, prefix "Field:" được dùng để truy xuất trực tiếp đến thuộc tính (property)
                //của một đối tượng mà không cần đi qua danh sách hoặc điều kiện lọc theo ID
                if (part.StartsWith("Field:"))
                {
                    p.PropertyName = part.Substring(6);
                }
                else if (part.Contains('['))
                {
                    int idxStart = part.IndexOf('[');
                    int idxEnd = part.IndexOf(']');
                    p.PropertyName = part.Substring(0, idxStart);
                    string inner = part.Substring(idxStart + 1, idxEnd - idxStart - 1);

                    if (int.TryParse(inner, out int index))
                    {
                        p.Index = index;
                    }
                    else
                    {
                        p.MatchName = inner;
                    }
                }
                else if (part.Contains(':'))
                {
                    var tokens = part.Split(':');
                    p.PropertyName = tokens[0];
                    p.MatchName = tokens[1];
                }
                else
                {
                    p.PropertyName = part;
                }

                result.Add(p);
            }

            return result;
        }
    }
}
