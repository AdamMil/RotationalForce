using System;
using System.Collections.Generic;
using System.Drawing;
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

public static class Engine
{
  public static IFileSystem FileSystem
  {
    get { return fileSystem; }
  }

  public static void Initialize(IFileSystem fileSystem, bool autoLoadImageMaps)
  {
    if(fileSystem == null) throw new ArgumentNullException();
    Engine.fileSystem = fileSystem;
    GLVideo.Initialize();
    
    if(autoLoadImageMaps)
    {
      LoadImageMapFiles();
    }
  }

  public static void Deinitialize()
  {
    GLVideo.Deinitialize();
    fileSystem = null;
    UnloadImageMaps();
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
  public static void AddImageMap(ImageMap map)
  {
    if(string.IsNullOrEmpty(map.Name))
    {
      throw new ArgumentException("The image map must have a name before it can be added.");
    }

    ImageMapHandle handle;
    imageMaps.TryGetValue(map.Name, out handle);
    if(handle != null)
    {
      handle.ReplaceMap(map);
    }
    else
    {
      imageMaps[map.Name] = new ImageMapHandle(map);
    }
  }

  public static ImageMapHandle GetImageMap(string mapName)
  {
    int dummy;
    return GetImageMap(mapName, out dummy);
  }

  public static ImageMapHandle GetImageMap(string mapName, out int frameNumber)
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

    ImageMapHandle map = imageMaps[mapName];

    if(hashPos != -1 && (frameNumber < 0 || frameNumber >= map.ImageMap.Frames.Count))
    {
      throw new ArgumentException("Image map '"+mapName+"' does not have a frame numbered "+frameNumber);
    }

    return map;    
  }

  public static ICollection<ImageMapHandle> GetImageMaps()
  {
    return imageMaps.Values;
  }

  public static void RemoveImageMap(string name)
  {
    if(!string.IsNullOrEmpty(name))
    {
      imageMaps.Remove(name);
    }
  }

  static void LoadImageMapFiles()
  {
    foreach(string mapFile in fileSystem.GetFiles(StandardPath.Images, "*.map", true))
    {
      Serializer.BeginBatch();
      try
      {
        ImageMap map = Serializer.Deserialize(fileSystem.OpenForRead(mapFile)) as ImageMap;
        if(map != null)
        {
          AddImageMap(map);
        }
      }
      catch { }
      Serializer.EndBatch();
    }
  }

  internal static void OnImageMapDisposed(ImageMap map)
  {
    ImageMapHandle handle;
    if(!string.IsNullOrEmpty(map.Name) && imageMaps.TryGetValue(map.Name, out handle) && handle.ImageMap == map)
    {
      handle.ReplaceMap(null);
    }
  }

  internal static void OnImageMapNameChanged(ImageMap map, string oldName)
  {
    ImageMapHandle handle;
    if(!string.IsNullOrEmpty(oldName) && imageMaps.TryGetValue(oldName, out handle) && handle.ImageMap == map)
    {
      imageMaps.Remove(oldName);
      imageMaps.Add(map.Name, handle);
    }
    #if DEBUG
    else
    {
      foreach(KeyValuePair<string, ImageMapHandle> de in imageMaps)
      {
        if(de.Value.ImageMap == map) throw new ArgumentException("Old image map name doesn't match old image map.");
      }
    }
    #endif
  }

  static void UnloadImageMaps()
  {
    foreach(ImageMapHandle handle in imageMaps.Values)
    {
      // calling dispose will automatically clear the handle's pointer
      if(handle.ImageMap != null) handle.ImageMap.Dispose();
    }
  }
  #endregion

  static Dictionary<string,ImageMapHandle> imageMaps = new Dictionary<string,ImageMapHandle>();
  static List<ITicker> tickers = new List<ITicker>();
  static IFileSystem fileSystem;
}

} // namespace RotationalForce.Engine