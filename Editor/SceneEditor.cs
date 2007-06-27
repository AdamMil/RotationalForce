using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RotationalForce.Engine;
using GameLib.Interop.OpenGL;
using GameLib.Video;
using MathConst = GameLib.Mathematics.MathConst;
using Math2D  = GameLib.Mathematics.TwoD.Math2D;
using GLPoint = GameLib.Mathematics.TwoD.Point;
using GLRect  = GameLib.Mathematics.TwoD.Rectangle;
using GLPoly  = GameLib.Mathematics.TwoD.Polygon;
using Vector  = GameLib.Mathematics.TwoD.Vector;
using Line    = GameLib.Mathematics.TwoD.Line;

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
  private ToolStripMenuItem editCopyMenuItem;
  private ToolStripMenuItem editPasteMenuItem;
  private ToolStripMenuItem editUnloadTraceItem;
  private ToolStripMenuItem resetPropertyValueMenuItem;
  private ToolStripMenuItem openColorPickerMenuItem;
  private TreeView treeView;
  private ToolStripMenuItem createVectorGroupMenuItem;
  private ContextMenuStrip vectorTreeMenu;
  private RenderPanel renderPanel;

  public SceneEditor()
  {
    toolbox = new Toolbox(this);

    InitializeComponent();
    systemDefinedIconCount = objectImgs.Images.Count;

    foreach(ToolboxItem item in ToolboxItem.GetItems())
    {
      try
      {
        AddToolboxItem(item);
      }
      catch(ResourceNotFoundException e)
      {
        MessageBox.Show("Could not load item '"+item.Name+"' because "+e.Message);
      }
    }

    toolBar.Items[0].Tag = Tools.Object;
    toolBar.Items[1].Tag = Tools.Layers;
    toolBar.Items[2].Tag = Tools.Zoom;

    CurrentTool = Tools.Object;
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    
    if(traceImage != null)
    {
      traceImage.Dispose();
    }
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
      string name = levelFile;
      if(string.IsNullOrEmpty(name))
      {
        name = "Level - Untitled";
      }
      return "Level - " + Path.GetFileNameWithoutExtension(name);
    }
  }

  public void CreateNew()
  {
    sceneView = new SceneViewControl();
    sceneView.BackColor = Color.Black;
    // use the minor camera axis so that we easily calculate the camera size needed to fully display a given object
    sceneView.CameraAxis      = CameraAxis.Minor;
    sceneView.CameraSize      = DefaultViewSize;
    sceneView.RenderInvisible = true;
    sceneView.Scene           = new Scene();

    localResources.Clear();
    desktop.BackColor = Color.Empty; // we want the whole background to come from the sceneview
    desktop.AddChild(sceneView);

    levelFile = null;
    InvalidateRender();
    isModified = false;
  }
  
  public bool Open()
  {
    OpenFileDialog fd = new OpenFileDialog();
    fd.DefaultExt       = "scene";
    fd.InitialDirectory = Project.LevelsPath.TrimEnd('/');
    fd.Filter           = "Level files (.scene)|*.scene";
    
    if(fd.ShowDialog() == DialogResult.OK)
    {
      using(SexpReader sr = new SexpReader(File.Open(fd.FileName, FileMode.Open, FileAccess.Read)))
      {
        Serializer.BeginBatch();
        Scene scene = (Scene)Serializer.Deserialize(sr);
        sceneView = (SceneViewControl)Serializer.Deserialize(sr);
        sceneView.Scene = scene;
        Serializer.EndBatch();
      }

      localResources.Clear();

      string localAnimPath = GetLocalAnimationPath(fd.FileName);
      if(File.Exists(localAnimPath))
      {
        using(SexpReader sr = new SexpReader(File.Open(localAnimPath, FileMode.Open, FileAccess.Read)))
        {
          Serializer.BeginBatch();
          while(!sr.EOF)
          {
            VectorShape shape = (VectorShape)Serializer.Deserialize(sr);
            Engine.Engine.AddResource<VectorShape>(shape);
            localResources.Add(shape.Name, null);
          }
          Serializer.EndBatch();
        }
      }

      desktop.BackColor = Color.Empty; // we want the whole background to come from the sceneview
      desktop.AddChild(sceneView);

      levelFile = fd.FileName;
      InvalidateRender();
      isModified = false;
      return true;
    }

    return false;
  }

  public bool Save(bool newFile)
  {
    string fileName = levelFile;

    if(string.IsNullOrEmpty(fileName))
    {
      newFile  = true;
      fileName = Project.GetLevelPath(fileName+"level 1.scene");
    }

    if(newFile)
    {
      SaveFileDialog fd = new SaveFileDialog();
      fd.DefaultExt = "scene";
      fd.FileName   = Path.GetFileName(fileName);
      fd.Filter     = "Levels (*.scene)|*.scene";
      fd.InitialDirectory = Path.GetDirectoryName(fileName);
      fd.OverwritePrompt  = true;
      fd.Title = "Save level as...";

      if(fd.ShowDialog() != DialogResult.OK)
      {
        return false;
      }
      
      fileName = fd.FileName;
    }

    using(SexpWriter writer = new SexpWriter(File.Open(fileName, FileMode.Create, FileAccess.Write)))
    {
      Serializer.BeginBatch();
      Serializer.Serialize(sceneView.Scene, writer);
      Serializer.Serialize(sceneView, writer);
      Serializer.EndBatch();
    }

    // create a dictionary of local resources currently in use. this way, we don't save local resources that aren't
    // in use.
    Dictionary<string,object> localResourcesUsed = new Dictionary<string,object>();
    foreach(SceneObject sceneObject in Scene.PickAll())
    {
      VectorObject vectorObject = sceneObject as VectorObject;
      if(vectorObject != null && IsLocalResource(vectorObject.ShapeName))
      {
        localResourcesUsed[vectorObject.ShapeName] = null;
      }
    }

    // save local animation resources to a per-level file
    string localAnimPath = GetLocalAnimationPath(fileName);
    if(localResourcesUsed.Count == 0)
    {
      File.Delete(localAnimPath);
    }
    else
    {
      Directory.CreateDirectory(Path.GetDirectoryName(localAnimPath));
      using(SexpWriter writer = new SexpWriter(File.Open(localAnimPath, FileMode.Create, FileAccess.Write)))
      {
        Serializer.BeginBatch();
        foreach(string resourceName in localResourcesUsed.Keys)
        {
          ResourceHandle<VectorShape> handle = Engine.Engine.GetResource<VectorShape>(resourceName);
          Serializer.Serialize(handle.Resource, writer);
        }
        Serializer.EndBatch();
      }
    }

    // now save non-local animations
    foreach(SceneObject sceneObject in Scene.PickAll())
    {
      VectorObject vectorObject = sceneObject as VectorObject;
      if(vectorObject != null && !IsLocalResource(vectorObject.ShapeName))
      {
        SaveSharedShape(vectorObject.Shape);
      }
    }

    levelFile = fileName;
    isModified = false;
    return true;
  }

  public bool TryClose()
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

  uint PickLayerMask
  {
    get
    {
      uint mask;

      if(pickToCurrentLayer)
      {
        mask = 0x80000000;
        for(int i=31; i>=CurrentLayer; i--)
        {
          mask = (mask>>1) | 0x80000000;
        }
      }
      else
      {
        mask = CurrentLayerMask;
      }

      return mask;
    }
  }

  bool PickToCurrentLayer
  {
    get { return pickToCurrentLayer; }
    set { pickToCurrentLayer = value; }
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
  bool renderToCurrentLayer, pickToCurrentLayer = true;
  #endregion

  string GetLocalAnimationPath(string sceneFile)
  {
    string path = GetPathMinus(sceneFile, Project.LevelsPath);
    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)+".shape");
    return Path.Combine(Project.PerLevelAnimationPath, path);
  }
  
  string GetPathMinus(string path, string directory)
  {
    path      = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    directory = directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    if(directory[directory.Length-1] != Path.DirectorySeparatorChar)
    {
      directory += Path.DirectorySeparatorChar;
    }

    if(path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
    {
      return path.Substring(directory.Length);
    }
    else
    {
      throw new ArgumentException("The path '"+path+"' is not within directory '"+directory+"'.");
    }
  }

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
    System.Windows.Forms.ToolStripSeparator menuSep1;
    System.Windows.Forms.ToolStripMenuItem editLoadTraceItem;
    System.Windows.Forms.ToolStripDropDownButton toolboxNewMenu;
    System.Windows.Forms.ToolStripMenuItem newStaticImage;
    System.Windows.Forms.SplitContainer mainSplitter;
    System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Static Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Animated Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Object Templates", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Vector Shapes", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Miscellaneous", System.Windows.Forms.HorizontalAlignment.Left);
    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SceneEditor));
    System.Windows.Forms.ContextMenuStrip propertyGridMenu;
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.editCopyMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.editPasteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.editUnloadTraceItem = new System.Windows.Forms.ToolStripMenuItem();
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    this.rightPane = new System.Windows.Forms.SplitContainer();
    this.treeView = new System.Windows.Forms.TreeView();
    this.objToolBar = new System.Windows.Forms.ToolStrip();
    this.objectList = new RotationalForce.Editor.ToolboxList();
    this.objectImgs = new System.Windows.Forms.ImageList(this.components);
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.resetPropertyValueMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.openColorPickerMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.toolBar = new System.Windows.Forms.ToolStrip();
    this.vectorTreeMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
    this.createVectorGroupMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.statusBar = new System.Windows.Forms.StatusStrip();
    this.mousePosLabel = new System.Windows.Forms.ToolStripStatusLabel();
    this.layerLabel = new System.Windows.Forms.ToolStripStatusLabel();
    selectTool = new System.Windows.Forms.ToolStripButton();
    layerTool = new System.Windows.Forms.ToolStripButton();
    cameraTool = new System.Windows.Forms.ToolStripButton();
    menuBar = new System.Windows.Forms.MenuStrip();
    menuSep1 = new System.Windows.Forms.ToolStripSeparator();
    editLoadTraceItem = new System.Windows.Forms.ToolStripMenuItem();
    toolboxNewMenu = new System.Windows.Forms.ToolStripDropDownButton();
    newStaticImage = new System.Windows.Forms.ToolStripMenuItem();
    mainSplitter = new System.Windows.Forms.SplitContainer();
    propertyGridMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
    menuBar.SuspendLayout();
    mainSplitter.Panel1.SuspendLayout();
    mainSplitter.Panel2.SuspendLayout();
    mainSplitter.SuspendLayout();
    this.renderPanel.SuspendLayout();
    this.rightPane.Panel1.SuspendLayout();
    this.rightPane.Panel2.SuspendLayout();
    this.rightPane.SuspendLayout();
    this.objToolBar.SuspendLayout();
    propertyGridMenu.SuspendLayout();
    this.toolBar.SuspendLayout();
    this.vectorTreeMenu.SuspendLayout();
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
    menuBar.Size = new System.Drawing.Size(600, 24);
    menuBar.TabIndex = 0;
    menuBar.Visible = false;
    // 
    // editMenu
    // 
    this.editMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editCopyMenuItem,
            this.editPasteMenuItem,
            menuSep1,
            editLoadTraceItem,
            this.editUnloadTraceItem});
    this.editMenu.MergeAction = System.Windows.Forms.MergeAction.Insert;
    this.editMenu.MergeIndex = 1;
    this.editMenu.Name = "editMenu";
    this.editMenu.Size = new System.Drawing.Size(37, 20);
    this.editMenu.Text = "&Edit";
    this.editMenu.DropDownOpening += new System.EventHandler(this.editMenu_DropDownOpening);
    // 
    // editCopyMenuItem
    // 
    this.editCopyMenuItem.Name = "editCopyMenuItem";
    this.editCopyMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
    this.editCopyMenuItem.Size = new System.Drawing.Size(174, 22);
    this.editCopyMenuItem.Text = "&Copy";
    this.editCopyMenuItem.Click += new System.EventHandler(this.editCopyMenuItem_Click);
    // 
    // editPasteMenuItem
    // 
    this.editPasteMenuItem.Name = "editPasteMenuItem";
    this.editPasteMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
    this.editPasteMenuItem.Size = new System.Drawing.Size(174, 22);
    this.editPasteMenuItem.Text = "&Paste";
    this.editPasteMenuItem.Click += new System.EventHandler(this.editPasteMenuItem_Click);
    // 
    // menuSep1
    // 
    menuSep1.Name = "menuSep1";
    menuSep1.Size = new System.Drawing.Size(171, 6);
    // 
    // editLoadTraceItem
    // 
    editLoadTraceItem.Name = "editLoadTraceItem";
    editLoadTraceItem.Size = new System.Drawing.Size(174, 22);
    editLoadTraceItem.Text = "Load tracing image";
    editLoadTraceItem.Click += new System.EventHandler(this.editLoadTraceItem_Click);
    // 
    // editUnloadTraceItem
    // 
    this.editUnloadTraceItem.Name = "editUnloadTraceItem";
    this.editUnloadTraceItem.Size = new System.Drawing.Size(174, 22);
    this.editUnloadTraceItem.Text = "Unload tracing image";
    this.editUnloadTraceItem.Click += new System.EventHandler(this.editUnloadTraceItem_Click);
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
    mainSplitter.Size = new System.Drawing.Size(772, 501);
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
    this.renderPanel.Size = new System.Drawing.Size(600, 501);
    this.renderPanel.TabIndex = 0;
    this.renderPanel.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseWheel);
    this.renderPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.renderPanel_MouseMove);
    this.renderPanel.RenderBackground += new System.Windows.Forms.PaintEventHandler(this.renderPanel_RenderBackground);
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
    this.rightPane.Panel1.Controls.Add(this.treeView);
    this.rightPane.Panel1.Controls.Add(this.objToolBar);
    this.rightPane.Panel1.Controls.Add(this.objectList);
    // 
    // rightPane.Panel2
    // 
    this.rightPane.Panel2.Controls.Add(this.propertyGrid);
    this.rightPane.Panel2Collapsed = true;
    this.rightPane.Size = new System.Drawing.Size(168, 477);
    this.rightPane.SplitterDistance = 209;
    this.rightPane.TabIndex = 3;
    // 
    // treeView
    // 
    this.treeView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
    this.treeView.HideSelection = false;
    this.treeView.Location = new System.Drawing.Point(2, 2);
    this.treeView.Name = "treeView";
    this.treeView.Size = new System.Drawing.Size(161, 470);
    this.treeView.TabIndex = 0;
    this.treeView.Visible = false;
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
    listViewGroup3.Header = "Object Templates";
    listViewGroup3.Name = "objectTemplateGroup";
    listViewGroup4.Header = "Vector Shapes";
    listViewGroup4.Name = "vectorShapeGroup";
    listViewGroup5.Header = "Miscellaneous";
    listViewGroup5.Name = "miscGroup";
    this.objectList.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5});
    this.objectList.LargeImageList = this.objectImgs;
    this.objectList.Location = new System.Drawing.Point(2, 24);
    this.objectList.MultiSelect = false;
    this.objectList.Name = "objectList";
    this.objectList.ShowItemToolTips = true;
    this.objectList.Size = new System.Drawing.Size(161, 448);
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
    this.propertyGrid.ContextMenuStrip = propertyGridMenu;
    this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
    this.propertyGrid.Location = new System.Drawing.Point(0, 0);
    this.propertyGrid.Name = "propertyGrid";
    this.propertyGrid.Size = new System.Drawing.Size(148, 44);
    this.propertyGrid.TabIndex = 0;
    this.propertyGrid.Visible = false;
    // 
    // propertyGridMenu
    // 
    propertyGridMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.resetPropertyValueMenuItem,
            this.openColorPickerMenuItem});
    propertyGridMenu.Name = "propertyGridMenu";
    propertyGridMenu.Size = new System.Drawing.Size(132, 26);
    propertyGridMenu.Opening += new System.ComponentModel.CancelEventHandler(this.propertyGridMenu_Opening);
    // 
    // resetPropertyValueMenuItem
    // 
    this.resetPropertyValueMenuItem.Name = "resetPropertyValueMenuItem";
    this.resetPropertyValueMenuItem.Size = new System.Drawing.Size(131, 22);
    this.resetPropertyValueMenuItem.Text = "Reset value";
    this.resetPropertyValueMenuItem.Click += new System.EventHandler(this.resetPropertyValueMenuItem_Click);
    // 
    // openColorPickerMenuItem
    // 
    this.openColorPickerMenuItem.Name = "openColorPickerMenuItem";
    this.openColorPickerMenuItem.Size = new System.Drawing.Size(131, 22);
    this.openColorPickerMenuItem.Text = "Open color picker...";
    this.openColorPickerMenuItem.Click += new System.EventHandler(this.openColorPickerMenuItem_Click);
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
    // vectorTreeMenu
    // 
    this.vectorTreeMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createVectorGroupMenuItem});
    this.vectorTreeMenu.Name = "vectorTreeMenu";
    this.vectorTreeMenu.Size = new System.Drawing.Size(139, 26);
    // 
    // createVectorGroupMenuItem
    // 
    this.createVectorGroupMenuItem.Name = "createVectorGroupMenuItem";
    this.createVectorGroupMenuItem.Size = new System.Drawing.Size(138, 22);
    this.createVectorGroupMenuItem.Text = "Create &group";
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
    propertyGridMenu.ResumeLayout(false);
    this.toolBar.ResumeLayout(false);
    this.toolBar.PerformLayout();
    this.vectorTreeMenu.ResumeLayout(false);
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

    public virtual bool CanCopy
    {
      get { return false; }
    }

    public virtual bool CanPaste
    {
      get { return false; }
    }

    public virtual void Activate() { }
    public virtual void Deactivate() { }

    public virtual bool Copy() { return false; }
    public virtual bool Paste() { return false; }

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

  // FIXME: invalidating an object doesn't invalidate and reposition its mounts
  #region ObjectTool
  sealed class ObjectTool : EditTool
  {
    public ObjectTool(SceneEditor editor) : base(editor)
    {
      freehandTool = new FreehandSubTool(editor, this);
      joinTool     = new JoinSubTool(editor, this);
      linksTool    = new LinksSubTool(editor, this);
      mountTool    = new MountSubTool(editor, this);
      spatialTool  = new SpatialSubTool(editor, this);
      vectorTool   = new VectorSubTool(editor, this);
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
   
    public FreehandSubTool FreehandTool
    {
      get { return freehandTool; }
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

    public SpatialSubTool SpatialTool
    {
      get { return spatialTool; }
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

    #region FreehandSubTool
    public sealed class FreehandSubTool : ObjectSubTool
    {
      public FreehandSubTool(SceneEditor editor, ObjectTool parent) : base(editor, parent) { }

      public override void Deactivate()
      {
        points.Clear();
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(e.KeyCode == Keys.Escape)
        {
          Panel.CancelMouseDrag();
          ObjectTool.SubTool = ObjectTool.VectorTool;
        }
      }

      public override bool MouseDragStart(MouseEventArgs e)
      {
        if(e.Button == MouseButtons.Left)
        {
          area = new Rectangle(e.X, e.Y, 1, 1);
          points.Clear();
          points.Add(e.Location);
          return true;
        }
        else
        {
          return false;
        }
      }

      public override void MouseDrag(MouseDragEventArgs e)
      {
        if(e.Offset.Width != 0 || e.Offset.Height != 0)
        {
          points.Add(e.Location);

          area = Rectangle.Union(area, new Rectangle(e.X, e.Y, 1, 1));
          Editor.InvalidateDecoration(area);
        }
      }

      public override void MouseDragEnd(MouseDragEventArgs e)
      {
        if(points.Count < 3)
        {
          MessageBox.Show("Not enough points were added to construct an actual polygon. Try drawing more!");
        }
        else
        {
          List<GLPoint> glPoints = new List<GLPoint>();
          foreach(Point pt in points)
          {
            glPoints.Add(SceneView.ClientToScene(pt));
          }

          // smooth the polygon by averaging each point with the surrounding two. this helps eliminate stairstepping
          // caused by the fact that mouse input does not have subpixel precision
          GLPoint prevPoint = glPoints[0];
          for(int i=1; i<glPoints.Count-1; i++)
          {
            GLPoint thisPoint = glPoints[i];
            glPoints[i] = new GLPoint((prevPoint.X+thisPoint.X+glPoints[i+1].X)/3,
                                      (prevPoint.Y+thisPoint.Y+glPoints[i+1].Y)/3);
            prevPoint = thisPoint;
          }
          glPoints[glPoints.Count-1] = new GLPoint((glPoints[glPoints.Count-1].X+glPoints[glPoints.Count-2].X) / 2,
                                                   (glPoints[glPoints.Count-1].Y+glPoints[glPoints.Count-2].Y) / 2);

          VectorShape shape = Editor.CreateLocalShape();

          VectorShape.PolygonNode polyNode = new VectorShape.PolygonNode("poly");
          shape.RootNode = polyNode;

          double x1=double.MaxValue, y1=double.MaxValue, x2=double.MinValue, y2=double.MinValue;
          foreach(GLPoint point in glPoints)
          {
            if(point.X < x1) x1 = point.X;
            if(point.Y < y1) y1 = point.Y;
            if(point.X > x2) x2 = point.X;
            if(point.Y > y2) y2 = point.Y;
          }
          double size = Math.Max(x2-x1, y2-y1);

          VectorObject obj = new VectorObject();
          obj.ShapeName = shape.Name;
          obj.Layer     = Editor.CurrentLayer;
          obj.Size      = new Vector(size, size);
          obj.Position  = new GLPoint(x1+(x2-x1)*0.5, y1+(y2-y1)*0.5);
          Scene.AddObject(obj);

          VectorShape.Vertex vertex = new VectorShape.Vertex();
          vertex.Color = ObjectTool.GetInverseBackgroundColor();
          vertex.Type  = VectorShape.VertexType.Split;
          foreach(GLPoint point in glPoints)
          {
            vertex = vertex.Clone();
            vertex.Position = obj.SceneToLocal(point);
            polyNode.Polygon.AddVertex(vertex);
          }
          
          polyNode.Polygon.MinimumLOD = polyNode.Polygon.MaximumLOD = 0.85f; // 0.85 is typically a bit closer to what the user will want

          ObjectTool.SelectObject(obj, true);
          ObjectTool.SubTool = ObjectTool.VectorTool;
          Editor.InvalidateRender();
        }
      }

      public override void PaintDecoration(Graphics g)
      {
        if(points.Count > 1)
        {
          using(Pen pen = new Pen(ObjectTool.GetInverseBackgroundColor()))
          {
            for(int i=1; i<points.Count; i++)
            {
              g.DrawLine(pen, points[i-1], points[i]);
            }
            g.DrawLine(pen, points[points.Count-1], points[0]);
          }
        }
      }

      List<Point> points = new List<Point>();
      Rectangle area;
    }
    #endregion

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

        VectorObject destObj = ObjectTool.ObjectUnderPoint(e.Location) as VectorObject;
        if(destObj == null) return false;
        
        VectorShape destShape = destObj.Shape;
        if(destShape == null) return false;

        VectorObject srcObj = (VectorObject)SelectedObjects[0];

        if(destObj == srcObj)
        {
          MessageBox.Show("You can't join an object with itself.", "Hmm...", MessageBoxButtons.OK,
                          MessageBoxIcon.Exclamation);
          return true;
        }

        if(destObj.Shape == srcObj.Shape)
        {
          MessageBox.Show("You can't join two instances of the same shape.", "Hmm...", MessageBoxButtons.OK,
                          MessageBoxIcon.Exclamation);
          return true;
        }

        // if the flipping of the objects differ, we'll have to reverse the polygon vertex order of one before
        // joining them.
        bool reversePolygons = VectorSubTool.PolygonsFlipped(srcObj) != VectorSubTool.PolygonsFlipped(destObj);

        VectorShape srcShape = CloneObject(srcObj.Shape);
        foreach(VectorShape.Polygon poly in VectorSubTool.GetPolygons(srcShape.RootNode))
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = destObj.SceneToLocal(srcObj.LocalToScene(vertex.Position));
          }
          
          if(reversePolygons)
          {
            poly.ReverseVertices();
          }
        }

        VectorShape.Node newNode = srcShape.RootNode;
        srcShape.RootNode = null;
        VectorSubTool.AddNodeToRoot(newNode, destShape);
        ObjectTool.DeleteObjectFromScene(srcObj);

        VectorSubTool.RecalculateObjectBounds(destObj);
        ObjectTool.SelectObject(destObj, true);
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
        ObjectTool.SubTool = ObjectTool.VectorTool;
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
        previousViewZoom   = SceneView.CameraZoom;

        previousRotation = Object.Rotation;
        Object.Rotation  = 0;

        linkPoints = Object.GetLinkPoints();
 
        SceneView.CameraPosition = Object.Position; // center the camera on the object
        SceneView.CameraZoom     = SceneView.CalculateZoom(Math.Max(Object.Width, Object.Height) * 1.10); // 10% bigger than the object's size
        Editor.InvalidateView();
      }

      public override void Deactivate()
      {
        Object.Rotation = previousRotation;

        SceneView.CameraPosition = previousViewCenter;
        SceneView.CameraZoom     = previousViewZoom;
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
                  ObjectTool.DeleteObjectFromScene(linkPoints[links[i]].Object);
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
      double  previousViewZoom, previousRotation;
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
          SceneView.CameraZoom = previousViewZoom;
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

          previousViewZoom = SceneView.CameraZoom;

          Vector objSize = parent.GetRotatedAreaBounds().Size;
          SceneView.CameraPosition = parent.Position;
          SceneView.CameraZoom     = SceneView.CalculateZoom(Math.Max(objSize.X, objSize.Y) * 1.50);
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
      
      double previousViewZoom;
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
          SelectObject(e.Location, true);
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
                menu.MenuItems.Add("Edit vector shape",
                                   delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.VectorTool; });
              }
              menu.MenuItems.Add("-");
            }

            menu.MenuItems.Add(new MenuItem("Delete object(s)", menu_Delete, Shortcut.Del));
            menu.MenuItems.Add("Export object(s)...", menu_Export);
            menu.MenuItems.Add("Copy object(s)", Editor.editCopyMenuItem_Click);
            
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
          menu.MenuItems.Add("New freehand shape",
                             delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.FreehandTool; });

          if(Editor.CurrentTool.CanPaste)
          {
            menu.MenuItems.Add("Paste", Editor.editPasteMenuItem_Click);
          }

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

        // if the mouse cursor is not over a control handle, or the selected objects themselves, 
        // select the object under the pointer
        if(dragHandle == Handle.None && !ObjectTool.selectedObjectBounds.Contains(e.Location))
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

      void DeleteSelectedObjects()
      {
        ObjectTool.InvalidateSelectedBounds(true);
        foreach(SceneObject obj in SelectedObjects)
        {
          ObjectTool.DeleteObjectFromScene(obj);
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
        return ObjectTool.GetHandleRect(GetHandleBorder(), handle);
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
        // warn the user about exporting objects that reference local resources
        foreach(SceneObject obj in SelectedObjects)
        {
          VectorObject vectorObj = obj as VectorObject;
          if(vectorObj != null && Editor.IsLocalResource(vectorObj.ShapeName))
          {
            
            if(MessageBox.Show("One or more selected objects reference resources local to this level. The resulting "+
                               "object template will not be importable into other levels. If this is not what you "+
                               "want, go back and export the resources from the level so that they can be shared. "+
                               "Then export the objects referencing the resources. Do you want to create a "+
                               "non-sharable object template?", "Create a non-sharable template??",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                 == DialogResult.No)
            {
              return;
            }

            break;
          }
        }

        SaveFileDialog fd = new SaveFileDialog();
        fd.FileName = "object 1.object";
        fd.Filter   = "Object files (*.object)|*.object";
        fd.Title    = "Select the path to which the objects will be saved";
        fd.InitialDirectory = Editor.Project.ObjectsPath.TrimEnd('/');
        
        if(fd.ShowDialog() == DialogResult.OK)
        {
          using(SexpWriter writer = new SexpWriter(File.Open(fd.FileName, FileMode.Create, FileAccess.Write)))
          {
            Serializer.BeginBatch();
            foreach(SceneObject obj in SelectedObjects)
            {
              Serializer.Serialize(obj, writer);
            }
            Serializer.EndBatch();
          }
          
          Editor.SetToolboxItem(new ObjectTemplateItem(fd.FileName));
        }
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

      public override bool CanCopy
      {
        get { return SelectedNode != null; }
      }

      public override void Activate()
      {
        Editor.propertyGrid.PropertyValueChanged += propertyGrid_PropertyValueChanged;

        Editor.treeView.AfterSelect     += treeView_AfterSelect;
        Editor.treeView.AfterLabelEdit  += treeView_AfterLabelEdit;
        Editor.treeView.DragDrop        += treeView_DragDrop;
        Editor.treeView.DragEnter       += treeView_DragEnter;
        Editor.treeView.DragOver        += treeView_DragOver;
        Editor.treeView.ItemDrag        += treeView_ItemDrag;
        Editor.treeView.KeyDown         += treeView_KeyDown;
        Editor.treeView.MouseDown       += treeView_MouseDown;
        Editor.treeView.AllowDrop = true;
        Editor.treeView.LabelEdit = true;
        Editor.treeView.ContextMenuStrip = Editor.vectorTreeMenu;
        Editor.createVectorGroupMenuItem.Click += createVectorGroupMenuItem_Click;
        
        VectorObject selectedObject = null;
        foreach(SceneObject obj in ObjectTool.selectedObjects)
        {
          if(IsValidObject(obj))
          {
            selectedObject = obj as VectorObject;
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

      void treeView_MouseDown(object sender, MouseEventArgs e)
      {
        // save the node that the user right-clicked on, so that we can tailor the pop-up menu if necessary
        if(e.Button == MouseButtons.Right)
        {
          rightClickNode = Editor.treeView.GetNodeAt(e.X, e.Y);
        }
      }

      public override void Deactivate()
      {
        Editor.treeView.AfterSelect     -= treeView_AfterSelect;
        Editor.treeView.AfterLabelEdit  -= treeView_AfterLabelEdit;
        Editor.treeView.DragDrop        -= treeView_DragDrop;
        Editor.treeView.DragEnter       -= treeView_DragEnter;
        Editor.treeView.DragOver        -= treeView_DragOver;
        Editor.treeView.ItemDrag        -= treeView_ItemDrag;
        Editor.treeView.KeyDown         -= treeView_KeyDown;
        Editor.treeView.MouseDown       -= treeView_MouseDown;
        Editor.treeView.AllowDrop = false;
        Editor.treeView.LabelEdit = false;
        Editor.treeView.ContextMenuStrip = null;
        Editor.createVectorGroupMenuItem.Click -= createVectorGroupMenuItem_Click;

        Editor.propertyGrid.PropertyValueChanged -= propertyGrid_PropertyValueChanged;
        Editor.HideTreeView();
      }

      public override bool Copy()
      {
        if(SelectedNode == null) return false;

        VectorShape.Node clone = CloneObject(SelectedNode);
        bool reverseOrder = PolygonsFlipped(SelectedObject);

        // vertices are stored on the clipboard in scene coordinates
        foreach(VectorShape.Polygon poly in GetPolygons(clone))
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = SelectedObject.LocalToScene(vertex.Position);
          }
          if(reverseOrder) // we want polygons to be stored in a clockwise fashion on the clipboard
          {
            poly.ReverseVertices();
          }
        }

        using(MemoryStream stream = new MemoryStream())
        {
          Serializer.BeginBatch();
          Serializer.Serialize(clone, stream);
          Serializer.EndBatch();
          EditorApp.Clipboard = new ClipboardObject(ObjectType.Node, stream.ToArray());
        }

        return true;
      }

      public override void KeyPress(KeyEventArgs e, bool down)
      {
        if(!down) return;

        char c = (char)e.KeyValue;
        if(e.KeyCode == Keys.Delete)
        {
          DeleteSelection();
          e.Handled = true;
        }
        else if(SelectedObject != null && e.Modifiers == Keys.Control && // keyboard navigation of shape hierarchy
                (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
        {
          TreeNode newNode = null;
          if(e.KeyCode == Keys.Left) // select the parent node of what's currently selected
          {
            newNode = Editor.treeView.SelectedNode.Parent;
          }
          else if(e.KeyCode == Keys.Right) // descend into child nodes (or the next sibling)
          {
            Editor.treeView.SelectedNode.Expand();
            newNode = Editor.treeView.SelectedNode.NextVisibleNode;
          }
          else if(e.KeyCode == Keys.Up) // previous sibling
          {
            newNode = Editor.treeView.SelectedNode.PrevNode;
            if(newNode == null) newNode = Editor.treeView.SelectedNode.PrevVisibleNode;
          }
          else if(e.KeyCode == Keys.Down) // next sibling
          {
            newNode = Editor.treeView.SelectedNode.NextNode;
            if(newNode == null) newNode = Editor.treeView.SelectedNode.NextVisibleNode;
          }

          if(newNode != null)
          {
            Editor.treeView.SelectedNode = newNode;
          }

          e.Handled = true;
        }
        else if(Control.ModifierKeys == Keys.None && e.KeyCode == Keys.F2 && SelectedNode != null) // F2 edits the currently-selected node
        {
          Cursor.Position = Editor.treeView.PointToScreen(new Point(5, 5));
          Editor.treeView.Focus();
          Editor.treeView.SelectedNode.BeginEdit();
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
            if(selectedPoints.Count == 0)
            {
              SelectObjectAndVertex(e.Location);
            }
            else
            {
              ToggleVertexSelection(e.Location);
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
              
              if(PolygonsFlipped(SelectedObject)) // select points in a counterclockwise fashion
              {
                if(endPoint <= startPoint)
                {
                  for(int i=endPoint; i>=startPoint; i--)
                  {
                    SelectVertex(i, false);
                  }
                }
                else
                {
                  for(int i=startPoint; i>=0; i--)
                  {
                    SelectVertex(i, false);
                  }
                  for(int i=SelectedPolygon.Vertices.Count-1; i>=endPoint; i--)
                  {
                    SelectVertex(i, false);
                  }
                }
              }
              else // select points in a clockwise fashion
              {
                if(startPoint <= endPoint)
                {
                  for(int i=startPoint; i<=endPoint; i++)
                  {
                    SelectVertex(i, false);
                  }
                }
                else
                {
                  for(int i=startPoint; i<SelectedPolygon.Vertices.Count; i++)
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
          }
          // ctrl-click breaks/joins the shape at that point
          else if(Control.ModifierKeys == Keys.Control)
          {
            if(SelectObjectAndVertex(e.Location) && selectedPoints.Count == 1)
            {
              VectorShape.Vertex vertex = SelectedPolygon.Vertices[selectedPoints[0]];
              vertex.Type = vertex.Type == VectorShape.VertexType.Normal ?
                VectorShape.VertexType.Split : VectorShape.VertexType.Normal;
              ObjectTool.InvalidateSelectedBounds(true);
            }
          }
        }
        else if(e.Button == MouseButtons.Middle) // middle click swaps between object/vector mode
        {
          SelectSingleVertex(e.Location);
          ObjectTool.SubTool = ObjectTool.SpatialTool;
        }
        else if(e.Button == MouseButtons.Right)
        {
          SelectObjectIfOutsideSelectedPolygon(e.Location);
          ContextMenu menu = new ContextMenu();

          if(SelectedObject != null)
          {
            if(SelectedPolygon != null)
            {
              if(selectedPoints.Count != SelectedPolygon.Vertices.Count)
              {
                menu.MenuItems.Add("Select all vertices", delegate(object s, EventArgs a) { SelectAllPoints(); });
              }

              if(selectedPoints.Count != 0) // operations upon the selected vertices
              {
                if(selectedPoints.Count != SelectedPolygon.Vertices.Count)
                {
                  menu.MenuItems.Add("Delete selected vertices",
                                     delegate(object s, EventArgs a) { DeleteSelectedPoints(); });
                  menu.MenuItems.Add("-");
                }
              }

              menu.MenuItems.Add("Copy selected polygon", Editor.editCopyMenuItem_Click);

              menu.MenuItems.Add("Convert polygon to pre-subdivided polygon", menu_ConvertPolyToVertexPoly);
            }

            // if it has no animations, it can be joined with another vector object
            if(SelectedShape.Animations.Count == 0)
            {
              menu.MenuItems.Add("Join with another object",
                                 delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.JoinTool; });
            }

            List<VectorShape.Polygon> polygons = GetPolygons();
            // if it has multiple polygons, the order of the current polygon can be changed
            if(polygons.Count > 1)
            {
              menu.MenuItems.Add("Convert shape to pre-subdivided shape", menu_ConvertShapeToVertexShape);

              if(SelectedPolygon != null)
              {
                menu.MenuItems.Add("Delete selected polygon",
                                   delegate(object s, EventArgs a) { DeleteSelectedNode(); });
              }
            }

            menu.MenuItems.Add("Delete shape", delegate(object s, EventArgs a) { DeleteSelectedObject(); });
            menu.MenuItems.Add("Edit object properties",
                               delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.SpatialTool; });
            menu.MenuItems.Add("Reduce object to minimum size (affects rendering)", menu_ReduceObjectSize);

            if(Editor.IsLocalResource(SelectedShape.Name))
            {
              menu.MenuItems.Add("Export shape...", delegate(object s, EventArgs a) { ExportLocalShape(); });
            }

            menu.MenuItems.Add("-");
          }

          menu.MenuItems.Add("New vertex shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, true); });
          menu.MenuItems.Add("New spline shape",
                             delegate(object s, EventArgs a) { ObjectTool.CreateShape(e.Location, false); });
          menu.MenuItems.Add("New freehand shape",
                             delegate(object s, EventArgs a) { ObjectTool.SubTool = ObjectTool.FreehandTool; });

          if(Editor.CurrentTool.CanPaste)
          {
            menu.MenuItems.Add("Paste", Editor.editPasteMenuItem_Click);
          }

          menu.Show(Panel, e.Location);
          return true;
        }

        return false;
      }
      
      void SelectSingleVertex(Point pt)
      {
        if(!SelectObjectAndVertex(pt))
        {
          DeselectObject();
        }
      }

      public override bool MouseDragStart(MouseEventArgs e)
      {
        if(e.Button == MouseButtons.Left) // for a left drag, move the selected vertex/polygon/object
        {
          // if a group is selected, we'll only select a new object if the cursor is outside the group bounds
          if(SelectedNode is VectorShape.GroupNode)
          {
            if(!SelectObjectIfOutsideNodeRect(e.Location)) return false;
          }
          // if multiple points are selected, only deselect if the cursor is not on one of the handles
          else if(selectedPoints.Count > 1)
          {
            if(!SelectObjectIfOutsideSelectedPointHandles(e.Location)) return false;
          }
          else // otherwise, select the object and vertex beneath the cursor
          {
            if(!SelectObjectAndVertex(e.Location)) return false;
          }

          dragPoints.Clear();

          // if no points (or all points) are selected, move the whole node or object
          if(selectedPoints.Count == 0 ||
             SelectedPolygon != null && selectedPoints.Count == SelectedPolygon.Vertices.Count)
          {
            if(SelectedNode == SelectedShape.RootNode) // if the root node is selected, drag the whole object
            {
              dragPoints.Add(SelectedObject.Position);
              dragMode = DragMode.MoveObject;
            }
            else // otherwise, just drag the selected node
            {
              foreach(VectorShape.Polygon poly in SelectedPolygons)
              {
                foreach(VectorShape.Vertex vertex in poly.Vertices)
                {
                  dragPoints.Add(SelectedObject.LocalToScene(vertex.Position));
                }
              }
              dragMode = DragMode.MoveNode;
            }
          }
          else // otherwise, just drag the selected points
          {
            foreach(int selectedPoint in selectedPoints)
            {
              dragPoints.Add(SelectedObject.LocalToScene(SelectedPolygon.Vertices[selectedPoint].Position));
            }
            dragMode = DragMode.MoveVertices;
          }
          
          return true;
        }
        else if(e.Button == MouseButtons.Right) // for right drags, clone and move vertices
        {
          // if there wasn't a point under the mouse, return false
          if(!SelectObjectAndVertex(e.Location) || selectedPoints.Count != 1) return false;
          CloneAndSelectPoint(selectedPoints[0]);
          dragPoints.Clear();
          dragPoints.Add(SelectedObject.LocalToScene(SelectedPolygon.Vertices[selectedPoints[0]].Position));
          dragMode = DragMode.MoveVertices;
          return true;
        }
        else if(e.Button == MouseButtons.Middle) // for middle drags, manipulate the texture
        {
          // if there wasn't a polygun under the mouse, or the polygon has no texture, or it doesn't define a valid
          // area, cancel the drag
          if(!SelectObjectAndVertex(e.Location) || string.IsNullOrEmpty(SelectedPolygon.Texture) ||
             SelectedPolygon.Vertices.Count < 3)
          {
            return false;
          }

          // get the size of the polygon in scene coordinates, so we can scaling the texture movement by the mouse
          // movement.
          GLRect polyBounds   = SelectedObject.LocalToScene(SelectedPolygon.GetBounds());
          GLPoint sceneCenter = EngineMath.GetCenterPoint(polyBounds);
          dragSize = Math.Max(polyBounds.Width, polyBounds.Height);

          if(Control.ModifierKeys == Keys.None)
          {
            dragVector = SelectedPolygon.TextureOffset;
            dragMode = DragMode.MoveTexture;
          }
          else if(Control.ModifierKeys == Keys.Control)
          {
            dragVector = new Vector(sceneCenter);
            dragRotation = Math2D.AngleBetween(sceneCenter, SceneView.ClientToScene(e.Location)) -
                           SelectedPolygon.TextureRotation * MathConst.DegreesToRadians;
            dragMode = DragMode.RotateTexture;
          }
          else if(Control.ModifierKeys == Keys.Shift)
          {
            dragVector = new Vector(sceneCenter);
            dragZoom = SelectedPolygon.TextureRepeat;
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
        switch(dragMode)
        {
          case DragMode.MoveVertices:   DragVertices(e); break;
          case DragMode.MoveNode:       DragNode(e); break;
          case DragMode.MoveObject:     DragObject(e); break;
          case DragMode.MoveTexture:    DragTexture(e); break;
          case DragMode.RotateTexture:  RotateTexture(e); break;
          case DragMode.ZoomTexture:    ZoomTexture(e); break;
        }
      }

      public override void MouseDragEnd(MouseDragEventArgs e)
      {
        // if we were moving the whole object, invalidate the render (to update mounted objects)
        if(dragMode == DragMode.MoveObject)
        {
          Editor.InvalidateRender();
        }
        // if we moved just part of the shape, recalculate the object bounds
        else if(dragMode == DragMode.MoveNode || dragMode == DragMode.MoveVertices)
        {
          ObjectTool.InvalidateSelectedBounds(true);
          RecalculateObjectBounds();
        }
      }

      void DragTexture(MouseDragEventArgs e)
      {
        Vector sceneDist = SceneView.ClientToScene(new Size(e.X-e.Start.X, e.Y-e.Start.Y));
        SelectedPolygon.TextureOffset = dragVector + sceneDist/dragSize;
        ObjectTool.InvalidateSelectedBounds(true);
      }
      
      void RotateTexture(MouseDragEventArgs e)
      {
        double rotation = Math2D.AngleBetween(dragVector.ToPoint(), SceneView.ClientToScene(e.Location)) - dragRotation;
        SelectedPolygon.TextureRotation = EngineMath.NormalizeAngle(rotation * MathConst.RadiansToDegrees);
        ObjectTool.InvalidateSelectedBounds(true);
      }
      
      void ZoomTexture(MouseDragEventArgs e)
      {
        GLPoint currentPoint = SceneView.ClientToScene(e.Location), startPoint = SceneView.ClientToScene(e.Start);
        double currentDist = currentPoint.DistanceTo(dragVector.ToPoint()),
                 startDist = startPoint.DistanceTo(dragVector.ToPoint());
        if(currentDist < 0.00001) return; // prevent divide by zero if the user moves the mouse close to the center
        SelectedPolygon.TextureRepeat = dragZoom * (startDist / currentDist);
        ObjectTool.InvalidateSelectedBounds(true);
      }

      void DragObject(MouseDragEventArgs e)
      {
        ObjectTool.InvalidateSelectedBounds(true);
        SelectedObject.Position = dragPoints[0] + SceneView.ClientToScene(GetClientDist(e));
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      void DragNode(MouseDragEventArgs e)
      {
        ObjectTool.InvalidateSelectedBounds(true);
        Vector sceneDist = SceneView.ClientToScene(GetClientDist(e));
        int pointIndex = 0;
        foreach(VectorShape.Polygon poly in SelectedPolygons)
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = SelectedObject.SceneToLocal(dragPoints[pointIndex++] + sceneDist);
          }
        }
        RecalculateObjectBounds();
      }

      void DragVertices(MouseDragEventArgs e)
      {
        ObjectTool.InvalidateSelectedBounds(true);

        Vector sceneDist = SceneView.ClientToScene(GetClientDist(e));
        for(int i=0; i<selectedPoints.Count; i++)
        {
          SelectedPolygon.Vertices[selectedPoints[i]].Position =
            SelectedObject.SceneToLocal(dragPoints[i] + sceneDist);
        }
        RecalculateObjectBounds();
      }

      void ExportLocalShape()
      {
        string badChars = null;
        char[] invalidChars = null;

        List<char> charList = new List<char>();
        foreach(char c in Path.GetInvalidFileNameChars())
        {
          // directory separators are allowed in shape names
          if(c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
          {
            charList.Add(c);
            if(c > 32) // control characters will just ugly up the string, so we'll skip them. (and space)
            {
              if(badChars != null) badChars += ' ';
              badChars += c;
            }
          }
        }
        invalidChars = charList.ToArray();

        StringDialog sd = new StringDialog("Enter shape name", "Enter the new name of the shape. The name must not "+
                                           "begin with an underscore, or contain the following characters: "+badChars);
        sd.Validating += delegate(object s, CancelEventArgs e)
        {
          string value = sd.Value.Trim();
          if(value == string.Empty || value.StartsWith("_") || value.IndexOfAny(invalidChars) != -1)
          {
            MessageBox.Show(sd.Prompt, sd.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            e.Cancel = true;
          }
          if(Engine.Engine.HasResource<VectorShape>(value))
          {
            MessageBox.Show("A shape with this name already exists.", sd.Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            e.Cancel = true;
          }
        };

        if(sd.ShowDialog() == DialogResult.OK)
        {
          string oldName = SelectedShape.Name;

          SelectedShape.Name = sd.Value.Trim(); // rename the shape
          foreach(SceneObject obj in Scene.PickAll()) // and update all VectorObjects that reference it
          {
            VectorObject vectorObj = obj as VectorObject;
            if(vectorObj != null && vectorObj.ShapeName == oldName)
            {
              vectorObj.ShapeName = SelectedShape.Name;
            }
          }

          // remove the name from the local resources list, since it's now a shared resource
          Editor.localResources.Remove(oldName);
          // save the shape to disk
          Editor.SaveSharedShape(SelectedShape);
          // and add the new item to the toolbox
          Editor.SetToolboxItem(new VectorShapeItem(SelectedShape));
        }
      }

      void RecalculateObjectBounds()
      {
        RecalculateObjectBounds(SelectedObject);
        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      internal static void AddNodeToRoot(VectorShape.Node srcNode, VectorShape destShape)
      {
        VectorShape.GroupNode group = destShape.RootNode as VectorShape.GroupNode;
        if(group == null)
        {
          VectorShape.Node root = destShape.RootNode;
          group = new VectorShape.GroupNode("group");
          destShape.UniquifyNames(group);

          destShape.RootNode = null;
          group.AddChild(root);
          destShape.RootNode = group;
        }
        
        destShape.UniquifyNames(srcNode);
        group.AddChild(srcNode);
      }

      internal static void RecalculateObjectBounds(VectorObject obj)
      {
        VectorShape shape = (VectorShape)obj.Shape;
        List<VectorShape.Polygon> polygons = GetPolygons(shape.RootNode);

        // convert the points to scene space
        foreach(VectorShape.Polygon poly in polygons)
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = obj.LocalToScene(vertex.Position);
          }
        }

        // get their scene-space bounding box
        GLRect rect = shape.RootNode.GetBounds();

        // if the object is square, we'll preserve that quality, so that texturing looks natural, etc.
        if(obj.Width == obj.Height)
        {
          // find the longest axis. the new data will be centered within the object.
          double sceneSize = Math.Max(rect.Width, rect.Height);
          obj.Size = new Vector(sceneSize, sceneSize);
        }
        else // otherwise, if the user has made the object non-square, we'll preserve that quality.
        {
          obj.Size = rect.Size;
        }

        obj.Position = EngineMath.GetCenterPoint(rect);

        // then convert back to local space in the new object
        foreach(VectorShape.Polygon poly in GetPolygons(shape.RootNode))
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = obj.SceneToLocal(vertex.Position);
          }
        }
      }

      public override void PaintDecoration(Graphics g)
      {
        if(SelectedPolygon != null)
        {
          VectorShape.Polygon poly = SelectedPolygon;
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
        else if(SelectedObject != null)
        {
          Rectangle rect = GetSelectedNodeClientBounds();

          using(Pen pen = new Pen(ObjectTool.GetInverseBackgroundColor()))
          {
            g.DrawRectangle(pen, rect);

            foreach(Handle handle in
                    new Handle[] { Handle.TopLeft, Handle.TopRight, Handle.BottomRight, Handle.BottomLeft })
            {
              Rectangle handleRect = GetHandleRect(rect, handle);
              g.FillRectangle(Brushes.Blue, handleRect);
              g.DrawRectangle(pen, handleRect);
            }
          }
        }
      }
      
      enum DragMode
      {
        MoveVertices, MoveNode, MoveObject,
        MoveTexture, RotateTexture, ZoomTexture
      }

      VectorShape SelectedShape
      {
        get { return SelectedObject == null ? null : SelectedObject.Shape; }
      }

      VectorShape.Node SelectedNode
      {
        get
        {
          if(SelectedObject == null) return null;
          TreeNode treeNode = Editor.treeView.SelectedNode;
          return treeNode == null ? SelectedShape.RootNode : SelectedShape.GetNode(treeNode.Name);
        }
      }

      VectorObject SelectedObject
      {
        get { return (VectorObject)ObjectTool.SelectedObject; }
      }

      VectorShape.PolygonNode SelectedPolyNode
      {
        get { return SelectedNode as VectorShape.PolygonNode; }
      }
      
      VectorShape.Polygon SelectedPolygon
      {
        get
        {
          VectorShape.PolygonNode polyNode = SelectedPolyNode;
          return polyNode == null ? null : polyNode.Polygon;
        }
      }
      
      List<VectorShape.PolygonNode> SelectedPolyNodes
      {
        get { return GetPolygonNodes(SelectedNode); }
      }

      List<VectorShape.Polygon> SelectedPolygons
      {
        get { return GetPolygons(SelectedNode); }
      }

      void CloneAndSelectPoint(int vertexIndex)
      {
        SelectedPolygon.InsertVertex(vertexIndex, SelectedPolygon.Vertices[vertexIndex].Clone());
        SelectVertex(vertexIndex + (PolygonsFlipped(SelectedObject) ? 0 : 1), true);
      }

      void DeleteSelection()
      {
        if(selectedPoints.Count != 0)
        {
          DeleteSelectedPoints();
        }
        else if(SelectedObject != null)
        {
          DeleteSelectedNode();
        }
      }

      void DeleteSelectedPoints()
      {
        if(selectedPoints.Count != 0)
        {
          // sort the points and remove from highest index to lowest, so that the indices of the points don't shift
          // as each one is deleted.
          selectedPoints.Sort();
          for(int i=selectedPoints.Count-1; i>=0; i--)
          {
            SelectedPolygon.RemoveVertex(selectedPoints[i]);
          }
          selectedPoints.Clear();

          if(SelectedPolygon.Vertices.Count == 0) // if there are no vertices left, delete the polygon node
          {
            DeleteSelectedNode();
          }
          else
          {
            OnSelectionChanged();
            OnDeleted();
          }
        }
      }

      void DeleteSelectedNode()
      {
        VectorShape.Node node = SelectedNode;
        SelectPolygon(null);

        SelectedShape.RemoveNode(node); // remove the selected node

        List<VectorShape.PolygonNode> polygons = GetPolygonNodes();
        if(polygons.Count == 0) // if there are no polygons left after removing the node, delete the whole object
        {
          DeleteSelectedObject();
        }
        else // otherwise, repopulate the tree with what's left and select the first polygon in the tree
        {
          RepopulateTreeView();
          SelectPolygon(polygons[0].Name);
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
          ObjectTool.DeleteObjectFromScene(obj);
        }
      }

      void DeselectObject()
      {
        if(SelectedObject != null)
        {
          DeselectPoints();
          ObjectTool.DeselectObjects();
          SelectPolygon(null);
          OnSelectionChanged();
          Editor.HideTreeView();
        }
      }

      void DeselectPoints()
      {
        selectedPoints.Clear();
        OnSelectionChanged();
      }

      Rectangle GetSelectedNodeClientBounds()
      {
        Rectangle rect = SceneView.SceneToClient(SelectedObject.LocalToScene(SelectedNode.GetBounds()));
        rect.Inflate(1, 1);
        return rect;
      }

      List<VectorShape.Polygon> GetPolygons()
      {
        return GetPolygons(SelectedShape.RootNode);
      }

      List<VectorShape.PolygonNode> GetPolygonNodes()
      {
        return GetPolygonNodes(SelectedShape.RootNode);
      }

      internal static List<VectorShape.Polygon> GetPolygons(VectorShape.Node tree)
      {
        List<VectorShape.PolygonNode> polyNodes = GetPolygonNodes(tree);
        List<VectorShape.Polygon> polygons = new List<VectorShape.Polygon>(polyNodes.Count);
        foreach(VectorShape.PolygonNode polyNode in polyNodes)
        {
          polygons.Add(polyNode.Polygon);
        }
        return polygons;
      }

      internal static List<VectorShape.PolygonNode> GetPolygonNodes(VectorShape.Node tree)
      {
        List<VectorShape.PolygonNode> nodes = new List<VectorShape.PolygonNode>();

        if(tree != null)
        {
          foreach(VectorShape.Node node in VectorShape.EnumerateNodes(tree))
          {
            VectorShape.PolygonNode polyNode = node as VectorShape.PolygonNode;
            if(polyNode != null) nodes.Add(polyNode);
          }
        }

        return nodes;
      }

      public static bool IsValidObject(SceneObject obj)
      {
        VectorObject vectorObj = obj as VectorObject;
        if(vectorObj != null)
        {
          VectorShape shape = vectorObj.Shape;
          if(shape == null || shape.RootNode == null)
          {
            return false;
          }

          return true;
        }

        return false;
      }

      void SelectAllPoints()
      {
        if(selectedPoints.Count != SelectedPolygon.Vertices.Count)
        {
          selectedPoints.Clear();
          for(int i=0; i<SelectedPolygon.Vertices.Count; i++)
          {
            selectedPoints.Add(i);
          }
          OnSelectionChanged();
        }
      }

      // if the mouse cursor is inside the selected node bounds, returns true.
      // otherwise, calls SelectObjectAndVertex to select an object.
      bool SelectObjectIfOutsideNodeRect(Point pt)
      {
        if(SelectedNode != null && GetSelectedNodeClientBounds().Contains(pt))
        {
          return true;
        }
        else
        {
          return SelectObjectAndVertex(pt);
        }
      }
      
      // if the mouse cursor is over one of the selected point handles, returns true.
      // otherwise, calls SelectObjectAndVertex to select an object.
      bool SelectObjectIfOutsideSelectedPointHandles(Point pt)
      {
        if(PointUnderCursor(pt) != -1)
        {
          return true;
        }
        else
        {
          return SelectObjectAndVertex(pt);
        }
      }

      bool SelectObjectIfOutsideSelectedPolygon(Point pt)
      {
        if(SelectedPolygon == null)
        {
          return SelectObjectIfOutsideNodeRect(pt);
        }
        else if(PolygonContains(SelectedPolygon, SelectedObject.SceneToLocal(SceneView.ClientToScene(pt))))
        {
          return true;
        }
        else
        {
          return SelectObjectAndVertex(pt);
        }
      }

      // if the mouse is over a vertex of the selected polygon (if any), selects that vertex and deselects others.
      // otherwise, if the mouse is over any polygon of any vector object, the object and polygon are selected. if the
      // mouse is over any vertex of that polygon, the vertex is selected.
      // returns true if a polygon is selected and false otherwise.
      bool SelectObjectAndVertex(Point pt)
      {
        if(SelectedPolygon != null)
        {
          int point = PointUnderCursor(pt);
          if(point != -1)
          {
            SelectVertex(point, true);
            return true;
          }
        }

        foreach(SceneObject obj in Scene.PickPoint(SceneView.ClientToScene(pt), Editor.GetPickerOptions()))
        {
          if(IsValidObject(obj) && TrySelectObjectAndPoint((VectorObject)obj, pt))
          {
            return true;
          }
        }

        DeselectPoints();
        return false;
      }
      
      /// <summary>Toggles the selection of the vertex of the selected polygon under the mouse point.</summary>
      void ToggleVertexSelection(Point pt)
      {
        int point = PointUnderCursor(pt);
        if(point != -1)
        {
          if(selectedPoints.Contains(point))
          {
            selectedPoints.Remove(point);
          }
          else
          {
            SelectVertex(point, false);
          }
        }
      }

      int PointUnderCursor(Point pt)
      {
        if(SelectedPolyNode == null) return -1;
        return PointUnderCursor(SelectedObject, SelectedPolyNode.Name, pt);
      }
      
      int PointUnderCursor(VectorObject obj, string polyName, Point pt)
      {
        VectorShape.Polygon poly = ((VectorShape.PolygonNode)obj.Shape.GetNode(polyName)).Polygon;
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

      bool SelectPolygonAndPoint(VectorObject obj, string polyName, Point pt)
      {
        int pointIndex = PointUnderCursor(obj, polyName, pt);
        if(pointIndex != -1)
        {
          SelectObject(obj);
          SelectPolygon(polyName);
          SelectVertex(pointIndex, true);
          return true;
        }

        DeselectPoints();
        return false;
      }

      void SelectObject(VectorObject obj)
      {
        if(obj != SelectedObject)
        {
          ObjectTool.SelectObject(obj, true);
          ObjectTool.InvalidateSelectedBounds(false);
          Editor.ShowTreeView();
          RepopulateTreeView();
          SelectPolygon(GetPolygonNodes()[0].Name);
        }
      }

      void SelectPolygon(string polyName)
      {
        if(SelectedPolyNode != null && polyName != SelectedPolyNode.Name ||
           SelectedPolyNode == null && polyName != null)
        {
          DeselectPoints();

          if(!string.IsNullOrEmpty(polyName) && SelectedShape != null)
          {
            TreeNode[] nodes = Editor.treeView.Nodes.Find(polyName, true);
            if(nodes.Length != 0)
            {
              Editor.treeView.SelectedNode = nodes[0];
            }
          }
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

      bool TrySelectObjectAndPoint(VectorObject obj, Point pt)
      {
        GLPoint localPoint = obj.SceneToLocal(SceneView.ClientToScene(pt));

        // scan the polygons from the top down
        List<VectorShape.PolygonNode> polygons = GetPolygonNodes(obj.Shape.RootNode);
        for(int polyIndex=polygons.Count-1; polyIndex >= 0; polyIndex--)
        {
          string polyName = polygons[polyIndex].Name;
          // if the polygon contains the point, select it and then 
          if(PolygonContains(polygons[polyIndex].Polygon, localPoint))
          {
            SelectObject(obj);
            SelectPolygon(polyName);
            SelectPolygonAndPoint(obj, polyName, pt);
            return true;
          }
          else if(SelectPolygonAndPoint(obj, polyName, pt))
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
        rightClickNode = null;
        UpdatePropertyGrid();
        Editor.InvalidateDecoration();
      }

      void RepopulateTreeView()
      {
        Editor.treeView.BeginUpdate();

        Editor.treeView.Nodes.Clear();
        if(SelectedShape != null && SelectedShape.RootNode != null)
        {
          RepopulateTreeView(null, SelectedShape.RootNode);
        }
        Editor.treeView.ExpandAll();

        Editor.treeView.EndUpdate();
      }
      
      void RepopulateTreeView(TreeNode parentNode, VectorShape.Node node)
      {
        TreeNode treeNode = new TreeNode();
        treeNode.Name = node.Name;
        treeNode.Text = (node is VectorShape.GroupNode ? "group:" : "poly:") + node.Name;

        if(parentNode == null)
        {
          Editor.treeView.Nodes.Add(treeNode);
        }
        else
        {
          parentNode.Nodes.Add(treeNode);
        }

        foreach(VectorShape.Node child in node.Children)
        {
          RepopulateTreeView(treeNode, child);
        }
      }

      void UpdatePropertyGrid()
      {
        if((SelectedNode as VectorShape.PolygonNode) == null)
        {
          Editor.HideRightPane();
          Editor.propertyGrid.SelectedObjects = null;
        }
        else
        {
          if(selectedPoints.Count == 0)
          {
            Editor.propertyGrid.SelectedObject = SelectedPolygon;
          }
          else
          {
            object[] vertices = new object[selectedPoints.Count];
            for(int i=0; i<vertices.Length; i++)
            {
              vertices[i] = SelectedPolygon.Vertices[selectedPoints[i]];
            }
            Editor.propertyGrid.SelectedObjects = vertices;
          }

          Editor.ShowPropertyGrid();
        }
      }

      void menu_ConvertPolyToVertexPoly(object sender, EventArgs e)
      {
        DeselectPoints();
        ConvertPolyToVertexPoly(SelectedPolygon);
      }

      void menu_ConvertShapeToVertexShape(object sender, EventArgs e)
      {
        DeselectPoints();
        foreach(VectorShape.Polygon poly in GetPolygons())
        {
          ConvertPolyToVertexPoly(poly);
        }
      }
      
      void menu_ReduceObjectSize(object sender, EventArgs e)
      {
        if(MessageBox.Show("This operation will modify all instances of this shape, and break automatic texture "+
                           "coordinate generation and LOD. It is only intended for use as an optimization. Are you "+
                           "sure you want to do it?", "r u sure?????", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                           MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
          return;
        }

        foreach(VectorShape.Polygon poly in GetPolygons())
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = SelectedObject.LocalToScene(vertex.Position);
          }
        }

        GLRect bounds = SelectedShape.RootNode.GetBounds();
        SelectedObject.Size     = bounds.Size;
        SelectedObject.Position = EngineMath.GetCenterPoint(bounds);

        foreach(VectorShape.Polygon poly in GetPolygons())
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = SelectedObject.SceneToLocal(vertex.Position);
          }
        }

        ObjectTool.RecalculateAndInvalidateSelectedBounds();
      }

      void ConvertPolyToVertexPoly(VectorShape.Polygon poly)
      {
        VectorShape.Polygon vertexPoly = poly.CloneAsPreSubdividedPolygon();

        poly.ClearVertices();
        foreach(VectorShape.Vertex vertex in vertexPoly.Vertices)
        {
          poly.AddVertex(vertex.Clone());
        }
        
        poly.MinimumLOD = poly.MaximumLOD = 1;
      }

      void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
      {
        Editor.InvalidateRender();
      }

      void createVectorGroupMenuItem_Click(object sender, EventArgs e)
      {
        VectorShape.GroupNode group = new VectorShape.GroupNode("group");
        SelectedShape.UniquifyNames(group);
        
        TreeNode treeNode = new TreeNode();
        treeNode.Name = group.Name;
        treeNode.Text = "group:" + group.Name;

        // if the user clicked on the root node, or no node, add the group under the root
        if(rightClickNode == null || rightClickNode.Parent == null)
        {
          AddNodeToRoot(group, SelectedShape);
        }
        else // otherwise, replace the node with the group and add the node to the group
        {
          VectorShape.Node selectedNode = SelectedShape.GetNode(rightClickNode.Name);
          VectorShape.GroupNode  parent = SelectedShape.GetParentNode(selectedNode);
          parent.RemoveChild(selectedNode);
          parent.InsertChild(rightClickNode.Index, group);
          group.AddChild(selectedNode);
        }

        RepopulateTreeView();
      }

      void treeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
      {
        VectorShape.Node node = SelectedShape.GetNode(e.Node.Name);
        string newLabel = e.Label;

        if(newLabel != null)
        {
          int colon = newLabel.IndexOf(':');
          if(colon != -1)
          {
            newLabel = newLabel.Substring(colon+1);
          }
        }

        if(!string.IsNullOrEmpty(newLabel) && newLabel != e.Node.Name)
        {
          try
          {
            node.Name   = newLabel;
            e.Node.Name = newLabel;
          }
          catch(ArgumentException)
          {
            MessageBox.Show("Node names must be non-empty and unique within the tree.", "oohneek!");
          }
        }

        if(node is VectorShape.PolygonNode)
        {
          e.Node.Text = "poly:" + e.Node.Name;
          SelectPolygon(e.Node.Name);
        }
        else
        {
          e.Node.Text  = "group:" + e.Node.Name;
        }

        // we always "cancel" the edit to prevent the framework from overriding the Text we just set
        e.CancelEdit = true;
      }

      void treeView_AfterSelect(object sender, TreeViewEventArgs e)
      {
        if(e.Action != TreeViewAction.Unknown)
        {
          ObjectTool.InvalidateSelectedBounds(false);
        }
        DeselectPoints();
      }

      void treeView_DragDrop(object sender, DragEventArgs e)
      {
        if(!e.Data.GetDataPresent("System.Windows.Forms.TreeNode")) return;

        TreeNode dropNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
        TreeNode overNode = Editor.treeView.GetNodeAt(Editor.treeView.PointToClient(new Point(e.X, e.Y)));

        VectorShape.Node sdropNode = SelectedShape.GetNode(dropNode.Name);
        VectorShape.Node soverNode = SelectedShape.GetNode(overNode.Name);

        // dragging a node onto a polygon node (or any node when ctrl is held) places the drop node before it.
        // however, this of course doesn't work on the root node.
        if(overNode.Parent != null && (Control.ModifierKeys == Keys.Control || overNode.Text.StartsWith("poly:")))
        {
          int index = overNode.Index;

          dropNode.Remove();
          overNode.Parent.Nodes.Insert(index, dropNode);
          
          SelectedShape.RemoveNode(sdropNode);
          SelectedShape.GetParentNode(soverNode).InsertChild(index, sdropNode);
        }
        // dragging a node onto a group node places the node at the end of the group
        else if(overNode.Text.StartsWith("group:"))
        {
          dropNode.Remove();
          overNode.Nodes.Add(dropNode);

          SelectedShape.RemoveNode(sdropNode);
          ((VectorShape.GroupNode)soverNode).AddChild(sdropNode);
        }

        Editor.InvalidateRender();
      }

      void treeView_DragEnter(object sender, DragEventArgs e)
      {
        if(e.Data.GetDataPresent("System.Windows.Forms.TreeNode"))
        {
          e.Effect = DragDropEffects.Move;
        }
        else
        {
          e.Effect = DragDropEffects.None;
        }
      }

      void treeView_DragOver(object sender, DragEventArgs e)
      {
        if(!e.Data.GetDataPresent("System.Windows.Forms.TreeNode")) return;
        
        TreeNode dropNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
        TreeNode overNode = Editor.treeView.GetNodeAt(Editor.treeView.PointToClient(new Point(e.X, e.Y)));
        
        if(overNode == null || overNode == dropNode)
        {
          e.Effect = DragDropEffects.None;
        }
        else
        {
          e.Effect = DragDropEffects.Move;
        }
      }

      void treeView_ItemDrag(object sender, ItemDragEventArgs e)
      {
        if(((TreeNode)e.Item).Parent != null) // can't drag the root node
        {
          Editor.treeView.DoDragDrop(e.Item, DragDropEffects.Move);
        }
      }

      void treeView_KeyDown(object sender, KeyEventArgs e)
      {
        if(Editor.treeView.SelectedNode == null) return;

        if(e.KeyCode == Keys.F2) // allow editing of node names when the user presses F2 on a selected node
        {
          Editor.treeView.SelectedNode.BeginEdit();
        }
        else if(e.KeyCode == Keys.Delete)
        {
          if(Editor.treeView.SelectedNode.Parent == null) // if it's the root node, delete the whole object
          {
            DeleteSelectedObject();
          }
          else
          {
            DeleteSelectedNode();
          }
          
          Editor.InvalidateRender();
        }
      }

      TreeNode rightClickNode;
      List<int> selectedPoints = new List<int>();
      List<GLPoint> dragPoints = new List<GLPoint>();
      Vector dragVector;
      double dragZoom, dragRotation, dragSize;
      DragMode dragMode;

      /// <summary>Gets client distance moved, taking into account aspect-locking (done with the shift key).</summary>
      static Size GetClientDist(MouseDragEventArgs e)
      {
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
        
        return clientDist;
      }

      /// <summary>Determines whether the specified polygon contains the given point.</summary>
      static bool PolygonContains(VectorShape.Polygon poly, GLPoint point)
      {
        if(poly.Vertices.Count < 3) return false; // if it's not a proper polygon, it won't contain the point.

        // create a GameLib polygon, since it has the appropriate functionality already
        GLPoly glPoly = new GLPoly(poly.Vertices.Count);
        foreach(VectorShape.Vertex vertex in poly.Vertices)
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
      
      /// <summary>Determines whether the polygon vertex order is flipped (counterclockwise) in the given object.</summary>
      internal static bool PolygonsFlipped(VectorObject obj)
      {
        // if an object is flipped once, the vertex order is flipped. if it's flipped twice, they cancel out
        return obj.HorizontalFlip ? !obj.VerticalFlip : obj.VerticalFlip;
      }
    }
    #endregion

    FreehandSubTool freehandTool;
    JoinSubTool joinTool;
    LinksSubTool linksTool;
    MountSubTool mountTool;
    SpatialSubTool spatialTool;
    VectorSubTool vectorTool;
    #endregion

    #region Delegation to subtool
    public override bool CanCopy
    {
      get { return SubTool.CanCopy || selectedObjects.Count != 0; }
    }

    public override bool CanPaste
    {
      get
      {
        return SubTool.CanPaste ||
               EditorApp.Clipboard.Type == ObjectType.Objects || EditorApp.Clipboard.Type == ObjectType.Node;
      }
    }

    public override bool Copy()
    {
      if(SubTool.Copy()) return true;

      if(selectedObjects.Count != 0)
      {
        MemoryStream stream = new MemoryStream();
        using(SexpWriter writer = new SexpWriter(stream))
        {
          Serializer.BeginBatch();
          foreach(SceneObject obj in selectedObjects)
          {
            Serializer.Serialize(obj, writer);
          }
          Serializer.EndBatch();
          writer.Flush();
          EditorApp.Clipboard = new ClipboardObject(ObjectType.Objects, stream.ToArray());
        }
        return true;
      }

      return false;
    }

    public override bool Paste()
    {
      if(SubTool.Paste()) return true;

      object[] objects;
      if(EditorApp.Clipboard.Type == ObjectType.Objects)
      {
        objects = EditorApp.Clipboard.Deserialize();

        // verify that vector objects on the clipboard still point to valid shapes
        foreach(SceneObject sceneObject in objects)
        {
          VectorObject vectorObject = sceneObject as VectorObject;
          if(vectorObject != null && !Editor.IsLocalResource(vectorObject.ShapeName))
          {
            MessageBox.Show("A vector shape referenced by a clipboard object is local to another level. To share a "+
                            "shape across levels, first right-click on an instance of the shape and save it to a "+
                            "file. Then, it will appear in the toolpane. Alternately, you can clone the shape by "+
                            "copying and pasting the root node of the shape rather than an object that references "+
                            "the shape.", "Share-aza", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
          }
        }
      }
      else if(EditorApp.Clipboard.Type == ObjectType.Node)
      {
        VectorShape shape = Editor.CreateLocalShape();
        shape.RootNode = (VectorShape.Node)EditorApp.Clipboard.Deserialize()[0]; // there's only one node on the clipboard

        GLRect bounds = shape.RootNode.GetBounds(); // the node geometry is stored in scene units
        double largestDimension = Math.Max(bounds.Width, bounds.Height);

        VectorObject obj = new VectorObject();
        obj.Size      = new Vector(largestDimension, largestDimension); // make the object square so that texturing,
        obj.Position  = EngineMath.GetCenterPoint(bounds);              // etc works normally
        obj.ShapeName = shape.Name;

        // convert scene units back to local units
        foreach(VectorShape.Polygon poly in VectorSubTool.GetPolygons(shape.RootNode))
        {
          foreach(VectorShape.Vertex vertex in poly.Vertices)
          {
            vertex.Position = obj.SceneToLocal(vertex.Position);
          }
        }

        objects = new object[] { obj };
      }
      else
      {
        return false;
      }

      // add objects to the center of the render panel
      ImportObjects(new Point(Editor.renderPanel.Width/2, Editor.renderPanel.Height/2), objects);
      return true;
    }

    public override void KeyPress(KeyEventArgs e, bool down)
    {
      SubTool.KeyPress(e, down);

      if(!e.Handled)
      {
        char c = char.ToLowerInvariant((char)e.KeyValue);
        if(e.Modifiers == Keys.Control && c >= '0' && c <= '9') // ctrl-# moves objects between layers
        {
          int layer = c=='0' ? 9 : c-'1';
          foreach(SceneObject obj in selectedObjects)
          {
            obj.Layer = layer;
          }
          EditorApp.MainForm.StatusText = "Object(s) moved to layer "+layer;
          //Editor.CurrentLayer = layer;
          Editor.InvalidateRender();
          e.Handled = true;
        }
        else if(down && e.Modifiers == Keys.Shift && c == 'v') // shift-v selects the vector subtool
        {
          SubTool = VectorTool;
          e.Handled = true;
        }
        else if(down && e.Modifiers == Keys.Shift && c == 's') // shift-s selects the spatial subtool
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
      VectorShape shape = Editor.CreateLocalShape();

      VectorShape.PolygonNode polyNode = new VectorShape.PolygonNode("poly");
      shape.RootNode = polyNode;

      VectorShape.Vertex vertex = new VectorShape.Vertex();
      vertex.Color    = GetInverseBackgroundColor();
      vertex.Position = new GLPoint(-1, -1);
      vertex.Type     = breakVertices ? VectorShape.VertexType.Split : VectorShape.VertexType.Normal;
      polyNode.Polygon.AddVertex(vertex);

      vertex = vertex.Clone();
      vertex.Position = new GLPoint(1, -1);
      polyNode.Polygon.AddVertex(vertex);
      vertex = vertex.Clone();
      vertex.Position = new GLPoint(1, 1);
      polyNode.Polygon.AddVertex(vertex);
      vertex = vertex.Clone();
      vertex.Position = new GLPoint(-1, 1);
      polyNode.Polygon.AddVertex(vertex);

      VectorObject obj = new VectorObject();
      double size = Math.Max(SceneView.CameraArea.Width, SceneView.CameraArea.Height);
      obj.ShapeName = shape.Name;
      obj.Layer     = Editor.CurrentLayer;
      obj.Position  = SceneView.ClientToScene(at);
      obj.Size      = new Vector(size/10, size/10);

      SubTool = SpatialTool;
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

    void DeleteObjectFromScene(SceneObject obj)
    {
      Scene.RemoveObject(obj);
    }

    Color GetInverseBackgroundColor()
    {
      Color bgColor = SceneView.BackColor;
      return Color.FromArgb(255-bgColor.R, 255-bgColor.G, 255-bgColor.B);
    }

    void ImportObjects(Point clientCenter, object[] sceneObjs)
    {
      // first find the bounds of the objects in scene space and in layers
      double x1=double.MaxValue, y1=double.MaxValue, x2=double.MinValue, y2=double.MinValue;
      int minLayer=int.MaxValue, maxLayer=int.MinValue;

      foreach(SceneObject obj in sceneObjs)
      {
        if(obj.X < x1) x1 = obj.X;
        if(obj.X > x2) x2 = obj.X;
        if(obj.Y < y1) y1 = obj.Y;
        if(obj.Y > y2) y2 = obj.Y;
        if(obj.Layer < minLayer) minLayer = obj.Layer;
        if(obj.Layer > maxLayer) maxLayer = obj.Layer;
      }

      // now calculate a vector that will center the objects around the 'clientCenter'
      Vector offset = SceneView.ClientToScene(clientCenter) - new GLPoint(x1+(x2-x1)*0.5, y1+(y2-y1)*0.5);
      
      // now calculate the layer offset that must be applied. we would prefer to put the topmost objects on the current
      // layer, but if that causes the bottommost items to be outside the valid layer range, we'll adjust it upwards
      int layerOffset = Editor.CurrentLayer - maxLayer;
      if(minLayer + layerOffset < 0)
      {
        layerOffset -= minLayer + layerOffset;
      }
      
      SubTool = SpatialTool;
      DeselectObjects();
      foreach(SceneObject obj in sceneObjs)
      {
        obj.Position += offset;
        obj.Layer    += layerOffset;
        Scene.AddObject(obj);
        SelectObject(obj, false);
      }
      Editor.InvalidateRender();
    }

    bool IsSelected(SceneObject obj) { return selectedObjects.Contains(obj); }
    
    bool IsShapeReferenced(VectorShape shape)
    {
      bool found = false;
      foreach(SceneObject sceneObj in Scene.PickAll())
      {
        VectorObject vectorObj = sceneObj as VectorObject;
        if(vectorObj != null && vectorObj.Shape == shape)
        {
          found = true;
          break;
        }
      }

      return found;
    }

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

    public void DeselectObjects()
    {
      if(selectedObjects.Count != 0)
      {
        InvalidateSelectedBounds(false);
        selectedObjects.Clear();
        ClearSelectedObjectBounds();
        OnSelectedObjectsChanged();
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

    ObjectSubTool subTool;

    Rectangle selectedObjectBounds;
    GLRect selectedObjectSceneBounds;
    List<SceneObject> selectedObjects = new List<SceneObject>();

    static T CloneObject<T>(T obj)
    {
      MemoryStream ms = new MemoryStream();
      Serializer.BeginBatch();
      Serializer.Serialize(obj, ms);
      Serializer.EndBatch();

      ms.Position = 0;
      Serializer.BeginBatch();
      obj = (T)Serializer.Deserialize(ms);
      Serializer.EndBatch();

      return obj;
    }

    [Flags]
    enum Handle
    {
      None=0,
      Top=1,
      Bottom=2,
      Left=4,
      Right=8,

      TopLeft=Top|Left, TopRight=Top|Right, BottomLeft=Bottom|Left, BottomRight=Bottom|Right,
    }

    static Rectangle GetHandleRect(Rectangle bounds, Handle handle)
    {
      int x, y;

      if((handle & Handle.Left) != 0) x = bounds.X;
      else if((handle & Handle.Right) != 0) x = bounds.Right;
      else x = bounds.X + bounds.Width/2;

      if((handle & Handle.Top) != 0) y = bounds.Y;
      else if((handle & Handle.Bottom) != 0) y = bounds.Bottom;
      else y = bounds.Y + bounds.Height/2;

      return new Rectangle(x-2, y-2, 5, 5);
    }

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

        pickToCurrent = new CheckBox();
        pickToCurrent.Location = new Point(renderToCurrent.Left, renderToCurrent.Bottom-1);
        pickToCurrent.Size     = renderToCurrent.Size;
        pickToCurrent.TabIndex = 1;
        pickToCurrent.Text     = "Pick to current layer";
        pickToCurrent.CheckedChanged += new EventHandler(pickToCurrent_CheckedChanged);

        layerBox = new CheckedListBox();
        layerBox.Location = new Point(renderToCurrent.Left, pickToCurrent.Bottom - 1);
        layerBox.Size = new Size(renderToCurrent.Width, layerPanel.Height - layerBox.Top - 1);
        layerBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
        layerBox.IntegralHeight = false;
        layerBox.TabIndex = 2;
        layerBox.CheckOnClick = true;
        layerBox.ItemCheck += new ItemCheckEventHandler(layerBox_ItemCheck);
        layerBox.SelectedIndexChanged += new EventHandler(layerBox_SelectedIndexChanged);

        for(int i=0; i<32; i++)
        {
          layerBox.Items.Add("Layer "+i);
        }

        layerPanel.Controls.Add(renderToCurrent);
        layerPanel.Controls.Add(pickToCurrent);
        layerPanel.Controls.Add(layerBox);
        layerPanel.Visible = false;
        Editor.rightPane.Panel2.Controls.Add(layerPanel);
      }

      Update();

      layerPanel.Visible = true;
      Editor.ShowRightPane(layerPanel);
    }

    public override void Deactivate()
    {
      Editor.HideRightPane();
    }

    public void Update()
    {
      renderToCurrent.Checked = Editor.RenderToCurrentLayer;
      pickToCurrent.Checked   = Editor.PickToCurrentLayer;

      for(int i=0; i<32; i++)
      {
        // don't use the accessor because we want to bypass RenderToCurrentLayer
        layerBox.SetItemChecked(i, (Editor.visibleLayerMask & (1<<i)) != 0);
      }
      layerBox.SelectedIndex = Editor.CurrentLayer;
    }

    void pickToCurrent_CheckedChanged(object sender, EventArgs e)
    {
      Editor.PickToCurrentLayer = pickToCurrent.Checked;
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
    CheckBox renderToCurrent, pickToCurrent;
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
        SceneView.CameraZoom    *= zoomFactor;
      }
      else // zooming out
      {
        SceneView.CameraZoom /= zoomFactor;
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

      Engine.Engine.AddResource<ImageMap>(md.ImageMap);
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
          displayArea.Height = iconBuffer.Width * displayArea.Height / displayArea.Width;
          displayArea.Width  = iconBuffer.Width;
        }
        else
        {
          displayArea.Width  = iconBuffer.Height * displayArea.Width / displayArea.Height;
          displayArea.Height = iconBuffer.Height;
        }
        displayArea.X = (iconBuffer.Width  - displayArea.Width)  / 2;
        displayArea.Y = (iconBuffer.Height - displayArea.Height) / 2;
        
        GLBuffer.SetCurrent(iconBuffer);
        Engine.Engine.ResetOpenGL(iconBuffer.Width, iconBuffer.Height, new Rectangle(new Point(), iconBuffer.Size));
        GL.glClearColor(Color.White);
        GL.glColor(Color.White);
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
    else if(item is VectorShapeItem || item is ObjectTemplateItem)
    {
      Scene scene = new Scene();

      SceneViewControl sceneView = new SceneViewControl();
      sceneView.Bounds     = new Rectangle(0, 0, iconBuffer.Width, iconBuffer.Height);
      sceneView.BackColor  = Color.White;
      sceneView.Scene      = scene;

      DesktopControl desktop = new DesktopControl();
      desktop.Bounds = sceneView.Bounds;
      desktop.AddChild(sceneView);

      SceneObject[] objs = item.CreateSceneObjects(sceneView);
      foreach(SceneObject obj in objs)
      {
        scene.AddObject(obj);
      }

      // find the rectangle that contains all of the objects
      GLRect objBounds = objs[0].GetRotatedAreaBounds();
      for(int i=1; i<objs.Length; i++)
      {
        objBounds.Unite(objs[i].GetRotatedAreaBounds());
      }

      // configure the camera to show all of the objects
      sceneView.CameraPosition = EngineMath.GetCenterPoint(objBounds);
      if(objBounds.Width >= objBounds.Height)
      {
        sceneView.CameraSize = objBounds.Width;
        sceneView.CameraAxis = CameraAxis.X;
      }
      else
      {
        sceneView.CameraSize = objBounds.Height;
        sceneView.CameraAxis = CameraAxis.Y;
      }

      // now render the scene
      GLBuffer.SetCurrent(iconBuffer);
      Engine.Engine.ResetOpenGL(desktop.Width, desktop.Height, desktop.Bounds);
      GL.glClear(GL.GL_COLOR_BUFFER_BIT);
      desktop.Render();
      GL.glFlush();
      GLBuffer.SetCurrent(null);

      return AddIcon(iconBuffer.CreateBitmap(), lvItem == null ? 0 : lvItem.ImageIndex);
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
      SceneObject[] objs = item.CreateSceneObjects(sceneView);
      
      // find the rectangle that bounds all object positions
      double x1=double.MaxValue, y1=double.MaxValue, x2=double.MinValue, y2=double.MinValue;
      int minLayer = int.MaxValue, maxLayer = int.MinValue;
      foreach(SceneObject obj in objs)
      {
        if(obj.X < x1) x1 = obj.X;
        if(obj.X > x2) x2 = obj.X;
        if(obj.Y < y1) y1 = obj.Y;
        if(obj.Y > y2) y2 = obj.Y;
        if(obj.Layer < minLayer) minLayer = obj.Layer;
        if(obj.Layer > maxLayer) maxLayer = obj.Layer;
      }

      // find the center point of all the objects and the offset needed to center the objects around the drop point
      GLPoint centerPoint = new GLPoint(x1+(x2-x1)*0.5, y1+(y2-y1)*0.5);
      GLPoint dropPoint = sceneView.ClientToScene(renderPanel.PointToClient(new Point(e.X, e.Y)));
      Vector  posOffset = dropPoint - centerPoint;

      int layerOffset = CurrentLayer-minLayer, layerSpread = maxLayer-minLayer;

      if(CurrentLayer + layerSpread > 31)
      {
        layerOffset -= CurrentLayer + layerSpread - 31;
      }

      // add each item to the scene, adjusting its position and layer
      foreach(SceneObject obj in objs)
      {
        obj.Position += posOffset;
        obj.Layer    += layerOffset;
        Scene.AddObject(obj);
      }

      InvalidateRender();

      CurrentTool = Tools.Object;
      Tools.Object.SubTool = Tools.Object.SpatialTool;

      // select all the objects added
      Tools.Object.DeselectObjects();
      foreach(SceneObject obj in objs)
      {
        Tools.Object.SelectObject(obj, false);
      }
    }
  }

  void newStaticImage_Click(object sender, EventArgs e)
  {
    OpenFileDialog ofd = new OpenFileDialog();
    ofd.Filter = "Image files (png;jpeg;bmp;pcx;gif)|*.png;*.jpg;*.jpeg;*.bmp;*.pcx;*.gif|All files (*.*)|*.*";
    ofd.Title  = "Select an image to import";
    ofd.InitialDirectory = Project.ImagesPath.TrimEnd('/');
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
        sfd.InitialDirectory = Project.ImagesPath.TrimEnd('/');
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
      else if(char.ToUpper(c)=='R' || e.KeyCode == Keys.Home) // pressing R or Home resets the camera view
      {
        sceneView.CameraZoom     = 1;
        sceneView.CameraPosition = new GLPoint();
        InvalidateView();
      }
      else if(e.Modifiers == Keys.Control && c == ' ') // ctrl-space toggles 'pickToCurrentLayer'
      {
        PickToCurrentLayer = !PickToCurrentLayer;
        if(CurrentTool == Tools.Layers)
        {
          Tools.Layers.Update();
        }
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

    const int DetentsPerClick = 120; // 120 is the standard delta for a single wheel click

    if(Control.ModifierKeys == Keys.None) // plain mouse wheeling zooms in and out
    {
      const double zoomFactor = 1.25;
      double wheelMovement = (double)e.Delta / DetentsPerClick;

      if(wheelMovement < 0) // zooming out
      {
        sceneView.CameraZoom /= Math.Pow(zoomFactor, -wheelMovement);
      }
      else // zooming in
      {
        sceneView.CameraZoom *= Math.Pow(zoomFactor, wheelMovement);
      }

      InvalidateView();
    }
    else if(Control.ModifierKeys == Keys.Control) // ctrl-wheeling changes current layer
    {
      CurrentLayer = EngineMath.Clip(CurrentLayer + e.Delta/DetentsPerClick, 0, 31);
    }
  }
  
  GLPoint dragStart;
  bool dragScrolling;

  void renderPanel_Resize(object sender, EventArgs e)
  {
    desktop.Bounds = renderPanel.ClientRectangle;
    if(sceneView != null) sceneView.Bounds = desktop.Bounds;
    if(CurrentTool != null) currentTool.PanelResized();
  }
  #endregion

  #region Painting, rendering, and layout
  void renderPanel_RenderBackground(object sender, PaintEventArgs e)
  {
    Color bgColor = sceneView.BackColor;

    // if we have a trace image, we need to insert it into the rendering pipeline without breaking encapsulation.
    // to do this, we'll temporarily set the background color of the scene view to a transparent color (eliminating
    // the background), and render our own background
    if(traceImage != null)
    {
      GL.glColor(bgColor);
      GL.glRecti(e.ClipRectangle.X, e.ClipRectangle.Y, e.ClipRectangle.Right, e.ClipRectangle.Bottom);

      Rectangle destRect = new Rectangle(0, 0, traceImage.ImgWidth, traceImage.ImgHeight);
      if((double)destRect.Width/destRect.Height >= (double)renderPanel.Width/renderPanel.Height)
      {
        destRect.Height = renderPanel.Width * destRect.Height / destRect.Width;
        destRect.Width  = renderPanel.Width;
      }
      else
      {
        destRect.Width  = renderPanel.Height * destRect.Width / destRect.Height;
        destRect.Height = renderPanel.Height;
      }
      destRect.X = (renderPanel.Width  - destRect.Width)  / 2;
      destRect.Y = (renderPanel.Height - destRect.Height) / 2;

      GL.glEnable(GL.GL_TEXTURE_2D);
      GL.glEnable(GL.GL_BLEND);
      GL.glColor(Color.White);
      traceImage.Bind();
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);
      GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
      GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);

      GL.glBegin(GL.GL_QUADS);
        GL.glTexCoord2d(0, 0);
        GL.glVertex2i(destRect.Left, destRect.Top);
        
        GL.glTexCoord2d((double)traceImage.ImgWidth/traceImage.TexWidth, 0);
        GL.glVertex2i(destRect.Right, destRect.Top);

        GL.glTexCoord2d((double)traceImage.ImgWidth/traceImage.TexWidth, (double)traceImage.ImgHeight/traceImage.TexHeight);
        GL.glVertex2i(destRect.Right, destRect.Bottom);

        GL.glTexCoord2d(0, (double)traceImage.ImgHeight/traceImage.TexHeight);
        GL.glVertex2i(destRect.Left, destRect.Bottom);
      GL.glEnd();

      GL.glDisable(GL.GL_TEXTURE_2D);
      GL.glDisable(GL.GL_BLEND);
      
      sceneView.BackColor = Color.FromArgb(0, 255, 255, 255);
    }

    sceneView.Invalidate();
    desktop.Render();

    if(traceImage != null)
    {
      sceneView.BackColor = bgColor;
    }
  }
 
  void renderPanel_Paint(object sender, PaintEventArgs e)
  {
    currentTool.PaintDecoration(e.Graphics);
  }

  void SceneEditor_GotFocus(object sender, EventArgs e)
  {
    // since we can't easily propogate texture modes between rendering contexts,
    // we'll simply reset the mode whenever we get focus
    foreach(ResourceHandle<ImageMap> handle in Engine.Engine.GetResources<ImageMap>())
    {
      if(handle.Resource != null) handle.Resource.InvalidateMode();
    }
  }

  void ShowTreeView()
  {
    treeView.Visible   = true;
    objectList.Visible = false;
    objToolBar.Visible = false;
  }
  
  void HideTreeView()
  {
    objToolBar.Visible = true;
    objectList.Visible = true;
    treeView.Visible   = false;
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
  
  #region Other event handlers
  void propertyGridMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
  {
    PropertyDescriptor prop = propertyGrid.SelectedGridItem == null ?
      null : propertyGrid.SelectedGridItem.PropertyDescriptor;
    
    resetPropertyValueMenuItem.Enabled = prop != null && prop.Attributes[typeof(DefaultValueAttribute)] != null;
    openColorPickerMenuItem.Visible    = prop != null && prop.PropertyType == typeof(Color);
  }

  void resetPropertyValueMenuItem_Click(object sender, EventArgs e)
  {
    PropertyDescriptor prop = propertyGrid.SelectedGridItem.PropertyDescriptor;
    object component = propertyGrid.SelectedObjects.Length == 1 ?
      propertyGrid.SelectedObject : propertyGrid.SelectedObjects;
    DefaultValueAttribute dv = (DefaultValueAttribute)prop.Attributes[typeof(DefaultValueAttribute)];
    prop.SetValue(component, Convert.ChangeType(dv.Value, prop.PropertyType));
    propertyGrid.Refresh();
    InvalidateRender();
  }

  void openColorPickerMenuItem_Click(object sender, EventArgs e)
  {
    PropertyDescriptor prop = propertyGrid.SelectedGridItem.PropertyDescriptor;
    object component = propertyGrid.SelectedObjects.Length == 1 ?
      propertyGrid.SelectedObject : propertyGrid.SelectedObjects;
    object value = prop.GetValue(component);
    
    ColorDialog cd = new ColorDialog();
    cd.AnyColor = true;
    cd.FullOpen = true;
    if(value != null)
    {
      cd.Color = (Color)value;
    }
    if(cd.ShowDialog() == DialogResult.OK)
    {
      prop.SetValue(component, cd.Color);
      propertyGrid.Refresh();
      InvalidateRender();
    }
  }

  void editMenu_DropDownOpening(object sender, EventArgs e)
  {
    editCopyMenuItem.Enabled  = CurrentTool.CanCopy;
    editPasteMenuItem.Enabled = CurrentTool.CanPaste;
    editUnloadTraceItem.Enabled = traceImage != null;
  }

  void editCopyMenuItem_Click(object sender, EventArgs e)
  {
    CurrentTool.Copy();
  }

  void editPasteMenuItem_Click(object sender, EventArgs e)
  {
    CurrentTool.Paste();
  }
  
  void editLoadTraceItem_Click(object sender, EventArgs e)
  {
    OpenFileDialog fd = new OpenFileDialog();
    fd.Filter = "Image files (*.jpg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*";
    fd.Title  = "Select tracing image";

    if(fd.ShowDialog() == DialogResult.OK)
    {
      Surface surface = new Surface(fd.FileName);
      //surface.UsingAlpha = true;
      //surface.Alpha      = 128;

      if(traceImage != null)
      {
        traceImage.Dispose();
      }

      traceImage = new GLTexture2D(surface);
      surface.Dispose();
      InvalidateRender();
    }
  }

  void editUnloadTraceItem_Click(object sender, EventArgs e)
  {
    traceImage.Dispose();
    traceImage = null;
    InvalidateRender();
  }
  #endregion

  PickOptions GetPickerOptions()
  {
    PickOptions options = new PickOptions();
    options.AllowInvisible  = true;
    options.AllowUnpickable = true;
    options.GroupMask       = 0xffffffff;
    options.LayerMask       = PickLayerMask;
    options.SortByLayer     = true;
    return options;
  }

  VectorShape CreateLocalShape()
  {
    VectorShape shape = new VectorShape(Project.GetNewShapeName());
    Engine.Engine.AddResource<VectorShape>(shape);
    localResources.Add(shape.Name, null);
    return shape;
  }

  bool IsLocalResource(string resourceName)
  {
    return localResources.ContainsKey(resourceName);
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

  void SaveSharedShape(VectorShape shape)
  {
    if(shape == null) throw new ArgumentNullException();

    string shapeName = shape.Name;

    // replace slashes with the directory separator character, for systems with different separator characters
    shapeName = shapeName.Replace('\\', Path.DirectorySeparatorChar);
    shapeName = shapeName.Replace('/', Path.DirectorySeparatorChar);

    // replace invalid path characters with a tilde-encoding scheme (assumes tilde is valid...)
    shapeName = shapeName.Replace("~", "~"+((int)'~').ToString("X"));
    foreach(char c in Path.GetInvalidFileNameChars())
    {
      if(c != '\0' && c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
      {
        shapeName = shapeName.Replace(c.ToString(), "~"+((int)c).ToString("X"));
      }
    }

    // construct the final path
    string shapePath = Path.Combine(Project.AnimationPath, shapeName+".shape");
    // ensure that the directory exists (the name can contain slashes, which will create directories)
    Directory.CreateDirectory(Path.GetDirectoryName(shapePath));

    // save the animation
    using(Stream file = File.Open(shapePath, FileMode.Create, FileAccess.Write))
    {
      Serializer.BeginBatch();
      Serializer.Serialize(shape, file);
      Serializer.EndBatch();
    }
  }

  Dictionary<string,object> localResources = new Dictionary<string,object>();
  DesktopControl desktop = new DesktopControl();
  SceneViewControl sceneView;
  GLTexture2D traceImage;
  string levelFile;
  int systemDefinedIconCount;
  bool isModified, isClosed;
  
  static GLBuffer iconBuffer = new GLBuffer(32, 32);
}

enum ToolboxCategory { StaticImages, AnimatedImages, ObjectTemplates, VectorShapes, Miscellaneous }

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
  
  public virtual SceneObject[] CreateSceneObjects(SceneViewControl sceneView)
  {
    return new SceneObject[] { CreateSceneObject(sceneView) };
  }

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

  protected abstract SceneObject CreateSceneObject(SceneViewControl sceneView);

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

  protected override SceneObject CreateSceneObject(SceneViewControl sceneView)
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
    return Engine.Engine.GetImageMap(imageMapName).Resource;
  }

  protected override SceneObject CreateSceneObject(SceneViewControl sceneView)
  {
    ImageMap map = GetImageMap();
    if(map.Frames.Count == 0)
    {
      MessageBox.Show("This image map has no frames. Try editing the map.", "Uh oh.",
                      MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
      return null;
    }

    StaticImageObject obj = new StaticImageObject();
    obj.ImageMap = map.Name;

    // calculate the right image size for the default camera zoom
    double previousZoom  = sceneView.CameraZoom;
    sceneView.CameraZoom = 1;
    obj.Size = sceneView.ClientToScene(map.Frames[0].Size);
    sceneView.CameraZoom = previousZoom;

    return obj;
  }

  string imageMapName;
}
#endregion

#region VectorShapeItem
sealed class VectorShapeItem : ToolboxItem
{
  public VectorShapeItem(VectorShape shape) : base("shape:"+shape.Name)
  {
    shapeName = shape.Name;
  }

  public override ToolboxCategory Category
  {
    get { return ToolboxCategory.VectorShapes; }
  }

  public override string DisplayName
  {
    get { return shapeName; }
  }

  protected override SceneObject CreateSceneObject(SceneViewControl sceneView)
  {
    double size = Math.Max(sceneView.CameraArea.Width, sceneView.CameraArea.Height);

    VectorObject obj = new VectorObject();
    obj.ShapeName = shapeName;
    obj.Size      = new Vector(size/10, size/10);
    return obj;
  }

  string shapeName;
}
#endregion

#region ObjectTemplateItem
sealed class ObjectTemplateItem : ToolboxItem
{
  public ObjectTemplateItem(string filename) : base("obj:"+filename.ToLower())
  {
    this.filename = filename;
  }

  public override ToolboxCategory Category
  {
    get { return ToolboxCategory.ObjectTemplates; }
  }

  public override string DisplayName
  {
    get { return Path.GetFileNameWithoutExtension(filename); }
  }

  public override SceneObject[] CreateSceneObjects(SceneViewControl sceneView)
  {
    using(SexpReader reader = new SexpReader(File.Open(filename, FileMode.Open, FileAccess.Read)))
    {
      List<SceneObject> objects = new List<SceneObject>();
      Serializer.BeginBatch();
      try
      {
        while(!reader.EOF)
        {
          objects.Add((SceneObject)Serializer.Deserialize(reader));
        }
      }
      finally
      {
        Serializer.EndBatch();
      }
      return objects.ToArray();
    }
  }
  
  protected override SceneObject CreateSceneObject(SceneViewControl sceneView)
  {
    throw new NotSupportedException();
  }
  
  string filename;
}
#endregion
#endregion

} // namespace RotationalForce.Editor
