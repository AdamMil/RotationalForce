using System;
using System.Collections.Generic;
using System.ComponentModel;
using GameLib.Input;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;
using GameLib.Interop.OpenGL;
using Color = System.Drawing.Color;

// TODO: replace some rotatation code with code that checks for 90/180/270 degrees and does perfect rotations [with no
// floating point error due to conversion to radians])

namespace RotationalForce.Engine
{

/// <summary>Determines the type of shading to use.</summary>
public enum ShadeModel : byte
{
  Flat, Smooth
}

#region Blending-related types
/// <summary>An enum containing acceptable source blending modes.</summary>
public enum SourceBlend : uint
{
  /// <summary>Use the blend mode of the parent object.</summary>
  Default           = 0xffffffff,
  Zero              = GL.GL_ZERO,
  One               = GL.GL_ONE,
  DestColor         = GL.GL_DST_COLOR,
  OneMinusDestColor = GL.GL_ONE_MINUS_DST_COLOR,
  SrcAlpha          = GL.GL_SRC_ALPHA,
  OneMinusSrcAlpha  = GL.GL_ONE_MINUS_SRC_ALPHA,
  DestAlpha         = GL.GL_DST_ALPHA,
  OneMinusDestAlpha = GL.GL_ONE_MINUS_DST_ALPHA,
  SrcAlphaSaturate  = GL.GL_SRC_ALPHA_SATURATE,
}

/// <summary>An enum containing acceptable destination blending modes.</summary>
public enum DestinationBlend : uint
{
  /// <summary>Use the blend mode of the parent object.</summary>
  Default           = 0xffffffff,
  Zero              = GL.GL_ZERO,
  One               = GL.GL_ONE,
  SrcColor          = GL.GL_SRC_COLOR,
  OneMinusSrcColor  = GL.GL_ONE_MINUS_SRC_COLOR,
  SrcAlpha          = GL.GL_SRC_ALPHA,
  OneMinusSrcAlpha  = GL.GL_ONE_MINUS_SRC_ALPHA,
  DestAlpha         = GL.GL_DST_ALPHA,
  OneMinusDestAlpha = GL.GL_ONE_MINUS_DST_ALPHA
}
#endregion

#region Collision-related types
/// <summary>Describes the shape of an object's collision area.</summary>
public enum CollisionArea
{
  None, Rectangular, Circular, Polygonal,
}

/// <summary>Determines what will happen to an object when it collides with something.</summary>
public enum CollisionResponse : byte
{
  /// <summary>Nothing happens, although the collision callbacks will still be called.</summary>
  None,
  /// <summary>
  /// The object will bounce off of the object it hit, meaning that one or both velocity components will be reversed.
  /// </summary>
  Bounce,
  /// <summary>The object will clamp against the surface that it hit, and any non-parallel velocity will be cancelled.
  /// </summary>
  Clamp,
  /// <summary>The object will come to rest.</summary>
  Stop,
  /// <summary>The object will be deleted.</summary>
  Kill
}

/// <param name="sender">The object under consideration. For the <see cref="CollisionObject.Hit"/> event, this is
/// the object that has hit <paramref name="other"/>. For the <see cref="CollisionObject.HitBy"/> event, this is the
/// object that has been hit by <paramref name="other"/>.
/// </param>
/// <param name="other">The other object in the collision. For the <see cref="CollisionObject.Hit"/> event, this is
/// the object that has been hit by <paramref name="sender"/>. For the <see cref="CollisionObject.HitBy"/> event,
/// this is the object that hit the <paramref name="sender"/>.
/// </param>
public delegate void CollisionEventHandler(SceneObject sender, SceneObject other);

/// <summary>This method is called to determine whether two objects have collided.</summary>
/// <returns>True if they have collided and false if not.</returns>
public delegate bool CustomCollisionDetector(SceneObject a, SceneObject b);
#endregion

public delegate void ClickEventHandler(SceneObject obj, MouseButton button, Point worldPosition);
public delegate void MouseMoveEventHandler(SceneObject obj, Point worldPosition);

public struct LinkPoint
{
  /// <summary>The offset of the link point from the center of the object, in local coordinates.</summary>
  /// <remarks>This value does not take into account the rotation of the object.</remarks>
  public Vector Offset;
  /// <summary>The position of the link point within the world, in world coordinates.</summary>
  /// <remarks>This value takes into account the rotation of the object.</remarks>
  public Point ScenePoint;
  /// <summary>The link point's ID.</summary>
  public int ID;
  /// <summary>The object that is mounted to this link point. This can be null.</summary>
  public SceneObject Object;
  /// <summary>True if the mounted object will be destroyed when the parent is destroyed.</summary>
  public bool ObjectOwned;
}

/// <summary>The base class of all renderable game objects.</summary>
public abstract class SceneObject : GameObject, ISerializable
{
  public SceneObject()
  {
    SetRectangularCollisionArea(-1, -1, 1, 1);
  }

  SceneObject(ISerializable dummy) { }

  #region Blending
  [Category("Rendering")]
  [Description(Strings.BlendingEnabled)]
  [DefaultValue(false)]
  public bool BlendingEnabled
  {
    get { return HasFlag(Flag.BlendEnabled); }
    set { SetFlag(Flag.BlendEnabled, value); }
  }

  [Category("Rendering")]
  [Description("Gets/sets the source blending mode of the object, if blending is enabled.")]
  [DefaultValue(SourceBlend.Default)]
  public SourceBlend SourceBlendMode
  {
    get { return sourceBlend; }
    set { sourceBlend = value; }
  }
  
  [Category("Rendering")]
  [Description("Gets/sets the destination blending mode of the object, if blending is enabled.")]
  [DefaultValue(DestinationBlend.Default)]
  public DestinationBlend DestinationBlendMode
  {
    get { return destBlend; }
    set { destBlend = value; }
  }

  public double GetBlendAlpha() { return color.A / 255.0; }

  public void SetBlendAlpha(double alpha)
  {
    EngineMath.AssertValidFloat(alpha);
    if(alpha < 0 || alpha > 1) throw new ArgumentOutOfRangeException("alpha", "Alpha value must be from 0 to 1");
    color = Color.FromArgb((int)(alpha * 255), color.R, color.G, color.B);
  }

  public void SetBlendingMode(SourceBlend source, DestinationBlend destination)
  {
    sourceBlend  = source;
    destBlend = destination;
  }
  #endregion
  
  #region Collision detection
  public event CollisionEventHandler Hit;
  public event CollisionEventHandler HitBy;
  
  /// <summary>Gets/sets a custom collision detection function. Note that custom collision detection will only be
  /// called after all other tests have passed.
  /// </summary>
  /// <value>A <see cref="CustomCollisionDetector"/> delegate, or null if there is no custom detector set.</value>
  [Browsable(false)]
  public CustomCollisionDetector CustomCollisionDetector
  {
    get { return collisionDetector; }
    set { collisionDetector = value; }
  }

  [Category("Collisions")]
  [Description("Indicates the type of collision area set for this object.")]
  public CollisionArea CollisionArea
  {
    get
    {
      return HasFlag(Flag.RectangleCollision) ? CollisionArea.Rectangular :
             HasFlag(Flag.CircleCollision)    ? CollisionArea.Circular    :
             HasFlag(Flag.PolygonCollision)   ? CollisionArea.Polygonal   :
             CollisionArea.None;
    }
  }

  [Category("Collisions")]
  [Description("Determines whether collision detection is enabled for this object.")]
  [DefaultValue(false)]
  public bool CollisionEnabled
  {
    get { return HasFlag(Flag.CollisionEnabled); }
    set { SetFlag(Flag.CollisionEnabled, value); }
  }

  [Category("Collisions")]
  [Description("A bitmask that determines the object groups considered for collision detection. This object can only "+
    "collide with objects in the given groups.")]
  [DefaultValue((uint)0xffffffff)]
  public uint CollisionGroups
  {
    get { return collisionGroups; }
    set { collisionGroups = value; }
  }
  
  [Category("Collisions")]
  [Description("A bitmask that determines the layers considered for collision detection. This object can only "+
    "collide with objects on the given layers.")]
  [DefaultValue((uint)0xffffffff)]
  public uint CollisionLayers
  {
    get { return collisionLayers; }
    set { collisionLayers = value; }
  }
  
  [Category("Collisions")]
  [Description("Determines what will happen when this object collides with another.")]
  [DefaultValue(CollisionResponse.Clamp)]
  public CollisionResponse CollisionResponse
  {
    get { return collisionResponse; }
    set { collisionResponse = value; }
  }

  [Category("Collisions")]
  [Description("Determines whether this object sends collisions to other objects. If set to false, other objects can "+
    "hit this one, but cannot be hit by this one.")]
  [DefaultValue(false)]
  public bool SendsCollisions
  {
    get { return HasFlag(Flag.SendsCollisions); }
    set { SetFlag(Flag.SendsCollisions, value); }
  }
  
  [Category("Collisions")]
  [Description("Determines whether this object receives collisions from other objects. If set to false, other "+
    "objects cannot hit this one, although they can still be hit by it.")]
  [DefaultValue(false)]
  public bool ReceivesCollisions
  {
    get { return HasFlag(Flag.ReceivesCollisions); }
    set { SetFlag(Flag.ReceivesCollisions, value); }
  }
  
  public object GetCollisionData()
  {
    switch(CollisionArea)
    {
      case CollisionArea.Circular:
        return new Circle(collisionX, collisionY, collisionRadius);
      case CollisionArea.Rectangular:
        return new Rectangle(collisionX, collisionY, collisionRight-collisionX, collisionBottom-collisionY);
      case CollisionArea.Polygonal:
        throw new NotImplementedException();
      default:
        return null;
    }
  }

  public void SetNullCollisionArea()
  {
    flags &= ~Flag.CollisionMask;
  }
  
  public void SetCircularCollisionArea(double centerX, double centerY, double radius)
  {
    EngineMath.AssertValidFloats(centerX, centerY, radius);

    if(radius < 0) throw new ArgumentOutOfRangeException("radius", "Radius cannot be negative");

    collisionX         = centerX;
    collisionY         = centerY;
    collisionRadius    = radius;
    collisionRadiusSqr = radius * radius;

    flags = (flags & ~Flag.CollisionMask) | Flag.CircleCollision;
  }
  
  public void SetRectangularCollisionArea(double x1, double y1, double x2, double y2)
  {
    EngineMath.AssertValidFloats(x1, y1, x2, y2);
    
    // swap values as necessary to ensure that collisionX < collisionRight, etc.
    if(x2 < x1) EngineMath.Swap(ref x1, ref x2);
    if(y2 < y1) EngineMath.Swap(ref y1, ref y2);

    collisionX = x1;
    collisionY = y1;
    collisionRight  = x2;
    collisionBottom = y2;    

    flags = (flags & ~Flag.CollisionMask) | Flag.RectangleCollision;
  }

  public void SetPolygonalCollisionArea(params Point[] points)
  {
    #if DEBUG
    foreach(Point point in points)
    {
      EngineMath.AssertValidFloats(point.X, point.Y);
    }
    #endif

    throw new NotImplementedException();
  }

  protected virtual void OnHit(SceneObject hit)
  {
    if(Hit != null) Hit(this, hit);
  }
  
  protected virtual void OnHitBy(SceneObject hitter)
  {
    if(HitBy != null) HitBy(this, hitter);
  }
  #endregion

  #region Coordinate conversion
  public Point LocalToScene(Point localPoint) { return LocalToScene(localPoint.X, localPoint.Y); }

  public Point LocalToScene(double localX, double localY)
  {
    EngineMath.AssertValidFloats(localX, localY);

    // negate coordinates according to our hflip and vflip flags
    if(HorizontalFlip) localX = -localX;
    if(VerticalFlip)   localY = -localY;

    // orient the point with our rotation
    Vector offset = new Vector(localX, localY);
    double rotation = EffectiveRotation;
    if(rotation != 0) offset.Rotate(rotation * MathConst.DegreesToRadians);

    // then offset it by our world position
    return position + LocalToScene(offset);
  }
  
  public Vector LocalToScene(Vector localSize)
  {
    EngineMath.AssertValidFloats(localSize.X, localSize.Y);
    return new Vector(localSize.X*0.5*Width, localSize.Y*0.5*Height);
  }

  public Point SceneToLocal(Point scenePoint) { return SceneToLocal(scenePoint.X, scenePoint.Y); }

  public Point SceneToLocal(double sceneX, double sceneY)
  {
    EngineMath.AssertValidFloats(sceneX, sceneY);

    // center the point around our origin, and scale it down by half our size (so it ends up between -1 and 1)
    Point localPoint = new Point((sceneX - position.X)*2/Width, (sceneY - position.Y)*2/Height);
    // then rotate it if necessary
    double rotation = EffectiveRotation;
    if(rotation != 0)
    {
      localPoint = new Vector(localPoint).Rotated(-rotation * MathConst.DegreesToRadians).ToPoint();
    }

    // negate coordinates according to our hflip and vflip flags
    if(HorizontalFlip) localPoint.X = -localPoint.X;
    if(VerticalFlip) localPoint.Y = -localPoint.Y;

    return localPoint;
  }
  
  public Vector SceneToLocal(Vector sceneSize)
  {
    EngineMath.AssertValidFloats(sceneSize.X, sceneSize.Y);
    return new Vector(sceneSize.X*2/Width, sceneSize.Y*2/Height);
  }
  #endregion

  #region Grouping
  [Category("Behavior")]
  [Description("A bitmask that determines the groups of which this object is a member.")]
  [DefaultValue(1)]
  public uint GroupMask
  {
    get { return groups; }
    set { groups = value; }
  }

  /// <summary>Adds this object to the given object group.</summary>
  /// <param name="group">The group number, from 0 to 31.</param>
  /// <remarks>Nothing occurs if the object is already a member of the group.</remarks>
  public void AddToGroup(int group)
  {
    ValidateGroup(group);
    groups |= (uint)(1<<group);
  }
  
  /// <summary>Returns true if this object is a member of the given object group.</summary>
  /// <param name="group">The group number, from 0 to 31.</param>
  public bool IsInGroup(int group)
  {
    ValidateGroup(group);
    return (groups & (uint)(1<<group)) != 0;
  }
  
  /// <summary>Removes this object from the given object group.</summary>
  /// <param name="group">The group number, from 0 to 31.</param>
  /// <remarks>Nothing occurs if the object is not a member of the group.</remarks>
  public void RemoveFromGroup(int group)
  {
    ValidateGroup(group);
    groups &= ~(uint)(1<<group);
  }
  
  /// <summary>Adds this object to, or removes this object from, a group.</summary>
  /// <param name="group">The group number, from 0 to 31.</param>
  /// <param name="inGroup">
  /// If true, the object will be added to the group. Otherwise, the object will be removed from the group.
  /// </param>
  public void SetGroupMembership(int group, bool inGroup)
  {
    if(inGroup) AddToGroup(group);
    else RemoveFromGroup(group);
  }

  /// <summary>Throws an exception if the group number is out of range.</summary>
  static void ValidateGroup(int group)
  {
    if(group < 0 || group > 31) throw new ArgumentOutOfRangeException("group", "Group must be from 0 to 31.");
  }
  #endregion

  #region Intersection and containment testing
  internal bool ContainedBy(Circle circle)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.Contains(ref circle, ref spatial.RotatedBounds);
    }
    else
    {
      return Math2D.Contains(ref circle, spatial.RotatedArea);
    }
  }

  internal bool ContainedBy(Rectangle rect)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.Contains(ref rect, ref spatial.RotatedBounds);
    }
    else
    {
      return Math2D.Contains(ref rect, spatial.RotatedArea);
    }
  }

  internal bool Contains(Point point)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.Contains(ref spatial.RotatedBounds, ref point);
    }
    else
    {
      return Math2D.Contains(spatial.RotatedArea, ref point);
    }
  }

  internal bool Intersects(Circle circle)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.Intersects(ref circle, ref spatial.RotatedBounds);
    }
    else
    {
      return Math2D.Intersects(ref circle, spatial.RotatedArea);
    }
  }

  internal bool Intersects(Line segment)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.SegmentIntersects(ref segment, ref spatial.RotatedBounds);
    }
    else
    {
      return Math2D.SegmentIntersects(ref segment, spatial.RotatedArea);
    }
  }

  internal bool Intersects(Rectangle rect)
  {
    UpdateSpatialInfo();

    if(spatial.Rotation != SpatialInfo.RotationType.Arbitrary)
    {
      return Math2D.Intersects(ref spatial.RotatedBounds, ref rect);
    }
    else
    {
      return Math2D.Intersects(ref rect, spatial.RotatedArea);
    }
  }
  #endregion

  #region Link points
  /// <summary>Creates a new link point given an offset in the object in local coordinates.</summary>
  /// <returns>The ID of the link point, which will be used to retrieve it later.</returns>
  public int AddLinkPoint(double localX, double localY)
  {
    return AddLinkPoint(localX, localY, null, false);
  }

  /// <summary>Removes all link points (but not mount points).</summary>
  public void ClearLinkPoints()
  {
    for(int i=0; i<numLinkPoints; )
    {
      // if it's a plain link point (not a mount point), remove it by overwriting it with the last link point
      if(linkPoints[i].Object == null)
      {
        linkPoints[i] = linkPoints[--numLinkPoints];
      }
      else // otherwise, it's a mount point, so skip over it
      {
        i++;
      }
    }
  }

  /// <summary>Gets the position of a link point (or mount point), in world coordinates.</summary>
  /// <param name="linkID">The ID of the link point to find.</param>
  public Point GetLinkPoint(int linkID)
  {
    int index = FindLinkPoint(linkID);
    if(index == -1) throw new ArgumentException("The link with linkID "+linkID+" could not be found.");
    UpdateSpatialInfo();
    return linkPoints[index].ScenePoint;
  }

  /// <summary>Gets an array of <see cref="LinkPoint"/> containing the object's link points.</summary>
  /// <remarks>The link points are copied upon return, so modifying them or the array will not produce any change in
  /// the object.
  /// </remarks>
  public LinkPoint[] GetLinkPoints()
  {
    LinkPoint[] links = new LinkPoint[numLinkPoints];
    if(numLinkPoints != 0)
    {
      UpdateSpatialInfo();
      Array.Copy(linkPoints, links, numLinkPoints);
    }
    return links;
  }

  /// <summary>Removes a link point, given its ID.</summary>
  /// <remarks>Nothing will happen if the link point does not exist.</remarks>
  public void RemoveLinkPoint(int linkID)
  {
    int index = FindLinkPoint(linkID);
    if(index != -1)
    {
      linkPoints[index] = linkPoints[--numLinkPoints]; // remove it by overwriting it with the last link in the list
    }
  }

  public void UpdateLinkPoint(int linkID, double localX, double localY)
  {
    EngineMath.AssertValidFloats(localX, localY);

    int index = FindLinkPoint(linkID);
    if(index == -1) throw new ArgumentException("The link with linkID "+linkID+" could not be found.");
    linkPoints[index].Offset     = new Vector(localX, localY);
    linkPoints[index].ScenePoint = LocalToScene(localX, localY);
  }

  int AddLinkPoint(double localX, double localY, SceneObject child, bool owned)
  {
    EngineMath.AssertValidFloats(localX, localY);

    int linkID = 0;
    for(int i=0; i<numLinkPoints; i++) // find the next link ID
    {
      if(linkPoints[i].ID >= linkID) linkID = linkPoints[i].ID + 1;
    }

    if(linkPoints == null || numLinkPoints == linkPoints.Length)
    {
      LinkPoint[] newLinks = new LinkPoint[numLinkPoints == 0 ? 4 : numLinkPoints*2];
      if(numLinkPoints != 0) Array.Copy(linkPoints, newLinks, numLinkPoints);
      linkPoints = newLinks;
    }

    linkPoints[numLinkPoints].ID          = linkID;
    linkPoints[numLinkPoints].Object      = child;
    linkPoints[numLinkPoints].ObjectOwned = owned;
    linkPoints[numLinkPoints].Offset      = new Vector(localX, localY);
    linkPoints[numLinkPoints].ScenePoint  = LocalToScene(localX, localY);
    numLinkPoints++;

    return linkID;
  }

  int FindLinkPoint(int linkID)
  {
    for(int i=0; i<numLinkPoints; i++)
    {
      if(linkPoints[i].ID == linkID) return i;
    }
    return -1;
  }
  #endregion

  #region Mounting
  /// <summary>Gets a value that indicates whether this object is mounted to another object.</summary>
  [Browsable(false)]
  public bool Mounted
  {
    get { return mountParent != null; }
  }
  
  /// <summary>Gets the object to which this object is mounted, or null if this object is not mounted.</summary>
  [Browsable(false)]
  public SceneObject MountParent
  {
    get { return mountParent; }
  }

  /// <summary>Mounts this object to another object.</summary>
  /// <param name="mountTo">The <see cref="SceneObject"/> to which this object will be mounted.</param>
  /// <param name="localX">The local X coordinate within <paramref name="mountTo"/> where this object will be mounted.</param>
  /// <param name="localY">The local Y coordinate within <paramref name="mountTo"/> where this object will be mounted.</param>
  /// <param name="owned">Whether this object will be owned by its parent. If true, this object will be deleted when
  /// <paramref name="mountTo"/> is deleted.
  /// </param>
  /// <param name="trackRotation">Whether this object will rotate when <paramref name="mountTo"/> rotates.</param>
  /// <param name="inheritProperties">Whether this object will inherit certain properties from
  /// <paramref name="mountTo"/>. Currently, the properties inherited are <see cref="HorizontalFlip"/>,
  /// <see cref="VerticalFlip"/>, and <see cref="Visible"/>.
  /// </param>
  /// <returns>Returns the ID of the link point within <see cref="mountTo"/> where this object will be mounted.</returns>
  public int Mount(SceneObject mountTo, double localX, double localY,
                   bool owned, bool trackRotation, bool inheritProperties)
  {
    EngineMath.AssertValidFloats(localX, localY);

    if(mountTo == null) throw new ArgumentNullException("Parent object is null.");

    if(Scene != null && Scene != mountTo.Scene)
    {
      throw new ArgumentException("Parent object belongs to a different scene.");
    }

    if(Mounted) Dismount();

    SetFlag(Flag.TrackRotation, trackRotation);
    SetFlag(Flag.InheritProperties, inheritProperties);
    Scene = mountTo.Scene;
    mountParent = mountTo; // this must be set after Scene because of the sanity checks Scene does
    InvalidateSpatialInfo();

    return mountTo.AddLinkPoint(localX, localY, this, owned);
  }
  
  /// <summary>Dismounts this object from its parent object.</summary>
  /// <remarks>This method has no effect if the object is not mounted.</remarks>
  public void Dismount()
  {
    if(Mounted)
    {
      mountParent.RemoveMount(this);

      SetFlag(Flag.InheritProperties|Flag.TrackRotation, false);
      InvalidateSpatialInfo(); // invalidate spatial info because our effective rotation, etc, may have changed
      mountParent = null;
    }
  }

  protected bool InheritProperties
  {
    get { return HasFlag(Flag.InheritProperties); }
  }
  #endregion

  #region Pointer events
  public event ClickEventHandler MouseUp;
  public event ClickEventHandler MouseDown;
  public event MouseMoveEventHandler MouseEnter;
  public event MouseMoveEventHandler MouseLeave;
  public event MouseMoveEventHandler MouseMove;
  #endregion

  #region Rotation
  /// <summary>Gets/sets the rotational velocity of the object, in degrees per second.</summary>
  [Category("Physics")]
  [Description("The rotational velocity of the object, in degrees per second.")]
  [DefaultValue(0)]
  public double AngularVelocity
  {
    get { return autoRotation; }
    set
    {
      EngineMath.AssertValidFloat(value);
      autoRotation = value;
    }
  }

  /// <summary>Gets/sets the object's rotation, in degrees.</summary>
  /// <value>The object's rotation, from 0 to 360 degrees, exclusive.</value>
  /// <remarks>When the rotation is set, it will be normalized to a value between 0 and 360 degrees, exclusive.
  /// If the object is mounted and set to track its parent's rotation, this value is the rotation in addition to the
  /// mount parent's rotation.
  /// </remarks>
  [Category("Spatial")]
  [Description("The rotation of the object, in degrees. If the object is mounted, this is the rotation in addition "+
    "to the parent's rotation.")]
  [DefaultValue(0)]
  public double Rotation
  {
    get { return rotation; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(value != rotation)
      {
        rotation = EngineMath.NormalizeAngle(value);
        InvalidateSpatialInfo();
      }
    }
  }
  
  /// <summary>Gets the object's rotation after taking into account additional rotation from object mounting.</summary>
  double EffectiveRotation
  {
    get { return HasFlag(Flag.TrackRotation) ? mountParent.EffectiveRotation + rotation : rotation; }
  }
  #endregion

  #region Size, position
  [Browsable(false)]
  public Rectangle Area
  {
    get
    { 
      UpdateSpatialInfo();
      return spatial.Area;
    }
  }

  [Category("Spatial")]
  [Description("The position of the object's center point, in scene coordinates.")]
  public Point Position
  {
    get { return position; }
    set
    { 
      if(value != position)
      {
        EngineMath.AssertValidFloats(value.X, value.Y);
        position = value;
        InvalidateSpatialInfo();
      }
    }
  }
  
  [Category("Spatial")]
  [Description("The size of the object, in scene coordinates.")]
  public Vector Size
  {
    get { return size; }
    set
    {
      if(value != size)
      {
        EngineMath.AssertValidFloats(value.X, value.Y);
        if(value.X < 0 || value.Y < 0)
        {
          throw new ArgumentOutOfRangeException("Scene objects cannot have a negative size.");
        }
        size = value;
        InvalidateSpatialInfo();
      }
    }
  }

  [Browsable(false)]
  public double X
  {
    get { return position.X; }
    set { Position = new Point(value, Y); }
  }
  
  [Browsable(false)]
  public double Y
  {
    get { return position.Y; }
    set { Position = new Point(X, value); }
  }

  [Browsable(false)]
  public double Width
  {
    get { return size.X; }
    set { Size = new Vector(value, Height); }
  }
  
  [Browsable(false)]
  public double Height
  {
    get { return size.Y; }
    set { Size = new Vector(Width, value); }
  }

  [Category("Spatial")]
  [Description("The object's layer, from 0 to 31. 0 is the topmost layer.")]
  [DefaultValue(0)]
  public int Layer
  {
    get { return layer; }
    set
    {
      if(value > 31) throw new ArgumentOutOfRangeException("Layer", "Layers can only be from 0-31.");
      layer = (byte)value;
    }
  }

  [Browsable(false)]
  public uint LayerMask
  {
    get { return (uint)1 << layer; }
  }

  public Polygon GetRotatedArea()
  {
    UpdateSpatialInfo();
    return spatial.RotatedArea;
  }

  public Rectangle GetRotatedAreaBounds()
  {
    UpdateSpatialInfo();
    return spatial.RotatedBounds;
  }

  public void SetBounds(double x, double y, double width, double height)
  {
    Position = new Point(x, y);
    Size     = new Vector(width, height);
  }

  public void SetPosition(double x, double y)
  {
    Position = new Point(x, y);
  }

  public void SetSize(double width, double height)
  {
    Size = new Vector(width, height);
  }
  #endregion

  #region Velocity, acceleration
  [Category("Physics")]
  [Description("The object's acceleration, in scene units per second per second.")]
  public Vector Acceleration
  {
    get { return acceleration; }
    set
    {
      EngineMath.AssertValidFloats(value.X, value.Y);
      acceleration = value;
    }
  }

  [Browsable(false)]
  public bool AtRest
  {
    get { return velocity.X == 0 && velocity.Y == 0; }
  }

  [Category("Physics")]
  [Description("The object's velocity, in scene units per second.")]
  public Vector Velocity
  {
    get { return velocity; }
    set
    {
      EngineMath.AssertValidFloats(value.X, value.Y);
      velocity = value;
    }
  }

  [Browsable(false)]
  public double VelocityX
  {
    get { return velocity.X; }
    set { Velocity = new Vector(value, VelocityY); } 
  }

  [Browsable(false)]
  public double VelocityY
  {
    get { return velocity.Y; }
    set { Velocity = new Vector(VelocityX, value); } 
  }

  public void AddVelocity(double xv, double yv)
  {
    Velocity += new Vector(xv, yv);
  }

  public void AddVelocity(Vector v)
  {
    Velocity += v;
  }

  public void AddVelocityPolar(double angle, double magnitude)
  {
    EngineMath.AssertValidFloats(angle, magnitude);
    if(magnitude != 0.0)
    {
      Velocity += new Vector(0, magnitude).Rotated(angle * MathConst.DegreesToRadians);
    }
  }

  public void SetAcceleration(double xv, double yv)
  {
    Acceleration = new Vector(xv, yv);
  }

  public void SetAccelerationPolar(double angle, double magnitude)
  {
    EngineMath.AssertValidFloats(angle, magnitude);

    Acceleration = magnitude == 0.0 ?
      new Vector() : new Vector(0, magnitude).Rotated(angle * MathConst.DegreesToRadians);
  }

  public void SetAtRest() { SetVelocity(0, 0); }

  public void SetVelocity(double xv, double yv)
  {
    Velocity = new Vector(xv, yv);
  }

  public void SetVelocityPolar(double angle, double magnitude)
  {
    EngineMath.AssertValidFloats(angle, magnitude);

    if(magnitude == 0.0)
    {
      velocity.X = velocity.Y = 0.0;
    }
    else
    {
      velocity = new Vector(0, magnitude).Rotated(angle * MathConst.DegreesToRadians);
    }
  }
  #endregion

  #region Lifetime, visibility, flipping, mobility, pickability, color...
  [Category("Rendering")]
  [Description("Gets/sets the base color of the object.")]
  public Color Color
  {
    get { return color; }
    set { color = value; }
  }

  /// <summary>Gets/sets whether the object will be flipped horizontally.</summary>
  /// <remarks>Flipping affects both the physics and the rendering of an object. If the object is mounted and set to
  /// inherit properties from the parent, the value of this property will mean that the object is flipped relative to
  /// the parent's own flip value.
  /// </remarks>
  [Category("Behavior")]
  [Description("Determines whether the object is flipped along the horizontal axis. This modifies not only "+
    "rendering, but also collision detection and physics.")]
  [DefaultValue(false)]
  public bool HorizontalFlip
  {
    get { return HasFlag(Flag.FlipHorizontal); }
    set { SetFlag(Flag.FlipHorizontal, value); }
  }

  /// <summary>Gets/sets whether the object will be flipped vertically.</summary>
  /// <remarks>Flipping affects both the physics and the rendering of an object. If the object is mounted and set to
  /// inherit properties from the parent, the value of this property will mean that the object is flipped relative to
  /// the parent's own flip value.
  [Category("Behavior")]
  [Description("Determines whether the object is flipped along the vertical axis. This modifies not only "+
    "rendering, but also collision detection and physics.")]
  [DefaultValue(false)]
  public bool VerticalFlip
  {
    get { return HasFlag(Flag.FlipVertical); }
    set { SetFlag(Flag.FlipVertical, value); }
  }

  [Category("Physics")]
  [Description("Determines whether the object is immobile. Immobile objects will not be moved by the engine.")]
  [DefaultValue(false)]
  public bool Immobile
  {
    get { return HasFlag(Flag.Immobile); }
    set { SetFlag(Flag.Immobile, value); }
  }

  /// <summary>Gets/sets the object's lifetime in seconds.</summary>
  /// <value>A non-negative floating point number. If set to zero (the default), the object will have an indefinite
  /// lifespan. Otherwise, when the lifetime drops to zero, the object will be auto-deleted.
  /// </value>
  /// <remarks>
  /// To delete an object immediately, don't set its lifetime to zero. Instead, use the <see cref="Delete"/> method.
  /// </remarks>
  [Category("Behavior")]
  [Description("Determines the object's lifetime. If set to a positive value, the object will be automatically "+
    "deleted after that many seconds.")]
  [DefaultValue(0)]
  public double Lifetime
  {
    get { return lifetime; }
    set
    {
      EngineMath.AssertValidFloat(lifetime);
      if(lifetime < 0) throw new ArgumentOutOfRangeException("Lifetime", "Lifetime cannot be negative");
      lifetime = value;
    }
  }
  
  /// <summary>Gets/sets whether the object will be returned by picking functions.</summary>
  [Description("Determines whether this object will be returned by picking functions.")]
  [DefaultValue(false)]
  public bool PickingAllowed
  {
    get { return HasFlag(Flag.PickingAllowed); }
    set { SetFlag(Flag.PickingAllowed, value); }
  }

  /// <summary>Gets/sets whether the object will be rendered in the scene.</summary>
  /// <remarks>This property can inherited from a mount parent.</remarks>
  [Category("Rendering")]
  [Description("Determines whether the object and its children will be rendered in the scene.")]
  [DefaultValue(false)]
  public bool Visible
  {
    get { return HasFlag(Flag.Visible); }
    set { SetFlag(Flag.Visible, value); }
  }

  /// <summary>Returns a value indicating whether the object has been marked for deletion.</summary>
  [Browsable(false)]
  public bool Dead
  {
    get { return HasFlag(Flag.Deleted); }
  }

  public override void Delete()
  {
    base.Delete();

    if(Mounted) Dismount();
    
    // dismount/delete child objects. we'll store the objects in a separate array because deleting the objects will
    // dismount them, and dismounting the objects will alter the link point array.
    List<SceneObject> children = null;
    
    // first add the owned objects
    for(int i=0; i<numLinkPoints; i++)
    {
      if(linkPoints[i].ObjectOwned)
      {
        if(children == null) children = new List<SceneObject>();
        children.Add(linkPoints[i].Object);
      }
    }
    
    // then mark how many owned objects there are
    int numOwned = children == null ? 0 : children.Count;
    
    // add the non-owned objects
    for(int i=0; i<numLinkPoints; i++)
    {
      if(linkPoints[i].Object != null && !linkPoints[i].ObjectOwned)
      {
        if(children == null) children = new List<SceneObject>();
        children.Add(linkPoints[i].Object);
      }
    }

    if(children != null) // if there were any child objects to be deleted/unmounted
    {
      for(int i=0; i<numOwned; i++) // delete the owned objects
      {
        children[i].Delete();
      }

      for(int i=numOwned; i<children.Count; i++) // and dismount the non-owned objects
      {
        children[i].Dismount();
      }
    }

    SetFlag(Flag.Deleted, true); // mark that we're dead
  }

  /// <summary>Gets the effective visibility, which takes into account the mount parent's visiblity.</summary>
  internal bool EffectiveVisibility
  {
    get { return Visible && (!InheritProperties || mountParent.EffectiveVisibility); }
  }
  
  /// <summary>Gets the effective horizontal flipping, which takes into account the mount parent's flip value.</summary>
  bool EffectiveHFlip
  {
    get
    {
      bool flip = HorizontalFlip;
      if(InheritProperties && mountParent.EffectiveHFlip) flip = !flip;
      return flip;
    }
  }

  /// <summary>Gets the effective vertical flipping, which takes into account the mount parent's flip value.</summary>
  bool EffectiveVFlip
  {
    get
    {
      bool flip = VerticalFlip;
      if(InheritProperties && mountParent.EffectiveVFlip) flip = !flip;
      return flip;
    }
  }
  #endregion

  #region Serialization
  protected override void Deserialize(DeserializationStore store)
  {
    InvalidateSpatialInfo(); // spatial information is not serialized, so we need to recalculate it
  }
  #endregion

  #region Internal stuff
  /// <summary>Gets/sets the scene of this object and all its mounted objects.</summary>
  internal Scene Scene
  {
    get { return scene; }
    set
    {
      if(value != scene)
      {
        if(Mounted && value != MountParent.Scene)
        {
          throw new InvalidOperationException("Can't put a mounted object in a different scene than its parent. "+
                                              "Either dismount the object first, or change the parent's scene.");
        }

        Scene oldScene = scene;
        scene = value; // we set this here so that the above check will succeed in child objects

        // change the scene of all mounted objects as well
        for(int i=0; i<numLinkPoints; i++)
        {
          if(linkPoints[i].Object != null)
          {
            if(oldScene != null) oldScene.RemoveObject(linkPoints[i].Object);
            if(value != null) value.AddObject(linkPoints[i].Object);
          }
        }
      }
    }
  }
  #endregion

  protected internal virtual void Render()
  {
    if(BlendingEnabled) // first set up the blending parameters
    {
      GL.glEnable(GL.GL_BLEND);
      GL.glBlendFunc((uint)(sourceBlend == SourceBlend.Default ? SourceBlend.One : sourceBlend),
                     (uint)(destBlend == DestinationBlend.Default ? DestinationBlend.Zero : destBlend));
    }

    GL.glColor(color); // we'll always set the blendColor, because some objects just use it as their color

    GL.glPushMatrix(); // we should be in ModelView mode
    GL.glTranslated(X, Y, 0); // translate us to the origin
    double rotation = EffectiveRotation;
    if(rotation != 0) GL.glRotated(rotation, 0, 0, 1); // rotate, if necessary
    GL.glScaled(Width  * (EffectiveHFlip ? -0.5 : 0.5),     // set up local coordinates (scale so that -1 to 1
                Height * (EffectiveVFlip ? -0.5 : 0.5), 1); // covers our area, including flipping)

    RenderContent(); // now allow the derived class to render the content

    GL.glPopMatrix(); // restore the old ModelView matrix

    if(BlendingEnabled) // and reset blending options, if necessary
    {
      GL.glDisable(GL.GL_BLEND);
    }
  }
  
  protected virtual void RenderContent()
  {
    // the default implementation just renders a white box
    GL.glColor(Color.White);
    GL.glBegin(GL.GL_QUADS);
      GL.glVertex2f(-1, -1);
      GL.glVertex2f( 1, -1);
      GL.glVertex2f( 1,  1);
      GL.glVertex2f(-1,  1);
    GL.glEnd();
  }

  protected internal virtual void Simulate(double timeDelta)
  {
    if(!HasFlag(Flag.Immobile))
    {
      Rotation += autoRotation * timeDelta;
      velocity += acceleration * timeDelta; // calculate our new velocity by applying acceleration
      Point newPos = Position + velocity * timeDelta; // find what our new position would be if there were no collisions

      /* TODO: do collision detection here */

      Position = newPos;
    }
  }

  protected internal virtual void PostSimulate() { }

  [Flags]
  enum Flag : uint
  {
    /// <summary>Determines whether collision detection is enabled.</summary>
    CollisionEnabled=0x01,
    /// <summary>Determines whether this object will trigger collision events in other objects, and whether the
    /// <see cref="Hit"/> event will be raised.
    /// </summary>
    SendsCollisions=0x02,
    /// <summary>Determines whether this object will receive collisions from other objects, and whether the
    /// <see cref="HitBy"/> event will be raised.
    /// </summary>
    ReceivesCollisions=0x04,
    /// <summary>No collision area will be defined. Collision detection will always fail, unless combined with custom
    /// collision detection.
    /// </summary>
    NoCollision=0, 
    /// <summary>A circular collision area will be used, defined by a point and a radius.</summary>
    CircleCollision=0x08,
    /// <summary>A rectangular collision area will be used.</summary>
    RectangleCollision=0x10,
    /// <summary>An arbitrary collision polygon will be used.</summary>
    PolygonCollision=0x18,
    /// <summary>A mask that can be applied to retrieve the collision type.</summary>
    CollisionMask=0x18,

    /// <summary>Determines whether the object and its children will be rendered in the scene.</summary>
    Visible=0x20,
    /// <summary>Determines whether GL blending is enabled.</summary>
    BlendEnabled=0x40,
    /// <summary>Determines whether the object is rendered flipped horizontally.</summary>
    FlipHorizontal=0x80,
    /// <summary>Determines whether the object is rendered flipped vertically.</summary>
    FlipVertical=0x100,
    /// <summary>Determines whether pointer (ie, mouse pointer) events (eg, OnMouseEnter, OnMouseLeave) will be fired.
    /// </summary>
    EnablePointerEvents=0x200,
    /// <summary>Determines whether the engine will never move this object. The object's location can be changed
    /// manually, of course.
    /// </summary>
    Immobile=0x400,
    /// <summary>This flag will be set when the object has been marked for deletion.</summary>
    Deleted=0x800,
    /// <summary>Determines whether the object will be returned by picking functions.</summary>
    PickingAllowed=0x1000,
    /// <summary>Determines if the criteria used to calculate cached spatial information (like the bounding box) have
    /// been changed.
    /// </summary>
    SpatialInfoDirty=0x2000,
    /// <summary>Determines if the criteria used to calculate link point position have changed.</summary>
    LinkPointsDirty=0x4000,
    /// <summary>Determines whether various properties will be inherited from the object's mount parent.</summary>
    InheritProperties=0x8000,
    /// <summary>Determines whether the object will track its mount parent's rotation.</summary>
    TrackRotation=0x10000,
  }

  struct SpatialInfo
  {
    public enum RotationType : byte { ZeroOr180, NinetyOr270, Arbitrary };

    public Rectangle Area, RotatedBounds;
    public Polygon RotatedArea;
    public RotationType Rotation;
  }

  /// <summary>Returns true if the given object flag is set.</summary>
  bool HasFlag(Flag flag) { return (flags & flag) != 0; }

  /// <summary>Sets or clears the given object flag.</summary>
  void SetFlag(Flag flag, bool value)
  {
    if(value) flags |= flag;
    else flags &= ~flag;
  }

  void InvalidateSpatialInfo() { InvalidateSpatialInfo(false); }
  void InvalidateSpatialInfo(bool onlyInvalidateLinks)
  {
    if(!onlyInvalidateLinks)
    {
      SetFlag(Flag.SpatialInfoDirty, true);
    }

    SetFlag(Flag.LinkPointsDirty, true);

    // recursively invalidate mounted objects' spatial info
    for(int i=0; i<numLinkPoints; i++)
    {
      if(linkPoints[i].Object != null)
      {
        // this is false because even if only this object's links moved, that still affects the total spatial
        // information of the mounted objects.
        linkPoints[i].Object.InvalidateSpatialInfo();
      }
    }
  }

  void RemoveMount(SceneObject child)
  {
    for(int i=0; i<numLinkPoints; i++)
    {
      if(linkPoints[i].Object == child)
      {
        linkPoints[i] = linkPoints[--numLinkPoints]; // remove it by overwriting it with the last link in the list
        return;
      }
    }

    throw new ArgumentException("Child is not mounted to this object.");
  }

  void UpdateSpatialInfo()
  {
    // if we're not part of a scene, return immediately.
    if(Scene == null) return;

    if(HasFlag(Flag.SpatialInfoDirty))
    {
      Vector halfSize = size*0.5;
      double rotation = EffectiveRotation;
      spatial.Area = new Rectangle(position.X-halfSize.X, position.Y-halfSize.Y, size.X, size.Y);
      spatial.Rotation =
        rotation == 0  || rotation == 180 ? SpatialInfo.RotationType.ZeroOr180   :
        rotation == 90 || rotation == 270 ? SpatialInfo.RotationType.NinetyOr270 :
        SpatialInfo.RotationType.Arbitrary;

      if(spatial.RotatedArea == null) spatial.RotatedArea = new Polygon(4);
      spatial.RotatedArea.Clear();
      spatial.RotatedArea.AddPoint(-halfSize.X, -halfSize.Y);
      spatial.RotatedArea.AddPoint( halfSize.X, -halfSize.Y);
      spatial.RotatedArea.AddPoint( halfSize.X,  halfSize.Y);
      spatial.RotatedArea.AddPoint(-halfSize.X,  halfSize.Y);
      if(spatial.Rotation != SpatialInfo.RotationType.ZeroOr180)
      {
        spatial.RotatedArea.Rotate(rotation * MathConst.DegreesToRadians);
      }

      spatial.RotatedArea.Offset(position.X, position.Y);

      if(spatial.Rotation != SpatialInfo.RotationType.ZeroOr180)
      {
        spatial.RotatedBounds = spatial.RotatedArea.GetBounds();
      }
      else
      {
        spatial.RotatedBounds = spatial.Area;
      }

      /* precalculate some things to speed collision detection, etc */

      SetFlag(Flag.SpatialInfoDirty, false);
    }

    if(HasFlag(Flag.LinkPointsDirty))
    {
      /* recalculate the world points associated with each link point. */
      Vector halfSize = size*0.5;

      // start out by setting all the world points to the local-space offsets
      for(int i=0; i<numLinkPoints; i++)
      {
        linkPoints[i].ScenePoint = linkPoints[i].Offset.ToPoint();
      }

      // then, orient rotate the localspace offsets to match our orientation
      double rotation = EffectiveRotation;
      if(rotation != 0)
      {
        double sin, cos;
        GLMath.GetRotationFactors(rotation * MathConst.DegreesToRadians, out sin, out cos);

        // finish recalculating the world points associated with each link point
        for(int i=0; i<numLinkPoints; i++)
        {
          // then orient it with our rotation
          Math2D.Rotate(ref linkPoints[i].ScenePoint, sin, cos);
        }
      }

      for(int i=0; i<numLinkPoints; i++)
      {
        // finally, scale by our size and offset by our position
        linkPoints[i].ScenePoint = new Point(linkPoints[i].ScenePoint.X*halfSize.X + position.X,
                                             linkPoints[i].ScenePoint.Y*halfSize.Y + position.Y);
      }
      
      SetFlag(Flag.LinkPointsDirty, false);
    }
  }

  /// <summary>The position of the circle's center if the type is Circle. The top-left corner of the collision
  /// rectangle if the type is Rectangle. The top-left corner of the polygon bounding box if type is Polygon.
  /// Values range from 0 to 1.
  /// </summary>
  double collisionX, collisionY;
  /// <summary>The radius of the collision circle if the type is Circle.</summary>
  double collisionRadius = 1.0;
  /// <summary>The radius of the collision circle, squared, if the type is Circle.</summary>
  double collisionRadiusSqr = 1.0;
  /// <summary>The bottom-right corner of the collision rectangle if the type is Rectangle. The bottom-right corner
  /// of the polygon bounding box if type is Polygon.
  /// </summary>
  double collisionRight=1.0, collisionBottom=1.0;
  /// <summary>A bitmask containing the layers on which this object can collide with other objects.</summary>
  uint collisionLayers = 0xffffffff;
  /// <summary>A bitmask containing the objects groups in which this object can collide with other objects.</summary>
  uint collisionGroups = 0xffffffff;

  /// <summary>The object's remaining lifetime. If zero, the object has infinite life. Otherwise, when it drops to
  /// zero, the object is deleted.
  /// </summary>
  double lifetime;

  /// <summary>The object's color.</summary>
  Color color = Color.White;
  /// <summary>The position of the object, in world units, within the parent object.</summary>
  Point position;
  /// <summary>The size of the object, in world units.</summary>
  Vector size = new Vector(10, 10);
  /// <summary>The velocity of the object, in world units per second.</summary>
  Vector velocity;
  /// <summary>The acceleration of the object, in world units per second per second.</summary>
  Vector acceleration;
  /// <summary>The rotation of the object, in degrees.</summary>
  double rotation;
  /// <summary>The rotational velocity of the object, in degrees per second.</summary>
  double autoRotation;
  /// <summary>Cached spatial information.</summary>
  [NonSerialized] SpatialInfo spatial;
  /// <summary>The object's link points (or null if the object contains no link points).</summary>
  LinkPoint[] linkPoints;
  /// <summary>The number of link points contained in <see cref="linkPoints"/>.</summary>
  int numLinkPoints;
  /// <summary>The scene containing this object (or null if the object is not part of a scene).</summary>
  [NonSerialized] Scene scene;
  /// <summary>The object to which this object is mounted.</summary>
  SceneObject mountParent;
  /// <summary>A bitfield that identifies the groups to which this object belongs.</summary>
  uint groups = 1;
  /// <summary>The type of GL source blending to use.</summary>
  SourceBlend sourceBlend = SourceBlend.Default;
  /// <summary>The type of GL destination blending to use.</summary>
  DestinationBlend destBlend = DestinationBlend.Default;
  /// <summary>A custom collision detector, or null if one is not defined.</summary>
  CustomCollisionDetector collisionDetector;
  /// <summary>A collection of several object flags.</summary>
  Flag flags = Flag.Visible | Flag.SpatialInfoDirty;
  /// <summary>Determines how the object will react when it collides with something.</summary>
  CollisionResponse collisionResponse = CollisionResponse.Clamp;
  /// <summary>A number (0-31) that determines which layer this object is on.</summary>
  byte layer;
}

} // namespace RotationalForce.Engine