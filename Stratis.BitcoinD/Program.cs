﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
			NodeArgs nodeArgs = NodeArgs.GetArgs(args);
			FullNode node = new FullNode(nodeArgs);
			// new mining code
			node.Network.Consensus.PowNoRetargeting = false;
			node.Network.Consensus.PowAllowMinDifficultyBlocks = false;
			node.Network.Consensus.PowTargetTimespan = TimeSpan.FromMinutes(10);
			node.Network.Consensus.PowTargetSpacing = TimeSpan.FromMinutes(1);

			CancellationTokenSource cts = new CancellationTokenSource();

			if (args.Any(a => a.Contains("mine")))
			{
				new Thread(() =>
				{
					Thread.Sleep(10000); // let the node start
					while (!node.IsDisposed)
					{
						Thread.Sleep(100); // wait 1 sec
						// generate 1 block
						var res = node.Miner.GenerateBlocks(new Stratis.Bitcoin.Miner.ReserveScript()
						{
							reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey
						}, 1, int.MaxValue, false);
						if (res.Any())
							Console.WriteLine("mined tip at: " + node?.Chain.Tip.Height + " h:" + node?.Chain.Tip.HashBlock + " d:" +
							                  node?.Chain.Tip.Header.Bits.ToUInt256());
					}
				})

				{
					IsBackground = true //so the process terminate
				}.Start();
			}
			new Thread(() =>
			{
				Console.WriteLine("Press one key to stop");
				Console.ReadLine();
				node.Dispose();
			})
			{
				IsBackground = true //so the process terminate
			}.Start();
			node.Start();

#if DEBUG
			var webWallet = new Dashboard.DashboardService(config =>
			{
				//in debug mode, it gets files from physical path, so i set a relative path to my web content.
				//in production mode, it gets contents from embedded resource and this parameter isn't used
				var appFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
				config.ContentRoot = System.IO.Path.Combine(appFolder, "..", "..", "..", "..", "Stratis.Dashboard");
				Console.WriteLine($"ContentRoot set to {config.ContentRoot}");
			});
#else
         var webWallet = new Dashboard.DashboardService();
#endif

			webWallet.AttachNode(node);
			webWallet.Start();

			node.WaitDisposed();
			node.Dispose();
		}
	}
}
