using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MoveLib
{
    public static class BCH
    {
        public static void BchToJson(string inFile, string outFile)
        {
            BCHFile bch;

            try
            {
                bch = FromUassetFile(inFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create JSON.\n" + ex.Message + " - " + ex.Data);
                throw;
            }

            Formatting format = Formatting.Indented;

            var json = JsonConvert.SerializeObject(bch, format, new Newtonsoft.Json.Converters.StringEnumConverter());

            File.WriteAllText(outFile, json);
        }

        public static bool JsonToBch(string inFile, string outFile)
        {
            BCHFile bch;

            try
            {
                bch = JsonConvert.DeserializeObject<BCHFile>(File.ReadAllText(inFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing JSON: " + ex.Message + " - " + ex.Data);
                return false;
            }

            try
            {
                ToUassetFile(bch, outFile);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static BCHFile FromUassetFile(string fileName)
        {
            BCHFile BCH = new BCHFile();

            byte[] fileBytes = File.ReadAllBytes(fileName);

            byte[] UassetHeaderBytes = Common.GetUassetHeader(fileBytes);
            fileBytes = Common.RemoveUassetHeader(fileBytes);


            using (var ms = new MemoryStream(fileBytes))
            using (var inFile = new BinaryReader(ms))
            {
                string bacString = new string(inFile.ReadChars(4));

                if (bacString != "#BCH")
                {
                    throw new Exception("Error: Not a valid KWBCH file!");
                }

                inFile.BaseStream.Seek(0xc, SeekOrigin.Begin);

                int numberOfVariables = inFile.ReadInt32();
                int startOfVariables = inFile.ReadInt32();

                if (inFile.BaseStream.Position != startOfVariables)
                {
                    Console.WriteLine("We're not at the start of the variables!!!");
                }

                List<int> VariableAddresses = new List<int>();

                for (int i = 0; i < numberOfVariables; i++)
                {
                    VariableAddresses.Add(inFile.ReadInt32());
                }

                BCH.BCH = new Dictionary<string, int>();

                foreach (var variableAddress in VariableAddresses)
                {
                    var name = Common.GetName(variableAddress, inFile);
                    inFile.BaseStream.Seek(variableAddress + 0x20, SeekOrigin.Begin);
                    var value = inFile.ReadInt32();

                    BCH.BCH.Add(name, value);
                }

            }

            BCH.RawUassetHeaderDontTouch = UassetHeaderBytes;

            return BCH;
        }

        public static void ToUassetFile(BCHFile file, string OutPutFileName)
        {
            byte[] outPutFileBytes;

            using (var ms = new MemoryStream())
            using (var outFile = new BinaryWriter(ms))
            {

                byte[] headerBytes =
                {
                    0x23, 0x42, 0x43, 0x48, 0xFE, 0xFF, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00
                };

                outFile.Write(headerBytes);

                outFile.Write(file.BCH.Count);

                var positionOfStartOfVariableAddressesAddress = outFile.BaseStream.Position;

                outFile.Write((int)0);

                var positionOfStartOfVariables = outFile.BaseStream.Position;

                Common.WriteInt32ToPosition(outFile, positionOfStartOfVariableAddressesAddress, (int)outFile.BaseStream.Position);

                foreach (var i in file.BCH)
                {
                    outFile.Write((int)0);
                }

                var positionOfActualValues = outFile.BaseStream.Position;

                for (int i = 0; i < file.BCH.Count; i++)
                {
                    Common.WriteInt32ToPosition(outFile, positionOfStartOfVariables + (i * 4), (int)positionOfActualValues + (i * 0x24));
                }

                foreach (var i in file.BCH)
                {
                    List<Byte> byteList = new List<byte>();

                    byteList.AddRange(Encoding.UTF8.GetBytes(i.Key));

                    while (byteList.Count < 0x20)
                    {
                        byteList.Add(0x00);
                    }

                    byteList.AddRange(BitConverter.GetBytes(i.Value));

                    outFile.Write(byteList.ToArray());
                }


                outPutFileBytes = ms.ToArray();
            }

            var outPut = outPutFileBytes.ToList();
            outPut.InsertRange(0, BitConverter.GetBytes(outPutFileBytes.Length));

            outPut.InsertRange(0, new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x05, 0x00, 0x00, 0x00, 
                0x00, 0x00, 0x00, 0x00
            });

            outPut.InsertRange(0, BitConverter.GetBytes(outPutFileBytes.Length + 4));

            outPut.InsertRange(0, new byte[]
            {
                0x07, 0x00, 0x00, 0x00, 
                0x00, 0x00, 0x00, 0x00, 
                0x03, 0x00, 0x00, 0x00, 
                0x00, 0x00, 0x00, 0x00
            });

            outPut = Common.CreateUassetFile(outPut, file.RawUassetHeaderDontTouch);

            Debug.WriteLine("Done.");

            File.WriteAllBytes(OutPutFileName, outPut.ToArray());
        }
    }

    public class BCHFile
    {
        public Dictionary<string, int> BCH { get; set; }
        public byte[] RawUassetHeaderDontTouch { get; set; }
    }
}
