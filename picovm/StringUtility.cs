using System.Text;

namespace picovm
{
    public static class StringUtility
    {
        public static string LeftAlignToSize(this StringBuilder? value, int size, char paddingCharacter = ' ')
        {
            return value?.ToString().LeftAlignToSize(size, paddingCharacter) ?? string.Empty;
        }

        public static string LeftAlignToSize(this string value, int size, char paddingCharacter = ' ')
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty.PadRight(size, paddingCharacter);
            if (value.Length < size)
                return value.PadRight(size, paddingCharacter);
            return value.Substring(0, size).PadRight(size, paddingCharacter);
        }

        public static string RightAlignHexToSize(this ulong value, int size = 16, char paddingCharacter = '0') => $"{value:x}".PadLeft(size, paddingCharacter);

        public static string RightAlignHexToSize(this uint value, int size = 4, char paddingCharacter = '0') => $"{value:x}".PadLeft(size, paddingCharacter);

        public static string RightAlignDecToSize(this ulong value, int size = 16, char paddingCharacter = '0') => $"{value}".PadLeft(size, paddingCharacter);

        public static string RightAlignDecToSize(this uint value, int size = 4, char paddingCharacter = '0') => $"{value}".PadLeft(size, paddingCharacter);
    }
}