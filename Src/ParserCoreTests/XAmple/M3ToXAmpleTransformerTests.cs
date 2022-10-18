// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.XPath;
using System.Xml.Xsl;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel.Utils;

namespace SIL.FieldWorks.WordWorks.Parser.XAmple
{
	/// <summary />
	[TestFixture]
	public class M3ToXAmpleTransformerTests
	{
<<<<<<< HEAD:Src/ParserCoreTests/XAmple/M3ToXAmpleTransformerTests.cs
		private string m_sM3FXTDump;
		private string m_sM3FXTCircumfixDump;
		private string m_sM3FXTCircumfixInfixDump;
		private string m_sM3FXTCliticDump;
		private string m_sM3FXTFullRedupDump;
		private string m_sM3FXTConceptualIntroDump;
		private string m_sM3FXTStemNameDump;
		private string m_sM3FXTStemName2Dump;
		private string m_sM3FXTStemName3Dump;
		private string m_sM3FXTRootCliticEnvsDump;
		private string m_sM3FXTCliticEnvsDump;
		private string m_sM3FXTAffixAlloFeatsDump;
		private string m_sM3FXTLatinDump;
		private string m_sM3FXTIrregularlyInflectedFormsDump;
		private readonly Dictionary<string, XPathDocument> m_mapXmlDocs = new Dictionary<string, XPathDocument>();
		private XslCompiledTransform m_adTransform;
		private XslCompiledTransform m_lexTransform;
		private XslCompiledTransform m_gramTransform;
		private bool m_fResultMatchesExpected = true;
||||||| f013144d5:Src/LexText/ParserCore/ParserCoreTests/M3ToXAmpleTransformerTests.cs
		string m_sM3FXTDump;
		string m_sM3FXTCircumfixDump;
		string m_sM3FXTCircumfixInfixDump;
		string m_sM3FXTCliticDump;
		string m_sM3FXTFullRedupDump;
		string m_sM3FXTConceptualIntroDump;
		string m_sM3FXTStemNameDump;
		string m_sM3FXTStemName2Dump;
		string m_sM3FXTStemName3Dump;
		string m_sM3FXTRootCliticEnvsDump;
		string m_sM3FXTCliticEnvsDump;
		string m_sM3FXTAffixAlloFeatsDump;
		string m_sM3FXTLatinDump;
		string m_sM3FXTIrregularlyInflectedFormsDump;
		readonly Dictionary<string, XPathDocument> m_mapXmlDocs = new Dictionary<string, XPathDocument>();

		XslCompiledTransform m_adTransform;
		XslCompiledTransform m_lexTransform;
		XslCompiledTransform m_gramTransform;

		bool m_fResultMatchesExpected = true;

=======
		string m_sM3FXTDump;
		string m_sM3FXTCircumfixDump;
		string m_sM3FXTCircumfixInfixDump;
		string m_sM3FXTCliticDump;
		string m_sM3FXTFullRedupDump;
		string m_sM3FXTConceptualIntroDump;
		string m_sM3FXTStemNameDump;
		string m_sM3FXTStemName2Dump;
		string m_sM3FXTStemName3Dump;
		string m_sM3FXTRootCliticEnvsDump;
		string m_sM3FXTCliticEnvsDump;
		string m_sM3FXTAffixAlloFeatsDump;
		string m_sM3FXTLatinDump;
		string m_sM3FXTIrregularlyInflectedFormsDump;
		private string m_sAbazaOrderClassPlayDump;
		readonly Dictionary<string, XPathDocument> m_mapXmlDocs = new Dictionary<string, XPathDocument>();

		XslCompiledTransform m_adTransform;
		XslCompiledTransform m_lexTransform;
		XslCompiledTransform m_gramTransform;

		bool m_fResultMatchesExpected = true;

>>>>>>> develop:Src/LexText/ParserCore/ParserCoreTests/M3ToXAmpleTransformerTests.cs
		/// <summary>
		/// Location of test files
		/// </summary>
		private string m_sTestPath;

		[OneTimeSetUp]
		public void FixtureSetup()
		{
			m_sTestPath = Path.Combine(FwDirectoryFinder.SourceDirectory, "ParserCoreTests", "XAmple", "M3ToXAmpleTransformerTestsDataFiles");
			SetUpXAmpleTransforms();
			SetUpM3FXTDump();
		}

		private void SetUpM3FXTDump()
		{
			m_sM3FXTDump = Path.Combine(m_sTestPath, "M3FXTDump.xml");
			m_sM3FXTCircumfixDump = Path.Combine(m_sTestPath, "M3FXTCircumfixDump.xml");
			m_sM3FXTCircumfixInfixDump = Path.Combine(m_sTestPath, "M3FXTCircumfixInfixDump.xml");
			m_sM3FXTCliticDump = Path.Combine(m_sTestPath, "CliticParserFxtResult.xml");
			m_sM3FXTFullRedupDump = Path.Combine(m_sTestPath, "M3FXTFullRedupDump.xml");
			m_sM3FXTConceptualIntroDump = Path.Combine(m_sTestPath, "ConceptualIntroTestParserFxtResult.xml");
			m_sM3FXTStemNameDump = Path.Combine(m_sTestPath, "M3FXTStemNameDump.xml");
			m_sM3FXTStemName2Dump = Path.Combine(m_sTestPath, "OrizabaParserFxtResult.xml");
			m_sM3FXTStemName3Dump = Path.Combine(m_sTestPath, "StemName3ParserFxtResult.xml");
			m_sM3FXTRootCliticEnvsDump = Path.Combine(m_sTestPath, "RootCliticEnvParserFxtResult.xml");
			m_sM3FXTCliticEnvsDump = Path.Combine(m_sTestPath, "CliticEnvsParserFxtResult.xml");
			m_sM3FXTAffixAlloFeatsDump = Path.Combine(m_sTestPath, "TestAffixAllomorphFeatsParserFxtResult.xml");
			m_sM3FXTLatinDump = Path.Combine(m_sTestPath, "LatinParserFxtResult.xml");
			m_sM3FXTIrregularlyInflectedFormsDump = Path.Combine(m_sTestPath, "IrregularlyInflectedFormsParserFxtResult.xml");
			m_sAbazaOrderClassPlayDump = Path.Combine(m_sTestPath, "Abaza-OrderclassPlay.xml");

			SetupXmlDocument(m_sM3FXTDump);
			SetupXmlDocument(m_sM3FXTCircumfixDump);
			SetupXmlDocument(m_sM3FXTCircumfixInfixDump);
			SetupXmlDocument(m_sM3FXTCliticDump);
			SetupXmlDocument(m_sM3FXTFullRedupDump);
			SetupXmlDocument(m_sM3FXTConceptualIntroDump);
			SetupXmlDocument(m_sM3FXTStemNameDump);
			SetupXmlDocument(m_sM3FXTStemName2Dump);
			SetupXmlDocument(m_sM3FXTStemName3Dump);
			SetupXmlDocument(m_sM3FXTRootCliticEnvsDump);
			SetupXmlDocument(m_sM3FXTCliticEnvsDump);
			SetupXmlDocument(m_sM3FXTAffixAlloFeatsDump);
			SetupXmlDocument(m_sM3FXTLatinDump);
			SetupXmlDocument(m_sM3FXTIrregularlyInflectedFormsDump);
			SetupXmlDocument(m_sAbazaOrderClassPlayDump);
		}

		private void SetupXmlDocument(string filepath)
		{
			m_mapXmlDocs.Add(filepath, new XPathDocument(filepath));
		}

		private void SetUpXAmpleTransforms()
		{
			SetUpTransform("FxtM3ParserToXAmpleADCtl", out m_adTransform);
			SetUpTransform("FxtM3ParserToXAmpleLex", out m_lexTransform);
			SetUpTransform("FxtM3ParserToToXAmpleGrammar", out m_gramTransform);
		}

		private static void SetUpTransform(string name, out XslCompiledTransform transform)
		{
			transform = M3ToXAmpleTransformer.CreateTransform(name, "ApplicationTransforms");
		}

		/// <summary>
		/// Test creating the AD Control file
		/// </summary>
		[Test]
		public void CreateXAmpleADControlFile()
		{
			ApplyTransform(m_sM3FXTDump, m_adTransform, "TestAdCtl.txt");
			ApplyTransform(m_sM3FXTStemNameDump, m_adTransform, "StemNameTestAdCtl.txt");
			ApplyTransform(m_sM3FXTStemName2Dump, m_adTransform, "Orizabaadctl.txt");
			ApplyTransform(m_sM3FXTStemName3Dump, m_adTransform, "StemName3adctl.txt");
			ApplyTransform(m_sM3FXTCliticDump, m_adTransform, "CliticAdCtl.txt");
			ApplyTransform(m_sM3FXTAffixAlloFeatsDump, m_adTransform, "AffixAlloFeatsAdCtl.txt");
			ApplyTransform(m_sM3FXTLatinDump, m_adTransform, "LatinAdCtl.txt");
			ApplyTransform(m_sM3FXTIrregularlyInflectedFormsDump, m_adTransform, "IrregularlyInflectedFormsAdCtl.txt");
		}

		/// <summary>
		/// Test creating the lexicon file
		/// </summary>
		[Test]
		public void CreateXAmpleLexiconFile()
		{
			ApplyTransform(m_sM3FXTDump, m_lexTransform, "TestLexicon.txt");
			ApplyTransform(m_sM3FXTCircumfixInfixDump, m_lexTransform, "CircumfixInfixLexicon.txt");
			ApplyTransform(m_sM3FXTFullRedupDump, m_lexTransform, "FullRedupLexicon.txt");
			ApplyTransform(m_sM3FXTConceptualIntroDump, m_lexTransform, "ConceptualIntroTestlex.txt");
			ApplyTransform(m_sM3FXTStemNameDump, m_lexTransform, "StemNameTestlex.txt");
			ApplyTransform(m_sM3FXTStemName2Dump, m_lexTransform, "Orizabalex.txt");
			ApplyTransform(m_sM3FXTStemName3Dump, m_lexTransform, "StemName3lex.txt");
			ApplyTransform(m_sM3FXTCliticDump, m_lexTransform, "CliticLexicon.txt");
			ApplyTransform(m_sM3FXTCliticEnvsDump, m_lexTransform, "CliticEnvsLexicon.txt");
			ApplyTransform(m_sM3FXTRootCliticEnvsDump, m_lexTransform, "RootCliticEnvsLexicon.txt");
			ApplyTransform(m_sM3FXTAffixAlloFeatsDump, m_lexTransform, "AffixAlloFeatsLexicon.txt");
			ApplyTransform(m_sM3FXTIrregularlyInflectedFormsDump, m_lexTransform, "IrregularlyInflectedFormsLexicon.txt");
			ApplyTransform(m_sAbazaOrderClassPlayDump, m_lexTransform, "Abaza-OrderclassPlaylex.txt");
		}

		/// <summary>
		/// Test creating the word grammar file
		/// </summary>
		[Test]
		public void CreateXAmpleWordGrammarFile()
		{
			ApplyTransform(m_sM3FXTDump, m_gramTransform, "TestWordGrammar.txt");
			ApplyTransform(m_sM3FXTCircumfixDump, m_gramTransform, "IndonCircumfixWordGrammar.txt");
			ApplyTransform(m_sM3FXTCircumfixInfixDump, m_gramTransform, "CircumfixInfixWordGrammar.txt");
			ApplyTransform(m_sM3FXTStemNameDump, m_gramTransform, "StemNameWordGrammar.txt");
			ApplyTransform(m_sM3FXTStemName2Dump, m_gramTransform, "Orizabagram.txt");
			ApplyTransform(m_sM3FXTStemName3Dump, m_gramTransform, "StemName3gram.txt");
			ApplyTransform(m_sM3FXTCliticDump, m_gramTransform, "CliticWordGrammar.txt");
			ApplyTransform(m_sM3FXTAffixAlloFeatsDump, m_gramTransform, "AffixAlloFeatsWordGrammar.txt");
			ApplyTransform(m_sM3FXTLatinDump, m_gramTransform, "LatinWordGrammar.txt");
		}

		private void ApplyTransform(string sInput, XslCompiledTransform transform, string sExpectedOutput)
		{
			var fxtDump = m_mapXmlDocs[sInput];
			var sOutput = FileUtils.GetTempFile("txt");
			using (var result = new StreamWriter(sOutput))
			{
				transform.Transform(fxtDump, null, result);
				result.Close();
			}
			var sExpectedResult = Path.Combine(m_sTestPath, sExpectedOutput);
			CheckOutputEquals(sExpectedResult, sOutput);
			// by deleting it here instead of a finally block, when it fails, we can see what the result is.
			File.Delete(sOutput);
		}

		private static void CheckOutputEquals(string sExpectedResultFile, string sActualResultFile)
		{
			string sExpected, sActual;
			using (var expected = new StreamReader(sExpectedResultFile))
			{
				sExpected = expected.ReadToEnd();
			}
			using (var actual = new StreamReader(sActualResultFile))
			{
				sActual = actual.ReadToEnd();
			}
			// A non-empty last line in a file from git always ends with '\n' character
			if (sActual.Substring(sActual.Length - 1) != "\n")
			{
				sActual += Environment.NewLine;
			}
			var sb = new StringBuilder();
			sb.Append("Expected file was ");
			sb.AppendLine(sExpectedResultFile);
			sb.Append("Actual file was ");
			sb.AppendLine(sActualResultFile);
			Assert.AreEqual(sExpected, sActual, sb.ToString());
		}

		[TestCase(null, typeof(ArgumentNullException))]
		public void ResolveUri_ThrowsException(string relativeUri, Type expectedException)
		{
			var sut = M3ToXAmpleTransformer.GetResourceResolver("ParserCoreTests");

			Assert.That(() => sut.ResolveUri(null, relativeUri), Throws.InstanceOf(expectedException));
		}

		[Platform(Include = "Linux")]
		[TestCase("/tmp/foo", "bar", "file:///tmp/bar")]
		[TestCase("/tmp/foo/", "bar", "file:///tmp/foo/bar")]
		public void ResolveUri_Linux(string baseUriString, string relativeUri, string resultUri)
		{
			var sut = M3ToXAmpleTransformer.GetResourceResolver("ParserCoreTests");

			var baseUri = baseUriString == null ? null : new Uri(baseUriString);
			Assert.That(sut.ResolveUri(baseUri, relativeUri), Is.EqualTo(new Uri(resultUri)));
		}

		[TestCase(null, "", "res:///")]
		[TestCase(null, "foo", "res://foo")]
		[TestCase(null, "/foo", "res:///foo")]
		[TestCase(null, "/tmp/foo", "res:///tmp/foo")]
		[TestCase(null, @"c:\tmp\foo", "res:///c:/tmp/foo")]
		[TestCase(null, "res:///tmp/foo", "res:///tmp/foo")]
		[TestCase(null, "file:///tmp/foo", "res:///tmp/foo")]
		[TestCase("res:///tmp/foo", "bar", "res:///tmp/bar")]
		[TestCase("file:///tmp/foo", "bar", "file:///tmp/bar")]
		[TestCase("file:///tmp/foo", "res:///bar", "res:///bar")]
		[TestCase("res:///tmp/foo", "file:///bar", "file:///bar")]
		[TestCase(@"c:\tmp\foo", "bar", "file:///c:/tmp/bar")]
		[TestCase("res:///tmp/foo/", "bar", "res:///tmp/foo/bar")]
		[TestCase("file:///tmp/foo/", "bar", "file:///tmp/foo/bar")]
		[TestCase(@"c:\tmp\foo\", "bar", "file:///c:/tmp/foo/bar")]
		[TestCase("file:///tmp/foo/", "res:///bar", "res:///bar")]
		[TestCase("res:///tmp/foo/", "file:///bar", "file:///bar")]
		public void ResolveUri(string baseUriString, string relativeUri, string resultUri)
		{
			var sut = M3ToXAmpleTransformer.GetResourceResolver("ParserCoreTests");

			var baseUri = baseUriString == null ? null : new Uri(baseUriString);
			Assert.That(sut.ResolveUri(baseUri, relativeUri), Is.EqualTo(new Uri(resultUri)));
		}
	}
}