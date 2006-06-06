using System.Collections.Generic;
using System.Drawing;

namespace RotationalForce.Engine
{

public class DesktopControl : GuiControl
{
  public DesktopControl()
  {
    this.BackColor = Color.Black;
    this.ForeColor = Color.White;
  }

  public void Render()
  {
    if(InvalidRect.Width != 0) base.Render(InvalidRect);

    if(controlsToRepaint.Count != 0)
    {
      for(int i=0; i<controlsToRepaint.Count; i++)
      {
        GuiControl control = controlsToRepaint[i];
        if(control.InvalidRect.Width > 0)
        {
          control.Render(control.ClientToScreen(control.InvalidRect));
        }
      }

      controlsToRepaint.Clear();
    }
  }

  internal void NeedsRepainting(GuiControl control)
  {
    if(!controlsToRepaint.Contains(control))
    {
      controlsToRepaint.Add(control);
    }
  }
  
  List<GuiControl> controlsToRepaint = new List<GuiControl>();
}

} // namespace RotationalForce.Engine