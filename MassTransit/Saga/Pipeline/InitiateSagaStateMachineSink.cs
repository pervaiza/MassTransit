// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Saga.Pipeline
{
	using System;
	using System.Collections.Generic;
	using Exceptions;
	using log4net;
	using Magnum;
	using Magnum.StateMachine;
	using MassTransit.Pipeline.Configuration.Subscribers;

    public class InitiateSagaStateMachineSink<TComponent, TMessage> :
		SagaMessageSinkBase<TComponent, TMessage>
		where TMessage : class, CorrelatedBy<Guid>
		where TComponent : SagaStateMachine<TComponent>, ISaga
	{
		private static readonly ILog _log = LogManager.GetLogger(typeof (InitiateSagaStateMachineSink<TComponent, TMessage>).ToFriendlyName());

		public InitiateSagaStateMachineSink(ISubscriberContext context, IServiceBus bus, ISagaRepository<TComponent> repository, DataEvent<TComponent,TMessage> dataEvent)
			: base(context, bus, repository)
		{
			EventToRaise = dataEvent;
		}

		public Event<TMessage> EventToRaise { get; set; }

		public override IEnumerable<Action<TMessage>> Enumerate(TMessage message)
		{
			var sagaId = message.CorrelationId;
			if (sagaId == Guid.Empty)
				sagaId = CombGuid.Generate();

			using (var enumerator = Repository.InitiateNewSaga(sagaId))
			{
				bool found = false;
				try
				{
					found = enumerator.MoveNext();
				}
				catch (SagaException sax)
				{
					_log.Error(sax);
				}

				if (!found)
				{
					yield return x => { };
					yield break;
				}

				do
				{
					var component = enumerator.Current;

					component.Bus = Bus;

					yield return x => component.RaiseEvent(EventToRaise, x);

				} while (enumerator.MoveNext());
			}
		}
	}
}