﻿using ACE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Watcher.Foodie.Model;
using System.Collections.Concurrent;

namespace Watcher.Foodie
{
    class Program
    {
        private static ConnectionMultiplexer redis;
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            BootStrapper.BootStrap();
            redis = ConnectionMultiplexer.Connect(Grit.Configuration.Redis.Configuration);

            BootStrapper.EventStation.SubscribeAsync("WatcherFoodie", new string[] { "#" }, x=> Increase(x.ToString()));

            Console.WriteLine("Ctrl-C to exit");
            Console.CancelKeyPress += (source, cancelKeyPressArgs) =>
            {
                Console.WriteLine("Shut down...");
                BootStrapper.Dispose();
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Console.WriteLine("Shut down completed");
            };

            Task.Run(() => {
                while (true)
                {
                    CleanAll().Wait();
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            });

            Thread.Sleep(Timeout.Infinite);
        }

        private static readonly IEnumerable<IPeriod> _countPeriods = new List<IPeriod>() { new Seconds(), new Minutes(), new Hours(), new Days()  };
        private static ConcurrentDictionary<string, DateTime> _waitingForClean = new ConcurrentDictionary<string, DateTime>();
        private static IList<Rule> _rules = new List<Rule>()
        {
             new Rule { Interval = 2, Period = new Seconds(), Predicate = new GreatThan(), Times = 10, Repeats = 5  }
        };

        private static async Task Increase(string name)
        {
            DateTime now = DateTime.Now;
            IDatabase db = redis.GetDatabase();
            foreach (var period in _countPeriods)
            {
                await db.HashIncrementAsync(period.Key(name), period.Field(now), 1);
            }
            _waitingForClean.TryAdd(name, now);
        }

        private static async Task CleanAll()
        {
            var list = _waitingForClean.ToArray();
            _waitingForClean.Clear();
            foreach(var item in list)
            {
                await Clean(item.Key);
            }
            Console.WriteLine("Clean: " + list.Length);
        }

        private static async Task Clean(string name)
        {
            DateTime now = DateTime.Now;
            IDatabase db = redis.GetDatabase();
            foreach (var period in _countPeriods)
            {
                var keys = await db.HashKeysAsync(period.Key(name));
                var keeps = period.KeepKeys(now);
                var removed = keys
                    .Select(n => n.ToString())
                    .Where(n => !keeps.Any(k => n.StartsWith(k)))
                    .Select(n => (RedisValue)n)
                    .ToArray();
                await db.HashDeleteAsync(period.Key(name), removed);
            }
        }

        private static async Task Patral(string name)
        {

        }
    }
}