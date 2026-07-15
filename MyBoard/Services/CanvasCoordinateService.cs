using System.Windows;

namespace MyBoard.Services
{
    internal static class CanvasCoordinateService
    {
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
