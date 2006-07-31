using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RotationalForce.Editor
{
  public partial class ImageMapDialog : Form
  {
    private ComboBox filterMode;
    private ComboBox imageMode;
    private CheckBox filterPad;
    private TextBox tileHeight;
    private TextBox tileWidth;
    private TextBox tileStartY;
    private TextBox tileStartX;
    private TextBox tileStrideY;
    private TextBox tileStrideX;
    private TextBox tileCount;
    private PictureBox imageBox;
    private Label lblImageSize;
    private TextBox imageName;
  
    public ImageMapDialog()
    {
      InitializeComponent();
    }

    private void InitializeComponent()
    {
      System.Windows.Forms.GroupBox grpImage;
      System.Windows.Forms.Label lblImageName;
      System.Windows.Forms.Label lblImageMode;
      System.Windows.Forms.Label label1;
      System.Windows.Forms.GroupBox grpTile;
      System.Windows.Forms.Label lblTileWidth;
      System.Windows.Forms.Label lblTileHeight;
      System.Windows.Forms.Label lblTileOffsetY;
      System.Windows.Forms.Label lblTileOffsetX;
      System.Windows.Forms.Label lblTileStrideY;
      System.Windows.Forms.Label lblTileStrideX;
      System.Windows.Forms.Button btnOK;
      System.Windows.Forms.Button btnCancel;
      System.Windows.Forms.Label lblTileCount;
      this.imageName = new System.Windows.Forms.TextBox();
      this.imageMode = new System.Windows.Forms.ComboBox();
      this.filterMode = new System.Windows.Forms.ComboBox();
      this.filterPad = new System.Windows.Forms.CheckBox();
      this.tileWidth = new System.Windows.Forms.TextBox();
      this.tileHeight = new System.Windows.Forms.TextBox();
      this.tileStartY = new System.Windows.Forms.TextBox();
      this.tileStartX = new System.Windows.Forms.TextBox();
      this.tileStrideY = new System.Windows.Forms.TextBox();
      this.tileStrideX = new System.Windows.Forms.TextBox();
      this.tileCount = new System.Windows.Forms.TextBox();
      this.imageBox = new System.Windows.Forms.PictureBox();
      this.lblImageSize = new System.Windows.Forms.Label();
      grpImage = new System.Windows.Forms.GroupBox();
      lblImageName = new System.Windows.Forms.Label();
      lblImageMode = new System.Windows.Forms.Label();
      label1 = new System.Windows.Forms.Label();
      grpTile = new System.Windows.Forms.GroupBox();
      lblTileWidth = new System.Windows.Forms.Label();
      lblTileHeight = new System.Windows.Forms.Label();
      lblTileOffsetY = new System.Windows.Forms.Label();
      lblTileOffsetX = new System.Windows.Forms.Label();
      lblTileStrideY = new System.Windows.Forms.Label();
      lblTileStrideX = new System.Windows.Forms.Label();
      btnOK = new System.Windows.Forms.Button();
      btnCancel = new System.Windows.Forms.Button();
      lblTileCount = new System.Windows.Forms.Label();
      grpImage.SuspendLayout();
      grpTile.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.imageBox)).BeginInit();
      this.SuspendLayout();
      // 
      // grpImage
      // 
      grpImage.Controls.Add(this.filterPad);
      grpImage.Controls.Add(label1);
      grpImage.Controls.Add(this.filterMode);
      grpImage.Controls.Add(this.imageMode);
      grpImage.Controls.Add(lblImageMode);
      grpImage.Controls.Add(this.imageName);
      grpImage.Controls.Add(lblImageName);
      grpImage.Location = new System.Drawing.Point(5, 7);
      grpImage.Name = "grpImage";
      grpImage.Size = new System.Drawing.Size(282, 101);
      grpImage.TabIndex = 0;
      grpImage.TabStop = false;
      grpImage.Text = "Image settings";
      // 
      // lblImageName
      // 
      lblImageName.Location = new System.Drawing.Point(9, 16);
      lblImageName.Name = "lblImageName";
      lblImageName.Size = new System.Drawing.Size(65, 20);
      lblImageName.TabIndex = 1;
      lblImageName.Text = "Image name";
      lblImageName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // imageName
      // 
      this.imageName.Location = new System.Drawing.Point(80, 16);
      this.imageName.Name = "imageName";
      this.imageName.Size = new System.Drawing.Size(194, 20);
      this.imageName.TabIndex = 1;
      // 
      // lblImageMode
      // 
      lblImageMode.Location = new System.Drawing.Point(9, 42);
      lblImageMode.Name = "lblImageMode";
      lblImageMode.Size = new System.Drawing.Size(65, 20);
      lblImageMode.TabIndex = 2;
      lblImageMode.Text = "Image mode";
      lblImageMode.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // imageMode
      // 
      this.imageMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.imageMode.FormattingEnabled = true;
      this.imageMode.Items.AddRange(new object[] {
            "Full",
            "Tiled"});
      this.imageMode.Location = new System.Drawing.Point(79, 42);
      this.imageMode.Name = "imageMode";
      this.imageMode.Size = new System.Drawing.Size(120, 21);
      this.imageMode.TabIndex = 2;
      // 
      // filterMode
      // 
      this.filterMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.filterMode.FormattingEnabled = true;
      this.filterMode.Items.AddRange(new object[] {
            "NearestNeighbor",
            "Bilinear"});
      this.filterMode.Location = new System.Drawing.Point(79, 69);
      this.filterMode.Name = "filterMode";
      this.filterMode.Size = new System.Drawing.Size(120, 21);
      this.filterMode.TabIndex = 3;
      // 
      // label1
      // 
      label1.Location = new System.Drawing.Point(9, 69);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(65, 20);
      label1.TabIndex = 3;
      label1.Text = "Filter mode";
      label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // filterPad
      // 
      this.filterPad.AutoSize = true;
      this.filterPad.Location = new System.Drawing.Point(205, 71);
      this.filterPad.Name = "filterPad";
      this.filterPad.Size = new System.Drawing.Size(69, 17);
      this.filterPad.TabIndex = 4;
      this.filterPad.Text = "Filter pad";
      this.filterPad.UseVisualStyleBackColor = true;
      // 
      // grpTile
      // 
      grpTile.Controls.Add(this.tileCount);
      grpTile.Controls.Add(lblTileCount);
      grpTile.Controls.Add(this.tileStrideY);
      grpTile.Controls.Add(this.tileStrideX);
      grpTile.Controls.Add(lblTileStrideY);
      grpTile.Controls.Add(lblTileStrideX);
      grpTile.Controls.Add(this.tileStartY);
      grpTile.Controls.Add(this.tileStartX);
      grpTile.Controls.Add(lblTileOffsetY);
      grpTile.Controls.Add(lblTileOffsetX);
      grpTile.Controls.Add(this.tileHeight);
      grpTile.Controls.Add(this.tileWidth);
      grpTile.Controls.Add(lblTileHeight);
      grpTile.Controls.Add(lblTileWidth);
      grpTile.Location = new System.Drawing.Point(5, 115);
      grpTile.Name = "grpTile";
      grpTile.Size = new System.Drawing.Size(282, 129);
      grpTile.TabIndex = 1;
      grpTile.TabStop = false;
      grpTile.Text = "Tile options";
      // 
      // lblTileWidth
      // 
      lblTileWidth.Location = new System.Drawing.Point(9, 15);
      lblTileWidth.Name = "lblTileWidth";
      lblTileWidth.Size = new System.Drawing.Size(65, 20);
      lblTileWidth.TabIndex = 5;
      lblTileWidth.Text = "Tile width";
      lblTileWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileHeight
      // 
      lblTileHeight.Location = new System.Drawing.Point(9, 41);
      lblTileHeight.Name = "lblTileHeight";
      lblTileHeight.Size = new System.Drawing.Size(65, 20);
      lblTileHeight.TabIndex = 6;
      lblTileHeight.Text = "Tile height";
      lblTileHeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // tileWidth
      // 
      this.tileWidth.Location = new System.Drawing.Point(79, 16);
      this.tileWidth.Name = "tileWidth";
      this.tileWidth.Size = new System.Drawing.Size(48, 20);
      this.tileWidth.TabIndex = 5;
      // 
      // tileHeight
      // 
      this.tileHeight.Location = new System.Drawing.Point(79, 42);
      this.tileHeight.Name = "tileHeight";
      this.tileHeight.Size = new System.Drawing.Size(48, 20);
      this.tileHeight.TabIndex = 6;
      // 
      // tileStartY
      // 
      this.tileStartY.Location = new System.Drawing.Point(79, 94);
      this.tileStartY.Name = "tileStartY";
      this.tileStartY.Size = new System.Drawing.Size(48, 20);
      this.tileStartY.TabIndex = 9;
      // 
      // tileStartX
      // 
      this.tileStartX.Location = new System.Drawing.Point(79, 68);
      this.tileStartX.Name = "tileStartX";
      this.tileStartX.Size = new System.Drawing.Size(48, 20);
      this.tileStartX.TabIndex = 8;
      // 
      // lblTileOffsetY
      // 
      lblTileOffsetY.Location = new System.Drawing.Point(9, 93);
      lblTileOffsetY.Name = "lblTileOffsetY";
      lblTileOffsetY.Size = new System.Drawing.Size(65, 20);
      lblTileOffsetY.TabIndex = 9;
      lblTileOffsetY.Text = "Tile start Y";
      lblTileOffsetY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileOffsetX
      // 
      lblTileOffsetX.Location = new System.Drawing.Point(9, 67);
      lblTileOffsetX.Name = "lblTileOffsetX";
      lblTileOffsetX.Size = new System.Drawing.Size(65, 20);
      lblTileOffsetX.TabIndex = 8;
      lblTileOffsetX.Text = "Tile start X";
      lblTileOffsetX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // tileStrideY
      // 
      this.tileStrideY.Location = new System.Drawing.Point(225, 93);
      this.tileStrideY.Name = "tileStrideY";
      this.tileStrideY.Size = new System.Drawing.Size(48, 20);
      this.tileStrideY.TabIndex = 11;
      // 
      // tileStrideX
      // 
      this.tileStrideX.Location = new System.Drawing.Point(225, 67);
      this.tileStrideX.Name = "tileStrideX";
      this.tileStrideX.Size = new System.Drawing.Size(48, 20);
      this.tileStrideX.TabIndex = 10;
      // 
      // lblTileStrideY
      // 
      lblTileStrideY.Location = new System.Drawing.Point(155, 92);
      lblTileStrideY.Name = "lblTileStrideY";
      lblTileStrideY.Size = new System.Drawing.Size(65, 20);
      lblTileStrideY.TabIndex = 11;
      lblTileStrideY.Text = "Tile stride Y";
      lblTileStrideY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileStrideX
      // 
      lblTileStrideX.Location = new System.Drawing.Point(155, 66);
      lblTileStrideX.Name = "lblTileStrideX";
      lblTileStrideX.Size = new System.Drawing.Size(65, 20);
      lblTileStrideX.TabIndex = 10;
      lblTileStrideX.Text = "Tile stride X";
      lblTileStrideX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // btnOK
      // 
      btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
      btnOK.Location = new System.Drawing.Point(5, 251);
      btnOK.Name = "btnOK";
      btnOK.Size = new System.Drawing.Size(75, 23);
      btnOK.TabIndex = 12;
      btnOK.Text = "&OK";
      btnOK.UseVisualStyleBackColor = true;
      // 
      // btnCancel
      // 
      btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      btnCancel.Location = new System.Drawing.Point(86, 251);
      btnCancel.Name = "btnCancel";
      btnCancel.Size = new System.Drawing.Size(75, 23);
      btnCancel.TabIndex = 13;
      btnCancel.Text = "Cancel";
      btnCancel.UseVisualStyleBackColor = true;
      // 
      // tileCount
      // 
      this.tileCount.Location = new System.Drawing.Point(226, 16);
      this.tileCount.Name = "tileCount";
      this.tileCount.Size = new System.Drawing.Size(48, 20);
      this.tileCount.TabIndex = 7;
      // 
      // lblTileCount
      // 
      lblTileCount.Location = new System.Drawing.Point(155, 15);
      lblTileCount.Name = "lblTileCount";
      lblTileCount.Size = new System.Drawing.Size(65, 20);
      lblTileCount.TabIndex = 7;
      lblTileCount.Text = "Tile count";
      lblTileCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // imageBox
      // 
      this.imageBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.imageBox.Location = new System.Drawing.Point(294, 7);
      this.imageBox.Name = "imageBox";
      this.imageBox.Size = new System.Drawing.Size(263, 237);
      this.imageBox.TabIndex = 4;
      this.imageBox.TabStop = false;
      // 
      // lblImageSize
      // 
      this.lblImageSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.lblImageSize.Location = new System.Drawing.Point(294, 251);
      this.lblImageSize.Name = "lblImageSize";
      this.lblImageSize.Size = new System.Drawing.Size(200, 20);
      this.lblImageSize.TabIndex = 14;
      this.lblImageSize.Text = "Image size:";
      // 
      // ImageMapDialog
      // 
      this.ClientSize = new System.Drawing.Size(565, 280);
      this.Controls.Add(this.lblImageSize);
      this.Controls.Add(this.imageBox);
      this.Controls.Add(btnCancel);
      this.Controls.Add(btnOK);
      this.Controls.Add(grpTile);
      this.Controls.Add(grpImage);
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(573, 307);
      this.Name = "ImageMapDialog";
      this.Text = "Image Map Editor";
      grpImage.ResumeLayout(false);
      grpImage.PerformLayout();
      grpTile.ResumeLayout(false);
      grpTile.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.imageBox)).EndInit();
      this.ResumeLayout(false);

    }
  }
}