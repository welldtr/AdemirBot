using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiscordBot.Utils
{
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, int x, int y, int width, int height, int cornerRadius)
        {
            Rectangle rectangle = new Rectangle(x, y, width, height);
            GraphicsPath path = GetRoundedRectangle(rectangle, cornerRadius);
            graphics.FillPath(brush, path);
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle rectangle, int cornerRadius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = cornerRadius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(rectangle.Location, size);

            // Top left arc
            path.AddArc(arc, 180, 90);

            // Top right arc
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
