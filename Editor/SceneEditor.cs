using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RotationalForce.Engine;
using GameLib.Interop.OpenGL;
using MathConst = GameLib.Mathematics.MathConst;
using Math2D  = GameLib.Mathematics.TwoD.Math2D;
using GLPoint = GameLib.Mathematics.TwoD.Point;
using GLRect  = GameLib.Mathematics.TwoD.Rectangle;
using GLPoly  = GameLib.Mathematics.TwoD.Polygon;
using Vector  = GameLib.Mathematics.TwoD.Vector;

namespace RotationalForce.Editor
{

public class SceneEditor : Form, IEditorForm
{
  const int TriggerIcon=0, StaticImageIcon=1;
  const int DecorationRadius = 8;
  const double DefaultViewSize = 100;
  
  private ToolStrip toolBar;
  private ToolStripMenuItem editMenu;
  private SplitContainer rightPane;
  private ToolStrip objToolBar;
  private ToolboxList objectList;
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
    systemDefinedIconCount = objectImgs.Images.Count;

    foreach(ToolboxItem item in ToolboxItem.GetItems())
    {
      AddToolboxItem(item);
    }

    toolBar.Items[0].Tag = Tools.Object;
    toolBar.Items[1].Tag = Tools.Layers;
    toolBar.Items[2].Tag = Tools.Zoom;

    CurrentTool = Tools.Object;
  }

  #region IEditorForm
  public bool HasUnsavedChanges
  {
    get { return isModified; }
  }

  public StatusStrip StatusBar
  {
    get { return statusBar; }
  }

  public string Title
  {
    get
    {
      string name = level.Name;
      if(string.IsNullOrEmpty(name))
      {
        name = "Untitled";
      }
      return "Level - " + name;
    }
  }

  public void CreateNew()
  {
    sceneView = new SceneViewControl();
    // use the minor camera axis so that we easily calculate the camera size needed to fully display a given object
    sceneView.CameraAxis      = CameraAxis.Minor;
    sceneView.CameraSize      = DefaultViewSize;
    sceneView.RenderInvisible = true;
    sceneView.Scene           = new Scene();

    desktop = new DesktopControl();
    desktop.AddChild(sceneView);

    level = Project.CreateLevel();
    InvalidateRender();
    isModified = false;
  }
  
  public bool Open()
  {
    OpenFileDialog fd = new OpenFileDialog();
    fd.DefaultExt       = "xml";
    fd.InitialDirectory = Project.LevelsPath;
    fd.Filter           = "Level files (.xml)|*.xml";
    
    if(fd.ShowDialog() == DialogResult.OK)
    {
      string levelPath = Path.Combine(Path.GetDirectoryName(fd.FileName),
                                      Path.GetFileNameWithoutExtension(fd.FileName)) + ".scene";
      if(!File.Exists(levelPath))
      {
        MessageBox.Show("Unable to find "+levelPath, "Scene not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
      }
      
      using(SexpReader sr = new SexpReader(new StreamReader(levelPath, System.Text.Encoding.UTF8)))
      {
        Serializer.BeginBatch();
        sceneView = (SceneViewControl)Serializer.Deserialize(sr);
        sceneView.Scene = (Scene)Serializer.Deserialize(sr);
        Serializer.EndBatch();
      }

      desktop = new DesktopControl();
      desktop.AddChild(sceneView);

      level = Project.LoadLevel(fd.FileName);
      InvalidateRender();
      isModified = false;
      return true;
    }

    return false;
  }

  public bool Save(bool newFile)
  {
    string fileName = level.File;

    if(string.IsNullOrEmpty(fileName))
    {
      newFile  = true;
      fileName = level.Name;
      if(string.IsNullOrEmpty(fileName))
      {
        fileName = "level 1";
      }

      fileName = Project.GetLevelPath(fileName+".xml");
    }

    if(newFile)
    {
      SaveFileDialog fd = new SaveFileDialog();
      fd.DefaultExt = "xml";
      fd.FileName   = Path.GetFileName(fileName);
      fd.Filter     = "Levels (*.xml)|*.xml";
      fd.InitialDirectory = Path.GetDirectoryName(fileName);
      fd.OverwritePrompt  = true;
      fd.Title = "Save level as...";

      if(fd.ShowDialog() != DialogResult.OK)
      {
        return false;
      }
      
      fileName = fd.FileName;
    }

    fileName = Path.GetFullPath(fileName);
    level.Save(fileName);

    fileName = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)) + ".scene";
    StreamWriter sw = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);
    try
    {
      Serializer.BeginBatch();
      Serializer.Serialize(sceneView, sw); // sceneViews don't serialize their scene objects
      Serializer.Serialize(sceneView.Scene, sw);
      Serializer.EndBatch();
    }
    finally
    {
      sw.Close();
    }
    
    isModified = false;
    return true;
  }

  public bool TryClose() { return TryClose(true); }

  bool TryClose(bool doClose)
  {
    Close();
    return isClosed;
  }
  #endregion
  
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
    renderPanel.InvalidateRender();
    isModified = true;
  }

  void InvalidateRender(Rectangle rect)
  {
    rect.Inflate(1, 1);
    sceneView.Invalidate(rect);
    renderPanel.InvalidateRender(rect);
    isModified = true;
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

  Project Project
  {
    get { return EditorApp.MainForm.Project; }
  }
  
  Scene Scene
  {
    get { return sceneView.Scene; }
  }

  #region InitializeComponent
  void InitializeComponent()
  {
    this.components = new System.ComponentModel.Container();
    System.Windows.Forms.ToolStripButton selectTool;
    System.Windows.Forms.ToolStripButton layerTool;
    System.Windows.Forms.ToolStripButton cameraTool;
    System.Windows.Forms.MenuStrip menuBar;
    System.Windows.Forms.ToolStripDropDownButton toolboxNewMenu;
    System.Windows.Forms.ToolStripMenuItem newStaticImage;
    System.Windows.Forms.SplitContainer mainSplitter;
    System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Static Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Animated Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Vector Animations", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Miscellaneous", System.Windows.Forms.HorizontalAlignment.Left);
    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SceneEditor));
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    this.rightPane = new System.Windows.Forms.SplitContainer();
    this.objToolBar = new System.Windows.Forms.ToolStrip();
    this.objectList = new RotationalForce.Editor.ToolboxList();
    this.objectImgs = new System.Windows.Forms.ImageList(this.components);
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.toolBar = new System.Windows.Forms.ToolStrip();
    this.statusBar = new System.Windows.Forms.StatusStrip();
    this.mousePosLabel = new System.Windows.Forms.ToolStripStatusLabel();
    this.layerLabel = new System.Windows.Forms.ToolStripStatusLabel();
    selectTool = new System.Windows.Forms.ToolStripButton();
    layerTool = new System.Windows.Forms.ToolStripButton();
    cameraTool = new System.Windows.Forms.ToolStripButton();
    menuBar = new System.Windows.Forms.MenuStrip();
    toolboxNewMenu = new System.Windows.Forms.ToolStripDropDownButton();
    newStaticImage = new System.Windows.Forms.ToolStripMenuItem();
    mainSplitter = new System.Windows.Forms.SplitContainer();
    menuBar.SuspendLayout();
    mainSplitter.Panel1.SuspendLayout();
    mainSplitter.Panel2.SuspendLayout();
    mainSplitter.SuspendLayout();
    this.renderPanel.SuspendLayout();
    this.rightPane.Panel1.SuspendLayout();
    this.rightPane.Panel2.SuspendLayout();
    this.rightPane.SuspendLayout();
    this.objToolBar.SuspendLayout();
    this.toolBar.SuspendLayout();
    this.statusBar.SuspendLayout();
    this.SuspendLayout();
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
    // toolboxNewMenu
    // 
    toolboxNewMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            newStaticImage});
    toolboxNewMenu.Name = "toolboxNewMenu";
    toolboxNewMenu.Size = new System.Drawing.Size(41, 17);
    toolboxNewMenu.Text = "New";
    toolboxNewMenu.ToolTipText = "Creates a toolbox item and adds it to the project and the level.";
    // 
    // newStaticImage
    // 
    newStaticImage.Name = "newStaticImage";
    newStaticImage.Size = new System.Drawing.Size(155, 22);
    newStaticImage.Text = "Static image map";
    newStaticImage.Click += new System.EventHandler(this.newStaticImage_Click);
    // 
    // mainSplitter
    // 
    mainSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
    mainSplitter.Location = new System.Drawing.Point(0, 0);
    mainSplitter.Name = "mainSplitter";
    // 
    // mainSplitter.Panel1
    // 
    mainSplitter.Panel1.Controls.Add(this.renderPanel);
    mainSplitter.Panel1MinSize = 300;
    // 
    // mainSplitter.Panel2
    // 
    mainSplitter.Panel2.Controls.Add(this.rightPane);
    mainSplitter.Panel2.Controls.Add(this.toolBar);
    mainSplitter.Panel2MinSize = 130;
    mainSplitter.Size = new System.Drawing.Size(772, 523);
    mainSplitter.SplitterDistance = 600;
    mainSplitter.TabIndex = 5;
    // 
    // renderPanel
    // 
    this.renderPanel.AllowDrop = true;
    this.renderPanel.BackColor = System.Drawing.Color.Black;
    this.renderPanel.Controls.Add(menuBar);
    this.renderPanel.Dock = System.Windows.Forms.DockStyle.Fill;
    this.renderPanel.Location = new System.Drawing.Point(0, 0);
    this.renderPanel.Name = "renderPanel";
    this.renderPanel.Size = new System.Drawing.Size(600, 523);
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
    // rightPane
    // 
    this.rightPane.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
    this.rightPane.Dock = System.Windows.Forms.DockStyle.Fill;
    this.rightPane.Location = new System.Drawing.Point(0, 24);
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
    this.rightPane.Size = new System.Drawing.Size(168, 499);
    this.rightPane.SplitterDistance = 209;
    this.rightPane.TabIndex = 3;
    // 
    // objToolBar
    // 
    this.objToolBar.AutoSize = false;
    this.objToolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
    this.objToolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            toolboxNewMenu});
    this.objToolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
    this.objToolBar.Location = new System.Drawing.Point(0, 0);
    this.objToolBar.Name = "objToolBar";
    this.objToolBar.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
    this.objToolBar.Size = new System.Drawing.Size(166, 24);
    this.objToolBar.TabIndex = 2;
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
    this.objectList.Location = new System.Drawing.Point(2, 24);
    this.objectList.MultiSelect = false;
    this.objectList.Name = "objectList";
    this.objectList.ShowItemToolTips = true;
    this.objectList.Size = new System.Drawing.Size(161, 470);
    this.objectList.TabIndex = 0;
    this.objectList.TileSize = new System.Drawing.Size(192, 36);
    this.objectList.UseCompatibleStateImageBehavior = false;
    this.objectList.DoubleClick += new System.EventHandler(this.objectList_DoubleClick);
    // 
    // objectImgs
    // 
    this.objectImgs.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("objectImgs.ImageStream")));
    this.objectImgs.TransparentColor = System.Drawing.Color.Transparent;
    this.objectImgs.Images.SetKeyName(0, "TriggerIcon.bmp");
    this.objectImgs.Images.SetKeyName(1, "StaticImageIcon.bmp");
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
    // toolBar
    // 
    this.toolBar.AutoSize = false;
    this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
    this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            selectTool,
            layerTool,
            cameraTool});
    this.toolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
    this.toolBar.Location = new System.Drawing.Point(0, 0);
    this.toolBar.Name = "toolBar";
    this.toolBar.Size = new System.Drawing.Size(168, 24);
    this.toolBar.TabIndex = 1;
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
    this.layerLabel.Size = new System.Drawing.Size(47, 17);
    this.layerLabel.Text = "Layer: 0";
    // 
    // SceneEditor
    // 
    this.ClientSize = new System.Drawing.Size(772, 523);
    this.Controls.Add(mainSplitter);
    this.Controls.Add(this.statusBar);
    this.MainMenuStrip = menuBar;
    this.MinimumSize = new System.Drawing.Size(430, 250);
    this.Name = "SceneEditor";
    this.Text = "Scene Editor";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
    this.GotFocus += new System.EventHandler(this.SceneEditor_GotFocus);
    menuBar.ResumeLayout(false);
    menuBar.PerformLayout();
    mainSplitter.Panel1.ResumeLayout(false);
    mainSplitter.Panel2.ResumeLayout(false);
    mainSplitter.ResumeLayout(false);
    this.renderPanel.ResumeLayout(false);
    this.renderPanel.PerformLayout();
    this.rightPane.Panel1.ResumeLayout(false);
    this.rightPane.Panel2.ResumeLayout(false);
    this.rightPane.ResumeLayout(false);
    this.objToolBar.ResumeLayout(false);
    this.objToolBar.PerformLayout();
    this.toolBar.ResumeLayout(false);
    this.toolBar.PerformLayout();
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
      get { return Editor.Scene; }
    }

    protected SceneViewControl SceneView
    {
      get { return Editor.sceneView; }
    }

    SceneEditor editor;
  }
  #endregion

  // FIXME: invalidating an object doesn't invalidate its mounts
  #region ObjectTool
  sealed class ObjectTool : EditTool
  {
    public ObjectTool(SceneEditor editor) : base(editor)
    {
      joinTool    = new JoinSubTool(editor, this);
      linksTool   = new LinksSubTool(editor, this);
      mountTool   = new MountSubTool(editor, this);
      spatialTool = new SpatialSubTool(editor, this);
      vectorTool  = new VectorSubTool(editor, this);
    }

    public SceneObject SelectedObject
    {
      get { return selectedObjects.Count == 0 ? null : selectedObjects[0]; }
    }

    public override void Activate()
    {
      SubTool = SpatialTool;
    }

    public override void Deactivate()
    {
      SubTool = null;
      Editor.HideRightPane();
      DeselectObjects();
    }

    #region SubTools
    public SpatialSubTool SpatialTool
    {
      get { return spatialTool; }
    }
    
    public JoinSubTool JoinTool
    {
      get { return joinTool; }
    }

    public LinksSubTool LinksTool
    {
      get { return linksTool; }
    }
    
    public MountSubTool MountTool
    {
      get { return mountTool; }
    }

    public VectorSubTool VectorTool
    {
      get { return vectorTool; }
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

    #region JoinSubTool
    public sealed class JoinSubTool : ObjectSubTool
    {
      public JoinSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }

      public override void Activate()
      {
        EditorApp.MainForm.StatusText = "Click on the parent object...";
      }

      public override void Deactivate()
      {
        EditorApp.MainForm.StatusText = "";
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(down && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
        {
          ObjectTool.SubTool = ObjectTool.SpatialTool;
          e.Handled = true;
        }
      }

      public override bool MouseClick(MouseEventArgs e)
      {
        if(e.Button != MouseButtons.Left) return false;

        AnimatedObject destObj = ObjectTool.ObjectUnderPoint(e.Location) as AnimatedObject;
        if(destObj == null) return false;
        
        VectorAnimation destAnim = destObj.Animation as VectorAnimation;
        if(destAnim == null) return false;

        if(destObj == SelectedObjects[0])
        {
          MessageBox.Show("You can't join an object with itself.", "Hmm...", MessageBoxButtons.OK,
                          MessageBoxIcon.Exclamation);
          return true;
        }

        AnimatedObject   srcObj = (AnimatedObject)SelectedObjects[0];
        VectorAnimation srcAnim = (VectorAnimation)srcObj.Animation;
        foreach(VectorAnimation.Polygon poly in srcAnim.Frames[0].Polygons)
        {
          foreach(VectorAnimation.Vertex vertex in poly.Vertices)
          {
            vertex.Position = destObj.SceneToLocal(srcObj.LocalToScene(vertex.Position));
          }
          destAnim.Frames[0].AddPolygon(poly);
        }

        ObjectTool.SelectObject(destObj, true);
        Scene.RemoveObject(srcObj);
        ObjectTool.SubTool = ObjectTool.VectorTool;
        ObjectTool.VectorTool.RecalculateObjectBounds();
        Editor.InvalidateRender();
        EditorApp.MainForm.StatusText = "Objects joined.";
        return true;
      }
    }
    #endregion

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
        if(down && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
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
        using(Pen pen = new Pen(ObjectTool.GetInverseBackgroundColor()))
        {
          g.DrawRectangle(pen, boundsRect);
        
          // loop through twice so that the mount points are on the bottom
          for(int i=0; i<linkPoints.Length; i++)
          {
            if(IsMountPoint(i)) DrawLinkPoint(g, pen, i);
          }
          for(int i=0; i<linkPoints.Length; i++)
          {
            if(!IsMountPoint(i)) DrawLinkPoint(g, pen, i);
          }
        }
      }

      void DrawLinkPoint(Graphics g, Pen pen, int linkPoint)
      {
        Rectangle rect = GetLinkRect(linkPoint);
        g.FillRectangle(IsMountPoint(linkPoint) ? Brushes.Red : Brushes.Blue, rect);
        g.DrawRectangle(pen, rect);
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

    #region MountSubTool
    public sealed class MountSubTool : ObjectSubTool
    {
      public MountSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }

      public override void Activate()
      {
        child = SelectedObjects[0];
        state = State.SelectingObject;
        EditorApp.MainForm.StatusText = "Click on the parent object...";
      }

      public override void Deactivate()
      {
        child = parent = null;
        links = null;
        
        if(state == State.SelectingMount)
        {
          SceneView.CameraSize = previousViewSize;
          Editor.InvalidateView();
        }
        
        EditorApp.MainForm.StatusText = "";
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(down && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
        {
          ObjectTool.SubTool = ObjectTool.SpatialTool;
          e.Handled = true;
        }
        // if the user toggles snap-to-grid, redraw the decoration 
        else if(state == State.SelectingMount && e.KeyCode == Keys.ShiftKey)
        {
          RecalculateSelectedLink();
        }
      }

      public override bool MouseClick(MouseEventArgs e)
      {
        if(e.Button != MouseButtons.Left) return false;
        
        if(state == State.SelectingObject)
        {
          parent = ObjectTool.ObjectUnderPoint(e.Location);
          if(parent == null) return false;

          if(parent == child)
          {
            MessageBox.Show("You can't mount an object to itself.", "Hmm...", MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
            return true;
          }
          
          links = parent.GetLinkPoints();

          previousViewSize = SceneView.CameraSize;

          Vector objSize = parent.GetRotatedAreaBounds().Size;
          SceneView.CameraPosition = parent.Position;
          SceneView.CameraSize     = Math.Max(objSize.X, objSize.Y) * 1.50;
          Editor.InvalidateView();

          EditorApp.MainForm.StatusText = "Select mount point...";
          state = State.SelectingMount;
          return true;
        }
        else if(state == State.SelectingMount)
        {
          MountDialog dialog = new MountDialog();

          if(dialog.ShowDialog() == DialogResult.OK)
          {
            // calculate the mount point
            GLPoint point;
            if(selectedLink == -1)
            {
              point = parent.SceneToLocal(SceneView.ClientToScene(e.Location));
            }
            else
            {
              point = links[selectedLink].Offset.ToPoint();
            }
            
            child.Mount(parent, point.X, point.Y, dialog.OwnedByParent, dialog.TrackRotation, dialog.InheritProperties);
            
            EditorApp.MainForm.StatusText = "Object mounted.";
            ObjectTool.SubTool = ObjectTool.SpatialTool;
          }

          return true;
        }
        else
        {
          return false;
        }
      }

      public override void MouseMove(MouseEventArgs e)
      {
        if(state == State.SelectingMount)
        {
          RecalculateSelectedLink();
        }
      }

      public override void PaintDecoration(Graphics g)
      {
        if(state == State.SelectingMount)
        {
          for(int i=0; i<links.Length; i++)
          {
            if(links[i].Object == null)
            {
              DrawLinkPoint(g, i);
            }
          }

          Point mousePoint;
          if(selectedLink != -1)
          {
            mousePoint = SceneView.SceneToClient(links[selectedLink].ScenePoint);
          }
          else
          {
            mousePoint = Panel.PointToClient(Control.MousePosition);
          }

          Rectangle rect = new Rectangle(mousePoint.X-4, mousePoint.Y-4, 9, 9);
          g.FillRectangle(Brushes.Green, rect);
          g.DrawRectangle(Pens.White, rect);
        }
      }

      enum State
      {
        SelectingObject,
        SelectingMount,
      }

      void RecalculateSelectedLink()
      {
        selectedLink = -1;
        
        bool snapToPoints = (Control.ModifierKeys & Keys.Shift) == 0;
        if(snapToPoints)
        {
          Point mousePoint = Panel.PointToClient(Control.MousePosition);
          int  closestDist = int.MaxValue;

          for(int i=0; i<links.Length; i++)
          {
            if(snapToPoints)
            {
              const int threshold = 12 * 12; // it will snap at 12 pixels distance
              Point linkPoint = SceneView.SceneToClient(links[i].ScenePoint);
              int xd = linkPoint.X-mousePoint.X, yd = linkPoint.Y-mousePoint.Y, dist = xd*xd + yd*yd;
              if(dist <= threshold && dist < closestDist)
              {
                closestDist  = dist;
                selectedLink = i;
              }
            }
          }
        }

        Editor.InvalidateDecoration();
      }

      void DrawLinkPoint(Graphics g, int linkPoint)
      {
        Rectangle rect = GetLinkRect(linkPoint);
        g.FillRectangle(Brushes.Blue, rect);
        g.DrawRectangle(Pens.White, rect);
      }

      Rectangle GetLinkRect(int linkPoint)
      {
        Point pt = SceneView.SceneToClient(links[linkPoint].ScenePoint);
        return new Rectangle(pt.X-2, pt.Y-2, 5, 5);
      }
      
      double previousViewSize;
      SceneObject child, parent;
      LinkPoint[] links;
      int selectedLink = -1;
      State state;
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
        else if(e.Button == MouseButtons.Middle) // middle click swaps between object/vector mode
        {
          ObjectTool.SubTool = ObjectTool.VectorTool;
        }
        // a plain right click opens a context menu for the selected items
        else if(e.Button == MouseButtons.Right && Control.ModifierKeys == Keys.None)
        {
          ContextMenu menu = new ContextMenu();

          // if there are no items selected or the mouse cursor is not over them, try to select the item under the mouse
          if(SelectedObjects.Count == 0 || !ObjectTool.selectedObjectBounds.Contains(e.Location))
          {
            SelectObject(e.Location, true);
          }

          if(SelectedObjects.Count != 0)
          {
            if(SelectedObjects.Count == 1)
            {
              SceneObject obj = SelectedObjects[0];
              menu.MenuItems.Add("Edit collision area", menu_EditCollision);
              menu.MenuItems.Add("Edit link points", menu_EditLinks);
              if(obj.Mounted)
              {
                menu.MenuItems.Add("Dismount object", menu_Dismount);
              }
              else
              {
                menu.MenuItems.Add("Mount to another object", menu_Mount);
              }
              if(VectorSubTool.IsValidObject(obj))
              {
                menu.MenuItems.Add("Edit vector properties",
                                   delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.VectorTool; });
              }
              menu.MenuItems.Add("-");
            }

            menu.MenuItems.Add(new MenuItem("Delete object(s)", menu_Delete, Shortcut.Del));
            menu.MenuItems.Add("Export object(s)", menu_Export);
            
            foreach(SceneObject obj in SelectedObjects)
            {
              if(obj.Rotation != 0)
              {
                menu.MenuItems.Add("Reset rotation", menu_ResetRotation);
                break;
              }
            }
            
            menu.MenuItems.Add("-");
          }

          menu.MenuItems.Add("New vertex shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, true); });
          menu.MenuItems.Add("New spline shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, false); });

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

        dragHandle = GetHandleUnderPoint(e.Location);

        // if zero or one objects are selected and the mouse cursor is not over a control handle,
        // select the object under the pointer
        if(dragHandle == Handle.None && SelectedObjects.Count < 2)
        {
          SceneObject obj = ObjectTool.ObjectUnderPoint(e.Location);
          if(obj != null) ObjectTool.SelectObject(obj, true);
          dragHandle = GetHandleUnderPoint(e.Location);
        }

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
        double rotation = Math2D.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location)) - dragRotation;
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
        dragRotation = Math2D.AngleBetween(dragCenter, SceneView.ClientToScene(e.Location));
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
          Editor.InvalidateDecoration();
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
            using(Pen pen = new Pen(ObjectTool.GetInverseBackgroundColor()))
            {
              // draw the control rectangle around the selected items (we'll use the opposite of the background color)
              g.DrawRectangle(pen, GetHandleBorder());
              foreach(Handle handle in GetHandles())
              {
                DrawControlHandle(g, pen, handle);
              }
            }
          }
        }

        if(dragMode == DragMode.Select)
        {
          using(Pen pen = new Pen(ObjectTool.GetInverseBackgroundColor()))
          {
            g.DrawRectangle(pen, dragBox);
          }
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
        ObjectTool.InvalidateSelectedBounds(true);
        foreach(SceneObject obj in SelectedObjects)
        {
          Scene.RemoveObject(obj);
        }
        ObjectTool.DeselectObjects();
      }

      void DrawControlHandle(Graphics g, Pen pen, Handle handle)
      {
        Rectangle rect = GetHandleRect(handle);
        g.FillRectangle(Brushes.Blue, rect);
        g.DrawRectangle(pen, rect);
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

      List<SceneObject> ObjectsInRect(Rectangle rect)
      {
        IEnumerator<SceneObject> e = Scene.PickRectangle(SceneView.ClientToScene(rect), Editor.GetPickerOptions())
                                          .GetEnumerator();
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
        // clicking on a control handle should not change the selection
        if(GetHandleUnderPoint(pt) != Handle.None) return;

        SceneObject obj = ObjectTool.ObjectUnderPoint(pt);
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
        SceneObject obj = ObjectTool.ObjectUnderPoint(pt);
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
        MessageBox.Show("Not implemented yet."); // TODO: implement
      }

      void menu_EditLinks(object sender, EventArgs e)
      {
        ObjectTool.SubTool = ObjectTool.LinksTool;
      }

      void menu_Mount(object sender, EventArgs e)
      {
        ObjectTool.SubTool = ObjectTool.MountTool;
      }

      void menu_Dismount(object sender, EventArgs e)
      {
        SelectedObjects[0].Dismount();
      }

      void menu_Export(object sender, EventArgs e)
      {
        MessageBox.Show("This used to work, but got erased when visual studio got bitchy! Boo."); // TODO: implement
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

    #region VectorTool
    public sealed class VectorSubTool : ObjectSubTool
    {
      public VectorSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }

      public override void Activate()
      {
        Editor.propertyGrid.PropertyValueChanged += propertyGrid_PropertyValueChanged;

        AnimatedObject selectedObject = null;
        foreach(SceneObject obj in ObjectTool.selectedObjects)
        {
          if(IsValidObject(obj))
          {
            selectedObject = obj as AnimatedObject;
            break;
          }
        }

        ObjectTool.DeselectObjects();
        if(selectedObject != null)
        {
          SelectObject(selectedObject);
          UpdatePropertyGrid();
        }
        else
        {
          DeselectObject();
        }
      }

      public override void Deactivate()
      {
        Editor.propertyGrid.PropertyValueChanged -= propertyGrid_PropertyValueChanged;
        DeselectPoints();
        selectedPoly = 0;
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        char c = (char)e.KeyValue;
        if(down && e.KeyCode == Keys.Delete)
        {
          DeleteSelection();
          e.Handled = true;
        }
      }

      public override bool MouseClick(MouseEventArgs e)
      {
        if(e.Button == MouseButtons.Left)
        {
          if(Control.ModifierKeys == Keys.None)
          {
            SelectSingleVertex(e.Location);
            return true;
          }
          // shift-click toggles selection of individual vertices.
          else if(Control.ModifierKeys == Keys.Shift)
          {
            if(SelectedObject == null)
            {
              SelectObject(e.Location, SelectMode.Select);
            }
            else
            {
              TrySelectObjectAndPoint(SelectedObject, e.Location, SelectMode.Toggle);
            }
          }
          // ctrl-shift-click selects contiguous vertices
          else if(Control.ModifierKeys == (Keys.Shift|Keys.Control))
          {
            if(selectedPoints.Count == 0)
            {
              SelectSingleVertex(e.Location);
            }
            else
            {
              int endPoint = PointUnderCursor(e.Location); // this will only select points within the current polygon
              if(endPoint == -1) return true;
              int startPoint = selectedPoints[selectedPoints.Count-1];
              
              // always select points in a clockwise fashion
              if(startPoint <= endPoint)
              {
                for(int i=startPoint; i<=endPoint; i++)
                {
                  SelectVertex(i, false);
                }
              }
              else
              {
                for(int i=startPoint; i<SelectedPoly.Vertices.Count; i++)
                {
                  SelectVertex(i, false);
                }
                for(int i=0; i<=endPoint; i++)
                {
                  SelectVertex(i, false);
                }
              }
            }
          }
          // ctrl-click breaks/joins the shape at that point
          else if(Control.ModifierKeys == Keys.Control)
          {
            if(SelectObject(e.Location, SelectMode.DeselectPoints) && selectedPoints.Count == 1)
            {
              VectorAnimation.Vertex vertex = SelectedPoly.Vertices[selectedPoints[0]];
              vertex.Split = !vertex.Split;
              ObjectTool.InvalidateSelectedBounds(true);
            }
          }
        }
        else if(e.Button == MouseButtons.Middle) // middle click swaps between object/vector mode
        {
          ObjectTool.SubTool = ObjectTool.SpatialTool;
        }
        else if(e.Button == MouseButtons.Right)
        {
          SelectObject(e.Location, SelectMode.Select);
          ContextMenu menu = new ContextMenu();

          if(SelectedObject != null)
          {
            if(selectedPoints.Count != SelectedPoly.Vertices.Count)
            {
              menu.MenuItems.Add("Select all vertices", delegate(object s, EventArgs a) { SelectAllPoints(); });
            }

            if(selectedPoints.Count != 0) // operations upon the selected vertices
            {
              if(selectedPoints.Count != SelectedPoly.Vertices.Count)
              {
                menu.MenuItems.Add("Delete selected vertices",
                                   delegate(object s, EventArgs a) { DeleteSelectedPoints(); });
                menu.MenuItems.Add("-");
              }
            }

            // if it only has one frame, it can be joined with another vector object
            if(SelectedAnimation.Frames.Count == 1)
            {
              menu.MenuItems.Add("Join with another object",
                                 delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.JoinTool; });
            }

            if(SelectedPoly.HasSpline)
            {
              menu.MenuItems.Add("Convert spline polygon to vertex polygon", menu_ConvertPolyToVertexPoly);
            }

            // if it has multiple polygons, the order of the current polygon can be changed
            if(SelectedFrame.Polygons.Count > 1)
            {
              bool hasSpline = false;
              for(int i=0; i<SelectedFrame.Polygons.Count; i++)
              {
                if(SelectedFrame.Polygons[i].HasSpline)
                {
                  hasSpline = true;
                  break;
                }
              }

              if(hasSpline)
              {
                menu.MenuItems.Add("Convert spline shape to vertex shape", menu_ConvertShapeToVertexShape);
              }

              menu.MenuItems.Add("Split polygon into separate shape");
              menu.MenuItems.Add("Delete selected polygon",
                                 delegate(object s, EventArgs a) { DeleteSelectedPolygon(); });

              if(selectedPoly != 0)
              {
                menu.MenuItems.Add("Move polygon towards back", delegate(object s, EventArgs a) { MovePolygon(-1); });
              }
              if(selectedPoly != SelectedFrame.Polygons.Count-1)
              {
                menu.MenuItems.Add("Move polygon towards front", delegate(object s, EventArgs a) { MovePolygon(1); });
              }
            }

            menu.MenuItems.Add("Delete shape", delegate(object s, EventArgs a) { DeleteSelectedObject(); });
            menu.MenuItems.Add("Edit shape properties",
                               delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.SpatialTool; });
            menu.MenuItems.Add("Edit in animation editor");
            menu.MenuItems.Add("-");
          }

          menu.MenuItems.Add("New vertex shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, true); });
          menu.MenuItems.Add("New spline shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, false); });

          menu.Show(Panel, e.Location);
          return true;
        }

        return false;
      }
      
      void SelectSingleVertex(Point pt)
      {
        if(!SelectObject(pt, SelectMode.DeselectPoints))
        {
          DeselectObject();
        }
      }

      public override bool MouseDragStart(MouseEventArgs e)
      {
        if(e.Button == MouseButtons.Left) // for a left drag, move the selected vertex/polygon/object
        {
          if(!SelectObject(e.Location, selectedPoints.Count > 1 ? SelectMode.DeselectIfNone : SelectMode.DeselectPoints))
          {
            return false;
          }

          dragPoints.Clear();
          // if no points or all the points are selected, move the whole polygon or object
          if(selectedPoints.Count == 0 || selectedPoints.Count == SelectedPoly.Vertices.Count)
          {
            if(SelectedFrame.Polygons.Count == 1) // if there's only one polygon, drag the whole object
            {
              dragPoints.Add(SelectedObject.Position);
            }
            else // otherwise, just drag the one polygon
            {
              for(int i=0; i<SelectedPoly.Vertices.Count; i++)
              {
                dragPoints.Add(SelectedObject.LocalToScene(SelectedPoly.Vertices[i].Position));
              }
            }
          }
          else // otherwise, just drag the selected points
          {
            foreach(int selectedPoint in selectedPoints)
            {
              dragPoints.Add(SelectedObject.LocalToScene(SelectedPoly.Vertices[selectedPoint].Position));
            }
          }
          
          dragMode = DragMode.MoveVertices;
          return true;
        }
        else if(e.Button == MouseButtons.Right) // for right drags, clone and move vertices
        {
          // if there wasn't a point under the mouse, return false
          if(!SelectObject(e.Location, SelectMode.DeselectPoints) || selectedPoints.Count != 1) return false;
          CloneAndSelectPoint(selectedPoints[0]);
          dragPoints.Clear();
          dragPoints.Add(SelectedObject.LocalToScene(SelectedPoly.Vertices[selectedPoints[0]].Position));
          dragMode = DragMode.MoveVertices;
          return true;
        }
        else if(e.Button == MouseButtons.Middle) // for middle drags, manipulate the texture
        {
          // if there wasn't a polygun under the mouse, or the polygon has no texture, or it doesn't define a valid
          // area, cancel the drag
          if(!SelectObject(e.Location, SelectMode.DeselectPoints) || string.IsNullOrEmpty(SelectedPoly.Texture) ||
             SelectedPoly.Vertices.Count < 3)
          {
            return false;
          }

          // get the size of the polygon in scene coordinates, so we can scale the texture movement by the mouse
          // movement.
          double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
          foreach(VectorAnimation.Vertex vertex in SelectedPoly.Vertices)
          {
            GLPoint pt = vertex.Position;
            if(pt.X < x1) x1 = pt.X;
            if(pt.X > x2) x2 = pt.X;
            if(pt.Y < y1) y1 = pt.Y;
            if(pt.Y > y2) y2 = pt.Y;
          }
          Vector sceneSize = SelectedObject.LocalToScene(new Vector(x2-x1, y2-y1));
          GLPoint sceneCenter = SelectedObject.LocalToScene(x1, y1) + sceneSize*0.5;
          dragSize = Math.Max(sceneSize.X, sceneSize.Y);

          if(Control.ModifierKeys == Keys.None)
          {
            dragVector = SelectedPoly.TextureOffset;
            dragMode = DragMode.MoveTexture;
          }
          else if(Control.ModifierKeys == Keys.Control)
          {
            dragVector = new Vector(sceneCenter);
            dragRotation = Math2D.AngleBetween(sceneCenter, SceneView.ClientToScene(e.Location)) -
                           SelectedPoly.TextureRotation * MathConst.DegreesToRadians;
            dragMode = DragMode.RotateTexture;
          }
          else if(Control.ModifierKeys == Keys.Shift)
          {
            dragVector = new Vector(sceneCenter);
            dragZoom = SelectedPoly.TextureRepeat;
            dragMode = DragMode.ZoomTexture;
          }
          else
          {
            return false;
          }
          
          return true;
        }

        return false;
      }

      public override void MouseDrag(MouseDragEventArgs e)
      {
        if(dragMode == DragMode.MoveVertices)
        {
          DragVertices(e);
        }
        else if(dragMode == DragMode.MoveTexture)
        {
          DragTexture(e);
        }
        else if(dragMode == DragMode.RotateTexture)
        {
          RotateTexture(e);
        }
        else if(dragMode == DragMode.ZoomTexture)
        {
          ZoomTexture(e);
        }
      }

      public override void MouseDragEnd(MouseDragEventArgs e)
      {
        // if we were moving the whole object, just invalidate the render
        if((selectedPoints.Count == 0 || selectedPoints.Count == SelectedPoly.Vertices.Count) &&
         SelectedFrame.Polygons.Count == 1)
        {
          Editor.InvalidateRender();
        }
        else // otherwise, we'll need to recalculate the object bounds
        {
          ObjectTool.InvalidateSelectedBounds(true);
          RecalculateObjectBounds();
          ObjectTool.RecalculateAndInvalidateSelectedBounds();
        }
      }

      void DragTexture(MouseDragEventArgs e)
      {
        Vector sceneDist = SceneView.ClientToScene(new Size(e.X-e.Start.X, e.Y-e.Start.Y));
        SelectedPoly.TextureOffset = dragVector + sceneDist/dragSize;
        ObjectTool.InvalidateSelectedBounds(true);
      }
      
      void RotateTexture(MouseDragEventArgs e)
      {
        double rotation = Math2D.AngleBetween(dragVector.ToPoint(), SceneView.ClientToScene(e.Location)) - dragRotation;
        SelectedPoly.TextureRotation = EngineMath.NormalizeAngle(rotation * MathConst.RadiansToDegrees);
        ObjectTool.InvalidateSelectedBounds(true);
      }
      
      void ZoomTexture(MouseDragEventArgs e)
      {
        GLPoint currentPoint = SceneView.ClientToScene(e.Location), startPoint = SceneView.ClientToScene(e.Start);
        double currentDist = currentPoint.DistanceTo(dragVector.ToPoint()),
                 startDist = startPoint.DistanceTo(dragVector.ToPoint());
        if(currentDist < 0.00001) return; // prevent divide by zero if the user moves the mouse close to the center
        SelectedPoly.TextureRepeat = dragZoom * (startDist / currentDist);
        ObjectTool.InvalidateSelectedBounds(true);
      }

      void DragVertices(MouseDragEventArgs e)
      {
        ObjectTool.InvalidateSelectedBounds(true);

        Size clientDist = new Size(e.X-e.Start.X, e.Y-e.Start.Y);

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

        // move the selected polygon/object if no points or all points are selected
        if(selectedPoints.Count == 0 || selectedPoints.Count == SelectedPoly.Vertices.Count)
        {
          if(SelectedFrame.Polygons.Count == 1) // if there's only one polygon, move the whole object
          {
            SelectedObject.Position = dragPoints[0] + sceneDist;
          }
          else // otherwise, move the selected polygon
          {
            for(int i=0; i<SelectedPoly.Vertices.Count; i++)
            {
              SelectedPoly.Vertices[i].Position = SelectedObject.SceneToLocal(dragPoints[i] + sceneDist);
            }
          }
        }
        else // otherwise, just move the selected points
        {
          for(int i=0; i<selectedPoints.Count; i++)
          {
            SelectedPoly.Vertices[selectedPoints[i]].Position = SelectedObject.SceneToLocal(dragPoints[i] + sceneDist);
          }
        }

        RecalculateObjectBounds();
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      internal void RecalculateObjectBounds()
      {
        // calculate the minimum bounding box to contain the points, assign the bounding box, and then readjust the
        // position of all points so that they fit within the new bounding box without moving

        double x1=double.MaxValue, y1=double.MaxValue, x2=double.MinValue, y2=double.MinValue; // our local-space bounds
        foreach(VectorAnimation.Polygon poly in SelectedFrame.Polygons)
        {
          foreach(VectorAnimation.Vertex vertex in poly.Vertices)
          {
            if(vertex.Position.X < x1) x1 = vertex.Position.X;
            if(vertex.Position.Y < y1) y1 = vertex.Position.Y;
            if(vertex.Position.X > x2) x2 = vertex.Position.X;
            if(vertex.Position.Y > y2) y2 = vertex.Position.Y;
          }
        }

        Vector localBounds = new Vector(x2-x1, y2-y1);
        double localSize   = Math.Max(localBounds.X, localBounds.Y);
        double localScale  = 2/localSize;
        Vector offset      = (new Vector(localSize, localSize) - localBounds) * 0.5 * localScale + new Vector(-1, -1);

        // we'll make the new bounds a square for ease-of-use, so find the longest axis. the new data will be centered
        // within the object.
        Vector sceneBounds = SelectedObject.LocalToScene(localBounds);
        double sceneSize   = Math.Max(sceneBounds.X, sceneBounds.Y);
        SelectedObject.Position = SelectedObject.LocalToScene(new GLPoint(x1+localBounds.X*0.5, y1+localBounds.Y*0.5));
        SelectedObject.Size     = new Vector(sceneSize, sceneSize);

        foreach(VectorAnimation.Polygon poly in SelectedFrame.Polygons)
        {
          for(int i=0; i<poly.Vertices.Count; i++)
          {
            VectorAnimation.Vertex vertex = poly.Vertices[i];
            // find the offset of the point within the old local bounds, scale it to be from 0 to 2, and offset it to
            // be from -1 to 1, centered in the object.
            Vector newPos = (new Vector(vertex.Position.X - x1, vertex.Position.Y - y1) * localScale + offset);
            vertex.Position = newPos.ToPoint();
          }
        }
      }

      public override void PaintDecoration(Graphics g)
      {
        VectorAnimation.Polygon poly = SelectedPoly;
        if(poly != null)
        {
          Point[] points = new Point[poly.Vertices.Count];
          for(int i=0; i<points.Length; i++)
          {
            points[i] = SceneView.SceneToClient(SelectedObject.LocalToScene(poly.Vertices[i].Position));
          }

          if(points.Length > 1)
          {
            g.DrawLines(Pens.Green, points);
            g.DrawLine(Pens.Green, points[points.Length-1], points[0]);
          }

          for(int i=0; i<points.Length; i++)
          {
            Rectangle rect = new Rectangle(points[i].X-2, points[i].Y-2, 5, 5);
            g.FillRectangle(selectedPoints.Contains(i) ? Brushes.Red : Brushes.Green, rect);
            g.DrawRectangle(Pens.White, rect);
          }
        }
      }

      [Flags]
      enum SelectMode
      {
        Select=0,
        Toggle=1,

        DeselectPoints=2,
        DeselectIfNone=4,
        DeselectMask=6,
      }
      
      enum DragMode
      {
        MoveVertices,
        MoveTexture,
        RotateTexture,
        ZoomTexture
      }

      VectorAnimation SelectedAnimation
      {
        get { return SelectedObject == null ? null : SelectedObject.Animation as VectorAnimation; }
      }

      VectorAnimation.Frame SelectedFrame
      {
        get { return SelectedObject == null ? null : SelectedAnimation.Frames[0]; }
      }

      AnimatedObject SelectedObject
      {
        get { return (AnimatedObject)ObjectTool.SelectedObject; }
      }
      
      VectorAnimation.Polygon SelectedPoly
      {
        get { return SelectedObject == null ? null : SelectedFrame.Polygons[selectedPoly]; }
      }

      void CloneAndSelectPoint(int vertexIndex)
      {
        SelectedPoly.InsertVertex(vertexIndex, SelectedPoly.Vertices[vertexIndex].Clone());
        SelectVertex(vertexIndex+1, true);
      }

      void DeleteSelection()
      {
        if(selectedPoints.Count != 0)
        {
          DeleteSelectedPoints();
        }
        else if(SelectedObject != null)
        {
          DeleteSelectedPolygon();
        }
      }

      void DeleteSelectedPoints()
      {
        if(selectedPoints.Count != 0)
        {
          selectedPoints.Sort();
          for(int i=selectedPoints.Count-1; i>=0; i--)
          {
            SelectedPoly.RemoveVertex(selectedPoints[i]);
          }
          selectedPoints.Clear();

          if(SelectedPoly.Vertices.Count == 0)
          {
            DeleteSelectedPolygon();
          }
          else
          {
            OnSelectionChanged();
            OnDeleted();
          }
        }
      }

      void DeleteSelectedPolygon()
      {
        if(SelectedFrame.Polygons.Count == 1)
        {
          DeleteSelectedObject();
        }
        else
        {
          int selection = selectedPoly;
          SelectedFrame.RemovePolygon(selectedPoly);
          selectedPoly = 0;

          if(selection >= SelectedFrame.Polygons.Count)
          {
            selection--;
          }
          SelectPolygon(selection);
          OnDeleted();
        }
      }

      void DeleteSelectedObject()
      {
        if(SelectedObject != null)
        {
          SceneObject obj = SelectedObject;
          ObjectTool.InvalidateSelectedBounds(true);
          DeselectObject();
          Scene.RemoveObject(obj);
        }
      }

      void DeselectObject()
      {
        if(SelectedObject != null)
        {
          DeselectPoints();
          ObjectTool.DeselectObjects();
          selectedPoly = 0;
          OnSelectionChanged();
        }
      }

      void DeselectPoints()
      {
        selectedPoints.Clear();
        OnSelectionChanged();
      }

      public static bool IsValidObject(SceneObject obj)
      {
        AnimatedObject animObj = obj as AnimatedObject;
        if(animObj != null)
        {
          VectorAnimation anim = animObj.Animation as VectorAnimation;
          if(anim == null || anim.Frames.Count == 0)
          {
            return false;
          }

          return true;
        }

        return false;
      }

      void MovePolygon(int offset) // offset is assumed to be -1 or 1
      {
        if(offset == -1 && selectedPoly > 0 ||
         offset == 1 && selectedPoly < SelectedFrame.Polygons.Count-1)
        {
          VectorAnimation.Polygon poly = SelectedPoly;
          SelectedFrame.RemovePolygon(selectedPoly);
          SelectedFrame.InsertPolygon(selectedPoly + offset, poly);
          SelectPolygon(selectedPoly + offset);
          ObjectTool.InvalidateSelectedBounds(true);
        }
      }

      void SelectAllPoints()
      {
        if(selectedPoints.Count != SelectedPoly.Vertices.Count)
        {
          selectedPoints.Clear();
          for(int i=0; i<SelectedPoly.Vertices.Count; i++)
          {
            selectedPoints.Add(i);
          }
          OnSelectionChanged();
        }
      }

      bool SelectObject(Point pt, SelectMode mode)
      {
        Rectangle clientRect = SceneView.ClientRect;
        clientRect.Inflate(DecorationRadius, DecorationRadius);

        foreach(SceneObject obj in Scene.PickRectangle(SceneView.ClientToScene(clientRect),
                                                       Editor.GetPickerOptions()))
        {
          if(IsValidObject(obj) && TrySelectObjectAndPoint((AnimatedObject)obj, pt, mode))
          {
            return true;
          }
        }

        if((mode&SelectMode.DeselectMask) == SelectMode.DeselectIfNone)
        {
          DeselectPoints();
        }
        return false;
      }

      int PointUnderCursor(Point pt)
      {
        return PointUnderCursor(SelectedObject, selectedPoly, pt);
      }
      
      int PointUnderCursor(AnimatedObject obj, int polyIndex, Point pt)
      {
        VectorAnimation.Polygon poly = ((VectorAnimation)obj.Animation).Frames[0].Polygons[polyIndex];
        for(int i=0; i<poly.Vertices.Count; i++)
        {
          // compare vertices in window space
          Point vertex = SceneView.SceneToClient(obj.LocalToScene(poly.Vertices[i].Position));
          int xd = vertex.X-pt.X, yd=vertex.Y-pt.Y, distSqr=xd*xd+yd*yd;
          if(distSqr<=32) // select points within approx 5.66 pixels
          {
            return i;
          }
        }
        return -1;
      }

      bool SelectPolygonAndPoint(AnimatedObject obj, int polyIndex, Point pt, SelectMode mode)
      {
        int pointIndex = PointUnderCursor(obj, polyIndex, pt);
        if(pointIndex != -1)
        {
          SelectObject(obj);
          SelectPolygon(polyIndex);
          if((mode&SelectMode.Toggle) != 0 && selectedPoints.Contains(pointIndex))
          {
            selectedPoints.Remove(pointIndex);
            OnSelectionChanged();
          }
          else
          {
            SelectVertex(pointIndex, (mode&SelectMode.DeselectMask) == SelectMode.DeselectPoints);
          }
          return true;
        }

        if((mode&SelectMode.DeselectMask) == SelectMode.DeselectPoints)
        {
          DeselectPoints();
        }
        return false;
      }

      void SelectObject(AnimatedObject obj)
      {
        if(obj != SelectedObject)
        {
          ObjectTool.SelectObject(obj, true);
          ObjectTool.InvalidateSelectedBounds(false);
          selectedPoly = -1;
          SelectPolygon(0);
        }
      }

      void SelectPolygon(int polyIndex)
      {
        if(polyIndex != selectedPoly)
        {
          selectedPoly = polyIndex;
          DeselectPoints();
        }
      }

      void SelectVertex(int vertexIndex, bool deselectOthers)
      {
        if(deselectOthers)
        {
          if(selectedPoints.Count == 1 && selectedPoints[0] == vertexIndex)
          {
            return;
          }
          selectedPoints.Clear();
        }

        if(deselectOthers || !selectedPoints.Contains(vertexIndex))
        {
          selectedPoints.Add(vertexIndex);
          OnSelectionChanged();
        }
      }

      bool TrySelectObjectAndPoint(AnimatedObject obj, Point pt, SelectMode mode)
      {
        GLPoint localPoint = obj.SceneToLocal(SceneView.ClientToScene(pt));

        // scan the polygons from the top down
        VectorAnimation.Frame frame = ((VectorAnimation)obj.Animation).Frames[0];
        for(int polyIndex=frame.Polygons.Count-1; polyIndex >= 0; polyIndex--)
        {
          // if the polygon contains the point, select it and then 
          if(PolygonContains(frame.Polygons[polyIndex], localPoint))
          {
            SelectObject(obj);
            SelectPolygon(polyIndex);
            SelectPolygonAndPoint(obj, polyIndex, pt, mode);
            return true;
          }
          else if(SelectPolygonAndPoint(obj, polyIndex, pt, mode))
          {
            return true;
          }
        }

        return false;
      }

      void OnDeleted()
      {
        ObjectTool.InvalidateSelectedBounds(true);
        RecalculateObjectBounds();
      }

      void OnSelectionChanged()
      {
        UpdatePropertyGrid();
        Editor.InvalidateDecoration();
      }

      void UpdatePropertyGrid()
      {
        if(SelectedObject == null)
        {
          Editor.HideRightPane();
          Editor.propertyGrid.SelectedObjects = null;
        }
        else
        {
          if(selectedPoints.Count == 0)
          {
            Editor.propertyGrid.SelectedObject = SelectedPoly;
          }
          else
          {
            object[] vertices = new object[selectedPoints.Count];
            for(int i=0; i<vertices.Length; i++)
            {
              vertices[i] = SelectedPoly.Vertices[selectedPoints[i]];
            }
            Editor.propertyGrid.SelectedObjects = vertices;
          }

          Editor.ShowPropertyGrid();
        }
      }

      void menu_ConvertPolyToVertexPoly(object sender, EventArgs e)
      {
        DeselectPoints();
        VectorAnimation.Polygon vertexPoly = SelectedPoly.CloneAsVertexPolygon();
        SelectedFrame.RemovePolygon(selectedPoly);
        SelectedFrame.InsertPolygon(selectedPoly, vertexPoly);
        UpdatePropertyGrid();
      }

      void menu_ConvertShapeToVertexShape(object sender, EventArgs e)
      {
        DeselectPoints();
        for(int i=0; i<SelectedFrame.Polygons.Count; i++)
        {
          VectorAnimation.Polygon vertexPoly = SelectedFrame.Polygons[i].CloneAsVertexPolygon();
          SelectedFrame.RemovePolygon(i);
          SelectedFrame.InsertPolygon(i, vertexPoly);
        }
        UpdatePropertyGrid();
      }

      void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
      {
        Editor.InvalidateRender();
      }

      List<int> selectedPoints = new List<int>();
      List<GLPoint> dragPoints = new List<GLPoint>();
      Vector dragVector;
      double dragZoom, dragRotation, dragSize;
      int selectedPoly;
      DragMode dragMode;

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
    }
    #endregion

    JoinSubTool joinTool;
    LinksSubTool linksTool;
    MountSubTool mountTool;
    SpatialSubTool spatialTool;
    VectorSubTool vectorTool;
    #endregion

    #region Delegation to subtool
    public override void KeyPress(KeyEventArgs e, bool down)
    {
      SubTool.KeyPress(e, down);

      if(!e.Handled)
      {
        char c = char.ToLowerInvariant((char)e.KeyValue);
        if(e.Modifiers == Keys.Alt && c >= '0' && c <= '9')
        {
          int layer = c=='0' ? 9 : c-'1';
          foreach(SceneObject obj in selectedObjects)
          {
            obj.Layer = layer;
          }
          EditorApp.MainForm.StatusText = "Object(s) moved to layer "+layer;
          Editor.CurrentLayer = layer;
          Editor.InvalidateRender();
          e.Handled = true;
        }
        else if(down && e.Modifiers == Keys.Shift && c == 'v')
        {
          SubTool = VectorTool;
          e.Handled = true;
        }
        else if(down && e.Modifiers == Keys.Shift && c == 's')
        {
          SubTool = SpatialTool;
          e.Handled = true;
        }
      }
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

    void CreateShape(Point at, bool breakVertices)
    {
      VectorAnimation anim = new VectorAnimation();

      VectorAnimation.Frame frame = new VectorAnimation.Frame();
      anim.AddFrame(frame);

      VectorAnimation.Polygon poly = new VectorAnimation.Polygon();
      frame.AddPolygon(poly);

      VectorAnimation.Vertex vertex = new VectorAnimation.Vertex();
      vertex.Color    = GetInverseBackgroundColor();
      vertex.Position = new GLPoint(-1, -1);
      vertex.Split    = breakVertices;
      poly.AddVertex(vertex);

      vertex = vertex.Clone();
      vertex.Position = new GLPoint(1, -1);
      poly.AddVertex(vertex);
      vertex = vertex.Clone();
      vertex.Position = new GLPoint(1, 1);
      poly.AddVertex(vertex);
      vertex = vertex.Clone();
      vertex.Position = new GLPoint(-1, 1);
      poly.AddVertex(vertex);

      AnimatedObject obj = new AnimatedObject();
      obj.Animation = anim;
      obj.Layer     = Editor.CurrentLayer;
      obj.Position  = SceneView.ClientToScene(at);
      obj.Size      = new Vector(SceneView.CameraSize/10, SceneView.CameraSize/10);

      Scene.AddObject(obj);
      Editor.InvalidateRender();
      SelectObject(obj, true);

      SubTool = VectorTool;
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

    Color GetInverseBackgroundColor()
    {
      Color bgColor = SceneView.BackColor;
      return Color.FromArgb(255-bgColor.R, 255-bgColor.G, 255-bgColor.B);
    }

    bool IsSelected(SceneObject obj) { return selectedObjects.Contains(obj); }

    void InvalidateSelectedBounds(bool invalidateRender)
    {
      Rectangle rect = selectedObjectBounds;
      rect.Inflate(DecorationRadius, DecorationRadius);
      Editor.Invalidate(rect, invalidateRender);
    }

    SceneObject ObjectUnderPoint(Point pt)
    {
      foreach(SceneObject obj in Scene.PickPoint(SceneView.ClientToScene(pt), Editor.GetPickerOptions()))
      {
        return obj; // return the first item, if there are any
      }

      return null;
    }

    void OnSelectedObjectsChanged()
    {
      if(SubTool != null) SubTool.OnSelectedObjectsChanged();
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
    ZoomTool zoomTool;
  }

  EditTool CurrentTool
  {
    get { return currentTool; }
    set
    {
      if(value != currentTool)
      {
        if(currentTool != null)
        {
          currentTool.Deactivate();
        }

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

  #region Toolbox
  int AddIcon(Bitmap icon, int currentIndex)
  {
    if(currentIndex < systemDefinedIconCount) // if it's currently a system-defined icon, don't replace it
    {
      objectImgs.Images.Add(icon);
      return objectImgs.Images.Count - 1;
    }
    else
    {
      objectImgs.Images[currentIndex] = icon;
      return currentIndex;
    }
  }
  
  void RemoveIcon(int index)
  {
    foreach(ListViewItem item in objectList.Items)
    {
      if(item.ImageIndex >= index) item.ImageIndex--;
    }

    objectImgs.Images.RemoveAt(index);
  }
  
  void AddToolboxItem(ToolboxItem item)
  {
    ListViewItem lvItem = new ListViewItem(item.DisplayName, GenerateThumbnail(item),
                                           objectList.Groups[(int)item.Category]);
    lvItem.Tag = item;
    objectList.Items.Add(lvItem);
  }
  
  void SetToolboxItem(ToolboxItem item)
  {
    ToolboxItem.SetItem(item);

    foreach(ListViewItem lvItem in objectList.Items)
    {
      if(string.Equals(item.Name, ((ToolboxItem)lvItem.Tag).Name, StringComparison.Ordinal))
      {
        lvItem.ImageIndex = GenerateThumbnail(item, lvItem);
        lvItem.Tag = item;
        return;
      }
    }
    
    AddToolboxItem(item);
  }

  void EditImageMap(ImageMapDialog md, ImageMap oldMap, string mapFile)
  {
    if(md.ShowDialog() == DialogResult.OK)
    {
      using(Stream stream = File.Open(mapFile, FileMode.Create, FileAccess.Write))
      {
        Serializer.BeginBatch();
        Serializer.Serialize(md.ImageMap, stream);
        Serializer.EndBatch();
      }

      Engine.Engine.AddImageMap(md.ImageMap);
      SetToolboxItem(new StaticImageItem(md.ImageMap));
      md.ImageMap.InvalidateMode(); // since the image map is being used in a new context now, reset its texture mode
      InvalidateRender();
    }
    else if(md.ImageMap != oldMap) // dispose the image map if it's not the one we gave it, and we're not going to use it
    {
      md.ImageMap.Dispose();
    }
  }

  int GenerateThumbnail(ToolboxItem item)
  {
    return GenerateThumbnail(item, null);
  }
  
  int GenerateThumbnail(ToolboxItem item, ListViewItem lvItem)
  {
    if(item is TriggerItem)
    {
      return TriggerIcon;
    }
    else if(item is StaticImageItem)
    {
      ImageMap map = ((StaticImageItem)item).GetImageMap();
      if(map == null || map.Frames.Count == 0)
      {
        if(lvItem.ImageIndex >= systemDefinedIconCount)
        {
          RemoveIcon(lvItem.ImageIndex);
        }
        return StaticImageIcon;
      }
      else
      {
        Rectangle displayArea = new Rectangle(new Point(), map.Frames[0].Size);
        if(displayArea.Width > displayArea.Height)
        {
          displayArea.Height = 32 * displayArea.Height / displayArea.Width;
          displayArea.Width  = 32;
        }
        else
        {
          displayArea.Width  = 32 * displayArea.Width / displayArea.Height;
          displayArea.Height = 32;
        }
        displayArea.X = (32 - displayArea.Width)  / 2;
        displayArea.Y = (32 - displayArea.Height) / 2;
        
        GLBuffer.SetCurrent(iconBuffer);
        Engine.Engine.ResetOpenGL(32, 32, new Rectangle(0, 0, 32, 32));
        GL.glClear(GL.GL_COLOR_BUFFER_BIT);

        GL.glEnable(GL.GL_TEXTURE_2D);
        map.InvalidateMode();
        map.BindFrame(0);

        GL.glBegin(GL.GL_QUADS);
          GL.glTexCoord2d(map.GetTextureCoord(0, new GLPoint(0, 0)));
          GL.glVertex2i(displayArea.Left, displayArea.Top);

          GL.glTexCoord2d(map.GetTextureCoord(0, new GLPoint(1, 0)));
          GL.glVertex2i(displayArea.Right, displayArea.Top);

          GL.glTexCoord2d(map.GetTextureCoord(0, new GLPoint(1, 1)));
          GL.glVertex2i(displayArea.Right, displayArea.Bottom);

          GL.glTexCoord2d(map.GetTextureCoord(0, new GLPoint(0, 1)));
          GL.glVertex2i(displayArea.Left, displayArea.Bottom);
        GL.glEnd();
        GL.glDisable(GL.GL_TEXTURE_2D);
        GL.glFlush();
        GLBuffer.SetCurrent(null);

        return AddIcon(iconBuffer.CreateBitmap(), lvItem == null ? 0 : lvItem.ImageIndex);
      }
    }
    
    throw new NotImplementedException();
  }

  void objectList_DoubleClick(object sender, EventArgs e)
  {
    if(objectList.SelectedItems.Count != 1) return;
    
    ListViewItem lvItem = objectList.SelectedItems[0];
    ToolboxItem item = (ToolboxItem)lvItem.Tag;
    
    StaticImageItem staticImage = item as StaticImageItem;
    if(staticImage != null)
    {
      ImageMap map = staticImage.GetImageMap();
      string imagePath = Project.GetRealPath(map.ImageFile);
      ImageMapDialog md = new ImageMapDialog();
      md.Open(map);
      EditImageMap(md, map,
                   Path.Combine(Path.GetDirectoryName(imagePath), Path.GetFileNameWithoutExtension(imagePath)+".map"));
    }
  }

  void renderPanel_DragEnter(object sender, DragEventArgs e)
  {
    e.Effect = DragDropEffects.Copy;
  }

  void renderPanel_DragDrop(object sender, DragEventArgs e)
  {
    string itemName = (string)e.Data.GetData(DataFormats.StringFormat);
    ToolboxItem item = ToolboxItem.GetItem(itemName);
    if(item != null)
    {
      SceneObject obj = item.CreateSceneObject(sceneView);
      obj.Position = sceneView.ClientToScene(renderPanel.PointToClient(new Point(e.X, e.Y)));
      obj.Layer    = CurrentLayer;
      Scene.AddObject(obj);
      InvalidateRender();

      CurrentTool = Tools.Object;
      Tools.Object.SubTool = Tools.Object.SpatialTool;
      Tools.Object.SelectObject(obj, true);
    }
  }

  void newStaticImage_Click(object sender, EventArgs e)
  {
    OpenFileDialog ofd = new OpenFileDialog();
    ofd.Filter = "Image files (png;jpeg;bmp;pcx;gif)|*.png;*.jpg;*.jpeg;*.bmp;*.pcx;*.gif|All files (*.*)|*.*";
    ofd.Title  = "Select an image to import";
    ofd.InitialDirectory = Project.ImagesPath;
    if(ofd.ShowDialog() != DialogResult.OK) return;
    
    string fileName = ofd.FileName;
    if(!Project.IsUnderDataPath(fileName))
    {
      do
      {
        if(MessageBox.Show("This file is not under the project's data path. You'll need to copy it there to continue.",
                           "Copy file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
        {
          return;
        }

        SaveFileDialog sfd = new SaveFileDialog();
        sfd.FileName = Path.GetFileName(ofd.FileName);
        sfd.Filter   = "All files (*.*)|*.*";
        sfd.Title    = "Select the path to which the image will be copied";
        sfd.InitialDirectory = Project.ImagesPath;
        if(sfd.ShowDialog() != DialogResult.OK)
        {
          return;
        }

        fileName = sfd.FileName;
      } while(!Project.IsUnderDataPath(fileName));
      
      File.Copy(ofd.FileName, fileName, true);
    }

    string mapFile = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)+".map");
    string imgFile = Project.GetEnginePath(fileName);

    ImageMap imap = Project.GetImageMap(imgFile);
    if(imap != null)
    {
      MessageBox.Show("An image map for this image has already been loaded into the project. The existing map will "+
                      "be edited.", "Image map already exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    ImageMapDialog md = new ImageMapDialog();
    if(imap == null)
    {
      md.CreateNew(imgFile);
    }
    else
    {
      md.Open(imap);
    }

    EditImageMap(md, imap, mapFile);
  }
  #endregion
 
  #region RenderPanel UI events
  void renderPanel_KeyDown(object sender, KeyEventArgs e)
  {
    if(!renderPanel.Focused) return;
    currentTool.KeyPress(e, true);
    
    if(!e.Handled)
    {
      char c = (char)e.KeyValue;
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
      else if(e.Modifiers == Keys.None && c >= '0' && c <= '9') // plain number presses change current layer
      {
        CurrentLayer = c == '0' ? 9 : c-'1'; // '1'-'9' are layers 0-8. '0' is layer 9.
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

  void renderPanel_Resize(object sender, EventArgs e)
  {
    sceneView.Bounds = desktop.Bounds = renderPanel.ClientRectangle;
    currentTool.PanelResized();
  }
  #endregion

  #region Painting, rendering, and layout
  void renderPanel_RenderBackground(object sender, EventArgs e)
  {
    sceneView.Invalidate();
    desktop.Render();
  }
 
  void renderPanel_Paint(object sender, PaintEventArgs e)
  {
    currentTool.PaintDecoration(e.Graphics);
  }

  void SceneEditor_GotFocus(object sender, EventArgs e)
  {
    // since we can't easily propogate texture modes between rendering contexts, we'll simply reset the mode whenever
    // we get focus
    foreach(ImageMapHandle handle in Engine.Engine.GetImageMaps())
    {
      if(handle.ImageMap != null) handle.ImageMap.InvalidateMode();
    }
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
 
  PickOptions GetPickerOptions()
  {
    PickOptions options = new PickOptions();
    options.AllowInvisible  = true;
    options.AllowUnpickable = true;
    options.GroupMask       = 0xffffffff;
    options.LayerMask       = CurrentLayerMask;
    options.SortByLayer     = true;
    return options;
  }

  protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
  {
    base.OnClosing(e);
    if(HasUnsavedChanges)
    {
      DialogResult result = MessageBox.Show(Title+" has unsaved changes. Save changes?", "Save changes?",
                                            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning,
                                            MessageBoxDefaultButton.Button1);
      if(result == DialogResult.Cancel)
      {
        e.Cancel = true;
      }
      else if(result == DialogResult.Yes && !Save(false))
      {
        e.Cancel = true;
      }
    }
  }

  protected override void OnClosed(EventArgs e)
  {
    base.OnClosed(e);
    isClosed = true;
  }

  DesktopControl desktop;
  SceneViewControl sceneView;
  Project.Level level;
  int systemDefinedIconCount;
  bool isModified, isClosed;
  
  static GLBuffer iconBuffer = new GLBuffer(32, 32);
}

enum ToolboxCategory { StaticImages, AnimatedImages, VectorAnimations, Miscellaneous }

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
          DoDragDrop(((ToolboxItem)item.Tag).Name, DragDropEffects.Link|DragDropEffects.Copy);
        }
      }

      pressPoint = new Point(-1, -1);
    }
  }
  
  Point pressPoint = new Point(-1, -1);
}
#endregion

#region ToolboxItem
abstract class ToolboxItem
{
  public ToolboxItem(string name)
  {
    this.name = name;
  }

  public abstract ToolboxCategory Category { get; }
  public abstract string DisplayName { get; }

  public string Name
  {
    get { return name; }
  }
  
  public abstract SceneObject CreateSceneObject(SceneViewControl sceneView);

  public static void ClearItems()
  {
    items.Clear();
  }

  public static ToolboxItem GetItem(string name)
  {
    ToolboxItem item;
    items.TryGetValue(name, out item);
    return item;
  }

  public static ICollection<ToolboxItem> GetItems()
  {
    return items.Values;
  }

  public static void SetItem(ToolboxItem item)
  {
    if(item == null) throw new ArgumentNullException();
    items[item.Name] = item;
  }
  
  public static void RemoveItem(string itemName)
  {
    items.Remove(itemName);
  }

  string name;

  static Dictionary<string,ToolboxItem> items = new Dictionary<string,ToolboxItem>();
}

#region TriggerItem
sealed class TriggerItem : ToolboxItem
{
  public TriggerItem() : base("misc:__trigger__") { }

  public override ToolboxCategory Category
  {
    get { return ToolboxCategory.Miscellaneous; }
  }

  public override string DisplayName
  {
    get { return "Trigger"; }
  }

  public override SceneObject CreateSceneObject(SceneViewControl sceneView)
  {
    return new TriggerObject();
  }
}
#endregion

#region StaticImageItem
sealed class StaticImageItem : ToolboxItem
{
  public StaticImageItem(ImageMap map) : base("static:"+map.Name)
  {
    this.imageMapName = map.Name;
  }

  public override ToolboxCategory Category
  {
    get { return ToolboxCategory.StaticImages; }
  }

  public override string DisplayName
  {
    get { return imageMapName; }
  }
  
  public ImageMap GetImageMap()
  {
    return Engine.Engine.GetImageMap(imageMapName).ImageMap;
  }

  public override SceneObject CreateSceneObject(SceneViewControl sceneView)
  {
    ImageMap map = GetImageMap();
    if(map.Frames.Count == 0)
    {
      MessageBox.Show("This image map has no frames. Try editing the map.", "Uh oh.",
                      MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
      return null;
    }

    StaticImageObject obj = new StaticImageObject();
    obj.Size = sceneView.ClientToScene(map.Frames[0].Size);
    obj.ImageMap = map.Name;
    return obj;
  }

  string imageMapName;
}
#endregion
#endregion

} // namespace RotationalForce.Editor
