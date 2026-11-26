// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using NINA.Core.Utility;
using System.IO;
using System.Windows;

namespace Zakfo.Unphased.Tests {
    [SetUpFixture]
    public class TestEnvironment {
        private string previousRoot;
        public static string Root { get; private set; }
        [OneTimeSetUp]
        public void Start() {
            previousRoot = CoreUtil.APPLICATIONTEMPPATH;
            Root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "runtime", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            CoreUtil.APPLICATIONTEMPPATH = Root;
        }
        [OneTimeTearDown]
        public void Stop() {
            CoreUtil.APPLICATIONTEMPPATH = previousRoot;
            // NINA may retain open log handles until the test process exits.
        }
    }
}
