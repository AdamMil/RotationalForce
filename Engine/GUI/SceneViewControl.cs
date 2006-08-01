using System;
using System.Collections.Generic;
using System.ComponentModel;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
using SPoint = System.Drawing.Point;
using SRectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace RotationalForce.Engine
{

// TODO: allow interpolation of the camera zoom while the camera is mounted
// TODO: camera rotation!

public delegate void SceneViewEventHandler(SceneViewControl view);

/// <summary>Determines the axis to which the camera size will apply.</summary>
/// <remarks>The camera area is defined by rectangle with a width or height given by the camera's size. Whether the
/// width or the height is equal to the camera size is determined by the camera axis.
/// </remarks>
public enum CameraAxis : byte
{
  /// <summary>Use the major (longer) axis. This is the default.</summary>
  Major=0, // these values correspond to SceneViewControl.Flag values
  /// <summary>Use the minor (shorter) axis.</summary>
  Minor=0x10,
  /// <summary>Use the X axis.</summary>
  X=0x20,
  /// <summary>Use the Y axis.</summary>
  Y=0x30
}

public class SceneViewControl : GuiControl, ITicker, IDisposable
{
  public SceneViewControl()
  {
    TargetCameraPosition = CameraPosition = new Point(0, 0);
    TargetCameraSize     = CameraSize     = 100;
    TargetCameraZoom     = CameraZoom     = 1;
    Engine.AddTicker(this);
  }
  ~SceneViewControl() { Dispose(true); }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  void Dispose(bool finalizing)
  {
    Engine.RemoveTicker(this);
  }

  /// <summary>This event is raised when the camera reaches its move target.</summary>
  /// <remarks>The event is not raised if the camera movement is stopped before completion. After the event is raised,
  /// the event handlers are removed from the event.
  /// </remarks>
  public event SceneViewEventHandler CameraMoved;

  #region Camera positioning
  /// <summary>Returns true if the camera is moving towards the target view, and false if not.</summary>
  [Browsable(false)]
  public bool CameraMoving
  {
    get { return HasFlag(Flag.CameraMoving); }
  }

  /// <summary>Gets/sets the camera axis used to calculate the camera area from the camera size.</summary>
  [Category("Camera")]
  [Description("The axis used to calculate the camera view. With a zoom factor of 1.0, the camera will show a number "+
    "of scene units equal to the CameraSize property along the given axis.")]
  [DefaultValue(CameraAxis.Minor)]
  public CameraAxis CameraAxis
  {
    get { return (CameraAxis)(flags & Flag.AxisMask); }
    set
    {
      if(value != CameraAxis)
      {
        flags = flags & ~Flag.AxisMask | (Flag)value | Flag.CameraDirty;
      }
    }
  }

  /// <summary>Gets the area of the scene covered by the current camera view, in scene units.</summary>
  [Browsable(false)]
  public Rectangle CameraArea
  {
    get
    {
      CalculateCameraView();
      Vector size = new Vector(UnitsPerPixel*Width, UnitsPerPixel*Height);
      return new Rectangle(currentCamera.Center.X-size.X/2, currentCamera.Center.Y-size.Y/2, size.X, size.Y);
    }
  }

  /// <summary>Gets/sets the center point of the current camera view within the scene, in scene units.</summary>
  [Category("Camera")]
  [Description("The center point of the camera view, in scene units.")]
  public Point CameraPosition
  {
    get { return currentCamera.Center; }
    set
    {
      EngineMath.AssertValidFloats(value.X, value.Y);
      StopCameraMove();
      currentCamera.Center = value;
      OnCameraChanged();
    }
  }

  /// <summary>Gets/sets the X coordinate of the center point of the current camera view within the scene, in scene units.</summary>
  [Browsable(false)]
  public double CameraX
  {
    get { return CameraPosition.X; }
    set { CameraPosition = new Point(value, CameraPosition.Y); }
  }

  /// <summary>Gets/sets the Y coordinate of the center point of the current camera view within the scene, in scene units.</summary>
  [Browsable(false)]
  public double CameraY
  {
    get { return CameraPosition.Y; }
    set { CameraPosition = new Point(CameraPosition.X, value); }
  }

  /// <summary>Gets/sets the size of the current camera view within the scene, in scene units.</summary>
  /// <remarks>The camera size is the number of scene units visible on the selected axis of the scene view control.
  /// This property can be set even while the camera is mounted. The selected axis can be set using the
  /// <see cref="CameraAxis"/> property.
  /// </remarks>
  [Category("Camera")]
  [Description("The number of scene units displayed along the camera axis, assuming a zoom factor of 1.0")]
  [DefaultValue(100)]
  public double CameraSize
  {
    get { return currentCamera.Size; }
    set
    {
      EngineMath.AssertValidFloat(value);
      currentCamera.Size = value;
      SetFlag(Flag.CameraMoving, false); // can't use StopCameraMove() because we want this to work with mounting, too.
      OnCameraChanged();
    }
  }

  /// <summary>Gets/sets the zoom factor used by the current camera view.</summary>
  /// <value>The camera zoom factor. If the zoom is 2.0, the width and height of the visible area of the scene will be
  /// half the size that they would normally be (objects will be displayed at 200% normal size). If the zoom is 0.5,
  /// the width and height will be double their normal values (objects will be displayed at 50% normal size). The zoom
  /// factor cannot be negative or zero.
  /// </value>
  /// <remarks>This property can be set even while the camera is mounted.</remarks>
  [Category("Camera")]
  [Description("The camera zoom factor. A factor of 2.0 will show objects zoomed to 200% of their normal size. A "+
    "factor of 0.5 will show objects zoomed to 50% of their normal size.")]
  [DefaultValue(1)]
  public double CameraZoom
  {
    get { return currentCamera.CameraZoom; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(value <= 0) throw new ArgumentOutOfRangeException("CameraZoom", "Camera zoom cannot be negative.");
      currentCamera.CameraZoom = value;
      SetFlag(Flag.CameraMoving, false); // can't use StopCameraMove() because we want this to work with mounting, too.
      OnCameraChanged();
    }
  }
  
  /// <summary>Gets/sets the center point of the target camera view within the scene, in scene units.</summary>
  [Browsable(false)]
  public Point TargetCameraPosition
  {
    get { return targetCamera.Center; }
    set
    {
      EngineMath.AssertValidFloats(value.X, value.Y);
      targetCamera.Center = value;
    }
  }

  /// <summary>Gets/sets the size of the target camera view within the scene, in scene units.</summary>
  /// <remarks>The camera size is the number of scene units visible along the selected axis of the scene view control.</remarks>
  [Browsable(false)]
  public double TargetCameraSize
  {
    get { return targetCamera.Size; }
    set
    {
      EngineMath.AssertValidFloat(value);
      targetCamera.Size = value;
    }
  }

  /// <summary>Gets/sets the target camera zoom factor.</summary>
  /// <value>The camera zoom factor. If the zoom is 2.0, the width and height of the visible area of the scene will be
  /// half the size that they would normally be. If the zoom is 0.5, the width and height will be double their normal
  /// values. The zoom factor cannot be negative.
  /// </value>
  [Browsable(false)]
  public double TargetCameraZoom
  {
    get { return targetCamera.CameraZoom; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(value < 0) throw new ArgumentOutOfRangeException("TargetCameraZoom", "Camera zoom cannot be negative.");
      targetCamera.CameraZoom = value;
    }
  }
  
  /// <summary>Gets/sets the camera interpolation mode.</summary>
  [Category("Camera")]
  [Description("The type of interpolation that the camera will use when transitioning from one view to another.")]
  [DefaultValue(InterpolationMode.Linear)]
  public InterpolationMode CameraInterpolation
  {
    get { return interpolationMode; }
    set { interpolationMode = value; }
  }
  
  /// <summary>Starts the camera moving from the current view to the target view.</summary>
  /// <param name="interpolationTime">
  /// The amount of time, in seconds, that it will take for the camera to morph to the target view.
  /// </param>
  public void StartCameraMove(double interpolationTime)
  {
    EngineMath.AssertValidFloat(interpolationTime);
    if(interpolationTime < 0)
    {
      throw new ArgumentOutOfRangeException("interpolationTime", "Interpolation time cannot be negative.");
    }

    AssertNotMounted();
    SetInterpolationTime(interpolationTime);
    InternalStartCameraMove(true);
  }

  /// <summary>Completes the current camera move, snapping the current camera view to the target view.</summary>
  /// <remarks>The current camera view will not be changed if the camera is not currently moving towards a target.</remarks>
  public void CompleteCameraMove()
  {
    AssertNotMounted();
    
    if(HasFlag(Flag.CameraMoving))
    {
      currentCamera = targetCamera;
      SetFlag(Flag.CameraDirty, true);
      OnCameraChanged();
      if(CameraMoved != null)
      {
        CameraMoved(this);
        CameraMoved = null;
      }
    }
  }

  /// <summary>Halts the current camera move.</summary>
  public void StopCameraMove()
  {
    AssertNotMounted();
    SetFlag(Flag.CameraMoving, false);
  }

  /// <summary>Snaps the current camera view to the view at the top of the view stack.</summary>
  /// <returns>Returns true if the camera was moved to the previous view, or false if the view stack was empty.</returns>
  /// <remarks>This method will modify the target camera view as well, as it's equivalent to calling
  /// <see cref="PopToTargetCameraAndMove"/> with an interpolation time of zero. If the view stack is empty, the camera
  /// will not move.
  /// </remarks>
  public bool PopToCurrentCamera() { return PopToTargetCameraAndMove(0); }

  /// <summary>Pops the view on top of the view stack to the target camera.</summary>
  /// <returns>Returns true if the previous camera view was popped from the stack, and false if the stack was empty.</returns>
  public bool PopToTargetCamera()
  {
    if(viewStack.Count == 0) return false;
    targetCamera = viewStack[viewStack.Count-1];
    viewStack.RemoveAt(viewStack.Count-1);
    return true;
  }

  /// <summary>Pops the view on top of the view stack into the target camera, and begins moving to that view.</summary>
  /// <param name="interpolationTime">The time, in seconds, it will take the current camera to reach the target view.</param>
  /// <returns>Returns true if the previous camera view was popped from the stack, and false if the stack was empty.</returns>
  /// <remarks>If the view stack is empty, the camera will not move.</remarks>
  public bool PopToTargetCameraAndMove(double interpolationTime)
  {
    AssertNotMounted();
    SetInterpolationTime(interpolationTime);
    if(!PopToTargetCamera()) return false;
    InternalStartCameraMove(false);
    return true;
  }

  int AxisLength
  {
    get
    {
      CameraAxis axis = CameraAxis;
      return axis == CameraAxis.Major ? Math.Max(Width, Height) :
             axis == CameraAxis.Minor ? Math.Min(Width, Height) :
             axis == CameraAxis.X     ? Width : Height;
    }
  }

  /// <summary>Throws an <see cref="InvalidOperationException"/> if the camera is mounted.</summary>
  void AssertNotMounted()
  {
    if(CameraMounted) throw new InvalidOperationException("Cannot manually control the camera while it's mounted.");
  }

  /// <summary>Calculates the zoomed camera view and the scene units to pixel ratio based on the current camera view.</summary>
  /// <remarks>This method will return immediately if the <see cref="Flag.CameraDirty"/> flag is not set.</remarks>
  void CalculateCameraView()
  {
    if(HasFlag(Flag.CameraDirty))
    {
      UnitsPerPixel = currentCamera.Size / (AxisLength * currentCamera.CameraZoom);
      Vector size = new Vector(UnitsPerPixel*Width, UnitsPerPixel*Height);
      ZoomedArea = new Rectangle(currentCamera.Center.X-size.X/2, currentCamera.Center.Y-size.Y/2, size.X, size.Y);
    }
  }

  /// <summary>Begins morphing the camera from its current view to its target view.</summary>
  /// <param name="pushCurrentView">If true, the current camera view will be pushed onto the view stack.</param>
  void InternalStartCameraMove(bool pushCurrentView)
  {
    // if we're already there, don't do anything.
    if(currentCamera.Center == targetCamera.Center && currentCamera.CameraZoom == targetCamera.CameraZoom &&
       currentCamera.Size == targetCamera.Size)
    {
      SetFlag(Flag.CameraMoving, false);
      if(pushCurrentView) PushCurrentCamera();
    }
    else
    {
      interpolationPosition = 0;
      sourceCamera = currentCamera;
      SetFlag(Flag.CameraMoving, true);
      if(pushCurrentView) PushCurrentCamera();
      if(interpolationTime == 0) CompleteCameraMove();
    }
  }

  /// <summary>Called when the current camera view has changed. Sets the camera dirty flag and invalidates the control.
  /// </summary>
  void OnCameraChanged()
  {
    SetFlag(Flag.CameraDirty, true);
    Invalidate();
  }
  
  /// <summary>Pushes the current camera view onto a stack so that it can be restored later.</summary>
  void PushCurrentCamera()
  {
    if(viewStack == null) viewStack = new List<CameraView>();
    else if(viewStack.Count == ViewsToSave) viewStack.RemoveAt(0);
    viewStack.Add(currentCamera);
  }
  
  void SetInterpolationTime(double time)
  {
    if(time < 0) throw new ArgumentOutOfRangeException("Camera interpolation time cannot be negative.");
    interpolationTime = time;
  }
  #endregion
  
  #region Camera mounting
  /// <summary>Gets a value that indicates whether the camera is currently mounted to an object.</summary>
  [Browsable(false)]
  public bool CameraMounted
  {
    get { return mountObject != null; }
  }

  /// <summary>Gets/sets how far into the future the camera will predict the mounted object's motion, in seconds.</summary>
  [Category("Mounting")]
  [Description("How far into the future the camera will predict the mounted object's motion, in seconds.")]
  public double MountLookahead
  {
    get { return mountLookahead; }
    set
    {
      EngineMath.AssertValidFloat(value);
      mountLookahead = value;
    }
  }

  /// <summary>Gets/sets an arbitrary offset applied to the camera's mount target.</summary>
  /// <remarks>This is wholly different from the offset passed to the <see cref="MountCamera"/> function. The mount
  /// offset is an offset from where the camera's normal destination is not a link point on the mount object.
  /// </remarks>
  [Category("Mounting")]
  [Description("An arbitrary offset applied to the camera's mount point.")]
  public Vector MountOffset
  {
    get { return mountOffset; }
    set
    {
      EngineMath.AssertValidFloats(value.X, value.Y);
      mountOffset = value;
    }
  }

  /// <summary>Gets/sets the maximum distance the camera can be from its mount point, in scene units.</summary>
  /// <remarks>R</remarks>
  [Category("Mounting")]
  [Description("The maximum distance the camera can be from its mount point, in scene units.")]
  public double MountRadius
  {
    get { return mountRadius; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(mountRadius < 0)
        throw new ArgumentOutOfRangeException("MountRadius", "Mount radius cannot be negative.");
      mountRadius = value;
    }
  }
  
  /// <summary>Gets/sets the rigidity of the camera mount. This is a non-negative value that describes how quickly the
  /// camera will approach its mount target.
  /// </summary>
  [Category("Mounting")]
  [Description("The rigidity of the mount point. This is a non-negative value that describes how quickly the camera "+
    "will approach its mount point.")]
  public double MountRigidity
  {
    get { return mountRigidity; }
    set
    {
      EngineMath.AssertValidFloat(value);
      if(mountRigidity < 0)
        throw new ArgumentOutOfRangeException("MountRigidity", "Mount rigidity cannot be negative.");
      mountRigidity = value;
    }
  }

  /// <summary>Mounts the camera to an object.</summary>
  /// <param name="mountTo">The <see cref="SceneObject"/> to which the camera will be mounted.</param>
  /// <param name="offset">The offset from the center of the object of the point at which the camera will be mounted.
  /// The mounted point will rotate along with the object.
  /// </param>
  /// <param name="snapToMount">If true, the camera will immediately snap to the mount point. This does not affect the
  /// future movement of the camera, which is determined by <paramref name="rigidity"/>.
  /// </param>
  /// <remarks>The camera's destination (mount target) is calculated by taking the offset from the object's center
  /// given to this function, predicting its future position with <see cref="MountLookahead"/>, and adding the
  /// <see cref="MountOffset"/>. The <see cref="MountRigidity"/> then determines how far the camera will move towards
  /// the destination. Finally, the camera is clipped to be no more than <see cref="MountRadius"/> away from the
  /// object.
  /// </remarks>
  public void MountCamera(SceneObject mountTo, double offsetX, double offsetY, bool snapToMount)
  {
    EngineMath.AssertValidFloats(offsetX, offsetY);
    if(mountTo == null) throw new ArgumentNullException("mountTo", "Can't mount the camera to a null object.");

    if(CameraMounted) DismountCamera();
    else StopCameraMove();
    
    mountObject = mountTo;
    mountLinkID = offsetX == 0 && offsetY == 0 ? -1 : mountObject.AddLinkPoint(offsetX, offsetY);

    if(snapToMount)
    {
      currentCamera.Center = CameraMountTarget;
      OnCameraChanged();
    }
  }

  /// <summary>Dismounts the camera.</summary>
  /// <remarks>If the camera is not mounted to an object, nothing will happen.</remarks>
  public void DismountCamera()
  {
    if(CameraMounted)
    {
      if(mountLinkID != -1)
      {
        mountObject.RemoveLinkPoint(mountLinkID);
        mountLinkID = -1;
      }
      mountObject = null;
    }
  }
  
  /// <summary>Gets the current mount position, in scene units.</summary>
  Point CameraMountTarget
  {
    get
    {
      // get the base mount position
      Point position = CameraMountPosition;
      // then apply the lookahead and offset
      return position + mountObject.Velocity*mountLookahead + mountOffset;
    }
  }
  
  Point CameraMountPosition
  {
    get { return mountLinkID == -1 ? mountObject.Position : mountObject.GetLinkPoint(mountLinkID); }
  }
  #endregion

  #region Coordinate conversion
  /// <summary>Converts a distance in client coordinates to a distance in scene coordinates.</summary>
  public Vector ClientToScene(Size clientSize)
  {
    CalculateCameraView();
    return new Vector(clientSize.Width*UnitsPerPixel, clientSize.Height*UnitsPerPixel);
  }

  /// <summary>Converts a point relative to the client area of the scene view to a point within the scene.</summary>
  /// <param name="clientPoint">A point relative to the client area of the scene view, in pixels.</param>
  /// <returns>The point within the scene, in scene units.</returns>
  public Point ClientToScene(SPoint clientPoint)
  {
    CalculateCameraView();
    return new Point(ZoomedArea.X + clientPoint.X*UnitsPerPixel, ZoomedArea.Y + clientPoint.Y*UnitsPerPixel);
  }

  /// <summary>Converts a rectangle relative to the client area of the scene view to a rectangle within the scene.</summary>
  /// <param name="clientPoint">A rectangle relative to the client area of the scene view, in pixels.</param>
  /// <returns>The rectangle within the scene, in scene units.</returns>
  public Rectangle ClientToScene(SRectangle clientRect)
  {
    return new Rectangle(ClientToScene(clientRect.Location), // ClientToScene will call CalculateCameraView
                         new Vector(clientRect.Width*UnitsPerPixel, clientRect.Height*UnitsPerPixel));
  }

  /// <summary>Converts a point within the scene to a point relative to the client area of the scene view.</summary>
  /// <param name="clientPoint">A point within the scene, in scene units.</param>
  /// <returns>The point relative to the client area of the scene view, in pixels.</returns>
  public SPoint SceneToClient(Point scenePoint)
  {
    EngineMath.AssertValidFloats(scenePoint.X, scenePoint.Y);
    CalculateCameraView();
    return new SPoint((int)Math.Round((scenePoint.X-ZoomedArea.X) / UnitsPerPixel),
                      (int)Math.Round((scenePoint.Y-ZoomedArea.Y) / UnitsPerPixel));
  }
  
  /// <summary>Converts a rectangle within the scene to a rectangle relative to the client area of the scene view.</summary>
  /// <param name="clientPoint">A rectangle within the scene, in scene units.</param>
  /// <returns>The rectangle relative to the client area of the scene view, in pixels.</returns>
  public SRectangle SceneToClient(Rectangle sceneRect)
  {
    EngineMath.AssertValidFloats(sceneRect.Width, sceneRect.Height);
    return new SRectangle(SceneToClient(sceneRect.Location), // SceneToClient will call CalculateCameraView
                          new Size((int)Math.Round(sceneRect.Width  / UnitsPerPixel),
                                   (int)Math.Round(sceneRect.Height / UnitsPerPixel)));
  }

  /// <summary>Converts a distance in scene coordinates to a distance in client coordinates.</summary>
  public Size SceneToClient(Vector sceneSize)
  {
    EngineMath.AssertValidFloats(sceneSize.X, sceneSize.Y);
    CalculateCameraView();
    return new Size((int)Math.Round(sceneSize.X/UnitsPerPixel), (int)Math.Round(sceneSize.Y/UnitsPerPixel));
  }

  #endregion

  #region Group and layer masks, rendering flags, scene object
  /// <summary>Gets/sets the object group mask used for rendering.</summary>
  /// <remarks>Only objects that are members of the groups specified in the group mask will be rendered.</remarks>
  [Category("Camera")]
  [Description("A bitmask which determines the object groups that will be rendered.")]
  [DefaultValue((uint)0xffffffff)]
  public uint GroupMask
  {
    get { return groupMask; }
    set
    {
      if(value != groupMask)
      {
        groupMask = value;
        Invalidate();
      }
    }
  }

  /// <summary>Gets/sets the object layer mask used for rendering.</summary>
  /// <remarks>Only objects that are members of the layers specified in the layer mask will be rendered.</remarks>
  [Category("Camera")]
  [Description("A bitmask which determines the layers that will be rendered.")]
  [DefaultValue((uint)0xffffffff)]
  public uint LayerMask
  {
    get { return layerMask; }
    set
    {
      if(value != layerMask)
      {
        layerMask = value;
        Invalidate();
      }
    }
  }

  /// <summary>Determines whether invisible objects will be rendered.</summary>
  [Category("Camera")]
  [Description("Whether invisible objects will be rendered or not.")]
  [DefaultValue(false)]
  public bool RenderInvisible
  {
    get { return HasFlag(Flag.RenderInvisible); }
    set
    {
      if(value != RenderInvisible)
      {
        SetFlag(Flag.RenderInvisible, value);
        Invalidate();
      }
    }
  }

  /// <summary>Gets/sets the scene that will be rendered in this scene view.</summary>
  /// <value>The <see cref="Scene"/> object to be rendered, or null.</value>
  [Browsable(false)]
  public Scene Scene
  {
    get { return scene; }
    set
    {
      if(value != scene)
      {
        scene = value;
        Invalidate();
      }
    }
  }
  #endregion

  protected internal override void RenderContent(SRectangle drawArea)
  {
    if(Scene == null) return;
    
    // set up a projection so that the area occupied by the viewport matches the area 
    GL.glMatrixMode(GL.GL_PROJECTION);
    GL.glPushMatrix(); // save the old Projection matrix first
    GL.glLoadIdentity();
    CalculateCameraView();
    GLU.gluOrtho2D(ZoomedArea.X, ZoomedArea.Right, ZoomedArea.Bottom, ZoomedArea.Y);

    SRectangle oldViewport = Video.GetViewport(); // backup current viewport
    Video.SetViewport(ScreenRect); // set up the new viewport

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glPushMatrix(); // save the old ModelView matrix
    GL.glLoadIdentity();

    Rectangle viewArea = ClientToScene(ScreenToClient(drawArea));
    Scene.Render(ref viewArea, LayerMask, GroupMask, RenderInvisible);

    GL.glPopMatrix(); // restore the ModelView matrix
    GL.glMatrixMode(GL.GL_PROJECTION); // switch to Projection mode
    GL.glPopMatrix(); // restore the Projection matrix
    GL.glMatrixMode(GL.GL_MODELVIEW); // switch back to ModelView mode

    Video.SetViewport(oldViewport); // restore the old viewport
  }

  protected override void OnSizeChanged(Size oldSize)
  {
    base.OnSizeChanged(oldSize);
    SetFlag(Flag.CameraDirty, true); // if the size changes, we need to update the unit conversion scale
  }

  protected virtual void Simulate(double timeDelta)
  {
    if(Scene == null) return;

    // calculate camera movement
    if(HasFlag(Flag.CameraMoving)) // if the camera is morphing towards a target view
    {
      interpolationPosition += timeDelta / interpolationTime; // update our position within the interpolation
      
      if(interpolationPosition >= 1) // if we're done, snap to the target and stop moving
      {
        CompleteCameraMove();
      }
      else
      {
        currentCamera.Center = EngineMath.Interpolate(ref sourceCamera.Center, ref targetCamera.Center,
                                                      interpolationPosition, interpolationMode);
        currentCamera.Size = EngineMath.Interpolate(sourceCamera.Size, targetCamera.Size,
                                                    interpolationPosition, interpolationMode);
        currentCamera.CameraZoom = EngineMath.Interpolate(sourceCamera.CameraZoom, targetCamera.CameraZoom,
                                                          interpolationPosition, interpolationMode);
        OnCameraChanged();
      }
    }
    else if(CameraMounted) // otherwise, if the camera is following a target, update its location
    {
      if(mountRadius == 0) // if the mount radius is zero, just snap directly to the mount point
      {
        currentCamera.Center = CameraMountPosition;
      }
      else
      {
        Point  target   = CameraMountTarget;
        Vector vector   = target - currentCamera.Center;
        double distance = vector.LengthSqr; // distance to mount target, squared

        // if the distance is near zero, we can't normalize the vector. and we're close enough anyway.
        if(!EngineMath.Equals(distance, 0))
        {
          vector.Normalize(Math.Sqrt(distance)*timeDelta*mountRigidity); // calculate movement distance
          // if the movement distance is further than the real distance, just snap to the target
          if(vector.LengthSqr >= distance)
          {
            currentCamera.Center = target;
          }
          else // otherwise, move part of the way to the target
          {
            currentCamera.Center += vector;
          }
        }

        // clip to the maximum radius (which is calculated based on the mount point, not the camera target)
        target   = CameraMountPosition;
        vector   = currentCamera.Center - CameraMountPosition;
        distance = vector.LengthSqr; // distance from mount point (on object), squared

        if(distance > mountRadius*mountRadius)
        {
          vector.Normalize(mountRadius);
          currentCamera.Center = CameraMountPosition + vector;
        }
      }

      OnCameraChanged();
    }

    // repaint the view if the camera moved (and thus probably changed)
    if(HasFlag(Flag.CameraDirty)) Invalidate();
  }

  /// <summary>The maximum number of camera views to save.</summary>
  const int ViewsToSave = 64;

  enum Flag : byte
  {
    /// <summary>Determines whether the view limit rectangle is enabled.</summary>
    ViewLimitEnabled = 0x01,
    /// <summary>Determines whether the camera will morph to the target view.</summary>
    CameraMoving = 0x02,
    /// <summary>Indicates whether the current camera view has changed.</summary>
    CameraDirty = 0x04,
    /// <summary>Indicates whether invisible objects should be rendered.</summary>
    RenderInvisible = 0x08,
    /// <summary>The mask applied to retrieve the camera axis.</summary>
    AxisMask = 0x30,
  }

  struct CameraView
  {
    /// <summary>The center of the scene to view, in scene units.</summary>
    public Point Center;
    /// <summary>The size of the scene to view, in scene units, assuming the camera is not zoomed.</summary>
    public double Size;
    /// <summary>The zoom factor of the camera, which should be a positive number.</summary>
    public double CameraZoom;
  }

  bool HasFlag(Flag flag)
  {
    return (flags & flag) != 0;
  }
  
  void SetFlag(Flag flag, bool on)
  {
    if(on) flags |= flag;
    else flags &= ~flag;
  }

  void ITicker.Tick(double timeDelta) { Simulate(timeDelta); }

  /// <summary>The current camera view.</summary>
  CameraView currentCamera;
  /// <summary>The camera view at the initiated of the camera move.</summary>
  CameraView sourceCamera;
  /// <summary>The camera view that the camera will morph to, assuming the CameraMoving flag is set.</summary>
  CameraView targetCamera;

  /// <summary>The viewed area of the current camera after camera zooming is taken into account.</summary>
  Rectangle ZoomedArea;
  /// <summary>The ratio of scene units to pixels of the current camera, after zooming is taken into account.</summary>
  double UnitsPerPixel;

  /// <summary>How long, in seconds, the camera will take to morph to the target view.</summary>
  double interpolationTime;
  /// <summary>How far we are in the morph from the source to the target, expressed as a number from 0 to 1.</summary>
  double interpolationPosition;

  /// <summary>An arbitrary offset applied to the camera's mount target.</summary>
  Vector mountOffset;
  /// <summary>A maximum radius for the camera mount point.</summary>
  double mountRadius;
  /// <summary>How rigid the mount is. With higher values, the camera will approach its target more quickly.</summary>
  double mountRigidity = 2.5;
  /// <summary>How far ahead in time the camera predicts the object's position.</summary>
  double mountLookahead = 3/4.0;
  /// <summary>The object to which the camera is mounted, or null if the camera is not mounted.</summary>
  SceneObject mountObject;

  /// <summary>Holds the last several camera views.</summary>
  List<CameraView> viewStack;

  /// <summary>The scene that this control will render.</summary>
  [NonSerialized] Scene scene;
  
  /// <summary>The mask that controls which layers to draw.</summary>
  uint layerMask = 0xffffffff;
  /// <summary>The mask that controls which object groups to draw.</summary>
  uint groupMask = 0xffffffff;
  
  int mountLinkID;
  Flag flags;
  InterpolationMode interpolationMode = InterpolationMode.Linear;
}

} // namespace RotationalForce.Engine