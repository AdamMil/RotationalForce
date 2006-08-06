using System;
using System.Windows.Forms;

namespace RotationalForce.Editor
{

static class EditorApp
{
  public static MainForm MainForm
  {
    get { return mainForm; }
  }
  
  [STAThread]
  static void Main()
  {
    Application.EnableVisualStyles();
    GLBuffer.SetCurrent(null); // make sure we always have a rendering context
    Application.Run(MainForm);
  }
  
  static readonly MainForm mainForm = new MainForm();
}

} // namespace RotationalForce.Editor