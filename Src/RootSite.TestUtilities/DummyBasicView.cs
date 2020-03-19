// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FieldWorks.TestUtilities;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;

namespace RootSite.TestUtilities
{
	/// <summary>
	/// Implementation of a basic view for testing, similar to DraftView
	/// </summary>
	public class DummyBasicView : SIL.FieldWorks.Common.RootSites.RootSite
	{
		#region Data members
		/// <summary />
		protected System.ComponentModel.IContainer components;
		/// <summary />
		protected VwBaseVc m_basicViewVc;
		/// <summary />
		protected SelectionHelper m_SelectionHelper;
		/// <summary>Text for the first and third test paragraph (English)</summary>
		internal const string kFirstParaEng = "This is the first test paragraph.";
		/// <summary>Text for the second and fourth test paragraph (English).</summary>
		/// <remarks>This text needs to be shorter than the text for the first para!</remarks>
		internal const string kSecondParaEng = "This is the 2nd test paragraph.";
		private int m_hvoRoot;
		private int m_flid;
		private Point m_scrollPosition = new Point(0, 0);
		#endregion

		#region Constructor, Dispose, InitializeComponent

		/// <summary />
		public DummyBasicView() : base(null)
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary />
		public DummyBasicView(int hvoRoot, int flid) : base(null)
		{
			m_hvoRoot = hvoRoot;
			m_flid = flid;

			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			base.Dispose(disposing);

			if (disposing)
			{
				components?.Dispose();
				var disposable = m_basicViewVc as IDisposable;
				disposable?.Dispose();
			}
			m_basicViewVc = null;
			m_SelectionHelper = null;
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.SuspendLayout();
			//
			// DummyBasicView
			//
			this.Name = "DummyBasicView";
			this.ResumeLayout(false);

		}
		#endregion
		#endregion

		#region Event handling methods
		/// <summary>
		/// Activates the view
		/// </summary>
		public virtual void ActivateView()
		{
			PerformLayout();
			Show();
			Focus();
		}

		/// <summary>
		/// Simulate a scroll to end
		/// </summary>
		public override void ScrollToEnd()
		{
			base.ScrollToEnd();
			RootBox.MakeSimpleSel(false, true, false, true);
			PerformLayout();
		}

		/// <summary>
		/// Simulate scrolling to top
		/// </summary>
		public override void ScrollToTop()
		{
			base.ScrollToTop();
			// The actual DraftView code for handling Ctrl-Home doesn't contain this method call.
			// The call to CallOnExtendedKey() in OnKeyDown() handles setting the IP.
			RootBox.MakeSimpleSel(true, true, false, true);
		}

		/// <summary>
		/// Calls the OnKeyDown method.
		/// </summary>
		public void CallOnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
		}
		#endregion

		#region Overrides of Control methods
		/// <summary>
		/// Focus got set to the draft view
		/// </summary>
		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);

			if (DesignMode || !m_fRootboxMade)
			{
				return;
			}
			if (m_SelectionHelper != null)
			{
				m_SelectionHelper.SetSelection(this);
				m_SelectionHelper = null;
			}
		}

		/// <summary>
		/// For AdjustScrollRangeTestHScroll we need the dummy to allow horizontal scrolling.
		/// </summary>
		protected override bool WantHScroll => true;

		/// <summary>
		/// Recompute the layout
		/// </summary>
		protected override void OnLayout(LayoutEventArgs levent)
		{
			if (!SkipLayout)
			{
				base.OnLayout(levent);
			}
		}
		#endregion

		/// <summary>
		/// Call the OnLayout methods
		/// </summary>
		public virtual void CallLayout()
		{
			OnLayout(new LayoutEventArgs(this, string.Empty));
		}

		/// <summary>
		/// Add English paragraphs
		/// </summary>
		public void MakeEnglishParagraphs()
		{
			var wsf = m_cache.WritingSystemFactory;
			var wsEng = wsf.GetWsFromStr("en");
			AddParagraphsToLangProj(wsEng, kFirstParaEng, kSecondParaEng);
		}

		/// <summary>
		/// Adds paragraphs to the database
		/// </summary>
		private void AddParagraphsToLangProj(int ws, string firstPara, string secondPara)
		{
			var servLoc = Cache.ServiceLocator;
			var txt = servLoc.GetInstance<ITextFactory>().Create();
			var text = servLoc.GetInstance<IStTextFactory>().Create();
			txt.ContentsOA = text;
			var stTxtParaFactory = servLoc.GetInstance<IStTxtParaFactory>();
			var para1 = stTxtParaFactory.Create();
			text.ParagraphsOS.Add(para1);
			var para2 = stTxtParaFactory.Create();
			text.ParagraphsOS.Add(para2);

			var tss = TsStringUtils.MakeString(firstPara, ws);
			para1.Contents = tss;

			tss = TsStringUtils.MakeString(secondPara, ws);
			para2.Contents = tss;
		}

		/// <summary>
		/// Exposes the OnKillFocus method to testing.
		/// </summary>
		public void KillFocus(Control newWindow)
		{
			OnKillFocus(newWindow, true);
		}

		#region Overrides of RootSite
		/// <summary>
		/// Since we don't really show this window, it doesn't have a working AutoScrollPosition;
		/// but to test making the selection visible, we have to remember what the view tries to
		/// change it to.
		/// </summary>
		public override Point ScrollPosition
		{
			get => m_scrollPosition;
			set => m_scrollPosition = new Point(-value.X, -value.Y);
		}

		/// <summary>
		/// Provides direct access to the base's MakeRoot() method.
		/// </summary>
		/// <remarks>This is needed in derived classes. See
		/// Test\AcceptanceTests\Common\RootSite\DummyDraftView.cs</remarks>
		protected void BaseMakeRoot()
		{
			base.MakeRoot();
		}

		/// <summary>
		/// Makes a root box and initializes it with appropriate data
		/// </summary>
		public void MakeRoot(int hvoRoot, int flid)
		{
			MakeRoot(hvoRoot, flid, 1);
		}

		/// <summary>
		/// Makes a root box and initializes it with appropriate data
		/// </summary>
		/// <param name="hvoRoot">Hvo of the root object</param>
		/// <param name="flid">Flid in which hvoRoot contains a sequence of StTexts</param>
		/// <param name="frag">Fragment for view constructor</param>
		public void MakeRoot(int hvoRoot, int flid, int frag)
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}
			base.MakeRoot();

			// Set up a new view constructor.
			m_basicViewVc = CreateVc(flid);
			m_basicViewVc.DefaultWs = m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle;

			RootBox.DataAccess = m_cache.DomainDataByFlid;
			RootBox.SetRootObject(hvoRoot, m_basicViewVc, frag, m_styleSheet);

			m_fRootboxMade = true;
			m_dxdLayoutWidth = kForceLayout;
			// Don't try to draw until we get OnSize and do layout.

			// Added this to keep from Asserting if the user tries to scroll the draft window
			// before clicking into it to place the insertion point.
			try
			{
				RootBox.MakeSimpleSel(true, true, false, true);
			}
			catch (COMException)
			{
				// We ignore failures since the text window may be empty, in which case making a
				// selection is impossible.
			}
		}

		/// <summary>
		/// Creates the view constructor.
		/// </summary>
		protected virtual VwBaseVc CreateVc(int flid)
		{
			return new DummyBasicViewVc(MyDisplayType, flid);
		}

		/// <summary>
		/// Makes a root box and initializes it with appropriate data
		/// </summary>
		public override void MakeRoot()
		{
			MakeRoot(m_hvoRoot, m_flid);
		}

		/// <summary>
		/// Creates a new DummyEditingHelper
		/// </summary>
		protected override EditingHelper CreateEditingHelper()
		{
			return new DummyEditingHelper(m_cache, this);
		}
		#endregion

		#region Properties
		/// <summary>
		/// Gets or sets the type of boxes to display: lazy or non-lazy or both
		/// </summary>
		public DisplayType MyDisplayType { get; set; }

		/// <summary>
		/// Gets the draft view's selection helper object
		/// </summary>
		public SelectionHelper SelectionHelper => m_SelectionHelper;

		/// <summary>
		/// Gets or sets a flag if OnLayout should be skipped.
		/// </summary>
		public bool SkipLayout { get; set; } = false;

		/// <summary>
		/// Gets the view constructor.
		/// </summary>
		public VwBaseVc ViewConstructor => m_basicViewVc;
		#endregion

		/// <summary />
		public void CallRootSiteOnKeyDown(KeyEventArgs e)
		{
			OnKeyDown(e);
		}

		/// <summary>
		/// Check for presence of proper paragraph properties.
		/// </summary>
		/// <param name="vwsel">[out] The selection</param>
		/// <param name="hvoText">[out] The HVO</param>
		/// <param name="tagText">[out] The tag</param>
		/// <param name="vqvps">[out] The paragraph properties</param>
		/// <param name="ihvoAnchor">[out] Start index of selection</param>
		/// <param name="ihvoEnd">[out] End index of selection</param>
		/// <returns>Return <c>false</c> if neither selection nor paragraph property. Otherwise
		/// return <c>true</c>.</returns>
		public bool IsParagraphProps(out IVwSelection vwsel, out int hvoText, out int tagText, out IVwPropertyStore[] vqvps, out int ihvoAnchor, out int ihvoEnd)
		{
			vwsel = null;
			hvoText = 0;
			tagText = 0;
			vqvps = null;
			ihvoAnchor = 0;
			ihvoEnd = 0;

			return EditingHelper.IsParagraphProps(out vwsel, out hvoText, out tagText, out vqvps, out ihvoAnchor, out ihvoEnd);
		}

		/// <summary>
		/// Get the view selection and paragraph properties.
		/// </summary>
		/// <param name="vwsel">[out] The selection</param>
		/// <param name="hvoText">[out] The HVO</param>
		/// <param name="tagText">[out] The tag</param>
		/// <param name="vqvps">[out] The paragraph properties</param>
		/// <param name="ihvoFirst">[out] Start index of selection</param>
		/// <param name="ihvoLast">[out] End index of selection</param>
		/// <param name="vqttp">[out] The style rules</param>
		/// <returns>Return false if there is neither a selection nor a paragraph property.
		/// Otherwise return true.</returns>
		public bool GetParagraphProps(out IVwSelection vwsel, out int hvoText,
			out int tagText, out IVwPropertyStore[] vqvps, out int ihvoFirst, out int ihvoLast, out ITsTextProps[] vqttp)
		{
			return EditingHelper.GetParagraphProps(out vwsel, out hvoText, out tagText, out vqvps, out ihvoFirst, out ihvoLast, out vqttp);
		}

		/// <summary>
		/// Handle a key press.
		/// </summary>
		public void HandleKeyPress(char keyChar)
		{
			using (new HoldGraphics(this))
			{
				EditingHelper.HandleKeyPress(keyChar, ModifierKeys);
			}
		}

		/// <summary>
		/// Provides access to <see cref="SimpleRootSite.GetCoordRects"/>.
		/// </summary>
		public new void GetCoordRects(out Rectangle rcSrcRoot, out Rectangle rcDstRoot)
		{
			rcSrcRoot = Rectangle.Empty;
			rcDstRoot = Rectangle.Empty;
			base.GetCoordRects(out rcSrcRoot, out rcDstRoot);
		}

		/// <summary>
		/// Provides access to <see cref="SimpleRootSite.AdjustScrollRange1"/>.
		/// </summary>
		public bool AdjustScrollRange(int dxdSize, int dxdPosition, int dydSize, int dydPosition)
		{
			return base.AdjustScrollRange1(dxdSize, dxdPosition, dydSize, dydPosition);
		}

		/// <summary>
		/// Provides access to <see cref="ScrollableControl.VScroll"/>
		/// </summary>
		public new bool VScroll
		{
			get => base.VScroll;
			set => base.VScroll = value;
		}

		/// <summary>
		/// Provides access to <see cref="ScrollableControl.HScroll"/>
		/// </summary>
		public new bool HScroll
		{
			get => base.HScroll;
			set => base.HScroll = value;
		}

		/// <summary>
		/// Gets the height of the selection.
		/// </summary>
		public int SelectionHeight
		{
			get
			{
				var nLineHeight = 0;
				using (new HoldGraphics(this))
				{
					GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
					var vwsel = RootBox.Selection;
					if (vwsel != null)
					{
						vwsel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out var rdIP, out _, out _, out _);
						nLineHeight = rdIP.bottom - rdIP.top;
					}
				}

				return nLineHeight;
			}
		}

		/// <summary>
		/// Gets the width of the selection.
		/// </summary>
		public int SelectionWidth
		{
			get
			{
				var nSelWidth = 0;
				using (new HoldGraphics(this))
				{
					GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
					var vwsel = RootBox.Selection;
					if (vwsel != null)
					{
						vwsel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out var rdIP, out _, out _, out _);
						nSelWidth = rdIP.right - rdIP.left;
					}
				}

				return nSelWidth;
			}
		}

		/// <summary>
		/// Provides access to EditingHelper.ApplyStyle
		/// </summary>
		public void ApplyStyle(string sStyleToApply)
		{
			EditingHelper.ApplyStyle(sStyleToApply);
		}

		/// <summary>
		/// Provides access to <see cref="SimpleRootSite.OnMouseDown"/>
		/// </summary>
		public void CallOnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
		}

		/// <summary>
		/// Provides access to <see cref="SimpleRootSite.OnMouseUp"/>
		/// </summary>
		public void CallOnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
		}

		/// <summary />
		public void SetRootBox(IVwRootBox rootb)
		{
			RootBox = rootb;
		}
	}
}