using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Pos.App;
using Pos.App.Converters;
using Xunit;

namespace Pos.UiTests;

/// <summary>
/// Loads every view with the same application resources the real app uses.
/// Runtime XAML errors (invalid resource types, style TargetType mismatches,
/// bad markup extensions) throw here instead of during a customer's first click.
/// Must run on Windows because this is WPF.
/// </summary>
public sealed class XamlSmokeTests
{
    [Fact]
    public void Every_view_instantiates_and_measures()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Pos;component/Themes/Theme.Dark.xaml")
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Pos;component/Themes/Tokens.xaml")
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Pos;component/Styles/Controls.xaml")
                });

                // Same converter keys as App.xaml (application-level resources
                // are not loaded automatically when a test creates Application).
                application.Resources["BoolToVisibility"] = new BoolToVisibilityConverter();
                application.Resources["BoolToText"] = new BoolToTextConverter();
                application.Resources["StringEquals"] = new StringEqualsConverter();
                application.Resources["StringMap"] = new StringMapConverter();

                var viewTypes = typeof(Pos.App.App).Assembly.GetTypes()
                    .Where(type => !type.IsAbstract && type.IsPublic &&
                                   typeof(UserControl).IsAssignableFrom(type))
                    .OrderBy(type => type.FullName, StringComparer.Ordinal);

                foreach (var viewType in viewTypes)
                {
                    var view = (UserControl)Activator.CreateInstance(viewType)!;
                    view.Measure(new Size(1366, 768));
                    view.Arrange(new Rect(0, 0, 1366, 768));
                    view.UpdateLayout();
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(failure is null, failure?.ToString() ?? "A view failed to load.");
    }
}
