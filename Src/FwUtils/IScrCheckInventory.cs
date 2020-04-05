// Copyright (c) 2012-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// Interface for scripture checks that provide an inventory mode.
	/// </summary>
	public interface IScrCheckInventory : IScriptureCheck
	{
		/// <summary>
		/// Inventory form queries this to know how what status to give each item
		/// in the inventory. Inventory form updates this if user has changed the status
		/// of any item.
		/// </summary>
		string ValidItems { get; set; }

		/// <summary>
		/// Inventory form queries this to know how what status to give each item
		/// in the inventory. Inventory form updates this if user has changed the status
		/// of any item.
		/// </summary>
		string InvalidItems { get; set; }

		/// <summary>
		/// Get all instances of the item being checked in the token list passed.
		/// This includes both valid and invalid instances.
		/// This is used 1) to create an inventory of these items.
		/// To show the user all instance of an item with a specified key.
		/// 2) With a "desiredKey" in order to fetch instance of a specific
		/// item (e.g. all the places where "the" is a repeated word.
		/// </summary>
		/// <param name="tokens">Tokens for text to be scanned</param>
		/// <param name="desiredKey">If you only want instance of a specific key (e.g. one word, one punctuation pattern,
		/// one character, etc.) place it here. Empty string returns all items.</param>
		/// <returns>List of token substrings</returns>
		List<TextTokenSubstring> GetReferences(IEnumerable<ITextToken> tokens, string desiredKey);

		/// <summary>
		/// Update the parameter values for storing the valid and invalid lists in CheckDataSource
		/// and then save them. This is here because the inventory form does not know the names of
		/// the parameters that need to be saved for a given check, only the check knows this.
		/// </summary>
		void Save();
	}
}