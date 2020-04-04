// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;

namespace LanguageExplorer.LcmUi
{
	/// <summary>
	/// Special VC for classes that have name and abbreviation, both displayed in UI WS.
	/// </summary>
	internal sealed class CmNameAbbrObjVc : CmNamedObjVc
	{
		private readonly int _flidAbbr;

		internal CmNameAbbrObjVc(LcmCache cache, int flidName, int flidAbbr)
			: base(cache, flidName)
		{
			_flidAbbr = flidAbbr;
		}

		public override void Display(IVwEnv vwenv, int hvo, int frag)
		{
			switch (frag)
			{
				case (int)VcFrags.kfragShortName:
					var wsUi = vwenv.DataAccess.WritingSystemFactory.UserWs;
					vwenv.AddStringAltMember(_flidAbbr, wsUi, this);
					break;
				default:
					base.Display(vwenv, hvo, frag);
					break;
			}
		}
	}
}