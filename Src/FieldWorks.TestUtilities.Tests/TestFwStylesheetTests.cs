// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using NUnit.Framework;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;

namespace FieldWorks.TestUtilities.Tests
{
	/// <summary />
	[TestFixture]
	public class TestFwStylesheetTests
	{
		/// <summary>
		/// Test ability to add a new style and retrieve it.
		/// </summary>
		[Test]
		public void TestAddAndRetrieveStyle()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var hvoNewStyle = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("FirstStyle", "bls", hvoNewStyle, 0, hvoNewStyle, 0, false, false, null);
			Assert.AreEqual(hvoNewStyle, stylesheet.get_NthStyle(0));
		}

		/// <summary>
		/// Test ability to retrieve text props for a named style.
		/// </summary>
		[Test]
		public void TestGetStyleRgch()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var propsBldr = TsStringUtils.MakePropsBldr();
			propsBldr.SetStrPropValue((int)FwTextStringProp.kstpFontFamily, "Times");
			var props1 = propsBldr.GetTextProps();
			propsBldr.SetIntPropValues((int)FwTextPropType.ktptForeColor, (int)FwTextPropVar.ktpvDefault, 256);
			var props2 = propsBldr.GetTextProps();
			var hvoNewStyle1 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("FirstStyle", "bla", hvoNewStyle1, 0, hvoNewStyle1, 0, false, false, props1);
			var hvoNewStyle2 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("SecondStyle", "bla", hvoNewStyle2, 0, hvoNewStyle1, 0, false, false, props2);
			var fEqual = TsTextPropsHelper.PropsAreEqual(props2, stylesheet.GetStyleRgch(0, "SecondStyle"), out var sHowDifferent);
			Assert.IsTrue(fEqual, sHowDifferent);
			fEqual = TsTextPropsHelper.PropsAreEqual(props1, stylesheet.GetStyleRgch(0, "FirstStyle"), out sHowDifferent);
			Assert.IsTrue(fEqual, sHowDifferent);
		}

		/// <summary>
		/// Test ability to retrieve the name of the Next style for the given named style.
		/// </summary>
		[Test]
		public void TestGetNextStyle()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var hvoNewStyle1 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("FirstStyle", "bla", hvoNewStyle1, 0, 0, 0, false, false, null);
			var hvoNewStyle2 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("SecondStyle", "bla", hvoNewStyle2, 0, hvoNewStyle1, 0, false, false, null);
			Assert.AreEqual("FirstStyle", stylesheet.GetNextStyle("SecondStyle"));
		}


		/// <summary>
		/// Test ability to retrieve the name of the Based On style for the given named style.
		/// </summary>
		[Test]
		public void TestGetBasedOnStyle()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var hvoNewStyle1 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("FirstStyle", "bla", hvoNewStyle1, 0, 0, 0, false, false, null);
			var hvoNewStyle2 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("SecondStyle", "bla", hvoNewStyle2, hvoNewStyle1, 0, 0, false, false, null);
			Assert.AreEqual("FirstStyle", stylesheet.GetBasedOn("SecondStyle"));
		}

		/// <summary>
		/// Test ability to override the font size for a single writing system.
		/// </summary>
		[Test]
		public void TestOverrideFontForWritingSystem_ForStyleWithNullProps()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var hvoNewStyle1 = stylesheet.MakeNewStyle();
			stylesheet.PutStyle("FirstStyle", "bla", hvoNewStyle1, 0, 0, 0, false, false, null);

			var wsf = new WritingSystemManager();
			var ws = wsf.get_Engine("de");
			var hvoGermanWs = ws.Handle;
			Assert.IsTrue(hvoGermanWs > 0, "Should have gotten an hvo for the German WS");

			// Array of 1 struct, contains writing system and font size to override
			var fontOverrides = new List<FontOverride>(1);
			FontOverride aFontOverride;
			aFontOverride.writingSystem = hvoGermanWs;
			aFontOverride.fontSize = 48;
			fontOverrides.Add(aFontOverride);
			((TestFwStylesheet)stylesheet).OverrideFontsForWritingSystems("FirstStyle", fontOverrides);

			//check results
			IVwPropertyStore vwps = VwPropertyStoreClass.Create();
			vwps.Stylesheet = stylesheet;
			vwps.WritingSystemFactory = wsf;

			var ttpBldr = TsStringUtils.MakePropsBldr();
			ttpBldr.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, "FirstStyle");
			ttpBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, hvoGermanWs);
			var ttp = ttpBldr.GetTextProps();

			var chrps = vwps.get_ChrpFor(ttp);
			ws.InterpretChrp(ref chrps);

			Assert.AreEqual(48, chrps.dympHeight / 1000);
		}

		/// <summary>
		/// Test ability to override the font size for multiple writing systems, where style
		/// has underlying props set as well.
		/// </summary>
		[Test]
		public void TestOverrideFontsForWritingSystems_ForStyleWithProps()
		{
			IVwStylesheet stylesheet = new TestFwStylesheet();
			var hvoNewStyle1 = stylesheet.MakeNewStyle();

			var propsBldr = TsStringUtils.MakePropsBldr();
			propsBldr.SetStrPropValue((int)FwTextStringProp.kstpFontFamily, "Arial");
			propsBldr.SetIntPropValues((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 23000);
			stylesheet.PutStyle("FirstStyle", "bla", hvoNewStyle1, 0, 0, 0, false, false, propsBldr.GetTextProps());

			var wsf = new WritingSystemManager();
			var wsIngles = wsf.get_Engine("en");
			var hvoInglesWs = wsIngles.Handle;
			Assert.IsTrue(hvoInglesWs > 0, "Should have gotten an HVO for the English WS");

			var wsFrench = wsf.get_Engine("fr");
			var hvoFrenchWs = wsFrench.Handle;
			Assert.IsTrue(hvoFrenchWs > 0, "Should have gotten an HVO for the French WS");

			var wsGerman = wsf.get_Engine("de");
			var hvoGermanWs = wsGerman.Handle;
			Assert.IsTrue(hvoGermanWs > 0, "Should have gotten an HVO for the German WS");

			Assert.IsTrue(hvoFrenchWs != hvoGermanWs, "Should have gotten different HVOs for each WS");
			Assert.IsTrue(hvoInglesWs != hvoGermanWs, "Should have gotten different HVOs for each WS");
			Assert.IsTrue(hvoFrenchWs != hvoInglesWs, "Should have gotten different HVOs for each WS");

			// Array of structs, containing writing systems and font sizes to override.
			var fontOverrides = new List<FontOverride>(2);
			FontOverride aFontOverride;
			aFontOverride.writingSystem = hvoInglesWs;
			aFontOverride.fontSize = 34;
			fontOverrides.Add(aFontOverride);
			aFontOverride.writingSystem = hvoGermanWs;
			aFontOverride.fontSize = 48;
			fontOverrides.Add(aFontOverride);
			((TestFwStylesheet)stylesheet).OverrideFontsForWritingSystems("FirstStyle", fontOverrides);

			//check results
			IVwPropertyStore vwps = VwPropertyStoreClass.Create();
			vwps.Stylesheet = stylesheet;
			vwps.WritingSystemFactory = wsf;

			var ttpBldr = TsStringUtils.MakePropsBldr();
			ttpBldr.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, "FirstStyle");
			ttpBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, hvoFrenchWs);
			var ttpFrench = ttpBldr.GetTextProps();
			ttpBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, hvoGermanWs);
			var ttpGerman = ttpBldr.GetTextProps();
			ttpBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, hvoInglesWs);
			var ttpIngles = ttpBldr.GetTextProps();

			var chrpsFrench = vwps.get_ChrpFor(ttpFrench);
			var chrpsGerman = vwps.get_ChrpFor(ttpGerman);
			var chrpsIngles = vwps.get_ChrpFor(ttpIngles);
			wsFrench.InterpretChrp(ref chrpsFrench);
			wsGerman.InterpretChrp(ref chrpsGerman);
			wsIngles.InterpretChrp(ref chrpsIngles);

			Assert.AreEqual(23, chrpsFrench.dympHeight / 1000);
			Assert.AreEqual(34, chrpsIngles.dympHeight / 1000);
			Assert.AreEqual(48, chrpsGerman.dympHeight / 1000);
		}
	}
}