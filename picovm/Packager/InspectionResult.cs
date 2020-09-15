using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace picovm.Packager
{
    public readonly struct InspectionResult
    {
        public readonly ImmutableList<object> Metadata;

        public InspectionResult(IEnumerable<object> metadata)
        {
            this.Metadata = metadata.ToImmutableList();
        }
    }
}