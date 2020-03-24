// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
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
	/// InflectionClassEditor is the spec/display component of the Bulk Edit bar used to
	/// set the Inflection class of a LexSense (which must already have an MoStemMsa with
	/// POS set).
	/// </summary>
	public class InflectionClassEditor : IBulkEditSpecControl, IDisposable
	{
		private TreeCombo m_tree;
		private LcmCache m_cache;
		private IPublisher m_publisher;
		private ISubscriber _subscriber;
		protected XMLViewsDataCache m_sda;
		private InflectionClassPopupTreeManager m_InflectionClassTreeManager;
		private int m_selectedHvo;
		private string m_selectedLabel;
		private int m_displayWs;
		public event EventHandler ControlActivated;
		public event FwSelectionChangedEventHandler ValueChanged;

		private InflectionClassEditor()
		{
			m_InflectionClassTreeManager = null;
			m_tree = new TreeCombo();
			m_tree.TreeLoad += m_tree_TreeLoad;
			//	Handle AfterSelect event in m_tree_TreeLoad() through m_pOSPopupTreeManager
		}

		public InflectionClassEditor(IPublisher publisher, ISubscriber subscriber, XElement configurationNode)
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
		~InflectionClassEditor()
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
		protected virtual void Dispose(bool disposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
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
				if (m_InflectionClassTreeManager != null)
				{
					m_InflectionClassTreeManager.AfterSelect -= m_pOSPopupTreeManager_AfterSelect;
					m_InflectionClassTreeManager.Dispose();
				}
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_selectedLabel = null;
			m_tree = null;
			m_InflectionClassTreeManager = null;
			m_cache = null;

			IsDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		/// <summary>
		/// Get/Set the property table'
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
			if (m_InflectionClassTreeManager == null)
			{
				m_InflectionClassTreeManager = new InflectionClassPopupTreeManager(m_tree, m_cache, new FlexComponentParameters(PropertyTable, m_publisher, _subscriber), false, PropertyTable.GetValue<Form>(FwUtils.window), m_displayWs);
				m_InflectionClassTreeManager.AfterSelect += m_pOSPopupTreeManager_AfterSelect;
			}
			m_InflectionClassTreeManager.LoadPopupTree(0);
		}

		private void m_pOSPopupTreeManager_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Todo: user selected a part of speech.
			// Arrange to turn all relevant items blue.
			// Remember which item was selected so we can later 'doit'.
			if (e.Node is HvoTreeNode hvoTreeNode)
			{
				var hvo = hvoTreeNode.Hvo;
				var clid = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetClsid(hvo);
				if (clid == MoInflClassTags.kClassId)
				{
					m_selectedHvo = hvo;
					m_selectedLabel = hvoTreeNode.Text;
				}
				else
				{
					m_tree.SelectedItem = null;
					m_selectedHvo = 0;
					m_selectedLabel = string.Empty;
				}
			}
			else
			{
				m_selectedHvo = 0;
				m_selectedLabel = string.Empty;
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
		/// Basically we want the MoInflClass indicated by m_selectedHvo, (even if 0?? not yet possible),
		/// to become the InflectionClass of each record that is appropriate to change.
		/// We do nothing to records where the check box is turned off,
		/// and nothing to ones that currently have an MSA other than an MoStemMsa,
		/// and nothing to ones that currently have an MSA with the wrong POS.
		/// (a) If the owning entry has an MoStemMsa with the right inflection class (and presumably POS),
		/// set the sense to use it.
		/// (b) If all senses using the current MoStemMsa are to be changed, just update
		/// the inflection class of that MoStemMsa.
		/// We could add this...but very probably unused MSAs would have been taken over
		/// when setting the POS.
		/// --(c) If the entry has an MoStemMsa which is not being used at all, change it to
		/// --the required POS and inflection class and use it.
		/// (d) Make a new MoStemMsa in the LexEntry with the required POS and inflection class
		/// and point the sense at it.
		/// </summary>
		public void DoIt(IEnumerable<int> itemsToChange, ProgressState state)
		{
			// A Set of eligible parts of speech to use in filtering.
			var possiblePOS = GetPossiblePartsOfSpeech();
			// Make a Dictionary from HVO of entry to list of modified senses.
			var sensesByEntryAndPos = new Dictionary<Tuple<ILexEntry, IPartOfSpeech>, List<ILexSense>>();
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
				var msa = (IMoStemMsa)ls.MorphoSyntaxAnalysisRA;
				var entry1 = ls.Entry;
				var key = new Tuple<ILexEntry, IPartOfSpeech>(entry1, msa.PartOfSpeechRA);
				if (!sensesByEntryAndPos.ContainsKey(key))
				{
					sensesByEntryAndPos[key] = new List<ILexSense>();
				}
				sensesByEntryAndPos[key].Add(ls);
			}
			m_cache.DomainDataByFlid.BeginUndoTask(LcmUiStrings.ksUndoBEInflClass, LcmUiStrings.ksRedoBEInflClass);
			i = 0;
			interval = Math.Min(100, Math.Max(sensesByEntryAndPos.Count / 50, 1));
			foreach (var kvp in sensesByEntryAndPos)
			{
				i++;
				if (i % interval == 0)
				{
					state.PercentDone = i * 80 / sensesByEntryAndPos.Count + 20;
					state.Breath();
				}
				var entry = kvp.Key.Item1;
				var sensesToChange = kvp.Value;
				var msmTarget = entry.MorphoSyntaxAnalysesOC.Select(msa => msa as IMoStemMsa).FirstOrDefault(msm => msm?.InflectionClassRA != null && msm.InflectionClassRA.Hvo == m_selectedHvo);
				if (msmTarget == null)
				{
					// See if we can reuse an existing MoStemMsa by changing it.
					// This is possible if it is used only by senses in the list, or not used at all.
					var otherSenses = new List<ILexSense>();
					if (entry.SensesOS.Count != sensesToChange.Count)
					{
						otherSenses.AddRange(entry.SensesOS.Where(ls => !sensesToChange.Contains(ls)));
					}
					foreach (var msa in entry.MorphoSyntaxAnalysesOC)
					{
						var msm = msa as IMoStemMsa;
						if (msm == null)
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
							// Adjust its POS as well as its inflection class, just to be sure.
							msmTarget = msm;
							msmTarget.PartOfSpeechRA = kvp.Key.Item2;
							msmTarget.InflectionClassRA = m_cache.ServiceLocator.GetInstance<IMoInflClassRepository>().GetObject(m_selectedHvo);
							break;
						}
					}
				}
				if (msmTarget == null)
				{
					// Nothing we can reuse...make a new one.
					msmTarget = m_cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
					entry.MorphoSyntaxAnalysesOC.Add(msmTarget);
					msmTarget.PartOfSpeechRA = kvp.Key.Item2;
					msmTarget.InflectionClassRA = m_cache.ServiceLocator.GetInstance<IMoInflClassRepository>().GetObject(m_selectedHvo);
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
		/// for each item in the list. Disable items that can't be set.
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
			throw new NotSupportedException("The method or operation is not supported.");
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

		public List<int> FieldPath => new List<int>(new[] { LexSenseTags.kflidMorphoSyntaxAnalysis, MoStemMsaTags.kflidPartOfSpeech, MoStemMsaTags.kflidInflectionClass });

		private bool IsItemEligible(ISilDataAccess sda, int hvo, HashSet<int> possiblePOS)
		{
			var fEnable = false;
			var ls = m_cache.ServiceLocator.GetInstance<ILexSenseRepository>().GetObject(hvo);
			if (ls.MorphoSyntaxAnalysisRA is IMoStemMsa msa)
			{
				var pos = msa.PartOfSpeechRA;
				if (pos != null && possiblePOS.Contains(pos.Hvo))
				{
					// Only show it as a change if it is different
					fEnable = msa.InflectionClassRA == null || msa.InflectionClassRA.Hvo != m_selectedHvo;
				}
			}
			return fEnable;
		}

		private HashSet<int> GetPossiblePartsOfSpeech()
		{
			var sda = m_cache.DomainDataByFlid;
			var possiblePOS = new HashSet<int>();
			if (m_selectedHvo != 0)
			{
				var rootPos = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_selectedHvo);
				while (rootPos != null && rootPos.ClassID == MoInflClassTags.kClassId)
				{
					rootPos = rootPos.Owner;
				}

				if (rootPos != null)
				{
					AddChildPos(sda, rootPos.Hvo, possiblePOS);
				}
			}
			return possiblePOS;
		}

		/// <summary>
		/// Get a type we can use to create a compatible filter.
		/// </summary>
		public static Type FilterType()
		{
			return typeof(InflectionClassFilter);
		}

		/// <summary>
		/// A special filter, where items are LexSenses, and matches are ones where an MSA is an MoStemMsa that
		/// has the correct POS.
		/// </summary>
		private sealed class InflectionClassFilter : ColumnSpecFilter
		{
			/// <summary>
			/// Default constructor for persistence.
			/// </summary>
			public InflectionClassFilter() { }

			public InflectionClassFilter(LcmCache cache, ListMatchOptions mode, int[] targets, XElement colSpec)
				: base(cache, mode, targets, colSpec)
			{
			}

			protected override string BeSpec => "external";

			public override bool CompatibleFilter(XElement colSpec)
			{
				if (!base.CompatibleFilter(colSpec))
				{
					return false;
				}
				return DynamicLoader.TypeForLoaderNode(colSpec) == typeof(InflectionClassEditor);
			}
		}
	}
}