using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace eStarter.Core
{
    public class TileAdorner : Adorner
    {
        private readonly Brush _visualBrush;
        private Point _centerOffset;

        public TileAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _visualBrush = new VisualBrush(adornedElement)
            {
                Opacity = 0.7,
                Stretch = Stretch.None
            };
            
            var bounds = VisualTreeHelper.GetDescendantBounds(adornedElement);
            _centerOffset = new Point(bounds.Width / 2, bounds.Height / 2);
            
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point position)
        {
            // We can't easily update position in standard AdornerLayer without invalidating arrange.
            // But for DragDrop, we usually use a custom window or popup, or just let the cursor handle it.
            // However, standard Win8 drag shows the tile.
            // We'll use the AdornerLayer.GetAdornerLayer(this.AdornedElement).Update() if needed, 
            // but actually standard DragDrop doesn't support updating Adorner position easily *during* the blocking DoDragDrop loop 
            // unless we use GiveFeedback event.
            
            // For simplicity in this MVP, we might rely on the standard cursor or a specialized implementation.
            // But let's try to make it work with GiveFeedback in MainWindow.
            
            _currentPosition = position;
            InvalidateVisual();
        }

        private Point _currentPosition;

        protected override void OnRender(DrawingContext drawingContext)
        {
            // If we are not using the GiveFeedback approach to move the adorner, 
            // it will stick to the element.
            // To make it follow mouse, we need to translate it.
            
            // Actually, a better approach for "Ghost" image in WPF DragDrop is using a Window or a Popup that follows the mouse,
            // or updating the Adorner transform in GiveFeedback.
            
            // Let's assume we will update the RenderTransform of this Adorner.
            
            var rect = new Rect(new Point(0, 0), DesiredSize);
            drawingContext.DrawRectangle(_visualBrush, null, rect);
        }
        
        public void SetOffsets(double left, double top)
        {
            // This method will be called from GiveFeedback to move the adorner
            this.RenderTransform = new TranslateTransform(left, top);
        }
    }
}
