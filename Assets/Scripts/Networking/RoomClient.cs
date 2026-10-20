using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using LestingCraft.Gameplay;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
#endif

namespace LestingCraft.Networking
{
    [Serializable] public class NetBlock {public int x,y,z,block;public long rev;}
    [Serializable] public class NetPlayer {public int offhand,weapon;public string id,name;public float x,y,z,yaw;public int[] equipment;}
    [Serializable] public class NetLaunchSite {public bool active;public int x,y,z;}
    [Serializable] public class NetShuttleLift
    {public bool active,moving,occupied;public float progress,height;public string direction,riderId;public long serverNow;}
    [Serializable] public class NetSpaceMission
    {public bool active;public string from,to,phase;public double elapsed,seconds,landingBurn;public double[] position;public bool pilot,autopilot;public int crewCount,seatIndex;public long ignitionAt;}
    [Serializable] public class NetStationDock
    {public bool latched,hatchOpen;public long latchedAt;}
    [Serializable] public class NetStationVisit
    {public bool active;public NetStationDock dock;public float[] local;public int crewCount;}
    [Serializable] public class NetPacket
    {
        public LivingSave living;public string ownerTag;
        // Private room mission snapshots are parsed, but the terrestrial client must refuse to render them as Earth.
        public NetLaunchSite launchSite;public NetShuttleLift lift;public NetSpaceMission space;public NetStationVisit station;public float[] local;public int stationRequestId,role,index;
        public LestingCraft.Story.StoryState story;public bool lobby,started,host;public string hostId,action;public int arg,stage,line,offhand,expectedRevision;public long serverNow;public string type,id,name,message,code,animal;public int weapon,requestId,contentVersion;public AnimalState[] animals;public AnimalLoot[] loot;public AnimalDamage[] damageEvents;public int seed,dimension,version,block,maxPlayers,mode,rulesVersion,serverRulesVersion;public bool runEnded,force,hatchOpen,open;public CampaignProgress campaign;public PlayerSave save;public long rev;
        public int[] equipment;public string[] seats,issues;public bool ready;public double clock;public float x,y,z,yaw;public NetBlock[] edits;public NetPlayer[] players;
    }
    public sealed class RoomClient : MonoBehaviour
    {
        public event Action<NetPacket> Message;
        public event Action<string> Status;
        public bool Connected {get;private set;}
        readonly ConcurrentQueue<string> incoming=new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> statuses=new ConcurrentQueue<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LC_Connect(string url,string objectName);
        [DllImport("__Internal")] static extern void LC_Send(string json);
        [DllImport("__Internal")] static extern void LC_Close();
#else
        ClientWebSocket socket;
        CancellationTokenSource cancellation;
        readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);
#endif
        public void Connect(string endpoint,string code,string playerName,string playerKey="")
        {
            gameObject.name="LestingCraftNetwork";
            string url=endpoint.TrimEnd('/').Replace("https://","wss://").Replace("http://","ws://")+"/api/rooms/"+Uri.EscapeDataString(code.Trim().ToLowerInvariant())+"/ws?name="+Uri.EscapeDataString(playerName)+"&key="+Uri.EscapeDataString(playerKey)+"&clientRules="+GameRules.Version;
#if UNITY_WEBGL && !UNITY_EDITOR
            LC_Connect(url,gameObject.name);
#else
            cancellation=new CancellationTokenSource();socket=new ClientWebSocket();ConnectNative(url,cancellation.Token);
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        async void ConnectNative(string url,CancellationToken token)
        {
            try
            {
                var ws=socket;await ws.ConnectAsync(new Uri(url),token);statuses.Enqueue("@open");
                var buffer=new byte[8192];
                while(ws.State==WebSocketState.Open&&!token.IsCancellationRequested)
                {
                    var text=new System.IO.MemoryStream();WebSocketReceiveResult result;
                    do
                    {
                        result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),token);
                        if(result.MessageType==WebSocketMessageType.Close){statuses.Enqueue("@closed");return;}
                        text.Write(buffer,0,result.Count);
                        if(text.Length>2_000_000)throw new Exception("Snapshot too large");
                    }while(!result.EndOfMessage);
                    incoming.Enqueue(Encoding.UTF8.GetString(text.ToArray()));text.Dispose();
                }
                statuses.Enqueue("@closed");
            }
            catch(OperationCanceledException){}catch(Exception e){statuses.Enqueue(e.Message);}
        }
        async void SendNative(string json)
        {
            bool acquired=false;
            try
            {
                await sendLock.WaitAsync();acquired=true;
                if(socket!=null&&socket.State==WebSocketState.Open)
                {var bytes=Encoding.UTF8.GetBytes(json);await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,cancellation.Token);}
            }
            catch(Exception e){statuses.Enqueue(e.Message);}finally{if(acquired)sendLock.Release();}
        }
#endif
        public void Send(NetPacket packet)
        {
            if(!Connected)return;string json=JsonUtility.ToJson(packet);
#if UNITY_WEBGL && !UNITY_EDITOR
            LC_Send(json);
#else
            SendNative(json);
#endif
        }
        public void OnSocketMessage(string json){incoming.Enqueue(json);}
        public void OnSocketStatus(string value){statuses.Enqueue(value);}
        void Update()
        {
            while(statuses.TryDequeue(out var state)){Connected=state=="@open";Status?.Invoke(state);}
            // Bound per-frame processing to keep rendering responsive during reconnect/snapshot.
            for(int i=0;i<64&&incoming.TryDequeue(out var json);i++)
            {
                try {Message?.Invoke(JsonUtility.FromJson<NetPacket>(json));}
                catch(Exception e){Status?.Invoke("Invalid server packet: "+e.Message);}
            }
        }
        void OnDestroy()
        {
            Connected=false;
#if UNITY_WEBGL && !UNITY_EDITOR
            LC_Close();
#else
            cancellation?.Cancel();socket?.Abort();socket?.Dispose();cancellation?.Dispose();
#endif
        }
    }
}
