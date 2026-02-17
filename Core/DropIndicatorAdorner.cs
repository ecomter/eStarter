using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace eStarter.Core
{
    public class DropIndicatorAdorner : Adorner
    {
        private double _x;
        private double _y;
        private double _height;
        private readonly Brush _brush;
        private readonly Pen _linePen;
        private readonly Pen _capPen;
        private readonly Brush _glowBrush;

        public DropIndicatorAdorner(UIElement adornedElement, Brush accentBrush)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            _brush = accentBrush;
            _height = 150;

            _linePen = new Pen(accentBrush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _linePen.Freeze();

            _capPen = new Pen(accentBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _capPen.Freeze();

            // Soft glow behind the line
            if (accentBrush is SolidColorBrush scb)
            {
                var glowColor = scb.Color;
                glowColor.A = 60;
                _glowBrush = new SolidColorBrush(glowColor);
                _glowBrush.Freeze();
            }
            else
            {
                _glowBrush = Brushes.Transparent;
            }
        }

        public void UpdatePosition(double x, double y, double height)
        {
            _x = x;
            _y = y;
            _height = height;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var top = _y + 2;
            var bottom = _y + _height - 2;

            // Soft glow rectangle behind the line
            dc.DrawRoundedRectangle(_glowBrush, null,
                new Rect(_x - 5, top, 10, bottom - top), 3, 3);

            // Main vertical insertion line
            dc.DrawLine(_linePen, new Point(_x, top), new Point(_x, bottom));

            // Top and bottom caps (small horizontal bars)
            dc.DrawLine(_capPen, new Point(_x - 5, top), new Point(_x + 5, top));
            dc.DrawLine(_capPen, new Point(_x - 5, bottom), new Point(_x + 5, bottom));
        }
    }
}
