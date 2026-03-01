using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class P2PManager : MonoBehaviour
{
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField ipInput;
    public TextMeshProUGUI ipText;

    public GameObject connectedObjPrefab; // Prefab ของจุดที่จะโชว์
    public Transform panelParent;         // Panel ที่จะเอา Obj ไปเรียง

    private TcpListener listener;
    private TcpClient client;
    private Thread netThread;
    private bool isHosting = false; 
    private bool addHostObj = false;
    private bool addClientObj = false;

    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
    }

    void Update()
    {
        // สร้างจุดของ Host
        if (addHostObj)
        {
            addHostObj = false;
            AddConnectedObj("Host");
        }

        // สร้างจุดของ Client
        if (addClientObj)
        {
            addClientObj = false;
            AddConnectedObj("Client");
        }
    }

    void AddConnectedObj(string who)
    {
        GameObject newObj = Instantiate(connectedObjPrefab, panelParent);
        newObj.SetActive(true);

        // ตั้งชื่อให้อ่านง่าย (Host/Client)
        TextMeshProUGUI label = newObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = who;
        }
    }

    // ================= HOST =================
    void StartHost()
    {
        if (isHosting) return; // ป้องกันกดซ้ำ

        string localIP = GetLocalIPAddress();
        ipText.text = "Host IP: " + localIP;

        // แสดงจุดของ Host ตัวเองทันที
        addHostObj = true;

        isHosting = true;
        hostButton.interactable = false; // ปิดปุ่ม Host

        netThread = new Thread(() =>
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 7777);
                listener.Start();

                client = listener.AcceptTcpClient(); // รอ Client ต่อเข้ามา
                Debug.Log("Client connected!");

                // อ่านข้อความจาก Client
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (msg == "CLIENT_CONNECTED")
                {
                    addClientObj = true;

                    // ส่งกลับไปว่า Host อยู่แล้ว
                    byte[] reply = Encoding.UTF8.GetBytes("HOST_CONNECTED");
                    stream.Write(reply, 0, reply.Length);
                }
            }
            catch (Exception e)
            {
                Debug.Log("Host Error: " + e.Message);
            }
        });
        netThread.IsBackground = true;
        netThread.Start();
    }

    // ================= CLIENT =================
    void StartClient()
    {
        string ip = ipInput.text;

        netThread = new Thread(() =>
        {
            try
            {
                client = new TcpClient(ip, 7777);
                Debug.Log("Connected to Host!");

                NetworkStream stream = client.GetStream();

                // บอก Host ว่ามี Client ต่อมาแล้ว
                byte[] msg = Encoding.UTF8.GetBytes("CLIENT_CONNECTED");
                stream.Write(msg, 0, msg.Length);

                // สร้างจุด Client ของตัวเอง
                addClientObj = true;

                // รอข้อความตอบกลับจาก Host
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string reply = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (reply == "HOST_CONNECTED")
                {
                    addHostObj = true;
                }
            }
            catch (Exception e)
            {
                Debug.Log("Join Error: " + e.Message);
            }
        });
        netThread.IsBackground = true;
        netThread.Start();
    }

    // ================= UTILS =================
    string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString(); // IPv4 address
            }
        }
        return "No IPv4 found";
    }


    private void OnApplicationQuit()
    {
        try
        {
            if (listener != null) listener.Stop();
            if (client != null) client.Close();
            if (netThread != null && netThread.IsAlive) netThread.Abort();
        }
        catch { }
    }
}

