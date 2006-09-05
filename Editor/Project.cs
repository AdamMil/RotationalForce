using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Text;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{

sealed class Project
{
  const string Images = StandardPath.Images, Animations = StandardPath.Animations, Levels = StandardPath.Scenes,
               PerLevel = "perLevel", Objects = StandardPath.Objects;

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

  public string AnimationPath
  {
    get { return Path.Combine(EngineDataPath, Animations); }
  }

  public string PerLevelAnimationPath
  {
    get { return Path.Combine(AnimationPath, PerLevel); }
  }

  public string BasePath
  {
    get { return path; }
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
  
  public string ObjectsPath
  {
    get { return Path.Combine(EngineDataPath, Objects); }
  }

  public string GetNewShapeName()
  {
    XmlAttribute nextShape = projectNode.Attributes["nextShape"];
    if(nextShape == null)
    {
      nextShape = projectNode.SetAttributeNode("nextShape", null);
      nextShape.Value = "0";
    }

    int nextIndex = int.Parse(nextShape.Value);
    nextShape.Value = (nextIndex+1).ToString(CultureInfo.InvariantCulture);
    OnProjectModified();

    return "_shape" + nextIndex.ToString(CultureInfo.InvariantCulture);
  }

  public string GetEnginePath(string filename)
  {
    if(!IsUnderDataPath(filename)) throw new ArgumentException("Filename is not under the base path.");
    return NormalizePath(Path.GetFullPath(filename).Remove(0, path.Length+5)); // remove path + data/
  }

  public string GetRealPath(string enginePath)
  {
    return Path.Combine(EngineDataPath, DenormalizePath(enginePath));
  }

  public string GetLevelPath(string filename)
  {
    return Path.Combine(LevelsPath, filename);
  }

  public ImageMap GetImageMap(string enginePath)
  {
    foreach(ResourceHandle<ImageMap> handle in Engine.Engine.GetResources<ImageMap>())
    {
      if(handle.Resource != null &&
         string.Equals(enginePath, handle.Resource.ImageFile, StringComparison.OrdinalIgnoreCase))
      {
        return handle.Resource;
      }
    }

    return null;
  }

  public bool IsUnderDataPath(string filename)
  {
    return NormalizePath(Path.GetFullPath(filename)).StartsWith(NormalizePath(path)+"data/");
  }

  public void Save()
  {
    if(!projectModified) return;

    using(StreamWriter projectFile = new StreamWriter(Path.Combine(path, "rfproject.xml"), false, Encoding.UTF8))
    {
      projectNode.OwnerDocument.Save(projectFile);
      projectModified = false;
    }
  }

  public static Project Create(string basePath)
  {
    if(string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
    {
      throw new DirectoryNotFoundException();
    }

    string engineDataPath = Path.Combine(basePath, "data");
    Directory.CreateDirectory(Path.Combine(engineDataPath, Images));
    Directory.CreateDirectory(Path.Combine(engineDataPath, Animations));
    Directory.CreateDirectory(Path.Combine(Path.Combine(engineDataPath, Animations), PerLevel));
    Directory.CreateDirectory(Path.Combine(engineDataPath, Levels));
    Directory.CreateDirectory(Path.Combine(engineDataPath, Objects));

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

  void OnProjectModified()
  {
    projectModified = true;
  }

  XmlElement projectNode;
  string path;
  bool projectModified;
}

} // namespace RotationalForce.Editor
