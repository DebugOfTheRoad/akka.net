﻿using Pigeon.Dispatch.SysMsg;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pigeon.Actor
{
    public partial class ActorCell 
    {
        public void Invoke(Envelope envelope)
        {
            this.CurrentMessage = envelope.Message;
            this.Sender = envelope.Sender;
            //set the current context
            UseThreadContext(() =>
            {
                try
                {
                    Default(envelope.Message);
                }
                catch (Exception reason)
                {
                    Parent.Tell(new SuperviceChild(reason));
                }
            });
        }

        public void SystemInvoke(Envelope envelope)
        {
            this.CurrentMessage = envelope.Message;
            this.Sender = envelope.Sender;
            //set the current context
            UseThreadContext(() =>
            {
                try
                {
                    Pattern.Match(envelope.Message)
                        //kill this actor
                        .With<Kill>(Kill)
                        //request to stop a child
                        .With<StopChild>(m => StopChild(m.Child))
                        //request to restart a child
                        .With<RestartChild>(m => RestartChild(m.Child))
                        //kill this actor
                        .With<PoisonPill>(PoisonPill)
                        //someone is watching us
                        .With<Watch>(Watch)
                        //someone is unwatching us
                        .With<Unwatch>(Unwatch)
                        //complete the future callback by setting the result in this thread
                        .With<CompleteFuture>(CompleteFuture)
                        //resolve time distance to actor
                        .With<Ping>(Ping)
                        //supervice exception from child actor
                        .With<SuperviceChild>(SuperviceChild)
                        //handle any other message
                        .With<Identity>(Identity)
                        //forward pong to user
                        .With<Pong>(Default)
                        .Default(m => { throw new NotImplementedException(); });
                }
                catch (Exception reason)
                {
                    Parent.Tell(new SuperviceChild(reason));
                }
            });
        }
		private void Identity(Identity m)
        {
            Sender.Tell(new ActorIdentity(m.MessageId, this.Self));
        }

        private void PoisonPill(PoisonPill m)
        {

        }

		/// <summary>
		/// Async restart this actor
		/// </summary>
        public void Restart()
        {
            this.Parent.Tell(new RestartChild(this.Self));
        }

        private void RestartChild(LocalActorRef child)
        {
            isTerminating = false;
            StopChild(child);
            Debug.WriteLine("restarting child: {0}", child.Path);
            Unbecome();//unbecome deadletters
            NewActor(child.Cell);
        }

		/// <summary>
		/// Async stop this actor
		/// </summary>
        public void Stop()
        {
            if (isTerminating)
                return;

            if (this.Parent != null)
            {
                this.Parent.Tell(new StopChild(this.Self));
            }
            foreach (var child in this.GetChildren())
            {
                child.Stop();
            }
        }

        private volatile bool isTerminating = false;
        private void StopChild(LocalActorRef child)
        {
            if (isTerminating)
                return;

            isTerminating = true;
            Debug.WriteLine("stopping child: {0}", child.Path);
            child.Cell.Become(System.DeadLetters.Tell);
            LocalActorRef tmp;
            var name = child.Path.Name;
            this.Children.TryRemove(name, out tmp);
        }

        private void Kill(Kill m)
        {
            throw new ActorKilledException("Kill");
        }
        private void Default(object m)
        {
            CurrentBehavior(m);
        }

        private void SuperviceChild(SuperviceChild m)
        {
            switch (this.Actor.SupervisorStrategyLazy().Handle(Sender, m.Reason))
            {
                case Directive.Escalate:
                    throw m.Reason;
                case Directive.Resume:
                    break;
                case Directive.Restart:
                    RestartChild((LocalActorRef)Sender);
                    break;
                case Directive.Stop:
                    StopChild((LocalActorRef)Sender);
                    break;
                default:
                    break;
            }
        }

        private void Ping(Ping m)
        {
            Sender.Tell(
                        new Pong
                        {
                            LocalUtcNow = m.LocalUtcNow,
                            RemoteUtcNow = DateTime.UtcNow
                        });
        }

        private BroadcastActorRef Watchers = new BroadcastActorRef();
        private void Watch(Watch m)
        {
            Watchers.Add(Sender);
        }

        private void Unwatch(Unwatch m)
        {
            Watchers.Remove(Sender);
        }

        private void CompleteFuture(CompleteFuture m)
        {
            m.SetResult();
        }
    }
}