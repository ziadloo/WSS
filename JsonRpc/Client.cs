using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Collections.Concurrent;

namespace JsonRpc
{
	public class Client
	{
		private long requestIdCounter = 1;
		private ConcurrentDictionary<long, Delegate> requestCallbacks = new ConcurrentDictionary<long, Delegate>();
		private ConcurrentDictionary<long, SmdService> smdService = new ConcurrentDictionary<long, SmdService>();

		public Client()
		{
		}

		public string RequestCall(string methodName, object[] param, Delegate callback = null)
		{
			Request req = new Request();
			req.Id = requestIdCounter;
			req.Method = methodName;
			req.Params = param;
			if (callback != null && !requestCallbacks.ContainsKey(requestIdCounter))
			{
				//Callback
				requestCallbacks.TryAdd(requestIdCounter, callback);
				//SMD
				Dictionary<string, Type> parameters = new Dictionary<string, Type>();
				ParameterInfo[] paramzs = callback.Method.GetParameters();
				if (paramzs.Length > 1)
				{
					throw new Exception("No client callback can have more than one paramters.");
				}
				for (int i = 0; i < paramzs.Length; i++)
				{
					parameters.Add(paramzs[i].Name, paramzs[i].ParameterType);
				}

				var resType = callback.Method.ReturnType;
				parameters.Add("returns", resType); // add the return type to the generic parameters list.
				smdService.TryAdd(requestIdCounter, new SmdService("POST", "JSON-RPC-2.0", parameters));
			}
			requestIdCounter++;
			return Newtonsoft.Json.JsonConvert.SerializeObject(req);
		}

		private object CleanUpParameter(object p, SmdAdditionalParameters metaData)
		{
			if (p == null || (p != null && p.GetType() == metaData.ObjectType))
            {
                return p;
            }

			var bob = p as Newtonsoft.Json.Linq.JValue;
			if (bob != null && (bob.Value == null || bob.Value.GetType() == metaData.ObjectType))
			{
				return bob.Value;
			}

			var paramI = p;
			try
			{
				return Newtonsoft.Json.JsonConvert.DeserializeObject(paramI.ToString(), metaData.ObjectType);
			}
			catch (Exception)
			{
				// no need to throw here, they will
				// get an invalid cast exception right after this.
			}

			return paramI;
		}

		public bool Handle(Response res)
		{
			long _id = -1;
			if (res.Id != null)
			{
				try
				{
					_id = Convert.ToInt64(res.Id);
				}
				catch {}
			}
			if (_id == -1)
			{
				return false;
			}

			Delegate handle = null;
			SmdService metadata = null;
			bool haveCallback = this.requestCallbacks.TryGetValue(_id, out handle);
			bool haveMetadata = this.smdService.TryGetValue(_id, out metadata);

			if (haveCallback == false || haveMetadata == false || handle == null || metadata == null)
			{
				return false;
			}

			if (res.Error != null)
			{
				{
					Delegate temp;
					requestCallbacks.TryRemove(_id, out temp);
				}
				{
					SmdService temp;
					smdService.TryRemove(_id, out temp);
				}
				return true;
			}

			object[] parameters = null;
			var metaDataParamCount = metadata.parameters.Count(x => x != null);

			if (metaDataParamCount != (res.Result == null ? 0 : 1))
			{
				throw new JsonRpcException(
					-32602
					, "Invalid params"
					, string.Format(
						"Expecting {0} parameters, and received {1}"
						, metadata.parameters.Length
						, (res.Result == null ? 0 : 1)
					)
				);
			}

			if (res.Result != null)
			{
				parameters = new object[1];
				parameters[0] = CleanUpParameter(res.Result, metadata.parameters[0]);
			}
			else
			{
				parameters = new object[0];
			}

			try
			{
				handle.DynamicInvoke(parameters);
				return true;
			}
			catch (Exception ex)
			{
				if (ex is TargetParameterCountException)
				{
					throw new JsonRpcException(-32602, "Invalid params", ex);
				}

				// We really dont care about the TargetInvocationException, just pass on the inner exception
				if (ex is JsonRpcException)
				{
					throw ex;
				}
				if (ex.InnerException != null && ex.InnerException is JsonRpcException)
				{
					throw ex.InnerException as JsonRpcException;
				}
				else if (ex.InnerException != null)
				{
					throw new JsonRpcException(-32603, "Internal Error", ex.InnerException);
				}

				throw new JsonRpcException(-32603, "Internal Error", ex);
			}
			finally
			{
				{
					Delegate temp;
					requestCallbacks.TryRemove(_id, out temp);
				}
				{
					SmdService temp;
					smdService.TryRemove(_id, out temp);
				}
			}
		}

		public bool Handle(string json)
		{
			try
			{
				Response res = new Response(json);
				return Handle(res);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
