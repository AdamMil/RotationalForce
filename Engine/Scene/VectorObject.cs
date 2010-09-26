using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AdamMil.Mathematics.Geometry;
using AdamMil.Mathematics.Geometry.TwoD;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics;
using RotationalForce.Engine.Design;
using Color=System.Drawing.Color;

namespace RotationalForce.Engine
{

public delegate void VectorAnimationEventHandler(VectorObject obj, VectorAnimationData animData);

#region VectorShape
/// <summary>Represents a vector shape, along with any animations that apply to that shape.</summary>
/// <remarks>A vector shape is composed of two parts: a hierarchical shape geometry, and one or more animations that
/// can morph that geometry. The shape geometry is stored as a tree of nodes, which are either group nodes or polygon
/// nodes. Group nodes simply contain other nodes. Polygon nodes are leaf nodes which render a polygon. Animations can
/// morph the geometry by either rotating, translating, and/or scaling nodes, or by replacing a polygon with another.
/// Multiple animations can be applied to the same shape simultaneously.
/// </remarks>
[ResourceKey]
public class VectorShape : Resource
{
  public VectorShape() { }

  public VectorShape(string resourceName)
  {
    this.Name = resourceName;
  }

  #region Animation
  /// <summary>Represents an animation of a vector shape.</summary>
  /// <remarks>An animation is composed of frames. Each frame can contain one or more modifiers that morph the geometry
  /// of the vector shape.
  /// </remarks>
  public sealed class Animation
  {
    public Animation() { }

    public Animation(string name)
    {
      Name = name;
    }

    /// <summary>Gets or sets the name of the animation. This must be non-empty and unique within the shape.</summary>
    [Description("The name of the animation. This must be unique within the shape.")]
    public string Name
    {
      get { return name; }
      set
      {
        if(string.IsNullOrEmpty(value)) throw new ArgumentException("Animation name cannot be empty.");

        if(!string.Equals(value, name, StringComparison.Ordinal))
        {
          if(shape != null)
          {
            shape.AssertValidAndUniqueAnimationName(value);
          }

          name = value;
        }
      }
    }

    /// <summary>Gets or sets the algorithm used to interpolate between frames.</summary>
    [Description("Determines how the animation interpolates values between frames.")]
    [DefaultValue(InterpolationMode.Linear)]
    public InterpolationMode Interpolation
    {
      get { return interpolation; }
      set { interpolation = value; }
    }

    /// <summary>Gets or sets how the animation loops, if at all.</summary>
    [Description("Determines how the animation loops, if at all.")]
    [DefaultValue(LoopType.NoLoop)]
    public LoopType Looping
    {
      get { return looping; }
      set { looping = value; }
    }

    /// <summary>A read-only collection of <see cref="Frame"/> objects that make up this animation.</summary>
    [Browsable(false)]
    public ReadOnlyCollection<Frame> Frames
    {
      get { return new ReadOnlyCollection<Frame>(frames); }
    }

    /// <summary>Adds a frame to the end of the animation.</summary>
    public void AddFrame(Frame frame)
    {
      InsertFrame(frames.Count, frame);
    }

    /// <summary>Inserts a frame into the animation.</summary>
    public void InsertFrame(int index, Frame frame)
    {
      if(frame == null) throw new ArgumentNullException();
      if(frame.Animation != null) throw new ArgumentException("Frame already belongs to an animation.");
      frame.Animation = this;
      frames.Insert(index, frame);
      InvalidateModifierData();
    }

    /// <summary>Removes a frame from the animation.</summary>
    public void RemoveFrame(int index)
    {
      Frame frame = frames[index];
      frame.Animation = null;
      frames.RemoveAt(index);
      InvalidateModifierData();
    }

    /// <summary>Advances an animation by a given amount of time.</summary>
    public void Simulate(double timeDelta, ref VectorAnimationData data)
    {
      throw new NotImplementedException();
    }

    /// <summary>Gets or sets the <see cref="VectorShape"/> that owns this animation.</summary>
    internal VectorShape Shape
    {
      get { return shape; }
      set
      {
        if(value != shape)
        {
          shape = value;
          InvalidateModifierData();
        }
      }
    }

    /// <summary>Applies data from the modifiers for the current frame to the nodes of the vector shape.</summary>
    /// <remarks>The data applied to the nodes, which will be used to morph their rendering, will only last until the
    /// next render, and must be reapplied after each rendering.
    /// </remarks>
    internal void ApplyModifiers(ref AnimationData data)
    {
      EnsureModifierData();

      int frameIndex = data.Frame;          // get the current frame index
      Frame frame    = frames[frameIndex];  // and the associated Frame
      double delta;

      if(frame.Interpolated) // if the frame is interpolated, calculate how far along we are in the frame from 0-1
      {
        delta = data.Offset / frame.FrameTime; // this is the linear offset into the frame, from 0-1
        // if a non-catmull interpolation is applied, we can optimize by precalculating a value that can be used with
        // linear interpolation
        if(interpolation != InterpolationMode.Catmull)
        {
          delta = EngineMath.CalculateLinearDelta(delta, interpolation);
        }
      }
      else
      {
        delta = 0;
      }

      for(int modIndex=0; modIndex<modifierData.Length; modIndex++) // for each node that has modifier data
      {
        Node affectedNode = modifierData[modIndex].Key;
        ModifierData modData = modifierData[modIndex].Value;

        if(modData.Polygons != null) // apply polygon changes if there are any
        {
          ((PolygonNode)affectedNode).tempPoly = modData.Polygons[frameIndex];

          // if polygon changes were all that there were, then we're done with this node.
          if(modData.Rotations == null && modData.Scalings == null && modData.Translations == null) continue;
        }

        // we combine the modifier values with the existing values not because there may be multiple modifiers on the
        // same node (that possibility is already factored into the modData collection), but because there may be
        // multiple animations being applied to the same shape

        if(!frame.Interpolated) // if the frame is not interpolated, apply the full values of the frame's modifiers
        {
          if(modData.Rotations != null)
          {
            affectedNode.tempRotation += modData.Rotations[frameIndex];
          }
          if(modData.Scalings != null)
          {
            affectedNode.tempScaling.X *= modData.Scalings[frameIndex].X;
            affectedNode.tempScaling.Y *= modData.Scalings[frameIndex].Y;
          }
          if(modData.Translations != null)
          {
            affectedNode.tempTranslation += modData.Translations[frameIndex];
          }
        }
        // otherwise, if it's not catmull interpolation, we can use the linear interpolation value calculated above
        else if(interpolation != InterpolationMode.Catmull)
        {
          int nextFrameIndex = GetFrameIndex(frameIndex + 1); // we'll interpolate between this frame and the next

          if(modData.Rotations != null)
          {
            affectedNode.tempRotation +=
                EngineMath.Interpolate(modData.Rotations[frameIndex], modData.Rotations[nextFrameIndex], delta);
          }

          if(modData.Scalings != null)
          {
            Vector scale = modData.Scalings[frameIndex], nextScale = modData.Scalings[nextFrameIndex];
            affectedNode.tempScaling.X *= EngineMath.Interpolate(scale.X, nextScale.X, delta);
            affectedNode.tempScaling.Y *= EngineMath.Interpolate(scale.Y, nextScale.Y, delta);
          }

          if(modData.Translations != null)
          {
            Vector translation = modData.Translations[frameIndex],
               nextTranslation = modData.Translations[nextFrameIndex];
            affectedNode.tempTranslation.X += EngineMath.Interpolate(translation.X, nextTranslation.X, delta);
            affectedNode.tempTranslation.Y += EngineMath.Interpolate(translation.Y, nextTranslation.Y, delta);
          }
        }
        // otherwise, it's catmull interpolation, meaning that we need values from four frames instead of two
        else
        {
          // square and cube the delta value beforehand so it doesn't need to be done per value
          double deltaSquared = delta*delta, deltaCubed = deltaSquared*delta;
          // get the indices of the four frames to be used in the calculation
          int prevFrame = GetFrameIndex(frameIndex-1), endFrame = GetFrameIndex(frameIndex+1),
              nextFrame = GetFrameIndex(frameIndex+2);
          // get the three distances (in time) between those four frames
          double frameTime0 = frames[prevFrame].FrameTime, frameTime1 = frames[frameIndex].FrameTime,
                 frameTime2 = frames[endFrame].FrameTime;

          if(modData.Rotations != null)
          {
            CatmullInterpolator ci =
                new CatmullInterpolator(modData.Rotations[prevFrame], modData.Rotations[frameIndex],
                                        modData.Rotations[endFrame],  modData.Rotations[nextFrame],
                                        frameTime0, frameTime1, frameTime2);
            affectedNode.tempRotation += ci.Interpolate(delta, deltaSquared, deltaCubed);
          }

          if(modData.Scalings != null)
          {
            Vector scale0 = modData.Scalings[prevFrame], scale1 = modData.Scalings[frameIndex],
                   scale2 = modData.Scalings[endFrame],  scale3 = modData.Scalings[nextFrame];
            CatmullInterpolator ci = new CatmullInterpolator(scale0.X, scale1.X, scale2.X, scale3.X,
                                                             frameTime0, frameTime1, frameTime2);
            affectedNode.tempScaling.X *= ci.Interpolate(delta, deltaSquared, deltaCubed);
            ci = new CatmullInterpolator(scale0.Y, scale1.Y, scale2.Y, scale3.Y,
                                         frameTime0, frameTime1, frameTime2);
            affectedNode.tempScaling.Y *= ci.Interpolate(delta, deltaSquared, deltaCubed);
          }

          if(modData.Translations != null)
          {
            Vector trans0 = modData.Translations[prevFrame], trans1 = modData.Translations[frameIndex],
                   trans2 = modData.Translations[endFrame],  trans3 = modData.Translations[nextFrame];
            CatmullInterpolator ci = new CatmullInterpolator(trans0.X, trans1.X, trans2.X, trans3.X,
                                                             frameTime0, frameTime1, frameTime2);
            affectedNode.tempTranslation.X += ci.Interpolate(delta, deltaSquared, deltaCubed);
            ci = new CatmullInterpolator(trans0.Y, trans1.Y, trans2.Y, trans3.Y,
                                         frameTime0, frameTime1, frameTime2);
            affectedNode.tempTranslation.Y += ci.Interpolate(delta, deltaSquared, deltaCubed);
          }
        }
      }
    }

    internal void InvalidateModifierData()
    {
      modifierData = null;
    }

    internal void OnDeserialized()
    {
      // reset the frames' Animation pointers, which are not serialized. this is done here instead of in an
      // implementation of ISerializable.Deserialize because the containing shape must be fully deserialized first.
      foreach(Frame frame in frames)
      {
        frame.Animation = this;
      }
    }

    struct ModifierData
    {
      public ModifierData(double[] rotation, Vector[] scaling, Vector[] translation, Polygon[] polygons)
      {
        Rotations    = rotation;
        Scalings     = scaling;
        Translations = translation;
        Polygons     = polygons;
      }

      public double[] Rotations;
      public Vector[] Scalings, Translations;
      public Polygon[] Polygons;
    }

    void EnsureModifierData()
    {
      if(modifierData != null) return;

      List<KeyValuePair<Node,ModifierData>> data = new List<KeyValuePair<Node,ModifierData>>(shape.nodeMap.Count);

      foreach(KeyValuePair<string,Node> de in shape.nodeMap) // for each node in the geometry
      {
        // these arrays will hold the effective rotation, scaling, translation, and polygon of the node for each frame
        // after all modifications have been applied. if a node has no rotations, etc, over the course of the
        // animation, the array will be null.
        double[] rotation = null;
        Vector[] scaling = null, translation = null;
        Polygon[] polygons = null;

        for(int frameIndex=0; frameIndex<frames.Count; frameIndex++)
        {
          // if any of the arrays are not null, propogate the value from the last frame to the current frame
          if(rotation != null)
          {
            rotation[frameIndex] = rotation[frameIndex-1];
          }
          if(scaling != null)
          {
            scaling[frameIndex] = scaling[frameIndex-1];
          }
          if(translation != null)
          {
            translation[frameIndex] = translation[frameIndex-1];
          }
          if(polygons != null)
          {
            polygons[frameIndex] = polygons[frameIndex-1];
          }

          foreach(Modifier modifier in frames[frameIndex].Modifiers)
          {
            // if the modifier does not apply to the current node, skip it
            if(!string.Equals(modifier.TargetNode, de.Key, StringComparison.Ordinal)) continue;

            // if it's a polygon-replacement modifier, add the new polygon to the array
            PolygonReplacer replacer = modifier as PolygonReplacer;
            if(replacer != null)
            {
              if(polygons == null)
              {
                polygons = new Polygon[frames.Count];
              }
              polygons[frameIndex] = replacer.Polygon;
            }

            // otherwise, if it's a node modifier, apply its rotation, scaling, and translation
            NodeModifier mod = modifier as NodeModifier;
            if(mod != null)
            {
              if(mod.Rotation != 0)
              {
                if(rotation == null)
                {
                  rotation = new double[frames.Count];
                }
                rotation[frameIndex] += mod.Rotation;
              }

              if(mod.Scaling.X != 1 || mod.Scaling.Y != 1)
              {
                if(scaling == null)
                {
                  scaling = new Vector[frames.Count];
                  // scaling is done by multiplication, so the identity value is not zero, but one. initialize to that.
                  for(int i=0; i<=frameIndex; i++)
                  {
                    scaling[i] = new Vector(1, 1);
                  }
                }

                scaling[frameIndex] =
                  new Vector(scaling[frameIndex].X*mod.Scaling.X, scaling[frameIndex].Y*mod.Scaling.Y);
              }

              if(mod.Translation.X != 0 || mod.Translation.Y != 0)
              {
                if(translation == null)
                {
                  translation = new Vector[frames.Count];
                }
                translation[frameIndex] += mod.Translation;
              }
            }
          }
        }

        // if any modifier of any frame referenced this node, add the modifier data
        if(rotation != null || scaling != null || translation != null || polygons != null)
        {
          data.Add(new KeyValuePair<Node,ModifierData>(de.Value,
                           new ModifierData(rotation, scaling, translation, polygons)));
        }
      }
      
      modifierData = data.ToArray();
    }

    /// <summary>Converts a possibly-invalid frame index to a valid one based on the animation's loop type.</summary>
    int GetFrameIndex(int index)
    {
      if(frames.Count == 0) throw new ArgumentOutOfRangeException();

      switch(looping)
      {
        case LoopType.NoLoop: // if no looping, clamp frame indexes
          if(index < 0) index = 0;
          else if(index >= frames.Count) index = frames.Count - 1;
          break;

        case LoopType.Repeat: // if repeating, wrap frame indexes
          if(index < 0)
          {
            do index += frames.Count; while(index < 0);
          }
          else if(index >= frames.Count)
          {
            do index -= frames.Count; while(index >= frames.Count);
          }
          break;

        case LoopType.PingPong: // for pingpong loops, bounce the index between the edges until it becomes valid
          while(true)
          {
            if(index < 0)
            {
              index = -index;
            }
            else if(index >= frames.Count)
            {
              index = frames.Count - (index - frames.Count);
            }
            else break;
          }
          break;

        default:
          throw new NotImplementedException();
      }

      return index;
    }

    List<Frame> frames = new List<Frame>(4);
    [NonSerialized] KeyValuePair<Node,ModifierData>[] modifierData;
    [NonSerialized] VectorShape shape;
    string name;
    InterpolationMode interpolation;
    LoopType looping;
  }
  #endregion

  #region Frame
  /// <summary>Represents a frame within a vector animation.</summary>
  public sealed class Frame : AnimationFrame
  {
    /// <summary>Gets or sets whether this frame is interpolated into the next frame in the animation.</summary>
    [Description("Determines whether this frame is interpolated into the next frame in the animation. Interpolation "+
      "produces smoother animations at the cost of some performance.")]
    [DefaultValue(true)]
    public bool Interpolated
    {
      get { return interpolated; }
      set { interpolated = value; }
    }

    [Browsable(false)]
    public ReadOnlyCollection<Modifier> Modifiers
    {
      get { return new ReadOnlyCollection<Modifier>(modifiers); }
    }

    /// <summary>Adds a modifier to this frame.</summary>
    public void AddModifier(Modifier modifier)
    {
      InsertModifier(modifiers.Count, modifier);
    }

    /// <summary>Inserts a modifier into this frame.</summary>
    public void InsertModifier(int index, Modifier modifier)
    {
      if(modifier == null) throw new ArgumentNullException();
      if(modifier.anim != null) throw new ArgumentException("Modifier already belongs to an animation.");
      if(anim != null && anim.Shape != null)
      {
        anim.Shape.AssertValidNodeName(modifier.TargetNode);
      }

      modifier.anim = anim;
      modifiers.Insert(index, modifier);
      InvalidateModifierData();
    }

    /// <summary>Removes a modifier from this frame.</summary>
    public void RemoveModifier(int index)
    {
      Modifier mod = modifiers[index];
      mod.anim = null;
      modifiers.RemoveAt(index);
      InvalidateModifierData();
    }

    /// <summary>Gets or sets the animation that owns this frame.</summary>
    internal Animation Animation
    {
      get { return anim; }
      set
      {
        if(value != anim)
        {
          if(value != null && value.Shape != null)
          {
            foreach(Modifier mod in modifiers) // validate that each modifier references a valid node name
            {
              value.Shape.AssertValidNodeName(mod.TargetNode);
            }
          }

          anim = value;

          foreach(Modifier mod in modifiers) // update the animation pointers of all the modifiers
          {
            mod.anim = value;
          }
        }
      }
    }

    // invalidate the animation's modifier data if this frame is changed
    void InvalidateModifierData()
    {
      if(anim != null) anim.InvalidateModifierData();
    }

    /// <summary>A list containing the modifiers within this frame.</summary>
    List<Modifier> modifiers = new List<Modifier>(4);
    /// <summary>A reference to the animation that owns this frame.</summary>
    [NonSerialized] Animation anim;
    /// <summary>Whether or not this frame is interpolated into the next frame.</summary>
    bool interpolated = true;
  }
  #endregion

  #region Modifier
  /// <summary>Represents a modifier within an animation frame.</summary>
  public abstract class Modifier
  {
    internal Modifier() { } // disallow external subclassing

    /// <summary>Gets or sets the name of the node to which this modifier applies.</summary>
    public string TargetNode
    {
      get { return targetNode; }
      set
      {
        if(!string.Equals(targetNode, value, StringComparison.Ordinal))
        {
          if(anim != null && anim.Shape != null)
          {
            anim.Shape.AssertValidNodeName(value);
          }

          targetNode = value;
        }
      }
    }

    /// <summary>Informs the animation that the modifier data has changed.</summary>
    protected void OnModifierDataChanged()
    {
      if(anim != null) anim.InvalidateModifierData();
    }

    [NonSerialized] internal Animation anim;
    string targetNode;
  }

  /// <summary>Represents a modifier that changes the rotation, scaling, or position of a node.</summary>
  public sealed class NodeModifier : Modifier
  {
    /// <summary>The amount the node should be rotated, in degrees. Note that rotations outside the range of 0-360
    /// should not be normalized.
    /// </summary>
    /// <remarks>-90 degrees is not the same as 270 degrees. Although the end result will be the same, the rotation
    /// may be interpolated along the way, meaning that the direction and speed of the rotation depend on the sign
    /// and magnitude of this value.
    /// </remarks>
    public double Rotation
    {
      get { return rotation; }
      set
      {
        EngineMath.AssertValidFloat(value);
        if(value != rotation)
        {
          rotation = value;
          OnModifierDataChanged();
        }
      }
    }

    /// <summary>A pair of scaling factors to be applied to the node. The pair (1, 1) will result in no scaling, while
    /// (2, 0.5) will double the size in the X dimension while halving the size in the Y dimension.
    /// </summary>
    public Vector Scaling
    {
      get { return scaling; }
      set
      {
        EngineMath.AssertValidFloats(value.X, value.Y);
        if(value != scaling)
        {
          scaling = value;
          OnModifierDataChanged();
        }
      }
    }

    /// <summary>The distance that the node will be translated, in local coordinates.</summary>
    public Vector Translation
    {
      get { return translation; }
      set
      {
        EngineMath.AssertValidFloats(value.X, value.Y);
        if(value != translation)
        {
          translation = value;
          OnModifierDataChanged();
        }
      }
    }

    Vector scaling = new Vector(1, 1), translation;
    double rotation;
  }

  /// <summary>Represents a modifier that replaces a node's polygon. This modifier can only be applied to
  /// <see cref="PolygonNode"/> objects.
  /// </summary>
  public sealed class PolygonReplacer : Modifier
  {
    /// <summary>Gets a reference to the polygon that will serve as the replacement.</summary>
    public Polygon Polygon
    {
      get { return poly; }
    }

    Polygon poly = new Polygon();
  }
  #endregion

  #region Node classes
  #region Node
  /// <summary>Represents a node in the vector shape's hierarchical geometry.</summary>
  public abstract class Node
  {
    internal Node() { } // disallow external derivation

    internal Node(string name)
    {
      if(string.IsNullOrEmpty(name)) throw new ArgumentException("Node name cannot be empty.");
      this.name = name;
    }

    /// <summary>Gets a collection containing the children of this node.</summary>
    public abstract ReadOnlyCollection<Node> Children { get; }

    /// <summary>Gets or sets the node's name. This must be unique within a shape.</summary>
    public string Name
    {
      get { return name; }
      set
      {
        if(!string.Equals(name, value, StringComparison.Ordinal))
        {
          if(shape != null)
          {
            shape.AssertValidAndUniqueNodeName(value);
            shape.OnNodeRenamed(name, value);
          }
          name = value;
        }
      }
    }

    /// <summary>Gets or sets the origin point of the node, which is the point around which the node will be rotated.
    /// The point is specified in local coordinates relative to the center of the node.
    /// </summary>
    public Point Origin
    {
      get { return origin; }
      set
      {
        EngineMath.AssertValidFloats(origin.X, origin.Y);
        origin = value;
      }
    }

    /// <summary>Gets the bounding area of the node, in local coordinates.</summary>
    public abstract Rectangle GetBounds();

    /// <summary>Renders the content of this node and calls <see cref="Render"/> on its children.</summary>
    protected abstract void RenderContent(float screenSize);

    /// <summary>Renders this node and its children. Assumes all desired modifiers have been applied.</summary>
    internal void Render(float screenSize)
    {
      bool pushedMatrix = false; // indicates whether we've pushed the GL matrix stack

      // if scaling is applied to this node, scale the matrix and reset the scaling.
      if(tempScaling.X != 1 || tempScaling.Y != 1)
      {
        if(!pushedMatrix)
        {
          GL.glPushMatrix();
          pushedMatrix = true;
        }

        GL.glScaled(tempScaling.X, tempScaling.Y, 1);

        tempScaling = new Vector(1, 1);
      }

      // if rotation is applied to this node, rotate the matrix and reset the rotation.
      if(tempRotation != 0)
      {
        if(!pushedMatrix)
        {
          GL.glPushMatrix();
          pushedMatrix = true;
        }

        // if the effective origin is the center, we can simply rotate
        if(effectiveOrigin.X == 0 && effectiveOrigin.Y == 0)
        {
          GL.glRotated(tempRotation, 0, 0, 1);
        }
        else // otherwise we must translate there and back
        {
          GL.glTranslated(effectiveOrigin.X, effectiveOrigin.Y, 0);
          GL.glRotated(tempRotation, 0, 0, 1);
          GL.glTranslated(-effectiveOrigin.X, -effectiveOrigin.Y, 0);
        }
        
        tempRotation = 0;
      }

      // if translation is applied to this node, translate the matrix and reset the translation
      if(tempTranslation.X != 0 || tempTranslation.Y != 0)
      {
        if(!pushedMatrix)
        {
          GL.glPushMatrix();
          pushedMatrix = true;
        }

        GL.glTranslated(tempTranslation.X, tempTranslation.Y, 0);
        
        tempTranslation = new Vector();
      }
      
      RenderContent(screenSize); // render the content of this node with the transformations applied
      
      if(pushedMatrix) // then, pop the GL matrix if we pushed it
      {
        GL.glPopMatrix();
      }
    }

    /// <summary>Called to recalculate the effective origins points of the nodes when the geometry changes.</summary>
#warning RecalculateEffectiveOrigins is never called
    internal void RecalculateEffectiveOrigins()
    {
      effectiveOrigin = EngineMath.GetCenterPoint(GetBounds()) + new Vector(origin);

      foreach(Node node in Children)
      {
        node.RecalculateEffectiveOrigins();
      }
    }

    /// <summary>A reference to the shape that owns this node.</summary>
    [NonSerialized] internal VectorShape shape;

    /// <summary>The node's name.</summary>
    string name;
    /// <summary>The origin point relative to the center of the geometry.</summary>
    Point origin;
    /// <summary>The absolute origin point in local space.</summary>
    Point effectiveOrigin;

    // these are only used temporarily, for the current render
    [NonSerialized] internal Vector tempScaling = new Vector(1, 1), tempTranslation;
    [NonSerialized] internal double tempRotation;
  }
  #endregion

  #region GroupNode
  /// <summary>Represents a node that groups other nodes together.</summary>
  public sealed class GroupNode : Node
  {
    public GroupNode() { }
    public GroupNode(string name) : base(name) { }

    public override ReadOnlyCollection<Node> Children
    {
      get { return new ReadOnlyCollection<Node>(children); }
    }

    /// <summary>Adds a child node to this group.</summary>
    public void AddChild(Node node)
    {
      InsertChild(children.Count, node);
    }

    /// <summary>Inserts a child node into this group.</summary>
    public void InsertChild(int index, Node node)
    {
      if(node == null) throw new ArgumentNullException();
      if(shape != null)
      {
        shape.AssertValidAndUniqueNodes(node);
      }

      children.Insert(index, node);

      if(shape != null)
      {
        shape.OnNodeAdded(node);
      }
    }

    /// <summary>Removes a child node from this group.</summary>
    public void RemoveChild(int index)
    {
      if(shape != null)
      {
        shape.OnNodeRemoved(children[index]);
      }
      children.RemoveAt(index);
    }

    /// <summary>Removes a child node from this group. The node must exist.</summary>
    public void RemoveChild(Node node)
    {
      for(int i=0; i<children.Count; i++)
      {
        if(children[i] == node)
        {
          RemoveChild(i);
          return;
        }
      }
      
      throw new ArgumentException("The given node does not exist in this group.");
    }

    public override Rectangle GetBounds()
    {
      if(children.Count == 0)
      {
        return new Rectangle();
      }
      else
      {
        Rectangle rect = children[0].GetBounds();
        for(int i=1; i<children.Count; i++)
        {
          rect.Unite(children[i].GetBounds());
        }
        return rect;
      }
    }

    protected override void RenderContent(float screenSize)
    {
      for(int i=0; i<children.Count; i++)
      {
        children[i].Render(screenSize);
      }
    }

    /// <summary>A list of the group's child nodes.</summary>
    List<Node> children = new List<Node>(4);
  }
  #endregion

  #region PolygonNode
  /// <summary>Represents a node that renders a polygon.</summary>
  public sealed class PolygonNode : Node
  {
    public PolygonNode() { }
    public PolygonNode(string name) : base(name) { }

    public override ReadOnlyCollection<Node> Children
    {
      get { return new ReadOnlyCollection<Node>(EmptyNodeList); } // polygons are leaf nodes and never have children
    }
    
    /// <summary>Gets the base <see cref="Polygon"/> that will be rendered by this node.</summary>
    /// <remarks>The actual polygon rendered may be altered by a <see cref="PolygonReplacer"/> modifier.</remarks>
    public Polygon Polygon
    {
      get { return basePoly; }
    }

    public override Rectangle GetBounds()
    {
      return basePoly.GetBounds();
    }

    protected override void RenderContent(float screenSize)
    {
      if(tempPoly == null)
      {
        tempPoly = basePoly;
      }
      tempPoly.Render(screenSize);
      tempPoly = null;
    }

    /// <summary>The polygon that will be used during the next render.</summary>
    [NonSerialized] internal Polygon tempPoly;
    /// <summary>The polygon that will be rendered by default.</summary>
    Polygon basePoly = new Polygon();

    static readonly Node[] EmptyNodeList = new Node[0];
  }
  #endregion
  #endregion

  #region Polygon
  public sealed class Polygon : ISerializable
  {
    public Polygon()
    {
      LODLevels = 1;
    }

    Polygon(ISerializable dummy) { } // don't set LODLevels in the deserialization case (it'll be set later)

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
    /// <summary>Gets or sets the color of the polygon's outline.</summary>
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

    /// <summary>Gets or sets the width of the polygon's outline, in pixels.</summary>
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
    /// <summary>Gets or sets whether <see cref="TextureAspect"/> will be autogenerated based on the texture size.</summary>
    /// <remarks>The default is true.</remarks>
    [Category("Texture")]
    [Description("Determines whether the texture aspect ratio will be autogenerated based on the texture size.")]
    [DefaultValue(true)]
    public bool GenerateTextureAspect
    {
      get { return genTextureAspect; }
      set
      {
        if(value != genTextureAspect)
        {
          genTextureAspect = value;
          if(value)
          {
            OnTextureAspectGenerationChanged();
          }
        }
      }
    }

    /// <summary>Gets or sets whether the texture coordinates for vertices in this polygon
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
          if(value) // if generated coordinates are enabled, mark that they need to be regenerated.
          {
            InvalidateTextureCoords();
          }
        }
      }
    }

    /// <summary>Gets or sets the name of the texture.</summary>
    /// <remarks>The texture name is the name of an <see cref="ImageMap"/>. The texture name can optionally have a
    /// number appended, which determines the zero-based frame of the image map to use. The name and the number are
    /// separated with a hash mark. For example, "textureName#4", references the fifth frame of the "textureName" image
    /// map. In general, it's recommended that the image map named by this property have its
    /// <see cref="ImageMap.TextureWrap"/> property set to <see cref="TextureWrap.Repeat"/>.
    /// </remarks>
    [Category("Texture")]
    [Description("The name of the texture. The texture name should be the name of an image map, possibly with a "+
    "frame number appended. The frame number should be separated from the texture name with a hash mark. For "+
    "example, \"textureName#4\" references the fifth frame of the \"textureName\" image map.")]
    [DefaultValue(null)]
    public string Texture
    {
      get { return textureName; }
      set
      {
        if(!string.Equals(value, textureName, System.StringComparison.Ordinal))
        {
          textureName = value;

          if(string.IsNullOrEmpty(textureName))
          {
            mapHandle = null;
          }
          else
          {
            mapHandle = Engine.GetImageMap(textureName, out frameNumber);
            if(genTextureAspect)
            {
              OnTextureAspectGenerationChanged();
            }
          }
        }
      }
    }

    /// <summary>Gets or sets the texture aspect ratio, expressed as width divided by height.
    /// The value must be positive. If <see cref="GenerateTextureAspect"/> is true, attempts to set this property may
    /// be ignored.
    /// </summary>
    [Category("Texture")]
    [Description("The texture aspect ratio, expressed as width divided by height. The value must be positive. If "+
      "GenerateTextureAspect is true, attempts to set this property may be ignored.")]
    [DefaultValue(1.0)]
    public double TextureAspect
    {
      get { return textureAspect; }
      set
      {
        if(value != textureAspect)
        {
          EngineMath.AssertValidFloat(value);
          if(value <= 0) throw new ArgumentOutOfRangeException("TextureAspect", "Texture aspect must be positive.");
          textureAspect = value;
          InvalidateTextureCoords();
        }
      }
    }

    /// <summary>Gets or sets the amount by which the texture is shifted, in texture coordinates.</summary>
    /// <remarks>Texture offset is only used when autogenerating texture coordinates.</remarks>
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
          if(genTextureCoords) InvalidateTextureCoords();
        }
      }
    }

    /// <summary>Gets or sets the rotation of the texture, in degrees.</summary>
    /// <remarks>Texture rotation is only used when autogenerating texture coordinates.</remarks>
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
          if(genTextureCoords) InvalidateTextureCoords();
        }
      }
    }

    /// <summary>Gets or sets the texture repeat factor.</summary>
    /// <remarks>This affects how many times the texture will wrap within the local space of the polygon. Higher values
    /// will cause the texture to wrap more times. The value must be positive. The default is 1.
    /// </remarks>
    [Category("Texture")]
    [Description("The repeat factor of the texture. Higher values will cause the texture to repeat more times. "+
      " The value must be positive.")]
    [DefaultValue(1.0)]
    public double TextureRepeat
    {
      get { return textureRepeat; }
      set
      {
        if(value != textureRepeat)
        {
          EngineMath.AssertValidFloat(value);
          if(TextureRepeat <= 0)
          {
            throw new ArgumentOutOfRangeException("TextureRepeat", "Texture repeat must be positive.");
          }
          textureRepeat = value;
          if(genTextureCoords) InvalidateTextureCoords();
        }
      }
    }
    #endregion

    #region LOD & Spline
    /// <summary>Gets or sets a value that alters the curve of how LOD thresholds are distributed among LOD levels.
    /// A value of one uses the default curve, which will concentrate more LOD thresholds near the lower screen sizes.
    /// Higher values will even out or even reverse the curve, concentrating more LOD thresholds near the upper screen
    /// sizes, while lower values will exaggerate the existing curve, concentrating even more LOD thresholds near
    /// smaller screen sizes.
    /// </summary>
    [Category("LOD")]
    [DefaultValue(DefaultLODCurve)]
    [Description("Alters how LOD values are assigned to LOD levels. Higher values assign more LOD levels to larger "+
      "object sizes, and should be used when the object will normally be large on screen, while smaller values "+
      "assign more LOD levels to smaller object sizes and should be used when the object will normally be small and "+
      "will not be zoomed greatly. The default is 1.")]
    [Range(0.1, 4)]
    public float LODCurve
    {
      get { return lodCurve; }
      set
      {
        if(value != lodCurve)
        {
          if(value <= 0) throw new ArgumentOutOfRangeException("LODCurve", "Must be greater than zero.");
          lodCurve = value;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Gets or sets the number of LOD levels, which determines the number of steps from
    /// <see cref="MinimumLOD"/> to <see cref="MaximumLOD"/>. A value greater than one causes the LOD to change
    /// depending on the size of the shape on screen.
    /// </summary>
    [Category("LOD")]
    [Description("The number of LOD levels between MinimumLOD and MaximumLOD. A value greater than one will cause "+
      "the level of detail to change depending on the size of the shape on screen.")]
    [DefaultValue(1)]
    public int LODLevels
    {
      get { return lodLevels; }
      set
      {
        if(value != lodLevels)
        {
          if(value < 1) throw new ArgumentOutOfRangeException("LODLevels", "Must be greater than or equal to one.");
          lodLevels = value;
          lodData = new LODData[value];
          for(int i=0; i<lodData.Length; i++) lodData[i].TexCoordsDirty = true;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Gets or sets the maximum LOD (level of detail) threshold that determines how many of the points
    /// generated by the subdivision process will be discarded, as a value from 0 to 1. A value of 1 will discard no
    /// points, and lower values will discard more.
    /// </summary>
    /// <remarks>Using a value of one with spline shapes is strongly discouraged due to the tendency of the b-spline
    /// algorithm to generate many useless points. Increasing this value will decrease rendering performance.
    /// Internally, this value inversely represents how far the curve has to bend before a new point will be inserted.
    /// This value even affects shapes that have no spline edges.
    /// </remarks>
    /// <seealso cref="Subdivisions"/>
    [Category("LOD")]
    [Description("The Level Of Detail threshold that determines how many of the points generated by the "+
      "spline subdivision process will be discarded, as a value from 0 to 1. A value of one will discard no points, "+
      "lower values will discard more. Using a value of one with spline shapes is strongly discouraged due to the "+
      "tendency of the spline algorithm to generate many useless points if any of the vertices are split "+
      "(Split is true).")]
    [DefaultValue(DefaultLOD)]
    [Range(0, 1)]
    public float MaximumLOD
    {
      get { return maxLOD; }
      set
      {
        if(value != maxLOD)
        {
          EngineMath.AssertValidFloat(value);
          if(value < 0 || value > 1) throw new ArgumentOutOfRangeException("MaximumLOD", "Must be from 0 to 1.");
          maxLOD = value;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Gets or sets the minimum LOD (level of detail) threshold that determines how many of the points
    /// generated by the subdivision process will be discarded, as a value from 0 to 1. A value of 1 will discard no
    /// points, and lower values will discard more.
    /// </summary>
    /// <remarks>Using a value of one with spline shapes is strongly discouraged due to the tendency of the b-spline
    /// algorithm to generate many useless points. Increasing this value will decrease rendering performance.
    /// Internally, this value inversely represents how far the curve has to bend before a new point will be inserted.
    /// This value even affects shapes that have no spline edges.
    /// </remarks>
    /// <seealso cref="Subdivisions"/>
    [Category("LOD")]
    [Description("The Level Of Detail threshold that determines how many of the points generated by the "+
      "spline subdivision process will be discarded, as a value from 0 to 1. A value of one will discard no points, "+
      "lower values will discard more. Using a value of one with spline shapes is strongly discouraged due to the "+
      "tendency of the spline algorithm to generate many useless points if any of the vertices are split "+
      "(Split is true).")]
    [DefaultValue(DefaultLOD)]
    [Range(0, 1)]
    public float MinimumLOD
    {
      get { return minLOD; }
      set
      {
        if(value != minLOD)
        {
          EngineMath.AssertValidFloat(value);
          if(value < 0 || value > 1) throw new ArgumentOutOfRangeException("MinimumLOD", "Must be from 0 to 1.");
          minLOD = value;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Determines how large the object must be, as a fraction of the desktop size from 0 to 1, to
    /// receive the highest level of detail (specified by <see cref="MaximumLOD"/>).
    /// </summary>
    [Category("LOD")]
    [Description("Determines how large the object must be, as a fraction of the destop size from 0 to 1, to "+
      "receive the highest level of detail (specified by MaximumLOD).")]
    [DefaultValue(DefaultMaxLODSize)]
    [Range(0, 1)]
    public float MaximumLODSize
    {
      get { return maxLODSize; }
      set
      {
        if(value != maxLODSize)
        {
          EngineMath.AssertValidFloat(value);
          if(value < 0 || value > 1) throw new ArgumentOutOfRangeException("MaximumLODSize", "Must be from 0 to 1.");
          maxLODSize = value;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Determines how large the object must be, as a fraction of the desktop size from 0 to 1, to
    /// receive the highest level of detail (specified by <see cref="MinimumLOD"/>).
    /// </summary>
    [Category("LOD")]
    [Description("Determines how large the object must be, as a fraction of the destop size from 0 to 1, to "+
      "receive the highest level of detail (specified by MinimumLOD).")]
    [DefaultValue(DefaultMinLODSize)]
    [Range(0, 1)]
    public float MinimumLODSize
    {
      get { return minLODSize; }
      set
      {
        if(value != minLODSize)
        {
          EngineMath.AssertValidFloat(value);
          if(value < 0 || value > 1) throw new ArgumentOutOfRangeException("MinimumLODSize", "Must be from 0 to 1.");
          minLODSize = value;
          RecalculateLODThresholds();
        }
      }
    }

    /// <summary>Gets or sets the number of points into which each spline is subdivided.</summary>
    /// <remarks>Each spline curve is linearly subdivided into a number of points. These points are then passed through
    /// the LOD process to exclude the points that are unnecessary. Increasing the number of subdivision points will
    /// increase the amount of time it takes to subdivide the polygon, but subdivision is only performed when
    /// the shape of the polygon changes.
    /// </remarks>
    [Category("LOD")]
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
    Type ISerializable.TypeToSerialize
    {
      get { return GetType(); }
    }

    void ISerializable.BeforeSerialize(SerializationStore store) { }
    void ISerializable.BeforeDeserialize(DeserializationStore store) { }

    void ISerializable.Serialize(SerializationStore store)
    {
      store.AddValue("lodLevels", LODLevels);
    }

    void ISerializable.Deserialize(DeserializationStore store)
    {
      // the vertices' Polygon pointer was not saved, so reset them here
      foreach(Vertex vertex in vertices)
      {
        vertex.Polygon = this;
      }

      if(!string.IsNullOrEmpty(textureName))
      {
        mapHandle = Engine.GetImageMap(textureName, out frameNumber);
      }

      LODLevels = store.GetInt32("lodLevels");
    }
    #endregion

    /// <summary>Gets or sets the shade model for this polygon.</summary>
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

    /// <summary>Gets a read-only collection of the polygon's vertices.</summary>
    [Browsable(false)]
    public ReadOnlyCollection<Vertex> Vertices
    {
      get { return new ReadOnlyCollection<Vertex>(vertices); }
    }

    public Polygon Clone()
    {
      Polygon poly = new Polygon();
      poly.BlendingEnabled       = BlendingEnabled;
      poly.DestinationBlendMode  = DestinationBlendMode;
      poly.GenerateTextureCoords = GenerateTextureCoords;
      poly.LODLevels             = LODLevels;
      poly.MaximumLOD            = MaximumLOD;
      poly.MinimumLOD            = MinimumLOD;
      poly.Subdivisions          = Subdivisions;
      poly.ShadeModel            = ShadeModel;
      poly.SourceBlendMode       = SourceBlendMode;
      poly.StrokeColor           = StrokeColor;
      poly.StrokeWidth           = StrokeWidth;
      poly.Texture               = Texture;
      poly.TextureOffset         = TextureOffset;
      poly.TextureRotation       = TextureRotation;

      foreach(Vertex vertex in vertices)
      {
        poly.AddVertex(vertex.Clone());
      }

      return poly;
    }

    /// <summary>Creates a copy of this polygon with all edges passed through the subdivision and LOD process.</summary>
    /// <remarks>If this polygon contains multiple LOD levels, the highest level of detail is used.</remarks>
    public Polygon CloneAsPreSubdividedPolygon()
    {
      Polygon poly = Clone();
      poly.ClearVertices();

      int lodIndex = GetLODLevelForScreenSize(1);
      if(lodData[lodIndex].TessellationDirty) Tessellate(ref lodData[lodIndex]);
      if(lodData[lodIndex].TexCoordsDirty) GenerateTextureCoordinates(ref lodData[lodIndex]);

      SubPoint[] subPoints = lodData[lodIndex].SubPoints;
      for(int i=0,numSubPoints=lodData[lodIndex].NumSubPoints; i<numSubPoints; i++)
      {
        Vertex vertex = new Vertex();
        vertex.Color        = subPoints[i].Color;
        vertex.Position     = subPoints[i].Position;
        vertex.TextureCoord = subPoints[i].TextureCoord;
        vertex.Type         = VertexType.Split;
        poly.AddVertex(vertex);
      }

      return poly;
    }

    public Rectangle GetBounds()
    {
      if(vertices.Count == 0)
      {
        return new Rectangle();
      }
      else
      {
        double x1=double.MaxValue, y1=double.MaxValue, x2=double.MinValue, y2=double.MinValue;
        foreach(Vertex vertex in vertices)
        {
          if(vertex.Position.X < x1) x1 = vertex.Position.X;
          if(vertex.Position.X > x2) x2 = vertex.Position.X;
          if(vertex.Position.Y < y1) y1 = vertex.Position.Y;
          if(vertex.Position.Y > y2) y2 = vertex.Position.Y;
        }
        return new Rectangle(x1, y1, x2-x1, y2-y1);
      }
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

    /// <summary>Reverses the vertices in the polygon. This can be used to convert a clockwise-defined polygon to
    /// counterclockwise, or vice versa.
    /// </summary>
    public void ReverseVertices()
    {
      vertices.Reverse();
    }

    /// <summary>Renders the polygon.</summary>
    /// <param name="screenSize">A value from 0 to 1 that represents the size of the shape on screen, with one
    /// meaning that the shape takes up a very large portion of the screen and zero meaning that it takes up a very
    /// small portion of the screen. This is used to select the level of detail.
    /// </param>
    public void Render(float screenSize)
    {
      if(vertices.Count < 3) return; // if we don't have a valid polygon yet, return

      int lodIndex = GetLODLevelForScreenSize(screenSize);
      if(lodData[lodIndex].TessellationDirty) // if the polygon tessellation is outdated, recalculate it
      {
        Tessellate(ref lodData[lodIndex]);
      }
      if(lodData[lodIndex].TexCoordsDirty) // ditto for texture coordinates
      {
        GenerateTextureCoordinates(ref lodData[lodIndex]);
      }

      bool blendWasDisabled = false; // is blending currently disabled? (meaning we need to enable it?)
      int oldSourceBlend=0, oldDestBlend=0;

      if(blendEnabled) // first set up the blending parameters
      {
        blendWasDisabled = !GL.glIsEnabled(GL.GL_BLEND);
        if(blendWasDisabled) GL.glEnable(GL.GL_BLEND); // enable blending if it was disabled

        // set blend mode if necessary (pulling values from the parent object for Default)
        if(sourceBlend != SourceBlend.Default || destBlend != DestinationBlend.Default)
        {
          oldSourceBlend = GL.glGetIntegerv(GL.GL_BLEND_SRC);
          oldDestBlend   = GL.glGetIntegerv(GL.GL_BLEND_DST);
          GL.glBlendFunc(sourceBlend == SourceBlend.Default ? oldSourceBlend : (int)sourceBlend,
                         destBlend == DestinationBlend.Default ? oldDestBlend : (int)destBlend);
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

      // if we have an assigned image map, enable texturing
      ImageMap imageMap = mapHandle == null ? null : mapHandle.Resource;
      if(imageMap != null)
      {
        GL.glEnable(GL.GL_TEXTURE_2D);
        imageMap.BindFrame(frameNumber);
      }

      // if tessellation failed (possibly due to a self-intersecting polygon), just draw using GL_POLYGON.
      if(lodData[lodIndex].TessPrimitives.Count == 0)
      {
        GL.glBegin(GL.GL_POLYGON);
        for(int i=0; i<vertices.Count; i++)
        {
          // we don't texture because this is supposed to just be debug output.
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
        List<int> tessPrimitives = lodData[lodIndex].TessPrimitives;
        List<int[]> tessVertices = lodData[lodIndex].TessVertices;
        SubPoint[] subPoints = lodData[lodIndex].SubPoints;

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
            if(imageMap != null)
            {
              GL.glTexCoord2d(imageMap.GetTextureCoord(frameNumber, subPoints[vertexIndex].TextureCoord));
            }
            GL.glVertex2d(subPoints[vertexIndex].Position);
          }
          GL.glEnd();
        }
      }

      if(imageMap != null) // disable texturing if we enabled it
      {
        GL.glDisable(GL.GL_TEXTURE_2D);
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
        if(lodData[lodIndex].NumSubPoints == 0)
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
          SubPoint[] subPoints = lodData[lodIndex].SubPoints;
          for(int i=0,numSubPoints=lodData[lodIndex].NumSubPoints; i<numSubPoints; i++)
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
        if(sourceBlend != SourceBlend.Default || destBlend != DestinationBlend.Default)
        {
          GL.glBlendFunc(oldSourceBlend, oldDestBlend);   // restore the previous blending mode if we changed it
        }
        GL.glHint(GL.GL_LINE_SMOOTH_HINT, GL.GL_FASTEST);
      }
    }

    /// <summary>Called when the tessellated/subdivided geometry of the polygon needs to be recalculated. Changes to
    /// interpolated vertex properties (color and texture coordinate) also cause this to need to be recalculated.
    /// </summary>
    internal void InvalidateGeometry()
    {
      for(int i=0; i<lodData.Length; i++) lodData[i].TessellationDirty = true;
      // invalidating the geometry means the subpoints will be invalidated, including the stored texture coordinates
      InvalidateTextureCoords();
    }

    void InvalidateTextureCoords()
    {
      if(genTextureCoords)
      {
        for(int i=0; i<lodData.Length; i++) lodData[i].TexCoordsDirty = true;
      }
    }

    void OnTextureAspectGenerationChanged()
    {
      // if we can calculate the texture aspect now, we can avoid invalidating the coordinates unless necessary,
      // by using the TextureAspect setter. otherwise, if we can't calculate it now, we have no choice but to
      // invalidate the coordinates.
      if(mapHandle != null && mapHandle.Resource != null)
      {
        System.Drawing.Size size = mapHandle.Resource.Frames[frameNumber].Size;
        TextureAspect = (double)size.Width / size.Height;
      }
      else
      {
        InvalidateTextureCoords();
      }
    }

    void GenerateTextureCoordinates(ref LODData lod)
    {
      // we won't generate texture coordinates until we have a valid texture
      if(mapHandle == null || mapHandle.Resource == null) return;

      if(genTextureAspect) // recalculate texture aspect if we're supposed to do that
      {
        System.Drawing.Size size = mapHandle.Resource.Frames[frameNumber].Size;
        textureAspect = (double)size.Width / size.Height;
      }

      Point[] texCoords = new Point[lod.NumSubPoints]; // create a working buffer

      // first find the bounding rectangle of the polygon's vertices
      double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
      for(int i=0; i<texCoords.Length; i++)
      {
        Point pt = lod.SubPoints[i].Position;
        if(pt.X < x1) x1 = pt.X;
        if(pt.X > x2) x2 = pt.X;
        if(pt.Y < y1) y1 = pt.Y;
        if(pt.Y > y2) y2 = pt.Y;
        texCoords[i] = pt;
      }

      double xFactor = x1 + (x2-x1)*0.5, yFactor = y1 + (y2-y1)*0.5;
      // xFactor and yFactor are values that can be subtracted to reorient the points around the origin
      for(int i=0; i<texCoords.Length; i++)
      {
        texCoords[i].X -= xFactor;
        texCoords[i].Y -= yFactor;
      }

      // now apply rotation if necessary. it might seem intuitively incorrect to rotate the texture before scaling, but
      // we're actually rotating the texture coordinates. we're doing the inverse operations to the texture coordinates
      // rather than attempting to rotate the texture. then, in texture space, x is still x and y is still y, so the
      // rotation doesn't alter the aspect ratio.
      double rotation = -textureRotation * MathConst.DegreesToRadians;
      if(rotation != 0)
      {
        Math2D.Rotate(texCoords, 0, texCoords.Length, rotation);
      }

      // a value that can be multiplied to rescale the coordinates to -1/2 TextureRepeat to 1/2 TextureRepeat.
      xFactor = yFactor = textureRepeat / Math.Max(x2-x1, y2-y1);

      // now take this value and apply the aspect ratio to get the scaling factors for both dimensions. if the texture
      // is twice as wide as it is high, the x coordinates will stretch half as quickly to compensate
      if(textureAspect > 1) // texture is wider than it is high
      {
        xFactor /= textureAspect;
      }
      else if(textureAspect < 1) // texture is higher than it is wide
      {
        yFactor *= textureAspect;
      }

      // scale the texture coordinates by the scaling factors
      for(int i=0; i<texCoords.Length; i++)
      {
        texCoords[i].X *= xFactor;
        texCoords[i].Y *= yFactor;
      }

      // finally, assign the coordinates back, plus the texture offset, and an offset to get the coordinates back to
      // what would be expected for texture coordinates. (we were using -0.5repeat to 0.5repeat. this puts it back to
      // 0 to 1repeat)
      Vector offset = new Vector(textureRepeat*0.5, textureRepeat*0.5) - textureOffset.Rotated(rotation);
      for(int i=0; i<texCoords.Length; i++)
      {
        lod.SubPoints[i].TextureCoord = texCoords[i] + offset;
      }

      lod.TexCoordsDirty = false;
    }

    int GetLODLevelForScreenSize(float screenSize)
    {
      int lodIndex = lodLevels-1;
      if(lodLevels > 1) // if we have multiple LOD levels, select the appropriate one for the size of the shape
      {
        screenSize *= 0.95f; // most vector objects are a bit bigger than the shape
        for(int i=0; i<lodIndex; i++)
        {
          float thresh = (lodData[i+1].ScreenSize-lodData[i].ScreenSize)*0.5f + lodData[i].ScreenSize;
          if(screenSize < thresh) { lodIndex=i; break; }
        }
      }
      return lodIndex;
    }

    void RecalculateLODThresholds()
    {
      InvalidateGeometry();

      // there are three issues here:
      // 1. what is the expected range of screen sizes for the shape? (specified by MinimumLODSize and MaximumLODSize)
      // 2. within that range of screen sizes, what are the minimum and maximum LOD values? (MinimumLOD and MaximumLOD)
      // 3. how should LOD values be assigned within that range? (LODCurve)
      //
      // first, we'll split the range of LOD into N bands, where N == LODLevels. if there's only one band,
      // its value will be the average of MinimumLOD and MaximumLOD. otherwise, the two outer bands will receive the
      // values of MinimumLOD and MaximumLOD, and any intervening bands will receive values according to curve
      // specified by: y = x ^ (0.5*lodCurve), where x is interpolated between 0 and 1, and y is the LOD
      // threshold produced for that x value. the screen sizes associated with each of those bands will be produced by
      // an inverse curve: y = x ^ (2/lodCurve), where y is the screen size
      if(LODLevels == 1)
      {
        lodData[0].Threshold = (MinimumLOD + MaximumLOD) * 0.5f;
      }
      else
      {
        double value = 0, step = 1.0 / (LODLevels-1);
        double sizeFactor = (double)MaximumLODSize - MinimumLODSize;
        double sizeAdd = MinimumLODSize + (sizeFactor/LODLevels)*0.5, sizePower = 2.0/lodCurve;
        double lodFactor = (double)MaximumLOD - MinimumLOD, lodPower = 0.5*lodCurve;

        for(int i=0; i<LODLevels; value += step, i++)
        {
          lodData[i].ScreenSize = (float)(sizeAdd + sizeFactor*Math.Pow(value, sizePower));
          lodData[i].Threshold  = (float)(MinimumLOD + lodFactor*Math.Pow(value, lodPower));
        }
      }
    }

    #region Subdivision
    const int DefaultSubdivisions = 20;
    const float DefaultLOD = 0.6f; // corresponds to a threshold of 18 degrees (1/10th PI, 1/20th of a circle)
    const float DefaultMinLODSize = 0.1f, DefaultMaxLODSize = 0.5f, DefaultLODCurve = 1;

    struct SubPoint
    {
      public Point Position, TextureCoord;
      public Color Color;
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
    void SubdividePolygon(ref LODData lod)
    {
      lod.ResetSubPoints(); // reset our array of subdivision points, and clear the LOD state

      // start by checking if it's a fully-joined shape (closed spline shape -- no split/forced vertices)
      int firstBreak = -1;
      for(int i=0; i<vertices.Count; i++)
      {
        if(vertices[i].Type != VertexType.Normal)
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
          SubdivideSegment(ref lod, i0, i1, i2, i3, false); // and subdivide this 
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
          } while(vertices[lastEdge].Type == VertexType.Normal);

          // 'edgeEnd' points to the next broken vertex. the spline goes from 'breakPoint' to 'edgeEnd', inclusive.
          int splineLength = breakPoint<lastEdge ? lastEdge-breakPoint : vertices.Count-breakPoint+lastEdge; // # of edges in spline

          // if there's only one edge involved, there can be no curve, so just add this vertex as-is
          if(splineLength == 1)
          {
            SubPoint subPoint = new SubPoint();
            subPoint.Color        = vertices[breakPoint].Color;
            subPoint.Position     = vertices[breakPoint].Position;
            subPoint.TextureCoord = vertices[breakPoint].TextureCoord;
            lod.AddSubPoint(ref subPoint, vertices[breakPoint].Type == VertexType.Forced);
          }
          else // otherwise, there are multiple edges involved in this segment, so use a clamped b-spline
          {
            // to create a clamped b-spline, we pretend that there are three of each first and last control point
            for(int i=-2; i < splineLength; i++)
            {
              int i0 = GetClampedIndex(i, breakPoint, lastEdge),   i1 = GetClampedIndex(i+1, breakPoint, lastEdge),
                  i2 = GetClampedIndex(i+2, breakPoint, lastEdge), i3 = GetClampedIndex(i+3, breakPoint, lastEdge);
              SubdivideSegment(ref lod, i0, i1, i2, i3, i == -2 && vertices[breakPoint].Type == VertexType.Forced);
            }
          }

          breakPoint = lastEdge; // advance 'breakPoint' to the next broken point to continue the loop
        } while(examined < vertices.Count);
      }

      lod.FlushSubPoints(); // finally, notify the LOD system that we are done so it can add any last points.
    }

    /// <summary>Given the indices of four control points, subdivides a b-spline.</summary>
    void SubdivideSegment(ref LODData lod, int i0, int i1, int i2, int i3, bool forceFirstPoint)
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

        // if we're not autogenerating texture coordinates, we'll interpolate the user-supplied ones
        if(!genTextureCoords)
        {
          subPoint.TextureCoord.X = b0*vertices[i0].TextureCoord.X + b1*vertices[i1].TextureCoord.X +
                                    b2*vertices[i2].TextureCoord.X + b3*vertices[i3].TextureCoord.X;
          subPoint.TextureCoord.Y = b0*vertices[i0].TextureCoord.Y + b1*vertices[i1].TextureCoord.Y +
                                    b2*vertices[i2].TextureCoord.Y + b3*vertices[i3].TextureCoord.Y;
        }

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

        lod.AddSubPoint(ref subPoint, forceFirstPoint && i == 0); // attempt to add the new point to the shape
      }
    }
    #endregion

    #region Tessellation
    void Tessellate(ref LODData lod)
    {
      SubdividePolygon(ref lod); // subdivide the shape into points
      lod.Tessellate();
    }
    #endregion

    #region LODData
    /// <summary>Contains the data for a single LOD of this shape.</summary>
    struct LODData
    {
      /// <summary>Cached tessellation info containing the list of primitives needed to render the polygon.</summary>
      public List<int> TessPrimitives;
      /// <summary>Cached tessellation info containing the list of vertex indices.</summary>
      public List<int[]> TessVertices;
      /// <summary>Cached subdivision info containing the subdivision points.</summary>
      public SubPoint[] SubPoints;
      /// <summary>The number of points in <see cref="subPoints"/>.</summary>
      public int NumSubPoints;
      /// <summary>The LOD threshold, from 0 to 1.</summary>
      public float Threshold;
      /// <summary>The screen size that maps to this LOD threshold.</summary>
      public float ScreenSize;
      /// <summary>Determines whether the cached tessellation or subdivision information needs to be recalculated.</summary>
      public bool TessellationDirty, TexCoordsDirty;

      /// <summary>Takes a <see cref="SubPoint"/> and adds to the subdivision array if it is necessary for the given LOD.</summary>
      public void AddSubPoint(ref SubPoint subPoint, bool forced)
      {
        // the LOD algorithm does not a have enough information until at least two points have been considered, so we'll
        // keep track of the number of points considered.
        SubState.PointsConsidered++;

        // the algorithm works by keeping track of 2 points, a Base point, which is the last point added, and the
        // point considered most recently. the angle change of each point is calculated based on the difference from
        // the previous angle. the changes are summed, and when the total angular deviation passes a certain threshold,
        // the point /before/ the one causing the deviation to pass the threshold is added, and the deviation reset to
        // zero.
        // the very first point considered is also stored so that AddSubPoint() can simply be called one last time
        // with the original point if the shape is a closed shape.

        if(SubState.PointsConsidered >= 3) // if this is the third or subsequent point, we can apply the LOD algorithm
        {
          // get the angle between the new point and the previous point
          double newAngle = Math2D.AngleBetween(SubState.PrevPoint.Position, subPoint.Position);
          // get the difference between that angle and the last angle
          double delta = newAngle - SubState.PrevAngle;

          // if the delta's magnitude is greater than 180 degrees (pi), it's better to consider it as a smaller change
          // in the opposite direction. for instance, a 350 degree change is considered to be a -10 degree change.
          // this makes sure that -10 and 350 are treated as identical deltas.
          if(delta > Math.PI) delta -= Math.PI*2;
          else if(delta < -Math.PI) delta += Math.PI*2;

          SubState.Deviation += delta; // accumulate the delta into the total angular deviation
          double threshold = (Math.PI/4) * (1-Threshold); // the value from 0 to 1 maps to pi/4 to 0 radians
          if(SubState.PrevForced || Math.Abs(SubState.Deviation) >= threshold) // if the deviation reaches the LOD
          {                                                                    // threshold or the point was forced
            AddSubPointToArray(ref SubState.PrevPoint); // add the previous point to the array
            SubState.BasePoint = SubState.PrevPoint;    // update the BasePoint
            SubState.Deviation = 0; // and reset the deviation to zero. we don't try to leave the "extra" deviation in
          }                         // there because it may be very large, and what should we do in that case?

          SubState.PrevPoint  = subPoint; // in all cases, update the previous point,
          SubState.PrevAngle  = newAngle; // the previous angle,
          SubState.PrevForced = forced;   // and the previous forced state
        }
        else if(SubState.PointsConsidered == 2)
        {
          // if this is the second point being considered, store it into PrevPoint and calculate the angle between it
          // and the base point. then set the deviation to zero.
          SubState.PrevPoint  = subPoint;
          SubState.PrevAngle  = Math2D.AngleBetween(SubState.BasePoint.Position, SubState.PrevPoint.Position);
          SubState.PrevForced = forced;
          SubState.Deviation  = 0;
        }
        else
        {
          // if this is the first point considered, store it in both FirstPoint and BasePoint.
          SubState.FirstPoint = SubState.BasePoint = subPoint;
          AddSubPointToArray(ref subPoint); // and always add the first point to the array
        }
      }

      /// <summary>Unconditionally adds a <see cref="SubPoint"/> to the list of subdivision points.</summary>
      void AddSubPointToArray(ref SubPoint subPoint)
      {
        if(SubPoints == null || SubPoints.Length == NumSubPoints) // (re)allocate the array if necessary
        {
          SubPoint[] newPoints = new SubPoint[NumSubPoints == 0 ? 8 : NumSubPoints*2];
          if(NumSubPoints != 0)
          {
            Array.Copy(SubPoints, newPoints, NumSubPoints);
          }
          SubPoints = newPoints;
        }

        SubPoints[NumSubPoints++] = subPoint; // add the point and increment the point count
      }

      /// <summary>Completes the subdivision process.</summary>
      public void FlushSubPoints()
      {
        // since the shape is always closed, call AddSubPoint() one last time with the original point
        AddSubPoint(ref SubState.FirstPoint, false);
      }

      /// <summary>Resets the subdivision process state.</summary>
      public void ResetSubPoints()
      {
        NumSubPoints = 0;
        SubState = new SubdivisionState();
      }

      public void Tessellate()
      {
        // reset our collections
        if(TessPrimitives == null) TessPrimitives = new List<int>(4);
        if(TessVertices == null) TessVertices = new List<int[]>();
        TessPrimitives.Clear();
        TessVertices.Clear();

        // if subdivision resulted in less than 3 points, we can't tessellate.
        // this can happen if the LOD value is set too high.
        if(NumSubPoints < 3) return;

        // the tessellation process will create OpenGL primitives composed of vertices (the vertices will be stored as
        // indices into the list of subdivision points).
        List<int> currentVertices = new List<int>();  // the vertices for the current OpenGL primitive
        int currentPrimitive = 0;                     // the current OpenGL primitive
        bool errorOccurred = false;                   // whether an error occurred during the tessellation process

        IntPtr tess = GLU.gluNewTess(); // create a new OpenGL tessellator
        if(tess == IntPtr.Zero) throw new OutOfMemoryException();

        try
        {
          List<int> tessPrimitives = TessPrimitives; // copy these to locals so we can access them from the delegates
          List<int[]> tessVertices = TessVertices;
          // set up the callbacks for the tessellator.
          // we'll put the delegates in variables to prevent them from being freed by the garbage collector
          GLU.GLUtessBeginProc beginProc = delegate(int type) { currentPrimitive = type; };
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
          GLU.GLUtessErrorProc errorProc = delegate(int error) { errorOccurred = true; };

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
            double* vertexData = stackalloc double[NumSubPoints*3];
            for(int i=0, j=0; i<NumSubPoints; i++, j+=3) // for each vertex
            {
              vertexData[j]   = SubPoints[i].Position.X; // add it to the vertex array
              vertexData[j+1] = SubPoints[i].Position.Y;
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
          TessPrimitives.Clear();
          TessVertices.Clear();
        }

        // finally, mark that we have finished tessellation. this should be set even if an error occurred, because we
        // don't want to repeatedly try to tessellate bad data.
        TessellationDirty = false;
      }

      struct SubdivisionState
      {
        public SubPoint FirstPoint;
        public SubPoint BasePoint, PrevPoint;
        public double PrevAngle, Deviation;
        public int PointsConsidered;
        public bool PrevForced;
      }

      /// <summary>The state of the current subdivision process.</summary>
      SubdivisionState SubState;
    }
    #endregion

    /// <summary>The vertices in this polygon.</summary>
    List<Vertex> vertices = new List<Vertex>(4);
    /// <summary>The name of the selected texture.</summary>
    string textureName;
    /// <summary>An array containing the LOD data for this shape.</summary>
    [NonSerialized] LODData[] lodData;
    /// <summary>The handle to the selected texture.</summary>
    [NonSerialized] ResourceHandle<ImageMap> mapHandle;
    /// <summary>The selected texture frame.</summary>
    [NonSerialized] int frameNumber;
    /// <summary>The offset within the texture.</summary>
    Vector textureOffset;
    /// <summary>The texture aspect ratio, as width / height.</summary>
    double textureAspect = 1;
    /// <summary>The texture rotation, in degrees.</summary>
    double textureRotation;
    /// <summary>The texture repeat factor.</summary>
    double textureRepeat = 1;
    /// <summary>The color of the polygon's stroke line.</summary>
    Color strokeColor = Color.Black;
    /// <summary>The width of the polygon's stroke line.</summary>
    float strokeWidth;
    /// <summary>The level of detail, from 0 to 1.</summary>
    float maxLOD = DefaultLOD, minLOD = DefaultLOD;
    /// <summary>The size the object must be as a fraction of desktop size to receive the minimum or maximum
    /// level of detail.
    /// </summary>
    float maxLODSize = DefaultMaxLODSize, minLODSize = DefaultMinLODSize;
    /// <summary>A value used during the dynamic LOD calculation process to adjust the curve of LOD values.</summary>
    float lodCurve = 1;
    /// <summary>The number of subdivisions of each spline curve.</summary>
    int subdivisions = DefaultSubdivisions;
    /// <summary>The number of LOD levels between maxLOD and minLOD.</summary>
    [NonSerialized] int lodLevels;
    /// <summary>The type of GL source blending to use.</summary>
    SourceBlend sourceBlend = SourceBlend.Default;
    /// <summary>The type of GL destination blending to use.</summary>
    DestinationBlend destBlend = DestinationBlend.Default;
    /// <summary>The type of shading to use.</summary>
    ShadeModel shadeModel = ShadeModel.Flat;
    /// <summary>Indicates whether blending is explicitly enabled for this polygon.</summary>
    /// <remarks>Note that even if this is false, blending will be enabled if the parent object is blended.</remarks>
    bool blendEnabled;
    /// <summary>Determines whether texture coordinates and aspect ratio will be autogenerated.</summary>
    bool genTextureCoords = true, genTextureAspect = true;
  }
  #endregion

  public enum VertexType : byte { Normal, Split, Forced }

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

    /// <summary>Gets or sets the vertex type, including whether it's split or always included in the shape.</summary>
    [Description("The type of the vertex, including whether it's splits the spline curve, or is always included "+
      "regardless of LOD settings.")]
    public VertexType Type
    {
      get { return type; }
      set
      {
        if(value != type)
        {
          type = value;
          InvalidateGeometry();
        }
      }
    }

    /// <summary>Gets or sets the vertex's texture coordinates.</summary>
    [Description("The vertex's texture coordinates.")]
    public Point TextureCoord
    {
      get { return textureCoord; }
      set
      {
        EngineMath.AssertValidFloats(value.X, value.Y);
        textureCoord = value;
      }
    }

    public Vertex Clone()
    {
      Vertex vertex = new Vertex();
      vertex.color        = color;
      vertex.position     = position;
      vertex.textureCoord = textureCoord;
      vertex.type         = type;
      return vertex;
    }

    [NonSerialized] internal Polygon Polygon;

    /// <summary>Called when a change was made to the vertex that would invalidate the cached geometric information
    /// held by the owning polygon.
    /// </summary>
    void InvalidateGeometry()
    {
      if(Polygon != null) Polygon.InvalidateGeometry();
    }

    /// <summary>This vertex's position within the polygon.</summary>
    Point position;
    /// <summary>
    /// This vertex's texture coordinate. It will only be used if a texture is specified in the parent polygon.
    /// </summary>
    Point textureCoord;
    /// <summary>The vertex's color.</summary>
    Color color;
    VertexType type;
  }
  #endregion

  #region Node management
  /// <summary>Gets or sets the root node in the geometry of this vector shape.</summary>
  public Node RootNode
  {
    get { return rootNode; }
    set
    {
      if(value != rootNode)
      {
        if(value != null)
        {
          AssertValidNodeTree(value);
        }

        if(rootNode != null)
        {
          OnNodeRemoved(rootNode);
        }

        rootNode = value;

        if(value != null)
        {
          OnNodeAdded(value);
        }
      }
    }
  }

  /// <summary>Gets the node with the given name.</summary>
  public Node GetNode(string nodeName)
  {
    return nodeMap[nodeName];
  }

  /// <summary>Returns the <see cref="GroupNode"/> that contains the given node, or null if the node is not contained
  /// within any group, meaning that it must be the root node. The node must exist in the tree.
  /// </summary>
  public GroupNode GetParentNode(Node node)
  {
    if(node == null) throw new ArgumentNullException();
    if(node == rootNode) return null;
    
    GroupNode group = GetParentNode(node, rootNode as GroupNode);
    if(group != null) return group;
    throw new ArgumentException("The given node does not exist in this tree.");
  }

  /// <summary>Removes a node from the tree. The node must exist in the tree.</summary>
  public void RemoveNode(string nodeName)
  {
    RemoveNode(GetNode(nodeName));
  }

  /// <summary>Removes a node from the tree. The node must exist in the tree.</summary>
  public void RemoveNode(Node node)
  {
    GroupNode parent = GetParentNode(node);
    if(parent != null)
    {
      parent.RemoveChild(node);
    }
    else
    {
      RootNode = null;
    }
  }

  /// <summary>
  /// Alters the names of the nodes in the given tree so that they don't conflict with any nodes in this shape's tree.
  /// </summary>
  public void UniquifyNames(Node tree)
  {
    Dictionary<string,Node> map = new Dictionary<string,Node>(nodeMap);
    foreach(Node node in EnumerateNodes(tree))
    {
      string name = node.Name;
      int  suffix = 2;

      while(map.ContainsKey(name))
      {
        name = node.Name + suffix++;
      }

      map.Add(name, node);
      node.Name = name;
    }
  }

  /// <summary>Returns an object that can enumerate all nodes in the given tree.</summary>
  public static IEnumerable<Node> EnumerateNodes(Node tree)
  {
    if(tree == null) yield break;

    yield return tree;

    foreach(Node child in tree.Children)
    {
      foreach(Node node in EnumerateNodes(child))
      {
        yield return node;
      }
    }
  }

  static GroupNode GetParentNode(Node node, GroupNode group)
  {
    if(group == null) return null;

    foreach(Node child in group.Children)
    {
      if(node == child) return group;

      GroupNode childGroup = child as GroupNode;
      if(childGroup != null)
      {
        GroupNode result = GetParentNode(node, childGroup);
        if(result != null) return result;
      }
    }
    
    return null;
  }
  #endregion

  #region Animation management
  /// <summary>Gets a read-only collection of the animations in this shape.</summary>
  public ICollection<Animation> Animations
  {
    get
    {
      return anims == null ? (ICollection<Animation>)new ReadOnlyCollection<Animation>(EmptyAnimationList)
                           : anims.Values;
    }
  }

  /// <summary>Adds a new animation to the vector shape. The animation must have a unique name.</summary>
  public void AddAnimation(Animation animation)
  {
    if(animation == null) throw new ArgumentNullException();
    if(animation.Shape != null) throw new ArgumentException("Animation already belongs to a shape.");

    if(anims == null)
    {
      anims = new Dictionary<string,Animation>(4);
    }
    else
    {
      AssertValidAndUniqueAnimationName(animation.Name);
    }

    anims.Add(animation.Name, animation);
    animation.Shape = this;
  }

  /// <summary>Gets an animation given its name. The animation must exist or an exception will occur.</summary>
  public Animation GetAnimation(string animationName)
  {
    return anims[animationName];
  }

  /// <summary>Removes an animation, given its name.</summary>
  public void RemoveAnimation(string animationName)
  {
    if(anims != null)
    {
      Animation anim;
      if(anims.TryGetValue(animationName, out anim))
      {
        anim.Shape = null;
        anims.Remove(animationName);
        if(anims.Count == 0) // free the list if the count drops to zero
        {
          anims = null;
        }
      }
    }
  }

  /// <summary>Attempts to retrieve an animation given its name.</summary>
  public bool TryGetAnimation(string animationName, out Animation animation)
  {
    if(anims == null)
    {
      animation = null;
      return false;
    }
    else
    {
      return anims.TryGetValue(animationName, out animation);
    }
  }
  #endregion

  protected override void Deserialize(DeserializationStore store)
  {
    base.Deserialize(store);
    
    if(rootNode != null) // restore the node map and the nodes' shape pointers, neither of which are serialized
    {
      OnNodeAdded(rootNode);
    }
    
    if(anims != null)
    {
      foreach(Animation anim in anims.Values)
      {
        anim.Shape = this;     // restore the animations' Shape pointer, which was not serialized
        anim.OnDeserialized(); // and invoke OnDeserialized(), so the animations can restore necessary pointers
      }
    }
  }

  /// <summary>Renders this shape. Assumes all desired modifiers have been applied to the nodes.</summary>
  /// <param name="screenSize">A value from 0 to 1 that represents the size of the shape on screen.</param>
  internal void Render(float screenSize)
  {
    if(rootNode != null)
    {
      rootNode.Render(screenSize);
    }
  }

  internal void AssertValidAndUniqueAnimationName(string animationName)
  {
    if(anims.ContainsKey(animationName)) throw new ArgumentException("Animation name must be unique within a shape.");
  }

  internal void AssertValidAndUniqueNodeName(string name)
  {
    AssertValidAndUniqueNodeName(name, nodeMap);
  }
  
  internal void AssertValidAndUniqueNodes(Node node)
  {
    AssertValidAndUniqueNodes(node, nodeMap);
  }

  internal void AssertValidNodeName(string name)
  {
    if(name == null || !nodeMap.ContainsKey(name)) throw new ArgumentException("Node name '"+name+"' does not exist.");
  }

  internal void AssertValidNodeTree(Node node)
  {
    AssertValidAndUniqueNodes(node, new Dictionary<string,Node>());
  }

  internal void OnNodeAdded(Node node)
  {
    node.shape = this; // update the node's shape pointer
    nodeMap.Add(node.Name, node); // add it to the node map
    
    foreach(Node child in node.Children) // and do the same with its descendants
    {
      OnNodeAdded(child);
    }
  }
  
  internal void OnNodeRemoved(Node node)
  {
    node.shape = null; // clear the node's shape pointer
    nodeMap.Remove(node.Name); // remove it from the node map
    
    foreach(Node child in node.Children) // and do the same with its descendants
    {
      OnNodeRemoved(child);
    }
  }
  
  internal void OnNodeRenamed(string oldName, string newName)
  {
    Node node = nodeMap[oldName];
    nodeMap.Remove(oldName);
    nodeMap.Add(newName, node);
  }

  /// <summary>A possibly-null dictionary containing the animations within this shape.</summary>
  Dictionary<string,Animation> anims;
  /// <summary>A dictionary node names to node objects.</summary>
  [NonSerialized] Dictionary<string,Node> nodeMap = new Dictionary<string,Node>();
  /// <summary>The root node of the vector shape hierarchy.</summary>
  Node rootNode;

  static void AssertValidAndUniqueNodeName(string name, Dictionary<string,Node> nodeMap)
  {
    if(string.IsNullOrEmpty(name)) throw new ArgumentException("Node name cannot be empty.");
    if(nodeMap.ContainsKey(name)) throw new ArgumentException("Node name '"+name+"' must be unique within a shape.");
  }

  static void AssertValidAndUniqueNodes(Node node, Dictionary<string,Node> nodeMap)
  {
    AssertValidAndUniqueNodeName(node.Name, nodeMap);
    if(node.shape != null) throw new ArgumentException("Node already belongs to a shape.");

    foreach(Node child in node.Children)
    {
      AssertValidAndUniqueNodes(child, nodeMap);
    }
  }

  static readonly Animation[] EmptyAnimationList = new Animation[0];
}
#endregion

#region VectorAnimationData
/// <summary>Represents the animation state of a vector animation.</summary>
public struct VectorAnimationData
{
  public VectorAnimationData(string animationName)
  {
    AnimationName = animationName;
    Completed     = null;
    Animation     = null;
    AnimationData = new AnimationData();
  }

  /// <summary>Raised whenever the animation completes, including when it loops.</summary>
  public event VectorAnimationEventHandler Completed;

  /// <summary>The animation's name.</summary>
  public string AnimationName;
  /// <summary>The <see cref="AnimationData"/> containing the position within the animation.</summary>
  public AnimationData AnimationData;
  
  /// <summary>A pointer to the actual animation object.</summary>
  [NonSerialized] internal VectorShape.Animation Animation;
}
#endregion

#region VectorObject
/// <summary>A scene object that renders a <see cref="VectorShape"/>, possibly with one more animations applied.</summary>
/// <remarks>Multiple animations can be applied to a vector object, to modify the vector shape. The order in which
/// these animations are applied may matter, so the order can be specified if necessary. A given animation can only be
/// added to the object once, however. Adding an animation that already exists on the object will replace that
/// animation.
/// </remarks>
public class VectorObject : SceneObject
{
  [Browsable(false)]
  public override bool AutoLOD
  {
    get { return true; }
  }

  /// <summary>Gets the <see cref="VectorShape"/> referenced by <see cref="ShapeName"/>.</summary>
  [Browsable(false)]
  public VectorShape Shape
  {
    get { return shapeHandle == null ? null : shapeHandle.Resource; }
  }

  /// <summary>Gets or sets the name of the animation displayed in this animated object.</summary>
  [Category("Shape")]
  [Description("The name of the shape resource displayed in this vector object.")]
  [DefaultValue(null)]
  public string ShapeName
  {
    get { return shapeName; }
    set
    {
      if(!string.Equals(value, shapeName, System.StringComparison.Ordinal))
      {
        shapeName = value;

        if(string.IsNullOrEmpty(shapeName))
        {
          shapeHandle = null;
        }
        else
        {
          shapeHandle = Engine.GetResource<VectorShape>(shapeName);
        }
        
        if(appliedAnims != null && Shape != null)
        {
          for(int i=0; i<appliedAnims.Count; i++)
          {
            VectorAnimationData data = appliedAnims[i];
            if(!Shape.TryGetAnimation(data.AnimationName, out data.Animation))
            {
              appliedAnims.Clear();
              throw new ArgumentException("No such animation in shape: "+data.AnimationName);
            }
            appliedAnims[i] = data;
          }
        }
      }
    }
  }

  #region Applied animations
  /// <summary>Gets a read-only collection of the animations currently applied to this object.</summary>
  public ReadOnlyCollection<VectorAnimationData> Animations
  {
    get
    {
      return new ReadOnlyCollection<VectorAnimationData>(
                   appliedAnims == null ? (IList<VectorAnimationData>)EmptyAnimationList : appliedAnims);
    }
  }

  /// <summary>Adds an animation to the object, given its name. The animation will play from the beginning.</summary>
  /// <remarks>If the named animation is already playing, it will be removed first.</remarks>
  public void AddAnimation(string animationName)
  {
    InsertAnimation(appliedAnims == null ? 0 : appliedAnims.Count, animationName);
  }

  /// <summary>Adds an animation to the object, given a <see cref="VectorAnimationData"/> object.</summary>
  /// <remarks>If the named animation is already playing, it will be removed first.</remarks>
  public void AddAnimation(VectorAnimationData data)
  {
    InsertAnimation(appliedAnims == null ? 0 : appliedAnims.Count, data);
  }

  /// <summary>Removes all animations from the object.</summary>
  public void ClearAnimations()
  {
    if(appliedAnims != null) appliedAnims.Clear();
  }

  /// <summary>Inserts an animation, given its name.</summary>
  /// <remarks>If the named animation is already playing, it will be removed first.</remarks>
  public void InsertAnimation(int index, string animationName)
  {
    InsertAnimation(index, new VectorAnimationData(animationName));
  }

  /// <summary>Inserts an animation to the object, given a <see cref="VectorAnimationData"/> object.</summary>
  /// <remarks>If the named animation is already playing, it will be removed first.</remarks>
  public void InsertAnimation(int index, VectorAnimationData data)
  {
    if(Shape == null)
    {
      if(string.IsNullOrEmpty(data.AnimationName)) throw new ArgumentException("Animation name cannot be empty.");
      data.Animation = null;
    }
    else
    {
      if(!Shape.TryGetAnimation(data.AnimationName, out data.Animation))
      {
        throw new ArgumentException("No such animation: "+data.AnimationName);
      }
    }
    
    if(appliedAnims == null)
    {
      appliedAnims = new List<VectorAnimationData>(4);
    }
    else
    {
      // first remove the existing animation if it already exists.
      for(int i=0; i<appliedAnims.Count; i++)
      {
        if(string.Equals(data.AnimationName, appliedAnims[i].AnimationName, StringComparison.Ordinal))
        {
          // but if the user wants to insert the animation into the same slot as where it currently exists, we can
          // simply replace the value in the array and return.
          if(i == index)
          {
            appliedAnims[i] = data;
            return;
          }
          else
          {
            appliedAnims.RemoveAt(i);
            break;
          }
        }
      }
    }

    appliedAnims.Insert(index, data);
  }

  /// <summary>Removes a playing animation, given its name.</summary>
  public void RemoveAnimation(string animationName)
  {
    if(appliedAnims != null)
    {
      for(int i=0; i<appliedAnims.Count; i++)
      {
        if(string.Equals(animationName, appliedAnims[i].AnimationName, StringComparison.Ordinal))
        {
          appliedAnims.RemoveAt(i);
          break;
        }
      }
    }
  }
  #endregion

  #region Serialization and Deserialization
  protected override void Deserialize(DeserializationStore store)
  {
    base.Deserialize(store);

    if(!string.IsNullOrEmpty(shapeName)) // reload the shape handle if we had one before
    {
      shapeHandle = Engine.GetResource<VectorShape>(shapeName);
    }
  }
  #endregion

  protected override void RenderContent(float screenSize)
  {
    if(Shape == null)
    {
      base.RenderContent(screenSize); // use default rendering if there's no shape set
    }
    else
    {
      // if animations are being applied, invoke them to temporarily modify the nodes in the shape
      if(appliedAnims != null)
      {
        for(int i=0; i<appliedAnims.Count; i++)
        {
          VectorAnimationData data = appliedAnims[i];
          if(data.Animation == null)
          {
            data.Animation = Shape.GetAnimation(data.AnimationName);
          }
          data.Animation.ApplyModifiers(ref data.AnimationData);
        }
      }

      Shape.Render(screenSize); // then render the shape
    }
  }

  protected internal override void Simulate(double timeDelta)
  {
    base.Simulate(timeDelta);

    // if animations and a shape are applied, update the animation info
    if(appliedAnims != null && Shape != null)
    {
      for(int i=0; i<appliedAnims.Count; i++)
      {
        VectorAnimationData data = appliedAnims[i];
        if(data.Animation == null)
        {
          data.Animation = Shape.GetAnimation(data.AnimationName);
        }
        data.Animation.Simulate(timeDelta, ref data);
        appliedAnims[i] = data;
      }
    }
  }

  /// <summary>The object's current shape.</summary>
  [NonSerialized] ResourceHandle<VectorShape> shapeHandle;
  /// <summary>The name of the object's shape.</summary>
  string shapeName;
  /// <summary>An list of the currently-applied animations.</summary>
  List<VectorAnimationData> appliedAnims;
  
  static readonly VectorAnimationData[] EmptyAnimationList = new VectorAnimationData[0];
}
#endregion

} // namespace RotationalForce.Engine