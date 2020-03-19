// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// Matches blanks.
	/// </summary>
	public class BlankMatcher : BaseMatcher
	{
		/// <summary>
		/// Matches any empty or null string, or one consisting entirely of white space
		/// characters. I think the .NET definition of white space is good enough; it's unlikely
		/// we'll need new PUA whitespace characters.
		/// </summary>
		public override bool Matches(ITsString arg)
		{
			return arg == null || arg.Length == 0 || arg.Text.All(char.IsWhiteSpace);
		}

		/// <summary>
		/// True if it is the same class and member vars match.
		/// </summary>
		public override bool SameMatcher(IMatcher other)
		{
			return other is BlankMatcher;
		}
	}
}