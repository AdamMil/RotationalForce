using System;
using System.Collections.Generic;

namespace RotationalForce.Engine
{

public delegate void GameObjectEventHandler(GameObject obj);

/// <summary>The base class of all game objects.</summary>
public abstract class GameObject : UniqueObject
{
  public event GameObjectEventHandler Deleted;

  public virtual void Delete()
  {
    if(Deleted != null) Deleted(this);
  }

  public bool ContainsScriptVar(string name)
  {
    return scriptData == null ? false : scriptData.ContainsKey(name);
  }
  
  public void DeleteScriptVar(string name)
  {
    if(scriptData != null)
    {
      if(scriptData.Remove(name) && scriptData.Count == 0)
      {
        scriptData = null; // free the dictionary when the last variable has been deleted
      }
    }
  }

  public object GetScriptVar(string name)
  {
    if(scriptData == null) throw new KeyNotFoundException();
    return scriptData[name];
  }

  public object SafeGetScriptVar(string name)
  {
    object value;
    TryGetScriptVar(name, out value);
    return value;
  }

  public void SetScriptVar(string name, object value)
  {
    if(scriptData == null)
    {
      scriptData = new Dictionary<string,object>(4);
    }

    scriptData[name] = value;
  }

  public bool TryGetScriptVar(string name, out object value)
  {
    if(scriptData == null)
    {
      value = null;
      return false;
    }
    else
    {
      return scriptData.TryGetValue(name, out value);
    }
  }

  /// <summary>A possibly-null dictionary that contains script data associated with this object.</summary>
  Dictionary<string,object> scriptData;
  /// <summary>The object's name. This may be null.</summary>
  string name;
  /// <summary>A bit field the groups to which the object belongs.</summary>
  uint groups;
}

} // namespace RotationalForce.Engine