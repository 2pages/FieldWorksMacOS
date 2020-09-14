// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Text;

namespace LanguageExplorer.UtilityTools
{
	/// <summary>
	/// SampleCitationFormTransducer can be used with the Tools:Utilities dialog
	/// It was actually built for Dennis Walters, but could be useful for someone else.
	/// </summary>
	/// <remarks>NOT USED</remarks>
	internal sealed class SampleCitationFormTransducer //: IUtility
	{
		private UtilityDlg m_dlg;

		/// <summary>
		/// Override method to return the Label property.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Label;
		}

		private static string InvokePython(string arguments)
		{
			using (var p = new Process())
			{
				p.StartInfo.FileName = "python";
				var dir = FwDirectoryFinder.GetCodeSubDirectory(Path.Combine("Language Explorer", "UserScripts"));
				p.StartInfo.Arguments = System.IO.Path.Combine(dir, "TransduceCitationForms.py ") + " " + arguments;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.Start();
				p.WaitForExit(1000);
				var output = p.StandardOutput.ReadToEnd();
				return output;
			}
		}

		#region IUtility implementation

		/// <summary>
		/// Get the main label describing the utility.
		/// </summary>
		public string Label => TransductionSampleStrings.ksTransduceCitationForms;

		/// <summary>
		/// Set the UtilityDlg.
		/// </summary>
		/// <remarks>
		/// This must be set, before calling any other property or method.
		/// </remarks>
		public UtilityDlg Dialog
		{
			set => m_dlg = value;
		}

		/// <summary>
		/// Load 0 or more items in the list box.
		/// </summary>
		public void LoadUtilities()
		{
			m_dlg.Utilities.Items.Add(this);

		}

		/// <summary>
		/// Notify the utility is has been selected in the dlg.
		/// </summary>
		public void OnSelection()
		{
			m_dlg.WhenDescription = TransductionSampleStrings.ksWhenToTransduceCitForms;
			m_dlg.WhatDescription = TransductionSampleStrings.ksDemoOfUsingPython;
			m_dlg.RedoDescription = TransductionSampleStrings.ksCannotUndoTransducingCitForms;
		}

		/// <summary>
		/// Have the utility do what it does.
		/// </summary>
		public void Process()
		{
			try
			{
				var cache = m_dlg.PropertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
				m_dlg.ProgressBar.Maximum = cache.LanguageProject.LexDbOA.Entries.Count();
				m_dlg.ProgressBar.Step = 1;
				var locale = InvokePython("-icu"); //ask the python script for the icu local
				locale = locale.Trim();
				var ws = cache.WritingSystemFactory.GetWsFromStr(locale);
				if (ws == 0)
				{
					MessageBox.Show(string.Format(TransductionSampleStrings.ksCannotLocateWsForX, locale));
					return;
				}
				foreach (var e in cache.LanguageProject.LexDbOA.Entries)
				{
					var a = e.CitationForm;
					var src = a.VernacularDefaultWritingSystem.Text;
					var output = InvokePython("-i " + src).Trim();
					a.set_String(ws, TsStringUtils.MakeString(output, ws));
					m_dlg.ProgressBar.PerformStep();
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(string.Format(TransductionSampleStrings.ksErrorMsgWithStackTrace, e.Message, e.StackTrace));
			}
		}
		#endregion IUtility implementation
	}
}