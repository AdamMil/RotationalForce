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
  Node
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

  public static void Log(string line) { logFile.WriteLine(line); }
  public static void Log(string format, params object[] args) { Log(string.Format(format, args)); }
  public static void Log(Exception e)
  {
    Log("Error occurred:\n"+e.ToString()+"\n");
    errorOccurred = true;
  }

  [STAThread]
  static void Main()
  {
    Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
    Application.EnableVisualStyles();

    try
    {
      try
      {
        GLBuffer.Initialize();
      }
      catch(Exception e)
      {
        Log(e);
      }

      if(!errorOccurred)
      {
        Application.Run(MainForm);
      }
    }
    finally
    {
      if(errorOccurred &&
         MessageBox.Show("Unhandled exceptions occurred. Save the log?", "Save the log?", MessageBoxButtons.YesNo,
                         MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
      {
        SaveFileDialog fd = new SaveFileDialog();
        fd.FileName = "errors.txt";
        fd.Title = "Save log as";

        if(fd.ShowDialog() == DialogResult.OK)
        {
          using(StreamWriter writer = new StreamWriter(fd.FileName))
          {
            writer.Write(logFile.GetStringBuilder().ToString());
          }
        }
      }
    }
  }

  static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
  {
    Log(e.Exception);

    if(MessageBox.Show("An unhandled exception occurred. To continue execution, click Yes. To exit, click No. You'll "+
                       "have a chance to save if you choose to exit. Maybe.\n\n" + e.Exception.ToString(), "Ruh roh",
                       MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
    {
      MainForm.Close();
    }
  }

  static readonly MainForm mainForm = new MainForm();
  static ClipboardObject clipboardObject;
  static StringWriter logFile = new StringWriter();
  static bool errorOccurred;
}

} // namespace RotationalForce.Editor