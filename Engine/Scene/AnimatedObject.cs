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
  /// <summary>The offset within the frame, in seconds.</summary>
  public double Offset;
  /// <summary>The zero-based index of the frame we're currently on.</summary>
  public int Frame;
  /// <summary>Indicates whether the animation has completed. If the animation loops, this indicates that the animation
  /// has begun to loop. The flag can be cleared at any time and checked again for another completion notification.
  /// </summary>
  public bool Complete;
}

#region Animation
[ResourceKey]
public abstract class Animation : Resource
{
  protected Animation(string resourceName)
  {
    this.Name = resourceName;
  }

  protected Animation(ISerializable dummy) { } // special constructor used during deserialization

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
  /// <summary>Gets the animation object referenced by <see cref="AnimationName"/>.</summary>
  [Browsable(false)]
  public Animation Animation
  {
    get { return animationHandle == null ? null : animationHandle.Resource; }
  }

  /// <summary>Gets or sets the name of the animation displayed in this animated object.</summary>
  [Category("Animation")]
  [Description("The name of the animation resource displayed in this animated object.")]
  [DefaultValue(null)]
  public string AnimationName
  {
    get { return animationName; }
    set
    {
      if(!string.Equals(value, animationName, System.StringComparison.Ordinal))
      {
        animationName = value;

        if(string.IsNullOrEmpty(animationName))
        {
          animationHandle = null;
        }
        else
        {
          animationHandle = Engine.GetResource<Animation>(animationName);
        }
      }
    }
  }

  /// <summary>Gets or sets the index of the animation frame that will be displayed within the object. If set to -1,
  /// a random animation frame will be chosen the next time the animation is advanced.
  /// </summary>
  [Category("Animation")]
  [Description("The index of the animation frame that is currently displayed within the object. If set to -1, a "+
    "random animation frame will be chosen the next time the animation is advanced.")]
  [DefaultValue(0)]
  public int AnimationFrame
  {
    get { return data.Frame; }
    set
    {
      if(value < -1) throw new ArgumentOutOfRangeException("Animation frame cannot be less than -1.");
      data.Frame = value;
    }
  }

  /// <summary>Gets or sets the offset into the current animation frame, in seconds.</summary>
  [Category("Animation")]
  [Description("The offset into the current animation frame, in seconds.")]
  [DefaultValue(0.0)]
  public double FrameOffset
  {
    get { return data.Offset; }
    set
    {
      if(value < 0) throw new ArgumentOutOfRangeException("Frame offset cannot be negative.");
      EngineMath.AssertValidFloat(value);
      data.Offset = value;
    }
  }

  [Category("Animation")]
  [Description("The speed of this animation, expressed as a multiple of the animation's normal speed.")]
  [DefaultValue(1.0)]
  public double AnimationSpeed
  {
    get { return animationSpeed; }
    set
    {
      if(value < 0) throw new ArgumentOutOfRangeException("Animation speed cannot be negative.");
      EngineMath.AssertValidFloat(value);
      animationSpeed = value;
    }
  }
  
  /// <summary>Gets or sets whether the animation is paused.</summary>
  [Category("Animation")]
  [Description("Whether the animation is paused.")]
  [DefaultValue(false)]
  public bool AnimationPaused
  {
    get { return paused; }
    set { paused = value; }
  }

  protected override void Deserialize(DeserializationStore store)
  {
    base.Deserialize(store);

    if(!string.IsNullOrEmpty(animationName)) // reload the animation handle if we had one before
    {
      animationHandle = Engine.GetResource<Animation>(animationName);
    }
  }

  protected override void RenderContent()
  {
    if(Animation != null)
    {
      Animation.Render(ref data);
    }
    else
    {
      base.RenderContent(); // use default rendering if there's no animation set
    }
  }

  protected internal override void Simulate(double timeDelta)
  {
    base.Simulate(timeDelta);

    if(!paused && Animation != null)
    {
      Animation.Simulate(ref data, timeDelta * animationSpeed);
    }
  }

  /// <summary>The object's current animation.</summary>
  [NonSerialized] ResourceHandle<Animation> animationHandle;
  /// <summary>The name of the object's animation.</summary>
  string animationName;
  /// <summary>An object that contains our state within the animation.</summary>
  AnimationData data;
  /// <summary>The object's animation speed, expressed as a multiple.</summary>
  double animationSpeed = 1.0;
  /// <summary>Whether the animation is currently paused.</summary>
  bool paused;
}
#endregion

} // namespace RotationalForce.Engine