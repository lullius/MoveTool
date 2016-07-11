using System;
using System.IO;
using MoveLib;
using MoveLib.BAC;
using MoveLib.BCM;

namespace MoveTool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("\n");
                Console.WriteLine("BAC/BCM to JSON: MoveTool.exe InFile.uasset OutFile.json");
                Console.WriteLine("JSON to BAC/BCM: MoveTool.exe InFile.json OutFile.uasset");
                Console.WriteLine("\n");
                Console.WriteLine("You can also drag and drop files onto this tool and it will\nautomatically create the JSON or BAC/BCM file with the same\nname in the same directory as the original file.");
                Console.WriteLine("\n");
                Console.WriteLine(("Back up your files, this tool will overwrite any file with the\nsame name as the output file!").ToUpper());
            }

            if (args.Length == 1)
            {
                Console.WriteLine(Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]));

                if (File.Exists(args[0]))
                {
                    if (args[0].ToLower().EndsWith("uasset"))
                    {
                        var type = FileTypeDecider.Decide(args[0]);

                        if (type == FileType.BAC)
                        {
                            Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                            try
                            {
                                BAC.BacToJson(args[0],
                                    Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) +
                                    ".json");
                                Console.WriteLine("Done writing file: " + Path.GetDirectoryName(args[0]) + @"\" +
                                                  Path.GetFileNameWithoutExtension(args[0]) + ".json");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                Console.Read();
                            }
                        }
                        else if (type == FileType.BCM)
                        {
                            try
                            {
                                Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");
                                BCM.BcmToJson(args[0],
                                    Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) +
                                    ".json");
                                Console.WriteLine("Done writing file: " + Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) + ".json");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Something went wrong: " + ex.Message + " - " + ex.Data);
                                Console.Read();
                            }

                        }
                        else if (type == FileType.Unknown)
                        {
                            Console.WriteLine("Unsupported format.");
                            Console.Read();
                        }
                    }

                    if (args[0].ToLower().EndsWith("json"))
                    {
                        Console.WriteLine("File is json.");

                        var success = BAC.JsonToBac(args[0],
                            Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) + ".uasset");

                        if (!success)
                        {
                            success = BCM.JsonToBcm(args[0],
                            Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) + ".uasset");
                        }

                        if (!success)
                        {
                            Console.WriteLine("Something went wrong while parsing json.");
                            Console.Read();
                        }
                        else
                        {
                            Console.WriteLine("Done writing file: " + Path.GetDirectoryName(args[0]) + @"\" + Path.GetFileNameWithoutExtension(args[0]) + ".uasset");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("File does not exist: " + args[0]);
                    Console.Read();
                }
            }

            if (args.Length == 2)
            {
                string inFile = args[0];
                string outFile = args[1];

                if (inFile.ToLower().EndsWith("uasset"))
                {
                    if (!outFile.ToLower().EndsWith("json"))
                    {
                        outFile += ".json";
                    }

                    var type = FileTypeDecider.Decide(inFile);

                    if (type == FileType.BAC)
                    {
                        Console.WriteLine("BAC file detected. Trying to do BAC to JSON.");
                        BAC.BacToJson(inFile, outFile);
                        Console.WriteLine("Done writing file: " + outFile);
                    }
                    else if (type == FileType.BCM)
                    {
                        Console.WriteLine("BCM file detected. Trying to do BCM to JSON.");
                        BCM.BcmToJson(inFile,outFile);
                        Console.WriteLine("Done writing file: " + outFile);
                    }
                    else if (type == FileType.Unknown)
                    {
                        Console.WriteLine("Unsupported format.");
                        Console.Read();
                    }
                }

                if (inFile.ToLower().EndsWith("json"))
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
                        Console.WriteLine("Something went wrong while parsing json.");
                        Console.Read();
                    }
                    else
                    {
                        Console.WriteLine("Done writing file: " + outFile);
                    }
                }
            }

            if (args.Length > 2)
            {
                Console.WriteLine("MoveTool can not understand more than 2 arguments. If you have spaces\n" +  @"in the paths, try adding "" around them.");
            }
        }
    }
}
