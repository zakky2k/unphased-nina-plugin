// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zakfo.Unphased.Tests {
    /// <summary>
    /// Every source file in this repository opens with the MPL-2.0 notice.
    /// <para>
    /// The licence asks for it in terms - "This Source Code Form is subject to the terms of the
    /// Mozilla Public License, v. 2.0" is Exhibit A of the licence text itself - and the copy of the
    /// licence the plugin ships says the plugin's own source is MPL-2.0, so the notice belongs in the
    /// files and not only in LICENSE.txt. N.I.N.A. does the same on every one of its own sources, and
    /// the plugin is built to sit inside that codebase, so the wording here is N.I.N.A.'s wording,
    /// two lines, nothing invented.
    /// </para>
    /// <para>
    /// This is one of those rules that is invisible when it is followed and obvious when it is not,
    /// and a new file added next week would silently break it - hence a test that walks the tree
    /// rather than a checklist in the handoff. <c>build/vendor/</c> is deliberately outside the walk:
    /// those files are someone else's code, kept unmodified, carrying their own licence (see
    /// <c>build/vendor/PROVENANCE.md</c>), and putting our notice on them would misstate who wrote them.
    /// </para>
    /// </summary>
    public class LicenceHeaderTests {
        private const string Notice =
            "This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.";

        /// <summary>Directories whose contents are not source this repository stands behind.</summary>
        private static readonly Regex NotSource =
            new Regex(@"[\\/](obj|bin|artifacts|TestResults|\.vs|vendor)[\\/]", RegexOptions.Compiled);

        [Test]
        public void EverySourceFileOpensWithTheLicenceNotice() {
            var root = RepositoryRoot();
            if (root == null) {
                Assert.Ignore(
                    "the source tree is not beside this test binary, so there are no files to check - "
                    + "run the suite from a checkout for this to mean anything");
            }

            var files = SourceFiles(root).ToList();

            // A walk that quietly finds nothing would pass, which is the failure mode worth pinning:
            // thirty-eight files as of round 9, and the count only ever grows from here.
            Assert.That(files, Has.Count.GreaterThanOrEqualTo(38),
                $"the walk under {root} found too few sources to be the real tree");
            Assert.That(files.Any(f => NotSource.IsMatch(f)), Is.False,
                "build/vendor is unmodified third-party code with its own licence, so it stays out of the walk");

            var missing = files
                .Where(f => !File.ReadAllText(f).Contains(Notice))
                .Select(f => f.Substring(root.Length + 1))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.That(missing, Is.Empty, "files without the MPL-2.0 notice in them");
        }

        private static IEnumerable<string> SourceFiles(string root) =>
            Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Where(f => !NotSource.IsMatch(f));

        /// <summary>
        /// Find the checkout this test was built from by walking up from the binary - the pair
        /// <c>UnphasedNina.slnx</c> and <c>src/UnphasedCore/NightCalculator.cs</c> is this
        /// repository's shape. Null when the suite is run from a bare binary with no sources, which
        /// is a real deployment of the test DLL and should be reported, not failed.
        /// </summary>
        private static string RepositoryRoot() {
            foreach (var start in new[] {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            }) {
                var dir = new DirectoryInfo(start);
                for (int hop = 0; dir != null && hop < 10; hop++, dir = dir.Parent) {
                    if (File.Exists(Path.Combine(dir.FullName, "UnphasedNina.slnx"))
                        && File.Exists(Path.Combine(dir.FullName, "src", "UnphasedCore", "NightCalculator.cs"))) {
                        return dir.FullName;
                    }
                }
            }
            return null;
        }
    }
}
