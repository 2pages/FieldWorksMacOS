// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.Text;

namespace LanguageExplorer.LcmUi
{
	internal class CmObjectVc : FwBaseVc
	{
		internal CmObjectVc(LcmCache cache)
		{
			Cache = cache;
		}

		public override void Display(IVwEnv vwenv, int hvo, int frag)
		{
			var sda = vwenv.DataAccess;
			var wsUi = sda.WritingSystemFactory.UserWs;
			var co = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvo);
			switch (frag)
			{
				case (int)VcFrags.kfragHeadWord:
					vwenv.AddString(co is ILexEntry lexEntry ? lexEntry.HeadWord : TsStringUtils.MakeString(co.ShortName, wsUi));
					break;
				case (int)VcFrags.kfragShortName:
					vwenv.AddString(TsStringUtils.MakeString(co.ShortName, wsUi));
					break;
				default:
					vwenv.AddString(TsStringUtils.MakeString(co.ToString(), wsUi));
					break;
			}
		}
	}
}