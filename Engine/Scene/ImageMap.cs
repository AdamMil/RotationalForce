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
  protected ImageMap(string imageFile)
  {
    if(string.IsNullOrEmpty(imageFile)) throw new ArgumentException("Image file path cannot be empty.");
    this.imageFile = imageFile;
  }

  ~ImageMap()
  {
    Dispose(true);
  }

  #region Frame
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
  #endregion

  public FilterMode FilterMode
  {
    get { return filterMode; }
    set
    {
      if(value != filterMode)
      {
        FilterMode oldMode = filterMode;
        filterMode = value;
        OnFilterModeChanged(oldMode);
      }
    }
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
    frames[frame].Bind();
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  protected abstract void CalculateFrames();

  protected virtual void Dispose(bool finalizing)
  {
    if(textureID != 0)
    {
      GL.glDeleteTexture(textureID);
      textureID = 0;
    }
  }

  protected void EnsureFrames()
  {
    if(framesDirty)
    {
      if(textureID == 0)
      {
        GL.glGenTexture(out textureID);
      }

      if(textureID != 0)
      {
        CalculateFrames();
        framesDirty = false;
      }
    }
  }

  protected Stream GetImageStream()
  {
    return Engine.FileSystem.OpenForRead(imageFile);
  }

  protected void Invalidate()
  {
    if(frames != null)
    {
      frames.Clear();
    }

    framesDirty = true;
  }

  protected virtual void OnFilterModeChanged(FilterMode oldMode) { }

  [NonSerialized] protected List<Frame> frames;
  [NonSerialized] protected uint textureID;
  string imageFile;
  FilterMode filterMode;
  bool framesDirty;
}
#endregion

#region FullImageMap
public sealed class FullImageMap : ImageMap
{
  public FullImageMap(string imageFile) : base(imageFile) { }

  protected override void CalculateFrames()
  {
    using(Surface surface = new Surface(GetImageStream()))
    {
      Size textureSize;
      OpenGL.TexImage2D(surface, out textureSize);
      frames.Add(new Frame(textureID, 0, 0, (double)surface.Width/textureSize.Width,
                           (double)surface.Height/textureSize.Height));
    }
  }
}
#endregion

#region TiledImageMap
public sealed class TiledImageMap : ImageMap
{
  public TiledImageMap(string imageFile) : base(imageFile) { }
  
  /// <summary>Gets or sets the maximum number of tiles in the image, or can be set to 0 (the default) to have no
  /// limit.
  /// </summary>
  /// <remarks>Normally, the number of tiles is determined by the tile start, size, and stride, and the size of the
  /// source image. For instance, a 128x128 image could be divided into 16 32x32 tiles. However, if only 14 of the 16
  /// tiles contained image data, this property could be set to 14 to limit the number of frames created in the
  /// image map.
  /// </remarks>
  public int TileLimit
  {
    get { return tileLimit; }
    set
    {
      if(value != tileLimit)
      {
        if(value < 0) throw new ArgumentOutOfRangeException("TileLimit", "Tile limit cannot be negative.");
        tileLimit = value;
        Invalidate();
      }
    }
  }

  /// <summary>Gets or sets the number of pixels from the source image that will be used to construct each tile.</summary>
  /// <remarks>If you set this property, you should also set the <see cref="TileStride"/> property.</remarks>
  public Size TileSize
  {
    get { return tileSize; }
    set
    {
      if(value != tileSize)
      {
        if(value.Width <= 0 || value.Height <= 0)
        {
          throw new ArgumentOutOfRangeException("TileSize", "TileSize must be positive.");
        }
        tileSize = value;
        Invalidate();
      }
    }
  }

  /// <summary>Gets or sets the point in the image at which the first tile will be read.</summary>
  /// <remarks>If you have a border around each tile, for instance, you can use this property to set the start index
  /// to the top-left corner of the first tile so that the border does not become part of the tiles.
  /// </remarks>
  public Point TileStart
  {
    get { return tileStart; }
    set
    {
      if(value != tileStart)
      {
        if(value.X < 0 || value.Y < 0)
        {
          throw new ArgumentOutOfRangeException("TileStart", "TileStart cannot be negative.");
        }
        tileStart = value;
        Invalidate();
      }
    }
  }

  /// <summary>Gets or sets the distance between the left edges of tiles stacked horizontally and the top edges of
  /// tiles stacked vertically.
  /// </summary>
  /// <remarks>If you have a border around each tile, for instance, you can set this property to the size of the tile
  /// plus the size of the border, so that the border will be skipped over after each tile is read. The tile stride
  /// dimensions can be negative (if your tiles are not ordered from right to left or bottom to top), but the
  /// magnitudes of the dimensions cannot be smaller than the tile size.
  /// </remarks>
  public Size TileStride
  {
    get { return tileStride; }
    set
    {
      if(value != tileStride)
      {
        if(value.Width == 0 || value.Height == 0)
        {
          throw new ArgumentOutOfRangeException("TileStride", "Tile stride cannot have any zero dimensions.");
        }
        tileStride = value;
        Invalidate();
      }
    }
  }

  protected override void CalculateFrames()
  {
    if(Math.Abs(tileStride.Width) < tileSize.Width || Math.Abs(tileStride.Height) < tileSize.Height)
    {
      throw new InvalidOperationException("Tile stride cannot have any dimensions smaller "+
                                          "in magnitude than the tile size.");
    }

    throw new NotImplementedException();
  }

  protected override void OnFilterModeChanged(FilterMode oldMode)
  {
    // if the old filter mode was nearest neighbor (and the new filter mode is not), and the image doesn't have padded
    // tiles, then padding needs to be added
    if(oldMode == FilterMode.NearestNeighbor && !tilesPadded)
    {
      Invalidate();
    }
  }

  Size tileSize = new Size(32, 32), tileStride = new Size(32, 32);
  Point tileStart;
  int tileLimit;
  bool tilesPadded;
}
#endregion

} // namespace RotationalForce.Engine