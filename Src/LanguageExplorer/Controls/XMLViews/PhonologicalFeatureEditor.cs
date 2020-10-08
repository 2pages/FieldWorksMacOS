// Copyright (c) 2013-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.LcmUi;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// PhonologicalFeatureEditor is the spec/display component of the Bulk Edit bar used to
	/// set the Phonological features of a PhPhoneme.
	/// </summary>
	internal sealed class PhonologicalFeatureEditor : IBulkEditSpecControl, IDisposable
	{
		private TreeCombo m_tree;
		private LcmCache m_cache;
		private IPublisher m_publisher;
		private ISubscriber _subscriber;
		private XMLViewsDataCache m_sda;
		private PhonologicalFeaturePopupTreeManager m_PhonologicalFeatureTreeManager;
		private int m_displayWs;
		private IFsClosedFeature m_closedFeature;
		public event FwSelectionChangedEventHandler ValueChanged;
		public event EventHandler<TargetFeatureEventArgs> EnableTargetFeatureCombo;

		private PhonologicalFeatureEditor()
		{
			m_PhonologicalFeatureTreeManager = null;
			m_tree = new TreeCombo();
			m_tree.TreeLoad += m_tree_TreeLoad;
			//	Handle AfterSelect event in m_tree_TreeLoad() through m_pOSPopupTreeManager
		}

		internal PhonologicalFeatureEditor(IPublisher publisher, ISubscriber subscriber, XElement configurationNode)
			: this()
		{
			m_publisher = publisher;
			_subscriber = subscriber;
			m_displayWs = WritingSystemServices.GetMagicWsIdFromName(XmlUtils.GetOptionalAttributeValue(configurationNode, "displayWs", "best analorvern"));
			var layout = XmlUtils.GetOptionalAttributeValue(configurationNode, "layout");
			if (string.IsNullOrEmpty(layout))
			{
				return;
			}
			const string layoutName = "CustomMultiStringForFeatureDefn_";
			var i = layout.IndexOf(layoutName);
			if (i >= 0)
			{
				FeatDefnAbbr = layout.Substring(i + layoutName.Length);
			}
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
		~PhonologicalFeatureEditor()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary />
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
				if (m_PhonologicalFeatureTreeManager != null)
				{
					m_PhonologicalFeatureTreeManager.AfterSelect -= m_PhonFeaturePopupTreeManager_AfterSelect;
					m_PhonologicalFeatureTreeManager.Dispose();
				}
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			SelectedLabel = null;
			m_tree = null;
			m_PhonologicalFeatureTreeManager = null;
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
		/// Semantic Domain Chooser BEdit Control overrides to return its Button
		/// </summary>
		public Button SuggestButton => null;

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
			if (m_PhonologicalFeatureTreeManager == null)
			{
				if (!string.IsNullOrEmpty(FeatDefnAbbr))
				{
					// Find the feature definition this editor was created to choose options from
					var featDefns = m_cache.LangProject.PhFeatureSystemOA.FeaturesOC.Where(s => s.Abbreviation.BestAnalysisAlternative.Text == FeatDefnAbbr);
					if (featDefns.Any())
					{
						m_closedFeature = featDefns.First() as IFsClosedFeature;
					}
				}
				m_PhonologicalFeatureTreeManager = new PhonologicalFeaturePopupTreeManager(m_tree, m_cache, false, new FlexComponentParameters(PropertyTable, m_publisher, _subscriber), PropertyTable.GetValue<Form>(FwUtilsConstants.window),
																						   m_displayWs, m_closedFeature);
				m_PhonologicalFeatureTreeManager.AfterSelect += m_PhonFeaturePopupTreeManager_AfterSelect;}
			m_PhonologicalFeatureTreeManager.LoadPopupTree(0);
		}

		private void m_PhonFeaturePopupTreeManager_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Arrange to turn all relevant items blue.
			// Remember which item was selected so we can later 'doit'.
			if (e.Node == null)
			{
				SelectedHvo = 0;
				SelectedLabel = string.Empty;
			}
			else
			{
				var hvo = ((HvoTreeNode)e.Node).Hvo;
				if (hvo == PhonologicalFeaturePopupTreeManager.kRemoveThisFeature)
				{
					if (sender is PhonologicalFeaturePopupTreeManager ptm)
					{
						SelectedHvo = ptm.ClosedFeature.Hvo;
						SelectedLabel = LcmUiResources.ksRemoveThisFeature;
						EnableTargetFeatureCombo?.Invoke(this, new TargetFeatureEventArgs(true));
					}
				}
				else
				{
					var obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvo);
					switch (obj)
					{
						case IFsFeatStruc _:
							SelectedHvo = hvo;
							SelectedLabel = e.Node.Text;
							// since we're using the phonological feature chooser, disable the
							// Target Feature combo (it's no longer relevant)
							EnableTargetFeatureCombo?.Invoke(this, new TargetFeatureEventArgs(false));
							break;
						case IFsSymFeatVal _:
							SelectedHvo = hvo;
							SelectedLabel = e.Node.Text;
							EnableTargetFeatureCombo?.Invoke(this, new TargetFeatureEventArgs(true));
							break;
						default:
							SelectedHvo = 0;
							SelectedLabel = string.Empty;
							m_tree.Text = string.Empty;
							EnableTargetFeatureCombo?.Invoke(this, new TargetFeatureEventArgs(true));
							break;
					}
				}
			}
			// Tell the parent control that we may have changed the selected item so it can
			// enable or disable the Apply and Preview buttons based on the selection.
			ValueChanged?.Invoke(this, new FwObjectSelectionEventArgs(SelectedHvo));
		}

		/// <summary>
		/// Required interface member not yet used.
		/// </summary>
		public IVwStylesheet Stylesheet
		{
			set { }
		}

		/// <summary>
		///
		/// </summary>
		public bool SelectedItemIsFsFeatStruc => m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetClsid(SelectedHvo) == FsFeatStrucTags.kClassId;

		/// <summary>
		/// Execute the change requested by the current selection in the combo.
		/// There are two main cases:
		/// 1. The user is removing a feature.
		/// 2. The user is using priority union to include the values of a feature structure.
		/// The latter has two subcases:
		/// a. The user has selected a value for the targeted feature and we have put that value in a FsFeatStruc.
		/// b. The user has employed the chooser to build a FsFeatStruc with the value(s) to change.  These values
		/// may or may not be for the targeted feature.
		/// We do nothing to (phoneme) records where the check box is turned off.
		/// For phonemes with the check box on, we either
		/// 1. remove the specified feature from the phoneme or
		/// 2. use priority union to set the value(s) in the FsFeatStruc.
		/// </summary>
		public void DoIt(IEnumerable<int> itemsToChange, ProgressState state)
		{
			var selectedObject = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(SelectedHvo);
			var i = 0;
			// Report progress 50 times or every 100 items, whichever is more (but no more than once per item!)
			var interval = Math.Min(100, Math.Max(itemsToChange.Count() / 50, 1));
			m_cache.DomainDataByFlid.BeginUndoTask(LcmUiResources.ksUndoBEPhonemeFeatures, LcmUiResources.ksRedoBEPhonemeFeatures);
			if (SelectedHvo != 0)
			{
				var fsTarget = GetTargetFsFeatStruc();
				foreach (var hvoPhoneme in itemsToChange)
				{
					i++;
					if (i % interval == 0)
					{
						state.PercentDone = i * 100 / itemsToChange.Count() + 20;
						state.Breath();
					}
					var phoneme = m_cache.ServiceLocator.GetInstance<IPhPhonemeRepository>().GetObject(hvoPhoneme);
					if (phoneme.FeaturesOA == null)
					{
						phoneme.FeaturesOA = Cache.ServiceLocator.GetInstance<IFsFeatStrucFactory>().Create();
					}
					if (fsTarget == null && selectedObject is IFsClosedFeature)
					{  // it's the remove option
						var firstClosedValue = phoneme.FeaturesOA.FeatureSpecsOC.FirstOrDefault(s => s.FeatureRA == selectedObject);
						if (firstClosedValue != null)
						{
							phoneme.FeaturesOA.FeatureSpecsOC.Remove(firstClosedValue);
						}
					}
					else
					{
						phoneme.FeaturesOA.PriorityUnion(fsTarget);
					}
				}
			}
			m_cache.DomainDataByFlid.EndUndoTask();
		}

		private IFsFeatStruc GetTargetFsFeatStruc()
		{
			IFsFeatStruc fsTarget = null;
			var obj = Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(SelectedHvo);
			switch (obj)
			{
				case IFsFeatStruc featStruc:
					fsTarget = featStruc;
					break;
				case IFsSymFeatVal symFeatVal:
				{
					var closedValue = symFeatVal;
					fsTarget = m_PhonologicalFeatureTreeManager.CreateEmptyFeatureStructureInAnnotation(symFeatVal);
					var fsClosedValue = Cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
					fsTarget.FeatureSpecsOC.Add(fsClosedValue);
					fsClosedValue.FeatureRA = (IFsFeatDefn)closedValue.Owner;
					fsClosedValue.ValueRA = closedValue;
					break;
				}
			}
			return fsTarget;
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

		public void ClearPreviousPreviews(IEnumerable<int> itemsToChange, int tagMadeUpFieldIdentifier)
		{
			foreach (var hvo in itemsToChange)
			{
				m_sda.RemoveMultiBaseStrings(hvo, tagMadeUpFieldIdentifier);
			}
		}

		/// <summary>
		/// Fake doing the change by setting the specified property to the appropriate value
		/// for each item in the set. Disable items that can't be set.
		/// </summary>
		public void FakeDoit(IEnumerable<int> itemsToChange, int tagMadeUpFieldIdentifier, int tagEnable, ProgressState state)
		{
			var labelToShow = SelectedLabel;
			// selectedHvo refers to either a FsFeatStruc we've made or the targeted feature
			var selectedObject = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(SelectedHvo);
			if (selectedObject is IFsFeatStruc)
			{
				labelToShow = GetLabelToShow(); // get the value for the targeted feature from the FsFeatStruc
			}
			else if (selectedObject is IFsClosedFeature)
			{
				labelToShow = " "; // it is the remove option so we just show nothing after the arrow
			}
			var tss = TsStringUtils.MakeString(labelToShow, m_cache.DefaultAnalWs);
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
				var fEnable = IsItemEligible(m_sda, hvo, selectedObject, labelToShow);
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

		/// <summary>
		/// This finds the value of the targeted feature in a FsFeatStruc
		/// </summary>
		private string GetLabelToShow()
		{
			var featureValuePairs = FeatureValuePairsInSelectedFeatStruc;
			if (!featureValuePairs.Any())
			{
				return string.Empty;
			}
			var matchPattern = FeatDefnAbbr + ":";
			var item = featureValuePairs.FirstOrDefault(abbr => abbr.StartsWith(matchPattern));
			return item?.Substring(matchPattern.Length) ?? string.Empty;
		}

		/// <summary>
		/// Return list of strings of abbreviations
		/// </summary>
		public string[] FeatureValuePairsInSelectedFeatStruc
		{
			get
			{
				var featValuePairs = SelectedLabel.Split(' ');
				if (featValuePairs.Any())
				{
					if (featValuePairs[0].StartsWith("["))
					{
						featValuePairs[0] = featValuePairs[0].Substring(1);
					}

					if (featValuePairs.Last().EndsWith("]"))
					{
						featValuePairs[featValuePairs.Length - 1] = featValuePairs.Last().Substring(0, featValuePairs.Last().Length - 1);
					}
				}
				return featValuePairs;
			}

		}
		/// <summary>
		/// Get feature definition abbreviation (column heading)
		/// </summary>
		public string FeatDefnAbbr { get; }

		public List<int> FieldPath => new List<int>(new[] { PhPhonemeTags.kflidFeatures });

		/// <summary />
		public int SelectedHvo { get; set; }

		/// <summary />
		public string SelectedLabel { get; set; }

		private bool IsItemEligible(ISilDataAccess sda, int hvo, ICmObject selectedObject, string labelToShow)
		{
			if (string.IsNullOrEmpty(labelToShow))
			{
				return false;
			}
			var hvoFeats = sda.get_ObjectProp(hvo, PhPhonemeTags.kflidFeatures);
			if (hvoFeats == 0)
			{
				return true; // phoneme does not have any features yet, so it is eligible
			}
			var feats = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoFeats);
			var clsid = feats.ClassID;
			if (clsid != FsFeatStrucTags.kClassId)
			{
				return false;
			}
			// Show it as a change only if it is different
			var features = (IFsFeatStruc)feats;
			switch (selectedObject.ClassID)
			{
				case FsSymFeatValTags.kClassId: // user has chosen a value for the targeted feature
					if (selectedObject is IFsSymFeatVal symFeatVal)
					{
						return !features.FeatureSpecsOC.Any(s => s.ClassID == FsClosedValueTags.kClassId && ((IFsClosedValue)s).ValueRA == symFeatVal);
					}
					break;
				case FsFeatStrucTags.kClassId: // user has specified one or more feature/value pairs
					if (selectedObject is IFsFeatStruc)
					{
						return !features.FeatureSpecsOC.Any(s =>
							s.ClassID == FsClosedValueTags.kClassId &&
							s.FeatureRA.Abbreviation.BestAnalysisAlternative.Text == FeatDefnAbbr &&
							((IFsClosedValue)s).ValueRA.Abbreviation.BestAnalysisAlternative.Text == labelToShow);
					}

					break;
				case FsClosedFeatureTags.kClassId: // user has chosen the remove targeted feature option
					if (selectedObject is IFsClosedFeature closedFeature)
					{
						return features.FeatureSpecsOC.Any(s => s.FeatureRA == closedFeature);
					}
					break;
				default:
					return hvoFeats != SelectedHvo;
			}
			return false;
		}

		/// <summary>
		/// Handles a TreeCombo control for use in selecting inflection features.
		/// </summary>
		private sealed class PhonologicalFeaturePopupTreeManager : PopupTreeManager
		{
			/// <summary>
			/// Used to indicate that a feature needs to be removed from the list of feature/value pairs in a phoneme
			/// </summary>
			public const int kRemoveThisFeature = -2;
			private const int kChoosePhonologicaFeatures = -3;
			private List<ICmBaseAnnotation> m_annotations = new List<ICmBaseAnnotation>();

			/// <summary />
			public PhonologicalFeaturePopupTreeManager(TreeCombo treeCombo, LcmCache cache, bool useAbbr, FlexComponentParameters flexComponentParameters, Form parent, int wsDisplay, IFsClosedFeature closedFeature)
				: base(treeCombo, cache, flexComponentParameters, cache.LanguageProject.PartsOfSpeechOA, wsDisplay, useAbbr, parent)
			{
				ClosedFeature = closedFeature;
			}

			/// <summary>
			/// The target feature (the one that the user selects in the "Target Field" dropdown combo box)
			/// </summary>
			public IFsClosedFeature ClosedFeature { get; }

			/// <summary />
			/// <remarks>These annotations and their feature structure objects are private to this control.
			/// They are deleted when this control is disposed.
			/// </remarks>
			public IFsFeatStruc CreateEmptyFeatureStructureInAnnotation(ICmObject obj)
			{
				var cba = Cache.ServiceLocator.GetInstance<ICmBaseAnnotationFactory>().Create();
				Cache.LanguageProject.AnnotationsOC.Add(cba);
				cba.BeginObjectRA = obj;
				var fs = Cache.ServiceLocator.GetInstance<IFsFeatStrucFactory>().Create();
				cba.FeaturesOA = fs;
				m_annotations.Add(cba);
				return fs;
			}

			protected override TreeNode MakeMenuItems(PopupTree popupTree, int hvoTarget)
			{
				TreeNode match = null;
				// We need a way to store feature structures the user has chosen during this session.
				// We use an annotation to do this.
				foreach (var cba in m_annotations)
				{
					var fs = cba.FeaturesOA;
					if (fs == null || fs.IsEmpty)
					{
						continue;
					}
					if (cba.BeginObjectRA != null)
					{
						continue;  // is not one of the feature structures created via the phon feat chooser
					}
					var node = new HvoTreeNode(fs.LongNameTSS, fs.Hvo);
					popupTree.Nodes.Add(node);
					if (fs.Hvo == hvoTarget)
					{
						match = node;
					}
				}
				if (ClosedFeature != null)
				{
					var sortedValues = ClosedFeature.ValuesOC.OrderBy(v => v.Abbreviation.BestAnalysisAlternative.Text);
					foreach (var closedValue in sortedValues)
					{
						var node = new HvoTreeNode(closedValue.Abbreviation.BestAnalysisAlternative, closedValue.Hvo);
						popupTree.Nodes.Add(node);
						if (closedValue.Hvo == hvoTarget)
						{
							match = node;
						}
					}
				}
				popupTree.Nodes.Add(new HvoTreeNode(TsStringUtils.MakeString(LanguageExplorerControls.ksRemoveThisFeature, Cache.WritingSystemFactory.UserWs), kRemoveThisFeature));
				return match;
			}

			protected override void m_treeCombo_AfterSelect(object sender, TreeViewEventArgs e)
			{
				var selectedNode = e.Node as HvoTreeNode;
				var pt = GetPopupTree();
				switch (selectedNode.Hvo)
				{
					case kChoosePhonologicaFeatures:
						// Only launch the dialog by a mouse click (or simulated mouse click).
						if (e.Action != TreeViewAction.ByMouse)
						{
							break;
						}
						// Force the PopupTree to Hide() to trigger popupTree_PopupTreeClosed().
						// This will effectively revert the list selection to a previous confirmed state.
						// Whatever happens below, we don't want to actually leave the "Choose phonological features" node selected!
						// This is at least required if the user selects "Cancel" from the dialog below.
						// N.B. the above does not seem to be true; therefore we check for cancel and an empty result
						// and force the combo text to be what it should be.
						pt.Hide();
						using (var dlg = new PhonologicalFeatureChooserDlg())
						{
							Cache.DomainDataByFlid.BeginUndoTask(LanguageExplorerControls.ksUndoInsertPhonologicalFeature, LanguageExplorerControls.ksRedoInsertPhonologicalFeature);
							var fs = CreateEmptyFeatureStructureInAnnotation(null);
							dlg.SetDlgInfo(Cache, _flexComponentParameters, fs);
							dlg.ShowIgnoreInsteadOfDontCare = true;
							dlg.SetHelpTopic("khtptoolBulkEditPhonemesChooserDlg");
							var result = dlg.ShowDialog(ParentForm);
							if (result == DialogResult.OK)
							{
								if (dlg.FS != null)
								{
									var sFeatures = dlg.FS.LongName;
									if (string.IsNullOrEmpty(sFeatures))
									{
										// user did not select anything in chooser; we want to show the last known node
										// in the dropdown, not "choose phonological feature".
										SetComboTextToLastConfirmedSelection();
									}
									else if (!pt.Nodes.ContainsKey(sFeatures))
									{
										var newSelectedNode = new HvoTreeNode(fs.LongNameTSS, fs.Hvo);
										pt.Nodes.Add(newSelectedNode);
										LoadPopupTree(fs.Hvo);
										selectedNode = newSelectedNode;
									}
								}
							}
							else if (result != DialogResult.Cancel)
							{
								dlg.HandleJump();
							}
							else if (result == DialogResult.Cancel)
							{
								// The user canceled out of the chooser; we want to show the last known node
								// in the dropdown, not "choose phonological feature".
								SetComboTextToLastConfirmedSelection();
							}
							Cache.DomainDataByFlid.EndUndoTask();
						}
						break;
				}
				// FWR-3432 - If we get here and we still haven't got a valid Hvo, don't continue
				// on to the base method. It'll crash.
				if (selectedNode.Hvo == kChoosePhonologicaFeatures)
				{
					return;
				}
				base.m_treeCombo_AfterSelect(sender, e);
			}

			#region IDisposable & Co. implementation

			/// <summary>
			/// Finalizer, in case client doesn't dispose it.
			/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
			/// </summary>
			/// <remarks>
			/// In case some clients forget to dispose it directly.
			/// </remarks>
			~PhonologicalFeaturePopupTreeManager()
			{
				Dispose(false);
				// The base class finalizer is called automatically.
			}

			/// <inheritdoc />
			protected override void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
				if (IsDisposed)
				{
					// No need to run it more than once.
					return;
				}

				if (disposing)
				{
					if (m_annotations != null)
					{
						Cache.DomainDataByFlid.BeginUndoTask(LanguageExplorerControls.ksUndoInsertPhonologicalFeature, LanguageExplorerControls.ksRedoInsertPhonologicalFeature);
						foreach (var cmBaseAnnotation in m_annotations)
						{
							cmBaseAnnotation.Delete();
						}
						Cache.DomainDataByFlid.EndUndoTask();
					}
				}
				// Dispose unmanaged resources here, whether disposing is true or false.
				m_annotations = null;
				base.Dispose(disposing);
			}

			#endregion IDisposable & Co. implementation
		}
	}
}