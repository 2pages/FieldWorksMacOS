// Copyright (c) 2013-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.UtilityTools
{
	internal sealed class WriteAllObjectsUtility : IUtility
	{
		private UtilityDlg _utilityDlg;

		/// <summary />
		internal WriteAllObjectsUtility(UtilityDlg utilityDlg)
		{
			Guard.AgainstNull(utilityDlg, nameof(utilityDlg));

			_utilityDlg = utilityDlg;
		}

		/// <summary>
		/// Override method to return the Label property.
		/// </summary>
		public override string ToString()
		{
			return Label;
		}

		#region IUtility implementation

		/// <summary />
		public string Label => "Write Everything";

		/// <summary />
		public void OnSelection()
		{
			_utilityDlg.WhenDescription = LanguageExplorerResources.ksWhenToWriteAllObjects;
			_utilityDlg.WhatDescription = LanguageExplorerResources.ksWhatIsWriteAllObjects;
			_utilityDlg.RedoDescription = LanguageExplorerResources.ksWriteAllObjectsUndo;
		}

		/// <summary />
		public void Process()
		{
			var cache = _utilityDlg.PropertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
			cache.ExportEverythingAsModified();
		}

		#endregion IUtility implementation
	}
}