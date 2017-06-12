// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.LexText.Controls;

namespace SIL.FieldWorks.FdoUi
{
	/// <summary>
	/// PartOfSpeechUi provides UI-specific methods for the PartOfSpeech class.
	/// </summary>
	public class PartOfSpeechUi : CmPossibilityUi
	{
		/// <summary>
		/// Create one. Argument must be a PartOfSpeech.
		/// Note that declaring it to be forces us to just do a cast in every case of MakeUi, which is
		/// passed an obj anyway.
		/// </summary>
		/// <param name="obj"></param>
		public PartOfSpeechUi(ICmObject obj) : base(obj)
		{
			Debug.Assert(obj is IPartOfSpeech);
		}

		internal PartOfSpeechUi() { }

		/// <summary>
		/// Handle the context menu for inserting a POS.
		/// </summary>
		/// <param name="propertyTable"></param>
		/// <param name="publisher"></param>
		/// <param name="classId"></param>
		/// <param name="hvoOwner"></param>
		/// <param name="flid"></param>
		/// <param name="insertionPosition"></param>
		/// <returns></returns>
		public new static PartOfSpeechUi CreateNewUiObject(IPropertyTable propertyTable, IPublisher publisher, int classId, int hvoOwner, int flid, int insertionPosition)
		{
			PartOfSpeechUi posUi = null;
			using (MasterCategoryListDlg dlg = new MasterCategoryListDlg())
			{
				FdoCache cache = propertyTable.GetValue<FdoCache>("cache");
				Debug.Assert(cache != null);
				var newOwner = cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>().GetObject(hvoOwner);
				dlg.SetDlginfo(newOwner.OwningList, propertyTable, true, newOwner);
				switch (dlg.ShowDialog(propertyTable.GetValue<Form>("window")))
				{
					case DialogResult.OK: // Fall through.
					case DialogResult.Yes:
						posUi = new PartOfSpeechUi(dlg.SelectedPOS);
						publisher.Publish("JumpToRecord", dlg.SelectedPOS.Hvo);
						break;
				}
			}
			return posUi;
		}

#if RANDYTODO
		/// <summary>
		/// Override to handle case of improper menu in the reversal cat list tool.
		/// </summary>
		/// <param name="commandObject"></param>
		/// <param name="display"></param>
		/// <returns></returns>
		public override bool OnDisplayJumpToTool(object commandObject, ref UIItemDisplayProperties display)
		{
			CheckDisposed();

			Command command = (Command)commandObject;
			string tool = Utils.XmlUtils.GetManditoryAttributeValue(command.Parameters[0], "tool");
			string toolChoice = PropTable.GetValue<string>("currentContentControl");

			if (tool == "posEdit" && toolChoice == "reversalToolReversalIndexPOS")
			{
				display.Visible = display.Enabled = false; // we're already there!
				return true;
			}
			else
			{
				return base.OnDisplayJumpToTool(commandObject, ref display);
			}
		}
#endif
	}
}
