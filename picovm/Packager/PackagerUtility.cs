using System;
using System.ComponentModel;
using System.Linq;

namespace picovm.Packager.Elf
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
    }
}