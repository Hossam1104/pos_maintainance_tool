namespace PosAdminTool.Maui.Services;

public static class UiAnimationExtensions
{
    private static readonly BindableProperty StoreMotionAttachedProperty = BindableProperty.CreateAttached(
        "StoreMotionAttached",
        typeof(bool),
        typeof(UiAnimationExtensions),
        false);

    private const uint HeroEntranceDuration = 470;
    private const uint ContentEntranceDuration = 380;
    private const uint HeroEntranceDelayStep = 90;
    private const uint ContentEntranceDelayStep = 55;
    private const double HeroEntranceTranslation = 20;
    private const double ContentEntranceTranslation = 12;
    private const double HeroEntranceScale = 0.982;
    private const double ContentEntranceScale = 0.994;
    private const double ButtonHoverScale = 1.006;
    private const double SurfaceHoverScale = 1.003;
    private const double ButtonHoverLift = -1;
    private const double SurfaceHoverLift = -1.5;
    private const double ButtonPressScale = 0.992;
    private const double SurfacePressScale = 0.998;
    private const double ButtonPressLift = 0.4;
    private const double SurfacePressLift = 0;

    private static readonly BindableProperty StoreHoveringProperty = BindableProperty.CreateAttached(
        "StoreHovering",
        typeof(bool),
        typeof(UiAnimationExtensions),
        false);

    private static readonly BindableProperty StorePressedProperty = BindableProperty.CreateAttached(
        "StorePressed",
        typeof(bool),
        typeof(UiAnimationExtensions),
        false);

    public static Task AnimateEntranceAsync(this VisualElement element, uint duration = 420, uint delay = 0)
    {
        return AnimateEntranceAsync(element, duration, delay, 22, 0.985);
    }

    public static async Task AnimateEntranceAsync(this VisualElement element, uint duration, uint delay, double translationY, double startScale)
    {
        if (!element.IsVisible)
        {
            return;
        }

        if (delay > 0)
        {
            await Task.Delay((int)delay).ConfigureAwait(true);
        }

        element.Opacity = 0;
        element.TranslationY = translationY;
        element.Scale = startScale;

        await Task.WhenAll(
            element.FadeToAsync(1, duration, Easing.CubicOut),
            element.TranslateToAsync(0, 0, duration, Easing.CubicOut),
            element.ScaleToAsync(1, duration, Easing.CubicOut));
    }

    public static async Task AnimateStorePageAsync(this Layout layout)
    {
        var children = layout.Children.OfType<VisualElement>().Where(child => child.IsVisible).ToList();
        if (children.Count == 0)
        {
            return;
        }

        var animations = new List<Task>
        {
            children[0].AnimateEntranceAsync(HeroEntranceDuration, 0, HeroEntranceTranslation, HeroEntranceScale)
        };

        var delay = HeroEntranceDelayStep;
        foreach (var child in children.Skip(1))
        {
            animations.Add(child.AnimateEntranceAsync(ContentEntranceDuration, delay, ContentEntranceTranslation, ContentEntranceScale));
            delay += ContentEntranceDelayStep;
        }

        await Task.WhenAll(animations).ConfigureAwait(true);
    }


    public static void EnableStoreInteractions(this Element root, bool includeBorders = false)
    {
        foreach (var element in EnumerateElements(root))
        {
            switch (element)
            {
                case Button button:
                    AttachButtonMotion(button);
                    break;
                case Border border when includeBorders:
                    AttachSurfaceMotion(border);
                    break;
            }
        }
    }

    private static IEnumerable<Element> EnumerateElements(Element root)
    {
        yield return root;

        foreach (var child in GetChildren(root))
        {
            foreach (var descendant in EnumerateElements(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<Element> GetChildren(Element element)
    {
        switch (element)
        {
            case Layout layout:
                foreach (var child in layout.Children.OfType<Element>())
                {
                    yield return child;
                }

                break;
            case Border border when border.Content is Element borderContent:
                yield return borderContent;
                break;
            case ContentView contentView when contentView.Content is Element content:
                yield return content;
                break;
            case ScrollView scrollView when scrollView.Content is Element scrollContent:
                yield return scrollContent;
                break;
            case ContentPage page when page.Content is Element pageContent:
                yield return pageContent;
                break;
        }
    }

    private static void AttachButtonMotion(Button button)
    {
        if ((bool)button.GetValue(StoreMotionAttachedProperty))
        {
            return;
        }

        button.SetValue(StoreMotionAttachedProperty, true);
        AttachHoverMotion(button, isButton: true);
        button.Pressed += OnButtonPressed;
        button.Released += OnButtonReleased;
        button.Unfocused += OnButtonUnfocused;
    }

    private static void AttachSurfaceMotion(Border border)
    {
        if ((bool)border.GetValue(StoreMotionAttachedProperty))
        {
            return;
        }

        border.SetValue(StoreMotionAttachedProperty, true);
        AttachHoverMotion(border, isButton: false);
    }

    private static void AttachHoverMotion(View element, bool isButton)
    {
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += async (_, _) =>
        {
            element.SetValue(StoreHoveringProperty, true);
            await AnimateInteractiveStateAsync(element, isButton).ConfigureAwait(true);
        };
        pointerGesture.PointerExited += async (_, _) =>
        {
            element.SetValue(StoreHoveringProperty, false);
            element.SetValue(StorePressedProperty, false);
            await AnimateInteractiveStateAsync(element, isButton).ConfigureAwait(true);
        };

        element.GestureRecognizers.Add(pointerGesture);
    }

    private static async void OnButtonPressed(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.SetValue(StorePressedProperty, true);
        await AnimateInteractiveStateAsync(button, isButton: true).ConfigureAwait(true);
    }

    private static async void OnButtonReleased(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.SetValue(StorePressedProperty, false);
        await AnimateInteractiveStateAsync(button, isButton: true).ConfigureAwait(true);
    }

    private static async void OnButtonUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.SetValue(StoreHoveringProperty, false);
        button.SetValue(StorePressedProperty, false);
        await AnimateInteractiveStateAsync(button, isButton: true).ConfigureAwait(true);
    }

    private static Task AnimateInteractiveStateAsync(VisualElement element, bool isButton)
    {
        var isHovering = (bool)element.GetValue(StoreHoveringProperty);
        var isPressed = (bool)element.GetValue(StorePressedProperty);

        double scale;
        double translationY;
        double opacity;
        uint duration;

        if (isPressed)
        {
            scale = isButton ? ButtonPressScale : SurfacePressScale;
            translationY = isButton ? ButtonPressLift : SurfacePressLift;
            opacity = 0.97;
            duration = 90;
        }
        else if (isHovering)
        {
            scale = isButton ? ButtonHoverScale : SurfaceHoverScale;
            translationY = isButton ? ButtonHoverLift : SurfaceHoverLift;
            opacity = 1;
            duration = 160;
        }
        else
        {
            scale = 1;
            translationY = 0;
            opacity = 1;
            duration = 180;
        }

        element.CancelAnimations();

        return Task.WhenAll(
            element.ScaleToAsync(scale, duration, Easing.CubicOut),
            element.TranslateToAsync(0, translationY, duration, Easing.CubicOut),
            element.FadeToAsync(opacity, duration, Easing.CubicOut));
    }
}
