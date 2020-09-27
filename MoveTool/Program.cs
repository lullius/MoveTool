using System;
using System.IO;
using System.Reflection;
using MoveLib;
using MoveLib.BAC;
using MoveLib.BCM;

namespace MoveTool
{
    public class Program
    {
        private static readonly char Separator = Path.DirectorySeparatorChar;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            Start(args);
        }

        private static void Start(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                {
                    Console.WriteLine("\nBAC/BCM/BCH to JSON: MoveTool.exe InFile.uasset OutFile.json"      + 
                                      "\nJSON to BAC/BCM/BCH: MoveTool.exe InFile.json OutFile.uasset"      + 
                                      "\n\nYou can also drag and drop files onto this tool and it will"     + 
                                      "\nautomatically create the JSON or BAC/BCM/BCH file with the "       +
                                      "\nsame name in the same directory as the original file."             + 
                                      ("\n\nBack up your files, this tool will overwrite any file with the" + 
                                      "\nsame name as the output file!").ToUpper()                          );

                    break;
                }

                case 1:
                {
                    var path = args[0];
                    var directory = Path.GetDirectoryName(path) + Separator;
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                    
                    Console.WriteLine(directory + fileNameWithoutExtension);

                    // Check if file exists
                    if (!File.Exists(path))
                    {
                        Console.WriteLine("File does not exist: " + path);
                        break;
                    }

                    #region Handle .UASSET files

                    if (path.ToLower().EndsWith("uasset"))
                    {
                        var type = FileTypeDecider.Decide(path);

                        switch (type)
                        {
                            case FileType.BAC:
                                Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                                try
                                {
                                    BAC.BacToJson(path, directory + fileNameWithoutExtension + ".json");

                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.BCM:
                                try
                                {
                                    Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");

                                    BCM.BcmToJson(path,
                                        directory + fileNameWithoutExtension + ".json");

                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.BCH:
                                try
                                {
                                    Console.WriteLine("BCH file detected. Trying to do BCH to JSON.");
                                    BCH.BchToJson(path, 
                                        directory + fileNameWithoutExtension + ".json");
                                    Console.WriteLine("Done writing file: " + 
                                                      directory + fileNameWithoutExtension + ".json");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                }

                                break;

                            case FileType.Unknown:
                            default:
                                Console.WriteLine("Unsupported format.");
                                break;
                        }
                    }

                    #endregion

                    #region Handle .JSON files

                    if (args[0].ToLower().EndsWith("json"))
                    {
                        Console.WriteLine("File is json.");

                        var success = BAC.JsonToBac(
                            args[0],
                            Path.GetDirectoryName(args[0]) + Separator +
                            Path.GetFileNameWithoutExtension(args[0]) + ".uasset");

                        if (!success)
                        {
                            success = BCM.JsonToBcm(args[0],
                                Path.GetDirectoryName(args[0]) + Separator +
                                Path.GetFileNameWithoutExtension(args[0]) + ".uasset");
                        }

                        if (!success)
                        {
                            success = BCH.JsonToBch(args[0],
                                Path.GetDirectoryName(args[0]) + Separator +
                                Path.GetFileNameWithoutExtension(args[0]) + ".uasset");
                        }

                        if (!success)
                        {
                            Console.WriteLine("Something went wrong while parsing json.");
                        }
                        else
                        {
                            Console.WriteLine("Done writing file: " + Path.GetDirectoryName(args[0]) +
                                              Separator + Path.GetFileNameWithoutExtension(args[0]) +
                                              ".uasset");
                        }
                    }

                    #endregion

                    break;
                }

                case 2:
                {
                    var inFile = args[0];
                    var outFile = args[1];

                    if (inFile.ToLower().EndsWith("uasset"))
                    {
                        if (!outFile.ToLower().EndsWith("json"))
                        {
                            outFile += ".json";
                        }

                        var type = FileTypeDecider.Decide(inFile);

                        switch (type)
                        {
                            case FileType.BAC:
                                Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                                BAC.BacToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.BCM:
                                Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");
                                BCM.BcmToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.BCH:
                                Console.WriteLine("BCH file detected. Trying to do BCH to JSON.");
                                BCH.BchToJson(inFile, outFile);
                                Console.WriteLine("Done writing file: " + outFile);
                                break;

                            case FileType.Unknown:
                            default:
                                Console.WriteLine("Unsupported format.");
                                break;
                        }
                    }
                    else if (inFile.ToLower().EndsWith("json"))
                    {
                        if (!outFile.ToLower().EndsWith("uasset"))
                        {
                            outFile += ".uasset";
                        }

                        Console.WriteLine("File is json.");

                        var success = BAC.JsonToBac(inFile, outFile);

                        if (!success)
                        {
                            success = BCM.JsonToBcm(inFile, outFile);
                        }

                        if (!success)
                        {
                            success = BCH.JsonToBch(inFile, outFile);
                        }

                        if (!success)
                            Console.WriteLine("Something went wrong while parsing json.");
                        else
                            Console.WriteLine("Done writing file: " + outFile);
                    }

                    break;
                }

                default:
                {
                    Console.WriteLine("MoveTool can not understand more than 2 arguments. \n" +
                                      @"If the paths contain spaces, try wrapping the paths in double quotes ("").");
                    break;
                }
            }

            Pause();
        }

        private static void Pause()
        {
            Console.Write("\n\nPress any key to continue...");
            Console.ReadKey(true);
            Console.WriteLine("\n");
        }

        // Part of enabling a single .exe file
        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(args.Name);

            var path = assemblyName.Name + ".dll";
            if (assemblyName.CultureInfo.Equals(System.Globalization.CultureInfo.InvariantCulture) == false)
            {
                path = $@"{assemblyName.CultureInfo}\{path}";
            }

            using (var stream = executingAssembly.GetManifestResourceStream(path))
            {
                if (stream == null)
                    return null;

                var assemblyRawBytes = new byte[stream.Length];
                stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                return Assembly.Load(assemblyRawBytes);
            }
        }
    }
}
