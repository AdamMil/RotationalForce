using System;
using System.ComponentModel;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;

namespace RotationalForce.Engine
{

public class StaticImageObject : SceneObject
{
  /// <summary>Gets or sets the image map name with an optional frame number.</summary>
  [Category("Rendering")]
  [Description("The name of the image map to display, with an optional zero-based frame number. The format is "+
               "imageMapName (eg, player) or imageMapName#frameNumber (eg, player#2).")]
  public string ImageMap
  {
    get { return imageMapName; }
    set
    {
      if(!string.Equals(value, imageMapName, System.StringComparison.Ordinal))
      {
        imageMapName = value;

        if(string.IsNullOrEmpty(imageMapName))
        {
          mapHandle = null;
        }
        else
        {
          mapHandle = Engine.GetImageMap(imageMapName, out frameNumber);
        }
      }
    }
  }

  protected override void Deserialize(DeserializationStore store)
  {
    base.Deserialize(store);

    if(!string.IsNullOrEmpty(imageMapName)) // reload the image map handle if we had one before
    {
      mapHandle = Engine.GetImageMap(imageMapName, out frameNumber);
    }
  }

  protected override void RenderContent(float screenSize)
  {
    ImageMap imageMap = mapHandle == null ? null : mapHandle.Resource;

    // use default rendering if the image map is null or the frame number is invalid
    if(imageMap == null || frameNumber >= imageMap.Frames.Count)
    {
      base.RenderContent(screenSize);
    }
    else
    {
      GL.glEnable(GL.GL_TEXTURE_2D);
      imageMap.BindFrame(frameNumber);
      GL.glBegin(GL.GL_QUADS);
        GL.glTexCoord2d(imageMap.GetTextureCoord(frameNumber, new Point(0, 0)));
        GL.glVertex2d(-1, -1);
        GL.glTexCoord2d(imageMap.GetTextureCoord(frameNumber, new Point(1, 0)));
        GL.glVertex2d(1, -1);
        GL.glTexCoord2d(imageMap.GetTextureCoord(frameNumber, new Point(1, 1)));
        GL.glVertex2d(1, 1);
        GL.glTexCoord2d(imageMap.GetTextureCoord(frameNumber, new Point(0, 1)));
        GL.glVertex2d(-1, 1);
      GL.glEnd();
      GL.glDisable(GL.GL_TEXTURE_2D);
    }
  }

  string imageMapName;
  [NonSerialized] ResourceHandle<ImageMap> mapHandle;
  [NonSerialized] int frameNumber;
}

} // namespace RotationalForce.Engine