using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Threading;
using System.Windows.Threading;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// The planner marshals settings changes onto the interface thread, and the shape of
    /// that marshalling is what this covers. A dispatcher call handed over with the wrong
    /// argument list crashes N.I.N.A. with a bare TargetParameterCountException buried in
    /// ExceptionWrapper, with no plugin frame in the stack at all, so it has to be
    /// reproduced here the way the application does it: a settings change arriving from a
    /// worker thread while the interface thread is running its message loop.
    /// </summary>
    [NonParallelizable]
    public class PlannerThreadMarshallingTests {
        private static IProfileService MockProfiles() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            return profiles.Object;
        }

        [Test, Timeout(20000)]
        public void ASettingsChangeFromAnotherThreadReachesThePlannerOnTheInterfaceThread() {
            Exception uiFailure = null, workerFailure = null;
            bool onInterfaceThread = false;
            bool workerFinished = false;

            var uiThread = new Thread(() => {
                // No Application here: the test process may only ever hold one, and
                // CompositionTests owns it. Without one the view model falls back to this
                // thread's own dispatcher, which is what Dispatcher.Run turns into the real
                // message loop - the pumping that N.I.N.A. provides. A hand-pushed
                // DispatcherFrame is no substitute: it either returns before the worker has
                // queued anything or never returns at all, and both read as a hung run.
                var ui = Dispatcher.CurrentDispatcher;
                PluginSettings settings = null;
                try {
                    settings = new PluginSettings(MockProfiles());
                    settings.Initialize();
                    var planner = new NightPlannerVM(settings);
                    planner.PropertyChanged += (_, e) => {
                        if (e.PropertyName == nameof(NightPlannerVM.SunColor))
                            onInterfaceThread = ReferenceEquals(Dispatcher.CurrentDispatcher, ui);
                    };

                    var worker = new Thread(() => {
                        try { settings.SunColor = "#FFD700"; } catch (Exception e) { workerFailure = e; }
                        workerFinished = true;
                        Stop(ui);
                    });
                    worker.Start();

                    // Watchdog: if the change never lands, close the loop anyway so the
                    // assertions below report the miss instead of the run hanging.
                    var watchdog = new Thread(() => {
                        Thread.Sleep(5000);
                        Stop(ui);
                    });
                    watchdog.Start();

                    Dispatcher.Run();
                    worker.Join();
                    watchdog.Join();
                } catch (Exception e) {
                    uiFailure = e;
                } finally {
                    if (settings != null) {
                        settings.ResetColors();
                        settings.Dispose();
                    }
                }
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            uiThread.Join();

            Assert.Multiple(() => {
                Assert.That(uiFailure, Is.Null,
                    $"the interface thread took a hit: {uiFailure?.GetType().Name}: {uiFailure?.Message}");
                Assert.That(workerFailure, Is.Null,
                    $"the thread that changed the setting saw: {workerFailure?.GetType().Name}: {workerFailure?.Message}");
                Assert.That(workerFinished, Is.True, "the settings change never came back");
                Assert.That(onInterfaceThread, Is.True,
                    "the announcement has to arrive on the interface thread, or the chart is touched from the wrong one");
            });
        }

        /// <summary>End the dispatcher loop of the thread under test, from wherever we are.</summary>
        private static void Stop(Dispatcher ui) {
            try {
                ui.BeginInvoke(DispatcherPriority.Normal, new Action(ui.InvokeShutdown));
            } catch (Exception) {
                // Already shut down.
            }
        }
    }
}
