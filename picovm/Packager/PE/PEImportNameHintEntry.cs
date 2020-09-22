using System.IO;

namespace picovm.Packager.PE
{
    public readonly struct PEImportNameHintEntry
    {
        public readonly ushort HintIndex;
        public readonly string Name;

        public PEImportNameHintEntry(Stream stream)
        {
            this.HintIndex = stream.ReadUInt16();
            this.Name = stream.ReadNulTerminatedString();
        }
    }
}