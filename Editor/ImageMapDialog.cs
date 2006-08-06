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
    private GroupBox grpTile;
    private GroupBox grpImage;
    private Button btnSave;
    private TextBox texturePriority;
    private Button btnZoomIn;
    private Button btnZoomOut;
    private ComboBox wrapMode;
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
      ValidateImage();
      initialized = true;
    }

    public void Open(ImageMap map)
    {
      imageMap = map;
      imageName.Text = map.Name;
      OnTypeChanged();
      ValidateImage();
      initialized = true;
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
      System.Windows.Forms.Button btnBackColor;
      System.Windows.Forms.Label lblTexturePriority;
      System.Windows.Forms.Label lblCoords;
      this.btnSave = new System.Windows.Forms.Button();
      this.grpImage = new System.Windows.Forms.GroupBox();
      this.texturePriority = new System.Windows.Forms.TextBox();
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
      this.btnZoomIn = new System.Windows.Forms.Button();
      this.btnZoomOut = new System.Windows.Forms.Button();
      this.renderPanel = new RotationalForce.Editor.RenderPanel();
      this.wrapMode = new System.Windows.Forms.ComboBox();
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
      btnBackColor = new System.Windows.Forms.Button();
      lblTexturePriority = new System.Windows.Forms.Label();
      lblCoords = new System.Windows.Forms.Label();
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
      lblTileCount.TabIndex = 16;
      lblTileCount.Text = "Tile limit";
      lblTileCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileStrideY
      // 
      lblTileStrideY.Location = new System.Drawing.Point(147, 92);
      lblTileStrideY.Name = "lblTileStrideY";
      lblTileStrideY.Size = new System.Drawing.Size(77, 20);
      lblTileStrideY.TabIndex = 15;
      lblTileStrideY.Text = "Tile stride Y";
      lblTileStrideY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileStrideX
      // 
      lblTileStrideX.Location = new System.Drawing.Point(147, 66);
      lblTileStrideX.Name = "lblTileStrideX";
      lblTileStrideX.Size = new System.Drawing.Size(77, 20);
      lblTileStrideX.TabIndex = 14;
      lblTileStrideX.Text = "Tile stride X";
      lblTileStrideX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileOffsetY
      // 
      lblTileOffsetY.Location = new System.Drawing.Point(8, 93);
      lblTileOffsetY.Name = "lblTileOffsetY";
      lblTileOffsetY.Size = new System.Drawing.Size(71, 20);
      lblTileOffsetY.TabIndex = 13;
      lblTileOffsetY.Text = "Tile start Y";
      lblTileOffsetY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileOffsetX
      // 
      lblTileOffsetX.Location = new System.Drawing.Point(8, 67);
      lblTileOffsetX.Name = "lblTileOffsetX";
      lblTileOffsetX.Size = new System.Drawing.Size(71, 20);
      lblTileOffsetX.TabIndex = 12;
      lblTileOffsetX.Text = "Tile start X";
      lblTileOffsetX.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileHeight
      // 
      lblTileHeight.Location = new System.Drawing.Point(8, 41);
      lblTileHeight.Name = "lblTileHeight";
      lblTileHeight.Size = new System.Drawing.Size(71, 20);
      lblTileHeight.TabIndex = 12;
      lblTileHeight.Text = "Tile height";
      lblTileHeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // lblTileWidth
      // 
      lblTileWidth.Location = new System.Drawing.Point(5, 15);
      lblTileWidth.Name = "lblTileWidth";
      lblTileWidth.Size = new System.Drawing.Size(74, 20);
      lblTileWidth.TabIndex = 11;
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
      btnCancel.TabIndex = 21;
      btnCancel.Text = "Cancel";
      btnCancel.UseVisualStyleBackColor = true;
      // 
      // btnBackColor
      // 
      btnBackColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      btnBackColor.Location = new System.Drawing.Point(294, 250);
      btnBackColor.Name = "btnBackColor";
      btnBackColor.Size = new System.Drawing.Size(75, 23);
      btnBackColor.TabIndex = 22;
      btnBackColor.Text = "Bkg. Color";
      btnBackColor.UseVisualStyleBackColor = true;
      btnBackColor.Click += new System.EventHandler(this.btnBackColor_Click);
      // 
      // lblTexturePriority
      // 
      lblTexturePriority.AutoSize = true;
      lblTexturePriority.Location = new System.Drawing.Point(147, 46);
      lblTexturePriority.Name = "lblTexturePriority";
      lblTexturePriority.Size = new System.Drawing.Size(76, 13);
      lblTexturePriority.TabIndex = 4;
      lblTexturePriority.Text = "Texture priority";
      lblTexturePriority.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // btnSave
      // 
      this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
      this.btnSave.Location = new System.Drawing.Point(5, 251);
      this.btnSave.Name = "btnSave";
      this.btnSave.Size = new System.Drawing.Size(75, 23);
      this.btnSave.TabIndex = 20;
      this.btnSave.Text = "&Save";
      this.btnSave.UseVisualStyleBackColor = true;
      // 
      // grpImage
      // 
      this.grpImage.Controls.Add(this.wrapMode);
      this.grpImage.Controls.Add(lblCoords);
      this.grpImage.Controls.Add(this.texturePriority);
      this.grpImage.Controls.Add(lblTexturePriority);
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
      // texturePriority
      // 
      this.texturePriority.Location = new System.Drawing.Point(226, 42);
      this.texturePriority.Name = "texturePriority";
      this.texturePriority.Size = new System.Drawing.Size(48, 20);
      this.texturePriority.TabIndex = 4;
      this.texturePriority.Validated += new System.EventHandler(this.texturePriority_Validated);
      this.texturePriority.Validating += new System.ComponentModel.CancelEventHandler(this.texturePriority_Validating);
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
      this.filterMode.Size = new System.Drawing.Size(62, 21);
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
      this.imageMode.Size = new System.Drawing.Size(62, 21);
      this.imageMode.TabIndex = 2;
      this.imageMode.SelectedIndexChanged += new System.EventHandler(this.imageMode_SelectedIndexChanged);
      // 
      // imageName
      // 
      this.imageName.Location = new System.Drawing.Point(80, 16);
      this.imageName.Name = "imageName";
      this.imageName.Size = new System.Drawing.Size(194, 20);
      this.imageName.TabIndex = 1;
      this.imageName.Validated += new System.EventHandler(this.imageName_Validated);
      this.imageName.Validating += new System.ComponentModel.CancelEventHandler(this.imageName_Validating);
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
      this.grpTile.Location = new System.Drawing.Point(5, 115);
      this.grpTile.Name = "grpTile";
      this.grpTile.Size = new System.Drawing.Size(282, 129);
      this.grpTile.TabIndex = 10;
      this.grpTile.TabStop = false;
      this.grpTile.Text = "Tile options";
      // 
      // tileLimit
      // 
      this.tileLimit.Location = new System.Drawing.Point(226, 16);
      this.tileLimit.Name = "tileLimit";
      this.tileLimit.Size = new System.Drawing.Size(48, 20);
      this.tileLimit.TabIndex = 16;
      this.tileLimit.Validated += new System.EventHandler(this.tileLimit_Validated);
      this.tileLimit.Validating += new System.ComponentModel.CancelEventHandler(this.tileLimit_Validating);
      // 
      // tileStrideY
      // 
      this.tileStrideY.Location = new System.Drawing.Point(225, 93);
      this.tileStrideY.Name = "tileStrideY";
      this.tileStrideY.Size = new System.Drawing.Size(48, 20);
      this.tileStrideY.TabIndex = 15;
      this.tileStrideY.Validated += new System.EventHandler(this.tileStride_Validated);
      this.tileStrideY.Validating += new System.ComponentModel.CancelEventHandler(this.tileStride_Validating);
      // 
      // tileStrideX
      // 
      this.tileStrideX.Location = new System.Drawing.Point(225, 67);
      this.tileStrideX.Name = "tileStrideX";
      this.tileStrideX.Size = new System.Drawing.Size(48, 20);
      this.tileStrideX.TabIndex = 14;
      this.tileStrideX.Validated += new System.EventHandler(this.tileStride_Validated);
      this.tileStrideX.Validating += new System.ComponentModel.CancelEventHandler(this.tileStride_Validating);
      // 
      // tileStartY
      // 
      this.tileStartY.Location = new System.Drawing.Point(79, 94);
      this.tileStartY.Name = "tileStartY";
      this.tileStartY.Size = new System.Drawing.Size(48, 20);
      this.tileStartY.TabIndex = 13;
      this.tileStartY.Validated += new System.EventHandler(this.tileStart_Validated);
      this.tileStartY.Validating += new System.ComponentModel.CancelEventHandler(this.tileStart_Validating);
      // 
      // tileStartX
      // 
      this.tileStartX.Location = new System.Drawing.Point(79, 68);
      this.tileStartX.Name = "tileStartX";
      this.tileStartX.Size = new System.Drawing.Size(48, 20);
      this.tileStartX.TabIndex = 12;
      this.tileStartX.Validated += new System.EventHandler(this.tileStart_Validated);
      this.tileStartX.Validating += new System.ComponentModel.CancelEventHandler(this.tileStart_Validating);
      // 
      // tileHeight
      // 
      this.tileHeight.Location = new System.Drawing.Point(79, 42);
      this.tileHeight.Name = "tileHeight";
      this.tileHeight.Size = new System.Drawing.Size(48, 20);
      this.tileHeight.TabIndex = 12;
      this.tileHeight.Validated += new System.EventHandler(this.tileSize_Validated);
      this.tileHeight.Validating += new System.ComponentModel.CancelEventHandler(this.tileSize_Validating);
      // 
      // tileWidth
      // 
      this.tileWidth.Location = new System.Drawing.Point(79, 16);
      this.tileWidth.Name = "tileWidth";
      this.tileWidth.Size = new System.Drawing.Size(48, 20);
      this.tileWidth.TabIndex = 11;
      this.tileWidth.Validated += new System.EventHandler(this.tileSize_Validated);
      this.tileWidth.Validating += new System.ComponentModel.CancelEventHandler(this.tileSize_Validating);
      // 
      // btnZoomIn
      // 
      this.btnZoomIn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnZoomIn.Location = new System.Drawing.Point(375, 250);
      this.btnZoomIn.Name = "btnZoomIn";
      this.btnZoomIn.Size = new System.Drawing.Size(75, 23);
      this.btnZoomIn.TabIndex = 23;
      this.btnZoomIn.Text = "Zoom &In";
      this.btnZoomIn.UseVisualStyleBackColor = true;
      this.btnZoomIn.Click += new System.EventHandler(this.btnZoomIn_Click);
      // 
      // btnZoomOut
      // 
      this.btnZoomOut.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnZoomOut.Location = new System.Drawing.Point(456, 250);
      this.btnZoomOut.Name = "btnZoomOut";
      this.btnZoomOut.Size = new System.Drawing.Size(75, 23);
      this.btnZoomOut.TabIndex = 24;
      this.btnZoomOut.Text = "Zoom &Out";
      this.btnZoomOut.UseVisualStyleBackColor = true;
      this.btnZoomOut.Click += new System.EventHandler(this.btnZoomOut_Click);
      // 
      // renderPanel
      // 
      this.renderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.renderPanel.BackColor = System.Drawing.Color.Black;
      this.renderPanel.Location = new System.Drawing.Point(294, 7);
      this.renderPanel.Name = "renderPanel";
      this.renderPanel.Size = new System.Drawing.Size(263, 237);
      this.renderPanel.TabIndex = 30;
      this.renderPanel.TabStop = false;
      this.renderPanel.RenderBackground += new System.EventHandler(this.renderPanel_RenderBackground);
      // 
      // lblCoords
      // 
      lblCoords.AutoSize = true;
      lblCoords.Location = new System.Drawing.Point(147, 72);
      lblCoords.Name = "lblCoords";
      lblCoords.Size = new System.Drawing.Size(63, 13);
      lblCoords.TabIndex = 5;
      lblCoords.Text = "Coordinates";
      lblCoords.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // wrapMode
      // 
      this.wrapMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.wrapMode.FormattingEnabled = true;
      this.wrapMode.Items.AddRange(new object[] {
            "Clamp",
            "Repeat"});
      this.wrapMode.Location = new System.Drawing.Point(212, 68);
      this.wrapMode.Name = "wrapMode";
      this.wrapMode.Size = new System.Drawing.Size(62, 21);
      this.wrapMode.TabIndex = 5;
      this.wrapMode.SelectedIndexChanged += new System.EventHandler(this.wrapMode_SelectedIndexChanged);
      // 
      // ImageMapDialog
      // 
      this.ClientSize = new System.Drawing.Size(565, 280);
      this.Controls.Add(this.btnZoomOut);
      this.Controls.Add(this.btnZoomIn);
      this.Controls.Add(btnBackColor);
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

    void OnTypeChanged()
    {
      TiledImageMap tiled = imageMap as TiledImageMap;
      imageMode.SelectedItem  = tiled == null ? "Full" : "Tiled";
      filterMode.SelectedItem = imageMap.FilterMode.ToString();
      wrapMode.SelectedItem   = imageMap.TextureWrap.ToString();
      grpTile.Enabled = btnZoomIn.Enabled = btnZoomOut.Enabled = tiled != null;
      texturePriority.Text = imageMap.Priority.ToString();

      if(tiled != null)
      {
        tileWidth.Text   = tiled.TileSize.Width.ToString();
        tileHeight.Text  = tiled.TileSize.Height.ToString();
        tileStrideX.Text = tiled.TileStride.Width.ToString();
        tileStrideY.Text = tiled.TileStride.Height.ToString();
        tileStartX.Text  = tiled.TileStart.X.ToString();
        tileStartY.Text  = tiled.TileStart.Y.ToString();
        tileLimit.Text   = tiled.TileLimit.ToString();
      }

      tileZoom = 1;
      renderPanel.InvalidateRender();
    }

    void ValidateImage()
    {
      try
      {
        new Surface(Engine.Engine.FileSystem.OpenForRead(imageMap.ImageFile)).Dispose();
      }
      catch
      {
        MessageBox.Show("Error loading image file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        grpImage.Enabled = grpTile.Enabled = btnSave.Enabled = false;
      }
    }

    void renderPanel_RenderBackground(object sender, EventArgs e)
    {
      GL.glClearColor(renderPanel.BackColor);
      GL.glClear(GL.GL_COLOR_BUFFER_BIT);
      GL.glEnable(GL.GL_TEXTURE_2D);

      try
      {
        if(imageMap.Frames.Count == 0)
        {
          MessageBox.Show("No frames could be picked out.", "Nothing to render",
                          MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        else if(imageMap.Frames.Count == 1 || imageMap is FullImageMap)
        {
          RenderFullImage();
        }
        else
        {
          RenderTiledImage();
        }
      }
      catch(Exception ex)
      {
        MessageBox.Show("An error occurred while rendering the image:\n\n"+ex.Message, "Error occurred",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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

    void RenderTiledImage()
    {
      const int borderSize = 2;

      TiledImageMap tiled = (TiledImageMap)imageMap;

      // see how many full tiles we can fit into the renderpanel at their natural size
      Size tileSize = new Size(tiled.TileSize.Width*tileZoom, tiled.TileSize.Height*tileZoom);
      Size tiles = new Size((renderPanel.Width-tileSize.Width) / (tileSize.Width+borderSize) + 1,
                            (renderPanel.Height-tileSize.Height) / (tileSize.Height+borderSize) + 1);
      
      // but make sure we show at least 2 tiles horizontally and 2 vertically
      if(tiles.Width < 2)
      {
        tiles.Width    = 2;
        tileSize.Width = (renderPanel.Width-borderSize) / 2;
      }
      if(tiles.Height < 2 && tiled.Frames.Count > 2)
      {
        tiles.Height    = 2;
        tileSize.Height = (renderPanel.Height-borderSize) / 2;
      }
      
      int numTiles = Math.Min(tiled.Frames.Count, tiles.Width*tiles.Height);

      Point destPoint = new Point();
      for(int i=0; i<numTiles; i++)
      {
        tiled.BindFrame(i);
        GL.glBegin(GL.GL_QUADS);
          GL.glTexCoord2d(tiled.GetTextureCoord(i, new GLPoint(0, 0)));
          GL.glVertex2i(destPoint.X, destPoint.Y);
          GL.glTexCoord2d(tiled.GetTextureCoord(i, new GLPoint(1, 0)));
          GL.glVertex2i(destPoint.X+tileSize.Width, destPoint.Y);
          GL.glTexCoord2d(tiled.GetTextureCoord(i, new GLPoint(1, 1)));
          GL.glVertex2i(destPoint.X+tileSize.Width, destPoint.Y+tileSize.Height);
          GL.glTexCoord2d(tiled.GetTextureCoord(i, new GLPoint(0, 1)));
          GL.glVertex2i(destPoint.X, destPoint.Y+tileSize.Height);
        GL.glEnd();
        
        destPoint.X += tileSize.Width + borderSize;
        if(destPoint.X + tileSize.Width > renderPanel.Width)
        {
          destPoint.X  = 0;
          destPoint.Y += tileSize.Height + borderSize;
        }
      }
    }

    void imageName_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      string name = ((TextBox)sender).Text;

      if(string.IsNullOrEmpty(name) || name.Contains("#"))
      {
        MessageBox.Show("Image map names cannot be empty or contain '#' characters.", "Err-ror. Err-ror.",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
        e.Cancel = false;
      }
    }

    void imageName_Validated(object sender, EventArgs e)
    {
      imageMap.Name = ((TextBox)sender).Text;
    }

    void imageMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      if(initialized)
      {
        ImageMap newMap = (string)imageMode.SelectedItem == "Full" ? new FullImageMap(imageMap.Name, imageMap.ImageFile)
          : (ImageMap)new TiledImageMap(imageMap.Name, imageMap.ImageFile);
        newMap.FilterMode = imageMap.FilterMode;
        imageMap.Dispose();
        imageMap = newMap;
        OnTypeChanged();
      }
    }

    void filterMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      imageMap.FilterMode = (FilterMode)Enum.Parse(typeof(FilterMode), (string)filterMode.SelectedItem);
      renderPanel.InvalidateRender();
    }

    void wrapMode_SelectedIndexChanged(object sender, EventArgs e)
    {
      imageMap.TextureWrap = (TextureWrap)Enum.Parse(typeof(TextureWrap), (string)wrapMode.SelectedItem);
      renderPanel.InvalidateRender();
    }

    void texturePriority_Validated(object sender, EventArgs e)
    {
      imageMap.Priority = float.Parse(((TextBox)sender).Text);
    }

    void texturePriority_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      float value;
      if(!float.TryParse(((TextBox)sender).Text, out value) || value < 0 || value > 1 || float.IsNaN(value))
      {
        e.Cancel = true;
      }
    }

    void tileLimit_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      int value;
      if(!int.TryParse(((TextBox)sender).Text, out value) || value < 0)
      {
        e.Cancel = true;
      }
    }

    void tileLimit_Validated(object sender, EventArgs e)
    {
      ((TiledImageMap)imageMap).TileLimit = int.Parse(((TextBox)sender).Text);
      renderPanel.InvalidateRender();
    }

    void tileSize_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      int value;
      if(!int.TryParse(((TextBox)sender).Text, out value) || value <= 0)
      {
        e.Cancel = true;
      }
    }

    void tileSize_Validated(object sender, EventArgs e)
    {
      bool isWidth = sender == tileWidth;
      TextBox sizeBox = (TextBox)sender, strideBox = isWidth ? tileStrideX : tileStrideY;
      TiledImageMap tiled = (TiledImageMap)imageMap;
      int value  = int.Parse(sizeBox.Text);
      int stride = int.Parse(strideBox.Text);

      if(value > Math.Abs(stride))
      {
        stride = value * Math.Sign(stride);
        strideBox.Text = stride.ToString();
        if(isWidth)
        {
          tiled.TileStride = new Size(stride, tiled.TileStride.Height);
        }
        else
        {
          tiled.TileStride = new Size(tiled.TileStride.Width, value);
        }
      }
      
      if(isWidth)
      {
        tiled.TileSize = new Size(value, tiled.TileSize.Height);
      }
      else
      {
        tiled.TileSize = new Size(tiled.TileSize.Width, value);
      }

      renderPanel.InvalidateRender();
    }

    void tileStart_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      int value;
      if(!int.TryParse(((TextBox)sender).Text, out value) || value < 0)
      {
        e.Cancel = true;
      }
    }

    void tileStart_Validated(object sender, EventArgs e)
    {
      TiledImageMap tiled = (TiledImageMap)imageMap;
      int value = int.Parse(((TextBox)sender).Text);
      bool isX = sender == tileStartX;
      if(isX)
      {
        tiled.TileStart = new Point(value, tiled.TileStart.Y);
      }
      else
      {
        tiled.TileStart = new Point(tiled.TileStart.X, value);
      }

      renderPanel.InvalidateRender();
    }

    void tileStride_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      TextBox input = (TextBox)sender;
      int value;
      if(!int.TryParse(input.Text, out value) || value == 0)
      {
        e.Cancel = true;
      }
    }

    void tileStride_Validated(object sender, EventArgs e)
    {
      TiledImageMap tiled = (TiledImageMap)imageMap;
      int value = int.Parse(((TextBox)sender).Text);
      bool isWidth = sender == tileStrideX;
      if(isWidth)
      {
        tiled.TileStride = new Size(value, tiled.TileStride.Height);
      }
      else
      {
        tiled.TileStride = new Size(tiled.TileStride.Width, value);
      }

      renderPanel.InvalidateRender();
    }

    void btnBackColor_Click(object sender, EventArgs e)
    {
      ColorDialog cd = new ColorDialog();
      cd.AnyColor = true;
      cd.Color = renderPanel.BackColor;
      if(cd.ShowDialog() == DialogResult.OK)
      {
        renderPanel.BackColor = cd.Color;
        renderPanel.InvalidateRender();
      }
    }

    void btnZoomIn_Click(object sender, EventArgs e)
    {
      tileZoom++;
      renderPanel.InvalidateRender();
    }

    void btnZoomOut_Click(object sender, EventArgs e)
    {
      if(tileZoom > 1) tileZoom--;
      renderPanel.InvalidateRender();
    }

    ImageMap imageMap;
    int tileZoom;
    bool initialized;
  }
}