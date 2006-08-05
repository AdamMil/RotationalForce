using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using GameLib.Interop.OpenGL;
using GameLib.Video;
using RotationalForce.Engine;
using GLPoint = GameLib.Mathematics.TwoD.Point;

namespace RotationalForce.Editor
{
  public partial class ImageMapDialog : Form
  {
    private ComboBox filterMode;
    private ComboBox imageMode;
    private TextBox tileHeight;
    private TextBox tileWidth;
    private TextBox tileStartY;
    private TextBox tileStartX;
    private TextBox tileStrideY;
    private TextBox tileStrideX;
    private TextBox tileLimit;
    private RenderPanel renderPanel;
    private Label lblImageSize;
    private GroupBox grpTile;
    private GroupBox grpImage;
    private Button btnSave;
    private TextBox imageName;
  
    public ImageMapDialog()
    {
      InitializeComponent();
    }

    public ImageMap ImageMap
    {
      get { return imageMap; }
    }

    public void CreateNew(string imageFile)
    {
      imageName.Text = Path.GetFileNameWithoutExtension(Project.DenormalizePath(imageFile)).Replace(" ", "");
      imageMap = new FullImageMap(imageName.Text, imageFile);
      OnTypeChanged();
      LoadImageSize();
    }

    public void Open(ImageMap map)
    {
      imageMap = map;
      imageName.Text = map.Name;
      OnTypeChanged();
      LoadImageSize();
    }

    #region InitializeComponent
    private void InitializeComponent()
    {
      System.Windows.Forms.Label label1;
      System.Windows.Forms.Label lblImageMode;
      System.Windows.Forms.Label lblImageName;
      System.Windows.Forms.Label lblTileCount;
      System.Windows.Forms.Label lblTileStrideY;
      System.Windows.Forms.Label lblTileStrideX;
      System.Windows.Forms.Label lblTileOffsetY;
      System.Windows.Forms.Label lblTileOffsetX;
      System.Windows.Forms.Label lblTileHeight;
      System.Windows.Forms.Label lblTileWidth;
      System.Windows.Forms.Button btnCancel;
      this.btnSave = new System.Windows.Forms.Button();
      this.grpImage = new System.Windows.Forms.GroupBox();
      this.filterMode = new System.Windows.Forms.ComboBox();
      this.imageMode = new System.Windows.Forms.ComboBox();
      this.imageName = new System.Windows.Forms.TextBox();
      this.grpTile = new System.Windows.Forms.GroupBox();
      this.tileLimit = new System.Windows.Forms.TextBox();
      this.tileStrideY = new System.Windows.Forms.TextBox();
      this.tileStrideX = new System.Windows.Forms.TextBox();
      this.tileStartY = new System.Windows.Forms.TextBox();
      this.tileStartX = new System.Windows.Forms.TextBox();
      this.tileHeight = new System.Windows.Forms.TextBox();
      this.tileWidth = new System.Windows.Forms.TextBox();
      this.lblImageSize = new System.Windows.Forms.Label();
      this.renderPanel = new RotationalForce.Editor.RenderPanel();
      label1 = new System.Windows.Forms.Label();
      lblImageMode = new System.Windows.Forms.Label();
      lblImageName = new System.Windows.Forms.Label();
      lblTileCount = new System.Windows.Forms.Label();
      lblTileStrideY = new System.Windows.Forms.Label();
      lblTileStrideX = new System.Windows.Forms.Label();
      lblTileOffsetY = new System.Windows.Forms.Label();
      lblTileOffsetX = new System.Windows.Forms.Label();
      lblTileHeight = new System.Windows.Forms.Label();
      lblTileWidth = new System.Windows.Forms.Label();
      btnCancel = new System.Windows.Forms.Button();
      this.grpImage.SuspendLayout();
      this.grpTile.SuspendLayout();
      this.SuspendLayout();
      // 
      // label1
      // 
      label1.Location = new System.Drawing.Point(6, 69);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(74, 20);
      label1.TabIndex = 3;
      label1.Text = "Filter mode";
      label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblImageMode
      // 
      lblImageMode.Location = new System.Drawing.Point(6, 42);
      lblImageMode.Name = "lblImageMode";
      lblImageMode.Size = new System.Drawing.Size(74, 20);
      lblImageMode.TabIndex = 2;
      lblImageMode.Text = "Image mode";
      lblImageMode.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblImageName
      // 
      lblImageName.Location = new System.Drawing.Point(6, 16);
      lblImageName.Name = "lblImageName";
      lblImageName.Size = new System.Drawing.Size(74, 20);
      lblImageName.TabIndex = 1;
      lblImageName.Text = "Image name";
      lblImageName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileCount
      // 
      lblTileCount.Location = new System.Drawing.Point(159, 15);
      lblTileCount.Name = "lblTileCount";
      lblTileCount.Size = new System.Drawing.Size(65, 20);
      lblTileCount.TabIndex = 7;
      lblTileCount.Text = "Tile limit";
      lblTileCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileStrideY
      // 
      lblTileStrideY.Location = new System.Drawing.Point(147, 92);
      lblTileStrideY.Name = "lblTileStrideY";
      lblTileStrideY.Size = new System.Drawing.Size(77, 20);
      lblTileStrideY.TabIndex = 11;
      lblTileStrideY.Text = "Tile stride Y";
      lblTileStrideY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileStrideX
      // 
      lblTileStrideX.Location = new System.Drawing.Point(147, 66);
      lblTileStrideX.Name = "lblTileStrideX";
      lblTileStrideX.Size = new System.Drawing.Size(77, 20);
      lblTileStrideX.TabIndex = 10;
      lblTileStrideX.Text = "Tile stride X";
      lblTileStrideX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileOffsetY
      // 
      lblTileOffsetY.Location = new System.Drawing.Point(8, 93);
      lblTileOffsetY.Name = "lblTileOffsetY";
      lblTileOffsetY.Size = new System.Drawing.Size(71, 20);
      lblTileOffsetY.TabIndex = 9;
      lblTileOffsetY.Text = "Tile start Y";
      lblTileOffsetY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileOffsetX
      // 
      lblTileOffsetX.Location = new System.Drawing.Point(8, 67);
      lblTileOffsetX.Name = "lblTileOffsetX";
      lblTileOffsetX.Size = new System.Drawing.Size(71, 20);
      lblTileOffsetX.TabIndex = 8;
      lblTileOffsetX.Text = "Tile start X";
      lblTileOffsetX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileHeight
      // 
      lblTileHeight.Location = new System.Drawing.Point(8, 41);
      lblTileHeight.Name = "lblTileHeight";
      lblTileHeight.Size = new System.Drawing.Size(71, 20);
      lblTileHeight.TabIndex = 6;
      lblTileHeight.Text = "Tile height";
      lblTileHeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileWidth
      // 
      lblTileWidth.Location = new System.Drawing.Point(5, 15);
      lblTileWidth.Name = "lblTileWidth";
      lblTileWidth.Size = new System.Drawing.Size(74, 20);
      lblTileWidth.TabIndex = 5;
      lblTileWidth.Text = "Tile width";
      lblTileWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
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
      // btnSave
      // 
      this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
      this.btnSave.Location = new System.Drawing.Point(5, 251);
      this.btnSave.Name = "btnSave";
      this.btnSave.Size = new System.Drawing.Size(75, 23);
      this.btnSave.TabIndex = 12;
      this.btnSave.Text = "&Save";
      this.btnSave.UseVisualStyleBackColor = true;
      // 
      // grpImage
      // 
      this.grpImage.Controls.Add(label1);
      this.grpImage.Controls.Add(this.filterMode);
      this.grpImage.Controls.Add(this.imageMode);
      this.grpImage.Controls.Add(lblImageMode);
      this.grpImage.Controls.Add(this.imageName);
      this.grpImage.Controls.Add(lblImageName);
      this.grpImage.Location = new System.Drawing.Point(5, 7);
      this.grpImage.Name = "grpImage";
      this.grpImage.Size = new System.Drawing.Size(282, 101);
      this.grpImage.TabIndex = 0;
      this.grpImage.TabStop = false;
      this.grpImage.Text = "Image settings";
      // 
      // filterMode
      // 
      this.filterMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.filterMode.FormattingEnabled = true;
      this.filterMode.Items.AddRange(new object[] {
            "None",
            "Smooth"});
      this.filterMode.Location = new System.Drawing.Point(79, 69);
      this.filterMode.Name = "filterMode";
      this.filterMode.Size = new System.Drawing.Size(120, 21);
      this.filterMode.TabIndex = 3;
      this.filterMode.SelectedIndexChanged += new System.EventHandler(this.filterMode_SelectedIndexChanged);
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
      this.imageMode.SelectedIndexChanged += new System.EventHandler(this.imageMode_SelectedIndexChanged);
      // 
      // imageName
      // 
      this.imageName.Location = new System.Drawing.Point(80, 16);
      this.imageName.Name = "imageName";
      this.imageName.Size = new System.Drawing.Size(194, 20);
      this.imageName.TabIndex = 1;
      this.imageName.LostFocus += new System.EventHandler(this.imageName_LostFocus);
      // 
      // grpTile
      // 
      this.grpTile.Controls.Add(this.tileLimit);
      this.grpTile.Controls.Add(lblTileCount);
      this.grpTile.Controls.Add(this.tileStrideY);
      this.grpTile.Controls.Add(this.tileStrideX);
      this.grpTile.Controls.Add(lblTileStrideY);
      this.grpTile.Controls.Add(lblTileStrideX);
      this.grpTile.Controls.Add(this.tileStartY);
      this.grpTile.Controls.Add(this.tileStartX);
      this.grpTile.Controls.Add(lblTileOffsetY);
      this.grpTile.Controls.Add(lblTileOffsetX);
      this.grpTile.Controls.Add(this.tileHeight);
      this.grpTile.Controls.Add(this.tileWidth);
      this.grpTile.Controls.Add(lblTileHeight);
      this.grpTile.Controls.Add(lblTileWidth);
      this.grpTile.Enabled = false;
      this.grpTile.Location = new System.Drawing.Point(5, 115);
      this.grpTile.Name = "grpTile";
      this.grpTile.Size = new System.Drawing.Size(282, 129);
      this.grpTile.TabIndex = 1;
      this.grpTile.TabStop = false;
      this.grpTile.Text = "Tile options";
      // 
      // tileLimit
      // 
      this.tileLimit.Location = new System.Drawing.Point(226, 16);
      this.tileLimit.Name = "tileLimit";
      this.tileLimit.Size = new System.Drawing.Size(48, 20);
      this.tileLimit.TabIndex = 7;
      this.tileLimit.Text = "0";
      // 
      // tileStrideY
      // 
      this.tileStrideY.Location = new System.Drawing.Point(225, 93);
      this.tileStrideY.Name = "tileStrideY";
      this.tileStrideY.Size = new System.Drawing.Size(48, 20);
      this.tileStrideY.TabIndex = 11;
      this.tileStrideY.Text = "32";
      // 
      // tileStrideX
      // 
      this.tileStrideX.Location = new System.Drawing.Point(225, 67);
      this.tileStrideX.Name = "tileStrideX";
      this.tileStrideX.Size = new System.Drawing.Size(48, 20);
      this.tileStrideX.TabIndex = 10;
      this.tileStrideX.Text = "32";
      // 
      // tileStartY
      // 
      this.tileStartY.Location = new System.Drawing.Point(79, 94);
      this.tileStartY.Name = "tileStartY";
      this.tileStartY.Size = new System.Drawing.Size(48, 20);
      this.tileStartY.TabIndex = 9;
      this.tileStartY.Text = "0";
      // 
      // tileStartX
      // 
      this.tileStartX.Location = new System.Drawing.Point(79, 68);
      this.tileStartX.Name = "tileStartX";
      this.tileStartX.Size = new System.Drawing.Size(48, 20);
      this.tileStartX.TabIndex = 8;
      this.tileStartX.Text = "0";
      // 
      // tileHeight
      // 
      this.tileHeight.Location = new System.Drawing.Point(79, 42);
      this.tileHeight.Name = "tileHeight";
      this.tileHeight.Size = new System.Drawing.Size(48, 20);
      this.tileHeight.TabIndex = 6;
      this.tileHeight.Text = "32";
      // 
      // tileWidth
      // 
      this.tileWidth.Location = new System.Drawing.Point(79, 16);
      this.tileWidth.Name = "tileWidth";
      this.tileWidth.Size = new System.Drawing.Size(48, 20);
      this.tileWidth.TabIndex = 5;
      this.tileWidth.Text = "32";
      // 
      // lblImageSize
      // 
      this.lblImageSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.lblImageSize.Location = new System.Drawing.Point(294, 251);
      this.lblImageSize.Name = "lblImageSize";
      this.lblImageSize.Size = new System.Drawing.Size(200, 20);
      this.lblImageSize.TabIndex = 14;
      this.lblImageSize.Text = "Image size: ";
      // 
      // renderPanel
      // 
      this.renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.renderPanel.Location = new System.Drawing.Point(294, 7);
      this.renderPanel.Name = "renderPanel";
      this.renderPanel.Size = new System.Drawing.Size(263, 237);
      this.renderPanel.TabIndex = 20;
      this.renderPanel.TabStop = false;
      this.renderPanel.RenderBackground += new System.EventHandler(this.renderPanel_RenderBackground);
      // 
      // ImageMapDialog
      // 
      this.ClientSize = new System.Drawing.Size(565, 280);
      this.Controls.Add(this.lblImageSize);
      this.Controls.Add(this.renderPanel);
      this.Controls.Add(btnCancel);
      this.Controls.Add(this.btnSave);
      this.Controls.Add(this.grpTile);
      this.Controls.Add(this.grpImage);
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(573, 307);
      this.Name = "ImageMapDialog";
      this.Text = "Image Map Editor";
      this.grpImage.ResumeLayout(false);
      this.grpImage.PerformLayout();
      this.grpTile.ResumeLayout(false);
      this.grpTile.PerformLayout();
      this.ResumeLayout(false);

    }
    #endregion
    
    void LoadImageSize()
    {
      try
      {
        Surface surface = new Surface(Engine.Engine.FileSystem.OpenForRead(imageMap.ImageFile));
        lblImageSize.Text += string.Format("{0}x{1}", surface.Width, surface.Height);
        surface.Dispose();
      }
      catch
      {
        MessageBox.Show("Error loading image file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        grpImage.Enabled = grpTile.Enabled = btnSave.Enabled = false;
      }
    }

    void OnTypeChanged()
    {
      imageMode.SelectedItem = imageMap is FullImageMap ? "Full" : "Tiled";
      filterMode.SelectedItem = imageMap.FilterMode.ToString();
      grpTile.Enabled = !(imageMap is FullImageMap);
      renderPanel.InvalidateRender();
    }

    void renderPanel_RenderBackground(object sender, EventArgs e)
    {
      GL.glClear(GL.GL_COLOR_BUFFER_BIT);
      GL.glEnable(GL.GL_TEXTURE_2D);

      if(imageMap is FullImageMap)
      {
        RenderFullImage();
      }
      
      GL.glDisable(GL.GL_TEXTURE_2D);
    }
    
    void RenderFullImage()
    {
      Size frameSize = imageMap.Frames[0].Size; // get the size of the frame in pixels

      Rectangle displayArea = new Rectangle();
      // given the frame size, see how high it would be if scaled to the full width of the renderpanel
      displayArea.Width  = renderPanel.Width;
      displayArea.Height = (int)Math.Round(renderPanel.Width * frameSize.Height / (double)frameSize.Width);

      // if it would be too high, then do it the other way, scaling it to the full height of the renderpanel
      if(displayArea.Height > renderPanel.Height)
      {
        displayArea.Width  = (int)Math.Round(renderPanel.Height * frameSize.Width / (double)frameSize.Height);
        displayArea.Height = renderPanel.Height;
      }

      // now center the display area within the render panel
      displayArea.X = (renderPanel.Width  - displayArea.Width)  / 2;
      displayArea.Y = (renderPanel.Height - displayArea.Height) / 2;

      imageMap.BindFrame(0);

      GL.glBegin(GL.GL_QUADS);
        GL.glTexCoord2d(imageMap.GetTextureCoord(0, new GLPoint(0, 0)));
        GL.glVertex2i(displayArea.Left, displayArea.Top);

        GL.glTexCoord2d(imageMap.GetTextureCoord(0, new GLPoint(1, 0)));
        GL.glVertex2i(displayArea.Right, displayArea.Top);

        GL.glTexCoord2d(imageMap.GetTextureCoord(0, new GLPoint(1, 1)));
        GL.glVertex2i(displayArea.Right, displayArea.Bottom);

        GL.glTexCoord2d(imageMap.GetTextureCoord(0, new GLPoint(0, 1)));
        GL.glVertex2i(displayArea.Left, displayArea.Bottom);
      GL.glEnd();
    }

    void imageName_LostFocus(object sender, EventArgs e)
    {
      imageMap.Name = imageName.Text;
    }

    void imageMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      ImageMap newMap = (string)imageMode.SelectedItem == "Full" ? new FullImageMap(imageMap.Name, imageMap.ImageFile)
        : (ImageMap)new TiledImageMap(imageMap.Name, imageMap.ImageFile);
      newMap.FilterMode = imageMap.FilterMode;
      imageMap.Dispose();
      imageMap = newMap;
      OnTypeChanged();
    }

    void filterMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      imageMap.FilterMode = (FilterMode)Enum.Parse(typeof(FilterMode), (string)filterMode.SelectedItem);
      renderPanel.InvalidateRender();
    }

    ImageMap imageMap;
  }
}