using System;
using System.ComponentModel;
using System.Linq;

namespace picovm.Packager.Elf
{
    public static class PackagerUtility
    {
        public static string GetEnumDescription<TEnum>(TEnum value) where TEnum : Enum
        {
            var fi = typeof(TEnum).GetField(value.ToString());
            if (fi == null)
                return value.ToString();

            var attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];

            if (attributes != null && attributes.Any())
                return attributes.First().Description;

            return value.ToString();
        }
    }
}