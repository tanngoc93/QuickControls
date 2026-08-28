using System;
using System.Drawing;
using QuickControls.Models;

namespace QuickControls.UI
{
    public static class PanelPlacement
    {
        public static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum) return minimum;
            return Math.Max(minimum, Math.Min(value, maximum));
        }

        public static Rectangle Clamp(Rectangle bounds, Rectangle workingArea)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0) return bounds;

            int left = bounds.Width >= workingArea.Width
                ? workingArea.Left
                : Clamp(bounds.Left, workingArea.Left, workingArea.Right - bounds.Width);
            int top = bounds.Height >= workingArea.Height
                ? workingArea.Top
                : Clamp(bounds.Top, workingArea.Top, workingArea.Bottom - bounds.Height);
            return new Rectangle(left, top, bounds.Width, bounds.Height);
        }

        public static PanelDockEdge FindNearestEdge(Point point, Rectangle workingArea)
        {
            int distanceFromLeft = Math.Abs(point.X - workingArea.Left);
            int distanceFromRight = Math.Abs(workingArea.Right - point.X);
            return distanceFromLeft < distanceFromRight ? PanelDockEdge.Left : PanelDockEdge.Right;
        }

        public static PanelDockEdge FindNearestEdge(Rectangle bounds, Rectangle workingArea)
        {
            Point center = new Point(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2);
            return FindNearestEdge(center, workingArea);
        }

        public static Rectangle GetDockBounds(
            Rectangle currentBounds,
            Rectangle workingArea,
            Size targetSize,
            PanelDockEdge edge)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0)
            {
                return new Rectangle(currentBounds.Location, targetSize);
            }

            PanelDockEdge resolvedEdge = edge == PanelDockEdge.Automatic
                ? FindNearestEdge(currentBounds, workingArea)
                : edge;
            if (resolvedEdge != PanelDockEdge.Left && resolvedEdge != PanelDockEdge.Right)
            {
                resolvedEdge = PanelDockEdge.Right;
            }

            int centerY = currentBounds.Height > 0
                ? currentBounds.Top + currentBounds.Height / 2
                : workingArea.Top + workingArea.Height / 2;
            int left = resolvedEdge == PanelDockEdge.Left
                ? workingArea.Left
                : workingArea.Right - targetSize.Width;
            Rectangle docked = new Rectangle(
                left,
                centerY - targetSize.Height / 2,
                targetSize.Width,
                targetSize.Height);
            return Clamp(docked, workingArea);
        }

        public static Rectangle GetDockBounds(
            Rectangle workingArea,
            Size targetSize,
            PanelDockEdge edge)
        {
            Rectangle centeredReference = new Rectangle(
                workingArea.Left + workingArea.Width / 2,
                workingArea.Top + workingArea.Height / 2,
                0,
                0);
            return GetDockBounds(centeredReference, workingArea, targetSize, edge);
        }
    }
}
