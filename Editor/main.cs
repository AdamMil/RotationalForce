using System;
using System.Windows.Forms;

namespace RotationalForce.Editor
{

static class EditorApp
{
  [STAThread]
  static void Main()
  {
    Application.EnableVisualStyles();
    Application.Run(new MainForm());
  }
}

} // namespace RotationalForce.Editor