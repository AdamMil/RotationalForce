using System;
using System.Collections.Generic;
using System.Drawing;

namespace RotationalForce.Engine
{

public abstract class GuiControl
{
  #region Color
  /// <summary>
  /// The control's background color. If set to Color.Empty, it will attempt to retrieve the parent's background color.
  /// </summary>
  public Color BackColor
  {
    get { return backColor.IsEmpty && parent != null ? parent.BackColor : backColor; }
    set
    { 
      if(value != BackColor) Invalidate();
      backColor = value;
    }
  }
  
  public Color RawBackColor
  {
    get { return backColor; }
    set
    { 
      if(value != backColor)
      {
        backColor = value;
        Invalidate();
      }
    }
  }

  /// <summary>
  /// The control's foreground color. If set to Color.Empty, it will attempt to retrieve the parent's background color.
  /// </summary>
  public Color ForeColor
  {
    get { return color.IsEmpty && parent != null ? parent.ForeColor : color; }
    set
    { 
      if(value != ForeColor) Invalidate();
      color = value;
    }
  }
  
  public Color RawForeColor
  {
    get { return color; }
    set
    { 
      if(value != color)
      {
        color = value;
        Invalidate();
      }
    }
  }
  #endregion

  #region Position and size
  public Rectangle Bounds
  {
    get { return bounds; }
    set
    {
      if(value.Size.Width < 0 || value.Size.Height < 0)
        throw new ArgumentOutOfRangeException("Bounds", "Control size cannot be negative.");

      if(value.Location != bounds.Location)
      {
        Point oldPosition = bounds.Location;
        bounds.Location = value.Location;
        OnPositionChanged(oldPosition);
      }

      if(value.Size != bounds.Size)
      {
        Size oldSize = bounds.Size;
        bounds.Size = value.Size;
        OnSizeChanged(oldSize);
      }
    }
  }

  public Point Position
  {
    get { return bounds.Location; }
    set { Bounds = new Rectangle(value, bounds.Size); }
  }

  public int X
  {
    get { return bounds.X; }
    set { Position = new Point(value, bounds.Y); }
  }

  public int Y
  {
    get { return bounds.Y; }
    set { Position = new Point(bounds.X, value); }
  }

  public Size Size
  {
    get { return bounds.Size; }
    set { Bounds = new Rectangle(bounds.Location, value); }
  }

  public int Width
  {
    get { return bounds.Width; }
    set { Size = new Size(value, bounds.Height); }
  }

  public int Height
  {
    get { return bounds.Height; }
    set { Size = new Size(bounds.Width, value); }
  }

  public Rectangle ClientRect
  {
    get { return new Rectangle(0, 0, bounds.Width, bounds.Height); }
  }

  public Rectangle ScreenRect
  {
    get { return ClientToScreen(bounds); }
  }
  #endregion

  public DesktopControl Desktop
  {
    get
    {
      GuiControl ctl = this;
      while(ctl.parent != null) ctl = ctl.parent;
      return ctl as DesktopControl;
    }
  }

  public GuiControl Parent
  {
    get { return parent; }
    set
    {
      if(value != parent)
      {
        if(parent != null) parent.RemoveChild(this);
        if(value != null) value.AddChild(this); // AddChild will set 'parent'
      }
    }
  }

  public bool Enabled
  {
    get { return HasFlag(Flag.Enabled); }
    set { SetFlag(Flag.Enabled, value); }
  }

  public bool Visible
  {
    get { return HasFlag(Flag.Visible); }
    set { SetFlag(Flag.Visible, value); }
  }

  public Point ClientToScreen(Point clientPoint)
  {
    Point screenPoint = clientPoint;

    GuiControl ctl = this;
    do
    {
      screenPoint.Offset(ctl.bounds.X, ctl.bounds.Y);
      ctl = ctl.parent;
    } while(ctl != null);
    
    return screenPoint;
  }

  public Rectangle ClientToScreen(Rectangle clientRect)
  {
    return new Rectangle(ClientToScreen(clientRect.Location), clientRect.Size);
  }

  public Point ScreenToClient(Point screenPoint)
  {
    Point clientPoint = screenPoint;

    GuiControl ctl = this;
    do
    {
      clientPoint.Offset(-ctl.bounds.X, -ctl.bounds.Y);
      ctl = ctl.parent;
    } while(ctl != null);
    
    return clientPoint;
  }

  public Rectangle ScreenToClient(Rectangle screenRect)
  {
    return new Rectangle(ScreenToClient(screenRect.Location), screenRect.Size);
  }

  public void AddChild(GuiControl control)
  {
    if(control == null) throw new ArgumentNullException("control");
    if(control.parent != null) throw new ArithmeticException("This control already has a parent.");

    if(children == null) children = new List<GuiControl>();
    control.parent = this;
    children.Add(control);
    control.Invalidate();
  }

  public void RemoveChild(GuiControl control)
  {
    if(control.parent != this) throw new ArgumentException("This control is not my child.");
    children.Remove(control);
    control.parent = null;
  }

  public void Invalidate() { Invalidate(ClientRect); }
  
  public void Invalidate(Rectangle dirtyRect)
  {
    // if this requires the parent to paint something, then invalidate that portion of the parent instead
    if(BackColor.A == 0 && parent != null)
    {
      dirtyRect.Offset(bounds.Location); // calculate the area of the parent that contains this control
      parent.Invalidate(dirtyRect); // and invalidate that area
    }
    else
    {
      dirtyRect.Intersect(ClientRect); // clip the dirty rect to the control surface
      if(dirtyRect.Width != 0) // if part of the control surface is dirty
      {
        if(invalid.Width == 0) invalid = dirtyRect; // set the invalid rect if it's empty
        else invalid = Rectangle.Union(invalid, dirtyRect); // otherwise union it with the dirty rect
      }
      
      // finally, if we don't have a repaint pending, notify the desktop that we need one
      if(!HasFlag(Flag.PendingRepaint))
      {
        DesktopControl desktop = Desktop;
        if(desktop != null) desktop.NeedsRepainting(this);
        SetFlag(Flag.PendingRepaint, true);
      }
    }
  }

  /// <summary>Renders this control.</summary>
  /// <param name="drawArea">
  /// The screen area in which we're allowed to draw. Rendering should be contained within this rectangle.
  /// </param>
  /// <remarks>The background of the control will already have been cleared (if appropriate). The child controls will
  /// be rendered after this method returns.
  /// </remarks>
  internal protected virtual void RenderContent(Rectangle drawArea) { }

  protected virtual void OnPositionChanged(Point oldPosition)
  {
    if(parent != null)
    {
      // invalidate the area of the parent where we used to be
      parent.Invalidate(new Rectangle(oldPosition, bounds.Size));
      Invalidate(); // and invalidate our new area so that we get redrawn
    }
  }

  protected virtual void OnSizeChanged(Size oldSize)
  {
    if(invalid.Width != 0) invalid.Intersect(ClientRect); // if we have an invalid rectangle, clip it to the new size
    
    // if we have a parent and got smaller, we'll need to invalidate a portion of the parent
    if(parent != null)
    {
      if(bounds.Width < oldSize.Width) // if the width shrunk...
      {
        // if both dimensions shrunk, invalidate the area of the parent containing our old location
        if(bounds.Height < oldSize.Height)
        {
          parent.Invalidate(new Rectangle(bounds.Location, oldSize));
        }
        else // only the width shrunk, so invalidate just the sliver on the right side that we used to occupy
        {
          parent.Invalidate(new Rectangle(bounds.Width+bounds.X, bounds.Y, oldSize.Width-bounds.Width, oldSize.Height));
        }
      }
      // if only the height shrunk, invalidate the sliver on the bottom that we used to occupy
      else if(bounds.Height < oldSize.Height)
      {
        parent.Invalidate(new Rectangle(bounds.X, bounds.Height+bounds.Y, oldSize.Width, oldSize.Height-bounds.Height));
      }
    }
    
    Invalidate(); // redraw ourselves in our new position
  }

  internal Rectangle InvalidRect
  {
    get { return invalid; }
  }

  /// <summary>Renders this control and its children.</summary>
  /// <param name="drawArea">
  /// The screen area in which we're allowed to draw. Rendering should be contained within this rectangle.
  /// </param>
  internal void Render(Rectangle drawArea)
  {
    // the assumption is made that it won't be called with anything less than its invalid rect
    invalid = new Rectangle();
    SetFlag(Flag.PendingRepaint, false);

    // fill the background area if necessary
    Color backColor = BackColor;
    if(backColor.A != 0)
    {
      Video.FillRectangle(drawArea, backColor);
    }

    RenderContent(drawArea); // draw this control

    if(children != null) // and draw the children over it
    {
      foreach(GuiControl child in children)
      {
        if(child.Visible)
        {
          Rectangle intersection = Rectangle.Intersect(drawArea, child.ScreenRect);
          if(intersection.Width != 0) child.Render(intersection);
        }
      }
    }
  }
  
  enum Flag : uint
  {
    /// <summary>Determines whether the control and its children will be rendered.</summary>
    Visible = 0x01,
    /// <summary>Determines whether the control will respond to user input.</summary>
    Enabled = 0x02,
    /// <summary>Determines whether we've requested a repaint already.</summary>
    PendingRepaint = 0x04,
  }
  
  bool HasFlag(Flag flag) { return (flags & flag) != 0; }

  void SetFlag(Flag flag, bool on)
  {
    if(on) flags |= flag;
    else flags &= ~flag;
  }

  /// <summary>The object in which this GUI control is contained, or NULL if it is the root of a control hierarchy.</summary>
  GuiControl parent;

  /// <summary>The position and size of the control within its parent, in pixels.</summary>
  Rectangle bounds = new Rectangle(0, 0, 100, 100);
  
  /// <summary>The area of this control that needs repainting.</summary>
  Rectangle invalid;

  /// <summary>The list of this control's children.</summary>
  List<GuiControl> children;

  Color backColor = Color.Empty, color = Color.Empty;

  Flag flags = Flag.Enabled | Flag.Visible;
}

} // namespace RotationalForce.Engine