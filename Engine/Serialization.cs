using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace RotationalForce.Engine
{

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

    writer.WriteStartElement(name);
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

    reader.Read();
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
    if(values == null) // if we've read no values, initialize the value dictionary
    {
      values = new LinkedList<KeyValuePair<string, object>>();
    }
    else // otherwise, check if we've read this value already.
    {
      if(TryGetCachedValue(name, out value)) // if so, return success
      {
        return true;
      }
    }

    // otherwise, we haven't read it yet, so read until we find it.
    while(reader.NodeType != XmlNodeType.EndElement) // while we're not at the end of the data yet
    {
      string nodeName = reader.LocalName;

      reader.Read(); // read to the data element
      value = Serializer.InnerDeserialize(reader);
      reader.ReadEndElement(); // read the closing data element

      // prepend the new object to the list (where it can be found quickest)
      values.AddFirst(new KeyValuePair<string, object>(nodeName, value));

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
    // skip all data nodes until we find the closing namespace node
    while(!string.Equals(reader.LocalName, ns, StringComparison.Ordinal) && reader.NodeType != XmlNodeType.EndElement)
    {
      reader.Skip();
    }
    reader.ReadEndElement(); // then consume the closing namespace node
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

  /// <summary>Gives the object a chance to add additional serialization data.</summary>
  /// <remarks>Note that as long as <see cref="TypeToSerialize"/> returns a value equal to
  /// <see cref="System.Object.GetType()"/>, the object's fields will automatically be serialized and deserialized, and
  /// don't need to be added manually.
  /// </remarks>
  void SerializeCustomValues(SerializationStore store);

  /// <summary>Provides the additonal serialization data added by <see cref="SerializeCustomValues"/>.</summary>
  /// <remarks>Note that if <see cref="SerializeCustomValues"/> does not add any data, this method will not be called.</remarks>
  void DeserializeCustomValues(DeserializationStore store);
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
///   void ISerializable.SerializeCustomValues(SerializationStore store) { }
///   void ISerializable.DeserializeCustomValues(DeserializationStore store) { }
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
    typeDict = new Dictionary<string,Type>(16);
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
  }

  #region Serialization
  /// <summary>Serializes an object into the given <see cref="Stream"/>. The object can be null.</summary>
  public static void Serialize(object obj, Stream store)
  {
    Serialize(obj, new StreamWriter(store));
  }

  /// <summary>Serializes an object into the given <see cref="TextWriter"/>. The object can be null.</summary>
  public static void Serialize(object obj, TextWriter store)
  {
    XmlWriterSettings settings = new XmlWriterSettings();
    settings.CheckCharacters = false;
    settings.NewLineHandling = NewLineHandling.Entitize;
    Serialize(obj, XmlWriter.Create(store, settings));
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
    else if(type.IsArray) // otherwise, if it's a complex array (simple arrays are handled by GetSimpleValue)
    {
      SerializeArray((Array)obj, type, store);
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

  static void SerializeObject(object obj, Type type, XmlWriter store)
  {
    ISerializable iserializable = obj as ISerializable;

    // get the type to write into the store, which may be different from the object type (eg, in the case of a proxy)
    Type typeToSerialize = iserializable == null ? type : iserializable.TypeToSerialize;

    // write the opening element with the type name of the object
    store.WriteStartElement(XmlConvert.EncodeLocalName(GetTypeName(typeToSerialize)));

    if(typeToSerialize != type) // if the type is different, add an attribute indicating that
    {
      store.WriteAttributeString(XmlConvert.EncodeLocalName(".isProxy"), "true");
    }
    else // otherwise, write out the fields. (if the type is different we can't write out the fields)
    {
      // get the instance fields (public and private)
      FieldInfo[] fields = type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);

      // loop through fields and write the simple ones as attributes.
      int numFields = fields.Length;
      for(int i=0; i<numFields; i++)
      {
        FieldInfo fi = fields[i];
        if(!fi.IsNotSerialized && !IsSimpleType(fi.FieldType))
        {
          // if it's a serializable, non-simple field, skip it for now (we'll handle those later)
          continue;
        }

        if(!fi.IsNotSerialized) // if it's a simple type...
        {
          store.WriteAttributeString(fi.Name, GetSimpleValue(fi.GetValue(obj), fi.FieldType));
        }

        fields[i--] = fields[--numFields]; // remove this element since we've handled it already
      }

      // then, write the non-simple ones as elements. this doesn't handle circular references.
      for(int i=0; i<numFields; i++)
      {
        FieldInfo fi = fields[i];
        store.WriteStartElement(fi.Name);
        InnerSerialize(fi.GetValue(obj), store);
        store.WriteEndElement();
      }
    }

    // then handle ISerializable's custom fields, which may add new elements.
    if(iserializable != null)
    {
      SerializationStore customStore = new SerializationStore(store, "CustomValues");
      iserializable.SerializeCustomValues(customStore);
      customStore.Finish();
    }

    store.WriteEndElement(); // finally, write the end element
  }

  /// <summary>Gets a string representation of a simple object.</summary>
  static string GetSimpleValue(object value, Type type)
  {
    if(type.IsEnum)
    {
      return Enum.GetName(type, value); // represent enum values by name
    }
    else if(type.IsArray) // if it's an array, represent it as a comma-separated list. it will be one-dimensional
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
    else // handle types representable by TypeCodes
    {
      switch(Type.GetTypeCode(type))
      {
        case TypeCode.Boolean: return (bool)value ? "true" : "false";
        case TypeCode.Byte: return ((byte)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Char: return ((int)(char)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.DateTime:
          // using "u" format type loses sub-second accuracy, but is much more readable
          return ((DateTime)value).ToString("u", CultureInfo.InvariantCulture);
        case TypeCode.Decimal: return ((decimal)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.Double: return ((double)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.Int16: return ((short)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Int32: return ((int)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Int64: return ((long)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.SByte: return ((sbyte)value).ToString(CultureInfo.InvariantCulture);
        case TypeCode.Single: return ((float)value).ToString("R", CultureInfo.InvariantCulture);
        case TypeCode.String: return (string)value;
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
    if(type.IsEnum)
    {
      return type.FullName; // enums have numeric typecodes, so special case them here
    }
    else
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
        case TypeCode.Object: return type.FullName;
        case TypeCode.SByte:  return "sbyte";
        case TypeCode.Single: return "float";
        case TypeCode.String: return "string";
        case TypeCode.UInt16: return "ushort";
        case TypeCode.UInt32: return "uint";
        case TypeCode.UInt64: return "ulong";
        default: throw new NotSupportedException("Unsupported type: "+type.FullName);
      }
    }
  }
  #endregion

  #region Deserialization
  /// <summary>Deserializes an object from a <see cref="Stream"/>.</summary>
  public static object Deserialize(Stream store)
  {
    return Deserialize(new StreamReader(store));
  }

  /// <summary>Deserializes an object from a <see cref="TextReader"/>.</summary>
  public static object Deserialize(TextReader store)
  {
    XmlReaderSettings settings = new XmlReaderSettings();
    settings.CheckCharacters = false;
    settings.IgnoreComments  = true;
    return Deserialize(XmlReader.Create(store, settings));
  }

  /// <summary>Deserializes an object from an <see cref="XmlReader"/>.</summary>
  public static object Deserialize(XmlReader store)
  {
    store.Read(); // position the reader on the first node
    if(store.NodeType == XmlNodeType.XmlDeclaration) // if it's an xml declaration (<?xml ... ?>), skip it
    {
      store.Read();
    }

    object value = InnerDeserialize(store); // deserialize the root object
    store.ReadEndElement(); // and consume the end node
    return value; // return the object
  }
  
  /// <summary>Deserializes an object from an <see cref="XmlReader"/>, assuming that the reader is positioned at the
  /// start node. The function will not consume the end node.
  /// </summary>
  internal static object InnerDeserialize(XmlReader store)
  {
    Type type = GetTypeFromName(XmlConvert.DecodeName(store.LocalName));

    if(type == null)
    {
      return null;
    }
    else if(IsSimpleType(type))
    {
      return ParseSimpleValue(store.ReadString(), type);
    }
    else if(type.IsArray)
    {
      return DeserializeArray(type, store);
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

      while(store.Read() && store.NodeType != XmlNodeType.EndElement)
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

  /// <summary>Deserializes a class or struct from the store.</summary>
  static object DeserializeObject(Type type, XmlReader store)
  {
    ConstructorInfo ci = type.GetConstructor(Type.EmptyTypes);
    if(ci == null)
    {
      throw new ArgumentException("Unable to construct object of type "+type.FullName+
                                    " because it has no parameterless constructor.");
    }

    object obj = ci.Invoke(null);

    string attr = store.GetAttribute(XmlConvert.EncodeLocalName(".isProxy"));
    bool isProxy = !string.IsNullOrEmpty(attr) && XmlConvert.ToBoolean(attr);

    if(isProxy) // if it's a proxy, there won't be any fields to deserialize, although there may be custom values
    {
      store.Read(); // advance to "CustomValues" or the end tag
    }
    else // otherwise, deserialize the fields (this will 
    {
      // first, loop through fields and write the simple ones as attributes.
      FieldInfo[] fields = type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
      int numFields = fields.Length;
      for(int i=0; i<numFields; i++)
      {
        FieldInfo fi = fields[i];
        if(!fi.IsNotSerialized && !IsSimpleType(fi.FieldType))
        {
          // if it's a serializable, non-simple field, skip it for now
          continue;
        }

        if(!fi.IsNotSerialized) // if it's a simple type...
        {
          fi.SetValue(obj, ParseSimpleValue(store.GetAttribute(fi.Name), fi.FieldType));
        }

        fields[i--] = fields[--numFields]; // remove this element since we've handled it already
      }

      // then, read the non-simple ones as elements
      for(int i=0; i<numFields; i++)
      {
        FieldInfo fi = fields[i];
        store.Read(); // read the field name element
        if(!string.Equals(store.LocalName, fi.Name, StringComparison.Ordinal))
        {
          throw new ArgumentException("Expected node: " + fi.Name);
        }
        store.Read(); // read the data element
        fi.SetValue(obj, InnerDeserialize(store));
        store.ReadEndElement(); // move past the the closing data element
      }

      // either move past the closing field name element, or if there were no fields, to "CustomValues" or the end tag
      store.Read();
    }

    ISerializable iserializable = obj as ISerializable;
    // if it implements ISerializable and we're not at the end yet
    if(iserializable != null && store.NodeType != XmlNodeType.EndElement)
    {
      if(!string.Equals(store.LocalName, "CustomValues", StringComparison.Ordinal))
      {
        throw new ArgumentException("Expected 'CustomValues' element for object of type: "+type.FullName);
      }

      DeserializationStore customStore = new DeserializationStore(store);
      iserializable.DeserializeCustomValues(customStore);
      customStore.Finish();
    }

    // finally, return the object, unless it's an object reference, in which case we'll use it to get the real object
    IObjectReference objRef = obj as IObjectReference;
    return objRef != null ? objRef.GetRealObject() : obj;
  }

  /// <summary>Parses a simple value from a string, given its type.</summary>
  static object ParseSimpleValue(string text, Type type)
  {
    if(type.IsEnum)
    {
      return Enum.Parse(type, text);
    }
    else if(type.IsArray) // if it's an array, the text will be a comma separated list
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
        case TypeCode.String: return text;
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

  /// <summary>Determines if a type is simple enough that it can be represented in an attribute string.</summary>
  /// <param name="type"></param>
  /// <returns></returns>
  static bool IsSimpleType(Type type)
  {
    if(type.IsPrimitive || type == typeof(string) || type.IsEnum || type == typeof(DateTime))
    {
      return true; // numerics, chars, strings, enums, and dates are simple.
    }
    else if(type.IsArray && type.GetArrayRank() == 1) // if it's a one-dimensional array ...
    {
      Type subType = type.GetElementType(); // ... of a non-array, non-string simple type, then it's simple
      return !subType.IsArray && subType != typeof(string) && IsSimpleType(subType);
    }
    else // otherwise, it's not simple
    {
      return false;
    }
  }
  
  static readonly Dictionary<string,Type> typeDict;
}
#endregion

} // namespace RotationalForce.Engine