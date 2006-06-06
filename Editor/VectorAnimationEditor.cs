using System;
using System.Drawing;
using System.Windows.Forms;
using RotationalForce.Engine;

namespace RotationalForce.Editor
{

class VectorAnimationEditor : Form
{
  private MenuStrip menuBar;
  private ListBox listBox;
  private ToolStripMenuItem editMenu;
  private CheckBox chkAsk;
  private Button btnUp;
  private Button btnDown;
  private Button btnNew;
  private Button btnDelete;
  private PropertyGrid propertyGrid;

  public VectorAnimationEditor()
  {
    InitializeComponent();
  }

  public void CreateNew()
  {
    animation = new VectorObject.Animation();
    animation.AddFrame(new VectorObject.Frame());

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
            propertyGrid.SelectedObject = SelectedPolygon;
            break;
        }

        btnUp.Enabled = btnDown.Enabled = (value == EditMode.Animation || value == EditMode.Frame);
        btnNew.Enabled  = btnDelete.Enabled = listBox.Enabled = (value != EditMode.Vertex);

        mode = value;
        PopulateListbox(select);
      }
    }
  }

  VectorObject.Frame SelectedFrame
  {
    get { return animation.Frames[selectedFrame]; }
  }
  
  VectorObject.Polygon SelectedPolygon
  {
    get { return SelectedFrame.Polygons[selectedPolygon]; }
  }

  void MoveSelectedItem(int distance)
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
        VectorObject.Frame frame = animation.Frames[listBox.SelectedIndex];
        animation.RemoveFrame(listBox.SelectedIndex);
        animation.InsertFrame(listBox.SelectedIndex + distance, frame);
        PopulateListbox(listBox.SelectedIndex + distance);
        break;
      }

      case EditMode.Frame:
      {
        VectorObject.Polygon poly = SelectedFrame.Polygons[listBox.SelectedIndex];
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
        foreach(VectorObject.Frame frame in animation.Frames)
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

    if(select != -1 && listBox.Items.Count != 0)
    {
      listBox.SelectedIndex = select >= listBox.Items.Count ? listBox.Items.Count-1 : select;
    }
  }

  VectorObject.Animation animation;
  int selectedFrame, selectedPolygon = -1;
  EditMode mode;

  #region InitializeComponent
  private void InitializeComponent()
  {
    System.Windows.Forms.Panel renderPanel;
    System.Windows.Forms.ToolStripMenuItem frameModeMenuItem;
    System.Windows.Forms.ToolStripMenuItem polygonModeMenuItem;
    this.btnUp = new System.Windows.Forms.Button();
    this.btnDown = new System.Windows.Forms.Button();
    this.btnNew = new System.Windows.Forms.Button();
    this.btnDelete = new System.Windows.Forms.Button();
    this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
    this.menuBar = new System.Windows.Forms.MenuStrip();
    this.propertyGrid = new System.Windows.Forms.PropertyGrid();
    this.listBox = new System.Windows.Forms.ListBox();
    this.chkAsk = new System.Windows.Forms.CheckBox();
    renderPanel = new System.Windows.Forms.Panel();
    frameModeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    polygonModeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    this.menuBar.SuspendLayout();
    this.SuspendLayout();
    // 
    // renderPanel
    // 
    renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
    renderPanel.BackColor = System.Drawing.Color.Black;
    renderPanel.Location = new System.Drawing.Point(3, 3);
    renderPanel.Name = "renderPanel";
    renderPanel.Size = new System.Drawing.Size(465, 453);
    renderPanel.TabIndex = 1;
    // 
    // frameModeMenuItem
    // 
    frameModeMenuItem.Name = "frameModeMenuItem";
    frameModeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
    frameModeMenuItem.Size = new System.Drawing.Size(179, 22);
    frameModeMenuItem.Tag = RotationalForce.Editor.VectorAnimationEditor.EditMode.Frame;
    frameModeMenuItem.Text = "Frame Mode";
    frameModeMenuItem.Click += new System.EventHandler(this.editModeMenuItem_Click);
    // 
    // polygonModeMenuItem
    // 
    polygonModeMenuItem.Name = "polygonModeMenuItem";
    polygonModeMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
    polygonModeMenuItem.Size = new System.Drawing.Size(179, 22);
    polygonModeMenuItem.Tag = RotationalForce.Editor.VectorAnimationEditor.EditMode.Polygon;
    polygonModeMenuItem.Text = "Polygon Mode";
    polygonModeMenuItem.Click += new System.EventHandler(this.editModeMenuItem_Click);
    // 
    // btnUp
    // 
    this.btnUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnUp.Location = new System.Drawing.Point(671, 260);
    this.btnUp.Name = "btnUp";
    this.btnUp.Size = new System.Drawing.Size(48, 23);
    this.btnUp.TabIndex = 3;
    this.btnUp.Text = "&Up";
    this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
    // 
    // btnDown
    // 
    this.btnDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnDown.Location = new System.Drawing.Point(671, 289);
    this.btnDown.Name = "btnDown";
    this.btnDown.Size = new System.Drawing.Size(48, 23);
    this.btnDown.TabIndex = 4;
    this.btnDown.Text = "&Down";
    this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
    // 
    // btnNew
    // 
    this.btnNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnNew.Location = new System.Drawing.Point(671, 318);
    this.btnNew.Name = "btnNew";
    this.btnNew.Size = new System.Drawing.Size(48, 23);
    this.btnNew.TabIndex = 5;
    this.btnNew.Text = "&New";
    this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
    // 
    // btnDelete
    // 
    this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.btnDelete.Location = new System.Drawing.Point(671, 347);
    this.btnDelete.Name = "btnDelete";
    this.btnDelete.Size = new System.Drawing.Size(48, 23);
    this.btnDelete.TabIndex = 6;
    this.btnDelete.Text = "&Delete";
    this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
    // 
    // editMenu
    // 
    this.editMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            frameModeMenuItem,
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
    // 
    // listBox
    // 
    this.listBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.listBox.IntegralHeight = false;
    this.listBox.Location = new System.Drawing.Point(474, 260);
    this.listBox.Name = "listBox";
    this.listBox.Size = new System.Drawing.Size(191, 196);
    this.listBox.TabIndex = 2;
    this.listBox.DoubleClick += new System.EventHandler(this.listBox_DoubleClick);
    this.listBox.SelectedIndexChanged += new System.EventHandler(this.listBox_SelectedIndexChanged);
    // 
    // chkAsk
    // 
    this.chkAsk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
    this.chkAsk.Checked = true;
    this.chkAsk.CheckState = System.Windows.Forms.CheckState.Checked;
    this.chkAsk.Location = new System.Drawing.Point(671, 377);
    this.chkAsk.Name = "chkAsk";
    this.chkAsk.Size = new System.Drawing.Size(48, 17);
    this.chkAsk.TabIndex = 7;
    this.chkAsk.Text = "Ask";
    // 
    // VectorAnimationEditor
    // 
    this.ClientSize = new System.Drawing.Size(722, 458);
    this.Controls.Add(this.chkAsk);
    this.Controls.Add(this.listBox);
    this.Controls.Add(this.btnDelete);
    this.Controls.Add(this.btnNew);
    this.Controls.Add(this.btnDown);
    this.Controls.Add(this.btnUp);
    this.Controls.Add(this.propertyGrid);
    this.Controls.Add(renderPanel);
    this.Controls.Add(this.menuBar);
    this.MainMenuStrip = this.menuBar;
    this.Name = "VectorAnimationEditor";
    this.Text = "Vector Animation";
    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
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

  private void btnUp_Click(object sender, EventArgs e)
  {
    MoveSelectedItem(-1);
  }

  private void btnDown_Click(object sender, EventArgs e)
  {
    MoveSelectedItem(1);
  }

  private void listBox_SelectedIndexChanged(object sender, EventArgs e)
  {
    switch(Mode)
    {
      case EditMode.Animation:
        selectedFrame = listBox.SelectedIndex;
        break;
      case EditMode.Frame:
        selectedPolygon = listBox.SelectedIndex;
        break;
    }
  }

  private void btnNew_Click(object sender, EventArgs e)
  {
    switch(Mode)
    {
      case EditMode.Animation:
        animation.InsertFrame(listBox.SelectedIndex+1, new VectorObject.Frame());
        PopulateListbox(listBox.SelectedIndex + 1);
        break;
      case EditMode.Frame:
        SelectedFrame.InsertPolygon(listBox.SelectedIndex+1, new VectorObject.Polygon());
        PopulateListbox(listBox.SelectedIndex + 1);
        break;
    }
  }

  private void btnDelete_Click(object sender, EventArgs e)
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
          animation.AddFrame(new VectorObject.Frame());
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
  }

  private void listBox_DoubleClick(object sender, EventArgs e)
  {
    switch(Mode)
    {
      case EditMode.Animation:
        Mode = EditMode.Frame;
        break;
      case EditMode.Frame:
        if(listBox.SelectedIndex != -1) Mode = EditMode.Polygon;
        break;
    }
  }

  private void editMenu_DropDownOpening(object sender, EventArgs e)
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
  #endregion

}

} // namespace RotationalForce.Editor