using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using GameLib.Interop.OpenGL;
using GameLib.Video;
using GLPoint = GameLib.Mathematics.TwoD.Point;

namespace RotationalForce.Engine
{

public enum FilterMode
{
  NearestNeighbor, Bilinear
}

#region ImageMap
public abstract class ImageMap : IDisposable
{
  public ImageMap(string imageFile)
  {
    if(string.IsNullOrEmpty(imageFile)) throw new ArgumentException("Image file path cannot be empty.");
    this.imageFile = imageFile;
  }

  ~ImageMap()
  {
    Dispose(true);
  }

  public sealed class Frame
  {
    internal Frame(uint textureID, double x, double y, double width, double height)
    {
      this.textureID = textureID;
      this.x         = x;
      this.y         = y;
      this.width     = width;
      this.height    = height;
    }

    public GLPoint GetTextureCoord(GLPoint localCoord)
    {
      return new GLPoint(x + localCoord.X*width, y + localCoord.Y*height);
    }

    internal void Bind()
    {
      GL.glBindTexture(GL.GL_TEXTURE_2D, textureID);
    }

    double x, y, width, height;
    uint textureID;
  }

  public ReadOnlyCollection<Frame> Frames
  {
    get
    {
      EnsureFrames();
      return new ReadOnlyCollection<Frame>(frames);
    }
  }

  public string ImageFile
  {
    get { return imageFile; }
  }

  public void BindFrame(int frame)
  {
    EnsureFrames();
    
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  protected abstract void CalculateFrames();

  protected virtual void Dispose(bool finalizing)
  {
    if(texture != null)
    {
      texture.Dispose();
      texture = null;
    }
  }

  protected void EnsureFrames()
  {
    if(framesDirty)
    {
      CalculateFrames();
      framesDirty = false;
    }
  }

  protected void Invalidate(bool invalidateTexture)
  {
    if(frames != null)
    {
      frames.Clear();
    }

    if(invalidateTexture && texture != null)
    {
      texture.Dispose();
      texture = null;
    }

    framesDirty = true;
  }

  [NonSerialized] protected GLTexture2D texture;
  [NonSerialized] protected List<Frame> frames;
  string imageFile;
  bool framesDirty;
}
#endregion

#region FullImageMap
#endregion

} // namespace RotationalForce.Engine