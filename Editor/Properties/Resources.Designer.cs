﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.235
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RotationalForce.Editor.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("RotationalForce.Editor.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        internal static System.Drawing.Bitmap LayerTool {
            get {
                object obj = ResourceManager.GetObject("LayerTool", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        internal static byte[] Rotate {
            get {
                object obj = ResourceManager.GetObject("Rotate", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        internal static System.Drawing.Bitmap SelectTool {
            get {
                object obj = ResourceManager.GetObject("SelectTool", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        internal static byte[] ZoomIn {
            get {
                object obj = ResourceManager.GetObject("ZoomIn", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        internal static byte[] ZoomOut {
            get {
                object obj = ResourceManager.GetObject("ZoomOut", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        internal static System.Drawing.Bitmap ZoomTool {
            get {
                object obj = ResourceManager.GetObject("ZoomTool", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
    }
}
