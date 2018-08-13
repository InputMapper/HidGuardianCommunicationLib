using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using Newtonsoft.Json;

namespace IMPluginInterface
{
    public enum HidGuardianStatusCodes { None, Received, Accepted, Rejected, Processing, Processed, Error }

    class HidGuardianClient: IDisposable
    {
        private uint clientPid;
        private List<string> acceptedDevices = new List<string>();
        private Dictionary<Guid, HidGuardianStatusCodes> requestStatuses = new Dictionary<Guid, HidGuardianStatusCodes>();
        private WebSocket ws;

        public HidGuardianClient(uint clientPid, string serverAddress = "ws://127.0.0.1:22408")
        {
            this.clientPid = clientPid;
            this.ws = new WebSocket(serverAddress);
            ws.OnMessage += Ws_OnMessage;
            ws.Connect();
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e)
        {

            if (e.IsText)
            {
                try
                {
                    dynamic wsMessage = JsonConvert.DeserializeObject<dynamic>((string)e.Data);


                    if ((string)wsMessage.Header.Kind == "AccessRequest")
                    {
                        if (!requestStatuses.ContainsKey((Guid)wsMessage.Header.Id))
                            requestStatuses[(Guid)wsMessage.Header.Id] = HidGuardianStatusCodes.Received;

                        HidGuardianRequest wsRequest = JsonConvert.DeserializeObject<HidGuardianRequest>((string)e.Data);

                        if (wsRequest.ProcessId == clientPid)

                            if (wsRequest.HardwareIds.Intersect(acceptedDevices).Count() > 0)
                            {
                                requestStatuses[(Guid)wsMessage.Header.Id] = HidGuardianStatusCodes.Accepted;
                                var wsResponse = new HidGuardianRequestResponse(wsRequest.Header.Id, true);
                                ws.Send(JsonConvert.SerializeObject(wsResponse));
                                requestStatuses[(Guid)wsMessage.Header.Id] = HidGuardianStatusCodes.Processing;
                            }
                            else
                            {
                                requestStatuses[(Guid)wsMessage.Header.Id] = HidGuardianStatusCodes.Rejected;
                                var wsResponse = new HidGuardianRequestResponse(wsRequest.Header.Id, false);
                                ws.Send(JsonConvert.SerializeObject(wsResponse));
                                requestStatuses[(Guid)wsMessage.Header.Id] = HidGuardianStatusCodes.Processing;
                            }
                    }
                    else if ((string)wsMessage.Header.Kind == "Confirmation")
                    {
                        HidGuardianConfirmation wsConfirmation = JsonConvert.DeserializeObject<HidGuardianConfirmation>((string)e.Data);

                        if ((Guid)wsMessage.Header.Id != Guid.Empty && !requestStatuses.ContainsKey((Guid)wsMessage.Header.Id))
                            requestStatuses[(Guid)wsMessage.Header.Id] = wsConfirmation.Code == 200 ? 
                                HidGuardianStatusCodes.Processed : HidGuardianStatusCodes.Error;

                    }
                } catch
                {
                    // Not a recignized or properly formatted request from HidGuardian server
                }
            }
        }

        public void WhitelistDevices(string[] hardwareIds)
        {
            acceptedDevices.AddRange(hardwareIds);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ws.Close();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    class Header
    {
        public Guid Id { get; internal set; }
        public string Kind { get; internal set; }
    }

    class HidGuardianRequest
    {
        public Header Header { get; internal set; }
        public List<string> HardwareIds { get; internal set; }
        public string DeviceId { get; internal set; }
        public string InstanceId { get; internal set; }
        public int ProcessId { get; internal set; }
    }

    class HidGuardianRequestResponse
    {
        public Header Header { get; internal set; } = new Header();
        public bool IsAllowed { get; internal set; }
        public bool IsPermanent { get; internal set; }

        internal HidGuardianRequestResponse(Guid RequestId, bool IsAllowed, bool IsPermanent = true)
        {
            this.Header.Id = RequestId;
            this.IsAllowed = IsAllowed;
            this.IsPermanent = IsPermanent;
        }
    }

    class HidGuardianConfirmation
    {
        public Header Header { get; internal set; } = new Header();
        public int Code { get; internal set; }
        public string Message { get; internal set; }
    }
}
