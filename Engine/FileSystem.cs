using System;
using System.IO;

namespace RotationalForce.Engine
{

#region StandardPath
/// <summary>Provides standard names for directories in which the engine will read or write data.</summary>
/// <remarks>These directory names are provided for implementers of <see cref="IFileSystem"/>. All paths provided by
/// the engine, except those referencing data at the root level, will begin with one of these directory names. Thus,
/// you can determine whether an engine-provided path is under a given standard directory by case-insensitively
/// checking whether the path begins with the directory value.
/// </remarks>
public static class StandardPath
{
  /// <summary>The directory from which image data will be loaded.</summary>
  public const string Images = "images/";
  /// <summary>The directory in which map files will be stored.</summary>
  public const string Maps = "maps/";
  /// <summary>The directory from which object data will be loaded.</summary>
  public const string Objects = "objects/";
  /// <summary>The directory in which save files will be stored.</summary>
  public const string SavedGames = "save/";
  /// <summary>The directory from which named scripts will be loaded.</summary>
  public const string Scripts = "scripts/";
}
#endregion

#region IFileSystem
/// <summary>The interface between the <see cref="Engine"/> and the logical filesystem in which the game's data files
/// are stored. This interface should treat filenames case-insensitively.
/// </summary>
public interface IFileSystem
{ 
  /// <summary>Determines whether the named file exists.</summary>
  /// <param name="path">A relative path within the filesystem. The directory separator character should be '/'.</param>
  /// <returns>True if the file exists and false otherwise.</returns>
  bool Exists(string path);
  /// <summary>Returns a list of file names in a given directory matching a given pattern.</summary>
  /// <param name="directory">
  /// A relative path naming a directory within the filesystem. The directory separator character should be '/', and
  /// the directory name will end with a separator.
  /// </param>
  /// <param name="pattern">A pattern to use to match filenames. The pattern is a standard filename except that the
  /// '*' character should match arbitrary substrings.
  /// </param>
  /// <returns>An array of fully-qualified paths to files that matched the given pattern.</returns>
  string[] GetFiles(string directory, string pattern);
  /// <summary>Opens a file for reading.</summary>
  /// <param name="path">
  /// A relative path naming a file within the filesystem. The directory separator character should be '/'.
  /// </param>
  /// <returns>A <see cref="Stream"/> containing the file data.</returns>
  /// <remarks>The returned stream is expected to be seeked to the beginning of the file. The stream must be seekable.
  /// The filesystem should support multiple concurrent readers of a single file if possible.
  /// </remarks>
  /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
  Stream OpenForRead(string path);
  /// <summary>Opens a file for writing.</summary>
  /// <param name="path">A relative path naming a file within the filesystem. The file does not have to exist.
  /// The directory separator character should be '/'.
  /// </param>
  /// <returns>An empty, seekable <see cref="Stream"/>.</returns>
  /// <remarks>The filesystem is only expected to support saving files beneath the
  /// <see cref="StandardPath.SavedGames"/> directory.
  /// </remarks>
  /// <exception cref="UnauthorizedAccessException">
  /// Thrown if the filesystem does not support saving under the given path.
  /// </exception>
  Stream OpenForWrite(string path);
}
#endregion

#region StandardFileSystem
/// <summary>
/// Provides an implementation of <see cref="IFileSystem"/> that maps directly to an operating system filesystem.
/// </summary>
public class StandardFileSystem : IFileSystem
{
  /// <summary>Initializes a <see cref="DefaultFileSystem"/> instance.</summary>
  /// <param name="baseDataPath">The base path from which data files should be loaded.</param>
  /// <param name="baseSavePath">The base path into which saved game files should be stored.</param>
  public StandardFileSystem(string baseDataPath, string baseSavePath)
  {
    if(baseDataPath == null) throw new ArgumentNullException("baseDataPath");
    if(baseSavePath == null) throw new ArgumentNullException("baseSavePath");
    dataPath = Path.GetFullPath(baseDataPath);
    savePath = Path.GetFullPath(baseSavePath);
  }

  public virtual bool Exists(string path) { return File.Exists(GetRealPath(path)); }

  public virtual string[] GetFiles(string directory, string pattern)
  {
    if(directory == null || pattern == null) throw new ArgumentNullException();
    string realDirectory = GetRealPath(directory);
    if(!Directory.Exists(realDirectory)) return new string[0];
    if(!directory.EndsWith("/")) directory += "/";
    string[] files = Directory.GetFiles(realDirectory, pattern);
    for(int i=0; i<files.Length; i++)
    {
      files[i] = directory + Path.GetFileName(files[i]);
    }
    return files;
  }

  public virtual Stream OpenForRead(string path)
  {
    return File.Open(GetReadPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
  }

  public virtual Stream OpenForWrite(string path)
  {
    return File.Open(GetWritePath(path), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
  }

  /// <summary>Gets the full path of the base data directory passed to the constructor.</summary>
  protected string DataPath { get { return dataPath; } }
  /// <summary>Gets the full path of the base save directory passed to the constructor.</summary>
  protected string SavePath { get { return savePath; } }

  /// <summary>Returns true if a path is under the saved game directory.</summary>
  protected static bool IsSavePath(string path)
  {
    return path.StartsWith(StandardPath.SavedGames, StringComparison.InvariantCultureIgnoreCase);
  }

  string GetDataPath(string path)
  {
    return Path.Combine(DataPath, path.Replace('/', Path.DirectorySeparatorChar));
  }

  string GetSavePath(string path)
  {
    path = path.Substring(StandardPath.SavedGames.Length); // strip off the save directory
    return Path.Combine(SavePath, path.Replace('/', Path.DirectorySeparatorChar));
  }

  string GetRealPath(string path)
  {
    if(path == null) throw new ArgumentNullException("path");
    return IsSavePath(path) ? GetSavePath(path) : GetDataPath(path);
  }

  string GetReadPath(string path)
  {
    if(Path.IsPathRooted(path)) throw new FileNotFoundException("Paths cannot be rooted.", path);
    return GetDataPath(path);
  }

  string GetWritePath(string path)
  {
    if(path == null) throw new ArgumentNullException("path");
    if(Path.IsPathRooted(path)) throw new UnauthorizedAccessException("Paths cannot be rooted.");
    if(!IsSavePath(path)) throw new UnauthorizedAccessException("Files can only be saved under the save directory.");
    return GetSavePath(path);
  }

  string dataPath, savePath;
}
#endregion

} // namespace RotationalForce.Engine