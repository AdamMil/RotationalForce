using System;
using System.IO;
using System.Xml;
using System.Text;

namespace RotationalForce.Editor
{

sealed class Project
{
  const string Images = "data/images", Audio = "data/audio", Levels = "data/levels", EditorData = "editorData";

  Project(string basePath)
  {
    if(string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
    {
      throw new DirectoryNotFoundException();
    }

    XmlDocument doc = new XmlDocument();
    doc.Load(Path.Combine(basePath, "rfproject.xml"));

    path        = Path.GetFullPath(basePath);
    projectNode = doc.DocumentElement;
  }

  #region Level
  public class Level
  {
    internal Level(Project project)
    {
      this.project = project;
    }

    public string File
    {
      get { return file; }
    }
    
    public string Name
    {
      get { return Xml.Attr(levelNode, "name"); }
    }

    public void Save(string filename)
    {
      string path = Path.GetFullPath(filename);
      levelNode.OwnerDocument.Save(path);
      file = path;
    }

    internal void CreateNew()
    {
      XmlDocument doc = new XmlDocument();
      levelNode = doc.CreateElement("RotationalForce.Level");
      doc.AppendChild(levelNode);
    }
    
    internal void Load(string filename)
    {
      XmlDocument doc = new XmlDocument();
      doc.Load(filename);
      levelNode = doc.DocumentElement;
      if(levelNode.LocalName != "RotationalForce.Level")
      {
        throw new ArgumentException("This is not a level file!");
      }
      file = Path.GetFullPath(filename);
    }

    Project project;
    XmlElement levelNode;
    string file;
  }
  #endregion

  public string BasePath
  {
    get { return path; }
  }

  public string EditorDataPath
  {
    get { return Path.Combine(path, EditorData); }
  }

  public string LevelsPath
  {
    get { return Path.Combine(path, Levels); }
  }

  public Level CreateLevel()
  {
    Level level = new Level(this);
    level.CreateNew();
    return level;
  }

  public Level LoadLevel(string filename)
  {
    Level level = new Level(this);
    level.Load(filename);
    return level;
  }

  public string GetLevelPath(string filename)
  {
    return Path.Combine(LevelsPath, filename);
  }

  public static Project Create(string basePath)
  {
    if(string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
    {
      throw new DirectoryNotFoundException();
    }

    Directory.CreateDirectory(Path.Combine(basePath, Images));
    Directory.CreateDirectory(Path.Combine(basePath, Audio));
    Directory.CreateDirectory(Path.Combine(basePath, Levels));
    Directory.CreateDirectory(Path.Combine(basePath, EditorData));

    StreamWriter projectFile = new StreamWriter(Path.Combine(basePath, "rfproject.xml"), false, Encoding.UTF8);
    projectFile.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8"" ?>");
    projectFile.WriteLine(@"<RotationalForce.Project schema=""1"" />");
    projectFile.Close();

    return Load(basePath);
  }

  public static Project Load(string basePath)
  {
    return new Project(basePath);
  }

  XmlElement projectNode;
  string path;
}

} // namespace RotationalForce.Editor
