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
                        WriteToOutput("Game process not found!");
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

        private void WriteToOutput(string output)
        {
            Invoke((MethodInvoker)delegate
            {
                lbOutput.Items.Add(DateTime.Now.ToShortTimeString() + " - " + output);
                lbOutput.SelectedIndex = lbOutput.Items.Count - 1;
            });
        }

        private void OnClick_ScanButton(object sender, EventArgs e)
        {
            // Reset combo boxes and file list.
            cbBAC.Items.Clear();
            cbBCM.Items.Clear();
            MemoryFileList = new List<MemoryFile>();

            WriteToOutput("Scanning game for BAC/BCM files...");
            Application.DoEvents();

            BACAddresses = Memory.MemoryScan(Encoding.UTF8.GetBytes("#BAC"));
            BCMAddresses = Memory.MemoryScan(Encoding.UTF8.GetBytes("#BCM"));

            foreach (var bacAddress in BACAddresses)
            {
                Console.WriteLine($@"BAC at: {bacAddress:X}");
            }

            foreach (var bcmAddress in BCMAddresses)
            {
                Console.WriteLine($@"BCM at: {bcmAddress:X}");
            }

            //CHANGED: Checks if Originals exists, and creates it if not. If it does exist, checks for empty folder.
            if (!Directory.Exists("Originals")) Directory.CreateDirectory("Originals");
            if (!Directory.EnumerateFiles("Originals").Any())
            {
                WriteToOutput("No files in Originals folder. Please add the uasset files for the current character to the \"Originals\" folder.");
                return;
            }
            // ---

            foreach (var file in Directory.GetFiles("Originals"))
            {
                if (!file.ToLower().EndsWith(".uasset"))
                {
                    continue; // Skip to next file if it's not a .uasset
                }

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
                            UassetFileName = Path.GetFileName(file),
                            Type = MemoryFileType.BAC
                        });
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue; // BAC was found. Skip to next file.
                }

                foreach (var bcmAddress in BCMAddresses)
                {
                    var foundAddresses = Memory.MemoryScan(fileBytes, bcmAddress, fileBytes.Length, 1);

                    if (foundAddresses.Count != 0)
                    {
                        MemoryFileList.Add(new MemoryFile()
                        {
                            OriginalAddress = bcmAddress,
                            UassetFileName = Path.GetFileName(file),
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
                        UassetFileName = "Unknown" + counter,
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
                        UassetFileName = "Unknown" + counter,
                        Type = MemoryFileType.BCM
                    });
                }
            }

            foreach (var memoryFile in MemoryFileList)
            {
                memoryFile.Pointers = Memory.MemoryScan(BitConverter.GetBytes(memoryFile.OriginalAddress));

                Console.WriteLine("\nFile:\n" +
                                  $"OriginalAddress: {memoryFile.OriginalAddress:X}\n" +
                                  $@"Name: {memoryFile.UassetFileName}");

                foreach (var pointer in memoryFile.Pointers)
                    Console.WriteLine($@"Pointer: {pointer:X}");
            }

            foreach (var memoryFile in MemoryFileList)
            {
                var uassetName = memoryFile.UassetFileName;

                if (uassetName == null) continue;

                if (memoryFile.Type == MemoryFileType.BAC)
                {
                    cbBAC.Items.Add(uassetName);
                }
                else
                {
                    cbBCM.Items.Add(uassetName);
                }
                WriteToOutput($"File: {uassetName} at: {memoryFile.OriginalAddress:X}");
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
            WriteToOutput("Ready to load JSON.");
        }

        private void bSelectBACJson_Click(object sender, EventArgs e)
        {
            SelectJson(sender, e);
        }

        private void bSelectBCMJson_Click(object sender, EventArgs e)
        {
            SelectJson(sender, e);
        }

        private void SelectJson(object sender, EventArgs e)
        {
            #region Show "Open File" dialog

            FileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JSON-file | *.json";
            var result = dialog.ShowDialog();

            if (result != DialogResult.OK)
            {
                return;
            }

            #endregion

            bool isBAC = ((Button)sender).Name.Contains("BAC");

            var cb = isBAC ? cbBAC : cbBCM;

            var fileName = dialog.FileName;

            MemoryFileList.First(m => m.UassetFileName == cb.SelectedItem.ToString()).JsonFilePath = fileName;

            foreach (var memoryFile in MemoryFileList)
            {
                Console.WriteLine($"File:\nOriginalAddress: {memoryFile.OriginalAddress:X}\n" +
                                  $"Name: {memoryFile.UassetFileName}\n" +
                                  $"JSON: {memoryFile.JsonFilePath}");
            }

            var label = isBAC ? lBAC : lBCM;
            label.Text = fileName;

            var path = Path.GetDirectoryName(fileName);
            var shortFileName = Path.GetFileName(fileName);

            // if no Watcher exists for the directory, add one
            if (shortFileName != null && path != null && !fileSystemWatchers.ContainsKey(path))
            {
                fileSystemWatchers.Add(path, new FileSystemWatcher(path));
            }

            foreach (var fileSystemWatcher in fileSystemWatchers)
            {
                fileSystemWatcher.Value.EnableRaisingEvents = true;
                fileSystemWatcher.Value.Changed -= FileModified;
                fileSystemWatcher.Value.Changed += FileModified;
            }

            UpdateFileInMemory(fileName);
            WriteToOutput("Loaded: " + shortFileName);
        }

        private void FileModified(object sender, FileSystemEventArgs e)
        {
            // Check if we care about the modified file...
            // TODO
            if (!MemoryFileList.Exists(m => m.JsonFilePath == e.FullPath)) return;

            Console.WriteLine("File modified: " + e.FullPath);
            Console.WriteLine("Updating file in memory");
            WriteToOutput("File modified: " + e.Name);
            WaitReady(e.FullPath);
            UpdateFileInMemory(e.FullPath);
        }

        // Wait until the file is ready to be accessed
        public static void WaitReady(string fileName)
        {
            while (true)
            {
                try
                {
                    using (Stream stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        if (stream != Stream.Null)
                        {
                            Trace.WriteLine($"Output file {fileName} ready.");
                            break;
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {
                    Trace.WriteLine($"Output file {fileName} not found: ({ex.Message})");
                }
                catch (IOException ex)
                {
                   Trace.WriteLine($"Output file {fileName} not yet ready ({ex.Message})");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.WriteLine($"Output file {fileName} not yet ready ({ex.Message})");
                }
                Thread.Sleep(500);
            }
        }

        private void UpdateFileInMemory(string fileName)
        {
            if (!MemoryFileList.Exists(m => m.JsonFilePath == fileName)) return;

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

            var memFile = MemoryFileList.First(m => m.JsonFilePath == fileName);

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
            WriteToOutput("Wrote: " + Path.GetFileName(fileName) + " to " + memFile.UassetFileName + " - " +
                          memFile.NewAddress.ToString("X"));
        }

        private void cbBAC_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MemoryFileList.Exists(m => m.UassetFileName == cbBAC.SelectedItem.ToString()))
            {
                lBAC.Text = MemoryFileList.First(m => m.UassetFileName == cbBAC.SelectedItem.ToString()).JsonFilePath;
            }

            if (lBAC.Text == "")
            {
                lBAC.Text = "No JSON-file set.";
            }
        }

        private void cbBCM_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MemoryFileList.Exists(m => m.UassetFileName == cbBCM.SelectedItem.ToString()))
            {
                lBCM.Text = MemoryFileList.First(m => m.UassetFileName == cbBCM.SelectedItem.ToString()).JsonFilePath;
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
            WriteToOutput("Game restored");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }
    }

    public class MemoryFile
    {
        public long OriginalAddress { get; set; }
        public long NewAddress { get; set; }
        public string UassetFileName { get; set; }
        public string JsonFilePath { get; set; }
        public MemoryFileType Type { get; set; }
        public List<long> Pointers { get; set; } 
    }

    public enum MemoryFileType
    {
        BAC = 0,
        BCM = 1
    }
}
