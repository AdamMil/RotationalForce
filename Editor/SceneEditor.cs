using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RotationalForce.Engine;
using GameLib.Interop.OpenGL;
using MathConst = GameLib.Mathematics.MathConst;
using GLMath  = GameLib.Mathematics.GLMath;
using GLPoint = GameLib.Mathematics.TwoD.Point;
using GLRect  = GameLib.Mathematics.TwoD.Rectangle;
using GLPoly  = GameLib.Mathematics.TwoD.Polygon;
using Vector  = GameLib.Mathematics.TwoD.Vector;

namespace RotationalForce.Editor
{

public class SceneEditor : Form
{
  const string TriggerName = "__trigger__";
  const int DecorationRadius = 8;
  
  private ToolStrip toolBar;
  private ToolStripMenuItem editMenu;
  private SplitContainer rightPane;
  private ToolStrip objToolBar;
  private ToolboxList objectList;
  private ToolStripButton newVectorAnim;
  private ImageList objectImgs;
  private System.ComponentModel.IContainer components;
  private ToolStripButton collisionTool;
  private PropertyGrid propertyGrid;
  private RenderPanel renderPanel;

  public SceneEditor()
  {
    toolbox = new Toolbox(this);

    InitializeComponent();

    ListViewItem triggerItem = new ListViewItem("Trigger", 0, objectList.Groups[3]);
    triggerItem.Tag = TriggerName;
    objectList.Items.Add(triggerItem);
    
    toolBar.Items[0].Tag = Tools.Object;
    toolBar.Items[1].Tag = Tools.Layers;

    CurrentTool = Tools.Object;
  }

  static SceneEditor()
  {
    ToolboxItem.RegisterItem(TriggerName, new TriggerItem());
  }

  public void CreateNew()
  {
    scene = new Scene();

    sceneView = new SceneViewControl();
    sceneView.Scene = scene;
    sceneView.RenderInvisible = true;

    desktop = new DesktopControl();
    desktop.AddChild(sceneView);
  }

  #region Invalidation
  new void Invalidate(Rectangle rect, bool invalidateRender)
  {
    if(invalidateRender)
    {
      InvalidateRender(rect);
    }
    else
    {
      InvalidateDecoration(rect);
    }
  }

  void InvalidateDecoration()
  {
    renderPanel.Invalidate();
  }

  void InvalidateDecoration(Rectangle rect)
  {
    rect.Inflate(1, 1);
    renderPanel.Invalidate(rect);
  }

  void InvalidateRender()
  {
    sceneView.Invalidate();
    renderPanel.Invalidate();
  }

  void InvalidateRender(Rectangle rect)
  {
    rect.Inflate(1, 1);
    sceneView.Invalidate(rect);
    renderPanel.InvalidateRender(rect);
  }

  void InvalidateObjectBounds(SceneObject obj, bool invalidateRender)
  {
    Rectangle controlRect = sceneView.SceneToClient(obj.GetRotatedAreaBounds()); // get the client rectangle
    controlRect.Inflate(DecorationRadius, DecorationRadius); // include space for our decoration
    Invalidate(controlRect, invalidateRender);
  }
  #endregion

  #region Layers
  int CurrentLayer
  {
    get { return currentLayer; }
    set
    {
      if(value != currentLayer)
      {
        currentLayer = value;
        if(RenderToCurrentLayer)
        {
          sceneView.LayerMask = VisibleLayerMask;
          InvalidateRender();
        }
      }
    }
  }
  
  uint CurrentLayerMask
  {
    get { return (uint)1<<CurrentLayer; }
  }

  bool RenderToCurrentLayer
  {
    get { return renderToCurrentLayer; }
    
    set
    {
      if(value != renderToCurrentLayer)
      {
        renderToCurrentLayer = value;
        if(currentLayer != 0)
        {
          sceneView.LayerMask = VisibleLayerMask;
          InvalidateRender();
        }
      }
    }
  }

  uint VisibleLayerMask
  {
    get
    {
      if(!RenderToCurrentLayer)
      {
        return visibleLayerMask;
      }
      else
      {
        uint layerMask = 0xffffffff;
        for(int layer=currentLayer; layer > 0; layer--)
        {
          layerMask <<= 1;
        }
        return layerMask & visibleLayerMask;
      }
    }

    set
    {
      uint oldValue = VisibleLayerMask;
      visibleLayerMask = value;

      if(VisibleLayerMask != oldValue)
      {
        sceneView.LayerMask = VisibleLayerMask;
        InvalidateRender();
      }
    }
  }
  
  int currentLayer = 0;
  uint visibleLayerMask = 0xffffffff;
  bool renderToCurrentLayer;
  #endregion

  DesktopControl desktop;
  SceneViewControl sceneView;
  Scene scene;

  #region InitializeComponent
  private void InitializeComponent()
  {
    this.components = new System.ComponentModel.Container();
    System.Windows.Forms.ToolStripButton newStaticImg;
    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SceneEditor));
    System.Windows.Forms.ToolStripButton newAnimatedImg;
    System.Windows.Forms.ToolStripButton deleteItem;
    System.Windows.Forms.ToolStripButton selectTool;
    System.Windows.Forms.ToolStripButton layerTool;
    System.Windows.Forms.ToolStripButton cameraTool;
    System.Windows.Forms.ToolStripButton terrainTool;
    System.Windows.Forms.ToolStripButton mountTool;
    System.Windows.Forms.MenuStrip menuBar;
    System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Static Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Animated Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Vector Animations", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Miscellaneous", System.Windows.Forms.HorizontalAlignment.Left);
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.toolBar = new System.Windows.Forms.ToolStrip();
    this.collisionTool = new System.Windows.Forms.ToolStripButton();
    this.rightPane = new System.Windows.Forms.SplitContainer();
    this.objToolBar = new System.Windows.Forms.ToolStrip();
    this.newVectorAnim = new System.Windows.Forms.ToolStripButton();
    this.objectList = new RotationalForce.Editor.ToolboxList();
    this.objectImgs = new System.Windows.Forms.ImageList(this.components);
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    newStaticImg = new System.Windows.Forms.ToolStripButton();
    newAnimatedImg = new System.Windows.Forms.ToolStripButton();
    deleteItem = new System.Windows.Forms.ToolStripButton();
    selectTool = new System.Windows.Forms.ToolStripButton();
    layerTool = new System.Windows.Forms.ToolStripButton();
    cameraTool = new System.Windows.Forms.ToolStripButton();
    terrainTool = new System.Windows.Forms.ToolStripButton();
    mountTool = new System.Windows.Forms.ToolStripButton();
    menuBar = new System.Windows.Forms.MenuStrip();
    menuBar.SuspendLayout();
    this.toolBar.SuspendLayout();
    this.rightPane.Panel1.SuspendLayout();
    this.rightPane.Panel2.SuspendLayout();
    this.rightPane.SuspendLayout();
    this.objToolBar.SuspendLayout();
    this.renderPanel.SuspendLayout();
    this.SuspendLayout();
    // 
    // newStaticImg
    // 
    newStaticImg.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    newStaticImg.Image = ((System.Drawing.Image)(resources.GetObject("newStaticImg.Image")));
    newStaticImg.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    newStaticImg.ImageTransparentColor = System.Drawing.Color.Magenta;
    newStaticImg.Name = "newStaticImg";
    newStaticImg.Size = new System.Drawing.Size(23, 20);
    newStaticImg.Text = "Import Static Image";
    newStaticImg.ToolTipText = "Import a new static image.";
    // 
    // newAnimatedImg
    // 
    newAnimatedImg.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    newAnimatedImg.Image = ((System.Drawing.Image)(resources.GetObject("newAnimatedImg.Image")));
    newAnimatedImg.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    newAnimatedImg.ImageTransparentColor = System.Drawing.Color.Magenta;
    newAnimatedImg.Name = "newAnimatedImg";
    newAnimatedImg.Size = new System.Drawing.Size(23, 20);
    newAnimatedImg.Text = "Import Animated Image";
    newAnimatedImg.ToolTipText = "Import a new animated image.";
    // 
    // deleteItem
    // 
    deleteItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    deleteItem.Image = ((System.Drawing.Image)(resources.GetObject("deleteItem.Image")));
    deleteItem.ImageTransparentColor = System.Drawing.Color.Magenta;
    deleteItem.Name = "deleteItem";
    deleteItem.Size = new System.Drawing.Size(23, 20);
    deleteItem.Text = "Delete Item";
    deleteItem.ToolTipText = "Deletes the selected item.";
    // 
    // selectTool
    // 
    selectTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    selectTool.Image = global::RotationalForce.Editor.Properties.Resources.SelectTool;
    selectTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    selectTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    selectTool.Name = "selectTool";
    selectTool.Size = new System.Drawing.Size(23, 20);
    selectTool.Text = "Select";
    selectTool.ToolTipText = "Select. Use this tool to select and manipulate objects.";
    selectTool.Click += new System.EventHandler(this.tool_Click);
    // 
    // layerTool
    // 
    layerTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    layerTool.Image = global::RotationalForce.Editor.Properties.Resources.LayerTool;
    layerTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    layerTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    layerTool.Name = "layerTool";
    layerTool.Size = new System.Drawing.Size(23, 20);
    layerTool.Text = "Layers";
    layerTool.ToolTipText = "Layers. Use this tool to show and hide layers.";
    layerTool.Click += new System.EventHandler(this.tool_Click);
    // 
    // cameraTool
    // 
    cameraTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    cameraTool.Image = global::RotationalForce.Editor.Properties.Resources.ZoomTool;
    cameraTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    cameraTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    cameraTool.Name = "cameraTool";
    cameraTool.Size = new System.Drawing.Size(23, 20);
    cameraTool.Text = "Camera";
    cameraTool.ToolTipText = "Camera. Use this tool to zoom and place the camera.";
    cameraTool.Click += new System.EventHandler(this.tool_Click);
    // 
    // terrainTool
    // 
    terrainTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    terrainTool.Image = global::RotationalForce.Editor.Properties.Resources.TerrainTool;
    terrainTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    terrainTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    terrainTool.Name = "terrainTool";
    terrainTool.Size = new System.Drawing.Size(23, 20);
    terrainTool.Text = "Terrain";
    terrainTool.ToolTipText = "Terrain. Use this tool to create vector-based terrain.";
    terrainTool.Click += new System.EventHandler(this.tool_Click);
    // 
    // mountTool
    // 
    mountTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    mountTool.Image = global::RotationalForce.Editor.Properties.Resources.LinkTool;
    mountTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    mountTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    mountTool.Name = "mountTool";
    mountTool.Size = new System.Drawing.Size(23, 20);
    mountTool.Text = "Mount Points";
    mountTool.ToolTipText = "Mount points. Use this tool to set the mount points of the selected object.";
    mountTool.Click += new System.EventHandler(this.tool_Click);
    // 
    // menuBar
    // 
    menuBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editMenu});
    menuBar.Location = new System.Drawing.Point(0, 0);
    menuBar.Name = "menuBar";
    menuBar.Size = new System.Drawing.Size(552, 24);
    menuBar.TabIndex = 0;
    menuBar.Visible = false;
    // 
    // editMenu
    // 
    this.editMenu.MergeAction = System.Windows.Forms.MergeAction.Insert;
    this.editMenu.MergeIndex = 1;
    this.editMenu.Name = "editMenu";
    this.editMenu.Size = new System.Drawing.Size(37, 20);
    this.editMenu.Text = "&Edit";
    // 
    // toolBar
    // 
    this.toolBar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
    this.toolBar.AutoSize = false;
    this.toolBar.Dock = System.Windows.Forms.DockStyle.None;
    this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
    this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            selectTool,
            layerTool,
            cameraTool,
            terrainTool,
            mountTool,
            this.collisionTool});
    this.toolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
    this.toolBar.Location = new System.Drawing.Point(561, 3);
    this.toolBar.Name = "toolBar";
    this.toolBar.Size = new System.Drawing.Size(206, 24);
    this.toolBar.TabIndex = 1;
    // 
    // collisionTool
    // 
    this.collisionTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    this.collisionTool.Image = global::RotationalForce.Editor.Properties.Resources.CollisionTool;
    this.collisionTool.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    this.collisionTool.ImageTransparentColor = System.Drawing.Color.Magenta;
    this.collisionTool.Name = "collisionTool";
    this.collisionTool.Size = new System.Drawing.Size(23, 20);
    this.collisionTool.Text = "Collision";
    this.collisionTool.ToolTipText = "Collision area. Use this tool to set the collision area of the selected object.";
    // 
    // rightPane
    // 
    this.rightPane.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Right)));
    this.rightPane.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
    this.rightPane.Location = new System.Drawing.Point(560, 30);
    this.rightPane.Name = "rightPane";
    this.rightPane.Orientation = System.Windows.Forms.Orientation.Horizontal;
    // 
    // rightPane.Panel1
    // 
    this.rightPane.Panel1.Controls.Add(this.objToolBar);
    this.rightPane.Panel1.Controls.Add(this.objectList);
    // 
    // rightPane.Panel2
    // 
    this.rightPane.Panel2.Controls.Add(this.propertyGrid);
    this.rightPane.Panel2Collapsed = true;
    this.rightPane.Size = new System.Drawing.Size(207, 491);
    this.rightPane.SplitterDistance = 209;
    this.rightPane.TabIndex = 3;
    // 
    // objToolBar
    // 
    this.objToolBar.AutoSize = false;
    this.objToolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
    this.objToolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            newStaticImg,
            newAnimatedImg,
            this.newVectorAnim,
            deleteItem});
    this.objToolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
    this.objToolBar.Location = new System.Drawing.Point(0, 0);
    this.objToolBar.Name = "objToolBar";
    this.objToolBar.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
    this.objToolBar.Size = new System.Drawing.Size(205, 24);
    this.objToolBar.TabIndex = 2;
    // 
    // newVectorAnim
    // 
    this.newVectorAnim.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
    this.newVectorAnim.Image = ((System.Drawing.Image)(resources.GetObject("newVectorAnim.Image")));
    this.newVectorAnim.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
    this.newVectorAnim.ImageTransparentColor = System.Drawing.Color.Magenta;
    this.newVectorAnim.Name = "newVectorAnim";
    this.newVectorAnim.Size = new System.Drawing.Size(23, 20);
    this.newVectorAnim.Text = "Import Vector Animation";
    this.newVectorAnim.ToolTipText = "Import a new vector animation.";
    // 
    // objectList
    // 
    this.objectList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
    listViewGroup1.Header = "Static Images";
    listViewGroup1.Name = "staticImgGroup";
    listViewGroup2.Header = "Animated Images";
    listViewGroup2.Name = "animImgGroup";
    listViewGroup3.Header = "Vector Animations";
    listViewGroup3.Name = "vectorAnimGroup";
    listViewGroup4.Header = "Miscellaneous";
    listViewGroup4.Name = "miscGroup";
    this.objectList.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4});
    this.objectList.LargeImageList = this.objectImgs;
    this.objectList.Location = new System.Drawing.Point(4, 24);
    this.objectList.MultiSelect = false;
    this.objectList.Name = "objectList";
    this.objectList.ShowItemToolTips = true;
    this.objectList.Size = new System.Drawing.Size(196, 462);
    this.objectList.TabIndex = 0;
    this.objectList.TileSize = new System.Drawing.Size(192, 36);
    this.objectList.UseCompatibleStateImageBehavior = false;
    // 
    // objectImgs
    // 
    this.objectImgs.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("objectImgs.ImageStream")));
    this.objectImgs.TransparentColor = System.Drawing.Color.Transparent;
    this.objectImgs.Images.SetKeyName(0, "TriggerIcon.bmp");
    // 
    // propertyGrid
    // 
    this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
    this.propertyGrid.Location = new System.Drawing.Point(0, 0);
    this.propertyGrid.Name = "propertyGrid";
    this.propertyGrid.Size = new System.Drawing.Size(205, 276);
    this.propertyGrid.TabIndex = 0;
    this.propertyGrid.Visible = false;
    // 
    // renderPanel
    // 
    this.renderPanel.AllowDrop = true;
    this.renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
    this.renderPanel.BackColor = System.Drawing.Color.Black;
    this.renderPanel.Controls.Add(menuBar);
    this.renderPanel.Location = new System.Drawing.Point(2, 3);
    this.renderPanel.Name = "renderPanel";
    this.renderPanel.Size = new System.Drawing.Size(552, 518);
    this.renderPanel.TabIndex = 0;
    this.renderPanel.RenderBackground += new System.EventHandler(this.renderPanel_RenderBackground);
    this.renderPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseMove);
    this.renderPanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseClick);
    this.renderPanel.MouseDrag += new RotationalForce.Editor.MouseDragEventHandler(this.renderPanel_MouseDrag);
    this.renderPanel.DragDrop += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragDrop);
    this.renderPanel.DragEnter += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragEnter);
    this.renderPanel.MouseDragStart += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseDragStart);
    this.renderPanel.Resize += new System.EventHandler(this.renderPanel_Resize);
    this.renderPanel.MouseEnter += new System.EventHandler(this.renderPanel_MouseEnter);
    this.renderPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.renderPanel_Paint);
    this.renderPanel.MouseDragEnd += new RotationalForce.Editor.MouseDragEventHandler(this.renderPanel_MouseDragEnd);
    // 
    // SceneEditor
    // 
    this.ClientSize = new System.Drawing.Size(772, 523);
    this.Controls.Add(this.rightPane);
    this.Controls.Add(this.toolBar);
    this.Controls.Add(this.renderPanel);
    this.KeyPreview = true;
    this.MainMenuStrip = menuBar;
    this.MinimumSize = new System.Drawing.Size(430, 250);
    this.Name = "SceneEditor";
    this.Text = "Scene Editor";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
    this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.SceneEditor_KeyUp);
    this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SceneEditor_KeyDown);
    menuBar.ResumeLayout(false);
    menuBar.PerformLayout();
    this.toolBar.ResumeLayout(false);
    this.toolBar.PerformLayout();
    this.rightPane.Panel1.ResumeLayout(false);
    this.rightPane.Panel2.ResumeLayout(false);
    this.rightPane.ResumeLayout(false);
    this.objToolBar.ResumeLayout(false);
    this.objToolBar.PerformLayout();
    this.renderPanel.ResumeLayout(false);
    this.renderPanel.PerformLayout();
    this.ResumeLayout(false);

  }

  #endregion

  #region Tools
  #region EditTool
  abstract class EditTool
  {
    public EditTool(SceneEditor editor)
    {
      this.editor = editor;
    }

    public virtual void Activate() { }
    public virtual void Deactivate() { }

    public virtual void KeyPress(KeyEventArgs e, bool down) { }
    public virtual void MouseClick(MouseEventArgs e) { }
    public virtual void MouseMove(MouseEventArgs e) { }
    public virtual void MouseDragStart(MouseEventArgs e) { }
    public virtual void MouseDrag(MouseDragEventArgs e) { }
    public virtual void MouseDragEnd(MouseDragEventArgs e) { }

    public virtual void PanelResized() { }
    public virtual void PaintDecoration(Graphics g) { }
    
    protected SceneEditor Editor
    {
      get { return editor; }
    }
    
    protected RenderPanel Panel
    {
      get { return Editor.renderPanel; }
    }
    
    protected Scene Scene
    {
      get { return Editor.scene; }
    }

    protected SceneViewControl SceneView
    {
      get { return Editor.sceneView; }
    }

    SceneEditor editor;
  }
  #endregion

  #region ObjectTool
  class ObjectTool : EditTool
  {
    public ObjectTool(SceneEditor editor) : base(editor) { }

    public override void Activate()
    {
      Editor.propertyGrid.PropertyValueChanged += propertyGrid_PropertyValueChanged;
    }

    public override void Deactivate()
    {
      Editor.HideRightPane();
      DeselectObjects();
      Editor.propertyGrid.PropertyValueChanged -= propertyGrid_PropertyValueChanged;
    }

    public override void KeyPress(KeyEventArgs e, bool down)
    {
      if(down && e.KeyCode == Keys.Delete)
      {
        foreach(SceneObject obj in selectedObjects)
        {
          Scene.RemoveObject(obj);
        }
        DeselectObjects();
        
        e.Handled = true;
      }
    }

    #region Mouse click
    public override void MouseClick(MouseEventArgs e)
    {
      // a plain left click selects the object beneath the cursor (if any) and deselects others
      if(e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.None)
      {
        SelectObject(e.Location, true);
      }
      // a shift-left click toggles the selection of the object beneath the cursor
      else if(e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Shift)
      {
        ToggleObjectSelection(e.Location);
      }
    }
    #endregion

    #region Mouse drag
    public override void MouseDragStart(MouseEventArgs e)
    {
      // we only care about left-drags
      if(e.Button != MouseButtons.Left)
      {
        Panel.CancelMouseDrag();
        return;
      }
      
      // if zero or one objects are selected, select the object under the pointer
      if(selectedObjects.Count < 2)
      {
        SceneObject obj = ObjectUnderPoint(e.Location);
        if(obj != null) SelectObject(obj, true);
      }
      
      dragHandle = GetHandleUnderPoint(e.Location);

      // if the user drags outside the selection rectangle, do a box-selection
      if(dragHandle == Handle.None && !GetHandleBorder().Contains(e.Location))
      {
        StartBoxSelection();
      }
      else
      {
        draggedObjs.Clear();
        foreach(SceneObject obj in selectedObjects)
        {
          draggedObjs.Add(new DraggedObject(obj));
        }

         // plain drag does movement or resizing. shift-drag does aspect-locked resizing or axis-locked moving.
        if(Control.ModifierKeys == Keys.None || Control.ModifierKeys == Keys.Shift)
        {
          StartResize();
        }
        else if(Control.ModifierKeys == Keys.Control) // rotation drag
        {
          StartRotation(e);
        }
      }
    }

    public override void MouseDrag(MouseDragEventArgs e)
    {
      InvalidateSelectedBounds(true);

      if(dragMode == DragMode.Select)
      {
        DragSelection(e);
      }
      else if(dragMode == DragMode.Rotate)
      {
        DragRotation(e);
      }
      else if(dragMode == DragMode.Move)
      {
        DragMove(e);
      }
      else if(dragMode == DragMode.Resize)
      {
        DragResize(e);
      }

      RecalculateSelectedBounds();
      InvalidateSelectedBounds(true);
    }

    void DragMove(MouseDragEventArgs e)
    {
      Size  clientDist = new Size(e.X - e.Start.X, e.Y - e.Start.Y);
      Vector sceneDist = SceneView.ClientToScene(clientDist);
      for(int i=0; i<selectedObjects.Count; i++)
      {
        selectedObjects[i].Position = draggedObjs[i].Position + sceneDist;
      }
    }

    void DragResize(MouseDragEventArgs e)
    {
      Size  clientDist = new Size(e.X - e.Start.X, e.Y - e.Start.Y); // the X and Y mouse distance in pixels
      Vector sceneDist = SceneView.ClientToScene(clientDist);    // the X and Y mouse distance in scene units
      Vector sizeDelta = new Vector(); // the amount the box's size is changing, in scene units

      if((dragHandle & Handle.Left) != 0) // if the left edge is being dragged
      {
        sizeDelta.X = -sceneDist.X; // then the resize amount gets bigger as the mouse moves left
      }
      else if((dragHandle & Handle.Right) != 0) // if the right edge is being dragged
      {
        sizeDelta.X = sceneDist.X; // then the resize amount gets bigger as the mouse moves right
      }
      else // neither left/right edge is being dragged
      {
        sceneDist.X = 0; // lock the mouse X movement to zero because we later use it to calculate object movement
      }

      if((dragHandle & Handle.Top) != 0) // top edge is being dragged
      {
        sizeDelta.Y = -sceneDist.Y; // resize amount gets bigger as mouse moves up
      }
      else if((dragHandle & Handle.Bottom) != 0) // bottom edge is being dragged
      {
        sizeDelta.Y = sceneDist.Y; // resize amount gets bigger as mouse moves down
      }
      else // neither top/bottom edge is being dragged
      {
        sceneDist.Y = 0; // lock the mouse Y movement to zero because we later use it to calculate object movement
      }

      // we use aspect lock if the shift key is depressed, or any object is rotated arbitrarily
      bool aspectLock = Control.ModifierKeys == Keys.Shift;
      foreach(SceneObject obj in selectedObjects)
      {
        if(obj.Rotation != 0 && obj.Rotation != 180 && obj.Rotation != 90 && obj.Rotation != 270)
        {
          aspectLock = true;
          break;
        }
      }

      if(aspectLock)
      {
        Vector oldDelta  = sizeDelta;
        Vector newSize   = dragBounds.Size + sizeDelta;
        double oldAspect = dragBounds.Width / dragBounds.Height;
        double newAspect = newSize.X / newSize.Y;

        if(newAspect < oldAspect)
        {
          if(newSize.X < dragBounds.Width && (dragHandle & (Handle.Top | Handle.Bottom)) == 0)
          {
            newSize.Y *= newAspect / oldAspect;
          }
          else
          {
            newSize.X *= oldAspect / newAspect;
          }
        }
        else
        {
          if(newSize.Y < dragBounds.Height && (dragHandle & (Handle.Left | Handle.Right)) == 0)
          {
            newSize.X *= oldAspect / newAspect;
          }
          else
          {
            newSize.Y *= newAspect / oldAspect;
          }
        }

        sizeDelta = newSize - dragBounds.Size;

        // fix up sceneDist to match the new magnitudes
        sceneDist.X = Math.Sign(sceneDist.X) * Math.Abs(sizeDelta.X);
        sceneDist.Y = Math.Sign(sceneDist.Y) * Math.Abs(sizeDelta.Y);

        // if the sign changed in either direction, swap the corresponding sign of sceneDist as well
        if(Math.Sign(oldDelta.X) != Math.Sign(sizeDelta.X)) sceneDist.X = -sceneDist.X;
        if(Math.Sign(oldDelta.Y) != Math.Sign(sizeDelta.Y)) sceneDist.Y = -sceneDist.Y;
      }

      // if the rectangle would be inverted in either direction, set the flip flag
      bool hFlip = dragBounds.Width + sizeDelta.X < 0, vFlip = dragBounds.Height + sizeDelta.Y < 0;

      for(int i=0; i<selectedObjects.Count; i++)
      {
        SceneObject obj = selectedObjects[i];
        Vector delta = sizeDelta, objSize = draggedObjs[i].Size; // create copies because we'll change them

        if(obj.Rotation == 90 || obj.Rotation == 270) // if the object is rotated on it's side, swap the axes
        {
          EngineMath.Swap(ref delta.X, ref delta.Y);
          EngineMath.Swap(ref objSize.X, ref objSize.Y);
        }

        // the size increase is proportional to how much space the object occupied in the original bounding rectangle
        Vector sizeIncrease = new Vector(delta.X * objSize.X / dragBounds.Width,
                                         delta.Y * objSize.Y / dragBounds.Height);
        // the movement is proportional to how far offset the object was from the edge in the original rectangle
        Vector movement = new Vector((draggedObjs[i].Position.X - dragBounds.X) / dragBounds.Width * sceneDist.X,
                                     (draggedObjs[i].Position.Y - dragBounds.Y) / dragBounds.Height * sceneDist.Y);
        Vector newSize = draggedObjs[i].Size + sizeIncrease;

        if(hFlip) newSize.X = -newSize.X;
        if(vFlip) newSize.Y = -newSize.Y;

        obj.HorizontalFlip = hFlip ? !draggedObjs[i].HorizontalFlip : draggedObjs[i].HorizontalFlip;
        obj.VerticalFlip = vFlip ? !draggedObjs[i].VerticalFlip : draggedObjs[i].VerticalFlip;
        obj.Size = newSize;
        obj.Position = draggedObjs[i].Position + movement;
      }
    }

    void DragRotation(MouseDragEventArgs e)
    {
      double rotation = GLMath.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location)) - dragRotation;

      for(int i=0; i<selectedObjects.Count; i++)
      {
        Vector movement = (draggedObjs[i].Position - dragCenter).Rotated(rotation);
        selectedObjects[i].Rotation = draggedObjs[i].Rotation + rotation * MathConst.RadiansToDegrees;
        selectedObjects[i].Position = dragCenter + movement;
      }
    }

    void DragSelection(MouseDragEventArgs e)
    {
      int x1 = e.Start.X, x2 = e.X, y1 = e.Start.Y, y2 = e.Y;
      if(x2 < x1) EngineMath.Swap(ref x1, ref x2);
      if(y2 < y1) EngineMath.Swap(ref y1, ref y2);

      Editor.InvalidateDecoration(dragBox);
      InvalidateSelectedBounds(false);

      dragBox = Rectangle.FromLTRB(x1, y1, x2, y2);
      Editor.InvalidateDecoration(dragBox);
      SelectObjects(dragBox, true);
    }

    public override void MouseDragEnd(MouseDragEventArgs e)
    {
      if(dragMode == DragMode.Select)
      {
        Editor.InvalidateDecoration(dragBox);
      }
      dragMode = DragMode.None;
    }

    void StartRotation(MouseEventArgs e)
    {
      dragMode = DragMode.Rotate;

      // store the centerpoint of the rotation
      if(selectedObjects.Count == 1)
      {
        dragCenter = selectedObjects[0].Position;
      }
      else
      {
        dragCenter = EngineMath.GetCenterPoint(selectedObjectSceneBounds);
      }

      // and the initial rotation value
      dragRotation = GLMath.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location));
    }

    void StartResize()
    {
      if(dragHandle != Handle.None) // resizing
      {
        dragBounds = selectedObjectSceneBounds;
        dragMode = DragMode.Resize;
      }
      else
      {
        dragMode = DragMode.Move;
      }
    }

    void StartBoxSelection()
    {
      dragMode = DragMode.Select;
      dragBox = new Rectangle();
      DeselectObjects();
    }
    #endregion

    public override void MouseMove(MouseEventArgs e)
    {
      SetCursor(e.Location);
    }

    public override void PaintDecoration(Graphics g)
    {
      if(selectedObjects.Count != 0)
      {
        Point[] scenePoints = new Point[5];
        GLPoint[] objPoints = new GLPoint[4];

        if(selectedObjects.Count > 1 || dragMode == DragMode.Select)
        {
          // draw boxes around selected items
          foreach(SceneObject obj in selectedObjects)
          {
            obj.GetRotatedArea().CopyTo(objPoints, 0); // copy the objects rotated bounding box into the point array
            for(int i=0; i<4; i++)
            {
              scenePoints[i] = SceneView.SceneToClient(objPoints[i]); // transform from scene space to control space
            }
            scenePoints[4] = scenePoints[0]; // form a loop
            
            g.DrawLines(Pens.LightGray, scenePoints);
          }
        }

        if(dragMode != DragMode.Select)
        {
          // draw the control rectangle around the selected items
          g.DrawRectangle(Pens.White, GetHandleBorder());
          foreach(Handle handle in GetHandles())
          {
            DrawControlHandle(g, handle);
          }
        }
      }

      if(dragMode == DragMode.Select)
      {
        g.DrawRectangle(Pens.White, dragBox);
      }
    }
  
    public override void PanelResized()
    {
      RecalculateSelectedBounds();
    }

    [Flags]
    enum Handle
    {
      None   = 0,
      Top    = 1,
      Bottom = 2,
      Left   = 4,
      Right  = 8,

      TopLeft=Top|Left, TopRight = Top|Right, BottomLeft = Bottom|Left, BottomRight = Bottom|Right,
    }

    void AddSelectedObjectBounds(SceneObject obj)
    {
      GLRect sceneBounds = obj.GetRotatedAreaBounds();
      Rectangle clientBounds = SceneView.SceneToClient(sceneBounds);

      if(selectedObjectBounds.Width == 0)
      {
        selectedObjectBounds = clientBounds;
        selectedObjectSceneBounds = sceneBounds;
      }
      else
      {
        selectedObjectBounds = Rectangle.Union(selectedObjectBounds, clientBounds);
        selectedObjectSceneBounds.Unite(sceneBounds);
      }
    }
    
    void ClearSelectedObjectBounds()
    {
      selectedObjectBounds = new Rectangle();
      selectedObjectSceneBounds = new GLRect();
    }

    void DeselectObject(SceneObject obj)
    {
      if(IsSelected(obj))
      {
        InvalidateSelectedBounds(false);
        ClearSelectedObjectBounds();
        foreach(SceneObject selectedObj in selectedObjects)
        {
          if(selectedObj != obj)
          {
            AddSelectedObjectBounds(selectedObj);
          }
        }
        selectedObjects.Remove(obj);
        OnSelectedObjectsChanged();
      }
    }

    void DeselectObjects()
    {
      if(selectedObjects.Count != 0)
      {
        InvalidateSelectedBounds(false);
        selectedObjects.Clear();
        ClearSelectedObjectBounds();
        OnSelectedObjectsChanged();
      }
    }

    void DrawControlHandle(Graphics g, Handle handle)
    {
      Rectangle rect = GetHandleRect(handle);
      g.FillRectangle(Brushes.Blue, rect);
      g.DrawRectangle(Pens.White, rect);
    }

    void InvalidateSelectedBounds(bool invalidateRender)
    {
      Rectangle rect = selectedObjectBounds;
      rect.Inflate(DecorationRadius, DecorationRadius);
      Editor.Invalidate(rect, invalidateRender);
    }

    bool IsSelected(SceneObject obj) { return selectedObjects.Contains(obj); }

    Handle GetHandleUnderPoint(Point pt)
    {
      foreach(Handle handle in GetHandles())
      {
        if(GetHandleRect(handle).Contains(pt)) return handle;
      }
      return Handle.None;
    }

    Rectangle GetHandleBorder()
    {
      Rectangle rect = selectedObjectBounds;
      if(rect.Width != 0)
      {
        rect.Inflate(5, 5);
        rect.Width  -= 1;
        rect.Height -= 1;
      }
      return rect;
    }
    
    static IEnumerable<Handle> GetHandles()
    {
      yield return Handle.TopLeft;
      yield return Handle.Top;
      yield return Handle.TopRight;
      yield return Handle.Right;
      yield return Handle.BottomRight;
      yield return Handle.Bottom;
      yield return Handle.BottomLeft;
      yield return Handle.Left;
    }

    Rectangle GetHandleRect(Handle handle)
    {
      Rectangle rect = GetHandleBorder();
      int x, y;
      
      if((handle & Handle.Left) != 0) x = rect.X;
      else if((handle & Handle.Right) != 0) x = rect.Right;
      else x = rect.X + rect.Width/2;

      if((handle & Handle.Top) != 0) y = rect.Y;
      else if((handle & Handle.Bottom) != 0) y = rect.Bottom;
      else y = rect.Y + rect.Height/2;

      return new Rectangle(x-2, y-2, 5, 5);
    }

    PickOptions GetPickerOptions()
    {
      PickOptions options = new PickOptions();
      options.AllowInvisible  = true;
      options.AllowUnpickable = true;
      options.GroupMask       = 0xffffffff;
      options.LayerMask       = Editor.CurrentLayerMask;
      options.SortByLayer     = true;
      return options;
    }
    
    SceneObject ObjectUnderPoint(Point pt)
    {
      foreach(SceneObject obj in Scene.PickPoint(SceneView.ClientToScene(pt), GetPickerOptions()))
      {
        return obj; // return the first item, if there are any
      }

      return null;
    }
    
    List<SceneObject> ObjectsInRect(Rectangle rect)
    {
      IEnumerator<SceneObject> e = Scene.PickRectangle(SceneView.ClientToScene(rect), GetPickerOptions()).GetEnumerator();
      if(!e.MoveNext()) return null;
      List<SceneObject> list = new List<SceneObject>();
      do
      {
        list.Add(e.Current);
      } while(e.MoveNext());
      return list;
    }

    void OnSelectedObjectsChanged()
    {
      if(selectedObjects.Count == 0)
      {
        Editor.HideRightPane();
      }
      else
      {
        Editor.propertyGrid.SelectedObjects = selectedObjects.ToArray();
        Editor.ShowPropertyGrid();
      }
    }

    void RecalculateSelectedBounds()
    {
      ClearSelectedObjectBounds();
      foreach(SceneObject obj in selectedObjects)
      {
        AddSelectedObjectBounds(obj);
      }
    }

    public void SelectObject(SceneObject obj, bool deselectOthers)
    {
      if(deselectOthers)
      {
        if(selectedObjects.Count == 1 && IsSelected(obj)) return;
        InvalidateSelectedBounds(false);
        DeselectObjects();
      }
      else if(IsSelected(obj))
      {
        return;
      }

      selectedObjects.Add(obj);
      AddSelectedObjectBounds(obj);
      InvalidateSelectedBounds(false);
      OnSelectedObjectsChanged();
    }

    void SelectObjects(IList<SceneObject> objs, bool deselectOthers)
    {
      if(deselectOthers)
      {
        if(selectedObjects.Count == objs.Count)
        {
          bool different = false;
          foreach(SceneObject obj in selectedObjects)
          {
            if(!objs.Contains(obj))
            {
              different = true;
              break;
            }
          }
          if(!different) return;
        }

        InvalidateSelectedBounds(false);
        DeselectObjects();
      }

      foreach(SceneObject obj in objs)
      {
        if(!IsSelected(obj))
        {
          selectedObjects.Add(obj);
          AddSelectedObjectBounds(obj);
        }
      }

      InvalidateSelectedBounds(false);
      OnSelectedObjectsChanged();
    }

    void SelectObject(Point pt, bool deselectOthers)
    {
      SceneObject obj = ObjectUnderPoint(pt);
      if(obj != null) SelectObject(obj, deselectOthers);
      else if(deselectOthers) DeselectObjects();
    }

    void SelectObjects(Rectangle rect, bool deselectOthers)
    {
      List<SceneObject> objs = ObjectsInRect(rect);
      if(objs != null) SelectObjects(objs, deselectOthers);
      else if(deselectOthers) DeselectObjects();
    }

    void SetCursor()
    {
      SetCursor(Panel.PointToClient(Control.MousePosition));
    }

    void SetCursor(Point pt)
    {
      if(!Panel.ClientRectangle.Contains(pt)) return;

      if(selectedObjects.Count != 0)
      {
        Handle handle = GetHandleUnderPoint(pt);
        if(handle != Handle.None)
        {
          if(Control.ModifierKeys == Keys.Control)
          {
            Panel.Cursor = Cursors.Hand; // cursor for rotation
          }
          else
          {
            switch(handle)
            {
              case Handle.Top: case Handle.Bottom:
                Panel.Cursor = Cursors.SizeNS;
                return;
              case Handle.Left: case Handle.Right:
                Panel.Cursor = Cursors.SizeWE;
                return;
              case Handle.TopLeft: case Handle.BottomRight:
                Panel.Cursor = Cursors.SizeNWSE;
                return;
              case Handle.TopRight: case Handle.BottomLeft:
                Panel.Cursor = Cursors.SizeNESW;
                return;
            }
          }
        }
        
        // check if the cursor is over the selected rectangle
        if(GetHandleBorder().Contains(pt))
        {
          if(Control.ModifierKeys == Keys.Control)
          {
            Panel.Cursor = Cursors.Hand; // cursor for rotation
          }
          else
          {
            Panel.Cursor = Cursors.SizeAll;
          }
          return;
        }
      }

      Panel.Cursor = Cursors.Default;
    }

    void ToggleObjectSelection(Point pt)
    {
      SceneObject obj = ObjectUnderPoint(pt);
      if(obj == null) return;

      if(IsSelected(obj))
      {
        DeselectObject(obj);
      }
      else
      {
        SelectObject(obj, false);
      }
    }

    void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
    {
      RecalculateSelectedBounds();
      Editor.InvalidateRender();
    }

    sealed class DraggedObject
    {
      public DraggedObject(SceneObject obj)
      {
        Position = obj.Position;
        Size     = obj.Size;
        Rotation = obj.Rotation;
        HorizontalFlip = obj.HorizontalFlip;
        VerticalFlip   = obj.VerticalFlip;
      }

      public GLPoint Position;
      public Vector  Size;
      public double  Rotation;
      public bool HorizontalFlip, VerticalFlip;
    }

    enum DragMode { None, Select, Resize, Move, Rotate }

    List<DraggedObject> draggedObjs = new List<DraggedObject>();
    GLPoint   dragCenter; // center of drag rotation
    GLRect    dragBounds; // original bounding rectangle during resize
    Rectangle dragBox;  // box size during selection
    double    dragRotation;
    Handle    dragHandle;
    DragMode  dragMode;

    Rectangle selectedObjectBounds;
    GLRect selectedObjectSceneBounds;
    List<SceneObject> selectedObjects = new List<SceneObject>();
  }
  #endregion

  #region LayerTool
  class LayerTool : EditTool
  {
    public LayerTool(SceneEditor editor) : base(editor) { }

    public override void Activate()
    {
      if(layerPanel == null)
      {
        layerPanel = new Panel();
        layerPanel.Size = new Size(Editor.rightPane.Width, Editor.rightPane.Height - Editor.rightPane.SplitterDistance);
        layerPanel.Dock = DockStyle.Fill;

        renderToCurrent = new CheckBox();
        renderToCurrent.Location = new Point(4, 3);
        renderToCurrent.Size = new Size(layerPanel.Width - 8, 24);
        renderToCurrent.TabIndex = 0;
        renderToCurrent.Text = "Render to current layer";
        renderToCurrent.CheckedChanged += new EventHandler(renderToCurrent_CheckedChanged);
        renderToCurrent.Checked = Editor.RenderToCurrentLayer;

        layerBox = new CheckedListBox();
        layerBox.Location = new Point(4, 26);
        layerBox.Size = new Size(renderToCurrent.Width, layerPanel.Height - renderToCurrent.Height);
        layerBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
        layerBox.IntegralHeight = false;
        layerBox.TabIndex = 1;
        layerBox.CheckOnClick = true;
        layerBox.ItemCheck += new ItemCheckEventHandler(layerBox_ItemCheck);
        layerBox.SelectedIndexChanged += new EventHandler(layerBox_SelectedIndexChanged);

        for(int i=0; i<32; i++)
        {
          layerBox.Items.Add("Layer "+i);
        }

        layerPanel.Controls.Add(renderToCurrent);
        layerPanel.Controls.Add(layerBox);
        layerPanel.Visible = false;
        Editor.rightPane.Panel2.Controls.Add(layerPanel);
      }

      for(int i=0; i<32; i++)
      {
        // don't use the accessor because we want to bypass RenderToCurrentLayer
        layerBox.SetItemChecked(i, (Editor.visibleLayerMask & (1<<i)) != 0);
      }
      layerBox.SelectedIndex = Editor.CurrentLayer;

      layerPanel.Visible = true;
      Editor.ShowRightPane(layerPanel);
    }

    public override void Deactivate()
    {
      Editor.HideRightPane();
    }

    void renderToCurrent_CheckedChanged(object sender, EventArgs e)
    {
      Editor.RenderToCurrentLayer = renderToCurrent.Checked;
    }

    void layerBox_ItemCheck(object sender, ItemCheckEventArgs e)
    {
      uint bit = (uint)1<<e.Index;
      if(e.NewValue == CheckState.Checked)
      {
        Editor.VisibleLayerMask |= bit;
      }
      else
      {
        Editor.VisibleLayerMask &= ~bit;
      }
    }

    void layerBox_SelectedIndexChanged(object sender, EventArgs e)
    {
      Editor.CurrentLayer = layerBox.SelectedIndex;
    }

    CheckedListBox layerBox;
    CheckBox renderToCurrent;
    Panel layerPanel;
  }
  #endregion

  class Toolbox
  {
    public Toolbox(SceneEditor editor) { this.editor = editor; }

    public LayerTool Layers
    {
      get
      {
        if(layerTool == null) layerTool = new LayerTool(editor);
        return layerTool;
      }
    }

    public ObjectTool Object
    {
      get
      {
        if(objectTool == null) objectTool = new ObjectTool(editor);
        return objectTool;
      }
    }

    SceneEditor editor;
    ObjectTool objectTool;
    LayerTool layerTool;
  }

  EditTool CurrentTool
  {
    get { return currentTool; }
    set
    {
      if(value != currentTool)
      {
        if(currentTool != null) currentTool.Deactivate();
        currentTool = value;
        currentTool.Activate();
        
        foreach(ToolStripButton item in toolBar.Items)
        {
          item.Checked = item.Tag == value;
        }
      }
    }
  }

  Toolbox Tools
  {
    get { return toolbox; }
  }

  void tool_Click(object sender, EventArgs e)
  {
    CurrentTool = (EditTool)((ToolStripButton)sender).Tag;
  }

  EditTool currentTool;
  Toolbox toolbox;
  #endregion

  void renderPanel_MouseEnter(object sender, EventArgs e)
  {
    renderPanel.Focus();
  }

  #region Delegation to tool
  void SceneEditor_KeyDown(object sender, KeyEventArgs e)
  {
    if(!renderPanel.Focused) return;
    currentTool.KeyPress(e, true);
  }

  void SceneEditor_KeyUp(object sender, KeyEventArgs e)
  {
    if(!renderPanel.Focused) return;
    currentTool.KeyPress(e, false);
  }

  void renderPanel_MouseMove(object sender, MouseEventArgs e)
  {
    currentTool.MouseMove(e);
  }

  void renderPanel_MouseClick(object sender, MouseEventArgs e)
  {
    currentTool.MouseClick(e);
  }

  void renderPanel_MouseDragStart(object sender, MouseEventArgs e)
  {
    currentTool.MouseDragStart(e);
  }

  void renderPanel_MouseDrag(object sender, MouseDragEventArgs e)
  {
    currentTool.MouseDrag(e);
  }

  void renderPanel_MouseDragEnd(object sender, MouseDragEventArgs e)
  {
    currentTool.MouseDragEnd(e);
  }
  #endregion

  #region Dragging items from the toolpane
  private void renderPanel_DragEnter(object sender, DragEventArgs e)
  {
    e.Effect = DragDropEffects.Copy;
  }

  private void renderPanel_DragDrop(object sender, DragEventArgs e)
  {
    string itemName = (string)e.Data.GetData(DataFormats.StringFormat);
    ToolboxItem item = ToolboxItem.GetItem(itemName);
    if(item != null)
    {
      SceneObject obj = item.CreateSceneObject();
      obj.Position = sceneView.ClientToScene(renderPanel.PointToClient(new Point(e.X, e.Y)));
      obj.Layer    = CurrentLayer;
      scene.AddObject(obj);

      CurrentTool = Tools.Object;
      Tools.Object.SelectObject(obj, true);
    }
  }
  #endregion

  private void renderPanel_Resize(object sender, EventArgs e)
  {
    sceneView.Bounds = desktop.Bounds = renderPanel.ClientRectangle;
    currentTool.PanelResized();
  }

  #region Painting, rendering, and layout
  private void renderPanel_RenderBackground(object sender, EventArgs e)
  {
    Engine.Engine.ResetOpenGL(renderPanel.Width, renderPanel.Height, renderPanel.ClientRectangle);
    sceneView.Invalidate();
    desktop.Render();
  }

  private void renderPanel_Paint(object sender, PaintEventArgs e)
  {
    currentTool.PaintDecoration(e.Graphics);
  }
  
  void HideRightPane()
  {
    rightPane.Panel2Collapsed = true;
    HideRightPaneControls(null);
  }

  void HideRightPaneControls(Control controlToIgnore)
  {
    foreach(Control child in rightPane.Panel2.Controls)
    {
      if(child != controlToIgnore) child.Visible = false;
    }

    if(propertyGrid != controlToIgnore) propertyGrid.SelectedObjects = null;
  }

  void ShowRightPane(Control controlToIgnore)
  {
    HideRightPaneControls(controlToIgnore);
    rightPane.Panel2Collapsed = false;
  }

  void ShowPropertyGrid()
  {
    propertyGrid.Visible = true;
    HideRightPaneControls(propertyGrid);
    rightPane.Panel2Collapsed = false;
  }
  #endregion

}

#region ToolboxList
public class ToolboxList : ListView
{
  protected override void OnMouseDown(MouseEventArgs e)
  {
    base.OnMouseDown(e);

    if(e.Button == MouseButtons.Left)
    {
      pressPoint = e.Location;
    }
  }

  protected override void OnMouseUp(MouseEventArgs e)
  {
    base.OnMouseUp(e);
    if(e.Button == MouseButtons.Left) pressPoint = new Point(-1, -1);
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    base.OnMouseMove(e);

    if(pressPoint.X != -1 && e.Button == MouseButtons.Left)
    {
      int xd = e.X-pressPoint.X, yd = e.Y-pressPoint.Y;
      if(xd*xd+yd*yd >= 16)
      {
        ListViewItem item = GetItemAt(pressPoint.X, pressPoint.Y);
        if(item != null)
        {
          DoDragDrop((string)item.Tag, DragDropEffects.Link|DragDropEffects.Copy);
        }
      }

      pressPoint = new Point(-1, -1);
    }
  }
  
  Point pressPoint = new Point(-1, -1);
}
#endregion

#region ToolboxItem
public abstract class ToolboxItem
{
  public abstract SceneObject CreateSceneObject();
  
  public static ToolboxItem GetItem(string name)
  {
    ToolboxItem item;
    items.TryGetValue(name, out item);
    return item;
  }

  public static void RegisterItem(string name, ToolboxItem item)
  {
    if(name == null || items == null) throw new ArgumentNullException();
    items.Add(name, item);
  }

  static Dictionary<string,ToolboxItem> items = new Dictionary<string,ToolboxItem>();
}

public class TriggerItem : ToolboxItem
{
  public override SceneObject CreateSceneObject()
  {
    return new TriggerObject();
  }
}
#endregion

} // namespace RotationalForce.Editor
