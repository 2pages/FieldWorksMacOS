// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary />
	public interface IHeightEstimator
	{
		/// <summary>
		/// Estimate the height of an object in points.
		/// </summary>
		/// <param name="hvo">The hvo of the object</param>
		/// <param name="frag">The frag used to display the object</param>
		/// <param name="availableWidth"></param>
		/// <returns>The estimated height for the specified object in points</returns>
		int EstimateHeight(int hvo, int frag, int availableWidth);
	}
}
