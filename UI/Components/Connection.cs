using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LiveSplit.UI.Components
{
    public class MessageEventArgs : EventArgs
    {
        public Connection Connection { get; }
        public string Message { get; }
        public string[] Arguments { get; }

        public MessageEventArgs(Connection connection, string message, string[] arguments)
        {
            Connection = connection;
            Message = message;
            Arguments = arguments;
        }
    }

    class ErrorResponse : Response
    {
        public ErrorResponse() {
            error = new Exception("Something went wrong");
        }
        public ErrorResponse(string name): base(name) {
            error = new Exception("Something went wrong");
        }
        public ErrorResponse(string name, Exception error)  :base(name) {
            this.error = error;
        }

        public Exception error;
    }

    class Response
    {
        public Response() { }
        public Response(string name)
        {
            this.name = name;
        }
        public Response(string name, object data)
        {
            this.name = name;
            this.data = data;
        }
        public string name;
        public object data;
    }

    public delegate void MessageEventHandler(object sender, MessageEventArgs e);

    public class Connection : WebSocketBehavior
    {
        private Action<object, MessageEventArgs> connection_MessageReceived;
        private List<Action> disconnectHandlers = new List<Action>();

        public Connection(MessageEventHandler connection_MessageReceived)
        {
            MessageReceived += connection_MessageReceived;
        }

        public event MessageEventHandler MessageReceived;
        public event EventHandler Disconnected;

        protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
        {
            string command = e.Data;
            if (command != null)
            {
                var args = command.Split(' ');
                MessageReceived?.Invoke(this, new MessageEventArgs(this, args[0], args.SubArray(1, args.Length - 1)));
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            disconnectHandlers.ForEach((handler) => handler());
            base.OnClose(e);
            Disconnected?.Invoke(this, new EventArgs());
        }

        public void SendMessage(string message)
        {
            SendAsync(JsonConvert.SerializeObject(new Response(message)), (success) => { });
        }

        public void SendMessage(string message, object data)
        {
            SendAsync(JsonConvert.SerializeObject(new Response(message, data)), (success) => { });
        }

        public void addDisconnectHandler(Action handler)
        {
            disconnectHandlers.Add(handler);
        }

        internal void SendError(string message, Exception e)
        {
            SendAsync(JsonConvert.SerializeObject(new ErrorResponse(message, e)), (success) => { });
        }
    }
}
