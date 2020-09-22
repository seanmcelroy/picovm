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
            this.ImportLookupTableRva = stream.ReadUInt32();
            this.Timestamp = stream.ReadUInt32();
            this.ForwarderChain = stream.ReadUInt32();
            this.NameRva = stream.ReadUInt32();
            this.ImportAddressTableRva = stream.ReadUInt32();
            var current = stream.Position;

            if (this.NameRva > 0)
            {
                stream.SeekToRVA(sectionHeaders, this.NameRva);
                _name = stream.ReadNulTerminatedString();
                stream.Seek(current, SeekOrigin.Begin);
            }
            else
                _name = null;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is PEImportDirectoryEntry))
                return false;

            var mys = (PEImportDirectoryEntry)obj;
            return
                mys.ImportLookupTableRva == this.ImportLookupTableRva &&
                mys.Timestamp == this.Timestamp &&
                mys.ForwarderChain == this.ForwarderChain &&
                mys.NameRva == this.NameRva &&
                mys.ImportAddressTableRva == this.ImportAddressTableRva;
        }

        public override int GetHashCode() => HashCode.Combine(ImportLookupTableRva, Timestamp, ForwarderChain, NameRva, ImportAddressTableRva);
        public override string ToString() => $"IltRVAv={this.ImportLookupTableRva}, NameRVA={this.NameRva}";
    }
}