using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using picovm.Packager.Elf.Elf;

namespace picovm.Packager.Elf.Elf32
{
    public struct ProgramHeader32
    {
        public ProgramHeaderType P_TYPE;

        [Description("This member gives the offset from the beginning of the file at which the first byte of the segment resides")]
        public UInt32 P_OFFSET;

        public UInt32 P_VADDR;

        public UInt32 P_PADDR;

        public UInt32 P_FILESZ;

        public UInt32 P_MEMSZ;

        public UInt32 P_FLAGS;

        public UInt32 P_ALIGN;

        public void Read(Stream stream)
        {
            P_TYPE = stream.ReadWord<ProgramHeaderType>(ProgramHeaderType.PT_NULL);
            P_OFFSET = stream.ReadOffset32();
            P_VADDR = stream.ReadAddress32();
            P_PADDR = stream.ReadAddress32();
            P_FILESZ = stream.ReadUInt32();
            P_MEMSZ = stream.ReadUInt32();
            P_FLAGS = stream.ReadUInt32();
            P_ALIGN = stream.ReadUInt32();
        }

        public (MemoryStream, uint programHeaderSizeReal, int programHeaderSizePad) ToMemoryStream()
        {
            var msProgramHeader = new MemoryStream();
            Write(msProgramHeader);
            var bwProgramHeader = new BinaryWriter(msProgramHeader);
            uint programHeaderSizeReal = (uint)msProgramHeader.Position;
            int programHeaderSizePad = programHeaderSizeReal.CalculateRoundUpTo16Pad();
            bwProgramHeader.Write(Enumerable.Repeat((byte)0x00, programHeaderSizePad).ToArray());
            bwProgramHeader.Flush();
            return (msProgramHeader, programHeaderSizeReal, programHeaderSizePad);
        }

        public UInt16 Write(Stream stream)
        {
            UInt16 headerLength = 0;

            headerLength += stream.WriteWord((UInt32)P_TYPE);
            headerLength += stream.WriteOffset32(P_OFFSET);
            headerLength += stream.WriteAddress32(P_VADDR);
            headerLength += stream.WriteAddress32(P_PADDR);
            headerLength += stream.WriteWord(P_FILESZ);
            headerLength += stream.WriteWord(P_MEMSZ);
            headerLength += stream.WriteWord(P_FLAGS);
            headerLength += stream.WriteWord(P_ALIGN);

            return headerLength;
        }
    }
}