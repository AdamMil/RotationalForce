using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{

sealed class Project
{
  const string Images = "images", Audio = "audio", Levels = "levels", EditorData = "editorData";

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
    
    if(!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
    {
      path += Path.DirectorySeparatorChar;
    }
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

  public string EngineDataPath
  {
    get { return Path.Combine(path, "data"); }
  }

  public string ImagesPath
  {
    get { return Path.Combine(EngineDataPath, Images); }
  }

  public string LevelsPath
  {
    get { return Path.Combine(EngineDataPath, Levels); }
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

  public string GetEnginePath(string filename)
  {
    if(!IsUnderDataPath(filename)) throw new ArgumentException("Filename is not under the base path.");
    return NormalizePath(Path.GetFullPath(filename).Remove(0, path.Length+5)); // remove path + data/
  }

  public string GetLevelPath(string filename)
  {
    return Path.Combine(LevelsPath, filename);
  }

  public ImageMap GetImageMap(string enginePath)
  {
    foreach(ImageMap map in Engine.Engine.GetImageMaps())
    {
      if(string.Equals(enginePath, map.ImageFile, StringComparison.OrdinalIgnoreCase))
      {
        return map;
      }
    }

    return null;
  }

  public bool IsUnderDataPath(string filename)
  {
    return NormalizePath(Path.GetFullPath(filename)).StartsWith(NormalizePath(path)+"data/");
  }

  public static Project Create(string basePath)
  {
    if(string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
    {
      throw new DirectoryNotFoundException();
    }

    string engineDataPath = Path.Combine(basePath, "data");
    Directory.CreateDirectory(Path.Combine(engineDataPath, Images));
    Directory.CreateDirectory(Path.Combine(engineDataPath, Audio));
    Directory.CreateDirectory(Path.Combine(engineDataPath, Levels));
    Directory.CreateDirectory(Path.Combine(engineDataPath, EditorData));

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

  public static string DenormalizePath(string enginePath)
  {
    if(Path.DirectorySeparatorChar != '/' && Path.AltDirectorySeparatorChar != '/')
    {
      enginePath = enginePath.Replace('/', Path.DirectorySeparatorChar);
    }
    return enginePath;
  }

  public static string NormalizePath(string path)
  {
    if(Path.DirectorySeparatorChar != '/')
    {
      path = path.Replace(Path.DirectorySeparatorChar, '/');
    }
    if(Path.AltDirectorySeparatorChar != '/')
    {
      path = path.Replace(Path.AltDirectorySeparatorChar, '/');
    }
    return path.ToLowerInvariant();
  }

  XmlElement projectNode;
  string path;
  bool projectModified;
}

} // namespace RotationalForce.Editor
