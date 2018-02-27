// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using XAmpleManagedWrapper;

namespace XAmpleManagedWrapperTests
{
	[TestFixture]
	public class TestXAmpleDLLWrapper
	{
		protected XAmpleDLLWrapper CreateXAmpleDllWrapper()
		{
			var xAmple = new XAmpleDLLWrapper();
			xAmple.Init();
			return xAmple;
		}

		protected void LoadFilesHelper(XAmpleDLLWrapper wrapper)
		{
			string tempPath = Path.Combine(FwDirectoryFinder.SourceDirectory, "ParserCoreTests", "M3ToXAmpleTransformerTestsDataFiles");
			string xPath = Path.Combine(FwDirectoryFinder.CodeDirectory, "Language Explorer", "Configuration", "Grammar");
			wrapper.LoadFiles(xPath, tempPath, "StemName3");
		}

		[Test]
		public void TestInit()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				Assert.IsNotNull(wrapper);
		}

		[Test]
		public void TestLoadFiles()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				LoadFilesHelper(wrapper);
		}

		[Test]
		public void TestSetParameter()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				wrapper.SetParameter("MaxAnalysesToReturn", "3");
		}

		[Test]
		public void TestGetSetup()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				Assert.AreNotEqual(IntPtr.Zero, wrapper.GetSetup());
		}

		[Test]
		[ExpectedException(typeof(NotImplementedException))]
		public void TestSetLogFile()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				wrapper.SetLogFile(Path.GetTempFileName());
		}

		[Test]
		public void GetAmpleThreadId()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
				int threadId = wrapper.GetAmpleThreadId();
#if __MonoCS__
				Assert.AreEqual(0, threadId);
#else
				Assert.AreNotEqual(0, threadId);
#endif
			}
		}

		[Test]
		public void TestParseString()
		{

			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
				LoadFilesHelper(wrapper);
				string parsedString = wrapper.ParseString("Hello");
				Assert.IsNotEmpty(parsedString);
				Assert.IsNotNull(parsedString);
			}
		}

		[Test]
		public void TestTraceString()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
				LoadFilesHelper(wrapper);
				string tracedString = wrapper.TraceString("Hello", "Hello");
				Assert.IsNotEmpty(tracedString);
				Assert.IsNotNull(tracedString);
			}
		}
	}
}
