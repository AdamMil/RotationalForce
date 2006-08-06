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

#region Texture enums
/// <summary>The type of texture filtering that will be performed.</summary>
public enum FilterMode : byte
{
  /// <summary>No filtering will be done for either magnification or minification. This is the default.</summary>
  None,
  /// <summary>Bilinear filtering will be done for magnification and minification.</summary>
  Smooth,
}

/// <summary>Determines how texture coordinates outside the range of 0 to 1 will be handled.</summary>
/// <remarks>Note that this only applies to the final OpenGL coordinates, not the coordinates within a single frame
/// (frame coordinates). See <see cref="ImageMap.TextureWrap"/> for more information.
/// </remarks>
public enum TextureWrap : byte
{
  /// <summary>Texture coordinates will be clamped to the range of 0 to 1. This is the default.</summary>
  Clamp,
  /// <summary>Texture the integral part of the texture coordinate will be ignored, so texture values will be wrapped
  /// around (eg, 1.6 becomes 0.6).
  /// </summary>
  Repeat
}
#endregion

#region ImageMap
/// <summary>The base class for image maps, which represent one or more image frames within a single OpenGL texture.</summary>
public abstract class ImageMap : UniqueObject, IDisposable
{
  protected ImageMap(string mapName, string imageFile)
  {
    if(string.IsNullOrEmpty(imageFile)) throw new ArgumentException("Image file path cannot be empty.");
    this.imageFile = imageFile;
    this.Name = mapName;
  }
  
  /// <summary>Initializes the image map when it's being deserialized.</summary>
  /// <remarks>Derived classes should provide a constructor with the same signature (it can be private). This is
  /// necessary to allow the ImageMap to be deserialized, because it does not have a default constructor.
  /// </remarks>
  protected ImageMap(ISerializable dummy) { }

  ~ImageMap()
  {
    Dispose(true);
  }

  #region Frame
  public sealed class Frame
  {
    internal Frame(uint textureID, Rectangle imageArea, Size textureSize)
    {
      this.textureID = textureID;
      this.x         = (double)imageArea.X / textureSize.Width;
      this.y         = (double)imageArea.Y / textureSize.Height;
      this.width     = (double)imageArea.Width  / textureSize.Width;
      this.height    = (double)imageArea.Height / textureSize.Height;
      this.imageSize = imageArea.Size;
      
      // determine which parts of this image need edge clamping
      this.clampLeft   = imageArea.X == 0;
      this.clampTop    = imageArea.Y == 0;
      this.clampRight  = imageArea.Right  == textureSize.Width;
      this.clampBottom = imageArea.Bottom == textureSize.Height;
    }

    public Size Size
    {
      get { return imageSize; }
    }

    internal GLPoint GetTextureCoord(GLPoint frameCoord)
    {
      return new GLPoint(x + frameCoord.X*width, y + frameCoord.Y*height);
    }

    internal void Bind()
    {
      GL.glBindTexture(GL.GL_TEXTURE_2D, textureID);
    }

    internal double x, y, width, height;
    uint textureID;
    Size imageSize;
    internal bool clampLeft, clampRight, clampTop, clampBottom;
  }
  #endregion

  /// <summary>Gets or sets the type of texture filtering that will be performed.</summary>
  /// <remarks>Texture filtering can increase the quality of the image when it's being rendered at a size larger or
  /// smaller than its usual size, but it also blurs the image slightly, reduces rendering performance, and can
  /// increase memory usage due to additional padding that needs to be added between frames in an image (possibly
  /// bumping the texture size up to the next power of two). The default is <see cref="FilterMode.None"/>.
  /// </remarks>
  public FilterMode FilterMode
  {
    get { return filterMode; }
    set
    {
      if(value != filterMode)
      {
        filterMode = value;
        OnFilterModeChanged();
        modeDirty = true;
      }
    }
  }

  /// <summary>Gets a collection of <see cref="Frame">Frames</see> in the image map.</summary>
  public ReadOnlyCollection<Frame> Frames
  {
    get
    {
      EnsureFrames();
      return new ReadOnlyCollection<Frame>(frames);
    }
  }

  /// <summary>Gets the path of the image file providing the data for this image map. This is the same as the path
  /// passed to the constructor.
  /// </summary>
  public string ImageFile
  {
    get { return imageFile; }
  }

  /// <summary>Gets or sets the friendly name of this image map. This is the name by which objects will reference the
  /// image map.
  /// </summary>
  public string Name
  {
    get { return name; }
    set
    {
      if(string.IsNullOrEmpty(value))
      {
        throw new ArgumentException("Image map name cannot be null or empty.");
      }
      name = value;
      Engine.OnImageMapNameChanged(this);
    }
  }

  /// <summary>Gets or sets how texture coordinates outside the range of 0 to 1 will be handled.</summary>
  /// <remarks>Note that this only applies to the final OpenGL coordinates, not the coordinates within a single frame
  /// (frame coordinates). This essentially means that you cannot safely use frame coordinates outside the range
  /// 0 to 1, unless you know that frame coordinates correspond directly to OpenGL texture coordinates. This is
  /// guaranteed in only one case -- when using a <see cref="FullImageMap"/> with an image that has dimensions that are
  /// powers of two in length. The default is <see cref="TextureWrap.Clamp"/>.
  /// </remarks>
  public TextureWrap TextureWrap
  {
    get { return textureWrap; }
    set
    {
      if(value != textureWrap)
      {
        textureWrap = value;
        modeDirty = true;
      }
    }
  }

  /// <summary>Gets or sets the OpenGL texture priority for this image map, as a value from 0 to 1.</summary>
  /// <remarks>The texture priority determines how OpenGL's memory manager will determine the set of textures to keep
  /// in texture memory. Textures kept in texture memory are much more efficient than those not, so you should set the
  /// priority of important and commonly-used textures higher than less important or more rarely-used textures. The
  /// default is 0.5.
  /// </remarks>
  public float Priority
  {
    get { return priority; }
    set
    {
      if(value != priority)
      {
        if(value < 0 || value > 1)
        {
          throw new ArgumentOutOfRangeException("Priority", "Texture priority must be from 0 to 1");
        }

        priority = value;

        if(textureID != 0) // if the texture is already allocated, set its priority immediately
        {
          GL.glPrioritizeTexture(textureID, priority);
        }
      }
    }
  }

  /// <summary>Binds a frame from this image map as the current OpenGL texture.</summary>
  /// <param name="frame">The index of the <see cref="Frame"/> to bind.</param>
  public void BindFrame(int frameIndex)
  {
    EnsureFrames();
    frames[frameIndex].Bind();

    if(modeDirty) // if the texture mode has changed, tell GL about it
    {
      uint textureWrap = TextureWrap == TextureWrap.Clamp ? GL.GL_CLAMP : GL.GL_REPEAT;
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, textureWrap);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, textureWrap);

      bool nearestNeighbor = FilterMode == FilterMode.None;
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, nearestNeighbor ? GL.GL_NEAREST : GL.GL_LINEAR);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, nearestNeighbor ? GL.GL_NEAREST : GL.GL_LINEAR);
      modeDirty = false;
    }
  }

  /// <summary>Converts a point from frame coordinates to texture coordinates.</summary>
  /// <param name="frame">The index of the <see cref="Frame"/>.</param>
  /// <param name="frameCoord">The frame coordinates. This should be from 0 to 1, unless you know that texture wrapping
  /// will work for this texture. See <see cref="TextureWrap"/> for more details about texture wrapping.
  /// </param>
  /// <returns>Returns the OpenGL texture coordinates corresponding to the coordinates within the frame.</returns>
  public GLPoint GetTextureCoord(int frameIndex, GLPoint frameCoord)
  {
    EnsureFrames();
    Frame frame = frames[frameIndex];
    GLPoint pt = frame.GetTextureCoord(frameCoord);

    if(filterMode == FilterMode.Smooth && textureWrap == TextureWrap.Clamp) // if we need edge clamping
    {
      if(frame.clampLeft && pt.X < leftEdge) pt.X = leftEdge;
      else if(frame.clampRight && pt.X > rightEdge) pt.X = rightEdge;
      if(frame.clampTop && pt.Y < topEdge) pt.Y = topEdge;
      else if(frame.clampBottom && pt.Y > bottomEdge) pt.Y = bottomEdge;
    }
    
    return pt;
  }

  /// <summary>Releases the OpenGL resources held by this image map.</summary>
  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  /// <summary>Called when the frames and texture data need to be calculated.</summary>
  /// <param name="textureSize">A return value that should be set to the size of the texture used.</param>
  /// <returns>Returns true if the operation was successful and <paramref name="textureSize"/> is valid, or false if
  /// <paramref name="textureSize"/> is not valid (eg, because there are no frames in the image map).
  /// </returns>
  /// <remarks>When called, the <see cref="textureID"/> field will name a valid OpenGL texture and the
  /// <see cref="frames"/> collection will be empty.
  /// The base class does not require the texture data to be loaded any time except the first. (The base class
  /// will not free the texture data until the class is disposed.), so if derived classes can avoid reloading the
  /// texture data, they should.
  /// </remarks>
  protected abstract bool CalculateFrames(out Size textureSize);

  /// <summary>Called when the image map is being disposed.</summary>
  /// <param name="finalizing">True if the object is being finalized.</param>
  /// <remarks>Derived classes can override this to release additional resources, but should always call the base.</remarks>
  protected virtual void Dispose(bool finalizing)
  {
    Engine.OnImageMapDisposed(this);

    if(textureID != 0)
    {
      GL.glDeleteTexture(textureID);
      textureID = 0;
    }
  }

  /// <summary>Called to ensure that the frames and texture data are recalculated if necessary.</summary>
  /// <remarks>Note that if OpenGL cannot allocate the texture, the frames collection will not be populated!</remarks>
  protected void EnsureFrames()
  {
    if(framesDirty)
    {
      // clear the 'framesDirty' flag immediately so that if something fails, we don't keep retrying 100 times per
      // frame.
      // TODO: think about how to communicate texture loading failure to the user of the engine
      framesDirty = false;

      if(textureID == 0) // try to allocate the texture if that hasn't been done yet
      {
        GL.glGenTexture(out textureID);
      }

      if(textureID != 0) // if the texture was allocated
      {
        uint oldTextureID;
        GL.glGetIntegerv(GL.GL_TEXTURE_BINDING_2D, out oldTextureID);

        try
        {
          GL.glBindTexture(GL.GL_TEXTURE_2D, textureID); // bind our texture

          Size textureSize;
          if(CalculateFrames(out textureSize))
          {
            // get the size of half a texel. when edge clamping (performed when clamping and filtering), we'll need to
            // clamp X to the range [xHalfTexel, 1-xHalfTexel] instead of [0,1] and similar for Y. this prevents the
            // filter from spilling over the edge of the texture, causing ugly artifacts. this can be done by using
            // GL_CLAMP_EDGE instead of GL_CLAMP, but not all GL implementations support it.
            double xHalfTexel = 0.5 / textureSize.Width, yHalfTexel = 0.5 / textureSize.Height;

            this.leftEdge   = xHalfTexel;
            this.topEdge    = yHalfTexel;
            this.rightEdge  = 1 - xHalfTexel;
            this.bottomEdge = 1 - yHalfTexel;
          }

          GL.glPrioritizeTexture(textureID, priority); // set the texture priority
        }
        finally
        {
          GL.glBindTexture(GL.GL_TEXTURE_2D, oldTextureID); // and restore the old texture when we're done
        }
      }
    }
  }

  /// <summary>Given a surface without an alpha channel, guesses the appropriate color key.</summary>
  /// <returns>A raw color that can be passed to <see cref="Surface.SetColorKey(uint)"/>.</returns>
  protected uint GetColorKey(Surface surface)
  {
    // uses palette entry 0 for 8-bit images and the color of the top-left pixel for other images
    return surface.Format.Depth == 8 ? 0 : surface.GetPixelRaw(0, 0);
  }

  /// <summary>Returns a <see cref="Stream"/> containing the image data. This stream should be closed when the derived
  /// class is done with it.
  /// </summary>
  protected Stream GetImageStream()
  {
    return Engine.FileSystem.OpenForRead(imageFile);
  }

  /// <summary>Invalidates the <see cref="Frames"/> so that <see cref="CalculateFrames"/> will be called the next time
  /// the frames are accessed.
  /// </summary>
  protected void Invalidate()
  {
    frames.Clear();
    framesDirty = true;
  }

  /// <summary>Called when the <see cref="FilterMode"/> is changed. Derived classes can handle this to invalidate the
  /// frame data (by calling <see cref="Invalidate"/>) if necessary.
  /// </summary>
  protected virtual void OnFilterModeChanged() { }

  [NonSerialized] protected List<Frame> frames = new List<Frame>();
  [NonSerialized] protected uint textureID;

  double leftEdge, rightEdge, topEdge, bottomEdge;
  string imageFile, name;
  float priority = 0.5f;
  FilterMode filterMode;
  TextureWrap textureWrap = TextureWrap.Clamp;
  [NonSerialized] bool framesDirty = true, modeDirty = true;
}
#endregion

#region FullImageMap
/// <summary>Loads an entire image as a single frame.</summary>
public sealed class FullImageMap : ImageMap
{
  public FullImageMap(string mapName, string imageFile) : base(mapName, imageFile) { }
  
  FullImageMap(ISerializable dummy) : base(dummy) { }

  protected override bool CalculateFrames(out Size textureSize)
  {
    using(Surface surface = new Surface(GetImageStream()))
    {
      // if the surface doesn't have an alpha channel, we'll use the color key
      if(surface.Format.AlphaMask == 0 && surface.Width > 0 && surface.Height > 0)
      {
        surface.SetColorKey(GetColorKey(surface));
      }

      OpenGL.TexImage2D(surface, out textureSize);
      frames.Add(new Frame(textureID, new Rectangle(0, 0, surface.Width, surface.Height), textureSize));
      return true;
    }
  }
}
#endregion

#region TiledImageMap
/// <summary>Loads an image as a set of tiles, creating multiple frames in a single texture.</summary>
public sealed class TiledImageMap : ImageMap
{
  public TiledImageMap(string mapName, string imageFile) : base(mapName, imageFile) { }

  TiledImageMap(ISerializable dummy) : base(dummy) { }

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

  protected override bool CalculateFrames(out Size textureSize)
  {
    if(Math.Abs(tileStride.Width) < tileSize.Width || Math.Abs(tileStride.Height) < tileSize.Height)
    {
      throw new InvalidOperationException("Tile stride cannot have any dimensions smaller "+
                                          "in magnitude than the tile size.");
    }
    
    using(Surface surface = new Surface(GetImageStream()))
    {
      textureSize = new Size();

      // find how much space we have available to retrieve tiles from. if the stride is negative, we'll move from the
      // start point (plus the first tile's size) towards the left/top. otherwise, we'll move from the start point
      // towards the right/bottom.
      Size availableSpace =
        new Size(tileStride.Width  > 0 ? surface.Width  - tileStart.X : tileStart.X + tileSize.Width,
                 tileStride.Height > 0 ? surface.Height - tileStart.Y : tileStart.Y + tileSize.Height);

      int numTiles;

      // if the space isn't enough to get any tiles from, return immediately. (this simplifies the calculations below)
      if(availableSpace.Width < tileSize.Width || availableSpace.Height < tileSize.Height)
      {
        return false;
      }
      // otherwise, make sure there's enough room to read the first tile
      else if(tileStart.X + tileSize.Width > surface.Width || tileStart.Y + tileSize.Height > surface.Height)
      {
        throw new InvalidOperationException("The start point must be positioned such that there is enough room for a "+
                                            "tile of size TileSize with its top-left pixel at the start point.");
      }
      else
      {
        // calculate the number of tiles. the first tile retrieved is not affected by the stride, only subsequent ones,
        // so we subtract the tile size, divide by the stride, and add one (for the first tile that was subtracted out)
        int horzTiles = (availableSpace.Width  - tileSize.Width)  / Math.Abs(tileStride.Width)  + 1;
        int vertTiles = (availableSpace.Height - tileSize.Height) / Math.Abs(tileStride.Height) + 1;
        numTiles = horzTiles * vertTiles; // calculate the total number of tiles
        if(tileLimit != 0 && numTiles > tileLimit) numTiles = tileLimit; // and apply the tile limit if there is one
      }

      // use a simple, brute force algorithm to calculate the optimum texture size given filter padding and OpenGL
      // texture size limitations. for 10 tiles, it will try 10x1, 5x2, 4x3, 3x4, 2x5, and 1x10
      for(int texturePixels=int.MaxValue, numRows=1; numRows <= numTiles; ) // numRows is incremented at the bottom
      {
        int numCols = (numTiles + numRows-1) / numRows;
        Size pixelDimensions = new Size(numCols*tileSize.Width, numRows*tileSize.Height);

        // if we're using filtering, we'll need filter padding. there will be (numCols-1)*2 pixels added to the width
        // and (numRows-1)*2 pixels added to the height
        if(FilterMode != FilterMode.None)
        {
          pixelDimensions.Width  += (numCols-1)*2;
          pixelDimensions.Height += (numRows-1)*2;
        }
        
        // now get the smallest texture that can hold an image of this size. this depends on the OpenGL implementation
        pixelDimensions = OpenGL.GetTextureSize(pixelDimensions);

        // calculate the number of pixels in this texture, and if it's less than the smallest we've found so far, or
        // it's more square, set it as the new optimum size.
        int numPixels = pixelDimensions.Width * pixelDimensions.Height;
        if(numPixels < texturePixels ||
           (numPixels == texturePixels &&
            pixelDimensions.Width+pixelDimensions.Height < textureSize.Width+textureSize.Height))
        {
          textureSize   = pixelDimensions;
          texturePixels = numPixels;
        }

        do // after trying, say, 2x5, it doesn't make sense to try 2x6, 2x7, 2x8, or 2x9, which can never be more
        {  // efficient than 2x5. so, we always increment numRows until the number of columns changes. this allows
          numRows++; // us to skip directly from 2x5 to 1x10, for instance. also, we break when numCols == 1 to avoid
        } while(numCols != 1 && (numTiles + numRows-1) / numRows == numCols); // an infinite loop.
      }

      // now that we have the optimum texture size, create a surface of that size and pack the textures into it.
      // we don't need to use the same number of rows and columns as above, and won't bother trying.
      using(Surface newSurface = new Surface(textureSize.Width, textureSize.Height, surface.Format))
      {
        // if the surface doesn't have an alpha channel, we'll use the color key
        if(surface.Format.AlphaMask == 0)
        {
          newSurface.SetColorKey(GetColorKey(surface));
          // but we don't use the key on the source because we want the transparent pixels to be copied, not skipped
          surface.UsingKey = false;
        }

        if(surface.Format.Depth == 8) // if the image is palettized, copy the palette
        {
          newSurface.SetPalette(surface.GetPalette());
        }

        // now, copy the tiles from one surface to the other
        Point srcPoint = tileStart, destPoint = new Point();
        for(int i=0; i<numTiles; i++)
        {
          surface.Blit(newSurface, new Rectangle(srcPoint, tileSize), destPoint); // copy the tile over to the texture
          
          // now add a frame based on where the tile is
          frames.Add(new Frame(textureID, new Rectangle(destPoint, tileSize), newSurface.Size));

          // now update the source X by adding the stride width. (note that stride can be negative)
          srcPoint.X += tileStride.Width;

          // if we have consumed all the tiles in this row, reset the source X and add the stride height
          if(tileStride.Width > 0 && surface.Width - srcPoint.X < tileSize.Width ||
             tileStride.Width < 0 && srcPoint.X < 0)
          {
            srcPoint.X  = tileStart.X;
            srcPoint.Y += tileStride.Height;
          }

          if(FilterMode != FilterMode.None) // if we need texture padding, add it
          {
            OpenGL.AddTextureBorder(newSurface, new Rectangle(destPoint, tileSize));
          }

          // now update the destination X by adding the tile size (and filter padding if we need it)
          destPoint.X += (FilterMode == FilterMode.None ? tileSize.Width : tileSize.Width+2);
          
          // if we can't fit any more tiles in this row, reset the destination X and add the tile height
          if(newSurface.Width - destPoint.X < tileSize.Width)
          {
            destPoint.X  = 0;
            destPoint.Y += (FilterMode == FilterMode.None ? tileSize.Height : tileSize.Height+2);
          }
        }

        // now upload the texture into the video card
        OpenGL.TexImage2D(newSurface);

        // finally, set 'tilesPadded' to indicate whether we added any padding
        // (so that we can avoid invalidating the texture unnecessarily)
        tilesPadded = FilterMode != FilterMode.None;
      }
      
      return true;
    }
  }

  protected override void OnFilterModeChanged()
  {
    // if the new filter mode needs padding, but the image doesn't have padded tiles, then padding needs to be added
    if(FilterMode != FilterMode.None && !tilesPadded)
    {
      Invalidate();
    }
  }

  Size tileSize = new Size(32, 32), tileStride = new Size(32, 32);
  Point tileStart;
  int tileLimit;
  [NonSerialized] bool tilesPadded;
}
#endregion

} // namespace RotationalForce.Engine