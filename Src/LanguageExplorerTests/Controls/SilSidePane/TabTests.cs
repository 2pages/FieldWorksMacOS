// SilSidePane, Copyright 2009-2019 SIL International. All rights reserved.
// SilSidePane is licensed under the Code Project Open License (CPOL), <http://www.codeproject.com/info/cpol10.aspx>.
// Derived from OutlookBar v2 2005 <http://www.codeproject.com/KB/vb/OutlookBar.aspx>, Copyright 2007 by Star Vega.
// Changed in 2008 and 2009 by SIL International to convert to C# and add more functionality.

using LanguageExplorer.Controls.SilSidePane;
using NUnit.Framework;

namespace LanguageExplorerTests.Controls.SilSidePane
{
	[TestFixture]
	public class TabTests
	{
		[Test]
		public void TabTest_basic()
		{
			Assert.DoesNotThrow(() => new Tab("name"));
			Assert.DoesNotThrow(() => new Tab(string.Empty));
		}
	}
}