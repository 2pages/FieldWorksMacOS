// Copyright (c) 2013-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using LanguageExplorer.Areas.TextsAndWords.Discourse;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Utils;
using SIL.WritingSystems;

namespace LanguageExplorerTests.Areas.TextsAndWords.Discourse
{

	/// <summary/>
	public class FakeConstituentChart : ConstituentChart
	{
		/// <summary/>
		public FakeConstituentChart(LcmCache cache) : base(cache)
		{
		}

		/// <summary/>
		public int m_SetHeaderColAndButtonWidths_callCount = 0;

		/// <summary>
		/// Just keep track of calls.
		/// </summary>
		protected override void SetHeaderColAndButtonWidths()
		{
			m_SetHeaderColAndButtonWidths_callCount++;
		}
	}

	/// <summary/>
	[TestFixture]
	public class ConstituentChartTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		#region Overrides of LcmTestBase
		public override void FixtureSetup()
		{
			if (!Sldr.IsInitialized)
			{
				// initialize the SLDR
				Sldr.Initialize();
			}

			base.FixtureSetup();
		}

		public override void FixtureTeardown()
		{
			base.FixtureTeardown();

			if (Sldr.IsInitialized)
			{
				Sldr.Cleanup();
			}
		}
		#endregion

		/// <summary/>
		[Test]
		public void Basic()
		{
			using(var chart = new ConstituentChart(Cache))
			{
			}
		}

		/// <summary/>
		[Test]
		public void LayoutIgnoredDuringColumnResizing()
		{
			using (var chart = new FakeConstituentChart(Cache))
			{
				// Reset count
				chart.m_SetHeaderColAndButtonWidths_callCount = 0;
				ReflectionHelper.CallMethod(chart, "m_headerMainCols_ColumnWidthChanging", new object[] { null, null });
				ReflectionHelper.CallMethod(chart, "m_headerMainCols_Layout", new object[] { null, null });
				Assert.That(chart.m_SetHeaderColAndButtonWidths_callCount, Is.EqualTo(0), "Layout event during column resizing should have done nothing.");
			}
		}
	}
}
