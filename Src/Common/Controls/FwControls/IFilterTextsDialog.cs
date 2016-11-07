// Copyright (c) 2010-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace SIL.FieldWorks.Common.Controls
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Interface for the FilterTextsDialog that is used in Interlinear in FLEx
	/// </summary>
	/// <typeparam name="T">Should only be used with IStText (but because of dependencies we
	/// can't specify that directly)</typeparam>
	/// <remarks>We have to use an interface to decouple the SE and BTE editions.</remarks>
	/// ----------------------------------------------------------------------------------------
	public interface IFilterTextsDialog<T>
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return a list of the included IStText nodes.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		T[] GetListOfIncludedTexts();

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Save the information needed to prune the tree later.
		/// </summary>
		/// <param name="interestingTexts">The list of texts to display in the dialog.</param>
		/// <param name="selectedText">The text that should be initially checked in the dialog.</param>
		/// ------------------------------------------------------------------------------------
		void PruneToInterestingTextsAndSelect(IEnumerable<T> interestingTexts, T selectedText);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get/set the label shown above the tree view.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		string TreeViewLabel { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the text on the dialog.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		string Text { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Shows the form as a modal dialog.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		DialogResult ShowDialog(IWin32Window owner);
	}

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// IFilterScrSectionDialog extension methods. We need this class so that we can use
	/// code like the following:
	/// using (IFilterScrSectionDialog dlg = (IDisposable)new FilterScrSectionDialog()) {}
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class FilterTextDialogExtensions
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting
		/// unmanaged resources.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void Dispose(this IFilterTextsDialog<Type> positionHandler)
		{
			var disposable = positionHandler as IDisposable;
			if (disposable != null)
				disposable.Dispose();
		}
	}
}
