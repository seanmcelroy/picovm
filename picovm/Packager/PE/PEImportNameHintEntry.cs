using System.IO;

namespace picovm.Packager.PE
{
    public readonly struct PEImportNameHintEntry(Stream stream)
    {
        public readonly ushort HintIndex = stream.ReadUInt16();
        public readonly string Name = stream.ReadNulTerminatedString();
    }
}