using System;
using System.Drawing;
using System.Windows.Forms;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{
  public partial class SceneEditor : Form
  {
    private ToolStrip toolBar;
    private TabControl tabControl;
    private ToolStripMenuItem editMenu;
    private TabPage staticTab;
    private TabPage animImgTab;
    private SplitContainer rightPane;
    private TabPage vectAnimTab;
    private TabPage miscTab;
    private Panel renderPanel;
  
    public SceneEditor()
    {
      InitializeComponent();
    }

    public void CreateNew()
    {
      scene = new Scene();
    }

    Scene scene;

    #region InitializeComponent
    private void InitializeComponent()
    {
      System.Windows.Forms.MenuStrip menuBar;
      System.Windows.Forms.ToolStripButton selectTool;
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SceneEditor));
      System.Windows.Forms.ToolStripButton layerTool;
      System.Windows.Forms.ToolStripButton mountTool;
      System.Windows.Forms.ToolStripButton cameraTool;
      this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
      this.renderPanel = new System.Windows.Forms.Panel();
      this.toolBar = new System.Windows.Forms.ToolStrip();
      this.tabControl = new System.Windows.Forms.TabControl();
      this.staticTab = new System.Windows.Forms.TabPage();
      this.animImgTab = new System.Windows.Forms.TabPage();
      this.vectAnimTab = new System.Windows.Forms.TabPage();
      this.miscTab = new System.Windows.Forms.TabPage();
      this.rightPane = new System.Windows.Forms.SplitContainer();
      menuBar = new System.Windows.Forms.MenuStrip();
      selectTool = new System.Windows.Forms.ToolStripButton();
      layerTool = new System.Windows.Forms.ToolStripButton();
      mountTool = new System.Windows.Forms.ToolStripButton();
      cameraTool = new System.Windows.Forms.ToolStripButton();
      menuBar.SuspendLayout();
      this.renderPanel.SuspendLayout();
      this.toolBar.SuspendLayout();
      this.tabControl.SuspendLayout();
      this.rightPane.Panel1.SuspendLayout();
      this.rightPane.SuspendLayout();
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
      // selectTool
      // 
      selectTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      selectTool.Image = ((System.Drawing.Image)(resources.GetObject("selectTool.Image")));
      selectTool.ImageTransparentColor = System.Drawing.Color.Magenta;
      selectTool.Name = "selectTool";
      selectTool.Size = new System.Drawing.Size(23, 20);
      selectTool.Text = "Select";
      selectTool.ToolTipText = "Use this tool to select and manipulate objects.";
      // 
      // layerTool
      // 
      layerTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      layerTool.Image = ((System.Drawing.Image)(resources.GetObject("layerTool.Image")));
      layerTool.ImageTransparentColor = System.Drawing.Color.Magenta;
      layerTool.Name = "layerTool";
      layerTool.Size = new System.Drawing.Size(23, 20);
      layerTool.Text = "Layers";
      layerTool.ToolTipText = "Use this tool to show and hide layers.";
      // 
      // mountTool
      // 
      mountTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      mountTool.Image = ((System.Drawing.Image)(resources.GetObject("mountTool.Image")));
      mountTool.ImageTransparentColor = System.Drawing.Color.Magenta;
      mountTool.Name = "mountTool";
      mountTool.Size = new System.Drawing.Size(23, 20);
      mountTool.Text = "Mount Points";
      mountTool.ToolTipText = "Use this tool to set the mount points of the selected object.";
      // 
      // cameraTool
      // 
      cameraTool.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      cameraTool.Image = ((System.Drawing.Image)(resources.GetObject("cameraTool.Image")));
      cameraTool.ImageTransparentColor = System.Drawing.Color.Magenta;
      cameraTool.Name = "cameraTool";
      cameraTool.Size = new System.Drawing.Size(23, 20);
      cameraTool.Text = "Camera";
      cameraTool.ToolTipText = "Use this tool to zoom and place the camera.";
      // 
      // renderPanel
      // 
      this.renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                  | System.Windows.Forms.AnchorStyles.Left)
                  | System.Windows.Forms.AnchorStyles.Right)));
      this.renderPanel.BackColor = System.Drawing.Color.Black;
      this.renderPanel.Controls.Add(menuBar);
      this.renderPanel.Location = new System.Drawing.Point(2, 3);
      this.renderPanel.Name = "renderPanel";
      this.renderPanel.Size = new System.Drawing.Size(552, 518);
      this.renderPanel.TabIndex = 0;
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
            mountTool});
      this.toolBar.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
      this.toolBar.Location = new System.Drawing.Point(561, 3);
      this.toolBar.Name = "toolBar";
      this.toolBar.Size = new System.Drawing.Size(206, 25);
      this.toolBar.TabIndex = 1;
      // 
      // tabControl
      // 
      this.tabControl.Controls.Add(this.staticTab);
      this.tabControl.Controls.Add(this.animImgTab);
      this.tabControl.Controls.Add(this.vectAnimTab);
      this.tabControl.Controls.Add(this.miscTab);
      this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
      this.tabControl.Location = new System.Drawing.Point(0, 0);
      this.tabControl.Name = "tabControl";
      this.tabControl.SelectedIndex = 0;
      this.tabControl.Size = new System.Drawing.Size(207, 490);
      this.tabControl.TabIndex = 2;
      // 
      // staticTab
      // 
      this.staticTab.Location = new System.Drawing.Point(4, 22);
      this.staticTab.Name = "staticTab";
      this.staticTab.Size = new System.Drawing.Size(199, 464);
      this.staticTab.TabIndex = 0;
      this.staticTab.Text = "Static";
      this.staticTab.UseVisualStyleBackColor = true;
      // 
      // animImgTab
      // 
      this.animImgTab.Location = new System.Drawing.Point(4, 22);
      this.animImgTab.Name = "animImgTab";
      this.animImgTab.Size = new System.Drawing.Size(199, 464);
      this.animImgTab.TabIndex = 1;
      this.animImgTab.Text = "AnimImg";
      this.animImgTab.UseVisualStyleBackColor = true;
      // 
      // vectAnimTab
      // 
      this.vectAnimTab.Location = new System.Drawing.Point(4, 22);
      this.vectAnimTab.Name = "vectAnimTab";
      this.vectAnimTab.Size = new System.Drawing.Size(199, 464);
      this.vectAnimTab.TabIndex = 2;
      this.vectAnimTab.Text = "VectAnim";
      this.vectAnimTab.UseVisualStyleBackColor = true;
      // 
      // miscTab
      // 
      this.miscTab.Location = new System.Drawing.Point(4, 22);
      this.miscTab.Name = "miscTab";
      this.miscTab.Size = new System.Drawing.Size(199, 464);
      this.miscTab.TabIndex = 3;
      this.miscTab.Text = "Misc";
      this.miscTab.UseVisualStyleBackColor = true;
      // 
      // rightPane
      // 
      this.rightPane.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                  | System.Windows.Forms.AnchorStyles.Right)));
      this.rightPane.Location = new System.Drawing.Point(560, 31);
      this.rightPane.Name = "rightPane";
      this.rightPane.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // rightPane.Panel1
      // 
      this.rightPane.Panel1.Controls.Add(this.tabControl);
      this.rightPane.Panel2Collapsed = true;
      this.rightPane.Size = new System.Drawing.Size(207, 490);
      this.rightPane.SplitterDistance = 209;
      this.rightPane.TabIndex = 3;
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
      this.tabControl.ResumeLayout(false);
      this.rightPane.Panel1.ResumeLayout(false);
      this.rightPane.ResumeLayout(false);
      this.ResumeLayout(false);

    }
    #endregion
  }
}