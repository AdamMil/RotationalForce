using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace RotationalForce.Editor
{
  public partial class MountDialog : Form
  {
    private CheckBox chkOwned;
    private CheckBox chkInherit;
    private CheckBox chkRotation;
  
    public MountDialog()
    {
      InitializeComponent();
    }

    public bool OwnedByParent
    {
      get { return chkOwned.Checked; }
    }
    
    public bool InheritProperties
    {
      get { return chkInherit.Checked; }
    }
    
    public bool TrackRotation
    {
      get { return chkRotation.Checked; }
    }

    private void InitializeComponent()
    {
      System.Windows.Forms.Button btnOK;
      System.Windows.Forms.Button btnCancel;
      this.chkOwned = new System.Windows.Forms.CheckBox();
      this.chkRotation = new System.Windows.Forms.CheckBox();
      this.chkInherit = new System.Windows.Forms.CheckBox();
      btnOK = new System.Windows.Forms.Button();
      btnCancel = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // btnOK
      // 
      btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
      btnOK.Location = new System.Drawing.Point(9, 90);
      btnOK.Name = "btnOK";
      btnOK.Size = new System.Drawing.Size(75, 23);
      btnOK.TabIndex = 3;
      btnOK.Text = "&OK";
      btnOK.UseVisualStyleBackColor = true;
      // 
      // btnCancel
      // 
      btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      btnCancel.Location = new System.Drawing.Point(100, 90);
      btnCancel.Name = "btnCancel";
      btnCancel.Size = new System.Drawing.Size(75, 23);
      btnCancel.TabIndex = 4;
      btnCancel.Text = "&Cancel";
      btnCancel.UseVisualStyleBackColor = true;
      // 
      // chkOwned
      // 
      this.chkOwned.Checked = true;
      this.chkOwned.CheckState = System.Windows.Forms.CheckState.Checked;
      this.chkOwned.Location = new System.Drawing.Point(9, 9);
      this.chkOwned.Margin = new System.Windows.Forms.Padding(1);
      this.chkOwned.Name = "chkOwned";
      this.chkOwned.Size = new System.Drawing.Size(172, 24);
      this.chkOwned.TabIndex = 0;
      this.chkOwned.Text = "Owned by parent";
      this.chkOwned.UseVisualStyleBackColor = true;
      // 
      // chkRotation
      // 
      this.chkRotation.Checked = true;
      this.chkRotation.Location = new System.Drawing.Point(9, 35);
      this.chkRotation.Margin = new System.Windows.Forms.Padding(1);
      this.chkRotation.Name = "chkRotation";
      this.chkRotation.Size = new System.Drawing.Size(172, 24);
      this.chkRotation.TabIndex = 1;
      this.chkRotation.Text = "Track parent\'s rotation";
      this.chkRotation.UseVisualStyleBackColor = true;
      // 
      // chkInherit
      // 
      this.chkInherit.Checked = true;
      this.chkInherit.CheckState = System.Windows.Forms.CheckState.Checked;
      this.chkInherit.Location = new System.Drawing.Point(9, 61);
      this.chkInherit.Margin = new System.Windows.Forms.Padding(1);
      this.chkInherit.Name = "chkInherit";
      this.chkInherit.Size = new System.Drawing.Size(172, 24);
      this.chkInherit.TabIndex = 2;
      this.chkInherit.Text = "Inherit rendering properties";
      this.chkInherit.UseVisualStyleBackColor = true;
      // 
      // MountDialog
      // 
      this.ClientSize = new System.Drawing.Size(185, 121);
      this.Controls.Add(this.chkInherit);
      this.Controls.Add(this.chkRotation);
      this.Controls.Add(this.chkOwned);
      this.Controls.Add(btnCancel);
      this.Controls.Add(btnOK);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "MountDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Mount Properties";
      this.ResumeLayout(false);

    }
  }
}