#region usings
using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Threading;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;
using VVVV.Core.Logging;

using ThreadState = System.Threading.ThreadState;
using System.Collections.Generic;

using OpenNI;
using System.Runtime.InteropServices;

using Emgu.Util;
using Emgu.CV;
using Emgu.CV.Structure;

using VVVV.Nodes.EmguCV;

#endregion usings

namespace VVVV.Nodes.OpenNI
{
	enum ImageNodeMode { RGB, IR };

	#region PluginInfo
	[PluginInfo(Name = "Images", Category = "OpenNI", Help = "OpenNI Image generator", Tags = "")]
	#endregion PluginInfo
	public class ImageNode : IPluginEvaluate, IDisposable
	{
		[DllImport("msvcrt.dll", EntryPoint = "memcpy")]
		public unsafe static extern void CopyMemory(IntPtr pDest, IntPtr pSrc, int length);

		#region fields & pins
		[Input("Context", IsSingle=true)]
		ISpread<OpenNIState> FPinInContext;

		[Input("Mode", IsSingle = true)]
		ISpread<ImageNodeMode> FPinInMode;

		[Output("Image")]
		ISpread<CVImageLink> FPinOutImageImage;

		[Output("Depth")]
		ISpread<CVImageLink> FPinOutImageDepth;

		[Output("World")]
		ISpread<CVImageLink> FPinOutImageWorld;

		[Output("Status")]
		ISpread<String> FPinOutStatus;

		CVImageOutput FImageImage = new CVImageOutput();
		CVImageOutput FImageDepth = new CVImageOutput();
		CVImageOutput FImageWorld = new CVImageOutput();

		ImageNodeMode FMode;

		Point3D[] FProjective = new Point3D[640 * 480];
		[Import]
		ILogger FLogger;

		OpenNIState FState;
		ImageGenerator FImageGenerator;
		IRGenerator FIRGenerator;

		string FStatus = "";
		#endregion fields & pins

		[ImportingConstructor]
		public ImageNode(IPluginHost host)
		{

		}

		public void Dispose()
		{

		}

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if (FPinInContext[0] != FState)
			{
				FState = FPinInContext[0];

				if (FState != null)
				{
					//we've made a connection

					//add a listener for intialisation
					FState.Initialised += new EventHandler(FState_Initialise);

					//if already initialised before we connected
					if (FState.Running)
						Initialise();
				}
			}

			FPinOutStatus[0] = FStatus;
		}

		void FState_Initialise(object sender, EventArgs e)
		{
			Initialise();
		}

		private void Initialise()
		{
			try
			{
				FMode = FPinInMode[0];
				Size size = new Size(640, 480);

				if (FMode == ImageNodeMode.RGB)
				{
					FImageGenerator = new ImageGenerator(FState.Context);
					FImageImage.Image.Initialise(size, TColourFormat.RGB8);
					FState.DepthGenerator.AlternativeViewpointCapability.SetViewpoint(FImageGenerator);
					FImageGenerator.StartGenerating();
				}
				else
				{
					FIRGenerator = new IRGenerator(FState.Context);
					FImageImage.Image.Initialise(size, TColourFormat.L16);
					FIRGenerator.StartGenerating();
				}
				
				FImageDepth.Image.Initialise(size, TColourFormat.L16);
				FImageWorld.Image.Initialise(size, TColourFormat.RGB32F);

				FPinOutImageImage[0] = FImageImage.Link;
				FPinOutImageDepth[0] = FImageDepth.Link;
				FPinOutImageWorld[0] = FImageWorld.Link;

				for (int x = 0; x < 640; x++)
					for (int y = 0; y < 480; y++)
					{
						FProjective[x + y * 640].X = x;
						FProjective[x + y * 640].Y = y;
					}

				FState.Update += new EventHandler(FState_Update);

				FStatus = "OK";
			}
			catch (StatusException e)
			{
				FStatus = e.Message;
			}
		}

		void FState_Update(object sender, EventArgs e)
		{
			Update();
		}

		private unsafe void Update()
		{
			if (FMode == ImageNodeMode.RGB)
			{
				byte* rgbs = (byte*)FImageGenerator.ImageMapPtr.ToPointer();
				byte* rgbd = (byte*)FImageImage.Image.Data.ToPointer();

				for (int i = 0; i < 640 * 480; i++)
				{
					rgbd[2] = rgbs[0];
					rgbd[1] = rgbs[1];
					rgbd[0] = rgbs[2];
					rgbs += 3;
					rgbd += 3;
				}
			}
			else if (FMode == ImageNodeMode.IR)
			{
				FImageImage.Image.SetPixels(FIRGenerator.IRMapPtr);
			}

			FImageDepth.Image.SetPixels(FState.DepthGenerator.DepthMapPtr);

			fillWorld();

			FImageImage.Send();
			FImageDepth.Send();
			FImageWorld.Send();
		}

		private unsafe void fillWorld()
		{
			float* xyz = (float*)FImageWorld.Data.ToPointer();
			ushort* d = (ushort*)FImageDepth.Data.ToPointer();

			for (int i = 0; i < 640*480; ++i)
				FProjective[i].Z = *d++;

			Point3D[] xyzp = FState.DepthGenerator.ConvertProjectiveToRealWorld(FProjective);

			for (int i = 0; i < 640 * 480; ++i, xyz += 3)
			{
				xyz[0] = xyzp[i].X / 1000.0f;
				xyz[1] = xyzp[i].Y / 1000.0f;
				xyz[2] = xyzp[i].Z / 1000.0f;
			}
		}
	}
}
