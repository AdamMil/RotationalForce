using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using RotationalForce.Engine.Design;

namespace RotationalForce.Editor
{

#region Custom Type Descriptor
/// <summary>Adds our custom type descriptor to various types.</summary>
sealed class MyTypeDescriptionProvider : TypeDescriptionProvider
{
  public MyTypeDescriptionProvider(TypeDescriptionProvider parent)
  {
    this.parent = parent;
  }

  public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
  {
    ICustomTypeDescriptor defaultDescriptor = parent.GetTypeDescriptor(objectType, instance);
    return objectType.IsPrimitive ? defaultDescriptor : new MyTypeDescriptor(defaultDescriptor);
  }

  readonly TypeDescriptionProvider parent;
}

/// <summary>A custom type descriptor that lets us override type properties.</summary>
sealed class MyTypeDescriptor : CustomTypeDescriptor
{
  public MyTypeDescriptor(ICustomTypeDescriptor parent) : base(parent) { }

  public override PropertyDescriptorCollection GetProperties()
  {
    return Filter(base.GetProperties());
  }

  public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
  {
    return Filter(base.GetProperties(attributes));
  }

  PropertyDescriptorCollection Filter(PropertyDescriptorCollection properties)
  {
    PropertyDescriptor[] newPds = null;

    for(int i=0; i<properties.Count; i++)
    {
      PropertyDescriptor pd = properties[i];

      // currently, it just adds the NumberEditor to properties that have a Range attribute
      RangeAttribute range = (RangeAttribute)pd.Attributes[typeof(RangeAttribute)];
      if(range != null && pd.Attributes[typeof(EditorAttribute)] == null)
      {
        if(newPds == null)
        {
          newPds = new PropertyDescriptor[properties.Count];
          properties.CopyTo(newPds, 0);
        }
        newPds[i] = new MyEditorDescriptor(pd, typeof(NumberEditor));
      }
    }

    return newPds == null ? properties : new PropertyDescriptorCollection(newPds);
  }
}

/// <summary>A property descriptor that adds a custom editor to the property.</summary>
class MyEditorDescriptor : PropertyDescriptor
{
  public MyEditorDescriptor(PropertyDescriptor parent, Type editorType)
    : base(parent, new Attribute[] { new EditorAttribute(editorType, typeof(UITypeEditor)) })
  {
    this.parent = parent;
  }

  public override Type ComponentType
  {
    get { return parent.ComponentType; }
  }

  public override bool IsReadOnly
  {
    get { return parent.IsReadOnly; }
  }

  public override Type PropertyType
  {
    get { return parent.PropertyType; }
  }

  public override bool CanResetValue(object component)
  {
    return parent.CanResetValue(component);
  }

  public override object GetValue(object component)
  {
    return parent.GetValue(component);
  }

  public override void ResetValue(object component)
  {
    parent.ResetValue(component);
  }

  public override void SetValue(object component, object value)
  {
    parent.SetValue(component, value);
  }

  public override bool ShouldSerializeValue(object component)
  {
    return parent.ShouldSerializeValue(component);
  }

  readonly PropertyDescriptor parent;
}
#endregion

#region Number editor
/// <summary>Edits a number using a trackbar.</summary>
public class NumberEditor : UITypeEditor
{
  public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
  {
    return UITypeEditorEditStyle.DropDown;
  }

  public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
  {
 	  // make sure it's a type we can edit
    if(value != null && value.GetType().IsPrimitive && !(value is bool) && !(value is char))
    {
      IWindowsFormsEditorService svc =
        (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
      if(svc != null)
      {
        RangeAttribute range = (RangeAttribute)context.PropertyDescriptor.Attributes[typeof(RangeAttribute)];
        if(range != null)
        {
          EditorControl control = new EditorControl(value, range.Minimum, range.Maximum);
          svc.DropDownControl(control);
          return control.Value;
        }
      }
    }
    return value;
  }

  sealed class EditorControl : UserControl
  {
    public EditorControl(object value, double min, double max)
    {
      InitializeComponent();
      this.valueType = value.GetType();
      this.currentValue = value;
      this.min = min;
      this.max = max;
      SetValue(value);
      trackBar.ValueChanged += trackBar_ValueChanged;
    }

    void trackBar_ValueChanged(object sender, EventArgs e)
    {
      double value = (double)trackBar.Value / trackBar.Maximum * (max-min) + min;

      switch(Type.GetTypeCode(valueType))
      {
        case TypeCode.Byte: currentValue = (byte)Math.Round(value * byte.MaxValue); break;
        case TypeCode.Decimal: currentValue = new Decimal(value); break;
        case TypeCode.Int16: currentValue = (short)Math.Round(value * short.MaxValue); break;
        case TypeCode.Int32: currentValue = (int)Math.Round(value * int.MaxValue); break;
        case TypeCode.Int64: currentValue = (long)Math.Round(value * long.MaxValue); break;
        case TypeCode.SByte: currentValue = (sbyte)Math.Round(value * sbyte.MaxValue); break;
        case TypeCode.Single: currentValue = (float)value; break;
        case TypeCode.UInt16: currentValue = (ushort)Math.Round(value * ushort.MaxValue); break;
        case TypeCode.UInt32: currentValue = (uint)Math.Round(value * uint.MaxValue); break;
        case TypeCode.UInt64: currentValue = (long)Math.Round(value * ulong.MaxValue); break;
        case TypeCode.Double: default: currentValue = value; break;
      }
    }

    public object Value
    {
      get { return currentValue; }
    }

    void SetValue(object value)
    {
      double fValue = Convert.ToDouble(value);
      long newValue = (long)Math.Round((fValue-min) / (max-min) * trackBar.Maximum);
      trackBar.Value = newValue < 0 ? 0 : newValue > int.MaxValue ? int.MaxValue : (int)newValue;
    }

    void InitializeComponent()
    {
      trackBar = new TrackBar();
      trackBar.Location = new Point(0, 0);
      trackBar.Size = new Size(150, 25);
      trackBar.TabIndex = 0;
      trackBar.TickStyle = TickStyle.None;

      trackBar.Minimum  = 0;
      trackBar.Maximum  = int.MaxValue;
      trackBar.SmallChange = trackBar.Maximum / trackBar.Width;
      trackBar.LargeChange = trackBar.SmallChange * 5;
      
      Controls.Add(this.trackBar);
      Size = new Size(150, 25);
      PerformLayout();
    }

    readonly double min, max;
    object currentValue;
    readonly Type valueType;
    TrackBar trackBar;
  }
}
#endregion

} // namespace RotationalForce.Editor