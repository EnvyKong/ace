﻿using Grit.CQRS.Events;
using Newtonsoft.Json;
using RabbitMQ.Client.Framing.v0_9_1;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Grit.CQRS
{
    public class EventBus : IEventBus
    {
        private IEventHandlerFactory _eventHandlerFactory;
        private IList<Event> _events = new List<Event>();

        public EventBus(IEventHandlerFactory eventHandlerFactory)
        {
            _eventHandlerFactory = eventHandlerFactory;
        }

        public void Publish<T>(T @event) where T : Event
        {
            _events.Add(@event);
        }

        public void Flush()
        {
            foreach (Event @event in _events)
            {
                FlushAnEvent(@event);
            }
            _events.Clear();
        }

        private void FlushAnEvent<T>(T @event) where T : Event
        {
            ServiceLocator.BusLogger.EventPublish(@event);

            if (ServiceLocator.DistributeEventToQueue && @event.Outer)
            {
                DistributeAnEventToQueue(@event);
            }

            if (ServiceLocator.DistributeEventInProcess)
            {
                DistributeEventInProcess<T>(@event);
            }
        }

        private void DistributeEventInProcess<T>(T @event) where T : Event
        {
            var handlers = _eventHandlerFactory.GetHandlers<T>();
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    // handle event in thread pool
                    ThreadPool.QueueUserWorkItem(x =>
                    {
                        try
                        {
                            handler.Handle(@event);
                        }
                        catch (Exception ex)
                        {
                            ServiceLocator.BusLogger.Exception(@event, ex);
                        }
                    });
                }
            }
        }

        private static void DistributeAnEventToQueue<T>(T @event) where T : Event
        {
            try
            {
                ServiceLocator.EasyNetQBus.Publish(@event, @event.RoutingKey);
            }
            catch (Exception ex)
            {
                ServiceLocator.BusLogger.Exception(@event, ex);
            }
        }

        public void Invoke<T>(T @event) where T : Event
        {
            ServiceLocator.BusLogger.EventHandle(@event);

            var handlers = _eventHandlerFactory.GetHandlers<T>();
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        // handle event in current thread
                        handler.Handle(@event);
                    }
                    catch (Exception ex)
                    {
                        ServiceLocator.BusLogger.Exception(@event, ex);
                    }
                }
            }
        }

        public void Purge()
        {
            _events.Clear();
        }

        public void Subscribe(string subscriptionId, string topic)
        {
            var worker = new EventWorker();

            ServiceLocator.EasyNetQBus.Subscribe<Event>(subscriptionId,
                @event => worker.Execute(@event),
                x => x.WithTopic(topic));
        }

        public void SubscribeInParallel(string subscriptionId, string topic, int capacity)
        {
            var workers = new BlockingCollection<EventWorker>();
            for (int i = 0; i < capacity; i++)
            {
                workers.Add(new EventWorker());
            }

            ServiceLocator.EasyNetQBus.SubscribeAsync<Event>(subscriptionId,
                @event => Task.Factory.StartNew(() =>
                {
                    var worker = workers.Take();
                    try
                    {
                        worker.Execute(@event);
                    }
                    finally
                    {
                        workers.Add(worker);
                    }
                }),
                x => x.WithTopic(topic));
        }
    }
}
