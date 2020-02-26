// Copyright (c) 2011-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Areas.TextsAndWords.Interlinear
{
	/// <summary>
	/// This class 'decorates' an SDA by intercepting calls to StTxtPara.Contents and, if ShowSpace is true,
	/// replacing zero-width-non-joining-space with a visible space that has a grey background. On write
	/// it replaces spaces with gray background with ZWNJS, and removes all background color.
	/// </summary>
	internal class ShowSpaceDecorator : DomainDataByFlidDecoratorBase
	{
		public static readonly int KzwsBackColor = (int)ColorUtil.ConvertColorToBGR(Color.LightGray);

		public ShowSpaceDecorator(ISilDataAccessManaged sda) : base(sda)
		{}

		public bool ShowSpaces { get; set; }

		public override ITsString get_StringProp(int hvo, int tag)
		{
			var result = base.get_StringProp(hvo, tag);
			if (!ShowSpaces || tag != StTxtParaTags.kflidContents || result == null)
			{
				return result;
			}
			var text = result.Text;
			if (text == null)
			{
				return result;
			}
			var index = text.IndexOf(AnalysisOccurrence.KchZws);
			if (index < 0)
			{
				return result;
			}
			var bldr = result.GetBldr();
			while (index >= 0)
			{
				bldr.Replace(index, index + 1, " ", null);
				bldr.SetIntPropValues(index, index + 1, (int)FwTextPropType.ktptBackColor, (int)FwTextPropVar.ktpvDefault, KzwsBackColor);
				index = text.IndexOf(AnalysisOccurrence.KchZws, index + 1);
			}
			return bldr.GetString();
		}

		public override void SetString(int hvo, int tag, ITsString tss)
		{
			if (!ShowSpaces || tag != StTxtParaTags.kflidContents || tss.Text == null)
			{
				base.SetString(hvo, tag, tss);
				return;
			}
			var text = tss.Text;
			var bldr = tss.GetBldr();
			var index = text.IndexOf(' ');
			while (index >= 0)
			{
				if (bldr.get_PropertiesAt(index).GetIntPropValues((int)FwTextPropType.ktptBackColor, out _) == KzwsBackColor)
				{
					bldr.Replace(index, index + 1, AnalysisOccurrence.KstrZws, null);
				}
				index = text.IndexOf(' ', index + 1);
			}
			for (var irun = bldr.RunCount - 1; irun >= 0; irun--)
			{
				if (bldr.get_Properties(irun).GetIntPropValues((int)FwTextPropType.ktptBackColor, out _) == KzwsBackColor)
				{
					bldr.GetBoundsOfRun(irun, out var ichMin, out var ichLim);
					bldr.SetIntPropValues(ichMin, ichLim, (int)FwTextPropType.ktptBackColor, -1, -1);
				}
			}
			base.SetString(hvo, tag, bldr.GetString());
		}
	}
}