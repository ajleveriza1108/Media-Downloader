using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace MediaDownloader;

public partial class MainWindow
{
    private void UpdateNoticeR1635_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (UpdateNoticeR1620.Visibility != Visibility.Visible)
        {
            return;
        }

        UpdateNoticeR1620.BeginAnimation(OpacityProperty, null);
        UpdateNoticeTransformR1635.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            null);

        UpdateNoticeR1620.Opacity = 0;
        UpdateNoticeTransformR1635.Y = -10;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        UpdateNoticeR1620.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        UpdateNoticeTransformR1635.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            slide,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void UpdateNoticeDismissR1635_Click(object sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation(UpdateNoticeR1620.Opacity, 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        var slide = new DoubleAnimation(UpdateNoticeTransformR1635.Y, -7, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };

        fade.Completed += (_, _) =>
        {
            UpdateNoticeR1620.BeginAnimation(OpacityProperty, null);
            UpdateNoticeTransformR1635.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            UpdateNoticeR1620.Opacity = 1;
            UpdateNoticeTransformR1635.Y = 0;
            UpdateNoticeR1620.Visibility = Visibility.Collapsed;
        };

        UpdateNoticeR1620.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        UpdateNoticeTransformR1635.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            slide,
            HandoffBehavior.SnapshotAndReplace);
    }
}
