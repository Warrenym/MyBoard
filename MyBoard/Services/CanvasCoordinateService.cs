using System.Windows;

namespace MyBoard.Services
{
    internal static class CanvasCoordinateService
    {
        public static double ClampZoom(double zoom) => Math.Clamp(zoom, 0.4, 3.0);

        public static Point PanForZoom(Point pointer, double panX, double panY, double oldZoom, double newZoom)
        {
            Point canvas = ScreenToCanvas(pointer, panX, panY, oldZoom);
            return new Point(pointer.X - canvas.X * newZoom, pointer.Y - canvas.Y * newZoom);
        }

        public static Size Resize(double width, double height, double deltaX, double deltaY, double? aspectRatio = null)
        {
            double newWidth = Math.Max(60, width + deltaX);
            if (aspectRatio is > 0)
            {
                newWidth = Math.Max(newWidth, 40 * aspectRatio.Value);
                return new Size(newWidth, newWidth / aspectRatio.Value);
            }
            return new Size(newWidth, Math.Max(40, height + deltaY));
        }

        public static Rect SelectionBounds(Point start, Point end) => new(start, end);

        // Converts a screen-space point into canvas-space, reversing the current pan offset and zoom scale.
        public static Point ScreenToCanvas(Point screenPoint, double panX, double panY, double zoomLevel)
        {
            return new Point(
                (screenPoint.X - panX) / zoomLevel,
                (screenPoint.Y - panY) / zoomLevel);
        }

        // Converts a canvas-space point back into screen-space
        public static Point CanvasToScreen(Point canvasPoint, double panX, double panY, double zoomLevel)
        {
            return new Point(
                (canvasPoint.X * zoomLevel) + panX,
                (canvasPoint.Y * zoomLevel) + panY);
        }
    }
}
