using System;
using System.ComponentModel;

namespace RotationalForce.Engine
{

/// <summary>Determines how an animation loops.</summary>
public enum LoopType : byte
{
  /// <summary>The animation does not loop, and stops at the end.</summary>
  NoLoop,
  /// <summary>The animation restarts from the beginning after it reaches the end.</summary>
  Forward,
  /// <summary>The animation plays backwards and restarts at the end after reaching the beginning.</summary>
  Reverse,
  /// <summary>The animation plays forwards and backwards, changing direction when it reaches the beginning or end.</summary>
  PingPong
}

public struct AnimationData
{
  /// <summary>The position within the frame, in seconds.</summary>
  public double Position;
  /// <summary>The zero-based index of the frame we're currently on.</summary>
  public int Frame;
  /// <summary>An object to use for storing additional data associated with the animation state.</summary>
  /// <remarks>The <see cref="Animation"/> object may use this as necessary to optimize the rendering or simulation.</remarks>
  public object ExtraData;
  /// <summary>Indicates whether the animation has completed.</summary>
  public bool Complete;
}

#region Animation
public abstract class Animation : UniqueObject
{
  [Category("Behavior")]
  [Description("Determines how the animation interpolates between frames.")]
  [DefaultValue(InterpolationMode.Linear)]
  public InterpolationMode Interpolation
  {
    get { return interpolation; }
    set { interpolation = value; }
  }

  [Category("Behavior")]
  [Description("Determines how the animation loops by default.")]
  [DefaultValue(LoopType.NoLoop)]
  public LoopType Looping
  {
    get { return looping; }
    set { looping = value; }
  }
  
  protected internal abstract void Render(ref AnimationData data);
  protected internal abstract void Simulate(ref AnimationData data, double timeDelta);

  InterpolationMode interpolation;
  LoopType looping;
}
#endregion

#region AnimationFrame
public abstract class AnimationFrame
{
  /// <summary>The length of time spent rendering this frame, in seconds, at the default animation speed.</summary>
  [Category("Behavior")]
  [Description("The length of time spent rendering this frame, in seconds, at the default animation speed.")]
  public double FrameTime
  {
    get { return frameTime; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(value < 0) throw new ArgumentOutOfRangeException("FrameTime", "FrameTime cannot be negative.");
      frameTime = value;
    }
  }

  /// <summary>The length of this frame, in seconds.</summary>
  double frameTime;
}
#endregion

#region AnimatedObject
public class AnimatedObject : SceneObject
{
  [Browsable(false)]
  public Animation Animation
  {
    get { return animation; }
    set
    {
      if(value != animation)
      {
        animation = value;
        data = new AnimationData(); // reset the animation data when the animation changes
      }
    }
  }

  protected override void RenderContent()
  {
    if(animation != null)
    {
      animation.Render(ref data);
    }
    else
    {
      base.RenderContent(); // use default rendering if there's no animation
    }
  }

  protected internal override void Simulate(double timeDelta)
  {
    base.Simulate(timeDelta);
    if(animation != null) animation.Simulate(ref data, timeDelta);
  }

  /// <summary>The object's current animation.</summary>
  Animation animation;
  /// <summary>An object that contains our state within the animation.</summary>
  AnimationData data;
  /// <summary>The object's animation speed, expressed as a multiple.</summary>
  double animationSpeed;
}
#endregion

} // namespace RotationalForce.Engine