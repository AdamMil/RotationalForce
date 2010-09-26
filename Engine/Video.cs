using System;
using System.Drawing;
using GameLib.Interop.OpenGL;
using GLPoint=AdamMil.Mathematics.Geometry.TwoD.Point;
using GLVideo=GameLib.Video.Video;
using Vector=AdamMil.Mathematics.Geometry.TwoD.Vector;

namespace RotationalForce.Engine
{

static class Video
{
  #region Window space functions
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

  public static void FillBox(Rectangle area, Color color)
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
  #endregion
  
  #region World space functions
  public static void FillCircle(GLPoint center, double radius)
  {
    const int Subdivisions = 32;
    const double AngleScale = Math.PI / (Subdivisions/2); // 2pi/subdivisions. NOTE: revise if Subdivisions is odd

    Vector vector = new Vector(radius, 0);

    GL.glBegin(GL.GL_TRIANGLE_FAN);
      GL.glVertex2d(center);
      GL.glVertex2d(vector);
      for(int i=0; i<Subdivisions; i++)
      {
        GL.glVertex2d(vector.Rotated(AngleScale * i));
      }
    GL.glEnd();
  }
  #endregion

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
  static int screenHeight;
}

} // namespace RotationalForce.Engine