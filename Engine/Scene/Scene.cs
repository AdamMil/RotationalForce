using System;
using System.Collections.Generic;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

public delegate void ObjectFinderCallback(SceneObject obj, object context);

public class Scene : ITicker, IDisposable
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
  uint FindObjects(ref Rectangle searchArea, uint layerMask, uint groupMask, bool returnInvisible, bool picking,
                   ObjectFinderCallback callback, object context, SceneObject objectToIgnore)
  {
    uint objectsFound = 0;

    foreach(SceneObject obj in objects)
    {
      // only return objects that are not ignored, dead, unpickable (when picking), or
      // invisible (when not returning invisible), and that match the layer/group masks
      // and are within the search area
      if(obj != objectToIgnore && !obj.Dead && (!picking || obj.PickingAllowed) && (returnInvisible || obj.Visible) &&
         (obj.GroupMask & groupMask) != 0 && (obj.LayerMask & layerMask) != 0 &&
         searchArea.Intersects(obj.Area))
      {
        callback(obj, context);
        objectsFound++;
      }
    }

    return objectsFound;
  }
  #endregion

  protected internal virtual void Render(ref Rectangle viewArea, uint layerMask, uint groupMask, bool renderInvisible)
  {
    // find objects to render within the view area (they will be placed in layeredRenderObjects)
    if(FindObjects(ref viewArea, layerMask, groupMask, renderInvisible, false,
                   new ObjectFinderCallback(AddObjectToRender), null, null) > 0)
    {
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

  void AddObjectToRender(SceneObject obj, object context)
  {
    int layer = obj.Layer;
    if(layeredRenderObjects[layer] == null) layeredRenderObjects[layer] = new List<SceneObject>();
    layeredRenderObjects[layer].Add(obj);
  }

  void ITicker.Tick(double timeDelta) { Simulate(timeDelta); }

  /// <summary>An acceleration applied to all objects in the scene.</summary>
  /// <remarks>This is used for things like gravity.</remarks>
  Vector acceleration;

  /// <summary>The total elapsed simulation time for this scene, in seconds.</summary>
  double elapsedTime;
  
  List<SceneObject> objects = new List<SceneObject>();
  /// <summary>Holds objects which are pending deletion.</summary>
  List<SceneObject> deleted = new List<SceneObject>();
  /// <summary>An array of list containing the objects to render.</summary>
  List<SceneObject>[] layeredRenderObjects = new List<SceneObject>[NumberOfLayers];

  Flag flags;

  static void PrepareObjectForRemoval(SceneObject obj)
  {
    obj.Scene = null;
  }
}

} // namespace RotationalForce.Engine