using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RealtimeEditor
{
    public class Memory
    {
        #region OpenProcess
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(
            ProcessAccessTypes desiredAccess,
            Boolean inheritHandle,
            Int32 processId
            );

        private enum ProcessAccessTypes
        {
            PROCESS_TERMINATE = 0x00000001,
            PROCESS_CREATE_THREAD = 0x00000002,
            PROCESS_SET_SESSIONID = 0x00000004,
            PROCESS_VM_OPERATION = 0x00000008,
            PROCESS_VM_READ = 0x00000010,
            PROCESS_VM_WRITE = 0x00000020,
            PROCESS_DUP_HANDLE = 0x00000040,
            PROCESS_CREATE_PROCESS = 0x00000080,
            PROCESS_SET_QUOTA = 0x00000100,
            PROCESS_SET_INFORMATION = 0x00000200,
            PROCESS_QUERY_INFORMATION = 0x00000400,
            STANDARD_RIGHTS_REQUIRED = 0x000F0000,
            SYNCHRONIZE = 0x00100000,
            PROCESS_ALL_ACCESS = PROCESS_TERMINATE | PROCESS_CREATE_THREAD | PROCESS_SET_SESSIONID | PROCESS_VM_OPERATION |
              PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_DUP_HANDLE | PROCESS_CREATE_PROCESS | PROCESS_SET_QUOTA |
              PROCESS_SET_INFORMATION | PROCESS_QUERY_INFORMATION | STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE
        }

        #endregion

        #region ReadProcessMemory

        [DllImport("kernel32.dll")]
        public static extern int ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] bBuffer, uint size, out IntPtr lpNumberOfBytesRead);

        #endregion

        #region WriteProcessMemory

        [DllImport("kernel32.dll")]
        public static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] bBuffer, uint size, out IntPtr lpNumberOfBytesWritten);

        #endregion

        #region VirtualAllocEx

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr address,
            UInt32 size,
            VirtualAllocExTypes allocationType,
            AccessProtectionFlags flags
            );

        private enum VirtualAllocExTypes
        {
            WRITE_WATCH_FLAG_RESET = 0x00000001, // Win98 only
            MEM_COMMIT = 0x00001000,
            MEM_RESERVE = 0x00002000,
            MEM_COMMIT_OR_RESERVE = 0x00003000,
            MEM_DECOMMIT = 0x00004000,
            MEM_RELEASE = 0x00008000,
            MEM_FREE = 0x00010000,
            MEM_PUBLIC = 0x00020000,
            MEM_MAPPED = 0x00040000,
            MEM_RESET = 0x00080000, // Win2K only
            MEM_TOP_DOWN = 0x00100000,
            MEM_WRITE_WATCH = 0x00200000, // Win98 only
            MEM_PHYSICAL = 0x00400000, // Win2K only
            //MEM_4MB_PAGES    = 0x80000000, // ??
            SEC_IMAGE = 0x01000000,
            MEM_IMAGE = SEC_IMAGE
        }

        private enum AccessProtectionFlags
        {
            PAGE_NOACCESS = 0x001,
            PAGE_READONLY = 0x002,
            PAGE_READWRITE = 0x004,
            PAGE_WRITECOPY = 0x008,
            PAGE_EXECUTE = 0x010,
            PAGE_EXECUTE_READ = 0x020,
            PAGE_EXECUTE_READWRITE = 0x040,
            PAGE_EXECUTE_WRITECOPY = 0x080,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400
        }

        #endregion

        #region VirtualQueryEx

        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public enum AllocationProtect : uint
        {
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }

        #endregion

        private Process _process;
        private IntPtr _processHandle;
        public bool ProcessIsOpen;

        public bool OpenProcess(string processName)
        {
            bool found = false;
            Process[] processesByName = Process.GetProcessesByName(processName);
            if (processesByName.Length > 0)
            {

                foreach (var process in processesByName)
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        Console.WriteLine("Not correct process: " + process.Id);
                    }
                    else
                    {
                        Console.WriteLine("Correct process: " + process.Id);
                        _process = process;
                        found = true;
                    }
                }

                if (!found)
                {
                    return false;
                }

                if (_process.Handle != IntPtr.Zero)
                {
                    _processHandle = _process.Handle;
                    //_processHandle = OpenProcess(ProcessAccessTypes.PROCESS_ALL_ACCESS, false, _process.Id);  //Not needed...?
                    ProcessIsOpen = true;
                    return true;
                }
            }

            Console.WriteLine("Game process not found.");
            ProcessIsOpen = false;
            return false;
        }

        public List<long> MemoryScan(byte[] bytesToFind, long scanStart, long scanSize, int searchQuality)
        {
            var bytesToSearch = ReadArrayOfBytes(scanStart, (uint)scanSize);
            List<long> foundAddresses = new List<long>();
            var searchLimit = bytesToSearch.Length - bytesToFind.Length;

            for (long l = 0; l <= searchLimit; l += searchQuality)
            {
                long m = 0;
                for (; m < bytesToFind.Length; m++)
                {
                    if (bytesToFind[m] != bytesToSearch[l + m])
                    {
                        break;
                    }
                }
                if (m == bytesToFind.Length)
                {
                    foundAddresses.Add(l + scanStart);
                }
            }

            return foundAddresses;
        }

        public List<long> MemoryScan(byte[] bytesToFind, long maximumRegionSize )
        {
            var regions = GetMemBasicInfo();

            List<long> foundAddresses = new List<long>();

            Parallel.ForEach(regions, memoryRegion =>
            {
                if (memoryRegion.Size <= maximumRegionSize)
                {
                    foundAddresses.AddRange(
                        MemoryScan(bytesToFind,
                            memoryRegion.Address, memoryRegion.Size, 4));
                }
            });

            return foundAddresses;
        }

        public List<long> MemoryScan(byte[] bytesToFind)
        {
            var regions = GetMemBasicInfo();

            List<long> foundAddresses = new List<long>();

            Parallel.ForEach(regions, memoryRegion =>
            {
                if (memoryRegion.Size < 2000000)
                {
                    foundAddresses.AddRange(
                        MemoryScan(bytesToFind,
                            memoryRegion.Address, memoryRegion.Size, 4));
                }
            });

            return foundAddresses;
        }

        public byte[] ReadArrayOfBytes(long memoryAddress, uint bytesToRead)
        {
            IntPtr ptr;
            byte[] buffer = new byte[bytesToRead];
            ReadProcessMemory(_processHandle, (IntPtr)memoryAddress, buffer, bytesToRead, out ptr);
            return buffer;
        }

        public byte ReadByte(long memoryAddress)
        {
            IntPtr ptr;
            byte[] buffer = new byte[1];
            if (ReadProcessMemory(_processHandle, (IntPtr)memoryAddress, buffer, 1, out ptr) == 0)
            {
                return 0;
            }

            return buffer[0];
        }

        public long MemAlloc(int size)
        {
          var address =  VirtualAllocEx(_processHandle, IntPtr.Zero, (uint)size, VirtualAllocExTypes.MEM_COMMIT_OR_RESERVE,
                AccessProtectionFlags.PAGE_READWRITE);
            return address.ToInt64();
        }

        public long MemAlloc(long position, int size)
        {
            var address = VirtualAllocEx(_processHandle, (IntPtr)position, (uint)size, VirtualAllocExTypes.MEM_COMMIT_OR_RESERVE,
                  AccessProtectionFlags.PAGE_READWRITE);
            return address.ToInt64();
        }

        public bool Write(long memoryAddress, byte[] bytesToWrite)
        {
            IntPtr ptr;
            WriteProcessMemory(_processHandle, (IntPtr)memoryAddress, bytesToWrite, (uint)bytesToWrite.Length, out ptr);
            return ptr.ToInt32() == bytesToWrite.Length;
        }

        public long AllocAndWriteFileToMemory(byte[] fileBytes)
        {
            var address = MemAlloc(fileBytes.Length*10); //Allocate "some" extra space, just in case...
            Write(address, fileBytes);
            return address;
        }

        public void WriteZeroes(long address, long zeroesToWrite)
        {
            List<byte> byteList = new List<byte>();
            for (int i = 0; i < zeroesToWrite; i++)
            {
                byteList.Add(0x00);
            }
            Write(address, byteList.ToArray());
        }

        public List<MemoryRegion> GetMemBasicInfo()
        {
            List<MemoryRegion> regions = new List<MemoryRegion>();
            long MaxAddress = long.MaxValue;
            long address = 0;
            do
            {
                MEMORY_BASIC_INFORMATION m;
                int result = VirtualQueryEx(_processHandle, (IntPtr) address, out m,
                    (uint) Marshal.SizeOf(typeof (MEMORY_BASIC_INFORMATION)));
                regions.Add(new MemoryRegion() {Address = m.BaseAddress.ToInt64(), Size = m.RegionSize.ToInt64()});

                if (address == (long) m.BaseAddress + (long) m.RegionSize)
                {
                    break;
                }
                address = (long) m.BaseAddress + (long) m.RegionSize;
            } 
            while (address <= MaxAddress);

            return regions;
        }

        public struct MemoryRegion
        {
            public long Address;
            public long Size;
        }
    }
}
