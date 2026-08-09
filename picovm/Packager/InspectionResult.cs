using System.Collections.Generic;
using System.Collections.Immutable;

namespace picovm.Packager
{
    public readonly struct InspectionResult(IEnumerable<object> metadata)
    {
        public readonly ImmutableList<object> Metadata = [.. metadata];
    }
}