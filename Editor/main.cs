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
    Application.Run(MainForm);
  }
  
  static readonly MainForm mainForm = new MainForm();
}

} // namespace RotationalForce.Editor