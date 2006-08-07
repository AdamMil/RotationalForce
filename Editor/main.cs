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
    Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
    
    Application.EnableVisualStyles();
    GLBuffer.SetCurrent(null); // make sure we always have a rendering context (null sets the default context)
    Application.Run(MainForm);
  }

  static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
  {
    if(MessageBox.Show("An unhandled exception occurred. To continue execution, click Yes. To exit, click No. You'll "+
                       "have a chance to save if you choose to exit.\n\n" + e.Exception.ToString(), "Ruh roh",
                       MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
    {
      MainForm.Close();
    }
  }

  static readonly MainForm mainForm = new MainForm();
}

} // namespace RotationalForce.Editor