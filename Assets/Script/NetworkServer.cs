using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public struct GameRoom
{
    public GameRoom(string pname, int ID1, int ID2) 
    {
        name = pname;
        opponent1ID = ID1;
        opponent2ID = ID2;
    }
    public string name;
    public int opponent1ID;
    public int opponent2ID;
}

public class NetworkServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    string stateIn = "";
    public List<GameRoom> GameRooms;

    // Start is called before the first frame update
    void Start()
    {
        GameRooms = new List<GameRoom>();
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);
        string[] msgSplit = msg.Split(',');
        switch (msgSplit[0])
        {
            case "Create":
                CreateAndJoinRooms(msg, id);
                break;
            case "Join":
                CreateAndJoinRooms(msg, id);
                break;
            case "Leave":
                CreateAndJoinRooms(msg, id);
                break;
            case "PlayTile":
                UpdateGame(msg, id);
                break;
            default:
                SigninAndSignUp(msg, id);
                break;
        }
    }

    private void CreateAndJoinRooms(string msg, int id)
    {
        string[] msgSplit = msg.Split(',');
        switch (msgSplit[0])
        {
            case "Create":
                {
                    if ((GameRooms.Find((GameRoom Room) => Room.name == msgSplit[1])).name == null)
                    {
                        GameRooms.Add(new GameRoom(msgSplit[1], id, 0));
                        SendMessageToClient("Room Created", id);
                        break;
                    }
                    SendMessageToClient("That name is used.", id);
                    break;
                }
            case "Join":
                if ((GameRooms.Find((GameRoom Room) => Room.name == msgSplit[1])).name != null)
                {
                    UpdateGameRooms(msgSplit[1], id);
                    SendMessageToClient("Joined", id);
                    break;
                }
                SendMessageToClient("Cant Join Room", id);
                break;
            case "Leave":
                LeaveRoom(msgSplit[1], id);
                SendMessageToClient("Leave", id);
                break;
            default:
                break;
        }
    }

    private void UpdateGame(string msg, int id)
    {
        string[] msgSplit = msg.Split(',');
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.opponent1ID == id || Room.opponent2ID == id);
        SendMessageToClient("Opponent," + msgSplit[2], (room.opponent1ID == id) ? room.opponent2ID : room.opponent1ID);
    }

    private void SigninAndSignUp(string msg, int id)
    {
        var path = Directory.GetCurrentDirectory();
        string[] lines = System.IO.File.ReadAllLines(path + @"/UserData.txt");
        string[] msgSplit = msg.Split(',');
        ChangeState(msgSplit[0]);
        Debug.Log(stateIn);
        string UserData = msgSplit[1] + "," + msgSplit[2];
        switch (stateIn)
        {
            case "Signup":
                foreach (var Accounts in lines)
                {
                    string[] temp = Accounts.Split(',');
                    string Username = temp[0];
                    if (Username == msgSplit[1])
                    {
                        SendMessageToClient("Username is already used", id);
                        return;
                    }
                }
                StreamWriter File = new StreamWriter("UserData.txt", true);
                File.WriteLine(UserData);
                File.Close();
                Debug.Log("Signing in...");
                SendMessageToClient("Account Created", id);
                break;
            case "Login":
                foreach (string Login in lines)
                {
                    string[] temp = Login.Split(',');
                    string Username = temp[0];
                    string Password = temp[1];
                    if (Username != msgSplit[1])
                    {
                        SendMessageToClient("Wrong Username", id);
                        Debug.Log("Wrong Username");
                    }
                    else if (Password != msgSplit[2])
                    {
                        SendMessageToClient("Wrong Password", id);
                        Debug.Log("Wrong Password");
                    }
                    else if (Login == UserData)
                    {
                        SendMessageToClient("Logged in!", id);
                        Debug.Log("Loging in...");
                    }
                }
                break;
            default:
                break;
        }
    }

    private void ChangeState(string State)
    {
        switch (State)
        {
            case "True":
                stateIn = "Signup";
                break;
            case "False":
                stateIn = "Login";
                break;
            default:
                break;
        }
    }

    private void ChangePlayerCount(string NewLine, int OldLine, string FileName)
    {
        string[] arrLine = File.ReadAllLines(FileName);
        arrLine[OldLine - 1] = NewLine;
        File.WriteAllLines(FileName, arrLine);
    }

    private void UpdateGameRooms(string GameRoomName, int NewID)
    {
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.name == GameRoomName);
        GameRooms.Remove(room);
        if (room.opponent1ID == 0)
        {
            room.opponent1ID = NewID;
        }
        else if (room.opponent2ID == 0)
        {
            room.opponent2ID = NewID;
        }
    }

    private void LeaveRoom(string GameRoomName, int ID)
    {
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.opponent1ID == ID || Room.opponent2ID == ID);
        GameRooms.Remove(room);
        if (room.opponent1ID == ID)
        {
            room.opponent1ID = 0;
        }
        else if (room.opponent2ID == ID)
        {
            room.opponent2ID = 0;
        }
        GameRooms.Add(room);
    }
}