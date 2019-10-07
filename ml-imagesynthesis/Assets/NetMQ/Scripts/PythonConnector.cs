using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

public class PythonConnector
{
    
    private RequestSocket client;
    private string ServerEndpoint;
    // Start is called before the first frame update
    public void start(int port)
    {
        ServerEndpoint = $"tcp://localhost:{port}";

        ForceDotNet.Force();
        Debug.Log($"C: Connecting to server...{ServerEndpoint}");

        client = new RequestSocket();
        client.Connect(ServerEndpoint);
        //client.Options.Linger = TimeSpan.Zero;
        //client.ReceiveReady += ClientOnReceiveReady;
    }

    // Update is called once per frame
    public void send(string fileName)
    {
        client.SendFrame(fileName);
        Debug.Log($"C: Frame sent...{fileName}");
    }

    public void sendMore(string fileName,string gtKeyPoints, string keypoints, bool control)
    {
        if(control){
            client.SendMoreFrame(fileName).SendMoreFrame(gtKeyPoints).SendMoreFrame(keypoints).SendFrame("True");
        }else{
            client.SendMoreFrame(fileName).SendMoreFrame(gtKeyPoints).SendFrame(keypoints);
        }
        Debug.Log($"C: Frame sent...{fileName}");
    }

    public string recieve()
    {
        string message = client.ReceiveFrameString();
        Debug.Log($"C: Frame recieved...{message}");
        return message;
    }

    public void close()
    {
        client.Disconnect(ServerEndpoint);
        client.Close();
        Debug.Log($"C: Frame disconnected...{ServerEndpoint}");
    }
}
