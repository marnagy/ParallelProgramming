using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace dns_netcore
{
	class PatrikRecursiveResolver : IRecursiveResolver
	{
		private IDNSClient dnsClient;
		private static ConcurrentDictionary<string, Task<IP4Addr>> domCache = new ConcurrentDictionary<string, Task<IP4Addr>>();
		private static ConcurrentDictionary<string, bool> cached = new ConcurrentDictionary<string, bool>();

		public PatrikRecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
		}

		public async Task<IP4Addr> Resolving(string domain)
		{
			cached[domain] = true;
			//Console.WriteLine("REsREs   " + domain);
			var dotInd = domain.IndexOf('.');

			if (dotInd == -1)
			{
				IP4Addr rs = dnsClient.GetRootServers()[0];//mozno nie ten prvy
				var t = dnsClient.Resolve(rs, domain);
				return await t;
			}
			else
			{
				string subDom = domain.Substring(0, dotInd);
				var res = await ResolveRecursive(domain.Substring(dotInd + 1));

				var t = dnsClient.Resolve(res, subDom);
				return await t;
			}
		}

		public async Task<IP4Addr> ResolveRecursive(string domain)
		{
			//Console.WriteLine("doing   " + domain);

			return await Task<IP4Addr>.Run(() =>
			{
				if (cached.ContainsKey(domain))
				{
					//Console.WriteLine("cache   " + domain);
					string rev;
					try
					{
						rev = dnsClient.Reverse(domCache[domain].Result).Result;
					}
					catch
					{
						//Console.WriteLine("cache miss   " + domain);
						domCache[domain] = Resolving(domain);

						return domCache[domain];
					}

					if (rev != domain)
					{
						//Console.WriteLine("cache miss   " + domain);
						domCache[domain] = Resolving(domain);
					}

					return domCache[domain];
				}
				else
				{
					//Console.WriteLine("resolving   " + domain);
					domCache[domain] = Resolving(domain);

					return domCache[domain];
				}
			});
		}
	}
}
