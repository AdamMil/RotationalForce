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
  const double DefaultViewSize = 100;
  
  private ToolStrip toolBar;
  private ToolStripMenuItem editMenu;
  private SplitContainer rightPane;
  private ToolStrip objToolBar;
  private ToolboxList objectList;
  private ToolStripButton newVectorAnim;
  private ImageList objectImgs;
  private System.ComponentModel.IContainer components;
  private PropertyGrid propertyGrid;
  private StatusStrip statusBar;
  private ToolStripStatusLabel mousePosLabel;
  private ToolStripStatusLabel layerLabel;
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
    toolBar.Items[2].Tag = Tools.Zoom;
    toolBar.Items[3].Tag = Tools.Terrain;

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
    // use the minor camera axis so that we easily calculate the camera size needed to display a given object
    sceneView.CameraAxis      = CameraAxis.Minor;
    sceneView.CameraSize      = DefaultViewSize;
    sceneView.Scene           = scene;
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

  void InvalidateView()
  {
    currentTool.ViewChanged();
    InvalidateRender();
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
        if(value < 0 || value > 31) throw new ArgumentOutOfRangeException();

        currentLayer = value;
        layerLabel.Text = "Layer: " + value.ToString();

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

  internal StatusStrip StatusBar
  {
    get { return statusBar; }
  }

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
    System.Windows.Forms.MenuStrip menuBar;
    System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Static Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Animated Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Vector Animations", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Miscellaneous", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ToolStripButton terrainTool;
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.toolBar = new System.Windows.Forms.ToolStrip();
    this.rightPane = new System.Windows.Forms.SplitContainer();
    this.objToolBar = new System.Windows.Forms.ToolStrip();
    this.newVectorAnim = new System.Windows.Forms.ToolStripButton();
    this.objectList = new RotationalForce.Editor.ToolboxList();
    this.objectImgs = new System.Windows.Forms.ImageList(this.components);
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    this.statusBar = new System.Windows.Forms.StatusStrip();
    this.mousePosLabel = new System.Windows.Forms.ToolStripStatusLabel();
    this.layerLabel = new System.Windows.Forms.ToolStripStatusLabel();
    newStaticImg = new System.Windows.Forms.ToolStripButton();
    newAnimatedImg = new System.Windows.Forms.ToolStripButton();
    deleteItem = new System.Windows.Forms.ToolStripButton();
    selectTool = new System.Windows.Forms.ToolStripButton();
    layerTool = new System.Windows.Forms.ToolStripButton();
    cameraTool = new System.Windows.Forms.ToolStripButton();
    menuBar = new System.Windows.Forms.MenuStrip();
    terrainTool = new System.Windows.Forms.ToolStripButton();
    menuBar.SuspendLayout();
    this.toolBar.SuspendLayout();
    this.rightPane.Panel1.SuspendLayout();
    this.rightPane.Panel2.SuspendLayout();
    this.rightPane.SuspendLayout();
    this.objToolBar.SuspendLayout();
    this.renderPanel.SuspendLayout();
    this.statusBar.SuspendLayout();
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
            terrainTool});
    this.toolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
    this.toolBar.Location = new System.Drawing.Point(561, 3);
    this.toolBar.Name = "toolBar";
    this.toolBar.Size = new System.Drawing.Size(206, 24);
    this.toolBar.TabIndex = 1;
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
    this.propertyGrid.Size = new System.Drawing.Size(148, 44);
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
    this.renderPanel.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseWheel);
    this.renderPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseMove);
    this.renderPanel.RenderBackground += new System.EventHandler(this.renderPanel_RenderBackground);
    this.renderPanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseClick);
    this.renderPanel.MouseDrag += new RotationalForce.Editor.MouseDragEventHandler(this.renderPanel_MouseDrag);
    this.renderPanel.DragDrop += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragDrop);
    this.renderPanel.DragEnter += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragEnter);
    this.renderPanel.MouseDragStart += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseDragStart);
    this.renderPanel.Resize += new System.EventHandler(this.renderPanel_Resize);
    this.renderPanel.KeyUp += new System.Windows.Forms.KeyEventHandler(this.renderPanel_KeyUp);
    this.renderPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.renderPanel_Paint);
    this.renderPanel.MouseDragEnd += new RotationalForce.Editor.MouseDragEventHandler(this.renderPanel_MouseDragEnd);
    this.renderPanel.KeyDown += new System.Windows.Forms.KeyEventHandler(this.renderPanel_KeyDown);
    // 
    // statusBar
    // 
    this.statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mousePosLabel,
            this.layerLabel});
    this.statusBar.Location = new System.Drawing.Point(0, 501);
    this.statusBar.Name = "statusBar";
    this.statusBar.Size = new System.Drawing.Size(772, 22);
    this.statusBar.TabIndex = 4;
    this.statusBar.Visible = false;
    // 
    // mousePosLabel
    // 
    this.mousePosLabel.Name = "mousePosLabel";
    this.mousePosLabel.Size = new System.Drawing.Size(58, 17);
    this.mousePosLabel.Text = "0.00, 0.00";
    // 
    // layerLabel
    // 
    this.layerLabel.Name = "layerLabel";
    this.layerLabel.Size = new System.Drawing.Size(38, 17);
    this.layerLabel.Text = "Layer:";
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
    // SceneEditor
    // 
    this.ClientSize = new System.Drawing.Size(772, 523);
    this.Controls.Add(this.statusBar);
    this.Controls.Add(this.rightPane);
    this.Controls.Add(this.toolBar);
    this.Controls.Add(this.renderPanel);
    this.MainMenuStrip = menuBar;
    this.MinimumSize = new System.Drawing.Size(430, 250);
    this.Name = "SceneEditor";
    this.Text = "Scene Editor";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
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
    this.statusBar.ResumeLayout(false);
    this.statusBar.PerformLayout();
    this.ResumeLayout(false);
    this.PerformLayout();

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
    public virtual bool MouseClick(MouseEventArgs e) { return false; }
    public virtual void MouseMove(MouseEventArgs e) { }
    public virtual bool MouseDragStart(MouseEventArgs e) { return false; }
    public virtual void MouseDrag(MouseDragEventArgs e) { }
    public virtual void MouseDragEnd(MouseDragEventArgs e) { }
    public virtual bool MouseWheel(MouseEventArgs e) { return false; }

    public virtual void PanelResized() { }
    public virtual void PaintDecoration(Graphics g) { }
    
    public virtual void ViewChanged() { }
    
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
  sealed class ObjectTool : EditTool
  {
    public ObjectTool(SceneEditor editor) : base(editor)
    {
      spatialTool = new SpatialSubTool(editor, this);
      linksTool   = new LinksSubTool(editor, this);
    }

    public override void Activate()
    {
      SubTool = SpatialTool;
      SubTool.Activate();
    }

    public override void Deactivate()
    {
      SubTool.Deactivate();
      Editor.HideRightPane();
      DeselectObjects();
    }

    #region SubTools
    public SpatialSubTool SpatialTool
    {
      get { return spatialTool; }
    }
    
    public LinksSubTool LinksTool
    {
      get { return linksTool; }
    }

    public ObjectSubTool SubTool
    {
      get { return subTool; }
      
      set
      {
        if(value != subTool)
        {
          if(subTool != null) SubTool.Deactivate();
          Panel.Cursor = Cursors.Default;
          subTool = value;
          if(value != null) value.Activate();
          Editor.InvalidateDecoration();
        }
      }
    }
    
    public abstract class ObjectSubTool : EditTool
    {
      public ObjectSubTool(SceneEditor editor, ObjectTool parent) : base(editor)
      {
        objectTool = parent;
      }
      
      internal virtual void OnSelectedObjectsChanged() { }

      protected ObjectTool ObjectTool
      {
        get { return objectTool; }
      }

      protected List<SceneObject> SelectedObjects
      {
        get { return ObjectTool.selectedObjects; }
      }
      
      ObjectTool objectTool;
    }

    #region LinksSubTool
    public sealed class LinksSubTool : ObjectSubTool
    {
      public LinksSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }
      
      SceneObject Object
      {
        get { return SelectedObjects[0]; }
      }

      public override void Activate()
      {
        previousViewCenter = SceneView.CameraPosition;
        previousViewSize   = SceneView.CameraSize;

        previousRotation = Object.Rotation;
        Object.Rotation  = 0;

        linkPoints = Object.GetLinkPoints();
 
        SceneView.CameraPosition = Object.Position; // center the camera on the object
        SceneView.CameraSize     = Math.Max(Object.Width, Object.Height) * 1.10; // 10% bigger than the object's size
        Editor.InvalidateView();
      }

      public override void Deactivate()
      {
        Object.Rotation = previousRotation;

        SceneView.CameraPosition = previousViewCenter;
        SceneView.CameraSize     = previousViewSize;
        Editor.InvalidateView();
        
        linkPoints = null;
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
        {
          ObjectTool.SubTool = ObjectTool.SpatialTool;
          e.Handled = true;
        }
      }

      public override bool MouseClick(MouseEventArgs e)
      {
        if(e.Button == MouseButtons.Left) // a left click creates a new link point
        {
          int linkPoint = GetLinkUnder(e.Location);
          if(linkPoint != -1) // if there's already a link point there, don't add a new one
          {
            MessageBox.Show("There's already a link point there, sir.", "Sorry!",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
          }

          GLPoint objPoint = Object.SceneToLocal(SceneView.ClientToScene(e.Location));
          Object.AddLinkPoint(objPoint.X, objPoint.Y);
          linkPoints = Object.GetLinkPoints();
          Editor.InvalidateDecoration();
          return true;
        }
        else if(e.Button == MouseButtons.Right) // a right click deletes a link point
        {
          List<int> links = GetLinksUnder(e.Location);
          if(links.Count == 0) return false;

          if(links.Count > 1) // if there are multiple link points under the mouse
          {
            if(MessageBox.Show("There are multiple link points here. Do you want to delete all of them?",
                               "Delete all?", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                               MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
              links.RemoveRange(1, links.Count-1); // remove all but the first one (the one clicked on)
            }
          }

          // see if any link points scheduled for deletion have a mounted object
          bool hasMount = false;
          for(int i=0; i<links.Count; i++)
          {
            if(IsMountPoint(links[i]))
            {
              hasMount = true;
              break;
            }
          }

          if(hasMount) // if there are mounted objects, unmount (and possibly delete) them first
          {
            DialogResult result =
              MessageBox.Show("At least one link point has a mounted object. Do you want to delete the mounted "+
                              "objects?", "Delete mounted objects?", MessageBoxButtons.YesNoCancel,
                              MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
            if(result == DialogResult.Cancel) return true;
            
            for(int i=0; i<links.Count; i++) // unmount and possibly delete mounted objects
            {
              if(IsMountPoint(links[i]))
              {
                linkPoints[links[i]].Object.Dismount();
                if(result == DialogResult.Yes)
                {
                  Scene.RemoveObject(linkPoints[links[i]].Object);
                }
              }
            }
          }

          for(int i=0; i<links.Count; i++) // now delete the link points
          {
            if(!IsMountPoint(links[i])) // mount points are automatically deleted when the object
            {                           // is unmounted, so we don't need to worry about those
              Object.RemoveLinkPoint(linkPoints[links[i]].ID);
            }
          }

          linkPoints = Object.GetLinkPoints();
          Editor.InvalidateDecoration();
          return true;
        }
        else
        {
          return false;
        }
      }

      public override bool MouseDragStart(MouseEventArgs e)
      {
        if(e.Button != MouseButtons.Left) return false; // we only care about left drags

        // left drags move a link point
        dragLinks = GetLinksUnder(e.Location);
        return dragLinks.Count != 0;
      }

      public override void MouseDrag(MouseDragEventArgs e)
      {
        GLPoint scenePoint = SceneView.ClientToScene(e.Location);
        for(int i=0; i<dragLinks.Count; i++)
        {
          linkPoints[dragLinks[i]].ScenePoint = scenePoint;
        }
        Editor.InvalidateDecoration();
        
        GLPoint localPoint = Object.SceneToLocal(scenePoint);
        EditorApp.MainForm.StatusText = string.Format("Moved to {0:f2}, {1:f2}", localPoint.X, localPoint.Y);
      }

      public override void MouseDragEnd(MouseDragEventArgs e)
      {
        GLPoint localPoint = Object.SceneToLocal(linkPoints[dragLinks[0]].ScenePoint);
        for(int i=0; i<dragLinks.Count; i++)
        {
          Object.UpdateLinkPoint(linkPoints[dragLinks[i]].ID, localPoint.X, localPoint.Y);
        }
        linkPoints = Object.GetLinkPoints();
      }

      public override void PaintDecoration(Graphics g)
      {
        Rectangle boundsRect = SceneView.SceneToClient(Object.GetRotatedAreaBounds());
        boundsRect.Inflate(2,2);
        g.DrawRectangle(Pens.White, boundsRect);
        
        // loop through twice so that the mount points are on the bottom
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(IsMountPoint(i)) DrawLinkPoint(g, i);
        }
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(!IsMountPoint(i)) DrawLinkPoint(g, i);
        }
      }

      void DrawLinkPoint(Graphics g, int linkPoint)
      {
        Rectangle rect = GetLinkRect(linkPoint);
        g.FillRectangle(IsMountPoint(linkPoint) ? Brushes.Red : Brushes.Blue, rect);
        g.DrawRectangle(Pens.White, rect);
      }

      Rectangle GetLinkRect(int linkPoint)
      {
        Point pt = SceneView.SceneToClient(linkPoints[linkPoint].ScenePoint);
        return new Rectangle(pt.X-2, pt.Y-2, 5, 5);
      }
      
      int GetLinkUnder(Point pt)
      {
        // loop through twice to select the non-mount points first
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(!IsMountPoint(i) && GetLinkRect(i).Contains(pt)) return i;
        }
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(IsMountPoint(i) && GetLinkRect(i).Contains(pt)) return i;
        }
        return -1;
      }

      // finds the link under the mouse point and all other links with the same position. returns them sorted from
      // front (non-mount points) to back (mount points)
      List<int> GetLinksUnder(Point pt)
      {
        List<int> links = new List<int>();

        int linkPoint = GetLinkUnder(pt);
        if(linkPoint == -1) return links;

        links.Add(linkPoint);

        // loop through twice to select the non-mount points first
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(i != linkPoint && !IsMountPoint(i) && linkPoints[i].Offset == linkPoints[linkPoint].Offset)
          {
            links.Add(i);
          }
        }
        for(int i=0; i<linkPoints.Length; i++)
        {
          if(i != linkPoint && IsMountPoint(i) && linkPoints[i].Offset == linkPoints[linkPoint].Offset)
          {
            links.Add(i);
          }
        }
        return links;
      }

      bool IsMountPoint(int linkPoint) { return linkPoints[linkPoint].Object != null; }

      LinkPoint[] linkPoints;
      List<int> dragLinks;
      GLPoint previousViewCenter;
      double  previousViewSize, previousRotation;
    }
    #endregion

    #region SpatialSubTool
    public sealed class SpatialSubTool : ObjectSubTool
    {
      public SpatialSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }

      public override void Activate()
      {
        Editor.propertyGrid.PropertyValueChanged += propertyGrid_PropertyValueChanged;
        OnSelectedObjectsChanged();
      }

      public override void Deactivate()
      {
        Editor.propertyGrid.SelectedObjects = null;
        Editor.propertyGrid.PropertyValueChanged -= propertyGrid_PropertyValueChanged;
        Editor.HideRightPane();
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(down && e.KeyCode == Keys.Delete)
        {
          DeleteSelectedObjects();
          e.Handled = true;
        }
        else if(e.KeyCode == Keys.ControlKey) // control key toggles rotation mode
        {
          SetCursor();
        }
      }

      #region Mouse click
      public override bool MouseClick(MouseEventArgs e)
      {
        // a plain left click selects the object beneath the cursor (if any) and deselects others
        if(e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.None)
        {
          SelectObject(e.Location, true);
          return true;
        }
        // a shift-left click toggles the selection of the object beneath the cursor
        else if(e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Shift)
        {
          ToggleObjectSelection(e.Location);
          return true;
        }
        // a plain right click opens a context menu for the selected items
        else if(e.Button == MouseButtons.Right && Control.ModifierKeys == Keys.None)
        {
          ContextMenu menu = new ContextMenu();

          // if there are no items selected or the mouse cursor is not over them, try to select the item under the mouse
          if(SelectedObjects.Count == 0 || !ObjectTool.selectedObjectBounds.Contains(e.Location))
          {
            SelectObject(e.Location, true);
            if(SelectedObjects.Count == 0) return false; // if there was no object under the mouse, return false
          }

          if(SelectedObjects.Count == 1)
          {
            SceneObject obj = SelectedObjects[0];
            menu.MenuItems.Add(new MenuItem("Edit collision area", menu_EditCollision));
            menu.MenuItems.Add(new MenuItem("Edit link points", menu_EditLinks));
            if(obj.Mounted)
            {
              menu.MenuItems.Add(new MenuItem("Dismount object", menu_Dismount));
            }
            else
            {
              menu.MenuItems.Add(new MenuItem("Mount to another object", menu_Mount));
            }
            menu.MenuItems.Add(new MenuItem("-"));
          }

          menu.MenuItems.Add(new MenuItem("Delete object(s)", menu_Delete, Shortcut.Del));
          menu.MenuItems.Add(new MenuItem("Export object(s)", menu_Export));
          
          foreach(SceneObject obj in SelectedObjects)
          {
            if(obj.Rotation != 0)
            {
              menu.MenuItems.Add(new MenuItem("Reset rotation", menu_ResetRotation));
              break;
            }
          }

          menu.MenuItems.Add(new MenuItem("Never mind"));
          
          menu.Show(Panel, e.Location);
          return true;
        }
        
        return false;
      }
      #endregion

      #region Mouse drag
      public override bool MouseDragStart(MouseEventArgs e)
      {
        // we only care about left-drags
        if(e.Button != MouseButtons.Left) return false;
        
        // if zero or one objects are selected, select the object under the pointer
        if(SelectedObjects.Count < 2)
        {
          SceneObject obj = ObjectUnderPoint(e.Location);
          if(obj != null) ObjectTool.SelectObject(obj, true);
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
          foreach(SceneObject obj in SelectedObjects)
          {
            draggedObjs.Add(new DraggedObject(obj));
          }

           // plain drag does movement or resizing. shift-drag does aspect-locked resizing or axis-locked moving.
          if(Control.ModifierKeys == Keys.None || Control.ModifierKeys == Keys.Shift)
          {
            if(dragHandle == Handle.None)
            {
              StartMove();
            }
            else
            {
              StartResize();
            }
          }
          else if((Control.ModifierKeys & Keys.Control) != 0) // rotation drag
          {
            StartRotation(e);
          }
        }
        
        return true;
      }
      
      public override void MouseDrag(MouseDragEventArgs e)
      {
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

        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      void DragMove(MouseDragEventArgs e)
      {
        Size clientDist = new Size(e.X - e.Start.X, e.Y - e.Start.Y);

        if(Control.ModifierKeys == Keys.Shift)
        {
          if(Math.Abs(clientDist.Width) > Math.Abs(clientDist.Height))
          {
            clientDist.Height = 0;
          }
          else
          {
            clientDist.Width = 0;
          }
        }

        Vector sceneDist = SceneView.ClientToScene(clientDist);
        for(int i=0; i<SelectedObjects.Count; i++)
        {
          SelectedObjects[i].Position = draggedObjs[i].Position + sceneDist;
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
        foreach(SceneObject obj in SelectedObjects)
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

        for(int i=0; i<SelectedObjects.Count; i++)
        {
          SceneObject obj = SelectedObjects[i];
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
        
        Vector finalSize = dragBounds.Size + sizeDelta;
        EditorApp.MainForm.StatusText =
          string.Format("W:{0:f2}% H:{1:f2}%",
                        Math.Abs(finalSize.X) / dragBounds.Width  * 100,
                        Math.Abs(finalSize.Y) / dragBounds.Height * 100);
      }

      void DragRotation(MouseDragEventArgs e)
      {
        // get the rotation in radians
        double rotation = GLMath.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location)) - dragRotation;
        // convert to a value from 0-360. we use degrees so that with the shift-mode, we can get exact 45/90/180/etc
        // degree angles (because the engine uses degrees internally), and we normalize it so that the shift-mode will
        // work and the status text will look nice
        rotation = EngineMath.NormalizeAngle(rotation * MathConst.RadiansToDegrees);

        if((Control.ModifierKeys & Keys.Shift) != 0)
        {
          const double inc = 45;
          for(double rlock=0; rlock<=360; rlock += inc)
          {
            if(Math.Abs(rotation-rlock) <= inc/2)
            {
              rotation = rlock;
              break;
            }
          }

          if(rotation == 360) rotation = 0; // keep it normalized
        }

        if(SelectedObjects.Count == 1)
        {
          SelectedObjects[0].Rotation = draggedObjs[0].Rotation + rotation;
          EditorApp.MainForm.StatusText = "Rotated to " + SelectedObjects[0].Rotation.ToString("f2") + "°";
        }
        else
        {
          double radians = rotation * MathConst.DegreesToRadians; // convert back to radians for GameLib
          for(int i=0; i<SelectedObjects.Count; i++)
          {
            Vector movement = (draggedObjs[i].Position - dragCenter).Rotated(radians);
            SelectedObjects[i].Rotation = draggedObjs[i].Rotation + rotation;
            SelectedObjects[i].Position = dragCenter + movement;
          }
          EditorApp.MainForm.StatusText = "Rotated by " + rotation.ToString("f2") + "°";
        }
      }

      void DragSelection(MouseDragEventArgs e)
      {
        int x1 = e.Start.X, x2 = e.X, y1 = e.Start.Y, y2 = e.Y;
        if(x2 < x1) EngineMath.Swap(ref x1, ref x2);
        if(y2 < y1) EngineMath.Swap(ref y1, ref y2);

        Editor.InvalidateDecoration(dragBox);
        ObjectTool.InvalidateSelectedBounds(false);

        dragBox = Rectangle.FromLTRB(x1, y1, x2, y2);
        Editor.InvalidateDecoration(dragBox);
        SelectObjects(dragBox, true);
      }

      void StartRotation(MouseEventArgs e)
      {
        dragMode = DragMode.Rotate;

        // store the centerpoint of the rotation
        if(SelectedObjects.Count == 1)
        {
          dragCenter = SelectedObjects[0].Position;
        }
        else
        {
          dragCenter = EngineMath.GetCenterPoint(ObjectTool.selectedObjectSceneBounds);
        }

        // and the initial rotation value
        dragRotation = GLMath.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location));
      }

      void StartMove()
      {
        dragMode = DragMode.Move;
      }

      void StartResize()
      {
        dragBounds = ObjectTool.selectedObjectSceneBounds;
        dragMode = DragMode.Resize;
      }

      void StartBoxSelection()
      {
        dragMode = DragMode.Select;
        dragBox  = new Rectangle();
        ObjectTool.DeselectObjects();
      }
      
      public override void MouseDragEnd(MouseDragEventArgs e)
      {
        if(dragMode == DragMode.Select)
        {
          Editor.InvalidateDecoration(dragBox);
        }
        dragMode = DragMode.None;
        
        Editor.propertyGrid.SelectedObjects = Editor.propertyGrid.SelectedObjects;
      }
      #endregion

      public override void MouseMove(MouseEventArgs e)
      {
        SetCursor(e.Location);
      }

      public override void PaintDecoration(Graphics g)
      {
        if(SelectedObjects.Count != 0)
        {
          Point[] scenePoints = new Point[5];
          GLPoint[] objPoints = new GLPoint[4];

          if(SelectedObjects.Count > 1 || dragMode == DragMode.Select)
          {
            // draw boxes around selected items
            foreach(SceneObject obj in SelectedObjects)
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

      [Flags]
      enum Handle
      {
        None   = 0,
        Top    = 1,
        Bottom = 2,
        Left   = 4,
        Right  = 8,

        TopLeft=Top|Left, TopRight=Top|Right, BottomLeft=Bottom|Left, BottomRight=Bottom|Right,
      }

      void DeleteSelectedObjects()
      {
        foreach(SceneObject obj in SelectedObjects)
        {
          Scene.RemoveObject(obj);
        }
        ObjectTool.DeselectObjects();
      }

      void DrawControlHandle(Graphics g, Handle handle)
      {
        Rectangle rect = GetHandleRect(handle);
        g.FillRectangle(Brushes.Blue, rect);
        g.DrawRectangle(Pens.White, rect);
      }

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
        Rectangle rect = ObjectTool.selectedObjectBounds;
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

      internal override void OnSelectedObjectsChanged()
      {
        if(SelectedObjects.Count == 0)
        {
          Editor.HideRightPane();
        }
        else
        {
          Editor.propertyGrid.SelectedObjects = SelectedObjects.ToArray();
          Editor.ShowPropertyGrid();
        }
      }

      void SelectObject(Point pt, bool deselectOthers)
      {
        SceneObject obj = ObjectUnderPoint(pt);
        if(obj != null) ObjectTool.SelectObject(obj, deselectOthers);
        else if(deselectOthers) ObjectTool.DeselectObjects();
      }

      void SelectObjects(Rectangle rect, bool deselectOthers)
      {
        List<SceneObject> objs = ObjectsInRect(rect);
        if(objs != null) ObjectTool.SelectObjects(objs, deselectOthers);
        else if(deselectOthers) ObjectTool.DeselectObjects();
      }

      void SetCursor()
      {
        SetCursor(Panel.PointToClient(Control.MousePosition));
      }

      void SetCursor(Point pt)
      {
        if(dragMode != DragMode.None) // if we have a drag mode, keep the correct cursor regardless
        {
          if(dragMode == DragMode.Move)
          {
            Panel.Cursor = Cursors.SizeAll;
          }
          else if(dragMode == DragMode.Resize)
          {
            SetResizeCursor(dragHandle);
          }
          else if(dragMode == DragMode.Rotate)
          {
            Panel.Cursor = rotateCursor;
          }
          else if(dragMode == DragMode.Select)
          {
            Panel.Cursor = Cursors.Default;
          }

          return;
        }

        if(!Panel.ClientRectangle.Contains(pt)) return;

        if(SelectedObjects.Count != 0)
        {
          Handle handle = GetHandleUnderPoint(pt);
          if(handle != Handle.None)
          {
            if(Control.ModifierKeys == Keys.Control)
            {
              Panel.Cursor = rotateCursor;
            }
            else if(handle != Handle.None)
            {
              SetResizeCursor(handle);
            }
            
            return;
          }

          // check if the cursor is over the selected rectangle
          if(GetHandleBorder().Contains(pt))
          {
            if(Control.ModifierKeys == Keys.Control)
            {
              Panel.Cursor = rotateCursor;
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

      void SetResizeCursor(Handle handle)
      {
        switch(handle)
        {
          case Handle.Top:
          case Handle.Bottom:
            Panel.Cursor = Cursors.SizeNS;
            break;
          case Handle.Left:
          case Handle.Right:
            Panel.Cursor = Cursors.SizeWE;
            break;
          case Handle.TopLeft:
          case Handle.BottomRight:
            Panel.Cursor = Cursors.SizeNWSE;
            break;
          case Handle.TopRight:
          case Handle.BottomLeft:
            Panel.Cursor = Cursors.SizeNESW;
            break;
        }
      }

      void ToggleObjectSelection(Point pt)
      {
        SceneObject obj = ObjectUnderPoint(pt);
        if(obj == null) return;

        if(ObjectTool.IsSelected(obj))
        {
          ObjectTool.DeselectObject(obj);
        }
        else
        {
          ObjectTool.SelectObject(obj, false);
        }
      }

      #region Context menu handlers
      void menu_Delete(object sender, EventArgs e)
      {
        DeleteSelectedObjects();
      }

      void menu_ResetRotation(object sender, EventArgs e)
      {
        foreach(SceneObject obj in SelectedObjects)
        {
          obj.Rotation = 0;
        }
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      void menu_EditCollision(object sender, EventArgs e)
      {
        throw new NotImplementedException();
      }

      void menu_EditLinks(object sender, EventArgs e)
      {
        ObjectTool.SubTool = ObjectTool.LinksTool;
      }

      void menu_Mount(object sender, EventArgs e)
      {
        throw new NotImplementedException();
      }

      void menu_Dismount(object sender, EventArgs e)
      {
        throw new NotImplementedException();
      }

      void menu_Export(object sender, EventArgs e)
      {
        throw new NotImplementedException();
      }
      #endregion

      void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
      {
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
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
        public Vector Size;
        public double Rotation;
        public bool HorizontalFlip, VerticalFlip;
      }

      enum DragMode { None, Select, Resize, Move, Rotate }

      List<DraggedObject> draggedObjs = new List<DraggedObject>();
      GLPoint dragCenter; // center of drag rotation
      GLRect dragBounds;  // original bounding rectangle during resize
      Rectangle dragBox;  // box size during selection
      double dragRotation;
      Handle dragHandle;
      DragMode dragMode;
    }
    #endregion

    LinksSubTool linksTool;
    SpatialSubTool spatialTool;
    #endregion

    #region Delegation to subtool
    public override void KeyPress(KeyEventArgs e, bool down)
    {
      SubTool.KeyPress(e, down);
    }

    public override bool MouseClick(MouseEventArgs e)
    {
      return SubTool.MouseClick(e);
    }

    public override bool MouseDragStart(MouseEventArgs e)
    {
      return SubTool.MouseDragStart(e);
    }

    public override void MouseDrag(MouseDragEventArgs e)
    {
      SubTool.MouseDrag(e);
    }

    public override void MouseDragEnd(MouseDragEventArgs e)
    {
      SubTool.MouseDragEnd(e);
    }

    public override void MouseMove(MouseEventArgs e)
    {
      SubTool.MouseMove(e);
    }

    public override bool MouseWheel(MouseEventArgs e)
    {
      return SubTool.MouseWheel(e);
    }

    public override void PaintDecoration(Graphics g)
    {
      SubTool.PaintDecoration(g);
    }
  
    public override void PanelResized()
    {
      SubTool.PanelResized();
      RecalculateAndInvalidateSelectedBounds();
    }

    public override void ViewChanged()
    {
      SubTool.ViewChanged();
      RecalculateAndInvalidateSelectedBounds();
    }
    #endregion

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

    bool IsSelected(SceneObject obj) { return selectedObjects.Contains(obj); }

    void InvalidateSelectedBounds(bool invalidateRender)
    {
      Rectangle rect = selectedObjectBounds;
      rect.Inflate(DecorationRadius, DecorationRadius);
      Editor.Invalidate(rect, invalidateRender);
    }

    void OnSelectedObjectsChanged()
    {
      SubTool.OnSelectedObjectsChanged();
    }
    
    void RecalculateSelectedBounds()
    {
      ClearSelectedObjectBounds();
      foreach(SceneObject obj in selectedObjects)
      {
        AddSelectedObjectBounds(obj);
      }
    }

    void RecalculateAndInvalidateSelectedBounds()
    {
      InvalidateSelectedBounds(true); // invalidate the old bounds
      RecalculateSelectedBounds();    // recalculate
      InvalidateSelectedBounds(true); // invalidate the new bounds
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

    ObjectSubTool subTool;

    Rectangle selectedObjectBounds;
    GLRect selectedObjectSceneBounds;
    List<SceneObject> selectedObjects = new List<SceneObject>();
    
    static readonly Cursor rotateCursor = new Cursor(new System.IO.MemoryStream(Properties.Resources.Rotate));
  }
  #endregion

  #region LayerTool
  sealed class LayerTool : EditTool
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
  
  #region TerrainTool
  sealed class TerrainTool : EditTool
  {
    public TerrainTool(SceneEditor editor) : base(editor) { }
  }
  #endregion

  #region ZoomTool
  sealed class ZoomTool : EditTool
  {
    public ZoomTool(SceneEditor editor) : base(editor) { }

    public override void Activate()
    {
      Editor.propertyGrid.PropertyValueChanged += propertyGrid_PropertyValueChanged;
      Editor.propertyGrid.SelectedObject = SceneView;
      Editor.ShowPropertyGrid();

      Panel.Cursor = zoomIn;
    }

    public override void Deactivate()
    {
      Editor.HideRightPane();
      Editor.propertyGrid.SelectedObject = null;
      Editor.propertyGrid.PropertyValueChanged -= propertyGrid_PropertyValueChanged;
    }

    public override void KeyPress(KeyEventArgs e, bool down)
    {
      if(e.KeyCode == Keys.ControlKey)
      {
        Panel.Cursor = down ? zoomOut : zoomIn;
        e.Handled = true;
      }
      else if(e.KeyCode == Keys.R && down) // pressing R resets the camera view
      {
        SceneView.CameraSize     = DefaultViewSize;
        SceneView.CameraPosition = new GLPoint();
        Editor.InvalidateView();
      }
    }

    public override bool MouseClick(MouseEventArgs e)
    {
      if(e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)
      {
        return false;
      }

      const double zoomFactor = 2;
      
      bool zoomIn = (Control.ModifierKeys & Keys.Control) == 0;
      if(e.Button == MouseButtons.Right) zoomIn = !zoomIn; // zoom the other way if the right mouse button is pressed

      if(zoomIn) // zooming in
      {
        SceneView.CameraPosition = SceneView.ClientToScene(e.Location);
        SceneView.CameraSize    /= zoomFactor;
      }
      else // zooming out
      {
        SceneView.CameraSize *= zoomFactor;
      }

      Editor.InvalidateView();
      return true;
    }

    void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
    {
      Editor.InvalidateView();
    }

    static readonly Cursor zoomIn  = new Cursor(new System.IO.MemoryStream(Properties.Resources.ZoomIn));
    static readonly Cursor zoomOut = new Cursor(new System.IO.MemoryStream(Properties.Resources.ZoomOut));
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

    public TerrainTool Terrain
    {
      get
      {
        if(terrainTool == null) terrainTool = new TerrainTool(editor);
        return terrainTool;
      }
    }

    public ZoomTool Zoom
    {
      get
      {
        if(zoomTool == null) zoomTool = new ZoomTool(editor);
        return zoomTool;
      }
    }

    SceneEditor editor;
    ObjectTool objectTool;
    LayerTool layerTool;
    TerrainTool terrainTool;
    ZoomTool zoomTool;
  }

  EditTool CurrentTool
  {
    get { return currentTool; }
    set
    {
      if(value != currentTool)
      {
        if(currentTool != null) currentTool.Deactivate();
        renderPanel.Cursor = Cursors.Default;
        currentTool = value;
        currentTool.Activate();
        
        foreach(ToolStripButton item in toolBar.Items)
        {
          item.Checked = item.Tag == value;
        }
        
        InvalidateDecoration();
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

  #region RenderPanel UI events
  void renderPanel_KeyDown(object sender, KeyEventArgs e)
  {
    if(!renderPanel.Focused) return;
    currentTool.KeyPress(e, true);
    
    if(!e.Handled)
    {
      // do keyboard scrolling if the arrow keys are pressed
      if(e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
      {
        double amount = sceneView.CameraSize / 8;
        double xd = e.KeyCode == Keys.Left ? -amount : e.KeyCode == Keys.Right ? amount : 0;
        double yd = e.KeyCode == Keys.Up   ? -amount : e.KeyCode == Keys.Down  ? amount : 0;
        sceneView.CameraPosition = new GLPoint(sceneView.CameraX + xd, sceneView.CameraY + yd);
        InvalidateView();
        e.Handled = true;
      }
    }
  }

  void renderPanel_KeyUp(object sender, KeyEventArgs e)
  {
    if(!renderPanel.Focused) return;
    currentTool.KeyPress(e, false);
  }

  void renderPanel_MouseMove(object sender, MouseEventArgs e)
  {
    renderPanel.Focus();
    currentTool.MouseMove(e);

    GLPoint worldPoint = sceneView.ClientToScene(e.Location);
    mousePosLabel.Text = worldPoint.X.ToString("f2") + ", " + worldPoint.Y.ToString("f2");
  }

  void renderPanel_MouseClick(object sender, MouseEventArgs e)
  {
    if(currentTool.MouseClick(e)) return;
  }

  void renderPanel_MouseDragStart(object sender, MouseEventArgs e)
  {
    if(currentTool.MouseDragStart(e)) return;
    
    if(e.Button == MouseButtons.Right) // right drag scrolls the view
    {
      dragScrolling = true;
      dragStart     = sceneView.CameraPosition;
    }
    else // nothing handled the drag, so cancel it
    {
      renderPanel.CancelMouseDrag();
    }
  }

  void renderPanel_MouseDrag(object sender, MouseDragEventArgs e)
  {
    if(dragScrolling)
    {
      sceneView.CameraPosition = dragStart; // reset the camera so that the correct mouse movement can be calculated

      Vector sceneOffset = sceneView.ClientToScene(new Size(e.X-e.Start.X, e.Y-e.Start.Y));
      sceneView.CameraPosition -= sceneOffset; // scroll in the opposite direction of the mouse movement

      InvalidateView();
    }
    else
    {
      currentTool.MouseDrag(e);
    }
  }

  void renderPanel_MouseDragEnd(object sender, MouseDragEventArgs e)
  {
    if(dragScrolling)
    {
      dragScrolling = false;
    }
    else
    {
      currentTool.MouseDragEnd(e);
    }
  }

  void renderPanel_MouseWheel(object sender, MouseEventArgs e)
  {
    if(currentTool.MouseWheel(e)) return;

    if(Control.ModifierKeys == Keys.None) // plain mouse wheeling zooms in and out
    {
      const double zoomFactor = 1.25;
      const double DetentsPerClick = 120; // 120 is the standard delta for a single wheel click
      double wheelMovement = e.Delta / DetentsPerClick;

      if(wheelMovement < 0) // zooming out
      {
        sceneView.CameraSize *= Math.Pow(zoomFactor, -wheelMovement);
      }
      else // zooming in
      {
        sceneView.CameraSize /= Math.Pow(zoomFactor, wheelMovement);
      }

      InvalidateView();
    }
  }
  
  GLPoint dragStart;
  bool dragScrolling;
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
      InvalidateRender();

      CurrentTool = Tools.Object;
      Tools.Object.SubTool = Tools.Object.SpatialTool;
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
