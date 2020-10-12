// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using Paratext.LexicalContracts;

namespace SIL.FieldWorks.ParatextLexiconPlugin
{
	internal class LcmLexiconSenseAddedEventArgs : EventArgs, LexiconSenseAddedEventArgs
	{
		private readonly Lexeme m_lexeme;
		private readonly LexiconSense m_sense;

		public LcmLexiconSenseAddedEventArgs(Lexeme lexeme, LexiconSense sense)
		{
			m_lexeme = lexeme;
			m_sense = sense;
		}

		public Lexeme Lexeme
		{
			get { return m_lexeme; }
		}

		public LexiconSense Sense
		{
			get { return m_sense; }
		}
	}
}