using System;

namespace RotationalForce.Engine.Design
{

[AttributeUsage(AttributeTargets.Property)]
public class RangeAttribute : Attribute
{
  public RangeAttribute(double min, double max)
  {
    Minimum = min;
    Maximum = max;
  }

  public readonly double Minimum, Maximum;
}

} // using RotationForce.Engine.Design