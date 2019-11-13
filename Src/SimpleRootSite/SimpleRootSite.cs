// Copyright (c) 2004-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Accessibility;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.Keyboarding;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;
using SIL.Windows.Forms.Keyboarding;
using SIL.Windows.Forms.Keyboarding.Windows;
using Win32 = SIL.FieldWorks.Common.FwUtils.Win32;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Base class for hosting a view in an application.
	/// </summary>
	public class SimpleRootSite : UserControl, IVwRootSite, IRootSite, IFlexComponent, IEditingCallbacks, IReceiveSequentialMessages, IMessageFilter
	{
		/// <summary>
		/// This event notifies you that the right mouse button was clicked,
		/// and gives you the click location, and the selection at that point.
		/// </summary>
		public event FwRightMouseClickEventHandler RightMouseClickedEvent;

		/// <summary>This event gets fired when the AutoScrollPosition value changes</summary>
		public event ScrollPositionChanged VerticalScrollPositionChanged;

		#region Member variables
		/// <summary>Value for the available width to tell the view that we want to do a
		/// layout</summary>
		public const int kForceLayout = -50000;
		/// <summary>Tag we use for user prompts</summary>
		/// <remarks>The process for setting up a user prompt has several aspects.
		/// 1. When displaying the property, if it is empty, call vwenv.AddProp(SimpleRootSite.kTagUserPrompt, this, frag);
		/// Also NoteDependency on the empty property.
		/// 2. Optionally, if it is NOT empty, to make the prompt appear if it becomes empty,
		/// use NoteStringValDependency to get a regenerate when the value is no longer empty.
		/// 3. Implement DisplayVariant to recognize kTagUserPrompt and insert the appropriate prompt.
		/// Usually this is all DisplayVariant is used for, so the frag argument can be used in any convenient way,
		/// for example, to indicate which prompt, or which writing system.
		/// Typically, the entire prompt should be given the property ktptUserPrompt (this suppresses various formatting commands).
		/// Typically, the text of the prompt should be given the ktptSpellCheck/DoNotCheck property.
		/// At the start of the prompt, insert "\u200B" (a zero-width space) in the desired writing system; this ensures
		/// that anything the user types will be in that writing system and that the right keyboard will be active.
		/// 4. Implement UpdateProp. See InterlinVc for an example. This is responsible to transfer any value typed
		/// over the prompt to the real property, and to restore the selection, which will be destroyed by
		/// updating the real property. It needs to save selection info, remove the kttpUserPrompt and doNotSpellCheck properties
		/// from what the user typed, set that as the value of the appropriate real property, and restore a selection that is
		/// similar but in the real property rather than in ktagUserPrompt. This should not be done while an IME composition
		/// is currently in progress (use IVwRootBox.IsCompositionInProgress), otherwise the composition will be terminated
		/// when the real property is updated and the selection is destroyed.
		/// 5. Implement SelectionChanged on the parent rootsite to notice that the selection is in a prompt
		/// and extend the selection to the whole property. The selection should not be extended while an IME composition
		/// is in progress, since the user prompt is about to be replaced by the real property.
		/// </remarks>
		public const int kTagUserPrompt = 1000000001;

		/// <summary>Property for indicating user prompt strings.
		/// This is an arbitrary number above 10,000.  Property numbers above 10,000 are
		/// "user-defined" and will be ignored by the property store.</summary>
		public const int ktptUserPrompt = 10537;

		/// <summary>If 0 we allow OnPaint to execute, if non-zero we don't perform OnPaint.
		/// This is used to prevent redraws from happening while we do a RefreshDisplay.
		/// This also sends the WM_SETREDRAW message.
		/// </summary>
		/// <remarks>Access to this variable should only be done through the property
		/// AllowPaint.</remarks>
		private int m_nAllowPaint;

		/// <summary>
		/// Subclasses can set this flag to keep SimpleRootSite from handling OnPrint.
		/// </summary>
		protected bool SuppressPrintHandling { get; set; }

		/// <summary>
		/// This allows storing of the AutoScrollPosition when AllowPainting == false
		/// as on Mono Setting AutoScrollPosition causes a redraw even when AllowPainting == false
		/// </summary>
		private Point? cachedAutoScrollPosition;

		/// <summary>Used to draw the rootbox</summary>
		private IVwDrawRootBuffered m_vdrb;
		private bool m_haveCachedDrawForDisabledView;

		//The message filter is a major kludge to prevent a spurious WM_KEYUP for VK_CONTROL from
		// interrupting a mouse click.  We remember when it is installed with this bool.
		private bool m_messageFilterInstalled;

		/// <summary>
		/// This variable is set at the start of OnGotFocus, and thus notes the root site that
		/// most recently received the input focus. It is cleared to null at the end of
		/// OnLostFocus...but ONLY if it is equal to this. Reason: OnGotFocus for the new
		/// focus window apparently can happen BEFORE OnLostFocus for the old window. We detect
		/// that situation by the fact that, in OnLostFocus, this != g_focusRootSite.Target.
		/// We use a WeakReference just in case so that if for some reason this doesn't get
		/// set to null we don't keep a rootsite around.
		/// </summary>
		private static WeakReference g_focusRootSite = new WeakReference(null);
		private IContainer components;
		/// <summary>True to allow layouts to take place, false otherwise (We use this instead
		/// of SuspendLayout because SuspendLayout didn't work)</summary>
		protected bool m_fAllowLayout = true;

		/// <summary>True if we are waiting to do a refresh on the view (will be done when the view
		/// becomes visible); false otherwise</summary>
		protected bool m_fRefreshPending;

		/// <summary>True to show range selections when focus is lost; false otherwise</summary>
		protected bool m_fShowRangeSelAfterLostFocus;
		/// <summary>True if this is a "text box" (or "combo box"); false otherwise</summary>
		protected bool m_fIsTextBox;

		/// <summary>True if <see cref="MakeRoot"/> was called</summary>
		protected bool m_fRootboxMade;

		/// <summary>Manages the VwGraphics creation and usage</summary>
		protected GraphicsManager m_graphicsManager;

		/// <summary>handler for typing and other edit requests</summary>
		protected EditingHelper m_editingHelper;
		/// <summary />
		private ToolStripMenuItem m_printMenu;

		/// <summary>
		/// A writing system factory used to interpret data in the view. Subclasses of
		/// SimpleRootSite should set this before doing much with the root site. The RootSite
		/// subclass can obtain one automatically from its LcmCache.
		/// </summary>
		protected ILgWritingSystemFactory m_wsf;

		/// <summary>The width returned by GetAvailWidth() when the root box was last laid out,
		/// or a large negative number if it has never been successfully laid out.</summary>
		protected int m_dxdLayoutWidth;

		/// <summary>The zoom ratio</summary>
		protected float m_Zoom = 1;

		/// <summary>height of an optional fixed header at the top of the client window.</summary>
		protected int m_dyHeader;

		/// <summary>list of drawing err messages that have been shown to the user</summary>
		protected static List<string> s_vstrDrawErrMsgs = new List<string>();

		// This is used for the LinkedFiles Link tooltip.
		//HWND m_hwndExtLinkTool;

		private Timer m_Timer;
		private int m_nHorizMargin = 2;

		/// <summary>The style sheet</summary>
		protected IVwStylesheet m_styleSheet;
		/// <summary>
		/// Supports the LayoutSizeChanged event by maintaining a list of who wants it.
		/// </summary>
		public event EventHandler LayoutSizeChanged;
		/// <summary>
		/// Flag used to prevent mouse move events from entering CallMouseMoveDrag multiple
		/// times before prior ones have exited.  Otherwise we get lines displayed multiple
		/// times while scrolling during a selection.
		/// </summary>
		private bool m_fMouseInProcess;

		/// <summary>We seem to get spurious OnSizeChanged messages when the size didn't
		/// really change... ignore them.</summary>
		private Size m_sizeLast;

		/// <summary>This gets around a bug in OnGotFocus generating a WM_INPUTLANGCHANGE
		/// message with the previous language value when we want to set our own that we know.
		/// If the user causes this message, we do want to change language/keyboard, but not
		/// if OnGotFocus causes the message.</summary>
		private bool m_fHandlingOnGotFocus;

		/// <summary>
		/// This tells the rootsite whether to attempt to construct the rootbox automatically
		/// when the window handle is created. For simple views, this is generally desirable,
		/// but views which are part of synchronously scrolling groups usually have their root
		/// objects set explicitly at the same time as the other views in their group.
		/// </summary>
		protected bool m_fMakeRootWhenHandleIsCreated = true;

		private int m_lastVerticalScrollPosition = 1;
		private bool m_fDisposed;

		private OrientationManager m_orientationManager;
		private ISubscriber m_subscriber;

		/// <summary />
		protected bool IsVertical => m_orientationManager.IsVertical;

		/// <summary>
		/// We suppress MouseMove when a paint is pending, since we don't want mousemoved to ask
		/// about a lazy box under the cursor and start expanding things; this can lead to recursive
		/// calls to paint, and hence to recursive calls to expand, and all kinds of pain (e.g., FWR-3103).
		/// </summary>
		public bool MouseMoveSuppressed { get; private set; }

		#endregion

		#region Constructor, Dispose, Designer generated code
		/// <inheritdoc />
		public SimpleRootSite()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			m_dxdLayoutWidth = kForceLayout; // Unlikely to be real current window width!
			WsPending = -1;
			BackColor = SystemColors.Window;
			Sequencer = new MessageSequencer(this);
			m_graphicsManager = CreateGraphicsManager();
			m_orientationManager = CreateOrientationManager();
			if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
			{
				SubscribeToRootSiteEventHandlerEvents();
			}
		}

		/// <summary>
		/// Creates the root site event handler.
		/// </summary>
		private void SubscribeToRootSiteEventHandlerEvents()
		{
			if (!KeyboardController.IsInitialized || RootSiteEventHandler != null)
			{
				return;
			}
			if (Platform.IsWindows)
			{
				RootSiteEventHandler = new WindowsLanguageProfileSink(this);
			}
			else
			{
				RootSiteEventHandler = new IbusRootSiteEventHandler(this);
			}
			KeyboardController.RegisterControl(this, RootSiteEventHandler);
		}

		private void UnsubscribeFromRootSiteEventHandlerEvents()
		{
			if (!KeyboardController.IsInitialized || RootSiteEventHandler == null)
			{
				return;
			}
			KeyboardController.UnregisterControl(this);
			RootSiteEventHandler = null;
		}

		/// <summary>
		/// Gets the root site event handler.
		/// </summary>
		protected internal object RootSiteEventHandler { get; protected set; }

		/// <summary>
		/// The default creates a normal horizontal orientation manager. Override to create one of the other
		/// classes as needed.
		/// </summary>
		/// <returns></returns>
		protected virtual OrientationManager CreateOrientationManager()
		{
			return new OrientationManager(this);
		}

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		~SimpleRootSite()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + " ******************");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				if (Subscriber != null)
				{
					Subscriber.Unsubscribe(FwUtils.FwUtils.AboutToFollowLink, AboutToFollowLink);
					Subscriber.Unsubscribe(FwUtils.FwUtils.WritingSystemHvo, WritingSystemHvo_Changed);
				}

				if (m_printMenu != null)
				{
					m_printMenu.Click -= Print_Click;
					m_printMenu.Enabled = false;
				}

				// Do this here, before disposing m_messageSequencer,
				// as we still get messages during dispose.
				// Once the the base class has shut down the window handle,
				// we are good to go on.
				// If we find we getting messages during this call,
				// we will be forced to call DestroyHandle() first.
				// That is done part way through the base method code,
				// but it may not be soon enough, for all the re-entrant events
				// that keep showing up.
				m_fAllowLayout = false;
				DestroyHandle();
			}

			base.Dispose(disposing);

			if (disposing)
			{
				UnsubscribeFromRootSiteEventHandlerEvents();

				if (RootBox != null)
				{
					CloseRootBox();
				}
				if (m_Timer != null)
				{
					m_Timer.Stop();
					m_Timer.Tick -= OnTimer;
					m_Timer.Dispose();
				}
				// Remove the filter when we are disposed now.
				if (m_messageFilterInstalled)
				{
					Application.RemoveMessageFilter(this);
					m_messageFilterInstalled = false;
				}
				m_editingHelper?.Dispose();
				Sequencer?.Dispose();
				if (m_graphicsManager != null)
				{
					// Uninit() first in case we're in the middle of displaying something.  See LT-7365.
					m_graphicsManager.Uninit();
					m_graphicsManager.Dispose();
				}
				components?.Dispose();
			}

			if (m_vdrb != null && Marshal.IsComObject(m_vdrb))
			{
				Marshal.ReleaseComObject(m_vdrb);
			}
			m_vdrb = null;
			if (m_styleSheet != null && Marshal.IsComObject(m_styleSheet))
			{
				Marshal.ReleaseComObject(m_styleSheet);
			}
			if (RootBox != null && Marshal.IsComObject(RootBox))
			{
				Marshal.ReleaseComObject(RootBox);
			}
			RootBox = null;
			m_styleSheet = null;
			m_graphicsManager = null;
			m_editingHelper = null;
			m_Timer = null;
			m_wsf = null;
			Sequencer = null;

			PropertyTable = null;
			Publisher = null;
			Subscriber = null;

			m_fDisposed = true;
		}

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new Container();
			m_Timer = new Timer(components);
			Name = "SimpleRootSite";
		}
		#endregion

		#region Properties
		/// <summary>
		/// Gets an image that can be used to represent graphically an image that cannot be
		/// found (similar to what IE does).
		/// </summary>
		public static Bitmap ImageNotFoundX => Properties.Resources.ImageNotFoundX;

		/// <summary>
		/// Creates the graphics manager.
		/// </summary>
		/// <remarks>We do this in a method for testing.</remarks>
		/// <returns>A new graphics manager.</returns>
		protected virtual GraphicsManager CreateGraphicsManager()
		{
			return new GraphicsManager(this);
		}

		/// <summary>
		/// Determines whether it's possible to do meaningful layout.
		/// </summary>
		protected virtual bool OkayToLayOut => OkayToLayOutAtCurrentWidth;

		/// <summary>
		/// If layout width is less than 2 pixels, probably the window has not received its
		/// initial OnSize message yet, and we can't do a meaningful layout.
		/// </summary>
		protected bool OkayToLayOutAtCurrentWidth => m_dxdLayoutWidth >= 2 || m_dxdLayoutWidth == kForceLayout;

		/// <summary>
		/// Gets/sets whether or not to show range selections when focus is lost
		/// </summary>
		public virtual bool ShowRangeSelAfterLostFocus
		{
			get
			{
				return m_fShowRangeSelAfterLostFocus;
			}
			set
			{
				m_fShowRangeSelAfterLostFocus = value;
				if (!Focused && RootBox != null)
				{
					UpdateSelectionEnabledState(null);
				}
			}
		}

		/// <summary>
		/// Gets/sets whether or not this is a "TextBox" (possibly embedded in a "ComboBox").
		/// </summary>
		public bool IsTextBox
		{
			get
			{
				return m_fIsTextBox;
			}
			set
			{
				m_fIsTextBox = value;
				if (!Focused && RootBox != null)
				{
					UpdateSelectionEnabledState(null);
				}
			}
		}

		/// <summary>
		/// Gets or sets the horizontal margin.
		/// Note: this is always considered the margin to either SIDE of the text; thus, in
		/// effect it becomes a vertical margin when displaying vertical text.
		/// </summary>
		[DefaultValue(2)]
		protected internal virtual int HorizMargin
		{
			get
			{
				return m_nHorizMargin;
			}
			set
			{
				m_nHorizMargin = value;
			}
		}

		/// <summary>
		/// The paragraph style name for the current selection.
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual string CurrentParagraphStyle => DesignMode ? string.Empty : EditingHelper.GetParaStyleNameFromSelection();

		/// <summary />
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual bool AllowLayout
		{
			get
			{
				return m_fAllowLayout;
			}
			set
			{
				m_fAllowLayout = value;
				if (m_fAllowLayout)
				{
					PerformLayout();
				}
			}
		}

		/// <summary>
		/// If we need to make a selection, but we can't because edits haven't been updated in the
		/// view, this method requests creation of a selection after the unit of work is complete.
		/// Derived classes should implement this if they have any hope of supporting multi-
		/// paragraph editing.
		/// </summary>
		/// <param name="rootb">The rootbox</param>
		/// <param name="ihvoRoot">Index of root element</param>
		/// <param name="cvlsi">count of levels</param>
		/// <param name="rgvsli">levels</param>
		/// <param name="tagTextProp">tag or flid of property containing the text (TsString)</param>
		/// <param name="cpropPrevious">number of previous occurrences of the text property</param>
		/// <param name="ich">character offset into the text</param>
		/// <param name="wsAlt">The id of the writing system for the selection.</param>
		/// <param name="fAssocPrev">Flag indicating whether to associate the insertion point
		/// with the preceding character or the following character</param>
		/// <param name="selProps">The selection properties.</param>
		public virtual void RequestSelectionAtEndOfUow(IVwRootBox rootb, int ihvoRoot, int cvlsi, SelLevInfo[] rgvsli,
			int tagTextProp, int cpropPrevious, int ich, int wsAlt, bool fAssocPrev, ITsTextProps selProps)
		{
			throw new NotSupportedException("Method RequestSelectionAtEndOfUow is not supported in the base class.");
		}

		/// <summary>
		/// If we need to make a selection, but we can't because edits haven't been updated in
		/// the view, this method requests creation of a selection after the unit of work is
		/// complete. It will also scroll the selection into view.
		/// Derived classes should implement this if they have any hope of supporting multi-
		/// paragraph editing.
		/// </summary>
		/// <param name="helper">The selection to restore</param>
		public virtual void RequestVisibleSelectionAtEndOfUow(SelectionHelper helper)
		{
			throw new NotSupportedException("Method RequestSelectionAtEndOfUow is not supported in the base class.");
		}

		/// <summary>
		/// Gets the root box
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public IVwRootBox RootBox { get; set; }

		/// <summary>
		/// Gets or sets the associated style sheet
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual IVwStylesheet StyleSheet
		{
			get
			{
				return m_styleSheet;
			}
			set
			{
				if (m_styleSheet == value)
				{
					return;
				}
				if (m_styleSheet != null && Marshal.IsComObject(m_styleSheet))
				{
					Marshal.ReleaseComObject(m_styleSheet);
				}
				m_styleSheet = value;
				if (RootBox != null)
				{
					// Clean up. This code assumes there is only one root, almost universally true.
					// If not, either make sure the stylesheet is set early, or override.
					int hvoRoot, frag;
					IVwViewConstructor vc;
					IVwStylesheet ss;
					RootBox.GetRootObject(out hvoRoot, out vc, out frag, out ss);
					RootBox.SetRootObject(hvoRoot, vc, frag, m_styleSheet);
				}
			}
		}

		/// <summary>
		/// Gets the location of the insertion point.
		/// NOTE: This is the point relative to the top of visible area not the location with
		/// in the rootsite.
		/// </summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Point IPLocation
		{
			get
			{
				if (RootBox != null && !DesignMode)
				{
					var vwsel = RootBox.Selection;
					if (vwsel != null)
					{
						// insertion point location is actually the endpoint of a span
						if (vwsel.IsRange)
						{
							vwsel = vwsel.EndPoint(true);
							Debug.Assert(vwsel != null);
						}
						using (new HoldGraphics(this))
						{
							Rectangle rcSrcRoot, rcDstRoot;
							Rect rcSec, rcPrimary;
							bool fSplit, fEndBeforeAnchor;
							GetCoordRects(out rcSrcRoot, out rcDstRoot);
							vwsel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSec, out fSplit, out fEndBeforeAnchor);
							return new Point((rcPrimary.right + rcPrimary.left) / 2, (rcPrimary.top + rcPrimary.bottom) / 2);
						}
					}
				}

				return Point.Empty;
			}
		}

		/// <summary>
		/// Gets or sets a value that tells this view that it is entirely read-only or not.
		/// If you call this before m_rootb is set (e.g., during an override of MakeRoot) and
		/// set it to false, consider setting MaxParasToScan to zero on the root box.
		/// </summary>
		public bool ReadOnlyView
		{
			get
			{
				return !EditingHelper.Editable;
			}
			set
			{
				// check if this property will actually change
				if (EditingHelper.Editable == !value)
				{
					return;
				}
				// If this is read-only, it should not try to handle keyboard input in general.
				if (EditingHelper.Editable && value)
				{
					UnsubscribeFromRootSiteEventHandlerEvents();
				}
				else if (!EditingHelper.Editable && !value)
				{
					SubscribeToRootSiteEventHandlerEvents();
				}
				EditingHelper.Editable = !value;
				// If the view is read-only, we don't want to waste time looking for an
				// editable insertion point when moving the cursor with the cursor movement
				// keys.  Setting this value to 0 accomplishes this.
				if (value && RootBox != null)
				{
					RootBox.MaxParasToScan = 0;
				}
				// This allows read-only simple root sites embedded in dialogs not to trap tab keys that should move focus
				// elsewhere and return keys that should close the dialog.
				// It's not obvious, however, that every editable view should accept return; some may be one-liners.
				// So only mess with it when set true.
				if (value)
				{
					AcceptsReturn = AcceptsTab = false;
				}
			}
		}

		/// <summary>
		/// Indicates that we expect the view to have an editable field.
		/// </summary>
		internal bool IsEditable => !ReadOnlyView;

		/// <summary>
		/// Gets or sets the zoom multiplier that magnifies (or shrinks) the view.
		/// </summary>
		public virtual float Zoom
		{
			get
			{
				return m_Zoom;
			}
			set
			{
				m_Zoom = value;
				RefreshDisplay();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the root can be constructed in design mode.
		/// </summary>
		/// <value>The default implementation always returns <c>false</c>. Override in your
		/// derived class to allow MakeRoot being called when the view is opened in designer in
		/// Visual Studio.
		/// </value>
		protected virtual bool AllowPaintingInDesigner => false;

		/// <summary>
		/// (For internal use only.) Determine whether or not client posted a "FollowLink"
		/// message, in which case we are about to switch tools.
		/// </summary>
		private bool IsFollowLinkMsgPending { get; set; }

		/// <summary>
		/// Gets the data access (corresponds to a DB connection) for the rootbox of this
		/// rootsite.
		/// </summary>
		public virtual ISilDataAccess DataAccess => RootBox?.DataAccess;

		/// <summary>
		/// Helper used for processing editing requests.
		/// </summary>
		public EditingHelper EditingHelper
		{
			get
			{
				if (m_editingHelper == null)
				{
					m_editingHelper = CreateEditingHelper();
					OnEditingHelperCreated();
				}
				return m_editingHelper;
			}
		}

		/// <summary>
		/// Called when the editing helper is created.
		/// </summary>
		protected virtual void OnEditingHelperCreated()
		{
		}

		/// <summary>
		/// Creates a new EditingHelper of the proper type.
		/// </summary>
		protected virtual EditingHelper CreateEditingHelper()
		{
			return new EditingHelper(this);
		}

		/// <summary>
		/// Gets/sets the dpi.
		/// </summary>
		protected internal Point Dpi { get; set; } = new Point(96, 96);

		/// <summary>
		/// Usually Cursors.IBeam; overridden in vertical windows.
		/// </summary>
		internal Cursor IBeamCursor => m_orientationManager.IBeamCursor;
		#endregion // Properties

		#region Implementation of IVwRootSite

		/// <summary>
		/// Adjust the scroll range when some lazy box got expanded. Needs to be done for both
		/// panes if we have more than one.
		/// </summary>
		public bool AdjustScrollRange(IVwRootBox prootb, int dxdSize, int dxdPosition, int dydSize, int dydPosition)
		{
			return AdjustScrollRange1(dxdSize, dxdPosition, dydSize, dydPosition);
		}

		/// <summary>
		/// Cause the immediate update of the display of the root box. This should cause all pending
		/// paint operations to be done immediately, at least for the screen area occupied by the
		/// root box. It is typically called after processing key strokes, to ensure that the updated
		/// text is displayed before trying to process any subsequent keystrokes.
		/// </summary>
		public virtual void DoUpdates(IVwRootBox prootb)
		{
			Update();
		}

		/// <summary>
		/// Get the width available for laying things out in the view.
		/// Return the layout width for the window, depending on whether or not there is a
		/// scroll bar. If there is no scroll bar, we pretend that there is, so we don't have
		/// to keep adjusting the width back and forth based on the toggling on and off of
		/// vertical and horizontal scroll bars and their interaction.
		/// The return result is in pixels.
		/// The only common reason to override this is to answer instead a very large integer,
		/// which has the effect of turning off line wrap, as everything apparently fits on
		/// a line.
		/// N.B. If it is necessary to override this, it is not advisable to use Int32.MaxValue
		/// for fear of overflow caused by VwSelection::InvalidateSel() adjustments.
		/// </summary>
		public virtual int GetAvailWidth(IVwRootBox prootb)
		{
			// The default -4 allows two pixels right and left to keep data clear of the margins.
			return m_orientationManager.GetAvailWidth() - HorizMargin * 2;
		}

		/// <summary>
		/// Invalidate rectangle
		/// </summary>
		public virtual void InvalidateRect(IVwRootBox root, int xsLeft, int ysTop, int xsWidth, int ysHeight)
		{
			if (xsWidth <= 0 || ysHeight <= 0)
			{
				// empty rectangle, may not produce paint.
				// REVIEW: We found that InvalidateRect was being called twice with the same rectangle for
				// every keystroke.  We assume that this problem is originating within the rootbox code.
				return;
			}
			// Convert from coordinates relative to the root box to coordinates relative to
			// the current client rectangle.
			Rectangle rcSrcRoot, rcDstRoot;
			using (new HoldGraphics(this))
			{
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
			}
			var left = MapXTo(xsLeft, rcSrcRoot, rcDstRoot);
			var top = MapYTo(ysTop, rcSrcRoot, rcDstRoot);
			var right = MapXTo(xsLeft + xsWidth, rcSrcRoot, rcDstRoot);
			var bottom = MapYTo(ysTop + ysHeight, rcSrcRoot, rcDstRoot);
			CallInvalidateRect(m_orientationManager.RotateRectDstToPaint(new Rectangle(left, top, right - left, bottom - top)), true);
		}

		/// <summary>
		/// Get a graphics object in an appropriate state for drawing and measuring in the view.
		/// The calling method should pass the IVwGraphics back to ReleaseGraphics() before
		/// it returns. In particular, problems will arise if OnPaint() gets called before the
		/// ReleaseGraphics() method.
		/// </summary>
		/// <remarks>
		/// REVIEW JohnT(?): We probably need a better way to handle this. Most likely: make
		/// the VwGraphics object we cache a true COM object so its reference count is
		/// meaningful; have this method create a new one. Problem: a useable VwGraphics object
		/// has a device context that is linked to a particular window; if the window closes,
		/// the VwGraphics is not useable, whatever its reference count says. It may therefore
		/// be that we just need to allocate a copy in this method, leaving the member variable
		/// alone. Or, the current strategy may prove adequate.
		/// </remarks>
		public virtual void GetGraphics(IVwRootBox prootb, out IVwGraphics pvg, out Rect rcSrcRoot, out Rect rcDstRoot)
		{
			InitGraphics();
			pvg = m_graphicsManager.VwGraphics;

			Rectangle rcSrc, rcDst;
			GetCoordRects(out rcSrc, out rcDst);

			rcSrcRoot = rcSrc;
			rcDstRoot = rcDst;
		}

		/// <summary>
		/// Get a graphics object in an appropriate state for drawing and measuring in the view.
		/// The calling method should pass the IVwGraphics back to ReleaseGraphics() before
		/// it returns. In particular, problems will arise if OnPaint() gets called before the
		/// ReleaseGraphics() method.
		/// </summary>
		IVwGraphics IVwRootSite.get_LayoutGraphics(IVwRootBox prootb)
		{
			InitGraphics();
			return m_graphicsManager.VwGraphics;
		}

		/// <summary>
		/// Get a transform for a given destination point...same for all points in this
		/// simple case.
		/// </summary>
		public void GetTransformAtDst(IVwRootBox root, Point pt, out Rect rcSrcRoot, out Rect rcDstRoot)
		{
			using (new HoldGraphics(this))
			{
				Rectangle rcSrc, rcDst;
				GetCoordRects(out rcSrc, out rcDst);

				rcSrcRoot = rcSrc;
				rcDstRoot = rcDst;
			}
		}

		/// <summary>
		/// Get a transform for a given layout point...same for all points in this
		/// simple case.
		/// </summary>
		public void GetTransformAtSrc(IVwRootBox root, Point pt, out Rect rcSrcRoot, out Rect rcDstRoot)
		{
			GetTransformAtDst(root, pt, out rcSrcRoot, out rcDstRoot);
		}

		/// <summary>
		/// Real drawing VG same as layout one for simple view.
		/// </summary>
		public IVwGraphics get_ScreenGraphics(IVwRootBox root)
		{
			InitGraphics();
			return m_graphicsManager.VwGraphics;
		}

		/// <summary>
		/// Inform the container when done with the graphics object.
		/// </summary>
		/// <remarks>
		/// REVIEW JohnT(?): could we somehow have this handled by the Release method
		/// of the IVwGraphics? But that method does not know anything about the status or
		/// source of its hdc.
		/// </remarks>
		public virtual void ReleaseGraphics(IVwRootBox prootb, IVwGraphics pvg)
		{
			Debug.Assert(pvg == m_graphicsManager.VwGraphics);
			UninitGraphics();
		}

		/// <summary>
		/// Notifies the site that something about the selection has changed.
		/// </summary>
		/// <param name="rootb">The rootbox whose selection changed</param>
		/// <param name="vwselNew">The new selection</param>
		/// <remarks>Don't you dare make this virtual!</remarks>
		public void SelectionChanged(IVwRootBox rootb, IVwSelection vwselNew)
		{
			Debug.Assert(rootb == EditingHelper.EditedRootBox);
			Debug.Assert(vwselNew == rootb.Selection);

			EditingHelper.SelectionChanged();
		}

		/// <summary>
		/// Notifies the site that the size of the root box changed; scroll ranges and/or
		/// window size may need to be updated. The standard response is to update the scroll range.
		/// </summary>
		/// <remarks>
		/// Review JohnT: might this also be the place to make sure the selection is still visible?
		/// Should we try to preserve the scroll position (at least the top left corner, say) even
		/// if the selection is not visible? Which should take priority?
		/// </remarks>
		public virtual void RootBoxSizeChanged(IVwRootBox prootb)
		{
			if (!AllowLayout)
			{
				return;
			}
			UpdateScrollRange();

			OnLayoutSizeChanged(new EventArgs());
		}

		/// <summary>
		/// Required method to implement the LayoutSizeChanged event.
		/// </summary>
		protected virtual void OnLayoutSizeChanged(EventArgs e)
		{
			//Invokes the delegates.
			LayoutSizeChanged?.Invoke(this, e);
		}

		/// <summary>
		/// When the state of the overlays changes, it propagates this to its site.
		/// </summary>
		public virtual void OverlayChanged(IVwRootBox prootb, IVwOverlay vo)
		{
			// do nothing
		}

		/// <summary>
		/// Return true if this kind of window uses semi-tagging.
		/// </summary>
		public virtual bool get_SemiTagging(IVwRootBox prootb)
		{
			return false;
		}

		/// <summary>
		/// Member ScreenToClient
		/// </summary>
		public virtual void ScreenToClient(IVwRootBox prootb, ref Point pt)
		{
			pt = PointToClient(pt);
		}

		/// <summary>
		/// Member ClientToScreen
		/// </summary>
		public virtual void ClientToScreen(IVwRootBox prootb, ref Point pt)
		{
			pt = PointToScreen(pt);
		}

		/// <summary>If there is a pending writing system that should be applied to typing,
		/// return it; also clear the state so that subsequent typing will not have a pending
		/// writing system until something sets it again.  (This is mainly used so that
		/// keyboard-change commands can be applied while the selection is a range.)</summary>
		public virtual int GetAndClearPendingWs(IVwRootBox prootb)
		{
			var ws = WsPending;
			WsPending = -1;
			return ws;
		}

		/// <summary>
		/// Answer whether boxes in the specified range of destination coordinates
		/// may usefully be converted to lazy boxes. Should at least answer false
		/// if any part of the range is visible. The default implementation avoids
		/// converting stuff within about a screen's height of the visible part(s).
		/// </summary>
		public virtual bool IsOkToMakeLazy(IVwRootBox prootb, int ydTop, int ydBottom)
		{
			return false; // Todo JohnT or TE team: make similar to AfVwWnd impl.
		}

		/// <summary>
		/// The user has attempted to delete something which the system does not inherently
		/// know how to delete. The dpt argument indicates the type of problem.
		/// </summary>
		/// <param name="sel">The selection</param>
		/// <param name="dpt">Problem type</param>
		/// <returns><c>true</c> to abort</returns>
		public virtual VwDelProbResponse OnProblemDeletion(IVwSelection sel, VwDelProbType dpt)
		{
			// give up quietly.
			// Review team (JohnT): a previous version threw NotImplementedException. This seems
			// overly drastic.
			return VwDelProbResponse.kdprFail;
		}

		/// <summary />
		public virtual VwInsertDiffParaResponse OnInsertDiffParas(IVwRootBox prootb, ITsTextProps ttpDest, int cPara, ITsTextProps[] ttpSrc, ITsString[] tssParas, ITsString tssTrailing)
		{
			return VwInsertDiffParaResponse.kidprDefault;
		}

		/// <summary> see OnInsertDiffParas </summary>
		public virtual VwInsertDiffParaResponse OnInsertDiffPara(IVwRootBox prootb, ITsTextProps ttpDest, ITsTextProps ttpSrc, ITsString tssParas, ITsString tssTrailing)
		{
			return VwInsertDiffParaResponse.kidprDefault;
		}

		/// <summary>
		/// Needs a cache in order to provide a meaningful implementation. SimpleRootsite
		/// should never have objects cut, copied, pasted.
		/// </summary>
		public virtual string get_TextRepOfObj(ref Guid guid)
		{
			throw new NotSupportedException("This should never get called for a simple rootsite.");
		}

		/// <summary>
		/// Needs a cache in order to provide a meaningful implementation. SimpleRootsite does
		/// not know how to handle GUIDs so just return an empty GUID which will cause the
		/// run to be deleted.
		/// </summary>
		/// <param name="bstrText">Text representation of object</param>
		/// <param name="_selDst">Provided for information in case it's needed to generate
		/// the new object (E.g., footnotes might need it to generate the proper sequence
		/// letter)</param>
		/// <param name="kodt">The object data type to use for embedding the new object
		/// </param>
		public virtual Guid get_MakeObjFromText(string bstrText, IVwSelection _selDst, out int kodt)
		{
			kodt = -1;
			return Guid.Empty;
		}

		/// <summary>
		/// Scrolls the selection into view, positioning it as requested
		/// </summary>
		/// <param name="sel">The selection, or <c>null</c> to use the current selection</param>
		/// <param name="scrollOption">The VwScrollSelOpts specification.</param>
		/// <returns>True if the selection was moved into view, false if this function did
		/// nothing</returns>
		public virtual bool ScrollSelectionIntoView(IVwSelection sel, VwScrollSelOpts scrollOption)
		{
			switch (scrollOption)
			{
				case VwScrollSelOpts.kssoDefault:
					return MakeSelectionVisible(sel, true);
				case VwScrollSelOpts.kssoNearTop:
					return ScrollSelectionToLocation(sel, LineHeight);
				case VwScrollSelOpts.kssoTop:
					return ScrollSelectionToLocation(sel, 1);
				case VwScrollSelOpts.kssoBoth:
					return MakeSelectionVisible(sel, true, true, true);
				default:
					throw new ArgumentException("Unsupported VwScrollSelOpts");
			}
		}

		/// <summary>
		/// Gets the root box
		/// </summary>
		IVwRootBox IVwRootSite.RootBox => RootBox;

		/// <summary>
		/// Gets the HWND.
		/// </summary>
		uint IVwRootSite.Hwnd => (uint)Handle;
		#endregion

		#region Implementation of IEditingCallbacks

		/// <summary>
		/// Use this instead of AutoScrollPosition, which works only for Rootsites with scroll
		/// bars, for some reason.
		/// </summary>
		public virtual Point ScrollPosition
		{
			get
			{
				return AutoScrollPosition;
			}
			set
			{
				var newPos = value;
				if (AutoScroll)
				{
					newPos.X = Math.Abs(newPos.X);
					newPos.Y = Math.Abs(newPos.Y);
				}
				else
				{
					// If we're not autoscrolling, don't do it!
					newPos.X = 0;
					newPos.Y = 0;
				}

				if (Platform.IsMono)
				{
					if (AllowPainting) // FWNX-235
					{
						AutoScrollPosition = newPos;
					}
					else
					{
						cachedAutoScrollPosition = newPos;
					}
				}
				else
				{
					AutoScrollPosition = newPos;
				}
			}
		}

		/// <summary>
		/// We'd like to be able to override the setter for AutoScrollMinSize, but
		/// it isn't virtual so we use this instead.
		/// </summary>
		public virtual Size ScrollMinSize
		{
			get
			{
				return AutoScrollMinSize;
			}
			set
			{
				if (Platform.IsMono)
				{
					// TODO-Linux
					// Due to difference in mono scrolling bar behaviour
					// possibly partly due to bug https://bugzilla.novell.com/show_bug.cgi?id=500796
					// although there are probably other problems.
					// possibly causes other unit test issues
					// Don't adjust this unless it's needed.  See FWNX-561.
					AutoScrollMinSize = value - new Size(VScroll ? SystemInformation.VerticalScrollBarWidth : 0, HScroll ? SystemInformation.HorizontalScrollBarHeight : 0);

					try
					{
						AdjustFormScrollbars(HScroll || VScroll);
					}
					catch (ArgumentOutOfRangeException)
					{
						// TODO-Linux: Investigate real cause of this.
						// I think it caused by mono setting ScrollPosition to 0 when Scrollbar isn't visible
						// it should possibly set it to Minimum value instead.
						// Currently occurs when dropping down scrollbars in Flex.
					}
				}
				else
				{
					AutoScrollMinSize = value;

					// Following line is necessary so that the DisplayRectangle gets updated.
					// This calls PerformLayout().
					AdjustFormScrollbars(HScroll || VScroll);
				}
			}
		}

		/// <summary>
		/// We want to allow clients to tell whether we are showing the horizontal scroll bar.
		/// </summary>
		public bool IsHScrollVisible => WantHScroll && AutoScrollMinSize.Width > Width;

		/// <summary>
		/// Root site slaves sometimes need to suppress the effects of OnSizeChanged.
		/// </summary>
		public virtual bool SizeChangedSuppression
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		/// <summary>
		/// Typically the client rectangle height, but this gives a bizarre value for
		/// root sites in a group, so it is overridden.
		/// </summary>
		public virtual int ClientHeight => ClientRectangle.Height;

		/// <summary>
		/// This returns the client rectangle that we want the selection to be inside.
		/// For a normal root site this is just its client rectangle.
		/// RootSite overrides.
		/// </summary>
		public virtual Rectangle AdjustedClientRectangle => ClientRectangle;

		/// <summary>Pending writing system</summary>
		/// <remarks>This gets set when there was a switch in the system keyboard,
		/// and at that point there was no insertion point on which to change the writing system.
		/// We store the information here until either they get an IP and start typing
		/// (the writing system is set using these) or the selection changes (throw the
		/// information away and reset the keyboard).
		/// </remarks>
		public int WsPending { get; set; }

		/// <summary>
		/// Scroll to the top
		/// </summary>
		public virtual void ScrollToTop()
		{
			if (DoingScrolling)
			{
				ScrollPosition = new Point(0, 0);
			}
		}

		/// <summary>
		/// Scroll to the bottom. This is somewhat tricky because after scrolling to the bottom of
		/// the range as we currently estimate it, expanding a closure may change things.
		/// <seealso cref="GoToEnd"/>
		/// </summary>
		public virtual void ScrollToEnd()
		{
			if (DoingScrolling && !DesignMode)
			{
				// dy gets added to the scroll offset. This means a positive dy causes there to be more
				// of the view hidden above the top of the screen. This is the same effect as clicking a
				// down arrow, which paradoxically causes the window contents to move up.
				int dy;
				var ydCurr = -ScrollPosition.Y; // Where the window thinks it is now.
				using (new HoldGraphics(this))
				{
					// This loop repeats until we have figured out a scroll distance AND confirmed
					// that we can draw that location without messing things up.
					for (; ; )
					{
						var ydMax = DisplayRectangle.Height - ClientHeight + 1;
						dy = ydMax - ydCurr;
						// OK, we need to move by dy. But, we may have to expand a lazy box there in order
						// to display a whole screen full. If the size estimate is off (which it usually is),
						// that would affect the scroll position we need to be at the very bottom.
						// To avoid this, we make the same PrepareToDraw call
						// that the rendering code will make before drawing after the scroll.
						Rectangle rcSrcRoot;
						Rectangle rcDstRoot;
						GetCoordRects(out rcSrcRoot, out rcDstRoot);
						rcDstRoot.Offset(0, -dy);

						var dyRange = RootBox.Height;
						var r = AdjustedClientRectangle;
						var clipRect = new Rect(r.Left, r.Top, r.Right, r.Bottom);

						if (m_graphicsManager.VwGraphics is IVwGraphicsWin32)
						{
							((IVwGraphicsWin32)m_graphicsManager.VwGraphics).SetClipRect(ref clipRect);
						}
						if (RootBox != null && m_dxdLayoutWidth > 0)
						{
							PrepareToDraw(rcSrcRoot, rcDstRoot);
						}
						ydCurr = -ScrollPosition.Y; // Where the window thinks it is now. (May have changed expanding.)
													// If PrepareToDraw didn't change the scroll range, it didn't mess anything up and we
													// can use the dy we figured. Otherwise, loop and figure it again with more complete
													// information, because something at a relevant point has been expanded to real boxes.
						if (RootBox.Height == dyRange)
						{
							break;
						}
						dy = 0; // Back to initial state.
					}
					if (dy != 0)
					{
						// Update the scroll bar.
						// We have to pass a positive value, although ScrollPosition
						// returns a negative one
						ScrollPosition = new Point(-ScrollPosition.X, ydCurr + dy);
					}
				}
			}
		}

		/// <summary>
		/// Show the context menu for the specified root box at the location of
		/// its selection (typically an IP).
		/// </summary>
		public virtual void ShowContextMenuAtIp(IVwRootBox rootb)
		{
			ContextMenu?.Show(this, IPLocation);
		}

		/// <summary>
		/// Gets the (estimated) height of one line in pixels
		/// </summary>
		/// <remarks>
		/// Should we use the selection text properties and stylesheet to get a more specific value?
		/// (font height + 4pt?)
		/// </remarks>
		public int LineHeight => (int)(14 * Math.Ceiling(Dpi.Y / (float)72));

		/// <summary>
		/// Return an indication of the behavior of some of the special keys (arrows, home,
		/// end).
		/// </summary>
		/// <param name="chw">Key value</param>
		/// <param name="ss">Shift status</param>
		/// <returns>Return <c>0</c> for physical behavior, <c>1</c> for logical behavior.
		/// </returns>
		/// <remarks>Physical behavior means that left arrow key goes to the left regardless
		/// of the direction of the text; logical behavior means that left arrow key always
		/// moves the IP one character (possibly plus diacritics, etc.) in the underlying text,
		/// in the direction that is to the left for text in the main paragraph direction.
		/// So, in a normal LTR paragraph, left arrow decrements the IP position; in an RTL
		/// paragraph, it increments it. Both produce a movement to the left in text whose
		/// direction matches the paragraph ("downstream" text). But where there is a segment
		/// of upstream text, logical behavior will jump almost to the other end of the
		/// segment and then move the 'wrong' way through it.
		/// </remarks>
		public virtual CkBehavior ComplexKeyBehavior(int chw, VwShiftStatus ss)
		{
			return CkBehavior.Logical;
		}

		/// <summary>
		/// RootBox being edited.
		/// </summary>
		public IVwRootBox EditedRootBox => RootBox;

		/// <summary>
		/// This tests whether the class has a cache (in the common RootSite subclass) or
		/// (in this base class) whether it has a ws. This is often used to determine whether
		/// we are sufficiently initialized to go ahead with some operation that may get called
		/// prematurely by something in the .NET framework.
		/// </summary>
		public virtual bool GotCacheOrWs => m_wsf != null;

		/// <summary>
		/// Gets the writing system for the HVO. This could either be the vernacular or
		/// analysis writing system.
		/// </summary>
		public virtual int GetWritingSystemForHvo(int hvo)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Perform any processing needed immediately prior to a paste operation.  This is very
		/// rarely implemented, but always called by EditingHelper.PasteClipboard.
		/// </summary>
		public virtual void PrePasteProcessing()
		{
		}
		#endregion

		#region Implementation of IRootSite
		/// <summary>
		/// Refreshes the Display :)
		/// </summary>
		public virtual bool RefreshDisplay()
		{
			if (RootBox?.Site == null)
			{
				return false;
			}
			var decorator = RootBox.DataAccess as DomainDataByFlidDecoratorBase;
			decorator?.Refresh();

			// If we aren't visible or don't belong to a form, then set a flag to do a refresh
			// the next time we go visible.
			if (!Visible || FindForm() == null)
			{
				m_fRefreshPending = true;
				return false;
			}
			// Rebuild the display... the drastic way.
			var restorer = CreateSelectionRestorer();
			try
			{
				using (new SuspendDrawing(this))
				{
					RootBox.Reconstruct();
					m_fRefreshPending = false;
				}
			}
			finally
			{
				restorer?.Dispose();
			}
			//Enhance: If all refreshable descendants are handled this should return true
			return false;
		}

		/// <summary>
		/// Creates a new selection restorer.
		/// </summary>
		/// <remarks>Overriding this method to return null will keep the selection from being
		/// restored</remarks>
		protected virtual SelectionRestorer CreateSelectionRestorer()
		{
			return new SelectionRestorer(this);
		}

		/// <summary>
		/// Allows the IRootSite to be cast as an IVwRootSite
		/// </summary>
		public virtual IVwRootSite CastAsIVwRootSite()
		{
			return this;
		}

		/// <summary>
		/// Return the internal rootbox as a list, or an empty list.
		/// </summary>
		public virtual List<IVwRootBox> AllRootBoxes()
		{
			var result = new List<IVwRootBox>();
			if (RootBox != null)
			{
				result.Add(RootBox);
			}
			return result;
		}

		/// <summary>
		/// <c>false</c> to prevent OnPaint from happening, <c>true</c> to perform
		/// OnPaint. This is used to prevent redraws from happening while we do a RefreshDisplay.
		/// </summary>
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool AllowPainting
		{
			get
			{
				return (m_nAllowPaint == 0);
			}
			set
			{
				// we use WM_SETREDRAW to prevent the scrollbar from jumping around when we are
				// in the middle of a Reconstruct. The m_nAllowPaint flag takes care of (not)
				// painting the view.

				if (value)
				{   // allow painting
					if (m_nAllowPaint > 0)
					{
						m_nAllowPaint--;
					}
					if (m_nAllowPaint == 0 && Visible && IsHandleCreated)
					{
						if (Platform.IsMono)
						{
							ResumeLayout();
							Update();
							Invalidate();

							// FWNX-235
							if (cachedAutoScrollPosition != null)
							{
								AutoScrollPosition = (Point)cachedAutoScrollPosition;
							}
							cachedAutoScrollPosition = null;
						}
						else
						{
							Win32.SendMessage(Handle, (int)Win32.WinMsgs.WM_SETREDRAW, 1, 0);
							Update();
							Invalidate();
						}
					}
				}
				else
				{   // prevent painting
					if (m_nAllowPaint == 0 && Visible && IsHandleCreated)
					{
						if (Platform.IsMono)
						{
							SuspendLayout();
						}
						else
						{
							Win32.SendMessage(Handle, (int)Win32.WinMsgs.WM_SETREDRAW, 0, 0);
						}
					}
					m_nAllowPaint++;
				}
			}
		}
		#endregion

		#region Print-related methods

		/// <summary>
		/// Print stuff.
		/// </summary>
		private void Print_Click(object sender, EventArgs e)
		{
			if (!Focused)
			{
				return;
			}
			using (var printDoc = new PrintDocument())
			using (var dlg = new PrintDialog())
			{
				dlg.Document = printDoc;
				dlg.AllowSomePages = true;
				dlg.AllowSelection = false;
				dlg.PrinterSettings.FromPage = 1;
				dlg.PrinterSettings.ToPage = 1;
				SetupPrintHelp(dlg);
				AdjustPrintDialog(dlg);

				if (dlg.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				if (MiscUtils.IsUnix)
				{
					using (var pageDlg = new PageSetupDialog())
					{
						pageDlg.Document = dlg.Document;
						pageDlg.AllowPrinter = false;
						if (pageDlg.ShowDialog() != DialogResult.OK)
						{
							return;
						}
					}
				}

				// REVIEW: .NET does not appear to handle the collation setting correctly
				// so for now, we do not support non-collated printing.  Forcing the setting
				// seems to work fine.
				printDoc.PrinterSettings.Collate = true;
				Print(printDoc);
			}
		}

		/// <summary>
		/// By default this does nothing. Override to, for example, enable the 'Selection' button.
		/// See XmlSeqView for an example.
		/// </summary>
		protected virtual void AdjustPrintDialog(PrintDialog dlg)
		{
		}

		/// <summary>
		/// If help is available for the print dialog, set ShowHelp to true,
		/// and add an event handler that can display some help.
		/// See DraftView in TeDll for an example.
		/// </summary>
		protected virtual void SetupPrintHelp(PrintDialog dlg)
		{
			dlg.ShowHelp = false;
		}

		/// <summary>
		/// Default is to print the exact same thing as displayed in the view, but
		/// subclasses (e.g., ConstChartBody) can override.
		/// </summary>
		protected virtual void GetPrintInfo(out int hvo, out IVwViewConstructor vc, out int frag, out IVwStylesheet ss)
		{
			RootBox.GetRootObject(out hvo, out vc, out frag, out ss);
		}

		/// <summary>
		/// SimpleRootSite Print(pd) method, overridden by e.g. XmlSeqView.
		/// Note: this does not implement the lower level IPrintRootSite.Print(pd).
		/// </summary>
		public virtual void Print(PrintDocument printDoc)
		{
			if (RootBox == null || DataAccess == null)
			{
				return;
			}
			int hvo;
			IVwViewConstructor vc;
			int frag;
			IVwStylesheet ss;
			GetPrintInfo(out hvo, out vc, out frag, out ss);
			IPrintRootSite printRootSite = new PrintRootSite(DataAccess, hvo, vc, frag, ss);

			try
			{
				printRootSite.Print(printDoc);
			}
			catch (ContinuableErrorException e)
			{
				throw e;
			}
			catch (Exception e)
			{
				var errorMsg = string.Format(Properties.Resources.kstidPrintingException, e.Message);
				MessageBox.Show(FindForm(), errorMsg, Properties.Resources.kstidPrintErrorCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		#endregion

		#region Scrolling-related methods
		// We don't need AfVwScrollWnd::ScrollBy - can be done directly in .NET

		/// <summary>
		/// Finds the distance between the scroll position and the IP (i.e. the distance
		/// between the top of the window and the IP).
		/// </summary>
		/// <param name="sel">The selection used to get the IP's location. If
		/// this value is null, the rootsite's current selection will be used.</param>
		public int IPDistanceFromWindowTop(IVwSelection sel)
		{
			if (sel == null && RootBox != null)
			{
				sel = RootBox.Selection;
			}
			if (sel == null)
			{
				return 0;
			}
			using (new HoldGraphics(this))
			{
				Rectangle rcSrcRoot;
				Rectangle rcDstRoot;
				GetCoordRects(out rcSrcRoot, out rcDstRoot);

				Rect rcPrimary;
				Rect rcSecondary;
				bool fSplit;
				bool fEndBeforeAnchor;

				sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSecondary, out fSplit, out fEndBeforeAnchor);

				return rcPrimary.top;
			}
		}

		/// <summary>
		/// Invalidate the pane because some lazy box expansion messed up the scroll position.
		/// Made a separate method so we can override.
		/// </summary>
		public virtual void InvalidateForLazyFix()
		{
			Invalidate();
		}

		/// <summary>
		/// Scroll by the specified amount (positive is down, that is, added to the scroll offset).
		/// If this would exceed the scroll range, move as far as possible. Update both actual display
		/// and scroll bar position. (Can also scroll up, if dy is negative. Name is just to indicate
		/// positive direction.)
		/// </summary>
		protected internal virtual void ScrollDown(int dy)
		{
			int xd, yd;
			GetScrollOffsets(out xd, out yd);
			var ydNew = yd + dy;
			if (ydNew < 0)
			{
				ydNew = 0;
			}
			if (ydNew > DisplayRectangle.Height)
			{
				ydNew = DisplayRectangle.Height;
			}
			if (ydNew != yd)
			{
				ScrollPosition = new Point(xd, ydNew);
			}
		}

		/// <summary>
		/// Use this method instead of <see cref="ScrollToEnd"/> if you need to go to the end
		/// of the view programmatically (not in response to a Ctrl-End). The code for handling
		/// Ctrl-End uses CallOnExtendedKey() in OnKeyDown() to handle setting the IP.
		/// </summary>
		public void GoToEnd()
		{
			ScrollToEnd();
			// The code for handling Ctrl-End doesn't do it this way:
			RootBox.MakeSimpleSel(false, IsEditable, false, true);
			// This method is not used ... possibly move to DummyFootnoteView ??
		}

		/// <summary>
		/// Determines whether automatic horizontal scrolling to show the selection should
		/// occur. Normally this is the case only if showing a horizontal scroll bar,
		/// but FwTextBox is an exception.
		/// </summary>
		protected virtual bool DoAutoHScroll => DoingScrolling && HScroll;

		/// <summary>
		/// Determines whether automatic vertical scrolling to show the selection should
		/// occur. Usually this is only appropriate if the window autoscrolls and has a
		/// vertical scroll bar, but TE's draft view needs to allow it anyway, because in
		/// synchronized scrolling only one of the sync'd windows has a scroll bar.
		/// </summary>
		protected virtual bool DoAutoVScroll => DoingScrolling && VScroll;

		/// <summary>
		/// Gets the rectangle of the selection. If it is a split selection we combine the
		/// two rectangles.
		/// </summary>
		/// <param name="vwsel">Selection</param>
		/// <param name="rcIdeal">Contains the rectangle of the selection on return</param>
		/// <param name="fEndBeforeAnchor">[Out] <c>true</c> if the end is before the anchor,
		/// otherwise <c>false</c>.</param>
		protected internal void SelectionRectangle(IVwSelection vwsel, out Rectangle rcIdeal, out bool fEndBeforeAnchor)
		{
			Debug.Assert(vwsel != null);
			Debug.Assert(m_graphicsManager.VwGraphics != null);

			Rect rcPrimary;
			Rect rcSecondary;
			bool fSplit;
			Rectangle rcSrcRoot;
			Rectangle rcDstRoot;
			GetCoordRects(out rcSrcRoot, out rcDstRoot);

			vwsel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSecondary, out fSplit, out fEndBeforeAnchor);
			rcIdeal = rcPrimary;

			if (fSplit)
			{
				rcIdeal = Rectangle.Union(rcIdeal, rcSecondary);
				if (AutoScroll && VScroll && rcIdeal.Height > ClientHeight || DoAutoHScroll && rcIdeal.Width > ClientRectangle.Width)
				{
					rcIdeal = rcPrimary; // Revert to just showing main IP.
				}
			}
		}

		/// <summary>
		/// Makes a default selection if no selection currently exists in our RootBox.
		/// </summary>
		protected virtual void EnsureDefaultSelection()
		{
			EnsureDefaultSelection(IsEditable);
		}

		/// <summary>
		///  Makes a default selection if no selection currently exists in our RootBox.
		/// </summary>
		/// <param name="fMakeSelInEditable">if true, first try selecting in editable position.</param>
		protected void EnsureDefaultSelection(bool fMakeSelInEditable)
		{
			if (RootBox == null)
			{
				return;
			}
			if (RootBox.Selection == null)
			{
				try
				{
					RootBox.MakeSimpleSel(true, fMakeSelInEditable, false, true);
				}
				catch
				{
					// Eat the exception, since it couldn't make a selection.
				}
			}
		}

		/// <summary>
		/// Scroll to make the selection visible.
		/// In general, scroll the minimum distance to make it entirely visible.
		/// If the selection is higher than the window, scroll the minimum distance to make it
		/// fill the window.
		/// If the window is too small to show both primary and secondary, show primary.
		/// </summary>
		/// <param name="sel">Selection</param>
		/// <remarks>
		/// Note: subclasses for which scrolling is disabled should override.
		/// If <paramref name="sel"/> is null, make the current selection visible.
		/// </remarks>
		/// <returns>True if the selection was made visible, false if it did nothing</returns>
		protected bool MakeSelectionVisible(IVwSelection sel)
		{
			return MakeSelectionVisible(sel, false);
		}

		/// <summary>
		/// Returns the selection that should be made visible if null is passed to
		/// MakeSelectionVisible. FwTextBox overrides to pass a selection that is the whole
		/// range, if nothing is selected.
		/// </summary>
		protected virtual IVwSelection SelectionToMakeVisible => RootBox.Selection;

		/// <summary>
		/// Scroll to make the selection visible.
		/// In general, scroll the minimum distance to make it entirely visible.
		/// If the selection is higher than the window, scroll the minimum distance to make it
		/// fill the window.
		/// If the window is too small to show both primary and secondary, show primary.
		/// If fWantOneLineSpace is true we make sure that at least 1 line is visible above
		/// and below the selection.
		/// By default ranges (that are not pictures) are allowed to be only partly visible.
		/// </summary>
		/// <param name="sel">The sel.</param>
		/// <param name="fWantOneLineSpace">if set to <c>true</c> [f want one line space].</param>
		/// <returns>Flag indicating whether the selection was made visible</returns>
		protected virtual bool MakeSelectionVisible(IVwSelection sel, bool fWantOneLineSpace)
		{
			if (RootBox == null)
			{
				return false;
			}
			var vwsel = (sel ?? SelectionToMakeVisible);
			return vwsel != null && MakeSelectionVisible(vwsel, fWantOneLineSpace, DefaultWantBothEnds(vwsel), false);
		}

		/// <summary>
		/// Scroll to make the selection visible.
		/// In general, scroll the minimum distance to make it entirely visible.
		/// If the selection is higher than the window, scroll the minimum distance to make it
		/// fill the window.
		/// If the window is too small to show both primary and secondary, show primary.
		/// If fWantOneLineSpace is true we make sure that at least 1 line is visible above
		/// and below the selection.
		/// </summary>
		/// <param name="vwsel">Selection</param>
		/// <param name="fWantOneLineSpace">True if we want at least 1 extra line above or
		/// below the selection, false otherwise</param>
		/// <param name="fWantBothEnds">if true, we want to be able to see both ends of
		/// the selection, if possible; if it is too large, align top to top.</param>
		/// <param name="fForcePrepareToDraw">Pass this as true when the selection was made under program
		/// control, not from a click. It indicates that even though the selection may be initially
		/// visible, there might be lazy boxes on screen; and when we come to paint, expanding them
		/// might make the selection no longer visible. Therefore we should not skip the full
		/// process just because the selection is already visible.</param>
		/// <remarks>
		/// Note: subclasses for which scrolling is disabled should override.
		/// If the selection is invalid, return false.
		/// </remarks>
		/// <returns>True if the selection was made visible, false if it did nothing</returns>
		protected virtual bool MakeSelectionVisible(IVwSelection vwsel, bool fWantOneLineSpace, bool fWantBothEnds, bool fForcePrepareToDraw)
		{
			// TODO: LT-2268,2508 - Why is this selection going bad...?  Also LT-13374 in the case vwsel == null.
			// The if will handle the crash, but there is still the problem
			// of the selections getting invalid.
			if (vwsel == null || !vwsel.IsValid)
			{
				return false; // can't work with an invalid selection
			}
			if (fWantOneLineSpace && ClientHeight < LineHeight * 3)
			{
				// The view is too short to have a line at the top and/or bottom of the line
				// we want to scroll into view. Since we can't possibly do a good job of
				// scrolling the selection into view in this circumstance, we just don't
				// even try.
				fWantOneLineSpace = false;
			}
			// if we're not forcing prepare to draw and the entire selection is visible then there is nothing to do
			if (!fForcePrepareToDraw && IsSelectionVisible(vwsel, fWantOneLineSpace, fWantBothEnds))
			{
				return false;
			}
			using (new HoldGraphics(this))
			{
				bool fEndBeforeAnchor;
				Rectangle rcIdeal;
				SelectionRectangle(vwsel, out rcIdeal, out fEndBeforeAnchor);

				//rcIdeal.Inflate(0, 1); // for good measure
				// OK, we want rcIdeal to be visible.
				// dy gets added to the scroll offset. This means a positive dy causes there to be more
				// of the view hidden above the top of the screen. This is the same effect as clicking a
				// down arrow, which paradoxically causes the window contents to move up.
				var dy = 0;
				int ydTop;

				#region DoAutoVScroll
				if (DoAutoVScroll)
				{
					// This loop repeats until we have figured out a scroll distance AND confirmed
					// that we can draw that location without messing things up.
					for (; ; )
					{
						// Get the amount to add to rdIdeal to make it comparable with the window
						// postion.
						ydTop = -ScrollPosition.Y; // Where the window thinks it is now.
												   // Adjust for that and also the height of the (optional) header.
						rcIdeal.Offset(0, ydTop - m_dyHeader); // Was in drawing coords, adjusted by top.
						var ydBottom = ydTop + ClientHeight - m_dyHeader;
						// Is the end of the selection partly off the top of the screen?
						var extraSpacing = fWantOneLineSpace ? LineHeight : 0;
						if (!fWantBothEnds)
						{
							// For a range (that is not a picture) we scroll just far enough to show the end.
							if (fEndBeforeAnchor)
							{
								if (rcIdeal.Top < ydTop + extraSpacing)
								{
									dy = rcIdeal.Top - (ydTop + extraSpacing);
								}
								else if (rcIdeal.Top > ydBottom - extraSpacing)
								{
									dy = rcIdeal.Top - (ydBottom - extraSpacing) + LineHeight;
								}
							}
							else
							{
								if (rcIdeal.Bottom < ydTop + extraSpacing)
								{
									dy = rcIdeal.Bottom - (ydTop + extraSpacing) - LineHeight;
								}
								else if (rcIdeal.Bottom > ydBottom - extraSpacing)
								{
									dy = rcIdeal.Bottom - (ydBottom - extraSpacing);
								}
							}
						}
						else
						{
							// we want both ends to be visible if possible; if not, as much as possible at the top.
							// Also if it is completely off the bottom of the screen, a good default place to put it is near
							// the top.
							if (rcIdeal.Top < ydTop + extraSpacing || rcIdeal.Height > ydBottom - ydTop - extraSpacing * 2 || rcIdeal.Top > ydBottom)
							{
								// The top is not visible, or there isn't room to show it all: scroll till the
								// top is just visible, after the specified gap.
								dy = rcIdeal.Top - (ydTop + extraSpacing);
							}
							else if (rcIdeal.Bottom > ydBottom - extraSpacing)
							{
								// scroll down minimum to make bottom visible. This involves
								// hiding more text at the top: positive dy.
								dy = rcIdeal.Bottom - (ydBottom - extraSpacing);
							}
						}
						// Else the end of the selection is already visible, do nothing.
						// (But still make sure we can draw the requisite screen full
						// without messing stuff up. Just in case a previous lazy box
						// expansion looked like making it visible, but another makes it
						// invisible again...I'm not sure this can happen, but play safe.)

						// OK, we need to move by dy. But, if that puts the selection near the
						// bottom of the screen, we may have to expand a lazy box above it in
						// order to display a whole screen full. If the size estimate is off
						// (which it usually is), that would affect the position' where the
						// selection gets moved to. To avoid this, we make the same PrepareToDraw
						// call that the rendering code will make before drawing after the scroll.
						Rectangle rcSrc, rcDst;
						GetCoordRects(out rcSrc, out rcDst);
						rcDst.Offset(0, -dy); // Want to draw at the position we plan to move to.

						// Get the whole range we are scrolling over. We use this later to see whether
						// PrepareToDraw messed anything up.
						var dyRange = RootBox.Height;
						if (RootBox != null && m_dxdLayoutWidth > 0)
						{
							SaveSelectionInfo(rcIdeal, ydTop);
							var xpdr = VwPrepDrawResult.kxpdrAdjust;
							// I'm not sure this loop is necessary because we repeat the outer loop until no
							// change in the root box height (therefore presumably no adjustment of scroll position)
							// happens. But it's harmless and makes it more obvious that the code is correct.
							while (xpdr == VwPrepDrawResult.kxpdrAdjust)
							{
								// In case the window contents aren't visible yet, it's important to do this as if it were.
								// When the window is invisible, the default clip rectangle is empty, and we don't
								// 'prepare to draw' very much. We want to be sure that we can draw everything in the window
								// without somehow affecting the position of this selection.
								var clipRect = new Rect(0, 0, ClientRectangle.Width, ClientHeight);
								((IVwGraphicsWin32)m_graphicsManager.VwGraphics).SetClipRect(ref clipRect);
								xpdr = PrepareToDraw(rcSrc, rcDst);
								GetCoordRects(out rcSrc, out rcDst);
							}
						}

						// If PrepareToDraw didn't change the scroll range, it didn't mess
						// anything up and we can use the dy we figured. Otherwise, loop and
						// figure it again with more complete information, because something at a
						// relevant point has been expanded to real boxes.
						if (RootBox.Height == dyRange)
						{
							break;
						}
						// Otherwise we need another iteration, we need to recompute the
						// selection location in view of the changes to layout.
						SelectionRectangle(vwsel, out rcIdeal, out fEndBeforeAnchor);
						dy = 0; // Back to initial state.
					} // for loop
				}
				#endregion
				ydTop = -ScrollPosition.Y; // Where the window thinks it is now.
				SaveSelectionInfo(rcIdeal, ydTop);

				if (dy - ScrollPosition.Y < 0)
				{
					dy = ScrollPosition.Y; // make offset 0 if it would have been less than that
				}
				// dx gets added to the scroll offset. This means a positive dx causes there to
				// be more of the view hidden left of the screen. This is the same effect as
				// clicking a right arrow, which paradoxically causes the window contents to
				// move left.
				var dx = 0;
				var xdLeft = -ScrollPosition.X; // Where the window thinks it is now.
				#region DoAutoHScroll
				if (DoAutoHScroll)
				{
					if (IsVertical)
					{
						// In all current vertical views we have no vertical scrolling, so only need
						// to consider horizontal. Also we have no laziness, so no need to mess with
						// possible effects of expanding lazy boxes that become visible.
						// In this case, rcPrimary's top is the distance from the right of the ClientRect to the
						// right of the selection, and the height of rcPrimary is a distance further left.
						var right = rcIdeal.Top; // distance to left of right edge of window
						var left = right + rcIdeal.Height;
						if (fWantOneLineSpace)
						{
							right -= LineHeight;
							left += LineHeight;
						}
						if (right < 0)
						{
							// selection is partly off the right of the window
							dx = -right; // positive dx to move window contents left.
						}
						else if (left > ClientRectangle.Width)
						{
							dx = ClientRectangle.Width - left; // negative to move window contents right
						}
					}
					else // not a vertical window, normal case
					{
						rcIdeal.Offset(xdLeft, 0); // Was in drawing coords, adjusted by left.
												   // extra 4 pixels so Ip doesn't disappear at right.
						var xdRight = xdLeft + ClientRectangle.Width;
						// Is the selection right of the right side of the screen?
						if (rcIdeal.Right > xdRight)
						{
							dx = rcIdeal.Right - xdRight;
						}
						// Is the selection partly off the left of the screen?
						if (rcIdeal.Left < xdLeft)
						{
							// Is it bigger than the screen?
							if (rcIdeal.Width > ClientRectangle.Width && !fEndBeforeAnchor)
							{
								// Is it bigger than the screen?
								if (rcIdeal.Width > ClientRectangle.Width)
								{
									// Left is off, and though it is too big to show entirely, we can show
									// more. Move the window contents right (negative dx).
									dx = rcIdeal.Right - xdRight;
								}
								else
								{
									// Partly off left, and fits: move window contents right (less is hidden,
									// neg dx).
									dx = rcIdeal.Left - xdLeft;
								}
							}
							else
							{
								// Left of selection is right of (or at) the left side of the screen.
								// Is right of selection right of the right side of the screen?
								if (rcIdeal.Right > xdRight)
								{
									if (rcIdeal.Width > ClientRectangle.Width && fEndBeforeAnchor)
									{
										// Left is visible, right isn't: move until lefts coincide to show as much
										// as possible. This is hiding more text left of the window: positive dx.
										dx = rcIdeal.Left - xdLeft;
									}
									else
									{
										// Fits entirely: scroll left minimum to make right visible. This involves
										// hiding more text at the left: positive dx.
										dx = rcIdeal.Right - xdRight;
									}
								}
								// Else it is already entirely visible, do nothing.
							}
							if (dx > Width - ClientRectangle.Width - 1 + ScrollPosition.X)
							{
								// This value makes it the maximum it can be, except this may make it negative
								dx = Width - ClientRectangle.Width - 1 + ScrollPosition.X;
							}
							if (dx - ScrollPosition.X < 0)
							{
								dx = ScrollPosition.X; // make offset 0 if it would have been less than that
							}
						}
					}
					var scrollRangeX = ScrollRange.Width;
					if (dx > scrollRangeX - ClientRectangle.Width - 1 + ScrollPosition.X)
					{
						// This value makes it the maximum it can be, except this may make it negative
						dx = scrollRangeX - ClientRectangle.Width - 1 + ScrollPosition.X;
					}
					if (dx - ScrollPosition.X < 0)
					{
						dx = ScrollPosition.X; // make offset 0 if it would have been less than that
					}
				}
				#endregion
				if (dx != 0 || dy != 0)
				{
					// Update the scroll bar.
					ScrollPosition = new Point(xdLeft + dx, ydTop + dy);
				}
				Invalidate();
			}

			return true;
		}

		/// <summary>
		/// Save some selection location information if needed.
		/// </summary>
		protected virtual void SaveSelectionInfo(Rectangle rcIdeal, int ydTop)
		{
			// Some subclasses (XmlBrowseViewBase to be exact) need to store some of this information.
		}

		/// <summary>Position the insertion point at the page top</summary>
		/// <param name="fIsShiftPressed">True if the shift key is pressed and selection is
		/// desired</param>
		protected void GoToPageTop(bool fIsShiftPressed)
		{
			// commented the content of this method out as it isn't working correctly. This may be a good starting point :)
			var newX = ClientRectangle.Left;
			var newY = ClientRectangle.Top;

			Debug.Assert(RootBox != null);
			if (RootBox == null)
			{
				return;
			}
			Rectangle rcSrcRoot;
			Rectangle rcDstRoot;
			using (new HoldGraphics(this))
			{
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
			}
			if (fIsShiftPressed)
			{
				RootBox.MouseDownExtended(newX, newY, rcSrcRoot, rcDstRoot);
			}
			else
			{
				RootBox.MouseDown(newX, newY, rcSrcRoot, rcDstRoot);
			}
		}

		/// <summary>Position the insertion point at the page bottom</summary>
		/// <param name="fIsShiftPressed">True if the shift key is pressed and selection is
		/// desired</param>
		protected void GoToPageBottom(bool fIsShiftPressed)
		{
			// commented the content of this method out as it isn't working correctly. This may be a good starting point :)
			var newX = ClientRectangle.Right;
			var newY = ClientRectangle.Bottom;

			Debug.Assert(RootBox != null);
			if (RootBox == null)
			{
				return;
			}
			Rectangle rcSrcRoot;
			Rectangle rcDstRoot;
			using (new HoldGraphics(this))
			{
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
			}
			if (fIsShiftPressed)
			{
				RootBox.MouseDownExtended(newX, newY, rcSrcRoot, rcDstRoot);
			}
			else
			{
				RootBox.MouseDown(newX, newY, rcSrcRoot, rcDstRoot);
			}
		}

		/// <summary>
		/// Scroll the selection in to the given client position.
		/// </summary>
		/// <param name="sel">The selection</param>
		/// <param name="dyPos">Position from top of client window where sel should be scrolled</param>
		/// <returns>True if the selection was scrolled into view, false if this function did
		/// nothing</returns>
		public bool ScrollSelectionToLocation(IVwSelection sel, int dyPos)
		{
			if (RootBox == null)
			{
				return false;
			}
			if (sel == null)
			{
				sel = RootBox.Selection;
			}
			if (sel == null)
			{
				return false;
			}
			using (new HoldGraphics(this))
			{
				// Put IP at top of window.
				MakeSelectionVisible(sel);

				Rectangle rcSrcRoot;
				Rectangle rcDstRoot;

				// Expand the lazy boxes 1 screen up
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				rcDstRoot.Offset(0, -ClientRectangle.Height);
				PrepareToDraw(rcSrcRoot, rcDstRoot);

				// Expand the lazy boxes 1 screen down
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				rcDstRoot.Offset(0, ClientRectangle.Height);
				PrepareToDraw(rcSrcRoot, rcDstRoot);

				// Get the difference between where we want the selection to be and its
				// current position
				Rect rcPrimary;
				Rect rcSecondary;
				bool fSplit;
				bool fEndBeforeAnchor;
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSecondary, out fSplit, out fEndBeforeAnchor);
				var difference = dyPos - rcPrimary.top;

				// Now move the scroll position so the IP will be where we want it.
				ScrollPosition = new Point(-ScrollPosition.X, -ScrollPosition.Y - difference);
			}

			// If the selection is still not visible (which should only be the case if
			// we're at the end of the view), just take whatever MakeSelectionVisible()
			// gives us).
			if (!IsSelectionVisible(sel) && dyPos >= 0 && dyPos <= ClientHeight)
			{
				MakeSelectionVisible(sel);
			}
			return true;
		}

		/// <summary>
		/// Wraps PrepareToDraw calls so as to suppress attempts to paint or any similar re-entrant call
		/// we might make while getting ready to do it.
		/// </summary>
		protected VwPrepDrawResult PrepareToDraw(Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			if (PaintInProgress || LayoutInProgress)
			{
				return VwPrepDrawResult.kxpdrNormal; // at least prevent loops
			}
			PaintInProgress = true;
			try
			{
				return RootBox.PrepareToDraw(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot);
			}
			finally
			{
				PaintInProgress = false;
			}
		}

		#endregion

		#region Event handling methods
		/// <summary>
		/// Flash the insertion point.
		/// </summary>
		protected virtual void OnTimer(object sender, EventArgs e)
		{
			if (RootBox != null && Focused)
			{
				RootBox.FlashInsertionPoint(); // Ignore any error code.
			}
		}

		/// <summary>
		/// Allow the orientation manager to convert arrow key codes.
		/// </summary>
		internal int ConvertKeyValue(int keyValue)
		{
			return m_orientationManager.ConvertKeyValue(keyValue);
		}

		/// <summary>
		/// Set the accessible name that the root box will return for this root site.
		/// </summary>
		public void SetAccessibleName(string name)
		{
			var acc = AccessibleRootObject as IAccessible;
			acc?.set_accName(null, name);
		}

		/// <summary>
		/// Get the accessible object from the root box (implements IAccessible)
		/// </summary>
		public object AccessibleRootObject
		{
			get
			{
				if (Platform.IsMono)
				{
					return null; // TODO-Linux IOleServiceProvider not listed in QueryInterface issue.
				}
				if (RootBox == null)
				{
					return null;
				}
				object obj = null;
#if JASONTODO
				TODO: There doesn't appear to be any implementors or users of IOleServiceProvider, other than this code, which really does nothing.
#endif
				if (RootBox is IOleServiceProvider)
				{
					var sp = (IOleServiceProvider)RootBox;
					if (sp == null)
					{
						// REVIEW (TomB): Shouldn't this just throw an exception?
						MessageBox.Show("Null IServiceProvider from root");
						Debug.Fail("Null IServiceProvider from root");
					}
					var guidAcc = Marshal.GenerateGuidForType(typeof(IAccessible));
					// 1st guid currently ignored.
					sp.QueryService(ref guidAcc, ref guidAcc, out obj);
				}
				return obj;
			}
		}

		/// <summary>
		/// Override the WndProc to handle WM_GETOBJECT so we can return the
		/// IAccessible implementation from the root box, rather than wrapping it
		/// as an AccessibleObject in .NET style. This is important because the test
		/// harness wants to be able to get back to the root box.  There are a couple
		/// of other messages we must handle at this level as well.
		///
		/// This override is now delegated through the message sequencer; see OriginalWndProc.
		/// </summary>
		protected override void WndProc(ref Message m)
		{
			Sequencer.SequenceWndProc(ref m);
		}

		#endregion // Event handling methods

		#region Overriden methods (of UserControl)
		/// <summary>
		/// When we go visible and we are waiting to refresh the display (do a rebuild) then
		/// call RefreshDisplay()
		/// </summary>
		protected override void OnVisibleChanged(EventArgs e)
		{
			base.OnVisibleChanged(e);
			if (Visible && m_fRootboxMade && RootBox != null && m_fRefreshPending)
			{
				RefreshDisplay();
			}
		}

		/// <summary>
		/// Return a wrapper around the COM IAccessible for the root box.
		/// </summary>
		protected override AccessibleObject CreateAccessibilityInstance()
		{
			AccessibleObject result = new AccessibilityWrapper(this, AccessibleRootObject as Accessibility.IAccessible);
			return result;
		}

		/// <summary>
		/// This provides an override opportunity for non-read-only views which do NOT
		/// want an initial selection made in OnLoad (e.g., InterlinDocForAnalysis) or OnGotFocus().
		/// </summary>
		public virtual bool WantInitialSelection => IsEditable;

		/// <summary />
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			if (m_fRootboxMade && RootBox != null && m_fAllowLayout)
			{
				PerformLayout();
				if (RootBox.Selection == null && WantInitialSelection && m_dxdLayoutWidth > 0)
				{
					try
					{
						RootBox.MakeSimpleSel(true, IsEditable, false, true);
					}
					catch (COMException)
					{
						// Ignore
					}
				}
			}
		}

		/// <summary>
		/// The window is first being created.
		/// </summary>
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime && !AllowPaintingInDesigner)
			{
				return;
			}
			// If it is the second pane of a split window, it may have been given a copy of the
			// first child's root box before the window gets created.
			if (RootBox == null && m_fMakeRootWhenHandleIsCreated)
			{
				using (new HoldGraphics(this))
				{
					MakeRoot();
					m_dxdLayoutWidth = kForceLayout; // Don't try to draw until we get OnSize and do layout.
				}
				// TODO JohnT: In case of an exception, do we have to display some message to
				// the user? Or go ahead with create, but set up Paint to display some message?
			}

			// Create a timer used to flash the insertion point if any.
			// We flash every half second (500 ms).
			// Note that the timer is not started until we get focus.
			m_Timer.Interval = 500;
			m_Timer.Tick += OnTimer;
		}

		/// <summary>
		/// Do cleaning up when handle gets destroyed
		/// </summary>
		protected override void OnHandleDestroyed(EventArgs e)
		{
			base.OnHandleDestroyed(e);

			if (DesignMode)
			{
				return;
			}
			// We generally need to close the rootbox here or we get memory leaks all over. But
			// if we always do it, we break switching fields in DE views, and anywhere else a
			// root box is shared.
			// Subclasses should override CloseRootBox if something else is still using it
			// after the window closes.
			CloseRootBox();

			// we used to call Marshal.ReleaseComObject here, but this proved to be unnecessary,
			// because we're going out of scope and so the GC takes care of that.
			// NOTE: ReleaseComObject() returned a high number of references. These are
			// references to the RCW (Runtime Callable Wrapper), not to the C++ COM object,
			// and the GC handles that.
			RootBox = null;
		}

		/// <summary>
		/// We intercept WM_SETFOCUS in our WndProc and call this because we need the
		/// information about the previous focus window, which .NET does not provide.
		/// </summary>
		protected void OnSetFocus(Message m)
		{
			// Can't set/get focus if we don't have a rootbox.  For some reason we may try
			// to process this message under those circumstances during an Undo() in IText (LT-2663).
			if (IsDisposed || RootBox == null)
			{
				return;
			}
			try
			{
				m_fHandlingOnGotFocus = true;
				OnGotFocus(EventArgs.Empty);

				// A tricky problem is that the old hwnd may be the Keyman tray icon, in which the
				// user has just selected a keyboard. If we SetKeyboardForSelection in that case,
				// we change the current keyboard before we find out what the user selected.
				// In most other cases, we'd like to make sure the right keyboard is selected.
				// If the user switches back to this application from some other without changing the
				// focus in this application, we should already have the right keyboard, since the OS
				// maintains a keyboard selection for each application. If he clicks in a view to
				// switch back, he will make a selection, which results in a SetKeyboardForSelection call.
				// If he clicks in a non-view, eventually the focus will move back to the view, and
				// the previous window will belong to our process, so the following code will fire.
				// So, unless there's a case I (JohnT) haven't thought of, the following should set
				// the keyboard at least as often as we need to, and not in the one crucial case where
				// we must not.
				// (Note: setting a WS by setting the Keyman keyboard is still not working, because
				// it seems .NET applications don't get the notification from Keyman.)
				if (Platform.IsMono)
				{
					// REVIEW: do we have to compare the process the old and new window belongs to?
					if (RootBox != null && EditingHelper != null)
					{
						EditingHelper.SetKeyboardForSelection(RootBox.Selection);
					}
				}
				else
				{
					var hwndOld = m.WParam;
					int procIdOld, procIdThis;
					Win32.GetWindowThreadProcessId(hwndOld, out procIdOld);
					Win32.GetWindowThreadProcessId(Handle, out procIdThis);
					if (procIdOld == procIdThis && RootBox != null && EditingHelper != null)
					{
						EditingHelper.SetKeyboardForSelection(RootBox.Selection);
					}
				}

				// Start the blinking cursor timer here and stop it in the OnKillFocus handler later.
				m_Timer?.Start();
			}
			finally
			{
				m_fHandlingOnGotFocus = false;
			}
		}

		#region Overrides of Control
		/// <summary>
		/// View is getting focus: Activate the rootbox and set the appropriate keyboard
		/// </summary>
		protected override void OnGotFocus(EventArgs e)
		{
			if (m_printMenu != null)
			{
				m_printMenu.Enabled = true;
			}

			g_focusRootSite.Target = this;
			base.OnGotFocus(e);
			if (DesignMode || !m_fRootboxMade || RootBox == null)
			{
				return;
			}

			// Enable selection display
			if (WantInitialSelection)
			{
				EnsureDefaultSelection();
			}
			Activate(VwSelectionState.vssEnabled);

			EditingHelper.GotFocus();
		}

		/// <summary />
		protected override void OnLostFocus(EventArgs e)
		{
			base.OnLostFocus(e);

			if (m_printMenu != null)
			{
				m_printMenu.Enabled = false;
			}
		}
		#endregion

		/// <summary>
		/// Called when the focus is lost to another window.
		/// </summary>
		/// <param name="newWindow">The new window. Might be <c>null</c>.</param>
		/// <param name="fIsChildWindow"><c>true</c> if the <paramref name="newWindow"/> is
		/// a child window of the current application.</param>
		protected virtual void OnKillFocus(Control newWindow, bool fIsChildWindow)
		{
			// If we have a timer (used for making the insertion point blink), then we want to stop it.
			m_Timer?.Stop();

			SimpleRootSite newFocusRootSite = null;
			// If g_focusRootSite is something other than this (or null), then some other root
			// site received OnGotFocus before this one received OnLostFocus. Leave it recorded
			// as the current focus root site. If this is still the focus root site, then
			// focus has not yet switched to another root site, so there is currently none.
			if (g_focusRootSite.Target != this)
			{
				newFocusRootSite = g_focusRootSite.Target as SimpleRootSite;
			}
			else
			{
				// This is not a nice kludge, but it takes care of the special case where
				// a Windows ComboBox grabs focus willy-nilly, and will allow tests for the
				// 'real' focus + maintaining g_focusRootSite in sensible manner.
				if (newWindow != null && (newWindow.GetType().Name != "ToolStripComboBoxControl"))
				{
					// We can't set g_focusRootSite.Target to null, so instead we create a new
					// empty weak reference.
					g_focusRootSite = new WeakReference(null);
				}
			}
			if (DesignMode || RootBox == null)
			{
				return;
			}
			// This event may occur while a large EditPaste is inserting text,
			// and if so we need to bail out to avoid a crash.
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return;
			}
			// NOTE: Do not call RemoveCmdHandler or SetActiveRootBox(NULL) here. There are many
			// cases where the view window loses focus, but we still want to keep track of the
			// last view window.  If it is necessary to forget about the view window, do it
			// somewhere else.  If nothing else sets the last view window to NULL, it will be
			// cleared when the view window gets destroyed.

			RootBox.LoseFocus();

			UpdateSelectionEnabledState(newWindow);

			// This window is losing control of the keyboard, so make sure when we get the focus
			// again we reset it to what we want.
			EditingHelper.LostFocus(newWindow, fIsChildWindow);

			if (newFocusRootSite == null)
			{
				//This is a major kludge to prevent a spurious WM_KEYUP for VK_CONTROL from
				// interrupting a mouse click.
				// If mouse button was pressed elsewhere causing this loss of focus,
				//  activate a message pre-filter in this root site to throw away the
				// spurious WM_KEYUP for VK_CONTROL that happens as a result of switching
				// to the default keyboard layout.
				if ((MouseButtons & MouseButtons.Left) != 0 && !m_messageFilterInstalled)
				{
					Application.AddMessageFilter(this);
					m_messageFilterInstalled = true;
				}
			}
		}

		/// <summary>
		/// Process mouse move
		/// </summary>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (DataUpdateMonitor.IsUpdateInProgress() || MouseMoveSuppressed)
			{
				return;
			}
			// REVIEW: Do we need the stuff implemented in AfVwWnd::OnMouseMove?
			// Convert to box coords and pass to root box (if any)
			var mousePos = new Point(e.X, e.Y);
			if (RootBox != null)
			{
				using (new HoldGraphics(this))
				{
					Rectangle rcSrcRoot;
					Rectangle rcDstRoot;
					GetCoordRects(out rcSrcRoot, out rcDstRoot);

					// For now at least we care only if the mouse is down.
					if (e.Button == MouseButtons.Left)
					{
						CallMouseMoveDrag(PixelToView(mousePos), rcSrcRoot, rcDstRoot);
					}
				}
			}

			OnMouseMoveSetCursor(mousePos);
		}

		/// <summary>
		/// Allow clients to override cursor type during OnMouseMove.
		/// </summary>
		protected virtual void OnMouseMoveSetCursor(Point mousePos)
		{
			EditingHelper.SetCursor(mousePos, RootBox);
		}

		/// <summary>
		/// Return true if the view is currently painting itself.
		/// (This is useful because of Windows' pathological habit of calling the WndProc re-entrantly.
		/// Some things should not happen while the paint is in progress and the view may be
		/// under construction.)
		/// </summary>
		public bool PaintInProgress { get; protected set; }

		/// <summary>
		/// Return true if a layout of the view is in progress.
		/// (This is useful because of Windows' pathological habit of calling the WndProc re-entrantly.
		/// Some things should not happen while the paint is in progress and the view may be
		/// under construction.)
		/// </summary>
		public bool LayoutInProgress { get; protected set; }

		/// <summary>
		/// Return true if a paint needs to be aborted because something like a paint or layout
		/// is already in progress. Arranges for a postponed paint if so.
		/// </summary>
		protected bool CheckForRecursivePaint()
		{
			if (PaintInProgress || LayoutInProgress || RootBox != null && RootBox.IsPropChangedInProgress)
			{
				// Somehow, typically some odd side effect of expanding lazy boxes, we
				// got a recursive OnPaint call. This can produce messy results, even
				// crashes, if we let it proceed...in particular, we are probably in
				// the middle of expanding a lazy box, and if PrepareToDraw tries to do
				// further expansion it may conflict with the one already in process
				// with unpredictably disastrous results. So don't do the recursive paint...
				// on the other hand, the paint that is in progress may have been
				// messed up, so request another one.
				Debug.WriteLine($"Recursive OnPaint call for {this}");
				// Calling Invalidate directly can cause an infinite loop of paint calls in certain
				// circumstances.  But we don't want to leave the window incorrectly painted.
				// Postponing the new Invalidate until the application is idle seems a good compromise.
				// In THEORY it should only paint when idle anyway, except for explicit Update calls.
				Application.Idle += PostponedInvalidate;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Call Draw() which does all the real painting
		/// </summary>
		protected override void OnPaint(PaintEventArgs e)
		{
			if (!AllowPainting)
			{
				return;
			}
			// Inform subscribers that the view changed.
			if (VerticalScrollPositionChanged != null && m_lastVerticalScrollPosition != -AutoScrollPosition.Y)
			{
				VerticalScrollPositionChanged(this, m_lastVerticalScrollPosition, -AutoScrollPosition.Y);
				m_lastVerticalScrollPosition = -AutoScrollPosition.Y;
			}

			// NOTE: This "if" needs to stay in synch with the "if" in OnPaintBackground to
			// paint something when the views code isn't painting.
			if (!m_fAllowLayout)
			{
				base.OnPaint(e);
				return;
			}
			if (CheckForRecursivePaint())
			{
				return;
			}
			PaintInProgress = true;
			try
			{
				Draw(e);
			}
			finally
			{
				PaintInProgress = false;
			}
			MouseMoveSuppressed = false; // successful paint, display is stable to do MouseMoved.
		}

		/// <summary>
		/// Executed on the Application Idle queue, this method invalidates the whole view.
		/// It is used when we cannot properly complete a paint because we detect that it
		/// is a recursive call, to ensure that the window is eventually painted properly.
		/// </summary>
		void PostponedInvalidate(object sender, EventArgs e)
		{
#if RANDYTODO_TEST_Application_Idle
// TODO: Remove when finished sorting out idle issues.
Debug.WriteLine($"Start: Application.Idle run at: '{DateTime.Now:HH:mm:ss.ffff}': on '{GetType().Name}'.");
#endif
			Application.Idle -= PostponedInvalidate;
			Invalidate();
#if RANDYTODO_TEST_Application_Idle
// TODO: Remove when finished sorting out idle issues.
Debug.WriteLine($"End: Application.Idle run at: '{DateTime.Now:HH:mm:ss.ffff}': on '{GetType().Name}'.");
#endif
		}

		/// <summary>
		/// Ignore the PaintBackground event because we do it our self (in the views code).
		/// </summary>
		protected override void OnPaintBackground(PaintEventArgs e)
		{
#pragma warning disable 219
			var parent = FindForm();
#pragma warning restore 219
			// Not everything has a form, when it is being designed.
			if (!m_fAllowLayout && AllowPainting)
			{
				base.OnPaintBackground(e);
			}
		}

		/// <summary>
		/// Recompute the layout
		/// </summary>
		protected override void OnLayout(LayoutEventArgs levent)
		{
			if ((!DesignMode || AllowPaintingInDesigner) && m_fRootboxMade && m_fAllowLayout && IsHandleCreated && !LayoutInProgress)
			{
				// Recompute your layout and redraw completely, unless the width has not
				// actually changed.
				// We have to do this before we call base.OnLayout, because the base class copies
				// the scroll range from AutoScrollMinSize to the DisplayRectangle. If we call
				// base.OnLayout first, we get bugs when switching back and forth between maximize
				// and normal.
				using (new HoldGraphics(this))
				{
					if (DoLayout())
					{
						Invalidate();
					}
				}
			}
			if (Platform.IsMono)
			{
				// TODO-Linux: Create simple test case for this and fix mono bug
				try
				{
					base.OnLayout(levent);
				}
				catch (ArgumentOutOfRangeException)
				{
					// TODO-Linux: Investigate real cause of this.
					// I think it caused by mono setting ScrollPosition to 0 when Scrollbar isn't visible
					// it should possibly set it to Minimum value instead.
					// Currently occurs when dropping down scrollbars in Flex.
				}
			}
			else
			{
				base.OnLayout(levent);
			}
		}

		/// <summary>
		/// Get a selection at the indicated point (as in MouseEventArgs.Location). If fInstall is true make it the
		/// active selection.
		/// </summary>
		public IVwSelection GetSelectionAtPoint(Point position, bool fInstall)
		{
			return GetSelectionAtViewPoint(PixelToView(position), fInstall);
		}

		/// <summary>
		/// Get a selection at the indicated point.
		/// </summary>
		/// <param name="position">Point where the selection is to be made</param>
		/// <param name="fInstall">Indicates whether or not to "install" the selection</param>
		public IVwSelection GetSelectionAtViewPoint(Point position, bool fInstall)
		{
			Rectangle rcSrcRoot, rcDstRoot;
			GetCoordRects(out rcSrcRoot, out rcDstRoot);
			try
			{
				return RootBox.MakeSelAt(position.X, position.Y, rcSrcRoot, rcDstRoot, false);
			}
			catch
			{
				// Ignore errors
				return null;
			}
		}

		/// <summary>
		/// Process left or right mouse button down
		/// </summary>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (RootBox == null || DataUpdateMonitor.IsUpdateInProgress())
			{
				return;
			}
			// If we have posted a FollowLink message, then don't process any other
			// mouse event, since will be changing areas, invalidating our MouseEventArgs.
			if (IsFollowLinkMsgPending)
			{
				return;
			}
			SwitchFocusHere();

			// Convert to box coords and pass to root box (if any).
			Point pt;
			Rectangle rcSrcRoot;
			Rectangle rcDstRoot;
			using (new HoldGraphics(this))
			{
				pt = PixelToView(new Point(e.X, e.Y));
				GetCoordRects(out rcSrcRoot, out rcDstRoot);

				// Note we need to UninitGraphics before processing CallMouseDown since that call
				// may jump to another record which invalidates this graphics object.
			}

			if (e.Button == MouseButtons.Right)
			{
				if (DataUpdateMonitor.IsUpdateInProgress())
				{
					return; //discard this event
				}
				if (IsFollowLinkMsgPending)
				{
					return; //discard this event
				}
				using (new HoldGraphics(this))
				{
					var sel = (IVwSelection)RootBox.Selection;
					// Use the existing selection if it covers the point clicked, and if it's
					// either a range or editable.
					if (sel != null && (sel.IsRange || SelectionHelper.IsEditable(sel)))
					{
						// TODO KenZ: We need a better way to determine if the cursor is in a selection
						// when the selection spans partial lines.

						// We don't want to destroy a range selection if we are within the range, since it
						// is quite likely the user will want to do a right+click cut or paste.
						Rect rcPrimary;
						Rect rcSecondary;
						bool fSplit;
						bool fEndBeforeAnchor;

						Debug.Assert(m_graphicsManager.VwGraphics != null);

						sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSecondary, out fSplit, out fEndBeforeAnchor);

						if (pt.X >= rcPrimary.left && pt.X < rcPrimary.right && pt.Y >= rcPrimary.top && pt.Y < rcPrimary.bottom)
						{
							return;
						}
					}

					try
					{
						// Make an invisible selection to see if we are in editable text.
						sel = RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, false);
						if (sel != null && SelectionHelper.IsEditable(sel))
						{
							// Make a simple text selection without executing a mouse click. This is
							// needed in order to not launch a hot link when we right+click.
							RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, true);
							return;
						}
					}
					catch
					{
					}
				}
			}

			if ((ModifierKeys & Keys.Shift) == Keys.Shift)
			{
				CallMouseDownExtended(pt, rcSrcRoot, rcDstRoot);
			}
			else
			{
				CallMouseDown(pt, rcSrcRoot, rcDstRoot);
			}
		}

		/// <summary>
		/// Process right mouse button up (typically show a context menu).
		/// Was mouse Down in an earlier life, but we concluded that the usual convention
		/// is context menu on mouse up. There may be vestiges.
		/// </summary>
		protected virtual bool OnRightMouseUp(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			IVwSelection invSel = null;
			try
			{
				// Make an invisible selection to see if we are in editable text, also it may
				// be useful to DoContextMenu.
				invSel = RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, false);
				// Notify any delegates.
				if (RightMouseClickedEvent != null)
				{
					if (invSel != null)
					{
						var args = new FwRightMouseClickEventArgs(pt, invSel);
						RightMouseClickedEvent(this, args);
						if (args.EventHandled)
						{
							return true;
						}
					}
				}
			}
			catch
			{
			}

			return DataUpdateMonitor.IsUpdateInProgress() || (IsFollowLinkMsgPending || DoContextMenu(invSel, pt, rcSrcRoot, rcDstRoot));
		}

		/// <summary>
		/// The user has chosen a keyboard combination which requests a context menu.
		/// Handle it, given the active selection and a point around the center of it.
		/// </summary>
		protected virtual bool HandleContextMenuFromKeyboard(IVwSelection vwsel, Point center)
		{
			if (RightMouseClickedEvent != null)
			{
				var args = new FwRightMouseClickEventArgs(center, vwsel);
				RightMouseClickedEvent(this, args);
				if (args.EventHandled)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Override to provide default handling of Context menu key.
		/// </summary>
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Apps || keyData == (Keys.F10 | Keys.Shift))
			{
				if (RootBox?.Selection != null)
				{
					Point pt;
					// Set point to somewhere around the middle of the selection in window coords.
					using (new HoldGraphics(this))
					{
						Rectangle rcSrcRoot, rcDstRoot;
						Rect rcSec, rcPrimary;
						bool fSplit, fEndBeforeAnchor;
						GetCoordRects(out rcSrcRoot, out rcDstRoot);
						RootBox.Selection.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSec, out fSplit, out fEndBeforeAnchor);

						pt = new Point((rcPrimary.right + rcPrimary.left) / 2, (rcPrimary.top + rcPrimary.bottom) / 2);
					}
					if (HandleContextMenuFromKeyboard(RootBox.Selection, pt))
					{
						return true;
					}
					// These two checks are copied from OnRightMouseUp; not sure why (or whether) they are needed.
					if (DataUpdateMonitor.IsUpdateInProgress())
					{
						return true; //discard this event
					}
					if (IsFollowLinkMsgPending)
					{
						return true; //discard this event
					}
					using (var hg = new HoldGraphics(this))
					{
						Rectangle rcSrcRoot, rcDstRoot;
						GetCoordRects(out rcSrcRoot, out rcDstRoot);
						return DoContextMenu(RootBox.Selection, pt, rcSrcRoot, rcDstRoot);
					}

				}
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		/// <summary>
		/// Override this to provide a context menu for some subclass.
		/// </summary>
		protected virtual bool DoContextMenu(IVwSelection invSel, Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			return false;
		}

		/// <summary>
		/// Process mouse double click
		/// </summary>
		protected override void OnDoubleClick(EventArgs e)
		{
			base.OnDoubleClick(e);

			// Convert to box coords and pass to root box (if any).
			if (RootBox != null && !DataUpdateMonitor.IsUpdateInProgress())
			{
				SwitchFocusHere();
				using (new HoldGraphics(this))
				{
					var pt = PointToClient(Cursor.Position);
					Rectangle rcSrcRoot;
					Rectangle rcDstRoot;
					GetCoordRects(out rcSrcRoot, out rcDstRoot);

					CallMouseDblClk(PixelToView(pt), rcSrcRoot, rcDstRoot);
				}
			}
		}

		/// <summary>
		/// Process mouse button up event
		/// </summary>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return;
			}
			// Convert to box coords and pass to root box (if any).
			if (RootBox != null)
			{
				using (new HoldGraphics(this))
				{
					var pt = PixelToView(new Point(e.X, e.Y));
					Rectangle rcSrcRoot;
					Rectangle rcDstRoot;
					GetCoordRects(out rcSrcRoot, out rcDstRoot);
					CallMouseUp(pt, rcSrcRoot, rcDstRoot);

					if (e.Button == MouseButtons.Right)
					{
						OnRightMouseUp(pt, rcSrcRoot, rcDstRoot);
					}
				}
			}
		}

		/// <summary>
		/// Handles OnKeyPress. Passes most things to EditingHelper.OnKeyPress
		/// </summary>
		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			base.OnKeyPress(e);
			if (!e.Handled)
			{
				// Let the views code handle the key
				using (new HoldGraphics(this))
				{
					Debug.Assert(m_graphicsManager.VwGraphics != null);
					var modifiers = ModifierKeys;
					// Ctrl-Backspace actually comes to us having the same key code as Delete, so we fix it here.
					// (Ctrl-Delete doesn't come through this code path at all.)
					if (modifiers == Keys.Control && e.KeyChar == '\u007f')
					{
						e.KeyChar = '\b';
					}
					EditingHelper.OnKeyPress(e, modifiers);
				}
			}
		}

		/// <summary>
		/// Checks input characters to see if they should be processsed. Static to allow
		/// function to be shared with PublicationControl.
		/// </summary>
		public static bool IsIgnoredKey(KeyPressEventArgs e, Keys modifiers)
		{
			var ignoredKey = false;
			if ((modifiers & Keys.Shift) == Keys.Shift && e.KeyChar == '\r')
			{
				ignoredKey = true;
			}
			else if ((modifiers & Keys.Control) == Keys.Control)
			{
				// only backspace, forward delete and control-M (same as return key) will be
				// passed on for processing
				ignoredKey = !(e.KeyChar == (int)VwSpecialChars.kscBackspace || e.KeyChar == (int)VwSpecialChars.kscDelForward || e.KeyChar == '\r');
			}

			return ignoredKey;
		}

		/// <summary>
		/// Gets or sets a value indicating whether pressing the TAB key will be handled by this control
		/// instead of moving the focus to the next control in the tab order.
		/// </summary>
		public bool AcceptsTab { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether pressing the ENTER key will be handled by this control
		/// instead of activating the default button for the form.
		/// </summary>
		public bool AcceptsReturn { get; set; }

		/// <summary>
		/// Clean up after page scrolling.
		/// </summary>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return; //throw this event away
			}
			base.OnKeyUp(e);
		}

		/// <summary>
		/// User pressed a key.
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return; //throw this event away
			}
			using (new HoldGraphics(this))
			{
				if (e.KeyCode == Keys.PageUp && ((e.Modifiers & Keys.Control) == Keys.Control))
				{
					GoToPageTop((e.Modifiers & Keys.Shift) == Keys.Shift);
				}
				else if (e.KeyCode == Keys.PageDown && ((e.Modifiers & Keys.Control) == Keys.Control))
				{
					GoToPageBottom((e.Modifiers & Keys.Shift) == Keys.Shift);
				}
				else
				{
					base.OnKeyDown(e);
				}

				Debug.Assert(m_graphicsManager.VwGraphics != null);

				if (!e.Handled)
				{
					EditingHelper.OnKeyDown(e);
				}
			}
		}

		/// <summary>
		/// Determines whether the specified key is a regular input key or a special key that
		/// requires preprocessing.
		/// Default implementation always returns true because we want to handle all keys!
		/// </summary>
		/// <param name="keyData">One of the <see cref="T:System.Windows.Forms.Keys"/> values.</param>
		/// <returns>
		/// true if the specified key is a regular input key; otherwise, false.
		/// </returns>
		/// <remarks>IsInputKey gets called while processing WM_KEYDOWN or WM_SYSKEYDOWN.</remarks>
		protected override bool IsInputKey(Keys keyData)
		{
			if ((keyData & Keys.Alt) != Keys.Alt)
			{
				switch (keyData & Keys.KeyCode)
				{
					case Keys.Return:
						return AcceptsReturn;

					case Keys.Escape:
						return false;

					case Keys.Tab:
						return AcceptsTab && (keyData & Keys.Control) == Keys.None;
				}
			}
			return true;
		}

		/// <summary>
		/// This also helps us handle all input keys...the documentation doesn't make very
		/// clear the distinction between this and IsInputKey, but failing to override this
		/// one can cause typing in the view to invoke buttons in the parent form. If you
		/// want to remove this, make sure it doesn't mess up windows that contain both views
		/// and buttons, such as the LexText interlinear view.
		/// </summary>
		/// <param name="charCode">The character to test.</param>
		/// <returns>
		/// true if the character should be sent directly to the control and not preprocessed;
		/// otherwise, false.
		/// </returns>
		/// <remarks>IsInputChar gets called while processing WM_CHAR or WM_SYSCHAR.</remarks>
		protected override bool IsInputChar(char charCode)
		{
			return true;
		}

		/// <summary>
		/// Gets a value indicating whether a selection changed is being handled.
		/// </summary>
		public virtual bool InSelectionChanged
		{
			get { return false; }
			set { }
		}

		/// <summary>
		/// Subclasses can override if they handle the scrolling for OnSizeChanged().
		/// </summary>
		protected virtual bool ScrollToSelectionOnSizeChanged => true;

		/// <summary>
		/// Size changed. Ensure that the selection is still visible.
		/// </summary>
		protected override void OnSizeChanged(EventArgs e)
		{
			// Ignore if our size didn't really change, also if we're in the middle of a paint.
			// (But, don't ignore if not previously laid out successfully...this can suppress
			// a necessary Layout after the root box is made.)
			if (PaintInProgress || LayoutInProgress || RootBox == null || (Size == m_sizeLast && m_dxdLayoutWidth >= 0))
			{
				// This is needed to handle .Net layout for the Control.
				base.OnSizeChanged(e);
				return;
			}

			// If the selection is not visible, then make a selection at the top of the view
			// that we can scroll to the top after the size changes. This keeps the view from
			// scrolling all over the place when the size is changed.
			IVwSelection selToScroll = null;
			if (ScrollToSelectionOnSizeChanged && !IsSelectionVisible(null) && AllowLayout)
			{
				using (new HoldGraphics(this))
				{
					Rectangle rcSrc;
					Rectangle rcDst;
					GetCoordRects(out rcSrc, out rcDst);
					LayoutInProgress = true;
					try
					{
						using (new SuspendDrawing(this))
						{
							selToScroll = RootBox.MakeSelAt(5, 5, rcSrc, rcDst, false);
						}
					}
					catch
					{
						// just ignore any errors we get...
						selToScroll = null;
					}
					finally
					{
						LayoutInProgress = false;
					}
				}
			}

			if (Platform.IsMono)
			{
				try
				{
					base.OnSizeChanged(e);
				}
				catch (ArgumentOutOfRangeException)
				{
					// TODO-Linux: Investigate real cause of this.
					// I think it caused by mono setting ScrollPosition to 0 when Scrollbar isn't visible
					// it should possibly set it to Minimum value instead.
					// Currently occurs when dropping down scrollbars in Flex.
				}
			}
			else
			{
				base.OnSizeChanged(e);
			}

			// Sometimes (e.g., I tried this in InterlinDocChild), we destroy the root box when
			// the pane is collapsed below a workable size.
			if (RootBox == null)
			{
				return;
			}
			// This was moved down here because the actual size change happens in
			// base.OnSizeChanged() so our size property could be wrong before that call
			m_sizeLast = Size;

			// REVIEW: We don't think it makes sense to do anything more if our width is 0.
			// Is this right?
			if (m_sizeLast.Width <= 0)
			{
				return;
			}
			if (ScrollToSelectionOnSizeChanged && !SizeChangedSuppression && !m_fRefreshPending && IsHandleCreated)
			{
				if (selToScroll == null)
				{
					ScrollSelectionIntoView(null, VwScrollSelOpts.kssoDefault);
				}
				else
				{
					// we want to scroll the selection into view at the top of the window
					// so that the text pretty much stays at the same position even when
					// the IP isn't visible.
					ScrollSelectionIntoView(selToScroll, VwScrollSelOpts.kssoTop);
				}
			}
		}

		// ENHANCE (EberhardB): implement passing of System characters to rootbox. Since the
		// rootbox doesn't do anything with that at the moment, I left that for later.

		/// <summary>
		/// Gets a value indicating whether the <see cref="P:System.Windows.Forms.Control.ImeMode"/>
		/// property can be set to an active value, to enable IME support.
		/// </summary>
		/// <returns>true in all cases.</returns>
		/// <remarks>This fixes part of LT-7487. This property requires .NET 2.0 SP1 or higher.
		/// </remarks>
		protected override bool CanEnableIme => true;
		#endregion

		#region Other virtual methods

		/// <summary>
		/// Set/Get the writing system factory used by this root site. The main RootSite
		/// subclass obtains this from the LcmCache if it has not been set.
		/// </summary>
		public virtual ILgWritingSystemFactory WritingSystemFactory
		{
			get
			{
				return m_wsf;
			}
			set
			{
				m_wsf = value;
			}
		}

		/// <summary>
		/// Returns the scroll position. Values are positive.
		/// </summary>
		protected internal virtual void GetScrollOffsets(out int dxd, out int dyd)
		{
			dxd = -ScrollPosition.X;
			dyd = -ScrollPosition.Y;
		}

		/// <summary>
		/// Adjust the scroll range when some lazy box got expanded.
		/// This is rather similar to SizeChanged, but is used when the size changed
		/// as a result of recomputing something that is invisible (typically about to become
		/// visible, but not currently on screen). Thus, the scroll bar range and possibly
		/// position need adjusting, but it isn't necessary to actually redraw anything except
		/// the scroll bar--unless the scroll position is forced to change, because we were
		/// in the process of scrolling to somewhere very close to the end, and the expansion
		/// was smaller than predicted, and the total range is now less than the current
		/// position.
		/// </summary>
		/// <param name="dxdSize"><paramref name="dydSize"/></param>
		/// <param name="dxdPosition"><paramref name="dydPosition"/></param>
		/// <param name="dydSize">The change (positive means larger) in the overall size of the
		/// root box</param>
		/// <param name="dydPosition">The position where the change happened. In general it may be
		/// assumed that if this change is above the thumb position, everything that changed
		/// is above it, and it needs to be increased by dydSize; otherwise, everything is below
		/// the screen, and no change to the thumb position is needed.</param>
		/// <returns><c>true</c> when the scroll position changed, otherwise <c>false</c>.
		/// </returns>
		/// <remarks>
		/// The <see cref="ScrollableControl.DisplayRectangle"/> determines the scroll range.
		/// To set it, you have to set <see cref="ScrollableControl.AutoScrollMinSize"/> and do
		/// a PerformLayout.
		/// </remarks>
		protected virtual bool AdjustScrollRange1(int dxdSize, int dxdPosition, int dydSize, int dydPosition)
		{
			var dydRangeNew = AdjustedScrollRange.Height;
			var dydPosNew = -ScrollPosition.Y;
			// Remember: ScrollPosition returns negative values!
			// If the current position is after where the change occurred, it needs to
			// be adjusted by the same amount.
			if (dydPosNew > dydPosition)
			{
				dydPosNew += dydSize;
			}
			var dxdRangeNew = AutoScrollMinSize.Width + dxdSize;
			var dxdPosNew = -ScrollPosition.X;
			if (HScroll && dxdPosNew > dxdPosition)
			{
				// Similarly for horizontal scroll bar.
				dxdPosNew += dxdSize;
			}

			// We don't want to change the location of any child controls (e.g., a focus box).
			// Changing the scroll position will unfortunately do so. Best we can do is restore them afterwards.
			var oldLocations = new List<Tuple<Control, Point>>();
			foreach (Control c in Controls)
			{
				oldLocations.Add(new Tuple<Control, Point>(c, c.Location));
			}
			var result = UpdateScrollRange(dxdRangeNew, dxdPosNew, dydRangeNew, dydPosNew);
			foreach (var locationTuple in oldLocations)
			{
				locationTuple.Item1.Location = locationTuple.Item2;
			}
			return result;
		}

		/// <summary>
		/// Draw to the given clip rectangle.
		/// </summary>
		/// <remarks>OPTIMIZE JohnT: pass clip rect to VwGraphics and make use of it.</remarks>
		protected virtual void Draw(PaintEventArgs e)
		{
			if (RootBox != null && m_dxdLayoutWidth > 0 && (!DesignMode || AllowPaintingInDesigner))
			{
				Debug.Assert(m_vdrb != null, "Need to call MakeRoot() before drawing");
				var hdc = e.Graphics.GetHdc();

				try
				{
					var parent = FindForm();
					if (parent != null && !parent.Enabled)
					{
						if (m_haveCachedDrawForDisabledView)
						{
							m_vdrb.ReDrawLastDraw(hdc, e.ClipRectangle);
						}
						else
						{
							m_orientationManager.DrawTheRoot(m_vdrb, RootBox, hdc, ClientRectangle, ColorUtil.ConvertColorToBGR(BackColor), true, ClientRectangle);
							m_haveCachedDrawForDisabledView = true;
						}
					}
					else
					{
						m_haveCachedDrawForDisabledView = false;
						// NOT (uint)(BackColor.ToArgb() & 0xffffff); this orders the components
						// as RRGGBB where C++ View code using the RGB() function requires BBGGRR.
						var rgbBackColor = ColorUtil.ConvertColorToBGR(BackColor);
						m_orientationManager.DrawTheRoot(m_vdrb, RootBox, hdc, ClientRectangle, rgbBackColor, true,
							Platform.IsMono ? Rectangle.Intersect(e.ClipRectangle, ClientRectangle) : e.ClipRectangle);
					}
				}
				catch
				{
					Debug.WriteLine("Draw exception."); // Just eat it
				}
				finally
				{
					e.Graphics.ReleaseHdc(hdc);
				}
				try
				{
					InitGraphics();
					RootBox.DrawingErrors(m_graphicsManager.VwGraphics);
				}
				catch (Exception ex)
				{
					ReportDrawErrMsg(ex);
				}
				finally
				{
					UninitGraphics();
				}
			}
			else
			{
				e.Graphics.FillRectangle(SystemBrushes.Window, ClientRectangle);
				e.Graphics.DrawString("Empty " + GetType(), SystemInformation.MenuFont, SystemBrushes.WindowText, ClientRectangle);
			}
		}

		/// <summary>
		/// Set focus to our window
		/// </summary>
		private void SwitchFocusHere()
		{
			if (FindForm() == Form.ActiveForm)
			{
				Focus();
			}
		}

		/// <summary>
		/// Override this method in your subclass.
		/// It should make a root box and initialize it with appropriate data and
		/// view constructor, etc.
		/// </summary>
		public virtual void MakeRoot()
		{
			if (!GotCacheOrWs || (DesignMode && !AllowPaintingInDesigner))
			{
				return;
			}
			SetAccessibleName(Name);

			// Managed object on Linux
			m_vdrb = Platform.IsMono ? new Views.VwDrawRootBuffered() : (IVwDrawRootBuffered)VwDrawRootBufferedClass.Create();

			RootBox = VwRootBoxClass.Create();
			RootBox.RenderEngineFactory = SingletonsContainer.Get<RenderEngineFactory>();
			RootBox.TsStrFactory = TsStringUtils.TsStrFactory;
			RootBox.SetSite(this);

			m_fRootboxMade = true;
		}

		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Subclasses should override CloseRootBox if something else is still using it
		/// after the window closes. In that case the subclass should do nothing.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		public virtual void CloseRootBox()
		{
			if (DesignMode || RootBox == null)
			{
				return;
			}
			m_Timer?.Stop();
			// Can't flash IP, if the root box is toast.

			RootBox.Close();
			// After the rootbox is closed its useless...
			if (Marshal.IsComObject(RootBox))
			{
				Marshal.ReleaseComObject(RootBox);
			}
			RootBox = null;
		}

		/// <summary>
		/// If a particular rootsite needs to do something special for putting the current
		/// selection on the keyboard, implement this method to do it, and have it return
		/// the filled-in ITsString object.  See LT-9475 for justification.
		/// </summary>
		public virtual ITsString GetTsStringForClipboard(IVwSelection vwsel)
		{
			return null;
		}

		/// <summary>
		/// Show the writing system choices?
		/// </summary>
		public virtual bool IsSelectionFormattable => true;

		/// <summary>
		/// Answer true if the Apply Styles menu option should be enabled.
		/// </summary>
		public virtual bool CanApplyStyle => IsSelectionFormattable;

		/// <summary>
		/// Get the best style name that suits the selection.
		/// </summary>
		public virtual string BestSelectionStyle => string.Empty;

		/// <summary>
		/// Show paragraph styles?
		/// </summary>
		public virtual bool IsSelectionInParagraph => false;

		#region Methods that delegate events to the rootbox

		/// <summary>
		/// Call MouseDown on the rootbox
		/// </summary>
		protected virtual void CallMouseDown(Point point, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			if (RootBox != null)
			{
				var oldSel = RootBox.Selection;
				WsPending = -1;

				try
				{
					RootBox.MouseDown(point.X, point.Y, rcSrcRoot, rcDstRoot);
				}
				catch
				{
					// This was causing unhandled exceptions from the MakeSelection call in the C++ code
					if (Platform.IsMono)
					{
						// So exception isn't silently hidden (FWNX-343)
						Debug.Assert(MiscUtils.RunningTests, "m_rootb.MouseDown threw exception.");
					}
				}

				EditingHelper.HandleMouseDown();
				var newSel = RootBox.Selection;
				// Some clicks (e.g., on a selection check box in a bulk edit view) don't result in
				// a new selection. If something old is selected, it can be bad to scroll away from
				// the user's focus to the old selection.
				if (newSel != null && newSel != oldSel)
				{
					MakeSelectionVisible(null);
				}
			}
		}

		/// <summary>
		/// Call MouseDblClk on rootbox
		/// </summary>
		protected virtual void CallMouseDblClk(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			RootBox?.MouseDblClk(pt.X, pt.Y, rcSrcRoot, rcDstRoot);
		}

		/// <summary>
		/// Call MouseDownExtended on the rootbox
		/// </summary>
		protected virtual void CallMouseDownExtended(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			if (RootBox != null)
			{
				WsPending = -1;

				RootBox.MouseDownExtended(pt.X, pt.Y, rcSrcRoot, rcDstRoot);
				MakeSelectionVisible(null);
			}
		}

		/// <summary>
		/// Call MouseMoveDrag on the rootbox
		/// </summary>
		protected virtual void CallMouseMoveDrag(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			if (RootBox != null && m_fMouseInProcess == false)
			{
				// various scrolling speeds, i.e. 10 pixels per mousemovedrag event when
				// dragging beyond the top or bottom of of the rootbox
				const int LOSPEED = 10;
				const int HISPEED = 30;
				int speed;
				const int SLOW_BUFFER_ZONE = 30;
				m_fMouseInProcess = true;
				if (DoAutoVScroll && pt.Y < 0)  // if drag above the top of rootbox
				{
					// if mouse is within 30 pixels of top of rootbox
					speed = pt.Y > -SLOW_BUFFER_ZONE ? LOSPEED : HISPEED;
					// regardless of where pt.Y is, select only to top minus "speed",
					// then scroll up by "speed" number of pixels, so top of selection is
					// always at top of rootbox
					RootBox.MouseMoveDrag(pt.X, -speed, rcSrcRoot, rcDstRoot);
					ScrollPosition = new Point(-ScrollPosition.X, -ScrollPosition.Y - speed);
					Refresh();
				}
				else if (DoAutoVScroll && pt.Y > Bottom)  // if drag below bottom of rootbox
				{
					// if mouse is within 30 pixels of bottom of rootbox
					speed = (pt.Y - Bottom) < SLOW_BUFFER_ZONE ? LOSPEED : HISPEED;
					// regardless of where pt.Y is, select only to bottom plus "speed",
					// then scroll down by "speed" number of pixels, so bottom of selection
					// is always at bottom of rootbox
					RootBox.MouseMoveDrag(pt.X, Bottom + speed, rcSrcRoot, rcDstRoot);
					ScrollPosition = new Point(-ScrollPosition.X, -ScrollPosition.Y + speed);
					Refresh();
				}
				else  // selection and drag is occuring all within window boundaries
				{
					RootBox.MouseMoveDrag(pt.X, pt.Y, rcSrcRoot, rcDstRoot);
				}
				m_fMouseInProcess = false;
			}
		}

		/// <summary>
		/// Call MouseUp on the rootbox
		/// </summary>
		protected virtual void CallMouseUp(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			var y = pt.Y;
			if (RootBox != null)
			{
				if (Parent is ScrollableControl)
				{
					y += ((ScrollableControl)Parent).AutoScrollPosition.Y;
				}
				// if we're dragging to create or extend a selection
				// don't select text that is currently above or below current viewable area
				if (y < 0)  // if mouse is above viewable area
				{
					RootBox.MouseUp(pt.X, 0, rcSrcRoot, rcDstRoot);
				}
				else if (y > Bottom)  // if mouse is below viewable area
				{
					RootBox.MouseUp(pt.X, Bottom, rcSrcRoot, rcDstRoot);
				}
				else  // mouse is inside viewable area
				{
					RootBox.MouseUp(pt.X, pt.Y, rcSrcRoot, rcDstRoot);
				}
			}
		}

		#endregion // Methods that delegate events to the rootbox

		/// <summary>
		/// Writing systems the user might reasonably choose. Overridden in RootSite to limit it to the ones active in this project.
		/// </summary>
		protected virtual CoreWritingSystemDefinition[] PlausibleWritingSystems
		{
			get
			{
				var manager = (WritingSystemManager)WritingSystemFactory;
				return manager == null ? new CoreWritingSystemDefinition[0] : manager.WritingSystemStore.AllWritingSystems.ToArray();
			}
		}

		/// <summary>
		/// When the user has selected a keyboard from the system tray, adjust the language of
		/// the selection to something that matches, if possible.
		/// </summary>
		/// <param name="vwsel">Selection</param>
		/// <param name="wsMatch">Writing system determined from keyboard change</param>
		public virtual void HandleKeyboardChange(IVwSelection vwsel, int wsMatch)
		{
			// Get the writing system factory associated with the root box.
			if (RootBox == null || !GotCacheOrWs)
			{
				return; // For paranoia.
			}
			if (vwsel == null)
			{
				// Delay handling it until we get a selection.
				WsPending = wsMatch;
				return;
			}
			var fRange = vwsel.IsRange;
			if (fRange)
			{
				// Delay handling it until we get an insertion point.
				WsPending = wsMatch;
				return;
			}
			ITsTextProps[] vttp;
			IVwPropertyStore[] vvps;
			int cttp;

			SelectionHelper.GetSelectionProps(vwsel, out vttp, out vvps, out cttp);

			if (cttp == 0)
			{
				return;
			}
			Debug.Assert(cttp == 1);

			// If nothing changed, avoid the infinite loop that happens when we change the selection
			// and update the system keyboard, which in turn tells the program to change its writing system.
			// (This is a problem on Windows 98.)
			int wsTmp;
			int var;
			wsTmp = vttp[0].GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			if (wsTmp == wsMatch)
			{
				return;
			}
			var tpb = vttp[0].GetBldr();
			tpb.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsMatch);
			vttp[0] = tpb.GetTextProps();
			vwsel.SetSelectionProps(cttp, vttp);
			SelectionChanged(RootBox, RootBox.Selection); // might NOT be vwsel any more
		}

		/// <summary>
		/// get the writing systems we should consider as candidates to be selected whent the user makes an
		/// external choice of keyboard. overridden in root site to use only active ones.
		/// </summary>
		protected virtual int[] GetPossibleWritingSystemsToSelectByInputLanguage(ILgWritingSystemFactory wsf)
		{
			var cws = wsf.NumberOfWs;
			var vwsTemp = new int[0];
			using (var ptr = MarshalEx.ArrayToNative<int>(cws))
			{
				wsf.GetWritingSystems(ptr, cws);
				vwsTemp = MarshalEx.NativeToArray<int>(ptr, cws);
			}
			return vwsTemp;
		}

		#endregion // Other virtual methods

		#region Other non-virtual methods

		/// <summary>
		/// Get the primary rectangle occupied by a selection (relative to the top left of the client rectangle).
		/// </summary>
		public Rect GetPrimarySelRect(IVwSelection sel)
		{
			Rect rcPrimary;
			using (new HoldGraphics(this))
			{
				Rectangle rcSrcRoot, rcDstRoot;
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				Rect rcSec;
				bool fSplit, fEndBeforeAnchor;
				sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out rcPrimary, out rcSec, out fSplit, out fEndBeforeAnchor);
			}
			return rcPrimary;
		}

		/// <summary>
		/// Updates the state of the selection (enabled/disabled).
		/// </summary>
		/// <param name="newWindow">The window about to receive focus</param>
		private void UpdateSelectionEnabledState(Control newWindow)
		{
			RootBox.Activate(GetNonFocusedSelectionState(newWindow));
		}

		/// <summary>
		/// Unless overridden, this will return a value indicating that range selections
		/// should be hidden.
		/// </summary>
		protected virtual VwSelectionState GetNonFocusedSelectionState(Control windowGainingFocus)
		{
			// If the selection exists, disable it.  This fixes LT-1203 and LT-1488.  (At one
			// point, we disabled only IP selections, but the leftover highlighted ranges
			// confused everyone.)
			// NOTE (TimS): The fix for LT-1203/LT-1488 broke TE's find replace so the
			// property ShowRangeSelAfterLostFocus was created.
			// Change (7/19/2006): Depending on the window that gets focus we leave the selection.
			// If user clicked e.g. on the toolbar it should still be enabled, but if user clicked
			// in a different view window we want to hide it (TE-3977). If user clicked on a
			// different app the selection should be hidden. If the user clicked on a second
			// window of our app we want to hide the selection.
			if ((m_fIsTextBox || windowGainingFocus == null || windowGainingFocus is SimpleRootSite
			     || ParentForm == null || !ParentForm.Contains(windowGainingFocus)) && !ShowRangeSelAfterLostFocus)
			{
				return VwSelectionState.vssDisabled;
			}

			return VwSelectionState.vssOutOfFocus;
		}

		/// <summary>
		/// This method is designed to be used by classes whose root object may be
		/// determined after the window's handle is created (when MakeRoot is normally
		/// called) and may be subsequently changed. It is passed the required
		/// arguments for SetRootObject, and if the root box already exists, all it
		/// does is call SetRootObject. If the root box does not already exist, it
		/// calls MakeRoot immediately, and arranges for the root box so created to
		/// be laid out, since it is possible that we have 'missed our chance'
		/// to have this happen when the window gets its initial OnSizeChanged message.
		/// </summary>
		public void ChangeOrMakeRoot(int hvoRoot, IVwViewConstructor vc, int frag, IVwStylesheet styleSheet)
		{
			if (RootBox == null)
			{
				MakeRoot();
				if (m_dxdLayoutWidth < 0)
				{
					OnSizeChanged(new EventArgs());
				}
			}
			else
			{
				RootBox.SetRootObject(hvoRoot, vc, frag, styleSheet);
			}
		}

		/// <summary>
		/// In DetailViews there are some root boxes which are created but (because scrolled out
		/// of view) never made visible. In certain circumstances, as their containers are
		/// disposed, something gets called that may cause calls to methods like OnLoad or
		/// OnHandleCreated. When we know a root site is about to become garbage, we want these
		/// methods to do as little as possible, both for performance and to prevent possible
		/// crashes as windows are created for (e.g.) objects that have been deleted.
		/// </summary>
		public void AboutToDiscard()
		{
			AllowLayout = false;
			m_fMakeRootWhenHandleIsCreated = false;
		}

		/// <summary>
		/// Make a selection that includes all the text.
		/// </summary>
		public void SelectAll()
		{
			EditingHelper.SelectAll();
		}

		/// <summary>
		/// Find out if this RootSite had Focus before being grabbed by some other entity (such as a Windows ComboBox)
		/// </summary>
		public bool WasFocused()
		{
			return g_focusRootSite.Target == this;
		}

		/// <summary>
		/// Some situations lead to invalidating very large rectangles. Something seems to go wrong
		/// if they are way bigger than the client rectangle. Finding the intersection makes it more
		/// reliable.
		/// </summary>
		private void CallInvalidateRect(Rectangle rect, bool fErase)
		{
			rect.Intersect(ClientRectangle);
			if (rect.Height <= 0 || rect.Width <= 0)
			{
				return; // no overlap, may not produce paint.
			}
			MouseMoveSuppressed = true; // until we paint and have a stable display.
			Invalidate(rect, fErase);
		}

		/// <summary>
		/// Construct coord transformation rectangles. Height and width are dots per inch.
		/// src origin is 0, dest origin is controlled by scrolling.
		/// </summary>
		protected internal void GetCoordRects(out Rectangle rcSrcRoot, out Rectangle rcDstRoot)
		{
			m_orientationManager.GetCoordRects(out rcSrcRoot, out rcDstRoot);
		}

		/// <summary>
		/// Allows RootSite to override, and still access the non-overridden ranges.
		/// </summary>
		protected virtual Size AdjustedScrollRange => ScrollRange;

		/// <summary>
		/// To make sure ScrollRange is enough.
		///
		/// Add 8 to get well clear of the descenders of the last line;
		/// about 4 is needed but we add a few more for good measure
		/// </summary>
		protected virtual int ScrollRangeFudgeFactor => 8;

		/// <summary>
		/// Gets the scroll ranges in both directions.
		/// </summary>
		protected virtual Size ScrollRange
		{
			get
			{
				int dysHeight;
				int dxsWidth;

				try
				{
					dysHeight = RootBox.Height;
					dxsWidth = RootBox.Width;
				}
				catch (COMException e)
				{
					Debug.WriteLine($"RootSite.UpdateScrollRange(): Unable to get height/width of rootbox. Source={e.Source}, Method={e.TargetSite}, Message={e.Message}");
					m_dxdLayoutWidth = kForceLayout; // No drawing until we get successful layout.
					return new Size(0, 0);
				}
				// Refactor JohnT: would it be clearer to extract this into a method of orientation manager?
				if (IsVertical)
				{
					//swap and add margins to height
					var temp = dysHeight;
					dysHeight = dxsWidth;
					dxsWidth = temp;
					dysHeight += HorizMargin * 2;
				}
				else
				{
					dxsWidth += HorizMargin * 2; // normal, add margins to width
				}

				Rectangle rcSrcRoot;
				Rectangle rcDstRoot;
				using (new HoldGraphics(this))
				{
					GetCoordRects(out rcSrcRoot, out rcDstRoot);
				}

				var result = new Size(0, 0)
				{
					// 32 bits should be enough for a scroll range but it may not be enough for
					// the intermediate result scroll range * 96 or so.
					// Review JohnT: should we do this adjustment differently if vertical?
					Height = (int)((long)dysHeight * rcDstRoot.Height / rcSrcRoot.Height),
					Width = (int)((long)dxsWidth * rcDstRoot.Width / rcSrcRoot.Width)
				};
				if (IsVertical)
				{
					result.Width += ScrollRangeFudgeFactor;
				}
				else
				{
					result.Height += ScrollRangeFudgeFactor;
				}
				return result;
			}
		}

		/// <summary>
		/// True if the control is being scrolled and should have its ScrollMinSize
		/// adjusted and its AutoScrollPosition modified. For example, an XmlBrowseViewBase
		/// scrolls, but using a separate scroll bar.
		/// </summary>
		public virtual bool DoingScrolling => AutoScroll;

		/// <summary>
		/// Update your scroll range to reflect current conditions.
		/// </summary>
		protected void UpdateScrollRange()
		{
			if (!DoingScrolling || RootBox == null)
			{
				return;
			}
			var range = AdjustedScrollRange;
			var scrollpos = ScrollPosition;
			UpdateScrollRange(range.Width, -scrollpos.X, range.Height, -scrollpos.Y);
		}

		/// <summary>
		/// Indicates whether a view wants a horizontal scroll bar.
		/// Note that just setting HScroll true cannot be used as an indication of this,
		/// because if ever the content width is small enough not to require a horizontal
		/// scroll bar, HScroll is set false by setting ScrollMinSize. If we use it as a flag,
		/// we will never after set a non-zero horizontal scroll min size.
		/// </summary>
		protected virtual bool WantHScroll => IsVertical;

		/// <summary>
		/// Indicates whether we want a vertical scroll bar. For example, in a slice, we
		/// do not, even if we are autoscrolling to produce a horizontal one when needed.
		/// </summary>
		protected virtual bool WantVScroll => true;

		/// <summary>
		/// Update the scroll range with the new range and position.
		/// </summary>
		/// <param name="dxdRange">The new horizontal scroll range</param>
		/// <param name="dxdPos">The new horizontal scroll position</param>
		/// <param name="dydRange">The new vertical scroll range</param>
		/// <param name="dydPos">The new vertical scroll position</param>
		/// <returns><c>true</c> if the scroll position had to be changed because it ended up
		/// below the scroll range.</returns>
		protected bool UpdateScrollRange(int dxdRange, int dxdPos, int dydRange, int dydPos)
		{
			var fRet = false;
			var dydWindHeight = ClientHeight;
			var dxdWindWidth = ClientRectangle.Width;
			if (WantVScroll)
			{
				// If it is now too big, adjust it. Also, this means we must be in the
				// middle of a draw that is failing, so invalidate and set the return flag.
				if (dydPos > Math.Max(dydRange - dydWindHeight, 0))
				{
					dydPos = Math.Max(dydRange - dydWindHeight, 0);
					InvalidateForLazyFix();
					fRet = true;
				}
				// It is also possible that we've made it too small. This can happen if
				// expanding a lazy box reduces the real scroll range to zero.
				if (dydPos < 0)
				{
					dydPos = 0;
					InvalidateForLazyFix();
					fRet = true;
				}
				var fOldSizeChangedSuppression = SizeChangedSuppression;
				var fOldInLayout = LayoutInProgress;
				try
				{
					// If setting the scroll range causes OnSizeChanged, which it does
					// in root site slaves, do NOT try to scroll to make the selection visible.
					SizeChangedSuppression = true;
					// It's also important to suppress our internal Layout code while we do this.
					// For some obscure reason, setting ScrollMinSize (at least) triggers a layout
					// event. This can be disastrous if we actually do a layout on the root box.
					// UpdateScrollRange can occur during expansion of lazy boxes, which in turn can
					// occur during loops where the current item might be a string box.
					// A Relayout at that point can change the state of the current box we are processing.
					// In any case it is a waste: we change the scroll position and range as a result of
					// laying out our contents; changing the scroll range and position do not affect
					// our contents.
					LayoutInProgress = true;
					// Make the actual adjustment. Note we need to reset the page, because
					// Windows does not allow nPage to be more than the scroll range, so if we
					// ever (even temporarily) compute a smaller scroll range, something has to
					// set the page size back to its proper value of the window height.

					// In order to keep the scrollbar from bottoming out we must set the
					// size and the position in the correct order.  If the new position
					// is greater than or equal to the old, we must increase the size first.
					// If the new position is less than the old, we must set the position first.
					// (Using greater than instead of greater than or equal causes LT-4990/5164.)
					if (dydPos >= -ScrollPosition.Y)
					{
						ScrollMinSize = new Size(AutoScrollMinSize.Width, dydRange);
						ScrollPosition = new Point(-ScrollPosition.X, dydPos);
					}
					else
					{
						ScrollPosition = new Point(-ScrollPosition.X, dydPos);
						ScrollMinSize = new Size(AutoScrollMinSize.Width, dydRange);
					}
				}
				finally
				{
					// We have to remember the old value because setting the autoscrollminsize
					// in a root site slave can produce a recursive call as our own size is changed.
					SizeChangedSuppression = fOldSizeChangedSuppression;
					LayoutInProgress = fOldInLayout;
				}
			}

			if (WantHScroll)
			{
				// Similarly for horizontal scroll bar.
				if (dxdPos > Math.Max(dxdRange - dxdWindWidth, 0))
				{
					dxdPos = Math.Max(dxdRange - dxdWindWidth, 0);
					InvalidateForLazyFix();
					fRet = true;
				}
				// It is also possible that we've made it too small. This can happen if
				// expanding a lazy box reduces the real scroll range to zero.
				if (dxdPos < 0)
				{
					dxdPos = 0;
					InvalidateForLazyFix();
					fRet = true;
				}

				if (dxdPos >= -ScrollPosition.X)
				{
					ScrollMinSize = new Size(dxdRange, (WantVScroll ? dydRange : 0));
					ScrollPosition = new Point(dxdPos, dydPos);
				}
				else
				{
					ScrollPosition = new Point(dxdPos, dydPos);
					ScrollMinSize = new Size(dxdRange, (WantVScroll ? dydRange : 0));
				}
			}

			return fRet;
		}

		/// <summary>
		/// Lay out your root box. If nothing significant has changed since the last layout, answer
		/// false; if it has, return true. Assumes that m_graphicsManager.VwGraphics is in a
		/// valid state (having a DC).
		/// </summary>
		/// <remarks>We assume that the VwGraphics object has already been setup for us
		/// </remarks>
		/// <returns>true if significant change since last layout, otherwise false</returns>
		protected virtual bool DoLayout()
		{
			if (DesignMode && !AllowPaintingInDesigner)
			{
				return false;
			}
			Debug.Assert(m_graphicsManager.VwGraphics != null);

			if (RootBox == null || Height == 0)
			{
				// Make sure we don't think we have a scroll range if we have no data!
				UpdateScrollRange();
				return false; // Nothing to do.
			}
			var dxdAvailWidth = GetAvailWidth(RootBox);
			if (dxdAvailWidth != m_dxdLayoutWidth)
			{
				m_dxdLayoutWidth = dxdAvailWidth;
				if (!OkayToLayOut)
				{
					// No drawing until we get reasonable size.
					if (!OkayToLayOutAtCurrentWidth)
					{
						m_dxdLayoutWidth = kForceLayout; // REVIEW (TomB): Not really sure why we have to do this.
					}
					return true;
				}
				SetupVc();

				LayoutInProgress = true;
				try
				{
					using (new SuspendDrawing(this))
					{
						RootBox.Layout(m_graphicsManager.VwGraphics, dxdAvailWidth);
					}
					MoveChildWindows();
				}
				catch (ExternalException objException)
				{
					// In one case, a user in India got this failure whenever starting Flex. He was
					// using a Tibetan font, but after reformatting his drive and reinstalling FW
					// he had failed to enable complex script support in Windows.
					m_dxdLayoutWidth = kForceLayout; // No drawing until we get successful layout.
					throw new Exception("Views Layout failed", objException);
				}
				finally
				{
					LayoutInProgress = false;
				}
			}

			// Update the scroll range anyway, because height may have changed.
			UpdateScrollRange();

			return true;
		}

		/// <summary>
		/// This is called in Layout to notify the VC of anything it needs to know.
		/// </summary>
		private void SetupVc()
		{
			if (Publisher == null)
			{
				return;
			}
			int hvo, frag;
			IVwViewConstructor vc;
			IVwStylesheet ss;
			RootBox.GetRootObject(out hvo, out vc, out frag, out ss);
			if (vc is VwBaseVc)
			{
				// This really only needs to be done once but I can't find another reliable way to do it.
				((VwBaseVc)vc).Publisher = Publisher;
			}
		}

		/// <summary>
		/// This hook provides an opportunity for subclasses to move child windows.
		/// For example, InterlinDocChild moves its Sandbox window to correspond
		/// to the position of the selected word in the text. It may be important to
		/// do this before updating the scroll range...for example, Windows forms may
		/// not allow the scroll range to be less than enough to show the whole of
		/// the (old position of) the child window.
		/// </summary>
		protected virtual void MoveChildWindows()
		{
		}

		/// <summary>
		/// This function returns a selection that includes the entire LinkedFile link at the current
		/// insertion point. It doesn't actually make the selection active.
		/// If the return value is false, none of the paramters should be looked at.
		/// If pfFoundLinkStyle is true, ppvwsel will contain the entire LinkedFiles Link string and
		/// pbstrFile will contain the filename the LinkedFiles link is pointing to.
		/// If pfFoundLinkStyle is false, the selection will still be valid, but it couldn't find
		/// any LinkedFiles link at the current insertion point.
		/// If ppt is not NULL, it will look for an LinkedFiles Link at that point. Otherwise,
		/// the current insertion point will be used.
		/// </summary>
		protected bool GetExternalLinkSel(out bool fFoundLinkStyle, out IVwSelection vwselParam, out string strbFile, Point pt)
		{
			fFoundLinkStyle = false;
			vwselParam = null;
			strbFile = null;
			if (RootBox == null)
			{
				return false;
			}
			IVwSelection vwsel;
			if (!pt.IsEmpty)
			{
				Rectangle rcSrcRoot;
				Rectangle rcDstRoot;
				using (new HoldGraphics(this))
				{
					GetCoordRects(out rcSrcRoot, out rcDstRoot);
				}
				vwsel = RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, false);
			}
			else
			{
				vwsel = RootBox.Selection;
			}
			if (vwsel == null)
			{
				return false;
			}
			ITsString tss;
			int ich;
			bool fAssocPrev;
			int hvoObj;
			int propTag;
			int ws;
			TsRunInfo tri;
			ITsTextProps ttp;
			vwsel.TextSelInfo(false, out tss, out ich, out fAssocPrev, out hvoObj, out propTag, out ws);
			// The following test is covering a bug in TextSelInfo until JohnT fixes it. If you right+click
			// to the right of a tags field (with one tag), TextSelInfo returns a qtss with a null
			// string and returns ich = length of the entire string. As a result get_RunAt fails
			// below because it is asking for a run at a non-existent location.
			if (tss == null)
			{
				return false;
			}
			var cch = tss.Length;
			if (ich >= cch)
			{
				return false; // No string to check.
			}
			string sbstr;
			var sbstrMain = string.Empty;
			var crun = tss.RunCount;
			var irun = tss.get_RunAt(ich);
			int irunMin;
			int irunLim;
			for (irunMin = irun; irunMin >= 0; irunMin--)
			{
				ttp = tss.FetchRunInfo(irunMin, out tri);
				sbstr = ttp.GetStrPropValue((int)FwTextPropType.ktptObjData);
				if (sbstr.Length == 0 || sbstr[0] != (byte)FwObjDataTypes.kodtExternalPathName)
				{
					break;
				}
				if (!fFoundLinkStyle)
				{
					fFoundLinkStyle = true;
					sbstrMain = sbstr;
				}
				else if (!sbstr.Equals(sbstrMain))
				{
					// This LinkedFiles Link is different from the other one, so
					// we've found the beginning.
					break;
				}
			}
			irunMin++;
			// If fFoundLinkStyle is true, irunMin now points to the first run
			// that has the LinkedFiles Link style.
			// If fFoundLinkStyle is false, there's no point in looking at
			// following runs.
			if (!fFoundLinkStyle)
			{
				return true;
			}
			for (irunLim = irun + 1; irunLim < crun; irunLim++)
			{
				ttp = tss.FetchRunInfo(irunLim, out tri);
				sbstr = ttp.GetStrPropValue((int)FwTextPropType.ktptObjData);
				if (sbstr.Length == 0 || sbstr[0] != (byte)FwObjDataTypes.kodtExternalPathName)
				{
					break;
				}
				if (!sbstr.Equals(sbstrMain))
				{
					// This LinkedFiles Link is different from the other one, so
					// we've found the ending.
					break;
				}
			}

			// We can now calculate the character range of this TsString that has
			// the LinkedFiles Link style applied to it.
			var ichMin = tss.get_MinOfRun(irunMin);
			var ichLim = tss.get_LimOfRun(irunLim - 1);

			MessageBox.Show("This code needs work! I don't think that it works like it should!");

			var cvsli = vwsel.CLevels(false);
			cvsli--; // CLevels includes the string property itself, but AllTextSelInfo doesn't need it.

			int ihvoRoot;
			int tagTextProp;
			int cpropPrevious;
			int ichAnchor;
			int ichEnd;
			int ihvoEnd;
			var rgvsli = SelLevInfo.AllTextSelInfo(vwsel, cvsli, out ihvoRoot, out tagTextProp, out cpropPrevious, out ichAnchor,
				out ichEnd, out ws, out fAssocPrev, out ihvoEnd, out ttp);

			// This does not actually make the selection active.
			RootBox.MakeTextSelection(ihvoRoot, cvsli, rgvsli, tagTextProp, cpropPrevious, ichMin, ichLim, ws, fAssocPrev, ihvoEnd, ttp, false);

			strbFile = sbstrMain.Substring(1);
			return true;
		}

		/// <summary />
		protected virtual void Activate(VwSelectionState vss)
		{
			if (RootBox != null && AllowDisplaySelection)
			{
				RootBox.Activate(vss);
			}
		}

		/// <summary>
		/// allows Activate(VwSelectionState) to activate the selection.
		/// By default, we don't do this when ReadOnlyView is set to true.
		/// So, ReadOnlyView subclasses should override when they want Selections to be displayed.
		/// </summary>
		protected virtual bool AllowDisplaySelection => IsEditable;

		/// <summary>
		/// Adjust a point to view coords from device coords. This is the translation from a
		/// point obtained from a windows message like WM_LBUTTONDOWN to a point that can be
		/// passed to the root box. Currently it does nothing, as any conversion is handled
		/// by the source and destination rectangles passed to the mouse routines. It is
		/// retained for possible future use.
		/// </summary>
		protected Point PixelToView(Point pt)
		{
			return m_orientationManager.RotatePointPaintToDst(pt);
		}

		/// <summary>
		/// An error has occurred during drawing, and the component in which it occurred should
		/// have recorded a system error information object describing the problem
		/// </summary>
		public static void ReportDrawErrMsg(Exception e)
		{
			if (e == null)
			{
				return;
			}
			// Look through the list to see if we've already given this message before.
			foreach (var msg in s_vstrDrawErrMsgs)
			{
				if (msg == e.Message)
				{
					return;
				}
			}

			s_vstrDrawErrMsgs.Add(e.Message);

			throw new ContinuableErrorException("Drawing Error", e);
		}

		/// <summary>
		/// Add or remove the tag in the given overlay of the current string.
		/// </summary>
		/// <param name="fApplyTag">True to add the tag, false to remove it</param>
		/// <param name="pvo">Overlay</param>
		/// <param name="itag">Index of tag</param>
		public void ModifyOverlay(bool fApplyTag, IVwOverlay pvo, int itag)
		{
			if (RootBox == null)
			{
				return;
			}
			Debug.WriteLine("WARNING: RootSite.ModifyOverlay() isn't tested yet");
			string uid;
			using (var arrayPtr = MarshalEx.StringToNative((int)VwConst1.kcchGuidRepLength + 1, true))
			{
				int hvo;
				uint clrFore;
				uint clrBack;
				uint clrUnder;
				int unt;
				bool fHidden;
				pvo.GetDbTagInfo(itag, out hvo, out clrFore, out clrBack, out clrUnder, out unt, out fHidden, arrayPtr);
				uid = MarshalEx.NativeToString(arrayPtr, (int)VwConst1.kcchGuidRepLength, false);
			}
			IVwSelection vwsel;
			ITsTextProps[] vttp;
			IVwPropertyStore[] vvps;
			if (EditingHelper.GetCharacterProps(out vwsel, out vttp, out vvps))
			{
				var cttp = vttp.Length;
				for (var ittp = 0; ittp < cttp; ittp++)
				{
					var strGuid = vttp[ittp].GetStrPropValue((int)FwTextPropType.ktptTags);
					// REVIEW (EberhardB): I'm not sure if this works
					var cGuids = strGuid.Length / Marshal.SizeOf(typeof(Guid));
					var guids = new List<string>();
					for (var i = 0; i < cGuids; i++)
					{
						guids.Add(strGuid.Substring(i, Marshal.SizeOf(typeof(Guid))));
					}
					if (fApplyTag)
					{
						// Add the tag if it does not exist
						if (guids.BinarySearch(uid) >= 0)
						{
							// The tag has already been applied to the textprop, so it doesn't
							// need to be modified.
							vttp[ittp] = null;
							continue;
						}
						// We need to add the tag to the textprop.
						guids.Add(uid);
						guids.Sort();
					}
					else
					{
						// Remove the tag from the textprop.
						guids.Remove(uid);
					}
					var tpb = vttp[ittp].GetBldr();
					tpb.SetStrPropValue((int)FwTextPropType.ktptTags, guids.ToString());
					vttp[ittp] = tpb.GetTextProps();
				}
				vwsel.SetSelectionProps(cttp, vttp);
			}
			if (FindForm() == Form.ActiveForm)
			{
				Focus();
			}
		}

		/// <summary>
		/// Checks if selection is visible. For a range selection we check the end of the
		/// selection, for an IP the entire selection must be visible.
		/// </summary>
		/// <remarks>
		/// This version doesn't test the secondary part of the selection if the combined
		/// primary and secondary selection is higher or wider then the ClientRectangle.
		/// </remarks>
		public bool IsSelectionVisible(IVwSelection sel)
		{
			return IsSelectionVisible(sel, false);
		}

		/// <summary>
		/// Checks if selection is visible. For a range selection (that is not a picture) we check the end of the
		/// selection, otherwise the entire selection must be visible.
		/// </summary>
		public bool IsSelectionVisible(IVwSelection sel, bool fWantOneLineSpace)
		{
			if (RootBox == null)
			{
				return false; // For paranoia.
			}
			var vwsel = sel ?? SelectionToMakeVisible;
			return vwsel != null && IsSelectionVisible(vwsel, fWantOneLineSpace, DefaultWantBothEnds(vwsel));
		}

		private static bool DefaultWantBothEnds(IVwSelection vwsel)
		{
			return vwsel.SelType == VwSelType.kstPicture || !vwsel.IsRange;
		}

		/// <summary>
		/// Checks if selection is visible, according to the parameters.
		/// </summary>
		/// <remarks>
		/// <para>This version doesn't test the secondary part of the selection if the combined
		/// primary and secondary selection is higher or wider then the ClientRectangle.</para>
		/// <para>This method tests that the selection rectangle is inside of the
		/// client rectangle, but it doesn't test if the selection is actually enabled!</para>
		/// </remarks>
		/// <param name="vwsel">The selection</param>
		/// <param name="fWantOneLineSpace">True if we want at least 1 extra line above or
		/// below the selection to be considered visible, false otherwise</param>
		/// <param name="fWantBothEnds">true if both ends must be visible; otherwise, it's
		/// good enough if the end is.</param>
		/// <returns>Returns true if selection is visible</returns>
		public bool IsSelectionVisible(IVwSelection vwsel, bool fWantOneLineSpace, bool fWantBothEnds)
		{
			Rectangle rcPrimary;
			bool fEndBeforeAnchor;
			int ydTop;
			// make sure we have a m_graphicsManager.VwGraphics
			using (new HoldGraphics(this))
			{
				SelectionRectangle(vwsel, out rcPrimary, out fEndBeforeAnchor);
			}
			if (IsVertical)
			{
				// In all current vertical views we have no vertical scrolling, so only need
				// to consider horizontal.
				// In this case, rcPrimary's top is the distance from the right of the ClientRect to the
				// right of the selection, and the height of rcPrimary is a distance further left.
				var right = rcPrimary.Top; // distance to left of right of window
				var left = right + rcPrimary.Height;
				if (fWantOneLineSpace)
				{
					right -= LineHeight;
					left += LineHeight;
				}
				return right >= 0 && left <= ClientRectangle.Width;
			}
			// Where the window thinks it is now.
			ydTop = -ScrollPosition.Y;
			// Adjust for that and also the height of the (optional) header.
			rcPrimary.Offset(0, ydTop - m_dyHeader); // Was in drawing coords, adjusted by top.
			// OK, we want rcIdealPrimary to be visible.
			var ydBottom = ydTop + ClientHeight - m_dyHeader;
			if (fWantOneLineSpace)
			{
				ydTop += LineHeight;
				ydBottom -= LineHeight;
			}
			var isVisible = false;
			// Does the selection rectangle overlap the screen one?
			// Note that for insertion points and pictures we want the selection to be
			// entirely visible.
			// Note that if we support horizontal scrolling we will need to enhance this.
			if (fWantBothEnds)
			{
				isVisible = rcPrimary.Top >= ydTop && rcPrimary.Bottom <= ydBottom;
			}
			else
			{
				isVisible = fEndBeforeAnchor
					? rcPrimary.Top > ydTop && rcPrimary.Top < ydBottom - LineHeight
					: rcPrimary.Bottom > ydTop + LineHeight && rcPrimary.Bottom < ydBottom;
			}
			// If not visible vertically, we don't care about horizontally.
			if (!isVisible)
			{
				return false;
			}
			// If not scrolling horizontally, vertically is good enough.
			if (!DoAutoHScroll)
			{
				return isVisible;
			}
			var ydLeft = -ScrollPosition.X + HorizMargin;
			var ydRight = ydLeft + ClientRectangle.Width - (HorizMargin * 2);
			return rcPrimary.Left >= ydLeft && rcPrimary.Right <= ydRight;
		}

		/// <summary>
		/// Make sure the graphics object has a DC. If it already has, increment a count,
		/// so we know when to really free the DC.
		/// </summary>
		internal void InitGraphics()
		{
			// EberhardB: we used to check for HandleCreated, but if we do this it is
			// impossible to run tests without showing a window. Not checking the creation
			// of the handle seems to work in both test and production.
			// DavidO: We check for IsDisposed because InitGraphics is called when
			// SimpleRootSite receives an OnKeyDown event. However, some OnKeyDown events in
			// the delegate chain may close the window on which a SimpleRootSite has been
			// placed. In such cases, InitGraphics was being called after the SimpleRootSite's
			// handle was disposed of, thus causing a crash when CreateGraphics() was called.
			// I (RandyR) added a call to DestroyHandle() very early on in the Dispose method,
			// so that should take care of re-entrant events.
			if (DesignMode && !AllowPaintingInDesigner)
			{
				return;
			}
			lock (m_graphicsManager)
			{
				m_graphicsManager.Init(Zoom);
				Dpi = new Point(m_graphicsManager.VwGraphics.XUnitsPerInch, m_graphicsManager.VwGraphics.YUnitsPerInch);
			}
		}

		/// <summary>
		/// Uninitialize the graphics object by releasing the DC.
		/// </summary>
		internal void UninitGraphics()
		{
			lock (m_graphicsManager)
			{
				m_graphicsManager.Uninit();
			}
		}
		#endregion // Other non-virtual methods

		#region static methods

		/// <summary>
		/// Performs the same coordinate transformation as C++ UtilRect rcSrc.MapXTo(x, rcDst).
		/// </summary>
		public static int MapXTo(int x, Rectangle rcSrc, Rectangle rcDst)
		{
			var dxs = rcSrc.Width;
			Debug.Assert(dxs > 0);
			var dxd = rcDst.Width;
			return dxs == dxd ? x + rcDst.Left - rcSrc.Left : rcDst.Left + (x - rcSrc.Left) * dxd / dxs;
		}

		/// <summary>
		/// Performs the same coordinate transformation as C++ UtilRect rcSrc.MapYTo(y, rcDst).
		/// </summary>
		public static int MapYTo(int y, Rectangle rcSrc, Rectangle rcDst)
		{
			var dys = rcSrc.Height;
			Debug.Assert(dys > 0);
			var dyd = rcDst.Height;
			return dys == dyd ? y + rcDst.Top - rcSrc.Top : rcDst.Top + (y - rcSrc.Top) * dyd / dys;
		}

		/// <summary>
		/// Get the writing system that is most probably intended by the user, when the input
		/// method changes to the specified <paramref name="inputMethod"/>, given the indicated
		/// candidates, and that <paramref name="wsCurrent"/> is the preferred result if it is
		/// a possible WS for the specified input method. wsCurrent is also returned if none
		/// of the <paramref name="candidates"/> is found to match the specified inputs.
		/// </summary>
		/// <param name="inputMethod">The input method or keyboard</param>
		/// <param name="wsCurrent">The writing system that is currently active in the form.
		/// This serves as a default that will be returned if no writing system can be
		/// determined from the first argument. It may be null. Also, if there is more than
		/// one equally promising match in candidates, and wsCurrent is one of them, it will
		/// be preferred. This ensures that we don't change WS on the user unless the keyboard
		/// they have selected definitely indicates a different WS.</param>
		/// <param name="candidates">The writing systems that should be considered as possible
		/// return values.</param>
		/// <returns>The best writing system for <paramref name="inputMethod"/>.</returns>
		/// <remarks>This method replaces IWritingSystemRepository.GetWsForInputLanguage and
		/// should preferably be used.</remarks>
		internal static CoreWritingSystemDefinition GetWSForInputMethod(IKeyboardDefinition inputMethod, CoreWritingSystemDefinition wsCurrent, CoreWritingSystemDefinition[] candidates)
		{
			if (inputMethod == null)
			{
				throw new ArgumentNullException(nameof(inputMethod));
			}
			// See if the default is suitable.
			return wsCurrent != null && inputMethod.Equals(wsCurrent.LocalKeyboard) ? wsCurrent
				: candidates.FirstOrDefault(ws => inputMethod.Equals(ws.LocalKeyboard)) ?? wsCurrent;
		}

		#endregion

		#region implementation of IReceiveSequentialMessages

		/// <summary />
		public MessageSequencer Sequencer { get; private set; }

		//		LRESULT LresultFromObject(
		//			REFIID riid,
		//			WPARAM wParam,
		//			LPUNKNOWN pAcc
		//
		/// <summary>
		/// Entry point we are required to call in response to WM_GETOBJECT
		/// </summary>
		[DllImport("oleacc.DLL", EntryPoint = "LresultFromObject", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern IntPtr LresultFromObject(ref Guid riid, IntPtr wParam, [MarshalAs(UnmanagedType.Interface)] object pAcc);

		/// <summary>
		/// Processes Windows messages.
		/// </summary>
		/// <param name="msg">The Windows Message to process.</param>
		public virtual void OriginalWndProc(ref Message msg)
		{
			switch (msg.Msg)
			{
				case 61: // WM_GETOBJECT
					if (!Platform.IsMono)
					{
						var obj = AccessibleRootObject;
						if (obj == null)
						{
							// If for some reason the root site isn't sufficiently initialized
							// to have a root box, the best we can do is the default IAccessible.
							//MessageBox.Show("Null root in IAccessible");
							base.WndProc(ref msg);
						}
						else
						{
							var guidAcc = Marshal.GenerateGuidForType(typeof(IAccessible));
							msg.Result = LresultFromObject(ref guidAcc, msg.WParam, obj);
						}
						return;
					}
					break;
				case 0x286: // WM_IME_CHAR
					{
						// We must handle this directly so that duplicate WM_CHAR messages don't get
						// posted, resulting in duplicated input.  (I suspect this may be a bug in the
						// .NET framework. - SMc)
						OnKeyPress(new KeyPressEventArgs((char)msg.WParam));
						return;
					}
				case (int)Win32.WinMsgs.WM_SETFOCUS:
					OnSetFocus(msg);
					if (Platform.IsMono)
					{
						// In Linux+Mono, if you .Focus() a SimpleRootSite, checking .Focused reports false unless
						// we comment out this case for intercepting WM_SETFOCUS, or call base.WndProc() to
						// presumably let Mono handle WM_SETFOCUS as well by successfully setting focus on the
						// base Control.
						// Affects six unit tests in FwCoreDlgsTests FwFindReplaceDlgTests: eg ApplyStyle_ToEmptyTextBox.
						//
						// Intercepting WM_SETFOCUS in Windows relates to focus switching with respect to Keyman.
						base.WndProc(ref msg);
					}
					return;
				case (int)Win32.WinMsgs.WM_KILLFOCUS:
					base.WndProc(ref msg);
					OnKillFocus(Control.FromHandle(msg.WParam), FwUtils.FwUtils.IsChildWindowOfForm(ParentForm, msg.WParam));
					return;
			}
			base.WndProc(ref msg);
		}

		/// <summary>
		/// Required by interface, but not used, because we don't user the MessageSequencer
		/// to sequence OnPaint calls.
		/// </summary>
		public void OriginalOnPaint(PaintEventArgs e)
		{
			Debug.Assert(false);
		}

		#endregion

		#region Sequential message processing enforcement

		private Form ContainingWindow()
		{
			for (var parent = Parent; parent != null; parent = parent.Parent)
			{
#if RANDYTODO
			// TODO: parent is really IFwMainWnd, but as of this writing, that interface
			// TODO: isn't available in this assembly, so use Form until
			// TODO: a better solution for IFwMainWnd is found and caller can use the interface directly.
#endif
				if (parent is Form)
				{
					return parent as Form;
				}
			}
			return null;
		}

		/// <summary>
		/// Begin a block of code which, even though it is not itself a message handler,
		/// should not be interrupted by other messages that need to be sequential.
		/// This may be called from within a message handler.
		/// EndSequentialBlock must be called without fail (use try...finally) at the end
		/// of the block that needs protection.
		/// </summary>
		public void BeginSequentialBlock()
		{
#if RANDYTODO
			// TODO: mainWindow is really IFwMainWnd, but as of this writing, that interface
			// TODO: isn't available in this assembly, so use Reflection until
			// TODO: a better solution for IFwMainWnd is found.
#endif
			var mainWindow = ContainingWindow();
			if (mainWindow != null)
			{
#if RANDYTODO
				// TODO: Use this when the IFwMainWnd can be used here.
				mainWindow.SuspendIdleProcessing();
#else
				// TODO: mainWindow is really IFwMainWnd, but as of this writing, that interface
				// TODO: isn't available in this assembly, so use Reflection until
				// TODO: a better solution for IFwMainWnd is found.
				var wndType = mainWindow.GetType();
				var mi = wndType.GetMethod("SuspendIdleProcessing");
				mi.Invoke(mainWindow, BindingFlags.InvokeMethod, null, null, null);
#endif
			}
			Sequencer.BeginSequentialBlock();
		}

		/// <summary>
		/// See BeginSequentialBlock.
		/// </summary>
		public void EndSequentialBlock()
		{
			Sequencer.EndSequentialBlock();
#if RANDYTODO
			// TODO: mainWindow is really IFwMainWnd, but as of this writing, that interface
			// TODO: isn't available in this assembly, so use Reflection until
			// TODO: a better solution for IFwMainWnd is found.
#endif
			var mainWindow = ContainingWindow();
			if (mainWindow != null)
			{
#if RANDYTODO
				// TODO: Use this when the IFwMainWnd can be used here.
				mainWindow.ResumeIdleProcessing();
#else
				// TODO: mainWindow is really IFwMainWnd, but as of this writing, that interface
				// TODO: isn't available in this assembly, so use Reflection until
				// TODO: a better solution for IFwMainWnd is found.
				var wndType = mainWindow.GetType();
				var mi = wndType.GetMethod("ResumeIdleProcessing");
				mi.Invoke(mainWindow, BindingFlags.InvokeMethod, null, null, null);
#endif
			}
		}
		#endregion

		#region IMessageFilter Members

		/// <summary>
		/// This is a major kludge to prevent a spurious WM_KEYUP for VK_CONTROL from
		/// interrupting a mouse click.
		/// If mouse button was pressed elsewhere causing this root site to loose focus,
		/// this message filter is installed to throw away the spurious WM_KEYUP for VK_CONTROL
		/// that happens as a result of switching to the default keyboard layout.
		/// See OnKillFocus for the corresponding code that installs this message filter.
		/// </summary>
		/// <param name="m">The message to be dispatched. You cannot modify this message.</param>
		/// <returns>
		/// true to filter the message and stop it from being dispatched; false to allow the
		/// message to continue to the next filter or control.
		/// </returns>
		public bool PreFilterMessage(ref Message m)
		{
			switch (m.Msg)
			{
				case (int)Win32.WinMsgs.WM_KEYUP:
				case (int)Win32.WinMsgs.WM_LBUTTONUP:
				case (int)Win32.WinMsgs.WM_KEYDOWN:
					// If user-initiated messages come (or our spurious one, which we check
					// for below), remove this filter.
					Application.RemoveMessageFilter(this);
					m_messageFilterInstalled = false;

					// Now check for the spurious CTRL-UP message
					if (m.Msg == (int)Win32.WinMsgs.WM_KEYUP &&
						m.WParam.ToInt32() == (int)Win32.VirtualKeycodes.VK_CONTROL)
					{
						return true; // discard this message
					}
					break;
			}
			return false; // Let the message be dispatched
		}
		#endregion

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public virtual void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;

			Subscriber.Subscribe(FwUtils.FwUtils.AboutToFollowLink, AboutToFollowLink);
			Subscriber.Subscribe(FwUtils.FwUtils.WritingSystemHvo, WritingSystemHvo_Changed);

			if (!SuppressPrintHandling)
			{
				// Get the "Print" menu from the window's "File" menu, and wire up the Print_Click handler and enable the menu.
				var mainWindow = PropertyTable.GetValue<Form>(FwUtils.FwUtils.window);
				// Many tests have no window.
				if (mainWindow != null)
				{
					// Have to do it the hard way.
					var windowType = mainWindow.GetType();
					var mi = windowType.GetMethod("GetMainMenu", new[] { typeof(string) });
					var fileMenu = (ToolStripMenuItem)mi.Invoke(mainWindow, new object[] { "_fileToolStripMenuItem" });
					m_printMenu = (ToolStripMenuItem)fileMenu.DropDownItems["CmdPrint"];
					m_printMenu.Click += Print_Click;
					m_printMenu.Enabled = false;
				}
			}
		}

		#endregion

		private void AboutToFollowLink(object newValue)
		{
			IsFollowLinkMsgPending = true;
		}

		/// <summary>
		/// Handle "WritingSystemHvo" message.
		/// </summary>
		private void WritingSystemHvo_Changed(object newValue)
		{
			ReallyHandleWritingSystemHvo_Changed(newValue);
		}

		/// <summary>
		/// Really handle it in a way subclasses can get involved.
		/// </summary>
		protected virtual void ReallyHandleWritingSystemHvo_Changed(object newValue)
		{
			EditingHelper.WritingSystemHvoChanged(int.Parse((string)newValue));
		}

		/// <summary>
		/// Really handle it in a way subclasses can get involved.
		/// </summary>
		public virtual string Style_Changed(BaseStyleInfo newValue)
		{
			return EditingHelper.BestStyleNameChanged(newValue);
		}

		// NOTE: we implement the IWIndowsLanguageProfileSink interface in a private class
		// so that we don't introduce an otherwise unnecessary dependency on
		// PalasoUIWindowsForms which would require to add a reference to all projects that
		// use a SimpleRootSite.
		private sealed class WindowsLanguageProfileSink : IWindowsLanguageProfileSink
		{
			private SimpleRootSite Parent { get; set; }

			public WindowsLanguageProfileSink(SimpleRootSite parent)
			{
				Parent = parent;
			}

			/// <summary>
			/// Called after the language profile has changed.
			/// </summary>
			/// <param name="previousKeyboard">The previous input method</param>
			/// <param name="newKeyboard">The new input method</param>
			public void OnInputLanguageChanged(IKeyboardDefinition previousKeyboard, IKeyboardDefinition newKeyboard)
			{
				if (Parent.IsDisposed || Parent.RootBox == null || DataUpdateMonitor.IsUpdateInProgress())
				{
					return;
				}
				var manager = Parent.WritingSystemFactory as WritingSystemManager;
				if (manager == null)
				{
					return;
				}
				// JT: apparently this comes to all the views, but only the active keyboard
				// needs to handle it.
				// SMc: furthermore, this is not really focused until OnGotFocus() has run.
				// Responding before that causes a nasty bug in language/keyboard selection.
				if (!Parent.Focused || g_focusRootSite.Target != Parent)
				{
					return;
				}
				// If possible, adjust the language of the selection to be one that matches
				// the keyboard just selected.
				var vwsel = Parent.RootBox.Selection; // may be null
				var wsSel = SelectionHelper.GetFirstWsOfSelection(vwsel); // may be zero
				CoreWritingSystemDefinition wsSelDefn = null;
				if (wsSel != 0)
				{
					wsSelDefn = manager.Get(wsSel);
				}
				var wsNewDefn = GetWSForInputMethod(newKeyboard, wsSelDefn, Parent.PlausibleWritingSystems);
				if (wsNewDefn == null || wsNewDefn.Equals(wsSelDefn))
				{
					return;
				}
				Parent.HandleKeyboardChange(vwsel, wsNewDefn.Handle);
				// The following line is needed to get Chinese IMEs to fully initialize.
				// This causes Text Services to set its focus, which is the crucial bit
				// of behavior.  See LT-7488 and LT-5345.
				Parent.Activate(VwSelectionState.vssEnabled);
			}
		}

		/// <summary>
		/// Suspends drawing the parent object
		/// </summary>
		/// <example>
		/// REQUIRED usage:
		/// using(new SuspendDrawing(Handle)) // this sends the WM_SETREDRAW message to the window
		/// {
		///		doStuff();
		/// } // this resumes drawing the parent object
		/// </example>
		private sealed class SuspendDrawing : IDisposable
		{
			private IRootSite m_parent;

			/// <summary>
			/// Suspend drawing of the parent.
			/// </summary>
			/// <param name="parent">Containing rootsite</param>
			public SuspendDrawing(IRootSite parent)
			{
				m_parent = parent;
				m_parent.AllowPainting = false;
			}

			/// <summary>
			/// See if the object has been disposed.
			/// </summary>
			private bool IsDisposed { get; set; }

			/// <summary>
			/// Finalizer, in case client doesn't dispose it.
			/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
			/// </summary>
			/// <remarks>
			/// In case some clients forget to dispose it directly.
			/// </remarks>
			~SuspendDrawing()
			{
				Dispose(false);
				// The base class finalizer is called automatically.
			}

			/// <summary>
			///
			/// </summary>
			/// <remarks>Must not be virtual.</remarks>
			public void Dispose()
			{
				Dispose(true);
				// This object will be cleaned up by the Dispose method.
				// Therefore, you should call GC.SuppressFinalize to
				// take this object off the finalization queue
				// and prevent finalization code for this object
				// from executing a second time.
				GC.SuppressFinalize(this);
			}

			/// <summary>
			/// Executes in two distinct scenarios.
			///
			/// 1. If disposing is true, the method has been called directly
			/// or indirectly by a user's code via the Dispose method.
			/// Both managed and unmanaged resources can be disposed.
			///
			/// 2. If disposing is false, the method has been called by the
			/// runtime from inside the finalizer and you should not reference (access)
			/// other managed objects, as they already have been garbage collected.
			/// Only unmanaged resources can be disposed.
			/// </summary>
			/// <param name="disposing"></param>
			/// <remarks>
			/// If any exceptions are thrown, that is fine.
			/// If the method is being done in a finalizer, it will be ignored.
			/// If it is thrown by client code calling Dispose,
			/// it needs to be handled by fixing the bug.
			///
			/// If subclasses override this method, they should call the base implementation.
			/// </remarks>
			private void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + " ******************");
				if (IsDisposed)
				{
					// No need to run it more than once.
					return;
				}

				if (disposing)
				{
					// Dispose managed resources here.
					if (m_parent != null)
					{
						m_parent.AllowPainting = true;
					}
				}

				// Dispose unmanaged resources here, whether disposing is true or false.
				m_parent = null;

				IsDisposed = true;
			}
		}
	}
}