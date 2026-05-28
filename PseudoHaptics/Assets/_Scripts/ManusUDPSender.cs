using System.Net.Sockets;
using System.Text;
using System.Globalization;
using UnityEngine;

public class ManusSender : MonoBehaviour
{
    public string ml2IP = "130.251.13.103";
    public int port = 5055;

    public Transform sourceJoint;

    public enum Axis { X, Y, Z }
    public Axis axis = Axis.Z;

    private UdpClient udpClient;

    void Start()
    {
        udpClient = new UdpClient();
    }

    void Update()
    {
        if (udpClient == null || sourceJoint == null) return;

        Vector3 euler = sourceJoint.localRotation.eulerAngles;

        float raw = axis == Axis.X ? euler.x :
                    axis == Axis.Y ? euler.y :
                    euler.z;

        float angle = raw > 180f ? raw - 360f : raw;

        string message = angle.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(message);

        udpClient.Send(data, data.Length, ml2IP, port);
    }

    void OnApplicationQuit()
    {
        udpClient?.Close();
    }

    void OnDestroy()
    {
        udpClient?.Close();
    }
}