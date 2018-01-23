﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using Paratext.LexicalContracts;

namespace SIL.FieldWorks.ParatextLexiconPlugin
{
	internal class LcmLanguageText : LanguageText
	{
		/// <summary>
		/// Creates a new LanguageText by wrapping the specified data
		/// </summary>
		public LcmLanguageText(string language, string text)
		{
			Debug.Assert(language.IsNormalized(), "We expect all strings to be normalized composed");
			Debug.Assert(text.IsNormalized(), "We expect all strings to be normalized composed");
			Language = language;
			Text = text;
		}

		#region Implementation of LanguageText
		/// <summary>
		/// Language of the text.
		/// </summary>
		public string Language { get; set; }

		/// <summary>
		/// The actual text.
		/// </summary>
		public string Text { get; set; }
		#endregion
	}
}
