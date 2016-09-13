using System;
using System.Diagnostics;
using System.IO;

namespace MoveLib
{
    public static class FileTypeDecider
    {
        public static FileType Decide(string fileName)
        {
            try
            {
                using (var fs = File.OpenRead(fileName))
                {
                    using (var inFile = new BinaryReader(fs))
                    {
                        inFile.BaseStream.Seek(0x18, SeekOrigin.Begin);
                        int OffsetToStart = inFile.ReadInt32();

                        inFile.BaseStream.Seek(OffsetToStart + 0x24, SeekOrigin.Begin);

                        string fileType = new string(inFile.ReadChars(4));

                        Debug.WriteLine("filetype: " + fileType);

                        if (fileType == "#BAC")
                        {
                            return FileType.BAC;
                        }

                        if (fileType == "#BCM")
                        {
                            return FileType.BCM;
                        }

                        if (fileType == "#BCH")
                        {
                            return FileType.BCH;
                        }

                        return FileType.Unknown;
                    }
                }
            }
            catch (Exception ex)
            {
                return FileType.Unknown;
            }

        }
    }

    public enum FileType
    {
        Unknown = 0,
        BCM = 1,
        BAC = 2,
        BCH = 3
    }
}
