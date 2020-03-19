// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.LcmUi.Dialogs;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.FwUtils.MessageBoxEx;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Utils;
using SIL.Xml;
using WaitCursor = SIL.FieldWorks.Common.FwUtils.WaitCursor;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	/// <summary />
	internal sealed class LexReferenceMultiSlice : Slice
	{
		private List<ILexReference> m_refs;
		private List<ILexRefType> m_refTypesAvailable = new List<ILexRefType>();
		private List<bool> m_rgfReversedRefType = new List<bool>();

		#region IDisposable override

		/// <inheritdoc />
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
				// Dispose managed resources here.
				m_refs?.Clear();
				m_refTypesAvailable?.Clear();
				m_rgfReversedRefType?.Clear();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_refs = null;
			m_refTypesAvailable = null;
			m_rgfReversedRefType = null;

			base.Dispose(disposing);
		}

		#endregion IDisposable override

		/// <summary>
		/// Override method to add suitable control.
		/// </summary>
		public override void FinishInit()
		{
			Debug.Assert(Cache != null);
			Debug.Assert(ConfigurationNode != null);

			base.FinishInit();
		}

		void SetRefs()
		{
			var fieldName = XmlUtils.GetMandatoryAttributeValue(ConfigurationNode, "field");
			var refs = ReflectionHelper.GetProperty(MyCmObject, fieldName);
			if (refs is IEnumerable<int> refsInts)
			{
				m_refs = (refsInts.Select(hvo => Cache.ServiceLocator.GetInstance<ILexReferenceRepository>().GetObject(hvo))).ToList();
			}
			else
			{
				m_refs = new List<ILexReference>();
				if (refs is IEnumerable refsObjs)
				{
					m_refs.AddRange(refsObjs.Cast<ILexReference>());
				}
				else
				{
					Debug.Fail("LexReferenceSlice could not interpret results from " + fieldName);
				}
			}
			ContainingDataTree.MonitorProp(MyCmObject.Hvo, Cache.MetaDataCacheAccessor.GetFieldId2(MyCmObject.ClassID, fieldName, true));
		}

		/// <summary />
		public override void GenerateChildren(XElement node, XElement caller, ICmObject obj, int indent, ref int insPos, ArrayList path, ObjSeqHashMap reuseMap, bool fUsePersistentExpansion)
		{
			// If node has children, figure what to do with them...

			// It's important to initialize m_refs here rather than in FinishInit, because we need it
			// to be updated when the slice is reused in a regenerate.
			// Refactor JohnT: better still, make it a virtual attribute, and Refresh will automatically
			// clear it from the cache.

			SetRefs();

			if (m_refs.Count == 0)
			{
				// It could have children but currently can't: we always show this as collapsedEmpty.
				Expansion = TreeItemState.ktisCollapsedEmpty;
				return;
			}
			for (var i = 0; i < m_refs.Count; i++)
			{
				GenerateChildNode(i, node, caller, indent, ref insPos, path, reuseMap);
			}
			Expansion = TreeItemState.ktisExpanded;
		}

		private void GenerateChildNode(int iChild, XElement node, XElement caller, int indent, ref int insPos, ArrayList path, ObjSeqHashMap reuseMap)
		{
			var lr = m_refs[iChild];
			var lrt = lr.Owner as ILexRefType;
			var sLabel = lrt.ShortName;
			if (string.IsNullOrEmpty(sLabel))
			{
				sLabel = lrt.Abbreviation.BestAnalysisAlternative.Text;
			}
			var fTreeRoot = true;
			var sda = Cache.DomainDataByFlid;
			var chvoTargets = sda.get_VecSize(lr.Hvo, LexReferenceTags.kflidTargets);
			// change the label for a Tree relationship.
			switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
			{
				case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
				case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
					if (chvoTargets > 0)
					{
						var hvoFirst = sda.get_VecItem(lr.Hvo, LexReferenceTags.kflidTargets, 0);
						if (hvoFirst != MyCmObject.Hvo)
						{
							return;
						}
					}
					break;
				case LexRefTypeTags.MappingTypes.kmtSenseTree:
				case LexRefTypeTags.MappingTypes.kmtEntryTree:
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
				case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair: // Sense Pair with different Forward/Reverse names
				case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair: // Entry Pair with different Forward/Reverse names
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair: // Entry or sense Pair with different Forward/Reverse names
					if (chvoTargets > 0)
					{
						var hvoFirst = sda.get_VecItem(lr.Hvo, LexReferenceTags.kflidTargets, 0);
						if (hvoFirst != MyCmObject.Hvo)
						{
							sLabel = lrt.ReverseName.BestAnalysisAlternative.Text;
							if (string.IsNullOrEmpty(sLabel))
							{
								sLabel = lrt.ReverseAbbreviation.BestAnalysisAlternative.Text;
							}
							fTreeRoot = false;
						}
					}
					break;
			}

			if (string.IsNullOrEmpty(sLabel))
			{
				sLabel = LanguageExplorerResources.ksStars;
			}
			// Theoretically, "editorSlice" will be reset to some other value in the following switch statement.
			// Otherwise, the original code would not have worked.
			var editorSlice = string.Empty;
			var sMenu = "mnuDataTree_DeleteAddLexReference";
			// generate Xml for a specific slice matching this reference
			switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
			{
				case LexRefTypeTags.MappingTypes.kmtSenseCollection:
					editorSlice = "lexreferencecollection";
					break;
				case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
					editorSlice = "lexreferenceunidirectional";
					break;
				case LexRefTypeTags.MappingTypes.kmtSensePair:
				case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair: // Sense Pair with different Forward/Reverse names
				case LexRefTypeTags.MappingTypes.kmtEntryPair:
				case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair: // Entry Pair with different Forward/Reverse names
				case LexRefTypeTags.MappingTypes.kmtEntryOrSensePair:
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair: // Entry or sense Pair with different forward/Reverse names
					editorSlice = "lexreferencepair";
					sMenu = "mnuDataTree_DeleteReplaceLexReference";
					break;
				case LexRefTypeTags.MappingTypes.kmtSenseTree:
					if (fTreeRoot)
					{
						editorSlice = "lexreferencetreebranches";
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					else
					{
						editorSlice = "lexreferencetreeroot";
						sMenu = "mnuDataTree_DeleteReplaceLexReference";
					}
					break;
				case LexRefTypeTags.MappingTypes.kmtSenseSequence:
				case LexRefTypeTags.MappingTypes.kmtEntrySequence:
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseSequence:
					editorSlice = "lexreferencesequence";
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryCollection:
					editorSlice = "lexreferencecollection";
					sMenu = "mnuDataTree_DeleteAddLexReference";
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
					editorSlice = "lexreferenceunidirectional";
					sMenu = "mnuDataTree_DeleteAddLexReference";
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryTree:
					if (fTreeRoot)
					{
						editorSlice = "lexreferencetreebranches";
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					else
					{
						editorSlice = "lexreferencetreeroot";
						sMenu = "mnuDataTree_DeleteReplaceLexReference";
					}
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseCollection:
					editorSlice = "lexreferencecollection";
					if (MyCmObject is ILexEntry)
					{
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
					editorSlice = "lexreferenceunidirectional";
					if (MyCmObject is ILexEntry)
					{
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					break;
				case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
					if (MyCmObject is ILexEntry)
					{
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					if (fTreeRoot)
					{
						editorSlice = "lexreferencetreebranches";
						sMenu = "mnuDataTree_DeleteAddLexReference";
					}
					else
					{
						editorSlice = "lexreferencetreeroot";
					}
					break;
			}

			node.ReplaceNodes(XElement.Parse($"<slice label=\"{sLabel}\" field=\"Targets\" editor=\"{editorSlice}\" mappingType=\"{lrt.MappingType}\" hvoDisplayParent=\"{MyCmObject.Hvo}\" menu=\"{sMenu}\"><deParams displayProperty=\"HeadWord\"/></slice>"));
			var firstNewSliceIndex = insPos;
			CreateIndentedNodes(caller, lr, indent, ref insPos, path, reuseMap, node);
			for (var islice = firstNewSliceIndex; islice < insPos; islice++)
			{
				var child = ContainingDataTree.Slices[islice];
				if (child is ILexReferenceSlice)
				{
					(child as ILexReferenceSlice).ParentSlice = this;
				}
			}
			node.RemoveNodes();
		}

		/// <summary>
		/// Some Slice subclasses (such as this one) do the menus themselves.
		/// </summary>
		protected override bool DoINeedToAddTheCoreContextMenus => false;

		private ContextMenuStrip m_contextMenuStrip;
		/// <summary />
		public override bool HandleMouseDown(Point p)
		{
			base.HandleMouseDown(p);
			DisposeContextMenu(this, new EventArgs());
			m_contextMenuStrip = SetupContextMenuStrip();
			m_contextMenuStrip.Closed += contextMenuStrip_Closed; // dispose when no longer needed (but not sooner! needed after this returns)
			if (m_contextMenuStrip.Items.Count > 0)
			{
				m_contextMenuStrip.Show(TreeNode, p);
			}
			return true;
		}

		private void contextMenuStrip_Closed(object sender, ToolStripDropDownClosedEventArgs e)
		{
			// It's apparently still needed by the menu handling code in .NET.
			// So we can't dispose it yet.
			// But we want to eventually (Eberhard says if it has a Dispose we MUST call it to make Mono happy)
			Application.Idle += DisposeContextMenu;
		}

		private void DisposeContextMenu(object sender, EventArgs e)
		{
			Application.Idle -= DisposeContextMenu;
			if (m_contextMenuStrip != null && !m_contextMenuStrip.IsDisposed)
			{
				m_contextMenuStrip.Dispose();
				m_contextMenuStrip = null;
			}
		}

		private ContextMenuStrip SetupContextMenuStrip()
		{
			var contextMenuStrip = new ContextMenuStrip();
			m_refTypesAvailable.Clear();
			m_rgfReversedRefType.Clear();
			var formatName = StringTable.Table.GetString("InsertSymmetricReference", StringTable.LexiconTools);
			var formatNameWithReverse = StringTable.Table.GetString("InsertAsymmetricReference", StringTable.LexiconTools);
			foreach (var lrt in Cache.LanguageProject.LexDbOA.ReferencesOA.PossibilitiesOS.Cast<ILexRefType>())
			{
				if (MyCmObject is ILexEntry)
				{
					switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
					{
						case LexRefTypeTags.MappingTypes.kmtSenseCollection:
						case LexRefTypeTags.MappingTypes.kmtSensePair:
						case LexRefTypeTags.MappingTypes.kmtSenseTree:
						case LexRefTypeTags.MappingTypes.kmtSenseSequence:
						case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair:
						case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
							continue;
						default:
							break;
					}
				}
				else
				{
					switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
					{
						case LexRefTypeTags.MappingTypes.kmtEntryCollection:
						case LexRefTypeTags.MappingTypes.kmtEntryPair:
						case LexRefTypeTags.MappingTypes.kmtEntryTree:
						case LexRefTypeTags.MappingTypes.kmtEntrySequence:
						case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair:
						case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
							continue;
						default:
							break;
					}
				}
				var label = string.Empty;
				var label2 = string.Empty;
				// was: string reverseName = ILexRefType.BestAnalysisOrVernReverseName(lrt.Cache, lrt.Hvo).Text; // replaces lrt.ReverseName.AnalysisDefaultWritingSystem;
				// ToDo JohnT: find a way to extend to include reversal writing systems.
				var reverseName = lrt.ReverseName.BestAnalysisVernacularAlternative.Text;
				if (string.IsNullOrEmpty(reverseName))
				{
					reverseName = LanguageExplorerResources.ksStars;
				}
				var name = lrt.ShortName;
				if (string.IsNullOrEmpty(name))
				{
					name = LanguageExplorerResources.ksStars;
				}
				switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseCollection:
					case LexRefTypeTags.MappingTypes.kmtSensePair:
					case LexRefTypeTags.MappingTypes.kmtSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
					case LexRefTypeTags.MappingTypes.kmtEntryCollection:
					case LexRefTypeTags.MappingTypes.kmtEntryPair:
					case LexRefTypeTags.MappingTypes.kmtEntrySequence:
					case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseCollection:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSensePair:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
						label = string.Format(formatName, name);
						break;
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
					case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair:
						label = string.Format(formatNameWithReverse, name, reverseName);
						label2 = string.Format(formatNameWithReverse, reverseName, name);
						break;
				}

				var iInsert = contextMenuStrip.Items.Count;
				contextMenuStrip.Items.Add(new ToolStripMenuItem(label, null, HandleCreateMenuItem));
				m_refTypesAvailable.Insert(iInsert, lrt);
				m_rgfReversedRefType.Insert(iInsert, false);
				if (label2.Length <= 0)
				{
					continue;
				}
				iInsert = contextMenuStrip.Items.Count;
				contextMenuStrip.Items.Add(new ToolStripMenuItem(label2, null, HandleCreateMenuItem));
				m_refTypesAvailable.Insert(iInsert, lrt);
				m_rgfReversedRefType.Insert(iInsert, true);
			}

			AddFinalContextMenuStripOptions(contextMenuStrip);
			return contextMenuStrip;
		}

		private void AddFinalContextMenuStripOptions(ContextMenuStrip contextMenuStrip)
		{
			ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip);
			contextMenuStrip.Items.Add(new ToolStripMenuItem(LanguageExplorerResources.ksCreateLexRefType_, null, HandleMoreMenuItem));

			var tsdropdown = new ToolStripDropDownMenu();
			var itemAlways = new ToolStripMenuItem(LanguageExplorerResources.ksAlwaysVisible, null, OnShowFieldAlwaysVisible1);
			var itemIfData = new ToolStripMenuItem(LanguageExplorerResources.ksHiddenUnlessData, null, OnShowFieldIfData1);
			var itemHidden = new ToolStripMenuItem(LanguageExplorerResources.ksNormallyHidden, null, OnShowFieldNormallyHidden1);
			itemAlways.CheckOnClick = true;
			itemIfData.CheckOnClick = true;
			itemHidden.CheckOnClick = true;
			itemAlways.Checked = IsVisibilityItemChecked("always");
			itemIfData.Checked = IsVisibilityItemChecked("ifdata");
			itemHidden.Checked = IsVisibilityItemChecked("never");

			tsdropdown.Items.Add(itemAlways);
			tsdropdown.Items.Add(itemIfData);
			tsdropdown.Items.Add(itemHidden);
			var fieldVis = new ToolStripMenuItem(LanguageExplorerResources.ksFieldVisibility)
			{
				DropDown = tsdropdown
			};

			ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip);
			contextMenuStrip.Items.Add(fieldVis);
		}

		/// <summary />
		public void OnShowFieldAlwaysVisible1(object sender, EventArgs args)
		{
			SetFieldVisibility("always");
		}
		/// <summary />
		public void OnShowFieldIfData1(object sender, EventArgs args)
		{
			SetFieldVisibility("ifdata");
		}
		/// <summary />
		public void OnShowFieldNormallyHidden1(object sender, EventArgs args)
		{
			SetFieldVisibility("never");
		}

		/// <summary />
		public void OnHelp(object sender, EventArgs args)
		{
			switch (HelpId)
			{
				case "LexSenseReferences":
					ShowHelp.ShowHelpTopic(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), "khtpFieldLexSenseLexicalRelations");
					break;
				case "LexEntryReferences":
					ShowHelp.ShowHelpTopic(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), "khtpFieldLexEntryCrossReference");
					break;
				default:
					Debug.Assert(false, "Tried to show help for a LexReferenceMultiSlice that does not have an associated Help Topic ID");
					break;
			}
		}

		/// <summary>
		/// Updates the display of a slice, if an hvo and tag it cares about has changed in some way.
		/// </summary>
		protected internal override bool UpdateDisplayIfNeeded(int hvo, int tag)
		{
			// Can't check hvo since it may have been deleted by an undo operation already.
			if (tag == LexRefTypeTags.kflidMembers)
			{
				// if this flickers too annoyingly, we can probably optimize by extracting relevant lines from Collapse.
				Collapse();
				Expand();
				return true;
			}
			return false;
		}

		/// <summary />
		public void HandleCreateMenuItem(object sender, EventArgs ea)
		{
			var tsItem = (ToolStripItem)sender;
			var itemIndex = (tsItem.Owner as ContextMenuStrip).Items.IndexOf(tsItem);
			var lrt = m_refTypesAvailable[itemIndex];
			var fReverseRef = m_rgfReversedRefType[itemIndex];
			ILexReference newRef = null;
			ICmObject first;
			if (fReverseRef)
			{
				// When creating a tree Lexical Relation and the user is choosing
				// the root of the tree, first see if the user selects a lexical entry.
				// If they do not select anything (hvoFirst==0) return and do not create the slice.
				first = GetRootObject(lrt);
				if (first == null)
				{
					return;     // the user cancelled out of the operation.
				}
				if (lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtSenseTree
					|| lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtEntryTree
					|| lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree)
				{
					// Use an existing ILexReference if one exists.
					foreach (var lr in lrt.MembersOC)
					{
						if (lr.TargetsRS.Count > 0 && lr.TargetsRS[0] == first)
						{
							newRef = lr;
							break;
						}
					}
				}
			}
			else
			{
				// Launch the dialog that allows the user to choose a lexical entry.
				// If they choose an entry, it is returned in hvoFirst so go ahead and
				// create the lexical relation and add this lexical entry to that relation.
				first = GetChildObject(lrt);
				if (first == null)
				{
					return;     // the user cancelled out of the operation.
				}
			}

			UowHelpers.UndoExtension(tsItem.Text, Cache.ActionHandlerAccessor, () =>
			{
				if (newRef != null)
				{
					newRef.TargetsRS.Add(MyCmObject);
				}
				else
				{
					newRef = Cache.ServiceLocator.GetInstance<ILexReferenceFactory>().Create();
					lrt.MembersOC.Add(newRef);
					if (fReverseRef)
					{
						newRef.TargetsRS.Insert(0, first);
						newRef.TargetsRS.Insert(1, MyCmObject);
					}
					else
					{
						//When creating a lexical relation slice,
						//add the current lexical entry to the lexical relation as the first item
						newRef.TargetsRS.Insert(0, MyCmObject);
						//then also add the lexical entry that the user selected in the chooser dialog.
						newRef.TargetsRS.Insert(1, first);
					}
				}
				m_refs.Add(newRef);
			});
		}

		/// <summary>
		/// This method is called when we are creating a new lexical relation slice.
		/// If the user selects an item it's hvo is returned.
		/// Otherwise 0 is returned and the lexical relation should not be created.
		/// </summary>
		private ICmObject GetRootObject(ILexRefType lrt)
		{
			ICmObject first = null;
			EntryGoDlg dlg = null;
			try
			{
				switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = true;
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
						dlg = new EntryGoDlg();
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						break;
					default:
						Debug.Assert(lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair || lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtSenseTree);
						return null;
				}
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				Debug.Assert(dlg != null);
				var wp = new WindowParams
				{
					m_title = string.Format(LexiconResources.ksIdentifyXEntry,
					lrt.ReverseName.BestAnalysisAlternative.Text),
					m_btnText = LanguageExplorerResources.ks_Add
				};
				dlg.SetDlgInfo(Cache, wp);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					first = dlg.SelectedObject;
				}
				return first;
			}
			finally
			{
				dlg?.Dispose();
			}
		}

		/// <summary>
		/// This method is called when we are creating a new lexical relation slice.
		/// If the user selects an item it's hvo is returned.
		/// Otherwise 0 is returned and the lexical relation should not be created.
		/// </summary>
		private ICmObject GetChildObject(ILexRefType lrt)
		{
			ICmObject first = null;
			EntryGoDlg dlg = null;
			try
			{
				string sTitle;
				switch ((LexRefTypeTags.MappingTypes)lrt.MappingType)
				{
					case LexRefTypeTags.MappingTypes.kmtEntryOrSensePair:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair:
						// Entry or sense pair with different Forward/Reverse
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = false;
						sTitle = string.Format(LanguageExplorerResources.ksIdentifyXLexEntryOrSense, lrt.Name.BestAnalysisAlternative.Text);
						break;
					case LexRefTypeTags.MappingTypes.kmtSenseCollection:
					case LexRefTypeTags.MappingTypes.kmtSensePair:
					case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
					// Sense pair with different Forward/Reverse names
					case LexRefTypeTags.MappingTypes.kmtSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = true;
						sTitle = string.Format(LanguageExplorerResources.ksIdentifyXSense, lrt.Name.BestAnalysisAlternative.Text);
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryCollection:
					case LexRefTypeTags.MappingTypes.kmtEntryPair:
					case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair:
					case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
					// Entry pair with different Forward/Reverse names
					case LexRefTypeTags.MappingTypes.kmtEntrySequence:
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
						dlg = new EntryGoDlg();
						sTitle = string.Format(LanguageExplorerResources.ksIdentifyXLexEntry, lrt.Name.BestAnalysisAlternative.Text);
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseCollection:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
						dlg = new LinkEntryOrSenseDlg();
						sTitle = string.Format(LanguageExplorerResources.ksIdentifyXLexEntryOrSense, lrt.Name.BestAnalysisAlternative.Text);
						break;
					default:
						Debug.Assert(lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair || lrt.MappingType == (int)LexRefTypeTags.MappingTypes.kmtSenseTree);
						return null;
				}
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				Debug.Assert(dlg != null);
				var wp = new WindowParams
				{
					m_title = sTitle,
					m_btnText = LanguageExplorerResources.ks_Add
				};
				// Don't display the current entry in the list of matching entries.  See LT-2611.
				var objEntry = MyCmObject;
				while (objEntry.ClassID == LexSenseTags.kClassId)
				{
					objEntry = objEntry.Owner;
				}
				Debug.Assert(objEntry.ClassID == LexEntryTags.kClassId);
				dlg.StartingEntry = objEntry as ILexEntry;

				dlg.SetDlgInfo(Cache, wp);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					first = dlg.SelectedObject;
				}
				return first;
			}
			finally
			{
				dlg?.Dispose();
			}
		}

		/// <summary />
		public void HandleMoreMenuItem(object sender, EventArgs ea)
		{
			MessageBoxExManager.Trigger("CreateNewLexicalReferenceType");
			Cache.DomainDataByFlid.BeginUndoTask(LanguageExplorerResources.ksUndoInsertLexRefType, LanguageExplorerResources.ksRedoInsertLexRefType);
			var list = Cache.LanguageProject.LexDbOA.ReferencesOA;
			var newKid = list.Services.GetInstance<ILexRefTypeFactory>().Create();
			list.PossibilitiesOS.Add(newKid);
			Cache.DomainDataByFlid.EndUndoTask();
			LinkHandler.PublishFollowLinkMessage(ContainingDataTree.Publisher, new FwLinkArgs(AreaServices.LexRefEditMachineName, newKid.Guid));
		}

		/// <summary />
		private void ExpandNewNode()
		{
			using (new DataTreeLayoutSuspensionHelper(PropertyTable.GetValue<IFwMainWnd>(FwUtils.window), ContainingDataTree))
			{
				XElement caller = null;
				if (Key.Length > 1)
				{
					caller = Key[Key.Length - 2] as XElement;
				}
				var insPos = IndexInContainer + m_refs.Count;
				GenerateChildNode(m_refs.Count - 1, ConfigurationNode, caller, Indent, ref insPos, new ArrayList(Key), new ObjSeqHashMap());
				Expansion = TreeItemState.ktisExpanded;
			}
		}

		/// <summary>
		/// Expand this node, which is at position iSlice in its parent.
		/// </summary>
		/// <remarks> I (JH) don't know why this was written to take the index of the slice.
		/// It's just as easy for this class to find its own index.</remarks>
		public override void Expand(int iSlice)
		{
			using (new DataTreeLayoutSuspensionHelper(PropertyTable.GetValue<IFwMainWnd>(FwUtils.window), ContainingDataTree))
			{
				XElement caller = null;
				if (Key.Length > 1)
				{
					caller = Key[Key.Length - 2] as XElement;
				}
				var insPos = iSlice + 1;
				GenerateChildren(ConfigurationNode, caller, MyCmObject, Indent, ref insPos, new ArrayList(Key), new ObjSeqHashMap(), false);
				Expansion = TreeItemState.ktisExpanded;
			}
		}

		/// <summary>
		/// This method is called when a user selects Delete Relation on a Lexical Relation slice.
		/// For: sequence relations (eg. Calendar)
		///     collection relations (eg. Synonym)
		///     tree relation (parts/whole when deleting a Whole slice)
		/// </summary>
		public void DeleteFromReference(ILexReference lr)
		{
			if (lr == null)
			{
				throw new FwConfigurationException("Slice:GetObjectHvoForMenusToOperateOn is either messed up or should not have been called, because it could not find the object to be deleted.", ConfigurationNode);
			}
			var mainWindow = PropertyTable.GetValue<Form>(FwUtils.window);
			using (new WaitCursor(mainWindow))
			using (var dlg = new ConfirmDeleteObjectDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
			{
				//We need this to determine which kind of relation we are deleting
				var lrtOwner = (ILexRefType)lr.Owner;
				var analWs = lrtOwner.Services.WritingSystems.DefaultAnalysisWritingSystem.Handle;
				var userWs = Cache.WritingSystemFactory.UserWs;
				var tisb = TsStringUtils.MakeIncStrBldr();
				tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, userWs);
				switch ((LexRefTypeTags.MappingTypes)lrtOwner.MappingType)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtEntrySequence:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseSequence:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseCollection:
					case LexRefTypeTags.MappingTypes.kmtEntryCollection:
					case LexRefTypeTags.MappingTypes.kmtSenseCollection:
						if (lr.TargetsRS.Count > 2)
						{
							tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, userWs);
							tisb.Append(string.Format(LanguageExplorerResources.ksDeleteSequenceCollectionA, StringUtils.kChHardLB.ToString()));
							tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, analWs);
							tisb.Append(lrtOwner.ShortName);
							tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, userWs);
							tisb.Append(LanguageExplorerResources.ksDeleteSequenceCollectionB);

							dlg.SetDlgInfo(lr, Cache, PropertyTable, tisb.GetString());
						}
						else
						{
							dlg.SetDlgInfo(lr, Cache, PropertyTable);
						}
						break;
					default:
						dlg.SetDlgInfo(lr, Cache, PropertyTable);
						break;
				}

				if (DialogResult.Yes == dlg.ShowDialog(mainWindow))
				{
					UndoableUnitOfWorkHelper.Do(LexiconResources.ksUndoDeleteRelation, LexiconResources.ksRedoDeleteRelation, MyCmObject, () =>
					{
						//If the user selected Yes, then we need to delete 'this' sense or entry
						lr.TargetsRS.Remove(MyCmObject);
					});
					//Update the display because we have removed this slice from the Lexical entry.
					UpdateForDelete(lr);
				}
			}
		}

		/// <summary>
		/// This method is called when a user selects Delete Relation on a Lexical Relation slice.
		/// For: Pair relation (eg. Antonym)
		///     tree relation (parts/whole when deleting a Parts slice)
		/// </summary>
		public void DeleteReference(ILexReference lr)
		{
			if (lr == null)
			{
				throw new FwConfigurationException("Slice:GetObjectHvoForMenusToOperateOn is either messed up or should not have been called, because it could not find the object to be deleted.", ConfigurationNode);
			}
			var mainWindow = PropertyTable.GetValue<Form>(FwUtils.window);
			using (new WaitCursor(mainWindow))
			using (var dlg = new ConfirmDeleteObjectDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
			{
				//We need this to determine which kind of relation we are deleting
				var lrtOwner = lr.Owner as ILexRefType;
				var userWs = Cache.WritingSystemFactory.UserWs;
				var tisb = TsStringUtils.MakeIncStrBldr();
				tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, userWs);

				switch ((LexRefTypeTags.MappingTypes)lrtOwner.MappingType)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
					case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
					case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
						tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, userWs);
						tisb.Append(string.Format(LanguageExplorerResources.ksDeleteLexTree, StringUtils.kChHardLB));
						dlg.SetDlgInfo(lr, Cache, PropertyTable, tisb.GetString());
						break;
					default:
						dlg.SetDlgInfo(lr, Cache, PropertyTable);
						break;
				}
				if (DialogResult.Yes == dlg.ShowDialog(mainWindow))
				{
					UndoableUnitOfWorkHelper.Do(LexiconResources.ksUndoDeleteRelation, LexiconResources.ksRedoDeleteRelation, MyCmObject, () =>
					{
						Cache.DomainDataByFlid.DeleteObj(lr.Hvo);
					});
					//Update the display because we have removed this slice from the Lexical entry.
					UpdateForDelete(lr);
				}
			}
		}

		private void UpdateForDelete(ILexReference lr)
		{
			// This slice might get disposed by one of the calling methods.  See FWR-3291.
			if (IsDisposed)
			{
				return;
			}
			m_refs.Remove(lr);
			// if this flickers too annoyingly, we can probably optimize by extracting relevant lines from Collapse.
			Collapse();
			Expand();
		}

		/// <summary>
		/// This method is called when a user selects "Edit Reference Set Details" for a Lexical Relation slice.
		/// </summary>
		public void EditReferenceDetails(ILexReference lr)
		{
			if (lr == null)
			{
				throw new FwConfigurationException("Slice:GetObjectHvoForMenusToOperateOn is either messed up or should not have been called, because it could not find the object to be deleted.", ConfigurationNode);
			}
			using (var dlg = new LexReferenceDetailsDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
			{
				dlg.ReferenceName = lr.Name.AnalysisDefaultWritingSystem.Text;
				dlg.ReferenceComment = lr.Comment.AnalysisDefaultWritingSystem.Text;
				if (dlg.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				using (var helper = new UndoableUnitOfWorkHelper(Cache.ActionHandlerAccessor, LexiconResources.ksUndoEditRefSetDetails, LexiconResources.ksRedoEditRefSetDetails))
				{
					lr.Name.SetAnalysisDefaultWritingSystem(dlg.ReferenceName);
					lr.Comment.SetAnalysisDefaultWritingSystem(dlg.ReferenceComment);
					helper.RollBack = false;
				}
			}
		}

		/// <summary />
		public static SimpleListChooser MakeSenseChooser(LcmCache cache, IHelpTopicProvider helpTopicProvider)
		{
			var chooser = new SimpleListChooser(null, ObjectLabel.CreateObjectLabels(cache, cache.ServiceLocator.GetInstance<ILexSenseRepository>().AllInstances(), "LongNameTSS"), LanguageExplorerResources.ksSenses, helpTopicProvider)
			{
				Cache = cache
			};
			return chooser;
		}
	}
}