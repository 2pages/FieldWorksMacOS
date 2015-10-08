// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Xml.Linq;
using SIL.CoreImpl;

namespace LanguageExplorer.Areas.TextsAndWords
{
	/// <summary>
	/// Interface for parser trace processing
	/// </summary>
	public interface IParserTrace
	{
		/// <summary>
		/// Create an HTML page of the results
		/// </summary>
		/// <param name="propertyTable"></param>
		/// <param name="result">XML of the parse trace output</param>
		/// <param name="isTrace"></param>
		/// <returns>URL of the resulting HTML page</returns>
		string CreateResultPage(IPropertyTable propertyTable, XDocument result, bool isTrace);
	}
}
