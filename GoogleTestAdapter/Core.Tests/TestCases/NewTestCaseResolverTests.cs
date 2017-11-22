﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using GoogleTestAdapter.DiaResolver;
using GoogleTestAdapter.Model;
using GoogleTestAdapter.Settings;
using GoogleTestAdapter.Tests.Common;
using GoogleTestAdapter.Tests.Common.Assertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static GoogleTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace GoogleTestAdapter.TestCases
{

    [TestClass]
    public class NewTestCaseResolverTests : TestsBase
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_DiscoveryTimeoutIsExceeded_DiscoveryIsCanceledAndCancelationIsLogged()
        {
            MockOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(1);
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);

            var reportedTestCases = new List<TestCase>();
            var stopWatch = Stopwatch.StartNew();
            var factory = new TestCaseFactory(TestResources.TenSecondsWaiter, MockLogger.Object, TestEnvironment.Options, null);
            var returnedTestCases = factory.CreateTestCases(testCase => reportedTestCases.Add(testCase));
            stopWatch.Stop();

            reportedTestCases.Should().BeEmpty();
            returnedTestCases.Should().BeEmpty();
            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(1));
            stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            MockLogger.Verify(o => o.LogError(It.Is<string>(s => s.Contains(TestResources.TenSecondsWaiter))), Times.Once);
            MockLogger.Verify(o => o.DebugError(It.Is<string>(s => s.Contains(Path.GetFileName(TestResources.TenSecondsWaiter)))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateTestCases_OldExeWithAdditionalPdb_TestCasesAreFound()
        {
            MockOptions.Setup(o => o.UseNewTestExecutionFramework).Returns(false);
            CheckIfSourceLocationsAreFound();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_NewExeWithAdditionalPdb_TestCasesAreFound()
        {
            MockOptions.Setup(o => o.UseNewTestExecutionFramework).Returns(false);
            CheckIfSourceLocationsAreFound();
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private void CheckIfSourceLocationsAreFound()
        { 
            string executable = TestResources.LoadTests_ReleaseX86;

            executable.AsFileInfo().Should().Exist();
            string pdb = Path.ChangeExtension(executable, ".pdb");
            pdb.AsFileInfo().Should().Exist();
            string renamedPdb = $"{pdb}.bak";
            renamedPdb.AsFileInfo().Should().NotExist();

            try
            {
                File.Move(pdb, renamedPdb);
                pdb.AsFileInfo().Should().NotExist();

                MockOptions.Setup(o => o.AdditionalPdbs).Returns("$(ExecutableDir)\\*.pdb.bak");
                var patterns = MockOptions.Object.GetAdditionalPdbs(executable);
                var resolver = new TestCaseResolver(new DefaultDiaResolverFactory(), MockLogger.Object);
                var testCases = resolver.ResolveAllTestCases(executable, new HashSet<string>(), "*", "", patterns);
                //var patterns = new[] { "foo" };
                //var resolver = new NewTestCaseResolver(executable, "", patterns, new DefaultDiaResolverFactory(), true, MockLogger.Object);
                //resolver.FindTestCaseLocation(new[] { "bar" }.ToList());
            }
            finally
            {
                File.Move(renamedPdb, pdb);
                pdb.AsFileInfo().Should().Exist();
            }
        }

        private bool HasSourceLocation(TestCase testCase)
        {
            return !string.IsNullOrEmpty(testCase.CodeFilePath) && testCase.LineNumber != 0;
        }
    }

}