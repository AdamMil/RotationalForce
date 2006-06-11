using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RotationalForce.Engine;
using GameLib.Interop.OpenGL;
using GLPoint = GameLib.Mathematics.TwoD.Point;
using GLRect  = GameLib.Mathematics.TwoD.Rectangle;
using GLPoly  = GameLib.Mathematics.TwoD.Polygon;
using Vector  = GameLib.Mathematics.TwoD.Vector;

namespace RotationalForce.Editor
{

public class SceneEditor : Form
{
  const string TriggerName = "__trigger__"; // public so that the designer can access it.
  const int VertexRadius = 2;
  
  private ToolStrip toolBar;
  private ToolStripMenuItem editMenu;
  private SplitContainer rightPane;
  private ToolStrip objToolBar;
  private ToolboxList objectList;
  private ToolStripButton newVectorAnim;
  private ImageList objectImgs;
  private System.ComponentModel.IContainer components;
  private ToolStripButton collisionTool;
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

  GLPoint ControlToScene(Point clientPoint)
  {
    return sceneView.ClientToScene(clientPoint);
  }

  Point SceneToControl(GLPoint scenePoint)
  {
    return sceneView.SceneToClient(scenePoint);
  }

  void InvalidateDecoration(Rectangle rect)
  {
    renderPanel.Invalidate(rect);
  }

  void InvalidateRender(Rectangle rect)
  {
    sceneView.Invalidate(rect);
  }

  void InvalidateObjectBounds(SceneObject obj, bool invalidateRender)
  {
    Rectangle controlRect = sceneView.SceneToClient(obj.GetRotatedArea().GetBounds()); // get the client rectangle
    controlRect.Inflate(VertexRadius*2+1, VertexRadius*2+1); // include space for our decoration
    if(invalidateRender)
    {
      InvalidateRender(controlRect);
    }
    else
    {
      InvalidateDecoration(controlRect);
    }
  }

  #region Object selection
  void DeselectObjects()
  {
    if(selectedObjects.Count != 0)
    {
      foreach(SceneObject selectedObj in selectedObjects)
      {
        InvalidateObjectBounds(selectedObj, false);
      }
      selectedObjects.Clear();
    }
  }

  void SelectObject(SceneObject obj, bool deselectOthers)
  {
    if(obj == null) throw new ArgumentNullException();

    if(deselectOthers)
    {
      DeselectObjects();
      selectedObjects.Add(obj);
      InvalidateObjectBounds(obj, false);
    }
    else if(!selectedObjects.Contains(obj))
    {
      selectedObjects.Add(obj);
      InvalidateObjectBounds(obj, false);
    }
  }

  List<SceneObject> selectedObjects = new List<SceneObject>();
  #endregion

  DesktopControl desktop;
  SceneViewControl sceneView;
  Scene scene;

  #region InitializeComponent
  private void InitializeComponent()
  {
    this.components = new System.ComponentModel.Container();
    System.Windows.Forms.MenuStrip menuBar;
    System.Windows.Forms.ToolStripButton newStaticImg;
    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SceneEditor));
    System.Windows.Forms.ToolStripButton newAnimatedImg;
    System.Windows.Forms.ToolStripButton deleteItem;
    System.Windows.Forms.ToolStripButton selectTool;
    System.Windows.Forms.ToolStripButton layerTool;
    System.Windows.Forms.ToolStripButton cameraTool;
    System.Windows.Forms.ToolStripButton terrainTool;
    System.Windows.Forms.ToolStripButton mountTool;
    System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Static Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Animated Images", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Vector Animations", System.Windows.Forms.HorizontalAlignment.Left);
    System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Miscellaneous", System.Windows.Forms.HorizontalAlignment.Left);
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.renderPanel = new RotationalForce.Editor.RenderPanel();
    this.toolBar = new System.Windows.Forms.ToolStrip();
    this.collisionTool = new System.Windows.Forms.ToolStripButton();
    this.rightPane = new System.Windows.Forms.SplitContainer();
    this.objToolBar = new System.Windows.Forms.ToolStrip();
    this.newVectorAnim = new System.Windows.Forms.ToolStripButton();
    this.objectList = new RotationalForce.Editor.ToolboxList();
    this.objectImgs = new System.Windows.Forms.ImageList(this.components);
    menuBar = new System.Windows.Forms.MenuStrip();
    newStaticImg = new System.Windows.Forms.ToolStripButton();
    newAnimatedImg = new System.Windows.Forms.ToolStripButton();
    deleteItem = new System.Windows.Forms.ToolStripButton();
    selectTool = new System.Windows.Forms.ToolStripButton();
    layerTool = new System.Windows.Forms.ToolStripButton();
    cameraTool = new System.Windows.Forms.ToolStripButton();
    terrainTool = new System.Windows.Forms.ToolStripButton();
    mountTool = new System.Windows.Forms.ToolStripButton();
    menuBar.SuspendLayout();
    this.renderPanel.SuspendLayout();
    this.toolBar.SuspendLayout();
    this.rightPane.Panel1.SuspendLayout();
    this.rightPane.SuspendLayout();
    this.objToolBar.SuspendLayout();
    this.SuspendLayout();
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
    this.renderPanel.DragDrop += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragDrop);
    this.renderPanel.DragEnter += new System.Windows.Forms.DragEventHandler(this.renderPanel_DragEnter);
    this.renderPanel.Resize += new System.EventHandler(this.renderPanel_Resize);
    this.renderPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.renderPanel_Paint);
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
    // SceneEditor
    // 
    this.ClientSize = new System.Drawing.Size(772, 523);
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
    this.renderPanel.ResumeLayout(false);
    this.renderPanel.PerformLayout();
    this.toolBar.ResumeLayout(false);
    this.toolBar.PerformLayout();
    this.rightPane.Panel1.ResumeLayout(false);
    this.rightPane.ResumeLayout(false);
    this.objToolBar.ResumeLayout(false);
    this.objToolBar.PerformLayout();
    this.ResumeLayout(false);

  }
  #endregion

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
      obj.Position = ControlToScene(renderPanel.PointToClient(new Point(e.X, e.Y)));
      scene.AddObject(obj);
      SelectObject(obj, true);
      InvalidateObjectBounds(obj, true);
    }
  }

  private void renderPanel_Resize(object sender, EventArgs e)
  {
    sceneView.Bounds = desktop.Bounds = renderPanel.ClientRectangle;
  }

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

      // draw boxes around selected items
      foreach(SceneObject obj in selectedObjects)
      {
        obj.GetRotatedArea().CopyTo(objPoints, 0); // copy the objects rotated bounding box into the point array
        for(int i=0; i<4; i++)
        {
          scenePoints[i] = SceneToControl(objPoints[i]); // transform from scene space to control space
        }
        scenePoints[4] = scenePoints[0]; // form a loop
        
        e.Graphics.DrawLines(Pens.White, scenePoints);
      }
    }
  }
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
