using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameLib.Mathematics.TwoD;
using GameLib.Interop.OpenGL;
using Color = System.Drawing.Color;

namespace RotationalForce.Engine
{

#region VectorAnimation
public sealed class VectorAnimation : Animation
{
  #region Frame
  public sealed class Frame : AnimationFrame
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
    /// <summary>Determines whether this frame will be interpolated into the next frame.</summary>
    bool interpolate = true;
    /// <summary>Determines whether the frame has changed and cached rendering information must be recalculated.</summary>
    bool frameChanged = true;
  }
  #endregion

  #region Polygon
  public sealed class Polygon
  {
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
      if(vertex.Polygon != null) throw new ArgumentException("Vertex already belongs to a polygon.");
      vertices.Insert(index, vertex);
      vertex.Polygon = this;
      OnPolygonChanged(true);
    }

    public void RemoveVertex(int index)
    {
      vertices[index].Polygon = null;
      vertices.RemoveAt(index);
      OnPolygonChanged(true);
    }

    public void Render()
    {
      if(vertices.Count < 3) return; // if we don't have a valid polygon yet, return

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

      bool setVertexColor;
      if(shadeModel == ShadeModel.Smooth) // set up the shade model (the default is flat)
      {
        GL.glShadeModel(GL.GL_SMOOTH);
        setVertexColor = true;
      }
      else
      {
        GL.glColor(vertices[0].Color);
        setVertexColor = false;
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

GL.glPointSize(2);
GL.glColor(Color.Red);
GL.glBegin(GL.GL_POINTS);
for(int i=0; i<numSubPoints; i++)
{
  GL.glVertex2d(subPoints[i].Position);
}
GL.glEnd();
      }

      if(strokeWidth != 0 && strokeColor.A != 0) // if stroking is enabled
      {
        GL.glLineWidth(strokeWidth); // set the stroke width

        if(blendEnabled) // if blending is enabled, we'll use antialiased lines
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
        if(numSubPoints == 0)
        {
          for(int i=0; i<vertices.Count; i++)
          {
            if(setVertexColor)
            {
              GL.glColor(vertices[i].Color.A, strokeColor);
            }
            GL.glVertex2d(vertices[i].Position);
          }
        }
        else // othrewise stroke the subpoint outline
        {
          for(int i=0; i<numSubPoints; i++)
          {
            if(setVertexColor)
            {
              GL.glColor(subPoints[i].Color.A, strokeColor);
            }
            GL.glVertex2d(subPoints[i].Position);
          }
        }

        GL.glEnd();

        if(blendEnabled) GL.glDisable(GL.GL_LINE_SMOOTH); // we enabled line smoothing above, so disable it here
      }

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

    internal void OnPolygonChanged(bool geometryChanged)
    {
      if(geometryChanged) tessellationDirty = true;
    }

    struct SubPoint
    {
      public Point Position;
      public Color Color;
    }

    void AddSubPoint(ref SubPoint subPoint)
    {
      if(subPoints == null || subPoints.Length == numSubPoints)
      {
        SubPoint[] newPoints = new SubPoint[numSubPoints == 0 ? 8 : numSubPoints*2];
        if(numSubPoints != 0)
        {
          Array.Copy(subPoints, newPoints, numSubPoints);
        }
        subPoints = newPoints;
      }
      
      subPoints[numSubPoints++] = subPoint;
    }

    int GetIndex(int offset, int edgeStart, int edgeEnd)
    {
      int newIndex = edgeStart + offset;
      if(edgeEnd == -1) // the shape is closed, meaning that the newIndex will wrap around
      {
        return newIndex<0 ? vertices.Count+newIndex : newIndex>=vertices.Count ? newIndex-vertices.Count : newIndex;
      }
      else if(edgeStart < edgeEnd) // it's a shape clamped to the given edges
      {
        return newIndex<edgeStart ? edgeStart : newIndex>edgeEnd ? edgeEnd : newIndex;
      }
      else // it's a shape clamped to the given edges, but the edge spans the zero newIndex
      {
        if(offset < 0) return edgeStart;
        int rightLength = vertices.Count-edgeStart;
        if(offset < rightLength) return newIndex;
        offset -= rightLength;
        if(offset > edgeEnd) return edgeEnd;
        return offset;
      }
    }

    void SubdividePolygon()
    {
      /* first subdivide the splines into a polygonal outline */
      numSubPoints = 0;

      // start by checking if it's a fully-broken shape (plain polygon) or fully-joined shape (closed spline shape)
      int firstBreak = -1;
      for(int i=0; i<vertices.Count; i++)
      {
        if(vertices[i].Split)
        {
          firstBreak = i;
          break;
        }
      }

      if(firstBreak == -1) // if the shape has no breaks, use the logic for a closed b-spline
      {
        for(int vertexIndex=0; vertexIndex<vertices.Count; vertexIndex++)
        {
          // get the indices of the control points
          int i0 = GetIndex(0, vertexIndex, -1), i1 = GetIndex(1, vertexIndex, -1),
              i2 = GetIndex(2, vertexIndex, -1), i3 = GetIndex(3, vertexIndex, -1);
          SubdivideSegment(i0, i1, i2, i3);
        }
      }
      else // the shaped is composed of straight-line segments, possibly with spline-based segments too
      {
        int breakPoint = firstBreak, lastEdge, examined = 0;
        do
        {
          // 'breakPoint' points at a broken vertex. scan to find the next broken vertex.
          lastEdge = breakPoint;
          do
          {
            if(++lastEdge == vertices.Count) lastEdge = 0; // move to the next point
            examined++;                                    // mark it examined
          } while(!vertices[lastEdge].Split);

          // 'edgeEnd' points to the next broken vertex. the spline goes from 'breakPoint' to 'edgeEnd', inclusive.
          int splineLength = breakPoint<lastEdge ? lastEdge-breakPoint : vertices.Count-breakPoint+lastEdge; // # of edges in spline

          // if there's only one edge involved, there can be no curve, so just add this vertex as-is
          if(splineLength == 1)
          {
            SubPoint subPoint = new SubPoint();
            subPoint.Color    = vertices[breakPoint].Color;
            subPoint.Position = vertices[breakPoint].Position;
            AddSubPoint(ref subPoint);
          }
          else // otherwise, there are multiple edges involved in this segment, so use a clamped b-spline
          {
            // to create a clamped b-spline, we pretend that the first and last control points are replicated
            for(int i=-2; i < splineLength; i++)
            {
              int i0 = GetIndex(  i, breakPoint, lastEdge), i1 = GetIndex(i+1, breakPoint, lastEdge),
                  i2 = GetIndex(i+2, breakPoint, lastEdge), i3 = GetIndex(i+3, breakPoint, lastEdge);
              SubdivideSegment(i0, i1, i2, i3);
            }
          }

          breakPoint = lastEdge; // advance 'breakPoint' to the next broken point to continue the loop
        } while(examined < vertices.Count);
      }
    }

    void SubdivideSegment(int i0, int i1, int i2, int i3)
    {
      const int Subdivisions = 8;
      for(int i=0; i<Subdivisions; i++)
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

        AddSubPoint(ref subPoint);
      }
    }

    void Tessellate()
    {
      SubdividePolygon();

      if(tessPrimitives == null) tessPrimitives = new List<uint>(4);
      if(tessVertices == null) tessVertices = new List<int[]>();
      tessPrimitives.Clear();
      tessVertices.Clear();

      List<int> currentVertices = new List<int>();
      uint currentPrimitive = 0;
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

          double* vertexData = stackalloc double[numSubPoints*3];
          for(int i=0, j=0; i<numSubPoints; i++, j+=3)
          {
            vertexData[j]   = subPoints[i].Position.X;
            vertexData[j+1] = subPoints[i].Position.Y;
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
        tessPrimitives.Clear();
        tessVertices.Clear();
      }

      tessellationDirty = false;
    }

    /// <summary>The vertices in this polygon.</summary>
    List<Vertex> vertices = new List<Vertex>(4);
    /// <summary>Cached tessellation info containing the list of primitives needed to render the polygon.</summary>
    List<uint> tessPrimitives;
    /// <summary>Cached tessellation info containing the list of vertex indices.</summary>
    List<int[]> tessVertices;
    /// <summary>Cached subdivision info containing the subdivision points.</summary>
    SubPoint[] subPoints;

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
    /// <summary>The number of points in <see cref="subPoints"/>.</summary>
    int numSubPoints;
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

  #region Vertex
  public sealed class Vertex
  {
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

    /// <summary>Determines whether the vertex will split the shape's spline, causing a break in continuity.</summary>
    [Description("Whether the vertex will split the shape's spline, causing a break in continuity.")]
    public bool Split
    {
      get { return split; }
      set
      {
        if(value != split)
        {
          split = value;
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

    public Vertex Clone()
    {
      Vertex vertex = new Vertex();
      vertex.color        = color;
      vertex.position     = position;
      vertex.textureCoord = textureCoord;
      vertex.split        = split;
      return vertex;
    }

    internal Polygon Polygon;

    void OnVertexChanged(bool geometryChanged)
    {
      if(Polygon != null) Polygon.OnPolygonChanged(geometryChanged);
    }

    /// <summary>This vertex's position within the polygon.</summary>
    Point position;
    /// <summary>This vertex's texture coordinate. It will only be used if a texture is specified in the parent
    /// polygon.
    /// </summary>
    Point textureCoord;
    /// <summary>The vertex's color.</summary>
    Color color;
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

  /// <summary>The frames that make up this animation.</summary>
  List<Frame> frames = new List<Frame>(4);
}
#endregion

} // namespace RotationalForce.Engine