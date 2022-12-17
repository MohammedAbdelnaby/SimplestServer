using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public struct Player
{
    public int ID;
    public bool IsPlayersTurn;
}


public struct GameRoom
{
    public GameRoom(string pname, int ID1 = 0, int ID2 = 0) 
    {
        name = pname;
        PlayerX.ID = ID1;
        PlayerO.ID = ID2;
        PlayerX.IsPlayersTurn = true;
        PlayerO.IsPlayersTurn = false;
        GameState = null;
    }
    public string name;
    public Player PlayerX;
    public Player PlayerO;
    public string GameState;
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
        Debug.Log(GameRooms.Count);
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
                PlayTile(msg, id);
                SwapTurns(msgSplit[1]);
                break;
            case "Update Game":
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
                    UpdateGame("PlayTile," + (GameRooms.Find((GameRoom Room) => Room.name == msgSplit[1])).name, id);
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

    private void PlayTile(string msg, int id)
    {
        Debug.Log(msg);
        string[] msgSplit = msg.Split(',');
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.PlayerX.ID == id || Room.PlayerO.ID == id);
        GameRooms.Remove(room);
        room.GameState = msgSplit[2];
        GameRooms.Add(room);
        SendMessageToClient("Opponent," + msgSplit[2], room.PlayerX.ID);
        SendMessageToClient("Opponent," + msgSplit[2], room.PlayerO.ID);
        SendMessageToClient("Is player turn", ((room.PlayerX.IsPlayersTurn)) ? room.PlayerX.ID : room.PlayerO.ID);
    }

    private void UpdateGame(string msg, int id)
    {
        Debug.Log(msg);
        string[] msgSplit = msg.Split(',');
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.PlayerX.ID == id || Room.PlayerO.ID == id);
        SendMessageToClient("Opponent," + room.GameState, room.PlayerX.ID);
        SendMessageToClient("Opponent," + room.GameState, room.PlayerO.ID);
        if (room.PlayerX.ID > 0 && room.PlayerO.ID > 0)
        {
            SendMessageToClient("Is player turn", ((room.PlayerX.IsPlayersTurn)) ? room.PlayerX.ID : room.PlayerO.ID);
            SwapTurns(room.name);
        }
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

    private void UpdateGameRooms(string GameRoomName, int NewID)
    {
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.name == GameRoomName);
        GameRooms.Remove(room);
        if (room.PlayerX.ID == 0)
        {
            room.PlayerX.ID = NewID;
        }
        else if (room.PlayerO.ID == 0)
        {
            room.PlayerO.ID = NewID;
        }
        GameRooms.Add(room);
    }

    private void LeaveRoom(string GameRoomName, int ID)
    {
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.PlayerX.ID == ID || Room.PlayerO.ID == ID);
        GameRooms.Remove(room);
        if (room.PlayerX.ID == ID)
        {
            room.PlayerX.ID = 0;
        }
        else if (room.PlayerO.ID == ID)
        {
            room.PlayerO.ID = 0;
        }
        GameRooms.Add(room);
    }

    private void SwapTurns(string name)
    {
        GameRoom room = GameRooms.Find((GameRoom Room) => Room.name == name);
        GameRooms.Remove(room);
        room.PlayerX.IsPlayersTurn = !room.PlayerX.IsPlayersTurn;
        room.PlayerO.IsPlayersTurn = !room.PlayerO.IsPlayersTurn;
        GameRooms.Add(room);
    }

}