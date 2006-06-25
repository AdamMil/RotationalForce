using System;
using System.Collections.Generic;
using System.ComponentModel;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
using Color = System.Drawing.Color;

namespace RotationalForce.Engine
{

public delegate void TriggerEventHandler(TriggerObject trigger, SceneObject obj);

public class TriggerObject : SceneObject
{
  public TriggerObject()
  {
    Immobile = true;
    Visible = false;

    // draw the trigger (in an editor, say) as a reddish, translucent area
    BlendingEnabled = true;
    BlendColor      = Color.FromArgb(128, 255, 32, 32);
    SetBlendingMode(SourceBlend.SrcAlpha, DestinationBlend.OneMinusSrcAlpha);

    // a trigger receives collisions, but does not respond to them
    CollisionEnabled   = true;
    ReceivesCollisions = true;
    CollisionResponse  = CollisionResponse.None;
  }

  public event TriggerEventHandler ObjectEnter;
  public event TriggerEventHandler ObjectLeave;

  protected override void OnHitBy(SceneObject hitter) // this should only be called once per object per frame
  {
    base.OnHitBy(hitter);

    if(hitThisFrame == null) hitThisFrame = new List<SceneObject>(2);
    hitThisFrame.Add(hitter);
  }

  protected internal override void PostSimulate()
  {
 	  base.PostSimulate();

 	  // if we want ObjectEnter notification and some objects may have been hit this frame
 	  if(ObjectEnter != null && hitThisFrame != null)
 	  {
 	    for(int i=0; i<hitThisFrame.Count; i++) // for each object hit this frame
 	    {
 	      // if it wasn't also hit last frame (meaning it entered this frame), raise the notification
 	      if(hitLastFrame == null || !hitLastFrame.Contains(hitThisFrame[i]))
 	      {
 	        ObjectEnter(this, hitThisFrame[i]);
 	      }
 	    }
 	  }
 	  
 	  // if we want ObjectLeave notification and some objects may have been last frame
 	  if(ObjectLeave != null && hitLastFrame != null)
 	  {
 	    for(int i=0; i<hitLastFrame.Count; i++) // for each object hit last frame
 	    {
 	      // it if wan't also hit this frame (meaning it left this frame), raise the notification
 	      if(!hitThisFrame.Contains(hitLastFrame[i]))
 	      {
 	        ObjectLeave(this, hitLastFrame[i]);
 	      }
 	    }
 	  }
 	  
 	  // now move 'hitThisFrame' to 'hitLastFrame'
 	  if(hitLastFrame != null)
 	  {
 	    EngineMath.Swap(ref hitLastFrame, ref hitThisFrame);
 	    if(hitThisFrame == null)
 	    {
 	      hitThisFrame = new List<SceneObject>(2);
 	    }
 	    else
 	    {
 	      hitThisFrame.Clear();
 	    }
 	  }
  }

  protected override void RenderContent()
  {
    switch(CollisionArea)
    {
      case CollisionArea.Circular:
      {
        Circle circle = (Circle)GetCollisionData();
        Video.FillCircle(circle.Center, circle.Radius);
        break;
      }
      case CollisionArea.Rectangular:
      {
        Rectangle rect = (Rectangle)GetCollisionData();
        GL.glRectd(rect.X, rect.Y, rect.Right, rect.Bottom);
        break;
      }
      case CollisionArea.Polygonal:
        throw new NotImplementedException();
      default:
        GL.glColor(Color.Red);
        GL.glBegin(GL.GL_LINE_LOOP);
          GL.glVertex2f(-1, -1);
          GL.glVertex2f( 1, -1);
          GL.glVertex2f( 1,  1);
          GL.glVertex2f(-1,  1);
        GL.glEnd();
        break;
    }
  }

  List<SceneObject> hitThisFrame, hitLastFrame;
}

} // namespace RotationalForce.Engine.Scene