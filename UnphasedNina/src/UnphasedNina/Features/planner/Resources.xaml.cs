// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.ComponentModel.Composition;
using System.Windows;

namespace Zakfo.Unphased.Features.planner {
    [Export(typeof(ResourceDictionary))]
    public partial class Resources : ResourceDictionary {
        public Resources() {
            InitializeComponent();
            // Pane template selector resolves "<VM type full name>_Dockable".
            this[typeof(PlannerDockableVM).FullName + "_Dockable"] = this["PlannerDockableTemplate"];
            Remove("PlannerDockableTemplate");
        }
    }
}
