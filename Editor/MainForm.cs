using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RotationalForce.Editor
{

sealed class MainForm : Form
{
  private ToolStripMenuItem saveMenuItem;
  private ToolStripMenuItem saveAsMenuItem;
  private ToolStripMenuItem tileHorzMenuItem;
  private ToolStripMenuItem tileVertMenuItem;
  private ToolStripMenuItem cascadeMenuItem;
  private ToolStripMenuItem newVectorAnimationMenuItem;
  private ToolStripMenuItem openVectorAnimationMenuItem;
  private ToolStripStatusLabel statusLabel;
  private StatusStrip statusBar;
  private ToolStripMenuItem saveAllMenuItem;

  public MainForm()
  {
    InitializeComponent();
  }

  public string StatusText
  {
    get { return statusLabel.Text; }
    set { statusLabel.Text = value; }
  }

  private void NewLevel()
  {
    SceneEditor form = new SceneEditor();
    form.CreateNew();
    form.MdiParent = this;
    form.Show();
  }

  private void NewObject()
  {
    VectorAnimationEditor form = new VectorAnimationEditor();
    form.CreateNew();
    form.MdiParent = this;
    form.Show();
  }

  private void TryToExit()
  {
    Close();
  }

  protected override void OnMdiChildActivate(EventArgs e)
  {
    base.OnMdiChildActivate(e);

    ToolStripManager.RevertMerge(statusBar);

    SceneEditor sceneEditor = ActiveMdiChild as SceneEditor;
    if(sceneEditor != null)
    {
      ToolStripManager.Merge(sceneEditor.StatusBar, statusBar);
    }
  }

  #region InitializeComponent
  private void InitializeComponent()
  {
    System.Windows.Forms.ToolStripMenuItem newMenuItem;
    System.Windows.Forms.ToolStripMenuItem newLevelMenuItem;
    System.Windows.Forms.ToolStripMenuItem openMenuItem;
    System.Windows.Forms.ToolStripMenuItem openLevelMenuItem;
    System.Windows.Forms.ToolStripSeparator menuSep1;
    System.Windows.Forms.ToolStripSeparator menuSep2;
    System.Windows.Forms.ToolStripMenuItem exitMenuItem;
    System.Windows.Forms.MenuStrip mainMenuBar;
    System.Windows.Forms.ToolStripMenuItem fileMenu;
    System.Windows.Forms.ToolStripMenuItem windowMenu;
    System.Windows.Forms.ToolStripSeparator windowsSep;
    this.newVectorAnimationMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.openVectorAnimationMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.tileHorzMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.tileVertMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.cascadeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.statusBar = new System.Windows.Forms.StatusStrip();
    this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
    newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    newLevelMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    openLevelMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    menuSep1 = new System.Windows.Forms.ToolStripSeparator();
    menuSep2 = new System.Windows.Forms.ToolStripSeparator();
    exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    mainMenuBar = new System.Windows.Forms.MenuStrip();
    fileMenu = new System.Windows.Forms.ToolStripMenuItem();
    windowMenu = new System.Windows.Forms.ToolStripMenuItem();
    windowsSep = new System.Windows.Forms.ToolStripSeparator();
    mainMenuBar.SuspendLayout();
    this.statusBar.SuspendLayout();
    this.SuspendLayout();
    // 
    // newMenuItem
    // 
    newMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            newLevelMenuItem,
            this.newVectorAnimationMenuItem});
    newMenuItem.Name = "newMenuItem";
    newMenuItem.Size = new System.Drawing.Size(179, 22);
    newMenuItem.Text = "&New";
    // 
    // newLevelMenuItem
    // 
    newLevelMenuItem.Name = "newLevelMenuItem";
    newLevelMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
    newLevelMenuItem.Size = new System.Drawing.Size(224, 22);
    newLevelMenuItem.Text = "&Level";
    newLevelMenuItem.Click += new System.EventHandler(this.newLevelMenuItem_Click);
    // 
    // newVectorAnimationMenuItem
    // 
    this.newVectorAnimationMenuItem.Name = "newVectorAnimationMenuItem";
    this.newVectorAnimationMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
                | System.Windows.Forms.Keys.N)));
    this.newVectorAnimationMenuItem.Size = new System.Drawing.Size(224, 22);
    this.newVectorAnimationMenuItem.Text = "&Vector Animation";
    this.newVectorAnimationMenuItem.Click += new System.EventHandler(this.newObjectMenuItem_Click);
    // 
    // openMenuItem
    // 
    openMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            openLevelMenuItem,
            this.openVectorAnimationMenuItem});
    openMenuItem.Name = "openMenuItem";
    openMenuItem.Size = new System.Drawing.Size(179, 22);
    openMenuItem.Text = "&Open";
    // 
    // openLevelMenuItem
    // 
    openLevelMenuItem.Name = "openLevelMenuItem";
    openLevelMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
    openLevelMenuItem.Size = new System.Drawing.Size(237, 22);
    openLevelMenuItem.Text = "&Level...";
    // 
    // openVectorAnimationMenuItem
    // 
    this.openVectorAnimationMenuItem.Name = "openVectorAnimationMenuItem";
    this.openVectorAnimationMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
                | System.Windows.Forms.Keys.O)));
    this.openVectorAnimationMenuItem.Size = new System.Drawing.Size(237, 22);
    this.openVectorAnimationMenuItem.Text = "&Vector Animation...";
    // 
    // menuSep1
    // 
    menuSep1.Name = "menuSep1";
    menuSep1.Size = new System.Drawing.Size(176, 6);
    // 
    // menuSep2
    // 
    menuSep2.Name = "menuSep2";
    menuSep2.Size = new System.Drawing.Size(176, 6);
    // 
    // exitMenuItem
    // 
    exitMenuItem.Name = "exitMenuItem";
    exitMenuItem.Size = new System.Drawing.Size(179, 22);
    exitMenuItem.Text = "E&xit";
    exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
    // 
    // mainMenuBar
    // 
    mainMenuBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            fileMenu,
            windowMenu});
    mainMenuBar.Location = new System.Drawing.Point(0, 0);
    mainMenuBar.Name = "mainMenuBar";
    mainMenuBar.Size = new System.Drawing.Size(772, 24);
    mainMenuBar.TabIndex = 0;
    // 
    // fileMenu
    // 
    fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            newMenuItem,
            openMenuItem,
            menuSep1,
            this.saveMenuItem,
            this.saveAsMenuItem,
            this.saveAllMenuItem,
            menuSep2,
            exitMenuItem});
    fileMenu.Name = "fileMenu";
    fileMenu.Size = new System.Drawing.Size(35, 20);
    fileMenu.Text = "&File";
    // 
    // saveMenuItem
    // 
    this.saveMenuItem.Name = "saveMenuItem";
    this.saveMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
    this.saveMenuItem.Size = new System.Drawing.Size(179, 22);
    this.saveMenuItem.Text = "&Save";
    // 
    // saveAsMenuItem
    // 
    this.saveAsMenuItem.Name = "saveAsMenuItem";
    this.saveAsMenuItem.Size = new System.Drawing.Size(179, 22);
    this.saveAsMenuItem.Text = "Save &as...";
    // 
    // saveAllMenuItem
    // 
    this.saveAllMenuItem.Name = "saveAllMenuItem";
    this.saveAllMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
                | System.Windows.Forms.Keys.S)));
    this.saveAllMenuItem.Size = new System.Drawing.Size(179, 22);
    this.saveAllMenuItem.Text = "Save a&ll";
    // 
    // windowMenu
    // 
    windowMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tileHorzMenuItem,
            this.tileVertMenuItem,
            this.cascadeMenuItem,
            windowsSep});
    windowMenu.Name = "windowMenu";
    windowMenu.Size = new System.Drawing.Size(57, 20);
    windowMenu.Text = "&Window";
    // 
    // tileHorzMenuItem
    // 
    this.tileHorzMenuItem.Name = "tileHorzMenuItem";
    this.tileHorzMenuItem.Size = new System.Drawing.Size(149, 22);
    this.tileHorzMenuItem.Text = "Tile Horizontally";
    // 
    // tileVertMenuItem
    // 
    this.tileVertMenuItem.Name = "tileVertMenuItem";
    this.tileVertMenuItem.Size = new System.Drawing.Size(149, 22);
    this.tileVertMenuItem.Text = "Tile Vertically";
    // 
    // cascadeMenuItem
    // 
    this.cascadeMenuItem.Name = "cascadeMenuItem";
    this.cascadeMenuItem.Size = new System.Drawing.Size(149, 22);
    this.cascadeMenuItem.Text = "Cascade";
    // 
    // windowsSep
    // 
    windowsSep.Name = "windowsSep";
    windowsSep.Size = new System.Drawing.Size(146, 6);
    // 
    // statusBar
    // 
    this.statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
    this.statusBar.Location = new System.Drawing.Point(0, 526);
    this.statusBar.Name = "statusBar";
    this.statusBar.Size = new System.Drawing.Size(772, 22);
    this.statusBar.TabIndex = 1;
    // 
    // statusLabel
    // 
    this.statusLabel.AutoSize = false;
    this.statusLabel.Name = "statusLabel";
    this.statusLabel.Size = new System.Drawing.Size(726, 17);
    this.statusLabel.Spring = true;
    this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
    // 
    // MainForm
    // 
    this.ClientSize = new System.Drawing.Size(772, 548);
    this.Controls.Add(this.statusBar);
    this.Controls.Add(mainMenuBar);
    this.IsMdiContainer = true;
    this.MainMenuStrip = mainMenuBar;
    this.Name = "MainForm";
    this.Text = "RotationalForce Editor";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
    mainMenuBar.ResumeLayout(false);
    mainMenuBar.PerformLayout();
    this.statusBar.ResumeLayout(false);
    this.statusBar.PerformLayout();
    this.ResumeLayout(false);
    this.PerformLayout();

  }
  #endregion

  #region Menu handlers
  private void exitMenuItem_Click(object sender, EventArgs e)
  {
    TryToExit();
  }

  private void newObjectMenuItem_Click(object sender, EventArgs e)
  {
    NewObject();
  }

  private void newLevelMenuItem_Click(object sender, EventArgs e)
  {
    NewLevel();
  }
  #endregion
}

} // namespace RotationalForce.Editor