// Copyright (c) 2006-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: ParagraphCorrelationTests.cs
// Responsibility: TE Team
// Last reviewed:
//
// <remarks>
// </remarks>
// --------------------------------------------------------------------------------------------
using NUnit.Framework;
using SIL.FieldWorks.Common.FwKernelInterfaces;
using SIL.FieldWorks.Common.ViewsInterfaces;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// </summary>
	[TestFixture]
	public class ParagraphCorrelationTests: SIL.FieldWorks.Test.TestUtils.BaseTest
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test calculating the correlation factor for various paragraphs
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CorrelationFactor()
		{
			ILgCharacterPropertyEngine engine = LgIcuCharPropEngineClass.Create();

			ParagraphCorrelation pc = new ParagraphCorrelation("Hello", "Hello", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello", "Hello ", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation(" Hello", "Hello", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello", "Hello there", engine);
			Assert.AreEqual(0.5, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello over there", "Hello over here", engine);
			Assert.AreEqual(0.5, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello there", "there Hello", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("I am really excited",
				"I am really really really really excited", engine);
			Assert.AreEqual(0.8125, pc.CorrelationFactor);

			pc = new ParagraphCorrelation(string.Empty, "What will happen here?", engine);
			Assert.AreEqual(0.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation(string.Empty, string.Empty, engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation(null, null, engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation(null, "what?", engine);
			Assert.AreEqual(0.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("what?", null, engine);
			Assert.AreEqual(0.0, pc.CorrelationFactor);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test calculating the correlation factor for various paragraphs with digits and
		/// punctuation.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CorrelationFactor_WithDigitsAndPunc()
		{
			ILgCharacterPropertyEngine engine = LgIcuCharPropEngineClass.Create();

			ParagraphCorrelation pc = new ParagraphCorrelation("Hello!", "2Hello.", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello", "Hello, there", engine);
			Assert.AreEqual(0.5, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("3Hello over there", "Hello over here", engine);
			Assert.AreEqual(0.5, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("Hello there?", "4there Hello!", engine);
			Assert.AreEqual(1.0, pc.CorrelationFactor);

			pc = new ParagraphCorrelation("5I am really excited!",
				"6I am really really really really excited.", engine);
			Assert.AreEqual(0.8125, pc.CorrelationFactor);
		}
	}
}
