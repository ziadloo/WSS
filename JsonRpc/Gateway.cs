using System;

namespace JsonRpc
{
	public class Gateway
	{
		public enum HandleType { None, Request, Response }

		private Server server = new Server();
		public Server Server
		{
			get { return server; }
		}

		private Client client = new Client();
		public Client Client
		{
			get { return client; }
			set { client = value; }
		}

		public Gateway()
		{
		}
		
		public void AddService(IService service)
		{
			server.AddService(service);
		}
		
		public void AddService(IService[] services)
		{
			server.AddService(services);
		}

		public string RequestCall(string methodName, object[] param, Delegate callback = null)
		{
			return client.RequestCall(methodName, param, callback);
		}

		public string Handle(string json, out HandleType type)
		{
			type = HandleType.None;
			Request req;
			try
			{
				req = new Request(json);
			}
			catch
			{
				try
				{
					Response res = new Response(json);
					bool r = client.Handle(res);
					type = HandleType.Response;
					if (r)
					{
						return "true";
					}
					else
					{
						return "false";
					}
				}
				catch (Exception)
				{
					return "false";
				}
			}

			try
			{
				Response res = server.Handle(req);
				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				type = HandleType.Request;
				return result;
			}
			catch (JsonRpcException ex)
			{
				Response res = new Response();
				try
				{
					req = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(json);
					res.Id = req.Id;
				}
				catch (Exception) {}
				res.Error = ex;

				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				return result;
			}
			catch (Exception ex)
			{
				Response res = new Response();
				try
				{
					req = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(json);
					res.Id = req.Id;
				}
				catch (Exception) {}
				res.Error = new JsonRpcException(0, ex.Message, ex.Data);

				var result = Newtonsoft.Json.JsonConvert.SerializeObject(res);
				return result;
			}
		}
	}
}

