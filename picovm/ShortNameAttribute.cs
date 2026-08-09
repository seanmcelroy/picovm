using System;

namespace picovm
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class ShortNameAttribute(string displayName) : Attribute()
    {

        //
        // Summary:
        //     Gets the display name for a property, event, or public void method that takes
        //     no arguments stored in this attribute.
        //
        // Returns:
        //     The display name.
        public string DisplayName { get; } = displayName;
    }
}