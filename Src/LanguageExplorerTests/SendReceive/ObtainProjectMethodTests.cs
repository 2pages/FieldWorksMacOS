// Copyright (c) 2013-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.IO;
using System.Reflection;
using System.Xml;
using LanguageExplorer.SendReceive;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Utils;

namespace LanguageExplorerTests.SendReceive
{
	/// <summary>
	/// This is the beginning of tests of the ObtainProjectMethod. Only part of it is tested.
	/// </summary>
	[TestFixture]
	public class ObtainProjectMethodTests
	{
		/// <summary>
		/// Basic test of scanning LIFT file for writing systems
		/// </summary>
		[Test]
		public void DefaultWritingSystemsFromLift_FindsCorrectWritingSystemsFromDefn()
		{
			const string input = @"<?xml version='1.0' encoding='utf-8'?>
<lift
	version='0.13'
	producer='SIL.FLEx 7.2.5.41073'>
	<entry
		dateCreated='2012-06-12T18:41:19Z'
		id='bɛben_00ff1845-1d48-47cc-b9f4-cd8834bc70e0'
		guid='00ff1845-1d48-47cc-b9f4-cd8834bc70e0'>
		<lexical-unit>
			<form
				lang='mfo'>
				<text>bɛben</text>
			</form>
		</lexical-unit>
		<trait
			name='morph-type'
			value='stem' />
		<sense
			id='6b800abe-c349-4f6a-8ece-0c03f6203b84'>
			<grammatical-info
				value='Noun'></grammatical-info>
			<definition>
				<form
					lang='sp'>
					<text>dance, music</text>
				</form>
			</definition>
		</sense>
	</entry>
</lift>";
			using (var stringReader = new StringReader(input))
			using (var reader = XmlReader.Create(stringReader))
			{
				ObtainProjectMethod.RetrieveDefaultWritingSystemIdsFromLift(reader, out var vernWs, out var analysisWs);
				reader.Close();
				Assert.That(vernWs, Is.EqualTo("mfo"));
				Assert.That(analysisWs, Is.EqualTo("sp"));
			}
		}

		/// <summary>
		/// Should also be able to get default analysis from gloss
		/// </summary>
		[Test]
		public void DefaultWritingSystemsFromLift_FindsCorrectWritingSystemsFromGloss()
		{
			const string input = @"<?xml version='1.0' encoding='utf-8'?>
<lift
	version='0.13'
	producer='SIL.FLEx 7.2.5.41073'>
	<entry
		dateCreated='2012-06-12T18:41:19Z'
		id='bɛben_00ff1845-1d48-47cc-b9f4-cd8834bc70e0'
		guid='00ff1845-1d48-47cc-b9f4-cd8834bc70e0'>
		<lexical-unit>
			<form
				lang='xyz'>
				<text>bɛben</text>
			</form>
		</lexical-unit>
		<trait
			name='morph-type'
			value='stem' />
		<sense
			id='6b800abe-c349-4f6a-8ece-0c03f6203b84'>
			<grammatical-info
				value='Noun'></grammatical-info>
			<gloss>
				<form
					lang='qed'>
					<text>dance, music</text>
				</form>
			</gloss>
		</sense>
	</entry>
</lift>";
			using (var stringReader = new StringReader(input))
			using (var reader = XmlReader.Create(stringReader))
			{
				ObtainProjectMethod.RetrieveDefaultWritingSystemIdsFromLift(reader, out var vernWs, out var analysisWs);
				reader.Close();
				Assert.That(vernWs, Is.EqualTo("xyz"));
				Assert.That(analysisWs, Is.EqualTo("qed"));
			}
		}

		/// <summary>
		/// May as well do this since that's our default anyway.
		/// </summary>
		[Test]
		public void DefaultWritingSystemsFromLiftWithNoEntries_ReturnsFrenchAndEnglish()
		{
			const string input = @"<?xml version='1.0' encoding='utf-8'?>
<lift
	version='0.13'
	producer='SIL.FLEx 7.2.5.41073'>
</lift>";
			using (var stringReader = new StringReader(input))
			using (var reader = XmlReader.Create(stringReader))
			{
				ObtainProjectMethod.RetrieveDefaultWritingSystemIdsFromLift(reader, out var vernWs, out var analysisWs);
				reader.Close();
				Assert.That(vernWs, Is.EqualTo("fr"));
				Assert.That(analysisWs, Is.EqualTo("en"));
			}
		}

		/// <summary />
		[Test]
		public void CallImportLexiconObtained_ImportObtainedLexiconCanBeFound()
		{
			const string ImportLexiconDll = @"LanguageExplorer.dll";
			const string ImportLexiconClass = @"LanguageExplorer.SendReceive.SendReceiveMenuManager";
			const string ImportLexiconMethod = @"ImportObtainedLexicon";
			var type = ReflectionHelper.GetType(ImportLexiconDll, ImportLexiconClass);
			Assert.NotNull(type, $"'{ImportLexiconClass}' has moved.");
			var method = type.GetMethod(ImportLexiconMethod, BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(method, $"'{ImportLexiconMethod}' method name on '{ImportLexiconClass}' has changed.");
			var parameterInfo = method.GetParameters();
			Assert.AreEqual(parameterInfo.Length, 3, "Wrong number of parameters.");
			Assert.AreEqual(parameterInfo[0].ParameterType.Name, "LcmCache", "Wrong class name for parameter.");
			Assert.AreEqual(parameterInfo[1].ParameterType.Name, "String", "Wrong class name for parameter.");
			Assert.AreEqual(parameterInfo[2].ParameterType.Name, "Form", "Wrong class name for parameter.");
		}
	}
}
