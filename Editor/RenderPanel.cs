using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameLib.Interop.OpenGL;

namespace RotationalForce.Editor
{

#region GLBuffer
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
    
    this.width  = width;
    this.height = height;
  }

  ~GLBuffer() { Dispose(true); }

  public int Width
  {
    get { return width; }
  }
  
  public int Height
  {
    get { return height; }
  }

  public Size Size
  {
    get { return new Size(width, height); }
  }

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
    if(currentBuffer != null && currentBuffer.Target == this) SetCurrent(null);

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
  int width, height;

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
#endregion

delegate void MouseDragEventHandler(object sender, MouseDragEventArgs e);

class MouseDragEventArgs : MouseEventArgs
{
  public MouseDragEventArgs(MouseButtons button, Point originalPoint, int x, int y, int offsetX, int offsetY)
    : base(button, 1, x, y, 0)
  {
    start  = originalPoint;
    offset = new Size(offsetX, offsetY);
  }
  
  public Point Start
  {
    get { return start; }
  }

  public Size Offset
  {
    get { return offset; }
  }

  Point start;
  Size offset;
}

#region RenderPanel
class RenderPanel : Control
{
  public RenderPanel()
  {
    SetStyle(ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint, true);
    SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ContainerControl |
             ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
    DoubleBuffered = true;
  }

  public event EventHandler RenderBackground;

  public void InvalidateRender() { InvalidateRender(ClientRectangle); }

  public void InvalidateRender(Rectangle area)
  {
    needRender = true;
    Invalidate(area);
  }

  protected override void Dispose(bool disposing)
  {
    DisposeBuffer();
    base.Dispose(disposing);
  }

  protected override void OnPaintBackground(PaintEventArgs e)
  {
    if(needRender && RenderBackground != null && !DesignMode)
    {
      needRender = false;

      GLBuffer.SetCurrent(GetBuffer());
      RenderBackground(this, EventArgs.Empty);
      GL.glFlush();

      if(image != null) image.Dispose();
      image = buffer.CreateBitmap();

      GLBuffer.SetCurrent(null);
    }

    if(image != null)
    {
      e.Graphics.DrawImage(image, e.ClipRectangle.X, e.ClipRectangle.Y, e.ClipRectangle, GraphicsUnit.Pixel);
    }
    else
    {
      base.OnPaintBackground(e);
    }
  }

  protected override void OnResize(EventArgs e)
  {
    base.OnResize(e);
    DisposeBuffer();
    InvalidateRender();
    Invalidate();
  }

  protected override bool ProcessDialogKey(Keys keyData)
  {
    switch(keyData & Keys.KeyCode)
    {
      case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right: // we want to handle arrow keys ourself
        return false;

      default:
        return base.ProcessDialogKey(keyData);
    }
  }

  #region Mouse dragging
  public event MouseEventHandler MouseDragStart;
  public event MouseDragEventHandler MouseDrag;
  public event MouseDragEventHandler MouseDragEnd;

  public void CancelMouseDrag()
  {
    if(dragButton != MouseButtons.None)
    {
      mouseDownPos[ButtonToIndex(dragButton)] = new Point(-1, -1); // mark the drag button as released
      dragButton = MouseButtons.None; // clear the dragging flag
      Capture = false; // stop capturing the mouse
    }
  }
  
  protected virtual void OnMouseDragStart(MouseEventArgs e)
  {
    if(MouseDragStart != null) MouseDragStart(this, e);
  }
  
  protected virtual void OnMouseDrag(MouseDragEventArgs e)
  {
    if(MouseDrag != null) MouseDrag(this, e);
  }

  protected virtual void OnMouseDragEnd(MouseDragEventArgs e)
  {
    if(MouseDragEnd != null) MouseDragEnd(this, e);
  }
  #endregion

  #region Low-level mouse handling
  /* use low-level mouse events to implement higher-level click and drag events */
  protected override void OnMouseClick(MouseEventArgs e)
  {
    // disable the base MouseClick event (because it fires at the end of a drag).
    // we'll call base.OnMouseClick if we want to fire the event
  }
  
  protected override void OnMouseDown(MouseEventArgs e)
  {
    base.OnMouseDown(e);

    int button = ButtonToIndex(e.Button);
    if(button == -1) return; // ignore unsupported buttons
    // when a mouse button is pressed, mark the location. this serves as both an indication that the button is pressed
    // and stores the beginning of the drag, if the user drags the mouse
    mouseDownPos[button] = e.Location;
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    base.OnMouseMove(e);

    if(dragButton != MouseButtons.None) // if we're currently dragging, fire a drag event
    {
      int xd = e.X-lastDragPos.X, yd = e.Y-lastDragPos.Y;
      if(xd == 0 && yd == 0) return;

      OnMouseDrag(new MouseDragEventArgs(dragButton, mouseDownPos[ButtonToIndex(dragButton)], e.X, e.Y, xd, yd));
      // update the last drag point so we can send a delta to OnMouseDrag()
      lastDragPos = e.Location;
    }
    else // otherwise, see if we should start dragging.
    {
      int button = ButtonToIndex(e.Button);
      if(button == -1 || !IsMouseDown(button)) return; // ignore unsupported buttons

      int xd = e.X-mouseDownPos[button].X, yd = e.Y-mouseDownPos[button].Y;
      int dist = xd*xd + yd*yd; // the squared distance
      if(dist >= 16) // if the mouse is moved four pixels or more, start a drag event
      {
        dragButton = e.Button;
        lastDragPos = e.Location;

        // issue a drag start using the stored location of where the mouse was originally pressed
        OnMouseDragStart(new MouseEventArgs(e.Button, e.Clicks, mouseDownPos[button].X, mouseDownPos[button].Y, e.Delta));

        if(dragButton != MouseButtons.None) // if the drag wasn't immediately cancelled
        {
          Capture = true; // capture the mouse so we can be sure to receive the end of the drag
          // then issue a drag event because the mouse has since moved. always specify the original drag button.
          OnMouseDrag(new MouseDragEventArgs(dragButton, mouseDownPos[ButtonToIndex(dragButton)], e.X, e.Y, xd, yd));
        }
      }
    }
  }

  protected override void OnMouseUp(MouseEventArgs e)
  {
    base.OnMouseUp(e);

    int button = ButtonToIndex(e.Button);
    if(button == -1) return; // ignore unsupported buttons

    if(dragButton == e.Button) // if we're currently dragging, end the drag
    {
      OnMouseDragEnd(new MouseDragEventArgs(dragButton, mouseDownPos[ButtonToIndex(dragButton)], e.X, e.Y, 0, 0)); // specify the original drag button
      dragButton = MouseButtons.None; // clear our drag button flag
      Capture = false; // stop capturing the mouse so other things can use it
    }
    else if(IsMouseDown(button)) // otherwise we're not currently dragging. was the button pressed over the control?
    {
      base.OnMouseClick(e); // yes, so now that it's been released, signal a click event.
    }

    mouseDownPos[button] = new Point(-1, -1); // in any case, mark the button as released.
  }

  bool IsMouseDown(int index) { return mouseDownPos[index].X >= 0; }

  Point[] mouseDownPos = new Point[3] { new Point(-1, -1), new Point(-1, -1), new Point(-1, -1) };
  Point lastDragPos;
  MouseButtons dragButton = MouseButtons.None;

  static int ButtonToIndex(MouseButtons button)
  {
    return button == MouseButtons.Left ? 0 : button == MouseButtons.Middle ? 1 : button == MouseButtons.Right ? 2 : -1;
  }
  #endregion

  void DisposeBuffer()
  {
    if(buffer != null)
    {
      buffer.Dispose();
      buffer = null;
    }
    if(image != null)
    {
      image.Dispose();
      image = null;
    }
  }
  
  GLBuffer GetBuffer()
  {
    if(buffer == null)
    {
      buffer = new GLBuffer(Width, Height);
    }
    return buffer;
  }

  GLBuffer buffer;
  Bitmap   image;
  bool     needRender = true;
}
#endregion

} // namespace RotationalForce.Editor