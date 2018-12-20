// Copyright (c) 2006-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Areas.TextsAndWords.Interlinear;
using LanguageExplorer.Controls.DetailControls;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.DomainServices;
using SIL.Xml;
using Rect = SIL.FieldWorks.Common.ViewsInterfaces.Rect;

namespace LanguageExplorer.Areas.TextsAndWords.Tools.Analyses
{
	/// <summary>
	/// This is the main class for the interlinear text control view of one analysis of one wordform.
	/// </summary>
	internal sealed class AnalysisInterlinearRs : RootSite, INotifyControlInCurrentSlice
	{
		private InterlinVc m_vc;
		private IWfiAnalysis m_wfiAnalysis;
		private ISharedEventHandlers _sharedEventHandlers;
		private XElement m_configurationNode;
		private OneAnalysisSandbox m_oneAnalSandbox;
		private Rect m_rcPrimary;
		private bool m_fInSizeChanged;

		private bool IsEditable => XmlUtils.GetBooleanAttributeValue(m_configurationNode.Element("deParams"), "editable");

		#region Construction

		/// <summary>
		/// Make one. Everything interesting happens when it is given a root object, however.
		/// </summary>
		public AnalysisInterlinearRs(ISharedEventHandlers sharedEventHandlers, LcmCache cache, IWfiAnalysis analysis, XElement configurationNode)
			: base(cache)
		{
			Guard.AgainstNull(sharedEventHandlers, nameof(sharedEventHandlers));
			Guard.AgainstNull(analysis, nameof(analysis));
			Guard.AgainstNull(configurationNode, nameof(configurationNode));

			_sharedEventHandlers = sharedEventHandlers;
			m_configurationNode = configurationNode;
			m_wfiAnalysis = analysis;
		}

		#endregion Construction

		#region Dispose

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				if (m_oneAnalSandbox != null)
				{
					m_oneAnalSandbox.SizeChanged -= HandleSandboxSizeChanged;
				}
			}

			base.Dispose(disposing);

			if (disposing)
			{
				m_oneAnalSandbox?.Dispose();
				m_vc?.Dispose();
			}
			m_vc = null;
			m_wfiAnalysis = null;
			_sharedEventHandlers = null;
			m_configurationNode = null;
			m_oneAnalSandbox = null;
		}

		#endregion Dispose

		#region Overrides of RootSite

		/// <summary>
		/// Make the root box.
		/// </summary>
		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode || m_wfiAnalysis == null)
			{
				return;
			}

			base.MakeRoot();

			m_vc = new InterlinVc(m_cache);
			// Theory has it that the slices that have 'true' in this attribute will allow the sandbox to be used.
			// We'll see how the theory goes, when I get to the point of wanting to see the sandbox.
			var isEditable = IsEditable;
			m_vc.ShowMorphBundles = true;
			m_vc.ShowDefaultSense = true;
			if (m_wfiAnalysis.GetAgentOpinion(m_cache.LanguageProject.DefaultParserAgent) == Opinions.approves && m_wfiAnalysis.GetAgentOpinion(m_cache.LanguageProject.DefaultUserAgent) != Opinions.approves)
			{
				m_vc.UsingGuess = true;
			}
			// JohnT: kwsVernInParagraph is rather weird here, where we don't have a paragraph, but it allows the
			// VC to deduce the WS of the wordform, not from the paragraph, but from the best vern WS of the wordform itself.
			m_vc.LineChoices = isEditable ? new EditableInterlinLineChoices(m_cache.LanguageProject, WritingSystemServices.kwsVernInParagraph, m_cache.DefaultAnalWs) : new InterlinLineChoices(m_cache.LanguageProject, WritingSystemServices.kwsVernInParagraph, m_cache.DefaultAnalWs);
			m_vc.LineChoices.Add(InterlinLineChoices.kflidMorphemes); // 1
			m_vc.LineChoices.Add(InterlinLineChoices.kflidLexEntries); //2
			m_vc.LineChoices.Add(InterlinLineChoices.kflidLexGloss); //3
			m_vc.LineChoices.Add(InterlinLineChoices.kflidLexPos); //4

			RootBox.DataAccess = m_cache.MainCacheAccessor;

			const int selectorId = InterlinVc.kfragSingleInterlinearAnalysisWithLabelsLeftAlign;
			RootBox.SetRootObject(m_wfiAnalysis.Hvo, m_vc, selectorId, m_styleSheet);

			if (!IsEditable)
			{
				return;
			}
			m_oneAnalSandbox = new OneAnalysisSandbox(_sharedEventHandlers, m_cache, StyleSheet, m_vc.LineChoices, m_wfiAnalysis.Hvo)
			{
				Visible = false
			};
			m_oneAnalSandbox.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			Controls.Add(m_oneAnalSandbox);
			if (m_oneAnalSandbox.RootBox == null)
			{
				m_oneAnalSandbox.MakeRoot();    // adding sandbox to Controls doesn't make rootbox.
			}
			InitSandbox();
			m_oneAnalSandbox.SizeChanged += (HandleSandboxSizeChanged);
			if (m_fSliceIsCurrent)
			{
				TurnOnSandbox();
			}
		}

		InterlinearSlice MySlice
		{
			get
			{
				var parent = Parent;
				while (parent != null)
				{
					if (parent is InterlinearSlice)
					{
						return parent as InterlinearSlice;
					}
					parent = parent.Parent;
				}
				return null;
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			var slice = MySlice;
			if (slice == null)
			{
				return; // in any case we don't want selections in the interlinear.
			}
			if (slice.ContainingDataTree.CurrentSlice != slice)
			{
				slice.ContainingDataTree.CurrentSlice = slice;
			}
		}

		/// <summary>
		/// Giving it a large maximum width independent of the container causes it to lay out
		/// at full width and scroll horizontally.
		/// </summary>
		public override int GetAvailWidth(IVwRootBox prootb)
		{
			return int.MaxValue / 2;
		}

		#endregion Overrides of RootSite

		#region INotifyControlInCurrentSlice Members

		private bool m_fSliceIsCurrent;

		private bool CanSaveAnalysis()
		{
			var extensions = m_cache.ActionHandlerAccessor as IActionHandlerExtensions;
			// JohnT: it's possible the Sandbox is still visible when we Undo the creation of an
			// analysis. At that point it should have no references to MSAs, since anything done to
			// the new analysis has already been undone. But unless we check, the Undo crashes, because
			// the analysis has already been destroyed by the time the slice stops being current.
			// It's also possible (FWR-3354) that we're in the process of performing an Undo/Redo.
			// In that case we CAN'T save the current analysis (and don't want to).
			// It's further possible (LT-11162) that we are losing focus in the midst of broadcasting
			// PropChange messages, as slices are replaced. In that case we can't save changes and must hope
			// that we already did.
			if (extensions == null)
			{
				if (m_cache.ActionHandlerAccessor.IsUndoOrRedoInProgress) // we can at least check this
				{
					return false;
				}
			}
			else if (!extensions.CanStartUow) // this is the usual and more reliable check.
			{
				return false;
			}
			// if false, we're in some weird state where we can't save changes to this presumably deleted object.
			// Otherwise go ahead and return true.
			return m_wfiAnalysis.IsValidObject;
		}

		/// <summary>
		/// Have the sandbox come and go, as appropriate.
		/// </summary>
		public bool SliceIsCurrent
		{
			set
			{
				m_fSliceIsCurrent = value;
				if (value)
				{
					TurnOnSandbox();
					return;
				}
				if (!IsEditable)
				{
					return;
				}
				SaveChanges();
				m_oneAnalSandbox.Visible = false;
				InitSandbox();
			}
		}

		private void SaveChanges()
		{
			if (IsDisposed)
			{
				throw new InvalidOperationException("Thou shalt not call methods after I am disposed!");
			}
			if (!IsEditable)
			{
				return;
			}
			if (!CanSaveAnalysis())
			{
				return;
			}
			// Collect up the old MSAs, since they need to go away, if they are unused afterwards.
			var msaSet = new HashSet<IMoMorphSynAnalysis>();
			m_wfiAnalysis.CollectReferencedMsas(msaSet);
			m_oneAnalSandbox.UpdateAnalysis(m_wfiAnalysis);
			foreach (var msa in msaSet)
			{
				if (msa != null && msa.CanDelete)
				{
					// TODO: Add UOW? Probably use one for all that are to be deleted (collect them into one list).
					m_cache.MainCacheAccessor.DeleteObj(msa.Hvo);
				}
			}
			Debug.Assert(m_wfiAnalysis.ApprovalStatusIcon == 1, "Analysis must be approved, since it started that way.");
		}

		/// <summary>
		/// This method seems to get called when we are switching to another tool (or area, or slice) AND when the
		/// program is shutting down. This makes it a good point to save our changes.
		/// </summary>
		protected override void OnValidating(CancelEventArgs e)
		{
			base.OnValidating(e);
			SaveChanges();
		}

		private void TurnOnSandbox()
		{
			if (!IsEditable || m_oneAnalSandbox == null)
			{
				return;
			}
			m_oneAnalSandbox.Visible = true;
			m_oneAnalSandbox.Focus();
		}

		public void HandleSandboxSizeChanged(object sender, EventArgs ea)
		{
			SetPadding();
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			if (m_fInSizeChanged)
			{
				return;
			}
			m_fInSizeChanged = true;
			try
			{
				base.OnSizeChanged(e);
				SetSandboxLocation();
			}
			finally
			{
				m_fInSizeChanged = false;
			}
		}

		internal Size DesiredSize
		{
			get
			{
				if (RootBox == null)
				{
					return PreferredSize;
				}
				var desiredWidth = RootBox.Width;
				var desiredHeight = RootBox.Height;
				if (Controls.Contains(m_oneAnalSandbox))
				{
					desiredWidth = Math.Max(desiredWidth, m_oneAnalSandbox.Left + m_oneAnalSandbox.Width);
					desiredHeight = Math.Max(desiredHeight, m_oneAnalSandbox.Top + m_oneAnalSandbox.Height);
				}
				return new Size(desiredWidth + 5, desiredHeight);
			}
		}

		private void InitSandbox()
		{
			SetSandboxSize();
			m_vc.LeftPadding = 0;
			RootBox.Reconstruct();
			using (new HoldGraphics(this))
			{
				Rectangle rcSrcRoot;
				Rectangle rcDstRoot;
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				var rgvsli = new SelLevInfo[1];
				rgvsli[0].ihvo = 0;
				rgvsli[0].tag = m_cache.MetaDataCacheAccessor.GetFieldId2(CmObjectTags.kClassId, "Self", false);
				var sel = RootBox.MakeTextSelInObj(0, rgvsli.Length, rgvsli, 0, null, true, false, false, false, false);
				if (sel == null)
				{
					Debug.WriteLine("Could not make selection in InitSandbox");
					return; // can't position it accurately.
				}
				Rect rcSec;
				bool fSplit, fEndBeforeAnchor;
				sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out m_rcPrimary, out rcSec, out fSplit, out fEndBeforeAnchor);
			}
			SetPadding();
			SetSandboxLocation();
		}

		private void SetSandboxLocation()
		{
			if (m_oneAnalSandbox == null)
			{
				return;
			}
			m_oneAnalSandbox.Left = m_vc.RightToLeft ? 0 : m_rcPrimary.left;
			// This prevents it from overwriting the labels in the pathological case that all
			// morphemes wrap onto another line.
			m_oneAnalSandbox.Top = m_rcPrimary.top;
		}

		private void SetPadding()
		{
			if (m_oneAnalSandbox == null || !m_vc.RightToLeft)
			{
				return;
			}
			int dpiX;
			using (var g = CreateGraphics())
			{
				dpiX = (int)g.DpiX;
			}
			m_vc.LeftPadding = ((m_oneAnalSandbox.Width - m_rcPrimary.right) * 72000) / dpiX;
			RootBox.Reconstruct();
		}

		#endregion

		#region Other methods

		// Set the size of the sandbox on the VC...if it exists yet.
		private void SetSandboxSize()
		{
			SetSandboxSizeForVc();
			// This should make it big enough not to scroll.
			if (m_oneAnalSandbox?.RootBox != null)
			{
				m_oneAnalSandbox.Size = new Size(m_oneAnalSandbox.RootBox.Width + 1, m_oneAnalSandbox.RootBox.Height + 1);
			}
		}

		// Set the VC size to match the sandbox. Return true if it changed.
		private void SetSandboxSizeForVc()
		{
			if (m_vc == null || m_oneAnalSandbox == null)
			{
				return;
			}
			m_oneAnalSandbox.PerformLayout();
		}

		#endregion Other methods
	}
}