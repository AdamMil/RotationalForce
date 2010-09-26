using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RotationalForce.Engine
{

#region NonSerializableObject
public abstract class NonSerializableObject : ISerializable
{
  Type ISerializable.TypeToSerialize
  {
    get { throw new NotSupportedException("Objects of type "+GetType().FullName+" cannot be serialized."); }
  }

  void ISerializable.BeforeSerialize(SerializationStore store)
  {
  }

  void ISerializable.BeforeDeserialize(DeserializationStore store)
  {
  }

  void ISerializable.Serialize(SerializationStore store)
  {
  }

  void ISerializable.Deserialize(DeserializationStore store)
  {
  }
}
#endregion

#region UniqueObject
/// <summary>Provides a way that identical objects in the object graph can be pooled. This also works around the
/// problem of id</summary>
public abstract class UniqueObject : ISerializable
{
  protected UniqueObject()
  {
    // generate a unique ID even if we're being deserialized, because we don't want the deserialized ID to conflict
    // with another ID
    id = nextID++;
  }

  protected virtual void Serialize(SerializationStore store) { }
  protected virtual void Deserialize(DeserializationStore store) { }

  #region ISerializable
  Type ISerializable.TypeToSerialize
  {
    get
    {
      // if we're not combining identical objects, or this object hasn't been serialized yet, then serialize this type
      if(!objectPool.ContainsKey(id))
      {
        return GetType();
      }
      else // otherwise, we are combining identical objects, and this object's been serialized already.
      {
         return typeof(UniqueObjectReference);
      }
    }
  }

  void ISerializable.BeforeSerialize(SerializationStore store)
  {
    store.AddValue("UniqueObject.ID", id);
    objectPool[id] = this;
  }

  void ISerializable.BeforeDeserialize(DeserializationStore store)
  {
    uint serializedId = store.GetUint32("UniqueObject.ID");
    objectPool.Add(serializedId, this);
  }

  void ISerializable.Serialize(SerializationStore store)
  {
    Serialize(store);
  }

  void ISerializable.Deserialize(DeserializationStore store)
  {
    Deserialize(store);
  }
  #endregion
  
  [NonSerialized] uint id;

  /// <summary>Retrieves a pooled object, given its ID. The object must exist in the pool, or an exception will occur.</summary>
  internal static UniqueObject GetPooledObject(uint id)
  {
    return objectPool[id];
  }

  /// <summary>Resets the pool of known objects. This should be called before and after each block of serializations
  /// that need identical objects to be pooled. Failing to call this can cause objects to hang around in memory and
  /// never be deleted, and can cause future serializations/deserializations to fail.
  /// </summary>
  internal static void ResetObjectPool()
  {
    objectPool.Clear();
  }
  
  static Dictionary<uint,UniqueObject> objectPool = new Dictionary<uint,UniqueObject>();

  static uint nextID = 1;
}
#endregion

#region UniqueObjectReference
sealed class UniqueObjectReference : ISerializable, IObjectReference
{
  Type ISerializable.TypeToSerialize
  {
    get { return GetType(); }
  }

  void ISerializable.BeforeSerialize(SerializationStore store)
  {
  }

  void ISerializable.BeforeDeserialize(DeserializationStore store)
  {
    id = store.GetUint32("UniqueObject.ID");
  }

  void ISerializable.Serialize(SerializationStore store)
  {
  }

  void ISerializable.Deserialize(DeserializationStore store)
  {
  }

  object IObjectReference.GetRealObject()
  {
    return UniqueObject.GetPooledObject(id);
  }
  
  uint id;
}
#endregion

#region SexpNodeType
/// <summary>An enum describing the type of the current node read from a <see cref="SexpReader"/>.</summary>
public enum SexpNodeType
{
  /// <summary>No node has been read yet. This value is only used internally by the <see cref="SexpReader"/> class.</summary>
  Invalid,
  /// <summary>The reader has just read the start of an element.</summary>
  Begin,
  /// <summary>The reader has just read some text content from the element.</summary>
  Content,
  /// <summary>The reader has just read the end of an element.</summary>
  End,
  /// <summary>The reader has consumed all available data.</summary>
  EOF
}
#endregion

#region SexpReader
/// <summary>This class represents a reader that can read a sequence of S-expressions.</summary>
public sealed class SexpReader : IDisposable
{
  /// <summary>Initializes the reader with a stream assumed to be encoded as UTF-8.</summary>
  public SexpReader(Stream stream) : this(stream, Encoding.UTF8) { }

  /// <summary>Initializes the reader with the given stream and encoding.</summary>
  public SexpReader(Stream stream, Encoding encoding) : this(new StreamReader(stream, encoding)) { }

  /// <summary>Initializes the reader with the given <see cref="TextReader"/>.</summary>
  public SexpReader(TextReader reader)
  {
    if(reader == null) throw new ArgumentNullException();
    this.reader = reader;
    NextChar(); // read the first character if any. ReadFrom*() assume we're at the first character of the next item
    Read(); // advance to the first S-expression node if any
  }

  /// <summary>Gets how deeply nested the reader is within the tag tree.</summary>
  public int Depth
  {
    get { return tagNames.Count; }
  }

  /// <summary>Gets whether the <see cref="SexpReader"/> has consumed all data.</summary>
  public bool EOF
  {
    get { return nodeType == SexpNodeType.EOF; }
  }

  /// <summary>Gets the <see cref="SexpNodeType"/> of the node at which the reader is currently positioned.</summary>
  public SexpNodeType NodeType
  {
    get { return nodeType; }
  }

  /// <summary>Gets the name of the tag in which the reader is currently positioned.</summary>
  public string TagName
  {
    get { return tagNames.Peek(); }
  }

  /// <summary>Closes the reader's underlying stream.</summary>
  public void Close()
  {
    reader.Close();
  }

  /// <summary>Disposes the reader's underlying stream.</summary>
  public void Dispose()
  {
    reader.Dispose();
  }

  /// <summary>Asserts that the reader is positioned at text content, and returns the content.</summary>
  public string GetContent()
  {
    if(nodeType != SexpNodeType.Content)
    {
      throw new InvalidOperationException("The reader is not positioned on any text content.");
    }

    return content;
  }

  /// <summary>Advances the reader to the next element or the next part of the current element.</summary>
  public void Read()
  {
    SkipWhitespace();

    switch(nodeType)
    {
      case SexpNodeType.Invalid:
        ReadFromBOF();
        break;

      case SexpNodeType.Begin:
        ReadFromBegin();
        break;

      case SexpNodeType.Content:
        ReadFromContent();
        break;

      case SexpNodeType.End:
        ReadFromEnd();
        break;

      case SexpNodeType.EOF: // at EOF, we don't change state
        break;
    }

    if(nodeType != SexpNodeType.Content) // clear the content if we're not on a content node
    {
      content = null;
    }
  }

  /// <summary>Asserts that the reader is positioned at the beginning of an element and then calls <see cref="Read"/>.</summary>
  public void ReadBeginElement()
  {
    if(nodeType != SexpNodeType.Begin)
    {
      throw new InvalidOperationException("The reader is not positioned at the beginning of an element.");
    }
    Read();
  }

  /// <summary>Asserts that the reader is positioned at the beginning of an element with the given name and then calls
  /// <see cref="Read"/>.
  /// </summary>
  public void ReadBeginElement(string name)
  {
    if(nodeType != SexpNodeType.Begin || !string.Equals(TagName, name, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("The reader is not positioned at the beginning of an element named '"+
                                          name+"'.");
    }
    Read();
  }

  /// <summary>Asserts that the reader is positioned at text content, returns the content, and then calls
  /// <see cref="Read"/> to advance past the content.
  /// </summary>
  public string ReadContent()
  {
    string content = GetContent();
    Read();
    return content;
  }

  /// <summary>Asserts that the reader is positioned at the end of an element and then calls <see cref="Read"/> to
  /// advance to the next item.
  /// </summary>
  public void ReadEndElement()
  {
    if(nodeType != SexpNodeType.End)
    {
      throw new InvalidOperationException("The reader is not positioned and the end of an element.");
    }
    Read();
  }

  /// <summary>Asserts that the reader is positioned at the beginning of an element with the given name, calls
  /// <see cref="Read"/>, gets the element content if any, and finally calls <see cref="ReadEndElement"/>.
  /// </summary>
  /// <returns>The text content of the element if there is any, and an empty string otherwise.</returns>
  public string ReadElement(string name)
  {
    ReadBeginElement(name);
    string content = nodeType == SexpNodeType.Content ? ReadContent() : string.Empty;
    ReadEndElement();
    return content;
  }

  /// <summary>Skips past the current node (skipping over all subnodes as well).</summary>
  public void Skip()
  {
    if(EOF) return;

    string tag = TagName;
    int  depth = tagNames.Count;

    // read until the end of the current node
    while(nodeType != SexpNodeType.End || tagNames.Count != depth ||
          !string.Equals(TagName, tag, StringComparison.Ordinal))
    {
      Read();
    }

    Read(); // then read one more (to advance past the end of the current node)
  }

  /// <summary>Returns a string briefly describing the current reader state.</summary>
  public override string ToString()
  {
    return nodeType == SexpNodeType.EOF ? "[EOF]" : string.Format("[{0} {1}]", TagName, nodeType);
  }

  // at the beginning of the stream, we expect to see the beginning of a node or EOF
  void ReadFromBOF()
  {
    if(streamAtEOF)
    {
      nodeType = SexpNodeType.EOF;
    }
    else
    {
      if(thisChar != '(') throw InvalidData("expected beginning of element");
      ReadStartNode();
    }
  }

  // after the beginning of a node, we may find content, another node, or the node end
  void ReadFromBegin()
  {
    // we'll be positioned on the character immediately after the tag name.
    if(streamAtEOF)
    {
      throw UnexpectedEOF();
    }
    else if(thisChar == '(') // if we're at the beginning of a new element, read the new element
    {
      ReadStartNode();
    }
    else if(thisChar == ')') // if we're at the end of the current element, mark that we're at the end
    {
      nodeType = SexpNodeType.End;
    }
    else // otherwise, we're at some content. read it in
    {
      ReadTextContent();
    }
  }

  // after text content, we may find another node or the end of the current node
  void ReadFromContent()
  {
    // we'll be positioned on the character immediately following the content, which should be a parenthesis.

    if(streamAtEOF)
    {
      throw UnexpectedEOF();
    }
    else if(thisChar == '(') // if we're at the beginning of a new element, read the new element
    {
      ReadStartNode();
    }
    else if(thisChar == ')') // if we're at the end of the current element, mark that we're at the end
    {
      nodeType = SexpNodeType.End;
    }
    else
    {
      throw InvalidData("unexpected text content. Has the underlying stream been repositioned?");
    }
  }

  // after a node end, we expect EOF (only at the root), more content (except at the root), or another root node
  void ReadFromEnd()
  {
    tagNames.Pop();
    NextChar(); // advance past closing parenthesis
    SkipWhitespace();

    if(streamAtEOF) // if we're at EOF, it's an error if not all tags are closed
    {
      if(tagNames.Count == 0)
      {
        nodeType = SexpNodeType.EOF;
      }
      else
      {
        throw UnexpectedEOF();
      }
    }
    else if(thisChar == '(') // if we're at another root node, read it
    {
      ReadStartNode();
    }
    else if(thisChar == ')') // if we're at an end node, it's invalid unless there's another tag open
    {
      if(tagNames.Count == 0)
      {
        throw InvalidData("unexpected ')' [all tags have been closed already]");
      }
    }
    else // if we're at some content, it's an error if all tags have been closed already
    {
      if(tagNames.Count == 0)
      {
        throw InvalidData("unexpected text content"); // content must not appear outside a tag
      }
      else
      {
        ReadTextContent();
      }
    }
  }

  void ReadStartNode()
  {
    NextChar(); // skip over the opening parethesis
    SkipWhitespace(); // skip whitespace before the tag name
    string tagName = ReadTagName(); // read the tag name
    tagNames.Push(tagName); // add it to the stack
    nodeType = SexpNodeType.Begin;
  }

  void ReadTextContent()
  {
    content  = ReadString(false);
    nodeType = SexpNodeType.Content;
  }

  string ReadTagName()
  {
    return ReadString(true);
  }

  string ReadString(bool stopAtWhitespace)
  {
    StringBuilder sb = new StringBuilder();

    // read until we find a node delimiter, EOF, or whitespace if stopAtWhitespace is true
    while(thisChar != '(' && thisChar != ')' && (!stopAtWhitespace || !char.IsWhiteSpace(thisChar)) && !streamAtEOF)
    {
      if(thisChar == '\\') // use backslash as an escape character
      {
        NextChar();
        if(streamAtEOF) throw new EndOfStreamException();
      }
      sb.Append(thisChar);
      NextChar();
    }

    return sb.ToString();
  }
  
  void SkipWhitespace()
  {
    while(char.IsWhiteSpace(thisChar))
    {
      NextChar();
    }
  }

  void NextChar()
  {
    int c = reader.Read();
    if(c == -1) // if we're at the end of the stream
    {
      streamAtEOF = true;
      thisChar = '\0';
    }
    else
    {
      thisChar = (char)c;
      if(thisChar == '\n')
      {
        line++;
        column = 1;
      }
      else if(thisChar != '\r') // don't count the \r in \r\n pairs as a column
      {
        column++;
      }
    }
  }

  InvalidDataException InvalidData(string message)
  {
    return new InvalidDataException(string.Format("At {0}:{1}, {2}", line, column, message));
  }
  
  EndOfStreamException UnexpectedEOF()
  {
    return new EndOfStreamException(string.Format("At {0}:{1}, unexpected EOF. element '{2}' was not closed.",
                                                  line, column, TagName));
  }

  /// <summary>The text stream.</summary>
  TextReader reader;
  /// <summary>A stack of open tags in which the reader is positioned.</summary>
  Stack<string> tagNames = new Stack<string>();
  /// <summary>The current text content, or null if the reader is not positioned at a text node.</summary>
  string content;
  /// <summary>The current position of the reader in the stream.</summary>
  int line=1, column=1;
  /// <summary>The character most-recently read from the stream.</summary>
  char thisChar;
  /// <summary>The type of node at which the reader is currently positioned.</summary>
  SexpNodeType nodeType;
  /// <summary>Whether or not we've reached the end of the stream.</summary>
  bool streamAtEOF;
}
#endregion

#region SexpWriter
public sealed class SexpWriter : IDisposable
{
  public SexpWriter(Stream stream) : this(stream, Encoding.UTF8) { }

  public SexpWriter(Stream stream, Encoding encoding) : this(new StreamWriter(stream, encoding)) { }

  public SexpWriter(TextWriter writer)
  {
    if(writer == null) throw new ArgumentNullException();
    this.writer = writer;
  }

  /// <summary>Begins an element with the given name. The element should be closed with <see cref="EndElement."/></summary>
  public void BeginElement(string tagName)
  {
    if(string.IsNullOrEmpty(tagName)) throw new ArgumentException("Tag name cannot be empty.");

    BeginContent();
    writer.Write('(');
    writer.Write(Encode(tagName, true));
    depth++;
    hasContent = false;
  }

  /// <summary>Ends an element previous started with <see cref="BeginElement"/>.</summary>
  public void EndElement()
  {
    if(depth == 0) throw new InvalidOperationException("No tags are open.");

    writer.Write(')');
    depth--;
    hasContent = true;
  }

  /// <summary>Writes text into the content area of the open element.</summary>
  public void WriteContent(string text)
  {
    if(!string.IsNullOrEmpty(text))
    {
      BeginContent();
      writer.Write(Encode(text, false));
    }
    else if(text == null)
    {
      throw new ArgumentNullException();
    }
  }
  
  /// <summary>Writes an empty element with the given name.</summary>
  public void WriteElement(string tagName)
  {
    WriteElement(tagName, null);
  }

  /// <summary>Writes an element with the given name and content.</summary>
  /// <param name="content">The element content. If null, an empty element will be written.</param>
  public void WriteElement(string tagName, string content)
  {
    BeginElement(tagName);
    if(content != null)
    {
      WriteContent(content);
    }
    EndElement();
  }

  public void Close()
  {
    writer.Close();
  }

  public void Dispose()
  {
    writer.Dispose();
  }

  public void Flush()
  {
    writer.Flush();
  }

  /// <summary>Adds the separator between the tag name and the content, if necessary.</summary>
  void BeginContent()
  {
    if(!hasContent)
    {
      writer.Write(' ');
      hasContent = true;
    }
  }

  /// <summary>The output stream.</summary>
  TextWriter writer;
  /// <summary>The current node depth.</summary>
  int depth;
  /// <summary>Whether the current node has content.</summary>
  /// <remarks>If false, a separator will be added before any content or nested tags are written.</remarks>
  bool hasContent = true;

  static string Encode(string str, bool encodeInnerSpaces)
  {
    StringBuilder sb = new StringBuilder(str.Length);
    bool afterLeadingSpaces = false;
    int numTrailingSpaces = 0;

    for(int i=0; i<str.Length; i++)
    {
      char c = str[i];

      // if it's a special character (a leading space, a backslash, or a parenthesis), escape it
      if((!afterLeadingSpaces || encodeInnerSpaces) && char.IsWhiteSpace(c) || c=='(' || c==')' || c=='\\')
      {
        sb.Append('\\').Append(c);
        numTrailingSpaces = 0;
      }
      else if(char.IsWhiteSpace(c)) // otherwise, if it's a potentially-trailing space, keep track of it
      {
        numTrailingSpaces++;
      }
      else // otherwise, it's a normal character, so append it as-is
      {
        if(numTrailingSpaces != 0) // if we were tracking any potentially-trailing spaces, write them out
        {
          sb.Append(str, i-numTrailingSpaces, numTrailingSpaces);
          numTrailingSpaces = 0;
        }

        sb.Append(c);
        afterLeadingSpaces = true;
      }
    }

    // now add the trailing spaces, encoding them
    for(int i=str.Length-numTrailingSpaces; i<str.Length; i++)
    {
      sb.Append('\\').Append(str[i]);
    }
    
    return sb.ToString();
  }
}
#endregion

#region SerializationStore
public class SerializationStore
{
  internal SerializationStore(SexpWriter writer, string namespaceName)
  {
    if(writer == null) throw new ArgumentNullException();
    this.writer = writer;
    this.ns     = namespaceName;
  }

  /// <summary>Adds an object to the serialization store.</summary>
  /// <param name="name">The name of the object to add. This must be unique.</param>
  /// <param name="value">The value to add.</param>
  /// <exception cref="ArgumentException">Thrown if the name has already been added to the store.</exception>
  public void AddValue(string name, object value)
  {
    if(name == null) throw new ArgumentNullException("name");

    if(!valueAdded)
    {
      writer.BeginElement(ns);
      valueAdded = true;

      #if DEBUG
      addedNames = new List<string>();
      #endif
    }
    
    #if DEBUG
    if(addedNames.Contains(name))
    {
      throw new ArgumentException("A value with the name '"+name+"' has already been added.");
    }
    addedNames.Add(name);
    #endif

    writer.BeginElement(name);
    Serializer.Serialize(value, writer);
    writer.EndElement();
  }

  /// <summary>Finishes writing the value store.</summary>
  internal void Finish()
  {
    if(valueAdded)
    {
      writer.EndElement();
    }
  }

  SexpWriter writer;
  string ns;
  bool valueAdded;
  
  #if DEBUG
  List<string> addedNames;
  #endif
}
#endregion

#region DeserializationStore
public class DeserializationStore
{
  internal DeserializationStore(SexpReader reader, string namespaceName)
  {
    if(reader == null) throw new ArgumentNullException("reader");
    this.reader = reader;
    this.ns     = namespaceName;

    // it's possible that the reader has no namespace node, in which case, there are no values to read.
    // but if it does have a namespace node, move past it.
    hasNamespaceNode = reader.NodeType == SexpNodeType.Begin &&
                       string.Equals(reader.TagName, namespaceName, StringComparison.Ordinal);
    if(hasNamespaceNode)
    {
      reader.Read();
    }
  }

  /// <summary>Gets the value corresponding to the given name.</summary>
  /// <exception cref="KeyNotFoundException">Thrown if the named value is not found.</exception>
  public object GetValue(string name)
  {
    object value;
    if(TryGetValue(name, out value))
    {
      return value;
    }
    else
    {
      throw new KeyNotFoundException("Cannot find a value called '"+name+"'.");
    }
  }

  public bool GetBoolean(string name)
  {
    return (bool)GetValue(name);
  }

  public int GetInt32(string name)
  {
    return (int)GetValue(name);
  }

  public uint GetUint32(string name)
  {
    return (uint)GetValue(name);
  }

  public float GetSingle(string name)
  {
    return (float)GetValue(name);
  }

  public double GetDouble(string name)
  {
    return (double)GetValue(name);
  }

  public string GetString(string name)
  {
    return (string)GetValue(name);
  }

  /// <summary>Tries to get the value corresponding to the given name.</summary>
  /// <returns>Returns true if the value was found and false otherwise.</returns>
  public bool TryGetValue(string name, out object value)
  {
    if(!hasNamespaceNode)
    {
      value = null;
      return false;
    }
    else if(values == null)
    {
      values = new LinkedList<KeyValuePair<string, object>>();
    }
    else if(TryGetCachedValue(name, out value))
    {
      return true; // if it's in the value cache, return it
    }

    // otherwise, we haven't read it yet, so read until we find it.
    while(reader.NodeType == SexpNodeType.Begin) // while we're not at the end of the data yet
    {
      string nodeName = reader.TagName;

      reader.ReadBeginElement(); // read the opening variable name element
      value = Serializer.Deserialize(reader);
      reader.ReadEndElement(); // consume the closing variable name node

      // prepend the new object to the list (where it can be found quickest)
      values.AddFirst(new KeyValuePair<string,object>(nodeName, value));

      if(string.Equals(nodeName, name, StringComparison.Ordinal)) // if this was the one we wanted, return success
      {
        return true;
      }
    }

    value = null;
    return false; // we couldn't find it.
  }

  /// <summary>Advances the reader past all the remaining nodes in the value store.</summary>
  internal void Finish()
  {
    if(hasNamespaceNode)
    {
      while(reader.NodeType == SexpNodeType.Begin) // skip all data nodes
      {
        reader.Skip();
      }
      reader.ReadEndElement(); // then consume the closing namespace node
    }
  }

  /// <summary>Searches the cache for a named value.</summary>
  bool TryGetCachedValue(string name, out object value)
  {
    LinkedListNode<KeyValuePair<string,object>> node = values.First;
    while(node != null && !string.Equals(node.Value.Key, name, StringComparison.Ordinal))
    {
      node = node.Next;
    }
    
    if(node == null)
    {
      value = null;
      return false;
    }
    else
    {
      value = node.Value.Value;
      return true;
    }
  }

  SexpReader reader;
  string ns;
  LinkedList<KeyValuePair<string,object>> values;
  bool hasNamespaceNode;
}
#endregion

#region ISerializable
/// <summary>This interface can be implemented to control serialization.</summary>
public interface ISerializable
{
  /// <summary>Gets the type to serialize.</summary>
  /// <remarks>Normally this should be the return value of <see cref="System.Object.GetType()"/>, but it can be set to
  /// a different type. During deserialization, an object of the given type will be deserialized. The type must have
  /// a public, parameterless constructor. If a type equal to the object type is returned, the object's fields will
  /// automatically be serialized.
  /// </remarks>
  /// <seealso cref="IObjectReference"/>
  Type TypeToSerialize { get; }

  /// <summary>Gives the object a chance to add additional serialization data. This method will be added before any
  /// complex fields (such as pointers to other objects) are processed.</summary>
  /// <remarks>The point of this method is to enable objects to handle circular references without too much trouble.
  /// An object can add itself to a dictionary in this method. The object can then return a proxy from
  /// <see cref="TypeToSerialize"/> based on that information.
  /// </remarks>
  /// <seealso cref="IObjectReference"/>
  void BeforeSerialize(SerializationStore store);

  /// <summary>Provides the additonal serialization data added by <see cref="BeforeSerialize"/>.</summary>
  /// <remarks>The point of this method is to enable objects to handle circular references without too much trouble.
  /// An object can add itself to a dictionary in this method, and any proxies for this object can read the dictionary
  /// to find the original object. Note that if <see cref="BeforeSerialize"/> does not add any data, this method will
  /// still be called.
  /// </remarks>
  /// <seealso cref="IObjectReference"/>
  void BeforeDeserialize(DeserializationStore store);

  /// <summary>Gives the object a chance to add additional serialization data.</summary>
  /// <remarks>Note that as long as <see cref="TypeToSerialize"/> returns a value equal to
  /// <see cref="System.Object.GetType()"/>, the object's fields will automatically be serialized and deserialized, and
  /// don't need to be added manually.
  /// </remarks>
  void Serialize(SerializationStore store);

  /// <summary>Provides the additonal serialization data added by <see cref="Serialize"/>.</summary>
  /// <remarks>Note that if <see cref="Serialize"/> does not add any data, this method will still be
  /// called.
  /// </remarks>
  void Deserialize(DeserializationStore store);
}
#endregion

#region IObjectReference
/// <summary>This interface can be implemented to implement a serialization proxy.</summary>
/// <example>
/// sealed class Singleton : ISerializable
/// {
///   private Singleton() { value = "Hello, world."; }
///   public string Value { get { return value; } }
///   public static Singleton Instance = new Singleton();
///   
///   Type ISerializable.TypeToSerialize { get { return typeof(SingletonReference); } }
///   void ISerializable.Serialize(SerializationStore store) { }
///   void ISerializable.Deserialize(DeserializationStore store) { }
/// }
/// 
/// sealed class SingletonReference : IObjectReference
/// {
///   object IObjectReference.GetRealObject() { return Singleton.Instance; }
/// }
/// </example>
/// <seealso cref="ISerializable"/>
public interface IObjectReference
{
  /// <summary>Returns the real value to return from the deserialization process.</summary>
  object GetRealObject();
}
#endregion

// TODO: optimize serialization/deserialization with custom-generated code
#region Serializer
public static class Serializer
{
  static Serializer()
  {
    typeDict = new Dictionary<string,Type>(18);
    typeDict["int"]      = typeof(int);
    typeDict["double"]   = typeof(double);
    typeDict["string"]   = typeof(string);
    typeDict["bool"]     = typeof(bool);
    typeDict["float"]    = typeof(float);
    typeDict["null"]     = null;
    typeDict["uint"]     = typeof(uint);
    typeDict["long"]     = typeof(long);
    typeDict["ulong"]    = typeof(ulong);
    typeDict["byte"]     = typeof(byte);
    typeDict["char"]     = typeof(char);
    typeDict["short"]    = typeof(short);
    typeDict["ushort"]   = typeof(ushort);
    typeDict["dateTime"] = typeof(DateTime);
    typeDict["decimal"]  = typeof(decimal);
    typeDict["sbyte"]    = typeof(sbyte);

    typeDict["point"]    = typeof(AdamMil.Mathematics.Geometry.TwoD.Point);
    typeDict["vector"]   = typeof(AdamMil.Mathematics.Geometry.TwoD.Vector);
  }

  /// <summary>Resets the pool of known objects. This should be called before each block of
  /// serialization/deserializations that need <see cref="UniqueObject"/> pointers to be pooled. This is necessary to
  /// properly handle circular pointers and multiple pointers to the same object. Failing to call this can cause future
  /// serializations/deserializations to fail.
  /// </summary>
  public static void BeginBatch()
  {
    if(inBatch) throw new InvalidOperationException("Serialization batches cannot be nested.");
    UniqueObject.ResetObjectPool();
    inBatch = true;
  }

  /// <summary>Resets the pool of known objects. This should be called before each block of
  /// serialization/deserializations that need <see cref="UniqueObject"/> pointers to be pooled. This is necessary to
  /// properly handle circular pointers and multiple pointers to the same object. Failing to call this can cause
  /// objects to hang around in memory and never be deleted, and can cause future serializations/deserializations
  /// to fail.
  /// </summary>
  public static void EndBatch()
  {
    if(!inBatch) throw new InvalidOperationException("A serialization batch was not begun.");
    UniqueObject.ResetObjectPool();
    inBatch = false;
  }

  #region Serialization
  /// <summary>Serializes an object into the given <see cref="Stream"/>. The object can be null.</summary>
  /// <remarks>If you'll be serializing multiple objects to the stream, you must instead create a
  /// <see cref="SexpWriter"/> and call the overload that takes it.
  /// </remarks>
  public static void Serialize(object obj, Stream store)
  {
    SexpWriter writer = new SexpWriter(store);
    Serialize(obj, writer);
    // make sure to flush the TextWriter inside the SexpWriter so that the content shows up in 'store' immediately
    writer.Flush();
  }

  /// <summary>Serializes an object into the given <see cref="TextWriter"/>. The object can be null.</summary>
  /// <remarks>If you'll be serializing multiple objects to the text writer, you must instead create a
  /// <see cref="SexpWriter"/> and call the overload that takes it.
  /// </remarks>
  public static void Serialize(object obj, TextWriter store)
  {
    Serialize(obj, new SexpWriter(store));
  }

  /// <summary>Serializes an object into the given <see cref="SexpWriter"/>. The object can be null.</summary>
  public static void Serialize(object obj, SexpWriter store)
  {
    if(store == null) throw new ArgumentNullException("store");

    Type type = obj == null ? null : obj.GetType(); // get the type of the object

    if(obj == null) // if it's a null object, just write (null)
    {
      store.WriteElement("null");
    }
    else if(IsSimpleType(type)) // otherwise, if it's a simple type, write out the simple type, eg (int 5)
    {
      store.WriteElement(GetTypeName(type), GetSimpleValue(obj, type));
    }
    else if(type == typeof(string))
    {
      store.WriteElement(GetTypeName(type), (string)obj);
    }
    else if(type.IsArray) // otherwise, if it's a complex array (simple arrays are handled by GetSimpleValue)
    {
      SerializeArray((Array)obj, type, store);
    }
    else if(obj is IList)
    {
      SerializeIList((IList)obj, type, store);
    }
    else if(obj is IDictionary)
    {
      SerializeDictionary((IDictionary)obj, type, store);
    }
    else // otherwise, it's an object (a non-primitive, non-simple type)
    {
      SerializeObject(obj, type, store);
    }
  }

  static void SerializeArray(Array array, Type type, SexpWriter store)
  {
    store.BeginElement(type.FullName); // write the array's typename

    // write the dimensions attribute (this doesn't handle non-zero-bounded arrays)
    // a 2x5 array will have (@dimensions 2,5)
    string dims = null;
    for(int i=0; i<array.Rank; i++)
    {
      if(array.GetLowerBound(i) != 0)
      {
        throw new NotImplementedException("Non-zero bound arrays are not implemented.");
      }

      if(i != 0) dims += ",";
      dims += array.GetLength(i).ToString(CultureInfo.InvariantCulture);
    }
    store.WriteElement("@dimensions", dims);

    foreach(object element in array) // serialize the elements themselves
    {
      Serialize(element, store);
    }

    store.EndElement(); // close the array element
  }
  
  static void SerializeDictionary(IDictionary dict, Type type, SexpWriter store)
  {
    store.BeginElement(GetTypeName(type));
    foreach(DictionaryEntry de in dict)
    {
      Serialize(de.Key, store);
      Serialize(de.Value, store);
    }
    store.EndElement();
  }
  
  static void SerializeIList(IList list, Type type, SexpWriter store)
  {
    store.BeginElement(GetTypeName(type));
    foreach(object o in list)
    {
      Serialize(o, store);
    }
    store.EndElement();
  }

  static void SerializeObject(object obj, Type type, SexpWriter store)
  {
    ISerializable iserializable = obj as ISerializable;

    // get the type to write into the store, which may be different from the object type (eg, in the case of a proxy)
    Type typeToSerialize = iserializable == null ? type : iserializable.TypeToSerialize;

    // write the opening element with the type name of the object
    store.BeginElement(GetTypeName(typeToSerialize));

    // call .BeforeSerialize if applicable
    if(iserializable != null)
    {
      SerializationStore customStore = new SerializationStore(store, "@beforeFields");
      iserializable.BeforeSerialize(customStore);
      customStore.Finish();
    }

    // then serialize the object fields
    if(typeToSerialize == type)
    {
      SerializeObjectFields(type, obj, store);
    }

    // then call .Serialize, if applicable.
    if(iserializable != null)
    {
      SerializationStore customStore = new SerializationStore(store, "@afterFields");
      iserializable.Serialize(customStore);
      customStore.Finish();
    }

    store.EndElement(); // finally, write the end element
  }

  static void SerializeObjectFields(Type objectType, object instance, SexpWriter store)
  {
    Dictionary<string,FieldInfo> fieldNames = new Dictionary<string,FieldInfo>();
    store.BeginElement("@fields");
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      SerializeObjectFields(type, instance, store, fieldNames);
    }
    store.EndElement();
  }

  static void SerializeObjectFields(Type type, object instance, SexpWriter store,
                                    Dictionary<string,FieldInfo> fieldNames)
  {
    // get the instance fields (public and private)
    FieldInfo[] fields = type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
    for(int i=0; i<fields.Length; i++)
    {
      FieldInfo fi = fields[i];
      if(ShouldSkipField(fi)) continue;

      store.BeginElement(GetFieldName(fi, fieldNames));
      if(IsSimpleType(fi.FieldType))
      {
        store.WriteContent(GetSimpleValue(fi.GetValue(instance), fi.FieldType));
      }
      else
      {
        Serialize(fi.GetValue(instance), store);
      }
      store.EndElement();
    }
  }

  /// <summary>Gets a string representation of a simple object.</summary>
  static string GetSimpleValue(object value, Type type)
  {
    if(type.IsArray) // if it's an array, represent it as a space-separated list. it will be one-dimensional
    {
      Array array = (Array)value; 
      if(array.Length == 0)
      {
        return string.Empty;
      }
      else
      {
        type = type.GetElementType(); // the element type will be a simple type

        StringBuilder sb = new StringBuilder();
        for(int i=0; i<array.Length; i++)
        {
          if(i != 0) sb.Append(' ');
          sb.Append(GetSimpleValue(array.GetValue(i), type));
        }
        return sb.ToString();
      }
    }
    else if(type == typeof(System.Drawing.Color))
    {
      System.Drawing.Color color = (System.Drawing.Color)value;
      if(color.IsNamedColor)
      {
        return color.Name;
      }
      else
      {
        string rgb = color.R+","+color.G+","+color.B;
        return color.A == 255 ? rgb : rgb+","+color.A.ToString();
      }
    }
    else // handle types representable by TypeCodes
    {
      switch(Type.GetTypeCode(type))
      {
        case TypeCode.Boolean: return (bool)value ? "true" : "false";
        case TypeCode.Byte: return ((byte)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Char: return ((int)(char)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.DateTime:
        {
          // using "u" or "s" format type loses sub-second accuracy, but is much more readable.
          DateTime dt = (DateTime)value;
          if(dt.Kind == DateTimeKind.Utc)
          {
            // we replace space with '@' because space is used as an array element separator
            return dt.ToString("u", CultureInfo.InvariantCulture).Replace(' ', '@');
          }
          else
          {
            return dt.ToString("s", CultureInfo.InvariantCulture);
          }
        }
        case TypeCode.Decimal: return ((decimal)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.Double: return ((double)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.Int16: return ((short)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Int32: return ((int)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Int64: return ((long)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.SByte: return ((sbyte)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Single: return ((float)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.UInt16: return ((ushort)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.UInt32: return ((uint)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.UInt64: return ((ulong)value).ToString(CultureInfo.InvariantCulture);
        default: throw new NotSupportedException("Unsupported type: "+type.FullName);
      }
    }
  }

  /// <summary>Given a type, gets its (possibly abbreviated) type name.</summary>
  static string GetTypeName(Type type)
  {
    switch(Type.GetTypeCode(type))
    {
      case TypeCode.Boolean: return "bool";
      case TypeCode.Byte: return "byte";
      case TypeCode.Char: return "char";
      case TypeCode.DateTime: return "dateTime";
      case TypeCode.Decimal:  return "decimal";
      case TypeCode.Double: return "double";
      case TypeCode.Int16:  return "short";
      case TypeCode.Int32:  return "int";
      case TypeCode.Int64:  return "long";
      case TypeCode.Object:
        if(type == typeof(AdamMil.Mathematics.Geometry.TwoD.Point))
        {
          return "point";
        }
        else if(type == typeof(AdamMil.Mathematics.Geometry.TwoD.Vector))
        {
          return "vector";
        }
        else
        {
          return type.FullName;
        }
      case TypeCode.SByte:  return "sbyte";
      case TypeCode.Single: return "float";
      case TypeCode.String: return "string";
      case TypeCode.UInt16: return "ushort";
      case TypeCode.UInt32: return "uint";
      case TypeCode.UInt64: return "ulong";
      default: throw new NotSupportedException("Unsupported type: "+type.FullName);
    }
  }
  #endregion

  #region Deserialization
  /// <summary>Deserializes an object from a <see cref="Stream"/>.</summary>
  /// <remarks>If you'll be deserializing multiple objects from the stream, you must instead create a
  /// <see cref="SexpReader"/> and call the overload that takes it.
  /// </remarks>
  public static object Deserialize(Stream store)
  {
    return Deserialize(new SexpReader(store));
  }

  /// <summary>Deserializes an object from a <see cref="TextReader"/>.</summary>
  /// <remarks>If you'll be deserializing multiple objects from the text reader, you must instead create a
  /// <see cref="SexpReader"/> and call the overload that takes it.
  /// </remarks>
  public static object Deserialize(TextReader store)
  {
    return Deserialize(new SexpReader(store));
  }

  /// <summary>Deserializes an object from an <see cref="SexpReader"/>.</summary>
  public static object Deserialize(SexpReader store)
  {
    Type type = GetTypeFromName(store.TagName);
    object value;

    store.ReadBeginElement();

    if(type == null)
    {
      value = null;
    }
    else if(IsSimpleType(type))
    {
      value = ParseSimpleValue(store.ReadContent(), type);
    }
    else if(type == typeof(string))
    {
      value = store.ReadContent();
    }
    else if(type.IsArray)
    {
      value = DeserializeArray(type, store);
    }
    else if(typeof(IList).IsAssignableFrom(type))
    {
      value = DeserializeIList(type, store);
    }
    else if(typeof(IDictionary).IsAssignableFrom(type))
    {
      value = DeserializeDictionary(type, store);
    }
    else
    {
      value = DeserializeObject(type, store);
    }

    store.ReadEndElement();
    return value;
  }
  
  /// <summary>Deserializes a complex array from the store.</summary>
  static Array DeserializeArray(Type type, SexpReader store)
  {
    string[] dimString = store.ReadElement("@dimensions").Split(',');
    int[] lengths = new int[dimString.Length];
    for(int i=0; i<lengths.Length; i++)
    {
      lengths[i] = int.Parse(dimString[i], CultureInfo.InvariantCulture);
    }

    Array array = Array.CreateInstance(type.GetElementType(), lengths);

    if(store.NodeType == SexpNodeType.Begin)
    {
      int[] indices = new int[lengths.Length];

      do
      {
        array.SetValue(Deserialize(store), indices);

        for(int i=indices.Length-1; i >= 0; i--)
        {
          if(++indices[i] == lengths[i])
          {
            indices[i] = 0;
          }
          else
          {
            break;
          }
        }
      } while(store.NodeType == SexpNodeType.Begin);
    }

    return array;
  }

  static IDictionary DeserializeDictionary(Type type, SexpReader store)
  {
    IDictionary dict = (IDictionary)ConstructObject(type);
    while(store.NodeType == SexpNodeType.Begin)
    {
      object key   = Deserialize(store);
      object value = Deserialize(store);
      dict.Add(key, value);
    }
    return dict;
  }

  static IList DeserializeIList(Type type, SexpReader store)
  {
    IList list = (IList)ConstructObject(type);
    while(store.NodeType == SexpNodeType.Begin)
    {
      list.Add(Deserialize(store));
    }
    return list;
  }

  /// <summary>Deserializes a class or struct from the store.</summary>
  static object DeserializeObject(Type type, SexpReader store)
  {
    object obj = ConstructObject(type);
    ISerializable iserializable = obj as ISerializable;

    if(iserializable != null) // call BeforeDeserialize if appropriate
    {
      DeserializationStore customStore = new DeserializationStore(store, "@beforeFields");
      iserializable.BeforeDeserialize(customStore);
      customStore.Finish();
    }
    // deserialize fields if there are any
    if(store.NodeType == SexpNodeType.Begin && string.Equals(store.TagName, "@fields", StringComparison.Ordinal))
    {
      store.ReadBeginElement();
      DeserializeObjectFields(type, obj, store);
      store.ReadEndElement(); // read the end of the @fields node
    }
    if(iserializable != null) // call Deserialize if appropriate
    {
      DeserializationStore customStore = new DeserializationStore(store, "@afterFields");
      iserializable.Deserialize(customStore);
      customStore.Finish();
    }

    // finally, return the object, unless it's an object reference, in which case we'll use it to get the real object
    IObjectReference objRef = obj as IObjectReference;
    return objRef != null ? objRef.GetRealObject() : obj;
  }

  static void DeserializeObjectFields(Type objectType, object instance, SexpReader store)
  {
    Dictionary<string,FieldInfo> fields = new Dictionary<string,FieldInfo>();
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      // get the instance fields (public and private)
      foreach(FieldInfo fi in type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.DeclaredOnly))
      {
        if(!ShouldSkipField(fi))
        {
          GetFieldName(fi, fields);
        }
      }
    }

    while(store.NodeType == SexpNodeType.Begin)
    {
      FieldInfo fi;
      if(fields.TryGetValue(store.TagName, out fi))
      {
        store.ReadBeginElement();
        object fieldValue;
        if(IsSimpleType(fi.FieldType))
        {
          fieldValue = ParseSimpleValue(store.ReadContent(), fi.FieldType);
        }
        else
        {
          fieldValue = Deserialize(store);
        }
        store.ReadEndElement();

        fi.SetValue(instance, fieldValue);
      }
      else
      {
        store.Skip();
      }
    }
  }

  static object ConstructObject(Type type)
  {
    ConstructorInfo ci;

    // first see if the object has the "special" deserialization constructor (which takes a dummy ISerializable)
    ci = type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                             null, iserializableTypeArray, null);
    if(ci != null)
    {
      return ci.Invoke(new object[1] { null });
    }

    if(type.IsValueType) // if it's a value type, it won't have a parameterless constructor...
    {
      return Activator.CreateInstance(type); // ... but we can use the Activator class to do it
    }
    else // otherwise, see if it has a parameterless constructor
    {
      ci = type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                               null, Type.EmptyTypes, null);
      if(ci != null)
      {
        return ci.Invoke(null);
      }
    }

    throw new ArgumentException("Unable to construct object of type "+type.FullName+
                                " because it has no suitable constructor.");
  }

  /// <summary>Parses a simple value from a string, given its type.</summary>
  static object ParseSimpleValue(string text, Type type)
  {
    if(type.IsArray) // if it's an array, the text will be a space separated list
    {
      Type elementType = type.GetElementType();
      string[] values = text.Split(' ');
      Array array = Array.CreateInstance(elementType, values.Length);
      for(int i=0; i<values.Length; i++)
      {
        array.SetValue(ParseSimpleValue(values[i], elementType), i);
      }
      return array;
    }
    else if(type == typeof(System.Drawing.Color))
    {
      // special casing colors this way loses a bit of information because the Color object stores the color's name
      // as well. so Color.Red != Color.FromArgb(255, 0, 0), even though they'll render the same. this is acceptable.
      if(text.IndexOf(',') == -1)
      {
        return System.Drawing.Color.FromName(text);
      }
      else
      {
        string[] values = text.Split(',');
        return System.Drawing.Color.FromArgb(values.Length == 4 ? int.Parse(values[3]) : 255,
                                             int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
      }
    }
    else
    {
      switch(Type.GetTypeCode(type))
      {
        case TypeCode.Boolean: return text.ToLower() == "true";
        case TypeCode.Byte: return byte.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Char: return (char)int.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.DateTime:
          // spaces were converted to '@' in GetSimpleValue because spaces are used to separate array elements.
          // convert them back here.
          return DateTime.Parse(text.Replace('@', ' '), CultureInfo.InvariantCulture);
        case TypeCode.Decimal: return decimal.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Double: return double.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Int16: return short.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Int32: return int.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Int64: return long.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.SByte: return sbyte.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Single: return float.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.UInt16: return ushort.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.UInt32: return uint.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.UInt64: return ulong.Parse(text, CultureInfo.InvariantCulture);
        default: throw new NotSupportedException("Unsupported type: "+type.FullName);
      }
    }
  }

  /// <summary>Return a <see cref="Type"/> from a potentially-abbreviated type name.</summary>
  static Type GetTypeFromName(string name)
  {
    Type type;

    if(typeDict.TryGetValue(name, out type)) // see if it's one of our short names
    {
      return type;
    }
    
    type = Type.GetType(name); // see if it's a type defined in this assembly
    if(type != null)
    {
      return type;
    }

    foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()) // see if it's a type defined in any loaded assembly
    {
      type = a.GetType(name);
      if(type != null)
      {
        return type;
      }
    }

    throw new TypeLoadException("Unable to find type: "+name);
  }
  #endregion

  static string GetFieldName(FieldInfo fi, Dictionary<string,FieldInfo> fieldNames)
  {
    string testName = fi.Name;
    int which = 2; // start disambiguation by appending "2" to the name, then proceed to "3", etc.

    while(fieldNames.ContainsKey(testName))
    {
      testName = fi.Name + which.ToString(CultureInfo.InvariantCulture);
      which++;
    }
    
    fieldNames[testName] = fi;
    return testName;
  }

  static List<Type> GetTypeHierarchy(Type type)
  {
    List<Type> hierarchy = new List<Type>();
    do
    {
      hierarchy.Add(type);
      type = type.BaseType;
    } while(type != null);
    hierarchy.Reverse();
    return hierarchy;
  }

  /// <summary>Determines if a type is simple enough that it can be represented in an attribute string.</summary>
  static bool IsSimpleType(Type type)
  {
    if(type.IsPrimitive || type.IsEnum || type == typeof(DateTime) || type == typeof(System.Drawing.Color))
    {
      return true; // numerics, chars, enums, dates, and colors are simple.
    }
    else if(type.IsArray && type.GetArrayRank() == 1) // if it's a one-dimensional array ...
    {
      Type subType = type.GetElementType(); // ... of a non-array simple type, then it's simple
      return !subType.IsArray && IsSimpleType(subType);
    }
    else // otherwise, it's not simple
    {
      return false;
    }
  }
  
  static bool ShouldSkipField(FieldInfo fi)
  {
    return fi.IsNotSerialized || fi.IsLiteral || fi.IsInitOnly || typeof(Delegate).IsAssignableFrom(fi.FieldType);
  }
  
  static readonly Dictionary<string,Type> typeDict;
  static readonly Type[] iserializableTypeArray = new Type[1] { typeof(ISerializable) };
  static bool inBatch;
}
#endregion

} // namespace RotationalForce.Engine