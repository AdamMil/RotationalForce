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
  #region Nested classes
  #region Frame
  public sealed class Frame
  {
    /// <summary>The length of time spent rendering this frame, in seconds, at the default animation speed.</summary>
    [Category("Behavior")]
    [Description("The length of time spent rendering this frame, in seconds, at the default animation speed.")]
    public double FrameTime
    {
      get { return frameTime; }
      set
      {
        if(value < 0) throw new ArgumentOutOfRangeException("FrameTime", value, "FrameTime cannot be negative.");
        frameTime = value;
      }
    }

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
      if(polygon == null) throw new ArgumentNullException("frame");
      polygons.Add(polygon);
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

    /// <summary>The length of this frame, in seconds.</summary>
    double frameTime;
    /// <summary>The polygons that make up this animation frame.</summary>
    List<Polygon> polygons = new List<Polygon>(4);
    /// <summary>Determines whether this frame will be interpolated into the next frame.</summary>
    bool interpolate = true;
  }
  #endregion

  #region Polygon
  public sealed class Polygon
  {
    [Browsable(false)]
    public ReadOnlyCollection<Vertex> Vertices
    {
      get { return new ReadOnlyCollection<Vertex>(vertices); }
    }

    /// <summary>Gets/sets the shade model for this polygon.</summary>
    [Category("Shading")]
    [Description("Determines whether the polygon will use flat shading or smooth shading.")]
    [DefaultValue(ShadeModel.Flat)]
    public ShadeModel ShadeModel
    {
      get { return shadeModel; }
      set { shadeModel = value; }
    }

    #region Blending
    /// <summary>Determines whether blending is explicitly enabled for this polygon.</summary>
    /// <remarks>Note that even if this value is set to false, blending may still be enabled if the parent object has
    /// blending enabled.
    /// </remarks>
    [Category("Blending")]
    [Description("Determines whether blending is explicitly enabled for this polygon. Note that even if this value "+
      "is set to false, blending may still be enabled if the parent object has blending enabled.")]
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
    [Description("Gets/sets the source blending mode of the polygon, if blending is enabled. A value of Default will "+
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
    [Description("Gets/sets the destination blending mode of the polygon, if blending is enabled. A value of Default "+
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
          throw new ArgumentOutOfRangeException("StrokeWidth", value, "The stroke width cannot be negative.");
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
      get { return genTextCoords; }
      set { genTextCoords = value; }
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
      set { textureName = value; }
    }

    /// <summary>The amount by which the texture is shifted, in texture coordinates.</summary>
    /// <remarks>Texture offset occurs after texture rotation. Texture offset is only used when autogenerating texture
    /// coordinates.
    /// </remarks>
    [Category("Texture")]
    [Description("The amount by which the texture is shifted, in texture coordinates.")]
    public Vector TextureOffset
    {
      get { return textureOffset;  }
      set { textureOffset = value; }
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
      get { return textureRotation;  }
      set { textureRotation = value; }
    }
    #endregion

    public void AddVertex(Vertex vertex)
    {
      vertices.Add(vertex);
    }
    
    public void InsertVertex(int index, Vertex vertex)
    {
      vertices.Insert(index, vertex);
    }

    public void RemoveVertex(int index)
    {
      vertices.RemoveAt(index);
    }

    public void Render()
    {
      if(vertices.Count < 3) return; // if we don't have a valid polygon yet, return

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

      // then, draw the interior of the polygon
      GL.glBegin(GL.GL_POLYGON);
      for(int i=0; i<vertices.Count; i++)
      {
        GL.glColor(vertices[i].Color);
        GL.glVertex2d(vertices[i].Position);
      }
      GL.glEnd();

      if(strokeWidth != 0 && strokeColor.A != 0) // if stroking is enabled
      {
        GL.glLineWidth(strokeWidth); // set the stroke width

        if(blendEnabled) // if blending is enabled, we'll use antialiased lines
        {
          GL.glEnable(GL.GL_LINE_SMOOTH);
          GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
        }
        else if(shadeModel == ShadeModel.Flat) // otherwise, if the shade model is flat, we'll set the color once
        {
          GL.glColor(vertices[0].Color.A, strokeColor);
        }

        // now stroke the edges of the polygon
        GL.glBegin(GL.GL_LINE_LOOP);
        for(int i=0; i<vertices.Count; i++)
        {
          // if we're using antialiased lines or a smooth shading model, we'll set the color at each vertex.
          // this allows the alpha value of the border to match that of the polygon edge
          if(blendEnabled || shadeModel == ShadeModel.Smooth)
          {
            GL.glColor(vertices[i].Color.A, strokeColor);
          }
          GL.glVertex2d(vertices[i].Position);
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
    /// <summary>The vertices in this polygon.</summary>
    List<Vertex> vertices = new List<Vertex>(4);
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
    bool genTextCoords = true;
  }
  #endregion

  #region Vertex
  public struct Vertex
  {
    /// <summary>This vertex's position within the object.</summary>
    public Point Position;
    /// <summary>This vertex's texture coordinate. It will only be used if a texture is specified in the parent
    /// polygon.
    /// </summary>
    public Point TextureCoord;
    /// <summary>The vertex's color.</summary>
    public Color Color;
  }
  #endregion
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
  
  public void InsertFrame(int index, Frame frame)
  {
    if(frame == null) throw new ArgumentNullException("frame");
    frames.Insert(index, frame);
  }

  public void RemoveFrame(int index)
  {
    frames.RemoveAt(index);
  }

  /// <summary>The frames that make up this animation.</summary>
  List<Frame> frames = new List<Frame>(4);

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
}
#endregion

public class VectorObject : AnimatedObject
{
  protected override void ValidateAnimation(Animation animation)
  {
    if(!(animation is VectorAnimation))
      throw new ArgumentException("This is not the right kind of animation! (Expecting a VectorAnimation)");
  }
}

} // namespace RotationalForce.Engine