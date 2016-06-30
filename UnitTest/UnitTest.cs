using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoveLib.BAC;
using MoveLib.BCM;


namespace UnitTest
{
    [TestClass]
    public class UnitTest
    {
        /*
        To use these tests you will need to put some, or all, the 
        BAC/BCM (uasset) files in the correct (UnitTest/Bin/Debug/Originals/...)
        */

        [TestMethod]
        public void TestBAC()
        {
            /*
            Not correct length: BAC_B59
            Bytes does not match up: BAC_CMN Position: 21C Original was: 0 Created was: 1C
            Not correct length: BAC_EFE
            */

            List<string> exceptions = new List<string>();

            foreach (var file in Directory.GetFiles(@"Originals\BAC"))
            {
                var originalBytes = File.ReadAllBytes(file);
                var bac = BAC.FromUassetFile(file);
                BAC.ToUassetFile(bac, @"Originals\BAC\testfile.uasset");
                var createdBytes = File.ReadAllBytes(@"Originals\BAC\testfile.uasset");
                bool isCorrectLength = true;

                if (originalBytes.Length != createdBytes.Length)
                {
                    exceptions.Add("Not correct length: " + Path.GetFileNameWithoutExtension(file));
                    isCorrectLength = false;
                }

                if (isCorrectLength)
                {
                    for (int i = 0; i < originalBytes.Length; i++)
                    {
                        if (originalBytes[i] != createdBytes[i])
                        {
                            exceptions.Add("Bytes does not match up: " + Path.GetFileNameWithoutExtension(file) +
                                           " Position: " + i.ToString("X") + " Original was: " +
                                           originalBytes[i].ToString("X") + " Created was: " +
                                           createdBytes[i].ToString("X"));
                        }
                    }
                }
            }

            File.Delete(@"Originals\BAC\testfile.uasset");

            foreach (var exception in exceptions)
            {
                Debug.WriteLine(exception);
            }

            if (exceptions.Count > 0)
            {
                var output = "";
                foreach (var exception in exceptions)
                {
                    output += exception + "\n";
                }

                Assert.Fail("Test failed, found exceptions:\n" + output);
            }
        }

        [TestMethod]
        public void TestBCMeff()
        {
            foreach (var file in Directory.GetFiles(@"Originals\BACeff"))
            {
                var originalBytes = File.ReadAllBytes(file);
                var bac = BACEff.FromUassetFile(file);
                BACEff.ToUassetFile(bac, @"Originals\BACeff\testfile.uasset");
                var createdBytes = File.ReadAllBytes(@"Originals\BACeff\testfile.uasset");

                Assert.AreEqual(originalBytes.Length, createdBytes.Length);

                for (int i = 0; i < originalBytes.Length; i++)
                {
                    Assert.AreEqual(originalBytes[i], createdBytes[i]);
                }
            }

            File.Delete(@"Originals\BACeff\testfile.uasset");
        }

        [TestMethod]
        public void TestBCM()
        {
            foreach (var file in Directory.GetFiles(@"Originals\BCM"))
            {
                var originalBytes = File.ReadAllBytes(file);
                var bcm = BCM.FromUassetFile(file);
                BCM.ToUassetFile(bcm, @"Originals\BCM\testfile.uasset");
                var createdBytes = File.ReadAllBytes(@"Originals\BCM\testfile.uasset");

                Assert.AreEqual(originalBytes.Length, createdBytes.Length);

                for (int i = 0; i < originalBytes.Length; i++)
                {
                    Assert.AreEqual(originalBytes[i], createdBytes[i]);
                }
            }

            File.Delete(@"Originals\BCM\testfile.uasset");
        }
    }
}
