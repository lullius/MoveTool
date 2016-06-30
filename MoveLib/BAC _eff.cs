using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MoveLib.BAC
{
    public static class BACEff
    {
        public static void BacToJson(string inFile, string outFile)
        {
            BACeffFile bac;

            try
            {
                bac = FromUassetFile(inFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create JSON.\n" + ex.Message + " - " + ex.Data );
                return;
            }

            Formatting format = Formatting.Indented;
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.FloatFormatHandling = FloatFormatHandling.DefaultValue;

            var json = JsonConvert.SerializeObject(bac, format, new Newtonsoft.Json.Converters.StringEnumConverter());

            File.WriteAllText(outFile, json);
        }

        public static bool JsonToBac(string inFile, string outFile)
        {
            BACeffFile bac;

            try
            {
                bac = JsonConvert.DeserializeObject<BACeffFile>(File.ReadAllText(inFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing JSON: " + ex.Message + " - " + ex.Data);
                return false;
            }

            try
            {
                ToUassetFile(bac, outFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create BAC.\n" + ex.Message + " - " + ex.Data);
                return false;
            }

            return true;
        }

        public static BACeffFile FromUassetFile(string fileName)
        {
            List<Move> MoveList = new List<Move>();
            List<HitboxEffects> HitboxEffectsList = new List<HitboxEffects>();

            byte[] fileBytes = File.ReadAllBytes(fileName);

            byte[] UassetHeaderBytes = GetUassetHeader(fileBytes);
            fileBytes = CreateGameBACFromFile(fileBytes);

            Debug.WriteLine("READING");
            using (var ms = new MemoryStream(fileBytes))
            using (var inFile = new BinaryReader(ms))
            {
                string bacString = new string(inFile.ReadChars(4));

                if (bacString != "#BAC")
                {
                    throw new Exception("Error: Not a valid KWBAC file!");
                }

                Debug.WriteLine(bacString);

                inFile.BaseStream.Seek(0xC, SeekOrigin.Begin);

                short MoveListCount = inFile.ReadInt16(); //MoveList has a similar structure to a BCM or BAC file in itself, but uses offsets from the start of the table instead of from the beginning of the file
                Debug.WriteLine("MoveListCount: " + MoveListCount);
                short HitboxEffectCount = inFile.ReadInt16();
                Debug.WriteLine("HitboxEffectCount: " + HitboxEffectCount);

                int startOfMoveTable = inFile.ReadInt32();
                int startOfHitboxEffects = inFile.ReadInt32();
                Debug.WriteLine("startOfMoveTable: " + startOfMoveTable.ToString("X"));
                Debug.WriteLine("startOfHitboxEffects: " + startOfHitboxEffects.ToString("X"));

                if (inFile.BaseStream.Position != startOfMoveTable)
                {
                    Debug.WriteLine("We're not at the right position! We're at: " + inFile.BaseStream.Position.ToString("X") + ", but we should be at: " + startOfMoveTable.ToString("X"));
                    throw new Exception("Was at wrong position when trying to read from startOfMoveTable");
                }

                List<int> baseMoveAddresses = new List<int>();

                for (int i = 0; i < MoveListCount; i++)
                {
                    baseMoveAddresses.Add(inFile.ReadInt32());
                }

                BACeffFile file = new BACeffFile();
                file.RawUassetHeaderDontTouch = UassetHeaderBytes;
                file.EffMoveLists = new EffMoveList[MoveListCount];

                for (int i = 0; i < baseMoveAddresses.Count; i++)
                {
                    int thisAddress = baseMoveAddresses[i];

                    Debug.WriteLine("Unknown1 at pos: " + thisAddress.ToString("X") + "  -  Index:" + i);

                    inFile.BaseStream.Seek(thisAddress, SeekOrigin.Begin);

                    short unknown1 = inFile.ReadInt16(); //-1 = NORMAL?
                    int MoveCount = inFile.ReadInt16();
                    int StartOfMovesOffset = inFile.ReadInt32();
                    int StartOfNameAddressesOffset = inFile.ReadInt32();
                    int StartOfNameAddresses = StartOfNameAddressesOffset + thisAddress;
                    int StartOfMoves = StartOfMovesOffset + thisAddress;

                    Debug.WriteLine("baseMoveAddresses.Count: " + baseMoveAddresses.Count);

                    Debug.WriteLine("U1: " + unknown1 +
                        "\nMoveCount: " + MoveCount +
                        "\nStartOfMovesOffset: " + StartOfMovesOffset
                        + "\nStartOfNameAddressesOffset: " + StartOfNameAddressesOffset.ToString("X"));

                    List<int> MoveAddresses = new List<int>();
                    List<int> NameAddresses = new List<int>();

                    file.EffMoveLists[i] = new EffMoveList();
                    file.EffMoveLists[i].Unknown1 = unknown1;
                    file.EffMoveLists[i].EffMoves = new EffMove[MoveCount];

                    for (int j = 0; j < MoveCount; j++)
                    {
                        inFile.BaseStream.Seek(StartOfMoves + (j * 4), SeekOrigin.Begin);
                        int MoveAddressOffset = inFile.ReadInt32();
                        int thisMoveAddress = MoveAddressOffset + thisAddress;

                        int NameAddressAddress = (j * 4) + StartOfNameAddresses;
                        inFile.BaseStream.Seek(NameAddressAddress, SeekOrigin.Begin);
                        int thisNameAddress = inFile.ReadInt32() + thisAddress;

                        if (MoveAddressOffset == 0)
                        {
                            thisMoveAddress = 0;
                        }

                        MoveAddresses.Add(thisMoveAddress);
                        NameAddresses.Add(thisNameAddress);
                    }

                    for (int j = 0; j < MoveCount; j++)
                    {
                        int thisMoveAddress = MoveAddresses[j];
                        if (thisMoveAddress == 0)
                        {
                            continue;
                        }

                        int thisNameAddress = NameAddresses[j];

                        inFile.BaseStream.Seek(thisNameAddress, SeekOrigin.Begin);
                        string Name = GetName(thisNameAddress, inFile);

                        inFile.BaseStream.Seek(thisMoveAddress, SeekOrigin.Begin);

                        Debug.WriteLine("Adding Move at: " + thisMoveAddress.ToString("X"));

                        #region DebuggingPurposes

                        int Size = 0;

                        if (j == MoveAddresses.Count - 1)
                        {
                            Size = 0;
                        }
                        else
                        {
                            int nextAddress = 1;
                            while (MoveAddresses[j + nextAddress] == 0)
                            {
                                nextAddress++;
                            }

                            Size = (MoveAddresses[j + nextAddress] - thisMoveAddress);

                            if (Size < 0)
                            {
                                Debug.WriteLine("Size was smaller than 0?? Next address:" + MoveAddresses[j + nextAddress].ToString("X") + " This Address: " + thisMoveAddress.ToString("X"));
                            }
                        }

                        Debug.WriteLine("Size: " + Size);

                        #endregion

                        EffMove thisMove = new EffMove()
                        {
                            Name = Name,
                            Index = j,
                            FirstHitboxFrame = inFile.ReadInt32(),
                            LastHitboxFrame = inFile.ReadInt32(),
                            InterruptFrame = inFile.ReadInt32(),
                            TotalTicks = inFile.ReadInt32(),

                            unk1 = inFile.ReadInt32(),
                            unk2 = inFile.ReadSingle(),
                            unk3 = inFile.ReadSingle(),
                            unk4 = inFile.ReadSingle(),
                            unk5 = inFile.ReadSingle(),
                            unk6 = inFile.ReadSingle(),
                            unk7 = inFile.ReadSingle(),
                            Flag = inFile.ReadInt32(),
                            unk9 = inFile.ReadInt32(),

                            numberOfTypes = inFile.ReadInt32(),

                            unk13 = inFile.ReadInt32(),
                            HeaderSize = inFile.ReadInt32(),
                        };

                        if (thisMove.HeaderSize == 0x58)
                        {
                            thisMove.Unknown12 = inFile.ReadInt16();
                            thisMove.Unknown13 = inFile.ReadInt16();
                            thisMove.Unknown14 = inFile.ReadInt16();
                            thisMove.Unknown15 = inFile.ReadInt16();
                            thisMove.Unknown16 = inFile.ReadInt16();
                            thisMove.Unknown17 = inFile.ReadInt16();
                            thisMove.Unknown18 = inFile.ReadSingle();
                            thisMove.Unknown19 = inFile.ReadInt16();
                            thisMove.Unknown20 = inFile.ReadInt16();
                            thisMove.Unknown21 = inFile.ReadInt16();
                            thisMove.Unknown22 = inFile.ReadInt16();
                        }

                        Debug.WriteLine("Name:  {0}\nIndex: {1}\nFirstHitboxFrame: {2}\nLastHitboxFrame: {3}" +
                                "\nInterruptFrame: {4}\nTotalTicks: {5}, HeaderSize: {6}", thisMove.Name, thisMove.Index, thisMove.FirstHitboxFrame,
                                thisMove.LastHitboxFrame, thisMove.InterruptFrame, thisMove.TotalTicks, thisMove.HeaderSize.ToString("X"));

                        List<TypeInfo> TypeInfoList = new List<TypeInfo>();

                        long typeListBaseOffset = inFile.BaseStream.Position;

                        for (int k = 0; k < thisMove.numberOfTypes; k++)
                        {
                            long thisTypeOffset = typeListBaseOffset + (12 * k);
                            inFile.BaseStream.Seek(thisTypeOffset, SeekOrigin.Begin);
                            short type = inFile.ReadInt16();
                            short count = inFile.ReadInt16();


                            int tickOffset = inFile.ReadInt32();
                            int dataOffset = inFile.ReadInt32();

                            long tickAddress = tickOffset + thisTypeOffset;
                            long dataAddress = dataOffset + thisTypeOffset;

                            Debug.WriteLine("Type: " + type + " Count: " + count + " tickAddress: " +
                                            tickAddress.ToString("X") + " dataAddress: " + dataAddress.ToString("X") +
                                            " dataEndForType4?: " + (dataAddress + (0x4c * count)).ToString("X"));

                            List<int> tickStarts = new List<int>();
                            List<int> tickEnds = new List<int>();

                            for (int l = 0; l < count; l++)
                            {
                                inFile.BaseStream.Seek(tickAddress + (8 * l), SeekOrigin.Begin);
                                tickStarts.Add(inFile.ReadInt32());
                                tickEnds.Add(inFile.ReadInt32());
                            }

                            inFile.BaseStream.Seek(dataAddress, SeekOrigin.Begin);

                            for (int l = 0; l < count; l++)
                            {

                                switch (type)
                                {
                                    case 0:
                                        {
                                            if (thisMove.AutoCancels == null)
                                            {
                                                thisMove.AutoCancels = new AutoCancel[count];
                                            }

                                            long thisType0Address = dataAddress + (l * 16);
                                            inFile.BaseStream.Seek(thisType0Address, SeekOrigin.Begin);

                                            AutoCancel thisType0 = new AutoCancel();
                                            thisType0.TickStart = tickStarts[l];
                                            thisType0.TickEnd = tickEnds[l];
                                            thisType0.Condition = (AutoCancelCondition)inFile.ReadInt16();
                                            thisType0.MoveIndex = inFile.ReadInt16();
                                            thisType0.Unknown1 = inFile.ReadInt16();
                                            thisType0.NumberOfInts = inFile.ReadInt16();
                                            thisType0.Unknown2 = inFile.ReadInt32();
                                            thisType0.Offset = inFile.ReadInt32();
                                            thisType0.Ints = new int[thisType0.NumberOfInts];

                                            inFile.BaseStream.Seek(thisType0Address + thisType0.Offset, SeekOrigin.Begin);

                                            for (int m = 0; m < thisType0.NumberOfInts; m++)
                                            {
                                                thisType0.Ints[m] = inFile.ReadInt32();
                                            }

                                            Debug.WriteLine(
                                                "thisType0 - TickStart: {0}, TickEnd: {1}, Condition: {2}, MoveIndex: {3}, Unknown1: {4}, NumberOfInts: {5}, Unknown2: {6} Offset: {7}",
                                                thisType0.TickStart, thisType0.TickEnd, thisType0.Condition,
                                                thisType0.MoveIndex, thisType0.Unknown1, thisType0.NumberOfInts,
                                                thisType0.Unknown2, thisType0.Offset.ToString("X"));

                                            string ints = "Ints:\n";

                                            foreach (var i1 in thisType0.Ints)
                                            {
                                                ints += i1 + " ,";
                                            }
                                            Debug.WriteLine(ints);

                                            thisMove.AutoCancels[l] = thisType0;

                                            break;
                                        }

                                    case 1:
                                        {
                                            if (thisMove.Type1s == null)
                                            {
                                                thisMove.Type1s = new Type1[count];
                                            }

                                            Type1 thisType1 = new Type1();
                                            thisType1.TickStart = tickStarts[l];
                                            thisType1.TickEnd = tickEnds[l];
                                            thisType1.Flag1 = inFile.ReadInt32();
                                            thisType1.Flag2 = inFile.ReadInt32();

                                            Debug.WriteLine(
                                                "thisType1 - TickStart: {0}, TickEnd: {1}, Flag1: {2}, Flag2: {3}",
                                                thisType1.TickStart, thisType1.TickEnd, thisType1.Flag1, thisType1.Flag2);

                                            thisMove.Type1s[l] = thisType1;

                                            break;
                                        }

                                    case 2:
                                        {
                                            if (thisMove.Forces == null)
                                            {
                                                thisMove.Forces = new Force[count];
                                            }

                                            Force thisForce = new Force();
                                            thisForce.TickStart = tickStarts[l];
                                            thisForce.TickEnd = tickEnds[l];
                                            thisForce.Amount = inFile.ReadSingle();
                                            thisForce.Flag = (ForceEnum)inFile.ReadInt32();

                                            Debug.WriteLine(
                                                "thisForce - TickStart: {0}, TickEnd: {1}, Amount: {2}, Flag: {3}",
                                                thisForce.TickStart, thisForce.TickEnd, thisForce.Amount, thisForce.Flag);

                                            thisMove.Forces[l] = thisForce;

                                            break;
                                        }

                                    case 3:
                                        {
                                            if (thisMove.Cancels == null)
                                            {
                                                thisMove.Cancels = new Cancel[count];
                                            }

                                            Cancel thisType3 = new Cancel();
                                            thisType3.TickStart = tickStarts[l];
                                            thisType3.TickEnd = tickEnds[l];
                                            thisType3.CancelList = inFile.ReadInt32();
                                            thisType3.Type = inFile.ReadInt32();

                                            Debug.WriteLine(
                                                "thisType3 - TickStart: {0}, TickEnd: {1}, CancelList: {2}, Type: {3}",
                                                thisType3.TickStart, thisType3.TickEnd, thisType3.CancelList, thisType3.Type);

                                            thisMove.Cancels[l] = thisType3;

                                            break;
                                        }

                                    case 4:
                                        {
                                            if (thisMove.Type4s == null)
                                            {
                                                thisMove.Type4s = new Type4[count];
                                            }

                                            long thisType4Address = dataAddress + (l * 12);
                                            inFile.BaseStream.Seek(thisType4Address, SeekOrigin.Begin);

                                            Type4 thisType4 = new Type4();

                                            thisType4.TickStart = tickStarts[l];
                                            thisType4.TickEnd = tickEnds[l];

                                            thisType4.Unknown1 = inFile.ReadInt32();
                                            thisType4.Unknown2 = inFile.ReadInt16();
                                            thisType4.NumberOfInts = inFile.ReadInt16();
                                            thisType4.Offset = inFile.ReadInt32();

                                            inFile.BaseStream.Seek(thisType4Address + thisType4.Offset, SeekOrigin.Begin);

                                            thisType4.Ints = new int[thisType4.NumberOfInts];

                                            for (int m = 0; m < thisType4.NumberOfInts; m++)
                                            {
                                                thisType4.Ints[m] = inFile.ReadInt32();
                                            }


                                            Debug.WriteLine(
                                                "thisType4 - TickStart: {0}, TickEnd: {1}, U1: {2}, U2: {3}, NumberOfInts: {4}, Offset: {5}",
                                                thisType4.TickStart, thisType4.TickEnd, thisType4.Unknown1,
                                                thisType4.Unknown2, thisType4.NumberOfInts, thisType4.Offset.ToString("X"));

                                            string ints = "Ints:\n";

                                            foreach (var i1 in thisType4.Ints)
                                            {
                                                ints += i1 + " ,";
                                            }
                                            Debug.WriteLine(ints);

                                            thisMove.Type4s[l] = thisType4;

                                            break;
                                        }

                                    case 5:
                                        {
                                            if (thisMove.Hitboxes == null)
                                            {
                                                thisMove.Hitboxes = new Hitbox[count];
                                            }

                                            Hitbox thisHitbox = new Hitbox();
                                            thisHitbox.TickStart = tickStarts[l];
                                            thisHitbox.TickEnd = tickEnds[l];
                                            thisHitbox.X = inFile.ReadSingle();
                                            thisHitbox.Y = inFile.ReadSingle();
                                            thisHitbox.Z = inFile.ReadSingle();
                                            thisHitbox.Width = inFile.ReadSingle();
                                            thisHitbox.Height = inFile.ReadSingle();

                                            thisHitbox.Unknown1 = inFile.ReadInt32();

                                            thisHitbox.Unknown2 = inFile.ReadInt16();
                                            thisHitbox.Unknown3 = inFile.ReadInt16();
                                            thisHitbox.Unknown4 = inFile.ReadInt16();
                                            thisHitbox.Unknown5 = inFile.ReadInt16();

                                            thisHitbox.Unknown6 = inFile.ReadInt16();
                                            thisHitbox.Unknown7 = inFile.ReadInt16();
                                            thisHitbox.Unknown8 = inFile.ReadInt16();
                                            thisHitbox.Unknown9 = inFile.ReadInt16();

                                            thisHitbox.Flag1 = inFile.ReadByte();
                                            thisHitbox.Flag2 = inFile.ReadByte();
                                            thisHitbox.Flag3 = inFile.ReadByte();
                                            thisHitbox.Flag4 = inFile.ReadByte();

                                            thisHitbox.HitEffect = inFile.ReadInt16();
                                            thisHitbox.Unknown10 = inFile.ReadInt16();
                                            thisHitbox.Unknown11 = inFile.ReadInt32();


                                            Debug.WriteLine(
                                                "thisHitbox - Tickstart: {0}, TickEnd: {1}, X: {2}, Y: {3}, Rot: {4}, Width: {5}, Height: {6}, U1: {7}, U2: {8}, U3: {9}, U4: {10}, U5: {11}, U6: {12}, U7: {13}, U8: {14}, U9: {15}, Flag1: {16}, Flag2: {17}, Flag3: {18}, Flag4: {19}, HitEffect: {20}, U10: {21}, U11: {22}",
                                                thisHitbox.TickStart, thisHitbox.TickEnd, thisHitbox.X, thisHitbox.Y,
                                                thisHitbox.Z, thisHitbox.Width, thisHitbox.Height,
                                                thisHitbox.Unknown1, thisHitbox.Unknown2, thisHitbox.Unknown3,
                                                thisHitbox.Unknown4, thisHitbox.Unknown5, thisHitbox.Unknown6,
                                                thisHitbox.Unknown7,
                                                thisHitbox.Unknown8, thisHitbox.Unknown9, thisHitbox.Flag1,
                                                thisHitbox.Flag2, thisHitbox.Flag3, thisHitbox.Flag4, thisHitbox.HitEffect,
                                                thisHitbox.Unknown10, thisHitbox.Unknown11);

                                            thisMove.Hitboxes[l] = thisHitbox;

                                            break;
                                        }

                                    case 6:
                                        {
                                            if (thisMove.Hurtboxes == null)
                                            {
                                                thisMove.Hurtboxes = new Hurtbox[count];
                                            }

                                            Hurtbox thisHurtbox = new Hurtbox();
                                            thisHurtbox.TickStart = tickStarts[l];
                                            thisHurtbox.TickEnd = tickEnds[l];
                                            thisHurtbox.X = inFile.ReadSingle();
                                            thisHurtbox.Y = inFile.ReadSingle();
                                            thisHurtbox.Z = inFile.ReadSingle();
                                            thisHurtbox.Width = inFile.ReadSingle();
                                            thisHurtbox.Height = inFile.ReadSingle();

                                            thisHurtbox.Unknown1 = inFile.ReadInt32();

                                            thisHurtbox.Unknown2 = inFile.ReadInt16();
                                            thisHurtbox.Unknown3 = inFile.ReadInt16();
                                            thisHurtbox.Unknown4 = inFile.ReadInt16();
                                            thisHurtbox.Unknown5 = inFile.ReadInt16();

                                            thisHurtbox.Unknown6 = inFile.ReadInt16();
                                            thisHurtbox.Unknown7 = inFile.ReadInt16();
                                            thisHurtbox.Unknown8 = inFile.ReadInt16();
                                            thisHurtbox.Unknown9 = inFile.ReadInt16();

                                            thisHurtbox.Flag1 = inFile.ReadByte();
                                            thisHurtbox.Flag2 = inFile.ReadByte();
                                            thisHurtbox.Flag3 = inFile.ReadByte();
                                            thisHurtbox.Flag4 = inFile.ReadByte();

                                            thisHurtbox.HitEffect = inFile.ReadInt16();
                                            thisHurtbox.Unknown10 = inFile.ReadInt16();
                                            thisHurtbox.Unknown11 = inFile.ReadInt32();

                                            thisHurtbox.Unknown12 = inFile.ReadSingle();


                                            Debug.WriteLine(
                                                "thisHurtbox - Tickstart: {0}, TickEnd: {1}, X: {2}, Y: {3}, Rot: {4}, Width: {5}, Height: {6}, U1: {7}, U2: {8}, U3: {9}, U4: {10}, U5: {11}, U6: {12}, U7: {13}, U8: {14}, U9: {15}, Flag1: {16}, Flag2: {17}, Flag3: {18}, Flag4: {19}, HitEffect: {20}, U10: {21}, U11: {22}, U12: {23}",
                                                thisHurtbox.TickStart, thisHurtbox.TickEnd, thisHurtbox.X, thisHurtbox.Y,
                                                thisHurtbox.Z, thisHurtbox.Width, thisHurtbox.Height,
                                                thisHurtbox.Unknown1, thisHurtbox.Unknown2, thisHurtbox.Unknown3,
                                                thisHurtbox.Unknown4, thisHurtbox.Unknown5, thisHurtbox.Unknown6,
                                                thisHurtbox.Unknown7,
                                                thisHurtbox.Unknown8, thisHurtbox.Unknown9, thisHurtbox.Flag1,
                                                thisHurtbox.Flag2, thisHurtbox.Flag3, thisHurtbox.Flag4,
                                                thisHurtbox.HitEffect, thisHurtbox.Unknown10, thisHurtbox.Unknown11,
                                                thisHurtbox.Unknown12);

                                            thisMove.Hurtboxes[l] = thisHurtbox;

                                            break;
                                        }

                                    case 7:
                                        {
                                            if (thisMove.Type7s == null)
                                            {
                                                thisMove.Type7s = new Type7[count];
                                            }

                                            Type7 thisType7 = new Type7();
                                            thisType7.TickStart = tickStarts[l];
                                            thisType7.TickEnd = tickEnds[l];
                                            thisType7.X = inFile.ReadSingle();
                                            thisType7.Y = inFile.ReadSingle();
                                            thisType7.Z = inFile.ReadSingle();
                                            thisType7.Width = inFile.ReadSingle();
                                            thisType7.Height = inFile.ReadSingle();

                                            thisType7.Unknown1 = inFile.ReadInt32();

                                            thisType7.Unknown2 = inFile.ReadInt16();
                                            thisType7.Unknown3 = inFile.ReadInt16();
                                            thisType7.Unknown4 = inFile.ReadInt16();
                                            thisType7.Unknown5 = inFile.ReadInt16();

                                            thisType7.Unknown6 = inFile.ReadInt32();


                                            Debug.WriteLine(
                                                "thisType7 - Tickstart: {0}, TickEnd: {1}, X: {2}, Y: {3}, Rot: {4}, Width: {5}, Height: {6}, U1: {7}, U2: {8}, U3: {9}, U4: {10}, U5: {11}, U6: {12}",
                                                thisType7.TickStart, thisType7.TickEnd, thisType7.X, thisType7.Y,
                                                thisType7.Z, thisType7.Width, thisType7.Height,
                                                thisType7.Unknown1, thisType7.Unknown2, thisType7.Unknown3,
                                                thisType7.Unknown4, thisType7.Unknown5, thisType7.Unknown6);

                                            thisMove.Type7s[l] = thisType7;

                                            break;
                                        }

                                    case 8:
                                        {
                                            if (thisMove.Animations == null)
                                            {
                                                thisMove.Animations = new Animation[count];
                                            }

                                            Animation thisType8 = new Animation();
                                            thisType8.TickStart = tickStarts[l];
                                            thisType8.TickEnd = tickEnds[l];
                                            thisType8.Index = inFile.ReadInt16();
                                            thisType8.Type = (AnimationEnum)inFile.ReadInt16();
                                            thisType8.FrameStart = inFile.ReadInt16();
                                            thisType8.FrameEnd = inFile.ReadInt16();
                                            thisType8.Unknown1 = inFile.ReadInt32();
                                            thisType8.Unknown2 = inFile.ReadInt32();

                                            Debug.WriteLine(
                                                "thisType8 - Tickstart: {0}, TickEnd: {1}, Index: {2}, Type: {3}, FrameStart: {4}, FrameEnd: {5}, U1: {6}, U2: {7}",
                                                thisType8.TickStart, thisType8.TickEnd, thisType8.Index, thisType8.Type,
                                                thisType8.FrameStart, thisType8.FrameEnd, thisType8.Unknown1,
                                                thisType8.Unknown2);

                                            thisMove.Animations[l] = thisType8;

                                            break;
                                        }

                                    case 9:
                                        {
                                            if (thisMove.Type9s == null)
                                            {
                                                thisMove.Type9s = new Type9[count];
                                            }

                                            Type9 thisType9 = new Type9();
                                            thisType9.TickStart = tickStarts[l];
                                            thisType9.TickEnd = tickEnds[l];
                                            thisType9.Unknown1 = inFile.ReadInt16();
                                            thisType9.Unknown2 = inFile.ReadInt16();
                                            thisType9.Unknown3 = inFile.ReadSingle();


                                            Debug.WriteLine(
                                                "thisType9 - TickStart: {0}, TickEnd: {1}, Unknown1: {2}, Unknown2: {3}, Unknown3:{4}",
                                                thisType9.TickStart, thisType9.TickEnd, thisType9.Unknown1,
                                                thisType9.Unknown2, thisType9.Unknown3);

                                            thisMove.Type9s[l] = thisType9;

                                            break;
                                        }

                                    case 10:
                                        {
                                            if (thisMove.SoundEffects == null)
                                            {
                                                thisMove.SoundEffects = new SoundEffect[count];
                                            }

                                            SoundEffect thisType10 = new SoundEffect();
                                            thisType10.TickStart = tickStarts[l];
                                            thisType10.TickEnd = tickEnds[l];
                                            thisType10.Unknown1 = inFile.ReadInt16();
                                            thisType10.Unknown2 = inFile.ReadInt16();
                                            thisType10.Unknown3 = inFile.ReadInt16();

                                            thisType10.Unknown4 = inFile.ReadInt16();
                                            thisType10.Unknown5 = inFile.ReadInt16();
                                            thisType10.Unknown6 = inFile.ReadInt16();


                                            Debug.WriteLine(
                                                "thisType10 - TickStart: {0}, TickEnd: {1}, Unknown1: {2}, Unknown2: {3}, Unknown3:{4}, Unknown4: {5}, Unknown5: {6}, Unknown6:{7}",
                                                thisType10.TickStart, thisType10.TickEnd, thisType10.Unknown1,
                                                thisType10.Unknown2, thisType10.Unknown3, thisType10.Unknown4,
                                                thisType10.Unknown5, thisType10.Unknown6);

                                            thisMove.SoundEffects[l] = thisType10;

                                            break;
                                        }

                                    case 11:
                                        {
                                            if (thisMove.VisualEffects == null)
                                            {
                                                thisMove.VisualEffects = new VisualEffect[count];
                                            }

                                            long thisPosition = inFile.BaseStream.Position;

                                            VisualEffect thisType11 = new VisualEffect();
                                            thisType11.TickStart = tickStarts[l];
                                            thisType11.TickEnd = tickEnds[l];
                                            thisType11.Unknown1 = inFile.ReadInt16();
                                            thisType11.Unknown2 = inFile.ReadInt16();
                                            thisType11.Unknown3 = inFile.ReadInt16();

                                            thisType11.Type = inFile.ReadInt16();
                                            thisType11.Unknown5 = inFile.ReadInt16();
                                            thisType11.AttachPoint = inFile.ReadInt16();

                                            thisType11.X = inFile.ReadSingle();
                                            thisType11.Y = inFile.ReadSingle();
                                            thisType11.Z = inFile.ReadSingle();

                                            thisType11.Unknown10 = inFile.ReadInt32();
                                            thisType11.Size = inFile.ReadSingle();
                                            thisType11.Unknown12 = inFile.ReadSingle();

                                            Debug.WriteLine(
                                                "thisType11 - TickStart: {0}, TickEnd: {1}, Unknown1: {2}, Unknown2: {3}, Unknown3:{4}, Type: {5}, Unknown5: {6}, AttachPoint: {7}, X: {8}, Y: {9}, Z: {10}, Unknown10: {11}, Size: {12}, Unknown12:{13}, filePos: {14}",
                                                thisType11.TickStart, thisType11.TickEnd, thisType11.Unknown1,
                                                thisType11.Unknown2, thisType11.Unknown3, thisType11.Type,
                                                thisType11.Unknown5, thisType11.AttachPoint, thisType11.X,
                                                thisType11.Y, thisType11.Z, thisType11.Unknown10,
                                                thisType11.Size, thisType11.Unknown12, thisPosition.ToString("X"));

                                            thisMove.VisualEffects[l] = thisType11;

                                            break;
                                        }

                                    case 12:
                                        {
                                            if (thisMove.Positions == null)
                                            {
                                                thisMove.Positions = new Position[count];
                                            }

                                            long thisFilePos = inFile.BaseStream.Position;

                                            Position thisPosition = new Position();
                                            thisPosition.TickStart = tickStarts[l];
                                            thisPosition.TickEnd = tickEnds[l];
                                            thisPosition.Movement = inFile.ReadSingle();
                                            thisPosition.Flag = inFile.ReadInt32();
                                            //   inFile.ReadInt32();

                                            Debug.WriteLine(
                                                "thisPosition - TickStart: {0}, TickEnd: {1}, Unknown1: {2}, Flag: {3}, filePos: {4}",
                                                thisPosition.TickStart, thisPosition.TickEnd, thisPosition.Movement,
                                                thisPosition.Flag.ToString("X"), thisFilePos.ToString("X"));

                                            thisMove.Positions[l] = thisPosition;

                                            break;
                                        }

                                }
                            }
                        }

                        foreach (var typeInfo in TypeInfoList)
                        {
                            Debug.WriteLine("Type:");
                            Debug.WriteLine("TickOffset: {0} DataOffset: {1} TypeNumber: {2} NumberOfType: {3}, TickAddress: {4}, DataAddress {5}", typeInfo.TickOffset.ToString("X"), typeInfo.DataOffset.ToString("X"), typeInfo.TypeNumber, typeInfo.NumberOfType, typeInfo.TickAddress.ToString("X"), typeInfo.DataAddress.ToString("X"));
                            if (typeInfo.DataOffset > Size && Size > 0)
                            {
                                Debug.WriteLine("DataOffset BIGGER THAN Size????");
                            }
                        }

                        MoveList.Add(thisMove);
                        file.EffMoveLists[i].EffMoves[j] = thisMove;
                    }
                }

                inFile.BaseStream.Seek(startOfHitboxEffects, SeekOrigin.Begin);

                List<int> HitboxEffectAddresses = new List<int>();

                for (int i = 0; i < HitboxEffectCount; i++)
                {
                    HitboxEffectAddresses.Add(inFile.ReadInt32());
                }

                for (int i = 0; i < HitboxEffectAddresses.Count; i++)
                {
                    HitboxEffects thisHitboxEffects = new HitboxEffects();

                    thisHitboxEffects.Index = i;

                    int thisHitboxEffectAddress = HitboxEffectAddresses[i];

                    if (thisHitboxEffectAddress == 0)
                    {
                        HitboxEffectsList.Add(thisHitboxEffects);
                        continue;
                    }

                    Debug.WriteLine("HitboxEffects at pos: " + thisHitboxEffectAddress.ToString("X") + "  -  Index:" + i);

                    for (int j = 0; j < 20; j++)
                    {
                        inFile.BaseStream.Seek(thisHitboxEffectAddress + (j * 4), SeekOrigin.Begin);

                        int thisTypeAddress = inFile.ReadInt32() + thisHitboxEffectAddress;

                        inFile.BaseStream.Seek(thisTypeAddress, SeekOrigin.Begin);

                        HitboxEffect hitboxEffect = new HitboxEffect()
                        {
                            Type = inFile.ReadInt16(),
                            Index = inFile.ReadInt16(),
                            Unk1 = inFile.ReadInt32(),
                            Unused1 = inFile.ReadByte(),
                            NumberOfType1 = inFile.ReadByte(),
                            NumberOfType2 = inFile.ReadByte(),
                            Unused2 = inFile.ReadByte(),
                            Damage = inFile.ReadInt16(),
                            Stun = inFile.ReadInt16(),
                            Index9 = inFile.ReadInt32(),
                            Index10 = inFile.ReadInt16(),
                            Index11 = inFile.ReadInt16(),
                            Index12 = inFile.ReadInt32(),
                            Index13 = inFile.ReadInt32(),
                            Index14 = inFile.ReadInt16(),
                            Index15 = inFile.ReadInt16(),
                            Index16 = inFile.ReadInt16(),
                            Index17 = inFile.ReadInt16(),
                            Index18 = inFile.ReadInt16(),
                            Index19 = inFile.ReadInt16(),
                            Index20 = inFile.ReadSingle(),
                            Index21 = inFile.ReadSingle(),
                            Index22 = inFile.ReadInt32(),
                            Index23 = inFile.ReadInt32(),
                            Index24 = inFile.ReadInt32(),
                            Index25 = inFile.ReadInt32(),
                            OffsetToStartOfType1 = inFile.ReadInt32(),
                            OffsetToStartOfType2 = inFile.ReadInt32()
                        };

                        hitboxEffect.Type1s = new HitboxEffectType1[hitboxEffect.NumberOfType1];
                        hitboxEffect.Type2s = new HitboxEffectType2[hitboxEffect.NumberOfType2];

                        int startOfType1 = hitboxEffect.OffsetToStartOfType1 + thisTypeAddress;
                        int startOfType2 = hitboxEffect.OffsetToStartOfType2 + thisTypeAddress;

                        if (hitboxEffect.NumberOfType1 > 0)
                        {
                            inFile.BaseStream.Seek(startOfType1, SeekOrigin.Begin);

                            for (int m = 0; m < hitboxEffect.NumberOfType1; m++)
                            {
                                HitboxEffectType1 thisType1 = new HitboxEffectType1()
                                {
                                    Unknown1 = inFile.ReadInt16(),
                                    Unknown2 = inFile.ReadInt16(),
                                    Unknown3 = inFile.ReadInt32(),
                                    Unknown4 = inFile.ReadInt32()
                                };

                                hitboxEffect.Type1s[m] = thisType1;
                            }
                        }

                        if (hitboxEffect.NumberOfType2 > 0)
                        {
                            inFile.BaseStream.Seek(startOfType2, SeekOrigin.Begin);

                            for (int m = 0; m < hitboxEffect.NumberOfType2; m++)
                            {
                                HitboxEffectType2 thisForce = new HitboxEffectType2()
                                {
                                    Unknown1 = inFile.ReadInt32(),
                                    Unknown2 = inFile.ReadInt16(),
                                    Unknown3 = inFile.ReadInt16(),
                                    Unknown4 = inFile.ReadInt16(),
                                    Unknown5 = inFile.ReadInt16(),
                                    Unknown6 = inFile.ReadInt32(),
                                    Unknown7 = inFile.ReadInt32(),
                                    Unknown8 = inFile.ReadInt32(),
                                    Unknown9 = inFile.ReadInt32(),
                                    Unknown10 = inFile.ReadSingle(),
                                    Unknown11 = inFile.ReadInt32()
                                };

                                hitboxEffect.Type2s[m] = thisForce;
                            }
                        }

                        Debug.WriteLine("TypeAddress: " + j + " - " + thisTypeAddress.ToString("X") + " RealType: " + hitboxEffect.Type + " Size: " /*+ (nextTypeAddress- thisTypeAddress).ToString("X")*/ + " Type1's: " + hitboxEffect.NumberOfType1 + " Type2's: " + hitboxEffect.NumberOfType2);

                        switch (j)
                        {
                            case 0:
                                {
                                    thisHitboxEffects.HIT_STAND = hitboxEffect;
                                    break;
                                }
                            case 1:
                                {
                                    thisHitboxEffects.HIT_CROUCH = hitboxEffect;
                                    break;
                                }
                            case 2:
                                {
                                    thisHitboxEffects.HIT_AIR = hitboxEffect;
                                    break;
                                }
                            case 3:
                                {
                                    thisHitboxEffects.HIT_UNKNOWN = hitboxEffect;
                                    break;
                                }
                            case 4:
                                {
                                    thisHitboxEffects.HIT_UNKNOWN2 = hitboxEffect;
                                    break;
                                }
                            case 5:
                                {
                                    thisHitboxEffects.GUARD_STAND = hitboxEffect;
                                    break;
                                }
                            case 6:
                                {
                                    thisHitboxEffects.GUARD_CROUCH = hitboxEffect;
                                    break;
                                }
                            case 7:
                                {
                                    thisHitboxEffects.GUARD_AIR = hitboxEffect;
                                    break;
                                }
                            case 8:
                                {
                                    thisHitboxEffects.GUARD_UNKNOWN = hitboxEffect;
                                    break;
                                }
                            case 9:
                                {
                                    thisHitboxEffects.GUARD_UNKNOWN2 = hitboxEffect;
                                    break;
                                }
                            case 10:
                                {
                                    thisHitboxEffects.COUNTERHIT_STAND = hitboxEffect;
                                    break;
                                }
                            case 11:
                                {
                                    thisHitboxEffects.COUNTERHIT_CROUCH = hitboxEffect;
                                    break;
                                }
                            case 12:
                                {
                                    thisHitboxEffects.COUNTERHIT_AIR = hitboxEffect;
                                    break;
                                }
                            case 13:
                                {
                                    thisHitboxEffects.COUNTERHIT_UNKNOWN = hitboxEffect;
                                    break;
                                }
                            case 14:
                                {
                                    thisHitboxEffects.COUNTERHIT_UNKNOWN2 = hitboxEffect;
                                    break;
                                }
                            case 15:
                                {
                                    thisHitboxEffects.UNKNOWN_STAND = hitboxEffect;
                                    break;
                                }
                            case 16:
                                {
                                    thisHitboxEffects.UNKNOWN_CROUCH = hitboxEffect;
                                    break;
                                }
                            case 17:
                                {
                                    thisHitboxEffects.UNKNOWN_AIR = hitboxEffect;
                                    break;
                                }
                            case 18:
                                {
                                    thisHitboxEffects.UNKNOWN_UNKNOWN = hitboxEffect;
                                    break;
                                }
                            case 19:
                                {
                                    thisHitboxEffects.UNKNOWN_UNKNOWN2 = hitboxEffect;
                                    break;
                                }
                        }
                    }

                    HitboxEffectsList.Add(thisHitboxEffects);
                }

                file.HitboxEffectses = HitboxEffectsList.ToArray();

                return file;
            }
        }

        private static void WriteInt32ToPosition(BinaryWriter outFile, long position, int Value)
        {
            long oldPosition = outFile.BaseStream.Position;
            outFile.BaseStream.Seek(position, SeekOrigin.Begin);
            outFile.Write(Value);
            outFile.BaseStream.Seek(oldPosition, SeekOrigin.Begin);
        }

        public static void ToUassetFile(BACeffFile file, string OutPutFileName)
        {
            byte[] outPutFileBytes;

            using (var ms = new MemoryStream())
            using (var outFile = new BinaryWriter(ms))
            {
                byte[] headerBytes =
                {
                    0x23, 0x42, 0x41, 0x43, 0xFE, 0xFF, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00
                };

                outFile.Write(headerBytes);

                outFile.Write((short)file.EffMoveLists.Count());
                outFile.Write((short)file.HitboxEffectses.Count());

                long StartOfStartOfMoveTableOffsets = outFile.BaseStream.Position;
                Debug.WriteLine("StartOfStartOfMoveTableOffsets: " + StartOfStartOfMoveTableOffsets.ToString("X"));
                outFile.Write(0);


                long StartOfStartOfHitboxEffectsOffsets = outFile.BaseStream.Position;
                Debug.WriteLine("StartOfStartOfHitboxEffectsOffsets: " + StartOfStartOfHitboxEffectsOffsets.ToString("X"));
                outFile.Write(0);


                long StartOfMoveTableOffsets = outFile.BaseStream.Position;
                WriteInt32ToPosition(outFile, StartOfStartOfMoveTableOffsets, (int)StartOfMoveTableOffsets);
                Debug.WriteLine("StartOfMoveTableOffsets: " + StartOfMoveTableOffsets.ToString("X"));

                for (int i = 0; i < file.EffMoveLists.Count(); i++)
                {
                    outFile.Write(0);
                }


                long StartOfHitboxEffectsOffsets = outFile.BaseStream.Position;
                WriteInt32ToPosition(outFile, StartOfStartOfHitboxEffectsOffsets, (int)StartOfHitboxEffectsOffsets);
                Debug.WriteLine("StartOfHitboxEffectsOffsets: " + StartOfHitboxEffectsOffsets.ToString("X"));

                for (int i = 0; i < file.HitboxEffectses.Count(); i++)
                {
                    outFile.Write(0);
                }

                Debug.WriteLine("MoveTableCount: " + file.EffMoveLists.Length);

                List<long> MoveTableBaseAddresses = new List<long>();
                List<long> MoveTableBaseNameOffsets = new List<long>();


                for (int i = 0; i < file.EffMoveLists.Length; i++)
                {
                    Debug.WriteLine("Writing MoveTable: " + i);


                    WriteInt32ToPosition(outFile, StartOfMoveTableOffsets + (i * 4), (int)outFile.BaseStream.Position);
                    long MoveTableBaseAddress = outFile.BaseStream.Position;

                    MoveTableBaseAddresses.Add(MoveTableBaseAddress);

                    outFile.Write(file.EffMoveLists[i].Unknown1);
                    outFile.Write((short)file.EffMoveLists[i].EffMoves.Length);

                    long StartOfStartOfMovesOffset = outFile.BaseStream.Position;
                    outFile.Write(0); //StartOfMovesOffset
                    long StartOfStartOfMovesNamesOffset = outFile.BaseStream.Position;
                    outFile.Write(0); //StartOfNameAddressesOffset


                    long StartOfMovesOffset = outFile.BaseStream.Position;
                    WriteInt32ToPosition(outFile, StartOfStartOfMovesOffset, (int)(StartOfMovesOffset - MoveTableBaseAddress));

                    Debug.WriteLine("MoveCount: " + file.EffMoveLists[i].EffMoves.Length);

                    foreach (var Move in file.EffMoveLists[i].EffMoves)
                    {
                        outFile.Write(0);
                    }

                    long StartOfMovesNamesOffset = outFile.BaseStream.Position;
                    MoveTableBaseNameOffsets.Add(StartOfMovesNamesOffset);
                    WriteInt32ToPosition(outFile, StartOfStartOfMovesNamesOffset, (int)(StartOfMovesNamesOffset - MoveTableBaseAddress));

                    Debug.WriteLine("Names: " + StartOfMovesNamesOffset.ToString("X"));

                    foreach (var Move in file.EffMoveLists[i].EffMoves)
                    {
                        outFile.Write(0);
                    }
                    Debug.WriteLine("Doing Moves... Current Position: " + outFile.BaseStream.Position.ToString("X"));

                    int j = 0;

                    foreach (var Move in file.EffMoveLists[i].EffMoves)
                    {
                        if (Move == null)
                        {
                            WriteInt32ToPosition(outFile, StartOfMovesOffset + (j * 4), 0);
                            j++;
                            continue;
                        }

                        WriteInt32ToPosition(outFile, StartOfMovesOffset + (j * 4), (int)(outFile.BaseStream.Position - MoveTableBaseAddress));

                        outFile.Write(Move.FirstHitboxFrame);
                        outFile.Write(Move.LastHitboxFrame);
                        outFile.Write(Move.InterruptFrame);
                        outFile.Write(Move.TotalTicks);
                        outFile.Write(Move.unk1);
                        outFile.Write(Move.unk2);
                        outFile.Write(Move.unk3);
                        outFile.Write(Move.unk4);
                        outFile.Write(Move.unk5);
                        outFile.Write(Move.unk6);
                        outFile.Write(Move.unk7);
                        outFile.Write(Move.Flag);
                        outFile.Write(Move.unk9);

                        outFile.Write(Move.numberOfTypes);

                        outFile.Write(Move.unk13);
                        outFile.Write(Move.HeaderSize);

                        if (Move.HeaderSize == 0x58)
                        {
                            outFile.Write(Move.Unknown12);
                            outFile.Write(Move.Unknown13);
                            outFile.Write(Move.Unknown14);
                            outFile.Write(Move.Unknown15);
                            outFile.Write(Move.Unknown16);
                            outFile.Write(Move.Unknown17);
                            outFile.Write(Move.Unknown18);
                            outFile.Write(Move.Unknown19);
                            outFile.Write(Move.Unknown20);
                            outFile.Write(Move.Unknown21);
                            outFile.Write(Move.Unknown22);
                        }

                        long type0TickOffsetAddress = 0;
                        long type1TickOffsetAddress = 0;
                        long ForceTickOffsetAddress = 0;
                        long type3TickOffsetAddress = 0;
                        long type4TickOffsetAddress = 0;
                        long HitboxTickOffsetAddress = 0;
                        long HurtboxTickOffsetAddress = 0;
                        long type7TickOffsetAddress = 0;
                        long type8TickOffsetAddress = 0;
                        long type9TickOffsetAddress = 0;
                        long type10TickOffsetAddress = 0;
                        long type11TickOffsetAddress = 0;
                        long PositionTickOffsetAddress = 0;


                        if (Move.AutoCancels != null && Move.AutoCancels.Length > 0)
                        {
                            outFile.Write((short)0);
                            outFile.Write((short)Move.AutoCancels.Length);
                            type0TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Type1s != null && Move.Type1s.Length > 0)
                        {
                            outFile.Write((short)1);
                            outFile.Write((short)Move.Type1s.Length);
                            type1TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Forces != null && Move.Forces.Length > 0)
                        {
                            outFile.Write((short)2);
                            outFile.Write((short)Move.Forces.Length);
                            ForceTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Cancels != null && Move.Cancels.Length > 0)
                        {
                            outFile.Write((short)3);
                            outFile.Write((short)Move.Cancels.Length);
                            type3TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Type4s != null && Move.Type4s.Length > 0)
                        {
                            outFile.Write((short)4);
                            outFile.Write((short)Move.Type4s.Length);
                            type4TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Hitboxes != null && Move.Hitboxes.Length > 0)
                        {
                            outFile.Write((short)5);
                            outFile.Write((short)Move.Hitboxes.Length);
                            HitboxTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Hurtboxes != null && Move.Hurtboxes.Length > 0)
                        {
                            outFile.Write((short)6);
                            outFile.Write((short)Move.Hurtboxes.Length);
                            HurtboxTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Type7s != null && Move.Type7s.Length > 0)
                        {
                            outFile.Write((short)7);
                            outFile.Write((short)Move.Type7s.Length);
                            type7TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Animations != null && Move.Animations.Length > 0)
                        {
                            outFile.Write((short)8);
                            outFile.Write((short)Move.Animations.Length);
                            type8TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Type9s != null && Move.Type9s.Length > 0)
                        {
                            outFile.Write((short)9);
                            outFile.Write((short)Move.Type9s.Length);
                            type9TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.SoundEffects != null && Move.SoundEffects.Length > 0)
                        {
                            outFile.Write((short)10);
                            outFile.Write((short)Move.SoundEffects.Length);
                            type10TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.VisualEffects != null && Move.VisualEffects.Length > 0)
                        {
                            outFile.Write((short)11);
                            outFile.Write((short)Move.VisualEffects.Length);
                            type11TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }
                        if (Move.Positions != null && Move.Positions.Length > 0)
                        {
                            outFile.Write((short)12);
                            outFile.Write((short)Move.Positions.Length);
                            PositionTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                        }

                        if (Move.AutoCancels != null && Move.AutoCancels.Length > 0)
                        {
                            List<long> type0Offsets = new List<long>();
                            List<long> IntOffsets = new List<long>();
                            List<long> IntPositions = new List<long>();

                            WriteInt32ToPosition(outFile, type0TickOffsetAddress, (int)(outFile.BaseStream.Position - (type0TickOffsetAddress - 4)));

                            foreach (var type0 in Move.AutoCancels)
                            {
                                outFile.Write(type0.TickStart);
                                outFile.Write(type0.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type0TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type0TickOffsetAddress - 4)));

                            foreach (var type0 in Move.AutoCancels)
                            {
                                if (type0.Ints.Length != 0)
                                {
                                    type0Offsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write((short)type0.Condition);
                                outFile.Write(type0.MoveIndex);
                                outFile.Write(type0.Unknown1);
                                outFile.Write(type0.NumberOfInts);
                                outFile.Write(type0.Unknown2);
                                if (type0.Ints.Length != 0)
                                {
                                    IntOffsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write(0);
                            }

                            foreach (var type0 in Move.AutoCancels)
                            {
                                if (type0.Ints.Length != 0)
                                {
                                    IntPositions.Add(outFile.BaseStream.Position);
                                    foreach (var i1 in type0.Ints)
                                    {
                                        outFile.Write(i1);
                                    }
                                }
                            }

                            for (int k = 0; k < type0Offsets.Count; k++)
                            {
                                WriteInt32ToPosition(outFile, IntOffsets[k], (int)(IntPositions[k] - type0Offsets[k]));
                            }
                        }

                        if (Move.Type1s != null && Move.Type1s.Length > 0)
                        {
                            WriteInt32ToPosition(outFile, type1TickOffsetAddress, (int)(outFile.BaseStream.Position - (type1TickOffsetAddress - 4)));

                            foreach (var type1 in Move.Type1s)
                            {
                                outFile.Write(type1.TickStart);
                                outFile.Write(type1.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type1TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type1TickOffsetAddress - 4)));

                            foreach (var type1 in Move.Type1s)
                            {
                                outFile.Write(type1.Flag1);
                                outFile.Write(type1.Flag2);
                            }
                        }

                        if (Move.Forces != null && Move.Forces.Length > 0)
                        {
                            WriteInt32ToPosition(outFile, ForceTickOffsetAddress, (int)(outFile.BaseStream.Position - (ForceTickOffsetAddress - 4)));

                            foreach (var Force in Move.Forces)
                            {
                                outFile.Write(Force.TickStart);
                                outFile.Write(Force.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, ForceTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (ForceTickOffsetAddress - 4)));

                            foreach (var Force in Move.Forces)
                            {
                                outFile.Write(Force.Amount);
                                outFile.Write((int)Force.Flag);
                            }
                        }

                        if (Move.Cancels != null && Move.Cancels.Length > 0)
                        {
                            WriteInt32ToPosition(outFile, type3TickOffsetAddress, (int)(outFile.BaseStream.Position - (type3TickOffsetAddress - 4)));

                            foreach (var type3 in Move.Cancels)
                            {
                                outFile.Write(type3.TickStart);
                                outFile.Write(type3.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type3TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type3TickOffsetAddress - 4)));

                            foreach (var type3 in Move.Cancels)
                            {
                                outFile.Write(type3.CancelList);
                                outFile.Write(type3.Type);
                            }
                        }

                        if (Move.Type4s != null && Move.Type4s.Length > 0)
                        {
                            List<long> type4Offsets = new List<long>();
                            List<long> IntOffsets = new List<long>();
                            List<long> IntPositions = new List<long>();

                            WriteInt32ToPosition(outFile, type4TickOffsetAddress, (int)(outFile.BaseStream.Position - (type4TickOffsetAddress - 4)));

                            foreach (var type4 in Move.Type4s)
                            {
                                outFile.Write(type4.TickStart);
                                outFile.Write(type4.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type4TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type4TickOffsetAddress - 4)));

                            foreach (var type4 in Move.Type4s)
                            {
                                if (type4.Ints.Length != 0)
                                {
                                    type4Offsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write(type4.Unknown1);
                                outFile.Write(type4.Unknown2);
                                outFile.Write(type4.NumberOfInts);
                                if (type4.Ints.Length != 0)
                                {
                                    IntOffsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write(0);
                            }

                            foreach (var type4 in Move.Type4s)
                            {
                                if (type4.Ints.Length != 0)
                                {
                                    IntPositions.Add(outFile.BaseStream.Position);
                                    foreach (var i1 in type4.Ints)
                                    {
                                        outFile.Write(i1);
                                    }
                                }
                            }

                            for (int k = 0; k < type4Offsets.Count; k++)
                            {
                                WriteInt32ToPosition(outFile, IntOffsets[k], (int)(IntPositions[k] - type4Offsets[k]));
                            }
                        }

                        if (Move.Hitboxes != null && Move.Hitboxes.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, HitboxTickOffsetAddress, (int)(outFile.BaseStream.Position - (HitboxTickOffsetAddress - 4)));

                            foreach (var Hitbox in Move.Hitboxes)
                            {
                                outFile.Write(Hitbox.TickStart);
                                outFile.Write(Hitbox.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, HitboxTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (HitboxTickOffsetAddress - 4)));

                            foreach (var Hitbox in Move.Hitboxes)
                            {
                                outFile.Write(Hitbox.X);
                                outFile.Write(Hitbox.Y);
                                outFile.Write(Hitbox.Z);
                                outFile.Write(Hitbox.Width);
                                outFile.Write(Hitbox.Height);

                                outFile.Write(Hitbox.Unknown1);

                                outFile.Write(Hitbox.Unknown2);
                                outFile.Write(Hitbox.Unknown3);
                                outFile.Write(Hitbox.Unknown4);
                                outFile.Write(Hitbox.Unknown5);

                                outFile.Write(Hitbox.Unknown6);
                                outFile.Write(Hitbox.Unknown7);
                                outFile.Write(Hitbox.Unknown8);
                                outFile.Write(Hitbox.Unknown9);

                                outFile.Write(Hitbox.Flag1);
                                outFile.Write(Hitbox.Flag2);
                                outFile.Write(Hitbox.Flag3);
                                outFile.Write(Hitbox.Flag4);

                                outFile.Write(Hitbox.HitEffect);
                                outFile.Write(Hitbox.Unknown10);
                                outFile.Write(Hitbox.Unknown11);
                            }
                        }

                        if (Move.Hurtboxes != null && Move.Hurtboxes.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, HurtboxTickOffsetAddress, (int)(outFile.BaseStream.Position - (HurtboxTickOffsetAddress - 4)));

                            foreach (var Hurtbox in Move.Hurtboxes)
                            {
                                outFile.Write(Hurtbox.TickStart);
                                outFile.Write(Hurtbox.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, HurtboxTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (HurtboxTickOffsetAddress - 4)));

                            foreach (var Hurtbox in Move.Hurtboxes)
                            {
                                outFile.Write(Hurtbox.X);
                                outFile.Write(Hurtbox.Y);
                                outFile.Write(Hurtbox.Z);
                                outFile.Write(Hurtbox.Width);
                                outFile.Write(Hurtbox.Height);

                                outFile.Write(Hurtbox.Unknown1);

                                outFile.Write(Hurtbox.Unknown2);
                                outFile.Write(Hurtbox.Unknown3);
                                outFile.Write(Hurtbox.Unknown4);
                                outFile.Write(Hurtbox.Unknown5);

                                outFile.Write(Hurtbox.Unknown6);
                                outFile.Write(Hurtbox.Unknown7);
                                outFile.Write(Hurtbox.Unknown8);
                                outFile.Write(Hurtbox.Unknown9);

                                outFile.Write(Hurtbox.Flag1);
                                outFile.Write(Hurtbox.Flag2);
                                outFile.Write(Hurtbox.Flag3);
                                outFile.Write(Hurtbox.Flag4);

                                outFile.Write(Hurtbox.HitEffect);
                                outFile.Write(Hurtbox.Unknown10);
                                outFile.Write(Hurtbox.Unknown11);

                                outFile.Write(Hurtbox.Unknown12);
                            }
                        }

                        if (Move.Type7s != null && Move.Type7s.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, type7TickOffsetAddress, (int)(outFile.BaseStream.Position - (type7TickOffsetAddress - 4)));

                            foreach (var type7 in Move.Type7s)
                            {
                                outFile.Write(type7.TickStart);
                                outFile.Write(type7.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type7TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type7TickOffsetAddress - 4)));

                            foreach (var type7 in Move.Type7s)
                            {
                                outFile.Write(type7.X);
                                outFile.Write(type7.Y);
                                outFile.Write(type7.Z);
                                outFile.Write(type7.Width);
                                outFile.Write(type7.Height);

                                outFile.Write(type7.Unknown1);

                                outFile.Write(type7.Unknown2);
                                outFile.Write(type7.Unknown3);
                                outFile.Write(type7.Unknown4);
                                outFile.Write(type7.Unknown5);

                                outFile.Write(type7.Unknown6);
                            }
                        }

                        if (Move.Animations != null && Move.Animations.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, type8TickOffsetAddress, (int)(outFile.BaseStream.Position - (type8TickOffsetAddress - 4)));

                            foreach (var type8 in Move.Animations)
                            {
                                outFile.Write(type8.TickStart);
                                outFile.Write(type8.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type8TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type8TickOffsetAddress - 4)));

                            foreach (var type8 in Move.Animations)
                            {
                                outFile.Write(type8.Index);
                                outFile.Write((short)type8.Type);
                                outFile.Write(type8.FrameStart);
                                outFile.Write(type8.FrameEnd);
                                outFile.Write(type8.Unknown1);
                                outFile.Write(type8.Unknown2);
                            }
                        }

                        if (Move.Type9s != null && Move.Type9s.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, type9TickOffsetAddress, (int)(outFile.BaseStream.Position - (type9TickOffsetAddress - 4)));

                            foreach (var type9 in Move.Type9s)
                            {
                                outFile.Write(type9.TickStart);
                                outFile.Write(type9.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type9TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type9TickOffsetAddress - 4)));

                            foreach (var type9 in Move.Type9s)
                            {
                                outFile.Write(type9.Unknown1);
                                outFile.Write(type9.Unknown2);
                                outFile.Write(type9.Unknown3);
                            }
                        }

                        if (Move.SoundEffects != null && Move.SoundEffects.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, type10TickOffsetAddress, (int)(outFile.BaseStream.Position - (type10TickOffsetAddress - 4)));

                            foreach (var type10 in Move.SoundEffects)
                            {
                                outFile.Write(type10.TickStart);
                                outFile.Write(type10.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type10TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type10TickOffsetAddress - 4)));

                            foreach (var type10 in Move.SoundEffects)
                            {
                                outFile.Write(type10.Unknown1);
                                outFile.Write(type10.Unknown2);
                                outFile.Write(type10.Unknown3);

                                outFile.Write(type10.Unknown4);
                                outFile.Write(type10.Unknown5);
                                outFile.Write(type10.Unknown6);
                            }
                        }

                        if (Move.VisualEffects != null && Move.VisualEffects.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, type11TickOffsetAddress, (int)(outFile.BaseStream.Position - (type11TickOffsetAddress - 4)));

                            foreach (var type11 in Move.VisualEffects)
                            {
                                outFile.Write(type11.TickStart);
                                outFile.Write(type11.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, type11TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type11TickOffsetAddress - 4)));

                            foreach (var type11 in Move.VisualEffects)
                            {
                                outFile.Write(type11.Unknown1);
                                outFile.Write(type11.Unknown2);
                                outFile.Write(type11.Unknown3);

                                outFile.Write(type11.Type);
                                outFile.Write(type11.Unknown5);
                                outFile.Write(type11.AttachPoint);

                                outFile.Write(type11.X);
                                outFile.Write(type11.Y);
                                outFile.Write(type11.Z);

                                outFile.Write(type11.Unknown10);
                                outFile.Write(type11.Size);
                                outFile.Write(type11.Unknown12);
                            }
                        }

                        if (Move.Positions != null && Move.Positions.Length > 0)
                        {

                            WriteInt32ToPosition(outFile, PositionTickOffsetAddress, (int)(outFile.BaseStream.Position - (PositionTickOffsetAddress - 4)));

                            foreach (var Position in Move.Positions)
                            {
                                outFile.Write(Position.TickStart);
                                outFile.Write(Position.TickEnd);
                            }

                            WriteInt32ToPosition(outFile, PositionTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (PositionTickOffsetAddress - 4)));

                            foreach (var Position in Move.Positions)
                            {
                                outFile.Write(Position.Movement);
                                outFile.Write(Position.Flag);
                            }
                        }

                        j++;
                    }

                }


                Debug.WriteLine("Done with Moves! Now doing HitboxEffects... CurrentPos: " + outFile.BaseStream.Position.ToString("X"));

                for (int i = 0; i < file.HitboxEffectses.Length; i++)
                {
                    if (file.HitboxEffectses[i].HIT_STAND == null)
                    {
                        WriteInt32ToPosition(outFile, StartOfHitboxEffectsOffsets + (i * 4), 0);
                        continue;
                    }

                    WriteInt32ToPosition(outFile, StartOfHitboxEffectsOffsets + (i * 4), (int)outFile.BaseStream.Position);

                    long HitboxEffectsBasePosition = outFile.BaseStream.Position;

                    for (int j = 0; j < 20; j++)
                    {
                        outFile.Write(0);
                    }

                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_STAND);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 4, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_CROUCH);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 8, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_AIR);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 12, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_UNKNOWN);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 16, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_UNKNOWN2);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 20, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_STAND);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 24, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_CROUCH);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 28, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_AIR);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 32, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_UNKNOWN);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 36, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_UNKNOWN2);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 40, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_STAND);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 44, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_CROUCH);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 48, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_AIR);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 52, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_UNKNOWN);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 56, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_UNKNOWN2);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 60, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_STAND);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 64, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_CROUCH);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 68, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_AIR);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 72, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_UNKNOWN);
                    WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 76, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_UNKNOWN2);
                }

                Debug.WriteLine("Done with HitboxEffects! Now doing Names... CurrentPos: " + outFile.BaseStream.Position.ToString("X"));

                for (int i = 0; i < file.EffMoveLists.Length; i++)
                {
                    List<long> NamePositions = new List<long>();

                    for (int j = 0; j < file.EffMoveLists[i].EffMoves.Length; j++)
                    {
                        if (file.EffMoveLists[i].EffMoves[j] == null)
                        {
                            continue;
                        }

                        NamePositions.Add(outFile.BaseStream.Position);
                        outFile.Write(file.EffMoveLists[i].EffMoves[j].Name.ToCharArray());

                        outFile.Write((byte)0x00);
                    }

                    for (int k = 0; k < file.EffMoveLists[i].EffMoves.Length; k++)
                    {
                        if (file.EffMoveLists[i].EffMoves[k] == null)
                        {
                            WriteInt32ToPosition(outFile, MoveTableBaseNameOffsets[i] + (k * 4), 0);
                            continue;
                        }
                        WriteInt32ToPosition(outFile, MoveTableBaseNameOffsets[i] + (k * 4), (int)(NamePositions[0] - MoveTableBaseAddresses[i]));
                        NamePositions.RemoveAt(0);
                    }
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

            outPut.AddRange(new byte[]
            {
                0x09, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            });

            var tempLengthBytes = BitConverter.GetBytes(outPut.Count);
            byte[] UassetHeader = file.RawUassetHeaderDontTouch;
            UassetHeader[0x1b8] = tempLengthBytes[0];
            UassetHeader[0x1b9] = tempLengthBytes[1];
            UassetHeader[0x1ba] = tempLengthBytes[2];
            UassetHeader[0x1bb] = tempLengthBytes[3];

            outPut.InsertRange(0, UassetHeader);

            byte[] UassetEnd = new byte[4];
            UassetHeader.ToList().CopyTo(0, UassetEnd, 0, 4);

            outPut.AddRange(UassetEnd);

            Debug.WriteLine("Done.");

            File.WriteAllBytes(OutPutFileName, outPut.ToArray());
        }

        private static void WriteHitboxEffect(BinaryWriter outFile, HitboxEffect effect)
        {
            long StartOfEffectAddress = outFile.BaseStream.Position;

            outFile.Write(effect.Type);
            outFile.Write(effect.Index);
            outFile.Write(effect.Unk1);
            outFile.Write(effect.Unused1);
            outFile.Write(effect.NumberOfType1);
            outFile.Write(effect.NumberOfType2);
            outFile.Write(effect.Unused2);
            outFile.Write(effect.Damage);
            outFile.Write(effect.Stun);
            outFile.Write(effect.Index9);
            outFile.Write(effect.Index10);
            outFile.Write(effect.Index11);
            outFile.Write(effect.Index12);
            outFile.Write(effect.Index13);
            outFile.Write(effect.Index14);
            outFile.Write(effect.Index15);
            outFile.Write(effect.Index16);
            outFile.Write(effect.Index17);
            outFile.Write(effect.Index18);
            outFile.Write(effect.Index19);
            outFile.Write(effect.Index20);
            outFile.Write(effect.Index21);
            outFile.Write(effect.Index22);
            outFile.Write(effect.Index23);
            outFile.Write(effect.Index24);
            outFile.Write(effect.Index25);

            long OffsetToType1Address = outFile.BaseStream.Position;
            outFile.Write(0);
            outFile.Write(0);

            for (int i = 0; i < effect.Type1s.Length; i++)
            {
                if (i == 0)
                {
                    WriteInt32ToPosition(outFile, OffsetToType1Address,
                        (int)(outFile.BaseStream.Position - StartOfEffectAddress));
                }
                outFile.Write(effect.Type1s[i].Unknown1);
                outFile.Write(effect.Type1s[i].Unknown2);
                outFile.Write(effect.Type1s[i].Unknown3);
                outFile.Write(effect.Type1s[i].Unknown4);
            }

            for (int i = 0; i < effect.Type2s.Length; i++)
            {
                if (i == 0)
                {
                    WriteInt32ToPosition(outFile, OffsetToType1Address + 4,
                        (int)(outFile.BaseStream.Position - StartOfEffectAddress));
                }
                outFile.Write(effect.Type2s[i].Unknown1);
                outFile.Write(effect.Type2s[i].Unknown2);
                outFile.Write(effect.Type2s[i].Unknown3);
                outFile.Write(effect.Type2s[i].Unknown4);
                outFile.Write(effect.Type2s[i].Unknown5);
                outFile.Write(effect.Type2s[i].Unknown6);
                outFile.Write(effect.Type2s[i].Unknown7);
                outFile.Write(effect.Type2s[i].Unknown8);
                outFile.Write(effect.Type2s[i].Unknown9);
                outFile.Write(effect.Type2s[i].Unknown10);
                outFile.Write(effect.Type2s[i].Unknown11);
            }
        }

        private static string GetName(int address, BinaryReader reader)
        {
            StringBuilder sb = new StringBuilder();

            reader.BaseStream.Seek(address, SeekOrigin.Begin);

            var c = reader.ReadChar();

            while (c != 0)
            {
                sb.Append(c);
                c = reader.ReadChar();
            }

            return sb.ToString();
        }

        private static byte[] CreateGameBACFromFile(byte[] fileBytes)
        {
            var tempList = fileBytes.ToList();
            tempList.RemoveRange(0, 0x1ec + 36);
            return tempList.ToArray();
        }

        private static byte[] GetUassetHeader(byte[] fileBytes)
        {
            var tempList = fileBytes.ToList();

            byte[] array = new byte[0x1ec];
            tempList.CopyTo(0, array, 0, 0x1ec);

            return array;
        }
    }

    public class BACeffFile
    {
        public EffMoveList[] EffMoveLists { get; set; }
        public HitboxEffects[] HitboxEffectses { get; set; }
        public byte[] RawUassetHeaderDontTouch { get; set; }
    }

    public class EffMoveList
    {
        public EffMove[] EffMoves { get; set; }
        public short Unknown1 { get; set; }
    }

    public class EffMove : Move
    {
        public short Unknown12 { get; set; }
        public short Unknown13 { get; set; }

        public short Unknown14 { get; set; }
        public short Unknown15 { get; set; }
        public short Unknown16 { get; set; }
        public short Unknown17 { get; set; }
        public float Unknown18 { get; set; }
        public short Unknown19 { get; set; }

        public short Unknown20 { get; set; }

        public short Unknown21 { get; set; }
        public short Unknown22 { get; set; }
    }
}