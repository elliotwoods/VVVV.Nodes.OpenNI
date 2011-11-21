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
using Emgu.CV;
using Emgu.CV.Structure;

using VVVV.Nodes.EmguCV;

#endregion usings

namespace VVVV.Nodes.OpenNI
{

	class OpenNIState {
		public Context Context;
		/// <summary>
		/// We have a special instance here since we
		/// want the thread to always wait on this
		/// generator.
		/// </summary>
		public DepthGenerator DepthGenerator;
		public bool Running = false;

		public event EventHandler Initialised;
		public void OnInitialised()
		{
			if (Initialised == null)
				return;
			Initialised(this, EventArgs.Empty);
		}

		public event EventHandler Update;
		public void OnUpdate()
		{
			if (Update == null)
				return;
			Update(this, EventArgs.Empty);
		}
	}

	#region PluginInfo
	[PluginInfo(Name = "Context", Category = "OpenNI", Help = "OpenNI context loader", Tags = "")]
	#endregion PluginInfo
	public class ContextNode : IPluginEvaluate, IDisposable
	{
		#region fields & pins
		[Input("Open", IsBang = true, IsSingle = true)]
		ISpread<bool> FPinInOpen;

		[Output("Context")]
		ISpread<OpenNIState> FPinOutContext;

		[Output("Status")]
		ISpread<String> FPinOutStatus;

		[Import]
		ILogger FLogger;

		string FStatus = "";

		OpenNIState FState = new OpenNIState();
		License FLicense = new License();
		Thread FThread;
		#endregion fields & pins

		[ImportingConstructor]
		public ContextNode(IPluginHost host)
		{

		}

		public void Dispose()
		{
			Close();
		}

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if (FPinInOpen[0])
			{
				Open();
			}

			FPinOutContext[0] = FState;
			FPinOutStatus[0] = FStatus;
		}

		void Open()
		{
			try
			{
				Close();

				FLicense.Vendor = "PrimeSense";
				FLicense.Key = "0KOIk2JeIBYClPWVnMoRKn5cdY4";

				FState.Context = new Context();
				FState.Context.AddLicense(FLicense);
				FState.Context.GlobalMirror = false;

				FState.DepthGenerator = new DepthGenerator(FState.Context);
				FState.DepthGenerator.StartGenerating();

				FState.OnInitialised();

				FState.Running = true;
				FThread = new Thread(ThreadedFunction);
				FThread.Start();

				FStatus = "OK";
			}
			catch (Exception e)
			{
				Close();
				FStatus = e.Message;
			}
		}

		void Close()
		{
			if (FState.Running)
			{
				if (FState.Context != null)
				{
					FState.Context.Release();
				}

				if (FThread != null)
				{
					FState.Running = false;
					FThread.Join();
				}
			}
		}

		void ThreadedFunction()
		{
			while (FState.Running)
			{
				try
				{
					FState.Context.WaitOneUpdateAll(FState.DepthGenerator);
					FState.OnUpdate();
					FStatus = "OK";
				}
				catch (Exception e)
				{
					FStatus = e.Message;
				}
			}
		}

	}
}
