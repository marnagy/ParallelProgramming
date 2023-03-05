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
		private const bool VERBOSE = false;
		private readonly IDNSClient dnsClient;
		private readonly IReadOnlyList<IP4Addr> rootServers;
		private readonly ConcurrentDictionary<string, Task<Task<IP4Addr>>> currentlyResolving = new();
		private readonly ConcurrentDictionary<IP4Addr, int> rootServersLoad = new();

		public RecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
			this.rootServers = client.GetRootServers();
			foreach (var rootServer in this.rootServers)
			{
				rootServersLoad.TryAdd(rootServer, 0);
			}
		}

		private async Task<IP4Addr> AskRootServer(string firstSubdomain) {
			(var rootServerIp, var server_load) = rootServersLoad.Min();
			rootServersLoad.TryUpdate(rootServerIp, server_load+1, server_load);
			var nextServer = await dnsClient.Resolve(rootServerIp, firstSubdomain);
			int later_server_load;
			while ( ! rootServersLoad.TryGetValue(rootServerIp, out later_server_load) ) { }
			rootServersLoad.TryUpdate(rootServerIp, later_server_load+1, later_server_load);
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
			var wholeSubdomains = Enumerable.Range(1, subdomains.Length)
				.Select( index => subdomains[(subdomainsLength-index)..] )
				.Select( subdomainsParts => String.Join('.', subdomainsParts) )
				.ToArray();
			Array.Reverse(subdomains);

			// TODO: check if already computed IP is valid
			int i = -1;
			IP4Addr res;
			Task<Task<IP4Addr>> t;

			for (int j = wholeSubdomains.Length - 1; j >= 0; j--)
			{
				if ( this.currentlyResolving.TryGetValue(wholeSubdomains[j], out var val) ){
					i = j;
					t = val;
					//res = await await val;
					break;
				}
					
			}

			i = i == -1 ? 0 : i;

			//IP4Addr res;
			if (i == 0) {
				// TODO: balance root servers' load
				t = Task.FromResult(AskRootServer(subdomains[0]));
				//var res = dnsClient.GetRootServers()[0];
				this.currentlyResolving.TryAdd(wholeSubdomains[0], t);
				//res = await await this.currentlyResolving[wholeSubdomains[0]];
			}
			else {
				t = currentlyResolving[wholeSubdomains[i]];
			}

			// t = Task.Run(async () => {
			// 	var value = await AskRootServer(subdomains[0]);
			// 	return Task.FromResult(value);
			// });

			if (VERBOSE)
				System.Console.WriteLine($"Started first task on {domain}");
			
			//this.currentlyResolving.TryAdd(wholeSubdomains[i], t);

			
			foreach (var (subdomain, wholeSubdomain) in Enumerable.Zip(subdomains.Skip(i+1), wholeSubdomains.Skip(i+1)) )
			{
				if (VERBOSE)
					System.Console.WriteLine($"Creating task for {subdomain} {wholeSubdomain} for {domain}");
				t = t.ContinueWith(async addr => {
					var addrTaskRes = await addr.Result;
					if (VERBOSE)
						System.Console.WriteLine($"Starting subtask on {subdomain} \"{wholeSubdomain}\", part of {domain} on server {addrTaskRes}");
					var value = dnsClient.Resolve(addrTaskRes, subdomain);
					if (VERBOSE)
						System.Console.WriteLine($"Resolved {subdomain} on {domain}");
					this.currentlyResolving.TryAdd(wholeSubdomain, Task.FromResult(value) );
					var valueRes = this.currentlyResolving[wholeSubdomain].Result;
					if (VERBOSE)
						System.Console.WriteLine($"Ending subtask {subdomain} on {wholeSubdomain}: server {valueRes}");
					return await valueRes;
				}, TaskContinuationOptions.PreferFairness);
			}

			var finalAddr = await await t;

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

			// // ? TODO: remove addresses for all subdomains
			// // what if we remove subpart of second resolving?
			foreach (var subdomain in wholeSubdomains)
			{
				if ( this.currentlyResolving.TryRemove(subdomain, out var val) ) {

				}
			}

			return finalAddr;
		}
	}
}
