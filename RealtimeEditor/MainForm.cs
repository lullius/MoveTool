using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MoveLib;
using MoveLib.BAC;
using MoveLib.BCM;
using System.IO;
using System.Threading;

namespace RealtimeEditor
{
    public partial class MainForm : Form
    {
        List<long> BACAddresses = new List<long>();
        List<long> BCMAddresses = new List<long>();
        List<MemoryFile> MemoryFileList = new List<MemoryFile>();
        Dictionary<string, FileSystemWatcher> fileSystemWatchers = new Dictionary<string, FileSystemWatcher>(); 
        private Memory _memory = new Memory();
        public Memory Memory
        {
            get
            {
                if (!_memory.ProcessIsOpen)
                {
                    var success = _memory.OpenProcess("StreetFighterV");
                    if (!success)
                    {
                        writeToOutput("Game process not found!");
                        return null;
                    }
                }
                return _memory;
            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void writeToOutput(string output)
        {
            Invoke((MethodInvoker)delegate
            {
                lbOutput.Items.Add(DateTime.Now.ToShortTimeString() + " - " + output);
                lbOutput.SelectedIndex = lbOutput.Items.Count - 1;
            });
        }

        private void bTest_Click(object sender, EventArgs e)
        {
            cbBAC.Items.Clear();
            cbBCM.Items.Clear();
            MemoryFileList = new List<MemoryFile>();

            writeToOutput("Scanning game for BAC/BCM files...");
            Application.DoEvents();

            BACAddresses = Memory.MemoryScan(Encoding.UTF8.GetBytes("#BAC"));
            BCMAddresses = Memory.MemoryScan(Encoding.UTF8.GetBytes("#BCM"));

            foreach (var bacAddress in BACAddresses)
            {
                Console.WriteLine("BAC at: " + bacAddress.ToString("X"));
            }

            foreach (var bcmAddress in BCMAddresses)
            {
                Console.WriteLine("BCM at: " + bcmAddress.ToString("X"));
            }

            foreach (var file in Directory.GetFiles("Originals"))
            {
                var fileBytes = File.ReadAllBytes(file);
                fileBytes = Common.RemoveUassetHeader(fileBytes);
                var temp = fileBytes.ToList();
                temp.RemoveRange(fileBytes.Length - 0x10, 0x10);
                fileBytes = temp.ToArray();
                bool found = false;

                foreach (var bacAddress in BACAddresses)
                {
                    var foundAddresses = Memory.MemoryScan(fileBytes, bacAddress, fileBytes.Length, 1);

                    if (foundAddresses.Count != 0)
                    {
                        MemoryFileList.Add(new MemoryFile()
                        {
                            OriginalAddress = bacAddress,
                            Name = Path.GetFileName(file),
                            Type = MemoryFileType.BAC
                        });
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }

                foreach (var bcmAddress in BCMAddresses)
                {
                    var foundAddresses = Memory.MemoryScan(fileBytes, bcmAddress, fileBytes.Length, 1);

                    if (foundAddresses.Count != 0)
                    {
                        MemoryFileList.Add(new MemoryFile()
                        {
                            OriginalAddress = bcmAddress,
                            Name = Path.GetFileName(file),
                            Type = MemoryFileType.BCM
                        });
                        break;
                    }
                }
            }

            int counter = 0;
            foreach (var bacAddress in BACAddresses)
            {
                counter++;
                if (!MemoryFileList.Exists(m => m.OriginalAddress == bacAddress))
                {
                    MemoryFileList.Add(new MemoryFile()
                    {
                        OriginalAddress = bacAddress,
                        Name = "Unknown" + counter,
                        Type = MemoryFileType.BAC
                    });
                }
            }

            counter = 0;

            foreach (var bcmAddress in BCMAddresses)
            {
                counter++;
                if (!MemoryFileList.Exists(m => m.OriginalAddress == bcmAddress))
                {
                    MemoryFileList.Add(new MemoryFile()
                    {
                        OriginalAddress = bcmAddress,
                        Name = "Unknown" + counter,
                        Type = MemoryFileType.BCM
                    });
                }
            }

            foreach (var memoryFile in MemoryFileList)
            {
                memoryFile.Pointers = Memory.MemoryScan(BitConverter.GetBytes(memoryFile.OriginalAddress));
                Console.WriteLine("\nFile:\nOriginalAddress: " + memoryFile.OriginalAddress.ToString("X") + "\nName: " + memoryFile.Name);
                foreach (var pointer in memoryFile.Pointers)
                {
                    Console.WriteLine("Pointer: " + pointer.ToString("X"));
                }
            }

            foreach (var memoryFile in MemoryFileList)
            {
                if (memoryFile.Type == MemoryFileType.BAC)
                {
                    cbBAC.Items.Add(Path.GetFileName(memoryFile.Name));
                }
                else
                {
                    cbBCM.Items.Add(Path.GetFileName(memoryFile.Name));
                }
                writeToOutput("File: " + memoryFile.Name + " at: " + memoryFile.OriginalAddress.ToString("X"));
            }

            if (cbBAC.Items.Count > 0)
            {
                cbBAC.SelectedIndex = 0;
            }

            if (cbBCM.Items.Count > 0)
            {
                cbBCM.SelectedIndex = 0;
            }

            Console.WriteLine("Done.");
            writeToOutput("Ready to load JSON.");
        }

        private void bSelectBACJson_Click(object sender, EventArgs e)
        {
            FileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JSON-file | *.json";
            var result = dialog.ShowDialog();

            if (result != DialogResult.OK)
            {
                return;
            }

            MemoryFileList.First(m => m.Name == cbBAC.SelectedItem.ToString()).JsonFile = dialog.FileName;

            foreach (var memoryFile in MemoryFileList)
            {
                Console.WriteLine("File:\nOriginalAddress: " + memoryFile.OriginalAddress.ToString("X") + "\nName: " + memoryFile.Name + "\nJSON: " + memoryFile.JsonFile );
            }

            lBAC.Text = dialog.FileName;

            var path = Path.GetDirectoryName(dialog.FileName);

            if (!fileSystemWatchers.ContainsKey(path))
            {
                fileSystemWatchers.Add(path, new FileSystemWatcher(path));
            }

            foreach (var fileSystemWatcher in fileSystemWatchers)
            {
                fileSystemWatcher.Value.EnableRaisingEvents = true;
                fileSystemWatcher.Value.Changed -= FileModified;
                fileSystemWatcher.Value.Changed += FileModified;
            }

            UpdateFileInMemory(dialog.FileName);
            writeToOutput("Loaded: " + Path.GetFileName(dialog.FileName));
        }

        private void bSelectBCMJson_Click(object sender, EventArgs e)
        {
            FileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JSON-file | *.json";
            var result = dialog.ShowDialog();

            if (result != DialogResult.OK)
            {
                return;
            }

            MemoryFileList.First(m => m.Name == cbBCM.SelectedItem.ToString()).JsonFile = dialog.FileName;

            foreach (var memoryFile in MemoryFileList)
            {
                Console.WriteLine("File:\nOriginalAddress: " + memoryFile.OriginalAddress.ToString("X") + "\nName: " + memoryFile.Name + "\nJSON: " + memoryFile.JsonFile);
            }

            lBCM.Text = dialog.FileName;
            var path = Path.GetDirectoryName(dialog.FileName);

            if (!fileSystemWatchers.ContainsKey(path))
            {
                fileSystemWatchers.Add(path, new FileSystemWatcher(path));
            }

            foreach (var fileSystemWatcher in fileSystemWatchers)
            {
                fileSystemWatcher.Value.EnableRaisingEvents = true;
                fileSystemWatcher.Value.Changed -= FileModified;
                fileSystemWatcher.Value.Changed += FileModified;
            }

            UpdateFileInMemory(dialog.FileName);
            writeToOutput("Loaded: " + Path.GetFileName(dialog.FileName));
        }

        void FileModified(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("File modified: " + e.FullPath);
            Console.WriteLine("Updating file in memory");
            writeToOutput("File modified: " + e.Name);
            WaitReady(e.FullPath);
            UpdateFileInMemory(e.FullPath);
        }

        public static void WaitReady(string fileName)
        {
            while (true)
            {
                try
                {
                    using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        if (stream != null)
                        {
                            Trace.WriteLine(string.Format("Output file {0} ready.", fileName));
                            break;
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {
                    Trace.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
                }
                catch (IOException ex)
                {
                   Trace.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
                }
                Thread.Sleep(500);
            }
        }

        private void UpdateFileInMemory(string fileName)
        {
            if (MemoryFileList.Exists(m => m.JsonFile == fileName))
            {
                var tempFileName = "temp_" + Path.GetFileName(fileName);
                var success = BAC.JsonToBac(fileName, tempFileName);

                if (!success)
                {
                    success = BCM.JsonToBcm(fileName,
                        tempFileName);
                }

                if (!success)
                {
                    lbOutput.Items.Add("Couldn't parse JSON");
                    return;
                }

                var memFile = MemoryFileList.First(m => m.JsonFile == fileName);

                var fileBytes = File.ReadAllBytes(tempFileName).ToList();
                fileBytes.RemoveRange(0, BitConverter.ToInt32(fileBytes.ToArray(), 0x18) + 36);

                if (memFile.NewAddress == 0)
                {
                    Console.WriteLine("Allocating memory space...");
                    var newAddress = Memory.AllocAndWriteFileToMemory(fileBytes.ToArray());
                    Console.WriteLine("Got some space at: " + newAddress.ToString("X"));
                    memFile.NewAddress = newAddress;
                    Console.WriteLine("Wrote file to: " + newAddress.ToString("X"));

                    foreach (var pointer in memFile.Pointers)
                    {
                        Memory.Write(pointer, BitConverter.GetBytes(memFile.NewAddress));
                        Console.WriteLine("Updated pointer at: " + pointer.ToString("X"));
                    }
                }

                Memory.Write(memFile.NewAddress, fileBytes.ToArray());
                Console.WriteLine("Wrote file to: " + memFile.NewAddress.ToString("X"));
                writeToOutput("Wrote: " + Path.GetFileName(fileName) + " to " + memFile.Name + " - " +
                              memFile.NewAddress.ToString("X"));
            }
        }

        private void cbBAC_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MemoryFileList.Exists(m => m.Name == cbBAC.SelectedItem.ToString()))
            {
                lBAC.Text = MemoryFileList.First(m => m.Name == cbBAC.SelectedItem.ToString()).JsonFile;
            }

            if (lBAC.Text == "")
            {
                lBAC.Text = "No JSON-file set.";
            }
        }

        private void cbBCM_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MemoryFileList.Exists(m => m.Name == cbBCM.SelectedItem.ToString()))
            {
                lBCM.Text = MemoryFileList.First(m => m.Name == cbBCM.SelectedItem.ToString()).JsonFile;
            }

            if (lBCM.Text == "")
            {
                lBCM.Text = "No JSON-file set.";
            }
        }

        private void bRestore_Click(object sender, EventArgs e)
        {
            foreach (var memoryFile in MemoryFileList)
            {
                foreach (var pointer in memoryFile.Pointers)
                {
                    Memory.Write(pointer, BitConverter.GetBytes(memoryFile.OriginalAddress));
                }

                for (int i = 0; i < 100; i++)
                {
                    Memory.Write(memoryFile.NewAddress + i, new byte[]{0x00});
                }
            }

            cbBAC.Items.Clear();
            cbBCM.Items.Clear();
            MemoryFileList = new List<MemoryFile>();
            writeToOutput("Game restored");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }
    }

    public class MemoryFile
    {
        public long OriginalAddress { get; set; }
        public long NewAddress { get; set; }
        public string Name { get; set; }
        public string JsonFile { get; set; }
        public MemoryFileType Type { get; set; }
        public List<long> Pointers { get; set; } 
    }

    public enum MemoryFileType
    {
        BAC = 0,
        BCM = 1
    }
}
