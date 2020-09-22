using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace picovm.Packager
{
    public static class PackagerUtility
    {
        public static string GetEnumAttributeValue<TEnum, TAttr>(TEnum value, Func<TAttr, string> selector)
            where TEnum : Enum
            where TAttr : Attribute
        {
            var fi = typeof(TEnum).GetField(value.ToString());
            if (fi == null)
                return value.ToString();

            var attributes = fi.GetCustomAttributes(typeof(TAttr), false) as TAttr[];

            if (attributes != null && attributes.Any())
                return attributes.Select(a => selector.Invoke(a)).First();

            return value.ToString();
        }

        public static string GetEnumDescription<TEnum>(TEnum value) where TEnum : Enum =>
            GetEnumAttributeValue<TEnum, DescriptionAttribute>(value, d => d.Description);

        public static string GetEnumDescription<TEnum>(UInt16 value) where TEnum : Enum
        {
            TEnum ev = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return GetEnumDescription<TEnum>(ev);
        }

        public static string GetEnumFlagsShortName<TEnum>(object value, string? separator = null) where TEnum : Enum
        {
            var flagString = new StringBuilder();
            foreach (var flag in Enum.GetValues(typeof(TEnum)).Cast<TEnum>())
            {
                if (((TEnum)value).HasFlag(flag))
                {
                    if (flagString.Length > 0 && !string.IsNullOrEmpty(separator))
                        flagString.Append(separator);
                    flagString.Append(PackagerUtility.GetEnumAttributeValue<TEnum, ShortNameAttribute>(flag, s => s.DisplayName));
                }
            }
            return flagString.ToString();
        }

        public static string ToByteString(this IEnumerable<byte>? bytes, string? separator = "")
        {
            if (bytes == null)
                return string.Empty;
            return bytes.Select(b => $"{b:x}").Aggregate((c, n) => $"{c}{separator}{n}");
        }
    }
}