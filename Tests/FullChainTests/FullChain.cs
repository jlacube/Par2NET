using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Par2NET.Interfaces;
using Par2NET;
using CommandLine;
using System.IO;

namespace FullChainTests
{
    /// <summary>
    /// Summary description for FullChainCreate
    /// </summary>
    [TestClass]
    public class FullChainSeparateDomains : MarshalByRefObject
    {
        public FullChainSeparateDomains()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        #region Create tests

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm", "1FileNoMT")]
        public void FullChainCreate1FileNoMT()
        {
            string[] args = new string[] {
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "1FileNoMT", "EntLib50.chm"),
                "/outputPath:" + Path.Combine(TestContext.TestDeploymentDir, "1FileNoMT"),
                "/action:ParCreate",
                "/rbc:10",
                "/mtcpu-",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm", "3FilesNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50_copy.chm", "3FilesNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50_copy2.chm", "3FilesNoMT")]
        public void FullChainCreate3FilesNoMT()
        {
            string[] args = new string[] {
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesNoMT", "EntLib50.chm"),
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesNoMT", "EntLib50_copy.chm"),
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesNoMT", "EntLib50_copy2.chm"),
                "/outputPath:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesNoMT"),
                "/action:ParCreate",
                "/rbc:30",
                "/mtcpu-",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm", "1FileMT")]
        public void FullChainCreate1FileMT()
        {
            string[] args = new string[] {
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "1FileMT", "EntLib50.chm"),
                "/outputPath:" + Path.Combine(TestContext.TestDeploymentDir, "1FileMT"),
                "/action:ParCreate",
                "/rbc:10",
                "/mtcpu+",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm", "3FilesMT")]
        [DeploymentItem("Tests\\Files\\EntLib50_copy.chm", "3FilesMT")]
        [DeploymentItem("Tests\\Files\\EntLib50_copy2.chm", "3FilesMT")]
        public void FullChainCreate3FilesMT()
        {
            string[] args = new string[] {
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesMT", "EntLib50.chm"),
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesMT", "EntLib50_copy.chm"),
                "/if:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesMT", "EntLib50_copy2.chm"),
                "/outputPath:" + Path.Combine(TestContext.TestDeploymentDir, "3FilesMT"),
                "/action:ParCreate",
                "/rbc:30",
                "/mtcpu+",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        #endregion

        #region Verify tests

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.par2", "VerifyOK_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm", "VerifyOK_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol0+1.PAR2", "VerifyOK_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol1+2.PAR2", "VerifyOK_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol3+3.PAR2", "VerifyOK_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol6+4.PAR2", "VerifyOK_1FileNoMT")]
        public void FullChainVerifyOK_1FileNoMT()
        {
            string[] args = new string[] {
                "/rf:" + Path.Combine(TestContext.TestDeploymentDir, "VerifyOK_1FileNoMT", "EntLib50.chm.par2"),
                "/action:ParVerify",
                "/mtcpu+",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        [TestMethod]
        [DeploymentItem("x64\\Release\\FastGaloisFieldsNative.dll")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.par2", "VerifyKO_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50_bad.chm", "VerifyKO_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol0+1.PAR2", "VerifyKO_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol1+2.PAR2", "VerifyKO_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol3+3.PAR2", "VerifyKO_1FileNoMT")]
        [DeploymentItem("Tests\\Files\\EntLib50.chm.vol6+4.PAR2", "VerifyKO_1FileNoMT")]
        public void FullChainVerifyKO_1FileNoMT()
        {
            File.Move(
                Path.Combine(TestContext.TestDeploymentDir, "VerifyKO_1FileNoMT", "EntLib50_bad.chm"),
                Path.Combine(TestContext.TestDeploymentDir, "VerifyKO_1FileNoMT", "EntLib50.chm")
                );

            string[] args = new string[] {
                "/rf:" + Path.Combine(TestContext.TestDeploymentDir, "VerifyKO_1FileNoMT", "EntLib50.chm.par2"),
                "/action:ParVerify",
                "/mtcpu-",
                "/mtio-"
            };

            FullChainRoot(args, false);
        }

        #endregion

        private void FullChainRoot(string[] args, bool needRepair)
        {
            AppDomain isolatedDomain = AppDomain.CreateDomain("isolatedDomain");

            FullChainSeparateDomains fc = (FullChainSeparateDomains)isolatedDomain.CreateInstanceFromAndUnwrap("FullChainTests.dll", "FullChainTests.FullChainSeparateDomains");

            fc.FullChainRoot_InnerDomain(args, needRepair);

            AppDomain.Unload(isolatedDomain);
        }

        private void FullChainRoot_InnerDomain(string[] args, bool needRepair)
        {
            Par2LibraryArguments par2args = new Par2LibraryArguments();

            if (!Parser.ParseArgumentsWithUsage(args, par2args))
                return;

            switch (par2args.action)
            {
                case ParAction.ParCreate:
                    if (par2args.inputFiles.Length == 0 || (par2args.redundancy == -1 && par2args.recoveryblockcount == -1))
                    {
                        Parser.ArgumentsUsage(par2args.GetType());
                        return;
                    }
                    break;
                case ParAction.ParVerify:
                case ParAction.ParRepair:
                    if (par2args.recoveryFiles.Length == 0)
                    {
                        Parser.ArgumentsUsage(par2args.GetType());
                        return;
                    }
                    break;
            }

            Par2Library library = new Par2Library(par2args.multithreadCPU, par2args.multithreadIO);

            List<string> inputFiles = new List<string>(par2args.inputFiles);
            List<string> recoveryFiles = new List<string>(par2args.recoveryFiles);

            if (string.IsNullOrEmpty(par2args.targetPath))
            {
                if (par2args.action == ParAction.ParCreate)
                    par2args.targetPath = Path.GetDirectoryName(par2args.inputFiles[0]);
                else
                    par2args.targetPath = Path.GetDirectoryName(par2args.recoveryFiles[0]);
            }

            ParResult result = library.Process(par2args);

            Assert.AreEqual<ParResult>(ParResult.Success, result);
        }
    }
}
