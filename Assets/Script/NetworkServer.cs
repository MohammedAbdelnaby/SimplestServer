using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;
using System.Linq;
using System;

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
        ChangeState(msg);
        switch (stateIn)
        {
            case "Signup":
                StreamWriter File = new StreamWriter("UserData.txt");
                File.Write(msgSplit[1] + "," + msgSplit[2]);
                File.Close();
                break;
            case "Login":
                foreach (string Login in lines)
                {
                    if (Login != msg)
                    {
                        SendMessageToClient("Wrong Username/Password", id);
                    }
                    else
                    {
                        SendMessageToClient("Logged in!", id);
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
            case "SignUp":
                stateIn = State;
                break;
            case "Loging":
                stateIn = State;
                break;
            default:
                break;

        }
    }

}