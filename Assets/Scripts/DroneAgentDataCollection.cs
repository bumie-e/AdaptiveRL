
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;

public class DroneAgentDataCollection : Agent
{
    public Transform Target;
    public float forceMultiplier = 20f;
    public float torqueMultiplier = 2f;
    public int maxSteps = 5000;
    public Camera droneCamera;
    public bool enableDataCollection = true;
    
    [Header("Data Collection")]
    public Shader segmentationShader;
    private int stepCounter = 0;

    private Vector3 lastLinearVelocity;
    private Vector3 localAcceleration;
    Rigidbody rBody;

    void Start () {
        rBody = GetComponent<Rigidbody>();
    }
    
    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(0, 17, -40);
        transform.rotation = Quaternion.Euler(0, 0, 0);
        rBody.angularVelocity = Vector3.zero;
        rBody.linearVelocity = Vector3.zero;
        lastLinearVelocity = Vector3.zero;

        GameObject[] wastes = GameObject.FindGameObjectsWithTag("Plastic");
        foreach (GameObject waste in wastes)
        {
            if (!waste.activeSelf) waste.SetActive(true);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(rBody.linearVelocity);
        sensor.AddObservation(this.transform.localRotation); 
        
        Vector3 localAngularVel = transform.InverseTransformDirection(rBody.angularVelocity);
        sensor.AddObservation(localAngularVel);

        Vector3 currentLinearVelocity = rBody.linearVelocity;
        localAcceleration = (currentLinearVelocity - lastLinearVelocity) / Time.fixedDeltaTime;
        lastLinearVelocity = currentLinearVelocity;
        sensor.AddObservation(transform.InverseTransformDirection(localAcceleration));
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 4 Actions: [0] Throttle, [1] Pitch, [2] Roll, [3] Yaw
        float throttle = actionBuffers.ContinuousActions[0];
        float pitch = actionBuffers.ContinuousActions[1];
        float roll = actionBuffers.ContinuousActions[2];
        float yaw = actionBuffers.ContinuousActions[3];

        rBody.AddForce(transform.up * throttle * forceMultiplier);
        rBody.AddForce(transform.forward * pitch * forceMultiplier);
        rBody.AddForce(transform.right * roll * forceMultiplier);
        rBody.AddTorque(transform.up * yaw * torqueMultiplier);

        AddReward(-0.0001f);

        if (StepCount >= maxSteps) EndEpisode();
        
        stepCounter++;
        if (enableDataCollection && stepCounter % 5 == 0)
        {
            TakePicture();
        }
    }




    private void TakePicture()
    {
        if (droneCamera == null) return;

        string timestamp = $"{Time.frameCount}_{System.DateTime.Now:HHmmss}";
        string dataPath = Path.Combine(Application.dataPath, "Data");
        string imagesPath = Path.Combine(dataPath, "images");
        string masksPath = Path.Combine(dataPath, "masks");

        if (!Directory.Exists(imagesPath)) Directory.CreateDirectory(imagesPath);
        if (!Directory.Exists(masksPath)) Directory.CreateDirectory(masksPath);

        CaptureCameraView(droneCamera, Path.Combine(imagesPath, $"img_{timestamp}.png"), null);
        CaptureCameraView(droneCamera, Path.Combine(masksPath, $"mask_{timestamp}.png"), segmentationShader);
    }

    private void CaptureCameraView(Camera cam, string fullPath, Shader replacementShader)
    {
        RenderTexture rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;

        if (replacementShader != null)
            cam.RenderWithShader(replacementShader, "RenderType");
        else
            cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        screenShot.Apply();

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);

        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(screenShot);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        
        float throttle = 0;
        if (Input.GetKey(KeyCode.Space)) throttle = 1;
        if (Input.GetKey(KeyCode.LeftShift)) throttle = -1;
        continuousActions[0] = throttle;

        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = Input.GetAxis("Horizontal");

        float yaw = 0;
        if (Input.GetKey(KeyCode.Q)) yaw = -1;
        if (Input.GetKey(KeyCode.E)) yaw = 1;
        continuousActions[3] = yaw;
    }
}
