using System;
using System.Collections.Generic;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

public delegate void ObjectFinderCallback(SceneObject obj, object context);

public class PickOptions
{
  public SceneObject ObjectToIgnore;
  public uint GroupMask, LayerMask;
  public bool AllowInvisible, AllowUnpickable, RequireContainment, SortByLayer;
}

public class Scene : UniqueObject, ITicker, IDisposable
{
  public Scene()
  {
    Engine.AddTicker(this);
  }
  ~Scene() { Dispose(true); }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }
  
  void Dispose(bool finalizing)
  {
    Engine.RemoveTicker(this);
  }

  #region Acceleration
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
  
  public bool ApplyAcceleration
  {
    get { return HasFlag(Flag.ApplyAcceleration); }
    set { SetFlag(Flag.ApplyAcceleration, value); }
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
  #endregion

  public double ElapsedTime
  {
    get { return elapsedTime; }
  }

  public bool Paused
  {
    get { return HasFlag(Flag.Paused); }
    set { SetFlag(Flag.Paused, value); }
  }

  public void AddObject(SceneObject obj)
  {
    if(obj == null) throw new ArgumentNullException("obj");
    if(obj.Scene != null) throw new ArgumentException("This object is already part of a scene.");
    if(obj.Dead) throw new ArgumentException("This object cannot be added to the scene because it is dead.");
    obj.Scene = this;
    objects.Add(obj);
  }

  public void ClearObjects() { ClearObjects(true); }
  public void ClearObjects(bool deleteObjects)
  {
    foreach(SceneObject obj in objects)
    {
      if(deleteObjects) obj.Delete();
      PrepareObjectForRemoval(obj);
    }

    objects.Clear();
  }

  public void RemoveObject(SceneObject obj)
  {
    PrepareObjectForRemoval(obj);
    objects.Remove(obj);
  }

  #region Picking and finding objects
  #region PickEnumerable
  abstract class PickEnumerable : IEnumerable<SceneObject>
  {
    public PickEnumerable(List<SceneObject> objects, PickOptions options)
    { 
      if(objects == null || options == null) throw new ArgumentNullException();
      this.objects = objects;
      this.options = options;
    }

    protected abstract class PickEnumerator : IEnumerator<SceneObject>
    {
      public PickEnumerator(IEnumerable<SceneObject> objects)
      { 
        this.objects = objects.GetEnumerator();
      }
      
      public abstract bool MoveNext();

      SceneObject IEnumerator<SceneObject>.Current
      {
	      get { return objects.Current; }
      }

      void IDisposable.Dispose() { }

      object System.Collections.IEnumerator.Current
      {
	      get { return objects.Current; }
      }

      void System.Collections.IEnumerator.Reset()
      {
 	      objects.Reset();
      }

      protected IEnumerator<SceneObject> objects;
    }

    public abstract IEnumerator<SceneObject> GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    protected List<SceneObject> objects;
    protected PickOptions options;
  }
  #endregion

  #region AllEnumerable
  sealed class AllEnumerable : PickEnumerable
  {
    public AllEnumerable(List<SceneObject> objects, PickOptions options)
      : base(objects, options) { }

    public override IEnumerator<SceneObject> GetEnumerator()
    {
      return new AllEnumerator(this);
    }

    sealed class AllEnumerator : PickEnumerator
    {
      public AllEnumerator(AllEnumerable parent) : base(parent.objects)
      {
        this.parent = parent;
      }

      public override bool MoveNext()
      {
        while(objects.MoveNext())
        {
          if(CanPick(objects.Current, parent.options))
          {
            return true;
          }
        }
        return false;
      }

      AllEnumerable parent;
    }
  }
  #endregion

  #region CircleEnumerable
  sealed class CircleEnumerable : PickEnumerable
  {
    public CircleEnumerable(List<SceneObject> objects, PickOptions options, Point worldPoint, double radius)
      : base(objects, options)
    { 
      this.circle = new Circle(worldPoint.X, worldPoint.Y, radius);
    }

    public override IEnumerator<SceneObject> GetEnumerator()
    {
      return new CircleEnumerator(this);
    }

    sealed class CircleEnumerator : PickEnumerator
    {
      public CircleEnumerator(CircleEnumerable parent) : base(parent.objects)
      { 
        this.parent = parent;
      }
      
      public override bool MoveNext()
      {
 	      while(objects.MoveNext())
 	      {
 	        SceneObject obj = objects.Current;
 	        if(CanPick(obj, parent.options))
 	        {
 	          if(parent.options.RequireContainment)
 	          {
 	            if(obj.ContainedBy(parent.circle)) return true;
 	          }
 	          else if(obj.Intersects(parent.circle))
 	          {
 	            return true;
 	          }
 	        }
 	      }
 	      return false;
      }

      CircleEnumerable parent;
    }

    Circle circle;
  }
  #endregion
  
  #region LineEnumerable
  sealed class LineEnumerable : PickEnumerable
  {
    public LineEnumerable(List<SceneObject> objects, PickOptions options, Point startPoint, Point endPoint)
      : base(objects, options)
    { 
      this.line = new Line(startPoint, endPoint);
    }

    public override IEnumerator<SceneObject> GetEnumerator()
    {
      return new LineEnumerator(this);
    }

    sealed class LineEnumerator : PickEnumerator
    {
      public LineEnumerator(LineEnumerable parent) : base(parent.objects)
      { 
        this.parent = parent;
      }
      
      public override bool MoveNext()
      {
 	      while(objects.MoveNext())
 	      {
 	        SceneObject obj = objects.Current;
 	        if(CanPick(obj, parent.options) && obj.Intersects(parent.line))
 	        {
            return true;
 	        }
 	      }
 	      return false;
      }

      LineEnumerable parent;
    }

    Line line;
  }
  #endregion

  #region PointEnumerable
  sealed class PointEnumerable : PickEnumerable
  {
    public PointEnumerable(List<SceneObject> objects, PickOptions options, Point worldPoint)
      : base(objects, options)
    { 
      point = worldPoint;
    }

    public override IEnumerator<SceneObject> GetEnumerator()
    {
      return new PointEnumerator(this);
    }

    sealed class PointEnumerator : PickEnumerator
    {
      public PointEnumerator(PointEnumerable parent) : base(parent.objects)
      { 
        this.parent = parent;
      }
      
      public override bool MoveNext()
      {
 	      while(objects.MoveNext())
 	      {
 	        SceneObject obj = objects.Current;
 	        if(CanPick(obj, parent.options) && obj.Contains(parent.point))
 	        {
            return true;
 	        }
 	      }
 	      return false;
      }

      PointEnumerable parent;
    }

    Point point;
  }
  #endregion

  #region RectangleEnumerable
  sealed class RectangleEnumerable : PickEnumerable
  {
    public RectangleEnumerable(List<SceneObject> objects, PickOptions options, Rectangle worldArea)
      : base(objects, options)
    { 
      rect = worldArea;
    }

    public override IEnumerator<SceneObject> GetEnumerator()
    {
      return new RectangleEnumerator(this);
    }

    sealed class RectangleEnumerator : PickEnumerator
    {
      public RectangleEnumerator(RectangleEnumerable parent) : base(parent.objects)
      { 
        this.parent = parent;
      }
      
      public override bool MoveNext()
      {
 	      while(objects.MoveNext())
 	      {
 	        SceneObject obj = objects.Current;
 	        if(CanPick(obj, parent.options))
 	        {
 	          if(parent.options.RequireContainment)
 	          {
 	            if(obj.ContainedBy(parent.rect))
 	            {
 	              return true;
 	            }
 	          }
 	          else if(obj.Intersects(parent.rect))
 	          {
 	            return true;
 	          }
 	        }
 	      }
 	      return false;
      }

      RectangleEnumerable parent;
    }

    Rectangle rect;
  }
  #endregion

  #region ObjectLayerSorter
  sealed class ObjectLayerSorter : Comparer<SceneObject>
  {
    ObjectLayerSorter() { }

    public override int Compare(SceneObject x, SceneObject y)
    {
      return y.Layer - x.Layer;
    }
    
    public static readonly ObjectLayerSorter Instance = new ObjectLayerSorter();
  }
  #endregion

  static bool CanPick(SceneObject obj, PickOptions options)
  {
    return obj != options.ObjectToIgnore && !obj.Dead &&
           (options.AllowUnpickable || obj.PickingAllowed) &&
           (obj.GroupMask & options.GroupMask) != 0 && (obj.LayerMask & options.LayerMask) != 0 &&
           (options.AllowInvisible || obj.EffectiveVisibility);
  }

  public IEnumerable<SceneObject> PickAll()
  {
    PickOptions options = new PickOptions();
    options.AllowInvisible = options.AllowUnpickable = true;
    options.GroupMask = options.LayerMask = 0xffffffff;
    return PickAll(options);
  }

  public IEnumerable<SceneObject> PickAll(PickOptions options)
  {
    return Pick(new AllEnumerable(objects, options), options.SortByLayer);
  }

  public uint PickAll(PickOptions options, ObjectFinderCallback callback, object context)
  {
    return Pick(PickAll(options), callback, context);
  }

  public IEnumerable<SceneObject> PickCircle(Point worldPoint, double radius, PickOptions options)
  {
    return Pick(new CircleEnumerable(objects, options, worldPoint, radius), options.SortByLayer);
  }

  public uint PickCircle(Point worldPoint, double radius, PickOptions options,
                         ObjectFinderCallback callback, object context)
  {
    return Pick(PickCircle(worldPoint, radius, options), callback, context);
  }

  public IEnumerable<SceneObject> PickLine(Point startPoint, Point endPoint, PickOptions options)
  {
    return Pick(new LineEnumerable(objects, options, startPoint, endPoint), options.SortByLayer);
  }

  public uint PickLine(Point startPoint, Point endPoint, PickOptions options,
                       ObjectFinderCallback callback, object context)
  {
    return Pick(PickLine(startPoint, endPoint, options), callback, context);
  }

  public IEnumerable<SceneObject> PickPoint(Point worldPoint, PickOptions options)
  {
    return Pick(new PointEnumerable(objects, options, worldPoint), options.SortByLayer);
  }

  public uint PickPoint(Point worldPoint, PickOptions options, ObjectFinderCallback callback, object context)
  {
    return Pick(PickPoint(worldPoint, options), callback, context);
  }

  public IEnumerable<SceneObject> PickRectangle(Rectangle worldArea, PickOptions options)
  {
    return Pick(new RectangleEnumerable(objects, options, worldArea), options.SortByLayer);
  }

  public uint PickRectangle(Rectangle worldArea, PickOptions options, ObjectFinderCallback callback, object context)
  {
    return Pick(PickRectangle(worldArea, options), callback, context);
  }

  static IEnumerable<SceneObject> Pick(IEnumerable<SceneObject> enumerable, bool sortByLayer)
  {
    if(!sortByLayer)
    {
      return enumerable;
    }
    else
    {
      List<SceneObject> sorted = new List<SceneObject>(enumerable);
      sorted.Sort(ObjectLayerSorter.Instance);
      return sorted;
    }
  }

  static uint Pick(IEnumerable<SceneObject> enumerable, ObjectFinderCallback callback, object context)
  {
    uint objectsFound = 0;
    foreach(SceneObject obj in enumerable)
    {
      callback(obj, context);
      objectsFound++;
    }
    return objectsFound;
  }
  #endregion

  #region Serialization
  protected override void Serialize(SerializationStore store)
  {
    // saving the objects here instead of during the automatic serialization allows us to eliminate dead objects on
    // load and restore the Scene pointers of the objects (SceneObjects don't serialize their Scene pointers)
    store.AddValue("Scene.Objects", objects);
  }

  protected override void Deserialize(DeserializationStore store)
  {
    List<SceneObject> savedObjects = (List<SceneObject>)store.GetValue("Scene.Objects");

    foreach(SceneObject obj in savedObjects) // re-add all non-dead objects using the normal API
    {
      if(!obj.Dead)
      {
        AddObject(obj);
      }
    }
  }
  #endregion

  protected internal virtual void Render(ref Rectangle viewArea, uint layerMask, uint groupMask, bool renderInvisible)
  {
    PickOptions options = new PickOptions();
    options.AllowInvisible  = renderInvisible;
    options.AllowUnpickable = true;
    options.GroupMask       = groupMask;
    options.LayerMask       = layerMask;

    // find objects to render within the view area and place them in layeredRenderObjects
    foreach(SceneObject obj in PickRectangle(viewArea, options))
    {
      int layer = obj.Layer;
      if(layeredRenderObjects[layer] == null) layeredRenderObjects[layer] = new List<SceneObject>();
      layeredRenderObjects[layer].Add(obj);
    }

    // loop through the layers, from back (31) to front (0)
    for(int layer=layeredRenderObjects.Length-1; layer >= 0; layer--)
    {
      List<SceneObject> layerObjects = layeredRenderObjects[layer];
      if(layerObjects == null || layerObjects.Count == 0) continue;

      foreach(SceneObject obj in layerObjects)
      {
        if(!obj.Dead) obj.Render(); // visibility checking is handled by FindObjects
      }

      layerObjects.Clear();
    }
  }

  protected virtual void Simulate(double timeDelta)
  {
    if(!HasFlag(Flag.Paused))
    {
      bool applyAcceleration = HasFlag(Flag.ApplyAcceleration);
      foreach(SceneObject obj in objects)
      {
        if(!obj.Dead) // objects could be marked dead anywhere, so we have to check this here
        {
          if(applyAcceleration) obj.AddVelocity(Acceleration);
          obj.Simulate(timeDelta);
        }
        if(obj.Dead) deleted.Add(obj);
      }

      foreach(SceneObject obj in objects)
      {
        if(!obj.Dead) obj.PostSimulate();
      }

      // remove dead objects from the scene
      foreach(SceneObject obj in deleted)
      {
        RemoveObject(obj);
      }
      deleted.Clear();

      elapsedTime += timeDelta;
    }
  }

  const int NumberOfLayers = 32;

  enum Flag
  {
    /// <summary>Determines whether the simulation is paused.</summary>
    Paused = 0x01,
    /// <summary>Determines whether the constant acceleration will be applied.</summary>
    ApplyAcceleration = 0x02,
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

  /// <summary>An acceleration applied to all objects in the scene.</summary>
  /// <remarks>This is used for things like gravity.</remarks>
  Vector acceleration;

  /// <summary>The total elapsed simulation time for this scene, in seconds.</summary>
  double elapsedTime;
  
  [NonSerialized] List<SceneObject> objects = new List<SceneObject>();
  /// <summary>Holds objects which are pending deletion.</summary>
  [NonSerialized] List<SceneObject> deleted = new List<SceneObject>();
  /// <summary>An array of list containing the objects to render.</summary>
  [NonSerialized] List<SceneObject>[] layeredRenderObjects = new List<SceneObject>[NumberOfLayers];

  Flag flags;

  static void PrepareObjectForRemoval(SceneObject obj)
  {
    obj.Scene = null;
  }
}

} // namespace RotationalForce.Engine