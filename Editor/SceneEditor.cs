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
    InitializeComponent();

    ListViewItem triggerItem = new ListViewItem("Trigger", 0, objectList.Groups[3]);
    triggerItem.Tag = TriggerName;
    objectList.Items.Add(triggerItem);
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
    renderPanel.Invalidate(rect);
  }

  void InvalidateRender()
  {
    sceneView.Invalidate();
    renderPanel.Invalidate();
  }

  void InvalidateRender(Rectangle rect)
  {
    sceneView.Invalidate(rect);
    renderPanel.InvalidateRender(rect);
  }

  void InvalidateObjectBounds(SceneObject obj, bool invalidateRender)
  {
    Rectangle controlRect = sceneView.SceneToClient(obj.GetRotatedAreaBounds()); // get the client rectangle
    controlRect.Inflate(DecorationRadius, DecorationRadius); // include space for our decoration
    Invalidate(controlRect, invalidateRender);
  }
  
  void InvalidateSelectedBounds(bool invalidateRender)
  {
    Rectangle rect = selectedObjectBounds;
    rect.Inflate(DecorationRadius, DecorationRadius);
    Invalidate(rect, invalidateRender);
  }
  #endregion

  #region Layers
  uint CurrentLayerMask
  {
    get { return 0xffffffff; }
  }

  uint VisibleLayerMask
  {
    get { return 0xffffffff; }
  }
  #endregion

  #region Object selection
  [Flags]
  new enum Handle
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
    Rectangle clientBounds = sceneView.SceneToClient(sceneBounds);

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

  SceneObject ObjectUnderPoint(Point pt)
  {
    PickOptions options = new PickOptions();
    options.AllowInvisible  = true;
    options.AllowUnpickable = true;
    options.GroupMask       = 0xffffffff;
    options.LayerMask       = CurrentLayerMask;
    options.SortByLayer     = true;

    foreach(SceneObject obj in scene.PickPoint(sceneView.ClientToScene(pt), options))
    {
      return obj; // return the first item
    }
    
    return null;
  }

  void OnSelectedObjectsChanged()
  {
    if(selectedObjects.Count == 0)
    {
      HideRightPane();
    }
    else
    {
      propertyGrid.SelectedObjects = selectedObjects.ToArray();
      ShowPropertyGrid();
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

  void SelectObject(SceneObject obj, bool deselectOthers)
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

  void SelectObject(Point pt, bool deselectOthers)
  {
    SceneObject obj = ObjectUnderPoint(pt);
    if(obj != null) SelectObject(obj, deselectOthers);
    else if(deselectOthers) DeselectObjects();
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

  Rectangle selectedObjectBounds;
  GLRect selectedObjectSceneBounds;
  List<SceneObject> selectedObjects = new List<SceneObject>();
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
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
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
    // propertyGrid
    // 
    this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
    this.propertyGrid.Location = new System.Drawing.Point(0, 0);
    this.propertyGrid.Name = "propertyGrid";
    this.propertyGrid.Size = new System.Drawing.Size(205, 276);
    this.propertyGrid.TabIndex = 0;
    this.propertyGrid.Visible = false;
    this.propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler(propertyGrid_PropertyValueChanged);
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

  void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
  {
    RecalculateSelectedBounds();
    InvalidateRender();
  }

  void SceneEditor_KeyDown(object sender, KeyEventArgs e)
  {
    SceneEditor_KeyChanged(e, true);
  }

  void SceneEditor_KeyUp(object sender, KeyEventArgs e)
  {
    SceneEditor_KeyChanged(e, false);
  }

  void SceneEditor_KeyChanged(KeyEventArgs e, bool down)
  {
    if(!renderPanel.Focused) return;

    if(e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey)
    {
      SetCursor();
    }
    else if(e.KeyCode == Keys.Delete && down)
    {
      foreach(SceneObject obj in selectedObjects)
      {
        scene.RemoveObject(obj);
      }
      DeselectObjects();
    }
  }

  void renderPanel_MouseEnter(object sender, EventArgs e)
  {
    renderPanel.Focus();
  }

  #region Mouse handling within the render pane
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

  void renderPanel_MouseMove(object sender, MouseEventArgs e)
  {
    SetCursor(e.Location);
  }

  void renderPanel_MouseClick(object sender, MouseEventArgs e)
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

  void renderPanel_MouseDragStart(object sender, MouseEventArgs e)
  {
    // we only care about left-drags
    if(e.Button != MouseButtons.Left)
    {
      renderPanel.CancelMouseDrag();
      return;
    }
    
    // if zero or one objects are selected, select the object under the pointer
    if(selectedObjects.Count < 2)
    {
      SceneObject obj = ObjectUnderPoint(e.Location);
      if(obj != null) SelectObject(obj, true);
    }
    
    dragHandle = GetHandleUnderPoint(e.Location);

    // ignore drags that take place outside the selection rectangle
    if(dragHandle == Handle.None && !GetHandleBorder().Contains(e.Location))
    {
      renderPanel.CancelMouseDrag();
      return;
    }

    draggedObjs.Clear();
    foreach(SceneObject obj in selectedObjects)
    {
      draggedObjs.Add(new DraggedObject(obj));
    }

     // plain drag does movement or resizing. shift-drag does aspect-locked resizing or axis-locked moving.
    if(Control.ModifierKeys == Keys.None || Control.ModifierKeys == Keys.Shift)
    {
      rotating = false;
      
      if(dragHandle != Handle.None) // resizing
      {
        dragBounds = selectedObjectSceneBounds;
      }
    }
    else if(Control.ModifierKeys == Keys.Control) // rotation drag
    {
      rotating = true;

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
      dragRotation = GLMath.AngleBetween(dragCenter, sceneView.ClientToScene(e.Location));
    }
  }

  void renderPanel_MouseDrag(object sender, MouseDragEventArgs e)
  {
    InvalidateSelectedBounds(true);

    if(rotating) // rotating selected objects
    {
      double rotation = GLMath.AngleBetween(dragCenter, sceneView.ClientToScene(e.Location)) - dragRotation;

      for(int i=0; i<selectedObjects.Count; i++)
      {
        Vector movement = (draggedObjs[i].Position - dragCenter).Rotated(rotation);
        selectedObjects[i].Rotation = draggedObjs[i].Rotation + rotation * MathConst.RadiansToDegrees;
        selectedObjects[i].Position = dragCenter + movement;
      }
    }
    else if(dragHandle == Handle.None) // moving selected objects
    {
      Size clientDist = new Size(e.X-e.Start.X, e.Y-e.Start.Y);
      Vector sceneDist = sceneView.ClientToScene(clientDist);
      for(int i=0; i<selectedObjects.Count; i++)
      {
        selectedObjects[i].Position = draggedObjs[i].Position + sceneDist;
      }
    }
    else // resizing selected objects
    {
      Size clientDist   = new Size(e.X-e.Start.X, e.Y-e.Start.Y);
      Vector sceneDist  = sceneView.ClientToScene(clientDist);
      Vector sizeDelta = new Vector();

      if((dragHandle & Handle.Left) != 0)
      {
        sizeDelta.X = -sceneDist.X;
      }
      else if((dragHandle & Handle.Right) != 0)
      {
        sizeDelta.X = sceneDist.X;
      }
      else
      {
        sceneDist.X = 0;
      }

      if((dragHandle & Handle.Top) != 0)
      {
        sizeDelta.Y = -sceneDist.Y;
      }
      else if((dragHandle & Handle.Bottom) != 0)
      {
        sizeDelta.Y = sceneDist.Y;
      }
      else
      {
        sceneDist.Y = 0;
      }

      bool aspectLock = Control.ModifierKeys == Keys.Shift;
      foreach(SceneObject obj in selectedObjects)
      {
        if(obj.Rotation != 0 && obj.Rotation != 180 && obj.Rotation != 90 && obj.Rotation != 270)
        {
          aspectLock = true;
          break;
        }
      }
      
      GLRect handleRect = sceneView.ClientToScene(GetHandleBorder());
      GLRect boundsRect = selectedObjectSceneBounds;

      if(aspectLock)
      {
        Vector oldSize = sizeDelta;

        if(dragHandle == Handle.Top || dragHandle == Handle.Bottom)
        {
          sizeDelta.X = sizeDelta.Y * boundsRect.Width / boundsRect.Height;
        }
        else if(dragHandle == Handle.Left || dragHandle == Handle.Right || sizeDelta.Y < sizeDelta.X)
        {
          sizeDelta.Y = sizeDelta.X * boundsRect.Height / boundsRect.Width;
        }
        else if(sizeDelta.X < sizeDelta.Y)
        {
          sizeDelta.X = sizeDelta.Y * boundsRect.Width / boundsRect.Height;
        }

        sceneDist.X = Math.Sign(sceneDist.X) * Math.Abs(sizeDelta.X);
        sceneDist.Y = Math.Sign(sceneDist.Y) * Math.Abs(sizeDelta.Y);

        if(Math.Sign(oldSize.X) != Math.Sign(sizeDelta.X)) sceneDist.X = -sceneDist.X;
        if(Math.Sign(oldSize.Y) != Math.Sign(sizeDelta.Y)) sceneDist.Y = -sceneDist.Y;
      }

      for(int i=0; i<selectedObjects.Count; i++)
      {
        SceneObject obj = selectedObjects[i];

        Vector delta = sizeDelta, objSize = draggedObjs[i].Size;

        if(obj.Rotation == 90 || obj.Rotation == 270)
        {
          EngineMath.Swap(ref objSize.X, ref objSize.Y);
          EngineMath.Swap(ref delta.X, ref delta.Y);
        }

        Vector sizeIncrease = new Vector(delta.X * objSize.X / dragBounds.Width,
                                         delta.Y * objSize.Y / dragBounds.Height);
        Vector movement = new Vector((draggedObjs[i].Position.X-dragBounds.X) / dragBounds.Width  * sceneDist.X,
                                     (draggedObjs[i].Position.Y-dragBounds.Y) / dragBounds.Height * sceneDist.Y);
        Vector newSize = draggedObjs[i].Size + sizeIncrease;

        bool hflip=false, vflip=false;
        if(newSize.X < 0)
        {
          hflip = true;
          newSize.X = -newSize.X;
        }
        if(newSize.Y < 0)
        {
          vflip = true;
          newSize.Y = -newSize.Y;
        }

        obj.HorizontalFlip = hflip ? !draggedObjs[i].HorizontalFlip : draggedObjs[i].HorizontalFlip;
        obj.VerticalFlip   = vflip ? !draggedObjs[i].VerticalFlip   : draggedObjs[i].VerticalFlip;
        obj.Size           = newSize;
        obj.Position       = draggedObjs[i].Position + movement;
      }
    }

    RecalculateSelectedBounds();
    InvalidateSelectedBounds(true);
  }

  void renderPanel_MouseDragEnd(object sender, MouseDragEventArgs e)
  {
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

  List<DraggedObject> draggedObjs = new List<DraggedObject>();
  GLPoint dragCenter; // center of drag rotation
  GLRect  dragBounds; // original bounding rectangle during resize
  double  dragRotation;
  Handle dragHandle;
  bool rotating;
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
      scene.AddObject(obj);
      SelectObject(obj, true);
      InvalidateObjectBounds(obj, true);
    }
  }
  #endregion

  private void renderPanel_Resize(object sender, EventArgs e)
  {
    sceneView.Bounds = desktop.Bounds = renderPanel.ClientRectangle;
    RecalculateSelectedBounds();
  }

  #region Painting and rendering
  private void renderPanel_RenderBackground(object sender, EventArgs e)
  {
    Engine.Engine.ResetOpenGL(renderPanel.Width, renderPanel.Height, renderPanel.ClientRectangle);
    sceneView.Invalidate();
    desktop.Render();
  }

  private void renderPanel_Paint(object sender, PaintEventArgs e)
  {
    if(selectedObjects.Count != 0)
    {
      Point[] scenePoints = new Point[5];
      GLPoint[] objPoints = new GLPoint[4];

      if(selectedObjects.Count > 1)
      {
        // draw boxes around selected items
        foreach(SceneObject obj in selectedObjects)
        {
          obj.GetRotatedArea().CopyTo(objPoints, 0); // copy the objects rotated bounding box into the point array
          for(int i=0; i<4; i++)
          {
            scenePoints[i] = sceneView.SceneToClient(objPoints[i]); // transform from scene space to control space
          }
          scenePoints[4] = scenePoints[0]; // form a loop
          
          e.Graphics.DrawLines(Pens.LightGray, scenePoints);
        }
      }

      // draw the control rectangle around the selected items
      e.Graphics.DrawRectangle(Pens.White, GetHandleBorder());
      foreach(Handle handle in GetHandles())
      {
        DrawControlHandle(e.Graphics, handle);
      }
    }
  }
  
  void DrawControlHandle(Graphics g, Handle handle)
  {
    Rectangle rect = GetHandleRect(handle);
    g.FillRectangle(Brushes.Blue, rect);
    g.DrawRectangle(Pens.White, rect);
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

  void SetCursor()
  {
    SetCursor(renderPanel.PointToClient(Control.MousePosition));
  }

  void SetCursor(Point pt)
  {
    if(!renderPanel.ClientRectangle.Contains(pt)) return;

    if(selectedObjects.Count != 0)
    {
      Handle handle = GetHandleUnderPoint(pt);
      if(handle != Handle.None)
      {
        if(Control.ModifierKeys == Keys.Control)
        {
          renderPanel.Cursor = Cursors.Hand; // cursor for rotation
        }
        else
        {
          switch(handle)
          {
            case Handle.Top: case Handle.Bottom:
              renderPanel.Cursor = Cursors.SizeNS;
              return;
            case Handle.Left: case Handle.Right:
              renderPanel.Cursor = Cursors.SizeWE;
              return;
            case Handle.TopLeft: case Handle.BottomRight:
              renderPanel.Cursor = Cursors.SizeNWSE;
              return;
            case Handle.TopRight: case Handle.BottomLeft:
              renderPanel.Cursor = Cursors.SizeNESW;
              return;
          }
        }
      }
      
      // check if the cursor is over the selected rectangle
      if(GetHandleBorder().Contains(pt))
      {
        if(Control.ModifierKeys == Keys.Control)
        {
          renderPanel.Cursor = Cursors.Hand; // cursor for rotation
        }
        else
        {
          renderPanel.Cursor = Cursors.SizeAll;
        }
        return;
      }
    }

    renderPanel.Cursor = Cursors.Default;
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
