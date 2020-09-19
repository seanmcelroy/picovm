using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using picovm.Packager.Elf;

namespace picovm.Packager.Elf.Elf64
{
    public struct ProgramHeader64
    {
        public ProgramHeaderType P_TYPE;

        public UInt32 P_FLAGS;

        [Description("This member gives the offset from the beginning of the file at which the first byte of the segment resides")]
        public UInt64 P_OFFSET;

        public UInt64 P_VADDR;

        public UInt64 P_PADDR;

        public UInt64 P_FILESZ;

        public UInt64 P_MEMSZ;

        public UInt64 P_ALIGN;

        public void Read(Stream stream)
        {
            P_TYPE = stream.ReadWord<ProgramHeaderType>(ProgramHeaderType.PT_NULL);
            P_FLAGS = stream.ReadUInt32();
            P_OFFSET = stream.ReadOffset64();
            P_VADDR = stream.ReadAddress64();
            P_PADDR = stream.ReadAddress64();
            P_FILESZ = stream.ReadUInt64();
            P_MEMSZ = stream.ReadUInt64();
            P_ALIGN = stream.ReadUInt64();
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
            headerLength += stream.WriteWord(P_FLAGS);
            headerLength += stream.WriteOffset64(P_OFFSET);
            headerLength += stream.WriteAddress64(P_VADDR);
            headerLength += stream.WriteAddress64(P_PADDR);
            headerLength += stream.WriteXWord(P_FILESZ);
            headerLength += stream.WriteXWord(P_MEMSZ);
            headerLength += stream.WriteXWord(P_ALIGN);

            return headerLength;
        }
    }
}