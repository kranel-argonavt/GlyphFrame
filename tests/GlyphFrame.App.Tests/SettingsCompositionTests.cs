using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class SettingsCompositionTests
{
    [Theory]
    [InlineData(1024)]
    [InlineData(1280)]
    [InlineData(1600)]
    public void ConditionalPlaceholder_CentersOnlyInActualSpareSpace_AcrossRepeatedResize(double width)
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new(); data.Set("Video");
            window.DataContext = data; window.Width = width; window.MinHeight = 0;
            window.Height = 1200; window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                RenderSettingsPanel panel = Assert.Single(Descendants(window).OfType<RenderSettingsPanel>());
                ScrollViewer body = (ScrollViewer)panel.Children[1];
                bool visible = false, hidden = false;
                foreach (double height in new[] { 1200d, 1100, 820, 720, 480, 1200, 1100, 1200 })
                {
                    window.Height = height; Settle(window);
                    AssertBindingContract(panel, data);
                    Assert.Equal(4, panel.Children.Count);
                    FrameworkElement placeholder = (FrameworkElement)panel.Children[3];
                    double parameters = ((UIElement)body.Content).DesiredSize.Height;
                    double spare = body.RenderSize.Height - parameters;
                    Assert.Equal(!panel.UseWholePaneScrolling && spare >= placeholder.DesiredSize.Height + RenderSettingsPanel.PlaceholderClearance, panel.ShowsComingSoon);
                    Assert.Equal(panel.ShowsComingSoon, placeholder.IsVisible);
                    visible |= panel.ShowsComingSoon; hidden |= !panel.ShowsComingSoon;
                    Assert.False(placeholder.Focusable); Assert.False(placeholder.IsHitTestVisible);
                    Assert.All(Descendants(placeholder).OfType<FrameworkElement>(), e => Assert.False(e.Focusable));
                    if (panel.ShowsComingSoon)
                    {
                        Rect group = Bounds(placeholder, panel), viewport = Bounds(body, panel);
                        double top = viewport.Top + parameters;
                        Assert.InRange(Math.Abs(group.Left + group.Width / 2 - panel.ActualWidth / 2), 0, 0.5);
                        Assert.InRange(Math.Abs(group.Top + group.Height / 2 - (top + viewport.Bottom) / 2), 0, 0.5);
                        Assert.True(group.Top >= top + RenderSettingsPanel.PlaceholderClearance / 2 - 0.5);
                        Assert.True(group.Bottom <= viewport.Bottom - RenderSettingsPanel.PlaceholderClearance / 2 + 0.5);
                        Assert.InRange(body.ScrollableHeight, 0, 1);
                    }
                    Assert.True(double.IsFinite(body.ViewportHeight) && body.ViewportHeight >= RenderSettingsPanel.MinimumParameterViewport);
                    Rect heading = Bounds((FrameworkElement)panel.Children[0], panel), audio = Bounds((FrameworkElement)panel.Children[2], panel);
                    Assert.InRange(Math.Abs(heading.Bottom + ((FrameworkElement)panel.Children[0]).Margin.Bottom - Bounds(body, panel).Top), 0, 1);
                    Assert.InRange(Math.Abs(audio.Top - ((FrameworkElement)panel.Children[2]).Margin.Top - Bounds(body, panel).Bottom), 0, 1);
                    Assert.InRange(Math.Abs(audio.Bottom - panel.ActualHeight), 0, 1);
                    if (height == 1200) { Assert.InRange(body.ScrollableHeight, 0, 1); }
                    InputSettingsPolishTests.Capture(window, $"placeholder-{width}-{height}");
                }
                Assert.True(visible); Assert.True(hidden);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ConditionalPlaceholder_IncreasedParameterTextHidesWithoutChangingPinnedAllocation()
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new(); data.Set("Video"); window.DataContext = data;
            window.Width = 1280; window.Height = 1080; window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                RenderSettingsPanel panel = Assert.Single(Descendants(window).OfType<RenderSettingsPanel>());
                ScrollViewer body = (ScrollViewer)panel.Children[1];
                FrameworkElement placeholder = (FrameworkElement)panel.Children[3];
                double spare = body.ActualHeight - ((UIElement)body.Content).DesiredSize.Height;
                window.Height -= spare - placeholder.DesiredSize.Height - RenderSettingsPanel.PlaceholderClearance - 2;
                Settle(window); Assert.True(panel.ShowsComingSoon);
                Rect heading = Bounds((FrameworkElement)panel.Children[0], panel), audio = Bounds((FrameworkElement)panel.Children[2], panel);
                double viewport = body.ViewportHeight, desired = panel.DesiredSize.Height;
                foreach (TextBlock text in Descendants(body).OfType<TextBlock>()) { text.FontSize = 24; }
                Settle(window);
                Assert.False(panel.ShowsComingSoon); Assert.False(placeholder.IsVisible);
                Assert.Equal(heading, Bounds((FrameworkElement)panel.Children[0], panel));
                Assert.Equal(audio, Bounds((FrameworkElement)panel.Children[2], panel));
                Assert.Equal(viewport, body.ViewportHeight); Assert.Equal(desired, panel.DesiredSize.Height);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ConditionalPlaceholder_ExactBoundaryNeverChangesMinimumHeightOrScrollExtent()
    {
        Sta(() =>
        {
            RenderSettingsPanel panel = new();
            panel.Children.Add(new Border { Height = 30 });
            Border parameters = new() { Height = 160, Margin = new Thickness(0, 3, 0, 7) };
            ScrollViewer body = new() { Content = parameters, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            panel.Children.Add(body); panel.Children.Add(new Border { Height = 100 });
            Border placeholder = new() { Height = 46, Width = 120, Focusable = false, IsHitTestVisible = false };
            panel.Children.Add(placeholder);
            double minimum = panel.MeasurePinnedMinimum(300);
            foreach (double free in new[] { 62d, 61, 0, -1, 100, 62, 61, 62 })
            {
                double height = 30 + 170 + free + 100;
                SetSize(height);
                Assert.Equal(free >= 62, panel.ShowsComingSoon);
                Assert.Equal(minimum, panel.MeasurePinnedMinimum(300));
                Assert.Equal(30, Bounds(body, panel).Top);
                Assert.Equal(height, Bounds((FrameworkElement)panel.Children[2], panel).Bottom);
                Assert.Equal(height, panel.DesiredSize.Height);
                double extent = body.ExtentHeight, viewport = body.ViewportHeight;
                placeholder.Height = 3000; SetSize(height);
                Assert.False(panel.ShowsComingSoon); Assert.False(placeholder.IsVisible);
                Assert.Equal(extent, body.ExtentHeight); Assert.Equal(viewport, body.ViewportHeight);
                Assert.Equal(height, panel.DesiredSize.Height); Assert.Equal(minimum, panel.MeasurePinnedMinimum(300));
                placeholder.Height = 46; SetSize(height); Assert.Equal(free >= 62, panel.ShowsComingSoon);
            }
            parameters.Height = 400; SetSize(362);
            Assert.True(body.ScrollableHeight > 0); Assert.False(panel.ShowsComingSoon);
            panel.UseWholePaneScrolling = true; panel.InvalidateMeasure();
            panel.Measure(new Size(300, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 300, panel.DesiredSize.Height)); panel.UpdateLayout();
            Assert.False(panel.ShowsComingSoon); Assert.Equal(ScrollBarVisibility.Disabled, body.VerticalScrollBarVisibility);
            Assert.Equal(0, body.ScrollableHeight);
            void SetSize(double height)
            { panel.InvalidateMeasure(); panel.Measure(new Size(300, height)); panel.Arrange(new Rect(0, 0, 300, height)); panel.UpdateLayout(); }
        });
    }
    [Fact]
    public void ReadyAndCharacters_UseDedicatedVectors_WithoutChangingSymbolizedOrSuccess()
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new(); data.Set("Video"); window.DataContext = data;
            window.Width = 1440; window.Height = 1080; window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                Border ready = Assert.Single(Descendants(window).OfType<Border>(), b => b.Name == "ReadyToRenderCard");
                Border characters = Assert.Single(Descendants(window).OfType<Border>(), b => b.Name == "CharactersCard");
                Button symbolized = Assert.Single(Descendants(window).OfType<Button>(), b => b.Name == "SymbolizedPreviewTab");
                Assert.True(ready.IsVisible); Assert.True(characters.IsVisible); Assert.True(symbolized.IsVisible);
                System.Windows.Shapes.Path flag = Assert.Single(Descendants(ready).OfType<System.Windows.Shapes.Path>());
                System.Windows.Shapes.Path hash = Assert.Single(Descendants(characters).OfType<System.Windows.Shapes.Path>(), p => p.Width == 20);
                System.Windows.Shapes.Path symbols = Assert.Single(Descendants(symbolized).OfType<System.Windows.Shapes.Path>());
                Assert.Equal(((Geometry)window.FindResource("ReadyToRenderIcon")).ToString(), flag.Data.ToString());
                Assert.Equal(((Geometry)window.FindResource("CharactersIcon")).ToString(), hash.Data.ToString());
                Assert.Equal(((Geometry)window.FindResource("SymbolsIcon")).ToString(), symbols.Data.ToString());
                Assert.Equal(Geometry.Parse("M3,3 L8,3 8,8 3,8 Z M16,3 L21,3 21,8 16,8 Z M3,16 L8,16 8,21 3,21 Z M16,16 L21,16 21,21 16,21 Z").ToString(), symbols.Data.ToString());
                Assert.Equal(Geometry.Parse("M1,8 L7,14 21,1").ToString(), ((Geometry)window.FindResource("SuccessIcon")).ToString());
                Assert.Equal(22, flag.Width); Assert.Equal(22, flag.Height);
                // Native layout rounding at the host DPI may round 22 DIPs to 22.4.
                Assert.InRange(Math.Abs(22 - Math.Max(flag.ActualWidth, flag.ActualHeight)), 0, 0.5);
                Assert.Equal(20, hash.Width); Assert.InRange(Math.Abs(20 - Math.Max(hash.ActualWidth, hash.ActualHeight)), 0, 0.5);
                Assert.Equal(4, hash.Data.GetFlattenedPathGeometry().Figures.Count);
                foreach (System.Windows.Shapes.Path icon in new[] { flag, hash, symbols })
                {
                    Assert.Equal(1.6, icon.StrokeThickness); Assert.Equal(PenLineCap.Round, icon.StrokeStartLineCap);
                    Assert.Equal(PenLineJoin.Round, icon.StrokeLineJoin); Assert.False(icon.Focusable);
                }
                Assert.NotEqual(flag.Data.ToString(), hash.Data.ToString());
                Assert.NotEqual(symbols.Data.ToString(), hash.Data.ToString());
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ActualSelectors_KeepValueAndChevronBoundsWhenOpened_AndUseSharedVectors()
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
            SettingsData data = new(); data.Set("Video");
            window.DataContext = data; window.Width = 1440; window.Height = 1080; window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                ComboBox[] selectors = Descendants(window).OfType<ComboBox>().Where(c => c.IsVisible && c.IsEnabled).ToArray();
                Assert.True(selectors.Length >= 5);
                foreach (ComboBox selector in selectors)
                {
                    selector.ApplyTemplate(); Settle(window);
                    FrameworkElement arrowCell = (FrameworkElement)selector.Template.FindName("ArrowCell", selector);
                    System.Windows.Shapes.Path chevron = (System.Windows.Shapes.Path)selector.Template.FindName("Chevron", selector);
                    FrameworkElement value = (FrameworkElement)selector.Template.FindName("SelectionContent", selector);
                    Rect original = Bounds(selector, window), valueBounds = Bounds(value, selector), arrowBounds = Bounds(chevron, selector);
                    Rect cell = Bounds(arrowCell, selector);
                    Assert.InRange(Math.Abs(arrowBounds.Left + arrowBounds.Width / 2 - cell.Left - cell.Width / 2), 0, 1);
                    Assert.InRange(Math.Abs(arrowBounds.Top + arrowBounds.Height / 2 - cell.Top - cell.Height / 2), 0, 1);
                    Assert.False(chevron.Focusable); Assert.Null(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(chevron));
                    object? selected = selector.SelectedItem;
                    selector.IsDropDownOpen = true; Settle(window);
                    Assert.Equal(180, Assert.IsType<RotateTransform>(chevron.RenderTransform).Angle);
                    Assert.Equal(original, Bounds(selector, window)); Assert.Equal(valueBounds, Bounds(value, selector));
                    Rect openArrow = Bounds(chevron, selector);
                    // A 180-degree transform can differ at floating-point epsilon, not a layout DIP.
                    Assert.Equal(arrowBounds.X, openArrow.X, 6); Assert.Equal(arrowBounds.Y, openArrow.Y, 6);
                    Assert.Equal(arrowBounds.Width, openArrow.Width, 6); Assert.Equal(arrowBounds.Height, openArrow.Height, 6);
                    Assert.Same(selected, selector.SelectedItem);
                    selector.IsDropDownOpen = false; Settle(window);
                    Assert.Equal(0, Assert.IsType<RotateTransform>(chevron.RenderTransform).Angle);
                }
                Geometry frameS = (Geometry)window.FindResource("RenderIcon"), settings = (Geometry)window.FindResource("RenderSettingsIcon");
                Assert.NotEqual(frameS.ToString(), settings.ToString());
                PathGeometry flattened = frameS.GetFlattenedPathGeometry();
                Assert.Equal(5, flattened.Figures.Count); // Four frame corners and one independent curved capital S.
                Rect s = new PathGeometry([flattened.Figures[^1]]).Bounds;
                Assert.True(s.Left > frameS.Bounds.Left && s.Right < frameS.Bounds.Right && s.Top > frameS.Bounds.Top && s.Bottom < frameS.Bounds.Bottom);
                Geometry speaker = (Geometry)window.FindResource("SpeakerIcon"), sound = (Geometry)window.FindResource("PlayerSoundIcon"), mute = (Geometry)window.FindResource("PlayerMuteIcon");
                Assert.Equal(speaker.ToString(), sound.ToString());
                Assert.StartsWith(sound.ToString(), mute.ToString());
                Assert.Equal(sound.GetFlattenedPathGeometry().Figures.Count + 1, mute.GetFlattenedPathGeometry().Figures.Count);
                Assert.DoesNotContain(Descendants(window).OfType<TextBlock>(), t => t.Text == "⌄");
                foreach (double width in new[] { 1024d, 1440 })
                {
                    window.Width = width; Settle(window);
                    FontSettingsPanel fonts = Assert.Single(Descendants(window).OfType<FontSettingsPanel>());
                    foreach (Grid pair in fonts.Children)
                    {
                        TextBlock label = pair.Children.OfType<TextBlock>().Single(); ComboBox field = pair.Children.OfType<ComboBox>().Single();
                        Rect l = Bounds(label, pair), f = Bounds(field, pair);
                        Assert.True(l.Right <= f.Left); Assert.InRange(Math.Abs(l.Top + l.Height / 2 - f.Top - f.Height / 2), 0, 1);
                        Assert.True(field.ActualWidth >= 64);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RenderBody_ChoosesOneScrollOwner_WithConditionalPlaceholder()
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new(); data.Set("Video");
            window.DataContext = data; window.Width = 1280; window.MinHeight = 0;
            window.Height = 1080; window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                ScrollViewer outer = (ScrollViewer)window.FindName("SettingsPaneScrollViewer");
                SettingsPanePanel composition = Assert.Single(Descendants(outer).OfType<SettingsPanePanel>());
                RenderSettingsPanel render = Assert.Single(Descendants(outer).OfType<RenderSettingsPanel>());
                ScrollViewer body = Assert.Single(Descendants(render).OfType<ScrollViewer>());
                FrameworkElement heading = (FrameworkElement)render.Children[0], audio = (FrameworkElement)render.Children[2];
                Assert.Equal(4, render.Children.Count);
                bool observedBodyOverflow = false, observedWholePaneFallback = false;
                foreach (double height in new[] { 1080d, 820, 780, 740, 700, 600, 480, 1080 })
                {
                    window.Height = height; Settle(window);
                    AssertBindingContract(outer, data);
                    Assert.True(double.IsFinite(body.ViewportHeight) && body.ViewportHeight > 0);
                    Assert.Equal(ScrollBarVisibility.Disabled, body.HorizontalScrollBarVisibility);
                    Assert.True(Bounds(heading, render).Bottom <= Bounds(body, render).Top + 1);
                    Assert.True(Bounds(body, render).Bottom <= Bounds(audio, render).Top + 1);
                    Assert.InRange(Math.Abs(Bounds(audio, render).Bottom - render.ActualHeight), 0, 1);

                    if (composition.UsesWholePaneScrolling)
                    {
                        observedWholePaneFallback = true;
                        Assert.Equal(ScrollBarVisibility.Disabled, body.VerticalScrollBarVisibility);
                        Assert.Equal(0, body.ScrollableHeight); Assert.True(outer.ScrollableHeight > 0);

                        outer.ScrollToEnd(); Settle(window);
                        Assert.True(Bounds(audio, outer).Bottom <= outer.ViewportHeight + 1);
                        outer.ScrollToHome(); Settle(window);
                    }
                    else
                    {
                        Assert.InRange(outer.ScrollableHeight, 0, 1);
                        Rect oldHeading = Bounds(heading, render), oldAudio = Bounds(audio, render);
                        if (body.ScrollableHeight > 0)
                        {
                            observedBodyOverflow = true;

                            body.ScrollToEnd(); Settle(window);
                            Assert.Equal(oldHeading, Bounds(heading, render)); Assert.Equal(oldAudio, Bounds(audio, render));
                            // Keyboard-style focus navigation can bring a lower parameter back into the one owner.
                            ComboBox last = Descendants(body).OfType<ComboBox>().Last();
                            last.Focus(); last.BringIntoView(); Settle(window);
                            Assert.InRange(outer.VerticalOffset, 0, 1);
                        }
                        if (height >= 820) { Assert.InRange(body.ScrollableHeight, 0, 1); }
                    }

                }
                Assert.True(observedBodyOverflow); Assert.True(observedWholePaneFallback);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(240, false)]
    [InlineData(389, false)]
    [InlineData(390, true)]
    [InlineData(420, true)]
    public void FontFields_ReflowFromActualWidthWithoutDuplicatingControls(double width, bool horizontal)
    {
        Sta(() =>
        {
            FontSettingsPanel panel = new();
            foreach (string label in new[] { "Font Family", "Font Size" })
            {
                StackPanel field = new();
                field.Children.Add(new TextBlock { Text = label });
                field.Children.Add(new ComboBox { Height = 36, ItemsSource = new[] { label == "Font Family" ? "Cascadia Mono" : "10" }, SelectedIndex = 0 });
                panel.Children.Add(field);
            }
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height)); panel.UpdateLayout();
            Assert.Equal(horizontal, panel.IsHorizontal);
            Assert.Equal(2, panel.Children.Count);
            Assert.All(panel.Children.Cast<FrameworkElement>(), c => AssertContained(c, panel));
            Rect family = Bounds((FrameworkElement)panel.Children[0], panel), size = Bounds((FrameworkElement)panel.Children[1], panel);
            Assert.False(family.IntersectsWith(size));
            Assert.True(horizontal ? family.Right < size.Left : family.Bottom < size.Top);
        });
    }

    [Theory]
    [InlineData(1280, 820, true)]
    [InlineData(1440, 1080, true)]
    [InlineData(1024, 720, false)]
    public void ActualSettings_FillFinitePaneAndUseOnlyNecessaryVerticalOverflow(double width, double height, bool fits)
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new();
            window.DataContext = data; window.Width = width; window.Height = height;
            window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                ScrollViewer scroll = (ScrollViewer)window.FindName("SettingsPaneScrollViewer");
                SettingsPanePanel composition = Assert.Single(Descendants(scroll).OfType<SettingsPanePanel>());
                Border[] cards = composition.Children.OfType<Border>().ToArray();
                Assert.Equal(3, cards.Length);
                ComboBox preset = Assert.Single(Descendants(window).OfType<ComboBox>(), c =>
                    BindingOperations.GetBinding(c, ItemsControl.ItemsSourceProperty)?.Path.Path == "Presets");
                Assert.True(preset.IsVisible);
                Assert.Same(data.SelectedPreset, preset.SelectedItem);
                Assert.Equal("Name", preset.DisplayMemberPath);
                Assert.All(Descendants(scroll).OfType<System.Windows.Shapes.Path>(), icon =>
                {
                    Assert.False(icon.Focusable);
                    Assert.Null(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(icon));
                    Assert.NotNull(icon.Data);
                    Assert.NotEqual(Colors.Black, Assert.IsType<SolidColorBrush>(icon.Stroke).Color);
                });
                foreach (string state in new[] { "Empty", "Image", "Video", "Silent", "Busy", "ReplacementError", "Image" })
                {
                    data.Set(state); Settle(window);
                    StackPanel audio = Assert.Single(Descendants(scroll).OfType<StackPanel>(), p => p.Name == "VideoAudioSection");
                    Assert.True(audio.IsVisible);
                    Assert.Contains(Descendants(audio).OfType<TextBlock>(), t => t.Text == "Applies to the rendered video.");
                    Border overlay = Assert.Single(Descendants(audio).OfType<Border>(), b => b.Name == "AudioUnavailableSurface");
                    Assert.Equal(data.ShowVideoAudioUnavailableOverlay, overlay.IsVisible);
                    if (overlay.IsVisible) { Assert.Contains(Descendants(overlay).OfType<TextBlock>(), t => t.Text == data.VideoAudioUnavailableReason); }
                    Grid buttons = Assert.Single(Descendants(audio).OfType<Grid>(), g => g.Name == "AudioButtons");
                    Assert.Equal(overlay.IsVisible ? 0 : 1, buttons.Opacity);
                    Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
                    Assert.Equal(System.Windows.Input.Cursors.Arrow, overlay.Cursor);
                    Assert.Equal(((SolidColorBrush)window.FindResource("PreviewBackgroundBrush")).Color, ((SolidColorBrush)overlay.Background).Color);
                    Assert.Equal(new Thickness(1), overlay.BorderThickness);
                    if (overlay.IsVisible) { Assert.Equal(Bounds(buttons, audio), Bounds(overlay, audio)); }
                    RadioButton[] choices = Descendants(audio).OfType<RadioButton>().ToArray();
                    Assert.Equal(2, choices.Length);
                    Assert.All(choices, c => Assert.Equal(data.AreVideoAudioSettingsEnabled, c.IsEnabled));
                    Assert.True(choices[0].IsChecked); Assert.False(choices[1].IsChecked);
                    Assert.Equal(Visibility.Visible, Assert.IsType<System.Windows.Shapes.Path>(choices[0].Template.FindName("SelectionMark", choices[0])).Visibility);
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<System.Windows.Shapes.Path>(choices[1].Template.FindName("SelectionMark", choices[1])).Visibility);
                    Assert.InRange(Math.Abs(choices[0].ActualWidth - choices[1].ActualWidth), 0, 1);
                    Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                    Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
                    if (fits && state != "ReplacementError")
                    {
                        Assert.True(scroll.ExtentHeight <= scroll.ViewportHeight + 1,
                            $"Unnecessary scroll at {width}x{height} {state}: extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, cards={string.Join(",", cards.Select(c => c.ActualHeight))}");
                        Assert.InRange(Math.Abs(Bounds(cards[^1], composition).Bottom - scroll.ViewportHeight), 0, 1);
                    }
                    else if (!fits) { Assert.True(scroll.ExtentHeight > scroll.ViewportHeight); }
                    FontSettingsPanel fonts = Assert.Single(Descendants(scroll).OfType<FontSettingsPanel>());
                    Assert.Equal(fonts.ActualWidth >= 390, fonts.IsHorizontal);
                    FrameworkElement family = (FrameworkElement)fonts.Children[0], size = (FrameworkElement)fonts.Children[1];
                    Assert.True(family.ActualWidth >= 230);
                    Assert.True(fonts.IsHorizontal ? Bounds(family, fonts).Right < Bounds(size, fonts).Left
                        : Bounds(family, fonts).Bottom < Bounds(size, fonts).Top);
                    TextBlock name = Assert.Single(Descendants(scroll).OfType<TextBlock>(), t => t.Name == "InputFileName");
                    Assert.Equal(data.InputDisplayName, name.Text);
                    Assert.Equal(data.SelectedFilePath, name.ToolTip);
                    AssertContained(name, cards[0]);
                    StackPanel sourceReady = Assert.Single(Descendants(scroll).OfType<StackPanel>(), s => s.Name == "SourceReady");
                    Assert.Equal(data.IsInputReady, sourceReady.IsVisible);
                    ComboBox presetField = Assert.Single(Descendants(scroll).OfType<ComboBox>(), c => c.Name == "PresetField");
                    Border ramp = Assert.Single(Descendants(scroll).OfType<Border>(), b => b.Name == "RampField");
                    TextBlock rampLabel = Assert.Single(Descendants(scroll).OfType<TextBlock>(), t => t.Name == "RampLabel");
                    Assert.InRange(Math.Abs(Bounds(presetField, composition).Left - Bounds(ramp, composition).Left), 0, 1);
                    Assert.InRange(Math.Abs(Bounds(rampLabel, composition).Top + rampLabel.ActualHeight / 2 - Bounds(ramp, composition).Top - ramp.ActualHeight / 2), 0, 1);
                    Button details = Assert.Single(Descendants(scroll).OfType<Button>(), b => b.Content as string == "Show Details");
                    Assert.Equal(data.ShowRetainedMediaInputErrorDetails, details.IsVisible);
                    Assert.Same(data.ShowMediaInputErrorDetailsCommand, details.Command);
                    Assert.Equal(data.HasMediaInputError, data.MediaInputErrorPresentation is not null);
                    if (details.IsVisible) { Assert.True(details.IsEnabled); AssertContained(details, cards[0]); }
                    scroll.ScrollToEnd(); Settle(window);
                    Rect audioBounds = Bounds(audio, scroll);
                    Assert.True(audioBounds.Bottom <= scroll.ViewportHeight + 1);
                    Assert.True(audioBounds.Top >= 0);
                    scroll.ScrollToHome(); Settle(window);
                    AssertBindingContract(scroll, data);
                }
                Assert.DoesNotContain(Descendants(window).OfType<ComboBox>(), c => c.ToolTip as string == "Select the character preset used by future symbol previews");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PlannedMetrics_AreFarRightAndNotResultOrExportControls()
    {
        Sta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow();
            SettingsData data = new(); data.Set("Video");
            window.DataContext = data; window.Width = 1440; window.Height = 1080;
            window.Left = -10000; window.ShowActivated = false;
            try
            {
                window.Show(); Settle(window);
                Border ready = Assert.Single(Descendants(window).OfType<Border>(), b => b.Name == "ReadyToRenderCard");
                Assert.True(ready.IsVisible);
                AdaptiveResultPanel panel = Assert.Single(Descendants(ready).OfType<AdaptiveResultPanel>());
                FrameworkElement metrics = Assert.Single(panel.Children.Cast<FrameworkElement>(), c => AdaptiveResultPanel.GetRole(c) == AdaptiveResultPanelRole.Metrics);
                Assert.InRange(Math.Abs(Bounds(metrics, panel).Right - panel.ActualWidth), 0, 1);
                Assert.Empty(Descendants(ready).OfType<Button>());
                Assert.DoesNotContain(Descendants(ready).OfType<TextBlock>(), t => t.Text is "FILE SIZE" or "FRAMES" or "OUTPUT");
            }
            finally { window.Close(); }
        });
    }

    private sealed class SettingsData : Phase13UiTests.MainLayoutData, INotifyPropertyChanged
    {
        public SettingsData() : base(false) { }
        private string _state = "Empty";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set(string state) { _state = state; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
        public new bool IsMediaSelected => _state != "Empty";
        public new bool IsVideoSelected => _state is "Video" or "Silent" or "Busy";
        public new bool IsImageSelected => IsMediaSelected && !IsVideoSelected;
        public new bool HasMediaInputError => _state == "ReplacementError";
        public new bool IsInputReady => IsMediaSelected && !HasMediaInputError;
        public new bool ShowRetainedMediaInputErrorDetails => HasMediaInputError;
        public ErrorPresentation? MediaInputErrorPresentation => HasMediaInputError ? ErrorPresentation.Create(
            ApplicationErrorCode.InvalidImage, ErrorPresentationContext.MediaInput, "Retained technical detail") : null;
        public new string InputStatusMessage => MediaInputErrorPresentation?.UserMessage ?? string.Empty;
        public new string InputDisplayName => IsMediaSelected ? "Оригінал_日本語_Кириллица_" + new string('Ж', 100) + ".mp4" : "No media selected";
        public new string InputCardSummary => IsMediaSelected ? InputMetadataSummary : "Choose an image or video.";
        public new string SelectedFilePath => @"C:\Медіа\long original source directory\" + InputDisplayName;
        public new string InputMetadataSummary => "1920 × 1080 • H.264 • 30 FPS • 0:20";
        public new string InputReadinessText => HasMediaInputError ? "" : IsMediaSelected ? "Source accepted" : "Choose an image or video.";
        public new string BrowseMediaActionLabel => IsMediaSelected ? "Change file" : "Browse Media";
        public new string InputDropText => IsMediaSelected ? "Drop another file to replace" : "Drop a local file here";
        public new bool AreSettingsEnabled => _state != "Busy";
        public new bool AreVideoAudioSettingsEnabled => IsVideoSelected && _state is not "Silent" and not "Busy";
        public new bool ShowVideoAudioUnavailableOverlay => AreSettingsEnabled && (!IsVideoSelected || _state == "Silent");
        public new string VideoAudioUnavailableReason => _state == "Empty" ? "Select a video first"
            : IsImageSelected ? "Audio does not apply to images" : "No audio track in this video";
        public new bool IsProcessingResultPanelVisible => IsMediaSelected;
        public new bool ShowReadyToRender => IsMediaSelected;
        public new bool ShowImageResultPanel => false;
        public new bool ShowVideoResultPanel => false;
        public new bool ShowImageExportControls => false;
        public new bool ShowVideoRenderMetrics => false;
        public new bool ShowImageRenderMetrics => false;
        public new bool HasVideoArtifact => false;
        public new bool ShowPreviewFrameAction => IsVideoSelected;
        public IRelayCommand BrowseMediaCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand DropMediaCommand { get; } = new RelayCommand(() => { });
    }

    private static void AssertBindingContract(DependencyObject root, object data)
    {
        foreach (DependencyObject element in Descendants(root))
        {
            LocalValueEnumerator values = element.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                BindingExpression? expression = BindingOperations.GetBindingExpression(element, values.Current.Property);
                if (expression?.DataItem != data) { continue; }
                Assert.NotNull(data.GetType().GetProperty(expression.ParentBinding.Path.Path.Split('.')[0]));
                Assert.False(expression.HasError, expression.ParentBinding.Path.Path);
            }
        }
    }
    private static void AssertContained(FrameworkElement child, Visual parent)
    {
        Rect bounds = Bounds(child, parent);
        Assert.True(bounds.Left >= -1 && bounds.Top >= -1);
        Assert.True(bounds.Right <= ((FrameworkElement)parent).ActualWidth + 1 && bounds.Bottom <= ((FrameworkElement)parent).ActualHeight + 1);
    }
    private static Rect Bounds(FrameworkElement child, Visual parent) => child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (DependencyObject nested in Descendants(child)) { yield return nested; }
        }
    }
    private static void Settle(Window window)
    {
        for (int i = 0; i < 3; i++) { window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
