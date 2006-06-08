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
public abstract class Animation
{
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

  LoopType looping;
}
#endregion

#region AnimatedObject
public abstract class AnimatedObject : SceneObject
{
  public Animation Animation
  {
    get { return animation; }
    set
    {
      if(value != animation)
      {
        if(value == null)
        {
          animation = null;
        }
        else
        {
          ValidateAnimation(value);
          animation = value;
        }

        data = new AnimationData(); // reset the animation data when the animation changes
      }
    }
  }

  protected internal override void Render()
  {
    if(animation != null) base.Render(); // only render if we have an animtion configured
  }

  protected override void RenderContent()
  {
    animation.Render(ref data); // no null check because that's handled in Render()
  }

  protected internal override void Simulate(double timeDelta)
  {
    base.Simulate(timeDelta);
    if(animation != null) animation.Simulate(ref data, timeDelta);
  }

  protected abstract void ValidateAnimation(Animation animation);

  /// <summary>The object's current animation.</summary>
  Animation animation;
  /// <summary>An object that contains our state within the animation.</summary>
  AnimationData data;
  /// <summary>The object's animation speed, expressed as a multiple.</summary>
  double animationSpeed;
}
#endregion

} // namespace RotationalForce.Engine