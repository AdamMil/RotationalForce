using System;
using System.Drawing;
using System.Windows.Forms;
using GameLib.Interop.OpenGL;
using RotationalForce.Engine;
using GLPoint = GameLib.Mathematics.TwoD.Point;
using GLPoly  = GameLib.Mathematics.TwoD.Polygon;

namespace RotationalForce.Editor
{

class RenderPanel : Panel
{
  protected override void OnPaintBackground(PaintEventArgs e)
  {
  }
}

class VectorAnimationEditor : Form
{
  MenuStrip menuBar;
  ListBox listBox;
  ToolStripMenuItem editMenu;
  CheckBox chkAsk;
  Button btnUp;
  Button btnDown;
  Button btnNew;
  Button btnDelete;
  Button btnBackColor;
  RenderPanel renderPanel;
  CheckBox chkDrawToCurrent;
  ToolStripMenuItem animationModeMenuItem;
  PropertyGrid propertyGrid;

  public VectorAnimationEditor()
  {
    InitializeComponent();
  }

  public void CreateNew()
  {
    animation = new VectorAnimation();
    animation.AddFrame(new VectorAnimation.Frame());
    Mode = EditMode.Animation;
  }

  enum EditMode
  {
    Invalid, Animation, Frame, Polygon, Vertex
  }

  EditMode Mode
  {
    get { return mode; }
    set
    {
      if(value != mode)
      {
        int select = -1;

        switch(value)
        {
          case EditMode.Animation:
            propertyGrid.SelectedObject = animation;
            select = selectedFrame;
            break;
          case EditMode.Frame:
            propertyGrid.SelectedObject = SelectedFrame;
            select = selectedPolygon;
            break;
          case EditMode.Polygon:
            if(SelectedFrame.Polygons.Count == 0) return;
            else if(selectedPolygon == -1) selectedPolygon = 0;
            else if(selectedPolygon >= SelectedFrame.Polygons.Count) selectedPolygon = SelectedFrame.Polygons.Count-1;
            propertyGrid.SelectedObject = SelectedPolygon;
            break;
          case EditMode.Vertex:
            propertyGrid.SelectedObject = SelectedVertex;
            break;
        }

        btnUp.Enabled = btnDown.Enabled = (value == EditMode.Animation || value == EditMode.Frame);
        btnNew.Enabled = btnDelete.Enabled = listBox.Enabled = (value != EditMode.Vertex);

        mode = value;
        PopulateListbox(select);
        InvalidateRender();
      }
    }
  }

  void ReorderListItem(int distance)
  {
    // don't move the first item up or the last item down
    if(distance<0 && listBox.SelectedIndex-distance < 0 ||
       distance>0 && listBox.SelectedIndex+distance >= listBox.Items.Count)
    {
      return;
    }

    switch(Mode)
    {
      case EditMode.Animation:
      {
        VectorAnimation.Frame frame = animation.Frames[listBox.SelectedIndex];
        animation.RemoveFrame(listBox.SelectedIndex);
        animation.InsertFrame(listBox.SelectedIndex + distance, frame);
        PopulateListbox(listBox.SelectedIndex + distance);
        break;
      }

      case EditMode.Frame:
      {
        VectorAnimation.Polygon poly = SelectedFrame.Polygons[listBox.SelectedIndex];
        SelectedFrame.RemovePolygon(listBox.SelectedIndex);
        SelectedFrame.InsertPolygon(listBox.SelectedIndex + distance, poly);
        PopulateListbox(listBox.SelectedIndex + distance);
        break;
      }
    }
  }

  void PopulateListbox(int select)
  {
    listBox.Items.Clear();
    switch(Mode)
    {
      case EditMode.Animation:
      {
        double time = 0.0;
        foreach(VectorAnimation.Frame frame in animation.Frames)
        {
          listBox.Items.Add("Frame @ " + time.ToString("n"));
          time += frame.FrameTime;
        }
        break;
      }

      case EditMode.Frame:
        for(int i=0; i<SelectedFrame.Polygons.Count; i++)
        {
          listBox.Items.Add("Polygon " + (i+1).ToString());
        }
        break;
    }

    btnDelete.Enabled = listBox.Items.Count != 0;

    if(select != -1 && listBox.Items.Count != 0)
    {
      listBox.SelectedIndex = select >= listBox.Items.Count ? listBox.Items.Count-1 : select;
    }
  }

  #region Rendering
  public Color RenderForeColor
  {
    get
    {
      return Color.FromArgb(255-renderPanel.BackColor.R, 255-renderPanel.BackColor.G, 255-renderPanel.BackColor.B);
    }
  }

  // invalidate the things we add to the scene, but not the scene itself.
  void InvalidateDecoration()
  {
    renderPanel.Invalidate();
  }

  void InvalidateRender() { InvalidateRender(false); }

  // invalidate the scene (if it's not already invalidated) as well as the decoration
  void InvalidateRender(bool renderImmediately)
  {
    if(!needRender)
    {
      needRender = true;
      if(renderImmediately) RenderFrame();
      renderPanel.Invalidate();
    }
  }
  
  void RenderFrame()
  {
    if(buffer == null)
    {
      int size = Math.Min(renderPanel.Width, renderPanel.Height) * 9 / 10;
      buffer = new GLBuffer(size, size);
    }
    
    GLBuffer.SetCurrent(buffer);
    Engine.Engine.ResetOpenGL(buffer.Width, buffer.Height);

    GL.glClearColor(renderPanel.BackColor);
    GL.glClear(GL.GL_COLOR_BUFFER_BIT);

    GL.glBegin(GL.GL_LINES);
    GL.glColor(RenderForeColor);

    // draw a grid
    float pos=0, add = (buffer.Width-1)/10f;
    for(int i=0; i<11; pos+=add,i++)
    {
      // horizontal
      GL.glVertex2f(0, pos);
      GL.glVertex2f(buffer.Width-1, pos);
      // vertical
      GL.glVertex2f(pos, 0);
      GL.glVertex2f(pos, buffer.Height-1);
    }
    GL.glEnd();

    // now draw the polygons
    GL.glMatrixMode(GL.GL_PROJECTION);
    GL.glLoadIdentity();
    GLU.gluOrtho2D(-1, 1, 1, -1);

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glLoadIdentity();

    for(int i=0,last=GetTopmostVisiblePolygonIndex(); i<=last; i++)
    {
      SelectedFrame.Polygons[i].Render();
    }

    GL.glFlush();
    GLBuffer.SetCurrent(null);
    
    if(backgroundImage != null) backgroundImage.Dispose();
    backgroundImage = buffer.CreateBitmap();

    needRender = false;
  }

  private void renderPanel_Paint(object sender, PaintEventArgs e)
  {
    if(needRender) RenderFrame();
    e.Graphics.DrawImageUnscaled(backgroundImage, (renderPanel.Width-buffer.Width)/2,
                                 (renderPanel.Height-buffer.Height)/2);

    VectorAnimation.Polygon poly = SelectedPolygon;
    if(poly != null)
    {
      Point[] points = new Point[poly.Vertices.Count];
      for(int i=0; i<points.Length; i++)
      {
        points[i] = LocalToControl(poly.Vertices[i].Position);
      }

      if(points.Length > 1)
      {
        e.Graphics.DrawLines(Pens.Green, points);
        e.Graphics.DrawLine(Pens.Green, points[points.Length-1], points[0]);
      }
      
      for(int i=0; i<points.Length; i++)
      {
        e.Graphics.FillEllipse(i==selectedVertex ? Brushes.Red : Brushes.Green, points[i].X-2, points[i].Y-2, 5, 5);
      }
    }
  }

  Bitmap backgroundImage;
  GLBuffer buffer;
  bool needRender = true;
  #endregion

  void SelectListItem()
  {
    if(listBox.SelectedIndex == -1) return;

    switch(Mode)
    {
      case EditMode.Animation:
        selectedFrame = listBox.SelectedIndex;
        Mode = EditMode.Frame;
        break;
      case EditMode.Frame:
        SelectPolygon(listBox.SelectedIndex);
        break;
    }
  }

  VectorAnimation animation;
  int selectedFrame, selectedPolygon = -1, selectedVertex = -1;
  EditMode mode;

  #region InitializeComponent
  void InitializeComponent()
  {
    System.Windows.Forms.ToolStripMenuItem polygonModeMenuItem;
    this.animationModeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.renderPanel = new RenderPanel();
    this.btnUp = new System.Windows.Forms.Button();
    this.btnDown = new System.Windows.Forms.Button();
    this.btnNew = new System.Windows.Forms.Button();
    this.btnDelete = new System.Windows.Forms.Button();
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.menuBar = new System.Windows.Forms.MenuStrip();
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.listBox = new System.Windows.Forms.ListBox();
    this.chkAsk = new System.Windows.Forms.CheckBox();
    this.btnBackColor = new System.Windows.Forms.Button();
    this.chkDrawToCurrent = new System.Windows.Forms.CheckBox();
    polygonModeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.menuBar.SuspendLayout();
    this.SuspendLayout();
    // 
    // polygonModeMenuItem
    // 
    polygonModeMenuItem.Name = "polygonModeMenuItem";
    polygonModeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
    polygonModeMenuItem.Size = new System.Drawing.Size(189, 22);
    polygonModeMenuItem.Tag = RotationalForce.Editor.VectorAnimationEditor.EditMode.Polygon;
    polygonModeMenuItem.Text = "&Polygon Mode";
    polygonModeMenuItem.Click += new System.EventHandler(this.editModeMenuItem_Click);
    // 
    // animationModeMenuItem
    // 
    this.animationModeMenuItem.Name = "animationModeMenuItem";
    this.animationModeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
    this.animationModeMenuItem.Size = new System.Drawing.Size(189, 22);
    this.animationModeMenuItem.Tag = RotationalForce.Editor.VectorAnimationEditor.EditMode.Animation;
    this.animationModeMenuItem.Text = "&Animation Mode";
    this.animationModeMenuItem.Click += new System.EventHandler(this.editModeMenuItem_Click);
    // 
    // renderPanel
    // 
    this.renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
    this.renderPanel.BackColor = System.Drawing.Color.Black;
    this.renderPanel.Location = new System.Drawing.Point(3, 3);
    this.renderPanel.Name = "renderPanel";
    this.renderPanel.Size = new System.Drawing.Size(465, 453);
    this.renderPanel.TabIndex = 1;
    this.renderPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseDown);
    this.renderPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseMove);
    this.renderPanel.Resize += new System.EventHandler(this.renderPanel_Resize);
    this.renderPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.renderPanel_Paint);
    this.renderPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseUp);
    // 
    // btnUp
    // 
    this.btnUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnUp.Location = new System.Drawing.Point(655, 260);
    this.btnUp.Name = "btnUp";
    this.btnUp.Size = new System.Drawing.Size(64, 23);
    this.btnUp.TabIndex = 3;
    this.btnUp.Text = "&Up";
    this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
    // 
    // btnDown
    // 
    this.btnDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnDown.Location = new System.Drawing.Point(655, 289);
    this.btnDown.Name = "btnDown";
    this.btnDown.Size = new System.Drawing.Size(64, 23);
    this.btnDown.TabIndex = 4;
    this.btnDown.Text = "&Down";
    this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
    // 
    // btnNew
    // 
    this.btnNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnNew.Location = new System.Drawing.Point(655, 318);
    this.btnNew.Name = "btnNew";
    this.btnNew.Size = new System.Drawing.Size(64, 23);
    this.btnNew.TabIndex = 5;
    this.btnNew.Text = "&New";
    this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
    // 
    // btnDelete
    // 
    this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnDelete.Location = new System.Drawing.Point(655, 347);
    this.btnDelete.Name = "btnDelete";
    this.btnDelete.Size = new System.Drawing.Size(64, 23);
    this.btnDelete.TabIndex = 6;
    this.btnDelete.Text = "&Delete";
    this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
    // 
    // editMenu
    // 
    this.editMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.animationModeMenuItem,
            polygonModeMenuItem});
    this.editMenu.MergeAction = System.Windows.Forms.MergeAction.Insert;
    this.editMenu.MergeIndex = 1;
    this.editMenu.Name = "editMenu";
    this.editMenu.Size = new System.Drawing.Size(37, 20);
    this.editMenu.Text = "Edit";
    this.editMenu.DropDownOpening += new System.EventHandler(this.editMenu_DropDownOpening);
    // 
    // menuBar
    // 
    this.menuBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editMenu});
    this.menuBar.Location = new System.Drawing.Point(0, 0);
    this.menuBar.Name = "menuBar";
    this.menuBar.Size = new System.Drawing.Size(707, 24);
    this.menuBar.TabIndex = 0;
    this.menuBar.Visible = false;
    // 
    // propertyGrid
    // 
    this.propertyGrid.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Right)));
    this.propertyGrid.Location = new System.Drawing.Point(474, 3);
    this.propertyGrid.Name = "propertyGrid";
    this.propertyGrid.Size = new System.Drawing.Size(245, 251);
    this.propertyGrid.TabIndex = 1;
    this.propertyGrid.ToolbarVisible = false;
    this.propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid_PropertyValueChanged);
    // 
    // listBox
    // 
    this.listBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.listBox.IntegralHeight = false;
    this.listBox.Location = new System.Drawing.Point(474, 260);
    this.listBox.Name = "listBox";
    this.listBox.Size = new System.Drawing.Size(175, 196);
    this.listBox.TabIndex = 2;
    this.listBox.DoubleClick += new System.EventHandler(this.listBox_DoubleClick);
    this.listBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.listBox_KeyPress);
    // 
    // chkAsk
    // 
    this.chkAsk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.chkAsk.Checked = true;
    this.chkAsk.CheckState = System.Windows.Forms.CheckState.Checked;
    this.chkAsk.Location = new System.Drawing.Point(655, 377);
    this.chkAsk.Name = "chkAsk";
    this.chkAsk.Size = new System.Drawing.Size(64, 17);
    this.chkAsk.TabIndex = 7;
    this.chkAsk.Text = "Ask";
    // 
    // btnBackColor
    // 
    this.btnBackColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnBackColor.Location = new System.Drawing.Point(655, 433);
    this.btnBackColor.Name = "btnBackColor";
    this.btnBackColor.Size = new System.Drawing.Size(64, 23);
    this.btnBackColor.TabIndex = 9;
    this.btnBackColor.Text = "Bk. Color";
    this.btnBackColor.Click += new System.EventHandler(this.btnBackColor_Click);
    // 
    // chkDrawToCurrent
    // 
    this.chkDrawToCurrent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.chkDrawToCurrent.Location = new System.Drawing.Point(655, 396);
    this.chkDrawToCurrent.Name = "chkDrawToCurrent";
    this.chkDrawToCurrent.Size = new System.Drawing.Size(64, 31);
    this.chkDrawToCurrent.TabIndex = 8;
    this.chkDrawToCurrent.Text = "Draw to current";
    this.chkDrawToCurrent.TextAlign = System.Drawing.ContentAlignment.TopLeft;
    this.chkDrawToCurrent.CheckedChanged += new System.EventHandler(this.chkDrawToCurrent_CheckedChanged);
    // 
    // VectorAnimationEditor
    // 
    this.ClientSize = new System.Drawing.Size(722, 458);
    this.Controls.Add(this.chkDrawToCurrent);
    this.Controls.Add(this.btnBackColor);
    this.Controls.Add(this.chkAsk);
    this.Controls.Add(this.listBox);
    this.Controls.Add(this.btnDelete);
    this.Controls.Add(this.btnNew);
    this.Controls.Add(this.btnDown);
    this.Controls.Add(this.btnUp);
    this.Controls.Add(this.propertyGrid);
    this.Controls.Add(this.renderPanel);
    this.Controls.Add(this.menuBar);
    this.MainMenuStrip = this.menuBar;
    this.Name = "VectorAnimationEditor";
    this.Text = "Vector Animation";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
    this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.VectorAnimationEditor_FormClosed);
    this.menuBar.ResumeLayout(false);
    this.menuBar.PerformLayout();
    this.ResumeLayout(false);
    this.PerformLayout();

  }
  #endregion
  
  #region Event handlers
  void editModeMenuItem_Click(object sender, EventArgs e)
  {
    Mode = (EditMode)((ToolStripMenuItem)sender).Tag;
  }

  void btnUp_Click(object sender, EventArgs e)
  {
    ReorderListItem(-1);
  }

  void btnDown_Click(object sender, EventArgs e)
  {
    ReorderListItem(1);
  }

  void btnNew_Click(object sender, EventArgs e)
  {
    switch(Mode)
    {
      case EditMode.Animation:
        animation.InsertFrame(listBox.SelectedIndex+1, new VectorAnimation.Frame());
        PopulateListbox(listBox.SelectedIndex + 1);
        break;
      case EditMode.Frame:
        SelectedFrame.InsertPolygon(listBox.SelectedIndex+1, new VectorAnimation.Polygon());
        PopulateListbox(listBox.SelectedIndex + 1);
        break;
    }
  }

  void btnDelete_Click(object sender, EventArgs e)
  {
    // if we're in animation or frame mode, and the "Ask" checkbox is checked, verify deletion
    if((Mode == EditMode.Animation || Mode == EditMode.Frame) && chkAsk.Checked &&
       MessageBox.Show("Are you sure you want to delete this item?", "Confirm deletion", MessageBoxButtons.YesNo,
                       MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1) == DialogResult.No)
    {
      return;
    }

    switch(Mode)
    {
      case EditMode.Animation:
        if(listBox.Items.Count == 1) // we can't delete the last frame, but we can clear it
        {
          animation.RemoveFrame(0);
          animation.AddFrame(new VectorAnimation.Frame());
          MessageBox.Show("The last frame cannot be deleted, so it was cleared.", "Frame cleared",
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
          animation.RemoveFrame(listBox.SelectedIndex);
          PopulateListbox(listBox.SelectedIndex);
        }
        break;

      case EditMode.Frame:
        SelectedFrame.RemovePolygon(listBox.SelectedIndex);
        PopulateListbox(listBox.SelectedIndex);
        if(listBox.Items.Count == 0) selectedPolygon = -1;
        break;
    }
    
    InvalidateRender();
  }

  void listBox_DoubleClick(object sender, EventArgs e)
  {
    SelectListItem();
  }

  void editMenu_DropDownOpening(object sender, EventArgs e)
  {
    foreach(ToolStripMenuItem item in editMenu.DropDownItems)
    {
      if(item.Tag is EditMode)
      {
        EditMode menuMode = (EditMode)item.Tag;
        item.Checked = menuMode == Mode;
        item.Enabled = menuMode == EditMode.Animation || menuMode == EditMode.Frame ||
                       menuMode == EditMode.Polygon && selectedPolygon != -1;
      }
    }
  }

  void btnBackColor_Click(object sender, EventArgs e)
  {
    ColorDialog picker = new ColorDialog();
    picker.Color = renderPanel.BackColor;
    if(picker.ShowDialog() == DialogResult.OK) renderPanel.BackColor = picker.Color;
    InvalidateRender();
  }

  void renderPanel_Resize(object sender, EventArgs e)
  {
    if(buffer != null) // dispose of the current buffer so a new one will be created of the proper size
    {
      buffer.Dispose();
      buffer = null;
    }

    InvalidateRender(true); // and force re-rendering
  }

  void VectorAnimationEditor_FormClosed(object sender, FormClosedEventArgs e)
  {
    if(buffer != null)
    {
      buffer.Dispose();
      buffer = null;
    }
    if(backgroundImage != null)
    {
      backgroundImage.Dispose();
      backgroundImage = null;
    }
  }

  void chkDrawToCurrent_CheckedChanged(object sender, EventArgs e)
  {
    if(mode != EditMode.Animation && selectedPolygon < SelectedFrame.Polygons.Count-1) InvalidateRender();
  }

  void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
  {
    // if we've edited a polygon or a vertex, re-render the scene
    switch(Mode)
    {
      case EditMode.Polygon:
        InvalidateRender();
        break;
      case EditMode.Vertex:
        // and since vertices are value objects, we need to replace them by hand
        ReplaceSelectedVertex((VectorAnimation.Vertex)propertyGrid.SelectedObject);
        InvalidateRender();
        break;
    }
  }

  void listBox_KeyPress(object sender, KeyPressEventArgs e)
  {
    if(e.KeyChar == '\r')
    {
      SelectListItem();
    }
  }
  #endregion

  #region Modification of vertices and polygons
  void CreateNewPolygon(Point pt)
  {
    VectorAnimation.Polygon poly = new VectorAnimation.Polygon();
    SelectedFrame.AddPolygon(poly);
    SelectPolygon(SelectedFrame.Polygons.Count - 1);

    VectorAnimation.Vertex vertex = new VectorAnimation.Vertex();
    vertex.Color    = Color.White;
    vertex.Position = ControlToLocal(pt);
    poly.AddVertex(vertex);
    SelectVertex(0);
  }
  
  void MoveSelectedVertexTo(Point pt)
  {
    VectorAnimation.Vertex vertex = SelectedVertex;
    vertex.Position = ControlToLocal(pt);
    ReplaceSelectedVertex(vertex);
    InvalidateDecoration();
  }
  
  void MoveSelectedPolygon(Size offset)
  {
    if(offset.IsEmpty) return;

    // convert the offset to local coordinates
    GLPoint localOffset = new GLPoint(offset.Width *2/(double)buffer.Width,
                                      offset.Height*2/(double)buffer.Height);

    VectorAnimation.Polygon polygon = SelectedPolygon;

    // create a copy of the polygon's vertices
    VectorAnimation.Vertex[] vertices = new VectorAnimation.Vertex[polygon.Vertices.Count];
    polygon.Vertices.CopyTo(vertices, 0);
    polygon.ClearVertices(); // and then clear them

    for(int i=0; i<vertices.Length; i++) // offset each vertex and add it back to the polygon
    {
      vertices[i].Position = new GLPoint(vertices[i].Position.X+localOffset.X,
                                         vertices[i].Position.Y+localOffset.Y);
      polygon.AddVertex(vertices[i]);
    }
    
    // since the vertices are value types, they won't automatically be updated in the property grid. do it manually.
    if(selectedVertex != -1) propertyGrid.SelectedObject = SelectedVertex;

    InvalidateDecoration();
  }
  
  void ReplaceSelectedVertex(VectorAnimation.Vertex vertex)
  {
    SelectedPolygon.RemoveVertex(selectedVertex);
    SelectedPolygon.InsertVertex(selectedVertex, vertex);
  }
  #endregion

  #region Selecting items in the UI and data structures
  /// <summary>Gets a the current frame. This is guaranteed to be non-null.</summary>
  VectorAnimation.Frame SelectedFrame
  {
    get { return animation.Frames[selectedFrame]; }
  }
  
  /// <summary>Gets the currently selected polygon. This will be null if no polygon is selected.</summary>
  VectorAnimation.Polygon SelectedPolygon
  {
    get { return selectedPolygon == -1 ? null : SelectedFrame.Polygons[selectedPolygon]; }
  }
  
  /// <summary>Gets the currently selected vertex. This will throw an exception if no vertex is selected.</summary>
  VectorAnimation.Vertex SelectedVertex
  {
    get { return SelectedPolygon.Vertices[selectedVertex]; }
  }

  void CloneAndSelectVertex(int index)
  {
    SelectedPolygon.InsertVertex(index, SelectedPolygon.Vertices[index]);
    SelectVertex(index + 1);
  }

  /// <summary>Selects a polygon in the current frame, given its index.</summary>
  /// <remarks>If the index doesn't match the currently-selected polygon, the selected vertex will be reset.
  /// The mode will be set to Polygon.
  /// </remarks>
  void SelectPolygon(int index)
  {
    if(index<0 || index>=SelectedFrame.Polygons.Count) throw new ArgumentOutOfRangeException("index");

    if(index != selectedPolygon) // if selecting a different polygon
    {
      selectedPolygon = index; // set the index
      selectedVertex  = -1;    // clear the vertex

      if(Mode == EditMode.Polygon) // if we're already in polygon mode, select the new polygon into the property editor
      {
        propertyGrid.SelectedObject = SelectedPolygon;
      }
      else // otherwise switch to polygon mode, which will take care of the selection too.
      {
        Mode = EditMode.Polygon;
      }

      if(chkDrawToCurrent.Checked) InvalidateRender();
      InvalidateDecoration(); // and invalidate the decoration
    }
    else if(Mode != EditMode.Polygon) // otherwise, if it's the same polygon, but we're in the wrong mode
    {
      Mode = EditMode.Polygon; // switch to polygon mode
    }
  }
  
  void SelectVertex(int index)
  {
    if(SelectedPolygon == null) throw new InvalidOperationException("No polygon selected.");
    if(index<0 || index>=SelectedPolygon.Vertices.Count) throw new ArgumentOutOfRangeException("index");

    if(index != selectedVertex) // if selecting a different vertex
    {
      selectedVertex = index;
      if(Mode == EditMode.Vertex) // if already in vertex mode, select the new vertex into the property grid
      {
        propertyGrid.SelectedObject = SelectedVertex;
      }
      else // otherwise, switch to vertex mode, which will take care of the selection too.
      {
        Mode = EditMode.Vertex;
      }
    }
    else if(Mode != EditMode.Vertex) // otherwise, if it's the same vertex, but we're in the wrong mode
    {
      Mode = EditMode.Vertex; // switch to vertex mode
    }
  }

  /// <summary>If a polygon is selected, this method deselects it and sets the mode to Frame.</summary>
  void DeselectPolygon()
  {
    if(selectedPolygon != -1)
    {
      selectedPolygon = -1;
      selectedVertex  = -1;
      Mode = EditMode.Frame;
    }
  }
  
  void DeselectVertex()
  {
    if(selectedVertex != -1)
    {
      selectedVertex = -1;
      SelectPolygon(selectedPolygon);
    }
  }
  #endregion

  #region Selecting and finding items spatially
  public GLPoint ControlToLocal(Point controlPoint)
  {
    Point topLeft = GetControlRenderRect().Location; // get the top-left corner of the render area
    controlPoint.Offset(-topLeft.X, -topLeft.Y);     // orient the control point to the render area
    // first multiply by two because the total range of local space is 2 units (-1 to 1), then divide by the size of
    // the render area minus one pixel. one pixel is subtracted because we want the rightmost pixel to be 2.0, not
    // slightly less than 2.0. finally, one is subtracted to shift it from the range 0-2 to -1 to 1
    return new GLPoint(controlPoint.X*2/(double)(buffer.Width -1) - 1.0,
                       controlPoint.Y*2/(double)(buffer.Height-1) - 1.0);
  }
  
  public Point LocalToControl(GLPoint localPoint)
  {
    Point topLeft = GetControlRenderRect().Location; // get the top-left corner of the render area
    localPoint.Offset(1, 1); // first, shift the coordinate space from -1 to 1 to 0-2.
    // then, multiply the point by the buffer width minus one pixel. one pixel is subtracted because we want the
    // rightmost pixel to correspond to the end of the local space, rather than one pixel beyond the rightmost. then,
    // the values are divided by 2 because the original coordinate space was from 0-2, resulting in the offsets being
    // doubled. last, the values are rounded to the nearest pixel and offset by the top-left corner of the render area
    return new Point((int)Math.Round(localPoint.X*(buffer.Width -1)/2) + topLeft.X,
                     (int)Math.Round(localPoint.Y*(buffer.Height-1)/2) + topLeft.Y);
  }

  Rectangle GetControlRenderRect()
  {
    return new Rectangle((renderPanel.Width - buffer.Width)/2, (renderPanel.Height - buffer.Height)/2, 
                         buffer.Width, buffer.Height);
  }

  /// <summary>Returns the index of the topmost visible polygon.</summary>
  int GetTopmostVisiblePolygonIndex()
  {
    return selectedPolygon != -1 && chkDrawToCurrent.Checked && mode != EditMode.Animation ?
      selectedPolygon : SelectedFrame.Polygons.Count-1;
  }

  /// <summary>Select the polygon and possibly the vertex at the given point.</summary>
  void SelectPolygonAndVertex(Point pt)
  {
    GLPoint localPoint = ControlToLocal(pt);
    int topPoly = GetTopmostVisiblePolygonIndex();
    // scan from the topmost visible polygon downwards and select the first one that contains the point
    for(int polyIndex=topPoly; polyIndex >= 0; polyIndex--)
    {
      if(PolygonContains(SelectedFrame.Polygons[polyIndex], localPoint)) // if the polygon contains the point, select it.
      {
        SelectPolygon(polyIndex);

        // now see if the point was sufficiently close to any of the polygon's vertices
        if(!SelectPolygonAndVertex(polyIndex, pt))
        {
          DeselectVertex(); // if not, deselect the current vertex
        }

        return;
      }
    }
    
    // if the point didn't intersect any polygon, we'll get here. examine polygons that wouldn't have been caught by
    // the above check because they either don't have enough points or the point was close to, but outside the polygon
    for(int polyIndex=topPoly; polyIndex >= 0; polyIndex--)
    {
      if(SelectPolygonAndVertex(polyIndex, pt)) return;
    }

    DeselectPolygon(); // the point doesn't intersect anything. deselect any polygon and vertex already selected
  }

  // see if the point intersects any vertices of the given polygon. if so, select the polygon and vertex.
  // returns true if anything was selected and false otherwise.
  bool SelectPolygonAndVertex(int polygonIndex, Point pt)
  {
    VectorAnimation.Polygon poly = SelectedFrame.Polygons[polygonIndex];
    for(int i=0; i<poly.Vertices.Count; i++)
    {
      Point vertex = LocalToControl(poly.Vertices[i].Position); // compare vertices in window space
      int xd = vertex.X-pt.X, yd = vertex.Y-pt.Y;
      int distSqr = xd*xd + yd*yd; // calculate the squared distance
      if(distSqr<=32) // if the point is within ~5.66 pixels of the vertex, select it
      {
        SelectPolygon(polygonIndex);
        SelectVertex(i);
        return true;
      }
    }
    return false;
  }

  /// <summary>Determines whether the specified polygon contains the given point.</summary>
  static bool PolygonContains(VectorAnimation.Polygon poly, GLPoint point)
  {
    if(poly.Vertices.Count < 3) return false; // if it's not a proper polygon, it won't contain the point.

    // create a GameLib polygon, since it has the appropriate functionality already
    GLPoly glPoly = new GLPoly(poly.Vertices.Count);
    foreach(VectorAnimation.Vertex vertex in poly.Vertices)
    {
      glPoly.AddPoint(vertex.Position);
    }

    if(glPoly.IsConvex()) // if the created polygon is convex, we can check immediately
    {
      return glPoly.ConvexContains(point);
    }
    else // otherwise, it's non-convex, so we need to split it into convex polygons
    {
      try
      {
        foreach(GLPoly convexPoly in glPoly.Split()) // split it
        {
          if(convexPoly.ConvexContains(point)) return true; // and check each one
        }
      }
      catch(NotSupportedException) { } // this can happen if the polygon is complex. we just return false, then.

      return false;
    }
  }
  #endregion

  #region High-level mouse handling
  void OnClick(MouseEventArgs e)
  {
    if(e.Button != MouseButtons.Left) return; // we only care about left clicks

    if(Control.ModifierKeys == 0) // if it was a plain left click, do selection
    {
      SelectPolygonAndVertex(e.Location); // select the polygon/vertex under the mouse cursor
    }
    else if(Control.ModifierKeys == Keys.Control) // otherwise, if it was a ctrl-click, create a new polygon
    {
      CreateNewPolygon(e.Location);
    }
  }

  void OnDragStart(MouseEventArgs e)
  {
    if(e.Button == MouseButtons.Left) // for a left drag, we move the selected vertex/polygon
    {
      SelectPolygonAndVertex(e.Location);
      if(selectedPolygon == -1) CancelDrag(); // if the user didn't click on anything, cancel the drag
    }
    else if(e.Button == MouseButtons.Right) // for right drags, we clone and drag vertices
    {
      SelectPolygonAndVertex(e.Location);
      if(selectedVertex != -1) // if the user selected a vertex, clone it and select the clone
      {
        CloneAndSelectVertex(selectedVertex);
      }
      else // otherwise, cancel the drag
      {
        CancelDrag();
      }
    }
  }

  void OnDrag(MouseEventArgs e, Size offset)
  {
    if(selectedVertex != -1)
    {
      MoveSelectedVertexTo(e.Location); // if a vertex is selected, drag it.
    }
    else
    {
      MoveSelectedPolygon(offset); // otherwise, move the selected polygon (there should be one)
    }
  }

  void OnDragEnd(MouseEventArgs e)
  {
    InvalidateRender();
  }

  void CancelDrag()
  {
    if(dragButton != MouseButtons.None)
    {
      mouseDownPos[ButtonToIndex(dragButton)] = new Point(-1, -1); // mark the drag button as released
      dragButton = MouseButtons.None; // clear the dragging flag
      renderPanel.Capture = false; // stop capturing the mouse
    }
  }
  #endregion

  #region Low-level mouse handling
  /* use low-level mouse events to implement higher-level click and drag events */

  void renderPanel_MouseDown(object sender, MouseEventArgs e)
  {
    int button = ButtonToIndex(e.Button);
    if(button == -1) return; // ignore unsupported buttons
    // when a mouse button is pressed, mark the location. this serves as both an indication that the button is pressed
    // and stores the beginning of the drag, if the user drags the mouse
    mouseDownPos[button] = e.Location;
  }

  void renderPanel_MouseMove(object sender, MouseEventArgs e)
  {
    if(dragButton != MouseButtons.None) // if we're currently dragging, fire a drag event
    {
      OnDrag(e, new Size(e.X-lastDragPos.X, e.Y-lastDragPos.Y));
      // update the last drag point so we can send a delta to OnDrag()
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
        OnDragStart(new MouseEventArgs(e.Button, e.Clicks, mouseDownPos[button].X, mouseDownPos[button].Y, e.Delta));

        if(dragButton != MouseButtons.None) // if the drag wasn't immediately cancelled
        {
          renderPanel.Capture = true; // capture the mouse so we can be sure to receive the end of the drag
          // then issue a drag event because the mouse has since moved. always specify the original drag button.
          OnDrag(new MouseEventArgs(dragButton, e.Clicks, e.X, e.Y, e.Delta), new Size(xd, yd));
        }
      }
    }
  }

  void renderPanel_MouseUp(object sender, MouseEventArgs e)
  {
    int button = ButtonToIndex(e.Button);
    if(button == -1) return; // ignore unsupported buttons

    if(dragButton == e.Button) // if we're currently dragging, end the drag
    {
      OnDragEnd(new MouseEventArgs(dragButton, e.Clicks, e.X, e.Y, e.Delta)); // specify the original drag button
      dragButton = MouseButtons.None; // clear our drag button flag
      renderPanel.Capture = false; // stop capturing the mouse so other things can use it
    }
    else if(IsMouseDown(button)) // otherwise we're not currently dragging. was the button pressed over the control?
    {
      OnClick(e); // yes, so now that it's been released, signal a click event.
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
}

} // namespace RotationalForce.Editor