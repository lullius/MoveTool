using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MoveLib.BAC
{
    public static class BAC
    {
        public static void BacToJson(string inFile, string outFile)
        {
            BACFile bac;

            try
            {
                bac = FromUassetFile(inFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create JSON.\n" + ex.Message + " - " + ex.Data);
                throw;
            }

            Formatting format = Formatting.Indented;

            var json = JsonConvert.SerializeObject(bac, format, new StringEnumConverter());

            File.WriteAllText(outFile, json);
        }

        public static bool JsonToBac(string inFile, string outFile)
        {
            BACFile bac;

            try
            {
                bac = JsonConvert.DeserializeObject<BACFile>(File.ReadAllText(inFile), new ForceEnumConverter(), new StringEnumConverter());
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
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static BACFile FromUassetFile(string fileName)
        {
            List<Move> MoveList = new List<Move>();
            List<HitboxEffects> HitboxEffectsList = new List<HitboxEffects>();

            byte[] fileBytes = File.ReadAllBytes(fileName);

            byte[] UassetHeaderBytes = Common.GetUassetHeader(fileBytes);
            fileBytes = Common.RemoveUassetHeader(fileBytes);

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

                inFile.BaseStream.Seek(0xA, SeekOrigin.Begin);
                short BACVER = inFile.ReadInt16();
                Debug.WriteLine("BAC version: " + BACVER);

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

                BACFile file = new BACFile();
                file.RawUassetHeaderDontTouch = UassetHeaderBytes;
                file.MoveLists = new MoveList[MoveListCount];
                file.BACVER = BACVER;

                for (int i = 0; i < baseMoveAddresses.Count; i++)
                {
                    int thisAddress = baseMoveAddresses[i];

                    Debug.WriteLine("ScriptStartTime at pos: " + thisAddress.ToString("X") + "  -  Index:" + i);

                    inFile.BaseStream.Seek(thisAddress, SeekOrigin.Begin);

                    Debug.WriteLine("Now at pos: " + inFile.BaseStream.Position.ToString("X"));

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

                    file.MoveLists[i] = new MoveList();
                    file.MoveLists[i].Unknown1 = unknown1;
                    file.MoveLists[i].Moves = new Move[MoveCount];

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
                        string Name = Common.GetName(thisNameAddress, inFile);

                        inFile.BaseStream.Seek(thisMoveAddress, SeekOrigin.Begin);

                        Debug.WriteLine("Adding Move at: " + thisMoveAddress.ToString("X"));

                        #region DebuggingPurposes

                        int size;

                        if (j == MoveAddresses.Count - 1)
                        {
                            size = 0;
                        }
                        else
                        {
                            int nextAddress = 1;
                            while (MoveAddresses[j + nextAddress] == 0)
                            {
                                nextAddress++;
                            }

                            size = (MoveAddresses[j + nextAddress] - thisMoveAddress);

                            if (size < 0)
                            {
                                Debug.WriteLine("Size was smaller than 0?? Next address:" + MoveAddresses[j+nextAddress].ToString("X") + " This Address: " + thisMoveAddress.ToString("X") );
                            }
                        }

                        Debug.WriteLine("Size: " + size);

                        #endregion

                        Move thisMove = new Move()
                        {
                            Name = Name,
                            Index = j,
                            FirstHitboxFrame = inFile.ReadInt32(),
                            LastHitboxFrame = inFile.ReadInt32(),
                            InterruptFrame = inFile.ReadInt32(),
                            TotalTicks = inFile.ReadInt32(),
                        
                            ReturnToOriginalPosition = inFile.ReadInt32(),
                            Slide = inFile.ReadSingle(),
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

                        if (thisMove.HeaderSize != 0x40)
                        {
                            Debug.WriteLine("NOT 40!!!");
                        }

                        List<TypeInfo> TypeInfoList = new List<TypeInfo>();

                        long typeListBaseOffset = inFile.BaseStream.Position;

                        for (int k = 0; k < thisMove.numberOfTypes; k++)
                        {
                            int typeSize = 12;
                            switch (BACVER)
                            {
                                case 1:
                                    {
                                        typeSize = 16;
                                        break;
                                    }
                            }

                            long thisTypeOffset = typeListBaseOffset + (typeSize*k);
                            inFile.BaseStream.Seek(thisTypeOffset, SeekOrigin.Begin);
                            short type = inFile.ReadInt16();
                            short count = inFile.ReadInt16();

                            int tickOffset = inFile.ReadInt32();
                            int dataOffset = inFile.ReadInt32();

                            Debug.WriteLine("TickOffset: " + tickOffset.ToString("X"));
                            Debug.WriteLine("dataOffset: " + dataOffset.ToString("X"));

                            List<int> BACVERintOffsets = new List<int>();

                            switch (BACVER)
                            {
                                case 1:
                                    {
                                        int BACVERint1Offset = inFile.ReadInt32();
                                        Debug.WriteLine("Unknown 4 bytes: " + BACVERint1Offset);
                                        Debug.WriteLine("Unknown 4 bytes (hex): " + BACVERint1Offset.ToString("X"));

                                        BACVERintOffsets.Add(BACVERint1Offset);
                                        break;
                                    }
                            }

                            long tickAddress = tickOffset + thisTypeOffset;
                            long dataAddress = dataOffset + thisTypeOffset;

                            List<long> BACVERintAddresses = new List<long>();

                            foreach (var offset in BACVERintOffsets)
                            {
                                BACVERintAddresses.Add(offset + thisTypeOffset);
                                Debug.WriteLine("BACVERintAddress: " +(offset + thisTypeOffset).ToString("X"));
                            }

                            Debug.WriteLine("Type: " + type + " Count: " + count + " tickAddress: " +
                                            tickAddress.ToString("X") + " dataAddress: " + dataAddress.ToString("X") +
                                            " dataEndForType4?: " + (dataAddress + (0x4c*count)).ToString("X"));

                            List<int> tickStarts = new List<int>();
                            List<int> tickEnds = new List<int>();

                            for (int l = 0; l < count; l++)
                            {
                                inFile.BaseStream.Seek(tickAddress + (8*l), SeekOrigin.Begin);
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

                                        long thisType0Address = dataAddress + (l*16);

                                            switch (BACVER)
                                            {
                                                case 1:
                                                    {
                                                        thisType0Address = dataAddress + (l * (16 + 8));
                                                        break;
                                                    }
                                            }
                                        
                                        inFile.BaseStream.Seek(thisType0Address, SeekOrigin.Begin);

                                        AutoCancel thisType0 = new AutoCancel
                                        {
                                            TickStart = tickStarts[l],
                                            TickEnd = tickEnds[l],
                                            Condition = (AutoCancelCondition) inFile.ReadInt16(),
                                            MoveIndex = inFile.ReadInt16(),
                                            ScriptStartTime = inFile.ReadInt16(),
                                            NumberOfInts = inFile.ReadInt16(),
                                            Unknown2 = inFile.ReadInt32()
                                        };

                                        switch (BACVER)
                                            {
                                                case 1:
                                                    {
                                                        thisType0.Unknown3 = inFile.ReadInt32();
                                                        thisType0.Unknown4 = inFile.ReadInt32();
                                                        break;
                                                    }
                                            }
                                      

                                        thisType0.Offset = inFile.ReadInt32();
                                        thisType0.Ints = new int[thisType0.NumberOfInts];

                                            if (thisType0.NumberOfInts != 0) //Is this correct!?
                                            {
                                                inFile.BaseStream.Seek(thisType0Address + thisType0.Offset, SeekOrigin.Begin);

                                                for (int m = 0; m < thisType0.NumberOfInts; m++)
                                                {
                                                    thisType0.Ints[m] = inFile.ReadInt32();
                                                }
                                            }

                                        Debug.WriteLine(
                                            "thisType0 - TickStart: {0}, TickEnd: {1}, Condition: {2}, MoveIndex: {3}, ScriptStartTime: {4}, NumberOfInts: {5}, Unknown2: {6} Offset: {7} Unknown3: {8} Unknown4: {9}",
                                            thisType0.TickStart, thisType0.TickEnd, thisType0.Condition,
                                            thisType0.MoveIndex, thisType0.ScriptStartTime, thisType0.NumberOfInts,
                                            thisType0.Unknown2, thisType0.Offset.ToString("X"),
                                            thisType0.Unknown3, thisType0.Unknown4);

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
                                        thisForce.Flag = inFile.ReadInt32();

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
                                        if (thisMove.Others == null)
                                        {
                                            thisMove.Others = new Other[count];
                                        }

                                        long thisType4Address = dataAddress + (l*12);
                                        inFile.BaseStream.Seek(thisType4Address, SeekOrigin.Begin);

                                        Other thisType4 = new Other();

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

                                        thisMove.Others[l] = thisType4;

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
                                        thisHitbox.NumberOfHits = inFile.ReadInt16();

                                        thisHitbox.HitType = inFile.ReadByte();
                                        thisHitbox.JuggleLimit = inFile.ReadByte();
                                        thisHitbox.JuggleIncrease = inFile.ReadByte();
                                        thisHitbox.Flag4 = inFile.ReadByte();

                                        thisHitbox.HitboxEffectIndex = inFile.ReadInt16();
                                        thisHitbox.Unknown10 = inFile.ReadInt16();
                                        thisHitbox.Unknown11 = inFile.ReadInt32();
                                        
                                        switch (BACVER)
                                            {
                                                case 1:
                                                    {
                                                        thisHitbox.Unknown12 = inFile.ReadInt32();
                                                        break;
                                                    }
                                            }

                                        Debug.WriteLine(
                                            "thisHitbox - Tickstart: {0}, TickEnd: {1}, X: {2}, Y: {3}, Rot: {4}, Width: {5}, Height: {6}, U1: {7}, U2: {8}, U3: {9}, U4: {10}, U5: {11}, U6: {12}, U7: {13}, U8: {14}, U9: {15}, Flag1: {16}, Flag2: {17}, Flag3: {18}, Flag4: {19}, HitEffect: {20}, U10: {21}, U11: {22}, U12 {23}",
                                            thisHitbox.TickStart, thisHitbox.TickEnd, thisHitbox.X, thisHitbox.Y,
                                            thisHitbox.Z, thisHitbox.Width, thisHitbox.Height,
                                            thisHitbox.Unknown1, thisHitbox.Unknown2, thisHitbox.Unknown3,
                                            thisHitbox.Unknown4, thisHitbox.Unknown5, thisHitbox.Unknown6,
                                            thisHitbox.Unknown7,
                                            thisHitbox.Unknown8, thisHitbox.NumberOfHits, thisHitbox.HitType,
                                            thisHitbox.JuggleLimit, thisHitbox.JuggleIncrease, thisHitbox.Flag4, thisHitbox.HitboxEffectIndex,
                                            thisHitbox.Unknown10, thisHitbox.Unknown11, thisHitbox.Unknown12);

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

                                        switch (BACVER)
                                            {
                                                case 1:
                                                    {
                                                        thisHurtbox.Unknown13 = inFile.ReadInt32();
                                                        break;
                                                    }
                                            }

                                        Debug.WriteLine(
                                            "thisHurtbox - Tickstart: {0}, TickEnd: {1}, X: {2}, Y: {3}, Rot: {4}, Width: {5}, Height: {6}, U1: {7}, U2: {8}, U3: {9}, U4: {10}, U5: {11}, U6: {12}, U7: {13}, U8: {14}, U9: {15}, Flag1: {16}, Flag2: {17}, Flag3: {18}, Flag4: {19}, HitEffect: {20}, U10: {21}, U11: {22}, U12: {23}, U13 {24}",
                                            thisHurtbox.TickStart, thisHurtbox.TickEnd, thisHurtbox.X, thisHurtbox.Y,
                                            thisHurtbox.Z, thisHurtbox.Width, thisHurtbox.Height,
                                            thisHurtbox.Unknown1, thisHurtbox.Unknown2, thisHurtbox.Unknown3,
                                            thisHurtbox.Unknown4, thisHurtbox.Unknown5, thisHurtbox.Unknown6,
                                            thisHurtbox.Unknown7,
                                            thisHurtbox.Unknown8, thisHurtbox.Unknown9, thisHurtbox.Flag1,
                                            thisHurtbox.Flag2, thisHurtbox.Flag3, thisHurtbox.Flag4,
                                            thisHurtbox.HitEffect, thisHurtbox.Unknown10, thisHurtbox.Unknown11,
                                            thisHurtbox.Unknown12, thisHurtbox.Unknown13);

                                        thisMove.Hurtboxes[l] = thisHurtbox;

                                        break;
                                    }

                                    case 7:
                                    {
                                        if (thisMove.PhysicsBoxes == null)
                                        {
                                            thisMove.PhysicsBoxes = new PhysicsBox[count];
                                        }

                                        PhysicsBox thisType7 = new PhysicsBox();
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

                                        thisMove.PhysicsBoxes[l] = thisType7;

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
                                        thisType8.Type = (AnimationEnum) inFile.ReadInt16();
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

                                        Debug.WriteLine(
                                            "thisPosition - TickStart: {0}, TickEnd: {1}, Unknown1: {2}, Flag: {3}, filePos: {4}",
                                            thisPosition.TickStart, thisPosition.TickEnd, thisPosition.Movement,
                                            thisPosition.Flag.ToString("X"), thisFilePos.ToString("X"));

                                        thisMove.Positions[l] = thisPosition;

                                        break;
                                    }
                                }

                                
                            
                            }

                            Debug.WriteLine("Current position: " + inFile.BaseStream.Position.ToString("X"));
                            switch (BACVER)
                            {
                                case 1:
                                    {
                                        if (inFile.BaseStream.Position > BACVERintAddresses[0])
                                        {
                                            Debug.WriteLine("We're At the WRONG POSITION, TOO FAR!");
                                            throw new Exception("WE WENT TOO FAR! (Position > BACVERintAddresses[0])");
                                        }

                                        if (inFile.BaseStream.Position < BACVERintAddresses[0])
                                        {
                                            Debug.WriteLine("We're at the WRONG POSITION, TOO SHORT!");
                                            //Trying to salvage it. Something is not quite right with the way we're reading type0's I think... This works for now.
                                            inFile.BaseStream.Seek(BACVERintAddresses[0], SeekOrigin.Begin);
                                        }
                                        
                                        for (int l = 0; l < count; l++)
                                        {
                                            switch (type)
                                            {
                                                case 0:
                                                    {
                                                            thisMove.AutoCancels[l].BACVERint1 = inFile.ReadInt32(); 
                                                            thisMove.AutoCancels[l].BACVERint2 = inFile.ReadInt32(); 
                                                            thisMove.AutoCancels[l].BACVERint3 = inFile.ReadInt32(); 
                                                            thisMove.AutoCancels[l].BACVERint4 = inFile.ReadInt32(); 
                                                        
                                                        break;
                                                    }

                                                case 1:
                                                    {
                                                            thisMove.Type1s[l].BACVERint1 = inFile.ReadInt32(); 
                                                            thisMove.Type1s[l].BACVERint2 = inFile.ReadInt32(); 
                                                            thisMove.Type1s[l].BACVERint3 = inFile.ReadInt32(); 
                                                            thisMove.Type1s[l].BACVERint4 = inFile.ReadInt32(); 
                                                        break;
                                                    }

                                                case 2:
                                                    {
                                                            thisMove.Forces[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Forces[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Forces[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Forces[l].BACVERint4 = inFile.ReadInt32();
                                                        break;
                                                    }

                                                case 3:
                                                    {
                                                            thisMove.Cancels[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Cancels[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Cancels[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Cancels[l].BACVERint4 = inFile.ReadInt32();
                                                        break;
                                                    }

                                                case 4:
                                                    {
                                                        thisMove.Others[l].BACVERint1 = inFile.ReadInt32();
                                                        thisMove.Others[l].BACVERint2 = inFile.ReadInt32();
                                                        thisMove.Others[l].BACVERint3 = inFile.ReadInt32();
                                                        thisMove.Others[l].BACVERint4 = inFile.ReadInt32();
                                                        break;
                                                    }

                                                case 5:
                                                    {
                                                        thisMove.Hitboxes[l].BACVERint1 = inFile.ReadInt32();
                                                        thisMove.Hitboxes[l].BACVERint2 = inFile.ReadInt32();
                                                        thisMove.Hitboxes[l].BACVERint3 = inFile.ReadInt32();
                                                        thisMove.Hitboxes[l].BACVERint4 = inFile.ReadInt32();
                                                        break;
                                                    }

                                                case 6:
                                                    {
                                                       
                                                            thisMove.Hurtboxes[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Hurtboxes[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Hurtboxes[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Hurtboxes[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 7:
                                                    {
                                                       
                                                            thisMove.PhysicsBoxes[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.PhysicsBoxes[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.PhysicsBoxes[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.PhysicsBoxes[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 8:
                                                    {
                                                       
                                                            thisMove.Animations[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Animations[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Animations[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Animations[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 9:
                                                    {
                                                       
                                                            thisMove.Type9s[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Type9s[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Type9s[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Type9s[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 10:
                                                    {
                                                            thisMove.SoundEffects[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.SoundEffects[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.SoundEffects[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.SoundEffects[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 11:
                                                    {
                                                       
                                                            thisMove.VisualEffects[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.VisualEffects[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.VisualEffects[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.VisualEffects[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }

                                                case 12:
                                                    {
                                                            thisMove.Positions[l].BACVERint1 = inFile.ReadInt32();
                                                            thisMove.Positions[l].BACVERint2 = inFile.ReadInt32();
                                                            thisMove.Positions[l].BACVERint3 = inFile.ReadInt32();
                                                            thisMove.Positions[l].BACVERint4 = inFile.ReadInt32();
                                                        
                                                        break;
                                                    }
                                            }
                                        }
                                        break;
                                    }
                                    
                            }
                            

                        }

                        foreach (var typeInfo in TypeInfoList)
                        {
                            Debug.WriteLine("Type:");
                            Debug.WriteLine("TickOffset: {0} DataOffset: {1} TypeNumber: {2} NumberOfType: {3}, TickAddress: {4}, DataAddress {5}", typeInfo.TickOffset.ToString("X"), typeInfo.DataOffset.ToString("X"), typeInfo.TypeNumber, typeInfo.NumberOfType, typeInfo.TickAddress.ToString("X"), typeInfo.DataAddress.ToString("X"));
                            if (typeInfo.DataOffset > size && size > 0)
                            {
                                Debug.WriteLine("DataOffset BIGGER THAN Size????");
                            }
                        }

                        MoveList.Add(thisMove);
                        file.MoveLists[i].Moves[j] = thisMove;
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
                            DamageType = inFile.ReadInt32(),
                            Unused1 = inFile.ReadByte(),
                            NumberOfType1 = inFile.ReadByte(),
                            NumberOfType2 = inFile.ReadByte(),
                            Unused2 = inFile.ReadByte(),
                            Damage = inFile.ReadInt16(),
                            Stun = inFile.ReadInt16(),
                            Index9 = inFile.ReadInt32(),
                            EXBuildAttacker = inFile.ReadInt16(),
                            EXBuildDefender = inFile.ReadInt16(),
                            Index12 = inFile.ReadInt32(), 
                            HitStunFramesAttacker = inFile.ReadInt32(),
                            HitStunFramesDefender = inFile.ReadInt16(),
                            FuzzyEffect = inFile.ReadInt16(),
                            RecoveryAnimationFramesDefender = inFile.ReadInt16(),
                            Index17 = inFile.ReadInt16(),
                            Index18 = inFile.ReadInt16(),
                            Index19 = inFile.ReadInt16(),
                            KnockBack = inFile.ReadSingle(),
                            FallSpeed = inFile.ReadSingle(),
                            Index22 = inFile.ReadInt32(),
                            Index23 = inFile.ReadInt32(),
                            Index24 = inFile.ReadInt32(),
                            Index25 = inFile.ReadInt32(),
                            OffsetToStartOfType1 = inFile.ReadInt32(),
                            OffsetToStartOfType2 = inFile.ReadInt32()
                        };
   
                        hitboxEffect.Type1s = new HitboxEffectSoundEffect[hitboxEffect.NumberOfType1];
                        hitboxEffect.Type2s = new HitboxEffectVisualEffect[hitboxEffect.NumberOfType2];

                        int startOfType1 = hitboxEffect.OffsetToStartOfType1 + thisTypeAddress;
                        int startOfType2 = hitboxEffect.OffsetToStartOfType2 + thisTypeAddress;

                        if (hitboxEffect.NumberOfType1 > 0)
                        {
                            inFile.BaseStream.Seek(startOfType1, SeekOrigin.Begin);

                            for (int m = 0; m < hitboxEffect.NumberOfType1; m++)
                            {
                                HitboxEffectSoundEffect thisType1 = new HitboxEffectSoundEffect()
                                {
                                    Unknown1 = inFile.ReadInt16(),
                                    SoundType = inFile.ReadInt16(),
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
                                HitboxEffectVisualEffect thisForce = new HitboxEffectVisualEffect()
                                {
                                    EffectType1 = inFile.ReadInt32(),
                                    EffectType2 = inFile.ReadInt16(),
                                    EffectType3 = inFile.ReadInt16(),
                                    Unknown4 = inFile.ReadInt16(),
                                    EffectPosition = inFile.ReadInt16(),
                                    Unknown6 = inFile.ReadInt32(),
                                    Unknown7 = inFile.ReadInt32(),
                                    Unknown8 = inFile.ReadInt32(),
                                    Unknown9 = inFile.ReadInt32(),
                                    Size = inFile.ReadSingle(),
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

        public static void ToUassetFile(BACFile file, string OutPutFileName)
        {
            byte[] outPutFileBytes;

            using (var ms = new MemoryStream())
            using (var outFile = new BinaryWriter(ms))
            {
                byte[] headerBytes =
                {
                    0x23, 0x42, 0x41, 0x43, 0xFE, 0xFF, 0x18, 0x00, 0x00, 0x00
                };
                
                outFile.Write(headerBytes);
                outFile.Write(file.BACVER);

                outFile.Write((short)file.MoveLists.Count());
                outFile.Write((short)file.HitboxEffectses.Count());

                long StartOfStartOfMoveTableOffsets = outFile.BaseStream.Position;
                Debug.WriteLine("StartOfStartOfMoveTableOffsets: " + StartOfStartOfMoveTableOffsets.ToString("X"));
                outFile.Write(0);


                long StartOfStartOfHitboxEffectsOffsets = outFile.BaseStream.Position;
                Debug.WriteLine("StartOfStartOfHitboxEffectsOffsets: " + StartOfStartOfHitboxEffectsOffsets.ToString("X"));
                outFile.Write(0);


                long StartOfMoveTableOffsets = outFile.BaseStream.Position;
                Common.WriteInt32ToPosition(outFile, StartOfStartOfMoveTableOffsets, (int)StartOfMoveTableOffsets);
                Debug.WriteLine("StartOfMoveTableOffsets: " + StartOfMoveTableOffsets.ToString("X"));

                for (int i = 0; i < file.MoveLists.Count(); i++)
                {
                    outFile.Write(0);
                }


                long StartOfHitboxEffectsOffsets = outFile.BaseStream.Position;
                Common.WriteInt32ToPosition(outFile, StartOfStartOfHitboxEffectsOffsets, (int)StartOfHitboxEffectsOffsets);
                Debug.WriteLine("StartOfHitboxEffectsOffsets: " + StartOfHitboxEffectsOffsets.ToString("X"));

                for (int i = 0; i < file.HitboxEffectses.Count(); i++)
                {
                    outFile.Write(0);
                }

                Debug.WriteLine("MoveTableCount: " + file.MoveLists.Length);

                List<long> MoveTableBaseAddresses = new List<long>();
                List<long> MoveTableBaseNameOffsets = new List<long>();


                for (int i = 0; i < file.MoveLists.Length; i++)
                {
                    Debug.WriteLine("Writing MoveTable: " + i);
                    Common.WriteInt32ToPosition(outFile, StartOfMoveTableOffsets + (i*4), (int)outFile.BaseStream.Position);
                    long MoveTableBaseAddress = outFile.BaseStream.Position;

                    MoveTableBaseAddresses.Add(MoveTableBaseAddress);

                    outFile.Write(file.MoveLists[i].Unknown1);
                    outFile.Write((short)file.MoveLists[i].Moves.Length);

                    long StartOfStartOfMovesOffset = outFile.BaseStream.Position;
                    outFile.Write(0); //StartOfMovesOffset
                    long StartOfStartOfMovesNamesOffset = outFile.BaseStream.Position;
                    outFile.Write(0); //StartOfNameAddressesOffset

                    long StartOfMovesOffset = outFile.BaseStream.Position;
                    Common.WriteInt32ToPosition(outFile, StartOfStartOfMovesOffset, (int)(StartOfMovesOffset - MoveTableBaseAddress));

                    Debug.WriteLine("MoveCount: " + file.MoveLists[i].Moves.Length);

                    foreach (var move in file.MoveLists[i].Moves)
                    {
                        outFile.Write(0);
                    }

                    long StartOfMovesNamesOffset = outFile.BaseStream.Position;
                    MoveTableBaseNameOffsets.Add(StartOfMovesNamesOffset);
                    Common.WriteInt32ToPosition(outFile, StartOfStartOfMovesNamesOffset, (int)(StartOfMovesNamesOffset - MoveTableBaseAddress));

                    Debug.WriteLine("Names: " + StartOfMovesNamesOffset.ToString("X"));

                    foreach (var move in file.MoveLists[i].Moves)
                    {
                        outFile.Write(0);
                    }
                    Debug.WriteLine("Doing Moves... Current Position: " + outFile.BaseStream.Position.ToString("X"));

                    int j = 0;

                    foreach (var move in file.MoveLists[i].Moves)
                    {
                        if (move == null)
                        {
                            Common.WriteInt32ToPosition(outFile, StartOfMovesOffset +(j*4), 0);
                            j++;
                            continue;
                        }

                        Common.WriteInt32ToPosition(outFile, StartOfMovesOffset +(j*4), (int)(outFile.BaseStream.Position - MoveTableBaseAddress));

                        outFile.Write(move.FirstHitboxFrame);
                        outFile.Write(move.LastHitboxFrame);
                        outFile.Write(move.InterruptFrame);
                        outFile.Write(move.TotalTicks);
                        outFile.Write(move.ReturnToOriginalPosition);
                        outFile.Write(move.Slide);
                        outFile.Write(move.unk3);
                        outFile.Write(move.unk4);
                        outFile.Write(move.unk5);
                        outFile.Write(move.unk6);
                        outFile.Write(move.unk7);
                        outFile.Write(move.Flag);
                        outFile.Write(move.unk9);

                        outFile.Write(move.numberOfTypes);

                        outFile.Write(move.unk13);
                        outFile.Write(move.HeaderSize);

                        if (move.HeaderSize == 0x58)
                        {
                            outFile.Write(move.Unknown12);
                            outFile.Write(move.Unknown13);
                            outFile.Write(move.Unknown14);
                            outFile.Write(move.Unknown15);
                            outFile.Write(move.Unknown16);
                            outFile.Write(move.Unknown17);
                            outFile.Write(move.Unknown18);
                            outFile.Write(move.Unknown19);
                            outFile.Write(move.Unknown20);
                            outFile.Write(move.Unknown21);
                            outFile.Write(move.Unknown22);
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

                        if (move.AutoCancels != null && move.AutoCancels.Length > 0)
                        {
                            outFile.Write((short) 0);
                            outFile.Write((short)move.AutoCancels.Length);
                            type0TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Type1s != null && move.Type1s.Length > 0)
                        {
                            outFile.Write((short)1);
                            outFile.Write((short)move.Type1s.Length);
                            type1TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Forces != null && move.Forces.Length > 0)
                        {
                            outFile.Write((short)2);
                            outFile.Write((short)move.Forces.Length);
                            ForceTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Cancels != null && move.Cancels.Length > 0)
                        {
                            outFile.Write((short)3);
                            outFile.Write((short)move.Cancels.Length);
                            type3TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Others != null && move.Others.Length > 0)
                        {
                            outFile.Write((short)4);
                            outFile.Write((short)move.Others.Length);
                            type4TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Hitboxes != null && move.Hitboxes.Length > 0)
                        {
                            outFile.Write((short)5);
                            outFile.Write((short)move.Hitboxes.Length);
                            HitboxTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Hurtboxes != null && move.Hurtboxes.Length > 0)
                        {
                            outFile.Write((short)6);
                            outFile.Write((short)move.Hurtboxes.Length);
                            HurtboxTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.PhysicsBoxes != null && move.PhysicsBoxes.Length > 0)
                        {
                            outFile.Write((short)7);
                            outFile.Write((short)move.PhysicsBoxes.Length);
                            type7TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Animations != null && move.Animations.Length > 0)
                        {
                            outFile.Write((short)8);
                            outFile.Write((short)move.Animations.Length);
                            type8TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Type9s != null && move.Type9s.Length > 0)
                        {
                            outFile.Write((short)9);
                            outFile.Write((short)move.Type9s.Length);
                            type9TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.SoundEffects != null && move.SoundEffects.Length > 0)
                        {
                            outFile.Write((short)10);
                            outFile.Write((short)move.SoundEffects.Length);
                            type10TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.VisualEffects != null && move.VisualEffects.Length > 0)
                        {
                            outFile.Write((short)11);
                            outFile.Write((short)move.VisualEffects.Length);
                            type11TickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }
                        if (move.Positions != null && move.Positions.Length > 0)
                        {
                            outFile.Write((short)12);
                            outFile.Write((short)move.Positions.Length);
                            PositionTickOffsetAddress = outFile.BaseStream.Position;
                            outFile.Write(0);
                            outFile.Write(0);
                            AddBytesDependingOnBACVER(file.BACVER, outFile);
                        }

                        if (move.AutoCancels != null && move.AutoCancels.Length > 0)
                        {
                            List<long> type0Offsets = new List<long>();
                            List<long> IntOffsets = new List<long>();
                            List<long> IntPositions = new List<long>();

                            Common.WriteInt32ToPosition(outFile, type0TickOffsetAddress, (int)(outFile.BaseStream.Position - (type0TickOffsetAddress - 4)));

                            foreach (var type0 in move.AutoCancels)
                            {
                                outFile.Write(type0.TickStart);
                                outFile.Write(type0.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type0TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type0TickOffsetAddress - 4)));

                            foreach (var type0 in move.AutoCancels)
                            {
                                if (type0.Ints.Length != 0)
                                {
                                    type0Offsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write((short)type0.Condition);
                                outFile.Write(type0.MoveIndex);
                                outFile.Write(type0.ScriptStartTime);
                                outFile.Write(type0.NumberOfInts);
                                outFile.Write(type0.Unknown2);

                                switch (file.BACVER)
                                {
                                    case 1:
                                        {
                                            outFile.Write(type0.Unknown3);
                                            outFile.Write(type0.Unknown4);
                                            break;
                                        }
                                }

                                if (type0.Ints.Length != 0)
                                {
                                    IntOffsets.Add(outFile.BaseStream.Position);
                                }
                                outFile.Write(0);
                            }

                            foreach (var type0 in move.AutoCancels)
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
                                Common.WriteInt32ToPosition(outFile, IntOffsets[k], (int)(IntPositions[k] - type0Offsets[k]));
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type0TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type0TickOffsetAddress - 4)));

                                        foreach (var type0 in move.AutoCancels)
                                        {
                                            outFile.Write(type0.BACVERint1);
                                            outFile.Write(type0.BACVERint2);
                                            outFile.Write(type0.BACVERint3);
                                            outFile.Write(type0.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Type1s != null && move.Type1s.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type1TickOffsetAddress, (int)(outFile.BaseStream.Position - (type1TickOffsetAddress - 4)));

                            foreach (var type1 in move.Type1s)
                            {
                                outFile.Write(type1.TickStart);
                                outFile.Write(type1.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type1TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type1TickOffsetAddress - 4)));

                            foreach (var type1 in move.Type1s)
                            {
                                outFile.Write(type1.Flag1);
                                outFile.Write(type1.Flag2);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type1TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type1TickOffsetAddress - 4)));

                                        foreach (var type1 in move.Type1s)
                                        {
                                            outFile.Write(type1.BACVERint1);
                                            outFile.Write(type1.BACVERint2);
                                            outFile.Write(type1.BACVERint3);
                                            outFile.Write(type1.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Forces != null && move.Forces.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, ForceTickOffsetAddress, (int)(outFile.BaseStream.Position - (ForceTickOffsetAddress - 4)));

                            foreach (var Force in move.Forces)
                            {
                                outFile.Write(Force.TickStart);
                                outFile.Write(Force.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, ForceTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (ForceTickOffsetAddress - 4)));

                            foreach (var Force in move.Forces)
                            {
                                outFile.Write(Force.Amount);
                                outFile.Write((int)Force.Flag);
                            }

                                switch (file.BACVER)
                                {
                                    case 1:
                                        {

                                            //TheBACunknownoffsetthingie
                                            Common.WriteInt32ToPosition(outFile, ForceTickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (ForceTickOffsetAddress - 4)));

                                            foreach (var Force in move.Forces)
                                            {
                                                outFile.Write(Force.BACVERint1);
                                                outFile.Write(Force.BACVERint2);
                                                outFile.Write(Force.BACVERint3);
                                                outFile.Write(Force.BACVERint4);
                                            }

                                            break;
                                        }
                                }
                        }

                        if (move.Cancels != null && move.Cancels.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type3TickOffsetAddress, (int)(outFile.BaseStream.Position - (type3TickOffsetAddress - 4)));

                            foreach (var type3 in move.Cancels)
                            {
                                outFile.Write(type3.TickStart);
                                outFile.Write(type3.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type3TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type3TickOffsetAddress - 4)));

                            foreach (var type3 in move.Cancels)
                            {
                                outFile.Write(type3.CancelList);
                                outFile.Write(type3.Type);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type3TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type3TickOffsetAddress - 4)));

                                        foreach (var type3 in move.Cancels)
                                        {
                                            outFile.Write(type3.BACVERint1);
                                            outFile.Write(type3.BACVERint2);
                                            outFile.Write(type3.BACVERint3);
                                            outFile.Write(type3.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Others != null && move.Others.Length > 0)
                        {
                            List<long> type4Offsets = new List<long>();
                            List<long> IntOffsets = new List<long>();
                            List<long> IntPositions = new List<long>();

                            Common.WriteInt32ToPosition(outFile, type4TickOffsetAddress, (int)(outFile.BaseStream.Position - (type4TickOffsetAddress - 4)));

                            foreach (var type4 in move.Others)
                            {
                                outFile.Write(type4.TickStart);
                                outFile.Write(type4.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type4TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type4TickOffsetAddress - 4)));

                            foreach (var type4 in move.Others)
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

                            foreach (var type4 in move.Others)
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
                                Common.WriteInt32ToPosition(outFile, IntOffsets[k], (int)(IntPositions[k] - type4Offsets[k]));
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type4TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type4TickOffsetAddress - 4)));

                                        foreach (var type4 in move.Others)
                                        {
                                            outFile.Write(type4.BACVERint1);
                                            outFile.Write(type4.BACVERint2);
                                            outFile.Write(type4.BACVERint3);
                                            outFile.Write(type4.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Hitboxes != null && move.Hitboxes.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, HitboxTickOffsetAddress, (int)(outFile.BaseStream.Position - (HitboxTickOffsetAddress - 4)));

                            foreach (var Hitbox in move.Hitboxes)
                            {
                                outFile.Write(Hitbox.TickStart);
                                outFile.Write(Hitbox.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, HitboxTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (HitboxTickOffsetAddress - 4)));

                            foreach (var Hitbox in move.Hitboxes)
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
                                outFile.Write(Hitbox.NumberOfHits);

                                outFile.Write(Hitbox.HitType);
                                outFile.Write(Hitbox.JuggleLimit);
                                outFile.Write(Hitbox.JuggleIncrease);
                                outFile.Write(Hitbox.Flag4);

                                outFile.Write(Hitbox.HitboxEffectIndex);
                                outFile.Write(Hitbox.Unknown10);
                                outFile.Write(Hitbox.Unknown11);

                                switch (file.BACVER)
                                {
                                    case 1:
                                        {
                                            outFile.Write(Hitbox.Unknown12);
                                            break;
                                        }
                                }
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, HitboxTickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (HitboxTickOffsetAddress - 4)));

                                        foreach (var Hitbox in move.Hitboxes)
                                        {
                                            outFile.Write(Hitbox.BACVERint1);
                                            outFile.Write(Hitbox.BACVERint2);
                                            outFile.Write(Hitbox.BACVERint3);
                                            outFile.Write(Hitbox.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Hurtboxes != null && move.Hurtboxes.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, HurtboxTickOffsetAddress, (int)(outFile.BaseStream.Position - (HurtboxTickOffsetAddress - 4)));

                            foreach (var Hurtbox in move.Hurtboxes)
                            {
                                outFile.Write(Hurtbox.TickStart);
                                outFile.Write(Hurtbox.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, HurtboxTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (HurtboxTickOffsetAddress - 4)));
                     
                            foreach (var Hurtbox in move.Hurtboxes)
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

                                switch (file.BACVER)
                                {
                                    case 1:
                                        {
                                            outFile.Write(Hurtbox.Unknown13);
                                            break;
                                        }
                                }
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, HurtboxTickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (HurtboxTickOffsetAddress - 4)));


                                        foreach (var Hurtbox in move.Hurtboxes)
                                        {
                                            outFile.Write(Hurtbox.BACVERint1);
                                            outFile.Write(Hurtbox.BACVERint2);
                                            outFile.Write(Hurtbox.BACVERint3);
                                            outFile.Write(Hurtbox.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.PhysicsBoxes != null && move.PhysicsBoxes.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type7TickOffsetAddress, (int)(outFile.BaseStream.Position - (type7TickOffsetAddress - 4)));

                            foreach (var type7 in move.PhysicsBoxes)
                            {
                                outFile.Write(type7.TickStart);
                                outFile.Write(type7.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type7TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type7TickOffsetAddress - 4)));
           
                            foreach (var type7 in move.PhysicsBoxes)
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

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type7TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type7TickOffsetAddress - 4)));

                                        foreach (var type7 in move.PhysicsBoxes)
                                        {
                                            outFile.Write(type7.BACVERint1);
                                            outFile.Write(type7.BACVERint2);
                                            outFile.Write(type7.BACVERint3);
                                            outFile.Write(type7.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Animations != null && move.Animations.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type8TickOffsetAddress, (int)(outFile.BaseStream.Position - (type8TickOffsetAddress - 4)));

                            foreach (var type8 in move.Animations)
                            {
                                outFile.Write(type8.TickStart);
                                outFile.Write(type8.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type8TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type8TickOffsetAddress - 4)));

                            foreach (var type8 in move.Animations)
                            {
                                outFile.Write(type8.Index);
                                outFile.Write((short)type8.Type);
                                outFile.Write(type8.FrameStart);
                                outFile.Write(type8.FrameEnd);
                                outFile.Write(type8.Unknown1);
                                outFile.Write(type8.Unknown2);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type8TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type8TickOffsetAddress - 4)));


                                        foreach (var type8 in move.Animations)
                                        {
                                            outFile.Write(type8.BACVERint1);
                                            outFile.Write(type8.BACVERint2);
                                            outFile.Write(type8.BACVERint3);
                                            outFile.Write(type8.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Type9s != null && move.Type9s.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type9TickOffsetAddress, (int)(outFile.BaseStream.Position - (type9TickOffsetAddress - 4)));

                            foreach (var type9 in move.Type9s)
                            {
                                outFile.Write(type9.TickStart);
                                outFile.Write(type9.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type9TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type9TickOffsetAddress - 4)));

                            foreach (var type9 in move.Type9s)
                            {
                                outFile.Write(type9.Unknown1);
                                outFile.Write(type9.Unknown2);
                                outFile.Write(type9.Unknown3);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type9TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type9TickOffsetAddress - 4)));

                                        foreach (var type9 in move.Type9s)
                                        {
                                            outFile.Write(type9.BACVERint1);
                                            outFile.Write(type9.BACVERint2);
                                            outFile.Write(type9.BACVERint3);
                                            outFile.Write(type9.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.SoundEffects != null && move.SoundEffects.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type10TickOffsetAddress, (int)(outFile.BaseStream.Position - (type10TickOffsetAddress - 4)));

                            foreach (var type10 in move.SoundEffects)
                            {
                                outFile.Write(type10.TickStart);
                                outFile.Write(type10.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type10TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type10TickOffsetAddress - 4)));
   
                            foreach (var type10 in move.SoundEffects)
                            {
                                outFile.Write(type10.Unknown1);
                                outFile.Write(type10.Unknown2);
                                outFile.Write(type10.Unknown3);

                                outFile.Write(type10.Unknown4);
                                outFile.Write(type10.Unknown5);
                                outFile.Write(type10.Unknown6);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type10TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type10TickOffsetAddress - 4)));

                                        foreach (var type10 in move.SoundEffects)
                                        {
                                            outFile.Write(type10.BACVERint1);
                                            outFile.Write(type10.BACVERint2);
                                            outFile.Write(type10.BACVERint3);
                                            outFile.Write(type10.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.VisualEffects != null && move.VisualEffects.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, type11TickOffsetAddress, (int)(outFile.BaseStream.Position - (type11TickOffsetAddress - 4)));

                            foreach (var type11 in move.VisualEffects)
                            {
                                outFile.Write(type11.TickStart);
                                outFile.Write(type11.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, type11TickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (type11TickOffsetAddress - 4)));

                            foreach (var type11 in move.VisualEffects)
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

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, type11TickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (type11TickOffsetAddress - 4)));

                                        foreach (var type11 in move.VisualEffects)
                                        {
                                            outFile.Write(type11.BACVERint1);
                                            outFile.Write(type11.BACVERint2);
                                            outFile.Write(type11.BACVERint3);
                                            outFile.Write(type11.BACVERint4);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (move.Positions != null && move.Positions.Length > 0)
                        {
                            Common.WriteInt32ToPosition(outFile, PositionTickOffsetAddress, (int)(outFile.BaseStream.Position - (PositionTickOffsetAddress - 4)));

                            foreach (var Position in move.Positions)
                            {
                                outFile.Write(Position.TickStart);
                                outFile.Write(Position.TickEnd);
                            }

                            Common.WriteInt32ToPosition(outFile, PositionTickOffsetAddress + 4, (int)(outFile.BaseStream.Position - (PositionTickOffsetAddress - 4)));

                            foreach (var Position in move.Positions)
                            {
                                outFile.Write(Position.Movement);
                                outFile.Write(Position.Flag);
                            }

                            switch (file.BACVER)
                            {
                                case 1:
                                    {
                                        //TheBACunknownoffsetthingie
                                        Common.WriteInt32ToPosition(outFile, PositionTickOffsetAddress + 8, (int)(outFile.BaseStream.Position - (PositionTickOffsetAddress - 4)));

                                        foreach (var Position in move.Positions)
                                        {
                                            outFile.Write(Position.BACVERint1);
                                            outFile.Write(Position.BACVERint2);
                                            outFile.Write(Position.BACVERint3);
                                            outFile.Write(Position.BACVERint4);
                                        }

                                        break;
                                    }
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
                        Common.WriteInt32ToPosition(outFile, StartOfHitboxEffectsOffsets + (i * 4), 0);
                        continue;
                    }

                    Common.WriteInt32ToPosition(outFile, StartOfHitboxEffectsOffsets + (i*4), (int)outFile.BaseStream.Position);

                    long HitboxEffectsBasePosition = outFile.BaseStream.Position;

                    for (int j = 0; j < 20; j++)
                    {
                        outFile.Write(0);
                    }

                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_STAND);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition +4, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_CROUCH);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 8, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_AIR);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 12, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_UNKNOWN);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 16, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].HIT_UNKNOWN2);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 20, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_STAND);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 24, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_CROUCH);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 28, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_AIR);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 32, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_UNKNOWN);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 36, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].GUARD_UNKNOWN2);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 40, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_STAND);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 44, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_CROUCH);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 48, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_AIR);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 52, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_UNKNOWN);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 56, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].COUNTERHIT_UNKNOWN2);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 60, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_STAND);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 64, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_CROUCH);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 68, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_AIR);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 72, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_UNKNOWN);
                    Common.WriteInt32ToPosition(outFile, HitboxEffectsBasePosition + 76, (int)(outFile.BaseStream.Position - HitboxEffectsBasePosition));
                    WriteHitboxEffect(outFile, file.HitboxEffectses[i].UNKNOWN_UNKNOWN2);
                }

                Debug.WriteLine("Done with HitboxEffects! Now doing Names... CurrentPos: " + outFile.BaseStream.Position.ToString("X"));

                for (int i = 0; i < file.MoveLists.Length; i++)
               {
                   List<long> NamePositions = new List<long>();

                   for (int j = 0; j < file.MoveLists[i].Moves.Length; j++)
                    {
                        if (file.MoveLists[i].Moves[j] == null)
                        {
                            continue;
                        }

                       NamePositions.Add(outFile.BaseStream.Position);
                       outFile.Write(file.MoveLists[i].Moves[j].Name.ToCharArray());
           
                        outFile.Write((byte)0x00);
                    }

                   for (int k = 0; k < file.MoveLists[i].Moves.Length; k++)
                   {
                       if (file.MoveLists[i].Moves[k] == null)
                       {
                           Common.WriteInt32ToPosition(outFile, MoveTableBaseNameOffsets[i] + (k * 4), 0);
                           continue;
                       }
                       Common.WriteInt32ToPosition(outFile, MoveTableBaseNameOffsets[i] + (k * 4), (int)(NamePositions[0] - MoveTableBaseAddresses[i]));
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

            outPut.InsertRange(0, BitConverter.GetBytes(outPutFileBytes.Length+4));

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

        private static void WriteHitboxEffect(BinaryWriter outFile, HitboxEffect effect)
        {
            long StartOfEffectAddress = outFile.BaseStream.Position;

            outFile.Write(effect.Type);
            outFile.Write(effect.Index);
            outFile.Write(effect.DamageType);
            outFile.Write(effect.Unused1);
            outFile.Write(effect.NumberOfType1);
            outFile.Write(effect.NumberOfType2);
            outFile.Write(effect.Unused2);
            outFile.Write(effect.Damage);
            outFile.Write(effect.Stun);
            outFile.Write(effect.Index9);
            outFile.Write(effect.EXBuildAttacker);
            outFile.Write(effect.EXBuildDefender);
            outFile.Write(effect.Index12);
            outFile.Write(effect.HitStunFramesAttacker);
            outFile.Write(effect.HitStunFramesDefender);
            outFile.Write(effect.FuzzyEffect);
            outFile.Write(effect.RecoveryAnimationFramesDefender);
            outFile.Write(effect.Index17);
            outFile.Write(effect.Index18);
            outFile.Write(effect.Index19);
            outFile.Write(effect.KnockBack);
            outFile.Write(effect.FallSpeed);
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
                    Common.WriteInt32ToPosition(outFile, OffsetToType1Address,
                        (int) (outFile.BaseStream.Position - StartOfEffectAddress));
                }
                outFile.Write(effect.Type1s[i].Unknown1);
                outFile.Write(effect.Type1s[i].SoundType);
                outFile.Write(effect.Type1s[i].Unknown3);
                outFile.Write(effect.Type1s[i].Unknown4);
            }

            for (int i = 0; i < effect.Type2s.Length; i++)
            {
                if (i == 0)
                {
                    Common.WriteInt32ToPosition(outFile, OffsetToType1Address + 4,
                        (int) (outFile.BaseStream.Position - StartOfEffectAddress));
                }
                outFile.Write(effect.Type2s[i].EffectType1);
                outFile.Write(effect.Type2s[i].EffectType2);
                outFile.Write(effect.Type2s[i].EffectType3);
                outFile.Write(effect.Type2s[i].Unknown4);
                outFile.Write(effect.Type2s[i].EffectPosition);
                outFile.Write(effect.Type2s[i].Unknown6);
                outFile.Write(effect.Type2s[i].Unknown7);
                outFile.Write(effect.Type2s[i].Unknown8);
                outFile.Write(effect.Type2s[i].Unknown9);
                outFile.Write(effect.Type2s[i].Size);
                outFile.Write(effect.Type2s[i].Unknown11);
            }
        }

        private static void AddBytesDependingOnBACVER(int BACVER, BinaryWriter outFile)
        {
            switch (BACVER)
            {
                case 1:
                    {
                        outFile.Write(0);
                        break;
                    }
            }
        }

        private static void WriteUnknownBytesDependingOnBACVER(dynamic type, int BACVER, BinaryWriter outFile)
        {
            switch (BACVER)
            {
                case 1:
                    {
                        outFile.Write(type.BACVERint1);
                        outFile.Write(type.BACVERint2);
                        outFile.Write(type.BACVERint3);
                        outFile.Write(type.BACVERint4);

                        break;
                    }
            }
        }
    }

    public class ForceEnumConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Debug.WriteLine($"\"{reader.Value}\" of type \"{reader.TokenType}\" found. Attempting conversion...");

            try
            {
                switch (reader.TokenType)
                {
                    case JsonToken.String:
                        var enumText = reader.Value.ToString();
                        var convertedStr = (int)Enum.Parse(typeof(ForceEnum), enumText);

                        Debug.WriteLine($"\tConverted \"{reader.Value}\" to {convertedStr}");

                        return convertedStr;

                    case JsonToken.Integer:
                        var convertedInt = Convert.ChangeType(reader.Value, objectType);

                        Debug.WriteLine($"\tConverted \"{reader.Value}\" to {convertedInt}");

                        return convertedInt;
                }
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException($"Error converting value {reader.Value} to type '{objectType}'.", ex);
            }

            throw new JsonReaderException($"{nameof(Force.Flag)} was not of type {JTokenType.String} nor {JTokenType.Integer}!");
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ForceEnum);
        }
    }

    public class BACFile
    {
        public MoveList[] MoveLists { get; set; }
        public HitboxEffects[] HitboxEffectses { get; set; }
        public byte[] RawUassetHeaderDontTouch { get; set; }
        public short BACVER { get; set; }
    }

    public class MoveList
    {
        public Move[] Moves { get; set; }
        public short Unknown1 { get; set; }
    }

    public class Move
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public int FirstHitboxFrame { get; set; }
        public int LastHitboxFrame { get; set; }
        public int InterruptFrame { get; set; }
        public int TotalTicks { get; set; }

        public int ReturnToOriginalPosition { get; set; }
        public float Slide { get; set; }
        public float unk3 { get; set; }
        public float unk4 { get; set; }
        public float unk5 { get; set; }
        public float unk6 { get; set; }
        public float unk7 { get; set; }
        public int Flag { get; set; }
        public int unk9 { get; set; }

        public int numberOfTypes { get; set; }

        public int unk13 { get; set; }
        public int HeaderSize { get; set; }


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


        public AutoCancel[] AutoCancels { get; set; }
        public Type1[] Type1s { get; set; }
        public Force[] Forces { get; set; }
        public Cancel[] Cancels { get; set; }
        public Other[] Others { get; set; } //projectiles
        public Hitbox[] Hitboxes { get; set; }
        public Hurtbox[] Hurtboxes { get; set; }
        public PhysicsBox[] PhysicsBoxes { get; set; }
        public Animation[] Animations { get; set; }
        public Type9[] Type9s { get; set; }
        public SoundEffect[] SoundEffects{ get; set; }
        public VisualEffect[] VisualEffects { get; set; }
        public Position[] Positions { get; set; }
    }

    public class HitboxEffects
    {
        public int Index { get; set; }
        public HitboxEffect HIT_STAND { get; set; }
        public HitboxEffect HIT_CROUCH { get; set; }
        public HitboxEffect HIT_AIR { get; set; }
        public HitboxEffect HIT_UNKNOWN { get; set; }
        public HitboxEffect HIT_UNKNOWN2 { get; set; }
        public HitboxEffect GUARD_STAND { get; set; }
        public HitboxEffect GUARD_CROUCH { get; set; }
        public HitboxEffect GUARD_AIR { get; set; }
        public HitboxEffect GUARD_UNKNOWN { get; set; }
        public HitboxEffect GUARD_UNKNOWN2 { get; set; }
        public HitboxEffect COUNTERHIT_STAND { get; set; }
        public HitboxEffect COUNTERHIT_CROUCH { get; set; }
        public HitboxEffect COUNTERHIT_AIR { get; set; }
        public HitboxEffect COUNTERHIT_UNKNOWN { get; set; }
        public HitboxEffect COUNTERHIT_UNKNOWN2 { get; set; }
        public HitboxEffect UNKNOWN_STAND { get; set; }
        public HitboxEffect UNKNOWN_CROUCH { get; set; }
        public HitboxEffect UNKNOWN_AIR { get; set; }
        public HitboxEffect UNKNOWN_UNKNOWN { get; set; }
        public HitboxEffect UNKNOWN_UNKNOWN2 { get; set; }
    }

    public class HitboxEffect
    {
        public short Type { get; set; }
        public short Index { get; set; }
        public int DamageType { get; set; }
        public byte Unused1 { get; set; }       //
        public byte NumberOfType1 { get; set; } // Flags?
        public byte NumberOfType2 { get; set; } //
        public byte Unused2 { get; set; }       //
        public short Damage { get; set; }
        public short Stun { get; set; }

        public int Index9 { get; set; } //0?
        public short EXBuildAttacker { get; set; }
        public short EXBuildDefender { get; set; }
        public int Index12 { get; set; } //0?
        public int HitStunFramesAttacker { get; set; }

        public short HitStunFramesDefender { get; set; }
        public short FuzzyEffect { get; set; }
        public short RecoveryAnimationFramesDefender { get; set; }
        public short Index17 { get; set; }
        public short Index18 { get; set; }
        public short Index19 { get; set; }
        public float KnockBack { get; set; }

        public float FallSpeed { get; set; }
        public int Index22 { get; set; } //0?
        public int Index23 { get; set; } //0?
        public int Index24 { get; set; } //0?

        public int Index25 { get; set; } //0?
        public int OffsetToStartOfType1 { get; set; }
        public int OffsetToStartOfType2 { get; set; }

        public HitboxEffectSoundEffect[] Type1s { get; set; }
        public HitboxEffectVisualEffect[] Type2s { get; set; }
    }

    public class HitboxEffectSoundEffect
    {
        public short Unknown1 { get; set;  }
        public short SoundType { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }
    }

    public class HitboxEffectVisualEffect
    {
        public int EffectType1 { get; set; }
        public short EffectType2 { get; set; }
        public short EffectType3 { get; set; }
        public short Unknown4 { get; set; }
        public short EffectPosition { get; set; }
        public int Unknown6 { get; set; } //0?
        public int Unknown7 { get; set; } //0?
        public int Unknown8 { get; set; } //0?
        public int Unknown9 { get; set; } //0?
        public float Size { get; set; }
        public int Unknown11 { get; set; } //0?
    }

    public class AutoCancel
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }


        public AutoCancelCondition Condition { get; set; }
        public short MoveIndex { get; set; }
        public string MoveIndexName { get; set; }

        public short ScriptStartTime { get; set; }
        public short NumberOfInts { get; set; }
        public int Unknown2 { get; set; }

        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }

        public int Offset { get; set; }

        public int[] Ints { get; set; }
    }

    public enum AutoCancelCondition
    {
        Always = 0,
        OnBlock = 2
    }

    public class Type1
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public int Flag1 { get; set; }
        public int Flag2 { get; set; }
    }
    
    public class Force
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public float Amount { get; set; }
        [JsonProperty("Flag")]
        [JsonConverter(typeof(ForceEnumConverter))]
        public int Flag { get; set; }
    }

    public enum ForceEnum
    {
        HorizontalSpeed = 0x1,
        VerticalSpeed = 0x10,
        HorizontalAcceleration = 0x1000,
        VerticalAcceleration = 0x10000
    }

    public class Cancel
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public int CancelList { get; set; }
        public int Type { get; set; }
    }

    public class Other //Projectiles and other BACeff stuff.
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public int Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public short NumberOfInts { get; set; }
        public int Offset { get; set; }

        public int[] Ints { get; set; }
    }

    public class Hitbox
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int Unknown1 { get; set; }

        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public short Unknown4 { get; set; }
        public short Unknown5 { get; set; }

        public short Unknown6 { get; set; }
        public short Unknown7 { get; set; }
        public short Unknown8 { get; set; }
        public short NumberOfHits { get; set; }

        public byte HitType { get; set; }
        public byte JuggleLimit { get; set; }
        public byte JuggleIncrease { get; set; }
        public byte Flag4 { get; set; }

        public short HitboxEffectIndex { get; set; }
        public short Unknown10 { get; set; }
        public int Unknown11 { get; set; }
        public int Unknown12 { get; set; }
    }

    public class Hurtbox
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int Unknown1 { get; set; }

        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public short Unknown4 { get; set; }
        public short Unknown5 { get; set; }

        public short Unknown6 { get; set; }
        public short Unknown7 { get; set; }
        public short Unknown8 { get; set; }
        public short Unknown9 { get; set; }

        public byte Flag1 { get; set; }
        public byte Flag2 { get; set; }
        public byte Flag3 { get; set; }
        public byte Flag4 { get; set; }

        public short HitEffect { get; set; }
        public short Unknown10 { get; set; }
        public int Unknown11 { get; set; }

        public float Unknown12 { get; set; }
        public int Unknown13 { get; set; }
    }

    public class PhysicsBox
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int Unknown1 { get; set; }

        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public short Unknown4 { get; set; }
        public short Unknown5 { get; set; }

        public int Unknown6 { get; set; }
    }

    public class Animation
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public short Index { get; set; }
        public AnimationEnum Type { get; set; }
        public short FrameStart { get; set; }
        public short FrameEnd { get; set; }

        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
    }

    public enum AnimationEnum : short
    {
        Regular = 2,
        Face = 4
    }

    public class Type9
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public short Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public float Unknown3 { get; set; }
    }

    public class SoundEffect
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public short Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }

        public short Unknown4 { get; set; }
        public short Unknown5 { get; set; }
        public short Unknown6 { get; set; }
    }

    public class VisualEffect
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public short Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public short Type { get; set; }
        public short Unknown5 { get; set; }
        public short AttachPoint { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Unknown10 { get; set; }
        public float Size { get; set; }
        public float Unknown12 { get; set; }
    }

    public class Position
    {
        public int TickStart { get; set; }
        public int TickEnd { get; set; }
        public int BACVERint1 { get; set; }
        public int BACVERint2 { get; set; }
        public int BACVERint3 { get; set; }
        public int BACVERint4 { get; set; }

        public float Movement { get; set; }
        public int Flag { get; set; }
    }

    public struct TypeInfo
    {
        public int TickOffset;
        public int DataOffset;
        public int TypeNumber;
        public int NumberOfType;

        public long TickAddress;
        public long DataAddress;
    }
}
