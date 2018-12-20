// Copyright (c) 2003-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;

namespace LanguageExplorer.DictionaryConfiguration
{
	/// <summary />
	public class ImageHolder : UserControl
	{
		public System.Windows.Forms.ImageList largeImages;
		public System.Windows.Forms.ImageList smallImages;
		public System.Windows.Forms.ImageList smallCommandImages;
		private System.Windows.Forms.Button button1;
		private System.ComponentModel.IContainer components;

		/// <summary />
		public ImageHolder()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType() + ". ****** ");
			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}

			if( disposing )
			{
				components?.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImageHolder));
			this.largeImages = new System.Windows.Forms.ImageList(this.components);
			this.smallImages = new System.Windows.Forms.ImageList(this.components);
			this.smallCommandImages = new System.Windows.Forms.ImageList(this.components);
			this.button1 = new System.Windows.Forms.Button();
			this.SuspendLayout();
			//
			// largeImages
			//
			this.largeImages.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("largeImages.ImageStream")));
			this.largeImages.TransparentColor = System.Drawing.Color.Fuchsia;
			this.largeImages.Images.SetKeyName(0, "");
			this.largeImages.Images.SetKeyName(1, "");
			this.largeImages.Images.SetKeyName(2, "");
			this.largeImages.Images.SetKeyName(3, "");
			this.largeImages.Images.SetKeyName(4, "");
			this.largeImages.Images.SetKeyName(5, "");
			this.largeImages.Images.SetKeyName(6, "");
			this.largeImages.Images.SetKeyName(7, "Lexicon 32.ico");
			this.largeImages.Images.SetKeyName(8, "Lists 32.ico");
			this.largeImages.Images.SetKeyName(9, "Texts 32.ico");
			this.largeImages.Images.SetKeyName(10, "Words 32.ico");
			this.largeImages.Images.SetKeyName(11, "Grammar 32.ico");
			//
			// smallImages
			//
			this.smallImages.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("smallImages.ImageStream")));
			this.smallImages.TransparentColor = System.Drawing.Color.Fuchsia;
			this.smallImages.Images.SetKeyName(0, "");
			this.smallImages.Images.SetKeyName(1, "");
			this.smallImages.Images.SetKeyName(2, "");
			this.smallImages.Images.SetKeyName(3, "");
			this.smallImages.Images.SetKeyName(4, "");
			this.smallImages.Images.SetKeyName(5, "");
			this.smallImages.Images.SetKeyName(6, "");
			this.smallImages.Images.SetKeyName(7, "");
			this.smallImages.Images.SetKeyName(8, "");
			this.smallImages.Images.SetKeyName(9, "ExternalLink.bmp");
			this.smallImages.Images.SetKeyName(10, "SideBySideView.bmp");
			//
			// smallCommandImages
			//
			this.smallCommandImages.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("smallCommandImages.ImageStream")));
			this.smallCommandImages.TransparentColor = System.Drawing.Color.Fuchsia;
			this.smallCommandImages.Images.SetKeyName(0, "");
			this.smallCommandImages.Images.SetKeyName(1, "");
			this.smallCommandImages.Images.SetKeyName(2, "");
			this.smallCommandImages.Images.SetKeyName(3, "");
			this.smallCommandImages.Images.SetKeyName(4, "");
			this.smallCommandImages.Images.SetKeyName(5, "");
			this.smallCommandImages.Images.SetKeyName(6, "");
			this.smallCommandImages.Images.SetKeyName(7, "");
			this.smallCommandImages.Images.SetKeyName(8, "");
			this.smallCommandImages.Images.SetKeyName(9, "");
			this.smallCommandImages.Images.SetKeyName(10, "");
			this.smallCommandImages.Images.SetKeyName(11, "");
			this.smallCommandImages.Images.SetKeyName(12, "MoveUp.bmp");
			this.smallCommandImages.Images.SetKeyName(13, "MoveRight.bmp");
			this.smallCommandImages.Images.SetKeyName(14, "MoveDown.bmp");
			this.smallCommandImages.Images.SetKeyName(15, "MoveLeft.bmp");
			this.smallCommandImages.Images.SetKeyName(16, "columnChooser.ico");
			//
			// button1
			//
			this.button1.ImageIndex = 6;
			this.button1.ImageList = this.largeImages;
			this.button1.Location = new System.Drawing.Point(32, 40);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(75, 56);
			this.button1.TabIndex = 0;
			this.button1.Text = "button1";
			this.button1.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
			//
			// ImageHolder
			//
			this.Controls.Add(this.button1);
			this.Name = "ImageHolder";
			this.ResumeLayout(false);

		}
		#endregion
	}
}
