// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using FieldWorks.TestUtilities;
using LanguageExplorer.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Base class for tests that use <see cref="SimpleRootSite"/>. This class is specific for
	/// Rootsite tests.
	/// </summary>
	public class SimpleRootsiteTestsBase<T> where T : IRealDataCache, new()
	{
		#region Data members

		/// <summary />
		protected FlexComponentParameters _flexComponentParameters;
		/// <summary>The data cache</summary>
		protected T m_cache;

		/// <summary>The draft form</summary>
		internal SimpleBasicView BasicView { get; set; }

		/// <summary />
		protected int m_hvoRoot;
		/// <summary>Fragment for view constructor</summary>
		protected int m_frag = 1;

		/// <summary>Text for the first and third test paragraph (French)</summary>
		internal const string kFirstParaFra = "C'est une paragraph en francais.";
		/// <summary>Text for the second and fourth test paragraph (French).</summary>
		/// <remarks>This text needs to be shorter than the text for the first para!</remarks>
		internal const string kSecondParaFra = "C'est une deuxieme paragraph.";

		/// <summary>Writing System Manager (reset for each test)</summary>
		protected WritingSystemManager m_wsManager;
		/// <summary>Id of English Writing System (reset for each test)</summary>
		protected int m_wsEng;
		/// <summary>Id of French Writing System (reset for each test)</summary>
		protected int m_wsFrn;
		/// <summary>Id of German Writing System (reset for each test)</summary>
		protected int m_wsDeu;
		/// <summary>Id of User Writing System (reset for each test)</summary>
		protected int m_wsUser;
		#endregion

		/// <summary>
		/// Fixture setup
		/// </summary>
		[TestFixtureSetUp]
		public virtual void FixtureSetup()
		{
			m_cache = new T
			{
				MetaDataCache = MetaDataCache.CreateMetaDataCache("TextCacheModel.xml"),
				ParaContentsFlid = SimpleRootsiteTestsConstants.kflidParaContents,
				ParaPropertiesFlid = SimpleRootsiteTestsConstants.kflidParaProperties,
				TextParagraphsFlid = SimpleRootsiteTestsConstants.kflidTextParas
			};

			Debug.Assert(m_wsManager == null);
			m_wsManager = new WritingSystemManager();
			m_cache.WritingSystemFactory = m_wsManager;

			m_wsManager.GetOrSet("en", out var enWs);
			m_wsEng = enWs.Handle;

			m_wsManager.GetOrSet("fr", out var frWs);
			m_wsFrn = frWs.Handle;

			m_wsManager.GetOrSet("de", out var deWs);
			m_wsDeu = deWs.Handle;

			m_wsManager.UserWs = m_wsEng;
			m_wsUser = m_wsManager.UserWs;
		}

		/// <summary>
		/// Teardown
		/// </summary>
		[TestFixtureTearDown]
		public virtual void FixtureTeardown()
		{
			FileUtils.Manager.Reset();

			// GrowToWord causes a Char Property Engine to be created, and the test runner
			// fails if we don't shut the factory down.
			m_cache.Dispose();
			m_cache = default;
		}

		/// <summary>
		/// Create a new basic view
		/// </summary>
		[SetUp]
		public virtual void TestSetup()
		{
			m_cache.ClearAllData();
			m_hvoRoot = m_cache.MakeNewObject(SimpleRootsiteTestsConstants.kclsidProject, 0, -1, -1);

			var styleSheet = new SimpleStyleSheet(m_cache);

			Assert.IsNull(BasicView);
			BasicView = new SimpleBasicView
			{
				Cache = m_cache,
				Visible = false,
				StyleSheet = styleSheet,
				WritingSystemFactory = m_wsManager
			};
			_flexComponentParameters = TestSetupServices.SetupTestTriumvirate();
			BasicView.InitializeFlexComponent(_flexComponentParameters);
		}

		/// <summary>
		/// Shuts down the view
		/// </summary>
		/// <remarks>This method is called after each test</remarks>
		[TearDown]
		public virtual void TestTearDown()
		{
			BasicView.Dispose();
			BasicView = null;
			TestSetupServices.DisposeTrash(_flexComponentParameters);
			_flexComponentParameters = null;
		}

		/// <summary>
		/// Set up the test form.
		/// </summary>
		protected void ShowForm(DisplayType display)
		{
			BasicView.MyDisplayType = display;

			// We don't actually want to show it, but we need to force the view to create the root
			// box and lay it out so that various test stuff can happen properly.
			BasicView.Width = 300;
			BasicView.Height = 307 - 25;
			BasicView.MakeRoot(m_hvoRoot, SimpleRootsiteTestsConstants.kflidDocFootnotes, m_frag, m_wsEng);
			BasicView.CallLayout();
			BasicView.AutoScrollPosition = new Point(0, 0);
			BasicView.Visible = true;
		}

		/// <summary>
		/// Insert the specified paragraphs and show the dialog
		/// </summary>
		protected void ShowForm(Lng lng, DisplayType display)
		{
			if ((lng & Lng.English) == Lng.English)
			{
				MakeEnglishParagraphs();
			}
			if ((lng & Lng.French) == Lng.French)
			{
				MakeFrenchParagraphs();
			}
			if ((lng & Lng.UserWs) == Lng.UserWs)
			{
				MakeUserWsParagraphs();
			}
			if ((lng & Lng.Empty) == Lng.Empty)
			{
				MakeEmptyParagraphs();
			}
			if ((lng & Lng.Mixed) == Lng.Mixed)
			{
				MakeMixedWsParagraph();
			}
			ShowForm(display);
		}

		/// <summary>
		/// Add English paragraphs
		/// </summary>
		protected void MakeEnglishParagraphs()
		{
			AddParagraphs(m_wsEng, SimpleBasicView.kFirstParaEng, SimpleBasicView.kSecondParaEng);
		}

		/// <summary>
		/// Add French paragraphs
		/// </summary>
		protected void MakeFrenchParagraphs()
		{
			AddParagraphs(m_wsFrn, kFirstParaFra, kSecondParaFra);
		}

		/// <summary>
		/// Add paragraphs with the user interface writing system
		/// </summary>
		protected void MakeUserWsParagraphs()
		{
			AddParagraphs(m_wsUser, "blabla", "abc");
		}

		/// <summary>
		/// Adds a run of text to the specified paragraph
		/// </summary>
		public void AddRunToMockedPara(int hvoPara, string runText, int ws)
		{
			var runStyle = TsStringUtils.MakeProps(null, ws);
			var contents = m_cache.get_StringProp(hvoPara, SimpleRootsiteTestsConstants.kflidParaContents);
			var bldr = contents.GetBldr();
			bldr.Replace(bldr.Length, bldr.Length, runText, runStyle);
			m_cache.SetString(hvoPara, SimpleRootsiteTestsConstants.kflidParaContents, bldr.GetString());
		}

		/// <summary>
		/// Makes a paragraph containing runs, each of which has a different writing system.
		/// </summary>
		protected void MakeMixedWsParagraph()
		{
			var para = AddFootnoteAndParagraph();

			AddRunToMockedPara(para, "ws1", m_wsEng);
			AddRunToMockedPara(para, "ws2", m_wsDeu);
			AddRunToMockedPara(para, "ws3", m_wsFrn);
		}

		/// <summary>
		/// Add empty paragraphs
		/// </summary>
		protected void MakeEmptyParagraphs()
		{
			AddParagraphs(m_wsUser, "", "");
		}

		/// <summary>
		/// Adds a footnote with a single paragraph to the cache
		/// </summary>
		/// <returns>HVO of the new paragraph</returns>
		protected int AddFootnoteAndParagraph()
		{
			var cTexts = m_cache.get_VecSize(m_hvoRoot, SimpleRootsiteTestsConstants.kflidDocFootnotes);
			var hvoFootnote = m_cache.MakeNewObject(SimpleRootsiteTestsConstants.kclsidStFootnote, m_hvoRoot, SimpleRootsiteTestsConstants.kflidDocFootnotes, cTexts);
			var hvoPara = m_cache.MakeNewObject(SimpleRootsiteTestsConstants.kclsidStTxtPara, hvoFootnote, SimpleRootsiteTestsConstants.kflidTextParas, 0);
			m_cache.CacheStringProp(hvoFootnote, SimpleRootsiteTestsConstants.kflidFootnoteMarker, TsStringUtils.MakeString("a", m_wsFrn));
			m_cache.CacheStringProp(hvoPara, SimpleRootsiteTestsConstants.kflidParaContents, TsStringUtils.MakeString(string.Empty, m_wsFrn));
			return hvoPara;
		}

		/// <summary>
		/// Adds two footnotes and their paragraphs to the cache
		/// </summary>
		/// <param name="ws">The writing system ID</param>
		/// <param name="firstPara">Text of the first paragraph</param>
		/// <param name="secondPara">Text of the second paragraph</param>
		private void AddParagraphs(int ws, string firstPara, string secondPara)
		{
			var para1 = AddFootnoteAndParagraph();
			var para2 = AddFootnoteAndParagraph();
			AddRunToMockedPara(para1, firstPara, ws);
			AddRunToMockedPara(para2, secondPara, ws);
		}

		/// <summary>Defines the possible languages</summary>
		[Flags]
		protected enum Lng
		{
			/// <summary>No paragraphs</summary>
			None = 0,
			/// <summary>English paragraphs</summary>
			English = 1,
			/// <summary>French paragraphs</summary>
			French = 2,
			/// <summary>UserWs paragraphs</summary>
			UserWs = 4,
			/// <summary>Empty paragraphs</summary>
			Empty = 8,
			/// <summary>Paragraph with 3 writing systems</summary>
			Mixed = 16,
		}

		private sealed class SimpleStyleSheet : IVwStylesheet
		{
			public SimpleStyleSheet(ISilDataAccess da)
			{
				DataAccess = da;
			}

			#region IVwStylesheet Members
			public int CStyles => 1;

			public void CacheProps(int cch, string rgchName, int hvoStyle, ITsTextProps ttp)
			{
				throw new NotSupportedException();
			}

			public ISilDataAccess DataAccess { get; }

			public void Delete(int hvoStyle)
			{
				throw new NotSupportedException();
			}

			public string GetBasedOn(string bstrName)
			{
				return string.Empty;
			}

			public int GetContext(string bstrName)
			{
				return 0;
			}

			public string GetDefaultBasedOnStyleName()
			{
				return "Normal";
			}

			public string GetDefaultStyleForContext(int nContext, bool fCharStyle)
			{
				return "Normal";
			}

			public string GetNextStyle(string bstrName)
			{
				return "Normal";
			}

			public ITsTextProps GetStyleRgch(int cch, string rgchName)
			{
				return TsStringUtils.MakeProps(null, 0);
			}

			public int GetType(string bstrName)
			{
				return 0;
			}

			public bool IsBuiltIn(string bstrName)
			{
				return true;
			}

			public bool IsModified(string bstrName)
			{
				return false;
			}

			public int MakeNewStyle()
			{
				throw new NotSupportedException();
			}

			public ITsTextProps NormalFontStyle => TsStringUtils.MakeProps(null, 0);

			public void PutStyle(string bstrName, string bstrUsage, int hvoStyle, int hvoBasedOn, int hvoNext, int nType, bool fBuiltIn, bool fModified, ITsTextProps ttp)
			{
				throw new NotSupportedException();
			}

			public bool get_IsStyleProtected(string bstrName)
			{
				return true;
			}

			public int get_NthStyle(int ihvo)
			{
				return 1234;
			}

			public string get_NthStyleName(int ihvo)
			{
				return "Normal";
			}
			#endregion
		}
	}
}