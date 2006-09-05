using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RotationalForce.Editor
{
  public class StringDialog : Form
  {
    private Label lblPrompt;
    private TextBox textBox;
  
    public StringDialog()
    {
      InitializeComponent();
    }

    public StringDialog(string title, string prompt) : this(title, prompt, string.Empty) { }

    public StringDialog(string title, string prompt, string defaultValue)
    {
      InitializeComponent();

      Text   = title;
      Prompt = prompt;
      Value  = defaultValue;
    }

    public new event CancelEventHandler Validating
    {
      add    { textBox.Validating += value; }
      remove { textBox.Validating -= value; }
    }

    public string Prompt
    {
      get { return lblPrompt.Text; }
      set { lblPrompt.Text = value; }
    }
    
    public string Value
    {
      get { return textBox.Text; }
      set { textBox.Text = value; }
    }

    private void InitializeComponent()
    {
      System.Windows.Forms.Button btnOk;
      System.Windows.Forms.Button btnCancel;
      this.lblPrompt = new System.Windows.Forms.Label();
      this.textBox = new System.Windows.Forms.TextBox();
      btnOk = new System.Windows.Forms.Button();
      btnCancel = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // btnOk
      // 
      btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
      btnOk.Location = new System.Drawing.Point(7, 82);
      btnOk.Name = "btnOk";
      btnOk.Size = new System.Drawing.Size(75, 23);
      btnOk.TabIndex = 2;
      btnOk.Text = "&OK";
      btnOk.UseVisualStyleBackColor = true;
      // 
      // btnCancel
      // 
      btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      btnCancel.Location = new System.Drawing.Point(88, 82);
      btnCancel.Name = "btnCancel";
      btnCancel.Size = new System.Drawing.Size(75, 23);
      btnCancel.TabIndex = 3;
      btnCancel.Text = "&Cancel";
      btnCancel.UseVisualStyleBackColor = true;
      // 
      // lblPrompt
      // 
      this.lblPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.lblPrompt.Location = new System.Drawing.Point(7, 7);
      this.lblPrompt.Name = "lblPrompt";
      this.lblPrompt.Size = new System.Drawing.Size(300, 36);
      this.lblPrompt.TabIndex = 0;
      this.lblPrompt.Text = "Enter prompt";
      // 
      // textBox
      // 
      this.textBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.textBox.Location = new System.Drawing.Point(7, 46);
      this.textBox.Name = "textBox";
      this.textBox.Size = new System.Drawing.Size(300, 20);
      this.textBox.TabIndex = 1;
      // 
      // StringDialog
      // 
      this.AcceptButton = btnOk;
      this.CancelButton = btnCancel;
      this.ClientSize = new System.Drawing.Size(314, 111);
      this.Controls.Add(this.textBox);
      this.Controls.Add(this.lblPrompt);
      this.Controls.Add(btnCancel);
      this.Controls.Add(btnOk);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "StringDialog";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Enter title";
      this.ResumeLayout(false);
      this.PerformLayout();

    }
  }
}