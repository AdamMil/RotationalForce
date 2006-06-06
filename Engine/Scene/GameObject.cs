using System;
using System.Collections.Generic;

namespace RotationalForce.Engine
{

public delegate void GameObjectEventHandler(GameObject obj);

/// <summary>The base class of all game objects.</summary>
public abstract class GameObject
{
  public GameObject()
  {
    id = nextID++;
  }

  public event GameObjectEventHandler Deleted;

  public virtual void Delete()
  {
    if(Deleted != null) Deleted(this);
  }

  public override int GetHashCode() { return (int)id; }

  /// <summary>A possibly-null dictionary that contains script data associated with this object.</summary>
  Dictionary<string,object> scriptData;
  /// <summary>The object's name. This may be null.</summary>
  string name;
  /// <summary>A bit field the groups to which the object belongs.</summary>
  uint groups;
  /// <summary>The object's unique ID.</summary>
  uint id;

  static uint nextID = 1;
}

} // namespace RotationalForce.Engine