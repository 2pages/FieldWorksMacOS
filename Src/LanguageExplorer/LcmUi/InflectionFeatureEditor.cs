// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.XMLViews;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.Xml;

namespace LanguageExplorer.LcmUi
{
	/// <summary>
	/// InflectionFeatureEditor is the spec/display component of the Bulk Edit bar used to
	/// set the Inflection features of a LexSense (which must already have an MoStemMsa with
	/// POS set).
	/// </summary>
	public class InflectionFeatureEditor : IBulkEditSpecControl, IDisposable
	{
		private TreeCombo m_tree;
		private LcmCache m_cache;
		private IPublisher m_publisher;
		private ISubscriber _subscriber;
		protected XMLViewsDataCache m_sda;
		private InflectionFeaturePopupTreeManager m_InflectionFeatureTreeManager;
		private int m_selectedHvo;
		private string m_selectedLabel;
		private int m_displayWs;
		public event EventHandler ControlActivated;
		public event FwSelectionChangedEventHandler ValueChanged;

		private InflectionFeatureEditor()
		{
			m_InflectionFeatureTreeManager = null;
			m_tree = new TreeCombo();
			m_tree.TreeLoad += m_tree_TreeLoad;
			//	Handle AfterSelect event in m_tree_TreeLoad() through m_pOSPopupTreeManager
		}

		public InflectionFeatureEditor(IPublisher publisher, ISubscriber subscriber, XElement configurationNode)
			: this()
		{
			m_publisher = publisher;
			_subscriber = subscriber;
			m_displayWs = WritingSystemServices.GetMagicWsIdFromName(XmlUtils.GetOptionalAttributeValue(configurationNode, "displayWs", "best analorvern"));
		}

		#region IDisposable & Co. implementation

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
		~InflectionFeatureEditor()
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
		protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				// Dispose managed resources here.
				if (m_tree != null)
				{
					m_tree.Load -= m_tree_TreeLoad;
					m_tree.Dispose();
				}
				if (m_InflectionFeatureTreeManager != null)
				{
					m_InflectionFeatureTreeManager.AfterSelect -= m_pOSPopupTreeManager_AfterSelect;
					m_InflectionFeatureTreeManager.Dispose();
				}
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_selectedLabel = null;
			m_tree = null;
			m_InflectionFeatureTreeManager = null;
			m_cache = null;

			IsDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		/// <summary>
		/// Get/Set the property table.
		/// </summary>
		public IPropertyTable PropertyTable { get; set; }

		/// <summary>
		/// Get or set the cache. Must be set before the tree values need to load.
		/// </summary>
		public LcmCache Cache
		{
			get => m_cache;
			set
			{
				m_cache = value;
				if (m_cache != null && m_tree != null)
				{
					m_tree.WritingSystemFactory = m_cache.WritingSystemFactory;
				}
			}
		}

		/// <summary>
		/// The special cache that can handle the preview and check-box properties.
		/// </summary>
		public XMLViewsDataCache DataAccess
		{
			get => m_sda ?? throw new InvalidOperationException("Must set the special cache of a BulkEditSpecControl");
			set => m_sda = value;
		}

		/// <summary>
		/// Get the actual tree control.
		/// </summary>
		public Control Control => m_tree;

		private void m_tree_TreeLoad(object sender, EventArgs e)
		{
			if (m_InflectionFeatureTreeManager == null)
			{
				m_InflectionFeatureTreeManager = new InflectionFeaturePopupTreeManager(m_tree, m_cache, false, new FlexComponentParameters(PropertyTable, m_publisher, _subscriber), PropertyTable.GetValue<Form>(FwUtilsConstants.window), m_displayWs);
				m_InflectionFeatureTreeManager.AfterSelect += m_pOSPopupTreeManager_AfterSelect;
			}
			m_InflectionFeatureTreeManager.LoadPopupTree(0);
		}

		private void m_pOSPopupTreeManager_AfterSelect(object sender, System.Windows.Forms.TreeViewEventArgs e)
		{
			// Todo: user selected a part of speech.
			// Arrange to turn all relevant items blue.
			// Remember which item was selected so we can later 'doit'.
			if (e.Node == null)
			{
				m_selectedHvo = 0;
				m_selectedLabel = string.Empty;
			}
			else
			{
				var hvo = ((HvoTreeNode)e.Node).Hvo;
				var obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvo);
				if (obj is IFsFeatStruc)
				{
					m_selectedHvo = hvo;
					m_selectedLabel = e.Node.Text;
				}
				else
				{
					m_selectedHvo = 0;
					m_selectedLabel = string.Empty;
					m_tree.Text = string.Empty;
				}
			}
			ControlActivated?.Invoke(this, new EventArgs());
			// Tell the parent control that we may have changed the selected item so it can
			// enable or disable the Apply and Preview buttons based on the selection.
			ValueChanged?.Invoke(this, new FwObjectSelectionEventArgs(m_selectedHvo));
		}

		/// <summary>
		/// Required interface member not yet used.
		/// </summary>
		public IVwStylesheet Stylesheet
		{
			set { }
		}

		/// <summary>
		/// Returns the Suggest button if our target is Semantic Domains, otherwise null.
		/// </summary>
		public Button SuggestButton => null;

		/// <summary>
		/// Execute the change requested by the current selection in the combo.
		/// Basically we want a copy of the FsFeatStruc indicated by m_selectedHvo, (even if 0?? not yet possible),
		/// to become the MsFeatures of each record that is appropriate to change.
		/// We do nothing to records where the check box is turned off,
		/// and nothing to ones that currently have an MSA other than an MoStemMsa,
		/// and nothing to ones that currently have an MSA with the wrong POS.
		/// (a) If the owning entry has an MoStemMsa with a matching MsFeatures (and presumably POS),
		/// set the sense to use it.
		/// (b) If all senses using the current MoStemMsa are to be changed, just update
		/// the MsFeatures of that MoStemMsa.
		/// We could add this...but very probably unused MSAs would have been taken over
		/// when setting the POS.
		/// --(c) If the entry has an MoStemMsa which is not being used at all, change it to
		/// --the required POS and inflection class and use it.
		/// (d) Make a new MoStemMsa in the LexEntry with the required POS and features
		/// and point the sense at it.
		/// </summary>
		public void DoIt(IEnumerable<int> itemsToChange, ProgressState state)
		{
			var pos = GetPOS();
			// Make a Set of eligible parts of speech to use in filtering.
			var possiblePOS = GetPossiblePartsOfSpeech();
			// Make a Dictionary from HVO of entry to list of modified senses.
			var sensesByEntry = new Dictionary<int, HashSet<ILexSense>>();
			var i = 0;
			// Report progress 50 times or every 100 items, whichever is more (but no more than once per item!)
			var interval = Math.Min(100, Math.Max(itemsToChange.Count() / 50, 1));
			foreach (var hvoSense in itemsToChange)
			{
				i++;
				if (i % interval == 0)
				{
					state.PercentDone = i * 20 / itemsToChange.Count();
					state.Breath();
				}

				if (!IsItemEligible(m_cache.DomainDataByFlid, hvoSense, possiblePOS))
				{
					continue;
				}
				var ls = m_cache.ServiceLocator.GetInstance<ILexSenseRepository>().GetObject(hvoSense);
				var hvoEntry = ls.EntryID;
				if (!sensesByEntry.ContainsKey(hvoEntry))
				{
					sensesByEntry[hvoEntry] = new HashSet<ILexSense>();
				}
				sensesByEntry[hvoEntry].Add(ls);
			}
			//REVIEW: Should these really be the same Undo/Redo strings as for InflectionClassEditor.cs?
			m_cache.DomainDataByFlid.BeginUndoTask(LcmUiResources.ksUndoBEInflClass, LcmUiResources.ksRedoBEInflClass);
			i = 0;
			interval = Math.Min(100, Math.Max(sensesByEntry.Count / 50, 1));
			IFsFeatStruc fsTarget = null;
			if (m_selectedHvo != 0)
			{
				fsTarget = Cache.ServiceLocator.GetInstance<IFsFeatStrucRepository>().GetObject(m_selectedHvo);
			}
			foreach (var kvp in sensesByEntry)
			{
				i++;
				if (i % interval == 0)
				{
					state.PercentDone = i * 80 / sensesByEntry.Count + 20;
					state.Breath();
				}
				var entry = m_cache.ServiceLocator.GetInstance<ILexEntryRepository>().GetObject(kvp.Key);
				var sensesToChange = kvp.Value;
				IMoStemMsa msmTarget = null;
				foreach (var msa in entry.MorphoSyntaxAnalysesOC)
				{
					if (msa is IMoStemMsa msm && MsaMatchesTarget(msm, fsTarget))
					{
						// Can reuse this one!
						msmTarget = msm;
						break;
					}
				}
				if (msmTarget == null)
				{
					// See if we can reuse an existing MoStemMsa by changing it.
					// This is possible if it is used only by senses in the list, or not used at all.
					var otherSenses = new HashSet<ILexSense>();
					var senses = new HashSet<ILexSense>(entry.AllSenses.ToArray());
					if (senses.Count != sensesToChange.Count)
					{
						foreach (var ls in senses)
						{
							if (!sensesToChange.Contains(ls))
							{
								otherSenses.Add(ls);
							}
						}
					}
					foreach (var msa in entry.MorphoSyntaxAnalysesOC)
					{
						if (!(msa is IMoStemMsa msm))
						{
							continue;
						}
						var fOk = true;
						foreach (var ls in otherSenses)
						{
							if (ls.MorphoSyntaxAnalysisRA == msm)
							{
								fOk = false;
								break;
							}
						}
						if (fOk)
						{
							// Can reuse this one! Nothing we don't want to change uses it.
							// Adjust its POS as well as its inflection feature, just to be sure.
							// Ensure that we don't change the POS!  See LT-6835.
							msmTarget = msm;
							InitMsa(msmTarget, msm.PartOfSpeechRA.Hvo);
							break;
						}
					}
				}
				if (msmTarget == null)
				{
					// Nothing we can reuse...make a new one.
					msmTarget = m_cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
					entry.MorphoSyntaxAnalysesOC.Add(msmTarget);
					InitMsa(msmTarget, pos.Hvo);
				}
				// Finally! Make the senses we want to change use it.
				foreach (var ls in sensesToChange)
				{
					ls.MorphoSyntaxAnalysisRA = msmTarget;
				}
			}
			m_cache.DomainDataByFlid.EndUndoTask();
		}

		/// <summary>
		/// Can't (yet) clear the field value.
		/// </summary>
		public bool CanClearField => false;

		/// <summary>
		/// Not needed since we said we can't do it.
		/// </summary>
		public void SetClearField()
		{
			throw new NotSupportedException();
		}

		private void InitMsa(IMoStemMsa msmTarget, int hvoPos)
		{
			msmTarget.PartOfSpeechRA = m_cache.ServiceLocator.GetObject(hvoPos) as IPartOfSpeech;
			var newFeatures = (IFsFeatStruc)m_cache.ServiceLocator.GetObject(m_selectedHvo);
			msmTarget.CopyMsFeatures(newFeatures);
		}

		/// <summary>
		/// Answer true if the selected MSA has an MsFeatures that is the same as the argument.
		/// </summary>
		private bool MsaMatchesTarget(IMoStemMsa msm, IFsFeatStruc fsTarget)
		{
			if (m_selectedHvo == 0 && msm.MsFeaturesOA == null)
			{
				return true;
			}
			return msm.MsFeaturesOA != null && msm.MsFeaturesOA.IsEquivalent(fsTarget);
		}

		/// <summary>
		/// Add to possiblePOS all the children (recursively) of hvoPos
		/// </summary>
		private static void AddChildPos(ISilDataAccess sda, int hvoPos, HashSet<int> possiblePOS)
		{
			possiblePOS.Add(hvoPos);
			var chvo = sda.get_VecSize(hvoPos, CmPossibilityTags.kflidSubPossibilities);
			for (var i = 0; i < chvo; i++)
			{
				AddChildPos(sda, sda.get_VecItem(hvoPos, CmPossibilityTags.kflidSubPossibilities, i), possiblePOS);
			}
		}

		/// <summary>
		/// Fake doing the change by setting the specified property to the appropriate value
		/// for each item in the set. Disable items that can't be set.
		/// </summary>
		public void FakeDoit(IEnumerable<int> itemsToChange, int tagMadeUpFieldIdentifier, int tagEnable, ProgressState state)
		{
			var tss = TsStringUtils.MakeString(m_selectedLabel, m_cache.DefaultAnalWs);
			// Build a Set of parts of speech that can take this class.
			var possiblePOS = GetPossiblePartsOfSpeech();
			var i = 0;
			// Report progress 50 times or every 100 items, whichever is more (but no more than once per item!)
			var interval = Math.Min(100, Math.Max(itemsToChange.Count() / 50, 1));
			foreach (var hvo in itemsToChange)
			{
				i++;
				if (i % interval == 0)
				{
					state.PercentDone = i * 100 / itemsToChange.Count();
					state.Breath();
				}
				var fEnable = IsItemEligible(m_sda, hvo, possiblePOS);
				if (fEnable)
				{
					m_sda.SetString(hvo, tagMadeUpFieldIdentifier, tss);
				}
				m_sda.SetInt(hvo, tagEnable, (fEnable ? 1 : 0));
			}
		}

		/// <summary>
		/// Used by SemanticDomainChooserBEditControl to make suggestions and then call FakeDoIt
		/// </summary>
		public void MakeSuggestions(IEnumerable<int> itemsToChange, int tagMadeUpFieldIdentifier, int tagEnabled, ProgressState state)
		{
			throw new NotSupportedException();
		}

		public List<int> FieldPath => new List<int>(new[] { LexSenseTags.kflidMorphoSyntaxAnalysis, MoStemMsaTags.kflidPartOfSpeech, MoStemMsaTags.kflidMsFeatures });

		private bool IsItemEligible(ISilDataAccess sda, int hvo, HashSet<int> possiblePOS)
		{
			var hvoMsa = sda.get_ObjectProp(hvo, LexSenseTags.kflidMorphoSyntaxAnalysis);
			if (hvoMsa == 0)
			{
				return false;
			}
			var clsid = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoMsa).ClassID;
			if (clsid != MoStemMsaTags.kClassId)
			{
				return false;
			}
			var fEnable = false;
			var pos = sda.get_ObjectProp(hvoMsa, MoStemMsaTags.kflidPartOfSpeech);
			if (pos != 0 && possiblePOS.Contains(pos))
			{
				// Only show it as a change if it is different
				var hvoFeature = sda.get_ObjectProp(hvoMsa, MoStemMsaTags.kflidMsFeatures);
				fEnable = hvoFeature != m_selectedHvo;
			}
			return fEnable;
		}

		private IPartOfSpeech GetPOS()
		{
			if (m_selectedHvo != 0)
			{
				var obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_selectedHvo).Owner;
				while (obj != null && !(obj is IPartOfSpeech))
				{
					obj = obj.Owner;
				}
				return obj as IPartOfSpeech;
			}
			return null;
		}

		private HashSet<int> GetPossiblePartsOfSpeech()
		{
			var sda = m_cache.DomainDataByFlid;
			var possiblePOS = new HashSet<int>();
			if (m_selectedHvo != 0)
			{
				var obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_selectedHvo).Owner;
				while (obj != null && !(obj is IPartOfSpeech))
				{
					obj = obj.Owner;
				}
				if (obj != null)
				{
					AddChildPos(sda, obj.Hvo, possiblePOS);
				}
			}
			return possiblePOS;
		}

		/// <summary>
		/// Handles a TreeCombo control for use in selecting inflection features.
		/// </summary>
		private sealed class InflectionFeaturePopupTreeManager : PopupTreeManager
		{
			private const int kEmpty = 0;
			private const int kLine = -1;
			private const int kMore = -2;

			/// <summary />
			public InflectionFeaturePopupTreeManager(TreeCombo treeCombo, LcmCache cache, bool useAbbr, FlexComponentParameters flexComponentParameters, Form parent, int wsDisplay)
				: base(treeCombo, cache, flexComponentParameters, cache.LanguageProject.PartsOfSpeechOA, wsDisplay, useAbbr, parent)
			{
			}

			protected override TreeNode MakeMenuItems(PopupTree popupTree, int hvoTarget)
			{
				var tagNamePOS = UseAbbr ? CmPossibilityTags.kflidAbbreviation : CmPossibilityTags.kflidName;
				var relevantPartsOfSpeech = new List<HvoTreeNode>();
				ControlServices.GatherPartsOfSpeech(Cache, PartOfSpeechTags.kflidInflectableFeats, tagNamePOS, WritingSystem, relevantPartsOfSpeech);
				relevantPartsOfSpeech.Sort();
				TreeNode match = null;
				foreach (var item in relevantPartsOfSpeech)
				{
					popupTree.Nodes.Add(item);
					var pos = Cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>().GetObject(item.Hvo);
					foreach (var fs in pos.ReferenceFormsOC)
					{
						// Note: beware of using fs.ShortName. That can be
						// absolutely EMPTY (if the user has turned off the 'Show Abbreviation as its label'
						// field for both the feature category and value).
						// ChooserName shows the short name if it is non-empty, otherwise the long name.
						var node = new HvoTreeNode(fs.ChooserNameTS, fs.Hvo);
						item.Nodes.Add(node);
						if (fs.Hvo == hvoTarget)
						{
							match = node;
						}
					}
					item.Nodes.Add(new HvoTreeNode(TsStringUtils.MakeString(LanguageExplorerControls.ksChooseInflFeats, Cache.WritingSystemFactory.UserWs), kMore));
				}
				return match;
			}

			protected override void m_treeCombo_AfterSelect(object sender, TreeViewEventArgs e)
			{
				var selectedNode = e.Node as HvoTreeNode;
				var pt = GetPopupTree();
				switch (selectedNode.Hvo)
				{
					case kMore:
						// Only launch the dialog by a mouse click (or simulated mouse click).
						if (e.Action != TreeViewAction.ByMouse)
						{
							break;
						}
						// Force the PopupTree to Hide() to trigger popupTree_PopupTreeClosed().
						// This will effectively revert the list selection to a previous confirmed state.
						// Whatever happens below, we don't want to actually leave the "More..." node selected!
						// This is at least required if the user selects "Cancel" from the dialog below.
						pt.Hide();
						using (var dlg = new MsaInflectionFeatureListDlg())
						{
							var parentNode = selectedNode.Parent as HvoTreeNode;
							var hvoPos = parentNode.Hvo;
							var pos = Cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>().GetObject(hvoPos);
							dlg.SetDlgInfo(Cache, _flexComponentParameters.PropertyTable, pos);
							switch (dlg.ShowDialog(ParentForm))
							{
								case DialogResult.OK:
									{
										var hvoFs = 0;
										if (dlg.FS != null)
										{
											hvoFs = dlg.FS.Hvo;
										}
										LoadPopupTree(hvoFs);
										// In the course of loading the popup tree, we will have selected the hvoFs item, and triggered an AfterSelect.
										// But, it will have had an Unknown action, and thus will not trigger some effects we want.
										// That one will work like arrowing over items: they are 'selected', but the system will not
										// behave as if the user actually chose this item.
										// But, we want clicking OK in the dialog to produce the same result as clicking an item in the list.
										// So, we need to trigger an AfterSelect with our own event args, which (since we're acting on it)
										// must have a ByMouse TreeViewAction.
										base.m_treeCombo_AfterSelect(sender, e);
										// everything should be setup with new node selected, so return.
										return;
									}
								case DialogResult.Yes:
									{
										// go to m_highestPOS in editor
										LinkHandler.PublishFollowLinkMessage(_flexComponentParameters.Publisher, new FwLinkArgs(LanguageExplorerConstants.PosEditMachineName, dlg.HighestPOS.Guid));
										if (ParentForm != null && ParentForm.Modal)
										{
											// Close the dlg that opened the popup tree,
											// since its hotlink was used to close it,
											// and a new item has been created.
											ParentForm.DialogResult = DialogResult.Cancel;
											ParentForm.Close();
										}
										break;
									}
								default:
									// NOTE: If the user has selected "Cancel", then don't change
									// our m_lastConfirmedNode to the "More..." node. Keep it
									// the value set by popupTree_PopupTreeClosed() when we
									// called pt.Hide() above. (cf. comments in LT-2522)
									break;
							}
						}
						break;
					default:
						break;
				}
				// FWR-3432 - If we get here and we still haven't got a valid Hvo, don't continue
				// on to the base method. It'll crash.
				if (selectedNode.Hvo == kMore)
				{
					return;
				}
				base.m_treeCombo_AfterSelect(sender, e);
			}
		}
	}
}