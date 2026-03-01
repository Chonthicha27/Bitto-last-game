using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

public class LocalMultiplayerUnicast : MonoBehaviour
{
    public GameObject dotPrefab;
    public Transform devicesPanel;
    public Button startButton;
    public TMP_InputField targetIPInput; // ใส่ IP ของเครื่องอีกฝั่ง
    public TextMeshProUGUI connectedText;

    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private HashSet<string> connectedDevices = new HashSet<string>();
    private bool isStarted = false;

    private void Start()
    {
        udpClient = new UdpClient(8888); // ใช้ port เดียวทั้งส่ง/รับ
        udpClient.BeginReceive(ReceiveData, udpClient);

        startButton.onClick.AddListener(StartMultiplayer);
        
        // 🔹 แสดง IP ของตัวเองตอนเริ่มเลย
        UpdateUI();
    }

    private void StartMultiplayer()
    {
        if (isStarted) return;
        isStarted = true;
        startButton.interactable = false;

        // อ่าน IP เครื่องอีกฝั่งจาก InputField
        string targetIP = targetIPInput.text;
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(targetIP), 8888);

        // เริ่มส่งข้อความทุก 2 วิ
        InvokeRepeating("SendPing", 0f, 2f);
        InvokeRepeating("UpdateUI", 0f, 1f);
    }

    private void SendPing()
    {
        string message = "HELLO:" + GetLocalIPAddress();
        byte[] data = Encoding.UTF8.GetBytes(message);
        udpClient.Send(data, data.Length, remoteEndPoint);
        Debug.Log("Sent to " + remoteEndPoint.Address + ": " + message);
    }

    private void ReceiveData(System.IAsyncResult ar)
    {
        UdpClient client = (UdpClient)ar.AsyncState;
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 8888);

        byte[] bytes = client.EndReceive(ar, ref groupEP);
        string receivedMessage = Encoding.UTF8.GetString(bytes);

        if (receivedMessage.StartsWith("HELLO:"))
        {
            string deviceIP = receivedMessage.Split(':')[1];
            if (!connectedDevices.Contains(deviceIP))
            {
                connectedDevices.Add(deviceIP);
                Debug.Log("Connected with: " + deviceIP);
            }
        }

        client.BeginReceive(ReceiveData, client);
    }

    private void UpdateUI()
    {
        foreach (Transform child in devicesPanel)
        {
            Destroy(child.gameObject);
        }

        StringBuilder sb = new StringBuilder("Connected:\n");

        Instantiate(dotPrefab, devicesPanel);
        sb.AppendLine(GetLocalIPAddress() + " (me)");

        foreach (string device in connectedDevices)
        {
            Instantiate(dotPrefab, devicesPanel);
            sb.AppendLine(device);
        }

        connectedText.text = sb.ToString();
    }

    private string GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}
