using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameLib.Mathematics;
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
    }

    public void ClearPolygons()
    {
      polygons.Clear();
    }

    public void InsertPolygon(int index, Polygon polygon)
    {
      if(polygon == null) throw new ArgumentNullException("frame");
      polygons.Insert(index, polygon);
    }

    public void RemovePolygon(int index)
    {
      polygons.RemoveAt(index);
    }

    public void Render(ref AnimationData data)
    {
      for(int i=0; i<polygons.Count; i++)
      {
        polygons[i].Render();
      }
    }

    /// <summary>The polygons that make up this animation frame.</summary>
    List<Polygon> polygons = new List<Polygon>(4);
    /// <summary>Determines whether this frame will be interpolated into the next frame.</summary>
    bool interpolate = true;
    /// <summary>Determines whether the frame has changed and cached rendering information must be recalculated.</summary>
  }
  #endregion

  #region Polygon
  public sealed class Polygon : UniqueObject // polygons can be shared between animation frames
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
      set { blendEnabled = value; }
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
      set { sourceBlend = value; }
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
      set { destBlend = value; }
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
      set { strokeColor = value; }
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
        if(strokeWidth < 0)
          throw new ArgumentOutOfRangeException("StrokeWidth", "The stroke width cannot be negative.");
        strokeWidth = value;
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
          InvalidateGeometry();
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
          InvalidateGeometry();
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
          EngineMath.AssertValidFloat(value);
          textureRotation = value;
          InvalidateGeometry();
        }
      }
    }
    #endregion

    #region LOD & Spline
    /// <summary>Determines whether the polygon has any spline edges.</summary>
    [Browsable(false)]
    public bool HasSpline
    {
      get
      {
        for(int i=0; i<vertices.Count; i++)
        {
          if(!vertices[i].Split) return true;
        }
        return false;
      }
    }

    /// <summary>The Level Of Detail threshold that determines how many of the points generated by the
    /// subdivision process will be discarded. A higher value will discard more points. A value of zero will discard
    /// no points.
    /// </summary>
    /// <remarks>Using a value of zero is strongly discouraged due to the tendency of the b-spline algorithm to
    /// generate many useless points. Decreasing this value will decrease rendering performance. Internally, this value
    /// represents the number of radians that the curve has to bend before a new point will be inserted.
    /// This value even affects shapes that have no splines (although in those shapes, every direction change is
    /// likely to be above the threshold).
    /// </remarks>
    /// <seealso cref="Subdivisions"/>
    [Description("The Level Of Detail threshold that determines how many of the points generated by the "+
      "spline subdivision process will be discarded. A lower value will discard fewer points, and zero will discard "+
      "no points. Using a value of zero, however, is strongly discouraged due to the tendency of the spline "+
      "algorithm to generate many useless points if any of the vertices are split (Split is true).")]
    [DefaultValue(DefaultLOD)]
    public double LOD
    {
      get { return lodThreshold; }
      set
      {
        if(value != lodThreshold)
        {
          EngineMath.AssertValidFloat(value);
          if(value < 0) throw new ArgumentOutOfRangeException("LOD", "LOD cannot be negative.");
          lodThreshold = value;
          InvalidateGeometry();
        }
      }
    }
    
    /// <summary>The number of points into which each spline is subdivided.</summary>
    /// <remarks>Each spline curve is linearly subdivided into a number of points. These points are then passed through
    /// the LOD process to exclude the points that are unnecessary. Increasing the number of subdivision points will
    /// adversely affect the amount of time it takes to subdivide the polygon, but subdivision is only performed when
    /// the shape of the polygon changes.
    /// </remarks>
    [Description("The number of points into which each spline edge is subdivided. These points provide the candidate "+
      "points for the Level Of Detail (LOD) algorithm.")]
    [DefaultValue(DefaultSubdivisions)]
    public int Subdivisions
    {
      get { return subdivisions; }
      set
      {
        if(value != subdivisions)
        {
          if(subdivisions < 1)
          {
            throw new ArgumentOutOfRangeException("Subdivisions", "Subdivisions must be greater than or equal to 1.");
          }
          subdivisions = value;
          InvalidateGeometry();
        }
      }
    }
    #endregion

    #region ISerializable
    protected override void Deserialize(DeserializationStore store)
    {
      tessellationDirty = true; // we don't save subdivision or tessellation info, so they'll need to be recalculated
      
      // the vertices' Polygon pointer was not saved (to avoid infinite loops), so reset them here
      foreach(Vertex vertex in vertices)
      {
        vertex.Polygon = this;
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
          // the subdivision process interpolates the colors differently depending on the shade model
          InvalidateGeometry();
        }
      }
    }

    [Browsable(false)]
    public ReadOnlyCollection<Vertex> Vertices
    {
      get { return new ReadOnlyCollection<Vertex>(vertices); }
    }

    /// <summary>Creates a copy of this polygon will all spline edges subdivided into static vertices.</summary>
    public Polygon CloneAsVertexPolygon()
    {
      if(tessellationDirty) Tessellate();
      
      Polygon poly = new Polygon();
      for(int i=0; i<numSubPoints; i++)
      {
        Vertex vertex = new Vertex();
        vertex.Color    = subPoints[i].Color;
        vertex.Position = subPoints[i].Position;
        vertex.Split    = true;
        poly.AddVertex(vertex);
      }
      
      return poly;
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
        InvalidateGeometry();
      }
    }

    public void InsertVertex(int index, Vertex vertex)
    {
      if(vertex.Polygon != null) throw new ArgumentException("Vertex already belongs to a polygon.");
      vertices.Insert(index, vertex);
      vertex.Polygon = this;
      InvalidateGeometry();
    }

    public void RemoveVertex(int index)
    {
      vertices[index].Polygon = null;
      vertices.RemoveAt(index);
      InvalidateGeometry();
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

    /// <summary>Called when the tessellated/subdivided geometry of the polygon needs to be recalculated. Changes to
    /// interpolated vertex properties (color and texture coordinate) also cause this to need to be recalculated.
    /// </summary>
    internal void InvalidateGeometry()
    {
      tessellationDirty = true;
    }

    const int DefaultSubdivisions = 20;
    const double DefaultLOD = 0.31415926535897932384626433832795; // 18 degrees (1/10th PI, 1/20th of a circle)

    struct SubPoint
    {
      public Point Position;
      public Color Color;
    }
    
    struct SubdivisionState
    {
      public SubPoint FirstPoint;
      public SubPoint BasePoint, PrevPoint;
      public double   PrevAngle, Deviation;
      public int      PointsConsidered;
    }

    /// <summary>Takes a <see cref="SubPoint"/> and adds to the subdivision array if it is necessary for the given LOD.</summary>
    void AddSubPoint(ref SubPoint subPoint)
    {
      // the LOD algorithm does not a have enough information until at least two points have been considered, so we'll
      // keep track of the number of points considered.
      subState.PointsConsidered++;

      // the algorithm works by keeping track of 2 points, a Base point, which is the last point added, and the
      // point considered most recently. the angle change of each point is calculated based on the difference from the
      // previous angle. the changes are summed, and when the total angular deviation passes a certain threshold,
      // the point /before/ the one causing the deviation to pass the threshold is added, and the deviation reset to
      // zero. the very first point considered is also stored so that AddSubPoint() can simply be called one last time
      // with the original point if the shape is a closed shape.

      if(subState.PointsConsidered >= 3) // if this is the third or subsequent point, we can apply the LOD algorithm
      {
        // get the angle between the new point and the previous point
        double newAngle = Math2D.AngleBetween(subState.PrevPoint.Position, subPoint.Position);
        // get the difference between that angle and the last angle
        double delta = newAngle - subState.PrevAngle;
        
        // if the delta's magnitude is greater than 180 degrees (pi), it's better to consider it as a smaller change
        // in the opposite direction. for instance, a 350 degree change is considered to be a -10 degree change.
        // this makes sure that -10 and 350 are treated as identical deltas.
        if(delta > Math.PI) delta -= Math.PI*2;
        else if(delta < -Math.PI) delta += Math.PI*2;

        subState.Deviation += delta; // accumulate the delta into the total angular deviation
        if(Math.Abs(subState.Deviation) >= lodThreshold) // if the magnitude of the deviation reaches the LOD threshold
        {
          AddSubPointToArray(ref subState.PrevPoint); // add the previous point to the array
          subState.BasePoint = subState.PrevPoint;    // update the BasePoint
          subState.Deviation = 0;                     // and reset the deviation to zero
        }

        subState.PrevPoint = subPoint; // in all cases, update the previous point
        subState.PrevAngle = newAngle; // and the previous angle
      }
      else if(subState.PointsConsidered == 2)
      {
        // if this is the second point being considered, store it into PrevPoint and calculate the angle between it
        // and the base point. then set the deviation to zero.
        subState.PrevPoint = subPoint;
        subState.PrevAngle = Math2D.AngleBetween(subState.BasePoint.Position, subState.PrevPoint.Position);
        subState.Deviation = 0;
      }
      else
      {
        // if this is the first point considered, store it in both FirstPoint and BasePoint.
        subState.FirstPoint = subState.BasePoint = subPoint;
        AddSubPointToArray(ref subPoint); // and always add the first point to the array
      }
    }

    /// <summary>Unconditionally adds a <see cref="SubPoint"/> to the list of subdivision points.</summary>
    void AddSubPointToArray(ref SubPoint subPoint)
    {
      if(subPoints == null || subPoints.Length == numSubPoints) // (re)allocate the array if necessary
      {
        SubPoint[] newPoints = new SubPoint[numSubPoints == 0 ? 8 : numSubPoints*2];
        if(numSubPoints != 0)
        {
          Array.Copy(subPoints, newPoints, numSubPoints);
        }
        subPoints = newPoints;
      }
      
      subPoints[numSubPoints++] = subPoint; // add the point and increment the point count
    }

    /// <summary>Completes the subdivision process.</summary>
    void FlushSubPoints()
    {
      // since the shape is always closed, call AddSubPoint() one last time with the original point
      AddSubPoint(ref subState.FirstPoint);
    }

    /// <summary>Resets the subdivision process state.</summary>
    void ResetSubPoints()
    {
      numSubPoints = 0;
      subState.PointsConsidered = 0;
    }

    /// <summary>Gets a control point index for a clamped spline given the indices of the start and end points of the
    /// spline and the offset into that range.
    /// </summary>
    int GetClampedIndex(int offset, int startPoint, int endPoint)
    {
      if(startPoint < endPoint) // it's a spline clamped to the given points, and they don't span zero
      {
        int newIndex = startPoint + offset; // simply clamp the index to the end points
        return newIndex<startPoint ? startPoint : newIndex>endPoint ? endPoint : newIndex;
      }
      else // the spline is clamped to the given points, and they span the zero index
      {
        // on a number line, the valid values are split. for instance, a polygon with 4 sides may have a spline edge
        // spanning indices 3, 0, and 1. this splits it into two groups: one on the left, based at zero, and one on
        // the right, based at 'startPoint'
        if(offset < 0) return startPoint;            // if the offset is negative, we know it's out of bounds.
        int rightLength = vertices.Count-startPoint; // calculate the number of points in the right group of indices.
        if(offset < rightLength) return startPoint + offset; // if 'offset' is fewer, then it hasn't wrapped around and
                                                             // the index is the 'startPoint' plus the 'offset'
        offset -= rightLength;                    // otherwise it has wrapped, so subtract the number of points on
                                                  // the right side to rebase the offset from the 0-based left side.
        if(offset > endPoint) return endPoint;    // if the offset is greater than the index of the endpoint, clamp it.
        return offset;                            // otherwise, the offset is valid, so return it as an index.
      }
    }
    
    /// <summary>Gets a control point index for a closed spline given the start point of the current segment and the
    /// offset into that segment.
    /// </summary>
    int GetClosedIndex(int offset, int startPoint)
    {
      // in a closed spline, the indices wrap around.
      int newIndex = startPoint + offset;
      return newIndex<0 ? vertices.Count+newIndex : newIndex>=vertices.Count ? newIndex-vertices.Count : newIndex;
    }

    /// <summary>Subdivides the shape into points.</summary>
    void SubdividePolygon()
    {
      ResetSubPoints(); // reset our array of subdivision points, and clear the LOD state

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
          // get the indices of the four spline control points for a closed spline.
          int i0 = GetClosedIndex(0, vertexIndex), i1 = GetClosedIndex(1, vertexIndex),
              i2 = GetClosedIndex(2, vertexIndex), i3 = GetClosedIndex(3, vertexIndex);
          SubdivideSegment(i0, i1, i2, i3); // and subdivide this 
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
            // to create a clamped b-spline, we pretend that there are three of each first and last control point
            for(int i=-2; i < splineLength; i++)
            {
              int i0 = GetClampedIndex(  i, breakPoint, lastEdge), i1 = GetClampedIndex(i+1, breakPoint, lastEdge),
                  i2 = GetClampedIndex(i+2, breakPoint, lastEdge), i3 = GetClampedIndex(i+3, breakPoint, lastEdge);
              SubdivideSegment(i0, i1, i2, i3);
            }
          }

          breakPoint = lastEdge; // advance 'breakPoint' to the next broken point to continue the loop
        } while(examined < vertices.Count);
      }
      
      FlushSubPoints(); // finally, notify the LOD system that we are done so it can add any last points.
    }

    /// <summary>Given the indices of four control points, subdivides a b-spline.</summary>
    void SubdivideSegment(int i0, int i1, int i2, int i3)
    {
      for(int i=0; i<subdivisions; i++)
      {
        // use the B-spline algorithm to calculate the blending factors for this position on the spline
        double delta = i / (double)subdivisions, invDelta = 1 - delta;
        double deltaSquared = delta*delta, deltaCubed = deltaSquared*delta;
        double b0 = invDelta*invDelta*invDelta / 6;                    // (1-delta)^3 / 6
        double b1 = deltaCubed*0.5 - deltaSquared + 4/6.0;             // (3delta^3 - 6delta^2 + 4) / 6
        double b2 = (deltaSquared - deltaCubed + delta + 1/3.0) * 0.5; // (-3delta^3 + 3delta^2 + 3delta + 1) / 6
        double b3 = deltaCubed / 6;                                    // delta^3 / 6

        // using the blending factors (which sum to 1.0), calculate the point on the spline
        SubPoint subPoint = new SubPoint();
        subPoint.Position.X = b0*vertices[i0].Position.X + b1*vertices[i1].Position.X +
                              b2*vertices[i2].Position.X + b3*vertices[i3].Position.X;
        subPoint.Position.Y = b0*vertices[i0].Position.Y + b1*vertices[i1].Position.Y +
                              b2*vertices[i2].Position.Y + b3*vertices[i3].Position.Y;

        if(shadeModel == ShadeModel.Flat) // if it's a flat shade model, we don't need to interpolate the color
        {
          subPoint.Color = vertices[i0].Color;
        }
        else // otherwise, we do.
        {
          int a, r, g, b;

          // use the same blending factors to calculate the interpolated colors
          a = (int)Math.Round(vertices[i0].Color.A*b0 + vertices[i1].Color.A*b1 +
                              vertices[i2].Color.A*b2 + vertices[i3].Color.A*b3);
          r = (int)Math.Round(vertices[i0].Color.R*b0 + vertices[i1].Color.R*b1 +
                              vertices[i2].Color.R*b2 + vertices[i3].Color.R*b3);
          g = (int)Math.Round(vertices[i0].Color.G*b0 + vertices[i1].Color.G*b1 +
                              vertices[i2].Color.G*b2 + vertices[i3].Color.G*b3);
          b = (int)Math.Round(vertices[i0].Color.B*b0 + vertices[i1].Color.B*b1 +
                              vertices[i2].Color.B*b2 + vertices[i3].Color.B*b3);

          subPoint.Color = Color.FromArgb(a, r, g, b);
        }

        AddSubPoint(ref subPoint); // attempt to add the new point to the shape
      }
    }

    void Tessellate()
    {
      // reset our collections
      if(tessPrimitives == null) tessPrimitives = new List<uint>(4);
      if(tessVertices == null) tessVertices = new List<int[]>();
      tessPrimitives.Clear();
      tessVertices.Clear();

      SubdividePolygon(); // subdivide the shape into points
      // if subdivision resulted in less than 3 points, we can't tessellate.
      // this can happen if the LOD value is set too high.
      if(numSubPoints < 3) return;

      // the tessellation process will create OpenGL primitives composed of vertices (the vertices will be stored as
      // indices into the list of subdivision points).
      List<int> currentVertices = new List<int>();  // the vertices for the current OpenGL primitive
      uint currentPrimitive = 0;                    // the current OpenGL primitive
      bool errorOccurred = false;                   // whether an error occurred during the tessellation process

      IntPtr tess = GLU.gluNewTess(); // create a new OpenGL tessellator
      if(tess == IntPtr.Zero) throw new OutOfMemoryException();

      try
      {
        // set up the callbacks for the tessellator.
        // we'll put the delegates in variables to prevent them from being freed by the garbage collector
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

          // OpenGL requires all the vertex data to be passed as pointers, and these pointers won't be evaluated until
          // the very end. So, we need to create some space to hold the vertex data until tessellation is finished.
          // we'll use stackalloc to avoid the need for heap allocation and pinning.
          double* vertexData = stackalloc double[numSubPoints*3];
          for(int i=0, j=0; i<numSubPoints; i++, j+=3) // for each vertex
          {
            vertexData[j]   = subPoints[i].Position.X; // add it to the vertex array
            vertexData[j+1] = subPoints[i].Position.Y;
            vertexData[j+2] = 0;                       // Z is zero
            GLU.gluTessVertex(tess, vertexData+j, new IntPtr(i)); // and give the pointer to OpenGL
          }

          GLU.gluTessEndContour(tess);
          GLU.gluTessEndPolygon(tess); // finish the tessellation
        }
      }
      finally
      {
        GLU.gluDeleteTess(tess);
      }

      // if an error occurred, clear our collections. the renderer will fall back on rendering the outline.
      if(errorOccurred)
      {
        tessPrimitives.Clear();
        tessVertices.Clear();
      }

      // finally, mark that we have finished tessellation. this should be set even if an error occurred, because we
      // don't want to repeatedly try to tessellate bad data.
      tessellationDirty = false;
    }

    /// <summary>The vertices in this polygon.</summary>
    List<Vertex> vertices = new List<Vertex>(4);
    /// <summary>Cached tessellation info containing the list of primitives needed to render the polygon.</summary>
    [NonSerialized] List<uint> tessPrimitives;
    /// <summary>Cached tessellation info containing the list of vertex indices.</summary>
    [NonSerialized] List<int[]> tessVertices;
    /// <summary>Cached subdivision info containing the subdivision points.</summary>
    [NonSerialized] SubPoint[] subPoints;

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
    [NonSerialized] int numSubPoints;
    /// <summary>The state of the current subdivision process.</summary>
    [NonSerialized] SubdivisionState subState;
    /// <summary>The angular devation threshold for the LOD process, in radians. If the curve bends more than this
    /// amount, a new point will be added.
    /// </summary>
    double lodThreshold = DefaultLOD;
    /// <summary>The number of subdivisions of each spline curve.</summary>
    int subdivisions = DefaultSubdivisions;
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
    [NonSerialized] bool tessellationDirty;
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
          InvalidateGeometry();
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
          EngineMath.AssertValidFloats(value.X, value.Y);
          position = value;
          InvalidateGeometry();
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
          InvalidateGeometry();
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
          EngineMath.AssertValidFloats(value.X, value.Y);
          if(value.X < 0 || value.X > 1 || value.Y < 0 || value.Y > 1)
          {
            throw new ArgumentOutOfRangeException("Texture coordinates must be from 0 to 1.");
          }
          textureCoord = value;
          InvalidateGeometry();
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

    [NonSerialized] internal Polygon Polygon;

    void InvalidateGeometry()
    {
      if(Polygon != null) Polygon.InvalidateGeometry();
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