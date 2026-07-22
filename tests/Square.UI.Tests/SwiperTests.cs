using Square.Controls;
using Square.Events;
using Square.Graphics;
using Square.Rendering;
using Xunit;

namespace Square.UI.Tests;

public class SwiperTests
{
    [Fact]
    public void OnlySelectedPageIsVisible()
    {
        var swiper = CreateSwiper();

        Assert.Equal(0, swiper.SelectedIndex);
        Assert.True(swiper.Children[0].IsVisible);
        Assert.False(swiper.Children[1].IsVisible);

        Assert.True(swiper.GoTo(1));

        Assert.False(swiper.Children[0].IsVisible);
        Assert.True(swiper.Children[1].IsVisible);
    }

    [Fact]
    public void NavigationClampsUnlessLoopIsEnabled()
    {
        var swiper = CreateSwiper();

        Assert.False(swiper.Previous());
        Assert.True(swiper.Next());
        Assert.False(swiper.Next());

        swiper.Loop = true;

        Assert.True(swiper.Next());
        Assert.Equal(0, swiper.SelectedIndex);
        Assert.True(swiper.Previous());
        Assert.Equal(1, swiper.SelectedIndex);
    }

    [Fact]
    public void KeyboardNavigatesAndRaisesChange()
    {
        var swiper = CreateSwiper();
        var changes = 0;
        swiper.AddEventListener(StandardEvents.Change, () => changes++);

        Assert.True(swiper.HandleKey(39));
        Assert.True(swiper.HandleKey(36));
        Assert.True(swiper.HandleKey(35));

        Assert.Equal(1, swiper.SelectedIndex);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void RemovingPagesKeepsSelectionInRange()
    {
        var swiper = CreateSwiper();
        swiper.SelectedIndex = 1;

        swiper.Children.RemoveAt(1);

        Assert.Equal(0, swiper.SelectedIndex);
        Assert.True(swiper.Children[0].IsVisible);
    }

    [Fact]
    public void LayoutUsesOnlyTheVisiblePage()
    {
        var swiper = CreateSwiper();
        var layout = new LayoutEngine();

        layout.Measure(swiper, new Size(300, 120));
        layout.Arrange(swiper, new Rect(0, 0, 300, 120));

        Assert.Equal(new Rect(0, 0, 300, 120), swiper.Children[0].Geometry);
        Assert.Equal(Rect.Empty, swiper.Children[1].Geometry);
    }

    private static Swiper CreateSwiper()
    {
        var swiper = new Swiper();
        swiper.Children.Add(new View());
        swiper.Children.Add(new View());
        return swiper;
    }
}
