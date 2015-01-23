using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace JsonRpc
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Response
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "jsonrpc")]
        public string JsonRpc { get { return "2.0"; } }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "result")]
        public object Result { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "error")]
        public JsonRpcException Error { get; set; }

        [JsonProperty(PropertyName = "id")]
        public object Id { get; set; }

		public Response()
		{

		}

		public Response(string json)
		{
			Response res;
			if (json.Length > 0)
			{
				res = Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(json);
				if (res == null)
				{
					throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text.");
				}
				else
				{
					if (res.Result == null && res.Error == null)
					{
						throw new JsonRpcException(-32600, "Invalid Request", "Missing both properties 'result' and 'error'");
					}
				}
			}
			else
			{
				throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. Empty text was given as request.");
			}

			this.Result = res.Result;
			this.Error = res.Error;
			this.Id = res.Id;
		}

		public Response(Response res)
		{
			if (res == null)
			{
				throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text.");
			}
			else
			{
				if (res.Result == null && res.Error == null)
				{
					throw new JsonRpcException(-32600, "Invalid Request", "Missing both properties 'result' and 'error'");
				}
			}

			this.Result = res.Result;
			this.Error = res.Error;
			this.Id = res.Id;
		}
    }
}
