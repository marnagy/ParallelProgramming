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
		private readonly ConcurrentDictionary<string, Task<IP4Addr>> currentlyResolving = new();
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
			(var rootServerIp, _) = rootServersLoad.Min();
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
			var wholeSubdomains = Enumerable.Range(0, subdomains.Length)
				.Select( index => subdomains[(subdomainsLength-index)..] )
				.Select( subdomainsParts => String.Join('.', subdomainsParts) )
				.ToArray();
			Array.Reverse(subdomains);

			// // TODO: check if already computed IP is valid
			// int i = -1;
			// IP4Addr res;

			// for (int j = allSubdomains.Length - 1; j >= 0; j--)
			// {
			// 	if ( this.currentlyResolving.TryGetValue(allSubdomains[j], out var val) ){
			// 		i = j;
			// 		res = await val;
			// 		break;
			// 	}
					
			// }

			// i = i == -1 ? 0 : i;

			// //IP4Addr? res = null;
			// if (i == 0) {
			// 	// TODO: balance root servers' load
			// 	var taskRes = AskRootServer(subdomains[0]);
			// 	//var res = dnsClient.GetRootServers()[0];
			// 	this.currentlyResolving.TryAdd(allSubdomains[0], taskRes);
			// 	res = await this.currentlyResolving[allSubdomains[0]];
			// }
			// else {
			// 	res = await currentlyResolving[allSubdomains[i]];
			// }
			var t = Task.Run(async () => {
				var value = await AskRootServer(subdomains[0]);
				return value;
			});
			
			this.currentlyResolving.TryAdd(wholeSubdomains[0], t);

			Task<Task<IP4Addr>> t2 = null;
			foreach (var (subdomain, wholeSubdomain) in Enumerable.Zip(subdomains.Skip(1), wholeSubdomains.Skip(1)) )
			{
				t2 = t.ContinueWith(async addrTask => {
					var value = await dnsClient.Resolve(await addrTask, subdomain);
					return value;
				});
				this.currentlyResolving.TryAdd(wholeSubdomains[0], t);
				var value = await this.currentlyResolving[wholeSubdomains[0]];
			}

			t.Start();

			var finalAddr = await t2.Result;

			// for (i = ++i; i < subdomainsLength; i++)
			// {
			// 	var subdomain = subdomains[i];
			// 	var wholeSubdomain = allSubdomains[i];
			// 	// ? TODO: check if subdomain is being computed
			// 	if ( this.currentlyResolving.TryGetValue(wholeSubdomain, out var val) ) {
			// 		res = await val;
			// 	}
			// 	else {
			// 		this.currentlyResolving[wholeSubdomain] = dnsClient.Resolve(res, subdomain);
			// 		res = await this.currentlyResolving[wholeSubdomain];
			// 	}
			// }

			// ? TODO: remove addresses for all subdomains
			// what if we remove subpart of second resolving?
			foreach (var subdomain in wholeSubdomains)
			{
				if ( this.currentlyResolving.TryRemove(subdomain, out var val) ) {

				}
			}

			return finalAddr;
		}
	}
}
