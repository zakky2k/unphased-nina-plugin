using CommunityToolkit.Mvvm.Input;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// The plugin's main page: a dockable panel showing the night planner
    /// chart. Everything lives inside this panel, so the plugin never touches
    /// other N.I.N.A. UI elements.
    /// </summary>
    [Export(typeof(NINA.Equipment.Interfaces.ViewModel.IDockableVM))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PlannerDockableVM : DockableVM {
        public const string ContentIdKey = "Zakfo.Unphased.Planner";

        private readonly PluginSettings settings;
        private readonly ITelescopeMediator telescope;

        private NightPlannerVM planner;
        private IRelayCommand prev, next, tonight, scrubBack, scrubForward, scrubClear, zoomOut, zoomIn;

        [ImportingConstructor]
        public PlannerDockableVM(IProfileService profileService, ITelescopeMediator telescope)
            : base(profileService) {
            // For(), not an import: N.I.N.A. composes the manifest and the parts of a plugin
            // in two different containers, so an imported part would be a second settings
            // object that the options page never writes to.
            this.settings = PluginSettings.For(profileService);
            this.telescope = telescope;
            Title = "Unphased Night Planner";
            CanClose = true;
        }

        /// <summary>
        /// Lazily built because the effective site depends on the active
        /// profile, which is not ready at MEF construction time.
        /// </summary>
        public NightPlannerVM Planner => planner ??= new NightPlannerVM(settings);

        public IRelayCommand PrevNightCommand => prev ??= new RelayCommand(() => Planner.PrevNight(), () => true);
        public IRelayCommand NextNightCommand => next ??= new RelayCommand(() => Planner.NextNight(), () => true);
        public IRelayCommand TonightCommand => tonight ??= new RelayCommand(() => Planner.Tonight(), () => true);

        // There used to be a RefreshCommand beside these. It went away with the button: the
        // panel works out a window when it is first shown and widens it on its own when the
        // calendar moves towards an edge, so a manual recompute is a way to press a button
        // that does nothing visible rather than a way to get unstuck.

        /// <summary>
        /// Hour-by-hour scrubbing of the chart. Dragging, the wheel and the arrow keys
        /// work on the chart itself; these buttons are the same control for people who
        /// would rather click, and the only way in when the panel is too small to
        /// pointer at.
        /// </summary>
        public IRelayCommand ScrubBackCommand => scrubBack ??= new RelayCommand(() => Planner.StepScrub(-1), () => true);
        public IRelayCommand ScrubForwardCommand => scrubForward ??= new RelayCommand(() => Planner.StepScrub(1), () => true);
        public IRelayCommand ScrubClearCommand => scrubClear ??= new RelayCommand(() => Planner.ClearScrub(), () => true);

        /// <summary>
        /// Chart frame length, one night (noon to noon, like the desktop planner) up to
        /// a week. The panel width no longer decides it: on a wide monitor the chart
        /// showed two nights without being asked, and one night is the comparison people
        /// actually want first.
        ///
        /// Both commands can always execute, and the buttons' IsEnabled binds straight to
        /// the planner. A command's CanExecute is not the place for this limit: WPF ANDs it
        /// into ButtonBase.IsEnabled, and inside a dockable pane the requery that refreshes
        /// it is not reliable, so the button stayed grey at one night no matter what the
        /// binding said. Zooming past an end is a no-op in the planner instead.
        /// </summary>
        public IRelayCommand ZoomOutCommand => zoomOut ??= new RelayCommand(
            () => { Planner.ZoomOut(); CommandManager.InvalidateRequerySuggested(); }, () => true);

        public IRelayCommand ZoomInCommand => zoomIn ??= new RelayCommand(
            () => { Planner.ZoomIn(); CommandManager.InvalidateRequerySuggested(); }, () => true);

        /// <summary>
        /// The panel's three palette buttons. They are exclusive because there is one
        /// setting behind them, not three switches - picking one sets the scheme, and the
        /// scheme label next to them says which one is on. Night changes what the plugin
        /// paints (the chart through PlannerPalette, this panel's chrome through brushes
        /// this panel puts in its own resources); Enhanced changes the chart only and
        /// leaves the panel looking like N.I.N.A. Either way N.I.N.A.'s theme is never
        /// touched, so the other tabs keep the user's colours. The commands come from the
        /// settings object, because the options page offers the same three and both have to
        /// mean the same thing.
        /// </summary>
        public IRelayCommand DefaultSchemeCommand => settings.DefaultSchemeCommand;

        public IRelayCommand NightSchemeCommand => settings.NightSchemeCommand;

        public IRelayCommand EnhancedSchemeCommand => settings.EnhancedSchemeCommand;
    }
}

