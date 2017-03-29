using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MoveLib.BCM
{
    public static class BCM
    {
        public static void BcmToJson(string inFile, string outFile)
        {
            BCMFile bcm;

            try
            {
                bcm = FromUassetFile(inFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create JSON.\n" + ex.Message + " - " + ex.Data);
                throw;
            }

            Formatting format = Formatting.Indented;

            var json = JsonConvert.SerializeObject(bcm, format, new Newtonsoft.Json.Converters.StringEnumConverter());

            File.WriteAllText(outFile, json);
        }

        public static bool JsonToBcm(string inFile, string outFile)
        {
            BCMFile bcm;

            try
            {
                bcm = JsonConvert.DeserializeObject<BCMFile>(File.ReadAllText(inFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing JSON: " + ex.Message + " - " + ex.Data);
                return false;
            }

            try
            {
                ToUassetFile(bcm, outFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong. Couldn't create BCM.\n" + ex.Message + " - " + ex.Data);
                return false;
            }

            return true;
        }

        public static BCMFile FromUassetFile(string fileName)
        {
            byte[] fileBytes = File.ReadAllBytes(fileName);

            byte[] UassetHeaderBytes = Common.GetUassetHeader(fileBytes);
            fileBytes = Common.RemoveUassetHeader(fileBytes);

           List<Move> MoveList = new List<Move>();
           List<CancelList> CancelLists = new List<CancelList>();
           List<Input> InputList = new List<Input>();
           List<Charge> ChargeList = new List<Charge>(); 

            Debug.WriteLine("READING");
            using (var ms = new MemoryStream(fileBytes))
            using (var inFile = new BinaryReader(ms))
            {
                string bcmString = new string(inFile.ReadChars(4));

                if (bcmString != "#BCM")
                {
                    throw new Exception("Error: Not a valid KWBCM file!");
                }

                Debug.WriteLine(bcmString);

                inFile.BaseStream.Seek(0xA, SeekOrigin.Begin);
                short BCMVER = inFile.ReadInt16();
                Debug.WriteLine("BCMVER: " + BCMVER);
                short ChargeCount = inFile.ReadInt16();
                Debug.WriteLine("ChargeCount: " + ChargeCount);
                short InputCount = inFile.ReadInt16();
                Debug.WriteLine("InputCount: " + InputCount);
                short MoveCount = inFile.ReadInt16();
                Debug.WriteLine("Movecount: " + MoveCount);
                short CancelCount = inFile.ReadInt16(); //last cancel index
                Debug.WriteLine("Cancelcount: " + CancelCount);

                int startOfCharges = inFile.ReadInt32();
                int startOfInputs = inFile.ReadInt32();
                int startOfMoves = inFile.ReadInt32();
                int startOfNames = inFile.ReadInt32();
                int startOfCancelLists = inFile.ReadInt32();
                Debug.WriteLine("StartOfCharges: " + startOfCharges);
                Debug.WriteLine("StartOfInputs: " + startOfInputs);

                Debug.WriteLine("StartOfMoves: " + startOfMoves);
                Debug.WriteLine("StartOfNames: " + startOfNames);

                Debug.WriteLine("StartOfCancelLists: " + startOfCancelLists);

                Debug.WriteLine("Current pos: " + inFile.BaseStream.Position.ToString("X"));
                Debug.WriteLine("\n\n");

                inFile.BaseStream.Seek(startOfCharges, SeekOrigin.Begin);

                List<int> ChargeAddresses = new List<int>();

                for (int i = 0; i < ChargeCount; i++)
                {
                    ChargeAddresses.Add(inFile.ReadInt32());
                }

                for (int i = 0; i < ChargeAddresses.Count; i++)
                {
                    Charge thisCharge = new Charge();
                    int thisChargeAddress = ChargeAddresses[i];
                    inFile.BaseStream.Seek(thisChargeAddress, SeekOrigin.Begin);
                    Debug.WriteLine("ChargeAddress: " + thisChargeAddress.ToString("X"));

                    thisCharge.ChargeDirection = inFile.ReadInt16();
                    thisCharge.Unknown1 = inFile.ReadInt16();
                    thisCharge.Unknown2 = inFile.ReadInt16();
                    thisCharge.Unknown3 = inFile.ReadInt16();
                    thisCharge.ChargeFrames = inFile.ReadInt16();
                    thisCharge.Flags = inFile.ReadInt16();
                    thisCharge.ChargeIndex = inFile.ReadInt16();
                    thisCharge.Unknown4 = inFile.ReadInt16();

                    thisCharge.Index = i;
                    ChargeList.Add(thisCharge);
                }

                inFile.BaseStream.Seek(startOfInputs, SeekOrigin.Begin);

                List<int> InputAddresses = new List<int>();

                for (int i = 0; i < InputCount; i++)
                {
                    InputAddresses.Add(inFile.ReadInt32());
                }

                for (int i = 0; i < InputAddresses.Count; i++)
                {
                    Input thisInput = new Input();
                    int thisInputAddress = InputAddresses[i];
                    inFile.BaseStream.Seek(thisInputAddress, SeekOrigin.Begin);
                    Debug.WriteLine("InputAddress: " + thisInputAddress.ToString("X"));

                    List<int> moveEntryOffsets = new List<int>();

                    moveEntryOffsets.Add(inFile.ReadInt32());
                    moveEntryOffsets.Add(inFile.ReadInt32());
                    moveEntryOffsets.Add(inFile.ReadInt32());
                    moveEntryOffsets.Add(inFile.ReadInt32());

                    var entries = new List<InputEntry>();

                    foreach (var entryOffset in moveEntryOffsets)
                    {
                        if (entryOffset == 0)
                        {
                            entries.Add(new InputEntry());
                            continue;
                        }

                        inFile.BaseStream.Seek(entryOffset + thisInputAddress, SeekOrigin.Begin);
                        var partCount = inFile.ReadInt32();

                        InputEntry thisInputEntry = new InputEntry();
                        List<InputPart> parts = new List<InputPart>();

                        for (int j = 0; j < partCount; j++)
                        {
                            InputPart thisPart = new InputPart()
                            {
                                InputType = (InputType)inFile.ReadInt16(),
                                Buffer = inFile.ReadInt16(),
                                InputDirection = (InputDirection)inFile.ReadInt16(),
                                Unknown1 = inFile.ReadInt16(),
                                Unknown2 = inFile.ReadInt16(),
                                Unknown3 = inFile.ReadInt16(),
                                Unknown4 = inFile.ReadInt16(),
                                Unknown5 = inFile.ReadInt16(),
                            };

                            parts.Add(thisPart);
                        }

                        for (int j = 0; j < 16-partCount; j++) //There can be up to 16 parts, but even if they are empty they are still there, only filled with 0x00
                        {
                            var unused = inFile.ReadBytes(16);
                            foreach (var b in unused)
                            {
                                if (b != 0)
                                {
                                    Debug.WriteLine("Read unexpected byte in what was thought to be an empty part of an input.");
                                }
                            }
                        }

                        thisInputEntry.InputParts = parts.ToArray();
                        entries.Add(thisInputEntry);
                    }

                    thisInput.InputEntries = entries.ToArray();
                
                    thisInput.Index = i;
                    InputList.Add(thisInput);
                    Debug.WriteLine("Created input with index: " + i);
                }

                for (int i = 0; i < MoveCount*4; i+=0x4)
                {
                    long thisMovePosition = startOfMoves + i;
                    long thisNamePosition = startOfNames + i;

                    inFile.BaseStream.Seek(thisNamePosition, SeekOrigin.Begin);
                    int NameAddress = inFile.ReadInt32();
                    string Name = GetName(NameAddress, inFile);
                   
                    inFile.BaseStream.Seek(thisMovePosition, SeekOrigin.Begin);
                    int offset = inFile.ReadInt32();
                    Debug.WriteLine("Adding move at: " + offset.ToString("X"));

                    inFile.BaseStream.Seek(offset, SeekOrigin.Begin);

                    short input = inFile.ReadInt16();
                    short inputFlags = inFile.ReadInt16();
                    int restrict = inFile.ReadInt32();
                    int restrict2 = inFile.ReadInt32();
                    float restrictDistance = inFile.ReadSingle();
                    int unknown4 = (BCMVER > 0) ? inFile.ReadInt32() : 0;
                    int projectileRestrict = inFile.ReadInt32();
                    int unknown6 = inFile.ReadInt16();
                    int unknown7 = inFile.ReadInt16();
                    short unknown8 = inFile.ReadInt16();
                    short unknown9 = inFile.ReadInt16();

                    short MeterRequirement = inFile.ReadInt16();
                    short MeterUsed = inFile.ReadInt16();
                    short unknown10 = inFile.ReadInt16();
                    short unknown11 = inFile.ReadInt16();
                    int VtriggerRequirement = inFile.ReadInt16();
                    int VtriggerUsed = inFile.ReadInt16();
                    int Unknown16 = inFile.ReadInt32();
                    int InputMotionIndex = inFile.ReadInt16();
                    int ScriptIndex = inFile.ReadInt16();

                    Move thisMove = new Move()
                    {
                        Name = Name,
                        Index = (short)(i == 0 ? 0 : (i / 4)), 
                        Input = input,
                        InputFlags = inputFlags,
                        PositionRestriction = restrict,
                        Unknown3 = restrict2,
                        RestrictionDistance = restrictDistance,
                        Unknown4 = unknown4,
                        ProjectileLimit = projectileRestrict,
                        Unknown6 = (short)unknown6,
                        Unknown7 = (short)unknown7,
                        Unknown8 = unknown8,
                        Unknown9 = unknown9,
                        Unknown10 = unknown10,
                        Unknown11 = unknown11,
                        MeterRequirement =  (short)MeterRequirement,
                        MeterUsed = (short)MeterUsed,
                        VtriggerRequirement = (short)VtriggerRequirement,
                        VtriggerUsed = (short)VtriggerUsed,
                        Unknown16 = Unknown16,
                        InputMotionIndex = (short)InputMotionIndex,
                        ScriptIndex = (short)ScriptIndex,
                        Unknown17 = inFile.ReadInt32(),
                        Unknown18 = inFile.ReadInt32(),
                        Unknown19 = (BCMVER == 0) ? inFile.ReadInt32() : 0,
                        Unknown20 = inFile.ReadSingle(),
                        Unknown21 = inFile.ReadSingle(),
                        Unknown22 = inFile.ReadInt32(),
                        Unknown23 = inFile.ReadInt32(),
                        Unknown24 = inFile.ReadInt32(),
                        Unknown25 = inFile.ReadInt32(),
                        Unknown26 = inFile.ReadInt16(),
                        NormalOrVtrigger = inFile.ReadInt16(),
                        Unknown28 = inFile.ReadInt32()
                    };

                    if (thisMove.InputMotionIndex != -1) //Just for debugging...
                    {
                        InputList.Where(x => x.Index == thisMove.InputMotionIndex).ToList()[0].Name += thisMove.Name + ", ";
                    }

                    MoveList.Add(thisMove);

                    Debug.WriteLine("MOVE: " + "Index: " + (i == 0 ? 0 : (i/4)) +
                                    "\nName: " + Name +
                                    "\nOffset: " + offset.ToString("X") +
                                    "\nNameOffet: " + NameAddress.ToString("X") +
                                    "\nInput: " + input +
                                    "\nflags: " + inputFlags
                                    + "\nRestrict: " + restrict
                                    + "\nRestrict2: " + restrict2
                                    + "\nRestrictDistance: " + restrictDistance
                                    + "\nUnknown4: " + unknown4
                                    + "\nProjectileRestrict: " + projectileRestrict
                                    + "\nUnknown6: " + unknown6
                                    + "\nUnknown7: " + unknown7
                                    + "\nUnknown8: " + unknown8
                                    + "\nUnknown9: " + unknown9
                                    + "\nUnknown10: " + unknown10
                                    + "\nUnknown11: " + unknown11
                                    + "\nMeterReq: " + MeterRequirement
                                    + "\nMeterUsed: " + MeterUsed
                                    + "\nVtriggerReq: " + VtriggerRequirement
                                    + "\nVtriggerUsed: " + VtriggerUsed
                                    + "\nUnknown16: " + Unknown16
                                    + "\nInputMotionIndex: " + InputMotionIndex
                                    + "\nScriptIndex: " + ScriptIndex
                                    +"\nUnknown17: " + thisMove.Unknown17
                                    + "\nUnknown18: " + thisMove.Unknown18
                                    + "\nUnknown19: " + thisMove.Unknown19
                                    + "\nUnknown20: " + thisMove.Unknown20
                                    + "\nUnknown21: " + thisMove.Unknown21
                                    + "\nUnknown22: " + thisMove.Unknown22
                                    + "\nUnknown23: " + thisMove.Unknown23
                                    + "\nUnknown24: " + thisMove.Unknown24
                                    + "\nUnknown25: " + thisMove.Unknown25
                                    + "\nUnknown26: " + thisMove.Unknown26
                                    + "\nUnknown27: " + thisMove.NormalOrVtrigger
                                    + "\nUnknown28: " + thisMove.Unknown28
                                    + "\n\n");
                }

                inFile.BaseStream.Seek(startOfCancelLists, SeekOrigin.Begin);
                List<int> CancelAddresses = new List<int>();
                for (int i = 0; i < CancelCount; i++)
                {
                    int thisCancelAddress = inFile.ReadInt32();
                    Debug.WriteLine("Cancel " + (i) + ": " + thisCancelAddress.ToString("X"));
                    CancelAddresses.Add(thisCancelAddress);
                }

                for (int i = 0; i < CancelAddresses.Count; i++)
                {
                    CancelList thisCancelList = new CancelList();
                    int thisAddress = CancelAddresses[i];

                    if (thisAddress == 0)
                    {
                        CancelLists.Add(new CancelList());
                        continue;
                    }

                    inFile.BaseStream.Seek(thisAddress, SeekOrigin.Begin);

                    thisCancelList.Unknown1 = inFile.ReadInt32();
                    int MovesInList = inFile.ReadInt32();
                    int LastIndex = inFile.ReadInt32(); //last move index in list -1...
                    int StartOffset = inFile.ReadInt32(); //offset until real list FROM START OF CANCEL!!!
                    int StartOfCancelInts = inFile.ReadInt32();
                    int StartOfCancelBytes = inFile.ReadInt32(); 

                    Debug.WriteLine("ThisCancelAddress: " + thisAddress.ToString("X"));
                    Debug.WriteLine("Cancel {6}:\nCU1: {0}\nMovesInList: {1}\nNumberOfSomethingInList: {2}\nStartOffset: {3}\nCU5: {4}\nEndOffset: {5}\n", thisCancelList.Unknown1, MovesInList, LastIndex, StartOffset.ToString("X"), StartOfCancelInts.ToString("X"), StartOfCancelBytes.ToString("X"), i);

                    inFile.BaseStream.Seek(thisAddress + StartOffset, SeekOrigin.Begin);
                    Debug.WriteLine("ListAddress: " + (thisAddress + StartOffset).ToString("X"));
                    Debug.WriteLine("ListAddressEnd: " + (thisAddress + StartOfCancelBytes).ToString("X"));

                    List<Cancel> cancels = new List<Cancel>();
                    thisCancelList.Index = i;

                    for (int j = 0; j < MovesInList; j++)
                    {
                        int thisMoveInList = inFile.ReadInt16();
                        Debug.WriteLine("Move: " + thisMoveInList);

                        Move cancelMove = MoveList.Where(x => x.Index == thisMoveInList).ToList()[0];

                        Cancel thisCancel = new Cancel();
                        thisCancel.Index = (short)thisMoveInList;
                        thisCancel.Name = cancelMove.Name;
                        thisCancel.ScriptIndex = cancelMove.ScriptIndex;

                        cancels.Add(thisCancel);
                    }

                    thisCancelList.Cancels = cancels.ToArray();

                    //All lists should have Moves divisible by 2. If it doesn't, simply add an empty one (0x00, 0x00)
                    if (MovesInList % 2 != 0)
                    {
                        Debug.WriteLine("READING EMPTY MOVE");
                        inFile.ReadInt16();
                    }

                    if (StartOfCancelInts != 0)
                    {
                        Debug.WriteLine("We got something!!!" + inFile.BaseStream.Position.ToString("X") + " - Should be: " + (thisAddress + StartOfCancelInts).ToString("X"));
                        Debug.WriteLine(((thisAddress + StartOfCancelBytes) - (thisAddress + StartOfCancelInts)) / MovesInList);

                        for (int j = 0; j < MovesInList; j++)
                        {
                            int value1 = inFile.ReadInt32();
                            int value2 = inFile.ReadInt32();

                            thisCancelList.Cancels[j].CancelInts = new CancelInts()
                            {
                                Unknown1 = value1,
                                Unknown2 = value2
                            };
                        }
                    }

                    Debug.WriteLine("Position is " + inFile.BaseStream.Position.ToString("X") + " - Should be: " + (thisAddress + StartOfCancelBytes).ToString("X"));

                    if (inFile.BaseStream.Position != thisAddress + StartOfCancelBytes) //NOT a good idea
                    {
                        Debug.WriteLine("We are not where we're supposed to be, reading bytes until we are...");

                        while (inFile.BaseStream.Position != thisAddress + StartOfCancelBytes)
                        {
                            Debug.WriteLine(inFile.ReadByte());
                        }

                        Debug.WriteLine("Position is " + inFile.BaseStream.Position.ToString("X") + " - Should be: " + (thisAddress + StartOfCancelBytes).ToString("X"));
                    }

                    for (int j = 0; j < LastIndex; j++)
                    {
                        inFile.BaseStream.Seek((thisAddress + StartOfCancelBytes) + (j * 4), SeekOrigin.Begin);

                        var offset = inFile.ReadInt32();

                        if (offset == 0)
                        {
                            continue;
                        }

                        Debug.WriteLine("SomethingElseInCancelList(offsets?): " + offset.ToString("X") + " Pos: " + (inFile.BaseStream.Position - 4).ToString("X") + " - Index: " + j + "  added offset:" + (offset+thisAddress).ToString("X"));

                        var address = offset + thisAddress;

                        inFile.BaseStream.Seek(address, SeekOrigin.Begin);

                        var cancelBytesBelongsTo = thisCancelList.Cancels.First(x => x.Index == j);

                        cancelBytesBelongsTo.UnknownBytes = inFile.ReadBytes(0x24);
                    }

                    CancelLists.Add(thisCancelList);
                    Debug.WriteLine("\n");
                }

                foreach (var cancelList in CancelLists)
                {
                    if (cancelList.Cancels == null)
                    {
                        continue;
                    }

                    foreach (var cancel in cancelList.Cancels)
                    {
                        if (cancel == null)
                        {
                            continue;
                        }
                        Debug.WriteLine("Cancel: " + cancel.Index + " ScriptIndex:" + cancel.ScriptIndex);
                        foreach (var unknownByte in cancel.UnknownBytes)
                        {
                            Debug.Write(unknownByte.ToString("X") + " ");
                        }
                        Debug.WriteLine("");
                    }
                }

                Debug.WriteLine("\nCharges\n");

                foreach (var charge in ChargeList)
                {
                    Debug.WriteLine("CHARGE: " + charge.Index);
                    Debug.WriteLine("Dir: " + charge.ChargeDirection);
                    Debug.WriteLine("u1: " + charge.Unknown1);
                    Debug.WriteLine("u2: " + charge.Unknown2);
                    Debug.WriteLine("u3: " + charge.Unknown3);
                    Debug.WriteLine("ChargeFrames: " + charge.ChargeFrames);
                    Debug.WriteLine("Flags: " + charge.Flags);
                    Debug.WriteLine("CINDEX: " + charge.ChargeIndex);
                    Debug.WriteLine("u4: " + charge.Unknown4);
                    Debug.WriteLine("\n");
                }

                foreach (var input in InputList)
                {
                    Debug.WriteLine("Input: " + input.Index);
                    Debug.WriteLine("Name: " + input.Name);
                    Debug.WriteLine("Entries: " + input.InputEntries.Length);

                    WriteInputToDebug(input);

                    Debug.WriteLine("\n");
                }
            }

            Debug.WriteLine("Done");

            BCMFile bcm = new BCMFile()
            {
                Inputs = InputList.ToArray(),
                CancelLists = CancelLists.ToArray(),
                Charges = ChargeList.ToArray(),
                Moves = MoveList.ToArray(),
                RawUassetHeaderDontTouch = UassetHeaderBytes
            };

            return bcm;
        }

        private static void WriteInt32ToPosition(BinaryWriter outFile, long position, int Value)
        {
            long oldPosition = outFile.BaseStream.Position;
            outFile.BaseStream.Seek(position, SeekOrigin.Begin);
            outFile.Write(Value);
            outFile.BaseStream.Seek(oldPosition, SeekOrigin.Begin);
        }

        public static void ToUassetFile(BCMFile file, string fileName)
        {
            byte[] outPutFileBytes;

            using (var ms = new MemoryStream())
            {
                using (var outFile = new BinaryWriter(ms))
                {
                    byte[] headerBytes =
                    {
                        0x23, 0x42, 0x43, 0x4D, 0xFE, 0xFF, 0x2c, 0x00, 0x00, 0x00, 0x00, 0x00
                    };

                    outFile.Write(headerBytes);

                    outFile.Write((short) file.Charges.Length);
                    outFile.Write((short) file.Inputs.Length);
                    outFile.Write((short) file.Moves.Length);
                    outFile.Write((short) file.CancelLists.Length);

                    var StartOfStartOfChargeOffsets = outFile.BaseStream.Position;
                    outFile.Write(0);

                    var StartOfStartOfInputOffsets = outFile.BaseStream.Position;
                    outFile.Write(0);

                    var StartOfStartOfMoveOffsets = outFile.BaseStream.Position;
                    outFile.Write(0);

                    var StartOfStartOfNameOffsets = outFile.BaseStream.Position;
                    outFile.Write(0);

                    var StartOfStartOfCancelListOffsets = outFile.BaseStream.Position;
                    outFile.Write(0);

                    outFile.Write(0); //Unused?

                    var StartOfChargeOffsets = outFile.BaseStream.Position;
                    if (file.Charges.Length > 0)
                    {
                        WriteInt32ToPosition(outFile, StartOfStartOfChargeOffsets, (int) outFile.BaseStream.Position);
                    }
                    for (int i = 0; i < file.Charges.Length; i++)
                    {
                        outFile.Write(0);
                    }

                    var StartOfInputOffsets = outFile.BaseStream.Position;
                    if (file.Inputs.Length > 0)
                    {
                        WriteInt32ToPosition(outFile, StartOfStartOfInputOffsets, (int)outFile.BaseStream.Position);
                    }
                    for (int i = 0; i < file.Inputs.Length; i++)
                    {
                        outFile.Write(0);
                    }

                    var StartOfMoveOffsets = outFile.BaseStream.Position;
                    if (file.Moves.Length > 0)
                    {
                        WriteInt32ToPosition(outFile, StartOfStartOfMoveOffsets, (int)outFile.BaseStream.Position);
                    }
                    for (int i = 0; i < file.Moves.Length; i++)
                    {
                        outFile.Write(0);
                    }

                    var StartOfNameOffsets = outFile.BaseStream.Position;
                    if (file.Moves.Length > 0)
                    {
                        WriteInt32ToPosition(outFile, StartOfStartOfNameOffsets, (int)outFile.BaseStream.Position);
                    }
                    for (int i = 0; i < file.Moves.Length; i++)
                    {
                        outFile.Write(0);
                    }

                    var StartOfCancelListOffsets = outFile.BaseStream.Position;
                    if (file.CancelLists.Length > 0)
                    {
                        WriteInt32ToPosition(outFile, StartOfStartOfCancelListOffsets, (int)outFile.BaseStream.Position);
                    }
                    for (int i = 0; i < file.CancelLists.Length; i++)
                    {
                        outFile.Write(0);
                    }

                    Debug.WriteLine("Done writing temp offsets, now at: " + outFile.BaseStream.Position.ToString("X"));

                    for (int i = 0; i < file.Charges.Length; i++)
                    {
                        WriteInt32ToPosition(outFile, StartOfChargeOffsets + (i*4), (int)outFile.BaseStream.Position);

                        outFile.Write(file.Charges[i].ChargeDirection);
                        outFile.Write(file.Charges[i].Unknown1);
                        outFile.Write(file.Charges[i].Unknown2);
                        outFile.Write(file.Charges[i].Unknown3);
                        outFile.Write(file.Charges[i].ChargeFrames);
                        outFile.Write(file.Charges[i].Flags);
                        outFile.Write(file.Charges[i].ChargeIndex);
                        outFile.Write(file.Charges[i].Unknown4);
                    }

                    Debug.WriteLine("Done writing charges, now at: " + outFile.BaseStream.Position.ToString("X"));

                    for (int i = 0; i < file.Inputs.Length; i++)
                    {
                        WriteInt32ToPosition(outFile, StartOfInputOffsets + (i * 4), (int)outFile.BaseStream.Position);
                        var entryOffsetPosition = outFile.BaseStream.Position;

                        outFile.Write(0);
                        outFile.Write(0);
                        outFile.Write(0);
                        outFile.Write(0);

                        for (int j = 0; j < file.Inputs[i].InputEntries.Length; j++)
                        {
                            if (file.Inputs[i].InputEntries[j].InputParts == null)
                            {
                                continue;
                            }

                            WriteInt32ToPosition(outFile, entryOffsetPosition + (j*4), (int)(outFile.BaseStream.Position- entryOffsetPosition));

                            outFile.Write(file.Inputs[i].InputEntries[j].InputParts.Length);

                            for (int k = 0; k < file.Inputs[i].InputEntries[j].InputParts.Length; k++)
                            {
                                outFile.Write((short)file.Inputs[i].InputEntries[j].InputParts[k].InputType);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Buffer);
                                outFile.Write((short)file.Inputs[i].InputEntries[j].InputParts[k].InputDirection);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Unknown1);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Unknown2);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Unknown3);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Unknown4);
                                outFile.Write(file.Inputs[i].InputEntries[j].InputParts[k].Unknown5);
                            }

                            for (int k = 0; k < 16 - file.Inputs[i].InputEntries[j].InputParts.Length; k++)
                            {
                                for (int l = 0; l < 16; l++)
                                {
                                    outFile.Write((byte)0x00);
                                }
                            }
                        }
                    }

                    Debug.WriteLine("Done writing Inputs, now at: " + outFile.BaseStream.Position.ToString("X"));

                    for (int i = 0; i < file.Moves.Length; i++)
                    {
                        WriteInt32ToPosition(outFile,StartOfMoveOffsets + (i*4), (int)outFile.BaseStream.Position);
                    
                        outFile.Write(file.Moves[i].Input);
                        outFile.Write(file.Moves[i].InputFlags);
                        outFile.Write(file.Moves[i].PositionRestriction);
                        outFile.Write(file.Moves[i].Unknown3);
                        outFile.Write(file.Moves[i].RestrictionDistance);
                        outFile.Write(file.Moves[i].ProjectileLimit);
                        outFile.Write(file.Moves[i].Unknown6);
                        outFile.Write(file.Moves[i].Unknown7);
                        outFile.Write(file.Moves[i].Unknown8);
                        outFile.Write(file.Moves[i].Unknown9);

                        outFile.Write(file.Moves[i].MeterRequirement);
                        outFile.Write(file.Moves[i].MeterUsed);

                        outFile.Write(file.Moves[i].Unknown10);
                        outFile.Write(file.Moves[i].Unknown11);

                        outFile.Write(file.Moves[i].VtriggerRequirement);
                        outFile.Write(file.Moves[i].VtriggerUsed);

                        outFile.Write(file.Moves[i].Unknown16);
                        outFile.Write(file.Moves[i].InputMotionIndex);
                        outFile.Write(file.Moves[i].ScriptIndex);

                        outFile.Write(file.Moves[i].Unknown17);
                        outFile.Write(file.Moves[i].Unknown18);
                        outFile.Write(file.Moves[i].Unknown19);
                        outFile.Write(file.Moves[i].Unknown20);
                        outFile.Write(file.Moves[i].Unknown21);
                        outFile.Write(file.Moves[i].Unknown22);
                        outFile.Write(file.Moves[i].Unknown23);
                        outFile.Write(file.Moves[i].Unknown24);
                        outFile.Write(file.Moves[i].Unknown25);
                        outFile.Write(file.Moves[i].Unknown26);
                        outFile.Write(file.Moves[i].NormalOrVtrigger);
                        outFile.Write(file.Moves[i].Unknown28);
                    }

                    Debug.WriteLine("Done writing Moves, now at: " + outFile.BaseStream.Position.ToString("X"));

                    List<long> CancelListOffsets = new List<long>();
                    List<long> StartOfCancelListsList = new List<long>();

                    for (int i = 0; i < file.CancelLists.Length; i++)
                    {
                        StartOfCancelListsList.Add(outFile.BaseStream.Position);
                        if (file.CancelLists[i].Cancels == null)
                        {
                            WriteInt32ToPosition(outFile, StartOfCancelListOffsets + (i*4), 0);
                            CancelListOffsets.Add(-1);
                            continue;
                        }

                        WriteInt32ToPosition(outFile, StartOfCancelListOffsets + (i*4),
                            (int) outFile.BaseStream.Position);

                        outFile.Write(file.CancelLists[i].Unknown1);
                        outFile.Write(file.CancelLists[i].Cancels.Length);
                        outFile.Write(file.CancelLists[i].Cancels[file.CancelLists[i].Cancels.Length - 1].Index + 1);

                        CancelListOffsets.Add(outFile.BaseStream.Position);

                        outFile.Write(0);
                        outFile.Write(0);
                        outFile.Write(0);
                    }

                    for (int i = 0; i < file.CancelLists.Length; i++)
                    {
                        if (file.CancelLists[i].Cancels == null)
                        {
                            continue;
                        }

                        Debug.WriteLine("CancelListAtAddress: " + outFile.BaseStream.Position.ToString("X"));
                        if (CancelListOffsets[i] != -1)
                        {
                            WriteInt32ToPosition(outFile, CancelListOffsets[i], (int)(outFile.BaseStream.Position - StartOfCancelListsList[i]));
                        }

                        for (int j = 0; j < file.CancelLists[i].Cancels.Length; j++)
                        {
                            outFile.Write(file.CancelLists[i].Cancels[j].Index);
                        }

                        if (file.CancelLists[i].Cancels.Length%2 != 0)
                        {
                            Debug.WriteLine("Writing empty move: " + outFile.BaseStream.Position.ToString("X"));
                            outFile.Write((short)0);
                        }

                        bool shouldWrite = false;
                        var CancelIntsPosition = outFile.BaseStream.Position;

                        foreach (var cancel in file.CancelLists[i].Cancels)
                        {
                            if (cancel.CancelInts != null)
                            {
                                outFile.Write(cancel.CancelInts.Unknown1);
                                outFile.Write(cancel.CancelInts.Unknown2);
                                shouldWrite = true;
                            }
                        }

                        if (shouldWrite)
                        {
                            if (CancelListOffsets[i] != -1)
                            {
                                WriteInt32ToPosition(outFile, CancelListOffsets[i] + 4,
                                    (int) (CancelIntsPosition - StartOfCancelListsList[i]));
                            }
                        }

                        Debug.WriteLine("Startofunknownbytes: " + outFile.BaseStream.Position.ToString("X"));

                        if (CancelListOffsets[i] != -1)
                        {
                            WriteInt32ToPosition(outFile, CancelListOffsets[i]+8, (int)(outFile.BaseStream.Position - StartOfCancelListsList[i]));
                        }

                        var numberOfOffsets =
                            file.CancelLists[i].Cancels[file.CancelLists[i].Cancels.Length - 1].Index + 1; //???? +1 only when not dividable by 2??

                        var UnknownBytesOffsetPosition = outFile.BaseStream.Position;

                        for (int j = 0; j < numberOfOffsets; j++)
                        {
                            outFile.Write(0);
                        }

                        for (int j = 0; j < file.CancelLists[i].Cancels.Length; j++)
                        {
                            if (file.CancelLists[i].Cancels[j].UnknownBytes != null)
                            {
                                WriteInt32ToPosition(outFile, UnknownBytesOffsetPosition  + (file.CancelLists[i].Cancels[j].Index*4), (int)(outFile.BaseStream.Position- StartOfCancelListsList[i]));
                                outFile.Write(file.CancelLists[i].Cancels[j].UnknownBytes);
                            }
                        }
                    }
                    
                    Debug.WriteLine("Done writing Cancels, now at: " + outFile.BaseStream.Position.ToString("X"));

                    for (int i = 0; i < file.Moves.Length; i++)
                    {
                        WriteInt32ToPosition(outFile, StartOfNameOffsets + (i*4), (int)outFile.BaseStream.Position);

                        outFile.Write(file.Moves[i].Name.ToCharArray());
                        outFile.Write((byte)0x00);
                    }

                    Debug.WriteLine("Done writing names, now at: " + outFile.BaseStream.Position.ToString("X"));

                    outPutFileBytes = ms.ToArray();

                    Debug.WriteLine("Done.");
                }
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

            File.WriteAllBytes(fileName, outPut.ToArray());
        }

        private static void WriteInputToDebug(Input input)
        {
            foreach (var entry  in input.InputEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                string output = "Entry:\n";

                if (entry.InputParts != null)
                {
                    foreach (var inputPart in entry.InputParts)
                    {

                        output += inputPart.InputType + ", Buffer: " + inputPart.Buffer + ", Direction: " +
                                  inputPart.InputDirection + ", U1:"
                                  + inputPart.Unknown1
                                  + ", U2:" + inputPart.Unknown2
                                  + ", U3:" + inputPart.Unknown3
                                  + ", U4:" + inputPart.Unknown4
                                  + ", U5:" + inputPart.Unknown5 + "\n";

                    }
                }
                Debug.WriteLine(output);

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
    }

    public class BCMFile
    {
        public Charge[] Charges { get; set; }
        public Input[] Inputs { get; set; }
        public Move[] Moves { get; set; }
        public CancelList[] CancelLists { get; set; }
        public byte[] RawUassetHeaderDontTouch { get; set; }
    }

    public class CancelList
    {
        public int Index { get; set; }
        public int Unknown1 { get; set; }
        public Cancel[] Cancels { get; set; }
    }

    public class Cancel
    {
        public string Name { get; set; }
        public short Index { get; set; }
        public int ScriptIndex { get; set; }
        public CancelInts CancelInts { get; set; }
        public byte[] UnknownBytes { get; set; }
    }

    public class CancelInts
    {
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
    }

    public class Move
    {
        public byte Offset { get; set; }
        public short Index { get; set; }
        public string Name { get; set; }
        public short Input { get; set; }
        public short InputFlags { get; set; }
        public int PositionRestriction { get; set; }
        public int Unknown3 { get; set; }
        public float RestrictionDistance { get; set; }
        public int Unknown4 { get; set; }
        public int ProjectileLimit { get; set; }
        public short Unknown6 { get; set; }
        public short Unknown7 { get; set; }
        public short Unknown8 { get; set; }
        public short Unknown9 { get; set; }
        public short MeterRequirement { get; set; }
        public short MeterUsed { get; set; }
        public short Unknown10 { get; set; }
        public short Unknown11 { get; set; }
        public short VtriggerRequirement { get; set; }
        public short VtriggerUsed { get; set; }
        public int Unknown16 { get; set; }
        public short InputMotionIndex { get; set; }
        public short ScriptIndex { get; set; }

        public int Unknown17 { get; set; }
        public int Unknown18 { get; set; }
        public int Unknown19 { get; set; }
        public float Unknown20 { get; set; }
        public float Unknown21 { get; set; }

        public int Unknown22 { get; set; }
        public int Unknown23 { get; set; }
        public int Unknown24 { get; set; }
        public int Unknown25 { get; set; }

        public short Unknown26 { get; set; }
        public short NormalOrVtrigger { get; set; }

        public int Unknown28 { get; set; }
    }

    public class Charge
    {
       public int Index { get; set; }
       public short ChargeDirection { get; set; }
       public short ChargeFrames { get; set; }
       public short Unknown1 { get; set; }
       public short Unknown2 { get; set; }
       public short Unknown3 { get; set; }
       public short Flags { get; set; }
       public short ChargeIndex { get; set; }
       public short Unknown4 { get; set; }
    }

    public class Input
    {
        public int Index { get; set; }
        public InputEntry[] InputEntries { get; set; }
        public string Name { get; set; }
    }
    
    public class InputEntry
    {
        public InputPart[] InputParts { get; set; } 
    }

    public class InputPart
    {
        public short Buffer { get; set; }
        public InputType InputType { get; set; }
        public InputDirection InputDirection { get; set; }
        public short Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public short Unknown4 { get; set; }
        public short Unknown5 { get; set; }
    }

    public enum InputType
    {
        Normal = 0,
        Charge = 1
    }

    [Flags]
    public enum InputDirection
    {
        Neutral = 0, //???
        Up = 1,
        Down = 2,
        Back = 4,
        Forward = 8,
    }
}
