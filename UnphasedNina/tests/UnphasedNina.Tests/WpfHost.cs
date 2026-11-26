using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Zakfo.Unphased.Tests {
    /// <summary>
    /// The one place the suite creates a WPF Application. N.I.N.A.'s drag/drop attached
    /// behaviours need an Application with a main window named RootGrid, and its resource
    /// dictionaries need to be in scope for the plugin's markup to resolve N.I.N.A. brushes
    /// and control styles. The Application is created once and never shut down: a second one
    /// throws, and tearing one down strands the render thread for the rest of the run.
    /// </summary>
    internal static class WpfHost {
        public static void Ensure() {
            if (Application.Current == null) new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var grid = new Grid();
            Application.Current.MainWindow = new Window { Content = grid };
            NameScope.SetNameScope(Application.Current.MainWindow, new NameScope());
            Application.Current.MainWindow.RegisterName("RootGrid", grid);
            foreach (var name in new[] { "ProfileService", "SVGDictionary", "Brushes", "Converters" }) {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri($"/NINA.WPF.Base;component/Resources/StaticResources/{name}.xaml", UriKind.Relative)
                });
            }
        }
    }
}
