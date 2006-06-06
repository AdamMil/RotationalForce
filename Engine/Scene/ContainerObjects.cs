using System;
using System.Collections.Generic;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

#region VisibleContainer
/// <summary>A renderable object that contains other renderable objects.</summary>
public abstract class VisibleContainer : SceneObject
{
  List<SceneObject> objects;
}
#endregion

#region ScrollingContainer
/// <summary>A container object that can scroll its content.</summary>
public abstract class ScrollingContainer : VisibleContainer
{
  /// <summary>A set of flags that control the directions in which the container scrolls.</summary>
  [Flags]
  public enum Mode
  {
    /// <summary>The container will not repeat its content.</summary>
    NoRepeat=0,
    /// <summary>The container will repeat its content vertically.</summary>
    RepeatVertical=1,
    /// <summary>The container will repeat its contetn horizontally.</summary>
    RepeatHorizontal=2
  }
  
  Mode tileMode;

  /// <summary>The offset into the container where rendering starts.</summary>
  /// <value>The offset alters the rendering such that if the offset is 5,5, the content of the container starting at
  /// 5,5 will be rendered at the origin.
  /// </value>
  Point offset;
}
#endregion

} // namespace RotationalForce.Engine