// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using SIL.FieldWorks.Common.FwUtils;

namespace SILUBS.ScriptureChecks
{
	/// <summary>
	/// SEE ICheckDataSource for documentation of these functions !!!!
	/// </summary>
	public class UnitTestChecksDataSource : IChecksDataSource
	{
		List<UnitTestUSFMTextToken> tokens2 = null;
		internal string m_extraWordFormingCharacters = String.Empty;

		Dictionary<string, string> parameterValues = new Dictionary<string, string>();
		string text;

		public UnitTestChecksDataSource()
		{
		}

		public string GetParameterValue(string key)
		{
			string value;
			if (!parameterValues.TryGetValue(key, out value))
				value = "";

			return value;
		}

		public void SetParameterValue(string key, string value)
		{
			parameterValues[key] = value;
		}

		public void Save()
		{
			//scrText.Save(scrText.Name);
		}

		public string Text
		{
			set
			{
				text = value;
				UnitTestTokenizer tokenizer = new UnitTestTokenizer();
				tokens2 = tokenizer.Tokenize(text);
			}
			get
			{
				return text;
			}
		}

		public IEnumerable<ITextToken> TextTokens
		{
			get
			{
				foreach (UnitTestUSFMTextToken tok in tokens2)
				{
					yield return (ITextToken)tok;
				}
			}
		}

		public CharacterCategorizer CharacterCategorizer
		{
			get { return new CharacterCategorizer(m_extraWordFormingCharacters, String.Empty);
			}
		}

		public bool GetText(int bookNum, int chapterNum)
		{
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a localized version of the specified string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string GetLocalizedString(string strToLocalize)
		{
			return strToLocalize;
		}
	}
}
