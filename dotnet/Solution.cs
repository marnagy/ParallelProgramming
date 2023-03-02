using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace dns_netcore
{
	class RecursiveResolver : IRecursiveResolver
	{
		private readonly IDNSClient dnsClient;
		private readonly IReadOnlyList<IP4Addr> rootServers;
		private readonly Dictionary<string, IP4Addr> currentlyResolving = new();
		private readonly Dictionary<IP4Addr, int> rootServersLoad = new();

		public RecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
			this.rootServers = client.GetRootServers();
			foreach (var rootServer in this.rootServers)
			{
				rootServersLoad.Add(rootServer, 0);
			}
		}

		private async Task<IP4Addr> AskRootServer(string firstSubdomain) {
			var rootServerIndex = rootServersLoad.Keys
				.Min(server => this.rootServersLoad[server]);
			var rootServerIp = rootServers[rootServerIndex];
			rootServersLoad[rootServerIp] += 1;
			var nextServer = await dnsClient.Resolve(rootServerIp, firstSubdomain);
			rootServersLoad[rootServerIp] -= 1;
			return nextServer;
		}

		public async Task<IP4Addr> ResolveRecursive(string domain)
		{
			/*
			 * Just copy-pasted code from serial resolver.
			 * Replace it with your implementation.
			 * Also you may change this method to async (it will work with the interface).
			 */

			var subdomains = domain.Split('.');
			Array.Reverse(subdomains);
			// TODO: check if subdomain is being computed
			// TODO: check if already computed IP is valid

			// DONE: balance root servers' load
			var serverToAsk = await AskRootServer(subdomains[0]);
			
			foreach (var subdomain in subdomains[1..])
			{
				var newServerToAsk = await dnsClient.Resolve(serverToAsk, subdomain);
				serverToAsk = newServerToAsk;
			}

			return serverToAsk;
		}
	}
}
