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
		private readonly Dictionary<string, Task<IP4Addr>> currentlyResolving = new();
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

			var subdomains = domain.Split('.').ToArray();
			var subdomainsLength = subdomains.Length;
			var allSubdomains = Enumerable.Range(0, subdomains.Length)
				.Select( index => subdomains[(subdomainsLength-index)..] )
				.Select( subdomainsParts => String.Join('.', subdomainsParts) )
				.ToArray();
			Array.Reverse(subdomains);

			// TODO: check if already computed IP is valid
			// How though?

			// DONE: balance root servers' load
			var res = await AskRootServer(subdomains[0]);
			this.currentlyResolving[allSubdomains[0]] = Task.FromResult(res);

			for (int i = 1; i < subdomainsLength; i++)
			{
				var subdomain = subdomains[i];
				var wholeSubdomain = allSubdomains[i];
				// ? TODO: check if subdomain is being computed
				if ( this.currentlyResolving.TryGetValue(wholeSubdomain, out var val) ) {
					res = await val;
				}
				else {
					this.currentlyResolving[wholeSubdomain] = dnsClient.Resolve(res, subdomain);
					res = await this.currentlyResolving[wholeSubdomain];
				}
			}

			// ? TODO: remove addresses for all subdomains
			// what if we remove subpart of second resolving?
			foreach (var subdomain in allSubdomains)
			{
				if ( this.currentlyResolving.TryGetValue(subdomain, out var val) )
					this.currentlyResolving.Remove(subdomain);
			}

			return res;
		}
	}
}
