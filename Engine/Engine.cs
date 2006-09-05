using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using GameLib.Interop.OpenGL;
using GLVideo = GameLib.Video.Video;
using SurfaceFlag = GameLib.Video.SurfaceFlag;

namespace RotationalForce.Engine
{

[Flags]
public enum ScreenFlag
{
  /// <summary>The default behavior will be used.</summary>
  None=0,
  /// <summary>Determines whether the window will be resizable by the user or not.</summary>
  Resizeable=1,
  /// <summary>Determines whether the window will be fullscreen.</summary>
  Fullscreen=2,
}

public interface ITicker
{
  void Tick(double timeDelta);
}

public class ResourceNotFoundException : ApplicationException
{
  public ResourceNotFoundException(string resourceName)
    : base("The resource named '"+resourceName+"' could not be found.") { }
}

#region Resource
public abstract class Resource : UniqueObject, IDisposable
{
  ~Resource()
  {
    Dispose(true);
  }

  /// <summary>Gets or sets the friendly name of this resource. This is the name by which objects will reference the
  /// resource.
  /// </summary>
  public string Name
  {
    get { return name; }
    set
    {
      if(string.IsNullOrEmpty(value))
      {
        throw new ArgumentException("Resource name cannot be null or empty.");
      }
      if(!string.Equals(name, value, StringComparison.Ordinal))
      {
        string oldName = name;
        name = value;
        Engine.OnResourceNameChanged(this, oldName);
      }
    }
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  protected virtual void Dispose(bool finalizing)
  {
    Engine.OnResourceDisposed(this);
  }

  string name;
}
#endregion

#region ResourceHandle
/// <summary>This class exists to support collections of resource handles that point to arbitrary resources.
/// This class should not need to be used directly by user code. Use <see cref="ResourceHandle"/> instead.
/// </summary>
public abstract class ResourceHandleBase : NonSerializableObject
{
  internal ResourceHandleBase() { } // prevent external derivation
  /// <summary>Clears the resource handle. Called when the resource is manually disposed.</summary>
  internal abstract void ClearResource();
  /// <summary>Disposes the resource and clears the resource handle.</summary>
  internal abstract void DisposeResource();
  internal abstract Resource GetResource();
}

/// <summary>Represents a handle to a named resource.</summary>
/// <remarks>The purpose of this class is to allow the engine to change the resource and have objects update
/// automatically. The objects hold a reference to the <see cref="ResourceHandle"/> rather than the resource, and
/// the engine will take care of updating the handles.
/// </remarks>
public sealed class ResourceHandle<T> : ResourceHandleBase where T : Resource
{
  /// <param name="resource">The initial resource value.</param>
  internal ResourceHandle(T resource) // prevent external creation
  {
    SetResource(resource);
  }

  /// <summary>Gets the resource referenced by the handle. This will be null if the resource associated
  /// with the name was deleted.
  /// </summary>
  public T Resource
  {
    get { return resource; }
  }

  internal override void ClearResource()
  {
    SetResource(null);
  }

  /// <summary>Disposes the resource if it supports <see cref="IDisposable"/>, and clears the resource pointer.</summary>
  internal override void DisposeResource()
  {
    IDisposable disp = resource as IDisposable;
    if(disp != null)
    {
      disp.Dispose();
    }
    resource = null;
  }

  internal override Resource GetResource()
  {
    return resource;
  }

  /// <summary>Called to set the resource referenced by this handled.</summary>
  internal void SetResource(T newResource)
  {
    resource = newResource;
  }

  T resource;
}
#endregion

#region ResourceKeyAttribute
/// <summary>Used to mark classes that form groups of named resources.</summary>
/// <remarks>As an example, the abstract <see cref="ImageMap"/> class is marked with this attribute, and the
/// concrete classes <see cref="FullImageMap"/> and <see cref="TiledImageMap"/> derive from it. The effect is that
/// all image maps, regardless of which concrete type they are, can be accessed through the base type. Otherwise, the
/// different types of image maps would not be grouped together and could not be retrieved without knowing their types.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited=false)]
public class ResourceKeyAttribute : Attribute
{
}
#endregion

#region Engine
public static class Engine
{
  public static IFileSystem FileSystem
  {
    get { return fileSystem; }
  }

  public static void Initialize(IFileSystem fileSystem, bool autoLoadResources)
  {
    if(initialized)
    {
      throw new InvalidOperationException("The engine has already been initialized. If you want to re-initialize it, "+
                                          "call Deinitialize first.");
    }

    if(fileSystem == null) throw new ArgumentNullException();
    Engine.fileSystem = fileSystem;
    GLVideo.Initialize();
    initialized = true;

    try
    {
      if(autoLoadResources)
      {
        LoadResources<ImageMap>(StandardPath.Images, "*.map"); // load images first because animations may depend on them
        LoadResources<Animation>(StandardPath.Animations, "*.anim");
        LoadResources<VectorShape>(StandardPath.Animations, "*.shape");
      }
    }
    catch
    {
      Deinitialize();
      throw;
    }
  }

  public static void Deinitialize()
  {
    AssertInitialized();
    GLVideo.Deinitialize();
    fileSystem = null;
    UnloadResources();
    resources.Clear();
    initialized = false;
  }

  public static void CreateWindow(int width, int height, string windowTitle, ScreenFlag flags)
  {
    SurfaceFlag sFlags = SurfaceFlag.DoubleBuffer;
    if((flags & ScreenFlag.Fullscreen) != 0) sFlags |= SurfaceFlag.Fullscreen;
    if((flags & ScreenFlag.Resizeable) != 0) sFlags |= SurfaceFlag.Resizeable;

    GLVideo.SetGLMode(800, 600, 0, sFlags);
    GameLib.Video.WM.WindowTitle = windowTitle;

    ResetOpenGL();
  }

  public static void Render(DesktopControl desktop)
  {
    desktop.Invalidate();
    desktop.Render();
    GLVideo.Flip();
  }

  public static void ResetOpenGL() { ResetOpenGL(GLVideo.Width, GLVideo.Height, GLVideo.DisplaySurface.Bounds); }

  public static void ResetOpenGL(int screenWidth, int screenHeight, Rectangle viewport)
  {
    // everything is parallel to the screen, so perspective correction is not necessary
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_FASTEST);

    GL.glHint(GL.GL_LINE_SMOOTH_HINT, GL.GL_NICEST); // these look nice
    GL.glHint(GL.GL_POLYGON_SMOOTH_HINT, GL.GL_NICEST);

    GL.glDisable(GL.GL_DEPTH_TEST);  // typically, we're drawing in order, so this isn't needed.
    GL.glDisable(GL.GL_CULL_FACE);   // everything is forward-facing, so we don't need culling.
    GL.glDisable(GL.GL_BLEND);       // things that need blending will enable it.
    GL.glDisable(GL.GL_LINE_SMOOTH); // smoothing requires blending, so the rule above applies.
    GL.glDisable(GL.GL_POLYGON_SMOOTH); // ditto
    GL.glDisable(GL.GL_TEXTURE_2D);  // things that need texturing will enable it.
    GL.glDisable(GL.GL_LIGHTING);    // things that need lighting will enable it.
    GL.glDisable(GL.GL_DITHER);      // don't need this.
    GL.glClearColor(0, 0, 0, 0);     // clear to black by default.
    GL.glLineWidth(1);               // lines will be one pixel thick.
    GL.glPointSize(1);               // pixels, too.
    GL.glShadeModel(GL.GL_FLAT);     // use flat shading by default
    GL.glBlendFunc(GL.GL_ONE, GL.GL_ZERO); // use the default blend mode

    GL.glBindTexture(GL.GL_TEXTURE_2D, 0); // unbind any bound texture

    GL.glMatrixMode(GL.GL_PROJECTION);
    GL.glLoadIdentity();
    GLU.gluOrtho2D(0, viewport.Width, viewport.Height, 0);

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glLoadIdentity();
    GL.glTranslated(0.375, 0.375, 0); // opengl "exact pixelization" hack described in OpenGL Redbook appendix H

    Video.SetViewport(screenHeight, viewport);
  }

  public static void Simulate(double timeDelta)
  {
    EngineMath.AssertValidFloat(timeDelta);
    if(timeDelta < 0) throw new ArgumentOutOfRangeException("timeDelta", "Time delta cannot be negative.");

    for(int i=0; i<tickers.Count; i++)
    {
      tickers[i].Tick(timeDelta);
    }
  }

  internal static void AddTicker(ITicker ticker)
  {
    if(ticker == null) throw new ArgumentNullException("ticker");
    if(!tickers.Contains(ticker)) tickers.Add(ticker);
  }

  internal static void RemoveTicker(ITicker ticker)
  {
    if(ticker == null) throw new ArgumentNullException("ticker");
    tickers.Remove(ticker);
  }

  #region Image maps
  public static ResourceHandle<ImageMap> GetImageMap(string mapName)
  {
    int dummy;
    return GetImageMap(mapName, out dummy);
  }

  public static ResourceHandle<ImageMap> GetImageMap(string mapName, out int frameNumber)
  {
    if(string.IsNullOrEmpty(mapName))
    {
      throw new ArgumentException("Image map name cannot be null or empty.");
    }

    int hashPos = mapName.IndexOf('#');
    if(hashPos == -1)
    {
      frameNumber = 0;
    }
    else
    {
      int.TryParse(mapName.Substring(hashPos+1), out frameNumber);
      mapName = mapName.Substring(0, hashPos);
    }

    ResourceHandle<ImageMap> map = GetResource<ImageMap>(mapName);

    if(hashPos != -1 && (frameNumber < 0 || frameNumber >= map.Resource.Frames.Count))
    {
      throw new ArgumentException("Image map '"+mapName+"' does not have a frame numbered "+frameNumber);
    }

    return map;    
  }
  #endregion

  #region Resources
  /// <summary>Adds a resource to the resource registry. This method will overwrite any resource of the same name
  /// and type.
  /// </summary>
  public static void AddResource<T>(Resource resource) where T : Resource
  {
    AssertInitialized();

    if(resource == null) throw new ArgumentNullException();
    if(string.IsNullOrEmpty(resource.Name))
    {
      throw new ArgumentException("The resource must have a name before it can be added.");
    }

    string key = GetResourcePrefix(resource) + resource.Name;

    ResourceHandle<T> handle;
    if(TryGetHandle(key, out handle))
    {
      handle.SetResource((T)resource);
    }
    else
    {
      resources[key] = new ResourceHandle<T>((T)resource);
    }
  }

  /// <summary>Retrieves a resource with the given name.</summary>
  /// <exception cref="ResourceNotFoundException">Thrown if the resource could not be found.</exception>
  public static ResourceHandle<T> GetResource<T>(string name) where T : Resource
  {
    AssertInitialized();
    ResourceHandleBase handle;
    if(!resources.TryGetValue(GetResourcePrefix(typeof(T)) + name, out handle))
    {
      throw new ResourceNotFoundException(name);
    }
    return (ResourceHandle<T>)handle;
  }

  /// <summary>Retrieves all the resources of a given type.</summary>
  public static ICollection<ResourceHandle<T>> GetResources<T>() where T : Resource
  {
    AssertInitialized();

    List<ResourceHandle<T>> handles = new List<ResourceHandle<T>>();
    foreach(ResourceHandleBase baseHandle in resources.Values)
    {
      ResourceHandle<T> handle = baseHandle as ResourceHandle<T>;
      if(handle != null) handles.Add(handle);
    }
    return handles;
  }

  /// <summary>Determines whether a resource with the given name exists.</summary>
  public static bool HasResource<T>(string name) where T : Resource
  {
    AssertInitialized();
    return resources.ContainsKey(GetResourcePrefix(typeof(T)) + name);
  }

  public static void UnloadResource<T>(ResourceHandle<T> handle) where T : Resource
  {
    if(handle == null) throw new ArgumentNullException();
    handle.DisposeResource();
  }

  public static void UnloadResource<T>(string name, bool removeHandle) where T : Resource
  {
    UnloadResource(GetResource<T>(name));
    if(removeHandle)
    {
      resources.Remove(GetResourcePrefix(typeof(T)) + name);
    }
  }

  static string GetResourcePrefix(Type resourceType)
  {
    Type keyType;
    if(!resourceKeyTypes.TryGetValue(resourceType, out keyType))
    {
      keyType = resourceType;
      // find the parent type with the ResourceKey attribute
      while(keyType != null && keyType.GetCustomAttributes(typeof(ResourceKeyAttribute), false).Length == 0)
      {
        keyType = keyType.BaseType;
      }

      if(keyType == null)
      {
        throw new ArgumentException("Could not find a class marked with ResourceKeyAttribute in the inheritence chain.");
      }
      
      resourceKeyTypes.Add(resourceType, keyType);
    }

    return keyType.FullName + ":";
  }

  static string GetResourcePrefix<T>(T resource) where T : Resource
  {
    return GetResourcePrefix(resource.GetType());
  }

  static void LoadResources<T>(string basePath, string wildcard) where T : Resource
  {
    foreach(string file in fileSystem.GetFiles(basePath, wildcard, true))
    {
      Serializer.BeginBatch();
      using(SexpReader reader = new SexpReader(fileSystem.OpenForRead(file)))
      {
        while(!reader.EOF)
        {
          T resource = null;
          try { resource = Serializer.Deserialize(reader) as T; }
          catch { }
          if(resource != null)
          {
            AddResource<T>(resource);
          }
        }
      }
      Serializer.EndBatch();
    }
  }

  internal static void OnResourceDisposed(Resource resource)
  {
    ResourceHandleBase handle;
    if(TryGetHandle(resource, out handle))
    {
      handle.ClearResource();
    }
  }

  internal static void OnResourceNameChanged(Resource resource, string oldName)
  {
    ResourceHandleBase handle;
    string prefix = GetResourcePrefix(resource);
    if(TryGetHandle(prefix, oldName, out handle) && handle.GetResource() == resource)
    {
      resources.Remove(prefix + oldName);
      resources.Add(prefix + resource.Name, handle);
    }
  }

  static bool TryGetHandle<T>(Resource resource, out T handle) where T : ResourceHandleBase
  {
    return TryGetHandle(GetResourcePrefix(resource), resource.Name, out handle) && handle.GetResource() == resource;
  }

  static bool TryGetHandle<T>(string prefix, string resourceName, out T handle) where T : ResourceHandleBase
  {
    if(!string.IsNullOrEmpty(resourceName) && TryGetHandle(prefix + resourceName, out handle))
    {
      return true;
    }
    else
    {
      handle = null;
      return false;
    }
  }

  static bool TryGetHandle<T>(string key, out T handle) where T : ResourceHandleBase
  {
    ResourceHandleBase baseHandle;
    handle = resources.TryGetValue(key, out baseHandle) ? (T)baseHandle : null;
    return handle != null;
  }

  static void UnloadResources()
  {
    foreach(ResourceHandleBase baseHandle in resources.Values)
    {
      baseHandle.DisposeResource();
    }
    resources.Clear();
  }
  #endregion
  
  static void AssertInitialized()
  {
    if(!initialized) throw new InvalidOperationException("The engine has not been initialized yet.");
  }

  static Dictionary<string,ResourceHandleBase> resources = new Dictionary<string,ResourceHandleBase>();
  static Dictionary<Type,Type> resourceKeyTypes = new Dictionary<Type,Type>(8);
  static List<ITicker> tickers = new List<ITicker>();
  static IFileSystem fileSystem;
  static bool initialized;
}
#endregion

} // namespace RotationalForce.Engine