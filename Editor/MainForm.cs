using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{

#region MainForm
sealed class MainForm : Form
{
  const string WindowTitle = "RotationalForce Editor";
  
  ToolStripMenuItem saveMenuItem;
  ToolStripMenuItem saveAsMenuItem;
  ToolStripMenuItem tileHorzMenuItem;
  ToolStripMenuItem tileVertMenuItem;
  ToolStripMenuItem cascadeMenuItem;
  ToolStripStatusLabel statusLabel;
  StatusStrip statusBar;
  ToolStripMenuItem closeMenuItem;
  ToolStripMenuItem closeProjectMenuItem;
  ToolStripMenuItem newLevelMenuItem;
  ToolStripMenuItem openLevelMenuItem;
  ToolStripMenuItem newProjectMenuItem;
  ToolStripMenuItem openProjectMenuItem;
  ToolStripMenuItem saveAllMenuItem;

  public MainForm()
  {
    InitializeComponent();
    UpdateTitle();
  }

  public Project Project
  {
    get { return project; }
  }

  public string StatusText
  {
    get { return statusLabel.Text; }
    set { statusLabel.Text = value; }
  }

  void NewLevel()
  {
    SceneEditor form = new SceneEditor();
    form.CreateNew();
    form.MdiParent = this;
    form.Show();
  }

  void NewProject()
  {
    if(TryCloseProject())
    {
      FolderBrowserDialog fb = new FolderBrowserDialog();
      fb.Description  = "Select the project root. Project files will be stored in subdirectories beneath the root.";
      fb.SelectedPath = Environment.CurrentDirectory;

      if(fb.ShowDialog() == DialogResult.OK)
      {
        try
        {
          project = Project.Create(fb.SelectedPath);
          OnProjectChanged();
        }
        catch
        {
          MessageBox.Show("Unable to create project. Check that the path is correct and you have write access.",
                          "Project creation failures.", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }
  }

  void OnProjectChanged()
  {
    UpdateTitle();
    UpdateFileMenu();

    ToolboxItem.ClearItems();

    if(project == null)
    {
      Engine.Engine.Deinitialize();
    }
    else
    {
      Engine.Engine.Initialize(new Engine.StandardFileSystem(project.EngineDataPath, project.EditorDataPath), true);

      ToolboxItem.SetItem(new TriggerItem());
      foreach(ResourceHandle<ImageMap> map in Engine.Engine.GetResources<ImageMap>())
      {
        ToolboxItem.SetItem(new StaticImageItem(map.Resource));
      }
    }
  }

  void OpenLevel()
  {
    SceneEditor form = new SceneEditor();
    if(form.Open())
    {
      form.MdiParent = this;
      form.Show();
    }
  }

  void OpenProject()
  {
    if(TryCloseProject())
    {
      OpenFileDialog fd = new OpenFileDialog();
      fd.DefaultExt  = "xml";
      fd.FileName    = "rfproject.xml";
      fd.Filter      = "Project files (rfproject.xml)|rfproject.xml";
      
      if(fd.ShowDialog() == DialogResult.OK)
      {
        project = Project.Load(Path.GetDirectoryName(Path.GetFullPath(fd.FileName)));
        OnProjectChanged();
      }
    }
  }

  void Save()
  {
    IEditorForm form = (IEditorForm)ActiveMdiChild;
    form.Save(false);
  }

  void SaveAs()
  {
    IEditorForm form = (IEditorForm)ActiveMdiChild;
    form.Save(true);
  }

  bool TryCloseWindow()
  {
    return ((IEditorForm)ActiveMdiChild).TryClose();
  }
  
  bool TryCloseAllWindows()
  {
    foreach(Form form in MdiChildren)
    {
      IEditorForm editor = form as IEditorForm;
      if(editor != null && !editor.TryClose())
      {
        return false;
      }
    }
    
    return true;
  }

  bool TryCloseProject()
  {
    if(project != null && !TryCloseAllWindows())
    {
      return false;
    }
    else
    {
      if(project != null)
      {
        project.Save();
        project = null;
        OnProjectChanged();
      }
      return true;
    }
  }

  bool TryExit()
  {
    if(TryCloseProject())
    {
      Application.Exit();
      return true;
    }

    return false;
  }

  void UpdateFileMenu()
  {
    bool isProjectOpen  = project != null;
    bool isEditorActive = ActiveMdiChild is IEditorForm;

    saveMenuItem.Enabled = saveAsMenuItem.Enabled = closeMenuItem.Enabled = isEditorActive;

    closeProjectMenuItem.Enabled = newLevelMenuItem.Enabled = openLevelMenuItem.Enabled =
      saveAllMenuItem.Enabled = isProjectOpen;
  }

  void UpdateTitle()
  {
    Text = project == null ? WindowTitle
                           : WindowTitle + " - " + Path.GetFileName(Path.GetDirectoryName(project.BasePath));
  }

  protected override void OnClosing(CancelEventArgs e)
  {
    if(!TryCloseProject())
    {
      e.Cancel = true;
    }
  }
  
  protected override void OnMdiChildActivate(EventArgs e)
  {
    base.OnMdiChildActivate(e);

    ToolStripManager.RevertMerge(statusBar);

    IEditorForm editor = ActiveMdiChild as IEditorForm;
    ToolStrip editorStatusBar = editor == null ? null : editor.StatusBar;
    if(editorStatusBar != null)
    {
      statusLabel.Spring = false;
      statusLabel.Width  = 1;
      ToolStripManager.Merge(editorStatusBar, this.statusBar);
      statusLabel.Spring = true;
    }

    UpdateFileMenu();
  }

  #region InitializeComponent
  void InitializeComponent()
  {
    System.Windows.Forms.ToolStripMenuItem newMenuItem;
    System.Windows.Forms.ToolStripMenuItem openMenuItem;
    System.Windows.Forms.ToolStripSeparator menuSep1;
    System.Windows.Forms.ToolStripSeparator menuSep2;
    System.Windows.Forms.ToolStripMenuItem exitMenuItem;
    System.Windows.Forms.MenuStrip mainMenuBar;
    System.Windows.Forms.ToolStripMenuItem fileMenu;
    System.Windows.Forms.ToolStripSeparator menuSep4;
    System.Windows.Forms.ToolStripMenuItem windowMenu;
    System.Windows.Forms.ToolStripSeparator windowsSep;
    this.newProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.newLevelMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.openProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.openLevelMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.closeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.closeProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveAsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.saveAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.tileHorzMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.tileVertMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.cascadeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.statusBar = new System.Windows.Forms.StatusStrip();
    this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
    newMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    menuSep1 = new System.Windows.Forms.ToolStripSeparator();
    menuSep2 = new System.Windows.Forms.ToolStripSeparator();
    exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    mainMenuBar = new System.Windows.Forms.MenuStrip();
    fileMenu = new System.Windows.Forms.ToolStripMenuItem();
    menuSep4 = new System.Windows.Forms.ToolStripSeparator();
    windowMenu = new System.Windows.Forms.ToolStripMenuItem();
    windowsSep = new System.Windows.Forms.ToolStripSeparator();
    mainMenuBar.SuspendLayout();
    this.statusBar.SuspendLayout();
    this.SuspendLayout();
    // 
    // newMenuItem
    // 
    newMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newProjectMenuItem,
            this.newLevelMenuItem});
    newMenuItem.Name = "newMenuItem";
    newMenuItem.Size = new System.Drawing.Size(179, 22);
    newMenuItem.Text = "&New";
    // 
    // newProjectMenuItem
    // 
    this.newProjectMenuItem.Name = "newProjectMenuItem";
    this.newProjectMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.N)));
    this.newProjectMenuItem.Size = new System.Drawing.Size(177, 22);
    this.newProjectMenuItem.Text = "&Project";
    this.newProjectMenuItem.Click += new System.EventHandler(this.newProjectMenuItem_Click);
    // 
    // newLevelMenuItem
    // 
    this.newLevelMenuItem.Enabled = false;
    this.newLevelMenuItem.Name = "newLevelMenuItem";
    this.newLevelMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
    this.newLevelMenuItem.Size = new System.Drawing.Size(177, 22);
    this.newLevelMenuItem.Text = "&Level";
    this.newLevelMenuItem.Click += new System.EventHandler(this.newLevelMenuItem_Click);
    // 
    // openMenuItem
    // 
    openMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openProjectMenuItem,
            this.openLevelMenuItem});
    openMenuItem.Name = "openMenuItem";
    openMenuItem.Size = new System.Drawing.Size(179, 22);
    openMenuItem.Text = "&Open";
    // 
    // openProjectMenuItem
    // 
    this.openProjectMenuItem.Name = "openProjectMenuItem";
    this.openProjectMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.O)));
    this.openProjectMenuItem.Size = new System.Drawing.Size(190, 22);
    this.openProjectMenuItem.Text = "&Project...";
    this.openProjectMenuItem.Click += new System.EventHandler(this.openProjectMenuItem_Click);
    // 
    // openLevelMenuItem
    // 
    this.openLevelMenuItem.Enabled = false;
    this.openLevelMenuItem.Name = "openLevelMenuItem";
    this.openLevelMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
    this.openLevelMenuItem.Size = new System.Drawing.Size(190, 22);
    this.openLevelMenuItem.Text = "&Level...";
    this.openLevelMenuItem.Click += new System.EventHandler(this.openLevelMenuItem_Click);
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
    exitMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
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
            this.closeMenuItem,
            this.closeProjectMenuItem,
            menuSep4,
            this.saveMenuItem,
            this.saveAsMenuItem,
            this.saveAllMenuItem,
            menuSep2,
            exitMenuItem});
    fileMenu.Name = "fileMenu";
    fileMenu.Size = new System.Drawing.Size(35, 20);
    fileMenu.Text = "&File";
    // 
    // closeMenuItem
    // 
    this.closeMenuItem.Enabled = false;
    this.closeMenuItem.Name = "closeMenuItem";
    this.closeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F4)));
    this.closeMenuItem.Size = new System.Drawing.Size(179, 22);
    this.closeMenuItem.Text = "&Close";
    this.closeMenuItem.Click += new System.EventHandler(this.closeMenuItem_Click);
    // 
    // closeProjectMenuItem
    // 
    this.closeProjectMenuItem.Enabled = false;
    this.closeProjectMenuItem.Name = "closeProjectMenuItem";
    this.closeProjectMenuItem.Size = new System.Drawing.Size(179, 22);
    this.closeProjectMenuItem.Text = "Close Project";
    this.closeProjectMenuItem.Click += new System.EventHandler(this.closeProjectMenuItem_Click);
    // 
    // menuSep4
    // 
    menuSep4.Name = "menuSep4";
    menuSep4.Size = new System.Drawing.Size(176, 6);
    // 
    // saveMenuItem
    // 
    this.saveMenuItem.Enabled = false;
    this.saveMenuItem.Name = "saveMenuItem";
    this.saveMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
    this.saveMenuItem.Size = new System.Drawing.Size(179, 22);
    this.saveMenuItem.Text = "&Save";
    this.saveMenuItem.Click += new System.EventHandler(this.saveMenuItem_Click);
    // 
    // saveAsMenuItem
    // 
    this.saveAsMenuItem.Enabled = false;
    this.saveAsMenuItem.Name = "saveAsMenuItem";
    this.saveAsMenuItem.Size = new System.Drawing.Size(179, 22);
    this.saveAsMenuItem.Text = "Save &as...";
    this.saveAsMenuItem.Click += new System.EventHandler(this.saveAsMenuItem_Click);
    // 
    // saveAllMenuItem
    // 
    this.saveAllMenuItem.Enabled = false;
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
    this.tileHorzMenuItem.Enabled = false;
    this.tileHorzMenuItem.Name = "tileHorzMenuItem";
    this.tileHorzMenuItem.Size = new System.Drawing.Size(149, 22);
    this.tileHorzMenuItem.Text = "Tile Horizontally";
    // 
    // tileVertMenuItem
    // 
    this.tileVertMenuItem.Enabled = false;
    this.tileVertMenuItem.Name = "tileVertMenuItem";
    this.tileVertMenuItem.Size = new System.Drawing.Size(149, 22);
    this.tileVertMenuItem.Text = "Tile Vertically";
    // 
    // cascadeMenuItem
    // 
    this.cascadeMenuItem.Enabled = false;
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
    this.statusLabel.Size = new System.Drawing.Size(757, 17);
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
  void exitMenuItem_Click(object sender, EventArgs e)
  {
    TryExit();
  }

  void newLevelMenuItem_Click(object sender, EventArgs e)
  {
    NewLevel();
  }

  private void openLevelMenuItem_Click(object sender, EventArgs e)
  {
    OpenLevel();
  }

  void newProjectMenuItem_Click(object sender, EventArgs e)
  {
    NewProject();
  }

  private void openProjectMenuItem_Click(object sender, EventArgs e)
  {
    OpenProject();
  }

  private void closeMenuItem_Click(object sender, EventArgs e)
  {
    TryCloseWindow();
  }

  private void closeProjectMenuItem_Click(object sender, EventArgs e)
  {
    TryCloseProject();
  }

  private void saveMenuItem_Click(object sender, EventArgs e)
  {
    Save();
  }

  private void saveAsMenuItem_Click(object sender, EventArgs e)
  {
    SaveAs();
  }
  #endregion

  Project project;
}
#endregion

interface IEditorForm
{
  bool HasUnsavedChanges
  {
    get;
  }
  
  StatusStrip StatusBar
  {
    get;
  }
  
  string Title
  {
    get;
  }

  void CreateNew();
  bool Open();
  bool Save(bool newFile);
  bool TryClose();
}

} // namespace RotationalForce.Editor