// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.IText;

namespace LanguageExplorer.Areas.TextsAndWords
{
	/// <summary>
	/// The 'TryAWordSandbox' is an IText Sandbox that is used within the Try A word dialog.
	/// </summary>
	internal sealed class TryAWordSandbox : SandboxBase
	{
		#region Data members

		#endregion Data members

		#region Construction and initialization

		/// <summary>
		/// Create a new one.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="ss"></param>
		/// <param name="choices"></param>
		/// <param name="analysis"></param>
		public TryAWordSandbox(FdoCache cache, IVwStylesheet ss, InterlinLineChoices choices,
			IAnalysis analysis)
			: base(cache, ss, choices)
		{
			SizeToContent = true;
			LoadForWordBundleAnalysis(analysis.Hvo);
		}

		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		protected override void Dispose( bool disposing )
		{
			//Debug.WriteLineIf(!disposing, "****************** " + GetType().Name + " 'disposing' is false. ******************");
			// Must not be run more than once.
			if (IsDisposed)
				return;

			base.Dispose( disposing );

			if (disposing)
			{
			}

		}

		#endregion Construction and initialization

	}
}
