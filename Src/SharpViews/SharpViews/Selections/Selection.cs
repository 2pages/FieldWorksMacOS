﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using System.Windows.Forms;
using SIL.FieldWorks.Common.ViewsInterfaces;

namespace SIL.FieldWorks.SharpViews.Selections
{
	/// <summary>
	/// The base class for all selections.
	/// </summary>
	public abstract class Selection
	{
		/// <summary>
		/// Install this selection as the current one for the root box.
		/// </summary>
		public void Install()
		{
			RootBox.Selection = this;
		}

		/// <summary>
		/// The root box this selection belongs to; nb the selection may NOT be the selection of that root box.
		/// </summary>
		abstract public RootBox RootBox
		{
			get;
		}

		public abstract ISelectionRestoreData RestoreData(Selection dataToSave);

		/// <summary>
		/// Answer true if the selection is contained in the other, that is, it is associated with
		/// one of the selected characters.
		/// </summary>
		public virtual bool Contains(InsertionPoint ip)
		{
			return false;
		}

		/// <summary>
		/// Return the data that should be passed to DoDragDrop when this selection is dragged.
		/// </summary>
		public virtual object DragDropData
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Return the selection that should be produced by a KeyDown event with the specified
		/// arguments. If the argument does not specify a key-moving event or it is not possible
		/// to move (e.g., left arrow at start of document), return null.
		/// </summary>
		public virtual Selection MoveByKey(KeyEventArgs args)
		{
			return null; // default behavior.
		}

		/// <summary>
		/// Invalidate the selection, that is, mark the rectangle it occupies as needing to be painted in the containing control.
		/// </summary>
		internal virtual void Invalidate()
		{
			var site = RootBox.Site;
			if (site == null)
				return;
			using (var gh = site.DrawingInfo)
			{
				site.Invalidate(GetSelectionLocation(gh.VwGraphics, gh.Transform));
			}
		}
		/// <summary>
		/// True for insertion point, or any other selection that should be shown and hidden by FlashInsertionPoint
		/// (e.g., a test dummy).
		/// </summary>
		public virtual bool IsInsertionPoint
		{
			get { return false; }
		}

		/// <summary>
		/// Determines whether the selection should flash. Typically one that looks like an insertion point.
		/// </summary>
		public virtual bool ShouldFlash
		{
			get { return IsInsertionPoint; }
		}

		public abstract bool IsValid { get; }

		/// <summary>
		/// Draw the selection on the VwGraphics.
		/// </summary>
		internal abstract void Draw(IVwGraphics vg, PaintTransform ptrans);

		/// <summary>
		/// Get the location, in the coordinates indicated by the transform, of a rectangle that contains the
		/// primary insertion point.
		/// Todo JohnT: there should be a parallel routine to get the location of the secondary rectangle.
		/// </summary>
		public abstract Rectangle GetSelectionLocation(IVwGraphics graphics, PaintTransform transform);

		/// <summary>
		/// Delete the selected material, or whatever else is appropriate when the Delete key is pressed.
		/// (Insertion Point deletes the following character.) Default is to do nothing.
		/// </summary>
		public virtual void Delete()
		{}

		/// <summary>
		/// Return true if Delete() will delete something. Default is that it will not.
		/// </summary>
		public virtual bool CanDelete()
		{
			return false;
		}

		public virtual bool CanApplyStyle(string style)
		{
			return false;
		}

		public virtual void ApplyStyle(string style)
		{
		}

		public virtual bool InsertRtfString(string input)
		{
			return false;
		}

		public virtual bool InsertTsString(ITsString input)
		{
			return false;
		}

		public virtual bool InsertText(string input)
		{
			return false;
		}

		public virtual string SelectedText()
		{
			return "";
		}
	}
}
