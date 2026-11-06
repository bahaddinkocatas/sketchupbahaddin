using System.Windows;
using System.Windows.Media.Animation;

namespace Sketch.App;

/// <summary>Splash screen shown in the middle of the screen while the application starts.</summary>
public partial class SplashWindow : Window
{
    public SplashWindow(TimeSpan duration)
    {
        InitializeComponent();
        // The loading line fills over the whole splash time once the window has its size.
        Loaded += (_, _) => Fill.BeginAnimation(WidthProperty, new DoubleAnimation(0, Track.ActualWidth, duration));
    }
}
