using System;
using System.Collections.Generic;

namespace picovm.Packager.PE
{
    public sealed class PEDataDictionary : List<PEDataDictionaryEntry>
    {
        public void Add(UInt32 rva, UInt32 size) => this.Add(new PEDataDictionaryEntry(rva, size));
    }
}