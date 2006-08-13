using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{

enum ObjectType
{
  None,
  Objects,
  Polygons
}

struct ClipboardObject
{
  public ClipboardObject(ObjectType type, byte[] data)
  {
    this.type = type;
    this.data = data;
  }
  
  public ObjectType Type
  {
    get { return type; }
  }
  
  public object[] Deserialize()
  {
    using(SexpReader reader = new SexpReader(new MemoryStream(data, false)))
    {
      List<object> list = new List<object>();

      Serializer.BeginBatch();
      while(!reader.EOF) list.Add(Serializer.Deserialize(reader));
      Serializer.EndBatch();

      return list.ToArray();
    }
  }

  byte[] data;
  ObjectType type;
}

static class EditorApp
{
  public static MainForm MainForm
  {
    get { return mainForm; }
  }
  
  public static ClipboardObject Clipboard
  {
    get { return clipboardObject; }
    set { clipboardObject = value; }
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
                       "have a chance to save if you choose to exit. Maybe.\n\n" + e.Exception.ToString(), "Ruh roh",
                       MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
    {
      MainForm.Close();
    }
  }

  static readonly MainForm mainForm = new MainForm();
  static ClipboardObject clipboardObject;
}

} // namespace RotationalForce.Editor