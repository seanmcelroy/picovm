using System.Collections.Generic;
using System.IO;

namespace picovm.Packager.PE
{
    public sealed class PEImportDirectoryTable : List<PEImportDirectoryEntry>
    {
        public PEImportDirectoryTable(Stream stream, IEnumerable<SectionHeaderEntry> sectionHeaders)
        {
            this.Add(stream, sectionHeaders);
        }

        private void Add(Stream stream, IEnumerable<SectionHeaderEntry> sectionHeaders)
        {
            while (true)
            {
                var nextEntry = new PEImportDirectoryEntry(stream, sectionHeaders);
                if (default(PEImportDirectoryEntry).Equals(nextEntry))
                    return;
                this.Add(nextEntry);
            }
        }
    }
}