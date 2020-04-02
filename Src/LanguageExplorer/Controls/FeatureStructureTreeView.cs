// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SIL.LCModel;

namespace LanguageExplorer.Controls
{
	/// <summary />
	public class FeatureStructureTreeView : TreeView
	{
		private ImageList imageList1;
		private System.ComponentModel.IContainer components;

		public FeatureStructureTreeView(System.ComponentModel.IContainer container)
		{
			// Required for Windows.Forms Class Composition Designer support
			container.Add(this);
			Init();
		}

		public FeatureStructureTreeView()
		{
			Init();
		}

		private void Init()
		{
			InitializeComponent();
			MouseUp += OnMouseUp;
			KeyUp += OnKeyUp;
		}

		public void PopulateTreeFromInflectableFeats(IEnumerable<IFsFeatDefn> defns)
		{
			foreach (var defn in defns)
			{
				AddNode(defn, null);
			}
		}

		public void PopulateTreeFromInflectableFeat(IFsFeatDefn defn)
		{
			AddNode(defn, null);
		}

		public void PopulateTreeFromFeatureStructure(IFsFeatStruc fs)
		{
			AddNode(fs, null);
		}

		public new void Sort()
		{
			if (Nodes.Count > 0)
			{
				Sort(Nodes);
			}
		}

		public void Sort(TreeNodeCollection col)
		{
			if (col.Count == 0)
			{
				return;
			}
			var list = new List<FeatureTreeNode>(col.Count);
			foreach (FeatureTreeNode childNode in col)
			{
				list.Add(childNode);
			}
			list.Sort();
			BeginUpdate();
			col.Clear();
			foreach (var childNode in list)
			{
				col.Add(childNode);
				if (childNode.Nodes.Count > 0)
				{
					if (childNode.Nodes[0].Nodes.Count > 0)
					{
						Sort(childNode.Nodes); // sort all but terminal nodes
					}
					else
					{
						// append "none of the above" node to terminal nodes
						InsertNode(new FeatureTreeNode(LanguageExplorerControls.ksNoneOfTheAbove, (int)LexTextImageKind.radio, (int)LexTextImageKind.radio, 0, FeatureTreeNodeKind.Other), childNode);
					}
				}
			}
			EndUpdate();
		}

		private void AddNode(IFsFeatStruc fs, FeatureTreeNode parentNode)
		{
			foreach (var spec in fs.FeatureSpecsOC)
			{
				AddNode(spec, parentNode);
			}
		}

		private void AddNode(IFsFeatDefn defn, FeatureTreeNode parentNode)
		{
			switch (defn)
			{
				case IFsClosedFeature closedFeature:
				{
					if (!AlreadyInTree(closedFeature.Hvo, parentNode))
					{
						// avoid duplicates
						var newNode = new FeatureTreeNode(closedFeature.Name.AnalysisDefaultWritingSystem.Text, (int)LexTextImageKind.feature, (int)LexTextImageKind.feature, closedFeature.Hvo, FeatureTreeNodeKind.Closed);
						InsertNode(newNode, parentNode);

						foreach (var val in closedFeature.ValuesSorted)
						{
							AddNode(val, newNode);
						}
					}

					break;
				}
				case IFsComplexFeature complexFeature:
				{
					if (!AlreadyInTree(complexFeature.Hvo, parentNode))
					{
						// avoid infinite loop if a complex feature's type is the same as other features.
						var newNode = new FeatureTreeNode(complexFeature.Name.BestAnalysisAlternative.Text, (int)LexTextImageKind.complex, (int)LexTextImageKind.complex, complexFeature.Hvo, FeatureTreeNodeKind.Complex);
						InsertNode(newNode, parentNode);
						var type = complexFeature.TypeRA;
						foreach (var defn2 in type.FeaturesRS)
						{
							AddNode(defn2, newNode);
						}
					}

					break;
				}
			}
		}

		private void AddNode(IFsSymFeatVal val, FeatureTreeNode parentNode)
		{
			var newNode = new FeatureTreeNode(val.Name.BestAnalysisAlternative.Text, (int)LexTextImageKind.radio, (int)LexTextImageKind.radio, val.Hvo, FeatureTreeNodeKind.SymFeatValue);
			InsertNode(newNode, parentNode);
		}

		private void AddNode(IFsFeatureSpecification spec, FeatureTreeNode parentNode)
		{
			var defn = spec.FeatureRA;
			var col = parentNode?.Nodes ?? Nodes;
			switch (spec)
			{
				case IFsClosedValue closedValue:
				{
					foreach (FeatureTreeNode node in col)
					{
						if (defn.Hvo == node.Hvo)
						{
							// already there (which is to be expected); see if its value is, too
							AddNodeFromFS(closedValue.ValueRA, node);
							return;
						}
					}
					// did not find the node, so add it and its value (not to be expected, but we'd better deal with it)
					var newNode = new FeatureTreeNode(defn.Name.AnalysisDefaultWritingSystem.Text, (int)LexTextImageKind.feature, (int)LexTextImageKind.feature, defn.Hvo, FeatureTreeNodeKind.Closed);
					InsertNode(newNode, parentNode);
					var val = closedValue.ValueRA;
					if (val != null)
					{
						var newValueNode = new FeatureTreeNode(val.Name.AnalysisDefaultWritingSystem.Text, (int)LexTextImageKind.radioSelected, (int)LexTextImageKind.radioSelected, val.Hvo, FeatureTreeNodeKind.SymFeatValue);
						newValueNode.Chosen = true;
						InsertNode(newValueNode, newNode);
					}

					break;
				}
				case IFsComplexValue complexValue:
				{
					foreach (FeatureTreeNode node in col)
					{
						if (defn.Hvo == node.Hvo)
						{
							// already there (which is to be expected); see if its value is, too
							AddNode((IFsFeatStruc)complexValue.ValueOA, node);
							return;
						}
					}
					// did not find the node, so add it and its value (not to be expected, but we'd better deal with it)
					var newNode = new FeatureTreeNode(defn.Name.AnalysisDefaultWritingSystem.Text, (int)LexTextImageKind.complex, (int)LexTextImageKind.complex, defn.Hvo, FeatureTreeNodeKind.Complex);
					InsertNode(newNode, parentNode);
					AddNode((IFsFeatStruc)complexValue.ValueOA, newNode);
					break;
				}
			}
		}
		private void AddNodeFromFS(IFsSymFeatVal val, FeatureTreeNode parentNode)
		{
			var col = parentNode?.Nodes ?? Nodes;
			if (val == null)
			{
				return; // can't select it!
			}
			var hvoVal = val.Hvo;
			foreach (FeatureTreeNode node in col)
			{
				if (hvoVal == node.Hvo)
				{
					// already there (which is to be expected); mark it as selected
					node.ImageIndex = (int)LexTextImageKind.radioSelected;
					node.SelectedImageIndex = (int)LexTextImageKind.radioSelected;
					node.Chosen = true;
					return;
				}
			}
			// did not find the node, so add it (not to be expected, but we'd better deal with it)
			var newNode = new FeatureTreeNode(val.Name.AnalysisDefaultWritingSystem.Text, (int)LexTextImageKind.radio, (int)LexTextImageKind.radio, val.Hvo, FeatureTreeNodeKind.SymFeatValue);
			InsertNode(newNode, parentNode);
			newNode.Chosen = true;
		}

		private void InsertNode(FeatureTreeNode newNode, FeatureTreeNode parentNode)
		{
			if (parentNode == null)
			{
				Nodes.Add(newNode);
			}
			else
			{
				parentNode.Nodes.Add(newNode);
			}
		}

		private bool AlreadyInTree(int iTag, FeatureTreeNode node)
		{
			if (node == null)
			{
				// at the top level
				if (Nodes.Cast<FeatureTreeNode>().Any(treeNode => iTag == treeNode.Hvo))
				{
					return true;
				}
			}
			while (node != null)
			{
				if (iTag == node.Hvo)
				{
					return true;
				}
				node = (FeatureTreeNode)node.Parent;
			}
			return false;
		}

		private void OnMouseUp(object obj, MouseEventArgs mea)
		{
			if (mea.Button == MouseButtons.Left)
			{
				var tv = (TreeView)obj;
				var tn = (FeatureTreeNode)tv.GetNodeAt(mea.X, mea.Y);
				if (tn != null)
				{
					var rec = tn.Bounds;
					rec.X += -18; // include the image bitmap (16 pixels plus 2 pixels between the image and the text)
					rec.Width += 18;
					if (rec.Contains(mea.X, mea.Y))
					{
						HandleCheckBoxNodes(tv, tn);
					}
				}
			}
		}

		private void OnKeyUp(object obj, KeyEventArgs kea)
		{
			var tv = (TreeView)obj;
			var tn = (FeatureTreeNode)tv.SelectedNode;
			if (kea.KeyCode == Keys.Space && tn != null)
			{
				HandleCheckBoxNodes(tv, tn);
			}
		}

		private static bool IsTerminalNode(TreeNode tn)
		{
			return tn.Nodes.Count == 0;
		}

		private static void HandleCheckBoxNodes(TreeView tv, FeatureTreeNode tn)
		{
			if (!IsTerminalNode(tn))
			{
				return;
			}
			tn.Chosen = true;
			tn.ImageIndex = tn.SelectedImageIndex = (int)LexTextImageKind.radioSelected;
			if (tn.Parent != null)
			{
				var sibling = (FeatureTreeNode)tn.Parent.FirstNode;
				while (sibling != null)
				{
					if (IsTerminalNode(sibling) && sibling != tn)
					{
						sibling.Chosen = false;
						sibling.ImageIndex = sibling.SelectedImageIndex = (int)LexTextImageKind.radio;
					}
					sibling = (FeatureTreeNode)sibling.NextNode;
				}
			}
			tv.Invalidate();
		}

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
				components?.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FeatureStructureTreeView));
			this.imageList1 = new System.Windows.Forms.ImageList(this.components);
			this.SuspendLayout();
			//
			// imageList1
			//
			this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
			this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
			this.imageList1.Images.SetKeyName(0, "");
			this.imageList1.Images.SetKeyName(1, "");
			this.imageList1.Images.SetKeyName(2, "");
			this.imageList1.Images.SetKeyName(3, "");
			//
			// FeatureStructureTreeView
			//
			resources.ApplyResources(this, "$this");
			this.ImageList = this.imageList1;
			this.ResumeLayout(false);

		}
		#endregion

		private enum LexTextImageKind
		{
			complex = 0,
			feature,
			radio,
			radioSelected
		}
	}
}