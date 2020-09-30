// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Text;

namespace LanguageExplorerTests.Controls.DetailControls
{
	/// <summary>
	/// Test functions of the Sandbox MorphemeBreaker class.
	/// </summary>
	[TestFixture]
	public class MorphemeBreakerTest : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private FlexComponentParameters _flexComponentParameters;

		public override void TestSetup()
		{
			base.TestSetup();

			_flexComponentParameters = TestSetupServices.SetupEverything(Cache, false);
		}
		public override void TestTearDown()
		{
			try
			{
				TestSetupServices.DisposeTrash(_flexComponentParameters);
				_flexComponentParameters = null;
			}
			catch (Exception err)
			{
				throw new Exception($"Error in running {GetType().Name} TestTearDown method.", err);
			}
			finally
			{
				base.TestTearDown();
			}
		}

		[Test]
		public void Phrase_BreakIntoMorphs()
		{
			// Test word breaks on a standard wordform.
			const string baseWord1 = "xxxpus";
			const string baseWord1_morphs1 = "xxxpus";
			var morphs = MorphemeBreaker.BreakIntoMorphs(baseWord1_morphs1, baseWord1);
			Assert.AreEqual(1, morphs.Count, $"Unexpected number of morphs in string '{baseWord1_morphs1}' compared to baseWord '{baseWord1}'.");
			Assert.AreEqual("xxxpus", morphs[0]);

			const string baseWord1_morphs2 = "xxxpu -s";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord1_morphs2, baseWord1);
			Assert.AreEqual(2, morphs.Count, $"Unexpected number of morphs in string '{baseWord1_morphs2}' compared to baseWord '{baseWord1}'.");
			Assert.AreEqual("xxxpu", morphs[0]);
			Assert.AreEqual("-s", morphs[1]);

			const string baseWord1_morphs3 = "xxx pu -s";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord1_morphs3, baseWord1);
			Assert.AreEqual(3, morphs.Count, $"Unexpected number of morphs in string '{baseWord1_morphs3}' compared to baseWord '{baseWord1}'.");
			Assert.AreEqual("xxx", morphs[0]);
			Assert.AreEqual("pu", morphs[1]);
			Assert.AreEqual("-s", morphs[2]);

			// Test word breaks on a phrase wordform.
			const string baseWord2 = "xxxpus xxxyalola";
			const string baseWord2_morphs1 = "pus xxxyalola";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord2_morphs1, baseWord2);
			Assert.AreEqual(1, morphs.Count, $"Unexpected number of morphs in string '{baseWord2_morphs1}' compared to baseWord '{baseWord2}'.");
			Assert.AreEqual("pus xxxyalola", morphs[0]);

			const string baseWord2_morphs2 = "xxxpus xxxyalo  -la";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord2_morphs2, baseWord2);
			Assert.AreEqual(2, morphs.Count, $"Unexpected number of morphs in string '{baseWord2_morphs2}' compared to baseWord '{baseWord2}'.");
			Assert.AreEqual("xxxpus xxxyalo", morphs[0]);
			Assert.AreEqual("-la", morphs[1]);

			const string baseWord2_morphs3 = "xxxpus  xxxyalo  -la";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord2_morphs3, baseWord2);
			Assert.AreEqual(3, morphs.Count, $"Unexpected number of morphs in string '{baseWord2_morphs3}' compared to baseWord '{baseWord2}'.");
			Assert.AreEqual("xxxpus", morphs[0]);
			Assert.AreEqual("xxxyalo", morphs[1]);
			Assert.AreEqual("-la", morphs[2]);

			const string baseWord3 = "xxxnihimbilira xxxpus xxxyalola";
			const string baseWord3_morphs1 = "xxxnihimbilira xxxpus xxxyalola";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord3_morphs1, baseWord3);
			Assert.AreEqual(1, morphs.Count, $"Unexpected number of morphs in string '{baseWord3_morphs1}' compared to baseWord '{baseWord3}'.");
			Assert.AreEqual("xxxnihimbilira xxxpus xxxyalola", morphs[0]);

			const string baseWord3_morphs2 = "xxxnihimbili  -ra  xxxpus xxxyalola";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord3_morphs2, baseWord3);
			Assert.AreEqual(3, morphs.Count, $"Unexpected number of morphs in string '{baseWord3_morphs2}' compared to baseWord '{baseWord3}'.");
			Assert.AreEqual("xxxnihimbili", morphs[0]);
			Assert.AreEqual("-ra", morphs[1]);
			Assert.AreEqual("xxxpus xxxyalola", morphs[2]);

			const string baseWord4 = "xxxpus xxxyalola xxxnihimbilira";
			const string baseWord4_morphs1 = "xxxpus xxxyalola xxxnihimbilira";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord4_morphs1, baseWord4);
			Assert.AreEqual(1, morphs.Count, $"Unexpected number of morphs in string '{baseWord4_morphs1}' compared to baseWord '{baseWord4}'.");
			Assert.AreEqual("xxxpus xxxyalola xxxnihimbilira", morphs[0]);

			const string baseWord4_morphs2 = "xxxpus  xxxyalola xxxnihimbilira";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord4_morphs2, baseWord4);
			Assert.AreEqual(2, morphs.Count, $"Unexpected number of morphs in string '{baseWord4_morphs2}' compared to baseWord '{baseWord4}'.");
			Assert.AreEqual("xxxpus", morphs[0]);
			Assert.AreEqual("xxxyalola xxxnihimbilira", morphs[1]);

			const string baseWord5 = "kicked the bucket";
			const string baseWord5_morphs2 = "kick the bucket  -ed";
			morphs = MorphemeBreaker.BreakIntoMorphs(baseWord5_morphs2, baseWord5);
			Assert.AreEqual(2, morphs.Count, $"Unexpected number of morphs in string '{baseWord5_morphs2}' compared to baseWord '{baseWord5}'.");
			Assert.AreEqual("kick the bucket", morphs[0]);
			Assert.AreEqual("-ed", morphs[1]);
		}

		[Test]
		public void EstablishDefaultEntry_Empty_Basic()
		{
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var morph = Cache.ServiceLocator.GetInstance<IMoAffixAllomorphFactory>().Create();
			entry.LexemeFormOA = morph;
			morph.Form.set_String(Cache.DefaultVernWs, "here");
			morph.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>().GetObject(MoMorphTypeTags.kguidMorphSuffix);
			using (var testSandbox = new SandboxForTests(Cache, InterlinLineChoices.DefaultChoices(Cache.LangProject, Cache.DefaultVernWs, Cache.DefaultAnalWs)))
			{
				testSandbox.InitializeFlexComponent(_flexComponentParameters);
				testSandbox.RawWordform = TsStringUtils.MakeString("here", Cache.DefaultVernWs);
				Assert.DoesNotThrow(() => testSandbox.EstablishDefaultEntry(morph.Hvo, "here", morph.MorphTypeRA, false));
				Assert.DoesNotThrow(() => testSandbox.EstablishDefaultEntry(morph.Hvo, "notHere", morph.MorphTypeRA, false));
			}
		}
	}
}