// Copyright (c) 2014-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Reflection;
using System.Text;
using LanguageExplorer.Dumpster;
using NUnit.Framework;

namespace LanguageExplorerTests.Dumpster
{
	/// <summary>
	/// Beginnings of tests for FlexBridgeListener
	/// </summary>
	[TestFixture]
	public class FlexBridgeListenerTests
	{

		[Test]
		public void ConvertFlexNotesToLift_ConvertsRefs()
		{
			string input = @"	<annotation
		class='question'
		ref='silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=6b466f54-f88a-42f6-b770-aca8fee5734c&amp;tag=&amp;id=6b466f54-f88a-42f6-b770-aca8fee5734c&amp;label=bother'
		guid='10fa26c9-ce35-4341-8a30-c1aa1250d0e0'>
		<message
			author='WhoAmI?'
			status=''
			date='2013-01-28T08:51:40Z'
			guid='25f47900-f6f6-4288-89ac-44f738e63431'>Is this the strongest expression of annoyance?</message>
	</annotation>
".Replace("'", "\"");

			string expected = @"	<annotation
		class='question'
		ref='lift://Fred.lift?type=entry&amp;label=bother&amp;id=6b466f54-f88a-42f6-b770-aca8fee5734c'
		guid='10fa26c9-ce35-4341-8a30-c1aa1250d0e0'>
		<message
			author='WhoAmI?'
			status=''
			date='2013-01-28T08:51:40Z'
			guid='25f47900-f6f6-4288-89ac-44f738e63431'>Is this the strongest expression of annoyance?</message>
	</annotation>
".Replace("'", "\"");

			var builder = new StringBuilder();
			using (var reader = new StringReader(input))
			using (var writer = new StringWriter(builder))
			{
				FLExBridgeListener.ConvertFlexNotesToLift(reader, writer, "Fred.lift");
			}

			Assert.That(builder.ToString(), Is.EqualTo(expected));
		}

		/// <summary>
		/// Make sure FLExBridgeListener can use Reflection to call OpenNewProject on the FieldWorks static class.
		/// </summary>
		[Test]
		public void MakeSureOpenNewProjectMethodHasNotMoved()
		{
			var fwAssembly = Assembly.LoadFrom("FieldWorks.exe");
			Assert.IsNotNull(fwAssembly, "Somebody deleted the main 'FieldWorks.exe'.");
			var fwType = fwAssembly.GetType("SIL.FieldWorks.FieldWorks");
			Assert.IsNotNull(fwType, "Somebody deleted the main 'FieldWorks' class.");
			var methodInfo = fwType.GetMethod("OpenNewProject", BindingFlags.Static | BindingFlags.Public);
			Assert.IsNotNull(methodInfo);
			Assert.IsNotNull(fwType, "Somebody deleted the 'OpenNewProject' static method from the main 'FieldWorks' class.");
		}

		[Test]
		public void ConvertLiftNotesToFlex_ConvertsRefs()
		{
			string input = @"	<annotation
		class='question'
		ref='lift://Fred.lift?type=entry&amp;label=bother&amp;id=6b466f54-f88a-42f6-b770-aca8fee5734c'
		guid='10fa26c9-ce35-4341-8a30-c1aa1250d0e0'>
		<message
			author='WhoAmI?'
			status=''
			date='2013-01-28T08:51:40Z'
			guid='25f47900-f6f6-4288-89ac-44f738e63431'>Is this the strongest expression of annoyance?</message>
	</annotation>
".Replace("'", "\"");

			string expected = @"	<annotation
		class='question'
		ref='silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=6b466f54-f88a-42f6-b770-aca8fee5734c&amp;tag=&amp;id=6b466f54-f88a-42f6-b770-aca8fee5734c&amp;label=bother'
		guid='10fa26c9-ce35-4341-8a30-c1aa1250d0e0'>
		<message
			author='WhoAmI?'
			status=''
			date='2013-01-28T08:51:40Z'
			guid='25f47900-f6f6-4288-89ac-44f738e63431'>Is this the strongest expression of annoyance?</message>
	</annotation>
".Replace("'", "\"");

			var builder = new StringBuilder();
			using (var reader = new StringReader(input))
			using (var writer = new StringWriter(builder))
			{
				FLExBridgeListener.ConvertLiftNotesToFlex(reader, writer, "Fred.lift");
			}

			Assert.That(builder.ToString(), Is.EqualTo(expected));
		}
	}
}
