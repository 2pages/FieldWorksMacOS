// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using LanguageExplorer.Controls.DetailControls;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Areas.TextsAndWords.Interlinear
{
	/// <summary>
	/// The modification of the main class suitable for this view.
	/// </summary>
	internal partial class InterlinPrintChild : InterlinDocRootSiteBase
	{
		public InterlinPrintChild()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Pull this out into a separate method so InterlinPrintChild can make an InterlinPrintVc.
		/// </summary>
		protected override void MakeVc()
		{
			Vc = new InterlinPrintVc(m_cache);
		}

		/// <summary>
		/// Activate() is disabled by default in ReadOnlyViews, but PrintView does want to show selections.
		/// </summary>
		protected override bool AllowDisplaySelection => true;

		/// <summary>
		/// Modifications of InterlinVc for printing.
		/// </summary>
		private sealed class InterlinPrintVc : InterlinVc
		{
			private const int kfragTextDescription = 200027;
			private int vtagStTextTitle;
			private int vtagStTextSource;

			internal InterlinPrintVc(LcmCache cache) : base(cache)
			{
			}

			protected override int LabelRGBFor(InterlinLineSpec spec)
			{
				// In the print view these colors are plain black.
				return 0;
			}

			protected override void GetSegmentLevelTags(LcmCache cache)
			{
				// for PrintView
				vtagStTextTitle = cache.MetaDataCacheAccessor.GetFieldId("StText", "Title", false);
				vtagStTextSource = cache.MetaDataCacheAccessor.GetFieldId("StText", "Source", false);
				base.GetSegmentLevelTags(cache);
			}

			public override void Display(IVwEnv vwenv, int hvo, int frag)
			{
				switch (frag)
				{
					case kfragStText: // The whole text, root object for the InterlinDocChild.
						if (hvo == 0)
						{
							return;     // What if the user deleted all the texts?  See LT-6727.
						}
						var stText = (IStText)m_coRepository.GetObject(hvo);
						vwenv.set_IntProperty((int)FwTextPropType.ktptEditable, (int)FwTextPropVar.ktpvDefault, (int)TptEditable.ktptNotEditable);
						vwenv.OpenDiv();
						vwenv.set_IntProperty((int)FwTextPropType.ktptMarginBottom, (int)FwTextPropVar.ktpvMilliPoint, 6000);
						vwenv.set_IntProperty((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 24000);
						// Add both vernacular and analysis if we have them (LT-5561).
						var fAddedVernacular = false;
						if (stText.Title.TryWs(WritingSystemServices.kwsFirstVern, out var wsVernTitle))
						{
							vwenv.OpenParagraph();
							vwenv.AddStringAltMember(vtagStTextTitle, wsVernTitle, this);
							vwenv.CloseParagraph();
							fAddedVernacular = true;
						}
						vwenv.set_IntProperty((int)FwTextPropType.ktptMarginBottom, (int)FwTextPropVar.ktpvMilliPoint, 10000);
						vwenv.OpenParagraph();
						if (stText.Title.TryWs(WritingSystemServices.kwsFirstAnal, out var wsAnalysisTitle, out var tssAnal) && !tssAnal.Equals(stText.Title.BestVernacularAlternative))
						{
							if (fAddedVernacular)
							{
								// display analysis title at smaller font size.
								vwenv.set_IntProperty((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 12000);
							}
							vwenv.AddStringAltMember(vtagStTextTitle, wsAnalysisTitle, this);
						}
						else
						{
							// just add a blank title.
							vwenv.AddString(TsStringUtils.EmptyString(m_wsAnalysis));
						}
						vwenv.CloseParagraph();
						vwenv.set_IntProperty((int)FwTextPropType.ktptMarginBottom, (int)FwTextPropVar.ktpvMilliPoint, 10000);
						if (stText.Source.TryWs(WritingSystemServices.kwsFirstVernOrAnal, out var wsSource))
						{
							vwenv.OpenParagraph();
							vwenv.set_IntProperty((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 12000);
							vwenv.AddStringAltMember(vtagStTextSource, wsSource, this);
							vwenv.CloseParagraph();
						}
						else
						{
							// just add a blank source.
							vwenv.AddString(TsStringUtils.EmptyString(m_wsAnalysis));
						}
						vwenv.set_IntProperty((int)FwTextPropType.ktptMarginBottom, (int)FwTextPropVar.ktpvMilliPoint, 10000);
						vwenv.OpenParagraph();
						if (stText.OwningFlid == TextTags.kflidContents)
						{
							vwenv.AddObjProp((int)CmObjectFields.kflidCmObject_Owner, this, kfragTextDescription);
						}
						vwenv.CloseParagraph();
						base.Display(vwenv, hvo, frag);
						vwenv.CloseDiv();
						break;
					case kfragTextDescription:
						vwenv.AddStringAltMember(CmMajorObjectTags.kflidDescription, m_wsAnalysis, this);
						break;
					default:
						base.Display(vwenv, hvo, frag);
						break;
				}
			}
		}
	}
}