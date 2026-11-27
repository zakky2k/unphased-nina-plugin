// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
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

        [TestCase(false), TestCase(true)]
        public void ASettingsChangeFromAnotherThreadReachesThePlannerOnTheInterfaceThread(bool changeSite) {
            Exception uiFailure = null, workerFailure = null;
            bool onInterfaceThread = false;
            bool workerFinished = false;

            var uiThread = new Thread(() => {
                var ui = Dispatcher.CurrentDispatcher;
                PluginSettings settings = null;
                try {
                    settings = new PluginSettings(MockProfiles());
                    settings.Initialize();
                    var planner = new NightPlannerVM(settings);
                    var property = changeSite ? nameof(NightPlannerVM.StatusText) : nameof(NightPlannerVM.SunColor);
                    planner.PropertyChanged += (_, e) => {
                        if (e.PropertyName == property)
                            onInterfaceThread = ReferenceEquals(Dispatcher.CurrentDispatcher, ui);
                    };
                    using var workerDone = new ManualResetEventSlim();

                    var worker = new Thread(() => {
                        try {
                            if (changeSite) settings.SiteLatOverride = "50.0875";
                            else settings.SunColor = "#FFD700";
                        } catch (Exception e) { workerFailure = e; }
                        workerFinished = true;
                        Stop(ui);
                        workerDone.Set();
                    });
                    worker.IsBackground = true;
                    worker.Start();

                    // Watchdog: if the change never lands, close the loop anyway so the
                    // assertions below report the miss instead of the run hanging.
                    var watchdog = new Thread(() => {
                        if (!workerDone.Wait(TimeSpan.FromSeconds(5))) Stop(ui);
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
            uiThread.IsBackground = true;
            uiThread.Start();
            Assert.That(uiThread.Join(TimeSpan.FromSeconds(15)), Is.True, "dispatcher thread finished");

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

        [Test, Apartment(ApartmentState.STA)]
        public void APlannerCallbackThatThrowsRunsOnlyOnce() {
            using var settings = new PluginSettings(MockProfiles());
            var planner = new NightPlannerVM(settings);
            var failure = new InvalidOperationException("Subscriber failed.");
            int calls = 0;
            planner.PropertyChanged += (_, e) => {
                if (e.PropertyName != nameof(NightPlannerVM.StatusText)) return;
                calls++;
                throw failure;
            };

            Assert.That(Assert.Throws<InvalidOperationException>(() => settings.SiteLatOverride = "50"), Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DispatcherShutdownDropsQueuedAndLaterSettingsChanges() {
            using var queued = new ManualResetEventSlim();
            bool wasQueued = false, workerFinished = false;
            Exception failure = null;
            int notifications = 0;
            var owner = new Thread(() => {
                try {
                    var ui = Dispatcher.CurrentDispatcher;
                    using var settings = new PluginSettings(MockProfiles());
                    var planner = new NightPlannerVM(settings);
                    planner.PropertyChanged += (_, _) => notifications++;
                    ui.Hooks.OperationPosted += (_, _) => queued.Set();
                    var worker = Task.Run(() => {
                        settings.SiteLatOverride = "50";
                        settings.SiteLonOverride = "14";
                    });
                    // Shut down before pumping the queue so the first change is aborted.
                    wasQueued = queued.Wait(TimeSpan.FromSeconds(5));
                    ui.InvokeShutdown();
                    workerFinished = worker.Wait(TimeSpan.FromSeconds(5));
                } catch (Exception ex) { failure = ex; }
            });
            owner.SetApartmentState(ApartmentState.STA);
            owner.IsBackground = true;
            owner.Start();

            Assert.That(owner.Join(TimeSpan.FromSeconds(15)), Is.True, "dispatcher thread finished");
            Assert.That(failure, Is.Null);
            Assert.That(wasQueued, Is.True);
            Assert.That(workerFinished, Is.True);
            Assert.That(notifications, Is.Zero);
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
