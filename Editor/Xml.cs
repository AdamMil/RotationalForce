using System.Xml;

namespace RotationalForce.Editor
{

static class Xml
{ public static string Attr(XmlNode node, string attribute) { return Attr(node, attribute, null); }
  public static string Attr(XmlNode node, string attribute, string defaultValue)
  { XmlAttribute attr = node.Attributes[attribute];
    return attr==null ? defaultValue : attr.Value;
  }
}

} // namespace RotationalForce.Editor