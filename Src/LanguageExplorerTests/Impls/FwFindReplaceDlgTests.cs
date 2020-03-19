// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using FieldWorks.TestUtilities;
using LanguageExplorer;
using NUnit.Framework;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;
using Application = System.Windows.Forms.Application;
using MenuItem = System.Windows.Forms.MenuItem;

namespace LanguageExplorerTests.Impls
{
	/// <summary />
	[TestFixture]
	public class FwFindReplaceDlgTests : FwFindReplaceDlgBaseTests
	{
		private CoreWritingSystemDefinition m_wsFr;
		private CoreWritingSystemDefinition m_wsIpa;

		/// <summary>
		/// Initializes the ScrReference for testing.
		/// </summary>
		public override void FixtureSetup()
		{
			base.FixtureSetup();
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("fr", out m_wsFr);
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("en-fonipa-x-etic", out m_wsIpa);
		}

		#region Helper methods
		/// <summary>
		/// Setup mocks to return styles
		/// </summary>
		protected void SetupStylesheet()
		{
			int hvoStyle = m_Stylesheet.MakeNewStyle();
			m_Stylesheet.PutStyle("CStyle3", "bla", hvoStyle, 0, 0, 1, false, false, null);
			hvoStyle = m_Stylesheet.MakeNewStyle();
			m_Stylesheet.PutStyle("CStyle2", "bla", hvoStyle, 0, 0, 1, false, false, null);
			hvoStyle = m_Stylesheet.MakeNewStyle();
			m_Stylesheet.PutStyle("PStyle1", "bla", hvoStyle, 0, 0, 0, false, false, null);
			hvoStyle = m_Stylesheet.MakeNewStyle();
			m_Stylesheet.PutStyle("CStyle1", "bla", hvoStyle, 0, 0, 1, false, false, null);
		}
		#endregion

		#region Dialog initilization tests
		/// <summary>
		/// Test to see if the selected text gets copied into the find dialog and verify state
		/// of controls.
		/// </summary>
		[Test]
		public void CheckInitialDlgState()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 4; // Select the first "Blah" in the view
			var selInitial = helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			// None of the options are checked initially and the More pane should be hidden
			// Except now the MatchDiacritics defaults to checked (LT-8191)
			ITsString tss;
			selInitial.GetSelectionString(out tss, string.Empty);
			AssertEx.AreTsStringsEqual(tss, m_dlg.FindText);
			Assert.IsFalse(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.MatchDiacriticsCheckboxChecked);
			Assert.IsFalse(m_dlg.MatchWholeWordCheckboxChecked);
			Assert.IsFalse(m_dlg.MatchCaseCheckboxChecked);
			Assert.IsFalse(m_dlg.MoreControlsPanelVisible);
		}

		/// <summary>
		/// Verify that the Styles menu contains the correct styles.
		/// </summary>
		[Test]
		public void VerifyAllStylesInMenu()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.PopulateStyleMenu();
			Assert.AreEqual("<no style>", m_dlg.StyleMenu.MenuItems[0].Text);
			Assert.IsTrue(m_dlg.StyleMenu.MenuItems[0].Checked);
			Assert.AreEqual("Default Paragraph Characters", m_dlg.StyleMenu.MenuItems[1].Text);
			Assert.IsFalse(m_dlg.StyleMenu.MenuItems[1].Checked);
			Assert.AreEqual("CStyle1", m_dlg.StyleMenu.MenuItems[2].Text);
			Assert.IsFalse(m_dlg.StyleMenu.MenuItems[2].Checked);
			Assert.AreEqual("CStyle2", m_dlg.StyleMenu.MenuItems[3].Text);
			Assert.IsFalse(m_dlg.StyleMenu.MenuItems[3].Checked);
			Assert.AreEqual("CStyle3", m_dlg.StyleMenu.MenuItems[4].Text);
			Assert.IsFalse(m_dlg.StyleMenu.MenuItems[4].Checked);
		}

		/// <summary>
		/// Verify that the Styles menu contains the correct checked style.
		/// </summary>
		[Test]
		public void VerifyCheckedStyle()
		{
			SetupStylesheet();
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 4; // Select the first "Blah" in the view
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.PopulateStyleMenu();
			Assert.AreEqual("CStyle3", m_dlg.StyleMenu.MenuItems[4].Text);
			Assert.IsTrue(m_dlg.StyleMenu.MenuItems[4].Checked);
		}

		/// <summary>
		/// Verify that the Writing System menu contains the correct writing systems.
		/// </summary>
		[Test]
		public void VerifyAllWritingSystemsInMenu()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			// For this test, we have a simple IP in French text, so that WS should be checked.
			m_dlg.PopulateWritingSystemMenu();
			Assert.AreEqual(6, m_dlg.WritingSystemMenu.MenuItems.Count);
			var i = 0;
			Assert.AreEqual("English", m_dlg.WritingSystemMenu.MenuItems[i].Text);
			Assert.IsFalse(m_dlg.WritingSystemMenu.MenuItems[i++].Checked);
			Assert.AreEqual("English (Phonetic)", m_dlg.WritingSystemMenu.MenuItems[i].Text);
			Assert.IsTrue(m_dlg.WritingSystemMenu.MenuItems[i++].Checked);
			// Depending on the ICU files present on a given machine, we may or may not have an English name for the French WS.
			Assert.IsTrue(m_dlg.WritingSystemMenu.MenuItems[i].Text == "fr" || m_dlg.WritingSystemMenu.MenuItems[i].Text == "French");
			Assert.IsFalse(m_dlg.WritingSystemMenu.MenuItems[i++].Checked);
			Assert.AreEqual("German", m_dlg.WritingSystemMenu.MenuItems[i].Text);
			Assert.IsFalse(m_dlg.WritingSystemMenu.MenuItems[i++].Checked);
			Assert.AreEqual("Spanish", m_dlg.WritingSystemMenu.MenuItems[i].Text);
			Assert.IsFalse(m_dlg.WritingSystemMenu.MenuItems[i++].Checked);
			Assert.AreEqual("Urdu", m_dlg.WritingSystemMenu.MenuItems[i].Text);
			Assert.IsFalse(m_dlg.WritingSystemMenu.MenuItems[i].Checked);
		}

		/// <summary>
		/// Verify that nothing in the Writing System menu is checked when selection contains
		/// multiple writing systems.
		/// </summary>
		[Test]
		public void VerifyNoCheckedWritingSystem()
		{
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.SetIntPropValues(0, 2, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, Cache.ServiceLocator.WritingSystemManager.GetWsFromStr("de"));
			para.Contents = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 4; // Select the first "Blah" in the view (two different WS's)
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.PopulateWritingSystemMenu();
			Assert.AreEqual(6, m_dlg.WritingSystemMenu.MenuItems.Count);
			foreach (MenuItem mi in m_dlg.WritingSystemMenu.MenuItems)
			{
				Assert.IsFalse(mi.Checked);
			}
		}

		/// <summary>
		/// Tests that reopening the dialog when previously a WS but no find text was specified
		/// remembers the WS. (TE-5127)
		/// </summary>
		[Test]
		public void ReopeningRemembersWsWithoutText()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.SetSelection(true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			// Set a writing system
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			// Simulate a find. This is necessary to save the changes.
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			m_dlg.PopulateWritingSystemMenu();
			Assert.AreEqual(0, m_dlg.FindText.Length, "Shouldn't have any find text before closing dialog");
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked, "WS Checkbox should be checked before closing dialog");
			Assert.AreEqual("German", m_dlg.FindFormatTextLabel.Text);
			m_dlg.Hide(); // this is usually done in OnClosing, but for whatever reason that doesn't work in our test

			// Simulate reshowing the dialog
			helper.IchAnchor = 0;
			helper.IchEnd = 0;
			helper.SetSelection(true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.PopulateWritingSystemMenu();
			Assert.AreEqual(0, m_dlg.FindText.Length, "Shouldn't have any find text after reopening dialog");
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked, "WS Checkbox should be checked after reopening dialog");
			Assert.AreEqual("German", m_dlg.FindFormatTextLabel.Text);
			// Match diacritics now defaults to checked (LT-8191)
			Assert.IsTrue(m_dlg.MatchDiacriticsCheckboxChecked);
			Assert.IsFalse(m_dlg.MatchWholeWordCheckboxChecked);
			Assert.IsFalse(m_dlg.MatchCaseCheckboxChecked);
			Assert.IsFalse(m_dlg.MoreControlsPanelVisible);
		}

		/// <summary>
		/// Test the LastTextBoxInFocus property.
		/// </summary>
		[Test]
		[Category("DesktopRequired")]
		public void LastTextBoxInFocus()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			Assert.AreEqual(m_dlg.FindTextControl, m_dlg.LastTextBoxInFocus);
			// set the focus to the replace box
			m_dlg.ReplaceTextControl.Focus();
			Assert.AreEqual(m_dlg.FindTextControl, m_dlg.LastTextBoxInFocus);
			// set the focus to the find box
			m_dlg.FindTextControl.Focus();
			Assert.AreEqual(m_dlg.ReplaceTextControl, m_dlg.LastTextBoxInFocus);
		}
		#endregion

		#region Apply style tests
		/// <summary>
		/// Test the ApplyStyle method with a range selection
		/// </summary>
		[Test]
		public void ApplyStyle_ToSelectedString()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 4; // Select the first "Blah" in the view (two different WS's)
			helper.SetSelection(false);
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			var propsBldr = TsStringUtils.MakePropsBldr();
			propsBldr.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, "CStyle3");
			propsBldr.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic"));
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, "Blah", propsBldr.GetTextProps());
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("CStyle3", m_dlg.FindFormatTextLabel.Text);
			// If we check the match WS checkbox we need to show the writing system
			m_dlg.MatchWsCheckboxChecked = true;
			Assert.AreEqual("CStyle3, English (Phonetic)", m_dlg.FindFormatTextLabel.Text);
			m_dlg.MatchWsCheckboxChecked = false;
			strBldr.SetStrPropValue(0, 4, (int)FwTextPropType.ktptNamedStyle, null);
			tssExpected = strBldr.GetString();
			// Not sure why we have to do this, but the selection seems to get lost in the test,
			// so if we don't do this, it applies the style to an IP, and the test fails.
			m_dlg.FindTextControl.SelectAll();
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "<No Style>");
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
		}

		/// <summary>
		/// Test the ApplyStyle method applying multiple styles to a string
		/// </summary>
		[Test]
		public void ApplyStyle_MultipleStyles()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 10; // Select the "Blah, blah" in the view
			helper.SetSelection(false);
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.FindTextControl.Select(0, 4);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.FindTextControl.Select(4, 6);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle2");
			// make the string backwards...
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, ", blah", StyleUtils.CharStyleTextProps("CStyle2", Cache.WritingSystemFactory.GetWsFromStr("de")));
			strBldr.Replace(0, 0, "Blah", StyleUtils.CharStyleTextProps("CStyle3", Cache.WritingSystemFactory.GetWsFromStr("de")));
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("Multiple Styles, German", m_dlg.FindFormatTextLabel.Text);
			// unchecking the match WS should hide the WS name
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("Multiple Styles", m_dlg.FindFormatTextLabel.Text);
		}

		/// <summary>
		/// Test the ApplyStyle method applying a style to part of a string
		/// </summary>
		[Test]
		public void ApplyStyle_StyleOnPartOfString()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 10; // Select the "Blah, blah" in the view
			helper.SetSelection(false);
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
			m_dlg.FindTextControl.Select(4, 6);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle2");
			// make the string backwards...
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, ", blah", StyleUtils.CharStyleTextProps("CStyle2", Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic")));
			strBldr.Replace(0, 0, "Blah", StyleUtils.CharStyleTextProps(null, Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic")));
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("Multiple Styles", m_dlg.FindFormatTextLabel.Text);
		}

		/// <summary>
		/// Test the ApplyStyle method with an IP in an empty text box
		/// </summary>
		[Test]
		public void ApplyStyle_ToEmptyTextBox()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find text box");
			var tssFind = m_dlg.FindTextControl.Tss;
			Assert.AreEqual(1, tssFind.RunCount);
			Assert.AreEqual("CStyle3", tssFind.get_Properties(0).GetStrPropValue((int)FwTextPropType.ktptNamedStyle));
		}
		#endregion

		#region Apply Writing System tests
		/// <summary>
		/// Test the ApplyWritingSystem method with a range selection
		/// </summary>
		[Test]
		public void ApplyWS_ToSelectedString()
		{
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 4; // Select the first "Blah" in the view (two different WS's)
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find box");
			var propsBldr = TsStringUtils.MakePropsBldr();
			propsBldr.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, Cache.WritingSystemFactory.GetWsFromStr("de"));
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, "Blah", propsBldr.GetTextProps());
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("German", m_dlg.FindFormatTextLabel.Text);
			// We should only show the WS information if the match WS check box is checked
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
		}

		/// <summary>
		/// Test the ApplyWritingSystem method applying multiple writing systems
		/// </summary>
		[Test]
		public void ApplyWS_MultipleWritingSystems()
		{
			// Blah, blah, blah!
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 10; // Select the "Blah, blah" in the view
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.FindTextControl.Select(0, 4);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.FindTextControl.Select(4, 6);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("en"));
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find box");
			// make the string backwards...
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, ", blah", StyleUtils.CharStyleTextProps(null, Cache.WritingSystemFactory.GetWsFromStr("en")));
			strBldr.Replace(0, 0, "Blah", StyleUtils.CharStyleTextProps(null, Cache.WritingSystemFactory.GetWsFromStr("de")));
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("Multiple Writing Systems", m_dlg.FindFormatTextLabel.Text);
			// We should only show the WS information if the match WS check box is checked
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
		}

		/// <summary>
		/// Test the ApplyWritingSystem method with an IP in an empty text box
		/// </summary>
		[Test]
		public void ApplyWS_ToEmptyTextBox()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find box");
			var tssFind = m_dlg.FindTextControl.Tss;
			Assert.AreEqual(1, tssFind.RunCount);
			Assert.AreEqual(Cache.WritingSystemFactory.GetWsFromStr("de"), tssFind.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out _));
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("German", m_dlg.FindFormatTextLabel.Text);
			// We should only show the WS information if the match WS check box is checked
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.IsFalse(m_dlg.FindFormatLabel.Visible);
			Assert.IsFalse(m_dlg.FindFormatTextLabel.Visible);
		}
		#endregion

		#region Both Style and WS tests
		/// <summary>
		/// Test the ApplyWritingSystem method applying multiple writing systems and one style
		/// </summary>
		[Test]
		public void ApplyWS_OneStyleMultipleWritingSystems()
		{
			SetupStylesheet();
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 10; // Select the "Blah, blah" in the view
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.FindTextControl.Select(0, 10);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.FindTextControl.Select(0, 4);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.FindTextControl.Select(4, 6);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("en"));
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find box");
			// make the string backwards...
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, ", blah", StyleUtils.CharStyleTextProps("CStyle3", Cache.WritingSystemFactory.GetWsFromStr("en")));
			strBldr.Replace(0, 0, "Blah", StyleUtils.CharStyleTextProps("CStyle3", Cache.WritingSystemFactory.GetWsFromStr("de")));
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("CStyle3, Multiple Writing Systems", m_dlg.FindFormatTextLabel.Text);
			// When we uncheck the match WS checkbox we should hide the WS information
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.AreEqual("CStyle3", m_dlg.FindFormatTextLabel.Text);
		}

		/// <summary>
		/// Test the ApplyWritingSystem method applying multiple writing systems and multiple
		/// styles
		/// </summary>
		[Test]
		public void ApplyWS_MultipleStylesMultipleWritingSystems()
		{
			// Blah, blah, blah!
			SetupStylesheet();
			var helper = SelectionHelper.Create(m_vwRootsite);
			helper.IchEnd = 10; // Select the "Blah, blah" in the view
			helper.SetSelection(false);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.Show();
			Application.DoEvents();
			m_dlg.FindTextControl.Select(0, 4);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.FindTextControl.Select(4, 6);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("en"));
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle2");
			Assert.IsTrue(m_dlg.FindTextControl.Focused, "Focus should have returned to Find box");
			// make the string backwards...
			var strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, ", blah", StyleUtils.CharStyleTextProps("CStyle2", Cache.WritingSystemFactory.GetWsFromStr("en")));
			strBldr.Replace(0, 0, "Blah", StyleUtils.CharStyleTextProps("CStyle3", Cache.WritingSystemFactory.GetWsFromStr("de")));
			var tssExpected = strBldr.GetString();
			AssertEx.AreTsStringsEqual(tssExpected, m_dlg.FindTextControl.Tss);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			Assert.IsTrue(m_dlg.FindFormatLabel.Visible);
			Assert.IsTrue(m_dlg.FindFormatTextLabel.Visible);
			Assert.AreEqual("Multiple Styles, Multiple Writing Systems", m_dlg.FindFormatTextLabel.Text);
			// When we uncheck the match WS checkbox we should hide the WS information
			m_dlg.MatchWsCheckboxChecked = false;
			Assert.AreEqual("Multiple Styles", m_dlg.FindFormatTextLabel.Text);
		}
		#endregion

		#region Find tests
		/// <summary>
		/// Test an initial search when finding a next match.
		/// </summary>
		[Test]
		public void InitialFindWithMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 4);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
		}

		/// <summary>
		/// Test an initial search when using an invalid regular expression (TE-4866).
		/// </summary>
		[Test]
		public void InitialFindWithRegEx_Invalid()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.UseRegExCheckboxChecked = true;
			m_dlg.FindText = TsStringUtils.MakeString("?", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.m_fInvalidRegExDisplayed);
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
		}

		/// <summary>
		/// Test an initial search when using a valid regular expression.
		/// </summary>
		[Test]
		public void InitialFindWithRegEx_Valid()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.UseRegExCheckboxChecked = true;
			m_dlg.FindText = TsStringUtils.MakeString("(blah, )?", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsFalse(m_dlg.m_fInvalidRegExDisplayed);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 6);
		}

		/// <summary>
		/// Test an initial search when not finding a match even after wrapping around.
		/// </summary>
		[Test]
		public void InitialFindWithNoMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Text that ain't there", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			// Make sure the dialog thinks there were no matches.
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			Assert.AreEqual(MatchType.NoMatchFound, m_dlg.m_matchNotFoundType);
			m_dlg.VerifySelection(0, 0, 0, 0, 0);
		}

		/// <summary>
		/// Test an initial backward search when not finding a match even after wrapping around.
		/// </summary>
		[Test]
		public void InitialFindPrevWithNoMatch()
		{
			m_vwRootsite.RootBox.MakeSimpleSel(false, true, false, true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Text that ain't there", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindPrevButtonClick();
			// Make sure the dialog thinks there were no matches.
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			Assert.AreEqual(MatchType.NoMatchFound, m_dlg.m_matchNotFoundType);
			m_dlg.VerifySelection(0, 0, 2, 17, 17);
		}

		/// <summary>
		/// Test an initial search when finding a match after wrapping around.
		/// </summary>
		[Test]
		public void InitialFindWithMatchAfterWrap()
		{
			var para = AddParaToMockedText(m_text, "Whatever");
			AddRunToMockedPara(para, "Waldo", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_vwRootsite.RootBox.Reconstruct();
			var levInfo = new SelLevInfo[1];
			levInfo[0] = new SelLevInfo
			{
				ihvo = 1,
				tag = StTextTags.kflidParagraphs
			};
			Assert.IsNotNull(m_vwRootsite.RootBox.MakeTextSelection(0, 1, levInfo,
				StTxtParaTags.kflidContents, 1, 0, 0, Cache.WritingSystemFactory.GetWsFromStr("fr"),
				false, -1, null, true));
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah!", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 12, 17);
		}

		/// <summary>
		/// Test finding a match when doing a find that isn't the initial find.
		/// </summary>
		[Test]
		public void FindNextWithMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("blah!", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 12, 17);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 1, 12, 17);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 2, 12, 17);
		}

		/// <summary>
		/// Test the FindNext method when doing a find that isn't the initial find. After
		/// finding some matches, the search wraps and finds no more matches.
		/// </summary>
		[Test]
		public void FindNextWithNoMatchAfterWrap()
		{
			var para = AddParaToMockedText(m_text, "Whatever");
			AddRunToMockedPara(para, "Waldo", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Waldo", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 1, 0, 0, 5);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 1, 1, 0, 5);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 1, 2, 0, 5);
			m_dlg.SimulateFindButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			// Make sure the dialog thinks there were no more matches.
			Assert.AreEqual(MatchType.NoMoreMatchesFound, m_dlg.m_matchNotFoundType);
			m_dlg.VerifySelection(0, 1, 2, 0, 5); // Selection shouldn't have moved
		}

		/// <summary>
		/// Test the FindNext method when doing a find that begins in the word we're searching
		/// for. The search should wrap and find the match but not find it again.
		/// </summary>
		[Test]
		public void FindNextFromWithinMatchingWord()
		{
			var selHelper = SelectionHelper.Create(m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, false), m_vwRootsite);
			selHelper.IchAnchor = 3;
			selHelper.IchEnd = 3;
			selHelper.MakeBest(false);
			m_vwPattern.MatchCase = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah, ", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 1, 0, 6);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 2, 0, 6);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 6);
			m_dlg.SimulateFindButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			// Make sure the dialog thinks there were no more matches.
			Assert.AreEqual(MatchType.NoMoreMatchesFound, m_dlg.m_matchNotFoundType);
			m_dlg.VerifySelection(0, 0, 0, 0, 6); // Selection shouldn't have moved
		}

		/// <summary>
		/// Test the FindNext method when doing a find when there is no selection in the view.
		/// </summary>
		[Test]
		public void FindWithNoInitialSelection()
		{
			// This destroys the selection
			m_vwRootsite.RootBox.Reconstruct();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("blah!", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 12, 17);
		}

		/// <summary>
		/// Test Find when the match contains an ORC.
		/// </summary>
		[Test]
		public void Find_ORCwithinMatch()
		{
			// Add ORC within title
			AddFootnote(m_genesis, m_text[0], 3);
			// This destroys the selection
			m_vwRootsite.RootBox.Reconstruct();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 5);
		}

		/// <summary>
		/// Test Find when the pattern to search for contains an ORC.
		/// </summary>
		[Test]
		public void Find_ORCwithinPattern()
		{
			// This destroys the selection
			m_vwRootsite.RootBox.Reconstruct();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			var strFind = TsStringUtils.MakeString("blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			var strBldr = strFind.GetBldr();
			TsStringUtils.InsertOrcIntoPara(Guid.NewGuid(), FwObjDataTypes.kodtOwnNameGuidHot, strBldr, 2, 2, Cache.WritingSystemFactory.GetWsFromStr("fr"));
			strFind = strBldr.GetString();
			m_dlg.FindText = strFind;
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.AreEqual("blah".ToCharArray(), m_dlg.FindText.Text.ToCharArray());
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 4);
		}

		/// <summary>
		/// Test Find when the pattern to search for contains an ORC.
		/// </summary>
		/// <remarks>This test needs to be modified to treat the found text like the program
		/// does. Also, see TeAppTests.cs for a test of the same name that attempts to test
		/// this issue there.</remarks>
		[Test]
		public void Replace_MatchContainsORC()
		{
			m_vwRootsite.CloseRootBox();
			m_vwRootsite.MyDisplayType = DisplayType.kNormal | DisplayType.kMappedPara;
			m_vwRootsite.MakeRoot(m_text.Hvo, ScrBookTags.kflidTitle, 3);
			// Add ORC within title (within last "blah!")
			var para = m_text[0];
			var strbldr = para.Contents.GetBldr();
			m_genesis.InsertFootnoteAt(0, strbldr, para.Contents.Length - 3);
			para.Contents = strbldr.GetString();
			m_vwRootsite.RefreshDisplay();
			var origFootnoteCount = m_genesis.FootnotesOS.Count;
			Assert.AreEqual(1, origFootnoteCount);
			// This destroys the selection
			m_vwRootsite.RootBox.Reconstruct();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("blah!", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			Debug.WriteLine(m_dlg.FindText.Text);
			m_dlg.ReplaceText = TsStringUtils.MakeString("text", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			Debug.WriteLine(m_dlg.ReplaceText.Text);
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.SimulateReplaceButtonClick();
			var expected = "Blah, blah, text" + StringUtils.kChObject;
			Assert.AreEqual(expected.ToCharArray(), para.Contents.Text.ToCharArray());
			// Confirm that the footnote was not deleted.
			Assert.AreEqual(origFootnoteCount, m_genesis.FootnotesOS.Count);
			m_dlg.VerifySelection(0, 0, 0, 17, 17);
		}
		#endregion

		#region Replace tests
		/// <summary>
		/// Test an initial find when finding a match using the replace button. The first time
		/// the user presses the "Replace" button, we just find the next match, but we don't
		/// actually replace.
		/// </summary>
		[Test]
		public void InitialFindUsingReplaceTabWithMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ReplaceText = TsStringUtils.MakeString("Monkey feet", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 4);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			Assert.AreEqual(m_kTitleText, m_text[0].Contents.Text);
		}

		/// <summary>
		/// Test a replace when finding a match using the replace button. This simulates the
		/// "second" time the user presses Replace, where we actually do the replace and then
		/// go on to find the next match.
		/// </summary>
		[Test]
		public void InitialReplaceTabWithMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ReplaceText = TsStringUtils.MakeString("Monkey feet", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 13, 17);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			Assert.AreEqual("Monkey feet, blah, blah!", m_text[0].Contents.Text);
		}

		/// <summary>
		/// Test a replace when the replace text contains multiple runs with styles.
		/// </summary>
		[Test]
		public void ReplaceStyles()
		{
			var ipaWs = Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic");
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", ipaWs);
			// Change the replace string in the dialog to have 2 styled runs.
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "Run 2", StyleUtils.CharStyleTextProps("CStyle1", ipaWs));
			bldr.Replace(0, 0, "Run 1", StyleUtils.CharStyleTextProps("CStyle3", ipaWs));
			m_dlg.ReplaceText = bldr.GetString();
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 12, 16);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, ", blah, blah!", StyleUtils.CharStyleTextProps(null, ipaWs));
			bldr.Replace(0, 0, "Run 2", StyleUtils.CharStyleTextProps("CStyle1", ipaWs));
			bldr.Replace(0, 0, "Run 1", StyleUtils.CharStyleTextProps("CStyle3", ipaWs));
			var expectedTssReplace = bldr.GetString();
			AssertEx.AreTsStringsEqual(expectedTssReplace, m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Test a Replace All matching for a character style (no text) and replacing with
		/// another character style (no text).
		/// </summary>
		[Test]
		public void ReplaceAllStyles()
		{
			SetupStylesheet();
			// Replace the default test text with a mixed-style sentence
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.PrevPatternText = null;
			m_dlg.FindText = TsStringUtils.MakeString(m_kTitleText, m_wsIpa.Handle);
			m_dlg.ReplaceText = BuildTssWithStyle("CStyle3");
			m_dlg.SimulateReplaceAllButtonClick();
			AssertEx.AreTsStringsEqual(BuildTssWithStyle("CStyle3"), m_text[0].Contents);
			// Set up the find/replace text with two different character styles.
			m_dlg.PrevPatternText = null;
			m_dlg.FindText = TsStringUtils.MakeString(string.Empty, m_wsIpa.Handle);
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.ReplaceText = TsStringUtils.MakeString(string.Empty, m_wsIpa.Handle);
			m_dlg.ApplyStyle(m_dlg.ReplaceTextControl, "CStyle2");
			m_dlg.SimulateReplaceAllButtonClick(); // SUT
			AssertEx.AreTsStringsEqual(BuildTssWithStyle("CStyle2"), m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Builds and returns a TsString with runs of the specified style interspersed throughout.
		/// </summary>
		private ITsString BuildTssWithStyle(string style)
		{
			var wsIpa = m_wsIpa.Handle;
			var bldrExpected = TsStringUtils.MakeStrBldr();
			bldrExpected.Replace(0, 0, " last text.", StyleUtils.CharStyleTextProps(null, wsIpa));
			bldrExpected.Replace(0, 0, "replace this", StyleUtils.CharStyleTextProps(style, wsIpa));
			bldrExpected.Replace(0, 0, " more text ", StyleUtils.CharStyleTextProps(null, wsIpa));
			bldrExpected.Replace(0, 0, "replace this", StyleUtils.CharStyleTextProps(style, wsIpa));
			bldrExpected.Replace(0, 0, " more text more text more text more text more text ", StyleUtils.CharStyleTextProps(null, wsIpa));
			bldrExpected.Replace(0, 0, "replace this", StyleUtils.CharStyleTextProps(style, wsIpa));
			bldrExpected.Replace(0, 0, "Initial text ", StyleUtils.CharStyleTextProps(null, wsIpa));
			return bldrExpected.GetString();
		}

		/// <summary>
		/// Test a replace when the replace text contains multiple runs with different writing
		/// systems.
		/// </summary>
		[Test]
		public void ReplaceWSs()
		{
			var ipaWs = Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic");
			var deWs = Cache.WritingSystemFactory.GetWsFromStr("de");
			var frWs = Cache.WritingSystemFactory.GetWsFromStr("fr");
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", ipaWs);
			// Change the replace string in the dialog to have 2 runs with different writing systems.
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "Run 2", StyleUtils.CharStyleTextProps("CStyle1", deWs));
			bldr.Replace(0, 0, "Run 1", StyleUtils.CharStyleTextProps("CStyle1", frWs));
			m_dlg.ReplaceText = bldr.GetString();
			m_dlg.MatchWsCheckboxChecked = true;
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.SimulateReplaceButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 12, 16);
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			// Create string with expected results.
			bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, ", blah, blah!", StyleUtils.CharStyleTextProps(null, ipaWs));
			bldr.Replace(0, 0, "Run 2", StyleUtils.CharStyleTextProps("CStyle1", deWs));
			bldr.Replace(0, 0, "Run 1", StyleUtils.CharStyleTextProps("CStyle1", frWs));
			var expectedTssReplace = bldr.GetString();
			AssertEx.AreTsStringsEqual(expectedTssReplace, m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Test a replace all when finding a match using the replace all button. The selection
		/// is already set to a match, and there's one other match in the document.
		/// </summary>
		[Test]
		public void ReplaceAllWithMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ReplaceText = TsStringUtils.MakeString("Monkey feet", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceAllButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 0);
			var expectedTss = TsStringUtils.MakeString("Monkey feet, Monkey feet, Monkey feet!", Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic"));
			AssertEx.AreTsStringsEqual(expectedTss, m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
			Assert.AreEqual(MatchType.ReplaceAllFinished, m_dlg.m_matchNotFoundType);
			Assert.AreEqual("Finished searching the document and made 3 replacements.", m_dlg.m_matchMsg);
		}

		/// <summary>
		/// Test a replace all when finding no matches.
		/// </summary>
		[Test]
		public void ReplaceAllWithNoMatch()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("Blepharophimosis", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ReplaceText = TsStringUtils.MakeString("Monkey feet", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateReplaceAllButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 0);
			var expectedTss = TsStringUtils.MakeString(m_kTitleText, Cache.WritingSystemFactory.GetWsFromStr("en-fonipa-x-etic"));
			AssertEx.AreTsStringsEqual(expectedTss, m_text[0].Contents);
			// TE-4839: Button always says Close after we're finished.
			Assert.AreEqual(MatchType.NoMatchFound, m_dlg.m_matchNotFoundType);
			Assert.AreEqual("Finished searching the document. The search item was not found.", m_dlg.m_matchMsg);
		}

		/// <summary>
		/// Test that ReplaceAll preserves the selection (at least when the replace string is the same length)
		/// </summary>
		[Test]
		public void ReplaceAllPreservesSelection()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString("ah", m_wsFr.Handle);
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			m_dlg.SimulateFindButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 8, 10); // make sure we have the expected selection beforehand
			m_dlg.FindText = TsStringUtils.MakeString("la", m_wsFr.Handle);
			m_dlg.ReplaceText = TsStringUtils.MakeString("at", m_wsFr.Handle);
			m_dlg.SimulateReplaceAllButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 8, 10); // make sure the selection hasn't changed
			var expectedTss = TsStringUtils.MakeString("Bath, bath, bath!", m_wsIpa.Handle);
			AssertEx.AreTsStringsEqual(expectedTss, m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
			Assert.AreEqual(MatchType.ReplaceAllFinished, m_dlg.m_matchNotFoundType);
			Assert.AreEqual("Finished searching the document and made 3 replacements.", m_dlg.m_matchMsg);
		}

		/// <summary>
		/// Test that ReplaceAll replaces all matches, even if the text length grows considerably
		/// </summary>
		[Test]
		public void ReplaceAllWithGrowingText()
		{
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.FindText = TsStringUtils.MakeString(",", m_wsFr.Handle);
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			m_dlg.SimulateFindButtonClick();
			m_dlg.FindText = TsStringUtils.MakeString("Blah", m_wsFr.Handle);
			m_dlg.ReplaceText = TsStringUtils.MakeString("Monkey feet", m_wsFr.Handle);
			m_dlg.SimulateReplaceAllButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 10, 11);
			var expectedTss = TsStringUtils.MakeString("Monkey feet, Monkey feet, Monkey feet!", m_wsIpa.Handle);
			AssertEx.AreTsStringsEqual(expectedTss, m_text[0].Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
			Assert.AreEqual(MatchType.ReplaceAllFinished, m_dlg.m_matchNotFoundType);
			Assert.AreEqual("Finished searching the document and made 3 replacements.", m_dlg.m_matchMsg);
		}

		/// <summary>
		/// Test that ReplaceAll does not erase free translations when making trivial replacements in multiple segments.
		/// </summary>
		[Test]
		public void ReplaceAll_PreservesFreeTranslationsWhenReplacingInMultipleSegments()
		{
			// Segmentize the find text: Blah. blah. blah!
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.PrevPatternText = null;
			m_dlg.FindText = TsStringUtils.MakeString(",", m_wsFr.Handle);
			m_dlg.ReplaceText = TsStringUtils.MakeString(".", m_wsFr.Handle);
			m_dlg.SimulateReplaceAllButtonClick();
			// Add free translations to segments
			var para = m_text[0];
			const int numSegs = 3;
			Assert.AreEqual(numSegs, para.SegmentsOS.Count, "Each sentence should be a segment.");
			for (var i = 0; i < numSegs; i++)
			{
				para.SegmentsOS[i].FreeTranslation.set_String(m_wsEn, TsStringUtils.MakeString(string.Format("{0}th Free Translation.", i), m_wsEn));
			}
			// Replace All that affects all segments
			m_dlg.FindText = TsStringUtils.MakeString("la", m_wsFr.Handle);
			m_dlg.ReplaceText = TsStringUtils.MakeString("at", m_wsFr.Handle);
			m_dlg.SimulateReplaceAllButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 0);
			var expectedTss = TsStringUtils.MakeString("Bath. bath. bath!", m_wsIpa.Handle);
			AssertEx.AreTsStringsEqual(expectedTss, m_text[0].Contents);
			// Verify that free translations have been preserved
			var segments = m_text[0].SegmentsOS;
			Assert.AreEqual(numSegs, segments.Count, "Replace All should not have changed the segment count.");
			for (var i = 0; i < numSegs; i++)
			{
				expectedTss = TsStringUtils.MakeString(string.Format("{0}th Free Translation.", i), m_wsEn);
				Assert.True(segments[i].FreeTranslation.TryWs(m_wsEn, out var outWs, out var actualTss));
				Assert.AreEqual(m_wsEn, outWs);
				AssertEx.AreTsStringsEqual(expectedTss, actualTss);
			}
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
			Assert.AreEqual(MatchType.ReplaceAllFinished, m_dlg.m_matchNotFoundType);
			Assert.AreEqual("Finished searching the document and made 3 replacements.", m_dlg.m_matchMsg);
		}
		#endregion

		#region Match style tests
		/// <summary>
		/// Test the ability to find occurrences of a character style (with no Find What text
		/// specified). This test finds no match.
		/// </summary>
		[Test]
		public void FindCharStyleWithNoFindText_NoMatch()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			m_dlg.PrevPatternText = null;
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.SimulateFindButtonClick();
			m_dlg.VerifySelection(0, 0, 0, 0, 0);
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
		}

		/// <summary>
		/// Test the ability to find occurrences of a character style (with no Find What text
		/// specified).
		/// </summary>
		[Test]
		public void FindCharStyleWithNoFindText_Match()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle3");
			para.Contents = bldr.GetString();
			m_dlg.PrevPatternText = null;
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 6, 14);
		}

		/// <summary>
		/// Test the ability to replace an occurence of a character style (with no Find What or
		/// Replace With text specified).
		/// </summary>
		[Test]
		public void ReplaceCharStyleWithNoFindText()
		{
			SetupStylesheet();
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle3");
			para.Contents = bldr.GetString();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle2");
			var expectedTss = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_dlg.PrevPatternText = null;
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.ReplaceTextControl.Tss = TsStringUtils.MakeString(string.Empty, Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ApplyStyle(m_dlg.ReplaceTextControl, "CStyle2");
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 6, 14);
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 14, 14);
			AssertEx.AreTsStringsEqual(expectedTss, para.Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Test the ability to replace an occurence of a character style after a footnote
		/// (with no Find What or Replace With text specified). (TE-5323)
		/// </summary>
		[Test]
		public void ReplaceCharStyleAfterFootnote()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			var para = m_text[0];
			AddFootnote(m_genesis, para, 0);
			var bldr = para.Contents.GetBldr();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle3");
			para.Contents = bldr.GetString();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle2");
			var expectedTss = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_dlg.PrevPatternText = null;
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.ReplaceTextControl.Tss = TsStringUtils.MakeString(string.Empty, Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ApplyStyle(m_dlg.ReplaceTextControl, "CStyle2");
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 6, 14);
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 14, 14);
			AssertEx.AreTsStringsEqual(expectedTss, para.Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Test the ability to replace an occurence of a character style after a footnote
		/// (with no Find What or Replace With text specified) when a footnote immediately
		/// follows the the found range. (TE-5323)
		/// </summary>
		[Test]
		public void ReplaceCharStyleBetweenFootnotes()
		{
			SetupStylesheet();
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			var para = m_text[0];
			AddFootnote(m_genesis, para, 0); // Footnote at beginning
			AddFootnote(m_genesis, para, 14); // Footnote after text selection.
			var bldr = para.Contents.GetBldr();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle3");
			para.Contents = bldr.GetString();
			bldr.SetStrPropValue(6, 14, (int)FwTextPropType.ktptNamedStyle, "CStyle2");
			var expectedTss = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_dlg.PrevPatternText = null;
			m_dlg.ApplyStyle(m_dlg.FindTextControl, "CStyle3");
			m_dlg.ReplaceTextControl.Tss = TsStringUtils.MakeString(string.Empty, Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ApplyStyle(m_dlg.ReplaceTextControl, "CStyle2");
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 6, 14);
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 14, 14);
			AssertEx.AreTsStringsEqual(expectedTss, para.Contents);
			// the cancel button should say "close"
			Assert.AreEqual("Close", m_dlg.CloseButton.Text);
		}

		/// <summary>
		/// Test the ability to replace text with a footnote in the middle of the find text.
		/// This is possible because we ignore footnote ORCs when doing the find, but during the
		/// replace, we push the footnotes to the end of the replacement text.
		/// </summary>
		[Test]
		public void ReplaceWhenFoundWithFootnote()
		{
			SetupStylesheet();
			var para = (IStTxtPara)m_text.ParagraphsOS[0];
			AddFootnote(m_genesis, para, 1); // Footnote near beginning
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			var bldr = para.Contents.GetBldr();
			bldr.Replace(2, 6, null, null); // Delete the 'lah,' after the footnote
			bldr.Replace(0, 1, "blah", null); // Add 'blah' before the footnote
			var expectedTss = bldr.GetString();
			m_dlg.PrevPatternText = null;
			m_dlg.FindText = TsStringUtils.MakeString("blah,", m_wsFr.Handle);
			m_dlg.ReplaceText = TsStringUtils.MakeString("blah", m_wsFr.Handle);
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 6);
			m_dlg.SimulateReplaceButtonClick();
			AssertEx.AreTsStringsEqual(expectedTss, para.Contents);
		}

		/// <summary>
		/// Test the ability to replace text with a footnote in the middle of the find text when
		/// also replacing styles. (TE-8761)
		/// </summary>
		[Test]
		public void ReplaceWhenFoundWithFootnote_WithStyles()
		{
			SetupStylesheet();
			var para = (IStTxtPara)m_text.ParagraphsOS[0];
			AddFootnote(m_genesis, para, 1); // Footnote at beginning
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, true, false, null, null, null);
			var bldr = para.Contents.GetBldr();
			bldr.Replace(2, 6, null, null); // Delete the 'lah,' after the footnote
			bldr.Replace(0, 1, "blah", StyleUtils.CharStyleTextProps("CStyle2", m_wsIpa.Handle)); // Add 'blah' before the footnote
			var expectedTss = bldr.GetString();
			m_dlg.PrevPatternText = null;
			m_dlg.FindText = TsStringUtils.MakeString("blah,", m_wsFr.Handle);
			m_dlg.ReplaceTextControl.Tss = TsStringUtils.MakeString("blah", m_wsFr.Handle);
			m_dlg.ApplyStyle(m_dlg.ReplaceTextControl, "CStyle2");
			m_dlg.SimulateReplaceButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 6);
			m_dlg.SimulateReplaceButtonClick();
			AssertEx.AreTsStringsEqual(expectedTss, para.Contents);
		}
		#endregion

		#region Advanced options tests
		/// <summary>
		/// Test an initial search when finding a string whose Writing System matches.
		/// </summary>
		[Test]
		public void FindWithMatchWs_NonEmptyFindText()
		{
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.SetIntPropValues(3, 7, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, Cache.WritingSystemFactory.GetWsFromStr("de"));
			para.Contents = bldr.GetString();
			m_vwPattern.MatchOldWritingSystem = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			m_dlg.FindText = TsStringUtils.MakeString(",", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 4, 5);
		}

		/// <summary>
		/// Test an initial search when finding anything in a given Writing System.
		/// </summary>
		[Test]
		public void FindWithMatchWs_EmptyFindText()
		{
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.SetIntPropValues(3, 7, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, Cache.WritingSystemFactory.GetWsFromStr("de"));
			para.Contents = bldr.GetString();
			m_vwPattern.MatchOldWritingSystem = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			Assert.IsTrue(m_dlg.MatchWsCheckboxChecked);
			m_dlg.ApplyWS(m_dlg.FindTextControl, Cache.WritingSystemFactory.GetWsFromStr("de"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 3, 7);
		}

		/// <summary>
		/// Test an initial search when finding a string whose diacritics match.
		/// </summary>
		[Test]
		public void FindWithMatchDiacritics()
		{
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.Replace(6, 6, "a\u0301", null);
			para.Contents = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_vwPattern.MatchDiacritics = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			Assert.IsTrue(m_dlg.MatchDiacriticsCheckboxChecked);
			// First, search for a base character with no diacritic. Characters in the text
			// that do have diacritics should not be found.
			m_dlg.FindText = TsStringUtils.MakeString("a", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 2, 3);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 10, 11);
			// Next, search for a character with a diacritic. Only characters in the text
			// that have the diacritic should be found.
			m_dlg.FindText = TsStringUtils.MakeString("a\u0301", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 1, 6, 8);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 2, 6, 8);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 6, 8);
			m_dlg.SimulateFindButtonClick();
			Assert.IsFalse(m_dlg.FindEnvironment.FoundMatch);
		}

		/// <summary>
		/// Test an initial search when finding a string which matches on a whole word.
		/// </summary>
		[Test]
		public void FindWithMatchWholeWord()
		{
			var para = m_text[0];
			var bldr = para.Contents.GetBldr();
			bldr.Replace(6, 6, "more", null);
			para.Contents = bldr.GetString();
			m_vwRootsite.RootBox.Reconstruct();
			m_vwRootsite.RootBox.MakeSimpleSel(true, true, false, true);
			m_vwPattern.MatchWholeWord = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			Assert.IsTrue(m_dlg.MatchWholeWordCheckboxChecked);
			m_dlg.FindText = TsStringUtils.MakeString("blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 4);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 16, 20);
		}

		/// <summary>
		/// Test an initial search when finding a string whose case matches.
		/// </summary>
		[Test]
		public void FindWithMatchCase()
		{
			m_vwPattern.MatchCase = true;
			m_dlg.SetDialogValues(Cache, m_vwPattern, m_vwRootsite, false, false, null, null, null);
			Assert.IsTrue(m_dlg.MatchCaseCheckboxChecked);
			m_dlg.FindText = TsStringUtils.MakeString("Blah", Cache.WritingSystemFactory.GetWsFromStr("fr"));
			m_dlg.PrevPatternText = null;
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 0, 0, 4);
			m_dlg.SimulateFindButtonClick();
			Assert.IsTrue(m_dlg.FindEnvironment.FoundMatch);
			m_dlg.VerifySelection(0, 0, 1, 0, 4);
		}
		#endregion
	}
}