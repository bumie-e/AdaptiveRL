
using UnityEngine;
using UnityEngine.Networking; 
using System.Collections; 
using System.IO; 

public class DroneAgentDataCollection : Agent
{
    public Transform Target;
    public int maxSteps = 5000;
    public float forceMultiplier = 3f;
    public Camera droneCamera;
    public bool enableDataCollection = true;
    private int stepCounter = 0;
    private Vector3 lastLinearVelocity;
    private Vector3 localAcceleration;
    public float ProximityPenalty = -0.01f;
    public float PlasticDetectionReward = 1f;
    public float RewardNormalisationFactor = 0.3f;

    public Shader segmentationShader;

    Rigidbody rBody;
    void Start () {
        rBody = GetComponent<Rigidbody>();
    }
    
    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(0, 17, -40);
        rBody.angularVelocity = Vector3.zero;
        rBody.linearVelocity = Vector3.zero;
        lastLinearVelocity = Vector3.zero;

        GameObject[] wastes = GameObject.FindGameObjectsWithTag("Plastic");
        foreach (GameObject waste in wastes)
        {
            if (!waste.activeSelf)
            {
                waste.SetActive(true);
            }
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

    private void OnCollisionEnter(Collision collision)
    {

    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        Vector3 controlSignal = Vector3.zero;
        controlSignal.x = actionBuffers.ContinuousActions[0];
        controlSignal.z = actionBuffers.ContinuousActions[1];
        rBody.AddForce(controlSignal * forceMultiplier);

        if (StepCount >= maxSteps)
        {
            EndEpisode();
        }
        
        stepCounter++;
        if (enableDataCollection && stepCounter % 5 == 0)
        {
            TakePicture();
        }
    }

    private void TakePicture()
    {
        if (droneCamera == null)
        {
            Debug.LogWarning("Drone camera is not assigned!");
            return;
        }

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
        {
            cam.RenderWithShader(replacementShader, "RenderType");
        }
        else
        {
            cam.Render();
        }

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
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
    }
}
