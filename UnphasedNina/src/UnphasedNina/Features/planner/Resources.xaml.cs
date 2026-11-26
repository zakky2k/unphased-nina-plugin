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
