using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameLib.Mathematics.TwoD;
using Color = System.Drawing.Color;

namespace RotationalForce.Engine
{

/// <summary>Determines the type of shading to use.</summary>
public enum ShadeModel : byte
{
  Flat, Smooth
}

/// <summary>Determines how an animation loops.</summary>
public enum LoopType : byte
{
  /// <summary>The animation does not loop, and stops at the end.</summary>
  NoLoop,
  /// <summary>The animation restarts from the beginning after it reaches the end.</summary>
  Forward,
  /// <summary>The animation plays backwards and restarts at the end after reaching the beginning.</summary>
  Reverse,
  /// <summary>The animation plays forwards and backwards, changing direction when it reaches the beginning or end.</summary>
  PingPong
}

public class VectorObject : SceneObject
{
  #region Animation
  public sealed class Animation
  {
    [Browsable(false)]
    public ReadOnlyCollection<Frame> Frames
    {
      get { return new ReadOnlyCollection<Frame>(frames); }
    }

    [Category("Behavior")]
    [Description("Determines how the animation loops by default.")]
    [DefaultValue(LoopType.NoLoop)]
    public LoopType Looping
    {
      get { return looping; }
      set { looping = value; }
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
    
    LoopType looping;
  }
  #endregion

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

  struct AnimationData
  {
    /// <summary>The position within the animation, in seconds.</summary>
    public double Position;
    /// <summary>Indicates whether the animation has completed.</summary>
    public bool Complete;
  }

  /// <summary>The object's current animation.</summary>
  Animation animation;
  /// <summary>The object's animation speed, expressed as a multiple.</summary>
  double animationSpeed;
}

} // namespace RotationalForce.Engine