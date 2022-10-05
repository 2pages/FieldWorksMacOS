// Copyright (c) 2016-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Xml.Linq;
using LanguageExplorer;
using NUnit.Framework;

namespace LanguageExplorerTests
{
	[TestFixture]
	public class LayoutKeyUtilsTests
	{
		[Test]
		public void GetSuffixedPartOfNamedViewOrDuplicateNodeWorks()
		{
			string[] keyAttributes = { null, null, "name", null };
			string[] keyValues = { null, null, "test#1", null};
			// SUT
			var suffix = LayoutKeyUtils.GetSuffixedPartOfNamedViewOrDuplicateNode(keyAttributes, keyValues, out var stdKeyValues);
			Assert.That(suffix, Does.Match("#1"));
			Assert.That(stdKeyValues[2], Does.Match("test"));
			keyValues[2] = "test%01";
			suffix = LayoutKeyUtils.GetSuffixedPartOfNamedViewOrDuplicateNode(keyAttributes, keyValues, out stdKeyValues);
			Assert.That(suffix, Does.Match("%01"));
			Assert.That(stdKeyValues[2], Does.Match("test"));
			keyValues[2] = "test_1";
			suffix = LayoutKeyUtils.GetSuffixedPartOfNamedViewOrDuplicateNode(keyAttributes, keyValues, out stdKeyValues);
			Assert.That(suffix, Does.Match("_1"));
			Assert.That(stdKeyValues[2], Does.Match("test"));
			keyValues[2] = "test_AsPara#Stem01";
			suffix = LayoutKeyUtils.GetSuffixedPartOfNamedViewOrDuplicateNode(keyAttributes, keyValues, out stdKeyValues);
			Assert.That(suffix, Does.Match("#Stem01"));
			Assert.That(stdKeyValues[2], Does.Match("test_AsPara"));
			keyValues[2] = "test-en";
			suffix = LayoutKeyUtils.GetSuffixedPartOfNamedViewOrDuplicateNode(keyAttributes, keyValues, out stdKeyValues);
			Assert.That(suffix, Does.Match("en"));
			Assert.That(stdKeyValues[2], Does.Match("test"));
		}

		[Test]
		public void GetPossibleParamSuffixWorks()
		{
			var element = XDocument.Parse("<part ref=\"RootSubEntryTypeConfig\" param=\"publishRootSubEntryType_1\" hideConfig=\"true\" dup=\"1\" />");
			var suffix = LayoutKeyUtils.GetPossibleParamSuffix(element.Root);
			Assert.That(suffix, Does.Match("_1"));

			element = XDocument.Parse("<part ref=\"RootSubEntryTypeConfig\" param=\"publishRootSubEntryType_%01_1\" hideConfig=\"true\" dup=\"1\" />");
			suffix = LayoutKeyUtils.GetPossibleParamSuffix(element.Root);
			Assert.That(suffix, Does.Match("%01_1"));
		}
	}
}
