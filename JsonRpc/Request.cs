using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace JsonRpc
{
    /// <summary>
    /// Represents a JsonRpc request
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Request
    {
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "jsonrpc")]
		public string JsonRpc { get { return "2.0"; } }

		public Request()
		{}

        public Request(string json)
        {
			Request req;
			if (json.Length > 0)
			{
				req = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(json);
				if (req == null)
				{
					throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text.");
				}
				else
				{
					if (req.Method == null)
					{
						throw new JsonRpcException(-32600, "Invalid Request", "Missing property 'method'");
					}
				}
			}
			else
			{
				throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. Empty text was given as request.");
			}

			this.Method = req.Method;
			this.Params = req.Params;
			this.Id = req.Id;
		}

		public Request(Request req)
		{
			if (req == null)
			{
				throw new JsonRpcException(-32700, "Parse error", "Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text.");
			}
			else
			{
				if (req.Method == null)
				{
					throw new JsonRpcException(-32600, "Invalid Request", "Missing property 'method'");
				}
			}
			
			this.Method = req.Method;
			this.Params = req.Params;
			this.Id = req.Id;
		}

		[JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public object Params { get; set; }

        [JsonProperty("id")]
        public object Id { get; set; }
    }
}
