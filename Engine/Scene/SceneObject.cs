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

public class RectangleObject : SceneObject
{
  public RectangleObject()
  {
    BlendingEnabled = true;
    SetBlendingMode(SourceBlend.SrcAlpha, DestinationBlend.OneMinusSrcAlpha);
  }

  protected override void RenderContent()
  {
    if(BlendingEnabled)
    {
      GL.glEnable(GL.GL_LINE_SMOOTH);
    }

    GL.glBegin(GL.GL_LINE_LOOP);
      GL.glVertex2f(-1f, -1f);
      GL.glVertex2f(1f, -1f);
      GL.glVertex2f(1f, 1f);
      GL.glVertex2f(-1f, 1f);
    GL.glEnd();
    
    GL.glDisable(GL.GL_LINE_SMOOTH);
  }
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
public abstract class SceneObject : GameObject
{
  public SceneObject()
  {
    SetRectangularCollisionArea(-1, -1, 1, 1);
  }

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
  [Description("Gets/sets the blending color of the object. Some objects simply use this as their color.")]
  public Color BlendColor
  {
    get { return blendColor; }
    set { blendColor = value; }
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

  public double GetBlendAlpha() { return blendColor.A / 255.0; }

  public void SetBlendAlpha(double alpha)
  {
    int intAlpha = (int)(alpha * 255);
    if(intAlpha < 0 || intAlpha > 255)
      throw new ArgumentOutOfRangeException("alpha", alpha, "Alpha value must be from 0 to 1");
    blendColor = Color.FromArgb(intAlpha, blendColor.R, blendColor.G, blendColor.B);
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
    EngineMath.AssertValidFloat(centerX);
    EngineMath.AssertValidFloat(centerY);
    EngineMath.AssertValidFloat(radius);

    if(radius < 0) throw new ArgumentOutOfRangeException("radius", radius, "Radius cannot be negative");

    collisionX         = centerX;
    collisionY         = centerY;
    collisionRadius    = radius;
    collisionRadiusSqr = radius * radius;

    flags = (flags & ~Flag.CollisionMask) | Flag.CircleCollision;
  }
  
  public void SetRectangularCollisionArea(double left, double top, double right, double bottom)
  {
    EngineMath.AssertValidFloat(left);
    EngineMath.AssertValidFloat(top);
    EngineMath.AssertValidFloat(right);
    EngineMath.AssertValidFloat(bottom);
    
    // swap values as necessary to ensure that collisionX < collisionRight, etc.
    if(right < left) EngineMath.Swap(ref left, ref right);
    if(bottom < top) EngineMath.Swap(ref top, ref bottom);

    collisionX = left;
    collisionY = top;
    collisionRight  = right;
    collisionBottom = bottom;    

    flags = (flags & ~Flag.CollisionMask) | Flag.RectangleCollision;
  }

  public void SetPolygonalCollisionArea(params Point[] points)
  {
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
    // orient the point with our rotation
    Vector offset = new Vector(localX, localY);
    if(rotation != 0) offset.Rotate(rotation * MathConst.DegreesToRadians);

    // then scale it by half our size and offset it by our world position
    return new Point(position.X + offset.X*(size.X*0.5), position.Y + offset.Y*(size.Y*0.5));
  }

  public Point SceneToLocal(Point worldPoint) { return SceneToLocal(worldPoint.X, worldPoint.Y); }

  public Point SceneToLocal(double worldX, double worldY)
  {
    // center the point around our origin, and scale it down by half our size
    Point localPoint = new Point((worldX - position.X)*2/size.X, (worldY - position.Y)*2/size.Y);
    // then rotate it if necessary
    if(rotation != 0)
    {
      localPoint = new Vector(localPoint).Rotated(-rotation * MathConst.DegreesToRadians).ToPoint();
    }
    return localPoint;
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
    if(group < 0 || group > 31) throw new ArgumentOutOfRangeException("group", group, "Group must be from 0 to 31.");
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

    linkPoints[numLinkPoints].Offset = new Vector(localX, localY);
    linkPoints[numLinkPoints].ID = linkID;
    numLinkPoints++;
    
    SetFlag(Flag.LinkPointsDirty, true);

    return linkID;
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
  /// <returns></returns>
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
    int index = FindLinkPoint(linkID);
    if(index == -1) throw new ArgumentException("The link with linkID "+linkID+" could not be found.");
    linkPoints[index].Offset     = new Vector(localX, localY);
    linkPoints[index].ScenePoint = LocalToScene(localX, localY);
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
  public bool Mounted
  {
    get { return mountParent != null; }
  }

  public int Mount(SceneObject mountTo, double x, double y, bool owned)
  {
    throw new NotImplementedException();
  }
  
  public void Dismount()
  {
    throw new NotImplementedException();
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
  /// <remarks>When the rotation is set, it will be normalized to a value between 0 and 360 degrees, exclusive.</remarks>
  [Category("Spatial")]
  [Description("The rotation of the object, in degrees.")]
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
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
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
    set
    {
      EngineMath.AssertValidFloat(value.X);
      EngineMath.AssertValidFloat(value.Y);
      EngineMath.AssertValidFloat(value.Width);
      EngineMath.AssertValidFloat(value.Height);

      Vector halfSize = new Vector(value.Width*0.5, value.Height*0.5);
      Point  center   = new Point(value.X+halfSize.X, value.Y+halfSize.Y);
      if(size.X != value.Width || size.Y != value.Height || center != position)
      {
        position = center;
        size     = new Vector(value.Width, value.Height);
        SetFlag(Flag.SpatialInfoDirty, true);
      }
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
        EngineMath.AssertValidFloat(value.X);
        EngineMath.AssertValidFloat(value.Y);
        position = value;
        SetFlag(Flag.SpatialInfoDirty, true);
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
        EngineMath.AssertValidFloat(value.X);
        EngineMath.AssertValidFloat(value.Y);
        if(value.X < 0 || value.Y < 0)
        {
          throw new ArgumentOutOfRangeException("Scene objects cannot have a negative size.");
        }
        size = value;
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
  }

  [Browsable(false)]
  public double X
  {
    get { return position.X; }
    set
    {
      if(value != position.X)
      {
        EngineMath.AssertValidFloat(value);
        position.X = value;
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
  }
  
  [Browsable(false)]
  public double Y
  {
    get { return position.Y; }
    set
    {
      if(value != position.Y)
      {
        EngineMath.AssertValidFloat(value);
        position.Y = value;
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
  }

  [Browsable(false)]
  public double Width
  {
    get { return size.X; }
    set
    {
      if(value != size.X)
      {
        EngineMath.AssertValidFloat(value);
        if(value < 0) throw new ArgumentOutOfRangeException("Scene objects cannot have a negative size.");
        size.X = value;
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
  }
  
  [Browsable(false)]
  public double Height
  {
    get { return size.Y; }
    set
    {
      if(value != size.Y)
      {
        EngineMath.AssertValidFloat(value);
        if(value < 0) throw new ArgumentOutOfRangeException("Scene objects cannot have a negative size.");
        size.Y = value;
        SetFlag(Flag.SpatialInfoDirty, true);
      }
    }
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
    Area = new Rectangle(x, y, width, height);
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
      EngineMath.AssertValidFloat(value.X);
      EngineMath.AssertValidFloat(value.Y);
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
      EngineMath.AssertValidFloat(value.X);
      EngineMath.AssertValidFloat(value.Y);
      velocity = value;
    }
  }

  [Browsable(false)]
  public double VelocityX
  {
    get { return velocity.X; }
    set
    {
      EngineMath.AssertValidFloat(value);
      velocity.X = value;
    } 
  }

  [Browsable(false)]
  public double VelocityY
  {
    get { return velocity.Y; }
    set
    {
      EngineMath.AssertValidFloat(value);
      velocity.Y = value;
    } 
  }

  public void AddVelocity(double xv, double yv) { Velocity += new Vector(xv, yv); }
  public void AddVelocity(Vector v) { Velocity += v; }

  public void AddVelocityPolar(double angle, double magnitude)
  {
    EngineMath.AssertValidFloat(angle);
    EngineMath.AssertValidFloat(magnitude);

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
    EngineMath.AssertValidFloat(angle);
    EngineMath.AssertValidFloat(magnitude);

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
    EngineMath.AssertValidFloat(angle);
    EngineMath.AssertValidFloat(magnitude);

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

  #region Lifetime, visibility, flipping, mobility, pickability
  [Category("Behavior")]
  [Description("Determines whether the object is flipped along the horizontal axis. This modifies not only "+
    "rendering, but also collision detection and physics.")]
  [DefaultValue(false)]
  public bool HorizontalFlip
  {
    get { return HasFlag(Flag.FlipHorizontal); }
    set { SetFlag(Flag.FlipHorizontal, value); }
  }

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
      if(lifetime < 0) throw new ArgumentOutOfRangeException("Lifetime", value, "Lifetime cannot be negative");
      lifetime = value;
    }
  }
  
  /// <summary>Gets/sets whether the object will be returned by picking functions.</summary>
  [Category("Miscellaneous")]
  [Description("Determines whether this object will be returned by picking functions.")]
  [DefaultValue(false)]
  public bool PickingAllowed
  {
    get { return HasFlag(Flag.PickingAllowed); }
    set { SetFlag(Flag.PickingAllowed, value); }
  }

  /// <summary>Gets/sets whether the object and its children will be rendered in the scene.</summary>
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
    SetFlag(Flag.Deleted, true);
  }
  #endregion

  #region Internal stuff
  internal Scene Scene
  {
    get { return scene; }
    set { scene = value; }
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

    GL.glColor(blendColor); // we'll always set the blendColor, because some objects just use it as their color

    GL.glPushMatrix(); // we should be in ModelView mode
    GL.glTranslated(X, Y, 0); // translate us to the origin
    if(rotation != 0) GL.glRotated(rotation, 0, 0, 1); // rotate, if necessary
    GL.glScaled(Width  * (HorizontalFlip ? -0.5 : 0.5),     // set up local coordinates (scale so that -1 to 1
                Height * (VerticalFlip   ? -0.5 : 0.5), 1); // covers our area, including flipping)

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

      /* collision detection */
      Position = newPos;
    }
  }

  protected internal virtual void PostSimulate() { }

  [Flags]
  enum Flag : ushort
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

  void UpdateSpatialInfo()
  {
    // if we're not part of a scene, return immediately.
    if(Scene == null) return;

    if(HasFlag(Flag.SpatialInfoDirty))
    {
      Vector halfSize = size*0.5;
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
      SetFlag(Flag.LinkPointsDirty, true);
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
      if(rotation != 0)
      {
        double sin, cos;
        Math2D.GetRotationFactors(rotation * MathConst.DegreesToRadians, out sin, out cos);

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

  /// <summary>The object's blending color.</summary>
  /// <remarks>The default color is white.</remarks>
  Color blendColor = Color.White;
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
  SpatialInfo spatial;
  /// <summary>The object's link points (or null if the object contains no link points).</summary>
  LinkPoint[] linkPoints;
  /// <summary>The number of link points contained in <see cref="linkPoints"/>.</summary>
  int numLinkPoints;
  /// <summary>The scene containing this object (or null if the object is not part of a scene).</summary>
  Scene scene;
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