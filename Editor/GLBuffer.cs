using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace RotationalForce.Editor
{

sealed class GLBuffer : IDisposable
{
  public GLBuffer(int width, int height)
  {
    hdc = CreateCompatibleDC(IntPtr.Zero);
    if(hdc == IntPtr.Zero)
    {
      Dispose();
      throw new ApplicationException("Failure in GetDC");
    }
    
    BitmapInfo bmp = new BitmapInfo();
    bmp.Size = Marshal.SizeOf(typeof(BitmapInfo));
    bmp.Width = width;
    bmp.Height = height;
    bmp.Planes = 1;
    bmp.BitCount = 24;

    hbitmap = CreateDIBSection(hdc, ref bmp, 0, IntPtr.Zero, IntPtr.Zero, 0);
    if(hbitmap == IntPtr.Zero)
    {
      Dispose();
      throw new ApplicationException("Failure in CreateBitmap");
    }

    IntPtr oldObject = SelectObject(hdc, hbitmap);
    if(oldObject != IntPtr.Zero) DeleteObject(oldObject);
    
    PixelFormatDescriptor pfd = new PixelFormatDescriptor();
    pfd.nSize      = (ushort)Marshal.SizeOf(typeof(PixelFormatDescriptor));
    pfd.nVersion   = 1;
    pfd.dwFlags    = PFlag.DepthDontcare | PFlag.DoublebufferDontcare | PFlag.DrawToBitmap | PFlag.SupportOpengl;
    pfd.iPixelType = 0; // PFD_TYPE_RGBA
    pfd.cColorBits = 24;
    
    int pixelFormat = ChoosePixelFormat(hdc, ref pfd);
    if(pixelFormat == 0)
    {
      Dispose();
      throw new ApplicationException("Failure in ChoosePixelFormat");
    }

    if(SetPixelFormat(hdc, pixelFormat, ref pfd) == 0)
    {
      Dispose();
      throw new ApplicationException("Failure in SetPixelFormat");
    }
    
    hglrc = wglCreateContext(hdc);
    if(hglrc == IntPtr.Zero)
    {
      Dispose();
      throw new ApplicationException("Failure in wglCreateContext");
    }
  }

  ~GLBuffer() { Dispose(true); }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
    Dispose(false);
  }

  public Bitmap CreateBitmap()
  {
    return Bitmap.FromHbitmap(hbitmap);
  }
  
  void Dispose(bool finalizing)
  {
    if(currentBuffer.Target == this) SetCurrent(null);

    if(hglrc != IntPtr.Zero)
    {
      wglDeleteContext(hglrc);
      hglrc = IntPtr.Zero;
    }
    if(hdc != IntPtr.Zero)
    {
      DeleteDC(hdc);
      hdc = IntPtr.Zero;
    }
    if(hbitmap != IntPtr.Zero)
    {
      DeleteObject(hbitmap);
      hbitmap = IntPtr.Zero;
    }
  }
  
  IntPtr hbitmap, hdc, hglrc;

  public static void SetCurrent(GLBuffer buffer)
  {
    if(buffer == null)
    {
      wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
      currentBuffer = null;
    }
    else
    {
      wglMakeCurrent(buffer.hdc, buffer.hglrc);
      currentBuffer = new WeakReference(buffer);
    }
  }

  [ThreadStatic] static WeakReference currentBuffer;

  [Flags]
  enum PFlag : uint
  {
    Doublebuffer          =0x00000001,
    Stereo                =0x00000002,
    DrawToWindow          =0x00000004,
    DrawToBitmap          =0x00000008,
    SupportGdi            =0x00000010,
    SupportOpengl         =0x00000020,
    GenericFormat         =0x00000040,
    NeedPalette           =0x00000080,
    NeedSystemPalette     =0x00000100,
    SwapExchange          =0x00000200,
    SwapCopy              =0x00000400,
    SwapLayerBuffers      =0x00000800,
    GenericAccelerated    =0x00001000,
    SupportDirectdraw     =0x00002000,
    DepthDontcare         =0x20000000,
    DoublebufferDontcare  =0x40000000,
    StereoDontcare        =0x80000000,
  }

  [StructLayout(LayoutKind.Sequential)]
  struct PixelFormatDescriptor
  {
    public ushort nSize, nVersion;
    public PFlag dwFlags;
    public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift,
                cAlphaBits, cAlphaShift, cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits,
                cDepthBits, cStencilBits, cAuxBuffers, iLayerType, bReserved;
    public uint dwLayerMask, dwVisibleMask, dwDamageMask;
  }
  
  [StructLayout(LayoutKind.Sequential)]
  struct BitmapInfo
  {
    public int Size, Width, Height;
    public ushort Planes, BitCount;
    public uint Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ColorUsed, ColorImportant;
    uint quads;
  }

  [DllImport("gdi32.dll")]
  static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BitmapInfo bmp, uint usage,
                                        IntPtr data, IntPtr section, uint offset);
  [DllImport("gdi32.dll")]
  static extern IntPtr CreateCompatibleDC(IntPtr hdc);
  [DllImport("gdi32.dll")]
  static extern int ChoosePixelFormat(IntPtr hdc, [In] ref PixelFormatDescriptor pfmt);
  [DllImport("gdi32.dll")]
  static extern int SetPixelFormat(IntPtr hdc, int iPixelFormat, [In] ref PixelFormatDescriptor pfmt);
  [DllImport("gdi32.dll")]
  static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
  [DllImport("gdi32.dll")]
  static extern int DeleteObject(IntPtr handle);
  [DllImport("gdi32.dll")]
  static extern int DeleteDC(IntPtr hdc);
  [DllImport("opengl32.dll")]
  static extern IntPtr wglCreateContext(IntPtr hdc);
  [DllImport("opengl32.dll")]
  static extern int wglDeleteContext(IntPtr hglrc);
  [DllImport("opengl32.dll")]
  static extern int wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
  [DllImport("kernel32.dll")]
  static extern uint GetLastError();
}

} // namespace RotationalForce.Editor