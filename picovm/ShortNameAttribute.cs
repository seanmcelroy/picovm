using System;

namespace picovm
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class ShortNameAttribute : Attribute
    {
        //
        // Summary:
        //     Initializes a new instance of the System.ComponentModel.DisplayNameAttribute
        //     class using the display name.
        //
        // Parameters:
        //   displayName:
        //     The display name.
        public ShortNameAttribute(string displayName) : base() => this.DisplayName = displayName;

        //
        // Summary:
        //     Gets the display name for a property, event, or public void method that takes
        //     no arguments stored in this attribute.
        //
        // Returns:
        //     The display name.
        public string DisplayName { get; }
    }
}