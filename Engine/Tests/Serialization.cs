using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using NUnit.Framework;

// TODO: clean up the test code

namespace RotationalForce.Engine.Tests
{

[TestFixture]
public sealed class SerializationTests
{
  #region SimpleBase
  class SimpleBase
  {
    protected const string StringValue = " \t hello<wo(rld,howare you?\ni'm f)ine\"tha+nks \t\t ";

    protected SimpleBase() { } // test non-public constructors

    // initialize variables in a special constructor so they definitely won't be initialized after the deserializer
    // constructs the object
    public SimpleBase(int dummy)
    {
      boolValue = true;
      charValue = 'A';
      decimalValue = -1.254m;
      sbyteValue = -128;
      byteValue  = 255;
      shortValue = -32768;
      ushortValue = 500;
      intValue = int.MinValue;
      uintValue = 5000;
      longValue = long.MinValue;
      ulongValue = 50000;
      floatValue = -145.234f;
      doubleValue = 2452.257482334;
      stringValue = StringValue;
      colorValue = Color.FromArgb(128,255,0,82);
      dtValue = new DateTime(2005, 2, 4, 18, 4, 22);
      intArr = new int[] { 1,-1,54,1000,-23043 };
      colorArr = new Color[] { Color.Red, Color.Blue, Color.Green, Color.FromArgb(64, Color.Firebrick) };
      kind = DateTimeKind.Utc;
    }

    public virtual void Check()
    {
      Assert.AreEqual(boolValue, true);
      Assert.AreEqual(charValue, 'A');
      Assert.AreEqual(decimalValue, -1.254m);
      Assert.AreEqual(sbyteValue, -128);
      Assert.AreEqual(byteValue, 255);
      Assert.AreEqual(shortValue, -32768);
      Assert.AreEqual(ushortValue, 500);
      Assert.AreEqual(intValue, int.MinValue);
      Assert.AreEqual(uintValue, 5000);
      Assert.AreEqual(longValue, long.MinValue);
      Assert.AreEqual(ulongValue, 50000);
      Assert.AreEqual(floatValue, -145.234f, 0f);
      Assert.AreEqual(doubleValue, 2452.257482334, 0);
      Assert.AreEqual(stringValue, StringValue);
      Assert.AreEqual(colorValue, Color.FromArgb(128,255,0,82));
      Assert.AreEqual(dtValue, new DateTime(2005, 2, 4, 18, 4, 22));
      Assert.AreEqual(kind, DateTimeKind.Utc);
      AssertArraysEqual(intArr, new int[] { 1, -1, 54, 1000, -23043 });
      AssertArraysEqual(colorArr, new Color[] { Color.Red, Color.Blue, Color.Green, Color.FromArgb(64, Color.Firebrick) });
    }

    bool boolValue;
    char charValue;
    decimal decimalValue;
    sbyte sbyteValue;
    byte byteValue;
    short shortValue;
    ushort ushortValue;
    int intValue;
    uint uintValue;
    long longValue;
    ulong ulongValue;
    float floatValue;
    double doubleValue;
    string stringValue;
    Color colorValue;
    DateTime dtValue;
    int[] intArr;
    Color[] colorArr;
    DateTimeKind kind;
  }
  #endregion

  #region SimpleDerived
  class SimpleDerived : SimpleBase
  {
    public SimpleDerived()
    {
      isXInitialized = false;
    }

    public SimpleDerived(int dummy) : base(dummy)
    {
      Foo = "bar";
      stringValue = "xxx";
      X = 100;
      isXInitialized = true;
    }

    public override void Check()
    {
      base.Check();
      Assert.AreEqual(Foo, "bar");
      Assert.AreEqual(stringValue, "xxx");
      Assert.AreEqual(X, isXInitialized ? 100 : 0);
    }

    string Foo;
    string stringValue; // make sure shadowed fields

    // make sure non-serialized fields aren't.
    [NonSerialized] int X;
    [NonSerialized] bool isXInitialized;
  }
  #endregion

  #region DerivedWithISerializable
  class DerivedWithISerializable : SimpleDerived, ISerializable
  {
    protected DerivedWithISerializable() { }

    public DerivedWithISerializable(int dummy) : base(dummy) { }

    public Type TypeToSerialize
    {
      get { return GetType(); }
    }

    public void BeforeSerialize(SerializationStore store)
    {
      // test storing extra values
      store.AddValue("x", 5);
    }

    public void BeforeDeserialize(DeserializationStore store)
    {
      Assert.IsTrue(store.GetValue("x") is int);
      Assert.AreEqual(store.GetInt32("x"), 5);
    }

    public void Serialize(SerializationStore store)
    {
      // test storing extra values
      store.AddValue("x", 5);
      store.AddValue("y", 5.0);
      store.AddValue("z+< ()", StringValue); // test illegal Sexp characters in names
      store.AddValue("dummy1", null);
    }

    public void Deserialize(DeserializationStore store)
    {
      Assert.IsTrue(store.GetValue("x") is int);
      Assert.AreEqual(store.GetInt32("x"), 5);
      
      // make sure getting items out of order works
      Assert.AreEqual(store.GetString("z+< ()"), StringValue);
      
      Assert.IsTrue(store.GetValue("y") is double);
      Assert.AreEqual(store.GetDouble("y"), 5.0, 0);
      
      // don't get "dummy1" -- test that not retrieving all values works
    }
    
    // test classes with no fields
  }
  #endregion

  #region DerivedWithISerializableAndMDArray
  class DerivedWithISerializableAndMDArray : DerivedWithISerializable
  {
    protected DerivedWithISerializableAndMDArray() { }

    public DerivedWithISerializableAndMDArray(int dummy) : base(dummy)
    {
      int2d = new int[,] { {1,2,3,4,5}, {-1,-4,-6,-7,-9} };
      float3d = new float[,,] { { { 1.1f, 2.2f }, { 3.3f, 4.4f }, { 5.5f, 6.6f } },
                                { { -1.1f, -2.2f }, { -3.3f, -4.4f }, { -5.5f, -6.6f } },
                                { { 1.1f, -2.2f }, { 3.3f, -4.4f }, { 5.5f, -6.6f } },
                                { { -1.1f, 2.2f }, { -3.3f, 4.4f }, { -5.5f, 6.6f } } };
      short2d = new short[,] { {-1,-2,-3,3,2,1} };
      byte2d = new byte[,] { {1}, {2}, {5}, {255} };
    }

    public override void Check()
    {
      AssertArraysEqual(int2d, new int[,] { {1,2,3,4,5}, {-1,-4,-6,-7,-9} }); // test 2d arrays
      AssertArraysEqual(float3d, new float[,,] { { { 1.1f, 2.2f }, { 3.3f, 4.4f }, { 5.5f, 6.6f } }, // test 3d arrays
                                { { -1.1f, -2.2f }, { -3.3f, -4.4f }, { -5.5f, -6.6f } },
                                { { 1.1f, -2.2f }, { 3.3f, -4.4f }, { 5.5f, -6.6f } },
                                { { -1.1f, 2.2f }, { -3.3f, 4.4f }, { -5.5f, 6.6f } } });
      AssertArraysEqual(short2d, new short[,] { {-1,-2,-3,3,2,1} }); // test MD arrays with a dimension of length 1
      AssertArraysEqual(byte2d, new byte[,] { {1}, {2}, {5}, {255} }); // test MD arrays with a different dimension of length 1
    }

    int[,] int2d;
    float[,,] float3d;
    short[,] short2d;
    byte[,] byte2d;
  }
  #endregion

  #region Struct
  struct Struct
  {
    public Struct(int dummy)
    {
      x = 10;
      nested = new Nested();
      nested.x = 100;
    }
    
    public void Check()
    {
      Assert.AreEqual(x, 10);
      Assert.AreEqual(nested.x, 100);
    }

    struct Nested // test nested classes
    {
      public int x;
    }

    Nested nested;
    int x;
  }
  #endregion

  #region Singleton
  sealed class Singleton : SimpleDerived, ISerializable
  {
    private Singleton() { }

    public static readonly Singleton Instance = new Singleton();
  
    Type ISerializable.TypeToSerialize
    {
	    get { return typeof(SingletonReference); }
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
  
  sealed class SingletonReference : IObjectReference
  {
    object IObjectReference.GetRealObject()
    {
      return Singleton.Instance;
    }
  }
  #endregion

  #region DerivedForReferenceTest
  class DerivedForReferenceTest : SimpleDerived, ISerializable
  {
    protected DerivedForReferenceTest() { }

    public DerivedForReferenceTest(int dummy) : base(dummy)
    {
      myID = ++nextID;
    }

    Type ISerializable.TypeToSerialize
    {
      get
      {
        return objDict.ContainsKey(myID) ? typeof(ComplexObjectReference) : GetType();
      }
    }

    void ISerializable.BeforeSerialize(SerializationStore store)
    {
      store.AddValue("ID", myID);
      objDict[myID] = this;
    }

    void ISerializable.BeforeDeserialize(DeserializationStore store)
    {
      objDict[store.GetUint32("ID")] = this;
    }

    void ISerializable.Serialize(SerializationStore store)
    {
    }

    void ISerializable.Deserialize(DeserializationStore store)
    {
    }

    uint myID;
  }
  
  sealed class ComplexObjectReference : ISerializable, IObjectReference
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
      id = store.GetUint32("ID");
    }

    void ISerializable.Serialize(SerializationStore store)
    {
    }

    void ISerializable.Deserialize(DeserializationStore store)
    {
    }

    object IObjectReference.GetRealObject()
    {
      return objDict[id];
    }
    
    uint id;
  }
  #endregion
  
  #region CircularReferenceTest
  class DerivedForCircularReferenceTest : DerivedForReferenceTest
  {
    protected DerivedForCircularReferenceTest() { }

    public DerivedForCircularReferenceTest(int dummy) : base(dummy)
    {
      foo = new Foo(this);
    }

    public override void Check()
    {
      base.Check();
      Assert.AreSame(this, foo.parent);
    }

    sealed class Foo
    {
      private Foo() { }
      public Foo(DerivedForCircularReferenceTest parent) { this.parent = parent; }
      public DerivedForCircularReferenceTest parent;
    }
    
    Foo foo;
  }
  #endregion

  static Dictionary<uint,DerivedForReferenceTest> objDict;
  static uint nextID;

  [SetUp]
  public void TestSetup()
  {
    nextID = 0;
    objDict = new Dictionary<uint,DerivedForReferenceTest>();
  }

  [Test]
  public void Test01Sexps()
  {
    MemoryStream ms = new MemoryStream();
    SexpWriter writer = new SexpWriter(ms);
    writer.WriteElement("a"); // (a) (b hello) (c xxx(d foo)yyy(e))
    writer.WriteElement("b", "hello");
    writer.BeginElement("c");
    writer.WriteContent("xxx");
    writer.WriteElement("d", "foo");
    writer.WriteContent("yyy");
    writer.WriteElement("e");
    writer.EndElement();
    writer.Flush();
    
    ms.Position = 0;
    SexpReader reader = new SexpReader(ms);
    reader.Read();
    reader.ReadBeginElement("a");
    reader.ReadEndElement(); // end a
    Assert.AreEqual(reader.ReadElement("b"), "hello");

    reader.ReadBeginElement("c");
    Assert.AreEqual(reader.ReadContent(), "xxx");
    Assert.AreEqual(reader.ReadElement("d"), "foo");
    Assert.AreEqual(reader.ReadContent(), "yyy");
    reader.ReadBeginElement("e");
    reader.ReadEndElement(); // end e
    reader.ReadEndElement(); // end c
    Assert.AreEqual(reader.NodeType, SexpNodeType.EOF);

    // test skipping
    ms.Position = 0;
    reader = new SexpReader(ms);
    reader.Read();
    Assert.AreEqual(reader.TagName, "a");
    Assert.AreEqual(reader.NodeType, SexpNodeType.Begin);
    reader.Skip();
    Assert.AreEqual(reader.TagName, "b");
    Assert.AreEqual(reader.NodeType, SexpNodeType.Begin);
    reader.Skip();
    Assert.AreEqual(reader.TagName, "c");
    Assert.AreEqual(reader.NodeType, SexpNodeType.Begin);
    reader.Skip();
    Assert.AreEqual(reader.NodeType, SexpNodeType.EOF);

    ms.Position = 0;
    reader = new SexpReader(ms);
    reader.Read();
    reader.Skip();
    reader.Skip(); // skip to 'c'
    reader.Read(); // read to 'xxx'
    reader.Skip();
    Assert.AreEqual(reader.NodeType, SexpNodeType.EOF);
  }

  [Test]
  public void Test02VerySimple()
  {
    int i = RoundTrip(5);
    Assert.AreEqual(i, 5);
  }

  [Test]
  public void Test03Simple()
  {
    Test(new SimpleBase(1));
  }

  [Test]
  public void Test04Derived()
  {
    Test(new SimpleDerived(1));
  }
  
  [Test]
  public void Test05SimpleISerializable()
  {
    Test(new DerivedWithISerializable(1));
  }
  
  [Test]
  public void Test06MultidimensionalArrays()
  {
    Test(new DerivedWithISerializableAndMDArray(1));
  }

  [Test]
  public void Test07Struct()
  {
    Struct s = new Struct(1);
    s.Check();
    s = RoundTrip(s);
    s.Check();
  }
  
  [Test]
  public void Test08Singleton()
  {
    Singleton s = RoundTrip(Singleton.Instance);
    Assert.AreSame(Singleton.Instance, s);
  }
  
  [Test]
  public void Test09List()
  {
    List<Struct> s = new List<Struct>();
    Assert.AreEqual(RoundTrip(s).Count, 0); // test empty list

    s.Add(new Struct(1));
    s = RoundTrip(s);
    Assert.AreEqual(s.Count, 1); // test struct list with 1 entry
    s[0].Check();
    
    System.Collections.ArrayList strs = new System.Collections.ArrayList(); // test arraylist with 2 entries
    strs.Add("hello");
    strs.Add(4);
    strs = RoundTrip(strs);
    Assert.AreEqual(strs.Count, 2);
    Assert.AreEqual((string)strs[0], "hello");
    Assert.AreEqual((int)strs[1], 4);

    List<SimpleBase> bl = new List<SimpleBase>();
    bl.Add(new SimpleBase(1));
    bl.Add(new SimpleDerived(1));
    bl.Add(Singleton.Instance);
    bl = RoundTrip(bl);
    Assert.AreEqual(bl.Count, 3);
    bl[0].Check();
    bl[1].Check();
    Assert.AreSame(bl[2], Singleton.Instance);
  }
  
  [Test]
  public void Test10Dict()
  {
    Dictionary<string,int> dsi = new Dictionary<string,int>();
    Assert.AreEqual(RoundTrip(dsi).Count, 0); // test empty dictionary
    
    dsi.Add("johnny", 5); // test dictionary with 1 item
    dsi = RoundTrip(dsi);
    Assert.AreEqual(dsi.Count, 1);
    Assert.AreEqual(dsi["johnny"], 5);
    
    System.Collections.Hashtable hash = new System.Collections.Hashtable(); // test hashtable with multiple items
    hash.Add(5, Singleton.Instance);
    hash.Add(new DateTime(2005, 11, 13), new DerivedWithISerializableAndMDArray(1));
    hash = RoundTrip(hash);
    Assert.AreEqual(hash.Count, 2);
    Assert.AreSame(hash[5], Singleton.Instance);
    ((DerivedWithISerializableAndMDArray)hash[new DateTime(2005, 11, 13)]).Check();
  }
  
  [Test]
  public void Test11ComplexObjectRef()
  {
    DerivedForReferenceTest a, b, c, orig;
    orig = a = b = c = new DerivedForReferenceTest(1);
    
    objDict.Clear();
    MemoryStream stream = new MemoryStream(); // also test serializing multiple objects to a single stream
    SexpWriter writer = new SexpWriter(stream);
    Serializer.Serialize(a, writer);
    Serializer.Serialize(b, writer);
    Serializer.Serialize(c, writer);
    writer.Flush();
    Assert.AreEqual(objDict.Count, 1); // only one object should have been added

    objDict.Clear();
    stream.Position = 0;
    SexpReader reader = new SexpReader(stream);
    a = (DerivedForReferenceTest)Serializer.Deserialize(reader);
    b = (DerivedForReferenceTest)Serializer.Deserialize(reader);
    c = (DerivedForReferenceTest)Serializer.Deserialize(reader);

    Assert.AreSame(a, b); // make sure the object references are equal between a, b, and c
    Assert.AreSame(b, c);
    Assert.AreNotSame(a, orig); // but not equal to the original object
  }
  
  [Test]
  public void Test12CircularReferences()
  {
    objDict.Clear();
    Test(new DerivedForCircularReferenceTest(1));
  }

  static T RoundTrip<T>(T o)
  {
    MemoryStream ms = new MemoryStream();
    Serializer.Serialize(o, ms);
    ms.Position = 0;
    return (T)Serializer.Deserialize(ms);
  }
  
  static void Test(SimpleBase b)
  {
    b.Check();
    b = RoundTrip(b);
    b.Check();
  }
  
  static void AssertAreEqual(Color a, Color b)
  {
    Assert.AreEqual(a.IsNamedColor, b.IsNamedColor);
    if(a.IsNamedColor)
    {
      Assert.AreEqual(a.Name, b.Name);
    }

    Assert.AreEqual(a.R, b.R);
    Assert.AreEqual(a.G, b.G);
    Assert.AreEqual(a.B, b.B);
    Assert.AreEqual(a.A, b.A);
  }

  static void AssertArraysEqual(Color[] a, Color[] b)
  {
    if(a == null && b == null) return;
    Assert.IsFalse(a == null || b == null);
    Assert.AreEqual(a.Length, b.Length);

    for(int i=0; i<a.Length; i++)
    {
      AssertAreEqual(a[i], b[i]);
    }
  }

  static void AssertArraysEqual(Array a, Array b)
  {
    if(a == null && b == null) return;
    Assert.IsFalse(a == null || b == null);

    Assert.AreEqual(a.Rank, b.Rank);
    int[] lengths = new int[a.Rank];
    for(int i=0; i<a.Rank; i++)
    {
      lengths[i] = a.GetLength(i);
      Assert.AreEqual(a.GetLength(i), b.GetLength(i));
      Assert.AreEqual(a.GetLowerBound(i), b.GetLowerBound(i));
    }

    int[] indices = new int[a.Rank];
    for(int i=0; i<a.Length; i++)
    {
      Assert.AreEqual(a.GetValue(indices), b.GetValue(indices));
      
      for(int j=a.Rank-1; j >= 0; j--)
      {
        if(++indices[j] == lengths[j])
        {
          indices[j] = 0;
        }
        else
        {
          break;
        }
      }
    }
  }
}

} // namespace RotationalForce.Engine.Tests