// Copyright (c) 2002-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	/// <summary />
	/// <remarks>
	/// Used by: FindExampleSentenceDlg
	/// </remarks>
	internal sealed class BasicPaneBarContainer : UserControl, IPropertyTableProvider
	{
		#region Data Members

		/// <summary />
		private IPaneBar m_paneBar;

		#endregion Data Members

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; set; }

		/// <summary>
		/// Init for basic BasicPaneBarContainer.
		/// </summary>
		internal void Init(IPropertyTable propertyTable, Control mainControl, IPaneBar paneBar)
		{
			if (PropertyTable != null && PropertyTable != propertyTable)
			{
				throw new ArgumentException("Mis-matched property tables being set for this object.");
			}
			PropertyTable = propertyTable;
			PaneBar = paneBar;
			Controls.Add((Control)PaneBar);

			mainControl.Dock = DockStyle.Fill;
			Controls.Add(mainControl);
			mainControl.BringToFront();
		}

		/// <summary />
		internal IPaneBar PaneBar
		{
			get => m_paneBar;
			set
			{
				if (m_paneBar != null)
				{
					throw new InvalidOperationException(@"Pane bar container already has a pane bar.");
				}
				m_paneBar = value;
				if (m_paneBar is Control pbAsControl && pbAsControl.AccessibleName == null)
				{
					pbAsControl.AccessibleName = "LanguageExplorer.Controls.PaneBar";
				}
			}
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");

			if (disposing)
			{
				if (m_paneBar != null)
				{
					var pbAsControl = m_paneBar as Control;
					pbAsControl?.Dispose();
				}
			}

			m_paneBar = null;
			PropertyTable = null;

			base.Dispose(disposing);
		}
	}
}