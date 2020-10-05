// Copyright (c) 2007-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using LanguageExplorer.Controls.MGA;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Controls
{
	/// <summary />
	internal class MasterInflectionFeatureListDlg : MasterListDlg
	{
		internal MasterInflectionFeatureListDlg()
		{
		}

		internal MasterInflectionFeatureListDlg(string className) : base(className, new GlossListTreeView())
		{
		}

		protected override void DoExtraInit()
		{
			Text = LanguageExplorerControls.ksInflectionFeatureCatalogTitle;
			label1.Text = LanguageExplorerControls.ksInflectionFeatureCatalogPrompt;
			label2.Text = LanguageExplorerControls.ksInflectionFeatureCatalogTreeLabel;
			label3.Text = LanguageExplorerControls.ksInflectionFeatureCatalogDescriptionLabel;
			SetLinkLabel();
			s_helpTopic = "khtpInsertInflectionFeature";
		}

		private void SetLinkLabel()
		{
			var sInflFeature = LanguageExplorerControls.ksInflectionFeature;
			var sFeatureKind = sInflFeature;
			if (m_sClassName == "FsComplexFeature")
			{
				sFeatureKind = LanguageExplorerControls.ksComplexFeature;
			}
			linkLabel1.Text = string.Format(LanguageExplorerControls.ksLinkText, sInflFeature, sFeatureKind);
		}

		protected override void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (var undoHelper = new UndoableUnitOfWorkHelper(m_cache.ServiceLocator.GetInstance<IActionHandler>(), LanguageExplorerControls.ksUndoInsertInflectionFeature, LanguageExplorerControls.ksRedoInsertInflectionFeature))
			{
				var fd = m_sClassName == "FsComplexFeature"
					? (IFsFeatDefn)m_cache.ServiceLocator.GetInstance<IFsComplexFeatureFactory>().Create()
					: m_cache.ServiceLocator.GetInstance<IFsClosedFeatureFactory>().Create();
				m_cache.LanguageProject.MsFeatureSystemOA.FeaturesOC.Add(fd);
				var type = m_cache.LanguageProject.MsFeatureSystemOA.GetFeatureType("Infl");
				if (type == null)
				{
					type = m_cache.ServiceLocator.GetInstance<IFsFeatStrucTypeFactory>().Create();
					m_cache.LanguageProject.MsFeatureSystemOA.TypesOC.Add(type);
					type.CatalogSourceId = "Infl";
					foreach (var ws in m_cache.ServiceLocator.WritingSystems.AnalysisWritingSystems)
					{
						var tss = TsStringUtils.MakeString("Infl", ws.Handle);
						type.Abbreviation.set_String(ws.Handle, tss);
						type.Name.set_String(ws.Handle, tss);
					}
				}
				type.FeaturesRS.Add(fd);
				SelectedFeatDefn = fd;
				undoHelper.RollBack = false;
			}
			DialogResult = DialogResult.Yes;
			Close();
		}
	}
}