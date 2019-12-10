// Copyright (c) 2005-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.DomainServices;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary />
	internal class XmlRDEBrowseViewVc : XmlBrowseViewBaseVc
	{
		/// <summary />
		public const int khvoNewItem = -1234567890;
		/// <summary />
		public const string ksEditColumnBaseName = "FakeEditColumn";
		// The following variables/constants are for implementing an editable row at the bottom
		// of the browse view table.
		// A Set of Hvos. If an HVO is in the set,
		// it is the HVO of a 'new' row that is allowed to be edited.
		private readonly HashSet<int> m_editableHvos = new HashSet<int>();

		#region Constructiona and initialization

		/// <summary />
		internal XmlRDEBrowseViewVc(XElement xnSpec, int madeUpFieldIdentifier, XmlBrowseViewBase xbv)
			: base(xnSpec, madeUpFieldIdentifier, xbv)
		{
			// set the border color
			BorderColor = SystemColors.ControlDark;
			// Check for the special editable row attributes.
			// this one is independently optional so we can handle it separately.
			EditRowMergeMethod = XmlUtils.GetOptionalAttributeValue(xnSpec, "editRowMergeMethod", null);
			var xa = xnSpec.Attribute("editRowModelClass");
			if (xa != null && xa.Value.Length != 0)
			{
				EditRowModelClass = xa.Value;
				xa = xnSpec.Attribute("editRowSaveMethod");
				if (xa != null && xa.Value.Length != 0)
				{
					EditRowSaveMethod = xa.Value;
					xa = xnSpec.Attribute("editRowAssembly");
					if (xa != null && xa.Value.Length != 0)
					{
						EditRowAssembly = xa.Value;
						xa = xnSpec.Attribute("editRowClass");
						if (xa != null && xa.Value.Length != 0)
						{
							EditRowClass = xa.Value;
						}
						else
						{
							// Should we complain to the user?  Die horribly? ...
							EditRowModelClass = null;
							EditRowSaveMethod = null;
							EditRowAssembly = null;
							Debug.WriteLine("editRowModelClass, editRowSaveMethod, and " + "editRowAssembly are set, but editRowClass is not!?");
						}
					}
					else
					{
						// Should we complain to the user?  Die horribly? ...
						EditRowModelClass = null;
						EditRowSaveMethod = null;
						Debug.WriteLine("editRowModelClass and editRowSaveMethod are set, " + "but editRowAssembly is not!?");
					}
				}
				else
				{
					// Should we complain to the user?  Die horribly? ...
					EditRowModelClass = null;
					Debug.WriteLine("editRowModelClass is set, but editRowSaveMethod is not!?");
				}
			}
			// For RDE use, we want total RTL user experience (see LT-5127).
			Cache = m_xbv.Cache;
			ShowColumnsRTL = IsWsRTL(m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle);
		}

		#endregion Construction and initialization

		#region Properties

		/// <summary>
		/// Gets the edit row model class.
		/// </summary>
		public string EditRowModelClass { get; }

		/// <summary>
		/// Gets the edit row assembly.
		/// </summary>
		public string EditRowAssembly { get; }

		/// <summary>
		/// Gets the edit row class.
		/// </summary>
		public string EditRowClass { get; }

		/// <summary>
		/// Gets the edit row save method.
		/// </summary>
		public string EditRowSaveMethod { get; }

		/// <summary>
		/// Gets the edit row merge method.
		/// </summary>
		public string EditRowMergeMethod { get; }

		/// <summary>
		/// Return a Set of HVOs that are editable...
		/// typically new objects added this session.
		/// </summary>
		public ISet<int> EditableObjectsClone()
		{
			return new HashSet<int>(m_editableHvos);
		}

		/// <summary>
		/// Editables the objects contains.
		/// </summary>
		public bool EditableObjectsContains(int key)
		{
			return m_editableHvos.Contains(key);
		}

		/// <summary>
		/// Editables the objects add.
		/// </summary>
		public void EditableObjectsAdd(int key)
		{
			lock (this)
			{
				m_editableHvos.Add(key);
			}
		}

		/// <summary>
		/// Editables the objects clear.
		/// </summary>
		public void EditableObjectsClear()
		{
			lock (this)
			{
				m_editableHvos.Clear();
			}
		}

		/// <summary>
		/// Editables the objects remove.
		/// </summary>
		public void EditableObjectsRemove(int data)
		{
			lock (this)
			{
				m_editableHvos.Remove(data);
			}
		}

		/// <summary>
		/// Editables the objects ids.
		/// </summary>
		public ICollection<int> EditableObjectsIds => m_editableHvos;

		/// <summary>
		/// Editables the objects remove invalid objects.
		/// </summary>
		public void EditableObjectsRemoveInvalidObjects()
		{
			if (m_editableHvos.Count == 0)
			{
				return;     // no processing needed on empty list
			}
			var done = false;
			var validSenses = new HashSet<int>();
			lock (this)
			{
				while (!done)
				{
					var restartEnumerator = false;
					foreach (var hvoSense in EditableObjectsIds)
					{
						if (validSenses.Contains(hvoSense)) // already processed
						{
							done = false;   // just for something to do...
						}
						else
						{
							if (m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoSense).ClassID <= 0)
							{
								EditableObjectsRemove(hvoSense);
								restartEnumerator = true;
								break;
							}
							validSenses.Add(hvoSense);
						}
					}
					if (!restartEnumerator)
					{
						done = true;
					}
				}
				// at this point we have a list of only valid objects
			}
		}

		#endregion Properties

		#region Other methods

		/// <summary>
		/// This is the main interesting method of displaying objects and fragments of them. Most
		/// subclasses should override.
		/// </summary>
		public override void Display(IVwEnv vwenv, int hvo, int frag)
		{
			switch (frag)
			{
				case kfragRoot:
					// assume the root object has been loaded.
					base.Display(vwenv, hvo, frag);
					vwenv.AddObj(khvoNewItem, this, kfragEditRow);
					break;
				case kfragEditRow:
					AddEditRow(vwenv, hvo);
					break;
				case kfragListItem:
					if (hvo != khvoNewItem)
					{
						base.Display(vwenv, hvo, frag);
					}
					break;
				default:
					base.Display(vwenv, hvo, frag);
					break;
			}
		}

		/// <summary>
		/// Change the background color of the non-editable items
		/// </summary>
		protected override void AddTableRow(IVwEnv vwenv, int hvo, int frag)
		{
			// change the background color for all non-editable items
			if (!m_editableHvos.Contains(hvo))
			{
				vwenv.set_IntProperty((int)FwTextPropType.ktptBackColor, (int)FwTextPropVar.ktpvDefault, (int)RGB(SystemColors.ControlLight));
			}
			// use the base functionality, just needed to set the colors
			base.AddTableRow(vwenv, hvo, frag);
		}

		/// <summary>
		/// In this subclass all cells need a specific editability, but it depends on whether it's a new
		/// item or a pre-existing one.
		/// </summary>
		protected override void SetCellProperties(int rowIndex, int icol, XElement node, int hvo, IVwEnv vwenv, bool fIsCellActive)
		{
			SetCellEditability(vwenv, m_editableHvos.Contains(hvo));
		}

		/// <summary>
		/// Override to support different content in edit row (or if a certain column is visible).
		/// This is used so that a Definition can be shown if Gloss is blank and only that column is shown,
		/// and similarly for Lexeme form and Citation form.
		/// </summary>
		public override void ProcessFrag(XElement frag, IVwEnv vwenv, int hvo, bool fEditable, XElement caller)
		{
			switch (frag.Name.LocalName)
			{
				case @"editrow":
					// Special keyword for rapid data entry: may have different content in edit row (or if a particular column is displayed)
					var wantYesChild = ShouldSuppressNoForOtherColumn(frag) || InEditableRow(vwenv);
					var wantChild = wantYesChild ? @"yes" : @"no";
					foreach (var clause in frag.Elements())
					{
						if (clause.Name == wantChild)
						{
							ProcessChildren(clause, vwenv, hvo, caller);
							break;
						}
					}
					break;
				default:
					base.ProcessFrag(frag, vwenv, hvo, fEditable, caller);
					break;
			}
		}

		private bool ShouldSuppressNoForOtherColumn(XElement frag)
		{
			var suppressNoForColumn = XmlUtils.GetOptionalAttributeValue(frag, @"suppressNoForColumn");
			if (!string.IsNullOrEmpty(suppressNoForColumn))
			{
				// If the column that suppresses the "no" special behavior is present, we don't want the "no" child.
				// That includes if a ws-specific column which includes that label is present.
				foreach (var col in m_columns)
				{
					if (XmlUtils.GetOptionalAttributeValue(col, @"label").Contains(suppressNoForColumn))
					{
						return true;
					}
				}
			}
			return false;
		}

		private bool InEditableRow(IVwEnv vwenv)
		{
			int hvoTop, tag, ihvo;
			vwenv.GetOuterObject(1, out hvoTop, out tag, out ihvo);
			return m_editableHvos.Contains(hvoTop);
		}


		internal override int SelectedRowBackgroundColor(int hvo)
		{
			if (m_editableHvos.Contains(hvo))
			{
				return (int)RGB(Color.FromKnownColor(KnownColor.Window));
			}
			return NoEditBackgroundColor;
		}

		private static int NoEditBackgroundColor => (int)RGB(SystemColors.ControlLight);

		/// <summary>
		/// Add a table/row for editing a new object.
		/// </summary>
		private void AddEditRow(IVwEnv vwenv, int hvo)
		{
			// set the border color to gray
			vwenv.set_IntProperty((int)FwTextPropType.ktptBorderColor, (int)FwTextPropVar.ktpvDefault, (int)RGB(BorderColor));
			// Make a table
			var rglength = m_xbv.GetColWidthInfo();
			var colCount = m_columns.Count;
			if (HasSelectColumn)
			{
				colCount++;
			}
			VwLength vl100; // Length representing 100%.
			vl100.unit = rglength[0].unit;
			vl100.nVal = 1;
			for (var i = 0; i < colCount; ++i)
			{
				Debug.Assert(vl100.unit == rglength[i].unit);
				vl100.nVal += rglength[i].nVal;
			}
			vwenv.OpenTable(colCount, // this many columns
				vl100, // using 100% of available space
				72000 / 96, //0, // no border
				VwAlignment.kvaLeft, // cells by default left aligned
				VwFramePosition.kvfpBelow | VwFramePosition.kvfpRhs,
				VwRule.kvrlCols, // vertical lines between columns
				0, // no space between cells
				0, // no padding within cell.
				false);
			for (var i = 0; i < colCount; ++i)
			{
				vwenv.MakeColumns(1, rglength[i]);
			}
			// the table only has a body (no header or footer), and only one row.
			vwenv.OpenTableBody();
			vwenv.OpenTableRow();
			var cda = m_cache.DomainDataByFlid as IVwCacheDa;
			// Make the cells.
			if (ShowColumnsRTL)
			{
				for (var i = m_columns.Count; i > 0; --i)
				{
					AddEditCell(vwenv, cda, i);
				}
			}
			else
			{
				for (var i = 1; i <= m_columns.Count; ++i)
				{
					AddEditCell(vwenv, cda, i);
				}
			}
			vwenv.CloseTableRow();
			vwenv.CloseTableBody();
			vwenv.CloseTable();
		}

		private void AddEditCell(IVwEnv vwenv, IVwCacheDa cda, int i)
		{
			var node = m_columns[i - 1];
			// Make a cell and embed an editable virtual string for the column.
			var editable = XmlUtils.GetOptionalBooleanAttributeValue(node, "editable", true);
			if (!editable)
			{
				vwenv.set_IntProperty((int)FwTextPropType.ktptBackColor, (int)FwTextPropVar.ktpvDefault, NoEditBackgroundColor);
			}
			vwenv.OpenTableCell(1, 1);
			var flid = XMLViewsDataCache.ktagEditColumnBase + i;
			var ws = WritingSystemServices.GetWritingSystem(m_cache, FwUtils.ConvertElement(node), null, m_cache.ServiceLocator.WritingSystems.DefaultAnalysisWritingSystem.Handle).Handle;
			// Paragraph directionality must be set before the paragraph is opened.
			var fRTL = IsWsRTL(ws);
			vwenv.set_IntProperty((int)FwTextPropType.ktptRightToLeft, (int)FwTextPropVar.ktpvEnum, fRTL ? -1 : 0);
			vwenv.set_IntProperty((int)FwTextPropType.ktptAlign, (int)FwTextPropVar.ktpvEnum, fRTL ? (int)FwTextAlign.ktalRight : (int)FwTextAlign.ktalLeft);
			// Fill in the cell with the virtual property.
			vwenv.OpenParagraph();
			vwenv.set_IntProperty((int)FwTextPropType.ktptEditable, (int)FwTextPropVar.ktpvEnum, editable ? (int)TptEditable.ktptIsEditable : (int)TptEditable.ktptNotEditable);
			vwenv.AddStringAltMember(flid, ws, this);
			vwenv.CloseParagraph();
			vwenv.CloseTableCell();
		}

		#endregion Other methods
	}
}