using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tobii.Research.Addons.Utility
{
    public static class Extensions
    {
        public static double Magnitude(float x, float y, float z)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        public static double Magnitude(this Point3D vec)
        {
            return Magnitude(vec.X, vec.Y, vec.Z);
        }

        public static double Magnitude(float x, float y)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }

        public static double Magnitude(this NormalizedPoint2D vec)
        {
            return Magnitude(vec.X, vec.Y);
        }

        public static Point3D Add(this Point3D me, Point3D other)
        {
            return new Point3D(me.X + other.X, me.Y + other.Y, me.Z + other.Z);
        }

        public static Point3D Sub(this Point3D me, Point3D other)
        {
            return new Point3D(me.X - other.X, me.Y - other.Y, me.Z - other.Z);
        }

        public static Point3D Mul(this Point3D me, double other)
        {
            return new Point3D(me.X * (float)other, me.Y * (float)other, me.Z * (float)other);
        }

        public static Point3D Div(this Point3D me, double other)
        {
            return new Point3D(me.X / (float)other, me.Y / (float)other, me.Z / (float)other);
        }

        public static Point3D Normalize(this Point3D vec)
        {
            var mag = (float)Magnitude(vec.X, vec.Y, vec.Z);
            return new Point3D(vec.X / mag, vec.Y / mag, vec.Z / mag);
        }

        public static Point3D NormalizedDirection(this Point3D start, Point3D end)
        {
            return Normalize(Direction(start, end));
        }

        public static Point3D Direction(this Point3D start, Point3D end)
        {
            var x = end.X - start.X;
            var y = end.Y - start.Y;
            var z = end.Z - start.Z;
            return new Point3D(x, y, z);
        }

        public static double DotProduct(this Point3D me, Point3D other)
        {
            return me.X * other.X + me.Y * other.Y + me.Z * other.Z;
        }

        private static double Clamp(this double me, double low = -1d, double high = 1d)
        {
            me = Math.Min(high, me);
            me = Math.Max(low, me);
            return me;
        }

        public static double Angle(this Point3D me, Point3D other)
        {
            return ((Math.Acos((me.DotProduct(other) / (me.Magnitude() * other.Magnitude())).Clamp()) * 180 / Math.PI) + 360) % 360;
        }

        public static double MeanAngle(this double[] angles)
        {
            var x = angles.Sum(a => Math.Cos(a * Math.PI / 180)) / angles.Length;
            var y = angles.Sum(a => Math.Sin(a * Math.PI / 180)) / angles.Length;
            return Math.Atan2(y, x) * 180.0 / Math.PI;
        }

        public static Point3D Average(this Queue<GazeDataEventArgs> queue, Func<GazeDataEventArgs, Point3D> selector)
        {
            var x = 0.0;
            var y = 0.0;
            var z = 0.0;

            foreach (var item in queue)
            {
                var p = selector(item);
                x += p.X;
                y += p.Y;
                z += p.Z;
            }

            return new Point3D((float)(x / queue.Count), (float)(y / queue.Count), (float)(z / queue.Count));
        }

        public static Point3D NormalizedPoint2DToPoint3D(this NormalizedPoint2D point2D, DisplayArea displayArea)
        {
            var dx = displayArea.TopRight.Sub(displayArea.TopLeft).Mul(point2D.X);
            var dy = displayArea.BottomLeft.Sub(displayArea.TopLeft).Mul(point2D.Y);
            return displayArea.TopLeft.Add(dx.Add(dy));
        }

        public static double RootMeanSquare(this Queue<GazeDataEventArgs> queue, Func<GazeDataEventArgs, EyeData> selector)
        {
            if (queue.Count < 2)
            {
                throw new ArgumentException("Can't calculate RMS on queue of less than 2 items.");
            }

            var ret = 0.0;
            var array = queue.ToArray();

            for (int i = 0; i < array.Length - 1; i++)
            {
                var curr = selector(array[i]);
                var next = selector(array[i + 1]);
                var dirCurr = curr.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(curr.GazePoint.PositionInUserCoordinates);
                var dirNext = next.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(next.GazePoint.PositionInUserCoordinates);
                ret += Math.Pow(dirCurr.Angle(dirNext), 2);
            }

            return Math.Sqrt(ret / (array.Length - 1));
        }
    }

    internal class TimeKeeper
    {
        private readonly object _lock = new object();
        private Stopwatch _stopwatch = new Stopwatch();
        private int _timeoutMS;

        public bool TimedOut
        {
            get
            {
                lock (_lock)
                {
                    return _stopwatch.ElapsedMilliseconds >= _timeoutMS;
                }
            }
        }

        public void Restart()
        {
            lock (_lock)
            {
                _stopwatch.Reset();
                _stopwatch.Start();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopwatch.Stop();
            }
        }

        public TimeKeeper(int timeoutMS)
        {
            _timeoutMS = timeoutMS;
        }
    }
}