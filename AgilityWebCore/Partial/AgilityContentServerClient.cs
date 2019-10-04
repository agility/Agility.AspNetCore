using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Description;
using System.Text;

namespace Agility.Web.AgilityContentServer
{
	internal partial class AgilityContentServerClient:IDisposable
	{

		public AgilityContentServerClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress, bool objGraphMax) :
			base(binding, remoteAddress)
		{
			foreach (var op in Endpoint.Contract.Operations)
			{
				var dataContractBehavior = op.Behaviors.Find<DataContractSerializerOperationBehavior>();
				if (dataContractBehavior != null)
				{
					dataContractBehavior.MaxItemsInObjectGraph = int.MaxValue;
				}
			}
		}

        public void Dispose()
        {
            GC.SuppressFinalize(true);
        }
    }
}
