using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    string stateIn = "";

    // Start is called before the first frame update
    void Start()
    {
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
                    if (Username == msgSplit[2])
                    {
                        SendMessageToClient("Username is already used", id);
                        break;
                    }
                    else
                    {
                        StreamWriter File = new StreamWriter("UserData.txt", true);
                        File.WriteLine(UserData);
                        File.Close();
                        Debug.Log("Signing in...");
                        SendMessageToClient("Account Created", id);
                        break;
                    }
                }
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
                Debug.Log("Nothing");
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

}