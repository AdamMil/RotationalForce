using System;
using System.Collections.Generic;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
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
  #region Tickers
  public static void AddTicker(ITicker ticker)
  {
    if(ticker == null) throw new ArgumentNullException("ticker");
    if(!tickers.Contains(ticker)) tickers.Add(ticker);
  }

  public static void ClearTickers() { tickers.Clear(); }

  public static void RemoveTicker(ITicker ticker)
  {
    if(ticker == null) throw new ArgumentNullException("ticker");
    tickers.Remove(ticker);
  }

  public static void Simulate(double timeDelta)
  {
    for(int i=0; i<tickers.Count; i++)
    {
      tickers[i].Tick(timeDelta);
    }
  }
  #endregion

  #region Initialize and Deinitialize
  public static void Initialize()
  {
    GLVideo.Initialize();
  }

  public static void Deinitialize()
  {
    ClearTickers();
    GLVideo.Deinitialize();
  }
  #endregion

  public static void CreateWindow(int width, int height, string windowTitle, ScreenFlag flags)
  {
    SurfaceFlag sFlags = SurfaceFlag.DoubleBuffer;
    if((flags & ScreenFlag.Fullscreen) != 0) sFlags |= SurfaceFlag.Fullscreen;
    if((flags & ScreenFlag.Resizeable) != 0) sFlags |= SurfaceFlag.Resizeable;

    GLVideo.SetGLMode(800, 600, 0, sFlags);
    GameLib.Video.WM.WindowTitle = windowTitle;

    ResetOpenGL();
  }

  public static void ResetOpenGL()
  {
    // everything is parallel to the screen, so perspective correction is not necessary
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_FASTEST);
    GL.glDisable(GL.GL_DEPTH_TEST);  // typically, we're drawing in order, so this isn't needed.
    GL.glDisable(GL.GL_CULL_FACE);   // everything is forward-facing, so we don't need culling.
    GL.glDisable(GL.GL_BLEND);       // things that need blending will enable it.
    GL.glDisable(GL.GL_LINE_SMOOTH); // smoothing requires blending, so the rule above applies.
    GL.glDisable(GL.GL_POLYGON_SMOOTH); // ditto
    GL.glDisable(GL.GL_TEXTURE_2D);  // things that need texturing will enable it.
    GL.glDisable(GL.GL_LIGHTING);    // things that need lighting will enable it.
    GL.glDisable(GL.GL_DITHER);      // don't need this.
    GL.glClearColor(0, 0, 0, 0);

    GL.glMatrixMode(GL.GL_PROJECTION);
    GL.glLoadIdentity();
    GLU.gluOrtho2D(0, GLVideo.Width, GLVideo.Height, 0);

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glLoadIdentity();
    GL.glTranslated(0.375, 0.375, 0); // opengl "exact pixelization" hack described in Redbook appendix H

    Video.SetViewport(GLVideo.DisplaySurface.Bounds);
  }
  
  public static void Render(DesktopControl desktop)
  {
    desktop.Invalidate();
    desktop.Render();
    GLVideo.Flip();
  }
  
  static List<ITicker> tickers = new List<ITicker>();
}

} // namespace RotationalForce.Engine