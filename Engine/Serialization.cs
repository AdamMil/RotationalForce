using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace RotationalForce.Engine
{

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
    AddToPool(id, this);
  }

  void ISerializable.BeforeDeserialize(DeserializationStore store)
  {
    uint serializedId = store.GetUint32("UniqueObject.ID");
    AddToPool(serializedId, this);
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
  
  uint id;

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

  static void AddToPool(uint id, UniqueObject obj)
  {
    if(objectPool != null)
    {
      objectPool.Add(id, obj);
    }
  }

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
    id = store.GetUint32("UniqueObject:ID");
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


#region SerializationStore
public class SerializationStore
{
  internal SerializationStore(XmlWriter writer, string defaultNamespace)
  {
    if(writer == null) throw new ArgumentNullException();
    this.writer = writer;
    this.ns     = defaultNamespace;
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
      writer.WriteStartElement(ns);
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

    writer.WriteStartElement(XmlConvert.EncodeLocalName(name));
    Serializer.InnerSerialize(value, writer);
    writer.WriteEndElement();
  }

  /// <summary>Finishes writing the value store.</summary>
  internal void Finish()
  {
    if(valueAdded)
    {
      writer.WriteEndElement();
    }
  }

  XmlWriter writer;
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
  internal DeserializationStore(XmlReader reader)
  {
    if(reader == null) throw new ArgumentNullException("reader");
    this.reader = reader;
    this.ns     = reader.LocalName;

    // it's possible that the reader has no namespace node, in which case, there are no values to read.
    // but if it does have a namespace node, move past it.
    hasNamespaceNode = reader.NodeType == XmlNodeType.Element;
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
    while(reader.NodeType == XmlNodeType.Element) // while we're not at the end of the data yet
    {
      string nodeName = XmlConvert.DecodeName(reader.LocalName);

      reader.Read(); // read to the data element
      value = Serializer.InnerDeserialize(reader);
      Serializer.ReadEndElement(reader); // read the closing data element

      // prepend the new object to the list (where it can be found quickest)
      values.AddFirst(new KeyValuePair<string,object>(nodeName, value));

      reader.Read(); // consume the closing variable name node, advancing to the next node, or the end of the data

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
      while(reader.NodeType == XmlNodeType.Element) // skip all data nodes
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

  XmlReader reader;
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

    typeDict["point"]    = typeof(GameLib.Mathematics.TwoD.Point);
    typeDict["vector"]   = typeof(GameLib.Mathematics.TwoD.Vector);
  }

  /// <summary>Resets the pool of known objects. This should be called before each block of
  /// serialization/deserializations that need <see cref="UniqueObject"/> pointers to be pooled. This is necessary to
  /// properly handle circular pointers and multiple pointers to the same object. Failing to call this can cause future
  /// serializations/deserializations to fail.
  /// </summary>
  public static void BeginBatch()
  {
    UniqueObject.ResetObjectPool();
  }

  /// <summary>Resets the pool of known objects. This should be called before each block of
  /// serialization/deserializations that need <see cref="UniqueObject"/> pointers to be pooled. This is necessary to
  /// properly handle circular pointers and multiple pointers to the same object. Failing to call this can cause
  /// objects to hang around in memory and never be deleted, and can cause future serializations/deserializations
  /// to fail.
  /// </summary>
  public static void EndBatch()
  {
    UniqueObject.ResetObjectPool();
  }

  #region Serialization
  public static XmlWriter CreateXmlWriter(Stream store, bool allowMultipleObjects)
  {
    return CreateXmlWriter(new StreamWriter(store), allowMultipleObjects);
  }

  public static XmlWriter CreateXmlWriter(TextWriter store, bool allowMultipleObjects)
  {
    XmlWriterSettings settings = new XmlWriterSettings();
    settings.CheckCharacters = false;
    settings.NewLineHandling = NewLineHandling.Entitize;
    if(allowMultipleObjects)
    {
      settings.ConformanceLevel = ConformanceLevel.Fragment;
    }
    return XmlWriter.Create(store, settings);
  }

  /// <summary>Serializes an object into the given <see cref="Stream"/>. The object can be null.</summary>
  public static void Serialize(object obj, Stream store)
  {
    Serialize(obj, CreateXmlWriter(store, false));
  }

  /// <summary>Serializes an object into the given <see cref="TextWriter"/>. The object can be null.</summary>
  public static void Serialize(object obj, TextWriter store)
  {
    Serialize(obj, CreateXmlWriter(store, false));
  }

  /// <summary>Serializes an object into the given <see cref="XmlWriter"/>. The object can be null.</summary>
  public static void Serialize(object obj, XmlWriter store)
  {
    if(store == null) throw new ArgumentNullException("store");
    InnerSerialize(obj, store);
    store.Flush();
  }

  /// <summary>Serializes an object into the given <see cref="XmlWriter"/> but without flushing the written data to the
  /// underlying store.
  /// </summary>
  internal static void InnerSerialize(object obj, XmlWriter store)
  {
    Type type = obj == null ? null : obj.GetType(); // get the type of the object

    if(obj == null) // if it's a null object, just write <null/>
    {
      store.WriteStartElement("null");
      store.WriteEndElement();
    }
    else if(IsSimpleType(type)) // otherwise, if it's a simple type, write out the simple type (eg, <int>5</int>)
    {
      store.WriteElementString(XmlConvert.EncodeLocalName(GetTypeName(type)), GetSimpleValue(obj, type));
    }
    else if(type == typeof(string))
    {
      store.WriteElementString(XmlConvert.EncodeLocalName(GetTypeName(type)), (string)obj);
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

  static void SerializeArray(Array array, Type type, XmlWriter store)
  {
    store.WriteStartElement(XmlConvert.EncodeLocalName(type.FullName)); // write the array's typename

    // write the dimensions attribute (this doesn't handle non-zero-bounded arrays)
    // a 2x5 array will have dimensions="2,5"
    store.WriteStartAttribute("dimensions");
    for(int i=0; i<array.Rank; i++)
    {
      if(array.GetLowerBound(i) != 0)
      {
        throw new NotImplementedException("Non-zero bound arrays are not implemented.");
      }

      if(i != 0) store.WriteRaw(",");
      store.WriteRaw(array.GetLength(i).ToString(CultureInfo.InvariantCulture));
    }
    store.WriteEndAttribute();

    foreach(object element in array) // serialize the elements themselves
    {
      InnerSerialize(element, store);
    }

    store.WriteEndElement(); // close the array attribute
  }
  
  static void SerializeDictionary(IDictionary dict, Type type, XmlWriter store)
  {
    SerializeTypeTag(type, store);
    
    foreach(DictionaryEntry de in dict)
    {
      InnerSerialize(de.Key, store);
      InnerSerialize(de.Value, store);
    }

    store.WriteEndElement();
  }
  
  static void SerializeIList(IList list, Type type, XmlWriter store)
  {
    SerializeTypeTag(type, store);
    foreach(object o in list)
    {
      InnerSerialize(o, store);
    }
    store.WriteEndElement();
  }

  static void SerializeObject(object obj, Type type, XmlWriter store)
  {
    ISerializable iserializable = obj as ISerializable;

    // get the type to write into the store, which may be different from the object type (eg, in the case of a proxy)
    Type typeToSerialize = iserializable == null ? type : iserializable.TypeToSerialize;

    // write the opening element with the type name of the object
    SerializeTypeTag(typeToSerialize, store);

    if(typeToSerialize != type) // if the type is different, add an attribute indicating that
    {
      store.WriteAttributeString(XmlConvert.EncodeLocalName(".isProxy"), "true");
    }
    else // otherwise, write out the fields. (if the type is different we can't write out the fields)
    {
      SerializeSimpleObjectFields(type, obj, store);
    }

    // call .BeforeSerialize if applicable
    if(iserializable != null)
    {
      SerializationStore customStore = new SerializationStore(store, XmlConvert.EncodeLocalName(".BeforeFields"));
      iserializable.BeforeSerialize(customStore);
      customStore.Finish();
    }

    // then serialize the complex fields
    if(typeToSerialize == type)
    {
      SerializeComplexObjectFields(type, obj, store);
    }

    // then call .Serialize, if applicable.
    if(iserializable != null)
    {
      SerializationStore customStore = new SerializationStore(store, XmlConvert.EncodeLocalName(".CustomValues"));
      iserializable.Serialize(customStore);
      customStore.Finish();
    }

    store.WriteEndElement(); // finally, write the end element
  }

  static void SerializeSimpleObjectFields(Type objectType, object instance, XmlWriter store)
  {
    Dictionary<string,object> fieldNames = new Dictionary<string,object>();
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      SerializeSimpleObjectFields(type, instance, store, fieldNames);
    }
  }

  static void SerializeComplexObjectFields(Type objectType, object instance, XmlWriter store)
  {
    Dictionary<string,object> fieldNames = new Dictionary<string,object>();
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      SerializeComplexObjectFields(type, instance, store, fieldNames);
    }
  }

  static void SerializeComplexObjectFields(Type type, object instance, XmlWriter store,
                                           Dictionary<string,object> fieldNames)
  {
    // get the instance fields (public and private)
    FieldInfo[] fields = type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
    for(int i=0; i<fields.Length; i++)
    {
      FieldInfo fi = fields[i];
      if(!ShouldSkipField(fi) && !IsSimpleType(fi.FieldType))
      {
        store.WriteStartElement(GetFieldName(fi.Name, fieldNames));
        InnerSerialize(fi.GetValue(instance), store);
        store.WriteEndElement();
      }
    }
  }

  static void SerializeSimpleObjectFields(Type type, object instance, XmlWriter store,
                                          Dictionary<string,object> fieldNames)
  {
    // get the instance fields (public and private)
    FieldInfo[] fields = type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
    // loop through fields and write the simple ones as attributes.
    for(int i=0; i<fields.Length; i++)
    {
      FieldInfo fi = fields[i];
      if(!ShouldSkipField(fi) && IsSimpleType(fi.FieldType))
      {
        store.WriteAttributeString(GetFieldName(fi.Name, fieldNames),
                                   GetSimpleValue(fi.GetValue(instance), fi.FieldType));
      }
    }
  }

  /// <summary>Gets a string representation of a simple object.</summary>
  static string GetSimpleValue(object value, Type type)
  {
    if(type.IsArray) // if it's an array, represent it as a comma-separated list. it will be one-dimensional
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
          if(i != 0) sb.Append(',');
          sb.Append(GetSimpleValue(array.GetValue(i), type));
        }

        return sb.ToString();
      }
    }
    else if(type == typeof(System.Drawing.Color))
    {
      System.Drawing.Color color = (System.Drawing.Color)value;
      string rgb = color.R+","+color.G+","+color.B;
      return color.A == 255 ? rgb : rgb+","+color.A.ToString();
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
          // using "u" or "s" format type loses sub-second accuracy, but is much more readable
          DateTime dt = (DateTime)value;
          return dt.ToString(dt.Kind == DateTimeKind.Utc ? "u" : "s", CultureInfo.InvariantCulture);
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

  static string GetTypeName(Type type)
  {
    bool abbreviated;
    return GetTypeName(type, out abbreviated);
  }

  /// <summary>Given a type, gets its (possibly abbreviated) type name.</summary>
  static string GetTypeName(Type type, out bool abbreviated)
  {
    abbreviated = true;
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
        if(type == typeof(GameLib.Mathematics.TwoD.Point))
        {
          return "point";
        }
        else if(type == typeof(GameLib.Mathematics.TwoD.Vector))
        {
          return "vector";
        }
        else
        {
          abbreviated = false;
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
  
  static void SerializeTypeTag(Type type, XmlWriter store)
  {
    bool abbreviated;
    string typeName = GetTypeName(type, out abbreviated);
    if(abbreviated)
    {
      store.WriteStartElement(typeName);
    }
    else
    {
      store.WriteStartElement("object");
      store.WriteAttributeString(XmlConvert.EncodeLocalName(".type"), typeName);
    }
  }
  #endregion

  #region Deserialization
  public static XmlReader CreateXmlReader(Stream store, bool allowMultipleObjects)
  {
    return CreateXmlReader(new StreamReader(store), allowMultipleObjects);
  }

  public static XmlReader CreateXmlReader(TextReader store, bool allowMultipleObjects)
  {
    XmlReaderSettings settings = new XmlReaderSettings();
    settings.CheckCharacters  = false;
    settings.IgnoreComments   = true;
    settings.IgnoreWhitespace = true;
    if(allowMultipleObjects)
    {
      settings.ConformanceLevel = ConformanceLevel.Fragment;
    }
    return XmlReader.Create(store, settings);
  }

  /// <summary>Deserializes an object from a <see cref="Stream"/>.</summary>
  public static object Deserialize(Stream store)
  {
    return Deserialize(CreateXmlReader(store, false));
  }

  /// <summary>Deserializes an object from a <see cref="TextReader"/>.</summary>
  public static object Deserialize(TextReader store)
  {
    return Deserialize(CreateXmlReader(store, false));
  }

  /// <summary>Deserializes an object from an <see cref="XmlReader"/>.</summary>
  public static object Deserialize(XmlReader store)
  {
    if(store.NodeType == XmlNodeType.None) // if no data has been read yet...
    {
      store.Read(); // ... position the reader on the first node
    }
    if(store.NodeType == XmlNodeType.XmlDeclaration) // if it's an xml declaration (<?xml ... ?>), skip it
    {
      store.Read();
    }

    object value = InnerDeserialize(store); // deserialize the root object
    ReadEndElement(store); // and consume the end node
    return value; // return the object
  }
  
  /// <summary>Deserializes an object from an <see cref="XmlReader"/>, assuming that the reader is positioned at the
  /// start node. The function will not consume the end node.
  /// </summary>
  internal static object InnerDeserialize(XmlReader store)
  {
    string typeName = store.LocalName;
    if(string.Equals(typeName, "object", StringComparison.Ordinal))
    {
      typeName = store.GetAttribute(XmlConvert.EncodeLocalName(".type"));
    }

    Type type = GetTypeFromName(XmlConvert.DecodeName(typeName));

    if(type == null)
    {
      return null;
    }
    else if(IsSimpleType(type))
    {
      return ParseSimpleValue(store.ReadString(), type);
    }
    else if(type == typeof(string))
    {
      return store.ReadString();
    }
    else if(type.IsArray)
    {
      return DeserializeArray(type, store);
    }
    else if(typeof(IList).IsAssignableFrom(type))
    {
      return DeserializeIList(type, store);
    }
    else if(typeof(IDictionary).IsAssignableFrom(type))
    {
      return DeserializeDictionary(type, store);
    }
    else
    {
      return DeserializeObject(type, store);
    }
  }
  
  /// <summary>Deserializes a complex array from the store.</summary>
  static Array DeserializeArray(Type type, XmlReader store)
  {
    string[] dimString = store.GetAttribute("dimensions").Split(',');

    int[] lengths = new int[dimString.Length];
    for(int i=0; i<lengths.Length; i++)
    {
      lengths[i] = int.Parse(dimString[i], CultureInfo.InvariantCulture);
    }

    Array array = Array.CreateInstance(type.GetElementType(), lengths);

    if(!store.IsEmptyElement)
    {
      int[] indices = new int[lengths.Length];

      while(store.Read() && store.NodeType == XmlNodeType.Element)
      {
        array.SetValue(InnerDeserialize(store), indices);

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
      }
    }

    return array;
  }

  static IDictionary DeserializeDictionary(Type type, XmlReader store)
  {
    IDictionary dict = (IDictionary)ConstructObject(type);
    
    if(!store.IsEmptyElement)
    {
      store.Read(); // advance to first item, or the end tag
      while(store.NodeType == XmlNodeType.Element)
      {
        object key = InnerDeserialize(store);
        store.Read(); // advance past key
        object value = InnerDeserialize(store);
        store.Read(); // advance past value to next item, or the end tag
        dict.Add(key, value);
      }
    }
    
    return dict;
  }

  static IList DeserializeIList(Type type, XmlReader store)
  {
    IList list = (IList)ConstructObject(type);
    
    if(!store.IsEmptyElement)
    {
      store.Read(); // advance to first item, or the end tag
      while(store.NodeType == XmlNodeType.Element)
      {
        list.Add(InnerDeserialize(store));
        store.Read(); // advance to next item, or the end tag
      }
    }
    
    return list;
  }

  /// <summary>Deserializes a class or struct from the store.</summary>
  static object DeserializeObject(Type type, XmlReader store)
  {
    object  obj  = ConstructObject(type);
    string attr  = store.GetAttribute(XmlConvert.EncodeLocalName(".isProxy"));
    bool isProxy = !string.IsNullOrEmpty(attr) && XmlConvert.ToBoolean(attr);
    ISerializable iserializable = obj as ISerializable;

    if(isProxy) // if it's a proxy, there won't be any fields to deserialize, although there may be custom values
    {
      if(!store.IsEmptyElement) store.Read(); // advance to "CustomValues" or the end tag
    }
    else // otherwise, deserialize the attribute fields
    {
      DeserializeSimpleObjectFields(type, obj, store);
    }

    CallDeserialize(iserializable, store, ".BeforeFields", true);
    DeserializeComplexObjectFields(type, obj, store);
    CallDeserialize(iserializable, store, ".CustomValues", false);

    // finally, return the object, unless it's an object reference, in which case we'll use it to get the real object
    IObjectReference objRef = obj as IObjectReference;
    return objRef != null ? objRef.GetRealObject() : obj;
  }

  static void CallDeserialize(ISerializable iserializable, XmlReader store, string fieldName, bool beforeDeserialize)
  {
    // call BeforeDeserialize or Deserialize if applicable
    if(iserializable != null)
    {
      if(store.NodeType == XmlNodeType.Element &&
         !string.Equals(XmlConvert.DecodeName(store.LocalName), fieldName, StringComparison.Ordinal))
      {
        throw new ArgumentException("Unexpected element '"+store.LocalName+"'");
      }

      DeserializationStore customStore = new DeserializationStore(store);
      if(beforeDeserialize)
      {
        iserializable.BeforeDeserialize(customStore);
      }
      else
      {
        iserializable.Deserialize(customStore);
      }
      customStore.Finish();
    }
  }

  static void DeserializeSimpleObjectFields(Type objectType, object instance, XmlReader store)
  {
    Dictionary<string, object> fieldNames = new Dictionary<string, object>();
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      DeserializeSimpleObjectFields(type, instance, store, fieldNames);
    }

    if(!store.IsEmptyElement) store.Read(); // read inside the open tag
  }

  static void DeserializeComplexObjectFields(Type objectType, object instance, XmlReader store)
  {
    Dictionary<string, object> fieldNames = new Dictionary<string, object>();
    foreach(Type type in GetTypeHierarchy(objectType))
    {
      DeserializeComplexObjectFields(type, instance, store, fieldNames);
    }
  }

  static void DeserializeComplexObjectFields(Type type, object instance, XmlReader store,
                                             Dictionary<string,object> fieldNames)
  {
    // get the instance fields (public and private)
    FieldInfo[] fields = type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
    for(int i=0; i<fields.Length; i++)
    {
      FieldInfo fi = fields[i];
      if(!ShouldSkipField(fi) && !IsSimpleType(fi.FieldType))
      {
        string fieldName = GetFieldName(fi.Name, fieldNames);
        if(!string.Equals(store.LocalName, fieldName, StringComparison.Ordinal))
        {
          throw new ArgumentException("Expected node: " + fi.Name);
        }
        store.Read(); // read the data element
        fi.SetValue(instance, InnerDeserialize(store));
        ReadEndElement(store); // move past the the closing data element
        ReadEndElement(store); // move past the the closing field name element
      }
    }
  }

  static void DeserializeSimpleObjectFields(Type type, object instance, XmlReader store,
                                            Dictionary<string,object> fieldNames)
  {
    // first, loop through fields and write the simple ones as attributes.
    FieldInfo[] fields = type.GetFields(BindingFlags.Public   | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
    for(int i=0; i<fields.Length; i++)
    {
      FieldInfo fi = fields[i];
      if(!ShouldSkipField(fi) && IsSimpleType(fi.FieldType))
      {
        fi.SetValue(instance, ParseSimpleValue(store.GetAttribute(GetFieldName(fi.Name, fieldNames)), fi.FieldType));
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
    if(type.IsArray) // if it's an array, the text will be a comma separated list
    {
      Type elementType = type.GetElementType();
      string[] values = text.Split(',');
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
      string[] values = text.Split(',');
      return System.Drawing.Color.FromArgb(values.Length == 4 ? int.Parse(values[3]) : 255,
                                           int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
    }
    else
    {
      switch(Type.GetTypeCode(type))
      {
        case TypeCode.Boolean: return XmlConvert.ToBoolean(text);
        case TypeCode.Byte: return byte.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.Char: return (char)int.Parse(text, CultureInfo.InvariantCulture);
        case TypeCode.DateTime: return DateTime.Parse(text, CultureInfo.InvariantCulture);
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
  
  internal static void ReadEndElement(XmlReader store)
  {
    if(store.IsEmptyElement)
    {
      store.Read();
    }
    else
    {
      store.ReadEndElement();
    }
  }
  #endregion

  static string GetFieldName(string name, Dictionary<string,object> fieldNames)
  {
    string testName = name;
    int which = 2;

    while(fieldNames.ContainsKey(testName))
    {
      testName = name + which.ToString(CultureInfo.InvariantCulture);
      which++;
    }
    
    fieldNames[testName] = null;
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
      Type subType = type.GetElementType(); // ... of a non-array, non-color simple type, then it's simple
      return !subType.IsArray && subType != typeof(System.Drawing.Color) && IsSimpleType(subType);
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
}
#endregion

} // namespace RotationalForce.Engine