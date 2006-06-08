using System.Drawing;
using GameLib.Interop.OpenGL;
using GLVideo = GameLib.Video.Video;


namespace RotationalForce.Engine
{

static class Video
{
  public static void DrawBox(Rectangle area, Color color)
  {
    GL.glColor(color);
    if(color.A != 255) SetBlending();

    GL.glBegin(GL.GL_LINE_LOOP);
      GL.glVertex2i(area.X, area.Y);
      GL.glVertex2i(area.Right-1, area.Y);
      GL.glVertex2i(area.Right-1, area.Bottom-1);
      GL.glVertex2i(area.X, area.Bottom-1);
    GL.glEnd();

    ResetBlending();
  }

  public static void FillRectangle(Rectangle area, Color color)
  {
    GL.glColor(color);
    if(color.A != 255) SetBlending();
    GL.glRecti(area.Left, area.Top, area.Right, area.Bottom);
    ResetBlending();
  }
  
  public static Rectangle GetViewport()
  {
    return viewport;
  }
  
  public static void SetViewport(Rectangle viewport) { SetViewport(screenHeight, viewport); }

  public static void SetViewport(int screenHeight, Rectangle viewport)
  {
    Video.viewport     = viewport;
    Video.screenHeight = screenHeight;

    GL.glViewport(viewport.X, screenHeight-viewport.Bottom, viewport.Width, viewport.Height);
  }

  static void SetBlending()
  {
    GL.glEnable(GL.GL_BLEND);
    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
  }
  
  static void ResetBlending()
  {
    GL.glDisable(GL.GL_BLEND);
  }

  static Rectangle viewport;
  static double pixelScale;
  static int screenHeight;
}

} // namespace RotationalForce.Engine