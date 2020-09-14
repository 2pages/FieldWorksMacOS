// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LanguageExplorer.Controls.XMLViews;
using LanguageExplorer.DictionaryConfiguration.DictionaryDetailsView;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.DictionaryConfiguration
{
	internal class DictionaryConfigurationController : IFlexComponent
	{
		/// <summary>
		/// The current model being worked with
		/// </summary>
		internal DictionaryConfigurationModel _model;
		/// <summary>
		/// The entry being used for preview purposes
		/// </summary>
		internal ICmObject _previewEntry;
		/// <summary>
		/// Available dictionary configurations (eg stem- and root-based)
		/// </summary>
		internal List<DictionaryConfigurationModel> _dictionaryConfigurations;
		/// <summary>
		/// Directory where configurations of the current type (Dictionary, Reversal, ...) are stored
		/// for the project.
		/// </summary>
		private string _projectConfigDir;
		/// <summary>
		/// Publication decorator necessary to view sense numbers in the preview
		/// </summary>
		private DictionaryPublicationDecorator _allEntriesPublicationDecorator;
		/// <summary>
		/// Directory where shipped default configurations of the current type (Dictionary, Reversal, ...)
		/// are stored.
		/// </summary>
		internal string _defaultConfigDir;
		private bool _isDirty;
		/// <summary>
		/// Flag whether we're highlighting the affected node in the preview area.
		/// </summary>
		private bool _isHighlighted;
		private LcmCache Cache => PropertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);

		/// <summary>
		/// The view to display the model in
		/// </summary>
		internal IDictionaryConfigurationView View { get; set; }

		/// <summary>
		/// Controls the portion of the dialog where an element in a dictionary entry is configured in detail
		/// </summary>
		private DictionaryDetailsController DetailsController { get; set; }

		/// <summary>
		/// Whether any changes have been saved, including changes to the Configs, which Config is the current Config, changes to Styles, etc.,
		/// that require the preview to be updated.
		/// </summary>
		public bool MasterRefreshRequired { get; private set; }

		/// <summary>
		/// Figure out what alternate dictionaries are available (eg root-, stem-, ...)
		/// Populate _dictionaryConfigurations with available models.
		/// Populate view's list of alternate dictionaries with available choices.
		/// </summary>
		private void LoadDictionaryConfigurations()
		{
			_dictionaryConfigurations = DictionaryConfigurationServices.GetDictionaryConfigurationModels(Cache, _defaultConfigDir, _projectConfigDir);
			View.SetChoices(_dictionaryConfigurations);
		}

		/// <summary>
		/// Populate dictionary elements tree, from model.
		/// </summary>
		internal void PopulateTreeView()
		{
			RefreshView();
		}

		/// <summary>
		/// Refresh view from model. Try to select nodeToSelect in the view afterward. If nodeToSelect is null, try to preserve the existing node selection.
		/// </summary>
		private void RefreshView(ConfigurableDictionaryNode nodeToSelect = null)
		{
			var tree = View.TreeControl.Tree;
			var expandedNodes = new List<ConfigurableDictionaryNode>();
			FindExpandedNodes(tree.Nodes, ref expandedNodes);
			ConfigurableDictionaryNode topVisibleNode = null;
			if (tree.TopNode != null)
			{
				topVisibleNode = tree.TopNode.Tag as ConfigurableDictionaryNode;
			}
			if (nodeToSelect == null && tree.SelectedNode != null)
			{
				nodeToSelect = tree.SelectedNode.Tag as ConfigurableDictionaryNode;
			}
			// Rebuild view from model
			tree.Nodes.Clear();
			var rootNodes = _model.Parts;
			CreateTreeOfTreeNodes(null, rootNodes);
			// Preserve treenode expansions
			foreach (var expandedNode in expandedNodes)
			{
				//If an expanded node is removed it is added to the expanedNodes list before
				//the tree is rebuilt. Therefore when tree is rebuilt FindTreeNode returns null since
				//it cannot find that node anymore.
				FindTreeNode(expandedNode, tree.Nodes)?.Expand();
			}
			if (nodeToSelect != null)
			{
				tree.SelectedNode = FindTreeNode(nodeToSelect, tree.Nodes);
			}
			// Fallback to selecting first root, trying to make sure there is always a selection for the buttons to be enabled or disabled with respect to.
			if (tree.SelectedNode == null && tree.Nodes.Count > 0)
			{
				tree.SelectedNode = tree.Nodes[0];
			}
			// Try to prevent scrolling away from what the user was seeing in the tree. But if necessary, scroll so the selected node is visible.
			if (topVisibleNode != null)
			{
				tree.TopNode = FindTreeNode(topVisibleNode, tree.Nodes);
			}
			tree.SelectedNode?.EnsureVisible();
			RefreshPreview();
			DisplayPublicationTypes();
		}

		/// <summary>Refresh the Preview without reloading the entire configuration tree</summary>
		private void RefreshPreview(bool isChangeInDictionaryModel = true)
		{
			if (_previewEntry == null || !_previewEntry.IsValidObject)
			{
				return;
			}
			if (isChangeInDictionaryModel)
			{
				_isDirty = true;
			}
			else
			{
				MasterRefreshRequired = true;
			}
			View.PreviewData = LcmXhtmlGenerator.GenerateEntryHtmlWithStyles(_previewEntry, _model, _allEntriesPublicationDecorator, PropertyTable);
			if (_isHighlighted)
			{
				View.HighlightContent(View.TreeControl.Tree.SelectedNode.Tag as ConfigurableDictionaryNode, Cache.GetManagedMetaDataCache());
			}
		}

		/// <summary>
		/// Populate a list of dictionary nodes that correspond to treenodes that are expanded in the 'treenodes' or its children.
		/// </summary>
		private static void FindExpandedNodes(TreeNodeCollection treenodes, ref List<ConfigurableDictionaryNode> expandedNodes)
		{
			foreach (TreeNode treenode in treenodes)
			{
				if (treenode.IsExpanded)
				{
					expandedNodes.Add(treenode.Tag as ConfigurableDictionaryNode);
				}
				FindExpandedNodes(treenode.Nodes, ref expandedNodes);
			}
		}

		/// <summary>
		/// Create a tree of TreeNodes from a list of nodes and their Children, adding
		/// them into the TreeView parented by the TreeNode corresponding
		/// to parent.
		/// If parent is null, the nodes are added as direct children of the TreeView
		/// </summary>
		internal void CreateTreeOfTreeNodes(ConfigurableDictionaryNode parent, List<ConfigurableDictionaryNode> nodes)
		{
			Guard.AgainstNull(nodes, nameof(nodes));

			foreach (var node in nodes)
			{
				CreateAndAddTreeNodeForNode(parent, node);
				// Configure shared nodes exactly once: under their master parent
				if (!node.IsSubordinateParent && node.ReferencedOrDirectChildren != null)
				{
					CreateTreeOfTreeNodes(node, node.ReferencedOrDirectChildren);
				}
			}
		}

		/// <summary>
		/// Create a TreeNode corresponding to node, and add it to the
		/// TreeView parented by the TreeNode corresponding to parentNode.
		/// If parentNode is null, node is considered to be at the root.
		/// </summary>
		internal void CreateAndAddTreeNodeForNode(ConfigurableDictionaryNode parentNode, ConfigurableDictionaryNode node)
		{
			Guard.AgainstNull(node, nameof(node));

			node.StringTable = StringTable.Table;   // for localization
			var newTreeNode = new TreeNode(node.DisplayLabel) { Tag = node, Checked = node.IsEnabled };
			var treeView = View.TreeControl.Tree;
			if (parentNode == null)
			{
				treeView.Nodes.Add(newTreeNode);
				treeView.TopNode = newTreeNode;
				return;
			}

			FindTreeNode(parentNode, treeView.Nodes)?.Nodes.Add(newTreeNode);
		}

		/// <summary>
		/// FindTreeNode returns the treenode which has the tag that matches nodeToMatch, or null
		/// </summary>
		internal static TreeNode FindTreeNode(ConfigurableDictionaryNode nodeToMatch, TreeNodeCollection treeNodeCollection)
		{
			Guard.AgainstNull(nodeToMatch, nameof(nodeToMatch));
			Guard.AgainstNull(treeNodeCollection, nameof(treeNodeCollection));

			foreach (TreeNode treeNode in treeNodeCollection)
			{
				if (nodeToMatch.Equals(treeNode.Tag))
				{
					return treeNode;
				}
				var branchResult = FindTreeNode(nodeToMatch, treeNode.Nodes);
				if (branchResult != null)
				{
					return branchResult;
				}
			}
			return null;
		}

		/// <summary>
		/// Default constructor to make testing easier.
		/// </summary>
		internal DictionaryConfigurationController()
		{
		}

		/// <summary>
		/// Constructs a DictionaryConfigurationController with a view and a model pulled from user settings
		/// </summary>
		public DictionaryConfigurationController(IDictionaryConfigurationView view, ICmObject previewEntry)
		{
			_previewEntry = previewEntry;
			View = view;
			// NB: Stuff from 'afar' does what is done in InitializeFlexComponent in the new world, so be sure to not merge it in here!
		}

		private void SetManagerTypeInfo(DictionaryConfigurationManagerDlg dialog)
		{
			dialog.HelpTopic = DictionaryConfigurationServices.GetDictionaryConfigurationBaseType(PropertyTable) == LanguageExplorerResources.Dictionary
						? "khtpDictConfigManager"
						: "khtpRevIndexConfigManager";
			if (DictionaryConfigurationServices.GetDictionaryConfigurationBaseType(PropertyTable) == LanguageExplorerResources.ReversalIndex)
			{
				dialog.Text = DictionaryConfigurationStrings.ReversalIndexConfigurationDlgTitle;
				dialog.ConfigurationGroupText = DictionaryConfigurationStrings.DictionaryConfigurationMangager_ReversalConfigurations_GroupLabel;
			}
		}

		public void SelectModelFromManager(DictionaryConfigurationModel model)
		{
			_model = model;
		}

		/// <summary>
		/// Returns a default entry for the given configuration type or null if the cache has no items for that type.
		/// </summary>
		internal static ICmObject GetDefaultEntryForType(string configurationType, LcmCache cache)
		{
			var serviceLocator = cache.ServiceLocator;
			switch (configurationType)
			{
				case "Dictionary":
					{
						var entryRepo = serviceLocator.GetInstance<ILexEntryRepository>().AllInstances().ToList();
						// try to find the first entry with a headword not equal to "???"; otherwise, any entry will have to do.
						return entryRepo.FirstOrDefault(entry => StringServices.DefaultHomographString() != entry.HeadWord.Text) ?? entryRepo.FirstOrDefault();
					}
				case "Reversal Index":
					{
						// TODO pH 2015.07: filter by WS
						var entryRepo = serviceLocator.GetInstance<IReversalIndexEntryRepository>().AllInstances().ToList();
						// try to find the first entry with a headword not equal to "???"; otherwise, any entry will have to do.
						return entryRepo.FirstOrDefault(entry => StringServices.DefaultHomographString() != entry.ReversalForm.BestAnalysisAlternative.Text)
							?? entryRepo.FirstOrDefault() ?? serviceLocator.GetInstance<IReversalIndexEntryFactory>().Create();
					}
				default:
					{
						throw new NotImplementedException($"Default entry for {configurationType} type not implemented.");
					}
			}
		}

		private void LoadLastDictionaryConfiguration()
		{
			var lastUsedConfiguration = DictionaryConfigurationServices.GetCurrentConfiguration(PropertyTable);
			_model = _dictionaryConfigurations.FirstOrDefault(config => config.FilePath == lastUsedConfiguration) ?? _dictionaryConfigurations.First();
		}

		private void SaveModelHandler(object sender, EventArgs e)
		{
			if (_isDirty)
			{
				SaveModel();
			}
		}

		internal void SaveModel()
		{
			foreach (var config in _dictionaryConfigurations)
			{
				config.FilePath = GetProjectConfigLocationForPath(config.FilePath);
				config.Save();
			}
			// This property must be set *after* saving, because the initial save changes the FilePath
			DictionaryConfigurationServices.SetCurrentConfiguration(PropertyTable, _model.FilePath, false);
			MasterRefreshRequired = true;
			_isDirty = false;
		}

		internal string GetProjectConfigLocationForPath(string filePath)
		{
			var projectConfigDir = LcmFileHelper.GetConfigSettingsDir(Cache.ProjectId.ProjectFolder);
			return filePath.StartsWith(projectConfigDir) ? filePath : Path.Combine(projectConfigDir, filePath.Substring(FwDirectoryFinder.DefaultConfigurations.Length + 1));
		}

		/// <summary>
		/// Populate options pane, from model.
		/// </summary>
		private void BuildAndShowOptions(ConfigurableDictionaryNode node)
		{
			if (DetailsController == null)
			{
				DetailsController = new DictionaryDetailsController(new DetailsView(), PropertyTable);
				DetailsController.DetailsModelChanged += (sender, e) => RefreshPreview();
				DetailsController.StylesDialogMadeChanges += (sender, e) =>
				{
					EnsureValidStylesInModel(_model, Cache); // in case the change was a rename or deletion
					RefreshPreview(false);
				};
				DetailsController.SelectedNodeChanged += (sender, e) =>
				{
					if (sender is ConfigurableDictionaryNode configurableDictionaryNode)
					{
						View.TreeControl.Tree.SelectedNode = FindTreeNode(configurableDictionaryNode, View.TreeControl.Tree.Nodes);
					}
				};
			}
			DetailsController.LoadNode(_model, node);
			View.DetailsView = DetailsController.View;
		}

		/// <summary>
		/// Whether node can be moved among its siblings, or if it can be moved out of a grouping node.
		/// </summary>
		public static bool CanReorder(ConfigurableDictionaryNode node, Direction direction)
		{
			Guard.AgainstNull(node, nameof(node));

			var parent = node.Parent;
			// Root nodes can't be moved
			if (parent == null)
			{
				return false;
			}
			var nodeIndex = parent.Children.IndexOf(node);
			return (direction != Direction.Up || nodeIndex != 0 || parent.DictionaryNodeOptions is DictionaryNodeGroupingOptions)
				   && (direction != Direction.Down || nodeIndex != parent.Children.Count - 1 || parent.DictionaryNodeOptions is DictionaryNodeGroupingOptions);
		}

		/// <summary>
		/// Display the list of publications configured by the current dictionary configuration.
		/// </summary>
		private void DisplayPublicationTypes()
		{
			View.ShowPublicationsForConfiguration(AffectedPublications);
		}

		/// <summary>
		/// Friendly display string listing the publications affected by making changes to the current dictionary configuration.
		/// </summary>
		public string AffectedPublications
		{
			get
			{
				if (_model.AllPublications)
				{
					return DictionaryConfigurationStrings.Allpublications;
				}
				var strbldr = new StringBuilder();
				if (_model.Publications == null || !_model.Publications.Any())
				{
					return DictionaryConfigurationStrings.ksNone1;
				}
				foreach (var pubType in _model.Publications)
				{
					strbldr.AppendFormat("{0}, ", pubType);
				}
				var str = strbldr.ToString();
				return str.Substring(0, str.Length - 2);
			}
		}

		private void SelectCurrentConfigurationAndRefresh()
		{
			View.SelectConfiguration(_model);
			// if the model has no homograph configurations saved then fill it with a default version
			if (_model.HomographConfiguration == null)
			{
				_model.HomographConfiguration = new DictionaryHomographConfiguration(new HomographConfiguration());
			}
			_model.HomographConfiguration.ExportToHomographConfiguration(Cache.ServiceLocator.GetInstance<HomographConfiguration>());
			RefreshView(); // REVIEW pH 2016.02: this is called only in ctor and after ManageViews. do we even want to refresh and set isDirty?
		}

		/// <summary>
		/// Move a node among its siblings in the model, and cause the view to update accordingly.
		/// </summary>
		public void Reorder(ConfigurableDictionaryNode node, Direction direction)
		{
			Guard.AgainstNull(node, nameof(node));
			if (!CanReorder(node, direction))
			{
				throw new ArgumentOutOfRangeException();
			}

			var parent = node.Parent;
			var nodeIndex = parent.Children.IndexOf(node);
			// For Direction.Up
			var newNodeIndex = nodeIndex - 1;
			// or Down
			if (direction == Direction.Down)
			{
				newNodeIndex = nodeIndex + 1;
			}
			var movingOutOfGroup = (newNodeIndex == -1 || newNodeIndex >= parent.Children.Count) && parent.DictionaryNodeOptions is DictionaryNodeGroupingOptions;
			if (movingOutOfGroup)
			{
				MoveNodeOutOfGroup(node, direction, parent, nodeIndex);
			}
			else if (parent.Children[newNodeIndex].DictionaryNodeOptions is DictionaryNodeGroupingOptions && !(node.DictionaryNodeOptions is DictionaryNodeGroupingOptions))
			{
				MoveNodeIntoGroup(node, direction, parent, newNodeIndex, nodeIndex);
			}
			else
			{
				parent.Children.RemoveAt(nodeIndex);
				parent.Children.Insert(newNodeIndex, node);
			}
			RefreshView();
		}

		private static void MoveNodeIntoGroup(ConfigurableDictionaryNode node, Direction direction, ConfigurableDictionaryNode parent, int newNodeIndex, int nodeIndex)
		{
			var targetGroupNode = parent.Children[newNodeIndex];
			parent.Children.RemoveAt(nodeIndex);
			if (targetGroupNode.Children == null)
			{
				targetGroupNode.Children = new List<ConfigurableDictionaryNode>();
			}
			if (direction == Direction.Up)
			{
				targetGroupNode.Children.Add(node);
			}
			else
			{
				targetGroupNode.Children.Insert(0, node);
			}
			node.Parent = targetGroupNode;
		}

		private static void MoveNodeOutOfGroup(ConfigurableDictionaryNode node, Direction direction, ConfigurableDictionaryNode parent, int nodeIndex)
		{
			parent.Children.RemoveAt(nodeIndex);
			var indexOfParentGroup = parent.Parent.Children.IndexOf(parent);
			if (direction == Direction.Down)
			{
				parent.Parent.Children.Insert(indexOfParentGroup + 1, node);
			}
			else
			{
				parent.Parent.Children.Insert(indexOfParentGroup, node);
			}
			node.Parent = parent.Parent;
		}

		/// <summary>
		/// Link this node to a SharedItem to use its children. Returns true if this node is the first (Master) parent; false otherwise
		/// </summary>
		public static bool LinkReferencedNode(List<ConfigurableDictionaryNode> sharedItems, ConfigurableDictionaryNode node, string referenceItem)
		{
			node.ReferencedNode = sharedItems.FirstOrDefault(si => si.Label == referenceItem && si.FieldDescription == node.FieldDescription && si.SubField == node.SubField);
			if (node.ReferencedNode == null)
			{
				throw new KeyNotFoundException($"Could not find Referenced Node named {referenceItem} for field {node.FieldDescription}.{node.SubField}");
			}
			node.ReferenceItem = referenceItem;
			node.ReferencedNode.IsEnabled = true;
			if (node.ReferencedNode.Parent != null)
			{
				return false;
			}
			node.ReferencedNode.Parent = node;
			return true;
		}

		/// <summary>
		/// Allow other nodes to reference this node's children
		/// </summary>
		public static void ShareNodeAsReference(List<ConfigurableDictionaryNode> sharedItems, ConfigurableDictionaryNode node, string cssClass = null)
		{
			if (node.ReferencedNode != null)
			{
				throw new InvalidOperationException($"Node {DictionaryConfigurationServices.BuildPathStringFromNode(node)} is already shared as {node.ReferenceItem ?? node.ReferencedNode.Label}");
			}
			if (node.Children == null || !node.Children.Any())
			{
				return; // no point sharing Children there aren't any
			}
			var dupItem = sharedItems.FirstOrDefault(item => item.FieldDescription == node.FieldDescription && item.SubField == node.SubField);
			if (dupItem != null)
			{
				var fullField = string.IsNullOrEmpty(node.SubField) ? node.FieldDescription : $"{node.FieldDescription}.{node.SubField}";
				MessageBoxUtils.Show(string.Format(DictionaryConfigurationStrings.InadvisableToShare, node.DisplayLabel, fullField, DictionaryConfigurationServices.BuildPathStringFromNode(dupItem.Parent)));
				return;
			}
			// ENHANCE (Hasso) 2016.03: enforce that the specified node is part of *this* model (incl shared items)
			var key = string.IsNullOrEmpty(node.ReferenceItem) ? $"Shared{node.Label}" : node.ReferenceItem;
			cssClass = string.IsNullOrEmpty(cssClass) ? $"shared{CssGenerator.GetClassAttributeForConfig(node)}" : cssClass.ToLowerInvariant();
			// Ensure the shared node's Label and CSSClassNameOverride are both unique within this Configuration
			if (sharedItems.Any(item => item.Label == key || item.CSSClassNameOverride == cssClass))
			{
				throw new ArgumentException($"A SharedItem already exists with the Label '{key}' or the class '{cssClass}'");
			}
			var sharedItem = new ConfigurableDictionaryNode
			{
				Label = key,
				CSSClassNameOverride = cssClass,
				FieldDescription = node.FieldDescription,
				SubField = node.SubField,
				Parent = node,
				Children = node.Children, // ENHANCE (Hasso) 2016.03: deep-clone so that unshared changes are not lost? Or only on share-with?
				IsEnabled = true // shared items are always enabled (for configurability)
			};
			foreach (var child in sharedItem.Children)
			{
				child.Parent = sharedItem;
			}
			sharedItems.Add(sharedItem);
			node.ReferenceItem = key;
			node.ReferencedNode = sharedItem;
			// For now, we expect that nodes have ReferencedChildren NAND direct Children.
			node.Children = null;
			// ENHANCE pH 2016.04: if we ever allow nodes to have both Referenced and direct Children, all DC-model-sync code will need to change.
		}

		#region ModelSynchronization
		public static void MergeTypesIntoDictionaryModel(DictionaryConfigurationModel model, LcmCache cache)
		{
			var complexTypes = new HashSet<Guid>();
			foreach (var pos in cache.LangProject.LexDbOA.ComplexEntryTypesOA.ReallyReallyAllPossibilities)
			{
				complexTypes.Add(pos.Guid);
			}
			complexTypes.Add(XmlViewsUtils.GetGuidForUnspecifiedComplexFormType());
			var variantTypes = new HashSet<Guid>();
			foreach (var pos in cache.LangProject.LexDbOA.VariantEntryTypesOA.ReallyReallyAllPossibilities)
			{
				variantTypes.Add(pos.Guid);
			}
			variantTypes.Add(XmlViewsUtils.GetGuidForUnspecifiedVariantType());
			var referenceTypes = new HashSet<Guid>();
			if (cache.LangProject.LexDbOA.ReferencesOA != null)
			{
				foreach (var pos in cache.LangProject.LexDbOA.ReferencesOA.PossibilitiesOS)
				{
					referenceTypes.Add(pos.Guid);
				}
			}
			var noteTypes = new HashSet<Guid>();
			if (cache.LangProject.LexDbOA.ExtendedNoteTypesOA != null)
			{
				noteTypes = new HashSet<Guid>(cache.LangProject.LexDbOA.ExtendedNoteTypesOA.ReallyReallyAllPossibilities.Select(pos => pos.Guid))
				{
					XmlViewsUtils.GetGuidForUnspecifiedExtendedNoteType()
				};
			}
			foreach (var part in model.PartsAndSharedItems)
			{
				FixTypeListOnNode(part, complexTypes, variantTypes, referenceTypes, noteTypes, model.IsHybrid, cache);
			}
		}

		private static void FixTypeListOnNode(ConfigurableDictionaryNode node, HashSet<Guid> complexTypes, HashSet<Guid> variantTypes, HashSet<Guid> referenceTypes, HashSet<Guid> noteTypes,
			bool isHybrid, LcmCache cache)
		{
			if (node.DictionaryNodeOptions is DictionaryNodeListOptions listOptions)
			{
				switch (listOptions.ListId)
				{
					case ListIds.None:
						break;
					case ListIds.Complex:
						FixOptionsAccordingToCurrentTypes(listOptions.Options, complexTypes, node, false, cache);
						break;
					case ListIds.Variant:
						FixOptionsAccordingToCurrentTypes(listOptions.Options, variantTypes, node,
							IsFilteringInflectionalVariantTypes(node, isHybrid), cache);
						break;
					case ListIds.Entry:
						FixOptionsAccordingToCurrentTypes(listOptions.Options, referenceTypes, node, false, cache);
						break;
					case ListIds.Sense:
						FixOptionsAccordingToCurrentTypes(listOptions.Options, referenceTypes, node, false, cache);
						break;
					case ListIds.Minor:
						Guid[] complexAndVariant = complexTypes.Union(variantTypes).ToArray();
						FixOptionsAccordingToCurrentTypes(listOptions.Options, complexAndVariant, node, false, cache);
						break;
					case ListIds.Note:
						FixOptionsAccordingToCurrentTypes(listOptions.Options, noteTypes, node, false, cache);
						break;
					default:
						System.Diagnostics.Debug.Fail($"Unhandled List Type: {listOptions.ListId}");
						break;
				}
			}
			//Recurse into child nodes and fix the type lists on them
			if (node.Children != null)
			{
				foreach (var child in node.Children)
				{
					FixTypeListOnNode(child, complexTypes, variantTypes, referenceTypes, noteTypes, isHybrid, cache);
				}
			}
		}

		/// <summary>Called on nodes with Variant options to determine whether they are sharing Variants with a sibling</summary>
		private static bool IsFilteringInflectionalVariantTypes(ConfigurableDictionaryNode node, bool isHybrid)
		{
			if (!isHybrid)
			{
				return false;
			}
			if (node.IsDuplicate)
			{
				return true;
			}
			var siblings = node.ReallyReallyAllSiblings;
			// check whether this node has a duplicate, most likely "Variants (Inflectional Variants)"
			return siblings != null && siblings.Any(sib => sib.FieldDescription == node.FieldDescription);
		}

		private static void FixOptionsAccordingToCurrentTypes(List<DictionaryNodeOption> options, ICollection<Guid> possibilities, ConfigurableDictionaryNode node,
			bool filterInflectionalVariantTypes, LcmCache cache)
		{
			var isDuplicate = node.IsDuplicate;
			var currentGuids = new HashSet<Guid>();
			foreach (var opt in options)
			{
				if (Guid.TryParse(opt.Id, out var guid))    // can be empty string
				{
					currentGuids.Add(guid);
				}
			}
			if (filterInflectionalVariantTypes)
			{
				foreach (var custVariantType in possibilities.Where(type => !currentGuids.Contains(type)))
				{
					//Variants without any type are not Inflectional
					var showCustomVariant = (custVariantType != XmlViewsUtils.GetGuidForUnspecifiedVariantType() && cache.ServiceLocator.GetObject(custVariantType) is ILexEntryInflType) ^ !isDuplicate;
					// add new custom variant types disabled for the original and enabled for the inflectional variants copy
					options.Add(new DictionaryNodeOption
					{
						Id = custVariantType.ToString(),
						IsEnabled = showCustomVariant
					});
				}
			}
			else
			{
				// add types that do not exist already
				foreach (var pos in possibilities)
				{
					if (options.Any(x => x.Id == $"{pos}:f" || x.Id == $"{pos}:r"))
					{
						continue;
					}
					var lexRelType = (ILexRefType)cache.LangProject.LexDbOA.ReferencesOA?.ReallyReallyAllPossibilities.FirstOrDefault(x => x.Guid == pos);
					if (lexRelType != null)
					{
						if (LexRefTypeTags.IsAsymmetric((LexRefTypeTags.MappingTypes)lexRelType.MappingType))
						{
							options.Add(new DictionaryNodeOption
							{
								Id = $"{pos}:f",
								IsEnabled = !isDuplicate
							});
							options.Add(new DictionaryNodeOption
							{
								Id = $"{pos}:r",
								IsEnabled = !isDuplicate
							});
						}
						else if (!currentGuids.Contains(pos))
						{
							options.Add(new DictionaryNodeOption
							{
								Id = pos.ToString(),
								IsEnabled = !isDuplicate
							});
						}
					}
					else if (!currentGuids.Contains(pos))
					{
						options.Add(new DictionaryNodeOption
						{
							Id = pos.ToString(),
							IsEnabled = !isDuplicate
						});
					}
				}
			}
			// remove options that no longer exist
			for (var i = options.Count - 1; i >= 0; --i)
			{
				// Truncate any :r or :f from the end of the guid
				var isValidGuid = Guid.TryParse(options[i].Id.Substring(0, Math.Min(options[i].Id.Length, 36)), out var guid);
				if (!isValidGuid || !possibilities.Contains(guid))
				{
					options.RemoveAt(i); //Guid was invalid, or not present in the current possibilities
				}
			}
		}

		public static void EnsureValidStylesInModel(DictionaryConfigurationModel model, LcmCache cache)
		{
			var styles = cache.LangProject.StylesOC.ToDictionary(style => style.Name);
			foreach (var part in model.PartsAndSharedItems)
			{
				if (part.IsMainEntry && string.IsNullOrEmpty(part.Style))
				{
					part.Style = "Dictionary-Normal";
				}
				EnsureValidStylesInConfigNodes(part, styles);
			}
		}

		public static void EnsureValidNumberingStylesInModel(IEnumerable<ConfigurableDictionaryNode> nodes)
		{
			DictionaryConfigurationServices.PerformActionOnNodes(nodes, n =>
			{
				if (n.DictionaryNodeOptions is DictionaryNodeSenseOptions options && options.NumberingStyle == "%O")
				{
					options.NumberingStyle = "%d";
				}
			});
		}

		public static void UpdateWritingSystemInModel(DictionaryConfigurationModel model, LcmCache cache)
		{
			foreach (var part in model.PartsAndSharedItems)
			{
				UpdateWritingSystemInConfigNodes(part, cache);
			}
		}

		private static void UpdateWritingSystemInConfigNodes(ConfigurableDictionaryNode node, LcmCache cache)
		{
			if (node.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions options)
			{
				UpdateWsOptions(options, cache);
			}
			if (node.Children == null)
			{
				return;
			}
			foreach (var child in node.Children)
			{
				UpdateWritingSystemInConfigNodes(child, cache);
			}
		}

		public static string GetWsDefaultName(string wsType)
		{
			switch (wsType)
			{
				case "analysis":
					return LanguageExplorerResources.ksDefaultAnalysis;
				case "vernacular":
					return LanguageExplorerResources.ksDefaultVernacular;
				case "pronunciation":
					return LanguageExplorerResources.ksDefaultPronunciation;
				case "reversal":
					return LanguageExplorerResources.ksCurrentReversal;
				case "analysis vernacular":
					return DictionaryConfigurationStrings.ksBestAnalOrVern;
				default:    // "vernacular analysis"
					return DictionaryConfigurationStrings.ksBestVernOrAnal;
			}
		}

		public static List<ListViewItem> LoadAvailableWsList(DictionaryNodeWritingSystemOptions wsOptions, LcmCache cache)
		{
			var wsLists = UpdateWsOptions(wsOptions, cache);
			// REVIEW (Hasso) 2017.04: most of this method is redundant to UpdateWsOptions; however, it's too risky to remove right before a release.
			var availableWSs = new List<ListViewItem>();
			foreach (var wsListItem in wsLists)
			{
				if (int.TryParse(wsListItem.Id, out var magicId))
				{
					var wsName = WritingSystemServices.GetMagicWsNameFromId(magicId);
					availableWSs.Add(new ListViewItem(GetWsDefaultName(wsName)) { Tag = magicId });
				}
				else
				{
					var ws = cache.WritingSystemFactory.get_Engine(wsListItem.Id);
					availableWSs.Add(new ListViewItem(((CoreWritingSystemDefinition)ws).DisplayLabel) { Tag = ws.Id });
				}
			}
			// Find and add available and selected Writing Systems
			var selectedWSs = wsOptions.Options.Where(ws => ws.IsEnabled).ToList();
			var atLeastOneWsChecked = false;
			// Check if the default WS is selected (it will be the one and only)
			if (selectedWSs.Count == 1)
			{
				var selectedWsDefaultId = WritingSystemServices.GetMagicWsIdFromName(selectedWSs[0].Id);
				if (selectedWsDefaultId < 0)
				{
					var defaultWsItem = availableWSs.FirstOrDefault(item => item.Tag.Equals(selectedWsDefaultId));
					if (defaultWsItem != null)
					{
						defaultWsItem.Checked = true;
						atLeastOneWsChecked = true;
					}
				}
			}
			if (!atLeastOneWsChecked) // we have not checked at least one WS in availableWSs--yet
			{
				// Insert checked named WS's in their saved order, after the Default WS (2 Default WS's if Type is Both)
				var insertionIdx = wsOptions.WsType == WritingSystemType.Both ? 2 : 1;
				foreach (var selectedItem in selectedWSs.Select(ws => availableWSs.FirstOrDefault(item => ws.Id.Equals(item.Tag))).Where(selectedItem => selectedItem != null && availableWSs.Remove(selectedItem)))
				{
					selectedItem.Checked = true;
					availableWSs.Insert(insertionIdx++, selectedItem);
					atLeastOneWsChecked = true;
				}
			}
			// If we still haven't checked one, check the first default (the previously-checked WS was removed)
			if (!atLeastOneWsChecked)
			{
				availableWSs[0].Checked = true;
			}
			return availableWSs;
		}

		/// <summary>Check for added or removed Writing Systems. Doesn't touch Magic WS's, which never change.</summary>
		public static List<DictionaryNodeOption> UpdateWsOptions(DictionaryNodeWritingSystemOptions wsOptions, LcmCache cache)
		{
			var availableWSs = GetCurrentWritingSystems(wsOptions.WsType, cache);
			// Add any new WS's to the end of the list
			wsOptions.Options.AddRange(availableWSs.Where(availWs => !int.TryParse(availWs.Id, out _) && wsOptions.Options.All(opt => opt.Id != availWs.Id)));
			// Remove any WS's that are no longer available in the project
			for (var i = wsOptions.Options.Count - 1; i >= 0; --i)
			{
				if (availableWSs.All(opt => opt.Id != wsOptions.Options[i].Id) && WritingSystemServices.GetMagicWsIdFromName(wsOptions.Options[i].Id) == 0)
				{
					wsOptions.Options.RemoveAt(i);
				}
			}
			// ensure at least one is enabled (default to the first, which is always Magic)
			if (wsOptions.Options.All(o => !o.IsEnabled))
			{
				wsOptions.Options[0].IsEnabled = true;
			}
			return availableWSs;
		}

		/// <summary>
		/// Return the current writing systems for a given writing system type as a list of DictionaryNodeOption objects
		/// </summary>
		public static List<DictionaryNodeOption> GetCurrentWritingSystems(WritingSystemType wsType, LcmCache cache)
		{
			var wsList = new List<DictionaryNodeOption>();
			switch (wsType)
			{
				case WritingSystemType.Vernacular:
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsVern.ToString() });
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					break;
				case WritingSystemType.Analysis:
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsAnal.ToString() });
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					break;
				case WritingSystemType.Both:
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsVern.ToString() });
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsAnal.ToString() });
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					break;
				case WritingSystemType.Pronunciation:
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsPronunciation.ToString() });
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentPronunciationWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					break;
				case WritingSystemType.Reversal:
					wsList.Add(new DictionaryNodeOption { Id = WritingSystemServices.kwsReversalIndex.ToString() });
					wsList.AddRange(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems.Select(ws => new DictionaryNodeOption { Id = ws.Id }));
					break;
			}
			return wsList;
		}

		private static void EnsureValidStylesInConfigNodes(ConfigurableDictionaryNode node, Dictionary<string, IStStyle> styles)
		{
			if (!string.IsNullOrEmpty(node.Style) && !styles.ContainsKey(node.Style))
			{
				node.Style = null;
			}
			if (node.DictionaryNodeOptions != null)
			{
				EnsureValidStylesInNodeOptions(node, styles);
			}
			if (node.Children == null)
			{
				return;
			}
			foreach (var child in node.Children)
			{
				EnsureValidStylesInConfigNodes(child, styles);
			}
		}

		private static void EnsureValidStylesInNodeOptions(ConfigurableDictionaryNode node, Dictionary<string, IStStyle> styles)
		{
			var options = node.DictionaryNodeOptions;
			if (options is DictionaryNodeSenseOptions senseOptions)
			{
				if (!string.IsNullOrEmpty(senseOptions.NumberStyle) && !styles.ContainsKey(senseOptions.NumberStyle))
				{
					senseOptions.NumberStyle = null;
				}
				return;
			}
			var nodeStyle = node.Style;
			if (options is IParaOption paraOptions && !string.IsNullOrEmpty(nodeStyle))
			{
				// Everywhere else we're deleting styles from nodes if the styles dictionary doesn't contain it.
				// Do the same here.
				if (!styles.ContainsKey(nodeStyle))
				{
					node.Style = null;
					return;
				}
				if (paraOptions.DisplayEachInAParagraph)
				{
					node.StyleType = StyleTypes.Paragraph;
					if (!IsParagraphStyle(nodeStyle, styles))
					{
						node.Style = null;
					}
				}
				else
				{
					node.StyleType = StyleTypes.Character;
					if (IsParagraphStyle(nodeStyle, styles))
					{
						node.Style = null;
					}
				}
			}
		}

		private static bool IsParagraphStyle(string styleName, Dictionary<string, IStStyle> styles)
		{
			return styles[styleName].Type == StyleType.kstParagraph;
		}

		public static List<string> GetAllPublications(LcmCache cache)
		{
			return cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.Select(p => p.Name.BestAnalysisAlternative.Text).ToList();
		}

		public static void FilterInvalidPublicationsFromModel(DictionaryConfigurationModel model, LcmCache cache)
		{
			if (model.Publications == null || !model.Publications.Any())
			{
				return;
			}
			var allPossibilities = cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.ToList();
			var allPossiblePublicationsInAllWs = new HashSet<string>();
			foreach (var possibility in allPossibilities)
			{
				foreach (var ws in cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems.Handles())
				{
					allPossiblePublicationsInAllWs.Add(possibility.Name.get_String(ws).Text);
				}
			}
			model.Publications = model.Publications.Where(allPossiblePublicationsInAllWs.Contains).ToList();
		}

		public static void MergeCustomFieldsIntoDictionaryModel(DictionaryConfigurationModel model, LcmCache cache)
		{
			// Detect a bad configuration file and report it in an intelligable way. We generated bad configs before the migration code was cleaned up
			// This is only expected to happen to our testers, we don't need to recover, just inform the testers.
			var badPart = model.Parts.FirstOrDefault(part => part.FieldDescription == null);
			if (badPart != null)
			{
				throw new ApplicationException($"{model.FilePath} is corrupt. {badPart.Label} has no FieldDescription. Deleting this configuration file may fix the problem.");
			}
			MergeCustomFieldsIntoDictionaryModel(cache, model.PartsAndSharedItems);
		}

		/// <summary>
		/// This helper method is used to recurse into all of the configuration nodes in a DictionaryModel and merge the custom fields
		/// in each ConfigurableDictionaryNode with those defined in the FieldWorks model according to the metadata cache.
		/// </summary>
		private static void MergeCustomFieldsIntoDictionaryModel(LcmCache cache, IEnumerable<ConfigurableDictionaryNode> configurationList)
		{
			if (configurationList == null)
			{
				return;
			}
			// Gather up the custom fields and map them by type name
			var classToCustomFields = BuildCustomFieldMap(cache);
			// Custom fields don't need to have their children merged; skip those
			foreach (var configNode in configurationList.Where(node => !node.IsCustomField))
			{
				var lookupClass = GetLookupClassForCustomFieldParent(configNode, cache);
				if (lookupClass != null)
				{
					var fieldsForType = GetCustomFieldsForType(cache, lookupClass, classToCustomFields);
					MergeCustomFieldLists(configNode, fieldsForType);
				}
				// recurse into the rest of the dictionary model
				MergeCustomFieldsIntoDictionaryModel(cache, configNode.Children);
			}
		}

		public static string GetLookupClassForCustomFieldParent(ConfigurableDictionaryNode parent, LcmCache cache)
		{
			// The class that contains the type information for the field we are inspecting
			var lookupClass = ConfiguredLcmGenerator.GetTypeForConfigurationNode(parent, cache.GetManagedMetaDataCache(), out _);
			// If the node describes a collection we may want to add the custom field node if the collection is of
			// the type that the field is added to. (e.g. Senses, ExampleSentences)
			if (ConfiguredLcmGenerator.GetPropertyTypeForConfigurationNode(parent, cache.GetManagedMetaDataCache()) == PropertyType.CollectionType)
			{
				if (lookupClass.IsGenericType)
				{
					lookupClass = lookupClass.GetGenericArguments()[0];
				}
			}
			return lookupClass == null ? null : lookupClass.Name;
		}

		/// <summary>
		/// This method will generate a mapping between the class name (and interface name)
		/// and each custom field in the model associated with that class.
		/// </summary>
		public static Dictionary<string, List<int>> BuildCustomFieldMap(LcmCache cache)
		{
			var metaDataCache = cache.GetManagedMetaDataCache();
			var classToCustomFields = new Dictionary<string, List<int>>();
			foreach (var customFieldId in metaDataCache.GetFieldIds().Where(metaDataCache.IsCustom))
			{
				var cfOwnerClassName = metaDataCache.GetOwnClsName(customFieldId);
				// Also generate a mapping for the corresponding LCM interface (metadata does not contain this)
				// Map the class name and then the interface name to the custom field id list
				if (classToCustomFields.ContainsKey(cfOwnerClassName))
				{
					classToCustomFields[cfOwnerClassName].Add(customFieldId);
				}
				else
				{
					classToCustomFields[cfOwnerClassName.Insert(0, "I")] = classToCustomFields[cfOwnerClassName] = new List<int> { customFieldId };
					if (cfOwnerClassName == "LexEntry")
					{
						classToCustomFields["ILexEntryRef"] = classToCustomFields["LexEntryRef"] = classToCustomFields["LexEntry"];
					}
				}
			}
			var senseOrEntryFields = new List<int>();
			if (classToCustomFields.ContainsKey("LexSense"))
			{
				senseOrEntryFields.AddRange(classToCustomFields["LexSense"]);
			}
			if (classToCustomFields.ContainsKey("LexEntry"))
			{
				senseOrEntryFields.AddRange(classToCustomFields["LexEntry"]);
			}
			if (senseOrEntryFields.Any())
			{
				classToCustomFields["SenseOrEntry"] = classToCustomFields["ISenseOrEntry"] = senseOrEntryFields;
			}
			return classToCustomFields;
		}

		private static void MergeCustomFieldLists(ConfigurableDictionaryNode parent, List<ConfigurableDictionaryNode> customFieldNodes)
		{
			// If parent has Referenced Children, return; fields will be merged under the Shared Item.
			// If the node is set to hide the custom fields then we will not merge the nodes (but we continue to recurse to its children)
			if (parent.ReferenceItem != null || parent.HideCustomFields)
			{
				return;
			}

			// Set the parent on the customFieldNodes (needed for Contains and to make any new fields valid when added)
			foreach (var customField in customFieldNodes)
			{
				customField.Parent = parent;
			}
			if (parent.Children == null)
			{
				parent.Children = new List<ConfigurableDictionaryNode>();
			}
			else
			{
				MergeCustomFieldLists(parent.Children, customFieldNodes);
				// If we have children, through the children and grouped children, removing any custom fields that no longer exist.
				foreach (var group in parent.Children.Where(child => child.DictionaryNodeOptions is DictionaryNodeGroupingOptions && child.Children != null))
				{
					// Set the parent on the customFieldNodes (for Contains)
					foreach (var customField in customFieldNodes)
					{
						customField.Parent = group;
					}
					MergeCustomFieldLists(group.Children, customFieldNodes);
				}
				// Set the parent back on the customFieldNodes (for when new fields are added)
				foreach (var customField in customFieldNodes)
				{
					customField.Parent = parent;
				}
			}

			// Add any custom fields that didn't already exist in the children (at the end).
			parent.Children.AddRange(customFieldNodes);
		}

		private static void MergeCustomFieldLists(List<ConfigurableDictionaryNode> existingNodes, List<ConfigurableDictionaryNode> customFieldNodes)
		{
			// Traverse through the existing nodes from end to beginning, removing any custom fields that no longer exist.
			for (var i = existingNodes.Count - 1; i >= 0; --i)
			{
				var configNode = existingNodes[i];
				if (!configNode.IsCustomField)
				{
					continue;
				}
				if (customFieldNodes.All(k => k.Label != configNode.Label))
				{
					existingNodes.Remove(configNode); // field no longer exists
				}
				else
				{
					customFieldNodes.Remove(configNode); // field found
				}
			}
		}

		/// <summary>
		/// Generate a list of ConfigurableDictionaryNode objects to represent each custom field of the given type.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="className"></param>
		/// <param name="customFieldMap">existing custom field map for performance, method will build one if none given</param>
		public static List<ConfigurableDictionaryNode> GetCustomFieldsForType(LcmCache cache, string className, Dictionary<string, List<int>> customFieldMap = null)
		{
			customFieldMap = customFieldMap ?? BuildCustomFieldMap(cache);
			if (!customFieldMap.ContainsKey(className))
			{
				return new List<ConfigurableDictionaryNode>();
			}
			var customFieldList = new List<ConfigurableDictionaryNode>();
			var metaDataCache = cache.GetManagedMetaDataCache();
			var isEntryRefType = className.EndsWith("EntryRef");
			foreach (var field in customFieldMap[className])
			{
				var configNode = new ConfigurableDictionaryNode
				{
					Label = metaDataCache.GetFieldLabel(field),
					IsCustomField = true,
					Before = " ",
					IsEnabled = false,
					// Custom fields in the Map under LexEntryRef are actually LexEntry CustomFields; look for them under OwningEntry
					FieldDescription = isEntryRefType ? "OwningEntry" : metaDataCache.GetFieldName(field),
					SubField = isEntryRefType ? metaDataCache.GetFieldName(field) : null,
					DictionaryNodeOptions = BuildOptionsForType(metaDataCache, field)
				};
				var listId = metaDataCache.GetFieldListRoot(field);
				if (listId != Guid.Empty)
				{
					AddFieldsForPossibilityList(configNode);
				}
				customFieldList.Add(configNode);
			}
			return customFieldList;
		}

		/// <summary>
		/// Add configuration nodes for all properties that we want to enable a user to display for a custom
		/// PossibilityList field. (Currently Name and Abbreviation)
		/// </summary>
		/// <remarks>
		/// We need this for migrating configurations of custom fields as well as for creating a configuration
		/// from scratch for a new custom field.
		/// </remarks>
		internal static void AddFieldsForPossibilityList(ConfigurableDictionaryNode configNode)
		{
			configNode.Children = new List<ConfigurableDictionaryNode>
			{
				new ConfigurableDictionaryNode
				{
					Label = "Name",
					FieldDescription = "Name",
					DictionaryNodeOptions = BuildWsOptionsForWsType("analysis"),
					Parent = configNode,
					IsCustomField = false // the parent node may be for a custom field, but this node is for a standard CmPossibility field
				},
				new ConfigurableDictionaryNode
				{
					Label = "Abbreviation",
					FieldDescription = "Abbreviation",
					DictionaryNodeOptions = BuildWsOptionsForWsType("analysis"),
					Parent = configNode,
					IsCustomField = false // the parent node may be for a custom field, but this node is for a standard CmPossibility field
				}
			};
		}

		private static DictionaryNodeOptions BuildOptionsForType(IFwMetaDataCacheManaged metaDataCache, int fieldId)
		{
			var fieldType = metaDataCache.GetFieldType(fieldId);
			switch (fieldType)
			{
				case (int)CellarPropertyType.MultiString:
				case (int)CellarPropertyType.MultiUnicode:
					{
						var wsTypeId = WritingSystemServices.GetMagicWsNameFromId(metaDataCache.GetFieldWs(fieldId));
						return BuildWsOptionsForWsType(wsTypeId);
					}
				case (int)CellarPropertyType.OwningCollection:
				case (int)CellarPropertyType.OwningSequence:
				case (int)CellarPropertyType.ReferenceCollection:
				case (int)CellarPropertyType.ReferenceSequence:
					return new DictionaryNodeListOptions();
			}
			return null;
		}

		private static DictionaryNodeOptions BuildWsOptionsForWsType(string wsTypeId)
		{
			return new DictionaryNodeWritingSystemOptions
			{
				WsType = GetWsTypeFromMagicWsName(wsTypeId),
				Options = { new DictionaryNodeOption { Id = wsTypeId, IsEnabled = true } },
			};
		}

		#endregion ModelSynchronization

		public static void EnableNodeAndDescendants(ConfigurableDictionaryNode node)
		{
			SetIsEnabledForSubTree(node, true);
		}

		public static void DisableNodeAndDescendants(ConfigurableDictionaryNode node)
		{
			SetIsEnabledForSubTree(node, false);
		}

		private static void SetIsEnabledForSubTree(ConfigurableDictionaryNode node, bool isEnabled)
		{
			if (node == null)
			{
				return;
			}
			node.IsEnabled = isEnabled;
			if (node.Children != null)
			{
				foreach (var child in node.Children)
				{
					SetIsEnabledForSubTree(child, isEnabled);
				}
			}
		}

		/// <summary>
		/// Search the TreeNode tree to find a starting node based on matching the "class"
		/// attributes of the generated XHTML tracing back from the XHTML element clicked.
		/// If no match is found, SelectedNode is not set.  Otherwise, the best match found
		/// is used to set SelectedNode.
		/// </summary>
		internal void SetStartingNode(List<string> classList)
		{
			if (classList == null || classList.Count == 0)
			{
				return;
			}

			// Search through the configuration trees associated with each top-level TreeNode to find the best match.
			var topNode = View?.TreeControl?.Tree?.Nodes.Cast<TreeNode>().Select(node => node.Tag).OfType<ConfigurableDictionaryNode>()
				.FirstOrDefault(configNode => classList[0].Split(' ').Contains(CssGenerator.GetClassAttributeForConfig(configNode)));
			// If no match is found, give up.
			if (topNode == null)
			{
				return;
			}
			// We have a match, so search through the TreeNode tree to find the TreeNode tagged
			// with the given configuration node.  If found, set that as the SelectedNode.
			classList.RemoveAt(0);
			var startingConfigNode = FindConfigNode(topNode, classList);
			foreach (TreeNode node in View.TreeControl.Tree.Nodes)
			{
				var startingTreeNode = FindMatchingTreeNode(node, startingConfigNode);
				if (startingTreeNode != null)
				{
					View.TreeControl.Tree.SelectedNode = startingTreeNode;
					break;
				}
			}
		}

		/// <summary>
		/// Recursively descend the configuration tree, progressively matching nodes against CSS class path.  Stop
		/// when we run out of both tree and classes.  Classes can be skipped if not matched.  Running out of tree nodes
		/// before running out of classes causes one level of backtracking up the configuration tree to look for a better match.
		/// </summary>
		/// <remarks>LT-17213 Now 'internal static' so DictionaryConfigurationDlg can use it.</remarks>
		internal static ConfigurableDictionaryNode FindConfigNode(ConfigurableDictionaryNode topNode, List<string> classPath)
		{
			if (!classPath.Any())
			{
				return topNode; // what we have already is the best we can find.
			}
			// If we can't go further down the configuration tree, but still have classes to match, back up one level
			// and try matching with the remaining classes.  The configuration tree doesn't always map exactly with
			// the XHTML tree structure.  For instance, in the XHTML, Examples contains instances of Example, each
			// of which contains an instance of Translations, which contains instances of Translation.  In the configuration
			// tree, Examples contains Example and Translations at the same level.
			if (topNode.ReferencedOrDirectChildren == null || topNode.ReferencedOrDirectChildren.Count == 0)
			{
				var match = FindConfigNode(topNode.Parent, classPath);
				return ReferenceEquals(match, topNode.Parent) ? topNode : match;
			}
			ConfigurableDictionaryNode matchingNode = null;
			foreach (var node in topNode.ReferencedOrDirectChildren)
			{
				var cssClass = CssGenerator.GetClassAttributeForConfig(node);
				// LT-17359 a reference node might have "senses mainentrysubsenses"
				if (cssClass == classPath[0].Split(' ')[0])
				{
					matchingNode = node;
					break;
				}
			}
			// If we didn't match, skip this class in the list and try the next class, looking at the same configuration
			// node.  There are classes in the XHTML that aren't represented in the configuration nodes.  ("sensecontent"
			// and "sense" among others)
			if (matchingNode == null)
			{
				matchingNode = topNode;
			}
			classPath.RemoveAt(0);
			return FindConfigNode(matchingNode, classPath);
		}

		/// <summary>
		/// Find the TreeNode that has the given configuration node as its Tag value.  (If there were a
		/// bidirectional link between the two, this method would be unnecessary...)
		/// </summary>
		private static TreeNode FindMatchingTreeNode(TreeNode topNode, ConfigurableDictionaryNode configNode)
		{
			if (ReferenceEquals(topNode.Tag as ConfigurableDictionaryNode, configNode))
			{
				return topNode;
			}
			foreach (TreeNode child in topNode.Nodes)
			{
				var start = FindMatchingTreeNode(child, configNode);
				if (start != null)
				{
					return start;
				}
			}
			return null;
		}

		public static WritingSystemType GetWsTypeFromMagicWsName(string wsType)
		{
			switch (wsType)
			{
				case "best analysis":
				case "all analysis":
				case "analysis":
				case "analysisform": return WritingSystemType.Analysis;
				case "best vernacular":
				case "all vernacular":
				case "vernacular": return WritingSystemType.Vernacular;
				case "vernacular analysis":
				case "analysis vernacular":
				case "best vernoranal":
				case "best analorvern":
				case "vernoranal": return WritingSystemType.Both;
				case "pronunciation": return WritingSystemType.Pronunciation;
				case "reversal": return WritingSystemType.Reversal;
				default: throw new ArgumentException($"Unknown writing system type {wsType}", nameof(wsType));
			}
		}

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
		#endregion

		#region Implementation of IFlexComponent
		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;

			var cache = PropertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
			_allEntriesPublicationDecorator = new DictionaryPublicationDecorator(cache, cache.GetManagedSilDataAccess(), cache.ServiceLocator.GetInstance<Virtuals>().LexDbEntries);
			if (_previewEntry == null)
			{
				_previewEntry = GetDefaultEntryForType(DictionaryConfigurationServices.GetDictionaryConfigurationBaseType(PropertyTable), cache);
			}
			_projectConfigDir = DictionaryConfigurationServices.GetProjectConfigurationDirectory(PropertyTable);
			_defaultConfigDir = DictionaryConfigurationServices.GetDefaultConfigurationDirectory(PropertyTable);
			LoadDictionaryConfigurations();
			LoadLastDictionaryConfiguration();
			PopulateTreeView();
			View.ManageConfigurations += (sender, args) =>
			{
				var currentModel = _model;
				bool managerMadeChanges;
				// show the Configuration Manager dialog
				using (var dialog = new DictionaryConfigurationManagerDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
				{
					var configurationManagerController = new DictionaryConfigurationManagerController(dialog, _dictionaryConfigurations, GetAllPublications(cache), _projectConfigDir, _defaultConfigDir, _model);
					configurationManagerController.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
					configurationManagerController.Finished += SelectModelFromManager;
					configurationManagerController.ConfigurationViewImported += () =>
					{
						SaveModel();
						MasterRefreshRequired = false; // We're reloading the whole app, that's refresh enough
						View.Close();
						Publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.ReloadAreaTools, LanguageExplorerConstants.ListsAreaMachineName));
					};
					SetManagerTypeInfo(dialog);
					dialog.ShowDialog(View as Form);
					managerMadeChanges = configurationManagerController.IsDirty || _model != currentModel;
				}
				// if the manager has not updated anything then we don't need to make any adustments
				if (!managerMadeChanges)
				{
					return;
				}
				// Update our Views
				View.SetChoices(_dictionaryConfigurations);
				SaveModel();
				SelectCurrentConfigurationAndRefresh();
			};
			View.SaveModel += SaveModelHandler;
			View.SwitchConfiguration += (sender, args) =>
			{
				if (_model == args.ConfigurationPicked)
				{
					return;
				}
				_model = args.ConfigurationPicked;
				DictionaryConfigurationServices.SetConfigureHomographParameters(_model, cache.ServiceLocator.GetInstance<HomographConfiguration>());
				RefreshView(); // isChangeInDictionaryModel: true, because we update the current config in the PropertyTable when we save the model.
			};
			View.TreeControl.MoveUp += node => Reorder((ConfigurableDictionaryNode)node.Tag, Direction.Up);
			View.TreeControl.MoveDown += node => Reorder((ConfigurableDictionaryNode)node.Tag, Direction.Down);
			View.TreeControl.Duplicate += node =>
			{
				var dictionaryNode = (ConfigurableDictionaryNode)node.Tag;
				RefreshView(dictionaryNode.DuplicateAmongSiblings(dictionaryNode.Parent == null ? _model.Parts : dictionaryNode.Parent.Children));
			};
			View.TreeControl.Rename += node =>
			{
				var dictionaryNode = (ConfigurableDictionaryNode)node.Tag;
				var siblings = dictionaryNode.Parent == null ? _model.Parts : dictionaryNode.Parent.Children;
				using (var renameDialog = new DictionaryConfigurationNodeRenameDlg())
				{
					renameDialog.DisplayLabel = dictionaryNode.DisplayLabel;
					renameDialog.NewSuffix = dictionaryNode.LabelSuffix;

					// Unchanged?
					if (renameDialog.ShowDialog() != DialogResult.OK || renameDialog.NewSuffix == dictionaryNode.LabelSuffix)
					{
						return;
					}

					if (!dictionaryNode.ChangeSuffix(renameDialog.NewSuffix, siblings))
					{
						MessageBox.Show(DictionaryConfigurationStrings.FailedToRename);
						return;
					}
				}
				RefreshView();
			};
			View.TreeControl.Remove += node =>
			{
				var dictionaryNode = (ConfigurableDictionaryNode)node.Tag;
				if (dictionaryNode.Parent == null)
				{
					_model.Parts.Remove(dictionaryNode);
				}
				else
				{
					dictionaryNode.UnlinkFromParent();
				}
				RefreshView();
			};
			var metaDataCacheAccessor = cache.GetManagedMetaDataCache();
			View.TreeControl.Highlight += (node, button, tooltip) =>
			{
				_isHighlighted = !_isHighlighted;
				if (_isHighlighted)
				{
					View.HighlightContent(node.Tag as ConfigurableDictionaryNode, metaDataCacheAccessor);
					button.BackColor = Color.White;
					tooltip.SetToolTip(button, DictionaryConfigurationStrings.RemoveHighlighting);
				}
				else
				{
					View.HighlightContent(null, metaDataCacheAccessor); // turns off current highlighting.
					button.BackColor = Color.Yellow;
					tooltip.SetToolTip(button, DictionaryConfigurationStrings.HighlightAffectedContent);
				}
			};
			View.TreeControl.Tree.AfterCheck += (sender, args) =>
			{
				var node = (ConfigurableDictionaryNode)args.Node.Tag;
				node.IsEnabled = args.Node.Checked;

				// Details may need to be enabled or disabled
				RefreshPreview();
				View.TreeControl.Tree.SelectedNode = FindTreeNode(node, View.TreeControl.Tree.Nodes);
				BuildAndShowOptions(node);
			};
			View.TreeControl.Tree.AfterSelect += (sender, args) =>
			{
				var node = (ConfigurableDictionaryNode)args.Node.Tag;

				View.TreeControl.MoveUpEnabled = CanReorder(node, Direction.Up);
				View.TreeControl.MoveDownEnabled = CanReorder(node, Direction.Down);
				View.TreeControl.DuplicateEnabled = !node.IsMainEntry;
				View.TreeControl.RemoveEnabled = node.IsDuplicate;
				View.TreeControl.RenameEnabled = node.IsDuplicate;

				BuildAndShowOptions(node);

				if (_isHighlighted)
				{
					// Highlighting is turned on, change what is highlighted.
					View.HighlightContent(node, metaDataCacheAccessor);
				}
			};
			View.TreeControl.CheckAll += treeNode =>
			{
				EnableNodeAndDescendants(treeNode.Tag as ConfigurableDictionaryNode);
				RefreshView();
			};
			View.TreeControl.UnCheckAll += treeNode =>
			{
				DisableNodeAndDescendants(treeNode.Tag as ConfigurableDictionaryNode);
				RefreshView();
			};
			SelectCurrentConfigurationAndRefresh();
			MasterRefreshRequired = _isDirty = false;
		}
		#endregion
	}
}
