using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;
using UnityEngine;

public class UDPReceiverTest : MonoBehaviour
{
    public int port = 5055;

    public Transform targetJoint;

    public enum Axis { X, Y, Z }
    public Axis axis = Axis.Z;

    public float inputMin = 0f;
    public float inputMax = 90f;

    public float outputMin = 0f;
    public float outputMax = -90f;

    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.25f;

    private UdpClient client;
    private Thread receiveThread;
    private readonly object dataLock = new object();

    private float targetValue = 0f;
    private float currentAngle = 0f;
    private Quaternion initialRotation;

    void Start()
    {
        if (targetJoint != null)
        {
            initialRotation = targetJoint.localRotation;
        }

        client = new UdpClient(port);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref ep);
                string text = Encoding.UTF8.GetString(data);

                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    lock (dataLock)
                    {
                        targetValue = value;
                    }
                }
            }
            catch { }
        }
    }

    void Update()
    {
        if (targetJoint == null) return;

        float rawValue;

        lock (dataLock)
        {
            rawValue = targetValue;
        }

        float normalized = Mathf.InverseLerp(inputMin, inputMax, rawValue);
        float targetAngle = Mathf.Lerp(outputMin, outputMax, normalized);

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, smoothSpeed);

        Quaternion offsetRotation;

        if (axis == Axis.X)
            offsetRotation = Quaternion.Euler(currentAngle, 0f, 0f);
        else if (axis == Axis.Y)
            offsetRotation = Quaternion.Euler(0f, currentAngle, 0f);
        else
            offsetRotation = Quaternion.Euler(0f, 0f, currentAngle);

        targetJoint.localRotation = initialRotation * offsetRotation;
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }

    void OnDestroy()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}