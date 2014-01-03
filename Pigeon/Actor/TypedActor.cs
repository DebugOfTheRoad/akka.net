﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pigeon.Actor
{
    public interface IHandle<TMessage>
    {
        void Handle(TMessage message);
    }

    public abstract class TypedActor : ActorBase
    {
        protected sealed override void OnReceive(object message)
        {
            var method = this.GetType().GetMethod("Handle", new[] { message.GetType() });
            if (method == null)
                throw new ArgumentException("Actor does not handle messages of type " + message.GetType().Name);

            method.Invoke(this, new[] { message });
        }
    }
}