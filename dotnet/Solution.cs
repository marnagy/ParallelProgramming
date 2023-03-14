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
		// variables for debugging
		private const bool VERBOSE = false;
		

		private readonly IDNSClient dnsClient;
		private readonly IReadOnlyList<IP4Addr> rootServers;
		private readonly ConcurrentDictionary<string, Task<Task<IP4Addr>>> currentlyResolving = new();
		private readonly ConcurrentDictionary<string, IP4Addr> cache = new();
		private readonly ConcurrentDictionary<IP4Addr, int> rootServersLoad = new();

		public RecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
			this.rootServers = client.GetRootServers();
			foreach (var rootServer in this.rootServers)
			{
				rootServersLoad[rootServer] = 0;
			}
		}

		private async Task<IP4Addr> AskRootServer(string firstSubdomain) {
			(var rootServerIp, var server_load) = rootServersLoad.Min();
			rootServersLoad.TryUpdate(rootServerIp, server_load+1, server_load);
			var nextServer = await dnsClient.Resolve(rootServerIp, firstSubdomain);
			this.cache[firstSubdomain] = nextServer;
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
				//.AsParallel()
				.Select( index => subdomains[(subdomainsLength-index)..] )
				.Select( subdomainsParts => String.Join('.', subdomainsParts) )
				.ToArray();
			Array.Reverse(subdomains);

			// TODO: check if already computed IP is valid
			int i = -1;				
			Task<Task<IP4Addr>> t = null;

			for (int j = wholeSubdomains.Length - 1; j >= 0; j--)
			{
				var wholeSubdomain = wholeSubdomains[j];

				if (VERBOSE)
					System.Console.WriteLine($"Checking for {wholeSubdomain} in cache or currently computing for {domain}");

				if ( this.cache.TryGetValue(wholeSubdomain, out var addr) ) {
					try{
						var subdomain = await this.dnsClient.Reverse(addr);
						if ( subdomain == wholeSubdomain ){
							i = j;
							t = Task.FromResult(Task.FromResult(addr));
						}
						else{
							this.cache.TryRemove(wholeSubdomain, out var _);
							continue;
						}
					}
					catch (DNSClientException) {
						continue;
					}
					if (VERBOSE)
						System.Console.WriteLine($"Found cached task for {wholeSubdomain} on {domain}");
					break;
				}

				if ( this.currentlyResolving.ContainsKey(wholeSubdomain) ){
					var val = this.currentlyResolving[wholeSubdomain];
					i = j;
					if (VERBOSE)
						System.Console.WriteLine($"Found currently computing task for {wholeSubdomain} on {domain}");
					break;
				}
					
			}

			i = i == -1 ? 0 : i;

			if (VERBOSE) {
				System.Console.WriteLine($"Continuing DNS from {i} -> {wholeSubdomains[i]}");
			}

			//IP4Addr res;
			if (i == 0 && t == null) {
				// TODO: balance root servers' load
				t = Task.FromResult( AskRootServer(subdomains[0]) );
				this.currentlyResolving[wholeSubdomains[0]] = t;
			}
			
			t = t ?? currentlyResolving[wholeSubdomains[i]];

			if (VERBOSE)
				System.Console.WriteLine($"Started first task on {domain}");
			
			foreach (var (subdomain, wholeSubdomain) in Enumerable.Zip(subdomains[(i+1)..], wholeSubdomains[(i+1)..]) )
			{
				if (VERBOSE)
					System.Console.WriteLine($"Creating task for {subdomain} {wholeSubdomain} for {domain}");
				t = t.ContinueWith(async addr => {
					var addrTaskRes = await addr;
					return await await addrTaskRes.ContinueWith(async serverAddr => {
						var serverIP = await serverAddr;
						if (VERBOSE)
							System.Console.WriteLine($"Starting subtask on {subdomain} \"{wholeSubdomain}\", part of {domain} on server {serverIP}");
						var value = await dnsClient.Resolve(serverIP, subdomain);
						this.cache[wholeSubdomain] = value;
						this.currentlyResolving.TryRemove(wholeSubdomain, out var _);
						return value;
					});
				});
				this.currentlyResolving[wholeSubdomain] = t;
			}

			var finalAddr = await await t;
			return finalAddr;
		}
	}
}
