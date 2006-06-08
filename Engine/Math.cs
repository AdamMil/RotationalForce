using System;
using System.Diagnostics;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

public enum InterpolationMode : byte
{
  /// <summary>The value morphs at a constant rate towards its target.</summary>
  Linear,
  /// <summary>The beginning and end of the morph are quite a bit slower than the middle.</summary>
  Sigmoid,
  /// <summary>The beginning and end of the morph are much slower than the middle.</summary>
  FastSigmoid,
}

public static class EngineMath
{
  public const double Epsilon = 0.0001;

  [Conditional("DEBUG")]
  public static void AssertValidFloat(double value)
  {
    Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value), "Not a valid floating point number.");
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

  // TODO: these can probably be optimized by only interpolating values that aren't already equal
  #region Interpolate
  public static Rectangle Interpolate(ref Rectangle start, ref Rectangle end, double delta, InterpolationMode mode)
  {
    delta = CalculateDelta(delta, mode);
    return new Rectangle(Interpolate(start.X, end.X, delta), Interpolate(start.Y, end.Y, delta),
                         Interpolate(start.Width, end.Width, delta), Interpolate(start.Height, end.Height, delta));
  }

  public static Point Interpolate(ref Point start, ref Point end, double delta, InterpolationMode mode)
  {
    delta = CalculateDelta(delta, mode);
    return new Point(Interpolate(start.X, end.X, delta, mode), Interpolate(start.Y, end.Y, delta, mode));
  }

  public static Vector Interpolate(ref Vector start, ref Vector end, double delta, InterpolationMode mode)
  {
    delta = CalculateDelta(delta, mode);
    return new Vector(Interpolate(start.X, end.X, delta, mode), Interpolate(start.Y, end.Y, delta, mode));
  }

  public static double Interpolate(double start, double end, double delta, InterpolationMode mode)
  {
    return Interpolate(start, end, CalculateDelta(delta, mode));
  }

  static double Interpolate(double start, double end, double delta)
  {
    return start + (end-start)*delta; // linear interpolatation
  }
  
  static double CalculateDelta(double linearDelta, InterpolationMode mode)
  {
    if(linearDelta < 0) return 0;
    else if(linearDelta >= 1) return 1;

    if(mode != InterpolationMode.Linear) // Sigmoid-ish interpolation
    {
      linearDelta -= 0.5; // shift delta to be centered around 0 rather than 0.5

      // apply exponentation
      if(mode == InterpolationMode.FastSigmoid)
      {
        linearDelta = Math.Pow(Math.E, linearDelta * -30);
      }
      else // "normal" Sigmoid
      {
        linearDelta = Math.Pow(2, linearDelta * -20);
      }

      linearDelta = 1/(1+linearDelta); // squeeze the value back into 0-1
    }
    
    return linearDelta;
  }
  #endregion

  public static void Swap<T>(ref T a, ref T b)
  {
    T tmp = a;
    a = b;
    b = tmp;
  }
}

} // namespace RotationalForce.Engine