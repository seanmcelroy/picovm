using System;
using System.Collections.Generic;
using System.IO;

namespace picovm.Packager.PE
{
    public readonly struct PEImportDirectoryEntry
    {
        public readonly UInt32 ImportLookupTableRva;
        public readonly UInt32 Timestamp;
        public readonly UInt32 ForwarderChain;
        public readonly UInt32 NameRva;
        public readonly UInt32 ImportAddressTableRva;

        private readonly string? _name;

        public string? Name { get => _name; }

        public PEImportDirectoryEntry(Stream stream, IEnumerable<SectionHeaderEntry> sectionHeaders)
        {
            ImportLookupTableRva = stream.ReadUInt32();
            Timestamp = stream.ReadUInt32();
            ForwarderChain = stream.ReadUInt32();
            NameRva = stream.ReadUInt32();
            ImportAddressTableRva = stream.ReadUInt32();
            var current = stream.Position;

            if (NameRva > 0)
            {
                stream.SeekToRVA(sectionHeaders, NameRva);
                _name = stream.ReadNulTerminatedString();
                stream.Seek(current, SeekOrigin.Begin);
            }
            else
                _name = null;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not PEImportDirectoryEntry mys)
                return false;

            return
                mys.ImportLookupTableRva == ImportLookupTableRva &&
                mys.Timestamp == Timestamp &&
                mys.ForwarderChain == ForwarderChain &&
                mys.NameRva == NameRva &&
                mys.ImportAddressTableRva == ImportAddressTableRva;
        }

        public override int GetHashCode() => HashCode.Combine(ImportLookupTableRva, Timestamp, ForwarderChain, NameRva, ImportAddressTableRva);
        public override string ToString() => $"IltRVAv={ImportLookupTableRva}, NameRVA={NameRva}";
    }
}