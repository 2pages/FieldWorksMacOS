// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace LanguageExplorer
{
	/// <summary>
	/// use this when a colleague is expecting you to pass a progress state
	/// but you aren't in a position to create a real one.
	/// </summary>
	public sealed class NullProgressState : ProgressState
	{
		/// <summary>
		/// just initializes the base class
		/// </summary>
		public NullProgressState()
			: base(null)
		{
		}

		/// <inheritdoc />
		public override void SetMilestone(string newStatus) { }

		/// <inheritdoc />
		public override void SetMilestone() { }

		/// <inheritdoc />
		public override void Breath() { }
	}
}