namespace PosAdminTool.Maui.Controls;

public sealed class PulsingDot : GraphicsView
{
    private bool _isAnimating;

    public static readonly BindableProperty DotColorProperty = BindableProperty.Create(
        nameof(DotColor),
        typeof(Color),
        typeof(PulsingDot),
        Color.FromArgb("#42A5F5"),
        propertyChanged: (bindable, _, _) => ((PulsingDot)bindable).Invalidate());

    public PulsingDot()
    {
        WidthRequest = 12;
        HeightRequest = 12;
        Drawable = new DotDrawable(this);
    }

    public Color DotColor
    {
        get => (Color)GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null)
        {
            _isAnimating = true;
            _ = PulseAsync();
        }
        else
        {
            _isAnimating = false;
        }
    }

    private async Task PulseAsync()
    {
        while (_isAnimating)
        {
            await Task.WhenAll(
                this.ScaleToAsync(1.28, 620, Easing.CubicInOut),
                this.FadeToAsync(0.58, 620, Easing.CubicInOut));
            await Task.WhenAll(
                this.ScaleToAsync(1, 620, Easing.CubicInOut),
                this.FadeToAsync(1, 620, Easing.CubicInOut));
        }
    }

    private sealed class DotDrawable(PulsingDot owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var outerRadius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f;
            var innerRadius = outerRadius * 0.58f;

            canvas.FillColor = owner.DotColor.WithAlpha(0.22f);
            canvas.FillCircle(dirtyRect.Center.X, dirtyRect.Center.Y, outerRadius);

            canvas.FillColor = owner.DotColor;
            canvas.FillCircle(dirtyRect.Center.X, dirtyRect.Center.Y, innerRadius);
        }
    }
}
