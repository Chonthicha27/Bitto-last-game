using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

public class LocalMultiplayer : MonoBehaviour
{
    // ตัวแปรสำหรับ UI
    public GameObject dotPrefab; // Prefab จุดสีเขียวที่แสดงใน UI
    public Transform devicesPanel; // UI Panel ที่เก็บจุดสีเขียว
    public Button startButton; // ปุ่มกดเริ่มเกม

    // ตัวแปรสำหรับการเชื่อมต่อ UDP
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private HashSet<string> connectedDevices = new HashSet<string>(); // ใช้เก็บ IP ของเครื่องที่เชื่อมต่อแล้ว
    private bool isStarted = false; // เช็คว่าเกมเริ่มหรือยัง

    // ตัวแปรสำหรับการแสดงผล IP
    public TextMeshProUGUI ipText; // ข้อความแสดง IP ของเครื่องตัวเอง
    public TextMeshProUGUI connectedText; // ข้อความแสดง IP ของเครื่องที่เชื่อมต่อ

    // เริ่มต้นเมื่อเริ่มเกม
    private void Start()
    {
        // ตั้งค่า UdpClient สำหรับการส่งและรับข้อมูลผ่าน UDP
        udpClient = new UdpClient();
        udpClient.EnableBroadcast = true; // เปิดการ Broadcast
        remoteEndPoint = new IPEndPoint(IPAddress.Broadcast, 8888); // กำหนดจุดหมายปลายทางในการ Broadcast

        // กำหนดให้ปุ่มเริ่มเกมเรียกฟังก์ชัน StartMultiplayer เมื่อกด
        startButton.onClick.AddListener(StartMultiplayer);
    }

    // ฟังก์ชันเริ่มเกม
    private void StartMultiplayer()
    {
        if (isStarted) return; // ถ้าเกมเริ่มแล้วจะไม่ทำอะไร

        isStarted = true; // เปลี่ยนสถานะว่าเกมเริ่มแล้ว
        startButton.interactable = false; // ปิดปุ่มเริ่มเกมไม่ให้กดซ้ำ
        
        // แสดง IP ของเครื่องตัวเองใน UI
        ipText.text = "My IP: " + GetLocalIPAddress();

        // เริ่มการ Broadcast IP ทุกๆ 2 วิ
        InvokeRepeating("BroadcastIP", 0f, 2f);
        // อัปเดต UI ทุกๆ 1 วิ
        InvokeRepeating("UpdateUI", 0f, 1f);

        // ฟังก์ชันเริ่มรับข้อมูลจากเครื่องอื่นที่เชื่อมต่อ
        UdpClient listener = new UdpClient(8888);
        listener.BeginReceive(new System.AsyncCallback(ReceiveData), listener);
    }

    // ฟังก์ชันที่ใช้ในการ Broadcast IP
    private void BroadcastIP()
    {
        // สร้างข้อความ "DISCOVER:" ตามด้วย IP ของเครื่อง
        string message = "DISCOVER:" + GetLocalIPAddress();
        byte[] data = Encoding.UTF8.GetBytes(message); // แปลงข้อความเป็นข้อมูลในรูปแบบ byte[]
        
        // ส่งข้อมูลไปยัง Broadcast endpoint (ทุกเครื่องในเครือข่าย)
        udpClient.Send(data, data.Length, remoteEndPoint);
    }

    // ฟังก์ชันที่ใช้ในการรับข้อมูลที่ส่งมา
    private void ReceiveData(System.IAsyncResult ar)
    {
        // รับข้อมูลจากการเชื่อมต่อ UDP
        UdpClient listener = (UdpClient)ar.AsyncState;
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 8888);
        byte[] bytes = listener.EndReceive(ar, ref groupEP);
        string receivedMessage = Encoding.UTF8.GetString(bytes); // แปลงข้อมูล byte[] เป็นข้อความ

        // ถ้าข้อความที่ได้รับเริ่มต้นด้วย "DISCOVER:" แสดงว่าเป็นการค้นหา IP
        if (receivedMessage.StartsWith("DISCOVER:"))
        {
            // แยก IP ออกมาจากข้อความ
            string deviceIP = receivedMessage.Split(':')[1];
            
            // 👇 ข้ามถ้าเป็น IP ตัวเอง
            if (deviceIP == GetLocalIPAddress())
                return;
            
            // ถ้ายังไม่เคยเชื่อมต่อกับ IP นี้
            if (!connectedDevices.Contains(deviceIP))
            {
                // เพิ่ม IP ลงใน HashSet
                connectedDevices.Add(deviceIP);
                Debug.Log("New Device Connected: " + deviceIP); // แสดงใน Console ว่าเครื่องใหม่เชื่อมต่อ
            }
        }

        // เรียกฟังก์ชันรับข้อมูลอีกครั้ง
        listener.BeginReceive(new System.AsyncCallback(ReceiveData), listener);
        
        Debug.Log("Received: " + receivedMessage + " from " + groupEP.Address);
    }

    // ฟังก์ชันที่ใช้ในการอัปเดต UI
    private void UpdateUI()
    {
        // ลบจุดสีเขียวเก่าทั้งหมดจาก Panel
        foreach (Transform child in devicesPanel)
        {
            Destroy(child.gameObject);
        }
        
        // สร้างข้อความแสดง IP ที่เชื่อมต่อ
        StringBuilder ipList = new StringBuilder("Connected: \n");
        
        // 👇 เพิ่มจุดของเครื่องเราเอง
        Instantiate(dotPrefab, devicesPanel);
        ipList.AppendLine(GetLocalIPAddress() + " (me)");

        // สร้างจุดสีเขียวสำหรับทุกเครื่องที่เชื่อมต่อ
        foreach (string device in connectedDevices)
        {
            Instantiate(dotPrefab, devicesPanel); // สร้างจุดสีเขียวใน Panel
            ipList.AppendLine(device); // เพิ่ม IP ของเครื่องที่เชื่อมต่อในข้อความ
        }
        
        // แสดงข้อความใน UI
        connectedText.text = ipList.ToString();
    }

    // ฟังก์ชันที่ใช้ในการหาค่า IP ของเครื่องตัวเอง
    private string GetLocalIPAddress()
    {
        // ลูปผ่าน IP ทุกตัวในเครื่องเพื่อหาที่เป็น IPv4
        foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString(); // คืนค่า IP ของเครื่องตัวเอง
            }
        }
        return "127.0.0.1"; // ถ้าไม่พบคืนค่า IP เริ่มต้น
    }
}
