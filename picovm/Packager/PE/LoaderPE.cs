using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using picovm.VM;

namespace picovm.Packager.PE
{
    public sealed class LoaderPE : ILoader
    {
        private readonly Stream stream;

        public LoaderPE(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            this.stream = stream;
        }

        public LoaderResult64 LoadImage()
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not available for reading", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream is not available for seeking", nameof(stream));

            var metadata = new List<object>();

            var msDosStubHeader = new MsDosStubHeader();
            msDosStubHeader.Read(stream);
            metadata.Add(msDosStubHeader);

            stream.Seek((long)msDosStubHeader.e_lfanew, SeekOrigin.Begin);
            var peHeader = new PEHeader();
            peHeader.Read(stream);
            metadata.Add(peHeader);

            UInt32 entryPoint = 0;
            if (peHeader.mSizeOfOptionalHeader > 0)
            {
                PEHeaderOption64 pe64;
                if (!PEHeaderOption64.TryRead(stream, out pe64))
                    return LoaderResult64.Error("Unable to read Pe64OptionalHeader");
                metadata.Add(pe64);

                entryPoint = pe64.mAddressOfEntryPoint;
            }

            return new LoaderResult64(entryPoint, new byte[0], metadata: metadata);
        }

        public ImmutableList<object> LoadMetadata()
        {
            var metadata = new List<object>();

            var msDosStubHeader = new MsDosStubHeader();
            msDosStubHeader.Read(stream);
            metadata.Add(msDosStubHeader);

            stream.Seek((long)msDosStubHeader.e_lfanew, SeekOrigin.Begin);
            var peHeader = new PEHeader();
            peHeader.Read(stream);
            metadata.Add(peHeader);

            var positionPeOptional = stream.Position;
            {
                var rvaAndSizes = new PEDataDictionary();
                var sectionHeaders = new SectionHeaderTable();

                PEHeaderOption64 pe64;
                if (PEHeaderOption64.TryRead(stream, out pe64))
                {
                    metadata.Add(pe64);

                    // Read data dictionaries
                    for (int i = 0; i < pe64.mNumberOfRvaAndSizes; i++)
                    {
                        var rva = stream.ReadUInt32();
                        var sz = stream.ReadUInt32();
                        rvaAndSizes.Add(rva, sz);
                    }

                    metadata.Add(rvaAndSizes);
                }
                else
                    stream.Seek(positionPeOptional, SeekOrigin.Begin);

                PEHeaderOption32 pe32;
                if (PEHeaderOption32.TryRead(stream, out pe32))
                {
                    metadata.Add(pe32);

                    // Read data dictionaries
                    for (int i = 0; i < pe32.mNumberOfRvaAndSizes; i++)
                    {
                        var rva = stream.ReadUInt32();
                        var sz = stream.ReadUInt32();
                        rvaAndSizes.Add(rva, sz);
                    }
                    metadata.Add(rvaAndSizes);

                    // Read section headers

                    for (int i = 0; i < peHeader.mNumberOfSections; i++)
                    {
                        var shdr = new SectionHeaderEntry(stream);
                        sectionHeaders.Add(shdr);
                    }
                    metadata.Add(sectionHeaders);

                    // PE Import table (2nd entry)
                    if (rvaAndSizes.Count + 1 >= (int)PEDataDictionaryIndex.IMPORT_TABLE)
                    {
                        var rva = rvaAndSizes[(int)PEDataDictionaryIndex.IMPORT_TABLE];
                        stream.SeekToRVA(sectionHeaders, rva.RelativeVirtualAddress);

                        var idt = new PEImportDirectoryTable(stream, sectionHeaders);
                        metadata.Add(idt);

                        foreach (var ide in idt)
                        {
                            var itt = new PEImportLookupTable();
                            stream.SeekToRVA(sectionHeaders, ide.ImportLookupTableRva);
                            UInt32 ite;
                            do
                            {
                                ite = stream.ReadUInt32();
                                if (ite == 0)
                                    break;
                                itt.Add(new PEImportLookupEntry(ite));
                            } while (true);

                            foreach (var entry in itt)
                            {
                                if (!entry.Key.OrdinalNameFlag)
                                {
                                    stream.SeekToRVA(sectionHeaders, entry.Key.HintTableNameRva);
                                    var nameHint = new PEImportNameHintEntry(stream);
                                    var name = nameHint.Name;
                                    itt.QueueNameUpdate(entry.Key, name);
                                }
                            }
                            itt.ApplyNameUpdates();

                            metadata.Add(itt);
                        }
                    }

                    // PE Resources table (3rd entry)
                    if (rvaAndSizes.Count + 1 >= (int)PEDataDictionaryIndex.RESOURCE_TABLE)
                    {
                        var rva = rvaAndSizes[(int)PEDataDictionaryIndex.RESOURCE_TABLE];
                        stream.SeekToRVA(sectionHeaders, rva.RelativeVirtualAddress);
                    }

                }
                else
                    stream.Seek(positionPeOptional, SeekOrigin.Begin);


            }

            return metadata.ToImmutableList();
        }

        ILoaderResult ILoader.LoadImage() => this.LoadImage();
    }
}
