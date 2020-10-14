// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using System.Xml.Linq;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// Matches non-blanks.
	/// </summary>
	internal sealed class NonBlankMatcher : BaseMatcher
	{
		/// <summary />
		internal NonBlankMatcher()
		{ }

		/// <summary>
		/// For use with IPersistAsXml
		/// </summary>
		internal NonBlankMatcher(XElement element)
			: base(element)
		{
		}

		/// <summary>
		/// The exact opposite of BlankMatcher.
		/// </summary>
		public override bool Matches(ITsString arg)
		{
			return arg != null && arg.Length != 0 && arg.Text.Any(t => !char.IsWhiteSpace(t));
		}

		/// <summary>
		/// True if it is the same class and member vars match.
		/// </summary>
		public override bool SameMatcher(IMatcher other)
		{
			return other is NonBlankMatcher;
		}
	}
}