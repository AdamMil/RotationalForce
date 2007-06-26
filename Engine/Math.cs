using System;
using System.Diagnostics;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

public struct CatmullInterpolator
{
  // catmull-rom interpolation is:
  // value(t) = value(t)   * (2delta^3 - 3delta^2 + 1) +
  //            value(t+1) * (3delta^2 - 2delta^3)     +
  //            tan(t)     * (delta^3 - 2delta^2 + delta) +
  //            tan(t+1)   * (delta^3 - delta^2)
  // where tan(t) calculates the tangent at the given value point. the tangent is effectively the average of the two
  // line slopes on either side of the sample point, where the slope is given as value difference / time difference.
  // tan(t) = ((value(t) - value(t-1)) / (time(t) - time(t-1)) +
  //           (value(t+1) - value(t)) / (time(t+1) - time(t))) / 2
  //
  // note that the use of value and time could be replaced with X and Y coordinates (interpolating X as Y changes),
  // or a vector and time (interpolating the vector as time changes, although you'd need a vector tangent), or whatever
  //
  // source: http://www.gamedev.net/reference/articles/article1497.asp
  // (although the tangent calculation in that article seems to be in error -- I've corrected it)
  // also: http://www.cubic.org/docs/hermite.htm  (haven't read this, but it looks useful)
  // note that Cubic interpolation is equivalent to Catmull interpolation where previous == start and end == next

  /// <summary>Creates a Catmull-Rom interpolator with four sample points and equal timing between the points.</summary>
  /// <remarks>The interpolation will occur from <paramref name="startValue"/> to <paramref name="endValue"/>.</remarks>
  public CatmullInterpolator(double prevValue, double startValue, double endValue, double nextValue)
    : this(prevValue, startValue, endValue, nextValue, 1, 1, 1) { }

  /// <summary>Creates a Catmull-Rom interpolator with four sample points and arbitrary distance between the points.</summary>
  /// <remarks>The interpolation will occur from <paramref name="startValue"/> to <paramref name="endValue"/>. The
  /// three distance parameters are the relative distances between the sample points used to construct the four sample
  /// values. The distance can be in time, space, or any other measure.
  /// </remarks>
  public CatmullInterpolator(double prevValue, double startValue, double endValue, double nextValue,
                             double prevStartDist, double startEndDist, double endNextDist)
  {
    double tanTmp = (endValue-startValue) / startEndDist;

    this.tanStart   = (((startValue - prevValue) / prevStartDist) + tanTmp) * 0.5;
    this.tanEnd     = (tanTmp + ((nextValue - endValue) / endNextDist)) * 0.5;
    this.startValue = startValue;
    this.endValue   = endValue;
  }

  /// <summary>Retrieves the start value passed to the constructor.</summary>
  public double StartValue
  {
    get { return startValue; }
  }

  /// <summary>Retrieves the end value passed to the constructor.</summary>
  public double EndValue
  {
    get { return endValue; }
  }

  public double Interpolate(double delta)
  {
    double deltaSquared = delta*delta;
    return Interpolate(delta, deltaSquared, deltaSquared*delta);
  }
  
  public double Interpolate(double delta, double deltaSquared, double deltaCubed)
  {
    return startValue * (2*deltaCubed - 3*deltaSquared + 1)   +
           endValue   * (3*deltaSquared - 2*deltaCubed)       +
           tanStart   * (deltaCubed - 2*deltaSquared + delta) +
           tanEnd     * (deltaCubed - deltaSquared);
  }

  double startValue, endValue, tanStart, tanEnd;
}

public enum InterpolationMode : byte
{
  /// <summary>The value morphs at a constant rate towards its target.</summary>
  Linear,
  /// <summary>The beginning and end of the morph are quite a bit slower than the middle. This interpolation mode,
  /// while it can be applied to animations, is best suited for camera interpolation.
  /// </summary>
  Sigmoid,
  /// <summary>The beginning and end of the morph are very much slower than the middle. This interpolation mode, while
  /// it can be applied to animations, is best suited for camera interpolation.
  /// </summary>
  FastSigmoid,
  /// <summary>Uses cubic spline interpolation. This produces smooth acceleration and deceleration near the endpoints.</summary>
  Cubic,
  /// <summary>Uses Catmull-Rom spline interpolation. For interpolation between only two data points (as is done with
  /// camera interpolation), this is equivalent to <see cref="Cubic"/> interpolation. This is the most CPU-intensive
  /// interpolation mode, but can produce very smooth animations.
  /// </summary>
  Catmull,
}

public static class EngineMath
{
  public const double Epsilon = 0.0001;

  [Conditional("DEBUG")]
  public static void AssertValidFloat(double value)
  {
    Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value), "Not a valid floating point number.");
  }

  [Conditional("DEBUG")]
  public static void AssertValidFloat(float value)
  {
    Debug.Assert(!float.IsInfinity(value) && !float.IsNaN(value), "Not a valid floating point number.");
  }

  [Conditional("DEBUG")]
  public static void AssertValidFloats(params double[] values)
  {
    for(int i=0; i<values.Length; i++)
    {
      AssertValidFloat(values[i]);
    }
  }

  public static int Clip(int value, int low, int high)
  {
    return value<low ? low : value>high ? high : value;
  }

  #region Equivalent
  public static bool Equivalent(double a, double b)
  {
    return Math.Abs(a-b) < Epsilon;
  }

  public static bool Equivalent(Point a, Point b)
  {
    return Equivalent(a.X, b.X) && Equivalent(a.Y, b.Y);
  }

  public static bool Equivalent(Vector a, Vector b)
  {
    return Equivalent(a.X, b.X) && Equivalent(a.Y, b.Y);
  }
  
  public static bool Equivalent(Rectangle a, Rectangle b)
  {
    return Equivalent(a.X, b.X) && Equivalent(a.Y, b.Y) &&
           Equivalent(a.Width, b.Width) && Equivalent(a.Height, b.Height);
  }
  #endregion

  public static Point GetCenterPoint(Rectangle rect)
  {
    return new Point(rect.X + rect.Width/2, rect.Y + rect.Height/2);
  }

  public static double CalculateLinearDelta(double linearDelta, InterpolationMode mode)
  {
    if(linearDelta < 0) return 0;
    else if(linearDelta >= 1) return 1;

    if(mode == InterpolationMode.Sigmoid || mode == InterpolationMode.FastSigmoid) // Sigmoid-ish interpolation
    {
      linearDelta -= 0.5; // shift delta to be centered around 0 rather than 0.5

      // apply exponentation
      if(mode == InterpolationMode.FastSigmoid)
      {
        linearDelta = Math.Pow(Math.E, linearDelta * -30);
      }
      else // normal "Sigmoid"
      {
        linearDelta = Math.Pow(2, linearDelta * -20);
      }

      linearDelta = 1/(1+linearDelta); // squeeze the value back into 0-1
    }
    else if(mode == InterpolationMode.Cubic || mode == InterpolationMode.Catmull)
    {
      // with interpolation between only two data points (as is the case here), catmull and cubic are equivalent, so
      // we'll just use cubic.
      // cubic interpolation is:
      // value = start * (2delta^3 - 3delta^2 + 1) + end * (3delta^2 - 2delta^3)
      // where delta is from 0 to 1, and start/end are the values interpolated between
      // source: http://www.gamedev.net/reference/articles/article1497.asp
      // in this case, start and end are known to be 0 and 1, so we can optimize
      double uSquared = linearDelta * linearDelta, uCubed = uSquared * linearDelta;
      return uSquared*3 - uCubed*2;
    }

    return linearDelta;
  }

  // TODO: these can possibly be optimized by only interpolating values that aren't already equal
  #region Interpolate
  public static Rectangle Interpolate(ref Rectangle start, ref Rectangle end, double delta, InterpolationMode mode)
  {
    delta = CalculateLinearDelta(delta, mode);
    return new Rectangle(Interpolate(start.X, end.X, delta), Interpolate(start.Y, end.Y, delta),
                         Interpolate(start.Width, end.Width, delta), Interpolate(start.Height, end.Height, delta));
  }

  public static Point Interpolate(ref Point start, ref Point end, double delta, InterpolationMode mode)
  {
    delta = CalculateLinearDelta(delta, mode);
    return new Point(Interpolate(start.X, end.X, delta, mode), Interpolate(start.Y, end.Y, delta, mode));
  }

  public static Vector Interpolate(ref Vector start, ref Vector end, double delta, InterpolationMode mode)
  {
    delta = CalculateLinearDelta(delta, mode);
    return new Vector(Interpolate(start.X, end.X, delta, mode), Interpolate(start.Y, end.Y, delta, mode));
  }

  public static double Interpolate(double start, double end, double delta, InterpolationMode mode)
  {
    return Interpolate(start, end, CalculateLinearDelta(delta, mode));
  }

  public static double Interpolate(double start, double end, double delta)
  {
    return start + (end-start)*delta; // linear interpolatation
  }
  #endregion

  public static double InterpolateNormalizedAngle(double start, double end, double delta, InterpolationMode mode)
  {
    delta = CalculateLinearDelta(delta, mode);

    if(Math.Abs(start - end) <= 180)
    {
      return Interpolate(start, end, delta);
    }
    else
    {
      double angle = start < end ? Interpolate(start, end-360, delta) : Interpolate(start-360, end, delta);
      return angle < 0 ? angle+360 : angle;
    }
  }

  public static double NormalizeAngle(double angle)
  {
    if(angle < 0)
    {
      do angle += 360; while(angle < 0);
    }
    else if(angle >= 360)
    {
      do angle -= 360; while(angle >= 360);
    }
    return angle;
  }

  public static void Swap<T>(ref T a, ref T b)
  {
    T tmp = a;
    a = b;
    b = tmp;
  }
}

} // namespace RotationalForce.Engine