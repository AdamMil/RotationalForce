using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameLib.Mathematics.TwoD;
using GameLib.Interop.OpenGL;
using Color = System.Drawing.Color;

namespace RotationalForce.Engine
{

#region IVectorAnimation
public interface IVectorAnimation
{
  System.Collections.ICollection Frames
  {
    get;
  }
  
  void AddFrame(IVectorFrame frame);
  IVectorFrame CreateFrame();
  IVectorFrame GetFrame(int index);
  void InsertFrame(int index, IVectorFrame frame);
  void RemoveFrame(int index);
}
#endregion

#region IVectorFrame
public interface IVectorFrame
{
  System.Collections.ICollection Polygons
  {
    get;
  }

  void AddPolygon(IVectorPolygon polygon);
  IVectorPolygon CreatePolygon();
  IVectorPolygon GetPolygon(int index);
  void InsertPolygon(int index, IVectorPolygon polygon);
  void RemovePolygon(int index);
}
#endregion

#region IVectorPolygon
public interface IVectorPolygon
{
  System.Collections.ICollection Vertices
  {
    get;
  }
  
  void AddVertex(VectorVertex vertex);
  void ClearVertices();
  VectorVertex CreateVertex();
  VectorVertex GetVertex(int index);
  void InsertVertex(int index, VectorVertex vertex);
  void RemoveVertex(int index);
}
#endregion

#region VectorFrame
public abstract class VectorFrame : AnimationFrame
{
  /// <summary>Determines whether the polygons from this frame are interpolated into the next frame.</summary>
  [Category("Behavior")]
  [Description("Determines whether the polygons from this frame are interpolated into the next frame.")]
  [DefaultValue(true)]
  public bool Interpolate
  {
    get { return interpolate; }
    set { interpolate = value; }
  }

  /// <summary>Determines whether this frame will be interpolated into the next frame.</summary>
  bool interpolate = true;
}
#endregion

#region VectorPolygon
public abstract class VectorPolygon
{
  /// <summary>Gets/sets the shade model for this polygon.</summary>
  [Category("Shading")]
  [Description("Determines whether the polygon will use flat shading or smooth shading.")]
  [DefaultValue(ShadeModel.Flat)]
  public ShadeModel ShadeModel
  {
    get { return shadeModel; }
    set
    {
      if(value != shadeModel)
      {
        shadeModel = value;
        OnPolygonChanged(false);
      }
    }
  }

  #region Blending
  /// <summary>Determines whether blending is explicitly enabled for this polygon.</summary>
  /// <remarks>Note that even if this value is set to false, blending may still be enabled if the parent object has
  /// blending enabled.
  /// </remarks>
  [Category("Blending")]
  [Description(Strings.BlendingEnabled)]
  [DefaultValue(false)]
  public bool BlendingEnabled
  {
    get { return blendEnabled; }
    set
    {
      if(value != blendEnabled)
      {
        blendEnabled = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>Get/sets the source blending mode of the polygon, if blending is enabled.</summary>
  /// <remarks>A value of <see cref="SourceBlend.Default"/> will cause the polygon to use the source blend of the
  /// <see cref="VectorObject"/> to which the animation is attached.
  /// </remarks>
  [Category("Blending")]
  [Description("Determines the source blending mode of the polygon, if blending is enabled. A value of Default will "+
    "cause the polygon to use the source blend of the VectorObject to which the animation is attached.")]
  [DefaultValue(SourceBlend.Default)]
  public SourceBlend SourceBlendMode
  {
    get { return sourceBlend; }
    set
    {
      if(value != sourceBlend)
      {
        sourceBlend = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>Get/sets the destination blending mode of the polygon, if blending is enabled.</summary>
  /// <remarks>A value of <see cref="DestinationBlend.Default"/> will cause the polygon to use the destination blend
  /// of the <see cref="VectorObject"/> to which the animation is attached.
  /// </remarks>
  [Category("Blending")]
  [Description("Determines the destination blending mode of the polygon, if blending is enabled. A value of Default "+
    "will cause the polygon to use the destination blend of the VectorObject to which the animation is attached.")]
  [DefaultValue(DestinationBlend.Default)]
  public DestinationBlend DestinationBlendMode
  {
    get { return destBlend; }
    set
    {
      if(value != destBlend)
      {
        destBlend = value;
        OnPolygonChanged(false);
      }
    }
  }
  #endregion

  #region Stroke
  /// <summary>Gets/sets the color of the polygon's outline.</summary>
  /// <remarks>The default color is black, but polygon stroking is disabled by default because
  /// <see cref="StrokeWidth"/> defaults to zero.
  /// </remarks>
  [Category("Stroke")]
  [Description("The color of the polygon's outline.")]
  public Color StrokeColor
  {
    get { return strokeColor; }
    set
    {
      if(value != strokeColor)
      {
        strokeColor = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>Gets/sets the width of the polygon's outline, in pixels.</summary>
  /// <remarks>Use the value zero to disable polygon stroking. The default value is zero.</remarks>
  [Category("Stroke")]
  [Description("The width of the polygon's outline, in pixels. Use the value zero to disable polygon stroking.")]
  [DefaultValue(0f)]
  public float StrokeWidth
  {
    get { return strokeWidth; }
    set
    {
      if(value != strokeWidth)
      {
        if(strokeWidth < 0)
          throw new ArgumentOutOfRangeException("StrokeWidth", value, "The stroke width cannot be negative.");
        strokeWidth = value;
        OnPolygonChanged(false);
      }
    }
  }
  #endregion

  #region Texture
  /// <summary>Determines whether the texture coordinates for vertices in this polygon
  /// will be autogenerated based on the <see cref="TextureOffset"/> and <see cref="TextureRotation."/>
  /// </summary>
  [Category("Texture")]
  [Description("Determines whether the texture coordinates for vertices in this polygon will be autogenerated "+
    "based on the texture offset and texture rotation.")]
  [DefaultValue(true)]
  public bool GenerateTextureCoords
  {
    get { return genTextureCoords; }
    set
    {
      if(value != genTextureCoords)
      {
        genTextureCoords = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>The name of the texture.</summary>
  /// <remarks>The texture name is the name of an <see cref="ImageMap"/>. The texture name can optionally have a
  /// number appended, which determines the zero-based frame of the image map to use. The name and the number are
  /// separated with a hash mark. For example, "textureName#4", references the fifth frame of the "textureName" image
  /// map.
  /// </remarks>
  [Category("Texture")]
  [Description("The name of the texture. The texture name should be the name of an image map, possibly with a "+
    "frame number appended. The frame number should be separated from the texture name with a hash mark. For "+
    "example, \"textureName#4\" references the fifth frame of the \"textureName\" image map.")]
  [DefaultValue(null)]
  public string TextureName
  {
    get { return textureName; }
    set
    {
      if(value != textureName)
      {
        textureName = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>The amount by which the texture is shifted, in texture coordinates.</summary>
  /// <remarks>Texture offset occurs after texture rotation. Texture offset is only used when autogenerating texture
  /// coordinates.
  /// </remarks>
  [Category("Texture")]
  [Description("The amount by which the texture is shifted, in texture coordinates.")]
  public Vector TextureOffset
  {
    get { return textureOffset; }
    set
    {
      if(value != textureOffset)
      {
        textureOffset = value;
        OnPolygonChanged(false);
      }
    }
  }

  /// <summary>The rotation of the texture, in degrees.</summary>
  /// <remarks>Texture rotation occurs before texture offset. Texture rotation is only used when autogenerating
  /// texture coordinates.
  /// </remarks>
  [Category("Texture")]
  [Description("The rotation of the texture, in degrees.")]
  [DefaultValue(0.0)]
  public double TextureRotation
  {
    get { return textureRotation; }
    set
    {
      if(value != textureRotation)
      {
        textureRotation = value;
        OnPolygonChanged(false);
      }
    }
  }
  #endregion

  public void Render()
  {
    if(!IsValid) return; // if we don't have a valid polygon yet, return

    if(tessellationDirty) // if the polygon tessellation is outdated, recalculate it
    {
      Tessellate();
    }

    bool blendWasDisabled = false; // is blending currently disabled? (meaning we need to enabled it?)

    if(blendEnabled) // first set up the blending parameters
    {
      blendWasDisabled = GL.glIsEnabled(GL.GL_BLEND) == 0;
      if(blendWasDisabled) GL.glEnable(GL.GL_BLEND); // enable blending if it was disabled

      // set blend mode if necessary (pulling values from the parent object for Default)
      if(sourceBlend != SourceBlend.Default || destBlend != DestinationBlend.Default)
      {
        GL.glBlendFunc((sourceBlend == SourceBlend.Default ?
                          (uint)GL.glGetIntegerv(GL.GL_BLEND_SRC) : (uint)sourceBlend),
                       (destBlend == DestinationBlend.Default ?
                          (uint)GL.glGetIntegerv(GL.GL_BLEND_DST) : (uint)destBlend));
      }
    }

    if(shadeModel == ShadeModel.Smooth) // set up the shade model (the default is flat)
    {
      GL.glShadeModel(GL.GL_SMOOTH);
    }

    RenderPolygon();

    if(shadeModel == ShadeModel.Smooth)
    {
      GL.glShadeModel(GL.GL_FLAT); // restore the shading model
    }

    if(blendEnabled) // restore blending options to the default
    {
      if(blendWasDisabled) GL.glDisable(GL.GL_BLEND); // only disable blending if we were the one to enable it
      GL.glHint(GL.GL_LINE_SMOOTH_HINT, GL.GL_FASTEST);
    }
  }

  protected abstract bool IsValid
  {
    get;
  }
  
  protected void InvalidateTessellation()
  {
    tessellationDirty = true;
  }

  protected internal virtual void OnPolygonChanged(bool geometryChanged)
  {
    if(geometryChanged) InvalidateTessellation();
  }

  protected abstract void RenderPolygon();

  protected virtual void Tessellate()
  {
    tessellationDirty = false;
  }

  protected bool Tessellate(Point[] points, List<uint> tessPrimitives, List<int[]> tessVertices)
  {
    List<int> currentVertices = new List<int>();
    uint currentPrimitive = 0;
    int primitivesCount = tessPrimitives.Count, verticesCount = tessVertices.Count; // used to restore in case of error
    bool errorOccurred = false;

    IntPtr tess = GLU.gluNewTess();
    if(tess == IntPtr.Zero) throw new OutOfMemoryException();

    try
    {
      // create variables for the delegates to prevent them from being freed by the garbage collector
      GLU.GLUtessBeginProc beginProc = delegate(uint type) { currentPrimitive = type; };
      GLU.GLUtessEndProc endProc = delegate()
        {
          if(currentVertices.Count != 0)
          {
            tessPrimitives.Add(currentPrimitive);
            tessVertices.Add(currentVertices.ToArray());
            currentVertices.Clear();
          }
        };
      GLU.GLUtessVertexProc vertexProc = delegate(IntPtr vertex) { currentVertices.Add(vertex.ToInt32()); };
      GLU.GLUtessErrorProc errorProc = delegate(uint error) { errorOccurred = true; };

      GLU.gluTessCallback(tess, beginProc);
      GLU.gluTessCallback(tess, endProc);
      GLU.gluTessCallback(tess, vertexProc);
      GLU.gluTessCallback(tess, errorProc);

      unsafe
      {
        GLU.gluTessBeginPolygon(tess);
        GLU.gluTessBeginContour(tess);

        double* vertexData = stackalloc double[points.Length*3];
        for(int i=0, j=0; i<points.Length; i++, j+=3)
        {
          vertexData[j]   = points[i].X;
          vertexData[j+1] = points[i].Y;
          vertexData[j+2] = 0;
          GLU.gluTessVertex(tess, vertexData+j, new IntPtr(i));
        }

        GLU.gluTessEndContour(tess);
        GLU.gluTessEndPolygon(tess);
      }
    }
    finally
    {
      GLU.gluDeleteTess(tess);
    }

    if(errorOccurred)
    {
      tessPrimitives.RemoveRange(primitivesCount, tessPrimitives.Count - primitivesCount);
      tessVertices.RemoveRange(verticesCount, tessVertices.Count - verticesCount);
    }

    return errorOccurred;
  }

  /// <summary>The offset within the texture.</summary>
  Vector textureOffset;
  /// <summary>The texture rotation, in degrees.</summary>
  double textureRotation;
  /// <summary>The texture's name. Used to look up the actual texture.</summary>
  string textureName;
  /// <summary>The color of the polygon's stroke line.</summary>
  Color strokeColor = Color.Black;
  /// <summary>The width of the polygon's stroke line.</summary>
  float strokeWidth;
  /// <summary>The type of GL source blending to use.</summary>
  SourceBlend sourceBlend = SourceBlend.Default;
  /// <summary>The type of GL destination blending to use.</summary>
  DestinationBlend destBlend = DestinationBlend.Default;
  /// <summary>The type of shading to use.</summary>
  ShadeModel shadeModel = ShadeModel.Flat;
  /// <summary>Indicates whether blending is explicitly enabled for this polygon.</summary>
  /// <remarks>Note that even if this is false, blending will be enabled if the parent object is blended.</remarks>
  bool blendEnabled;
  /// <summary>Determines whether texture coordinates will be autogenerated.</summary>
  bool genTextureCoords = true;
  /// <summary>Determines whether the cached tessellation information needs to be recalculated.</summary>
  bool tessellationDirty;
}
#endregion

#region VectorVertex
public class VectorVertex
{
  public VectorVertex() { }

  public VectorVertex(Point position)
  {
    this.position = position;
  }

  /// <summary>Gets/sets the vertex's position within the polygon, in local coordinates (-1 to 1).</summary>
  [Description("The vertex's position within the polygon, in local coordinates (-1 to 1).")]
  public Point Position
  {
    get { return position; }
    set
    {
      if(value != position)
      {
        position = value;
        OnVertexChanged(true);
      }
    }
  }

  /// <summary>Gets/sets the vertex's texture coordinates (0 to 1).</summary>
  [Description("The vertex's texture coordinates, which range from 0 to 1.")]
  public Point TextureCoord
  {
    get { return textureCoord; }
    set
    {
      if(value != textureCoord)
      {
        textureCoord = value;
        OnVertexChanged(false);
      }
    }
  }

  /// <summary>Gets/sets the vertex's color.</summary>
  [Description("The vertex's color.")]
  public Color Color
  {
    get { return color; }
    set
    {
      if(value != color)
      {
        color = value;
        OnVertexChanged(false);
      }
    }
  }

  public VectorVertex Clone()
  {
    VectorVertex vertex = CreateClone();
    CloneInto(vertex);
    return vertex;
  }

  protected virtual VectorVertex CreateClone()
  {
    return new VectorVertex();
  }
  
  protected virtual void CloneInto(VectorVertex vertex)
  {
    vertex.color        = color;
    vertex.position     = position;
    vertex.textureCoord = textureCoord;
  }

  internal VectorPolygon Container;

  void OnVertexChanged(bool geometryChanged)
  {
    if(Container != null) Container.OnPolygonChanged(geometryChanged);
  }

  /// <summary>This vertex's position within the polygon.</summary>
  Point position;
  /// <summary>This vertex's texture coordinate. It will only be used if a texture is specified in the parent
  /// polygon.
  /// </summary>
  Point textureCoord;
  /// <summary>The vertex's color.</summary>
  Color color;
}
#endregion

#region PolygonAnimation
[Serializable]
public sealed class PolygonAnimation : Animation, IVectorAnimation
{
  #region Frame
  [Serializable]
  public sealed class Frame : VectorFrame, IVectorFrame
  {
    [Browsable(false)]
    public ReadOnlyCollection<Polygon> Polygons
    {
      get { return new ReadOnlyCollection<Polygon>(polygons); }
    }

    public void AddPolygon(Polygon polygon)
    {
      if(polygon == null) throw new ArgumentNullException("polygon");
      InsertPolygon(polygons.Count, polygon);
      OnFrameChanged(true);
    }

    public void ClearPolygons()
    {
      if(polygons.Count != 0)
      {
        polygons.Clear();
        OnFrameChanged(true);
      }
    }

    public void InsertPolygon(int index, Polygon polygon)
    {
      if(polygon == null) throw new ArgumentNullException("frame");
      polygons.Insert(index, polygon);
      OnFrameChanged(true);
    }

    public void RemovePolygon(int index)
    {
      polygons.RemoveAt(index);
      OnFrameChanged(true);
    }

    public void Render(ref AnimationData data)
    {
      for(int i=0; i<polygons.Count; i++)
      {
        polygons[i].Render();
      }
    }

    /// <summary>Called when the frame changes.</summary>
    /// <param name="geometryChanged">If true, indicates that the actual shape of the polygons has changed. Otherwise,
    /// the change is limited to miscellaneous properties, like color.
    /// </param>
    internal void OnFrameChanged(bool geometryChanged)
    {
      if(geometryChanged)
      {
        frameChanged = true;
      }
    }

    /// <summary>The polygons that make up this animation frame.</summary>
    List<Polygon> polygons = new List<Polygon>(4);
    /// <summary>Determines whether the frame has changed and cached rendering information must be recalculated.</summary>
    bool frameChanged = true;

    #region IVectorFrame
    System.Collections.ICollection IVectorFrame.Polygons
    {
      get { return polygons; }
    }

    void IVectorFrame.AddPolygon(IVectorPolygon polygon)
    {
      AddPolygon((Polygon)polygon);
    }

    IVectorPolygon IVectorFrame.CreatePolygon()
    {
      return new Polygon();
    }

    IVectorPolygon IVectorFrame.GetPolygon(int index)
    {
      return polygons[index];
    }

    void IVectorFrame.InsertPolygon(int index, IVectorPolygon polygon)
    {
      InsertPolygon(index, (Polygon)polygon);
    }
    #endregion
  }
  #endregion

  #region Polygon
  [Serializable]
  public sealed class Polygon : VectorPolygon, IVectorPolygon
  {
    [Browsable(false)]
    public ReadOnlyCollection<VectorVertex> Vertices
    {
      get { return new ReadOnlyCollection<VectorVertex>(vertices); }
    }

    public void AddVertex(VectorVertex vertex)
    {
      InsertVertex(vertices.Count, vertex);
    }

    public void ClearVertices()
    {
      if(vertices.Count != 0)
      {
        vertices.Clear();
        OnPolygonChanged(true);
      }
    }

    public void InsertVertex(int index, VectorVertex vertex)
    {
      if(vertex.Container != null) throw new ArgumentException("Vertex already belongs to a polygon.");
      vertices.Insert(index, vertex);
      vertex.Container = this;
      OnPolygonChanged(true);
    }

    public void RemoveVertex(int index)
    {
      vertices[index].Container = null;
      vertices.RemoveAt(index);
      OnPolygonChanged(true);
    }

    protected override bool IsValid
    {
      get { return vertices.Count >= 3; }
    }

    protected override void RenderPolygon()
    {
      if(tessPrimitives.Count == 0)
      {
        // if tessellation failed (possibly due to a self-intersecting polygon), just draw using GL_POLYGON.
        GL.glBegin(GL.GL_POLYGON);
        for(int i=0; i<vertices.Count; i++)
        {
          GL.glColor(vertices[i].Color);
          GL.glVertex2d(vertices[i].Position);
        }
        GL.glEnd();
      }
      else // otherwise, draw using the tesselation info
      {
        for(int i=0; i<tessPrimitives.Count; i++)
        {
          int[] indices = tessVertices[i];

          GL.glBegin(tessPrimitives[i]);
          for(int j=0; j<indices.Length; j++)
          {
            int vertexIndex = indices[j];
            GL.glColor(vertices[vertexIndex].Color);
            GL.glVertex2d(vertices[vertexIndex].Position);
          }
          GL.glEnd();
        }
      }

      if(StrokeWidth != 0 && StrokeColor.A != 0) // if stroking is enabled
      {
        GL.glLineWidth(StrokeWidth); // set the stroke width

        if(BlendingEnabled) // if blending is enabled, we'll use antialiased lines
        {
          GL.glEnable(GL.GL_LINE_SMOOTH);
          GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
        }
        else if(ShadeModel == ShadeModel.Flat) // otherwise, if the shade model is flat, we'll set the color once
        {
          GL.glColor(vertices[0].Color.A, StrokeColor);
        }

        // if we're using antialiased lines or a smooth shading model, we'll set the color at each vertex.
        // this allows the alpha value of the border to match that of the polygon edge
        bool setVertexColor = BlendingEnabled || ShadeModel == ShadeModel.Smooth;

        // now stroke the edges of the polygon
        GL.glBegin(GL.GL_LINE_LOOP);
        for(int i=0; i<vertices.Count; i++)
        {
          if(setVertexColor)
          {
            GL.glColor(vertices[i].Color.A, StrokeColor);
          }
          GL.glVertex2d(vertices[i].Position);
        }
        GL.glEnd();

        if(BlendingEnabled) GL.glDisable(GL.GL_LINE_SMOOTH); // we enabled line smoothing above, so disable it here
      }
    }

    protected override void Tessellate()
    {
      if(tessPrimitives == null) tessPrimitives = new List<uint>(4);
      if(tessVertices == null) tessVertices = new List<int[]>();
      tessPrimitives.Clear();
      tessVertices.Clear();

      Point[] points = new Point[vertices.Count];
      for(int i=0; i<points.Length; i++)
      {
        points[i] = vertices[i].Position;
      }
      
      Tessellate(points, tessPrimitives, tessVertices);

      base.Tessellate();
    }

    #region IVectorPolygon
    System.Collections.ICollection IVectorPolygon.Vertices
    {
      get { return vertices; }
    }
    
    VectorVertex IVectorPolygon.CreateVertex()
    {
      return new VectorVertex();
    }

    VectorVertex IVectorPolygon.GetVertex(int index)
    {
      return vertices[index];
    }
    #endregion

    /// <summary>The vertices in this polygon.</summary>
    List<VectorVertex> vertices = new List<VectorVertex>(4);
    /// <summary>Cached tessellation info containing the list of primitives needed to render the polygon.</summary>
    List<uint> tessPrimitives;
    /// <summary>Cached tessellation info containing the list of vertex indices.</summary>
    List<int[]> tessVertices;
  }
  #endregion

  [Browsable(false)]
  public ReadOnlyCollection<Frame> Frames
  {
    get { return new ReadOnlyCollection<Frame>(frames); }
  }

  public void AddFrame(Frame frame)
  {
    if(frame == null) throw new ArgumentNullException("frame");
    frames.Add(frame);
  }

  public void ClearFrames()
  {
    frames.Clear();
  }

  public void InsertFrame(int index, Frame frame)
  {
    if(frame == null) throw new ArgumentNullException("frame");
    frames.Insert(index, frame);
  }

  public void RemoveFrame(int index)
  {
    frames.RemoveAt(index);
  }

  protected internal override void Render(ref AnimationData data)
  {
    int frame = EngineMath.Clip(data.Frame, 0, Frames.Count-1);
    if(frame == -1) return;
    frames[frame].Render(ref data);
  }

  protected internal override void Simulate(ref AnimationData data, double timeDelta)
  {
    throw new NotImplementedException();
  }

  #region IVectorAnimation
  System.Collections.ICollection IVectorAnimation.Frames
  {
    get { return frames; }
  }

  void IVectorAnimation.AddFrame(IVectorFrame Frame)
  {
    AddFrame((Frame)Frame);
  }
  
  IVectorFrame IVectorAnimation.CreateFrame()
  {
    return new Frame();
  }
  
  IVectorFrame IVectorAnimation.GetFrame(int index)
  {
    return frames[index];
  }

  void IVectorAnimation.InsertFrame(int index, IVectorFrame Frame)
  {
    InsertFrame(index, (Frame)Frame);
  }
  #endregion

  /// <summary>The frames that make up this animation.</summary>
  List<Frame> frames = new List<Frame>(4);
}
#endregion

#region SplineAnimation
[Serializable]
public sealed class SplineAnimation : Animation, IVectorAnimation
{
  #region Frame
  [Serializable]
  public sealed class Frame : VectorFrame, IVectorFrame
  {
    [Browsable(false)]
    public ReadOnlyCollection<Polygon> Polygons
    {
      get { return new ReadOnlyCollection<Polygon>(polygons); }
    }

    public void AddPolygon(Polygon polygon)
    {
      if(polygon == null) throw new ArgumentNullException("polygon");
      InsertPolygon(polygons.Count, polygon);
      OnFrameChanged(true);
    }

    public void ClearPolygons()
    {
      if(polygons.Count != 0)
      {
        polygons.Clear();
        OnFrameChanged(true);
      }
    }

    public void InsertPolygon(int index, Polygon polygon)
    {
      if(polygon == null) throw new ArgumentNullException("frame");
      polygons.Insert(index, polygon);
      OnFrameChanged(true);
    }

    public void RemovePolygon(int index)
    {
      polygons.RemoveAt(index);
      OnFrameChanged(true);
    }

    public void Render(ref AnimationData data)
    {
      for(int i=0; i<polygons.Count; i++)
      {
        polygons[i].Render();
      }
    }

    /// <summary>Called when the frame changes.</summary>
    /// <param name="geometryChanged">If true, indicates that the actual shape of the polygons has changed. Otherwise,
    /// the change is limited to miscellaneous properties, like color.
    /// </param>
    internal void OnFrameChanged(bool geometryChanged)
    {
      if(geometryChanged)
      {
        frameChanged = true;
      }
    }

    #region IVectorFrame
    System.Collections.ICollection IVectorFrame.Polygons
    {
      get { return polygons; }
    }

    void IVectorFrame.AddPolygon(IVectorPolygon polygon)
    {
      AddPolygon((Polygon)polygon);
    }

    IVectorPolygon IVectorFrame.CreatePolygon()
    {
      return new Polygon();
    }

    IVectorPolygon IVectorFrame.GetPolygon(int index)
    {
      return polygons[index];
    }

    void IVectorFrame.InsertPolygon(int index, IVectorPolygon polygon)
    {
      InsertPolygon(index, (Polygon)polygon);
    }
    #endregion

    /// <summary>The polygons that make up this animation frame.</summary>
    List<Polygon> polygons = new List<Polygon>(4);
    /// <summary>Determines whether the frame has changed and cached rendering information must be recalculated.</summary>
    bool frameChanged = true;
  }
  #endregion

  #region Polygon
  [Serializable]
  public sealed class Polygon : VectorPolygon, IVectorPolygon
  {
    [Browsable(false)]
    public ReadOnlyCollection<Vertex> Vertices
    {
      get { return new ReadOnlyCollection<Vertex>(vertices); }
    }

    public void AddVertex(Vertex vertex)
    {
      InsertVertex(vertices.Count, vertex);
    }

    public void ClearVertices()
    {
      if(vertices.Count != 0)
      {
        vertices.Clear();
        OnPolygonChanged(true);
      }
    }

    public void InsertVertex(int index, Vertex vertex)
    {
      if(vertex.Container != null) throw new ArgumentException("Vertex already belongs to a polygon.");
      vertices.Insert(index, vertex);
      vertex.Container = this;
      OnPolygonChanged(true);
    }

    public void RemoveVertex(int index)
    {
      vertices[index].Container = null;
      vertices.RemoveAt(index);
      OnPolygonChanged(true);
    }

    protected override bool IsValid
    {
      get { return vertices.Count >= 3; }
    }

    protected override void RenderPolygon()
    {
      bool setVertexColor = ShadeModel == ShadeModel.Smooth; // determine if we need to set every vertex color
      if(!setVertexColor) // if not, set it once from the first vertex
      {
        GL.glColor(vertices[0].Color);
      }

      // if tessellation failed (possibly due to a self-intersecting polygon), just draw using GL_POLYGON.
      if(tessPrimitives.Count == 0)
      {
        GL.glBegin(GL.GL_POLYGON);
        for(int i=0; i<vertices.Count; i++)
        {
          if(setVertexColor)
          {
            GL.glColor(vertices[i].Color);
          }
          GL.glVertex2d(vertices[i].Position);
        }
        GL.glEnd();
      }
      else // otherwise, draw using the tesselation info
      {
        for(int i=0; i<tessPrimitives.Count; i++)
        {
          int[] indices = tessVertices[i];

          GL.glBegin(tessPrimitives[i]);
          for(int j=0; j<indices.Length; j++)
          {
            int vertexIndex = indices[j];
            if(setVertexColor)
            {
              GL.glColor(subPoints[vertexIndex].Color);
            }
            GL.glVertex2d(subPoints[vertexIndex].Position);
          }
          GL.glEnd();
        }
      }

      if(StrokeWidth != 0 && StrokeColor.A != 0) // if stroking is enabled
      {
        GL.glLineWidth(StrokeWidth); // set the stroke width

        if(BlendingEnabled) // if blending is enabled, we'll use antialiased lines
        {
          GL.glEnable(GL.GL_LINE_SMOOTH);
          GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
        }

        if(!setVertexColor) // if the shade model is flat, we'll set the color once
        {
          GL.glColor(vertices[0].Color.A, StrokeColor);
        }

        // now stroke the edges of the polygon
        GL.glBegin(GL.GL_LINE_LOOP);

        // if there are no sub-points (possibly due to a self-intersecting polygon), stroke the polygonal hull
        if(subPoints.Count == 0)
        {
          for(int i=0; i<vertices.Count; i++)
          {
            if(setVertexColor)
            {
              GL.glColor(vertices[i].Color.A, StrokeColor);
            }
            GL.glVertex2d(vertices[i].Position);
          }
        }
        else // othrewise stroke the subpoint outline
        {
          for(int i=0; i<subPoints.Count; i++)
          {
            if(setVertexColor)
            {
              GL.glColor(subPoints[i].Color.A, StrokeColor);
            }
            GL.glVertex2d(subPoints[i].Position);
          }
        }

        GL.glEnd();

        if(BlendingEnabled) GL.glDisable(GL.GL_LINE_SMOOTH); // we enabled line smoothing above, so disable it here
      }
    }

    protected override void Tessellate()
    {
      // first subdivide the splines into a polygonal outline
      if(subPoints == null) subPoints = new List<SubPoint>();
      subPoints.Clear();

      bool closed = true;
      for(int vertexIndex=0; vertexIndex<vertices.Count; vertexIndex++) // loop through the edges
      {
        const int Subdivisions = 8;
        
        int index = closed ? vertexIndex : vertexIndex-2;
        // get the indices of the control points
        int i0 = GetIndex(index), i1 = GetIndex(index+1), i2 = GetIndex(index+2), i3 = GetIndex(index+3);

        for(int i=0; i<=Subdivisions; i++)
        {
          double delta = i / (double)Subdivisions, invDelta = 1 - delta;
          double deltaSquared = delta*delta, deltaCubed = deltaSquared*delta;
          double b0 = invDelta*invDelta*invDelta / 6;                    // (1-delta)^3 / 6
          double b1 = deltaCubed*0.5 - deltaSquared + 4/6.0;             // (3delta^3 - 6delta^2 + 4) / 6
          double b2 = (deltaSquared - deltaCubed + delta + 1/3.0) * 0.5; // (-3delta^3 + 3delta^2 + 3delta + 1) / 6
          double b3 = deltaCubed / 6;                                    // delta^3 / 6
          
          SubPoint subPoint = new SubPoint();
          subPoint.Position.X = b0*vertices[i0].Position.X + b1*vertices[i1].Position.X +
                                b2*vertices[i2].Position.X + b3*vertices[i3].Position.X;
          subPoint.Position.Y = b0*vertices[i0].Position.Y + b1*vertices[i1].Position.Y +
                                b2*vertices[i2].Position.Y + b3*vertices[i3].Position.Y;
          subPoint.Color = vertices[i0].Color; // TODO: interpolate the color
          
          subPoints.Add(subPoint);
        }
      }

      // now tessellate the polygons to create triangles
      if(tessPrimitives == null) tessPrimitives = new List<uint>(4);
      if(tessVertices == null) tessVertices = new List<int[]>();
      tessPrimitives.Clear();
      tessVertices.Clear();

      Point[] points = new Point[subPoints.Count];
      for(int i=0; i<points.Length; i++)
      {
        points[i] = subPoints[i].Position;
      }
      
      Tessellate(points, tessPrimitives, tessVertices);

      base.Tessellate();
    }

    int GetIndex(int index)
    {
      // if the shape is closed, the index will wrap around
      return index<0 ? vertices.Count+index : index>=vertices.Count ? index-vertices.Count : index;
    }

    struct SubPoint
    {
      public Point Position;
      public Color Color;
    }

    /// <summary>The vertices in this polygon.</summary>
    List<Vertex> vertices = new List<Vertex>(4);
    /// <summary>Cached tessellation info containing the list of primitives needed to render the polygon.</summary>
    List<uint> tessPrimitives;
    /// <summary>Cached tessellation info containing the list of vertex indices.</summary>
    List<int[]> tessVertices;
    /// <summary>Cached subdivision info containing the subdivision points.</summary>
    List<SubPoint> subPoints;

    #region IVectorPolygon
    System.Collections.ICollection IVectorPolygon.Vertices
    {
      get { return vertices; }
    }

    void IVectorPolygon.AddVertex(VectorVertex vertex)
    {
      AddVertex((Vertex)vertex);
    }
    
    VectorVertex IVectorPolygon.CreateVertex()
    {
      return new Vertex();
    }

    VectorVertex IVectorPolygon.GetVertex(int index)
    {
      return vertices[index];
    }

    void IVectorPolygon.InsertVertex(int index, VectorVertex vertex)
    {
      InsertVertex(index, (Vertex)vertex);
    }
    #endregion
  }
  #endregion

  #region Vertex
  [Serializable]
  public sealed class Vertex : VectorVertex
  {
    public Vertex() { }
    public Vertex(Point position) : base(position) { }

    /// <summary>Determines whether the vertex will split the shape's spline, causing a break in continuity.</summary>
    [Description("Whether the vertex will split the shape's spline, causing a break in continuity.")]
    public bool Split
    {
      get { return split; }
      set { split = value; }
    }

    protected override VectorVertex CreateClone()
    {
      return new Vertex();
    }

    bool split;
  }
  #endregion

  [Browsable(false)]
  public ReadOnlyCollection<Frame> Frames
  {
    get { return new ReadOnlyCollection<Frame>(frames); }
  }

  public void AddFrame(Frame frame)
  {
    if(frame == null) throw new ArgumentNullException("frame");
    frames.Add(frame);
  }

  public void ClearFrames()
  {
    frames.Clear();
  }

  public void InsertFrame(int index, Frame frame)
  {
    if(frame == null) throw new ArgumentNullException("frame");
    frames.Insert(index, frame);
  }

  public void RemoveFrame(int index)
  {
    frames.RemoveAt(index);
  }

  protected internal override void Render(ref AnimationData data)
  {
    int frame = EngineMath.Clip(data.Frame, 0, Frames.Count-1);
    if(frame == -1) return;
    frames[frame].Render(ref data);
  }

  protected internal override void Simulate(ref AnimationData data, double timeDelta)
  {
    throw new NotImplementedException();
  }


  #region IVectorAnimation
  System.Collections.ICollection IVectorAnimation.Frames
  {
    get { return frames; }
  }

  void IVectorAnimation.AddFrame(IVectorFrame Frame)
  {
    AddFrame((Frame)Frame);
  }

  IVectorFrame IVectorAnimation.CreateFrame()
  {
    return new Frame();
  }

  IVectorFrame IVectorAnimation.GetFrame(int index)
  {
    return frames[index];
  }

  void IVectorAnimation.InsertFrame(int index, IVectorFrame Frame)
  {
    InsertFrame(index, (Frame)Frame);
  }
  #endregion

  /// <summary>The frames that make up this animation.</summary>
  List<Frame> frames = new List<Frame>(4);
}
#endregion

} // namespace RotationalForce.Engine