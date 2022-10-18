// Copyright (c) 2017-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using NUnit.Framework;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary />
	[TestFixture]
	public class RenderEngineFactoryTests
	{
		/// <summary>
		/// Tests the get_RendererFromChrp method with a normal font.
		/// </summary>
		[Test]
		public void get_Renderer_Uniscribe()
		{
			using (var control = new Form())
			using (var gm = new GraphicsManager(control))
			using (var reFactory = new RenderEngineFactory())
			{
				gm.Init(1.0f);
				try
				{
					var wsManager = new WritingSystemManager();
					var ws = wsManager.Set("en-US");
					var chrp = new LgCharRenderProps { ws = ws.Handle, szFaceName = new ushort[32] };
					MarshalEx.StringToUShort("Arial", chrp.szFaceName);
					gm.VwGraphics.SetupGraphics(ref chrp);
<<<<<<< HEAD:Src/RootSiteTests/RenderEngineFactoryTests.cs
					var engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.IsNotNull(engine);
||||||| f013144d5:Src/Common/SimpleRootSite/SimpleRootSiteTests/RenderEngineFactoryTests.cs
					IRenderEngine engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.IsNotNull(engine);
=======
					IRenderEngine engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.That(engine, Is.Not.Null);
>>>>>>> develop:Src/Common/SimpleRootSite/SimpleRootSiteTests/RenderEngineFactoryTests.cs
					Assert.AreSame(wsManager, engine.WritingSystemFactory);
					Assert.IsInstanceOf(typeof(UniscribeEngine), engine);
					wsManager.Save();
				}
				finally
				{
					gm.Uninit();
				}
			}
		}

		/// <summary>
		/// Tests the get_RendererFromChrp method with a Graphite font.
		/// </summary>
		[Test]
		public void get_Renderer_Graphite()
		{
			using (var control = new Form())
			using (var gm = new GraphicsManager(control))
			using (var reFactory = new RenderEngineFactory())
			{
				gm.Init(1.0f);
				try
				{
					var wsManager = new WritingSystemManager();
					// by default Graphite is disabled
					var ws = wsManager.Set("en-US");
					var chrp = new LgCharRenderProps { ws = ws.Handle, szFaceName = new ushort[32] };
					MarshalEx.StringToUShort("Charis SIL", chrp.szFaceName);
					gm.VwGraphics.SetupGraphics(ref chrp);
<<<<<<< HEAD:Src/RootSiteTests/RenderEngineFactoryTests.cs
					var engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.IsNotNull(engine);
||||||| f013144d5:Src/Common/SimpleRootSite/SimpleRootSiteTests/RenderEngineFactoryTests.cs
					IRenderEngine engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.IsNotNull(engine);
=======
					IRenderEngine engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.That(engine, Is.Not.Null);
>>>>>>> develop:Src/Common/SimpleRootSite/SimpleRootSiteTests/RenderEngineFactoryTests.cs
					Assert.AreSame(wsManager, engine.WritingSystemFactory);
					Assert.IsInstanceOf(typeof(UniscribeEngine), engine);

					ws.IsGraphiteEnabled = true;
					gm.VwGraphics.SetupGraphics(ref chrp);
					engine = reFactory.get_Renderer(ws, gm.VwGraphics);
					Assert.That(engine, Is.Not.Null);
					Assert.AreSame(wsManager, engine.WritingSystemFactory);
					Assert.IsInstanceOf(typeof(GraphiteEngine), engine);
					wsManager.Save();
				}
				finally
				{
					gm.Uninit();
				}
			}
		}
	}
}