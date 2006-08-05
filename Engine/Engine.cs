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

  public static void Initialize(IFileSystem fileSystem)
  {
    if(fileSystem == null) throw new ArgumentNullException();
    Engine.fileSystem = fileSystem;
    GLVideo.Initialize();
  }

  public static void Deinitialize()
  {
    GLVideo.Deinitialize();
    fileSystem = null;
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

  internal static void OnImageMapDisposed(ImageMap map)
  {
    // TODO: implement once we have image map handling
  }

  internal static void OnImageMapNameChanged(ImageMap map)
  {
    // TODO: implement once we have image map handling
  }

  static List<ITicker> tickers = new List<ITicker>();
  static IFileSystem fileSystem;
}

} // namespace RotationalForce.Engine