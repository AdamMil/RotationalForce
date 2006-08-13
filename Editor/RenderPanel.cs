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
    try
    {
      osbCreator.Create(ref hdc, ref hbitmap, ref hglrc, width, height);
    }
    catch
    {
      Dispose(false);
      throw;
    }

    if(globalBuffer != null)
    {
      if(wglShareLists(globalBuffer.hglrc, hglrc) == 0)
      {
        throw Failed("wglShareLists");
      }
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

  public static void Initialize()
  {
    EditorApp.Log("Initializing OpenGL rendering context");

    IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
    if(hdc == IntPtr.Zero) throw Failed("CreateCompatibleDC");
    EditorApp.Log("Got compatible DC. Enumerating available pixel formats...");
    
    try
    {
      uint size = (uint)Marshal.SizeOf(typeof(PixelFormatDescriptor));
      PixelFormatDescriptor pfd, bestPfd = new PixelFormatDescriptor();
      int maxFormat = DescribePixelFormat(hdc, 1, size, out pfd);
      int bestFormat = 0, bestScore = 0;
      if(maxFormat == 0) throw Failed("DescribePixelFormat");

      for(int format=1; format<=maxFormat; format++)
      {
        DescribePixelFormat(hdc, format, size, out pfd);
        int score = LogPixelFormat(format, ref pfd);

        if(score > bestScore)
        {
          bestScore  = score;
          bestFormat = format;
          bestPfd    = pfd;
        }
      }

      EditorApp.Log("Best format: " + bestFormat);
      if(bestFormat == 0) throw Failed("No suitable pixel format found!");
      
      int tries = 0;
      while(true)
      {
        EditorApp.Log("Attempting offscreen buffer creation with device-dependent bitmaps.");
        osbCreator = new OsbCreator(bestFormat, bestPfd, true);

        try
        {
          try
          {
            globalBuffer = new GLBuffer(16, 16);
            break;
          }
          catch(Exception e)
          {
            EditorApp.Log("Buffer creation failed. Error was: "+e.ToString());
            EditorApp.Log("Retrying with device-independent bitmaps.");

            osbCreator = new OsbCreator(bestFormat, bestPfd, false);
            globalBuffer = new GLBuffer(16, 16);
            break;
          }
        }
        catch(Exception e)
        {
          EditorApp.Log("Buffer creation failed. Error was: "+e.ToString());
          if(tries == 0)
          {
            EditorApp.Log("Asking the card what pixel format we should be using.");
            pfd = new PixelFormatDescriptor();
            pfd.nSize       = (ushort)size;
            pfd.nVersion    = 1;
            pfd.dwFlags     = PFlag.DrawToBitmap | PFlag.SupportOpengl;
            pfd.iPixelType  = PixelType.RGBA;
            pfd.cColorBits  = 32;
            
            bestFormat = ChoosePixelFormat(hdc, ref pfd);
            if(bestFormat != 0)
            {
              DescribePixelFormat(hdc, bestFormat, size, out bestPfd);
            }

            if(bestFormat == 0 || bestPfd.cColorBits < 16)
            {
              EditorApp.Log("The card recommended no pixel formats. Retrying with double buffering allowed.");
              pfd.dwFlags |= PFlag.DoublebufferDontcare;
              bestFormat = ChoosePixelFormat(hdc, ref pfd);
              if(bestFormat != 0)
              {
                DescribePixelFormat(hdc, bestFormat, size, out bestPfd);
              }

              if(bestFormat == 0 || bestPfd.cColorBits < 16)
              {
                EditorApp.Log("The card recommended no usable pixel formats at all.");
                EditorApp.Log("Giving up.");
                throw;
              }
            }

            LogPixelFormat(bestFormat, ref bestPfd);
            tries++;
          }
          else
          {
            EditorApp.Log("Giving up.");
            throw;
          }
        }
      }

      GLBuffer.SetCurrent(globalBuffer);
    }
    finally
    {
      DeleteDC(hdc);
    }
  }

  public static void SetCurrent(GLBuffer buffer)
  {
    if(buffer == null)
    {
      if(wglMakeCurrent(globalBuffer.hdc, globalBuffer.hglrc) == 0)
      {
        throw Failed("wglMakeCurrent(null)");
      }
      currentBuffer = null;
    }
    else
    {
      if(wglMakeCurrent(buffer.hdc, buffer.hglrc) == 0)
      {
        throw Failed("wglMakeCurrent");
      }
      currentBuffer = new WeakReference(buffer);
    }
  }

  #region OsbCreator
  sealed class OsbCreator
  {
    public OsbCreator(int formatIndex, PixelFormatDescriptor format, bool useDDB)
    {
      this.pixelFormatIndex = formatIndex;
      this.pixelFormat      = format;
      this.useDDB           = useDDB;
    }

    public void Create(ref IntPtr hdc, ref IntPtr hbitmap, ref IntPtr hglrc, int width, int height)
    {
      EditorApp.Log("Creating {0}x{1} offscreen buffer", width, height);

      IntPtr screenDC = GetDC(IntPtr.Zero);
      if(screenDC == IntPtr.Zero)
      {
        throw Failed("GetDC");
      }

      try
      {
        hdc = CreateCompatibleDC(screenDC);
        if(hdc == IntPtr.Zero)
        {
          throw Failed("CreateCompatibleDC");
        }

        if(useDDB)
        {
          hbitmap = CreateCompatibleBitmap(screenDC, width, height);
          if(hbitmap == IntPtr.Zero)
          {
            throw Failed("CreateCompatibleBitmap");
          }
        }
        else
        {
          BitmapInfo bmp = new BitmapInfo();
          bmp.Size     = Marshal.SizeOf(typeof(BitmapInfo));
          bmp.Width    = width;
          bmp.Height   = height;
          bmp.Planes   = 1;
          bmp.BitCount = pixelFormat.cColorBits;

          hbitmap = CreateDIBSection(hdc, ref bmp, 0, IntPtr.Zero, IntPtr.Zero, 0);
          if(hbitmap == IntPtr.Zero)
          {
            throw Failed("CreateDIBSection");
          }
        }

        if(SelectObject(hdc, hbitmap) == IntPtr.Zero)
        {
          throw Failed("SelectObject");
        }

        if(SetPixelFormat(hdc, pixelFormatIndex, ref pixelFormat) == 0)
        {
          throw Failed("SetPixelFormat");
        }

        hglrc = wglCreateContext(hdc);
        if(hglrc == IntPtr.Zero)
        {
          throw Failed("wglCreateContext");
        }
      }
      finally
      {
        DeleteDC(screenDC);
      }
    }
    
    PixelFormatDescriptor pixelFormat;
    int pixelFormatIndex;
    bool useDDB;
  }
  #endregion

  static Exception Failed(string message)
  {
    return new ApplicationException("Failure: "+message+". Last error: "+GetLastError());
  }
  
  static int LogPixelFormat(int formatIndex, ref PixelFormatDescriptor pfd)
  {
    bool accelerated  = (pfd.dwFlags&PFlag.GenericAccelerated) != 0;
    bool hwSupport    = (pfd.dwFlags&PFlag.GenericFormat) == 0;
    bool drawToBitmap = (pfd.dwFlags&PFlag.DrawToBitmap) != 0;
    bool openGL       = (pfd.dwFlags&PFlag.SupportOpengl) != 0;
    bool isStereo     = (pfd.dwFlags&PFlag.Stereo) != 0;
    bool doubleBuffer = (pfd.dwFlags&PFlag.Doublebuffer) != 0;
    bool isRGBA       = pfd.iPixelType == PixelType.RGBA;

    int score = accelerated ? 20 : hwSupport ? 10 : 5;
    score += pfd.cColorBits == 32 ? 35 : pfd.cColorBits == 24 ? 30 : pfd.cColorBits == 16 ? 5 : 0;

    if(!drawToBitmap || !openGL || isStereo || !isRGBA || pfd.cColorBits == 8) score = 0;
    else
    {
      if(doubleBuffer) score--;
      score -= pfd.cDepthBits / 16;
    }

    EditorApp.Log("Format #"+formatIndex);
    EditorApp.Log("Color bits: "+pfd.cColorBits);
    EditorApp.Log("Depth bits: "+pfd.cDepthBits);
    EditorApp.Log("Stencil bits: "+pfd.cStencilBits);
    EditorApp.Log("Supported directly by hardware: {0}", hwSupport ? "Yes" : "No");
    EditorApp.Log("Hardware accelerated: {0}", accelerated ? "Yes" : "No");
    EditorApp.Log("Double buffered: {0}", doubleBuffer ? "Yes" : "No");
    EditorApp.Log("Supports rendering to bitmaps: {0}", drawToBitmap ? "Yes" : "No**");
    EditorApp.Log("Supports OpenGL: {0}", openGL ? "Yes" : "No**");
    EditorApp.Log("Stereoscopic: {0}", isStereo ? "Yes**" : "No");
    EditorApp.Log("Pixel type: {0}", isRGBA ? "RGB" : "Palette**");
    EditorApp.Log("Score: "+score);
    EditorApp.Log("");
    
    return score;
  }

  [ThreadStatic] static WeakReference currentBuffer;

  static OsbCreator osbCreator;
  static GLBuffer globalBuffer;

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

  [Flags] enum PixelType : byte { RGBA=0, Palette=1 }

  [StructLayout(LayoutKind.Sequential)]
  struct PixelFormatDescriptor
  {
    public ushort nSize, nVersion;
    public PFlag dwFlags;
    public PixelType iPixelType;
    public byte cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift,
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

  [DllImport("user32.dll")]
  static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("gdi32.dll")]
  static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
  [DllImport("gdi32.dll")]
  static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BitmapInfo bmp, uint usage,
                                        IntPtr data, IntPtr section, uint offset);
  [DllImport("gdi32.dll")]
  static extern IntPtr CreateCompatibleDC(IntPtr hdc);
  [DllImport("gdi32.dll")]
  static extern int ChoosePixelFormat(IntPtr hdc, [In] ref PixelFormatDescriptor pfmt);
  [DllImport("gdi32.dll")]
  static extern int DescribePixelFormat(IntPtr hdc, int formatIndex, uint bufferSize, out PixelFormatDescriptor pfmt);
  [DllImport("gdi32.dll")]
  static extern int SetPixelFormat(IntPtr hdc, int iPixelFormat, [In] ref PixelFormatDescriptor pfmt);
  [DllImport("gdi32.dll")]
  static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
  [DllImport("gdi32.dll")]
  static extern int DeleteObject(IntPtr handle);
  [DllImport("gdi32.dll")]
  static extern int DeleteDC(IntPtr hdc);
  [DllImport("opengl32.dll")]
  static extern int wglCopyContext(IntPtr hglrcSrc, IntPtr hglrcDest, uint attributes);
  [DllImport("opengl32.dll")]
  static extern IntPtr wglCreateContext(IntPtr hdc);
  [DllImport("opengl32.dll")]
  static extern int wglDeleteContext(IntPtr hglrc);
  [DllImport("opengl32.dll")]
  static extern int wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
  [DllImport("opengl32.dll")]
  static extern int wglShareLists(IntPtr hglrcSrc, IntPtr hglrcDest);
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

  public event PaintEventHandler RenderBackground;

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
      Engine.Engine.ResetOpenGL(Width, Height, ClientRectangle);
      RenderBackground(this, e);
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