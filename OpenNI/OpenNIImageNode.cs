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
	#region PluginInfo
	[PluginInfo(Name = "Images", Category = "OpenNI", Help = "OpenNI Image generator", Tags = "")]
	#endregion PluginInfo
	public class OpenNIImageNode : IPluginEvaluate, IDisposable
	{
		[DllImport("msvcrt.dll", EntryPoint = "memcpy")]
		public unsafe static extern void CopyMemory(IntPtr pDest, IntPtr pSrc, int length);

		#region fields & pins
		[Input("Context")]
		ISpread<OpenNIContext> FPinInContext;

		[Output("RGB")]
		ISpread<CVImageLink> FPinOutImageRGB;

		[Output("Depth")]
		ISpread<CVImageLink> FPinOutImageDepth;

		[Output("World")]
		ISpread<CVImageLink> FPinOutImageWorld;

		[Output("Status")]
		ISpread<String> FPinOutStatus;

		CVImageOutput FImageRGB = new CVImageOutput();
		CVImageOutput FImageDepth = new CVImageOutput();
		CVImageOutput FImageWorld = new CVImageOutput();

		[Import]
		ILogger FLogger;

		string FConfig;
		OpenNIContext FContext;
		bool FRunning = false;

		ImageGenerator FImageGenerator;
		DepthGenerator FDepthGenerator;

		Thread FThread;
		#endregion fields & pins

		[ImportingConstructor]
		public OpenNIImageNode(IPluginHost host)
		{

		}

		public void Dispose()
		{

		}

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if (FPinInContext[0] != FContext)
			{
				FContext = FPinInContext[0];
			}

			if (FContext == null)
			{
				FRunning = false;
				return;
			}

			if (FContext.running && !FRunning && FContext.context != null)
			{
				try
				{
					FImageGenerator = FContext.context.FindExistingNode(global::OpenNI.NodeType.Image) as ImageGenerator;
					FDepthGenerator = FContext.context.FindExistingNode(global::OpenNI.NodeType.Depth) as DepthGenerator;
					FDepthGenerator.AlternativeViewpointCapability.SetViewpoint(FImageGenerator);

					Size size = new Size(640, 480);

					FImageRGB.Image.Initialise(size, TColourFormat.RGB8);
					FImageDepth.Image.Initialise(size, TColourFormat.L16);
					FImageWorld.Image.Initialise(size, TColourFormat.RGB32F);

					FPinOutImageRGB[0] = FImageRGB.Link;
					FPinOutImageDepth[0] = FImageDepth.Link;
					FPinOutImageWorld[0] = FImageWorld.Link;

					FThread = new Thread(fnThread);
					FRunning = true;
					FThread.Start();
					FPinOutStatus[0] = "OK";
				}
				catch (StatusException e)
				{
					FRunning = false;
					FPinOutStatus[0] = e.Message;
				}
			}


		}

		private unsafe void fnThread()
		{
			while (FRunning)
			{
				this.FContext.context.WaitOneUpdateAll(this.FDepthGenerator);

				byte* rgbs = (byte*)FImageGenerator.ImageMapPtr.ToPointer();
				byte* rgbd = (byte*)FImageRGB.Image.Data.ToPointer();

				for (int i=0; i<640*480; i++) {
					rgbd[2] = rgbs[0];
					rgbd[1] = rgbs[1];
					rgbd[0] = rgbs[2];
					rgbs += 3;
					rgbd += 3;
				}

				CopyMemory(FImageDepth.Image.Data, FDepthGenerator.DepthMapPtr, 640 * 480 * 2);

				fillWorld();

				FImageRGB.Send();
				FImageDepth.Send();
				FImageWorld.Send();
			}
		}

		private unsafe void fillWorld()
		{
			float* DataXYZ = (float*)FImageWorld.Data.ToPointer();
			ushort* DataDepth = (ushort*)FImageDepth.Data.ToPointer();

			//calculate world positions and move pointer
			for (int iY = 0; iY < 480; iY++)
				for (int iX = 0; iX < 640; iX++, DataXYZ += 3, DataDepth++)
					DepthToWorld(iX, iY, DataDepth[0], DataXYZ);
		}

		const float fx_d = 1.0f / 5.9421434211923247e+02f;
		const float fy_d = 1.0f / 5.9104053696870778e+02f;
		const float cx_d = 3.3930780975300314e+02f;
		const float cy_d = 2.4273913761751615e+02f;

		private unsafe void DepthToWorld(int x, int y, int depthValue, float* data)
		{
			//adapted from http://graphics.stanford.edu/~mdfisher/Kinect.html

			//presume we're in mm units
			data[2] = (float)depthValue / 1000.0f;
			data[0] = ((float)x - cx_d) * fx_d * data[2];
			data[1] = -((float)y - cy_d) * fy_d * data[2];
		}

	}
}
