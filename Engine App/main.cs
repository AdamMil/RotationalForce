using RotationalForce.Engine;
using TheEngine = RotationalForce.Engine.Engine;
using System.Drawing;
using GameLib.Interop.OpenGL;

namespace RotationalForce
{

public class RectangleObject : SceneObject
{
  public RectangleObject()
  {
    BlendingEnabled = true;
    SetBlendingMode(SourceBlend.SrcAlpha, DestinationBlend.OneMinusSrcAlpha);
  }

  protected override void RenderContent()
  {
    if(BlendingEnabled)
    {
      GL.glEnable(GL.GL_LINE_SMOOTH);
    }

    GL.glBegin(GL.GL_LINE_LOOP);
    GL.glVertex2f(-1f, -1f);
    GL.glVertex2f(1f, -1f);
    GL.glVertex2f(1f, 1f);
    GL.glVertex2f(-1f, 1f);
    GL.glEnd();

    GL.glDisable(GL.GL_LINE_SMOOTH);
  }
}

static class EngineApp
{
  static void Main()
  {
    TheEngine.Initialize(new StandardFileSystem(".", "."), false);
    TheEngine.CreateWindow(800, 600, "Test", ScreenFlag.None);

    Scene scene = new Scene();

    RectangleObject obj = new RectangleObject();
    obj.Width = 50;
    obj.Height = 50;
    obj.X = 120;
    obj.Y = 80;
    obj.BlendColor = Color.Red;
    obj.AddToGroup(1);
    scene.AddObject(obj);

    obj = new RectangleObject();
    obj.Width = 1000;
    obj.Height = 1;
    obj.X = 0;
    obj.Y = 1000;
    obj.AddToGroup(1);
    obj.BlendColor = Color.DarkGreen;
    scene.AddObject(obj);

    obj = new RectangleObject();
    obj.Width = 50;
    obj.Height = 50;
    obj.X = 50;
    obj.Y = 50;
    obj.AddToGroup(1);
    scene.AddObject(obj);

    SceneViewControl view = new SceneViewControl();
    view.Width = 800;
    view.Height = 600;
    view.Scene = scene;
    view.TargetCameraPosition = obj.Position;
    view.TargetCameraZoom = 0.1;
    view.CameraInterpolation = InterpolationMode.Sigmoid;

    DesktopControl desktop = new DesktopControl();
    desktop.Width = 800;
    desktop.Height = 600;
    desktop.AddChild(view);

    bool cameraMoved = false;
    view.StartCameraMove(1.1);
    view.CameraMoved += delegate(SceneViewControl dummy) { cameraMoved = true; };

    while(!cameraMoved)
    {
      TheEngine.Render(desktop);
      TheEngine.Simulate(0.025);
      System.Threading.Thread.Sleep(25);
    }

    obj.SetVelocity(100, 0);
    obj.SetAcceleration(0, 300);
    obj.AngularVelocity = 300;
    view.MountCamera(obj, 0, 0, false);
    view.MountRadius = view.CameraArea.Height * 0.4;

    for(int i=0; i<10000; i += 25)
    {
      TheEngine.Render(desktop);
      TheEngine.Simulate(0.025);
      System.Threading.Thread.Sleep(25);
    }

    TheEngine.Deinitialize();
  }
}

} // namespace RotationalForce