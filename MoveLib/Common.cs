using System;
using System.Collections.Generic;
using System.Linq;

namespace MoveLib
{
    public static class Common
    {
        public static byte[] RemoveUassetHeader(byte[] fileBytes)
        {
            var tempList = fileBytes.ToList();

            int sizeOfHeader = BitConverter.ToInt32(fileBytes, 0x18);

            tempList.RemoveRange(0, sizeOfHeader + 36);
            return tempList.ToArray();
        }

        public static byte[] GetUassetHeader(byte[] fileBytes)
        {
            var tempList = fileBytes.ToList();

            int sizeOfHeader = BitConverter.ToInt32(fileBytes, 0x18);

            byte[] array = new byte[sizeOfHeader];
            tempList.CopyTo(0, array, 0, sizeOfHeader);

            return array;
        }

        public static List<byte> CreateUassetFile(List<byte> fileBytes, byte[] uassetHeader)
        {
            fileBytes.AddRange(new byte[]
            {
                0x09, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            });

            var tempLengthBytes = BitConverter.GetBytes(fileBytes.Count);

            uassetHeader[uassetHeader.Length - 0x34] = tempLengthBytes[0];
            uassetHeader[uassetHeader.Length - 0x33] = tempLengthBytes[1];
            uassetHeader[uassetHeader.Length - 0x32] = tempLengthBytes[2];
            uassetHeader[uassetHeader.Length - 0x31] = tempLengthBytes[3];

            fileBytes.InsertRange(0, uassetHeader);

            byte[] UassetEnd = new byte[4];
            uassetHeader.ToList().CopyTo(0, UassetEnd, 0, 4);

            fileBytes.AddRange(UassetEnd);

            return fileBytes;
        }
    }
}
