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
                        inFile.BaseStream.Seek(0x210, SeekOrigin.Begin);
                        string fileType = new string(inFile.ReadChars(4));

                        Debug.WriteLine("filetype: " + fileType);

                        if (fileType == "#BAC")
                        {
                            return FileType.BACeff;
                        }

                        inFile.BaseStream.Seek(0x208, SeekOrigin.Begin);

                        fileType = new string(inFile.ReadChars(4));

                        Debug.WriteLine("filetype: " + fileType);

                        if (fileType == "#BAC")
                        {
                            return FileType.BAC;
                        }

                        if (fileType == "#BCM")
                        {
                            return FileType.BCM;
                        }

                        return FileType.Unknown;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Can't figure out what kind of file this is...\n" + ex.Message + " - " + ex.Data);
                return FileType.Unknown;
            }
        }
    }

    public enum FileType
    {
        Unknown = 0,
        BCM = 1,
        BAC = 2,
        BACeff = 3
    }
}