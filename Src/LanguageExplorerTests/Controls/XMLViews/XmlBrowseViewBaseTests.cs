// Copyright (c) 2012-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Drawing;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using LanguageExplorer.Controls.XMLViews;
using LanguageExplorer.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace LanguageExplorerTests.Controls.XMLViews
{
	/// <summary />
	[TestFixture]
	public class XmlBrowseViewBaseTests : MemoryOnlyBackendProviderTestBase
	{
		private FakeXmlBrowseViewBase m_view;
		private FlexComponentParameters _flexComponentParameters;
		#region Overrides of LcmTestBase

		public override void TestSetup()
		{
			base.TestSetup();
			_flexComponentParameters = TestSetupServices.SetupTestTriumvirate();
			var bv = new FakeBrowseViewer();
			bv.InitializeFlexComponent(_flexComponentParameters);
			m_view = (FakeXmlBrowseViewBase)bv.BrowseView;
			ConfigureScrollBars();
		}

		public override void TestTearDown()
		{
			try
			{
				m_view.m_bv.Dispose();
				TestSetupServices.DisposeTrash(_flexComponentParameters);
				_flexComponentParameters = null;
			}
			catch (Exception err)
			{
				throw new Exception($"Error in running {GetType().Name} TestTearDown method.", err);
			}
			finally
			{
				base.TestTearDown();
			}
		}
		#endregion

		private void ConfigureScrollBars()
		{
			// AdjustControls ends up getting called when running Flex
			ReflectionHelper.CallMethod(m_view.m_bv, "AdjustControls");
			var sizeRequestedBySRSUpdateScrollRange = new Size(m_view.AutoScrollMinSize.Width, m_view.GetScrollRange().Height);
			m_view.ScrollMinSize = sizeRequestedBySRSUpdateScrollRange;
		}

		/// <summary>
		/// Unit test helper to avoided repeated code
		/// </summary>
		private static int CalculateMaxScrollBarHeight(int maxUserReachable, int largeChange)
		{
			// http://msdn.microsoft.com/en-us/library/vstudio/system.windows.forms.scrollbar.maximum
			return maxUserReachable + largeChange - 1;
		}

		/// <summary />
		[Test]
		public void ScrollPosition_SetToSensibleValue_Allowed()
		{
			m_view.m_rowCount = 100;
			ConfigureScrollBars();

			var input = new Point(0, 0);
			var expected = new Point(0, -input.Y);
			m_view.ScrollPosition = input;
			var output = m_view.ScrollPosition;
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y));

			input = new Point(10, 10);
			expected = new Point(0, -input.Y);
			m_view.ScrollPosition = input;
			output = m_view.ScrollPosition;
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y));
		}

		/// <summary />
		[Test]
		public void ScrollPosition_SetToTooLargeValue_Limited()
		{
			var input = new Point(200, 200);
			Assert.That(200, Is.GreaterThan(m_view.ScrollPositionMaxUserReachable), "Unit test bad assumption");

			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var expected = new Point(0, -desiredMaxUserReachable);

			m_view.ScrollPosition = input;
			var output = m_view.ScrollPosition;

			Assert.That(output, Is.EqualTo(expected));
			Assert.That(output.Y, Is.EqualTo(-m_view.ScrollPositionMaxUserReachable), "Should have been set to the max value that could be set");
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y), "Should have been set to the max value that could be set");
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(m_view.ScrollPositionMaxUserReachable), "Should have been set to the max value that could be set");
		}

		/// <summary />
		[Test]
		public void ScrollPosition_SetToNegativeValue_ResetToTop()
		{
			m_view.m_rowCount = 100;
			ConfigureScrollBars();

			// Starting at 0, trying to scroll up to -200

			var input = new Point(-200, -200);
			var expected = new Point(0, 0);
			m_view.ScrollPosition = input;
			var output = m_view.ScrollPosition;
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y));

			// Continuing from 0, trying to scroll down to 10

			input = new Point(10, 10);
			expected = new Point(0, -input.Y);
			m_view.ScrollPosition = input;
			output = m_view.ScrollPosition;
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y));

			// Continuing from 10, trying to scroll up to -200

			input = new Point(-200, -200);
			expected = new Point(0, 0);
			m_view.ScrollPosition = input;
			output = m_view.ScrollPosition;
			Assert.That(output, Is.EqualTo(expected));
			Assert.That(m_view.m_bv.ScrollBar.Value, Is.EqualTo(-expected.Y));
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleLargeChange()
		{
			Assert.That(m_view.m_bv.ScrollBar.LargeChange, Is.EqualTo(m_view.ClientHeight - m_view.MeanRowHeight));
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleLargeChangeWhenNoRows()
		{
			m_view.m_rowCount = 0;
			ConfigureScrollBars();
			Assert.That(m_view.m_bv.ScrollBar.LargeChange, Is.EqualTo(m_view.ClientHeight - m_view.MeanRowHeight));
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleLargeChange_AfterLazyExpansion()
		{
			m_view.m_rowCount = 10000;
			const int aSensibleRowHeight = 25;
			// Content not fully expanded to begin with
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 500);
			ConfigureScrollBars();

			// We'll assume we are working with a display for records that is at least bigger than the average height of a record
			Assert.That(m_view.ClientHeight, Is.GreaterThan(m_view.MeanRowHeight));

			var contentHeight = m_view.RootBox.Height;
			Assert.That(contentHeight, Is.EqualTo(aSensibleRowHeight * (m_view.RowCount - 500)), "Unit test not set up right");

			var desiredLargeChangeValue = m_view.ClientHeight - m_view.MeanRowHeight;
			Assert.That(m_view.m_bv.ScrollBar.LargeChange, Is.EqualTo(desiredLargeChangeValue), "Incorrect LargeChange before expansion");

			// Lazy boxes expand
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 0);
			var expandedSizeRequestedBySRSUpdateScrollRange = new Size(m_view.AutoScrollMinSize.Width, m_view.GetScrollRange().Height);
			m_view.ScrollMinSize = expandedSizeRequestedBySRSUpdateScrollRange;

			desiredLargeChangeValue = m_view.ClientHeight - m_view.MeanRowHeight;
			Assert.That(m_view.m_bv.ScrollBar.LargeChange, Is.EqualTo(desiredLargeChangeValue), "Incorrect LargeChange after expansion");
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleSmallChange()
		{
			Assert.That(m_view.m_bv.ScrollBar.SmallChange, Is.EqualTo(m_view.MeanRowHeight));
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleSmallChangeWhenZeroRows()
		{
			m_view.m_rowCount = 0;
			ConfigureScrollBars();
			Assert.That(m_view.m_bv.ScrollBar.SmallChange, Is.EqualTo(m_view.MeanRowHeight));
		}

		/// <summary />
		[Test]
		public void ScrollBar_SensibleSmallChange_AfterLazyExpansion()
		{
			m_view.m_rowCount = 10000;
			const int aSensibleRowHeight = 25;
			// Content not fully expanded to begin with
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 500);
			ConfigureScrollBars();

			// We'll assume we are working with a display for records that is at least bigger than the average height of a record
			Assert.That(m_view.ClientHeight, Is.GreaterThan(m_view.MeanRowHeight));

			var contentHeight = m_view.RootBox.Height;
			Assert.That(contentHeight, Is.EqualTo(aSensibleRowHeight * (m_view.RowCount - 500)), "Unit test not set up right");

			var initialMeanRowHeight = m_view.MeanRowHeight;
			Assert.That(m_view.m_bv.ScrollBar.SmallChange, Is.EqualTo(m_view.MeanRowHeight), "Incorrect SmallChange before expansion");

			// Lazy boxes expand
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 0);
			var expandedSizeRequestedBySRSUpdateScrollRange = new Size(m_view.AutoScrollMinSize.Width, m_view.GetScrollRange().Height);
			m_view.ScrollMinSize = expandedSizeRequestedBySRSUpdateScrollRange;
			Assert.That(initialMeanRowHeight, Is.Not.EqualTo(m_view.MeanRowHeight), "Unit test not set up right");

			Assert.That(m_view.m_bv.ScrollBar.SmallChange, Is.EqualTo(m_view.MeanRowHeight), "Incorrect SmallChange after expansion");
		}

		/// <summary>
		/// When height of all rows is less than m_view.ClientHeight
		/// </summary>
		[Test]
		public void ScrollBar_HasSensibleMaximumWhenFewRows()
		{
			m_view.m_rowCount = 2;
			ConfigureScrollBars();

			// Display of records is definitely bigger than the height of the content
			Assert.That(m_view.ClientHeight, Is.GreaterThan(m_view.MeanRowHeight * (m_view.RowCount + 1)), "Unit test not set up right");

			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary>
		/// Many rows higher than the m_view.ClientHeight.
		/// An ideal maximum would allow scrolling through so all rows are shown, and not any blank space at the end (unless
		/// the m_view.ClientHeight is taller than the RootBox.Height).
		/// </summary>
		[Test]
		public void ScrollBar_HasSensibleMaximumWhenManyRows()
		{
			m_view.m_rowCount = 10000;
			ConfigureScrollBars();

			// We'll assume we are working with a display of records that is at least bigger than the average height of a record
			Assert.That(m_view.ClientHeight, Is.GreaterThan(m_view.MeanRowHeight), "Bad unit test assumption");

			Assert.That(m_view.RootBox.Height, Is.GreaterThan(m_view.ClientHeight), "Unit test not set up right");

			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary>
		/// Would probably be acceptable with either the normal formula or 0.
		/// </summary>
		[Test]
		public void ScrollBar_HasSensibleMaximumWhenZeroRows()
		{
			m_view.m_rowCount = 0;
			ConfigureScrollBars();

			Assert.That(m_view.RootBox.Height, Is.EqualTo(0), "Mistake in unit test");
			Assert.That(m_view.MeanRowHeight, Is.EqualTo(0), "Mistake in unit test");
			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary />
		[Test]
		public void MeanRowHeight_Normal()
		{
			// Measurement when running Flex for single-line row
			int expectedHeight = 25;
			Assert.That(m_view.MeanRowHeight, Is.EqualTo(expectedHeight));
		}

		/// <summary />
		[Test]
		public void MeanRowHeight_WhenNoRows()
		{
			m_view.m_rowCount = 0;
			const int expectedHeight = 0;
			Assert.That(m_view.MeanRowHeight, Is.EqualTo(expectedHeight));
		}

		/// <summary />
		[Test]
		public void MeanRowHeight_WhenNullRootBox()
		{
			m_view.SetRootBox(null);
			const int expectedHeight = 0;
			Assert.That(m_view.MeanRowHeight, Is.EqualTo(expectedHeight));
		}

		/// <summary />
		[Test]
		public void ScrollPositionMaxUserReachable_WhenManyRows()
		{
			m_view.m_rowCount = 10000;
			ConfigureScrollBars();

			var contentHeight = m_view.RootBox.Height;
			var desiredMaxUserReachable = contentHeight - m_view.ClientHeight;
			Assert.That(m_view.ScrollPositionMaxUserReachable, Is.EqualTo(desiredMaxUserReachable));
		}

		/// <summary />
		[Test]
		public void ScrollPositionMaxUserReachable_WhenFewRows()
		{
			m_view.m_rowCount = 2;
			ConfigureScrollBars();

			Assert.That(m_view.ClientHeight, Is.GreaterThan((m_view.RowCount + 1) * m_view.MeanRowHeight), "Unit test not set up right");
			const int desiredMaxUserReachable = 0;
			Assert.That(m_view.ScrollPositionMaxUserReachable, Is.EqualTo(desiredMaxUserReachable));
		}

		/// <summary />
		[Test]
		public void ScrollPositionMaxUserReachable_WhenNoRows()
		{
			m_view.m_rowCount = 0;
			ConfigureScrollBars();

			var contentHeight = m_view.RootBox.Height;
			Assert.That(contentHeight, Is.EqualTo(0), "Problem with unit test");

			Assert.That(m_view.ScrollPositionMaxUserReachable, Is.EqualTo(0));
		}

		/// <summary>
		/// Test Scrollbar response to lazy expansion of content.
		/// </summary>
		[Test]
		public void LazyExpansion_UpdatesMaximums()
		{
			m_view.m_rowCount = 10000;
			const int aSensibleRowHeight = 25;
			// Content not fully expanded to begin with
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 500);
			ConfigureScrollBars();

			// We'll assume we are working with a display for records that is at least bigger than the average height of a record
			Assert.That(m_view.ClientHeight, Is.GreaterThan(m_view.MeanRowHeight));

			var contentHeight = m_view.RootBox.Height;
			Assert.That(contentHeight, Is.EqualTo(aSensibleRowHeight * (m_view.RowCount - 500)), "Unit test not set up right");
			var desiredMaxUserReachable = contentHeight - m_view.ClientHeight;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);

			Assert.That(m_view.ScrollPositionMaxUserReachable, Is.EqualTo(desiredMaxUserReachable));
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(desiredMaxScrollbarHeight));

			// Lazy boxes expand
			((FakeRootBox)m_view.RootBox).Height = aSensibleRowHeight * (m_view.RowCount - 0);
			var expandedSizeRequestedBySRSUpdateScrollRange = new Size(m_view.AutoScrollMinSize.Width, m_view.GetScrollRange().Height);
			m_view.ScrollMinSize = expandedSizeRequestedBySRSUpdateScrollRange;

			Assert.That(m_view.MeanRowHeight, Is.EqualTo(aSensibleRowHeight), "Expecting MeanRowHeight to be aSensibleRowHeight by now.");

			contentHeight = m_view.RootBox.Height;
			desiredMaxUserReachable = contentHeight - m_view.ClientHeight;
			desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);

			// Both ScrollBar.Maximum and the setting-bounds of XmlBrowseViewBase.ScrollPosition should reflect an
			// increased RootBox size.
			Assert.That(m_view.ScrollPositionMaxUserReachable, Is.EqualTo(desiredMaxUserReachable));
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary />
		[Test]
		public void ScrollRange_IsDesiredScrollBarMaxWhenManyRows()
		{
			m_view.m_rowCount = 10000;
			ConfigureScrollBars();
			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			var output = m_view.GetScrollRange().Height;
			Assert.That(output, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary />
		[Test]
		public void ScrollRange_IsDesiredScrollBarMaxWhenFewRows()
		{
			m_view.m_rowCount = 2;
			ConfigureScrollBars();
			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			var output = m_view.GetScrollRange().Height;
			Assert.That(output, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary />
		[Test]
		public void ScrollRange_IsDesiredScrollBarMaxWhenZeroRows()
		{
			m_view.m_rowCount = 0;
			ConfigureScrollBars();
			var desiredMaxUserReachable = m_view.ScrollPositionMaxUserReachable;
			var desiredMaxScrollbarHeight = CalculateMaxScrollBarHeight(desiredMaxUserReachable, m_view.m_bv.ScrollBar.LargeChange);
			var output = m_view.GetScrollRange().Height;
			Assert.That(output, Is.EqualTo(desiredMaxScrollbarHeight));
		}

		/// <summary />
		[Test]
		public void ScrollMinSize_SettingSetsScrollBarMaximumToSame()
		{
			var height = 456;
			m_view.ScrollMinSize = new Size(123, height);
			Assert.That(m_view.m_bv.ScrollBar.Maximum, Is.EqualTo(height));
		}

		/// <summary />
		private sealed class FakeBrowseViewer : BrowseViewer
		{
			/// <summary/>
			public FakeBrowseViewer()
			{
				ScrollBar = new VScrollBar();
				m_configureButton = new Button();
				m_lvHeader = new DhListView(this);
				Scroller = new BrowseViewScroller(this);
				// When running FieldWorks, the constructor eventually creates an XmlBrowseView and calls AddControl() with it,
				// and adds m_scrollContainer to Controls. Model this so the .Dispose methods can behave the same way when
				// testing as when running FieldWorks.
				Controls.Add(Scroller);
				BrowseView = new FakeXmlBrowseViewBase(this);
				AddControl(BrowseView);
			}
		}

		/// <summary />
		private sealed class FakeXmlBrowseViewBase : XmlBrowseViewBase
		{
			/// <summary />
			internal int m_rowCount = 3;

			/// <summary/>
			internal override int RowCount => m_rowCount;

			/// <summary />
			internal FakeXmlBrowseViewBase(BrowseViewer bv)
			{
				m_bv = bv;
				RootBox = new FakeRootBox();
				((FakeRootBox)RootBox).m_xmlBrowseViewBase = this;
			}

			/// <summary />
			public void SetRootBox(IVwRootBox newRootBox)
			{
				RootBox = newRootBox;
			}

			/// <summary />
			public Size GetScrollRange()
			{
				return ScrollRange;
			}
		}

		/// <summary />
		private sealed class FakeRootBox : IVwRootBox
		{
			/// <summary />
			internal XmlBrowseViewBase m_xmlBrowseViewBase;

			/// <summary>
			/// null unless manually set
			/// </summary>
			private int? m_height;

			#region IVwRootBox methods
			/// <summary>
			/// Height of FakeRootBox, unless changed by test code
			/// </summary>
			public int Height
			{
				set
				{
					m_height = value;
				}
				get
				{
					if (m_height != null)
					{
						return (int)m_height;
					}
					// Measurement when running Flex for single-line row
					const int measuredRowHeight = 25;
					return m_xmlBrowseViewBase.RowCount * measuredRowHeight;
				}
			}

			/// <summary />
			public ISilDataAccess DataAccess
			{
				get;
				set;
			}

			public IRenderEngineFactory RenderEngineFactory
			{
				get;
				set;
			}

			public ITsStrFactory TsStrFactory
			{
				get;
				set;
			}

			/// <summary />
			public IVwOverlay Overlay
			{
				get;
				set;
			}

			/// <summary />
			public IVwSelection Selection
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public VwSelectionState SelectionState
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public int Width
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public IVwRootSite Site
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public IVwStylesheet Stylesheet
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public int XdPos
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public IVwSynchronizer Synchronizer
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public int MaxParasToScan
			{
				get;
				set;
			}

			/// <summary />
			public bool IsCompositionInProgress
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public bool IsPropChangedInProgress
			{
				get { throw new NotSupportedException(); }
			}

			/// <summary />
			public void PropChanged(int hvo, int tag, int ivMin, int cvIns, int cvDel)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetSite(IVwRootSite _vrs)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetRootObjects(int[] _rghvo, IVwViewConstructor[] _rgpvwvc, int[] _rgfrag, IVwStylesheet _ss, int chvo)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetRootObject(int hvo, IVwViewConstructor _vwvc, int frag, IVwStylesheet _ss)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetRootVariant(object v, IVwStylesheet _ss, IVwViewConstructor _vwvc, int frag)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetRootString(ITsString _tss, IVwStylesheet _ss, IVwViewConstructor _vwvc, int frag)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public object GetRootVariant()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void Serialize(IStream _strm)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void Deserialize(IStream _strm)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void WriteWpx(IStream _strm)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void DestroySelection()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeTextSelection(int ihvoRoot, int cvlsi, SelLevInfo[] _rgvsli, int tagTextProp, int cpropPrevious, int ichAnchor, int ichEnd, int ws, bool fAssocPrev, int ihvoEnd, ITsTextProps _ttpIns, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeRangeSelection(IVwSelection _selAnchor, IVwSelection _selEnd, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeSimpleSel(bool fInitial, bool fEdit, bool fRange, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeTextSelInObj(int ihvoRoot, int cvsli, SelLevInfo[] _rgvsli, int cvsliEnd, SelLevInfo[] _rgvsliEnd, bool fInitial, bool fEdit, bool fRange, bool fWholeObj, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeSelInObj(int ihvoRoot, int cvsli, SelLevInfo[] _rgvsli, int tag, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeSelAt(int xd, int yd, Rect rcSrc, Rect rcDst, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public IVwSelection MakeSelInBox(IVwSelection _selInit, bool fEndPoint, int iLevel, int iBox, bool fInitial, bool fRange, bool fInstall)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool get_IsClickInText(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool get_IsClickInObject(int xd, int yd, Rect rcSrc, Rect rcDst, out int _odt)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool get_IsClickInOverlayTag(int xd, int yd, Rect rcSrc1, Rect rcDst1, out int _iGuid, out string _bstrGuids, out Rect _rcTag, out Rect _rcAllTags, out bool _fOpeningTag)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void OnTyping(IVwGraphics _vg, string bstrInput, VwShiftStatus ss, ref int _wsPending)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void DeleteRangeIfComplex(IVwGraphics _vg, out bool _fWasComplex)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void OnChar(int chw)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void OnSysChar(int chw)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public int OnExtendedKey(int chw, VwShiftStatus ss, int nFlags)
			{
				throw new NotImplementedException();
			}

			/// <summary />
			public void FlashInsertionPoint()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void MouseDown(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void MouseDblClk(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void MouseMoveDrag(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void MouseDownExtended(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void MouseUp(int xd, int yd, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void Activate(VwSelectionState vss)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public VwPrepDrawResult PrepareToDraw(IVwGraphics _vg, Rect rcSrc, Rect rcDst)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void DrawRoot(IVwGraphics _vg, Rect rcSrc, Rect rcDst, bool fDrawSel)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void Layout(IVwGraphics _vg, int dxsAvailWidth)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void InitializePrinting(IVwPrintContext _vpc)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public int GetTotalPrintPages(IVwPrintContext _vpc)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void PrintSinglePage(IVwPrintContext _vpc, int nPageNo)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool LoseFocus()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void Close()
			{

			}

			/// <summary />
			public void Reconstruct()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void OnStylesheetChange()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void DrawingErrors(IVwGraphics _vg)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetTableColWidths(VwLength[] _rgvlen, int cvlen)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool IsDirty()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void GetRootObject(out int _hvo, out IVwViewConstructor _pvwvc, out int _frag, out IVwStylesheet _pss)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void DrawRoot2(IVwGraphics _vg, Rect rcSrc, Rect rcDst, bool fDrawSel, int ysTop, int dysHeight)
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool DoSpellCheckStep()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public bool IsSpellCheckComplete()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void RestartSpellChecking()
			{
				throw new NotSupportedException();
			}

			/// <summary />
			public void SetSpellingRepository(IGetSpellChecker _gsp)
			{
			}

			#endregion IVwRootBox methods
		}
	}
}